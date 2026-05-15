namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Represents a Tuning type in the Flow type system — the value produced by
/// <c>(loadScala "path")</c> and consumed by <c>tuning t { ... }</c> blocks.
/// The 15th SpecialType per CONTEXT D-* + Claude's Discretion.
///
/// Specificity 137 — slotted between <see cref="SequenceType"/> (134) and
/// <see cref="SongType"/> (140) per Plan 32-03 RESEARCH §"Type Specificity
/// Ordering". Reference equality per Claude's Discretion: two
/// <c>(loadScala "x.scl")</c> calls produce distinct values even with identical
/// content (Phase 32 doesn't cache per SPEC out-of-scope list).
/// </summary>
public sealed class TuningType : FlowType
{
    private TuningType() { }

    public static TuningType Instance { get; } = new();

    public override string Name => "Tuning";

    public override int GetSpecificity() => 137;

    public override bool IsCompatibleWith(FlowType target) => target is TuningType;

    public override bool CanConvertTo(FlowType target) => target is TuningType;
}
