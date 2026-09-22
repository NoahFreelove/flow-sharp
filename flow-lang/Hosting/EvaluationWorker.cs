#if !FLOW_WEB
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowLang.Core;

namespace FlowLang.Hosting;

/// <summary>One evaluation request sent to a worker process (one JSON line).</summary>
public sealed record WorkerRequest(long Id, string Source, string? FileName, int? TimeLimitMs);

/// <summary>A worker's reply to one <see cref="WorkerRequest"/> (one JSON line).</summary>
public sealed record WorkerResponse(
    long Id, string Outcome, string Stdout, string Stderr, IReadOnlyList<string> Errors, double ElapsedMs);

[JsonSerializable(typeof(WorkerRequest))]
[JsonSerializable(typeof(WorkerResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class WorkerJsonContext : JsonSerializerContext;

/// <summary>
/// Worker-process side: reads <see cref="WorkerRequest"/> lines, evaluates each in a
/// fresh engine whose output is captured (never written to the protocol stream),
/// and writes one <see cref="WorkerResponse"/> line per request. Returns at end of input.
/// Hosted by <c>flow-interpreter --worker</c>.
/// </summary>
public static class EvaluationWorkerServer
{
    public static int Run(TextReader input, TextWriter output)
    {
        string? line;
        while ((line = input.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            WorkerResponse response;
            try
            {
                var request = JsonSerializer.Deserialize(line, WorkerJsonContext.Default.WorkerRequest)
                    ?? throw new InvalidDataException("empty request");
                response = Evaluate(request);
            }
            catch (Exception ex)
            {
                response = new WorkerResponse(-1, "HostFailure", "", "", [ex.Message], 0);
            }
            output.WriteLine(JsonSerializer.Serialize(response, WorkerJsonContext.Default.WorkerResponse));
            output.Flush();
        }
        return 0;
    }

    private static WorkerResponse Evaluate(WorkerRequest request)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        using var engine = new FlowEngine(new EngineOptions { Output = stdout, Diagnostics = stderr });
        var result = engine.Evaluate(request.Source, request.FileName, new EvaluationOptions
        {
            TimeLimit = request.TimeLimitMs is int ms ? TimeSpan.FromMilliseconds(ms) : null,
        });
        var errors = result.Errors.Select(e => e.ToString())
            .Concat(result.Diagnostics.Select(d => d.Message))
            .ToList();
        return new WorkerResponse(request.Id, result.Outcome.ToString(), stdout.ToString(), stderr.ToString(),
            errors, result.Elapsed.TotalMilliseconds);
    }
}

/// <summary>Result of <see cref="ProcessEvaluationWorker.EvaluateAsync"/>.</summary>
public sealed record WorkerEvaluation(
    JobStatus Status, string Stdout, string Stderr, IReadOnlyList<string> Errors, int ProcessId);

/// <summary>
/// Host side of an out-of-process evaluator. Where cooperative cancellation is not
/// enough (native code, blocking builtins, a runaway plugin build), the host kills
/// the worker process tree on timeout or cancellation and starts a fresh one for the
/// next request. The audio device and the host's own state are unaffected.
/// </summary>
public sealed class ProcessEvaluationWorker : IDisposable
{
    private readonly Func<ProcessStartInfo> _startInfo;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
    private long _nextId;
    private bool _disposed;

    /// <param name="startInfo">Builds the command that starts a worker speaking the
    /// line protocol on stdin/stdout, for example <c>dotnet flow-interpreter.dll --worker</c>.</param>
    public ProcessEvaluationWorker(Func<ProcessStartInfo> startInfo) => _startInfo = startInfo;

    /// <summary>Worker running <c>dotnet &lt;interpreterDll&gt; --worker</c>.</summary>
    public static ProcessEvaluationWorker ForInterpreter(string interpreterDll) =>
        new(() => new ProcessStartInfo("dotnet") { ArgumentList = { interpreterDll, "--worker" } });

    /// <summary>Process id of the running worker, or null when none is running.</summary>
    public int? ProcessId => _process is { HasExited: false } p ? p.Id : null;

    /// <summary>Workers killed because a request timed out or was cancelled.</summary>
    public int Kills { get; private set; }

    /// <summary>
    /// Evaluates <paramref name="source"/> in the worker. If it does not answer within
    /// <paramref name="hardTimeout"/>, or <paramref name="cancellation"/> fires, the
    /// worker process tree is killed and awaited before this returns.
    /// </summary>
    public async Task<WorkerEvaluation> EvaluateAsync(
        string source, string? fileName, TimeSpan hardTimeout, CancellationToken cancellation = default)
    {
        await _gate.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var process = EnsureStarted();
            long id = ++_nextId;
            var request = new WorkerRequest(id, source, fileName, (int)hardTimeout.TotalMilliseconds);
            await process.StandardInput.WriteLineAsync(
                JsonSerializer.Serialize(request, WorkerJsonContext.Default.WorkerRequest)).ConfigureAwait(false);
            await process.StandardInput.FlushAsync().ConfigureAwait(false);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(hardTimeout);
            try
            {
                var line = await process.StandardOutput.ReadLineAsync(timeout.Token).ConfigureAwait(false);
                if (line is null)
                {
                    int exitedPid = process.Id;
                    Discard();
                    return new WorkerEvaluation(JobStatus.Failed, "", "", ["worker exited unexpectedly"], exitedPid);
                }
                var response = JsonSerializer.Deserialize(line, WorkerJsonContext.Default.WorkerResponse)!;
                var status = response.Outcome switch
                {
                    nameof(EvaluationOutcome.Succeeded) => JobStatus.Succeeded,
                    nameof(EvaluationOutcome.TimedOut) => JobStatus.TimedOut,
                    nameof(EvaluationOutcome.Cancelled) => JobStatus.Cancelled,
                    _ => JobStatus.Failed,
                };
                return new WorkerEvaluation(status, response.Stdout, response.Stderr, response.Errors, process.Id);
            }
            catch (OperationCanceledException)
            {
                int killedPid = process.Id;
                await KillAsync(process).ConfigureAwait(false);
                var status = cancellation.IsCancellationRequested ? JobStatus.Cancelled : JobStatus.TimedOut;
                return new WorkerEvaluation(status, "", "",
                    [status == JobStatus.Cancelled ? "evaluation cancelled; worker terminated" : "evaluation timed out; worker terminated"],
                    killedPid);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private Process EnsureStarted()
    {
        if (_process is { HasExited: false } running) return running;
        _process?.Dispose();
        var info = _startInfo();
        info.RedirectStandardInput = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.UseShellExecute = false;
        var process = Process.Start(info) ?? throw new InvalidOperationException("could not start worker process");
        // Drain stderr so a chatty worker cannot block on a full pipe.
        process.ErrorDataReceived += (_, _) => { };
        process.BeginErrorReadLine();
        _process = process;
        return process;
    }

    private async Task KillAsync(Process process)
    {
        Kills++;
        try
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
        Discard();
    }

    private void Discard()
    {
        _process?.Dispose();
        _process = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_process is { HasExited: false } p)
        {
            try
            {
                p.StandardInput.Close();
                if (!p.WaitForExit(2000)) p.Kill(entireProcessTree: true);
            }
            catch { /* best effort */ }
        }
        Discard();
        _gate.Dispose();
    }
}
#endif
