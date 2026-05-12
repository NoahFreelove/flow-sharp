using System;
using System.IO;
using System.Linq;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Helpers;
using Melanchall.DryWetMidi.Core;
using Xunit;

namespace FlowLang.Tests.Integration.Phase28;

/// <summary>
/// Phase 28 (SPEC-9) Plan 07 acceptance facts pinning the two ragtime
/// fixtures end-to-end:
///
///   • <see cref="Ragtime_SyntheticFixture_Renders"/> + Maple Leaf —
///     each runs to exit 0 and produces non-empty WAV + MID outputs.
///   • <see cref="Ragtime_Synthetic_RmsRegression"/> + Maple Leaf —
///     rendered WAV matches committed baseline within ±0.5 dB / 100 ms
///     (SPEC-8 default tolerance).
///   • <see cref="Ragtime_Synthetic_MultiTrackMidi"/> — generated .mid
///     has at least 2 chunks (conductor + ≥ 1 sequence track).
///   • <see cref="Ragtime_TwoRunDeterminism"/> — synthetic fixture
///     produces byte-identical .wav AND .mid across two consecutive
///     runs (preserves Phase 18/25/27 determinism contract).
///
/// Tests run inside the existing "FlowScripts" Collection so FileIO's
/// dither RNG doesn't race with parallel-class WAV writes (same
/// rationale as Plan 28-06's RmsRegressionDiagnosticTests).
/// </summary>
[Collection("FlowScripts")]
public class RagtimeFixtureTests
{
    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "examples", "tests")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not locate repo root");
    }

    private static string BaselinesDir => Path.Combine(FindRepoRoot(),
        "flow-lang.Tests", "baselines", "Phase28");

    /// <summary>
    /// Runs the named fixture from <c>examples/tests/</c>. Assumes the
    /// fixture writes to a fixed <c>examples/output/{name}.wav|mid</c>.
    /// Returns the full paths to those outputs after running. The fixtures
    /// reference relative paths that resolve against the process's
    /// current working directory, so the test sets cwd to the repo root
    /// for the duration of the run.
    /// </summary>
    private static (string WavPath, string MidPath) RunFixture(string name)
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "examples", "tests", $"{name}.flow");
        Assert.True(File.Exists(scriptPath), $"Fixture missing: {scriptPath}");

        string outDir = Path.Combine(repoRoot, "examples", "output");
        Directory.CreateDirectory(outDir);
        string wavPath = Path.Combine(outDir, $"{name}.wav");
        string midPath = Path.Combine(outDir, $"{name}.mid");
        if (File.Exists(wavPath)) File.Delete(wavPath);
        if (File.Exists(midPath)) File.Delete(midPath);

        string source = File.ReadAllText(scriptPath);
        string oldCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;
            using var runner = new FlowEngineRunner();
            var (success, _, stderr, errorCount) = runner.RunSource(source, scriptPath);
            Assert.True(success && errorCount == 0,
                $"{name} failed: errorCount={errorCount}\nstderr:\n{stderr}");
        }
        finally { Environment.CurrentDirectory = oldCwd; }

        Assert.True(File.Exists(wavPath), $"writeWav did not produce {wavPath}");
        Assert.True(File.Exists(midPath), $"writeMidi did not produce {midPath}");
        Assert.True(new FileInfo(wavPath).Length > 0, $"{wavPath} is empty");
        Assert.True(new FileInfo(midPath).Length > 0, $"{midPath} is empty");
        return (wavPath, midPath);
    }

    [Fact]
    public void Ragtime_SyntheticFixture_Renders()
    {
        var (wavPath, midPath) = RunFixture("ragtime_polyphony");
        // Sanity: WAV has plausible 4-bar duration (4 beats × 4 bars / 120 BPM
        // = 8 sec; with reverb/release tail, expect 4-12 MB at 44.1 kHz stereo).
        Assert.InRange(new FileInfo(wavPath).Length, 1_000_000, 20_000_000);
        Assert.True(new FileInfo(midPath).Length > 100, "MIDI file suspiciously small");
    }

    [Fact]
    public void Ragtime_MapleLeaf_Renders()
    {
        var (wavPath, midPath) = RunFixture("maple_leaf_opening");
        Assert.InRange(new FileInfo(wavPath).Length, 1_000_000, 20_000_000);
        Assert.True(new FileInfo(midPath).Length > 100, "MIDI file suspiciously small");
    }

    [Fact]
    public void Ragtime_Synthetic_RmsRegression()
    {
        var (wavPath, _) = RunFixture("ragtime_polyphony");
        // The Flow script already wrote a dithered WAV — file-path overload
        // skips the double-dither round-trip that the AudioBuffer overload
        // applies for from-scratch renders.
        string baseline = Path.Combine(BaselinesDir, "ragtime_polyphony.wav");
        RmsRegressionTests.AssertWavMatchesBaseline(wavPath, baseline);
    }

    [Fact]
    public void Ragtime_MapleLeaf_RmsRegression()
    {
        var (wavPath, _) = RunFixture("maple_leaf_opening");
        string baseline = Path.Combine(BaselinesDir, "maple_leaf_opening.wav");
        RmsRegressionTests.AssertWavMatchesBaseline(wavPath, baseline);
    }

    [Fact]
    public void Ragtime_Synthetic_MultiTrackMidi()
    {
        var (_, midPath) = RunFixture("ragtime_polyphony");
        var midi = MidiFile.Read(midPath);
        // SPEC-6 multi-track: conductor + at least one sequence track. The
        // synthetic fixture uses one Sequence "v" so chunk count is 2.
        Assert.True(midi.Chunks.Count >= 2,
            $"expected ≥ 2 MIDI chunks (conductor + ≥1 sequence), got {midi.Chunks.Count}");
    }

    [Fact]
    public void Ragtime_TwoRunDeterminism()
    {
        // Two consecutive runs of the synthetic fixture must produce
        // byte-identical .wav and .mid (Phase 18/25/27 determinism contract
        // preserved under Phase 28 — voice-pool steal-oldest, dither RNG,
        // and noise RNG all reset per-call).
        var (wav1, mid1) = RunFixture("ragtime_polyphony");
        byte[] wavBytes1 = File.ReadAllBytes(wav1);
        byte[] midBytes1 = File.ReadAllBytes(mid1);

        // Run again — RunFixture deletes prior outputs first, so the second
        // run produces fresh files at the same paths.
        var (wav2, mid2) = RunFixture("ragtime_polyphony");
        byte[] wavBytes2 = File.ReadAllBytes(wav2);
        byte[] midBytes2 = File.ReadAllBytes(mid2);

        Assert.True(wavBytes1.SequenceEqual(wavBytes2),
            $"two runs of ragtime_polyphony.flow produced different WAV bytes (lens {wavBytes1.Length} vs {wavBytes2.Length})");
        Assert.True(midBytes1.SequenceEqual(midBytes2),
            $"two runs of ragtime_polyphony.flow produced different MIDI bytes (lens {midBytes1.Length} vs {midBytes2.Length})");
    }
}
