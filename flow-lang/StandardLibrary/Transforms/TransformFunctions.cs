using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Transforms;

/// <summary>
/// Registers pattern transform functions for sequences: transpose, invert, retrograde,
/// augment, diminish, up, down, repeat, and concat.
/// </summary>
public static class TransformFunctions
{
    private const int MIDI_MIN = 16;  // E0
    private const int MIDI_MAX = 136; // E10

    public static void Register(InternalFunctionRegistry registry)
    {
        RegisterTranspose(registry);
        RegisterInvert(registry);
        RegisterRetrograde(registry);
        RegisterAugmentDiminish(registry);
        RegisterOctaveShift(registry);
        RegisterRepeat(registry);
        RegisterConcat(registry);
        // Phase 44 Plan 44-05: crescendo / decrescendo / swell / ritardando /
        // accelerando / humanize / humanizeGaussian / tremolo registrations are
        // owned by RegisterContextDependent so each per-arg clamp site can read
        // context.CallerStrictMode at the leaf. They are NOT registered here —
        // RegisterSignaturesOnly + RegisterAllImplementations both invoke
        // RegisterContextDependentFunctions which routes through
        // TransformFunctions.RegisterContextDependent, so the LSP signature path
        // and runtime path both pick up the context-aware delegates.
        // (The legacy non-context-dep Crescendo/Decrescendo/Swell/RitardandoTransform/
        // AccelerandoTransform/Humanize/HumanizeGaussian/Tremolo private methods
        // were deleted Phase 44 Plan 44-05 — the strict-aware *Strict wrappers
        // delegate directly to the extracted *Core helpers.)
        // Fermata stays non-context-dep (no input-perimeter clamp on its arg).
        RegisterFermata(registry);
        RegisterOrnamentTransforms(registry);
    }

    /// <summary>
    /// Phase 22 DX-14 (plan 22-06): registers <c>legato(Sequence, Double)</c> and
    /// <c>portamento(Sequence, Millisecond)</c> articulation transforms. Both set per-note
    /// defaulted-parameter fields (<see cref="MusicalNoteData.DurationOverlap"/>,
    /// <see cref="MusicalNoteData.PortamentoMs"/>) consumed by <c>BarRenderer</c> and
    /// <c>MidiExport</c> at render time.
    ///
    /// Per CONTEXT D-02 + Pitfall 3: onsets are NOT moved. The transforms only stamp
    /// fields onto each note; <c>bar.ToTimeline()</c> already produced the onset positions
    /// before render time. The audio renderer extends durationBeats; MIDI export emits
    /// extended NoteOff + CC65/CC5 bracket events.
    ///
    /// Per CONTEXT line 18 (rollback-independence): each transform calls
    /// <c>note.With(...)</c> naming ONLY the field this plan owns. Sibling 22-05's
    /// <c>OnsetOffset</c> and the other 22-06 slot are preserved by the builder helper's
    /// null-coalesce, so neither transform enumerates fields it doesn't own.
    /// </summary>
    public static void RegisterArticulationTransforms(InternalFunctionRegistry registry)
    {
        // legato(Sequence, Double) -> Sequence
        var legatoSig = new FunctionSignature("legato",
            [SequenceType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "overlap"]);
        registry.Register("legato", legatoSig, args =>
        {
            var seq = args[0].As<SequenceData>();
            double overlap = args[1].As<double>();
            return Value.Sequence(TransformNotes(seq, note =>
                note.With(durationOverlap: overlap)));
        });

        // portamento(Sequence, Millisecond) -> Sequence
        var portamentoSig = new FunctionSignature("portamento",
            [SequenceType.Instance, MillisecondType.Instance],
            ParameterNames: ["seq", "glideMs"]);
        registry.Register("portamento", portamentoSig, args =>
        {
            var seq = args[0].As<SequenceData>();
            double ms = args[1].As<double>();   // Millisecond is backed by double
            return Value.Sequence(TransformNotes(seq, note =>
                note.With(portamentoMs: ms)));
        });
    }

    /// <summary>
    /// Phase 22 DX-13: registers <c>quantize(Sequence, NoteValue, strength, swing)</c> which
    /// reads <see cref="MusicalContext.TimeSignature"/> from the active context for grid alignment.
    ///
    /// Per Pitfall 9 (CRITICAL byte-identical regression gate): <c>strength=0</c> + <c>swing=0</c>
    /// short-circuits to identity (returns the input <c>SequenceData</c> unchanged) BEFORE any
    /// allocation. A single byte difference at strength=0 would break the ByteIdenticalTutorial /
    /// ByteIdenticalShowcase / EuclideanByteIdentical regression gate.
    ///
    /// Per V5 input validation (T-22-V5-17, T-22-V5-18) and CLAUDE.md charitable interpretation
    /// memory: strength is clamped to [0, 1] and swing is clamped to [-1, 1]. Out-of-range inputs
    /// are silently corrected — no exception, no error.
    ///
    /// Per CONTEXT D-04..D-06: linear swing offset = swing × (subdivBeats / 2), signed
    /// (positive = drag offbeat later, negative = push earlier), applied to every other
    /// subdivision at the requested resolution.
    /// </summary>
    public static void RegisterContextDependent(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
        var quantizeSig = new FunctionSignature("quantize",
            [SequenceType.Instance, NoteValueType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "resolution", "strength", "swing"]);
        registry.Register("quantize", quantizeSig, args =>
        {
            var seq = args[0].As<SequenceData>();
            int resEnum = args[1].As<int>();
            // Phase 44 Plan 44-05 (D-06/D-07): strict-mode promotes the V5 charitable
            // clamps to ErrorReporter errors. Non-strict path stays byte-identical to
            // the pre-Phase-44 Math.Clamp + fallback shape (Pitfall 5 two-run cmp-clean).
            double strengthRaw = args[2].As<double>();
            double swingRaw = args[3].As<double>();
            double strength;
            double swing;
            if (context.CallerStrictMode)
            {
                if (strengthRaw < 0.0 || strengthRaw > 1.0)
                {
                    context.ErrorReporter.ReportError(
                        $"[strict] quantize strength {strengthRaw} outside [0.0, 1.0]",
                        context.CurrentCallSite);
                    return Value.Void();
                }
                if (swingRaw < -1.0 || swingRaw > 1.0)
                {
                    context.ErrorReporter.ReportError(
                        $"[strict] quantize swing {swingRaw} outside [-1.0, 1.0]",
                        context.CurrentCallSite);
                    return Value.Void();
                }
                strength = strengthRaw;
                swing = swingRaw;
            }
            else
            {
                // V5: clamp out-of-range inputs (charitable D-07; threats T-22-V5-17, T-22-V5-18).
                strength = Math.Clamp(strengthRaw, 0.0, 1.0);
                swing = Math.Clamp(swingRaw, -1.0, 1.0);
            }

            // Pitfall 9 — byte-identical regression gate. strength=0 + swing=0 MUST return the
            // input sequence object reference unchanged, or the ByteIdentical gate breaks.
            if (strength == 0.0 && swing == 0.0) return Value.Sequence(seq);

            var timesig = context.GetMusicalContext().TimeSignature
                ?? new TimeSignatureData(4, 4);
            return Value.Sequence(QuantizeSequence(seq, (NoteValueType.Value)resEnum,
                strength, swing, timesig));
        });

        // Phase 44 Plan 44-05 (D-06/D-07): re-register the dynamic / tempo / humanize /
        // ornament transforms via context-capturing closures so their per-arg clamp
        // checks can read context.CallerStrictMode at the leaf site. Non-strict path
        // is byte-identical to the pre-Phase-44 Math.Clamp shape (Pitfall 5).
        RegisterDynamicTransformsContextDependent(registry, context);
        RegisterTempoTransformsContextDependent(registry, context);
        RegisterHumanizeContextDependent(registry, context);
        RegisterHumanizeGaussianContextDependent(registry, context);
        RegisterTremoloContextDependent(registry, context);
    }

    // ===== Phase 44 Plan 44-05 context-dependent re-registrations =====

    /// <summary>
    /// Phase 44 Plan 44-05 — re-register crescendo / decrescendo / swell with a
    /// context-capturing closure so their per-arg [0.0, 1.0] velocity clamps can
    /// promote to <c>[strict] &lt;builtin&gt; &lt;param&gt; ... outside [0.0, 1.0]</c>
    /// when <see cref="ExecutionContext.CallerStrictMode"/> is true. The
    /// non-context-dependent <see cref="RegisterDynamicTransforms"/> still runs
    /// first via <see cref="Register"/>; this method overlays the same names
    /// with context-aware closures via the registry's last-write-wins overload
    /// lookup (the runtime resolver picks the most-recently-registered impl
    /// matching the same signature shape).
    /// </summary>
    private static void RegisterDynamicTransformsContextDependent(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
        var crescSig = new FunctionSignature("crescendo",
            [SequenceType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "startVel", "endVel"]);
        registry.Register("crescendo", crescSig, args => CrescendoStrict(args, context));

        var decrescSig = new FunctionSignature("decrescendo",
            [SequenceType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "startVel", "endVel"]);
        registry.Register("decrescendo", decrescSig, args => DecrescendoStrict(args, context));

        var swellSig = new FunctionSignature("swell",
            [SequenceType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "edgeVel", "peakVel"]);
        registry.Register("swell", swellSig, args => SwellStrict(args, context));
    }

    private static void RegisterTempoTransformsContextDependent(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
        var ritSig = new FunctionSignature("ritardando",
            [SequenceType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "amount"]);
        registry.Register("ritardando", ritSig, args => RitardandoStrict(args, context));

        var accelSig = new FunctionSignature("accelerando",
            [SequenceType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "amount"]);
        registry.Register("accelerando", accelSig, args => AccelerandoStrict(args, context));
    }

    private static void RegisterHumanizeContextDependent(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
        var humanizeSig = new FunctionSignature("humanize",
            [SequenceType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "amount"]);
        registry.Register("humanize", humanizeSig, args => HumanizeStrict(args, context));
    }

    private static void RegisterHumanizeGaussianContextDependent(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
        var sig = new FunctionSignature("humanizeGaussian",
            [SequenceType.Instance, DoubleType.Instance, IntType.Instance],
            ParameterNames: ["seq", "amount", "seed"]);
        registry.Register("humanizeGaussian", sig, args => HumanizeGaussianStrict(args, context));
    }

    private static void RegisterTremoloContextDependent(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
        var tremSig = new FunctionSignature("tremolo",
            [SequenceType.Instance, IntType.Instance],
            ParameterNames: ["seq", "reps"]);
        registry.Register("tremolo", tremSig, args => TremoloStrict(args, context));
    }

