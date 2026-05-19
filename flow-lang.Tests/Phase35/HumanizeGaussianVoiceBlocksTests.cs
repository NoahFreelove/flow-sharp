using System;
using System.IO;
using System.Linq;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 HK-01 regression facts: humanizeGaussian must recurse into a bar's
/// ParallelVoices (Phase 28 voice-block polyphony) instead of dropping them.
///
/// Pre-Phase-35 bug (root cause at TransformFunctions.cs:931-962):
///   HumanizeGaussian iterates only <c>bar.MusicalNotes</c> and constructs
///   <c>new BarData(newNotes, bar.TimeSignature!)</c>, dropping the
///   <c>bar.ParallelVoices</c> entirely. Result: a sequence built from a voice
///   block (`| {voice ...}{voice ...} |`) renders to a header-only WAV (~44 bytes)
///   instead of the expected polyphonic audio.
///
/// Fix shape (mirrors BarRenderer.cs:62-77): when a bar has non-null
/// ParallelVoices, recursively humanize each voice sub-bar reusing the SAME
/// seeded Random instance so the Phase 18/25 byte-identical determinism contract
/// stays intact (Pitfall 8 in 35-RESEARCH.md).
/// </summary>
[Collection("FlowScripts")]
public class HumanizeGaussianVoiceBlocksTests
{
    private const string Prelude = @"
use ""@std""
use ""@audio""
use ""@notation""
";

    private static string TempWav(string suffix) =>
        Path.Combine(Path.GetTempPath(), $"flow_phase35_humanize_voice_{suffix}_{Guid.NewGuid():N}.wav");

    /// <summary>
    /// Renders a 2-voice block, applies humanizeGaussian, writes a WAV.
    /// Returns the WAV bytes on disk so the assertions can compare both size
    /// and PCM content.
    /// </summary>
    private static byte[] RenderHumanizedVoiceBlockWav(string wavPath, int seed = 314)
    {
        string source = Prelude + $@"
tempo 120 {{
    timesig 4/4 {{
        Sequence stride = | {{voice C4w}} {{voice C5q D5q E5q F5q}} |
        Sequence humanized = (humanizeGaussian stride 0.03 {seed})
        section main {{ Sequence v = humanized }}
        Song s = [main]
        Buffer mix = (renderSong s ""organ"")
        (writeWav ""{wavPath.Replace("\\", "/")}"" mix)
    }}
}}
";

        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(source);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nsource:\n{source}");
        Assert.True(File.Exists(wavPath), $"writeWav did not produce {wavPath}");
        return File.ReadAllBytes(wavPath);
    }

    /// <summary>
    /// Counts non-zero PCM frames in a 16-bit-LE WAV. Returns 0 for a
    /// header-only / silent file. WAV header is 44 bytes; samples start at
    /// byte 44 and are 2 bytes each (16-bit little-endian).
    /// </summary>
    private static int CountNonZeroSamples(byte[] wav)
    {
        if (wav.Length <= 44) return 0;
        int count = 0;
        for (int i = 44; i + 1 < wav.Length; i += 2)
        {
            short sample = (short)(wav[i] | (wav[i + 1] << 8));
            if (sample != 0) count++;
        }
        return count;
    }

    [Fact]
    public void HumanizeOverVoiceBlockProducesNonEmptyWav()
    {
        // HK-01 acceptance: humanizeGaussian over a voice block must produce
        // > 44-byte WAV (more than header) AND contain non-zero PCM samples.
        // Pre-fix shape: WAV is 44 bytes (header only) because ParallelVoices
        // are silently dropped at TransformFunctions.cs:959.
        var wavPath = TempWav("nonempty");
        try
        {
            var bytes = RenderHumanizedVoiceBlockWav(wavPath, seed: 314);
            Assert.True(bytes.Length > 44,
                $"WAV is header-only ({bytes.Length} bytes) — humanizeGaussian dropped ParallelVoices");
            int nonZero = CountNonZeroSamples(bytes);
            Assert.True(nonZero > 100,
                $"WAV is silent ({nonZero} non-zero samples) — voice-block content not rendered after humanize");
        }
        finally
        {
            if (File.Exists(wavPath)) File.Delete(wavPath);
        }
    }

    [Fact]
    public void HumanizeOverVoiceBlockIsDeterministic()
    {
        // T-35-04 mitigation: humanizeGaussian determinism contract — two runs
        // at the same seed produce byte-identical output. Inherits the Phase
        // 18/25 byte-identical sentinel. The fix must reuse a SINGLE seeded
        // Random across all voices in a bar (NOT seed per-voice).
        var wavA = TempWav("detA");
        var wavB = TempWav("detB");
        try
        {
            var bytesA = RenderHumanizedVoiceBlockWav(wavA, seed: 42);
            var bytesB = RenderHumanizedVoiceBlockWav(wavB, seed: 42);
            Assert.True(bytesA.Length > 44, $"run A is header-only ({bytesA.Length} bytes)");
            Assert.True(bytesB.Length > 44, $"run B is header-only ({bytesB.Length} bytes)");
            Assert.True(bytesA.SequenceEqual(bytesB),
                $"WAV bytes differ across two seeded runs: A.Len={bytesA.Length}, B.Len={bytesB.Length} — determinism broken");
        }
        finally
        {
            if (File.Exists(wavA)) File.Delete(wavA);
            if (File.Exists(wavB)) File.Delete(wavB);
        }
    }
}
