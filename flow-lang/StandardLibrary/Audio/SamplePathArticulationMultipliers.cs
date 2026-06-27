using System;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Phase 37 SAMP-03 — per-articulation scalar ADSR multipliers that stack
/// multiplicatively on top of Phase 28's locked
/// <see cref="SynthUtils.GenerateArticulationADSR"/> output.
///
/// <para><b>Why this exists</b> (per CLAUDE.md "Known sampled-instrument
/// quirks" + RESEARCH §Pattern 7): under Phase 28's locked staccato envelope
/// (25% duration + sustain=0 + release×0.5), the SAMPLE-path envelope cuts
/// before the sample body develops. Sampled staccato sounds thinner than the
/// equivalent synth-path articulation. This multiplier table closes that
/// perceptual gap by applying a per-stage scalar multiplier to the Phase 28
/// envelope output BEFORE it multiplies the sample buffer.</para>
///
/// <para><b>Pitfall 10 scoping (T-37-03-04)</b>: this multiplier is consumed
/// ONLY by sample-path callers (<see cref="Sfz.SfzRenderer"/> and Plan 37-04's
/// <see cref="SampledInstrumentRenderer"/>). Phase 28's
/// <c>SynthUtils.GenerateArticulationADSR</c> itself is unchanged — the
/// synth-path callers receive the Phase 28 baseline as before, so Phase 28's
/// articulation RMS regression tests stay green.</para>
///
/// <para><b>A8 (Option A scalar ADSR multiplier)</b> locked per CONTEXT
/// "Claude's Discretion" + RESEARCH §Pattern 7: scalar per-stage multipliers
/// (Option A) chosen over full per-frame curve overlay (Option B) for lower
/// risk + composability. Escalates to Option B only if subsequent UAT
/// iterations still flag the staccato gap.</para>
///
/// <para><b>Locked multiplier table</b> (CLAUDE.md "Locked articulation rules"
/// composed with the SAMP-03 multiplicative stack — values chosen to
/// demonstrably brighten staccato while leaving the other articulations near
/// identity):
/// <list type="bullet">
///   <item><description><c>Normal</c> → (1.0, 1.0, 1.0, 1.0) — no-op identity.</description></item>
///   <item><description><c>Staccato</c> → (0.5, 1.2, 1.0, 0.8) — 2× faster attack
///   + slight decay brightening; primary closer for the Phase 29 v1.5
///   thinness gap.</description></item>
///   <item><description><c>Marcato</c> → (0.6, 1.1, 1.0, 0.9) — milder staccato shape.</description></item>
///   <item><description><c>Tenuto</c> → (1.0, 1.0, 1.0, 1.05) — slight release
///   lengthening for a softer tail.</description></item>
///   <item><description><c>Legato</c> → (1.0, 1.0, 1.0, 1.0) — no-op; Phase 28's
///   envelope is already shaped for legato.</description></item>
///   <item><description><c>Accent</c> → (0.7, 1.0, 1.0, 1.0) — slightly faster
///   attack to emphasize the accent's transient.</description></item>
///   <item><description><c>Sforzando</c> → (0.5, 1.0, 1.0, 1.0) — sharpened
///   attack only; Phase 28's 1.5×→1.0× spike already handles the body.</description></item>
/// </list>
/// </para>
///
/// <para><b>Sample frame layout</b> (<see cref="SamplePathMultiplier.Sample"/>):
/// quartile-split A/D/S/R buckets across <c>totalFrames</c> — frames
/// <c>[0, N/4)</c> use AttackMult, <c>[N/4, N/2)</c> use DecayMult,
/// <c>[N/2, 3N/4)</c> use SustainMult, <c>[3N/4, N]</c> use ReleaseMult.
/// Simple and predictable; full ADSR-stage-aware multiplier curves are
/// reserved for the Option B escalation path.</para>
/// </summary>
public static class SamplePathArticulationMultipliers
{
    /// <summary>
    /// Returns the SAMP-03 scalar multiplier triple for <paramref name="art"/>.
    /// Stacks AFTER <see cref="SynthUtils.ApplyEnvelope"/> at the SFZ /
    /// sample-path caller site.
    /// </summary>
    public static SamplePathMultiplier For(Articulation art) => art switch
    {
        // A8 locked table (RESEARCH §Pattern 7 Option A).
        Articulation.Staccato  => new SamplePathMultiplier(0.5, 1.2, 1.0, 0.8),
        Articulation.Marcato   => new SamplePathMultiplier(0.6, 1.1, 1.0, 0.9),
        Articulation.Tenuto    => new SamplePathMultiplier(1.0, 1.0, 1.0, 1.05),
        Articulation.Legato    => SamplePathMultiplier.Identity,
        Articulation.Accent    => new SamplePathMultiplier(0.7, 1.0, 1.0, 1.0),
        Articulation.Sforzando => new SamplePathMultiplier(0.5, 1.0, 1.0, 1.0),
        // Articulation.Normal + any future enum addition → identity.
        _ => SamplePathMultiplier.Identity,
    };
}

/// <summary>
/// Phase 37 SAMP-03 — per-stage scalar multiplier triple bundled with a
/// quartile-split <see cref="Sample(int, int)"/> accessor. Returned by
/// <see cref="SamplePathArticulationMultipliers.For"/>; consumed at the
/// SFZ / sample-path caller site after Phase 28's envelope has been
/// applied. <see cref="IsNontrivial"/> short-circuits the multiplication
/// loop for the Identity case so the common path (Normal / Legato) costs
/// nothing beyond a boolean read.
/// </summary>
public readonly struct SamplePathMultiplier
{
    public double AttackMult { get; }
    public double DecayMult { get; }
    public double SustainMult { get; }
    public double ReleaseMult { get; }

    /// <summary>
    /// True when any stage multiplier differs from 1.0. Callers can skip
    /// the per-sample multiply when this is false (the Phase 28 baseline
    /// output is already the desired audio).
    /// </summary>
    public bool IsNontrivial { get; }

    /// <summary>Singleton identity multiplier (all 1.0).</summary>
    public static SamplePathMultiplier Identity { get; } =
        new SamplePathMultiplier(1.0, 1.0, 1.0, 1.0);

    public SamplePathMultiplier(double attack, double decay, double sustain, double release)
    {
        AttackMult = attack;
        DecayMult = decay;
        SustainMult = sustain;
        ReleaseMult = release;
        const double Epsilon = 1e-9;
        IsNontrivial =
            Math.Abs(attack - 1.0) > Epsilon ||
            Math.Abs(decay - 1.0) > Epsilon ||
            Math.Abs(sustain - 1.0) > Epsilon ||
            Math.Abs(release - 1.0) > Epsilon;
    }

    /// <summary>
    /// Returns the scalar multiplier for frame <paramref name="frameIndex"/>
    /// in a buffer of <paramref name="totalFrames"/> total length, using a
    /// quartile-split A/D/S/R bucket layout. Inputs outside <c>[0, totalFrames)</c>
    /// clamp to the nearest valid bucket; <paramref name="totalFrames"/> &lt; 4
    /// degenerate cases (very short notes) all fall back to AttackMult so the
    /// transient brightening still applies.
    /// </summary>
    public float Sample(int frameIndex, int totalFrames)
    {
        if (totalFrames < 4) return (float)AttackMult;
        if (frameIndex < 0) return (float)AttackMult;
        int quarter = totalFrames / 4;
        if (frameIndex < quarter) return (float)AttackMult;
        if (frameIndex < 2 * quarter) return (float)DecayMult;
        if (frameIndex < 3 * quarter) return (float)SustainMult;
        return (float)ReleaseMult;
    }
}
