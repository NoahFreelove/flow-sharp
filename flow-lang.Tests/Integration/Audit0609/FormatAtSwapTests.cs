using System.Collections.Generic;
using FlowInterpreter;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit-0609 §5.7 — Sample-rate / channel-count format carried inside the staged buffer.
///
/// Before the fix, StartRenderTask immediately updated _currentSampleRate/_currentChannels
/// on the render thread while the streaming loop continued pushing the OLD buffer
/// with the NEW format values — producing the wrong pitch/speed or broken
/// interleave until the next bar swap.
///
/// The fix: SampleRate/Channels are now fields inside <see cref="LiveBlockBuffer"/>
/// and are applied ONLY at the streaming-loop bar-boundary swap.
/// </summary>
[Collection("FlowScripts")]
public class FormatAtSwapTests : IDisposable
{
    public FormatAtSwapTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    /// <summary>
    /// A <see cref="LiveBlockBuffer"/> constructed with non-default SampleRate/Channels
    /// MUST preserve those values on the record.
    /// </summary>
    [Fact]
    public void LiveBlockBuffer_CarriesSampleRateAndChannels()
    {
        var buf = new LiveBlockBuffer(
            BlockId: 0,
            Bytes: new float[64],
            Length: 64,
            SampleRate: 48000,
            Channels: 1);

        Assert.Equal(48000, buf.SampleRate);
        Assert.Equal(1, buf.Channels);
    }

    /// <summary>
    /// Default constructor (test-seam paths that pre-date §5.7 pass only 3 args)
    /// MUST default to 44100 / 2 so old tests keep compiling and passing.
    /// </summary>
    [Fact]
    public void LiveBlockBuffer_DefaultsToStereo44100()
    {
        var buf = new LiveBlockBuffer(BlockId: 0, Bytes: new float[16], Length: 16);

        Assert.Equal(44100, buf.SampleRate);
        Assert.Equal(2, buf.Channels);
    }

    /// <summary>
    /// The staging path (wrapping a captured buffer into the swap dict) MUST
    /// propagate the buffer's SampleRate and Channels into the LiveBlockBuffer.
    /// This is exercised by constructing a mock LiveBlockBuffer with a non-default
    /// rate and verifying the record round-trips.
    /// </summary>
    [Fact]
    public void LiveBlockBuffer_NonDefaultFormatRoundTrips()
    {
        const int expectedRate = 22050;
        const int expectedChannels = 1;

        var dict = new Dictionary<int, LiveBlockBuffer>
        {
            [0] = new LiveBlockBuffer(
                BlockId: 0,
                Bytes: new float[32],
                Length: 32,
                SampleRate: expectedRate,
                Channels: expectedChannels),
        };

        var entry = dict[0];
        Assert.Equal(expectedRate, entry.SampleRate);
        Assert.Equal(expectedChannels, entry.Channels);
    }
}
