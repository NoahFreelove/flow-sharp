using System;
using System.Collections.Generic;
using System.Globalization;

namespace FlowLang.StandardLibrary.Audio.Tuning;

/// <summary>
/// Result of parsing a Scala <c>.scl</c> file. Consumed by Plan 32-03 to build
/// <c>ResolvedTuning</c> (the render-time value). Fields:
///
///   - <see cref="Description"/> — verbatim first non-comment line (trimmed).
///   - <see cref="StepCents"/> — N-1 intra-period steps in cents (D-10). The
///     period is EXTRACTED to <see cref="PeriodCents"/> as a dedicated field, so
///     <c>StepCents.Length</c> matches "pitches per period minus the period itself"
///     without an off-by-one.
///   - <see cref="PeriodCents"/> — the final step in cents (1200.0 for 2/1,
///     1404.0 for Carlos Alpha, etc.).
///   - <see cref="Ratios"/> — original ratio form preserved per D-11; keys are
///     step indices 0..N-1 inclusive (the period at index N-1 IS in the dict if
///     it was a ratio input). Cents-input steps are absent from the dict.
///   - <see cref="FilePath"/> — origin path, propagated into error messages.
/// </summary>
public sealed record ParsedScala(
    string Description,
    double[] StepCents,
    double PeriodCents,
    IReadOnlyDictionary<int, (int Num, int Den)> Ratios,
    string FilePath);

/// <summary>
/// Hand-rolled .scl format parser per the Huygens-Fokker spec
/// (https://www.huygens-fokker.org/scala/scl_format.html). Single-pass, line by
/// line, with explicit line/column tracking for {file}:{line}:{col} diagnostics
/// (SPEC-7).
///
/// Strict-reject rules per CONTEXT D-18 (spec is silent on each):
///   - <c>3 / 2</c> (whitespace around slash) — REJECT
///   - <c>1.5e2</c> (scientific notation in cents) — REJECT
///   - <c>100,5</c> (comma-decimal cents) — REJECT
/// All numeric parsing uses <see cref="CultureInfo.InvariantCulture"/> + a
/// NumberStyles mask that excludes <c>AllowExponent</c> / <c>AllowThousands</c>
/// (Pitfall 8 determinism guard).
///
/// DoS guard per threat T-32-PARSE-01: hard cap of 10000 step values.
/// </summary>
public sealed class ScalaParser
{
    private const int MaxStepCount = 10000;

    /// <summary>
    /// NumberStyles for cents values. AllowExponent and AllowThousands are
    /// excluded per D-18 (strict-reject 1.5e2 and 100,5).
    /// </summary>
    private const NumberStyles CentsStyle =
        NumberStyles.Float & ~NumberStyles.AllowExponent & ~NumberStyles.AllowThousands;

