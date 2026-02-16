using System;
using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// MIDI-style saxophone synthesizer modeled after the acoustic properties of a real sax.
/// Uses a sawtooth-based reed tone with even and odd harmonics, breath noise,
/// formant resonances at ~500 Hz, ~1400 Hz, and ~2800 Hz, vibrato with delayed onset,
/// and a sub-harmonic growl layer for body. The result is a warm, buzzy, expressive
/// saxophone timbre rather than a clean pipe/flute sound.
/// </summary>
public class SaxSynthesizer : INoteSynthesizer
{
    private const double VibratoRate = 5.0;       // Hz — typical sax vibrato
    private const double VibratoDepth = 0.005;    // ~8.6 cents — wider than flute
    private const double VibratoOnsetBeats = 0.3; // Vibrato fades in after initial attack

    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm)
    {
        if (note.IsRest)
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        double frequency = PitchConversion.NoteToFrequency(note);
        double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
        int numSamples = (int)(durationSeconds * sampleRate);
        if (numSamples <= 0)
            return new AudioBuffer(0, 1, sampleRate);

        var samples = new float[numSamples];

        // --- 1. Reed tone: sawtooth with delayed-onset vibrato ---
        // Saxophones produce a sawtooth-like waveform from the vibrating reed,
        // containing both even and odd harmonics (unlike a flute's mostly-odd spectrum).
        double vibratoOnsetSec = SynthUtils.BeatsToSeconds(VibratoOnsetBeats, bpm);
        double phase = 0.0;
        for (int i = 0; i < numSamples; i++)
        {
            double t = i / (double)sampleRate;

            // Vibrato fades in smoothly after the attack
            double vibEnv = t < vibratoOnsetSec ? t / vibratoOnsetSec : 1.0;
            double vibrato = 1.0 + VibratoDepth * vibEnv * Math.Sin(2.0 * Math.PI * VibratoRate * t);
            double freq = frequency * vibrato;
            double phaseInc = freq / sampleRate;

            // Sawtooth: (2 * phase - 1)
            samples[i] = (float)(0.22 * (2.0 * phase - 1.0));
            phase += phaseInc;
            if (phase >= 1.0) phase -= 1.0;
        }

        // --- 2. Additional harmonics for reed buzz ---
        // 2nd harmonic (octave) — even harmonics are what distinguish sax from clarinet
        SynthUtils.GenerateSine(samples, frequency * 2, 0.08, sampleRate);
        // 3rd harmonic — adds nasal quality
        SynthUtils.GenerateSine(samples, frequency * 3, 0.05, sampleRate);
        // 4th harmonic — brightness
        SynthUtils.GenerateSine(samples, frequency * 4, 0.03, sampleRate);

        // --- 3. Sub-harmonic growl layer ---
        // Sax players often produce a subtle sub-octave through reed buzz/multiphonics.
        // This adds warmth and body that pure saw lacks.
        SynthUtils.GenerateSine(samples, frequency * 0.5, 0.03, sampleRate);

        // --- 4. Breath noise ---
        // Saxophones have significant air turbulence noise, especially in the attack.
        // We generate broadband noise and shape it with its own fast-decay envelope.
        var breathNoise = new float[numSamples];
        SynthUtils.GenerateWhiteNoise(breathNoise, 0.06);
        float[] breathEnv = SynthUtils.GenerateADSR(
            attack: 0.01, decay: 0.08, sustain: 0.15, release: 0.05,
            frames: numSamples, sampleRate: sampleRate);
        SynthUtils.ApplyEnvelope(breathNoise, breathEnv);
        // Bandlimit the breath noise to the upper register (1kHz - 8kHz)
        SynthUtils.OnePoleLP(breathNoise, 8000.0, sampleRate);
        for (int i = 0; i < numSamples; i++)
            samples[i] += breathNoise[i];

        // --- 5. Main ADSR envelope ---
        // Sax has a moderately fast attack (tongue articulation), sustained tone,
        // and a medium release as the reed stops vibrating.
        float[] envelope = SynthUtils.GenerateADSR(
            attack: 0.03, decay: 0.06, sustain: 0.75, release: 0.10,
            frames: numSamples, sampleRate: sampleRate);
        SynthUtils.ApplyEnvelope(samples, envelope);

        // --- 6. Formant filtering ---
        // The sax body has characteristic resonances. We apply multiple filter stages:
        // - Lowpass to tame extreme highs (sax is warm, not shrill)
        // - Formant peak around vocal tract region for that "vocal" sax quality
        SynthUtils.OnePoleLP(samples, 4000.0 + frequency * 0.5, sampleRate);

        // Formant resonance via bandpass — centered around the sax body resonance.
        // Real sax formants sit around 500 Hz, 1400 Hz, and 2800 Hz.
        // We approximate the dominant formant with a bandpass that preserves the
        // midrange warmth while cutting sub-bass mud and extreme highs.
        float nyquist = sampleRate / 2f;
        float bpLow = Math.Max(250f, (float)(frequency * 0.6));
        float bpHigh = (float)Math.Min(frequency * 8.0, nyquist - 1.0);
        bpHigh = Math.Min(bpHigh, 6000f); // Cap to keep it warm

        if (bpHigh > bpLow + 10f)
        {
            // Mix filtered with dry to retain some body (70% filtered, 30% dry)
            var tempBuf = SynthUtils.ToMonoBuffer(samples, sampleRate);
            var filtered = Filter.Bandpass(tempBuf, bpLow, bpHigh);
            for (int i = 0; i < numSamples; i++)
                samples[i] = filtered.Data[i] * 0.7f + samples[i] * 0.3f;
        }

        return SynthUtils.ToMonoBuffer(samples, sampleRate);
    }
}
