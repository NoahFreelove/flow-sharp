using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Diagnostics;
using FlowLang.Interpreter;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using RuntimeContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Music;

/// <summary>
/// Executes the music statements bound through <see cref="DomainBindings"/>: musical
/// context blocks, tuning blocks, live blocks and section declarations. One instance
/// wraps one interpreter; bodies run through the interpreter's block API so return
/// and error handling match the language's.
/// </summary>
internal sealed class MusicStatements
{
    private readonly FlowLang.Interpreter.Interpreter _interpreter;
    private readonly RuntimeContext _context;
    private readonly ErrorReporter _errorReporter;
    private readonly ExpressionEvaluator _evaluator;

    public MusicStatements(FlowLang.Interpreter.Interpreter interpreter)
    {
        _interpreter = interpreter;
        _context = interpreter.Context;
        _errorReporter = interpreter.Errors;
        _evaluator = interpreter.Evaluator;
    }

    private void ExecuteStatement(Statement stmt) => _interpreter.ExecuteStatement(stmt);
    private Value? _lastExpressionValue => _interpreter.LastExpressionValue;
    private bool InsideProcCall => _interpreter.InsideProcCall;
    private void ClearLeakedReturn(string blockKind, Core.SourceLocation location) =>
        _interpreter.ClearLeakedReturn(blockKind, location);

    // Bare-expression sequences of the section body being declared, so nested
    // context/tuning/live blocks inside a section still produce audible output.
    private List<SequenceData>? _activeSectionBareExpressions
    {
        get => _context.Music.ActiveSectionCapture;
        set => _context.Music.ActiveSectionCapture = value;
    }

