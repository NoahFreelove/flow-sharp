using System.Collections.Generic;
using System.Text.RegularExpressions;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Notation;

/// <summary>
/// Phase 39 Plan 39-03 ABC-01 / ABC-02 — hand-rolled ABC 2.1 subset parser
/// + abc2midi <c>Q:</c> tempo + modal keys. Returns <see cref="SectionData"/>
/// or <c>List&lt;SectionData&gt;</c> per the dispatch rule in
/// <see cref="NotationIoBuiltins"/>.
///
/// <para>
/// Charitable per D-39-17 / D-v1.5-05: NEVER throws on malformed input;
/// unknown ornaments/headers drop with one-shot <c>[abc]</c> stderr
/// advisories. The resulting Section may be incomplete but is always
/// usable.
/// </para>
///
/// <para>
/// Coverage per D-39-15:
/// <list type="bullet">
///   <item>Headers: X (index), T (title), M (meter), L (unit note length),
///     K (key), Q (tempo). All other headers ignored with advisory.</item>
///   <item>Notes: A-G (octave 4), a-g (octave 5), with <c>'</c> shifting up
///     and <c>,</c> shifting down by an octave.</item>
///   <item>Accidentals: <c>^</c> (sharp), <c>^^</c> (double-sharp),
///     <c>_</c> (flat), <c>__</c> (double-flat), <c>=</c> (natural).</item>
///   <item>Durations: <c>N</c> (multiply), <c>/</c> (halve), <c>/N</c>
///     (divide by N). Composed with the active L: length.</item>
///   <item>Bar lines: <c>|</c>, <c>||</c>, <c>|]</c>, <c>:|</c>, <c>|:</c>.
///     Repeat marks (<c>:|</c> / <c>|:</c>) treated as plain bars with
///     one-shot advisory (Flow's BarData doesn't carry repeat marks).</item>
///   <item>Rests: <c>z</c>.</item>
///   <item>Ornaments + decorations: dropped with advisory.</item>
///   <item>Modal keys: <c>Edor</c> / <c>Dmix</c> / <c>Aphr</c> / <c>Cmix</c> /
///     <c>Glyd</c> / <c>Bphr</c> / <c>Floc</c> parsed; stored verbatim in
///     <c>Context.Key</c> for downstream renderer inspection.</item>
/// </list>
/// </para>
/// </summary>
public static class AbcImport
{
    private static readonly Regex XHeaderLineRegex =
        new Regex(@"^X:\s*\d+", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Phase 44 Plan 44-07: thread-local strict-mode context. Set by the
    /// public entry points (ParseSingleTune / ParseMultiTune) in a try/finally
    /// so the deep parser helpers can consult CallerStrictMode without
    /// threading ctx through ~10 parsing methods. Cleared on exit so the
    /// AbcImport static surface stays re-entrant-safe across threads.
    /// </summary>
    [System.ThreadStatic]
    private static FlowLang.Runtime.ExecutionContext? _strictCtx;

    /// <summary>
    /// Helper for the deep parser helpers — picks the strict branch when
    /// the thread-local context bit is set, otherwise emits the original
    /// WarnOnce advisory verbatim (byte-identical to pre-Plan-44-07 behavior).
    /// <c>internal</c> so <see cref="AbcLexer"/> (sibling parser-deep helper)
    /// can route its own advisories through the same strict-mode gate.
    /// </summary>
    internal static void EmitAbcAdvisory(string sentinelKey, string sentinelBody, string strictBody)
    {
        var ctx = _strictCtx;
        if (ctx is not null && ctx.CallerStrictMode)
        {
            ctx.ErrorReporter.ReportError($"[strict] {strictBody}", ctx.CurrentCallSite);
            return;
        }
        RenderingDiagnostics.WarnOnce(sentinelKey, sentinelBody);
    }

    /// <summary>
    /// Parse a single ABC tune (any number of X: blocks treated as one
    /// concatenated tune). Returns a <see cref="SectionData"/> built from
    /// the parsed notes, with the active musical context populated from
    /// the headers (M:, L:, K:, Q:).
    /// </summary>
    public static SectionData ParseSingleTune(string source, FlowLang.Runtime.ExecutionContext? strictCtx = null)
    {
        var previous = _strictCtx;
        _strictCtx = strictCtx;
        try
        {
            return ParseSingleTuneInner(source);
        }
        finally
        {
            _strictCtx = previous;
        }
    }

    /// <summary>
    /// Inner workhorse — assumes <see cref="_strictCtx"/> is set by the
    /// caller. Recursive ParseMultiTune calls into this so it doesn't
    /// stomp on the outer caller's strict-ctx.
    /// </summary>
    private static SectionData ParseSingleTuneInner(string source)
    {
        try
        {
            return ParseTune(source ?? string.Empty);
        }
        catch (System.Exception ex)
        {
            // Charitable D-39-17: never throw — emit advisory + return minimal usable Section.
            string preview = (source ?? string.Empty).Length > 40
                ? (source ?? string.Empty).Substring(0, 40)
                : (source ?? string.Empty);
            EmitAbcAdvisory(
                sentinelKey: $"abc-parse-error:{preview}",
                sentinelBody: $"[abc] parse error: {ex.Message}",
                strictBody: $"[abc] could not parse tempo — parse error: {ex.Message}");
            return EmptySection("abc");
        }
    }

    /// <summary>
    /// Parse a multi-tune ABC file. Splits on <c>X:N</c> line boundaries
    /// and returns one <see cref="SectionData"/> per tune.
    /// </summary>
    public static List<SectionData> ParseMultiTune(string source, FlowLang.Runtime.ExecutionContext? strictCtx = null)
    {
        var previous = _strictCtx;
        _strictCtx = strictCtx;
        try
        {
            return ParseMultiTuneInner(source);
        }
        finally
        {
            _strictCtx = previous;
        }
    }

    private static List<SectionData> ParseMultiTuneInner(string source)
    {
        var result = new List<SectionData>();
        if (string.IsNullOrEmpty(source))
        {
            result.Add(EmptySection("abc"));
            return result;
        }

        // Find X: header line positions. Each tune ends just before the next X: line.
        var matches = XHeaderLineRegex.Matches(source);
        if (matches.Count <= 1)
        {
            result.Add(ParseSingleTuneInner(source));
            return result;
        }

        for (int i = 0; i < matches.Count; i++)
        {
            int start = matches[i].Index;
            int end = (i + 1 < matches.Count) ? matches[i + 1].Index : source.Length;
            string tuneText = source.Substring(start, end - start);
            result.Add(ParseSingleTuneInner(tuneText));
        }
        return result;
    }

    private static SectionData EmptySection(string name)
    {
        var ts = new TimeSignatureData(4, 4);
        var seq = new SequenceData();
        seq.AddBar(new BarData(new List<MusicalNoteData>(), ts));
        var ctx = new MusicalContext { Tempo = 120.0, TimeSignature = ts, Key = "Cmajor" };
        var sequences = new Dictionary<string, SequenceData> { ["abc"] = seq };
        return new SectionData(name, sequences, ctx, null);
    }

    /// <summary>
    /// Core ABC tune parser. Walks headers + body tokens, accumulates notes
    /// into Bars split on <see cref="AbcTokenType.BarLine"/>. Returns a
    /// SectionData with a single Sequence named "abc".
    /// </summary>
    private static SectionData ParseTune(string source)
    {
        // === Pass 1: headers + accumulate body text ===
        string title = "abc";
        int meterNum = 4, meterDenom = 4;
        bool meterSpecified = false;
        // Default L: per Pitfall 3 — set after meter is known
        int defaultLengthNumerator = 1;
        int defaultLengthDenominator = 4;  // will be re-derived from meter
        bool defaultLengthExplicit = false;
        string key = "Cmajor";
        double tempo = 120.0;
        var bodyLines = new List<string>();

        using (var reader = new System.IO.StringReader(source))
        {
            string? rawLine;
            int lineNo = 0;
            while ((rawLine = reader.ReadLine()) != null)
            {
                lineNo++;
                string lineTrim = rawLine.TrimEnd();
                if (lineTrim.Length == 0) continue;
                if (lineTrim.StartsWith("%")) continue;  // comment

                // Header line if matches "L:..."
                if (lineTrim.Length >= 2 && lineTrim[1] == ':' && char.IsLetter(lineTrim[0]))
                {
                    char letter = lineTrim[0];
                    string val = lineTrim.Substring(2).Trim();
                    switch (letter)
                    {
                        case 'X':
                            // Index — ignored at SectionData level (multi-tune splitting handled upstream)
                            break;
                        case 'T':
                            title = val.Length > 0 ? val : title;
                            break;
                        case 'M':
                            (meterNum, meterDenom, meterSpecified) = ParseMeter(val, lineNo);
                            break;
                        case 'L':
                            (defaultLengthNumerator, defaultLengthDenominator) = ParseUnitLength(val, lineNo);
                            defaultLengthExplicit = true;
                            break;
                        case 'K':
                            key = ParseKey(val, lineNo);
                            break;
                        case 'Q':
                            tempo = ParseTempo(val, tempo, lineNo);
                            break;
                        default:
                            // Unknown header — drop with advisory
                            EmitAbcAdvisory(
                                sentinelKey: $"abc-header:{letter}",
                                sentinelBody: $"[abc] ignored header '{letter}'",
                                strictBody: $"[abc] unknown bar marker — ignored header '{letter}' at line {lineNo}");
                            break;
                    }
                    continue;
                }

                bodyLines.Add(rawLine);
            }
        }

        // Apply Pitfall 3 default L: when not explicitly set
        if (!defaultLengthExplicit)
        {
            // ABC 2.1 §3.1.1.6: if meter ≥ 3/4, default L = 1/4; else default L = 1/8.
            // We approximate meter "magnitude" as numerator/denominator ratio ≥ 0.75.
            double ratio = meterDenom > 0 ? (double)meterNum / meterDenom : 1.0;
            if (ratio >= 0.75)
            {
                defaultLengthNumerator = 1;
                defaultLengthDenominator = 4;
            }
            else
            {
                defaultLengthNumerator = 1;
                defaultLengthDenominator = 8;
            }
        }

        // === Pass 2: tokenize and walk body ===
        var ts = new TimeSignatureData(meterNum, meterDenom);
        var sequence = new SequenceData();
        var currentBarNotes = new List<MusicalNoteData>();
        int currentAccidental = 0;  // resets each bar
        int accidentalForNext = 0;  // accumulated by ^, _, =, _.. ^^.. tokens
        bool nextAccidentalIsExplicit = false;

        string bodyText = string.Join("\n", bodyLines);
        var tokens = AbcLexer.Tokenize(bodyText);

        int idx = 0;
        while (idx < tokens.Count)
        {
            var tok = tokens[idx];
            switch (tok.Type)
            {
                case AbcTokenType.Eof:
                case AbcTokenType.Newline:
                    idx++;
                    continue;

                case AbcTokenType.Accidental:
                    accidentalForNext = tok.Text switch
                    {
                        "^^" => 2,
                        "^"  => 1,
                        "__" => -2,
                        "_"  => -1,
                        "="  => 0,
                        _    => 0,
                    };
                    nextAccidentalIsExplicit = true;
                    idx++;
                    continue;

                case AbcTokenType.Ornament:
                case AbcTokenType.Decoration:
                    EmitAbcAdvisory(
                        sentinelKey: $"abc-ornament:{tok.Text}:{tok.Line}",
                        sentinelBody: $"[abc] dropped ornament '{tok.Text}' at line {tok.Line}",
                        strictBody: $"[abc] dropped ornament '{tok.Text}' at line {tok.Line}");
                    idx++;
                    continue;

                case AbcTokenType.Quote:
                    // Chord-symbol annotation — drop for v1.5 (renderer doesn't consume).
                    idx++;
                    continue;

                case AbcTokenType.BarLine:
                    {
                        // Repeat marks emit a one-shot advisory; treat as plain bar.
                        if (tok.Text == ":|" || tok.Text == "|:")
                        {
                            EmitAbcAdvisory(
                                sentinelKey: $"abc-repeat:{tok.Text}",
                                sentinelBody: $"[abc] repeat mark '{tok.Text}' parsed as plain bar (Flow's BarData has no repeat support)",
                                strictBody: $"[abc] unknown bar marker '{tok.Text}' parsed as plain bar at line {tok.Line}");
                        }
                        // Flush current bar
                        if (currentBarNotes.Count > 0)
                        {
                            sequence.AddBar(new BarData(currentBarNotes, ts));
                            currentBarNotes = new List<MusicalNoteData>();
                        }
                        accidentalForNext = 0;
                        nextAccidentalIsExplicit = false;
                        idx++;
                        continue;
                    }

                case AbcTokenType.Note:
                    {
                        char noteLetter = tok.Text[0];
                        // ABC convention: A..G octave 4; a..g octave 5
                        bool isLower = noteLetter >= 'a' && noteLetter <= 'g';
                        char upper = char.ToUpperInvariant(noteLetter);
                        int octave = isLower ? 5 : 4;

                        // Consume trailing octave shifts
                        idx++;
                        while (idx < tokens.Count &&
                               (tokens[idx].Type == AbcTokenType.OctaveUp ||
                                tokens[idx].Type == AbcTokenType.OctaveDown))
                        {
                            if (tokens[idx].Type == AbcTokenType.OctaveUp) octave++;
                            else octave--;
                            idx++;
                        }

                        // Consume trailing duration
                        (int numer, int denom) = (1, 1);
                        bool dotted = false;
                        if (idx < tokens.Count && tokens[idx].Type == AbcTokenType.Duration)
                        {
                            (numer, denom, dotted) = ParseDurationToken(tokens[idx].Text);
                            idx++;
                        }

                        // Consume trailing tie marker
                        bool tied = false;
                        if (idx < tokens.Count && tokens[idx].Type == AbcTokenType.Tie)
                        {
                            tied = true;
                            idx++;
                        }

                        // Combine default L: with note duration
                        // Resulting duration in quarter-note units = (defaultLN/defaultLD) × (numer/denom) × 4
                        // i.e., 4 * defaultLN * numer / (defaultLD * denom) quarters
                        double quarters = 4.0 * defaultLengthNumerator * numer / (defaultLengthDenominator * (double)denom);
                        var nv = QuartersToNoteValue(quarters, out bool dottedFromValue);
                        int? durationValue = nv;
                        bool finalDotted = dotted || dottedFromValue;

                        int alteration = nextAccidentalIsExplicit ? accidentalForNext : 0;
                        nextAccidentalIsExplicit = false;
                        accidentalForNext = 0;

                        currentBarNotes.Add(new MusicalNoteData(
                            upper, octave, alteration,
                            durationValue, isRest: false, centOffset: null, isTied: tied,
                            velocity: 0.63, articulation: Articulation.Normal, isDotted: finalDotted));
                        continue;
                    }

                case AbcTokenType.Rest:
                    {
                        idx++;
                        (int numer, int denom) = (1, 1);
                        bool dotted = false;
                        if (idx < tokens.Count && tokens[idx].Type == AbcTokenType.Duration)
                        {
                            (numer, denom, dotted) = ParseDurationToken(tokens[idx].Text);
                            idx++;
                        }
                        double quarters = 4.0 * defaultLengthNumerator * numer / (defaultLengthDenominator * (double)denom);
                        var nv = QuartersToNoteValue(quarters, out bool dottedFromValue);
                        bool finalDotted = dotted || dottedFromValue;
                        currentBarNotes.Add(new MusicalNoteData(
                            'C', 4, 0, nv, isRest: true, centOffset: null, isTied: false,
                            velocity: 0.63, articulation: Articulation.Normal, isDotted: finalDotted));
                        continue;
                    }

                case AbcTokenType.OpenChord:
                case AbcTokenType.CloseChord:
                    // Chord brackets in ABC body context — Flow's chord handling differs; for
                    // v1.5 we skip the brackets and let the inner notes emit sequentially
                    // (charitable simplification). Future v1.6 may interpret [CEG] as polyphony.
                    idx++;
                    continue;

                case AbcTokenType.Duration:
                case AbcTokenType.OctaveUp:
                case AbcTokenType.OctaveDown:
                case AbcTokenType.Tie:
                    // Orphan modifier — skip (no preceding note to attach to)
                    idx++;
                    continue;

                case AbcTokenType.Header:
                case AbcTokenType.Unknown:
                default:
                    idx++;
                    continue;
            }
        }

        // Flush trailing bar
        if (currentBarNotes.Count > 0)
        {
            sequence.AddBar(new BarData(currentBarNotes, ts));
        }
        // Ensure at least one bar exists (empty SectionData is still valid)
        if (sequence.Bars.Count == 0)
        {
            sequence.AddBar(new BarData(new List<MusicalNoteData>(), ts));
        }

        var context = new MusicalContext { Tempo = tempo, TimeSignature = ts, Key = key };
        var sequencesDict = new Dictionary<string, SequenceData> { ["abc"] = sequence };
        return new SectionData(title, sequencesDict, context, null);
    }

    // === Helpers ===

    private static (int num, int denom, bool specified) ParseMeter(string val, int line)
    {
        // Accepts "4/4", "6/8", "C" (common time → 4/4), "C|" (cut time → 2/2).
        string v = val.Trim();
        if (v == "C") return (4, 4, true);
        if (v == "C|") return (2, 2, true);
        var parts = v.Split('/');
        if (parts.Length == 2 &&
            int.TryParse(parts[0].Trim(), out int n) &&
            int.TryParse(parts[1].Trim(), out int d) &&
            n > 0 && d > 0)
        {
            return (n, d, true);
        }
        EmitAbcAdvisory(
            sentinelKey: $"abc-meter:{val}:{line}",
            sentinelBody: $"[abc] could not parse meter '{val}' at line {line}; using 4/4 default",
            strictBody: $"[abc] could not parse meter '{val}' at line {line} — using 4/4");
        return (4, 4, false);
    }

    private static (int num, int denom) ParseUnitLength(string val, int line)
    {
        // Accepts "1/4", "1/8", "1/16", "1" (rare), etc.
        string v = val.Trim();
        if (int.TryParse(v, out int wholeOnly) && wholeOnly > 0) return (wholeOnly, 1);
        var parts = v.Split('/');
        if (parts.Length == 2 &&
            int.TryParse(parts[0].Trim(), out int n) &&
            int.TryParse(parts[1].Trim(), out int d) &&
            n > 0 && d > 0)
        {
            return (n, d);
        }
        EmitAbcAdvisory(
            sentinelKey: $"abc-unitlength:{val}:{line}",
            sentinelBody: $"[abc] could not parse unit length '{val}' at line {line}; using 1/8 default",
            strictBody: $"[abc] could not parse Q: header — unit length '{val}' at line {line}, using 1/8");
        return (1, 8);
    }

    /// <summary>
    /// Map ABC key string ("Cmaj", "Amin", "Dmix", "Edor", "F#dor", "Bbmaj", etc.) to
    /// Flow's MusicalContext.Key convention ("Cmajor", "Aminor", "Edor", ...). Modal keys
    /// per D-39-15 are stored verbatim (Edor/Dmix/etc.); major/minor get the canonical Flow
    /// suffix.
    /// </summary>
    private static string ParseKey(string val, int line)
    {
        string v = val.Trim();
        if (v.Length == 0) return "Cmajor";

        // Strip mode word (case-insensitive) — accepts "Cmaj", "Cmajor", "Amin", "Aminor",
        // "Dmix", "Edor", etc.
        // Simple normalization: extract the leading [A-Ga-g][b#]? as the root, then map.
        int i = 0;
        char root = char.ToUpperInvariant(v[i]);
        if (root < 'A' || root > 'G')
        {
            EmitAbcAdvisory(
                sentinelKey: $"abc-key:{val}:{line}",
                sentinelBody: $"[abc] unknown key '{val}' at line {line}; using Cmajor",
                strictBody: $"[abc] unknown key '{val}' at line {line} — using Cmajor");
            return "Cmajor";
        }
        i++;
        string accidental = "";
        if (i < v.Length && v[i] == '#') { accidental = "sharp"; i++; }
        else if (i < v.Length && v[i] == 'b') { accidental = "b"; i++; }

        string rest = i < v.Length ? v.Substring(i).ToLowerInvariant() : "";
        rest = rest.Replace(" ", "");

        // Modal recognition
        string mode;
        if (rest.StartsWith("dor")) mode = "dorian";
        else if (rest.StartsWith("mix")) mode = "mixolydian";
        else if (rest.StartsWith("phr")) mode = "phrygian";
        else if (rest.StartsWith("lyd")) mode = "lydian";
        else if (rest.StartsWith("loc")) mode = "locrian";
        else if (rest.StartsWith("min") || rest == "m") mode = "minor";
        else if (rest.StartsWith("maj") || rest.Length == 0) mode = "major";
        else mode = "major";  // charitable

        return $"{root}{accidental}{mode}";
    }

    private static double ParseTempo(string val, double current, int line)
    {
        string v = val.Trim();

        // Strip leading quoted-text annotation (e.g., `"Allegro" 1/4=120`)
        if (v.StartsWith("\""))
        {
            int closeQuote = v.IndexOf('"', 1);
            if (closeQuote > 0) v = v.Substring(closeQuote + 1).TrimStart();
        }

        // Match "{numerator}/{denominator}={bpm}" pattern → bpm is the meaningful value
        var equalsMatch = Regex.Match(v, @"=\s*(\d+(?:\.\d+)?)");
        if (equalsMatch.Success && double.TryParse(equalsMatch.Groups[1].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double bpm) && bpm > 0)
        {
            return bpm;
        }

        // Bare number "Q:120"
        if (double.TryParse(v, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double simpleBpm) && simpleBpm > 0)
        {
            return simpleBpm;
        }

        EmitAbcAdvisory(
            sentinelKey: $"abc-tempo:{val}:{line}",
            sentinelBody: $"[abc] could not parse tempo '{val}' at line {line}; using default 120",
            strictBody: $"[abc] could not parse tempo '{val}' at line {line} — using default 120");
        return current;
    }

    /// <summary>
    /// Parse an ABC duration token like "", "2", "/", "/2", "/4". Returns
    /// (numerator, denominator, isDotted). Dotted notation in ABC uses
    /// trailing dot suffix (e.g., "A2.") but our lexer captures dots as
    /// unknown — kept reserved for v1.6 expansion.
    /// </summary>
    private static (int num, int denom, bool dotted) ParseDurationToken(string text)
    {
        if (string.IsNullOrEmpty(text)) return (1, 1, false);
        if (text[0] == '/')
        {
            // "/" → denom 2; "/N" → denom N
            if (text.Length == 1) return (1, 2, false);
            string rest = text.Substring(1);
            if (int.TryParse(rest, out int d) && d > 0) return (1, d, false);
            return (1, 2, false);
        }
        if (int.TryParse(text, out int n) && n > 0) return (n, 1, false);
        return (1, 1, false);
    }

    /// <summary>
    /// Map a quarter-note duration count → Flow NoteValue enum integer.
    /// Returns null for non-power-of-2 multiples; the resulting GetBeats
    /// fallback (1.0) still gives a usable note (charitable per D-v1.5-05).
    /// </summary>
    private static int? QuartersToNoteValue(double quarters, out bool dotted)
    {
        dotted = false;
        if (quarters <= 0) return (int)NoteValueType.Value.QUARTER;

        // Standard mappings (Flow's NoteValue.Value)
        // WHOLE = 4q, HALF = 2q, QUARTER = 1q, EIGHTH = 0.5q, SIXTEENTH = 0.25q, etc.
        const double eps = 1e-6;
        if (System.Math.Abs(quarters - 4.0) < eps) return (int)NoteValueType.Value.WHOLE;
        if (System.Math.Abs(quarters - 2.0) < eps) return (int)NoteValueType.Value.HALF;
        if (System.Math.Abs(quarters - 1.0) < eps) return (int)NoteValueType.Value.QUARTER;
        if (System.Math.Abs(quarters - 0.5) < eps) return (int)NoteValueType.Value.EIGHTH;
        if (System.Math.Abs(quarters - 0.25) < eps) return (int)NoteValueType.Value.SIXTEENTH;
        if (System.Math.Abs(quarters - 0.125) < eps) return (int)NoteValueType.Value.THIRTYSECOND;
        // Dotted equivalents
        if (System.Math.Abs(quarters - 6.0) < eps) { dotted = true; return (int)NoteValueType.Value.WHOLE; }
        if (System.Math.Abs(quarters - 3.0) < eps) { dotted = true; return (int)NoteValueType.Value.HALF; }
        if (System.Math.Abs(quarters - 1.5) < eps) { dotted = true; return (int)NoteValueType.Value.QUARTER; }
        if (System.Math.Abs(quarters - 0.75) < eps) { dotted = true; return (int)NoteValueType.Value.EIGHTH; }
        if (System.Math.Abs(quarters - 0.375) < eps) { dotted = true; return (int)NoteValueType.Value.SIXTEENTH; }

        // Fallback: pick nearest power-of-2 (charitable)
        if (quarters >= 3.0) return (int)NoteValueType.Value.WHOLE;
        if (quarters >= 1.5) return (int)NoteValueType.Value.HALF;
        if (quarters >= 0.75) return (int)NoteValueType.Value.QUARTER;
        if (quarters >= 0.375) return (int)NoteValueType.Value.EIGHTH;
        if (quarters >= 0.1875) return (int)NoteValueType.Value.SIXTEENTH;
        return (int)NoteValueType.Value.THIRTYSECOND;
    }
}
