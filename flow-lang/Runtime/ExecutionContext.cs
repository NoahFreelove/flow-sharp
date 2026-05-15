using FlowLang.Diagnostics;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Runtime;

/// <summary>
/// Manages the execution state including the call stack and function registry.
/// </summary>
public class ExecutionContext
{
    private readonly Stack<StackFrame> _callStack = new();
    private readonly ErrorReporter _errorReporter;
    private readonly OverloadResolver _overloadResolver;
    private readonly TextWriter? _diagnosticOutput;
    private int _callDepth = 0;
    private const int MaxCallDepth = 1000;
    private int _maxIterations = 10000;

    /// <summary>
    /// Maximum number of iterations allowed per loop before the iteration guard triggers.
    /// </summary>
    public int MaxIterations
    {
        get => _maxIterations;
        set => _maxIterations = value > 0 ? value : throw new ArgumentException("MaxIterations must be positive");
    }

    // ===== Random Number Generation State =====
    
    public int FixedRandSeed { get; set; } = 0;
    public Random? FixedGen { get; set; }
    public Random? Gen { get; set; }
    public readonly object RandLock = new();

    public Random GetRand(bool fixedRng = false)
    {
        lock (RandLock)
        {
            if (fixedRng)
            {
                if (FixedGen == null)
                {
                    if (FixedRandSeed == 0) FixedRandSeed = Random.Shared.Next();
                    FixedGen = new Random(FixedRandSeed);
                }
                return FixedGen;
            }
            
            if (Gen == null)
            {
                Gen = new Random(Random.Shared.Next());
            }
            return Gen;
        }
    }

    public void ResetGen()
    {
        lock (RandLock)
        {
            FixedGen = new Random(FixedRandSeed);
        }
    }

    public void SetSeed(int seed)
    {
        FixedRandSeed = seed;
        ResetGen();
    }

    public StackFrame CurrentFrame => _callStack.Peek();
    public StackFrame GlobalFrame { get; }
    public InternalFunctionRegistry InternalRegistry { get; }
    public Dictionary<string, SectionData> SectionRegistry { get; } = new();

    /// <summary>
    /// Per-context Symbol intern table — guarantees pointer equality for <c>#foo</c> literals
    /// (Phase 26.1 SYM-01). All <c>Value.Symbol(name, ctx)</c> calls with the same name and the
    /// same context return the same <see cref="Value"/> instance, so reference-equality of the
    /// Value wrappers is the canonical Symbol equality check.
    /// </summary>
    public Dictionary<string, Value> SymbolInternTable { get; } = new();
    
    /// <summary>
    /// Invoker used to execute userspace functions/lambdas from standard library or engine.
    /// Injected by the interpreter upon creation.
    /// </summary>
    public Interpreter.IFunctionInvoker? Invoker { get; set; }

    public ExecutionContext(ErrorReporter errorReporter, InternalFunctionRegistry internalRegistry, TextWriter? diagnosticOutput = null)
    {
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        InternalRegistry = internalRegistry ?? throw new ArgumentNullException(nameof(internalRegistry));
        _diagnosticOutput = diagnosticOutput;
        _overloadResolver = new OverloadResolver(errorReporter, diagnosticOutput);

        // Create and push global frame
        GlobalFrame = new StackFrame();
        _callStack.Push(GlobalFrame);
    }

    /// <summary>
    /// The diagnostic output writer for verbose logging (null when verbose mode is off).
    /// </summary>
    public TextWriter? DiagnosticOutput => _diagnosticOutput;

    /// <summary>
    /// Pushes a new stack frame for a function call.
    /// </summary>
    public void PushFrame()
    {
        _callDepth++;
        if (_callDepth > MaxCallDepth)
            throw new InvalidOperationException($"Stack overflow: maximum call depth of {MaxCallDepth} exceeded");

        var newFrame = new StackFrame(CurrentFrame);
        _callStack.Push(newFrame);
    }

    /// <summary>
    /// Pops the current stack frame after a function returns.
    /// </summary>
    public void PopFrame()
    {
        if (_callStack.Count <= 1)
            throw new InvalidOperationException("Cannot pop global frame");

        _callStack.Pop();
        _callDepth--;
    }

    /// <summary>
    /// Declares a variable in the current frame.
    /// </summary>
    public void DeclareVariable(string name, Value value)
    {
        CurrentFrame.DeclareVariable(name, value);
    }

    /// <summary>
    /// Gets a variable from the current scope or parent scopes.
    /// </summary>
    public Value GetVariable(string name)
    {
        return CurrentFrame.GetVariable(name);
    }

