using FlowLang.Runtime;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Composition;

/// <summary>
/// Provides the vary() built-in function for probabilistic pattern variation.
/// Supports four mutation types (pitch, rhythm, rest, velocity) with optional
/// diatonic pitch constraint and seeded reproducibility.
/// </summary>
public static class VariationFunctions
{
    private const int MIDI_MIN = 36;  // C2
    private const int MIDI_MAX = 96;  // C7

    private static readonly string[] MutationTypes = ["pitch", "rhythm", "rest", "velocity"];

    public static void Register(InternalFunctionRegistry registry)
    {
        // vary(Sequence, Double) -> Sequence (random mutation type, chromatic)
        var sig1 = new FunctionSignature("vary",
            [SequenceType.Instance, DoubleType.Instance]);
        registry.Register("vary", sig1, VaryRandom);

        // vary(Sequence, Double, String) -> Sequence (specific mutation type, chromatic)
        var sig2 = new FunctionSignature("vary",
            [SequenceType.Instance, DoubleType.Instance, StringType.Instance]);
        registry.Register("vary", sig2, VaryTyped);

        // vary(Sequence, Double, Int) -> Sequence (random mutation type, seeded)
        var sig3 = new FunctionSignature("vary",
            [SequenceType.Instance, DoubleType.Instance, IntType.Instance]);
        registry.Register("vary", sig3, VarySeeded);

        // vary(Sequence, Double, String, Int) -> Sequence (specific type, seeded)
        var sig4 = new FunctionSignature("vary",
            [SequenceType.Instance, DoubleType.Instance, StringType.Instance, IntType.Instance]);
        registry.Register("vary", sig4, VaryTypedSeeded);

        // vary(Sequence, Double, String, String) -> Sequence (specific type, diatonic with key)
        var sig5 = new FunctionSignature("vary",
            [SequenceType.Instance, DoubleType.Instance, StringType.Instance, StringType.Instance]);
        registry.Register("vary", sig5, VaryTypedWithKey);

        // vary(Sequence, Double, String, String, Int) -> Sequence (specific type, diatonic, seeded)
        var sig6 = new FunctionSignature("vary",
            [SequenceType.Instance, DoubleType.Instance, StringType.Instance, StringType.Instance, IntType.Instance]);
        registry.Register("vary", sig6, VaryTypedWithKeySeed);
    }

    // ===== Overload Entry Points =====

    private static Value VaryRandom(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double probability = args[1].As<double>();
        return Value.Sequence(ApplyVariation(seq, probability, null, new Random(), null));
    }

    private static Value VaryTyped(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double probability = args[1].As<double>();
        string mutationType = (string)args[2].Data!;
        return Value.Sequence(ApplyVariation(seq, probability, mutationType, new Random(), null));
    }

    private static Value VarySeeded(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double probability = args[1].As<double>();
        int seed = args[2].As<int>();
        return Value.Sequence(ApplyVariation(seq, probability, null, new Random(seed), null));
    }

    private static Value VaryTypedSeeded(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double probability = args[1].As<double>();
        string mutationType = (string)args[2].Data!;
        int seed = args[3].As<int>();
        return Value.Sequence(ApplyVariation(seq, probability, mutationType, new Random(seed), null));
    }

    private static Value VaryTypedWithKey(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double probability = args[1].As<double>();
        string mutationType = (string)args[2].Data!;
        string keyContext = (string)args[3].Data!;
        return Value.Sequence(ApplyVariation(seq, probability, mutationType, new Random(), keyContext));
    }

    private static Value VaryTypedWithKeySeed(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double probability = args[1].As<double>();
        string mutationType = (string)args[2].Data!;
        string keyContext = (string)args[3].Data!;
        int seed = args[4].As<int>();
        return Value.Sequence(ApplyVariation(seq, probability, mutationType, new Random(seed), keyContext));
    }

    // ===== Core Variation Logic =====

    private static SequenceData ApplyVariation(
        SequenceData seq, double probability, string? mutationType, Random rng, string? keyContext)
    {
        var result = new SequenceData();

        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();

            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest || rng.NextDouble() >= probability)
                {
                    // Keep original note (no mutation)
                    newNotes.Add(note);
                    continue;
                }

                // Pick mutation type
                string type = mutationType ?? MutationTypes[rng.Next(MutationTypes.Length)];

