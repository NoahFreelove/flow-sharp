using System.Diagnostics;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.Hosting;

/// <summary>
/// Phase 2 gate: where a hard stop is required, the host kills the worker process
/// and the next request runs in a fresh worker.
/// </summary>
[Collection("FlowScripts")]
public class ProcessWorkerTests
{
    private static readonly string InterpreterDll = Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll");

    [Fact]
    public async Task WorkerEvaluatesWithCapturedOutput()
    {
        using var worker = ProcessEvaluationWorker.ForInterpreter(InterpreterDll);
        var result = await worker.EvaluateAsync("use \"@std\"\n(print (str (add 40 2)))", "w.flow", TimeSpan.FromSeconds(60));
        Assert.Equal(JobStatus.Succeeded, result.Status);
        Assert.Equal("42\n", result.Stdout);
        Assert.Equal(0, worker.Kills);

        var again = await worker.EvaluateAsync("use \"@std\"\n(print (undefinedThing))", "w.flow", TimeSpan.FromSeconds(60));
        Assert.Equal(JobStatus.Failed, again.Status);
        Assert.Equal(result.ProcessId, again.ProcessId);   // the same worker serves both
    }

    [Fact]
    public async Task UnresponsiveWorkerIsKilledAndReplaced()
    {
        // A worker that never answers stands in for native code that ignores cancellation.
        int starts = 0;
        using var hung = new ProcessEvaluationWorker(() =>
        {
            starts++;
            return new ProcessStartInfo("sleep") { ArgumentList = { "60" } };
        });
        var watch = Stopwatch.StartNew();
        var result = await hung.EvaluateAsync("use \"@std\"", "h.flow", TimeSpan.FromMilliseconds(300));
        watch.Stop();

        Assert.Equal(JobStatus.TimedOut, result.Status);
        Assert.Equal(1, hung.Kills);
        Assert.Null(hung.ProcessId);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(result.ProcessId));

        var retry = await hung.EvaluateAsync("use \"@std\"", "h.flow", TimeSpan.FromMilliseconds(300));
        Assert.Equal(JobStatus.TimedOut, retry.Status);
        Assert.Equal(2, starts);
        Assert.NotEqual(result.ProcessId, retry.ProcessId);
    }

    [Fact]
    public async Task HostCancellationKillsTheWorker()
    {
        using var worker = ProcessEvaluationWorker.ForInterpreter(InterpreterDll);
        using var cts = new CancellationTokenSource();
        var pending = worker.EvaluateAsync("""
            use "@std"
            (setMaxIterations 1000000)
            while true {
                for Int i in (range 0 1000) { (Nothing) }
            }
            """, "loop.flow", TimeSpan.FromMinutes(5), cts.Token);
        await Task.Delay(1500);
        cts.Cancel();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(JobStatus.Cancelled, result.Status);
        Assert.Equal(1, worker.Kills);
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(result.ProcessId));
    }
}
