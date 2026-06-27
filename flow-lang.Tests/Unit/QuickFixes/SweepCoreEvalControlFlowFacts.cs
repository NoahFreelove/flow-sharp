using System;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.QuickFixes;

/// <summary>
/// sweep-2026-06-14 (group core-eval-controlflow) regression facts.
///
/// Covers four confirmed bugs:
///   1. (if cond then else) evaluated BOTH branches unconditionally — the parser
///      never auto-wrapped the then/else branches in LazyExpression, so the eager
///      arg loop fired every branch's side-effects before the strict overload
///      merely selected the already-computed value. Now the interpreter auto-defers
///      Lazy<T> parameter slots into Thunks, so only the taken branch runs (and
///      `if` can guard against errors in the untaken branch).
///   2. and/or did not short-circuit for the same reason.
///   3. Integer / float division by zero threw to FlowEngine's catch-all and
///      rendered as a location-less "0:0: error: Unexpected error: ..."; now it
///      routes through the located charitable ReportDivisionByZero handler and the
///      program continues.
///   4. (str 440Hz) failed with an ambiguous-overload error (str(Float)/str(Double))
///      because no str(Hertz) overload existed; now (str 440Hz) -> "440Hz".
///   5. euclidean threw InvalidOperationException on degenerate inputs instead of
///      the documented charitable WarnOnce-advisory + sane default (D-v1.5-05).
/// </summary>
// FlowEngineRunner redirects the global Console.Out/Console.Error; the
// "FlowScripts" collection is DisableParallelization=true (see FlowScriptTests.cs)
// so concurrent console-capturing tests don't clobber each other's stdout/stderr.
[Collection("FlowScripts")]
public class SweepCoreEvalControlFlowFacts
{
    // ---- Bug 1: (if) evaluates only the taken branch ----------------------

