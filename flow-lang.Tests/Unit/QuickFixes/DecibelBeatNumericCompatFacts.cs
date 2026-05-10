using System;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.QuickFixes;

/// <summary>
/// QUICK-260504-w24 regression facts: Decibel and Beat must be numerically
/// compatible with Double/Float (mirroring the existing CentType precedent at
/// CentType.cs:24-27) so well-typed musical expressions like
/// <c>(gain rendered -12dB)</c> resolve to the existing
/// <c>gain(Buffer, Double)</c> overload (which already interprets its second
/// argument as dB) instead of producing
/// <c>"No matching overload for function 'gain' with argument types (Buffer, Decibel)"</c>.
///
/// Pre-fix state: Decibel and Beat have ZERO IsCompatibleWith / CanConvertTo
/// overrides, so <see cref="FunctionSignature.Matches"/> rejects them at every
/// Double parameter slot. Cent already overrides IsCompatibleWith and works.
///
/// Fix surface (intentionally minimal):
///   1. <c>DecibelType.IsCompatibleWith(Double|Float)</c> -> true
///   2. <c>BeatType.IsCompatibleWith(Double|Float)</c> -> true
///   3. Explicit <c>gain(Buffer, Decibel)</c> overload registration delegating
///      to the same lambda (exact-match score parity with
///      <c>transpose(Sequence, Cent)</c>).
///
/// Out of scope (deferred to Phase 26.2): Millisecond/Second/Semitone compat,
/// gain dB-vs-linear semantic policy, Hertz type, delay/compress sidechain
/// Decibel overloads.
/// </summary>
public class DecibelBeatNumericCompatFacts
{
    // ===== Type-system level (4 facts) — fastest to fail, narrowest scope =====

    [Fact]
    public void Decibel_IsCompatibleWith_Double_ReturnsTrue()
    {
        Assert.True(DecibelType.Instance.IsCompatibleWith(DoubleType.Instance),
            "DecibelType must be compatible with DoubleType so existing builtins " +
            "(e.g. gain(Buffer, Double)) accept literal -12dB. Mirrors CentType.cs:24-27.");
    }

    [Fact]
    public void Decibel_IsCompatibleWith_Float_ReturnsTrue()
    {
        Assert.True(DecibelType.Instance.IsCompatibleWith(FloatType.Instance),
            "DecibelType must also be compatible with FloatType (mirrors CentType " +
            "which lists both Double and Float in the same override expression).");
    }

    [Fact]
    public void Beat_IsCompatibleWith_Double_ReturnsTrue()
    {
        Assert.True(BeatType.Instance.IsCompatibleWith(DoubleType.Instance),
            "BeatType is stored as a fractional double, so passing a Beat to a " +
            "Double-typed parameter must match (parity with Cent precedent).");
    }

    [Fact]
    public void Beat_IsCompatibleWith_Float_ReturnsTrue()
    {
        Assert.True(BeatType.Instance.IsCompatibleWith(FloatType.Instance),
            "BeatType is stored as a fractional double, so passing a Beat to a " +
            "Float-typed parameter must match.");
    }

    // ===== Engine-eval — proves the fix flows through OverloadResolver and the registered builtins =====

    /// <summary>
    /// Source-level proof: <c>(gain src -12dB)</c> resolves to the same dB-interpreting
    /// code path as <c>(gain src -12.0)</c>, producing per-sample identical buffers.
    /// Pre-fix: errorCount > 0 with stderr matching "No matching overload for function 'gain'".
    /// Post-fix: identical output, errorCount == 0.
    /// </summary>
    [Fact]
    public void GainWithDecibelLiteral_ResolvesAndProducesSameOutputAsDoubleEquivalent()
    {
        using var runner = new FlowEngineRunner();
        // -12.0 is bound to a variable so the lexer doesn't ambiguate the leading `-`
        // with infix subtraction at the call site (`gain src -12.0` parses as
        // `gain (src - 12.0)` because the `-` after a Buffer-typed identifier binds
        // as a binary operator). The dB literal does not have this issue because the
        // `dB` suffix forces a single DecibelLiteral token even with a leading sign.
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src = (createSineTone 0.05 220.0 0.5)
Double minus12 = -12.0
Buffer attenDb     = (gain src -12dB)
Buffer attenDouble = (gain src minus12)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        Assert.False(stderr.Contains("No matching overload", StringComparison.OrdinalIgnoreCase),
            $"Decibel literal should not produce an overload error, got: {stderr}");

        var byDb = runner.GetVariable("attenDb").As<AudioBuffer>();
        var byDouble = runner.GetVariable("attenDouble").As<AudioBuffer>();

        Assert.Equal(byDouble.Data.Length, byDb.Data.Length);
        Assert.True(byDb.Data.Length > 0, "Sine tone buffer must be non-empty");

        for (int i = 0; i < byDb.Data.Length; i++)
        {
            Assert.True(MathF.Abs(byDb.Data[i] - byDouble.Data[i]) < 1e-6f,
                $"Per-sample mismatch at i={i}: dB={byDb.Data[i]}, double={byDouble.Data[i]}");
        }
    }

