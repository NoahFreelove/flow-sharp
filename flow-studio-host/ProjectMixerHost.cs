using Flow.Studio.Model;
using Flow.Studio.Engine;
namespace Flow.Studio.Host;

public enum MixerGestureStatus { Committed, Stale, Failed }
public sealed record MixerGestureCompletion(Guid RequestId, Guid? TrackId, MixerGestureStatus Status, string? Error);

/// <summary>One control owner submits discrete mixer gestures and polls completion.
/// One worker runs at a time. Queued gestures capture the latest project only when
/// started, so discrete track/parameter gestures compose instead of superseding each other.
/// Drain completions to release the bounded outstanding-request capacity.</summary>
public sealed class ProjectMixerHost : IAsyncDisposable
{
    private sealed record Intent(Guid RequestId, Guid? TrackId, Func<ProjectDocument, MixerEditRequest> Begin, PlaybackParameterPreview? Preview = null);
    private sealed record Outcome(PreparedMixerEdit? Prepared, string? Error);
    private readonly ProjectPlaybackSession _session;
    private readonly Func<MixerEditRequest, CancellationToken, PreparedMixerEdit> _prepare;
    private readonly Queue<Intent> _pending = new();
    private readonly Queue<MixerGestureCompletion> _completed = new();
    private readonly int _capacity;
    private Intent? _active;
    private MixerEditRequest? _request;
    private CancellationTokenSource? _cancellation;
    private Task<Outcome>? _running;
    private Task? _shutdown;
    private PlaybackParameterPreview? _preview;
    public bool IsWorking => _running is not null || _pending.Count != 0 || _shutdown is { IsCompleted: false };
    public int OutstandingCount => _pending.Count + _completed.Count + (_active is null ? 0 : 1);

    public ProjectMixerHost(ProjectPlaybackSession session, int capacity = 64)
        : this(session, ProjectMixerAuthoring.Prepare, capacity) { }
    internal ProjectMixerHost(ProjectPlaybackSession session,
        Func<MixerEditRequest, CancellationToken, PreparedMixerEdit> prepare, int capacity = 64)
    {
        if (capacity is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(capacity));
        _session = session; _prepare = prepare; _capacity = capacity;
    }

