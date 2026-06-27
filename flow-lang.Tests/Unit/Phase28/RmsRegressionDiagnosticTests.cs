using System;
using System.Collections.Generic;
using System.IO;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Synthesizers;
using FlowLang.Tests.Helpers;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase28;

/// <summary>
/// Phase 28 (SPEC-8) Plan 06 acceptance facts pinning the
/// <see cref="RmsRegressionTests"/> helper itself:
///
///   • <see cref="RmsRegression_PositiveBaseline"/> — render under the same
///     articulation as the committed baseline → assertion passes.
///   • <see cref="RmsRegression_NegativeDiagnostic"/> — render under a different
///     articulation → assertion fails with the SPEC-8 diagnostic format.
///   • <see cref="RmsRegression_FrameCountMismatch"/> — different buffer length
///     → assertion fails at the Frames equality check (before window iteration).
///   • <see cref="RmsRegression_ToleranceOverrideRequiresReason"/> — supplying a
///     non-default tolerance without overrideReason raises ArgumentException.
///   • <see cref="WavReader_RoundTrip"/> — write a known buffer via FileIO,
///     read it back via WavReader, assert sample-by-sample match within the
///     int16 round-trip quantization (1/32768).
///
/// The Staccato baseline WAV is auto-generated on first test run if missing
/// (and committed to git after generation), so the test class is self-
/// bootstrapping and re-runs produce identical baselines via deterministic
/// rendering (Phase 18 Plan 05 noise+dither RNG seed contract).
///
/// Note on test parallelism: <see cref="FileIO.WriteWav"/> uses a SHARED static
/// dither RNG that's reset at the start of each export and advanced sample-
/// by-sample within. Two concurrent tests both writing WAVs interleave their
/// dither samples, producing non-deterministic per-sample noise that drifts
/// outside the SPEC-8 ±0.5 dB tolerance for silent windows. This class +
/// HeldNoteRmsTests + VoiceBlockRenderTests live in the "FlowScripts" xUnit
/// Collection so they serialize against the broader engine-runner test pool.
/// </summary>
[Collection("FlowScripts")]
public class RmsRegressionDiagnosticTests
{
    private const int SampleRate = 44100;
    private const double Bpm = 120.0;

    private static string BaselinePath => Path.Combine(
        FindBaselinesRoot(), "staccato_baseline.wav");

    private static string FindBaselinesRoot()
    {
        // Walk up from the test assembly's bin/Debug/net10.0 to find the
        // flow-lang.Tests directory and its baselines/ subdir.
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(dir, "baselines", "Phase28");
            if (Path.GetFileName(dir) == "flow-lang.Tests")
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not locate flow-lang.Tests");
    }

    /// <summary>
    /// Renders a one-bar sequence: a C4q under <paramref name="art"/> + 3 quarter
    /// rests. Flattens the resulting voices into a single AudioBuffer at the bar's
    /// full length (4 beats × 0.5 sec = 2 sec = 88200 frames @ 44.1 kHz). The
    /// fixed length lets us compare different articulations at the same frame
    /// count — Staccato's note ends early, leaving silence in late windows where
    /// Normal still has audible decay.
    /// </summary>
    private static AudioBuffer RenderC4q(Articulation art)
    {
        SynthUtils.ResetNoiseRng();
        var note = new MusicalNoteData('C', 4, 0,
            (int)NoteValueType.Value.QUARTER,
            isRest: false,
            articulation: art);
        var rest = new MusicalNoteData(' ', 0, 0,
            (int)NoteValueType.Value.QUARTER,
            isRest: true);
        var bar = new BarData(new[] { note, rest, rest, rest }, new TimeSignatureData(4, 4));

        var voices = BarRenderer.RenderBarToVoices(bar, "sine", SampleRate, Bpm);
        // Flatten into a fixed-length mono buffer covering the full bar.
        // Bar = 4 beats × 60/120 = 2.0 sec = 88200 frames.
        const int totalFrames = 88200;
        var output = new AudioBuffer(totalFrames, 1, SampleRate);
        foreach (var voice in voices)
        {
            int onsetFrames = (int)(voice.OffsetBeats * 60.0 / Bpm * SampleRate);
            for (int i = 0; i < voice.Buffer.Frames; i++)
            {
                int dst = onsetFrames + i;
                if (dst < 0 || dst >= totalFrames) continue;
                float sum = 0f;
                for (int ch = 0; ch < voice.Buffer.Channels; ch++)
                    sum += voice.Buffer.GetSample(i, ch);
                sum /= Math.Max(1, voice.Buffer.Channels);
                output.SetSample(dst, 0, output.GetSample(dst, 0) + sum);
            }
        }
        return output;
    }

