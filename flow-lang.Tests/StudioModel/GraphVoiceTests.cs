using Flow.Audio;
using Flow.Audio.Graph;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class GraphVoiceTests
{
    private static AudioGraphDefinition Instrument()
    {
        var envelope = AudioGraphDefinition.Input("gate", 1).Then("envelope", "flow.adsr",
            new Dictionary<string, double> { ["attackMs"] = 0, ["decayMs"] = 0, ["sustain"] = 1, ["releaseMs"] = 2 });
        return AudioGraphDefinition.Multiply("level", envelope, AudioGraphDefinition.Input("velocity", 2));
    }
    private static ScheduledNote Note(long start, long end, double velocity = 1) => new(Guid.NewGuid(), Guid.NewGuid(), start, end, 100, velocity);
    [Fact]
    public void IndependentVoicesReceiveVelocityGateAndBoundedRelease()
    {
        var playback = new PreparedNotePlayback([Note(0, 2, .5), Note(1, 3, .25)], 1000, 16,
            settings: new(VoiceLimit: 2, VoiceGraph: Instrument()));
        float[] result = new float[10]; Assert.Equal(5, playback.TotalFrames); Assert.Equal(5, playback.Read(result));
        float[] expected = [.5f, .75f, .5f, .125f, 0];
        for (int i = 0; i < expected.Length; i++)
        { Assert.Equal(expected[i], result[i * 2], 6); Assert.Equal(result[i * 2], result[i * 2 + 1]); }
        playback.Reset(); playback.Read(result.AsSpan(0, 4));
        long before = GC.GetAllocatedBytesForCurrentThread(); playback.Read(result.AsSpan(0, 6));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
    [Fact]
    public void StealingSeekAndBlockPartitionsResetVoiceState()
    {
        var notes = new[] { Note(0, 20), Note(4, 12, .3) };
        var graph = AudioGraphDefinition.Multiply("output", Instrument(), AudioGraphDefinition.Input("pitch").Then("osc", "flow.sineOsc"));
        var a = new PreparedNotePlayback(notes, 1000, 32, settings: new(VoiceLimit: 1, VoiceGraph: graph));
        var b = new PreparedNotePlayback(notes, 1000, 32, settings: new(VoiceLimit: 1, VoiceGraph: graph));
        float[] actual = new float[44], expected = new float[44];
        a.Read(actual); b.Read(expected.AsSpan(0, 10)); b.Read(expected.AsSpan(10)); Assert.Equal(expected, actual);
        Assert.Equal(1, a.StolenVoices); Assert.Equal(0, actual[8]);
        a.Seek(6); a.Read(actual.AsSpan(0, 2)); Assert.Equal(0, actual[0]);
        a.Reset(); a.Read(actual); Assert.Equal(expected, actual);
    }
    [Fact]
    public void InvalidPortsAndExcessivePoolWorkAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new PreparedNotePlayback([], 1000, 32,
            settings: new(VoiceGraph: AudioGraphDefinition.Input("bad", 3))));
        var graph = AudioGraphDefinition.Input("gate", 1);
        for (int i = 0; i < 17; i++) graph = graph.Then("gain" + i, "flow.gain");
        Assert.Throws<ArgumentException>(() => new PreparedNotePlayback([], 1000, 32,
            settings: new(VoiceLimit: 256, VoiceGraph: graph)));
    }

    [Fact]
    public void EventSegmentsMatchSingleFrameReferenceAcrossPolyphonyAndSeeks()
    {
        var graph = AudioGraphDefinition.Multiply("sound", Instrument(),
            AudioGraphDefinition.Input("pitch").Then("osc", "flow.sineOsc"))
            .Then("echo", "flow.delay", new Dictionary<string, double>
            { ["timeMs"] = 3, ["repeats"] = 2, ["feedback"] = .4, ["wet"] = .3 });
        var random = new Random(7123);
        var notes = Enumerable.Range(0, 250).Select(i => new ScheduledNote(Guid.NewGuid(), Guid.NewGuid(),
            i * 3, i * 3 + random.Next(1, 90), random.Next(50, 400), random.NextDouble(),
            random.NextDouble() * 2, random.NextDouble() * 2 - 1)).ToArray();
        var settings = new SineVoiceSettings(VoiceLimit: 8, VoiceGraph: graph);
        var reference = new PreparedNotePlayback(notes, 1000, 1, settings: settings);
        var blocked = new PreparedNotePlayback(notes, 1000, 128, settings: settings);
        float[] one = new float[2], block = new float[256];
        foreach (long start in new long[] { 0, 75, 401, 0 })
        {
            reference.Seek(start); blocked.Seek(start);
            while (blocked.PositionFrames < blocked.TotalFrames)
            {
                int count = blocked.Read(block.AsSpan(0, random.Next(1, 129) * 2));
                for (int i = 0; i < count; i++)
                {
                    Assert.Equal(1, reference.Read(one));
                    Assert.Equal(one[0], block[i * 2]); Assert.Equal(one[1], block[i * 2 + 1]);
                }
            }
            Assert.Equal(reference.StolenVoices, blocked.StolenVoices);
        }
        blocked.Reset(); blocked.Read(block);
        long before = GC.GetAllocatedBytesForCurrentThread(); blocked.Read(block);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void LiveBatchUpdatesEveryVoiceAndSurvivesStealSeekAndInvalidBatch()
    {
        var graph = AudioGraphDefinition.Value("level", 1);
        var playback = new PreparedNotePlayback([Note(0, 20), Note(0, 20), Note(10, 30)], 1000, 16,
            settings: new(VoiceLimit: 2, VoiceGraph: graph));
        float[] output = new float[16]; playback.Read(output);
        Assert.All(output, x => Assert.Equal(2, x, 6));
        playback.SetLatestParameters([new("level", "value", .25)]);
        Assert.Throws<ArgumentException>(() => playback.SetLatestParameters([new("level", "value", .9), new("level", "value", .5)]));
        long before = GC.GetAllocatedBytesForCurrentThread(); playback.Read(output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(.5f, output[^1], 6); Assert.Equal(1, playback.StolenVoices);
        playback.Seek(12); playback.Read(output.AsSpan(0, 2)); Assert.Equal(.5f, output[0], 6);
        playback.Reset(); playback.Read(output); Assert.All(output, x => Assert.Equal(.5f, x, 6));
        playback.SetLatestParameters([new("level", "value", .75)]);
        playback.SetLatestParameters([new("level", "value", .5)]);
        playback.Seek(0); playback.Read(output); Assert.All(output, x => Assert.Equal(1, x, 6));
    }

    [Fact]
    public async Task ConcurrentVoiceBatchesNeverExposeHalfAnUpdate()
    {
        var graph = AudioGraphDefinition.Mix("cancel", AudioGraphDefinition.Value("a", 0),
            AudioGraphDefinition.Multiply("negative", AudioGraphDefinition.Value("b", 0), AudioGraphDefinition.Value("sign", -1)));
        var playback = new PreparedNotePlayback([Note(0, 100000), Note(0, 100000)], 1000, 16,
            settings: new(VoiceLimit: 2, VoiceGraph: graph));
        var writer = Task.Run(() =>
        {
            for (int i = 0; i < 10000; i++) playback.SetLatestParameters([new("a", "value", i % 7), new("b", "value", i % 7)]);
        });
        float[] output = new float[32];
        for (int i = 0; i < 3000; i++)
        { playback.Read(output); Assert.All(output, value => Assert.Equal(0, value)); }
        await writer; playback.Read(output); Assert.All(output, value => Assert.Equal(0, value));
    }

    [Fact]
    public void VoiceAutomationUsesProjectTimeOnLateStartsAndHeldSeeks()
    {
        var graph = AudioGraphDefinition.Value("level", 1);
        var lane = new GraphAutomationLane("level", "value", [new(0, 0), new(100, 1)]);
        var playback = new PreparedNotePlayback([Note(50, 150)], 1000, 128,
            settings: new(VoiceLimit: 1, VoiceGraph: graph), graphAutomation: [lane]);
        float[] samples = new float[256]; playback.Read(samples);
        Assert.Equal(.5f, samples[100], 6); Assert.Equal(1, samples[200], 6);
        playback.Seek(75); playback.Read(samples.AsSpan(0, 2)); Assert.Equal(.75f, samples[0], 6);
        Assert.Throws<InvalidOperationException>(() => playback.SetLatestParameters([new("level", "value", .5)]));
        long before = GC.GetAllocatedBytesForCurrentThread(); playback.Read(samples.AsSpan(0, 16));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
