using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Sfz;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Quick260702Tpn;

/// <summary>
/// quick-260702-tpn — SFZ <c>amp_veltrack</c> velocity-amplitude curve.
///
/// <para>Before this task the SFZ renderer used note velocity ONLY for
/// region/layer selection and the xfin/xfout crossfade — velocity never scaled
/// amplitude. VSCO Community Edition (and every Sforzando/ARIA-authored library)
/// bakes per-layer makeup gains that EXPECT the <c>amp_veltrack=100</c> default
/// curve <c>(vel/127)²</c> to attenuate soft layers; without it a soft layer's
/// +18 dB makeup rendered pp LOUDER than ff — dynamics flat-to-inverted.</para>
///
/// <para>These facts pin: (1) the curve math via the test-only helper,
/// (2) the parser default (absent → 100.0) + explicit values, and (3) the
/// headline loudness ordering (pp quieter than ff on a two-layer makeup-gain
/// patch — the inverted-dynamics bug closed).</para>
/// </summary>
[Collection("FlowScripts")]
public class SfzAmpVeltrackTests : IDisposable
{
    public SfzAmpVeltrackTests()
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

    private static double RmsDb(AudioBuffer buf)
    {
        if (buf.Frames == 0) return double.NegativeInfinity;
        double sumSq = 0.0;
        for (int i = 0; i < buf.Data.Length; i++)
        {
            double s = buf.Data[i];
            sumSq += s * s;
        }
        double rms = Math.Sqrt(sumSq / buf.Data.Length);
        return rms <= 1e-12 ? double.NegativeInfinity : 20.0 * Math.Log10(rms);
    }

