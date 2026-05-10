using System;
using System.Collections.Generic;

namespace FlowLang.TypeSystem.SpecialTypes
{
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
            THIRTYSECOND = 5
        }

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
                default:
                    return noteValue.ToString().ToLowerInvariant();
            }
        }

        public override string ToString()
        {
            return "NoteValue";
        }
    }
}
