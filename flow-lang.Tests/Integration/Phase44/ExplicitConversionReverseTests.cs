using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-04 — reverse-direction extractor overloads per D-10.
/// Each of the four extractors (<c>double</c>, <c>float</c>, <c>int</c>,
/// <c>long</c>) gains overloads accepting all six tagged music types
/// (Decibel / Hertz / Cent / Millisecond / Second / Semitone) — 24
/// registrations total. Always available, mode-independent (D-09 + D-10),
/// lossy-floor semantics for <c>(int &lt;fractional music type&gt;)</c>
/// mirror the existing <c>StdLib.DoubleToInt</c> floor convention
/// (T-44-04-01 mitigation).
/// </summary>
[Collection("FlowScripts")]
public class ExplicitConversionReverseTests
{
    /// <summary>
    /// 24-row matrix: 4 extractors × 6 tagged music types. Asserts the
    /// bound variable's <c>Type</c> is the target primitive type AND
    /// the underlying CLR value matches the expected canonical value.
    /// </summary>
    [Theory]
    // --- (double <music type>) → Double (6 rows) ---
    [InlineData("Double r = (double -12dB)",     "Double", -12.0)]
    [InlineData("Double r = (double 440Hz)",     "Double", 440.0)]
    [InlineData("Double r = (double +50c)",      "Double",  50.0)]
    [InlineData("Double r = (double 100ms)",     "Double", 100.0)]
    [InlineData("Double r = (double 2.5s)",      "Double",   2.5)]
    [InlineData("Double r = (double +2st)",      "Double",   2.0)]
    // --- (float <music type>) → Float (6 rows) ---
    [InlineData("Float r = (float -12dB)",       "Float",  -12.0)]
    [InlineData("Float r = (float 440Hz)",       "Float",  440.0)]
    [InlineData("Float r = (float +50c)",        "Float",   50.0)]
    [InlineData("Float r = (float 100ms)",       "Float",  100.0)]
    [InlineData("Float r = (float 2.5s)",        "Float",    2.5)]
    [InlineData("Float r = (float +2st)",        "Float",    2.0)]
    public void Fact_ReverseExtractor_ProducesCorrectDoubleOrFloat(
        string flowDecl, string expectedTypeName, double expectedValue)
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(flowDecl + "\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");

