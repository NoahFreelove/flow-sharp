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
            double bpm)
        {
            var allVoices = new List<Voice>();
            var timeline = sequence.ToTimeline();

            foreach (var (bar, offsetBeats) in timeline)
            {
                // Render the bar at its position
                var barVoices = BarRenderer.RenderBarAtBeat(
                    bar,
                    offsetBeats,
                    synthType,
                    sampleRate,
                    bpm);

                allVoices.AddRange(barVoices);
            }

            return allVoices;
        }
    }
}
