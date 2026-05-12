using System;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_2;

/// <summary>
/// Phase 26.2 Wave 0 — D-14 / D-15 / RESEARCH Pitfall 1 closure facts.
///
/// Verifies signed music literals (<c>-12dB</c>, <c>+6dB</c>, <c>-100ms</c>,
/// <c>-50c</c>, <c>+440Hz</c>) at expression-start positions (after LParen, etc.)
/// lex as a single signed-music-literal token rather than splitting into
/// <c>(- 12dB)</c> binary subtraction.
///
/// The non-skipped facts (<see cref="NegativeDecibel_AfterLParen_LexesAsDecibelLiteral"/>,
/// <see cref="PositiveDecibel_AfterLParen_LexesAsDecibelLiteral"/>,
/// <see cref="NegativeCent_AfterLParen_LexesAsCentLiteral"/>) are GREEN immediately
/// after the Wave 0 Value.ConvertTo Double-arm patch lands — proving the
/// defence-in-depth root-cause fix (RESEARCH Pitfall 1) closes the
/// pre-existing RED <c>DecibelBeatNumericCompatFacts.GainWith…</c> facts
/// AND makes -12dB / +6dB / -50c parse-and-resolve cleanly via the
/// dedicated music-typed overloads.
///
/// Skipped facts (<see cref="NegativeMs_AfterLParen_LexesAsTimeLiteral"/> and
/// <see cref="PositiveHertz_AfterLParen_LexesAsHertzLiteral"/>) un-skip in
/// Waves 2-3 once the Ms/Hertz overloads ship.
/// </summary>
public class NegativeMusicLiteralFacts
{
    [Fact]
    public void NegativeDecibel_AfterLParen_LexesAsDecibelLiteral()
    {
        using var runner = new FlowEngineRunner();
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
            $"-12dB after LParen should resolve to gain(Buffer, Decibel), got: {stderr}");

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

    [Fact]
    public void PositiveDecibel_AfterLParen_LexesAsDecibelLiteral()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src = (createSineTone 0.05 220.0 0.5)
Double plus6 = 6.0
Buffer ampDb     = (gain src +6dB)
Buffer ampDouble = (gain src plus6)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        Assert.False(stderr.Contains("No matching overload", StringComparison.OrdinalIgnoreCase),
            $"+6dB after LParen should resolve to gain(Buffer, Decibel), got: {stderr}");

        var byDb = runner.GetVariable("ampDb").As<AudioBuffer>();
        var byDouble = runner.GetVariable("ampDouble").As<AudioBuffer>();
        Assert.Equal(byDouble.Data.Length, byDb.Data.Length);
        Assert.True(byDb.Data.Length > 0, "Sine tone buffer must be non-empty");
        for (int i = 0; i < byDb.Data.Length; i++)
        {
            Assert.True(MathF.Abs(byDb.Data[i] - byDouble.Data[i]) < 1e-6f,
                $"Per-sample mismatch at i={i}: dB={byDb.Data[i]}, double={byDouble.Data[i]}");
        }
    }

    [Fact]
    public void NegativeMs_AfterLParen_LexesAsTimeLiteral()
    {
        // After Wave 3: delay(Buffer, Millisecond, Double, Double) overload exists.
        // -100ms is nonsensical as a delay length, but we're testing the LEXER —
        // it must produce a single signed TimeLiteral token rather than splitting
        // on the leading '-'. The contract asserted here is ONLY that the call
        // parses + resolves to the Millisecond overload — the runtime DSP path
        // may reject the negative value with its own error, which is fine.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, _) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src = (createSineTone 0.05 220.0 0.5)
Buffer dMs = (delay src -100ms 0.5 0.4)
");
        // The lex/parse pipeline emits "Unexpected token" when token splitting
        // fails — that string MUST NOT appear if -100ms lexed as a single
        // signed TimeLiteral. The DSP runtime emits its own "Delay time must be
        // positive" error, which is allowed (different layer, different
        // contract).
        Assert.False(stderr.Contains("Unexpected token", StringComparison.OrdinalIgnoreCase),
            $"-100ms after LParen must lex as a single signed TimeLiteral token, got: {stderr}");
        // Resolution must succeed — no overload-mismatch error.
        Assert.False(stderr.Contains("No matching overload", StringComparison.OrdinalIgnoreCase),
            $"delay(Buffer, Millisecond, Double, Double) overload should resolve with -100ms, got: {stderr}");
    }

    /// <summary>
    /// Regression canary — the existing -50c lex path already works because
    /// CentType.IsCompatibleWith(Double) ships and the bare-Double
    /// transpose(Sequence, Double) overload exists (or transpose(Sequence, Cent)
    /// resolves at exact-match score 1000). Stays GREEN through every wave.
    /// Mirrors <c>DecibelBeatNumericCompatFacts.Cent_DoubleCompat_NotRegressed_…</c>.
    /// </summary>
    [Fact]
    public void NegativeCent_AfterLParen_LexesAsCentLiteral()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
Sequence transposed = (transpose (| C4 D4 E4 |) -50c)
");
        Assert.Equal(0, errorCount);
        Assert.False(stderr.Contains("No matching overload", StringComparison.OrdinalIgnoreCase),
            $"transpose(Sequence, Cent) with -50c must still resolve, got: {stderr}");
        Assert.False(stderr.Contains("Unexpected", StringComparison.OrdinalIgnoreCase),
            $"-50c after LParen must lex as a single signed CentLiteral token, got: {stderr}");
    }

    [Fact]
    public void PositiveHertz_AfterLParen_LexesAsHertzLiteral()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src = (createSineTone 0.05 220.0 0.5)
Buffer x = (lowpass src +440Hz)
");
        Assert.Equal(0, errorCount);
        Assert.False(stderr.Contains("Unexpected", StringComparison.OrdinalIgnoreCase),
            $"+440Hz after LParen must lex as a single signed HertzLiteral token, got: {stderr}");
        Assert.False(stderr.Contains("No matching overload", StringComparison.OrdinalIgnoreCase),
            $"lowpass(Buffer, Hertz) overload should resolve with +440Hz, got: {stderr}");
    }
}
