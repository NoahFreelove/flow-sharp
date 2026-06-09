using System;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase46;

/// <summary>
/// Phase 46 Plan 01 (D-03 prerequisite) — EXACT-BYTE oscillator guard.
///
/// WHY THIS EXISTS:
/// Plan 46-06 (Wave 2) redirects the four primitive synth classes
/// (Sine/Saw/Square/Triangle) in <c>NoteSynthesizer.cs:24-182</c> so their inline
/// oscillator loops route through the shared <c>SynthUtils.Generate*</c> generators
/// (D-03 "remove the private duplicate helpers"). That redirect is ONLY safe if the
/// rendered <c>float[]</c> stays bit-identical.
///
/// The single locked phase-verification gate (D-18: full <c>dotnet test</c> + every
/// <c>tests/test_*.flow</c> + Phase 28 RMS-windowed baselines + two-run cmp-clean
/// determinism) CANNOT catch a before-vs-after ±1-ULP IEEE-754 shift:
///   • RMS tolerance is ±0.5 dB / 100 ms — a 1-ULP drift is far below that floor.
///   • two-run cmp-clean compares the SAME code against itself — it is blind to a
///     pre-redirect vs post-redirect divergence.
/// This Fact is that missing safety net: it freezes the CURRENT (pre-redirect) build's
/// oscillator output as an exact element-wise contract.
///
/// THE REAL RISK (verified live at authorship): the current NoteSynthesizer uses an
/// ABSOLUTE-TIME formula — <c>t = i / sampleRate; sample = amp * f(frequency * t)</c> —
/// whereas <c>SynthUtils.Generate*</c> use an INCREMENTAL PHASE ACCUMULATOR
/// (<c>phase += phaseInc</c> with a wrap at 1.0). Those two formulations are NOT
/// guaranteed bit-identical: floating-point rounding of <c>frequency * (i/sr)</c>
/// differs from a running sum of <c>frequency/sr</c>. So this guard is EXPECTED to be
/// load-bearing, not a formality.
///
/// FALLBACK (RESEARCH §D-03 + Open Q2): if this Fact goes RED after the Wave 2
/// redirect, 46-06 must NOT force bit-equality. Instead keep the oscillator loops
/// inline in NoteSynthesizer and redirect ONLY the trivially-identical helpers
/// (<c>BeatsToSeconds</c> + <c>CreateSilence</c>), which removes the duplication that
/// matters without changing a single rendered sample.
///
/// CAPTURE METHOD: the baseline is reconstructed in-test by an independent oracle that
/// replicates the EXACT arithmetic of the current NoteSynthesizer per-synth loops
/// (see <see cref="ExpectedSine"/> etc.). Because the oracle mirrors the current code
/// element-for-element, the assertion is GREEN against the current build by
/// construction, and any unintended deviation makes it RED. No external
/// binary baseline file is needed; <c>baselines/Phase46/.gitkeep</c> reserves the dir
/// for any future binary capture. This test does NOT touch NoteSynthesizer.cs.
///
/// SAW/SQUARE REGENERATION (quick 260608-wcy — "Sound Design 2.0", D-37-09 pulled
/// forward): the Saw + Square oscillators are now PolyBLEP band-limited to remove
/// aliasing on low notes. That LEGITIMATELY changes their rendered bytes, so
/// <see cref="ExpectedSaw"/> and <see cref="ExpectedSquare"/> were regenerated to mirror
/// the NEW band-limited arithmetic element-for-element (the <see cref="PolyBlep"/> residual
/// + the saw <c>naive − blep(phase)</c> and square <c>naive + blep(phase) − blep((phase+0.5)%1)</c>
/// expressions). The guard stays a bit-exact compare — it is NOT loosened. Only the
/// ORACLE math moved, in lockstep with the production loop, so the guard still fires on
/// any unintended cross-synth or arithmetic regression.
/// <see cref="ExpectedSine"/> + <see cref="ExpectedTriangle"/> are UNCHANGED — sine is pure
/// and triangle's 1/n² rolloff barely aliases (needs polyBLAMP, out of scope), so both
/// synth loops were left verbatim and their oracles stay frozen at the pre-band-limiting
/// contract.
///
/// Fixed inputs (frozen so the captured arrays are stable):
///   pitch       = A4  (NoteName 'A', octave 4, alteration 0) → MIDI 69 → 440.0 Hz in
///                 12-TET via RenderTuning.Default (EqualTemperament short-circuit).
///   sampleRate  = 44100
///   durationBeats = 1.0
///   bpm         = 120  → durationSeconds = 0.5 → numSamples = 22050
///   velocity    = 0.63 (the MusicalNoteData default)
/// </summary>
[Collection("FlowScripts")]
public class NoteSynthesizerByteGuardTests
{
    private const int SampleRate = 44100;
    private const double DurationBeats = 1.0;
    private const double Bpm = 120.0;
    private const double Velocity = 0.63;

    // A4 = MIDI 69 = 440.0 Hz exactly in 12-TET. Pinned so the oracle is self-documenting;
    // the synth itself derives this via PitchConversion.NoteToFrequency(note, Default).
    private const double FrequencyA4 = 440.0;

    private static MusicalNoteData A4()
        => new MusicalNoteData(
            noteName: 'A', octave: 4, alteration: 0,
            durationValue: null, isRest: false,
            velocity: Velocity);

    private static float[] Render(string synthType)
    {
        INoteSynthesizer synth = SynthesizerFactory.Create(synthType);
        AudioBuffer buf = synth.RenderNote(A4(), SampleRate, DurationBeats, Bpm, RenderTuning.Default);
        Assert.Equal(1, buf.Channels);
        return buf.Data;
    }

