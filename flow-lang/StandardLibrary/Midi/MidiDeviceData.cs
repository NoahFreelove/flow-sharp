#if !FLOW_WEB
using FlowLang.Audio;

namespace FlowLang.StandardLibrary.Midi;

/// <summary>
/// Phase 40 MIDI-RT-01 (D-40-03) — runtime state behind a <c>MidiDevice</c>
/// handle. Models <see cref="StandardLibrary.Network.OscHandleData"/>'s
/// <c>required</c>-init record shape. Carries the opened
/// <see cref="IMidiOutputHandle"/> (null = charitable dead handle when the port
/// was absent or <c>librtmidi.so</c> is missing) and the port name the composer
/// requested.
///
/// <para><c>#if !FLOW_WEB</c> — Compile-Removed on the Web target (T-40-03),
/// like <c>OscHandleData</c>.</para>
/// </summary>
public sealed class MidiDeviceData
{
    /// <summary>The port name the composer passed to <c>(openMidiOutput ...)</c>.</summary>
    public required string PortName { get; init; }

    /// <summary>
    /// The opened output handle, or <c>null</c> for a charitable dead handle
    /// (absent port / missing native lib). Every <c>midi*</c> builtin null-guards
    /// this so a dead handle degrades to a quiet no-op rather than throwing.
    /// </summary>
    public required IMidiOutputHandle? Handle { get; init; }
}
#endif