    public static ParsedScala Parse(string content, string filePath)
    {
        // Single-pass line walker. We track the lexical line number (1-based)
        // for diagnostics, distinct from the logical "next data line" cursor.
        var lines = content.Split('\n');
        int lineCursor = 0;  // index into `lines` (0-based)

        // 1. Skip leading blank lines and `!` comments; capture description.
        //    RESEARCH A1 charitable: blank lines before the description tolerated.
        //    RESEARCH §.scl Format Reference: description is the first non-comment
        //    line; per spec "an empty line" encodes a blank description.
        string? description = null;
        while (lineCursor < lines.Length)
        {
            var raw = lines[lineCursor];
            var stripped = StripCr(raw);
            lineCursor++;
            // `!` comments anywhere in the file (RESEARCH §.scl Format Reference).
            if (stripped.TrimStart().StartsWith('!')) continue;
            // Leading blank lines before description: charitable skip.
            if (stripped.Trim().Length == 0) continue;
            description = stripped.Trim();
            break;
        }
        if (description is null)
        {
            // No description line found — file is all comments or empty.
            throw new ScalaParseException(filePath, lines.Length, 1,
                "scale description (first non-comment line)", "end of file");
        }

        // 2. Next non-comment-non-blank line: the step count.
        int stepCount = -1;
        int stepCountLine = -1;
        while (lineCursor < lines.Length)
        {
            var raw = lines[lineCursor];
            var stripped = StripCr(raw);
            lineCursor++;
            if (stripped.TrimStart().StartsWith('!')) continue;
            if (stripped.Trim().Length == 0) continue;
            var token = stripped.Trim();
            stepCountLine = lineCursor;  // 1-based line of the step count

            // Reject any sign character, leading `+`, or non-digits.
            // NumberStyles.None forbids leading sign + leading whitespace + decimal point.
            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out stepCount))
            {
                throw new ScalaParseException(filePath, stepCountLine, 1,
                    "step count (positive integer)", token);
            }
            // Per spec, 0 IS a valid step count (the implicit 1/1 only). We still
            // accept here; the loop will simply run zero times and produce an
            // empty intra-period array with PeriodCents == 0 — caller should
            // handle that gracefully (Plan 32-03's ResolvedTuning builder will
            // reject 0-step scales since they have no period). Document for
            // future relax: Phase 32 ships with no real 0-step fixtures.
            if (stepCount < 0)
            {
                throw new ScalaParseException(filePath, stepCountLine, 1,
                    "step count (positive integer)", token);
            }
            if (stepCount > MaxStepCount)
            {
                // T-32-PARSE-01 mitigation: bounded loop allocation guard. 10000
                // covers every real-world tuning archive file (max known ~120).
                throw new ScalaParseException(filePath, stepCountLine, 1,
                    "step count <= 10000", token);
            }
            break;
        }
        if (stepCount < 0)
        {
            throw new ScalaParseException(filePath, lines.Length, 1,
                "step count (positive integer)", "end of file");
        }

        // 3. Read EXACTLY `stepCount` step lines. Bounded loop per T-32-PARSE-01.
        var cents = new double[stepCount];
        var ratios = new Dictionary<int, (int Num, int Den)>();
        for (int i = 0; i < stepCount; i++)
        {
            (int stepLine, string stepTok, double stepCents, (int Num, int Den)? ratio) =
                NextStep(lines, ref lineCursor, filePath);
            cents[i] = stepCents;
            if (ratio.HasValue) ratios[i] = ratio.Value;
            // stepLine/stepTok intentionally only consumed for context in NextStep;
            // we don't surface them here on success.
        }

        // 4. Split into intra-period steps (length N-1) + period (the final step).
        // Per D-10 the period is a dedicated field, NOT part of StepCents.
        double[] intra;
        double periodCents;
        if (stepCount == 0)
        {
            // Edge: a 0-step .scl. Conceptually "the implicit 1/1 only" — period
            // is 0 cents (unison). Plan 32-03 will reject this; Phase 32 parser
            // returns it as-is to keep the parser uncritical of higher-level
            // semantics.
            intra = Array.Empty<double>();
            periodCents = 0.0;
        }
        else
        {
            intra = new double[stepCount - 1];
            Array.Copy(cents, 0, intra, 0, stepCount - 1);
            periodCents = cents[stepCount - 1];
        }

        return new ParsedScala(description, intra, periodCents, ratios, filePath);
    }

    /// <summary>
    /// Reads the next non-comment-non-blank line from <paramref name="lines"/>
    /// starting at <paramref name="cursor"/>. Returns the parsed cents value
    /// + optional original-ratio form. Advances <paramref name="cursor"/> past
    /// the consumed line. Throws <see cref="ScalaParseException"/> on malformed
    /// step values or premature EOF.
    /// </summary>
    private static (int Line, string Token, double Cents, (int Num, int Den)? Ratio)
        NextStep(string[] lines, ref int cursor, string filePath)
    {
        while (cursor < lines.Length)
        {
            var raw = lines[cursor];
            var stripped = StripCr(raw);
            cursor++;
            if (stripped.TrimStart().StartsWith('!')) continue;
            if (stripped.Trim().Length == 0) continue;
            int line = cursor;  // 1-based after the increment

            // Per spec: "Anything after a valid pitch value should be ignored.
            // Space or horizontal tab characters are allowed and should be ignored."
            // → Take the FIRST whitespace-delimited token of the trimmed line.
            var trimmed = stripped.TrimStart();
            string token;
            // Find end of first whitespace-delimited token. Use a manual scan
            // so we know what was actually consumed.
            int end = 0;
            while (end < trimmed.Length && !char.IsWhiteSpace(trimmed[end])) end++;
            token = trimmed[..end];

            // Determine cents-vs-ratio: per spec "If the value contains a period,
            // it is a cents value, otherwise a ratio." Slash-no-period = ratio.
            // No-slash-no-period (e.g. `2`) = implicit `2/1` ratio per spec.
            // After taking just the first whitespace token, if the user wrote
            // `3 / 2`, our token is `3` — no slash, integer interpreted as ratio
            // 3/1. That's NOT what the user wrote, so we ALSO check the rest of
            // the trimmed line: if the next non-whitespace character is `/`,
            // treat the whole authored sequence as a malformed ratio per D-18
            // (whitespace around slash is strict-rejected).
            if (!token.Contains('.'))
            {
                // Defensive D-18 check: scan post-token for a stray `/` token.
                // If the rest of the line (before any `!` or other content) starts
                // with optional whitespace then `/`, the user authored `N / M`.
                int restStart = end;
                while (restStart < trimmed.Length && (trimmed[restStart] == ' ' || trimmed[restStart] == '\t'))
                    restStart++;
                if (restStart < trimmed.Length && trimmed[restStart] == '/')
                {
                    // Reconstruct the full malformed authored sequence for the
                    // 'found' field so the error is human-readable.
                    // Take the rest of the line up to the next `!` comment marker.
                    int restEnd = trimmed.Length;
                    int bang = trimmed.IndexOf('!', restStart);
                    if (bang >= 0) restEnd = bang;
                    var authored = trimmed[..restEnd].TrimEnd();
                    throw new ScalaParseException(filePath, line, 1,
                        "cents value or ratio", authored);
                }
            }

            if (token.Contains('.'))
            {
                // Cents path: strict reject AllowExponent/AllowThousands per D-18.
                // Also reject embedded comma (CultureInfo.InvariantCulture already
                // rejects it — but assert defensively for clarity).
                if (token.Contains(',') || token.Contains('e') || token.Contains('E'))
                {
                    throw new ScalaParseException(filePath, line, 1,
                        "cents value or ratio", token);
                }
                if (!double.TryParse(token, CentsStyle, CultureInfo.InvariantCulture, out double c))
                {
                    throw new ScalaParseException(filePath, line, 1,
                        "cents value or ratio", token);
                }
                // Negative cents accepted verbatim per D-09.
                return (line, token, c, null);
            }
            else
            {
                // Ratio path. `N` or `N/M`. Slashes more than one = error.
                int slashCount = 0;
                foreach (var ch in token) if (ch == '/') slashCount++;
                if (slashCount > 1)
                {
                    throw new ScalaParseException(filePath, line, 1,
                        "cents value or ratio", token);
                }

                int num, den;
                if (slashCount == 0)
                {
                    // Per spec: integer values with no period or slash are ratios
                    // with implicit denominator 1 (e.g. `2` → 2/1).
                    if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out num))
                    {
                        throw new ScalaParseException(filePath, line, 1,
                            "cents value or ratio", token);
                    }
                    den = 1;
                }
                else
                {
                    var parts = token.Split('/');
                    if (parts.Length != 2 ||
                        !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out num) ||
                        !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out den))
                    {
                        throw new ScalaParseException(filePath, line, 1,
                            "cents value or ratio", token);
                    }
                }
                if (num <= 0 || den <= 0)
                {
                    // Per spec: "Negative ratios are meaningless and should give
                    // a read error." Also reject 0 in numerator or denominator.
                    throw new ScalaParseException(filePath, line, 1,
                        "positive ratio", token);
                }
                double centsValue = 1200.0 * Math.Log2((double)num / (double)den);
                return (line, token, centsValue, (num, den));
            }
        }
        // Premature EOF — the declared step count exceeds available data lines.
        throw new ScalaParseException(filePath, lines.Length, 1,
            "step value", "end of file");
    }

    /// <summary>Trim a trailing carriage return so we handle both LF and CRLF.</summary>
    private static string StripCr(string line)
        => line.Length > 0 && line[^1] == '\r' ? line[..^1] : line;
}
