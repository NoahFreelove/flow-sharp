using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using System.Numerics;

namespace FlowLang.Runtime;

/// <summary>
/// Wraps a CLR value with Flow type information.
/// </summary>
public class Value
{
    public object? Data { get; }
    public FlowType Type { get; }

    public Value(object? data, FlowType type)
    {
        Data = data;
        Type = type ?? throw new ArgumentNullException(nameof(type));
    }

    // Factory methods for common types
    public static Value Void() => new(null, VoidType.Instance);
    public static Value Int(int value) => new(value, IntType.Instance);
    public static Value Float(double value) => new(value, FloatType.Instance);
    public static Value Long(long value) => new(value, LongType.Instance);
    public static Value Double(double value) => new(value, DoubleType.Instance);
    public static Value String(string value) => new(value, StringType.Instance);
    public static Value Bool(bool value) => new(value, BoolType.Instance);
    public static Value Number(BigInteger value) => new(value, NumberType.Instance);
    public static Value Buffer(object? value = null) => new(value, BufferType.Instance);
    public static Value Note(string value) => new(value, NoteType.Instance);
    public static Value Bar(BarData value) => new(value, BarType.Instance);
    public static Value Semitone(int value) => new(value, SemitoneType.Instance);
    public static Value Cent(double value) => new(value, CentType.Instance);
    public static Value Millisecond(double value) => new(value, MillisecondType.Instance);
    public static Value Second(double value) => new(value, SecondType.Instance);
    public static Value Decibel(double value) => new(value, DecibelType.Instance);
    public static Value OscillatorState(StandardLibrary.Audio.OscillatorState value) => new(value, OscillatorStateType.Instance);
    public static Value Envelope(StandardLibrary.Audio.Envelope value) => new(value, EnvelopeType.Instance);
    public static Value Beat(double value) => new(value, BeatType.Instance);
    public static Value Voice(StandardLibrary.Audio.Voice value) => new(value, VoiceType.Instance);
    public static Value Track(StandardLibrary.Audio.Track value) => new(value, TrackType.Instance);
    public static Value NoteValue(int enumValue) => new(enumValue, NoteValueType.Instance);
    public static Value TimeSignature(TimeSignatureData timeSig) => new(timeSig, TimeSignatureType.Instance);
    public static Value MusicalNote(MusicalNoteData note) => new(note, MusicalNoteType.Instance);
    public static Value Sequence(SequenceData sequence) => new(sequence, SequenceType.Instance);
    public static Value Chord(ChordData chord) => new(chord, ChordType.Instance);
    public static Value Section(SectionData section) => new(section, SectionType.Instance);
    public static Value Song(SongData song) => new(song, SongType.Instance);
    public static Value Function(FunctionOverload overload) => new(overload, TypeSystem.PrimitiveTypes.FunctionType.Instance);

    /// <summary>
    /// Automatically infers the Flow type from a CLR object and creates a Value.
    /// </summary>
    public static Value From(object? obj) => obj switch
    {
        null => Void(),
        int i => Int(i),
        long l => Long(l),
        float f => Float(f),
        double d => Double(d),
        bool b => Bool(b),
        string s => String(s),
        BigInteger bi => Number(bi),
        Thunk t => throw new InvalidOperationException("Use Value.Lazy() to create lazy values"),
        IReadOnlyList<Value> arr => throw new InvalidOperationException("Use Value.Array() to create array values"),
        _ => throw new InvalidOperationException($"Cannot infer Flow type from CLR type {obj.GetType()}")
    };

    public static Value Array(IReadOnlyList<Value> elements, FlowType elementType)
    {
        return new Value(elements, new ArrayType(elementType));
    }

    public static Value Lazy(Thunk thunk, FlowType innerType)
    {
        return new Value(thunk, new LazyType(innerType));
    }

