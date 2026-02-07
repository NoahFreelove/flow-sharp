using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio
{
    public static class MusicalConversions
    {
        /// <summary>
        /// Converts a note value to beats based on the time signature denominator.
        /// Example: Quarter note (0.25) in 4/4 = 0.25 * 4 = 1 beat
        /// Example: Eighth note (0.125) in 6/8 = 0.125 * 8 = 1 beat
        /// </summary>
        public static double NoteValueToBeats(int noteValueEnum, int denominator)
        {
            double fraction = NoteValueType.ToFraction((NoteValueType.Value)noteValueEnum);
            return fraction * denominator;
        }

        /// <summary>
        /// Returns the number of beats per bar based on the time signature numerator.
        /// </summary>
        public static double BeatsPerBar(TimeSignatureData timeSignature)
        {
            return timeSignature.Numerator;
        }

        /// <summary>
        /// Validates that the total duration of notes in a bar fits within the time signature.
        /// </summary>
        public static bool ValidateBarDuration(BarData bar, TimeSignatureData timeSignature)
        {
            if (bar.Mode != BarMode.Musical)
                return true;

            double totalBeats = 0;
            foreach (var note in bar.MusicalNotes)
            {
                totalBeats += note.GetBeats(timeSignature.Denominator);
            }

            return totalBeats <= timeSignature.Numerator;
        }

        /// <summary>
        /// Calculates the total duration of a bar in beats.
        /// </summary>
        public static double CalculateBarDuration(BarData bar, TimeSignatureData timeSignature)
        {
            if (bar.Mode != BarMode.Musical)
                return timeSignature.Numerator; // Default to full bar

            double totalBeats = 0;
            foreach (var note in bar.MusicalNotes)
            {
                totalBeats += note.GetBeats(timeSignature.Denominator);
            }

            return totalBeats;
        }

        /// <summary>
        /// Returns the remaining capacity in a bar (in beats).
        /// Returns 0 if the bar is full or over capacity.
        /// </summary>
        public static double GetRemainingBeats(BarData bar)
        {
            if (bar.Mode != BarMode.Musical || bar.TimeSignature == null)
            {
                return 0;
            }

            double totalBeats = 0;
            foreach (var note in bar.MusicalNotes)
            {
                totalBeats += note.GetBeats(bar.TimeSignature.Denominator);
            }

            double remaining = bar.TimeSignature.Numerator - totalBeats;
            return Math.Max(0, remaining);
        }

        /// <summary>
        /// Checks if a note would fit in the bar without exceeding its capacity.
        /// </summary>
        public static bool WouldFit(BarData bar, MusicalNoteData note)
        {
            if (bar.Mode != BarMode.Musical || bar.TimeSignature == null)
            {
                return false;
            }

            double currentBeats = 0;
            foreach (var n in bar.MusicalNotes)
            {
                currentBeats += n.GetBeats(bar.TimeSignature.Denominator);
            }

            double noteBeats = note.GetBeats(bar.TimeSignature.Denominator);
            return (currentBeats + noteBeats) <= bar.TimeSignature.Numerator;
        }

        /// <summary>
        /// Calculates how much overflow exists if the bar exceeds its capacity.
        /// Returns 0 if the bar is within capacity.
        /// Returns positive number representing excess beats if over capacity.
        /// </summary>
        public static double CalculateOverflow(BarData bar)
        {
            if (bar.Mode != BarMode.Musical || bar.TimeSignature == null)
            {
                return 0;
            }

            double totalBeats = 0;
            foreach (var note in bar.MusicalNotes)
            {
                totalBeats += note.GetBeats(bar.TimeSignature.Denominator);
            }

            double overflow = totalBeats - bar.TimeSignature.Numerator;
            return Math.Max(0, overflow);
        }
    }
}
