using FlowLang.Lexing;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Parsing;

/// <summary>
/// Parses type annotations from tokens.
/// </summary>
public static class TypeParser
{
    /// <summary>
    /// Parses a type from the token stream starting at the given index.
    /// Returns the parsed type and the next index to continue parsing.
    /// Also returns whether this is a varargs type (e.g., "Ints" for Int...).
    /// </summary>
    public static (FlowType type, int nextIndex, bool isVarArgs) ParseType(List<Token> tokens, int index)
    {
        if (index >= tokens.Count)
            throw new ParseException("Unexpected end of input while parsing type");

        var token = tokens[index];
        bool isVarArgs = false;

        // Check for function type: (Type, Type => Type)
        if (token.Type == TokenType.LParen && LooksLikeFunctionType(tokens, index))
        {
            return ParseFunctionType(tokens, index);
        }

        // Phase 26.1 TUP-09: Tuple<<T1, T2, ...>> generic type. Empty <<>> and singleton <<T>>
        // are valid arities. Place BEFORE the Lazy<T> generic so a future Lazy<Tuple<<...>>>
        // recurses correctly (RESEARCH § Pitfall 5 — Lazy<Dict<...>> ordering).
        //
        // Dual-form delimiter handling: `<<` and `>>` may lex as either a single
        // LessLess/GreaterGreater token (the common case — when prev-emitted is in the
        // expression-start gate) OR as two adjacent LessThan/GreaterThan tokens (in nested
        // contexts like `Dict<Tuple<<K, V>>, Int>` where the lexer's gate fires differently
        // for the inner `<<`). Accept BOTH forms here so all sites parse correctly.
        if (token.Type == TokenType.Identifier && token.Text == "Tuple")
        {
            index++; // past "Tuple"

            if (index < tokens.Count && tokens[index].Type == TokenType.LessLess)
            {
                index++; // consumed single `<<` token
            }
            else
            {
                if (index >= tokens.Count || tokens[index].Type != TokenType.LessThan)
                    throw new ParseException($"Expected '<<' after Tuple at {tokens[Math.Min(index, tokens.Count - 1)].Location}");
                index++;
                if (index >= tokens.Count || tokens[index].Type != TokenType.LessThan)
                    throw new ParseException($"Expected '<<' after Tuple (second '<') at {tokens[Math.Min(index, tokens.Count - 1)].Location}");
                index++;
            }

            var elementTypes = new List<FlowType>();
            bool first = true;
            while (index < tokens.Count
                   && tokens[index].Type != TokenType.GreaterThan
                   && tokens[index].Type != TokenType.GreaterGreater)
            {
                if (!first)
                {
                    if (tokens[index].Type != TokenType.Comma)
                        throw new ParseException($"Expected ',' between Tuple element types at {tokens[index].Location}");
                    index++;
                }
                var (elemType, next, _) = ParseType(tokens, index);
                elementTypes.Add(elemType);
                index = next;
                first = false;
            }

            if (index < tokens.Count && tokens[index].Type == TokenType.GreaterGreater)
            {
                index++; // single `>>` token
            }
            else
            {
                if (index >= tokens.Count || tokens[index].Type != TokenType.GreaterThan)
                    throw new ParseException($"Expected '>>' after Tuple element types at {tokens[Math.Min(index, tokens.Count - 1)].Location}");
                index++;
                if (index >= tokens.Count || tokens[index].Type != TokenType.GreaterThan)
                    throw new ParseException($"Expected '>>' after Tuple element types (second '>') at {tokens[Math.Min(index, tokens.Count - 1)].Location}");
                index++;
            }

            return (new TupleType(elementTypes), index, isVarArgs: false);
        }

        // Phase 26.1 DICT-01: Dict<K, V> generic type. Place BEFORE the plural-form check
        // (RESEARCH § Pitfall 5 ordering). K must satisfy IsHashable() — enforced HERE
        // at parse time with the "is not hashable" allowlist message that
        // DictTypeRejectionFacts pins.
        if (token.Type == TokenType.Identifier && token.Text == "Dict")
        {
            index++; // past "Dict"
            if (index >= tokens.Count || tokens[index].Type != TokenType.LessThan)
                throw new ParseException($"Expected '<' after Dict at {tokens[Math.Min(index, tokens.Count - 1)].Location}");
            index++; // skip <

            var keyTokenLoc = tokens[Math.Min(index, tokens.Count - 1)].Location;
            var (keyType, nextK, _) = ParseType(tokens, index);
            // VoidType key is the wildcard used by std.flow proc declarations
            // for the Dict-side (each)/(map)/(filter)/(get)/(set)/etc. overloads —
            // exempted from IsHashable enforcement (Dict<Void, Void> is not user-facing
            // for storage; it's the dispatch shape).
            if (!keyType.IsHashable() && !(keyType is VoidType))
                throw new ParseException(
                    $"Dict key type '{keyType.Name}' is not hashable. Allowed: Int, Long, Float, " +
                    $"String, Symbol, Note, Chord, Tuple-of-hashables. At {keyTokenLoc}");
            index = nextK;

            if (index >= tokens.Count || tokens[index].Type != TokenType.Comma)
                throw new ParseException($"Expected ',' between Dict K and V at {tokens[Math.Min(index, tokens.Count - 1)].Location}");
            index++; // skip ,

            var (valueType, nextV, _) = ParseType(tokens, index);
            index = nextV;

            if (index >= tokens.Count || tokens[index].Type != TokenType.GreaterThan)
                throw new ParseException($"Expected '>' after Dict<K, V> at {tokens[Math.Min(index, tokens.Count - 1)].Location}");
            index++; // skip >

            return (new DictType(keyType, valueType), index, isVarArgs: false);
        }

        // Check for generic Lazy<T> type FIRST
        if (token.Type == TokenType.Identifier && token.Text == "Lazy")
        {
            index++; // Move past "Lazy"

            // Check if generic type parameter is specified
            if (index < tokens.Count && tokens[index].Type == TokenType.LessThan)
            {
                index++; // Skip <

                // Parse inner type
                var (innerType, nextIndex, _) = ParseType(tokens, index);
                index = nextIndex;

                if (index >= tokens.Count || tokens[index].Type != TokenType.GreaterThan)
                    throw new ParseException($"Expected '>' after Lazy inner type at {tokens[index].Location}");
                index++; // Skip >

                return (new LazyType(innerType), index, isVarArgs: false);
            }
            else
            {
                // No generic parameter specified - default to Lazy<Void>
                return (new LazyType(VoidType.Instance), index, isVarArgs: false);
            }
        }

        // Check for plural form (arrays) like "Ints", "Strings", "Voids"
        // This is syntactic sugar for Int[], String[], Void[], etc.
        if (token.Type == TokenType.Identifier && token.Text.EndsWith("s"))
        {
            var singularName = token.Text.Substring(0, token.Text.Length - 1);
            var baseType = TryParseSingularType(singularName);
            if (baseType != null)
            {
                index++; // Move past the type name
                return (new ArrayType(baseType), index, isVarArgs: false);
            }
        }

        FlowType parsedType = token.Type switch
        {
            TokenType.Void => VoidType.Instance,
            TokenType.Int => IntType.Instance,
            TokenType.Float => FloatType.Instance,
            TokenType.Long => LongType.Instance,
            TokenType.Double => DoubleType.Instance,
            TokenType.String => StringType.Instance,
            TokenType.Bool => BoolType.Instance,
            TokenType.Number => NumberType.Instance,
            TokenType.Buf => BufType.Instance,
            TokenType.Identifier when token.Text == "Buffer" => BufferType.Instance,
            TokenType.Identifier when token.Text == "Note" => NoteType.Instance,
            TokenType.Identifier when token.Text == "Bar" => BarType.Instance,
            TokenType.Identifier when token.Text == "Semitone" => SemitoneType.Instance,
            TokenType.Identifier when token.Text == "Cent" => CentType.Instance,
            TokenType.Identifier when token.Text == "Millisecond" => MillisecondType.Instance,
            TokenType.Identifier when token.Text == "Second" => SecondType.Instance,
            TokenType.Identifier when token.Text == "Decibel" => DecibelType.Instance,
            TokenType.Identifier when token.Text == "Hertz" => HertzType.Instance,
            TokenType.Identifier when token.Text == "OscillatorState" => OscillatorStateType.Instance,
            TokenType.Identifier when token.Text == "Envelope" => EnvelopeType.Instance,
            TokenType.Identifier when token.Text == "Beat" => BeatType.Instance,
            TokenType.Identifier when token.Text == "Voice" => VoiceType.Instance,
            TokenType.Identifier when token.Text == "Track" => TrackType.Instance,
            TokenType.Identifier when token.Text == "NoteValue" => NoteValueType.Instance,
            TokenType.Identifier when token.Text == "TimeSignature" => TimeSignatureType.Instance,
            TokenType.Identifier when token.Text == "Sequence" => SequenceType.Instance,
            TokenType.Identifier when token.Text == "MusicalNote" => MusicalNoteType.Instance,
            TokenType.Identifier when token.Text == "Chord" => ChordType.Instance,
            TokenType.Identifier when token.Text == "Symbol" => SymbolType.Instance,
            TokenType.Identifier when token.Text == "Section" => SectionType.Instance,
            TokenType.Identifier when token.Text == "Song" => SongType.Instance,
            // Phase 32 Plan 32-04: Tuning is the 15th SpecialType. Required so
            // `Tuning t = (loadScala "...")` declarations parse.
            TokenType.Identifier when token.Text == "Tuning" => TuningType.Instance,
            // Phase 33 Plan 33-05: Sfz is the 16th SpecialType. Required so
            // `Sfz v = (loadSfz #violin)` declarations parse. (Plan 33-02 shipped
            // the type itself; this entry wires it into the type-name parser.)
            TokenType.Identifier when token.Text == "Sfz" => SfzType.Instance,
            // Phase 36 Plan 36-06: MarkovModel is the 17th SpecialType. Required so
            // `MarkovModel m = (markovTrain corpus 2)` declarations parse.
            TokenType.Identifier when token.Text == "MarkovModel" => MarkovModelType.Instance,
            // Phase 36 Plan 36-07: LsystemModel is the 18th SpecialType. Required so
            // `LsystemModel m = (lsystemModel #A rules)` declarations parse.
            TokenType.Identifier when token.Text == "LsystemModel" => LsystemModelType.Instance,
            // Phase 38 Plan 38-06: OscHandle is the 19th SpecialType. Required so
            // `OscHandle h = (oscListen 7777 "/x" handler)` declarations parse + `use "@osc"` imports.
            TokenType.Identifier when token.Text == "OscHandle" => OscHandleType.Instance,
#if !FLOW_WEB
            // Phase 40 Plan 40-01: MidiDevice is the reference-identity handle for
            // an opened MIDI output port. Required so `MidiDevice dev =
            // (openMidiOutput "port")` declarations parse + `use "@midi"` imports.
            // #if !FLOW_WEB — MidiDeviceType is stripped on Web (T-40-03).
            TokenType.Identifier when token.Text == "MidiDevice" => MidiDeviceType.Instance,
            // Phase 40 Plan 40-02: ClockHandle is the reference-identity handle for
            // a MIDI clock master/slave. Required so `ClockHandle h =
            // (clockMaster dev)` declarations parse + the `clockStop` decl in
            // midi.flow type-checks. #if !FLOW_WEB — ClockHandleType is stripped
            // on Web (T-40-03).
            TokenType.Identifier when token.Text == "ClockHandle" => ClockHandleType.Instance,
            // Phase 40 Plan 40-03: JackHandle is the reference-identity handle
            // returned by (jackSync). Required so `JackHandle h = (jackSync)`
            // declarations parse + the jackSync decl in jack.flow type-checks.
            // #if !FLOW_WEB — JackHandleType is stripped on Web (T-40-03).
            TokenType.Identifier when token.Text == "JackHandle" => JackHandleType.Instance,
#endif
            TokenType.Identifier when token.Text == "Function" => FunctionType.Instance,
            _ => throw new ParseException($"Expected type name but got {token.Type} '{token.Text}' at {token.Location}")
        };

        index++; // Move past the type name

        // Check for array type []
        if (index < tokens.Count && tokens[index].Type == TokenType.LBracket)
        {
            index++; // Skip [
            if (index >= tokens.Count || tokens[index].Type != TokenType.RBracket)
                throw new ParseException($"Expected ] after [ in array type at {tokens[index].Location}");
            index++; // Skip ]
            return (new ArrayType(parsedType), index, isVarArgs: false);
        }

        return (parsedType, index, isVarArgs);
    }

