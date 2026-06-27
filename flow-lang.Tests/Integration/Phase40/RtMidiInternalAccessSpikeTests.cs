// =============================================================================
// PHASE 40 OPEN Q1 — OBSOLETED by Plan 40-04 (direct librtmidi P/Invoke).
// =============================================================================
//
// HISTORY. Plan 40-01 resolved Open Q1 ("how does the clock reach the internal
// raw-byte path?") with strategy (a): REFLECTION into RtMidi.Core's internal
// IRtMidiOutputDevice.SendMessage(byte[]) via the private MidiOutputDevice
// ._outputDevice bridge field. The original tests in this file pinned those
// internal members so a future RtMidi.Core bump would fire RED at CI.
//
// SUPERSESSION (Plan 40-04). RtMidi.Core 1.0.53 (2018) was REMOVED entirely. Its
// pinned binding calls the OLD `const char* rtmidi_get_port_name(device, port)`
// signature; modern librtmidi (>= 4.0; 6.0.0 / librtmidi.so.7 on the bench box)
// changed that to `int rtmidi_get_port_name(device, port, char* bufOut, int* bufLen)`.
// RtMidi.Core reads the length-out pointer as a string and frees garbage —
// `free(): invalid pointer` aborts the WHOLE process during `(midiPorts)`
// enumeration on any modern Linux. The in-process CaptureMidiBackend seam hid this;
// it crashed on real hardware.
//
// The fix binds librtmidi DIRECTLY (Audio/LibRtMidi.cs, modern signatures),
// mirroring the [DllImport("jack")] approach in JackFunctions.cs. Crucially, raw
// byte send is now the PUBLIC `rtmidi_out_send_message` entry point — clock
// 0xF8/0xFA/0xFB/0xFC + notes/CC/sysex all flow through it with NO reflection. The
// strategy-(c) "documented fallback" of the old spike IS the shipped path now.
//
// Therefore the reflection-internal assertions are DELETED: there is no RtMidi.Core
// assembly to reflect into, and asserting its internal members exist would be a
// false invariant. The real native-path verification lives in
// RealMidiLoopbackTests (ALSA VirMIDI loopback, captured via amidi) — the actual
// proof that the modern bindings work end-to-end.
// =============================================================================

using Xunit;

namespace FlowLang.Tests.Integration.Phase40;

/// <summary>
/// Phase 40 — documenting placeholder for the OBSOLETED Open-Q1 RtMidi.Core
/// internal-access spike. Plan 40-04 replaced RtMidi.Core with direct librtmidi
/// P/Invoke (<see cref="FlowLang.Audio.LibRtMidi"/>), so the reflection bridge —
/// and every assertion pinning RtMidi.Core internal members — is gone. The native
/// MIDI path is now verified for real by
/// <c>RealMidiLoopbackTests</c> (ALSA VirMIDI loopback). See the top-of-file
/// comment for the full supersession rationale.
/// </summary>
public class RtMidiInternalAccessSpikeTests
{
    /// <summary>
    /// The Open-Q1 reflection strategy is superseded. This single test documents
    /// the supersession (and keeps the file compiling without referencing the
    /// removed RtMidi.Core assembly). It MUST NOT reintroduce any RtMidi.Core
    /// internal-member assertion — raw byte send is the public
    /// <c>rtmidi_out_send_message</c>; the real-path proof is RealMidiLoopbackTests.
    /// </summary>
    [Fact]
    public void OpenQ1_ReflectionBridge_SupersededByDirectPInvoke()
    {
        // Intentionally a no-op assertion. The historical reflection target
        // (RtMidi.Core.dll) no longer exists in the dependency closure; binding to
        // its internals would be a false invariant. Direct librtmidi P/Invoke
        // (LibRtMidi.cs) is the shipped path; RealMidiLoopbackTests proves it.
        Assert.True(true);
    }
}
