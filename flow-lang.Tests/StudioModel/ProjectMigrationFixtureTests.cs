using Flow.Studio.Engine;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class ProjectMigrationFixtureTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(11)]
    [InlineData(12)]
    public void FixedOlderFilesPreserveTimingIdentityAndRepairableReferences(int version)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "projects", $"v{version:00}-arrangement.flowproject");
        string original = File.ReadAllText(path);
        var doc = ProjectFile.Load(path); var p = doc.Snapshot;
        Assert.False(doc.History.IsDirty); Assert.Empty(p.Sources);
        Assert.Equal(new ProjectRenderSettings(48000, 256), p.RenderSettings);
        Assert.Equal(42, p.Context.Seed); Assert.Equal(.75, p.Context.Parameters["density"]);
        Assert.Same(p.Context.Tempo, p.Arrangement.Tempo); Assert.Same(p.Context.Meter, p.Arrangement.Meter);
        Assert.Equal(90, p.Arrangement.Tempo.Changes[1].Bpm);
        Assert.Equal(3, p.Arrangement.Meter.Changes[1].Numerator);
        Assert.Equal(version >= 6 ? new int?[] { 3, 1 } : [0, 1], p.Routing.Tracks.Select(t => t.InputBus));
        Assert.Equal(version >= 12, p.Routing.Tracks[0].Muted);
        Assert.Equal(version >= 12, p.Routing.Tracks[1].Solo);
        Assert.Equal(version >= 11 ? 1 : 0, p.Routing.Effects.Count);
        var score = Assert.Single(p.Arrangement.ScoreClips); var audio = Assert.Single(p.Arrangement.AudioClips);
        Assert.Equal(1, score.SourceOffsetQuarters); Assert.Equal(12.5, score.Nudge.Milliseconds);
        Assert.Equal(9007199254740993L, audio.SourceOffsetFrames); Assert.Equal(-7, audio.Nudge.Milliseconds);
        if (version >= 6) Assert.Equal(audio.SourceId, Assert.Single(p.Assets).Id);
        else Assert.Empty(p.Assets);
        string current = ProjectJson.Serialize(p);
        Assert.Equal(current, ProjectJson.Serialize(ProjectJson.Deserialize(current)));
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var flow = engine.Evaluate(FlowProjectExporter.Export(p));
        Assert.True(flow.Succeeded, string.Join("\n", flow.Errors));
        Assert.Equal(current, ProjectJson.Serialize(flow.LastValue!.As<ProjectSnapshot>()));
        ProjectClipCommands.Move(doc, [score.Id, audio.Id], 2);
        Assert.True(doc.History.Undo()); Assert.Same(p, doc.Snapshot);
        Assert.True(doc.History.Redo());
        var edited = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
        Assert.Equal(6, edited.Arrangement.ScoreClips.Single().AnchorQuarters);
        Assert.Equal(p.Routing.Tracks, edited.Routing.Tracks);
        Assert.Equal(original, File.ReadAllText(path));
    }
}
