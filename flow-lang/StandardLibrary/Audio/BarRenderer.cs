using System;
using System.Collections.Generic;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio;

public static class BarRenderer
{
    /// <summary>
    /// Renders a musical bar to a collection of positioned voices.
    /// Each note becomes a Voice positioned on the timeline.
    /// Phase 23: existing call sites pass <see cref="RenderTuning.Default"/> so the
    /// byte-identical 12-TET path is taken via Pitfall 6 short-circuit. Task 3 wires
    /// the real per-section RenderTuning resolution at the SongRenderer entry.
    /// </summary>
    public static List<Voice> RenderBarToVoices(
        BarData bar,
        string synthType,
        int sampleRate,
        double bpm)
    {
        return RenderBarToVoices(bar, SynthesizerFactory.Create(synthType), sampleRate, bpm, RenderTuning.Default);
    }

    public static List<Voice> RenderBarToVoices(
        BarData bar,
        INoteSynthesizer synthesizer,
        int sampleRate,
        double bpm)
    {
        return RenderBarToVoices(bar, synthesizer, sampleRate, bpm, RenderTuning.Default);
    }

    public static List<Voice> RenderBarToVoices(
        BarData bar,
        INoteSynthesizer synthesizer,
        int sampleRate,
        double bpm,
        RenderTuning tuning,
        bool sustainPedalActive = false)
    {
        if (bar.Mode != BarMode.Musical)
        {
            throw new InvalidOperationException("Can only render musical mode bars. Use bar creation functions to create musical bars.");
        }

        if (bar.TimeSignature == null)
        {
            throw new InvalidOperationException("Bar must have a time signature to render.");
        }

        // Phase 28 (SPEC-1): voice-block rendering. When the bar has parallel
        // voices (compiled from `| {voice ...} {voice ...} |`), recursively
        // render each child bar starting at offset 0 (all voices share the
        // parent bar's onset) and concatenate the resulting voices. The
        // SongRenderer's mix-to-stereo path then sums them additively → true
        // polyphony for held + running patterns. The parent bar's own
        // MusicalNotes list is ignored when ParallelVoices is non-null
        // (compiler emits a single whole-bar rest as a placeholder so the
        // bar still spans the full duration for cursor-advance bookkeeping).
        if (bar.ParallelVoices != null && bar.ParallelVoices.Count > 0)
        {
            var combined = new List<Voice>();
            foreach (var voiceBar in bar.ParallelVoices)
            {
                // Each voice block is its own BarData with its own MusicalNotes
                // and shares the parent bar's TimeSignature. Render at offset 0
                // (caller provides the bar-level offset via the wrapping
                // RenderBarAtBeat overload).
                if (voiceBar.TimeSignature == null)
                    voiceBar.TimeSignature = bar.TimeSignature;
                var subVoices = RenderBarToVoices(voiceBar, synthesizer, sampleRate, bpm, tuning, sustainPedalActive);
                combined.AddRange(subVoices);
            }
            return combined;
        }

        // Convert bar to timeline
        var timeline = bar.ToTimeline();
        var voices = new List<Voice>();

        // Render each note (indexed loop so tied notes can look ahead for rests)
        for (int idx = 0; idx < timeline.Count; idx++)
        {
            var (note, offsetBeats) = timeline[idx];
            if (note.IsRest)
                continue; // Skip rests - they create gaps in the timeline

            // Calculate duration in beats
            double durationBeats = note.GetBeats(bar.TimeSignature.Denominator);

            // Phase 28 locked articulation duration multipliers (SPEC-4):
            //   Staccato 25%, Marcato 25% (Staccato-shortened), Legato 110%,
            //   Tenuto 100%, Accent 100%, Sforzando 100%.
            // Per-instrument envelope shaping (sustain, release, spike) lands at
            // the synthesizer in Plan 28-03. Both Legato sources compose: a note
            // with Articulation.Legato AND DurationOverlap=0.5 ends up rendered
            // at 1.0 × 1.10 × 1.5 = 1.65 of authored duration (DurationOverlap
            // multiplier is applied below).
            switch (note.Articulation)
            {
                case Articulation.Staccato:
                    durationBeats *= 0.25;
                    break;
                case Articulation.Marcato:
                    durationBeats *= 0.25;
                    break;
                case Articulation.Legato:
                    durationBeats *= 1.10;
                    break;
                // Tenuto, Accent, Sforzando, Normal — duration unchanged
            }

            // For tied notes, extend render duration to sustain through subsequent
            // rest elements in the same voice/bar. This matches the standard musical
            // interpretation of a tie: the note rings through the following silence.
            // Crossfade tail is only added when sustain pedal is OFF; otherwise the
            // sustain extension carries the smoothing and a 100ms crossfade would
            // add an audible bleed into the next bar's attack (perceived as a grace
            // note 100ms after the bar boundary).
            if (note.IsTied)
            {
                double tiedExtension = 0;
                for (int j = idx + 1; j < timeline.Count; j++)
                {
                    var (next, _) = timeline[j];
                    if (next.IsRest)
                        tiedExtension += next.GetBeats(bar.TimeSignature.Denominator);
                    else
                        break;
                }
                durationBeats += tiedExtension;

                if (!sustainPedalActive)
                {
                    double overlapSeconds = 0.1;
                    double overlapBeats = (overlapSeconds / 60.0) * bpm;
                    durationBeats += overlapBeats;
                }
            }

            // DX-14 legato: extend rendered duration by overlap factor BEFORE rendering audio buffer.
            // Per CONTEXT D-01: durationOverlap=0.5 -> durationBeats x 1.5.
            // Per CONTEXT D-02 + Pitfall 3: bar.ToTimeline() already produced offsetBeats; we ONLY
            // change how long this note's audio buffer plays. Onset is NOT moved here. Polyphonic
            // mix in SongRenderer sums overlapping voices automatically.
            if (note.DurationOverlap > 0.0)
            {
                durationBeats *= (1.0 + note.DurationOverlap);
            }

            // Sustain pedal — extend every note's rendered buffer by the section's
            // pedal-tail. Notes ring through subsequent attacks, mimicking piano
            // pedal behavior. Onset is unchanged (additive mix handles overlap),
            // so positions remain correct.
            if (sustainPedalActive)
            {
                double sustainTailBeats = (FlowLang.Runtime.MusicalContext.SustainTailSeconds * bpm) / 60.0;
                durationBeats += sustainTailBeats;
            }

            // Render note to audio buffer.
            // Phase 23 Pattern A: tuning threaded from SongRenderer per-section
            // resolution. RenderTuning.Default short-circuits to byte-identical
            // 12-TET via PitchConversion.NoteToFrequency Pitfall 6 mitigation.
            AudioBuffer buffer = synthesizer.RenderNote(note, sampleRate, durationBeats, bpm, tuning);

            // Create voice at the appropriate position
            Voice voice = new Voice(buffer, offsetBeats);
            voices.Add(voice);
        }

        return voices;
    }

