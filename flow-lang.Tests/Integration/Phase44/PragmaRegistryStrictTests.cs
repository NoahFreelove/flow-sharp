using System;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-01 Task 1 — Facts pinning the <c>strict</c> pragma's
/// presence in <see cref="PragmaRegistry.KnownPragmas"/> per D-01 / D-04,
/// the verbatim D-04 description string, and the existing Phase 21 D-12
/// levenshtein typo-recovery + pragma-position-error paths working end-to-end
/// for the new pragma name (Pitfall 5 + Pitfall 7 / W6 regression pins).
///
/// <para>
/// Collection isolation + RenderingDiagnostics.ResetForTesting mirror Phase 42
/// + 43 integration-layout convention; the Wave 0 sanity Facts in
/// <see cref="StrictErrorManifestSanityTests"/> set the ceremony precedent for
/// the Phase 44 fixture family.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class PragmaRegistryStrictTests : IDisposable
{
    /// <summary>
    /// D-04 verbatim description string. Single source of truth for this Fact
    /// + the dict entry in <c>flow-lang/Lexing/PragmaRegistry.cs</c>. If the
    /// description in either site drifts, this Fact will surface the drift.
    /// </summary>
    private const string D04Description =
        "Opt-in strict mode: no type coercion + input-perimeter clamps become errors + Bool-required for if/and/or/not + same-type required for equals/comparisons. File-scoped, no propagation via use imports.";

    public PragmaRegistryStrictTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    [Fact]
    public void Fact_StrictPragmaEntry_Exists()
    {
        Assert.True(PragmaRegistry.KnownPragmas.ContainsKey("strict"),
            "PragmaRegistry.KnownPragmas must contain 'strict' per Phase 44 D-01 / D-04.");
        Assert.True(PragmaRegistry.IsKnown("strict"),
            "PragmaRegistry.IsKnown(\"strict\") must return true per Phase 44 D-04.");
    }

    [Fact]
    public void Fact_StrictPragmaDescription_MatchesD04Verbatim()
    {
        Assert.True(PragmaRegistry.KnownPragmas.ContainsKey("strict"),
            "precondition: 'strict' entry must exist.");
        Assert.Equal(D04Description, PragmaRegistry.KnownPragmas["strict"]);
    }

    [Fact]
    public void Fact_StricTypo_LevenshteinSuggestsStrict()
    {
        // D-12 + D-04 free wiring: typing `enable stric;` should produce an
        // unknown-pragma error whose message references 'strict' as the
        // levenshtein-nearest suggestion. The PragmaScanner consults
        // PragmaRegistry.SuggestNearest which now includes 'strict' in its
        // closed-set candidate list — no PragmaScanner.cs code change needed
        // (Pitfall 5 mitigation).
        var reporter = new ErrorReporter();
        var (_, _) = PragmaScanner.Scan("enable stric;\n", "<test>", reporter);

        Assert.True(reporter.HasErrors,
            "expected unknown-pragma error for 'enable stric;'");
        var first = reporter.Errors[0];
        Assert.Contains("strict", first.Message);
        Assert.Contains("stric", first.Message);
    }

    [Fact]
    public void Fact_StrictPragmaAfterFirstStatement_TriggersPositionError()
    {
        // W6 + Pitfall 7 regression pin: `enable strict;` placed AFTER a non-
        // comment statement must trigger the Phase 21 D-11 pragma-after-statement
        // error. This Fact proves the existing PragmaScanner D-11 path handles
        // the new pragma name end-to-end (not just by registry lookup) — the
        // 'strict' name lands in the error message because the scanner uses the
        // recognized pragma name when emitting the error.
        var reporter = new ErrorReporter();
        var source = "Int x = 5;\nenable strict;\n";
        var (_, _) = PragmaScanner.Scan(source, "<test>", reporter);

        Assert.True(reporter.HasErrors,
            "expected pragma-after-statement error for 'enable strict;' on line 2.");
        var msg = reporter.Errors[0].Message;
        Assert.Contains("strict", msg);
        // D-11 wording includes "before any other statement".
        Assert.Contains("before any other statement", msg);
    }
}