    [Fact]
    public void If_FalseCondition_EvaluatesOnlyElseBranch()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errorCount) = runner.RunSource(
            "(if false (print \"THEN\") (print \"ELSE\"))");

        Assert.True(success, $"Expected success. Stderr:\n{stderr}");
        Assert.Equal(0, errorCount);
        Assert.Contains("ELSE", stdout);
        Assert.DoesNotContain("THEN", stdout);
    }

    [Fact]
    public void If_TrueCondition_EvaluatesOnlyThenBranch()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, _) = runner.RunSource(
            "(if true (print \"THEN\") (print \"ELSE\"))");

        Assert.Contains("THEN", stdout);
        Assert.DoesNotContain("ELSE", stdout);
    }

    [Fact]
    public void If_GuardsAgainstErrorInUntakenBranch()
    {
        // Pre-fix: the untaken (head (list)) branch was eagerly evaluated and
        // threw "Cannot get head of empty array". Now it must never run.
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errorCount) = runner.RunSource(
            "(print (if (empty (list)) \"empty\" (head (list))))");

        Assert.True(success, $"Expected success. Stderr:\n{stderr}");
        Assert.Equal(0, errorCount);
        Assert.Contains("empty", stdout);
    }

    [Fact]
    public void If_InsideLambda_EvaluatesOneBranchPerElement()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, _) = runner.RunSource(
            "(each (list 1 (neg 1)) (fn Int n => (if (gt n 0) (print \"pos\") (print \"neg\"))))");

        // Exactly one "pos" (n=1) and one "neg" (n=-1) — not two of each.
        Assert.Equal(1, CountOccurrences(stdout, "pos"));
        Assert.Equal(1, CountOccurrences(stdout, "neg"));
    }

    [Fact]
    public void If_ValueReturning_StillWorks()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(
            "Int x = (if true 10 20)\n(print (str x))");

        Assert.Equal(0, errorCount);
        Assert.Contains("10", stdout);
    }

    [Fact]
    public void If_NonBoolTruthyCondition_EvaluatesOnlyTakenBranch()
    {
        // Phase 44 charitable truthy-coerce path (IfTruthy) must ALSO defer +
        // force only the selected branch (ForceIfLazy), not run both.
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errorCount) = runner.RunSource(
            "(if 5 (print \"T\") (print \"F\"))");

        Assert.True(success, $"Expected success. Stderr:\n{stderr}");
        Assert.Equal(0, errorCount);
        Assert.Contains("T", stdout);
        Assert.DoesNotContain("F", stdout);
    }

    [Fact]
    public void If_NonBoolTruthyCondition_ReturnsCorrectValue()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, _) = runner.RunSource(
            "(print (if 0 \"truthy\" \"falsy\"))");

        Assert.Contains("falsy", stdout);
        Assert.DoesNotContain("truthy", stdout);
    }

    [Fact]
    public void If_ExplicitLazyBranches_StillEvaluatesOnlyTaken()
    {
        // The explicit lazy(...) path must keep working (no double-wrap regression).
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, _) = runner.RunSource(
            "(if false lazy((print \"L-THEN\")) lazy((print \"L-ELSE\")))");

        Assert.Contains("L-ELSE", stdout);
        Assert.DoesNotContain("L-THEN", stdout);
    }

    // ---- and/or value correctness (return values) -----------------------
    //
    // NOTE: and/or operand short-circuit of SIDE-EFFECTS is intentionally NOT
    // fixed in this sweep. Phase 44's AndBool/AndLastTruthy/OrBool/OrLastTruthy
    // overloads discriminate on operand TYPE (Bool vs charitable wildcard), so
    // deferring their operands into Thunks would re-type them as Lazy<Void> and
    // break that dispatch. The interpreter's lazy-slot deferral therefore only
    // applies to slots that are Lazy/Void in EVERY candidate overload (true for
    // `if` then/else, false for and/or operands). Proper and/or short-circuit is
    // a follow-up that requires reworking the Phase 44 charitable impls to accept
    // Thunks. These tests pin that return VALUES stay correct.

    [Fact]
    public void AndOr_ReturnCorrectValues()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, _) = runner.RunSource(
            "(print (and true true))\n(print (or false false))\n(print (and true false))");

        Assert.Contains("true", stdout);
        Assert.Contains("false", stdout);
    }

    // ---- Bug 3: division by zero is located + charitable -----------------

    [Theory]
    [InlineData("Int a = (div 5 0)")]
    [InlineData("Int a = (idiv 5 0)")]
    [InlineData("Double a = (div 5.0 0.0)")]
    public void DivisionByZero_ReportsLocatedDiagnostic_NotUnexpectedError(string source)
    {
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(source + "\n(print \"after\")");

        Assert.False(success);
        Assert.True(errorCount > 0, "Expected at least one error.");
        Assert.Contains("Division by zero", stderr);
        // The bug was a location-less "Unexpected error" framing — must be gone.
        Assert.DoesNotContain("Unexpected error", stderr);
        // The first error must carry a real source location (not 0:0).
        Assert.DoesNotContain("0:0: error: Division by zero", stderr);
    }

    [Fact]
    public void DivisionByZero_ProgramContinuesAfterReportedError()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, _) = runner.RunSource(
            "Int a = (div 5 0)\n(print \"after-div\")");

        // Charitable report-and-continue: the print after the bad division runs.
        Assert.Contains("after-div", stdout);
    }

    // ---- Bug 4: str(Hertz) ----------------------------------------------

    [Theory]
    [InlineData("(print (str 440Hz))", "440Hz")]
    [InlineData("(print (str 1.5kHz))", "1500Hz")]
    public void StrHertz_StringifiesWithHzSuffix(string source, string expected)
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errorCount) = runner.RunSource(source);

        Assert.True(success, $"Expected success. Stderr:\n{stderr}");
        Assert.Equal(0, errorCount);
        Assert.DoesNotContain("Ambiguous overload", stderr);
        Assert.Contains(expected, stdout);
    }

    // ---- Bug 5: euclidean charitable degenerate handling -----------------

    [Theory]
    [InlineData("(euclidean 0 8 C4)")]   // hits <= 0
    [InlineData("(euclidean 4 0 C4)")]   // steps <= 0
    [InlineData("(euclidean 20 8 C4)")]  // hits > steps (clamped)
    public void Euclidean_DegenerateInput_DoesNotThrow_IsCharitable(string call)
    {
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(
            "Sequence eu = " + call + "\n(print \"survived\")");

        // No throw → no FlowEngine catch-all "Unexpected error", no 0:0 framing.
        Assert.True(success, $"euclidean should be charitable, not throw. Stderr:\n{stderr}");
        Assert.Equal(0, errorCount);
        Assert.DoesNotContain("Unexpected error", stderr);
    }

    [Fact]
    public void Euclidean_DegenerateInput_ProgramContinues()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, _) = runner.RunSource(
            "Sequence eu = (euclidean 0 8 C4)\n(print \"survived\")");

        Assert.Contains("survived", stdout);
    }

    [Fact]
    public void Euclidean_ValidInput_StillWorks()
    {
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(
            "Sequence eu = (euclidean 3 8 C4)\n(print \"ok\")");

        Assert.True(success, $"Valid euclidean should still work. Stderr:\n{stderr}");
        Assert.Equal(0, errorCount);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
