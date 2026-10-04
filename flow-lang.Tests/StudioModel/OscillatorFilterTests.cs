using Flow.Audio.Graph;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;
namespace FlowLang.Tests.StudioModel;
[Collection("FlowScripts")]
public class OscillatorFilterTests
{
    [Fact]
    public void OscillatorIsStereoPhaseContinuousAcrossBlocksAndResetsDeterministically()
    {
        var definition = AudioGraphDefinition.Input("frequency").Then("osc", "flow.sineOsc");
        var graph = new PreparedAudioGraph(definition, 8000, 128);
        var whole = new PreparedAudioGraph(definition, 8000, 128);
        float[] frequencies = Enumerable.Range(0, 256).Select(i => i % 2 == 0 ? 1000f : 500f).ToArray();
        float[] actual = new float[256], expected = new float[256];
        graph.Process(frequencies.AsSpan(0, 64), actual.AsSpan(0, 64));
        graph.Process(frequencies.AsSpan(64), actual.AsSpan(64)); whole.Process(frequencies, expected);
        Assert.Equal(expected, actual);
        for (int frame = 0; frame < 128; frame++)
        {
            Assert.Equal((float)Math.Sin(2 * Math.PI * (frame % 8) / 8), actual[frame * 2], 6);
            Assert.Equal((float)Math.Sin(2 * Math.PI * (frame % 16) / 16), actual[frame * 2 + 1], 6);
        }
        graph.Reset(1000); graph.Process(frequencies, actual); Assert.Equal(expected, actual);
    }
    [Fact]
    public void LowPassMatchesImpulseRecurrenceAndHasBoundedRenderedTail()
    {
        var definition = AudioGraphDefinition.LowPass("filter", AudioGraphDefinition.Input("audio"), AudioGraphDefinition.Value("cutoff", 1000));
        var graph = new PreparedAudioGraph(definition, 8000, 128);
        float[] input = new float[256], output = new float[256]; input[0] = 1; input[1] = -1;
        graph.Process(input, output); double pole = Math.Exp(-2 * Math.PI * 1000 / 8000);
        for (int i = 0; i < 128; i++)
        { Assert.Equal((float)((1 - pole) * Math.Pow(pole, i)), output[i * 2], 6); Assert.Equal(-output[i * 2], output[i * 2 + 1]); }
        Assert.Equal(16000, graph.TailFrames);
        graph.Reset(); graph.Process(new float[256], output); Assert.All(output, x => Assert.Equal(0, x));
        graph.SetLatestParameter("cutoff", "value", 2000); graph.Process(input, output);
        long before = GC.GetAllocatedBytesForCurrentThread(); graph.Process(input, output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
    [Fact]
    public void ModulatedCutoffGraphExportsThroughPublicFlowAndStaysFinite()
    {
        var lfo = AudioGraphDefinition.Value("rate", 2).Then("lfo", "flow.sineOsc");
        var cutoff = AudioGraphDefinition.Mix("cutoff", AudioGraphDefinition.Value("base", 1000),
            AudioGraphDefinition.Multiply("depth", lfo, AudioGraphDefinition.Value("amount", 500)));
        var definition = AudioGraphDefinition.LowPass("filter", AudioGraphDefinition.Input("audio"), cutoff);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(FlowGraphExporter.Export(definition)); Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Equal(AudioGraphJson.Serialize(definition), AudioGraphJson.Serialize(result.LastValue!.As<AudioGraphDefinition>()));
        var graph = new PreparedAudioGraph(definition); float[] input = Enumerable.Repeat(.5f, 512).ToArray(), output = new float[512];
        graph.Process(input, output); Assert.All(output, x => Assert.True(float.IsFinite(x) && x >= 0 && x <= .5));
    }
}
