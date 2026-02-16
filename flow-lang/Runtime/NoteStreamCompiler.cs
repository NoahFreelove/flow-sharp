using FlowLang.Ast.Expressions;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Runtime;

/// <summary>
/// Compiles a NoteStreamExpression into a SequenceData using the active MusicalContext.
/// Handles auto-fit duration calculation, rest insertion, and bar validation.
/// </summary>
public class NoteStreamCompiler
{
    /// <summary>
    /// Maps duration suffix characters to NoteValue enum values.
    /// w=whole, h=half, q=quarter, e=eighth, s=sixteenth, t=32nd
    /// </summary>
    private static readonly Dictionary<string, NoteValueType.Value> DurationSuffixMap = new()
    {
        { "w", NoteValueType.Value.WHOLE },
        { "h", NoteValueType.Value.HALF },
        { "q", NoteValueType.Value.QUARTER },
        { "e", NoteValueType.Value.EIGHTH },
        { "s", NoteValueType.Value.SIXTEENTH },
        { "t", NoteValueType.Value.THIRTYSECOND }
    };

    /// <summary>
    /// Compiles a NoteStreamExpression into a SequenceData using the given musical context.
    /// </summary>
    public SequenceData Compile(NoteStreamExpression noteStream, MusicalContext context, ExecutionContext? executionContext = null)
    {
        var sequence = new SequenceData();
        var timeSig = context.TimeSignature ?? new TimeSignatureData(4, 4);

        foreach (var bar in noteStream.Bars)
        {
            var barData = CompileBar(bar, timeSig, context, executionContext);
            sequence.AddBar(barData);
        }

        return sequence;
    }

    /// <summary>
    /// Compiles a single bar of note stream elements into a BarData.
    /// </summary>
    private BarData CompileBar(NoteStreamBar bar, TimeSignatureData timeSig, MusicalContext context, ExecutionContext? executionContext)
    {
        var musicalNotes = new List<MusicalNoteData>();

        if (bar.Elements.Count == 0)
        {
            // Empty bar: create a whole-bar rest
            var restNote = new MusicalNoteData(' ', 0, 0, (int)NoteValueType.Value.WHOLE, isRest: true);
            musicalNotes.Add(restNote);
            return new BarData(musicalNotes, timeSig);
        }

        // Determine auto-fit duration for elements without explicit durations
        var autoFitDuration = CalculateAutoFitDuration(bar.Elements, timeSig);

        foreach (var element in bar.Elements)
        {
            switch (element)
            {
                case NoteElement note:
                    musicalNotes.Add(CompileNoteElement(note, autoFitDuration, context));
                    break;

                case RestElement rest:
                    musicalNotes.Add(CompileRestElement(rest, autoFitDuration));
                    break;

                case ChordElement chord:
                    // Expand chord to individual notes (all with same duration, played simultaneously)
                    foreach (var chordNote in CompileChordElement(chord, autoFitDuration))
                    {
                        musicalNotes.Add(chordNote);
                    }
                    break;

                case NamedChordElement namedChord:
                    foreach (var chordNote in CompileNamedChordElement(namedChord, autoFitDuration))
                    {
                        musicalNotes.Add(chordNote);
                    }
                    break;

                case RomanNumeralElement romanNumeral:
                    foreach (var chordNote in CompileRomanNumeralElement(romanNumeral, autoFitDuration, context))
                    {
                        musicalNotes.Add(chordNote);
                    }
                    break;

                case RandomChoiceElement choice:
                    musicalNotes.Add(CompileRandomChoiceElement(choice, autoFitDuration));
                    break;

                case VariableReferenceElement varRef:
                    musicalNotes.Add(CompileVariableReferenceElement(varRef, autoFitDuration, executionContext));
                    break;

                case GhostNoteElement ghost:
                {
                    var (name, octave, alteration) = NoteType.Parse(ghost.NoteName);
                    int? dv = ResolveDuration(ghost.DurationSuffix, autoFitDuration);
                    musicalNotes.Add(new MusicalNoteData(name, octave, alteration, dv,
                        isRest: false, velocity: 0.15));
                    break;
                }

                case GraceNoteElement grace:
                {
                    var (name, octave, alteration) = NoteType.Parse(grace.NoteName);
                    musicalNotes.Add(new MusicalNoteData(name, octave, alteration,
                        (int)NoteValueType.Value.THIRTYSECOND, isRest: false, velocity: 0.5));
                    break;
                }
            }
        }

        // Post-process: if notes have varying velocities, smooth-interpolate between them
        // This handles the common case of: | pp C4 cresc D4 E4 ff F4 |
        // where D4 and E4 should get interpolated velocities
        InterpolateVelocities(musicalNotes);

        return new BarData(musicalNotes, timeSig);
    }

