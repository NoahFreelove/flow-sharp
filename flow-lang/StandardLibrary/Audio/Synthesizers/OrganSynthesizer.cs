using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Hammond-style organ synthesizer using additive synthesis.
/// Combines sine waves at harmonic ratios corresponding to traditional
/// drawbar registrations (16', 8', 5-1/3', 4', 2-2/3', 2') for
/// a classic tonewheel organ sound.
/// </summary>
public class OrganSynthesizer : INoteSynthesizer
{
    // Drawbar harmonic ratios and their relative amplitudes
    private static readonly (double ratio, double amplitude)[] Drawbars =
    {
        (1.0, 1.0),   // 16'  - fundamental
        (2.0, 0.8),   // 8'   - octave
        (3.0, 0.6),   // 5-1/3' - twelfth
        (4.0, 0.5),   // 4'   - two octaves
        (6.0, 0.3),   // 2-2/3' - octave + fifth
        (8.0, 0.2),   // 2'   - three octaves
    };

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
        double baseAmp = 0.08 * note.Velocity;
        double nyquist = sampleRate / 2.0;

        // Additive synthesis: layer sine waves at each drawbar harmonic
        foreach (var (ratio, amp) in Drawbars)
        {
            double partialFreq = frequency * ratio;
            if (partialFreq >= nyquist)
                continue;

            SynthUtils.GenerateSine(samples, partialFreq, baseAmp * amp, sampleRate);
        }

        // Near-instant attack, full sustain, minimal release (organ key click character)
        float[] envelope = SynthUtils.GenerateADSR(
            attack: 0.005, decay: 0.01, sustain: 1.0, release: 0.01,
            frames: numSamples, sampleRate: sampleRate);
        SynthUtils.ApplyEnvelope(samples, envelope);

        return SynthUtils.ToMonoBuffer(samples, sampleRate);
    }
}
