using System;
using System.IO;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Sweep0614;

/// <summary>
/// sweep-0614 (gap-routing-tuning-format): <c>(loadScala "x.scl")</c> used to
/// resolve its argument CWD-relative via a bare <c>File.ReadAllText</c>, unlike
/// <c>use</c> / relative module imports which resolve relative to the script that
/// contains the statement. A composer who put <c>my.scl</c> next to their
/// <c>.flow</c> file had to run from a specific working directory.
///
/// <para>It now resolves SCRIPT-relative first (sibling-of-the-calling-file), with
/// a charitable CWD-relative fallback when no sibling exists — so absolute paths
/// and prior CWD-relative usage keep working unchanged.</para>
/// </summary>
[Collection("FlowScripts")]
public class LoadScalaScriptRelativeTests : IDisposable
{
    private string? _tempDir;

    public void Dispose()
    {
        if (_tempDir is not null && Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        }
    }

    private string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "fixtures")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not locate repo root");
    }

    [Fact]
    public void LoadScala_ResolvesRelativeToScriptNotCwd()
    {
        // Build an isolated temp dir holding a .scl and a .flow that references it
        // by a BARE relative name. The test process CWD is the test bin dir, where
        // no such .scl exists — so CWD-relative resolution would fail with a
        // FileNotFoundException. Script-relative resolution finds the sibling.
        _tempDir = Path.Combine(Path.GetTempPath(), $"flow_sweep0614_scala_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        string sclSource = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "flow-lang.Tests", "fixtures", "scala", "slendro.scl"));
        File.WriteAllText(Path.Combine(_tempDir, "my_tuning.scl"), sclSource);

        string flowPath = Path.Combine(_tempDir, "song.flow");
        File.WriteAllText(flowPath, @"use ""@std""
Tuning t = (loadScala ""my_tuning.scl"")
(print (str t))
");

        // Sanity: the bare name must NOT exist relative to the current CWD, so the
        // success below can only come from script-relative resolution.
        Assert.False(File.Exists("my_tuning.scl"),
            "test precondition: bare name must not resolve CWD-relative");

        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, _) = runner.RunFile(flowPath);

        Assert.True(ok, $"expected script-relative loadScala to succeed; stderr: {stderr}");
        Assert.Contains("steps", stdout);
    }

    [Fact]
    public void LoadScala_AbsolutePath_StillWorks()
    {
        // Absolute paths must pass through verbatim (no script-dir join).
        string sclPath = Path.Combine(
            FindRepoRoot(), "flow-lang.Tests", "fixtures", "scala", "slendro.scl");

        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, _) = runner.RunSource($@"use ""@std""
Tuning t = (loadScala ""{sclPath}"")
(print (str t))
");
        Assert.True(ok, $"expected absolute-path loadScala to succeed; stderr: {stderr}");
        Assert.Contains("steps", stdout);
    }
}
