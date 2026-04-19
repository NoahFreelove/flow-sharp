using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase09;

/// <summary>
/// QOL-02 regression test: examples/tutorial.flow runs to completion without
/// interpreter errors post-Phase-12 stability fixes. The tutorial is NOT
/// covered by the Theory harness — FlowScriptData.GetFlowScripts globs
/// tests/ only (FlowScriptData.cs:8), not examples/. This Fact pins exit-
/// code-zero + errorCount==0 for the tutorial as it stands in HEAD;
/// tutorial pedagogical refresh is tracked separately under QOL-03 (Phase 16).
///
/// CWD pivot mirrors FlowScriptTests.cs:19-24 in case the tutorial writes
/// with a relative path; HEAD writes to /tmp/flow_tutorial_output.wav
/// (absolute) so the pivot is defensive.
/// </summary>
[Collection("FlowScripts")]
public class TutorialTests
{
    [Fact]
    public void TutorialRunsToCompletion()
    {
        var testsRoot = FlowScriptData.FindTestsRoot();
        var repoRoot = Path.GetDirectoryName(testsRoot)!;
        var tutorialPath = Path.Combine(repoRoot, "examples", "tutorial.flow");

        var origCwd = Environment.CurrentDirectory;
        Environment.CurrentDirectory = repoRoot;
        try
        {
            using var runner = new FlowEngineRunner();
            var (ok, _, stderr, errorCount) = runner.RunFile(tutorialPath);
            Assert.True(ok, $"tutorial errored: {stderr}");
            Assert.Equal(0, errorCount);
        }
        finally
        {
            Environment.CurrentDirectory = origCwd;
        }
    }
}
