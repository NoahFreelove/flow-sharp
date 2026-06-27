using System;
using System.IO;
using System.Text.Json;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Synthesizers;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.Tests.Helpers;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase29;

/// <summary>
/// Phase 29 REQ-6 / SPEC D-23 — Drums / Organ / Wavetable harmonic-richness
/// ratio must increase ≥ 20% vs the pinned Phase 28 baseline. Baseline values
/// are computed once (Plan 05 Task 1 via <c>ComputePhase28Baseline</c>) and
/// pinned in <c>flow-lang.Tests/fixtures/Phase29/phase28_harmonic_richness_baseline.json</c>.
/// This test compares the current (Phase 29) output to the pinned baseline.
///
/// All three retained-synth instruments (Drums kick at MIDI 36, Organ at C4,
/// the three new wavetable variants at C4) must clear the 1.20× gate.
///
/// Note on the fundamental frequencies used here:
///   • Drums kick — the synth produces a pitch-sweeping body sine ending near
///     50 Hz; the baseline measurement uses 50 Hz as the "fundamental" bin.
///   • Organ / Wavetable — C4 = 261.63 Hz (12-TET, A4 = 440 Hz).
/// </summary>
public class HarmonicRichnessTests
{
    private const double GAIN_THRESHOLD = 1.20;  // ≥ 20% increase
    private const int SampleRate = 44100;
    private const double Bpm = 120.0;
    // 1 second of audio at 120 BPM = 2 beats. Matches the baseline computation
    // (ComputePhase28Baseline.cs uses the same duration).
    private const double DurationBeats = 2.0;

    private static readonly JsonElement Baseline = LoadBaseline();

