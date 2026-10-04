using Flow.Studio.Engine;
using Flow.Studio.Model;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class MidiRecordingSessionTests
{
    private static ProjectDocument Document()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        return new(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter), routing: new([new(Guid.NewGuid(), "Recording")], null)));
    }
    [Fact]
    public void StopDrainsInBoundedTicksThenCommitsCapturedTake()
    {
        var doc = Document(); var session = new MidiRecordingSession(doc.Snapshot, 0, 0, 1000);
        Assert.True(session.TryCapture(new(0, 0x90, 60, 100))); Assert.True(session.TryCapture(new(100, 0x80, 60, 0)));
        Assert.True(session.TryCapture(new(200, 0x90, 64, 90))); session.RequestStop(500);
        Assert.False(session.TryCapture(new(501, 0x80, 64, 0)));
        session.Poll(1); Assert.Equal(MidiRecordingState.Stopping, session.State); Assert.Null(session.Take);
        session.Poll(1); session.Poll(1); session.Poll(1);
        Assert.Equal(MidiRecordingState.Completed, session.State); Assert.Equal(2, session.Take!.Notes.Count);
        Assert.True(session.Take.Commit(doc, doc.Snapshot.Routing.Tracks.Single().Id)); Assert.Equal(1, doc.History.UndoCount);
    }
    [Fact]
    public void OverflowDiscontinuityAndBadTimeCannotProduceSuccessfulTakes()
    {
        var doc = Document(); var overflow = new MidiRecordingSession(doc.Snapshot, 0, 0, 1000, queueCapacity: 1);
        Assert.True(overflow.TryCapture(new(0, 0x90, 60, 100))); Assert.False(overflow.TryCapture(new(1, 0x80, 60, 0)));
        overflow.RequestStop(10); overflow.Poll(); Assert.Equal(MidiRecordingState.Faulted, overflow.State); Assert.Null(overflow.Take);
        Assert.Equal(1, overflow.DroppedEvents);
        var seek = new MidiRecordingSession(doc.Snapshot, 0, 0, 1000); seek.ReportDiscontinuity("Transport seek"); seek.Poll();
        Assert.Equal(MidiRecordingState.Faulted, seek.State); Assert.False(seek.TryCapture(new(0, 0x90, 60, 100)));
        var invalid = new MidiRecordingSession(doc.Snapshot, 10, 0, 1000); Assert.False(invalid.TryCapture(new(9, 0x90, 60, 100)));
        invalid.Poll(); Assert.Equal(MidiRecordingState.Faulted, invalid.State);
    }
    [Fact]
    public async Task StopRacingProducerDrainsEveryAcceptedNote()
    {
        var doc = Document(); var session = new MidiRecordingSession(doc.Snapshot, 0, 0, 1000, queueCapacity: 65536);
        int accepted = 0;
        var writer = Task.Run(() =>
        {
            for (int i = 0; i < 10000; i++)
            {
                if (!session.TryCapture(new(i, 0x90, 60, 100))) break;
                Interlocked.Increment(ref accepted);
            }
        });
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref accepted) > 0, TimeSpan.FromSeconds(5)));
        session.RequestStop(20000);
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        while (session.State == MidiRecordingState.Stopping && deadline.Elapsed < TimeSpan.FromSeconds(10))
        { session.Poll(127); await Task.Yield(); }
        await writer;
        Assert.Equal(MidiRecordingState.Completed, session.State); Assert.Equal(accepted, session.Take!.Notes.Count);
        Assert.False(session.TryCapture(new(20001, 0x90, 61, 100)));
    }
    [Fact]
    public void CancellationCreatesNoTakeAndValidAdmissionAllocatesNothing()
    {
        var doc = Document(); var session = new MidiRecordingSession(doc.Snapshot, 0, 0, 1000);
        session.TryCapture(new(0, 0x90, 60, 100));
        long before = GC.GetAllocatedBytesForCurrentThread(); session.TryCapture(new(1, 0x80, 60, 0));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        session.Cancel(); session.Poll(); Assert.Equal(MidiRecordingState.Cancelled, session.State); Assert.Null(session.Take);
        Assert.Equal(0, doc.History.UndoCount);
    }
}
