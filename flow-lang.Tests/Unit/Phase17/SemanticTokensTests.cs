using System.Linq;
using FlowLang.Lexing;
using FlowLsp.Semantic;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 Plan 04 — SemanticTokensEncoder Facts.
///
/// Exercises the pure static encoder (no OmniSharp Document types) that maps
/// SimpleLexer TokenType → LSP SemanticTokenType legend index and emits the
/// 5-tuple delta-encoded int[] per LSP 3.17.
///
/// Critical invariants pinned here:
/// - Eof and Identifier (not in legend) are SKIPPED, not emitted as zeros.
///   Emitting zeros would corrupt subsequent tokens' delta math.
/// - Same-line delta uses column offset (currCol - prevCol).
/// - Cross-line delta uses absolute column (currCol, since prevCol resets).
/// - Both line and column are 0-based in LSP (SourceLocation is 1-based).
/// </summary>
public class SemanticTokensTests
{
    private static IReadOnlyList<Token> Tokens(string source) =>
        LspFixtures.Parse(source).Tokens;

    [Fact]
    public void EncodeTokens_EmptyBuffer_ReturnsEmpty()
    {
        // Only Eof token, which maps to null → skipped.
        var encoded = SemanticTokensEncoder.EncodeTokens(Tokens(""));
        Assert.Empty(encoded);
    }

    [Fact]
    public void EncodeTokens_SingleKeyword_Emits5Tuple()
    {
        var tokens = Tokens("proc");
        var proc = tokens.First(t => t.Type == TokenType.Proc);
        var encoded = SemanticTokensEncoder.EncodeTokens(new[] { proc });
        Assert.Equal(5, encoded.Length);
        // First token absolute: deltaLine = 0 (line 1 → 0-based 0 = prevLine 0), deltaStartChar = column - 1 = 0
        Assert.Equal(0, encoded[0]);
        Assert.Equal(proc.Location.Column - 1, encoded[1]);
        Assert.Equal(4, encoded[2]); // "proc".Length
        var keywordIdx = System.Array.IndexOf(SemanticTokensEncoder.Legend, SemanticTokenType.Keyword);
        Assert.Equal(keywordIdx, encoded[3]);
        Assert.Equal(0, encoded[4]); // no modifiers
    }

    [Fact]
    public void EncodeTokens_SameLineDelta_UsesColumnOffset()
    {
        // Two keywords on the same line: "proc return" — both are mapped tokens.
        var tokens = Tokens("proc return").Where(t => t.Type != TokenType.Eof).ToList();
        Assert.True(tokens.Count >= 2);
        var procTok = tokens.First(t => t.Type == TokenType.Proc);
        var retTok = tokens.First(t => t.Type == TokenType.Return);
        var encoded = SemanticTokensEncoder.EncodeTokens(new[] { procTok, retTok });
        Assert.Equal(10, encoded.Length); // 2 tokens × 5 ints
        // Second token same line → dLine=0, dCol = retCol - procCol (relative to prior token).
        Assert.Equal(0, encoded[5]);
        Assert.Equal(retTok.Location.Column - procTok.Location.Column, encoded[6]);
        Assert.Equal(6, encoded[7]); // "return".Length
    }

    [Fact]
    public void EncodeTokens_CrossLineDelta_UsesAbsoluteColumn()
    {
        var tokens = Tokens("proc\n  return").Where(t => t.Type != TokenType.Eof).ToList();
        var procTok = tokens.First(t => t.Type == TokenType.Proc);
        var retTok = tokens.First(t => t.Type == TokenType.Return);
        var encoded = SemanticTokensEncoder.EncodeTokens(new[] { procTok, retTok });
        // dLine = 1 (line 2 - line 1 in 0-based), dCol = absolute column of return (Location.Column - 1)
        Assert.Equal(1, encoded[5]);
        Assert.Equal(retTok.Location.Column - 1, encoded[6]);
    }

    [Fact]
    public void EncodeTokens_SkipsUnmappedTokens()
    {
        // "()" lexes to LParen + RParen + Eof. All three are unmapped → empty int[].
        // (Critical: MUST be empty, not [0,0,1,0,0] — zeros would corrupt delta math
        // for any following mapped tokens in more complex encodings.)
        // Phase 31 Plan 31-10: Identifier is now contextually mapped to Function/
        // Variable, so we use a token that genuinely has no classification (LParen)
        // to keep this test's intent (unmapped-token skip behavior) intact.
        var tokens = Tokens("()");
        var encoded = SemanticTokensEncoder.EncodeTokens(tokens);
        Assert.Empty(encoded);
    }

