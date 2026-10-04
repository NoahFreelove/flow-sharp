using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class ProjectTuningTests
{
    [Theory]
    [InlineData("JustIntonation", "Dminor")]
    [InlineData("Pythagorean", "Ebmajor")]
    public void MidiResolutionMatchesFlowGeneratedPitch(string system, string key)
    {
        var tuning = new ProjectTuning(system, key);
        var context = new GenerationContext(0, 7, new([new(0, 120)]), new([new(1, 4, 4)]), tuning: tuning);
        var built = FlowDawGenerator.Build(new(new(1, Guid.NewGuid(), "generate"), 1,
            FlowDawGenerator.Template, context, TimeSpan.FromSeconds(10)));
        Assert.True(built.Status == JobStatus.Succeeded, built.Error);
        Assert.Equal(Frequency(built.Value!), GeneratorTuning.ResolveMidi(tuning)[69], 10);
    }

    [Fact]
    public void RecordingCapturesResolvedPitchAndOmitsUnmappedKeys()
    {
        var tuning = new ProjectTuning(scala: Scale, keyboardMap: "12\n69\n69\n60\n69\n432.0\n0\n0\n1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n11\n");
        var context = new GenerationContext(0, 7, new([new(0, 120)]), new([new(1, 4, 4)]), tuning: tuning);
        var snapshot = new ProjectSnapshot(new(Guid.NewGuid(), context.Tempo, context.Meter), context);
        var map = GeneratorTuning.ResolveMidi(tuning);
        Assert.Equal(0, map[60]); Assert.Equal(432, map[69], 8);
        var session = new Flow.Studio.Engine.MidiRecordingSession(snapshot, 0, 0, 1000, midiPitchMap: map);
        session.TryCapture(new(0, 0x90, 60, 127)); session.TryCapture(new(0, 0x90, 69, 127));
        session.TryCapture(new(100, 0x80, 60, 0)); session.TryCapture(new(100, 0x80, 69, 0));
        session.RequestStop(200); session.Poll();
        Assert.Equal(Flow.Studio.Engine.MidiRecordingState.Completed, session.State);
        Assert.Equal(432, Assert.Single(session.Take!.Notes).Pitch!.FrequencyHz, 8);
        Assert.Throws<ArgumentException>(() => new Flow.Studio.Engine.MidiRecordingSession(snapshot, 0, 0, 1000));
    }

    private const string Scale = "Captured ET\n12\n100.0\n200.0\n300.0\n400.0\n500.0\n600.0\n700.0\n800.0\n900.0\n1000.0\n1100.0\n2/1\n";
    private const string Map = "0\n0\n127\n60\n69\n432.0\n0\n";
    private static double Frequency(GeneratedSourceOutput output) => output.ScoreLayers[0].Composition.Placements[0].Section.Sequences[0].Notes[0].Pitch!.FrequencyHz;
    [Fact]
    public async Task CapturedScalaChangesGeneratedPitchInDirectAndIsolatedBuilds()
    {
        var tuning = new ProjectTuning(scala: Scale, keyboardMap: Map);
        var context = new GenerationContext(0, 7, new([new(0, 120)]), new([new(1, 4, 4)]), tuning: tuning);
        var request = new GeneratorBuildRequest(new(1, Guid.NewGuid(), "generate"), 1, FlowDawGenerator.Template, context, TimeSpan.FromSeconds(10));
        var built = FlowDawGenerator.Build(request);
        Assert.True(built.Status == JobStatus.Succeeded, built.Error); Assert.Equal(432, Frequency(built.Value!), 8);
        var overridden = FlowDawGenerator.Build(request with { Source = "enable equalTemperament;\n" + request.Source });
        Assert.True(overridden.Status == JobStatus.Succeeded, overridden.Error); Assert.Equal(440, Frequency(overridden.Value!), 8);
#if !FLOW_WEB
        await using var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll"));
        var isolated = await worker.BuildAsync(request, TestContext.Current.CancellationToken);
        Assert.True(isolated.Status == JobStatus.Succeeded, isolated.Error); Assert.Equal(432, Frequency(isolated.Value!), 8);
#else
        await Task.CompletedTask;
#endif
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), context.Tempo, context.Meter), context));
        var ticket = doc.BeginBuild(request.Descriptor, request.Source);
        Assert.True(doc.Accept(ticket, new(request.Descriptor.SourceId, ticket.Revision, ticket.Context, built.Value!.ScoreLayers)));
        var before = doc.Snapshot;
        GeneratorTuning.Set(doc, new());
        Assert.Equal(432, Frequency(doc.Snapshot.Sources[request.Descriptor.SourceId].Result), 8);
        var loaded = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
        Assert.Equal(new ProjectTuning(), loaded.Context.Tuning);
        Assert.Equal(tuning, loaded.Sources[request.Descriptor.SourceId].Result.Context.Tuning);
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var exported = engine.Evaluate(FlowProjectExporter.Export(before));
        Assert.True(exported.Succeeded, string.Join("\n", exported.Errors));
        Assert.Equal(tuning, exported.LastValue!.As<ProjectSnapshot>().Context.Tuning);
    }
    [Fact]
    public void MalformedCapturedScaleFailsBeforeEditingDocument()
    {
        var context = new GenerationContext(0, 7, new([new(0, 120)]), new([new(1, 4, 4)]));
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), context.Tempo, context.Meter), context));
        var before = doc.Snapshot;
        Assert.ThrowsAny<Exception>(() => GeneratorTuning.Set(doc, new(scala: "invalid")));
        Assert.Same(before, doc.Snapshot); Assert.Equal(0, doc.History.UndoCount);
    }
}
