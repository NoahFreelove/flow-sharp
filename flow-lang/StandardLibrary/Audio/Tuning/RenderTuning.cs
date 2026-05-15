namespace FlowLang.StandardLibrary.Audio.Tuning;

/// <summary>
/// Resolved render-time tuning context threaded through <c>INoteSynthesizer.RenderNote</c>
/// per Pattern A (RESEARCH §Architecture Patterns Pattern 1; Pattern B static accessor
/// rejected — no codebase analog and would introduce global mutable state).
/// Default value (System=EqualTemperament, Mode=Major, TonicLetter='C', TonicAlteration=0,
/// Custom=null) triggers the byte-identical 12-TET short-circuit in
/// <c>PitchConversion.NoteToFrequency</c> per Pitfall 6 mitigation.
///
/// Phase 32 extension (CONTEXT D-03): the optional <c>Custom</c> field carries a
/// <see cref="ResolvedTuning"/> when a user-supplied .scl is active. When
/// <c>Custom != null</c>, <see cref="PitchConversion.NoteToFrequency"/> reads
/// <c>Custom.MidiToHz[midi]</c> as an O(1) array lookup; when <c>Custom == null</c>,
/// the existing Phase 23 12-TET / JI / Pythagorean logic runs unchanged. All 4-arg
/// call sites (SongRenderer:184, <see cref="Default"/> factory, ≥ 4 Phase 23 test
/// sites) compile unchanged because the new parameter has a default value of <c>null</c>.
/// </summary>
public readonly record struct RenderTuning(
    TuningSystem System,
    Mode Mode,
    char TonicLetter,
    int TonicAlteration,
    ResolvedTuning? Custom = null)
{
    public static RenderTuning Default => new(TuningSystem.EqualTemperament, Mode.Major, 'C', 0);
}
