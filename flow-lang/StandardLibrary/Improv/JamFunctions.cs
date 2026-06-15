using System;
using System.Collections.Generic;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
// Disambiguate FlowLang.Runtime.ExecutionContext from System.Threading.ExecutionContext —
// the bare name is ambiguous under net10.0's implicit usings.
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.StandardLibrary.Improv;

/// <summary>
/// Phase 36 Plan 36-11 (IMPROV-01 / D-36-10) — <c>jam</c> chord-aware Markov
/// improvisation backed by composer-overridable Flow-file style packs
/// (registered via <see cref="StyleRegistry"/>).
///
/// <para>
/// <b>Signature (D-36-10):</b>
/// <code>
///   jam(Sequence over, Symbol style = #jazz, Int length = 8, String? key = null,
///       Int? seed = null, Int order = 2) → Sequence
/// </code>
/// Only <c>over</c> is required. Six arity overloads cover the positional
/// composer-call surface, and named-arg dispatch (Phase 36 Plan 36-02) works
/// for a CONTIGUOUS named prefix that matches one of those overloads:
/// <c>(jam over=chords)</c>, <c>(jam over=chords style=#blues)</c>,
/// <c>(jam over=chords style=#blues length=8)</c>, and so on. Skipping a
/// middle parameter — e.g. <c>(jam over=chords style=#blues seed=42)</c>,
/// which omits <c>length</c> and <c>key</c> — does NOT resolve: the
/// OverloadResolver requires the supplied names to exactly cover one
/// overload's parameter list and does not yet default-fill skipped slots. To
/// pass a seed today, either go fully positional —
/// <c>(jam chords #jazz 8 "Cmajor" 42)</c> — or name the full contiguous
/// prefix up to <c>seed</c>. Sparse middle-skip named args are the v1.6
/// "OverloadResolver relaxation" backlog item (CLAUDE.md § Known status).
/// (Aside: <c>key=</c> as a named-arg label additionally collides with the
/// reserved <c>key</c> musical-context keyword at PARSE time — a separate
/// tracked defect.)
/// </para>
///
/// <para>
/// <b>Algorithm (RESEARCH §Pattern 8):</b>
/// 1. Look up rule pack from <see cref="ExecutionContext.StyleRegistry"/>;
///    unknown style → fall back to <c>#jazz</c> + one-shot stderr advisory.
/// 2. Resolve PRNG: explicit seed → <c>new Random(seed)</c>; unseeded →
///    <see cref="PrngRegistry.GetRandom"/> keyed by
///    <c>(CurrentCallSite, "jam")</c>.
/// 3. Resolve active key: <c>key=</c> override wins, else
///    <c>GetMusicalContext().Key</c>, else fall back to <c>"Cmajor"</c> +
///    one-shot stderr advisory.
/// 4. For each output bar in <c>[0, length)</c>:
///    a. Pick the chord from <c>over.Bars[i % over.Bars.Count]</c>.
///    b. Extract chord-tone MIDI pitches (every non-rest note in that bar).
///    c. Extract scale-tone MIDI pitches at MIDI 60..72 (one octave centred
///       on middle C) from <see cref="ScaleDatabase.GetScaleNotes"/>.
///    d. For each of the 8 eighth-note slots:
///       i.   Classify beat strength (#strong / #weak / #syncopated) per the
///            locked heuristic (4/4-implicit; see SUMMARY for the exact map).
///       ii.  Roulette-pick a category (#chord_tone / #scale_tone / #chromatic_passing)
///            via the pack's <c>#beat_weights</c> for that strength.
///       iii. Within the category, score candidate pitches by
///            <c>#interval_transitions</c> relative to the previous emitted
///            pitch (step_up / step_down / leap_up / leap_down / chromatic /
///            repeat) and roulette-pick.
///       iv.  Apply the pack's <c>#articulation_distribution</c> for that
///            beat strength.
///    e. Build a <see cref="BarData"/> with the 8 chosen notes.
/// 5. Return the constructed <see cref="SequenceData"/>.
/// </para>
///
/// <para>
/// <b>Charitable interpretation (D-v1.5-05 / D-36-08):</b>
/// <list type="bullet">
///   <item>Unknown style → fall back to <c>#jazz</c> + WarnOnce.</item>
///   <item>Empty <c>over</c> → empty Sequence + WarnOnce.</item>
///   <item><c>length &lt;= 0</c> → empty Sequence + WarnOnce.</item>
///   <item><c>order</c> outside [1, 3] → clamp + WarnOnce.</item>
///   <item>No <c>#jazz</c> in registry either → empty Sequence + WarnOnce.</item>
///   <item>Style + key musical incompatibility → WarnOnce + continue
///     rendering (NOT a hard error — D-36-08 Claude's Discretion pick,
///     matches Flow's ergonomics-first goal).</item>
///   <item>Missing pack field → use built-in default for that field + WarnOnce
///     per missing field.</item>
/// </list>
/// </para>
/// </summary>
public static class JamFunctions
{
    /// <summary>Eighth-note slots per bar in the locked v1.5 rhythmic template.</summary>
    private const int BeatSlotsPerBar = 8;

