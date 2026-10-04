using Flow.Audio.Graph;
using Flow.Music.Model.Editing;
using Flow.Studio.Engine;
using Flow.Studio.Host;
using Flow.Studio.Model;
using Xunit;
namespace FlowLang.Tests.PlatformAudio;
[Collection("FlowScripts")]
public class ProjectMixerAuthoringTests
{
    private static void AddNote(ProjectDocument doc, Guid track)
    {
        var source = Guid.NewGuid(); ProjectNoteCommands.CreateBlank(doc, source, Guid.NewGuid(), track, 0, 4);
        var section = doc.Snapshot.Sources[source].Result.ScoreLayers[0].Composition.Placements[0].Section;
        ProjectNoteCommands.EditSequence(doc, source, section.Id, section.Sequences[0].Id, "Draw note", s =>
            NoteEditing.Add(s, [new(Guid.NewGuid(), "voice", 0, 1, new('A', 4, 0, null, 69, 440))]));
    }
    private static float[] Render(ProjectSnapshot p)
    { var samples = new float[128]; ProjectCompiler.Prepare(p, 8000, 64).Playback.Read(samples); return samples; }

    [Fact]
    public void AddedTrackSharesMasterAutomationAndIsOneCapturedUndoableFlowEdit()
    {
        var doc = ProjectFactory.Create(); var first = doc.Snapshot.Routing.Tracks.Single();
        AddNote(doc, first.Id);
        var lane = new ProjectAutomationLane(Guid.NewGuid(), doc.Snapshot.Routing.GraphBinding!.Value,
            "masterGain", "gain", [new(0, .5)], AutomationShape.Step);
        ProjectAutomationCommands.Set(doc, lane);
        var before = doc.Snapshot; var baseline = Render(before); int history = doc.History.UndoCount;
        var newId = Guid.NewGuid();
        var request = ProjectMixerAuthoring.BeginAddTrack(doc, newId, "Second", "masterGain", instrumentBinding: first.InstrumentBinding);
        var prepared = ProjectMixerAuthoring.Prepare(request, TestContext.Current.CancellationToken);
        Assert.Same(before, doc.Snapshot); Assert.True(ProjectMixerAuthoring.Commit(doc, prepared));
        Assert.Equal(history + 1, doc.History.UndoCount);
        var after = doc.Snapshot; Assert.Equal(2, after.Routing.Tracks.Count);
        Assert.NotEqual(before.Routing.GraphBinding, after.Routing.GraphBinding);
        Assert.All(before.Sources, pair => Assert.Same(pair.Value, after.Sources[pair.Key]));
        Assert.Equal(lane.Id, after.Automation.Single().Id);
        Assert.Equal(after.Routing.GraphBinding, after.Automation.Single().GraphBinding);
        Assert.Equal(baseline, Render(after));
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(after, doc.Snapshot);
        Assert.False(ProjectMixerAuthoring.Commit(doc, prepared));
        AddNote(doc, newId); var mixed = Render(doc.Snapshot);
        Assert.Equal(baseline.Select(x => x * 2).ToArray(), mixed);
        var restored = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
        Assert.Equal(mixed, Render(restored));
        Assert.Contains("dawSum", restored.Sources[request.Ticket.Descriptor.SourceId].Code);
    }
    [Fact]
    public void StaleAndCancelledPreparationNeverPartiallyAddATrack()
    {
        var doc = ProjectFactory.Create(); var first = doc.Snapshot.Routing.Tracks.Single();
        var request = ProjectMixerAuthoring.BeginAddTrack(doc, Guid.NewGuid(), "Second", "masterGain");
        Assert.Throws<OperationCanceledException>(() => ProjectMixerAuthoring.Prepare(request, new(true)));
        var prepared = ProjectMixerAuthoring.Prepare(request, TestContext.Current.CancellationToken);
        ProjectTrackCommands.Rename(doc, first.Id, "Changed"); var current = doc.Snapshot;
        Assert.False(ProjectMixerAuthoring.Commit(doc, prepared)); Assert.Same(current, doc.Snapshot);
        Assert.Single(doc.Snapshot.Routing.Tracks); Assert.Single(doc.Snapshot.Sources);
    }
    [Fact]
    public void DependentTransformFailureDoesNotAcceptSourceOrConsumeHistory()
    {
        var doc = ProjectFactory.Create(); var before = doc.Snapshot;
        var request = ProjectMixerAuthoring.BeginAddTrack(doc, Guid.NewGuid(), "Second", "masterGain");
        var prepared = ProjectMixerAuthoring.Prepare(request, TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => doc.Accept(request.Ticket, prepared.Output, "Invalid edit",
            _ => throw new InvalidOperationException("Routing failed")));
        Assert.Same(before, doc.Snapshot); Assert.Equal(0, doc.History.UndoCount);
        Assert.True(ProjectMixerAuthoring.Commit(doc, prepared));
        Assert.Equal(1, doc.History.UndoCount);
    }
    [Fact]
    public void InvalidDestinationFailsBeforeCreatingHistoryOrChangingRouting()
    {
        var doc = ProjectFactory.Create(); var before = doc.Snapshot;
        Assert.Throws<ArgumentException>(() => ProjectMixerAuthoring.BeginAddTrack(doc, Guid.NewGuid(), "New", "missing"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectMixerAuthoring.BeginAddTrack(doc, Guid.NewGuid(), "New", "trackInput"));
        Assert.Throws<ArgumentException>(() => ProjectMixerAuthoring.BeginAddTrack(doc, Guid.NewGuid(), "New", "masterGain", instrumentBinding: Guid.NewGuid()));
        Assert.Same(before, doc.Snapshot); Assert.Equal(0, doc.History.UndoCount);
    }
}
