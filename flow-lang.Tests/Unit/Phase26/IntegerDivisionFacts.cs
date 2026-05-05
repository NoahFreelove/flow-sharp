using System;
using System.Collections.Generic;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26;

/// <summary>
/// Phase 26 Wave 0 (RED): pins Decision D-08 — the integer-division asymmetry.
/// `(div Int Int)` auto-promotes to Double (returns 0.5 for `(div 1 2)`); the
/// niche case `(idiv Int Int)` returns Int with truncation (returns 0).
///
/// Decisions referenced (locked in 26-CONTEXT.md):
///   D-08 — `(div Int Int) → Double` auto-promotion (foot-gun resolved); ship
///          `(idiv Int Int) → Int` for explicit integer division. All other
///          same-type div overloads return their input type unchanged.
///
/// Pattern: registry-direct (no FlowEngine spin-up) per S-05.
/// </summary>
public class IntegerDivisionFacts
{
    private static InternalFunctionRegistry BuildRegistry()
    {
        var registry = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterAllImplementations(registry);
        return registry;
    }

    private static Value Call(InternalFunctionRegistry registry, string name,
                              IReadOnlyList<FlowType> argTypes, IReadOnlyList<Value> args)
    {
        var sig = new FunctionSignature(name, argTypes);
        if (!registry.TryGetImplementation(name, sig, out var fn, out _) || fn is null)
            throw new InvalidOperationException($"{name} overload {sig} not registered");
        return fn(args);
    }

    [Fact]
    public void DivIntInt_AutoPromotesToDouble()
    {
        // D-08: (div 1 2) returns Double 0.5, NOT Int 0.
        var registry = BuildRegistry();
        var result = Call(registry, "div",
            [IntType.Instance, IntType.Instance],
            [Value.Int(1), Value.Int(2)]);
        Assert.IsType<DoubleType>(result.Type);
        Assert.Equal(0.5, result.As<double>(), 12);
    }

    [Fact]
    public void IDivIntInt_TruncatesToInt()
    {
        // D-08: (idiv 1 2) returns Int 0 (truncating integer division).
        var registry = BuildRegistry();
        var result = Call(registry, "idiv",
            [IntType.Instance, IntType.Instance],
            [Value.Int(1), Value.Int(2)]);
        Assert.IsType<IntType>(result.Type);
        Assert.Equal(0, result.As<int>());
    }
}
