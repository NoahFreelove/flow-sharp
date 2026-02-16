using FlowLang.Diagnostics;
using FlowLang.StandardLibrary;
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
    private int _callDepth = 0;
    private const int MaxCallDepth = 1000;

    public StackFrame CurrentFrame => _callStack.Peek();
    public StackFrame GlobalFrame { get; }
    public InternalFunctionRegistry InternalRegistry { get; }
    public Dictionary<string, SectionData> SectionRegistry { get; } = new();

    public ExecutionContext(ErrorReporter errorReporter, InternalFunctionRegistry internalRegistry)
    {
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        InternalRegistry = internalRegistry ?? throw new ArgumentNullException(nameof(internalRegistry));
        _overloadResolver = new OverloadResolver(errorReporter);

        // Create and push global frame
        GlobalFrame = new StackFrame();
        _callStack.Push(GlobalFrame);
    }

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
            }
            if (resolved.TimeSignature != null && resolved.Tempo != null
                && resolved.Swing != null && resolved.Key != null
                && resolved.Velocity != null)
                break;
        }
        // Defaults
        resolved.TimeSignature ??= new TypeSystem.SpecialTypes.TimeSignatureData(4, 4);
        resolved.Tempo ??= 120.0;
        resolved.Swing ??= 0.5;
        return resolved;
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
