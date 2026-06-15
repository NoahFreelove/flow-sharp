using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
// Disambiguate FlowLang.Runtime.ExecutionContext from System.Threading.ExecutionContext
// — the bare name is ambiguous under net10.0's implicit usings.
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.StandardLibrary.Patterns;

/// <summary>
/// Phase 36 Plan 36-05 (PAT-01 / PAT-02 / GEN-05): 13 Tidal-style combinators
/// on <see cref="SequenceData"/>. Composer surface is the headline ergonomic
/// from RESEARCH § Code Examples:
/// <code>
///   seq -> (every 4 (fn s =&gt; (fast s 2))) as varied
///       -&gt; (sometimes 0.3 rev)
///       -&gt; (jux (fn s =&gt; (transpose s 7))) as stereo
///       -&gt; render
/// </code>
///
/// <para>
/// <b>D-36-01</b> ships 12 combinators (every / fast / slow / chunk / phase /
/// rev / jux / sometimes / iter / palindrome / degrade / superimpose), <b>D-36-02</b>
/// adds the Flow-native helper <c>sparseSeq</c>. <b>D-36-03</b> mandates the
/// lambda-required style — every transform-arg combinator (every / chunk /
/// sometimes / jux / superimpose) takes a <c>Function</c> as its transform
/// argument; no partial application. <b>D-36-04</b> sets the cycle unit for
/// cycle-dependent combinators (every / chunk / phase) to <b>bars</b>. <b>D-36-05</b>
/// keeps <c>sometimes</c> probability-explicit with a default-arg
/// <c>(sometimes fn seq)</c> overload at prob=0.5.
/// </para>
///
/// <para>
/// <b>Stochastic-combinator PRNG threading (D-v1.5-06 / D-36-09):</b> the four
/// stochastic combinators (sometimes / degrade / sparseSeq plus the
/// default-prob sometimes overload) route their PRNG through
/// <see cref="ExecutionContext.PrngRegistry"/> keyed by
/// <c>(ExecutionContext.CurrentCallSite, &lt;name&gt;)</c>. The call-site
/// SourceLocation is set by <c>ExpressionEvaluator.EvaluateFunctionCall</c>
/// immediately before invoking the registered C# lambda. Direct
/// <c>new Random(...)</c> construction is BANNED — the source-grep CI gate
/// (<c>PrngRegistryNewRandomGateTests</c>) keeps this file honest.
/// </para>
///
/// <para>
/// <b>Charitable interpretation (PAT-02 + Pitfall 2 + Pitfall 9):</b> every
/// combinator returns its input unchanged + emits a one-shot stderr advisory
/// via <see cref="RenderingDiagnostics.WarnOnce"/> when called on degenerate
/// input (empty sequence, n &lt;= 0, factor == 0, NaN/Infinity offsets, prob
/// outside [0,1]). NEVER throws.
/// </para>
/// </summary>
public static class PatternFunctions
{
    // ====================================================================
    // Registration entry point
    // ====================================================================

    /// <summary>
    /// Wires all 13 combinators into the internal function registry. Called
    /// from <c>FlowEngine</c> engine init alongside
    /// <c>HarmonyFunctions.RegisterContextDependent</c> and
    /// <c>TransformFunctions.RegisterContextDependent</c>. The
    /// <see cref="ExecutionContext"/> is captured by the registered lambdas
    /// so they can invoke composer-supplied lambda callbacks via
    /// <see cref="InvokeCallback"/> and access the per-context
    /// <see cref="ExecutionContext.PrngRegistry"/> for stochastic combinators.
    /// </summary>
    public static void RegisterContextDependent(
        InternalFunctionRegistry registry,
        ExecutionContext context)
    {
        // 10 deterministic combinators — D-36-01 PAT-01 baseline.
        // Phase 44 Plan 44-06: every combinator now threads `context` so each
        // WarnOnce leaf site can read `context.CallerStrictMode` and elevate
        // the advisory to an ErrorReporter `[strict]` error when called from a
        // strict file (D-05 + D-06 + D-07). Non-strict path remains
        // byte-identical (Pitfall 5).
        RegisterEvery(registry, context);
        RegisterFast(registry, context);
        RegisterSlow(registry, context);
        RegisterChunk(registry, context);
        RegisterPhase(registry, context);
        RegisterRev(registry, context);
        RegisterIter(registry, context);
        RegisterPalindrome(registry, context);
        RegisterJux(registry, context);
        RegisterSuperimpose(registry, context);

        // 3 stochastic combinators (sometimes ships in 2 overloads) — D-36-02 +
        // D-36-09. All PRNG flows through context.PrngRegistry keyed by
        // (CurrentCallSite, <combinator-name>); ZERO `new Random(` constructions
        // in this file (CI gate via PrngRegistryNewRandomGateTests).
        RegisterSometimes(registry, context);
        RegisterDegrade(registry, context);
        RegisterSparseSeq(registry, context);
    }

    // ====================================================================
    // Lambda invocation helper (Phase 26.1 dict pattern; D-36-03)
    // ====================================================================

    /// <summary>
    /// Invokes a composer-supplied <see cref="FunctionOverload"/> with the given
    /// args, dispatching to the C# impl for internal procs OR to the
    /// user-function invoker (with closure captures) for user-defined lambdas.
    /// Mirrors <c>DictFunctions.InvokeCallback</c> at <c>DictFunctions.cs:41-46</c>.
    /// </summary>
    private static Value InvokeCallback(
        ExecutionContext context,
        FunctionOverload cb,
        List<Value> args)
    {
        return cb.IsInternal
            ? cb.Implementation!(args)
            : context.Invoker!.ExecuteUserFunctionWithCaptures(
                cb.Declaration!, args, cb.CapturedVariables);
    }

