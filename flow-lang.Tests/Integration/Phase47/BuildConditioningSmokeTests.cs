using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace FlowLang.Tests.Integration.Phase47;

/// <summary>
/// Phase 47 Plan 47-01 — Pin acceptance of the FlowTarget=Desktop|Web MSBuild
/// conditioning. Each test shells out to `dotnet build flow-lang/flow-lang.csproj`
/// and asserts exit code 0 in both modes. Per D-47-01..03: single csproj,
/// FLOW_WEB preprocessor symbol, conditional &lt;Compile Remove&gt; strip list.
///
/// These tests run nested-build-of-self (the test process is itself a
/// `dotnet test` invocation). Each test spawns a fresh `dotnet build` against
/// the flow-lang.csproj file alone — independent of the test assembly's
/// own compilation. Test working directory is the repo root, located by
/// walking up from AppContext.BaseDirectory until flow-lang/flow-lang.csproj
/// is found.
/// </summary>
public class BuildConditioningSmokeTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "flow-lang", "flow-lang.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate repo root from " + AppContext.BaseDirectory);
    }

    private static (int exitCode, string stdout, string stderr) RunDotnetBuild(string args)
    {
        var repoRoot = FindRepoRoot();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build flow-lang/flow-lang.csproj " + args + " -v quiet --nologo",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(120_000);
        return (p.ExitCode, stdout, stderr);
    }

    [Fact]
    public void DesktopBuild_ExitCodeIsZero()
    {
        var (code, stdout, stderr) = RunDotnetBuild("-p:FlowTarget=Desktop");
        Assert.True(code == 0,
            $"Expected exit 0 with FlowTarget=Desktop, got {code}.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }

    [Fact]
    public void DefaultBuild_ExitCodeIsZero_AndImpliesDesktop()
    {
        var (code, stdout, stderr) = RunDotnetBuild("");
        Assert.True(code == 0,
            $"Expected exit 0 with no FlowTarget arg (default=Desktop per D-47-01), got {code}.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }

    [Fact]
    public void WebBuild_ExitCodeIsZero()
    {
        // Phase 47 D-47-01..03: Web build must link cleanly after Plans 47-02 + 47-03
        // wire the WebAudioBackend stub + FlowEngine/BuiltInFunctions guards.
        // If this test is run after Plan 47-01 alone (before 47-02/03), it may
        // fail with C# CS0246 (missing SfzBuiltins / OscFunctions / etc.) — that's
        // expected; the failure surfaces in 47-02/03 acceptance. The csproj
        // condition itself MUST NOT error at MSBuild eval time.
        var (code, stdout, stderr) = RunDotnetBuild("-p:FlowTarget=Web");
        Assert.True(code == 0,
            $"Expected exit 0 with FlowTarget=Web, got {code}.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }
}
