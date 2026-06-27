using System;
using System.Collections.Generic;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase32;

/// <summary>
/// Phase 32 Plan 03 Task 1 — Facts pinning the math at the
/// <see cref="ResolvedTuning"/> construction layer + <see cref="TuningType"/>
/// type-system registration. Scale-step semantics per the plan's
/// <c>&lt;algorithm_semantics&gt;</c> block: the cross-fixture anchor is
/// <c>MidiToHz[kbm.ReferenceNote] == kbm.ReferenceHz</c> EXACTLY by construction
/// (default KBM: <c>MidiToHz[69] ≈ 440.0</c> for every non-12-TET fixture). The
/// 12-TET-derived <c>MidiToHz[60] ≈ 261.6256</c> value is NEVER asserted under a
/// non-12-TET fixture — that number is the 12-TET answer (440 / 2^(9/12)), wrong
/// by construction for a scale-step tuning.
///
/// Per-fixture invariants are pinned as RATIOS (e.g. partch_43 step 1 = 81/80,
/// step 2 = 33/32). Carlos Alpha's headline non-octave wrap (period 1404¢,
/// 18 steps per period) lands within ±0.1¢ of the SPEC-5 acceptance condition.
/// </summary>
public class TuningTypeFacts
{
    // ---- Helpers that construct synthetic ParsedScala values via the public ctor.
    // Real fixtures (Plan 32-01) would land via ScalaParser.Parse, which Plan 32-02
    // ships in parallel. These helpers replicate the Huygens-Fokker archive contents
    // verbatim so the ResolvedTuning math is exercised end-to-end without a parser
    // dependency.

    /// <summary>
    /// Huygens-Fokker partch_43.scl content as a ParsedScala value. 43 ratio steps;
    /// final step 2/1 = 1200¢ period. First ratio 81/80 (≈21.506¢); second ratio
    /// 33/32 (≈53.273¢). Sourced from the in-repo fixture
    /// <c>flow-lang.Tests/fixtures/scala/partch_43.scl</c>.
    /// </summary>
    private static ParsedScala MakePartch43()
    {
        var ratioList = new (int Num, int Den)[]
        {
            (81, 80), (33, 32), (21, 20), (16, 15), (12, 11), (11, 10), (10, 9),
            (9, 8), (8, 7), (7, 6), (32, 27), (6, 5), (11, 9), (5, 4), (14, 11),
            (9, 7), (21, 16), (4, 3), (27, 20), (11, 8), (7, 5), (10, 7), (16, 11),
            (40, 27), (3, 2), (32, 21), (14, 9), (11, 7), (8, 5), (18, 11), (5, 3),
            (27, 16), (12, 7), (7, 4), (16, 9), (9, 5), (20, 11), (11, 6), (15, 8),
            (40, 21), (64, 33), (160, 81), (2, 1)
        };
        return MakeRatioOnly("Harry Partch's 43-tone pure scale", ratioList);
    }

    /// <summary>
    /// Huygens-Fokker carlos_alpha.scl content. 18 cents-only steps; final entry
    /// 1404.00¢ — the non-octave period. SPEC-5 acceptance fixture.
    /// </summary>
    private static ParsedScala MakeCarlosAlpha()
    {
        // Carlos Alpha: 18 equal steps of period 1404 / 18 = 78¢ each.
        // The .scl file lists cents values 78.00, 156.00, 234.00, ..., 1404.00.
        var cents = new double[18];
        for (int i = 0; i < 18; i++) cents[i] = 78.0 * (i + 1);
        // Description matches the archive file's first non-comment line.
        return new ParsedScala(
            Description: "Wendy Carlos' Alpha scale with perfect fifth divided in nine",
            StepCents: cents[..^1],
            PeriodCents: cents[^1],
            Ratios: new Dictionary<int, (int Num, int Den)>(),
            FilePath: "synthetic:carlos_alpha.scl");
    }

