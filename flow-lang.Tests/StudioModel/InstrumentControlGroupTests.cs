using Flow.Audio;
using Flow.Audio.Graph;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class InstrumentControlGroupTests
{
    [Fact]
    public async Task ScheduledAndMonitoredVoicesShareOneParameterBoundary()
    {
        var settings = new SineVoiceSettings(VoiceLimit: 2, VoiceGraph: AudioGraphDefinition.Value("level", 0));
        var scheduled = new PreparedNotePlayback([new(Guid.NewGuid(), Guid.NewGuid(), 0, 10000, 440)], 1000, 16, settings: settings);
        var live = new PreparedLiveInstrument(settings, 1000, 16);
        var controls = new PreparedInstrumentControlGroup([scheduled], [live]);
        var difference = AudioGraphDefinition.Mix("difference", AudioGraphDefinition.Input("a", 0),
            AudioGraphDefinition.Multiply("negative", AudioGraphDefinition.Input("b", 1), AudioGraphDefinition.Value("sign", -1)));
        var parent = new PreparedGraphPlayback(new(difference, 1000, 16), [scheduled, new PreparedPcmPlayback([], 1000, 16)],
            instrumentControls: [controls], monitors: new Dictionary<int, PreparedLiveInstrument> { [1] = live });
        live.TryWrite(0x90, 69, 127);
        var writer = Task.Run(() =>
        {
            for (int i = 0; i < 10000; i++) controls.SetLatestParameters([new("level", "value", i % 7)]);
        });
        var output = new float[32];
        for (int i = 0; i < 300; i++) { parent.Read(output); Assert.All(output, x => Assert.Equal(0, x)); }
        await writer;
        controls.SetLatestParameters([new("level", "value", .5)]);
        parent.Read(output); Assert.All(output, x => Assert.Equal(0, x));
    }

    [Fact]
    public async Task SharedInstrumentControlsReachAllTracksAtOneParentBoundary()
    {
        var settings = new SineVoiceSettings(VoiceLimit: 2, VoiceGraph: AudioGraphDefinition.Value("level", 0));
        ScheduledNote[] notes = [new(Guid.NewGuid(), Guid.NewGuid(), 0, 100000, 440)];
        var first = new PreparedNotePlayback(notes, 1000, 16, settings: settings);
        var second = new PreparedNotePlayback(notes, 1000, 16, settings: settings);
        var controls = new PreparedInstrumentControlGroup([first, second]);
        var difference = AudioGraphDefinition.Mix("difference", AudioGraphDefinition.Input("a", 0),
            AudioGraphDefinition.Multiply("invert", AudioGraphDefinition.Input("b", 1), AudioGraphDefinition.Value("sign", -1)));
        var parent = new PreparedGraphPlayback(new(difference, 1000, 16), [first, second], instrumentControls: [controls]);
        var writer = Task.Run(() =>
        {
            for (int i = 0; i < 10000; i++) controls.SetLatestParameters([new("level", "value", i % 11)]);
        });
        float[] output = new float[32];
        for (int i = 0; i < 3000; i++)
        { parent.Read(output); Assert.All(output, x => Assert.Equal(0, x)); }
        await writer;
        controls.SetLatestParameters([new("level", "value", .5)]);
        Assert.Throws<ArgumentOutOfRangeException>(() => controls.SetLatestParameters([new("level", "value", double.NaN)]));
        parent.Seek(0); parent.Read(output); Assert.All(output, x => Assert.Equal(0, x));
        long before = GC.GetAllocatedBytesForCurrentThread(); parent.Read(output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
