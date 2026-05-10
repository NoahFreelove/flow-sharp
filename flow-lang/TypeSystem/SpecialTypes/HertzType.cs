using System;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Represents a frequency value in Hertz (cycles per second).
/// Phase 26.2 ERG-04 — first-class music type for audio frequency parameters.
/// Stored as a single canonical double (kHz multiplied by 1000 at lex time per
/// CONTEXT D-11/D-12 — no unit-discriminator at runtime).
/// </summary>
public sealed class HertzType : FlowType
{
    private HertzType() { }

    public static HertzType Instance { get; } = new();

    public override string Name => "Hertz";

    /// <summary>
    /// 144 — one above Cent (143). Unique among existing music types
    /// (Cent=143, Beat=139, Decibel=128, Semitone=125, Millisecond=122, Second=123).
    /// Maintains the established convention of one specificity slot per type.
    /// </summary>
    public override int GetSpecificity() => 144;

    /// <summary>
    /// Phase 26.2 ERG-04 — Hertz is compatible with Double and Float so
    /// (lowpass buf 800.0) and (lowpass buf 800Hz) both reach the same
    /// parameter slots. Mirrors CentType.cs:24-27 / DecibelType.cs:23-27.
    /// </summary>
    public override bool IsCompatibleWith(FlowType target)
    {
        return target is DoubleType or FloatType || base.IsCompatibleWith(target);
    }

    /// <summary>
    /// Parse a hertz value from string. Accepts both "Hz" and "kHz" suffixes:
    ///   "800Hz"   -> 800.0
    ///   "1.5kHz"  -> 1500.0  (canonical Hz: kHz × 1000)
    ///   "440Hz"   -> 440.0
    ///
    /// kHz is checked BEFORE Hz because EndsWith("Hz") is also true for "kHz" strings.
    /// </summary>
    public static double Parse(string hzStr)
    {
        if (string.IsNullOrEmpty(hzStr))
            throw new ArgumentException("Hertz string cannot be empty");

        // Check kHz BEFORE Hz (kHz is the longer suffix)
        if (hzStr.EndsWith("kHz"))
        {
            string numberPart = hzStr[..^3]; // Remove "kHz" (3 chars)
            if (!double.TryParse(numberPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double kHzValue))
                throw new ArgumentException($"Invalid kHz value: {numberPart}");
            return kHzValue * 1000.0;
        }

        if (hzStr.EndsWith("Hz"))
        {
            string numberPart = hzStr[..^2]; // Remove "Hz" (2 chars)
            if (!double.TryParse(numberPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double hzValue))
                throw new ArgumentException($"Invalid Hz value: {numberPart}");
            return hzValue;
        }

        throw new ArgumentException($"Invalid hertz format: {hzStr}. Must end with 'Hz' or 'kHz'.");
    }
}
