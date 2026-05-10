namespace FlowLang.StandardLibrary.Audio.Tuning;

/// <summary>
/// Math helpers for tuning-aware frequency synthesis. Pure static; no state.
/// Reuses <see cref="PitchConversion.NoteToFrequency(char, int, int)"/> for the tonic
/// 12-TET reference frequency; per RESEARCH §Pitfall 6 the existing 1-arg + 3-arg
/// overloads stay byte-identical.
/// </summary>
public static class RatioMath
{
    /// <summary>
    /// 12-TET frequency of the tonic note at the given octave. Used as the "1/1"
    /// reference Hz for ratio-based tuning systems per CONTEXT D-01.
    /// </summary>
    public static double TonicHzFromKey(char tonicLetter, int tonicAlteration, int octave)
        => PitchConversion.NoteToFrequency(tonicLetter, octave, tonicAlteration);

    /// <summary>
    /// Multiplicative factor for an additive cent offset per CONTEXT D-10:
    /// freq = base * 2^(cents/1200). Applied AFTER the ratio multiply.
    /// </summary>
    public static double CentOffsetMultiplier(double cents)
        => cents == 0.0 ? 1.0 : Math.Pow(2.0, cents / 1200.0);
}
