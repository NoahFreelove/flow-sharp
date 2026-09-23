using FlowLang.Runtime;

namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Implicit conversions into and between music types (formerly inside
/// <see cref="Value.ConvertTo"/>), registered with <see cref="ValueConversions"/>.
/// </summary>
internal static class MusicValueConversions
{
    public static Value? Convert(Value value, FlowType target)
    {
        switch (target)
        {
            // Int-backed data: NoteValue enum values and semitone counts (e.g. 5st).
            case NoteValueType when value.Data is int noteValue:
                return MusicValue.NoteValue(noteValue);
            case SemitoneType when value.Type is NoteType && value.Data is string noteText:
                try
                {
                    var parsed = NoteType.Parse(noteText);
                    return MusicValue.Semitone(NoteType.ToMidiNote(parsed.note, parsed.octave, parsed.alteration));
                }
                catch
                {
                    throw new InvalidCastException($"Cannot convert Note '{noteText}' to Semitone");
                }
            case SemitoneType when value.Data is int semitones:
                return MusicValue.Semitone(semitones);
            case NoteType when value.Type is SemitoneType && value.Data is int midi:
                var note = NoteType.FromMidiNote(midi);
                return MusicValue.Note(NoteType.Format(note.note, note.octave, note.alteration));
            // String-backed data (strings, symbols, notes) casts to Note as text.
            case NoteType when value.Data is string text:
                return MusicValue.Note(text);
            // Durations.
            case SecondType when value.Type is MillisecondType && value.Data is double ms:
                return MusicValue.Second(ms / 1000.0);
            case MillisecondType when value.Type is SecondType && value.Data is double seconds:
                return MusicValue.Millisecond(seconds * 1000.0);
            default:
                return null;
        }
    }

    /// <summary>Semitones, cents, durations and decibels compare as plain numbers.</summary>
    public static (double Double, System.Numerics.BigInteger? Whole)? NumericView(Value value) => value.Type switch
    {
        SemitoneType => (value.As<int>(), new System.Numerics.BigInteger(value.As<int>())),
        CentType or MillisecondType or SecondType or DecibelType => (value.As<double>(), null),
        _ => null,
    };

    /// <summary>Durations compare in seconds across ms and s: (equals 1000ms 1s).</summary>
    public static (double A, double B)? CommonScale(Value a, Value b)
    {
        static bool IsTime(Value v) => v.Type is MillisecondType or SecondType;
        static double Seconds(Value v) => v.Type is MillisecondType ? System.Convert.ToDouble(v.Data) / 1000.0 : System.Convert.ToDouble(v.Data);
        return IsTime(a) && IsTime(b) ? (Seconds(a), Seconds(b)) : null;
    }
}
