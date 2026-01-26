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
            TokenType.Identifier when token.Text == "OscillatorState" => OscillatorStateType.Instance,
            TokenType.Identifier when token.Text == "Envelope" => EnvelopeType.Instance,
            TokenType.Identifier when token.Text == "Beat" => BeatType.Instance,
            TokenType.Identifier when token.Text == "Voice" => VoiceType.Instance,
            TokenType.Identifier when token.Text == "Track" => TrackType.Instance,
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
