#if !FLOW_WEB
namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Phase 40 CLOCK-01/02 (D-40-03) — first-class value type for a MIDI clock
/// handle. Returned by <c>(clockMaster MidiDevice)</c> + <c>(clockSlave String)</c>
/// (Plan 40-02); consumed by <c>(clockStop ClockHandle)</c> for lifecycle
/// teardown. Wraps a <see cref="StandardLibrary.Midi.ClockHandleData"/> record
/// holding the master timing thread (or slave listener Task) +
/// CancellationTokenSource + mode discriminator.
///
/// <para>Specificity 153 — slotted above <see cref="MidiDeviceType"/> (152) and
/// <see cref="OscHandleType"/> (151), per D-40-03 discretion. Reference identity
/// like every other handle type: two <c>(clockMaster dev)</c> calls produce
/// DISTINCT <c>ClockHandle</c> values (each spawns its own timing thread + CTS —
/// no caching at the value layer). Mirrors <see cref="OscHandleType"/> /
/// <see cref="MidiDeviceType"/>.</para>
///
/// <para>Strict compatibility: <see cref="IsCompatibleWith"/> and
/// <see cref="CanConvertTo"/> return <c>true</c> ONLY when the target is
/// <see cref="ClockHandleType"/>. No numeric coercion, no cross-handle
/// compatibility (a ClockHandle is NOT a MidiDevice).</para>
///
/// <para><c>#if !FLOW_WEB</c> — Compile-Removed on the Web target (T-40-03),
/// like <see cref="MidiDeviceType"/>.</para>
/// </summary>
public sealed class ClockHandleType : FlowType
{
    private ClockHandleType() { }

    public static ClockHandleType Instance { get; } = new();

    public override string Name => "ClockHandle";

    public override int GetSpecificity() => 153;

    public override bool IsCompatibleWith(FlowType target) => target is ClockHandleType;

    public override bool CanConvertTo(FlowType target) => target is ClockHandleType;
}
#endif
