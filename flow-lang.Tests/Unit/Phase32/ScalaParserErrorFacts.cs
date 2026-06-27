using System;
using System.IO;
using System.Text.RegularExpressions;
using FlowLang.StandardLibrary.Audio.Tuning;
using Xunit;

namespace FlowLang.Tests.Unit.Phase32;

/// <summary>
/// Phase 32 Plan 02 Task 2 — negative-case Facts pinning the .scl parser's
/// {file}:{line}:{col} — expected X, got 'Y' diagnostic format. Closes SPEC-7
/// (clear error semantics) acceptance.
///
/// Two malformed fixtures (committed by Plan 32-01) + three D-18 strict-reject
/// Facts (whitespace-around-slash ratios, scientific-notation cents, and
/// comma-decimal cents).
/// </summary>
public class ScalaParserErrorFacts
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
    public void MalformedStepCount_ProducesLineCol_NegativeIntegerRejected()
    {
        var content = LoadFixture("malformed_step_count.scl");
        var ex = Assert.Throws<ScalaParseException>(
            () => ScalaParser.Parse(content, "malformed_step_count.scl"));

        // Plan 32-01's fixture places `-5` at line 4 col 1.
        Assert.Equal(4, ex.Line);
        Assert.Equal(1, ex.Column);
        Assert.Contains("step count", ex.Expected);
        Assert.Equal("-5", ex.Found);
        Assert.Matches(
            new Regex(@"malformed_step_count\.scl:\d+:1 — expected step count \(positive integer\), got '-5'"),
            ex.Message);
    }

    [Fact]
    public void MalformedCents_ProducesLineCol_NonNumericTokenRejected()
    {
        var content = LoadFixture("malformed_cents.scl");
        var ex = Assert.Throws<ScalaParseException>(
            () => ScalaParser.Parse(content, "malformed_cents.scl"));

        // Plan 32-01's fixture places `foo` at line 7 col 1.
        Assert.Equal(7, ex.Line);
        Assert.Equal(1, ex.Column);
        Assert.Equal("foo", ex.Found);
        Assert.Matches(
            new Regex(@"malformed_cents\.scl:\d+:1 — expected cents value or ratio, got 'foo'"),
            ex.Message);
    }

    [Fact]
    public void D18_WhitespaceAroundSlashRatio_Rejected()
    {
        // D-18: `3 / 2` is silent in the spec; Phase 32 rejects strictly.
        // The parser must NOT split on whitespace before the slash; the FIRST
        // whitespace token `3` has no slash, so reject as missing slash on a
        // bare integer that the line nevertheless declares as a ratio attempt.
        var content =
            "!\n" +
            "!\n" +
            "Synthetic\n" +
            " 1\n" +
            "!\n" +
            " 3 / 2\n";
        var ex = Assert.Throws<ScalaParseException>(
            () => ScalaParser.Parse(content, "synthetic.scl"));
        Assert.Contains("cents value or ratio", ex.Expected);
    }

    [Fact]
    public void D18_ScientificNotationCents_Rejected()
    {
        // D-18: 1.5e2 in cents value silent in spec; Phase 32 strict-rejects.
        var content =
            "!\n" +
            "Synthetic\n" +
            " 1\n" +
            " 1.5e2\n";
        var ex = Assert.Throws<ScalaParseException>(
            () => ScalaParser.Parse(content, "synthetic.scl"));
        Assert.Contains("cents value or ratio", ex.Expected);
        Assert.Equal("1.5e2", ex.Found);
    }

    [Fact]
    public void D18_CommaDecimalCents_Rejected()
    {
        // D-18: 100,5 (comma-decimal) silent in spec; Phase 32 strict-rejects
        // because parser uses CultureInfo.InvariantCulture exclusively (Pitfall 8
        // determinism guard).
        var content =
            "!\n" +
            "Synthetic\n" +
            " 1\n" +
            " 100,5\n";
        var ex = Assert.Throws<ScalaParseException>(
            () => ScalaParser.Parse(content, "synthetic.scl"));
        Assert.Contains("cents value or ratio", ex.Expected);
        Assert.Equal("100,5", ex.Found);
    }
}
