using System.Runtime.InteropServices;
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
    /// Used by background FlowEngine instances during live reload AND by test runs
    /// (auto-enabled when FLOW_SUPPRESS_PLAYBACK=1 — set by flow-lang.Tests'
    /// ModuleInitializer so tests never push audio through PulseAudio).
    /// </summary>
    public bool CaptureMode { get; set; }
        = Environment.GetEnvironmentVariable("FLOW_SUPPRESS_PLAYBACK") == "1";

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
#if !FLOW_WEB
            // On macOS, prefer CoreAudio (AudioToolbox.framework is always present on
            // a standard install). Fall through to PulseAudio for the rare case where
            // a composer runs PulseAudio under Homebrew on a Mac.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return CoreAudioBackend.IsAvailable() || PulseAudioSimpleBackend.IsAvailable();

            // On Linux (and other non-macOS platforms), PulseAudio Simple covers both
            // native PulseAudio and PipeWire's compatibility layer.
            return PulseAudioSimpleBackend.IsAvailable();
#else
            // Phase 47 D-47-08: PulseAudio + CoreAudio backends stripped from
            // Web build. The WebAudioBackend stub is unavailable for playback
            // until Phase 48 — IsAudioAvailable returns false so feature
            // detection is honest about the gap.
            return WebAudioBackend.IsAvailable();
#endif
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
        // Phase 47 D-47-06: Web target probe FIRST. OperatingSystem.IsBrowser()
        // is a JIT intrinsic — constant-false on every Desktop platform, so the
        // Mono-WASM linker dead-code-eliminates the WebAudioBackend instantiation
        // on trim-mode Desktop builds (per D-47-07). On Mono-WASM the same
        // intrinsic returns true and this branch wins before any P/Invoke probe
        // would have run. Phase 47 ships the stub; Phase 48 fills the [JSImport]
        // bodies — until then a Web build that calls Play() will throw
        // PlatformNotSupportedException with a clear stub message.
        if (WebAudioBackend.IsAvailable())
            return new WebAudioBackend();

#if !FLOW_WEB
        // macOS: prefer CoreAudio via AudioToolbox.framework. AudioToolbox is a
        // system framework so this should always succeed on a standard install.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (CoreAudioBackend.IsAvailable())
                return new CoreAudioBackend();
            // Fall through to PulseAudio probe — covers the (rare) macOS user
            // running PulseAudio under Homebrew.
        }

        // Try PulseAudio Simple API — this also works on PipeWire systems since
        // PipeWire provides a PulseAudio compatibility layer.
        if (PulseAudioSimpleBackend.IsAvailable())
            return new PulseAudioSimpleBackend();
#endif

        throw new PlatformNotSupportedException(
            "No audio output available. On Linux, install PipeWire or PulseAudio. " +
            "On macOS, CoreAudio (AudioToolbox.framework) should be present by default.");
    }

    public override string ToString() =>
        _backend != null ? $"AudioPlaybackManager[{_backend.Name}]" : "AudioPlaybackManager[no backend]";
}
