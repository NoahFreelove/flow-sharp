namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Phase 36 Plan 36-06 (GEN-01, D-36-06) — first-class value type for a
/// trained Markov model. Returned by <c>(markovTrain corpus order)</c>;
/// consumed by <c>(markovGenerate model length [seed])</c> and
/// <c>(markovEqual a b)</c>. The wrapping live-data class is
/// <see cref="Runtime.MarkovModelData"/>.
///
/// <para>
/// Specificity 148 — slotted between Phase 26.2's <see cref="HertzType"/> (144)
/// and Phase 33's <see cref="SfzType"/> (150), matching the table in
/// <c>36-PATTERNS.md § Specificity slot table</c>. Reference identity (NOT
/// numeric / NOT cross-music-type compatible) — mirrors the Phase 32
/// <see cref="TuningType"/> + Phase 33 <see cref="SfzType"/> posture: two
/// independently-trained models are DISTINCT values even with identical
/// content. Structural compare lives in the dedicated builtin
/// <c>(markovEqual a b)</c>, not in the type system.
/// </para>
///
/// <para>
/// Strict compatibility: <see cref="IsCompatibleWith"/> and
/// <see cref="CanConvertTo"/> return <c>true</c> ONLY when the target is
/// <see cref="MarkovModelType"/>. No numeric coercion, no cross-music-type
/// flow. Same posture as <see cref="TuningType"/> and <see cref="SfzType"/>.
/// </para>
/// </summary>
public sealed class MarkovModelType : FlowType
{
    private MarkovModelType() { }

    public static MarkovModelType Instance { get; } = new();

    public override string Name => "MarkovModel";

    public override int GetSpecificity() => 148;

    public override bool IsCompatibleWith(FlowType target) => target is MarkovModelType;

    public override bool CanConvertTo(FlowType target) => target is MarkovModelType;
}