    [Fact]
    public void EncodeTokens_SkipBetweenMapped_PreservesDeltaMath()
    {
        // "proc () return" — Proc (mapped), LParen/RParen (skipped), Return (mapped).
        // The skipped delimiters must NOT shift the delta origin. Return's delta
        // should be measured from Proc, not from RParen.
        // Phase 31 Plan 31-10: previously this test used Identifier "xyz" between
        // the two keywords. Identifier is now contextually classified (bare ID →
        // Variable), so we switch to delimiters that truly have no semantic-tokens
        // scope.
        var tokens = Tokens("proc () return").Where(t => t.Type != TokenType.Eof).ToList();
        var procTok = tokens.First(t => t.Type == TokenType.Proc);
        var retTok = tokens.First(t => t.Type == TokenType.Return);
        var encoded = SemanticTokensEncoder.EncodeTokens(tokens);
        Assert.Equal(10, encoded.Length); // only Proc + Return encoded (parens skipped)
        // Second emitted tuple is Return, deltas relative to Proc.
        Assert.Equal(0, encoded[5]);
        Assert.Equal(retTok.Location.Column - procTok.Location.Column, encoded[6]);
    }

    [Fact]
    public void MapTokenType_Keyword_HasIndex() =>
        Assert.NotNull(SemanticTokensEncoder.MapTokenType(TokenType.Proc));

    [Fact]
    public void MapTokenType_NoteLiteral_MapsToVariable()
    {
        var idx = SemanticTokensEncoder.MapTokenType(TokenType.NoteLiteral);
        Assert.NotNull(idx);
        Assert.Equal(SemanticTokenType.Variable, SemanticTokensEncoder.Legend[idx.Value]);
    }

    [Fact]
    public void MapTokenType_ChordLiteral_MapsToFunction()
    {
        var idx = SemanticTokensEncoder.MapTokenType(TokenType.ChordLiteral);
        Assert.NotNull(idx);
        Assert.Equal(SemanticTokenType.Function, SemanticTokensEncoder.Legend[idx.Value]);
    }

    [Fact]
    public void MapTokenType_Eof_ReturnsNull() =>
        Assert.Null(SemanticTokensEncoder.MapTokenType(TokenType.Eof));

    [Fact]
    public void MapTokenType_Identifier_ReturnsNull() =>
        Assert.Null(SemanticTokensEncoder.MapTokenType(TokenType.Identifier));

    [Fact]
    public void MapTokenType_PipeDelimiter_MapsToMacro()
    {
        // | pipe delimiters — no standard "delimiter" scope. Use Macro (closest standard).
        var idx = SemanticTokensEncoder.MapTokenType(TokenType.Pipe);
        Assert.NotNull(idx);
        Assert.Equal(SemanticTokenType.Macro, SemanticTokensEncoder.Legend[idx.Value]);
    }

    [Fact]
    public void MapTokenType_StructuralArrows_MapToMacro()
    {
        // Phase 31 Plan 31-08 UAT follow-up: -> (Arrow), => (FatArrow), and
        // ~> (TildeArrow) are control-flow / call-composition symbols rather
        // than arithmetic. They map to Macro alongside the | pipe so editor
        // themes paint them with the same structural prominence the composer
        // expects.
        foreach (var t in new[] { TokenType.Arrow, TokenType.FatArrow, TokenType.TildeArrow })
        {
            var idx = SemanticTokensEncoder.MapTokenType(t);
            Assert.NotNull(idx);
            Assert.Equal(SemanticTokenType.Macro, SemanticTokensEncoder.Legend[idx.Value]);
        }
    }

    [Fact]
    public void MapTokenType_ArithmeticOperators_StillMapToOperator()
    {
        // Regression guard: bumping the structural arrows out of Operator scope
        // must NOT also bump the arithmetic / comparison / assignment operators
        // — those stay where they are.
        foreach (var t in new[] {
            TokenType.Plus, TokenType.Minus, TokenType.Star, TokenType.Slash,
            TokenType.LessThan, TokenType.GreaterThan, TokenType.Assign })
        {
            var idx = SemanticTokensEncoder.MapTokenType(t);
            Assert.NotNull(idx);
            Assert.Equal(SemanticTokenType.Operator, SemanticTokensEncoder.Legend[idx.Value]);
        }
    }

