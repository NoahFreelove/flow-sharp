using Flow.Studio.Engine;
using Flow.Studio.Model;
using Xunit;
namespace FlowLang.Tests.StudioModel;

public class ProjectPlaybackPreparationHostTests
{
    [Fact]
    public async Task BurstKeepsOnlyLatestPendingSnapshotAndNeverOverlapsWorkers()
    {
        var (doc, clip) = ProjectPlaybackCoordinatorTests.Create();
        var playback = new ProjectPlaybackCoordinator(doc, "/tmp", 1000, 16);
        using var started = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        int calls = 0, active = 0, peak = 0;
        var host = new ProjectPlaybackPreparationHost(playback, (request, token) =>
        {
            int count = Interlocked.Increment(ref active); peak = Math.Max(peak, count);
            try
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    started.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
                    Assert.True(token.IsCancellationRequested);
                }
                return ProjectPlaybackCoordinator.Prepare(request, "/tmp"); // Deliberately slow cancellation.
            }
            finally { Interlocked.Decrement(ref active); }
        });
        try
        {
            host.Request(); Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            for (int i = 0; i < 100; i++) { ProjectClipCommands.Move(doc, [clip], .01); host.Request(); host.Poll(); }
            Assert.Equal(1, Volatile.Read(ref calls)); Assert.Equal(1, playback.Queue.Generation);
            release.Set();
            Assert.True(SpinWait.SpinUntil(() => { host.Poll(); return !host.IsPreparing; }, TimeSpan.FromSeconds(5)));
            Assert.Equal(2, calls); Assert.Equal(1, peak);
            playback.Queue.Read(new float[32]); host.Poll();
            Assert.Same(doc.Snapshot, playback.ActiveSnapshot); Assert.Equal(2, playback.Queue.Generation);
        }
        finally { release.Set(); await host.DisposeAsync(); }
    }

    [Fact]
    public async Task FailureCanBeRetriedWithoutReplacingOldAudio()
    {
        var (doc, _) = ProjectPlaybackCoordinatorTests.Create();
        var playback = new ProjectPlaybackCoordinator(doc, "/tmp", 1000, 16); int calls = 0;
        await using var host = new ProjectPlaybackPreparationHost(playback, (request, token) =>
            ++calls == 1 ? throw new InvalidOperationException("Failed asset read") : ProjectPlaybackCoordinator.Prepare(request, "/tmp", token));
        host.Request();
        Assert.True(SpinWait.SpinUntil(() => { host.Poll(); return !host.IsPreparing; }, TimeSpan.FromSeconds(5)));
        Assert.Equal(ProjectPlaybackStatus.Failed, playback.Status); Assert.Equal("Failed asset read", playback.Error);
        Assert.True(playback.Queue.TryPlay()); var samples = new float[32]; playback.Queue.Read(samples);
        Assert.All(samples, x => Assert.Equal(.5f, x));
        host.Request();
        Assert.True(SpinWait.SpinUntil(() => { host.Poll(); return !host.IsPreparing; }, TimeSpan.FromSeconds(5)));
        playback.Queue.Read(samples); host.Poll();
        Assert.Equal(ProjectPlaybackStatus.Active, playback.Status); Assert.Equal(2, playback.Queue.Generation);
    }

    [Fact]
    public async Task ShutdownJoinsWorkerAndDropsPendingBuild()
    {
        var (doc, _) = ProjectPlaybackCoordinatorTests.Create();
        var playback = new ProjectPlaybackCoordinator(doc, "/tmp", 1000, 16);
        using var started = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        int calls = 0;
        var host = new ProjectPlaybackPreparationHost(playback, (request, token) =>
        {
            Interlocked.Increment(ref calls); started.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5))); token.ThrowIfCancellationRequested();
            return ProjectPlaybackCoordinator.Prepare(request, "/tmp", token);
        });
        try
        {
            host.Request(); Assert.True(started.Wait(TimeSpan.FromSeconds(5))); host.Request();
            var shutdown = host.DisposeAsync().AsTask(); Assert.False(shutdown.IsCompleted);
            Assert.True(host.IsPreparing);
            Assert.Throws<ObjectDisposedException>(() => host.Request()); Assert.Throws<ObjectDisposedException>(() => host.Poll());
            release.Set(); await shutdown;
            Assert.False(host.IsPreparing);
            Assert.Equal(1, calls); Assert.Equal(1, playback.Queue.Generation);
            await host.DisposeAsync();
        }
        finally { release.Set(); await host.DisposeAsync(); }
    }
}
