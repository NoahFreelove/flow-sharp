using System;
using System.Collections.Generic;
using System.IO;
using FlowLang.StandardLibrary.Audio.Tuning;
using Xunit;

namespace FlowLang.Tests.Unit.Phase32;

/// <summary>
/// Phase 32 Plan 32-04 Task 2 — SPEC-5 ±0.1¢ acceptance battery for carlos_alpha,
/// SPEC-4 .kbm-alters-mapping-at-non-tonic-MIDI verification, D-09 negative-cents
/// descending-pitch Fact, and a small Partch ratio sanity battery. Pattern C
/// (frequency-comparison) — direct buffer inspection at the
/// <see cref="ResolvedTuning"/> layer (no FFT, no rendered audio).
///
/// SPEC-5 acceptance: <c>carlos_alpha.scl</c> renders ascending MIDI notes with
/// frequencies within ±0.1¢ of reference values for every step 0..18 (the full
/// period; 18 is the period wrap at 1404¢). The reference is computed from the
/// parsed <see cref="ParsedScala.StepCents"/> + <see cref="ParsedScala.PeriodCents"/>
/// directly inside the test so the Fact is self-consistent (no externally pinned
/// hex value).
/// </summary>
public class NonOctavePitchFacts
{
    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "fixtures")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not locate repo root");
    }

    private static string FixturePath(string name)
        => Path.Combine(FindRepoRoot(), "flow-lang.Tests", "fixtures", "scala", name);

    [Fact]
    public void CarlosAlpha_MidiAscending_FrequenciesMatchSpecValues_Within01Cents()
    {
        // SPEC-5 acceptance: for every step i = 0..18 the deviation from the
        // self-referenced cents target must be < 0.1¢.
        var parsed = ScalaParser.Parse(File.ReadAllText(FixturePath("carlos_alpha.scl")), "carlos_alpha.scl");
        var resolved = new ResolvedTuning(parsed, ScalaKbmParser.Default(parsed));
        double middleHz = resolved.MidiToHz[60];

        // i==0: degree 0 (the tonic); i==1..17: intra-period steps;
        // i==18: the period wrap (PeriodCents).
        int stepsPerPeriod = parsed.StepCents.Length + 1;
        for (int i = 0; i <= stepsPerPeriod; i++)
        {
            double expectedCents;
            if (i == 0) expectedCents = 0.0;
            else if (i < stepsPerPeriod) expectedCents = parsed.StepCents[i - 1];
            else expectedCents = parsed.PeriodCents;
            double expectedHz = middleHz * Math.Pow(2.0, expectedCents / 1200.0);
            double actual = resolved.MidiToHz[60 + i];
            double centsDiff = 1200.0 * Math.Log2(actual / expectedHz);
            Assert.True(Math.Abs(centsDiff) < 0.1,
                $"step {i}: {centsDiff:F4}¢ deviation (expected={expectedHz:R} Hz, actual={actual:R} Hz)");
        }
    }

    [Fact]
    public void CarlosAlpha_PeriodWrap_IsNonOctave()
    {
        // Headline non-octave verification: 18 steps of 78¢ each = 1404¢.
        //   2^(1404/1200) ≈ 2.2501 — wider than an octave but narrower than a fifth-octave.
        //   NOT 2.0 (octave). Carlos Alpha is a "9 equal divisions of the perfect fifth"
        //   scale — the period spans 18 such steps which is roughly 1.17 octaves.
        var parsed = ScalaParser.Parse(File.ReadAllText(FixturePath("carlos_alpha.scl")), "carlos_alpha.scl");
        var resolved = new ResolvedTuning(parsed, ScalaKbmParser.Default(parsed));
        int stepsPerPeriod = parsed.StepCents.Length + 1;
        int periodWrapMidi = 60 + stepsPerPeriod;
        double ratio = resolved.MidiToHz[periodWrapMidi] / resolved.MidiToHz[60];
        double expectedRatio = Math.Pow(2.0, parsed.PeriodCents / 1200.0);
        // SPEC-5 cents-precision tolerance: <0.1¢.
        double centsError = Math.Abs(1200.0 * Math.Log2(ratio / expectedRatio));
        Assert.True(centsError < 0.1,
            $"period wrap exceeded ±0.1¢: ratio={ratio:R}, expected={expectedRatio:R}, cents error={centsError:F4}¢ " +
            $"(stepsPerPeriod={stepsPerPeriod}, periodCents={parsed.PeriodCents})");
        // Sanity — verify the period is wider than an octave (1.17 octaves, not 1.0).
        Assert.True(ratio > 2.10,
            $"period wrap should be wider than an octave — got {ratio:F4}");
        // The period is NOT an octave: cents must NOT be 1200.
        Assert.NotEqual(1200.0, parsed.PeriodCents);
        // Analytic value: 2^(1404/1200) = 2.2501.
        Assert.InRange(ratio, 2.20, 2.30);
    }

    [Fact]
    public void NegativeCents_ProducesDescendingPitch()
    {
        // D-09: negative cents accepted verbatim; 2^(stepCents/1200) naturally
        // produces a ratio < 1 for negative input → descending pitch.
        var parsed = new ParsedScala(
            Description: "synthetic-descending",
            StepCents: new[] { -100.0 },
            PeriodCents: 1200.0,
            Ratios: new Dictionary<int, (int Num, int Den)>(),
            FilePath: "synthetic:negative.scl");
        var resolved = new ResolvedTuning(parsed, ScalaKbmParser.Default(parsed));

        Assert.True(resolved.MidiToHz[61] < resolved.MidiToHz[60],
            $"Negative step cents must produce descending pitch: " +
            $"MidiToHz[61]={resolved.MidiToHz[61]} should be < MidiToHz[60]={resolved.MidiToHz[60]}");

        // The cents difference between MIDI 60→61 should be -100¢ within fp noise.
        double centsDiff = 1200.0 * Math.Log2(resolved.MidiToHz[61] / resolved.MidiToHz[60]);
        Assert.Equal(-100.0, centsDiff, precision: 6);
    }

    [Fact]
    public void Partch43_KnownRatios_ProduceExpectedHz()
    {
        // Partch ratio inputs (D-11 preserves them); the cents → Hz round-trip
        // should land at the exact n/d ratio against MidiToHz[middleNote].
        //
        // MidiToHz[middleNote+degree] reads StepCents[degree-1] which corresponds to
        // Ratios[degree-1] (both keyed by step index — see ScalaParser layout).
        //   degree 1 ↔ Ratios[0]  = 81/80
        //   degree 2 ↔ Ratios[1]  = 33/32
        //   degree 3 ↔ Ratios[2]  = 21/20
        //   degree 18 ↔ Ratios[17] = 4/3
        var parsed = ScalaParser.Parse(File.ReadAllText(FixturePath("partch_43.scl")), "partch_43.scl");
        var resolved = new ResolvedTuning(parsed, ScalaKbmParser.Default(parsed));

        double r1 = resolved.MidiToHz[61] / resolved.MidiToHz[60];
        double r2 = resolved.MidiToHz[62] / resolved.MidiToHz[60];
        double r3 = resolved.MidiToHz[63] / resolved.MidiToHz[60];
        Assert.Equal(81.0 / 80.0, r1, precision: 9);
        Assert.Equal(33.0 / 32.0, r2, precision: 9);
        Assert.Equal(21.0 / 20.0, r3, precision: 9);

        // Ratios[17] is 4/3 — verified by reading fixtures/scala/partch_43.scl.
        Assert.True(parsed.Ratios.ContainsKey(17),
            "partch_43 should preserve Ratios[17] as (4, 3)");
        Assert.Equal((4, 3), parsed.Ratios[17]);
        // degree=18 reads StepCents[17] which derives from Ratios[17]=4/3.
        double r18 = resolved.MidiToHz[78] / resolved.MidiToHz[60];
        Assert.Equal(4.0 / 3.0, r18, precision: 9);
    }

    [Fact]
    public void LoadScala_TwoArg_KbmAltersPitchMapping_AtNonTonicMidi()
    {
        // SPEC-4 acceptance: a real .kbm with a non-default middleNote produces a
        // demonstrably different MidiToHz mapping at a non-tonic MIDI note. Uses
        // partch_43 because its 43-step asymmetric structure makes the position
        // shift detectable at distance from both middleNotes.
        var parsed = ScalaParser.Parse(File.ReadAllText(FixturePath("partch_43.scl")), "partch_43.scl");
        var defaultKbm = ScalaKbmParser.Default(parsed);
        var resolvedDefault = new ResolvedTuning(parsed, defaultKbm);

        // Synthesize a .kbm with middleNote=64 (instead of 60), but the SAME ref
        // anchor (refNote=69, refHz=440). Period auto-adopts from .scl per D-07.
        string kbmContent = string.Join("\n",
            "! synthetic-shifted-middle",
            "0",        // size = 0 (linear mapping)
            "0",        // firstMidi
            "127",      // lastMidi
            "64",       // middleNote = 64 (shifted from default 60)
            "69",       // refNote
            "440.0",    // refHz
            "0");       // formalOctave
        var partialKbm = ScalaKbmParser.Parse(kbmContent, "<synthetic>");
        var realKbm = new ScalaKbm(
            partialKbm.Size,
            partialKbm.FirstMidi,
            partialKbm.LastMidi,
            partialKbm.MiddleNote,
            partialKbm.ReferenceNote,
            partialKbm.ReferenceHz,
            partialKbm.FormalOctave,
            partialKbm.Mapping,
            period: parsed.PeriodCents);
        var resolvedRealKbm = new ResolvedTuning(parsed, realKbm);

        // Non-tonic MIDI note 65 — well outside both middleNotes (60 vs 64) so the
        // step-walk produces different scale-degree positions. The partch step grid
        // is asymmetric → MidiToHz[65] WILL differ between the two KBMs.
        double diffHz = Math.Abs(resolvedDefault.MidiToHz[65] - resolvedRealKbm.MidiToHz[65]);
        Assert.True(diffHz > 0.5,
            $".kbm should demonstrably alter MidiToHz at non-tonic MIDI 65: " +
            $"diff={diffHz} Hz (default={resolvedDefault.MidiToHz[65]}, realKbm={resolvedRealKbm.MidiToHz[65]})");

        // Anchor invariant: each KBM's MidiToHz[refNote] == its own refHz.
        // Both KBMs use refNote=69, refHz=440.0, so both anchor at the same point.
        Assert.Equal(440.0, resolvedDefault.MidiToHz[69], precision: 6);
        Assert.Equal(440.0, resolvedRealKbm.MidiToHz[69], precision: 6);
    }
}