    [Fact]
    public void MapTokenType_MusicContextKeywords_MapToKeyword()
    {
        // D-05: no invented scopes — Tempo/Timesig/Key/Swing/etc. fold into Keyword.
        foreach (var t in new[] {
            TokenType.Tempo, TokenType.Timesig, TokenType.Key, TokenType.Swing,
            TokenType.Dynamics, TokenType.Rit, TokenType.Accel, TokenType.Pickup,
            TokenType.Section })
        {
            var idx = SemanticTokensEncoder.MapTokenType(t);
            Assert.NotNull(idx);
            Assert.Equal(SemanticTokenType.Keyword, SemanticTokensEncoder.Legend[idx.Value]);
        }
    }

    [Fact]
    public void MapTokenType_TypeKeywords_MapToType()
    {
        foreach (var t in new[] {
            TokenType.Void, TokenType.Int, TokenType.Float, TokenType.Long,
            TokenType.Double, TokenType.String, TokenType.Bool, TokenType.Number,
            TokenType.Note, TokenType.Buf })
        {
            var idx = SemanticTokensEncoder.MapTokenType(t);
            Assert.NotNull(idx);
            Assert.Equal(SemanticTokenType.Type, SemanticTokensEncoder.Legend[idx.Value]);
        }
    }

    // ---------- Golden-file Theory: representative Flow snippets pinning encoded int[]. ----------

    public static IEnumerable<object[]> GoldenFixtures() => new[]
    {
        // Fixture 1: Single keyword. One 5-tuple.
        new object[]
        {
            "proc",
            new[] { 0, 0, 4, /*Keyword*/0, 0 }
        },
        // Fixture 2: Keyword + type on same line. "proc Int"
        //   proc @ L1C1 → (0, 0, 4, 0, 0)
        //   Int  @ L1C6 → same-line, dCol = 6-1 = 5; length 3; type index 1
        new object[]
        {
            "proc Int",
            new[] {
                0, 0, 4, 0, 0,   // proc → Keyword
                0, 5, 3, 1, 0    // Int  → Type, dCol=5 from prior col
            }
        },
        // Fixture 3: Two keywords across two lines. "proc\nreturn"
        //   proc   @ L1C1 → (0, 0, 4, 0, 0)
        //   return @ L2C1 → cross-line, dLine=1, dCol=absolute=0, length 6
        new object[]
        {
            "proc\nreturn",
            new[] {
                0, 0, 4, 0, 0,   // proc
                1, 0, 6, 0, 0    // return
            }
        },
    };

    [Theory]
    [MemberData(nameof(GoldenFixtures))]
    public void EncodeTokens_GoldenFixtures_Match(string source, int[] expected)
    {
        var tokens = Tokens(source);
        var encoded = SemanticTokensEncoder.EncodeTokens(tokens);
        Assert.Equal(expected, encoded);
    }

    // ---------- Phase 31 Plan 31-10: contextual Identifier classification. ----------

    [Fact]
    public void ClassifyTokens_IdentifierAfterLParen_IsFunction()
    {
        // "(print)" — LParen, Identifier "print", RParen, Eof.
        // The Identifier is the head of an S-expression call → Function.
        var tokens = Tokens("(print)");
        var classifications = SemanticTokensEncoder.ClassifyTokens(tokens);
        var printIdx = tokens.Select((t, i) => (t, i))
            .First(p => p.t.Type == TokenType.Identifier && p.t.Text == "print").i;
        var functionLegendIdx = System.Array.IndexOf(SemanticTokensEncoder.Legend, SemanticTokenType.Function);
        Assert.Equal(functionLegendIdx, classifications[printIdx]);
    }

    [Fact]
    public void ClassifyTokens_BareIdentifier_IsVariable()
    {
        // "x" — Identifier preceded by no token (i==0) → Variable, not Function.
        var tokens = Tokens("x");
        var classifications = SemanticTokensEncoder.ClassifyTokens(tokens);
        var xIdx = tokens.Select((t, i) => (t, i))
            .First(p => p.t.Type == TokenType.Identifier && p.t.Text == "x").i;
        var variableLegendIdx = System.Array.IndexOf(SemanticTokensEncoder.Legend, SemanticTokenType.Variable);
        Assert.Equal(variableLegendIdx, classifications[xIdx]);
    }

    [Fact]
    public void ClassifyTokens_IdentifierAfterProc_IsFunction()
    {
        // "proc demo" — Identifier "demo" follows Proc → Function (proc decl name).
        var tokens = Tokens("proc demo");
        var classifications = SemanticTokensEncoder.ClassifyTokens(tokens);
        var demoIdx = tokens.Select((t, i) => (t, i))
            .First(p => p.t.Type == TokenType.Identifier && p.t.Text == "demo").i;
        var functionLegendIdx = System.Array.IndexOf(SemanticTokensEncoder.Legend, SemanticTokenType.Function);
        Assert.Equal(functionLegendIdx, classifications[demoIdx]);
    }

