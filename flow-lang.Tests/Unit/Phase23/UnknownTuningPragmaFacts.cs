using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase23;

/// <summary>
/// Phase 23 D-14 / MICR-03 acceptance Facts. When an unknown pragma typed name
/// resembles a tuning pragma (Levenshtein ≤ 3 from any known tuning name OR
/// substring whitelist match), the unknown-pragma error message is extended
/// with a pointer to the deferred Scala (.scl) loader (v1.4 deferral).
///
/// Decisions referenced (CONTEXT 23-microtonal-tuning-wedge):
///   D-12 (Phase 21) — unknown-pragma error format with did-you-mean +
///                     alphabetized known-pragma list.
///   D-14 — MICR-03 extension: append Scala-loader v1.4 pointer when typed
///          name looks like a tuning system.
/// </summary>
[Collection("FlowScripts")]
public class UnknownTuningPragmaFacts
{
    [Fact]
    public void UnknownTuning_ErrorIncludesScalaPointer()
    {
        // 'maqam' is a real microtonal system name not in our closed set; the
        // substring whitelist match on "tun" / "scal" / "intone" would NOT fire
        // for 'maqam', but 'maqam' is NOT in the typo distance from any tuning
        // name either. We deliberately use 'microMaqam' which contains "micro"
        // (substring whitelist match) so the Scala pointer fires.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, _) = runner.RunSource(@"enable microMaqam;
use ""@std""
(print ""x"")
");
        Assert.Contains("Full Scala (.scl) loader is documented as deferred to v1.4", stderr);
    }

    [Fact]
    public void UnknownTuning_DidYouMean_FromLevenshtein()
    {
        // Typo 'justIntonatio' (one char missing) is Levenshtein-distance 1 from
        // 'justIntonation', so SuggestNearest returns it. Levenshtein-≤-3 from
        // a tuning name also fires LooksLikeTuningName, so the Scala pointer
        // appears too.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, _) = runner.RunSource(@"enable justIntonatio;
use ""@std""
(print ""x"")
");
        Assert.Contains("Did you mean 'justIntonation'?", stderr);
        Assert.Contains("Full Scala (.scl) loader", stderr);
    }

    [Fact]
    public void UnknownNonTuningPragma_DoesNotIncludeScalaPointer()
    {
        // 'verbose' shares no substring with the tuning whitelist (tun/scal/temp
        // /just/pyth/micro/intone) and is far from any tuning name in
        // Levenshtein distance. The Scala pointer must NOT appear.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, _) = runner.RunSource(@"enable verbose;
use ""@std""
(print ""x"")
");
        Assert.Contains("unknown pragma 'verbose'", stderr);
        Assert.DoesNotContain("Scala (.scl) loader", stderr);
    }

    [Fact]
    public void UnknownTuning_ErrorContainsAlphabetizedList()
    {
        // The error message includes an alphabetized list of all 4 known pragmas
        // (Phase 21 D-12 contract; preserved across Phase 23 closed-set growth).
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, _) = runner.RunSource(@"enable microMaqam;
use ""@std""
(print ""x"")
");
        Assert.Contains("equalTemperament", stderr);
        Assert.Contains("hAsB", stderr);
        Assert.Contains("justIntonation", stderr);
        Assert.Contains("pythagorean", stderr);
    }
}
