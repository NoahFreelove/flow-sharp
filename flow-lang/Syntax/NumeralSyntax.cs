namespace FlowLang.Syntax;

/// <summary>
/// Lexical rules for roman numerals (<c>I</c>, <c>ii</c>, <c>V7</c>, <c>viidim</c>). Part of
/// Flow's one grammar (numerals in streams and match patterns); the music layer's
/// <c>ScaleDatabase</c> resolves them against a key.
/// </summary>
public static class NumeralSyntax
{
    /// <summary>
    /// Checks if text looks like a roman numeral chord reference.
    /// </summary>
    public static bool IsRomanNumeral(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        var (baseNumeral, _) = SplitRomanNumeral(text);
        return baseNumeral != null;
    }

    /// <summary>
    /// Splits a roman numeral string into the base numeral and optional quality extension.
    /// </summary>
    public static (string? baseNumeral, string? extension) SplitRomanNumeral(string text)
    {
        // Try longest roman numeral first to avoid partial matches
        string[] upperNumerals = { "VII", "III", "VI", "IV", "II", "V", "I" };
        string[] lowerNumerals = { "vii", "iii", "vi", "iv", "ii", "v", "i" };

        foreach (var rn in upperNumerals)
        {
            if (text.StartsWith(rn, StringComparison.Ordinal))
            {
                string ext = text[rn.Length..];
                if (ext.Length == 0 || IsQualityExtension(ext))
                    return (rn, ext.Length == 0 ? null : ext);
            }
        }

        foreach (var rn in lowerNumerals)
        {
            if (text.StartsWith(rn, StringComparison.Ordinal))
            {
                string ext = text[rn.Length..];
                if (ext.Length == 0 || IsQualityExtension(ext))
                    return (rn, ext.Length == 0 ? null : ext);
            }
        }

        return (null, null);
    }

    private static bool IsQualityExtension(string ext)
    {
        return ext is "7" or "maj7" or "min7" or "m7" or "dim7" or "sus2" or "sus4"
            or "9" or "6" or "m6" or "add9" or "aug" or "dim";
    }
}