    /// <summary>Lowest MIDI pitch we'll emit (middle C-ish range).</summary>
    private const int MidiBassFloor = 48;   // C3

    /// <summary>Highest MIDI pitch we'll emit.</summary>
    private const int MidiTrebleCeiling = 84; // C6

    /// <summary>Charitable default key when no MusicalContext.Key is set.</summary>
    private const string DefaultKey = "Cmajor";

    /// <summary>
    /// Registers the <c>jam</c> arity overloads against the internal function
    /// registry. Wired from <see cref="FlowLang.Core.FlowEngine"/> alongside
    /// the other Phase 36 generative builtins.
    /// </summary>
    public static void RegisterContextDependent(
        InternalFunctionRegistry registry,
        ExecutionContext context)
    {
        // jam-named-args (0615) — COLLAPSED to a SINGLE 6-param signature with
        // per-parameter defaults (D-36-10 surface). The old six positional
        // arity overloads are gone: with OverloadResolver default-fill, this one
        // signature resolves every documented call form —
        //   (jam chords)                                   positional, 5 defaulted
        //   (jam chords #jazz 8 "Cmajor" 42 2)             fully positional
        //   (jam over=chords style=#jazz seed=42)          sparse middle-skip named
        //   (jam over=chords key="Cmajor" length=4)        sparse named, key= label
        //
        // Default sentinels (D-v1.5-05 charitable):
        //   over   — required (null default → resolver errors if omitted)
        //   style  — #jazz (interned Symbol)
        //   length — 8
        //   key    — Void sentinel → handler treats as "no override; use the
        //            active MusicalContext.Key, else Cmajor + advisory"
        //   seed   — Void sentinel → handler routes through PrngRegistry
        //            (unseeded, two-run cmp-clean preserved)
        //   order  — 2 (clamped to [1, 3] in the handler)
        var sig = new FunctionSignature("jam",
            [SequenceType.Instance, SymbolType.Instance, IntType.Instance,
             StringType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["over", "style", "length", "key", "seed", "order"],
            ParameterDefaults:
            [
                null,                              // over — required
                Value.Symbol("jazz", context),    // style
                Value.Int(8),                      // length
                Value.Void(),                      // key — Void = no override
                Value.Void(),                      // seed — Void = PrngRegistry
                Value.Int(2),                      // order
            ]);
        registry.Register("jam", sig, args => Jam(args, context));
    }

    // ====================================================================
    // jam dispatch — argument extraction + entry validation
    // ====================================================================

    private static Value Jam(
        IReadOnlyList<Value> args,
        ExecutionContext ctx)
    {
        // jam-named-args (0615): the collapsed single 6-param signature always
        // arrives with all six slots materialized — positional + named args
        // first, then OverloadResolver default-fill for the rest (in
        // ExpressionEvaluator). So args is always exactly:
        //   args[0] = over   (Sequence)  — required
        //   args[1] = style  (Symbol)    — default #jazz
        //   args[2] = length (Int)       — default 8
        //   args[3] = key    (String)    — default Void sentinel → no override
        //   args[4] = seed   (Int)       — default Void sentinel → PrngRegistry
        //   args[5] = order  (Int)       — default 2; clamped to [1, 3]
        //
        // The `key` / `seed` slots default to a Void sentinel (rather than a
        // concrete String/Int) precisely so the handler can distinguish
        // "composer omitted it" (→ context-driven / PrngRegistry) from "composer
        // explicitly passed it". A guard keeps the handler safe even if a
        // future caller under-fills (charitable: treat short arg lists as
        // all-default).
        var over = args[0].As<SequenceData>();

        // Default style = #jazz when no Symbol arg supplied.
        Value styleSymbol = args.Count >= 2 && args[1].Type is not VoidType
            ? args[1]
            : Value.Symbol("jazz", ctx);

        // Default length = 8 bars when no Int arg supplied.
        int length = args.Count >= 3 && args[2].Type is not VoidType ? args[2].As<int>() : 8;

        // Optional key override — Void sentinel (the registered default) means
        // "no override; resolve from MusicalContext".
        string? keyOverride = args.Count >= 4 && args[3].Type is not VoidType
            ? args[3].As<string>()
            : null;

        // Optional explicit seed — Void sentinel means "unseeded; route through
        // PrngRegistry" (two-run cmp-clean preserved).
        int? seed = null;
        if (args.Count >= 5 && args[4].Type is not VoidType)
            seed = args[4].As<int>();

        // Optional order — default 2; clamped to [1, 3].
        int order = 2;
        if (args.Count >= 6 && args[5].Type is not VoidType)
        {
            int requestedOrder = args[5].As<int>();
            order = Math.Clamp(requestedOrder, 1, 3);
            if (order != requestedOrder)
            {
                // Phase 44 Plan 44-07 Pattern S3: strict-mode branch. Returns the
                // clamped order after reporting; the caller still gets a valid
                // order so the rest of generation completes.
                if (ctx.CallerStrictMode)
                {
                    ctx.ErrorReporter.ReportError(
                        $"[strict] [jam] order clamped to {order} (got {requestedOrder}) at {ctx.CurrentCallSite}",
                        ctx.CurrentCallSite);
                }
                else
                {
                    RenderingDiagnostics.WarnOnce(
                        $"jam:order-clamp:{ctx.CurrentCallSite}:{requestedOrder}",
                        $"[jam] order {requestedOrder} clamped to {order} at {ctx.CurrentCallSite} "
                        + "(IMPROV-01 limits order to [1, 3])");
                }
            }
        }

        return Value.Sequence(GenerateJam(ctx, over, styleSymbol, length, keyOverride, seed, order));
    }

    /// <summary>
    /// Core jam generation — the algorithm from RESEARCH §Pattern 8. Returns
    /// a charitable empty <see cref="SequenceData"/> on degenerate input
    /// rather than throwing (matches Flow's ergonomics-first goal per
    /// CLAUDE.md).
    /// </summary>
    private static SequenceData GenerateJam(
        ExecutionContext ctx,
        SequenceData over,
        Value styleSymbol,
        int length,
        string? keyOverride,
        int? seed,
        int order)
    {
        var output = new SequenceData();

        // ---- Charitable degenerate input ----
        if (length <= 0)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [jam] length clamped — got {length} (must be > 0) at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return output;
            }
            RenderingDiagnostics.WarnOnce(
                $"jam:invalid-length:{ctx.CurrentCallSite}",
                $"[jam] length {length} must be > 0 at {ctx.CurrentCallSite}; returned empty sequence");
            return output;
        }
        if (over.Bars.Count == 0)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [jam] empty over chord progression at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return output;
            }
            RenderingDiagnostics.WarnOnce(
                $"jam:empty-over:{ctx.CurrentCallSite}",
                $"[jam] `over` is empty at {ctx.CurrentCallSite}; returned empty sequence");
            return output;
        }

        // ---- Style pack lookup with charitable fallback ----
        DictData? pack = LookupStylePack(ctx, styleSymbol);
        if (pack == null)
        {
            // Neither requested style nor #jazz exists in the registry. Engine
            // init failed to load any packs — emit a final advisory and bail.
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [jam] degenerate input — no style packs in registry at {ctx.CurrentCallSite}, returning empty",
                    ctx.CurrentCallSite);
                return output;
            }
            RenderingDiagnostics.WarnOnce(
                $"jam:no-fallback-jazz:{ctx.CurrentCallSite}",
                $"[jam] no style packs in registry at {ctx.CurrentCallSite} — returned empty sequence");
            return output;
        }

        // ---- Resolve PRNG ----
        // Per D-v1.5-06 / D-36-09: explicit seed uses new Random(seed) directly
        // (PRNG-SANCTIONED — this is the only sanctioned new Random in this
        // file; MarkovDeterminismTests-style source-grep gates allow at most 1
        // hit). Unseeded routes through PrngRegistry keyed by (call-site, name).
        Random rng = seed.HasValue
            ? new Random(seed.Value) // PRNG-SANCTIONED: explicit-seed path per D-36-10
            : ctx.PrngRegistry.GetRandom(ctx.CurrentCallSite, "jam");

        // ---- Resolve active key ----
        // keyOverride wins, else GetMusicalContext().Key, else DefaultKey + advisory.
        var musicalCtx = ctx.GetMusicalContext();
        string activeKey = keyOverride ?? musicalCtx.Key ?? DefaultKey;
        if (keyOverride == null && musicalCtx.Key == null)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [jam] no active key — using {DefaultKey} at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
            }
            else
            {
                RenderingDiagnostics.WarnOnce(
                    $"jam:default-key:{ctx.CurrentCallSite}",
                    $"[jam] no active key at {ctx.CurrentCallSite}; using {DefaultKey}");
            }
        }

        // ---- Pre-compute scale-tone pitch classes for the active key ----
        var scaleNotes = ScaleDatabase.GetScaleNotes(activeKey);
        var scalePitchClasses = new HashSet<int>();
        if (scaleNotes != null)
        {
            foreach (var noteName in scaleNotes)
            {
                if (TryNoteNameToPitchClass(noteName, out int pc))
                    scalePitchClasses.Add(pc);
            }
        }
        else
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [jam] roulette fallback — unknown key '{activeKey}' at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
            }
            else
            {
                RenderingDiagnostics.WarnOnce(
                    $"jam:unknown-key:{ctx.CurrentCallSite}:{activeKey}",
                    $"[jam] unknown key '{activeKey}' at {ctx.CurrentCallSite}; "
                    + "scale-tone weight redistributed to chord/chromatic");
            }
        }

        // ---- Style + key musical-incompatibility heuristic advisory ----
        // D-36-08 Claude's Discretion + RESEARCH Pattern 8: when scale_tone_weight is
        // non-zero but the active chord progression uses tones largely OUTSIDE the
        // active key, advise the composer once. We measure heuristically: across all
        // `over` chord tones, count fraction in-key vs out-of-key; >50% out-of-key
        // alongside any non-zero scale_tone weight triggers the advisory.
        AdviseStyleKeyIncompatibilityIfNeeded(ctx, pack, over, scalePitchClasses, styleSymbol, activeKey);

        // ---- Read articulation distribution map (charitable defaults) ----
        Articulation downbeatArt = ResolveArticulationFromPack(pack, "downbeat", Articulation.Legato);
        Articulation offbeatArt = ResolveArticulationFromPack(pack, "offbeat", Articulation.Accent);
        Articulation syncopatedArt = ResolveArticulationFromPack(pack, "syncopated", Articulation.Marcato);

        // ---- Read beat-weight maps (charitable: missing → uniform 0.33 each) ----
        BeatWeights strongW = ResolveBeatWeights(pack, "strong",
            defaultChord: 0.6, defaultScale: 0.3, defaultChromatic: 0.1);
        BeatWeights weakW = ResolveBeatWeights(pack, "weak",
            defaultChord: 0.3, defaultScale: 0.5, defaultChromatic: 0.2);
        BeatWeights syncopatedW = ResolveBeatWeights(pack, "syncopated",
            // Charitable: when #syncopated missing, fall back to #weak's weights.
            defaultChord: weakW.Chord, defaultScale: weakW.Scale, defaultChromatic: weakW.Chromatic);

        // ---- Read interval-transition weights ----
        IntervalWeights intervalW = ResolveIntervalWeights(pack);

        // ---- Default timesig ----
        var timeSig = musicalCtx.TimeSignature ?? new TimeSignatureData(4, 4);

        // ---- Iterate output bars ----
        int previousMidi = -1; // sentinel: no previous note yet (first slot uses uniform pick within category)
        for (int barIdx = 0; barIdx < length; barIdx++)
        {
            var sourceChord = over.Bars[barIdx % over.Bars.Count];
            var chordPitchClasses = ExtractChordPitchClasses(sourceChord);
            // If the source chord has no pitched notes at all, fall back to the scale
            // (e.g., a rest-only bar). Charitable — keeps the surface useful for
            // progressions that mix rests and chords.
            if (chordPitchClasses.Count == 0)
            {
                // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
                if (ctx.CallerStrictMode)
                {
                    ctx.ErrorReporter.ReportError(
                        $"[strict] [jam] degenerate input — `over` bar {barIdx % over.Bars.Count} has no pitched notes at {ctx.CurrentCallSite}",
                        ctx.CurrentCallSite);
                }
                else
                {
                    RenderingDiagnostics.WarnOnce(
                        $"jam:rest-chord-bar:{ctx.CurrentCallSite}:{barIdx % over.Bars.Count}",
                        $"[jam] `over` bar {barIdx % over.Bars.Count} has no pitched notes at {ctx.CurrentCallSite}; "
                        + "improvising on scale tones only for that bar");
                }
            }

            var barNotes = new List<MusicalNoteData>(BeatSlotsPerBar);

            for (int slot = 0; slot < BeatSlotsPerBar; slot++)
            {
                var strength = ClassifyBeatStrength(slot);
                BeatWeights weights = strength switch
                {
                    BeatStrength.Strong => strongW,
                    BeatStrength.Weak => weakW,
                    BeatStrength.Syncopated => syncopatedW,
                    _ => weakW,
                };

                int chosenMidi = PickNote(rng, weights, intervalW, chordPitchClasses,
                    scalePitchClasses, previousMidi);

                // Choose articulation per beat-strength.
                Articulation art = strength switch
                {
                    BeatStrength.Strong => downbeatArt,
                    BeatStrength.Weak => offbeatArt,
                    BeatStrength.Syncopated => syncopatedArt,
                    _ => Articulation.Normal,
                };

                // Build the MusicalNoteData. Eighth-note duration per the locked
                // v1.5 rhythmic_template (RESEARCH Pattern 8). Velocity = default mf
                // (0.63) — composers can post-process with humanize().
                int clampedMidi = Math.Clamp(chosenMidi, 12, 127);
                var (name, octave, alteration) = NoteType.FromMidiNote(clampedMidi);
                barNotes.Add(new MusicalNoteData(
                    name, octave, alteration,
                    durationValue: (int)NoteValueType.Value.EIGHTH,
                    isRest: false,
                    velocity: 0.63,
                    articulation: art));

                previousMidi = chosenMidi;
            }

            output.AddBar(new BarData(barNotes, timeSig));
        }

        return output;
    }

    // ====================================================================
    // Style pack lookup + charitable fallback
    // ====================================================================

    /// <summary>
    /// Resolves the requested style symbol against
    /// <see cref="ExecutionContext.StyleRegistry"/>. On miss, falls back to
    /// <c>#jazz</c> and emits a one-shot stderr advisory. Returns null only
    /// when NEITHER the requested style nor <c>#jazz</c> exists — i.e., engine
    /// init failed to load shipped packs entirely.
    /// </summary>
    private static DictData? LookupStylePack(ExecutionContext ctx, Value styleSymbol)
    {
        if (ctx.StyleRegistry.TryGetValue(styleSymbol, out var pack))
            return pack;

        string requestedName = styleSymbol.Data as string ?? "<unknown>";
        var jazz = Value.Symbol("jazz", ctx);
        if (ctx.StyleRegistry.TryGetValue(jazz, out var jazzPack))
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [jam] unknown style '#{requestedName}' — falling back to #jazz at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
            }
            else
            {
                RenderingDiagnostics.WarnOnce(
                    $"jam:unknown-style:{requestedName}",
                    $"[jam] unknown style '#{requestedName}' — falling back to #jazz");
            }
            return jazzPack;
        }
        return null;
    }

    // ====================================================================
    // Beat-strength classification + structured weight types
    // ====================================================================

    /// <summary>
    /// Beat-strength tag per the locked v1.5 heuristic. 4/4-implicit (matches
    /// the active timesig in the test corpus). 8 eighth-note slots per bar:
    /// <list type="bullet">
    ///   <item>slot 0  → Strong  (downbeat)</item>
    ///   <item>slot 4  → Strong  (mid-bar — beat 3 in 4/4)</item>
    ///   <item>slot 2, 6 → Weak  (beats 2 + 4)</item>
    ///   <item>slot 1, 3, 5, 7 → Syncopated (off-beat eighths)</item>
    /// </list>
    /// </summary>
    private enum BeatStrength { Strong, Weak, Syncopated }

    private static BeatStrength ClassifyBeatStrength(int slot)
    {
        return slot switch
        {
            0 => BeatStrength.Strong,
            4 => BeatStrength.Strong,
            2 => BeatStrength.Weak,
            6 => BeatStrength.Weak,
            _ => BeatStrength.Syncopated,
        };
    }

    private readonly record struct BeatWeights(double Chord, double Scale, double Chromatic);

    private readonly record struct IntervalWeights(
        double StepUp, double StepDown,
        double LeapUp, double LeapDown,
        double Chromatic, double Repeat);

    // ====================================================================
    // Pack field extraction (charitable — missing fields use defaults)
    // ====================================================================

    private static BeatWeights ResolveBeatWeights(
        DictData pack, string strengthName,
        double defaultChord, double defaultScale, double defaultChromatic)
    {
        if (!TryGetSymbolKeyed(pack, "beat_weights", out var beatWeightsDict))
            return new BeatWeights(defaultChord, defaultScale, defaultChromatic);

        if (!TryGetSymbolKeyed(beatWeightsDict, strengthName, out var inner))
            return new BeatWeights(defaultChord, defaultScale, defaultChromatic);

        double chord = TryGetSymbolKeyedDouble(inner, "chord_tone", defaultChord);
        double scale = TryGetSymbolKeyedDouble(inner, "scale_tone", defaultScale);
        double chromatic = TryGetSymbolKeyedDouble(inner, "chromatic_passing", defaultChromatic);
        return new BeatWeights(chord, scale, chromatic);
    }

    private static IntervalWeights ResolveIntervalWeights(DictData pack)
    {
        // Charitable defaults: stepwise motion strongly favoured, leaps rare,
        // chromatic + repeats minor. Roughly matches the jazz baseline.
        const double dStepUp = 0.30, dStepDown = 0.30;
        const double dLeapUp = 0.10, dLeapDown = 0.15;
        const double dChromatic = 0.10, dRepeat = 0.05;

        if (!TryGetSymbolKeyed(pack, "interval_transitions", out var dict))
            return new IntervalWeights(dStepUp, dStepDown, dLeapUp, dLeapDown, dChromatic, dRepeat);

        double stepUp = TryGetSymbolKeyedDouble(dict, "step_up", dStepUp);
        double stepDown = TryGetSymbolKeyedDouble(dict, "step_down", dStepDown);
        double leapUp = TryGetSymbolKeyedDouble(dict, "leap_up", dLeapUp);
        double leapDown = TryGetSymbolKeyedDouble(dict, "leap_down", dLeapDown);
        double chromatic = TryGetSymbolKeyedDouble(dict, "chromatic", dChromatic);
        double repeat = TryGetSymbolKeyedDouble(dict, "repeat", dRepeat);
        return new IntervalWeights(stepUp, stepDown, leapUp, leapDown, chromatic, repeat);
    }

    private static Articulation ResolveArticulationFromPack(
        DictData pack, string strengthName, Articulation defaultArt)
    {
        if (!TryGetSymbolKeyed(pack, "articulation_distribution", out var dict))
            return defaultArt;

        // Look up the strength's articulation symbol from the dict. The value
        // is itself a Symbol like #legato; map by name.
        var key = FindSymbolKey(dict, strengthName);
        if (key == null) return defaultArt;
        if (!dict.Entries.TryGetValue(key, out var val)) return defaultArt;

        if (val.Type is SymbolType && val.Data is string artName)
            return ArticulationFromSymbol(artName);
        return defaultArt;
    }

    private static Articulation ArticulationFromSymbol(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "legato" => Articulation.Legato,
            "tenuto" => Articulation.Tenuto,
            "accent" => Articulation.Accent,
            "marcato" => Articulation.Marcato,
            "staccato" => Articulation.Staccato,
            "sforzando" => Articulation.Sforzando,
            "normal" => Articulation.Normal,
            _ => Articulation.Normal,
        };
    }

    // ----- DictData helpers (Symbol-keyed lookups) ---------------------

    /// <summary>
    /// Walks the dict's Symbol-typed keys and returns true (with the matching
    /// key as <paramref name="key"/>) when one matches <paramref name="name"/>.
    /// </summary>
    private static Value? FindSymbolKey(DictData dict, string name)
    {
        foreach (var kv in dict.Entries)
        {
            if (kv.Key.Type is SymbolType && kv.Key.Data is string s && s == name)
                return kv.Key;
        }
        return null;
    }

    /// <summary>
    /// Looks up a Symbol-keyed entry in <paramref name="dict"/> by symbol
    /// <paramref name="name"/>. Returns true when found AND the value is itself
    /// a Dict (the rule-pack's nested-dict shape).
    /// </summary>
    private static bool TryGetSymbolKeyed(DictData dict, string name, out DictData inner)
    {
        var key = FindSymbolKey(dict, name);
        if (key == null) { inner = null!; return false; }
        if (!dict.Entries.TryGetValue(key, out var val)) { inner = null!; return false; }
        if (val.Data is DictData d) { inner = d; return true; }
        inner = null!; return false;
    }

    /// <summary>
    /// Looks up a Symbol-keyed numeric weight in <paramref name="dict"/>.
    /// Returns <paramref name="defaultValue"/> when the key is missing OR the
    /// value isn't numeric. Charitable.
    /// </summary>
    private static double TryGetSymbolKeyedDouble(DictData dict, string name, double defaultValue)
    {
        var key = FindSymbolKey(dict, name);
        if (key == null) return defaultValue;
        if (!dict.Entries.TryGetValue(key, out var val)) return defaultValue;
        return ToDouble(val) ?? defaultValue;
    }

    private static double? ToDouble(Value v)
    {
        if (v.Data is double d) return d;
        if (v.Data is float f) return (double)f;
        if (v.Data is int i) return (double)i;
        if (v.Data is long l) return (double)l;
        return null;
    }

    // ====================================================================
    // Chord-tone extraction from a `over` bar
    // ====================================================================

    /// <summary>
    /// Extracts the set of chord-tone pitch classes from a single bar of the
    /// `over` Sequence. The bar is expected to contain a single chord literal
    /// (Cmaj7, V7, etc.) compiled by NoteStreamCompiler into multiple
    /// MusicalNoteData entries — every non-rest pitched note contributes its
    /// pitch class (mod 12) to the set.
    /// </summary>
    private static HashSet<int> ExtractChordPitchClasses(BarData bar)
    {
        var pcs = new HashSet<int>();
        foreach (var note in bar.MusicalNotes)
        {
            if (note.IsRest) continue;
            // Defensively skip notes with invalid name characters (shouldn't
            // happen for ChordParser-emitted notes but charitable input is
            // cheap here).
            try
            {
                int midi = NoteType.ToMidiNote(note.NoteName, note.Octave, note.Alteration);
                pcs.Add(((midi % 12) + 12) % 12);
            }
            catch { /* skip non-note entry */ }
        }
        return pcs;
    }

    /// <summary>
    /// Converts a chord/scale note-name string (e.g., "C", "Cs", "F", "Bf") to
    /// its 0..11 pitch class. Uses ChordParser's NoteToSemitone mapping shape;
    /// since that mapping is private, we hand-roll the inverse here.
    /// </summary>
    private static bool TryNoteNameToPitchClass(string noteName, out int pc)
    {
        pc = 0;
        if (string.IsNullOrEmpty(noteName)) return false;

        // First char = letter A-G.
        char letter = char.ToUpperInvariant(noteName[0]);
        int basePc = letter switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => -1,
        };
        if (basePc < 0) return false;

        // Optional accidental modifier — 's' / 'S' / '#' = +1 ; 'f' / 'F' / 'b' = -1.
        if (noteName.Length >= 2)
        {
            char acc = noteName[1];
            if (acc == 's' || acc == 'S' || acc == '#') basePc += 1;
            else if (acc == 'f' || acc == 'F' || acc == 'b') basePc -= 1;
        }

        pc = ((basePc % 12) + 12) % 12;
        return true;
    }

    // ====================================================================
    // Note picking — category roulette + interval-transition bias
    // ====================================================================

    /// <summary>
    /// Picks a MIDI pitch for the current beat slot:
    /// <list type="number">
    ///   <item>Roulette-pick a category (chord_tone / scale_tone / chromatic_passing)
    ///     per the active beat-strength's pack weights.</item>
    ///   <item>Build a candidate-pitch pool in the working pitch range
    ///     <c>[MidiBassFloor, MidiTrebleCeiling]</c> matching the chosen
    ///     category.</item>
    ///   <item>Within the pool, weight each candidate by its interval direction
    ///     relative to <paramref name="previousMidi"/> and roulette-pick.</item>
    /// </list>
    /// When <paramref name="previousMidi"/> is -1 (first slot, no prior pitch),
    /// the candidate pool is sampled uniformly.
    /// </summary>
    private static int PickNote(
        Random rng, BeatWeights bw, IntervalWeights iw,
        HashSet<int> chordPCs, HashSet<int> scalePCs,
        int previousMidi)
    {
        // Roulette-pick a category. Charitable: zero total weight → default to
        // chord-tone equal preference. Negative weights treated as zero (no advisory —
        // composers tweak weights live; charity beats spam).
        double wChord = Math.Max(0.0, bw.Chord);
        double wScale = Math.Max(0.0, bw.Scale);
        double wChrom = Math.Max(0.0, bw.Chromatic);
        double total = wChord + wScale + wChrom;

        Category category;
        if (total <= 0.0)
        {
            category = Category.ChordTone; // last-resort fallback
        }
        else
        {
            double draw = rng.NextDouble() * total;
            if (draw < wChord) category = Category.ChordTone;
            else if (draw < wChord + wScale) category = Category.ScaleTone;
            else category = Category.ChromaticPassing;
        }

        // Build candidate pool per category. Each candidate is a MIDI pitch in
        // [MidiBassFloor, MidiTrebleCeiling]. If a category has no candidates
        // (e.g., empty chord), fall back through ChordTone → ScaleTone →
        // Chromatic → middle C.
        var candidates = BuildCandidatePool(category, chordPCs, scalePCs);
        if (candidates.Count == 0 && category != Category.ChromaticPassing)
        {
            candidates = BuildCandidatePool(Category.ChromaticPassing, chordPCs, scalePCs);
        }
        if (candidates.Count == 0)
        {
            return 60; // middle C — last resort
        }

        // Uniform pick if no previous pitch to bias against.
        if (previousMidi < 0)
        {
            return candidates[rng.Next(candidates.Count)];
        }

        // Weight each candidate by interval-transition.
        var transitionWeights = new double[candidates.Count];
        double sum = 0.0;
        for (int i = 0; i < candidates.Count; i++)
        {
            transitionWeights[i] = ScoreIntervalTransition(iw, candidates[i] - previousMidi);
            sum += transitionWeights[i];
        }

        if (sum <= 0.0)
        {
            return candidates[rng.Next(candidates.Count)];
        }

        double pickDraw = rng.NextDouble() * sum;
        double accum = 0.0;
        for (int i = 0; i < candidates.Count; i++)
        {
            accum += transitionWeights[i];
            if (pickDraw < accum) return candidates[i];
        }
        return candidates[^1];
    }

    private enum Category { ChordTone, ScaleTone, ChromaticPassing }

    /// <summary>
    /// Builds the MIDI candidate pool for a category. Chord-tones = pitches in
    /// the chord-PC set. Scale-tones = pitches in the scale-PC set MINUS chord
    /// tones (composers explicitly want scale-NOT-chord tones to be a distinct
    /// surface — chord weight handles in-chord picks). Chromatic-passing =
    /// pitches NOT in chord OR scale.
    /// </summary>
    private static List<int> BuildCandidatePool(
        Category category, HashSet<int> chordPCs, HashSet<int> scalePCs)
    {
        var pool = new List<int>();
        for (int midi = MidiBassFloor; midi <= MidiTrebleCeiling; midi++)
        {
            int pc = ((midi % 12) + 12) % 12;
            bool inChord = chordPCs.Contains(pc);
            bool inScale = scalePCs.Contains(pc);

            bool match = category switch
            {
                Category.ChordTone => inChord,
                Category.ScaleTone => inScale && !inChord,
                Category.ChromaticPassing => !inChord && !inScale,
                _ => false,
            };
            if (match) pool.Add(midi);
        }
        return pool;
    }

    /// <summary>
    /// Returns the interval-transition weight for the given semitone delta.
    /// Classification:
    /// <list type="bullet">
    ///   <item>delta == 0 → Repeat</item>
    ///   <item>delta == +1 or delta == -1 → Chromatic (the pack's bent-note knob)</item>
    ///   <item>delta == +2 → StepUp</item>
    ///   <item>delta == -2 → StepDown</item>
    ///   <item>+3..+12 → LeapUp</item>
    ///   <item>-3..-12 → LeapDown</item>
    ///   <item>|delta| > 12 → 0.0 (out-of-range; effectively discouraged)</item>
    /// </list>
    /// Note: ±1 maps to Chromatic (not StepUp/StepDown) because the rule-pack
    /// shape treats #chromatic as the single-semitone passing-tone slot per
    /// the README; ±2 is the canonical scale-step ("step_up"/"step_down").
    /// </summary>
    private static double ScoreIntervalTransition(IntervalWeights iw, int delta)
    {
        if (delta == 0) return Math.Max(0.0, iw.Repeat);
        int abs = Math.Abs(delta);
        if (abs == 1) return Math.Max(0.0, iw.Chromatic);
        if (abs == 2) return delta > 0 ? Math.Max(0.0, iw.StepUp) : Math.Max(0.0, iw.StepDown);
        if (abs <= 12) return delta > 0 ? Math.Max(0.0, iw.LeapUp) : Math.Max(0.0, iw.LeapDown);
        return 0.0;
    }

    // ====================================================================
    // Style+key musical-incompatibility advisory (D-36-08)
    // ====================================================================

    /// <summary>
    /// Heuristic: if the pack has any non-zero <c>#scale_tone</c> weight AND
    /// the `over` chord progression's pitch classes are mostly outside the
    /// active key's scale, emit a one-shot stderr advisory. Charitable
    /// (NOT a hard error) — matches D-36-08 Claude's Discretion pick.
    /// </summary>
    private static void AdviseStyleKeyIncompatibilityIfNeeded(
        ExecutionContext ctx, DictData pack, SequenceData over,
        HashSet<int> scalePCs, Value styleSymbol, string activeKey)
    {
        // Fast-fail when we couldn't resolve any scale tones — already advised.
        if (scalePCs.Count == 0) return;

        // Fast-fail when the pack has zero scale_tone weight everywhere — composer
        // never asked for in-key bias, so incompatibility is moot.
        BeatWeights anyScaleWeight = ResolveBeatWeights(pack, "strong",
            defaultChord: 0, defaultScale: 0, defaultChromatic: 0);
        BeatWeights anyScaleWeightWeak = ResolveBeatWeights(pack, "weak",
            defaultChord: 0, defaultScale: 0, defaultChromatic: 0);
        if (anyScaleWeight.Scale <= 0.0 && anyScaleWeightWeak.Scale <= 0.0) return;

        // Count chord-progression pitch classes that are out-of-key.
        int total = 0, outOfKey = 0;
        foreach (var bar in over.Bars)
        {
            var pcs = ExtractChordPitchClasses(bar);
            foreach (int pc in pcs)
            {
                total++;
                if (!scalePCs.Contains(pc)) outOfKey++;
            }
        }
        if (total == 0) return;

        double outFrac = (double)outOfKey / total;
        if (outFrac > 0.5)
        {
            string styleName = styleSymbol.Data as string ?? "<unknown>";
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [jam] key/style mismatch — '#{styleName}' + '{activeKey}' may produce unexpected harmonic flavor ({outOfKey}/{total} chord tones outside key) at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
            }
            else
            {
                RenderingDiagnostics.WarnOnce(
                    $"jam:style-key-mismatch:{styleName}:{activeKey}",
                    $"[jam] style '#{styleName}' + key '{activeKey}' may produce unexpected "
                    + $"harmonic flavor ({outOfKey}/{total} chord tones outside key)");
            }
        }
    }
}
