using Flow.Audio;
using Xunit;

namespace FlowLang.Tests.MusicModel;

public class PlaybackWaveWriterTests
{
    private sealed class Playback(long frames, float sample = 1.25f) : IPreparedAudioPlayback
    {
        public int SampleRate => 8000;
        public int MaxBlockFrames => 16;
        public long TotalFrames => frames;
        public long PositionFrames { get; private set; }
        public int Read(Span<float> output)
        { int count = (int)Math.Min(output.Length / 2, frames - PositionFrames); output.Fill(sample); PositionFrames += count; return count; }
        public void Seek(long frame) => PositionFrames = frame;
        public void Reset() => PositionFrames = 0;
    }
    [Fact]
    public void FloatSamplesAboveUnityAndPartialFinalBlockRoundtripExactly()
    {
        using var stream = new MemoryStream();
        PlaybackWaveWriter.Write(stream, new Playback(19)); Assert.Equal(58 + 19 * 8, stream.Length);
        stream.Position = 0; var asset = WaveAssetReader.Read(stream); var samples = new float[38]; asset.CopyTo(samples);
        Assert.All(samples, sample => Assert.Equal(1.25f, sample));
        Assert.True(stream.CanRead);
    }
    [Fact]
    public void OversizeAndUsedCursorsFailBeforeHeaderAndNonfiniteAudioIsRejected()
    {
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentException>(() => PlaybackWaveWriter.Write(stream, new Playback(uint.MaxValue / 8L)));
        Assert.Equal(0, stream.Length);
        var used = new Playback(16); used.Seek(1);
        Assert.Throws<ArgumentException>(() => PlaybackWaveWriter.Write(stream, used)); Assert.Equal(0, stream.Length);
        Assert.Throws<InvalidDataException>(() => PlaybackWaveWriter.Write(stream, new Playback(1, float.NaN)));
    }
}
