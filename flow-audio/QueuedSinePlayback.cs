namespace Flow.Audio;

/// <summary>
/// Bounded single-producer/single-consumer control for prepared sine playback.
/// One control thread calls Try* and RequestStop; one audio thread calls Read.
/// Construction transfers exclusive source ownership. No other thread may use it.
/// Commands apply at the next nonempty block boundary, not at sample offsets.
/// </summary>
public sealed class QueuedSinePlayback
{
    private enum Kind { Play, Pause, Seek, SetLoop, ClearLoop }
    private readonly record struct Command(Kind Kind, long First = 0, long Second = 0);
    private readonly PreparedSineTransport _transport;
    private readonly Command[] _commands;
    // One spare slot distinguishes full from empty. Only the producer writes tail;
    // only the consumer writes head. Release/acquire publishes slot contents and
    // prevents slot reuse until the consumer has copied the command.
    private int _head, _tail;
    private int _stopPending;
    private long _rejectedCommands;
    private long _discardedCommands;
    private long _position;
    private int _state;

    public int Capacity { get; }
    public int SampleRate => _transport.SampleRate;
    public int MaxBlockFrames => _transport.MaxBlockFrames;
    public long TotalFrames => _transport.TotalFrames;
    public bool StopPending => Volatile.Read(ref _stopPending) != 0;
    public long RejectedCommands => Volatile.Read(ref _rejectedCommands);
    public long DiscardedCommands => Volatile.Read(ref _discardedCommands);
    // Independent observations, not an atomic multi-field snapshot. Published after
    // each nonempty Read; safe for a control thread to poll without touching DSP state.
    public long PositionFrames => Volatile.Read(ref _position);
    public TransportState State => (TransportState)Volatile.Read(ref _state);

    public QueuedSinePlayback(PreparedSinePlayback source, int capacity = 256)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (capacity < 1 || capacity > 65_536) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        _commands = new Command[capacity + 1];
        _transport = new(source);
    }

    public bool TryPlay() => Enqueue(new(Kind.Play));
    public bool TryPause() => Enqueue(new(Kind.Pause));
    public bool TryClearLoop() => Enqueue(new(Kind.ClearLoop));

    public bool TrySeek(long frame)
    {
        if (frame < 0 || frame > TotalFrames) throw new ArgumentOutOfRangeException(nameof(frame));
        return Enqueue(new(Kind.Seek, frame));
    }

    public bool TrySetLoop(long startFrame, long endFrame)
    {
        if (startFrame < 0 || startFrame >= TotalFrames) throw new ArgumentOutOfRangeException(nameof(startFrame));
        if (endFrame <= startFrame || endFrame > TotalFrames) throw new ArgumentOutOfRangeException(nameof(endFrame));
        return Enqueue(new(Kind.SetLoop, startFrame, endFrame));
    }

    /// <summary>
    /// Always accepted, even when full. Repeated requests coalesce. At the next
    /// boundary observing this request, the consumer discards queued commands and
    /// stops/rewinds before producing silence. Normal commands are rejected while
    /// stop is pending; retry after acknowledgment (StopPending == false).
    /// A request concurrent with an already-started block may take the next block.
    /// </summary>
    public void RequestStop() => Volatile.Write(ref _stopPending, 1);

    private bool Enqueue(Command command)
    {
        int next = Next(_tail);
        if (StopPending || next == Volatile.Read(ref _head))
        {
            Volatile.Write(ref _rejectedCommands, _rejectedCommands + 1);
            return false;
        }
        _commands[_tail] = command;
        Volatile.Write(ref _tail, next);
        return true;
    }

    /// <summary>
    /// Audio-thread entry point. Drains at most Capacity commands observed at entry,
    /// then renders. Valid calls allocate nothing, lock nothing and never wait for
    /// the producer. Invalid/empty buffers do not consume commands or acknowledge stop.
    /// Ordinary FIFO overflow returns false to the producer; nothing is overwritten.
    /// </summary>
    public int Read(Span<float> output)
    {
        if (output.Length % 2 != 0 || output.Length / 2 > MaxBlockFrames)
            throw new ArgumentException("Output must contain stereo frames within the prepared block limit", nameof(output));
        if (output.IsEmpty) return 0;
        if (StopPending)
        {
            int tail = Volatile.Read(ref _tail);
            int discarded = tail >= _head ? tail - _head : _commands.Length - _head + tail;
            Volatile.Write(ref _head, tail);
            Volatile.Write(ref _discardedCommands, _discardedCommands + discarded);
            _transport.Stop();
            // Acknowledge last: producer may publish fresh commands only after the
            // old queue and transport have been reset. Those wait for another block.
            Volatile.Write(ref _stopPending, 0);
        }
        else
        {
            int boundary = Volatile.Read(ref _tail);
            while (_head != boundary)
            {
                var command = _commands[_head];
                Volatile.Write(ref _head, Next(_head));
                switch (command.Kind)
                {
                    case Kind.Play: _transport.Play(); break;
                    case Kind.Pause: _transport.Pause(); break;
                    case Kind.Seek: _transport.Seek(command.First); break;
                    case Kind.SetLoop: _transport.SetLoop(command.First, command.Second); break;
                    case Kind.ClearLoop: _transport.ClearLoop(); break;
                }
            }
        }
        int frames = _transport.Read(output);
        Volatile.Write(ref _position, _transport.PositionFrames);
        Volatile.Write(ref _state, (int)_transport.State);
        return frames;
    }

    private int Next(int index) => index + 1 == _commands.Length ? 0 : index + 1;
}