    public void ExecuteMusicalContext(MusicalContextStatement ctx)
    {
        _context.PushFrame();
        try
        {
            var musicalCtx = new MusicalContext();

            switch (ctx.ContextType)
            {
                case MusicalContextType.Timesig:
                    var num = _evaluator.Evaluate(ctx.Value);
                    var den = _evaluator.Evaluate(ctx.Value2!);
                    int numVal = num.As<int>();
                    int denVal = den.As<int>();
                    try
                    {
                        musicalCtx.TimeSignature = new TimeSignatureData(numVal, denVal);
                    }
                    catch (ArgumentException ex)
                    {
                        _errorReporter.ReportError(ex.Message, ctx.Location);
                        break;
                    }
                    break;

                case MusicalContextType.Tempo:
                    var tempoVal = _evaluator.Evaluate(ctx.Value);
                    double tempo = tempoVal.Type is IntType
                        ? (double)tempoVal.As<int>()
                        : tempoVal.As<double>();
                    if (!MusicalContext.IsValidTempo(tempo))
                    {
                        _errorReporter.ReportError(
                            $"Tempo must be positive, got {tempo}", ctx.Location);
                        break;
                    }
                    musicalCtx.Tempo = tempo;
                    break;

                case MusicalContextType.Swing:
                    var swingVal = _evaluator.Evaluate(ctx.Value);
                    double swing = swingVal.Type is IntType
                        ? (double)swingVal.As<int>()
                        : swingVal.As<double>();
                    if (!MusicalContext.IsValidSwing(swing))
                    {
                        _errorReporter.ReportError(
                            $"Swing must be between 0.0 and 1.0, got {swing}", ctx.Location);
                        break;
                    }
                    musicalCtx.Swing = swing;
                    break;

                case MusicalContextType.Dynamics:
                    var velVal = _evaluator.Evaluate(ctx.Value);
                    double vel = velVal.Type is IntType
                        ? (double)velVal.As<int>()
                        : velVal.As<double>();
                    vel = Math.Clamp(vel, 0.0, 1.0);
                    musicalCtx.Velocity = vel;
                    break;

                case MusicalContextType.Rit:
                {
                    var targetVal = _evaluator.Evaluate(ctx.Value);
                    double targetTempo = targetVal.Type is IntType
                        ? (double)targetVal.As<int>()
                        : targetVal.As<double>();
                    // Approximate rit by averaging current tempo and target
                    double currentTempo = _context.GetMusicalContext().Tempo ?? 120.0;
                    musicalCtx.Tempo = (currentTempo + targetTempo) / 2.0;
                    break;
                }
                case MusicalContextType.Accel:
                {
                    var targetVal = _evaluator.Evaluate(ctx.Value);
                    double targetTempo = targetVal.Type is IntType
                        ? (double)targetVal.As<int>()
                        : targetVal.As<double>();
                    double currentTempo = _context.GetMusicalContext().Tempo ?? 120.0;
                    musicalCtx.Tempo = (currentTempo + targetTempo) / 2.0;
                    break;
                }

                case MusicalContextType.Pan:
                {
                    var panVal = _evaluator.Evaluate(ctx.Value);
                    double pan = panVal.Type is IntType
                        ? (double)panVal.As<int>()
                        : panVal.As<double>();
                    if (pan < -1.0 || pan > 1.0)
                    {
                        _errorReporter.ReportError(
                            $"Pan value must be between -1.0 and 1.0, got {pan}", ctx.Location);
                        break;
                    }
                    musicalCtx.Pan = pan;
                    break;
                }

                case MusicalContextType.Gain:
                {
                    var gainVal = _evaluator.Evaluate(ctx.Value);
                    double gain = gainVal.Type is IntType
                        ? (double)gainVal.As<int>()
                        : gainVal.As<double>();
                    if (gain < 0.0 || gain > 2.0)
                    {
                        _errorReporter.ReportError(
                            $"Gain must be between 0.0 and 2.0, got {gain}", ctx.Location);
                        break;
                    }
                    musicalCtx.Gain = gain;
                    break;
                }

                case MusicalContextType.ReverbTime:
                {
                    var rtVal = _evaluator.Evaluate(ctx.Value);
                    double rt60 = rtVal.Type is IntType ? (double)rtVal.As<int>() : rtVal.As<double>();
                    // D-03: silent clamp to 30s (negative already rejected at parse time)
                    rt60 = Math.Min(rt60, 30.0);
                    // D-02: 0.0 preserved as sentinel for "dry" — no error, no clamp-up
                    musicalCtx.ReverbTime = rt60;
                    break;
                }

                case MusicalContextType.VoicePool:
                {
                    // Phase 28 SPEC-7: voicePool N { ... } — N must be in [1, 256].
                    // Out-of-range emits a clear composer-facing error pointing at the
                    // statement location.
                    var poolVal = _evaluator.Evaluate(ctx.Value);
                    int poolSize = poolVal.As<int>();
                    if (poolSize < 1 || poolSize > 256)
                    {
                        _errorReporter.ReportError(
                            $"Voice pool size must be between 1 and 256, got {poolSize}", ctx.Location);
                        break;
                    }
                    musicalCtx.VoicePoolSize = poolSize;
                    break;
                }

                case MusicalContextType.Octave:
                {
                    // octave N { ... } — sets the default octave for bare note letters
                    // (letters written without an explicit octave digit) inside note
                    // streams. CHARITABLE clamp to [1, 9] with a one-shot advisory — never
                    // a throw. [1, 9] is the widest block-octave range where every bare
                    // A–G letter (with typical accidentals) stays inside NoteType's
                    // E0(MIDI 16)–E10(MIDI 136) window (C1=24, B9=131), so the
                    // default-octave path can NEVER make NoteType.Parse throw. Extreme
                    // registers remain reachable via explicit per-note octave digits.
                    var octVal = _evaluator.Evaluate(ctx.Value);
                    int oct = octVal.As<int>();
                    if (oct < 1 || oct > 9)
                    {
                        int clamped = Math.Clamp(oct, 1, 9);
                        FlowLang.Diagnostics.RenderingDiagnostics.WarnOnce(
                            $"octave-clamp:{oct}",
                            $"[octave] octave {oct} out of range [1, 9] — clamped to {clamped}");
                        oct = clamped;
                    }
                    musicalCtx.DefaultOctave = oct;
                    break;
                }

                case MusicalContextType.SustainPedal:
                    // Notes evaluated within this block render with their buffer
                    // extended by MusicalContext.SustainTailSeconds, mimicking a
                    // piano's sustain pedal. The flag itself is part of the
                    // context Clone so nesting works.
                    musicalCtx.SustainPedal = true;
                    break;

                case MusicalContextType.Key:
                    if (ctx.Value is LiteralExpression keyExpr)
                    {
                        string keyName = (string)keyExpr.Value;
                        if (!MusicalContext.IsValidKey(keyName))
                        {
                            _errorReporter.ReportError(
                                $"Unrecognized key '{keyName}'. Valid keys include: Cmajor, Aminor, Fsharpmajor, etc.",
                                ctx.Location);
                            break;
                        }
                        musicalCtx.Key = keyName;
                    }
                    else
                    {
                        _errorReporter.ReportError(
                            "Expected a key name literal (e.g., Cmajor, Aminor)", ctx.Location);
                        break;
                    }
                    break;
            }

            _context.SetCurrentFrameMusicalContext(musicalCtx);

            foreach (var stmt in ctx.Body)
            {
                ExecuteStatement(stmt);

                // When nested inside a section, capture bare expression sequences
                // so `section { gain N { | notes | } }` produces audible output.
                if (_activeSectionBareExpressions != null
                    && stmt is ExpressionStatement
                    && _lastExpressionValue?.Data is SequenceData innerSeq)
                {
                    _activeSectionBareExpressions.Add(innerSeq);
                }

                if (_interpreter.HasPendingReturn) break;
            }

            // Audit §2.3 — a musical-context block is a transparent statement
            // wrapper: a `return` inside it propagates to an enclosing proc.
            // But at TOP LEVEL (no proc on the stack) a leaked return would make
            // the top-of-ExecuteStatement guard silently skip the rest of the
            // program. Report + clear so the program keeps running.
            if (!InsideProcCall)
                ClearLeakedReturn("musical-context", ctx.Location);
        }
        finally
        {
            _context.PopFrame();
        }
    }

