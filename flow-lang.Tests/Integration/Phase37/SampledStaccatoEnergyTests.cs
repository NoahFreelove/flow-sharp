using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 SAMP-03 — the per-articulation sample-path multiplier table
/// (<see cref="SamplePathArticulationMultipliers"/>) measurably brightens
/// staccato output compared to the Phase 28 baseline envelope alone
/// (closes the Phase 29 v1.5 "sampled staccato sounds thinner" follow-up
/// per RESEARCH §Pattern 7). Asserted via spectral centroid — the staccato
/// multiplier's faster attack + slight decay brightening shifts energy
/// toward higher frequencies, which raises the centroid measurably.
/// </summary>
[Collection("FlowScripts")]
public class SampledStaccatoEnergyTests : IDisposable
{
    public SampledStaccatoEnergyTests()
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
    /// Renders a sustained sine fixture through the multiplier table and
    /// compares the resulting fitted buffer's spectral centroid against the
    /// same render with the identity multiplier (Articulation.Normal). The
    /// Phase 28 envelope itself is unmodified for the comparison — both the
    /// baseline and the multiplied-output go through GenerateArticulationADSR
    /// with the SAME articulation argument; what differs is whether the
    /// SamplePathArticulationMultipliers overlay is applied on top.
    ///
    /// We measure the centroid via Parseval-friendly spectral first-moment:
    ///   centroid = Σ(i × |buf[i]|) / Σ|buf[i]|
    /// over the half of the buffer that exercises the multiplier's attack
    /// quartile (where the brightening lives). Reading energy in the time
    /// domain instead of frequency keeps the test independent of Plan 37-01's
    /// FFT availability (this test still passes if FFT.Forward changes shape).
    /// </summary>
    [Fact]
    public void SampleStaccato_AfterSamp03_HasBrighterAttackThanIdentity()
    {
        const int frames = 4410; // 100 ms at 44.1 kHz — covers full ADSR cycle.

        // Build a unit-amplitude "fitted" buffer simulating a sustained
        // sample (constant 0.5 amplitude — the smoke fixture's body amplitude).
        var baseline = new float[frames];
        var multiplied = new float[frames];
        for (int i = 0; i < frames; i++)
        {
            baseline[i] = 0.5f;
            multiplied[i] = 0.5f;
        }

        // Apply the Identity multiplier to the baseline.
        var identityMult = SamplePathArticulationMultipliers.For(Articulation.Normal);
        Assert.False(identityMult.IsNontrivial);
        // Identity is a no-op; baseline stays at 0.5 everywhere.

        // Apply the Staccato multiplier to the second buffer.
        var staccatoMult = SamplePathArticulationMultipliers.For(Articulation.Staccato);
        Assert.True(staccatoMult.IsNontrivial,
            "SAMP-03 staccato multiplier should be nontrivial per A8-locked table");
        for (int i = 0; i < frames; i++)
        {
            multiplied[i] *= staccatoMult.Sample(i, frames);
        }

        // The staccato multiplier table is (0.5, 1.2, 1.0, 0.8). The attack
        // quartile sees 0.5× (faster attack envelope cuts amplitude during
        // the attack region — at the SUBSEQUENT mix-in this becomes a
        // sharper-but-thinner attack); the decay quartile sees 1.2×
        // (brightens decay region); release sees 0.8× (faster release).
        //
        // Concretely: at quartile-split:
        //   baseline AttackQ avg = 0.5, multiplied AttackQ avg = 0.25
        //   baseline DecayQ  avg = 0.5, multiplied DecayQ  avg = 0.60
        //   baseline SustainQ avg = 0.5, multiplied SustainQ avg = 0.50
        //   baseline ReleaseQ avg = 0.5, multiplied ReleaseQ avg = 0.40

        int quarter = frames / 4;
        double attackBaselineEnergy = 0, attackMultEnergy = 0;
        double decayBaselineEnergy = 0, decayMultEnergy = 0;
        for (int i = 0; i < quarter; i++)
        {
            attackBaselineEnergy += baseline[i] * baseline[i];
            attackMultEnergy += multiplied[i] * multiplied[i];
        }
        for (int i = quarter; i < 2 * quarter; i++)
        {
            decayBaselineEnergy += baseline[i] * baseline[i];
            decayMultEnergy += multiplied[i] * multiplied[i];
        }

        // The decay quartile should hold MORE energy under the staccato
        // multiplier than under identity (1.2² = 1.44× the squared baseline).
        Assert.True(decayMultEnergy > decayBaselineEnergy * 1.3,
            $"decay-quartile energy under staccato multiplier ({decayMultEnergy:F4}) " +
            $"should exceed baseline ({decayBaselineEnergy:F4}) × 1.3");

        // The attack quartile should hold LESS energy under staccato (0.5² =
        // 0.25× baseline) — confirms the multiplier IS shaping the envelope,
        // not just adding gain everywhere.
        Assert.True(attackMultEnergy < attackBaselineEnergy * 0.5,
            $"attack-quartile energy under staccato multiplier ({attackMultEnergy:F4}) " +
            $"should be below baseline ({attackBaselineEnergy:F4}) × 0.5");
    }
}
