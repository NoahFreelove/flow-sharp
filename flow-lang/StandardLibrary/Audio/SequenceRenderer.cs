using FlowLang.Audio;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio
{
    public static class SequenceRenderer
    {
        /// <summary>
        /// Creates a new empty sequence.
        /// </summary>
        public static SequenceData CreateSequence()
        {
            return new SequenceData();
        }

        /// <summary>
        /// Adds a bar to a sequence.
        /// The bar will be automatically positioned after the last bar.
        /// </summary>
        public static void AddBarToSequence(SequenceData sequence, BarData bar)
        {
            sequence.AddBar(bar);
        }

        /// <summary>
        /// Renders a sequence to a collection of positioned voices.
        /// Each bar is rendered at its calculated beat offset.
        /// </summary>
        public static List<Voice> RenderSequenceToVoices(
            SequenceData sequence,
            string synthType,
            int sampleRate,
            double bpm,
            int maxVoices = 1024)
        {
            return RenderSequenceToVoices(sequence, SynthesizerFactory.Create(synthType), sampleRate, bpm, maxVoices);
        }

        public static List<Voice> RenderSequenceToVoices(
            SequenceData sequence,
            INoteSynthesizer synthesizer,
            int sampleRate,
            double bpm,
            int maxVoices = 1024)
        {
            return RenderSequenceToVoices(sequence, synthesizer, sampleRate, bpm, RenderTuning.Default, maxVoices);
        }

        /// <summary>
        /// Phase 23: tuning-aware overload threads <see cref="RenderTuning"/> from the
        /// SongRenderer per-section resolution down through BarRenderer to each
        /// synthesizer.RenderNote invocation. Callers passing
        /// <see cref="RenderTuning.Default"/> trigger the byte-identical 12-TET path
        /// (Pitfall 6 short-circuit).
        /// </summary>
        public static List<Voice> RenderSequenceToVoices(
            SequenceData sequence,
            INoteSynthesizer synthesizer,
            int sampleRate,
            double bpm,
            RenderTuning tuning,
            int maxVoices = 1024)
        {
            var allVoices = new List<Voice>();
            var timeline = sequence.ToTimeline();

            foreach (var (bar, offsetBeats) in timeline)
            {
                // Render the bar at its position
                var barVoices = BarRenderer.RenderBarAtBeat(
                    bar,
                    offsetBeats,
                    synthesizer,
                    sampleRate,
                    bpm,
                    tuning);

                allVoices.AddRange(barVoices);
            }

            return VoiceAllocator.Allocate(allVoices, sampleRate, maxVoices);
        }

        /// <summary>
        /// Phase 28 SPEC-7: voice-pool-aware overload. Wires the active
        /// <see cref="MusicalContext.VoicePoolSize"/> (or the SPEC-locked default
        /// of 32 when the section's context didn't override it) through to
        /// <see cref="VoiceAllocator.AllocateWithPool"/> for steal-oldest behavior.
        /// Legacy <see cref="RenderSequenceToVoices"/> overloads (loudest-N policy
        /// via the existing <see cref="VoiceAllocator.Allocate"/>) are preserved
        /// for backward compatibility — direct callers (tests, REPL) work unchanged.
        /// </summary>
        public static List<Voice> RenderSequenceToVoicesWithPool(
            SequenceData sequence,
            INoteSynthesizer synthesizer,
            int sampleRate,
            double bpm,
            RenderTuning tuning,
            int? voicePoolSize)
        {
            var allVoices = new List<Voice>();
            var timeline = sequence.ToTimeline();

            foreach (var (bar, offsetBeats) in timeline)
            {
                var barVoices = BarRenderer.RenderBarAtBeat(
                    bar,
                    offsetBeats,
                    synthesizer,
                    sampleRate,
                    bpm,
                    tuning);
                allVoices.AddRange(barVoices);
            }

            int effectivePool = voicePoolSize ?? 32; // SPEC-7 locked default
            return VoiceAllocator.AllocateWithPool(allVoices, sampleRate, effectivePool, bpm);
        }

        /// <summary>
        /// Timeline-aware version of RenderSequenceToVoices.
        /// </summary>
        public static List<Voice> RenderSequenceToVoices(
            SequenceData sequence,
            string synthType,
            int sampleRate,
            double bpm,
            TimelineMap timelineMap,
            string scopeName = "top-level",
            int maxVoices = 1024)
        {
            return RenderSequenceToVoices(sequence, SynthesizerFactory.Create(synthType), sampleRate, bpm, timelineMap, scopeName, maxVoices);
        }

        public static List<Voice> RenderSequenceToVoices(
            SequenceData sequence,
            INoteSynthesizer synthesizer,
            int sampleRate,
            double bpm,
            TimelineMap timelineMap,
            string scopeName = "top-level",
            int maxVoices = 1024)
        {
            var allVoices = new List<Voice>();
            var timeline = sequence.ToTimeline();

            foreach (var (bar, offsetBeats) in timeline)
            {
                var barVoices = BarRenderer.RenderBarAtBeat(
                    bar,
                    offsetBeats,
                    synthesizer,
                    sampleRate,
                    bpm,
                    timelineMap,
                    scopeName);

                allVoices.AddRange(barVoices);
            }

            return VoiceAllocator.Allocate(allVoices, sampleRate, maxVoices);
        }
    }
}
