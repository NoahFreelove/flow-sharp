using Flow.Audio;
using Flow.Studio.Engine;
using Flow.Studio.Host;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

public class ProjectMonitoringTests
{
    [Fact]
    public async Task MetronomeTogglePublishesWithoutEditingAndCanRecordPastEmptyProjectEnd()
    {
        var document = ProjectFactory.Create(); var snapshot = document.Snapshot;
        Output? output = null;
        await using var session = new ProjectPlaybackSession(document, "/tmp", 8000, 16,
            q => new Flow.Platform.Linux.PlaybackOutputSession(q, p => output = new(p)));
        Assert.True(session.Connect()); long generation = session.Playback.Queue.Generation;
        Assert.True(session.SetMetronome(true)); Assert.False(session.SetMetronome(true));
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while ((session.Playback.Queue.Generation == generation || session.IsPreparing) && DateTime.UtcNow < deadline)
        { session.Poll(); output!.Render(); await Task.Delay(5, TestContext.Current.CancellationToken); }
        session.Poll(); Assert.True(session.Playback.Queue.Generation > generation);
        Assert.True(session.Playback.Queue.TryBeginRecording(out var token));
        Assert.Contains(output!.Render(), sample => sample != 0);
        Assert.Equal(0, session.Playback.Queue.TotalFrames);
        session.Playback.Queue.RequestEndRecording(token); output.Render();
        Assert.All(output.Render(), sample => Assert.Equal(0, sample));
        Assert.Same(snapshot, document.Snapshot); Assert.Equal(0, document.History.UndoCount);
        Assert.False(ProjectCompiler.Prepare(snapshot, 8000, 16).Playback.MonitoringEnabled);
    }

    private sealed class Output(Flow.Platform.Linux.CallbackRenderProbe probe) : Flow.Platform.Linux.IPlaybackOutputStream
    {
        public bool IsActive { get; private set; }
        public bool CallbackFaulted => false;
        public void Start() => IsActive = true;
        public void Dispose() => IsActive = false;
        public float[] Render() { var samples = new float[32]; probe.Process(samples); return samples; }
    }

    [Fact]
    public async Task SessionPreparationResolvesProjectTuningForThePublishedMonitor()
    {
        var document = ProjectFactory.Create();
        FlowLang.Hosting.GeneratorTuning.Set(document, new("JustIntonation", "Dminor"));
        var snapshot = document.Snapshot; var track = snapshot.Routing.Tracks[0].Id;
        Output? output = null;
        await using var session = new ProjectPlaybackSession(document, "/tmp", 8000, 16,
            q => new Flow.Platform.Linux.PlaybackOutputSession(q, p => output = new(p)));
        Assert.True(session.Connect()); session.SetMonitoredTrack(track);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!session.Playback.TryGetMonitor(out _) && DateTime.UtcNow < deadline)
        { session.Poll(); output!.Render(); await Task.Delay(5, TestContext.Current.CancellationToken); }
        session.Poll();
        Assert.True(session.Playback.TryGetMonitor(out var endpoint));
        var prepared = ProjectCompiler.Prepare(snapshot, 8000, 16, monitoredTrack: track,
            midiPitchMap: FlowLang.Hosting.GeneratorTuning.ResolveMidi(snapshot.Context.Tuning));
        var referenceTransport = new PreparedSineTransport(prepared.Playback);
        prepared.Monitors[track].TryWrite(0x90, 69, 127); endpoint!.TryWrite(0x90, 69, 127);
        var expected = new float[32]; referenceTransport.Read(expected);
        Assert.Equal(expected, output!.Render()); Assert.Contains(expected, value => value != 0);
        Assert.Same(snapshot, document.Snapshot);
        Assert.Throws<InvalidOperationException>(() => ProjectCompiler.Prepare(snapshot, 8000, 16, monitoredTrack: track));
    }

    [Fact]
    public void MutedTrackGatesLiveMonitoringAfterTheInstrument()
    {
        var document = ProjectFactory.Create(); var track = document.Snapshot.Routing.Tracks[0].Id;
        Flow.Studio.Model.ProjectTrackCommands.SetMuted(document, track, true);
        var prepared = ProjectCompiler.Prepare(document.Snapshot, 1000, 16, monitoredTrack: track);
        Assert.True(prepared.Monitors[track].TryWrite(0x90, 69, 127));
        var transport = new PreparedSineTransport(prepared.Playback); var output = new float[32];
        transport.Read(output); Assert.All(output, x => Assert.Equal(0, x));
        Assert.Equal(0, transport.PositionFrames);
    }
    [Fact]
    public void DefaultFlowInstrumentCanBeMonitoredWithoutChangingProjectOrFiniteExport()
    {
        var document = ProjectFactory.Create(); var snapshot = document.Snapshot;
        var track = snapshot.Routing.Tracks[0].Id;
        var offline = ProjectCompiler.Prepare(snapshot, 1000, 16);
        Assert.Empty(offline.Monitors); Assert.False(offline.Playback.MonitoringEnabled);
        var prepared = ProjectCompiler.Prepare(snapshot, 1000, 16, monitoredTrack: track);
        var monitor = prepared.Monitors[track];
        Assert.Single(prepared.Monitors); Assert.True(prepared.Playback.MonitoringEnabled);
        var transport = new PreparedSineTransport(prepared.Playback);
        var output = new float[32];
        monitor.TryWrite(0x90, 69, 127); transport.Read(output);
        Assert.Contains(output, x => Math.Abs(x) > .01f);
        Assert.Equal(TransportState.Stopped, transport.State); Assert.Equal(0, transport.PositionFrames);
        Assert.Equal(offline.Playback.TotalFrames, prepared.Playback.TotalFrames);
        Assert.Same(snapshot, document.Snapshot); Assert.Equal(0, document.History.UndoCount);
        monitor.CloseAdmission(); transport.Read(output); Assert.All(output, x => Assert.Equal(0, x));
        Assert.Throws<ArgumentException>(() => ProjectCompiler.Prepare(snapshot, 1000, 16, monitoredTrack: Guid.NewGuid()));
    }
}
