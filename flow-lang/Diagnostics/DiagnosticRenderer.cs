using System.Text;
using FlowLang.Core;

namespace FlowLang.Diagnostics;

/// <summary>
/// Phase 35 LANG-04 Wave 2a — Rust-style multi-line diagnostic renderer.
///
/// <para>
/// Consumes a <see cref="FlowDiagnostic"/> + <see cref="SourceMap"/> and
/// produces a multi-line string per RESEARCH §Example 4:
/// </para>
/// <code>
/// error: unknown identifier 'transpos'
///   --> tests/test_chain.flow:7:9
///    |
///  7 | seq -> (transpos 2)
///    |        ^^^^^^^^ not found in scope
///    |
///    = note: tried looking in: enclosing function 'main', module 'std'
///    = help: did you mean 'transpose'?
/// </code>
///
/// <para>
/// <see cref="Render(FlowDiagnostic, SourceMap, bool)"/> with
/// <c>useColor:true</c> embeds ANSI escape sequences for the level
/// keyword (red for error / yellow for warning / cyan for info) and the
/// caret line; with <c>useColor:false</c> the output is pure ASCII (the
/// baseline-comparable form used by the golden tests). Top-level emit
/// (flow-interpreter/Program.cs Task 3) selects useColor based on
/// stdout redirection per the existing Program.cs:77 convention.
/// </para>
/// </summary>
public static class DiagnosticRenderer
{
    // ANSI escape sequences — empty when useColor is off so the format
    // logic stays uniform across the colored/plain paths.
    private const string AnsiReset = "\x1b[0m";
    private const string AnsiRed = "\x1b[31m";
    private const string AnsiYellow = "\x1b[33m";
    private const string AnsiCyan = "\x1b[36m";
    private const string AnsiBold = "\x1b[1m";

    /// <summary>
    /// Renders the diagnostic into a multi-line string. The returned
    /// string does NOT include a trailing newline — callers append one
    /// (or join multiple diagnostics with a blank-line separator via
    /// <see cref="ErrorReporter.FormatDiagnostics"/>).
    /// </summary>
    public static string Render(
        FlowDiagnostic diagnostic,
        SourceMap sources,
        bool useColor = true)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentNullException.ThrowIfNull(sources);

        var sb = new StringBuilder();

        // Color palette — empty strings when useColor off.
        string c_level = useColor ? LevelColor(diagnostic.Level) + AnsiBold : "";
        string c_caret = useColor ? LevelColor(diagnostic.Level) : "";
        string c_note = useColor ? AnsiCyan + AnsiBold : "";
        string c_reset = useColor ? AnsiReset : "";

        // ─── Line 1: header — "error: <message>" ─────────────────────
        sb.Append(c_level);
        sb.Append(LevelKeyword(diagnostic.Level));
        sb.Append(':');
        sb.Append(c_reset);
        sb.Append(' ');
        sb.Append(diagnostic.Message);
        sb.Append('\n');

        // ─── Line 2: location — "  --> <file>:<line>:<col>" ──────────
        var primaryFile = diagnostic.Primary.Start.FileName ?? "<unknown>";
        var primaryLine = diagnostic.Primary.Start.Line;
        var primaryCol = diagnostic.Primary.Start.Column;
        sb.Append("  --> ");
        sb.Append(primaryFile);
        sb.Append(':');
        sb.Append(primaryLine);
        sb.Append(':');
        sb.Append(primaryCol);
        sb.Append('\n');

        // ─── Source-quote + caret rows ───────────────────────────────
        // Only emit when the source for the primary file is registered
        // AND the primary span resolves to a valid line in it. Per
        // ReplDiagnosticTests.MissingSourceEntryRendersLocationWithoutQuote.
        bool hasSource = sources.TryGetSource(primaryFile, out var sourceText)
                         && primaryLine >= 1;
        string[]? lines = null;
        int lineNumWidth = 0;
        if (hasSource)
        {
            lines = SplitLines(sourceText);
            if (primaryLine > lines.Length)
            {
                hasSource = false;
            }
            else
            {
                // Right-align line numbers across all lines we render. The
                // primary span's line is the only line we render today (the
                // labels-on-other-lines case is rare in the unknown-ident /
                // type-mismatch fixtures Wave 2a ships).
                lineNumWidth = primaryLine.ToString().Length;
            }
        }

