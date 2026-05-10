namespace FlowLang.Lexing;

/// <summary>
/// Closed-set registry of recognized file-scope pragma names. Phase 21 ships
/// <c>hAsB</c> as the only entry per D-17. Future phases (23 microtonal, 24
/// scaleLint) add their own entries when they ship — the closed-set design
/// guarantees unknown names error via D-12 rather than silently passing.
/// </summary>
public static class PragmaRegistry
{
    /// <summary>
    /// Map from pragma name to a one-line human-readable description. The
    /// description is currently informational (not surfaced in errors); it lives
    /// here so the registry doubles as the canonical pragma reference.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KnownPragmas =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hAsB"] = "Inside note streams, accept 'H' as a synonym for 'B' (German notation).",
            ["justIntonation"] = "5-limit just-intonation render-time tuning rooted at active key tonic (default C major).",
            ["pythagorean"] = "3-limit Pythagorean (chain-of-fifths) render-time tuning rooted at active key tonic.",
            ["equalTemperament"] = "12-tone equal temperament (default). Explicit form for tooling-visible intent.",
            ["scaleLint"] = "Inside `key { ... }` blocks, surface non-diatonic notes as Information-severity LSP diagnostics."
        };

    /// <summary>True iff <paramref name="name"/> is a recognized pragma.</summary>
    public static bool IsKnown(string name) => KnownPragmas.ContainsKey(name);

    /// <summary>
    /// Returns the alphabetized csv of known pragma names for D-12 errors.
    /// Ordinal sort ensures deterministic output for tests and snapshots.
    /// </summary>
    public static string AlphabetizedKnownNames() =>
        string.Join(", ", KnownPragmas.Keys.OrderBy(s => s, StringComparer.Ordinal));

    /// <summary>
    /// Wagner-Fischer Levenshtein. Returns the closest known pragma name within
    /// distance <c>max(2, typed.Length / 3)</c>, or <c>null</c> if no candidate
    /// is close enough. Pure-stdlib implementation; correctness over speed since
    /// this is only invoked on the unknown-pragma error path.
    /// </summary>
    public static string? SuggestNearest(string typed)
    {
        if (string.IsNullOrEmpty(typed)) return null;
        int threshold = Math.Max(2, typed.Length / 3);
        string? best = null;
        int bestDist = int.MaxValue;
        foreach (var name in KnownPragmas.Keys)
        {
            int d = LevenshteinDistance(typed, name);
            if (d <= threshold && d < bestDist)
            {
                bestDist = d;
                best = name;
            }
        }
        return best;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        // Wagner-Fischer DP. Two-row rolling array; O(n*m) time, O(m) space.
        // The closed-set max name length bounds the inner-loop allocation regardless
        // of caller-supplied input length (T-21-02 mitigation in the threat register).
        int n = a.Length, m = b.Length;
        if (n == 0) return m;
        if (m == 0) return n;
        var prev = new int[m + 1];
        var curr = new int[m + 1];
        for (int j = 0; j <= m; j++) prev[j] = j;
        for (int i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[m];
    }
}
