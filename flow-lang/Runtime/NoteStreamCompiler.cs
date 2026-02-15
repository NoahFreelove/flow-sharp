using FlowLang.Ast.Expressions;
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
    public SequenceData Compile(NoteStreamExpression noteStream, MusicalContext context)
    {
        var sequence = new SequenceData();
        var timeSig = context.TimeSignature ?? new TimeSignatureData(4, 4);

        foreach (var bar in noteStream.Bars)
        {
            var barData = CompileBar(bar, timeSig, context);
            sequence.AddBar(barData);
        }

        return sequence;
    }

    /// <summary>
    /// Compiles a single bar of note stream elements into a BarData.
    /// </summary>
    private BarData CompileBar(NoteStreamBar bar, TimeSignatureData timeSig, MusicalContext context)
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
                    musicalNotes.Add(CompileNoteElement(note, autoFitDuration));
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
            }
        }

        return new BarData(musicalNotes, timeSig);
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
                _ => null
            };

            bool isDotted = elem switch
            {
                NoteElement n => n.IsDotted,
                RestElement r => r.IsDotted,
                ChordElement c => c.IsDotted,
                NamedChordElement nc => nc.IsDotted,
                RomanNumeralElement rn => rn.IsDotted,
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
    private MusicalNoteData CompileNoteElement(NoteElement note, NoteValueType.Value? autoFitDuration)
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

        return new MusicalNoteData(noteName, octave, alteration, durationValue, isRest: false);
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

        return new MusicalNoteData(' ', 0, 0, durationValue, isRest: true);
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
            notes.Add(new MusicalNoteData(name, octave, alteration, durationValue, isRest: false));
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
            notes.Add(new MusicalNoteData(' ', 0, 0, durationValue, isRest: true));
            return notes;
        }

        foreach (var noteName in chordData.NoteNames)
        {
            var (name, octave, alteration) = NoteType.Parse(noteName);
            notes.Add(new MusicalNoteData(name, octave, alteration, durationValue, isRest: false));
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
            notes.Add(new MusicalNoteData(' ', 0, 0, durationValue, isRest: true));
            return notes;
        }

        var chordData = ScaleDatabase.ResolveRomanNumeral(romanNumeral.Numeral, context.Key);
        if (chordData == null)
        {
            notes.Add(new MusicalNoteData(' ', 0, 0, durationValue, isRest: true));
            return notes;
        }

        foreach (var noteName in chordData.NoteNames)
        {
            var (name, octave, alteration) = NoteType.Parse(noteName);
            notes.Add(new MusicalNoteData(name, octave, alteration, durationValue, isRest: false));
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
}