    /// <summary>
    /// Checks if a token sequence starting at index looks like a function type: (Type, ... => Type).
    /// Scans for a FatArrow at parenthesis depth 1 before the matching RParen.
    /// </summary>
    public static bool LooksLikeFunctionType(List<Token> tokens, int index)
    {
        if (index >= tokens.Count || tokens[index].Type != TokenType.LParen)
            return false;

        // Phase 35 Plan 35-05 (LANG-01): `(match scrutinee | pat => body | ...)` carries
        // a `=>` at depth 1 (each arm uses FatArrow) but is NOT a function-type
        // annotation. Cheap structural disambiguation — when the very next token after
        // the LParen is the `match` keyword, refuse to claim function-type shape so
        // ParseStatement falls through to expression-statement parsing where
        // ParsePrimary's `(match` branch picks it up. Same posture as ParseStatement's
        // existing keyword sniffs (Pan/Gain/ReverbTime/VoicePool).
        if (index + 1 < tokens.Count && tokens[index + 1].Type == TokenType.Match)
            return false;

        int depth = 1;
        int pos = index + 1;
        while (pos < tokens.Count && depth > 0)
        {
            var t = tokens[pos];
            if (t.Type == TokenType.LParen) depth++;
            else if (t.Type == TokenType.RParen) depth--;
            else if (t.Type == TokenType.FatArrow && depth == 1) return true;
            pos++;
        }
        return false;
    }

