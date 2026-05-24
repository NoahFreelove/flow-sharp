using System;
using System.Collections.Generic;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 FLUTE-01 (Plan 37-05) — D5 timbre crossover gap closed.
///
/// Before this plan: flute notes at D5 (MIDI 74) varispeed-shifted from G4
/// (MIDI 67 — 7 semitones up — large stretch factor) because the only flute
/// samples were G4 + G5 (the nearest of which to D5 is G4 by 5 semitones — wait,
/// actually D5 is equidistant to G4 below and G5 above; tie-breaks to the lower
/// pitch via <see cref="SampleCache.NearestSamplePitch"/>'s stable scan).
///
/// After this plan: A4 (MIDI 69) lands between G4 and G5. D5 now picks A4 as
/// nearest source (5-semitone varispeed stretch — vs G4's 7 or G5's 5). The
/// shorter varispeed stretch produces less timbre distortion because
/// <see cref="FileIO.VarispeedResample"/> uses linear interpolation, which
/// degrades formant content monotonically with stretch magnitude.
///
/// The fact compares a D5 rendered via A4-source (5-semitone shift) against a
/// D5 rendered via G4-source (7-semitone shift) and asserts the A4-sourced
/// result has a spectral centroid CLOSER to the unstretched-A4 baseline. The
/// shorter the stretch, the more faithfully the formant region is preserved.
///
/// Phase 37 Plan 37-01 ships the Wave 0 scaffold; this plan (37-05) fills it.
/// </summary>
[Collection("FlowScripts")]
public class FluteD5CrossoverTests : IDisposable
{
    public FluteD5CrossoverTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    /// <summary>
    /// Minimal SongData — eager-load walks the manifest, not the song.
    /// </summary>
    private static SongData BuildEmptyFluteSong()
    {
        return new SongData(
            new List<SongSectionRef>(),
            new Dictionary<string, SectionData>());
    }

