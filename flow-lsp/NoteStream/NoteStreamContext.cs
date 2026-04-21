using System;
using System.Collections.Generic;
using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Lexing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using FlowProgram = FlowLang.Ast.Program;

namespace FlowLsp.NoteStream;

/// <summary>
/// Token-scan-based detector for "is cursor inside a note stream" and "what key encloses
/// the cursor, if any". Walks the TOKEN LIST ALREADY PRODUCED BY ParseSession (available
/// on ParseResult.Tokens) backward from the cursor position and tracks brace depth to
/// correctly handle block exits (cursor AFTER a closed `key { }` block returns null —
/// a naive line-heuristic would over-match).
///
/// We deliberately DO NOT re-instantiate SimpleLexer: (a) its constructor requires an
/// ErrorReporter, (b) duplicating the lex work is wasteful, and (c) the caller always
/// has the tokens in hand via ParseResult.Tokens. Pass the tokens in.
///
/// Phase 17 (17-06). Consumed by CompletionHandler note-stream branch (D-11).
/// </summary>
public static class NoteStreamContext
{
    /// <summary>
    /// True iff the cursor sits inside a `| ... |` note stream. Uses the AST (which the
    /// parser already produced) to detect stream boundaries, since the lexer doesn't flag
    /// "inside stream" specially — stream boundaries are a parser construct.
    /// </summary>
    public static bool IsInsideNoteStream(FlowProgram ast, string source, Position cursor)
    {
        int cursorOffset = OffsetOf(source, cursor);
        return WalkFindStream(ast.Statements, source, cursorOffset);
    }

    /// <summary>
    /// Returns the name (e.g. "Cmajor", "Dminor") of the innermost `key &lt;name&gt; { ... }`
    /// block enclosing the cursor, or null if none. Consumes the cached token list from
    /// ParseResult.Tokens — see ParseSession in plan 17-01. Handles block exits correctly.
    /// </summary>
    public static string? FindEnclosingKey(
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source,
        Position cursor)
    {
        int cursorOffset = OffsetOf(source, cursor);
        int[] lineStarts = ComputeLineStarts(source);

        // Index of the token AT or just-before the cursor offset.
        int cursorTokIdx = FindTokenBeforeOffset(tokens, lineStarts, cursorOffset);

        // Walk backward from cursorTokIdx, tracking brace depth.
        // depth increments on `}` (we're crossing OUT of a block as we scan back).
        // depth decrements on `{` — when depth < 0, the cursor is INSIDE the block
        // whose opening brace we just passed.
        //
        // When we cross an opening `{` with depth becoming < 0, inspect the tokens
        // just before that `{`: if they form `key <identifier>`, the cursor is inside
        // that key block and we return the identifier. Otherwise keep walking back (the
        // current `{` is some other block — continue to find an outer `key` if any).
        int depth = 0;
        for (int i = cursorTokIdx; i >= 0; i--)
        {
            var tok = tokens[i];
            if (tok.Type == TokenType.RBrace)
            {
                depth++;
            }
            else if (tok.Type == TokenType.LBrace)
            {
                depth--;
                if (depth < 0)
                {
                    // Cursor is inside this `{ ... }` block. Is it a key block?
                    // Look at the tokens immediately preceding this `{`:
                    //   `key <identifier> {`  → return identifier
                    //   anything else         → keep scanning back, but note depth has reset:
                    //                           the enclosing block wasn't a key, so the
                    //                           search continues at the depth "inside that
                    //                           block" relative to any OUTER block. We need
                    //                           to continue with depth = 0 to find the next
                    //                           enclosing block out.
                    int keyIdx = i - 1;
                    // SimpleLexer does not emit whitespace tokens (confirmed at
                    // SkipWhitespaceAndComments). If a future phase adds trivia tokens,
                    // extend IsTriviaToken accordingly.
                    while (keyIdx >= 0 && IsTriviaToken(tokens[keyIdx])) keyIdx--;

                    // Expect identifier (key name) then `key` keyword.
                    if (keyIdx >= 1
                        && tokens[keyIdx].Type == TokenType.Identifier
                        && tokens[keyIdx - 1].Type == TokenType.Key)
                    {
                        // Token.Text carries the lexeme (the identifier text itself).
                        return tokens[keyIdx].Text;
                    }

                    // Not a key block — we're inside some non-key block (e.g. `proc` body,
                    // `tempo` block, `section`). Continue scanning back for an OUTER key,
                    // but reset depth to 0 so we correctly track the next enclosing block.
                    depth = 0;
                }
            }
        }
        return null;
    }

    private static bool IsTriviaToken(Token t)
    {
        // SimpleLexer does not emit whitespace tokens as of Phase 14 — SkipWhitespaceAndComments
        // consumes whitespace in the lex loop and never enqueues a trivia token. If a future
        // phase adds whitespace/newline tokens, extend this check accordingly.
        return false;
    }

