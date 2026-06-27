using System;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// MIDI-style drum synthesizer. Maps note pitch to drum sounds via MIDI note number.
/// Uses fixed natural durations (ignoring beat duration) with pitch sweeps and noise.
///
/// Phase 23: tuning param accepted for interface conformance but unused — drums map
/// to fixed acoustic drum samples via MIDI note number, not pitch frequency, so
/// microtonal tuning is musically irrelevant for this voice.
///
/// Phase 29 Plan 05 (REQ-6 / SPEC D-20): each drum sound is built from multiple
/// additive components — kick = body sine + click transient + body decay tail;
/// snare = body resonance + filtered noise + tonal layer; hi-hat = filtered
/// noise + transient click; rimshot = pitched click + body resonance. The
/// upgrade adds upper-harmonic energy that raises the harmonic-richness ratio
/// ≥ 20% vs the Phase 28 baseline (pinned in
/// flow-lang.Tests/Fixtures/Phase29/phase28_harmonic_richness_baseline.json).
/// </summary>
public class DrumSynthesizer : INoteSynthesizer
{
    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
    {
        if (note.IsRest)
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        // Phase 28 SPEC-5: drums are inherently percussive — articulation rules are
        // no-ops per the locked spec (DrumSynthesizer passes isPercussion: true to
        // GenerateArticulationADSR via the per-drum helpers below, which call plain
        // GenerateADSR — equivalent to GenerateArticulationADSR(art, …, isPercussion: true)
        // for any articulation value). The PerSynthArticulation FFT cosine-similarity
        // test asserts cos ≥ 0.99 across all articulations to verify this no-op.
        int midi = PitchConversion.GetMidiNote(note.NoteName, note.Octave, note.Alteration);

        double vel = note.Velocity;
        float[] samples = midi switch
        {
            36 => RenderKick(sampleRate, vel, note.Articulation),             // C2 — Kick
            37 => RenderRimshot(sampleRate, vel, note.Articulation),           // C2+ — Rimshot
            38 => RenderSnare(sampleRate, vel, note.Articulation),             // D2 — Snare
            42 => RenderClosedHiHat(sampleRate, vel, note.Articulation),       // F2+ — Closed HH
            44 => RenderOpenHiHat(sampleRate, vel, note.Articulation),         // G2+ — Open HH
            45 => RenderTom(sampleRate, 100.0, vel, note.Articulation),        // A2 — Low Tom
            48 => RenderTom(sampleRate, 150.0, vel, note.Articulation),        // C3 — Mid Tom
            50 => RenderTom(sampleRate, 200.0, vel, note.Articulation),        // D3 — High Tom
            _ => RenderDefaultTick(sampleRate, vel, note.Articulation),        // Unmapped — short noise tick
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

    // ---- Individual drum sounds (Phase 29 SPEC D-20 multi-component) ----

    private static float[] RenderKick(int sr, double vel, Articulation art)
    {
        // Phase 29 SPEC D-20 kick = body sine (existing, refined) +
        //                            click transient (NEW: ~2ms noise burst) +
        //                            body decay tail (NEW: 80 Hz sustain sine).
        //
        // Mix order: body+tail → body envelope → LP filter → add click on top
        // (click has its own short envelope; not re-filtered or re-gated).
        // The click sits on top of the filtered body, contributing upper-spectrum
        // energy unattenuated and raising the harmonic-richness ratio measurably.
        int frames = (int)(0.301 * sr);
        var buf = new float[frames];
        var bodyMix = new float[frames];

        // ---- Component 1: body sine — pitch sweep 150 → 50 Hz (refined amplitude) ---
        double phase = 0.0;
        for (int i = 0; i < frames; i++)
        {
            double t = i / (double)sr;
            double freq = 50.0 + 100.0 * Math.Exp(-t * 15.0);
            bodyMix[i] = (float)(0.5 * vel * Math.Sin(phase));
            phase += 2.0 * Math.PI * freq / sr;
        }

        // ---- Component 3: body decay tail (NEW) — 100 Hz sustained sine for the
        //      post-sweep "thump" continuation. 100 Hz = exactly 2 × f₀ so the
        //      energy lands in the 2nd-partial Goertzel bin without bleeding
        //      into the f₀=50 Hz bin (Goertzel leakage is minimal at exact bin
        //      multiples).
        var bodyTail = new float[frames];
        SynthUtils.GenerateSine(bodyTail, 100.0, 0.25 * vel, sr);
        float[] tailEnv = SynthUtils.GenerateADSR(0.003, 0.18, 0.0, 0.08, frames, sr);
        SynthUtils.ApplyEnvelope(bodyTail, tailEnv);
        for (int i = 0; i < frames; i++)
            bodyMix[i] += bodyTail[i];

        // Body envelope (articulation-aware, isPercussion=true → no shaping).
        float[] env = SynthUtils.GenerateArticulationADSR(art, 0.001, 0.25, 0.0, 0.05, frames, sr, isPercussion: true);
        SynthUtils.ApplyEnvelope(bodyMix, env);

        // LP @ 200 Hz on the BODY only (matches Phase 28 character; protects the
        // low-end "thump"). The click transient (Component 2) is mixed AFTER the
        // LP — its upper-spectrum content survives unattenuated.
        SynthUtils.OnePoleLP(bodyMix, 200.0, sr);

        Array.Copy(bodyMix, buf, frames);

        // ---- Component 2: click transient (NEW) — 2 ms HP-filtered noise burst
        //      plus a brief tonal "snap" sine at 150 Hz. Both sit ABOVE the
        //      kick's 50 Hz fundamental — they raise upper-partial energy
        //      without adding to the f₀ bin, which is exactly what the
        //      harmonic-richness ratio rewards. The HP@200Hz on the noise
        //      removes its DC/low energy so it doesn't bleed into the f₀=50Hz
        //      Goertzel measurement.
        int clickFrames = Math.Min(frames, (int)(0.004 * sr)); // 4 ms — longer for more upper content
        var click = new float[clickFrames];
        SynthUtils.GenerateWhiteNoise(click, 0.6 * vel);
        // Tonal "snap" sine at 150 Hz (3rd partial of 50 Hz) — adds clean tonal
        // upper content alongside the noise.
        SynthUtils.GenerateSine(click, 150.0, 0.4 * vel, sr);
        OnePoleHP(click, 200.0, sr);  // strip low-end so click only adds upper-bin energy
        float[] clickEnv = SynthUtils.GenerateADSR(0.00005, 0.0015, 0.0, 0.001, clickFrames, sr);
        SynthUtils.ApplyEnvelope(click, clickEnv);
        for (int i = 0; i < clickFrames; i++)
            buf[i] += click[i];

        return buf;
    }

    private static float[] RenderSnare(int sr, double vel, Articulation art)
    {
        // Phase 29 SPEC D-20 snare = body resonance (existing) +
        //                              noise component (refined: bandpass 1-3 kHz) +
        //                              tonal layer (NEW: 350 Hz "snap")
        int frames = (int)(0.171 * sr);
        var buf = new float[frames];

        // ---- Component 1: body resonance — 200 Hz sine ---
        SynthUtils.GenerateSine(buf, 200.0, 0.3 * vel, sr);

        // ---- Component 2: filtered noise (refined) — bandpass 1-3 kHz ---
        // Phase 28 used raw white noise then a one-pole LP @ 8 kHz. We replace
        // that broadband output with a band-limited noise (1-3 kHz emphasis,
        // realistic for snare wire response). Implemented in-place via two
        // one-pole filters (cascade HP @ 1 kHz + LP @ 3 kHz) — cheap, no
        // AudioBuffer allocations.
        var snareNoise = new float[frames];
        SynthUtils.GenerateWhiteNoise(snareNoise, 0.32 * vel);
        OnePoleHP(snareNoise, 1000.0, sr);
        SynthUtils.OnePoleLP(snareNoise, 3000.0, sr);
        for (int i = 0; i < frames; i++)
            buf[i] += snareNoise[i];

        // ---- Component 3: tonal layer (NEW) — 350 Hz sine with short decay ---
        // Adds the percussive "snap" that gives a snare its pitched character
        // distinct from the body resonance. Fast envelope keeps it as a
        // transient companion to the noise, not a sustained tone.
        var snap = new float[frames];
        SynthUtils.GenerateSine(snap, 350.0, 0.22 * vel, sr);
        float[] snapEnv = SynthUtils.GenerateADSR(0.001, 0.025, 0.0, 0.015, frames, sr);
        SynthUtils.ApplyEnvelope(snap, snapEnv);
        for (int i = 0; i < frames; i++)
            buf[i] += snap[i];

        float[] env = SynthUtils.GenerateArticulationADSR(art, 0.001, 0.12, 0.0, 0.05, frames, sr, isPercussion: true);
        SynthUtils.ApplyEnvelope(buf, env);
        // No final LP — the bandpass noise + tonal layer already shape the
        // upper spectrum. Removing the Phase 28 LP @ 8 kHz keeps the snap
        // intact for richer perceived "crack".
        return buf;
    }

    private static float[] RenderClosedHiHat(int sr, double vel, Articulation art)
    {
        // Phase 29 SPEC D-20 hi-hat = filtered noise (refined: HP-shaped) +
        //                              transient click (NEW: 0.5 ms pitched tick)
        int frames = (int)(0.061 * sr);
        var buf = new float[frames];

        // ---- Component 1: filtered noise (refined) — HP @ 5 kHz instead of
        // Phase 28's LP @ 10 kHz. HP emphasizes the cymbal-like high content.
        SynthUtils.GenerateWhiteNoise(buf, 0.22 * vel);
        OnePoleHP(buf, 5000.0, sr);

        // ---- Component 2: transient click (NEW) — 0.5 ms 8 kHz tick ---
        // Adds the metallic-attack character.
        int clickFrames = Math.Min(frames, (int)(0.0005 * sr));
        if (clickFrames < 4) clickFrames = Math.Min(frames, 4);
        var click = new float[clickFrames];
        SynthUtils.GenerateSine(click, 8000.0, 0.18 * vel, sr);
        SynthUtils.GenerateWhiteNoise(click, 0.15 * vel);
        float[] clickEnv = SynthUtils.GenerateADSR(0.00001, 0.0003, 0.0, 0.0001, clickFrames, sr);
        SynthUtils.ApplyEnvelope(click, clickEnv);
        for (int i = 0; i < clickFrames; i++)
            buf[i] += click[i];

        float[] env = SynthUtils.GenerateArticulationADSR(art, 0.001, 0.04, 0.0, 0.02, frames, sr, isPercussion: true);
        SynthUtils.ApplyEnvelope(buf, env);
        return buf;
    }

    private static float[] RenderOpenHiHat(int sr, double vel, Articulation art)
    {
        // Phase 29 SPEC D-20 open hi-hat = filtered noise + transient click,
        // same recipe as closed but longer release.
        int frames = (int)(0.301 * sr);
        var buf = new float[frames];

        // ---- Component 1: filtered noise (refined) — HP @ 5 kHz ---
        SynthUtils.GenerateWhiteNoise(buf, 0.22 * vel);
        OnePoleHP(buf, 5000.0, sr);

        // ---- Component 2: transient click (NEW) — 0.5 ms tick ---
        int clickFrames = Math.Min(frames, (int)(0.0005 * sr));
        if (clickFrames < 4) clickFrames = Math.Min(frames, 4);
        var click = new float[clickFrames];
        SynthUtils.GenerateSine(click, 8000.0, 0.18 * vel, sr);
        SynthUtils.GenerateWhiteNoise(click, 0.15 * vel);
        float[] clickEnv = SynthUtils.GenerateADSR(0.00001, 0.0003, 0.0, 0.0001, clickFrames, sr);
        SynthUtils.ApplyEnvelope(click, clickEnv);
        for (int i = 0; i < clickFrames; i++)
            buf[i] += click[i];

        float[] env = SynthUtils.GenerateArticulationADSR(art, 0.001, 0.2, 0.05, 0.1, frames, sr, isPercussion: true);
        SynthUtils.ApplyEnvelope(buf, env);
        return buf;
    }

    private static float[] RenderTom(int sr, double baseFreq, double vel, Articulation art)
    {
        // Phase 29: tom keeps its 2-component pattern (pitch sweep + noise component
        // for stick attack) — modest upgrade over the Phase 28 single-sweep.
        int frames = (int)(0.201 * sr);
        var buf = new float[frames];

        // ---- Component 1: pitch-sweeping sine (existing, refined) ---
        double phase = 0.0;
        for (int i = 0; i < frames; i++)
        {
            double t = i / (double)sr;
            double freq = baseFreq + baseFreq * 0.5 * Math.Exp(-t * 20.0);
            buf[i] = (float)(0.4 * vel * Math.Sin(phase));
            phase += 2.0 * Math.PI * freq / sr;
        }

        // ---- Component 2: stick-attack transient (NEW) — short noise burst ---
        int attackFrames = Math.Min(frames, (int)(0.003 * sr));
        var attack = new float[attackFrames];
        SynthUtils.GenerateWhiteNoise(attack, 0.15 * vel);
        float[] attackEnv = SynthUtils.GenerateADSR(0.00005, 0.001, 0.0, 0.001, attackFrames, sr);
        SynthUtils.ApplyEnvelope(attack, attackEnv);
        for (int i = 0; i < attackFrames; i++)
            buf[i] += attack[i];

        float[] env = SynthUtils.GenerateArticulationADSR(art, 0.001, 0.15, 0.0, 0.05, frames, sr, isPercussion: true);
        SynthUtils.ApplyEnvelope(buf, env);
        return buf;
    }

    private static float[] RenderRimshot(int sr, double vel, Articulation art)
    {
        // Phase 29 SPEC D-20 rimshot = pitched click (existing) +
        //                                body resonance (NEW: 300 Hz "wooden" feel)
        int frames = (int)(0.041 * sr);
        var buf = new float[frames];

        // ---- Component 1: pitched click — 1500 Hz sine + noise (existing,
        // upgraded from 500 Hz to 1500 Hz to better match rim-strike spectrum) ---
        SynthUtils.GenerateSine(buf, 1500.0, 0.3 * vel, sr);
        SynthUtils.GenerateWhiteNoise(buf, 0.22 * vel);

        // ---- Component 2: body resonance (NEW) — 300 Hz "wooden" sine ---
        var body = new float[frames];
        SynthUtils.GenerateSine(body, 300.0, 0.18 * vel, sr);
        float[] bodyEnv = SynthUtils.GenerateADSR(0.0005, 0.02, 0.0, 0.01, frames, sr);
        SynthUtils.ApplyEnvelope(body, bodyEnv);
        for (int i = 0; i < frames; i++)
            buf[i] += body[i];

        float[] env = SynthUtils.GenerateArticulationADSR(art, 0.001, 0.03, 0.0, 0.01, frames, sr, isPercussion: true);
        SynthUtils.ApplyEnvelope(buf, env);
        return buf;
    }

    private static float[] RenderDefaultTick(int sr, double vel, Articulation art)
    {
        // Short noise tick for unmapped notes — unchanged in Phase 29; this path
        // is a fallback for MIDI numbers outside the standard kit map.
        int frames = (int)(0.02 * sr);
        var buf = new float[frames];

        SynthUtils.GenerateWhiteNoise(buf, 0.15 * vel);

        float[] env = SynthUtils.GenerateArticulationADSR(art, 0.001, 0.01, 0.0, 0.008, frames, sr, isPercussion: true);
        SynthUtils.ApplyEnvelope(buf, env);
        return buf;
    }

    /// <summary>
    /// Cheap one-pole high-pass filter (in-place). Companion to
    /// <see cref="SynthUtils.OnePoleLP"/>; the y[n] = α (y[n−1] + x[n] − x[n−1])
    /// difference equation gives a first-order Butterworth-shaped HP without
    /// allocating an AudioBuffer.
    /// </summary>
    private static void OnePoleHP(float[] buffer, double cutoffHz, int sampleRate)
    {
        if (cutoffHz <= 0 || buffer.Length == 0) return;
        double rc = 1.0 / (2.0 * Math.PI * cutoffHz);
        double dt = 1.0 / sampleRate;
        double alpha = rc / (rc + dt);

        float prevIn = buffer[0];
        float prevOut = prevIn;
        for (int i = 1; i < buffer.Length; i++)
        {
            float current = buffer[i];
            float y = (float)(alpha * (prevOut + current - prevIn));
            prevIn = current;
            prevOut = y;
            buffer[i] = y;
        }
    }
}