    /// <summary>
    /// Parses a function type annotation: (ParamType1, ParamType2 => ReturnType)
    /// Returns FunctionType.Instance (function types are structurally compatible at runtime).
    /// </summary>
    private static (FlowType type, int nextIndex, bool isVarArgs) ParseFunctionType(List<Token> tokens, int index)
    {
        index++; // skip opening (

        // Parse parameter types until =>
        while (index < tokens.Count && tokens[index].Type != TokenType.FatArrow)
        {
            // Parse each parameter type (we validate them but store as FunctionType.Instance)
            var (_, nextIdx, _) = ParseType(tokens, index);
            index = nextIdx;

            if (index < tokens.Count && tokens[index].Type == TokenType.Comma)
                index++; // skip comma between parameter types
        }

        if (index >= tokens.Count || tokens[index].Type != TokenType.FatArrow)
            throw new ParseException($"Expected '=>' in function type at {tokens[Math.Min(index, tokens.Count - 1)].Location}");
        index++; // skip =>

        // Parse return type
        var (_, retNextIdx, _) = ParseType(tokens, index);
        index = retNextIdx;

        if (index >= tokens.Count || tokens[index].Type != TokenType.RParen)
            throw new ParseException($"Expected ')' after function type at {tokens[Math.Min(index, tokens.Count - 1)].Location}");
        index++; // skip closing )

        return (FunctionType.Instance, index, false);
    }

