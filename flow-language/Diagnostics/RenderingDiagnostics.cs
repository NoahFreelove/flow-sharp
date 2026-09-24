namespace FlowLang.Diagnostics;

/// <summary>
/// One-shot warning channel with per-session per-sentinel-key deduplication.
/// Inside an engine session (<see cref="Runtime.SessionServices.Current"/>) the
/// advisory goes to that session's diagnostic sink and is deduplicated per session;
/// outside any session it goes to stderr with process-wide deduplication.
/// Phase 23 Plan 23-03 Task 1 (CONTEXT D-11 / D-13 / Pitfall 5).
///
/// Public surface:
///   - <see cref="WarnOnce"/> — emit at most once per sentinel key per process.
///   - <see cref="ResetForTesting"/> — clear dedup state for [Collection]-isolated Facts.
///
/// Used by:
///   - <c>HarmonyFunctions.Enharmonic</c> when called inside non-12-TET tuning (D-11).
///   - <c>MidiExport.WriteMidi</c> when called under non-12-TET tuning (D-13).
/// Phase 24 scaleLint and future render-time advisories may also reuse this helper.
///
/// Warning style mirrors <c>TransformFunctions.TransposeSemitone</c> (Console.Error.WriteLine),
/// with a HashSet-backed dedup wrapper so iterative REPL workflows don't flood the console.
/// </summary>
public static class RenderingDiagnostics
{
    private static readonly HashSet<string> _emitted = new(StringComparer.Ordinal);
    // Keys emitted by any session or the process since the last reset; test support only.
    private static readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private static readonly object _lock = new();

    /// <summary>
    /// Writes <paramref name="message"/> the FIRST time <paramref name="sentinelKey"/>
    /// is seen in the current session (or, outside a session, in this process).
    /// Thread-safe.
    /// </summary>
    public static void WarnOnce(string sentinelKey, string message)
    {
        lock (_lock) { _seen.Add(sentinelKey); }
        var session = Runtime.SessionServices.Current;
        if (session is not null)
        {
            session.WarnOnce(sentinelKey, message);
            return;
        }
        lock (_lock)
        {
            if (!_emitted.Add(sentinelKey)) return;
        }
        Console.Error.WriteLine(message);
    }

    /// <summary>
    /// Test-only: clears process-wide dedup state, and the current session's when
    /// called inside one.
    /// </summary>
    public static void ResetForTesting()
    {
        lock (_lock) { _emitted.Clear(); _seen.Clear(); }
        Runtime.SessionServices.Current?.ResetAdvisories();
    }

    /// <summary>
    /// Test-only: true if <paramref name="sentinelKey"/> was emitted by any session or
    /// the process since the last <see cref="ResetForTesting"/>.
    /// </summary>
    public static bool WasWarnedForTesting(string sentinelKey)
    {
        lock (_lock) { return _seen.Contains(sentinelKey); }
    }
}
