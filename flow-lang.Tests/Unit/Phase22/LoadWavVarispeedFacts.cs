using System;
using System.IO;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase22;

/// <summary>
/// DX-15 acceptance Facts pinning varispeed loadWav. Decisions referenced:
///   RESEARCH Assumption A1 (Int vs Double dispatch unambiguous via OverloadResolver scoring)
///   Pitfall 8 (sample-count ±1 tolerance for OLA edge / rounding)
///   RESEARCH §Resampler choice (linear interpolation default, OLA/sinc deferred)
///
/// Tests synthesize input buffers in-memory (avoid binary fixture files).
/// Engine-eval tests (OverloadDispatch_*) write a synthetic WAV to a temp dir,
/// then exercise (loadWav path Int) vs (loadWav path Double) through the full
/// signature dispatcher and assert the resulting buffer's frame count matches
/// the chosen overload's math.
///
/// Phase 22 plan 22-02 — RED state: most Facts must FAIL before Task 2 implements
/// `VarispeedResample`, `LoadWavSemitones`, `LoadWavRatio` bodies and registers the
/// two new overloads. The stub from Task 1 throws NotImplementedException to make
/// the compilation green while keeping assertions RED.
/// </summary>
public class LoadWavVarispeedFacts
{
    /// <summary>
    /// Helper: synthesize a mono sine buffer in memory at a given duration.
    /// Skips the WAV roundtrip — directly exercises the resample math.
    /// </summary>
    private static AudioBuffer SynthSine(int frames, int sampleRate, int channels)
    {
        var buf = new AudioBuffer(frames, channels, sampleRate);
        for (int f = 0; f < frames; f++)
            for (int ch = 0; ch < channels; ch++)
                buf.SetSample(f, ch, (float)Math.Sin(2 * Math.PI * 440 * f / sampleRate));
        return buf;
    }

    [Fact]
    public void TwelveSemitones_HalvesFrames()
    {
        // 12 semitones up = ratio 2.0 = sample count exactly halves (±1 for rounding).
        var src = SynthSine(44100, 44100, 1);
        var result = FileIO.VarispeedResample(src, 2.0);
        Assert.InRange(result.Frames, 22049, 22051);
    }

    [Fact]
    public void RatioOverload_RescalesFrames()
    {
        // ratio 1.5 → frames ≈ source / 1.5 (±1).
        var src = SynthSine(30000, 44100, 1);
        var result = FileIO.VarispeedResample(src, 1.5);
        Assert.InRange(result.Frames, 19999, 20001);
    }

    [Fact]
    public void Channels_Preserved()
    {
        // Stereo source → resampled output also stereo.
        var src = SynthSine(10000, 44100, 2);
        var result = FileIO.VarispeedResample(src, 2.0);
        Assert.Equal(2, result.Channels);
    }

    [Fact]
    public void SampleRate_Preserved()
    {
        // Varispeed does NOT change sample rate, only frame count.
        var src = SynthSine(10000, 44100, 1);
        var result = FileIO.VarispeedResample(src, 2.0);
        Assert.Equal(44100, result.SampleRate);
    }