    // NOTE: foreach → for(idx) conversion above is intentional. The other
    // RenderBarToVoices overloads delegate here, so the tie-sustain logic
    // is centralized.

    /// <summary>
    /// Overload that applies pan value from musical context to all rendered voices.
    /// </summary>
    public static List<Voice> RenderBarToVoices(
        BarData bar,
        string synthType,
        int sampleRate,
        double bpm,
        double pan)
    {
        var voices = RenderBarToVoices(bar, synthType, sampleRate, bpm);
        foreach (var voice in voices)
            voice.Pan = pan;
        return voices;
    }

    /// <summary>
    /// Renders multiple bars sequentially to a collection of voices.
    /// Each bar is positioned after the previous one.
    /// </summary>
    public static List<Voice> RenderBarsToVoices(
        List<BarData> bars,
        string synthType,
        int sampleRate,
        double bpm)
    {
        var allVoices = new List<Voice>();
        double currentOffset = 0;

        foreach (var bar in bars)
        {
            if (bar.TimeSignature == null)
            {
                throw new InvalidOperationException("All bars must have time signatures to render.");
            }

            // Render this bar
            var barVoices = RenderBarToVoices(bar, synthType, sampleRate, bpm);

            // Offset all voices by the current position
            foreach (var voice in barVoices)
            {
                voice.OffsetBeats += currentOffset;
                allVoices.Add(voice);
            }

            // Move to next bar position
            currentOffset += bar.IsPickup ? bar.GetActualBeats() : bar.TimeSignature.Numerator;
        }

        return allVoices;
    }

    /// <summary>
    /// Renders a bar and positions all voices at a specific beat offset.
    /// Allows manual control over bar positioning on the timeline.
    /// </summary>
    public static List<Voice> RenderBarAtBeat(
        BarData bar,
        double beatOffset,
        string synthType,
        int sampleRate,
        double bpm)
    {
        return RenderBarAtBeat(bar, beatOffset, SynthesizerFactory.Create(synthType), sampleRate, bpm);
    }

    public static List<Voice> RenderBarAtBeat(
        BarData bar,
        double beatOffset,
        INoteSynthesizer synthesizer,
        int sampleRate,
        double bpm)
    {
        return RenderBarAtBeat(bar, beatOffset, synthesizer, sampleRate, bpm, RenderTuning.Default);
    }

    public static List<Voice> RenderBarAtBeat(
        BarData bar,
        double beatOffset,
        INoteSynthesizer synthesizer,
        int sampleRate,
        double bpm,
        RenderTuning tuning,
        bool sustainPedalActive = false)
    {
        var voices = RenderBarToVoices(bar, synthesizer, sampleRate, bpm, tuning, sustainPedalActive);

        // Add beat offset to all voices
        foreach (var voice in voices)
        {
            voice.OffsetBeats += beatOffset;
        }

        return voices;
    }

    /// <summary>
    /// Renders a bar and positions all voices at a specific time offset (in seconds).
    /// Converts the time offset to beats based on the BPM.
    /// </summary>
    public static List<Voice> RenderBarAtTime(
        BarData bar,
        double timeSeconds,
        string synthType,
        int sampleRate,
        double bpm)
    {
        return RenderBarAtTime(bar, timeSeconds, SynthesizerFactory.Create(synthType), sampleRate, bpm);
    }

    public static List<Voice> RenderBarAtTime(
        BarData bar,
        double timeSeconds,
        INoteSynthesizer synthesizer,
        int sampleRate,
        double bpm)
    {
        // Convert seconds to beats: beats = (seconds / 60) * bpm
        double beatOffset = (timeSeconds / 60.0) * bpm;
        return RenderBarAtBeat(bar, beatOffset, synthesizer, sampleRate, bpm);
    }
}
