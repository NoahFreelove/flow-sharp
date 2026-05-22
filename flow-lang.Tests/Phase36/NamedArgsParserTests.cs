using System.Collections.Generic;
using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-02 — named-argument syntax facts (D-36-11 universal rollout).
///
/// Tasks 1 + 2: signature/AST extension, then parser recognition of
/// <c>Identifier=Expression</c> in call position via the 2-token peek. The
/// resolver-side dispatch facts (Test 7-11) live in
/// <see cref="NamedArgsResolverTests"/>; the positional-call regression gate
/// (Test 12) lives in <see cref="NamedArgBackcompatTests"/>.
///
/// <para>
/// Design pinch (D-36-11): <see cref="FunctionSignature.ParameterNames"/> and
/// <see cref="FunctionCallExpression.NamedArgs"/> are BOTH defaulted-null —
/// signatures registered without ParameterNames remain functional with
/// positional-only calls. Plans 36-03 + 36-04 backfill the ~350 existing
/// builtin signatures in parallel; until then, the resolver raises a graceful
/// advisory when a named-arg call targets a not-yet-annotated signature
/// (see <see cref="NamedArgsResolverTests"/> Test 11).
/// </para>
/// </summary>
public class NamedArgsParserTests
{
    private static Program ParseSource(string source)
    {
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, reporter);
        var tokens = lexer.Tokenize();
        Assert.False(reporter.HasErrors, $"Lexer errors: {reporter.FormatErrors()}");
        var parser = new Parser(tokens, reporter);
        var program = parser.Parse();
        Assert.False(reporter.HasErrors, $"Parser errors: {reporter.FormatErrors()}");
        return program;
    }

    private static FunctionCallExpression FirstCall(Program program, string name)
    {
        foreach (var stmt in program.Statements)
        {
            var expr = stmt switch
            {
                ExpressionStatement es => es.Expression,
                VariableDeclaration vd => vd.Value,
                _ => null,
            };
            if (expr is FunctionCallExpression fc && fc.Name == name) return fc;
        }
        throw new Xunit.Sdk.XunitException($"Expected a FunctionCallExpression named '{name}' at top level.");
    }

    // ===================================================================
    // Task 1 — AST + signature extension (defaulted-positional fields)
    // ===================================================================

    [Fact]
    public void SignatureParameterNamesDefaultsToNull()
    {
        // Backfill safety net: every pre-Phase-36 FunctionSignature construction
        // must continue compiling. The default-null shape is the contract that
        // unblocks Plans 36-03/04 to backfill in parallel.
        var sig = new FunctionSignature(
            "transpose",
            new List<FlowType> { SequenceType.Instance, SemitoneType.Instance });
        Assert.Null(sig.ParameterNames);
    }

    [Fact]
    public void SignatureParameterNamesAccepted()
    {
        // Named-arg-aware signature: ParameterNames passed as the LAST
        // positional parameter, mirroring Phase 35 LANG-03 IntermediateName
        // sweep convention (defaulted-positional, additive, no new record).
        var sig = new FunctionSignature(
            "transpose",
            new List<FlowType> { SequenceType.Instance, SemitoneType.Instance },
            ParameterNames: new[] { "seq", "amount" });
        Assert.NotNull(sig.ParameterNames);
        Assert.Equal(2, sig.ParameterNames!.Count);
        Assert.Equal("seq", sig.ParameterNames[0]);
        Assert.Equal("amount", sig.ParameterNames[1]);
    }

    // ===================================================================
    // Task 2 — parser named-arg recognition (2-token peek)
    // ===================================================================

    [Fact]
    public void ParsesAllNamedArgs()
    {
        // `(transpose seq=foo amount=2)` — both args delivered as named.
        // Arguments list MUST be empty; NamedArgs carries both bindings.
        var program = ParseSource("(transpose seq=foo amount=2)");
        var call = FirstCall(program, "transpose");
        Assert.Empty(call.Arguments);
        Assert.NotNull(call.NamedArgs);
        Assert.Equal(2, call.NamedArgs!.Count);
        Assert.True(call.NamedArgs.ContainsKey("seq"));
        Assert.True(call.NamedArgs.ContainsKey("amount"));
    }

    [Fact]
    public void ParsesMixedPositionalThenNamed()
    {
        // `(compress sig -12dB ratio=4.0)` — positionals first, named tail.
        // RESEARCH § Named-arg syntax: same shape as Python / C# — positional
        // args must precede all named args. NOTE: `buf` is a reserved
        // TokenType.Buf keyword in Flow so the buffer-typed local is named
        // `sig` here to keep the test independent of the buf-keyword surface.
        var program = ParseSource("(compress sig -12dB ratio=4.0)");
        var call = FirstCall(program, "compress");
        Assert.Equal(2, call.Arguments.Count);
        Assert.IsType<VariableExpression>(call.Arguments[0]);
        // The 2nd positional is a Decibel literal — wrapped in LiteralExpression.
        Assert.IsType<LiteralExpression>(call.Arguments[1]);
        Assert.NotNull(call.NamedArgs);
        Assert.Single(call.NamedArgs!);
        Assert.True(call.NamedArgs.ContainsKey("ratio"));
    }

    [Fact]
    public void NamedAfterPositionalAllowed_PositionalAfterNamedRejected()
    {
        // RESEARCH § Named-arg syntax Pattern: once a named arg has been
        // parsed, a subsequent positional raises a clear diagnostic.
        // NOTE: `fn` is the lambda keyword in Flow; use `xform` (plain
        // identifier) so the call form parses through the function-call
        // branch and the inner arg-list loop hits our new diagnostic.
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer("(xform arg1 name=val arg2)", reporter);
        var tokens = lexer.Tokenize();
        Assert.False(reporter.HasErrors, $"Lexer errors: {reporter.FormatErrors()}");
        var parser = new Parser(tokens, reporter);
        parser.Parse();
        Assert.True(reporter.HasErrors, "Expected parser error for positional after named arg");
        Assert.Contains("positional", reporter.FormatErrors(), System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("named", reporter.FormatErrors(), System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NegativeLiteralAfterAssign()
    {
        // Phase 26 D-04 + Phase 36 D-36-11 RESEARCH Open Question 4:
        // TokenType.Assign is already in TryLexSignedNumber's expression-start
        // set, so `arg=-5` lexes the `-5` as a single signed IntLiteral and
        // the parser binds it as the named-arg value. NOTE: `fn` is reserved
        // (lambda keyword) — use `xform` to keep this fact focused on the
        // post-Assign signed-literal lex path, not the keyword-collision.
        var program = ParseSource("(xform arg=-5)");
        var call = FirstCall(program, "xform");
        Assert.Empty(call.Arguments);
        Assert.NotNull(call.NamedArgs);
        Assert.True(call.NamedArgs!.ContainsKey("arg"));
        // The bound expression is a single signed IntLiteral, NOT a
        // `(neg 5)` FunctionCallExpression.
        var lit = Assert.IsType<LiteralExpression>(call.NamedArgs["arg"]);
        Assert.Equal(-5, lit.Value);
    }
}
