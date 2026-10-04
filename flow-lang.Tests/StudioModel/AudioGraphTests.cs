using Flow.Audio.Graph;
using FlowLang.Core;
using FlowLang.Hosting;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class AudioGraphTests
{
    private static Dictionary<string, double> P(string id, double value) => new() { [id] = value };
    private static AudioGraphDefinition Chain() => AudioGraphDefinition.Input("in")
        .Then("gain", "flow.gain", P("gain", 0.25)).Then("drive", "flow.drive", P("drive", 2));

    [Fact]
    public void MixingProcessesDistinctBusesAndMetersOutput()
    {
        var left = AudioGraphDefinition.Input("left", 0).Then("lg", "flow.gain", P("gain", 0.5));
        var right = AudioGraphDefinition.Input("right", 1).Then("rg", "flow.gain", P("gain", 2));
        var graph = new PreparedAudioGraph(AudioGraphDefinition.Mix("master", left, right), maxBlockFrames: 2);
        var output = new float[4];
        graph.Process([1, -1, 0.5f, -0.5f, 0.1f, 0.2f, 0.3f, 0.4f], output);
        Assert.Equal(new float[] { 0.7f, -0.1f, 0.85f, 0.55f }, output, new FloatComparer());
        var meter = graph.GetMeter("master");
        Assert.Equal(0.85f, meter.PeakLeft, 6);
        Assert.Equal(Math.Sqrt((0.7 * 0.7 + 0.85 * 0.85) / 2), meter.RmsLeft, 6);
    }

    [Fact]
    public void FlowExportReconstructsOrderConnectionsParametersAndBypass()
    {
        var graph = AudioGraphDefinition.Mix("group", Chain(), AudioGraphDefinition.Input("other", 1))
            .Then("bypass", "flow.pan", P("pan", -0.4), bypassed: true);
        string code = FlowGraphExporter.Export(graph);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(code);
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var restored = result.LastValue!.As<AudioGraphDefinition>();
        Assert.Equal(FlowGraphExporter.Export(graph), FlowGraphExporter.Export(restored));
        float[] input = [0.1f, 0.5f, 0.6f, -0.3f, 0.2f, 0.2f, 0.2f, 0.2f];
        var a = new float[4]; var b = new float[4];
        new PreparedAudioGraph(graph).Process(input, a);
        new PreparedAudioGraph(restored).Process(input, b);
        Assert.Equal(a, b);
    }

    [Fact]
    public void EffectOrderChangesSoundAndInstancesHaveIndependentState()
    {
        var reversed = AudioGraphDefinition.Input("in").Then("drive", "flow.drive", P("drive", 2)).Then("gain", "flow.gain", P("gain", 0.25));
        var a = new PreparedAudioGraph(Chain(), sampleRate: 1000);
        var b = new PreparedAudioGraph(reversed, sampleRate: 1000);
        var c = new PreparedAudioGraph(Chain(), sampleRate: 1000);
        var first = new float[2]; var second = new float[2]; var third = new float[2];
        a.Process([1, 1], first); b.Process([1, 1], second); c.Process([1, 1], third);
        Assert.NotEqual(first[0], second[0]);
        Assert.Equal(first, third);
        a.TrySetParameter("gain", "gain", 0);
        a.Process([1, 1], first); c.Process([1, 1], third);
        Assert.NotEqual(first[0], third[0]);
    }

    [Fact]
    public void ParametersRampAcrossBlocksQueueIsBoundedAndResetUsesTarget()
    {
        var graph = new PreparedAudioGraph(AudioGraphDefinition.Input("in").Then("gain", "flow.gain", P("gain", 0)),
            sampleRate: 1000, maxBlockFrames: 3, parameterCapacity: 1);
        Assert.True(graph.TrySetParameter("gain", "gain", 1));
        Assert.False(graph.TrySetParameter("gain", "gain", 0.5));
        var output = new float[6];
        graph.Process([1, 1, 1, 1, 1, 1], output);
        Assert.Equal(new float[] { 0.2f, 0.2f, 0.4f, 0.4f, 0.6f, 0.6f }, output, new FloatComparer());
        graph.Process([1, 1, 1, 1], output.AsSpan(0, 4));
        Assert.Equal(0.8f, output[0], 6);
        Assert.Equal(1, output[2]);
        Assert.True(graph.TrySetParameter("gain", "gain", 0.3));
        graph.Reset();
        Assert.Equal(default, graph.GetMeter("gain"));
        graph.Process([1, 1], output.AsSpan(0, 2));
        Assert.Equal(0.3f, output[0], 6);
    }

    [Fact]
    public void PanMatchesExistingFlowBufferKernel()
    {
        var buffer = new AudioBuffer(3, 2, 48000);
        new float[] { 0.2f, -0.5f, 0.8f, 0.3f, 1, -1 }.CopyTo(buffer.Data, 0);
        var expected = Panner.Apply(buffer, 0.25f);
        var actual = new float[6];
        new PreparedAudioGraph(AudioGraphDefinition.Input("in").Then("pan", "flow.pan", P("pan", 0.25)))
            .Process(buffer.Data, actual);
        Assert.Equal(expected.Data, actual);
    }

    [Fact]
    public void InvalidGraphsAndInputsFailBeforeProcessing()
    {
        Assert.Throws<ArgumentException>(() => new PreparedAudioGraph(new([new("a", "flow.gain", 1, ["b"]), new("b", "flow.gain", 1, ["a"])], "a")));
        Assert.Throws<ArgumentException>(() => new PreparedAudioGraph(new([new("a", "flow.gain", 1, ["missing"])], "a")));
        Assert.Throws<ArgumentException>(() => new PreparedAudioGraph(new([new("a", "flow.input", 1, []), new("b", "flow.input", 1, [])], "a")));
        Assert.Throws<ArgumentException>(() => new PreparedAudioGraph(Chain(), maxBufferBytes: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Chain().Then("bad", "flow.gain", P("gain", double.NaN)));
        Assert.Throws<ArgumentException>(() => Chain().Then("bad", "flow.gain", version: 999));
        var graph = new PreparedAudioGraph(Chain());
        var output = new float[] { 17, 17 };
        Assert.Throws<ArgumentException>(() => graph.Process([], output));
        Assert.Equal(new float[] { 17, 17 }, output);
    }

    [Fact]
    public void PreparedProcessingAllocatesNoManagedMemory()
    {
        var graph = new PreparedAudioGraph(Chain());
        var input = new float[512]; var output = new float[512];
        Array.Fill(input, 0.2f);
        for (int i = 0; i < 100; i++) graph.Process(input, output);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) graph.Process(input, output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void FlowBufferPreviewUsesPreparedGraphAndDoesNotChangeInput()
    {
        var input = new AudioBuffer(513, 2, 48000);
        Array.Fill(input.Data, 0.2f);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        engine.Context.DeclareVariable("audio", FlowLang.TypeSystem.SpecialTypes.MusicValue.Buffer(input));
        var result = engine.Evaluate("""
            use "@flowDaw"
            AudioGraph graph = (dawDrive "drive" (dawGain "gain" (dawInput "input" 0) 0.25) 2.0)
            (dawProcess graph audio)
            """);
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var output = result.LastValue!.As<AudioBuffer>();
        var expected = new float[input.Data.Length];
        var processor = new PreparedAudioGraph(Chain(), 48000, 513);
        processor.Process(input.Data, expected);
        Assert.Equal(expected, output.Data);
        Assert.All(input.Data, sample => Assert.Equal(0.2f, sample));
    }

    [Fact]
    public void TinyAndNegativeParametersExportAsValidFlowNumbers()
    {
        var graph = AudioGraphDefinition.Input("input").Then("tiny", "flow.gain", P("gain", 1e-20))
            .Then("pan", "flow.pan", P("pan", -0.00000001));
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(FlowGraphExporter.Export(graph));
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var restored = result.LastValue!.As<AudioGraphDefinition>();
        Assert.Equal(1e-20, restored.Nodes.Single(n => n.Id == "tiny").Parameters["gain"]);
        Assert.Equal(-0.00000001, restored.Nodes.Single(n => n.Id == "pan").Parameters["pan"]);
    }

    private sealed class FloatComparer : IEqualityComparer<float>
    {
        public bool Equals(float a, float b) => Math.Abs(a - b) < 0.000001f;
        public int GetHashCode(float obj) => 0;
    }
}
