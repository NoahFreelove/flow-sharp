using FlowLang.Ast;
using FlowLang.Ast.Statements;
using FlowLang.Ast.Expressions;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using System.Numerics;
using RuntimeContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Interpreter;

/// <summary>
/// Signal exception thrown by break statements to exit the innermost loop.
/// </summary>
public class BreakSignal : Exception { }

/// <summary>
/// Signal exception thrown by continue statements to skip to the next loop iteration.
/// </summary>
public class ContinueSignal : Exception { }

/// <summary>
/// Main interpreter for executing Flow AST.
/// </summary>
public class Interpreter : IFunctionInvoker
{
    private readonly RuntimeContext _context;
    private readonly ErrorReporter _errorReporter;
    private readonly ExpressionEvaluator _evaluator;
    private readonly ModuleLoader _moduleLoader;
    private Value? _returnValue;
    private Value? _lastExpressionValue;
    private List<SequenceData>? _activeSectionBareExpressions;
    private int _recursionDepth = 0;
    private const int MaxRecursionDepth = 1000;

    public Interpreter(RuntimeContext context, ErrorReporter errorReporter, ModuleLoader? moduleLoader = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        _evaluator = new ExpressionEvaluator(context, errorReporter, this);
        _moduleLoader = moduleLoader ?? new ModuleLoader(errorReporter);

        // Wire up the invoker for higher-order functions
        _context.Invoker = this;
    }

    /// <summary>
    /// Gets the last expression value from the most recent execution (for REPL).
    /// </summary>
    public Value? GetLastExpressionValue() => _lastExpressionValue;

    /// <summary>
    /// Phase 36 Plan 36-10 — IFunctionInvoker contract; exposes the last
    /// evaluated expression value to the ExpressionEvaluator's section-call
    /// dispatcher so bare-expression sequences emitted by a parameterized
    /// section body can be captured at call time.
    /// </summary>
    public Value? LastExpressionValue => _lastExpressionValue;

    /// <summary>
    /// Executes a program.
    /// </summary>
    public void Execute(Program program)
    {
        _lastExpressionValue = null;  // Clear previous value

        foreach (var statement in program.Statements)
        {
            ExecuteStatement(statement);
        }
    }

    /// <summary>
    /// Executes a single statement.
    /// </summary>
    public void ExecuteStatement(Statement stmt)
    {
        if (_returnValue != null)
            return; // Already returned
        // AUDIT-VERIFIED 2026-04-18: C2 — Dismissed: _returnValue only set by ReturnStatement; guard is correct (tests/spike/c2-return-value-short-circuit.flow)

        switch (stmt)
        {
            case ProcDeclaration proc:
                ExecuteProcDeclaration(proc);
                break;

            case VariableDeclaration varDecl:
                ExecuteVariableDeclaration(varDecl);
                break;

            case TupleDestructureStatement destruct:
                ExecuteTupleDestructure(destruct);
                break;

            case AssignmentStatement assignment:
                ExecuteAssignment(assignment);
                break;

            case ReturnStatement ret:
                ExecuteReturn(ret);
                break;

            case ImportStatement import:
                ExecuteImport(import);
                break;

            // Phase 43 Plan 43-03 D-05: ModuleDeclarationStatement is consumed by
            // ModuleLoader BEFORE Interpreter.Execute returns (the loader inspects
            // program.Statements[0] post-Execute to register the module name).
            // At execute-time the statement is metadata-only — no runtime action
            // required. We add an explicit arm so the default `NotSupportedException`
            // branch below does not fire when the program ITSELF carries a
            // `module <name>` declaration (top-level scripts or REPL evals).
            case ModuleDeclarationStatement:
                break;

            case SectionDeclaration section:
                ExecuteSectionDeclaration(section);
                break;

            case MusicalContextStatement ctx:
                ExecuteMusicalContext(ctx);
                break;

            case TuningContextStatement tctx:
                ExecuteTuningContext(tctx);
                break;

            case LiveBlockStatement live:
                ExecuteLiveBlock(live);
                break;

            case ExpressionStatement exprStmt:
                var value = _evaluator.Evaluate(exprStmt.Expression);
                _lastExpressionValue = value;  // Store for REPL
                break;

            case ForStatement forStmt:
                ExecuteForStatement(forStmt);
                break;

            case WhileStatement whileStmt:
                ExecuteWhileStatement(whileStmt);
                break;

            case BreakStatement:
                throw new BreakSignal();

            case ContinueStatement:
                throw new ContinueSignal();

            default:
                throw new NotSupportedException($"Statement type {stmt.GetType().Name} not supported");
        }
    }