    [Fact]
    public void ZeroSemitones_ReturnsUnchangedFrameCount()
    {
        // semitones=0 short-circuit → output frames == source frames (identity).
        // Exercise via LoadWavSemitones over a roundtripped WAV.
        string tmp = Path.Combine(Path.GetTempPath(), $"dx15_zerosemi_{Guid.NewGuid():N}.wav");
        try
        {
            var src = SynthSine(4410, 44100, 1);
            // Use existing WAV writer (private path: reuse ExportWavInternal via WriteWav).
            FileIO.WriteWav(new[] { Value.String(tmp), Value.Buffer(src) });

            var loaded = FileIO.LoadWavSemitones(new[] { Value.String(tmp), Value.Int(0) }).As<AudioBuffer>();
            Assert.Equal(src.Frames, loaded.Frames);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void RatioOne_ReturnsUnchangedFrameCount()
    {
        // ratio=1.0 short-circuit → output frames == source frames (identity).
        string tmp = Path.Combine(Path.GetTempPath(), $"dx15_ratioone_{Guid.NewGuid():N}.wav");
        try
        {
            var src = SynthSine(4410, 44100, 1);
            FileIO.WriteWav(new[] { Value.String(tmp), Value.Buffer(src) });

            var loaded = FileIO.LoadWavRatio(new[] { Value.String(tmp), Value.Double(1.0) }).As<AudioBuffer>();
            Assert.Equal(src.Frames, loaded.Frames);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void NegativeRatio_ThrowsArgumentException()
    {
        // ratio <= 0.0 must throw ArgumentException (DoS guard, threat T-22-V5-09).
        string tmp = Path.Combine(Path.GetTempPath(), $"dx15_neg_{Guid.NewGuid():N}.wav");
        try
        {
            var src = SynthSine(4410, 44100, 1);
            FileIO.WriteWav(new[] { Value.String(tmp), Value.Buffer(src) });

            Assert.Throws<ArgumentException>(() =>
                FileIO.LoadWavRatio(new[] { Value.String(tmp), Value.Double(-1.0) }));
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void ZeroRatio_ThrowsArgumentException()
    {
        // ratio == 0.0 must throw (would otherwise produce infinite/NaN frame count).
        string tmp = Path.Combine(Path.GetTempPath(), $"dx15_zero_{Guid.NewGuid():N}.wav");
        try
        {
            var src = SynthSine(4410, 44100, 1);
            FileIO.WriteWav(new[] { Value.String(tmp), Value.Buffer(src) });

            Assert.Throws<ArgumentException>(() =>
                FileIO.LoadWavRatio(new[] { Value.String(tmp), Value.Double(0.0) }));
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void NegativeSemitones_LowerPitch_DoublesFrames()
    {
        // -12 semitones = ratio 0.5 → output frames ≈ 2× source.
        var src = SynthSine(10000, 44100, 1);
        var result = FileIO.VarispeedResample(src, 0.5);
        Assert.InRange(result.Frames, 19999, 20001);
    }

    [Fact]
    public void SingleArgUnchanged()
    {
        // Regression gate: 1-arg LoadWav path must produce a buffer identical to the
        // original (frame-count-and-channels-and-sample-rate). Exercises the existing
        // path twice on the same WAV; both calls must return byte-equivalent buffers.
        string tmp = Path.Combine(Path.GetTempPath(), $"dx15_1arg_{Guid.NewGuid():N}.wav");
        try
        {
            var src = SynthSine(4410, 44100, 1);
            FileIO.WriteWav(new[] { Value.String(tmp), Value.Buffer(src) });

            var a = FileIO.LoadWav(new[] { Value.String(tmp) }).As<AudioBuffer>();
            var b = FileIO.LoadWav(new[] { Value.String(tmp) }).As<AudioBuffer>();

            Assert.Equal(a.Frames, b.Frames);
            Assert.Equal(a.Channels, b.Channels);
            Assert.Equal(a.SampleRate, b.SampleRate);
            Assert.Equal(a.Data.Length, b.Data.Length);
            for (int i = 0; i < a.Data.Length; i++)
                Assert.Equal(a.Data[i], b.Data[i]);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void OverloadDispatch_IntChoosesSemitones()
    {
        // Pitfall 1 / A1: (loadWav path 12) routes to the Int (semitones) overload.
        // 12 semitones = ratio 2.0; createSineTone is stereo at 44100 → 1s = 44100 frames →
        // resampled to ~22050 frames. Asserts frame count matches semitones path.
        string tmp = Path.Combine(Path.GetTempPath(), $"dx15_dispatch_int_{Guid.NewGuid():N}.wav");
        string tmpEsc = tmp.Replace("\\", "/");
        try
        {
            using var runner = new FlowEngineRunner();
            var (_, _, stderr, errorCount) = runner.RunSource($@"
use ""@std""
use ""@audio""
Buffer src = (createSineTone 1.0 440.0 0.8)
(writeWav ""{tmpEsc}"" src)
Buffer high = (loadWav ""{tmpEsc}"" 12)
Int frames = (getFrames high)
");
            Assert.Equal(0, errorCount);
            Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("error", StringComparison.OrdinalIgnoreCase),
                $"unexpected stderr: {stderr}");

            int frames = runner.GetVariable("frames").As<int>();
            Assert.InRange(frames, 22049, 22051);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void OverloadDispatch_DoubleChoosesRatio()
    {
        // Pitfall 1 / A1: (loadWav path 1.5) routes to the Double (ratio) overload.
        // ratio 1.5 → frames ≈ source/1.5; 44100 frames → ~29400 frames.
        string tmp = Path.Combine(Path.GetTempPath(), $"dx15_dispatch_double_{Guid.NewGuid():N}.wav");
        string tmpEsc = tmp.Replace("\\", "/");
        try
        {
            using var runner = new FlowEngineRunner();
            var (_, _, stderr, errorCount) = runner.RunSource($@"
use ""@std""
use ""@audio""
Buffer src = (createSineTone 1.0 440.0 0.8)
(writeWav ""{tmpEsc}"" src)
Buffer fast = (loadWav ""{tmpEsc}"" 1.5)
Int frames = (getFrames fast)
");
            Assert.Equal(0, errorCount);
            Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("error", StringComparison.OrdinalIgnoreCase),
                $"unexpected stderr: {stderr}");

            int frames = runner.GetVariable("frames").As<int>();
            Assert.InRange(frames, 29399, 29401);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
