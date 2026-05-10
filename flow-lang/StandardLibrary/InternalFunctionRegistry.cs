using FlowLang.Runtime;
using FlowLang.TypeSystem;

namespace FlowLang.StandardLibrary;

/// <summary>
/// Registry of C# implementations for internal procedures.
/// Maps function names to their C# delegate implementations, supporting overloads.
/// </summary>
public class InternalFunctionRegistry
{
    private readonly Dictionary<string, List<(FunctionSignature Signature, Func<IReadOnlyList<Value>, Value> Implementation)>> _implementations = new();

    public virtual void Register(string name, FunctionSignature signature, Func<IReadOnlyList<Value>, Value> implementation)
    {
        if (!_implementations.ContainsKey(name))
            _implementations[name] = [];

        _implementations[name].Add((signature, implementation));
    }

    public bool TryGetImplementation(string name, FunctionSignature requestedSignature, out Func<IReadOnlyList<Value>, Value>? implementation, out FunctionSignature? registeredSignature)
    {
        implementation = null;
        registeredSignature = null;

        if (!_implementations.TryGetValue(name, out var overloads))
            return false;

        // Find matching overload by signature
        foreach (var (signature, impl) in overloads)
        {
            if (SignaturesMatch(signature, requestedSignature))
            {
                implementation = impl;
                registeredSignature = signature;
                return true;
            }
        }

        return false;
    }

    private bool SignaturesMatch(FunctionSignature registered, FunctionSignature requested)
    {
        if (registered.IsVarArgs)
        {
            // A varargs function requires at least (N - 1) arguments to match the fixed part
            int fixedCount = registered.InputTypes.Count - 1;
            if (requested.InputTypes.Count < fixedCount) return false;

            // Check fixed arguments
            for (int i = 0; i < fixedCount; i++)
            {
                if (!TypesEqual(registered.InputTypes[i], requested.InputTypes[i]))
                    return false;
            }

            // Check varargs
            var varArgType = registered.InputTypes[fixedCount];
            for (int i = fixedCount; i < requested.InputTypes.Count; i++)
            {
                if (!TypesEqual(varArgType, requested.InputTypes[i]))
                    return false;
            }

            return true;
        }
        else
        {
            // Non-varargs: count must match exactly
            if (registered.InputTypes.Count != requested.InputTypes.Count)
                return false;

            for (int i = 0; i < registered.InputTypes.Count; i++)
            {
                if (!TypesEqual(registered.InputTypes[i], requested.InputTypes[i]))
                    return false;
            }

            return true;
        }
    }

    private bool TypesEqual(FlowType registered, FlowType requested)
    {
        // Check if types are exactly equal
        if (registered.Equals(requested))
            return true;

        // VoidType mathematically represents 'Any' — but NOT a match against LazyType.
        // Lazy is not interchangeable with concrete types at the C# implementation level
        // (Lazy impls expect Thunks, strict impls expect concrete values). Excluding Lazy
        // here disambiguates the Lazy/strict `if` overloads (plan 12-05, TEST-03).
        if (registered is TypeSystem.PrimitiveTypes.VoidType && requested is not TypeSystem.PrimitiveTypes.LazyType)
            return true;
        if (requested is TypeSystem.PrimitiveTypes.VoidType && registered is not TypeSystem.PrimitiveTypes.LazyType)
            return true;

        // Special case: ArrayType(Void) matches any ArrayType
        if (registered is ArrayType rArray && requested is ArrayType reqArray)
        {
            return TypesEqual(rArray.ElementType, reqArray.ElementType);
        }

        return false;
    }

    public bool HasImplementation(string name) => _implementations.ContainsKey(name);

    /// <summary>
    /// Read-only enumerator over registered (name, signatures) pairs.
    /// Added for Phase 17 LSP BuiltInIndex (17-05). Does NOT expose the implementation
    /// delegates — only signatures are needed for completion/hover/signature-help.
    /// </summary>
    public IEnumerable<KeyValuePair<string, IReadOnlyList<FunctionSignature>>> EnumerateSignatures()
    {
        foreach (var kvp in _implementations)
        {
            var sigs = kvp.Value.Select(tuple => tuple.Signature).ToList();
            yield return new KeyValuePair<string, IReadOnlyList<FunctionSignature>>(kvp.Key, sigs);
        }
    }

    /// <summary>
    /// Replaces all implementations for a given function name with a single new one.
    /// Used by the editor to intercept built-in functions like renderSong/play.
    /// </summary>
    public void ReplaceAll(string name, FunctionSignature signature, Func<IReadOnlyList<Value>, Value> implementation)
    {
        _implementations[name] = new List<(FunctionSignature, Func<IReadOnlyList<Value>, Value>)>
        {
            (signature, implementation)
        };
    }
}
