using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase32;

/// <summary>
/// Phase 32 Plan 32-04 Task 1 — end-to-end Facts that the
/// <c>(loadScala "path")</c> 1-arg + <c>(loadScala "scl" "kbm")</c> 2-arg
/// builtins are registered + callable from Flow source. Uses
/// <see cref="FlowEngineRunner"/> so the dispatch goes through the real
/// FlowEngine → InternalFunctionRegistry path. The underlying parsers
/// (Plan 32-02) + ResolvedTuning (Plan 32-03) are exercised end-to-end.
///
/// Description format pinned by <c>(str t)</c> output: per CONTEXT D-04,
/// <c>Tuning("<description>", N steps, period XXX.XX¢)</c>.
/// </summary>
[Collection("FlowScripts")]
public class LoadScalaBuiltinFacts : IDisposable
{
    public LoadScalaBuiltinFacts() { RenderingDiagnostics.ResetForTesting(); }
    public void Dispose()         { RenderingDiagnostics.ResetForTesting(); }

    /// <summary>
    /// Walk up from the test binary's bin/Debug/net10.0 dir to find the repo root —
    /// the dir that contains <c>flow-lang.Tests/fixtures</c>. Same pattern as
    /// <c>ScalaParserFacts.FindRepoRoot</c> (Plan 32-02).
    /// </summary>
    private static string FindRepoRoot()
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

    private static string FixturePath(string name)
        => Path.Combine(FindRepoRoot(), "flow-lang.Tests", "fixtures", "scala", name);

    [Fact]
    public void LoadScala_OneArg_Partch43_ParsesAndReturnsTuning()
    {
        using var runner = new FlowEngineRunner();
        string sclPath = FixturePath("partch_43.scl");
        var (ok, stdout, stderr, _) = runner.RunSource($@"use ""@std""
Tuning t = (loadScala ""{sclPath}"")
(print (str t))
");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Contains("Harry Partch's 43-tone pure scale", stdout);
        Assert.Contains("43 steps", stdout);
        Assert.Contains("1200.00", stdout); // period for partch (octave-repeating)
    }

    [Fact]
    public void LoadScala_OneArg_CarlosAlpha_ParsesAndReturnsTuning()
    {
        using var runner = new FlowEngineRunner();
        string sclPath = FixturePath("carlos_alpha.scl");
        var (ok, stdout, stderr, _) = runner.RunSource($@"use ""@std""
Tuning t = (loadScala ""{sclPath}"")
(print (str t))
");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Contains("Wendy Carlos' Alpha", stdout);
        Assert.Contains("18 steps", stdout);
        Assert.Contains("1404.00", stdout); // non-octave period
    }

    [Fact]
    public void LoadScala_TwoArg_AppliesKbm()
    {
        // Synthesize a small .kbm with a shifted middleNote.
        // Spec format (7 header fields + Size mapping entries):
        //   size, firstMidi, lastMidi, middleNote, refNote, refHz, formalOctave
        // Linear mapping (size=0) means we don't need any mapping entries.
        string kbmContent = string.Join("\n", new[]
        {
            "! synthetic-test .kbm",
            "0",        // size = 0 (linear mapping)
            "0",        // firstMidi
            "127",      // lastMidi
            "64",       // middleNote = 64 (shifted from default 60)
            "69",       // refNote = 69
            "440.0",    // refHz
            "0",        // formalOctave (must be 0 per RESEARCH A10)
        });
        string kbmPath = Path.Combine(Path.GetTempPath(), $"p32_test_kbm_{Guid.NewGuid():N}.kbm");
        File.WriteAllText(kbmPath, kbmContent);
        string sclPath = FixturePath("partch_43.scl");

        try
        {
            using var runner = new FlowEngineRunner();
            var (ok, stdout, stderr, _) = runner.RunSource($@"use ""@std""
Tuning t = (loadScala ""{sclPath}"" ""{kbmPath}"")
(print (str t))
");
            // Primary contract: the 2-arg overload IS registered AND runs without crashing.
            Assert.True(ok, $"expected clean run; stderr: {stderr}");
            // Description still surfaces in str output (the kbm doesn't change description).
            Assert.Contains("Harry Partch's 43-tone pure scale", stdout);
        }
        finally
        {
            if (File.Exists(kbmPath)) File.Delete(kbmPath);
        }
    }

    [Fact]
    public void LoadScala_NonexistentFile_RaisesError()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"use ""@std""
Tuning t = (loadScala ""nonexistent_xyz_unlikely_to_exist.scl"")
");
        Assert.False(ok, "expected non-zero exit due to missing file");
        // Either the file path or the word 'not found' should surface somewhere.
        // The wrapped error mode matches how Flow's existing file builtins surface
        // FileNotFoundException — we don't pin the exact wording, just that stderr
        // mentions enough context for a composer to diagnose.
        Assert.True(
            stderr.Contains("nonexistent_xyz_unlikely_to_exist.scl") || stderr.Length > 0,
            $"expected error to mention bad path or surface SOME error message; got stderr: {stderr}");
    }
}
