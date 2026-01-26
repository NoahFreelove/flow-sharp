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

    public StackFrame(StackFrame? parent = null)
    {
        Parent = parent;
    }

    // Variable management

    public void DeclareVariable(string name, Value value)
    {
        if (_variables.ContainsKey(name))
            throw new InvalidOperationException($"Variable '{name}' already declared in this scope");

        if (HasFunction(name))
            throw new InvalidOperationException($"Cannot declare variable '{name}': a function with that name already exists");

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

    public List<FunctionOverload> GetFunctionOverloads(string name)
    {
        var overloads = new List<FunctionOverload>();

        // Collect from current frame
        if (_functions.TryGetValue(name, out var localOverloads))
            overloads.AddRange(localOverloads);

        // Collect from parent frames
        if (Parent != null)
            overloads.AddRange(Parent.GetFunctionOverloads(name));

        return overloads;
    }

    public bool HasFunction(string name)
    {
        return _functions.ContainsKey(name) || (Parent?.HasFunction(name) ?? false);
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

    public bool IsInternal => Implementation != null;

    private FunctionOverload(
        string name,
        FunctionSignature signature,
        Func<IReadOnlyList<Value>, Value>? implementation,
        Ast.Statements.ProcDeclaration? declaration)
    {
        Name = name;
        Signature = signature;
        Implementation = implementation;
        Declaration = declaration;
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
        Ast.Statements.ProcDeclaration declaration)
    {
        return new FunctionOverload(name, signature, null, declaration);
    }
}
