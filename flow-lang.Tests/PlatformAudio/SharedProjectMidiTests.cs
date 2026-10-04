using System.Diagnostics;
using Flow.Audio;
using Flow.Platform.Linux;
using Flow.Studio.Engine;
using Flow.Studio.Host;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

public class SharedProjectMidiTests
{
    private sealed class Output(CallbackRenderProbe probe) : IPlaybackOutputStream
    {
        public bool IsActive { get; private set; }
        public bool CallbackFaulted => false;
        public void Start() => IsActive = true;
        public void Dispose() => IsActive = false;
        public void Render(float[] output) => probe.Process(output);
    }
    private sealed class Input(MidiPacketReceiver receive) : IMidiInputConnection
    {
        public Exception? Error { get; set; }
        public bool IsRunning { get; private set; } = true;
        public int Stops;
        public void Send(params byte[] packet) => receive(packet, Stopwatch.GetTimestamp());
        public bool TryStop(TimeSpan timeout) { Stops++; IsRunning = false; return true; }
        public void Dispose() => TryStop(TimeSpan.Zero);
    }

    [Fact]
    public async Task AuditionAndRecordingUseOneKeyboardAndStoppingTakeLeavesAuditionConnected()
    {
        var document = ProjectFactory.Create(); Output? output = null; Input? input = null; int opens = 0;
        await using var session = new ProjectPlaybackSession(document, "/tmp", 1000, 16,
            q => new PlaybackOutputSession(q, p => output = new(p)),
            (_, receive) => { opens++; return input = new(receive); });
        session.Connect(); var track = document.Snapshot.Routing.Tracks[0].Id;
        session.SetMonitoredTrack(track); var samples = new float[32];
        Assert.True(SpinWait.SpinUntil(() =>
        { output!.Render(samples); session.Poll(); return session.Playback.TryGetMonitor(out _); }, TimeSpan.FromSeconds(5)));
        session.MidiMonitoring.Connect("keys");
        input!.Send(0x90, 69, 127); output!.Render(samples);
        Assert.Contains(samples, x => Math.Abs(x) > .01f);
        Assert.Equal(TransportState.Stopped, session.Playback.Queue.State);
        input.Send(0x80, 69, 0); output.Render(samples);
        session.MidiRecording.Start("keys", track); output.Render(samples); session.Poll();
        Assert.Equal(MidiRecordingState.Recording, session.MidiRecording.State); Assert.Equal(1, opens);
        input.Send(0x90, 60, 100);
        await Task.Delay(20, TestContext.Current.CancellationToken);
        input.Send(0x80, 60, 0);
        Assert.True(session.MidiRecording.Stop(TimeSpan.FromSeconds(1)));
        Assert.Equal(MidiRecordingState.Completed, session.MidiRecording.State);
        Assert.Single(session.MidiRecording.Take!.Notes); Assert.True(session.MidiMonitoring.IsConnected);
        Assert.Equal(0, input.Stops); Assert.Equal(1, session.MidiInputHub.SubscriberCount);
        input.Send(0x90, 72, 127); output.Render(samples);
        Assert.Contains(samples, x => Math.Abs(x) > .01f);
        Assert.True(session.MidiRecording.Commit());
        Assert.Single(document.Snapshot.Arrangement.ScoreClips);
        Assert.True(SpinWait.SpinUntil(() =>
        { output.Render(samples); session.Poll(); return !session.IsPreparing && session.Playback.TryGetMonitor(out _); }, TimeSpan.FromSeconds(5)));
        input.Send(0x90, 67, 127); output.Render(samples);
        Assert.Contains(samples, x => Math.Abs(x) > .01f); Assert.True(session.MidiMonitoring.IsConnected);
        Assert.Equal(1, opens); Assert.Equal(0, input.Stops);
        Assert.True(session.MidiMonitoring.Disconnect(TimeSpan.FromSeconds(1)));
        Assert.Equal(1, input.Stops); Assert.Equal(1, opens);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeviceLossFaultsBothPathsButMonitorOverflowDoesNotLoseRecordedInput(bool overflowMonitor)
    {
        var document = ProjectFactory.Create(); Output? output = null; Input? input = null;
        await using var session = new ProjectPlaybackSession(document, "/tmp", 1000, 16,
            q => new PlaybackOutputSession(q, p => output = new(p)), (_, receive) => input = new(receive));
        session.Connect(); var track = document.Snapshot.Routing.Tracks[0].Id;
        session.SetMonitoredTrack(track); var samples = new float[32];
        Assert.True(SpinWait.SpinUntil(() =>
        { output!.Render(samples); session.Poll(); return session.Playback.TryGetMonitor(out _); }, TimeSpan.FromSeconds(5)));
        session.MidiMonitoring.Connect("keys");
        session.MidiRecording.Start("keys", track); output!.Render(samples); session.Poll();
        if (overflowMonitor)
        {
            // Drain recording on the control owner, but deliberately starve the
            // monitor's audio consumer. Only the monitor queue should overflow.
            for (int i = 0; i < 4097; i++)
            { input!.Send(0x90, 60, 100); if (i % 512 == 511) session.Poll(); }
            session.Poll();
            Assert.NotNull(session.MidiMonitoring.Error); Assert.False(session.MidiMonitoring.IsConnected);
            Assert.Equal(MidiRecordingState.Recording, session.MidiRecording.State);
            Assert.Null(session.MidiRecording.Error); Assert.Equal(0, input!.Stops);
            await Task.Delay(20, TestContext.Current.CancellationToken);
            Assert.True(session.MidiRecording.Stop(TimeSpan.FromSeconds(1)));
            Assert.Equal(MidiRecordingState.Completed, session.MidiRecording.State);
            Assert.NotEmpty(session.MidiRecording.Take!.Notes);
        }
        else
        {
            input!.Error = new IOException("device lost"); session.Poll();
            Assert.Equal(MidiRecordingState.Faulted, session.MidiRecording.State);
            Assert.Null(session.MidiRecording.Take); Assert.NotNull(session.MidiMonitoring.Error);
            Assert.False(session.MidiMonitoring.IsConnected);
        }
        Assert.False(session.MidiInputHub.DeviceOwned); Assert.Equal(1, input!.Stops);
    }

}