                switch (type.ToLowerInvariant())
                {
                    case "pitch":
                        newNotes.Add(MutatePitch(note, rng, keyContext));
                        break;
                    case "rhythm":
                        MutateRhythm(note, rng, newNotes);
                        break;
                    case "rest":
                        newNotes.Add(MutateToRest(note));
                        break;
                    case "velocity":
                        newNotes.Add(MutateVelocity(note, rng));
                        break;
                    default:
                        newNotes.Add(note); // Unknown type, keep original
                        break;
                }
            }

            var newBar = new BarData(newNotes, bar.TimeSignature!);
            result.AddBar(newBar);
        }

        return result;
    }

    // ===== Mutation Implementations =====

    /// <summary>
    /// Pitch mutation: diatonic when key is provided, chromatic fallback otherwise.
    /// </summary>
    private static MusicalNoteData MutatePitch(MusicalNoteData note, Random rng, string? keyContext)
    {
        if (keyContext != null)
        {
            // Diatonic pitch mutation (D-17)
            var scaleNotes = ScaleDatabase.GetScaleNotes(keyContext);
            if (scaleNotes != null)
                return MutatePitchDiatonic(note, rng, scaleNotes);
        }

        // Chromatic fallback
        return MutatePitchChromatic(note, rng);
    }

    private static MusicalNoteData MutatePitchDiatonic(MusicalNoteData note, Random rng, string[] scaleNotes)
    {
        // Find current note in scale by matching note name
        string currentNoteName = NoteToScaleName(note.NoteName, note.Alteration);
        int scaleIndex = -1;

        for (int i = 0; i < scaleNotes.Length; i++)
        {
            if (string.Equals(scaleNotes[i], currentNoteName, StringComparison.OrdinalIgnoreCase))
            {
                scaleIndex = i;
                break;
            }
        }

        if (scaleIndex < 0)
        {
            // Note not in scale, use chromatic fallback
            return MutatePitchChromatic(note, rng);
        }

        // Shift by 1-2 scale degrees in either direction
        int shift = rng.Next(-2, 3); // -2, -1, 0, 1, 2
        if (shift == 0) shift = 1; // Ensure actual change

        int newScaleIndex = scaleIndex + shift;
        int octaveShift = 0;

        // Handle wrapping around the scale
        while (newScaleIndex < 0)
        {
            newScaleIndex += scaleNotes.Length;
            octaveShift--;
        }
        while (newScaleIndex >= scaleNotes.Length)
        {
            newScaleIndex -= scaleNotes.Length;
            octaveShift++;
        }

        string newScaleNote = scaleNotes[newScaleIndex];
        var (newNoteName, newAlteration) = ScaleNameToNote(newScaleNote);
        int newOctave = note.Octave + octaveShift;

        // Clamp MIDI range
        int midi = ToMidi(newNoteName, newOctave, newAlteration);
        midi = Math.Clamp(midi, MIDI_MIN, MIDI_MAX);
        var (clampedName, clampedOctave, clampedAlt) = FromMidi(midi);

        return new MusicalNoteData(
            clampedName, clampedOctave, clampedAlt,
            note.DurationValue, false, note.CentOffset, note.IsTied,
            note.Velocity, note.Articulation, note.IsDotted);
    }

    private static MusicalNoteData MutatePitchChromatic(MusicalNoteData note, Random rng)
    {
        int midi = ToMidi(note.NoteName, note.Octave, note.Alteration);
        int shift = rng.Next(-2, 3); // -2 to +2 semitones
        if (shift == 0) shift = 1;

        midi = Math.Clamp(midi + shift, MIDI_MIN, MIDI_MAX);
        var (newName, newOctave, newAlt) = FromMidi(midi);

        return new MusicalNoteData(
            newName, newOctave, newAlt,
            note.DurationValue, false, note.CentOffset, note.IsTied,
            note.Velocity, note.Articulation, note.IsDotted);
    }

    /// <summary>
    /// Rhythm mutation: splits a note into two notes of half duration.
    /// </summary>
    private static void MutateRhythm(MusicalNoteData note, Random rng, List<MusicalNoteData> output)
    {
        // Only split if duration allows halving
        // NoteValueType.Value enum: WHOLE=0, HALF=1, QUARTER=2, EIGHTH=3, SIXTEENTH=4
        int? halfDuration = note.DurationValue switch
        {
            0 => 1,   // whole -> half
            1 => 2,   // half -> quarter
            2 => 3,   // quarter -> eighth
            3 => 4,   // eighth -> sixteenth
            _ => null  // Can't split further (sixteenth) or unknown
        };

        if (halfDuration == null)
        {
            output.Add(note); // Can't split, keep original
            return;
        }

        // Create two notes of half duration with same pitch
        var split1 = new MusicalNoteData(
            note.NoteName, note.Octave, note.Alteration,
            halfDuration.Value, false, note.CentOffset, false,
            note.Velocity, note.Articulation, false);
        var split2 = new MusicalNoteData(
            note.NoteName, note.Octave, note.Alteration,
            halfDuration.Value, false, note.CentOffset, false,
            note.Velocity, note.Articulation, false);

        output.Add(split1);
        output.Add(split2);
    }

    /// <summary>
    /// Rest mutation: replaces note with a rest of the same duration.
    /// </summary>
    private static MusicalNoteData MutateToRest(MusicalNoteData note)
    {
        return new MusicalNoteData(
            ' ', 0, 0,
            note.DurationValue, isRest: true,
            isDotted: note.IsDotted);
    }

    /// <summary>
    /// Velocity mutation: adjusts velocity by +/- 0.2, clamped to [0.05, 1.0].
    /// </summary>
    private static MusicalNoteData MutateVelocity(MusicalNoteData note, Random rng)
    {
        double newVelocity = note.Velocity + (rng.NextDouble() * 0.4 - 0.2);
        newVelocity = Math.Clamp(newVelocity, 0.05, 1.0);

        return new MusicalNoteData(
            note.NoteName, note.Octave, note.Alteration,
            note.DurationValue, false, note.CentOffset, note.IsTied,
            newVelocity, note.Articulation, note.IsDotted);
    }

    // ===== MIDI Helpers (same logic as TransformFunctions) =====

    private static int ToMidi(char noteName, int octave, int alteration)
    {
        int noteOffset = noteName switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5,
            'G' => 7, 'A' => 9, 'B' => 11,
            _ => throw new ArgumentException($"Invalid note name: {noteName}")
        };
        return (octave + 1) * 12 + noteOffset + alteration;
    }

    private static (char NoteName, int Octave, int Alteration) FromMidi(int midi)
    {
        int octave = (midi / 12) - 1;
        int pitchClass = midi % 12;
        if (pitchClass < 0) { pitchClass += 12; octave--; }

        var (noteName, alteration) = pitchClass switch
        {
            0  => ('C', 0),
            1  => ('C', 1),
            2  => ('D', 0),
            3  => ('D', 1),
            4  => ('E', 0),
            5  => ('F', 0),
            6  => ('F', 1),
            7  => ('G', 0),
            8  => ('G', 1),
            9  => ('A', 0),
            10 => ('A', 1),
            11 => ('B', 0),
            _  => ('C', 0)
        };

        return (noteName, octave, alteration);
    }

    // ===== Scale Name Helpers =====

    /// <summary>
    /// Converts a MusicalNoteData note name + alteration to a scale name string (e.g., "C", "Cs", "Fs").
    /// </summary>
    private static string NoteToScaleName(char noteName, int alteration)
    {
        if (alteration == 0) return noteName.ToString();
        if (alteration == 1) return $"{noteName}s";
        if (alteration == -1)
        {
            // Flat: convert to enharmonic sharp equivalent
            // Db -> Cs, Eb -> Ds, Gb -> Fs, Ab -> Gs, Bb -> As
            int midi = ToMidi(noteName, 4, alteration);
            int pitchClass = midi % 12;
            return pitchClass switch
            {
                1 => "Cs", 3 => "Ds", 6 => "Fs", 8 => "Gs", 10 => "As",
                _ => noteName.ToString()
            };
        }
        return noteName.ToString();
    }

    /// <summary>
    /// Converts a scale name string (e.g., "C", "Cs", "Fs") back to note name + alteration.
    /// </summary>
    private static (char NoteName, int Alteration) ScaleNameToNote(string scaleName)
    {
        if (scaleName.Length == 1) return (scaleName[0], 0);
        if (scaleName.Length == 2 && scaleName[1] == 's') return (scaleName[0], 1);
        return (scaleName[0], 0);
    }
}
