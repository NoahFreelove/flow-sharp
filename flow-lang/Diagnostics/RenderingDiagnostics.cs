namespace FlowLang.Diagnostics;

/// <summary>
/// One-shot stderr warning channel with per-process per-sentinel-key deduplication.
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
    private static readonly object _lock = new();

    /// <summary>
    /// Writes <paramref name="message"/> to <see cref="Console.Error"/> the FIRST time
    /// <paramref name="sentinelKey"/> is seen in this process. Subsequent calls
    /// with the same key are no-ops. Thread-safe.
    /// </summary>
    public static void WarnOnce(string sentinelKey, string message)
    {
        lock (_lock)
        {
            if (!_emitted.Add(sentinelKey)) return;
        }
        Console.Error.WriteLine(message);
    }

    /// <summary>
    /// Test-only: clears the dedup set so [Collection]-serialized Facts can isolate
    /// without a process restart. Also used between sequential FlowEngineRunner runs
    /// in <c>WriteMidi_BytesUnchanged_UnderJI</c> per WARNING-4.
    ///
    /// Public visibility required for cross-assembly Facts (no InternalsVisibleTo
    /// configured — same convention as <see cref="StandardLibrary.Audio.EffectsFunctions"/>
    /// helpers exposed for testing).
    /// </summary>
    public static void ResetForTesting()
    {
        lock (_lock) { _emitted.Clear(); }
    }

    /// <summary>
    /// Test-only: returns <c>true</c> if <paramref name="sentinelKey"/> was
    /// recorded by <see cref="WarnOnce"/> at least once since the last
    /// <see cref="ResetForTesting"/> call. Consumed by Phase 38 Plan 38-03
    /// TimeoutRevertTests to verify the live-block timeout advisory's dedup
    /// sentinel landed at the locked <c>live-timeout:&lt;line&gt;</c> format
    /// per UI-SPEC line 330.
    /// </summary>
    public static bool WasWarnedForTesting(string sentinelKey)
    {
        lock (_lock) { return _emitted.Contains(sentinelKey); }
    }
}
