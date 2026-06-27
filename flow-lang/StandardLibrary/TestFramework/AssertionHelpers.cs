using System;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.TestFramework;

/// <summary>
/// Phase 35 Plan 35-04 TEST-01 — shared C# implementation helpers for the
/// five assertion-primitive builtins. Each method throws
/// <see cref="AssertionException"/> on a failing predicate, returning
/// silently on success. The (test ...) framework's <see cref="TestRunner"/>
/// catches the throw to record a FAIL outcome.
///
/// <para>
/// These helpers are also reachable from xUnit facts (see
/// <c>flow-lang.Tests/Phase35/AssertWithinDbTests.cs</c>) so the
/// C#-layer semantics can be pinned independently of the Flow surface.
/// </para>
/// </summary>
public static class AssertionHelpers
{
    /// <summary>
    /// (assert cond) — throws AssertionException if cond is false.
    /// </summary>
    public static void AssertOrThrow(bool condition, string? customMessage = null)
    {
        if (!condition)
            throw new AssertionException(customMessage ?? "assert failed: condition evaluated to false.");
    }

    /// <summary>
    /// (assertEq a b) — delegates to <see cref="Utils.LooseEquals"/>
    /// (matches the existing <c>(equals a b)</c> builtin shape at
    /// <c>BuiltInFunctions.cs:371-374</c>). Throws AssertionException with
    /// a diff-shaped message when unequal.
    /// </summary>
    public static void AssertEqOrThrow(Value a, Value b)
    {
        if (!Utils.LooseEquals(a, b))
        {
            throw new AssertionException(
                $"assertEq failed: {DescribeForDiagnostic(a)} != {DescribeForDiagnostic(b)}");
        }
    }

