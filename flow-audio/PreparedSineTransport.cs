namespace Flow.Audio;

public enum TransportState { Stopped, Playing, Paused }

/// <summary>
/// Single-owner transport for prepared stereo sources (historical public class name). The host transfers exclusive
/// cursor ownership of the prepared source and serializes all operations (eventually
/// on the audio thread through a command queue). No locks, timers, IO or callbacks.
/// Position is measured in score frames and advances only through Read.
/// Valid operations allocate no managed memory; device deadlines remain unproven.
/// </summary>
public sealed class PreparedSineTransport
{
    private readonly IPreparedAudioPlayback _source;
    private long _extraFrames;
    private IPreparedAudioPlayback? _countIn;
    private readonly float[] _monitorScratch;
    public long CountInRemainingFrames => _countIn is { } lead ? lead.TotalFrames - lead.PositionFrames : 0;
    /// <summary>Offset within the last nonempty Read where a counted recording
    /// started, including an offset equal to the block length.</summary>
    public int? RecordingStartOffset { get; private set; }
    public bool RecordingEnabled { get; private set; }

    public TransportState State { get; private set; }
    public long PositionFrames => _source.PositionFrames + _extraFrames;
    public long TotalFrames => _source.TotalFrames;
    public int SampleRate => _source.SampleRate;
    public int MaxBlockFrames => _source.MaxBlockFrames;
    public bool LoopEnabled { get; private set; }
    public long LoopStartFrames { get; private set; }
    public long LoopEndFrames { get; private set; }

    /// <summary>Transfers cursor ownership and starts stopped at frame zero.</summary>
    public PreparedSineTransport(IPreparedAudioPlayback source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _monitorScratch = new float[source.MaxBlockFrames * 2];
        _source.Reset();
    }

    /// <summary>
    /// Starts/resumes at the cursor. At EOF without looping or recording, stays stopped; use
    /// Stop or Seek to replay. Loop repositioning occurs on the next nonempty Read.
    /// </summary>
    public void Play() => State = PositionFrames < TotalFrames || LoopEnabled || RecordingEnabled || _countIn is not null
        ? TransportState.Playing : TransportState.Stopped;

    public void Pause()
    {
        _countIn = null;
        if (State == TransportState.Playing) State = TransportState.Paused;
    }

    /// <summary>Stops and rewinds to zero, preserving the configured loop.</summary>
    public void Stop()
    {
        _source.Reset();
        _extraFrames = 0; RecordingEnabled = false;
        _countIn = null; RecordingStartOffset = null;
        State = TransportState.Stopped;
    }

    /// <summary>
    /// Sets the exact cursor, preserving state. A playing seek to EOF is resolved
    /// on the next nonempty Read. Invalid seeks leave all state unchanged.
    /// </summary>
    public void Seek(long frame)
    {
        _source.Seek(frame); // Validate before changing the extended cursor.
        if (_countIn is not null) { _countIn = null; State = TransportState.Stopped; }
        _extraFrames = 0;
    }

    /// <summary>Continue the transport clock with silence beyond the prepared source.
    /// This changes no project data. Looping is disabled for a straight-through take.</summary>
    public void BeginRecording()
    {
        _countIn = null;
        LoopEnabled = false; RecordingEnabled = true; State = TransportState.Playing;
    }

    /// <summary>Transfers a fresh matching-format lead-in cursor. Project position
    /// stays frozen until it ends; live monitoring remains audible. Pause/seek/stop
    /// cancel the lead-in rather than implicitly starting a take afterwards.</summary>
    public void BeginRecording(IPreparedAudioPlayback countIn)
    {
        ArgumentNullException.ThrowIfNull(countIn);
        if (ReferenceEquals(countIn, _source) || countIn.SampleRate != SampleRate || countIn.MaxBlockFrames != MaxBlockFrames ||
            countIn.PositionFrames != 0 || countIn.TotalFrames <= 0)
            throw new ArgumentException("Count-in requires a fresh, positive, matching-format cursor");
        _countIn = countIn; RecordingEnabled = false; LoopEnabled = false;
        RecordingStartOffset = null; State = TransportState.Playing;
    }

