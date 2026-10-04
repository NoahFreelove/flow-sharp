using Flow.Audio.Graph;
using Flow.Music.Model.Editing;
using Flow.Studio.Engine;
using Flow.Studio.Host;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;
namespace FlowLang.Tests.PlatformAudio;
[Collection("FlowScripts")]
public class MixerEffectTests
{
    private static ProjectDocument Create()
    {
        var doc = ProjectFactory.Create(); var source = Guid.NewGuid();
        ProjectNoteCommands.CreateBlank(doc, source, Guid.NewGuid(), doc.Snapshot.Routing.Tracks.Single().Id, 0, 4);
        var section = doc.Snapshot.Sources[source].Result.ScoreLayers[0].Composition.Placements[0].Section;
        ProjectNoteCommands.EditSequence(doc, source, section.Id, section.Sequences[0].Id, "Draw", s =>
            NoteEditing.Add(s, [new(Guid.NewGuid(), "voice", 0, 1, new('A', 4, 0, null, 69, 440))]));
        return doc;
    }
    private static float[] Render(ProjectSnapshot p)
    { var samples = new float[128]; ProjectCompiler.Prepare(p, 8000, 64).Playback.Read(samples); return samples; }
    private static void Commit(ProjectDocument doc, MixerEditRequest request) =>
        Assert.True(ProjectMixerAuthoring.Commit(doc, ProjectMixerAuthoring.Prepare(request, TestContext.Current.CancellationToken)));
    private static void Finish(ProjectMixerHost host, ProjectPlaybackSession session)
    {
        Assert.True(SpinWait.SpinUntil(() => { host.Poll(); return !host.IsWorking && !session.IsPreparing; }, TimeSpan.FromSeconds(10)));
        Assert.True(host.TryTakeCompletion(out var result)); Assert.Equal(MixerGestureStatus.Committed, result!.Status);
    }
    [Fact]
    public async Task InsertBypassRemoveAndUndoUseCapturedFlowParameters()
    {
        var doc = Create(); var baseline = Render(doc.Snapshot); int history = doc.History.UndoCount;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        await using var host = new ProjectMixerHost(session);
        var parameters = new Dictionary<string, double> { ["gain"] = .25 };
        Assert.True(host.TryInsertEffect("trim", "flow.gain", "masterGain", out _, parameters: parameters)); parameters["gain"] = 1;
        Finish(host, session); var managed = doc.Snapshot.Sources.Values.Single(s => s.IsManagedGraph);
        Assert.Equal(baseline.Select(x => x * .25f), Render(doc.Snapshot));
        Assert.True(host.TrySetBypass("trim", true, out _)); Finish(host, session);
        Assert.Equal(baseline, Render(doc.Snapshot));
        Assert.True(host.TryRemoveEffect("trim", out _)); Finish(host, session);
        Assert.Equal(baseline, Render(doc.Snapshot)); Assert.Equal(history + 3, doc.History.UndoCount);
        Assert.Equal(managed.Descriptor.SourceId, doc.Snapshot.Sources.Values.Single(s => s.IsManagedGraph).Descriptor.SourceId);
        Assert.True(doc.History.Undo()); Assert.Equal(baseline, Render(doc.Snapshot));
        Assert.True(doc.History.Undo()); Assert.Equal(baseline.Select(x => x * .25f), Render(doc.Snapshot));
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var exported = engine.Evaluate(FlowProjectExporter.Export(doc.Snapshot));
        Assert.True(exported.Succeeded, string.Join("\n", exported.Errors));
        Assert.Equal(Render(doc.Snapshot), Render(exported.LastValue!.As<ProjectSnapshot>()));
    }
    [Fact]
    public void DelayTailAndAutomationFollowBypassRemovalAndUndo()
    {
        var doc = Create(); long originalFrames = ProjectCompiler.Prepare(doc.Snapshot, 8000, 64).Playback.TotalFrames;
        Commit(doc, ProjectMixerAuthoring.BeginInsertEffect(doc, "echo", "flow.delay", "masterGain", parameters:
            new Dictionary<string, double> { ["timeMs"] = 10, ["repeats"] = 2 }));
        var binding = doc.Snapshot.Routing.GraphBinding!.Value;
        var echo = new ProjectAutomationLane(Guid.NewGuid(), binding, "echo", "wet", [new(0, .3)]);
        var master = new ProjectAutomationLane(Guid.NewGuid(), binding, "masterGain", "gain", [new(0, .5)]);
        ProjectAutomationCommands.Set(doc, echo); ProjectAutomationCommands.Set(doc, master);
        Assert.Equal(originalFrames + 160, ProjectCompiler.Prepare(doc.Snapshot, 8000, 64).Playback.TotalFrames);
        Commit(doc, ProjectMixerAuthoring.BeginSetBypass(doc, "echo", true));
        Assert.Equal(2, doc.Snapshot.Automation.Count); Assert.Equal(originalFrames, ProjectCompiler.Prepare(doc.Snapshot, 8000, 64).Playback.TotalFrames);
        var before = doc.Snapshot;
        Commit(doc, ProjectMixerAuthoring.BeginRemoveEffect(doc, "echo"));
        Assert.Equal(master.Id, Assert.Single(doc.Snapshot.Automation).Id);
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot); Assert.Equal(2, doc.Snapshot.Automation.Count);
    }
    [Fact]
    public void SharedEffectRemovalRewiresEveryConsumerAndCanRemoveOutputEffect()
    {
        var doc = Create(); var source = doc.Snapshot.Sources.Values.Single(s => s.Result.GraphLayers.Count > 0);
        var shared = AudioGraphDefinition.Input("input").Then("shared", "flow.drive");
        var graph = AudioGraphDefinition.Mix("sum", shared.Then("left", "flow.gain"), shared.Then("right", "flow.gain"))
            .Then("output", "flow.gain");
        doc.Edit("Load graph", p => ProjectGraphConstruction.Replace(p, source.Descriptor.SourceId, "master", graph));
        Commit(doc, ProjectMixerAuthoring.BeginRemoveEffect(doc, "shared"));
        var edited = doc.Snapshot.Sources.Values.Single(s => s.IsManagedGraph).Result.GraphLayers.Single().Graph;
        Assert.All(edited.Nodes.Where(n => n.Id is "left" or "right"), n => Assert.Equal("input", Assert.Single(n.Inputs)));
        var samples = Render(doc.Snapshot); Commit(doc, ProjectMixerAuthoring.BeginRemoveEffect(doc, "output"));
        Assert.Equal(samples, Render(doc.Snapshot));
    }
    [Fact]
    public void InvalidEffectGesturesDoNotChangeHistoryOrSource()
    {
        var doc = Create(); var before = doc.Snapshot; int history = doc.History.UndoCount;
        Assert.Throws<ArgumentException>(() => ProjectMixerAuthoring.BeginInsertEffect(doc, "masterGain", "flow.gain", "masterGain"));
        Assert.Throws<ArgumentException>(() => ProjectMixerAuthoring.BeginInsertEffect(doc, "bad", "flow.sum", "masterGain"));
        Assert.Throws<ArgumentException>(() => ProjectMixerAuthoring.BeginSetBypass(doc, "trackInput", true));
        Assert.Throws<ArgumentException>(() => ProjectMixerAuthoring.BeginRemoveEffect(doc, "trackInput"));
        Assert.Same(before, doc.Snapshot); Assert.Equal(history, doc.History.UndoCount);
    }
}
