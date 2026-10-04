using Flow.Audio;
using Flow.Platform.Linux;
using Flow.Studio.Engine;
using Flow.Studio.Host;
using FlowLang.Tests.StudioModel;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

public class ProjectMidiRecordingHostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CountedCaptureDrainsEarlyInputAndHandlesLatePollOrPreBoundaryStop(bool stopBeforeBoundary)
    {
        var doc = ProjectFactory.Create();
        Flow.Studio.Model.ProjectTimingCommands.Set(doc, new([new(0, 121)]), doc.Snapshot.Context.Meter);
        Output? output = null; Input? input = null; long now = 0;
        long frequency = System.Diagnostics.Stopwatch.Frequency;
        await using var playback = new ProjectPlaybackSession(doc, "/tmp", 1000, 16,
            q => new PlaybackOutputSession(q, p => output = new(p)));
        using var host = new ProjectMidiRecordingHost(playback, (_, receive) => input = new(receive), () => now, frequency);
        Assert.True(playback.Connect()); var queue = playback.Playback.Queue;
        queue.TryReadClock(out var clock); now = clock.Timestamp;
        var before = doc.Snapshot;
        host.ArmCounted("keys", before.Routing.Tracks[0].Id);
        Assert.True(host.CountInRemainingFrames > 0);
        int earlyEvents = 0;
        for (int i = 0; i < 200; i++)
        {
            output!.Render(); Assert.True(queue.TryReadClock(out clock)); now = clock.Timestamp;
            if (clock.RecordingEnabled) break;
            Assert.Equal(0, clock.PositionFrames);
            for (int j = 0; j < 100; j++) { input!.Send(now, 0xb0, 1, 10); earlyEvents++; }
            host.Poll(); Assert.Null(host.Error); Assert.Equal(MidiRecordingState.Arming, host.State);
        }
        Assert.True(earlyEvents > 4096); Assert.True(clock.RecordingEnabled);
        long start = clock.RecordingStartTimestamp;
        if (stopBeforeBoundary)
        {
            Assert.True(start > now);
            Assert.True(host.Stop(TimeSpan.Zero)); Assert.Equal(MidiRecordingState.Cancelled, host.State);
            Assert.Null(host.Take);
        }
        else
        {
            input!.Send(start - 1, 0x90, 50, 100); // Excluded even though the host has not observed activation yet.
            input.Send(start + frequency / 10, 0x90, 69, 127);
            input.Send(start + frequency / 5, 0x80, 69, 0);
            now = start + frequency * 3 / 10;
            Assert.True(host.Stop(TimeSpan.Zero)); Assert.Equal(MidiRecordingState.Completed, host.State);
            var note = Assert.Single(host.Take!.Notes); Assert.Equal(69, note.Pitch!.MidiKey);
            Assert.Equal(0, host.Take.AnchorQuarters);
            Assert.Equal(121d / 600, note.OffsetQuarters, 8);
        }
        Assert.False(host.DeviceOwned); Assert.Same(before, doc.Snapshot);
        output!.Render(); Assert.True(queue.TryReadClock(out clock)); Assert.False(clock.RecordingEnabled);
        Assert.Equal(0, clock.CountInRemainingFrames);
    }

    private sealed class Output(CallbackRenderProbe probe) : IPlaybackOutputStream
    {
        public bool Active;
        public bool IsActive => Active;
        public bool CallbackFaulted => false;
        public void Start() => Active = true;
        public void Dispose() => Active = false;
        public void Render() => probe.Process(new float[32]);
    }
    private sealed class Input(MidiPacketReceiver receiver) : IMidiInputConnection
    {
        public Exception? Error { get; set; }
        public bool IsRunning { get; set; } = true;
        public bool BlockStop;
        public bool FailDuringStop;
        public int Stops;
        public void Send(long ticks, params byte[] bytes) => receiver(bytes, ticks);
        public bool TryStop(TimeSpan timeout)
        {
            Stops++;
            if (BlockStop) return false;
            if (FailDuringStop) Error = new IOException("last read failed");
            IsRunning = false; return true;
        }
        public void Dispose() => TryStop(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyFlowProjectRecordsFromAcknowledgedBoundaryAndExtendsOnlyOnCommit(bool customTuning)
    {
        var doc = ProjectFactory.Create();
        if (customTuning) doc = new(FlowLang.Hosting.GeneratorTuning.WithTuning(doc.Snapshot,
            new(scala: "ET\n12\n100.0\n200.0\n300.0\n400.0\n500.0\n600.0\n700.0\n800.0\n900.0\n1000.0\n1100.0\n2/1\n",
                keyboardMap: "0\n0\n127\n60\n69\n432.0\n0\n")));
        Output? output = null; Input? input = null; long now = 0;
        long frequency = System.Diagnostics.Stopwatch.Frequency;
        await using var playback = new ProjectPlaybackSession(doc, "/tmp", 1000, 16,
            q => new PlaybackOutputSession(q, p => output = new(p)));
        using var host = new ProjectMidiRecordingHost(playback, (_, receive) => input = new(receive), () => now, frequency);
        Assert.True(playback.Connect());
        var queue = playback.Playback.Queue;
        Assert.Equal(0, queue.TotalFrames);
        queue.TryReadClock(out var before); now = before.Timestamp;
        host.Arm("keys", doc.Snapshot.Routing.Tracks[0].Id);
        Assert.Equal(MidiRecordingState.Arming, host.State);
        input!.Send(now, 0x90, 50, 100); // Before activation: outside this take.
        output!.Render(); queue.TryReadClock(out var clock);
        Assert.True(clock.RecordingEnabled); Assert.Equal(0, clock.RecordingStartFrame);
        long start = clock.RecordingStartTimestamp;
        input.Send(start + frequency / 10, 0x90, 64, 127);
        input.Send(start + frequency * 3 / 10, 0x80, 64, 0);
        now = start + frequency / 2;
        // No control tick occurred before these messages; arming must retain them.
        Assert.True(host.Stop(TimeSpan.Zero));
        for (int i = 0; i < 10 && host.State == MidiRecordingState.Stopping; i++) host.Poll(1);
        Assert.Equal(MidiRecordingState.Completed, host.State); Assert.Null(host.Error);
        var take = Assert.IsType<MidiRecordingTake>(host.Take);
        var note = Assert.Single(take.Notes);
        Assert.Equal((customTuning ? 432 : 440) * Math.Pow(2, (64 - 69) / 12.0), note.Pitch!.FrequencyHz, 8);
        Assert.Equal(0, take.AnchorQuarters); Assert.Equal(.2, note.OffsetQuarters, 10);
        Assert.Equal(.4, note.DurationQuarters, 10); Assert.Equal(1, take.LengthQuarters, 10);
        Assert.Empty(doc.Snapshot.Arrangement.ScoreClips); Assert.Equal(0, doc.History.UndoCount);
        output.Render(); queue.TryReadClock(out clock);
        Assert.False(clock.RecordingEnabled); Assert.Equal(TransportState.Stopped, clock.State);
        Assert.Equal(0, clock.PositionFrames); Assert.Equal(0, queue.TotalFrames);
        Assert.True(host.Commit()); Assert.Equal(1, doc.History.UndoCount);
        Assert.Single(doc.Snapshot.Arrangement.ScoreClips);
        Assert.True(doc.History.Undo()); Assert.Empty(doc.Snapshot.Arrangement.ScoreClips);
        Assert.True(doc.History.Redo()); Assert.Single(doc.Snapshot.Arrangement.ScoreClips);
    }

    [Fact]
    public async Task FullInputBudgetDrainsAfterStopWithoutMovingTheCapturedEndTime()
    {
        var doc = ProjectFactory.Create(); Output? output = null; Input? input = null; long now = 0;
        long frequency = System.Diagnostics.Stopwatch.Frequency;
        await using var playback = new ProjectPlaybackSession(doc, "/tmp", 1000, 16,
            q => new PlaybackOutputSession(q, p => output = new(p)));
        using var host = new ProjectMidiRecordingHost(playback, (_, receive) => input = new(receive), () => now, frequency);
        playback.Connect(); var queue = playback.Playback.Queue;
        queue.TryReadClock(out var clock); now = clock.Timestamp;
        host.Start("keys", doc.Snapshot.Routing.Tracks[0].Id);
        output!.Render(); queue.TryReadClock(out clock); long origin = clock.RecordingStartTimestamp;
        input!.Send(origin, 0x90, 60, 100);
        for (int i = 1; i < 4096; i++) input.Send(origin + i, 0xb0, 1, 10);
        now = origin + frequency;
        Assert.True(host.Stop(TimeSpan.Zero)); Assert.Equal(MidiRecordingState.Stopping, host.State);
        Assert.Null(host.Take); Assert.False(host.DeviceOwned);
        output.Render(); // Release is acknowledged before the take finishes draining.
        now += frequency;
        host.Poll(1);
        Assert.Equal(MidiRecordingState.Completed, host.State); Assert.Null(host.Error);
        Assert.Equal(2, host.Take!.LengthQuarters, 10);
        Assert.Equal(2, Assert.Single(host.Take.Notes).DurationQuarters, 10);
    }

    [Fact]
    public async Task ArmingOverflowAndCancellationReleaseTheExtendedTransport()
    {
        var doc = ProjectFactory.Create(); Output? output = null; Input? input = null; long now = 0;
        await using var playback = new ProjectPlaybackSession(doc, "/tmp", 1000, 16,
            q => new PlaybackOutputSession(q, p => output = new(p)));
        using var host = new ProjectMidiRecordingHost(playback, (_, receive) => input = new(receive),
            () => now, System.Diagnostics.Stopwatch.Frequency);
        playback.Connect(); var queue = playback.Playback.Queue;
        queue.TryReadClock(out var clock); now = clock.Timestamp;
        host.Arm("keys", doc.Snapshot.Routing.Tracks[0].Id);
        for (int i = 0; i < 4097; i++) input!.Send(now + i, 0x90, 60, 100);
        host.Poll(); Assert.Equal(MidiRecordingState.Faulted, host.State); Assert.Null(host.Take);
        output!.Render(); queue.TryReadClock(out clock);
        Assert.False(clock.RecordingEnabled); Assert.False(host.DeviceOwned);
        now = clock.Timestamp;
        host.Arm("keys", doc.Snapshot.Routing.Tracks[0].Id);
        host.Cancel(); output.Render(); queue.TryReadClock(out clock);
        Assert.False(clock.RecordingEnabled); Assert.Null(host.Take); Assert.False(host.DeviceOwned);
        Assert.Equal(0, doc.History.UndoCount);
    }

    [Fact]
    public async Task NativePacketsBecomeOneEditableUndoableTakeAtThePlaybackClock()
    {
        var (doc, _) = ProjectPlaybackCoordinatorTests.Create();
        Output? output = null; Input? input = null; long now = 0;
        await using var playback = new ProjectPlaybackSession(doc, "/tmp", 1000, 16,
            q => new PlaybackOutputSession(q, p => output = new(p)));
        using var host = new ProjectMidiRecordingHost(playback, (_, receive) => input = new(receive), () => now, System.Diagnostics.Stopwatch.Frequency);
        Assert.Throws<InvalidOperationException>(() => host.Start("keys", doc.Snapshot.Routing.Tracks[0].Id));
        Assert.True(playback.Connect()); Assert.True(playback.Playback.Queue.TryPlay()); output!.Render();
        Assert.True(playback.Playback.Queue.TryReadClock(out var clock)); now = clock.Timestamp;
        host.Start("keys", doc.Snapshot.Routing.Tracks[0].Id);
        output.Render(); playback.Playback.Queue.TryReadClock(out clock);
        now = clock.RecordingStartTimestamp;
        long frequency = System.Diagnostics.Stopwatch.Frequency;
        int history = doc.History.UndoCount;
        input!.Send(now + frequency / 10, 0x91, 64, 127);
        input.Send(now + frequency * 3 / 10, 0x81, 64, 0);
        now += frequency / 2;
        Assert.True(host.Stop(TimeSpan.Zero));
        Assert.Equal(MidiRecordingState.Completed, host.State); Assert.Null(host.Error);
        var take = Assert.IsType<MidiRecordingTake>(host.Take);
        var note = Assert.Single(take.Notes);
        Assert.Equal(.032, take.AnchorQuarters, 10);
        Assert.Equal(.2, note.OffsetQuarters, 10); Assert.Equal(.4, note.DurationQuarters, 10);
        Assert.Equal("midi-channel-2", note.VoiceId);
        Assert.Equal(history, doc.History.UndoCount);
        Assert.True(host.Commit()); Assert.Equal(history + 1, doc.History.UndoCount);
        Assert.True(doc.History.Undo()); Assert.True(doc.History.Redo());
        Assert.Contains(take.SourceId, doc.Snapshot.Sources.Keys);
        Assert.False(host.DeviceOwned);
    }

    [Theory]
    [InlineData("pause-resume")]
    [InlineData("device")]
    [InlineData("malformed")]
    [InlineData("stop-fault")]
    [InlineData("timeout")]
    [InlineData("project")]
    [InlineData("stale")]
    public async Task DiscontinuitiesCannotProduceACommittableTake(string failure)
    {
        var (doc, clip) = ProjectPlaybackCoordinatorTests.Create();
        Output? output = null; Input? input = null; long now = 0;
        await using var playback = new ProjectPlaybackSession(doc, "/tmp", 1000, 16,
            q => new PlaybackOutputSession(q, p => output = new(p)));
        using var host = new ProjectMidiRecordingHost(playback, (_, receive) => input = new(receive), () => now, System.Diagnostics.Stopwatch.Frequency);
        playback.Connect(); playback.Playback.Queue.TryPlay(); output!.Render();
        playback.Playback.Queue.TryReadClock(out var clock); now = clock.Timestamp;
        host.Start("keys", doc.Snapshot.Routing.Tracks[0].Id);
        output.Render(); playback.Playback.Queue.TryReadClock(out clock);
        now = clock.RecordingStartTimestamp;
        input!.Send(now + System.Diagnostics.Stopwatch.Frequency / 100, 0x90, 60, 100);
        now += System.Diagnostics.Stopwatch.Frequency / 10;
        switch (failure)
        {
            case "pause-resume": playback.Playback.Queue.TryPause(); playback.Playback.Queue.TryPlay(); output.Render(); break;
            case "stale": now += System.Diagnostics.Stopwatch.Frequency * 3; break;
            case "device": input.Error = new IOException("unplugged"); break;
            case "malformed": input.Send(now, 0x90, 60); break;
            case "stop-fault": input.FailDuringStop = true; break;
            case "timeout": input.BlockStop = true; break;
            case "project": Flow.Studio.Model.ProjectClipCommands.Move(doc, [clip], 1); break;
        }
        host.Stop(TimeSpan.Zero);
        Assert.Equal(MidiRecordingState.Faulted, host.State);
        Assert.NotNull(host.Error); Assert.Null(host.Take);
        Assert.Throws<InvalidOperationException>(() => host.Commit());
        if (failure == "timeout")
        {
            Assert.True(host.DeviceOwned); input.BlockStop = false;
            Assert.True(host.Stop(TimeSpan.Zero)); Assert.False(host.DeviceOwned);
        }
    }

    [Fact]
    public async Task LoopedOrPendingTransportCannotStartRecording()
    {
        var (doc, _) = ProjectPlaybackCoordinatorTests.Create(); Output? output = null;
        await using var playback = new ProjectPlaybackSession(doc, "/tmp", 1000, 16,
            q => new PlaybackOutputSession(q, p => output = new(p)));
        using var host = new ProjectMidiRecordingHost(playback, (_, _) => throw new Exception("Must not open"), () => 0, 1000);
        playback.Connect(); playback.Playback.Queue.TryPlay();
        Assert.Throws<InvalidOperationException>(() => host.Start("keys", doc.Snapshot.Routing.Tracks[0].Id));
        playback.Playback.Queue.TrySetLoop(0, 500); output!.Render();
        Assert.Throws<InvalidOperationException>(() => host.Start("keys", doc.Snapshot.Routing.Tracks[0].Id));
    }
}
