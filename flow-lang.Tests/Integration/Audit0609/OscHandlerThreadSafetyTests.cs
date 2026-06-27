using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Network;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit 2026-06-09 §5.3 — the OSC receive loop must NOT invoke composer
/// handler lambdas on the background ThreadPool thread against the shared,
/// non-thread-safe Interpreter/ExecutionContext. The fix queues user-proc
/// handler invocations and drains them on the foreground evaluator thread
/// (osc* call sites + the new <c>(oscPump)</c> builtin).
///
/// <para>These tests use a REAL <see cref="FlowEngine"/> (so the
/// <c>context.Invoker</c> path — not the <see cref="OscFunctions.HandlerInvokeOverride"/>
/// seam — is exercised) and feed packets through
/// <see cref="OscFunctions.DispatchPacketForTesting"/> from a background
/// thread, modeling the production listener loop without binding a socket.</para>
/// </summary>
[Collection("FlowScripts")]
public class OscHandlerThreadSafetyTests : IDisposable
{
    public OscHandlerThreadSafetyTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        OscFunctions.ResetForTesting();
        OscFunctions.HandlerInvokeOverride = null; // exercise the REAL invoker path
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        OscFunctions.ResetForTesting();
        OscFunctions.HandlerInvokeOverride = null;
    }

    private static FunctionOverload GetUserProc(FlowEngine engine, string name)
        => engine.Context.GlobalFrame.GetFunctionOverloads(name)[0];

    /// <summary>
    /// A real user-proc handler is QUEUED, not run, when a packet is dispatched
    /// on a non-foreground thread. The global it mutates stays untouched until
    /// the foreground explicitly drains via DrainPendingHandlers.
    /// </summary>
    [Fact]
    public void UserProcHandler_IsQueued_NotRunOnDispatchThread()
    {
        const string script = @"use ""@osc""
Int hits = 0
proc onHit (Int: v)
    hits = (add hits 1)
    (print)
end proc
";
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(script, "<audit-5.3-queue>");
        Assert.True(ok, stderr);

        var engine = runner.GetEngine();
        var handler = GetUserProc(engine, "onHit");
        var ctx = engine.Context;

        var msg = new Rug.Osc.OscMessage("/hit", 7);

        // Dispatch on a BACKGROUND thread (models the listener loop). With the
        // fix this only ENQUEUES — the proc body must not run here.
        var bg = Task.Run(() =>
            OscFunctions.DispatchPacketForTesting(msg, "/hit", handler, ctx));
        bg.Wait(TimeSpan.FromSeconds(2));

        Assert.Equal(0, engine.Context.GlobalFrame.GetVariable("hits").As<int>());

        // Drain on the foreground (test) thread — NOW it runs.
        int drained = OscFunctions.DrainPendingHandlers();
        Assert.Equal(1, drained);
        Assert.Equal(1, engine.Context.GlobalFrame.GetVariable("hits").As<int>());
    }

    /// <summary>
    /// Stress: two background threads flood the OSC handler queue (modeling the
    /// production receive loop, which ONLY enqueues) WHILE the foreground thread
    /// drains them — every drained invocation runs the real <c>onHit</c> proc,
    /// pushing and popping a <see cref="StackFrame"/> on the shared interpreter
    /// call stack and writing the global <c>hits</c>. If the dispatch path ran
    /// the proc on a background thread (the pre-fix bug), this interleaving would
    /// corrupt the plain <c>Stack&lt;StackFrame&gt;</c> (an
    /// InvalidOperationException / torn count) or lose increments to a racing
    /// global write. The fix serializes ALL proc invocations onto the foreground
    /// thread, so the run completes without throwing and <c>hits</c> equals the
    /// exact number of packets enqueued.
    /// </summary>
    [Fact]
    public void ConcurrentForegroundEvalAndHandlerDispatch_DoesNotCorruptFrameStack()
    {
        const string script = @"use ""@osc""
Int hits = 0
proc onHit (Int: v)
    hits = (add hits 1)
    (print)
end proc
";
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(script, "<audit-5.3-stress>");
        Assert.True(ok, stderr);

        var engine = runner.GetEngine();
        var handler = GetUserProc(engine, "onHit");
        var ctx = engine.Context;

        const int producers = 2;
        const int perProducer = 500;
        const int total = producers * perProducer;
        int enqueued = 0;
        var startGate = new ManualResetEventSlim(false);

        // Background producers: model the production receive loop, which ONLY ever
        // enqueues a user-proc handler invocation (never runs the proc, never
        // reads live interpreter state). EnqueueHandlerForTesting does exactly the
        // thread-safe ConcurrentQueue.Enqueue the real InvokeHandler does.
        var handlerArgs = new List<Value> { Value.Int(0) };
        var tasks = new List<Task>(producers);
        for (int p = 0; p < producers; p++)
        {
            tasks.Add(Task.Run(() =>
            {
                startGate.Wait();
                for (int i = 0; i < perProducer; i++)
                {
                    OscFunctions.EnqueueHandlerForTesting(handler, handlerArgs, ctx);
                    Interlocked.Increment(ref enqueued);
                }
            }));
        }

        // Foreground: drain continuously on THIS thread while producers enqueue.
        // Each drain runs onHit, exercising real frame push/pop on the shared call
        // stack concurrently with the producers' enqueues. No exception may escape.
        startGate.Set();
        var all = Task.WhenAll(tasks);
        int totalDrained = 0;
        while (!all.IsCompleted || OscFunctions.PendingHandlerCountForTesting > 0)
        {
            totalDrained += OscFunctions.DrainPendingHandlers();
            Thread.Yield();
        }
        all.Wait(TimeSpan.FromSeconds(15));
        totalDrained += OscFunctions.DrainPendingHandlers(); // final sweep

        Assert.Equal(total, enqueued);
        Assert.Equal(total, totalDrained);
        Assert.Equal(total, engine.Context.GlobalFrame.GetVariable("hits").As<int>());
    }
}
