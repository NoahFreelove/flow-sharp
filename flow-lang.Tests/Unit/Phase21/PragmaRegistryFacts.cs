using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase21;

/// <summary>
/// PRAG-01 closed-set registry Facts. Phase 21 ships hAsB as the ONLY known pragma
/// per D-17. Future phases (23 microtonal, 24 scaleLint) add their own entries.
///
/// Decisions referenced (locked in 21-CONTEXT.md):
///   D-12 — Unknown pragma name cites alphabetized known list + did-you-mean (Levenshtein).
///   D-17 — hAsB is the only active pragma in Phase 21; closed-set membership.
/// </summary>
public class PragmaRegistryFacts
{
    [Fact]
    public void IsKnown_HAsB_ReturnsTrue()
    {
        Assert.True(PragmaRegistry.IsKnown("hAsB"));
    }

    [Fact]
    public void IsKnown_UnknownName_ReturnsFalse()
    {
        // Phase 23 closed-set growth: justIntonation now IS known. Phase 24 closed-set
        // growth: scaleLint now IS known. The negative assertion intent is preserved
        // via the sentinel "futureUnknownPragma" — mirrors the prior justIntonation
        // migration documented at this Fact's comment in earlier phases.
        Assert.False(PragmaRegistry.IsKnown("futureUnknownPragma"));
        Assert.False(PragmaRegistry.IsKnown(""));
    }

    [Fact]
    public void AlphabetizedKnownNames_ReturnsCsvSorted()
    {
        // Phase 24 closed-set growth: 5 entries — equalTemperament, hAsB,
        // justIntonation, pythagorean, scaleLint (ordinal-sorted: e < h < j < p < s).
        var csv = PragmaRegistry.AlphabetizedKnownNames();
        Assert.Equal("equalTemperament, hAsB, justIntonation, pythagorean, scaleLint", csv);
    }

    [Fact]
    public void SuggestNearest_FindsClose_HAsBForHasb()
    {
        // typed "hasb" → distance 2 from "hAsB" (case differs in 2 positions).
        // threshold = max(2, 4/3) = 2 → "hAsB" is suggested.
        Assert.Equal("hAsB", PragmaRegistry.SuggestNearest("hasb"));
    }

    [Fact]
    public void SuggestNearest_ReturnsNullForFarAway()
    {
        // "wibblefoo" is far beyond any known pragma; no suggestion.
        Assert.Null(PragmaRegistry.SuggestNearest("wibblefoo"));
    }
}
