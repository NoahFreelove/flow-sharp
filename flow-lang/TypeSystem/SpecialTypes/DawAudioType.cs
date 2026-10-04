namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>Owned immutable decoded stereo audio for DAW source windows.</summary>
public sealed class DawAudioType : FlowType
{
    public static DawAudioType Instance { get; } = new();
    private DawAudioType() { }
    public override string Name => "DawAudio";
    public override int GetSpecificity() => 150;
}
