using FlowLang.Ast.Expressions;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Runtime;

/// <summary>
/// Compiles a ProgressionExpression into a SequenceData with voice leading.
/// Resolves roman numerals via ScaleDatabase, applies nearest-neighbor voice leading
/// (bass follows root, upper voices minimize movement), and builds bars with musical notes.
/// </summary>
public class ProgressionCompiler
{
    /// <summary>
    /// Map note names to semitone offsets from C.
    /// </summary>
    private static readonly Dictionary<string, int> NoteToSemitone = new(StringComparer.OrdinalIgnoreCase)
    {
        { "C", 0 }, { "Cs", 1 }, { "Db", 1 },
        { "D", 2 }, { "Ds", 3 }, { "Eb", 3 },
        { "E", 4 },
        { "F", 5 }, { "Fs", 6 }, { "Gb", 6 },
        { "G", 7 }, { "Gs", 8 }, { "Ab", 8 },
        { "A", 9 }, { "As", 10 }, { "Bb", 10 },
        { "B", 11 },
    };

    /// <summary>
    /// Chromatic note names indexed by semitone (0=C ... 11=B).
    /// </summary>
    private static readonly (char NoteName, int Alteration)[] SemitoneToNote =
    {
        ('C', 0),  // 0
        ('C', 1),  // 1  C#
        ('D', 0),  // 2
        ('D', 1),  // 3  D#
        ('E', 0),  // 4
        ('F', 0),  // 5
        ('F', 1),  // 6  F#
        ('G', 0),  // 7
        ('G', 1),  // 8  G#
        ('A', 0),  // 9
        ('A', 1),  // 10 A#
        ('B', 0),  // 11
    };

    /// <summary>
    /// Compiles a progression expression into a SequenceData with voice-led chords.
    /// </summary>
    public SequenceData Compile(ProgressionExpression expr, MusicalContext context)
    {
        if (context.Key == null)
            throw new InvalidOperationException(
                "progression requires an active key context (use `key Cmajor { ... }`)");

        // Get time signature (default 4/4)
        var timeSig = context.TimeSignature ?? new TimeSignatureData(4, 4);

        // Resolve all roman numerals to ChordData
        var chords = new List<(ChordData chord, int barCount)>();
        foreach (var elem in expr.Chords)
        {
            var chordData = ScaleDatabase.ResolveRomanNumeral(elem.Numeral, context.Key);
            if (chordData == null)
                throw new InvalidOperationException(
                    $"Cannot resolve '{elem.Numeral}' in key {context.Key}");
            chords.Add((chordData, elem.BarCount));
        }

        if (chords.Count == 0)
            throw new InvalidOperationException("Progression must contain at least one chord");

        // Determine voice count: explicit, or max chord note count
        int voiceCount = expr.VoiceCount ?? chords.Max(c => c.chord.NoteNames.Length);
        if (voiceCount < 1) voiceCount = 3; // minimum sensible voice count

        // Voice leading: build sequence
        var sequence = new SequenceData();
        int[] currentPitches = InitializeVoices(chords[0].chord, voiceCount);

        for (int i = 0; i < chords.Count; i++)
        {
            var (chord, barCount) = chords[i];
            if (i > 0)
                currentPitches = ApplyVoiceLeading(currentPitches, chord, voiceCount);

            for (int b = 0; b < barCount; b++)
            {
                var bar = BuildBar(currentPitches, timeSig);
                sequence.AddBar(bar);
            }
        }

        return sequence;
    }

    /// <summary>
    /// Initializes voice pitches for the first chord in root position.
    /// Bass voice at octave 3 (MIDI 48-59), upper voices spread in octave 4 —
    /// keeping the whole chord within a ~octave span so it reads as a block chord
    /// rather than a wide, bassy spread. (FIX 0615: bass was at octave 2 / MIDI 36.)
    /// </summary>
    private int[] InitializeVoices(ChordData chord, int voiceCount)
    {
        var chordTones = GetChordMidiPitches(chord);
        var pitches = new int[voiceCount];

        // Bass voice: root at octave 3 (C3 = MIDI 48, bass range ~48-59)
        int rootSemitone = GetRootSemitone(chord);
        pitches[0] = 48 + rootSemitone;

        // Upper voices: spread chord tones in octave 4 (MIDI 60-72)
        for (int v = 1; v < voiceCount; v++)
        {
            if (v - 1 < chordTones.Length)
            {
                // Place chord tone in octave 4
                int semitone = chordTones[v - 1] % 12;
                pitches[v] = 60 + semitone;
            }
            else
            {
                // Duplicate: use root or fifth in a higher register
                int semitone = chordTones[(v - 1) % chordTones.Length] % 12;
                pitches[v] = 60 + semitone;
                // If duplicating, try an octave higher to avoid unison
                if (Array.IndexOf(pitches, pitches[v], 1, v - 1) >= 0 && pitches[v] + 12 <= 84)
                    pitches[v] += 12;
            }
        }

        return pitches;
    }

