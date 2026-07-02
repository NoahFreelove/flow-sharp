using FlowLang.Diagnostics;

namespace FlowLang.StandardLibrary.Audio.Vocalization;

/// <summary>
/// A single formant band: center frequency, bandwidth, and relative amplitude.
/// </summary>
public record FormantEntry(float Frequency, float Bandwidth, float AmplitudeDb);

/// <summary>
/// Vowel formant frequency tables based on Csound Appendix D Tenor values.
/// Each vowel has 5 formant bands (F1-F5) defining its spectral shape.
/// </summary>
public static class FormantData
{
    /// <summary>
    /// Tenor formant data for 5 vowels (ah, ee, eh, oh, oo).
    /// Each entry contains 5 formants with frequency, bandwidth, and amplitude in dB.
    /// Source: Csound Appendix D Tenor reference.
    /// </summary>
    public static readonly Dictionary<string, FormantEntry[]> TenorFormants = new()
    {
        ["ah"] = new FormantEntry[]
        {
            new(650, 80, 0),
            new(1080, 90, -6),
            new(2650, 120, -7),
            new(2900, 130, -8),
            new(3250, 140, -22)
        },
        ["ee"] = new FormantEntry[]
        {
            new(290, 40, 0),
            new(1870, 90, -15),
            new(2800, 100, -18),
            new(3250, 120, -20),
            new(3540, 120, -30)
        },
        ["eh"] = new FormantEntry[]
        {
            new(400, 70, 0),
            new(1700, 80, -14),
            new(2600, 100, -12),
            new(3200, 120, -14),
            new(3580, 120, -20)
        },
        ["oh"] = new FormantEntry[]
        {
            new(400, 70, 0),
            new(800, 80, -10),
            new(2600, 100, -12),
            new(2800, 130, -12),
            new(3000, 135, -26)
        },
        ["oo"] = new FormantEntry[]
        {
            new(350, 40, 0),
            new(600, 60, -20),
            new(2700, 100, -17),
            new(2900, 120, -14),
            new(3300, 120, -26)
        }
    };

    /// <summary>
    /// Gets the formant entries for a vowel phoneme.
    /// </summary>
    /// <param name="vowel">Vowel key: ah, ee, eh, oh, oo</param>
    /// <returns>Array of 5 FormantEntry values (F1-F5).</returns>
    /// <remarks>
    /// Charitable interpretation (quick-260701-vx4): an unrecognized phoneme no longer
    /// throws — it degrades to the neutral <c>"ah"</c> vowel with a one-shot stderr
    /// advisory so a stray token never halts a render. Valid phonemes are byte-identical
    /// (the found-branch is untouched). Covers every caller — the direct vowel path, the
    /// consonant-vowel path (unmapped vowel remainder), and the whole-string fallback.
    /// </remarks>
    public static FormantEntry[] GetFormants(string vowel)
    {
        if (TenorFormants.TryGetValue(vowel, out var formants))
            return formants;

        RenderingDiagnostics.WarnOnce(
            sentinelKey: $"vocal-unknown-phoneme:{vowel}",
            message: $"[vocal] unknown phoneme '{vowel}' — using 'ah' " +
                     "(valid: ah, ee, eh, oh, oo; onsets s/t/n)");
        return TenorFormants["ah"];
    }

    /// <summary>
    /// Converts a decibel value to linear amplitude.
    /// </summary>
    public static float DbToLinear(double db)
    {
        return (float)Math.Pow(10.0, db / 20.0);
    }
}
