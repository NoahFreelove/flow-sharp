using System;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase32;

/// <summary>
/// Phase 32 Plan 32-07 (D-19 — composer-facing tutorial chapter) CI gate.
/// Pins <c>examples/scala/intro.flow</c> as a runnable artifact so future
/// changes that break the tutorial surface (lexer keyword removal, builtin
/// signature drift, parser regression, fixture move) are caught immediately.
///
/// Two Facts:
///   • <see cref="IntroScript_RunsToCompletion_ProducesWav"/> — the tutorial
///     executes via FlowEngineRunner with ok==true and writes a non-empty
///     WAV to <c>/tmp/p32_intro.wav</c> (the path the tutorial hard-codes).
///   • <see cref="IntroScript_TwoRuns_ProducesByteIdenticalWav"/> — two
///     consecutive runs of the tutorial produce byte-identical WAVs,
///     extending the SPEC-6 two-run determinism contract from the per-fixture
///     ScalaTuningDeterminismTests to the composer-facing tutorial path.
///
/// Cwd handling: the tutorial
/// references the .scl fixtures via the relative path
/// <c>flow-lang.Tests/fixtures/scala/partch_43.scl</c>, which resolves only
/// when the process's cwd is the repo root. The test sets cwd to the repo
/// root for the duration of the run and restores it in a finally block.
/// </summary>
[Collection("FlowScripts")]
public class TutorialScriptTests : IDisposable
{
    private const string OutputWavPath = "/tmp/p32_intro.wav";

    public TutorialScriptTests()
    {
        RenderingDiagnostics.ResetForTesting();
        if (File.Exists(OutputWavPath)) File.Delete(OutputWavPath);
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        if (File.Exists(OutputWavPath)) File.Delete(OutputWavPath);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            // Anchor: the in-repo Scala fixtures directory must exist next to
            // the examples/ tree; mirrors LastWinsTuningTests' anchor and is
            // robust against future top-level file additions.
            if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "fixtures", "scala"))
                && Directory.Exists(Path.Combine(dir, "examples", "scala")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not locate repo root (looking for flow-lang.Tests/fixtures/scala + examples/scala)");
    }

    private static string IntroScriptPath() =>
        Path.Combine(FindRepoRoot(), "examples", "scala", "intro.flow");

    /// <summary>
    /// Runs <c>examples/scala/intro.flow</c> once with cwd = repo root and
    /// returns the path to the produced WAV. Asserts ok==true + WAV exists
    /// + WAV is non-empty. Shared helper for both Facts.
    /// </summary>
    private static string RunTutorialOnce()
    {
        string scriptPath = IntroScriptPath();
        Assert.True(File.Exists(scriptPath), $"tutorial missing: {scriptPath}");

        if (File.Exists(OutputWavPath)) File.Delete(OutputWavPath);

        string oldCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = FindRepoRoot();
            using var runner = new FlowEngineRunner();
            var (ok, _, stderr, errorCount) = runner.RunFile(scriptPath);
            Assert.True(
                ok && errorCount == 0,
                $"intro.flow failed: errorCount={errorCount}\nstderr:\n{stderr}");
        }
        finally
        {
            Environment.CurrentDirectory = oldCwd;
        }

        Assert.True(File.Exists(OutputWavPath), $"writeWav did not produce {OutputWavPath}");
        // Guard against header-only / empty buffer regressions: a real
        // 4-section sine render under any tuning produces hundreds of KB.
        // Threshold 1 KB is the spec-required floor; the actual file is
        // measured at ~1.4 MB at plan-author time.
        Assert.True(
            new FileInfo(OutputWavPath).Length > 1024,
            $"{OutputWavPath} is suspiciously small ({new FileInfo(OutputWavPath).Length} bytes)");
        return OutputWavPath;
    }

    [Fact]
    public void IntroScript_RunsToCompletion_ProducesWav()
    {
        // VALIDATION.md W7 — composer-facing tutorial chapter test gate.
        // The tutorial demonstrates all 3 D-15 surface forms + last-wins
        // pragma+block interaction; this Fact asserts the entire chapter
        // still runs end-to-end and produces audible output.
        var wavPath = RunTutorialOnce();
        Assert.True(File.Exists(wavPath));
    }

    [Fact]
    public void IntroScript_TwoRuns_ProducesByteIdenticalWav()
    {
        // Two-run determinism — the SPEC-6 contract extended to the
        // composer-facing tutorial path. Catches any tutorial-specific
        // non-determinism not covered by Plan 32-06 ScalaTuningDeterminismTests
        // (e.g. a regression in how multi-section Songs with mixed tunings
        // serialize their voice-pool steal-oldest ordering).
        var wavPath1 = RunTutorialOnce();
        byte[] bytes1 = File.ReadAllBytes(wavPath1);

        RenderingDiagnostics.ResetForTesting();
        var wavPath2 = RunTutorialOnce();
        byte[] bytes2 = File.ReadAllBytes(wavPath2);

        Assert.True(
            bytes1.SequenceEqual(bytes2),
            $"two runs of examples/scala/intro.flow produced different WAV bytes (lens {bytes1.Length} vs {bytes2.Length})");
    }
}
