using System;
using System.IO;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Helpers;
using Xunit;

namespace FlowLang.Tests.Integration.Phase28;

/// <summary>
/// Phase 28 (SPEC-2) Plan 06 acceptance facts pinning held-note non-truncation
/// in voice-block rendering. The composer's intent for
///     | {voice C2w} {voice C5q D5q E5q F5q} |
/// is that the C2 whole-note SUSTAINS for the full bar while the running
/// C5..F5 line plays on top. Pre-Phase-28 BarRenderer flattened ParallelVoices
/// to a single voice; Phase 28's per-voice rendering preserves the held bass.
///
/// SPEC-2 acceptance: bandpass-isolate the held-note's pitch range; the RMS in
/// the LAST 50ms of the bar must be ≥ 50% of the FIRST 50ms of the bar — i.e.
/// the held note didn't get truncated by the running line above it.
/// </summary>
[Collection("FlowScripts")]
public class HeldNoteRmsTests
{
    private const string Prelude = @"
use ""@std""
use ""@audio""
use ""@notation""
";

    private static string TempWavPath(string name) =>
        Path.Combine(Path.GetTempPath(), $"flow_phase28_heldnote_{name}_{Guid.NewGuid():N}.wav");

    private static AudioBuffer RunAndReadWav(string flowSource, string testName)
    {
        string outPath = TempWavPath(testName);
        string source = flowSource.Replace("{{OUTPATH}}", outPath.Replace("\\", "/"));
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(source);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nsource:\n{source}");
        Assert.True(File.Exists(outPath), $"writeWav did not produce {outPath}");
        var buf = WavReader.ReadWav(outPath);
        File.Delete(outPath);
        return buf;
    }

    /// <summary>
    /// Computes the RMS of <paramref name="buffer"/> over a frame range AFTER
    /// bandpass filtering to the C2 fundamental band (~50-90 Hz). Isolates the
    /// held bass note's energy from the running C5..F5 line on top.
    /// </summary>
    private static double ComputeC2BandRms(AudioBuffer buffer, int startFrame, int endFrame)
    {
        var filtered = Filter.Bandpass(buffer, 50f, 90f);
        double sumSq = 0.0;
        int count = 0;
        for (int i = startFrame; i < Math.Min(endFrame, filtered.Frames); i++)
            for (int ch = 0; ch < filtered.Channels; ch++)
            {
                double s = filtered.GetSample(i, ch);
                sumSq += s * s;
                count++;
            }
        return count == 0 ? 0.0 : Math.Sqrt(sumSq / count);
    }

    [Fact]
    public void HeldNote_NonTruncation()
    {
        // Render the canonical Phase 28 voice-block test pattern: C2 whole note
        // held under a C5..F5 running line. Read back the WAV; bandpass-isolate
        // the C2 fundamental (~65 Hz); compare last-50ms RMS vs first-50ms RMS.
        // SPEC-2 acceptance: lastRms ≥ 0.5 × firstRms.
        // Render with the organ synth: near-instant attack + full sustain
        // (sustain=1.0, release=0.01) means the held note's amplitude is
        // essentially flat across the bar. Piano synth's 0.6-sec decay would
        // make even a non-truncated held note drop below 50% via natural
        // decay alone — the SPEC-2 acceptance is about VOICE-BLOCK
        // PRESERVATION, not synth-envelope decay. Organ isolates the routing.
        const string source = Prelude + @"
tempo 120 {
    timesig 4/4 {
        Sequence stride = | {voice C2w} {voice C5q D5q E5q F5q} |
        section main { Sequence v = stride }
        Song s = [main]
        Buffer mix = (renderSong s ""organ"")
        (writeWav ""{{OUTPATH}}"" mix)
    }
}
";
        var buf = RunAndReadWav(source, nameof(HeldNote_NonTruncation));

        // 50ms window @ 44.1 kHz = 2205 frames. Bar is 2.0 sec = 88200 frames.
        const int windowFrames = 2205;
        Assert.True(buf.Frames >= 88200, $"expected ≥ 88200 frames, got {buf.Frames}");

        // First-50ms (right after attack starts; skip first 5ms of pre-attack
        // ramp-in to avoid envelope startup artefacts).
        const int attackSkipFrames = 220; // ~5ms
        double firstRms = ComputeC2BandRms(buf, attackSkipFrames, attackSkipFrames + windowFrames);

        // Last-50ms (just before bar ends — the held note must still be
        // sustaining here, not truncated by the running C5..F5 line above).
        // Skip the very last few frames where release tail goes to silence.
        const int releaseSkipFrames = 2205; // skip last 50ms of release
        int lastWindowEnd = buf.Frames - releaseSkipFrames;
        double lastRms = ComputeC2BandRms(buf, lastWindowEnd - windowFrames, lastWindowEnd);

        Assert.True(firstRms > 0.001, $"C2 firstRms too small ({firstRms:E2}) — held note inaudible");
        double ratio = lastRms / firstRms;
        Assert.True(ratio >= 0.5,
            $"SPEC-2 held-note non-truncation failed: lastRms ({lastRms:F4}) / firstRms ({firstRms:F4}) = {ratio:F2} — expected ≥ 0.5 (held note must sustain ≥ 50% energy through bar end)");
    }
}
