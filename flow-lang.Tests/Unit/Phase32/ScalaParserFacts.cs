using System;
using System.IO;
using FlowLang.StandardLibrary.Audio.Tuning;
using Xunit;

namespace FlowLang.Tests.Unit.Phase32;

/// <summary>
/// Phase 32 Plan 02 Task 2 — per-fixture parse-correctness Facts for the .scl
/// parser. Each Fact loads one of the 5 canonical Huygens-Fokker fixtures committed
/// by Plan 32-01 (Wave 0) and asserts the structural invariants documented in the
/// plan's &lt;behavior&gt; block:
///   - Description (verbatim first non-comment line, trimmed)
///   - StepCents.Length (intra-period only per D-10; N-1 entries)
///   - PeriodCents (the final step in cents)
///   - Ratios dictionary (ratio inputs only per D-11)
///   - Cents precision (1e-9 on ratio-derived cents)
///
/// Additional Facts pin D-09 (negative cents accepted verbatim) and the
/// comments-only-header-tolerance charitable-parsing rule (RESEARCH A1).
///
/// Closes SPEC-3 (cents/ratio/comments/descriptions parser) acceptance for the
/// happy paths. Error-path Facts live in ScalaParserErrorFacts.
/// </summary>
public class ScalaParserFacts
{
    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "fixtures")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not locate repo root");
    }

    private static string LoadFixture(string name)
    {
        var path = Path.Combine(FindRepoRoot(), "flow-lang.Tests", "fixtures", "scala", name);
        return File.ReadAllText(path);
    }

    [Fact]
    public void Partch43_Parses_43Steps_Period2to1_RatiosPreserved()
    {
        var content = LoadFixture("partch_43.scl");
        var scl = ScalaParser.Parse(content, "partch_43.scl");

        Assert.Equal("Harry Partch's 43-tone pure scale", scl.Description);
        Assert.Equal(42, scl.StepCents.Length);
        // 2/1 → 1200.0 * log2(2) == 1200.0 exactly.
        Assert.Equal(1200.0, scl.PeriodCents, precision: 9);
        // D-11: ratio inputs preserved. Step 0 is 81/80; step 42 is 2/1.
        Assert.True(scl.Ratios.ContainsKey(0), "Ratios should contain key 0 (81/80)");
        Assert.Equal((81, 80), scl.Ratios[0]);
        Assert.True(scl.Ratios.ContainsKey(42), "Ratios should contain key 42 (period 2/1)");
        Assert.Equal((2, 1), scl.Ratios[42]);
        // Ratio-derived cents precision: 1200 * log2(81/80) ≈ 21.50628...
        Assert.Equal(1200.0 * Math.Log2(81.0 / 80.0), scl.StepCents[0], precision: 9);
    }

    [Fact]
    public void CarlosAlpha_Parses_17Intraperiod_Period1404_NonOctave()
    {
        var content = LoadFixture("carlos_alpha.scl");
        var scl = ScalaParser.Parse(content, "carlos_alpha.scl");

        Assert.Equal("Wendy Carlos' Alpha scale with perfect fifth divided in nine", scl.Description);
        // 18 step lines → 17 intra-period + period as separate field (D-10).
        Assert.Equal(17, scl.StepCents.Length);
        Assert.Equal(1404.0, scl.PeriodCents, precision: 9);
        // Cents-only file → ratio dict is empty (D-11).
        Assert.Empty(scl.Ratios);
        // First step = 78.0 cents (78.00000 in file).
        Assert.Equal(78.0, scl.StepCents[0], precision: 9);
    }

    [Fact]
    public void Slendro_Parses_4Intraperiod_PeriodFromRatio2to1()
    {
        var content = LoadFixture("slendro.scl");
        var scl = ScalaParser.Parse(content, "slendro.scl");

        Assert.StartsWith("Observed Javanese Slendro scale", scl.Description);
        Assert.Equal(4, scl.StepCents.Length);
        Assert.Equal(1200.0, scl.PeriodCents, precision: 9);
        Assert.Equal(228.0, scl.StepCents[0], precision: 9);
        // Mixed cents + final ratio: only the period (step index 4) is in Ratios.
        Assert.Single(scl.Ratios);
        Assert.Equal((2, 1), scl.Ratios[4]);
    }

    [Fact]
    public void Pythagorean12_Parses_AllRatios_Period2to1()
    {
        var content = LoadFixture("pythagorean_12.scl");
        var scl = ScalaParser.Parse(content, "pythagorean_12.scl");

        // Plan acceptance: StepCents.Length == 11 (intra-period); PeriodCents == 1200.0;
        // Ratios.Count == 12 (all 12 step lines were ratios; key 11 = period).
        Assert.Equal(11, scl.StepCents.Length);
        Assert.Equal(1200.0, scl.PeriodCents, precision: 9);
        Assert.Equal(12, scl.Ratios.Count);
        Assert.Equal((2, 1), scl.Ratios[11]);
    }

    [Fact]
    public void Just5Limit_Parses_7over5_AtStep5_Period2to1()
    {
        var content = LoadFixture("just_5limit.scl");
        var scl = ScalaParser.Parse(content, "just_5limit.scl");

        Assert.Equal(11, scl.StepCents.Length);
        Assert.Equal(1200.0, scl.PeriodCents, precision: 9);
        // Step 5 in the file is 7/5 (the 7-limit tritone).
        Assert.Equal((7, 5), scl.Ratios[5]);
        Assert.Equal((2, 1), scl.Ratios[11]);
    }

    [Fact]
    public void NegativeCents_AcceptedVerbatim_D09()
    {
        // D-09: negative cents must parse — produces ratio < 1 (descending pitch).
        // Synthetic 2-step .scl: one descending cents step, then 2/1 period.
        var content =
            "! synthetic descending\n" +
            "!\n" +
            "Synthetic descending scale\n" +
            " 2\n" +
            "!\n" +
            " -100.0\n" +
            " 2/1\n";
        var scl = ScalaParser.Parse(content, "synthetic.scl");

        Assert.Single(scl.StepCents);
        Assert.Equal(-100.0, scl.StepCents[0], precision: 9);
        Assert.Equal(1200.0, scl.PeriodCents, precision: 9);
    }

    [Fact]
    public void CommentsOnlyHeader_Tolerated_RESEARCH_A1()
    {
        // RESEARCH A1: skip leading blank/comment lines BEFORE the description.
        // First non-comment-non-blank line is the description verbatim.
        var content =
            "! header comment 1\n" +
            "! header comment 2\n" +
            "! header comment 3\n" +
            "My scale description\n" +
            " 1\n" +
            "!\n" +
            " 2/1\n";
        var scl = ScalaParser.Parse(content, "synthetic.scl");

        Assert.Equal("My scale description", scl.Description);
        Assert.Empty(scl.StepCents);  // 1 step → 0 intra-period
        Assert.Equal(1200.0, scl.PeriodCents, precision: 9);
        Assert.Equal((2, 1), scl.Ratios[0]);
    }
}
