using Flow.Studio.Engine;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class MidiRecordingTests
{
    [Fact]
    public void SustainRetriggerAndChannelsProduceDeterministicNotes()
    {
        var take = new MidiNoteRecording(0);
        take.Process(new(0, 0x90, 60, 100));
        take.Process(new(1, 0x91, 60, 90));
        take.Process(new(2, 0xb0, 64, 127));
        take.Process(new(3, 0x90, 60, 0)); // Velocity-zero note-off held by pedal.
        take.Process(new(4, 0x81, 60, 0));
        take.Process(new(5, 0x90, 60, 80)); // Retrigger closes old sustained note.
        take.Process(new(6, 0x80, 60, 0));
        take.Process(new(7, 0xb0, 64, 0));
        Assert.Equal(new[] { new RecordedMidiNote(0, 60, 100, 0, 5), new(1, 60, 90, 1, 4), new(0, 60, 80, 5, 7) }, take.Complete(10));
        Assert.Throws<InvalidOperationException>(() => take.Complete(10));
    }
    [Fact]
    public void StopClosesHeldNotesAndFaultsPreventPartialTakeAcceptance()
    {
        var take = new MidiNoteRecording(10, 1);
        take.Process(new(10, 0x90, 60, 100)); take.Process(new(11, 0x90, 61, 100));
        Assert.Throws<InvalidOperationException>(() => take.Complete(20)); Assert.True(take.IsFaulted);
        Assert.Throws<InvalidOperationException>(() => take.Complete(20));
        var lost = new MidiNoteRecording(0); lost.ReportInputLoss(); Assert.Throws<InvalidOperationException>(() => lost.Complete(1));
        var zero = new MidiNoteRecording(0); zero.Process(new(0, 0x90, 60, 100)); Assert.Empty(zero.Complete(0)); Assert.Equal(1, zero.DroppedZeroLengthNotes);
        var stop = new MidiNoteRecording(0); stop.Process(new(0, 0x90, 60, 100)); Assert.Equal(10, stop.Complete(10).Single().EndFrame);
    }
    [Fact]
    public void InputQueueIsBoundedOrderedAndAllocationFree()
    {
        var queue = new MidiInputQueue(2);
        Assert.True(queue.TryWrite(new(0, 0x90, 60, 100))); Assert.True(queue.TryWrite(new(1, 0x80, 60, 0)));
        Assert.False(queue.TryWrite(new(2, 0x90, 61, 100))); Assert.Equal(1, queue.DroppedEvents);
        Assert.True(queue.TryRead(out var first)); Assert.Equal(0, first.Frame);
        Assert.True(queue.TryRead(out var second)); Assert.Equal(1, second.Frame); Assert.False(queue.TryRead(out _));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 3; i < 10003; i++) { queue.TryWrite(new(i, 0x90, 60, 100)); queue.TryRead(out _); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Throws<ArgumentException>(() => queue.TryWrite(new(0, 0x90, 60, 100)));
    }
    [Fact]
    public void AllNotesOffHonorsSustainButAllSoundOffStopsImmediately()
    {
        var take = new MidiNoteRecording(0);
        take.Process(new(0, 0x90, 60, 100)); take.Process(new(1, 0xb0, 64, 127));
        take.Process(new(2, 0xb0, 123, 0)); take.Process(new(3, 0xb0, 120, 0));
        Assert.Equal(3, take.Complete(4).Single().EndFrame);
    }
}
