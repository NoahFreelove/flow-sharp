using System;
using System.Collections.Generic;
using System.Numerics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26;

/// <summary>
/// Phase 26 Wave 0 (RED): pins Decision D-07 — `(neg)` ships as a 5-pack with
/// one overload per numeric type. Each Fact verifies sign-flip correctness AND
/// that the result type matches the input type (no widening).
///
/// Decisions referenced (locked in 26-CONTEXT.md):
///   D-07 — `(neg Int) → Int`, `(neg Long) → Long`, `(neg Float) → Float`,
///          `(neg Double) → Double`, `(neg Number) → Number`. Return type
///          matches input. No Sequence/Note overload (out of scope).
///
/// Pattern: registry-direct (no FlowEngine spin-up) per S-05.
/// </summary>
public class NegOverloadFacts
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
    public void NegInt_FlipsSign()
    {
        var registry = BuildRegistry();
        var result = Call(registry, "neg", [IntType.Instance], [Value.Int(42)]);
        Assert.IsType<IntType>(result.Type);
        Assert.Equal(-42, result.As<int>());
    }

    [Fact]
    public void NegLong_FlipsSign()
    {
        var registry = BuildRegistry();
        var result = Call(registry, "neg", [LongType.Instance], [Value.Long(42L)]);
        Assert.IsType<LongType>(result.Type);
        Assert.Equal(-42L, result.As<long>());
    }

    [Fact]
    public void NegFloat_FlipsSign()
    {
        var registry = BuildRegistry();
        var result = Call(registry, "neg", [FloatType.Instance], [Value.Float(3.14)]);
        Assert.IsType<FloatType>(result.Type);
        Assert.Equal(-3.14, result.As<double>(), 12);
    }

    [Fact]
    public void NegDouble_FlipsSign()
    {
        var registry = BuildRegistry();
        var result = Call(registry, "neg", [DoubleType.Instance], [Value.Double(3.14)]);
        Assert.IsType<DoubleType>(result.Type);
        Assert.Equal(-3.14, result.As<double>(), 12);
    }

    [Fact]
    public void NegNumber_FlipsSign()
    {
        var registry = BuildRegistry();
        var input = BigInteger.Parse("1000000000");
        var result = Call(registry, "neg", [NumberType.Instance], [Value.Number(input)]);
        Assert.IsType<NumberType>(result.Type);
        Assert.Equal(BigInteger.Parse("-1000000000"), result.As<BigInteger>());
    }
}
