using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.StandardLibrary.Audio.Synthesizers;

namespace FlowLang.StandardLibrary.Audio.Vocalization;

/// <summary>
/// Core formant synthesis engine. Produces vowel sounds by filtering a buzz source
/// (sawtooth oscillator) through parallel bandpass filters tuned to vowel formant
/// frequencies. Kraftwerk-style vocal synthesis.
/// </summary>
public static class FormantSynthesizer
{
    /// <summary>
    /// Synthesizes a vowel sound at the given pitch and duration.
    /// Uses parallel bandpass filtering of a sawtooth buzz source.
    /// </summary>
    /// <param name="vowel">Vowel phoneme key (ah, ee, eh, oh, oo).</param>
    /// <param name="frequencyHz">Fundamental frequency in Hz.</param>
    /// <param name="durationSeconds">Duration in seconds.</param>
    /// <param name="sampleRate">Sample rate (default 44100).</param>
    /// <returns>Mono AudioBuffer containing the synthesized vowel.</returns>
    public static AudioBuffer SynthesizeVowel(string vowel, double frequencyHz, double durationSeconds, int sampleRate = 44100)
    {
        int numSamples = (int)(durationSeconds * sampleRate);
        if (numSamples <= 0)
            return new AudioBuffer(0, 1, sampleRate);

        // Generate buzz source (sawtooth oscillator)
        float[] buzzSamples = new float[numSamples];
        SynthUtils.GenerateSaw(buzzSamples, frequencyHz, 0.8, sampleRate);

        // Apply spectral tilt to simulate glottal source roll-off
        SynthUtils.OnePoleLP(buzzSamples, frequencyHz * 4, sampleRate);

        // Wrap buzz in an AudioBuffer for Filter.Bandpass
        var buzzBuffer = new AudioBuffer(numSamples, 1, sampleRate);
        Array.Copy(buzzSamples, buzzBuffer.Data, numSamples);

        // Get formant data for the requested vowel
        var formants = FormantData.GetFormants(vowel);

        // Sum filtered formant bands into result
        var result = new AudioBuffer(numSamples, 1, sampleRate);

        foreach (var formant in formants)
        {
            // Compute bandpass range from formant center and bandwidth
            float lowHz = formant.Frequency - formant.Bandwidth / 2f;
            float highHz = formant.Frequency + formant.Bandwidth / 2f;

            // Clamp to valid range for Filter.Bandpass
            lowHz = Math.Max(lowHz, 20f);
            highHz = Math.Min(highHz, sampleRate / 2f - 1f);

            // Skip invalid ranges
            if (highHz <= lowHz)
                continue;

            var filtered = Filter.Bandpass(buzzBuffer, lowHz, highHz);
            float gain = FormantData.DbToLinear(formant.AmplitudeDb);

            for (int i = 0; i < result.Data.Length && i < filtered.Data.Length; i++)
            {
                result.Data[i] += filtered.Data[i] * gain;
            }
        }

        // Apply ADSR envelope
        float[] envelope = SynthUtils.GenerateADSR(
            attack: 0.02, decay: 0.05, sustain: 0.8, release: 0.05,
            frames: numSamples, sampleRate: sampleRate);
        SynthUtils.ApplyEnvelope(result.Data, envelope);

        // Apply master gain to prevent clipping
        for (int i = 0; i < result.Data.Length; i++)
        {
            result.Data[i] *= 0.3f;
        }

        return result;
    }

    /// <summary>
    /// Synthesizes a syllable (optional consonant onset + vowel body).
    /// Supports consonant-vowel combinations like "na", "ta", "sa".
    /// </summary>
    /// <param name="phoneme">Phoneme string (e.g., "ah", "na", "ta", "see").</param>
    /// <param name="frequencyHz">Fundamental frequency in Hz.</param>
    /// <param name="durationSeconds">Duration in seconds.</param>
    /// <param name="sampleRate">Sample rate (default 44100).</param>
    /// <returns>Mono AudioBuffer containing the synthesized syllable.</returns>
    public static AudioBuffer SynthesizeSyllable(string phoneme, double frequencyHz, double durationSeconds, int sampleRate = 44100)
    {
        // Check if it's a known vowel directly
        if (phoneme.Length <= 2 && IsVowelPhoneme(phoneme))
            return SynthesizeVowel(phoneme, frequencyHz, durationSeconds, sampleRate);

        // Check for consonant-vowel pattern
        if (phoneme.Length >= 2 && ConsonantSynthesizer.IsConsonant(phoneme[0]))
        {
            string consonant = phoneme[0].ToString();
            string remaining = phoneme[1..];

            // Map single vowel chars to phoneme names
            string vowelName = MapToVowelPhoneme(remaining);

            // Generate consonant onset
            float[] consonantSamples = ConsonantSynthesizer.Generate(consonant, frequencyHz, sampleRate);

            // Generate vowel body
            AudioBuffer vowelBuffer = SynthesizeVowel(vowelName, frequencyHz, durationSeconds, sampleRate);

            // Crossfade: overlap last 15ms of consonant with first 15ms of vowel
            int crossfadeSamples = sampleRate * 15 / 1000; // 15ms

            if (consonantSamples.Length == 0)
                return vowelBuffer;

            if (vowelBuffer.Frames == 0)
                return SynthUtils.ToMonoBuffer(consonantSamples, sampleRate);

            // Ensure crossfade doesn't exceed either buffer
            crossfadeSamples = Math.Min(crossfadeSamples, consonantSamples.Length);
            crossfadeSamples = Math.Min(crossfadeSamples, vowelBuffer.Frames);

            int consonantNonOverlap = consonantSamples.Length - crossfadeSamples;
            int vowelNonOverlap = vowelBuffer.Frames - crossfadeSamples;
            int totalSamples = consonantNonOverlap + crossfadeSamples + vowelNonOverlap;

            float[] combined = new float[totalSamples];

            // Copy consonant (before crossfade region)
            Array.Copy(consonantSamples, 0, combined, 0, consonantNonOverlap);

            // Crossfade region: linear blend
            for (int i = 0; i < crossfadeSamples; i++)
            {
                float t = (float)i / crossfadeSamples;
                float cSample = consonantSamples[consonantNonOverlap + i];
                float vSample = vowelBuffer.Data[i];
                combined[consonantNonOverlap + i] = cSample * (1f - t) + vSample * t;
            }

            // Copy vowel (after crossfade region)
            Array.Copy(vowelBuffer.Data, crossfadeSamples, combined, consonantNonOverlap + crossfadeSamples, vowelNonOverlap);

            return SynthUtils.ToMonoBuffer(combined, sampleRate);
        }

        // Fallback: treat entire string as vowel phoneme name
        return SynthesizeVowel(phoneme, frequencyHz, durationSeconds, sampleRate);
    }

    /// <summary>
    /// Checks if a string is a known vowel phoneme.
    /// </summary>
    private static bool IsVowelPhoneme(string s)
    {
        return s == "ah" || s == "ee" || s == "eh" || s == "oh" || s == "oo";
    }

    /// <summary>
    /// Maps single vowel characters to phoneme names.
    /// </summary>
    private static string MapToVowelPhoneme(string chars)
    {
        return chars switch
        {
            "a" => "ah",
            "e" => "eh",
            "i" => "ee",
            "o" => "oh",
            "u" => "oo",
            _ => chars // Treat as phoneme name directly
        };
    }
}
