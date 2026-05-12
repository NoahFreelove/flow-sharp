using System;
using System.IO;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase29;

/// <summary>
/// Phase 29 REQ-7 — each of the 6 A/B fixtures renders without exception under
/// Phase 29. This is the floor test; it does NOT verify audible realism (that's
/// the composer's manual A/B sign-off at closure, Plan 07). It catches obvious
/// breakage: fixture has syntax errors, instrument not found, sample missing,
/// etc.
///
/// Each test reads the fixture's .flow source, runs it through a fresh
/// <see cref="FlowEngineRunner"/>, and asserts (a) Success + zero errors and
/// (b) the fixture wrote a non-empty WAV to examples/output/realism_ab/.
///
/// Serialized via <c>[Collection("FlowScripts")]</c> because each test mutates
/// <see cref="Environment.CurrentDirectory"/> (the fixture's writeWav path is
/// relative to cwd, and SampleCache also resolves samples relative to cwd in
/// <see cref="FlowLang.Core.FlowEngine.CurrentSampleCache"/>'s eager-load
/// path). Parallel cwd-mutating suites would corrupt path resolution.
/// </summary>
[Collection("FlowScripts")]
public class AbFixtureSmokeTests
{
    [Theory]
    [InlineData("piano")]
    [InlineData("brass")]
    [InlineData("sax")]
    [InlineData("strings")]
    [InlineData("flute")]
    [InlineData("drums")]
    public void RealismAbFixture_RendersWithoutError(string instrument)
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string scriptPath = Path.Combine(
            repoRoot, "examples", "tests", "realism_ab", $"{instrument}.flow");

        Assert.True(File.Exists(scriptPath), $"Fixture missing at {scriptPath}");

        string source = File.ReadAllText(scriptPath);

        // The fixture writes via the relative path
        // examples/output/realism_ab/{instrument}_rendered.wav, so cwd MUST be
        // the repo root. (FileIO.WriteWav auto-creates the parent directory.)
        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;

            using var runner = new FlowEngineRunner();
            var result = runner.RunSource(source, $"<smoke-{instrument}>");

            Assert.True(result.Success,
                $"Fixture {instrument}.flow failed to render. Stderr:\n{result.Stderr}");
            Assert.Equal(0, result.ErrorCount);

            // Verify output file produced and non-empty.
            string outputPath = Path.Combine(
                repoRoot, "examples", "output", "realism_ab",
                $"{instrument}_rendered.wav");
            Assert.True(File.Exists(outputPath),
                $"Fixture {instrument}.flow ran but did not produce {outputPath}");
            Assert.True(new FileInfo(outputPath).Length > 0,
                $"Output WAV is empty: {outputPath}");
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }
}
