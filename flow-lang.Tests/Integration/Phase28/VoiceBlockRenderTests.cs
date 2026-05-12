using System;
using System.IO;
using System.Linq;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Helpers;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Xunit;

namespace FlowLang.Tests.Integration.Phase28;

/// <summary>
/// Phase 28 (SPEC-1) Plan 06 acceptance facts pinning voice-block render
/// behavior end-to-end through both audio (WAV) and MIDI export:
///
///   • <see cref="VoiceBlock_HeldPlusRunning"/> — bandpass-isolate C5..F5
///     band; assert 4 distinct attack transients at quarter-note positions
///     (0, 0.5s, 1.0s, 1.5s) — SPEC-1 acceptance b
///   • <see cref="VoiceBlock_MidiNoteTickPositions"/> — C4 NoteOn at tick 0
///     with NoteOff at 4×TPQN (whole note = 1920 ticks at TPQN 480), and
///     C5..F5 NoteOn/NoteOff pairs at 0/480/960/1440 ticks — SPEC-1
///     acceptance c
/// </summary>
[Collection("FlowScripts")]
public class VoiceBlockRenderTests
{
    private const string Prelude = @"
use ""@std""
use ""@audio""
use ""@notation""
";

    private static string TempPath(string ext, string name) =>
        Path.Combine(Path.GetTempPath(), $"flow_phase28_voiceblock_{name}_{Guid.NewGuid():N}.{ext}");

    private static (AudioBuffer Wav, string MidiPath) RunAndReadOutputs(string flowSource, string testName)
    {
        string wavPath = TempPath("wav", testName);
        string midPath = TempPath("mid", testName);
        string source = flowSource
            .Replace("{{WAVPATH}}", wavPath.Replace("\\", "/"))
            .Replace("{{MIDPATH}}", midPath.Replace("\\", "/"));
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(source);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nsource:\n{source}");
        Assert.True(File.Exists(wavPath), $"writeWav did not produce {wavPath}");
        Assert.True(File.Exists(midPath), $"writeMidi did not produce {midPath}");
        var wav = WavReader.ReadWav(wavPath);
        File.Delete(wavPath);
        return (wav, midPath);
    }

    private const string StrideSource = Prelude + @"
tempo 120 {
    timesig 4/4 {
        Sequence stride = | {voice C4w} {voice C5q D5q E5q F5q} |
        section main { Sequence v = stride }
        Song s = [main]
        Buffer mix = (renderSong s ""organ"")
        (writeWav ""{{WAVPATH}}"" mix)
        (writeMidi ""{{MIDPATH}}"" s)
    }
}
";

    [Fact]
    public void VoiceBlock_HeldPlusRunning()
    {
        // Render | {voice C4w} {voice C5q D5q E5q F5q} |. Verify the running
        // C5..F5 line plays at quarter-note positions 0/0.5/1.0/1.5 sec by
        // bandpass-isolating each individual pitch's fundamental and asserting
        // each one's RMS peaks in its expected 0.5-sec window.
        var (wav, midPath) = RunAndReadOutputs(StrideSource, nameof(VoiceBlock_HeldPlusRunning));
        File.Delete(midPath); // not needed for this test

        // For each running note, compute RMS in narrow per-pitch bands and assert
        // the RMS in its expected 0.5-sec window is > 2× the RMS in any other
        // window. Pitches: C5=523, D5=587, E5=659, F5=698 Hz.
        var pitches = new[]
        {
            ("C5", 510f, 540f, 0.0, 0.5),
            ("D5", 575f, 605f, 0.5, 1.0),
            ("E5", 645f, 680f, 1.0, 1.5),
            ("F5", 685f, 710f, 1.5, 2.0),
        };

        foreach (var (label, lo, hi, expectStart, expectEnd) in pitches)
        {
            var filtered = Filter.Bandpass(wav, lo, hi);
            int sr = wav.SampleRate;
            int expectedStartFrame = (int)(expectStart * sr);
            int expectedEndFrame = (int)(expectEnd * sr);

            double expectedRms = WindowRms(filtered, expectedStartFrame, expectedEndFrame);
            // Check the OTHER three quarter-note windows for this pitch's energy
            double maxOtherRms = 0.0;
            foreach (var (_, _, _, otherStart, otherEnd) in pitches)
            {
                if (Math.Abs(otherStart - expectStart) < 0.01) continue;
                int oStartFrame = (int)(otherStart * sr);
                int oEndFrame = (int)(otherEnd * sr);
                double otherRms = WindowRms(filtered, oStartFrame, oEndFrame);
                if (otherRms > maxOtherRms) maxOtherRms = otherRms;
            }

            Assert.True(expectedRms > 0.001,
                $"{label}: expected RMS in window {expectStart:F1}-{expectEnd:F1}s too small ({expectedRms:E2}) — running note inaudible");
            Assert.True(expectedRms > maxOtherRms,
                $"{label}: RMS in expected window {expectStart:F1}-{expectEnd:F1}s ({expectedRms:F4}) must exceed max RMS in other quarter-note windows ({maxOtherRms:F4})");
        }
    }

