using System;
using System.IO;
using FlowLang.Core;
using FlowLang.Diagnostics;
using Xunit;

namespace FlowLang.Tests.Integration.Phase45;

/// <summary>
/// Phase 45 Plan 45-05 — Facts pinning the pragma-aware <c>(beat N)</c>
/// constructor (D-05). The plain-Register call at BuiltInFunctions.cs:547-555
/// migrated to <c>BeatConstructorFunctions.RegisterContextDependent</c>, so the
/// constructor now reads <see cref="FlowLang.Runtime.ExecutionContext.BeatTrueToSig"/>
/// + the active <c>MusicalContext.TimeSignature</c> per call, applying the SAME
/// multiplier formula as <c>ExpressionEvaluator.EvaluateBeatLiteral</c> (Plan 45-04):
/// <c>final = pragma_on ? raw × (4.0 / denom) : raw</c>.
///
/// <list type="number">
///   <item>Constructor multiplier matrix (Tests 1-4) — mirrors the Signal 4
///         grid for the <c>(beat N)</c> path, byte-identical to the literal-form
///         multiplier matrix pinned in <see cref="BeatTrueToSigPragmaTests"/>.</item>
///   <item>DICT-01 regression (Tests 5-7) — Phase 26.1 Tuple-of-hashables Dict
///         keys (<c>&lt;&lt;C4, (beat 0.25)&gt;&gt;</c>) still round-trip through the
///         migrated constructor across all three (pragma × timesig) combinations
///         (Assumption A4 — signature dispatch preserved).</item>
/// </list>
///
/// <para>
/// Distinct test class from Plan 45-03's <c>BeatTrueToSigPragmaTests</c> to
/// preserve same-wave parallel safety with Plan 45-04. Mirrors that class's
/// <c>RunCapture</c> stdout-capture helper + tempdir-free FlowEngine.Execute
/// end-to-end style.
/// </para>
/// </summary>
[Trait("Category", Phase45TestCategory.Phase45)]
[Collection("FlowScripts")]
public class BeatConstructorTests : IDisposable
{
    public BeatConstructorTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    /// <summary>
    /// Executes <paramref name="source"/> end-to-end via a fresh
    /// <see cref="FlowEngine"/>, capturing stdout and returning it trimmed.
    /// Mirrors <c>BeatTrueToSigPragmaTests.RunCapture</c> exactly.
    /// </summary>
    private static string RunCapture(string source)
    {
        var prev = Console.Out;
        var sw = new StringWriter();
        try
        {
            Console.SetOut(sw);
            using var engine = new FlowEngine();
            var ok = engine.Execute(source, "<test>");
            Assert.True(ok, $"execute failed: {engine.ErrorReporter.FormatErrors()}");
        }
        finally
        {
            Console.SetOut(prev);
        }
        return sw.ToString().Trim();
    }

    // ===== Tests 1-4 — constructor multiplier matrix =====

    [Theory]
    [InlineData("4/4")]
    [InlineData("6/8")]
    [InlineData("2/2")]
    public void BeatConstructor_PragmaOff_Identity(string timesig)
    {
        // Pragma OFF → multiplier 1.0 in EVERY timesig. `(beat 1.0)` prints "1"
        // regardless of denominator (D-05 / D-02 mirror of the literal path).
        var src = $"timesig {timesig} {{ Beat b = (beat 1.0); (print (str b)) }}";
        Assert.Equal("1", RunCapture(src));
    }

    [Fact]
    public void BeatConstructor_PragmaOn_4Over4()
    {
        // denom=4 → multiplier 4/4 = 1.0 (identity). Pragma activation does NOT
        // corrupt 4/4 scripts (D-02 Pitfall-4 default-meter safety).
        var src = "enable beat-true-to-sig;\ntimesig 4/4 { Beat b = (beat 1.0); (print (str b)) }";
        Assert.Equal("1", RunCapture(src));
    }

    [Fact]
    public void BeatConstructor_PragmaOn_6Over8()
    {
        // denom=8 → multiplier 4/8 = 0.5. `(beat 1.0)` = half a quarter (one eighth).
        var src = "enable beat-true-to-sig;\ntimesig 6/8 { Beat b = (beat 1.0); (print (str b)) }";
        Assert.Equal("0.5", RunCapture(src));
    }

    [Fact]
    public void BeatConstructor_PragmaOn_2Over2()
    {
        // denom=2 → multiplier 4/2 = 2.0. `(beat 0.5)` = one quarter (one half / 2).
        var src = "enable beat-true-to-sig;\ntimesig 2/2 { Beat b = (beat 0.5); (print (str b)) }";
        Assert.Equal("1", RunCapture(src));
    }

    // ===== Tests 5-7 — DICT-01 Tuple-of-hashables regression =====

    [Fact]
    public void Dict01Regression_PragmaOff_4Over4()
    {
        // Phase 26.1 DICT-01: <<C4, (beat 0.25)>> is a valid Tuple<<Note, Beat>>
        // Dict key. Pragma OFF in 4/4 → multiplier 1.0 → key value 0.25; INSERT and
        // LOOKUP construct the identical internal Value.Beat(0.25), so (get) hits.
        var src =
            "timesig 4/4 {\n" +
            "  Dict<Tuple<<Note, Beat>>, Int> d = (dict <<C4, (beat 0.25)>> 100)\n" +
            "  (print (str (get d <<C4, (beat 0.25)>>)))\n" +
            "}";
        Assert.Equal("100", RunCapture(src));
    }

    [Fact]
    public void Dict01Regression_PragmaOn_4Over4()
    {
        // Pragma ON in 4/4 → multiplier 1.0 (identity). Key value identical to
        // pragma-off (0.25); round-trip still hits.
        var src =
            "enable beat-true-to-sig;\n" +
            "timesig 4/4 {\n" +
            "  Dict<Tuple<<Note, Beat>>, Int> d = (dict <<C4, (beat 0.25)>> 100)\n" +
            "  (print (str (get d <<C4, (beat 0.25)>>)))\n" +
            "}";
        Assert.Equal("100", RunCapture(src));
    }

    [Fact]
    public void Dict01Regression_PragmaOn_6Over8()
    {
        // Pragma ON in 6/8 → multiplier 0.5. BOTH the INSERT key and the LOOKUP key
        // construct <<C4, Value.Beat(0.125)>> (0.25 × 0.5) — identical internal value,
        // so the round-trip still hits (§Signal 5 RESEARCH.md row 3). Proves the
        // migrated context-dependent constructor preserves Tuple-of-hashables key
        // dispatch consistently under the pragma multiplier.
        var src =
            "enable beat-true-to-sig;\n" +
            "timesig 6/8 {\n" +
            "  Dict<Tuple<<Note, Beat>>, Int> d = (dict <<C4, (beat 0.25)>> 100)\n" +
            "  (print (str (get d <<C4, (beat 0.25)>>)))\n" +
            "}";
        Assert.Equal("100", RunCapture(src));
    }
}
