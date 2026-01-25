using FlowLang.Diagnostics;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;

namespace FlowLang.Runtime;

/// <summary>
/// Manages the execution state including the call stack and function registry.
/// </summary>
public class ExecutionContext
{
    private readonly Stack<StackFrame> _callStack = new();
    private readonly ErrorReporter _errorReporter;
    private readonly OverloadResolver _overloadResolver;

    public StackFrame CurrentFrame => _callStack.Peek();
    public StackFrame GlobalFrame { get; }
    public InternalFunctionRegistry InternalRegistry { get; }

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
