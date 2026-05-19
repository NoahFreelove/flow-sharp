using FlowLang.Core;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-05 Wave 0 — runtime match-evaluation gates (LANG-01).
///
/// Drives <see cref="FlowEngine"/> on small (match ...) sources and inspects
/// the resulting last-expression <see cref="Value"/>. Pins:
///
///   1. First-match-wins (no C-style fall-through).
///   2. WildcardPattern unconditional match.
///   3. BindingPattern binds the scrutinee into the arm-body frame.
///   4. Bindings die with the arm-body frame (Pitfall 6).
///   5. GuardPattern fires only when the guard expression evaluates truthy.
///
/// RED state: ExpressionEvaluator has no MatchExpression dispatch arm yet —
/// the runtime throws NotSupportedException at the first (match ...). Task 4
/// flips this GREEN.
/// </summary>
public class MatchRuntimeTests
{
    private static Value? Eval(string source)
    {
        using var engine = new FlowEngine(verbose: false);
        return engine.ExecuteScriptAndGetResult(source);
    }

    [Fact]
    public void FirstMatchWins()
    {
        // Two identical literal arms — first must win, second must not execute.
        var v = Eval("(match 5 | 5 => \"first\" | 5 => \"second\")");
        Assert.NotNull(v);
        Assert.Equal("first", v!.As<string>());
    }

    [Fact]
    public void WildcardMatchesAnything()
    {
        var v = Eval("(match 42 | _ => \"any\")");
        Assert.NotNull(v);
        Assert.Equal("any", v!.As<string>());
    }

    [Fact]
    public void BindingPatternBindsScrutinee()
    {
        // The bare identifier `n` captures 42; arm body multiplies by 2.
        // The (mul ...) builtin lives in @std — load it so the arm body
        // resolves the function name.
        var v = Eval("use \"@std\"\n(match 42 | n => (mul n 2))");
        Assert.NotNull(v);
        Assert.Equal(84, v!.As<int>());
    }

    [Fact]
    public void BindingDoesNotLeakToEnclosingScope()
    {
        // After the match, `n` MUST NOT be defined in the enclosing scope.
        // The test executes a 2-statement program: first the match (which
        // would bind n=42 ONLY for the arm body), then a top-level reference
        // to n. The second statement must produce an error because n is
        // undefined outside the arm frame.
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute("(match 42 | n => n)\n(print n)");
        // Either Execute returns false (n unknown → error reported) OR the
        // engine surfaced an "Undefined variable" error. Pitfall 6 holds
        // iff at least one of those signals fires.
        var errors = engine.ErrorReporter.FormatErrors();
        var nLeaked = ok && !errors.Contains("n", System.StringComparison.OrdinalIgnoreCase);
        Assert.False(nLeaked, $"Binding 'n' leaked past the match arm. Errors: {errors}");
    }

    [Fact]
    public void GuardPatternFiresOnlyWhenGuardTrue()
    {
        // Positive case — n=5, guard (gt n 0) → true, first arm fires.
        // (greater ...) is a @std builtin; load it so the guard predicate resolves.
        var pos = Eval("use \"@std\"\n(match 5 | n when (gt n 0) => \"pos\" | _ => \"neg\")");
        Assert.NotNull(pos);
        Assert.Equal("pos", pos!.As<string>());

        // Negative case — n=-5, guard → false, falls through to wildcard.
        var neg = Eval("use \"@std\"\n(match -5 | n when (gt n 0) => \"pos\" | _ => \"neg\")");
        Assert.NotNull(neg);
        Assert.Equal("neg", neg!.As<string>());
    }
}