    /// <summary>
    /// Converts this value to another type if possible.
    /// </summary>
    public Value ConvertTo(FlowType targetType)
    {
        if (Type.Equals(targetType))
            return this;

        // Numeric conversions
        if (Data is int intVal)
        {
            if (targetType is LongType) return Long(intVal);
            if (targetType is FloatType) return Float(intVal);
            if (targetType is DoubleType) return Double(intVal);
            if (targetType is NumberType) return Number(new BigInteger(intVal));
            if (targetType is NoteValueType) return NoteValue(intVal);
            if (targetType is BoolType) return Bool(intVal != 0);
            if (targetType is SemitoneType) return Semitone(intVal); // e.g. 5st
        }

        if (Data is long longVal)
        {
            if (targetType is IntType) return Int((int)longVal); // Lossy
            if (targetType is FloatType) return Float(longVal);
            if (targetType is DoubleType) return Double(longVal);
            if (targetType is NumberType) return Number(new BigInteger(longVal));
            if (targetType is BoolType) return Bool(longVal != 0);
        }

        if (Data is double doubleVal)
        {
            if (targetType is IntType) return Int((int)doubleVal); // Lossy
            if (targetType is LongType) return Long((long)doubleVal); // Lossy
            if (targetType is FloatType) return Float((float)doubleVal); // Lossy
            if (targetType is NumberType) return Number(new BigInteger(doubleVal));
        }

        if (Data is float floatVal)
        {
            if (targetType is IntType) return Int((int)floatVal); // Lossy
            if (targetType is LongType) return Long((long)floatVal); // Lossy
            if (targetType is DoubleType) return Double(floatVal);
            if (targetType is NumberType) return Number(new BigInteger(floatVal));
        }

        // Boxed BigInteger
        if (Data is BigInteger bigVal)
        {
            if (targetType is IntType) return Int((int)bigVal); // Lossy
            if (targetType is LongType) return Long((long)bigVal); // Lossy
            if (targetType is FloatType) return Float((float)bigVal); // Lossy
            if (targetType is DoubleType) return Double((double)bigVal); // Lossy
        }

        if (Data is bool boolVal)
        {
            if (targetType is IntType) return Int(boolVal ? 1 : 0);
            if (targetType is LongType) return Long(boolVal ? 1L : 0L);
            if (targetType is DoubleType) return Double(boolVal ? 1.0 : 0.0);
        }

        if (Data is string str)
        {
            // Simple casts
            if (targetType is NoteType) return Note(str);
            if (targetType is StringType) return String(str);
        }

        if (Type is NoteType && targetType is SemitoneType && Data is string noteStr)
        {
            // Try convert Note to Semitone
            try
            {
                var parsed = NoteType.Parse(noteStr);
                int midi = NoteType.ToMidiNote(parsed.note, parsed.octave, parsed.alteration);
                return Semitone(midi);
            }
            catch
            {
                throw new InvalidCastException($"Cannot convert Note '{noteStr}' to Semitone");
            }
        }

        if (Type is SemitoneType && targetType is NoteType && Data is int semiVal)
        {
            // Convert Semitone to Note
            var parsed = NoteType.FromMidiNote(semiVal);
            return Note(NoteType.Format(parsed.note, parsed.octave, parsed.alteration));
        }

        // Time conversions
        if (Type is MillisecondType && targetType is SecondType && Data is double msVal)
        {
            return Second(msVal / 1000.0);
        }

        if (Type is SecondType && targetType is MillisecondType && Data is double secVal)
        {
            return Millisecond(secVal * 1000.0);
        }

        // Array conversions - Void[] can convert to any array type (empty arrays)
        if (Type is ArrayType sourceArray && targetType is ArrayType targetArray)
        {
            if (sourceArray.ElementType is TypeSystem.PrimitiveTypes.VoidType)
            {
                // Convert Void[] to T[] by returning a new array with the target element type
                var arrayData = Data as IReadOnlyList<Value> ?? throw new InvalidCastException($"Expected array data, got {Data?.GetType()}");
                return Array(arrayData, targetArray.ElementType);
            }
        }

        // Explicit Type Name error
        throw new InvalidCastException($"Cannot convert Flow type '{Type.Name}' with underlying CLR type '{(Data != null ? Data.GetType().Name : "null")}' to Flow target type '{targetType.Name}'");
    }

    /// <summary>
    /// Gets the CLR value as a specific type safely.
    /// </summary>
    public T As<T>()
    {
        if (Data is T t)
        {
            return t;
        }

        // Add detailed InvalidCastException log per Bug 6, C2
        string actualType = Data?.GetType().Name ?? "null";
        throw new InvalidCastException($"Type cast failure. Expected underlying CLR type '{typeof(T).Name}' from Flow value of type '{Type.Name}', but found '{actualType}'.");
    }

    /// <summary>
    /// Gets the CLR value as a specific type, or default if null or wrong type.
    /// </summary>
    public T? AsOrDefault<T>() => Data is T t ? t : default;

    public override string ToString()
    {
        if (Data is null) return "void";
        if (Data is string str) return $"\"{str}\"";
        if (Data is bool b) return b ? "true" : "false";
        if (Data is IReadOnlyList<Value> arr)
            return $"[{string.Join(", ", arr.Select(v => v.ToString()))}]";
        if (Data is Thunk thunk)
            return thunk.IsEvaluated ? $"<lazy: {thunk.Force()}>" : "<lazy: unevaluated>";
        if (Data is double d) return d.ToString("G10");
        return Data.ToString() ?? "null";
    }
}
