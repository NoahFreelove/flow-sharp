#if !FLOW_WEB
namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Phase 40 JACK-01 (D-40-03 / D-40-05 best-effort) — first-class value type for a
/// JACK transport-sync handle. Returned by <c>(jackSync)</c> (opt-in
/// <c>use "@jack"</c>); the handle records whether a JACK server was reachable and
/// the transport snapshot that drove <see cref="FlowLang.Runtime.MusicalContext.Tempo"/>
/// at sync time. Wraps a <see cref="StandardLibrary.Midi.JackHandleData"/> record.
///
/// <para>Specificity 154 — slotted above <see cref="ClockHandleType"/> (153),
/// <see cref="MidiDeviceType"/> (152) and <see cref="OscHandleType"/> (151), per
/// D-40-03 discretion. Reference identity like every other handle type: two
/// <c>(jackSync)</c> calls produce DISTINCT <c>JackHandle</c> values (each is its
/// own transport-query snapshot — no caching at the value layer). Mirrors
/// <see cref="OscHandleType"/> / <see cref="MidiDeviceType"/> /
/// <see cref="ClockHandleType"/>.</para>
///
/// <para>Strict compatibility: <see cref="IsCompatibleWith"/> and
/// <see cref="CanConvertTo"/> return <c>true</c> ONLY when the target is
/// <see cref="JackHandleType"/>. No numeric coercion, no cross-handle
/// compatibility (a JackHandle is NOT a ClockHandle).</para>
///
/// <para><c>#if !FLOW_WEB</c> — Compile-Removed on the Web target (T-40-03), like
/// <see cref="MidiDeviceType"/> / <see cref="ClockHandleType"/>. JACK is a
/// Linux-only native dep that can never run in a browser sandbox.</para>
/// </summary>
public sealed class JackHandleType : FlowType
{
    private JackHandleType() { }

    public static JackHandleType Instance { get; } = new();

    public override string Name => "JackHandle";

    public override int GetSpecificity() => 154;

    public override bool IsCompatibleWith(FlowType target) => target is JackHandleType;

    public override bool CanConvertTo(FlowType target) => target is JackHandleType;
}
#endif
