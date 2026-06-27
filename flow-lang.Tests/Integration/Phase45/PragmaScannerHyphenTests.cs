using System;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Integration.Phase45;

/// <summary>
/// Phase 45 Plan 45-01 — REQ-BEAT-PRAGMA-HYPHEN-01 (closes Open Question 1 /
/// Pitfall 7). Pins that <see cref="PragmaScanner"/>'s identifier parser at
/// <c>PragmaScanner.cs:239</c> accepts hyphens in pragma names so the
/// composer-facing kebab-case form <c>enable beat-true-to-sig;</c> parses
/// cleanly. The leading-char predicate at line 238 stays unchanged (pragma
/// names still start with letter or underscore — hyphen cannot appear as
/// the first char).
///
/// <para>
/// At Wave 1 scope, the pragma name <c>beat-true-to-sig</c> is NOT yet
/// registered in <see cref="PragmaRegistry.KnownPragmas"/> — that wires
/// later in Wave 2/3 (REQ-BEAT-PRAGMA-01). So the scanner's expected
/// behavior for the hyphenated form is to:
/// <list type="number">
/// <item>RECOGNIZE the line as a well-formed pragma declaration
/// (TryMatchPragmaLine returns non-null with the full hyphenated
/// <c>Name</c>) — proves the hyphen-in-identifier gap is closed.</item>
/// <item>EMIT an unknown-pragma error citing the full hyphenated name
/// in the message — proves the error message uses the extracted name
/// (not a truncated <c>beat</c> prefix).</item>
/// </list>
/// Once Wave 2/3 registers the pragma, an additional Fact pinning
/// <c>pragmaSet.Has("beat-true-to-sig") == true</c> will be added.
/// </para>
/// </summary>
[Trait("Category", Phase45TestCategory.Phase45)]
[Collection("FlowScripts")]
public class PragmaScannerHyphenTests : IDisposable
{
    public PragmaScannerHyphenTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    [Fact]
    public void Fact_PragmaScanner_AcceptsHyphenatedName_BeatTrueToSig()
    {
        // REQ-BEAT-PRAGMA-HYPHEN-01: the identifier parser at PragmaScanner.cs:239
        // must accept hyphens in CONTINUATION position. Wave 1 closed the
        // scanner gap; Wave 2 (Plan 45-03 D-03) REGISTERED the pragma in
        // PragmaRegistry — so `enable beat-true-to-sig;` now scans cleanly with
        // NO error and the FULL hyphenated name lands in the PragmaSet. (This
        // Fact originally asserted an unknown-pragma error pre-registration; it
        // is updated to the post-registration reality per Plan 45-04 — the
        // scanner-accepts-hyphens invariant is now proven via successful
        // recognition rather than via a full-name unknown-pragma message.)
        var reporter = new ErrorReporter();
        var (pragmas, _) = PragmaScanner.Scan("enable beat-true-to-sig;\n", "<test>", reporter);

        Assert.False(reporter.HasErrors,
            "beat-true-to-sig is registered (Plan 45-03 D-03) — the scanner must " +
            "extract the full hyphenated name and recognize it with NO error. " +
            "A truncated 'beat' prefix would fail registry lookup and error here, " +
            "so a clean parse proves the hyphen identifier scan worked: " +
            reporter.FormatErrors());
        Assert.True(pragmas.Has("beat-true-to-sig"),
            "the PragmaSet must contain the FULL hyphenated name 'beat-true-to-sig' " +
            "(not a truncated 'beat'), proving the scanner's identifier parser " +
            "accepted the hyphens in continuation position.");
    }

    [Fact]
    public void Fact_PragmaScanner_AcceptsTypoHyphenatedName_BeaTrueToSig()
    {
        // Verify a typo with hyphens (`bea-true-to-sig` — missing trailing 't' in
        // 'beat') is recognized as a hyphenated pragma name. Wave 3 will pin
        // the Levenshtein advisory; here we only confirm the SCAN extracts the
        // full hyphenated typo so it flows downstream to PragmaRegistry
        // resolution as one name (not three).
        var reporter = new ErrorReporter();
        var (_, _) = PragmaScanner.Scan("enable bea-true-to-sig;\n", "<test>", reporter);

        Assert.True(reporter.HasErrors,
            "expected unknown-pragma error for typo 'bea-true-to-sig'.");
        var msg = reporter.Errors[0].Message;
        Assert.Contains("bea-true-to-sig", msg);
    }

    [Fact]
    public void Fact_PragmaScanner_NoHyphen_StrictPragmaUnchanged()
    {
        // Regression pin: closing the hyphen gap must NOT perturb existing
        // no-hyphen pragma names. `enable strict;` (Phase 44) continues to
        // parse identically — recognized + no error.
        var reporter = new ErrorReporter();
        var (pragmas, transformedSource) = PragmaScanner.Scan(
            "enable strict;\n", "<test>", reporter);

        Assert.False(reporter.HasErrors,
            "enable strict; (Phase 44) must continue to parse cleanly with no error.");
        Assert.True(pragmas.Has("strict"),
            "PragmaSet must contain 'strict' after Phase 44 D-04 wiring.");
        // The line should be whitespace-stripped per D-04 (preserves column alignment).
        Assert.Contains("              \n", transformedSource);
    }

    [Fact]
    public void Fact_PragmaScanner_HyphenAtStart_StillRejected()
    {
        // T-45-01 threat mitigation: the leading-char predicate at
        // PragmaScanner.cs:238 stays unchanged — hyphen cannot appear as
        // the first char of the identifier. A line like `enable -foo;`
        // must NOT lex as a pragma; it should fall through to the
        // SimpleLexer for regular error handling (the line is copied
        // verbatim into the transformed source per the else-branch in
        // PragmaScanner.Scan).
        var reporter = new ErrorReporter();
        var (pragmas, transformedSource) = PragmaScanner.Scan(
            "enable -foo;\n", "<test>", reporter);

        // The line is NOT a valid pragma → TryMatchPragmaLine returns null →
        // PragmaScanner does not emit a pragma error (the lexer/parser will
        // surface the issue downstream). Critically: the line text passes
        // through verbatim (not whitespace-stripped).
        Assert.False(pragmas.Has("foo"),
            "leading hyphen must NOT produce a recognized pragma.");
        Assert.False(pragmas.Has("-foo"),
            "leading hyphen must NOT produce a recognized pragma even with the hyphen.");
        Assert.Contains("enable -foo;", transformedSource);
    }
}
