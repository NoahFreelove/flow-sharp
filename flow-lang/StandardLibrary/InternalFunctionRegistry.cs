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

        // Phase 44 Plan 44-08 — TWO-PASS lookup: prefer EXACT signature match
        // before falling back to wildcard / Void-cross matches. Required when
        // the same function name has both a typed overload (e.g.
        // `print(String)`) AND a Void-wildcard overload
        // (e.g. `print(Void)`) — without the prefer-exact pass, the surface
        // declaration `internal proc print (Void: s)` would bind to the FIRST
        // registered impl (`StdLib.Print`) because `Void` matches any
        // registered type via `TypesEqual` line 95-98. Pass 1 catches the
        // intended `print(Void) → StdLib.PrintAny` binding before the wildcard
        // fallback fires. Backwards-compatible: the existing single-impl
        // (or single-wildcard-impl) cases still resolve identically since
        // Pass 1 either matches (good) or finds nothing, falling through to
        // Pass 2.
        // First pass — exact signature equality on every InputTypes slot
        // (no Void-wildcard cross-matching).
        foreach (var (signature, impl) in overloads)
        {
            if (SignaturesMatchExactly(signature, requestedSignature))
            {
                implementation = impl;
                registeredSignature = signature;
                return true;
            }
        }

        // Second pass — original compatibility-based match (Void wildcards
        // honored on either side).
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

    /// <summary>
    /// Phase 44 Plan 44-08 — strict per-slot equality check used by Pass 1
    /// of <see cref="TryGetImplementation"/>. Does NOT treat <c>Void</c> as a
    /// wildcard — both the registered and requested signature must match
    /// type-for-type. Mirrors <see cref="SignaturesMatch"/>'s arity / varargs
    /// handling so the prefer-exact pass and the wildcard-fallback pass have
    /// identical structural shape, only the per-slot predicate differs.
    /// </summary>
    private bool SignaturesMatchExactly(FunctionSignature registered, FunctionSignature requested)
    {
        if (registered.IsVarArgs)
        {
            int fixedCount = registered.InputTypes.Count - 1;
            if (requested.InputTypes.Count < fixedCount) return false;
            for (int i = 0; i < fixedCount; i++)
            {
                if (!registered.InputTypes[i].Equals(requested.InputTypes[i]))
                    return false;
            }
            var varArgType = registered.InputTypes[fixedCount];
            for (int i = fixedCount; i < requested.InputTypes.Count; i++)
            {
                if (!varArgType.Equals(requested.InputTypes[i]))
                    return false;
            }
            return true;
        }
        if (registered.InputTypes.Count != requested.InputTypes.Count)
            return false;
        for (int i = 0; i < registered.InputTypes.Count; i++)
        {
            if (!registered.InputTypes[i].Equals(requested.InputTypes[i]))
                return false;
        }
        return true;
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

        // Phase 33 Plan 33-05 — DictType wildcard symmetric with the ArrayType
        // case above. The dict ops (`get`/`set`/etc.) registered in
        // BuiltInFunctions.cs:944-957 use a `DictType(Void, Void)` wildcard, and
        // Plan 33-05's `__enableSfzModule(Dict<Symbol, String>)` follows the
        // same convention. Without this recursive wildcard, a concrete-typed
        // declaration like `internal proc __enableSfzModule (Dict<Symbol, String>: instruments)`
        // fails to bind even though `DictType(Void, Void).IsCompatibleWith(...)`
        // returns true at the OverloadResolver layer — the SignaturesMatch /
        // TypesEqual layer has a stricter equality semantics that needs the
        // same wildcard handling.
        if (registered is FlowLang.TypeSystem.SpecialTypes.DictType rDict
            && requested is FlowLang.TypeSystem.SpecialTypes.DictType reqDict)
        {
            return TypesEqual(rDict.KeyType, reqDict.KeyType)
                && TypesEqual(rDict.ValueType, reqDict.ValueType);
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
