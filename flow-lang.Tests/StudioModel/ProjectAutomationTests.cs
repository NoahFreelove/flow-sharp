using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Studio.Engine;
using Flow.Studio.Model;
using System.Text.Json.Nodes;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class ProjectAutomationTests
{
    [Fact]
    public void MusicalSlopeSurvivesTempoChangeAndSubFrameCollisionsAreDeterministic()
    {
        var tempo = new ProjectTempoMap([new(0, 120), new(2, 60)]);
        var lane = new ProjectAutomationLane(Guid.NewGuid(), Guid.NewGuid(), "gain", "gain", [new(0, 0), new(4, 1)]);
        var lowered = AutomationCompiler.Lower(lane, tempo, 1000);
        Assert.Equal(new long[] { 0, 1000, 3000 }, lowered.Points.Select(p => p.Frame));
        Assert.Equal(.25, lowered.ValueAt(500, 1)); Assert.Equal(.75, lowered.ValueAt(2000, 1));
        var collapsed = AutomationCompiler.Lower(new(Guid.NewGuid(), lane.GraphBinding, "gain", "gain",
            [new(0, 0), new(.00001, .5), new(1, 1)]), tempo, 1000);
        Assert.Equal(.5, collapsed.Points[0].Value); Assert.Equal(2, collapsed.Points.Count);
    }
    [Fact]
    public void EditingRoundTripUndoAndPlaybackUseSavedMusicalAutomation()
    {
        var tempo = new ProjectTempoMap([new(0, 120), new(2, 60)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
        var ticket = doc.BeginBuild(new(1, Guid.NewGuid(), "generate"), "saved output");
        var graph = AudioGraphDefinition.Input("input").Then("gain", "flow.gain");
        Assert.True(doc.Accept(ticket, new(ticket.Descriptor.SourceId, ticket.Revision, ticket.Context, [],
            [new("audio", new PcmAsset(Enumerable.Repeat(1f, 8000).ToArray(), 1000))], [new("mix", graph)])));
        var bindings = doc.Snapshot.Sources.Values.Single().Bindings;
        var graphId = bindings.Single(b => b.Output.Role == GeneratedRole.Graph).Id;
        var audioId = bindings.Single(b => b.Output.Role == GeneratedRole.Audio).Id; var track = Guid.NewGuid();
        doc.Edit("Place", p => new(new(p.Arrangement.Id, tempo, meter, audioClips:
            [new(Guid.NewGuid(), track, audioId, 0, 0, 4000, 1000)]), p.Context, p.Sources.Values,
            new([new(track, "Track")], graphId)));
        var lane = new ProjectAutomationLane(Guid.NewGuid(), graphId, "gain", "gain", [new(0, 0), new(4, 1)]);
        ProjectAutomationCommands.Set(doc, lane);
        var restored = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
        Assert.Single(restored.Automation);
        var playback = ProjectCompiler.Prepare(restored, 1000, 16).Playback; playback.Seek(2000);
        var samples = new float[2]; playback.Read(samples); Assert.Equal(.75f, samples[0]);
        Assert.True(doc.History.Undo()); Assert.Empty(doc.Snapshot.Automation);
        Assert.True(doc.History.Redo()); Assert.Same(lane, doc.Snapshot.Automation.Single());
        ProjectAutomationCommands.Remove(doc, lane.Id); Assert.Empty(doc.Snapshot.Automation);
        Assert.True(doc.History.Undo()); Assert.Single(doc.Snapshot.Automation);
        var previous = doc.Snapshot.Sources.Values.Single();
        var next = doc.BeginBuild(previous.Descriptor, "regenerated");
        Assert.True(doc.Accept(next, new(next.Descriptor.SourceId, next.Revision, next.Context, [],
            previous.Result.AudioLayers, previous.Result.GraphLayers)));
        Assert.Same(lane, doc.Snapshot.Automation.Single());
    }
    [Fact]
    public void OlderProjectMigrationAndDuplicateTargetsAreValidated()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var snapshot = new ProjectSnapshot(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter));
        var node = JsonNode.Parse(ProjectJson.Serialize(snapshot))!.AsObject(); node["Version"] = 2; node.Remove("Automation");
        Assert.Empty(ProjectJson.Deserialize(node.ToJsonString()).Automation);
        var lane = new ProjectAutomationLane(Guid.NewGuid(), Guid.NewGuid(), "gain", "gain", [new(0, 1)]);
        Assert.Throws<ArgumentException>(() => new ProjectSnapshot(snapshot.Arrangement, snapshot.Context, automation:
            [lane, new(Guid.NewGuid(), lane.GraphBinding, "gain", "gain", [new(0, .5)])]));
    }
}