    private static JsonElement LoadBaseline()
    {
        string fixturePath = LocateFixture("phase28_harmonic_richness_baseline.json");
        string json = File.ReadAllText(fixturePath);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Walks up from AppContext.BaseDirectory until it finds the
    /// flow-lang.Tests source directory, then returns the path to the named
    /// fixture under fixtures/Phase29/. Same pattern used by
    /// ComputePhase28Baseline.cs's source-tree resolver.
    /// </summary>
    private static string LocateFixture(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "flow-lang.Tests", "fixtures", "Phase29", fileName);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            $"Could not locate fixture '{fileName}' in flow-lang.Tests/fixtures/Phase29/ above AppContext.BaseDirectory.");
    }

    [Fact]
    public void DrumKick_HarmonicRichness_AtLeast20PercentGainOverPhase28Baseline()
    {
        var phase28 = Baseline.GetProperty("drums_kick").GetDouble();
        var fundamentalHz = Baseline.GetProperty("drums_kick_fundamental_hz").GetDouble();

        var synth = new DrumSynthesizer();
        // Construct C2 kick note (MIDI 36)
        var note = new MusicalNoteData(
            noteName: 'C', octave: 2, alteration: 0,
            durationValue: 1, isRest: false, velocity: 0.9,
            articulation: Articulation.Normal);
        var buf = synth.RenderNote(note, SampleRate, DurationBeats, Bpm, RenderTuning.Default);

        double phase29 = Phase29Fft.HarmonicRichnessRatio(buf, fundamentalHz);
        double gain = phase29 / phase28;

        Assert.True(gain >= GAIN_THRESHOLD,
            $"DrumSynthesizer kick harmonic-richness gain insufficient. " +
            $"Phase 28 baseline: {phase28:F3}, Phase 29: {phase29:F3}, gain: {gain:F3}× (need ≥ {GAIN_THRESHOLD}×)");
    }

    [Fact]
    public void Organ_HarmonicRichness_AtLeast20PercentGainOverPhase28Baseline()
    {
        var phase28 = Baseline.GetProperty("organ_c4").GetDouble();
        var fundamentalHz = Baseline.GetProperty("organ_c4_fundamental_hz").GetDouble();

        var synth = new OrganSynthesizer();
        var note = new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 0,
            durationValue: 1, isRest: false, velocity: 0.7,
            articulation: Articulation.Normal);
        var buf = synth.RenderNote(note, SampleRate, DurationBeats, Bpm, RenderTuning.Default);

        double phase29 = Phase29Fft.HarmonicRichnessRatio(buf, fundamentalHz);
        double gain = phase29 / phase28;

        Assert.True(gain >= GAIN_THRESHOLD,
            $"OrganSynthesizer C4 harmonic-richness gain insufficient. " +
            $"Phase 28 baseline: {phase28:F3}, Phase 29: {phase29:F3}, gain: {gain:F3}× (need ≥ {GAIN_THRESHOLD}×)");
    }

    /// <summary>
    /// The "bright" variant is the canonical Phase 29 wavetable reference for
    /// the default-richness comparison. Its narrow-pulse spectrum is the
    /// richest of the three new variants and most clearly demonstrates the
    /// Plan 05 Task 4 upgrade vs the canonical Phase 28 sawtooth baseline.
    /// "warm" and "buzz" are exercised independently by the
    /// WavetableVariants_AreRegistered_AndExceedBaseline theory below.
    /// </summary>
    [Fact]
    public void Wavetable_HarmonicRichness_AtLeast20PercentGainOverPhase28Baseline()
    {
        var phase28 = Baseline.GetProperty("wavetable_c4_default").GetDouble();
        var fundamentalHz = Baseline.GetProperty("wavetable_c4_fundamental_hz").GetDouble();

        // Use SynthesizerFactory so the "bright" wavetable variant gets
        // registered via EnsureBuiltinVariantsRegistered on first call.
        var synth = SynthesizerFactory.Create("bright");
        var note = new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 0,
            durationValue: 1, isRest: false, velocity: 0.7,
            articulation: Articulation.Normal);
        var buf = synth.RenderNote(note, SampleRate, DurationBeats, Bpm, RenderTuning.Default);

        double phase29 = Phase29Fft.HarmonicRichnessRatio(buf, fundamentalHz);
        double gain = phase29 / phase28;

        Assert.True(gain >= GAIN_THRESHOLD,
            $"Wavetable 'bright' harmonic-richness gain insufficient. " +
            $"Phase 28 baseline: {phase28:F3}, Phase 29: {phase29:F3}, gain: {gain:F3}× (need ≥ {GAIN_THRESHOLD}×)");
    }

    /// <summary>
    /// All three Phase 29 wavetable variants must be (a) registered with the
    /// factory and (b) render a non-empty buffer. The richness gain is also
    /// checked per variant — all three variants exceed the Phase 28 baseline
    /// (warm ≈ 0.998 / +90%, bright ≈ 3.15 / +498%, buzz ≈ 1.71 / +226%).
    /// </summary>
    // === quick 260608-wcy — Sound Design 2.0 band-limiting floor ============
    // The Saw + Square oscillators are now PolyBLEP band-limited (D-37-09 pulled
    // forward). Band-limiting removes ONLY the aliased fold-back energy ABOVE
    // Nyquist — every legitimate sub-Nyquist harmonic survives. These two NEW facts
    // MEASURE that the harmonic-richness floor holds post-band-limiting (each
    // assertion PROVES the floor; it does not assume it). Phase29Fft.HarmonicRichnessRatio
    // already skips partials >= Nyquist, so it counts only legit in-band harmonics —
    // exactly the energy band-limiting is designed to preserve. A4 = 440 Hz (12-TET);
    // the raw ratio is used (no Phase 28 baseline exists for these synths). The existing
    // drums/organ/wavetable facts above are untouched.
    //
    // WHY TWO DIFFERENT FLOORS (measured, not guessed — quick 260608-wcy verification):
    //   • SAW has ALL integer harmonics (1/n rolloff), so the 2nd..8th-partial sweep is
    //     dense → measured richness ≈ 0.52 at A4/C4/A2, clears the canonical 0.20 floor
    //     by 2.6×. The naive pre-band-limiting saw measured ≈ 0.527 — so band-limiting
    //     preserved the in-band harmonics essentially intact (the >=0.20 assert is real).
    //   • SQUARE has ODD-ONLY harmonics — its even partials (2f,4f,6f,8f) are ~0 by the
    //     waveform's nature. The helper's 2nd..8th sweep therefore only captures 3f/5f/7f,
    //     capping a square's measurable ratio near (1/9 + 1/25 + 1/49) ≈ 0.172 REGARDLESS
    //     of band-limiting. The naive pre-band-limiting square measured 0.1715; the
    //     band-limited square measures 0.1697 — a 0.001 delta that is fold-back removal,
    //     NOT harmonic loss. So the honest square floor is its odd-harmonic ceiling, not
    //     the saw's 0.20. We assert >= 0.15 (a comfortable margin under the 0.17 intrinsic
    //     value) AND assert the band-limited ratio tracks the naive ratio within 0.02 —
    //     the latter is the REAL invariant: band-limiting preserved the legit harmonics.
    //   Setting the square floor to 0.20 would be a FALSE assertion about a square wave's
    //   spectrum, not a real regression gate — so it is documented down to the truth here
    //   rather than tolerance-loosened to force a pass.

    private const double A4_Hz = 440.0;
    private const double SAW_RICHNESS_FLOOR = 0.20;   // saw clears this ~2.6x (all harmonics)
    private const double SQUARE_RICHNESS_FLOOR = 0.15; // square's odd-only 2..8 sweep caps near 0.17

    [Fact]
    public void Saw_HarmonicRichness_ClearsFloor_AfterBandLimiting()
    {
        var synth = SynthesizerFactory.Create("saw");
        var note = new MusicalNoteData(
            noteName: 'A', octave: 4, alteration: 0,
            durationValue: 1, isRest: false, velocity: 0.7,
            articulation: Articulation.Normal);
        var buf = synth.RenderNote(note, SampleRate, DurationBeats, Bpm, RenderTuning.Default);

        double ratio = Phase29Fft.HarmonicRichnessRatio(buf, A4_Hz);

        Assert.True(ratio >= SAW_RICHNESS_FLOOR,
            $"Band-limited SAW harmonic richness fell below the floor. " +
            $"A4=440Hz measured ratio: {ratio:F4} (need >= {SAW_RICHNESS_FLOOR:F2}). " +
            $"PolyBLEP removes only aliased fold-back above Nyquist — legit sub-Nyquist " +
            $"harmonics (all integer multiples for a saw) must remain, keeping the ratio " +
            $"well above the floor.");
    }

    [Fact]
    public void Square_HarmonicRichness_ClearsFloor_AfterBandLimiting()
    {
        var synth = SynthesizerFactory.Create("square");
        var note = new MusicalNoteData(
            noteName: 'A', octave: 4, alteration: 0,
            durationValue: 1, isRest: false, velocity: 0.7,
            articulation: Articulation.Normal);
        var buf = synth.RenderNote(note, SampleRate, DurationBeats, Bpm, RenderTuning.Default);

        double bandLimited = Phase29Fft.HarmonicRichnessRatio(buf, A4_Hz);

        // The REAL invariant: band-limiting preserves the legit in-band harmonics, so the
        // band-limited richness must track the NAIVE (pre-band-limiting) richness — it must
        // NOT have lost harmonic energy below Nyquist. Reconstruct the naive square inline
        // (same fixed inputs) and assert the two ratios agree within 0.02 (the difference is
        // aliased fold-back the helper barely sees at A4, not harmonic loss).
        double naive = NaiveSquareRichness(A4_Hz);

        Assert.True(bandLimited >= SQUARE_RICHNESS_FLOOR,
            $"Band-limited SQUARE harmonic richness fell below the (odd-harmonic) floor. " +
            $"A4=440Hz measured ratio: {bandLimited:F4} (need >= {SQUARE_RICHNESS_FLOOR:F2}). " +
            $"A square has odd-only harmonics, so the 2..8 sweep caps near 0.17 by the " +
            $"waveform's nature; band-limiting must not push it below 0.15.");

        Assert.True(Math.Abs(bandLimited - naive) <= 0.02,
            $"Band-limiting changed the SQUARE's in-band harmonic richness by more than 0.02 — " +
            $"that would mean legit sub-Nyquist harmonics were lost, not just aliasing. " +
            $"naive={naive:F4}, band-limited={bandLimited:F4}, |delta|={Math.Abs(bandLimited - naive):F4}.");
    }

    // Reconstructs the NAIVE (pre-band-limiting) square at the given fundamental over the
    // existing 2.0-beat / 120-bpm / 44100 convention, then measures its harmonic richness.
    // Pins the "band-limiting preserved the legit harmonics" invariant for the square fact.
    private static double NaiveSquareRichness(double fundamentalHz)
    {
        double durationSeconds = (DurationBeats / Bpm) * 60.0;
        int numSamples = (int)(durationSeconds * SampleRate);
        double amplitude = 0.2 * 0.7; // square amplitude scalar * velocity (matches the synth)
        var data = new float[numSamples];
        for (int i = 0; i < numSamples; i++)
        {
            double t = i / (double)SampleRate;
            double phase = (fundamentalHz * t) % 1.0;
            data[i] = (float)(amplitude * (phase < 0.5 ? 1.0 : -1.0));
        }
        return Phase29Fft.HarmonicRichnessRatio(data, SampleRate, fundamentalHz);
    }

    [Theory]
    [InlineData("warm")]
    [InlineData("bright")]
    [InlineData("buzz")]
    public void WavetableVariants_AreRegistered_AndExceedBaseline(string variant)
    {
        var phase28 = Baseline.GetProperty("wavetable_c4_default").GetDouble();
        var fundamentalHz = Baseline.GetProperty("wavetable_c4_fundamental_hz").GetDouble();

        var synth = SynthesizerFactory.Create(variant);
        Assert.NotNull(synth);

        var note = new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 0,
            durationValue: 1, isRest: false, velocity: 0.7,
            articulation: Articulation.Normal);
        var buf = synth.RenderNote(note, SampleRate, DurationBeats, Bpm, RenderTuning.Default);
        Assert.True(buf.Frames > 0, $"Wavetable variant '{variant}' produced an empty buffer");

        double phase29 = Phase29Fft.HarmonicRichnessRatio(buf, fundamentalHz);
        double gain = phase29 / phase28;

        Assert.True(gain >= GAIN_THRESHOLD,
            $"Wavetable variant '{variant}' harmonic-richness gain insufficient. " +
            $"Phase 28 baseline: {phase28:F3}, variant: {phase29:F3}, gain: {gain:F3}× (need ≥ {GAIN_THRESHOLD}×)");
    }
}
