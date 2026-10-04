using Flow.Music.Model;
using Flow.Studio.Model;
namespace Flow.Studio.Engine;

public enum MidiRecordingState { Recording, Stopping, Completed, Cancelled, Faulted, Arming }

/// <summary>One input producer calls TryCapture; one control owner calls Poll/Stop/Cancel.
/// Stop closes admission then Poll drains the in-flight writer before completing.
/// No native devices or audio callbacks are owned here.</summary>
public sealed class MidiRecordingSession
{
    private readonly MidiInputQueue _queue;
    private readonly MidiNoteRecording _recording;
    private readonly ProjectSnapshot _snapshot;
    private readonly MidiPitchMap _pitchMap;
    private readonly long _start, _projectStart;
    private readonly int _sampleRate, _latency;
    private int _accepting = 1, _writers;
    private Exception? _error;
    private long _end;
    public MidiRecordingState State { get; private set; } = MidiRecordingState.Recording;
    public Exception? Error => Volatile.Read(ref _error);
    public MidiRecordingTake? Take { get; private set; }
    public long DroppedEvents => _queue.DroppedEvents;
    public MidiRecordingSession(ProjectSnapshot snapshot, long captureStartFrame, long projectStartFrame,
        int sampleRate, int inputLatencyFrames = 0, int queueCapacity = 4096, int maxNotes = 100000, MidiPitchMap? midiPitchMap = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (captureStartFrame < 0 || projectStartFrame < 0 || sampleRate is < 1 or > 384000 ||
            inputLatencyFrames < 0 || inputLatencyFrames > (long)sampleRate * 10) throw new ArgumentException("Invalid recording clock");
        if (midiPitchMap is null && (snapshot.Context.Tuning.Scala is not null || snapshot.Context.Tuning.System != "EqualTemperament"))
            throw new ArgumentException("Recording requires a resolved MIDI pitch map for this tuning");
        _pitchMap = midiPitchMap ?? MidiPitchMap.EqualTemperament;
        _snapshot = snapshot; _start = captureStartFrame; _projectStart = projectStartFrame;
        _sampleRate = sampleRate; _latency = inputLatencyFrames;
        _queue = new(queueCapacity); _recording = new(captureStartFrame, maxNotes);
    }
    public bool TryCapture(MidiInputEvent message)
    {
        Interlocked.Increment(ref _writers);
        try
        {
            if (Volatile.Read(ref _accepting) == 0) return false;
            if (message.Frame < _start) throw new ArgumentException("Input precedes recording start");
            if (_queue.TryWrite(message)) return true;
            Fail(new InvalidOperationException("MIDI input queue overflow; take is incomplete")); return false;
        }
        catch (Exception error) { Fail(error); return false; }
        finally { Interlocked.Decrement(ref _writers); }
    }
    /// <summary>Report pause/seek/loop, device loss or an input timestamp discontinuity.</summary>
    public void ReportDiscontinuity(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (State is MidiRecordingState.Completed or MidiRecordingState.Cancelled) return;
        Fail(new InvalidOperationException(reason));
    }
    public void RequestStop(long captureEndFrame)
    {
        if (State != MidiRecordingState.Recording) throw new InvalidOperationException("Recording cannot be stopped in its current state");
        if (captureEndFrame <= _start) throw new ArgumentOutOfRangeException(nameof(captureEndFrame));
        _end = captureEndFrame; Interlocked.Exchange(ref _accepting, 0); State = MidiRecordingState.Stopping;
    }
    public void Poll(int maxEvents = 4096)
    {
        if (maxEvents is < 1 or > 65536) throw new ArgumentOutOfRangeException(nameof(maxEvents));
        if (State is MidiRecordingState.Completed or MidiRecordingState.Cancelled or MidiRecordingState.Faulted) return;
        if (Error is not null) { State = MidiRecordingState.Faulted; return; }
        try
        {
            int count = 0;
            while (count < maxEvents && _queue.TryRead(out var message)) { _recording.Process(message); count++; }
            if (Error is not null) { State = MidiRecordingState.Faulted; return; }
            // An in-flight producer may still publish; wait for a later control tick.
            if (State != MidiRecordingState.Stopping || count == maxEvents || Volatile.Read(ref _writers) != 0) return;
            // Admission is closed and writer is gone; drain once more to cover a
            // publication that raced the first empty read. Still respect tick budget.
            while (count < maxEvents && _queue.TryRead(out var message)) { _recording.Process(message); count++; }
            if (count == maxEvents) return;
            if (Error is not null) { State = MidiRecordingState.Faulted; return; }
            Take = new(_snapshot, _recording.Complete(_end), _start, _end, _projectStart, _sampleRate, _latency, _pitchMap);
            State = MidiRecordingState.Completed;
        }
        catch (Exception error) { Fail(error); State = MidiRecordingState.Faulted; }
    }
    public void Cancel()
    {
        if (State == MidiRecordingState.Completed) throw new InvalidOperationException("Take is already complete");
        Interlocked.Exchange(ref _accepting, 0); State = MidiRecordingState.Cancelled; Take = null;
    }
    private void Fail(Exception error)
    { Interlocked.CompareExchange(ref _error, error, null); Interlocked.Exchange(ref _accepting, 0); }
}
