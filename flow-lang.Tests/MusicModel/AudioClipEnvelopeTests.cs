using Flow.Audio;
using Flow.Music.Model;
using Xunit;

namespace FlowLang.Tests.MusicModel;

public class AudioClipEnvelopeTests
{
    private static float[] Render(PreparedPcmPlayback playback)
    {
        var output = new float[playback.TotalFrames * 2];
        for (int offset = 0; offset < output.Length; offset += 6)
            playback.Read(output.AsSpan(offset, Math.Min(6, output.Length - offset)));
        return output;
    }

    [Fact]
    public void SplitWindowsAndSeekPreserveTheSameFadeWithoutTouchingSourceSamples()
    {
        var samples = Enumerable.Repeat(1f, 16).ToArray(); var asset = new PcmAsset(samples, 8000);
        var envelope = new AudioClipEnvelope(0, 8, .5, 2, 2);
        var whole = new PreparedPcmPlayback([new(Guid.NewGuid(), asset, 0, 8, 0, 8, envelope)], 8000, 3);
        var split = new PreparedPcmPlayback([new(Guid.NewGuid(), asset, 0, 3, 0, 3, envelope),
            new(Guid.NewGuid(), asset, 3, 8, 3, 5, envelope)], 8000, 3);
        var expected = new[] { 0f, .25f, .5f, .5f, .5f, .5f, .25f, 0f }.SelectMany(x => new[] { x, x }).ToArray();
        Assert.Equal(expected, Render(whole)); Assert.Equal(expected, Render(split));
        split.Seek(5); var tail = new float[6]; split.Read(tail); Assert.Equal(expected[10..], tail);
        var unchanged = new float[16]; asset.CopyTo(unchanged); Assert.Equal(samples, unchanged);
    }

    [Fact]
    public void OverlapFractionalRateConversionAndLargeFrameAnchorsHaveDefinedGain()
    {
        var envelope = new AudioClipEnvelope(0, 4, 1, 3, 3);
        Assert.Equal(2d / 9, envelope.GainAt(1), 12);
        var asset = new PcmAsset(Enumerable.Repeat(1f, 8).ToArray(), 8000);
        var playback = new PreparedPcmPlayback([new(Guid.NewGuid(), asset, 0, 8, 0, 4, envelope)], 16000, 3);
        var rendered = Render(playback);
        Assert.Equal((float)envelope.GainAt(0, .5), rendered[2]);
        const long anchor = 9007199254740993;
        var large = new AudioClipEnvelope(anchor, 8, 1, 2, 2);
        Assert.Equal(.5, large.GainAt(anchor + 1)); Assert.Equal(.5, large.GainAt(anchor + 6));
        Assert.Equal(0, large.GainAt(anchor + 7));
        Assert.Throws<ArgumentException>(() => new AudioClipEnvelope(0, 4, double.NaN));
        Assert.Throws<ArgumentException>(() => new AudioClipEnvelope(0, 4, 1, 5));
    }
}
