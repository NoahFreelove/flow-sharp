using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Studio.Engine;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using System.Text.Json.Nodes;
using Xunit;
namespace FlowLang.Tests.StudioModel;

public class ProjectTrackCommandTests
{
    [Fact]
    public void ColorPersistsAndExportsWithoutChangingRoutingOrSound()
    {
        var doc = Create(); var initial = doc.Snapshot;
        var id = initial.Routing.Tracks[0].Id; var audio = Render(initial);
        int count = doc.History.UndoCount;
        ProjectTrackCommands.SetColor(doc, id, 0x12ABEF);
        var colored = doc.Snapshot;
        Assert.Equal(count + 1, doc.History.UndoCount);
        ProjectTrackCommands.SetColor(doc, id, 0x12ABEF);
        Assert.Equal(count + 1, doc.History.UndoCount);
        Assert.Equal(audio, Render(colored));
        Assert.Equal(initial.Routing.Tracks[0] with { ColorRgb = 0x12ABEF }, colored.Routing.Tracks[0]);
        Assert.True(doc.History.Undo()); Assert.Same(initial, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(colored, doc.Snapshot);
        var reopened = ProjectJson.Deserialize(ProjectJson.Serialize(colored));
        Assert.Equal(colored.Routing.Tracks, reopened.Routing.Tracks);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var exported = engine.Evaluate(FlowProjectExporter.Export(reopened));
        Assert.True(exported.Succeeded, string.Join("\n", exported.Errors));
        var rebuilt = exported.LastValue!.As<ProjectSnapshot>();
        Assert.Equal(colored.Routing.Tracks, rebuilt.Routing.Tracks);
        Assert.Equal(audio, Render(rebuilt));
        ProjectTrackCommands.SetColor(doc, id, null);
        Assert.Null(doc.Snapshot.Routing.Tracks[0].ColorRgb);
        Assert.True(doc.History.Undo()); Assert.Same(colored, doc.Snapshot);
        foreach (int invalid in new[] { -1, 0x1000000 })
            Assert.Throws<ArgumentException>(() => ProjectTrackCommands.SetColor(doc, id, invalid));
        Assert.Same(colored, doc.Snapshot);
        Assert.Equal(count + 1, doc.History.UndoCount);
        var reset = engine.Evaluate($"(dawTrackColor (dawTrackColor (dawTrack \"{id}\" \"Track\" \"\") 0) -1)");
        Assert.True(reset.Succeeded); Assert.Null(reset.LastValue!.As<ProjectTrack>().ColorRgb);
    }

    [Fact]
    public void OldProjectsDefaultToAutomaticColorAndRejectNewColorUnderOldVersion()
    {
        var doc = Create(); var json = JsonNode.Parse(ProjectJson.Serialize(doc.Snapshot))!;
        json["Version"] = 16;
        foreach (var track in json["Tracks"]!.AsArray()) track!.AsObject().Remove("ColorRgb");
        Assert.All(ProjectJson.Deserialize(json.ToJsonString()).Routing.Tracks, t => Assert.Null(t.ColorRgb));
        json["Tracks"]![0]!["ColorRgb"] = 0;
        Assert.Throws<System.Text.Json.JsonException>(() => ProjectJson.Deserialize(json.ToJsonString()));
        json["Version"] = 17;
        Assert.Equal(0, ProjectJson.Deserialize(json.ToJsonString()).Routing.Tracks[0].ColorRgb);
        json["Tracks"]![0]!["ColorRgb"] = 0x1000000;
        Assert.Throws<ArgumentException>(() => ProjectJson.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void MuteSoloPersistExportAndUndoWithMuteTakingPrecedence()
    {
        var doc = Create(); var a = doc.Snapshot.Routing.Tracks[0].Id; var b = doc.Snapshot.Routing.Tracks[1].Id;
        ProjectTrackCommands.SetSolo(doc, a, true);
        Assert.All(Render(doc.Snapshot), v => Assert.Equal(.4f, v));
        ProjectTrackCommands.SetSolo(doc, b, true);
        Assert.All(Render(doc.Snapshot), v => Assert.Equal(.5f, v));
        ProjectTrackCommands.SetMuted(doc, a, true);
        Assert.All(Render(doc.Snapshot), v => Assert.Equal(.1f, v));
        var saved = doc.Snapshot; int count = doc.History.UndoCount;
        ProjectTrackCommands.SetMuted(doc, a, true);
        Assert.Same(saved, doc.Snapshot); Assert.Equal(count, doc.History.UndoCount);
        Assert.Throws<ArgumentException>(() => ProjectTrackCommands.SetSolo(doc, Guid.NewGuid(), true));
        Assert.Same(saved, doc.Snapshot);
        Assert.True(doc.History.Undo()); Assert.All(Render(doc.Snapshot), v => Assert.Equal(.5f, v));
        Assert.True(doc.History.Redo()); Assert.Same(saved, doc.Snapshot);
        var restored = ProjectJson.Deserialize(ProjectJson.Serialize(saved));
        Assert.Equal(saved.Routing.Tracks, restored.Routing.Tracks); Assert.Equal(Render(saved), Render(restored));
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(FlowProjectExporter.Export(saved));
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var exported = result.LastValue!.As<ProjectSnapshot>();
        Assert.Equal(saved.Routing.Tracks, exported.Routing.Tracks); Assert.Equal(Render(saved), Render(exported));
        ProjectTrackCommands.SetSolo(doc, b, false);
        Assert.All(Render(doc.Snapshot), v => Assert.Equal(0, v));
        // Explicit stems replace saved solo selection but retain saved mute.
        var stem = new float[32]; ProjectCompiler.Prepare(doc.Snapshot, 1000, 16, soloTrack: b).Playback.Read(stem);
        Assert.All(stem, v => Assert.Equal(.1f, v));
        ProjectCompiler.Prepare(doc.Snapshot, 1000, 16, soloTrack: a).Playback.Read(stem);
        Assert.All(stem, v => Assert.Equal(0, v));
    }
    [Fact]
    public void OldSchemaDefaultsAudibleAndRejectsNewNondefaultTrackFlags()
    {
        var doc = Create(); var json = JsonNode.Parse(ProjectJson.Serialize(doc.Snapshot))!;
        json["Version"] = 11;
        foreach (var track in json["Tracks"]!.AsArray())
        { track!.AsObject().Remove("Muted"); track.AsObject().Remove("Solo"); }
        var old = ProjectJson.Deserialize(json.ToJsonString());
        Assert.All(old.Routing.Tracks, t => { Assert.False(t.Muted); Assert.False(t.Solo); });
        Assert.Equal(Render(doc.Snapshot), Render(old));
        json["Tracks"]![0]!["Muted"] = true;
        Assert.Throws<System.Text.Json.JsonException>(() => ProjectJson.Deserialize(json.ToJsonString()));
    }
    private static ProjectDocument Create()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
        var ticket = doc.BeginBuild(new(1, Guid.NewGuid(), "generate"), "saved");
        var graph = AudioGraphDefinition.Mix("mix", AudioGraphDefinition.Input("a", 0),
            AudioGraphDefinition.Input("b", 1).Then("gain", "flow.gain", new Dictionary<string, double> { ["gain"] = .25 }));
        Assert.True(doc.Accept(ticket, new(ticket.Descriptor.SourceId, ticket.Revision, ticket.Context, [],
            [new("audio", new PcmAsset(Enumerable.Repeat(.4f, 200).ToArray(), 1000))], [new("mix", graph)], [new("sine", new SineVoiceSettings())])));
        var source = doc.Snapshot.Sources.Values.Single();
        var audio = source.Bindings.Single(b => b.Output.Role == GeneratedRole.Audio).Id;
        var output = source.Bindings.Single(b => b.Output.Role == GeneratedRole.Graph).Id;
        var a = new ProjectTrack(Guid.NewGuid(), "A"); var b = new ProjectTrack(Guid.NewGuid(), "B");
        doc.Edit("Place", p => new(new(p.Arrangement.Id, tempo, meter, audioClips:
            [new(Guid.NewGuid(), a.Id, audio, 0, 0, 100, 1000), new(Guid.NewGuid(), b.Id, audio, 0, 0, 100, 1000)]),
            p.Context, p.Sources.Values, new([a, b], output)));
        return doc;
    }
    private static float[] Render(ProjectSnapshot project)
    { var samples = new float[32]; ProjectCompiler.Prepare(project, 1000, 16).Playback.Read(samples); return samples; }

    [Fact]
    public void ReorderRenameRemoveAndUndoPreserveOtherTracksBusAndAudio()
    {
        var doc = Create(); var a = doc.Snapshot.Routing.Tracks[0]; var b = doc.Snapshot.Routing.Tracks[1];
        var original = Render(doc.Snapshot); Assert.All(original, value => Assert.Equal(.5f, value));
        ProjectTrackCommands.Reorder(doc, [b.Id, a.Id]);
        Assert.Equal(new int?[] { 1, 0 }, doc.Snapshot.Routing.Tracks.Select(t => t.InputBus));
        Assert.Equal(original, Render(doc.Snapshot));
        ProjectTrackCommands.Rename(doc, a.Id, "Lead"); Assert.Equal(original, Render(doc.Snapshot));
        int count = doc.History.UndoCount;
        ProjectTrackCommands.Remove(doc, [a.Id]);
        Assert.Equal(count + 1, doc.History.UndoCount); Assert.Single(doc.Snapshot.Arrangement.AudioClips);
        Assert.Equal(1, doc.Snapshot.Routing.Tracks.Single().InputBus);
        Assert.All(Render(doc.Snapshot), value => Assert.Equal(.1f, value));
        var restored = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
        Assert.Equal(Render(doc.Snapshot), Render(restored));
        Assert.True(doc.History.Undo()); Assert.Equal(original, Render(doc.Snapshot));
        ProjectTrackCommands.Remove(doc, [a.Id, b.Id]);
        Assert.Empty(doc.Snapshot.Routing.Tracks); Assert.All(Render(doc.Snapshot), value => Assert.Equal(0, value));
        Assert.Single(doc.Snapshot.Sources);
    }
    [Fact]
    public void AddReusesExplicitEmptyBusAndInvalidGesturesAreAtomic()
    {
        var doc = Create(); var a = doc.Snapshot.Routing.Tracks[0];
        ProjectTrackCommands.Remove(doc, [a.Id]);
        ProjectTrackCommands.Add(doc, new(Guid.NewGuid(), "New", InputBus: 0));
        Assert.All(Render(doc.Snapshot), value => Assert.Equal(.1f, value));
        var before = doc.Snapshot; int count = doc.History.UndoCount;
        Assert.Throws<ArgumentException>(() => ProjectTrackCommands.Add(doc, new(Guid.NewGuid(), "Duplicate bus", InputBus: 1)));
        Assert.Throws<ArgumentException>(() => ProjectTrackCommands.Add(doc, new(Guid.NewGuid(), "Missing bus", InputBus: 2)));
        Assert.Throws<ArgumentException>(() => ProjectTrackCommands.Reorder(doc, [a.Id]));
        Assert.Throws<ArgumentException>(() => ProjectTrackCommands.SetInstrument(doc, doc.Snapshot.Routing.Tracks[0].Id, Guid.NewGuid()));
        Assert.Same(before, doc.Snapshot); Assert.Equal(count, doc.History.UndoCount);
    }
    [Fact]
    public void InstrumentAssignmentKeepsBusAndUndoesWithoutChangingSourceCode()
    {
        var doc = Create(); var track = doc.Snapshot.Routing.Tracks[1]; var source = doc.Snapshot.Sources.Values.Single();
        var instrument = source.Bindings.Single(b => b.Output.Role == GeneratedRole.Instrument).Id;
        ProjectTrackCommands.SetInstrument(doc, track.Id, instrument);
        var changed = doc.Snapshot.Routing.Tracks.Single(t => t.Id == track.Id);
        Assert.Equal(instrument, changed.InstrumentBinding); Assert.Equal(track.InputBus, changed.InputBus);
        Assert.Same(source, doc.Snapshot.Sources[source.Descriptor.SourceId]);
        Assert.True(doc.History.Undo()); Assert.Null(doc.Snapshot.Routing.Tracks.Single(t => t.Id == track.Id).InstrumentBinding);
        Assert.True(doc.History.Redo()); Assert.Equal(instrument, doc.Snapshot.Routing.Tracks.Single(t => t.Id == track.Id).InstrumentBinding);
    }
    [Fact]
    public void OlderProjectsInferOrderButVersionSixRequiresExplicitBuses()
    {
        var doc = Create(); var data = JsonNode.Parse(ProjectJson.Serialize(doc.Snapshot))!;
        foreach (var track in data["Tracks"]!.AsArray()) track!.AsObject().Remove("InputBus");
        Assert.Throws<System.Text.Json.JsonException>(() => ProjectJson.Deserialize(data.ToJsonString()));
        data["Version"] = 5;
        var old = ProjectJson.Deserialize(data.ToJsonString());
        Assert.Equal(new int?[] { 0, 1 }, old.Routing.Tracks.Select(t => t.InputBus));
        Assert.Equal(Render(doc.Snapshot), Render(old));
    }
    [Fact]
    public void ExpandedFlowExportRetainsReorderedSparseBusIdentity()
    {
        var doc = Create(); ProjectTrackCommands.Remove(doc, [doc.Snapshot.Routing.Tracks[0].Id]);
        string code = FlowProjectExporter.Export(doc.Snapshot);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(code);
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var restored = result.LastValue!.As<ProjectSnapshot>();
        Assert.Equal(ProjectJson.Serialize(doc.Snapshot), ProjectJson.Serialize(restored));
        Assert.Equal(Render(doc.Snapshot), Render(restored));
    }
}
