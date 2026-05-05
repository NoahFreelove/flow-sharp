using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase24;

/// <summary>
/// Phase 24 Plan 24-01 (LINT-02 closed-set foundation): pins scaleLint as a
/// known pragma so `enable scaleLint;` declarations parse without the D-12
/// unknown-pragma error. Mirrors the Phase 21 PragmaRegistryFacts pattern and
/// the Phase 23 PragmaTuningFacts closed-set growth pattern.
///
/// Decisions referenced (24-CONTEXT.md):
///   D-04 — single one-line flow-lang touch; everything else lives in flow-lsp.
///   D-19 — analyzer activation gate is `Ast.Pragmas.Has("scaleLint")`. Without
///          this registry entry, PragmaScanner rejects the declaration before
///          the gate ever runs.
/// </summary>
public class PragmaRegistryScaleLintFacts
{
    [Fact]
    public void IsKnown_ScaleLint_ReturnsTrue()
    {
        Assert.True(PragmaRegistry.IsKnown("scaleLint"));
    }

    [Fact]
    public void KnownPragmas_HasAtLeastFiveEntries()
    {
        // Phase 21 (1) + Phase 23 (3) + Phase 24 (1) = 5. Lower-bound only per
        // WARNING-3 — future pragma additions must not break this Fact.
        Assert.True(PragmaRegistry.KnownPragmas.Count >= 5,
            $"expected >= 5 known pragmas; got {PragmaRegistry.KnownPragmas.Count}");
    }

    [Fact]
    public void AlphabetizedKnownNames_IncludesScaleLint()
    {
        var csv = PragmaRegistry.AlphabetizedKnownNames();
        Assert.Contains("scaleLint", csv);
    }

    [Fact]
    public void IsKnown_PriorEntries_StillRegistered()
    {
        // Phase 21/23 entries must survive Phase 24 closed-set growth.
        Assert.True(PragmaRegistry.IsKnown("hAsB"));
        Assert.True(PragmaRegistry.IsKnown("justIntonation"));
        Assert.True(PragmaRegistry.IsKnown("pythagorean"));
        Assert.True(PragmaRegistry.IsKnown("equalTemperament"));
    }
}
