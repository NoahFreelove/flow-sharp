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
        // Phase 23 closed-set growth: justIntonation now IS known (Phase 23 D-08); the
        // Wave 2 plan migrated the original "justIntonation will land in Phase 23"
        // negative assertion. scaleLint remains a Phase 24 future entry — still unknown.
        Assert.False(PragmaRegistry.IsKnown("scaleLint"));
        Assert.False(PragmaRegistry.IsKnown(""));
    }

    [Fact]
    public void AlphabetizedKnownNames_ReturnsCsvSorted()
    {
        // Phase 23 closed-set growth: 4 entries — equalTemperament, hAsB,
        // justIntonation, pythagorean (ordinal-sorted: uppercase 'h' < lowercase 'h'
        // and 'e' < 'h' < 'j' < 'p').
        var csv = PragmaRegistry.AlphabetizedKnownNames();
        Assert.Equal("equalTemperament, hAsB, justIntonation, pythagorean", csv);
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
