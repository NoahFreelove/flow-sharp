namespace FlowLang.Diagnostics;

/// <summary>
/// Phase 35 LANG-04 Wave 2a — pure Levenshtein-distance helper, EXTRACTED
/// from <see cref="FlowLang.Lexing.PragmaRegistry"/> per PATTERNS.md
/// Bucket 2a §LevenshteinHelper.cs so the diagnostic renderer's
/// did-you-mean suggestion path and the pragma-registry's unknown-pragma
/// suggestion path converge on a single source of truth.
///
/// <para>
/// Both <see cref="LevenshteinDistance"/> and <see cref="SuggestNearest"/>
/// are public statics — PragmaRegistry now delegates to this helper. The
/// algorithm (Wagner-Fischer DP, two-row rolling array, O(n*m) time +
/// O(m) space) is unchanged from the Phase 21 pre-lift impl.
/// </para>
///
/// <para>
/// Per Pitfall 5 (RESEARCH §471-480), <see cref="SuggestNearest"/>
/// returns at most ONE suggestion: the closest match within
/// <c>threshold = Math.Max(2, typed.Length / 3)</c>. Ties broken by:
/// (1) longest common prefix to <paramref name="typed"/>, then
/// (2) alphabetical (Ordinal).
/// </para>
/// </summary>
public static class LevenshteinHelper
{
    /// <summary>
    /// Returns the closest <paramref name="candidates"/> entry within
    /// edit distance <paramref name="threshold"/> of <paramref name="typed"/>,
    /// or <c>null</c> when none qualify.
    ///
    /// <para>
    /// When <paramref name="threshold"/> is null, defaults to
    /// <c>Math.Max(2, typed.Length / 3)</c> — matches the existing
    /// PragmaRegistry threshold + RESEARCH § Pitfall 5 recommendation.
    /// </para>
    ///
    /// <para>
    /// Returns null when <paramref name="typed"/> is null/empty or
    /// <paramref name="candidates"/> is empty.
    /// </para>
    /// </summary>
    public static string? SuggestNearest(
        string typed,
        IEnumerable<string> candidates,
        int? threshold = null)
    {
        if (string.IsNullOrEmpty(typed)) return null;
        ArgumentNullException.ThrowIfNull(candidates);

        int t = threshold ?? Math.Max(2, typed.Length / 3);
        string? best = null;
        int bestDist = int.MaxValue;
        int bestPrefix = -1;

        foreach (var name in candidates)
        {
            if (name is null) continue;
            int d = LevenshteinDistance(typed, name);
            if (d > t) continue;

            int prefix = CommonPrefixLength(typed, name);

            if (d < bestDist)
            {
                bestDist = d;
                best = name;
                bestPrefix = prefix;
            }
            else if (d == bestDist)
            {
                // Tie on edit-distance. Tie-break 1: longer common prefix wins.
                if (prefix > bestPrefix)
                {
                    best = name;
                    bestPrefix = prefix;
                }
                else if (prefix == bestPrefix
                         && string.CompareOrdinal(name, best) < 0)
                {
                    // Tie-break 2: alphabetical (Ordinal) — first wins.
                    best = name;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Wagner-Fischer Levenshtein distance. Two-row rolling array;
    /// O(n*m) time, O(m) space. Lifted verbatim from the Phase 21
    /// <see cref="FlowLang.Lexing.PragmaRegistry"/> impl —
    /// see Phase 21 plan 21-02 D-12.
    /// </summary>
    public static int LevenshteinDistance(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

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

    private static int CommonPrefixLength(string a, string b)
    {
        int max = Math.Min(a.Length, b.Length);
        for (int i = 0; i < max; i++)
        {
            if (a[i] != b[i]) return i;
        }
        return max;
    }
}