    /// <summary>
    /// (assertNotesMatch seqA seqB) — structural equality over
    /// SequenceData. Compares bar count, each bar's TimeSignature, each
    /// bar's MusicalNotes (NoteName / Octave / Alteration / DurationValue /
    /// IsRest / IsDotted / IsTied / CentOffset / Articulation / Velocity).
    /// Per-voice sub-bars (ParallelVoices) are recursively compared. Throws
    /// AssertionException on the FIRST structural mismatch with a precise
    /// path-into-the-sequence pointer.
    /// </summary>
    public static void AssertNotesMatchOrThrow(SequenceData a, SequenceData b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));

        if (a.Bars.Count != b.Bars.Count)
            throw new AssertionException(
                $"assertNotesMatch failed: bar count mismatch ({a.Bars.Count} vs {b.Bars.Count}).");

        for (int i = 0; i < a.Bars.Count; i++)
        {
            CompareBarsOrThrow(a.Bars[i], b.Bars[i], $"bar[{i}]");
        }
    }

    /// <summary>
    /// (assertBytesEqual buf1 buf2) — compares PCM sample data byte-by-byte
    /// (well, sample-by-sample on the float[] backing). Asserts metadata
    /// matches (SampleRate / Channels / Frames); throws on any sample
    /// mismatch with the offending frame + channel index.
    /// </summary>
    public static void AssertBytesEqualOrThrow(AudioBuffer a, AudioBuffer b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));

        if (a.SampleRate != b.SampleRate)
            throw new AssertionException(
                $"assertBytesEqual failed: SampleRate mismatch ({a.SampleRate} vs {b.SampleRate}).");
        if (a.Channels != b.Channels)
            throw new AssertionException(
                $"assertBytesEqual failed: channel count mismatch ({a.Channels} vs {b.Channels}).");
        if (a.Frames != b.Frames)
            throw new AssertionException(
                $"assertBytesEqual failed: frame count mismatch ({a.Frames} vs {b.Frames}).");

        // Compare the float[] backing directly — same shape, same length.
        for (int i = 0; i < a.Data.Length; i++)
        {
            // Use bitwise equality (BitConverter.SingleToInt32Bits) so NaN ==
            // NaN holds when both encodings match — useful for round-trip
            // tests against a WAV that contains NaN samples (rare but legal).
            if (BitConverter.SingleToInt32Bits(a.Data[i]) !=
                BitConverter.SingleToInt32Bits(b.Data[i]))
            {
                int frame = i / a.Channels;
                int channel = i % a.Channels;
                throw new AssertionException(
                    $"assertBytesEqual failed: sample mismatch at frame {frame} channel {channel} " +
                    $"({a.Data[i]} vs {b.Data[i]}).");
            }
        }
    }

    /// <summary>
    /// (assertWithinDb buf1 buf2 toleranceDb) — wraps
    /// <see cref="RmsComparator.MaxWindowDeviationDb"/> at the SPEC-8 locked
    /// 100 ms window. Throws AssertionException when any window's absolute
    /// dB deviation exceeds <paramref name="toleranceDb"/>, with the first
    /// failing window's (start ms, end ms, dbA, dbB, delta) tuple in the
    /// message.
    /// </summary>
    public static void AssertWithinDbOrThrow(
        AudioBuffer a,
        AudioBuffer b,
        double toleranceDb)
    {
        var firstFailure = RmsComparator.FirstWindowExceedingTolerance(
            a, b, toleranceDb, windowMs: RmsComparator.DefaultWindowMs);
        if (firstFailure is null) return;

        var (win, startMs, endMs, dbA, dbB, delta) = firstFailure.Value;
        throw new AssertionException(
            $"assertWithinDb failed: RMS deviation in window {win} ({startMs}ms-{endMs}ms): " +
            $"expected {dbB:F2} dB, got {dbA:F2} dB " +
            $"(delta {delta:F2} dB exceeds tolerance {toleranceDb} dB).");
    }

    private static void CompareBarsOrThrow(
        TypeSystem.SpecialTypes.BarData a,
        TypeSystem.SpecialTypes.BarData b,
        string path)
    {
        if (a.Mode != b.Mode)
            throw new AssertionException(
                $"assertNotesMatch failed at {path}: Mode mismatch ({a.Mode} vs {b.Mode}).");

        if (!TimeSignatureEquals(a.TimeSignature, b.TimeSignature))
            throw new AssertionException(
                $"assertNotesMatch failed at {path}: TimeSignature mismatch " +
                $"({a.TimeSignature} vs {b.TimeSignature}).");

        // ParallelVoices recursion (Phase 28 voice-block polyphony).
        var aVoices = a.ParallelVoices;
        var bVoices = b.ParallelVoices;
        if ((aVoices == null) != (bVoices == null))
            throw new AssertionException(
                $"assertNotesMatch failed at {path}: ParallelVoices presence mismatch " +
                $"({aVoices?.Count.ToString() ?? "null"} vs {bVoices?.Count.ToString() ?? "null"}).");
        if (aVoices != null && bVoices != null)
        {
            if (aVoices.Count != bVoices.Count)
                throw new AssertionException(
                    $"assertNotesMatch failed at {path}: voice count mismatch " +
                    $"({aVoices.Count} vs {bVoices.Count}).");
            for (int v = 0; v < aVoices.Count; v++)
                CompareBarsOrThrow(aVoices[v], bVoices[v], $"{path}.voice[{v}]");
        }

        if (a.MusicalNotes.Count != b.MusicalNotes.Count)
            throw new AssertionException(
                $"assertNotesMatch failed at {path}: note count mismatch " +
                $"({a.MusicalNotes.Count} vs {b.MusicalNotes.Count}).");

        for (int n = 0; n < a.MusicalNotes.Count; n++)
        {
            CompareNotesOrThrow(a.MusicalNotes[n], b.MusicalNotes[n], $"{path}.note[{n}]");
        }
    }

    private static void CompareNotesOrThrow(
        TypeSystem.SpecialTypes.MusicalNoteData a,
        TypeSystem.SpecialTypes.MusicalNoteData b,
        string path)
    {
        if (a.NoteName != b.NoteName)
            Throw(path, "NoteName", a.NoteName, b.NoteName);
        if (a.Octave != b.Octave)
            Throw(path, "Octave", a.Octave, b.Octave);
        if (a.Alteration != b.Alteration)
            Throw(path, "Alteration", a.Alteration, b.Alteration);
        if (a.DurationValue != b.DurationValue)
            Throw(path, "DurationValue", a.DurationValue, b.DurationValue);
        if (a.IsRest != b.IsRest)
            Throw(path, "IsRest", a.IsRest, b.IsRest);
        if (a.IsDotted != b.IsDotted)
            Throw(path, "IsDotted", a.IsDotted, b.IsDotted);
        if (a.IsTied != b.IsTied)
            Throw(path, "IsTied", a.IsTied, b.IsTied);
        if (!NullableDoubleEquals(a.CentOffset, b.CentOffset))
            Throw(path, "CentOffset", a.CentOffset, b.CentOffset);
        if (a.Articulation != b.Articulation)
            Throw(path, "Articulation", a.Articulation, b.Articulation);
        if (Math.Abs(a.Velocity - b.Velocity) > 1e-9)
            Throw(path, "Velocity", a.Velocity, b.Velocity);
    }

    private static void Throw(string path, string field, object? actual, object? expected)
    {
        throw new AssertionException(
            $"assertNotesMatch failed at {path}: {field} mismatch ({actual} vs {expected}).");
    }

    private static bool NullableDoubleEquals(double? a, double? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return Math.Abs(a.Value - b.Value) < 1e-9;
    }

    private static bool TimeSignatureEquals(TimeSignatureData? a, TimeSignatureData? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Numerator == b.Numerator && a.Denominator == b.Denominator;
    }

    private static string DescribeForDiagnostic(Value v)
    {
        if (v.Data is null) return $"<{v.Type}>";
        return $"{v.Data}:{v.Type}";
    }
}
