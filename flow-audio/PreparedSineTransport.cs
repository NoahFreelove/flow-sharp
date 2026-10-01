namespace Flow.Audio;

public enum TransportState { Stopped, Playing, Paused }

/// <summary>
/// Single-owner transport for the dry-sine prototype. The host transfers exclusive
/// cursor ownership of the prepared source and serializes all operations (eventually
/// on the audio thread through a command queue). No locks, timers, IO or callbacks.
/// Position is measured in score frames and advances only through Read.
/// Valid operations allocate no managed memory; device deadlines remain unproven.
/// </summary>
public sealed class PreparedSineTransport
{
    private readonly PreparedSinePlayback _source;

    public TransportState State { get; private set; }
    public long PositionFrames => _source.PositionFrames;
    public long TotalFrames => _source.TotalFrames;
    public int SampleRate => _source.SampleRate;
    public int MaxBlockFrames => _source.MaxBlockFrames;
    public bool LoopEnabled { get; private set; }
    public long LoopStartFrames { get; private set; }
    public long LoopEndFrames { get; private set; }

    /// <summary>Transfers cursor ownership and starts stopped at frame zero.</summary>
    public PreparedSineTransport(PreparedSinePlayback source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _source.Reset();
    }

    /// <summary>
    /// Starts/resumes at the cursor. At EOF without a loop, stays stopped; use
    /// Stop or Seek to replay. Loop repositioning occurs on the next nonempty Read.
    /// </summary>
    public void Play() => State = PositionFrames < TotalFrames || LoopEnabled
        ? TransportState.Playing : TransportState.Stopped;

    public void Pause()
    {
        if (State == TransportState.Playing) State = TransportState.Paused;
    }

    /// <summary>Stops and rewinds to zero, preserving the configured loop.</summary>
    public void Stop()
    {
        _source.Reset();
        State = TransportState.Stopped;
    }

    /// <summary>
    /// Sets the exact cursor, preserving state. A playing seek to EOF is resolved
    /// on the next nonempty Read. Invalid seeks leave all state unchanged.
    /// </summary>
    public void Seek(long frame) => _source.Seek(frame);

    /// <summary>
    /// Enables [startFrame, endFrame), requiring 0 &lt;= start &lt; end &lt;= TotalFrames.
    /// Does not move the cursor: frames before the start play as a lead-in;
    /// positions at/past the end wrap to the start on the next playing Read.
    /// Wraps are exact seeks, with no crossfade or effect-tail handling.
    /// </summary>
    public void SetLoop(long startFrame, long endFrame)
    {
        if (startFrame < 0 || startFrame >= TotalFrames)
            throw new ArgumentOutOfRangeException(nameof(startFrame));
        if (endFrame <= startFrame || endFrame > TotalFrames)
            throw new ArgumentOutOfRangeException(nameof(endFrame));
        LoopStartFrames = startFrame;
        LoopEndFrames = endFrame;
        LoopEnabled = true;
    }

    /// <summary>Disables looping without changing cursor, state or stored range.</summary>
    public void ClearLoop() => LoopEnabled = false;

    /// <summary>
    /// Fills caller-owned interleaved stereo, returning the number of score frames
    /// rendered (including silent notes). Paused/stopped and post-EOF output is zero.
    /// Exact EOF stops with the cursor at TotalFrames. Exact loop end immediately
    /// wraps the cursor, including at block end. Empty buffers do nothing; invalid
    /// buffers fail before changing output, state or cursor, even while stopped.
    /// </summary>
    public int Read(Span<float> output)
    {
        if (output.Length % 2 != 0 || output.Length / 2 > MaxBlockFrames)
            throw new ArgumentException("Output must contain stereo frames within the prepared block limit", nameof(output));
        output.Clear();
        if (State != TransportState.Playing || output.IsEmpty) return 0;

        int written = 0;
        while (written < output.Length / 2)
        {
            if (LoopEnabled && PositionFrames >= LoopEndFrames) _source.Seek(LoopStartFrames);
            long end = LoopEnabled ? LoopEndFrames : TotalFrames;
            int count = (int)Math.Min(output.Length / 2 - written, end - PositionFrames);
            if (count > 0)
            {
                _source.Read(output.Slice(written * 2, count * 2));
                written += count;
            }
            if (LoopEnabled)
            {
                if (PositionFrames == LoopEndFrames) _source.Seek(LoopStartFrames);
            }
            else if (PositionFrames == TotalFrames)
            {
                State = TransportState.Stopped;
                break;
            }
        }
        return written;
    }
}
