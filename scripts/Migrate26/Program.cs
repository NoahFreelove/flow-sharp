using System.Text;
using FlowLang.Lexing;
using FlowLang.Diagnostics;

namespace FlowLang.Migrate26;

/// <summary>
/// Phase 26 migration tool — Wave 2 walker (plan 26-03).
///
/// Re-uses <see cref="SimpleLexer"/> to walk tokens; for every infix
/// Plus/Minus/Star/Slash between value-producing tokens, emits the prefix form
/// `(add A B)` / `(sub A B)` / `(mul A B)` / `(div A B)`. String concatenation
/// becomes `(concat A B)`. Parser shorthand `-IDENT` collapses to `(neg IDENT)`;
/// `+IDENT` is silently stripped (D-03).
///
/// Note-stream regions (Pipe...Pipe) are pass-through — they have their own
/// typed-literal arithmetic (-3dB, +50c, C4/12 fractional duration). RESEARCH
/// Pitfall 3.
///
/// Idempotent — running twice produces zero diff. Structurally guaranteed:
/// `(add a b)` has no Plus/Minus token between value-producing tokens.
///
/// Wave 3 (plan 26-04) runs this against tests/, examples/, and flow-lang/*.flow.
/// </summary>
internal static class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: dotnet run --project scripts/Migrate26 -- <file-or-dir> [...]");
            Console.Error.WriteLine("Migrates infix arithmetic in .flow files to prefix form.");
            return 1;
        }

        var files = ExpandPaths(args).ToList();
        int touched = 0, skipped = 0;
        foreach (var file in files)
        {
            string before = File.ReadAllText(file);
            string after;
            try { after = Migrate(before); }
            catch (Exception ex)
            {
                // File fails to lex — likely a pre-existing broken/aspirational test
                // (e.g. tests/test_special_identifiers.flow uses `#`, `$`, `?`, `<>`
                // which the lexer rejects). Skip it: a file that can't lex can't run,
                // so migrating it has no effect on byte-identical output. Logged so
                // operators can audit which files were skipped.
                Console.Error.WriteLine($"SKIP (lex error): {file}: {ex.Message}");
                skipped++;
                continue;
            }
            if (before != after)
            {
                File.WriteAllText(file, after);
                Console.WriteLine($"migrated: {file}");
                touched++;
            }
            else
            {
                skipped++;
            }
        }
        Console.WriteLine($"Done. {touched} migrated, {skipped} unchanged.");
        return 0;
    }

    static IEnumerable<string> ExpandPaths(string[] args)
    {
        foreach (var p in args)
        {
            if (Directory.Exists(p))
            {
                foreach (var f in Directory.EnumerateFiles(p, "*.flow", SearchOption.AllDirectories))
                    yield return f;
            }
            else if (File.Exists(p) && p.EndsWith(".flow"))
            {
                yield return p;
            }
            else
            {
                Console.Error.WriteLine($"warning: skipping {p} (not a .flow file or directory)");
            }
        }
    }

    // ===== Per-file migration =====

    static string Migrate(string source)
    {
        var lexer = new SimpleLexer(source, new ErrorReporter());
        var tokens = lexer.Tokenize();
        return RewriteSpans(source, tokens);
    }

    // ===== Walker =====

    static string RewriteSpans(string source, List<Token> tokens)
    {
        // Compute byte offset of each token using line-offset table.
        int[] lineStart = ComputeLineStarts(source);
        int[] tokenOffset = new int[tokens.Count];
        for (int i = 0; i < tokens.Count; i++)
        {
            var loc = tokens[i].Location;
            int line = loc.Line - 1;     // 1-based -> 0-based
            int col = loc.Column - 1;
            tokenOffset[i] = (line >= 0 && line < lineStart.Length) ? lineStart[line] + col : 0;
        }

        // Detect spans of infix arithmetic at top level (NOT inside Pipe...Pipe).
        // For each detected span, build prefix-form replacement and record (start, end, text).
        var edits = new List<(int Start, int End, string Replacement)>();

        int idx = 0;
        int pipeDepth = 0;        // toggle on each Pipe; ODD = inside note stream
        while (idx < tokens.Count)
        {
            var t = tokens[idx];

            // Stop at EOF
            if (t.Type == TokenType.Eof) break;

            if (t.Type == TokenType.Pipe)
            {
                pipeDepth++;
                idx++;
                continue;
            }
            if (pipeDepth % 2 == 1)
            {
                // Inside note stream — pass through (Pitfall 3).
                idx++;
                continue;
            }

            // Musical-context keywords (Timesig, Tempo, Key, Pan, Swing) take a
            // literal value as their argument — NOT a general expression. The
            // parser hard-codes the shape (e.g., timesig 4/4 expects IntLit Slash
            // IntLit). If we let the additive walker run on `4/4` it rewrites to
            // `(div 4 4)`, which the parser then rejects with "Expected integer
            // numerator". Skip past the keyword + its literal payload so the
            // walker never sees those tokens.
            if (t.Type == TokenType.Timesig)
            {
                idx++; // past 'timesig'
                // Optional sign on numerator (defensive — parser allows none, but be safe)
                if (idx < tokens.Count && (tokens[idx].Type == TokenType.Plus || tokens[idx].Type == TokenType.Minus)) idx++;
                if (idx < tokens.Count && tokens[idx].Type == TokenType.IntLiteral) idx++;
                if (idx < tokens.Count && tokens[idx].Type == TokenType.Slash) idx++;
                if (idx < tokens.Count && tokens[idx].Type == TokenType.IntLiteral) idx++;
                continue;
            }
            if (t.Type == TokenType.Tempo || t.Type == TokenType.Swing || t.Type == TokenType.Pan)
            {
                idx++; // past keyword
                // Single numeric literal (with optional unary sign) per parser.
                if (idx < tokens.Count && (tokens[idx].Type == TokenType.Plus || tokens[idx].Type == TokenType.Minus)) idx++;
                if (idx < tokens.Count && (tokens[idx].Type == TokenType.IntLiteral || tokens[idx].Type == TokenType.FloatLiteral)) idx++;
                continue;
            }
            if (t.Type == TokenType.Key)
            {
                idx++; // past 'key'
                // Key value is an Identifier (Cmajor, Fminor, etc.) — atomic.
                if (idx < tokens.Count && tokens[idx].Type == TokenType.Identifier) idx++;
                continue;
            }

            // Square brackets at top level are SONG ARRANGEMENT context (`[intro
            // verse*2 chorus]`), NOT array literal. The `*` between section name
            // and repeat count is not arithmetic — it's parser-level repeat
            // syntax (Parser.ParseSongExpression). The bracket handling inside
            // TryParsePrimarySpan correctly captures `[...]` verbatim, but only
            // when the bracket is part of a larger expression span (so its idx
            // advancement reaches the outer loop). If the bracket sits at the
            // start of a statement (no preceding value-producing token), the
            // outer loop falls through and individually visits the inside
            // tokens, where it would rewrite `verse*2` to `(mul verse 2)`. Skip
            // past matching `]` here so song arrangements are never visited.
            if (t.Type == TokenType.LBracket)
            {
                int depth = 1;
                idx++;
                while (idx < tokens.Count && depth > 0)
                {
                    if (tokens[idx].Type == TokenType.LBracket) depth++;
                    else if (tokens[idx].Type == TokenType.RBracket) depth--;
                    idx++;
                }
                continue;
            }

            // Try to detect an infix-arithmetic span starting at this token.
            if (IsExpressionStart(t.Type) || t.Type == TokenType.Minus || t.Type == TokenType.Plus)
            {
                int spanEnd;
                string? prefix = TryParseAdditiveSpan(tokens, idx, out spanEnd);
                if (prefix != null && spanEnd > idx)
                {
                    // Compute byte-range in source. Span starts at tokenOffset[idx], ends at end of token spanEnd-1.
                    int startOffset = tokenOffset[idx];
                    int lastIdx = spanEnd - 1;
                    int endOffset = tokenOffset[lastIdx] + tokens[lastIdx].Text.Length;
                    if (endOffset > source.Length) endOffset = source.Length;
                    if (startOffset < 0) startOffset = 0;
                    // Ensure we actually changed something (idempotence guard).
                    if (endOffset > startOffset)
                    {
                        string original = source.Substring(startOffset, endOffset - startOffset);
                        if (original != prefix)
                            edits.Add((startOffset, endOffset, prefix));
                    }
                    idx = spanEnd;
                    continue;
                }
            }
            idx++;
        }

        // Apply edits in reverse so offsets stay valid.
        edits.Sort((a, b) => b.Start.CompareTo(a.Start));
        var sb = new StringBuilder(source);
        foreach (var (s, e, r) in edits)
        {
            sb.Remove(s, e - s);
            sb.Insert(s, r);
        }
        return sb.ToString();
    }

    static int[] ComputeLineStarts(string source)
    {
        var list = new List<int> { 0 };
        for (int i = 0; i < source.Length; i++)
            if (source[i] == '\n') list.Add(i + 1);
        return list.ToArray();
    }

    // ===== Precedence climber =====
    //
    // Mirrors deleted ParseAdditive / ParseMultiplicative:
    //   additive       := multiplicative ((Plus|Minus) multiplicative)*
    //   multiplicative := primary ((Star|Slash) primary)*
    //   primary        := IntLit | FloatLit | StringLit | Ident | NoteLit | (expr) | [...]
    //                   | (Plus|Minus) primary    -- unary
    //
    // Returns the prefix-form text spanning [startIdx .. spanEnd) (exclusive). Returns
    // null if no infix arithmetic was found at startIdx (caller advances by 1).

    static string? TryParseAdditiveSpan(List<Token> tokens, int startIdx, out int spanEnd)
    {
        int idx = startIdx;
        bool rewrote = false;
        string? lhs = TryParseMultiplicativeSpan(tokens, ref idx, ref rewrote);
        if (lhs == null) { spanEnd = startIdx; return null; }

        bool isStringConcat = LooksLikeStringStart(tokens, startIdx);
        while (idx < tokens.Count
            && (tokens[idx].Type == TokenType.Plus || tokens[idx].Type == TokenType.Minus)
            && (idx + 1 < tokens.Count) && IsPrimaryStart(tokens[idx + 1].Type))
        {
            string op = tokens[idx].Type == TokenType.Plus ? "add" : "sub";
            // Special-case: String + value -> (concat ...) — defensive (D-09).
            if (op == "add" && isStringConcat) op = "concat";
            idx++;
            string? rhs = TryParseMultiplicativeSpan(tokens, ref idx, ref rewrote);
            if (rhs == null) { spanEnd = startIdx; return null; }
            lhs = $"({op} {lhs} {rhs})";
            rewrote = true;
        }
        spanEnd = idx;
        // Return null when the span produced no rewrites — the walker advances by 1
        // so we never strand the same span without progress. `rewrote` is true if
        // ANY transformation happened (additive op, multiplicative op, unary
        // lowering, or recursive rewrite inside a paren).
        return rewrote ? lhs : null;
    }

    static string? TryParseMultiplicativeSpan(List<Token> tokens, ref int idx, ref bool rewrote)
    {
        string? lhs = TryParsePrimarySpan(tokens, ref idx, ref rewrote);
        if (lhs == null) return null;

        while (idx < tokens.Count
            && (tokens[idx].Type == TokenType.Star || tokens[idx].Type == TokenType.Slash)
            && (idx + 1 < tokens.Count) && IsPrimaryStart(tokens[idx + 1].Type))
        {
            string op = tokens[idx].Type == TokenType.Star ? "mul" : "div";
            idx++;
            string? rhs = TryParsePrimarySpan(tokens, ref idx, ref rewrote);
            if (rhs == null) return null;
            lhs = $"({op} {lhs} {rhs})";
            rewrote = true;
        }
        return lhs;
    }

    static string? TryParsePrimarySpan(List<Token> tokens, ref int idx, ref bool rewrote)
    {
        if (idx >= tokens.Count) return null;

        // Unary minus on identifier -> (neg IDENT)
        if (tokens[idx].Type == TokenType.Minus
            && idx + 1 < tokens.Count
            && tokens[idx + 1].Type == TokenType.Identifier)
        {
            string name = tokens[idx + 1].Text;
            idx += 2;
            rewrote = true;
            return $"(neg {name})";
        }
        // Unary plus on identifier -> just the identifier (D-03 silent strip)
        if (tokens[idx].Type == TokenType.Plus
            && idx + 1 < tokens.Count
            && tokens[idx + 1].Type == TokenType.Identifier)
        {
            string name = tokens[idx + 1].Text;
            idx += 2;
            rewrote = true;   // dropped the leading '+', which IS a rewrite
            return name;
        }
        // Unary minus on a number literal that the lexer kept as a separate Minus token
        // (e.g., the lexer didn't gate this position as expression-start). Coalesce into
        // a signed literal text so emitted prefix calls look like `(sub a -3)` rather
        // than `(sub a (sub 0 3))`.
        if (tokens[idx].Type == TokenType.Minus
            && idx + 1 < tokens.Count
            && (tokens[idx + 1].Type == TokenType.IntLiteral
                || tokens[idx + 1].Type == TokenType.FloatLiteral))
        {
            string text = tokens[idx + 1].Text;
            idx += 2;
            rewrote = true;
            return text.StartsWith("-") ? text : "-" + text;
        }
        if (tokens[idx].Type == TokenType.Plus
            && idx + 1 < tokens.Count
            && (tokens[idx + 1].Type == TokenType.IntLiteral
                || tokens[idx + 1].Type == TokenType.FloatLiteral))
        {
            string text = tokens[idx + 1].Text;
            idx += 2;
            rewrote = true;
            return text;
        }

        var t = tokens[idx];

        // Parenthesised sub-expression: capture inner tokens, recurse for migration.
        if (t.Type == TokenType.LParen)
        {
            int depth = 1;
            int start = idx;
            idx++;
            while (idx < tokens.Count && depth > 0)
            {
                if (tokens[idx].Type == TokenType.LParen) depth++;
                else if (tokens[idx].Type == TokenType.RParen) depth--;
                if (depth > 0) idx++;
            }
            if (idx >= tokens.Count) return null;
            idx++; // consume RParen

            // Recurse: the inner content might contain infix that needs migrating.
            int innerStart = start + 1, innerEnd = idx - 1;
            if (innerEnd > innerStart)
            {
                var inner = tokens.GetRange(innerStart, innerEnd - innerStart);
                int e;
                string? rewrittenInner = TryParseAdditiveSpan(inner, 0, out e);
                if (rewrittenInner != null && e == inner.Count)
                {
                    rewrote = true;   // inner span was rewritten
                    return $"({rewrittenInner})";
                }
                // Otherwise emit the original verbatim (parens preserved, inner might be an
                // existing prefix call or comma-separated argument list which doesn't need
                // migrating). NOTE: this does NOT mark `rewrote=true` — the walker should
                // skip this span entirely (advance by 1 from outer loop, eventually stepping
                // into the inner tokens to handle them per-statement).
                return $"({TokensToText(inner)})";
            }
            return "()";
        }

        // Bracket-delimited sub-expression (array literal): capture verbatim
        if (t.Type == TokenType.LBracket)
        {
            int depth = 1;
            int start = idx;
            idx++;
            while (idx < tokens.Count && depth > 0)
            {
                if (tokens[idx].Type == TokenType.LBracket) depth++;
                else if (tokens[idx].Type == TokenType.RBracket) depth--;
                if (depth > 0) idx++;
            }
            if (idx >= tokens.Count) return null;
            idx++; // consume RBracket
            return TokensToText(tokens.GetRange(start, idx - start));
        }

        // Atomic token — Identifier / IntLiteral / FloatLiteral / StringLiteral / NoteLiteral / BoolLiteral
        if (IsAtomicValueToken(t.Type))
        {
            // Token.Text for StringLiteral already includes quotes (verified at
            // SimpleLexer.cs:201: `$"\"{value}\""`). All other atomic tokens use
            // Text as the verbatim lexeme. So emitting Text is round-trip-safe.
            string text = t.Text;
            idx++;
            return text;
        }

        return null;
    }

    // ===== Token classification =====

    static bool IsValueProducing(TokenType t) =>
        t is TokenType.IntLiteral or TokenType.FloatLiteral or TokenType.StringLiteral
          or TokenType.NoteLiteral or TokenType.BoolLiteral or TokenType.Identifier
          or TokenType.RParen or TokenType.RBracket;

    // What can BEGIN an additive/multiplicative expression — anything that can start a
    // primary, plus the unary +/- prefix sequences handled in TryParsePrimarySpan.
    static bool IsExpressionStart(TokenType t) =>
        t is TokenType.IntLiteral or TokenType.FloatLiteral or TokenType.StringLiteral
          or TokenType.NoteLiteral or TokenType.BoolLiteral or TokenType.Identifier
          or TokenType.LParen or TokenType.LBracket;

    // What can BEGIN a primary span — same as IsExpressionStart, plus unary +/-.
    static bool IsPrimaryStart(TokenType t) =>
        IsExpressionStart(t) || t == TokenType.Minus || t == TokenType.Plus;

    static bool IsAtomicValueToken(TokenType t) =>
        t is TokenType.IntLiteral or TokenType.FloatLiteral or TokenType.StringLiteral
          or TokenType.NoteLiteral or TokenType.BoolLiteral or TokenType.Identifier;

    // Did the LHS span begin with a StringLiteral? Used to switch (add) → (concat)
    // for the "abc" + x case (D-09, defensive).
    static bool LooksLikeStringStart(List<Token> tokens, int startIdx)
    {
        if (startIdx >= tokens.Count) return false;
        return tokens[startIdx].Type == TokenType.StringLiteral;
    }

    static string TokensToText(List<Token> tokens)
    {
        // Reconstruct source by joining token texts with single spaces.
        // (Slightly lossy on whitespace, but acceptable since the migration only edits
        //  spans that contained arithmetic.)
        return string.Join(" ", tokens.Select(t => t.Text));
    }
}
