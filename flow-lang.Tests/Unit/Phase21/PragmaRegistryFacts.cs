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
        // justIntonation will land in Phase 23; not in the Phase 21 closed set.
        Assert.False(PragmaRegistry.IsKnown("justIntonation"));
        Assert.False(PragmaRegistry.IsKnown("scaleLint"));
        Assert.False(PragmaRegistry.IsKnown(""));
    }

    [Fact]
    public void AlphabetizedKnownNames_ReturnsCsvSorted()
    {
        // Phase 21: only "hAsB". Future-proof: assert ordinal-sorted alphabetization.
        var csv = PragmaRegistry.AlphabetizedKnownNames();
        Assert.Equal("hAsB", csv);
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
