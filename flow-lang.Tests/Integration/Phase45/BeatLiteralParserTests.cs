using System;
using System.Linq;
using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using Xunit;

namespace FlowLang.Tests.Integration.Phase45;

/// <summary>
/// Phase 45 Plan 45-01 — lexer-correctness Facts/Theory for the new
/// <c>Nb</c> beat literal surface (D-06 signed + D-07 unsigned + D-08
/// negative-double acceptance).
///
/// <para>
/// Theory grid (Signal 1, 15 cases): 7 positive lex shapes (unsigned/signed,
/// integer/fractional, positive/negative) + 8 identifier-guard / B-prefix
/// negative cases pinning that <c>1bar</c> / <c>1beats</c> / <c>2bpm</c> /
/// <c>b1</c> / <c>Bb</c> / <c>B4</c> / <c>Bmaj7</c> / <c>0.5b D4q</c> all
/// lex via their pre-Phase-45 token routings (no spurious <see cref="TokenType.BeatLiteral"/>
/// consumption).
/// </para>
///
/// <para>
/// RED/GREEN sequencing: Task 1 (this scaffold) leaves the 7 positive lex
/// Facts RED — they fail until Task 2 lands the signed+unsigned lexer
/// branches in <c>SimpleLexer.cs</c>. The 8 identifier-guard Facts PASS
/// GREEN immediately because the lexer is unchanged in Task 1.
/// </para>
///
/// <para>
/// Token.Value carries the parsed double for music-literal tokens
/// (<c>Token.cs:30</c>); we assert on <c>Type</c>, <c>Text</c> (raw source
/// slice), and <c>Value</c> (parsed double) per the planner's contract.
/// </para>
/// </summary>
[Trait("Category", Phase45TestCategory.Phase45)]
[Collection("FlowScripts")]
public class BeatLiteralParserTests : IDisposable
{
    public BeatLiteralParserTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    /// <summary>
    /// Tokenize <paramref name="source"/> with a fresh <see cref="SimpleLexer"/>
    /// and an empty <see cref="PragmaSet"/>. Trims the trailing
    /// <see cref="TokenType.Eof"/> token so test assertions can index by
    /// content position without off-by-one.
    /// </summary>
    private static Token[] Tokenize(string source)
    {
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, reporter, "<test>", PragmaSet.Empty);
        var tokens = lexer.Tokenize();
        // Strip trailing EOF for cleaner assertions.
        if (tokens.Count > 0 && tokens[^1].Type == TokenType.Eof)
            return tokens.Take(tokens.Count - 1).ToArray();
        return tokens.ToArray();
    }

    // ====================================================================
    // SIGNAL 1A — Sanity: TokenType.BeatLiteral enum case is defined
    // ====================================================================

    [Fact]
    public void TokenTypeEnumContainsBeatLiteral()
    {
        // REQ-BEAT-LEX-01 — the enum case must compile-resolve. Use
        // Enum.IsDefined so the test runs even before SimpleLexer emits one.
        Assert.True(Enum.IsDefined(typeof(TokenType), "BeatLiteral"),
            "TokenType.BeatLiteral must be defined per Phase 45 D-06 / REQ-BEAT-LEX-01.");
    }

    // ====================================================================
    // SIGNAL 1B — Theory grid: positive lex shapes
    //   (RED until Task 2 lands the SimpleLexer.cs branches; expected to
    //    fail with an InvalidOperationException or token-type mismatch
    //    in Task 1's intermediate state.)
    // ====================================================================

    /// <summary>
    /// Unsigned fractional beat literal — D-07 path
    /// (ScanNumberOrSpecialLiteral).
    /// </summary>
    [Fact]
    public void LexUnsignedFractional_0_5b_EmitsBeatLiteralWithValue0_5()
    {
        // Bind through an expression-start position so the lexer's
        // music-literal expression-start gate (ERG-05) does not block
        // unsigned forms. Standalone "0.5b" sits at file-start which is
        // an expression-start position.
        var tokens = Tokenize("0.5b");
        Assert.Single(tokens);
        Assert.Equal(TokenType.BeatLiteral, tokens[0].Type);
        Assert.Equal("0.5b", tokens[0].Text);
        Assert.Equal(0.5, (double)tokens[0].Value!);
    }

    /// <summary>Unsigned integer beat literal — D-07 path.</summary>
    [Fact]
    public void LexUnsignedInteger_2b_EmitsBeatLiteralWithValue2()
    {
        var tokens = Tokenize("2b");
        Assert.Single(tokens);
        Assert.Equal(TokenType.BeatLiteral, tokens[0].Type);
        Assert.Equal("2b", tokens[0].Text);
        Assert.Equal(2.0, (double)tokens[0].Value!);
    }

    /// <summary>
    /// Unsigned 1.0 float with explicit decimal — D-07 path. Confirms
    /// trailing zero in the mantissa does not perturb the lexer.
    /// </summary>
    [Fact]
    public void LexUnsignedDecimalZero_1_0b_EmitsBeatLiteralWithValue1()
    {
        var tokens = Tokenize("1.0b");
        Assert.Single(tokens);
        Assert.Equal(TokenType.BeatLiteral, tokens[0].Type);
        Assert.Equal("1.0b", tokens[0].Text);
        Assert.Equal(1.0, (double)tokens[0].Value!);
    }

    /// <summary>
    /// Signed positive beat literal — D-06 path
    /// (TryLookAheadSpecialLiteral). Lexes at expression-start position
    /// after the binding's '=' sign per ERG-05.
    /// </summary>
    [Fact]
    public void LexSignedPositive_PlusOneB_EmitsBeatLiteralWithValue1()
    {
        // Wrap in a binding so '+1b' sits at an expression-start position.
        // The lexer should emit: [Type, Identifier, Assign, BeatLiteral(+1.0)]
        var tokens = Tokenize("Beat b = +1b");
        // Find the BeatLiteral token by type (order: Beat keyword, Identifier 'b',
        // Assign '=', BeatLiteral '+1b').
        var beat = Array.Find(tokens, t => t.Type == TokenType.BeatLiteral);
        Assert.NotNull(beat);
        Assert.Equal("+1b", beat!.Text);
        Assert.Equal(1.0, (double)beat.Value!);
    }

    /// <summary>Signed negative beat literal — D-06 + D-08 acceptance.</summary>
    [Fact]
    public void LexSignedNegative_MinusTwoB_EmitsBeatLiteralWithValueMinus2()
    {
        var tokens = Tokenize("Beat b = -2b");
        var beat = Array.Find(tokens, t => t.Type == TokenType.BeatLiteral);
        Assert.NotNull(beat);
        Assert.Equal("-2b", beat!.Text);
        Assert.Equal(-2.0, (double)beat.Value!);
    }

    /// <summary>Signed positive fractional beat literal.</summary>
    [Fact]
    public void LexSignedFractional_PlusZeroPointFiveB_EmitsBeatLiteralWithValue0_5()
    {
        var tokens = Tokenize("Beat b = +0.5b");
        var beat = Array.Find(tokens, t => t.Type == TokenType.BeatLiteral);
        Assert.NotNull(beat);
        Assert.Equal("+0.5b", beat!.Text);
        Assert.Equal(0.5, (double)beat.Value!);
    }

    /// <summary>
    /// Signed negative fractional beat literal — combines D-06 signed
    /// path with D-08 negative-double acceptance.
    /// </summary>
    [Fact]
    public void LexSignedFractionalNegative_MinusZeroPoint25b_EmitsBeatLiteralWithValueMinus0_25()
    {
        var tokens = Tokenize("Beat b = -0.25b");
        var beat = Array.Find(tokens, t => t.Type == TokenType.BeatLiteral);
        Assert.NotNull(beat);
        Assert.Equal("-0.25b", beat!.Text);
        Assert.Equal(-0.25, (double)beat.Value!);
    }

    // ====================================================================
    // SIGNAL 1C — Identifier-guard Facts (GREEN immediately — depend only
    //   on existing lexer behavior).
    //
    //   The guard `!char.IsLetter(PeekNext())` in the existing `c` / `s`
    //   suffix branches is mirrored verbatim by the Phase 45 `b` branches
    //   (45-PATTERNS.md §Pitfall 1). Tests here PASS in Task 1 because
    //   in the absence of the 'b' branch the lexer naturally falls
    //   through to number+identifier emission.
    // ====================================================================

    /// <summary>
    /// <c>1bar</c> must lex as [IntLiteral(1), Identifier("bar")] —
    /// the 'b' must NOT be consumed by a putative BeatLiteral branch
    /// because the next char 'a' IS a letter (45-RESEARCH §Pitfall 1).
    /// </summary>
    [Fact]
    public void LexNotConsumedByIdentifierBar_1bar_IntPlusIdentifier()
    {
        var tokens = Tokenize("1bar");
        Assert.Equal(2, tokens.Length);
        Assert.Equal(TokenType.IntLiteral, tokens[0].Type);
        Assert.Equal("1", tokens[0].Text);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("bar", tokens[1].Text);
    }

    /// <summary><c>1beats</c> → [IntLiteral(1), Identifier("beats")].</summary>
    [Fact]
    public void LexNotConsumedByIdentifierBeats_1beats_IntPlusIdentifier()
    {
        var tokens = Tokenize("1beats");
        Assert.Equal(2, tokens.Length);
        Assert.Equal(TokenType.IntLiteral, tokens[0].Type);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("beats", tokens[1].Text);
    }

    /// <summary><c>2bpm</c> → [IntLiteral(2), Identifier("bpm")].</summary>
    [Fact]
    public void LexNotConsumedByIdentifierBpm_2bpm_IntPlusIdentifier()
    {
        var tokens = Tokenize("2bpm");
        Assert.Equal(2, tokens.Length);
        Assert.Equal(TokenType.IntLiteral, tokens[0].Type);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("bpm", tokens[1].Text);
    }

    /// <summary>
    /// <c>b1</c> is an identifier — leading char is letter, no digits
    /// precede it. Lex as Identifier("b1").
    /// </summary>
    [Fact]
    public void LexBStartingIdentifier_b1_LexesAsIdentifier()
    {
        var tokens = Tokenize("b1");
        Assert.Single(tokens);
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal("b1", tokens[0].Text);
    }

    /// <summary>
    /// <c>Bb</c> must keep its pre-Phase-45 routing — the leading capital
    /// 'B' is the German-notation Bb pitch root; downstream the parser /
    /// chord recognizer handles this. We assert here only that no
    /// BeatLiteral token is emitted (the surface stays unchanged for
    /// existing B-prefix forms).
    /// </summary>
    [Fact]
    public void LexBbStillFlatNote_NoBeatLiteralEmitted()
    {
        var tokens = Tokenize("Bb");
        Assert.NotEmpty(tokens);
        Assert.DoesNotContain(tokens, t => t.Type == TokenType.BeatLiteral);
    }

    /// <summary>
    /// <c>B4</c> must lex as NoteLiteral (uppercase 'B' + octave digit).
    /// Phase 45 does not touch the music-pitch token boundary at expression-start.
    /// </summary>
    [Fact]
    public void LexB4StillNoteLiteral_NoBeatLiteralEmitted()
    {
        var tokens = Tokenize("B4");
        Assert.NotEmpty(tokens);
        Assert.DoesNotContain(tokens, t => t.Type == TokenType.BeatLiteral);
        Assert.Contains(tokens, t => t.Type == TokenType.NoteLiteral);
    }

    /// <summary>
    /// <c>Bmaj7</c> must lex as ChordLiteral. Phase 45 does not perturb
    /// the chord-recognition path.
    /// </summary>
    [Fact]
    public void LexBmaj7StillChordLiteral_NoBeatLiteralEmitted()
    {
        var tokens = Tokenize("Bmaj7");
        Assert.NotEmpty(tokens);
        Assert.DoesNotContain(tokens, t => t.Type == TokenType.BeatLiteral);
        Assert.Contains(tokens, t => t.Type == TokenType.ChordLiteral);
    }

    /// <summary>
    /// <c>0.5b D4q</c> — Beat literal followed by a note expression. The
    /// BeatLiteral must terminate cleanly at the whitespace, allowing the
    /// following <c>D4</c> to lex via its existing music-literal path
    /// (NoteLiteral at expression-start) and the trailing <c>q</c> to lex
    /// as an Identifier outside note-stream mode. The critical property:
    /// the BeatLiteral does NOT consume <c>b D4q</c> beyond the single
    /// `b` suffix, and the boundary is whitespace-clean.
    /// (Task 2 turns this from RED to GREEN once the unsigned b-branch lands.)
    /// </summary>
    [Fact]
    public void LexFollowedByNoteToken_0_5b_Space_D4q_BeatLiteralFollowedByNoteAndIdent()
    {
        var tokens = Tokenize("0.5b D4q");
        // [BeatLiteral(0.5b), NoteLiteral(D4), Identifier(q)] —
        // D4 lexes as a NoteLiteral via the existing pitch-letter+octave
        // path; q is an identifier outside `| ... |` note-stream mode.
        Assert.Equal(3, tokens.Length);
        Assert.Equal(TokenType.BeatLiteral, tokens[0].Type);
        Assert.Equal("0.5b", tokens[0].Text);
        Assert.Equal(0.5, (double)tokens[0].Value!);
        // The boundary properties we care about: the BeatLiteral terminates
        // cleanly at the whitespace, and the following tokens are NOT
        // BeatLiterals (no spurious second-emission consuming the space-D4q).
        Assert.DoesNotContain(tokens.Skip(1), t => t.Type == TokenType.BeatLiteral);
        Assert.Equal(TokenType.NoteLiteral, tokens[1].Type);
        Assert.Equal("D4", tokens[1].Text);
        Assert.Equal(TokenType.Identifier, tokens[2].Type);
        Assert.Equal("q", tokens[2].Text);
    }

    // ====================================================================
    // SIGNAL 2 — AST-shape Facts (Plan 45-02, Wave 2).
    //   Pin that the Parser emits a dedicated BeatLiteralExpression record
    //   (NOT a flat LiteralExpression) for every BeatLiteral token, with
    //   RawValue carrying the parsed double bit-identically (D-01 / D-09).
    //
    //   These exercise the five composer-facing surfaces enumerated in
    //   45-RESEARCH §Signal 2: variable initializer, function arg, flow-op
    //   chain, arithmetic operand, and tuple element.
    // ====================================================================

    /// <summary>
    /// Parse <paramref name="source"/> with a fresh <see cref="SimpleLexer"/>
    /// + <see cref="Parser"/>, asserting the <see cref="ErrorReporter"/>
    /// stays error-free so AST-shape assertions act on a clean parse.
    /// </summary>
    private static Program Parse(string source)
    {
        var reporter = new ErrorReporter();
        // Rule 3 (blocking): plan snippet used `new PragmaSet()` but PragmaSet has no
        // parameterless ctor (requires Enabled set + declaration sites). PragmaSet.Empty
        // is the no-pragma carrier — these AST-shape Facts exercise the pragma-OFF path
        // (the multiplier is identity at eval time; Wave 4 adds the pragma-ON eval tests).
        var pragmaSet = PragmaSet.Empty;
        var lexer = new SimpleLexer(source, reporter, "<test>", pragmaSet);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, reporter, pragmaSet);
        var program = parser.Parse();
        Assert.False(reporter.HasErrors,
            $"Parse of '{source}' should be error-free; got: " +
            string.Join("; ", reporter.Errors.Select(e => e.Message)));
        return program;
    }

    /// <summary>
    /// Test 1 — <c>Beat b = 0.5b</c> produces a <see cref="VariableDeclaration"/>
    /// whose initializer is a <see cref="BeatLiteralExpression"/> with
    /// <c>RawValue == 0.5</c> (D-09 dedicated-record routing).
    /// </summary>
    [Fact]
    public void AstShapeAssignedToVariable()
    {
        var program = Parse("Beat b = 0.5b");
        var decl = Assert.IsType<VariableDeclaration>(program.Statements[0]);
        var beatLit = Assert.IsType<BeatLiteralExpression>(decl.Value);
        Assert.Equal(0.5, beatLit.RawValue);
    }

    /// <summary>
    /// Test 2 — <c>(delay sig 0.5b 0.5 0.4)</c> parses; the <c>delay</c>
    /// call's <c>Arguments[1]</c> is a <see cref="BeatLiteralExpression"/>
    /// (Nb survives as a function arg at a non-leading position).
    /// (Uses identifier <c>sig</c> rather than <c>buf</c> — the latter is the
    /// reserved <c>Buf</c> type keyword and would not lex as an identifier arg.)
    /// </summary>
    [Fact]
    public void AstShapeAsFunctionArg()
    {
        var program = Parse("(delay sig 0.5b 0.5 0.4)");
        var stmt = Assert.IsType<ExpressionStatement>(program.Statements[0]);
        var call = Assert.IsType<FunctionCallExpression>(stmt.Expression);
        Assert.Equal("delay", call.Name);
        var beatLit = Assert.IsType<BeatLiteralExpression>(call.Arguments[1]);
        Assert.Equal(0.5, beatLit.RawValue);
    }

    /// <summary>
    /// Test 3 — <c>0.5b -&gt; (delay sig 0.5 0.4)</c> parses; the parse-time
    /// <c>-&gt;</c> transform threads the Beat literal in as
    /// <c>Arguments[0]</c> of the <c>delay</c> call (REQ-BEAT-AST-03 — Nb at
    /// expression-start / flow-chain head).
    /// </summary>
    [Fact]
    public void AstShapeViaFlowOperator()
    {
        var program = Parse("0.5b -> (delay sig 0.5 0.4)");
        var stmt = Assert.IsType<ExpressionStatement>(program.Statements[0]);
        var call = Assert.IsType<FunctionCallExpression>(stmt.Expression);
        Assert.Equal("delay", call.Name);
        var beatLit = Assert.IsType<BeatLiteralExpression>(call.Arguments[0]);
        Assert.Equal(0.5, beatLit.RawValue);
    }

    /// <summary>
    /// Test 4 — <c>(add 0.5b 0.5b)</c> parses with both operands as
    /// <see cref="BeatLiteralExpression"/> (Nb as an arithmetic operand).
    /// </summary>
    [Fact]
    public void AstShapeAsArithmeticOperand()
    {
        var program = Parse("(add 0.5b 0.5b)");
        var stmt = Assert.IsType<ExpressionStatement>(program.Statements[0]);
        var call = Assert.IsType<FunctionCallExpression>(stmt.Expression);
        Assert.Equal("add", call.Name);
        var lhs = Assert.IsType<BeatLiteralExpression>(call.Arguments[0]);
        var rhs = Assert.IsType<BeatLiteralExpression>(call.Arguments[1]);
        Assert.Equal(0.5, lhs.RawValue);
        Assert.Equal(0.5, rhs.RawValue);
    }

    /// <summary>
    /// Test 5 — <c>&lt;&lt;C4, 0.5b&gt;&gt;</c> parses;
    /// <c>TupleLiteralExpression.Elements[1]</c> is a
    /// <see cref="BeatLiteralExpression"/> (Phase 26.1 DICT-01 tuple reuse).
    /// Bound through the canonical <c>Tuple&lt;&lt;...&gt;&gt; name = &lt;&lt;...&gt;&gt;</c>
    /// form (mirrors <c>tests/test_tuple_literal.flow</c>) so the initializer
    /// <c>&lt;&lt;</c> is parsed as a tuple-literal expression rather than a
    /// statement-start destructure (<c>&lt;&lt;a, b&gt;&gt; = expr</c>) target.
    /// </summary>
    [Fact]
    public void AstShapeInTuple()
    {
        var program = Parse("Tuple<<Note, Beat>> entry = <<C4, 0.5b>>");
        var decl = Assert.IsType<VariableDeclaration>(program.Statements[0]);
        var tuple = Assert.IsType<TupleLiteralExpression>(decl.Value);
        var beatLit = Assert.IsType<BeatLiteralExpression>(tuple.Elements[1]);
        Assert.Equal(0.5, beatLit.RawValue);
    }
}
