using System;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Network;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit 2026-06-09 §5.13 — an OSC Buffer <c>,b</c> blob must preserve channel
/// count + sample rate across the send/receive round trip. The pre-fix code
/// flattened the buffer with no header and rebuilt it as mono/44100, silently
/// corrupting a well-formed stereo/48k buffer. The fix prefixes a 12-byte
/// header (magic + channels + sampleRate); a headerless foreign blob still
/// decodes charitably as mono/44100 with a one-shot advisory.
///
/// <para>Exercises the PUBLIC surface: <see cref="OscFunctions.AudioBufferToBlob"/>
/// (send-side flatten) → <see cref="OscFunctions.RugOscArgToFlowValue"/> (the
/// receive-side inverse that runs BlobToBuffer on a byte[]).</para>
/// </summary>
[Collection("FlowScripts")]
public class OscBlobRoundTripTests : IDisposable
{
    public OscBlobRoundTripTests() => RenderingDiagnostics.ResetForTesting();
    public void Dispose() => RenderingDiagnostics.ResetForTesting();

    /// <summary>A stereo 48 kHz buffer round-trips with channels=2, rate=48000,
    /// frame count and every sample value intact.</summary>
    [Fact]
    public void Stereo48k_RoundTrips_ChannelsRateAndSamples()
    {
        const int frames = 64;
        const int channels = 2;
        const int sampleRate = 48000;
        var src = new AudioBuffer(frames, channels, sampleRate);
        for (int i = 0; i < src.Data.Length; i++)
            src.Data[i] = (i % 2 == 0) ? (i * 0.001f) : (-i * 0.002f); // distinct L/R

        var blob = OscFunctions.AudioBufferToBlob(src);
        var decoded = OscFunctions.RugOscArgToFlowValue(blob);

        Assert.Equal(BufferType.Instance, decoded.Type);
        var buf = decoded.As<AudioBuffer>();
        Assert.Equal(channels, buf.Channels);
        Assert.Equal(sampleRate, buf.SampleRate);
        Assert.Equal(frames, buf.Frames);
        Assert.Equal(src.Data.Length, buf.Data.Length);
        for (int i = 0; i < src.Data.Length; i++)
            Assert.Equal(src.Data[i], buf.Data[i]);
    }

    /// <summary>A mono 44100 buffer also round-trips exactly (the header is
    /// always present on Flow-emitted blobs, regardless of channel count).</summary>
    [Fact]
    public void Mono44100_RoundTrips_Exactly()
    {
        var src = new AudioBuffer(32, 1, 44100);
        for (int i = 0; i < src.Data.Length; i++) src.Data[i] = i * 0.01f;

        var blob = OscFunctions.AudioBufferToBlob(src);
        var buf = OscFunctions.RugOscArgToFlowValue(blob).As<AudioBuffer>();

        Assert.Equal(1, buf.Channels);
        Assert.Equal(44100, buf.SampleRate);
        Assert.Equal(32, buf.Frames);
        for (int i = 0; i < src.Data.Length; i++) Assert.Equal(src.Data[i], buf.Data[i]);
    }

    /// <summary>A headerless (foreign-app) blob still decodes — charitably as
    /// mono/44100 — and emits the one-shot advisory so interop is preserved.</summary>
    [Fact]
    public void HeaderlessBlob_DecodesAsMono44100_WithAdvisory()
    {
        // 10 raw little-endian floats, NO Flow header.
        var raw = new byte[10 * 4];
        for (int i = 0; i < 10; i++)
            BitConverter.GetBytes(i * 0.5f).CopyTo(raw, i * 4);

        var originalErr = Console.Error;
        var sw = new System.IO.StringWriter();
        Console.SetError(sw);
        AudioBuffer buf;
        try
        {
            buf = OscFunctions.RugOscArgToFlowValue(raw).As<AudioBuffer>();
        }
        finally
        {
            Console.SetError(originalErr);
        }

        Assert.Equal(1, buf.Channels);
        Assert.Equal(44100, buf.SampleRate);
        Assert.Equal(10, buf.Frames);
        for (int i = 0; i < 10; i++) Assert.Equal(i * 0.5f, buf.Data[i]);
        Assert.Contains("[osc]", sw.ToString());
        Assert.Contains("without Flow channel/rate metadata", sw.ToString());
    }
}
