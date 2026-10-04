using Flow.Audio.Graph;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;
namespace FlowLang.Tests.StudioModel;
[Collection("FlowScripts")]
public class FlowAutomationTests
{
    [Fact]
    public void ExportedCurvesReconstructExactlyAndPreviewMatchesSharedProjectLowering()
    {
        var lane = new ProjectAutomationLane(Guid.NewGuid(), Guid.NewGuid(), "gain", "gain", [new(0, 0), new(4, 1)]);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var built = engine.Evaluate(FlowAutomationExporter.Export([lane]));
        Assert.True(built.Succeeded, string.Join("\n", built.Errors));
        var restored = built.LastValue!.As<List<Value>>().Single().As<ProjectAutomationLane>();
        Assert.Equal(lane.Id, restored.Id); Assert.Equal(lane.GraphBinding, restored.GraphBinding);
        Assert.Equal(lane.Points, restored.Points); Assert.Equal(lane.Shape, restored.Shape);
        var graph = AudioGraphDefinition.Input("input").Then("gain", "flow.gain");
        var input = new AudioBuffer(4000, 2, 1000); Array.Fill(input.Data, 1f);
        engine.Context.DeclareVariable("audio", MusicValue.Buffer(input));
        engine.Context.DeclareVariable("curves", built.LastValue);
        engine.Context.DeclareVariable("graph", new Value(graph, AudioGraphType.Instance));
        var result = engine.Evaluate("""
            Dict<Double, Double> tempoMap = (dict 0.0 120.0 2.0 60.0)
            (dawProcess graph audio curves tempoMap)
            """);
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var actual = result.LastValue!.As<AudioBuffer>();
        var processor = new PreparedAudioGraph(graph, 1000, 256, automation:
            [MusicalAutomationCompiler.Lower(lane, new([new(0, 120), new(2, 60)]), 1000)]);
        var expected = new float[input.Data.Length];
        for (int offset = 0; offset < 4000; offset += 256)
        {
            int count = Math.Min(256, 4000 - offset);
            processor.Process(input.Data.AsSpan(offset * 2, count * 2), expected.AsSpan(offset * 2, count * 2));
        }
        Assert.Equal(expected, actual.Data); Assert.Equal(.75f, actual.Data[4000]);
        var multiple = engine.Evaluate("""
            Buffers buses = (list audio audio)
            AudioGraph mixed = (dawGain "gain" (dawMix "sum" (dawInput "a" 0) (dawInput "b" 1)) 1.0)
            (dawProcess mixed buses curves tempoMap)
            """);
        Assert.True(multiple.Succeeded, string.Join("\n", multiple.Errors));
        Assert.Equal(expected.Select(x => x * 2), multiple.LastValue!.As<AudioBuffer>().Data);
    }
    [Fact]
    public void StepAndTinyNegativeValuesExportAsValidFlow()
    {
        var lane = new ProjectAutomationLane(Guid.NewGuid(), Guid.NewGuid(), "pan", "pan",
            [new(0, -1e-8), new(.0000001, .5)], AutomationShape.Step);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(FlowAutomationExporter.Export([lane]));
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var copy = result.LastValue!.As<List<Value>>().Single().As<ProjectAutomationLane>();
        Assert.Equal(lane.Points, copy.Points); Assert.Equal(AutomationShape.Step, copy.Shape);
    }
}