    // ── Oracles: byte-for-byte mirrors of the CURRENT NoteSynthesizer.cs loops ─────────
    // Each replicates the absolute-time phase formula (t = i / sampleRate;
    // phase = (frequency * t) % 1.0) and the exact amplitude scalar used by the matching
    // synth class. Sine/Triangle are frozen at the pre-band-limiting contract; Saw/Square
    // mirror the PolyBLEP band-limited math (quick 260608-wcy) element-for-element.

    private static int ExpectedSampleCount()
    {
        double durationSeconds = (DurationBeats / Bpm) * 60.0; // BeatsToSeconds
        return (int)(durationSeconds * SampleRate);
    }

    /// <summary>
    /// Exact mirror of <c>BlepOscillator.PolyBlep</c> in NoteSynthesizer.cs — the standard
    /// 2-sample PolyBLEP residual. Replicated here element-for-element so the saw/square
    /// oracles are byte-identical to the production loop BY CONSTRUCTION.
    /// </summary>
    private static double PolyBlep(double t, double dt)
    {
        if (t < dt)
        {
            t = t / dt;
            return t + t - t * t - 1.0;
        }
        else if (t > 1.0 - dt)
        {
            t = (t - 1.0) / dt;
            return t * t + t + t + 1.0;
        }
        else
        {
            return 0.0;
        }
    }

    private static float[] ExpectedSine()
    {
        int n = ExpectedSampleCount();
        double amplitude = 0.3 * Velocity;
        var expected = new float[n];
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SampleRate;
            expected[i] = (float)(amplitude * Math.Sin(2.0 * Math.PI * FrequencyA4 * t));
        }
        return expected;
    }

    // PolyBLEP band-limited saw (quick 260608-wcy). One +1 reset discontinuity per period
    // at the 0/1 wrap → subtract the residual. dt = frequency / sampleRate.
    private static float[] ExpectedSaw()
    {
        int n = ExpectedSampleCount();
        double amplitude = 0.2 * Velocity;
        double dt = FrequencyA4 / (double)SampleRate;
        var expected = new float[n];
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SampleRate;
            double phase = (FrequencyA4 * t) % 1.0;
            double naive = 2.0 * phase - 1.0;
            double value = naive - PolyBlep(phase, dt);
            expected[i] = (float)(amplitude * value);
        }
        return expected;
    }

    // PolyBLEP band-limited square (quick 260608-wcy). Rising +1 edge at the 0/1 wrap
    // (+residual) and falling −1 edge at phase 0.5 (−residual at the half-shifted phase).
    private static float[] ExpectedSquare()
    {
        int n = ExpectedSampleCount();
        double amplitude = 0.2 * Velocity;
        double dt = FrequencyA4 / (double)SampleRate;
        var expected = new float[n];
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SampleRate;
            double phase = (FrequencyA4 * t) % 1.0;
            double naive = phase < 0.5 ? 1.0 : -1.0;
            double value = naive + PolyBlep(phase, dt) - PolyBlep((phase + 0.5) % 1.0, dt);
            expected[i] = (float)(amplitude * value);
        }
        return expected;
    }

    private static float[] ExpectedTriangle()
    {
        int n = ExpectedSampleCount();
        double amplitude = 0.3 * Velocity;
        var expected = new float[n];
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SampleRate;
            double phase = (FrequencyA4 * t) % 1.0;
            expected[i] = (float)(amplitude * (phase < 0.5 ? 4 * phase - 1 : 3 - 4 * phase));
        }
        return expected;
    }

    /// <summary>
    /// Exact element-wise float compare. NOT RMS tolerance — the whole point is to fire
    /// on a single ±1-ULP divergence. Uses bit-pattern equality so -0.0f/+0.0f and any
    /// last-bit mantissa drift are both caught, and reports the first offending index.
    /// </summary>
    private static void AssertExactFloatArray(float[] expected, float[] actual, string synth)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            int eBits = BitConverter.SingleToInt32Bits(expected[i]);
            int aBits = BitConverter.SingleToInt32Bits(actual[i]);
            if (eBits != aBits)
            {
                Assert.Fail(
                    $"[{synth}] byte guard diverged at sample {i}: " +
                    $"expected {expected[i]:R} (0x{eBits:X8}) but got {actual[i]:R} (0x{aBits:X8}). " +
                    "If this fired after the Wave 2 D-03 redirect, take the documented fallback: " +
                    "keep the oscillator loops inline and redirect only BeatsToSeconds+CreateSilence.");
            }
        }
    }

    [Fact]
    public void Sine_RenderNote_MatchesPreRedirectBaseline()
        => AssertExactFloatArray(ExpectedSine(), Render("sine"), "sine");

    [Fact]
    public void Saw_RenderNote_MatchesPreRedirectBaseline()
        => AssertExactFloatArray(ExpectedSaw(), Render("saw"), "saw");

    [Fact]
    public void Square_RenderNote_MatchesPreRedirectBaseline()
        => AssertExactFloatArray(ExpectedSquare(), Render("square"), "square");

    [Fact]
    public void Triangle_RenderNote_MatchesPreRedirectBaseline()
        => AssertExactFloatArray(ExpectedTriangle(), Render("triangle"), "triangle");

    /// <summary>
    /// Sanity floor: the fixed inputs must yield the documented 22050-sample buffer.
    /// Pins the capture parameters so a future edit to DurationBeats/Bpm/SampleRate that
    /// would silently shrink the guarded window is caught.
    /// </summary>
    [Fact]
    public void CaptureParameters_ProduceExpectedSampleCount()
    {
        Assert.Equal(22050, ExpectedSampleCount());
        Assert.Equal(22050, Render("sine").Length);
    }
}
