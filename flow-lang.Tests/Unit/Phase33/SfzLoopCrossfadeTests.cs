using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Sfz;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase33;

/// <summary>
/// Phase 33 Plan 33-06 — SPEC-5 facts pinning the 441-frame equal-power
/// sin/cos loop crossfade. The crossfade is the failure-analyst's flagged
/// worst-case for Phase 34 — a discontinuity at the loop boundary turns a
/// 4-second sustained note into an audible "tick-tick-tick" at the loop
/// rate. These facts gate that.
///
/// Also covers Pitfall 3 (loop_end clamp), the no_loop short-circuit,
/// Phase 28 articulation envelope composition (Staccato vs Legato body
/// length), and SPEC-8 ampeg_attack override.
/// </summary>
[Collection("FlowScripts")]
public class SfzLoopCrossfadeTests : IDisposable
{
    private const int SampleRate = 44100;

    private readonly string _tmpRoot;

    public SfzLoopCrossfadeTests()
    {
        RenderingDiagnostics.ResetForTesting();
        _tmpRoot = Path.Combine(Path.GetTempPath(), $"sfz-loop-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tmpRoot);
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        try { Directory.Delete(_tmpRoot, recursive: true); } catch { /* swallow */ }
    }

    [Fact]
    public void DiscontinuityCheck_LoopContinuous_HasNoAudibleClick()
    {
        // SPEC-5 gate: 4-second sustained sine over a 1-second source with
        // loop_continuous; max |output[i+1] - output[i]| across the body must
        // be ≤ 0.05 (an audible click would show |Δ| > 0.1 at the loop seam).
        //
        // Sample shape: 22050-frame (500 ms) sine at 220 Hz, loop_start=11025
        // (mid-sample), loop_end=22050 (end). The loop length is 11025 frames
        // — over a 4-second render, the loop body repeats roughly 7 times,
        // exercising the crossfade at every seam.
        string wavPath = Path.Combine(_tmpRoot, "sustain.wav");
        SfzRegionMatchTests.WriteSineWav(wavPath, frequencyHz: 220.0, frames: 22050);

        var region = new SfzRegion(
            "sustain.wav", PitchKeycenter: 60,
            LoKey: 0, HiKey: 127,
            LoVel: 1, HiVel: 127,
            LoopMode: SfzLoopMode.LoopContinuous,
            LoopStart: 11025, LoopEnd: 22050,
            AmpegAttack: 0.0, AmpegRelease: 0.0,
            Volume: 1.0, Pan: 0.0);
        var patch = BuildPatch(_tmpRoot, "loop-cont", region);

        var cache = new SfzSampleCache();
        EagerLoadDirect(cache, patch);
        var renderer = new SfzRenderer(cache);

        // 4 seconds at 60 bpm = 4 beats; render at MIDI 60 (no varispeed shift).
        var note = new MusicalNoteData('C', 4, 0, durationValue: 4, isRest: false, velocity: 0.5);
        var buf = renderer.Render(note, SampleRate, durationBeats: 4.0, bpm: 60.0, patch);

        Assert.True(buf.Frames > SampleRate * 3, "4-second render must produce > 3s of frames.");

        // Walk the body (skip Phase 28's attack/release ramps which intentionally
        // ramp from 0 → 1 and back). The body window is the middle 80% of frames
        // — well inside the sustain plateau where any audible click would be
        // crossfade-induced, not envelope-induced.
        int start = (int)(buf.Frames * 0.1);
        int end   = (int)(buf.Frames * 0.9);
        double maxDelta = 0.0;
        for (int i = start + 1; i < end; i++)
        {
            double delta = Math.Abs(buf.Data[i] - buf.Data[i - 1]);
            if (delta > maxDelta) maxDelta = delta;
        }
        // SPEC-5 locked: max per-sample discontinuity ≤ 0.05.
        Assert.True(maxDelta <= 0.05,
            $"Max sample-to-sample delta in body ({maxDelta}) exceeded 0.05 — loop crossfade has an audible click (SPEC-5 gate).");
    }

    [Fact]
    public void EqualPowerCrossfade_PreservesEnergy_AcrossLoopSeam()
    {
        // Equal-power constraint: cos²(πt/2N) + sin²(πt/2N) = 1 for all t.
        // The 441-frame crossfade window should preserve the total RMS energy
        // of the source signal across the seam — i.e., the RMS in a window
        // straddling the loop boundary should be within ±2% of the RMS in a
        // window safely inside the loop body.
        string wavPath = Path.Combine(_tmpRoot, "sus.wav");
        SfzRegionMatchTests.WriteSineWav(wavPath, frequencyHz: 220.0, frames: 22050);

        var region = new SfzRegion(
            "sus.wav", PitchKeycenter: 60,
            LoKey: 0, HiKey: 127, LoVel: 1, HiVel: 127,
            LoopMode: SfzLoopMode.LoopContinuous,
            LoopStart: 11025, LoopEnd: 22050,
            AmpegAttack: 0.0, AmpegRelease: 0.0, Volume: 1.0, Pan: 0.0);
        var patch = BuildPatch(_tmpRoot, "eq-power", region);

        var cache = new SfzSampleCache();
        EagerLoadDirect(cache, patch);
        var renderer = new SfzRenderer(cache);

        var note = new MusicalNoteData('C', 4, 0, durationValue: 4, isRest: false, velocity: 0.5);
        var buf = renderer.Render(note, SampleRate, durationBeats: 4.0, bpm: 60.0, patch);

        // Window inside body: frames [40000..50000] — well past the attack
        // ramp, inside the loop region.
        double rmsInside = WindowedRms(buf, 40000, 50000);
        // Window straddling a loop seam: frames around 22050 (first seam) —
        // SHOULD have equal-power coverage at the crossfade.
        double rmsSeam = WindowedRms(buf, 22050 - 220, 22050 + 220);

        Assert.True(rmsInside > 0, "Inside-body RMS must be non-zero.");
        Assert.True(rmsSeam > 0, "Seam RMS must be non-zero.");
        // Equal-power preservation within ±15% — a wider band than the SPEC's
        // ±2% spectral-centroid criterion because raw RMS is sensitive to
        // partial-loop phase, but a linear crossfade would show > 30% sag
        // (cos² + sin² = 1, linear dips to 0.5 at t=0.5).
        double ratio = rmsSeam / rmsInside;
        Assert.InRange(ratio, 0.85, 1.15);
    }

    [Fact]
    public void LoopEndBeyondSampleLength_Clamped_DoesNotThrow()
    {
        // Pitfall 3 / T-33-LOOP-01: a malformed SFZ can declare
        // loop_end > sample.Length. The renderer must clamp to
        // sample.Length - 1 at render time to avoid IndexOutOfRangeException.
        string wavPath = Path.Combine(_tmpRoot, "short.wav");
        SfzRegionMatchTests.WriteSineWav(wavPath, frequencyHz: 220.0, frames: 22050);

        var region = new SfzRegion(
            "short.wav", PitchKeycenter: 60,
            LoKey: 0, HiKey: 127, LoVel: 1, HiVel: 127,
            LoopMode: SfzLoopMode.LoopContinuous,
            LoopStart: 0, LoopEnd: 999999,  // BAD: way past sample.Length
            AmpegAttack: 0.0, AmpegRelease: 0.0, Volume: 1.0, Pan: 0.0);
        var patch = BuildPatch(_tmpRoot, "loop-clamp", region);

        var cache = new SfzSampleCache();
        EagerLoadDirect(cache, patch);
        var renderer = new SfzRenderer(cache);

        var note = new MusicalNoteData('C', 4, 0, durationValue: 2, isRest: false, velocity: 0.5);
        // Must NOT throw — renderer is responsible for the clamp.
        var buf = renderer.Render(note, SampleRate, 2.0, 60.0, patch);
        Assert.True(buf.Frames > 0);
        Assert.True(SfzRegionMatchTests.Rms(buf) > 0,
            "Clamped loop must still produce audible output (don't render silence).");
    }

    [Fact]
    public void NoLoopMode_DoesNotExtendBeyondSampleBody()
    {
        // Source is 22050 frames (500 ms). Render duration = 2 seconds = 88200
        // frames. With loop_mode=no_loop the tail after frame ~22050 should
        // taper to silence — the Phase 28 envelope's release tail is short
        // (50 ms = 2205 frames), so the back half of the buffer must be
        // essentially silent.
        string wavPath = Path.Combine(_tmpRoot, "short.wav");
        SfzRegionMatchTests.WriteSineWav(wavPath, frequencyHz: 220.0, frames: 22050);

        var region = new SfzRegion(
            "short.wav", PitchKeycenter: 60,
            LoKey: 0, HiKey: 127, LoVel: 1, HiVel: 127,
            LoopMode: SfzLoopMode.NoLoop, LoopStart: 0, LoopEnd: 0,
            AmpegAttack: 0.0, AmpegRelease: 0.0, Volume: 1.0, Pan: 0.0);
        var patch = BuildPatch(_tmpRoot, "no-loop", region);

        var cache = new SfzSampleCache();
        EagerLoadDirect(cache, patch);
        var renderer = new SfzRenderer(cache);

        var note = new MusicalNoteData('C', 4, 0, durationValue: 2, isRest: false, velocity: 0.5);
        var buf = renderer.Render(note, SampleRate, durationBeats: 2.0, bpm: 60.0, patch);

        Assert.True(buf.Frames >= 88200 - 10, $"2s render expected ~88200 frames, got {buf.Frames}.");
        // First-half (covers body) should be loud.
        double rmsFirst = WindowedRms(buf, 5000, 18000);
        // Last quarter (well past 22050 source + 2205 release tail) should be silent.
        double rmsLast = WindowedRms(buf, 66000, 88000);
        Assert.True(rmsFirst > 0.05, $"first-half RMS ({rmsFirst}) too low — body should be loud.");
        Assert.True(rmsLast < 0.005, $"last-quarter RMS ({rmsLast}) too high — no_loop should not extend past sample body.");
    }

    [Fact]
    public void Staccato_BodyShorterThan_Legato()
    {
        // Phase 28 articulation rules: Staccato sustain=0 + release×0.5 should
        // produce a SHORTER audible body than Legato on the SAME note. We
        // measure body length as "frames where |sample| > 0.01" — a coarse
        // envelope-floor crossing that's robust to phase.
        string wavPath = Path.Combine(_tmpRoot, "src.wav");
        SfzRegionMatchTests.WriteSineWav(wavPath, frequencyHz: 220.0, frames: 22050);
        var region = new SfzRegion(
            "src.wav", PitchKeycenter: 60,
            LoKey: 0, HiKey: 127, LoVel: 1, HiVel: 127,
            LoopMode: SfzLoopMode.LoopContinuous,
            LoopStart: 11025, LoopEnd: 22050,
            AmpegAttack: 0.0, AmpegRelease: 0.0, Volume: 1.0, Pan: 0.0);
        var patch = BuildPatch(_tmpRoot, "stac-vs-leg", region);

        var cache = new SfzSampleCache();
        EagerLoadDirect(cache, patch);
        var renderer = new SfzRenderer(cache);

        var stac = new MusicalNoteData('C', 4, 0, 4, false, velocity: 0.5, articulation: Articulation.Staccato);
        var leg  = new MusicalNoteData('C', 4, 0, 4, false, velocity: 0.5, articulation: Articulation.Legato);

        var bufStac = renderer.Render(stac, SampleRate, 1.0, 60.0, patch);
        var bufLeg  = renderer.Render(leg,  SampleRate, 1.0, 60.0, patch);

        int audibleStac = CountAudibleFrames(bufStac, 0.01f);
        int audibleLeg  = CountAudibleFrames(bufLeg, 0.01f);

        Assert.True(audibleStac < audibleLeg,
            $"Staccato audible frames ({audibleStac}) must be < Legato ({audibleLeg}) — Phase 28 articulation envelope.");
    }

    [Fact]
    public void AmpegAttack_Overrides_Baseline()
    {
        // SPEC-8 acceptance: a region with ampeg_attack=0.5 must produce a
        // measurably slower attack than a region with ampeg_attack=0.005 on
        // the same note. We measure "time to half-peak RMS" — should be >>
        // 200 ms for the slow region and ~0 ms for the fast region.
        string wavPath = Path.Combine(_tmpRoot, "src.wav");
        SfzRegionMatchTests.WriteSineWav(wavPath, frequencyHz: 220.0, frames: 22050);

        var rFast = new SfzRegion(
            "src.wav", 60, 0, 127, 1, 127,
            SfzLoopMode.LoopContinuous, 11025, 22050,
            AmpegAttack: 0.005, AmpegRelease: 0.05,
            Volume: 1.0, Pan: 0.0);
        var rSlow = new SfzRegion(
            "src.wav", 60, 0, 127, 1, 127,
            SfzLoopMode.LoopContinuous, 11025, 22050,
            AmpegAttack: 0.5, AmpegRelease: 0.05,
            Volume: 1.0, Pan: 0.0);

        var patchFast = BuildPatch(_tmpRoot, "fast", rFast);
        var patchSlow = BuildPatch(_tmpRoot, "slow", rSlow);

        var cache = new SfzSampleCache();
        EagerLoadDirect(cache, patchFast);
        EagerLoadDirect(cache, patchSlow);
        var renderer = new SfzRenderer(cache);

        var note = new MusicalNoteData('C', 4, 0, 1, false, velocity: 0.5);
        // Render 1 second = 44100 frames so the slow attack has room to ramp.
        var bufFast = renderer.Render(note, SampleRate, durationBeats: 1.0, bpm: 60.0, patchFast);
        var bufSlow = renderer.Render(note, SampleRate, durationBeats: 1.0, bpm: 60.0, patchSlow);

        int tFast = TimeToHalfPeakFrames(bufFast);
        int tSlow = TimeToHalfPeakFrames(bufSlow);

        // Fast attack should reach 50% peak very quickly (< 0.5s at 44.1 kHz = 22050).
        Assert.True(tFast < 11025, $"Fast attack reached 50% peak in {tFast} frames — should be < 11025.");
        // Slow attack should take noticeably longer.
        Assert.True(tSlow > tFast * 3,
            $"Slow attack (tSlow={tSlow}) should be >>3× fast attack (tFast={tFast}).");
        // ampeg_attack=0.5 → expect > 200 ms (= 8820 frames at 44.1 kHz) to reach half-peak.
        Assert.True(tSlow > 8820, $"Slow attack ({tSlow} frames) should exceed 200ms (8820 frames).");
    }

    // ===== Helpers =====

    private static SfzData BuildPatch(string basePath, string description, params SfzRegion[] regions)
    {
        var grid = new SfzRegion?[128, 128];
        foreach (var r in regions)
        {
            for (int p = Math.Max(0, r.LoKey); p <= Math.Min(127, r.HiKey); p++)
                for (int v = Math.Max(0, r.LoVel); v <= Math.Min(127, r.HiVel); v++)
                    grid[p, v] = r;
        }
        var sortedPitches = regions
            .SelectMany(r => Enumerable.Range(Math.Max(0, r.LoKey), Math.Min(127, r.HiKey) - Math.Max(0, r.LoKey) + 1))
            .Distinct()
            .OrderBy(p => p)
            .ToArray();
        return new SfzData(description, basePath, regions.ToList(), grid, sortedPitches);
    }

    private static void EagerLoadDirect(SfzSampleCache cache, SfzData patch)
    {
        var section = new SectionData(
            "tmp",
            new Dictionary<string, SequenceData> { ["s"] = BuildSequence(patch) },
            context: null);
        var registry = new Dictionary<string, SectionData> { ["tmp"] = section };
        var song = new SongData(new List<SongSectionRef> { new("tmp", 1) }, registry);
        cache.EagerLoad(song, patch);
    }

    private static SequenceData BuildSequence(SfzData patch)
    {
        var seq = new SequenceData();
        var ts = new TimeSignatureData(4, 4);
        var notes = new List<MusicalNoteData>();
        foreach (var r in patch.Regions)
        {
            notes.Add(new MusicalNoteData('C', 4, 0, durationValue: 4, isRest: false, velocity: 0.5));
        }
        if (notes.Count == 0)
            notes.Add(new MusicalNoteData('C', 4, 0, 4, true));
        var bar = new BarData(notes, ts);
        seq.AddBar(bar);
        return seq;
    }

    private static double WindowedRms(AudioBuffer buf, int start, int end)
    {
        start = Math.Max(0, Math.Min(start, buf.Frames - 1));
        end = Math.Max(start + 1, Math.Min(end, buf.Frames));
        double sum = 0;
        for (int i = start; i < end; i++) sum += buf.Data[i] * buf.Data[i];
        return Math.Sqrt(sum / (end - start));
    }

    private static int CountAudibleFrames(AudioBuffer buf, float threshold)
    {
        int n = 0;
        for (int i = 0; i < buf.Data.Length; i++) if (Math.Abs(buf.Data[i]) > threshold) n++;
        return n;
    }

    private static int TimeToHalfPeakFrames(AudioBuffer buf)
    {
        // Sliding-window RMS to find the first index where rms ≥ peak/2.
        const int win = 441;  // 10 ms window
        if (buf.Frames < win) return buf.Frames;

        // First pass — find peak RMS across the whole buffer.
        double peak = 0;
        for (int i = 0; i + win < buf.Frames; i += win)
        {
            double sum = 0;
            for (int j = 0; j < win; j++) sum += buf.Data[i + j] * buf.Data[i + j];
            double rms = Math.Sqrt(sum / win);
            if (rms > peak) peak = rms;
        }
        if (peak <= 0) return buf.Frames;
        double half = peak * 0.5;
        // Second pass — first frame where windowed RMS crosses half-peak.
        for (int i = 0; i + win < buf.Frames; i += 50)
        {
            double sum = 0;
            for (int j = 0; j < win; j++) sum += buf.Data[i + j] * buf.Data[i + j];
            double rms = Math.Sqrt(sum / win);
            if (rms >= half) return i;
        }
        return buf.Frames;
    }
}
