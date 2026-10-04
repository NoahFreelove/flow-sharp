using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;
namespace FlowLang.Tests.StudioModel;
[Collection("FlowScripts")]
public class GraphInstrumentPersistenceTests
{
    [Fact]
    public void AuthoredInstrumentSurvivesProjectReopenAndExecutableExport()
    {
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate("""
            use "@flowDaw"
            AudioGraph pitch = (dawInput "pitch" 0)
            AudioGraph gate = (dawInput "gate" 1)
            AudioGraph velocity = (dawInput "velocity" 2)
            AudioGraph envelope = (dawAdsr "envelope" gate 2ms 4ms 0.7 10ms)
            AudioGraph tone = (dawMultiply "shaped" (dawSineOsc "osc" pitch) envelope)
            (dawGraphInstrument (dawMultiply "output" tone velocity) 8)
            """);
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var settings = result.LastValue!.As<SineVoiceSettings>();
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var document = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
        var ticket = document.BeginBuild(new(1, Guid.NewGuid(), "generate"), "saved code");
        Assert.True(document.Accept(ticket, new(ticket.Descriptor.SourceId, ticket.Revision, ticket.Context, [],
            instrumentLayers: [new("synth", settings)])));
        var reopened = ProjectJson.Deserialize(ProjectJson.Serialize(document.Snapshot));
        string code = FlowProjectExporter.Export(reopened);
        Assert.Contains("dawGraphInstrument", code);
        var exported = engine.Evaluate(code); Assert.True(exported.Succeeded, string.Join("\n", exported.Errors));
        var restored = exported.LastValue!.As<ProjectSnapshot>();
        Assert.Equal(ProjectJson.Serialize(reopened), ProjectJson.Serialize(restored));
        var restoredSettings = restored.Sources.Values.Single().Result.InstrumentLayers.Single().Instrument;
        ScheduledNote[] notes = [new(Guid.NewGuid(), Guid.NewGuid(), 0, 80, 440, .6)];
        var a = new PreparedNotePlayback(notes, 8000, 256, settings: settings);
        var b = new PreparedNotePlayback(notes, 8000, 256, settings: restoredSettings);
        float[] expected = new float[512], actual = new float[512]; a.Read(expected); b.Read(actual);
        Assert.Equal(expected, actual); Assert.Contains(actual, v => v != 0);
        string content = GeneratedContentJson.Serialize(document.Snapshot.Sources.Values.Single().Result);
        Assert.Throws<System.Text.Json.JsonException>(() => GeneratedContentJson.Deserialize(content.Replace("\"Version\":4", "\"Version\":2"), ticket.Descriptor.SourceId, ticket.Revision, ticket.Context));
    }

#if !FLOW_WEB
    [Theory]
    [InlineData("enveloped-sine.flow")]
    [InlineData("subtractive-synth.flow")]
    [InlineData("sampled-instrument.flow")]
    public async Task ExampleBuildsInWorkerAndCompilesAsRoutedProject(string example)
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
        string source = File.ReadAllText(Path.Combine(FlowLang.Tests.Characterization.Snapshot.RepoRoot, "examples/instruments", example));
        var ticket = doc.BeginBuild(new(1, Guid.NewGuid(), "generate"), source);
        var request = new GeneratorBuildRequest(ticket.Descriptor, ticket.Revision, source, ticket.Context, TimeSpan.FromSeconds(20));
        await using var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll"));
        var built = await worker.BuildAsync(request, TestContext.Current.CancellationToken);
        Assert.True(built.Status == JobStatus.Succeeded, built.Error); Assert.True(doc.Accept(ticket, built.Value!));
        var bindings = doc.Snapshot.Sources.Values.Single().Bindings;
        Guid track = Guid.NewGuid();
        doc.Edit("Route example", p => new(new(p.Arrangement.Id, tempo, meter,
            [new(Guid.NewGuid(), track, ticket.Descriptor.SourceId, "main", 0, 0, 4)]), p.Context, p.Sources.Values,
            new([new(track, "Synth", bindings.Single(b => b.Output.Role == GeneratedRole.Instrument).Id)],
                bindings.Single(b => b.Output.Role == GeneratedRole.Graph).Id)));
        var reopened = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
        var playback = Flow.Studio.Engine.ProjectCompiler.Prepare(reopened, 8000, 256).Playback;
        float[] samples = new float[512]; playback.Read(samples); Assert.Contains(samples, x => x != 0);
    }
#endif
}
