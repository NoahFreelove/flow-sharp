using FlowLang.Core;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-07 Wave 0 — `-> CALL as name` scope gates (LANG-03).
///
/// Pins Pitfall 7's composer-visible scope model: the `as` clause binds
/// the result in the CURRENT frame, so subsequent chain steps and same-
/// block statements can read it, but the binding dies with the enclosing
/// proc / block. Verified by running short FlowEngine programs and
/// inspecting last-expression Values + error-reporter state.
///
/// RED state: EvaluateFlowExpression doesn't yet call DeclareVariable
/// after computing a chain result. Task 4 wires the declaration; these
/// tests flip GREEN with it.
/// </summary>
public class AsBindingScopeTests
{
    private static Value? Eval(string source)
    {
        using var engine = new FlowEngine(verbose: false);
        return engine.ExecuteScriptAndGetResult(source);
    }

    [Fact]
    public void BindingVisibleToSubsequentChainSteps()
    {
        // 5 -> (mul 2) as doubled -> (add doubled)
        //   step 1: (mul 5 2) -> 10, bound as `doubled`
        //   step 2: (add 10 doubled) -> 10 + 10 = 20
        // The chain step `(add doubled)` MUST read the bound name from the
        // CURRENT frame (Pitfall 7).
        var v = Eval("use \"@std\"\nInt x = (5 -> (mul 2) as doubled -> (add doubled))\n(print (str x))");
        // ExecuteScriptAndGetResult returns last-expression value; (print ...)
        // returns Void — so we also assert via Execute + inspecting variable.
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(
            "use \"@std\"\nInt x = (5 -> (mul 2) as doubled -> (add doubled))");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        var x = engine.Context.GetVariable("x");
        Assert.Equal(20, x.As<int>());
    }

    [Fact]
    public void BindingVisibleToSameBlockStatement()
    {
        // proc test() {
        //   5 -> (mul 2) as doubled;
        //   Int captured = doubled
        // }
        // After the chain, `doubled` must be visible to the next statement
        // in the SAME block frame (per Pitfall 7's composer-visible model
        // — "available from this point onward in the enclosing scope").
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(
            "use \"@std\"\n" +
            "5 -> (mul 2) as doubled\n" +
            "Int captured = doubled\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        var captured = engine.Context.GetVariable("captured");
        Assert.Equal(10, captured.As<int>());
    }

    [Fact]
    public void BindingDoesNotEscapeProcBoundary()
    {
        // proc inner() { 5 -> (mul 2) as doubled }
        // proc outer() { (inner) (print doubled) }   <-- `doubled` must be unknown here
        //
        // The binding dies with inner's frame on PopFrame; the outer scope
        // never sees it. Either Execute returns false OR ErrorReporter
        // surfaces an "undefined" diagnostic.
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(
            "use \"@std\"\n" +
            "proc inner() { 5 -> (mul 2) as doubled }\n" +
            "proc outer() { (inner) (print (str doubled)) }\n" +
            "(outer)\n");
        var errors = engine.ErrorReporter.FormatErrors();
        var doubledLeaked = ok && !errors.Contains("doubled", System.StringComparison.OrdinalIgnoreCase);
        Assert.False(
            doubledLeaked,
            "Binding 'doubled' leaked past inner()'s frame. Errors: " + errors);
    }
}
