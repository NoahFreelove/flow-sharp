using System;
using FlowLang.Core;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.TestFramework;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-04 Wave 0 — assertWithinDb gate (TEST-01 RMS-windowed
/// equality primitive at the SPEC-8 locked ±0.5 dB / 100 ms tolerance).
///
/// Two facts pin the C# pure-comparator surface (<see cref="RmsComparator"/>):
///   1. Identical buffers produce 0 dB deviation — assertion does not throw.
///   2. A buffer whose amplitude differs by &gt; 0.5 dB in any 100 ms window
///      exceeds the locked tolerance and trips the assertion.
///
/// RED state: requires <c>FlowLang.StandardLibrary.TestFramework.RmsComparator</c>
/// + <c>AssertionException</c> + <c>AssertionHelpers</c> — all land in Task 2.
/// </summary>
public class AssertWithinDbTests
{
    private const int SampleRate = 44100;
    private const int Frames = SampleRate; // 1 second of audio

    [Fact]
    public void IdenticalBuffersPass()
    {
        var a = BuildSineBuffer(amplitude: 0.5f);
        var b = BuildSineBuffer(amplitude: 0.5f);

        var deviation = RmsComparator.MaxWindowDeviationDb(a, b, windowMs: 100.0);
        Assert.True(deviation <= 0.001,
            $"Identical buffers should yield ~0 dB deviation; got {deviation:F4} dB.");

        // AssertionHelpers wraps the comparator and is what (assertWithinDb) calls.
        AssertionHelpers.AssertWithinDbOrThrow(a, b, toleranceDb: 0.5);
    }

    [Fact]
    public void DeviationExceedingToleranceThrows()
    {
        // Build two buffers whose RMS differs by ~6 dB (one is half the
        // amplitude of the other). 6 dB FAR exceeds the 0.5 dB tolerance.
        var loud = BuildSineBuffer(amplitude: 0.5f);
        var quiet = BuildSineBuffer(amplitude: 0.25f);

        var deviation = RmsComparator.MaxWindowDeviationDb(loud, quiet, windowMs: 100.0);
        Assert.True(deviation > 0.5,
            $"6 dB amplitude split should produce > 0.5 dB deviation; got {deviation:F4} dB.");

        var ex = Assert.Throws<AssertionException>(() =>
            AssertionHelpers.AssertWithinDbOrThrow(loud, quiet, toleranceDb: 0.5));
        Assert.Contains("assertWithinDb", ex.Message);
    }

    private static AudioBuffer BuildSineBuffer(float amplitude)
    {
        var buf = new AudioBuffer(Frames, channels: 1, sampleRate: SampleRate);
        const double frequency = 440.0;
        for (int i = 0; i < Frames; i++)
        {
            double phase = 2.0 * Math.PI * frequency * i / SampleRate;
            buf.SetSample(i, channel: 0, value: amplitude * (float)Math.Sin(phase));
        }
        return buf;
    }
}