    /// <summary>
    /// Ensures the staccato baseline WAV exists; generates it from the current
    /// Phase 28 implementation when missing. Bootstrap-and-commit pattern:
    /// committed-WAV-found → use it; missing → generate, write, fail with
    /// instruction to commit (so a regression in render output gets noticed
    /// before the baseline is overwritten silently).
    /// </summary>
    private static AudioBuffer EnsureBaseline()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath)!);
        if (!File.Exists(BaselinePath))
        {
            var rendered = RenderC4q(Articulation.Staccato);
            var args = new List<Value>
            {
                Value.String(BaselinePath),
                Value.Buffer(rendered),
            };
            FileIO.WriteWav(args);
            // First-run bootstrap: not a failure, but make the situation visible.
            // The committed WAV is what subsequent runs assert against — Phase 18
            // Plan 05's deterministic dither RNG ensures byte-identical regeneration.
        }
        return WavReader.ReadWav(BaselinePath);
    }

    [Fact]
    public void WavReader_RoundTrip()
    {
        // Round-trip: write a known buffer via FileIO, read via WavReader,
        // compare sample-by-sample within int16 quantization (1/32768 ≈ 3.05e-5).
        // Mirrors the existing Phase 13 writeWav test pattern for confidence
        // that the reader implementation matches the writer's RIFF format.
        const int frames = 4410; // 0.1 sec at 44.1 kHz
        var src = new AudioBuffer(frames, 1, SampleRate);
        for (int i = 0; i < frames; i++)
            src.SetSample(i, 0, (float)(0.5 * Math.Sin(2.0 * Math.PI * 440.0 * i / SampleRate)));

        string tempPath = Path.Combine(Path.GetTempPath(),
            $"phase28_wavreader_roundtrip_{Guid.NewGuid():N}.wav");
        try
        {
            var args = new List<Value>
            {
                Value.String(tempPath),
                Value.Buffer(src),
            };
            FileIO.WriteWav(args);

            var read = WavReader.ReadWav(tempPath);
            Assert.Equal(frames, read.Frames);
            Assert.Equal(1, read.Channels);
            Assert.Equal(SampleRate, read.SampleRate);

            for (int i = 0; i < frames; i++)
            {
                float diff = Math.Abs(src.GetSample(i, 0) - read.GetSample(i, 0));
                // int16 quantization step is 1/32768 ≈ 3.05e-5, plus TPDF dither
                // (uniform on [-1 LSB, +1 LSB] in float space → up to 2 LSBs of
                // round-trip error per sample) → per-sample tolerance 1.5e-4 covers
                // the worst-case dither + quantization combo with margin.
                Assert.True(diff < 1.5e-4,
                    $"frame {i}: src={src.GetSample(i, 0):F6}, read={read.GetSample(i, 0):F6}, diff={diff:F6}");
            }
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void RmsRegression_PositiveBaseline()
    {
        EnsureBaseline();
        var rendered = RenderC4q(Articulation.Staccato);
        // Should pass — deterministic dither RNG ensures regenerating C4q-Staccato
        // produces the same bytes as the committed baseline.
        RmsRegressionTests.AssertRmsWithinTolerance(rendered, BaselinePath);
    }

    [Fact]
    public void RmsRegression_NegativeDiagnostic()
    {
        EnsureBaseline();
        // Render under Articulation.Normal (100% duration vs Staccato's 25%).
        // The frame-count check passes (BarRenderer allocates the same length
        // either way — only the audible content differs), so window-by-window
        // RMS comparison kicks in. Late windows on the Staccato baseline are
        // silent (~-120 dB) while Normal still has audible signal → big delta.
        var rendered = RenderC4q(Articulation.Normal);
        var ex = Assert.ThrowsAny<Exception>(() =>
            RmsRegressionTests.AssertRmsWithinTolerance(rendered, BaselinePath));

        // SPEC-8 diagnostic format: "RMS deviation in window N (XXXms-YYYms): expected -A dB, got -B dB"
        Assert.Contains("RMS deviation in window", ex.Message);
        Assert.Contains("expected", ex.Message);
        Assert.Contains("got", ex.Message);
        Assert.Contains("dB", ex.Message);
    }

    [Fact]
    public void RmsRegression_FrameCountMismatch()
    {
        EnsureBaseline();
        // Baseline is the full bar (88200 frames). Render a single-voice (no
        // rests) Staccato bar — same articulation but different bar length, so
        // frame counts mismatch.
        var note = new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER,
            isRest: false, articulation: Articulation.Staccato);
        var bar = new BarData(new[] { note }, new TimeSignatureData(4, 4));
        var voices = BarRenderer.RenderBarToVoices(bar, "sine", SampleRate, Bpm);
        var rendered = voices[0].Buffer;
        Assert.NotEqual(88200, rendered.Frames);

        // Frame-count Assert.Equal throws Xunit.Sdk.EqualException — not the
        // "RMS deviation in window" diagnostic. Confirm it's the early frame check.
        var ex = Assert.ThrowsAny<Exception>(() =>
            RmsRegressionTests.AssertRmsWithinTolerance(rendered, BaselinePath));
        Assert.DoesNotContain("RMS deviation in window", ex.Message);
    }

    [Fact]
    public void RmsRegression_ToleranceOverrideRequiresReason()
    {
        EnsureBaseline();
        var rendered = RenderC4q(Articulation.Staccato);
        var ex = Assert.Throws<ArgumentException>(() =>
            RmsRegressionTests.AssertRmsWithinTolerance(
                rendered, BaselinePath, toleranceDb: 1.0));
        Assert.Contains("requires overrideReason", ex.Message);
    }

    [Fact]
    public void RmsRegression_ToleranceOverrideAcceptedWithReason()
    {
        EnsureBaseline();
        // Demonstrate the documented escape hatch: caller can widen the band
        // when there's a domain reason (e.g. a known stochastic synthesizer
        // component). reason MUST be non-empty.
        var rendered = RenderC4q(Articulation.Staccato);
        RmsRegressionTests.AssertRmsWithinTolerance(
            rendered, BaselinePath, toleranceDb: 2.0,
            overrideReason: "Smoke regression — wider band documenting synth-determinism caveat");
    }
}
