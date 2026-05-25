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
        // remain in this file but are unreferenced after Phase 44 Plan 44-05;
        // kept as documentation of the pre-strict shape.)
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
        return HumanizeCore(seq, amount);
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
        // resolution → subdivision length in beats (where 1 beat == 1/timesig.Denominator whole).
        // QUARTER at 4/4 = 1 beat per subdivision; EIGHTH at 4/4 = 0.5 beats; SIXTEENTH = 0.25 beats.
        double subdivBeats = NoteValueToBeats(resolution, timesig.Denominator);
        // CONTEXT D-04: linear swing offset = swing × (subdivBeats / 2).
        double swingOffset = swing * (subdivBeats / 2.0);

        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            double currentBeat = 0.0;
            int subdivIdx = 0;
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

                currentBeat += note.GetBeats(timesig.Denominator);
                subdivIdx++;
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature ?? timesig));
        }
        return result;
    }

    /// <summary>
    /// DX-13: converts a NoteValue resolution into beats per subdivision for the active
    /// time signature. Charitable D-07: out-of-range enum values fall through to the
    /// default arm and are treated as a quarter note (no exception, no crash).
    /// </summary>
    private static double NoteValueToBeats(NoteValueType.Value nv, int denom)
    {
        // 1 beat == 1/denom whole. So WHOLE = denom beats; QUARTER = denom/4 beats.
        double whole = denom;
        return nv switch
        {
            NoteValueType.Value.WHOLE        => whole,
            NoteValueType.Value.HALF         => whole / 2,
            NoteValueType.Value.QUARTER      => whole / 4,
            NoteValueType.Value.EIGHTH       => whole / 8,
            NoteValueType.Value.SIXTEENTH    => whole / 16,
            NoteValueType.Value.THIRTYSECOND => whole / 32,
            _                                => whole / 4,
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
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                newNotes.Add(transform(note));
            }
            var newBar = new BarData(newNotes, bar.TimeSignature!);
            result.AddBar(newBar);
        }
        return result;
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

        var result = TransformNotes(seq, note =>
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
            return new MusicalNoteData(name, oct, alt, note.DurationValue, isRest: false, note.CentOffset, note.IsTied, note.Velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength);
        });

        return Value.Sequence(result);
    }

    private static Value TransposeCent(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double cents = args[1].As<double>();
        int semitones = (int)Math.Round(cents / 100.0);

        if (Math.Abs(cents - semitones * 100.0) > 0.01)
        {
            Console.Error.WriteLine(
                $"Warning: transpose by {cents}c rounded to {semitones} semitones (not an exact multiple of 100c)");
        }

        // Delegate to semitone transpose
        return TransposeSemitone([args[0], Value.Semitone(semitones)]);
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

        // Find the first non-rest note across all bars (the axis)
        int? axisMidi = null;
        foreach (var bar in seq.Bars)
        {
            foreach (var note in bar.MusicalNotes)
            {
                if (!note.IsRest)
                {
                    axisMidi = ToMidi(note.NoteName, note.Octave, note.Alteration);
                    break;
                }
            }
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
            return new MusicalNoteData(name, oct, alt, note.DurationValue, isRest: false, note.CentOffset, note.IsTied, note.Velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength);
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

        // True retrograde: reverse both the bar order AND the notes within each bar
        var reversedBars = new List<BarData>();
        foreach (var bar in seq.Bars)
        {
            var reversedNotes = new List<MusicalNoteData>(bar.MusicalNotes);
            reversedNotes.Reverse();
            reversedBars.Add(new BarData(reversedNotes, bar.TimeSignature!));
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
                return new MusicalNoteData(
                    note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    note.Velocity, note.Articulation, note.IsDotted,
                    note.SourceLocation, note.SourceLength,
                    durationFraction: doubled);
            }

            if (!note.DurationValue.HasValue) return note;

            int newDur = note.DurationValue.Value - 1; // toward WHOLE=0
            if (newDur < (int)NoteValueType.Value.WHOLE)
            {
                Console.Error.WriteLine("Warning: augment clamped duration at whole note");
                newDur = (int)NoteValueType.Value.WHOLE;
            }

            return new MusicalNoteData(note.NoteName, note.Octave, note.Alteration, newDur, note.IsRest, note.CentOffset, note.IsTied, note.Velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength);
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
                return new MusicalNoteData(
                    note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    note.Velocity, note.Articulation, note.IsDotted,
                    note.SourceLocation, note.SourceLength,
                    durationFraction: halved);
            }

            if (!note.DurationValue.HasValue) return note;

            int newDur = note.DurationValue.Value + 1; // toward THIRTYSECOND=5
            if (newDur > (int)NoteValueType.Value.THIRTYSECOND)
            {
                Console.Error.WriteLine("Warning: diminish clamped duration at thirty-second note");
                newDur = (int)NoteValueType.Value.THIRTYSECOND;
            }

            return new MusicalNoteData(note.NoteName, note.Octave, note.Alteration, newDur, note.IsRest, note.CentOffset, note.IsTied, note.Velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength);
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
                var newBar = new BarData(new List<MusicalNoteData>(bar.MusicalNotes), bar.TimeSignature!);
                result.AddBar(newBar);
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
            foreach (var bar in seq.Bars)
            {
                var newNotes = new List<MusicalNoteData>();
                foreach (var note in bar.MusicalNotes)
                {
                    if (note.IsRest)
                    {
                        newNotes.Add(note);
                        continue;
                    }

                    int midi = ToMidi(note.NoteName, note.Octave, note.Alteration) + cumulativeTranspose;
                    midi = Math.Clamp(midi, MIDI_MIN, MIDI_MAX);
                    var (name, oct, alt) = FromMidi(midi);
                    newNotes.Add(new MusicalNoteData(name, oct, alt, note.DurationValue, isRest: false, note.CentOffset, note.IsTied, note.Velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
                }
                var newBar = new BarData(newNotes, bar.TimeSignature!);
                result.AddBar(newBar);
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
        foreach (var bar in seqA.Bars)
        {
            var newBar = new BarData(new List<MusicalNoteData>(bar.MusicalNotes), bar.TimeSignature!);
            result.AddBar(newBar);
        }
        foreach (var bar in seqB.Bars)
        {
            var newBar = new BarData(new List<MusicalNoteData>(bar.MusicalNotes), bar.TimeSignature!);
            result.AddBar(newBar);
        }
        return Value.Sequence(result);
    }

    // ===== Dynamic Transforms =====

    private static void RegisterDynamicTransforms(InternalFunctionRegistry registry)
    {
        var crescSig = new FunctionSignature("crescendo",
            [SequenceType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "startVel", "endVel"]);
        registry.Register("crescendo", crescSig, Crescendo);

        var decrescSig = new FunctionSignature("decrescendo",
            [SequenceType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "startVel", "endVel"]);
        registry.Register("decrescendo", decrescSig, Decrescendo);

        var swellSig = new FunctionSignature("swell",
            [SequenceType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "edgeVel", "peakVel"]);
        registry.Register("swell", swellSig, Swell);
    }

    private static Value Crescendo(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double startVel = Math.Clamp(args[1].As<double>(), 0.0, 1.0);
        double endVel = Math.Clamp(args[2].As<double>(), 0.0, 1.0);
        return Value.Sequence(ApplyVelocityGradient(seq, startVel, endVel));
    }

    private static Value Decrescendo(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double startVel = Math.Clamp(args[1].As<double>(), 0.0, 1.0);
        double endVel = Math.Clamp(args[2].As<double>(), 0.0, 1.0);
        // Reverse the velocity gradient: decrescendo goes from endVel down to startVel
        return Value.Sequence(ApplyVelocityGradient(seq, endVel, startVel));
    }

    private static Value Swell(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double edgeVel = Math.Clamp(args[1].As<double>(), 0.0, 1.0);
        double peakVel = Math.Clamp(args[2].As<double>(), 0.0, 1.0);
        return SwellCore(seq, edgeVel, peakVel);
    }

    /// <summary>
    /// Shared body of swell extracted Phase 44 Plan 44-05 so the strict-aware
    /// <see cref="SwellStrict"/> can call the same code path with already-validated
    /// raw values (strict skips the clamp; non-strict pre-clamps).
    /// </summary>
    private static Value SwellCore(SequenceData seq, double edgeVel, double peakVel)
    {
        int totalNotes = 0;
        foreach (var bar in seq.Bars)
            foreach (var note in bar.MusicalNotes)
                if (!note.IsRest) totalNotes++;

        if (totalNotes <= 1)
            return Value.Sequence(seq);

        int midpoint = totalNotes / 2;
        int noteIndex = 0;

        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest)
                {
                    newNotes.Add(note);
                    continue;
                }

                double t;
                int descendLength = totalNotes - 1 - midpoint;
                if (noteIndex <= midpoint && midpoint > 0)
                    t = (double)noteIndex / midpoint;
                else if (descendLength > 0)
                    t = 1.0 - ((double)(noteIndex - midpoint) / descendLength);
                else
                    t = 1.0;

                double velocity = Math.Clamp(edgeVel + t * (peakVel - edgeVel), 0.0, 1.0);

                newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
                noteIndex++;
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return Value.Sequence(result);
    }

    private static SequenceData ApplyVelocityGradient(SequenceData seq, double startVel, double endVel)
    {
        int totalNotes = 0;
        foreach (var bar in seq.Bars)
            foreach (var note in bar.MusicalNotes)
                if (!note.IsRest) totalNotes++;

        if (totalNotes <= 1)
        {
            return TransformNotes(seq, note =>
            {
                if (note.IsRest) return note;
                return new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    startVel, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength);
            });
        }

        int noteIndex = 0;
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest)
                {
                    newNotes.Add(note);
                    continue;
                }

                double t = (double)noteIndex / (totalNotes - 1);
                double velocity = Math.Clamp(startVel + t * (endVel - startVel), 0.0, 1.0);

                newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
                noteIndex++;
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return result;
    }

    // ===== Tempo Transforms =====

    private static void RegisterTempoTransforms(InternalFunctionRegistry registry)
    {
        var ritSig = new FunctionSignature("ritardando",
            [SequenceType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "amount"]);
        registry.Register("ritardando", ritSig, RitardandoTransform);

        var accelSig = new FunctionSignature("accelerando",
            [SequenceType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "amount"]);
        registry.Register("accelerando", accelSig, AccelerandoTransform);

        var fermataSig = new FunctionSignature("fermata",
            [SequenceType.Instance, IntType.Instance],
            ParameterNames: ["seq", "index"]);
        registry.Register("fermata", fermataSig, FermataTransform);
    }

    /// <summary>
    /// Ritardando: progressively stretch note durations. Amount 0.5 = last note 1.5x duration.
    /// We approximate by adjusting velocity downward for later notes (lower velocity sounds
    /// "slower" perceptually).
    /// </summary>
    private static Value RitardandoTransform(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double amount = Math.Clamp(args[1].As<double>(), 0.0, 1.0);
        return RitardandoCore(seq, amount);
    }

    /// <summary>
    /// Shared body of ritardando extracted Phase 44 Plan 44-05 so the
    /// strict-aware <see cref="RitardandoStrict"/> can call the same code path
    /// with an already-validated amount (strict skips the clamp; non-strict pre-clamps).
    /// </summary>
    private static Value RitardandoCore(SequenceData seq, double amount)
    {
        int totalNotes = 0;
        foreach (var bar in seq.Bars)
            foreach (var note in bar.MusicalNotes)
                if (!note.IsRest) totalNotes++;

        if (totalNotes <= 1) return Value.Sequence(seq);

        int noteIndex = 0;
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest) { newNotes.Add(note); continue; }

                double t = (double)noteIndex / (totalNotes - 1);
                // Reduce velocity slightly for rit feel (later = softer = perceived slower)
                double velReduction = t * amount * 0.3;
                double newVel = Math.Clamp(note.Velocity - velReduction, 0.05, 1.0);

                newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    newVel, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
                noteIndex++;
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return Value.Sequence(result);
    }

    private static Value AccelerandoTransform(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double amount = Math.Clamp(args[1].As<double>(), 0.0, 1.0);
        return AccelerandoCore(seq, amount);
    }

    /// <summary>
    /// Shared body of accelerando extracted Phase 44 Plan 44-05.
    /// </summary>
    private static Value AccelerandoCore(SequenceData seq, double amount)
    {
        int totalNotes = 0;
        foreach (var bar in seq.Bars)
            foreach (var note in bar.MusicalNotes)
                if (!note.IsRest) totalNotes++;

        if (totalNotes <= 1) return Value.Sequence(seq);

        int noteIndex = 0;
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest) { newNotes.Add(note); continue; }

                double t = (double)noteIndex / (totalNotes - 1);
                // Increase velocity slightly for accel feel (later = louder = perceived faster)
                double velBoost = t * amount * 0.3;
                double newVel = Math.Clamp(note.Velocity + velBoost, 0.05, 1.0);

                newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    newVel, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
                noteIndex++;
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
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

        int noteIndex = 0;
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (!note.IsRest && noteIndex == targetIdx && note.DurationValue.HasValue)
                {
                    // Augment: move to next larger duration (e.g. quarter -> half)
                    int newDur = Math.Max(note.DurationValue.Value - 1, (int)NoteValueType.Value.WHOLE);
                    newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                        newDur, note.IsRest, note.CentOffset, note.IsTied,
                        note.Velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
                }
                else
                {
                    newNotes.Add(note);
                }
                if (!note.IsRest) noteIndex++;
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return Value.Sequence(result);
    }

    // ===== Humanize =====

    private static void RegisterHumanize(InternalFunctionRegistry registry)
    {
        var humanizeSig = new FunctionSignature("humanize",
            [SequenceType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "amount"]);
        registry.Register("humanize", humanizeSig, Humanize);
    }

    private static readonly Random HumanizeRng = new();

    private static Value Humanize(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double amount = Math.Clamp(args[1].As<double>(), 0.0, 1.0);
        return HumanizeCore(seq, amount);
    }

    /// <summary>
    /// Shared body of humanize extracted Phase 44 Plan 44-05.
    /// </summary>
    private static Value HumanizeCore(SequenceData seq, double amount)
    {
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest)
                {
                    newNotes.Add(note);
                    continue;
                }

                // Velocity jitter: random variation scaled by amount
                double velJitter = (HumanizeRng.NextDouble() * 2.0 - 1.0) * amount * 0.2;
                double newVelocity = Math.Clamp(note.Velocity + velJitter, 0.05, 1.0);

                newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    newVelocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return Value.Sequence(result);
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

    private static void RegisterHumanizeGaussian(InternalFunctionRegistry registry)
    {
        var sig = new FunctionSignature("humanizeGaussian",
            [SequenceType.Instance, DoubleType.Instance, IntType.Instance],
            ParameterNames: ["seq", "amount", "seed"]);
        registry.Register("humanizeGaussian", sig, HumanizeGaussian);
    }

    private static Value HumanizeGaussian(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double amount = Math.Clamp(args[1].As<double>(), 0.0, 1.0);  // D-08
        int seed = args[2].As<int>();                                // D-15
        return HumanizeGaussianCore(seq, amount, seed);
    }

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
        var rng = new Random(seed);

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
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest || !note.DurationValue.HasValue)
                {
                    newNotes.Add(note);
                    continue;
                }

                // Split into rapid alternation: note -> upper -> note -> upper
                int trillDur = Math.Min(note.DurationValue.Value + 2, (int)NoteValueType.Value.THIRTYSECOND);
                int midi = ToMidi(note.NoteName, note.Octave, note.Alteration);
                int upperMidi = Math.Clamp(midi + semitones, MIDI_MIN, MIDI_MAX);
                var (upperName, upperOct, upperAlt) = FromMidi(upperMidi);

                // 4 alternations
                for (int i = 0; i < 4; i++)
                {
                    if (i % 2 == 0)
                        newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                            trillDur, false, note.CentOffset, false, note.Velocity, note.Articulation, sourceLocation: note.SourceLocation, sourceLength: note.SourceLength));
                    else
                        newNotes.Add(new MusicalNoteData(upperName, upperOct, upperAlt,
                            trillDur, false, velocity: note.Velocity, sourceLocation: note.SourceLocation, sourceLength: note.SourceLength));
                }
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return Value.Sequence(result);
    }

    private static Value Tremolo(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int reps = Math.Clamp(args[1].As<int>(), 1, 16);
        return TremoloCore(seq, reps);
    }

    /// <summary>
    /// Shared body of tremolo extracted Phase 44 Plan 44-05.
    /// </summary>
    private static Value TremoloCore(SequenceData seq, int reps)
    {
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest || !note.DurationValue.HasValue)
                {
                    newNotes.Add(note);
                    continue;
                }

                // Subdivide: use a smaller duration for each repetition
                int subDur = Math.Min(note.DurationValue.Value + 2, (int)NoteValueType.Value.THIRTYSECOND);
                for (int i = 0; i < reps; i++)
                {
                    newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                        subDur, false, note.CentOffset, false, note.Velocity, note.Articulation, sourceLocation: note.SourceLocation, sourceLength: note.SourceLength));
                }
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return Value.Sequence(result);
    }
}
