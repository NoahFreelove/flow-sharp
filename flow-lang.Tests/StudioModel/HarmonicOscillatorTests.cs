using Flow.Audio.Graph;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;
namespace FlowLang.Tests.StudioModel;
[Collection("FlowScripts")]
public class HarmonicOscillatorTests
{
    [Theory]
    [InlineData("flow.sawOsc", "dawSawOsc")]
    [InlineData("flow.squareOsc", "dawSquareOsc")]
    public void StereoModulationPartitionsResetAndExportAreStable(string device, string function)
    {
        var definition = AudioGraphDefinition.Input("hz").Then("osc", device);
        var whole = new PreparedAudioGraph(definition, 8000, 128);
        var split = new PreparedAudioGraph(definition, 8000, 128);
        float[] input = Enumerable.Range(0, 256).Select(i => i % 2 == 0 ? 1000f : -500f).ToArray();
        float[] actual = new float[256], expected = new float[256];
        whole.Process(input, expected); split.Process(input.AsSpan(0, 30), actual.AsSpan(0, 30));
        split.Process(input.AsSpan(30), actual.AsSpan(30)); Assert.Equal(expected, actual);
        Assert.All(actual, x => Assert.True(float.IsFinite(x) && Math.Abs(x) <= 1));
        Assert.Equal(0, actual[0]); Assert.Equal(0, actual[1]);
        split.Reset(); split.Process(input, actual); Assert.Equal(expected, actual);
        long before = GC.GetAllocatedBytesForCurrentThread(); split.Process(input, actual);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(FlowGraphExporter.Export(definition)); Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Equal(AudioGraphJson.Serialize(definition), AudioGraphJson.Serialize(result.LastValue!.As<AudioGraphDefinition>()));
        var authored = engine.Evaluate($"({function} \"osc\" (dawInput \"hz\" 0))");
        Assert.True(authored.Succeeded, string.Join("\n", authored.Errors));
        Assert.Equal(AudioGraphJson.Serialize(definition), AudioGraphJson.Serialize(authored.LastValue!.As<AudioGraphDefinition>()));
    }
    [Fact]
    public void SawCorrectionReducesRepresentativeFoldedHarmonic()
    {
        const int rate = 8192, frequency = 1500;
        var graph = new PreparedAudioGraph(AudioGraphDefinition.Input("hz").Then("osc", "flow.sawOsc"), rate, rate);
        var input = Enumerable.Repeat((float)frequency, rate * 2).ToArray(); var output = new float[input.Length];
        graph.Process(input, output);
        double naiveReal = 0, naiveImag = 0, real = 0, imag = 0;
        for (int i = 0; i < rate; i++)
        {
            double phase = (double)(i * frequency % rate) / rate;
            double angle = 2 * Math.PI * 3692 * i / rate; // Folded third harmonic.
            naiveReal += (2 * phase - 1) * Math.Cos(angle); naiveImag += (2 * phase - 1) * Math.Sin(angle);
            real += output[i * 2] * Math.Cos(angle); imag += output[i * 2] * Math.Sin(angle);
        }
        Assert.True(real * real + imag * imag < .5 * (naiveReal * naiveReal + naiveImag * naiveImag));
    }
}
