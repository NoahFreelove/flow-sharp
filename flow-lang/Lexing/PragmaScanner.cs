using System.Text;
using FlowLang.Core;
using FlowLang.Diagnostics;

namespace FlowLang.Lexing;

/// <summary>
/// Pre-lex source-transformation stage that extracts file-scope <c>enable
/// &lt;pragma&gt;;</c> declarations from the prefix region of a Flow source file.
/// Runs BEFORE <see cref="SimpleLexer"/> in <see cref="FlowLang.Core.FlowEngine"/>
/// and <see cref="FlowLang.Runtime.ModuleLoader"/> per D-01.
///
/// Returns a <see cref="PragmaSet"/> + a transformed source string. The
/// transformed source is character-by-character identical to the original
/// EXCEPT the matched pragma lines are replaced with equivalent-length
/// whitespace (preserving newlines). This keeps line + column numbering aligned
/// with the user's original source for downstream lex/parse error messages
/// (D-04).
///
/// Fast path (Pitfall F): when the source contains no <c>"enable"</c>
/// substring, the scanner returns the SAME string reference unchanged — zero
/// allocation, byte-identical determinism for every legacy .flow file.
/// </summary>
public static class PragmaScanner
{
    /// <summary>
    /// Phase 23 D-14 / MICR-03: single source-of-truth string appended to the unknown-pragma
    /// error when the typed name resembles a tuning pragma. Originally pointed at the
    /// then-deferred Scala loader; the loader SHIPPED in Phase 32 (v1.4), so the pointer
    /// now directs composers to the shipped surface (audit 2026-06-09 follow-up to §7.7).
    /// </summary>
    private const string ScalaLoaderDeferralPointer =
        "For custom tunings use the shipped Scala loader: (loadScala \"x.scl\") applied via a tuning t { ... } block.";

