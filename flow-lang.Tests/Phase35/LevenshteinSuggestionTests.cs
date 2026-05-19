using FlowLang.Diagnostics;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 LANG-04 Wave 2a — did-you-mean Levenshtein suggestion tests.
///
/// <para>
/// Mirrors PragmaRegistry's existing Levenshtein behavior: threshold of
/// <c>max(2, typed.Length / 3)</c>, ties broken by longest common prefix
/// to <c>typed</c>, alphabetical when prefix lengths also tie. Returns at
/// most ONE suggestion (per RESEARCH § Pitfall 5 — confusing
/// did-you-mean shadow rule).
/// </para>
/// </summary>
public class LevenshteinSuggestionTests
{
    [Fact]
    public void ClosestMatchWithinThreshold()
    {
        // typed = "transpos" (8 chars); threshold = max(2, 8/3) = 2.
        // Distance("transpos", "transpose") = 1 (insert 'e' at end) → match.
        // Distance("transpos", "reverse") = 7 → above threshold.
        var suggestion = LevenshteinHelper.SuggestNearest(
            "transpos",
            new[] { "transpose", "reverse" });
        Assert.Equal("transpose", suggestion);
    }

    [Fact]
    public void NullWhenAllCandidatesExceedThreshold()
    {
        // typed = "xyz" (3 chars); threshold = max(2, 3/3) = 2.
        // Distance("xyz", "abc") = 3 → above threshold.
        // Distance("xyz", "def") = 3 → above threshold.
        var suggestion = LevenshteinHelper.SuggestNearest(
            "xyz",
            new[] { "abc", "def" });
        Assert.Null(suggestion);
    }

    [Fact]
    public void TieBrokenByLongestCommonPrefixThenAlphabetical()
    {
        // typed = "tra" (3 chars); threshold = max(2, 3/3) = 2.
        // Distance("tra", "tray") = 1 (insert y)        → LCP("tra","tray") = 3
        // Distance("tra", "trab") = 1 (insert b)        → LCP("tra","trab") = 3
        // Distance("tra", "tron") = 2 (sub a→o, ins n)  → LCP("tra","tron") = 2
        // Closest distance is 1 → narrow to {"tray","trab"}.
        // Both have LCP=3 with "tra" → tie on LCP → fall back to alphabetical.
        // Alphabetical: "trab" < "tray" → "trab" wins.
        var suggestion = LevenshteinHelper.SuggestNearest(
            "tra",
            new[] { "tray", "trab", "tron" });
        Assert.Equal("trab", suggestion);
    }

    [Fact]
    public void EmptyOrNullTypedReturnsNull()
    {
        Assert.Null(LevenshteinHelper.SuggestNearest("", new[] { "a", "b" }));
    }

    [Fact]
    public void EmptyCandidatesReturnsNull()
    {
        Assert.Null(LevenshteinHelper.SuggestNearest("foo", Array.Empty<string>()));
    }

    [Fact]
    public void LevenshteinDistance_KnownValues()
    {
        Assert.Equal(0, LevenshteinHelper.LevenshteinDistance("abc", "abc"));
        Assert.Equal(1, LevenshteinHelper.LevenshteinDistance("abc", "abd"));
        Assert.Equal(3, LevenshteinHelper.LevenshteinDistance("", "abc"));
        Assert.Equal(3, LevenshteinHelper.LevenshteinDistance("abc", ""));
        Assert.Equal(2, LevenshteinHelper.LevenshteinDistance("kitten", "sitten")
            + LevenshteinHelper.LevenshteinDistance("sitten", "sittin"));
    }
}
