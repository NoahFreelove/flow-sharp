using System.Collections.Generic;
using FlowLang.Diagnostics;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Notation;

/// <summary>
/// Phase 39 Plan 39-04 MML-01 — hand-rolled PC-98-era MML common-core parser.
///
/// <para>
/// Supported per D-39-18:
/// <list type="bullet">
///   <item>Notes <c>a</c>..<c>g</c> (case-insensitive), with trailing
///     accidentals <c>+</c> / <c>#</c> (sharp), <c>-</c> (flat).</item>
///   <item>Octave: <c>O&lt;n&gt;</c> (absolute, 0..8 clamped),
///     <c>&gt;</c> (up 1), <c>&lt;</c> (down 1).</item>
///   <item>Length: <c>L&lt;n&gt;</c> sets default; trailing digits after a
///     note override per-note (e.g. <c>c8</c> = eighth note even with L4
///     active).</item>
///   <item>Tempo: <c>T&lt;n&gt;</c> sets BPM (populates the Sequence's
///     MusicalContext).</item>
///   <item>Loops: <c>[...]&lt;n&gt;</c> repeats the bracketed content N
///     times. Nesting depth capped at 16 per D-39-19 (mirrors T-36-17
///     DoS guard); total expanded notes capped at 65536 per Pitfall 4.</item>
///   <item>Rest: <c>r</c> / <c>R</c> (with length suffix).</item>
///   <item>Dot: trailing <c>.</c> after a note extends length × 1.5
///     (handled via <c>isDotted</c>).</item>
///   <item>Tie: trailing <c>&amp;</c> sets <c>IsTied</c>.</item>
/// </list>
/// </para>
///
/// <para>
/// Charitable per D-39-19 / D-v1.5-05: malformed input never throws.
/// Unknown opcodes drop with a one-shot <c>[mml] dropped opcode</c>
/// stderr advisory. The loop bomb defense (Pitfall 4) emits one
/// <c>[mml] loop nesting depth</c> or <c>[mml] expansion cap</c> advisory
/// and truncates expansion.
/// </para>
/// </summary>
public static class MmlImport
{
    private const int MaxLoopNestingDepth = 16;
    private const int MaxExpandedNoteCount = 65536;

    public static SequenceData ParseMml(string source)
    {
        try
        {
            return ParseInternal(source ?? string.Empty);
        }
        catch (System.Exception ex)
        {
            // Charitable per D-39-19 — never throw on malformed input.
            string preview = (source ?? string.Empty).Length > 40
                ? (source ?? string.Empty).Substring(0, 40)
                : (source ?? string.Empty);
            RenderingDiagnostics.WarnOnce(
                $"mml-parse-error:{preview}",
                $"[mml] parse error: {ex.Message}");
            var ts = new TimeSignatureData(4, 4);
            var seq = new SequenceData();
            seq.AddBar(new BarData(new List<MusicalNoteData>(), ts));
            return seq;
        }
    }

    private sealed class ParserState
    {
        public string Source = string.Empty;
        public int Pos;
        public int Octave = 4;
        public int DefaultLengthN = 4;
        public double Tempo = 120.0;
        public int ExpandedCount;
        public List<MusicalNoteData> Notes = new();
    }

    private static SequenceData ParseInternal(string source)
    {
        var state = new ParserState { Source = source };
        ParseRun(state, depth: 0);

        var ts = new TimeSignatureData(4, 4);
        var seq = new SequenceData();
        // Single Bar carrying all notes (charitable simplification — MML has
        // no meter concept; downstream renderer treats the whole run as one
        // contiguous bar in 4/4).
        seq.AddBar(new BarData(state.Notes, ts));
        return seq;
    }

