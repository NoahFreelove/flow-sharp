using Flow.Audio;
using Xunit;

namespace FlowLang.Tests.MusicModel;

public class CountInTransportTests
{
    private static PreparedPcmPlayback Pcm(int frames, float sample) => new(
        frames == 0 ? [] : [new(Guid.NewGuid(), new PcmAsset(Enumerable.Repeat(sample, frames * 2).ToArray(), 1000), 0, frames, 0, frames)],
        1000, 16);

    [Theory]
    [InlineData(16)]
    [InlineData(21)]
    public void LeadInFreezesProjectAndStartsRecordingAtExactBlockOffset(int leadFrames)
    {
        var transport = new PreparedSineTransport(Pcm(50, .5f)); transport.Seek(7);
        transport.BeginRecording(Pcm(leadFrames, .25f));
        var output = new float[32];
        Assert.Equal(0, transport.Read(output)); Assert.Equal(7, transport.PositionFrames);
        Assert.All(output, value => Assert.Equal(.25f, value));
        if (leadFrames == 16)
        {
            Assert.True(transport.RecordingEnabled); Assert.Equal(16, transport.RecordingStartOffset);
            Assert.Equal(16, transport.Read(output)); Assert.Null(transport.RecordingStartOffset);
            Assert.All(output, value => Assert.Equal(.5f, value)); Assert.Equal(23, transport.PositionFrames);
        }
        else
        {
            Assert.False(transport.RecordingEnabled); Assert.Equal(5, transport.CountInRemainingFrames);
            Assert.Equal(11, transport.Read(output)); Assert.Equal(5, transport.RecordingStartOffset);
            Assert.Equal(18, transport.PositionFrames); Assert.True(transport.RecordingEnabled);
            Assert.All(output[..10], value => Assert.Equal(.25f, value));
            Assert.All(output[10..], value => Assert.Equal(.5f, value));
        }
    }

    [Theory]
    [InlineData("pause")]
    [InlineData("seek")]
    [InlineData("stop")]
    [InlineData("end")]
    public void DiscontinuitiesCancelLeadInWithoutStartingARecording(string action)
    {
        var transport = new PreparedSineTransport(Pcm(50, .5f)); transport.Seek(7);
        transport.BeginRecording(Pcm(21, .25f)); transport.Read(new float[32]);
        switch (action)
        {
            case "pause": transport.Pause(); break;
            case "seek": transport.Seek(9); break;
            case "stop": transport.Stop(); break;
            default: transport.EndRecording(); break;
        }
        Assert.Equal(0, transport.CountInRemainingFrames); Assert.False(transport.RecordingEnabled);
        var output = new float[32]; Assert.Equal(0, transport.Read(output));
        Assert.All(output, value => Assert.Equal(0, value));
    }

    [Fact]
    public void CountedRecordingExtendsAnEmptyProjectWithoutCallbackAllocations()
    {
        var transport = new PreparedSineTransport(Pcm(0, 0)); var output = new float[32];
        transport.BeginRecording(Pcm(17, .25f)); transport.Read(output); transport.Read(output);
        Assert.Equal(15, transport.PositionFrames); Assert.Equal(0, transport.TotalFrames);
        transport.Stop(); transport.BeginRecording(Pcm(1024, .25f));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 128; i++) transport.Read(output);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated); Assert.Equal(1024, transport.PositionFrames);
    }
}
