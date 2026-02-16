using System;
using System.Collections.Generic;
using FlowLang.Audio;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio
{
    public static class BarRenderer
    {
        /// <summary>
        /// Renders a musical bar to a collection of positioned voices.
        /// Each note becomes a Voice positioned on the timeline.
        /// </summary>
        public static List<Voice> RenderBarToVoices(
            BarData bar,
            string synthType,
            int sampleRate,
            double bpm)
        {
            if (bar.Mode != BarMode.Musical)
            {
                throw new InvalidOperationException("Can only render musical mode bars. Use bar creation functions to create musical bars.");
            }

            if (bar.TimeSignature == null)
            {
                throw new InvalidOperationException("Bar must have a time signature to render.");
            }

            // Create synthesizer
            INoteSynthesizer synthesizer = SynthesizerFactory.Create(synthType);

            // Convert bar to timeline
            var timeline = bar.ToTimeline();
            var voices = new List<Voice>();

            // Render each note
            foreach (var (note, offsetBeats) in timeline)
            {
                if (note.IsRest)
                    continue; // Skip rests - they create gaps in the timeline

                // Calculate duration in beats
                double durationBeats = note.GetBeats(bar.TimeSignature.Denominator);

                // Apply articulation to duration
                switch (note.Articulation)
                {
                    case Articulation.Staccato:
                        durationBeats *= 0.5;
                        break;
                    case Articulation.Marcato:
                        durationBeats *= 0.8;
                        break;
                    // Normal, Tenuto, Accent, Sforzando don't shorten duration
                }

                // For tied notes, extend render duration so the audio tail overlaps the next note.
                // This creates a legato transition since voices mix additively on the timeline.
                if (note.IsTied)
                {
                    double overlapSeconds = 0.1; // 100ms overlap for smooth crossfade
                    double overlapBeats = (overlapSeconds / 60.0) * bpm;
                    durationBeats += overlapBeats;
                }

                // Render note to audio buffer
                AudioBuffer buffer = synthesizer.RenderNote(note, sampleRate, durationBeats, bpm);

                // Create voice at the appropriate position
                Voice voice = new Voice(buffer, offsetBeats);
                voices.Add(voice);
            }

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
            var voices = RenderBarToVoices(bar, synthType, sampleRate, bpm);

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
            // Convert seconds to beats: beats = (seconds / 60) * bpm
            double beatOffset = (timeSeconds / 60.0) * bpm;
            return RenderBarAtBeat(bar, beatOffset, synthType, sampleRate, bpm);
        }

        /// <summary>
        /// Timeline-aware version of RenderBarToVoices. Populates the timeline map
        /// with entries for each rendered note (at beat offset 0).
        /// </summary>
        public static List<Voice> RenderBarToVoices(
            BarData bar,
            string synthType,
            int sampleRate,
            double bpm,
            TimelineMap timelineMap,
            string scopeName = "top-level")
        {
            return RenderBarAtBeat(bar, 0, synthType, sampleRate, bpm, timelineMap, scopeName);
        }

        /// <summary>
        /// Timeline-aware version of RenderBarAtBeat.
        /// </summary>
        public static List<Voice> RenderBarAtBeat(
            BarData bar,
            double beatOffset,
            string synthType,
            int sampleRate,
            double bpm,
            TimelineMap timelineMap,
            string scopeName = "top-level")
        {
            // Render voices (timeline entries are recorded with barOffsetBeats = beatOffset)
            var voices = RenderBarToVoices(bar, synthType, sampleRate, bpm);

            // Record timeline entries
            if (timelineMap != null && bar.TimeSignature != null)
            {
                double secondsPerBeat = 60.0 / bpm;
                var timeline = bar.ToTimeline();

                foreach (var (note, offsetBeats) in timeline)
                {
                    if (note.IsRest || note.SourceLocation == null)
                        continue;

                    double durationBeats = note.GetBeats(bar.TimeSignature.Denominator);
                    double noteStartSeconds = (beatOffset + offsetBeats) * secondsPerBeat;
                    double noteEndSeconds = noteStartSeconds + (durationBeats * secondsPerBeat);

                    timelineMap.Add(new TimelineEntry(
                        noteStartSeconds,
                        noteEndSeconds,
                        note.SourceLocation,
                        note.SourceLength > 0 ? note.SourceLength : note.ToString().Length,
                        scopeName));
                }
            }

            // Apply beat offset
            foreach (var voice in voices)
            {
                voice.OffsetBeats += beatOffset;
            }

            return voices;
        }
    }
}
