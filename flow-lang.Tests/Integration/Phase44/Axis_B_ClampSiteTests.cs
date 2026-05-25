using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using Xunit;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-05 — Axis B input-perimeter clamp Theory.
///
/// <para>
/// Pins the 13 §6a HIGH-priority clamp sites in
/// <c>flow-lang/StandardLibrary/Transforms/TransformFunctions.cs</c>:
/// in strict mode an out-of-range arg raises <c>[strict] &lt;builtin&gt; &lt;param&gt; {value} outside [lo, hi]</c>
/// via the ErrorReporter; in non-strict mode the existing charitable
/// <c>Math.Clamp(...)</c> + fallback path is preserved byte-identical.
/// </para>
///
/// <para>
/// Theory data source is <see cref="StrictErrorManifestLoader.LoadHighPrioritySites"/>
/// filtered to TransformFunctions.cs rows; the manifest is the authoritative
/// sentinel-body source (D-07 + AUDIT §6a Column 5 composer-approved 2026-05-24).
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class Axis_B_ClampSiteTests : IDisposable
{
    public Axis_B_ClampSiteTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    /// <summary>
    /// MemberData source — 13 rows: §6a HIGH-priority TransformFunctions.cs clamp sites.
    /// Yields: filePath, line, builtin, tag, sentinelBody, priority.
    /// </summary>
    public static IEnumerable<object[]> SixAClampSites =>
        StrictErrorManifestLoader.LoadAll()
            .Where(r => !r.CarveOut
                     && r.Priority == "HIGH"
                     && r.Axis == "B"
                     && !string.IsNullOrEmpty(r.Param)
                     && r.FilePath.EndsWith("TransformFunctions.cs", StringComparison.Ordinal))
            .Select(r => new object[]
            {
                r.FilePath, r.Line, r.Builtin, r.Tag, r.SentinelBody, r.Priority,
            });

    [Theory]
    [MemberData(nameof(SixAClampSites))]
    public void Fact_StrictClampSite_ProducesVerbatimError(
        string filePath, int line, string builtin, string tag, string sentinelBody, string priority)
    {
        // Construct an out-of-range arg for this site. The manifest's sentinelBody
        // contains the verbatim shape: "[strict] <builtin> <param> {value} outside [lo, hi]".
        // We extract the range from the manifest's Range column for THIS row.
        var row = StrictErrorManifestLoader.LoadAll()
            .First(r => r.FilePath == filePath && r.Line == line);

        // Build a minimal .flow program calling the builtin with an out-of-range value.
        string flowSrc = BuildOutOfRangeStrictProgram(builtin, row.Param, row.Range);

        using var engine = new FlowEngine();
        var ok = engine.Execute(flowSrc, "<top>");

        // Strict-mode error reported via ErrorReporter (NOT thrown).
        Assert.False(ok, $"strict-mode out-of-range call expected to fail; flow:\n{flowSrc}");

        // Extract the verbatim [strict] prefix + builtin + param substring from sentinelBody.
        // sentinelBody shape: "[strict] <builtin> <param> {value} outside [lo, hi]"
        // We assert the literal prefix "[strict] <builtin> <param>" appears in some error.
        var errors = engine.ErrorReporter.FormatErrors();
        int prefixEnd = sentinelBody.IndexOf('{');
        Assert.True(prefixEnd > 0, $"sentinelBody malformed: {sentinelBody}");
        string verbatimPrefix = sentinelBody.Substring(0, prefixEnd).TrimEnd();

        Assert.Contains(verbatimPrefix, errors);

        // Also assert the verbatim "outside [..." suffix appears.
        int suffixStart = sentinelBody.IndexOf("outside", StringComparison.Ordinal);
        Assert.True(suffixStart > 0, $"sentinelBody missing 'outside': {sentinelBody}");
        string suffix = sentinelBody.Substring(suffixStart);
        Assert.Contains(suffix, errors);
    }

    [Theory]
    [MemberData(nameof(SixAClampSites))]
    public void Fact_NonStrictClampSite_NoError(
        string filePath, int line, string builtin, string tag, string sentinelBody, string priority)
    {
        // Same out-of-range call WITHOUT enable strict; — must succeed (charitable clamp).
        var row = StrictErrorManifestLoader.LoadAll()
            .First(r => r.FilePath == filePath && r.Line == line);

        string flowSrc = BuildOutOfRangeNonStrictProgram(builtin, row.Param, row.Range);

        using var engine = new FlowEngine();
        var ok = engine.Execute(flowSrc, "<top>");

        Assert.True(ok,
            $"non-strict out-of-range call MUST succeed (charitable Math.Clamp); " +
            $"flow:\n{flowSrc}\nerrors:\n{engine.ErrorReporter.FormatErrors()}");
    }

    [Fact]
    public void Fact_InRangeArgs_BothModes_NoError()
    {
        // crescendo with valid in-range velocities — succeeds in both modes.
        const string strictSrc =
            "enable strict;\n"
            + "Sequence s = | C4q D4q E4q F4q |\n"
            + "(crescendo s 0.3 0.7)\n";
        const string nonStrictSrc =
            "Sequence s = | C4q D4q E4q F4q |\n"
            + "(crescendo s 0.3 0.7)\n";

        using (var engine = new FlowEngine())
        {
            var ok = engine.Execute(strictSrc, "<top>");
            Assert.True(ok, $"strict in-range crescendo must succeed: {engine.ErrorReporter.FormatErrors()}");
        }
        using (var engine = new FlowEngine())
        {
            var ok = engine.Execute(nonStrictSrc, "<top>");
            Assert.True(ok, $"non-strict in-range crescendo must succeed: {engine.ErrorReporter.FormatErrors()}");
        }
    }

    [Fact]
    public void Fact_QuantizeBothClamps_ReportInOrder()
    {
        // Strict mode: (quantize s q 1.5 1.5) — strength out of [0,1] AND swing out of [-1,1].
        // The strict-mode early-return on the FIRST violation means we expect a strength
        // error (the FIRST clamp check). Verify the strength error is reported; swing may
        // or may not be reported depending on early-return semantics.
        const string src =
            "enable strict;\n"
            + "use \"@notation\"\n"
            + "Sequence s = | C4q D4q E4q F4q |\n"
            + "(quantize s QUARTER 1.5 1.5)\n";

        using var engine = new FlowEngine();
        var ok = engine.Execute(src, "<top>");
        Assert.False(ok, "strict-mode quantize with out-of-range args expected to fail");

        var errors = engine.ErrorReporter.FormatErrors();
        // At minimum, the strength error must appear (first clamp checked).
        Assert.Contains("[strict] quantize strength", errors);
    }

    [Fact]
    public void Fact_BackCompat_CrescendoScript_StillRuns()
    {
        // Smoke: a non-strict file using crescendo executes successfully.
        const string src =
            "Sequence s = | C4q D4q E4q F4q |\n"
            + "(crescendo s 0.2 0.9)\n"
            + "(decrescendo s 0.9 0.2)\n"
            + "(swell s 0.3 0.9)\n";

        using var engine = new FlowEngine();
        var ok = engine.Execute(src, "<top>");
        Assert.True(ok,
            $"existing transform-using script must still execute: {engine.ErrorReporter.FormatErrors()}");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Construct a minimal strict-mode .flow program that calls the given builtin
    /// with an out-of-range value for the given parameter.
    /// </summary>
    private static string BuildOutOfRangeStrictProgram(string builtin, string param, string range)
    {
        string outOfRangeArg = OutOfRangeForRange(range);
        // @notation is required for QUARTER (used by quantize); harmless for others.
        return "enable strict;\n"
            + "use \"@notation\"\n"
            + "Sequence s = | C4q D4q E4q F4q |\n"
            + BuildCallExpression(builtin, param, outOfRangeArg);
    }

    private static string BuildOutOfRangeNonStrictProgram(string builtin, string param, string range)
    {
        string outOfRangeArg = OutOfRangeForRange(range);
        return "use \"@notation\"\n"
            + "Sequence s = | C4q D4q E4q F4q |\n"
            + BuildCallExpression(builtin, param, outOfRangeArg);
    }

    /// <summary>
    /// Choose a value that's out of the manifest's range. Use the upper-bound + 1.0
    /// for double ranges (so 2.0 for [0.0, 1.0]) and upper-bound + 4 for int ranges
    /// (so 20 for [1, 16]).
    /// </summary>
    private static string OutOfRangeForRange(string range)
    {
        // Parse "[lo, hi]" — bounds may be doubles or ints.
        string trimmed = range.Trim('[', ']');
        var parts = trimmed.Split(',');
        string hiStr = parts[1].Trim();
        if (hiStr.Contains('.'))
        {
            double hi = double.Parse(hiStr, System.Globalization.CultureInfo.InvariantCulture);
            return (hi + 1.0).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        int hiInt = int.Parse(hiStr);
        return (hiInt + 4).ToString();
    }

    /// <summary>
    /// Build a Flow call expression for the given builtin + parameter index. The
    /// out-of-range value is placed at the offending parameter; other params are
    /// safe defaults. The seq variable 's' is the first arg for all 13 sites.
    /// </summary>
    private static string BuildCallExpression(string builtin, string offendingParam, string outOfRange)
    {
        return builtin switch
        {
            // (quantize s NoteValue strength swing) — strength is arg 2, swing is arg 3.
            // NoteValue must be passed as an identifier enum constant (QUARTER/EIGHTH/SIXTEENTH).
            "quantize" when offendingParam == "strength" => $"(quantize s QUARTER {outOfRange} 0.0)\n",
            "quantize" when offendingParam == "swing" => $"(quantize s QUARTER 0.5 {outOfRange})\n",
            // (crescendo s startVel endVel)
            "crescendo" when offendingParam == "startVel" => $"(crescendo s {outOfRange} 0.5)\n",
            "crescendo" when offendingParam == "endVel" => $"(crescendo s 0.5 {outOfRange})\n",
            // (decrescendo s startVel endVel)
            "decrescendo" when offendingParam == "startVel" => $"(decrescendo s {outOfRange} 0.5)\n",
            "decrescendo" when offendingParam == "endVel" => $"(decrescendo s 0.5 {outOfRange})\n",
            // (swell s edgeVel peakVel)
            "swell" when offendingParam == "edgeVel" => $"(swell s {outOfRange} 0.5)\n",
            "swell" when offendingParam == "peakVel" => $"(swell s 0.5 {outOfRange})\n",
            // (ritardando s amount)
            "ritardando" => $"(ritardando s {outOfRange})\n",
            // (accelerando s amount)
            "accelerando" => $"(accelerando s {outOfRange})\n",
            // (humanize s amount)
            "humanize" => $"(humanize s {outOfRange})\n",
            // (humanizeGaussian s amount seed)
            "humanizeGaussian" => $"(humanizeGaussian s {outOfRange} 42)\n",
            // (tremolo s reps)
            "tremolo" => $"(tremolo s {outOfRange})\n",
            _ => throw new InvalidOperationException(
                $"unknown builtin+param combo: {builtin}/{offendingParam}"),
        };
    }
}
