using System.Text.Json.Nodes;
using Flow.Studio.Engine;
using Flow.Studio.Host;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

public class ProjectRenderSettingsTests
{
    [Fact]
    public void PreferencesSurviveEditsHistoryPersistenceAndFlowReconstruction()
    {
        var doc = ProjectFactory.Create(); var defaults = doc.Snapshot;
        var settings = new ProjectRenderSettings(22050, 128);
        ProjectRenderCommands.Set(doc, settings);
        var configured = doc.Snapshot; int count = doc.History.UndoCount;
        ProjectRenderCommands.Set(doc, new(22050, 128));
        Assert.Same(configured, doc.Snapshot); Assert.Equal(count, doc.History.UndoCount);
        Assert.True(doc.History.Undo()); Assert.Same(defaults, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(configured, doc.Snapshot);
        var track = doc.Snapshot.Routing.Tracks[0].Id; var clip = Guid.NewGuid();
        ProjectNoteCommands.CreateBlank(doc, Guid.NewGuid(), clip, track, 0, 4);
        ProjectClipCommands.TrimScore(doc, clip, 1, 2);
        ProjectClipCommands.Repeat(doc, clip, [Guid.NewGuid()]);
        ProjectTrackCommands.SetMuted(doc, track, true);
        var graph = doc.Snapshot.Routing.GraphBinding!.Value;
        var lane = new ProjectAutomationLane(Guid.NewGuid(), graph, "test", "gain", [new(0, .5)]);
        ProjectAutomationCommands.Set(doc, lane); Assert.Equal(settings, doc.Snapshot.RenderSettings);
        ProjectAutomationCommands.Remove(doc, lane.Id);
        var source = doc.Snapshot.Sources.Values.First(s => !s.IsEditable);
        var ticket = doc.BeginBuild(source.Descriptor, source.Code);
        Assert.True(doc.Accept(ticket, new(ticket.Descriptor.SourceId, ticket.Revision, ticket.Context,
            source.Result.ScoreLayers, source.Result.AudioLayers, source.Result.GraphLayers, source.Result.InstrumentLayers)));
        Assert.Equal(settings, doc.Snapshot.RenderSettings);
        Assert.Equal(settings, ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot)).RenderSettings);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(FlowProjectExporter.Export(doc.Snapshot));
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Equal(settings, result.LastValue!.As<ProjectSnapshot>().RenderSettings);
        var changed = engine.Evaluate("(dawRenderSettings project0 32000 64)");
        Assert.True(changed.Succeeded, string.Join("\n", changed.Errors));
        Assert.Equal(new ProjectRenderSettings(32000, 64), changed.LastValue!.As<ProjectSnapshot>().RenderSettings);
    }
    [Fact]
    public void SchemaRequiresPreferencesAndRejectsInvalidOrDowngradedValues()
    {
        var doc = ProjectFactory.Create(); ProjectRenderCommands.Set(doc, new(44100, 64));
        var json = JsonNode.Parse(ProjectJson.Serialize(doc.Snapshot))!;
        json["Version"] = 12;
        Assert.Throws<System.Text.Json.JsonException>(() => ProjectJson.Deserialize(json.ToJsonString()));
        json["Version"] = 13; json.AsObject().Remove("RenderSettings");
        Assert.Throws<System.Text.Json.JsonException>(() => ProjectJson.Deserialize(json.ToJsonString()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectRenderSettings(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectRenderSettings(48000, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectRenderSettings(48000, 65537));
    }
    [Fact]
    public async Task SavedDefaultsDriveExportsAndSurviveFileRecoveryAndPackaging()
    {
        var doc = ProjectFactory.Create(); ProjectRenderCommands.Set(doc, new(8000, 16));
        string root = Path.Combine(Path.GetTempPath(), "flow-render-settings-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "project.flowproject"); ProjectFile.Save(path, doc);
            Assert.Equal(doc.Snapshot.RenderSettings, ProjectFile.Load(path).Snapshot.RenderSettings);
            string recovery = Path.Combine(root, "recovery"); ProjectFile.SaveRecovery(recovery, doc.Snapshot);
            Assert.Equal(doc.Snapshot.RenderSettings, ProjectRecovery.Restore(ProjectRecovery.Inspect(path, recovery)).Snapshot.RenderSettings);
            var packaged = ProjectPackage.Create(doc.Snapshot, root, Path.Combine(root, "package"));
            Assert.Equal(doc.Snapshot.RenderSettings, ProjectFile.Load(packaged).Snapshot.RenderSettings);
            var bounce = await ProjectBounceFile.ExportAsync(doc.Snapshot, root, Path.Combine(root, "mix.wav"));
            Assert.Equal(8000, bounce.SampleRate);
            var stems = await ProjectStemFiles.ExportAsync(doc.Snapshot, root, Path.Combine(root, "stems"));
            Assert.Equal(8000, stems.SampleRate);
            var overridden = await ProjectBounceFile.ExportAsync(doc.Snapshot, root, Path.Combine(root, "override.wav"), sampleRate: 16000);
            Assert.Equal(16000, overridden.SampleRate); Assert.Equal(8000, doc.Snapshot.RenderSettings.SampleRate);
        }
        finally { Directory.Delete(root, true); }
    }
}
