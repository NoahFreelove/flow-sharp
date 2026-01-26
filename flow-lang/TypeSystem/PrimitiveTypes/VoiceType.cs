namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Represents a voice type for positioned audio clips on a timeline.
/// </summary>
public sealed class VoiceType : FlowType
{
    private VoiceType() { }

    public static VoiceType Instance { get; } = new();

    public override string Name => "Voice";

    public override int GetSpecificity() => 140;
}
