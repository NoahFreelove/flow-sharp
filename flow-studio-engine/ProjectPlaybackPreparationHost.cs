namespace Flow.Studio.Engine;

/// <summary>Control-thread preparation owner: at most one running build and one
/// pending ticket. Request coalesces edits, Poll delivers results and retries audio
/// publication. No timers, per-edit task chains, or worker document mutations.
/// DisposeAsync cancels and joins the worker; dispose the audio device separately.</summary>
public sealed class ProjectPlaybackPreparationHost : IAsyncDisposable
{
    private sealed record Completion(PlaybackPreparation Request, PreparedProjectPlayback? Result, string? Error);
    private readonly Func<PlaybackPreparation, CancellationToken, PreparedProjectPlayback> _prepare;
    private Task<Completion>? _running;
    private CancellationTokenSource? _cancellation;
    private PlaybackPreparation? _pending, _active;
    private Task? _shutdown;
    public ProjectPlaybackCoordinator Playback { get; }
    public bool IsPreparing => _running is not null || _pending is not null || _shutdown is { IsCompleted: false };

    public ProjectPlaybackPreparationHost(ProjectPlaybackCoordinator playback, string projectDirectory)
        : this(playback, (request, cancellation) => ProjectPlaybackCoordinator.Prepare(request, projectDirectory, cancellation)) { }

    public ProjectPlaybackPreparationHost(ProjectPlaybackCoordinator playback,
        Func<PlaybackPreparation, CancellationToken, PreparedProjectPlayback> prepare)
    { Playback = playback; _prepare = prepare; }

    public void Request()
    {
        ThrowIfDisposed();
        _pending = Playback.BeginPreparation();
        // Only one pending snapshot survives a burst, even if cancellation is slow.
        if (_running is not null) _cancellation!.Cancel();
        else StartPending();
    }

    public void Poll()
    {
        ThrowIfDisposed();
        if (_running is { IsCompleted: true })
        {
            var completion = _running.GetAwaiter().GetResult();
            _running = null; _active = null;
            _cancellation!.Dispose(); _cancellation = null;
            if (completion.Result is not null) Playback.Complete(completion.Result);
            else Playback.Fail(completion.Request, completion.Error!);
            StartPending();
        }
        Playback.TryPublish();
    }

    private void StartPending()
    {
        if (_pending is null) return;
        var request = _pending; _pending = null; _active = request;
        var cancellation = new CancellationTokenSource(); _cancellation = cancellation;
        var prepare = _prepare;
        _running = Task.Run(() =>
        {
            try
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var result = prepare(request, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                return new Completion(request, result, null);
            }
            catch (OperationCanceledException) { return new Completion(request, null, "Playback preparation cancelled"); }
            catch (Exception error) { return new Completion(request, null, error.Message); }
        });
    }

    public ValueTask DisposeAsync()
    {
        if (_shutdown is not null) return new(_shutdown);
        var request = _pending ?? _active;
        if (request is not null) Playback.Fail(request, "Playback preparation stopped");
        _pending = null; _active = null;
        var running = _running; _running = null;
        var cancellation = _cancellation; _cancellation = null;
        cancellation?.Cancel();
        _shutdown = Join(running, cancellation);
        return new(_shutdown);
    }
    private static async Task Join(Task? running, CancellationTokenSource? cancellation)
    {
        try { if (running is not null) await running.ConfigureAwait(false); }
        finally { cancellation?.Dispose(); }
    }
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_shutdown is not null, this);
}
