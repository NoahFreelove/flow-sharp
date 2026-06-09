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

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit 2026-06-09 §5.10 — a future-timetag OSC bundle scheduled via
/// <c>Task.Delay(...).ContinueWith(...)</c> must NOT invoke the composer's
/// handler after the listener was stopped. The fix threads the listener's
/// CancellationToken into the Delay AND re-checks it in the continuation;
/// <c>DispatchPacket</c> also short-circuits at entry when the token is already
/// cancelled. Stop semantics win.
/// </summary>
[Collection("FlowScripts")]
public class OscFutureTimetagCancelTests : IDisposable
{
    private int _fireCount;

    public OscFutureTimetagCancelTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        OscFunctions.ResetForTesting();
        OscFunctions.HandlerInvokeOverride = (_, _) => Interlocked.Increment(ref _fireCount);
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
        var sig = new FunctionSignature("test_handler", new FlowType[] { VoidType.Instance }, IsVarArgs: true);
        return FunctionOverload.Internal("test_handler", sig, _ => Value.Void());
    }

    private static FlowLang.Runtime.ExecutionContext MakeContext()
    {
        var reporter = new ErrorReporter();
        var registry = new InternalFunctionRegistry();
        return new FlowLang.Runtime.ExecutionContext(reporter, registry);
    }

    /// <summary>A bundle ~250 ms in the future, scheduled with a listener token
    /// that is then cancelled (modeling oscStop), must NOT fire its handler.</summary>
    [Fact]
    public void FutureTimetagBundle_AfterStop_DoesNotInvokeHandler()
    {
        var ctx = MakeContext();
        var handler = StubHandler();
        using var cts = new CancellationTokenSource();

        var future = Rug.Osc.OscTimeTag.FromDataTime(DateTime.UtcNow.AddMilliseconds(250));
        var bundle = new Rug.Osc.OscBundle(future, new Rug.Osc.OscMessage("/late", 1));

        // Schedule the future dispatch under the listener token.
        OscFunctions.DispatchPacketForTesting(bundle, "/late", handler, ctx, cts.Token);

        // Composer stops the listener (oscStop cancels the Cts) BEFORE the
        // timetag elapses.
        cts.Cancel();

        // Wait well past the future timetag — the continuation must be dropped.
        Thread.Sleep(600);
        Assert.Equal(0, Volatile.Read(ref _fireCount));
    }

    /// <summary>Control: the SAME future-timetag bundle, left un-cancelled, DOES
    /// fire after the delay — proves the timetag-honor path still works and the
    /// §5.10 fix only suppresses the cancelled case.</summary>
    [Fact]
    public void FutureTimetagBundle_NotStopped_StillFires()
    {
        var ctx = MakeContext();
        var handler = StubHandler();
        using var cts = new CancellationTokenSource();

        var future = Rug.Osc.OscTimeTag.FromDataTime(DateTime.UtcNow.AddMilliseconds(150));
        var bundle = new Rug.Osc.OscBundle(future, new Rug.Osc.OscMessage("/soon", 1));

        OscFunctions.DispatchPacketForTesting(bundle, "/soon", handler, ctx, cts.Token);

        // Do NOT cancel. Poll up to 2 s for the deferred fire.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (Volatile.Read(ref _fireCount) == 0 && DateTime.UtcNow < deadline)
            Thread.Sleep(20);

        Assert.Equal(1, Volatile.Read(ref _fireCount));
    }

    /// <summary>An already-cancelled token short-circuits dispatch at entry —
    /// even a plain immediate message is dropped for a stopped handle.</summary>
    [Fact]
    public void AlreadyCancelledToken_DropsImmediateMessage()
    {
        var ctx = MakeContext();
        var handler = StubHandler();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var msg = new Rug.Osc.OscMessage("/now", 1);
        OscFunctions.DispatchPacketForTesting(msg, "/now", handler, ctx, cts.Token);

        Assert.Equal(0, Volatile.Read(ref _fireCount));
    }
}
