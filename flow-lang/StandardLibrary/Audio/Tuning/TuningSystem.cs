namespace FlowLang.StandardLibrary.Audio.Tuning;

/// <summary>
/// Closed-set tuning system identifier (CONTEXT D-08, RESEARCH §Standard Stack).
/// Default value is <see cref="EqualTemperament"/> so an unset <c>RenderTuning</c>
/// short-circuits to the byte-identical 12-TET path per Pitfall 6.
/// </summary>
public enum TuningSystem
{
    EqualTemperament,
    JustIntonation,
    Pythagorean,
}
