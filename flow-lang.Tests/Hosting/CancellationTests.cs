using System.Diagnostics;
using FlowLang.Core;
using Xunit;

namespace FlowLang.Tests.Hosting;

/// <summary>
/// Phase 2 gate: evaluations stop at cooperative checkpoints when cancelled or out of
/// time, the engine stays usable, and resources a script opened are released.
/// </summary>
[Collection("FlowScripts")]
public class CancellationTests
{
    // Unbounded work that still passes checkpoints: nested loops at the host ceiling.
    private const string Endless = """
        use "@std"
        (setMaxIterations 1000000)
        Int total = 0
        while true {
            for Int i in (range 0 1000) {
                total = (add total 1)
            }
        }
        """;

    private static FlowEngine Quiet() =>
        new(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });

    [Fact]
    public void TimeLimitStopsARunawayLoopAndTheEngineStaysUsable()
    {
        using var engine = Quiet();
        var watch = Stopwatch.StartNew();
        var result = engine.Evaluate(Endless, "endless.flow", new EvaluationOptions { TimeLimit = TimeSpan.FromMilliseconds(200) });
        watch.Stop();

        Assert.Equal(EvaluationOutcome.TimedOut, result.Outcome);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"took {watch.Elapsed}");
        Assert.Contains(result.Errors, e => e.Message.Contains("time limit of 0.2s"));

        var next = engine.Evaluate("use \"@std\"\n(add 1 2)");
        Assert.Equal(EvaluationOutcome.Succeeded, next.Outcome);
        Assert.Equal(3, next.LastValue!.As<int>());
    }

    [Fact]
    public void HostCancellationStopsEvaluationFromAnotherThread()
    {
        using var engine = Quiet();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(150);
        var watch = Stopwatch.StartNew();
        var result = engine.Evaluate(Endless, "endless.flow", new EvaluationOptions { Cancellation = cts.Token });
        watch.Stop();

        Assert.Equal(EvaluationOutcome.Cancelled, result.Outcome);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"took {watch.Elapsed}");
    }

    [Fact]
    public void CancellationReachesRecursionAndRendering()
    {
        using var engine = Quiet();
        var recursion = engine.Evaluate("""
            use "@std"
            proc spin (Int: n)
                (spin (add n 1))
                (spin (add n 1))
            end proc
            (spin 0)
            """, "spin.flow", new EvaluationOptions { TimeLimit = TimeSpan.FromMilliseconds(200) });
        Assert.Equal(EvaluationOutcome.TimedOut, recursion.Outcome);

        // Sustained rendering work: a finite repeat-heavy render may finish before
        // the deadline after assembly optimizations. Keep rendering small buffers
        // until cancellation; bounded-copy checkpoints have a separate unit test.
        var watch = Stopwatch.StartNew();
        var render = engine.Evaluate("""
            use "@std"
            use "@audio"
            section verse { Sequence piano = | C4 E4 G4 C5 | C4 E4 G4 C5 | C4 E4 G4 C5 | C4 E4 G4 C5 | }
            Song s = [verse]
            while true {
                Buffer b = (renderSong s "piano")
            }
            """, "long.flow", new EvaluationOptions { TimeLimit = TimeSpan.FromMilliseconds(300) });
        watch.Stop();
        Assert.Equal(EvaluationOutcome.TimedOut, render.Outcome);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"render took {watch.Elapsed}");
    }

    [Fact]
    public void CancelledLazyValueIsNotCachedAsCancelled()
    {
        using var engine = Quiet();
        // The thunk itself runs until a global flag flips, so the time limit fires
        // while it is being forced.
        var first = engine.Evaluate("""
            use "@std"
            Bool stop = false
            proc work ()
                (setMaxIterations 1000000)
                while (not stop) {
                    for Int i in (range 0 1000) { (Nothing) }
                }
                "done"
            end proc
            Lazy slow = lazy ((work))
            (eval slow)
            """, "lazy.flow", new EvaluationOptions { TimeLimit = TimeSpan.FromMilliseconds(150) });
        Assert.Equal(EvaluationOutcome.TimedOut, first.Outcome);

        // Forcing it again evaluates afresh instead of rethrowing the old timeout.
        var second = engine.Evaluate("stop = true\n(eval slow)", "again.flow",
            new EvaluationOptions { TimeLimit = TimeSpan.FromSeconds(10) });
        Assert.Equal(EvaluationOutcome.Succeeded, second.Outcome);
        Assert.Equal("done", second.LastValue!.As<string>());
    }

    [Fact]
    public void ScriptsCannotRaiseTheIterationLimitAboveTheHostCeiling()
    {
        var output = new StringWriter();
        var diagnostics = new StringWriter();
        using var engine = new FlowEngine(new EngineOptions
        {
            Output = output, Diagnostics = diagnostics, MaxIterationsCeiling = 50,
        });
        engine.Execute("""
            use "@std"
            (setMaxIterations 100000)
            Int n = 0
            while true { n = (add n 1) }
            (print (str n))
            """);
        Assert.Contains("[budget] setMaxIterations 100000 exceeds the host limit of 50; using 50", diagnostics.ToString());
        Assert.Equal("50", output.ToString().Trim());
    }

    [Fact]
    public void DisposingTheEngineReleasesTrackedResources()
    {
        var engine = Quiet();
        int released = 0;
        engine.Session.Track(() => released++);
        Assert.Equal(1, engine.Session.TrackedResourceCount);
        engine.Dispose();
        Assert.Equal(1, released);
        Assert.Throws<ObjectDisposedException>(() => engine.Evaluate("1"));
    }

    [FlowLang.Tests.Helpers.FlowTargetFact("Desktop")]
    public void DisposingTheEngineStopsScriptOpenedOscListeners()
    {
        int port;
        using (var probe = new System.Net.Sockets.UdpClient(0, System.Net.Sockets.AddressFamily.InterNetwork))
            port = ((System.Net.IPEndPoint)probe.Client.LocalEndPoint!).Port;

        using var engine = Quiet();
        var ok = engine.Execute($"""
            use "@std"
            use "@osc"
            OscHandle h = (oscListen {port} "/x" (fn Double v => (print (str v))))
            """);
        Assert.True(ok, engine.ErrorReporter.FormatAll(engine.SourceMap, useColor: false));
        Assert.Equal(1, engine.Session.TrackedResourceCount);
#if !FLOW_WEB
        // oscListen connects on its background task. Observe readiness before
        // asserting that disposal releases a bound port; otherwise scheduling
        // can make the pre-disposal assertion run before the socket binds.
        var handle = engine.Context.GlobalFrame.GetVariable("h")
            .As<FlowLang.StandardLibrary.Network.OscHandleData>();
        Assert.NotNull(handle.Receiver);
        Assert.True(SpinWait.SpinUntil(
            () => handle.Receiver.State == Rug.Osc.OscSocketState.Connected,
            TimeSpan.FromSeconds(5)), "OSC listener did not connect");
#endif
        // The script never calls (oscStop); the port stays bound until the engine goes.
        Assert.ThrowsAny<System.Net.Sockets.SocketException>(() => new System.Net.Sockets.UdpClient(port).Dispose());

        engine.Dispose();
        using var rebound = new System.Net.Sockets.UdpClient(port);   // port released
    }
}
