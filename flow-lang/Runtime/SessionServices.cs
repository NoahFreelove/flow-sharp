namespace FlowLang.Runtime;

/// <summary>
/// Everything one engine session owns that used to be process-global: output and
/// diagnostic sinks, advisory deduplication, the configuration snapshot, the
/// cancellation token and budget of the running evaluation, and domain services
/// (render caches, random state) registered by the music layer.
///
/// Ownership is explicit: the engine creates its session and disposes it. Legacy
/// static call sites that cannot yet take the session as a parameter (renderer
/// internals, advisory helpers) find it through <see cref="Current"/>, which the
/// engine sets for the duration of each entry point with <see cref="Enter"/>.
/// This lookup is a transitional adapter slated for removal when the renderer
/// takes explicit render contexts (roadmap Phase 5); see
/// docs/decisions/2026-09-22-session-lifetime.md.
/// </summary>
public sealed class SessionServices : IDisposable
{
    private static readonly AsyncLocal<SessionServices?> _current = new();

    private readonly TextWriter? _output;
    private readonly TextWriter? _diagnostics;
    private readonly AdvisoryLog _advisories;
    private readonly Dictionary<Type, object> _services = new();
    private readonly List<Action> _tracked = new();
    private readonly object _lock = new();
    private bool _disposed;

    /// <param name="output">Receives <c>print</c> output. Null follows <see cref="Console.Out"/>.</param>
    /// <param name="diagnostics">Receives advisories and warnings. Null follows <see cref="Console.Error"/>.</param>
    /// <param name="config">Configuration snapshot. Null takes <see cref="FlowConfig.Active"/>.</param>
    /// <param name="advisories">One-shot advisory log. Null gives this session its own; a host
    /// that recreates engines for one logical session (watch mode) can share one log.</param>
    public SessionServices(TextWriter? output = null, TextWriter? diagnostics = null,
        FlowConfigPoco? config = null, AdvisoryLog? advisories = null)
    {
        _output = output;
        _diagnostics = diagnostics;
        Config = config ?? FlowConfig.Active;
        _advisories = advisories ?? new AdvisoryLog();
    }

    /// <summary>The one-shot advisory log this session deduplicates against.</summary>
    public AdvisoryLog Advisories => _advisories;

    /// <summary>The session in whose entry point the current code runs, if any.</summary>
    public static SessionServices? Current => _current.Value;

    public TextWriter Output => _output ?? Console.Out;

    public TextWriter Diagnostics => _diagnostics ?? Console.Error;

    /// <summary>Configuration snapshot taken when the session was created.</summary>
    public FlowConfigPoco Config { get; set; }

    /// <summary>Cancellation of the evaluation currently running in this session.</summary>
    public CancellationToken Cancellation { get; internal set; }

    /// <summary>
    /// Host ceiling for loop iterations. <c>(setMaxIterations N)</c> can lower the
    /// script's limit freely but cannot raise it above this value.
    /// </summary>
    public int MaxIterationsCeiling { get; init; } = 1_000_000;

    public bool IsDisposed => _disposed;

    /// <summary>
    /// Stopwatch timestamp after which the running evaluation is out of time, or 0.
    /// Checked at every checkpoint, so time budgets hold without timer callbacks
    /// (single-threaded WASM cannot run a timer while a script is busy).
    /// </summary>
    public long Deadline { get; internal set; }

    /// <summary>
    /// Cancellation checkpoint. Throws <see cref="OperationCanceledException"/> when
    /// the running evaluation was cancelled, or <see cref="EvaluationTimeoutException"/>
    /// when it passed its deadline.
    /// </summary>
    public void ThrowIfCancelled()
    {
        Cancellation.ThrowIfCancellationRequested();
        if (Deadline != 0 && System.Diagnostics.Stopwatch.GetTimestamp() > Deadline)
            throw new EvaluationTimeoutException();
    }

    /// <summary>
    /// Writes <paramref name="message"/> to this session's diagnostics the first time
    /// <paramref name="key"/> is seen in this session. Returns true when written.
    /// </summary>
    public bool WarnOnce(string key, string message)
    {
        if (!_advisories.TryAdd(key)) return false;
        Diagnostics.WriteLine(message);
        return true;
    }

    public bool WasWarned(string key) => _advisories.Contains(key);

    public void ResetAdvisories() => _advisories.Clear();

    /// <summary>Returns the session's service of type <typeparamref name="T"/>, creating it once.</summary>
    public T GetOrAdd<T>(Func<T> factory) where T : class
    {
        lock (_lock)
        {
            if (_services.TryGetValue(typeof(T), out var existing)) return (T)existing;
            var created = factory();
            _services[typeof(T)] = created;
            return created;
        }
    }

    public T? Get<T>() where T : class
    {
        lock (_lock) return _services.TryGetValue(typeof(T), out var s) ? (T)s : null;
    }

    public void Set<T>(T service) where T : class
    {
        lock (_lock) _services[typeof(T)] = service;
    }

    /// <summary>
    /// Registers a resource a script opened (listener, clock, device) so it is
    /// released when the session is disposed, even if the script never stops it.
    /// Release actions run in reverse order and must be idempotent.
    /// </summary>
    public void Track(Action release)
    {
        bool disposed;
        lock (_lock)
        {
            disposed = _disposed;
            if (!disposed) _tracked.Add(release);
        }
        if (disposed) release();
    }

    /// <summary>Number of tracked resources not yet released (diagnostics and tests).</summary>
    public int TrackedResourceCount
    {
        get { lock (_lock) return _tracked.Count; }
    }

    /// <summary>
    /// Makes this session <see cref="Current"/> until the returned scope is disposed.
    /// Scopes nest and flow into tasks and threads started inside them.
    /// </summary>
    public Scope Enter()
    {
        var previous = _current.Value;
        _current.Value = this;
        return new Scope(previous);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        List<IDisposable> owned;
        List<Action> tracked;
        lock (_lock)
        {
            owned = _services.Values.OfType<IDisposable>().ToList();
            _services.Clear();
            tracked = new List<Action>(_tracked);
            _tracked.Clear();
        }
        for (int i = tracked.Count - 1; i >= 0; i--)
        {
            try { tracked[i](); } catch { /* release is best effort */ }
        }
        foreach (var service in owned)
        {
            try { service.Dispose(); } catch { /* disposal is best effort */ }
        }
        if (ReferenceEquals(_current.Value, this)) _current.Value = null;
    }

    public readonly struct Scope(SessionServices? previous) : IDisposable
    {
        public void Dispose() => _current.Value = previous;
    }
}

/// <summary>Thread-safe set of advisory keys already shown.</summary>
public sealed class AdvisoryLog
{
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);

    /// <summary>Records <paramref name="key"/>; false when it was already recorded.</summary>
    public bool TryAdd(string key)
    {
        lock (_keys) return _keys.Add(key);
    }

    public bool Contains(string key)
    {
        lock (_keys) return _keys.Contains(key);
    }

    public void Clear()
    {
        lock (_keys) _keys.Clear();
    }
}

/// <summary>Thrown at a checkpoint when an evaluation exceeds its time budget.</summary>
public sealed class EvaluationTimeoutException : OperationCanceledException
{
    public EvaluationTimeoutException() : base("evaluation exceeded its time limit") { }
}

/// <summary>
/// Output helpers for code that cannot receive the session explicitly: writes go to
/// the current session's sinks, or to the process console outside any session.
/// </summary>
public static class FlowConsole
{
    public static TextWriter Out => SessionServices.Current?.Output ?? Console.Out;

    public static TextWriter Error => SessionServices.Current?.Diagnostics ?? Console.Error;
}
