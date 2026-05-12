using System;
using System.Collections.Generic;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase28;

/// <summary>
/// Phase 28 (SPEC-4) Plan 02 acceptance facts pinning the LOCKED articulation
/// duration multipliers applied at <see cref="BarRenderer"/>:
///
///   Normal     100%
///   Staccato    25%
///   Marcato     25% (Staccato-shortened envelope; Accent's velocity boost lives in
///                    <see cref="ArticulationVelocityTests"/>)
///   Tenuto     100%
///   Legato     110%
///   Accent     100%
///   Sforzando  100% (envelope spike applied per-synth in Plan 28-03 — buffer-level
///                    AUDIBLE duration is unchanged)
///
/// Tolerance: ±5% of expected audible duration per SPEC-4. Tests render a single C4
/// quarter note at BPM 120 (0.5 sec authored) under each articulation through the
/// "sine" synthesizer (minimal envelope shaping → audible duration cleanly reflects
/// BarRenderer's duration multiplier; per-synth envelope shape tests live in Plan 03).
/// </summary>
public class ArticulationRulesTests
{
    private const int SampleRate = 44100;
    private const double Bpm = 120.0;

    /// <summary>
    /// Renders a single C4 quarter note under <paramref name="articulation"/> via
    /// BarRenderer.RenderBarToVoices and returns the produced voices. At BPM 120 the
    /// authored sounding duration is 1 beat = 0.5 seconds (Normal baseline).
    /// </summary>
    private static List<Voice> RenderArticulatedC4q(Articulation articulation)
    {
        var note = new MusicalNoteData(
            'C', 4, 0,
            (int)NoteValueType.Value.QUARTER,
            isRest: false,
            articulation: articulation);
        var bar = new BarData(new[] { note }, new TimeSignatureData(4, 4));
        return BarRenderer.RenderBarToVoices(bar, "sine", SampleRate, Bpm);
    }

    /// <summary>
    /// Returns the audible duration of <paramref name="buffer"/> in seconds, defined as
    /// the time between the first and last frames where any channel's |sample| exceeds
    /// 0.001 (matches the BarRenderer's allocated buffer length so trailing silence is
    /// excluded). Returns 0.0 when no samples cross the threshold.
    /// </summary>
    private static double ComputeAudibleDurationSeconds(AudioBuffer buffer)
    {
        const double threshold = 0.001;
        int firstAudible = -1;
        int lastAudible = -1;
        for (int frame = 0; frame < buffer.Frames; frame++)
        {
            bool audible = false;
            for (int ch = 0; ch < buffer.Channels; ch++)
            {
                if (Math.Abs(buffer.GetSample(frame, ch)) > threshold)
                {
                    audible = true;
                    break;
                }
            }
            if (audible)
            {
                if (firstAudible < 0) firstAudible = frame;
                lastAudible = frame;
            }
        }
        if (firstAudible < 0) return 0.0;
        return (lastAudible - firstAudible + 1) / (double)buffer.SampleRate;
    }

    private static double ExpectedSeconds(double multiplier) => 0.5 * multiplier;

    private static (double Min, double Max) Tolerance(double expectedSec, double pct = 0.05)
        => (expectedSec * (1.0 - pct), expectedSec * (1.0 + pct));

    [Fact]
    public void Articulation_Normal_100Percent()
    {
        var voices = RenderArticulatedC4q(Articulation.Normal);
        Assert.Single(voices);
        double audible = ComputeAudibleDurationSeconds(voices[0].Buffer);
        var (min, max) = Tolerance(ExpectedSeconds(1.00));
        Assert.InRange(audible, min, max);
    }

    [Fact]
    public void Articulation_Staccato_25Percent()
    {
        var voices = RenderArticulatedC4q(Articulation.Staccato);
        Assert.Single(voices);
        double audible = ComputeAudibleDurationSeconds(voices[0].Buffer);
        var (min, max) = Tolerance(ExpectedSeconds(0.25));
        Assert.InRange(audible, min, max);
    }

    [Fact]
    public void Articulation_Marcato_25Percent()
    {
        // Marcato shares Staccato's 25% duration envelope — the Accent +0.30 velocity
        // boost is verified separately in ArticulationVelocityTests.
        var voices = RenderArticulatedC4q(Articulation.Marcato);
        Assert.Single(voices);
        double audible = ComputeAudibleDurationSeconds(voices[0].Buffer);
        var (min, max) = Tolerance(ExpectedSeconds(0.25));
        Assert.InRange(audible, min, max);
    }

    [Fact]
    public void Articulation_Tenuto_100Percent()
    {
        var voices = RenderArticulatedC4q(Articulation.Tenuto);
        Assert.Single(voices);
        double audible = ComputeAudibleDurationSeconds(voices[0].Buffer);
        var (min, max) = Tolerance(ExpectedSeconds(1.00));
        Assert.InRange(audible, min, max);
    }

    [Fact]
    public void Articulation_Legato_110Percent()
    {
        var voices = RenderArticulatedC4q(Articulation.Legato);
        Assert.Single(voices);
        double audible = ComputeAudibleDurationSeconds(voices[0].Buffer);
        var (min, max) = Tolerance(ExpectedSeconds(1.10));
        Assert.InRange(audible, min, max);
    }

    [Fact]
    public void Articulation_Accent_100Percent()
    {
        var voices = RenderArticulatedC4q(Articulation.Accent);
        Assert.Single(voices);
        double audible = ComputeAudibleDurationSeconds(voices[0].Buffer);
        var (min, max) = Tolerance(ExpectedSeconds(1.00));
        Assert.InRange(audible, min, max);
    }

    [Fact]
    public void Articulation_Sforzando_100Percent()
    {
        // Sforzando duration unchanged at the BarRenderer layer — the time-varying
        // envelope spike lands per-synth in Plan 28-03 (FFT cosine-similarity tests
        // belong there, not here). Audible duration on "sine" synth still equals 100%.
        var voices = RenderArticulatedC4q(Articulation.Sforzando);
        Assert.Single(voices);
        double audible = ComputeAudibleDurationSeconds(voices[0].Buffer);
        var (min, max) = Tolerance(ExpectedSeconds(1.00));
        Assert.InRange(audible, min, max);
    }

    [Fact]
    public void ArticulationRules_AllSix()
    {
        // Cross-cut Fact: render every non-Normal articulation and assert each buffer's
        // audible duration matches its locked multiplier. Guards against any single-rule
        // regression slipping past an individual Fact.
        var expected = new (Articulation Art, double Mul)[]
        {
            (Articulation.Staccato,  0.25),
            (Articulation.Marcato,   0.25),
            (Articulation.Tenuto,    1.00),
            (Articulation.Legato,    1.10),
            (Articulation.Accent,    1.00),
            (Articulation.Sforzando, 1.00),
        };
        foreach (var (art, mul) in expected)
        {
            var voices = RenderArticulatedC4q(art);
            Assert.Single(voices);
            double audible = ComputeAudibleDurationSeconds(voices[0].Buffer);
            var (min, max) = Tolerance(ExpectedSeconds(mul));
            Assert.True(audible >= min && audible <= max,
                $"{art}: expected {ExpectedSeconds(mul):F4}s ±5%, got {audible:F4}s (range [{min:F4}, {max:F4}])");
        }
    }
}