    private static AudioBuffer RenderAtVelocityFraction(
        SfzRenderer renderer, SfzData patch, double velocityFraction)
    {
        var note = new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 0,
            durationValue: 4, isRest: false,
            velocity: velocityFraction,
            articulation: Articulation.Normal);
        return renderer.Render(note, sampleRate: 44100, durationBeats: 1.0, bpm: 120.0, patch);
    }

    // ----- 1. Curve-math pins -------------------------------------------

    [Fact]
    public void ComputeVelocityGain_DefaultTrack_IsVelocitySquared()
    {
        // amp_veltrack=100 (t=1) → pure (vel/127)^2 curve.
        Assert.Equal(1.0, SfzRenderer.ComputeVelocityGain_TestOnly(100, 127), 4);
        // (64/127)^2 = 0.25396...
        Assert.Equal(0.2540, SfzRenderer.ComputeVelocityGain_TestOnly(100, 64), 3);
    }

    [Fact]
    public void ComputeVelocityGain_ZeroTrack_IsAlwaysUnity()
    {
        // amp_veltrack=0 (t=0) → velocity does not affect amplitude.
        Assert.Equal(1.0, SfzRenderer.ComputeVelocityGain_TestOnly(0, 32), 4);
        Assert.Equal(1.0, SfzRenderer.ComputeVelocityGain_TestOnly(0, 127), 4);
    }

    [Fact]
    public void ComputeVelocityGain_OutOfRange_ClampsTrackFraction()
    {
        // t clamped to [0,1]: 150 behaves as 100, -50 behaves as 0.
        Assert.Equal(
            SfzRenderer.ComputeVelocityGain_TestOnly(100, 64),
            SfzRenderer.ComputeVelocityGain_TestOnly(150, 64), 4);
        Assert.Equal(
            SfzRenderer.ComputeVelocityGain_TestOnly(0, 64),
            SfzRenderer.ComputeVelocityGain_TestOnly(-50, 64), 4);
    }

    // ----- 2. Parser-default pin ----------------------------------------

    [Fact]
    public void Parser_AmpVeltrack_DefaultsTo100_AndReadsExplicit()
    {
        // Absent → 100.0 (Sforzando/ARIA default).
        var absent = SfzParser.Parse(
            "<region>\nsample=C4_sine.wav lokey=0 hikey=127 pitch_keycenter=60\n",
            "/tmp/absent.sfz", "absent");
        Assert.Equal(100.0, absent.Regions[0].AmpVeltrack);

        // amp_veltrack=0 → 0.0 (velocity does not affect amplitude).
        var zero = SfzParser.Parse(
            "<region>\nsample=C4_sine.wav lokey=0 hikey=127 pitch_keycenter=60 amp_veltrack=0\n",
            "/tmp/zero.sfz", "zero");
        Assert.Equal(0.0, zero.Regions[0].AmpVeltrack);

        // amp_veltrack=50 → 50.0.
        var half = SfzParser.Parse(
            "<region>\nsample=C4_sine.wav lokey=0 hikey=127 pitch_keycenter=60 amp_veltrack=50\n",
            "/tmp/half.sfz", "half");
        Assert.Equal(50.0, half.Regions[0].AmpVeltrack);
    }

    // ----- 3. Loudness ordering (the diagnosis headline) ----------------

    [Fact]
    public void TwoLayerMakeupGainPatch_PpQuieterThanFf()
    {
        string repoRoot = FindRepoRoot();
        string wavPath = Path.Combine(repoRoot, "flow-lang.Tests", "fixtures",
            "sfz-smoke", "C4_sine.wav");
        var buf = FileIO.LoadWavInternal(wavPath);

        // VSCO-style two-layer patch: a SOFT layer (vel 0-62) carrying a large
        // +18 dB makeup gain, and a LOUD layer (vel 63-127) at +6 dB. Both
        // reference the same C4_sine.wav. Absent amp_veltrack → effective
        // track 100 → each render also carries (vel/127)^2.
        //
        // Expected magnitudes from the diagnosis:
        //   pp: vel 32 hits SOFT → curve (32/127)^2 ≈ 0.0635 (-23.9 dB) × +18 dB
        //       makeup → net ≈ -6 dB relative to the raw sample.
        //   ff: vel 111 hits LOUD → curve (111/127)^2 ≈ 0.764 (-2.3 dB) × +6 dB
        //       makeup → net ≈ +3.7 dB relative to the raw sample.
        // → pp RMS < ff RMS (inverted-dynamics bug closed). Pre-fix the curve
        //   was absent, so pp's +18 dB makeup rendered LOUDER than ff's +6 dB.
        string sfz =
            "<region>\n" +
            "sample=C4_sine.wav lokey=0 hikey=127 pitch_keycenter=60 lovel=0 hivel=62 volume=18\n" +
            "<region>\n" +
            "sample=C4_sine.wav lokey=0 hikey=127 pitch_keycenter=60 lovel=63 hivel=127 volume=6\n";
        var patch = SfzParser.Parse(sfz, Path.Combine(repoRoot, "flow-lang.Tests",
            "fixtures", "sfz-smoke", "two-layer.sfz"), "two-layer");

        var cache = new SfzSampleCache();
        foreach (var region in patch.Regions)
            cache.SetRaw_TestOnly(patch, region.SamplePath, buf);
        var renderer = new SfzRenderer(cache);

        var ppBuf = RenderAtVelocityFraction(renderer, patch, 0.25);   // → vel 32, soft
        var ffBuf = RenderAtVelocityFraction(renderer, patch, 0.875);  // → vel 111, loud

        double ppDb = RmsDb(ppBuf);
        double ffDb = RmsDb(ffBuf);
        Assert.False(double.IsNegativeInfinity(ppDb), "pp render must not be silent");
        Assert.False(double.IsNegativeInfinity(ffDb), "ff render must not be silent");
        Assert.True(ppDb < ffDb,
            $"pp (vel 32, soft +18 dB) RMS {ppDb:F2} dB must be QUIETER than " +
            $"ff (vel 111, loud +6 dB) RMS {ffDb:F2} dB — the inverted-dynamics bug regressed.");
    }
}
