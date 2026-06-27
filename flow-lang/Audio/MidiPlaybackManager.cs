namespace FlowLang.Audio;

/// <summary>
/// Phase 40 MIDI-RT-01/02/04 — manages the real-time MIDI backend lifecycle,
/// the sibling of <see cref="AudioPlaybackManager"/>. Lock + lazy
/// auto-detect; thread-safe; closes the backend on <see cref="Dispose"/>.
///
/// <para><b>The one deviation from the audio manager (40-RESEARCH:118 / Open Q2):</b>
/// <see cref="DetectBackend"/> NEVER throws. Where
/// <c>AudioPlaybackManager.DetectBackend</c> ends in
/// <c>throw new PlatformNotSupportedException</c>, this manager falls back to
/// <see cref="NullMidiBackend"/> so a live session never dies on a missing
/// <c>librtmidi.so</c> (charitable rule).</para>
///
/// <para>NOT Web-stripped: <see cref="NullMidiBackend"/> + <see cref="IMidiBackend"/>
/// compile cleanly on Web. The only native-MIDI reference (the librtmidi-backed
/// <see cref="RtMidiMidiBackend"/>) is inside the <c>#if !FLOW_WEB</c> block of
/// <see cref="DetectBackend"/> / <see cref="IsMidiAvailable"/>.</para>
/// </summary>
public sealed class MidiPlaybackManager : IDisposable
{
    private IMidiBackend? _backend;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Gets the active MIDI backend, auto-detecting if needed. NEVER throws on a
    /// missing native lib — returns <see cref="NullMidiBackend"/> in that case.
    /// </summary>
    public IMidiBackend GetBackend()
    {
        lock (_lock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MidiPlaybackManager));

            if (_backend != null)
                return _backend;

            _backend = DetectBackend();
            return _backend;
        }
    }

    /// <summary>
    /// Whether a real-time MIDI backend is available on this system. Does not
    /// throw — safe for feature detection. Mirrors
    /// <c>AudioPlaybackManager.IsAudioAvailable</c> (lines 73-99).
    /// </summary>
    public bool IsMidiAvailable()
    {
        try
        {
#if !FLOW_WEB
            return RtMidiMidiBackend.IsAvailable();
#else
            // Web target: no real-time MIDI backend (librtmidi backend stripped).
            return false;
#endif
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Auto-detect the best available MIDI backend. Unlike the audio manager,
    /// the fallthrough is a charitable <see cref="NullMidiBackend"/>, NOT a thrown
    /// <see cref="PlatformNotSupportedException"/> (40-RESEARCH:118).
    /// </summary>
    private static IMidiBackend DetectBackend()
    {
#if !FLOW_WEB
        try
        {
            if (RtMidiMidiBackend.IsAvailable())
                return new RtMidiMidiBackend();
        }
        catch
        {
            // Probe itself failed — fall through to Null.
        }
#endif
        // Charitable fallback: a live session never dies on missing librtmidi.so.
        return new NullMidiBackend();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _backend?.Dispose(); } catch { /* best-effort */ }
        _backend = null;
    }

    public override string ToString() =>
        _backend != null ? $"MidiPlaybackManager[{_backend.Name}]" : "MidiPlaybackManager[no backend]";
}
