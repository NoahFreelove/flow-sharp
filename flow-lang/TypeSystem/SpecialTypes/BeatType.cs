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
}
