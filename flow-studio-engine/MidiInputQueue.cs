namespace Flow.Studio.Engine;

/// <summary>Channel message stamped in the host recording frame clock.</summary>
public readonly record struct MidiInputEvent(long Frame, byte Status, byte Data1, byte Data2)
{
    public void Validate()
    {
        if (Frame < 0 || Status is < 0x80 or > 0xef || Data1 > 127 || Data2 > 127)
            throw new ArgumentException("Invalid timestamped MIDI channel message");
    }
}

/// <summary>Single input producer, single control consumer. Overflow is observable;
/// a recorder must invalidate a take when DroppedEvents changes. No callback allocation.</summary>
public sealed class MidiInputQueue
{
    private readonly MidiInputEvent[] _events;
    private int _head, _tail;
    private long _lastFrame = -1, _dropped;
    public long DroppedEvents => Interlocked.Read(ref _dropped);
    public MidiInputQueue(int capacity = 4096)
    {
        if (capacity is < 1 or > 65536) throw new ArgumentOutOfRangeException(nameof(capacity));
        _events = new MidiInputEvent[capacity + 1];
    }
    public bool TryWrite(MidiInputEvent message)
    {
        message.Validate();
        if (message.Frame < _lastFrame) throw new ArgumentException("Input clock must be monotonic within a capture session");
        _lastFrame = message.Frame;
        int next = (_tail + 1) % _events.Length;
        if (next == Volatile.Read(ref _head)) { Interlocked.Increment(ref _dropped); return false; }
        _events[_tail] = message; Volatile.Write(ref _tail, next); return true;
    }
    public bool TryRead(out MidiInputEvent message)
    {
        if (_head == Volatile.Read(ref _tail)) { message = default; return false; }
        message = _events[_head]; Volatile.Write(ref _head, (_head + 1) % _events.Length); return true;
    }
    /// <summary>Consumer-only observation; the producer cannot replace this slot
    /// until the consumer advances it with TryRead.</summary>
    public bool TryPeek(out MidiInputEvent message)
    {
        if (_head == Volatile.Read(ref _tail)) { message = default; return false; }
        message = _events[_head]; return true;
    }
}