    /// <summary>
    /// Interpolates velocities for notes that don't have explicit velocity markings.
    /// Finds notes with explicit (different) velocities and linearly interpolates between them.
    /// </summary>
    private static void InterpolateVelocities(List<MusicalNoteData> notes)
    {
        if (notes.Count < 3) return;

        // Find unique velocities among non-rest notes
        var nonRestVelocities = notes.Where(n => !n.IsRest).Select(n => n.Velocity).Distinct().ToList();
        if (nonRestVelocities.Count < 2) return; // All same velocity, nothing to interpolate

        // Simple linear interpolation from first non-rest to last non-rest
        int firstIdx = -1, lastIdx = -1;
        for (int i = 0; i < notes.Count; i++)
        {
            if (!notes[i].IsRest)
            {
                if (firstIdx == -1) firstIdx = i;
                lastIdx = i;
            }
        }

        if (firstIdx == lastIdx) return;

        double startVel = notes[firstIdx].Velocity;
        double endVel = notes[lastIdx].Velocity;
        if (Math.Abs(startVel - endVel) < 0.01) return; // Effectively same

        // Count non-rest notes for interpolation
        int nonRestCount = 0;
        for (int i = firstIdx; i <= lastIdx; i++)
            if (!notes[i].IsRest) nonRestCount++;

        if (nonRestCount < 3) return; // Need at least 3 to interpolate

        int noteIdx = 0;
        for (int i = firstIdx; i <= lastIdx; i++)
        {
            if (notes[i].IsRest) continue;

            // First and last keep their explicit velocities
            if (noteIdx > 0 && noteIdx < nonRestCount - 1)
            {
                double t = (double)noteIdx / (nonRestCount - 1);
                double vel = Math.Clamp(startVel + t * (endVel - startVel), 0.0, 1.0);
                var n = notes[i];
                notes[i] = new MusicalNoteData(n.NoteName, n.Octave, n.Alteration,
                    n.DurationValue, n.IsRest, n.CentOffset, n.IsTied, vel, n.Articulation, n.IsDotted);
            }
            noteIdx++;
        }
    }

    /// <summary>
    /// Calculates the auto-fit NoteValue for elements without explicit durations.
    /// Divides the bar's total beats evenly among auto-fit elements.
    /// </summary>
    private NoteValueType.Value? CalculateAutoFitDuration(
        IReadOnlyList<NoteStreamElement> elements, TimeSignatureData timeSig)
    {
        // Count elements with and without explicit durations
        int autoFitCount = 0;
        double explicitBeats = 0;

        foreach (var elem in elements)
        {
            string? durSuffix = elem switch
            {
                NoteElement n => n.DurationSuffix,
                RestElement r => r.DurationSuffix,
                ChordElement c => c.DurationSuffix,
                NamedChordElement nc => nc.DurationSuffix,
                RomanNumeralElement rn => rn.DurationSuffix,
                RandomChoiceElement rc => rc.DurationSuffix,
                VariableReferenceElement vr => vr.DurationSuffix,
                GhostNoteElement g => g.DurationSuffix,
                GraceNoteElement _ => null,
                _ => null
            };

            bool isDotted = elem switch
            {
                NoteElement n => n.IsDotted,
                RestElement r => r.IsDotted,
                ChordElement c => c.IsDotted,
                NamedChordElement nc => nc.IsDotted,
                RomanNumeralElement rn => rn.IsDotted,
                RandomChoiceElement rc => rc.IsDotted,
                VariableReferenceElement vr => vr.IsDotted,
                GhostNoteElement g => g.IsDotted,
                GraceNoteElement _ => false,
                _ => false
            };

            if (durSuffix != null && DurationSuffixMap.TryGetValue(durSuffix, out var noteVal))
            {
                double fraction = NoteValueType.ToFraction(noteVal);
                if (isDotted) fraction *= 1.5;
                explicitBeats += fraction * timeSig.Denominator;
            }
            else
            {
                autoFitCount++;
            }
        }

        if (autoFitCount == 0)
            return null; // All elements have explicit durations

        // Calculate remaining beats for auto-fit elements
        double totalBeats = timeSig.Numerator;
        double remainingBeats = totalBeats - explicitBeats;
        if (remainingBeats <= 0)
            remainingBeats = totalBeats; // If overflow, use full bar

        double beatsPerNote = remainingBeats / autoFitCount;

        // Map to closest NoteValue
        return FindClosestNoteValue(beatsPerNote, timeSig.Denominator);
    }

