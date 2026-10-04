using Flow.Audio;
using Flow.Audio.Graph;
using FlowLang.Core;
using FlowLang.Hosting;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Daw;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;
namespace FlowLang.Tests.StudioModel;

[Collection("FlowScripts")]
public class DelayGraphTests
{
    private static AudioGraphDefinition Delay(bool bypass = false) => AudioGraphDefinition.Input("input")
        .Then("delay", "flow.delay", new Dictionary<string, double>
        { ["timeMs"] = 2, ["repeats"] = 3, ["feedback"] = .5, ["wet"] = 1 }, bypassed: bypass);
    private static PreparedGraphPlayback Playback(int block = 16) => new(new(Delay(), 1000, block),
        [new PreparedPcmPlayback([new(Guid.NewGuid(), new PcmAsset([1, -1], 1000), 0, 1, 0, 1)], 1000, block)]);
    [Fact]
    public void EchoTrainRendersThroughBoundedTailAndClearsAtEof()
    {
        var playback = Playback(); Assert.Equal(7, playback.TotalFrames);
        var samples = new float[16]; Assert.Equal(7, playback.Read(samples));
        Assert.Equal(new float[] { 0, 0, 0, 0, 1, -1, 0, 0, .5f, -.5f, 0, 0, .25f, -.25f, 0, 0 }, samples);
        Assert.Equal(0, playback.Read(samples)); Assert.All(samples, x => Assert.Equal(0, x));
        playback.Seek(2); playback.Read(samples); Assert.All(samples, x => Assert.Equal(0, x));
        playback.Reset(); playback.Read(samples); Assert.Equal(1, samples[4]);
    }
    [Fact]
    public void TailUsesLongestPathAndBypassHasNoState()
    {
        var chain = Delay().Then("second", "flow.delay", new Dictionary<string, double> { ["timeMs"] = 3, ["repeats"] = 2 });
        Assert.Equal(12, new PreparedAudioGraph(chain, 1000).TailFrames);
        var bypass = new PreparedAudioGraph(Delay(true), 1000); Assert.Equal(0, bypass.TailFrames);
        var output = new float[2]; bypass.Process([1, -1], output); Assert.Equal(new float[] { 1, -1 }, output);
        Assert.Throws<ArgumentException>(() => new PreparedAudioGraph(Delay(), 1000, maxBufferBytes: 1));
        Assert.Throws<ArgumentException>(() => Delay().Then("bad", "flow.delay", new Dictionary<string, double> { ["repeats"] = 1.5 }));
        Assert.Throws<ArgumentException>(() => bypass.TrySetParameter("delay", "timeMs", 10));
    }
    [Fact]
    public void DelayReadAndResetAllocateNothingAndBlockSizeDoesNotChangeSound()
    {
        var full = Playback(); var split = Playback(1); var expected = new float[14]; var actual = new float[14];
        full.Read(expected); for (int i = 0; i < 7; i++) split.Read(actual.AsSpan(i * 2, 2));
        Assert.Equal(expected, actual);
        for (int i = 0; i < 100; i++) { full.Reset(); full.Read(expected); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { full.Reset(); full.Read(expected); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
    [Fact]
    public void FlowPreviewAndExportUseSameTailAndSamplesAsPlayback()
    {
        var input = new AudioBuffer(1, 2, 1000); input.Data[0] = 1; input.Data[1] = -1;
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        engine.Context.DeclareVariable("audio", MusicValue.Buffer(input));
        var result = engine.Evaluate("""
            use "@flowDaw"
            AudioGraph graph = (dawDelay "delay" (dawInput "input" 0) 2ms 3 0.5 1.0)
            (dawProcess graph audio)
            """);
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var buffer = result.LastValue!.As<AudioBuffer>(); var expected = new float[14]; Playback().Read(expected);
        Assert.Equal(expected, buffer.Data);
        var exported = engine.Evaluate(FlowGraphExporter.Export(Delay()));
        Assert.True(exported.Succeeded, string.Join("\n", exported.Errors));
        Assert.Equal(AudioGraphJson.Serialize(Delay()), AudioGraphJson.Serialize(exported.LastValue!.As<AudioGraphDefinition>()));
    }
}
