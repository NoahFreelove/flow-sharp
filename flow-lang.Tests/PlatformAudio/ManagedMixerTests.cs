using Flow.Music.Model.Editing;
using Flow.Studio.Engine;
using Flow.Studio.Host;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using System.Text.Json.Nodes;
using Xunit;
namespace FlowLang.Tests.PlatformAudio;
[Collection("FlowScripts")]
public class ManagedMixerTests
{
    private static float[] Render(ProjectSnapshot project)
    { var samples = new float[128]; ProjectCompiler.Prepare(project, 8000, 64).Playback.Read(samples); return samples; }
    private static void Set(ProjectDocument doc, double value)
    {
        var request = ProjectMixerAuthoring.BeginSetParameter(doc, "masterGain", "gain", value);
        Assert.True(ProjectMixerAuthoring.Commit(doc, ProjectMixerAuthoring.Prepare(request, TestContext.Current.CancellationToken)));
    }
    [Fact]
    public async Task QueuedParametersReuseCanonicalSourceAndRoundTripThroughFlowAndUndo()
    {
        var doc = ProjectFactory.Create(); var userSource = doc.Snapshot.Sources.Values.Single();
        var source = Guid.NewGuid(); var track = doc.Snapshot.Routing.Tracks.Single().Id;
        ProjectNoteCommands.CreateBlank(doc, source, Guid.NewGuid(), track, 0, 4);
        var section = doc.Snapshot.Sources[source].Result.ScoreLayers[0].Composition.Placements[0].Section;
        ProjectNoteCommands.EditSequence(doc, source, section.Id, section.Sequences[0].Id, "Draw", s =>
            NoteEditing.Add(s, [new(Guid.NewGuid(), "voice", 0, 1, new('A', 4, 0, null, 69, 440))]));
        var baseline = Render(doc.Snapshot); int history = doc.History.UndoCount;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        await using var host = new ProjectMixerHost(session);
        Assert.True(host.TrySetParameter("masterGain", "gain", .5, out _));
        Assert.True(host.TrySetParameter("masterGain", "gain", .25, out _));
        Assert.True(SpinWait.SpinUntil(() => { host.Poll(); return !host.IsWorking && !session.IsPreparing; }, TimeSpan.FromSeconds(10)));
        Assert.True(host.TryTakeCompletion(out var a)); Assert.Equal(MixerGestureStatus.Committed, a!.Status); Assert.Null(a.TrackId);
        Assert.True(host.TryTakeCompletion(out var b)); Assert.Equal(MixerGestureStatus.Committed, b!.Status);
        Assert.Equal(history + 2, doc.History.UndoCount); Assert.Equal(3, doc.Snapshot.Sources.Count);
        Assert.Same(userSource, doc.Snapshot.Sources[userSource.Descriptor.SourceId]);
        var managed = doc.Snapshot.Sources.Values.Single(s => s.IsManagedGraph);
        var graphBinding = doc.Snapshot.Routing.GraphBinding;
        Assert.Equal(baseline.Select(x => x * .25f), Render(doc.Snapshot));
        var restored = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
        Assert.True(restored.Sources[managed.Descriptor.SourceId].IsManagedGraph);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var exported = engine.Evaluate(FlowProjectExporter.Export(restored));
        Assert.True(exported.Succeeded, string.Join("\n", exported.Errors));
        Assert.Equal(ProjectJson.Serialize(restored), ProjectJson.Serialize(exported.LastValue!.As<ProjectSnapshot>()));
        Assert.True(doc.History.Undo()); Assert.Equal(graphBinding, doc.Snapshot.Routing.GraphBinding);
        Assert.Equal(baseline.Select(x => x * .5f), Render(doc.Snapshot));
        Assert.True(doc.History.Redo()); Assert.Equal(graphBinding, doc.Snapshot.Routing.GraphBinding);
    }
    [Fact]
    public void NormalCodeAcceptanceTransfersOwnershipAndNextVisualEditClonesIt()
    {
        var doc = ProjectFactory.Create(); Set(doc, .5);
        var managed = doc.Snapshot.Sources.Values.Single(s => s.IsManagedGraph);
        var ticket = doc.BeginBuild(managed.Descriptor, managed.Code);
        var built = FlowDawGenerator.Build(new(ticket.Descriptor, ticket.Revision, ticket.Code, ticket.Context, TimeSpan.FromSeconds(20)));
        Assert.True(built.Status == JobStatus.Succeeded, built.Error); Assert.True(doc.Accept(ticket, built.Value!));
        var ownedByUser = doc.Snapshot.Sources[managed.Descriptor.SourceId]; Assert.False(ownedByUser.IsManagedGraph);
        Set(doc, .75);
        Assert.Same(ownedByUser, doc.Snapshot.Sources[managed.Descriptor.SourceId]);
        Assert.NotEqual(managed.Descriptor.SourceId, doc.Snapshot.Sources.Values.Single(s => s.IsManagedGraph).Descriptor.SourceId);
    }
    [Fact]
    public void ParameterValidationAndAutomationCannotPartiallyChangeTheDocument()
    {
        var doc = ProjectFactory.Create(); var before = doc.Snapshot;
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectMixerAuthoring.BeginSetParameter(doc, "masterGain", "gain", 100));
        Assert.Throws<ArgumentException>(() => ProjectMixerAuthoring.BeginSetParameter(doc, "masterGain", "missing", 1));
        Assert.Throws<ArgumentException>(() => ProjectMixerAuthoring.BeginSetParameter(doc, "trackInput", "bus", 1));
        Assert.Same(before, doc.Snapshot); Assert.Equal(0, doc.History.UndoCount);
        ProjectAutomationCommands.Set(doc, new(Guid.NewGuid(), before.Routing.GraphBinding!.Value, "masterGain", "gain", [new(0, .5)]));
        before = doc.Snapshot;
        Assert.Throws<InvalidOperationException>(() => ProjectMixerAuthoring.BeginSetParameter(doc, "masterGain", "gain", .75));
        Assert.Same(before, doc.Snapshot);
    }
    [Fact]
    public void OlderFilesDefaultToUserOwnershipAndInvalidManagedShapeIsRejected()
    {
        var doc = ProjectFactory.Create(); Set(doc, .5);
        var data = JsonNode.Parse(ProjectJson.Serialize(doc.Snapshot))!; data["Version"] = 6;
        foreach (var source in data["Sources"]!.AsArray()) source!.AsObject().Remove("ManagedGraph");
        Assert.All(ProjectJson.Deserialize(data.ToJsonString()).Sources.Values, s => Assert.False(s.IsManagedGraph));
        data = JsonNode.Parse(ProjectJson.Serialize(ProjectFactory.Create().Snapshot))!;
        data["Sources"]![0]!["ManagedGraph"] = true;
        Assert.Throws<ArgumentException>(() => ProjectJson.Deserialize(data.ToJsonString()));
    }
}
