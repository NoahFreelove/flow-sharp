using FlowLang.Core;
using Xunit;

namespace FlowLang.Tests.Phase0615;

/// <summary>
/// Feature-addition 0615 (#7 break-control) — the prefix-only <c>(break)</c> /
/// <c>(continue)</c> call-position builtins for loop control.
///
/// <para>
/// Before this feature only the <c>break</c> / <c>continue</c> KEYWORD statements
/// existed (parse-time <c>_inLoop</c> gate). The builtins resolve at EVAL time, so
/// they additionally work inside lazy-wrapped positions — the <c>then</c>/<c>else</c>
/// branch of an <c>(if ...)</c>, where the keyword form is awkward. Implemented via a
/// <c>BreakSignal</c>/<c>ContinueSignal</c> the loop constructs catch; a runtime
/// <c>ExecutionContext.LoopDepth</c> counter lets a stray <c>(break)</c> outside any
/// loop be a charitable no-op (house style) rather than crash the render.
/// </para>
/// </summary>
public class BreakControlTests
{
    private static int RunInt(string source, string varName)
    {
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute("use \"@std\"\n" + source);
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        return engine.Context.GetVariable(varName)!.As<int>();
    }

    [Fact]
    public void BreakInsideWhile_StopsAtRightIteration()
    {
        // Loop would run to 100 without the break; (break) fires when count == 5.
        var count = RunInt(
            "Int count = 0\n" +
            "while (lt count 100) {\n" +
            "  count = (add count 1)\n" +
            "  (if (gte count 5) (break) (print \"tick\"))\n" +
            "}\n",
            "count");
        Assert.Equal(5, count);
    }

    [Fact]
    public void BreakInsideFor_LazyWrappedInIf_StopsEarly()
    {
        // (break) lives in the `then` branch of (if) — a LAZY position the keyword
        // form cannot occupy. seen increments only for n in {1,2,3}; n=4 breaks.
        var seen = RunInt(
            "Int seen = 0\n" +
            "for Int n in [1, 2, 3, 4, 5, 6, 7, 8] {\n" +
            "  (if (gt n 3) (break) (print \"ok\"))\n" +
            "  seen = (add seen 1)\n" +
            "}\n",
            "seen");
        Assert.Equal(3, seen);
    }

    [Fact]
    public void NestedLoops_BreakAffectsInnermostOnly()
    {
        // Inner loop breaks at j>=20 (so innerHits += 1 per outer pass, ×3 = 3).
        // The OUTER loop is unaffected and runs all 3 passes (outerHits == 3).
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(
            "use \"@std\"\n" +
            "Int outerHits = 0\n" +
            "Int innerHits = 0\n" +
            "for Int i in [1, 2, 3] {\n" +
            "  outerHits = (add outerHits 1)\n" +
            "  for Int j in [10, 20, 30, 40] {\n" +
            "    (if (gte j 20) (break) (print \"inner\"))\n" +
            "    innerHits = (add innerHits 1)\n" +
            "  }\n" +
            "}\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        Assert.Equal(3, engine.Context.GetVariable("outerHits")!.As<int>());
        Assert.Equal(3, engine.Context.GetVariable("innerHits")!.As<int>());
    }

    [Fact]
    public void ContinueBuiltin_SkipsRestOfBody()
    {
        // (continue) skips the `ctest` increment every iteration; the loop still
        // advances ci to its terminating value.
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(
            "use \"@std\"\n" +
            "Int ctest = 0\n" +
            "Int ci = 0\n" +
            "while (lt ci 3) {\n" +
            "  ci = (add ci 1)\n" +
            "  (continue)\n" +
            "  ctest = (add ctest 1)\n" +
            "}\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        Assert.Equal(0, engine.Context.GetVariable("ctest")!.As<int>());
        Assert.Equal(3, engine.Context.GetVariable("ci")!.As<int>());
    }

    [Fact]
    public void BreakOutsideLoop_IsCharitableNoOp_NoError()
    {
        // House style: a stray (break) with no enclosing loop is a NO-OP, not a
        // crash. Execution continues to the assignment after it.
        var marker = RunInt(
            "(break)\n" +
            "Int marker = 42\n",
            "marker");
        Assert.Equal(42, marker);
    }

    [Fact]
    public void ContinueOutsideLoop_IsCharitableNoOp_NoError()
    {
        var marker = RunInt(
            "(continue)\n" +
            "Int marker = 7\n",
            "marker");
        Assert.Equal(7, marker);
    }

    [Fact]
    public void BreakInsideProcCalledFromLoop_DoesNotBreakCallersLoop()
    {
        // The (break) lives in a proc that has NO loop of its own. Even though the
        // proc is CALLED from inside a loop, the builtin must NOT break the caller's
        // loop (control flow must not leak across the call boundary — parity with the
        // `break` keyword's lexical _inLoop gate). LoopDepth is zeroed across the
        // proc-call boundary, so (break) is a charitable no-op inside the proc body
        // and the loop runs all 3 iterations.
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(
            "use \"@std\"\n" +
            "proc tryBreak ()\n" +
            "  (break)\n" +
            "  Int unused = 1\n" +
            "end proc\n" +
            "Int hits = 0\n" +
            "for Int n in [1, 2, 3] {\n" +
            "  (tryBreak)\n" +
            "  hits = (add hits 1)\n" +
            "}\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        Assert.Equal(3, engine.Context.GetVariable("hits")!.As<int>());
    }

    [Fact]
    public void BreakKeywordStillWorks_NoRegression()
    {
        // The pre-existing `break` KEYWORD statement form must keep working
        // alongside the new (break) builtin.
        var bv = RunInt(
            "Int bv = 0\n" +
            "while true {\n" +
            "  bv = (add bv 1)\n" +
            "  break\n" +
            "}\n",
            "bv");
        Assert.Equal(1, bv);
    }
}
