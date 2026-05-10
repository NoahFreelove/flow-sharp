using System;
using System.IO;
using System.Linq;
using FlowLang.Tests;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase27;

/// <summary>
/// Phase 27 D-403 byte-identical determinism gate for examples/pragmas/h_alias.flow
/// + examples/pragmas/microtonal_ji.flow companion files.
///
/// Mirrors Phase18/ByteIdenticalShowcaseTests.cs:1-90 verbatim — the only changes are
/// the namespace, the class name, the script paths (examples/pragmas/{baseName}.flow),
/// and the run-file basenames (phase27_{baseName}_run1.{ext}). The helper takes
/// `baseName` as a parameter so the same body services both companion files via
/// 4 facts (2 files × 2 extensions).
///
/// CRITICAL: this test class uses two-run SequenceEqual, NOT inline byte[] pin literals.
/// Pin-bytes are reserved for compact MIDI velocity sequences in Phase15/EuclideanByteIdenticalTests.
/// See Phase 27 RESEARCH.md Pitfall 1 — CONTEXT D-204 wording is misleading; the actual
/// closure work for D-204 is "verify Phase 18/25 stay GREEN," NOT "encode hex literals."
/// </summary>
[Collection("FlowScripts")]
public class Phase27ByteIdenticalPragmaTests
{
    [Fact]
    public void HAlias_TwoRunsProduceIdenticalWav()
    {
        RunTwiceAndCompare("h_alias", isMidi: false);
    }

    [Fact]
    public void HAlias_TwoRunsProduceIdenticalMidi()
    {
        RunTwiceAndCompare("h_alias", isMidi: true);
    }

    [Fact]
    public void MicrotonalJi_TwoRunsProduceIdenticalWav()
    {
        RunTwiceAndCompare("microtonal_ji", isMidi: false);
    }

    [Fact]
    public void MicrotonalJi_TwoRunsProduceIdenticalMidi()
    {
        RunTwiceAndCompare("microtonal_ji", isMidi: true);
    }

    private static void RunTwiceAndCompare(string baseName, bool isMidi)
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string scriptPath = Path.Combine(repoRoot, "examples", "pragmas", $"{baseName}.flow");
        Assert.True(File.Exists(scriptPath), $"{baseName}.flow missing at {scriptPath}");

        string ext = isMidi ? "mid" : "wav";
        string outDir = Path.Combine(repoRoot, "tests", "output");
        string path1 = Path.Combine(outDir, $"phase27_{baseName}_run1.{ext}");
        string path2 = Path.Combine(outDir, $"phase27_{baseName}_run2.{ext}");

        if (File.Exists(path1)) File.Delete(path1);
        if (File.Exists(path2)) File.Delete(path2);
        Directory.CreateDirectory(outDir);

        string source = File.ReadAllText(scriptPath);
        string defaultRel = $"examples/output/{baseName}.{ext}";
        string sourceRun1 = source.Replace(defaultRel, $"tests/output/phase27_{baseName}_run1.{ext}");
        string sourceRun2 = source.Replace(defaultRel, $"tests/output/phase27_{baseName}_run2.{ext}");

        Assert.NotEqual(source, sourceRun1); // substitution must have actually replaced

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
