using Flow.Audio;
using Flow.Audio.Graph;
using Xunit;

namespace FlowLang.Tests.MusicModel;

public class MonitoringMixTests
{
    private static PreparedLiveInstrument Live() => new(new(VoiceGraph:
        AudioGraphDefinition.Input("gate", 1).Then("level", "flow.gain", new Dictionary<string, double> { ["gain"] = .2 })), 1000, 16);
    private static AudioGraphDefinition Mixer() => AudioGraphDefinition.Input("track").Then("drive", "flow.drive",
        new Dictionary<string, double> { ["drive"] = 2 });

    [Fact]
    public void ClipAndMonitorAreSummedBeforeNonlinearEffectsAndStoppedMonitoringSkipsClips()
    {
        var asset = new PcmAsset(Enumerable.Repeat(.3f, 16).ToArray(), 1000);
        var clip = new PreparedPcmPlayback([new(Guid.NewGuid(), asset, 0, 8, 0, 8)], 1000, 16);
        var live = Live();
        var playback = new PreparedGraphPlayback(new(Mixer(), 1000, 16), [clip], monitors: new Dictionary<int, PreparedLiveInstrument> { [0] = live });
        var transport = new PreparedSineTransport(playback);
        var output = new float[16];
        live.TryWrite(0x90, 60, 127);
        Assert.Equal(0, transport.Read(output)); // Audition while stopped.
        Assert.All(output, x => Assert.Equal((float)Math.Tanh(.4), x, 6));
        Assert.Equal(0, clip.PositionFrames); Assert.Equal(0, transport.PositionFrames);
        transport.Play(); Assert.Equal(4, transport.Read(output.AsSpan(0, 8)));
        Assert.All(output.Take(8), x => Assert.Equal((float)Math.Tanh(1), x, 6));
        transport.Pause(); Assert.Equal(0, transport.Read(output));
        Assert.All(output, x => Assert.Equal((float)Math.Tanh(.4), x, 6));
        Assert.Equal(4, clip.PositionFrames); Assert.Equal(4, transport.PositionFrames);
        transport.Play(); Assert.Equal(4, transport.Read(output.AsSpan(0, 8)));
        Assert.All(output.Take(8), x => Assert.Equal((float)Math.Tanh(1), x, 6));
        Assert.Equal(8, playback.TotalFrames); Assert.Equal(TransportState.Stopped, transport.State);
        transport.Read(output); Assert.All(output, x => Assert.Equal((float)Math.Tanh(.4), x, 6));
        Assert.Equal(8, transport.PositionFrames);
        transport.Stop(); transport.Read(output); Assert.All(output, x => Assert.Equal(0, x));
        live.TryWrite(0x90, 60, 127); transport.Read(output);
        Assert.All(output, x => Assert.Equal((float)Math.Tanh(.4), x, 6));
    }

    [Fact]
    public void PausedMonitoringLetsExistingMixerTailsDecayWithoutReplayingClips()
    {
        var asset = new PcmAsset([1f, 1f], 1000);
        var clip = new PreparedPcmPlayback([new(Guid.NewGuid(), asset, 0, 1, 0, 1)], 1000, 16);
        var live = Live();
        var graph = AudioGraphDefinition.Input("track").Then("echo", "flow.delay", new Dictionary<string, double>
            { ["timeMs"] = 2, ["repeats"] = 1, ["feedback"] = .5, ["wet"] = 1 });
        var playback = new PreparedGraphPlayback(new(graph, 1000, 16), [clip], monitors: new Dictionary<int, PreparedLiveInstrument> { [0] = live });
        var transport = new PreparedSineTransport(playback); transport.Play();
        transport.Read(new float[2]); transport.Pause();
        var output = new float[4]; Assert.Equal(0, transport.Read(output));
        Assert.Equal(new float[] { 0, 0, 1, 1 }, output);
        transport.Read(output); Assert.All(output, x => Assert.Equal(0, x));
        Assert.Equal(1, transport.PositionFrames); Assert.Equal(1, clip.PositionFrames);
    }

    [Fact]
    public void DisablingMonitoringSilencesAutonomousMixerGraphsWhileStopped()
    {
        var live = Live();
        var source = new PreparedGraphPlayback(new(AudioGraphDefinition.Mix("mix", AudioGraphDefinition.Input("track"), AudioGraphDefinition.Value("constant", .2)), 1000, 16),
            [new PreparedPcmPlayback([], 1000, 16)], monitors: new Dictionary<int, PreparedLiveInstrument> { [0] = live });
        var transport = new PreparedSineTransport(source); var output = new float[32];
        transport.Read(output); Assert.All(output, x => Assert.Equal(.2f, x));
        live.CloseAdmission(); source.DisableMonitoring(); transport.Read(output);
        Assert.All(output, x => Assert.Equal(0, x)); Assert.Equal(TransportState.Stopped, transport.State);
    }

