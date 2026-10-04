using Flow.Studio.Model;

namespace Flow.Studio.Host;

/// <summary>Control-owner polled autosave. One background write at a time, with
/// newer edits coalesced into the next snapshot. Dispose flushes the snapshot
/// captured at close; the caller must stop editing and await disposal.</summary>
public sealed class ProjectAutosaveHost : IAsyncDisposable
{
    private sealed record Completion(long Version, bool Deleted, Exception? Error);
    private readonly ProjectDocument _document;
    private readonly string _path;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _interval;
    private readonly FileStream _lease;
    private readonly Action<string, ProjectSnapshot> _write;
    private long _lastAttempt, _writtenVersion = -1;
    private bool _owned, _disposed;
    private Task<Completion>? _running;
    private Task? _shutdown;
    public bool IsSaving => _running is not null || _shutdown is { IsCompleted: false };
    public Exception? LastError { get; private set; }
    public long LastSavedVersion => _writtenVersion;
    public string RecoveryPath => _path;

    public ProjectAutosaveHost(ProjectDocument document, string recoveryPath, string? projectPath = null,
        TimeSpan? interval = null, TimeProvider? clock = null)
        : this(document, recoveryPath, projectPath, interval, clock, ProjectFile.SaveRecovery) { }

    internal ProjectAutosaveHost(ProjectDocument document, string recoveryPath, string? projectPath,
        TimeSpan? interval, TimeProvider? clock, Action<string, ProjectSnapshot> write)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document; _path = Path.GetFullPath(recoveryPath); _write = write;
        _interval = interval ?? TimeSpan.FromSeconds(30); _clock = clock ?? TimeProvider.System;
        if (_interval <= TimeSpan.Zero || _interval > TimeSpan.FromHours(1)) throw new ArgumentOutOfRangeException(nameof(interval));
        if (projectPath is not null && (Path.GetFullPath(projectPath) == _path || Path.GetFullPath(projectPath) == _path + ".lock"))
            throw new ArgumentException("Recovery and its lease must have separate paths from the project");
        _lastAttempt = _clock.GetTimestamp();
        _lease = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (File.Exists(_path))
        {
            _lease.Dispose();
            throw new InvalidOperationException("Inspect and resolve existing recovery before starting autosave");
        }
    }
    public void Poll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_running is { IsCompleted: true }) { Apply(_running.GetAwaiter().GetResult()); _running = null; }
        if (_running is not null || _clock.GetElapsedTime(_lastAttempt) < _interval) return;
        bool dirty = _document.History.IsDirty;
        if (dirty ? _writtenVersion == _document.ChangeVersion : !_owned) return;
        var snapshot = dirty ? _document.Snapshot : null;
        long version = _document.ChangeVersion;
        _lastAttempt = _clock.GetTimestamp();
        _running = Task.Run(() => Write(snapshot, version));
    }
    private Completion Write(ProjectSnapshot? snapshot, long version)
    {
        try
        {
            if (snapshot is null) File.Delete(_path); else _write(_path, snapshot);
            return new(version, snapshot is null, null);
        }
        catch (Exception error) { return new(version, snapshot is null, error); }
    }
    private void Apply(Completion result)
    {
        LastError = result.Error;
        if (result.Error is not null) return;
        _owned = !result.Deleted; _writtenVersion = result.Deleted ? -1 : result.Version;
    }
    public ValueTask DisposeAsync()
    {
        if (_shutdown is not null) return new(_shutdown);
        _disposed = true;
        // Capture before awaiting: background work never reads a mutable document.
        var snapshot = _document.History.IsDirty ? _document.Snapshot : null;
        long version = _document.ChangeVersion;
        _shutdown = Close(snapshot, version);
        return new(_shutdown);
    }
    private async Task Close(ProjectSnapshot? snapshot, long version)
    {
        try
        {
            if (_running is not null) { Apply(await _running.ConfigureAwait(false)); _running = null; }
            if (snapshot is not null && _writtenVersion != version || snapshot is null && _owned)
                Apply(await Task.Run(() => Write(snapshot, version)).ConfigureAwait(false));
            if (LastError is not null) throw new IOException("Final recovery save failed", LastError);
        }
        finally { _lease.Dispose(); }
    }
}
