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
    public static Value Hertz(double value) => new(value, HertzType.Instance);
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

    /// <summary>
    /// Phase 32 Plan 32-04 — wraps a <see cref="StandardLibrary.Audio.Tuning.ResolvedTuning"/>
    /// reference in a Flow <see cref="Value"/> typed as <see cref="TuningType.Instance"/>.
    /// Identity follows reference equality per CONTEXT D-* / Claude's Discretion: two
    /// <c>(loadScala "x.scl")</c> calls produce distinct Values even with identical
    /// content (Phase 32 doesn't cache per SPEC out-of-scope).
    /// </summary>
    public static Value Tuning(StandardLibrary.Audio.Tuning.ResolvedTuning resolved)
        => new(resolved, TuningType.Instance);

    /// <summary>
    /// Phase 33 Plan 33-02 — wraps a <see cref="StandardLibrary.Audio.Sfz.SfzData"/>
    /// reference in a Flow <see cref="Value"/> typed as <see cref="SfzType.Instance"/>.
    /// Identity follows reference equality per CONTEXT § "Claude's Discretion": two
    /// <c>(loadSfz #violin)</c> calls produce distinct Values even with identical
    /// resolved paths (Phase 33 doesn't cache at the value layer; mirrors Phase 32's
    /// <see cref="Value.Tuning"/> contract).
    /// </summary>
    public static Value Sfz(StandardLibrary.Audio.Sfz.SfzData data)
        => new(data, SfzType.Instance);

    /// <summary>
    /// Phase 36 Plan 36-06 (GEN-01, D-36-06) — wraps a
    /// <see cref="MarkovModelData"/> reference in a Flow <see cref="Value"/>
    /// typed as <see cref="MarkovModelType.Instance"/>. Reference identity per
    /// CLAUDE.md Music Types Quick Reference (Pitfall 6 in
    /// <c>36-PATTERNS.md</c>): two <c>(markovTrain corpus order)</c> calls
    /// produce distinct Values even with identical training input. Composers
    /// who need structural compare use the dedicated <c>(markovEqual a b)</c>
    /// builtin. Mirrors the Phase 32 <see cref="Value.Tuning"/> + Phase 33
    /// <see cref="Value.Sfz"/> precedent.
    /// </summary>
    public static Value MarkovModel(MarkovModelData model)
        => new(model, MarkovModelType.Instance);

    /// <summary>
    /// Phase 36 Plan 36-07 (GEN-02, D-36-06 + D-36-08) — wraps an
    /// <see cref="LsystemModelData"/> reference in a Flow <see cref="Value"/>
    /// typed as <see cref="LsystemModelType.Instance"/>. Reference identity per
    /// CLAUDE.md Music Types Quick Reference (Pitfall 6 in
    /// <c>36-PATTERNS.md</c>): two <c>(lsystemModel axiom rules)</c> calls
    /// produce distinct Values even with identical axiom + rules input.
    /// Composers who need structural compare use the dedicated
    /// <c>(lsystemEqual a b)</c> builtin. Mirrors the Phase 32
    /// <see cref="Value.Tuning"/> + Phase 33 <see cref="Value.Sfz"/> + Plan
    /// 36-06 <see cref="Value.MarkovModel"/> precedent.
    /// </summary>
    public static Value LsystemModel(LsystemModelData model)
        => new(model, LsystemModelType.Instance);

    public static Value Function(FunctionOverload overload) => new(overload, TypeSystem.PrimitiveTypes.FunctionType.Instance);

    /// <summary>
    /// Symbol factory (Phase 26.1 SYM-01) — interns the symbol via the per-context
    /// <see cref="ExecutionContext.SymbolInternTable"/>. Two calls with the same <paramref name="name"/>
    /// against the same <paramref name="ctx"/> return the SAME Value instance (reference-equal),
    /// which is the SYM-01 contract: pointer-equality for <c>#foo</c> literals.
    /// </summary>
    public static Value Symbol(string name, ExecutionContext ctx)
    {
        if (ctx.SymbolInternTable.TryGetValue(name, out var existing)) return existing;
        var v = new Value(name, SymbolType.Instance);
        ctx.SymbolInternTable[name] = v;
        return v;
    }

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

    /// <summary>
    /// Tuple factory (Phase 26.1 TUP-09). Storage is the same <see cref="IReadOnlyList{Value}"/>
    /// shape as arrays so <see cref="ExpressionEvaluator"/>'s <c>EvaluateArrayIndex</c> can dispatch
    /// on operand type without a separate AST node (see RESEARCH § Q4 — reuse ArrayIndexExpression).
    /// Per-position <see cref="FlowType"/> annotations live on the constructed <see cref="TupleType"/>;
    /// <c>elementTypes.Count</c> defines arity (empty list → empty tuple <c>&lt;&lt;&gt;&gt;</c>).
    /// </summary>
    public static Value Tuple(IReadOnlyList<Value> components, IReadOnlyList<FlowType> elementTypes)
    {
        return new Value(components, new TupleType(elementTypes));
    }

    /// <summary>
    /// Dict factory (Phase 26.1 DICT-02). Wraps a <see cref="DictData"/> with the underlying
    /// <see cref="DictType"/> drawn from the data's recorded type. Insertion-order preserved
    /// via <see cref="System.Collections.Generic.OrderedDictionary{TKey,TValue}"/> in DictData.
    /// </summary>
    public static Value Dict(DictData data) => new(data, data.Type);

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

        // Phase 26: Float-typed values are double-backed (Value.Float stores a double).
        // Without this fast-path, the `Data is double doubleVal` branch below treats
        // Float values as Doubles and never produces a real Double widening output.
        // Tests assert that `(add Float Double)` widens to Double, which requires this.
        if (Type is FloatType && Data is double floatBackedDouble)
        {
            if (targetType is DoubleType) return Double(floatBackedDouble);
            if (targetType is NumberType) return Number(new BigInteger(floatBackedDouble));
            if (targetType is IntType) return Int((int)floatBackedDouble);   // Lossy
            if (targetType is LongType) return Long((long)floatBackedDouble); // Lossy
        }

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
            // Phase 26.2 — RESEARCH Pitfall 1 root-cause fix.
            // Music types (Decibel/Beat/Cent/Ms/Sec/Hertz) are double-backed but
            // FlowType.CanConvertTo defaults to IsCompatibleWith. With CentType /
            // DecibelType / BeatType / (and Wave-1) Ms/Sec/Hertz IsCompatibleWith(Double)
            // returning true, the function-call coercion path at
            // ExpressionEvaluator.cs:249 fires ConvertTo(DoubleType) on a music-typed
            // double-backed Value. Without this arm, the call falls through to
            // line 252's InvalidCastException ("Cannot convert Flow type 'Decibel' with
            // underlying CLR type 'Double' to Flow target type 'Double'") — the exact
            // exception that fails (gain src -12dB) when only the bare-Double overload exists.
            // This is defence-in-depth; the dedicated music-typed overload (when present
            // via audio.flow forward-decl) wins resolution at score 1000 (exact match)
            // and never invokes ConvertTo, but EVERY user-proc with Double params
            // benefits from this arm.
            if (targetType is DoubleType) return Double(doubleVal);

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

        // Phase 26.1 DICT-01: Dict<Void, Void> can convert to any Dict<K, V>
        // (empty dicts produced by (dict) with no args; mirrors Void[] above).
        // Re-key the underlying DictData with the target's KeyType comparer so
        // future (set) calls hash by the user-facing K type rather than VoidType.
        if (Type is DictType sourceDict && targetType is DictType targetDict
            && sourceDict.KeyType is TypeSystem.PrimitiveTypes.VoidType
            && sourceDict.ValueType is TypeSystem.PrimitiveTypes.VoidType
            && Data is DictData sourceData
            && sourceData.Entries.Count == 0)
        {
            return Dict(DictData.Empty(targetDict));
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
        // Phase 26.1 SYM-01: print Symbols as `#name` (must precede the generic string branch
        // since Symbol's underlying CLR Data is a string).
        if (Type is SymbolType && Data is string symName) return $"#{symName}";
        if (Data is string str) return $"\"{str}\"";
        if (Data is bool b) return b ? "true" : "false";
        // Phase 26.1 TUP-09: Tuples print `<<a, b, c>>` matching their literal source form.
        // MUST precede the generic IReadOnlyList<Value> branch since tuple storage is the
        // same shape as arrays (see Value.Tuple factory comment).
        if (Type is TupleType && Data is IReadOnlyList<Value> tup)
            return $"<<{string.Join(", ", tup.Select(v => v.ToString()))}>>";
        // Phase 26.1 DICT-02: Dicts print `{k: v, ...}` per CONTEXT § Specifics block 6.
        if (Type is DictType && Data is DictData dd)
            return "{" + string.Join(", ", dd.Entries.Select(kv => $"{kv.Key}: {kv.Value}")) + "}";
        if (Data is IReadOnlyList<Value> arr)
            return $"[{string.Join(", ", arr.Select(v => v.ToString()))}]";
        if (Data is Thunk thunk)
            return thunk.IsEvaluated ? $"<lazy: {thunk.Force()}>" : "<lazy: unevaluated>";
        if (Data is double d) return d.ToString("G10");
        return Data.ToString() ?? "null";
    }
}
