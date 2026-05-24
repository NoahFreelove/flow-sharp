namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Phase 36 Plan 36-07 (GEN-02, D-36-06 + D-36-08) — first-class value type for
/// a trained L-system model. Returned by <c>(lsystemModel axiom rules)</c>;
/// consumed by <c>(lsystemGenerate model iterations)</c> and
/// <c>(lsystemEqual a b)</c>. The wrapping live-data class is
/// <see cref="Runtime.LsystemModelData"/>.
///
/// <para>
/// Specificity 149 — slotted between Phase 36 Plan 36-06's <see cref="MarkovModelType"/>
/// (148) and Phase 33's <see cref="SfzType"/> (150) per
/// <c>36-PATTERNS.md § Specificity slot table</c>. Reference identity
/// (NOT numeric / NOT cross-music-type compatible) — mirrors the Phase 32
/// <see cref="TuningType"/> + Phase 33 <see cref="SfzType"/> + Plan 36-06
/// <see cref="MarkovModelType"/> posture: two independently-built models are
/// DISTINCT values even with identical content. Structural compare lives in
/// the dedicated builtin <c>(lsystemEqual a b)</c>, not in the type system.
/// </para>
///
/// <para>
/// Strict compatibility: <see cref="IsCompatibleWith"/> and
/// <see cref="CanConvertTo"/> return <c>true</c> ONLY when the target is
/// <see cref="LsystemModelType"/>. No numeric coercion, no cross-music-type
/// flow. Same posture as <see cref="TuningType"/> / <see cref="SfzType"/> /
/// <see cref="MarkovModelType"/>.
/// </para>
/// </summary>
public sealed class LsystemModelType : FlowType
{
    private LsystemModelType() { }

    public static LsystemModelType Instance { get; } = new();

    public override string Name => "LsystemModel";

    public override int GetSpecificity() => 149;

    public override bool IsCompatibleWith(FlowType target) => target is LsystemModelType;

    public override bool CanConvertTo(FlowType target) => target is LsystemModelType;
}
