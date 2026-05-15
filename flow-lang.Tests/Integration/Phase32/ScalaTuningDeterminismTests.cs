using System;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase32;

/// <summary>
/// Phase 32 Plan 32-06 Task 3 — SPEC-6 two-run byte-identical determinism gate
/// for the full Scala tuning loader + <c>tuning t { ... }</c> block path.
///
/// Pattern D (RESEARCH §"Pattern D — byte-identical determinism test"): same
/// .flow source + same .scl file + same git SHA → byte-identical WAV across
/// two consecutive FlowEngineRunner instances. <see cref="RenderingDiagnostics.ResetForTesting"/>
/// fires in ctor + Dispose AND between runs so dedup state never leaks.
///
/// SPEC-6 constraint (locked, Pitfall 8): "Two-run byte-identical
/// determinism: same script + same .scl/.kbm files + same git SHA →
/// byte-identical WAV/MID". Most likely failure modes are Dictionary
/// iteration order (mitigated by Plan 32-03 eager-precompute at
/// construction time) and locale-dependent parsing (mitigated by
/// Plan 32-02's <c>InvariantCulture</c> + <c>NumberStyles.Float</c> guard).
/// This test class catches any regression in that determinism contract.
///
/// Fixtures (5 canonical + 1 MIDI export case) covering the SPEC-6
/// acceptance battery:
///
/// <list type="bullet">
///   <item>partch_43 — Harry Partch's 43-tone JI fan (octave-period)</item>
///   <item>carlos_alpha — Wendy Carlos' Alpha (NON-octave, 1404¢ period)</item>
///   <item>slendro — Javanese gamelan 5-tone (octave-period)</item>
///   <item>pythagorean_12 — pure 3-limit 12-tone (octave-period)</item>
///   <item>just_5limit — 5-limit-dominant 12-tone (octave-period, has a 7-limit tritone)</item>
///   <item>partch_43 → MIDI export — verifies Phase 23 D-13 advisory firing
///   does not introduce ordering non-determinism in the SMF byte stream</item>
/// </list>
///
/// Pattern mirrors <see cref="Phase23.TuningDeterminismTests"/> two-runner shape
/// (CommonRunSourceTwiceAndCompare) but with a Scala fixture pulled via
/// <see cref="FindRepoRoot"/> so the test runs from <c>bin/Debug/net10.0/</c>
/// without relying on cwd.
/// </summary>
[Collection("FlowScripts")]
public class ScalaTuningDeterminismTests : IDisposable
{
    public ScalaTuningDeterminismTests() { RenderingDiagnostics.ResetForTesting(); }
    public void Dispose()                { RenderingDiagnostics.ResetForTesting(); }

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

    /// <summary>
    /// Builds a minimal .flow source that loads the given .scl fixture, opens
    /// a <c>tuning t { ... }</c> block around a short C4q D4q E4q F4q sequence,
    /// and emits a WAV at <paramref name="outputPath"/>. The melody is short
    /// (4 quarter notes at tempo 120 → ~2 seconds) to keep test runtime low.
    /// </summary>
    private static string BuildWavSource(string sclPath, string outputPath) => $@"use ""@std""
use ""@audio""
Tuning t = (loadScala ""{sclPath}"")
tempo 120 {{
    timesig 4/4 {{
        tuning t {{
            section sec_det {{
                | C4q D4q E4q F4q |
            }}
        }}
    }}
}}
Song song = [sec_det]
Buffer audio = (renderSong song ""sine"")
(writeWav ""{outputPath}"" audio)
";

    /// <summary>
    /// Variant for the MIDI export path. Uses <c>writeMidi</c> instead of
    /// <c>writeWav</c>; per Phase 23 D-13, MIDI export emits 12-TET pitches +
    /// a one-shot advisory under custom tunings — the advisory firing must
    /// NOT introduce ordering non-determinism in the SMF byte stream.
    /// </summary>
    private static string BuildMidiSource(string sclPath, string outputPath) => $@"use ""@std""
use ""@audio""
Tuning t = (loadScala ""{sclPath}"")
tempo 120 {{
    timesig 4/4 {{
        tuning t {{
            section sec_det_mid {{
                | C4q D4q E4q F4q |
            }}
        }}
    }}
}}
Song song = [sec_det_mid]
(writeMidi ""{outputPath}"" song)
";

