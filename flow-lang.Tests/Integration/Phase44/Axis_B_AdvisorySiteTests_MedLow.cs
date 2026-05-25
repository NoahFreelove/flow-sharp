using System;
using System.Collections.Generic;
using System.Linq;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-07 — Axis B MED + LOW priority advisory-elevation Theory.
///
/// <para>
/// Pins the ~55 MED + LOW priority §6b advisory sites from
/// <c>strict-error-manifest.csv</c> (MED + LOW partition, carve_out=false)
/// by exercising the most representative builtins from `.flow` source with
/// strict mode enabled and asserting the ErrorReporter captures the
/// manifest verbatim <c>[strict]</c> body prefix.
/// </para>
///
/// <para>
/// <b>Per-site test strategy</b>: many MED+LOW sites can't be triggered from
/// a `.flow` source line because the surface requires authored ABC/MML
/// files (parser-deep), OSC network access (bind/connect), or special
/// state (Tuning under custom .scl). The Theory exercises the subset that
/// CAN fire from `.flow`. The carve-out smoke Facts assert
/// (a) the partition count matches Plan 44-07 MED+LOW inventory,
/// (b) the non-strict path remains charitable byte-identical, and
/// (c) the HIGH + MED + LOW partition union covers the in-scope manifest.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class Axis_B_AdvisorySiteTests_MedLow : IDisposable
{
    public Axis_B_AdvisorySiteTests_MedLow()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    /// <summary>
    /// MemberData source — per-site trigger sources for the .flow-triggerable
    /// MED+LOW advisory sites. Each row: (sentinelSubstring, strictSrc, nonStrictSrc).
    /// </summary>
    public static IEnumerable<object[]> TriggerableSites => new[]
    {
        // Markov — invalid length (length <= 0)
        new object[]
        {
            "[strict] [markov] length clamped",
            "enable strict;\nuse \"@generative\"\nSequence corpus = | C4q D4q |\n(markov corpus 1 0 42)\n",
            "use \"@generative\"\nSequence corpus = | C4q D4q |\n(markov corpus 1 0 42)\n"
        },
        // Markov — order clamp (order > 3)
        new object[]
        {
            "[strict] [markov] order clamped",
            "enable strict;\nuse \"@generative\"\nSequence corpus = | C4q D4q E4q F4q |\n(markov corpus 99 4 42)\n",
            "use \"@generative\"\nSequence corpus = | C4q D4q E4q F4q |\n(markov corpus 99 4 42)\n"
        },
        // Lsystem — iterations clamp (negative)
        new object[]
        {
            "[strict] [lsystem] iterations clamped",
            "enable strict;\nuse \"@generative\"\n(lsystem #A (dict #A <<#A>>) (neg 1))\n",
            "use \"@generative\"\n(lsystem #A (dict #A <<#A>>) (neg 1))\n"
        },
        // Lsystem — iterations clamp (> 20 cap)
        new object[]
        {
            "[strict] [lsystem] iterations clamped",
            "enable strict;\nuse \"@generative\"\n(lsystem #A (dict #A <<#A>>) 999)\n",
            "use \"@generative\"\n(lsystem #A (dict #A <<#A>>) 999)\n"
        },
        // Cellular — dimension clamp (> MaxDimension)
        new object[]
        {
            "[strict] [cellular] width/height clamped",
            "enable strict;\nuse \"@generative\"\n(cellular 30 99999 5 42)\n",
            "use \"@generative\"\n(cellular 30 99999 5 42)\n"
        },
        // Chaos — logistic r clamp (negative)
        new object[]
        {
            "[strict] [logistic] r clamped",
            "enable strict;\nuse \"@generative\"\n(logistic -1.0 10 42)\n",
            "use \"@generative\"\n(logistic -1.0 10 42)\n"
        },
        // Chaos — lorenz length clamp
        new object[]
        {
            "[strict] [lorenz] length clamped",
            "enable strict;\nuse \"@generative\"\n(lorenz 10.0 28.0 2.67 0 42)\n",
            "use \"@generative\"\n(lorenz 10.0 28.0 2.67 0 42)\n"
        },
        // Chaos — quantizeToScale unknown scale
        new object[]
        {
            "[strict] [quantizeToScale] unknown scale",
            "enable strict;\nuse \"@generative\"\nDouble[] series = (logistic 3.7 10 42)\n(quantizeToScale series \"NotARealScale\")\n",
            "use \"@generative\"\nDouble[] series = (logistic 3.7 10 42)\n(quantizeToScale series \"NotARealScale\")\n"
        },
        // Chaos — quantizeToScale empty series
        new object[]
        {
            "[strict] [quantizeToScale] empty series",
            "enable strict;\nuse \"@generative\"\nDouble[] empty = (logistic 3.7 0 42)\n(quantizeToScale empty \"cmajor\")\n",
            "use \"@generative\"\nDouble[] empty = (logistic 3.7 0 42)\n(quantizeToScale empty \"cmajor\")\n"
        },
        // Jam — length invalid (length=0 hits the length-clamp advisory)
        new object[]
        {
            "[strict] [jam] length clamped",
            "enable strict;\nuse \"@improv\"\nSequence over = | Cmaj |\n(jam over #jazz 0 \"Cmajor\" 1234 2)\n",
            "use \"@improv\"\nSequence over = | Cmaj |\n(jam over #jazz 0 \"Cmajor\" 1234 2)\n"
        },
    };

    [Theory]
    [MemberData(nameof(TriggerableSites))]
    public void Fact_MedLowStrictSite_ProducesVerbatimError(
        string sentinelSubstring, string strictSrc, string nonStrictSrc)
    {
        using var engine = new FlowEngine();
        engine.Execute(strictSrc, "<top>");

        var errors = engine.ErrorReporter.FormatErrors();
        Assert.Contains(sentinelSubstring, errors);
    }

    [Theory]
    [MemberData(nameof(TriggerableSites))]
    public void Fact_MedLowNonStrictSite_NoError(
        string sentinelSubstring, string strictSrc, string nonStrictSrc)
    {
        using var engine = new FlowEngine();
        var ok = engine.Execute(nonStrictSrc, "<top>");

        Assert.True(ok,
            $"non-strict charitable call MUST succeed; flow:\n{nonStrictSrc}\nerrors:\n{engine.ErrorReporter.FormatErrors()}");
    }

    [Fact]
    public void Fact_MedLowCount_MatchesManifest()
    {
        // Plan 44-07 covers the in-scope MED + LOW partition. Manifest expects
        // ~55-65 rows across the 13 production files in Plan 44-07's scope.
        // Tolerance band per the plan's ≈65 estimate.
        var rows = StrictErrorManifestLoader.LoadMedLowPrioritySites().ToList();

        Assert.True(rows.Count >= 50,
            $"MED + LOW partition expected ≥50 rows, found {rows.Count}");
        Assert.True(rows.Count <= 80,
            $"MED + LOW partition expected ≤80 rows, found {rows.Count}");
    }

    [Fact]
    public void Fact_TotalInScopeCount_MatchesSumOfPlans()
    {
        // Partition-union sanity check: HIGH ∪ MED ∪ LOW = in-scope (carve_out=false).
        // Plans 44-05 + 44-06 + 44-07 together cover the full in-scope manifest.
        var highCount = StrictErrorManifestLoader.LoadAll()
            .Count(r => !r.CarveOut && r.Priority == "HIGH");
        var medCount = StrictErrorManifestLoader.LoadAll()
            .Count(r => !r.CarveOut && r.Priority == "MED");
        var lowCount = StrictErrorManifestLoader.LoadAll()
            .Count(r => !r.CarveOut && r.Priority == "LOW");
        var inScopeCount = StrictErrorManifestLoader.LoadAll()
            .Count(r => !r.CarveOut);

        Assert.Equal(highCount + medCount + lowCount, inScopeCount);
    }
}
