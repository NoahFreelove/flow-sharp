using Flow.Music.Model;
using Flow.Studio.Engine;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.StudioModel;

[Collection("FlowScripts")]
public class NoteClipProcessingTests
{
    internal static (ProjectDocument Document, ScoreClip Clip) Create()
    {
        var (doc, audio) = AudioClipProcessingTests.Create();
        var note = new NoteEvent(Guid.NewGuid(), "voice", 0, 2, new('A', 4, 0, 0, 69, 440), .6,
            NoteArticulation.Staccato, ExactDuration: new(2, 1));
        var section = new SectionSnapshot(Guid.NewGuid(), "Repeated", new(Gain: .35, Pan: .4, SustainPedal: true),
            [new(Guid.NewGuid(), "Voice", 2, [note])]);
        var descriptor = new GeneratorDescriptor(1, Guid.NewGuid(), "generate"); var ticket = doc.BeginBuild(descriptor, "notes");
        Assert.True(doc.Accept(ticket, new(descriptor.SourceId, ticket.Revision, ticket.Context,
            [new("main", new(Guid.NewGuid(), [new(Guid.NewGuid(), section, 3)]))])));
        var clip = new ScoreClip(Guid.NewGuid(), audio.TrackId, descriptor.SourceId, "main", 0, 1, 4, new(7));
        doc.Edit("Place notes", p => new(new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter, [clip], p.Arrangement.AudioClips),
            p.Context, p.Sources.Values, p.Routing));
        return (doc, clip);
    }
    private static GeneratedSourceOutput Result(NoteClipProcessingOperation operation, PluginNoteInput? input = null)
        => new(operation.Descriptor.SourceId, 0, operation.Context,
            [new(operation.Package.OutputLayer, (input ?? operation.Input).ToComposition(operation.Descriptor.SourceId, operation.Package.OutputLayer))]);
    private static float[] Render(ProjectSnapshot project)
    {
        var playback = ProjectCompiler.Prepare(project, 8000, 16).Playback;
        var samples = new float[40000];
        for (int i = 0; i < samples.Length; i += 32) playback.Read(samples.AsSpan(i, Math.Min(32, samples.Length - i)));
        return samples;
    }
    [Fact]
    public void IdentityTransformPreservesRepeatedSplitPlaybackAndUndoRedo()
    {
        var (doc, clip) = Create(); ProjectRenderCommands.Set(doc, new(22050, 128));
        var before = doc.Snapshot; var expected = Render(before); int count = doc.History.UndoCount;
        Assert.Contains(expected, value => value != 0);
        var operation = NoteClipProcessingOperation.Capture(doc, clip.Id, NoteTransformPluginTests.Package());
        Assert.True(operation.Accept(Result(operation)));
        var after = doc.Snapshot; Assert.Equal(expected, Render(after)); Assert.Same(before.Context, after.Context);
        Assert.Equal(before.RenderSettings, after.RenderSettings);
        Assert.Equal(clip.SourceOffsetQuarters, after.Arrangement.ScoreClips[0].SourceOffsetQuarters);
        Assert.Equal(clip.Nudge, after.Arrangement.ScoreClips[0].Nudge);
        Assert.Equal(count + 1, doc.History.UndoCount);
        var loaded = ProjectJson.Deserialize(ProjectJson.Serialize(after)); Assert.Equal(expected, Render(loaded));
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var evaluated = engine.Evaluate(FlowProjectExporter.Export(after));
        Assert.True(evaluated.Succeeded, string.Join("\n", evaluated.Errors));
        Assert.Equal(expected, Render(evaluated.LastValue!.As<ProjectSnapshot>()));
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(after, doc.Snapshot);
        Assert.False(operation.Accept(Result(operation)));
    }
    [Fact]
    public void NewNotesAndLongerOutputExtendSourceAndRetainSettingsAtDestination()
    {
        var (doc, clip) = Create();
        var operation = NoteClipProcessingOperation.Capture(doc, clip.Id, NoteTransformPluginTests.Package());
        var note = operation.Input.Notes[0] with { Id = Guid.NewGuid(), OffsetQuarters = 6, DurationQuarters = 1, ExactDuration = null };
        Assert.True(operation.Accept(Result(operation, new(8, [note]))));
        Assert.Equal(8, doc.Snapshot.Arrangement.ScoreClips[0].LengthQuarters);
        var score = doc.Snapshot.Sources[operation.Descriptor.SourceId].Result.ScoreLayers[0].Composition;
        var last = score.Placements.Last().Section;
        Assert.Equal(3, last.DurationQuarters); Assert.Single(last.Sequences[0].Notes);
        Assert.Equal(1, last.Sequences[0].Notes[0].OffsetQuarters);
        Assert.Equal(.35, score.Placements[0].Section.Settings.Gain);
    }
    [Fact]
    public void MovingNotesAcrossSectionsUsesDestinationControlsAndRemovalDoesNotLeakOtherNotes()
    {
        var (doc, clip) = Create();
        var source = doc.Snapshot.Sources[clip.SourceId];
        var first = source.Result.ScoreLayers[0].Composition.Placements[0].Section;
        var second = new SectionSnapshot(Guid.NewGuid(), "Second", first.Settings with { Gain = .8, Pan = -.5 }, first.Sequences);
        var ticket = doc.BeginBuild(source.Descriptor, source.Code);
        Assert.True(doc.Accept(ticket, new(clip.SourceId, ticket.Revision, ticket.Context,
            [new("main", new(Guid.NewGuid(), [new(Guid.NewGuid(), first), new(Guid.NewGuid(), second)]))])));
        var operation = NoteClipProcessingOperation.Capture(doc, clip.Id, NoteTransformPluginTests.Package());
        var moved = operation.Input.Notes[0] with { OffsetQuarters = 1.25, DurationQuarters = .25, ExactDuration = null };
        Assert.True(operation.Accept(Result(operation, new(4, [moved]))));
        var score = doc.Snapshot.Sources[operation.Descriptor.SourceId].Result.ScoreLayers[0].Composition;
        Assert.Empty(score.Placements[0].Section.Sequences[0].Notes);
        Assert.Equal(.8, score.Placements[1].Section.Settings.Gain);
        Assert.Equal(-.5, score.Placements[1].Section.Settings.Pan);
        Assert.Equal(.25, Assert.Single(score.Placements[1].Section.Sequences[0].Notes).OffsetQuarters);
    }
    [Fact]
    public void StaleAndInvalidResultsCannotEditTheDocument()
    {
        var (doc, clip) = Create(); var before = doc.Snapshot;
        var operation = NoteClipProcessingOperation.Capture(doc, clip.Id, NoteTransformPluginTests.Package());
        Assert.Throws<ArgumentException>(() => operation.Accept(Result(operation, new(0, [])))); Assert.Same(before, doc.Snapshot);
        doc.Edit("Transient", p => new(p.Arrangement, p.Context, p.Sources.Values, p.Routing)); doc.History.Undo();
        Assert.Same(before, doc.Snapshot); Assert.False(operation.Accept(Result(operation)));
    }
}
