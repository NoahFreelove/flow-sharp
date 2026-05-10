using OmniSharp.Extensions.LanguageServer.Protocol;

namespace FlowLsp;

/// <summary>
/// Per-URI buffer cache with debounced re-parse scheduling.
///
/// Design (D-03): each Update cancels any prior in-flight parse for the same URI,
/// schedules a fresh parse after the debounce window, and fires the injected
/// <c>onParse</c> callback. Close cancels pending work and removes the buffer.
///
/// Thread-safety: OmniSharp dispatches handlers on multiple threads, so the
/// internal <c>_buffers</c> Dictionary is always accessed under <c>_lock</c>.
///
/// Close-race guard: <see cref="HasDocument"/> is exposed so the onParse callback
/// can suppress publishDiagnostics for a URI that closed during the debounce
/// window. Without this guard a late parse would revive cleared squiggles.
/// </summary>
public sealed class DocumentManager
{
    private readonly Dictionary<DocumentUri, BufferEntry> _buffers = new();
    private readonly object _lock = new();
    private readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(150);
    private readonly Func<DocumentUri, string, CancellationToken, Task> _onParse;

    public DocumentManager(Func<DocumentUri, string, CancellationToken, Task> onParse)
        => _onParse = onParse;

    public void Open(DocumentUri uri, string text) => Update(uri, text);

    public void Update(DocumentUri uri, string text)
    {
        CancellationToken ct;
        lock (_lock)
        {
            if (_buffers.TryGetValue(uri, out var existing))
                existing.Cts.Cancel();
            var cts = new CancellationTokenSource();
            _buffers[uri] = new BufferEntry(text, cts);
            ct = cts.Token;
        }
        // Schedule outside the lock to avoid holding the lock across async dispatch.
        _ = ScheduleParseAsync(uri, text, ct);
    }

    public void Close(DocumentUri uri)
    {
        lock (_lock)
        {
            if (_buffers.Remove(uri, out var existing))
                existing.Cts.Cancel();
        }
    }

    /// <summary>
    /// True iff the document is currently tracked (open and not closed). The onParse
    /// callback checks this BEFORE publishing diagnostics, so a debounced parse that
    /// completes AFTER Close does not revive cleared diagnostics (the close-race guard).
    /// </summary>
    public bool HasDocument(DocumentUri uri)
    {
        lock (_lock)
        {
            return _buffers.ContainsKey(uri);
        }
    }

    public string? GetText(DocumentUri uri)
    {
        lock (_lock)
        {
            return _buffers.TryGetValue(uri, out var entry) ? entry.Text : null;
        }
    }

    private async Task ScheduleParseAsync(DocumentUri uri, string text, CancellationToken ct)
    {
        try { await Task.Delay(_debounce, ct); }
        catch (TaskCanceledException) { return; }
        if (ct.IsCancellationRequested) return;
        await _onParse(uri, text, ct);
    }

    private sealed record BufferEntry(string Text, CancellationTokenSource Cts);
}
