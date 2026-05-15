using FlowLang.Core;
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
        SemanticTokenType.Macro,      // 8 — | pipe delimiters AND flow arrows (->, =>, ~>);
                                      //     no standard "structural delimiter" scope, so we
                                      //     pile structural / call-composition symbols here
                                      //     so editors that paint Macro distinctly (most
                                      //     JetBrains + VSCode themes do) give them visual
                                      //     prominence comparable to keywords (Phase 31 Plan
                                      //     31-08 UAT followup — Operator scope is too muted)
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
    /// Pure per-TokenType mapping. Returns <c>null</c> for tokens whose
    /// classification depends on context — notably <see cref="TokenType.Identifier"/>,
    /// which <see cref="ClassifyTokens"/> upgrades to Function or Variable based
    /// on the preceding token. Callers that want context-aware classification
    /// MUST use <see cref="ClassifyTokens"/> instead.
    ///
    /// Kept as a stable pure function for the per-type unit tests; not used by
    /// the LSP handler or by <see cref="EncodeTokens"/> directly any more
    /// (Phase 31 Plan 31-10 contextual upgrade).
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

        // --- Arithmetic / comparison / assignment operators ---
        TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Slash or
        TokenType.LessThan or TokenType.GreaterThan or TokenType.Assign
            => (int)LegendIndex.Operator,

        // --- Structural / flow operators ---
        // Arrow `->` (flow op), FatArrow `=>` (lambda separator), and
        // TildeArrow `~>` (Phase 26.1 tuple-unpack flow op) are control-flow
        // / call-composition symbols, not arithmetic. Map to Macro so they
        // pair visually with the `|` pipe delimiters (note streams) — the
        // composer's eye reads them as structural in the same way.
        TokenType.Arrow or TokenType.FatArrow or TokenType.TildeArrow
            => (int)LegendIndex.Macro,

        // --- Comments ---
        TokenType.Comment => (int)LegendIndex.Comment,

        // --- Music literals (lexer-precise — the TM grammar cannot reliably
        //     disambiguate these from identifiers; that is the whole point of D-04) ---
        TokenType.NoteLiteral => (int)LegendIndex.Variable,
        TokenType.ChordLiteral => (int)LegendIndex.Function,

        // --- Pipe delimiters for note streams (paired with flow arrows above) ---
        TokenType.Pipe => (int)LegendIndex.Macro,

        // Everything else (Identifier, LParen/RParen/LBracket/…, Dot, At, Colon,
        // Comma, Semicolon, Ellipsis, Underscore, Tilde, Eof, interpolated
        // start/end delimiters) has no per-TokenType classification. Identifier
        // is upgraded to Function/Variable contextually by ClassifyTokens.
        _ => null,
    };

    /// <summary>
    /// Identifier names that should be classified as <see cref="LegendIndex.Type"/>
    /// even though the lexer emits them as <see cref="TokenType.Identifier"/>.
    ///
    /// Flow's primitive types (Int, Float, String, Bool, Note, Buf, etc.) have
    /// dedicated lexer tokens and are handled by <see cref="MapTokenType"/>. The
    /// music special types and Phase 26.1 generic-container types are NOT first-
    /// class lexer keywords — they're regular identifiers everywhere they appear,
    /// including in type-annotation position (`Beat x = 1.5`). This set restores
    /// them to Type scope in semantic tokens.
    ///
    /// Source of truth: <c>flow-lang/TypeSystem/SpecialTypes/</c> + the Music
    /// Types Quick Reference table in CLAUDE.md.
    /// </summary>
    private static readonly HashSet<string> KnownTypeIdentifiers = new(StringComparer.Ordinal)
    {
        // Music special types
        "Semitone", "Cent", "Millisecond", "Second", "Decibel", "Beat", "Hertz",
        "Bar", "TimeSignature", "NoteValue", "Sequence", "MusicalNote", "Chord",
        "Section", "Song",
        // Phase 26.1 — symbols/tuples/dicts
        "Symbol", "Tuple", "Dict",
        // Synthesis/audio runtime types
        "Buffer", "Lazy", "Function", "Envelope", "OscillatorState", "Voice", "Track",
    };

    /// <summary>
    /// Context-aware classifier — like <see cref="MapTokenType"/> but with
    /// Identifier upgrades based on identifier text + preceding token:
    /// <list type="bullet">
    /// <item>Identifier whose text is a <see cref="KnownTypeIdentifiers"/> entry → Type</item>
    /// <item>Identifier after <c>LParen</c> → Function (S-expression call head)</item>
    /// <item>Identifier after <c>Proc</c> → Function (proc declaration name)</item>
    /// <item>Identifier otherwise → Variable</item>
    /// </list>
    ///
    /// Returns an array parallel to <paramref name="tokens"/>; null entries mean
    /// "skip — no classification" (same contract as <see cref="MapTokenType"/>).
    /// Used by <see cref="EncodeTokens"/> and by SemanticTokensHandler.Tokenize.
    ///
    /// Phase 31 Plan 31-10: previously bare identifiers were unmapped (null), so
    /// composers saw uncolored function calls in editors that relied only on
    /// semantic tokens (notably LSP4IJ-based JetBrains plugins where there is
    /// no TextMate-grammar baseline). This classifier closes that gap WITHOUT
    /// requiring a full IntelliJ Language class.
    ///
    /// Args/parameters note: function-call arguments and proc parameters are
    /// classified as Variable (same scope as variable reads). The LSP
    /// <c>SemanticTokenType.Parameter</c> scope is a future refinement
    /// requiring parameter-list scope tracking — deferred to v1.5.
    /// </summary>
    public static int?[] ClassifyTokens(IReadOnlyList<Token> tokens)
    {
        var result = new int?[tokens.Count];
        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Type == TokenType.Identifier)
            {
                // Type-identifier check wins regardless of position.
                if (t.Text != null && KnownTypeIdentifiers.Contains(t.Text))
                {
                    result[i] = (int)LegendIndex.Type;
                    continue;
                }

                // Find the previous non-Comment token (synthetic Comment tokens
                // from ScanCommentTokens may be merged in; skip them when
                // resolving syntactic context).
                TokenType? prev = null;
                for (int j = i - 1; j >= 0; j--)
                {
                    if (tokens[j].Type != TokenType.Comment)
                    {
                        prev = tokens[j].Type;
                        break;
                    }
                }
                if (prev == TokenType.LParen || prev == TokenType.Proc)
                {
                    result[i] = (int)LegendIndex.Function;
                }
                else
                {
                    result[i] = (int)LegendIndex.Variable;
                }
                continue;
            }
            result[i] = MapTokenType(t.Type);
        }
        return result;
    }

    /// <summary>
    /// Scan a source buffer for Flow's 5 lexer-recognized comment forms and
    /// emit synthetic <see cref="Token"/> instances with type
    /// <see cref="TokenType.Comment"/>. The lexer itself consumes comments as
    /// whitespace (<c>SimpleLexer.SkipWhitespaceAndComments</c>) without
    /// producing tokens, so this side-channel scan is needed to surface them
    /// as semantic tokens for editor coloring.
    ///
    /// Recognized forms (mirroring <c>SimpleLexer.SkipWhitespaceAndComments</c>):
    /// <list type="bullet">
    /// <item>Mid-line <c>//</c> to end-of-line (outside string literals).</item>
    /// <item>Line-start <c>;</c> (D-11 Option A, Phase 31 SPEC-4).</item>
    /// <item>Line-start <c>Note:</c>.</item>
    /// <item>Line-start <c>TODO:</c> (Phase 31 SPEC-4).</item>
    /// <item>Line-start <c>FIXME:</c> (Phase 31 SPEC-4).</item>
    /// </list>
    ///
    /// "Line-start" means after optional leading whitespace only (matches the
    /// lexer's <c>IsStartOfLineContent()</c> predicate).
    ///
    /// Returns at most one comment token per line (subsequent comment-starters
    /// on the same line are inside the first comment's range). String-literal
    /// recognition is intentionally minimal — tracks <c>"..."</c> and
    /// backslash-escaped characters; doesn't handle interpolated string
    /// segments, but those produce <see cref="TokenType.InterpolatedStringText"/>
    /// tokens already classified as String by the encoder.
    /// </summary>
    public static IReadOnlyList<Token> ScanCommentTokens(string text)
    {
        var comments = new List<Token>();
        if (string.IsNullOrEmpty(text)) return comments;

        var lines = text.Split('\n');
        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            // strip trailing \r for CRLF files so column accounting stays clean
            var line = lines[lineIdx].TrimEnd('\r');
            int lineNo = lineIdx + 1;

            // Pass 1: line-start lead-ins (after optional whitespace).
            int wsEnd = 0;
            while (wsEnd < line.Length && char.IsWhiteSpace(line[wsEnd])) wsEnd++;
            if (wsEnd < line.Length)
            {
                string? leadIn = null;
                if (line[wsEnd] == ';') leadIn = ";";
                else if (StartsWithAt(line, wsEnd, "Note:")) leadIn = "Note:";
                else if (StartsWithAt(line, wsEnd, "TODO:")) leadIn = "TODO:";
                else if (StartsWithAt(line, wsEnd, "FIXME:")) leadIn = "FIXME:";

                if (leadIn != null)
                {
                    string commentText = line.Substring(wsEnd);
                    comments.Add(new Token(
                        TokenType.Comment,
                        commentText,
                        new SourceLocation(lineNo, wsEnd + 1)));
                    continue;
                }
            }

            // Pass 2: mid-line `//` outside string literals.
            bool inString = false;
            for (int j = 0; j < line.Length - 1; j++)
            {
                char ch = line[j];
                if (ch == '\\' && j + 1 < line.Length)
                {
                    j++; // skip escaped character (\", \\, etc.)
                    continue;
                }
                if (ch == '"')
                {
                    inString = !inString;
                    continue;
                }
                if (!inString && ch == '/' && line[j + 1] == '/')
                {
                    string commentText = line.Substring(j);
                    comments.Add(new Token(
                        TokenType.Comment,
                        commentText,
                        new SourceLocation(lineNo, j + 1)));
                    break;
                }
            }
        }
        return comments;
    }

    private static bool StartsWithAt(string line, int offset, string pattern) =>
        offset + pattern.Length <= line.Length &&
        line.AsSpan(offset, pattern.Length).SequenceEqual(pattern.AsSpan());

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
        var classifications = ClassifyTokens(tokens);
        int prevLine = 0;
        int prevCol = 0;
        bool first = true;
        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            var typeIdx = classifications[i];
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