    /// <summary>
    /// Tries to parse a singular type name (for varargs plural form).
    /// Returns null if the name doesn't match a known type.
    /// </summary>
    private static FlowType? TryParseSingularType(string name)
    {
        return name switch
        {
            "Void" => VoidType.Instance,
            "Int" => IntType.Instance,
            "Float" => FloatType.Instance,
            "Long" => LongType.Instance,
            "Double" => DoubleType.Instance,
            "String" => StringType.Instance,
            "Bool" => BoolType.Instance,
            "Number" => NumberType.Instance,
            "Buf" => BufType.Instance,
            "Buffer" => BufferType.Instance,
            "Note" => NoteType.Instance,
            "Bar" => BarType.Instance,
            "Semitone" => SemitoneType.Instance,
            "Cent" => CentType.Instance,
            "Millisecond" => MillisecondType.Instance,
            "Second" => SecondType.Instance,
            "Decibel" => DecibelType.Instance,
            "Hertz" => HertzType.Instance,
            "OscillatorState" => OscillatorStateType.Instance,
            "Envelope" => EnvelopeType.Instance,
            "Beat" => BeatType.Instance,
            "Voice" => VoiceType.Instance,
            "Track" => TrackType.Instance,
            "NoteValue" => NoteValueType.Instance,
            "TimeSignature" => TimeSignatureType.Instance,
            "Sequence" => SequenceType.Instance,
            "MusicalNote" => MusicalNoteType.Instance,
            "Chord" => ChordType.Instance,
            "Symbol" => SymbolType.Instance,
            "Section" => SectionType.Instance,
            "Song" => SongType.Instance,
            "Tuning" => TuningType.Instance, // Phase 32 Plan 32-04
            "Sfz" => SfzType.Instance,       // Phase 33 Plan 33-05
            "MarkovModel" => MarkovModelType.Instance, // Phase 36 Plan 36-06
            "LsystemModel" => LsystemModelType.Instance, // Phase 36 Plan 36-07
            "OscHandle" => OscHandleType.Instance, // Phase 38 Plan 38-06
#if !FLOW_WEB
            "MidiDevice" => MidiDeviceType.Instance, // Phase 40 Plan 40-01 (#if !FLOW_WEB)
            "ClockHandle" => ClockHandleType.Instance, // Phase 40 Plan 40-02 (#if !FLOW_WEB)
            "JackHandle" => JackHandleType.Instance, // Phase 40 Plan 40-03 (#if !FLOW_WEB)
#endif
            "Function" => FunctionType.Instance,
            _ => null
        };
    }
}

/// <summary>
/// Special buf type for audio buffers (placeholder - will be properly implemented in flow-std).
/// </summary>
public sealed class BufType : FlowType
{
    private BufType() { }
    public static BufType Instance { get; } = new();
    public override string Name => "buf";
    public override int GetSpecificity() => 135;
}

public class ParseException : Exception
{
    public ParseException(string message) : base(message) { }
}
