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
    /// </summary>
    public MusicalContext GetMusicalContext()
    {
        var resolved = new MusicalContext();
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
                // Phase 23 D-05: Tuning is a top-level non-stacked field. Inherit via the
                // same ??= merge pattern; D-07 REPL persistence is preserved because
                // FlowEngine.SetTuning writes to GlobalFrame and never clears on null.
                resolved.Tuning ??= frame.MusicalContext.Tuning;
            }
            if (resolved.TimeSignature != null && resolved.Tempo != null
                && resolved.Swing != null && resolved.Key != null
                && resolved.Velocity != null && resolved.Pan != null
                && resolved.Gain != null && resolved.ReverbTime != null
                && resolved.Tuning != null)
                break;
        }
        // Defaults
        resolved.TimeSignature ??= new TypeSystem.SpecialTypes.TimeSignatureData(4, 4);
        resolved.Tempo ??= 120.0;
        resolved.Swing ??= 0.5;
        return resolved;
    }

    /// <summary>
    /// Phase 23 D-06/D-07: writes the resolved tuning system into the global (root) frame's
    /// <see cref="MusicalContext"/>. D-07 REPL persistence: passing <c>null</c> is a no-op
    /// (does NOT clear). Only an explicit value mutates the GlobalFrame's
    /// <see cref="MusicalContext.Tuning"/>. Called by <see cref="FlowLang.Core.FlowEngine"/>'s
    /// pragma bridge once between parse and interpret.
    /// </summary>
    public void SetTuning(TuningSystem? tuning)
    {
        if (tuning is null) return; // D-07: no-op on null — preserve previous REPL state.
        if (GlobalFrame.MusicalContext == null)
            GlobalFrame.MusicalContext = new MusicalContext();
        GlobalFrame.MusicalContext.Tuning = tuning;
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
