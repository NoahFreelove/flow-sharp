using System;
using System.Collections.Generic;
using FlowLang.StandardLibrary.Audio.Tuning;
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
        /// Phase 23 tuning-aware overload. Renders a note's frequency under the active
        /// <see cref="RenderTuning"/> per the live runtime pipeline. The byte-identical
        /// 12-TET fast path is the load-bearing mechanism: when
        /// <c>tuning.System == TuningSystem.EqualTemperament</c> this overload literally
        /// delegates to the 1-arg <see cref="NoteToFrequency(MusicalNoteData)"/> body
        /// (Pitfall 6 contract). Default-pragma + explicit-equalTemperament + no-pragma
        /// must all produce byte-identical output.
        ///
        /// Non-12-TET path (CONTEXT D-10):
        ///   freq = TonicHzFromKey(tuning.tonic, note.Octave) × LookupRatio(...) × CentOffsetMultiplier(cents)
        /// Cents are applied AFTER the ratio multiply per D-10 cent-additive math.
        ///
        /// D-02 silent C-major default: when no <c>key</c> block is in scope, the caller
        /// (SongRenderer.ResolveRenderTuning) silently roots at C major (tonic = ('C', 0),
        /// mode = Major) so the rendered output is musically meaningful even without an
        /// explicit key declaration — aligns with charitable-interpretation memory.
        ///
        /// D-09 spelling-aware: the chromatic ratio table keys on (Letter, Alteration) so
        /// Eb (E, -1) and D# (D, +1) produce distinct ratios under JI / Pythagorean.
        ///
        /// Pitfall 3 chromatic fallback: non-diatonic spellings absent from the mode-
        /// specific table fall back to the Major (Ionian) table for the same tuning
        /// system.
        /// </summary>
        public static double NoteToFrequency(MusicalNoteData note, RenderTuning tuning)
        {
            if (note.IsRest) return 0.0;

            // Phase 32 D-03 custom-tuning branch (MUST appear BEFORE the 12-TET
            // short-circuit per Pitfall 3 mutual-exclusion guard). When a user-supplied
            // .scl is active, read the precomputed O(1) MidiToHz lookup directly. The
            // 128-entry table was eagerly populated at ResolvedTuning ctor time per
            // D-02 — render-time cost is one array index + one cent-offset multiply.
            if (tuning.Custom is not null)
            {
                int midi = GetMidiNote(note.NoteName, note.Octave, note.Alteration);
                if (midi < 0 || midi > 127) return 0.0;
                double hz = tuning.Custom.MidiToHz[midi];
                if (note.CentOffset.HasValue && note.CentOffset.Value != 0.0 && hz > 0.0)
                    hz *= RatioMath.CentOffsetMultiplier(note.CentOffset.Value);
                return hz;
            }

            // Pitfall 6: EqualTemperament short-circuits to literally the existing 1-arg
            // overload body so default-pragma + explicit-equalTemperament + no-pragma all
            // produce byte-identical output. This guards the ByteIdentical regression
            // suite across tutorial.flow + showcase.flow + Phase 18-22 byte-identical
            // Facts after Pattern A threading lands.
            //
            // Pitfall 3 (Phase 32) mutual-exclusion: the predicate ALSO requires
            // `tuning.Custom is null` so that if someone hand-constructs
            // `new RenderTuning(EqualTemperament, …, custom: someResolved)`, the EQ
            // short-circuit does NOT silently swallow the override. The early return
            // above handles that case correctly; the `Custom is null` requirement here
            // is defense-in-depth so a future refactor (e.g. dropping the early return,
            // restructuring the dispatch) doesn't reintroduce the silent-swallow bug.
            if (tuning.Custom is null && tuning.System == TuningSystem.EqualTemperament)
            {
                double eqFreq = NoteToFrequency(note); // delegates to existing 1-arg overload — UNCHANGED body
                if (note.CentOffset.HasValue && note.CentOffset.Value != 0.0)
                    eqFreq *= RatioMath.CentOffsetMultiplier(note.CentOffset.Value);
                return eqFreq;
            }

            // Non-12-TET path: tonic Hz × ratio × cent multiplier (D-10).
            double tonicHz = RatioMath.TonicHzFromKey(tuning.TonicLetter, tuning.TonicAlteration, note.Octave);
            double ratio;
            try
            {
                ratio = TuningTables.LookupRatio(tuning.System, tuning.Mode, note.NoteName, note.Alteration);
            }
            catch (KeyNotFoundException)
            {
                // Pitfall 3 chromatic fallback: non-diatonic chromatic spellings in mode
                // tables fall back to the Major (Ionian) table for the same tuning
                // system. Charitable interpretation: rather than throw on an obscure
                // chromatic spelling, route it through the closest authoritative table.
                ratio = TuningTables.LookupRatio(tuning.System, Mode.Major, note.NoteName, note.Alteration);
            }
            double freq = tonicHz * ratio;
            if (note.CentOffset.HasValue && note.CentOffset.Value != 0.0)
                freq *= RatioMath.CentOffsetMultiplier(note.CentOffset.Value);
            return freq;
        }

        /// <summary>
        /// Converts note information to a MIDI note number.
        /// C4 (middle C) = 60, A4 = 69
        /// </summary>
        public static int GetMidiNote(char noteName, int octave, int alteration)
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
