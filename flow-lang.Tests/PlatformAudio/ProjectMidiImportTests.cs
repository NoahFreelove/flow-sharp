using Flow.Studio.Host;
using Flow.Studio.Model;
using FlowLang.Tests.StudioModel;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

public class ProjectMidiImportTests
{
    [Fact]
    public async Task FileImportCreatesEditableClipsInOneActionWithoutChangingProjectTiming()
    {
        string path = Path.Combine(FlowLang.Tests.Characterization.Snapshot.RepoRoot,
            "flow-lang.Tests", "fixtures", "midi", "two_voice_counterpoint.mid");
        var imported = await ProjectMidiImport.ReadAsync(path, TestContext.Current.CancellationToken);
        var (doc, original) = AudioClipProcessingTests.Create(); var before = doc.Snapshot; int history = doc.History.UndoCount;
        var clips = ProjectMidiImport.Apply(doc, imported, original.TrackId, 8);
        var after = doc.Snapshot;
        Assert.Equal(imported.Parts.Count, clips.Count); Assert.Same(before.Context, after.Context);
        Assert.Equal(history + 1, doc.History.UndoCount);
        foreach (var clip in after.Arrangement.ScoreClips)
        {
            Assert.Equal(8, clip.AnchorQuarters); Assert.Equal(original.TrackId, clip.TrackId);
            Assert.True(after.Sources[clip.SourceId].IsEditable);
        }
        Assert.Equal(imported.Parts.Sum(p => p.Notes.Count), after.Arrangement.ScoreClips.Sum(c =>
            after.Sources[c.SourceId].Result.ScoreLayers.Single().Composition.Placements.Single().Section.Sequences.Single().Notes.Count));
        Assert.Equal(ProjectJson.Serialize(after), ProjectJson.Serialize(ProjectJson.Deserialize(ProjectJson.Serialize(after))));
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(after, doc.Snapshot);
        Assert.Throws<ArgumentException>(() => ProjectMidiImport.Apply(doc, imported, Guid.NewGuid(), 0));
        Assert.Same(after, doc.Snapshot); Assert.Equal(history + 1, doc.History.UndoCount);
    }
}
