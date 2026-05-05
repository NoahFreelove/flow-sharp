namespace FlowLang.StandardLibrary.Audio.Tuning;

/// <summary>
/// Closed-set diatonic mode identifier (CONTEXT D-03 — mode shifts the table).
/// Default value is <see cref="Major"/> so an unset <c>RenderTuning.Mode</c>
/// matches the D-02 silent C-major-default fallback.
/// </summary>
public enum Mode
{
    Major,
    Minor,
    Dorian,
    Phrygian,
    Lydian,
    Mixolydian,
    Locrian,
}
