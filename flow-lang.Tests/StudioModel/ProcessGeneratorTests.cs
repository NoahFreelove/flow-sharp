using System.Diagnostics;
using Flow.Studio.Model;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.StudioModel;

[Collection("FlowScripts")]
public class ProcessGeneratorTests
{
    private static readonly string Interpreter = Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll");
    private static GeneratorBuildRequest Request(Guid? id = null) => new(new(1, id ?? Guid.NewGuid(), "generate"), 7,
        FlowDawGenerator.Template, new(11, 987, new([new(0, 123), new(8, 99)]), new([new(1, 4, 4), new(4, 3, 4)]),
            new Dictionary<string, double> { ["shape"] = 0.25 }), TimeSpan.FromSeconds(20));

    [Fact]
    public async Task RealProcessReturnsEquivalentDetachedScoreAndExits()
    {
        var request = Request();
        var direct = FlowDawGenerator.Build(request, TestContext.Current.CancellationToken);
        Assert.True(direct.Status == JobStatus.Succeeded, direct.Error);
        await using var worker = ProcessGeneratorWorker.ForInterpreter(Interpreter);
        var result = await worker.BuildAsync(request, TestContext.Current.CancellationToken);
        Assert.True(result.Status == JobStatus.Succeeded, result.Error);
        Assert.Equal(CompositionJson.Serialize(direct.Value!.ScoreLayers[0].Composition), CompositionJson.Serialize(result.Value!.ScoreLayers[0].Composition));
        Assert.Same(request.Context, result.Value.Context);
        Assert.Equal(7, result.Value.SourceRevision);
        Assert.Null(worker.ProcessId);
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(worker.LastProcessId));
    }

    [Fact]
    public async Task UnresponsiveProcessIsKilledAndNextBuildStartsFresh()
    {
        int starts = 0;
        await using var worker = new ProcessGeneratorWorker(() => ++starts == 1
            ? new ProcessStartInfo("sleep") { ArgumentList = { "60" } }
            : new ProcessStartInfo("dotnet") { ArgumentList = { Interpreter, "--daw-worker" } });
        var request = Request();
        var timer = Stopwatch.StartNew();
        var timed = await worker.BuildAsync(request with { TimeLimit = TimeSpan.FromMilliseconds(250) }, TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.TimedOut, timed.Status);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5));
        int oldPid = worker.LastProcessId;
        Assert.Equal(1, worker.Kills);
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(oldPid));
        var good = await worker.BuildAsync(request, TestContext.Current.CancellationToken);
        Assert.True(good.Status == JobStatus.Succeeded, good.Error);
        Assert.NotEqual(oldPid, worker.LastProcessId);
        Assert.Equal(2, starts);
    }

    [Fact]
    public async Task CancellationAlsoInterruptsABlockedRequestWrite()
    {
        await using var worker = new ProcessGeneratorWorker(() => new ProcessStartInfo("sleep") { ArgumentList = { "60" } });
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var request = Request() with { Source = new string(' ', 1024 * 1024) };
        var result = await worker.BuildAsync(request, cancel.Token);
        Assert.Equal(JobStatus.Cancelled, result.Status);
        Assert.Equal(1, worker.Kills);
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(worker.LastProcessId));
    }

    [Fact]
    public async Task MalformedReplyCannotPublishAndChildIsJoined()
    {
        await using var worker = new ProcessGeneratorWorker(() => new ProcessStartInfo("sh")
        {
            ArgumentList = { "-c", "cat >/dev/null; printf 'not-json'" }
        });
        var result = await worker.BuildAsync(Request(), TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Failed, result.Status);
        Assert.Null(result.Value);
        Assert.Null(worker.ProcessId);
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(worker.LastProcessId));
    }

    [Fact]
    public async Task IsolatedHostKeepsLastGoodAfterBadSource()
    {
        var request = Request();
        using var host = new FlowDawGeneratorHost(request.Descriptor.SourceId, ProcessGeneratorWorker.ForInterpreter(Interpreter));
        var good = await host.Submit(request);
        Assert.True(good.Status == JobStatus.Succeeded, good.Error);
        var bad = await host.Submit(request with { Source = "not valid Flow", SourceRevision = 8 });
        Assert.Equal(JobStatus.Failed, bad.Status);
        Assert.Same(good.Value, host.LastGood);
    }
    [Fact]
    public async Task WrongRequestIdentityIsRejected()
    {
        await using var worker = new ProcessGeneratorWorker(() => new ProcessStartInfo("python3")
        {
            ArgumentList = { "-c", "import json,sys; r=json.load(sys.stdin); json.dump(dict(Version=6,RequestId='00000000-0000-0000-0000-000000000001',SourceId=r['Descriptor']['SourceId'],SourceRevision=r['SourceRevision'],ContextRevision=r['ContextRevision'],Status=0,Contents=None,Error=None),sys.stdout)" }
        });
        var result = await worker.BuildAsync(Request(), TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Failed, result.Status);
        Assert.Contains("identity/revision mismatch", result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task DisposalCancelsAndJoinsActiveWorker()
    {
        var worker = new ProcessGeneratorWorker(() => new ProcessStartInfo("sleep") { ArgumentList = { "60" } });
        var build = worker.BuildAsync(Request(), TestContext.Current.CancellationToken);
        // BuildAsync starts the process before reaching its first pipe-read wait.
        Assert.NotNull(worker.ProcessId);
        int pid = worker.LastProcessId;
        await worker.DisposeAsync();
        Assert.Equal(JobStatus.Cancelled, (await build).Status);
        Assert.Null(worker.ProcessId);
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(pid));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => worker.BuildAsync(Request(), TestContext.Current.CancellationToken));
        await worker.DisposeAsync();
    }
}
