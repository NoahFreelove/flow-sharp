using System;
using System.Globalization;
using System.IO;
using System.Text;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Synthesizers;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.Tests.Helpers;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Tools;

/// <summary>
/// One-shot baseline-computer for Phase 29 Plan 05 / SPEC D-23 / REQ-6.
///
/// Computes the harmonic-richness ratio of the three retained-synth instruments
/// (Drums kick at C2/MIDI 36, Organ at C4, Wavetable at C4) under the CURRENT
/// (Phase 28) synthesizer implementations and writes the values to
/// <c>flow-lang.Tests/Fixtures/Phase29/phase28_harmonic_richness_baseline.json</c>.
///
/// The HarmonicRichnessTests in Phase 29 then assert that the post-Plan-05
/// synth output is ≥ 1.20× these pinned baselines.
///
/// This Fact is tagged <c>[Trait("Category", "Phase29Baseline")]</c> so it can
/// be invoked explicitly via
/// <c>dotnet test --filter "Category=Phase29Baseline"</c> without polluting
/// normal CI runs (it writes a file under flow-lang.Tests/Fixtures/Phase29/).
///
/// CRITICAL: this MUST be run with the pre-Plan-05 synth code in place. If
/// run after Plan 05 modifies the synth classes, the "baseline" values it
/// emits will already include Plan 05's gains and the comparison test will
/// trivially pass — defeating the purpose. The committed JSON is the locked
/// pre-modification snapshot.
/// </summary>
public class Phase29BaselineRecorder
{
    private const int SampleRate = 44100;
    private const double Bpm = 120.0;
    // 1 beat at 120 BPM = 0.5 seconds; 2 beats = 1.0 second. Each baseline
    // measurement is over 2 beats / 1 sec of rendered audio.
    private const double DurationBeats = 2.0;

    private const double DrumsKickFundamentalHz = 50.0;
    // C4 = 261.6256 Hz (12-TET, A4 = 440 Hz). Per spec, baseline uses 261.63.
    private const double OrganC4FundamentalHz = 261.63;
    private const double WavetableC4FundamentalHz = 261.63;

    [Fact]
    [Trait("Category", "Phase29Baseline")]
    public void Compute_AndWriteJsonFixture()
    {
        // --- Drums kick (C2 = MIDI 36) ----------------------------------------
        var drumSynth = new DrumSynthesizer();
        var kickNote = new MusicalNoteData(
            noteName: 'C', octave: 2, alteration: 0,
            durationValue: 1, isRest: false, velocity: 0.9,
            articulation: Articulation.Normal);
        var kickBuffer = drumSynth.RenderNote(kickNote, SampleRate, DurationBeats, Bpm, RenderTuning.Default);
        double drumsKick = Phase29Fft.HarmonicRichnessRatio(kickBuffer, DrumsKickFundamentalHz);

        // --- Organ C4 ----------------------------------------------------------
        var organSynth = new OrganSynthesizer();
        var organNote = new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 0,
            durationValue: 1, isRest: false, velocity: 0.7,
            articulation: Articulation.Normal);
        var organBuffer = organSynth.RenderNote(organNote, SampleRate, DurationBeats, Bpm, RenderTuning.Default);
        double organC4 = Phase29Fft.HarmonicRichnessRatio(organBuffer, OrganC4FundamentalHz);

