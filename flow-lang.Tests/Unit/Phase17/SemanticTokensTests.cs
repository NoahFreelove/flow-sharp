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
        // "xyz" lexes to Identifier + Eof. Both unmapped → empty int[].
        // (Critical: MUST be empty, not [0,0,3,0,0] — zeros would corrupt delta math
        // for any following mapped tokens in more complex encodings.)
        var tokens = Tokens("xyz");
        var encoded = SemanticTokensEncoder.EncodeTokens(tokens);
        Assert.Empty(encoded);
    }

    [Fact]
    public void EncodeTokens_SkipBetweenMapped_PreservesDeltaMath()
    {
        // "proc xyz return" — Proc (mapped), Identifier xyz (skipped), Return (mapped).
        // The skipped identifier must NOT shift the delta origin. Return's delta
        // should be measured from Proc, not from xyz.
        var tokens = Tokens("proc xyz return").Where(t => t.Type != TokenType.Eof).ToList();
        var procTok = tokens.First(t => t.Type == TokenType.Proc);
        var retTok = tokens.First(t => t.Type == TokenType.Return);
        var encoded = SemanticTokensEncoder.EncodeTokens(tokens);
        Assert.Equal(10, encoded.Length); // only Proc + Return encoded (xyz skipped)
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
}
