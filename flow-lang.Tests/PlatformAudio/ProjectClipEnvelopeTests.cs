using Flow.Studio.Model;
using Flow.Studio.Engine;
using FlowLang.Core;
using FlowLang.Hosting;
using FlowLang.Tests.StudioModel;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

public class ProjectClipEnvelopeTests
{
    private static float[] Render(ProjectSnapshot snapshot)
    {
        var playback = ProjectCompiler.Prepare(snapshot, 8000, 16).Playback;
        var samples = new float[playback.TotalFrames * 2];
        for (int offset = 0; offset < samples.Length; offset += 32)
            playback.Read(samples.AsSpan(offset, Math.Min(32, samples.Length - offset)));
        return samples;
    }
    [Fact]
    public void ClipEnvelopeSurvivesHistoryPersistenceAndFlowWithIdenticalPlayback()
    {
        var (doc, clip) = AudioClipProcessingTests.Create();
        var before = doc.Snapshot;
        ProjectClipEnvelopeCommands.Set(doc, clip.Id, .5, 1, 1);
        var after = doc.Snapshot; var changed = after.Arrangement.AudioClips.Single(c => c.Id == clip.Id);
        Assert.NotNull(changed.Envelope);
        int count = doc.History.UndoCount; ProjectClipEnvelopeCommands.Set(doc, clip.Id, .5, 1, 1);
        Assert.Same(after, doc.Snapshot); Assert.Equal(count, doc.History.UndoCount);
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(after, doc.Snapshot);
        var expected = Render(after); Assert.False(Render(before).SequenceEqual(expected));
        var saved = ProjectJson.Serialize(after);
        Assert.Equal(expected, Render(ProjectJson.Deserialize(saved)));
        var oldVersion = System.Text.Json.Nodes.JsonNode.Parse(saved)!;
        oldVersion["Version"] = 15;
        Assert.Throws<System.Text.Json.JsonException>(() => ProjectJson.Deserialize(oldVersion.ToJsonString()));
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var exported = engine.Evaluate(FlowProjectExporter.Export(after));
        Assert.True(exported.Succeeded, string.Join("\n", exported.Errors));
        Assert.Equal(expected, Render(exported.LastValue!.As<ProjectSnapshot>()));
        var split = ClipOperations.SplitFrames(changed, 2, after.Arrangement.Tempo, Guid.NewGuid());
        Assert.Same(changed.Envelope, split.Left.Envelope); Assert.Same(changed.Envelope, split.Right.Envelope);
        Assert.Same(changed.Envelope, ClipOperations.TrimFrames(changed, 1, 2, after.Arrangement.Tempo).Envelope);
        Assert.Same(changed.Envelope, ClipOperations.Repeat(changed, [Guid.NewGuid()], after.Arrangement.Tempo)[0].Envelope);
        var splitSnapshot = new ProjectSnapshot(new(after.Arrangement.Id, after.Context.Tempo, after.Context.Meter,
            after.Arrangement.ScoreClips, after.Arrangement.AudioClips.Where(c => c.Id != clip.Id).Concat([split.Left, split.Right])),
            after.Context, after.Sources.Values, after.Routing, after.Assets, after.Automation, after.RenderSettings);
        Assert.Equal(expected, Render(splitSnapshot));
    }
}
