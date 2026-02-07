using System;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio
{
    public static class PitchConversion
    {
        /// <summary>
        /// Converts a musical note to its frequency in Hz.
        /// Uses the formula: freq = 440 * 2^((midiNote - 69) / 12)
        /// where A4 = 440 Hz (MIDI note 69)
        /// </summary>
        public static double NoteToFrequency(char noteName, int octave, int alteration)
        {
            int midiNote = GetMidiNote(noteName, octave, alteration);
            return 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);
        }

        /// <summary>
        /// Overload that takes a MusicalNoteData object.
        /// </summary>
        public static double NoteToFrequency(MusicalNoteData note)
        {
            if (note.IsRest)
                return 0.0; // Rests have no frequency

            return NoteToFrequency(note.NoteName, note.Octave, note.Alteration);
        }

        /// <summary>
        /// Converts note information to a MIDI note number.
        /// C4 (middle C) = 60, A4 = 69
        /// </summary>
        private static int GetMidiNote(char noteName, int octave, int alteration)
        {
            int noteOffset = noteName switch
            {
                'C' => 0,
                'D' => 2,
                'E' => 4,
                'F' => 5,
                'G' => 7,
                'A' => 9,
                'B' => 11,
                _ => throw new ArgumentException($"Invalid note name: {noteName}")
            };

            // MIDI note calculation: (octave + 1) * 12 + noteOffset + alteration
            return (octave + 1) * 12 + noteOffset + alteration;
        }
    }
}
