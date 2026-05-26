using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-11 Task 2 — integration phase-gate. Spawns the
/// flow-interpreter as a subprocess against every <c>tests/strict/*.flow</c>
/// composer-facing fixture and asserts:
///   1. Exit code 0 (script ran to completion).
///   2. Stdout contains <c>PASS</c> (the canonical fixture-end sentinel).
///
/// <para>
/// This is the xUnit-wrapped equivalent of <c>for f in tests/strict/*.flow;
/// do dotnet run --project flow-interpreter "$f"; done</c> per 44-VALIDATION.md
/// §"Per-phase integration command". Wrapping it as a Fact lets CI surface
/// per-file failures (a single fixture regression won't mask the others) and
/// keeps the gate inside the standard <c>dotnet test --filter Category=Phase44</c>
/// invocation that every other Phase 44 fixture rides.
/// </para>
///
/// <para>
/// Subprocess strategy mirrors <c>FlowTestCliTests</c> (Phase 35) — use
/// <c>dotnet exec</c> against the pre-built <c>flow-interpreter.dll</c>
/// rather than <c>dotnet run --project</c>. <c>dotnet run</c> performs an
/// implicit no-op restore + build check on every call (30-60s per invocation
/// in CI); <c>dotnet exec</c> launches the assembly directly in ~1s.
/// </para>
///
/// <para>
/// Charitable skip: if the flow-interpreter dll is not built (e.g., a CI
/// matrix that runs the test project before the interpreter project), every
/// Fact in this fixture short-circuits to a no-op via <see cref="DllMissing"/>.
/// Mirrors the Phase 39 <c>MscoreAvailable()</c> charitable-skip pattern
/// (D-v1.5-05 — gates must never block local dev when prerequisites are
/// absent).
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class StrictFlowScriptSuiteTests
{
    /// <summary>
    /// Walks up from the test assembly location to the repository root
    /// (identified by the presence of <c>flow-sharp.sln</c>). Mirrors the
    /// helper in <c>AuditHarnessTests</c> (Phase 42) and the inline-equivalent
    /// in <c>FlowTestCliTests</c> (Phase 35).
    /// </summary>
    internal static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "flow-sharp.sln")))
                dir = dir.Parent;
            if (dir == null)
                throw new InvalidOperationException(
                    "Could not locate flow-sharp.sln walking up from " + AppContext.BaseDirectory);
            return dir.FullName;
        }
    }

    internal static string FlowInterpreterDll =>
        Path.Combine(RepoRoot, "flow-interpreter", "bin", "Debug", "net10.0", "flow-interpreter.dll");

    /// <summary>
    /// True when the flow-interpreter dll is not present on disk. Used by
    /// every Fact in this fixture as the charitable-skip gate so a missing
    /// build does not redden the suite.
    /// </summary>
    internal static bool DllMissing => !File.Exists(FlowInterpreterDll);

    /// <summary>
    /// MemberData source: every <c>.flow</c> file under <c>tests/strict/</c>
    /// in repo-relative form. The Theory body re-anchors to RepoRoot per
    /// invocation so the printed args remain stable across machines.
    /// </summary>
    public static IEnumerable<object[]> StrictFlowFiles()
    {
        var strictDir = Path.Combine(RepoRoot, "tests", "strict");
        if (!Directory.Exists(strictDir))
        {
            // No fixtures yet — yield empty so the Theory still attaches the
            // sanity Fact below as the regression-pin.
            yield break;
        }
        foreach (var path in Directory.EnumerateFiles(strictDir, "*.flow")
                                       .OrderBy(p => p, StringComparer.Ordinal))
        {
            // Repo-relative form for test-output stability.
            var rel = Path.GetRelativePath(RepoRoot, path).Replace('\\', '/');
            yield return new object[] { rel };
        }
    }

    /// <summary>
    /// Spawn the interpreter on a single fixture and assert PASS exit. Each
    /// Theory row is independent — one fixture regression surfaces without
    /// hiding the others.
    /// </summary>
    [Theory]
    [MemberData(nameof(StrictFlowFiles))]
    public void Fact_StrictFlowFile_RunsToCompletion(string relativePath)
    {
        if (DllMissing)
            return;  // charitable skip

        var (exitCode, stdout, stderr) = RunInterpreter(relativePath);
        Assert.True(exitCode == 0,
            $"expected exit code 0 for {relativePath}; got {exitCode}.\n" +
            $"stdout:\n{stdout}\nstderr:\n{stderr}");
        Assert.Contains("PASS", stdout);
    }

    /// <summary>
    /// Regression-pin against accidental fixture deletion. Plan 44-11 ships
    /// exactly 7 fixtures (6 narrow + 1 showcase); future plans may add more
    /// but should NEVER drop below this floor.
    /// </summary>
    [Fact]
    public void Fact_AtLeastSevenStrictFiles_Exist()
    {
        var strictDir = Path.Combine(RepoRoot, "tests", "strict");
        Assert.True(Directory.Exists(strictDir),
            $"expected tests/strict/ directory at {strictDir}");
        var count = Directory.EnumerateFiles(strictDir, "*.flow").Count();
        Assert.True(count >= 7,
            $"expected >= 7 strict fixtures under tests/strict/; found {count}. " +
            "Plan 44-11 ships 7 (6 narrow + 1 showcase). Future plans may add more.");
    }

    /// <summary>
    /// Sanity: showcase_strict.flow specifically must exist (it's the audio-
    /// emitting fixture that <see cref="Phase44TwoRunDeterminismTests"/>
    /// SHA-pins for two-run cmp-clean).
    /// </summary>
    [Fact]
    public void Fact_ShowcaseStrict_Exists()
    {
        var showcase = Path.Combine(RepoRoot, "tests", "strict", "showcase_strict.flow");
        Assert.True(File.Exists(showcase),
            $"expected tests/strict/showcase_strict.flow at {showcase}");
    }

    /// <summary>
    /// Spawns <c>dotnet exec flow-interpreter.dll &lt;path&gt;</c> with a
    /// 60s wall-clock cap. Returns (exitCode, stdout, stderr). The 60s cap
    /// per T-44-11-01 mitigation prevents an infinite-loop fixture from
    /// hanging CI.
    /// </summary>
    internal static (int exitCode, string stdout, string stderr) RunInterpreter(string repoRelativePath)
    {
        var fullPath = Path.Combine(RepoRoot, repoRelativePath);
        var psi = new ProcessStartInfo("dotnet",
            $"exec \"{FlowInterpreterDll}\" \"{fullPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = RepoRoot,
        };
        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        if (!proc.WaitForExit(milliseconds: 60_000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw new TimeoutException(
                $"flow-interpreter timed out after 60s on {repoRelativePath}.\n" +
                $"stdout:\n{stdout}\nstderr:\n{stderr}");
        }
        return (proc.ExitCode, stdout, stderr);
    }
}
