using Flow.Audio;
using Flow.Audio.Graph;
using Xunit;

namespace FlowLang.Tests.MusicModel;

public class LiveInstrumentTests
{
    [Fact]
    public void CapturedTuningMatchesScheduledPitchAndUnmappedKeysRemainSilent()
    {
        var frequencies = new double[128]; frequencies[69] = 432;
        var map = new Flow.Music.Model.MidiPitchMap(frequencies);
        frequencies[69] = 440; // Host mutation cannot retune a prepared voice.
        var settings = new SineVoiceSettings(AttackMilliseconds: 0, ReleaseMilliseconds: 0);
        var live = new PreparedLiveInstrument(settings, map, 8000, 16);
        var scheduled = new PreparedNotePlayback([new(Guid.NewGuid(), Guid.NewGuid(), 0, 100, 432, 1)], 8000, 16, settings: settings);
        var actual = new float[32]; var expected = new float[32];
        live.TryWrite(0x90, 60, 127); live.Read(actual);
        Assert.All(actual, value => Assert.Equal(0, value));
        live.TryWrite(0x90, 69, 127); live.Read(actual); scheduled.Read(expected);
        Assert.Equal(expected, actual);
        Assert.Contains(actual, value => value != 0);
        Assert.Throws<ArgumentException>(() => new Flow.Music.Model.MidiPitchMap(new double[127]));
        Assert.Throws<ArgumentException>(() => new Flow.Music.Model.MidiPitchMap(Enumerable.Repeat(double.NaN, 128)));
    }

    private static AudioGraphDefinition Envelope()
    {
        var gate = AudioGraphDefinition.Input("gate", 1).Then("env", "flow.adsr",
            new Dictionary<string, double> { ["attackMs"] = 0, ["decayMs"] = 0, ["sustain"] = 1, ["releaseMs"] = 4 });
        return AudioGraphDefinition.Multiply("output", gate, AudioGraphDefinition.Input("velocity", 2));
    }

    [Theory]
    [InlineData("sine")]
    [InlineData("sample")]
    [InlineData("graph")]
    [InlineData("graph-sample")]
    public void LiveAndScheduledVoicesProduceIdenticalSamples(string kind)
    {
        var sample = new PcmAsset(Enumerable.Range(0, 128).Select(i => (float)Math.Sin(i * .2)).ToArray(), 1000);
        SineVoiceSettings settings = kind switch
        {
            "sine" => new(AttackMilliseconds: 1, ReleaseMilliseconds: 4),
            "sample" => new(AttackMilliseconds: 1, ReleaseMilliseconds: 4, Sample: sample),
            "graph" => new(VoiceGraph: AudioGraphDefinition.Multiply("sound", Envelope(),
                AudioGraphDefinition.Input("pitch").Then("osc", "flow.sineOsc"))),
            _ => new(VoiceGraph: AudioGraphDefinition.Multiply("sound", Envelope(),
                AudioGraphDefinition.Sample("sample", AudioGraphDefinition.Input("pitch"), AudioGraphDefinition.Input("gate", 1), 0, 440)),
                GraphSamples: new(new Dictionary<int, PcmAsset> { [0] = sample }))
        };
        var live = new PreparedLiveInstrument(settings, 1000, 16);
        var scheduled = new PreparedNotePlayback([new(Guid.NewGuid(), Guid.NewGuid(), 0, 8, 440, 1)], 1000, 16, settings: settings);
        var actual = new float[16]; var expected = new float[16];
        Assert.True(live.TryWrite(0x90, 69, 127));
        live.Read(actual); scheduled.Read(expected); Assert.Equal(expected, actual);
        Assert.True(live.TryWrite(0x80, 69, 0));
        live.Read(actual); scheduled.Read(expected); Assert.Equal(expected, actual);
        live.Read(actual); Assert.All(actual, x => Assert.Equal(0, x));
    }

    [Fact]
    public void SustainRetriggerAndChannelPanicPreserveOtherVoices()
    {
        var live = new PreparedLiveInstrument(new(VoiceLimit: 8, VoiceGraph: Envelope()), 1000, 16);
        var one = new float[2];
        live.TryWrite(0x90, 60, 127); live.TryWrite(0x91, 60, 127); live.Read(one);
        Assert.All(one, x => Assert.Equal(2f, x, 6));
        live.TryWrite(0xb0, 64, 127); live.TryWrite(0x80, 60); live.Read(one);
        Assert.All(one, x => Assert.Equal(2f, x, 6));
        live.TryWrite(0xb0, 120, 0); live.Read(one); // Immediate channel-zero silence, including tails.
        Assert.All(one, x => Assert.Equal(1f, x, 6));
        live.TryWrite(0x91, 60, 127); live.Read(one); // Retrigger releases old voice, starts a fresh one.
        Assert.All(one, x => Assert.Equal(1.75f, x, 6));
        live.TryWrite(0xb1, 64, 127); live.TryWrite(0xb1, 123, 0); live.Read(new float[16]);
        live.Read(one); Assert.All(one, x => Assert.Equal(1f, x, 6));
        live.TryWrite(0xb1, 64, 0); live.Read(new float[16]);
        live.Read(one); Assert.All(one, x => Assert.Equal(0, x));
    }

