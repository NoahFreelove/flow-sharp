using System.Diagnostics;
using Flow.Audio;
using Flow.Platform.Linux;
using Flow.Studio.Engine;
using Flow.Studio.Model;

namespace Flow.Studio.Host;

/// <summary>
/// Single control-owner recording into an existing track during uninterrupted playback.
/// Poll alongside the playback session. One input thread submits complete RtMidi packets.
/// Takes use receipt-clock timing, not hardware timestamps; no monitoring is supplied here.
/// </summary>
public sealed class ProjectMidiRecordingHost : IDisposable
{
    private readonly ProjectPlaybackSession _playback;
    private readonly Func<string, MidiPacketReceiver, IMidiInputConnection> _open;
    private readonly Func<long> _now;
    private readonly long _frequency;
    private IMidiInputConnection? _input;
    private MidiRecordingSession? _recording;
    private ProjectSnapshot? _snapshot;
    private Flow.Music.Model.MidiPitchMap? _pitchMap;
    private long _originTicks, _controlVersion, _generation;
    private Guid _track;
    private Exception? _packetError;
    private bool _disposed, _arming, _armFailed, _stopRequested;
    private long _recordingToken, _stopTicks, _armedAt, _lastClockTimestamp;
    private int _inputLatency;
    private long _countInFrames, _armDeadlineFrames;
    public long CountInRemainingFrames => !_arming ? 0 :
        _playback.Playback.Queue.TryReadClock(out var clock) && clock.RecordingToken == _recordingToken
            ? clock.CountInRemainingFrames : _countInFrames;
    private MidiInputQueue? _rawInput;

    private void ReleaseExtension()
    {
        if (_recordingToken == 0) return;
        _playback.Playback.Queue.RequestEndRecording(_recordingToken);
        _recordingToken = 0;
    }

    private void Fault(string reason)
    {
        if (_recording is null)
        {
            _arming = false; _armFailed = true;
            Interlocked.CompareExchange(ref _packetError, new InvalidOperationException(reason), null);
        }
        else _recording.ReportDiscontinuity(reason);
    }

    public MidiRecordingState? State => _arming ? MidiRecordingState.Arming : _armFailed ? MidiRecordingState.Faulted :
        _stopRequested && _recording?.State == MidiRecordingState.Recording ? MidiRecordingState.Stopping : _recording?.State;
    public Exception? Error => _recording?.Error ?? Volatile.Read(ref _packetError);
    public MidiRecordingTake? Take => _recording?.Take;
    public bool DeviceOwned => _input is not null;

    public ProjectMidiRecordingHost(ProjectPlaybackSession playback)
        : this(playback, (playback ?? throw new ArgumentNullException(nameof(playback))).MidiInputHub.Subscribe, Stopwatch.GetTimestamp, Stopwatch.Frequency) { }

