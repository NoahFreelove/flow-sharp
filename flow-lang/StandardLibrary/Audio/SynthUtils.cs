using System;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Shared utilities for instrument synthesizers: oscillators, noise, envelopes, filters, and buffer helpers.
/// All oscillator methods are additive (+=) so harmonics can be layered into the same buffer.
/// </summary>
public static class SynthUtils
{
    private static readonly Random Rng = new();

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
