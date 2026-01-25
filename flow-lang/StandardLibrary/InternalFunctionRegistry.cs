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

    public void Register(string name, FunctionSignature signature, Func<IReadOnlyList<Value>, Value> implementation)
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
        // Special case: if registered is varargs with Void type,
        // it accepts any requested signature (even non-varargs)
        if (registered.IsVarArgs &&
            registered.InputTypes.Count == 1 &&
            registered.InputTypes[0] is TypeSystem.PrimitiveTypes.VoidType)
        {
            // This handles cases like list(Void...: items) which accepts any arguments
            return requested.InputTypes.Count == 1 &&
                   requested.InputTypes[0] is TypeSystem.PrimitiveTypes.VoidType;
        }

        // Check varargs flag - both must agree
        if (registered.IsVarArgs != requested.IsVarArgs)
            return false;

        // Check parameter count
        if (registered.InputTypes.Count != requested.InputTypes.Count)
            return false;

        // Check each parameter type
        for (int i = 0; i < registered.InputTypes.Count; i++)
        {
            if (!TypesEqual(registered.InputTypes[i], requested.InputTypes[i]))
                return false;
        }

        return true;
    }

    private bool TypesEqual(FlowType a, FlowType b)
    {
        // Check if types are exactly equal
        if (a.Equals(b))
            return true;

        // Special case: ArrayType(Void) matches any ArrayType
        if (a is ArrayType aArray && b is ArrayType bArray)
        {
            // If either element type is Void, consider them compatible
            if (aArray.ElementType is TypeSystem.PrimitiveTypes.VoidType ||
                bArray.ElementType is TypeSystem.PrimitiveTypes.VoidType)
            {
                return true;
            }

            return TypesEqual(aArray.ElementType, bArray.ElementType);
        }

        // Check if they're the same type class
        return a.GetType() == b.GetType();
    }

    public bool HasImplementation(string name) => _implementations.ContainsKey(name);
}
