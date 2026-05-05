namespace FlowLang.StandardLibrary.Audio.Tuning;

/// <summary>
/// Spelling-aware chromatic ratio map per D-09. Indexed by (NoteLetter, Alteration)
/// where Alteration is -1 for flats, 0 for naturals, +1 for sharps. Eb (E,-1) and
/// D# (D,+1) MUST be different entries — in 5-limit JI Eb=6/5 (1.200) and
/// D#=75/64 (1.171875) are different pitches.
/// </summary>
public sealed record ChromaticRatioTable(
    IReadOnlyDictionary<(char Letter, int Alteration), double> Ratios)
{
    /// <summary>
    /// Constructs a ChromaticRatioTable from three convenience sub-maps.
    /// All 7 naturals (C/D/E/F/G/A/B) are REQUIRED. Sharps and flats are optional
    /// (each entry is added only if present in the corresponding sub-map).
    /// </summary>
    public static ChromaticRatioTable Build(
        IReadOnlyDictionary<char, double> naturals,
        IReadOnlyDictionary<char, double> sharps,
        IReadOnlyDictionary<char, double> flats)
    {
        var dict = new Dictionary<(char, int), double>();
        foreach (var letter in new[] { 'C', 'D', 'E', 'F', 'G', 'A', 'B' })
        {
            if (!naturals.TryGetValue(letter, out var r))
                throw new InvalidOperationException(
                    $"ChromaticRatioTable.Build: natural '{letter}' is required");
            dict[(letter, 0)] = r;
        }
        foreach (var (letter, ratio) in sharps) dict[(letter, +1)] = ratio;
        foreach (var (letter, ratio) in flats)  dict[(letter, -1)] = ratio;
        return new ChromaticRatioTable(dict);
    }

    /// <summary>
    /// Looks up the ratio for a (letter, alteration) pair. Throws
    /// <see cref="KeyNotFoundException"/> when the spelling has no entry — the
    /// caller (TuningTables.LookupRatio) is responsible for fallback construction
    /// per RESEARCH §Pitfall 3 chromatic-fallback rule.
    /// </summary>
    public double Lookup(char letter, int alteration) =>
        Ratios[(letter, alteration)];

    public bool TryLookup(char letter, int alteration, out double ratio) =>
        Ratios.TryGetValue((letter, alteration), out ratio);
}