    /// <summary>
    /// Two-runner byte-identical comparison. Mirrors the
    /// <see cref="Phase23.TuningDeterminismTests.RunTwiceAndCompare"/> pattern.
    /// <see cref="RenderingDiagnostics.ResetForTesting"/> fires BETWEEN the two
    /// runs so the second runner doesn't inherit dedup state — defending
    /// against any future code path where warning emission affects rendering
    /// control flow.
    /// </summary>
    private static void RunTwiceAndCompare(string source, string outputPath)
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);

        // Run #1.
        using (var runner1 = new FlowEngineRunner())
        {
            var (ok1, _, stderr1, _) = runner1.RunSource(source);
            Assert.True(ok1, $"first run failed; stderr: {stderr1}");
        }
        Assert.True(File.Exists(outputPath), $"first run did not produce {outputPath}");
        byte[] firstRun = File.ReadAllBytes(outputPath);

        // Run #2 — fresh runner, fresh diagnostics state.
        File.Delete(outputPath);
        RenderingDiagnostics.ResetForTesting();
        using (var runner2 = new FlowEngineRunner())
        {
            var (ok2, _, stderr2, _) = runner2.RunSource(source);
            Assert.True(ok2, $"second run failed; stderr: {stderr2}");
        }
        Assert.True(File.Exists(outputPath), $"second run did not produce {outputPath}");
        byte[] secondRun = File.ReadAllBytes(outputPath);

        Assert.True(firstRun.Length > 0, $"empty output at {outputPath}");
        Assert.Equal(firstRun.Length, secondRun.Length);
        // Find first divergence for a helpful error message.
        int divergeIdx = -1;
        int minLen = Math.Min(firstRun.Length, secondRun.Length);
        for (int i = 0; i < minLen; i++)
        {
            if (firstRun[i] != secondRun[i]) { divergeIdx = i; break; }
        }
        Assert.True(firstRun.SequenceEqual(secondRun),
            $"byte-identical determinism violated for {outputPath}: " +
            $"run1 len={firstRun.Length}, run2 len={secondRun.Length}, " +
            $"first divergence at byte {divergeIdx}");
    }

    [Fact]
    public void Determinism_Partch43_WavBytesIdenticalAcrossRuns()
    {
        const string outputPath = "/tmp/p32_06_det_partch43.wav";
        RunTwiceAndCompare(BuildWavSource(FixturePath("partch_43.scl"), outputPath), outputPath);
    }

    [Fact]
    public void Determinism_CarlosAlpha_WavBytesIdenticalAcrossRuns()
    {
        // Non-octave path — the math-heavy case; period ≈ 1404¢ exercises the
        // PeriodCents pre-compute branch in ResolvedTuning's MidiToHz table
        // builder, which is the most likely site of locale-sensitive parsing
        // or Dictionary-order non-determinism.
        const string outputPath = "/tmp/p32_06_det_carlos_alpha.wav";
        RunTwiceAndCompare(BuildWavSource(FixturePath("carlos_alpha.scl"), outputPath), outputPath);
    }

    [Fact]
    public void Determinism_Slendro_WavBytesIdenticalAcrossRuns()
    {
        const string outputPath = "/tmp/p32_06_det_slendro.wav";
        RunTwiceAndCompare(BuildWavSource(FixturePath("slendro.scl"), outputPath), outputPath);
    }

    [Fact]
    public void Determinism_Pythagorean12_WavBytesIdenticalAcrossRuns()
    {
        const string outputPath = "/tmp/p32_06_det_pythagorean_12.wav";
        RunTwiceAndCompare(BuildWavSource(FixturePath("pythagorean_12.scl"), outputPath), outputPath);
    }

    [Fact]
    public void Determinism_Just5Limit_WavBytesIdenticalAcrossRuns()
    {
        const string outputPath = "/tmp/p32_06_det_just_5limit.wav";
        RunTwiceAndCompare(BuildWavSource(FixturePath("just_5limit.scl"), outputPath), outputPath);
    }

    [Fact]
    public void Determinism_PartchMidiExport_BytesIdenticalAcrossRuns()
    {
        // writeMidi under custom tuning — verifies the D-13 advisory firing
        // doesn't introduce ordering non-determinism in the SMF byte stream.
        // MIDI export per Phase 23 D-13 emits 12-TET pitches + a one-shot
        // stderr advisory; the advisory firing path uses RenderingDiagnostics.WarnOnce
        // which is sentinel-keyed, so two runs against the same fixture +
        // ResetForTesting between them MUST produce identical .mid bytes.
        const string outputPath = "/tmp/p32_06_det_partch.mid";
        RunTwiceAndCompare(BuildMidiSource(FixturePath("partch_43.scl"), outputPath), outputPath);
    }
}