    [Fact]
    public void FrozenAutomationDoesNotFreezeOrResetDspAndCanRepositionBackwards()
    {
        var graph = AudioGraphDefinition.Value("pitch", 100).Then("osc", "flow.sineOsc").Then("gain", "flow.gain");
        var lane = new GraphAutomationLane("gain", "gain", [new(0, 0), new(100, 1)]);
        var frozen = new PreparedAudioGraph(graph, 1000, 16, automation: [lane]);
        var referenceGraph = AudioGraphDefinition.Value("pitch", 100).Then("osc", "flow.sineOsc").Then("gain", "flow.gain",
            new Dictionary<string, double> { ["gain"] = .1 });
        var reference = new PreparedAudioGraph(referenceGraph, 1000, 16);
        var actual = new float[32]; var expected = new float[32];
        for (int i = 0; i < 2; i++)
        {
            frozen.ProcessAt([], actual, 10, false); reference.Process([], expected);
            Assert.Equal(expected, actual);
        }
        frozen.ProcessAt([], actual, 5, false); reference.Process([], expected);
        for (int i = 0; i < actual.Length; i++) Assert.Equal(expected[i] * .5f, actual[i], 6);
    }

    [Fact]
    public void FrozenDelayAutomationRetainsItsHistoryAcrossTimelineRepositioning()
    {
        var graph = AudioGraphDefinition.Input("in").Then("echo", "flow.delay", new Dictionary<string, double>
            { ["timeMs"] = 2, ["repeats"] = 1, ["feedback"] = .5, ["wet"] = 0 });
        var prepared = new PreparedAudioGraph(graph, 1000, 16,
            automation: [new GraphAutomationLane("echo", "wet", [new(0, 0), new(10, 1)])]);
        var output = new float[8];
        prepared.ProcessAt(Enumerable.Repeat(1f, 8).ToArray(), output, 0, false);
        Assert.All(output, x => Assert.Equal(1, x));
        prepared.ProcessAt(new float[8], output, 10, false);
        Assert.Equal(new float[] { 1, 1, 1, 1, 0, 0, 0, 0 }, output);
    }

    [Fact]
    public void PausedVoiceAutomationHoldsWhileItsEnvelopeAndMonitoringClockAdvance()
    {
        var envelope = AudioGraphDefinition.Input("gate", 1).Then("env", "flow.adsr", new Dictionary<string, double>
            { ["attackMs"] = 4, ["decayMs"] = 0, ["sustain"] = 1, ["releaseMs"] = 4 }).Then("gain", "flow.gain");
        var live = new PreparedLiveInstrument(new(VoiceGraph: envelope), 1000, 16,
            automation: [new GraphAutomationLane("gain", "gain", [new(0, 0), new(100, 1)])]);
        live.TryWrite(0x90, 60, 127);
        var output = new float[8]; live.ReadAt(output, 50, false);
        Assert.Equal(.125f, output[0], 6); Assert.Equal(.5f, output[^1], 6);
        live.ReadAt(output, 50, false); Assert.All(output, x => Assert.Equal(.5f, x, 6));
        live.TryWrite(0x80, 60); live.ReadAt(output, 50, false);
        Assert.Equal(.375f, output[0], 6); Assert.Equal(0f, output[^1], 6);
        Assert.Equal(12, live.PositionFrames);
    }

    [Fact]
    public void EmptyRecordingMonitorsBeyondEofAndDoesNotChangeExportDurationOrAllocate()
    {
        var live = Live(); var source = new PreparedPcmPlayback([], 1000, 16);
        var playback = new PreparedGraphPlayback(new(Mixer(), 1000, 16), [source], monitors: new Dictionary<int, PreparedLiveInstrument> { [0] = live });
        var transport = new PreparedSineTransport(playback);
        live.TryWrite(0x90, 60, 127); transport.BeginRecording();
        var output = new float[32]; transport.Read(output);
        Assert.All(output, x => Assert.Equal((float)Math.Tanh(.4), x, 6));
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) transport.Read(output);
        Assert.Equal(allocated, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(1616, transport.PositionFrames); Assert.Equal(0, playback.PositionFrames);
        Assert.Equal(0, playback.TotalFrames); Assert.Equal(0, source.PositionFrames);
        transport.EndRecording(); transport.Read(output);
        Assert.Equal(0, transport.PositionFrames); Assert.Equal(TransportState.Stopped, transport.State);
        Assert.All(output, x => Assert.Equal((float)Math.Tanh(.4), x, 6));
    }
}
