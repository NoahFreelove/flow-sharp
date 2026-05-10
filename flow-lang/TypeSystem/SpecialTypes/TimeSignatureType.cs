using System;
using System.Collections.Generic;

namespace FlowLang.TypeSystem.SpecialTypes
{
    public class TimeSignatureData
    {
        public int Numerator { get; }
        public int Denominator { get; }

        public TimeSignatureData(int numerator, int denominator)
        {
            if (!IsValidTimeSignature(numerator, denominator))
            {
                throw new ArgumentException($"Invalid time signature: {numerator}/{denominator}");
            }

            Numerator = numerator;
            Denominator = denominator;
        }

        private static bool IsValidTimeSignature(int numerator, int denominator)
        {
            // Numerator must be positive
            if (numerator <= 0)
                return false;

            // Denominator must be a power of 2
            if (denominator <= 0 || (denominator & (denominator - 1)) != 0)
                return false;

            // Allow any valid combination (removed whitelist - supports 7/8, 11/16, 13/8, etc.)
            return true;
        }

        public override string ToString()
        {
            return $"{Numerator}/{Denominator}";
        }

        public override bool Equals(object obj)
        {
            if (obj is TimeSignatureData other)
            {
                return Numerator == other.Numerator && Denominator == other.Denominator;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Numerator, Denominator);
        }
    }

    public sealed class TimeSignatureType : FlowType
    {
        private TimeSignatureType() { }

        public static TimeSignatureType Instance { get; } = new();

        public override string Name => "TimeSignature";

        public override int GetSpecificity() => 133;

        public static TimeSignatureData Parse(string str)
        {
            var parts = str.Split('/');
            if (parts.Length != 2)
            {
                throw new ArgumentException($"Invalid time signature format: {str}. Expected format: N/D (e.g., 4/4)");
            }

            if (!int.TryParse(parts[0].Trim(), out int numerator))
            {
                throw new ArgumentException($"Invalid numerator in time signature: {parts[0]}");
            }

            if (!int.TryParse(parts[1].Trim(), out int denominator))
            {
                throw new ArgumentException($"Invalid denominator in time signature: {parts[1]}");
            }

            return new TimeSignatureData(numerator, denominator);
        }

        public override string ToString()
        {
            return "TimeSignature";
        }
    }
}
