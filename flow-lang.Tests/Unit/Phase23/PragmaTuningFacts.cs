using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase23;

/// <summary>
/// Phase 23 D-08 closed-set growth Facts. Wave 2 grows
/// <see cref="PragmaRegistry.KnownPragmas"/> from the Phase 21 single entry (hAsB)
/// to 4 entries by adding three tuning pragmas: justIntonation, pythagorean,
/// equalTemperament. Phase 24 will further extend this set with scaleLint —
/// the lower-bound pin (<c>>= 4</c>) is intentionally upper-unconstrained per
/// WARNING-3 so that future pragma additions don't break this Fact.
///
/// Decisions referenced (CONTEXT 23-microtonal-tuning-wedge):
///   D-08 — three tuning pragmas register at known-pragma list level so the
///          unknown-pragma error path (D-12) gates typos out before parse.
///   D-17 (Phase 21) — closed-set growth pattern; Wave 2 reserves 3 entries.
/// </summary>
public class PragmaTuningFacts
{
    [Fact]
    public void IsKnown_JustIntonation_ReturnsTrue()
    {
        Assert.True(PragmaRegistry.IsKnown("justIntonation"));
    }

    [Fact]
    public void IsKnown_Pythagorean_ReturnsTrue()
    {
        Assert.True(PragmaRegistry.IsKnown("pythagorean"));
    }

    [Fact]
    public void IsKnown_EqualTemperament_ReturnsTrue()
    {
        Assert.True(PragmaRegistry.IsKnown("equalTemperament"));
    }

    [Fact]
    public void IsKnown_HAsB_StillRegistered()
    {
        // Phase 21 entry must be preserved across Phase 23 closed-set growth.
        Assert.True(PragmaRegistry.IsKnown("hAsB"));
    }

    [Fact]
    public void KnownPragmas_HasAtLeastFourEntries()
    {
        // Phase 24 will add scaleLint (count → 5); the upper bound is intentionally
        // unconstrained so future pragma additions don't break this Fact (WARNING-3).
        Assert.True(PragmaRegistry.KnownPragmas.Count >= 4,
            $"expected >= 4 known pragmas; got {PragmaRegistry.KnownPragmas.Count}");
    }

    [Fact]
    public void AlphabetizedKnownNames_ContainsAllFour()
    {
        var csv = PragmaRegistry.AlphabetizedKnownNames();
        Assert.Contains("hAsB", csv);
        Assert.Contains("justIntonation", csv);
        Assert.Contains("pythagorean", csv);
        Assert.Contains("equalTemperament", csv);
    }
}
