using Flow.Audio.Graph;
using Flow.Music.Model.Editing;
using Flow.Studio.Engine;
using Flow.Studio.Host;
using Flow.Studio.Model;
using Xunit;
namespace FlowLang.Tests.PlatformAudio;
[Collection("FlowScripts")]
public class MixerPreviewTests
{
    private static ProjectDocument Create()
    {
        var doc = ProjectFactory.Create(); var source = Guid.NewGuid();
        ProjectNoteCommands.CreateBlank(doc, source, Guid.NewGuid(), doc.Snapshot.Routing.Tracks.Single().Id, 0, 4);
        var section = doc.Snapshot.Sources[source].Result.ScoreLayers[0].Composition.Placements[0].Section;
        ProjectNoteCommands.EditSequence(doc, source, section.Id, section.Sequences[0].Id, "Draw", s =>
            NoteEditing.Add(s, [new(Guid.NewGuid(), "voice", 0, 4, new('A', 4, 0, null, 69, 440))]));
        return doc;
    }
    private static float[] Render(ProjectPlaybackCoordinator playback)
    {
        Assert.True(playback.Queue.TrySeek(0)); Assert.True(playback.Queue.TryPlay());
        var samples = new float[256]; playback.Queue.Read(samples); playback.Queue.Read(samples); return samples;
    }
    private static void Finish(ProjectMixerHost host, ProjectPlaybackSession session) =>
        Assert.True(SpinWait.SpinUntil(() => { host.Poll(); return !host.IsWorking && !session.IsPreparing; }, TimeSpan.FromSeconds(10)));

    [Fact]
    public async Task BurstPreviewChangesAudioWithoutHistoryAndCancelRestoresSavedSound()
    {
        var doc = Create(); var before = doc.Snapshot; int history = doc.History.UndoCount;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
        await using var host = new ProjectMixerHost(session);
        var original = Render(session.Playback);
        Assert.True(host.TryBeginParameterPreview("masterGain", "gain"));
        for (int i = 0; i < 10000; i++) Assert.True(host.TryUpdateParameterPreview(i % 16));
        Assert.True(host.TryUpdateParameterPreview(.25));
        Assert.Equal(original.Select(x => x * .25f), Render(session.Playback));
        Assert.Same(before, doc.Snapshot); Assert.Equal(history, doc.History.UndoCount);
        host.CancelParameterPreview(); Assert.Equal(original, Render(session.Playback));
        Assert.False(host.TryUpdateParameterPreview(.5));
    }
    [Fact]
    public async Task FinalValueCommitsOneActionAndUndoRestoresIt()
    {
        var doc = Create(); var before = doc.Snapshot; int history = doc.History.UndoCount;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
        await using var host = new ProjectMixerHost(session);
        var original = Render(session.Playback);
        Assert.True(host.TryBeginParameterPreview("masterGain", "gain"));
        Assert.True(host.TryUpdateParameterPreview(.25)); Assert.True(host.TryCommitParameterPreview(out var id));
        Assert.False(host.TryUpdateParameterPreview(.75));
        Finish(host, session);
        Assert.True(host.TryTakeCompletion(out var completion)); Assert.Equal(id, completion!.RequestId);
        Assert.Equal(MixerGestureStatus.Committed, completion.Status);
        Assert.Equal(history + 1, doc.History.UndoCount);
        Assert.Equal(original.Select(x => x * .25f), Render(session.Playback));
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot); session.RequestPreparation(); Finish(host, session);
        Assert.Equal(original, Render(session.Playback));
    }
    [Fact]
    public async Task FailedCommitAndShutdownRestorePreviewWithoutDocumentMutation()
    {
        var doc = Create(); var before = doc.Snapshot;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
        var original = Render(session.Playback);
        await using (var host = new ProjectMixerHost(session, (_, _) => throw new InvalidOperationException("failed")))
        {
            Assert.True(host.TryBeginParameterPreview("masterGain", "gain"));
            Assert.True(host.TryUpdateParameterPreview(0)); Assert.True(host.TryCommitParameterPreview(out _));
            Finish(host, session); Assert.True(host.TryTakeCompletion(out var result)); Assert.Equal(MixerGestureStatus.Failed, result!.Status);
            Assert.Equal(original, Render(session.Playback)); Assert.Same(before, doc.Snapshot);
            Assert.True(host.TryBeginParameterPreview("masterGain", "gain")); Assert.True(host.TryUpdateParameterPreview(0));
        }
        Assert.Equal(original, Render(session.Playback)); Assert.Same(before, doc.Snapshot);
    }
    [Fact]
    public async Task UnrelatedEditInvalidatesPreviewAndAutomationCannotBeOverridden()
    {
        var doc = Create(); await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
        await using var host = new ProjectMixerHost(session);
        var original = Render(session.Playback);
        Assert.True(host.TryBeginParameterPreview("masterGain", "gain")); Assert.True(host.TryUpdateParameterPreview(0));
        ProjectTrackCommands.Rename(doc, doc.Snapshot.Routing.Tracks.Single().Id, "New name");
        host.Poll(); Assert.False(host.TryUpdateParameterPreview(.5)); Assert.False(host.TryCommitParameterPreview(out _));
        Assert.Equal(original, Render(session.Playback));
        Assert.False(host.TryBeginParameterPreview("masterGain", "gain")); // Document not yet acknowledged.
        ProjectAutomationCommands.Set(doc, new(Guid.NewGuid(), doc.Snapshot.Routing.GraphBinding!.Value, "masterGain", "gain", [new(0, 1)]));
        session.RequestPreparation(); Finish(host, session);
        Assert.Throws<InvalidOperationException>(() => host.TryBeginParameterPreview("masterGain", "gain"));
        Assert.Throws<ArgumentException>(() => host.TryBeginParameterPreview("trackInput", "bus"));
    }
    [Fact]
    public void FailedPlaybackPreparationRestoresPreviewEvenAfterDocumentCommit()
    {
        var doc = Create(); var playback = new ProjectPlaybackCoordinator(doc, "/tmp", 8000, 128);
        var original = Render(playback);
        Assert.True(playback.TryBeginParameterPreview("masterGain", "gain", out var preview));
        Assert.True(playback.TryUpdateParameterPreview(preview!, .25));
        Assert.True(playback.FreezeParameterPreview(preview!));
        var edit = ProjectMixerAuthoring.BeginSetParameter(doc, "masterGain", "gain", .25);
        Assert.True(ProjectMixerAuthoring.Commit(doc, ProjectMixerAuthoring.Prepare(edit, TestContext.Current.CancellationToken)));
        playback.AcceptParameterPreview(preview!);
        var preparation = playback.BeginPreparation();
        Assert.Equal(original.Select(x => x * .25f), Render(playback));
        Assert.True(playback.Fail(preparation, "Asset unavailable"));
        Assert.False(preview!.IsActive); Assert.Equal(original, Render(playback));
        Assert.NotSame(doc.Snapshot, playback.ActiveSnapshot); // Saved edit survives, old playable project remains.
    }

}
