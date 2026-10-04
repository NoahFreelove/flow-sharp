#if !FLOW_WEB
using Flow.Studio.Model;
using Flow.Studio.Engine;
using Flow.Audio;
using FlowLang.Hosting;
namespace Flow.Studio.Host;

public sealed record ProjectGenerationCompletion(Guid SourceId, long Revision, JobStatus Status, bool Accepted, string? Error);

/// <summary>Single control-owner adapter from isolated Flow builds to document
/// acceptance and playback preparation. Poll delivers completion on the caller's
/// thread. One process runs at a time; queued requests coalesce per source.</summary>
public sealed class ProjectGeneratorHost : IAsyncDisposable
{
    private sealed record Request(SourceBuildTicket Ticket, TimeSpan Limit, PluginPackage? Plugin, int Size,
        ProjectSnapshot Snapshot, long ChangeVersion, Guid[] AssetIds);
    private readonly ProjectPlaybackSession _session;
    private readonly ProcessGeneratorWorker _worker;
    private readonly LinkedList<Request> _pending = new();
    private readonly Dictionary<Guid, LinkedListNode<Request>> _bySource = [];
    private Request? _active;
    private CancellationTokenSource? _cancellation;
    private Task<JobResult<GeneratedSourceOutput>>? _running;
    private int _pendingCharacters;
    private bool _disposed;
    private Task? _shutdown;
    public const int MaxPendingSources = 64, MaxPendingCharacters = 16 * 1024 * 1024;
    public int PendingCount => _pending.Count;
    public bool IsBuilding => _running is not null || _pending.Count != 0 || _shutdown is { IsCompleted: false };
    public ProjectGenerationCompletion? LastCompletion { get; private set; }

    /// <summary>Takes exclusive ownership of worker; does not own the playback session.
    /// Dispose this host before disposing the playback session.</summary>
    public ProjectGeneratorHost(ProjectPlaybackSession session, ProcessGeneratorWorker worker)
    { _session = session; _worker = worker; }

    public void RequestBuild(GeneratorDescriptor descriptor, string code, TimeSpan? timeLimit = null)
        => RequestBuildCore(descriptor, code, null, timeLimit);
    public void RequestBuildWithAssets(GeneratorDescriptor descriptor, string code, IEnumerable<Guid> assetIds, TimeSpan? timeLimit = null)
        => RequestBuildCore(descriptor, code, null, timeLimit, assetIds);

    public void RequestPluginBuild(Guid sourceId, PluginPackage plugin, TimeSpan? timeLimit = null)
    {
        ThrowIfDisposed(); ArgumentNullException.ThrowIfNull(plugin);
        plugin.Manifest.ValidateProcessing(_session.Playback.Queue.SampleRate, _session.Playback.Queue.MaxBlockFrames);
        if (plugin.Manifest.Kind is not (FlowPluginKind.AudioEffect or FlowPluginKind.Instrument)) throw new NotSupportedException("Only declared audio-effect and graph-instrument builds are supported by this adapter");
        RequestBuildCore(new(1, sourceId, plugin.Manifest.Builder), plugin.Source, plugin, timeLimit);
    }
    private void RequestBuildCore(GeneratorDescriptor descriptor, string code, PluginPackage? plugin, TimeSpan? timeLimit, IEnumerable<Guid>? assetIds = null)
    {
        ThrowIfDisposed(); ArgumentNullException.ThrowIfNull(descriptor); ArgumentNullException.ThrowIfNull(code);
        var snapshot = _session.Document.Snapshot;
        var ids = assetIds?.Take(33).ToArray() ?? (plugin is null && snapshot.Sources.TryGetValue(descriptor.SourceId, out var existing)
            ? existing.AssetGrants.ToArray() : []);
        if (ids.Length > 32 || ids.Distinct().Count() != ids.Length || ids.Any(id => !snapshot.Assets.Any(a => a.Id == id)))
            throw new ArgumentException("Generator samples must select at most 32 distinct project audio assets");
        if (snapshot.Assets.Where(a => ids.Contains(a.Id)).Sum(a => checked(a.Frames * 8)) > GeneratorAssets.MaxBytes)
            throw new ArgumentException("Generator sample decode budget exceeded");
        var limit = timeLimit ?? TimeSpan.FromSeconds(20);
        if (limit <= TimeSpan.Zero || limit > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(timeLimit));
        _bySource.TryGetValue(descriptor.SourceId, out var previous);
        int requestSize = checked(code.Length + (plugin?.Serialize().Length ?? 0));
        int size = checked(_pendingCharacters - (previous?.Value.Size ?? 0) + requestSize);
        if ((previous is null && _pending.Count >= MaxPendingSources) || size > MaxPendingCharacters)
            throw new InvalidOperationException("Pending generator budget exceeded");
        var ticket = _session.Document.BeginBuild(descriptor, code);
        var request = new Request(ticket, limit, plugin, requestSize, snapshot, _session.Document.ChangeVersion, ids);
        if (previous is not null) previous.Value = request;
        else _bySource.Add(descriptor.SourceId, _pending.AddLast(request));
        _pendingCharacters = size;
        if (_active?.Ticket.Descriptor.SourceId == descriptor.SourceId) _cancellation!.Cancel();
        if (_running is null) StartPending();
    }

