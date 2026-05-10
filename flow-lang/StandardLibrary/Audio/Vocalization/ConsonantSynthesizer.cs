using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.StandardLibrary.Audio.Synthesizers;

namespace FlowLang.StandardLibrary.Audio.Vocalization;

/// <summary>
/// Synthesizes consonant onset sounds: fricatives (s), plosives (t), and nasals (n).
/// These are short transient sounds that precede vowels in syllable synthesis.
/// </summary>
public static class ConsonantSynthesizer
{
    /// <summary>
    /// Generates consonant onset samples for the given consonant type.
    /// </summary>
    /// <param name="consonant">Consonant identifier: "s", "t", or "n".</param>
    /// <param name="pitchHz">Pitch frequency in Hz (used for pitched consonants like nasals).</param>
    /// <param name="sampleRate">Sample rate.</param>
    /// <returns>Float array of consonant onset samples.</returns>
    public static float[] Generate(string consonant, double pitchHz, int sampleRate)
    {
        return consonant switch
        {
            "s" => GenerateFricative(sampleRate),
            "t" => GeneratePlosive(sampleRate),
            "n" => GenerateNasal(pitchHz, sampleRate),
            _ => Array.Empty<float>()
        };
    }

    /// <summary>
    /// Generates a fricative consonant (e.g., "s") using filtered white noise.
    /// Duration: 80ms.
    /// </summary>
    private static float[] GenerateFricative(int sampleRate)
    {
        int samples = sampleRate * 80 / 1000;
        float[] buffer = new float[samples];

        // Fill with white noise
        SynthUtils.GenerateWhiteNoise(buffer, 0.3);

        // Apply highpass filter at 4kHz for sibilant character
        var tempBuf = new AudioBuffer(samples, 1, sampleRate);
        Array.Copy(buffer, tempBuf.Data, samples);
        var filtered = Filter.Highpass(tempBuf, 4000f);
        Array.Copy(filtered.Data, buffer, samples);

        // Fade-in (2ms) to avoid clicks
        int fadeIn = sampleRate * 2 / 1000;
        for (int i = 0; i < fadeIn && i < samples; i++)
        {
            buffer[i] *= (float)i / fadeIn;
        }

        // Fade-out (10ms) to avoid clicks
        int fadeOut = sampleRate * 10 / 1000;
        for (int i = 0; i < fadeOut && i < samples; i++)
        {
            int idx = samples - 1 - i;
            buffer[idx] *= (float)i / fadeOut;
        }

        return buffer;
    }

    /// <summary>
    /// Generates a plosive consonant (e.g., "t") using a short noise burst with exponential decay.
    /// Duration: 10ms.
    /// </summary>
    private static float[] GeneratePlosive(int sampleRate)
    {
        int samples = sampleRate * 10 / 1000;
        float[] buffer = new float[samples];

        // Short noise burst
        SynthUtils.GenerateWhiteNoise(buffer, 0.5);

        // Sharp exponential decay envelope
        for (int i = 0; i < samples; i++)
        {
            buffer[i] *= (float)Math.Exp(-5.0 * i / samples);
        }

        return buffer;
    }

    /// <summary>
    /// Generates a nasal consonant (e.g., "n") using formant-filtered buzz
    /// with nasal resonances and an anti-formant notch.
    /// Duration: 150ms.
    /// </summary>
    private static float[] GenerateNasal(double pitchHz, int sampleRate)
    {
        int samples = sampleRate * 150 / 1000;

        // Pre-roll for filter settling
        int preRoll = sampleRate * 50 / 1000;
        int extended = preRoll + samples;
        float[] buzz = new float[extended];

        // Voiced buzz source
        SynthUtils.GenerateSaw(buzz, pitchHz, 0.6, sampleRate);
        SynthUtils.OnePoleLP(buzz, pitchHz * 4, sampleRate);

        var buzzBuf = new AudioBuffer(extended, 1, sampleRate);
        Array.Copy(buzz, buzzBuf.Data, extended);

        // Nasal formants: strong low nasal resonance + weak high nasal resonance
        // F1: ~270Hz (nasal murmur), F2: ~2000Hz (alveolar "n" character)
        var nasalF1 = Filter.Bandpass(buzzBuf, 200f, 350f);
        var nasalF2 = Filter.Bandpass(buzzBuf, 1800f, 2500f);

        float[] result = new float[extended];
        for (int i = 0; i < extended; i++)
        {
            // Strong nasal resonance + subtle high formant
            result[i] = nasalF1.Data[i] * 0.8f + nasalF2.Data[i] * 0.15f;
        }

        // Anti-formant: notch out 400-600Hz to distinguish from vowel
        // (nasals have zeros where vowels have formants)
        var resultBuf = new AudioBuffer(extended, 1, sampleRate);
        Array.Copy(result, resultBuf.Data, extended);
        var antiF = Filter.Bandpass(resultBuf, 400f, 600f);
        for (int i = 0; i < extended; i++)
        {
            result[i] -= antiF.Data[i] * 0.6f;
        }

        // Discard pre-roll
        float[] buffer = new float[samples];
        Array.Copy(result, preRoll, buffer, 0, samples);

        // Raised cosine fade-in over 2 cycles
        int fadeIn = (int)(2.0 * sampleRate / pitchHz);
        fadeIn = Math.Min(fadeIn, samples);
        for (int i = 0; i < fadeIn; i++)
        {
            float t = (float)(0.5 * (1.0 - Math.Cos(Math.PI * i / fadeIn)));
            buffer[i] *= t;
        }

        // Fade-out (10ms)
        int fadeOut = sampleRate * 10 / 1000;
        for (int i = 0; i < fadeOut && i < samples; i++)
        {
            int idx = samples - 1 - i;
            buffer[idx] *= (float)i / fadeOut;
        }

        return buffer;
    }

    /// <summary>
    /// Checks if a character is a recognized consonant.
    /// </summary>
    public static bool IsConsonant(char c)
    {
        return c == 's' || c == 't' || c == 'n';
    }
}