    /// <summary>
    /// Sets a variable in the current scope or parent scopes.
    /// </summary>
    public void SetVariable(string name, Value value)
    {
        CurrentFrame.SetVariable(name, value);
    }

    /// <summary>
    /// Declares a function overload.
    /// </summary>
    public void DeclareFunction(FunctionOverload overload)
    {
        CurrentFrame.DeclareFunction(overload);
    }

    /// <summary>
    /// Resolves a function call to a specific overload.
    /// </summary>
    public FunctionOverload? ResolveFunction(string name, IReadOnlyList<FlowType> argTypes, Core.SourceLocation? location = null)
    {
        var overloads = CurrentFrame.GetFunctionOverloads(name);

        if (overloads.Count == 0)
        {
            _diagnosticOutput?.WriteLine($"[verbose] Function '{name}' not found (0 overloads registered)");
            _errorReporter.ReportError($"Function '{name}' not found", location);
            return null;
        }

        var signatures = overloads.Select(o => o.Signature).ToList();
        var signature = _overloadResolver.Resolve(name, signatures, argTypes, location);

        if (signature == null)
            return null;

        return overloads.FirstOrDefault(o => o.Signature == signature);
    }

    /// <summary>
    /// Resolves the current musical context by walking the stack from top to bottom.
    /// First non-null value for each property wins. Uses defaults for any unresolved properties.
    /// Defaults: 4/4 time signature, 120 BPM, 0.5 swing (straight), no key.
    ///
    /// Phase 32 D-12: <see cref="MusicalContext.TuningStack"/> resolution walks frames
    /// top-to-bottom and copies the FIRST non-empty stack onto the resolved context.
    /// This preserves the existing innermost-wins semantic of the other ??= fields:
    /// readers consume <see cref="MusicalContext.ActiveTuning"/> on the returned
    /// context, which peeks the resolved stack's top frame.
    /// </summary>
    public MusicalContext GetMusicalContext()
    {
        var resolved = new MusicalContext();
        bool tuningResolved = false;
        foreach (var frame in _callStack)
        {
            if (frame.MusicalContext != null)
            {
                resolved.TimeSignature ??= frame.MusicalContext.TimeSignature;
                resolved.Tempo ??= frame.MusicalContext.Tempo;
                resolved.Swing ??= frame.MusicalContext.Swing;
                resolved.Key ??= frame.MusicalContext.Key;
                resolved.Velocity ??= frame.MusicalContext.Velocity;
                resolved.Pan ??= frame.MusicalContext.Pan;
                resolved.Gain ??= frame.MusicalContext.Gain;
                resolved.ReverbTime ??= frame.MusicalContext.ReverbTime;
                // Phase 32 D-12 (supersedes Phase 23 D-05): Tuning is a push/pop stack.
                // Resolution walks frames top-to-bottom and adopts the first non-empty
                // stack encountered. Innermost-frame-wins matches the existing ??= shape
                // of the other fields. File-scope pragmas live on GlobalFrame.MusicalContext;
                // block forms (Plan 32-06) push above on the innermost frame, so a hit
                // higher in the call stack naturally wins.
                if (!tuningResolved && frame.MusicalContext.TuningStack.Count > 0)
                {
                    // Reference-share the stack — readers consume ActiveTuning (peek),
                    // not the stack instance, so aliasing is safe + cheaper than cloning.
                    foreach (var rt in new Stack<StandardLibrary.Audio.Tuning.RenderTuning>(frame.MusicalContext.TuningStack))
                        resolved.TuningStack.Push(rt);
                    tuningResolved = true;
                }
                // Phase 28 SPEC-7: voice pool size inherits via the same ??= chain.
                // null means "no override" — SequenceRenderer.RenderSequenceToVoicesWithPool
                // applies the SPEC-7 locked default of 32 at the render call.
                resolved.VoicePoolSize ??= frame.MusicalContext.VoicePoolSize;
            }
            if (resolved.TimeSignature != null && resolved.Tempo != null
                && resolved.Swing != null && resolved.Key != null
                && resolved.Velocity != null && resolved.Pan != null
                && resolved.Gain != null && resolved.ReverbTime != null
                && tuningResolved && resolved.VoicePoolSize != null)
                break;
        }
        // REQ-4 (Plan 30-03): three-tier fallback for Tempo + TimeSignature.
        //   1. Call-stack-resolved value (active tempo/timesig block) — already
        //      consumed in the ??= chain above.
        //   2. FlowConfig.Active (~/.config/flow/config.toml override) — this layer.
        //   3. Hard-coded baked default (120 BPM / 4/4) — final fallback.
        // Swing has no config knob in SPEC-4 so it skips tier 2.
        resolved.Tempo ??= FlowConfig.Active.DefaultTempo.HasValue
            ? (double)FlowConfig.Active.DefaultTempo.Value
            : 120.0;
        resolved.TimeSignature ??= ParseTimesigOrDefault(FlowConfig.Active.DefaultTimesig);
        resolved.Swing ??= 0.5;
        return resolved;
    }