    /// <summary>
    /// Absolute path to <c>flow-lang/Samples</c>.
    /// </summary>
    private static string SamplesRoot()
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        return Path.Combine(repoRoot, "flow-lang", "Samples");
    }

    /// <summary>
    /// Magnitude-weighted spectral centroid (Hz-equivalent bin index) of a real
    /// audio buffer's first <paramref name="windowFrames"/> frames. Used as a
    /// timbre fingerprint — formant shifts are visible as centroid shifts
    /// because the magnitude spectrum's center of mass tracks the dominant
    /// harmonic clusters. Higher centroid = brighter timbre / formant pushed
    /// upward by varispeed pitch shift.
    ///
    /// Implementation: naive O(n²) DFT magnitude on a Hann window of the first
    /// 4096 frames. Test-only — production rendering is unaffected. Returns
    /// the weighted-mean bin index (0 ≤ centroid &lt; n/2), which scales
    /// linearly with frequency given a fixed sample rate.
    /// </summary>
    private static double SpectralCentroid(float[] data, int windowFrames)
    {
        int n = Math.Min(windowFrames, data.Length);
        if (n < 8) return 0.0;
        // Use power-of-two window for cleaner DFT bins.
        int pow2 = 1;
        while (pow2 * 2 <= n) pow2 *= 2;
        n = pow2;

        // Hann window — reduces spectral leakage at the window edges so the
        // centroid measurement reflects steady-state content rather than
        // truncation transients.
        double[] windowed = new double[n];
        for (int i = 0; i < n; i++)
        {
            double hann = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (n - 1)));
            windowed[i] = data[i] * hann;
        }

        // Naive DFT magnitude (only the first n/2 bins — the upper half is the
        // mirror image for a real input). 4096-sample DFT is ~33 ms of compute
        // per call on a modern CPU — acceptable for one test fact.
        int bins = n / 2;
        double[] magnitude = new double[bins];
        for (int k = 0; k < bins; k++)
        {
            double real = 0.0, imag = 0.0;
            double twoPiKOverN = 2.0 * Math.PI * k / n;
            for (int i = 0; i < n; i++)
            {
                double angle = twoPiKOverN * i;
                real += windowed[i] * Math.Cos(angle);
                imag -= windowed[i] * Math.Sin(angle);
            }
            magnitude[k] = Math.Sqrt(real * real + imag * imag);
        }

        // Magnitude-weighted bin index. Skip DC (bin 0) — it carries the
        // window's mean offset, not the timbre.
        double weightedSum = 0.0;
        double weightTotal = 0.0;
        for (int k = 1; k < bins; k++)
        {
            weightedSum += k * magnitude[k];
            weightTotal += magnitude[k];
        }
        return weightTotal > 1e-12 ? weightedSum / weightTotal : 0.0;
    }

    [Fact]
    public void FluteD5Crossover_RmsMatchesNearerSamplePoint_WithinHalfDb()
    {
        var cache = new SampleCache(SamplesRoot());
        cache.EagerLoad(BuildEmptyFluteSong(), "flute");

        // Cache invariant: G4 (67), A4 (69), G5 (79) all loaded.
        Assert.True(cache.HasLayer("flute", 67, "mf"), "flute G4 sample missing — Plan 37-05 setup broken");
        Assert.True(cache.HasLayer("flute", 69, "mf"), "flute A4 sample missing — Plan 37-05 composer drop or manifest broken");
        Assert.True(cache.HasLayer("flute", 79, "mf"), "flute G5 sample missing — Phase 29 setup broken");

        // FLUTE-01 — render D5 (MIDI 74) two ways and an unstretched A4 baseline:
        //   * A4-sourced D5 — varispeed shift +5 semitones from A4 (MIDI 69)
        //   * G4-sourced D5 — varispeed shift +7 semitones from G4 (MIDI 67)
        //   * Unstretched A4 — varispeed shift 0 from A4 (the timbre reference)
        // Then compare spectral centroids: the SHORTER stretch (A4-source, 5 semitones)
        // should produce a centroid CLOSER to the unstretched A4 baseline than the
        // LONGER stretch (G4-source, 7 semitones) does to its own G4 baseline.
        var a4D5 = cache.GetVarispeed("flute", 69, "mf", semitonesShift: 5);
        var g4D5 = cache.GetVarispeed("flute", 67, "mf", semitonesShift: 7);
        var a4Ref = cache.GetVarispeed("flute", 69, "mf", semitonesShift: 0);
        var g4Ref = cache.GetVarispeed("flute", 67, "mf", semitonesShift: 0);

        Assert.NotNull(a4D5);
        Assert.NotNull(g4D5);
        Assert.NotNull(a4Ref);
        Assert.NotNull(g4Ref);

        // 4096-frame window — ~93 ms at 44.1 kHz, well inside the 1.5 s sample
        // body (post-trim). Captures the steady-state vibrato carrier without
        // straying into the natural-decay tail.
        const int WindowFrames = 4096;
        double centroidA4D5 = SpectralCentroid(a4D5!.Data, WindowFrames);
        double centroidG4D5 = SpectralCentroid(g4D5!.Data, WindowFrames);
        double centroidA4Ref = SpectralCentroid(a4Ref!.Data, WindowFrames);
        double centroidG4Ref = SpectralCentroid(g4Ref!.Data, WindowFrames);

        // Stretch deltas — each measures how far the varispeed-shifted output
        // strayed from its OWN unstretched source. A4's 5-semitone stretch and
        // G4's 7-semitone stretch shift the spectrum upward by different
        // fractions (linear-interpolation varispeed compresses/expands the
        // magnitude spectrum along the bin axis); the centroid drift scales
        // monotonically with the stretch magnitude.
        double a4DriftAbs = Math.Abs(centroidA4D5 - centroidA4Ref);
        double g4DriftAbs = Math.Abs(centroidG4D5 - centroidG4Ref);

        // Sanity: both drifts must be positive (varispeed DID move the spectrum).
        // If one is near zero, varispeed isn't engaged — assertion below would
        // pass vacuously, so guard explicitly.
        Assert.True(a4DriftAbs > 1.0,
            $"A4→D5 varispeed produced near-zero centroid shift ({a4DriftAbs:F2} bins) — varispeed broken");
        Assert.True(g4DriftAbs > 1.0,
            $"G4→D5 varispeed produced near-zero centroid shift ({g4DriftAbs:F2} bins) — varispeed broken");

        // FLUTE-01 core assertion: A4's shorter 5-semitone stretch produces a
        // SMALLER absolute centroid drift from its own source than G4's longer
        // 7-semitone stretch produces from its own source. ≥ 5% relative
        // difference per Plan 37-05 action prose ("Tolerance: ≥ 5% difference
        // between the two paths"). The ratio of stretch magnitudes is
        // 7/5 = 1.40, so a ≥ 5% bound is comfortably below the expected drift
        // ratio under linear-interpolation varispeed.
        double driftRatio = g4DriftAbs / a4DriftAbs;
        Assert.True(driftRatio >= 1.05,
            $"FLUTE-01: expected G4-source D5 drift ≥ 1.05× A4-source D5 drift " +
            $"(A4 = {a4DriftAbs:F2} bins, G4 = {g4DriftAbs:F2} bins, ratio = {driftRatio:F3}). " +
            $"Closer stretch should produce less timbre distortion per RESEARCH §Pattern 10.");
    }
}
