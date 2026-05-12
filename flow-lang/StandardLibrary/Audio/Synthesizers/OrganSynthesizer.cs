using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Hammond-style organ synthesizer using additive synthesis with a vowel-formant
/// filter bank.
///
/// Phase 28 (baseline): additive sine waves at drawbar harmonic ratios
/// (16', 8', 5-1/3', 4', 2-2/3', 2').
///
/// Phase 29 Plan 05 (REQ-6 / SPEC D-21): the drawbar-additive tone is mixed
/// 50/50 with a copy passed through a 3-formant bandpass bank emulating the
/// "Aaaa" vowel (F1 ≈ 700 Hz, F2 ≈ 1220 Hz, F3 ≈ 2600 Hz, Q ≈ 5). This adds
/// vocal-like resonance peaks at frequencies that map to high partials of
/// most musical pitches, lifting the harmonic-richness ratio ≥ 20% vs the
/// Phase 28 baseline while retaining the organ-pipe character of the dry
/// drawbar mix.
///
/// Formant frequencies sourced from the standard IPA vowel-formant chart
/// (open central /a/) — 700 / 1220 / 2600 Hz are the commonly-cited adult
/// values; small variation across published tables is acoustically inaudible
/// for this application.
/// </summary>
public class OrganSynthesizer : INoteSynthesizer
{
    // Drawbar harmonic ratios and their relative amplitudes (Phase 28).
    private static readonly (double ratio, double amplitude)[] Drawbars =
    {
        (1.0, 1.0),   // 16'  - fundamental
        (2.0, 0.8),   // 8'   - octave
        (3.0, 0.6),   // 5-1/3' - twelfth
        (4.0, 0.5),   // 4'   - two octaves
        (6.0, 0.3),   // 2-2/3' - octave + fifth
        (8.0, 0.2),   // 2'   - three octaves
    };

    // Phase 29 SPEC D-21 "Aaaa" formant set (Hz, Q).
    private const double Formant1Hz = 700.0;
    private const double Formant2Hz = 1220.0;
    private const double Formant3Hz = 2600.0;
    private const double FormantQ = 5.0;
    // 50/50 dry/wet mix between the drawbar additive output and the
    // formant-filtered output. Tuned so the formant adds vocal-like
    // resonance without obscuring the underlying tonewheel character.
    private const double FormantMix = 0.5;

    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
    {
        if (note.IsRest)
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        double frequency = PitchConversion.NoteToFrequency(note, tuning);
        double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
        int numSamples = (int)(durationSeconds * sampleRate);
        if (numSamples <= 0)
            return new AudioBuffer(0, 1, sampleRate);

        var dry = new float[numSamples];
        double baseAmp = 0.08 * note.Velocity;
        double nyquist = sampleRate / 2.0;

        // ---- Drawbar additive synthesis (Phase 28 path, unchanged) ----
        foreach (var (ratio, amp) in Drawbars)
        {
            double partialFreq = frequency * ratio;
            if (partialFreq >= nyquist)
                continue;

            SynthUtils.GenerateSine(dry, partialFreq, baseAmp * amp, sampleRate);
        }

        // ---- Phase 29 SPEC D-21: formant filter bank ----
        // Render the dry tone through 3 parallel bandpass filters centred on
        // the "Aaaa" formant frequencies, sum the formant outputs, then mix
        // 50/50 with the dry signal.
        var formantOut = new float[numSamples];
        // Each bandpass is independent — we add their outputs into a single
        // float[] without modifying the dry buffer.
        ApplyBandpassAdditive(dry, formantOut, Formant1Hz, FormantQ, sampleRate);
        ApplyBandpassAdditive(dry, formantOut, Formant2Hz, FormantQ, sampleRate);
        ApplyBandpassAdditive(dry, formantOut, Formant3Hz, FormantQ, sampleRate);

        // Mix: out = (1 − mix) × dry + mix × formant. Tuned so the formant peaks
        // ride alongside the drawbar tone without overpowering it.
        var samples = new float[numSamples];
        for (int i = 0; i < numSamples; i++)
            samples[i] = (float)((1.0 - FormantMix) * dry[i] + FormantMix * formantOut[i]);

        // Near-instant attack, full sustain, minimal release (organ key click character).
        // Phase 28 SPEC-5: articulation-aware. Baseline: attack 0.005, decay 0.01,
        // sustain 1.0, release 0.01.
        float[] envelope = SynthUtils.GenerateArticulationADSR(
            note.Articulation,
            baseAttack: 0.005, baseDecay: 0.01, baseSustain: 1.0, baseRelease: 0.01,
            frames: numSamples, sampleRate: sampleRate);
        SynthUtils.ApplyEnvelope(samples, envelope);

        return SynthUtils.ToMonoBuffer(samples, sampleRate);
    }

    /// <summary>
    /// Direct-Form-I biquad bandpass that ADDS its output to <paramref name="dest"/>
    /// (instead of returning a new array). Coefficients computed inline to avoid
    /// the DSP.Filter.Bandpass AudioBuffer allocation. Same constant-skirt-gain
    /// formula as DSP.Filter.ComputeBandpassCoefficients (Phase 22).
    /// </summary>
    private static void ApplyBandpassAdditive(float[] source, float[] dest, double centerHz, double q, int sampleRate)
    {
        double nyquist = sampleRate / 2.0;
        if (centerHz <= 0 || centerHz >= nyquist) return;

        double w0 = 2.0 * System.Math.PI * centerHz / sampleRate;
        double cosW0 = System.Math.Cos(w0);
        double sinW0 = System.Math.Sin(w0);
        double alpha = sinW0 / (2.0 * q);

        double a0 = 1.0 + alpha;
        double b0 = alpha / a0;
        // b1 = 0 for bandpass
        double b2 = -alpha / a0;
        double a1 = -2.0 * cosW0 / a0;
        double a2 = (1.0 - alpha) / a0;

        double x1 = 0.0, x2 = 0.0;
        double y1 = 0.0, y2 = 0.0;
        for (int i = 0; i < source.Length; i++)
        {
            double x0 = source[i];
            double y0 = b0 * x0 + b2 * x2 - a1 * y1 - a2 * y2;
            // Denormal guard (matches DSP.Filter ApplyBiquad pattern).
            if (double.IsSubnormal(y0)) y0 = 0.0;
            dest[i] += (float)y0;
            x2 = x1; x1 = x0;
            y2 = y1; y1 = y0;
        }
    }
}
