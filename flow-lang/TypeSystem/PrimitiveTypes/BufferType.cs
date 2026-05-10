namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Represents an audio buffer type.
/// Buffer instances are created via standard library functions.
/// </summary>
public sealed class BufferType : FlowType
{
    private BufferType() { }

    public static BufferType Instance { get; } = new();

    public override string Name => "Buffer";

    public override int GetSpecificity() => 136;
}
