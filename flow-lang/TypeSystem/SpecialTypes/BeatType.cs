using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Represents a musical beat as a unit of time.
/// Stored as a double (e.g., 1.5 = 1 beat + half beat).
/// Converted to frames based on BPM context.
/// </summary>
public sealed class BeatType : FlowType
{
    private BeatType() { }

    public static BeatType Instance { get; } = new();

    public override string Name => "Beat";

    public override int GetSpecificity() => 139;

    /// <summary>
    /// Beat is compatible with Double and Float — Beat is stored as a fractional double
    /// (e.g. 1.5 = one and a half beats), so passing a Beat to a Double-typed parameter
    /// (e.g. arithmetic builtins, user procs) just works. Mirrors CentType.
    /// </summary>
    public override bool IsCompatibleWith(FlowType target)
    {
        return target is DoubleType or FloatType || base.IsCompatibleWith(target);
    }

    /// <summary>
    /// Phase 26.1 DICT-01: Beat is double-backed and has natural value equality —
    /// usable as a Dict key (CONTEXT § Specifics block 9 acceptance shape
    /// <c>Dict&lt;Tuple&lt;&lt;Note, Beat&gt;&gt;, Int&gt;</c>).
    /// </summary>
    public override bool IsHashable() => true;
}
