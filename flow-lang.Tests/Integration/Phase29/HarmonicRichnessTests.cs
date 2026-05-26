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
