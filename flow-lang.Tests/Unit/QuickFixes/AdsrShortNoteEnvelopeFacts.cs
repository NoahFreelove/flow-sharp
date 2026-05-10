using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Unit.QuickFixes;

/// <summary>
/// QUICK-260504-v6j regression facts for short-note ADSR envelope shape.
///
/// Bug: <see cref="EnvelopeProcessor.GenerateEnvelopeCurve"/> previously clamped
/// release to <c>totalFrames - attackFrames - decayFrames</c>, which collapses
/// release to ZERO frames whenever total length is shorter than attack+decay.
/// The result was short notes (32nd notes in MIDI imports, anything tagged
/// staccato in BarRenderer) ending on a non-zero sample → audible click.
///
/// Fix: when attack+decay+release exceeds totalFrames, scale all three
/// proportionally so release is preserved. Long notes (a+d+r &lt; totalFrames)
/// are unchanged.
///
/// Sample rate constant matches the 44.1 kHz default used everywhere in the
/// project audio pipeline.
/// </summary>
public class AdsrShortNoteEnvelopeFacts
{
    private const int SampleRate44100 = 44100;

    // Piano main ADSR (PianoSynthesizer.cs:51-54) — the canonical bug repro params.
    private const double PianoAttack  = 0.003;
    private const double PianoDecay   = 0.6;
    private const double PianoSustain = 0.12;
    private const double PianoRelease = 0.3;

    private static Envelope MakePianoAdsr() =>
        new(EnvelopeKind.ADSR,
            new[] { PianoAttack, PianoDecay, PianoSustain, PianoRelease },
            SampleRate44100);

    /// <summary>
    /// 62.5 ms note (32nd at 120 BPM) → 2756 frames at 44.1 kHz.
    /// Total a+d+r = 903 ms ≫ 62.5 ms, so the pre-fix clamping collapses release to 0.
    /// </summary>
    private const int ShortNoteFrames = 2756;

    [Fact]
    public void ShortNote_HasNonZeroReleaseTail()
    {
        var env = MakePianoAdsr();

        var curve = EnvelopeProcessor.GenerateEnvelopeCurve(env, ShortNoteFrames);

        // 1) Curve must end at exactly 0.0 — no abrupt non-zero cutoff.
        Assert.Equal(0.0f, curve[ShortNoteFrames - 1]);

        // 2) The last 5 frames before the final zero must form a strictly
        //    descending tail, AND the start of that tail must be > 0.
        //    This proves a multi-sample release ramp exists (not a single-sample cliff).
        Assert.True(curve[ShortNoteFrames - 5] > 0.0f,
            $"Expected non-zero amplitude 5 frames from end, got {curve[ShortNoteFrames - 5]}");

        for (int i = ShortNoteFrames - 5; i < ShortNoteFrames - 1; i++)
        {
            Assert.True(curve[i] > curve[i + 1],
                $"Expected descending tail at i={i}: curve[{i}]={curve[i]} should be > curve[{i + 1}]={curve[i + 1]}");
        }
    }

    [Fact]
    public void ShortNote_HasNonZeroAttack()
    {
        var env = MakePianoAdsr();

        var curve = EnvelopeProcessor.GenerateEnvelopeCurve(env, ShortNoteFrames);

        // Attack always starts at 0.
        Assert.Equal(0.0f, curve[0]);

        // Within the first 200 frames, at least one ascending pair must exist.
        bool foundAscending = false;
        for (int i = 0; i < 200 && i + 1 < ShortNoteFrames; i++)
        {
            if (curve[i] < curve[i + 1])
            {
                foundAscending = true;
                break;
            }
        }
        Assert.True(foundAscending,
            "Expected an ascending pair in the first 200 frames (attack phase still present)");
    }