    /// <summary>
    /// Applies voice leading from previous pitches to a new chord.
    /// Bass follows root; upper voices move to nearest available chord tone.
    /// </summary>
    private int[] ApplyVoiceLeading(int[] prevPitches, ChordData chord, int voiceCount)
    {
        var newPitches = new int[voiceCount];
        var chordTones = GetChordMidiPitches(chord);
        int rootSemitone = GetRootSemitone(chord);

        // Bass voice: find nearest root pitch in octave-3 range 48-59
        // (FIX 0615: was 36-55, which dipped into octave 2 and sounded over-bassy).
        newPitches[0] = FindNearestPitchClass(prevPitches[0], rootSemitone, 48, 59);

        // Track which target pitches are used to avoid unison
        var usedPitches = new HashSet<int> { newPitches[0] };

        // Upper voices: find nearest chord tone
        for (int v = 1; v < voiceCount; v++)
        {
            int bestPitch = prevPitches[v]; // fallback
            int bestDistance = int.MaxValue;

            // Try each chord tone at various octaves within range 48-84
            foreach (int chordTone in chordTones)
            {
                int semitone = chordTone % 12;
                for (int oct = 3; oct <= 6; oct++) // MIDI 48-84 range (C3 to C6)
                {
                    int candidate = oct * 12 + semitone;
                    if (candidate < 48 || candidate > 84)
                        continue;

                    int distance = Math.Abs(candidate - prevPitches[v]);
                    if (distance < bestDistance && !usedPitches.Contains(candidate))
                    {
                        bestDistance = distance;
                        bestPitch = candidate;
                    }
                }
            }

            // If all candidates were used, allow duplicates with different octave
            if (bestDistance == int.MaxValue)
            {
                foreach (int chordTone in chordTones)
                {
                    int semitone = chordTone % 12;
                    for (int oct = 3; oct <= 6; oct++)
                    {
                        int candidate = oct * 12 + semitone;
                        if (candidate < 48 || candidate > 84)
                            continue;

                        int distance = Math.Abs(candidate - prevPitches[v]);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestPitch = candidate;
                        }
                    }
                }
            }

            newPitches[v] = bestPitch;
            usedPitches.Add(bestPitch);
        }