    /// <summary>
    /// Slendro: 5-step mixed-cents-and-ratio scale; final step 2/1 = 1200¢ period.
    /// First step is 228¢ per the archive file. Used for the cross-fixture anchor
    /// invariant (MidiToHz[69] ≈ 440 under default KBM).
    /// </summary>
    private static ParsedScala MakeSlendro()
    {
        var cents = new double[] { 228.0, 462.0, 708.0, 942.0, 1200.0 };
        var ratios = new Dictionary<int, (int Num, int Den)> { [4] = (2, 1) };
        return new ParsedScala(
            Description: "Observed Javanese Slendro scale",
            StepCents: cents[..^1],
            PeriodCents: cents[^1],
            Ratios: ratios,
            FilePath: "synthetic:slendro.scl");
    }

    /// <summary>
    /// Synthetic helper — builds a ratio-only ParsedScala from an N-entry list where
    /// the LAST entry is the period. Mirrors the ScalaParser's StepCents-of-length-N-1
    /// + PeriodCents extraction per CONTEXT D-10.
    /// </summary>
    private static ParsedScala MakeRatioOnly(string description, (int Num, int Den)[] ratios)
    {
        int n = ratios.Length;
        var cents = new double[n];
        var ratioDict = new Dictionary<int, (int Num, int Den)>();
        for (int i = 0; i < n; i++)
        {
            cents[i] = 1200.0 * Math.Log2((double)ratios[i].Num / ratios[i].Den);
            ratioDict[i] = ratios[i];
        }
        return new ParsedScala(
            Description: description,
            StepCents: cents[..^1],
            PeriodCents: cents[^1],
            Ratios: ratioDict,
            FilePath: "synthetic:" + description.Substring(0, Math.Min(16, description.Length)));
    }

    /// <summary>
    /// Default KBM (linear mapping, middleNote=60, refNote=69, refHz=440.0) auto-adopting
    /// the loaded tuning's period per D-07. Synthetic equivalent of
    /// <c>ScalaKbmParser.Default(scl)</c>.
    /// </summary>
    private static ScalaKbm DefaultKbm(ParsedScala scl) =>
        new ScalaKbm(
            size: 0, firstMidi: 0, lastMidi: 127, middleNote: 60, referenceNote: 69,
            referenceHz: 440.0, formalOctave: 0, mapping: Array.Empty<int?>(),
            period: scl.PeriodCents);

    // ---- TuningType type-system registration Facts ----

    [Fact]
    public void TuningType_Instance_IsSingleton()
    {
        Assert.Same(TuningType.Instance, TuningType.Instance);
    }

    [Fact]
    public void TuningType_Name_IsTuning()
    {
        Assert.Equal("Tuning", TuningType.Instance.Name);
    }

    [Fact]
    public void TuningType_Specificity_Is137()
    {
        Assert.Equal(137, TuningType.Instance.GetSpecificity());
    }

    [Fact]
    public void TuningType_IsCompatibleWithSelf()
    {
        Assert.True(TuningType.Instance.IsCompatibleWith(TuningType.Instance));
    }

    [Fact]
    public void TuningType_NotCompatibleWithSong()
    {
        Assert.False(TuningType.Instance.IsCompatibleWith(SongType.Instance));
    }

    // ---- ResolvedTuning construction + Description Facts ----

    [Fact]
    public void ResolvedTuning_Partch43_Describes()
    {
        var scl = MakePartch43();
        var tuning = new ResolvedTuning(scl, DefaultKbm(scl));
        Assert.Equal("Harry Partch's 43-tone pure scale", tuning.Description);
        Assert.Equal(128, tuning.MidiToHz.Count);
    }

    // ---- Cross-fixture anchor invariants ----
    // MidiToHz[kbm.ReferenceNote] == kbm.ReferenceHz EXACTLY by construction. This is
    // the SAME number (440.0) for EVERY non-12-TET fixture loaded with the default KBM.

    [Fact]
    public void ResolvedTuning_Partch43_MidiToHz69_AnchorsAt440()
    {
        var scl = MakePartch43();
        var tuning = new ResolvedTuning(scl, DefaultKbm(scl));
        Assert.Equal(440.0, tuning.MidiToHz[69], precision: 6);
    }

