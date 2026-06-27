using System;
using System.Collections.Generic;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_1;

/// <summary>
/// Phase 26.1 Wave 1 (GREEN): pins SYM-01 — Symbol primitive type with
/// interning (pointer-equality) and strict separation from String.
///
/// Pattern: registry-direct (S4 from PATTERNS.md). Symbol interning is per-context,
/// so each fact builds a fresh <see cref="ExecutionContext"/> alongside the registry.
/// </summary>
public class SymbolFacts
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
    public void Interning_SamePointer()
    {
        var registry = BuildRegistry();
        var ctx = new FlowLang.Runtime.ExecutionContext(new ErrorReporter(), registry);
        var a = Value.Symbol("foo", ctx);
        var b = Value.Symbol("foo", ctx);
        Assert.Same(a, b); // reference-equal — pointer interning per SYM-01
    }

    [Fact]
    public void StrictSeparation_SymbolNeqString()
    {
        // Per CONTEXT § Symbol/String charitable equivalence: `(equals #foo "foo")` MUST be false.
        // The `equals` builtin is registered with [VoidType, VoidType] (Void wildcard).
        // Phase 44 Plan 44-09 Task 2 moved `equals` from RegisterStdLib to
        // RegisterContextDependentFunctions so the impl can read ctx.CallerStrictMode for
        // D-11 set-theoretic strict equality — the registration now requires a live
        // ExecutionContext at registration time.
        var registry = BuildRegistry();
        var ctx = new FlowLang.Runtime.ExecutionContext(new ErrorReporter(), registry);
        BuiltInFunctions.RegisterContextDependentFunctions(registry, ctx);
        var sym = Value.Symbol("foo", ctx);
        var str = Value.String("foo");
        var result = Call(registry, "equals",
            [VoidType.Instance, VoidType.Instance],
            [sym, str]);
        Assert.False(result.As<bool>(), "Symbol vs String must NOT be equal — strict separation per CONTEXT");
    }

    [Fact]
    public void EqualsBuiltinReturnsTrueForSameSymbol()
    {
        var registry = BuildRegistry();
        var ctx = new FlowLang.Runtime.ExecutionContext(new ErrorReporter(), registry);
        BuiltInFunctions.RegisterContextDependentFunctions(registry, ctx);
        var a = Value.Symbol("foo", ctx);
        var b = Value.Symbol("foo", ctx);
        var result = Call(registry, "equals",
            [VoidType.Instance, VoidType.Instance],
            [a, b]);
        Assert.True(result.As<bool>());
    }
}