    // AUDIT-VERIFIED 2026-04-19: C1 — Fixed (returns→breaks); body now runs under partial/default context (tests/spike/c1-musical-context-body.flow GREEN)

    /// <summary>
    /// Phase 32 Plan 32-06 D-13/D-14 — executes a <c>tuning &lt;expr&gt; { ... }</c>
    /// musical-context block. Evaluates the tuning expression (any of the three D-15
    /// forms: identifier / inline call / desugared string-literal), verifies the
    /// resulting <see cref="Value"/> carries <see cref="TuningType.Instance"/>,
    /// wraps the underlying <see cref="StandardLibrary.Audio.Tuning.ResolvedTuning"/>
    /// in a <see cref="StandardLibrary.Audio.Tuning.RenderTuning"/> with
    /// <c>Custom != null</c>, and push/pops via the Plan 32-05 stack API.
    ///
    /// D-14 graceful unwinding: the body executes inside a try/finally so that
    /// even if the body throws, <see cref="RuntimeContext.PopTuning"/> still fires
    /// — preserving the Pitfall 2 contract (blocks force-close at REPL eval
    /// boundary, never leak across evals). Mirrors the
    /// <see cref="ExecuteMusicalContext"/> try/finally shape at lines 137-322.
    ///
    /// Per Plan 32-03 Pitfall 3 mutual-exclusion: when
    /// <see cref="StandardLibrary.Audio.Tuning.RenderTuning.Custom"/> is set, the
    /// System / Mode / Tonic fields are effectively ignored by PitchConversion's
    /// custom-wins branch (and by SongRenderer.ResolveRenderTuning's three-branch
    /// resolution at Plan 32-05). We use fixed placeholder defaults
    /// (EqualTemperament, Major, 'C', 0) here — keeping the wedge fields
    /// consistent with Phase 23 D-05 defaults but irrelevant to the custom path.
    /// </summary>
    public void ExecuteTuningContext(TuningContextStatement tctx)
    {
        // Step 1: evaluate the tuning expression. Per D-15 this could be a
        // VariableExpression (identifier form), a FunctionCallExpression (inline
        // call OR the synthetic loadScala desugar from string-literal sugar).
        var tuningValue = _evaluator.Evaluate(tctx.TuningExpr);

        // Step 2: type-check. The value MUST carry TuningType.Instance — any
        // other type is a composer error.
        if (tuningValue.Type is not TuningType)
        {
            _errorReporter.ReportError(
                $"tuning block expects a Tuning value, got {tuningValue.Type.Name}",
                tctx.Location);
            return;
        }

        // Step 3: extract the ResolvedTuning. MusicValue.Tuning(ResolvedTuning) is the
        // Plan 32-04 factory; the unwrap reads the Data slot directly.
        var resolved = (StandardLibrary.Audio.Tuning.ResolvedTuning)tuningValue.Data!;

        // Step 4: construct the RenderTuning to push. Custom is the active payload;
        // the (System, Mode, TonicLetter, TonicAlteration) wedge is defensive
        // defaults — Plan 32-03 Task 2 asserted Custom-takes-priority as
        // defense-in-depth, so these are irrelevant on the custom path.
        var renderTuning = new StandardLibrary.Audio.Tuning.RenderTuning(
            StandardLibrary.Audio.Tuning.TuningSystem.EqualTemperament,
            StandardLibrary.Audio.Tuning.Mode.Major,
            'C',
            0,
            Custom: resolved);

        // Step 5: push onto the topmost frame's TuningStack (Plan 32-05 API).
        _context.PushTuning(renderTuning);

        // Step 6: execute the body inside try/finally so the stack frame still
        // pops if anything throws (D-14 graceful unwinding).
        try
        {
            foreach (var stmt in tctx.Body)
            {
                ExecuteStatement(stmt);

                // Mirror ExecuteMusicalContext's bare-expression capture: when nested
                // inside a section, surface sequence-valued expressions to the
                // active section bare-expression sink so `section { tuning t {
                // | C4 D4 | } }` produces audible output.
                if (_activeSectionBareExpressions != null
                    && stmt is ExpressionStatement
                    && _lastExpressionValue?.Data is SequenceData innerSeq)
                {
                    _activeSectionBareExpressions.Add(innerSeq);
                }

                if (_interpreter.HasPendingReturn) break;
            }

            // Audit §2.3 — same transparent-wrapper rule as ExecuteMusicalContext:
            // a leaked top-level return must not silently truncate the program.
            if (!InsideProcCall)
                ClearLeakedReturn("tuning", tctx.Location);
        }
        finally
        {
            _context.PopTuning();
        }
    }

