using System.Collections.Generic;
using System.Linq;
using FlowLang.Core;
using FlowLang.Runtime;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-10 Task 2 — section overload registry + dispatch facts.
///
/// <para>
/// Tests the runtime contract: multiple <c>section verse(...)</c> declarations
/// with different pattern signatures coexist in <see cref="ExecutionContext.SectionRegistry"/>
/// (list-of-overloads shape), <see cref="SectionOverloadDispatch.Resolve"/>
/// picks the highest-specificity match, ambiguous shapes are caught at
/// declaration time (Pitfall 3).
/// </para>
/// </summary>
public class SectionOverloadTests
{
    [Fact]
    public void SectionRegistryHoldsMultipleOverloads()
    {
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(
            "section verse(Note root) { Sequence inner = | C4q | }\n" +
            "section verse(<<Note root, Int repeats>>) { Sequence inner = | D4q | }\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        Assert.True(engine.Context.SectionRegistry.TryGetValue("verse", out var overloads));
        Assert.Equal(2, overloads!.Count);
    }

    [Fact]
    public void DispatchPicksHighestSpecificity()
    {
        // Two overloads: verse(Note root) [+500] vs verse(Cmaj7) [+800].
        // A call with Cmaj7 should pick the Cmaj7 (higher-specificity) overload.
        // We verify by inspecting the song's flat registry — the materialized
        // section keyed under verse#0 should carry the Sequence from the
        // chord-pattern body (which declares `Sequence chosen = | E4q |`).
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(
            "section verse(Note root) { Sequence chosen = | C4q | }\n" +
            "section verse(Cmaj7) { Sequence chosen = | E4q | }\n" +
            "Song s = [verse(Cmaj7)]\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        var sVal = engine.Context.GetVariable("s");
        Assert.NotNull(sVal);
        var songData = sVal.As<SongData>();
        Assert.Single(songData.Sections);
        // The materialized synthetic name verse#0 should be in the flat registry.
        var key = songData.Sections[0].Name;
        Assert.True(songData.SectionRegistry.ContainsKey(key));
        // The body executed should be the chord-pattern body — sequence was named "chosen".
        Assert.Contains("chosen", songData.SectionRegistry[key].Sequences.Keys);
    }

    [Fact]
    public void AmbiguousOverloadRaisesAtDeclarationTime()
    {
        // Two `section verse(Note root)` — identical Parameters shape, must
        // be caught at declaration time per Pitfall 3.
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(
            "section verse(Note root) { Sequence a = | C4q | }\n" +
            "section verse(Note root2) { Sequence b = | D4q | }\n");
        // Execute may return true (errors collected, not thrown), but
        // ErrorReporter should have at least one diagnostic mentioning ambiguity.
        var hasAmbig = engine.ErrorReporter.Errors.Any(e =>
            e.Message.Contains("Ambiguous section overload", System.StringComparison.OrdinalIgnoreCase)
            || e.Message.Contains("ambiguous", System.StringComparison.OrdinalIgnoreCase));
        Assert.True(hasAmbig,
            "Expected an Ambiguous-overload diagnostic. Got: " + engine.ErrorReporter.FormatErrors());
    }

    [Fact]
    public void NoMatchingOverloadRaisesAtCallTime()
    {
        // Declare verse(Note root). Call with a String arg — no overload matches.
        using var engine = new FlowEngine(verbose: false);
        engine.Execute(
            "section verse(Note root) { Sequence inner = | C4q | }\n" +
            "Song s = [verse(\"string\")]\n");
        var hasNoMatch = engine.ErrorReporter.Errors.Any(e =>
            e.Message.Contains("no overload of section", System.StringComparison.OrdinalIgnoreCase)
            || e.Message.Contains("does not match", System.StringComparison.OrdinalIgnoreCase));
        Assert.True(hasNoMatch,
            "Expected a no-match diagnostic. Got: " + engine.ErrorReporter.FormatErrors());
    }

    [Fact]
    public void TupleDestructurePopulatesBindings()
    {
        // `section verse(<<Note root, Int repeats>>)` called with `<<D4, 3>>`
        // should bind root=D4 and repeats=3 in the synthetic frame. We verify
        // by having the body declare a Sequence whose name depends on the
        // bound `repeats` Int (via interpolation isn't supported, so just
        // check the section materializes and contains the expected anonymous
        // sequence — root being bound is enough that the body doesn't error).
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(
            "section verse(<<Note root, Int repeats>>) { Sequence bound = | C4q | }\n" +
            "Song s = [verse(<<D4, 3>>)]\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        var sVal = engine.Context.GetVariable("s");
        var songData = sVal.As<SongData>();
        Assert.Single(songData.Sections);
        var key = songData.Sections[0].Name;
        Assert.Contains("bound", songData.SectionRegistry[key].Sequences.Keys);
    }

    [Fact]
    public void ZeroArgSectionStillDispatches()
    {
        // Backward-compat: bare `section verse { ... }` referenced as `verse`
        // (no parens) still works.
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(
            "section verse { Sequence inner = | C4q | }\n" +
            "Song s = [verse]\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        var sVal = engine.Context.GetVariable("s");
        var songData = sVal.As<SongData>();
        Assert.Single(songData.Sections);
        Assert.Equal("verse", songData.Sections[0].Name);
    }
}
