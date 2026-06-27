using System;
using System.Globalization;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Synthesizers;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.Tests.Helpers;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Tools;

/// <summary>
/// Sanity check — runs each retained-synth instrument and prints the current
/// harmonic-richness ratio. Useful during Plan 05 Tasks 2-4 development to
/// confirm we hit the 20% gain target before wiring the full
/// HarmonicRichnessTests assertion file.
///
/// Tagged Category="Phase29Verify" so it doesn't run in regular CI.
/// </summary>
public class VerifyRichnessGain
{
    [Fact]
    [Trait("Category", "Phase29Verify")]
    public void Print_Current_RichnessRatios()
    {
        const int sr = 44100;
        const double bpm = 120.0;
        const double beats = 2.0;

        // Drums kick
        var drums = new DrumSynthesizer();
        var kick = new MusicalNoteData('C', 2, 0, 1, false, velocity: 0.9, articulation: Articulation.Normal);
        var kickBuf = drums.RenderNote(kick, sr, beats, bpm, RenderTuning.Default);
        double r1 = Phase29Fft.HarmonicRichnessRatio(kickBuf, 50.0);

        // Organ
        var organ = new OrganSynthesizer();
        var organNote = new MusicalNoteData('C', 4, 0, 1, false, velocity: 0.7, articulation: Articulation.Normal);
        var organBuf = organ.RenderNote(organNote, sr, beats, bpm, RenderTuning.Default);
        double r2 = Phase29Fft.HarmonicRichnessRatio(organBuf, 261.63);

        // Wavetable default (Phase 28 plain sawtooth)
        var table = new float[2048];
        for (int i = 0; i < table.Length; i++) table[i] = (float)(2.0 * i / table.Length - 1.0);
        var wt = new WavetableSynthesizer(table);
        var wtNote = new MusicalNoteData('C', 4, 0, 1, false, velocity: 0.7, articulation: Articulation.Normal);
        var wtBuf = wt.RenderNote(wtNote, sr, beats, bpm, RenderTuning.Default);
        double r3 = Phase29Fft.HarmonicRichnessRatio(wtBuf, 261.63);

        // Wavetable variants (Phase 29) — via factory so we hit the registration gate.
        var warmSynth = SynthesizerFactory.Create("warm");
        var warmBuf = warmSynth.RenderNote(wtNote, sr, beats, bpm, RenderTuning.Default);
        double r4 = Phase29Fft.HarmonicRichnessRatio(warmBuf, 261.63);

        var brightSynth = SynthesizerFactory.Create("bright");
        var brightBuf = brightSynth.RenderNote(wtNote, sr, beats, bpm, RenderTuning.Default);
        double r5 = Phase29Fft.HarmonicRichnessRatio(brightBuf, 261.63);

        var buzzSynth = SynthesizerFactory.Create("buzz");
        var buzzBuf = buzzSynth.RenderNote(wtNote, sr, beats, bpm, RenderTuning.Default);
        double r6 = Phase29Fft.HarmonicRichnessRatio(buzzBuf, 261.63);

        string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "phase29_verify_richness.txt");
        System.IO.File.WriteAllText(tmp,
            $"Drums kick:           {r1.ToString("F4", CultureInfo.InvariantCulture)}\n" +
            $"Organ C4:             {r2.ToString("F4", CultureInfo.InvariantCulture)}\n" +
            $"Wavetable saw:        {r3.ToString("F4", CultureInfo.InvariantCulture)}\n" +
            $"Wavetable warm:       {r4.ToString("F4", CultureInfo.InvariantCulture)}\n" +
            $"Wavetable bright:     {r5.ToString("F4", CultureInfo.InvariantCulture)}\n" +
            $"Wavetable buzz:       {r6.ToString("F4", CultureInfo.InvariantCulture)}\n");
    }
}
