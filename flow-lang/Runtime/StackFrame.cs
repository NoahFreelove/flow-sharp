using FlowLang.TypeSystem;

namespace FlowLang.Runtime;

/// <summary>
/// Represents a single stack frame containing variables and functions in a scope.
/// </summary>
public class StackFrame
{
    private readonly Dictionary<string, Value> _variables = new();
    private readonly Dictionary<string, List<FunctionOverload>> _functions = new();

    public StackFrame? Parent { get; }

    /// <summary>
    /// Optional musical context for this scope. Null means "inherit from parent".
    /// </summary>
    public MusicalContext? MusicalContext { get; set; }

    public StackFrame(StackFrame? parent = null)
    {
        Parent = parent;
    }

    // Variable management

    public void DeclareVariable(string name, Value value)
    {
        if (_variables.ContainsKey(name))
            throw new InvalidOperationException($"Variable '{name}' already declared in this scope");

        _variables[name] = value;
    }

    public Value GetVariable(string name)
    {
        if (_variables.TryGetValue(name, out var value))
            return value;

        if (Parent != null)
            return Parent.GetVariable(name);

        throw new InvalidOperationException($"Variable '{name}' not found");
    }

    public void SetVariable(string name, Value value)
    {
        if (_variables.ContainsKey(name))
        {
            _variables[name] = value;
            return;
        }

        if (Parent != null)
        {
            Parent.SetVariable(name, value);
            return;
        }

        throw new InvalidOperationException($"Variable '{name}' not found");
    }

    public bool HasVariable(string name)
    {
        return _variables.ContainsKey(name) || (Parent?.HasVariable(name) ?? false);
    }

    /// <summary>
    /// Gets all variables declared in this frame (not including parent frames).
    /// </summary>
    public IReadOnlyDictionary<string, Value> GetLocalVariables()
    {
        return _variables;
    }

    /// <summary>
    /// Gets all variables accessible from this frame, including parent frames.
    /// If a variable is shadowed, only the most local version is included.
    /// </summary>
    public IReadOnlyDictionary<string, Value> GetAllAccessibleVariables()
    {
        var result = new Dictionary<string, Value>();

        // Start from root and work down so local variables override parent variables
        var frames = new Stack<StackFrame>();
        var current = this;
        while (current != null)
        {
            frames.Push(current);
            current = current.Parent;
        }

        while (frames.Count > 0)
        {
            var frame = frames.Pop();
            foreach (var (name, value) in frame._variables)
            {
                result[name] = value; // Overwrite if shadowed
            }
        }

        return result;
    }

    // Function management

    public void DeclareFunction(FunctionOverload overload)
    {
        if (HasVariable(overload.Name))
            throw new InvalidOperationException($"Cannot declare function '{overload.Name}': a variable with that name already exists");

        if (!_functions.ContainsKey(overload.Name))
        {
            _functions[overload.Name] = [];
        }

        // Check if an overload with matching signature already exists
        var existingIndex = _functions[overload.Name]
            .FindIndex(existing => existing.Signature.Equals(overload.Signature));

        if (existingIndex >= 0)
        {
            // Replace existing overload (for REPL redefinition)
            _functions[overload.Name][existingIndex] = overload;
        }
        else
        {
            // Add new overload
            _functions[overload.Name].Add(overload);
        }
    }

    /// <summary>
    /// Returns the list of <see cref="FunctionOverload"/> registrations for
    /// <paramref name="name"/> visible from this frame, walking the parent
    /// chain top-down.
    ///
    /// <para>
    /// Bundle A (260524-r4o) — fast path: the returned list MUST be treated
    /// as read-only by callers. When only one frame in the parent chain
    /// holds overloads for this name, this method returns a direct reference
    /// to that frame's internal list to avoid per-call allocation. Mutating
    /// the returned list is undefined behavior — register new overloads via
    /// <see cref="DeclareFunction"/>. All 5 in-tree callers (StackFrame
    /// self-recursion, ModuleLoader, ExpressionEvaluator,
    /// ExecutionContext.ResolveFunction, ExecutionContext.TryResolveFunction)
    /// are read-only as of Bundle A (260524-r4o); if a future caller mutates
    /// the returned list, fix the caller (not this method).
    /// </para>
    /// </summary>
    public List<FunctionOverload> GetFunctionOverloads(string name)
    {
        if (_functions.TryGetValue(name, out var localOverloads))
        {
            // Fast path: local hit AND parent chain has no shadow for this name
            // → return the internal list directly (read-only contract above).
            if (Parent == null || !Parent.HasFunction(name))
                return localOverloads;

            // Multi-frame shadow: allocate-merge as before. Recurse into Parent;
            // Parent's recursion may itself land on its own fast path, but the
            // result is still consumed read-only via AddRange here.
            var overloads = new List<FunctionOverload>(localOverloads);
            overloads.AddRange(Parent.GetFunctionOverloads(name));
            return overloads;
        }

        // No local hit: defer entirely to parent (no wrapping). Parent's fast
        // path may return its own internal list — safe because callers are
        // read-only by contract.
        if (Parent == null)
            return new List<FunctionOverload>(0);
        return Parent.GetFunctionOverloads(name);
    }