    private static double WindowRms(AudioBuffer buf, int startFrame, int endFrame)
    {
        double sumSq = 0.0;
        int count = 0;
        for (int i = startFrame; i < Math.Min(endFrame, buf.Frames); i++)
            for (int ch = 0; ch < buf.Channels; ch++)
            {
                double s = buf.GetSample(i, ch);
                sumSq += s * s;
                count++;
            }
        return count == 0 ? 0.0 : Math.Sqrt(sumSq / count);
    }

    [Fact]
    public void VoiceBlock_MidiNoteTickPositions()
    {
        // Verify SPEC-1 acceptance c: MIDI export of the voice-block stride
        // pattern produces NoteOn/NoteOff pairs at the correct ticks for both
        // the held C4 (tick 0..1920) and the running C5..F5 (ticks 0/480/960/1440).
        var (wav, midPath) = RunAndReadOutputs(StrideSource, nameof(VoiceBlock_MidiNoteTickPositions));
        try
        {
            var midi = MidiFile.Read(midPath);
            // 1 conductor + 1 "v" sequence track (the only sequence).
            Assert.Equal(2, midi.Chunks.Count);
            var trackChunks = midi.Chunks.OfType<TrackChunk>().Skip(1).ToArray();
            Assert.Single(trackChunks);

            var noteOns = trackChunks[0].GetTimedEvents()
                .Where(te => te.Event is NoteOnEvent)
                .Select(te => (Tick: te.Time, Note: (int)(byte)((NoteOnEvent)te.Event).NoteNumber))
                .OrderBy(t => t.Tick)
                .ToArray();

            // Expected: C4=60 at tick 0, plus C5=72/D5=74/E5=76/F5=77 at ticks 0/480/960/1440
            // (TPQN=480, quarter-note = 480 ticks, whole = 1920 ticks).
            // Total NoteOn count: 5 (1 held + 4 running).
            Assert.Equal(5, noteOns.Length);

            // The C4 held note + C5 running note both NoteOn at tick 0
            var atTick0 = noteOns.Where(n => n.Tick == 0).Select(n => n.Note).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { 60, 72 }, atTick0); // C4 + C5 simultaneous

            // D5, E5, F5 at ticks 480, 960, 1440
            Assert.Equal((480L, 74), noteOns.Single(n => n.Tick == 480));
            Assert.Equal((960L, 76), noteOns.Single(n => n.Tick == 960));
            Assert.Equal((1440L, 77), noteOns.Single(n => n.Tick == 1440));

            // C4 NoteOff at tick 1920 (4 quarter notes = 1 whole note)
            var c4Off = trackChunks[0].GetTimedEvents()
                .Where(te => te.Event is NoteOffEvent off && (int)(byte)off.NoteNumber == 60)
                .Single();
            Assert.Equal(1920L, c4Off.Time);
        }
        finally
        {
            if (File.Exists(midPath)) File.Delete(midPath);
        }
    }
}
