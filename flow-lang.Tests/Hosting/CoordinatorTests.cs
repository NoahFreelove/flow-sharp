using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.Hosting;

/// <summary>
/// Phase 2 gate: superseded and timed-out work does not remain active, stale results
/// are rejected, and the previous good result survives a failed edit.
/// </summary>
[Collection("FlowScripts")]
public class CoordinatorTests
{
    private sealed record Rendered(string Text);

    private const string Endless = """
        use "@std"
        (setMaxIterations 1000000)
        while true {
            for Int i in (range 0 1000) { (Nothing) }
        }
        """;

    // Evaluates source in a fresh engine the way watch mode does.
    private static JobResult<Rendered> Evaluate(string source, CancellationToken token, TimeSpan? limit = null)
    {
        var output = new StringWriter();
        using var engine = new FlowEngine(new EngineOptions { Output = output, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(source, "edit.flow", new EvaluationOptions { Cancellation = token, TimeLimit = limit });
        var status = result.Outcome switch
        {
            EvaluationOutcome.Succeeded => JobStatus.Succeeded,
            EvaluationOutcome.TimedOut => JobStatus.TimedOut,
            EvaluationOutcome.Cancelled => JobStatus.Cancelled,
            _ => JobStatus.Failed,
        };
        return new JobResult<Rendered>(status, status == JobStatus.Succeeded ? new Rendered(output.ToString()) : null,
            result.Errors.FirstOrDefault()?.Message);
    }

    [Fact]
    public async Task NewEditSupersedesARunawayEvaluation()
    {
        using var coordinator = new LatestRequestCoordinator<Rendered>();
        var runaway = coordinator.Submit(t => Evaluate(Endless, t));
        await Task.Delay(100);
        var edit = coordinator.Submit(t => Evaluate("use \"@std\"\n(print \"fixed\")", t));

        var first = await runaway.WaitAsync(TimeSpan.FromSeconds(10));
        var second = await edit.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(JobStatus.Superseded, first.Status);
        Assert.Equal(JobStatus.Succeeded, second.Status);
        Assert.Equal("fixed\n", coordinator.LastGood!.Text);
        Assert.Equal(0, coordinator.ActiveJobs);
    }

    [Fact]
    public async Task FailedEditKeepsThePreviousGoodResult()
    {
        using var coordinator = new LatestRequestCoordinator<Rendered>();
        var good = await coordinator.Submit(t => Evaluate("use \"@std\"\n(print \"v1\")", t));
        var broken = await coordinator.Submit(t => Evaluate("use \"@std\"\n(print (undefinedThing))", t));
        var timedOut = await coordinator.Submit(t => Evaluate(Endless, t, TimeSpan.FromMilliseconds(150)));

        Assert.Equal(JobStatus.Succeeded, good.Status);
        Assert.Equal(JobStatus.Failed, broken.Status);
        Assert.Equal(JobStatus.TimedOut, timedOut.Status);
        Assert.Equal("v1\n", coordinator.LastGood!.Text);
        Assert.Equal(good.Generation, coordinator.LastGoodGeneration);
        Assert.Equal(0, coordinator.ActiveJobs);
    }

    [Fact]
    public async Task LateResultOfASupersededJobIsRejected()
    {
        using var coordinator = new LatestRequestCoordinator<Rendered> { TerminationGrace = TimeSpan.FromMilliseconds(50) };
        using var release = new ManualResetEventSlim(false);
        // Ignores its token: finishes only when released, after the newer job.
        var stubborn = coordinator.Submit(_ =>
        {
            release.Wait();
            return new JobResult<Rendered>(JobStatus.Succeeded, new Rendered("stale"));
        });
        await Task.Delay(50);
        var newer = await coordinator.Submit(_ => new JobResult<Rendered>(JobStatus.Succeeded, new Rendered("fresh")));
        Assert.Equal(1, coordinator.ActiveJobs);   // honest: the stubborn job is still running
        release.Set();
        var late = await stubborn.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(JobStatus.Succeeded, newer.Status);
        Assert.Equal(JobStatus.Superseded, late.Status);
        Assert.Null(late.Value);
        Assert.Equal("fresh", coordinator.LastGood!.Text);
        Assert.Equal(0, coordinator.ActiveJobs);
    }

    [Fact]
    public async Task DisposeCancelsTheRunningJob()
    {
        var coordinator = new LatestRequestCoordinator<Rendered>();
        var running = coordinator.Submit(t => Evaluate(Endless, t));
        await Task.Delay(100);
        coordinator.Dispose();
        var completion = await running.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(JobStatus.Superseded, completion.Status);
        Assert.Equal(0, coordinator.ActiveJobs);
    }
}