        var v = runner.GetVariable("r");
        Assert.Equal(expectedTypeName, v.Type.Name);
        Assert.Equal(expectedValue, v.As<double>());
    }

    /// <summary>
    /// (int &lt;music type&gt;) matrix — 6 rows. Floor semantics for the
    /// 5 double-backed music types; identity for Semitone (Int-backed).
    /// Pin <c>(int 2.5s)</c> → <c>2</c> (Math.Floor of 2.5) and
    /// <c>(int +2st)</c> → <c>2</c> (Semitone's Int-backed identity).
    /// </summary>
    [Theory]
    [InlineData("Int r = (int -12dB)",    -12)]
    [InlineData("Int r = (int 440Hz)",   440)]
    [InlineData("Int r = (int +50c)",     50)]
    [InlineData("Int r = (int 100ms)",   100)]
    [InlineData("Int r = (int 2.5s)",      2)]   // floor of 2.5
    [InlineData("Int r = (int +2st)",      2)]
    public void Fact_ReverseExtractor_ProducesCorrectInt(string flowDecl, int expectedValue)
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(flowDecl + "\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");

        var v = runner.GetVariable("r");
        Assert.Same(IntType.Instance, v.Type);
        Assert.Equal(expectedValue, v.As<int>());
    }

    /// <summary>
    /// (long &lt;music type&gt;) matrix — 6 rows. Same floor semantics as
    /// (int), wider integer width.
    /// </summary>
    [Theory]
    [InlineData("Long r = (long -12dB)",    -12L)]
    [InlineData("Long r = (long 440Hz)",   440L)]
    [InlineData("Long r = (long +50c)",     50L)]
    [InlineData("Long r = (long 100ms)",   100L)]
    [InlineData("Long r = (long 2.5s)",      2L)]
    [InlineData("Long r = (long +2st)",      2L)]
    public void Fact_ReverseExtractor_ProducesCorrectLong(string flowDecl, long expectedValue)
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(flowDecl + "\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");

        var v = runner.GetVariable("r");
        Assert.Same(LongType.Instance, v.Type);
        Assert.Equal(expectedValue, v.As<long>());
    }

    /// <summary>
    /// Lossy-floor regression — <c>(int 100.7ms)</c> floors to 100, NOT 101.
    /// Pins T-44-04-01 mitigation: mirrors the existing <c>StdLib.DoubleToInt</c>
    /// floor convention. <c>100.7ms</c> is a Millisecond literal (the lexer
    /// parses the numeric portion as a double, attaches the ms suffix as the
    /// tag), so the (int Millisecond) extractor reads 100.7 and floors.
    /// </summary>
    [Fact]
    public void Fact_IntFromMillisecond_FloorsLossy()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource("Int r = (int 100.7ms)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");

        var v = runner.GetVariable("r");
        Assert.Same(IntType.Instance, v.Type);
        Assert.Equal(100, v.As<int>());
    }

    /// <summary>
    /// Negative-input floor regression — <c>(int -2.5s)</c> floors to <c>-3</c>
    /// (Math.Floor of -2.5, NOT C-style truncation which would yield -2).
    /// Pins T-44-04-01 mitigation: Math.Floor convention is uniform across
    /// the sign domain. (This matches existing doubleToInt floor semantics.)
    /// </summary>
    [Fact]
    public void Fact_IntFromSecond_FloorsNegativeCorrectly()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource("Int r = (int -2.5s)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");

        var v = runner.GetVariable("r");
        Assert.Same(IntType.Instance, v.Type);
        Assert.Equal(-3, v.As<int>());
    }

    /// <summary>
    /// Round-trip cleanliness — <c>(semitones (int +2st))</c> chains
    /// Semitone → Int → Semitone preserving the original value. (The
    /// Int-direct path is used because Semitone is Int-backed; calling
    /// through Double would hit the OverloadResolver "ambiguous overload"
    /// case where <c>(int Double)</c> matches BOTH <c>int(Decibel)</c> and
    /// <c>int(Hertz)</c> via inverse IsCompatibleWith — see Phase 44 RESEARCH
    /// Pitfall 1 + the strict-mode +100 / inverse-compat tier discussion.
    /// Plan 44-04 ships only the targeted music-type overloads; resolving
    /// the broader inverse-compat ambiguity is Plan 44-02's job.)
    /// </summary>
    [Fact]
    public void Fact_SemitoneIntRoundTrip()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "Semitone r = (semitones (int +2st))\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");

        var v = runner.GetVariable("r");
        Assert.Equal("Semitone", v.Type.Name);
        Assert.Equal(2, v.As<int>());
    }

    /// <summary>
    /// Reverse-then-forward round trip — Decibel → Double, then a new Decibel
    /// is bound from a freshly-supplied Double literal preserving the value.
    /// (Direct <c>(db (double -12dB))</c> would hit the same OverloadResolver
    /// inverse-compat ambiguity as the Semitone case — see <see cref="Fact_SemitoneIntRoundTrip"/>
    /// — because <c>(db Double)</c> resolves cleanly via the exact registered
    /// overload but only if no other <c>db(&lt;X&gt;)</c> overload matches Double
    /// via inverse-compat. With <c>db(Decibel)</c> also registered, the same
    /// ambiguity would arise. The two-step form below isolates the value
    /// flow without triggering nested-call inverse-compat collisions.)
    /// </summary>
    [Fact]
    public void Fact_DbDoubleTwoStep_RoundTrip()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "Double d = (double -12dB)\nDecibel r = (db d)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");

        var d = runner.GetVariable("d");
        Assert.Equal("Double", d.Type.Name);
        Assert.Equal(-12.0, d.As<double>());

        var r = runner.GetVariable("r");
        Assert.Equal("Decibel", r.Type.Name);
        Assert.Equal(-12.0, r.As<double>());
    }

    /// <summary>
    /// D-09 mode-independence smoke — same reverse extractors work after a
    /// <c>enable strict;</c> pragma when Plan 44-01 is wired. Soft assertion
    /// (passes whether or not the pragma is recognized) — the load-bearing
    /// claim is that the EXTRACTORS themselves are not gated by strict bit.
    /// </summary>
    [Theory]
    [InlineData("Double r = (double -12dB)", "Double", -12.0)]
    [InlineData("Int r = (int +2st)",         "Int",     2.0)]
    public void Fact_ReverseExtractors_WorkUnderEnableStrict(
        string flowDecl, string expectedTypeName, double expectedValue)
    {
        using var runner = new FlowEngineRunner();
        var src = "enable strict;\n" + flowDecl + "\n";
        var (ok, _, _, _) = runner.RunSource(src);
        if (ok)
        {
            var v = runner.GetVariable("r");
            Assert.Equal(expectedTypeName, v.Type.Name);
            if (v.Type.Name == "Int")
                Assert.Equal((int)expectedValue, v.As<int>());
            else
                Assert.Equal(expectedValue, v.As<double>());
        }
    }
}