    [Fact]
    public void ShortNote_NoAbruptCliff()
    {
        var env = MakePianoAdsr();

        var curve = EnvelopeProcessor.GenerateEnvelopeCurve(env, ShortNoteFrames);

        // No two adjacent samples may differ by more than 0.5. Attack/decay/release
        // ramps span many samples, so any single-sample drop > 0.5 indicates the
        // missing-release cliff (sustain-level → 0 in one frame).
        for (int i = 0; i < ShortNoteFrames - 1; i++)
        {
            float delta = MathF.Abs(curve[i] - curve[i + 1]);
            Assert.True(delta <= 0.5f,
                $"Abrupt cliff detected at i={i}: |curve[{i}]={curve[i]} - curve[{i + 1}]={curve[i + 1]}| = {delta} > 0.5");
        }
    }

    [Fact]
    public void LongNote_PreservesExactFrameCounts()
    {
        // 3 seconds @ 44.1 kHz = 132300 frames. a+d+r = 903 ms < 3 s, so the
        // proportional-scale path must NOT trigger; long-note shape stays identical.
        const int totalFrames = 132300;
        var env = MakePianoAdsr();

        var curve = EnvelopeProcessor.GenerateEnvelopeCurve(env, totalFrames);

        // attackFrames = (int)(0.003 * 44100) = 132.
        // The last attack sample (frame 131) writes (131/132)f ≈ 0.9924; frame 132
        // belongs to decay and writes 1.0 (decay i=0 → t=0 → 1 - 0*(1-s) = 1.0).
        Assert.InRange(curve[132], 1.0f - 0.05f, 1.0f + 0.05f);

        // Decay length = (int)(0.6 * 44100) = 26460. Last decay sample = frame 132+26459;
        // its value is 1 - (26459/26460)*(1-0.12) ≈ 0.12.
        int decayEnd = 132 + 26460 - 1;
        Assert.InRange(curve[decayEnd], (float)PianoSustain - 0.05f, (float)PianoSustain + 0.05f);

        // Release start = totalFrames - releaseFrames = 132300 - 13230 = 119070.
        // First release frame holds sustainLevel (release i=0 → t=0 → s*(1-0) = s).
        int releaseStart = totalFrames - 13230;
        Assert.InRange(curve[releaseStart], (float)PianoSustain - 0.05f, (float)PianoSustain + 0.05f);

        // Final sample of release: i = releaseFrames-1 → t ≈ 1 → s*(1-1) ≈ 0.
        // Tolerance allows for the exact ramp endpoint being s/releaseFrames, not 0.
        Assert.InRange(curve[totalFrames - 1], 0.0f, 0.05f);
    }

    [Fact]
    public void MediumNote_AttackPlusDecayJustExceedsBuffer_StillHasRelease()
    {
        // totalFrames such that attack+decay (in seconds) ≈ buffer length.
        // (0.003 + 0.6) * 44100 ≈ 26593 frames. With release=0.3 (13230 frames)
        // requested, the pre-fix clamp gave 0 release frames; the fix must allocate
        // a proportional share so the curve ends at 0 with a real ramp.
        const int totalFrames = 26593;
        var env = MakePianoAdsr();

        var curve = EnvelopeProcessor.GenerateEnvelopeCurve(env, totalFrames);

        // Final sample MUST be 0.
        Assert.Equal(0.0f, curve[totalFrames - 1]);

        // Confirm a release-shaped tail exists: at least one descending pair near the end.
        bool foundDescending = false;
        int searchStart = Math.Max(0, totalFrames - 200);
        for (int i = searchStart; i < totalFrames - 1; i++)
        {
            if (curve[i] > curve[i + 1] && curve[i] > 0.0f)
            {
                foundDescending = true;
                break;
            }
        }
        Assert.True(foundDescending,
            "Medium note (a+d ≈ buffer length) must still produce a non-zero descending release tail");
    }

    [Fact]
    public void ZeroDurationNote_ReturnsAllZeroCurve_NoExceptions()
    {
        var env = MakePianoAdsr();

        // Should not throw. Returns an empty array.
        var curve = EnvelopeProcessor.GenerateEnvelopeCurve(env, 0);

        Assert.NotNull(curve);
        Assert.Empty(curve);
    }
}