    /// <summary>Return to the finite source. At/beyond EOF clamp and stop; otherwise
    /// retain playback. Stop always clears this mode too.</summary>
    public void EndRecording()
    {
        if (_countIn is not null) { _countIn = null; State = TransportState.Stopped; }
        RecordingEnabled = false; _extraFrames = 0;
        if (PositionFrames >= TotalFrames) State = TransportState.Stopped;
    }

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
        if (RecordingEnabled || _countIn is not null) EndRecording();
        LoopStartFrames = startFrame;
        LoopEndFrames = endFrame;
        LoopEnabled = true;
    }

    /// <summary>Audio-owner replacement handoff. Preserve absolute frame position,
    /// clamp shortened projects and loop ends; disable a loop whose start no longer
    /// exists. Seek resets DSP/held-note state rather than migrating it.</summary>
    internal void InheritTransport(PreparedSineTransport previous)
    {
        Seek(Math.Min(previous.PositionFrames, TotalFrames));
        if (previous.LoopEnabled && previous.LoopStartFrames < TotalFrames)
            SetLoop(previous.LoopStartFrames, Math.Min(previous.LoopEndFrames, TotalFrames));
        State = previous.CountInRemainingFrames > 0 ? TransportState.Stopped : previous.State;
        if (State == TransportState.Playing && PositionFrames == TotalFrames && !LoopEnabled)
            State = TransportState.Stopped;
    }

    /// <summary>Disables looping without changing cursor, state or stored range.</summary>
    public void ClearLoop() => LoopEnabled = false;

    private void ReadMonitoring(Span<float> output, bool advanceTimeline)
    {
        if (_source is IPreparedMonitoringPlayback { MonitoringEnabled: true } monitoring)
            monitoring.ReadMonitoring(output, PositionFrames, advanceTimeline);
    }

    /// <summary>
    /// Fills caller-owned interleaved stereo, returning the number of score frames
    /// rendered (including silent notes). Paused/stopped and post-EOF output contains only explicitly attached monitoring;
    /// without monitoring it is zero. The return count tracks arrangement/recording frames.
    /// Outside recording, exact EOF stops with the cursor at TotalFrames. Recording
    /// advances through zero-filled output beyond EOF without changing source length. Exact loop end immediately
    /// wraps the cursor, including at block end. Empty buffers do nothing; invalid
    /// buffers fail before changing output, state or cursor, even while stopped.
    /// </summary>
    public int Read(Span<float> output)
    {
        if (output.Length % 2 != 0 || output.Length / 2 > MaxBlockFrames)
            throw new ArgumentException("Output must contain stereo frames within the prepared block limit", nameof(output));
        output.Clear();
        if (output.IsEmpty) return 0;
        RecordingStartOffset = null;
        if (State != TransportState.Playing)
        { ReadMonitoring(output, false); return 0; }

        int written = 0, leadFrames = 0;
        if (_countIn is { } lead)
        {
            leadFrames = (int)Math.Min(output.Length / 2, lead.TotalFrames - lead.PositionFrames);
            if (lead.Read(output[..(leadFrames * 2)]) != leadFrames)
                throw new InvalidOperationException("Count-in returned an incomplete block");
            var monitor = _monitorScratch.AsSpan(0, leadFrames * 2); monitor.Clear();
            ReadMonitoring(monitor, false);
            for (int i = 0; i < monitor.Length; i++) output[i] += monitor[i];
            written = leadFrames;
            if (lead.PositionFrames == lead.TotalFrames)
            {
                BeginRecording(); RecordingStartOffset = leadFrames;
            }
        }
        while (written < output.Length / 2)
        {
            if (RecordingEnabled && PositionFrames >= TotalFrames)
            {
                int remaining = output.Length / 2 - written;
                if (PositionFrames > long.MaxValue - remaining)
                { EndRecording(); break; }
                ReadMonitoring(output.Slice(written * 2), true);
                _extraFrames += remaining;
                written += remaining;
                break;
            }
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
            else if (PositionFrames == TotalFrames && !RecordingEnabled)
            {
                State = TransportState.Stopped;
                if (written < output.Length / 2) ReadMonitoring(output.Slice(written * 2), false);
                break;
            }
        }
        return written - leadFrames;
    }
}