    /// <summary>
    /// REQ-4 (Plan 30-03): parse the <c>default_timesig</c> config string ("N/M") into
    /// a <see cref="TypeSystem.SpecialTypes.TimeSignatureData"/>. Charitable per
    /// CLAUDE.md feedback_charitable_interpretation memory:
    ///   - null / whitespace -> 4/4 silently
    ///   - malformed (not "N/M" with positive integers AND power-of-2 denominator)
    ///     -> 4/4 + single stderr Warning at first encounter. The static guard
    ///     <see cref="_timesigWarningEmitted"/> avoids spamming the warning on every
    ///     <see cref="GetMusicalContext"/> call (note streams + bars + songs all hit
    ///     this code path).
    /// </summary>
    private static TypeSystem.SpecialTypes.TimeSignatureData ParseTimesigOrDefault(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
            return new TypeSystem.SpecialTypes.TimeSignatureData(4, 4);
        var parts = config.Split('/');
        if (parts.Length == 2
            && int.TryParse(parts[0], out var num) && num > 0
            && int.TryParse(parts[1], out var den) && den > 0
            // TimeSignatureData constructor validates denominator-is-power-of-2;
            // pre-check here so the throw becomes a charitable fallback instead.
            && (den & (den - 1)) == 0)
        {
            return new TypeSystem.SpecialTypes.TimeSignatureData(num, den);
        }
        if (!_timesigWarningEmitted)
        {
            Console.Error.WriteLine(
                $"Warning: malformed default_timesig in config.toml: \"{config}\" — falling back to 4/4.");
            _timesigWarningEmitted = true;
        }
        return new TypeSystem.SpecialTypes.TimeSignatureData(4, 4);
    }

    // Test-only access: reset the one-shot warning latch so successive tests can
    // each independently assert the malformed-timesig path. Intentionally internal-
    // scoped through reflection-free static reset — production code never touches it.
    private static bool _timesigWarningEmitted = false;
    internal static void ResetTimesigWarningLatchForTests() => _timesigWarningEmitted = false;

    /// <summary>
    /// Phase 32 D-12 transitional shim: bridges Phase 23's
    /// <c>SetTuning(TuningSystem?)</c> callers through to <see cref="SetFileScopeTuning"/>.
    /// Marked <see cref="ObsoleteAttribute"/> so any unmigrated FlowEngine pragma bridge
    /// surfaces as a compile warning; replaced by <see cref="SetFileScopeTuning"/> in
    /// Plan 32-05 Task 2 (FlowEngine.ApplyTuningPragma builds a <see cref="RenderTuning"/>
    /// from the pragma name). Will be removed after Plan 32-06 lands.
    /// </summary>
    [Obsolete("Phase 32 D-12: use SetFileScopeTuning(RenderTuning). Scheduled for removal after Plan 32-06 lands.")]
    public void SetTuning(TuningSystem? tuning)
    {
        if (tuning is null) return; // D-07: no-op on null — preserve previous REPL state.
        // Use the same defaults SongRenderer.ResolveRenderTuning would fall back to when
        // no key context exists (D-02 silent C-major default): tonic = ('C', 0),
        // mode = Major. The full key-aware resolution happens at section render time.
        SetFileScopeTuning(new RenderTuning(tuning.Value, Mode.Major, 'C', 0));
    }

