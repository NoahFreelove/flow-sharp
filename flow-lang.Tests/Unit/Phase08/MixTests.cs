using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Unit.Phase08;

/// <summary>
/// AUDIO-05 regression test: AudioCore.Mix sums samples additively
/// (not overwrite/average/min/max). Pre-AUDIO-05 there was no mix function;
/// this Fact pins the additive contract at the C# API layer so any future
/// refactor that switches to average/normalize would break the suite.
///
/// API shape (per flow-lang/StandardLibrary/Audio/AudioCore.cs:170):
///   public static Value Mix(IReadOnlyList&lt;Value&gt; args)
///     — args[0] and args[1] are Value.Buffer(AudioBuffer).
///     — returns Value wrapping the mixed AudioBuffer.
///     — sums sample-by-sample at unity gain (AudioCore.cs:200:
///       result.Data[i] = sampleA + sampleB).
/// </summary>
public class MixTests
{
    [Fact]
    public void Mix_SumsSamples_AdditiveSemantics()
    {
        // Construct two 1-frame mono buffers with known sample values.
        var bufA = new AudioBuffer(frames: 1, channels: 1, sampleRate: 44100);
        bufA.SetSample(0, 0, 0.5f);

        var bufB = new AudioBuffer(frames: 1, channels: 1, sampleRate: 44100);
        bufB.SetSample(0, 0, 0.3f);

        // Call AudioCore.Mix with the boxed-Value arg shape used by the
        // built-in dispatcher.
        var args = new[] { Value.Buffer(bufA), Value.Buffer(bufB) };
        var result = AudioCore.Mix(args);

        // Unwrap the result and assert the first sample is the additive sum.
        var mixed = result.As<AudioBuffer>();
        Assert.Equal(1, mixed.Frames);
        Assert.Equal(1, mixed.Channels);
        Assert.Equal(0.8f, mixed.GetSample(0, 0), precision: 4);
    }
}