    [Fact]
    public void OverflowClosesAdmissionAndCannotLeaveHeldOrQueuedNotesSounding()
    {
        var live = new PreparedLiveInstrument(new(VoiceGraph: Envelope()), 1000, 16, capacity: 2);
        var output = new float[16];
        live.TryWrite(0x90, 60, 127); live.Read(output); Assert.All(output, x => Assert.Equal(1f, x, 6));
        Assert.True(live.TryWrite(0x90, 62, 127)); Assert.True(live.TryWrite(0x80, 60));
        Assert.False(live.TryWrite(0x80, 62)); Assert.True(live.Faulted); Assert.True(live.AdmissionClosed);
        Assert.Equal(1, live.DroppedMessages); Assert.False(live.TryWrite(0x90, 64, 127));
        live.Read(output); Assert.All(output, x => Assert.Equal(0, x));
        live.Read(output); Assert.All(output, x => Assert.Equal(0, x));
    }

    [Fact]
    public void CommandWorkIsBoundedAndPanicIsIndependentOfTheFifo()
    {
        var live = new PreparedLiveInstrument(new(VoiceGraph: Envelope()), 1000, 16, capacity: 4, maxMessagesPerBlock: 1);
        var output = new float[16];
        live.TryWrite(0x90, 60, 127); live.TryWrite(0x90, 61, 127);
        live.Read(output); Assert.All(output, x => Assert.Equal(1f, x, 6));
        live.Read(output); Assert.All(output, x => Assert.Equal(2f, x, 6));
        for (byte i = 62; i < 66; i++) Assert.True(live.TryWrite(0x90, i, 127));
        live.RequestPanic(); live.Read(output); Assert.All(output, x => Assert.Equal(0, x));
        Assert.False(live.Faulted); Assert.True(live.TryWrite(0x90, 67, 127));
        live.Read(output); Assert.All(output, x => Assert.Equal(1f, x, 6));
        live.CloseAdmission(); live.Read(output); Assert.All(output, x => Assert.Equal(0, x));
    }

    [Fact]
    public void NewlyStartedVoicesSnapToTheSamePublicTargetsAsScheduledVoices()
    {
        var settings = new SineVoiceSettings(VoiceGraph: Envelope().Then("gain", "flow.gain"));
        var live = new PreparedLiveInstrument(settings, 1000, 16);
        var scheduled = new PreparedNotePlayback([new(Guid.NewGuid(), Guid.NewGuid(), 0, 100, 440, 1)], 1000, 16, settings: settings);
        live.SetLatestParameters([new("gain", "gain", .25)]);
        scheduled.SetLatestParameters([new("gain", "gain", .25)]);
        live.TryWrite(0x90, 69, 127);
        var actual = new float[32]; var expected = new float[32];
        live.Read(actual); scheduled.Read(expected);
        Assert.Equal(expected, actual); Assert.Equal(.25f, actual[0], 6);
    }

    [Fact]
    public async Task ClosingAdmissionWhileInputIsPublishingCannotLeaveSoundingNotes()
    {
        var live = new PreparedLiveInstrument(new(VoiceGraph: Envelope()), 1000, 16);
        using var started = new ManualResetEventSlim();
        var producer = Task.Run(() =>
        {
            started.Set();
            for (int i = 0; i < 100000; i++)
                if (!live.TryWrite(0x90, (byte)(i % 128), 127)) break;
        });
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        var output = new float[32];
        for (int i = 0; i < 10; i++) live.Read(output);
        live.CloseAdmission();
        await producer.WaitAsync(TimeSpan.FromSeconds(5));
        live.Read(output); Assert.All(output, x => Assert.Equal(0, x));
        Assert.False(live.TryWrite(0x90, 60, 127));
        live.Read(output); Assert.All(output, x => Assert.Equal(0, x));
    }

    [Fact]
    public void LiveControlsSurviveStealingAndValidAdmissionAndRenderingAllocateNothing()
    {
        var graph = Envelope().Then("gain", "flow.gain");
        var live = new PreparedLiveInstrument(new(VoiceLimit: 1, VoiceGraph: graph), 1000, 16);
        var output = new float[32];
        live.TryWrite(0x90, 60, 127); live.Read(output);
        live.SetLatestParameters([new("gain", "gain", .25)]);
        live.Read(output); Assert.Equal(.25f, output[^1], 6);
        live.TryWrite(0x90, 61, 127); live.Read(output);
        Assert.Equal(1, live.StolenVoices); Assert.Equal(.25f, output[^1], 6);
        live.TryWrite(0x80, 60); live.Read(output); Assert.Equal(.25f, output[^1], 6); // Off for a stolen key cannot release its replacement.
        live.TryWrite(0x80, 61); live.Read(output); Assert.All(output.Skip(8), x => Assert.Equal(0, x));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        { live.TryWrite(0x90, 60, 127); live.Read(output); live.TryWrite(0x80, 60); live.Read(output); }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
