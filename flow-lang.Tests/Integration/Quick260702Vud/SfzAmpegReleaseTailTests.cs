using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Sfz;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Quick260702Vud;

/// <summary>
/// quick-260702-vud — SFZ <c>ampeg_release</c> tail past the authored note end.
///
/// <para>Before this task <see cref="SfzRenderer"/> squeezed the whole
/// <c>ampeg_release</c> INTO the authored note window: with VSCO CE's
/// OboeSusVib (<c>ampeg_release=0.7</c>) a 0.3s eighth note became ~93% release
/// ramp, so every SFZ note decayed to zero before its slot ended — melodies
/// sounded detached/staccato/quiet.</para>
///
/// <para>Real SFZ semantics: <c>ampeg_release</c> is what happens AFTER note-off,
/// so the tail RINGS PAST the note boundary. These facts pin the fix: a
/// sustained-articulation note holds its level through the authored end and rings
/// out via an exponential tail appended past the boundary
/// (<c>buf.Frames == authoredFrames + releaseFrames</c>), continuous at the seam
/// and decaying to ~-60 dB. Staccato (sustain=0) and ampeg_release-absent patches
/// get NO tail.</para>
/// </summary>
[Collection("FlowScripts")]
public class SfzAmpegReleaseTailTests : IDisposable
{
    private const int SampleRate = 44100;
    private const double AmpegRelease = 0.7;
    // 0.5 beats @ 120 BPM = 0.25s → the 0.7s tail dominates the 0.25s note.
    private const double DurationBeats = 0.5;
    private const double Bpm = 120.0;

    public SfzAmpegReleaseTailTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "fixtures")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException(
            "Could not locate repo root from " + AppContext.BaseDirectory);
    }

    private static AudioBuffer LoadC4Sine()
    {
        string wavPath = Path.Combine(FindRepoRoot(), "flow-lang.Tests", "fixtures",
            "sfz-smoke", "C4_sine.wav");
        return FileIO.LoadWavInternal(wavPath);
    }

    /// <summary>
    /// Build a one-region loop-continuous patch. The committed C4_sine.wav is
    /// only 0.1s (4410 frames), so a <c>loop_continuous</c> body is required to
    /// fill the authored+tail window (mirrors the smoke fixture). When
    /// <paramref name="ampegRelease"/> is left at the parser's absent-sentinel
    /// (0.001) the renderer appends no tail.
    /// </summary>
    private static SfzData BuildLoopPatch(string description, double ampegRelease)
    {
        var region = new SfzRegion(
            SamplePath: "C4_sine.wav",
            PitchKeycenter: 60,
            LoKey: 48, HiKey: 71,
            LoVel: 1, HiVel: 127,
            LoopMode: SfzLoopMode.LoopContinuous,
            LoopStart: 0, LoopEnd: 4409,
            AmpegAttack: 0.005,
            AmpegRelease: ampegRelease,
            Volume: 1.0, Pan: 0.0);
        var grid = new SfzRegion?[128, 128];
        for (int p = region.LoKey; p <= region.HiKey; p++)
            for (int v = region.LoVel; v <= region.HiVel; v++)
                grid[p, v] = region;
        return new SfzData(description, "/tmp/vud.sfz",
            new System.Collections.Generic.List<SfzRegion> { region },
            grid, new[] { 60 });
    }

    private static SfzRenderer NewRenderer(SfzData patch)
    {
        var buf = LoadC4Sine();
        var cache = new SfzSampleCache();
        foreach (var region in patch.Regions)
            cache.SetRaw_TestOnly(patch, region.SamplePath, buf);
        return new SfzRenderer(cache);
    }

    private static MusicalNoteData MakeC4(Articulation articulation) =>
        new(noteName: 'C', octave: 4, alteration: 0,
            durationValue: 8, isRest: false,
            velocity: 0.8, articulation: articulation);

    private static int AuthoredFrames() =>
        (int)(DurationBeats * 60.0 / Bpm * SampleRate);

    /// <summary>Max abs sample magnitude (across channels) over frames [start, end).</summary>
    private static float MaxAbs(AudioBuffer buf, int startFrame, int endFrame)
    {
        int ch = buf.Channels;
        startFrame = Math.Max(0, startFrame);
        endFrame = Math.Min(buf.Frames, endFrame);
        float maxAbs = 0f;
        for (int f = startFrame; f < endFrame; f++)
            for (int c = 0; c < ch; c++)
            {
                float v = MathF.Abs(buf.Data[f * ch + c]);
                if (v > maxAbs) maxAbs = v;
            }
        return maxAbs;
    }

    /// <summary>Abs magnitude of a single frame (max across channels).</summary>
    private static float FrameAbs(AudioBuffer buf, int frame)
    {
        int ch = buf.Channels;
        float maxAbs = 0f;
        for (int c = 0; c < ch; c++)
        {
            float v = MathF.Abs(buf.Data[frame * ch + c]);
            if (v > maxAbs) maxAbs = v;
        }
        return maxAbs;
    }

    /// <summary>RMS over frames [start, end) across all channels.</summary>
    private static double Rms(AudioBuffer buf, int startFrame, int endFrame)
    {
        int ch = buf.Channels;
        startFrame = Math.Max(0, startFrame);
        endFrame = Math.Min(buf.Frames, endFrame);
        if (endFrame <= startFrame) return 0.0;
        double sumSq = 0.0;
        int n = 0;
        for (int f = startFrame; f < endFrame; f++)
            for (int c = 0; c < ch; c++)
            {
                double v = buf.Data[f * ch + c];
                sumSq += v * v;
                n++;
            }
        return n == 0 ? 0.0 : Math.Sqrt(sumSq / n);
    }

    // ----- 1. Hold-at-authored-end (anti-regression for the 93%-fade bug) ----

    [Fact]
    public void SustainedNote_HoldsLevelAtAuthoredEnd()
    {
        var patch = BuildLoopPatch("hold-at-end", AmpegRelease);
        var renderer = NewRenderer(patch);
        var buf = renderer.Render(MakeC4(Articulation.Normal), SampleRate, DurationBeats, Bpm, patch);

        int authored = AuthoredFrames();
        // Level held at the authored end must be within 90% of the mid-note level
        // — the note must NOT have decayed to near-zero at its authored end.
        float endLevel = MaxAbs(buf, authored - 200, authored);
        float midLevel = MaxAbs(buf, authored / 2 - 100, authored / 2 + 100);

        Assert.True(midLevel > 0f, "mid-note window must carry signal");
        Assert.True(endLevel >= 0.9f * midLevel,
            $"authored-end level {endLevel:F4} must be >= 90% of mid-note level " +
            $"{midLevel:F4} — the 93%-fade cutoff bug regressed (note faded inside window)");
    }

    // ----- 2. Buffer length = authoredFrames + releaseFrames -----------------

    [Fact]
    public void SustainedNote_BufferLength_IsAuthoredPlusRelease()
    {
        var patch = BuildLoopPatch("length", AmpegRelease);
        var renderer = NewRenderer(patch);
        var buf = renderer.Render(MakeC4(Articulation.Normal), SampleRate, DurationBeats, Bpm, patch);

        int expected = AuthoredFrames() + (int)(AmpegRelease * SampleRate);
        Assert.InRange(buf.Frames, expected - 2, expected + 2);
    }

    [Fact]
    public void StaccatoNote_NoTail_SamePatch()
    {
        var patch = BuildLoopPatch("staccato-no-tail", AmpegRelease);
        var renderer = NewRenderer(patch);
        var buf = renderer.Render(MakeC4(Articulation.Staccato), SampleRate, DurationBeats, Bpm, patch);

        int authored = AuthoredFrames();
        Assert.InRange(buf.Frames, authored - 2, authored + 2);
    }

    // ----- 3. Tail continuity + decay ----------------------------------------

    [Fact]
    public void Tail_IsContinuousAndDecays()
    {
        var patch = BuildLoopPatch("continuity", AmpegRelease);
        var renderer = NewRenderer(patch);
        var buf = renderer.Render(MakeC4(Articulation.Normal), SampleRate, DurationBeats, Bpm, patch);

        int authored = AuthoredFrames();
        int tailFrames = (int)(AmpegRelease * SampleRate);

        // Continuity: the seam sample (first tail frame, level=1.0) is within a
        // small absolute tolerance of the last authored frame (envelope held at
        // sustain=1.0). No step discontinuity.
        float before = FrameAbs(buf, authored - 1);
        float after = FrameAbs(buf, authored);
        Assert.True(MathF.Abs(after - before) < 0.05f,
            $"tail must be continuous at the authored seam: |{after:F4} - {before:F4}| " +
            $"= {MathF.Abs(after - before):F4} exceeds tolerance");

        // Decay: RMS of the last quarter of the tail is far below the first quarter.
        double firstQuarter = Rms(buf, authored, authored + tailFrames / 4);
        double lastQuarter = Rms(buf, authored + 3 * tailFrames / 4, buf.Frames);
        Assert.True(firstQuarter > 0.0, "first tail quarter must carry signal");
        Assert.True(lastQuarter < 0.2 * firstQuarter,
            $"tail must decay: last-quarter RMS {lastQuarter:E3} must be < 20% of " +
            $"first-quarter RMS {firstQuarter:E3}");
    }

    // ----- 4. ampeg_release absent → no tail ---------------------------------

    [Fact]
    public void AmpegReleaseAbsent_NoTail()
    {
        // Parse a real patch that omits ampeg_release → parser absent-sentinel
        // (0.001) → renderer appends no tail.
        var patch = SfzParser.Parse(
            "<region>\nsample=C4_sine.wav lokey=48 hikey=71 pitch_keycenter=60 " +
            "loop_mode=loop_continuous loop_start=0 loop_end=4409\n",
            "/tmp/absent-release.sfz", "absent-release");
        var renderer = NewRenderer(patch);
        var buf = renderer.Render(MakeC4(Articulation.Normal), SampleRate, DurationBeats, Bpm, patch);

        int authored = AuthoredFrames();
        Assert.InRange(buf.Frames, authored - 2, authored + 2);
    }

    // ----- 5. Two-run determinism --------------------------------------------

    [Fact]
    public void TwoRuns_ByteIdentical()
    {
        var patch = BuildLoopPatch("determinism", AmpegRelease);
        var renderer = NewRenderer(patch);
        var a = renderer.Render(MakeC4(Articulation.Normal), SampleRate, DurationBeats, Bpm, patch);
        var b = renderer.Render(MakeC4(Articulation.Normal), SampleRate, DurationBeats, Bpm, patch);

        Assert.Equal(a.Data.Length, b.Data.Length);
        for (int i = 0; i < a.Data.Length; i++)
            Assert.Equal(a.Data[i], b.Data[i]);
    }
}