    [Fact]
    public void ResolvedTuning_Slendro_MidiToHz69_AnchorsAt440()
    {
        var scl = MakeSlendro();
        var tuning = new ResolvedTuning(scl, DefaultKbm(scl));
        Assert.Equal(440.0, tuning.MidiToHz[69], precision: 6);
    }

    [Fact]
    public void ResolvedTuning_CarlosAlpha_MidiToHz69_AnchorsAt440()
    {
        var scl = MakeCarlosAlpha();
        var tuning = new ResolvedTuning(scl, DefaultKbm(scl));
        Assert.Equal(440.0, tuning.MidiToHz[69], precision: 6);
    }

    // ---- Per-step ratio Facts (internal-consistency invariants) ----

    [Fact]
    public void ResolvedTuning_Partch43_FirstStepRatio_Is81Over80()
    {
        var scl = MakePartch43();
        var tuning = new ResolvedTuning(scl, DefaultKbm(scl));
        // MidiToHz[middleNote+1] / MidiToHz[middleNote] == 81/80 EXACTLY (up to FP
        // rounding) because Partch step 1 is the literal ratio 81/80.
        double ratio = tuning.MidiToHz[61] / tuning.MidiToHz[60];
        Assert.Equal(81.0 / 80.0, ratio, precision: 9);
    }

    [Fact]
    public void ResolvedTuning_Partch43_SecondStepRatio_Is33Over32()
    {
        var scl = MakePartch43();
        var tuning = new ResolvedTuning(scl, DefaultKbm(scl));
        double ratio = tuning.MidiToHz[62] / tuning.MidiToHz[60];
        Assert.Equal(33.0 / 32.0, ratio, precision: 9);
    }

    // ---- SPEC-5 acceptance: non-octave period ----

    [Fact]
    public void ResolvedTuning_CarlosAlpha_NonOctaveWrap()
    {
        // The 18th step (period) of Carlos Alpha spans 1404¢, NOT 1200¢.
        // MidiToHz[middleNote+18] / MidiToHz[middleNote] should be 2^(1404/1200) ≈ 3.2003.
        var scl = MakeCarlosAlpha();
        var tuning = new ResolvedTuning(scl, DefaultKbm(scl));
        double observed = tuning.MidiToHz[60 + 18] / tuning.MidiToHz[60];
        double expected = Math.Pow(2.0, 1404.0 / 1200.0);
        // Convert tolerance to cents: |20.0 * log10(observed/expected)|? Actually use
        // |1200 * log2(observed/expected)| ≤ 0.1.
        double centsError = Math.Abs(1200.0 * Math.Log2(observed / expected));
        Assert.True(centsError < 0.1,
            $"carlos_alpha non-octave wrap exceeded ±0.1¢: observed={observed:R} expected={expected:R} centsError={centsError}");
    }

    // ---- Negative-cents semantics (D-09) ----

    [Fact]
    public void ResolvedTuning_NegativeCents_ProducesDescendingPitch()
    {
        // Synthetic 2-step scale: step 1 = -100¢ (descending), step 2 = 1200¢ (period).
        var scl = new ParsedScala(
            Description: "synthetic descending",
            StepCents: new[] { -100.0 },
            PeriodCents: 1200.0,
            Ratios: new Dictionary<int, (int Num, int Den)>(),
            FilePath: "synthetic:negative.scl");
        var tuning = new ResolvedTuning(scl, DefaultKbm(scl));
        Assert.True(tuning.MidiToHz[61] < tuning.MidiToHz[60],
            $"Negative step cents must produce descending pitch: MidiToHz[61]={tuning.MidiToHz[61]} >= MidiToHz[60]={tuning.MidiToHz[60]}");
    }

    // ---- ToString format (D-04) ----

    [Fact]
    public void ResolvedTuning_ToString_Format()
    {
        var scl = MakePartch43();
        var tuning = new ResolvedTuning(scl, DefaultKbm(scl));
        // D-04 format: Tuning("<description>", N steps, period XXX.XX¢)
        // N == StepCents.Count + 1 == 43 for partch.
        Assert.Equal(
            "Tuning(\"Harry Partch's 43-tone pure scale\", 43 steps, period 1200.00¢)",
            tuning.ToString());
    }
}
