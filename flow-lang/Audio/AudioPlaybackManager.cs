using FlowLang.StandardLibrary.Audio;

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
    private AudioBuffer? _capturedBuffer;

    /// <summary>
    /// When true, play()/loop() store the buffer instead of playing through PulseAudio.
    /// Used by background FlowEngine instances during live reload.
    /// </summary>
    public bool CaptureMode { get; set; }

    /// <summary>
    /// Maximum number of simultaneous voices allowed. Default is 32.
    /// Can be changed at runtime via the setMaxVoices() built-in function.
    /// </summary>
    public int MaxVoices { get; set; } = 32;

    /// <summary>
    /// Retrieves the buffer captured during CaptureMode execution. Returns null if none captured.
    /// Clears the captured buffer after retrieval.
    /// </summary>
    public AudioBuffer? GetCapturedBuffer()
    {
        var buf = _capturedBuffer;
        _capturedBuffer = null;
        return buf;
    }

    /// <summary>
    /// Stores a buffer for capture mode (called by PlaybackFunctions when CaptureMode is true).
    /// </summary>
    public void SetCapturedBuffer(AudioBuffer buffer) => _capturedBuffer = buffer;

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
