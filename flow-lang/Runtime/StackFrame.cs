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

    /// <summary>
    /// Audit §2.5 (D5) — marks a frame as a USER-PROC / lambda CALL BOUNDARY.
    /// Set only by <see cref="ExecutionContext.PushFrame(bool)"/> from
    /// <c>Interpreter.ExecuteUserFunctionWithCaptures</c>'s call site. Block
    /// frames (musical-context / loop / section-call / pattern-match / live)
    /// stay <c>false</c> so they keep their lexical parent-walk.
    ///
    /// <para>
    /// When <c>true</c>, variable lookup/assignment (<see cref="TryGetVariable"/>,
    /// <see cref="GetVariable"/>, <see cref="SetVariable"/>, <see cref="HasVariable"/>)
    /// does NOT walk into <see cref="Parent"/> (the caller's scope) — it jumps
    /// straight to <see cref="GlobalScope"/> instead, so top-level/global
    /// bindings stay readable + writable from procs but the caller's LOCALS are
    /// invisible and unwritable. A proc thus has lexical (not dynamic) variable
    /// scope: it sees its own params/locals, injected closure captures (declared
    /// directly into this frame), and globals — nothing in-between. An
    /// assignment to a name found in neither this frame nor the global frame
    /// surfaces the existing composer-facing undeclared-variable diagnostic
    /// (the SetVariable throw → ExecuteAssignment's "Variable 'X' not found").
    /// </para>
    ///
    /// <para>
    /// Musical-context dynamic scope (tempo/key/timesig/tuning) is a SEPARATE
    /// mechanism that walks <see cref="ExecutionContext"/>'s <c>_callStack</c>,
    /// not this <see cref="Parent"/> chain, so it is completely unaffected.
    /// </para>
    /// </summary>
    public bool IsCallBoundary { get; init; }

    /// <summary>
    /// Audit §2.5 (D5) — the GLOBAL (root) frame, supplied when this frame is a
    /// <see cref="IsCallBoundary"/>. Variable walk-up across the boundary
    /// redirects here instead of <see cref="Parent"/> so global bindings stay
    /// reachable. Null on non-boundary frames (they walk <see cref="Parent"/>).
    /// </summary>
    public StackFrame? GlobalScope { get; init; }

    /// <summary>
    /// The frame the variable parent-walk should continue into. For a call
    /// boundary that is <see cref="GlobalScope"/> (skipping the caller's locals);
    /// otherwise it is the lexical <see cref="Parent"/>.
    /// </summary>
    private StackFrame? VariableWalkParent => IsCallBoundary ? GlobalScope : Parent;

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

        // Audit §2.5 (D5) — a call boundary redirects the walk to the global
        // frame (GlobalScope) instead of the caller's Parent, so a proc sees
        // its own locals + globals but NOT the caller's locals (lexical scope).
        var next = VariableWalkParent;
        if (next != null)
            return next.GetVariable(name);

        throw new InvalidOperationException($"Variable '{name}' not found");
    }

    /// <summary>
    /// Bundle B (260524-rjm) hot-path probe. Walks this → parent chain
    /// identically to <see cref="GetVariable"/> but returns <c>false</c>
    /// instead of throwing on miss. Does NOT throw under any circumstance.
    /// Use this in dispatch hot paths; use <see cref="GetVariable"/> where
    /// the throw IS the correct semantic.
    /// </summary>
    public bool TryGetVariable(string name, out Value value)
    {
        if (_variables.TryGetValue(name, out var v))
        {
            value = v;
            return true;
        }

        // Audit §2.5 (D5) — call-boundary redirect (see GetVariable).
        var next = VariableWalkParent;
        if (next != null)
            return next.TryGetVariable(name, out value);

        value = default!;
        return false;
    }

    public void SetVariable(string name, Value value)
    {
        if (_variables.ContainsKey(name))
        {
            _variables[name] = value;
            return;
        }

        // Audit §2.5 (D5) — a call boundary blocks write-through to the
        // caller's locals: the walk redirects to the global frame, so a
        // top-level/global binding stays writable from a proc but a name
        // declared only in the caller is NOT silently mutated. A name found
        // in neither this frame nor globals throws (→ undeclared-variable
        // diagnostic at the ExecuteAssignment call site).
        var next = VariableWalkParent;
        if (next != null)
        {
            next.SetVariable(name, value);
            return;
        }

        throw new InvalidOperationException($"Variable '{name}' not found");
    }

    public bool HasVariable(string name)
    {
        if (_variables.ContainsKey(name))
            return true;
        // Audit §2.5 (D5) — call-boundary redirect (see GetVariable).
        return VariableWalkParent?.HasVariable(name) ?? false;
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
    ///
    /// <para>
    /// Audit §2.5 (D5) — the walk follows <see cref="VariableWalkParent"/>, so
    /// at a call boundary it jumps to <see cref="GlobalScope"/> rather than the
    /// caller's <see cref="Parent"/>. This keeps lambda capture LEXICAL: a
    /// lambda created inside a proc body snapshots that proc's locals + globals,
    /// not the caller's locals (which were never in the lambda's lexical scope).
    /// </para>
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
            current = current.VariableWalkParent;
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
