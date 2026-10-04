using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class ProjectTimingCommandTests
{
    [Fact]
    public void TimingEditPreservesWindowsAndHistoricalSourcesWithExactUndo()
    {
        var (doc, clip) = AudioClipProcessingTests.Create(); var before = doc.Snapshot;
        var tempo = new ProjectTempoMap([new(0, 60), new(8, 150)]); var meter = new ProjectMeterMap([new(1, 3, 4), new(4, 7, 8)]);
        int count = doc.History.UndoCount;
        ProjectTimingCommands.Set(doc, tempo, meter); var after = doc.Snapshot;
        Assert.Equal(before.Context.Revision + 1, after.Context.Revision);
        Assert.Same(tempo, after.Context.Tempo); Assert.Same(meter, after.Context.Meter);
        Assert.Equal(before.Context.Tuning, after.Context.Tuning); Assert.Same(before.RenderSettings, after.RenderSettings);
        Assert.Equal(before.Arrangement.AudioClips, after.Arrangement.AudioClips);
        Assert.Equal(clip.StartSeconds(before.Arrangement.Tempo) + 1, clip.StartSeconds(tempo), 10);
        Assert.Same(before.Sources.Values.Single(), after.Sources.Values.Single());
        Assert.Same(before.Context, after.Sources.Values.Single().Result.Context);
        Assert.Equal(count + 1, doc.History.UndoCount);
        ProjectTimingCommands.Set(doc, new(tempo.Changes), new(meter.Changes)); Assert.Same(after, doc.Snapshot);
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(after, doc.Snapshot);
        var restored = ProjectJson.Deserialize(ProjectJson.Serialize(after));
        Assert.Equal(tempo.Changes, restored.Arrangement.Tempo.Changes);
        Assert.Equal(before.Context.Tempo.Changes, restored.Sources.Values.Single().Result.Context.Tempo.Changes);
    }
    [Fact]
    public void TypedFlowOffsetsAndTimingMatchDocumentOperations()
    {
        var (doc, audio) = AudioClipProcessingTests.Create();
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var setup = engine.Evaluate(FlowProjectExporter.Export(doc.Snapshot));
        Assert.True(setup.Succeeded, string.Join("\n", setup.Errors));
        // Export orders audio clips after score clips; this fixture contains only audio.
        foreach (string expression in new[] { "(dawRelativeOffsetMs clip0 25ms)", "(dawRelativeOffsetMs clip0 0.025s)" })
        {
            var result = engine.Evaluate(expression); Assert.True(result.Succeeded, string.Join("\n", result.Errors));
            Assert.Equal(audio.Nudge.Milliseconds + 25, result.LastValue!.As<AudioClip>().Nudge.Milliseconds, 10);
        }
        var absolute = engine.Evaluate("(dawSetOffsetMs (dawSetOffsetMs clip0 25ms) 0.025s)");
        Assert.True(absolute.Succeeded, string.Join("\n", absolute.Errors));
        Assert.Equal(25, absolute.LastValue!.As<AudioClip>().Nudge.Milliseconds);
        var score = engine.Evaluate($"DawClip noteClip = (dawScoreClip \"{Guid.NewGuid()}\" \"{audio.TrackId}\" \"{Guid.NewGuid()}\" \"main\" 4.0 1.0 2.0 0.0)\n(dawSetOffsetMs (dawRelativeOffsetMs noteClip 0.025s) 10ms)");
        Assert.True(score.Succeeded, string.Join("\n", score.Errors));
        var noteClip = score.LastValue!.As<ScoreClip>();
        Assert.Equal(10, noteClip.Nudge.Milliseconds); Assert.Equal(4, noteClip.AnchorQuarters); Assert.Equal(1, noteClip.SourceOffsetQuarters);
        var timed = engine.Evaluate("(dawProjectTiming project0 (dict 0.0 60.0 8.0 150.0) (dict 1 3 4 7) (dict 1 4 4 8))");
        Assert.True(timed.Succeeded, string.Join("\n", timed.Errors));
        Assert.Equal(150, timed.LastValue!.As<ProjectSnapshot>().Context.Tempo.Changes[1].Bpm);
        int count = doc.History.UndoCount;
        ProjectClipCommands.RelativeOffset(doc, [audio.Id], new(25));
        Assert.Equal(count + 1, doc.History.UndoCount);
        Assert.True(doc.History.Undo()); Assert.Equal(audio, doc.Snapshot.Arrangement.AudioClips[0]);
        var before = doc.Snapshot;
        ProjectClipCommands.SetOffset(doc, [audio.Id], audio.Nudge); Assert.Same(before, doc.Snapshot);
        Assert.Throws<ArgumentException>(() => ProjectClipCommands.RelativeOffset(doc, [audio.Id, Guid.NewGuid()], new(10)));
        Assert.Same(before, doc.Snapshot);
        var badMeter = engine.Evaluate("(dawProjectTiming project0 (dict 0.0 60.0) (dict 1 3) (dict 2 4))");
        Assert.False(badMeter.Succeeded);
    }
}