    /// <summary>
    /// Phase 38 Plan 38-02 LIVE-01 — executes a <c>live &lt;quantize&gt; { ... }</c>
    /// block during initial render. Three steps:
    /// <list type="number">
    ///   <item>Emit the D-v1.5-07 stderr advisory once per (line, process) via
    ///   <see cref="RenderingDiagnostics.WarnOnce"/> with sentinel
    ///   <c>live-determinism-optout:&lt;line&gt;</c> — explicit opt-out from the
    ///   two-run cmp-clean determinism contract per D-v1.5-07.</item>
    ///   <item>Resolve <see cref="LiveBlockStatement.QuantizeValue"/> to a beat
    ///   count: Int payload → bars × beatsPerBar from active
    ///   <see cref="MusicalContext"/>; String payload → NoteValue (q/h/w/e/s)
    ///   → fraction × 4 beats per whole.</item>
    ///   <item>Register a <see cref="LiveBlockRegistration"/> into
    ///   <see cref="ExecutionContext.LiveBlockRegistry"/> so Plan 38-03's
    ///   swap consumer can hang per-block pending-buffer slots off the
    ///   BlockId, then execute the body once inside a scope frame so initial
    ///   render captures the per-block buffer.</item>
    /// </list>
    ///
    /// <para>
    /// Scope discipline mirrors <see cref="ExecuteMusicalContext"/>: PushFrame
    /// before the body, PopFrame in a finally so a body throw still rebalances
    /// the call stack. The body inherits the caller's musical context (tempo /
    /// timesig / key) — necessary so <c>tempo 120 { live 1bar { ... } }</c>
    /// resolves bar = 4 beats from the outer frame.
    /// </para>
    /// </summary>
    public void ExecuteLiveBlock(LiveBlockStatement live)
    {
        // Step 1: D-v1.5-07 stderr advisory — once per (line, process).
        RenderingDiagnostics.WarnOnce(
            $"live-determinism-optout:{live.Location.Line}",
            $"[live] entering live block at line {live.Location.Line} — opts OUT of two-run cmp-clean determinism");

        // Step 2: resolve quantize to beats.
        var quantizeValue = _evaluator.Evaluate(live.QuantizeValue);
        double quantizeBeats = ResolveQuantizeBeats(quantizeValue);

        // Step 3: register into LiveBlockRegistry so Plan 38-03's swap
        // consumer can address this block by BlockId.
        var registration = new LiveBlockRegistration(
            live.BlockId,
            live.Location,
            live.Body,
            quantizeBeats);
        _context.LiveBlockRegistry.Register(registration);

        // Step 4: execute body once in a scope frame. PushFrame/PopFrame
        // mirrors ExecuteMusicalContext at lines 149-150 so block-local
        // declarations don't leak.
        _context.PushFrame();
        try
        {
            foreach (var stmt in live.Body)
            {
                ExecuteStatement(stmt);

                // Mirror ExecuteMusicalContext's bare-expression capture so
                // section-nested live blocks still produce audible output.
                if (_activeSectionBareExpressions != null
                    && stmt is ExpressionStatement
                    && _lastExpressionValue?.Data is SequenceData innerSeq)
                {
                    _activeSectionBareExpressions.Add(innerSeq);
                }

                if (_interpreter.HasPendingReturn) break;
            }

            // Audit §2.3 — a live block runs once at this lexical level; a leaked
            // top-level return must not silently skip the rest of the program.
            if (!InsideProcCall)
                ClearLeakedReturn("live", live.Location);
        }
        finally
        {
            _context.PopFrame();
        }
    }

