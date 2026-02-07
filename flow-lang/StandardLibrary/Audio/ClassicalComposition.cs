using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio
{
    public static class ClassicalComposition
    {
        /// <summary>
        /// Creates a musical note with pitch and duration.
        /// </summary>
        public static MusicalNoteData CreateMusicalNote(string pitchStr, int durationValue)
        {
            var (noteName, octave, alteration) = NoteType.Parse(pitchStr);
            return new MusicalNoteData(noteName, octave, alteration, durationValue, isRest: false);
        }

        /// <summary>
        /// Creates a rest with the specified duration.
        /// </summary>
        public static MusicalNoteData CreateRest(int durationValue)
        {
            // For rests, pitch information is irrelevant but we provide defaults
            return new MusicalNoteData('C', 4, 0, durationValue, isRest: true);
        }

        /// <summary>
        /// Creates a time signature from numerator and denominator.
        /// </summary>
        public static TimeSignatureData CreateTimeSignature(int numerator, int denominator)
        {
            return new TimeSignatureData(numerator, denominator);
        }

        /// <summary>
        /// Creates a musical bar from a list of musical notes and a time signature.
        /// Validates that the notes fit within the time signature.
        /// </summary>
        public static BarData CreateMusicalBar(List<MusicalNoteData> notes, TimeSignatureData timeSignature)
        {
            var bar = new BarData(notes, timeSignature);

            if (!bar.ValidateDuration())
            {
                double totalBeats = notes.Sum(n => n.GetBeats(timeSignature.Denominator));
                throw new InvalidOperationException(
                    $"Bar duration ({totalBeats} beats) exceeds time signature {timeSignature} ({timeSignature.Numerator} beats)"
                );
            }

            return bar;
        }

        /// <summary>
        /// Creates an empty musical bar with the specified time signature.
        /// Notes can be added incrementally using TryAddNoteToBar or AddNoteToBar.
        /// </summary>
        public static BarData CreateEmptyMusicalBar(TimeSignatureData timeSignature)
        {
            return new BarData(new List<MusicalNoteData>(), timeSignature);
        }

        /// <summary>
        /// Tries to add a note to a musical bar.
        /// Returns true if the note was added successfully, false if it would exceed the bar's capacity.
        /// </summary>
        public static bool TryAddNoteToBar(BarData bar, MusicalNoteData note)
        {
            if (bar.Mode != BarMode.Musical || bar.TimeSignature == null)
            {
                throw new InvalidOperationException("Bar must be in Musical mode with a time signature to add notes");
            }

            // Calculate current duration
            double currentBeats = bar.MusicalNotes.Sum(n => n.GetBeats(bar.TimeSignature.Denominator));
            double noteBeats = note.GetBeats(bar.TimeSignature.Denominator);
            double newTotal = currentBeats + noteBeats;

            // Check if adding this note would exceed capacity
            if (newTotal > bar.TimeSignature.Numerator)
            {
                return false;
            }

            // Add the note
            bar.MusicalNotes.Add(note);
            return true;
        }

        /// <summary>
        /// Adds a note to a musical bar.
        /// Throws InvalidOperationException if the note would exceed the bar's capacity.
        /// </summary>
        public static void AddNoteToBar(BarData bar, MusicalNoteData note)
        {
            if (!TryAddNoteToBar(bar, note))
            {
                if (bar.TimeSignature == null)
                {
                    throw new InvalidOperationException("Bar has no time signature");
                }

                double currentBeats = bar.MusicalNotes.Sum(n => n.GetBeats(bar.TimeSignature.Denominator));
                double noteBeats = note.GetBeats(bar.TimeSignature.Denominator);
                double newTotal = currentBeats + noteBeats;

                throw new InvalidOperationException(
                    $"Adding note ({noteBeats} beats) would exceed bar capacity. " +
                    $"Current: {currentBeats} beats, Would be: {newTotal} beats, Maximum: {bar.TimeSignature.Numerator} beats"
                );
            }
        }
    }
}
