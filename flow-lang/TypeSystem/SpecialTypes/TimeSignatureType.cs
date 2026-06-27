using System;
using System.Collections.Generic;

namespace FlowLang.TypeSystem.SpecialTypes;

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

    /// <summary>
    /// The capacity of one bar expressed in quarter-note units — the universal
    /// duration unit used by <see cref="MusicalNoteData.GetBeats"/>, the wall-clock
    /// (BeatsToSeconds) and tick (ticksPerQuarter) conversions, and the
    /// ValidateBarFit quarter-units truncation path.
    ///
    /// One denominator-unit beat equals (4 / Denominator) quarter notes, so a bar
    /// holds Numerator × 4 / Denominator quarters:
    ///   4/4 → 16/4 = 4 quarters; 6/8 → 24/8 = 3 quarters; 2/2 → 8/2 = 4 quarters.
    /// Bare <c>Numerator</c> (denominator-unit beats) is wrong everywhere a beat
    /// total is converted to seconds or ticks — use this instead. (sweep-0614:
    /// non-4/4 wall-clock + MIDI speed fix.)
    /// </summary>
    public double BarCapacityQuarters => Numerator * 4.0 / Denominator;

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
