using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;
namespace FlowLang.Tests.StudioModel;
[Collection("FlowScripts")]
public class PublicAutomationPersistenceTests
{
    [Fact]
    public void PublicCurvePreservesTargetThroughSaveExportAndHistory()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var document = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
        var lane = new ProjectAutomationLane(Guid.NewGuid(), Guid.NewGuid(), "", "cutoff", [new(0, 500), new(4, 2000)],
            targetKind: AutomationTargetKind.PluginParameter);
        ProjectAutomationCommands.Set(document, lane);
        string json = ProjectJson.Serialize(document.Snapshot);
        Assert.Equal(AutomationTargetKind.PluginParameter, ProjectJson.Deserialize(json).Automation.Single().TargetKind);
        var legacy = System.Text.Json.Nodes.JsonNode.Parse(json)!; legacy["Version"] = 9;
        Assert.Throws<System.Text.Json.JsonException>(() => ProjectJson.Deserialize(legacy.ToJsonString()));
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(FlowProjectExporter.Export(document.Snapshot));
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Equal(json, ProjectJson.Serialize(result.LastValue!.As<ProjectSnapshot>()));
        Assert.True(document.History.Undo()); Assert.Empty(document.Snapshot.Automation);
        Assert.True(document.History.Redo()); Assert.Same(lane, document.Snapshot.Automation.Single());
        Assert.Throws<InvalidOperationException>(() => MusicalAutomationCompiler.Lower(lane, tempo, 8000));
    }
}
