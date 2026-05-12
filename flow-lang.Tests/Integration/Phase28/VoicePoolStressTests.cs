using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase28;

/// <summary>
/// Phase 28 (SPEC-7) Plan 05 integration-level acceptance facts:
///
///   1. <see cref="VoicePool_50OnsetsStealOldest"/> — 50 simultaneous notes
///      under default pool 32 → ~32 voices remain audible; the EARLIEST
///      18 (lowest original index) get truncated.
///   2. <see cref="VoicePool_DeterministicTwoRun"/> — two consecutive
///      AllocateWithPool runs over identical input produce byte-identical
///      mutated buffers (preserves the Phase 18/25/27 two-run-cmp-clean
///      determinism contract).
///   3. <see cref="VoicePool_ExplicitOverride_8"/> — pool size 8 truncates
///      42 of 50 input voices.
///
/// Tests construct <see cref="Voice"/> objects directly to keep the
/// truncation logic isolated from synthesizer/song-renderer behavior.
/// </summary>
public class VoicePoolStressTests
{
    private const int SampleRate = 44100;
    private const double Bpm = 120.0;

    /// <summary>
    /// Builds N voices all sharing onset 0 (worst-case stress for the pool —
    /// every voice competes for an active slot at the same moment). Each voice
    /// gets a 0.5-second mono buffer filled with a 1 kHz sine at 0.5 amplitude
    /// so the truncation/zero-tail behaviour is observable via
    /// <see cref="CountAudibleVoices"/>.
    /// </summary>
    private static List<Voice> BuildSimultaneousOnsetVoices(int count)
    {
        const double durationSec = 0.5;
        int frames = (int)(durationSec * SampleRate);
        var voices = new List<Voice>(count);
        for (int idx = 0; idx < count; idx++)
        {
            var buffer = new AudioBuffer(frames, 1, SampleRate);
            for (int f = 0; f < frames; f++)
            {
                buffer.SetSample(f, 0, (float)(0.5 * Math.Sin(2.0 * Math.PI * 1000.0 * f / SampleRate)));
            }
            // All voices share onset 0 — pool steals by ORIGINAL INDEX (deterministic
            // tiebreaker per AllocateWithPool implementation): smallest idx steals first.
            voices.Add(new Voice(buffer, offsetBeats: 0.0));
        }
        return voices;
    }

    /// <summary>
    /// Counts voices whose buffer has any |sample| above the threshold. Truncated
    /// voices have their tails zeroed plus a 5ms fade-out; with all-onset-zero
    /// voices and a pool of N, exactly N voices retain non-zero samples (the rest
    /// were stolen at onset 0 → truncated to ~0 frames).
    /// </summary>
    private static int CountAudibleVoices(List<Voice> voices)
    {
        const double threshold = 0.001;
        int count = 0;
        foreach (var v in voices)
        {
            for (int f = 0; f < v.Buffer.Frames; f++)
            {
                bool audible = false;
                for (int ch = 0; ch < v.Buffer.Channels; ch++)
                    if (Math.Abs(v.Buffer.GetSample(f, ch)) > threshold) { audible = true; break; }
                if (audible) { count++; break; }
            }
        }
        return count;
    }

    /// <summary>
    /// Returns a flat byte[] copy of the (per-voice, per-sample) buffer state.
    /// Used to compare two runs of AllocateWithPool for byte-identical output.
    /// </summary>
    private static byte[] SerializeVoices(List<Voice> voices)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        foreach (var v in voices)
        {
            writer.Write(v.Buffer.Frames);
            writer.Write(v.Buffer.Channels);
            for (int i = 0; i < v.Buffer.Data.Length; i++)
                writer.Write(v.Buffer.Data[i]);
        }
        return ms.ToArray();
    }

    [Fact]
    public void VoicePool_50OnsetsStealOldest()
    {
        // 50 voices, all onset=0, pool=32. AllocateWithPool processes in order;
        // the FIRST 32 establish the pool; voice 33 evicts voice 0 (smallest idx),
        // voice 34 evicts voice 1, …, voice 49 evicts voice 17. Net: 18 voices
        // (indices 0..17) get truncated to ~0 frames; 32 voices (indices 18..49)
        // remain audible.
        var voices = BuildSimultaneousOnsetVoices(50);
        var result = VoiceAllocator.AllocateWithPool(voices, SampleRate, 32, Bpm);
        Assert.Same(voices, result); // returns original list (mutated in-place)
        int audible = CountAudibleVoices(voices);
        Assert.Equal(32, audible);

        // Verify the SPECIFIC truncated set: voices[0..17] should be zeroed.
        for (int i = 0; i < 18; i++)
        {
            float peak = 0f;
            for (int f = 0; f < voices[i].Buffer.Frames; f++)
                peak = Math.Max(peak, Math.Abs(voices[i].Buffer.GetSample(f, 0)));
            Assert.True(peak < 0.001f, $"voice {i} expected truncated (peak<0.001), got {peak:F4}");
        }
        // And voices[18..49] should remain audible.
        for (int i = 18; i < 50; i++)
        {
            float peak = 0f;
            for (int f = 0; f < voices[i].Buffer.Frames; f++)
                peak = Math.Max(peak, Math.Abs(voices[i].Buffer.GetSample(f, 0)));
            Assert.True(peak > 0.1f, $"voice {i} expected audible (peak>0.1), got {peak:F4}");
        }
    }

    [Fact]
    public void VoicePool_DeterministicTwoRun()
    {
        // Two independent AllocateWithPool runs over equivalent input must produce
        // byte-identical output (preserves the Phase 18/25/27 two-run-cmp-clean
        // contract). Steal-oldest's onset+index sort is deterministic.
        var voicesA = BuildSimultaneousOnsetVoices(50);
        var voicesB = BuildSimultaneousOnsetVoices(50);

        VoiceAllocator.AllocateWithPool(voicesA, SampleRate, 32, Bpm);
        VoiceAllocator.AllocateWithPool(voicesB, SampleRate, 32, Bpm);

        byte[] a = SerializeVoices(voicesA);
        byte[] b = SerializeVoices(voicesB);
        Assert.Equal(a.Length, b.Length);
        Assert.True(a.SequenceEqual(b),
            "two AllocateWithPool runs over identical input must produce byte-identical output");
    }

    [Fact]
    public void VoicePool_ExplicitOverride_8()
    {
        // Pool size 8 over 50 onset=0 voices → 42 truncated, 8 audible
        // (indices 42..49 survive; 0..41 truncated).
        var voices = BuildSimultaneousOnsetVoices(50);
        VoiceAllocator.AllocateWithPool(voices, SampleRate, 8, Bpm);
        int audible = CountAudibleVoices(voices);
        Assert.Equal(8, audible);
    }
}
