using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_1;

/// <summary>
/// Phase 26.1 Wave 4 (GREEN): pins the Float-key NaN special case from
/// CONTEXT § Hashable enforcement timing — <c>(set d nan v)</c> then
/// <c>(get d nan)</c> returns <c>v</c> (Dict-internal NaN-eq-NaN), while Flow's
/// general <c>(equals nan nan)</c> continues to follow IEEE 754 (returns false).
/// This is the Pitfall 3 regression gate: verifies <c>Utils.StrictEquals</c>
/// was NOT modified — only the Dict hash/eq path has the special case.
///
/// REVISION 2 (plan-checker iter-1 BLOCKER fix): the substitute (div 0.0 0.0)
/// added in REVISION 1 does NOT work — Flow's (div) builtins throw
/// InvalidOperationException("Division by zero") for zero divisor; they do NOT
/// pass through to IEEE 754 NaN. Task 1 step 0a adds a new (nanFloat) zero-arg
/// builtin that returns Value.Float(double.NaN). All NaN-producing test paths
/// in this file use (nanFloat).
///
/// Source-running facts go through <see cref="FlowEngineRunner"/> with
/// <c>[Collection("FlowScripts")]</c> so Console.SetOut is serialized
/// (RESEARCH Pitfall 4).
/// </summary>
[Collection("FlowScripts")]
public class DictNanKeyFacts
{
    [Fact]
    public void NanFloatBuiltin_ProducesNaN()
    {
        // REVISION 2 — NEW regression guard. Locks (nanFloat) builtin behavior
        // independent of the Dict path. (equals nan nan) returns false per IEEE 754,
        // so this Fact serves double duty: confirms (nanFloat) actually produced NaN
        // (because (equals X X) on a non-NaN value would return true) AND confirms
        // Utils.StrictEquals still honors IEEE 754 (Pitfall 3 invariant).
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Float n = (nanFloat)
Bool isReflexive = (equals n n)
(print (str isReflexive))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("false", stdout);
    }

    [Fact]
    public void NanAsSelfInDict()
    {
        // REVISION 2: use (nanFloat) — see file-level comment.
        // CONTEXT § Float NaN edge case: NaN-as-key MUST behave reflexively
        // INSIDE a Dict<Float, V> via DictKeyComparer's FloatType branch
        // (Pitfall 3: scoped to Dict only).
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Float nanVal = (nanFloat)
Dict<Float, Int> nanD = (dict nanVal 42)
Int found = (get nanD nanVal)
(print (str found))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("42", stdout);
    }

    [Fact]
    public void GeneralEqualityUnchanged()
    {
        // REGRESSION GUARD: Pitfall 3 — global (equals NaN NaN) MUST still return false.
        // If this fact ever turns RED, Utils.StrictEquals was incorrectly modified.
        // REVISION 2: use (nanFloat) — see file-level comment. The Dict-internal
        // NaN-as-self special case is exercised by NanAsSelfInDict above; this fact
        // guarantees that special case did NOT bleed into general Utils.StrictEquals.
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Float nanVal = (nanFloat)
Bool r = (equals nanVal nanVal)
(print (str r))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("false", stdout);
    }
}