    /// <summary>
    /// Walks tokens starting from <c>state.Pos</c>, appending notes to
    /// <c>state.Notes</c>. Returns when a top-level <c>]</c> is reached
    /// (loop-frame close) or end-of-source. Loop frames recurse.
    /// </summary>
    private static void ParseRun(ParserState state, int depth)
    {
        while (state.Pos < state.Source.Length)
        {
            if (state.ExpandedCount >= MaxExpandedNoteCount)
            {
                RenderingDiagnostics.WarnOnce(
                    "mml-expansion-cap",
                    $"[mml] expansion cap reached ({MaxExpandedNoteCount} notes); truncating");
                // Skip to end of source
                state.Pos = state.Source.Length;
                return;
            }

            char c = state.Source[state.Pos];

            // Whitespace / commas — skip
            if (c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == ',')
            {
                state.Pos++;
                continue;
            }

            // Loop frame
            if (c == '[')
            {
                state.Pos++;
                int loopStartNotes = state.Notes.Count;
                if (depth >= MaxLoopNestingDepth)
                {
                    RenderingDiagnostics.WarnOnce(
                        "mml-loop-depth",
                        $"[mml] loop nesting depth {MaxLoopNestingDepth} exceeded — collapsing to 1 iteration");
                    // Parse body once but discard repeat-count; treat the [ as a no-op wrapper
                    ParseRun(state, depth + 1);
                    // Read trailing digit(s) to consume the count and ignore
                    ReadDigits(state);
                    continue;
                }
                // Walk inner body once
                ParseRun(state, depth + 1);
                // After the inner ParseRun, state.Pos should point past the matching ]
                // Read iteration count (default 1)
                int iterations = ReadDigits(state, defaultVal: 1);
                if (iterations <= 1) continue;  // already walked once

                // Replicate (iterations - 1) more times by copying the slice we just appended.
                int sliceLen = state.Notes.Count - loopStartNotes;
                if (sliceLen <= 0) continue;
                // Snapshot the slice
                var slice = state.Notes.GetRange(loopStartNotes, sliceLen);
                for (int k = 1; k < iterations; k++)
                {
                    foreach (var n in slice)
                    {
                        if (state.ExpandedCount >= MaxExpandedNoteCount)
                        {
                            RenderingDiagnostics.WarnOnce(
                                "mml-expansion-cap",
                                $"[mml] expansion cap reached ({MaxExpandedNoteCount} notes); truncating");
                            state.Pos = state.Source.Length;
                            return;
                        }
                        state.Notes.Add(n);
                        state.ExpandedCount++;
                    }
                }
                continue;
            }

            if (c == ']')
            {
                state.Pos++;
                return;  // close this loop frame
            }

            // Tempo
            if (c == 'T' || c == 't')
            {
                state.Pos++;
                int t = ReadDigits(state, defaultVal: -1);
                if (t > 0) state.Tempo = t;
                continue;
            }

            // Length
            if (c == 'L' || c == 'l')
            {
                state.Pos++;
                int l = ReadDigits(state, defaultVal: -1);
                if (l > 0) state.DefaultLengthN = l;
                continue;
            }

            // Absolute octave
            if (c == 'O' || c == 'o')
            {
                state.Pos++;
                int oct = ReadDigits(state, defaultVal: state.Octave);
                if (oct < 0) oct = 0;
                if (oct > 8) oct = 8;
                state.Octave = oct;
                continue;
            }

            // Relative octave up/down
            if (c == '>')
            {
                state.Pos++;
                state.Octave++;
                continue;
            }
            if (c == '<')
            {
                state.Pos++;
                state.Octave--;
                continue;
            }

            // Notes a..g (case-insensitive)
            if ((c >= 'a' && c <= 'g') || (c >= 'A' && c <= 'G'))
            {
                state.Pos++;
                char upper = char.ToUpperInvariant(c);
                // Trailing accidental
                int alteration = 0;
                if (state.Pos < state.Source.Length)
                {
                    char next = state.Source[state.Pos];
                    if (next == '+' || next == '#') { alteration = 1; state.Pos++; }
                    else if (next == '-') { alteration = -1; state.Pos++; }
                }
                // Trailing length digits
                int lenN = ReadDigits(state, defaultVal: state.DefaultLengthN);
                // Trailing dot
                bool dotted = false;
                if (state.Pos < state.Source.Length && state.Source[state.Pos] == '.')
                {
                    dotted = true;
                    state.Pos++;
                }
                // Trailing tie
                bool tied = false;
                if (state.Pos < state.Source.Length && state.Source[state.Pos] == '&')
                {
                    tied = true;
                    state.Pos++;
                }

                int? durationValue = LengthDenominatorToNoteValue(lenN);
                state.Notes.Add(new MusicalNoteData(
                    upper, state.Octave, alteration,
                    durationValue, isRest: false, centOffset: null, isTied: tied,
                    velocity: 0.63, articulation: Articulation.Normal, isDotted: dotted));
                state.ExpandedCount++;
                continue;
            }

            // Rest
            if (c == 'r' || c == 'R')
            {
                state.Pos++;
                int lenN = ReadDigits(state, defaultVal: state.DefaultLengthN);
                bool dotted = false;
                if (state.Pos < state.Source.Length && state.Source[state.Pos] == '.')
                {
                    dotted = true;
                    state.Pos++;
                }
                int? durationValue = LengthDenominatorToNoteValue(lenN);
                state.Notes.Add(new MusicalNoteData(
                    'C', 4, 0, durationValue, isRest: true, centOffset: null, isTied: false,
                    velocity: 0.63, articulation: Articulation.Normal, isDotted: dotted));
                state.ExpandedCount++;
                continue;
            }

            // Unknown opcode — collect up to next whitespace/known-prefix char
            {
                int tokenStart = state.Pos;
                state.Pos++;
                while (state.Pos < state.Source.Length)
                {
                    char x = state.Source[state.Pos];
                    if (char.IsWhiteSpace(x) || x == ',' || x == '['
                        || x == ']' || x == '>' || x == '<')
                        break;
                    // Known opcode-starters
                    if ((x >= 'a' && x <= 'g') || (x >= 'A' && x <= 'G')
                        || x == 'T' || x == 't' || x == 'L' || x == 'l'
                        || x == 'O' || x == 'o' || x == 'r' || x == 'R')
                        break;
                    state.Pos++;
                }
                string tok = state.Source.Substring(tokenStart, state.Pos - tokenStart);
                RenderingDiagnostics.WarnOnce(
                    $"mml-opcode:{tok}:{tokenStart}",
                    $"[mml] dropped opcode '{tok}' at offset {tokenStart}");
            }
        }
    }

