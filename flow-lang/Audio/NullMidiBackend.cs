namespace FlowLang.Audio;

/// <summary>
/// Phase 40 MIDI-RT-01/04 — silent no-op <see cref="IMidiBackend"/>. The
/// charitable fallback returned by <c>MidiPlaybackManager.DetectBackend</c>
/// when no real-time MIDI backend is available (e.g. <c>librtmidi.so</c> is
/// absent on this dev box). A live session NEVER dies on a missing native lib:
/// every operation degrades to a quiet no-op.
///
/// <para>Pure managed — NOT Web-stripped. It compiles cleanly on the Web target
/// (no RtMidi.Core reference) so <c>MidiPlaybackManager</c> can fall back to it
/// uniformly. Models the <c>WebAudioBackend</c> stub branch shape.</para>
/// </summary>
public sealed class NullMidiBackend : IMidiBackend
{
    /// <inheritdoc/>
    public string Name => "Null";

    /// <inheritdoc/>
    public bool IsInitialized => true;

    /// <inheritdoc/>
    /// <remarks>Always empty — a Null backend exposes no ports.</remarks>
    public IReadOnlyList<string> ListPorts() => Array.Empty<string>();

    /// <inheritdoc/>
    /// <remarks>Always null — there is nothing to open. The composer's
    /// <c>openMidiOutput</c> surfaces this as a charitable dead handle.</remarks>
    public IMidiOutputHandle? OpenOutput(string port) => null;

    /// <inheritdoc/>
    /// <remarks>Never raised — a Null backend's port set never changes.</remarks>
    public event Action<IReadOnlyList<string>>? PortChanged
    {
        add { /* no-op */ }
        remove { /* no-op */ }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing to release.
    }
}