    /// <summary>
    /// Convert a 0-based LSP (Line, Character) position to a 0-based byte offset in the
    /// source. Treats '\n' as the sole line separator (consistent with SimpleLexer).
    /// </summary>
    private static int OffsetOf(string source, Position cursor)
    {
        int line = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (line == cursor.Line)
            {
                return Math.Min(i + Math.Max(0, cursor.Character), source.Length);
            }
            if (source[i] == '\n') line++;
        }
        return source.Length;
    }

    /// <summary>
    /// Returns the index of the token whose starting offset is ≤ cursorOffset. If the
    /// cursor is before any token, returns -1. Uses Token.Location.Line / .Column
    /// converted via the source-line offsets.
    /// </summary>
    private static int FindTokenBeforeOffset(IReadOnlyList<Token> tokens, int[] lineStarts, int cursorOffset)
    {
        int last = -1;
        for (int i = 0; i < tokens.Count; i++)
        {
            int tokOffset = TokenAbsOffset(tokens[i], lineStarts);
            if (tokOffset <= cursorOffset) last = i;
            else break;
        }
        return last;
    }

    private static int[] ComputeLineStarts(string source)
    {
        var starts = new List<int> { 0 };
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n') starts.Add(i + 1);
        }
        return starts.ToArray();
    }

    private static int TokenAbsOffset(Token t, int[] lineStarts)
    {
        // SimpleLexer uses 1-based Line/Column. Token carries SourceLocation, not top-level
        // Line/Column fields. SourceLocation.Unknown has (0,0) — guard with Math.Max.
        int line0 = Math.Max(0, t.Location.Line - 1);
        int col0 = Math.Max(0, t.Location.Column - 1);
        if (line0 >= lineStarts.Length) return 0;
        return lineStarts[line0] + col0;
    }

    // === IsInsideNoteStream uses an AST walk (stream boundaries are parser-level) ===

    private static bool WalkFindStream(IReadOnlyList<Statement> stmts, string source, int cursorOffset)
    {
        foreach (var s in stmts)
        {
            if (StatementContainsStream(s, source, cursorOffset)) return true;
        }
        return false;
    }

    private static bool StatementContainsStream(Statement s, string source, int cursorOffset)
    {
        switch (s)
        {
            case MusicalContextStatement m:
                if (WalkFindStream(m.Body, source, cursorOffset)) return true;
                break;
            case SectionDeclaration sd:
                if (WalkFindStream(sd.Body, source, cursorOffset)) return true;
                break;
            case ProcDeclaration pd:
                if (WalkFindStream(pd.Body, source, cursorOffset)) return true;
                break;
            case ExpressionStatement es:
                if (es.Expression is NoteStreamExpression ns
                    && StreamContainsOffset(ns, source, cursorOffset))
                    return true;
                break;
            case VariableDeclaration vd:
                if (vd.Value is NoteStreamExpression nsv
                    && StreamContainsOffset(nsv, source, cursorOffset))
                    return true;
                break;
        }
        return false;
    }

    private static bool StreamContainsOffset(NoteStreamExpression ns, string source, int cursorOffset)
    {
        // NoteStreamExpression.Location points at the opening `|`. End is the matching `|`.
        // Walk the source forward from the opening `|` until the matching `|` to compute
        // the end offset.
        int[] lineStarts = ComputeLineStarts(source);
        int line0 = Math.Max(0, ns.Location.Line - 1);
        int col0 = Math.Max(0, ns.Location.Column - 1);
        if (line0 >= lineStarts.Length) return false;
        int startOffset = lineStarts[line0] + col0;
        int endOffset = FindMatchingCloseStream(source, startOffset);
        return cursorOffset >= startOffset && cursorOffset <= endOffset;
    }

    private static int FindMatchingCloseStream(string source, int startOffset)
    {
        // startOffset points at the opening `|`. Note streams can have multiple bars:
        // | C4 D4 | E4 F4 |. Walk forward through all consecutive `|`-delimited bars.
        //
        // WR-05 fix: if the user is mid-edit with an UNCLOSED stream (typed `| C4 D4 `
        // without the closing `|`), the end-of-stream must extend to end-of-file
        // (or the next `}`) rather than collapsing back to the opening `|` — otherwise
        // StreamContainsOffset's `cursor <= endOffset` check fails and the cursor is
        // reported as OUTSIDE the stream exactly when the user is actively typing in it.
        //
        // Strategy:
        //   - closedEnd tracks the last `|` we've seen (`-1` → no closing pipe yet).
        //   - If we hit a `}` or end-of-source:
        //       * If we saw at least one closing `|` after startOffset, that is the
        //         stream end (closed form).
        //       * Otherwise the stream is unclosed — the cursor should still be
        //         treated as inside, so return the `}` index or end-of-source as a
        //         wide "inside" range.
        int closedEnd = -1;
        for (int i = startOffset + 1; i < source.Length; i++)
        {
            char c = source[i];
            if (c == '|') { closedEnd = i; }
            else if (c == '}')
            {
                // Stream terminated by a closing brace. If we saw closing `|`s, return
                // the last one; otherwise the stream was mid-edit and unclosed — return
                // the brace index so the cursor (which sits before the brace) is inside.
                return closedEnd >= 0 ? closedEnd : i;
            }
        }
        // Reached end-of-source. If we saw a closing `|`, use it. Otherwise the stream
        // is unclosed (user is mid-typing); treat all remaining source as inside.
        return closedEnd >= 0 ? closedEnd : source.Length;
    }
}
