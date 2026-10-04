namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>Declarative DAW note-instrument settings, not live DSP state.</summary>
public sealed class DawInstrumentType : FlowType
{
    public static DawInstrumentType Instance { get; } = new();
    private DawInstrumentType() { }
    public override string Name => "DawInstrument";
    public override int GetSpecificity() => 150;
}