    public bool HasFunction(string name)
    {
        return _functions.ContainsKey(name) || (Parent?.HasFunction(name) ?? false);
    }

    /// <summary>
    /// Phase 35 Plan 35-04 TEST-02 — snapshot helper used by
    /// <see cref="ExecutionContext.SnapshotState"/>. Returns a SHALLOW copy
    /// of this frame's local variable dictionary so subsequent mutations
    /// (DeclareVariable / SetVariable on the same key) do not leak into the
    /// snapshot. Values themselves are not deep-cloned — Flow Values are
    /// effectively-immutable wrappers (Data is set in the constructor) and
    /// the underlying CLR objects that mutate at runtime (AudioBuffer,
    /// SequenceData, etc.) are captured by reference. Tests that mutate
    /// shared-by-reference Audio/Sequence data still affect each other
    /// (documented limitation — assertion semantics should be value-based
    /// or use fresh constructions per test).
    /// </summary>
    public Dictionary<string, Value> SnapshotLocalVariables()
        => new Dictionary<string, Value>(_variables);

    /// <summary>
    /// Phase 35 Plan 35-04 TEST-02 — restore helper used by
    /// <see cref="ExecutionContext.RestoreState"/>. Replaces this frame's
    /// local variable dictionary with <paramref name="snapshot"/>'s
    /// contents — any keys declared since the snapshot are dropped, any
    /// values reassigned are reverted. The function-overload map and
    /// MusicalContext are NOT touched (those have separate snapshot fields).
    /// </summary>
    public void RestoreLocalVariables(IReadOnlyDictionary<string, Value> snapshot)
    {
        _variables.Clear();
        foreach (var (k, v) in snapshot)
            _variables[k] = v;
    }
}

/// <summary>
/// Represents a function overload (either user-defined or internal).
/// </summary>
public class FunctionOverload
{
    public string Name { get; }
    public FunctionSignature Signature { get; }
    public Func<IReadOnlyList<Value>, Value>? Implementation { get; }
    public Ast.Statements.ProcDeclaration? Declaration { get; }

    /// <summary>
    /// Variables captured at lambda creation time (snapshot capture).
    /// Null for non-lambda functions. When set, these bindings are injected
    /// into the lambda's execution frame before the body runs.
    /// </summary>
    public IReadOnlyDictionary<string, Value>? CapturedVariables { get; }

    public bool IsInternal => Implementation != null;

    private FunctionOverload(
        string name,
        FunctionSignature signature,
        Func<IReadOnlyList<Value>, Value>? implementation,
        Ast.Statements.ProcDeclaration? declaration,
        IReadOnlyDictionary<string, Value>? capturedVariables = null)
    {
        Name = name;
        Signature = signature;
        Implementation = implementation;
        Declaration = declaration;
        CapturedVariables = capturedVariables;
    }

    public static FunctionOverload Internal(
        string name,
        FunctionSignature signature,
        Func<IReadOnlyList<Value>, Value> implementation)
    {
        return new FunctionOverload(name, signature, implementation, null);
    }

    public static FunctionOverload UserDefined(
        string name,
        FunctionSignature signature,
        Ast.Statements.ProcDeclaration declaration,
        IReadOnlyDictionary<string, Value>? capturedVariables = null)
    {
        return new FunctionOverload(name, signature, null, declaration, capturedVariables);
    }
}
