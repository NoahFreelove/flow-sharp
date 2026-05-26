namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Phase 38 OSC-01 — first-class value type for an OSC listener handle.
/// Returned by <c>(oscListen Int String Function)</c> (Plan 38-06);
/// consumed by <c>(oscStop OscHandle)</c> for lifecycle teardown per
/// D-38-16. Wraps a <see cref="StandardLibrary.Network.OscHandleData"/>
/// record holding the Port + Path + underlying Rug.Osc OscReceiver +
/// CancellationTokenSource + listener Task.
///
/// Specificity 151 — slotted above all existing music types
/// (TuningType=150, SfzType=150, MarkovModelType=148, LsystemModelType=149).
/// Reference identity per CONTEXT D-38-16 + the Phase 32 Tuning / Phase 33
/// Sfz / Phase 36 MarkovModel/LsystemModel precedent: two
/// <c>(oscListen 7777 "/x" h)</c> calls produce DISTINCT
/// <see cref="OscHandle"/> values even with identical port + path
/// arguments (each call spawns its own receive loop and CTS — no caching
/// at the value layer).
///
/// Strict compatibility: <see cref="IsCompatibleWith"/> and
/// <see cref="CanConvertTo"/> return <c>true</c> ONLY when the target is
/// <see cref="OscHandleType"/>. No numeric coercion, no cross-music-type
/// compatibility. Same posture as <see cref="TuningType"/> /
/// <see cref="SfzType"/>.
/// </summary>
public sealed class OscHandleType : FlowType
{
    private OscHandleType() { }

    public static OscHandleType Instance { get; } = new();

    public override string Name => "OscHandle";

    public override int GetSpecificity() => 151;

    public override bool IsCompatibleWith(FlowType target) => target is OscHandleType;

    public override bool CanConvertTo(FlowType target) => target is OscHandleType;
}
