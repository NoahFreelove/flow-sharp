#if !FLOW_WEB
using System.Diagnostics;
using System.Text.Json;
using Flow.Studio.Model;

namespace FlowLang.Hosting;

/// <summary>Serialized, one-process-per-build generator host. Timeout/cancellation
/// covers writes, reads and exit; every child is joined before another is started.
/// This isolates hangs and engine GC, not filesystem/network privileges or OS memory.</summary>
public sealed class ProcessGeneratorWorker : IDisposable, IAsyncDisposable
{
    private readonly Func<ProcessStartInfo> _startInfo;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly object _state = new();
    private Process? _process;
    private int _disposed, _kills, _lastProcessId;
    public int Kills => Volatile.Read(ref _kills);
    public int LastProcessId => Volatile.Read(ref _lastProcessId);
    public int? ProcessId
    {
        get { lock (_state) return _process is { HasExited: false } p ? p.Id : null; }
    }

    public ProcessGeneratorWorker(Func<ProcessStartInfo> startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        _startInfo = startInfo;
    }
    public static ProcessGeneratorWorker ForInterpreter(string interpreterDll) =>
        new(() => new ProcessStartInfo("dotnet") { ArgumentList = { Path.GetFullPath(interpreterDll), "--daw-worker" } });

    public async Task<JobResult<GeneratedSourceOutput>> BuildAsync(GeneratorBuildRequest request, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TimeLimit <= TimeSpan.Zero || request.TimeLimit > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(request));
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _stop.Token);
        timeout.CancelAfter(request.TimeLimit);
        bool entered = false;
        Process? process = null;
        Task? stderr = null;
        try
        {
            await _gate.WaitAsync(timeout.Token).ConfigureAwait(false);
            entered = true;
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            // A previous teardown failure keeps ownership here; never overlap children.
            if (_process is not null) await TerminateAndJoinAsync(_process).ConfigureAwait(false);
            Guid id = Guid.NewGuid();
            string json = JsonSerializer.Serialize(GeneratorWireRequest.From(request, id), GeneratorWorkerProtocol.Json);
            if (json.Length > GeneratorWorkerProtocol.MaxRequestCharacters) throw new InvalidDataException("Generator request exceeds transport budget");
            timeout.Token.ThrowIfCancellationRequested();
            var info = _startInfo();
            info.RedirectStandardInput = info.RedirectStandardOutput = info.RedirectStandardError = true;
            info.UseShellExecute = false;
            process = Process.Start(info) ?? throw new InvalidOperationException("Could not start generator worker");
            lock (_state) _process = process;
            Volatile.Write(ref _lastProcessId, process.Id);
            stderr = DrainAsync(process.StandardError, timeout.Token);
            // Drain stdout while writing so a worker cannot deadlock on full pipes.
            var response = GeneratorWorkerProtocol.ReadBoundedAsync(process.StandardOutput,
                GeneratorWorkerProtocol.MaxResponseCharacters, timeout.Token);
            try
            {
                await process.StandardInput.WriteAsync(json.AsMemory(), timeout.Token).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(timeout.Token).ConfigureAwait(false);
                process.StandardInput.Close();
                string text = await response.ConfigureAwait(false);
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                if (process.ExitCode != 0) throw new InvalidDataException($"Generator worker exited with code {process.ExitCode}");
                var reply = JsonSerializer.Deserialize<GeneratorWireResponse>(text, GeneratorWorkerProtocol.Json)
                    ?? throw new JsonException("Missing generator reply");
                if (reply.Version != 6 || reply.RequestId != id || reply.SourceId != request.Descriptor.SourceId ||
                    reply.SourceRevision != request.SourceRevision || reply.ContextRevision != request.Context.Revision)
                    throw new InvalidDataException("Generator reply identity/revision mismatch");
                if (reply.Status is not (JobStatus.Succeeded or JobStatus.Failed or JobStatus.Cancelled or JobStatus.TimedOut))
                    throw new InvalidDataException("Invalid generator reply status");
                if (reply.Status != JobStatus.Succeeded) return new(reply.Status, null, reply.Error);
                if (reply.Contents is null) throw new InvalidDataException("Missing generator output contents");
                var generated = GeneratedContentJson.Deserialize(reply.Contents, request.Descriptor.SourceId, request.SourceRevision, request.Context);
                if (request.AudioInput is { } audioInput && request.Plugin is { } processor)
                    processor.ValidateOutput(generated, audioInput.SampleRate, 256);
                else if (request.NoteInput is not null && request.Plugin is { } noteProcessor)
                    noteProcessor.ValidateOutput(generated, 1, 1);
                timeout.Token.ThrowIfCancellationRequested();
                return new(JobStatus.Succeeded, generated);
            }
            finally
            {
                // Any write/parse failure also cancels outstanding pipe reads.
                timeout.Cancel();
                try { await response.ConfigureAwait(false); } catch { /* reported by the primary operation */ }
            }
        }
        catch (OperationCanceledException)
        {
            return new(cancellation.IsCancellationRequested || _stop.IsCancellationRequested ? JobStatus.Cancelled : JobStatus.TimedOut,
                null, "Generator worker cancelled or exceeded its deadline");
        }
        catch (Exception error) { return new(JobStatus.Failed, null, error.Message); }
        finally
        {
            timeout.Cancel();
            try
            {
                if (process is not null) await TerminateAndJoinAsync(process).ConfigureAwait(false);
                if (stderr is not null) try { await stderr.ConfigureAwait(false); } catch (OperationCanceledException) { }
            }
            finally { if (entered) _gate.Release(); }
        }
    }

    private static async Task DrainAsync(TextReader reader, CancellationToken token)
    {
        var buffer = new char[4096];
        while (await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false) != 0) { }
    }

    private async Task TerminateAndJoinAsync(Process process)
    {
        if (!process.HasExited)
        {
            try { process.Kill(entireProcessTree: true); Interlocked.Increment(ref _kills); }
            catch (InvalidOperationException) when (process.HasExited) { }
        }
        // On failure retain the Process and prevent reuse until a later successful join.
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        lock (_state)
        {
            if (ReferenceEquals(_process, process)) _process = null;
            process.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        _stop.Cancel();
        await _gate.WaitAsync().ConfigureAwait(false);
        try { if (_process is not null) await TerminateAndJoinAsync(_process).ConfigureAwait(false); }
        finally { _gate.Release(); }
        // Keep synchronization primitives alive for callers already waiting at disposal.
    }
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
#endif
