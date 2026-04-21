using FlowLang.Lexing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace FlowLsp.Semantic;

/// <summary>
/// Pure (transport-free) mapping from Flow <see cref="TokenType"/> to LSP
/// <see cref="SemanticTokenType"/> legend indices, plus 5-tuple delta encoding
/// per LSP 3.17 spec.
///
/// Intentionally isolates the encoding logic from OmniSharp handler types so
/// xUnit tests can pin the <c>int[]</c> output without booting a
/// <c>SemanticTokensBuilder</c> or <c>ILanguageServerFacade</c>.
///
/// Design notes:
/// - D-04 hybrid: this encoder produces semantic tokens that layer ON TOP of
///   the TextMate grammar shipped in plan 17-02. The TM grammar paints first
///   (zero-latency, regex-based); this encoder refines with lexer-precise
///   classification once the server emits <c>semanticTokens/full</c>.
/// - D-05 standard-scopes-only: every <see cref="Legend"/> entry is a stock
///   <see cref="SemanticTokenType"/> property (Keyword, Type, String, Number,
///   Operator, Comment, Variable, Function, Macro). No Flow-specific scopes.
/// - Port of <c>flow-editor/Editor/FlowSyntaxHighlighter.cs</c>:95-146 switch,
///   but returning a legend index instead of an <c>IBrush</c>. Category
///   assignment is deliberately identical so flow-editor and flow-lsp agree
///   on what "kind" each token is.
/// </summary>
public static class SemanticTokensEncoder
{
    /// <summary>
    /// Legend exposed to the LSP client via <c>SemanticTokensRegistrationOptions.Legend</c>.
    /// The client decodes each 5-tuple's <c>tokenType</c> field as an index into this array.
    /// MUST remain in the exact order that <see cref="LegendIndex"/> declares.
    /// </summary>
    public static readonly SemanticTokenType[] Legend =
    {
        SemanticTokenType.Keyword,    // 0 — proc/use/return/fn/tempo/key/section/for/while/...
        SemanticTokenType.Type,       // 1 — Int/Float/Bool/Note/Buf/...
        SemanticTokenType.String,     // 2 — StringLiteral, InterpolatedStringText
        SemanticTokenType.Number,     // 3 — Int/Float/Semitone/Cent/Time/Decibel/Bool literals
        SemanticTokenType.Operator,   // 4 — -> => + - * / < > =
        SemanticTokenType.Comment,    // 5
        SemanticTokenType.Variable,   // 6 — NoteLiteral (musical notes colored as identifiers/symbols)
        SemanticTokenType.Function,   // 7 — ChordLiteral (chord names render as callable-like)
        SemanticTokenType.Macro,      // 8 — | pipe delimiters (no standard delimiter scope)
    };

    /// <summary>
    /// Modifier legend — empty for v1. Hover/declaration/readonly decoration is
    /// a future refinement (CONTEXT §Claude's Discretion).
    /// </summary>
    public static readonly SemanticTokenModifier[] ModifierLegend =
        System.Array.Empty<SemanticTokenModifier>();

    /// <summary>
    /// Parallel indices into <see cref="Legend"/>. Using an internal enum keeps
    /// the mapping readable without making the numeric values public API.
    /// </summary>
    private enum LegendIndex
    {
        Keyword = 0,
        Type = 1,
        String = 2,
        Number = 3,
        Operator = 4,
        Comment = 5,
        Variable = 6,
        Function = 7,
        Macro = 8,
    }

