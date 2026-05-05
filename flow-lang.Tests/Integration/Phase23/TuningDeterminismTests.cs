using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase23;

/// <summary>
/// Phase 23 Plan 23-04 Task 2 — byte-identical determinism pin for the JI /
/// Pythagorean / explicit-EqualTemperament rendering paths.
///
/// Per CONTEXT.md Claude's Discretion: tutorial.flow + showcase.flow stay
/// 12-TET (preserve v1.2 byte-identical pin); these Facts independently pin
/// the JI/Pythagorean/explicit-EqualTemperament rendering paths via
/// Fact-controlled inline .flow source strings.
///
/// Per WARNING-5: this test class uses Fact-controlled INLINE .flow source
/// strings with per-Fact unique /tmp WAV paths
/// (<c>/tmp/flow_test_tuning_determinism_xunit_*.wav</c>). It does NOT execute
/// the on-disk smoke script that ships under tests/ — that script owns its
/// own hardcoded path for the .flow integration loop. This isolation prevents
/// any race between the xUnit suite and the .flow integration loop.
///
/// Per WARNING-4: <see cref="RenderingDiagnostics.ResetForTesting"/> runs in
/// the ctor + Dispose AND between the two sequential FlowEngineRunner instances
/// in <see cref="RunTwiceAndCompare"/>, defending against future warning-gate
/// changes where dedup HashSet state could leak between runs and mask a
/// non-determinism regression.
///
/// Mirrors <see cref="Phase18.ByteIdenticalTutorialTests"/> two-runner pattern
/// (Pattern S6) and <see cref="ByteIdenticalDefaultTuningTests"/> inline-source
/// shape.
/// </summary>
[Collection("FlowScripts")]
public class TuningDeterminismTests : System.IDisposable
{
    public TuningDeterminismTests() { RenderingDiagnostics.ResetForTesting(); }
    public void Dispose()           { RenderingDiagnostics.ResetForTesting(); }

    [Fact]
    public void JustIntonation_TwoRunsProduceIdenticalWav()
    {
        const string wavPath = "/tmp/flow_test_tuning_determinism_xunit_ji.wav";
        RunTwiceAndCompare(BuildInlineSource("justIntonation", wavPath), wavPath);
    }

    [Fact]
    public void ExplicitEqualTemperament_TwoRunsProduceIdenticalWav()
    {
        const string wavPath = "/tmp/flow_test_tuning_determinism_xunit_eq.wav";
        RunTwiceAndCompare(BuildInlineSource("equalTemperament", wavPath), wavPath);
    }

    [Fact]
    public void Pythagorean_TwoRunsProduceIdenticalWav()
    {
        const string wavPath = "/tmp/flow_test_tuning_determinism_xunit_pyth.wav";
        RunTwiceAndCompare(BuildInlineSource("pythagorean", wavPath), wavPath);
    }

    /// <summary>
    /// Builds a minimal .flow source string that declares the given tuning
    /// pragma and renders a fixed Sequence to <paramref name="wavPath"/>.
    /// Per WARNING-5 this is a self-contained test artifact — does NOT depend
    /// on the on-disk smoke script's hardcoded path (which the .flow
    /// integration loop owns).
    ///
    /// Source shape mirrors <see cref="ByteIdenticalDefaultTuningTests.FlowSource"/>:
    /// section + Song + renderSong is the canonical Sequence -> Buffer pipeline
    /// since renderSequence returns Voice[] (per flow-lang/notation.flow:201).
    /// </summary>
    private static string BuildInlineSource(string pragma, string wavPath) =>
        "enable " + pragma + ";\n" + @"
use ""@std""
use ""@audio""
use ""@composition""
tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section sec23_04 {
                Sequence mel = | C4q E4q G4q |
            }
            Song s23_04 = [sec23_04]
            Buffer renderedBuffer = (renderSong s23_04 ""sine"")
            (writeWav """ + wavPath.Replace("\\", "/") + @""" renderedBuffer)
        }
    }
}
";

    /// <summary>
    /// Two-runner byte-identical comparison. Runs <paramref name="source"/>
    /// twice in fresh <see cref="FlowEngineRunner"/> instances, compares the
    /// resulting WAV bytes via <see cref="Enumerable.SequenceEqual"/>.
    ///
    /// Per WARNING-4: <see cref="RenderingDiagnostics.ResetForTesting"/> fires
    /// BETWEEN the two runs so the second runner does not inherit dedup state
    /// from the first — defends against any future code path where warning
    /// emission affects rendering control flow.
    /// </summary>
    private static void RunTwiceAndCompare(string source, string wavPath)
    {
        // Run #1
        if (File.Exists(wavPath)) File.Delete(wavPath);
        using (var runner1 = new FlowEngineRunner())
        {
            var (ok1, _, stderr1, _) = runner1.RunSource(source);
            Assert.True(ok1, $"first run failed; stderr: {stderr1}");
        }
        Assert.True(File.Exists(wavPath), $"first run did not produce {wavPath}");
        byte[] firstRun = File.ReadAllBytes(wavPath);

        // Run #2 — clear any one-shot dedup state between runs (WARNING-4).
        File.Delete(wavPath);
        RenderingDiagnostics.ResetForTesting();
        using (var runner2 = new FlowEngineRunner())
        {
            var (ok2, _, stderr2, _) = runner2.RunSource(source);
            Assert.True(ok2, $"second run failed; stderr: {stderr2}");
        }
        Assert.True(File.Exists(wavPath), $"second run did not produce {wavPath}");
        byte[] secondRun = File.ReadAllBytes(wavPath);

        Assert.True(firstRun.Length > 0, $"empty WAV at {wavPath}");
        Assert.Equal(firstRun.Length, secondRun.Length);
        Assert.True(firstRun.SequenceEqual(secondRun),
            $"byte-identical determinism violated for {wavPath}: " +
            $"run1 len={firstRun.Length}, run2 len={secondRun.Length}");
    }
}
