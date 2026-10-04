using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Studio.Engine;
using Flow.Studio.Model;
using Xunit;
namespace FlowLang.Tests.StudioModel;

public class ProjectPlaybackCoordinatorTests
{
    internal static (ProjectDocument Document, Guid Clip) Create()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
        var ticket = doc.BeginBuild(new(1, Guid.NewGuid(), "generate"), "saved output");
        Assert.True(doc.Accept(ticket, new(ticket.Descriptor.SourceId, ticket.Revision, ticket.Context, [],
            [new("audio", new PcmAsset(Enumerable.Repeat(.5f, 8000).ToArray(), 1000))],
            [new("mix", AudioGraphDefinition.Input("input"))])));
        var bindings = doc.Snapshot.Sources.Values.Single().Bindings;
        var graph = bindings.Single(b => b.Output.Role == GeneratedRole.Graph).Id;
        var audio = bindings.Single(b => b.Output.Role == GeneratedRole.Audio).Id;
        var track = Guid.NewGuid(); var clip = Guid.NewGuid();
        doc.Edit("Place", p => new(new(p.Arrangement.Id, tempo, meter, audioClips:
            [new(clip, track, audio, 0, 0, 4000, 1000)]), p.Context, p.Sources.Values,
            new([new(track, "Track")], graph)));
        return (doc, clip);
    }
    [Fact]
    public void ReplacementBecomesActiveOnlyAtAudioBoundaryAndDoesNotEditHistory()
    {
        var (doc, clip) = Create(); var coordinator = new ProjectPlaybackCoordinator(doc, "/tmp", 1000, 16);
        var original = doc.Snapshot; ProjectClipCommands.Move(doc, [clip], 2);
        int history = doc.History.UndoCount;
        var prepared = ProjectPlaybackCoordinator.Prepare(coordinator.BeginPreparation(), "/tmp");
        Assert.True(coordinator.Complete(prepared)); Assert.False(coordinator.Complete(prepared));
        Assert.True(coordinator.TryPublish()); Assert.Same(original, coordinator.ActiveSnapshot);
        Assert.Equal(ProjectPlaybackStatus.AwaitingAudio, coordinator.Status);
        coordinator.Queue.Read(new float[32]);
        Assert.False(coordinator.TryPublish()); Assert.Same(doc.Snapshot, coordinator.ActiveSnapshot);
        Assert.Equal(ProjectPlaybackStatus.Active, coordinator.Status);
        Assert.Equal(history, doc.History.UndoCount); Assert.Equal(2, coordinator.Queue.Generation);
        Assert.True(coordinator.Queue.TryPlay()); var block = new float[32]; coordinator.Queue.Read(block);
        Assert.All(block, sample => Assert.Equal(0, sample)); // Clip moved one second later.
    }
    [Fact]
    public void SupersededStaleAndFailedWorkPreserveWorkingAudio()
    {
        var (doc, clip) = Create(); var coordinator = new ProjectPlaybackCoordinator(doc, "/tmp", 1000, 16);
        var old = coordinator.BeginPreparation(); var newest = coordinator.BeginPreparation();
        Assert.False(coordinator.Complete(ProjectPlaybackCoordinator.Prepare(old, "/tmp")));
        Assert.False(coordinator.Fail(old, "superseded"));
        ProjectClipCommands.Move(doc, [clip], 2);
        Assert.False(coordinator.Complete(ProjectPlaybackCoordinator.Prepare(newest, "/tmp")));
        Assert.Equal(ProjectPlaybackStatus.Stale, coordinator.Status);
        doc.Edit("Break routing", p => new(p.Arrangement, p.Context, p.Sources.Values,
            new(p.Routing.Tracks, Guid.NewGuid()), p.Assets, p.Automation));
        var failed = coordinator.BeginPreparation();
        var error = Assert.Throws<InvalidOperationException>(() => ProjectPlaybackCoordinator.Prepare(failed, "/tmp"));
        Assert.True(coordinator.Fail(failed, error.Message)); Assert.False(coordinator.TryPublish());
        Assert.Equal(ProjectPlaybackStatus.Failed, coordinator.Status); Assert.Equal(error.Message, coordinator.Error);
        Assert.Equal(1, coordinator.Queue.Generation);
        Assert.True(coordinator.Queue.TryPlay()); var block = new float[32]; coordinator.Queue.Read(block);
        Assert.All(block, sample => Assert.Equal(.5f, sample));
    }
    [Fact]
    public void StopAndPendingPublicationApplyBackpressureWithoutLosingReadyBuild()
    {
        var (doc, clip) = Create(); var coordinator = new ProjectPlaybackCoordinator(doc, "/tmp", 1000, 16);
        ProjectClipCommands.Move(doc, [clip], 1);
        Assert.True(coordinator.Complete(ProjectPlaybackCoordinator.Prepare(coordinator.BeginPreparation(), "/tmp")));
        coordinator.Queue.RequestStop(); Assert.False(coordinator.TryPublish());
        Assert.Equal(ProjectPlaybackStatus.Ready, coordinator.Status);
        coordinator.Queue.Read(new float[32]); Assert.True(coordinator.TryPublish());
        var submitted = doc.Snapshot;
        ProjectClipCommands.Move(doc, [clip], 1);
        Assert.True(coordinator.Complete(ProjectPlaybackCoordinator.Prepare(coordinator.BeginPreparation(), "/tmp")));
        Assert.False(coordinator.TryPublish());
        coordinator.Queue.Read(new float[32]); Assert.True(coordinator.TryPublish());
        Assert.Same(submitted, coordinator.ActiveSnapshot);
        coordinator.Queue.Read(new float[32]); Assert.False(coordinator.TryPublish());
        Assert.Same(doc.Snapshot, coordinator.ActiveSnapshot); Assert.Equal(3, coordinator.Queue.Generation);
    }
    [Fact]
    public void EditingAfterPreparationDropsReadyBuildAndCancellationDoesNotPublish()
    {
        var (doc, clip) = Create(); var coordinator = new ProjectPlaybackCoordinator(doc, "/tmp", 1000, 16);
        var request = coordinator.BeginPreparation();
        Assert.Throws<OperationCanceledException>(() => ProjectPlaybackCoordinator.Prepare(request, "/tmp", new(true)));
        Assert.True(coordinator.Complete(ProjectPlaybackCoordinator.Prepare(request, "/tmp")));
        ProjectClipCommands.Move(doc, [clip], 1);
        Assert.False(coordinator.TryPublish()); Assert.Equal(ProjectPlaybackStatus.Stale, coordinator.Status);
        Assert.Equal(1, coordinator.Queue.Generation);
    }
    [Fact]
    public async Task RepeatedPublicationWithConcurrentAudioAcknowledgesEverySnapshot()
    {
        var (doc, clip) = Create(); var coordinator = new ProjectPlaybackCoordinator(doc, "/tmp", 1000, 16);
        using var stop = new CancellationTokenSource();
        var audio = Task.Run(() =>
        {
            var block = new float[32];
            while (!stop.IsCancellationRequested) { coordinator.Queue.Read(block); Thread.Yield(); }
        });
        try
        {
            for (int i = 0; i < 100; i++)
            {
                ProjectClipCommands.Move(doc, [clip], .01);
                Assert.True(coordinator.Complete(ProjectPlaybackCoordinator.Prepare(coordinator.BeginPreparation(), "/tmp")));
                Assert.True(SpinWait.SpinUntil(() => coordinator.TryPublish(), TimeSpan.FromSeconds(5)));
                Assert.True(SpinWait.SpinUntil(() =>
                {
                    coordinator.TryPublish();
                    return ReferenceEquals(doc.Snapshot, coordinator.ActiveSnapshot);
                }, TimeSpan.FromSeconds(5)));
                Assert.Equal(i + 2, coordinator.Queue.Generation);
            }
        }
        finally { stop.Cancel(); await audio; }
    }
    [Fact]
    public void ProjectEditKeepsPlayingAtTheCurrentTimelineFrame()
    {
        var (doc, clip) = Create(); var coordinator = new ProjectPlaybackCoordinator(doc, "/tmp", 1000, 16);
        var block = new float[32]; coordinator.Queue.TrySeek(100); coordinator.Queue.TryPlay(); coordinator.Queue.Read(block);
        ProjectClipCommands.Move(doc, [clip], .1);
        Assert.True(coordinator.Complete(ProjectPlaybackCoordinator.Prepare(coordinator.BeginPreparation(), "/tmp")));
        Assert.True(coordinator.TryPublish()); coordinator.Queue.Read(block); coordinator.TryPublish();
        Assert.Equal(132, coordinator.Queue.PositionFrames); Assert.Equal(TransportState.Playing, coordinator.Queue.State);
        Assert.All(block, sample => Assert.Equal(.5f, sample)); Assert.Same(doc.Snapshot, coordinator.ActiveSnapshot);
    }

}
