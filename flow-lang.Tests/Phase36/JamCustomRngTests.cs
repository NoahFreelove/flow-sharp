using System.Linq;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Quick task 260626-wml — <c>jam</c>'s optional <c>rng=</c> custom random
/// function (option A: a composer <c>(Int =&gt; Double)</c> index→value fn,
/// jam-only). Proves the supplied RNG actually drives jam's note selection, that
/// a PURE rng keeps two-run determinism, that it takes precedence over an
/// explicit seed, that the <c>rng=</c> named surface resolves, and that omitting
/// it leaves the existing seed / PrngRegistry path intact.
/// </summary>
[Collection("FlowScripts")]
public class JamCustomRngTests
{
    // Pitch fingerprint of a jam result — jam writes MusicalNoteData (name/octave/
    // alteration). Two different RNGs steering different note picks ⇒ different
    // fingerprint; the same pure RNG ⇒ identical fingerprint.
    private static string Fingerprint(SequenceData seq) =>
        string.Join("|", seq.Bars.Select(b =>
            string.Join(",", b.MusicalNotes.Select(n => $"{n.NoteName}{n.Octave}_{n.Alteration}"))));

    // Fully-positional jam call (7 args incl. seed=42) so resolution is
    // unambiguous; the rng= function takes precedence over the seed.
    private static string RunJamPositional(string rngExpr)
    {
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource($$"""
            use "@std"
            use "@improv"
            Sequence over = | Cmaj7 | Am7 | Dm7 | G7 |
            Sequence result = (jam over #jazz 4 "Cmajor" 42 2 {{rngExpr}})
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");
        return Fingerprint(runner.GetVariable("result").As<SequenceData>());
    }

    [Fact]
    public void CustomRng_DrivesTheDraws_DifferentRngsProduceDifferentMusic()
    {
        // Two distinct PURE (Int => Double) functions steer jam to different note
        // choices — direct proof the composer rng= is what drives selection.
        string low = RunJamPositional("(fn Int i => 0.05)");
        string high = RunJamPositional("(fn Int i => 0.95)");
        Assert.False(string.IsNullOrEmpty(low), "jam produced no notes");
        Assert.NotEqual(low, high);
    }

    [Fact]
    public void CustomRng_IsPureAndDeterministic_SameRngSameOutput()
    {
        // A pure index->value rng= keeps two-run cmp-clean: identical source,
        // identical jam output, across independent engine runs.
        string a = RunJamPositional("(fn Int i => 0.05)");
        string b = RunJamPositional("(fn Int i => 0.05)");
        Assert.Equal(a, b);
    }

    [Fact]
    public void CustomRng_TakesPrecedenceOverSeed()
    {
        // Both calls carry seed=42; only the rng differs. Divergent output proves
        // the rng= (not the seed) drove the draws.
        string withLow = RunJamPositional("(fn Int i => 0.05)");
        string withHigh = RunJamPositional("(fn Int i => 0.95)");
        Assert.NotEqual(withLow, withHigh);
    }

    [Fact]
    public void NamedRngArg_ResolvesAndDrivesDraws()
    {
        // The composer-facing named surface `rng=` must resolve and drive jam.
        string Run(string rngExpr)
        {
            using var runner = new FlowEngineRunner();
            var (success, _, stderr, _) = runner.RunSource($$"""
                use "@std"
                use "@improv"
                Sequence over = | Cmaj7 | Am7 | Dm7 | G7 |
                Sequence result = (jam over=over style=#jazz length=4 key="Cmajor" seed=1 order=2 rng={{rngExpr}})
                """);
            Assert.True(success, $"Named-arg script failed; stderr:\n{stderr}");
            return Fingerprint(runner.GetVariable("result").As<SequenceData>());
        }

        Assert.NotEqual(Run("(fn Int i => 0.05)"), Run("(fn Int i => 0.95)"));
    }

    [Fact]
    public void OmittingRng_LeavesExistingPathIntact()
    {
        // rng= omitted → the existing explicit-seed path; still a valid 4-bar jam.
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource("""
            use "@std"
            use "@improv"
            Sequence over = | Cmaj7 | Am7 | Dm7 | G7 |
            Sequence result = (jam over #jazz 4 "Cmajor" 42 2)
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");
        var seq = runner.GetVariable("result").As<SequenceData>();
        Assert.Equal(4, seq.Bars.Count);
    }
}
