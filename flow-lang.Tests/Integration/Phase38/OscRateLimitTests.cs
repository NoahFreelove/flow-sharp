using System;
using System.Collections.Generic;
using System.Threading;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Network;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-06 OSC-01 — per-path drop-newest sample-and-hold rate
/// limit at 200 Hz per D-38-14 + RESEARCH §M. Exercises
/// <see cref="OscFunctions.DispatchPacketForTesting"/> directly with a
/// captured-list HandlerInvokeOverride so CI doesn't depend on real UDP
/// sockets or sleep timing.
/// </summary>
[Collection("FlowScripts")]
public class OscRateLimitTests : IDisposable
{
    private readonly List<int> _invocations = new();

    public OscRateLimitTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        OscFunctions.ResetForTesting();
        OscFunctions.HandlerInvokeOverride = (_, _) =>
        {
            lock (_invocations) _invocations.Add(_invocations.Count);
        };
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        OscFunctions.ResetForTesting();
        OscFunctions.HandlerInvokeOverride = null;
    }

    private static FunctionOverload StubHandler()
    {
        // No-op signature with a single Void wildcard arg + no-op
        // implementation. The HandlerInvokeOverride takes over before the
        // implementation runs.
        var sig = new FunctionSignature("test_handler",
            new FlowType[] { VoidType.Instance },
            ParameterNames: new[] { "x" });
        return FunctionOverload.Internal("test_handler", sig, _ => Value.Void());
    }

    private static FlowLang.Runtime.ExecutionContext MakeContext()
    {
        var reporter = new FlowLang.Diagnostics.ErrorReporter();
        var registry = new InternalFunctionRegistry();
        return new FlowLang.Runtime.ExecutionContext(reporter, registry);
    }

    /// <summary>
    /// SamePath_DropNewestWithinWindow: dispatch two OscMessage handlers
    /// back-to-back at the same path; assert handler invoked exactly once
    /// per D-38-14 sample-and-hold (the second is dropped because the
    /// first set the lastFireTime).
    /// </summary>
    [Fact]
    public void SamePath_DropNewestWithinWindow()
    {
        var handler = StubHandler();
        var ctx = MakeContext();
        var msg1 = new Rug.Osc.OscMessage("/fader/1", 0.1f);
        var msg2 = new Rug.Osc.OscMessage("/fader/1", 0.2f);

        OscFunctions.DispatchPacketForTesting(msg1, "/fader/1", handler, ctx);
        OscFunctions.DispatchPacketForTesting(msg2, "/fader/1", handler, ctx);

        Assert.Single(_invocations);
    }

    /// <summary>
    /// DifferentPaths_BothFire: dispatch two OscMessage handlers at
    /// different paths within the same window; assert both fire (gate is
    /// per-path).
    /// </summary>
    [Fact]
    public void DifferentPaths_BothFire()
    {
        var handler = StubHandler();
        var ctx = MakeContext();
        var msgA = new Rug.Osc.OscMessage("/fader/1", 0.1f);
        var msgB = new Rug.Osc.OscMessage("/fader/2", 0.2f);

        OscFunctions.DispatchPacketForTesting(msgA, "/fader/1", handler, ctx);
        OscFunctions.DispatchPacketForTesting(msgB, "/fader/2", handler, ctx);

        Assert.Equal(2, _invocations.Count);
    }

    /// <summary>
    /// SamePath_AfterWindow_BothFire: dispatch with a sleep of more than
    /// the 5ms window between calls; assert both fire (above the rate
    /// limit window). Uses 50ms sleep to be robust against scheduler
    /// jitter while still well below typical OSC controller rates.
    /// </summary>
    [Fact]
    public void SamePath_AfterWindow_BothFire()
    {
        var handler = StubHandler();
        var ctx = MakeContext();
        var msg1 = new Rug.Osc.OscMessage("/fader/1", 0.1f);
        var msg2 = new Rug.Osc.OscMessage("/fader/1", 0.2f);

        OscFunctions.DispatchPacketForTesting(msg1, "/fader/1", handler, ctx);
        Thread.Sleep(50);
        OscFunctions.DispatchPacketForTesting(msg2, "/fader/1", handler, ctx);

        Assert.Equal(2, _invocations.Count);
    }

    /// <summary>
    /// PathMismatch_NoFire: dispatch an OscMessage whose address differs
    /// from the listener's targetPath; assert handler not invoked
    /// (literal-path match per D-38-16 v1.5 scope — wildcards deferred to
    /// v1.6).
    /// </summary>
    [Fact]
    public void PathMismatch_NoFire()
    {
        var handler = StubHandler();
        var ctx = MakeContext();
        var msg = new Rug.Osc.OscMessage("/other/path", 0.1f);

        OscFunctions.DispatchPacketForTesting(msg, "/fader/1", handler, ctx);

        Assert.Empty(_invocations);
    }
}
