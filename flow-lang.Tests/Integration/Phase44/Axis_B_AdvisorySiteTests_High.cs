using System;
using System.Collections.Generic;
using System.Linq;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-06 — Axis B HIGH-priority advisory-elevation Theory.
///
/// <para>
/// Pins the ~39 HIGH-priority §6b advisory sites from
/// <c>strict-error-manifest.csv</c> (HIGH partition, carve_out=false,
/// non-§6a) by exercising the most representative builtins from `.flow`
/// source with strict mode enabled and asserting the ErrorReporter captures
/// the manifest verbatim `[strict] ` body prefix.
/// </para>
///
/// <para>
/// <b>Per-site test strategy</b>: many HIGH-priority sites (SFZ parser
/// errors, SFZ renderer missing-region paths, etc.) cannot be triggered
/// from a `.flow` source line because the surface requires authored .sfz
/// files. The Theory exercises the subset that CAN fire from `.flow` —
/// Patterns, DSP, Match — and the carve-out smoke Facts assert
/// (a) the partition count matches Plan 44-06 HIGH inventory and
/// (b) the non-strict path remains charitable.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class Axis_B_AdvisorySiteTests_High : IDisposable
{
    public Axis_B_AdvisorySiteTests_High()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    /// <summary>
    /// MemberData source — per-site trigger sources for the .flow-triggerable
    /// HIGH advisory sites. Each row: (sentinelPrefix, strictSrc, nonStrictSrc).
    /// </summary>
    public static IEnumerable<object[]> TriggerableSites => new[]
    {
        // Patterns — fast factor &lt;= 0
        new object[]
        {
            "[strict] [fast] factor must be > 0 and finite (got 0)",
            "enable strict;\nuse \"@patterns\"\nSequence s = | C4q D4q |\n(fast s 0.0)\n",
            "use \"@patterns\"\nSequence s = | C4q D4q |\n(fast s 0.0)\n"
        },
        // Patterns — slow factor &lt;= 0
        new object[]
        {
            "[strict] [slow] factor must be > 0 and finite (got 0)",
            "enable strict;\nuse \"@patterns\"\nSequence s = | C4q D4q |\n(slow s 0.0)\n",
            "use \"@patterns\"\nSequence s = | C4q D4q |\n(slow s 0.0)\n"
        },
        // Patterns — chunk n &lt;= 0
        new object[]
        {
            "[strict] [chunk] n must be > 0 (got 0)",
            "enable strict;\nuse \"@patterns\"\nSequence s = | C4q D4q |\n(chunk 0 (fn Sequence seq => seq) s)\n",
            "use \"@patterns\"\nSequence s = | C4q D4q |\n(chunk 0 (fn Sequence seq => seq) s)\n"
        },
        // Patterns — iter n &lt;= 0
        new object[]
        {
            "[strict] [iter] n must be > 0 (got 0)",
            "enable strict;\nuse \"@patterns\"\nSequence s = | C4q D4q |\n(iter 0 s)\n",
            "use \"@patterns\"\nSequence s = | C4q D4q |\n(iter 0 s)\n"
        },
        // Patterns — sometimes prob outside range
        new object[]
        {
            "[strict] [sometimes] probability 2 outside [0.0, 1.0]",
            "enable strict;\nuse \"@patterns\"\nSequence s = | C4q D4q |\n(sometimes 2.0 (fn Sequence seq => seq) s)\n",
            "use \"@patterns\"\nSequence s = | C4q D4q |\n(sometimes 2.0 (fn Sequence seq => seq) s)\n"
        },
        // Patterns — sparseSeq prob outside range
        new object[]
        {
            "[strict] [sparseSeq] probability 2 outside [0.0, 1.0]",
            "enable strict;\nuse \"@patterns\"\nSequence s = | C4q D4q |\n(sparseSeq 2.0 s)\n",
            "use \"@patterns\"\nSequence s = | C4q D4q |\n(sparseSeq 2.0 s)\n"
        },
        // DSP — granular unknown windowing symbol
        new object[]
        {
            "[strict] [granular] unknown windowing symbol '#unknown'",
            "enable strict;\nuse \"@audio\"\nBuffer b = (createSineTone 0.5 440.0 0.5)\n(granular b 50ms 20Hz 0.3 #unknown)\n",
            "use \"@audio\"\nBuffer b = (createSineTone 0.5 440.0 0.5)\n(granular b 50ms 20Hz 0.3 #unknown)\n"
        },
        // DSP — pitchShift unknown mode symbol
        new object[]
        {
            "[strict] [pitchShift] unknown mode symbol '#unknown'",
            "enable strict;\nuse \"@audio\"\nBuffer b = (createSineTone 0.5 440.0 0.5)\n(pitchShift b +2st #unknown)\n",
            "use \"@audio\"\nBuffer b = (createSineTone 0.5 440.0 0.5)\n(pitchShift b +2st #unknown)\n"
        },
        // DSP — stretch unknown mode symbol
        new object[]
        {
            "[strict] [stretch] unknown mode symbol '#unknown'",
            "enable strict;\nuse \"@audio\"\nBuffer b = (createSineTone 0.5 440.0 0.5)\n(stretch b 2.0 #unknown)\n",
            "use \"@audio\"\nBuffer b = (createSineTone 0.5 440.0 0.5)\n(stretch b 2.0 #unknown)\n"
        },
    };

    [Theory]
    [MemberData(nameof(TriggerableSites))]
    public void Fact_StrictAdvisorySite_ProducesVerbatimError(
        string sentinelPrefix, string strictSrc, string nonStrictSrc)
    {
        using var engine = new FlowEngine();
        engine.Execute(strictSrc, "<top>");

        var errors = engine.ErrorReporter.FormatErrors();
        Assert.Contains(sentinelPrefix, errors);
    }

    [Theory]
    [MemberData(nameof(TriggerableSites))]
    public void Fact_NonStrictAdvisorySite_NoError(
        string sentinelPrefix, string strictSrc, string nonStrictSrc)
    {
        using var engine = new FlowEngine();
        var ok = engine.Execute(nonStrictSrc, "<top>");

        Assert.True(ok,
            $"non-strict charitable call MUST succeed; flow:\n{nonStrictSrc}\nerrors:\n{engine.ErrorReporter.FormatErrors()}");
    }

    [Fact]
    public void Fact_HighSiteCount_MatchesManifestPartition()
    {
        // Plan 44-06 + 44-05 cover the entire HIGH partition (52 rows in the
        // manifest at this commit: 13 §6a TransformFunctions clamps elevated
        // by Plan 44-05 + ~39 §6b advisory sites elevated by Plan 44-06).
        var highRows = StrictErrorManifestLoader.LoadAll()
            .Where(r => !r.CarveOut && r.Priority == "HIGH")
            .ToList();

        Assert.True(highRows.Count >= 50,
            $"HIGH partition expected ≥50 rows, found {highRows.Count}");

        // §6a partition (TransformFunctions.cs) accounts for 13 input-perimeter
        // clamp sites — Plan 44-05 scope. The remaining HIGH rows are §6b
        // advisory sites this plan (44-06) elevates.
        var sixA = highRows.Where(r => !string.IsNullOrEmpty(r.Param)).ToList();
        Assert.Equal(13, sixA.Count);

        var sixB = highRows.Where(r => string.IsNullOrEmpty(r.Param)).ToList();
        Assert.True(sixB.Count >= 36,
            $"§6b HIGH partition expected ≥36 rows, found {sixB.Count}");
    }

    [Fact]
    public void Fact_StrictMatchNonExhaustive_ProducesVerbatimError()
    {
        // Phase 35 D-v1.5-11 + Plan 44-06 — match expression with no matching
        // arm falls through. Under strict mode the existing WarnOnce advisory
        // becomes a [strict] error via ErrorReporter.
        string src = "enable strict;\nInt x = 42;\n(match x | 1 => 1 | 2 => 2)\n";

        using var engine = new FlowEngine();
        engine.Execute(src, "<top>");

        var errors = engine.ErrorReporter.FormatErrors();
        Assert.Contains("[strict] [match] non-exhaustive pattern", errors);
    }

    [Fact]
    public void Fact_NonStrictMatchNonExhaustive_NoError()
    {
        // Mirror of Fact_StrictMatchNonExhaustive — charitable path preserved.
        string src = "Int x = 42;\n(match x | 1 => 1 | 2 => 2)\n";

        using var engine = new FlowEngine();
        var ok = engine.Execute(src, "<top>");

        Assert.True(ok,
            $"non-strict match non-exhaustive must succeed: {engine.ErrorReporter.FormatErrors()}");
    }
}
