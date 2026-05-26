using System;
using System.Collections.Generic;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using Xunit;

namespace FlowLang.Tests.Integration.Phase43;

/// <summary>
/// Phase 43 Plan 43-02 Task 1 — Wave 0 unit tests for <see cref="ModuleRegistry"/>.
///
/// Drives the registry directly (no FlowEngine for the registry-shape facts) so
/// the keying / lookup / dedup / per-context-isolation behavior is verifiable
/// without dragging in the lexer / parser / interpreter. The full wire-up
/// (ModuleLoader hook + ExpressionEvaluator dispatch) is covered by Plan 43-03.
///
/// Per CONTEXT D-02 (registry-first member dispatch) + D-05 (Register at use-time)
/// + D-06 (last-write-wins for duplicate module names; advisory is the
/// ModuleLoader caller's responsibility in Plan 43-03, not here).
///
/// Mirrors the unit-style cadence of
/// <see cref="FlowLang.Tests.Phase36.PrngRegistryTests"/> and the S5
/// <c>RenderingDiagnostics.ResetForTesting()</c> ceremony of
/// <see cref="FlowLang.Tests.Integration.Phase38.LiveBlockParserTests"/>.
/// </summary>
[Collection("FlowScripts")]
public class ModuleRegistryTests : IDisposable
{
    public ModuleRegistryTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    /// <summary>
    /// Builds a minimal exported-procs dict for tests. Uses the
    /// <see cref="FunctionOverload.Internal"/> path because it's the
    /// simpler factory shape (Value + FunctionSignature only; no
    /// ProcDeclaration AST tree required).
    /// </summary>
    private static Value StubFunction(string name)
    {
        var sig = new FunctionSignature(name, new List<FlowType>());
        var overload = FunctionOverload.Internal(name, sig, _ => Value.Void());
        return Value.Function(overload);
    }

    // ------------------------------------------------------------------
    // Test 1 — fresh registry is empty.
    // ------------------------------------------------------------------
    [Fact]
    public void FreshRegistryIsEmpty()
    {
        var reg = new ModuleRegistry();

        Assert.False(reg.Contains("anything"));
        Assert.False(reg.TryGetProc("anything", "any", out var v));
        Assert.Null(v);
    }

    // ------------------------------------------------------------------
    // Test 2 — Register + Contains round-trip.
    // ------------------------------------------------------------------
    [Fact]
    public void RegisterThenContainsReturnsTrue()
    {
        var reg = new ModuleRegistry();
        var procs = new Dictionary<string, Value>
        {
            ["sin"] = StubFunction("sin"),
        };

        reg.Register("math", procs);

        Assert.True(reg.Contains("math"));
        Assert.False(reg.Contains("not-registered"));
    }

    // ------------------------------------------------------------------
    // Test 3 — TryGetProc returns the registered Function Value.
    // ------------------------------------------------------------------
    [Fact]
    public void TryGetProcReturnsRegisteredFunctionValue()
    {
        var reg = new ModuleRegistry();
        var stub = StubFunction("sin");
        var procs = new Dictionary<string, Value> { ["sin"] = stub };
        reg.Register("math", procs);

        var hit = reg.TryGetProc("math", "sin", out var procValue);

        Assert.True(hit);
        Assert.NotNull(procValue);
        Assert.IsType<FlowLang.TypeSystem.PrimitiveTypes.FunctionType>(procValue!.Type);
        // Reference identity — registry returns the same Value reference, not a copy.
        Assert.Same(stub, procValue);
    }

    // ------------------------------------------------------------------
    // Test 4 — TryGetProc misses cleanly.
    // ------------------------------------------------------------------
    [Fact]
    public void TryGetProcMissesReturnFalseAndNullValue()
    {
        var reg = new ModuleRegistry();
        var procs = new Dictionary<string, Value> { ["sin"] = StubFunction("sin") };
        reg.Register("math", procs);

        // Known module, unknown proc.
        Assert.False(reg.TryGetProc("math", "nope", out var v1));
        Assert.Null(v1);

        // Unknown module entirely.
        Assert.False(reg.TryGetProc("unknown", "anything", out var v2));
        Assert.Null(v2);
    }

    // ------------------------------------------------------------------
    // Test 5 — last-write-wins on duplicate Register (D-06).
    // ------------------------------------------------------------------
    [Fact]
    public void DuplicateRegisterKeepsLastWriteWins()
    {
        var reg = new ModuleRegistry();
        var first = StubFunction("sin");
        var second = StubFunction("sin");

        reg.Register("math", new Dictionary<string, Value> { ["sin"] = first });
        reg.Register("math", new Dictionary<string, Value> { ["sin"] = second });

        Assert.True(reg.TryGetProc("math", "sin", out var procValue));
        Assert.Same(second, procValue);
        Assert.NotSame(first, procValue);

        // The second Register also drops the first's proc set entirely — re-registration
        // with a NEW dict that lacks the original key should miss.
        reg.Register("math", new Dictionary<string, Value> { ["cos"] = StubFunction("cos") });
        Assert.False(reg.TryGetProc("math", "sin", out var v));
        Assert.Null(v);
        Assert.True(reg.TryGetProc("math", "cos", out _));
    }

    // ------------------------------------------------------------------
    // Test 6 — per-ExecutionContext isolation (RESEARCH A1).
    // ------------------------------------------------------------------
    [Fact]
    public void DistinctExecutionContextsExposeDistinctRegistries()
    {
        using var engineA = new FlowEngine();
        using var engineB = new FlowEngine();

        Assert.NotNull(engineA.Context.ModuleRegistry);
        Assert.NotNull(engineB.Context.ModuleRegistry);
        Assert.NotSame(engineA.Context.ModuleRegistry, engineB.Context.ModuleRegistry);

        // Registering on A must not be visible from B (the per-context-isolation
        // contract — Phase 35 TEST-02 hermetic snapshot relies on this).
        engineA.Context.ModuleRegistry.Register(
            "math",
            new Dictionary<string, Value> { ["sin"] = StubFunction("sin") });

        Assert.True(engineA.Context.ModuleRegistry.Contains("math"));
        Assert.False(engineB.Context.ModuleRegistry.Contains("math"));
    }
}