    [Fact]
    public void ClassifyTokens_KnownTypeIdentifier_IsType()
    {
        // "Beat x = 1.5" — Beat is an Identifier (no dedicated lexer keyword) but
        // is in KnownTypeIdentifiers, so it classifies as Type, not Variable.
        var tokens = Tokens("Beat x = 1.5");
        var classifications = SemanticTokensEncoder.ClassifyTokens(tokens);
        var beatIdx = tokens.Select((t, i) => (t, i))
            .First(p => p.t.Type == TokenType.Identifier && p.t.Text == "Beat").i;
        var typeLegendIdx = System.Array.IndexOf(SemanticTokensEncoder.Legend, SemanticTokenType.Type);
        Assert.Equal(typeLegendIdx, classifications[beatIdx]);
    }

    [Fact]
    public void ClassifyTokens_KnownTypeIdentifier_BeatsContextRule()
    {
        // "(Beat x)" — Beat would normally be Function-by-context (Identifier after
        // LParen). But the KnownTypeIdentifiers check wins: Beat is always Type.
        var tokens = Tokens("(Beat x)");
        var classifications = SemanticTokensEncoder.ClassifyTokens(tokens);
        var beatIdx = tokens.Select((t, i) => (t, i))
            .First(p => p.t.Type == TokenType.Identifier && p.t.Text == "Beat").i;
        var typeLegendIdx = System.Array.IndexOf(SemanticTokensEncoder.Legend, SemanticTokenType.Type);
        Assert.Equal(typeLegendIdx, classifications[beatIdx]);
    }

    [Fact]
    public void ScanCommentTokens_LineCommentMidLine_EmitsCommentToken()
    {
        // "x = 5 // comment text" — // is found at column 7 (1-based).
        var scanned = SemanticTokensEncoder.ScanCommentTokens("x = 5 // comment text\n");
        Assert.Single(scanned);
        Assert.Equal(TokenType.Comment, scanned[0].Type);
        Assert.Equal("// comment text", scanned[0].Text);
        Assert.Equal(1, scanned[0].Location.Line);
        Assert.Equal(7, scanned[0].Location.Column);
    }

    [Fact]
    public void ScanCommentTokens_LineStartLeadIns_AllRecognized()
    {
        // 4 line-start lead-ins per D-11 + Phase 31 SPEC-4. Each line should
        // produce one Comment token spanning from the lead-in to end-of-line.
        var src = "Note: chapter 1\nTODO: refactor\nFIXME: bug\n  ; lisp comment\n";
        var scanned = SemanticTokensEncoder.ScanCommentTokens(src);
        Assert.Equal(4, scanned.Count);
        Assert.Equal("Note: chapter 1", scanned[0].Text);
        Assert.Equal("TODO: refactor", scanned[1].Text);
        Assert.Equal("FIXME: bug", scanned[2].Text);
        Assert.Equal("; lisp comment", scanned[3].Text);
        // The `;` lead-in starts at column 3 (after 2 leading spaces).
        Assert.Equal(3, scanned[3].Location.Column);
    }

    [Fact]
    public void ScanCommentTokens_SlashSlashInsideString_NotMistakenForComment()
    {
        // `(print "// not a comment")` — the // inside the string literal must NOT
        // be recognized as a line comment; only true line comments outside strings
        // count.
        var scanned = SemanticTokensEncoder.ScanCommentTokens("(print \"// not a comment\")\n");
        Assert.Empty(scanned);
    }

    [Fact]
    public void EncodeTokens_FunctionCall_EmitsFunctionScope()
    {
        // "(print x)" — encoded tokens:
        //   (         L1C1 → unmapped, skipped
        //   print     L1C2 → Function (after LParen)
        //   x         L1C8 → Variable (after Identifier, not LParen/Proc)
        //   )         L1C9 → unmapped, skipped
        // Expect 2 5-tuples; first is Function, second is Variable.
        var tokens = Tokens("(print x)");
        var encoded = SemanticTokensEncoder.EncodeTokens(tokens);
        Assert.Equal(10, encoded.Length); // 2 mapped tokens × 5 ints
        var functionLegendIdx = System.Array.IndexOf(SemanticTokensEncoder.Legend, SemanticTokenType.Function);
        var variableLegendIdx = System.Array.IndexOf(SemanticTokensEncoder.Legend, SemanticTokenType.Variable);
        Assert.Equal(functionLegendIdx, encoded[3]);  // print → Function
        Assert.Equal(variableLegendIdx, encoded[8]);  // x → Variable
    }
}
