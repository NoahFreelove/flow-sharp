using System;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Shared utilities for instrument synthesizers: oscillators, noise, envelopes, filters, and buffer helpers.
/// All oscillator methods are additive (+=) so harmonics can be layered into the same buffer.
/// </summary>
public static class SynthUtils
{
    // Synth white-noise RNG. Seeded with a fixed value for cross-render
    // determinism (Phase 15 Plan 05, ROADMAP criterion #2 / D-18). Pre-fix:
    // unseeded `new()` produced different per-render hammer transient and
    // breath-noise samples on every call — same audio quality, but raw
    // sample bytes never matched between renders. Decorrelation across
    // samples within one render still holds; the seed simply pins the
    // sequence so two renderSong calls produce byte-identical buffers.
    private const int SynthNoiseSeed = 0x55EED;
    private static Random Rng = new Random(SynthNoiseSeed);

    /// <summary>
    /// Resets the white-noise RNG to its fixed seed. Called by SongRenderer at
    /// the start of every <c>renderSong</c> so that consecutive renders of the
    /// same Song produce byte-identical buffers (Plan 15-05 ROADMAP #2).
    /// </summary>
    public static void ResetNoiseRng() => Rng = new Random(SynthNoiseSeed);

    /// <summary>
    /// Converts a beat duration to seconds given a BPM.
    /// </summary>
    public static double BeatsToSeconds(double beats, double bpm)
    {
        return (beats / bpm) * 60.0;
    }

    /// <summary>
    /// Creates a silent mono AudioBuffer for the given duration in beats.
    /// </summary>
    public static AudioBuffer CreateSilence(int sampleRate, double durationBeats, double bpm)
    {
        double durationSeconds = BeatsToSeconds(durationBeats, bpm);
        int numSamples = (int)(durationSeconds * sampleRate);
        return new AudioBuffer(numSamples, 1, sampleRate);
    }

