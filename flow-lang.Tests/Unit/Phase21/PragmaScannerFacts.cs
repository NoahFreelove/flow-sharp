using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase21;

/// <summary>
/// PRAG-01 acceptance Facts pinning the pre-lex PragmaScanner.Scan algorithm.
///
/// Decisions referenced (locked in 21-CONTEXT.md):
///   D-01 — Pre-scan returns (PragmaSet, transformedSource).
///   D-03 — Comments + blanks are legal anywhere in the prefix region.
///   D-04 — Pragma lines replaced with equivalent-length whitespace; line numbers align.
///   D-09 — Duplicate enable is silent (set semantics).
///   D-11 — Pragma after first non-pragma statement raises a parse error.
///   D-12 — Unknown pragma name cites alphabetized known list + did-you-mean.
///   Pitfall F — Zero-allocation fast path: when no "enable" substring is present,
///               return the SAME string reference.
///   Pitfall G — CRLF line endings preserved.
/// </summary>
public class PragmaScannerFacts
{
    private static (PragmaSet pragmas, string transformed, ErrorReporter reporter) Scan(string source)
    {
        var reporter = new ErrorReporter();
        var (pragmas, transformed) = PragmaScanner.Scan(source, fileName: null, reporter);
        return (pragmas, transformed, reporter);
    }

    [Fact]
    public void Empty_Source_ReturnsEmptyPragmasAndSource()
    {
        var (pragmas, transformed, reporter) = Scan("");
        Assert.Same(PragmaSet.Empty, pragmas);
        Assert.Equal("", transformed);
        Assert.False(reporter.HasErrors);
    }

    [Fact]
    public void NoEnableSubstring_FastPath_ReturnsOriginalReference()
    {
        // Pitfall F + zero-allocation contract — preserves byte-identical determinism for legacy .flow files.
        var source = "Int x = 5;\n(print (str x))\n";
        var (pragmas, transformed, reporter) = Scan(source);
        Assert.Same(source, transformed);   // SAME reference (zero StringBuilder allocation)
        Assert.Same(PragmaSet.Empty, pragmas);
        Assert.False(reporter.HasErrors);
    }

    [Fact]
    public void EnableHAsB_AtTop_Recognized()
    {
        var source = "enable hAsB;\nInt x = 5;";
        var (pragmas, transformed, reporter) = Scan(source);
        Assert.False(reporter.HasErrors, $"unexpected: {reporter.FormatErrors()}");
        Assert.True(pragmas.Has("hAsB"));
        Assert.Single(pragmas.Enabled);
        Assert.Single(pragmas.Sites);
        // D-04: pragma line replaced by equivalent-length whitespace, newline preserved.
        // "enable hAsB;" is 12 chars, then '\n' at index 12. Index 13 onward is "Int x = 5;".
        Assert.Equal('\n', transformed[12]);
        Assert.Equal("Int x = 5;", transformed.Substring(13));
        // Whitespace-only prefix verifies equivalent-length replacement.
        Assert.Equal(new string(' ', 12), transformed.Substring(0, 12));
    }

    [Fact]
    public void PrefixCommentsAndBlanks_Allowed()
    {
        // D-03 — comments and blank lines are legal in the prefix region.
        var source = "// header\n\nenable hAsB;\n// notes\nInt x = 5;";
        var (pragmas, transformed, reporter) = Scan(source);
        Assert.False(reporter.HasErrors, $"unexpected: {reporter.FormatErrors()}");
        Assert.True(pragmas.Has("hAsB"));
        // Verify the comments are preserved in transformed output (not stripped).
        Assert.Contains("// header", transformed);
        Assert.Contains("// notes", transformed);
        Assert.Contains("Int x = 5;", transformed);
    }

    [Fact]
    public void LineNumbersAlignAfterStrip()
    {
        // D-04 — after stripping pragma line, subsequent content stays at original offsets.
        var source = "enable hAsB;\nInt x = 5;";
        var (_, transformed, _) = Scan(source);
        Assert.Equal(source.IndexOf("Int x", System.StringComparison.Ordinal),
                     transformed.IndexOf("Int x", System.StringComparison.Ordinal));
        Assert.Equal(source.Length, transformed.Length);
    }

    [Fact]
    public void Duplicate_Silent()
    {
        // D-09 — set semantics; second declaration is silent (no error, no warning).
        var source = "enable hAsB;\nenable hAsB;\nInt x = 5;";
        var (pragmas, _, reporter) = Scan(source);
        Assert.False(reporter.HasErrors, $"unexpected: {reporter.FormatErrors()}");
        Assert.Single(pragmas.Enabled);
        Assert.True(pragmas.Has("hAsB"));
        // Both declaration sites are recorded for diagnostic provenance.
        Assert.Equal(2, pragmas.Sites.Count);
    }

    [Fact]
    public void EnableAfterStatement_RaisesError()
    {
        // D-11 — pragma after first non-pragma statement raises a parse error citing both lines.
        var source = "Int x = 5;\nenable hAsB;\n";
        var (_, _, reporter) = Scan(source);
        Assert.True(reporter.HasErrors);
        var msg = reporter.FormatErrors();
        Assert.Contains("pragmas must appear before any other statement", msg);
        Assert.Contains("Move the pragma to the top of the file", msg);
    }

    [Fact]
    public void UnknownPragma_RaisesError_WithSuggestion()
    {
        // D-12 — unknown pragma name cites alphabetized known list + did-you-mean.
        var source = "enable hasb;\n";
        var (_, _, reporter) = Scan(source);
        Assert.True(reporter.HasErrors);
        var msg = reporter.FormatErrors();
        Assert.Contains("unknown pragma 'hasb'", msg);
        Assert.Contains("Did you mean 'hAsB'?", msg);
        Assert.Contains("Known pragmas: hAsB", msg);
    }

    [Fact]
    public void CrlfLineEndings_Preserved()
    {
        // Pitfall G — \r\n line endings must be preserved verbatim after pragma strip.
        var source = "enable hAsB;\r\nInt x = 5;\r\n";
        var (pragmas, transformed, reporter) = Scan(source);
        Assert.False(reporter.HasErrors, $"unexpected: {reporter.FormatErrors()}");
        Assert.True(pragmas.Has("hAsB"));
        // Pragma line "enable hAsB;" = 12 chars; then \r at index 12, \n at index 13.
        Assert.Equal('\r', transformed[12]);
        Assert.Equal('\n', transformed[13]);
        Assert.Equal("Int x = 5;\r\n", transformed.Substring(14));
        Assert.Equal(source.Length, transformed.Length);
    }
}
