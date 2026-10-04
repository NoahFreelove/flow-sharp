using Flow.Audio;
using Flow.Platform.Linux;
using Flow.Studio.Host;
using Flow.Studio.Model;
using FlowLang.Tests.StudioModel;
using Xunit;
namespace FlowLang.Tests.PlatformAudio;

public class ProjectPlaybackSessionTests
{
    private sealed class Stream(CallbackRenderProbe probe) : IPlaybackOutputStream
    {
        public bool Active, FailClose;
        public bool IsActive => Active;
        public bool CallbackFaulted => false;
        public void Start() => Active = true;
        public void Dispose()
        {
            if (FailClose) throw new InvalidOperationException("Still owns callback");
            Active = false;
        }
        public void Render(float[] buffer) { Assert.True(Active); probe.Process(buffer); }
    }
    [Fact]
    public async Task OfflineMonitorSelectionAndReconnectRequireFreshAcknowledgedHandles()
    {
        var (doc, _) = ProjectPlaybackCoordinatorTests.Create(); Stream? stream = null;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 1000, 16,
            queue => new PlaybackOutputSession(queue, probe => stream = new(probe)));
        var track = doc.Snapshot.Routing.Tracks[0].Id; int history = doc.History.UndoCount;
        Assert.True(session.SetMonitoredTrack(track));
        Assert.True(SpinWait.SpinUntil(() =>
        {
            session.Poll(); return !session.IsPreparing && session.Playback.Status == Flow.Studio.Engine.ProjectPlaybackStatus.Active;
        }, TimeSpan.FromSeconds(5)));
        Assert.False(session.Playback.TryGetMonitor(out _));
        Assert.True(session.Connect());
        var output = new float[32];
        Assert.True(SpinWait.SpinUntil(() =>
        {
            stream!.Render(output); session.Poll(); return session.Playback.TryGetMonitor(out _);
        }, TimeSpan.FromSeconds(5)));
        Assert.True(session.Playback.TryGetMonitor(out var first));
        Assert.True(first!.TryWrite(0x90, 69, 127)); stream!.Render(output);
        Assert.Contains(output, x => Math.Abs(x) > .01f);
        Assert.Equal(TransportState.Stopped, session.Playback.Queue.State);
        Assert.True(session.Disconnect()); Assert.False(first.IsActive);
        Assert.True(session.Connect());
        Assert.True(SpinWait.SpinUntil(() =>
        {
            stream!.Render(output); session.Poll(); return session.Playback.TryGetMonitor(out _);
        }, TimeSpan.FromSeconds(5)));
        Assert.True(session.Playback.TryGetMonitor(out var second)); Assert.NotSame(first, second);
        Assert.False(first.TryWrite(0x90, 69, 127));
        Assert.Equal(history, doc.History.UndoCount);
    }

    [Fact]
    public async Task OfflineEditsPublishThenReconnectUsesLatestProjectWithoutAutoplay()
    {
        var (doc, clip) = ProjectPlaybackCoordinatorTests.Create();
        Stream? stream = null; int opens = 0;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 1000, 16,
            queue => new PlaybackOutputSession(queue, probe => { opens++; return stream = new(probe); }));
        ProjectClipCommands.Move(doc, [clip], 2); session.RequestPreparation();
        Assert.True(SpinWait.SpinUntil(() =>
        { session.Poll(); return ReferenceEquals(doc.Snapshot, session.Playback.ActiveSnapshot); }, TimeSpan.FromSeconds(5)));
        Assert.Equal(0, opens); Assert.Equal(PlaybackOutputState.Offline, session.OutputState);
        Assert.True(session.Connect()); Assert.Equal(TransportState.Stopped, session.Playback.Queue.State);
        Assert.True(session.Playback.Queue.TryPlay()); var samples = new float[32]; stream!.Render(samples);
        Assert.All(samples, x => Assert.Equal(0, x)); // Moved clip starts later.
        Assert.True(session.Disconnect());
        Assert.True(doc.History.Undo()); session.RequestPreparation();
        Assert.True(SpinWait.SpinUntil(() =>
        { session.Poll(); return ReferenceEquals(doc.Snapshot, session.Playback.ActiveSnapshot); }, TimeSpan.FromSeconds(5)));
        Assert.True(session.Connect()); Assert.Equal(TransportState.Stopped, session.Playback.Queue.State);
        Assert.True(session.Playback.Queue.TryPlay()); stream!.Render(samples);
        Assert.All(samples, x => Assert.Equal(.5f, x));
        Assert.Equal(2, opens);
    }
    [Fact]
    public async Task FailedDeviceClosePreventsOfflineConsumptionAndShutdownCanBeRetried()
    {
        var (doc, clip) = ProjectPlaybackCoordinatorTests.Create(); Stream? stream = null; int opens = 0;
        var session = new ProjectPlaybackSession(doc, "/tmp", 1000, 16,
            queue => new PlaybackOutputSession(queue, probe => { opens++; return stream = new(probe); }));
        try
        {
            Assert.True(session.Connect()); stream!.FailClose = true;
            Assert.False(session.Disconnect());
            ProjectClipCommands.Move(doc, [clip], 2); session.RequestPreparation();
            Assert.True(SpinWait.SpinUntil(() => { session.Poll(); return !session.IsPreparing; }, TimeSpan.FromSeconds(5)));
            for (int i = 0; i < 10; i++) session.Poll();
            Assert.Equal(1, session.Playback.Queue.Generation); Assert.True(session.Playback.Queue.StopPending);
            Assert.False(session.Connect()); Assert.Equal(1, opens);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await session.DisposeAsync());
            Assert.Equal(PlaybackOutputState.Faulted, session.OutputState);
            stream.FailClose = false; Assert.True(session.Disconnect()); session.Poll();
            Assert.Same(doc.Snapshot, session.Playback.ActiveSnapshot);
            await session.DisposeAsync(); await session.DisposeAsync();
            Assert.Equal(PlaybackOutputState.Disposed, session.OutputState);
            Assert.Throws<ObjectDisposedException>(() => session.RequestPreparation());
            Assert.Throws<ObjectDisposedException>(() => session.Poll());
        }
        finally { if (stream is not null) stream.FailClose = false; await session.DisposeAsync(); }
    }
    [Fact]
    public async Task LostDeviceAcceptsPreparedUpdatesOfflineAndCanReconnect()
    {
        var (doc, clip) = ProjectPlaybackCoordinatorTests.Create(); Stream? stream = null;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 1000, 16,
            queue => new PlaybackOutputSession(queue, probe => stream = new(probe)));
        Assert.True(session.Connect()); stream!.Active = false; session.Poll();
        Assert.Equal(PlaybackOutputState.Faulted, session.OutputState); Assert.NotNull(session.OutputError);
        ProjectClipCommands.Move(doc, [clip], 1); session.RequestPreparation();
        Assert.True(SpinWait.SpinUntil(() =>
        { session.Poll(); return ReferenceEquals(doc.Snapshot, session.Playback.ActiveSnapshot); }, TimeSpan.FromSeconds(5)));
        Assert.Equal(PlaybackOutputState.Faulted, session.OutputState); // Publishing does not conceal a device failure.
        Assert.True(session.Connect()); Assert.Null(session.OutputError);
        Assert.Equal(TransportState.Stopped, session.Playback.Queue.State);
    }
}
