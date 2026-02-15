using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Transforms;

/// <summary>
/// Registers pattern transform functions for sequences: transpose, invert, retrograde,
/// augment, diminish, up, down, repeat, and concat.
/// </summary>
public static class TransformFunctions
{
    private const int MIDI_MIN = 16;  // E0
    private const int MIDI_MAX = 136; // E10

    public static void Register(InternalFunctionRegistry registry)
    {
        RegisterTranspose(registry);
        RegisterInvert(registry);
        RegisterRetrograde(registry);
        RegisterAugmentDiminish(registry);
        RegisterOctaveShift(registry);
        RegisterRepeat(registry);
        RegisterConcat(registry);
    }

    // ===== MIDI Helpers =====

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

        // Map chromatic pitches to naturals + alteration (sharps only)
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

    /// <summary>
    /// Applies a transform function to every note in every bar, producing a new SequenceData.
    /// </summary>
    private static SequenceData TransformNotes(SequenceData seq, Func<MusicalNoteData, MusicalNoteData> transform)
    {
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                newNotes.Add(transform(note));
            }
            var newBar = new BarData(newNotes, bar.TimeSignature!);
            result.AddBar(newBar);
        }
        return result;
    }

    // ===== Transpose =====

    private static void RegisterTranspose(InternalFunctionRegistry registry)
    {
        // transpose(Sequence, Semitone)
        var transposeSemitoneSig = new FunctionSignature("transpose",
            [SequenceType.Instance, SemitoneType.Instance]);
        registry.Register("transpose", transposeSemitoneSig, TransposeSemitone);

        // transpose(Sequence, Cent)
        var transposeCentSig = new FunctionSignature("transpose",
            [SequenceType.Instance, CentType.Instance]);
        registry.Register("transpose", transposeCentSig, TransposeCent);
    }

    private static Value TransposeSemitone(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int semitones = args[1].As<int>();

        var result = TransformNotes(seq, note =>
        {
            if (note.IsRest) return note;

            int midi = ToMidi(note.NoteName, note.Octave, note.Alteration) + semitones;

            if (midi < MIDI_MIN || midi > MIDI_MAX)
            {
                int clamped = Math.Clamp(midi, MIDI_MIN, MIDI_MAX);
                Console.Error.WriteLine(
                    $"Warning: transpose would put {NoteType.Format(note.NoteName, note.Octave, note.Alteration)} " +
                    $"out of range (MIDI {midi}), clamping to MIDI {clamped}");
                midi = clamped;
            }

            var (name, oct, alt) = FromMidi(midi);
            return new MusicalNoteData(name, oct, alt, note.DurationValue, isRest: false);
        });

        return Value.Sequence(result);
    }

    private static Value TransposeCent(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double cents = args[1].As<double>();
        int semitones = (int)Math.Round(cents / 100.0);

        if (Math.Abs(cents - semitones * 100.0) > 0.01)
        {
            Console.Error.WriteLine(
                $"Warning: transpose by {cents}c rounded to {semitones} semitones (not an exact multiple of 100c)");
        }

        // Delegate to semitone transpose
        return TransposeSemitone([args[0], Value.Semitone(semitones)]);
    }

    // ===== Invert =====

    private static void RegisterInvert(InternalFunctionRegistry registry)
    {
        var invertSig = new FunctionSignature("invert",
            [SequenceType.Instance]);
        registry.Register("invert", invertSig, Invert);
    }

    private static Value Invert(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();

        // Find the first non-rest note across all bars (the axis)
        int? axisMidi = null;
        foreach (var bar in seq.Bars)
        {
            foreach (var note in bar.MusicalNotes)
            {
                if (!note.IsRest)
                {
                    axisMidi = ToMidi(note.NoteName, note.Octave, note.Alteration);
                    break;
                }
            }
            if (axisMidi.HasValue) break;
        }

        // If no notes found (all rests), return a clone
        if (!axisMidi.HasValue)
            return Value.Sequence(TransformNotes(seq, n => n));

        int axis = axisMidi.Value;
        var result = TransformNotes(seq, note =>
        {
            if (note.IsRest) return note;

            int midi = ToMidi(note.NoteName, note.Octave, note.Alteration);
            int inverted = 2 * axis - midi;
            inverted = Math.Clamp(inverted, MIDI_MIN, MIDI_MAX);

            var (name, oct, alt) = FromMidi(inverted);
            return new MusicalNoteData(name, oct, alt, note.DurationValue, isRest: false);
        });

        return Value.Sequence(result);
    }

    // ===== Retrograde =====

    private static void RegisterRetrograde(InternalFunctionRegistry registry)
    {
        var retrogradeSig = new FunctionSignature("retrograde",
            [SequenceType.Instance]);
        registry.Register("retrograde", retrogradeSig, Retrograde);
    }

    private static Value Retrograde(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();

        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var reversedNotes = new List<MusicalNoteData>(bar.MusicalNotes);
            reversedNotes.Reverse();
            var newBar = new BarData(reversedNotes, bar.TimeSignature!);
            result.AddBar(newBar);
        }
        return Value.Sequence(result);
    }

    // ===== Augment / Diminish =====

    private static void RegisterAugmentDiminish(InternalFunctionRegistry registry)
    {
        var augmentSig = new FunctionSignature("augment",
            [SequenceType.Instance]);
        registry.Register("augment", augmentSig, Augment);

        var diminishSig = new FunctionSignature("diminish",
            [SequenceType.Instance]);
        registry.Register("diminish", diminishSig, Diminish);
    }

    private static Value Augment(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();

        var result = TransformNotes(seq, note =>
        {
            if (!note.DurationValue.HasValue) return note;

            int newDur = note.DurationValue.Value - 1; // toward WHOLE=0
            if (newDur < (int)NoteValueType.Value.WHOLE)
            {
                Console.Error.WriteLine("Warning: augment clamped duration at whole note");
                newDur = (int)NoteValueType.Value.WHOLE;
            }

            return new MusicalNoteData(note.NoteName, note.Octave, note.Alteration, newDur, note.IsRest);
        });

        return Value.Sequence(result);
    }

    private static Value Diminish(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();

        var result = TransformNotes(seq, note =>
        {
            if (!note.DurationValue.HasValue) return note;

            int newDur = note.DurationValue.Value + 1; // toward THIRTYSECOND=5
            if (newDur > (int)NoteValueType.Value.THIRTYSECOND)
            {
                Console.Error.WriteLine("Warning: diminish clamped duration at thirty-second note");
                newDur = (int)NoteValueType.Value.THIRTYSECOND;
            }

            return new MusicalNoteData(note.NoteName, note.Octave, note.Alteration, newDur, note.IsRest);
        });

        return Value.Sequence(result);
    }

    // ===== Octave Shift =====

    private static void RegisterOctaveShift(InternalFunctionRegistry registry)
    {
        var upSig = new FunctionSignature("up",
            [SequenceType.Instance, IntType.Instance]);
        registry.Register("up", upSig, OctaveUp);

        var downSig = new FunctionSignature("down",
            [SequenceType.Instance, IntType.Instance]);
        registry.Register("down", downSig, OctaveDown);
    }

    private static Value OctaveUp(IReadOnlyList<Value> args)
    {
        int octaves = args[1].As<int>();
        return TransposeSemitone([args[0], Value.Semitone(octaves * 12)]);
    }

    private static Value OctaveDown(IReadOnlyList<Value> args)
    {
        int octaves = args[1].As<int>();
        return TransposeSemitone([args[0], Value.Semitone(-octaves * 12)]);
    }

    // ===== Repeat =====

    private static void RegisterRepeat(InternalFunctionRegistry registry)
    {
        // repeat(Sequence, Int)
        var repeatSig = new FunctionSignature("repeat",
            [SequenceType.Instance, IntType.Instance]);
        registry.Register("repeat", repeatSig, Repeat);

        // repeat(Sequence, Int, Semitone)
        var repeatTransposeSig = new FunctionSignature("repeat",
            [SequenceType.Instance, IntType.Instance, SemitoneType.Instance]);
        registry.Register("repeat", repeatTransposeSig, RepeatTranspose);
    }

    private static Value Repeat(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int times = args[1].As<int>();

        var result = new SequenceData();
        for (int i = 0; i < times; i++)
        {
            foreach (var bar in seq.Bars)
            {
                var newBar = new BarData(new List<MusicalNoteData>(bar.MusicalNotes), bar.TimeSignature!);
                result.AddBar(newBar);
            }
        }
        return Value.Sequence(result);
    }

    private static Value RepeatTranspose(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int times = args[1].As<int>();
        int semitones = args[2].As<int>();

        var result = new SequenceData();
        for (int i = 0; i < times; i++)
        {
            int cumulativeTranspose = i * semitones;
            foreach (var bar in seq.Bars)
            {
                var newNotes = new List<MusicalNoteData>();
                foreach (var note in bar.MusicalNotes)
                {
                    if (note.IsRest)
                    {
                        newNotes.Add(note);
                        continue;
                    }

                    int midi = ToMidi(note.NoteName, note.Octave, note.Alteration) + cumulativeTranspose;
                    midi = Math.Clamp(midi, MIDI_MIN, MIDI_MAX);
                    var (name, oct, alt) = FromMidi(midi);
                    newNotes.Add(new MusicalNoteData(name, oct, alt, note.DurationValue, isRest: false));
                }
                var newBar = new BarData(newNotes, bar.TimeSignature!);
                result.AddBar(newBar);
            }
        }
        return Value.Sequence(result);
    }

    // ===== Concat =====

    private static void RegisterConcat(InternalFunctionRegistry registry)
    {
        var concatSig = new FunctionSignature("concat",
            [SequenceType.Instance, SequenceType.Instance]);
        registry.Register("concat", concatSig, ConcatSequences);
    }

    private static Value ConcatSequences(IReadOnlyList<Value> args)
    {
        var seqA = args[0].As<SequenceData>();
        var seqB = args[1].As<SequenceData>();

        var result = new SequenceData();
        foreach (var bar in seqA.Bars)
        {
            var newBar = new BarData(new List<MusicalNoteData>(bar.MusicalNotes), bar.TimeSignature!);
            result.AddBar(newBar);
        }
        foreach (var bar in seqB.Bars)
        {
            var newBar = new BarData(new List<MusicalNoteData>(bar.MusicalNotes), bar.TimeSignature!);
            result.AddBar(newBar);
        }
        return Value.Sequence(result);
    }
}
