namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Phase 33 — first-class value type for an SFZ-format sampler patch.
/// Returned by <c>(loadSfz Symbol)</c> / <c>(loadSfz String)</c> builtins
/// (Plan 33-05); consumed by the <c>"sampler:NAME"</c> instrument-string
/// dispatcher in <c>SongRenderer</c> (Plan 33-07).
///
/// Specificity 150 — slotted above all existing music types
/// (TuningType=137, SectionType=138, BeatType=139, SongType=140,
/// HertzType=144). Reference identity per CONTEXT § "Claude's Discretion":
/// two <c>(loadSfz #violin)</c> calls produce DISTINCT <see cref="Sfz"/>
/// values even with identical resolved paths — no caching at the value
/// layer, mirroring Phase 32's <see cref="TuningType"/> contract.
///
/// Strict compatibility: <see cref="IsCompatibleWith"/> and
/// <see cref="CanConvertTo"/> return <c>true</c> ONLY when the target is
/// <see cref="SfzType"/>. No numeric coercion, no cross-music-type
/// compatibility (e.g. an Sfz value will NOT pass into a Tuning-typed
/// parameter slot). Same posture as <see cref="TuningType"/>.
/// </summary>
public sealed class SfzType : FlowType
{
    private SfzType() { }

    public static SfzType Instance { get; } = new();

    public override string Name => "Sfz";

    public override int GetSpecificity() => 150;

    public override bool IsCompatibleWith(FlowType target) => target is SfzType;

    public override bool CanConvertTo(FlowType target) => target is SfzType;
}