    private void ExecuteMusicalContext(MusicalContextStatement ctx)
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

                if (_returnValue != null) break;
            }
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
    private void ExecuteTuningContext(TuningContextStatement tctx)
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

        // Step 3: extract the ResolvedTuning. Value.Tuning(ResolvedTuning) is the
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

                if (_returnValue != null) break;
            }
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
    private void ExecuteLiveBlock(LiveBlockStatement live)
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

                if (_returnValue != null) break;
            }
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

    private void ExecuteForStatement(ForStatement stmt)
    {
        var collectionValue = _evaluator.Evaluate(stmt.Collection);
        var items = collectionValue.Data as List<Value>;
        if (items == null)
        {
            _errorReporter.ReportError($"Cannot iterate over {collectionValue.Type.Name}; expected an array", stmt.Location);
            return;
        }
        int iterations = 0;
        foreach (var item in items)
        {
            if (++iterations > _context.MaxIterations)
            {
                _errorReporter.ReportError($"Iteration limit of {_context.MaxIterations} exceeded in for loop", stmt.Location);
                break;
            }
            _context.PushFrame();
            try
            {
                _context.CurrentFrame.DeclareVariable(stmt.VariableName, item);
                foreach (var bodyStmt in stmt.Body)
                {
                    ExecuteStatement(bodyStmt);
                    if (_returnValue != null) return;
                }
            }
            catch (BreakSignal) { break; }
            catch (ContinueSignal) { continue; }
            finally { _context.PopFrame(); }
        }
    }

    private void ExecuteWhileStatement(WhileStatement stmt)
    {
        int iterations = 0;
        while (true)
        {
            if (++iterations > _context.MaxIterations)
            {
                _errorReporter.ReportError($"Iteration limit of {_context.MaxIterations} exceeded in while loop", stmt.Location);
                break;
            }
            var condValue = _evaluator.Evaluate(stmt.Condition);
            if (condValue.Data is not bool condBool)
            {
                _errorReporter.ReportError("While condition must evaluate to Bool", stmt.Location);
                return;
            }
            if (!condBool) break;

            _context.PushFrame();
            try
            {
                foreach (var bodyStmt in stmt.Body)
                {
                    ExecuteStatement(bodyStmt);
                    if (_returnValue != null) return;
                }
            }
            catch (BreakSignal) { break; }
            catch (ContinueSignal) { continue; }
            finally { _context.PopFrame(); }
        }
    }

    private void ExecuteSectionDeclaration(SectionDeclaration section)
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

                    if (_returnValue != null) break;
                }
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

    private void ExecuteProcDeclaration(ProcDeclaration proc)
    {
        // Create function signature
        var inputTypes = proc.Parameters.Select(p => p.Type).ToList();
        var isVarArgs = proc.Parameters.Any(p => p.IsVarArgs);

        var signature = new FunctionSignature(proc.Name, inputTypes, isVarArgs);

        if (proc.IsInternal)
        {
            // Look up C# implementation for internal procedure
            if (_context.InternalRegistry.TryGetImplementation(proc.Name, signature, out var impl, out var registeredSignature))
            {
                // Use the registered signature which has the correct IsVarArgs flag
                var overload = FunctionOverload.Internal(proc.Name, registeredSignature!, impl!);
                _context.DeclareFunction(overload);
            }
            else
            {
                _errorReporter.ReportError(
                    $"No C# implementation found for internal proc '{proc.Name}' with signature {signature}",
                    proc.Location);
            }
        }
        else
        {
            // User-defined function
            var overload = FunctionOverload.UserDefined(proc.Name, signature, proc);
            _context.DeclareFunction(overload);
        }
    }

    private void ExecuteVariableDeclaration(VariableDeclaration varDecl)
    {
        var value = _evaluator.Evaluate(varDecl.Value);

        // Check if this is a default value initialization (when expression evaluates to Int 0 for non-Int types)
        // Exclude NoteValue since it's int-backed and 0 is a valid enum value (WHOLE)
        bool isDefaultInit = value.Type is IntType && value.As<int>() == 0 && varDecl.Type is not IntType
            && varDecl.Type is not TypeSystem.SpecialTypes.NoteValueType;

        if (isDefaultInit)
        {
            // Create appropriate default value for the type
            value = CreateDefaultValue(varDecl.Type);
        }
        else
        {
            // Phase 26: variable initialization may need to narrow Double→Float
            // (e.g., `Float a = 1.5` where 1.5 lexes as Double). Value.ConvertTo
            // already implements Int→Long→Float→Double→Number widening AND the
            // narrowing direction Double→Float (line 114). The Type-level
            // CanConvertTo only declares the widening side to keep OverloadResolver
            // unambiguous; here at the assignment boundary we additionally try a
            // direct Value.ConvertTo for the narrow numeric cases.
            bool typeCompatible = value.Type.IsCompatibleWith(varDecl.Type)
                || value.Type.CanConvertTo(varDecl.Type);

            // Try direct Value-level coercion for numeric narrowing (Double→Float, etc.)
            if (!typeCompatible && IsNumericNarrowing(value.Type, varDecl.Type))
            {
                try
                {
                    var coerced = value.ConvertTo(varDecl.Type);
                    if (coerced.Type.Equals(varDecl.Type))
                    {
                        value = coerced;
                        typeCompatible = true;
                    }
                }
                catch { /* fall through to error */ }
            }

            // Type checking (simplified - just check if compatible)
            // Skip type check for function values (lambdas assigned to variables with return-type annotations)
            if (value.Type is not TypeSystem.PrimitiveTypes.FunctionType && !typeCompatible)
            {
                _errorReporter.ReportError(
                    $"Cannot assign {value.Type} to variable of type {varDecl.Type}",
                    varDecl.Location);
                return;
            }

            // Convert if needed
            if (!value.Type.Equals(varDecl.Type) && value.Type.CanConvertTo(varDecl.Type))
            {
                value = value.ConvertTo(varDecl.Type);
            }
        }

        // Phase 33 D-12: register typed-Sfz bindings in the patch registry so
        // renderSong song "sampler:violin" can find the bound patch by name.
        // Reassignment-overwrite is naturally handled by Dictionary indexer
        // semantics (Pitfall 10's last-bound-wins contract).
        if (varDecl.Type is FlowLang.TypeSystem.SpecialTypes.SfzType &&
            value.Data is FlowLang.StandardLibrary.Audio.Sfz.SfzData sfzData)
        {
            _context.SfzPatchRegistry[varDecl.Name] = sfzData;
        }

        _context.DeclareVariable(varDecl.Name, value);
    }

    /// <summary>
    /// Phase 26.1 TUP-09: executes <c>&lt;&lt;Type? name, Type? name, ...&gt;&gt; = expr</c>.
    /// Evaluates the RHS once, validates it is a Tuple, runtime-checks arity, then per-slot
    /// type-checks (when an annotation is provided) before binding each component into the
    /// current frame. Type-mismatch and arity-mismatch are soft errors (mirrors
    /// <see cref="ExecuteVariableDeclaration"/> precedent so the rest of the program continues).
    /// </summary>
    private void ExecuteTupleDestructure(TupleDestructureStatement stmt)
    {
        var rhs = _evaluator.Evaluate(stmt.Value);
        if (rhs.Type is not TupleType || rhs.Data is not IReadOnlyList<Value> tupArr)
        {
            _errorReporter.ReportError(
                $"Right-hand side of destructure must be a Tuple, got {rhs.Type}",
                stmt.Location);
            return;
        }
        if (tupArr.Count != stmt.Patterns.Count)
        {
            _errorReporter.ReportError(
                $"Tuple destructure arity mismatch: pattern has {stmt.Patterns.Count} slot(s), value has {tupArr.Count}",
                stmt.Location);
            return;
        }
        for (int i = 0; i < stmt.Patterns.Count; i++)
        {
            var pattern = stmt.Patterns[i];
            var component = tupArr[i];
            if (pattern.Type != null
                && !component.Type.IsCompatibleWith(pattern.Type)
                && !component.Type.CanConvertTo(pattern.Type))
            {
                _errorReporter.ReportError(
                    $"Cannot bind tuple component {i} of type {component.Type} to {pattern.Type} {pattern.Name}",
                    stmt.Location);
                return;
            }
            if (pattern.Type != null
                && !component.Type.Equals(pattern.Type)
                && component.Type.CanConvertTo(pattern.Type))
            {
                component = component.ConvertTo(pattern.Type);
            }
            _context.DeclareVariable(pattern.Name, component);
        }
    }

    /// <summary>
    /// Phase 26: detects numeric-narrowing initialization like `Float a = 1.5`
    /// where Value.ConvertTo can produce the narrower type but the FlowType-level
    /// CanConvertTo doesn't (intentionally — to keep OverloadResolver unambiguous).
    /// </summary>
    private static bool IsNumericNarrowing(FlowType from, FlowType to)
    {
        return (from is TypeSystem.PrimitiveTypes.DoubleType && to is TypeSystem.PrimitiveTypes.FloatType)
            || (from is TypeSystem.PrimitiveTypes.LongType && to is TypeSystem.PrimitiveTypes.IntType);
    }

    private Value CreateDefaultValue(FlowType type)
    {
        // Phase 26.1 TUP-09: Tuple default-init constructs per-position default values
        // recursively (so `Tuple<<Note, Beat>> entry` produces `<<C4, 0.0>>`).
        if (type is TupleType tt)
        {
            if (tt.IsAnyArity)
                return Value.Tuple(new List<Value>(), new List<FlowType>());
            var components = new List<Value>(tt.ElementTypes.Count);
            foreach (var et in tt.ElementTypes)
                components.Add(CreateDefaultValue(et));
            return Value.Tuple(components, tt.ElementTypes);
        }

        return type switch
        {
            IntType => Value.Int(0),
            FloatType => Value.Float(0.0),
            LongType => Value.Long(0L),
            DoubleType => Value.Double(0.0),
            StringType => Value.String(""),
            BoolType => Value.Bool(false),
            NumberType => Value.Number(System.Numerics.BigInteger.Zero),
            ArrayType arr => Value.Array(new List<Value>(), arr.ElementType),
            BufferType => Value.Buffer(null),
            NoteType => Value.Note("C4"),
            SemitoneType => Value.Semitone(0),
            CentType => Value.Cent(0.0),
            MillisecondType => Value.Millisecond(0.0),
            SecondType => Value.Second(0.0),
            DecibelType => Value.Decibel(0.0),
            BeatType => Value.Beat(0.0),
            NoteValueType => Value.NoteValue(0),
            TimeSignatureType => Value.TimeSignature(new TimeSignatureData(4, 4)),
            SequenceType => Value.Sequence(new SequenceData()),
            BarType => Value.Bar(new BarData(new List<MusicalNoteData>(), new TimeSignatureData(4, 4))),
            _ => Value.Void()
        };
    }

    private void ExecuteAssignment(AssignmentStatement assignment)
    {
        // Evaluate new value
        var newValue = _evaluator.Evaluate(assignment.Value);

        // Get existing variable to check type compatibility.
        // Bundle B (260524-rjm) — non-throwing TryGetVariable replaces the
        // legacy try/catch on InvalidOperationException. Identical type-check
        // + conversion + SetVariable semantics on the found branch; identical
        // error wording on the not-found branch.
        if (_context.CurrentFrame.TryGetVariable(assignment.Name, out var existingValue))
        {
            var targetType = existingValue.Type;

            // Type check
            if (!newValue.Type.IsCompatibleWith(targetType) &&
                !newValue.Type.CanConvertTo(targetType))
            {
                _errorReporter.ReportError(
                    $"Cannot assign {newValue.Type} to variable of type {targetType}",
                    assignment.Location);
                return;
            }

            // Convert if needed
            if (!newValue.Type.Equals(targetType) && newValue.Type.CanConvertTo(targetType))
            {
                newValue = newValue.ConvertTo(targetType);
            }

            // Update variable
            _context.SetVariable(assignment.Name, newValue);
        }
        else
        {
            _errorReporter.ReportError(
                $"Variable '{assignment.Name}' not found",
                assignment.Location);
        }
    }

    private void ExecuteReturn(ReturnStatement ret)
    {
        _returnValue = _evaluator.Evaluate(ret.Value);
    }

    private void ExecuteImport(ImportStatement import)
    {
        // Get current file from import statement location
        string? currentFile = import.Location.FileName;

        var result = _moduleLoader.LoadModule(import.FilePath, currentFile ?? "", _context, import.Location);

        if (result == ModuleLoadResult.Error)
        {
            _errorReporter.ReportError($"Failed to import '{import.FilePath}'", import.Location);
        }
    }

    /// <summary>
    /// Executes a user-defined function.
    /// </summary>
    public Value ExecuteUserFunction(ProcDeclaration proc, IReadOnlyList<Value> args)
    {
        return ExecuteUserFunctionWithCaptures(proc, args, null);
    }

    /// <summary>
    /// Executes a user-defined function with optional captured closure variables.
    /// </summary>
    public Value ExecuteUserFunctionWithCaptures(
        ProcDeclaration proc,
        IReadOnlyList<Value> args,
        IReadOnlyDictionary<string, Value>? capturedVariables)
    {
        if (++_recursionDepth > MaxRecursionDepth)
        {
            _recursionDepth--;
            _errorReporter.ReportError($"Recursion depth limit ({MaxRecursionDepth}) exceeded", proc.Location);
            return Value.Void();
        }

        // Create new stack frame
        _context.PushFrame();

        try
        {
            // Inject captured closure variables (snapshot from lambda creation time)
            if (capturedVariables != null)
            {
                foreach (var (name, value) in capturedVariables)
                {
                    _context.DeclareVariable(name, value);
                }
            }

            // Bind parameters (may shadow captured variables, which is correct)
            for (int i = 0; i < proc.Parameters.Count; i++)
            {
                var param = proc.Parameters[i];
                Value paramValue;

                if (param.IsVarArgs)
                {
                    // Check if we're passing a single array argument that already matches the expected type
                    if (args.Count - i == 1 && args[i].Type is ArrayType arrayType && arrayType.ElementType.Equals(param.Type))
                    {
                        // Use the array directly instead of wrapping it
                        paramValue = args[i];
                    }
                    else
                    {
                        // Collect remaining arguments into an array
                        var varArgs = new List<Value>();
                        for (int j = i; j < args.Count; j++)
                        {
                            varArgs.Add(args[j]);
                        }

                        // Create array value with the parameter's base type as element type
                        paramValue = Value.Array(varArgs, param.Type);
                    }
                }
                else
                {
                    paramValue = args[i];
                }

                // Use SetVariable if the name was already declared (from captures), otherwise declare
                if (capturedVariables != null && capturedVariables.ContainsKey(param.Name))
                    _context.SetVariable(param.Name, paramValue);
                else
                    _context.DeclareVariable(param.Name, paramValue);
            }

            // Execute function body with implicit return collection
            var collector = new ImplicitReturnCollector();
            _returnValue = null;

            foreach (var statement in proc.Body)
            {
                if (_returnValue != null)
                    break; // Explicit return encountered

                ExecuteStatement(statement);

                // If statement was an expression, collect its value (already evaluated in ExecuteStatement)
                if (statement is ExpressionStatement)
                {
                    collector.Collect(_lastExpressionValue ?? Value.Void());
                }
            }

            // Return result
            if (_returnValue != null)
            {
                var result = _returnValue;
                _returnValue = null;
                return result;
            }

            return collector.GetResult();
        }
        finally
        {
            _context.PopFrame();
            _recursionDepth--;
        }
    }
}
