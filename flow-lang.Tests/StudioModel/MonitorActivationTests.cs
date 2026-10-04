using Flow.Audio;
using Flow.Studio.Engine;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class MonitorActivationTests
{
    private static void Complete(ProjectPlaybackCoordinator playback)
    {
        var request = playback.BeginPreparation();
        Assert.True(playback.Complete(ProjectPlaybackCoordinator.Prepare(request, "/tmp")));
        Assert.True(playback.TryPublish());
    }
    private static void Acknowledge(ProjectPlaybackCoordinator playback)
    {
        playback.Queue.Read(new float[32]); playback.TryPublish();
    }

    [Fact]
    public void OnlyAcknowledgedGenerationAdmitsAndRetiredHandlesCannotFeedNewTrack()
    {
        var (document, _) = ProjectPlaybackCoordinatorTests.Create();
        var track = document.Snapshot.Routing.Tracks[0].Id;
        var playback = new ProjectPlaybackCoordinator(document, "/tmp", 1000, 16);
        int history = document.History.UndoCount;
        Assert.True(playback.SelectMonitorTrack(track));
        Complete(playback); Assert.False(playback.TryGetMonitor(out _));
        Acknowledge(playback); Assert.True(playback.TryGetMonitor(out var old));
        Assert.Equal(track, old!.TrackId); Assert.Equal(playback.Queue.Generation, old.Generation);
        Assert.True(old.TryWrite(0x90, 69, 127));
        var output = new float[32]; playback.Queue.Read(output);
        Assert.Contains(output, x => Math.Abs(x) > .01f); Assert.Equal(TransportState.Stopped, playback.Queue.State);
        Complete(playback);
        Assert.False(old.IsActive); Assert.False(old.TryWrite(0x90, 72, 127));
        Assert.False(playback.TryGetMonitor(out _));
        Acknowledge(playback); Assert.True(playback.TryGetMonitor(out var current));
        Assert.NotSame(old, current); Assert.True(current!.Generation > old.Generation);
        old.Close(); Assert.True(current.TryWrite(0x90, 69, 127));
        playback.Queue.Read(output); Assert.Contains(output, x => Math.Abs(x) > .01f);
        Assert.Equal(history, document.History.UndoCount);
        var secondTrack = Guid.NewGuid();
        document.Edit("Replace track", p => new(new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter),
            p.Context, p.Sources.Values, new([new(secondTrack, "Second")], p.Routing.GraphBinding)));
        playback.SelectMonitorTrack(secondTrack); Complete(playback); Acknowledge(playback);
        Assert.True(playback.TryGetMonitor(out var selected)); Assert.Equal(secondTrack, selected!.TrackId);
        Assert.False(current.TryWrite(0x90, 60, 127)); current.Close();
        Assert.True(selected.TryWrite(0x90, 69, 127)); playback.Queue.Read(output);
        Assert.Contains(output, x => Math.Abs(x) > .01f);
    }

    [Fact]
    public void FailedPreparationKeepsWorkingMonitorAndSelectionChangeRejectsOldBuild()
    {
        var (document, _) = ProjectPlaybackCoordinatorTests.Create();
        var track = document.Snapshot.Routing.Tracks[0].Id;
        var playback = new ProjectPlaybackCoordinator(document, "/tmp", 1000, 16);
        playback.SelectMonitorTrack(track); Complete(playback); Acknowledge(playback);
        Assert.True(playback.TryGetMonitor(out var endpoint));
        var failed = playback.BeginPreparation(); Assert.Equal(track, failed.MonitoredTrack);
        Assert.True(playback.Fail(failed, "compiler error"));
        Assert.True(playback.TryGetMonitor(out var same)); Assert.Same(endpoint, same);
        var request = playback.BeginPreparation(); var built = ProjectPlaybackCoordinator.Prepare(request, "/tmp");
        Assert.True(playback.SelectMonitorTrack(null)); Assert.False(endpoint!.IsActive);
        Assert.False(playback.Complete(built)); Assert.False(playback.TryGetMonitor(out _));
        Complete(playback); Acknowledge(playback);
        Assert.Null(playback.RequestedMonitorTrack); Assert.False(playback.TryGetMonitor(out _));
    }

    [Fact]
    public void ClosingDuringPublicationCannotReactivateAndExplicitReprepareCanRecover()
    {
        var (document, _) = ProjectPlaybackCoordinatorTests.Create();
        var playback = new ProjectPlaybackCoordinator(document, "/tmp", 1000, 16);
        playback.SelectMonitorTrack(document.Snapshot.Routing.Tracks[0].Id);
        Complete(playback); playback.CloseMonitor(); Acknowledge(playback);
        Assert.False(playback.TryGetMonitor(out _));
        Complete(playback); Acknowledge(playback); Assert.True(playback.TryGetMonitor(out var endpoint));
        Assert.True(endpoint!.TryWrite(0x90, 69, 127));
        playback.CloseMonitor(); playback.Queue.Read(new float[32]);
        Assert.False(endpoint.TryWrite(0x90, 69, 127)); Assert.False(playback.TryGetMonitor(out _));
    }

    [Fact]
    public void RemovingSelectedTrackClearsSelectionAndClosesRetainedEndpoint()
    {
        var (document, _) = ProjectPlaybackCoordinatorTests.Create();
        var playback = new ProjectPlaybackCoordinator(document, "/tmp", 1000, 16);
        playback.SelectMonitorTrack(document.Snapshot.Routing.Tracks[0].Id);
        Complete(playback); Acknowledge(playback); Assert.True(playback.TryGetMonitor(out var endpoint));
        document.Edit("Remove track", p => new(new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter),
            p.Context, p.Sources.Values, new([], p.Routing.GraphBinding)));
        var request = playback.BeginPreparation();
        Assert.Null(request.MonitoredTrack); Assert.Null(playback.RequestedMonitorTrack);
        Assert.False(endpoint!.IsActive); Assert.False(endpoint.TryWrite(0x90, 60, 127));
    }
}
