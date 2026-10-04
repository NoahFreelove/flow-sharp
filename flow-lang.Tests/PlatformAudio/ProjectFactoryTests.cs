using Flow.Audio.Graph;
using Flow.Music.Model.Editing;
using Flow.Studio.Engine;
using Flow.Studio.Host;
using Flow.Studio.Model;
using Xunit;
namespace FlowLang.Tests.PlatformAudio;
[Collection("FlowScripts")]
public class ProjectFactoryTests
{
    [Fact]
    public void DefaultProjectHasSavedFlowDevicesCleanHistoryAndIndependentIdentity()
    {
        var doc = ProjectFactory.Create(90, 42, "Lead"); var other = ProjectFactory.Create();
        Assert.Equal(0, doc.History.UndoCount); Assert.False(doc.History.IsDirty);
        Assert.NotEqual(doc.Snapshot.Arrangement.Id, other.Snapshot.Arrangement.Id);
        var source = Assert.Single(doc.Snapshot.Sources.Values);
        Assert.Equal(ProjectFactory.DefaultDeviceCode, source.Code);
        Assert.Single(source.Result.GraphLayers); Assert.Single(source.Result.InstrumentLayers);
        Assert.Equal(42, doc.Snapshot.Context.Seed); Assert.Equal("Lead", Assert.Single(doc.Snapshot.Routing.Tracks).Name);
        var restored = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
        Assert.Equal(0, ProjectCompiler.Prepare(restored, 8000, 16).Playback.TotalFrames);
    }
    [Fact]
    public async Task DrawnNotePlaysThroughDefaultFlowDevicesAndPublishesMeters()
    {
        var doc = ProjectFactory.Create();
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        var track = doc.Snapshot.Routing.Tracks.Single(); var sourceId = Guid.NewGuid();
        ProjectNoteCommands.CreateBlank(doc, sourceId, Guid.NewGuid(), track.Id, 0, 4);
        var section = doc.Snapshot.Sources[sourceId].Result.ScoreLayers[0].Composition.Placements[0].Section;
        var sequence = section.Sequences[0];
        ProjectNoteCommands.EditSequence(doc, sourceId, section.Id, sequence.Id, "Draw note", s =>
            NoteEditing.Add(s, [new(Guid.NewGuid(), "voice", 0, 1, new('A', 4, 0, null, 69, 440))]));
        session.RequestPreparation();
        Assert.True(SpinWait.SpinUntil(() => { session.Poll(); return !session.IsPreparing; }, TimeSpan.FromSeconds(5)));
        Assert.Same(doc.Snapshot, session.Playback.ActiveSnapshot);
        Assert.Empty(session.Playback.ActiveDiagnostics); Assert.Empty(session.Playback.ActiveAssetDiagnostics);
        Assert.Equal(new[] { "trackInput", "trackGain", "masterGain" }, session.Playback.ActiveMeterNodeIds);
        var meters = new StereoMeter[3]; Assert.True(session.Playback.TryReadActiveMeters(meters, out long frame));
        Assert.Equal(0, frame);
        var samples = new float[32]; Assert.True(session.Playback.Queue.TryPlay()); session.Playback.Queue.Read(samples);
        Assert.Contains(samples, x => x != 0);
        Assert.True(session.Playback.TryReadActiveMeters(meters, out frame)); Assert.Equal(16, frame);
        Assert.All(meters, m => Assert.True(m.PeakLeft > 0));
        Assert.True(doc.History.Undo()); session.RequestPreparation();
        Assert.True(SpinWait.SpinUntil(() => { session.Poll(); return !session.IsPreparing; }, TimeSpan.FromSeconds(5)));
        Assert.True(session.Playback.TryReadActiveMeters(meters, out frame)); Assert.Equal(0, frame);
        Assert.All(meters, m => Assert.Equal(default, m));
    }
    [Fact]
    public void InvalidOptionsAndCancellationDoNotReturnPartialProjects()
    {
        Assert.Throws<ArgumentException>(() => ProjectFactory.Create(trackName: ""));
        Assert.ThrowsAny<ArgumentException>(() => ProjectFactory.Create(bpm: -1));
        Assert.Throws<OperationCanceledException>(() => ProjectFactory.Create(cancellation: new(true)));
    }
}
