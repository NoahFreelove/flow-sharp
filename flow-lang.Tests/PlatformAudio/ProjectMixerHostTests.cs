using Flow.Studio.Host;
using Flow.Studio.Model;
using Xunit;
namespace FlowLang.Tests.PlatformAudio;
[Collection("FlowScripts")]
public class ProjectMixerHostTests
{
    private static void Finish(ProjectMixerHost host, ProjectPlaybackSession session) =>
        Assert.True(SpinWait.SpinUntil(() => { host.Poll(); return !host.IsWorking && !session.IsPreparing; }, TimeSpan.FromSeconds(10)));

    [Fact]
    public async Task QueuedAddGesturesComposeAndRetainEachResultWithinCapacity()
    {
        var doc = ProjectFactory.Create();
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        await using var host = new ProjectMixerHost(session, capacity: 2);
        Assert.True(host.TryAddTrack(Guid.NewGuid(), "Second", "masterGain", out var first));
        Assert.True(host.TryAddTrack(Guid.NewGuid(), "Third", "masterGain", out var second));
        Assert.False(host.TryAddTrack(Guid.NewGuid(), "Overflow", "masterGain", out var rejected)); Assert.Equal(Guid.Empty, rejected);
        Finish(host, session);
        Assert.Equal(3, doc.Snapshot.Routing.Tracks.Count); Assert.Equal(2, doc.History.UndoCount);
        Assert.Same(doc.Snapshot, session.Playback.ActiveSnapshot);
        Assert.Equal(new int?[] { 0, 1, 2 }, doc.Snapshot.Routing.Tracks.Select(t => t.InputBus));
        Assert.Equal(2, host.OutstandingCount); // Unread completions count toward the limit too.
        Assert.False(host.TryAddTrack(Guid.NewGuid(), "Overflow", "masterGain", out _));
        Assert.True(host.TryTakeCompletion(out var a)); Assert.Equal(first, a!.RequestId); Assert.Equal(MixerGestureStatus.Committed, a.Status);
        Assert.True(host.TryTakeCompletion(out var b)); Assert.Equal(second, b!.RequestId); Assert.Equal(MixerGestureStatus.Committed, b.Status);
        Assert.False(host.TryTakeCompletion(out _)); Assert.Equal(0, host.OutstandingCount);
        Assert.True(doc.History.Undo()); Assert.Equal(2, doc.Snapshot.Routing.Tracks.Count);
    }

    [Fact]
    public async Task StaleWorkerCannotOverwriteAnEditAndNextGestureUsesCurrentProject()
    {
        var doc = ProjectFactory.Create(); var originalTrack = doc.Snapshot.Routing.Tracks.Single();
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        using var started = new ManualResetEventSlim(); using var release = new ManualResetEventSlim(); int calls = 0;
        var host = new ProjectMixerHost(session, (request, token) =>
        {
            if (Interlocked.Increment(ref calls) == 1) { started.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(5))); }
            return ProjectMixerAuthoring.Prepare(request, token);
        });
        try
        {
            Assert.True(host.TryAddTrack(Guid.NewGuid(), "Stale", "masterGain", out _)); host.Poll();
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            ProjectTrackCommands.Rename(doc, originalTrack.Id, "Renamed while preparing");
            Assert.True(host.TryAddTrack(Guid.NewGuid(), "Current", "masterGain", out _)); release.Set(); Finish(host, session);
            Assert.True(host.TryTakeCompletion(out var stale)); Assert.Equal(MixerGestureStatus.Stale, stale!.Status);
            Assert.True(host.TryTakeCompletion(out var current)); Assert.Equal(MixerGestureStatus.Committed, current!.Status);
            Assert.Equal(new[] { "Renamed while preparing", "Current" }, doc.Snapshot.Routing.Tracks.Select(t => t.Name));
            Assert.Equal(2, doc.History.UndoCount); Assert.Same(doc.Snapshot, session.Playback.ActiveSnapshot);
        }
        finally { release.Set(); await host.DisposeAsync(); }
    }

    [Fact]
    public async Task FailedPreparationReportsItsRequestAndDoesNotPreventTheNextOne()
    {
        var doc = ProjectFactory.Create(); int calls = 0;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        await using var host = new ProjectMixerHost(session, (request, token) =>
            ++calls == 1 ? throw new InvalidOperationException("Preparation failed") : ProjectMixerAuthoring.Prepare(request, token));
        Assert.True(host.TryAddTrack(Guid.NewGuid(), "Failed", "masterGain", out var failed));
        Assert.True(host.TryAddTrack(Guid.NewGuid(), "Good", "masterGain", out _)); Finish(host, session);
        Assert.True(host.TryTakeCompletion(out var result)); Assert.Equal(failed, result!.RequestId);
        Assert.Equal(MixerGestureStatus.Failed, result.Status); Assert.Equal("Preparation failed", result.Error);
        Assert.True(host.TryTakeCompletion(out result)); Assert.Equal(MixerGestureStatus.Committed, result!.Status);
        Assert.Equal(2, doc.Snapshot.Routing.Tracks.Count); Assert.Equal(1, doc.History.UndoCount);
    }

    [Fact]
    public async Task ShutdownCancelsAndJoinsWorkWithoutCommittingOrStartingQueuedGestures()
    {
        var doc = ProjectFactory.Create(); var before = doc.Snapshot; int calls = 0;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        using var started = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var host = new ProjectMixerHost(session, (request, token) =>
        {
            Interlocked.Increment(ref calls); started.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
            token.ThrowIfCancellationRequested(); return ProjectMixerAuthoring.Prepare(request, token);
        });
        try
        {
            Assert.True(host.TryAddTrack(Guid.NewGuid(), "Active", "masterGain", out _)); host.Poll();
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(host.TryAddTrack(Guid.NewGuid(), "Pending", "masterGain", out _));
            var close = host.DisposeAsync().AsTask(); Assert.False(close.IsCompleted); Assert.True(host.IsWorking);
            Assert.Throws<ObjectDisposedException>(() => host.Poll());
            release.Set(); await close; Assert.False(host.IsWorking);
            Assert.Same(before, doc.Snapshot); Assert.Equal(1, calls); Assert.Equal(0, doc.History.UndoCount);
        }
        finally { release.Set(); await host.DisposeAsync(); }
    }
}
