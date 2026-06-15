using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Sfz;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 SAMP-02 RENDER side (sweep-0614 regression). The parser-side test
/// <see cref="SfzVelocityCrossfadeTests"/> only verifies opcode parsing. This
/// test exercises the ACTUAL render path: it sweeps velocity 50→90 through the
/// repo's documented two-layer velocity_xfade.sfz fixture and asserts the
/// equal-power crossfade holds roughly constant power across the overlap band
/// [60, 80] instead of dropping to silence at the band edge.
///
/// <para>Before the fix the renderer picked exactly ONE region per (pitch, vel)
/// cell and applied its sin()/cos() gain with no complementary layer summing
/// in, so vel=60 rendered TOTAL SILENCE (-inf dB) and vel=65/70 sat 6-11 dB
/// below the full-level baseline. The fix renders ALL overlapping xfin/xfout
/// layers and sums them (cos²+sin² = 1), removing the dropout.</para>
/// </summary>
[Collection("FlowScripts")]
public class SfzVelocityCrossfadeRenderTests : IDisposable
{
    public SfzVelocityCrossfadeRenderTests()
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

    private static SfzData LoadXfadeFixture(out string repoRoot)
    {
        repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "flow-lang.Tests", "fixtures",
            "Phase37", "velocity_xfade.sfz");
        var content = File.ReadAllText(path);
        return SfzParser.Parse(content, path, "velocity_xfade");
    }

    private static SfzSampleCache BuildCacheFor(SfzData patch, string repoRoot)
    {
        // Both regions reference ../sfz-smoke/C4_sine.wav relative to the
        // fixture's parent dir. Load it directly so the renderer never misses.
        var cache = new SfzSampleCache();
        string wavPath = Path.Combine(repoRoot, "flow-lang.Tests", "fixtures",
            "sfz-smoke", "C4_sine.wav");
        var buf = FileIO.LoadWavInternal(wavPath);
        foreach (var region in patch.Regions)
            cache.SetRaw_TestOnly(patch, region.SamplePath, buf);
        return cache;
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

    private static AudioBuffer RenderAtVelocity(SfzRenderer renderer, SfzData patch, int midiVel)
    {
        var note = new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 0,
            durationValue: 4, isRest: false,
            velocity: midiVel / 127.0,
            articulation: Articulation.Normal);
        return renderer.Render(note, sampleRate: 44100, durationBeats: 1.0, bpm: 120.0, patch);
    }

    [Fact]
    public void VelocityCrossfade_AcrossBand_NoDropout_NeverBelowReference()
    {
        var patch = LoadXfadeFixture(out var repoRoot);
        var cache = BuildCacheFor(patch, repoRoot);
        var renderer = new SfzRenderer(cache);

        // Reference level: velocity below the overlap band (full level, single
        // hard-switch layer — no xfade gain applies). Use vel=50.
        double refDb = RmsDb(RenderAtVelocity(renderer, patch, 50));
        Assert.False(double.IsNegativeInfinity(refDb), "reference render must not be silent");

        // Sweep across the overlap band [60, 80] (inclusive edges + interior).
        //
        // The fixture's two layers reference the SAME C4_sine.wav, so they sum
        // COHERENTLY: with per-layer gains cos(θ)+sin(θ) the amplitude is 1.0 at
        // the band edges and √2 (≈ +3 dB) at the center — never a dropout, never
        // below the single-layer reference. (Real patches use DISTINCT per-layer
        // recordings whose decorrelated power sums to a flat 0 dB; that is the
        // intended musical behavior, but the committed test fixture shares one
        // WAV, so we assert the coherent-sum envelope instead.)
        //
        // The ORIGINAL bug rendered ONE layer with its sin()/cos() gain and an
        // extra 0.7071: vel=60 → SILENCE, vel 65/70 → 6-11 dB BELOW reference.
        // The two invariants below reject every one of those:
        //   * never silent (no -inf dB)
        //   * never more than 0.5 dB BELOW the single-layer reference
        foreach (int vel in new[] { 60, 65, 70, 75, 80 })
        {
            var buf = RenderAtVelocity(renderer, patch, vel);
            double db = RmsDb(buf);
            Assert.False(double.IsNegativeInfinity(db),
                $"vel={vel} rendered TOTAL SILENCE — velocity crossfade dropout regressed");
            Assert.True(db >= refDb - 0.5,
                $"vel={vel}: RMS {db:F2} dB fell BELOW reference {refDb:F2} dB — " +
                "a velocity-crossfade hole/dropout regressed (layers must sum, not drop).");
            // Coherent ceiling for identical-source layers is √2 (+3.01 dB).
            Assert.True(db <= refDb + 3.5,
                $"vel={vel}: RMS {db:F2} dB exceeded the coherent-sum ceiling " +
                $"({refDb:F2}+3.5 dB) — unexpected gain stacking.");
        }
    }

    [Fact]
    public void VelocityCrossfade_BandLowEdge_IsNotSilent()
    {
        // Tightest regression on the exact failure: vel=60 was the band's LOW
        // edge where the surviving xfin layer's sin() hit 0 → total silence.
        var patch = LoadXfadeFixture(out var repoRoot);
        var cache = BuildCacheFor(patch, repoRoot);
        var renderer = new SfzRenderer(cache);

        var buf = RenderAtVelocity(renderer, patch, 60);
        double db = RmsDb(buf);
        Assert.False(double.IsNegativeInfinity(db),
            "vel=60 (band low edge) rendered silence — the SAMP-02 dropout bug regressed");
    }
}
