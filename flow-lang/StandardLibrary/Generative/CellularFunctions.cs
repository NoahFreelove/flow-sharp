using System;
using System.Collections.Generic;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
// Disambiguate FlowLang.Runtime.ExecutionContext from System.Threading.ExecutionContext
// — bare name is ambiguous under net10.0's implicit usings (Plan 36-05/06/07 precedent).
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.StandardLibrary.Generative;

/// <summary>
/// Phase 36 Plan 36-08 (GEN-03, D-36-08): the cellular automata primitives.
///
/// <para>
/// <b>Composer surface (three registered builtins):</b>
/// <code>
///   (cellular rule width steps seed)                            ; 1D elementary CA → Sequence
///   (cellularSeeded rule width steps seed initialPattern)       ; 1D CA with explicit Array[Bool] seed
///   (life width height steps seed)                              ; 2D Game of Life → Array[Sequence]
/// </code>
/// </para>
///
/// <para>
/// <b>1D CA semantics (RESEARCH §Pattern 4):</b> Wolfram-convention default —
/// the initial row is a single 1 at column <c>width/2</c>. The <c>seed</c>
/// arg is accepted for signature uniformity with REQ wording but ignored
/// for the default; use <c>cellularSeeded</c> with an explicit
/// <c>Array[Bool]</c> initial pattern to override. Rule lookup: the
/// 3-neighbor (left, center, right) state of the previous row's cell at
/// position <c>i</c> packs into a 3-bit pattern in <c>[0, 7]</c>, and the
/// new cell is <c>(rule &gt;&gt; pattern) &amp; 1</c>. Wrap-around at the
/// row boundaries.
/// </para>
///
/// <para>
/// <b>Grid → Sequence mapping (1D):</b> each row of the grid becomes a
/// <see cref="BarData"/>; each <c>true</c> cell becomes a C4 note at the
/// column's onset position, each <c>false</c> cell becomes a rest. Note
/// duration is the <c>NoteValueType</c> slot closest to <c>1/width</c> of a
/// whole note — width=16 → sixteenth, width=8 → eighth, width=4 → quarter,
/// etc. Non-power-of-2 widths round to the nearest power-of-2 slot.
/// </para>
///
/// <para>
/// <b>2D Life semantics:</b> Moore neighborhood (8 neighbors) with
/// wrap-around. Birth on exactly 3 neighbors, survival on 2 or 3 — Conway's
/// classic ruleset. Initial fill: <c>new Random(seed)</c> stamps each cell
/// alive with probability 0.30 (RESEARCH §Pattern 4). The composer's
/// supplied seed flows directly to <c>new Random</c> — no PrngRegistry
/// routing because REQ signatures REQUIRE the seed arg explicitly. The line
/// bears the <c>// PRNG-SANCTIONED:</c> marker per the Plan 36-06
/// convention.
/// </para>
///
/// <para>
/// <b>Grid → Array[Sequence] mapping (2D):</b> each row index 0..height-1
/// becomes a <see cref="SequenceData"/> in the output array; each step
/// 0..steps-1 becomes a <see cref="BarData"/> within that sequence; each
/// column becomes a note (alive → C4, dead → rest) at the column's onset
/// position. Higher row indices map to lower pitches (row 0 = C5,
/// row height-1 = a few octaves lower) — composer's eye reads "top of
/// the matrix = high pitches" which matches a piano-roll visualization.
/// </para>
///
/// <para>
/// <b>Charitable interpretation (D-v1.5-05 + Pitfall 2):</b>
/// <list type="bullet">
///   <item>rule outside <c>[0, 255]</c> → wrap via <c>(rule &amp; 0xFF)</c> + advisory</item>
///   <item>width / height / steps <c>&lt;= 0</c> → return empty + advisory</item>
///   <item>width / height / steps <c>&gt; 1024</c> → clamp + advisory (T-36-19 DoS guard)</item>
/// </list>
/// </para>
/// </summary>
public static class CellularFunctions
{
    // ====================================================================
    // Constants — DoS guards (T-36-19)
    // ====================================================================