        return newPitches;
    }

    /// <summary>
    /// Builds a musical bar from MIDI pitches, with all notes sounding simultaneously
    /// (as a block chord) for the full bar duration.
    /// </summary>
    /// <remarks>
    /// FIX 0615: the first voice is the chord lead (IsChordTone=false — it advances the
    /// bar's beat cursor); every remaining voice carries IsChordTone=true so it shares the
    /// lead's onset rather than being played sequentially. This is the SAME simultaneity
    /// mechanism a note-stream chord bracket <c>[C4 E4 G4]</c> uses
    /// (see <see cref="NoteStreamCompiler"/> / <see cref="MusicalNoteData.IsChordTone"/> /
    /// <c>BarType.ToTimeline</c>). Before this fix the voices were sequential
    /// MusicalNoteData with no chord marking, so a progression arpeggiated instead of
    /// striking a block chord.
    /// </remarks>
    private static BarData BuildBar(int[] pitches, TimeSignatureData timeSig)
    {
        var notes = new List<MusicalNoteData>();

        // Determine duration value to fill one bar.
        // In 4/4 time, a whole note fills the bar. In 3/4, a dotted half.
        // For simplicity, use whole note (DurationValue=0 = WHOLE) which in 4/4
        // gives 1.0 * 4 = 4 beats. For other time sigs, we adjust.
        int durationValue = (int)NoteValueType.Value.WHOLE; // whole note

        bool first = true;
        foreach (int midi in pitches)
        {
            var (noteName, alteration) = FromMidi(midi);
            int octave = midi / 12 - 1; // MIDI convention: C4 = 60, so octave = 60/12 - 1 = 4

            notes.Add(new MusicalNoteData(
                noteName: noteName,
                octave: octave,
                alteration: alteration,
                durationValue: durationValue,
                isRest: false,
                velocity: 0.63,
                // Lead voice keeps IsChordTone=false; the rest stack on its onset.
                isChordTone: !first
            ));
            first = false;
        }

        return new BarData(notes, timeSig);
    }

    /// <summary>
    /// Converts a MIDI note number to (NoteName, Alteration).
    /// </summary>
    private static (char NoteName, int Alteration) FromMidi(int midi)
    {
        int semitone = ((midi % 12) + 12) % 12; // ensure positive
        return SemitoneToNote[semitone];
    }

    /// <summary>
    /// Converts a MIDI note number to absolute semitone (note class 0-11).
    /// </summary>
    private static int ToMidi(char noteName, int octave, int alteration)
    {
        int baseSemitone = noteName switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5,
            'G' => 7, 'A' => 9, 'B' => 11,
            _ => 0
        };
        return (octave + 1) * 12 + baseSemitone + alteration;
    }

    /// <summary>
    /// Gets the MIDI pitch classes (semitones 0-11) for all notes in a chord.
    /// </summary>
    private static int[] GetChordMidiPitches(ChordData chord)
    {
        var pitches = new List<int>();
        foreach (string noteName in chord.NoteNames)
        {
            // NoteNames are like "C4", "E4", "G4", "C4+" (sharp)
            if (TryParseNoteName(noteName, out int semitone))
            {
                pitches.Add(semitone);
            }
        }
        return pitches.Count > 0 ? pitches.ToArray() : new[] { 0, 4, 7 }; // fallback to C major
    }

    /// <summary>
    /// Parses a note name string (e.g., "C4", "E4", "G4+") into a semitone value (0-11).
    /// </summary>
    private static bool TryParseNoteName(string name, out int semitone)
    {
        semitone = 0;
        if (string.IsNullOrEmpty(name))
            return false;

        char letter = name[0];
        int baseSemitone = letter switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5,
            'G' => 7, 'A' => 9, 'B' => 11,
            _ => -1
        };

        if (baseSemitone < 0)
            return false;

        // Check for sharp/flat indicators
        int alteration = 0;
        for (int i = 1; i < name.Length; i++)
        {
            if (name[i] == '+') alteration++;
            else if (name[i] == '-') alteration--;
            else if (name[i] == 's' && i == 1) alteration++; // "Cs" format
            else if (name[i] == 'f' && i == 1) alteration--; // "Cf" format
        }

        semitone = ((baseSemitone + alteration) % 12 + 12) % 12;
        return true;
    }

    /// <summary>
    /// Gets the root semitone (0-11) for a chord.
    /// </summary>
    private static int GetRootSemitone(ChordData chord)
    {
        string root = chord.Root;
        if (NoteToSemitone.TryGetValue(root, out int semitone))
            return semitone;

        // Fallback: parse first character
        return root[0] switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5,
            'G' => 7, 'A' => 9, 'B' => 11,
            _ => 0
        };
    }

    /// <summary>
    /// Finds the nearest MIDI pitch with the given pitch class within a range.
    /// </summary>
    private static int FindNearestPitchClass(int currentPitch, int targetPitchClass, int minPitch, int maxPitch)
    {
        int bestPitch = -1;
        int bestDistance = int.MaxValue;

        for (int oct = minPitch / 12; oct <= maxPitch / 12 + 1; oct++)
        {
            int candidate = oct * 12 + targetPitchClass;
            if (candidate < minPitch || candidate > maxPitch)
                continue;

            int distance = Math.Abs(candidate - currentPitch);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPitch = candidate;
            }
        }

        // If no candidate found in range, clamp
        if (bestPitch < 0)
            bestPitch = Math.Clamp(currentPitch / 12 * 12 + targetPitchClass, minPitch, maxPitch);

        return bestPitch;
    }
}
