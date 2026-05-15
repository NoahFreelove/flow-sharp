using System;
using System.Collections.Generic;
using System.Globalization;

namespace FlowLang.StandardLibrary.Audio.Tuning;

/// <summary>
/// Hand-rolled <c>.kbm</c> format parser per RESEARCH §.kbm Format Reference
/// (assembled from the modartt.com forum + Sevish blog corroborating sources;
/// the canonical Huygens-Fokker spec page for .kbm is currently 404).
///
/// Public surface (two static methods):
///   - <see cref="Parse(string, string)"/> — reads a real .kbm file.
///   - <see cref="Default(ParsedScala)"/> — synthesizes the linear-mapping
///     KBM per CONTEXT D-05/D-07 (auto-adopts the tuning's PeriodCents).
///
/// The internal model is ALWAYS "has KBM" (D-05); no nullable Kbm field on
/// the resolved Tuning. <see cref="ScalaKbmParser.Default"/> produces the
/// synthetic linear-mapping KBM when no real .kbm file is supplied.
///
/// DoS guard per threat T-32-PARSE-02: hard cap of 10000 mapping entries.
/// Determinism guard per Pitfall 8: all numeric parsing routes through
/// <see cref="CultureInfo.InvariantCulture"/>.
/// </summary>
public sealed class ScalaKbmParser
{
    private const int MaxMappingEntries = 10000;

    /// <summary>
    /// Default linear-mapping KBM for a parsed .scl. Per D-05/D-07 the period
    /// auto-adopts the tuning's PeriodCents — the period-mismatch edge case is
    /// dissolved structurally.
    /// </summary>
    public static ScalaKbm Default(ParsedScala scl)
        => new ScalaKbm(
            size: 0,
            firstMidi: 0,
            lastMidi: 127,
            middleNote: 60,
            referenceNote: 69,
            referenceHz: 440.0,
            formalOctave: 0,
            mapping: Array.Empty<int?>(),
            period: scl.PeriodCents);

    /// <summary>
    /// Parse a .kbm file. Throws <see cref="ScalaKbmParseException"/> on any
    /// malformed input. NOTE: the Period field on the returned ScalaKbm is
    /// 0.0 here because Parse alone has no access to the paired .scl; callers
    /// that load a real .kbm should construct the final ScalaKbm by combining
    /// these 7 header fields with the .scl's PeriodCents — that wiring lives
    /// in Plan 32-03 (ResolvedTuning builder).
    /// </summary>
    public static ScalaKbm Parse(string content, string filePath)
    {
        var lines = content.Split('\n');
        int cursor = 0;

        // Read the 7 header fields in order. Each must be on its own
        // non-comment-non-blank line.
        int size = ReadInt(lines, ref cursor, filePath, "size of map (non-negative integer)",
            validate: v => v >= 0);

        // T-32-PARSE-02 DoS guard.
        if (size > MaxMappingEntries)
        {
            throw new ScalaKbmParseException(filePath, cursor, 1,
                "size of map <= 10000", size.ToString(CultureInfo.InvariantCulture));
        }

        int firstMidi = ReadInt(lines, ref cursor, filePath, "first MIDI note (0..127)",
            validate: v => v >= 0 && v <= 127);
        int lastMidi = ReadInt(lines, ref cursor, filePath, "last MIDI note (0..127)",
            validate: v => v >= 0 && v <= 127);
        if (firstMidi > lastMidi)
        {
            throw new ScalaKbmParseException(filePath, cursor, 1,
                "last MIDI >= first MIDI",
                $"first={firstMidi}, last={lastMidi}");
        }

        int middleNote = ReadInt(lines, ref cursor, filePath, "middle note (0..127)",
            validate: v => v >= 0 && v <= 127);
        int referenceNote = ReadInt(lines, ref cursor, filePath, "reference note (0..127)",
            validate: v => v >= 0 && v <= 127);
        double referenceHz = ReadDouble(lines, ref cursor, filePath,
            "reference frequency (positive Hz)",
            validate: v => v > 0.0);

        // Formal octave: Phase 32 rejects non-zero per RESEARCH A10 (defer to v1.5).
        int formalOctave = ReadInt(lines, ref cursor, filePath,
            "formal octave 0 (non-zero deferred to v1.5)",
            validate: v => v == 0);

        // Read EXACTLY `size` mapping entries. Each entry is a non-negative
        // integer OR the literal lowercase `x` per RESEARCH §unmapped encoding.
        var mapping = new int?[size];
        for (int i = 0; i < size; i++)
        {
            (int line, string token) = NextField(lines, ref cursor, filePath, "mapping entry");
            // Unmapped: literal lowercase `x`. NOT `X`, NOT `?`.
            if (token == "x")
            {
                mapping[i] = null;
                continue;
            }
            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int degree)
                || degree < 0)
            {
                throw new ScalaKbmParseException(filePath, line, 1,
                    "mapping entry (non-negative integer or 'x')", token);
            }
            mapping[i] = degree;
        }

        // Period field is 0.0 here — see XML-doc above. Callers building a
        // ResolvedTuning with a real .kbm should overlay the .scl's PeriodCents.
        return new ScalaKbm(
            size, firstMidi, lastMidi, middleNote, referenceNote,
            referenceHz, formalOctave, mapping, period: 0.0);
    }

    // ─── private helpers ─────────────────────────────────────────────────────

    /// <summary>Read the next non-comment-non-blank line as an integer field.</summary>
    private static int ReadInt(
        string[] lines, ref int cursor, string filePath,
        string expectedDesc, Func<int, bool> validate)
    {
        (int line, string token) = NextField(lines, ref cursor, filePath, expectedDesc);
        if (!int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v)
            || !validate(v))
        {
            throw new ScalaKbmParseException(filePath, line, 1, expectedDesc, token);
        }
        return v;
    }

    /// <summary>Read the next non-comment-non-blank line as a Double field.</summary>
    private static double ReadDouble(
        string[] lines, ref int cursor, string filePath,
        string expectedDesc, Func<double, bool> validate)
    {
        (int line, string token) = NextField(lines, ref cursor, filePath, expectedDesc);
        // Reject AllowExponent / AllowThousands per Pitfall 8 / D-18 spirit.
        const NumberStyles style =
            NumberStyles.Float & ~NumberStyles.AllowExponent & ~NumberStyles.AllowThousands;
        if (!double.TryParse(token, style, CultureInfo.InvariantCulture, out double v)
            || !validate(v))
        {
            throw new ScalaKbmParseException(filePath, line, 1, expectedDesc, token);
        }
        return v;
    }

    /// <summary>
    /// Find the next non-comment-non-blank line. Returns its 1-based line
    /// number + the first whitespace-delimited token on that line. Throws on
    /// premature EOF.
    /// </summary>
    private static (int Line, string Token) NextField(
        string[] lines, ref int cursor, string filePath, string expectedDesc)
    {
        while (cursor < lines.Length)
        {
            var raw = lines[cursor];
            var stripped = StripCr(raw);
            cursor++;
            if (stripped.TrimStart().StartsWith('!')) continue;
            if (stripped.Trim().Length == 0) continue;
            int line = cursor;  // 1-based after the increment

            // Take the first whitespace-delimited token.
            var trimmed = stripped.TrimStart();
            int end = 0;
            while (end < trimmed.Length && !char.IsWhiteSpace(trimmed[end])) end++;
            return (line, trimmed[..end]);
        }
        throw new ScalaKbmParseException(filePath, lines.Length, 1, expectedDesc, "end of file");
    }

    private static string StripCr(string line)
        => line.Length > 0 && line[^1] == '\r' ? line[..^1] : line;
}