    /// <summary>
    /// Phase 38 Plan 38-02 — resolves a quantize <see cref="Value"/> (Int = bars,
    /// String = NoteValue token text q/h/w/e/s) to a double beat count using the
    /// active <see cref="MusicalContext"/>'s time signature.
    ///
    /// <para>
    /// Charitable per D-v1.5-05: unknown or malformed payloads silently fall
    /// back to one bar's worth of beats so the registry registration doesn't
    /// abort the script — the live-coding session keeps running ("live session
    /// never dies mid-set" Pitfall #12).
    /// </para>
    /// </summary>
    private double ResolveQuantizeBeats(Value quantizeValue)
    {
        var musicalContext = _context.GetMusicalContext();
        int numerator = musicalContext.TimeSignature?.Numerator ?? 4;
        // beatsPerBar uses the time signature's numerator (canonical 4 for 4/4,
        // 3 for 3/4, 7 for 7/8). For non-standard denominators the bar still
        // contains numerator-many denominator-units, which is what composers
        // intuitively call "a bar's worth of beats" inside a live block.
        double beatsPerBar = (double)numerator;

        // Int payload → bars (the parser produces Int for both "live N bar/bars"
        // and the omitted-default 1bar path).
        if (quantizeValue.Type is FlowLang.TypeSystem.PrimitiveTypes.IntType)
        {
            int bars = quantizeValue.As<int>();
            return bars * beatsPerBar;
        }

        // String payload → NoteValue suffix q/h/w/e/s.
        if (quantizeValue.Type is FlowLang.TypeSystem.PrimitiveTypes.StringType)
        {
            string suffix = quantizeValue.As<string>();
            double fractionOfWhole = suffix switch
            {
                "w" => 1.0,        // whole note
                "h" => 0.5,        // half note
                "q" => 0.25,       // quarter note
                "e" => 0.125,      // eighth note
                "s" => 0.0625,     // sixteenth note
                _   => 0.25,       // charitable fallback to quarter
            };
            // 4 quarter-note beats per whole note in the canonical 4-beat bar.
            return fractionOfWhole * 4.0;
        }

        // Unknown shape — charitable fallback to 1 bar.
        return beatsPerBar;
    }