    /// <summary>
    /// Map a Flow <see cref="TokenType"/> to its legend index, or <c>null</c> if
    /// the token has no semantic-tokens classification (Identifier, delimiters,
    /// Eof, etc.). Callers MUST skip unmapped tokens — emitting zero-indexed
    /// placeholders would corrupt the delta encoding for subsequent tokens.
    /// </summary>
    public static int? MapTokenType(TokenType t) => t switch
    {
        // --- Keywords (general + music-context + flow-control) ---
        TokenType.Proc or TokenType.EndProc or TokenType.Return or
        TokenType.Use or TokenType.Internal or TokenType.Lazy or TokenType.Fn or
        TokenType.Section or
        TokenType.For or TokenType.While or TokenType.Break or
        TokenType.Continue or TokenType.In or TokenType.Progression or
        TokenType.Tempo or TokenType.Timesig or TokenType.Key or TokenType.Swing or
        TokenType.Dynamics or TokenType.Rit or TokenType.Accel or TokenType.Pickup or
        TokenType.Pan or TokenType.Gain
            => (int)LegendIndex.Keyword,

        // --- Type keywords ---
        TokenType.Void or TokenType.Int or TokenType.Float or TokenType.Long or
        TokenType.Double or TokenType.String or TokenType.Bool or TokenType.Number or
        TokenType.Note or TokenType.Buf
            => (int)LegendIndex.Type,

        // --- String literals ---
        TokenType.StringLiteral or TokenType.InterpolatedStringText
            => (int)LegendIndex.String,

        // --- Numeric + musical-unit literals (all render as Number in stock themes) ---
        TokenType.IntLiteral or TokenType.FloatLiteral or
        TokenType.SemitoneLiteral or TokenType.CentLiteral or
        TokenType.TimeLiteral or TokenType.DecibelLiteral or
        TokenType.BoolLiteral
            => (int)LegendIndex.Number,

        // --- Operators ---
        TokenType.Arrow or TokenType.FatArrow or TokenType.Plus or TokenType.Minus or
        TokenType.Star or TokenType.Slash or TokenType.LessThan or
        TokenType.GreaterThan or TokenType.Assign
            => (int)LegendIndex.Operator,

        // --- Comments ---
        TokenType.Comment => (int)LegendIndex.Comment,

        // --- Music literals (lexer-precise — the TM grammar cannot reliably
        //     disambiguate these from identifiers; that is the whole point of D-04) ---
        TokenType.NoteLiteral => (int)LegendIndex.Variable,
        TokenType.ChordLiteral => (int)LegendIndex.Function,

        // --- Pipe delimiters for note streams ---
        TokenType.Pipe => (int)LegendIndex.Macro,

        // Everything else (Identifier, LParen/RParen/LBracket/…, Dot, At, Colon,
        // Comma, Semicolon, Ellipsis, Underscore, Tilde, Eof, interpolated
        // start/end delimiters) has no semantic-tokens scope. Skip.
        _ => null,
    };

    /// <summary>
    /// Encode a sorted token list into LSP's 5-tuple delta format:
    /// <c>[deltaLine, deltaStartChar, length, tokenType, tokenModifiers]</c>.
    ///
    /// Invariants:
    /// - Tokens must already be in source order (line, column ascending).
    ///   <see cref="SimpleLexer.Tokenize"/> produces them in that order.
    /// - Same-line delta uses <c>currCol - prevCol</c> (relative).
    /// - Cross-line delta uses <c>currCol</c> directly (absolute — prevCol resets).
    /// - Both line and column are 0-based in the output (LSP spec); Flow's
    ///   <see cref="Core.SourceLocation"/> is 1-based, so subtract 1 and clamp to 0.
    /// - Tokens whose <see cref="MapTokenType"/> returns null are SKIPPED, not
    ///   emitted as zeros. Skipping preserves the delta origin for the next
    ///   mapped token (see <c>EncodeTokens_SkipBetweenMapped_PreservesDeltaMath</c>).
    /// </summary>
    public static int[] EncodeTokens(IReadOnlyList<Token> tokens)
    {
        var data = new List<int>(tokens.Count * 5);
        int prevLine = 0;
        int prevCol = 0;
        bool first = true;
        foreach (var t in tokens)
        {
            var typeIdx = MapTokenType(t.Type);
            if (typeIdx is null) continue;

            int line = System.Math.Max(0, t.Location.Line - 1);
            int col = System.Math.Max(0, t.Location.Column - 1);

            int dLine = first ? line : line - prevLine;
            int dCol = (first || dLine > 0) ? col : col - prevCol;

            data.Add(dLine);
            data.Add(dCol);
            data.Add(t.Text?.Length ?? 0);
            data.Add(typeIdx.Value);
            data.Add(0); // no modifiers (v1)

            prevLine = line;
            prevCol = col;
            first = false;
        }
        return data.ToArray();
    }
}
