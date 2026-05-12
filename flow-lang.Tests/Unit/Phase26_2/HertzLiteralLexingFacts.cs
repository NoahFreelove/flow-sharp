using System;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_2;

/// <summary>
/// Phase 26.2 Wave 0 RED scaffolding (ERG-04 D-12 lexer facts).
///
/// All facts are skipped in Wave 0 because the Hz/kHz lexer arms ship in Wave 2
/// (plan 03). When the arms land, the Skip attributes are removed in the same
/// wave's commit and the engine-eval body executes.
///
/// CONTEXT D-12 / RESEARCH Pitfall 4: Hz lexing must be tried BEFORE Note lexing
/// in <c>SimpleLexer</c> so <c>800Hz</c> doesn't accidentally tokenize as
/// <c>800</c> + identifier <c>Hz</c>; kHz must be tried before Hz so
/// <c>1.5kHz</c> doesn't tokenize as <c>1.5k</c> + <c>Hz</c>.
/// All Hz literals canonicalize to a Double-backed Hertz Value (kHz × 1000).
/// </summary>
public class HertzLiteralLexingFacts
{
    [Fact]
    public void Hz_LexesToCanonicalDouble()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
Hertz freq = 800Hz
");
        Assert.Equal(0, errorCount);
        Assert.False(stderr.Contains("Unexpected", StringComparison.OrdinalIgnoreCase),
            $"800Hz must lex as a single HertzLiteral token, got: {stderr}");
        Assert.Equal(800.0, runner.GetVariable("freq").As<double>(), precision: 6);
    }

    [Fact]
    public void kHz_LexesToCanonicalDouble()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
Hertz freq = 1.5kHz
");
        Assert.Equal(0, errorCount);
        Assert.False(stderr.Contains("Unexpected", StringComparison.OrdinalIgnoreCase),
            $"1.5kHz must lex as a single HertzLiteral token canonicalized to 1500.0, got: {stderr}");
        Assert.Equal(1500.0, runner.GetVariable("freq").As<double>(), precision: 6);
    }

    [Fact]
    public void Hz_AfterLParen_LexesAsHertzLiteral()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src = (createSineTone 0.05 1100.0 0.5)
Buffer x = (lowpass src 800Hz)
");
        Assert.Equal(0, errorCount);
        Assert.False(stderr.Contains("Unexpected identifier 'Hz'", StringComparison.OrdinalIgnoreCase),
            $"800Hz at expression-start (after LParen) must lex as a single HertzLiteral token, got: {stderr}");
        Assert.False(stderr.Contains("No matching overload", StringComparison.OrdinalIgnoreCase),
            $"lowpass(Buffer, Hertz) overload should resolve, got: {stderr}");
    }

    [Fact]
    public void kHz_AfterLParen_LexesAsHertzLiteral()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src = (createSineTone 0.05 220.0 0.5)
Buffer x = (lowpass src 1.5kHz)
");
        Assert.Equal(0, errorCount);
        Assert.False(stderr.Contains("Unexpected", StringComparison.OrdinalIgnoreCase),
            $"1.5kHz at expression-start (after LParen) must lex as a single HertzLiteral token, got: {stderr}");
        Assert.False(stderr.Contains("No matching overload", StringComparison.OrdinalIgnoreCase),
            $"lowpass(Buffer, Hertz) overload should resolve, got: {stderr}");
    }
}
