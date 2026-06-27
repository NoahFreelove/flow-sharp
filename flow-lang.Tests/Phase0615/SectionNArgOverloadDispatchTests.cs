using System.Linq;
using FlowLang.Core;
using FlowLang.Runtime;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase0615;

/// <summary>
/// Feature-addition 0615 (#4 section-overload-dispatch) — N-arg overload
/// dispatch by arity + type, mirroring OverloadResolver.
///
/// <para>
/// Regression for: with BOTH <c>section verse(Note root)</c> and
/// <c>section verse(Note root, Int n)</c> registered, <c>Song s = [verse(C4, 3)]</c>
/// errored "section verse expects 1 arguments, got 2 positional" — the
/// dispatcher's per-candidate <c>BuildFinalArgs</c> emitted a HARD error when a
/// candidate's arity did not match, instead of silently disqualifying that
/// candidate and letting the arity-correct sibling overload win.
/// </para>
///
/// <para>
/// The fix makes every per-candidate binding failure (too many positionals,
/// unknown named slot, named/positional collision, missing default) a SILENT
/// disqualification; the aggregate "no overload matches" diagnostic in
/// <see cref="FlowLang.Interpreter.SectionOverloadDispatch.Resolve"/> fires only
/// when EVERY candidate fails.
/// </para>
/// </summary>
public class SectionNArgOverloadDispatchTests
{
    // Each overload's body declares a uniquely-named Sequence so we can prove
    // which one materialized by inspecting the flat registry's Sequences keys.
    private const string TwoArities =
        "section verse(Note root) { Sequence oneArg = | C4q | }\n" +
        "section verse(Note root, Int n) { Sequence twoArg = | D4q | }\n";

    [Fact]
    public void OneArgCallSelectsOneArgOverload()
    {
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(TwoArities + "Song s = [verse(C4)]\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());

        var songData = engine.Context.GetVariable("s")!.As<SongData>();
        Assert.Single(songData.Sections);
        var key = songData.Sections[0].Name;
        Assert.Contains("oneArg", songData.SectionRegistry[key].Sequences.Keys);
        Assert.DoesNotContain("twoArg", songData.SectionRegistry[key].Sequences.Keys);
    }

    [Fact]
    public void TwoArgCallSelectsTwoArgOverload_NoSpuriousArityError()
    {
        // This is the exact reproduction: before the fix it errored
        // "section verse expects 1 arguments, got 2 positional".
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(TwoArities + "Song s = [verse(C4, 3)]\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());

        // No "expects N arguments, got M positional" diagnostic should survive.
        Assert.DoesNotContain(engine.ErrorReporter.Errors, e =>
            e.Message.Contains("expects", System.StringComparison.OrdinalIgnoreCase)
            && e.Message.Contains("positional", System.StringComparison.OrdinalIgnoreCase));

        var songData = engine.Context.GetVariable("s")!.As<SongData>();
        Assert.Single(songData.Sections);
        var key = songData.Sections[0].Name;
        Assert.Contains("twoArg", songData.SectionRegistry[key].Sequences.Keys);
        Assert.DoesNotContain("oneArg", songData.SectionRegistry[key].Sequences.Keys);
    }

    [Fact]
    public void RepetitionOperatorComposesWithOneArgCall()
    {
        // verse(C4)*2 — D-36-14 repetition operator on a parameterized call.
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(TwoArities + "Song s = [verse(C4)*2]\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());

        var songData = engine.Context.GetVariable("s")!.As<SongData>();
        // *2 is stored as a single section ref carrying RepeatCount=2 (the
        // arrangement renderer expands it at render time).
        Assert.Single(songData.Sections);
        Assert.Equal(2, songData.Sections[0].RepeatCount);
        // The single 1-arg overload must have been materialized for the call.
        Assert.Contains("oneArg", songData.SectionRegistry[songData.Sections[0].Name].Sequences.Keys);
    }

    [Fact]
    public void BothAritiesInOneSong()
    {
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(TwoArities + "Song s = [verse(C4) verse(C4, 3)]\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());

        var songData = engine.Context.GetVariable("s")!.As<SongData>();
        Assert.Equal(2, songData.Sections.Count);
        var keys = songData.Sections.Select(s => s.Name).ToList();
        Assert.Contains("oneArg", songData.SectionRegistry[keys[0]].Sequences.Keys);
        Assert.Contains("twoArg", songData.SectionRegistry[keys[1]].Sequences.Keys);
    }

    [Fact]
    public void ThreeArgCallWithNoMatchStillRaisesAggregateDiagnostic()
    {
        // Charitable-but-honest: when NO overload's arity fits, the aggregate
        // "no overload matches" diagnostic must still fire (the per-candidate
        // silence must not swallow genuinely-unmatchable calls).
        using var engine = new FlowEngine(verbose: false);
        engine.Execute(TwoArities + "Song s = [verse(C4, 3, 9)]\n");
        Assert.Contains(engine.ErrorReporter.Errors, e =>
            e.Message.Contains("no overload of section", System.StringComparison.OrdinalIgnoreCase)
            || e.Message.Contains("does not match", System.StringComparison.OrdinalIgnoreCase));
    }
}