    /// <summary>
    /// Finds the closest NoteValue enum for a given number of beats.
    /// </summary>
    private NoteValueType.Value FindClosestNoteValue(double beats, int timeSigDenominator)
    {
        // Convert beats to fraction of a whole note
        double fraction = beats / timeSigDenominator;

        // Find the closest NoteValue
        var values = new[]
        {
            (NoteValueType.Value.WHOLE, 1.0),
            (NoteValueType.Value.HALF, 0.5),
            (NoteValueType.Value.QUARTER, 0.25),
            (NoteValueType.Value.EIGHTH, 0.125),
            (NoteValueType.Value.SIXTEENTH, 0.0625),
            (NoteValueType.Value.THIRTYSECOND, 0.03125)
        };

        NoteValueType.Value closest = NoteValueType.Value.QUARTER;
        double closestDiff = double.MaxValue;

        foreach (var (noteVal, noteFraction) in values)
        {
            double diff = Math.Abs(noteFraction - fraction);
            if (diff < closestDiff)
            {
                closestDiff = diff;
                closest = noteVal;
            }
        }

        return closest;
    }

    /// <summary>
    /// Compiles a NoteElement into a MusicalNoteData.
    /// </summary>
    private MusicalNoteData CompileNoteElement(NoteElement note, NoteValueType.Value? autoFitDuration, MusicalContext context)
    {
        var (noteName, octave, alteration) = NoteType.Parse(note.NoteName);
        int? durationValue;

        if (note.DurationSuffix != null && DurationSuffixMap.TryGetValue(note.DurationSuffix, out var noteVal))
        {
            durationValue = (int)noteVal;
        }
        else if (autoFitDuration != null)
        {
            durationValue = (int)autoFitDuration.Value;
        }
        else
        {
            durationValue = (int)NoteValueType.Value.QUARTER; // Default to quarter note
        }

        // Determine velocity: note-level override > context velocity > default mf
        double velocity = note.Velocity ?? context.Velocity ?? 0.63;

        // Apply accent articulations as velocity boost
        var articulation = note.ArticulationMark ?? Articulation.Normal;
        if (articulation == Articulation.Accent)
            velocity = Math.Min(velocity + 0.2, 1.0);
        else if (articulation == Articulation.Marcato)
            velocity = Math.Min(velocity + 0.3, 1.0);
        else if (articulation == Articulation.Sforzando)
            velocity = 0.95;

        return new MusicalNoteData(noteName, octave, alteration, durationValue, isRest: false,
            centOffset: note.CentOffset, isTied: note.IsTied,
            velocity: velocity, articulation: articulation,
            isDotted: note.IsDotted);
    }

    /// <summary>
    /// Compiles a RestElement into a MusicalNoteData with IsRest=true.
    /// </summary>
    private MusicalNoteData CompileRestElement(RestElement rest, NoteValueType.Value? autoFitDuration)
    {
        int? durationValue;

        if (rest.DurationSuffix != null && DurationSuffixMap.TryGetValue(rest.DurationSuffix, out var noteVal))
        {
            durationValue = (int)noteVal;
        }
        else if (autoFitDuration != null)
        {
            durationValue = (int)autoFitDuration.Value;
        }
        else
        {
            durationValue = (int)NoteValueType.Value.QUARTER;
        }

        return new MusicalNoteData(' ', 0, 0, durationValue, isRest: true, isDotted: rest.IsDotted);
    }

