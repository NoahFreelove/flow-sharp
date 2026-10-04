using Flow.Studio.Engine;
using Flow.Studio.Model;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

public class ContinuousMetronomeTests
{
    [Fact]
    public void EmptyProjectRecordingHasClicksButPausedMonitoringAndOfflineExportStaySilent()
    {
        var doc = Flow.Studio.Host.ProjectFactory.Create();
        var prepared = ProjectCompiler.Prepare(doc.Snapshot, 8000, 32).Playback;
        prepared.AttachTimelineMonitor(new PreparedMetronome(doc.Snapshot.Context.Tempo, doc.Snapshot.Context.Meter, 8000, 32));
        var transport = new Flow.Audio.PreparedSineTransport(prepared); var samples = new float[64];
        Assert.Equal(0, prepared.TotalFrames); transport.Read(samples); Assert.All(samples, value => Assert.Equal(0, value));
        transport.BeginRecording(); transport.Read(samples); Assert.Contains(samples, value => value != 0);
        Assert.Equal(32, transport.PositionFrames); Assert.Equal(0, transport.TotalFrames);
        transport.Pause(); transport.Read(samples); Assert.All(samples, value => Assert.Equal(0, value));
        Assert.False(ProjectCompiler.Prepare(doc.Snapshot, 8000, 32).Playback.MonitoringEnabled);
    }

    [Fact]
    public void ContinuousClicksMatchFiniteScheduleAcrossTempoMeterAndSeek()
    {
        var tempo = new ProjectTempoMap([new(0, 120), new(2, 90)]);
        var meter = new ProjectMeterMap([new(1, 4, 4), new(2, 3, 8)]);
        var beats = MetronomeSchedule.Between(tempo, meter, 0, 7, 8000);
        long end = (long)Math.Round(tempo.SecondsAt(7) * 8000, MidpointRounding.AwayFromZero);
        var finite = MetronomePlayback.Prepare(beats, end, 8000, 32);
        var continuous = new PreparedMetronome(tempo, meter, 8000, 32);
        var expected = new float[64]; var actual = new float[64];
        for (long frame = 0; frame < end; frame += 32)
        {
            int samples = (int)Math.Min(32, end - frame) * 2;
            finite.Read(expected.AsSpan(0, samples)); continuous.ReadAt(actual.AsSpan(0, samples), frame);
            Assert.Equal(expected[..samples], actual[..samples]);
        }
        continuous.ReadAt(actual, 0); Assert.Contains(actual, value => value != 0); // loop/seek retriggers bar accent.
        continuous.ReadAt(actual, 0, false); Assert.All(actual, value => Assert.Equal(0, value));
        continuous.ReadAt(actual, 0); Assert.Contains(actual, value => value != 0);
    }

    [Fact]
    public void ArbitraryLateTimelineNeedsNoFiniteSongOrCallbackAllocation()
    {
        var monitor = new PreparedMetronome(new([new(0, 120)]), new([new(1, 4, 4)]), 8000, 32);
        var output = new float[64]; long start = 8000L * 3600;
        monitor.ReadAt(output, start); Assert.Contains(output, value => value != 0);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 1; i < 512; i++) monitor.ReadAt(output, start + i * 32);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