    // ===== Phase 44 Plan 44-05 strict-mode-aware implementations =====
    //
    // Each method follows the RESEARCH §"Axis B Site Rewrite" template:
    //   1. Extract raw arg(s) into local var(s).
    //   2. If ctx.CallerStrictMode:
    //        - For each out-of-range raw, ReportError "[strict] <builtin> <param>
    //          {raw} outside [lo, hi]" + return Value.Void() (early return; report
    //          ONE error per call to keep diagnostics minimal).
    //        - Otherwise pass raw values through (no clamp needed).
    //   3. Else: existing Math.Clamp + fallback path verbatim (preserves the
    //      pre-Phase-44 byte-identical non-strict shape per Pitfall 5).
    //
    // Error strings match the strict-error-manifest.csv §6a rows verbatim
    // (D-07 + AUDIT §6a Column 5 composer-approved 2026-05-24). NO PRNG /
    // DateTime / Guid in the message — preserves two-run cmp-clean determinism.

    private static Value CrescendoStrict(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext ctx)
    {
        var seq = args[0].As<SequenceData>();
        double startRaw = args[1].As<double>();
        double endRaw = args[2].As<double>();
        if (ctx.CallerStrictMode)
        {
            if (startRaw < 0.0 || startRaw > 1.0)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] crescendo startVel {startRaw} outside [0.0, 1.0]",
                    ctx.CurrentCallSite);
                return Value.Void();
            }
            if (endRaw < 0.0 || endRaw > 1.0)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] crescendo endVel {endRaw} outside [0.0, 1.0]",
                    ctx.CurrentCallSite);
                return Value.Void();
            }
            return Value.Sequence(ApplyVelocityGradient(seq, startRaw, endRaw));
        }
        double startVel = Math.Clamp(startRaw, 0.0, 1.0);
        double endVel = Math.Clamp(endRaw, 0.0, 1.0);
        return Value.Sequence(ApplyVelocityGradient(seq, startVel, endVel));
    }

    private static Value DecrescendoStrict(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext ctx)
    {
        var seq = args[0].As<SequenceData>();
        double startRaw = args[1].As<double>();
        double endRaw = args[2].As<double>();
        if (ctx.CallerStrictMode)
        {
            if (startRaw < 0.0 || startRaw > 1.0)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] decrescendo startVel {startRaw} outside [0.0, 1.0]",
                    ctx.CurrentCallSite);
                return Value.Void();
            }
            if (endRaw < 0.0 || endRaw > 1.0)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] decrescendo endVel {endRaw} outside [0.0, 1.0]",
                    ctx.CurrentCallSite);
                return Value.Void();
            }
            // Reverse the velocity gradient: decrescendo goes from endVel down to startVel
            return Value.Sequence(ApplyVelocityGradient(seq, endRaw, startRaw));
        }
        double startVel = Math.Clamp(startRaw, 0.0, 1.0);
        double endVel = Math.Clamp(endRaw, 0.0, 1.0);
        return Value.Sequence(ApplyVelocityGradient(seq, endVel, startVel));
    }

    private static Value SwellStrict(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext ctx)
    {
        var seq = args[0].As<SequenceData>();
        double edgeRaw = args[1].As<double>();
        double peakRaw = args[2].As<double>();
        double edgeVel;
        double peakVel;
        if (ctx.CallerStrictMode)
        {
            if (edgeRaw < 0.0 || edgeRaw > 1.0)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] swell edgeVel {edgeRaw} outside [0.0, 1.0]",
                    ctx.CurrentCallSite);
                return Value.Void();
            }
            if (peakRaw < 0.0 || peakRaw > 1.0)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] swell peakVel {peakRaw} outside [0.0, 1.0]",
                    ctx.CurrentCallSite);
                return Value.Void();
            }
            edgeVel = edgeRaw;
            peakVel = peakRaw;
        }
        else
        {
            edgeVel = Math.Clamp(edgeRaw, 0.0, 1.0);
            peakVel = Math.Clamp(peakRaw, 0.0, 1.0);
        }
        return SwellCore(seq, edgeVel, peakVel);
    }

    private static Value RitardandoStrict(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext ctx)
    {
        var seq = args[0].As<SequenceData>();
        double amountRaw = args[1].As<double>();
        double amount;
        if (ctx.CallerStrictMode)
        {
            if (amountRaw < 0.0 || amountRaw > 1.0)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] ritardando amount {amountRaw} outside [0.0, 1.0]",
                    ctx.CurrentCallSite);
                return Value.Void();
            }
            amount = amountRaw;
        }
        else
        {
            amount = Math.Clamp(amountRaw, 0.0, 1.0);
        }
        return RitardandoCore(seq, amount);
    }

    private static Value AccelerandoStrict(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext ctx)
    {
        var seq = args[0].As<SequenceData>();
        double amountRaw = args[1].As<double>();
        double amount;
        if (ctx.CallerStrictMode)
        {
            if (amountRaw < 0.0 || amountRaw > 1.0)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] accelerando amount {amountRaw} outside [0.0, 1.0]",
                    ctx.CurrentCallSite);
                return Value.Void();
            }
            amount = amountRaw;
        }
        else
        {
            amount = Math.Clamp(amountRaw, 0.0, 1.0);
        }
        return AccelerandoCore(seq, amount);
    }

    private static Value HumanizeStrict(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext ctx)
    {
        var seq = args[0].As<SequenceData>();
        double amountRaw = args[1].As<double>();
        double amount;
        if (ctx.CallerStrictMode)
        {
            if (amountRaw < 0.0 || amountRaw > 1.0)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] humanize amount {amountRaw} outside [0.0, 1.0]",
                    ctx.CurrentCallSite);
                return Value.Void();
            }
            amount = amountRaw;
        }
        else
        {
            amount = Math.Clamp(amountRaw, 0.0, 1.0);
        }
        // Sweep 2026-06-14: route the velocity-jitter RNG through PrngRegistry keyed by
        // (CurrentCallSite, "humanize") so the unseeded uniform humanize reseeds at the
        // renderSong/writeWav boundary (ResetAtRenderBoundary) and is byte-identical across
        // two consecutive renders. The old process-global wall-clock `new Random()` made
        // (humanize seq amount) -> writeWav non-deterministic, violating two-run cmp-clean.
        var rng = ctx.PrngRegistry.GetRandom(ctx.CurrentCallSite, "humanize");
        return HumanizeCore(seq, amount, rng);
    }

    private static Value HumanizeGaussianStrict(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext ctx)
    {
        var seq = args[0].As<SequenceData>();
        double amountRaw = args[1].As<double>();
        int seed = args[2].As<int>();
        double amount;
        if (ctx.CallerStrictMode)
        {
            if (amountRaw < 0.0 || amountRaw > 1.0)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] humanizeGaussian amount {amountRaw} outside [0.0, 1.0]",
                    ctx.CurrentCallSite);
                return Value.Void();
            }
            amount = amountRaw;
        }
        else
        {
            amount = Math.Clamp(amountRaw, 0.0, 1.0);
        }
        return HumanizeGaussianCore(seq, amount, seed);
    }

    private static Value TremoloStrict(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext ctx)
    {
        var seq = args[0].As<SequenceData>();
        int repsRaw = args[1].As<int>();
        int reps;
        if (ctx.CallerStrictMode)
        {
            if (repsRaw < 1 || repsRaw > 16)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] tremolo reps {repsRaw} outside [1, 16]",
                    ctx.CurrentCallSite);
                return Value.Void();
            }
            reps = repsRaw;
        }
        else
        {
            reps = Math.Clamp(repsRaw, 1, 16);
        }
        return TremoloCore(seq, reps);
    }

    /// <summary>
    /// DX-13 implementation: walks each bar's notes sequentially, computes the nearest grid
    /// target at the requested resolution (with optional swing shift on every other
    /// subdivision), and stores the per-note onset displacement in <c>note.OnsetOffset</c>
    /// via the <c>With(...)</c> builder helper. <c>BarType.ToTimeline</c> later adds this
    /// offset to the emitted onset position so audio renderer + MIDI export both honor
    /// quantization without parallel rebuild paths.
    ///
    /// Uses <c>note.With(onsetOffset: …)</c> rather than the full ctor — this is intentional
    /// per Phase 22 CONTEXT line 18 (independent shippability): future Phase 22 plans
    /// (e.g. 22-06 legato/portamento) will append more defaulted fields, and this transform
    /// must not enumerate fields it doesn't own.
    /// </summary>
    private static SequenceData QuantizeSequence(
        SequenceData seq, NoteValueType.Value resolution,
        double strength, double swing, TimeSignatureData timesig)
    {
        // sweep-0614: the quantize grid is compared against the per-note onset cursor,
        // which accumulates GetBeats in QUARTER-note units. The subdivision length must
        // therefore be quarter-units too (QUARTER = 1.0, EIGHTH = 0.5, SIXTEENTH = 0.25 —
        // independent of the time-signature denominator). In 4/4 this equals the old
        // denominator-unit value, so 4/4 quantize stays byte-identical; in non-4/4 the
        // grid now lines up with the (now quarter-units) timeline.
        double subdivBeats = NoteValueToQuarters(resolution);
        // CONTEXT D-04: linear swing offset = swing × (subdivBeats / 2).
        double swingOffset = swing * (subdivBeats / 2.0);

        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            result.AddBar(QuantizeBar(bar, subdivBeats, swingOffset, strength, timesig));
        }
        return result;
    }

    /// <summary>
    /// Audit 2026-06-09 §4.1: quantizes one bar, recursing into Phase 28 ParallelVoices so
    /// voice-block sequences keep their content (each voice gets its own beat cursor — voices
    /// are parallel, all starting at the bar onset). Before this fix QuantizeSequence rebuilt
    /// <c>new BarData(newNotes, ts)</c> and dropped ParallelVoices, silently muting voiced
    /// sequences. Per-note rebuild stays on <c>note.With(onsetOffset:)</c> (§4.2-clean already).
    /// </summary>
    private static BarData QuantizeBar(
        BarData bar, double subdivBeats, double swingOffset, double strength, TimeSignatureData timesig)
    {
        var newNotes = new List<MusicalNoteData>(bar.MusicalNotes.Count);
        double currentBeat = 0.0;
        int subdivIdx = 0;
        TimeSignatureData barTs = bar.TimeSignature ?? timesig;
        foreach (var note in bar.MusicalNotes)
        {
            double targetGrid = Math.Round(currentBeat / subdivBeats) * subdivBeats;
            // CONTEXT D-06: every other subdivision (the offbeat) receives the swing shift.
            if (subdivIdx % 2 == 1) targetGrid += swingOffset;

            // strength=1 hard-snap; strength=0 no shift; linear interpolation between.
            double snappedBeat = currentBeat + strength * (targetGrid - currentBeat);
            double onsetShift = snappedBeat - currentBeat;

            // Builder-helper rebuild — preserves all other fields, even ones added by
            // future Phase 22 plans, without naming them here. Rollback-independent.
            newNotes.Add(note.With(onsetOffset: onsetShift));

            currentBeat += note.GetBeats(barTs.Denominator);
            subdivIdx++;
        }
        var newBar = new BarData(newNotes, barTs) { IsPickup = bar.IsPickup };
        if (bar.ParallelVoices != null)
        {
            var voices = new List<BarData>(bar.ParallelVoices.Count);
            foreach (var voiceBar in bar.ParallelVoices)
                voices.Add(QuantizeBar(voiceBar, subdivBeats, swingOffset, strength, timesig));
            newBar.ParallelVoices = voices;
        }
        return newBar;
    }

    /// <summary>
    /// DX-13: converts a NoteValue resolution into beats per subdivision for the active
    /// time signature. Charitable D-07: out-of-range enum values fall through to the
    /// default arm and are treated as a quarter note (no exception, no crash).
    /// </summary>
    /// <summary>
    /// sweep-0614: converts a NoteValue resolution into QUARTER-note units (the universal
    /// duration unit used by GetBeats / the render + MIDI timeline). QUARTER = 1.0, EIGHTH
    /// = 0.5, etc. — meter-independent. Charitable D-07: out-of-range enum values fall
    /// through to the quarter-note default (no exception).
    /// </summary>
    private static double NoteValueToQuarters(NoteValueType.Value nv)
    {
        return nv switch
        {
            NoteValueType.Value.WHOLE        => 4.0,
            NoteValueType.Value.HALF         => 2.0,
            NoteValueType.Value.QUARTER      => 1.0,
            NoteValueType.Value.EIGHTH       => 0.5,
            NoteValueType.Value.SIXTEENTH    => 0.25,
            NoteValueType.Value.THIRTYSECOND => 0.125,
            _                                => 1.0,
        };
    }

    // ===== MIDI Helpers =====

    private static int ToMidi(char noteName, int octave, int alteration)
    {
        int noteOffset = noteName switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5,
            'G' => 7, 'A' => 9, 'B' => 11,
            _ => throw new ArgumentException($"Invalid note name: {noteName}")
        };
        return (octave + 1) * 12 + noteOffset + alteration;
    }

    private static (char NoteName, int Octave, int Alteration) FromMidi(int midi)
    {
        int octave = (midi / 12) - 1;
        int pitchClass = midi % 12;
        if (pitchClass < 0) { pitchClass += 12; octave--; }

        // Map chromatic pitches to naturals + alteration (sharps only)
        var (noteName, alteration) = pitchClass switch
        {
            0  => ('C', 0),
            1  => ('C', 1),
            2  => ('D', 0),
            3  => ('D', 1),
            4  => ('E', 0),
            5  => ('F', 0),
            6  => ('F', 1),
            7  => ('G', 0),
            8  => ('G', 1),
            9  => ('A', 0),
            10 => ('A', 1),
            11 => ('B', 0),
            _  => ('C', 0)
        };

        return (noteName, octave, alteration);
    }

    /// <summary>
    /// Applies a transform function to every note in every bar, producing a new SequenceData.
    /// </summary>
    private static SequenceData TransformNotes(SequenceData seq, Func<MusicalNoteData, MusicalNoteData> transform)
    {
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            result.AddBar(TransformBar(bar, transform));
        }
        return result;
    }

    /// <summary>
    /// Audit 2026-06-09 §4.1: applies the per-note <paramref name="transform"/> to one
    /// bar, recursing into Phase 28 <see cref="BarData.ParallelVoices"/> so voice-block
    /// sequences survive transpose / invert / augment / diminish / legato / portamento /
    /// velocity-gradient. Mirrors <see cref="HumanizeBar"/> — the parent bar's
    /// MusicalNotes list is the whole-bar-rest placeholder (left a rest by the transform's
    /// IsRest fast-path) while the audible content lives in each voice sub-bar. Before
    /// this fix every TransformNotes-based transform constructed
    /// <c>new BarData(newNotes, ts)</c> and never copied ParallelVoices, silently
    /// deleting all voice content → silent WAVs.
    /// </summary>
    private static BarData TransformBar(BarData bar, Func<MusicalNoteData, MusicalNoteData> transform)
    {
        var newNotes = new List<MusicalNoteData>(bar.MusicalNotes.Count);
        foreach (var note in bar.MusicalNotes)
            newNotes.Add(transform(note));

        var newBar = new BarData(newNotes, bar.TimeSignature!) { IsPickup = bar.IsPickup };

        if (bar.ParallelVoices != null)
        {
            var newVoices = new List<BarData>(bar.ParallelVoices.Count);
            foreach (var voiceBar in bar.ParallelVoices)
                newVoices.Add(TransformBar(voiceBar, transform));   // recurse (Phase 28 emits one level)
            newBar.ParallelVoices = newVoices;
        }

        return newBar;
    }

    /// <summary>
    /// Audit 2026-06-09 §4.1: deep-copies a bar preserving notes, pickup flag, and any
    /// Phase 28 ParallelVoices (each voice sub-bar cloned recursively). Used by the
    /// hand-rolled bar-rebuild loops in <c>repeat</c> / <c>concat</c> that previously
    /// constructed <c>new BarData(notes, ts)</c> and dropped voice content entirely.
    /// </summary>
    private static BarData CloneBarWithVoices(BarData bar)
    {
        var clone = new BarData(new List<MusicalNoteData>(bar.MusicalNotes), bar.TimeSignature!)
        {
            IsPickup = bar.IsPickup,
        };
        if (bar.ParallelVoices != null)
        {
            var voices = new List<BarData>(bar.ParallelVoices.Count);
            foreach (var voiceBar in bar.ParallelVoices)
                voices.Add(CloneBarWithVoices(voiceBar));
            clone.ParallelVoices = voices;
        }
        return clone;
    }

    /// <summary>
    /// Audit 2026-06-09 §4.1: retrograde-clones a bar — reverses the note order AND any
    /// Phase 28 ParallelVoices (each voice sub-bar reversed recursively) so retrograde of
    /// a voice-block sequence stays audible. The voice list order is preserved (voices are
    /// parallel, not sequential); only each voice's internal note order reverses.
    /// </summary>
    private static BarData ReverseBar(BarData bar)
    {
        var reversedNotes = new List<MusicalNoteData>(bar.MusicalNotes);
        reversedNotes.Reverse();
        var newBar = new BarData(reversedNotes, bar.TimeSignature!) { IsPickup = bar.IsPickup };
        if (bar.ParallelVoices != null)
        {
            var voices = new List<BarData>(bar.ParallelVoices.Count);
            foreach (var voiceBar in bar.ParallelVoices)
                voices.Add(ReverseBar(voiceBar));
            newBar.ParallelVoices = voices;
        }
        return newBar;
    }

    /// <summary>
    /// Audit 2026-06-09 §4.1: counts every non-rest note in a sequence INCLUDING Phase 28
    /// voice-block sub-bars. The velocity/tempo-shaping transforms (swell / crescendo /
    /// decrescendo / ritardando / accelerando) index notes globally; this counts the same
    /// set the indexed walk visits so the gradient endpoints line up.
    /// </summary>
    private static int CountAudibleNotes(SequenceData seq)
    {
        int total = 0;
        foreach (var bar in seq.Bars)
            total += CountAudibleNotes(bar);
        return total;
    }

    private static int CountAudibleNotes(BarData bar)
    {
        int total = 0;
        foreach (var note in bar.MusicalNotes)
            if (!note.IsRest) total++;
        if (bar.ParallelVoices != null)
            foreach (var voiceBar in bar.ParallelVoices)
                total += CountAudibleNotes(voiceBar);
        return total;
    }

    /// <summary>
    /// Sweep 2026-06-14: returns the MIDI number of the first non-rest note in a bar,
    /// scanning <c>bar.MusicalNotes</c> first then recursing into Phase 28
    /// <see cref="BarData.ParallelVoices"/> in order (matching <see cref="CountAudibleNotes"/>'s
    /// parent-then-voices traversal so the chosen axis is deterministic / two-run cmp-clean).
    /// Used by <c>invert</c> so a voice-block-only sequence — whose parent MusicalNotes hold
    /// only a whole-bar rest placeholder — still finds an inversion axis instead of degenerating
    /// to an identity clone.
    /// </summary>
    private static int? FindFirstNonRestMidi(BarData bar)
    {
        foreach (var note in bar.MusicalNotes)
            if (!note.IsRest)
                return ToMidi(note.NoteName, note.Octave, note.Alteration);
        if (bar.ParallelVoices != null)
            foreach (var voiceBar in bar.ParallelVoices)
            {
                var found = FindFirstNonRestMidi(voiceBar);
                if (found.HasValue) return found;
            }
        return null;
    }

    /// <summary>
    /// Audit 2026-06-09 §4.1: rebuilds a sequence applying a per-note function that receives
    /// the running global non-rest index, recursing into Phase 28 ParallelVoices so voice-block
    /// content is shaped (and preserved) instead of silently deleted. Rests pass through
    /// untouched and do NOT advance the index (matching the pre-fix gradient counting). The
    /// per-note function returns the rebuilt note (callers use <c>note.With(velocity:)</c> so the
    /// trailing five fields survive — Audit §4.2). <paramref name="index"/> is threaded by ref
    /// across bars and voices.
    /// </summary>
    private static SequenceData MapNotesIndexed(
        SequenceData seq, Func<MusicalNoteData, int, MusicalNoteData> shape)
    {
        int index = 0;
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
            result.AddBar(MapBarIndexed(bar, shape, ref index));
        return result;
    }

    private static BarData MapBarIndexed(
        BarData bar, Func<MusicalNoteData, int, MusicalNoteData> shape, ref int index)
    {
        var newNotes = new List<MusicalNoteData>(bar.MusicalNotes.Count);
        foreach (var note in bar.MusicalNotes)
        {
            if (note.IsRest) { newNotes.Add(note); continue; }
            newNotes.Add(shape(note, index));
            index++;
        }
        var newBar = new BarData(newNotes, bar.TimeSignature!) { IsPickup = bar.IsPickup };
        if (bar.ParallelVoices != null)
        {
            var voices = new List<BarData>(bar.ParallelVoices.Count);
            foreach (var voiceBar in bar.ParallelVoices)
                voices.Add(MapBarIndexed(voiceBar, shape, ref index));
            newBar.ParallelVoices = voices;
        }
        return newBar;
    }

    // ===== Transpose =====

    private static void RegisterTranspose(InternalFunctionRegistry registry)
    {
        // Phase 36 Plan 36-02 (D-36-11): transpose is the seed builtin for
        // the universal named-arg surface — Plans 36-03/04 backfill the rest.
        // Both Semitone and Cent overloads share the (seq, amount) parameter
        // name shape so `(transpose s amount=2)` and `(transpose s amount=+50c)`
        // both work transparently.
        var transposeSemitoneSig = new FunctionSignature("transpose",
            [SequenceType.Instance, SemitoneType.Instance],
            ParameterNames: ["seq", "amount"]);
        registry.Register("transpose", transposeSemitoneSig, TransposeSemitone);

        // transpose(Sequence, Cent)
        var transposeCentSig = new FunctionSignature("transpose",
            [SequenceType.Instance, CentType.Instance],
            ParameterNames: ["seq", "amount"]);
        registry.Register("transpose", transposeCentSig, TransposeCent);

        // sweep-0614: transpose(Sequence, Long) — a whole-number Long acts as a
        // semitone count, consistent with the Int → Long widening chain (Int and
        // Double already work; Long previously failed "No matching overload").
        // Scoped here rather than via a global SemitoneType widening so the D-08
        // "(semitones x) is Int-ONLY" carve-out stays intact.
        var transposeLongSig = new FunctionSignature("transpose",
            [SequenceType.Instance, LongType.Instance],
            ParameterNames: ["seq", "amount"]);
        registry.Register("transpose", transposeLongSig, TransposeLong);
    }

    /// <remarks>
    /// Phase 23 D-12 / MICR-02 caveat: under non-12-TET tunings (justIntonation,
    /// pythagorean), transpose may silently respell notes at enharmonic junctions
    /// (e.g., F#4 → Gb4 round-trip), producing an audible ~21 cent shift in the
    /// rendered output even though the MIDI number is preserved. Transforms remain
    /// MIDI-based by design (MICR-02): same MIDI numbers under all 3 tunings; only
    /// the rendered Hz differ. A strict-mode <c>transposePreserveSpelling</c> escape
    /// hatch is documented as a v1.4 candidate — see CONTEXT.md D-12 +
    /// REQUIREMENTS.md "Future Requirements".
    /// </remarks>
    private static Value TransposeSemitone(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int semitones = args[1].As<int>();
        return Value.Sequence(TransposeBy(seq, semitones, centsRemainder: 0.0));
    }

    /// <summary>
    /// sweep-0614: explicit transpose(Sequence, Long) overload. A whole-number
    /// Long sits in the documented Int → Long widening chain, and Semitone is
    /// whole-numbers-by-design, so a composer reasonably expects
    /// <c>(transpose seq longVal)</c> to behave like a semitone count — exactly
    /// as the Int and Double paths already do. Scoped to transpose (NOT a global
    /// SemitoneType widening) so the D-08 "(semitones x) is Int-ONLY" carve-out
    /// is untouched. A Long beyond int range is clamped (transpose past ±127
    /// semitones is meaningless anyway).
    /// </summary>
    private static Value TransposeLong(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        long raw = args[1].As<long>();
        int semitones = raw > int.MaxValue ? int.MaxValue
                      : raw < int.MinValue ? int.MinValue
                      : (int)raw;
        return Value.Sequence(TransposeBy(seq, semitones, centsRemainder: 0.0));
    }

    /// <summary>
    /// Audit 2026-06-09 §10-gap-3: true cent-precision transpose. The previous shape
    /// rounded cents to whole semitones (<c>(int)Math.Round(cents/100)</c>), so
    /// <c>(transpose seq +50c)</c> was a silent no-op — yet per-note cent offsets are
    /// core syntax (<c>C4+50c</c>), MusicalNoteData carries a CentOffset that every
    /// transform copies, and the render path honors arbitrary cents. Now the whole-semitone
    /// part shifts pitch and the fractional remainder folds into each note's CentOffset.
    /// <c>+50c</c> → +0st with +50c folded in; <c>+150c</c> → +1st with +50c folded in.
    /// </summary>
    private static Value TransposeCent(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double cents = args[1].As<double>();

        // Whole-semitone part shifts the pitch; the signed remainder folds into CentOffset.
        // Round toward nearest to keep the folded remainder in [-50, +50] when possible —
        // but truncate-toward-zero keeps the remainder same-sign as the input so a positive
        // request never silently flips a note's spelling downward. We use Math.Truncate so
        // +150c → +1st + 50c and -150c → -1st - 50c (intuitive for composers).
        int semitones = (int)Math.Truncate(cents / 100.0);
        double centsRemainder = cents - semitones * 100.0;

        return Value.Sequence(TransposeBy(seq, semitones, centsRemainder));
    }

    /// <summary>
    /// Shared transpose core: shifts pitch by <paramref name="semitones"/> and, when
    /// <paramref name="centsRemainder"/> is non-zero, ADDS that remainder to each note's
    /// existing CentOffset (folding cent-precision transposition into the field the render
    /// path already honors). Routes every rebuilt note through <c>With(...)</c> (Audit §4.2)
    /// so IsChordTone / DurationFraction / OnsetOffset / DurationOverlap / PortamentoMs all
    /// survive — fixing chord-bracket re-arpeggiation and tuplet/quantize/legato strip.
    /// </summary>
    private static SequenceData TransposeBy(SequenceData seq, int semitones, double centsRemainder)
    {
        return TransformNotes(seq, note =>
        {
            if (note.IsRest) return note;

            int midi = ToMidi(note.NoteName, note.Octave, note.Alteration) + semitones;

            if (midi < MIDI_MIN || midi > MIDI_MAX)
            {
                int clamped = Math.Clamp(midi, MIDI_MIN, MIDI_MAX);
                Console.Error.WriteLine(
                    $"Warning: transpose would put {NoteType.Format(note.NoteName, note.Octave, note.Alteration)} " +
                    $"out of range (MIDI {midi}), clamping to MIDI {clamped}");
                midi = clamped;
            }

            var (name, oct, alt) = FromMidi(midi);
            double? newCent = centsRemainder == 0.0
                ? null                                          // keep existing CentOffset
                : (note.CentOffset ?? 0.0) + centsRemainder;    // fold remainder into per-note cents
            return note.With(noteName: name, octave: oct, alteration: alt, centOffset: newCent);
        });
    }

    // ===== Invert =====

    private static void RegisterInvert(InternalFunctionRegistry registry)
    {
        var invertSig = new FunctionSignature("invert",
            [SequenceType.Instance],
            ParameterNames: ["seq"]);
        registry.Register("invert", invertSig, Invert);
    }

    private static Value Invert(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();

        // Find the first non-rest note across all bars (the axis). Sweep 2026-06-14:
        // recurse into Phase 28 ParallelVoices so a voice-block-only sequence (whose parent
        // bar.MusicalNotes hold only a whole-bar rest placeholder, with the audible notes in
        // ParallelVoices) finds an axis. Before this fix axisMidi stayed null for voice blocks,
        // so invert hit the all-rest early return and returned a byte-identical clone (no-op).
        int? axisMidi = null;
        foreach (var bar in seq.Bars)
        {
            axisMidi = FindFirstNonRestMidi(bar);
            if (axisMidi.HasValue) break;
        }

        // If no notes found (all rests), return a clone
        if (!axisMidi.HasValue)
            return Value.Sequence(TransformNotes(seq, n => n));

        int axis = axisMidi.Value;
        var result = TransformNotes(seq, note =>
        {
            if (note.IsRest) return note;

            int midi = ToMidi(note.NoteName, note.Octave, note.Alteration);
            int inverted = 2 * axis - midi;
            inverted = Math.Clamp(inverted, MIDI_MIN, MIDI_MAX);

            var (name, oct, alt) = FromMidi(inverted);
            // Audit §4.2: With(...) preserves IsChordTone / DurationFraction / OnsetOffset /
            // DurationOverlap / PortamentoMs that the old 12-arg ctor dropped.
            return note.With(noteName: name, octave: oct, alteration: alt);
        });

        return Value.Sequence(result);
    }

    // ===== Retrograde =====

    private static void RegisterRetrograde(InternalFunctionRegistry registry)
    {
        var retrogradeSig = new FunctionSignature("retrograde",
            [SequenceType.Instance],
            ParameterNames: ["seq"]);
        registry.Register("retrograde", retrogradeSig, Retrograde);
    }

    private static Value Retrograde(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();

        // True retrograde: reverse both the bar order AND the notes within each bar.
        var reversedBars = new List<BarData>();
        foreach (var bar in seq.Bars)
        {
            reversedBars.Add(ReverseBar(bar));
        }
        reversedBars.Reverse();

        var result = new SequenceData();
        foreach (var bar in reversedBars)
            result.AddBar(bar);
        return Value.Sequence(result);
    }

    // ===== Augment / Diminish =====

    private static void RegisterAugmentDiminish(InternalFunctionRegistry registry)
    {
        var augmentSig = new FunctionSignature("augment",
            [SequenceType.Instance],
            ParameterNames: ["seq"]);
        registry.Register("augment", augmentSig, Augment);

        var diminishSig = new FunctionSignature("diminish",
            [SequenceType.Instance],
            ParameterNames: ["seq"]);
        registry.Register("diminish", diminishSig, Diminish);
    }

    // AUDIT-VERIFIED 2026-04-18: C5 — augment correct (lengthens); observed A=#### vs Q=## columns in visualize (tests/spike/c5-augment-diminish.flow)
    // AUDIT-VERIFIED 2026-04-26: re-validated against tuplet sequences (Phase 19 TUP-07)
    //   — see flow-lang.Tests/Unit/Phase19/TupletAugmentDiminishTests.cs (rational doubling pinned via Fraction(2, 1) × f)
    private static Value Augment(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();

        var result = TransformNotes(seq, note =>
        {
            // TUP-07: rational branch — when DurationFraction is set, double it via Phase 18 Fraction(2,1) × f.
            // Existing enum path runs verbatim when DurationFraction is null (Phase 18 byte-identical contract).
            if (note.DurationFraction.HasValue)
            {
                var doubled = note.DurationFraction.Value * new Fraction(2, 1);
                // Audit §4.2: rational branch kept durationFraction but the old 12-arg
                // ctor still dropped OnsetOffset / DurationOverlap / PortamentoMs / IsChordTone.
                return note.With(durationFraction: doubled);
            }

            if (!note.DurationValue.HasValue) return note;

            int newDur = note.DurationValue.Value - 1; // toward WHOLE=0
            if (newDur < (int)NoteValueType.Value.WHOLE)
            {
                Console.Error.WriteLine("Warning: augment clamped duration at whole note");
                newDur = (int)NoteValueType.Value.WHOLE;
            }

            return note.With(durationValue: newDur);
        });

        return Value.Sequence(result);
    }

    // AUDIT-VERIFIED 2026-04-18: C5 — diminish correct (shortens); observed D=# vs Q=## columns in visualize (tests/spike/c5-augment-diminish.flow)
    // AUDIT-VERIFIED 2026-04-26: re-validated against tuplet sequences (Phase 19 TUP-07)
    //   — see flow-lang.Tests/Unit/Phase19/TupletAugmentDiminishTests.cs (rational halving pinned via Fraction(1, 2) × f)
    private static Value Diminish(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();

        var result = TransformNotes(seq, note =>
        {
            // TUP-07: rational branch — when DurationFraction is set, halve it via Phase 18 Fraction(1,2) × f.
            // Existing enum path runs verbatim when DurationFraction is null (Phase 18 byte-identical contract).
            if (note.DurationFraction.HasValue)
            {
                var halved = note.DurationFraction.Value * new Fraction(1, 2);
                // Audit §4.2: rational branch kept durationFraction but the old 12-arg
                // ctor still dropped OnsetOffset / DurationOverlap / PortamentoMs / IsChordTone.
                return note.With(durationFraction: halved);
            }

            if (!note.DurationValue.HasValue) return note;

            int newDur = note.DurationValue.Value + 1; // toward THIRTYSECOND=5
            if (newDur > (int)NoteValueType.Value.THIRTYSECOND)
            {
                Console.Error.WriteLine("Warning: diminish clamped duration at thirty-second note");
                newDur = (int)NoteValueType.Value.THIRTYSECOND;
            }

            return note.With(durationValue: newDur);
        });

        return Value.Sequence(result);
    }

    // ===== Test wrappers =====
    // Public test wrappers expose the private dispatch for direct C# unit tests
    // (per Phase 19 Plan 19-05 TUP-07 — flow-lang.Tests/Unit/Phase19/TupletAugmentDiminishTests.cs).
    // Production callers continue routing through the registry's `augment` / `diminish` signatures.
    public static SequenceData AugmentForTesting(SequenceData seq) =>
        Augment(new List<Value> { Value.Sequence(seq) }).As<SequenceData>();

    public static SequenceData DiminishForTesting(SequenceData seq) =>
        Diminish(new List<Value> { Value.Sequence(seq) }).As<SequenceData>();

    // Audit 2026-06-09 §4.2 — direct transpose wrapper so C#-built tuplet sequences
    // (DurationFraction set) can be transposed without routing through the note-stream
    // lexer (which has no quarter-triplet literal). Production callers use the registry's
    // `transpose` signatures.
    public static SequenceData ApplyTransposeForTesting(SequenceData seq, int semitones) =>
        TransposeSemitone(new List<Value> { Value.Sequence(seq), Value.Semitone(semitones) }).As<SequenceData>();

    // Audit 2026-06-09 §4.5 — direct trill wrapper so a C#-built source note carrying a
    // specific CentOffset + Articulation can be trilled and the upper-neighbour propagation
    // asserted without depending on combined cent+articulation note-stream literals.
    public static SequenceData TrillForTesting(SequenceData seq, int semitones) =>
        Trill(new List<Value> { Value.Sequence(seq), Value.Semitone(semitones) }).As<SequenceData>();

    // Audit 2026-06-09 §10-gap-3 — direct cent-transpose wrapper so a C#-built note carrying
    // a known CentOffset can be cent-transposed and the fold-into-CentOffset behaviour asserted.
    public static SequenceData ApplyTransposeCentForTesting(SequenceData seq, double cents) =>
        TransposeCent(new List<Value> { Value.Sequence(seq), Value.Cent(cents) }).As<SequenceData>();

    // ===== Octave Shift =====

    private static void RegisterOctaveShift(InternalFunctionRegistry registry)
    {
        var upSig = new FunctionSignature("up",
            [SequenceType.Instance, IntType.Instance],
            ParameterNames: ["seq", "octaves"]);
        registry.Register("up", upSig, OctaveUp);

        var downSig = new FunctionSignature("down",
            [SequenceType.Instance, IntType.Instance],
            ParameterNames: ["seq", "octaves"]);
        registry.Register("down", downSig, OctaveDown);

        // Quick 260701-vqz follow-up: a Semitone literal into the Int octaves slot
        // silently converted (+2st meant 2 OCTAVES). The exact-match Semitone
        // overload honors the unit as written: (up seq +2st) = up 2 semitones.
        // Int octave forms are unchanged (exact Int match still wins for bare ints).
        var upStSig = new FunctionSignature("up",
            [SequenceType.Instance, SemitoneType.Instance],
            ParameterNames: ["seq", "semitones"]);
        registry.Register("up", upStSig, args => TransposeSemitone([args[0], args[1]]));

        var downStSig = new FunctionSignature("down",
            [SequenceType.Instance, SemitoneType.Instance],
            ParameterNames: ["seq", "semitones"]);
        registry.Register("down", downStSig, args =>
            TransposeSemitone([args[0], Value.Semitone(-args[1].As<int>())]));
    }

    private static Value OctaveUp(IReadOnlyList<Value> args)
    {
        int octaves = args[1].As<int>();
        return TransposeSemitone([args[0], Value.Semitone(octaves * 12)]);
    }

    private static Value OctaveDown(IReadOnlyList<Value> args)
    {
        int octaves = args[1].As<int>();
        return TransposeSemitone([args[0], Value.Semitone(-octaves * 12)]);
    }

    // ===== Repeat =====

    private static void RegisterRepeat(InternalFunctionRegistry registry)
    {
        // repeat(Sequence, Int)
        var repeatSig = new FunctionSignature("repeat",
            [SequenceType.Instance, IntType.Instance],
            ParameterNames: ["seq", "times"]);
        registry.Register("repeat", repeatSig, Repeat);

        // repeat(Sequence, Int, Semitone)
        var repeatTransposeSig = new FunctionSignature("repeat",
            [SequenceType.Instance, IntType.Instance, SemitoneType.Instance],
            ParameterNames: ["seq", "times", "transposeBy"]);
        registry.Register("repeat", repeatTransposeSig, RepeatTranspose);

        // sweep-260620 soft-overload: Cent slot routes to RepeatTransposeCent with remainder
        // folding (CORRECTNESS — the Semitone slot previously threw for `(repeat seq 3 +50c)`).
        var repeatTransposeCentSig = new FunctionSignature("repeat",
            [SequenceType.Instance, IntType.Instance, CentType.Instance],
            ParameterNames: ["seq", "times", "transposeBy"]);
        registry.Register("repeat", repeatTransposeCentSig, RepeatTransposeCent);
    }

    private static Value Repeat(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int times = args[1].As<int>();

        var result = new SequenceData();
        for (int i = 0; i < times; i++)
        {
            foreach (var bar in seq.Bars)
            {
                // Audit §4.1: CloneBarWithVoices preserves Phase 28 ParallelVoices.
                result.AddBar(CloneBarWithVoices(bar));
            }
        }
        return Value.Sequence(result);
    }

    private static Value RepeatTranspose(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int times = args[1].As<int>();
        int semitones = args[2].As<int>();

        var result = new SequenceData();
        for (int i = 0; i < times; i++)
        {
            int cumulativeTranspose = i * semitones;
            // Audit §4.1/§4.2: TransformBar recurses into ParallelVoices and With(...)
            // preserves the trailing five fields the old 12-arg ctor dropped.
            Func<MusicalNoteData, MusicalNoteData> shift = note =>
            {
                if (note.IsRest) return note;
                int midi = ToMidi(note.NoteName, note.Octave, note.Alteration) + cumulativeTranspose;
                midi = Math.Clamp(midi, MIDI_MIN, MIDI_MAX);
                var (name, oct, alt) = FromMidi(midi);
                return note.With(noteName: name, octave: oct, alteration: alt);
            };
            foreach (var bar in seq.Bars)
            {
                result.AddBar(TransformBar(bar, shift));
            }
        }
        return Value.Sequence(result);
    }

    /// <summary>
    /// sweep-260620 soft-overload: cent-precision repeat-transpose. Per iteration the cumulative
    /// amount is i*cents; each iteration's cumulative cents splits into a whole-semitone pitch
    /// shift + a remainder folded into CentOffset (models RepeatTranspose + the TransposeBy fold).
    /// </summary>
    private static Value RepeatTransposeCent(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int times = args[1].As<int>();
        double cents = args[2].As<double>();

        var result = new SequenceData();
        for (int i = 0; i < times; i++)
        {
            double cumulativeCents = i * cents;
            int cumulativeSemitones = (int)Math.Truncate(cumulativeCents / 100.0);
            double centsRemainder = cumulativeCents - cumulativeSemitones * 100.0;

            Func<MusicalNoteData, MusicalNoteData> shift = note =>
            {
                if (note.IsRest) return note;
                int midi = ToMidi(note.NoteName, note.Octave, note.Alteration) + cumulativeSemitones;
                midi = Math.Clamp(midi, MIDI_MIN, MIDI_MAX);
                var (name, oct, alt) = FromMidi(midi);
                double? newCent = centsRemainder == 0.0
                    ? null
                    : (note.CentOffset ?? 0.0) + centsRemainder;
                return note.With(noteName: name, octave: oct, alteration: alt, centOffset: newCent);
            };
            foreach (var bar in seq.Bars)
            {
                result.AddBar(TransformBar(bar, shift));
            }
        }
        return Value.Sequence(result);
    }

    // ===== Concat =====

    private static void RegisterConcat(InternalFunctionRegistry registry)
    {
        var concatSig = new FunctionSignature("concat",
            [SequenceType.Instance, SequenceType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("concat", concatSig, ConcatSequences);
    }

    private static Value ConcatSequences(IReadOnlyList<Value> args)
    {
        var seqA = args[0].As<SequenceData>();
        var seqB = args[1].As<SequenceData>();

        var result = new SequenceData();
        // Audit §4.1: CloneBarWithVoices preserves Phase 28 ParallelVoices in both halves.
        foreach (var bar in seqA.Bars)
            result.AddBar(CloneBarWithVoices(bar));
        foreach (var bar in seqB.Bars)
            result.AddBar(CloneBarWithVoices(bar));
        return Value.Sequence(result);
    }

    // ===== Dynamic Transforms (Phase 44 Plan 44-05: strict-aware registration
    // lives in RegisterContextDependent + *Strict wrappers above. The legacy
    // Crescendo / Decrescendo / Swell private methods and
    // RegisterDynamicTransforms registration method were deleted Phase 44
    // Plan 44-05 because they are no longer reachable — the strict-aware
    // path delegates to SwellCore / ApplyVelocityGradient directly.) =====

    /// <summary>
    /// Shared body of swell extracted Phase 44 Plan 44-05 so the strict-aware
    /// <see cref="SwellStrict"/> can call the same code path with already-validated
    /// raw values (strict skips the clamp; non-strict pre-clamps).
    /// </summary>
    private static Value SwellCore(SequenceData seq, double edgeVel, double peakVel)
    {
        // Audit §4.1: count + index include Phase 28 voice-block notes.
        int totalNotes = CountAudibleNotes(seq);

        if (totalNotes <= 1)
            return Value.Sequence(seq);

        int midpoint = totalNotes / 2;
        int descendLength = totalNotes - 1 - midpoint;

        var result = MapNotesIndexed(seq, (note, noteIndex) =>
        {
            double t;
            if (noteIndex <= midpoint && midpoint > 0)
                t = (double)noteIndex / midpoint;
            else if (descendLength > 0)
                t = 1.0 - ((double)(noteIndex - midpoint) / descendLength);
            else
                t = 1.0;

            double velocity = Math.Clamp(edgeVel + t * (peakVel - edgeVel), 0.0, 1.0);
            // Audit §4.2: With(velocity:) preserves the trailing five fields.
            return note.With(velocity: velocity);
        });
        return Value.Sequence(result);
    }

    private static SequenceData ApplyVelocityGradient(SequenceData seq, double startVel, double endVel)
    {
        // Audit §4.1: count + index include Phase 28 voice-block notes.
        int totalNotes = CountAudibleNotes(seq);

        if (totalNotes <= 1)
        {
            return TransformNotes(seq, note =>
                note.IsRest ? note : note.With(velocity: startVel));
        }

        return MapNotesIndexed(seq, (note, noteIndex) =>
        {
            double t = (double)noteIndex / (totalNotes - 1);
            double velocity = Math.Clamp(startVel + t * (endVel - startVel), 0.0, 1.0);
            return note.With(velocity: velocity);
        });
    }

    // ===== Tempo Transforms (Phase 44 Plan 44-05: ritardando + accelerando
    // moved to RegisterTempoTransformsContextDependent for strict-aware
    // clamp checking; fermata stays non-context-dep — no input-perimeter
    // clamp on its int arg.) =====

    /// <summary>
    /// Standalone fermata registration carved out Phase 44 Plan 44-05 — fermata
    /// has no out-of-range clamp so it stays in the non-context-dep Register
    /// path. Called from Register() alongside the other ornament transforms.
    /// </summary>
    private static void RegisterFermata(InternalFunctionRegistry registry)
    {
        var fermataSig = new FunctionSignature("fermata",
            [SequenceType.Instance, IntType.Instance],
            ParameterNames: ["seq", "index"]);
        registry.Register("fermata", fermataSig, FermataTransform);
    }

    /// <summary>
    /// Shared body of ritardando extracted Phase 44 Plan 44-05 so the
    /// strict-aware <see cref="RitardandoStrict"/> can call the same code path
    /// with an already-validated amount (strict skips the clamp; non-strict pre-clamps).
    /// </summary>
    private static Value RitardandoCore(SequenceData seq, double amount)
    {
        // Audit §4.1: count + index include Phase 28 voice-block notes.
        int totalNotes = CountAudibleNotes(seq);
        if (totalNotes <= 1) return Value.Sequence(seq);

        var result = MapNotesIndexed(seq, (note, noteIndex) =>
        {
            double t = (double)noteIndex / (totalNotes - 1);
            // Reduce velocity slightly for rit feel (later = softer = perceived slower)
            double velReduction = t * amount * 0.3;
            double newVel = Math.Clamp(note.Velocity - velReduction, 0.05, 1.0);
            return note.With(velocity: newVel);   // Audit §4.2: preserves trailing five fields
        });
        return Value.Sequence(result);
    }

    /// <summary>
    /// Shared body of accelerando extracted Phase 44 Plan 44-05. The legacy
    /// non-context-dep AccelerandoTransform method was deleted along with the
    /// other pre-strict shapes — strict-aware AccelerandoStrict delegates here directly.
    /// </summary>
    private static Value AccelerandoCore(SequenceData seq, double amount)
    {
        // Audit §4.1: count + index include Phase 28 voice-block notes.
        int totalNotes = CountAudibleNotes(seq);
        if (totalNotes <= 1) return Value.Sequence(seq);

        var result = MapNotesIndexed(seq, (note, noteIndex) =>
        {
            double t = (double)noteIndex / (totalNotes - 1);
            // Increase velocity slightly for accel feel (later = louder = perceived faster)
            double velBoost = t * amount * 0.3;
            double newVel = Math.Clamp(note.Velocity + velBoost, 0.05, 1.0);
            return note.With(velocity: newVel);   // Audit §4.2: preserves trailing five fields
        });
        return Value.Sequence(result);
    }

    /// <summary>
    /// Fermata: hold the note at the given index for 2x its normal duration (move to next
    /// larger duration value).
    /// </summary>
    private static Value FermataTransform(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int targetIdx = args[1].As<int>();

        // Audit §4.1/§4.2: MapNotesIndexed recurses into Phase 28 voice blocks and With(...)
        // preserves the trailing five fields the old 12-arg ctor dropped. The non-rest index
        // counts voice-block notes too, matching the pre-fix global-index semantics.
        var result = MapNotesIndexed(seq, (note, noteIndex) =>
        {
            if (noteIndex == targetIdx && note.DurationValue.HasValue)
            {
                // Augment: move to next larger duration (e.g. quarter -> half)
                int newDur = Math.Max(note.DurationValue.Value - 1, (int)NoteValueType.Value.WHOLE);
                return note.With(durationValue: newDur);
            }
            return note;
        });
        return Value.Sequence(result);
    }

    // ===== Humanize (Phase 44 Plan 44-05: registration owned by
    // RegisterHumanizeContextDependent + HumanizeStrict wrapper above. Legacy
    // RegisterHumanize + Humanize private methods were deleted along with the
    // other pre-strict shapes.) =====

    /// <summary>
    /// Shared body of humanize extracted Phase 44 Plan 44-05. The strict-aware
    /// HumanizeStrict delegates here directly. Sweep 2026-06-14: the jitter RNG is now
    /// passed in (a PrngRegistry-backed <see cref="Random"/> keyed by the call site) so
    /// the unseeded uniform humanize reseeds at the render boundary and stays two-run
    /// cmp-clean — it used to draw from a process-global wall-clock <c>Random</c>.
    /// </summary>
    private static Value HumanizeCore(SequenceData seq, double amount, Random rng)
    {
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            result.AddBar(HumanizeUniformBar(bar, amount, rng));
        }
        return Value.Sequence(result);
    }

    /// <summary>
    /// Audit 2026-06-09 §4.1/§4.2: humanize one bar, recursing into Phase 28 ParallelVoices
    /// so voice-block sequences stay audible (they rendered silent before). Still one
    /// <c>rng.NextDouble()</c> draw per non-rest note, in parent-then-voices order. The RNG
    /// is the PrngRegistry-backed deterministic stream threaded from HumanizeStrict (sweep
    /// 2026-06-14) so it reseeds at the renderSong/writeWav boundary and preserves two-run
    /// cmp-clean. Rebuilt notes go through <c>With(velocity:)</c> so the trailing fields survive.
    /// </summary>
    private static BarData HumanizeUniformBar(BarData bar, double amount, Random rng)
    {
        var newNotes = new List<MusicalNoteData>(bar.MusicalNotes.Count);
        foreach (var note in bar.MusicalNotes)
        {
            if (note.IsRest) { newNotes.Add(note); continue; }

            // Velocity jitter: random variation scaled by amount
            double velJitter = (rng.NextDouble() * 2.0 - 1.0) * amount * 0.2;
            double newVelocity = Math.Clamp(note.Velocity + velJitter, 0.05, 1.0);
            newNotes.Add(note.With(velocity: newVelocity));
        }
        var newBar = new BarData(newNotes, bar.TimeSignature!) { IsPickup = bar.IsPickup };
        if (bar.ParallelVoices != null)
        {
            var voices = new List<BarData>(bar.ParallelVoices.Count);
            foreach (var voiceBar in bar.ParallelVoices)
                voices.Add(HumanizeUniformBar(voiceBar, amount, rng));
            newBar.ParallelVoices = voices;
        }
        return newBar;
    }

    // ===== Humanize Gaussian =====
    // PHASE 25 (DEFER-06): humanizeGaussian(Sequence, Double, Int) Box-Muller transform.
    //
    // Anchors decisions from .planning/phases/25-gaussian-humanize-last-prng-phase/25-CONTEXT.md:
    //   D-01  signature (Sequence, Double, Int) order (seq, amount, seed)
    //   D-03  LOCAL new Random(seed) per call; does NOT read or mutate ExecutionContext.GetRand.
    //         Mirrors VariationFunctions.VarySeeded at :71-77 and BuiltInFunctions.cs:1258.
    //   D-05  basic Box-Muller (cos branch); D-06 sin discarded
    //   D-07  velJitter = z * amount * 0.2 (matches uniform humanize jitter range)
    //   D-08  amount clamped to [0, 1]; D-09 velocity clamped to [0.05, 1.0]
    //   D-10  amount==0 short-circuit returns input unchanged
    //   D-11  rests pass through; D-12/D-13 empty/all-rest sequences pass through
    //   D-18  existing Humanize is FROZEN — humanizeGaussian uses note.With(velocity:)
    //         to avoid repeating the 12-arg ctor field-drop bug at :896-898
    //
    // NOTE: System.Random is NOT cryptographically secure. humanizeGaussian is for
    // musical jitter only — never use for security purposes.

    // Phase 44 Plan 44-05: registration owned by
    // RegisterHumanizeGaussianContextDependent + HumanizeGaussianStrict wrapper.
    // Legacy RegisterHumanizeGaussian + HumanizeGaussian private methods
    // deleted along with other pre-strict shapes.

    /// <summary>
    /// Shared body of humanizeGaussian extracted Phase 44 Plan 44-05 so the
    /// strict-aware <see cref="HumanizeGaussianStrict"/> can reuse the same
    /// renderer path with an already-validated amount.
    /// </summary>
    private static Value HumanizeGaussianCore(SequenceData seq, double amount, int seed)
    {
        if (amount == 0.0) return Value.Sequence(seq);               // D-10 short-circuit

        // D-03: LOCAL new Random(seed) scoped to THIS call; does NOT read or mutate
        // ExecutionContext.GetRand. Mirrors VariationFunctions.VarySeeded at :71-77.
        var rng = new Random(seed); // PRNG-SANCTIONED: explicit-seed humanizeGaussian overload (D-03)

        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            result.AddBar(HumanizeBar(bar, amount, rng));
        }
        return Value.Sequence(result);
    }

    /// <summary>
    /// Phase 35 HK-01 (BarRenderer.cs:62-77 mirror): humanize one bar with
    /// recursive ParallelVoices support. When the bar carries Phase 28 voice
    /// blocks, recurse into each voice sub-bar reusing the SAME seeded RNG —
    /// per RESEARCH §Pitfall 8, sharing the single seeded Random across all
    /// voices preserves the Phase 18/25 byte-identical determinism contract
    /// (T-35-04 mitigation). Pre-Phase-35 shape iterated only bar.MusicalNotes
    /// and dropped ParallelVoices entirely → silent 44-byte WAVs.
    /// </summary>
    private static BarData HumanizeBar(BarData bar, double amount, Random rng)
    {
        // Voice-block branch: recurse, preserving ParallelVoices in the
        // output bar. The parent bar's MusicalNotes list is the BarRenderer
        // convention for the whole-bar rest placeholder; humanize it the
        // same way so its rest stays a rest (IsRest fast-path) and any
        // future non-rest content on the parent bar still gets jittered.
        if (bar.ParallelVoices != null && bar.ParallelVoices.Count > 0)
        {
            var humanizedParent = HumanizeBarNotes(bar, amount, rng);
            var humanizedVoices = new List<BarData>(bar.ParallelVoices.Count);
            foreach (var voiceBar in bar.ParallelVoices)
            {
                // Each voice block is its own BarData. Each one may itself
                // recurse if it carries nested ParallelVoices (defensive —
                // the Phase 28 compiler emits one level today, but the
                // recursion is the safe shape).
                humanizedVoices.Add(HumanizeBar(voiceBar, amount, rng));
            }
            humanizedParent.ParallelVoices = humanizedVoices;
            return humanizedParent;
        }

        return HumanizeBarNotes(bar, amount, rng);
    }

    /// <summary>
    /// Inner humanize step — iterates a single bar's MusicalNotes list and
    /// builds a new BarData preserving the per-note With(velocity:) update.
    /// Extracted from HumanizeGaussian so HumanizeBar can reuse it on each
    /// recursion level (parent bar + each voice sub-bar).
    /// </summary>
    private static BarData HumanizeBarNotes(BarData bar, double amount, Random rng)
    {
        var newNotes = new List<MusicalNoteData>(bar.MusicalNotes.Count);
        foreach (var note in bar.MusicalNotes)
        {
            if (note.IsRest) { newNotes.Add(note); continue; }   // D-11

            double z = NextGaussianSample(rng);                  // D-05/D-06
            double velJitter = z * amount * 0.2;                 // D-07
            double newVelocity = Math.Clamp(note.Velocity + velJitter, 0.05, 1.0);  // D-09

            // RESEARCH §Critical Pre-Existing Bug: use With(velocity:) to preserve
            // all 17 MusicalNoteData fields (Plan 25-01 extended With with velocity slot).
            newNotes.Add(note.With(velocity: newVelocity));
        }
        return new BarData(newNotes, bar.TimeSignature!);
    }

    // D-17: extracted helper for testability — basic Box-Muller (cos branch).
    // Two NextDouble draws produce one N(0, 1) sample. Sin companion DISCARDED per D-06.
    // Math.Max(u1, 1e-300) guards Math.Log(0) divergence (Pitfall 2: Random.NextDouble
    // contract is [0, 1) so 0.0 IS a possible output; the 1e-300 floor produces a
    // worst-case ~37-stddev Gaussian sample, clamped at velocity boundary — benign).
    private static double NextGaussianSample(Random rng)
    {
        double u1 = rng.NextDouble();
        double u2 = rng.NextDouble();
        u1 = Math.Max(u1, 1e-300);  // guard log(0); see Pitfall 2 in 25-RESEARCH.md
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    // ===== Ornament Transforms (Trill / Tremolo) =====

    private static void RegisterOrnamentTransforms(InternalFunctionRegistry registry)
    {
        var trillSig = new FunctionSignature("trill",
            [SequenceType.Instance, SemitoneType.Instance],
            ParameterNames: ["seq", "interval"]);
        registry.Register("trill", trillSig, Trill);

        // sweep-260620 soft-overload: Cent slot routes to TrillCent with remainder folding
        // (CORRECTNESS — the Semitone/Int-strict slot previously threw "no matching overload"
        // for `(trill seq +50c)`). Models the Math.Truncate split on TransposeCent.
        var trillCentSig = new FunctionSignature("trill",
            [SequenceType.Instance, CentType.Instance],
            ParameterNames: ["seq", "interval"]);
        registry.Register("trill", trillCentSig, TrillCent);

        // Phase 44 Plan 44-05: tremolo registration moved to
        // RegisterContextDependent so its reps-clamp can read
        // context.CallerStrictMode at the leaf site.
    }

    private static Value Trill(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int semitones = args[1].As<int>();

        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            result.AddBar(TrillBar(bar, semitones));
        }
        return Value.Sequence(result);
    }

    /// <summary>
    /// sweep-260620 soft-overload: cent-precision trill. CentType's CLR backing IS double,
    /// so args[1].As&lt;double&gt;() reads the raw cents. The whole-semitone part drives the
    /// upper-neighbour interval; the fractional remainder folds into every emitted
    /// subdivision's CentOffset (mirrors the TransposeCent split + the TransposeBy fold).
    /// </summary>
    private static Value TrillCent(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double cents = args[1].As<double>();

        // Same truncate-toward-zero split as TransposeCent: +150c → +1st + 50c remainder.
        int semitones = (int)Math.Truncate(cents / 100.0);
        double centsRemainder = cents - semitones * 100.0;

        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            result.AddBar(TrillBar(bar, semitones, centsRemainder));
        }
        return Value.Sequence(result);
    }

    /// <summary>
    /// Audit 2026-06-09 §4.5: trills one bar, recursing into Phase 28 ParallelVoices.
    /// Fixes: (1) <b>dotted notes preserve their full duration</b> — the alternation count is
    /// derived from the source note's actual duration (dot included) divided by the trill
    /// subdivision, so a dotted half (3 beats) yields 6 eighths (3 beats) instead of the old
    /// fixed 4 eighths (2 beats); (2) the <b>upper neighbour carries CentOffset + Articulation</b>
    /// (rebuilt via <c>note.With(...)</c> off the source note, not a 12-arg ctor that defaulted
    /// them away). Each trill note is plain (isDotted: false) — the dot's duration is absorbed by
    /// the extra subdivision, not by dotting every subdivision.
    ///
    /// <para>Sweep 2026-06-14 fixes two more:</para>
    /// <para>(3) <b>chord brackets trill as a unit</b> — bar notes are grouped into
    /// (lead, [following IsChordTone notes]) runs. Each subdivision emits a fresh lead (and its
    /// upper neighbour on odd subdivisions) followed by the chord tones (and their own upper
    /// neighbours on odd subdivisions), all carrying IsChordTone=true so they stack on THAT
    /// subdivision's lead onset in <c>BarType.ToTimeline</c>. Previously every chord tone was
    /// expanded independently with IsChordTone=true, so all its subdivisions piled onto the
    /// lead's last onset — only the lead actually trilled, the upper voices collapsed to a
    /// single-onset cluster.</para>
    /// <para>(4) <b>tuplet notes honour DurationFraction</b> — when a source note carries a
    /// rational DurationFraction (e.g. a 2/3-quarter triplet), the subdivision is computed as an
    /// exact <see cref="Fraction"/> and emitted via <c>With(durationFraction:)</c>. The old enum
    /// path passed only <c>durationValue:</c>, so <c>With</c>'s null-coalesce preserved the OLD
    /// DurationFraction and every alternation kept the full tuplet-note duration — overflowing
    /// the bar (GetBeats takes the DurationFraction branch whenever it HasValue).</para>
    /// </summary>
    private static BarData TrillBar(BarData bar, int semitones, double centsRemainder = 0.0)
    {
        var newNotes = new List<MusicalNoteData>();
        var src = bar.MusicalNotes;
        for (int idx = 0; idx < src.Count; idx++)
        {
            var note = src[idx];
            if (note.IsRest || !note.DurationValue.HasValue)
            {
                newNotes.Add(note);
                continue;
            }

            // Group the lead note with any immediately-following chord tones ([C4 E4 G4]q →
            // C4 is the lead, E4/G4 carry IsChordTone=true). The whole chord trills in lockstep.
            int groupEnd = idx + 1;
            while (groupEnd < src.Count && src[groupEnd].IsChordTone)
                groupEnd++;

            int alternations = TrillAlternationCount(note);

            for (int i = 0; i < alternations; i++)
            {
                bool upperStep = i % 2 != 0;
                // Lead (or its upper neighbour on odd subdivisions): NOT a chord tone, so it
                // advances the timeline cursor for this subdivision.
                newNotes.Add(BuildTrillNote(note, semitones, upperStep, isChordTone: false, centsRemainder));
                // Chord tones stack on THIS subdivision's lead onset (isChordTone:true) and
                // trill in lockstep — same subdivision duration, same alternation step.
                for (int c = idx + 1; c < groupEnd; c++)
                    newNotes.Add(BuildTrillNote(src[c], semitones, upperStep, isChordTone: true, centsRemainder));
            }

            idx = groupEnd - 1; // skip the chord tones consumed by this group
        }
        var newBar = new BarData(newNotes, bar.TimeSignature!) { IsPickup = bar.IsPickup };
        if (bar.ParallelVoices != null)
        {
            var voices = new List<BarData>(bar.ParallelVoices.Count);
            foreach (var voiceBar in bar.ParallelVoices)
                voices.Add(TrillBar(voiceBar, semitones, centsRemainder));
            newBar.ParallelVoices = voices;
        }
        return newBar;
    }

    /// <summary>
    /// Sweep 2026-06-14: build one trill subdivision of <paramref name="note"/>. When
    /// <paramref name="upperStep"/> is true the pitch is raised by <paramref name="semitones"/>
    /// (the upper neighbour); otherwise the source pitch is kept. The per-subdivision duration
    /// honours a rational DurationFraction (tuplet notes) via the exact-fraction path so
    /// GetBeats sees the new shortened duration; plain power-of-2 notes keep the byte-identical
    /// enum path. <paramref name="isChordTone"/> stamps chord tones so they stack on the lead's
    /// onset in <see cref="BarType.ToTimeline"/>.
    /// </summary>
    private static MusicalNoteData BuildTrillNote(
        MusicalNoteData note, int semitones, bool upperStep, bool isChordTone, double centsRemainder = 0.0)
    {
        // Pitch override only on the upper-neighbour step; CentOffset / Articulation / Velocity
        // are carried by With(...) off the source note in both cases.
        char? name = null;
        int? oct = null;
        int? alt = null;
        if (upperStep)
        {
            int midi = ToMidi(note.NoteName, note.Octave, note.Alteration);
            int upperMidi = Math.Clamp(midi + semitones, MIDI_MIN, MIDI_MAX);
            (name, oct, alt) = FromMidi(upperMidi);
        }

        // sweep-260620: fold a cent-precision remainder (from the Cent trill overload) into
        // each emitted subdivision's CentOffset (default 0.0 keeps the Semitone path byte-identical).
        double? newCent = centsRemainder == 0.0
            ? null
            : (note.CentOffset ?? 0.0) + centsRemainder;

        if (note.DurationFraction.HasValue)
        {
            // Tuplet / rational note: emit an exact fraction = (base quarter-units) / alternations
            // so the whole trill fills exactly the source note's duration. Passing
            // durationFraction OVERRIDES the preserved old fraction (With coalesces non-null).
            int alternations = TrillAlternationCount(note);
            Fraction subFraction = BaseQuarterFraction(note) * new Fraction(1, alternations);
            return note.With(noteName: name, octave: oct, alteration: alt,
                durationFraction: subFraction, isDotted: false, isTied: false,
                isChordTone: isChordTone, centOffset: newCent);
        }

        // Plain power-of-2 path — byte-identical to the pre-sweep enum shape.
        int trillDur = Math.Min(note.DurationValue!.Value + 2, (int)NoteValueType.Value.THIRTYSECOND);
        return note.With(noteName: name, octave: oct, alteration: alt,
            durationValue: trillDur, isDotted: false, isTied: false,
            isChordTone: isChordTone, centOffset: newCent);
    }

    /// <summary>
    /// Sweep 2026-06-14: the alternation count for one trilled note, valid for both the
    /// enum and the rational (tuplet) paths. For plain notes it equals duration /
    /// trill-subdivision (e.g. half / eighth = 4; dotted half / eighth = 6), always ≥ 2.
    /// For rational notes the subdivision target is the same enum-derived trill duration, so
    /// the count matches a non-tuplet note of the nearest enum duration (≥ 2). This supersedes
    /// the old TrillSubdivisions helper, which ignored DurationFraction entirely.
    /// </summary>
    private static int TrillAlternationCount(MusicalNoteData note)
    {
        int trillDur = Math.Min(note.DurationValue!.Value + 2, (int)NoteValueType.Value.THIRTYSECOND);
        double subFraction = NoteValueType.ToFraction((NoteValueType.Value)trillDur);
        double baseFraction;
        if (note.DurationFraction.HasValue)
        {
            // Rational duration in quarter-units → whole-note units (÷4) to match ToFraction.
            var f = note.DurationFraction.Value;
            baseFraction = (double)f.Num / (f.Denom * 4.0);
        }
        else
        {
            baseFraction = NoteValueType.ToFraction((NoteValueType.Value)note.DurationValue!.Value);
            if (note.IsDotted) baseFraction *= 1.5;
        }
        int count = (int)Math.Round(baseFraction / subFraction);
        return Math.Max(count, 2);
    }

    // Sweep 2026-06-14: TrillSubdivisions was superseded by TrillAlternationCount (which
    // also handles the rational tuplet path); removed as pure redundancy.

    // Phase 44 Plan 44-05: Tremolo registration owned by
    // RegisterTremoloContextDependent + TremoloStrict wrapper. The legacy
    // non-context-dep Tremolo method was deleted along with other pre-strict shapes.

    /// <summary>
    /// Shared body of tremolo extracted Phase 44 Plan 44-05. The strict-aware
    /// TremoloStrict delegates here directly.
    /// </summary>
    private static Value TremoloCore(SequenceData seq, int reps)
    {
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            result.AddBar(TremoloBar(bar, reps));
        }
        return Value.Sequence(result);
    }

    /// <summary>
    /// Audit 2026-06-09 §4.5: tremolo one bar, recursing into Phase 28 ParallelVoices.
    /// The subdivision is now <b>derived from <paramref name="reps"/></b> so the N repetitions
    /// fill exactly the source note's duration (dot included) — previously a FIXED quarter
    /// subdivision (<c>DurationValue+2</c>) only preserved total length at reps=4, doubling it
    /// at reps=8 and halving it at reps=2. For a plain note with a power-of-2 reps the enum
    /// path is kept (so the reps=4 doc example renders byte-identical); dotted notes and
    /// non-power-of-2 reps use an exact <c>DurationFraction = base / reps</c> in quarter-units.
    ///
    /// <para>Sweep 2026-06-14: <b>chord brackets tremolo as a unit</b> — bar notes are grouped
    /// into (lead, [following IsChordTone notes]) runs. Each repetition emits a fresh lead
    /// followed by the chord tones (IsChordTone=true) so they stack on THAT repetition's lead
    /// onset in <see cref="BarType.ToTimeline"/> and repeat in lockstep. Previously each chord
    /// tone was expanded independently with IsChordTone=true, so all its repeats piled onto the
    /// lead's last onset — only the lead actually tremolo'd, the upper voices collapsed to a
    /// single-onset cluster.</para>
    /// </summary>
    private static BarData TremoloBar(BarData bar, int reps)
    {
        var newNotes = new List<MusicalNoteData>();
        var src = bar.MusicalNotes;
        for (int idx = 0; idx < src.Count; idx++)
        {
            var note = src[idx];
            if (note.IsRest || !note.DurationValue.HasValue)
            {
                newNotes.Add(note);
                continue;
            }

            // Group the lead note with any immediately-following chord tones.
            int groupEnd = idx + 1;
            while (groupEnd < src.Count && src[groupEnd].IsChordTone)
                groupEnd++;

            for (int i = 0; i < reps; i++)
            {
                // Lead repetition (NOT a chord tone — advances the timeline cursor).
                newNotes.Add(BuildTremoloNote(note, reps, isChordTone: false));
                // Chord tones stack on THIS repetition's lead onset and repeat in lockstep.
                for (int c = idx + 1; c < groupEnd; c++)
                    newNotes.Add(BuildTremoloNote(src[c], reps, isChordTone: true));
            }

            idx = groupEnd - 1; // skip the chord tones consumed by this group
        }
        var newBar = new BarData(newNotes, bar.TimeSignature!) { IsPickup = bar.IsPickup };
        if (bar.ParallelVoices != null)
        {
            var voices = new List<BarData>(bar.ParallelVoices.Count);
            foreach (var voiceBar in bar.ParallelVoices)
                voices.Add(TremoloBar(voiceBar, reps));
            newBar.ParallelVoices = voices;
        }
        return newBar;
    }

    /// <summary>
    /// Sweep 2026-06-14: build one tremolo repetition of <paramref name="note"/>. Keeps the
    /// pre-sweep byte-identical enum path for plain power-of-2-reps notes; dotted / non-power-of-2
    /// / rational notes use the exact <c>DurationFraction = base / reps</c> path.
    /// <paramref name="isChordTone"/> stamps chord tones so they stack on the lead's onset.
    /// </summary>
    private static MusicalNoteData BuildTremoloNote(MusicalNoteData note, int reps, bool isChordTone)
    {
        int dv = note.DurationValue!.Value;
        int log2 = Log2Exact(reps);
        // Power-of-2 reps on a plain (non-dotted, non-rational) note: the subdivision is a
        // clean enum value (base + log2(reps)). Keep this path so the reps=4 doc example and
        // every existing power-of-2 plain-note tremolo stay byte-identical (LEDGER).
        if (log2 >= 0 && !note.IsDotted && !note.DurationFraction.HasValue
            && dv + log2 <= (int)NoteValueType.Value.THIRTYSECOND)
        {
            int subDur = dv + log2;
            return note.With(durationValue: subDur, isDotted: false, isTied: false,
                isChordTone: isChordTone);
        }
        // Exact rational subdivision = (note's quarter-unit duration) / reps. Covers dotted
        // notes (×1.5 preserved), non-power-of-2 reps, and enum-overflow cases.
        Fraction subFraction = BaseQuarterFraction(note) * new Fraction(1, reps);
        return note.With(durationFraction: subFraction, isDotted: false, isTied: false,
            isChordTone: isChordTone);
    }

    /// <summary>The source note's full duration (dot included) expressed in quarter-note units
    /// as an exact <see cref="Fraction"/>. Mirrors <see cref="MusicalNoteData.GetBeats"/>'s
    /// rational convention (DurationFraction is quarter-units). Enum value <c>dv</c> has
    /// whole-fraction <c>1/2^dv</c> → quarter-units <c>4/2^dv</c>; a dot multiplies by 3/2.</summary>
    private static Fraction BaseQuarterFraction(MusicalNoteData note)
    {
        if (note.DurationFraction.HasValue) return note.DurationFraction.Value;
        int dv = note.DurationValue!.Value;
        var qf = new Fraction(4, 1 << dv);
        if (note.IsDotted) qf = qf * new Fraction(3, 2);
        return qf;
    }

    /// <summary>Returns log2(n) when n is a positive power of two, else -1.</summary>
    private static int Log2Exact(int n)
    {
        if (n <= 0 || (n & (n - 1)) != 0) return -1;
        int log = 0;
        while ((1 << log) < n) log++;
        return log;
    }
}
