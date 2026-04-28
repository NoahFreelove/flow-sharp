using System;
using System.IO;
using System.Linq;
using FlowLang.Tests;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase18;

/// <summary>
/// FRAC-02 byte-identical determinism gate for examples/tutorial.flow.
/// Phase 18 ships DurationFraction wiring DORMANT (per D-USER-04 — no code
/// path sets it). This Fact verifies that all .flow scripts producing
/// real WAV+MIDI output remain byte-identical across two consecutive runs.
///
/// Per D-USER-02: NO committed binary baseline. Two-runner pattern asserts
/// byte-identity across two FRESH FlowEngineRunner instances in this
/// session — mirrors EuclideanByteIdenticalTests.cs and is RESEARCH Open
/// Q 3 fallback option (b).
///
/// If this Fact goes RED after Plan 18-02 lands, Pitfall 1 has fired:
/// the GetBeats branch is silently reordering or producing different
/// double values for the existing enum path. Bisect the NoteType.cs edit.
/// </summary>
[Collection("FlowScripts")]
public class ByteIdenticalTutorialTests
{
    [Fact]
    public void Tutorial_TwoRunsProduceIdenticalWav()
    {
        RunTwiceAndCompare(isMidi: false);
    }

    [Fact]
    public void Tutorial_TwoRunsProduceIdenticalMidi()
    {
        RunTwiceAndCompare(isMidi: true);
    }

    /// <summary>
    /// Two-runner two-output-path comparison. Reads examples/tutorial.flow
    /// from disk, rewrites the writeWav/writeMidi target path per run, runs
    /// each in a FRESH FlowEngineRunner, then SequenceEqual-compares the
    /// resulting bytes.
    /// </summary>
    private static void RunTwiceAndCompare(bool isMidi)
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string scriptPath = Path.Combine(repoRoot, "examples", "tutorial.flow");
        Assert.True(File.Exists(scriptPath), $"tutorial.flow missing at {scriptPath}");

        string ext = isMidi ? "mid" : "wav";
        string baseName = "flow_tutorial";
        string outDir = Path.Combine(repoRoot, "tests", "output");
        string path1 = Path.Combine(outDir, $"phase18_tutorial_run1.{ext}");
        string path2 = Path.Combine(outDir, $"phase18_tutorial_run2.{ext}");

        if (File.Exists(path1)) File.Delete(path1);
        if (File.Exists(path2)) File.Delete(path2);
        Directory.CreateDirectory(outDir);

        string source = File.ReadAllText(scriptPath);

        // Rewrite the canonical write path to our per-run path so the two
        // runs do not race on the same output file. Tutorial writes to
        // examples/output/flow_tutorial.{wav,mid} — substitute precisely
        // those paths (verified via grep at plan time, lines 618-619).
        string defaultRel = $"examples/output/{baseName}.{ext}";
        string sourceRun1 = source.Replace(defaultRel, $"tests/output/phase18_tutorial_run1.{ext}");
        string sourceRun2 = source.Replace(defaultRel, $"tests/output/phase18_tutorial_run2.{ext}");

        // If the substitution did not actually replace anything, the script
        // does not emit this format — skip rather than false-pass.
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
