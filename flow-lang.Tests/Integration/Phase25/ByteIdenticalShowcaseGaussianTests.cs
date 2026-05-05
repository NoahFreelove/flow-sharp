using System;
using System.IO;
using System.Linq;
using FlowLang.Tests;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase25;

/// <summary>
/// Phase 25 DEFER-06 byte-identical determinism gate for examples/showcase.flow
/// post-D-20 additive humanizeGaussian call site on melody (seed=314, amount=0.08).
///
/// Mirrors Phase18/ByteIdenticalShowcaseTests.cs:1-90 verbatim — the only changes are
/// the namespace, the class name, and the run-file basenames.
///
/// The assertion is bytes1.SequenceEqual(bytes2) — two consecutive runs produce
/// identical output. Phase 18's existing showcase test continues to pass under the
/// same assertion (self-re-pinning per RESEARCH §Critical Note on Phase 18 Showcase
/// Test) — both tests assert run-to-run identity, not against a frozen byte set.
///
/// Skip-marked while Plan 25-03 is pending (showcase.flow edit lives there); Plan
/// 25-03 removes Skip after the additive humanizeGaussian wraps the melody Sequence.
/// </summary>
[Collection("FlowScripts")]
public class ByteIdenticalShowcaseGaussianTests
{
    [Fact]
    public void Showcase_TwoRunsProduceIdenticalWav()
    {
        RunTwiceAndCompare(isMidi: false);
    }

    [Fact]
    public void Showcase_TwoRunsProduceIdenticalMidi()
    {
        RunTwiceAndCompare(isMidi: true);
    }

    private static void RunTwiceAndCompare(bool isMidi)
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string scriptPath = Path.Combine(repoRoot, "examples", "showcase.flow");
        Assert.True(File.Exists(scriptPath), $"showcase.flow missing at {scriptPath}");

        string ext = isMidi ? "mid" : "wav";
        string baseName = "flow_showcase";
        string outDir = Path.Combine(repoRoot, "tests", "output");
        string path1 = Path.Combine(outDir, $"phase25_showcase_run1.{ext}");
        string path2 = Path.Combine(outDir, $"phase25_showcase_run2.{ext}");

        if (File.Exists(path1)) File.Delete(path1);
        if (File.Exists(path2)) File.Delete(path2);
        Directory.CreateDirectory(outDir);

        string source = File.ReadAllText(scriptPath);
        string defaultRel = $"examples/output/{baseName}.{ext}";
        string sourceRun1 = source.Replace(defaultRel, $"tests/output/phase25_showcase_run1.{ext}");
        string sourceRun2 = source.Replace(defaultRel, $"tests/output/phase25_showcase_run2.{ext}");

        Assert.NotEqual(source, sourceRun1);

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;

            using (var runner1 = new FlowEngineRunner())
            {
                var (success1, _, stderr1, errorCount1) = runner1.RunSource(sourceRun1);
                Assert.True(success1, $"run1 failed: stderr={stderr1}");
                Assert.Equal(0, errorCount1);
            }

            using (var runner2 = new FlowEngineRunner())
            {
                var (success2, _, stderr2, errorCount2) = runner2.RunSource(sourceRun2);
                Assert.True(success2, $"run2 failed: stderr={stderr2}");
                Assert.Equal(0, errorCount2);
            }

            Assert.True(File.Exists(path1), $"output not written: {path1}");
            Assert.True(File.Exists(path2), $"output not written: {path2}");

            byte[] bytes1 = File.ReadAllBytes(path1);
            byte[] bytes2 = File.ReadAllBytes(path2);

            Assert.True(bytes1.Length > 0, $"empty output: {path1}");
            Assert.True(bytes1.SequenceEqual(bytes2),
                $"{ext} bytes differ: run1 len={bytes1.Length}, run2 len={bytes2.Length}");
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }
}
