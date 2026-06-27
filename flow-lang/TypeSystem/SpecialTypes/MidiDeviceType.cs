#if !FLOW_WEB
namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Phase 40 MIDI-RT-01 (D-40-03) — first-class reference-identity value type for
/// an opened MIDI output device. Returned by <c>(openMidiOutput "port")</c>;
/// consumed by <c>(midiNoteOn dev ...)</c> / <c>(midiCC ...)</c> /
/// <c>(midiSysex ...)</c>. Wraps a
/// <see cref="StandardLibrary.Midi.MidiDeviceData"/> holding the opened
/// <c>IMidiOutputHandle?</c> (null = charitable dead handle) + the port name.
///
/// <para>Specificity 152 — slotted just above <see cref="OscHandleType"/> (=151),
/// per 40-RESEARCH Pattern 3 / D-40-03 discretion. Reference identity per the
/// Phase 32 Tuning / Phase 33 Sfz / Phase 36 Markov/Lsystem / Phase 38 OscHandle
/// precedent: two <c>(openMidiOutput "x")</c> calls produce DISTINCT
/// <see cref="MidiDeviceType"/> values even for the same port name.</para>
///
/// <para>Strict compatibility: <see cref="IsCompatibleWith"/> /
/// <see cref="CanConvertTo"/> return true ONLY for <see cref="MidiDeviceType"/>.
/// No numeric coercion. Same posture as <see cref="OscHandleType"/>.</para>
///
/// <para><c>#if !FLOW_WEB</c> — Compile-Removed on the Web target (T-40-03).</para>
/// </summary>
public sealed class MidiDeviceType : FlowType
{
    private MidiDeviceType() { }

    public static MidiDeviceType Instance { get; } = new();

    public override string Name => "MidiDevice";

    public override int GetSpecificity() => 152;

    public override bool IsCompatibleWith(FlowType target) => target is MidiDeviceType;

    public override bool CanConvertTo(FlowType target) => target is MidiDeviceType;
}
#endif
