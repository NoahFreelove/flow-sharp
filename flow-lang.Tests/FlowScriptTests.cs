using Xunit;
using FlowLang.Tests.Fixtures;

namespace FlowLang.Tests;

[CollectionDefinition("FlowScripts", DisableParallelization = true)]
public class FlowScriptsCollection { }

[Collection("FlowScripts")]
public class FlowScriptTests
{
    [Theory]
    [MemberData(nameof(FlowScriptData.GetFlowScripts), MemberType = typeof(FlowScriptData))]
    public void RunsToCompletion(string relativePath)
    {
        var testsRoot = FlowScriptData.FindTestsRoot();
        var absolute = Path.Combine(testsRoot, relativePath);

        // Match the working-directory contract of `dotnet run --project flow-interpreter
        // tests/foo.flow` which runs from the repo root. Several test scripts
        // (test_wav_loading.flow, test_full_song.flow, etc.) use relative paths like
        // "tests/test_output_roundtrip.wav" that resolve against CWD.
        var origCwd = Environment.CurrentDirectory;
        Environment.CurrentDirectory = Path.GetDirectoryName(testsRoot)!;

        try
        {
            using var runner = new FlowEngineRunner();
            var (_, stdout, stderr, errorCount) = runner.RunFile(absolute);

            // Normalize path separators for dictionary lookup (Windows compatibility).
            var key = relativePath.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar);

            if (FlowScriptData.ExpectedErrorScripts.TryGetValue(key, out var expectedStderr))
            {
                Assert.Contains(expectedStderr, stderr);
            }
            else
            {
                Assert.True(errorCount == 0,
                    $"Script {relativePath} reported {errorCount} error(s):\n{stderr}");
            }

            if (FlowScriptData.RequiredSentinels.TryGetValue(key, out var sentinels))
            {
                foreach (var sentinel in sentinels)
                    Assert.Contains(sentinel, stdout);
            }
        }
        finally
        {
            Environment.CurrentDirectory = origCwd;
        }
    }
}