    /// <summary>Returns false without creating a ticket or changing the document if
    /// outstanding work/results fill capacity. Validation failures leave it unchanged.</summary>
    public bool TryAddTrack(Guid trackId, string name, string destinationNodeId, out Guid requestId,
        int inputIndex = 0, Guid? instrumentBinding = null)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name); ArgumentException.ThrowIfNullOrWhiteSpace(destinationNodeId);
        if (trackId == Guid.Empty || name.Length > 1024 || destinationNodeId.Length > 128 || inputIndex < 0 || instrumentBinding == Guid.Empty)
            throw new ArgumentException("Invalid track gesture");
        requestId = Guid.Empty;
        if (OutstandingCount >= _capacity) return false;
        requestId = Guid.NewGuid();
        _pending.Enqueue(new(requestId, trackId, document => ProjectMixerAuthoring.BeginAddTrack(document, trackId, name, destinationNodeId, inputIndex, instrumentBinding)));
        return true;
    }

    /// <summary>Submit once per completed parameter gesture. Continuous preview
    /// and automation recording have separate transport/automation APIs.</summary>
    public bool TrySetParameter(string nodeId, string parameterId, double value, out Guid requestId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId); ArgumentException.ThrowIfNullOrWhiteSpace(parameterId);
        if (nodeId.Length > 128 || parameterId.Length > 128 || !double.IsFinite(value))
            throw new ArgumentException("Invalid parameter gesture");
        requestId = Guid.Empty;
        if (OutstandingCount >= _capacity) return false;
        requestId = Guid.NewGuid();
        _pending.Enqueue(new(requestId, null, document => ProjectMixerAuthoring.BeginSetParameter(document, nodeId, parameterId, value)));
        return true;
    }

    public bool TryBeginParameterPreview(string nodeId, string parameterId)
    {
        ThrowIfDisposed();
        if (IsWorking || OutstandingCount >= _capacity || _preview is { IsActive: true }) return false;
        return _session.Playback.TryBeginParameterPreview(nodeId, parameterId, out _preview);
    }
    public bool TryBeginPluginParameterPreview(Guid sourceId, string parameterId)
    {
        ThrowIfDisposed();
        if (IsWorking || OutstandingCount >= _capacity || _preview is { IsActive: true }) return false;
        return _session.Playback.TryBeginPluginParameterPreview(sourceId, parameterId, out _preview);
    }
    public bool TryUpdateParameterPreview(double value)
    { ThrowIfDisposed(); return _preview is { } p && _session.Playback.TryUpdateParameterPreview(p, value); }
    public void CancelParameterPreview()
    {
        ThrowIfDisposed();
        if (_preview is { } p) _session.Playback.CancelParameterPreview(p);
        _preview = null;
    }
    /// <summary>Capture one final knob value as one Flow-backed undoable edit.
    /// Failed/stale work restores the saved audible value; success retains preview
    /// until the newly prepared project replaces it.</summary>
    public bool TryCommitParameterPreview(out Guid requestId)
    {
        ThrowIfDisposed(); requestId = Guid.Empty;
        if (_preview is not { } p || IsWorking || OutstandingCount >= _capacity ||
            !_session.Playback.FreezeParameterPreview(p)) return false;
        if (p.PluginSourceId is { } sourceId)
        {
            requestId = Guid.NewGuid(); _preview = null;
            try
            {
                ProjectPluginCommands.SetParameter(_session.Document, sourceId, p.ParameterId, p.Value);
                _session.Playback.AcceptParameterPreview(p);
                _session.RequestPreparation();
                _completed.Enqueue(new(requestId, null, MixerGestureStatus.Committed, null));
            }
            catch (Exception error)
            {
                _session.Playback.CancelParameterPreview(p);
                _completed.Enqueue(new(requestId, null, MixerGestureStatus.Failed, error.Message));
            }
            return true;
        }
        var before = _session.Document.Snapshot;
        requestId = Guid.NewGuid();
        _pending.Enqueue(new(requestId, null, document =>
        {
            if (!ReferenceEquals(before, document.Snapshot)) throw new InvalidOperationException("Parameter gesture became stale");
            return ProjectMixerAuthoring.BeginSetParameter(document, p.NodeId, p.ParameterId, p.Value);
        }, p));
        _preview = null; return true;
    }

    public bool TryTakeCompletion(out MixerGestureCompletion? completion)
    { ThrowIfDisposed(); return _completed.TryDequeue(out completion); }

    public bool TryInsertEffect(string nodeId, string deviceId, string destinationNodeId, out Guid requestId,
        int inputIndex = 0, IReadOnlyDictionary<string, double>? parameters = null, int version = 1)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationNodeId);
        if (destinationNodeId.Length > 128 || inputIndex < 0) throw new ArgumentException("Invalid destination");
        // Validate/catalog-bound and detach values at submission, not when a caller's
        // mutable dictionary may have changed by the time this request starts.
        var definition = new Flow.Audio.Graph.AudioGraphNode(nodeId, deviceId, version, ["pendingInput"], parameters);
        return EnqueueEffect(doc => ProjectMixerAuthoring.BeginInsertEffect(doc, nodeId, deviceId, destinationNodeId,
            inputIndex, definition.Parameters, version), out requestId);
    }
    public bool TrySetBypass(string nodeId, bool bypassed, out Guid requestId)
    {
        ThrowIfDisposed(); ValidateNode(nodeId);
        return EnqueueEffect(doc => ProjectMixerAuthoring.BeginSetBypass(doc, nodeId, bypassed), out requestId);
    }
    public bool TryRemoveEffect(string nodeId, out Guid requestId)
    {
        ThrowIfDisposed(); ValidateNode(nodeId);
        return EnqueueEffect(doc => ProjectMixerAuthoring.BeginRemoveEffect(doc, nodeId), out requestId);
    }
    public bool TryReorderEffects(IReadOnlyList<string> order, out Guid requestId)
    {
        ThrowIfDisposed(); ArgumentNullException.ThrowIfNull(order);
        if (order.Count is < 2 or > 1024) throw new ArgumentException("Select a bounded serial effect chain");
        var captured = order.ToArray(); foreach (string id in captured) ValidateNode(id);
        return EnqueueEffect(doc => ProjectMixerAuthoring.BeginReorderEffects(doc, captured), out requestId);
    }
    private bool EnqueueEffect(Func<ProjectDocument, MixerEditRequest> begin, out Guid requestId)
    {
        requestId = Guid.Empty;
        if (OutstandingCount >= _capacity) return false;
        requestId = Guid.NewGuid(); _pending.Enqueue(new(requestId, null, begin)); return true;
    }
    private static void ValidateNode(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        if (nodeId.Length > 128) throw new ArgumentException("Invalid node ID");
    }

    public void Poll()
    {
        ThrowIfDisposed();
        if (_running is { IsCompleted: true })
        {
            var outcome = _running.GetAwaiter().GetResult();
            var intent = _active!; var request = _request!;
            _running = null; _active = null; _request = null;
            _cancellation!.Dispose(); _cancellation = null;
            MixerGestureStatus status = MixerGestureStatus.Failed; string? error = outcome.Error;
            try
            {
                if (outcome.Prepared is not null)
                {
                    bool committed = ProjectMixerAuthoring.Commit(_session.Document, outcome.Prepared);
                    status = committed ? MixerGestureStatus.Committed : MixerGestureStatus.Stale;
                    if (committed)
                    {
                        if (intent.Preview is { } preview) _session.Playback.AcceptParameterPreview(preview);
                        _session.RequestPreparation();
                    }
                }
            }
            catch (Exception failure) { error = failure.Message; }
            finally { _session.Document.DiscardBuild(request.Ticket); }
            if (status != MixerGestureStatus.Committed && intent.Preview is { } cancelled)
                _session.Playback.CancelParameterPreview(cancelled);
            _completed.Enqueue(new(intent.RequestId, intent.TrackId, status, error));
        }
        if (_running is null && _pending.TryDequeue(out var next)) Start(next);
        _session.Poll();
    }

    private void Start(Intent intent)
    {
        MixerEditRequest request;
        try
        {
            request = intent.Begin(_session.Document);
        }
        catch (Exception error)
        {
            if (intent.Preview is { } preview) _session.Playback.CancelParameterPreview(preview);
            _completed.Enqueue(new(intent.RequestId, intent.TrackId, MixerGestureStatus.Failed, error.Message));
            return;
        }
        _active = intent; _request = request;
        var cancellation = new CancellationTokenSource(); _cancellation = cancellation;
        var prepare = _prepare;
        _running = Task.Run(() =>
        {
            try
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var result = prepare(request, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                return new Outcome(result, null);
            }
            catch (Exception error) { return new Outcome(null, error.Message); }
        });
    }

    /// <summary>Cancel/join before disposing the playback session. No document
    /// mutations or completion callbacks run from the shutdown continuation.</summary>
    public ValueTask DisposeAsync()
    {
        if (_shutdown is not null) return new(_shutdown);
        if (_preview is { } preview) _session.Playback.CancelParameterPreview(preview);
        if (_active?.Preview is { } activePreview) _session.Playback.CancelParameterPreview(activePreview);
        foreach (var pending in _pending)
            if (pending.Preview is { } pendingPreview) _session.Playback.CancelParameterPreview(pendingPreview);
        _preview = null;
        if (_request is not null) _session.Document.DiscardBuild(_request.Ticket);
        _pending.Clear(); _completed.Clear(); _active = null; _request = null;
        var running = _running; _running = null;
        var cancellation = _cancellation; _cancellation = null;
        cancellation?.Cancel(); _shutdown = Join(running, cancellation);
        return new(_shutdown);
    }
    private static async Task Join(Task? running, CancellationTokenSource? cancellation)
    {
        try { if (running is not null) await running.ConfigureAwait(false); }
        finally { cancellation?.Dispose(); }
    }
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_shutdown is not null, this);
}
