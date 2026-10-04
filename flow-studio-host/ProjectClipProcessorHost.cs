#if !FLOW_WEB
using Flow.Audio;
using Flow.Studio.Model;
using FlowLang.Hosting;

namespace Flow.Studio.Host;

/// <summary>Single control-owner clip processor. Owns its isolated worker, not the
/// playback session. One operation at a time; Poll applies completion on the owner
/// thread. Dispose before the playback session.</summary>
public sealed class ProjectClipProcessorHost : IAsyncDisposable
{
    private readonly ProjectPlaybackSession _session;
    private readonly ProcessGeneratorWorker _worker;
    private IClipProcessingOperation? _operation;
    private CancellationTokenSource? _cancellation;
    private Task<JobResult<GeneratedSourceOutput>>? _running;
    private bool _disposed;
    private Task? _shutdown;
    public bool IsProcessing => _running is not null;
    public ProjectGenerationCompletion? LastCompletion { get; private set; }

    public ProjectClipProcessorHost(ProjectPlaybackSession session, ProcessGeneratorWorker worker)
    { _session = session; _worker = worker; }

    public void ProcessAudio(Guid clipId, PluginPackage package, IReadOnlyDictionary<string, double>? values = null,
        IReadOnlyDictionary<Guid, PcmAsset>? externalAssets = null, TimeSpan? timeLimit = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsProcessing) throw new InvalidOperationException("A clip processor is already running");
        var limit = timeLimit ?? TimeSpan.FromSeconds(20);
        if (limit <= TimeSpan.Zero || limit > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(timeLimit));
        var operation = AudioClipProcessingOperation.Capture(_session.Document, clipId, package, values, externalAssets);
        Start(operation, new(operation.Descriptor, 0, package.Source, operation.Context, limit, package, operation.Input));
    }

    public void ProcessNotes(Guid clipId, PluginPackage package, IReadOnlyDictionary<string, double>? values = null,
        TimeSpan? timeLimit = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsProcessing) throw new InvalidOperationException("A clip processor is already running");
        var limit = timeLimit ?? TimeSpan.FromSeconds(20);
        if (limit <= TimeSpan.Zero || limit > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(timeLimit));
        var operation = NoteClipProcessingOperation.Capture(_session.Document, clipId, package, values);
        Start(operation, new(operation.Descriptor, 0, package.Source, operation.Context, limit, package, NoteInput: operation.Input));
    }

    private void Start(IClipProcessingOperation operation, GeneratorBuildRequest request)
    {
        var cancellation = new CancellationTokenSource();
        _operation = operation; _cancellation = cancellation; LastCompletion = null;
        _running = Task.Run(async () =>
        {
            try
            {
                return await _worker.BuildAsync(request, cancellation.Token).ConfigureAwait(false);
            }
            catch (Exception error) { return new(JobStatus.Failed, null, error.Message); }
        });
    }

    public void Cancel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _operation?.Cancel(); _cancellation?.Cancel();
    }

    public void Poll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_operation is { IsCurrent: false }) _cancellation?.Cancel();
        if (_running is { IsCompleted: true })
        {
            var result = _running.GetAwaiter().GetResult();
            var operation = _operation!;
            var status = _cancellation!.IsCancellationRequested ? JobStatus.Superseded : result.Status;
            bool accepted = false; string? error = result.Error;
            try
            {
                if (status == JobStatus.Succeeded && result.Value is not null)
                {
                    accepted = operation.Accept(result.Value);
                    if (!accepted) status = JobStatus.Superseded;
                }
            }
            catch (Exception failure) { status = JobStatus.Failed; error = failure.Message; }
            operation.Cancel();
            _running = null; _operation = null; _cancellation.Dispose(); _cancellation = null;
            LastCompletion = new(operation.Descriptor.SourceId, 0, status, accepted, error);
            if (accepted) _session.RequestPreparation();
        }
        _session.Poll();
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true; _operation?.Cancel(); _cancellation?.Cancel();
        }
        if (_shutdown is null || _shutdown.IsFaulted) _shutdown = Shutdown();
        return new(_shutdown);
    }
    private async Task Shutdown()
    {
        try
        {
            if (_running is not null) await _running.ConfigureAwait(false);
            await _worker.DisposeAsync().ConfigureAwait(false);
        }
        finally { _cancellation?.Dispose(); }
        _running = null; _operation = null; _cancellation = null;
    }
}
#endif
