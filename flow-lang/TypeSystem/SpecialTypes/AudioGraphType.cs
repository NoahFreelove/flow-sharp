namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>Immutable declarative audio graph. No callback/engine or device handles.</summary>
public sealed class AudioGraphType : FlowType
{
    public static AudioGraphType Instance { get; } = new();
    private AudioGraphType() { }
    public override string Name => "AudioGraph";
    public override int GetSpecificity() => 150;
}