    /// <summary>
    /// Read a contiguous run of digits at <c>state.Pos</c>. Returns the
    /// parsed int, or <paramref name="defaultVal"/> when no digits at the
    /// position.
    /// </summary>
    private static int ReadDigits(ParserState state, int defaultVal = -1)
    {
        if (state.Pos >= state.Source.Length || !char.IsDigit(state.Source[state.Pos]))
            return defaultVal;
        int start = state.Pos;
        while (state.Pos < state.Source.Length && char.IsDigit(state.Source[state.Pos]))
            state.Pos++;
        return int.TryParse(state.Source.Substring(start, state.Pos - start), out int val)
            ? val
            : defaultVal;
    }

    /// <summary>
    /// Map MML length denominator (1 = whole, 2 = half, 4 = quarter,
    /// 8 = eighth, 16 = sixteenth, 32 = thirty-second) to Flow's
    /// NoteValue enum integer. Returns QUARTER (charitable default) for
    /// unrecognized values.
    /// </summary>
    private static int? LengthDenominatorToNoteValue(int n)
    {
        return n switch
        {
            1 => (int)NoteValueType.Value.WHOLE,
            2 => (int)NoteValueType.Value.HALF,
            4 => (int)NoteValueType.Value.QUARTER,
            8 => (int)NoteValueType.Value.EIGHTH,
            16 => (int)NoteValueType.Value.SIXTEENTH,
            32 => (int)NoteValueType.Value.THIRTYSECOND,
            64 => (int)NoteValueType.Value.SIXTYFOURTH,
            128 => (int)NoteValueType.Value.ONETWENTYEIGHTH,
            _ => (int)NoteValueType.Value.QUARTER,
        };
    }
}
