using System;
using System.Collections.Generic;

namespace FlowLang.TypeSystem.SpecialTypes;

public sealed class NoteValueType : FlowType
{
    private NoteValueType() { }

    public static NoteValueType Instance { get; } = new();

    public override string Name => "NoteValue";

    public override int GetSpecificity() => 132;

    public override bool IsCompatibleWith(FlowType other)
    {
        // NoteValue is backed by int, allow Int values to be used as NoteValue
        return other is NoteValueType || other is PrimitiveTypes.IntType || base.IsCompatibleWith(other);
    }

    public enum Value
    {
        WHOLE = 0,
        HALF = 1,
        QUARTER = 2,
        EIGHTH = 3,
        SIXTEENTH = 4,
        THIRTYSECOND = 5,
        SIXTYFOURTH = 6,
        ONETWENTYEIGHTH = 7
    }

    /// <summary>
    /// 0615 bare-notevalues — predefined GLOBAL NoteValue constant names. Maps the
    /// canonical single-letter duration short-forms (the SAME table
    /// <see cref="FlowLang.Runtime.NoteStreamCompiler"/> uses for note-stream
    /// duration suffixes) to their <see cref="Value"/> enum. Resolved by
    /// <c>ExpressionEvaluator.EvaluateVariable</c> ONLY when no variable or
    /// function with that name is in scope, so a composer's <c>Int e = 5</c>
    /// (or a proc param named <c>q</c>) shadows the constant naturally — the
    /// frame-chain lookup hits first. This is the least-breaking path to the
    /// documented <c>(quantize seq e 1.0 0.0)</c> call form: e/q/h/w/s (and the
    /// finer t/x/y) are NOT reserved keywords. Dotted variants (<c>q.</c>) are
    /// intentionally absent — a dot is a note-stream duration modifier, not a
    /// distinct NoteValue enum member, and would not lex as a bare identifier.
    /// </summary>
    private static readonly Dictionary<string, Value> PredefinedConstants = new(StringComparer.Ordinal)
    {
        { "w", Value.WHOLE },
        { "h", Value.HALF },
        { "q", Value.QUARTER },
        { "e", Value.EIGHTH },
        { "s", Value.SIXTEENTH },
        { "t", Value.THIRTYSECOND },
        { "x", Value.SIXTYFOURTH },
        { "y", Value.ONETWENTYEIGHTH }
    };

    /// <summary>
    /// 0615 bare-notevalues — resolves a bare identifier to a predefined NoteValue
    /// constant. Returns <c>true</c> + the enum value for the canonical duration
    /// short-forms (w/h/q/e/s/t/x/y); <c>false</c> for anything else. Single source
    /// of truth for the bare-notevalue feature; mirrors NoteStreamCompiler's
    /// duration-suffix table so a bare <c>e</c> and a note-stream <c>C4e</c> agree.
    /// </summary>
    public static bool TryGetPredefinedConstant(string name, out Value value)
        => PredefinedConstants.TryGetValue(name, out value);

    public static Value Parse(string str)
    {
        switch (str.ToLowerInvariant().Trim())
        {
            case "whole":
            case "1":
                return Value.WHOLE;
            case "half":
            case "2":
                return Value.HALF;
            case "quarter":
            case "4":
                return Value.QUARTER;
            case "eighth":
            case "8":
                return Value.EIGHTH;
            case "sixteenth":
            case "16":
                return Value.SIXTEENTH;
            case "thirtysecond":
            case "32":
                return Value.THIRTYSECOND;
            case "sixtyfourth":
            case "64":
                return Value.SIXTYFOURTH;
            case "onetwentyeighth":
            case "128":
                return Value.ONETWENTYEIGHTH;
            default:
                throw new ArgumentException($"Invalid note value: {str}");
        }
    }

    public static double ToFraction(Value noteValue)
    {
        switch (noteValue)
        {
            case Value.WHOLE:
                return 1.0;
            case Value.HALF:
                return 0.5;
            case Value.QUARTER:
                return 0.25;
            case Value.EIGHTH:
                return 0.125;
            case Value.SIXTEENTH:
                return 0.0625;
            case Value.THIRTYSECOND:
                return 0.03125;
            case Value.SIXTYFOURTH:
                return 0.015625;
            case Value.ONETWENTYEIGHTH:
                return 0.0078125;
            default:
                throw new ArgumentException($"Invalid note value: {noteValue}");
        }
    }

    public static string Format(Value noteValue)
    {
        switch (noteValue)
        {
            case Value.WHOLE:
                return "whole";
            case Value.HALF:
                return "half";
            case Value.QUARTER:
                return "quarter";
            case Value.EIGHTH:
                return "eighth";
            case Value.SIXTEENTH:
                return "sixteenth";
            case Value.THIRTYSECOND:
                return "thirtysecond";
            case Value.SIXTYFOURTH:
                return "sixtyfourth";
            case Value.ONETWENTYEIGHTH:
                return "onetwentyeighth";
            default:
                return noteValue.ToString().ToLowerInvariant();
        }
    }

    public override string ToString()
    {
        return "NoteValue";
    }
}