    internal ProjectMidiRecordingHost(ProjectPlaybackSession playback,
        Func<string, MidiPacketReceiver, IMidiInputConnection> open, Func<long> now, long frequency)
    {
        ArgumentNullException.ThrowIfNull(playback);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(now);
        if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency));
        _playback = playback; _open = open; _now = now; _frequency = frequency;
    }

    /// <summary>Starts asynchronous arming; capture begins at the acknowledged audio boundary.</summary>
    public void Start(string portName, Guid trackId, int inputLatencyFrames = 0) => Arm(portName, trackId, inputLatencyFrames);

    /// <summary>Start from stopped/paused/playing, including an empty project. Input
    /// is buffered until the audio boundary acknowledges the recording clock.
    /// Packets received before that boundary are outside the take and discarded.</summary>
    public void Arm(string portName, Guid trackId, int inputLatencyFrames = 0)
        => ArmCore(portName, trackId, inputLatencyFrames, 0);

    /// <summary>Count one to eight bars at the cursor's tempo/meter before capture.
    /// The count-in is monitoring only; it is not written into the project or take.</summary>
    public void ArmCounted(string portName, Guid trackId, int bars = 1, int inputLatencyFrames = 0)
    {
        if (bars is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(bars));
        ArmCore(portName, trackId, inputLatencyFrames, bars);
    }

    private void ArmCore(string portName, Guid trackId, int inputLatencyFrames, int countInBars)
    {
        ThrowIfDisposed();
        if (_input is not null || State is MidiRecordingState.Recording or MidiRecordingState.Stopping or MidiRecordingState.Arming)
            throw new InvalidOperationException("Previous MIDI input must be stopped first.");
        var queue = _playback.Playback.Queue;
        if (_playback.OutputState != PlaybackOutputState.Running || queue.CommandsPending ||
            !queue.TryReadClock(out var clock) || clock.LoopEnabled || _playback.IsPreparing ||
            !ReferenceEquals(_playback.Document.Snapshot, _playback.Playback.ActiveSnapshot))
            throw new InvalidOperationException("Recording requires acknowledged, non-looping output with no pending commands or edits.");
        var snapshot = _playback.Document.Snapshot;
        if (!snapshot.Routing.Tracks.Any(t => t.Id == trackId)) throw new ArgumentException("Unknown recording track.", nameof(trackId));
        if (inputLatencyFrames < 0 || inputLatencyFrames > queue.SampleRate * 10L)
            throw new ArgumentOutOfRangeException(nameof(inputLatencyFrames));
        _pitchMap = FlowLang.Hosting.GeneratorTuning.ResolveMidi(snapshot.Context.Tuning);
        IPreparedAudioPlayback? lead = null;
        if (countInBars != 0)
        {
            var schedule = MetronomeSchedule.CountIn(snapshot.Context.Tempo, snapshot.Context.Meter,
                snapshot.Context.Tempo.QuarterAt((double)clock.PositionFrames / queue.SampleRate), countInBars, queue.SampleRate);
            lead = MetronomePlayback.Prepare(schedule.Beats, schedule.Frames, queue.SampleRate, queue.MaxBlockFrames);
        }
        _countInFrames = lead?.TotalFrames ?? 0;
        _armDeadlineFrames = checked(_countInFrames + queue.SampleRate * 2L);
        _snapshot = snapshot; _track = trackId; _inputLatency = inputLatencyFrames;
        _generation = clock.Generation; _armedAt = _now(); _lastClockTimestamp = clock.Timestamp;
        _packetError = null; _recording = null; _rawInput = new();
        _arming = true; _armFailed = _stopRequested = false;
        try
        {
            _input = _open(portName, Receive);
            if (!queue.TryBeginRecording(lead, out _recordingToken)) throw new InvalidOperationException("Recording playback command was rejected.");
            _controlVersion = queue.ControlVersion;
        }
        catch (Exception error)
        {
            Fault("MIDI recording could not be armed: " + error.Message);
            CloseInput(TimeSpan.Zero); ReleaseExtension(); throw;
        }
    }

    private void TryActivate()
    {
        if (!_arming) return;
        var queue = _playback.Playback.Queue;
        if (!queue.TryReadClock(out var clock)) return;
        if (!clock.RecordingEnabled || clock.RecordingToken != _recordingToken)
        {
            if (FramesSince(_armedAt, _now()) > _armDeadlineFrames) Fault("Recording playback was not acknowledged.");
            return;
        }
        _originTicks = clock.RecordingStartTimestamp;
        _recording = new(_snapshot!, 0, clock.RecordingStartFrame, queue.SampleRate, _inputLatency, midiPitchMap: _pitchMap);
        _arming = false;
    }

    private long FramesSince(long origin, long ticks)
    {
        if (ticks < origin) throw new InvalidOperationException("MIDI clock moved backwards.");
        // Int128 avoids overflow and loss of integer precision on long-running hosts.
        Int128 frames = ((Int128)ticks - origin) * _playback.Playback.Queue.SampleRate / _frequency;
        return checked((long)frames);
    }

    private void Receive(ReadOnlySpan<byte> packet, long ticks)
    {
        try
        {
            if (!MidiChannelPacket.TryDecode(packet, out var message)) return;
            if (!_rawInput!.TryWrite(new(ticks, message.Status, message.Data1, message.Data2)))
                throw new InvalidOperationException("MIDI input queue overflow; take is incomplete.");
        }
        catch (Exception error) { Interlocked.CompareExchange(ref _packetError, error, null); }
    }

    private void CheckHealth()
    {
        if (_stopRequested || State is not (MidiRecordingState.Recording or MidiRecordingState.Stopping or MidiRecordingState.Arming)) return;
        string? failure = null;
        var queue = _playback.Playback.Queue;
        if (_input?.Error is { } inputError) failure = "MIDI input failed: " + inputError.Message;
        else if (Volatile.Read(ref _packetError) is { } packetError) failure = packetError.Message;
        else if (_input is not null && !_input.IsRunning) failure = "MIDI input stopped unexpectedly.";
        else if (_playback.OutputState != PlaybackOutputState.Running) failure = "Audio output is unavailable.";
        else if (!ReferenceEquals(_snapshot, _playback.Document.Snapshot)) failure = "Project changed during recording.";
        else if (queue.ControlVersion != _controlVersion || queue.Generation != _generation)
            failure = "Transport or playback source changed during recording.";
        else if (queue.TryReadClock(out var clock))
        {
            _lastClockTimestamp = clock.Timestamp;
            if ((!_arming && clock.State != TransportState.Playing) || clock.LoopEnabled) failure = "Playback is no longer uninterrupted.";
            else if (FramesSince(clock.Timestamp, _now()) > queue.SampleRate * 2L) failure = "Playback clock is stale.";
        }
        else if (FramesSince(_lastClockTimestamp, _now()) > queue.SampleRate * 2L)
            failure = "Playback clock could not be observed before its deadline.";
        if (failure is not null) Fault(failure);
    }

    public void Poll(int maxEvents = 4096)
    {
        ThrowIfDisposed();
        if (maxEvents is < 1 or > 65536) throw new ArgumentOutOfRangeException(nameof(maxEvents));
        CheckHealth(); TryActivate();
        // Discard only messages proven older than a still-counting audio clock.
        // Newer packets survive until the exact start is known, even if the audio
        // thread finishes count-in concurrently with this control tick.
        if (_arming && _rawInput is { } early && _playback.Playback.Queue.TryReadClock(out var counting) &&
            counting.RecordingToken == _recordingToken && counting.CountInRemainingFrames > 0)
        {
            int count = 0;
            while (count++ < maxEvents && early.TryPeek(out var message) && message.Frame <= counting.Timestamp)
                early.TryRead(out _);
        }
        bool drained = true;
        if (_rawInput is { } raw && !_arming && !_armFailed && _recording is not null &&
            _recording.State == MidiRecordingState.Recording)
        {
            int count = 0, budget = Math.Min(maxEvents, 4096);
            while (count < budget && raw.TryRead(out var message))
            {
                count++;
                if (message.Frame < _originTicks) continue;
                _recording.TryCapture(message with { Frame = FramesSince(_originTicks, message.Frame) });
            }
            drained = count < budget;
        }
        if (_stopRequested && drained && _recording?.State == MidiRecordingState.Recording)
        {
            long end = _stopTicks <= _originTicks ? 0 : FramesSince(_originTicks, _stopTicks);
            if (end == 0) _recording.Cancel();
            else _recording.RequestStop(end);
        }
        _recording?.Poll(maxEvents);
        if (State == MidiRecordingState.Faulted) { CloseInput(TimeSpan.Zero); ReleaseExtension(); }
    }

    /// <summary>Join input before completing the take. A timeout faults the take and
    /// retains the device; retry Stop or Dispose. Completion drains on later Poll ticks.</summary>
    public bool Stop(TimeSpan timeout)
    {
        ThrowIfDisposed();
        CheckHealth(); TryActivate();
        if (!CloseInput(timeout))
        {
            Fault("MIDI input did not stop before its deadline.");
            _recording?.Poll(); ReleaseExtension(); return false;
        }
        CheckHealth(); TryActivate(); // A final audio boundary may have completed while input joined.
        if (_arming) { _arming = false; _armFailed = false; _recording = null; }
        if (_recording?.State == MidiRecordingState.Recording && !_stopRequested)
        { _stopTicks = _now(); _stopRequested = true; }
        ReleaseExtension();
        Poll(); return true;
    }

    private bool CloseInput(TimeSpan timeout)
    {
        if (_input is null) return true;
        if (!_input.TryStop(timeout)) return false;
        // Error may be published by the last in-flight read during the join.
        if (_input.Error is { } error) Fault("MIDI input failed: " + error.Message);
        if (Volatile.Read(ref _packetError) is { } packetError) Fault(packetError.Message);
        _input = null;
        return true;
    }

    public bool Commit(string name = "Recorded notes")
    {
        ThrowIfDisposed();
        if (State != MidiRecordingState.Completed || Take is null) throw new InvalidOperationException("No completed MIDI take.");
        if (!Take.Commit(_playback.Document, _track, name)) return false;
        _playback.RequestPreparation(); return true;
    }

    public void Cancel()
    {
        ThrowIfDisposed();
        if (_recording is not null && _recording.State != MidiRecordingState.Completed) _recording.Cancel();
        _arming = false; _armFailed = false; _stopRequested = false;
        CloseInput(TimeSpan.Zero); ReleaseExtension();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Cancel();
        if (!CloseInput(TimeSpan.FromSeconds(2))) throw new TimeoutException("MIDI input still owns its reader; retry disposal.");
        _disposed = true;
    }
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