    /// <summary>
    /// Semantic sanity: +6dB applies ~2.0x linear gain, 0dB applies 1.0x.
    /// Asserts the linear ratio (gainResult / src) per non-near-zero sample.
    /// Skips samples where the source is ~0 to avoid div-by-near-zero noise;
    /// asserts the non-skipped count > 100 to confirm the fact actually exercised
    /// the code path (catches the failure mode where everything got skipped).
    /// </summary>
    [Fact]
    public void GainWithPositiveDecibelLiteral_AppliesExpectedLinearGain()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src      = (createSineTone 0.05 220.0 0.5)
Buffer plus6    = (gain src +6dB)
Buffer unityDb  = (gain src 0dB)
");
        Assert.Equal(0, errorCount);
        Assert.False(stderr.Contains("No matching overload", StringComparison.OrdinalIgnoreCase),
            $"Positive Decibel literal should not produce an overload error, got: {stderr}");

        var src = runner.GetVariable("src").As<AudioBuffer>();
        var plus6 = runner.GetVariable("plus6").As<AudioBuffer>();
        var unity = runner.GetVariable("unityDb").As<AudioBuffer>();

        // +6 dB == 10^(6/20) == ~1.99526 linear
        float expectedPlus6 = (float)Math.Pow(10.0, 6.0 / 20.0);
        // 0 dB == 1.0 linear
        const float expectedUnity = 1.0f;

        int comparedPlus6 = 0;
        int comparedUnity = 0;
        for (int i = 0; i < src.Data.Length; i++)
        {
            if (MathF.Abs(src.Data[i]) < 1e-6f) continue;

            float ratioPlus6 = plus6.Data[i] / src.Data[i];
            float ratioUnity = unity.Data[i] / src.Data[i];

            Assert.InRange(ratioPlus6, expectedPlus6 - 0.001f, expectedPlus6 + 0.001f);
            Assert.InRange(ratioUnity, expectedUnity - 0.001f, expectedUnity + 0.001f);

            comparedPlus6++;
            comparedUnity++;
        }

        Assert.True(comparedPlus6 > 100,
            $"Expected at least 100 comparable samples, got {comparedPlus6} — the fact " +
            "did not actually exercise the gain path on enough non-zero samples.");
        Assert.True(comparedUnity > 100,
            $"Expected at least 100 comparable samples for unityDb, got {comparedUnity}.");
    }

    /// <summary>
    /// Beat-level signature compat — proves Beat reaches Double parameter slots
    /// through the OverloadResolver / FunctionSignature.Matches path generically
    /// (not just via specific builtins). Asserted at the type-system level
    /// because Beat has no source-level literal and no Beat-producing builtin
    /// is registered today; this fact is the equivalent generic proof.
    /// </summary>
    [Fact]
    public void BeatArgument_MatchesFunctionSignatureWithDoubleParameter()
    {
        // Synthetic signature mirroring "proc takesDouble(Double x) -> Double { x }"
        var doubleParamSig = new FunctionSignature(
            "takesDouble",
            new FlowType[] { DoubleType.Instance });

        Assert.True(doubleParamSig.Matches(new FlowType[] { BeatType.Instance }),
            "A signature with a Double parameter must accept a Beat-typed argument " +
            "after the IsCompatibleWith override is in place. This is the generic proof " +
            "that Beat reaches every Double parameter site (builtins, user procs, lambdas).");

        // And Float parameter slot, matching the symmetric override clause.
        var floatParamSig = new FunctionSignature(
            "takesFloat",
            new FlowType[] { FloatType.Instance });

        Assert.True(floatParamSig.Matches(new FlowType[] { BeatType.Instance }),
            "Beat -> Float must also match (mirrors the Cent precedent which covers both).");
    }

    /// <summary>
    /// Regression canary: the existing Cent -> Double compat path must remain intact.
    /// <c>(transpose seq 50.0)</c> is a Double argument matching the
    /// <c>transpose(Sequence, Cent)</c> overload via Cent.IsCompatibleWith(Double)
    /// applied as the third leaf of <see cref="FunctionSignature.Matches"/>
    /// (InputTypes[i].IsCompatibleWith(argTypes[i])). If this fact regresses,
    /// our changes have leaked into shared resolution logic.
    /// </summary>
    [Fact]
    public void Cent_DoubleCompat_NotRegressed_TransposeWithDoubleStillResolves()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
Sequence transposed = (transpose (| C4 D4 E4 |) 50.0)
");
        Assert.Equal(0, errorCount);
        Assert.False(stderr.Contains("No matching overload", StringComparison.OrdinalIgnoreCase),
            $"transpose(Sequence, Double) must still resolve to transpose(Sequence, Cent), got: {stderr}");
    }
}
