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
/// Phase 26 Wave 0 (RED): pins Decision D-05 same-type fast paths for the Long
/// and Number arithmetic overloads. Eight Facts cover (add/sub/mul/div) ×
/// (Long/Long, Number/Number). Wave 1 (plan 26-02) registers the implementations
/// and turns these Facts GREEN.
///
/// Decisions referenced (locked in 26-CONTEXT.md):
///   D-05 — five same-type fast paths per op (Int/Long/Float/Double/Number); these
///          Facts pin the new Long and Number entries that did not exist pre-Phase 26.
///   D-06 — same-type result narrows to input type (no surprise widening).
///
/// Pattern: registry-direct (no FlowEngine spin-up) per S-05; analog
/// flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs:41-98.
/// </summary>
public class NewOverloadFacts
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
    public void AddLong_SameType_ReturnsLong()
    {
        var registry = BuildRegistry();
        var result = Call(registry, "add",
            [LongType.Instance, LongType.Instance],
            [Value.Long(5L), Value.Long(6L)]);
        Assert.IsType<LongType>(result.Type);
        Assert.Equal(11L, result.As<long>());
    }

    [Fact]
    public void SubLong_SameType_ReturnsLong()
    {
        var registry = BuildRegistry();
        var result = Call(registry, "sub",
            [LongType.Instance, LongType.Instance],
            [Value.Long(10L), Value.Long(3L)]);
        Assert.IsType<LongType>(result.Type);
        Assert.Equal(7L, result.As<long>());
    }

    [Fact]
    public void MulLong_SameType_ReturnsLong()
    {
        var registry = BuildRegistry();
        var result = Call(registry, "mul",
            [LongType.Instance, LongType.Instance],
            [Value.Long(4L), Value.Long(5L)]);
        Assert.IsType<LongType>(result.Type);
        Assert.Equal(20L, result.As<long>());
    }

    [Fact]
    public void DivLong_SameType_ReturnsLong()
    {
        var registry = BuildRegistry();
        var result = Call(registry, "div",
            [LongType.Instance, LongType.Instance],
            [Value.Long(10L), Value.Long(2L)]);
        Assert.IsType<LongType>(result.Type);
        Assert.Equal(5L, result.As<long>());
    }

    [Fact]
    public void AddNumber_SameType_ReturnsNumber()
    {
        var registry = BuildRegistry();
        var a = BigInteger.Parse("1000000000000");
        var b = BigInteger.Parse("2000000000000");
        var result = Call(registry, "add",
            [NumberType.Instance, NumberType.Instance],
            [Value.Number(a), Value.Number(b)]);
        Assert.IsType<NumberType>(result.Type);
        Assert.Equal(BigInteger.Parse("3000000000000"), result.As<BigInteger>());
    }

    [Fact]
    public void SubNumber_SameType_ReturnsNumber()
    {
        var registry = BuildRegistry();
        var a = BigInteger.Parse("5000000000000");
        var b = BigInteger.Parse("2000000000000");
        var result = Call(registry, "sub",
            [NumberType.Instance, NumberType.Instance],
            [Value.Number(a), Value.Number(b)]);
        Assert.IsType<NumberType>(result.Type);
        Assert.Equal(BigInteger.Parse("3000000000000"), result.As<BigInteger>());
    }

    [Fact]
    public void MulNumber_SameType_ReturnsNumber()
    {
        var registry = BuildRegistry();
        var a = BigInteger.Parse("1000000000");
        var b = BigInteger.Parse("3");
        var result = Call(registry, "mul",
            [NumberType.Instance, NumberType.Instance],
            [Value.Number(a), Value.Number(b)]);
        Assert.IsType<NumberType>(result.Type);
        Assert.Equal(BigInteger.Parse("3000000000"), result.As<BigInteger>());
    }

    [Fact]
    public void DivNumber_SameType_ReturnsNumber()
    {
        var registry = BuildRegistry();
        var a = BigInteger.Parse("9000000000000");
        var b = BigInteger.Parse("3");
        var result = Call(registry, "div",
            [NumberType.Instance, NumberType.Instance],
            [Value.Number(a), Value.Number(b)]);
        Assert.IsType<NumberType>(result.Type);
        Assert.Equal(BigInteger.Parse("3000000000000"), result.As<BigInteger>());
    }
}
