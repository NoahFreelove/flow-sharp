using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-04 Wave 0 — <c>flow test [path]</c> CLI integration gate.
///
/// Spawns the actual <c>flow-cli</c> binary via <c>dotnet run --project</c> and
/// asserts:
///   1. Per-test PASS lines + a "Total: N; Passed: P; Failed: F" summary appear
///      on stdout, exit code 0, when every test in the fixture passes.
///   2. Exit code is non-zero when at least one test fails.
///
/// The CLI surface (TestCommand.cs + CommandRegistry.cs registration) lands in
/// Task 3 — these facts are RED until then.
/// </summary>
public class FlowTestCliTests
{
    private static string RepoRoot
    {
        get
        {
            // flow-lang.Tests/bin/Debug/net10.0/ → repo root is 4 levels up.
            var asmDir = Path.GetDirectoryName(typeof(FlowTestCliTests).Assembly.Location)!;
            return Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", ".."));
        }
    }

    [Fact]
    public void FlowTestRunsAllRegisteredTests()
    {
        // Write a passing-fixture .flow file to a temp directory and point
        // `flow test` at it. The fixture authored in Task 3 (tests/
        // test_test_framework.flow) is the canonical end-to-end file; this
        // fact uses an inline temp fixture to keep the Wave 0 stub self-
        // contained.
        var fixture = Path.Combine(Path.GetTempPath(),
            $"flow_test_cli_passing_{Guid.NewGuid():N}.flow");
        File.WriteAllText(fixture, """
            use "@std"
            use "@test"
            (test "one plus one" lazy((assertEq 2 (add 1 1))))
            (test "true is true" lazy((assert true)))
            """);
        try
        {
            var (exitCode, stdout, _) = RunFlowCli("test", fixture);
            Assert.Equal(0, exitCode);
            Assert.Contains("PASS", stdout);
            Assert.Contains("one plus one", stdout);
            Assert.Contains("true is true", stdout);
            Assert.Contains("Total:", stdout);
            Assert.Contains("Passed: 2", stdout);
            Assert.Contains("Failed: 0", stdout);
        }
        finally
        {
            if (File.Exists(fixture)) File.Delete(fixture);
        }
    }

    [Fact]
    public void FailingTestExitsNonZero()
    {
        var fixture = Path.Combine(Path.GetTempPath(),
            $"flow_test_cli_failing_{Guid.NewGuid():N}.flow");
        File.WriteAllText(fixture, """
            use "@std"
            use "@test"
            (test "deliberate fail" lazy((assert false)))
            """);
        try
        {
            var (exitCode, stdout, _) = RunFlowCli("test", fixture);
            Assert.NotEqual(0, exitCode);
            Assert.Contains("FAIL", stdout);
            Assert.Contains("deliberate fail", stdout);
        }
        finally
        {
            if (File.Exists(fixture)) File.Delete(fixture);
        }
    }

    private static (int exitCode, string stdout, string stderr) RunFlowCli(params string[] cliArgs)
    {
        // Use `dotnet exec` against the already-built flow.dll instead of
        // `dotnet run --project` — Wave-1 baseline measurements showed the
        // run-project path took 30-60s per test invocation (`dotnet run`
        // does a full no-op restore + build check); `dotnet exec` skips
        // all of that and launches the assembly in ~1s. The dotnet test
        // run for FlowTestCliTests now completes in well under the 120s
        // safety timeout the CLI test pre-set.
        var flowDll = Path.Combine(RepoRoot, "flow-cli", "bin", "Debug", "net10.0", "flow.dll");
        if (!File.Exists(flowDll))
        {
            throw new InvalidOperationException(
                $"flow.dll missing at {flowDll} — build flow-cli first " +
                $"(`dotnet build flow-cli/flow-cli.csproj`).");
        }

        var argv = new System.Collections.Generic.List<string> { "exec", flowDll };
        argv.AddRange(cliArgs);

        var psi = new ProcessStartInfo("dotnet", string.Join(" ", argv.ConvertAll(QuoteIfNeeded)))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepoRoot,
        };
        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        if (!proc.WaitForExit(milliseconds: 120_000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw new TimeoutException(
                $"flow CLI timed out after 120s. stdout:\n{stdout}\nstderr:\n{stderr}");
        }
        return (proc.ExitCode, stdout, stderr);
    }

    private static string QuoteIfNeeded(string s) =>
        s.Contains(' ') ? $"\"{s}\"" : s;
}
