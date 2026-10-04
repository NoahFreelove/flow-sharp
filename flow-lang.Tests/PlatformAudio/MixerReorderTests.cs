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
public class MixerReorderTests
{
    private static ProjectDocument Create(AudioGraphDefinition graph)
    {
        var doc = ProjectFactory.Create(); var devices = doc.Snapshot.Sources.Values.Single();
        doc.Edit("Graph", p => ProjectGraphConstruction.Replace(p, devices.Descriptor.SourceId, "master", graph));
        var source = Guid.NewGuid(); ProjectNoteCommands.CreateBlank(doc, source, Guid.NewGuid(), doc.Snapshot.Routing.Tracks.Single().Id, 0, 4);
        var section = doc.Snapshot.Sources[source].Result.ScoreLayers[0].Composition.Placements[0].Section;
        ProjectNoteCommands.EditSequence(doc, source, section.Id, section.Sequences[0].Id, "Draw", s =>
            NoteEditing.Add(s, [new(Guid.NewGuid(), "voice", 0, 1, new('A', 4, 0, null, 69, 440))]));
        return doc;
    }
    private static AudioGraphDefinition Chain(bool reversed = false)
    {
        var input = AudioGraphDefinition.Input("input");
        return reversed ? input.Then("drive", "flow.drive", new Dictionary<string, double> { ["drive"] = 2 })
            .Then("gain", "flow.gain", new Dictionary<string, double> { ["gain"] = .25 })
            : input.Then("gain", "flow.gain", new Dictionary<string, double> { ["gain"] = .25 })
            .Then("drive", "flow.drive", new Dictionary<string, double> { ["drive"] = 2 });
    }
    private static float[] Render(ProjectSnapshot p)
    { var output = new float[256]; ProjectCompiler.Prepare(p, 8000, 128).Playback.Read(output); return output; }
    [Fact]
    public async Task QueuedOrderChangesSoundPreservesAutomationAndRoundTripsThroughFlow()
    {
        var doc = Create(Chain()); var binding = doc.Snapshot.Routing.GraphBinding!.Value;
        var lane = new ProjectAutomationLane(Guid.NewGuid(), binding, "drive", "drive", [new(0, 2)]);
        ProjectAutomationCommands.Set(doc, lane); var before = doc.Snapshot; var originalAudio = Render(before); int history = doc.History.UndoCount;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        await using var host = new ProjectMixerHost(session);
        string[] order = ["drive", "gain"];
        Assert.True(host.TryReorderEffects(order, out _)); order[0] = "invalid-after-submit";
        Assert.True(SpinWait.SpinUntil(() => { host.Poll(); return !host.IsWorking && !session.IsPreparing; }, TimeSpan.FromSeconds(10)));
        Assert.True(host.TryTakeCompletion(out var completion)); Assert.Equal(MixerGestureStatus.Committed, completion!.Status);
        Assert.Equal(history + 1, doc.History.UndoCount);
        var managed = doc.Snapshot.Sources.Values.Single(s => s.IsManagedGraph);
        Assert.Equal(new[] { "input", "drive", "gain" }, managed.Result.GraphLayers.Single().Graph.GetProcessingOrder().Select(n => n.Id));
        Assert.Equal("gain", managed.Result.GraphLayers.Single().Graph.OutputId);
        Assert.Equal(lane.Id, doc.Snapshot.Automation.Single().Id); Assert.Equal("drive", doc.Snapshot.Automation.Single().NodeId);
        var reference = ProjectGraphConstruction.Replace(doc.Snapshot, managed.Descriptor.SourceId, "mixer", Chain(true));
        var reordered = Render(doc.Snapshot); Assert.Equal(Render(reference), reordered); Assert.False(originalAudio.SequenceEqual(reordered));
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(FlowProjectExporter.Export(doc.Snapshot));
        Assert.True(result.Succeeded, string.Join("\n", result.Errors)); Assert.Equal(reordered, Render(result.LastValue!.As<ProjectSnapshot>()));
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot); Assert.Equal(originalAudio, Render(doc.Snapshot));
        Assert.True(doc.History.Redo()); Assert.Equal(reordered, Render(doc.Snapshot));
    }
    [Fact]
    public void ChainTailFanOutIsRewiredToTheNewTail()
    {
        var chain = Chain(); var doc = Create(AudioGraphDefinition.Mix("sum", chain.Then("left", "flow.gain"), chain.Then("right", "flow.gain")));
        var request = ProjectMixerAuthoring.BeginReorderEffects(doc, ["drive", "gain"]);
        Assert.True(ProjectMixerAuthoring.Commit(doc, ProjectMixerAuthoring.Prepare(request, TestContext.Current.CancellationToken)));
        var graph = doc.Snapshot.Sources.Values.Single(s => s.IsManagedGraph).Result.GraphLayers.Single().Graph;
        Assert.All(graph.Nodes.Where(n => n.Id is "left" or "right"), n => Assert.Equal("gain", Assert.Single(n.Inputs)));
        Assert.Equal("sum", graph.OutputId); Assert.Contains(Render(doc.Snapshot), x => x != 0);
    }
    [Fact]
    public void IntermediateFanOutAndNonContiguousSelectionAreRejectedAtomically()
    {
        var shared = AudioGraphDefinition.Input("input").Then("gain", "flow.gain");
        var doc = Create(AudioGraphDefinition.Mix("sum", shared.Then("drive", "flow.drive"), shared.Then("pan", "flow.pan")));
        var before = doc.Snapshot; int history = doc.History.UndoCount;
        Assert.Throws<ArgumentException>(() => ProjectMixerAuthoring.BeginReorderEffects(doc, ["drive", "gain"]));
        Assert.Throws<ArgumentException>(() => ProjectMixerAuthoring.BeginReorderEffects(doc, ["drive", "pan"]));
        Assert.Throws<ArgumentException>(() => ProjectMixerAuthoring.BeginReorderEffects(doc, ["gain", "gain"]));
        Assert.Throws<ArgumentException>(() => ProjectMixerAuthoring.BeginReorderEffects(doc, ["input", "gain"]));
        Assert.Same(before, doc.Snapshot); Assert.Equal(history, doc.History.UndoCount);
    }
}
