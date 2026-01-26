namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Represents an oscillator state type for signal generation.
/// Maintains phase continuity across buffer fills.
/// </summary>
public sealed class OscillatorStateType : FlowType
{
    private OscillatorStateType() { }

    public static OscillatorStateType Instance { get; } = new();

    public override string Name => "OscillatorState";

    public override int GetSpecificity() => 137;
}
