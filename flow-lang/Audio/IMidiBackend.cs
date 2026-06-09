namespace FlowLang.Audio;

/// <summary>
/// Phase 40 MIDI-RT-01 — abstraction for real-time MIDI output backends,
/// the MIDI sibling of <see cref="IAudioBackend"/>. Implementations route
/// note / control-change / program-change / sysex / raw-byte events to a
/// hardware synth or DAW MIDI track.
///
/// <para>Two implementations ship (Plan 40-01; backend rewritten in Plan 40-04):</para>
/// <list type="bullet">
///   <item><c>RtMidiMidiBackend</c> — direct librtmidi P/Invoke over ALSA-seq
///         (Desktop, MIDI-RT-02). <c>#if !FLOW_WEB</c> guarded + Compile-Removed on Web.</item>
///   <item><c>NullMidiBackend</c> — silent no-op fallback so a live session never
///         dies when <c>librtmidi.so</c> is absent (charitable rule).</item>
/// </list>
///
/// <para><b>Charitable contract:</b> NO method on this interface (or
/// <see cref="IMidiOutputHandle"/>) may throw on a missing device / port /
/// native lib. <see cref="ListPorts"/> returns an empty list (never null);
/// <see cref="OpenOutput"/> returns <c>null</c> on failure; sends are no-ops.
/// Mirrors <see cref="IAudioBackend.GetDevices"/> ("May be empty").</para>
/// </summary>
public interface IMidiBackend : IDisposable
{
    /// <summary>
    /// Human-readable name of this backend (e.g., "RtMidi", "Null").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this backend is initialized and ready. A <c>NullMidiBackend</c>
    /// reports <c>true</c> (it is always "ready" to no-op) so callers don't
    /// special-case it.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// List available MIDI output ports.
    /// </summary>
    /// <returns>Port names. May be empty if enumeration is unsupported or no
    /// device is connected — NEVER null, NEVER throws (MIDI-RT-01 charitable).</returns>
    IReadOnlyList<string> ListPorts();

    /// <summary>
    /// Open a MIDI output port for sending.
    /// </summary>
    /// <param name="port">Matched against <see cref="ListPorts"/>: an exact
    /// case-insensitive name match is preferred, falling back to a case-insensitive
    /// substring match. An empty/whitespace port matches NO device (WR-07) and
    /// yields a dead handle rather than binding an arbitrary first device.</param>
    /// <returns>An <see cref="IMidiOutputHandle"/> on success, or <c>null</c> when
    /// the port is absent / empty / the native lib is missing (charitable failure —
    /// NEVER throws, T-40-04). The caller treats null as a dead handle.</returns>
    IMidiOutputHandle? OpenOutput(string port);

    /// <summary>
    /// Raised when the set of available ports changes. librtmidi exposes NO native
    /// hot-plug event, so any implementation that supports this is poll-based
    /// (40-RESEARCH Pattern 1 / A7). Optional — a backend may never raise it.
    /// </summary>
    event Action<IReadOnlyList<string>>? PortChanged;
}

/// <summary>
/// Phase 40 MIDI-RT-01 — a handle to an opened MIDI output port. Returned by
/// <see cref="IMidiBackend.OpenOutput"/>. All channel-voice arguments are
/// CLAMPED at the Flow builtin boundary BEFORE reaching these methods
/// (channel 0..15, pitch/vel/CC 0..127) — see T-40-01. A handle implementation
/// must still defend charitably and never throw on a send failure.
/// </summary>
public interface IMidiOutputHandle : IDisposable
{
    /// <summary>Send a Note On (channel 0-based; pitch/velocity 0..127).</summary>
    void SendNoteOn(int channel, int pitch, int velocity);

    /// <summary>Send a Note Off (channel 0-based; pitch 0..127).</summary>
    void SendNoteOff(int channel, int pitch);

    /// <summary>Send a Control Change (channel 0-based; controller/value 0..127).</summary>
    void SendControlChange(int channel, int controller, int value);

    /// <summary>
    /// Send a Program Change (channel 0-based; program 0..127). Used by
    /// <c>midiOut</c> GM routing per D-40-02.
    /// </summary>
    void SendProgramChange(int channel, int program);

    /// <summary>
    /// Send a System Exclusive message. Best-effort queue (MIDI-RT-04). The
    /// caller length-caps the array at the builtin boundary (T-40-04) before
    /// it reaches here.
    /// </summary>
    void SendSysex(byte[] data);

    /// <summary>
    /// Send raw MIDI bytes verbatim. The clock send (0xF8 / 0xFA / 0xFB / 0xFC)
    /// routes through here. Plan 40-04: this is the public
    /// <c>rtmidi_out_send_message</c> entry point — the Open-Q1 reflection seam is
    /// gone (no internal-member access needed for raw byte send).
    /// </summary>
    void SendRaw(byte[] bytes);

    /// <summary>Close the port. Idempotent; never throws.</summary>
    void Close();
}