    /// <summary>
    /// Phase 23 D-14: returns true if <paramref name="typed"/> looks like a tuning pragma
    /// either via Levenshtein distance ≤ 3 from any of the three known tuning names, or via
    /// substring whitelist (tun, scal, temp, just, pyth, micro, intone). Used to gate the
    /// Scala-loader deferral pointer on the unknown-pragma error path so non-tuning typos
    /// (e.g. <c>verbose</c>) don't get the irrelevant pointer.
    /// </summary>
    private static bool LooksLikeTuningName(string typed)
    {
        var tuningNames = new[] { "justIntonation", "pythagorean", "equalTemperament" };
        foreach (var t in tuningNames)
            if (LevenshteinSmall(typed, t) <= 3) return true;
        var lower = typed.ToLowerInvariant();
        foreach (var sub in new[] { "tun", "scal", "temp", "just", "pyth", "micro", "intone" })
            if (lower.Contains(sub, System.StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>
    /// Wagner-Fischer Levenshtein, sized for closed-set pragma names. Same shape as
    /// <see cref="PragmaRegistry"/>'s private helper — the closed-set max name length
    /// bounds the inner loop regardless of caller-supplied input length (T-23-02-05
    /// mitigation in the threat register).
    /// </summary>
    private static int LevenshteinSmall(string a, string b)
    {
        int n = a.Length, m = b.Length;
        if (n == 0) return m;
        if (m == 0) return n;
        var prev = new int[m + 1];
        var curr = new int[m + 1];
        for (int j = 0; j <= m; j++) prev[j] = j;
        for (int i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = System.Math.Min(System.Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[m];
    }

    /// <summary>
    /// Pre-scan <paramref name="source"/> for file-scope pragma declarations.
    /// Errors (D-11 pragma-after-statement, D-12 unknown-pragma) are accumulated
    /// in <paramref name="errors"/>; this method never throws.
    /// </summary>
    public static (PragmaSet Pragmas, string TransformedSource) Scan(
        string source,
        string? fileName,
        ErrorReporter errors)
    {
        if (string.IsNullOrEmpty(source))
            return (PragmaSet.Empty, source ?? string.Empty);

        // Zero-allocation fast path (Pitfall F): if "enable" doesn't appear anywhere,
        // return the original string reference unchanged. Preserves byte-identical
        // determinism for every pre-Phase-21 .flow file (Phase 18 regression gate).
        if (source.IndexOf("enable", StringComparison.Ordinal) < 0)
            return (PragmaSet.Empty, source);

        var enabled = new HashSet<string>(StringComparer.Ordinal);
        var sites = new List<PragmaDeclarationSite>();
        SourceLocation? firstNonPragmaLineLoc = null;
        string? firstNonPragmaLineSummary = null;
        var sb = new StringBuilder(source.Length);

        int i = 0;
        int line = 1;
        bool prefixDone = false;

        while (i < source.Length)
        {
            int lineStart = i;
            // Walk to end-of-line (newline OR end-of-source).
            while (i < source.Length && source[i] != '\n') i++;
            int newlineIdx = i;          // index of '\n' or source.Length
            int contentEnd = newlineIdx; // exclusive end of line text (excludes \r\n / \n)
            // CRLF (Pitfall G): if last char before '\n' is '\r', exclude it from line text.
            if (contentEnd > lineStart && source[contentEnd - 1] == '\r') contentEnd--;
            string lineText = source.Substring(lineStart, contentEnd - lineStart);

            // Newline span: 0 (eof), 1 (\n alone), or 2 (\r\n).
            int newlineSpan;
            if (newlineIdx >= source.Length) newlineSpan = 0;
            else if (newlineIdx > lineStart && source[newlineIdx - 1] == '\r') newlineSpan = 2;
            else newlineSpan = 1;
            int lineEndIncl = newlineIdx < source.Length ? newlineIdx + 1 : newlineIdx;

            var pragmaMatch = TryMatchPragmaLine(lineText);
            bool isBlank = string.IsNullOrWhiteSpace(lineText);
            bool isLineComment = lineText.TrimStart().StartsWith("//", StringComparison.Ordinal);
            // "Note:" line comments (handled by SimpleLexer.SkipWhitespaceAndComments)
            // are also legal in the prefix region per D-03.
            bool isNoteComment = lineText.TrimStart().StartsWith("Note:", StringComparison.Ordinal);

            if (pragmaMatch != null && !prefixDone)
            {
                var name = pragmaMatch.Name;
                var nameLoc = new SourceLocation(line, pragmaMatch.NameStartCol, fileName);
                if (!PragmaRegistry.IsKnown(name))
                {
                    // D-12: unknown pragma name + alphabetized known list + did-you-mean.
                    var sugg = PragmaRegistry.SuggestNearest(name);
                    var msg = $"unknown pragma '{name}' at line {line}. " +
                              (sugg != null ? $"Did you mean '{sugg}'? " : "") +
                              $"Known pragmas: {PragmaRegistry.AlphabetizedKnownNames()}.";
                    // Phase 23 D-14 / MICR-03: when the typed name resembles a tuning pragma,
                    // append a pointer to the deferred Scala (.scl) loader so users searching
                    // for microtonal extension paths land on the v1.4 deferral note.
                    if (LooksLikeTuningName(name))
                        msg += "\n" + ScalaLoaderDeferralPointer;
                    errors.ReportError(msg, nameLoc);
                    // Continue scanning per CLAUDE.md error-accumulation principle.
                }
                else
                {
                    // D-09: duplicate is silent (set semantics).
                    enabled.Add(name);
                    sites.Add(new PragmaDeclarationSite(name, nameLoc));
                }

                // D-04: replace pragma line with equivalent-length spaces; preserve newline (\n or \r\n).
                AppendSpaces(sb, contentEnd - lineStart);
                AppendNewline(sb, source, newlineIdx, newlineSpan);
            }
            else if (pragmaMatch != null && prefixDone)
            {
                // D-11: pragma after the first non-pragma statement.
                var nameLoc = new SourceLocation(line, pragmaMatch.NameStartCol, fileName);
                int firstStmtLine = firstNonPragmaLineLoc!.Line;
                errors.ReportError(
                    $"'enable {pragmaMatch.Name};' at line {line}: pragmas must appear " +
                    $"before any other statement. First non-pragma statement was at " +
                    $"line {firstStmtLine} ({firstNonPragmaLineSummary}). " +
                    $"Move the pragma to the top of the file.",
                    nameLoc);
                // Strip the line so subsequent lex/parse doesn't double-error on `enable`.
                AppendSpaces(sb, contentEnd - lineStart);
                AppendNewline(sb, source, newlineIdx, newlineSpan);
            }
            else
            {
                // Non-pragma line: copy verbatim (including any \r\n).
                sb.Append(source, lineStart, lineEndIncl - lineStart);
                if (!prefixDone && !isBlank && !isLineComment && !isNoteComment)
                {
                    prefixDone = true;
                    firstNonPragmaLineLoc = new SourceLocation(line, 1, fileName);
                    firstNonPragmaLineSummary = lineText.Trim();
                    if (firstNonPragmaLineSummary.Length > 40)
                        firstNonPragmaLineSummary = firstNonPragmaLineSummary[..37] + "...";
                }
            }

            line++;
            i = lineEndIncl;
        }

        return (new PragmaSet(enabled, sites), sb.ToString());
    }

    private static void AppendSpaces(StringBuilder sb, int count)
    {
        for (int k = 0; k < count; k++) sb.Append(' ');
    }

    private static void AppendNewline(StringBuilder sb, string source, int newlineIdx, int newlineSpan)
    {
        if (newlineSpan == 2)
        {
            // CRLF: preserve \r and \n verbatim.
            sb.Append('\r');
            sb.Append('\n');
        }
        else if (newlineSpan == 1)
        {
            sb.Append(source[newlineIdx]); // '\n'
        }
        // newlineSpan == 0: end-of-source, append nothing.
    }

    private sealed record PragmaLineMatch(string Name, int NameStartCol, int NameEndCol);

    /// <summary>
    /// Manual state machine equivalent of:
    ///   ^[ \t]*enable[ \t]+IDENT[ \t]*;[ \t]*(// any)?$
    /// Returns null if the line is not a pragma declaration.
    /// </summary>
    private static PragmaLineMatch? TryMatchPragmaLine(string lineText)
    {
        int p = 0;
        while (p < lineText.Length && (lineText[p] == ' ' || lineText[p] == '\t')) p++;
        if (p + 6 > lineText.Length) return null;
        if (string.CompareOrdinal(lineText, p, "enable", 0, 6) != 0) return null;
        p += 6;
        // Require at least one whitespace after "enable".
        if (p >= lineText.Length || (lineText[p] != ' ' && lineText[p] != '\t')) return null;
        while (p < lineText.Length && (lineText[p] == ' ' || lineText[p] == '\t')) p++;
        // Identifier: [A-Za-z_][A-Za-z0-9_-]*
        // Phase 45 REQ-BEAT-PRAGMA-HYPHEN-01 — accept hyphens in CONTINUATION
        // position so `enable beat-true-to-sig;` parses cleanly. Leading-char
        // predicate stays unchanged (still letter/underscore only — hyphen
        // cannot appear as the first char). PragmaRegistry.KnownPragmas is a
        // closed-set Ordinal-string dictionary; unknown hyphenated names
        // (e.g. `foo-bar`) still error via the existing Levenshtein-suggester
        // path (45-PATTERNS.md §"Threat T-45-01").
        int identStart = p;
        if (p >= lineText.Length || !(char.IsLetter(lineText[p]) || lineText[p] == '_')) return null;
        while (p < lineText.Length && (char.IsLetterOrDigit(lineText[p]) || lineText[p] == '_' || lineText[p] == '-')) p++;
        int identEnd = p;
        string ident = lineText.Substring(identStart, identEnd - identStart);
        // Optional whitespace, then ';'
        while (p < lineText.Length && (lineText[p] == ' ' || lineText[p] == '\t')) p++;
        if (p >= lineText.Length || lineText[p] != ';') return null;
        p++;
        // Optional trailing whitespace, optional // comment to end of line.
        while (p < lineText.Length && (lineText[p] == ' ' || lineText[p] == '\t')) p++;
        if (p < lineText.Length)
        {
            // Must be the start of a // comment, otherwise the line has trailing
            // junk and is NOT a pragma (let the lexer handle it).
            if (p + 1 < lineText.Length && lineText[p] == '/' && lineText[p + 1] == '/')
                return new PragmaLineMatch(ident, identStart + 1, identEnd + 1); // +1: 1-based col
            return null;
        }
        return new PragmaLineMatch(ident, identStart + 1, identEnd + 1);
    }
}