    // ====================================================================
    // Shared utilities (charitable-interpretation + bar/note helpers)
    // ====================================================================

    /// <summary>
    /// Empty-seq guard. Returns true and emits a one-shot advisory when
    /// <paramref name="seq"/> has zero bars. Caller returns the input
    /// unchanged after this fires (Pitfall 9 — charitable interpretation).
    ///
    /// <para>Phase 44 Plan 44-06: under strict mode the advisory is reported
    /// as a composer-facing [strict] error via ErrorReporter. The non-strict
    /// path remains byte-identical (Pitfall 5 two-run cmp-clean preserved).</para>
    /// </summary>
    private static bool IsEmptySeqAdvisory(SequenceData seq, string name, ExecutionContext ctx)
    {
        if (seq.Bars.Count > 0) return false;
        // Phase 44 review CR-03: dedup strict-elevated advisory per
        // ExecutionContext lifetime. Each combinator-name + call-site pair
        // emits at most one [strict] error per process — mirrors the WarnOnce
        // sentinel discipline in the non-strict path. Critical because higher-
        // order combinators (`each chunks (fn s => (every 4 cb s))` with
        // some degenerate chunk) record one strict error per iteration.
        var sentinel = $"{name}:empty:{ctx.CurrentCallSite}";
        if (ctx.CallerStrictMode)
        {
            if (ctx.StrictAdvisoryDedup.Add(sentinel))
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [{name}] empty sequence at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
            }
            return true;
        }
        RenderingDiagnostics.WarnOnce(
            sentinel,
            $"[{name}] empty sequence at {ctx.CurrentCallSite}; returned unchanged");
        return true;
    }

    /// <summary>
    /// Constructs a new SequenceData with the given bars copied (defensive copy
    /// — same shape pattern <c>TransformFunctions.Repeat</c> uses to avoid
    /// aliasing the input sequence's bar list).
    /// </summary>
    private static SequenceData FromBars(IEnumerable<BarData> bars)
    {
        var result = new SequenceData();
        foreach (var b in bars) result.AddBar(b);
        return result;
    }

    /// <summary>
    /// Wraps a single bar in a single-bar SequenceData so a composer-supplied
    /// lambda can be invoked with a Sequence argument. Used by
    /// <c>every</c> / <c>chunk</c> when the transform-arg lambda's signature
    /// is <c>(Sequence =&gt; Sequence)</c>.
    /// </summary>
    private static SequenceData SingleBarSeq(BarData bar)
    {
        var seq = new SequenceData();
        seq.AddBar(bar);
        return seq;
    }

    /// <summary>
    /// Clones a BarData preserving notes, time signature, pickup flag, and any
    /// Phase 28 ParallelVoices. Used when we want to copy a bar through
    /// unchanged into the output sequence.
    /// </summary>
    private static BarData CloneBar(BarData b)
    {
        var clone = new BarData(new List<MusicalNoteData>(b.MusicalNotes), b.TimeSignature!)
        {
            IsPickup = b.IsPickup,
            ParallelVoices = b.ParallelVoices == null
                ? null
                : new List<BarData>(b.ParallelVoices)
        };
        return clone;
    }

    // ====================================================================
    // 1. every — (Int n, Function fn, Sequence seq) -> Sequence  (D-36-04 BARS)
    // ====================================================================

    private static void RegisterEvery(InternalFunctionRegistry registry, ExecutionContext context)
    {
        var sig = new FunctionSignature("every",
            [IntType.Instance, FunctionType.Instance, SequenceType.Instance],
            ParameterNames: ["n", "fn", "seq"]);
        registry.Register("every", sig, args => Every(args, context));
    }

    /// <summary>
    /// <c>(every n fn seq)</c> applies the lambda <c>fn</c> to bar <c>i</c>
    /// whenever <c>i % n == 0</c>. Cycle unit is <b>bars</b> per D-36-04: a
    /// 4-bar sequence with <c>(every 2 ...)</c> transforms bars 0 and 2 and
    /// passes bars 1 and 3 through unchanged. Charitable on <c>n &lt;= 0</c>.
    /// </summary>
    private static Value Every(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        int n = args[0].As<int>();
        var fn = args[1].As<FunctionOverload>();
        var seq = args[2].As<SequenceData>();

        if (n <= 0)
        {
            // Phase 44 review WR-01: this site was missed in the Plan 44-06
            // Axis B strict-elevation pass. Every other charitable advisory
            // in PatternFunctions branches on ctx.CallerStrictMode and emits
            // a [strict] error before falling through to WarnOnce. Match the
            // sibling pattern + CR-03 dedup gate.
            var sentinel = $"every:invalid-n:{ctx.CurrentCallSite}";
            if (ctx.CallerStrictMode)
            {
                if (ctx.StrictAdvisoryDedup.Add(sentinel))
                {
                    ctx.ErrorReporter.ReportError(
                        $"[strict] [every] n must be > 0 (got {n}) at {ctx.CurrentCallSite}",
                        ctx.CurrentCallSite);
                }
                return Value.Sequence(seq);
            }
            RenderingDiagnostics.WarnOnce(
                sentinel,
                $"[every] n must be > 0 (got {n}) at {ctx.CurrentCallSite}; sequence unchanged");
            return Value.Sequence(seq);
        }
        if (IsEmptySeqAdvisory(seq, "every", ctx)) return Value.Sequence(seq);

        var output = new List<BarData>(seq.Bars.Count);
        for (int i = 0; i < seq.Bars.Count; i++)
        {
            if (i % n == 0)
            {
                // Wrap bar i in a synthetic single-bar Sequence, invoke fn,
                // splice fn's resulting bars into the output.
                var single = SingleBarSeq(seq.Bars[i]);
                var lambdaResult = InvokeCallback(
                    ctx, fn,
                    new List<Value> { Value.Sequence(single) });

                if (lambdaResult.Data is SequenceData transformed)
                {
                    foreach (var b in transformed.Bars) output.Add(b);
                }
                else
                {
                    // Lambda returned non-Sequence — charitable: pass through.
                    // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
                    if (ctx.CallerStrictMode)
                    {
                        ctx.ErrorReporter.ReportError(
                            $"[strict] [every] lambda at {ctx.CurrentCallSite} did not return Sequence",
                            ctx.CurrentCallSite);
                        return Value.Sequence(seq);
                    }
                    RenderingDiagnostics.WarnOnce(
                        $"every:non-sequence-fn:{ctx.CurrentCallSite}",
                        $"[every] lambda at {ctx.CurrentCallSite} did not return Sequence; bar passed through unchanged");
                    output.Add(seq.Bars[i]);
                }
            }
            else
            {
                output.Add(seq.Bars[i]);
            }
        }
        return Value.Sequence(FromBars(output));
    }

    // ====================================================================
    // 2. fast — (Sequence seq, Double factor) -> Sequence
    // ====================================================================

    private static void RegisterFast(InternalFunctionRegistry registry, ExecutionContext context)
    {
        var sig = new FunctionSignature("fast",
            [SequenceType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "factor"]);
        registry.Register("fast", sig, args => Fast(args, context));
    }

    /// <summary>
    /// <c>(fast seq factor)</c> shortens each note by <c>factor</c>: a factor
    /// of 2.0 halves durations (quarter → eighth). Implemented by reusing
    /// the existing <c>diminish</c> primitive at integer factor steps for
    /// powers of 2; for non-integer factors we re-stamp DurationValue by
    /// rounding <c>currentDur + log2(factor)</c> to the nearest enum slot.
    /// Charitable on <c>factor &lt;= 0</c> / non-finite.
    /// </summary>
    private static Value Fast(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var seq = args[0].As<SequenceData>();
        double factor = args[1].As<double>();
        return FastSlowImpl(seq, factor, "fast", invert: false, ctx);
    }

    // ====================================================================
    // 3. slow — (Sequence seq, Double factor) -> Sequence
    // ====================================================================

    private static void RegisterSlow(InternalFunctionRegistry registry, ExecutionContext context)
    {
        var sig = new FunctionSignature("slow",
            [SequenceType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "factor"]);
        registry.Register("slow", sig, args => Slow(args, context));
    }

    /// <summary>
    /// <c>(slow seq factor)</c> lengthens each note by <c>factor</c>: a factor
    /// of 2.0 doubles durations (quarter → half). Inverse of <c>fast</c>;
    /// shares the implementation with the <c>invert: true</c> flag.
    /// </summary>
    private static Value Slow(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var seq = args[0].As<SequenceData>();
        double factor = args[1].As<double>();
        return FastSlowImpl(seq, factor, "slow", invert: true, ctx);
    }

    private static Value FastSlowImpl(SequenceData seq, double factor, string name, bool invert, ExecutionContext ctx)
    {
        if (!double.IsFinite(factor) || factor <= 0.0)
        {
            // Charitable: zero / negative / NaN / Infinity → unchanged.
            // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [{name}] factor must be > 0 and finite (got {factor})",
                    ctx.CurrentCallSite);
                return Value.Sequence(seq);
            }
            // (Use a no-CurrentCallSite key so the advisory dedups by name —
            //  Fast/Slow have no context.PrngRegistry threading.)
            RenderingDiagnostics.WarnOnce(
                $"{name}:invalid-factor",
                $"[{name}] factor must be > 0 and finite (got {factor}); sequence unchanged");
            return Value.Sequence(seq);
        }
        if (factor == 1.0) return Value.Sequence(seq);

        // log2-shift the duration enum. NoteValueType.Value is power-of-2-tiered:
        // WHOLE=0, HALF=1, QUARTER=2, EIGHTH=3, SIXTEENTH=4, THIRTYSECOND=5,
        // SIXTYFOURTH=6, ONETWENTYEIGHTH=7. A factor of 2 = +1 step (fast) or
        // -1 step (slow). Non-power-of-2 factors round to the nearest enum.
        // Direction flips for slow.
        double log2 = Math.Log2(factor);
        int shift = invert ? -(int)Math.Round(log2) : (int)Math.Round(log2);

        const int minDur = (int)NoteValueType.Value.WHOLE;
        const int maxDur = (int)NoteValueType.Value.ONETWENTYEIGHTH;

        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>(bar.MusicalNotes.Count);
            foreach (var note in bar.MusicalNotes)
            {
                if (!note.DurationValue.HasValue) { newNotes.Add(note); continue; }
                int newDur = Math.Clamp(note.DurationValue.Value + shift, minDur, maxDur);
                // Note.With() does not own a durationValue slot — go through the
                // full constructor to mint a fresh note with the new duration enum.
                newNotes.Add(new MusicalNoteData(
                    note.NoteName, note.Octave, note.Alteration,
                    newDur, note.IsRest, note.CentOffset, note.IsTied,
                    note.Velocity, note.Articulation, note.IsDotted,
                    note.SourceLocation, note.SourceLength, note.DurationFraction,
                    note.OnsetOffset, note.DurationOverlap, note.PortamentoMs,
                    note.IsChordTone));
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return Value.Sequence(result);
    }

    // ====================================================================
    // 4. chunk — (Int n, Function fn, Sequence seq) -> Sequence  (D-36-04 BARS)
    // ====================================================================

    // Per-call-site rotation counter for `chunk` lives on the per-context
    // ExecutionContext.PrngRegistry (NOT a process-static field), so it is
    // reset at every render boundary alongside the PRNG state — see
    // PrngRegistry.NextChunkRotation / ResetAtRenderBoundary. This makes
    // chunk two-run cmp-clean (a re-render restarts the rotation from chunk 0)
    // and stops the counter leaking across independent FlowEngine instances.

    private static void RegisterChunk(InternalFunctionRegistry registry, ExecutionContext context)
    {
        var sig = new FunctionSignature("chunk",
            [IntType.Instance, FunctionType.Instance, SequenceType.Instance],
            ParameterNames: ["n", "fn", "seq"]);
        registry.Register("chunk", sig, args => Chunk(args, context));
    }

    /// <summary>
    /// <c>(chunk n fn seq)</c> divides the sequence into <c>n</c> bar-aligned
    /// chunks and applies <c>fn</c> to one chunk per cycle, rotating which
    /// chunk receives the transform on successive invocations. The rotation
    /// counter is keyed by <see cref="ExecutionContext.CurrentCallSite"/>
    /// (deterministic — same source position advances the counter by one each
    /// time, NOT randomly). Charitable on <c>n &lt;= 0</c> or empty sequence.
    /// </summary>
    private static Value Chunk(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        int n = args[0].As<int>();
        var fn = args[1].As<FunctionOverload>();
        var seq = args[2].As<SequenceData>();

        if (n <= 0)
        {
            // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [chunk] n must be > 0 (got {n}) at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return Value.Sequence(seq);
            }
            RenderingDiagnostics.WarnOnce(
                $"chunk:invalid-n:{ctx.CurrentCallSite}",
                $"[chunk] n must be > 0 (got {n}) at {ctx.CurrentCallSite}; sequence unchanged");
            return Value.Sequence(seq);
        }
        if (IsEmptySeqAdvisory(seq, "chunk", ctx)) return Value.Sequence(seq);

        // Per-call-site rotation counter — advances on each invocation. Lives
        // on the per-context PrngRegistry so it is reset at every render
        // boundary (two-run cmp-clean) and never leaks across FlowEngines.
        int counter = ctx.PrngRegistry.NextChunkRotation(ctx.CurrentCallSite);
        int activeChunk = counter % n;

        int barCount = seq.Bars.Count;
        // Distribute bars EVENLY across n chunks (matching Tidal): the first
        // (barCount % n) chunks get one extra bar. e.g. 5 bars / n=4 → sizes
        // 2,1,1,1 — every rotation index transforms a real (non-empty) chunk,
        // so there is no silent dead cycle. (The old ceil-divide front-loaded
        // bars into the early chunks: 5/4 → 2,2,1 plus a dead 4th chunk.)
        int baseSize = barCount / n;
        int rem = barCount % n;
        // chunkStart = sum of the sizes of all chunks before activeChunk.
        int chunkStart = activeChunk * baseSize + Math.Min(activeChunk, rem);
        int thisSize = baseSize + (activeChunk < rem ? 1 : 0);
        int chunkEnd = Math.Min(chunkStart + thisSize, barCount);

        // Only reachable when barCount < n (some trailing chunks legitimately
        // have zero bars); charitable passthrough applies the transform to
        // nothing for those genuinely-empty rotation indices.
        if (chunkStart >= barCount) return Value.Sequence(seq);

        // Build a sub-Sequence of the active chunk, invoke fn, splice result back.
        var chunkSeq = new SequenceData();
        for (int i = chunkStart; i < chunkEnd; i++) chunkSeq.AddBar(seq.Bars[i]);
        var lambdaResult = InvokeCallback(ctx, fn, new List<Value> { Value.Sequence(chunkSeq) });

        var output = new List<BarData>(seq.Bars.Count);
        for (int i = 0; i < chunkStart; i++) output.Add(seq.Bars[i]);
        if (lambdaResult.Data is SequenceData transformedChunk)
        {
            foreach (var b in transformedChunk.Bars) output.Add(b);
        }
        else
        {
            // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [chunk] lambda at {ctx.CurrentCallSite} did not return Sequence",
                    ctx.CurrentCallSite);
                return Value.Sequence(seq);
            }
            RenderingDiagnostics.WarnOnce(
                $"chunk:non-sequence-fn:{ctx.CurrentCallSite}",
                $"[chunk] lambda at {ctx.CurrentCallSite} did not return Sequence; chunk passed through unchanged");
            for (int i = chunkStart; i < chunkEnd; i++) output.Add(seq.Bars[i]);
        }
        for (int i = chunkEnd; i < barCount; i++) output.Add(seq.Bars[i]);
        return Value.Sequence(FromBars(output));
    }

    /// <summary>
    /// Compatibility shim — now a no-op. The <c>chunk</c> rotation counter
    /// moved off this class's process-static field onto the per-context
    /// <see cref="PrngRegistry"/> (reset at every render boundary, fresh per
    /// FlowEngine), so each test that builds its own engine already starts
    /// from rotation index 0 without an explicit reset. Retained so the three
    /// Phase 36 facts that call it keep compiling; safe to delete once those
    /// call sites are removed.
    /// </summary>
    public static void ResetChunkRotationForTesting()
    {
        // No-op: per-context PrngRegistry owns the counter and is fresh per
        // FlowEngine / cleared at each render boundary.
    }

    // ====================================================================
    // 5. phase — (Double offset, Sequence seq) -> Sequence  (D-36-04 BARS)
    // ====================================================================

    private static void RegisterPhase(InternalFunctionRegistry registry, ExecutionContext context)
    {
        var sig = new FunctionSignature("phase",
            [DoubleType.Instance, SequenceType.Instance],
            ParameterNames: ["offset", "seq"]);
        registry.Register("phase", sig, args => Phase(args, context));
    }

    /// <summary>
    /// <c>(phase offset seq)</c> rotates the bar order by
    /// <c>round(offset × seq.Bars.Count)</c> positions. <c>offset 0.5</c>
    /// on a 4-bar seq rotates by 2. Charitable on non-finite offsets and
    /// empty sequences.
    /// </summary>
    private static Value Phase(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        double offset = args[0].As<double>();
        var seq = args[1].As<SequenceData>();

        if (!double.IsFinite(offset))
        {
            // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [phase] offset must be finite (got {offset})",
                    ctx.CurrentCallSite);
                return Value.Sequence(seq);
            }
            RenderingDiagnostics.WarnOnce(
                "phase:non-finite",
                $"[phase] offset must be finite (got {offset}); sequence unchanged");
            return Value.Sequence(seq);
        }
        if (seq.Bars.Count == 0)
        {
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    "[strict] [phase] empty sequence",
                    ctx.CurrentCallSite);
                return Value.Sequence(seq);
            }
            RenderingDiagnostics.WarnOnce(
                "phase:empty",
                "[phase] empty sequence; returned unchanged");
            return Value.Sequence(seq);
        }

        int barCount = seq.Bars.Count;
        int shift = ((int)Math.Round(offset * barCount) % barCount + barCount) % barCount;
        if (shift == 0) return Value.Sequence(seq);

        var output = new List<BarData>(barCount);
        for (int i = 0; i < barCount; i++)
            output.Add(seq.Bars[(i + shift) % barCount]);
        return Value.Sequence(FromBars(output));
    }

    // ====================================================================
    // 6. rev — (Sequence seq) -> Sequence
    // ====================================================================

    private static void RegisterRev(InternalFunctionRegistry registry, ExecutionContext context)
    {
        var sig = new FunctionSignature("rev",
            [SequenceType.Instance],
            ParameterNames: ["seq"]);
        registry.Register("rev", sig, args => Rev(args, context));
    }

    /// <summary>
    /// <c>(rev seq)</c> reverses BAR ORDER only — within-bar note order is
    /// preserved. Compare to the existing <c>retrograde</c> which reverses
    /// both. Charitable on empty: returns empty unchanged.
    /// </summary>
    private static Value Rev(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var seq = args[0].As<SequenceData>();
        if (seq.Bars.Count == 0)
        {
            // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07. The
            // pre-strict non-strict path is silent (Pitfall 9), so under strict
            // we surface an error to the composer at the call boundary.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    "[strict] [rev] empty sequence",
                    ctx.CurrentCallSite);
            }
            return Value.Sequence(seq);
        }

        var reversed = new List<BarData>(seq.Bars);
        reversed.Reverse();
        return Value.Sequence(FromBars(reversed));
    }

    // ====================================================================
    // 7. iter — (Int n, Sequence seq) -> Sequence
    // ====================================================================

    private static void RegisterIter(InternalFunctionRegistry registry, ExecutionContext context)
    {
        var sig = new FunctionSignature("iter",
            [IntType.Instance, SequenceType.Instance],
            ParameterNames: ["n", "seq"]);
        registry.Register("iter", sig, args => Iter(args, context));
    }

    /// <summary>
    /// <c>(iter n seq)</c> rotates the note list by 1/n of the total note count.
    /// Differs from <c>phase</c>: iter rotates notes WITHIN bars (note-level),
    /// phase rotates bars (bar-level). Per Tidal: <c>iter 4</c> on a 4-note
    /// pattern advances by one note each cycle; the v1.5 implementation
    /// applies a single rotation by <c>totalNotes / n</c> positions across
    /// the entire flattened note list, then re-distributes notes back into
    /// bars preserving the original bar boundaries. Charitable on
    /// <c>n &lt;= 0</c>.
    /// </summary>
    private static Value Iter(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        int n = args[0].As<int>();
        var seq = args[1].As<SequenceData>();

        if (n <= 0)
        {
            // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [iter] n must be > 0 (got {n})",
                    ctx.CurrentCallSite);
                return Value.Sequence(seq);
            }
            RenderingDiagnostics.WarnOnce(
                "iter:invalid-n",
                $"[iter] n must be > 0 (got {n}); sequence unchanged");
            return Value.Sequence(seq);
        }
        if (seq.Bars.Count == 0)
        {
            // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    "[strict] [iter] empty sequence",
                    ctx.CurrentCallSite);
            }
            return Value.Sequence(seq);
        }

        // Flatten notes, rotate, re-distribute keeping bar.Count counts identical.
        var flat = new List<MusicalNoteData>();
        var barSizes = new List<int>(seq.Bars.Count);
        foreach (var bar in seq.Bars)
        {
            barSizes.Add(bar.MusicalNotes.Count);
            flat.AddRange(bar.MusicalNotes);
        }

        if (flat.Count == 0) return Value.Sequence(seq);
        int shift = (flat.Count / n) % flat.Count;
        if (shift == 0) return Value.Sequence(seq);

        // Rotate left by `shift`: rotated[i] = flat[(i+shift) % count]
        var rotated = new List<MusicalNoteData>(flat.Count);
        for (int i = 0; i < flat.Count; i++)
            rotated.Add(flat[(i + shift) % flat.Count]);

        // Re-distribute into bars preserving original bar lengths.
        var result = new SequenceData();
        int idx = 0;
        for (int b = 0; b < seq.Bars.Count; b++)
        {
            int size = barSizes[b];
            var newNotes = new List<MusicalNoteData>(size);
            for (int j = 0; j < size; j++) newNotes.Add(rotated[idx++]);
            result.AddBar(new BarData(newNotes, seq.Bars[b].TimeSignature!));
        }
        return Value.Sequence(result);
    }

    // ====================================================================
    // 8. palindrome — (Sequence seq) -> Sequence
    // ====================================================================

    private static void RegisterPalindrome(InternalFunctionRegistry registry, ExecutionContext context)
    {
        var sig = new FunctionSignature("palindrome",
            [SequenceType.Instance],
            ParameterNames: ["seq"]);
        registry.Register("palindrome", sig, args => Palindrome(args, context));
    }

    /// <summary>
    /// <c>(palindrome seq)</c> appends the bar-reversed sequence to the original:
    /// <c>[A B C] → [A B C C B A]</c>. Bar-level mirroring per RESEARCH
    /// (Tidal's palindrome is per-cycle / per-bar; we mirror at bar
    /// granularity to compose with <c>rev</c>'s contract).
    /// </summary>
    private static Value Palindrome(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var seq = args[0].As<SequenceData>();
        if (seq.Bars.Count == 0)
        {
            // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    "[strict] [palindrome] empty sequence",
                    ctx.CurrentCallSite);
            }
            return Value.Sequence(seq);
        }

        var result = new SequenceData();
        foreach (var b in seq.Bars) result.AddBar(b);
        for (int i = seq.Bars.Count - 1; i >= 0; i--) result.AddBar(seq.Bars[i]);
        return Value.Sequence(result);
    }

    // ====================================================================
    // 9. jux — (Function fn, Sequence seq) -> Sequence
    // ====================================================================

    private static void RegisterJux(InternalFunctionRegistry registry, ExecutionContext context)
    {
        var sig = new FunctionSignature("jux",
            [FunctionType.Instance, SequenceType.Instance],
            ParameterNames: ["fn", "seq"]);
        registry.Register("jux", sig, args => Jux(args, context));
    }

    /// <summary>
    /// <c>(jux fn seq)</c> layers the original sequence with <c>fn(seq)</c>
    /// as a Phase 28 voice block: each bar's <c>ParallelVoices</c> is set to
    /// <c>[original-as-voice-bar, transformed-as-voice-bar]</c> so the
    /// SongRenderer mixes them additively (left/right stereo separation is
    /// a v1.6 follow-up; v1.5 mixes both to mono). Charitable when the
    /// lambda returns non-Sequence or the bar counts mismatch.
    /// </summary>
    private static Value Jux(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var fn = args[0].As<FunctionOverload>();
        var seq = args[1].As<SequenceData>();

        if (IsEmptySeqAdvisory(seq, "jux", ctx)) return Value.Sequence(seq);

        var lambdaResult = InvokeCallback(ctx, fn, new List<Value> { Value.Sequence(seq) });
        if (lambdaResult.Data is not SequenceData other)
        {
            // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [jux] lambda at {ctx.CurrentCallSite} did not return Sequence",
                    ctx.CurrentCallSite);
                return Value.Sequence(seq);
            }
            RenderingDiagnostics.WarnOnce(
                $"jux:non-sequence-fn:{ctx.CurrentCallSite}",
                $"[jux] lambda at {ctx.CurrentCallSite} did not return Sequence; original passed through");
            return Value.Sequence(seq);
        }
        if (other.Bars.Count != seq.Bars.Count)
        {
            // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [jux] lambda result has {other.Bars.Count} bars vs source {seq.Bars.Count}",
                    ctx.CurrentCallSite);
                return Value.Sequence(seq);
            }
            RenderingDiagnostics.WarnOnce(
                $"jux:bar-mismatch:{ctx.CurrentCallSite}",
                $"[jux] lambda result has {other.Bars.Count} bars vs source {seq.Bars.Count}; original passed through");
            return Value.Sequence(seq);
        }

        var result = new SequenceData();
        for (int i = 0; i < seq.Bars.Count; i++)
        {
            var parent = CloneBar(seq.Bars[i]);
            // Phase 28 voice-block additive mixing — parent's voices = [src, fn(src)].
            parent.ParallelVoices = new List<BarData>
            {
                CloneBar(seq.Bars[i]),
                CloneBar(other.Bars[i])
            };
            result.AddBar(parent);
        }
        return Value.Sequence(result);
    }

    // ====================================================================
    // 10. superimpose — (Function fn, Sequence seq) -> Sequence
    // ====================================================================

    private static void RegisterSuperimpose(InternalFunctionRegistry registry, ExecutionContext context)
    {
        var sig = new FunctionSignature("superimpose",
            [FunctionType.Instance, SequenceType.Instance],
            ParameterNames: ["fn", "seq"]);
        registry.Register("superimpose", sig, args => Superimpose(args, context));
    }

    /// <summary>
    /// <c>(superimpose fn seq)</c> is the mono analog of <c>jux</c> — layers
    /// the original with <c>fn(seq)</c> as a voice block, both mixed equally.
    /// Same wire-shape as <c>jux</c>; the semantic distinction is that
    /// <c>jux</c> reserves the right to do L/R stereo placement in v1.6 while
    /// <c>superimpose</c> stays mono-mixed. Today (v1.5) they are functionally
    /// identical at the voice-block level.
    /// </summary>
    private static Value Superimpose(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        // Mirror jux's wire shape; only the advisory key differs.
        var fn = args[0].As<FunctionOverload>();
        var seq = args[1].As<SequenceData>();

        if (IsEmptySeqAdvisory(seq, "superimpose", ctx)) return Value.Sequence(seq);

        var lambdaResult = InvokeCallback(ctx, fn, new List<Value> { Value.Sequence(seq) });
        if (lambdaResult.Data is not SequenceData other)
        {
            // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [superimpose] lambda at {ctx.CurrentCallSite} did not return Sequence",
                    ctx.CurrentCallSite);
                return Value.Sequence(seq);
            }
            RenderingDiagnostics.WarnOnce(
                $"superimpose:non-sequence-fn:{ctx.CurrentCallSite}",
                $"[superimpose] lambda at {ctx.CurrentCallSite} did not return Sequence; original passed through");
            return Value.Sequence(seq);
        }
        if (other.Bars.Count != seq.Bars.Count)
        {
            // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [superimpose] lambda result has {other.Bars.Count} bars vs source {seq.Bars.Count}",
                    ctx.CurrentCallSite);
                return Value.Sequence(seq);
            }
            RenderingDiagnostics.WarnOnce(
                $"superimpose:bar-mismatch:{ctx.CurrentCallSite}",
                $"[superimpose] lambda result has {other.Bars.Count} bars vs source {seq.Bars.Count}; original passed through");
            return Value.Sequence(seq);
        }

        var result = new SequenceData();
        for (int i = 0; i < seq.Bars.Count; i++)
        {
            var parent = CloneBar(seq.Bars[i]);
            parent.ParallelVoices = new List<BarData>
            {
                CloneBar(seq.Bars[i]),
                CloneBar(other.Bars[i])
            };
            result.AddBar(parent);
        }
        return Value.Sequence(result);
    }

    // ====================================================================
    // 11. sometimes — (Double prob, Function fn, Sequence seq) -> Sequence
    //               + (Function fn, Sequence seq) -> Sequence  (default prob=0.5)
    //
    // STOCHASTIC — PRNG routed through PrngRegistry per D-v1.5-06 / D-36-09.
    // ====================================================================

    private static void RegisterSometimes(InternalFunctionRegistry registry, ExecutionContext context)
    {
        // Explicit-prob overload (D-36-05).
        var sigProb = new FunctionSignature("sometimes",
            [DoubleType.Instance, FunctionType.Instance, SequenceType.Instance],
            ParameterNames: ["prob", "fn", "seq"]);
        registry.Register("sometimes", sigProb, args => Sometimes(args, context, hasProb: true));

        // Default-prob overload (D-36-05 — convenience shortcut at prob=0.5).
        var sigDefault = new FunctionSignature("sometimes",
            [FunctionType.Instance, SequenceType.Instance],
            ParameterNames: ["fn", "seq"]);
        registry.Register("sometimes", sigDefault, args => Sometimes(args, context, hasProb: false));
    }

    /// <summary>
    /// <c>(sometimes prob fn seq)</c> applies <c>fn</c> to each bar with
    /// probability <c>prob</c>. Default-prob overload uses 0.5. PRNG is keyed
    /// by <c>(CurrentCallSite, "sometimes")</c> — two calls at the same source
    /// position within a single render pass share their PRNG stream; reseeded
    /// at every render boundary. Charitable: probs outside [0,1] are clamped
    /// with a WarnOnce advisory.
    /// </summary>
    private static Value Sometimes(
        IReadOnlyList<Value> args,
        ExecutionContext ctx,
        bool hasProb)
    {
        double prob;
        FunctionOverload fn;
        SequenceData seq;
        if (hasProb)
        {
            prob = args[0].As<double>();
            fn = args[1].As<FunctionOverload>();
            seq = args[2].As<SequenceData>();
        }
        else
        {
            prob = 0.5;
            fn = args[0].As<FunctionOverload>();
            seq = args[1].As<SequenceData>();
        }

        if (!double.IsFinite(prob) || prob < 0.0 || prob > 1.0)
        {
            double clamped = double.IsFinite(prob) ? Math.Clamp(prob, 0.0, 1.0) : 0.5;
            // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [sometimes] probability {prob} outside [0.0, 1.0]",
                    ctx.CurrentCallSite);
                return Value.Sequence(seq);
            }
            RenderingDiagnostics.WarnOnce(
                $"sometimes:clamp:{ctx.CurrentCallSite}",
                $"[sometimes] prob {prob} clamped to {clamped} at {ctx.CurrentCallSite}");
            prob = clamped;
        }
        if (IsEmptySeqAdvisory(seq, "sometimes", ctx)) return Value.Sequence(seq);

        // PRNG via the per-context registry — keyed by (CurrentCallSite, "sometimes").
        // Source-grep gate (PatternDeterminismTests.NoNewRandomInPatternFunctions)
        // bans `new Random(` in this file.
        var output = new List<BarData>(seq.Bars.Count);
        for (int i = 0; i < seq.Bars.Count; i++)
        {
            double draw = ctx.PrngRegistry.NextDouble(ctx.CurrentCallSite, "sometimes");
            if (draw < prob)
            {
                // Apply fn to this bar.
                var single = SingleBarSeq(seq.Bars[i]);
                var lambdaResult = InvokeCallback(ctx, fn,
                    new List<Value> { Value.Sequence(single) });
                if (lambdaResult.Data is SequenceData transformed)
                    foreach (var b in transformed.Bars) output.Add(b);
                else
                {
                    // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
                    if (ctx.CallerStrictMode)
                    {
                        ctx.ErrorReporter.ReportError(
                            $"[strict] [sometimes] lambda at {ctx.CurrentCallSite} did not return Sequence",
                            ctx.CurrentCallSite);
                        return Value.Sequence(FromBars(output));
                    }
                    RenderingDiagnostics.WarnOnce(
                        $"sometimes:non-sequence-fn:{ctx.CurrentCallSite}",
                        $"[sometimes] lambda at {ctx.CurrentCallSite} did not return Sequence; bar passed through");
                    output.Add(seq.Bars[i]);
                }
            }
            else
            {
                output.Add(seq.Bars[i]);
            }
        }
        return Value.Sequence(FromBars(output));
    }

    // ====================================================================
    // 12. degrade — (Sequence seq) -> Sequence  (fixed 50% drop; Tidal compat)
    // ====================================================================

    private static void RegisterDegrade(InternalFunctionRegistry registry, ExecutionContext context)
    {
        var sig = new FunctionSignature("degrade",
            [SequenceType.Instance],
            ParameterNames: ["seq"]);
        registry.Register("degrade", sig, args => Degrade(args, context));
    }

    /// <summary>
    /// <c>(degrade seq)</c> drops each bar with fixed 50% probability (Tidal
    /// compatibility — RESEARCH § Tidal). For composer-controlled drop rate
    /// see <see cref="SparseSeq"/>. PRNG keyed by
    /// <c>(CurrentCallSite, "degrade")</c>.
    /// </summary>
    private static Value Degrade(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var seq = args[0].As<SequenceData>();
        if (IsEmptySeqAdvisory(seq, "degrade", ctx)) return Value.Sequence(seq);
        return DropBars(seq, prob: 0.5, name: "degrade", ctx: ctx);
    }

    // ====================================================================
    // 13. sparseSeq — (Double prob, Sequence seq) -> Sequence  (Flow-native; D-36-02)
    // ====================================================================

    private static void RegisterSparseSeq(InternalFunctionRegistry registry, ExecutionContext context)
    {
        var sig = new FunctionSignature("sparseSeq",
            [DoubleType.Instance, SequenceType.Instance],
            ParameterNames: ["prob", "seq"]);
        registry.Register("sparseSeq", sig, args => SparseSeq(args, context));
    }

    /// <summary>
    /// <c>(sparseSeq prob seq)</c> drops each bar with probability <c>prob</c>.
    /// Composer-supplied analog of <see cref="Degrade"/>. PRNG keyed by
    /// <c>(CurrentCallSite, "sparseSeq")</c>. Charitable: probs outside [0,1]
    /// are clamped with a WarnOnce advisory.
    /// </summary>
    private static Value SparseSeq(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        double prob = args[0].As<double>();
        var seq = args[1].As<SequenceData>();

        if (!double.IsFinite(prob) || prob < 0.0 || prob > 1.0)
        {
            double clamped = double.IsFinite(prob) ? Math.Clamp(prob, 0.0, 1.0) : 0.5;
            // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [sparseSeq] probability {prob} outside [0.0, 1.0]",
                    ctx.CurrentCallSite);
                return Value.Sequence(seq);
            }
            RenderingDiagnostics.WarnOnce(
                $"sparseSeq:clamp:{ctx.CurrentCallSite}",
                $"[sparseSeq] prob {prob} clamped to {clamped} at {ctx.CurrentCallSite}");
            prob = clamped;
        }
        if (IsEmptySeqAdvisory(seq, "sparseSeq", ctx)) return Value.Sequence(seq);
        return DropBars(seq, prob, name: "sparseSeq", ctx: ctx);
    }

    /// <summary>
    /// Shared bar-drop implementation for <c>degrade</c> and <c>sparseSeq</c>.
    /// For each bar, draws a PRNG sample from the per-context registry keyed
    /// by <c>(CurrentCallSite, name)</c> and drops the bar if the draw is
    /// LESS than <c>prob</c>. (Note: <c>degrade</c>'s convention in some Tidal
    /// docs is "drop when draw >= 0.5", but the equivalent contract here is
    /// "drop when draw &lt; prob" — for prob=0.5 the expected drop rate is
    /// the same 50%.)
    /// </summary>
    private static Value DropBars(SequenceData seq, double prob, string name, ExecutionContext ctx)
    {
        var output = new List<BarData>(seq.Bars.Count);
        foreach (var bar in seq.Bars)
        {
            double draw = ctx.PrngRegistry.NextDouble(ctx.CurrentCallSite, name);
            if (draw >= prob)
            {
                output.Add(bar);
            }
            // else: drop this bar.
        }
        // Charitable: if everything dropped on a non-empty input, leave at
        // least the structure intact — return the empty SequenceData (composer
        // can hear silence and adjust). No advisory because this is the
        // expected behavior for high prob values.
        return Value.Sequence(FromBars(output));
    }
}