        // --- Wavetable C4 (default sine-shaped table to match the existing test
        //     convention — RegisterWavetable callers historically pass a soft
        //     single-cycle waveform). Use a canonical sawtooth table for the
        //     baseline; matches the "default" wavetable shape used by the
        //     SynthesizerFactory's RegisterWavetable fallback elsewhere in
        //     the codebase.
        var defaultTable = GeneratePhase28DefaultWavetable(2048);
        var wavetableSynth = new WavetableSynthesizer(defaultTable);
        var wavetableNote = new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 0,
            durationValue: 1, isRest: false, velocity: 0.7,
            articulation: Articulation.Normal);
        var wavetableBuffer = wavetableSynth.RenderNote(wavetableNote, SampleRate, DurationBeats, Bpm, RenderTuning.Default);
        double wavetableC4Default = Phase29Fft.HarmonicRichnessRatio(wavetableBuffer, WavetableC4FundamentalHz);

        Console.WriteLine($"Phase 28 baseline (drums_kick, MIDI 36, f₀ = {DrumsKickFundamentalHz:F2} Hz)    = {drumsKick:F6}");
        Console.WriteLine($"Phase 28 baseline (organ_c4,  f₀ = {OrganC4FundamentalHz:F2} Hz)               = {organC4:F6}");
        Console.WriteLine($"Phase 28 baseline (wavetable_c4_default, f₀ = {WavetableC4FundamentalHz:F2} Hz) = {wavetableC4Default:F6}");

        Assert.True(drumsKick > 0.0, "Drums kick richness should be positive; got 0 → Goertzel failure or silence.");
        Assert.True(organC4 > 0.0, "Organ C4 richness should be positive; got 0.");
        Assert.True(wavetableC4Default > 0.0, "Wavetable C4 richness should be positive; got 0.");

        string fixtureDir = ResolveSourceTreeFixturesDir();
        Directory.CreateDirectory(fixtureDir);
        string fixturePath = Path.Combine(fixtureDir, "phase28_harmonic_richness_baseline.json");

        // Pinned-baseline guard: once committed, the baseline JSON must NOT be
        // silently overwritten by a later filter-bypassed test invocation.
        // The values committed in Plan 05 Task 1 reflect the PRE-PLAN-05 synth
        // state — running this method after the synths have been upgraded (as
        // they were in Tasks 2-4) would silently rewrite the file with
        // post-upgrade values, and the Plan 05 Task 5 HarmonicRichnessTests
        // would trivially pass with a 1.00× "gain". Delete the file by hand
        // to deliberately regenerate (e.g. when starting a Phase 30+ baseline
        // reset).
        if (File.Exists(fixturePath))
        {
            Console.WriteLine($"[SKIP] Phase 28 baseline already pinned at: {fixturePath}");
            Console.WriteLine("       Delete the file first if a deliberate regeneration is intended.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("    \"comment\": \"Phase 28 baseline harmonic-richness ratios. Each value is Σ E(k·f₀ for k in 2..8) / E(f₀) computed via Phase29Fft.HarmonicRichnessRatio over 1.0 sec of rendering at 44.1 kHz, 120 BPM (= 2 beats). Phase 29 Plan 05 must show ≥ 1.20 × these values per instrument (REQ-6 / SPEC D-23).\",");
        sb.Append("    \"drums_kick\": ").Append(drumsKick.ToString("F6", CultureInfo.InvariantCulture)).AppendLine(",");
        sb.Append("    \"drums_kick_fundamental_hz\": ").Append(DrumsKickFundamentalHz.ToString("F2", CultureInfo.InvariantCulture)).AppendLine(",");
        sb.Append("    \"organ_c4\": ").Append(organC4.ToString("F6", CultureInfo.InvariantCulture)).AppendLine(",");
        sb.Append("    \"organ_c4_fundamental_hz\": ").Append(OrganC4FundamentalHz.ToString("F2", CultureInfo.InvariantCulture)).AppendLine(",");
        sb.Append("    \"wavetable_c4_default\": ").Append(wavetableC4Default.ToString("F6", CultureInfo.InvariantCulture)).AppendLine(",");
        sb.Append("    \"wavetable_c4_fundamental_hz\": ").Append(WavetableC4FundamentalHz.ToString("F2", CultureInfo.InvariantCulture)).AppendLine(",");
        sb.AppendLine("    \"baseline_computed_at\": \"2026-05-11\",");
        sb.AppendLine("    \"baseline_method\": \"Goertzel @ k·f₀ for k in 1..8, summed energy above ÷ energy at f₀\",");
        sb.AppendLine("    \"baseline_duration_seconds\": 1.0,");
        sb.AppendLine("    \"baseline_sample_rate\": 44100");
        sb.Append("}");

        File.WriteAllText(fixturePath, sb.ToString());
        Console.WriteLine($"Baseline JSON written to: {fixturePath}");
    }

    /// <summary>
    /// Walks up from AppContext.BaseDirectory (bin/Debug/net10.0) until the
    /// flow-lang.Tests source directory is found, then returns
    /// <c>{flow-lang.Tests}/Fixtures/Phase29</c>. We want the source-tree
    /// fixtures dir (committed to git), not a copy under bin/ — the JSON
    /// is a checked-in artifact, not a runtime asset.
    /// </summary>
    private static string ResolveSourceTreeFixturesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "flow-lang.Tests", "Fixtures", "Phase29");
            if (Directory.Exists(Path.Combine(dir.FullName, "flow-lang.Tests")))
            {
                Directory.CreateDirectory(candidate);
                return candidate;
            }
            // We may already be inside flow-lang.Tests/bin/Debug/net10.0 — climb past bin/ + Debug/ + net10.0/.
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate flow-lang.Tests source directory above AppContext.BaseDirectory.");
    }

    /// <summary>
    /// Generates the canonical Phase 28 "default" wavetable: a 2048-point
    /// sawtooth single-cycle waveform amplitude 1.0. Sawtooth is chosen for
    /// the baseline because it has rich harmonic content (theoretical
    /// 1/n falloff across all integer harmonics) — comparable to the most
    /// common Phase 28 user wavetable; pinning a richer shape here makes
    /// the Phase 29 +20% target more meaningful (Plan 05 Task 4 explicitly
    /// upgrades the "default" wavetable shape to include richer partials,
    /// or any equivalent improvement that adds upper-partial energy).
    /// </summary>
    private static float[] GeneratePhase28DefaultWavetable(int size)
    {
        var table = new float[size];
        for (int i = 0; i < size; i++)
        {
            double phase = (double)i / size;        // 0..1
            table[i] = (float)(2.0 * phase - 1.0);  // −1..+1 sawtooth
        }
        return table;
    }
}