    /// <summary>
    /// Compiles a ChordElement into multiple MusicalNoteData (one per note in the chord).
    /// </summary>
    private List<MusicalNoteData> CompileChordElement(ChordElement chord, NoteValueType.Value? autoFitDuration)
    {
        var notes = new List<MusicalNoteData>();
        int? durationValue;

        if (chord.DurationSuffix != null && DurationSuffixMap.TryGetValue(chord.DurationSuffix, out var noteVal))
        {
            durationValue = (int)noteVal;
        }
        else if (autoFitDuration != null)
        {
            durationValue = (int)autoFitDuration.Value;
        }
        else
        {
            durationValue = (int)NoteValueType.Value.QUARTER;
        }

        foreach (var noteName in chord.Notes)
        {
            var (name, octave, alteration) = NoteType.Parse(noteName);
            notes.Add(new MusicalNoteData(name, octave, alteration, durationValue, isRest: false, isDotted: chord.IsDotted));
        }

        return notes;
    }

    /// <summary>
    /// Compiles a NamedChordElement (e.g., Cmaj7) into multiple MusicalNoteData.
    /// </summary>
    private List<MusicalNoteData> CompileNamedChordElement(NamedChordElement namedChord, NoteValueType.Value? autoFitDuration)
    {
        var notes = new List<MusicalNoteData>();
        int? durationValue = ResolveDuration(namedChord.DurationSuffix, autoFitDuration);

        if (!ChordParser.TryParse(namedChord.ChordSymbol, out var chordData) || chordData == null)
        {
            // Invalid chord — insert a rest as fallback
            notes.Add(new MusicalNoteData(' ', 0, 0, durationValue, isRest: true, isDotted: namedChord.IsDotted));
            return notes;
        }

        foreach (var noteName in chordData.NoteNames)
        {
            var (name, octave, alteration) = NoteType.Parse(noteName);
            notes.Add(new MusicalNoteData(name, octave, alteration, durationValue, isRest: false, isDotted: namedChord.IsDotted));
        }

        return notes;
    }

    /// <summary>
    /// Compiles a RomanNumeralElement into multiple MusicalNoteData using the active key context.
    /// </summary>
    private List<MusicalNoteData> CompileRomanNumeralElement(
        RomanNumeralElement romanNumeral, NoteValueType.Value? autoFitDuration, MusicalContext context)
    {
        var notes = new List<MusicalNoteData>();
        int? durationValue = ResolveDuration(romanNumeral.DurationSuffix, autoFitDuration);

        if (context.Key == null)
        {
            // No key context — insert a rest as fallback
            notes.Add(new MusicalNoteData(' ', 0, 0, durationValue, isRest: true, isDotted: romanNumeral.IsDotted));
            return notes;
        }

        var chordData = ScaleDatabase.ResolveRomanNumeral(romanNumeral.Numeral, context.Key);
        if (chordData == null)
        {
            notes.Add(new MusicalNoteData(' ', 0, 0, durationValue, isRest: true, isDotted: romanNumeral.IsDotted));
            return notes;
        }

        foreach (var noteName in chordData.NoteNames)
        {
            var (name, octave, alteration) = NoteType.Parse(noteName);
            notes.Add(new MusicalNoteData(name, octave, alteration, durationValue, isRest: false, isDotted: romanNumeral.IsDotted));
        }

        return notes;
    }

    /// <summary>
    /// Resolves a duration suffix to a NoteValue, falling back to autoFitDuration or quarter note.
    /// </summary>
    private int? ResolveDuration(string? durationSuffix, NoteValueType.Value? autoFitDuration)
    {
        if (durationSuffix != null && DurationSuffixMap.TryGetValue(durationSuffix, out var noteVal))
            return (int)noteVal;
        if (autoFitDuration != null)
            return (int)autoFitDuration.Value;
        return (int)NoteValueType.Value.QUARTER;
    }

    /// <summary>
    /// Compiles a RandomChoiceElement by selecting one note randomly from the choice set.
    /// </summary>
    private MusicalNoteData CompileRandomChoiceElement(RandomChoiceElement choice, NoteValueType.Value? autoFitDuration)
    {
        int? durationValue = ResolveDuration(choice.DurationSuffix, autoFitDuration);

        // Select a note from the choices
        string selectedNote;
        bool hasWeights = choice.Choices.Any(c => c.Weight.HasValue);

        if (hasWeights)
        {
            // Weighted random selection
            int totalWeight = choice.Choices.Sum(c => c.Weight ?? 0);
            if (totalWeight <= 0)
            {
                Console.Error.WriteLine("Warning: random choice weights sum to 0, using uniform selection");
                hasWeights = false;
            }
            else
            {
                if (totalWeight != 100)
                {
                    Console.Error.WriteLine($"Warning: random choice weights sum to {totalWeight}, not 100. Normalizing.");
                }
                float rand = Utils.FRand(choice.IsSeeded) * totalWeight;
                float cumulative = 0;
                selectedNote = choice.Choices[^1].Note; // Default to last
                foreach (var (note, weight) in choice.Choices)
                {
                    cumulative += weight ?? 0;
                    if (rand < cumulative)
                    {
                        selectedNote = note;
                        break;
                    }
                }
                return CreateNoteFromChoice(selectedNote, durationValue, choice.IsDotted);
            }
        }

        // Uniform random selection
        int index = (int)(Utils.FRand(choice.IsSeeded) * choice.Choices.Count);
        index = Math.Clamp(index, 0, choice.Choices.Count - 1);
        selectedNote = choice.Choices[index].Note;
        return CreateNoteFromChoice(selectedNote, durationValue, choice.IsDotted);
    }