    public void ExecuteSectionDeclaration(SectionDeclaration section)
    {
        // Phase 36 Plan 36-10 (D-36-18 SECT-01) — section overload registration.
        // Multiple same-name sections coexist when their parameter pattern
        // signatures DIFFER; identical signatures raise "ambiguous section
        // overload" via the declaration-time pre-flight check (Pitfall 3).
        //
        // Backward-compat: a zero-arg section (Parameters == null) is rejected
        // as a duplicate when ANOTHER zero-arg section already exists under the
        // same name — identical Parameters-null shapes are by definition
        // indistinguishable.

        if (section.Parameters != null)
        {
            // Parameterized section — DO NOT execute the body at declaration time;
            // the body executes on each call site with bound parameter values
            // pushed into a synthetic frame. Stash the declaration metadata in
            // a SectionData with empty Sequences (the call-site dispatch
            // re-runs the body and materializes the sequences).

            // Pitfall 3 pre-flight: scan existing overloads for an identical
            // pattern shape; identical shapes raise an Ambiguous-overload
            // diagnostic instead of registering.
            if (_context.SectionRegistry.TryGetValue(section.Name, out var existing))
            {
                foreach (var prior in existing)
                {
                    if (SectionsHaveIdenticalShape(prior, section))
                    {
                        _errorReporter.ReportError(
                            $"Ambiguous section overload — section '{section.Name}' " +
                            $"already declared with identical pattern shape" +
                            (prior.SourceLocation != null
                                ? $" at {prior.SourceLocation}"
                                : ""),
                            section.Location);
                        return;
                    }
                }
            }

            var musicalCtx = _context.GetMusicalContext();
            var stubData = new SectionData(
                section.Name,
                new Dictionary<string, SequenceData>(),
                musicalCtx,
                section.Location,
                parameters: section.Parameters,
                defaultValues: section.DefaultValues,
                body: section.Body);

            if (!_context.SectionRegistry.TryGetValue(section.Name, out var list))
            {
                list = new List<SectionData>();
                _context.SectionRegistry[section.Name] = list;
            }
            list.Add(stubData);
            return;
        }

        // Legacy zero-arg form: check for duplicate.
        if (_context.SectionRegistry.TryGetValue(section.Name, out var existingZero)
            && existingZero.Any(s => s.Parameters == null))
        {
            _errorReporter.ReportError(
                $"Section '{section.Name}' is already defined", section.Location);
            return;
        }

        // Push a new scope for the section body
        _context.PushFrame();
        try
        {
            // Snapshot the musical context before executing the body
            var musicalContext = _context.GetMusicalContext();

            // Track bare expression results during body execution
            var bareExpressionSequences = new List<SequenceData>();

            // Install capture sink so nested MusicalContextStatement bodies
            // (gain/tempo/timesig/key) also surface bare-expression sequences.
            var previousCapture = _activeSectionBareExpressions;
            _activeSectionBareExpressions = bareExpressionSequences;
            try
            {
                // Execute the section body
                foreach (var stmt in section.Body)
                {
                    ExecuteStatement(stmt);

                    // Capture bare expressions that produce sequences
                    if (stmt is ExpressionStatement && _lastExpressionValue?.Data is SequenceData exprSeq)
                    {
                        bareExpressionSequences.Add(exprSeq);
                    }

                    if (_interpreter.HasPendingReturn) break;
                }

                // Audit §2.3 — a section declaration is purely definitional: its
                // body runs once to collect named/bare sequences. A `return` inside
                // it never propagates to an enclosing proc (the section is collected,
                // not called here), so a leaked flag would silently skip the rest of
                // the program. Always report + clear, regardless of nesting.
                _interpreter.DiscardPendingReturn(
                    $"'return' is not allowed inside section '{section.Name}' — a section is a definition, not a function. The return was ignored.",
                    section.Location);
            }
            finally
            {
                _activeSectionBareExpressions = previousCapture;
            }

            // Collect all Sequence variables declared in the section scope
            var sequences = new Dictionary<string, SequenceData>();
            foreach (var (name, value) in _context.CurrentFrame.GetLocalVariables())
            {
                if (value.Data is SequenceData seq)
                {
                    sequences[name] = seq;
                }
            }

            // Add bare expression sequences with auto-generated names
            for (int i = 0; i < bareExpressionSequences.Count; i++)
            {
                // Only add if not already captured as a named variable
                if (!sequences.ContainsValue(bareExpressionSequences[i]))
                {
                    sequences[$"_anon_{i}"] = bareExpressionSequences[i];
                }
            }

            var sectionData = new SectionData(section.Name, sequences, musicalContext, section.Location);
            if (!_context.SectionRegistry.TryGetValue(section.Name, out var sectionList))
            {
                sectionList = new List<SectionData>();
                _context.SectionRegistry[section.Name] = sectionList;
            }
            sectionList.Add(sectionData);
        }
        finally
        {
            _context.PopFrame();
        }
    }