    public void Poll()
    {
        ThrowIfDisposed();
        if (_running is { IsCompleted: true })
        {
            var result = _running.GetAwaiter().GetResult();
            var request = _active!; var ticket = request.Ticket;
            bool cancelled = _cancellation!.IsCancellationRequested;
            _running = null; _active = null; _cancellation.Dispose(); _cancellation = null;
            bool accepted = false; var status = cancelled ? JobStatus.Superseded : result.Status;
            string? error = result.Error;
            try
            {
                if (request.AssetIds.Length != 0 && request.ChangeVersion != _session.Document.ChangeVersion)
                    status = JobStatus.Superseded;
                var originalGrants = request.Snapshot.Sources.TryGetValue(ticket.Descriptor.SourceId, out var original) ? original.AssetGrants : [];
                var currentGrants = _session.Document.Snapshot.Sources.TryGetValue(ticket.Descriptor.SourceId, out var current) ? current.AssetGrants : [];
                if (!originalGrants.SequenceEqual(currentGrants)) status = JobStatus.Superseded;
                if (status == JobStatus.Succeeded && result.Value is not null)
                {
                    accepted = request.Plugin is null ? _session.Document.Accept(ticket, result.Value, "Accept Flow generator",
                        p => ProjectAssetGrantCommands.WithGrants(p, ticket.Descriptor.SourceId, request.AssetIds))
                        : _session.Document.Accept(ticket, result.Value, "Accept Flow plugin", static p => p, plugin: request.Plugin);
                    if (!accepted) status = JobStatus.Superseded;
                }
            }
            catch (Exception failure) { status = JobStatus.Failed; error = failure.Message; }
            _session.Document.DiscardBuild(ticket);
            LastCompletion = new(ticket.Descriptor.SourceId, ticket.Revision, status, accepted, error);
            if (accepted) _session.RequestPreparation();
            StartPending();
        }
        _session.Poll();
    }
    private void StartPending()
    {
        if (_pending.First is not { } first) return;
        var request = first.Value; _pending.RemoveFirst();
        _bySource.Remove(request.Ticket.Descriptor.SourceId); _pendingCharacters -= request.Size;
        _active = request; var cancellation = new CancellationTokenSource(); _cancellation = cancellation;
        var worker = _worker;
        string directory = _session.ProjectDirectory;
        int sampleRate = _session.Playback.Queue.SampleRate, blockFrames = _session.Playback.Queue.MaxBlockFrames;
        _running = Task.Run(async () =>
        {
            try
            {
                var samples = new List<KeyValuePair<Guid, PcmAsset>>(); long remaining = GeneratorAssets.MaxBytes;
                foreach (var id in request.AssetIds)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    var reference = request.Snapshot.Assets.Single(a => a.Id == id);
                    var sample = AudioAssetFiles.Resolve(directory, reference, remaining, cancellation.Token);
                    remaining -= sample.Bytes; samples.Add(new(id, sample));
                }
                var result = await worker.BuildAsync(new(request.Ticket.Descriptor, request.Ticket.Revision,
                    request.Ticket.Code, request.Ticket.Context, request.Limit, request.Plugin,
                    Assets: samples.Count == 0 ? null : new GeneratorAssets(samples)), cancellation.Token).ConfigureAwait(false);
                if (result.Status == JobStatus.Succeeded && result.Value is not null && request.Plugin is { } plugin)
                    plugin.ValidateOutput(result.Value, sampleRate, blockFrames);
                cancellation.Token.ThrowIfCancellationRequested();
                return result;
            }
            catch (Exception error) { return new JobResult<GeneratedSourceOutput>(JobStatus.Failed, null, error.Message); }
        });
    }
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            foreach (var request in _pending) _session.Document.DiscardBuild(request.Ticket);
            if (_active is not null) _session.Document.DiscardBuild(_active.Ticket);
            _pending.Clear(); _bySource.Clear(); _pendingCharacters = 0;
            _cancellation?.Cancel();
        }
        // Worker close failures retain ownership and can be retried explicitly.
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
        _running = null; _active = null; _cancellation = null;
    }
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
#endif