    /// <summary>
    /// Compiles a VariableReferenceElement by resolving the variable from the execution context.
    /// Supports Note (string) and MusicalNote (MusicalNoteData) variable types.
    /// Falls back to a rest on error (undefined variable, wrong type).
    /// </summary>
    private MusicalNoteData CompileVariableReferenceElement(
        VariableReferenceElement varRef, NoteValueType.Value? autoFitDuration, ExecutionContext? executionContext)
    {
        int? durationValue = ResolveDuration(varRef.DurationSuffix, autoFitDuration);

        if (executionContext == null)
        {
            Console.Error.WriteLine($"Warning: cannot resolve variable '{varRef.VariableName}' in note stream (no execution context)");
            return new MusicalNoteData(' ', 0, 0, durationValue, isRest: true);
        }

        Value value;
        try
        {
            value = executionContext.GetVariable(varRef.VariableName);
        }
        catch (InvalidOperationException)
        {
            Console.Error.WriteLine($"Warning: undefined variable '{varRef.VariableName}' in note stream, inserting rest");
            return new MusicalNoteData(' ', 0, 0, durationValue, isRest: true);
        }

        // Handle Note type (string like "C4", "D#5")
        if (value.Type is NoteType && value.Data is string noteStr)
        {
            try
            {
                var (noteName, octave, alteration) = NoteType.Parse(noteStr);
                return new MusicalNoteData(noteName, octave, alteration, durationValue, isRest: false,
                    centOffset: varRef.CentOffset, isTied: varRef.IsTied, isDotted: varRef.IsDotted);
            }
            catch
            {
                Console.Error.WriteLine($"Warning: variable '{varRef.VariableName}' has invalid note value '{noteStr}', inserting rest");
                return new MusicalNoteData(' ', 0, 0, durationValue, isRest: true);
            }
        }

        // Handle MusicalNote type (MusicalNoteData)
        if (value.Data is MusicalNoteData musicalNote)
        {
            // Use stream-level duration/modifiers if provided, otherwise use the MusicalNote's own values
            int? finalDuration = varRef.DurationSuffix != null
                ? ResolveDuration(varRef.DurationSuffix, autoFitDuration)
                : musicalNote.DurationValue ?? durationValue;
            bool finalDotted = varRef.DurationSuffix != null ? varRef.IsDotted : musicalNote.IsDotted;
            bool finalTied = varRef.IsTied || musicalNote.IsTied;
            double? finalCentOffset = varRef.CentOffset ?? musicalNote.CentOffset;

            return new MusicalNoteData(musicalNote.NoteName, musicalNote.Octave, musicalNote.Alteration,
                finalDuration, isRest: musicalNote.IsRest,
                centOffset: finalCentOffset, isTied: finalTied, isDotted: finalDotted,
                velocity: musicalNote.Velocity, articulation: musicalNote.Articulation);
        }

        Console.Error.WriteLine($"Warning: variable '{varRef.VariableName}' is type {value.Type.Name}, expected Note or MusicalNote, inserting rest");
        return new MusicalNoteData(' ', 0, 0, durationValue, isRest: true);
    }

    private static MusicalNoteData CreateNoteFromChoice(string noteStr, int? durationValue, bool isDotted = false)
    {
        if (noteStr == "_")
            return new MusicalNoteData(' ', 0, 0, durationValue, isRest: true, isDotted: isDotted);

        var (name, octave, alteration) = NoteType.Parse(noteStr);
        return new MusicalNoteData(name, octave, alteration, durationValue, isRest: false, isDotted: isDotted);
    }
}