    /// <summary>
    /// Additively generates a sine wave into the buffer. Returns the ending phase.
    /// </summary>
    public static double GenerateSine(float[] buffer, double frequency, double amplitude, int sampleRate, double startPhase = 0.0)
    {
        double phaseInc = 2.0 * Math.PI * frequency / sampleRate;
        double phase = startPhase;

        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] += (float)(amplitude * Math.Sin(phase));
            phase += phaseInc;
        }

        return phase;
    }

    /// <summary>
    /// Additively generates a sawtooth wave into the buffer. Returns the ending phase (0..1).
    /// </summary>
    public static double GenerateSaw(float[] buffer, double frequency, double amplitude, int sampleRate, double startPhase = 0.0)
    {
        double phaseInc = frequency / sampleRate;
        double phase = startPhase;

        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] += (float)(amplitude * (2.0 * phase - 1.0));
            phase += phaseInc;
            if (phase >= 1.0) phase -= 1.0;
        }

        return phase;
    }

    /// <summary>
    /// Additively generates a square wave into the buffer. Returns the ending phase (0..1).
    /// </summary>
    public static double GenerateSquare(float[] buffer, double frequency, double amplitude, int sampleRate, double startPhase = 0.0)
    {
        double phaseInc = frequency / sampleRate;
        double phase = startPhase;

        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] += (float)(amplitude * (phase < 0.5 ? 1.0 : -1.0));
            phase += phaseInc;
            if (phase >= 1.0) phase -= 1.0;
        }

        return phase;
    }

    /// <summary>
    /// Additively generates a triangle wave into the buffer. Returns the ending phase (0..1).
    /// </summary>
    public static double GenerateTriangle(float[] buffer, double frequency, double amplitude, int sampleRate, double startPhase = 0.0)
    {
        double phaseInc = frequency / sampleRate;
        double phase = startPhase;

        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] += (float)(amplitude * (phase < 0.5 ? 4.0 * phase - 1.0 : 3.0 - 4.0 * phase));
            phase += phaseInc;
            if (phase >= 1.0) phase -= 1.0;
        }

        return phase;
    }

    /// <summary>
    /// Fills the buffer with white noise at the given amplitude (additive).
    /// </summary>
    public static void GenerateWhiteNoise(float[] buffer, double amplitude)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] += (float)(amplitude * (Rng.NextDouble() * 2.0 - 1.0));
        }
    }

    /// <summary>
    /// Generates an ADSR envelope curve as a float array using the existing EnvelopeProcessor.
    /// </summary>
    public static float[] GenerateADSR(double attack, double decay, double sustain, double release, int frames, int sampleRate)
    {
        var parameters = new double[] { attack, decay, sustain, release };
        var envelope = new Envelope(EnvelopeKind.ADSR, parameters, sampleRate);
        return EnvelopeProcessor.GenerateEnvelopeCurve(envelope, frames);
    }

    /// <summary>
    /// Phase 28 (SPEC-5): Articulation-aware ADSR. Layers the LOCKED per-articulation
    /// envelope shaping rules from SPEC-5 on top of the synthesizer's baseline ADSR.
    ///
    /// Locked rules:
    ///   Staccato + Marcato: attack × 0.66 (1.5× faster), sustain = 0, release × 0.5
    ///   Tenuto:             release × 1.2 (soft release)
    ///   Legato + Accent + Sforzando + Normal: synth-default ADSR
    ///   Sforzando ALSO multiplies the curve by 1.5×→1.0× over the first 15% of frames
    ///
    /// Composition with Plan 28-02:
    ///   • <see cref="BarRenderer"/> already shortened the rendered duration via the
    ///     SPEC-4 multipliers (Staccato/Marcato 0.25, Legato 1.10) BEFORE this curve
    ///     is generated. The two layers compose: shortened buffer × shaped envelope.
    ///   • <see cref="NoteStreamCompiler"/> already applied the +0.30 velocity boost
    ///     for Accent/Marcato. Sforzando velocity passes through here unchanged
    ///     because the spike is time-varying — the multiplier curve below.
    ///
    /// Drum synth passes <paramref name="isPercussion"/> = true to bypass shaping
    /// (drums are inherently percussive — articulation rules are no-ops per SPEC-5).
    /// </summary>
    public static float[] GenerateArticulationADSR(
        Articulation articulation,
        double baseAttack, double baseDecay, double baseSustain, double baseRelease,
        int frames, int sampleRate, bool isPercussion = false)
    {
        if (isPercussion)
            return GenerateADSR(baseAttack, baseDecay, baseSustain, baseRelease, frames, sampleRate);

        double attack = baseAttack;
        double decay = baseDecay;
        double sustain = baseSustain;
        double release = baseRelease;

        switch (articulation)
        {
            case Articulation.Staccato:
            case Articulation.Marcato:
                attack  = baseAttack  * 0.66; // 1.5× faster — sharper transient
                sustain = 0.0;                // zero sustain — pure attack-then-decay
                release = baseRelease * 0.5;  // fast release
                break;
            case Articulation.Tenuto:
                release = baseRelease * 1.2; // soft release — held to full value
                break;
            case Articulation.Legato:
            case Articulation.Accent:
            case Articulation.Sforzando:
            case Articulation.Normal:
            default:
                // Synth-default ADSR. Sforzando spike applies post-curve below.
                break;
        }

        var curve = GenerateADSR(attack, decay, sustain, release, frames, sampleRate);

        if (articulation == Articulation.Sforzando)
        {
            // 1.5×→1.0× linear decay over the first 15% of frames. Replaces the prior
            // static `velocity = 0.95` override removed in Plan 28-02 — composer's base
            // velocity is preserved and the spike is purely envelope-side.
            int spikeFrames = Math.Max(1, (int)(frames * 0.15));
            for (int i = 0; i < spikeFrames; i++)
            {
                float t = (float)i / spikeFrames;
                float multiplier = 1.5f * (1.0f - t) + 1.0f * t;
                curve[i] *= multiplier;
            }
        }

        return curve;
    }

    /// <summary>
    /// Applies an envelope curve to a sample buffer in-place (multiply).
    /// Samples beyond the envelope length are zeroed.
    /// </summary>
    public static void ApplyEnvelope(float[] buffer, float[] envelope)
    {
        int envLen = envelope.Length;
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] *= i < envLen ? envelope[i] : 0f;
        }
    }

    /// <summary>
    /// Cheap one-pole lowpass filter applied in-place.
    /// Avoids allocating AudioBuffer objects like the biquad Filter class does.
    /// </summary>
    public static void OnePoleLP(float[] buffer, double cutoffHz, int sampleRate)
    {
        if (cutoffHz <= 0 || buffer.Length == 0) return;

        double rc = 1.0 / (2.0 * Math.PI * cutoffHz);
        double dt = 1.0 / sampleRate;
        double alpha = dt / (rc + dt);

        float prev = buffer[0];
        for (int i = 1; i < buffer.Length; i++)
        {
            prev += (float)(alpha * (buffer[i] - prev));
            buffer[i] = prev;
        }
    }

    /// <summary>
    /// Copies a float sample array into a mono AudioBuffer.
    /// </summary>
    public static AudioBuffer ToMonoBuffer(float[] samples, int sampleRate)
    {
        var buf = new AudioBuffer(samples.Length, 1, sampleRate);
        Array.Copy(samples, buf.Data, samples.Length);
        return buf;
    }
}
