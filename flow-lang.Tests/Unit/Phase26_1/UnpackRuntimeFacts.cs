using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_1;

/// <summary>
/// Phase 26.1 Wave 3 (GREEN): pins TUP-11 — <c>(unpack tuple func)</c>
/// value-level runtime builtin (mirror of Lisp <c>(apply f args)</c>).
/// Exactly 4 theory cases per CONTEXT § (unpack tuple func): zero-arg,
/// single-arg, multi-arg, dynamic-Function.
///
/// Source-running facts go through <see cref="FlowEngineRunner"/> with
/// <c>[Collection("FlowScripts")]</c> so Console.SetOut is serialized
/// (RESEARCH Pitfall 4).
/// </summary>
[Collection("FlowScripts")]
public class UnpackRuntimeFacts
{
    [Fact]
    public void ZeroArg_EmptyTuple()
    {
        // (unpack <<>> getFortyTwo) → 42.
        // NOTE: zero-arg `proc getFortyTwo ()` is auto-called as a bare identifier
        // (ExpressionEvaluator.EvaluateVariable line 150 — 0-arg function shortcut).
        // To pass the FUNCTION as a value to (unpack), use a Function-typed binding
        // (`fn => 42` lambda) — same path the spec calls out.
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Function getFortyTwo = fn => 42
Tuple<<>> e = <<>>
Int r = (unpack e getFortyTwo)
(print (str r))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("42", stdout);
    }

    [Fact]
    public void SingleArg_OneTuple()
    {
        // (unpack <<5>> doubler) → 10
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
proc doubler (Int: n)
    (mul n 2)
end proc
Tuple<<Int>> s = <<5>>
Int r = (unpack s doubler)
(print (str r))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("10", stdout);
    }

    [Fact]
    public void MultiArg_TwoTuple()
    {
        // (unpack <<3, 4>> addPair) → 7
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
proc addPair (Int: a, Int: b)
    (add a b)
end proc
Tuple<<Int, Int>> p = <<3, 4>>
Int r = (unpack p addPair)
(print (str r))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("7", stdout);
    }

    [Fact]
    public void DynamicFunctionValue()
    {
        // Function-typed VARIABLE used at the (unpack) call site — dynamic dispatch path.
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Function f = fn Int a, Int b => (mul a b)
Tuple<<Int, Int>> p = <<6, 7>>
Int r = (unpack p f)
(print (str r))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("42", stdout); // 6 * 7
    }
}