    /// <summary>
    /// Phase 36 Plan 36-10 (Pitfall 3) — declaration-time identical-shape check.
    /// Two parameterized sections are indistinguishable when both have the same
    /// parameter-arity AND every parameter slot has a structurally identical
    /// pattern (kind + flags + type annotation + sub-pattern shapes). Identical
    /// shapes cannot be tiebroken by the resolver; the user must change one of
    /// the signatures.
    /// </summary>
    private static bool SectionsHaveIdenticalShape(
        FlowLang.TypeSystem.SpecialTypes.SectionData prior,
        SectionDeclaration newSection)
    {
        // Zero-arg vs parameterized never collides via this path
        if (prior.Parameters == null) return false;
        if (newSection.Parameters == null) return false;
        if (prior.Parameters.Count != newSection.Parameters.Count) return false;
        for (int i = 0; i < prior.Parameters.Count; i++)
        {
            if (!PatternsHaveIdenticalShape(prior.Parameters[i], newSection.Parameters[i]))
                return false;
        }
        return true;
    }

    private static bool PatternsHaveIdenticalShape(
        FlowLang.Ast.Patterns.Pattern a,
        FlowLang.Ast.Patterns.Pattern b)
    {
        if (a.GetType() != b.GetType()) return false;
        switch (a)
        {
            case FlowLang.Ast.Patterns.BindingPattern bpA:
                var bpB = (FlowLang.Ast.Patterns.BindingPattern)b;
                return bpA.TypeAnnotation?.GetType() == bpB.TypeAnnotation?.GetType();
            case FlowLang.Ast.Patterns.ConstructorPattern cpA:
                var cpB = (FlowLang.Ast.Patterns.ConstructorPattern)b;
                if (cpA.IsChordLiteral != cpB.IsChordLiteral) return false;
                if (cpA.IsRomanNumeral != cpB.IsRomanNumeral) return false;
                if (cpA.IsArticulationSymbol != cpB.IsArticulationSymbol) return false;
                if (cpA.IsSymbolLiteral != cpB.IsSymbolLiteral) return false; // sweep-0614
                // Tuple-destructure shape: arity matters
                if (cpA.Name == "Tuple" && cpB.Name == "Tuple")
                {
                    if (cpA.SubPatterns.Count != cpB.SubPatterns.Count) return false;
                    for (int i = 0; i < cpA.SubPatterns.Count; i++)
                    {
                        if (!PatternsHaveIdenticalShape(cpA.SubPatterns[i], cpB.SubPatterns[i]))
                            return false;
                    }
                    return true;
                }
                // Non-Tuple ConstructorPattern: same flag set + same Name
                return cpA.Name == cpB.Name;
            case FlowLang.Ast.Patterns.GuardPattern gpA:
                var gpB = (FlowLang.Ast.Patterns.GuardPattern)b;
                return PatternsHaveIdenticalShape(gpA.Inner, gpB.Inner);
            default:
                // LiteralPattern / WildcardPattern have no further fields to compare
                return true;
        }
    }
}
