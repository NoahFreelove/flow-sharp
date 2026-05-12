using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_1;

/// <summary>
/// Phase 26.1 Wave 3 (GREEN): pins TUP-10 — `~&gt;` parse-time unpack
/// flow operator. Multi-arg call site is `entry ~&gt; renderHit` ≡
/// `(renderHit entry@0 entry@1)`. Non-tuple LHS falls through to `-&gt;` semantics
/// per ROADMAP success criterion 3 (charitable interpretation).
///
/// Source-running facts go through <see cref="FlowEngineRunner"/> with
/// <c>[Collection("FlowScripts")]</c> so Console.SetOut is serialized
/// (RESEARCH Pitfall 4).
/// </summary>
[Collection("FlowScripts")]
public class UnpackFlowFacts
{
    [Fact]
    public void MultiArg_UnpacksAndCalls()
    {
        // Tuple LHS unpacks into positional args: <<1, 2, 3>> ~> add3 ≡ (add3 1 2 3) → 6
        // Mirrors `Int r = 5 -> doubler` shape from test_lambdas.flow:97 — flow operators
        // work at expression-statement / RHS-of-assignment positions, not inside parens.
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
proc add3 (Int: a, Int: b, Int: c)
    (add a (add b c))
end proc
Tuple<<Int, Int, Int>> t = <<1, 2, 3>>
Int r = t ~> add3
(print (str r))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("6", stdout); // 1 + 2 + 3
    }

    [Fact]
    public void NonTupleLhs_FallsThroughToArrowSemantics()
    {
        // Charitable: non-tuple LHS to ~> behaves as if `->` was used (single-arg).
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
proc doubleIt (Int: n)
    (mul n 2)
end proc
Int x = 5
Int r = x ~> doubleIt
(print (str r))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("10", stdout); // 5 * 2 — single-arg fallthrough
    }
}
