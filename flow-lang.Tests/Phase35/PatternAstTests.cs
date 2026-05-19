using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Patterns;
using FlowLang.Core;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-05 Wave 0 — Pattern AST family construction + record-equality
/// gates (LANG-01). Asserts every pattern subtype:
///
///   1. Can be constructed with a non-null Span argument.
///   2. Reports that Span via the inherited Span init-property (not null,
///      not <see cref="Span.Unknown"/> when an explicit Span is provided).
///   3. Supports C# record value-equality (two patterns with equal payloads
///      and equal Span are <c>.Equals</c> true).
///
/// Also pins MatchArm's value-record shape and the MatchExpression sibling
/// in Ast/Expressions/.
///
/// RED state: Pattern records and MatchExpression do not yet exist — the
/// file fails to compile until Task 2 lands the AST types.
/// </summary>
public class PatternAstTests
{
    private static readonly SourceLocation Loc = new(1, 1, "<test>");
    private static readonly Span TestSpan = new(Loc, new SourceLocation(1, 5, "<test>"));

    [Fact]
    public void AllPatternKindsHaveSpan()
    {
        var lit = new LiteralPattern(Loc, 42, Span: TestSpan);
        var wild = new WildcardPattern(Loc, Span: TestSpan);
        var bind = new BindingPattern(Loc, "n", Span: TestSpan);
        var ctor = new ConstructorPattern(Loc, "Cmaj7", new List<Pattern>(), Span: TestSpan);
        var guard = new GuardPattern(
            Loc,
            bind,
            new LiteralExpression(Loc, true, Span: TestSpan),
            Span: TestSpan);

        Assert.NotNull(lit.Span);
        Assert.NotEqual(Span.Unknown, lit.Span);
        Assert.NotNull(wild.Span);
        Assert.NotEqual(Span.Unknown, wild.Span);
        Assert.NotNull(bind.Span);
        Assert.NotEqual(Span.Unknown, bind.Span);
        Assert.NotNull(ctor.Span);
        Assert.NotEqual(Span.Unknown, ctor.Span);
        Assert.NotNull(guard.Span);
        Assert.NotEqual(Span.Unknown, guard.Span);
    }

    [Fact]
    public void MatchArmIsValueRecord()
    {
        var p1 = new BindingPattern(Loc, "x", Span: TestSpan);
        var p2 = new BindingPattern(Loc, "x", Span: TestSpan);
        var body1 = new LiteralExpression(Loc, 1, Span: TestSpan);
        var body2 = new LiteralExpression(Loc, 1, Span: TestSpan);

        var arm1 = new MatchArm(p1, body1, Span: TestSpan);
        var arm2 = new MatchArm(p2, body2, Span: TestSpan);

        Assert.Equal(arm1, arm2);
    }

    [Fact]
    public void MatchExpressionLivesInExpressionsFolder()
    {
        // RESEARCH § Recommended Project Structure puts MatchExpression in
        // Ast/Expressions/, not in Ast/Patterns/. Verify the namespace.
        var scrutinee = new VariableExpression(Loc, "x", Span: TestSpan);
        var arms = new List<MatchArm>
        {
            new(new WildcardPattern(Loc, Span: TestSpan),
                new LiteralExpression(Loc, 0, Span: TestSpan),
                Span: TestSpan),
        };
        var match = new MatchExpression(Loc, scrutinee, arms, Span: TestSpan);

        Assert.Equal("FlowLang.Ast.Expressions", typeof(MatchExpression).Namespace);
        // And Pattern types live in Ast.Patterns
        Assert.Equal("FlowLang.Ast.Patterns", typeof(WildcardPattern).Namespace);
        // Sanity — MatchExpression is an Expression
        Assert.IsAssignableFrom<Expression>(match);
    }

    [Fact]
    public void ConstructorPatternFlagsDefaultFalse()
    {
        // Plan 35-06 sets IsChordLiteral / IsRomanNumeral / IsArticulationSymbol
        // via the parser when the source matches those token shapes. Plan 35-05
        // ships them defaulted to false so the runtime falls through to
        // structural / silent-Void behavior.
        var ctor = new ConstructorPattern(Loc, "Cmaj7", new List<Pattern>(), Span: TestSpan);
        Assert.False(ctor.IsChordLiteral);
        Assert.False(ctor.IsRomanNumeral);
        Assert.False(ctor.IsArticulationSymbol);
    }
}
