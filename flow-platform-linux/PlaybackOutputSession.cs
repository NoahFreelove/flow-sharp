using Flow.Audio;

namespace Flow.Platform.Linux;

public enum PlaybackOutputState { Offline, Running, Faulted, Disposed }

/// <summary>
/// A stream must join all callbacks before Dispose returns successfully. Failed
/// disposal retains ownership and must be retryable; opening a second stream is unsafe.
/// </summary>
public interface IPlaybackOutputStream : IDisposable
{
    void Start();
    bool IsActive { get; }
    bool CallbackFaulted { get; }
}

/// <summary>
/// Control-thread owner of one output stream. Serialize calls with the playback
/// queue's producer. Poll from the host update loop; no timers or automatic retries.
/// Reconnect requires explicit Connect and never resumes music automatically.
/// Stream activity reports backend health, not physical device presence behind a mixer.
/// </summary>
public sealed class PlaybackOutputSession : IDisposable
{
    private readonly QueuedSinePlayback _playback;
    private readonly Func<CallbackRenderProbe, IPlaybackOutputStream> _open;
    private readonly float[] _silence;
    private IPlaybackOutputStream? _stream;

    public PlaybackOutputState State { get; private set; }
    public Exception? LastError { get; private set; }

    public PlaybackOutputSession(QueuedSinePlayback playback, string? deviceSelector = null)
        : this(playback, probe => new PortAudioOutput(probe, deviceSelector: deviceSelector)) { }

    internal PlaybackOutputSession(QueuedSinePlayback playback,
        Func<CallbackRenderProbe, IPlaybackOutputStream> open)
    {
        ArgumentNullException.ThrowIfNull(playback);
        ArgumentNullException.ThrowIfNull(open);
        _playback = playback;
        _open = open;
        _silence = new float[playback.MaxBlockFrames * 2];
    }

    public bool Connect()
    {
        ThrowIfDisposed();
        if (State == PlaybackOutputState.Running) return true;
        // Retained after a failed close: never overlap consumers of the queue.
        if (!CloseStream()) return false;
        ResetPlayback();
        try
        {
            // Fresh fault latch on each connection; bounded diagnostic storage.
            _stream = _open(new CallbackRenderProbe(_playback, sampleCapacity: 1, mute: false));
            _stream.Start();
            if (_stream.CallbackFaulted || !_stream.IsActive)
                throw new InvalidOperationException("Audio output did not become active");
            State = PlaybackOutputState.Running;
            LastError = null;
            return true;
        }
        catch (Exception error)
        {
            Fail(error);
            return false;
        }
    }

    public PlaybackOutputState Poll()
    {
        ThrowIfDisposed();
        if (State != PlaybackOutputState.Running)
        {
            // Offline edits still need a consumer boundary. A failed close can
            // retain a live callback, so state alone does not grant ownership.
            if (_stream is null) ResetPlayback();
            return State;
        }
        try
        {
            if (_stream!.CallbackFaulted)
                throw new InvalidOperationException("Audio callback failed");
            if (!_stream.IsActive)
                throw new InvalidOperationException("Audio output stopped unexpectedly");
        }
        catch (Exception error) { Fail(error); }
        return State;
    }

    public bool Disconnect()
    {
        ThrowIfDisposed();
        if (!CloseStream()) return false;
        ResetPlayback();
        State = PlaybackOutputState.Offline;
        LastError = null;
        return true;
    }

    public void Dispose()
    {
        if (State == PlaybackOutputState.Disposed) return;
        if (!Disconnect()) throw new InvalidOperationException("Audio output could not be closed; ownership retained", LastError);
        State = PlaybackOutputState.Disposed;
    }

    private void Fail(Exception error)
    {
        LastError = error;
        State = PlaybackOutputState.Faulted;
        _playback.RequestStop();
        if (CloseStream()) ResetPlayback();
    }

    private bool CloseStream()
    {
        try
        {
            _stream?.Dispose();
            _stream = null;
            return true;
        }
        catch (Exception error)
        {
            LastError = error;
            State = PlaybackOutputState.Faulted;
            _playback.RequestStop();
            return false;
        }
    }

    private void ResetPlayback()
    {
        // No native consumer exists after a successful close. Temporarily take
        // consumer ownership to acknowledge stop and any pending publication.
        _playback.RequestStop();
        _playback.Read(_silence);
        if (_playback.ReplacementPending) _playback.Read(_silence);
        _playback.TryTakeRetired(out _);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(State == PlaybackOutputState.Disposed, this);
}