    /// <summary>
    /// T-36-19 DoS guard. width × height × steps must not produce more than
    /// a few hundred million cell evaluations. Per-dimension cap of 1024 means
    /// the worst-case 1D grid is 1024 columns × 1024 steps = 1M cells; the
    /// worst-case 2D grid is 1024 × 1024 × 1024 = 1G cells (acceptable headroom
    /// for unusual composer experiments, but the cap rejects runaway requests).
    /// </summary>
    internal const int MaxDimension = 1024;

    // ====================================================================
    // Registration entry point
    // ====================================================================

    public static void RegisterContextDependent(
        InternalFunctionRegistry registry,
        ExecutionContext context)
    {
        // ---- cellular (1D, single-1-center default seed pattern) ----
        var cellularSig = new FunctionSignature("cellular",
            [IntType.Instance, IntType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["rule", "width", "steps", "seed"]);
        registry.Register("cellular", cellularSig, args => Cellular(args, context));

        // ---- cellularSeeded (1D, explicit Array[Bool] initial pattern) ----
        var cellularSeededSig = new FunctionSignature("cellularSeeded",
            [IntType.Instance, IntType.Instance, IntType.Instance, IntType.Instance,
             new ArrayType(BoolType.Instance)],
            ParameterNames: ["rule", "width", "steps", "seed", "initialPattern"]);
        registry.Register("cellularSeeded", cellularSeededSig,
            args => CellularSeeded(args, context));

        // ---- life (2D Game of Life) ----
        var lifeSig = new FunctionSignature("life",
            [IntType.Instance, IntType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["width", "height", "steps", "seed"]);
        registry.Register("life", lifeSig, args => Life(args, context));
    }

    // ====================================================================
    // 1D cellular automaton
    // ====================================================================

    private static Value Cellular(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        int rule = args[0].As<int>();
        int width = args[1].As<int>();
        int steps = args[2].As<int>();
        // seed is accepted for signature uniformity (REQ wording requires it) but
        // ignored for the single-1-center default — composer uses cellularSeeded
        // to override.
        // int seed = args[3].As<int>();

        rule = WrapRuleWithAdvisory(rule, ctx, "cellular");
        width = ClampDimensionWithAdvisory(width, ctx, "cellular", "width");
        steps = ClampDimensionWithAdvisory(steps, ctx, "cellular", "steps");

        if (width <= 0 || steps <= 0)
            return Value.Sequence(new SequenceData());

        // Default initial row: single 1 at center.
        var initial = new bool[width];
        initial[width / 2] = true;

        var grid = RunElementaryCa(rule, initial, steps);
        return Value.Sequence(Ca1dGridToSequence(grid, width));
    }

    private static Value CellularSeeded(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        int rule = args[0].As<int>();
        int width = args[1].As<int>();
        int steps = args[2].As<int>();
        // seed ignored (purely deterministic given the explicit initialPattern).
        var initialPattern = args[4].As<IReadOnlyList<Value>>();

        rule = WrapRuleWithAdvisory(rule, ctx, "cellularSeeded");
        width = ClampDimensionWithAdvisory(width, ctx, "cellularSeeded", "width");
        steps = ClampDimensionWithAdvisory(steps, ctx, "cellularSeeded", "steps");

        if (width <= 0 || steps <= 0)
            return Value.Sequence(new SequenceData());

        // Build the initial row from the composer-supplied pattern. If the
        // pattern is shorter than width, pad with false; if longer, truncate.
        var initial = new bool[width];
        int len = Math.Min(width, initialPattern.Count);
        for (int i = 0; i < len; i++)
        {
            if (initialPattern[i].Data is bool b && b)
                initial[i] = true;
        }

        var grid = RunElementaryCa(rule, initial, steps);
        return Value.Sequence(Ca1dGridToSequence(grid, width));
    }

    /// <summary>
    /// Iterates the elementary CA rule over the initial row for
    /// <paramref name="steps"/> total rows (initial counts as row 0, so the
    /// loop runs <c>steps - 1</c> times). Returns the resulting grid as a
    /// list of <c>bool[]</c> arrays, each of length <c>width</c>.
    /// </summary>
    private static List<bool[]> RunElementaryCa(int rule, bool[] initial, int steps)
    {
        var grid = new List<bool[]>(steps);
        grid.Add(initial);
        for (int s = 1; s < steps; s++)
        {
            var prev = grid[s - 1];
            int w = prev.Length;
            var next = new bool[w];
            for (int i = 0; i < w; i++)
            {
                bool l = prev[(i - 1 + w) % w];
                bool c = prev[i];
                bool r = prev[(i + 1) % w];
                int pattern = (l ? 4 : 0) | (c ? 2 : 0) | (r ? 1 : 0);
                next[i] = ((rule >> pattern) & 1) == 1;
            }
            grid.Add(next);
        }
        return grid;
    }

    private static SequenceData Ca1dGridToSequence(List<bool[]> grid, int width)
    {
        var seq = new SequenceData();
        var timeSig = new TimeSignatureData(4, 4);
        int durationValue = NoteDurationFromWidth(width);

        foreach (var row in grid)
        {
            var notes = new List<MusicalNoteData>(width);
            for (int c = 0; c < width; c++)
            {
                if (row[c])
                {
                    // Alive cell → C4 note at this column's onset.
                    notes.Add(new MusicalNoteData(
                        noteName: 'C', octave: 4, alteration: 0,
                        durationValue: durationValue, isRest: false));
                }
                else
                {
                    // Dead cell → rest at this column's onset.
                    notes.Add(new MusicalNoteData(
                        noteName: 'C', octave: 4, alteration: 0,
                        durationValue: durationValue, isRest: true));
                }
            }
            seq.AddBar(new BarData(notes, timeSig));
        }
        return seq;
    }

    // ====================================================================
    // 2D Game of Life
    // ====================================================================

    private static Value Life(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        int width = args[0].As<int>();
        int height = args[1].As<int>();
        int steps = args[2].As<int>();
        int seed = args[3].As<int>();

        width = ClampDimensionWithAdvisory(width, ctx, "life", "width");
        height = ClampDimensionWithAdvisory(height, ctx, "life", "height");
        steps = ClampDimensionWithAdvisory(steps, ctx, "life", "steps");

        if (width <= 0 || height <= 0 || steps <= 0)
            return Value.Array(new List<Value>(), SequenceType.Instance);

        // Seeded random fill at 30% density. The composer's seed flows
        // directly to new Random — no PrngRegistry routing because the
        // REQ signature REQUIRES the seed arg.
        var rng = new Random(seed); // PRNG-SANCTIONED: explicit-seed REQ contract per D-36-08
        var initial = new bool[height][];
        for (int r = 0; r < height; r++)
        {
            initial[r] = new bool[width];
            for (int c = 0; c < width; c++)
                initial[r][c] = rng.NextDouble() < 0.30;
        }

        var grid = RunGameOfLife(initial, height, width, steps);
        return Value.Array(Life2dGridToArrayOfSequences(grid, height, width, steps), SequenceType.Instance);
    }

    /// <summary>
    /// Iterates Conway's Game of Life over the initial grid for
    /// <paramref name="steps"/> total steps (initial counts as step 0).
    /// Moore neighborhood (8 neighbors) with wrap-around; birth on exactly
    /// 3 neighbors, survival on 2 or 3.
    /// </summary>
    private static List<bool[][]> RunGameOfLife(bool[][] initial, int height, int width, int steps)
    {
        var grid = new List<bool[][]>(steps);
        grid.Add(initial);
        for (int s = 1; s < steps; s++)
        {
            var prev = grid[s - 1];
            var next = new bool[height][];
            for (int r = 0; r < height; r++)
            {
                next[r] = new bool[width];
                for (int c = 0; c < width; c++)
                {
                    int n = CountAliveNeighbors(prev, r, c, height, width);
                    bool alive = prev[r][c];
                    next[r][c] = alive ? (n == 2 || n == 3) : (n == 3);
                }
            }
            grid.Add(next);
        }
        return grid;
    }

    private static int CountAliveNeighbors(bool[][] grid, int r, int c, int height, int width)
    {
        int count = 0;
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                int rr = ((r + dr) % height + height) % height;
                int cc = ((c + dc) % width + width) % width;
                if (grid[rr][cc]) count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Maps the 2D grid sequence to an Array[Sequence] of length=height.
    /// Each Sequence contains <paramref name="steps"/> bars. Within each bar,
    /// columns 0..width-1 become notes (alive → pitch, dead → rest).
    /// Row index → pitch: row 0 maps to C5 (MIDI 72), descending one
    /// semitone per row (capped at C2 = MIDI 36 for very tall grids).
    /// </summary>
    private static List<Value> Life2dGridToArrayOfSequences(
        List<bool[][]> grid, int height, int width, int steps)
    {
        var timeSig = new TimeSignatureData(4, 4);
        int durationValue = NoteDurationFromWidth(width);
        var result = new List<Value>(height);

        for (int r = 0; r < height; r++)
        {
            var seq = new SequenceData();
            // Pitch: row 0 → MIDI 72 (C5), descending semitone per row;
            // clamp at MIDI 36 (C2) for tall grids.
            int midiPitch = Math.Max(36, 72 - r);
            var (noteName, octave, alteration) = NoteType.FromMidiNote(midiPitch);

            for (int s = 0; s < steps; s++)
            {
                var notes = new List<MusicalNoteData>(width);
                for (int c = 0; c < width; c++)
                {
                    bool alive = grid[s][r][c];
                    notes.Add(new MusicalNoteData(
                        noteName, octave, alteration, durationValue, isRest: !alive));
                }
                seq.AddBar(new BarData(notes, timeSig));
            }
            result.Add(Value.Sequence(seq));
        }
        return result;
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    /// <summary>
    /// Maps a grid width to the nearest NoteValueType enum slot
    /// (<see cref="NoteValueType.Value"/>): width 1 → WHOLE, width 2 → HALF,
    /// width 4 → QUARTER, width 8 → EIGHTH, width 16 → SIXTEENTH, etc.
    /// Non-power-of-2 widths round to the closest power-of-2.
    /// </summary>
    private static int NoteDurationFromWidth(int width)
    {
        if (width <= 1) return (int)NoteValueType.Value.WHOLE;
        // log2(width) rounded to nearest int.
        int log = (int)Math.Round(Math.Log2(width));
        // Clamp into [WHOLE, ONETWENTYEIGHTH] = [0, 7].
        log = Math.Clamp(log, (int)NoteValueType.Value.WHOLE, (int)NoteValueType.Value.ONETWENTYEIGHTH);
        return log;
    }

    private static int WrapRuleWithAdvisory(int rule, ExecutionContext ctx, string siteName)
    {
        int wrapped = rule & 0xFF;
        if (wrapped != rule)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [{siteName}] steps clamped — rule {rule} outside [0, 255], wrapped to {wrapped} at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return wrapped;
            }
            RenderingDiagnostics.WarnOnce(
                $"{siteName}:rule-wrap:{ctx.CurrentCallSite}:{rule}",
                $"[{siteName}] rule {rule} outside [0, 255] at {ctx.CurrentCallSite}; "
                + $"wrapped to {wrapped} via & 0xFF");
        }
        return wrapped;
    }

    private static int ClampDimensionWithAdvisory(int value, ExecutionContext ctx, string siteName, string dimName)
    {
        if (value <= 0)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            // Phase 44 review WR-07: the previous message claimed "clamped to
            // [1, MaxDimension]" but the value was NOT clamped — the strict
            // path returned raw `value` (often 0 or negative). Two fixes:
            // (1) align the message with the actual behavior — caller's
            // downstream guard handles the empty-result charitable case;
            // (2) keep the strict path's return value identical to non-strict
            // so observable behavior is consistent between modes (only the
            // diagnostic noisier under strict).
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [{siteName}] {dimName} must be > 0 — got {dimName}={value} at {ctx.CurrentCallSite}; returning empty result",
                    ctx.CurrentCallSite);
                return value;
            }
            RenderingDiagnostics.WarnOnce(
                $"{siteName}:{dimName}-nonpositive:{ctx.CurrentCallSite}:{value}",
                $"[{siteName}] {dimName} {value} <= 0 at {ctx.CurrentCallSite}; "
                + "returning empty result");
            return value;
        }
        if (value > MaxDimension)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [{siteName}] width/height clamped to [1, {MaxDimension}] — got {dimName}={value} (> {MaxDimension}) at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return MaxDimension;
            }
            RenderingDiagnostics.WarnOnce(
                $"{siteName}:{dimName}-cap:{ctx.CurrentCallSite}:{value}",
                $"[{siteName}] {dimName} {value} > {MaxDimension} at {ctx.CurrentCallSite}; "
                + $"clamped to {MaxDimension} (T-36-19 DoS guard)");
            return MaxDimension;
        }
        return value;
    }
}
