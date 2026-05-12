using System;
using System.IO;
using System.Linq;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase29;

/// <summary>
/// Phase 29 two-run determinism — each of the 6 A/B fixtures must produce
/// byte-identical output across two consecutive runs at the same git SHA.
/// Extends Phase 18's <see cref="Phase18.ByteIdenticalShowcaseTests"/> /
/// ByteIdenticalTutorialTests pattern to Phase 29 fixtures.
///
/// SPEC D-31: "running the same script twice produces byte-identical output."
/// (Note: Phase 28's articulation rewrite legitimately changed pinned bytes
/// for the older tutorial / showcase scripts, but the two-run determinism
/// contract IS preserved — the dither RNG is reset to a fixed seed at every
/// writeWav, see <see cref="FlowLang.StandardLibrary.Audio.FileIO"/>.)
///
/// Pattern: read the fixture's source, substitute its hard-coded
/// examples/output/realism_ab/{instrument}_rendered.wav path for a distinct
/// tests/output/phase29_{instrument}_run{1,2}.wav path on each of two runs,
/// then byte-compare the two output files. Each run uses a fresh
/// FlowEngineRunner (so no engine-level cache carries between runs).
///
/// Serialized via <c>[Collection("FlowScripts")]</c> — same cwd-mutation
/// rationale as <see cref="AbFixtureSmokeTests"/>.
/// </summary>
[Collection("FlowScripts")]
public class Phase29ByteIdenticalTests
{
    [Theory]
    [InlineData("piano")]
    [InlineData("brass")]
    [InlineData("sax")]
    [InlineData("strings")]
    [InlineData("flute")]
    [InlineData("drums")]
    public void RealismAbFixture_TwoRunsProduceIdenticalWav(string instrument)
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string scriptPath = Path.Combine(
            repoRoot, "examples", "tests", "realism_ab", $"{instrument}.flow");
        Assert.True(File.Exists(scriptPath), $"Fixture missing at {scriptPath}");

        string source = File.ReadAllText(scriptPath);
        string outDir = Path.Combine(repoRoot, "tests", "output");
        Directory.CreateDirectory(outDir);

        string path1 = Path.Combine(outDir, $"phase29_{instrument}_run1.wav");
        string path2 = Path.Combine(outDir, $"phase29_{instrument}_run2.wav");
        if (File.Exists(path1)) File.Delete(path1);
        if (File.Exists(path2)) File.Delete(path2);

        // Substitute the fixture's writeWav target so the two runs write to
        // distinct output files. The fixtures all use the exact literal
        // "examples/output/realism_ab/{instrument}_rendered.wav".
        string defaultRel = $"examples/output/realism_ab/{instrument}_rendered.wav";
        string sourceRun1 = source.Replace(
            defaultRel, $"tests/output/phase29_{instrument}_run1.wav");
        string sourceRun2 = source.Replace(
            defaultRel, $"tests/output/phase29_{instrument}_run2.wav");
        Assert.NotEqual(source, sourceRun1);  // substitution must have happened

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;

            using (var runner1 = new FlowEngineRunner())
            {
                var r1 = runner1.RunSource(sourceRun1, $"<phase29-{instrument}-run1>");
                Assert.True(r1.Success, $"Run 1 failed: stderr={r1.Stderr}");
                Assert.Equal(0, r1.ErrorCount);
            }

            using (var runner2 = new FlowEngineRunner())
            {
                var r2 = runner2.RunSource(sourceRun2, $"<phase29-{instrument}-run2>");
                Assert.True(r2.Success, $"Run 2 failed: stderr={r2.Stderr}");
                Assert.Equal(0, r2.ErrorCount);
            }

            Assert.True(File.Exists(path1), $"output not written: {path1}");
            Assert.True(File.Exists(path2), $"output not written: {path2}");

            byte[] bytes1 = File.ReadAllBytes(path1);
            byte[] bytes2 = File.ReadAllBytes(path2);
            Assert.True(bytes1.Length > 0, $"empty output: {path1}");
            Assert.True(bytes1.SequenceEqual(bytes2),
                $"Phase 29 {instrument}.flow is not deterministic — two runs " +
                $"produced different bytes (run1: {bytes1.Length} bytes, " +
                $"run2: {bytes2.Length} bytes).");
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }
}
