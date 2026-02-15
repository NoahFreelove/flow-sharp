namespace FlowLang.Audio;

/// <summary>
/// Manages audio backend lifecycle. Auto-detects the best available backend
/// and provides a singleton access point for playback operations.
/// Thread-safe: multiple callers can request the backend concurrently.
/// </summary>
public sealed class AudioPlaybackManager : IDisposable
{
    private IAudioBackend? _backend;
    private CancellationTokenSource? _playbackCts;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Gets the active audio backend, auto-detecting if needed.
    /// Throws <see cref="PlatformNotSupportedException"/> if no backend is available.
    /// </summary>
    public IAudioBackend GetBackend()
    {
        lock (_lock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioPlaybackManager));

            if (_backend != null)
                return _backend;

            _backend = DetectBackend();
            return _backend;
        }
    }

    /// <summary>
    /// Whether any audio backend is available on this system.
    /// Does not throw — safe to call for feature detection.
    /// </summary>
    public bool IsAudioAvailable()
    {
        try
        {
            // Check PulseAudio (covers PipeWire compatibility too)
            return PulseAudioSimpleBackend.IsAvailable();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a new <see cref="CancellationTokenSource"/> for the current playback.
    /// Cancels any previous playback first.
    /// </summary>
    public CancellationToken StartPlayback()
    {
        lock (_lock)
        {
            // Cancel any ongoing playback
            _playbackCts?.Cancel();
            _playbackCts?.Dispose();
            _playbackCts = new CancellationTokenSource();
            return _playbackCts.Token;
        }
    }

    /// <summary>
    /// Cancels any currently running playback.
    /// </summary>
    public void StopPlayback()
    {
        lock (_lock)
        {
            _playbackCts?.Cancel();

            if (_backend != null)
            {
                try { _backend.Stop(); }
                catch { /* best effort */ }
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            StopPlayback();
            _playbackCts?.Dispose();
            _backend?.Dispose();
            _backend = null;
        }
    }

    private static IAudioBackend DetectBackend()
    {
        // Try PulseAudio Simple API first — this also works on PipeWire systems
        // since PipeWire provides a PulseAudio compatibility layer.
        if (PulseAudioSimpleBackend.IsAvailable())
            return new PulseAudioSimpleBackend();

        throw new PlatformNotSupportedException(
            "No audio output available. Install PipeWire or PulseAudio.");
    }

    public override string ToString() =>
        _backend != null ? $"AudioPlaybackManager[{_backend.Name}]" : "AudioPlaybackManager[no backend]";
}
