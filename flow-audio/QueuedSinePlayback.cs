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
    private PreparedSineTransport _transport;
    private PreparedSineTransport? _pendingReplacement;
    private PreparedSineTransport? _retired;
    // Producer-owned identity guard; prevents publishing the active cursor twice.
    private PreparedSinePlayback _ownedSource;
    private long _totalFrames;
    private long _generation = 1;
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
    public int SampleRate { get; }
    public int MaxBlockFrames { get; }
    public long TotalFrames => Volatile.Read(ref _totalFrames);
    public long Generation => Volatile.Read(ref _generation);
    public bool ReplacementPending => Volatile.Read(ref _pendingReplacement) is not null;
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
        _ownedSource = source;
        _totalFrames = source.TotalFrames;
        SampleRate = source.SampleRate;
        MaxBlockFrames = source.MaxBlockFrames;
    }

    public bool TryPlay() => Enqueue(new(Kind.Play));
    public bool TryPause() => Enqueue(new(Kind.Pause));
    public bool TryClearLoop() => Enqueue(new(Kind.ClearLoop));

    public bool TrySeek(long frame)
    {
        if (ReplacementPending) return RejectCommand();
        if (frame < 0 || frame > TotalFrames) throw new ArgumentOutOfRangeException(nameof(frame));
        return Enqueue(new(Kind.Seek, frame));
    }

    public bool TrySetLoop(long startFrame, long endFrame)
    {
        if (ReplacementPending) return RejectCommand();
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

    /// <summary>
    /// Control-thread publication of an already prepared source. Requires the same
    /// sample rate and block limit. Success transfers exclusive ownership; failure
    /// leaves source ownership/cursor with the caller. Allocates a transport here,
    /// never in Read. One pending and one retired slot bound retained generations.
    /// Returns false during stop/replacement or until the retired source is collected.
    /// At installation, old commands are discarded and the new score starts stopped
    /// at zero with no loop. Commands are rejected while ReplacementPending; retry
    /// after acknowledgment, when TotalFrames and Generation describe the new score.
    /// </summary>
    public bool TryReplace(PreparedSinePlayback source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SampleRate != SampleRate || source.MaxBlockFrames != MaxBlockFrames)
            throw new ArgumentException("Replacement must match the host sample rate and block limit", nameof(source));
        if (StopPending || ReplacementPending || Volatile.Read(ref _retired) is not null) return false;
        if (ReferenceEquals(source, _ownedSource))
            throw new ArgumentException("The host already owns this source", nameof(source));
        if (Generation == long.MaxValue) throw new InvalidOperationException("Playback generation limit reached");
        var replacement = new PreparedSineTransport(source);
        _ownedSource = source;
        Volatile.Write(ref _pendingReplacement, replacement);
        return true;
    }

    /// <summary>
    /// Control-thread reclamation. Success transfers the old transport back to the
    /// caller; the audio thread will never touch it again. Drop/reuse its managed
    /// resources here, off the callback. Current dry-sine metadata needs no Dispose.
    /// </summary>
    public bool TryTakeRetired(out PreparedSineTransport? retired)
    {
        retired = Interlocked.Exchange(ref _retired, null);
        return retired is not null;
    }

    private bool Enqueue(Command command)
    {
        int next = Next(_tail);
        if (StopPending || ReplacementPending || next == Volatile.Read(ref _head)) return RejectCommand();
        _commands[_tail] = command;
        Volatile.Write(ref _tail, next);
        return true;
    }

    private bool RejectCommand()
    {
        Volatile.Write(ref _rejectedCommands, _rejectedCommands + 1);
        return false;
    }

    private void DiscardCommands()
    {
        int tail = Volatile.Read(ref _tail);
        int discarded = tail >= _head ? tail - _head : _commands.Length - _head + tail;
        Volatile.Write(ref _head, tail);
        Volatile.Write(ref _discardedCommands, _discardedCommands + discarded);
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
        var replacement = Volatile.Read(ref _pendingReplacement);
        bool installed = false;
        if (StopPending)
        {
            DiscardCommands();
            _transport.Stop();
            // Acknowledge last: producer may publish fresh commands only after the
            // old queue and transport have been reset. Those wait for another block.
            Volatile.Write(ref _stopPending, 0);
        }
        else if (replacement is not null)
        {
            DiscardCommands();
            var old = _transport;
            _transport = replacement;
            Volatile.Write(ref _totalFrames, replacement.TotalFrames);
            Volatile.Write(ref _generation, _generation + 1);
            // No more accesses to old after this release. The producer can reclaim
            // immediately; its retirement work never runs on the audio thread.
            Volatile.Write(ref _retired, old);
            installed = true;
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
        // Release producer submissions only after metadata/status and retirement
        // have been published. Fresh commands wait for the next block boundary.
        if (installed) Volatile.Write(ref _pendingReplacement, null);
        return frames;
    }

    private int Next(int index) => index + 1 == _commands.Length ? 0 : index + 1;
}