    /// <summary>
    /// Phase 32 D-12 (supersedes Phase 23 D-06/D-07 <c>SetTuning(TuningSystem?)</c>):
    /// REPLACES the bottom-of-stack file-scope pragma frame on
    /// <see cref="StackFrame.MusicalContext"/>.<see cref="MusicalContext.TuningStack"/>
    /// of the global frame. Called by <see cref="FlowLang.Core.FlowEngine"/>'s pragma
    /// bridge once between parse and interpret.
    ///
    /// Algorithm (Pitfall 2 — bottom frame is sticky across REPL evals):
    /// <list type="number">
    ///   <item>Pop any block frames above the file-scope frame (defensive — if
    ///   <c>ResetBlockTuningStack</c> wasn't called at the prior REPL boundary,
    ///   the global frame's stack should still only carry the bottom pragma frame).</item>
    ///   <item>Pop the existing file-scope frame (if any).</item>
    ///   <item>Push the new <paramref name="renderTuning"/> as the new bottom frame.</item>
    /// </list>
    /// Net result: <c>GlobalFrame.MusicalContext.TuningStack.Count == 1</c>, containing
    /// the new file-scope tuning. D-08 REPL stickiness (carried over from Phase 23):
    /// FlowEngine's <c>ApplyTuningPragma</c> only calls this when a tuning pragma is
    /// actually present in the parsed program; absent-pragma case leaves the previous
    /// frame untouched.
    /// </summary>
    public void SetFileScopeTuning(RenderTuning renderTuning)
    {
        if (GlobalFrame.MusicalContext == null)
            GlobalFrame.MusicalContext = new MusicalContext();
        var stack = GlobalFrame.MusicalContext.TuningStack;
        while (stack.Count > 0)
            stack.Pop();
        stack.Push(renderTuning);
    }

    /// <summary>
    /// Phase 32 D-12 + Plan 32-06 entry: pushes a <see cref="RenderTuning"/> onto the
    /// topmost (current-frame) <see cref="MusicalContext.TuningStack"/>. Used by the
    /// <c>tuning t { ... }</c> block interpreter case to layer a block tuning above
    /// the file-scope pragma frame. The paired <see cref="PopTuning"/> is invoked in
    /// the block's exit (try/finally per Plan 32-06).
    /// </summary>
    public void PushTuning(RenderTuning renderTuning)
    {
        if (CurrentFrame.MusicalContext == null)
            CurrentFrame.MusicalContext = new MusicalContext();
        CurrentFrame.MusicalContext.TuningStack.Push(renderTuning);
    }

    /// <summary>
    /// Phase 32 D-12 + Plan 32-06 exit: pops the topmost frame's
    /// <see cref="MusicalContext.TuningStack"/>. Throws <see cref="InvalidOperationException"/>
    /// when the stack is empty — defensive guard that should never fire if push/pop pairs
    /// are balanced via try/finally per Plan 32-06.
    /// </summary>
    public void PopTuning()
    {
        if (CurrentFrame.MusicalContext == null || CurrentFrame.MusicalContext.TuningStack.Count == 0)
            throw new InvalidOperationException(
                "PopTuning called with an empty TuningStack — push/pop must be balanced (Phase 32 D-12).");
        CurrentFrame.MusicalContext.TuningStack.Pop();
    }

    /// <summary>
    /// Phase 32 D-14 + Pitfall 2 — REPL eval boundary hook: pops the global frame's
    /// <see cref="MusicalContext.TuningStack"/> down to at most ONE entry (the file-scope
    /// pragma frame). Block-form pushes above the pragma frame are ephemeral per D-14;
    /// the pragma frame stays sticky across REPL evals per Phase 23 D-08 carried forward.
    /// Called by the REPL eval boundary in <see cref="FlowLang.Core.FlowEngine"/>.
    ///
    /// Cardinality contract: after this call, <c>GlobalFrame.MusicalContext.TuningStack.Count</c>
    /// is <c>≤ 1</c> — exactly the file-scope pragma frame if one was pushed; empty otherwise.
    /// </summary>
    public void ResetBlockTuningStack()
    {
        if (GlobalFrame.MusicalContext == null) return;
        var stack = GlobalFrame.MusicalContext.TuningStack;
        while (stack.Count > 1)
            stack.Pop();
    }

    /// <summary>
    /// Tries to resolve a function without reporting errors (for probing).
    /// </summary>
    public FunctionOverload? TryResolveFunction(string name, IReadOnlyList<FlowType> argTypes)
    {
        var overloads = CurrentFrame.GetFunctionOverloads(name);

        if (overloads.Count == 0)
            return null;

        var signatures = overloads.Select(o => o.Signature).ToList();

        // Create a temporary error reporter that doesn't actually report
        var tempReporter = new ErrorReporter();
        var tempResolver = new OverloadResolver(tempReporter);
        var signature = tempResolver.Resolve(name, signatures, argTypes, null);

        if (signature == null)
            return null;

        return overloads.FirstOrDefault(o => o.Signature == signature);
    }
}