        if (hasSource && lines is not null)
        {
            // Pipe-prefixed empty line (rustc convention)
            sb.Append(PadLineNum("", lineNumWidth));
            sb.Append(" |");
            sb.Append('\n');

            // Source-quote line — " N | <source line>"
            var srcLine = lines[primaryLine - 1];
            sb.Append(PadLineNum(primaryLine.ToString(), lineNumWidth));
            sb.Append(" | ");
            sb.Append(srcLine);
            sb.Append('\n');

            // Caret line — "   | <leading spaces><carets> <label?>"
            // Carets sized from primary.End.Column - primary.Start.Column,
            // minimum 1 (zero-width spans still get one caret).
            var startCol = diagnostic.Primary.Start.Column;
            var endCol = diagnostic.Primary.End.Column;
            int caretWidth = Math.Max(1, endCol - startCol);
            // (startCol - 1) leading spaces between the `| ` and the first caret.
            int leadingSpaces = Math.Max(0, startCol - 1);

            // Primary-label text — prefer the label that matches the primary
            // span exactly; fall back to first label whose span lies on the
            // same line; otherwise empty.
            string primaryLabelText = ResolvePrimaryLabel(diagnostic);

            sb.Append(PadLineNum("", lineNumWidth));
            sb.Append(" | ");
            sb.Append(new string(' ', leadingSpaces));
            sb.Append(c_caret);
            sb.Append(new string('^', caretWidth));
            sb.Append(c_reset);
            if (!string.IsNullOrEmpty(primaryLabelText))
            {
                sb.Append(' ');
                sb.Append(c_caret);
                sb.Append(primaryLabelText);
                sb.Append(c_reset);
            }
            sb.Append('\n');

            // Pipe-prefixed empty line between caret and notes/help (rustc).
            if (diagnostic.Notes.Count > 0 || diagnostic.Suggestion != null)
            {
                sb.Append(PadLineNum("", lineNumWidth));
                sb.Append(" |");
                sb.Append('\n');
            }
        }

        // ─── Notes — "   = note: <text>" rows ────────────────────────
        foreach (var note in diagnostic.Notes)
        {
            sb.Append(PadLineNum("", lineNumWidth));
            sb.Append(" = ");
            sb.Append(c_note);
            sb.Append("note");
            sb.Append(c_reset);
            sb.Append(": ");
            sb.Append(note);
            sb.Append('\n');
        }

        // ─── Suggestion — "   = help: did you mean '<sugg>'?" row ────
        if (diagnostic.Suggestion is not null)
        {
            sb.Append(PadLineNum("", lineNumWidth));
            sb.Append(" = ");
            sb.Append(c_note);
            sb.Append("help");
            sb.Append(c_reset);
            sb.Append(": did you mean '");
            sb.Append(diagnostic.Suggestion);
            sb.Append("'?");
            sb.Append('\n');
        }

        // Strip the trailing newline so callers compose with explicit
        // separators (FormatDiagnostics joins with \n\n).
        if (sb.Length > 0 && sb[sb.Length - 1] == '\n')
            sb.Length--;

        return sb.ToString();
    }

    private static string LevelKeyword(DiagnosticLevel level) => level switch
    {
        DiagnosticLevel.Error => "error",
        DiagnosticLevel.Warning => "warning",
        DiagnosticLevel.Info => "info",
        _ => "diagnostic",
    };

    private static string LevelColor(DiagnosticLevel level) => level switch
    {
        DiagnosticLevel.Error => AnsiRed,
        DiagnosticLevel.Warning => AnsiYellow,
        DiagnosticLevel.Info => AnsiCyan,
        _ => "",
    };

    private static string PadLineNum(string s, int width)
    {
        // Pre-pad with one space to give the gutter a consistent left-edge.
        if (width <= 0) return " " + s;
        return " " + s.PadLeft(width);
    }

    private static string ResolvePrimaryLabel(FlowDiagnostic d)
    {
        if (d.Labels.Count == 0) return string.Empty;

        // Prefer a label whose span equals the primary span exactly —
        // matches the unknown-identifier fixture shape where the caller
        // attached `new DiagnosticLabel(primary, "not found in scope")`.
        foreach (var lbl in d.Labels)
        {
            if (lbl.Span == d.Primary)
                return lbl.Text;
        }

        // Fall back to the first label on the same line as the primary.
        foreach (var lbl in d.Labels)
        {
            if (lbl.Span.Start.Line == d.Primary.Start.Line)
                return lbl.Text;
        }

        return string.Empty;
    }

    private static string[] SplitLines(string source)
    {
        // Normalize \r\n → \n so column math doesn't drift on CRLF inputs.
        return source.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
    }
}
