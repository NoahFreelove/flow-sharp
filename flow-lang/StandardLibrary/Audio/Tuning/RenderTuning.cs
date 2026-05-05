namespace FlowLang.StandardLibrary.Audio.Tuning;

/// <summary>
/// Resolved render-time tuning context threaded through <c>INoteSynthesizer.RenderNote</c>
/// per Pattern A (RESEARCH §Architecture Patterns Pattern 1; Pattern B static accessor
/// rejected — no codebase analog and would introduce global mutable state).
/// Default value (System=EqualTemperament, Mode=Major, TonicLetter='C', TonicAlteration=0)
/// triggers the byte-identical 12-TET short-circuit in <c>PitchConversion.NoteToFrequency</c>
/// per Pitfall 6 mitigation.
/// </summary>
public readonly record struct RenderTuning(
    TuningSystem System,
    Mode Mode,
    char TonicLetter,
    int TonicAlteration)
{
    public static RenderTuning Default => new(TuningSystem.EqualTemperament, Mode.Major, 'C', 0);
}
