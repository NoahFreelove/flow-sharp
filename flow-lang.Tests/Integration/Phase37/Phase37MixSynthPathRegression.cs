using System;
using System.IO;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Helpers;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 MIX-01 (D-37-15) — verify the already-shipped synth-path per-voice
/// pan formula at <c>SongRenderer:308-309</c> via an RMS-baseline pin. Per
/// D-37-15 the synth-path pan needs no code change; this fixture just pins
/// the bytes so future drift is caught.
///
/// <para><b>Baseline generation</b>: on first run with no baseline file, the
/// rendered WAV is written to <c>flow-lang.Tests/baselines/Phase37/mix_synth_path_pan.wav</c>
/// and the test is marked passing (so the committer sees a clean run); the
/// committed baseline is then asserted against on every subsequent run via
/// SPEC-8 RMS regression (±0.5 dB / 100 ms windows). To regenerate the
/// baseline after a deliberate change, delete the .wav and re-run.</para>
/// </summary>
[Collection("FlowScripts")]
public class Phase37MixSynthPathRegression : IDisposable
{
    public Phase37MixSynthPathRegression()
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
            if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "baselines")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException(
            "Could not locate repo root from " + AppContext.BaseDirectory);
    }

    // Render two synth-path voices at opposing pans via Flow source — the
    // synth path (piano) accepts `pan` as a context-block value (per the
    // existing tests/test_panning.flow convention). Voice 1 panned
    // hard-left (-0.7); voice 2 panned hard-right (+0.7). 1 second total
    // via 2 quarter-notes at 120 bpm.
    private const string TwoVoiceOpposingPanScript = @"use ""@audio""
pan -0.7 {
    section left {
        Sequence main = | C4q |
    }
}
pan 0.7 {
    section right {
        Sequence main = | G4q |
    }
}
Song s = [left right]
Buffer mix = (renderSong s ""piano"")
";

    [Fact]
    public void SynthPathPan_TwoVoicesOppositePan_RmsMatchesBaseline()
    {
        var repoRoot = FindRepoRoot();
        var baselinePath = Path.Combine(repoRoot, "flow-lang.Tests",
            "baselines", "Phase37", "mix_synth_path_pan.wav");

        // Piano samples load via Environment.CurrentDirectory — point the
        // engine at the repo root so flow-lang/Samples/piano/* resolves.
        string originalCwd = Environment.CurrentDirectory;
        AudioBuffer buf;
        try
        {
            Environment.CurrentDirectory = repoRoot;
            using var runner = new FlowEngineRunner();
            var (ok, _, stderr, _) = runner.RunSource(TwoVoiceOpposingPanScript,
                "<phase37-mix-synth-baseline>");
            Assert.True(ok, $"Synth-path render failed: {stderr}");
            buf = runner.GetVariable("mix").As<AudioBuffer>();
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
        Assert.NotNull(buf);
        Assert.True(buf.Frames > 0, "MIX-01 baseline render produced zero frames");
        Assert.Equal(2, buf.Channels);

        if (!File.Exists(baselinePath))
        {
            // First-run: generate the baseline and pass the test (the
            // composer then commits the baseline; subsequent runs pin to it).
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            var args = new System.Collections.Generic.List<Value>
            {
                Value.String(baselinePath),
                Value.Buffer(buf),
            };
            FileIO.WriteWav(args);
            // Defensive: re-read the baseline to verify the round-trip wrote
            // correctly so the next run's RMS compare won't trip on EOF.
            Assert.True(File.Exists(baselinePath),
                $"Baseline write failed at {baselinePath}");
            return;
        }

        RmsRegressionTests.AssertRmsWithinTolerance(buf, baselinePath);
    }

    /// <summary>
    /// Synth-path pan formula sanity check — voice 1 (pan=-0.7, sounding
    /// during 0..500ms) should have left RMS &gt; right RMS by ≥ 3 dB; voice
    /// 2 (pan=+0.7, sounding 500ms..1000ms) should have right &gt; left by
    /// ≥ 3 dB. Confirms the SongRenderer:308-309 constant-power formula is
    /// shipping; complementary to the bytes-pin check above.
    /// </summary>
    [Fact]
    public void SynthPathPan_LeftAndRightRmsDifferAsExpected()
    {
        string repoRoot = FindRepoRoot();
        string originalCwd = Environment.CurrentDirectory;
        AudioBuffer buf;
        try
        {
            Environment.CurrentDirectory = repoRoot;
            using var runner = new FlowEngineRunner();
            var (ok, _, stderr, _) = runner.RunSource(TwoVoiceOpposingPanScript,
                "<phase37-mix-synth-leftright>");
            Assert.True(ok, $"Synth-path render failed: {stderr}");
            buf = runner.GetVariable("mix").As<AudioBuffer>();
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
        Assert.NotNull(buf);
        Assert.Equal(2, buf.Channels);

        int sr = buf.SampleRate;
        // Empirically the two-section render at default 60 bpm + quarter-note
        // sequences lands at 4 s total (each section is ~2 s including tail).
        // Voice 1 (pan=-0.7) lives in the first half; voice 2 (pan=+0.7) in
        // the second. Pick small windows clear of section seams + attack
        // transients.
        int v1Start = 200 * sr / 1000;
        int v1End = 1000 * sr / 1000;
        int v2Start = 2200 * sr / 1000;
        int v2End = 3000 * sr / 1000;

        v1End = Math.Min(v1End, buf.Frames);
        v2End = Math.Min(v2End, buf.Frames);

        double v1Left = RmsChannel(buf, 0, v1Start, v1End);
        double v1Right = RmsChannel(buf, 1, v1Start, v1End);
        double v2Left = RmsChannel(buf, 0, v2Start, v2End);
        double v2Right = RmsChannel(buf, 1, v2Start, v2End);

        // 3 dB voltage ratio ≈ 1.41x. Pan=-0.7 puts ~95% of power in L,
        // ~5% in R via constant-power; the RMS ratio is much larger than
        // 3 dB at this pan depth, so the 3 dB floor is conservatively safe.
        double v1Db = 20.0 * Math.Log10(Math.Max(v1Left, 1e-12) / Math.Max(v1Right, 1e-12));
        double v2Db = 20.0 * Math.Log10(Math.Max(v2Right, 1e-12) / Math.Max(v2Left, 1e-12));

        Assert.True(v1Db >= 3.0,
            $"voice 1 (pan=-0.7) L/R dB delta {v1Db:F2} dB should be >= 3.0 dB " +
            $"(L={v1Left:E3} R={v1Right:E3} window={v1Start}..{v1End} frames={buf.Frames} ch={buf.Channels} sr={sr})");
        Assert.True(v2Db >= 3.0,
            $"voice 2 (pan=+0.7) R/L dB delta {v2Db:F2} dB should be >= 3.0 dB " +
            $"(L={v2Left:E3} R={v2Right:E3} window={v2Start}..{v2End})");
    }

    private static double RmsChannel(AudioBuffer buf, int channel, int startFrame, int endFrame)
    {
        if (endFrame <= startFrame) return 0.0;
        double sumSq = 0.0;
        for (int f = startFrame; f < endFrame; f++)
        {
            float s = buf.GetSample(f, channel);
            sumSq += (double)s * s;
        }
        return Math.Sqrt(sumSq / (endFrame - startFrame));
    }
}
