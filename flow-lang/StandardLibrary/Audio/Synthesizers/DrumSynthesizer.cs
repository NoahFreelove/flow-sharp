using System;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// MIDI-style drum synthesizer. Maps note pitch to drum sounds via MIDI note number.
/// Uses fixed natural durations (ignoring beat duration) with pitch sweeps and noise.
/// </summary>
public class DrumSynthesizer : INoteSynthesizer
{
    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm)
    {
        if (note.IsRest)
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        int midi = PitchConversion.GetMidiNote(note.NoteName, note.Octave, note.Alteration);

        double vel = note.Velocity;
        float[] samples = midi switch
        {
            36 => RenderKick(sampleRate, vel),             // C2 — Kick
            37 => RenderRimshot(sampleRate, vel),           // C2+ — Rimshot
            38 => RenderSnare(sampleRate, vel),             // D2 — Snare
            42 => RenderClosedHiHat(sampleRate, vel),       // F2+ — Closed HH
            44 => RenderOpenHiHat(sampleRate, vel),         // G2+ — Open HH
            45 => RenderTom(sampleRate, 100.0, vel),        // A2 — Low Tom
            48 => RenderTom(sampleRate, 150.0, vel),        // C3 — Mid Tom
            50 => RenderTom(sampleRate, 200.0, vel),        // D3 — High Tom
            _ => RenderDefaultTick(sampleRate, vel),        // Unmapped — short noise tick
        };

        // Pad or trim to match the expected beat duration so the voice
        // occupies the correct time slot in the mix.
        double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
        int targetSamples = (int)(durationSeconds * sampleRate);
        if (targetSamples <= 0)
            return new AudioBuffer(0, 1, sampleRate);

        var output = new float[targetSamples];
        int copyLen = Math.Min(samples.Length, targetSamples);
        Array.Copy(samples, output, copyLen);

        return SynthUtils.ToMonoBuffer(output, sampleRate);
    }

    // ---- Individual drum sounds ----

    private static float[] RenderKick(int sr, double vel)
    {
        // Sine with pitch sweep 150->50 Hz, ADSR 1ms/250ms/0/50ms, LP 200Hz
        int frames = (int)(0.301 * sr); // attack+decay+release
        var buf = new float[frames];

        // Pitch sweep: exponential decay from 150 to 50 Hz
        double phase = 0.0;
        for (int i = 0; i < frames; i++)
        {
            double t = i / (double)sr;
            double freq = 50.0 + 100.0 * Math.Exp(-t * 15.0);
            buf[i] = (float)(0.5 * vel * Math.Sin(phase));
            phase += 2.0 * Math.PI * freq / sr;
        }

        float[] env = SynthUtils.GenerateADSR(0.001, 0.25, 0.0, 0.05, frames, sr);
        SynthUtils.ApplyEnvelope(buf, env);
        SynthUtils.OnePoleLP(buf, 200.0, sr);
        return buf;
    }

    private static float[] RenderSnare(int sr, double vel)
    {
        // Sine 200Hz + white noise, ADSR 1ms/120ms/0/50ms, LP 8kHz
        int frames = (int)(0.171 * sr);
        var buf = new float[frames];

        SynthUtils.GenerateSine(buf, 200.0, 0.3 * vel, sr);
        SynthUtils.GenerateWhiteNoise(buf, 0.25 * vel);

        float[] env = SynthUtils.GenerateADSR(0.001, 0.12, 0.0, 0.05, frames, sr);
        SynthUtils.ApplyEnvelope(buf, env);
        SynthUtils.OnePoleLP(buf, 8000.0, sr);
        return buf;
    }

    private static float[] RenderClosedHiHat(int sr, double vel)
    {
        // White noise, ADSR 1ms/40ms/0/20ms, LP 10kHz
        int frames = (int)(0.061 * sr);
        var buf = new float[frames];

        SynthUtils.GenerateWhiteNoise(buf, 0.2 * vel);

        float[] env = SynthUtils.GenerateADSR(0.001, 0.04, 0.0, 0.02, frames, sr);
        SynthUtils.ApplyEnvelope(buf, env);
        SynthUtils.OnePoleLP(buf, 10000.0, sr);
        return buf;
    }

    private static float[] RenderOpenHiHat(int sr, double vel)
    {
        // White noise, ADSR 1ms/200ms/0.05/100ms, LP 10kHz
        int frames = (int)(0.301 * sr);
        var buf = new float[frames];

        SynthUtils.GenerateWhiteNoise(buf, 0.2 * vel);

        float[] env = SynthUtils.GenerateADSR(0.001, 0.2, 0.05, 0.1, frames, sr);
        SynthUtils.ApplyEnvelope(buf, env);
        SynthUtils.OnePoleLP(buf, 10000.0, sr);
        return buf;
    }

    private static float[] RenderTom(int sr, double baseFreq, double vel)
    {
        // Sine with slight pitch sweep from baseFreq*1.5 -> baseFreq, ADSR 1ms/150ms/0/50ms
        int frames = (int)(0.201 * sr);
        var buf = new float[frames];

        double phase = 0.0;
        for (int i = 0; i < frames; i++)
        {
            double t = i / (double)sr;
            double freq = baseFreq + baseFreq * 0.5 * Math.Exp(-t * 20.0);
            buf[i] = (float)(0.4 * vel * Math.Sin(phase));
            phase += 2.0 * Math.PI * freq / sr;
        }

        float[] env = SynthUtils.GenerateADSR(0.001, 0.15, 0.0, 0.05, frames, sr);
        SynthUtils.ApplyEnvelope(buf, env);
        return buf;
    }

    private static float[] RenderRimshot(int sr, double vel)
    {
        // Noise + sine 500Hz, ADSR 1ms/30ms/0/10ms
        int frames = (int)(0.041 * sr);
        var buf = new float[frames];

        SynthUtils.GenerateSine(buf, 500.0, 0.3 * vel, sr);
        SynthUtils.GenerateWhiteNoise(buf, 0.2 * vel);

        float[] env = SynthUtils.GenerateADSR(0.001, 0.03, 0.0, 0.01, frames, sr);
        SynthUtils.ApplyEnvelope(buf, env);
        return buf;
    }

    private static float[] RenderDefaultTick(int sr, double vel)
    {
        // Short noise tick for unmapped notes
        int frames = (int)(0.02 * sr);
        var buf = new float[frames];

        SynthUtils.GenerateWhiteNoise(buf, 0.15 * vel);

        float[] env = SynthUtils.GenerateADSR(0.001, 0.01, 0.0, 0.008, frames, sr);
        SynthUtils.ApplyEnvelope(buf, env);
        return buf;
    }
}
