using System;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_2;

/// <summary>
/// Phase 26.2 Wave 0 RED scaffolding (ERG-01 + ERG-04 type-level facts).
///
/// Mirrors the type-system block of
/// <see cref="FlowLang.Tests.Unit.QuickFixes.DecibelBeatNumericCompatFacts"/>.
/// Wave 1 (plan 02) ships the IsCompatibleWith overrides on Ms/Sec/Hertz that
/// flip the four numbered RED facts to GREEN. Hertz facts are skipped in Wave 0
/// because <c>HertzType</c> doesn't exist yet — Wave 1 ships the type, after
/// which the Skip attributes are removed in the same wave's commit.
///
/// CONTEXT D-03 sentinel: Semitone STAYS Int-only — see
/// <see cref="Semitone_NotCompatibleWith_Double_RegressionCanary"/>.
/// CentType regression canary mirrors the existing precedent in
/// <c>CentType.cs:24-27</c> (already-shipped behavior, must stay green).
/// </summary>
public class MusicTypeNumericCompatFacts
{
    // ===== Ms/Sec compat — Wave 1 makes these GREEN =====

    [Fact]
    public void Millisecond_IsCompatibleWith_Double_ReturnsTrue()
    {
        Assert.True(MillisecondType.Instance.IsCompatibleWith(DoubleType.Instance),
            "MillisecondType must be compatible with DoubleType so existing builtins " +
            "(e.g. delay(Buffer, Double, ...)) accept literal 100ms. Mirrors CentType.cs:24-27. " +
            "Wave 1 (plan 02) ships the override.");
    }

    [Fact]
    public void Millisecond_IsCompatibleWith_Float_ReturnsTrue()
    {
        Assert.True(MillisecondType.Instance.IsCompatibleWith(FloatType.Instance),
            "MillisecondType must also be compatible with FloatType (mirrors CentType " +
            "which lists both Double and Float in the same override expression).");
    }

    [Fact]
    public void Second_IsCompatibleWith_Double_ReturnsTrue()
    {
        Assert.True(SecondType.Instance.IsCompatibleWith(DoubleType.Instance),
            "SecondType must be compatible with DoubleType so existing builtins " +
            "(e.g. reverb(Buffer, Double, Double)) accept literal 1.5s. Mirrors CentType.cs:24-27. " +
            "Wave 1 (plan 02) ships the override.");
    }

    [Fact]
    public void Second_IsCompatibleWith_Float_ReturnsTrue()
    {
        Assert.True(SecondType.Instance.IsCompatibleWith(FloatType.Instance),
            "SecondType must also be compatible with FloatType (parity with the " +
            "Cent precedent which covers both).");
    }

    // ===== Hertz compat — Wave 1 ships HertzType, Wave 2 ships lexer; both must land for these =====

    [Fact]
    public void Hertz_IsCompatibleWith_Double_ReturnsTrue()
    {
        Assert.True(HertzType.Instance.IsCompatibleWith(DoubleType.Instance),
            "HertzType must be compatible with DoubleType so existing builtins " +
            "(e.g. lowpass(Buffer, Double)) accept literal 800Hz. Mirrors CentType.cs:24-27. " +
            "Wave 1 (plan 02) ships HertzType + the override.");
    }

    [Fact]
    public void Hertz_IsCompatibleWith_Float_ReturnsTrue()
    {
        Assert.True(HertzType.Instance.IsCompatibleWith(FloatType.Instance),
            "HertzType must also be compatible with FloatType (parity with the " +
            "Cent precedent which covers both).");
    }

    // ===== Regression canaries — STAY GREEN through every wave =====

    /// <summary>
    /// CONTEXT D-03 sentinel: Semitone is INT-only and MUST NOT widen to Double
    /// (would break (transpose seq 5) which currently resolves to the Semitone
    /// overload via Int compat). If this fact regresses, we accidentally widened
    /// SemitoneType.IsCompatibleWith.
    /// </summary>
    [Fact]
    public void Semitone_NotCompatibleWith_Double_RegressionCanary()
    {
        Assert.False(SemitoneType.Instance.IsCompatibleWith(DoubleType.Instance),
            "SemitoneType must REMAIN Int-only per CONTEXT D-03 — widening to Double " +
            "would change the resolution shape of (transpose seq 5).");
    }

    /// <summary>
    /// Existing precedent: CentType.IsCompatibleWith(Double) returns true (CentType.cs:24-27).
    /// If this regresses, our changes have leaked into shared resolution logic.
    /// </summary>
    [Fact]
    public void Cent_StillCompatibleWith_Double_RegressionCanary()
    {
        Assert.True(CentType.Instance.IsCompatibleWith(DoubleType.Instance),
            "CentType.IsCompatibleWith(Double) must STAY true — this is the precedent " +
            "Wave 1 mirrors for Ms/Sec/Hertz. A regression here would cascade.");
    }
}
