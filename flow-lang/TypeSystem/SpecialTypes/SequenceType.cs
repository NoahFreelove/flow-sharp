using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowLang.TypeSystem.SpecialTypes
{
    /// <summary>
    /// Runtime representation of a Sequence containing ordered bars.
    /// </summary>
    public class SequenceData
    {
        /// <summary>
        /// The ordered list of bars in this sequence.
        /// </summary>
        public List<BarData> Bars { get; }

        /// <summary>
        /// The total duration of the sequence in beats.
        /// </summary>
        public double TotalBeats { get; private set; }

        public SequenceData()
        {
            Bars = new List<BarData>();
            TotalBeats = 0;
        }

        /// <summary>
        /// Adds a bar to the sequence.
        /// The bar will be positioned after the last bar in the sequence.
        /// </summary>
        public void AddBar(BarData bar)
        {
            if (bar.Mode != BarMode.Musical || bar.TimeSignature == null)
            {
                throw new InvalidOperationException("Only musical bars with time signatures can be added to sequences");
            }

            Bars.Add(bar);
            TotalBeats += bar.TimeSignature.Numerator;
        }

        /// <summary>
        /// Converts the sequence to a timeline with each bar's offset in beats.
        /// </summary>
        public List<(BarData bar, double offsetBeats)> ToTimeline()
        {
            var timeline = new List<(BarData, double)>();
            double offset = 0;

            foreach (var bar in Bars)
            {
                timeline.Add((bar, offset));
                if (bar.TimeSignature != null)
                {
                    offset += bar.TimeSignature.Numerator;
                }
            }

            return timeline;
        }

        /// <summary>
        /// Returns the number of bars in this sequence.
        /// </summary>
        public int Count => Bars.Count;

        /// <summary>
        /// Formats the sequence as a string.
        /// </summary>
        public override string ToString()
        {
            return $"Sequence[{Count} bars, {TotalBeats} beats total]";
        }
    }

    /// <summary>
    /// Represents a sequence of musical bars.
    /// </summary>
    public sealed class SequenceType : FlowType
    {
        private SequenceType() { }

        public static SequenceType Instance { get; } = new();

        public override string Name => "Sequence";

        public override int GetSpecificity() => 134;
    }
}
