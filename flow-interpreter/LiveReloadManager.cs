using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowLang.Audio;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;

namespace FlowInterpreter;

/// <summary>
/// Per-block pending swap buffer. Phase 38 Plan 38-02 will populate this from
/// the <c>live { }</c> AST visitor with a real <see cref="BlockId"/>; Plan
/// 38-01 ships the orchestration with a sentinel <see cref="BlockId"/> = 0
/// for the whole-script swap path (D-38-01 default) so the streaming loop's
/// swap branch already iterates a dict, not a single field.
/// </summary>
/// <param name="BlockId">Identifier — 0 = whole-script swap (Plan 38-01); &gt;0 = per-<c>live { }</c>-block (Plan 38-02).</param>
/// <param name="Bytes">Raw PCM data ready to swap in at the next bar boundary.</param>
/// <param name="Length">Number of valid samples in <see cref="Bytes"/>.</param>
/// <param name="SampleRate">
/// Sample rate of <see cref="Bytes"/>. Audit-0609 §5.7: carried alongside the
/// buffer so the streaming loop can apply the new format AT the bar-boundary swap
/// rather than immediately after rendering (which caused the old buffer to play
/// at the wrong rate until the swap).
/// </param>
/// <param name="Channels">
/// Channel count of <see cref="Bytes"/>. Audit-0609 §5.7: same rationale as
/// <see cref="SampleRate"/> — applied atomically with the buffer swap.
/// </param>
/// <remarks>
/// Phase 38 Plan 38-03 LIVE-03 promoted this record from <c>internal</c> to
/// <c>public</c> so cross-assembly Wave 0 tests (PrngReseedAtSwapTests) can
/// construct synthetic buffer dicts at the <see cref="LiveReloadManager.StagePendingBuffers"/>
/// test seam without needing InternalsVisibleTo or reflection.
/// </remarks>
public sealed record LiveBlockBuffer(int BlockId, float[] Bytes, int Length, int SampleRate = 44100, int Channels = 2);

/// <summary>
/// Orchestrates file watching, background rendering, streaming playback, and
/// bar-boundary buffer swapping for live-coding mode.
///
/// The playback loop continuously streams audio in chunks via WriteChunk.
/// When a file change is detected, a background FlowEngine renders the new
/// version in capture mode. At the next bar boundary the buffer is atomically
/// swapped.
///
/// Phase 38 Plan 38-01 modernization (per CONTEXT D-38-05 / D-38-07 / D-38-08):
/// <list type="bullet">
/// <item><description>200ms debounce (down from 500ms — Pitfall #21).</description></item>
/// <item><description>30s CancellationToken-equivalent wall-clock cap on each
/// render via <c>Task.Run + Wait(TimeSpan)</c> (RESEARCH §E Option A; orphan
/// workers past 30s leak — accepted per D-38-07, T-38-22).</description></item>
/// <item><description>Per-block pending-buffer dict
/// (<see cref="LiveBlockBuffer"/>) replacing the prior single-field
/// <c>_pendingBuffer</c> — Plan 38-02 fills this with real
/// <c>live { }</c>-block ids; Plan 38-01 uses sentinel BlockId=0.</description></item>
/// <item><description>Console.ForegroundColor blocks replaced with
/// <see cref="LiveStatusPanel"/> PublishAdvisory calls using the locked
/// advisory wording from UI-SPEC §Advisory Catalog (lines 322-341).</description></item>
/// </list>
///
/// Phase 28/29/33 byte-identical determinism contract preserved for the
/// whole-script swap path: <see cref="CheckBarBoundary"/> + <see cref="ApplyCrossfade"/> +
/// <see cref="RenderScript"/> body are unchanged from the Phase 28 baseline
/// (the <see cref="RenderScript"/> signature gains a new <c>out</c> param
/// stubbed to <c>null</c> in Plan 38-01; Plan 38-02 will fill it).
///
/// Class is no longer <c>sealed</c> so Phase 38 Wave 0 xUnit tests can subclass
/// via the <see cref="OnRenderTriggered"/> testable seam to count debounce
/// coalescence without booting <see cref="FlowEngine"/>.
/// </summary>
public class LiveReloadManager : IDisposable
{
    /// <summary>
    /// File-watch debounce constant (D-38-05 LOCK / Pitfall #21). Down from
    /// the Phase 28 500ms baseline so composer edits feel snappier.
    /// </summary>
    public const int DebounceMs = 200;

    /// <summary>
    /// Wall-clock cap on a single live re-render (D-38-07). Workers that
    /// exceed this leak as orphans per RESEARCH §E Option A — accepted as a
    /// tractable v1.5 tradeoff against the cost of a true cooperative
    /// CancellationToken plumbing through <see cref="FlowEngine"/>.
    /// </summary>
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(30);

    private readonly string _filePath;
    private readonly string? _deviceName;

    // Audio buffers -- accessed from multiple threads
    private float[]? _currentBuffer;

    // Phase 38 D-38-02: per-block pending swap buffer dict.
    // Plan 38-01 uses BlockId=0 sentinel for the whole-script swap path
    // (replaces the prior single _pendingBuffer field at the line-23 slot).
    // Plan 38-02 will populate with real live{} block ids from the AST visitor.
    private Dictionary<int, LiveBlockBuffer>? _pendingPerBlock;
    private readonly object _pendingLock = new();
    private MusicalContext? _pendingMusicalContext;

    // Current playback state (from the playing version).
    // Audit-0609 §5.7: _currentSampleRate/_currentChannels are now written ONLY
    // from the streaming loop at the bar-boundary swap (not from the render thread)
    // so they stay coherent with the buffer being streamed. They are read from the
    // streaming thread only; the render thread writes them into LiveBlockBuffer.
    // Both are plain ints — int reads/writes are atomic on all .NET platforms
    // per ECMA-335 §I.12.6.6; no additional Volatile needed here.
    private double _currentTempo = 120.0;
    private int _currentBeatsPerBar = 4;
    private int _currentSampleRate = 44100;
    private int _currentChannels = 2;

    // Infrastructure
    private CancellationTokenSource? _cts;
    private Task? _playbackTask;
    private FileSystemWatcher? _watcher;

    // Audit-0609 D3: trailing-edge debounce state.
    // Each file-change event resets _debounceTimer; the render fires once
    // when the timer elapses (quiesces after the last event).
    // _lastChangeTime tracks the most recent event under _debounceLock
    // so the old leading-edge semantics for "was anything triggered?" is
    // preserved for tests that count render firings.
    private readonly object _debounceLock = new();
    private Timer? _debounceTimer;
    private DateTime _lastChangeTime = DateTime.MinValue;

    // Playback manager for the streaming loop (NOT capture mode)
    private AudioPlaybackManager? _streamingManager;

    // Phase 38 Plan 38-01: ANSI live status panel + structured stderr advisories.
    // Constructed lazily at Run() entry so test subclasses that never call
    // Run() (e.g. WatchDebounceTests) don't allocate a heartbeat Timer.
    private LiveStatusPanel? _panel;

    // Phase 38 Plan 38-03 LIVE-03: most-recent voices snapshot used by
    // PreserveVoiceState across live-block swaps. Populated from the
    // RenderScript output once VoiceAllocator surfaces the per-section voice
    // lists at the manager seam — for v1.5 this stays at null (the live-swap
    // staging path remains correct because DiffByVoiceName on (empty, next)
    // routes every next voice through the Added branch, identical to a
    // cold-start render). A future plan can populate _lastVoices from the
    // FlowEngine capture-mode pipeline to enable per-voice preservation
    // across whole-script swaps too.
    private IReadOnlyList<Voice>? _lastVoices;

    // Phase 38 Plan 38-03 D-38-04 file-scope-edit detection — tracks the
    // most-recent file text we successfully parsed so the FileSystemWatcher
    // changed-event handler can compute a line-range diff to detect edits
    // OUTSIDE any live { } block body.
    private string? _lastParsedSource;

    private const int ChunkSamples = 4096;
    private const int CrossfadeSamples = 64;

    public LiveReloadManager(string filePath, string? deviceName = null)
    {
        _filePath = Path.GetFullPath(filePath);
        _deviceName = deviceName;
    }

    /// <summary>
    /// Testable seam: subclasses override this to count debounce-gate firings
    /// without booting <see cref="FlowEngine"/>. The default implementation
    /// dispatches the real <see cref="StartRenderTask"/> path; tests skip the
    /// dispatch to keep the harness in pure-counting mode.
    /// </summary>
    protected virtual void OnRenderTriggered()
    {
        StartRenderTask();
    }

    /// <summary>
    /// Test-only hook: subclasses call this to drive the same debounce gate
    /// the real FileSystemWatcher Changed event uses. <c>protected internal</c>
    /// so cross-assembly test subclasses (flow-lang.Tests) reach it via
    /// inheritance.
    /// </summary>
    protected internal void InvokeTriggerForTesting()
    {
        TriggerBackgroundRender();
    }

    /// <summary>
    /// Main entry point. Performs initial render, starts streaming loop,
    /// sets up file watcher, and blocks until cancelled.
    /// </summary>
    public void Run()
    {
        // Plan 38-01: install LiveStatusPanel at Run() entry so all subsequent
        // advisories route through the structured panel surface.
        _panel = new LiveStatusPanel(cliArgs: Environment.GetCommandLineArgs());

        // 1. Initial execution with capture mode.
        // Audit-0609 D2-minimal: RenderScript now returns the engine so we can
        // call StagePendingBuffers (PRNG reset + stale-closure gate) before
        // disposing it. The initial render does not stage to _pendingPerBlock
        // (it sets _currentBuffer directly), so we just dispose the engine here.
        var initialBuffer = RenderScript(_filePath, out var musicalContext, out var errors, out _, out var initialEngine);
        initialEngine?.Dispose();

        if (initialBuffer == null)
        {
            _panel.PublishAdvisory(
                $"[live] initial execution failed: {errors ?? "no audio output detected"} — cannot start live reload",
                AdvisoryLevel.Error,
                dedupKey: $"live-init-fail:{_filePath}");
            return;
        }

        // Store initial state
        Volatile.Write(ref _currentBuffer, initialBuffer.Data);
        _currentSampleRate = initialBuffer.SampleRate;
        _currentChannels = initialBuffer.Channels;

        if (musicalContext != null)
        {
            _currentTempo = musicalContext.Tempo ?? 120.0;
            _currentBeatsPerBar = musicalContext.TimeSignature?.Numerator ?? 4;
        }

        // 2. Set up streaming playback manager
        _streamingManager = new AudioPlaybackManager();
        if (_deviceName != null && _streamingManager.IsAudioAvailable())
        {
            var backend = _streamingManager.GetBackend();
            backend.SetDevice(_deviceName);
        }

        // 3. Set up file watcher
        var directory = Path.GetDirectoryName(_filePath)!;
        var fileName = Path.GetFileName(_filePath);

        _watcher = new FileSystemWatcher(directory, fileName);
        _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
        _watcher.Changed += (_, _) => TriggerBackgroundRender();
        _watcher.Created += (_, _) => TriggerBackgroundRender();
        _watcher.Renamed += (_, _) => TriggerBackgroundRender();
        _watcher.EnableRaisingEvents = true;

        // 4. Start streaming loop
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _playbackTask = Task.Run(() => StreamingLoop(ct), ct);

        // 5. Set up Ctrl+C handler
        var exitRequested = false;
        Console.CancelKeyPress += (_, e) =>
        {
            if (!exitRequested)
            {
                e.Cancel = true;
                Console.WriteLine();
                Console.WriteLine("Stopping playback. Press Ctrl+C again to exit.");
                exitRequested = true;
                _cts?.Cancel();
            }
            // Second Ctrl+C: default behavior (exit)
        };

        Console.WriteLine($"Watching {fileName} for changes... (Ctrl+C to stop)");

        // Initial state publish so the panel populates row 1 + row 3 from
        // the cold-start musical context.
        PublishPanelState(barNumber: 1);

        // 6. Block until cancelled
        try
        {
            _playbackTask.Wait();
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException or OperationCanceledException))
        {
            // Normal cancellation
        }

        // Wait for exit
        while (!exitRequested)
        {
            Thread.Sleep(200);
        }

        Console.WriteLine("Live reload ended.");
    }

    /// <summary>
    /// Continuous streaming playback loop. Writes audio in chunks via WriteChunk,
    /// checks for pending buffer swaps at bar boundaries, and loops the buffer.
    /// </summary>
    private void StreamingLoop(CancellationToken ct)
    {
        var backend = _streamingManager!.GetBackend();
        backend.EnsureInitialized(_currentSampleRate, _currentChannels);

        int position = 0;

        while (!ct.IsCancellationRequested)
        {
            var buffer = Volatile.Read(ref _currentBuffer);
            if (buffer == null || buffer.Length == 0)
            {
                Thread.Sleep(10);
                continue;
            }

            // Bar boundary check: swap pending buffer(s) at a bar boundary.
            // Plan 38-01: iterate the per-block dict (with sentinel BlockId=0
            // for the whole-script swap path). Plan 38-02 will use real
            // per-block ids; for now we drain the dict whole and apply the
            // first/only buffer to _currentBuffer.
            LiveBlockBuffer? pendingDrain = null;
            lock (_pendingLock)
            {
                if (_pendingPerBlock != null && _pendingPerBlock.Count > 0)
                {
                    // Plan 38-01: only sentinel BlockId=0 is populated; take it.
                    var first = _pendingPerBlock.Values.First();
                    pendingDrain = first;
                }
            }

            if (pendingDrain != null)
            {
                var (isAtBoundary, barNumber) = CheckBarBoundary(position);
                if (isAtBoundary)
                {
                    // Consume the dict (whole-drain in Plan 38-01).
                    Dictionary<int, LiveBlockBuffer>? consumed;
                    lock (_pendingLock)
                    {
                        consumed = _pendingPerBlock;
                        _pendingPerBlock = null;
                    }

                    if (consumed != null && consumed.Count > 0)
                    {
                        var swapped = consumed.Values.First();
                        var newBuf = swapped.Bytes;

                        // Apply micro-crossfade to prevent clicks (Phase 28 primitive — preserved byte-identical).
                        ApplyCrossfade(buffer, position, newBuf, 0);

                        Volatile.Write(ref _currentBuffer, newBuf);
                        buffer = newBuf;

                        // Audit-0609 §5.7: apply SampleRate/Channels AT the swap
                        // boundary (not on the render thread) so WriteChunk and
                        // CheckBarBoundary always see a consistent format for the
                        // buffer they are currently streaming.
                        _currentSampleRate = swapped.SampleRate;
                        _currentChannels = swapped.Channels;

                        // Update tempo/timesig from pending context
                        var pendingCtx = Interlocked.Exchange(ref _pendingMusicalContext, null);
                        if (pendingCtx != null)
                        {
                            _currentTempo = pendingCtx.Tempo ?? _currentTempo;
                            _currentBeatsPerBar = pendingCtx.TimeSignature?.Numerator ?? _currentBeatsPerBar;
                        }

                        position = 0;

                        // Plan 38-01: replace Console.ForegroundColor block with
                        // structured PublishAdvisory using UI-SPEC §Advisory Catalog
                        // line 329 — `[live] block @L<N> swapped at bar <M>`.
                        _panel?.PublishAdvisory(
                            $"[live] block @L0 swapped at bar {barNumber}",
                            AdvisoryLevel.Success,
                            dedupKey: null);

                        PublishPanelState(barNumber);
                    }
                }
            }

            // Calculate chunk size
            int remaining = buffer.Length - position;
            if (remaining <= 0)
            {
                // Loop back to beginning
                position = 0;
                continue;
            }

            int chunkSize = Math.Min(ChunkSamples, remaining);

            try
            {
                backend.WriteChunk(buffer, position, chunkSize, _currentSampleRate, _currentChannels);
            }
            catch (InvalidOperationException ex)
            {
                _panel?.PublishAdvisory(
                    $"[live] audio write error: {ex.Message}",
                    AdvisoryLevel.Error,
                    dedupKey: $"live-write:{ex.GetType().Name}");
                Thread.Sleep(100);
                continue;
            }

            position += chunkSize;

            // Loop the buffer
            if (position >= buffer.Length)
            {
                position = 0;
            }
        }
    }

    /// <summary>
    /// Checks whether the given sample position is at (or within one chunk of) a bar boundary.
    /// Returns (isAtBoundary, barNumber).
    ///
    /// PRESERVED BYTE-IDENTICAL from the Phase 28 baseline (D-38-06).
    /// </summary>
    private (bool IsAtBoundary, int BarNumber) CheckBarBoundary(int samplePosition)
    {
        double secondsPerBeat = 60.0 / _currentTempo;
        double secondsPerBar = secondsPerBeat * _currentBeatsPerBar;
        int samplesPerBar = (int)(secondsPerBar * _currentSampleRate) * _currentChannels;

        if (samplesPerBar <= 0)
            return (true, 1);

        int barNumber = samplePosition / samplesPerBar + 1;
        int positionInBar = samplePosition % samplesPerBar;

        // Within one chunk of the bar boundary
        bool isAtBoundary = positionInBar < ChunkSamples;
        return (isAtBoundary, barNumber);
    }

    /// <summary>
    /// Applies a micro-crossfade between the tail of the old buffer and the start of the new buffer
    /// to prevent audible clicks at the swap point. Modifies newBuffer in-place.
    ///
    /// PRESERVED BYTE-IDENTICAL from the Phase 28 baseline (D-38-06) — both
    /// whole-script swap (Plan 38-01) and per-live-block swap (Plan 38-02)
    /// paths consume this same 64-sample equal-power crossfade.
    /// </summary>
    private static void ApplyCrossfade(float[] oldBuffer, int oldPosition, float[] newBuffer, int newPosition)
    {
        int fadeLength = Math.Min(CrossfadeSamples, newBuffer.Length - newPosition);
        int oldRemaining = oldBuffer.Length - oldPosition;
        fadeLength = Math.Min(fadeLength, oldRemaining);

        if (fadeLength <= 0)
            return;

        for (int i = 0; i < fadeLength; i++)
        {
            float t = (float)i / fadeLength; // 0.0 -> 1.0
            float oldSample = oldBuffer[oldPosition + i];
            float newSample = newBuffer[newPosition + i];
            // Crossfade: fade out old, fade in new
            newBuffer[newPosition + i] = oldSample * (1.0f - t) + newSample * t;
        }
    }

    /// <summary>
    /// Triggers a background render of the script. Called when file changes are detected.
    ///
    /// Audit-0609 D3: trailing-edge debounce (overrides D-38-05 leading-edge
    /// LOCK per owner approval 2026-06-09). Each call RESETS a
    /// <see cref="DebounceMs"/>-ms System.Threading.Timer; the render fires
    /// exactly once, after the burst quiesces. This ensures that a
    /// format-on-save or atomic temp-file-rename editor's FINAL write is
    /// always rendered rather than silently dropped.
    ///
    /// State (_lastChangeTime + _debounceTimer) is guarded by _debounceLock
    /// since FileSystemWatcher fires from thread-pool threads.
    /// </summary>
    private void TriggerBackgroundRender()
    {
        lock (_debounceLock)
        {
            _lastChangeTime = DateTime.Now;

            if (_debounceTimer == null)
            {
                // First event in a burst — create the restartable timer.
                _debounceTimer = new Timer(
                    _ => FireRenderAfterDebounce(),
                    state: null,
                    dueTime: DebounceMs,
                    period: Timeout.Infinite);
            }
            else
            {
                // Subsequent event — restart the timer (trailing-edge reset).
                _debounceTimer.Change(DebounceMs, Timeout.Infinite);
            }
        }
    }

    /// <summary>
    /// Called by the debounce timer after the burst quiesces. Disposes the
    /// one-shot timer so the next burst allocates fresh, then forwards to
    /// <see cref="OnRenderTriggered"/>.
    /// </summary>
    private void FireRenderAfterDebounce()
    {
        lock (_debounceLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }

        // Plan 38-01: testable seam. Real callers go through OnRenderTriggered
        // which dispatches StartRenderTask; test subclasses override
        // OnRenderTriggered to count without booting FlowEngine.
        OnRenderTriggered();
    }

    /// <summary>
    /// Dispatches the actual render work on a background task, wrapped in the
    /// 30s wall-clock cap per D-38-07 / RESEARCH §E Option A.
    /// </summary>
    private void StartRenderTask()
    {
        // Allow file write to complete
        Thread.Sleep(100);

        Task.Run(() =>
        {
            // Audit-0609 D2-minimal: declare renderEngine outside the try block
            // so the catch block can dispose it on unexpected exceptions.
            FlowEngine? renderEngine = null;

            try
            {
                _panel?.PublishAdvisory(
                    "[watch] change detected, re-rendering...",
                    AdvisoryLevel.Info,
                    dedupKey: null);

                AudioBuffer? capturedBuffer = null;
                MusicalContext? musicalContext = null;
                string? errors = null;
                Dictionary<int, LiveBlockBuffer>? perBlockBuffers = null;

                // RESEARCH §E Option A: 30s wall-clock cap via Task.Run + Wait.
                // Workers that exceed 30s leak as orphans — acceptable for v1.5
                // per D-38-07; Plan 38-XX may revisit if HUMAN-UAT reports
                // worker accumulation (T-38-22 documented).
                //
                // Audit-0609 D2-minimal: RenderScript now returns the live engine
                // (ownership transferred to caller). The engine is disposed after
                // StagePendingBuffers is called below.
                var workerTask = Task.Run(() =>
                {
                    capturedBuffer = RenderScript(_filePath, out musicalContext, out errors, out perBlockBuffers, out renderEngine);
                });

                if (!workerTask.Wait(RenderTimeout))
                {
                    // Phase 38 Plan 38-03 LIVE-02 — Timeout-revert path.
                    // Aligns wording / level / dedup key with UI-SPEC line 330:
                    //   body: "[live] evaluation timed out at 30s at line N — keeping previous version"
                    //   level: Error (red, UI-SPEC line 99 destructive)
                    //   dedup: "live-timeout:<line>"
                    // The worker continues running in the background as an
                    // orphan per RESEARCH §E Option A. KEEP previous buffer
                    // (no swap) — Pitfall #12 "live session never dies
                    // mid-set" lock.
                    //
                    // Line-N for the timed-out render — we don't know which
                    // specific live { } block hung (the worker's already
                    // detached), so we report line 1 as the file-scope
                    // anchor. Future plans can thread per-block timeout
                    // tracking; this v1.5 cut emits the locked wording at
                    // the documented dedup format.
                    // Audit-0609 D2-minimal: engine leaked as orphan on timeout
                    // (same as the orphan worker — accepted per D-38-07).
                    PublishTimeoutAdvisory(line: 1);
                    return;
                }

                if (capturedBuffer == null)
                {
                    // Audit-0609 D2-minimal: dispose engine on non-null render
                    // failure (parse error / no audio output).
                    renderEngine?.Dispose();
                    renderEngine = null;

                    var msg = !string.IsNullOrEmpty(errors)
                        ? $"[live] {errors} — keeping previous version"
                        : "[live] no audio output detected — keeping previous version";
                    _panel?.PublishAdvisory(
                        msg,
                        AdvisoryLevel.Error,
                        dedupKey: $"live-parse:{_filePath}");
                    return;
                }

                // Store the pending musical context
                Interlocked.Exchange(ref _pendingMusicalContext, musicalContext);

                // Plan 38-01: wrap the single captured buffer in a dict with
                // sentinel BlockId=0 (whole-script swap mode per D-38-01).
                // Plan 38-02 will pass perBlockBuffers through unchanged when
                // the AST visitor produces a non-null dict.
                //
                // Audit-0609 §5.7: SampleRate + Channels are carried INSIDE the
                // LiveBlockBuffer so they are applied atomically in the streaming
                // loop at the bar-boundary swap — NOT here on the render thread.
                // Applying them here (as the original code did) caused the old
                // buffer to stream at the wrong format until the swap fired.
                var swap = new Dictionary<int, LiveBlockBuffer>
                {
                    [0] = new LiveBlockBuffer(
                        BlockId: 0,
                        Bytes: capturedBuffer.Data,
                        Length: capturedBuffer.Data.Length,
                        SampleRate: capturedBuffer.SampleRate,
                        Channels: capturedBuffer.Channels),
                };
                // NOTE: _currentSampleRate / _currentChannels intentionally NOT
                // updated here — they are updated at the bar-boundary swap below
                // (Audit-0609 §5.7 fix).

                // Audit-0609 D2-minimal: route through StagePendingBuffers so
                // the whole-script swap path fires PrngRegistry.ResetAtRenderBoundary
                // exactly once per swap and runs the stale-closure gate (a no-op
                // for sentinel BlockId=0 since it has no LiveBlockRegistration).
                // StagePendingBuffers owns the _pendingPerBlock write; we no
                // longer set it directly.
                if (renderEngine != null)
                {
                    var emptyBlocks = renderEngine.Context.LiveBlockRegistry.Snapshot();
                    StagePendingBuffers(swap, renderEngine, emptyBlocks);
                    renderEngine.Dispose();
                    renderEngine = null;
                }
                else
                {
                    // Fallback: engine was null (shouldn't happen; defensive path).
                    lock (_pendingLock)
                    {
                        _pendingPerBlock = swap;
                    }
                }
            }
            catch (Exception ex)
            {
                // Audit-0609 D2-minimal: dispose engine on unexpected exception.
                renderEngine?.Dispose();
                _panel?.PublishAdvisory(
                    $"[live] {ex.Message} — keeping previous version",
                    AdvisoryLevel.Error,
                    dedupKey: $"live-exception:{ex.GetType().Name}");
            }
        });
    }

    /// <summary>
    /// Pushes the current playback state to <see cref="_panel"/>. Plan 38-01
    /// ships the whole-script swap path so the live-blocks list is empty;
    /// Plan 38-02 will populate from the per-block dict.
    /// </summary>
    private void PublishPanelState(int barNumber)
    {
        if (_panel == null) return;

        // Plan 38-01: voice introspection deferred to Plan 38-03 (which adds
        // VoiceAllocator hook); for now publish 0/32 + empty instrument map.
        _panel.PublishState(
            tempo: _currentTempo,
            timesig: (_currentBeatsPerBar, 4),
            bar: barNumber,
            blocks: Array.Empty<LiveBlockDisplay>(),
            activeVoices: 0,
            poolSize: 32,
            perInstrumentCount: new Dictionary<string, int>());
    }

    /// <summary>
    /// Phase 38 Plan 38-03 LIVE-03 — per-block buffer staging consumer.
    /// Called after a successful background render: walks each new live-block
    /// registration body via <see cref="LambdaCaptureAuditor.CollectFileScopeReferences"/>
    /// to detect stale closures, fires the <c>live-stale-closure</c> advisory
    /// + SKIPS that block's swap when found, calls
    /// <see cref="PrngRegistry.ResetAtRenderBoundary"/> ONCE per RESEARCH §D
    /// line 770, and populates <see cref="_pendingPerBlock"/> with the
    /// surviving buffers for the streaming loop to drain at the next bar
    /// boundary.
    ///
    /// <para>
    /// Pitfall #12 lock: any per-block failure (stale closure) DOES NOT
    /// abort the whole swap — the surviving blocks still stage. The
    /// previous buffer for the failed block keeps playing.
    /// </para>
    /// </summary>
    /// <param name="newBuffers">Per-block buffers freshly rendered by the
    /// background task. Keys are <see cref="LiveBlockRegistration.BlockId"/>;
    /// sentinel <c>0</c> is the whole-script swap path from Plan 38-01.</param>
    /// <param name="engine">The FlowEngine that rendered <paramref name="newBuffers"/>.
    /// Its <see cref="ExecutionContext.LiveBlockRegistry"/> and
    /// <see cref="ExecutionContext.PrngRegistry"/> are consumed by the staging
    /// gate.</param>
    /// <param name="newBlocks">Snapshot of the engine's LiveBlockRegistry
    /// captured at render-time. Passed in (rather than re-snapshotted here)
    /// so test seams can drive synthetic snapshots without booting an
    /// engine.</param>
    /// <remarks>
    /// Audit-0609 D2-minimal: <c>virtual</c> so test subclasses can intercept
    /// the call to verify the production dispatch path (via
    /// <c>D2MinimalPrngReseedTests.D2MinimalHarness</c>) without needing
    /// InternalsVisibleTo or reflection.
    /// </remarks>
    protected virtual void StagePendingBuffers(
        Dictionary<int, LiveBlockBuffer> newBuffers,
        FlowEngine engine,
        IReadOnlyDictionary<int, LiveBlockRegistration> newBlocks)
    {
        if (newBuffers == null) throw new ArgumentNullException(nameof(newBuffers));
        if (engine == null) throw new ArgumentNullException(nameof(engine));
        if (newBlocks == null) throw new ArgumentNullException(nameof(newBlocks));

        // Per-block stale-closure gate. Each block's body is audited; any
        // captured reference not present at file scope triggers the advisory
        // and removes that block's buffer from the staging dict (its
        // previous-pass buffer keeps playing per Pitfall #12).
        var surviving = new Dictionary<int, LiveBlockBuffer>();
        foreach (var (blockId, buffer) in newBuffers)
        {
            // The sentinel BlockId=0 (Plan 38-01 whole-script swap path) has
            // no LiveBlockRegistration entry — there's no body to audit.
            // Stage it unconditionally.
            if (!newBlocks.TryGetValue(blockId, out var registration))
            {
                surviving[blockId] = buffer;
                continue;
            }

            // Walk the live-block body for file-scope references that aren't
            // in the engine's global frame. Any miss = stale closure.
            var refs = FlowLang.Interpreter.LambdaCaptureAuditor.CollectFileScopeReferences(registration.Body);
            string? staleName = null;
            foreach (var name in refs)
            {
                if (!engine.Context.GlobalFrame.HasVariable(name)
                    && !engine.Context.GlobalFrame.HasFunction(name))
                {
                    staleName = name;
                    break;
                }
            }

            if (staleName != null)
            {
                _panel?.PublishAdvisory(
                    $"[live] stale closure: references removed binding '{staleName}' at line {registration.Location.Line} — keeping previous version",
                    AdvisoryLevel.Error,
                    dedupKey: $"live-stale-closure:{staleName}:{registration.Location.Line}");
                // SKIP staging — the previous buffer for this block keeps
                // playing (Pitfall #12 lock).
                continue;
            }

            surviving[blockId] = buffer;
        }

        // PRNG reseed at the swap boundary — fires ONCE per swap regardless
        // of how many blocks survived (RESEARCH §D line 770). Matches the
        // Phase 36 Plan 36-01 contract every other render path obeys.
        engine.Context.PrngRegistry.ResetAtRenderBoundary();

        // Stage the surviving buffers — the streaming loop will drain
        // _pendingPerBlock at the next bar boundary via CheckBarBoundary.
        lock (_pendingLock)
        {
            _pendingPerBlock = surviving.Count > 0 ? surviving : null;
        }
    }

    /// <summary>
    /// Phase 38 Plan 38-03 LIVE-03 — per-voice preservation across live-block
    /// swaps. Partitions <paramref name="prevVoices"/> + <paramref name="nextVoices"/>
    /// via <see cref="VoiceAllocator.DiffByVoiceName"/>; calls
    /// <see cref="Voice.CopyStateFrom"/> on each Preserved entry (the new
    /// instance receives the previous OffsetBeats so the composer hears no
    /// envelope retrigger), and calls <see cref="VoiceAllocator.ApplyFadeOut"/>
    /// on each Dropped entry for a clean tail.
    ///
    /// <para>
    /// Per RESEARCH §F lines 530-535 this is the single mutation site for
    /// voice state across the live-swap path. Added voices need no
    /// processing — they enter the next render fresh.
    /// </para>
    /// </summary>
    protected void PreserveVoiceState(
        IReadOnlyList<Voice> prevVoices,
        IReadOnlyList<Voice> nextVoices,
        int sampleRate)
    {
        if (prevVoices == null || nextVoices == null) return;

        var (preserved, dropped, _) = VoiceAllocator.DiffByVoiceName(prevVoices, nextVoices);

        // Build prev-name → voice map once so CopyStateFrom doesn't re-scan
        // the prev list per preserved entry.
        var prevByName = new Dictionary<string, Voice>(StringComparer.Ordinal);
        for (int i = 0; i < prevVoices.Count; i++)
        {
            var v = prevVoices[i];
            if (!string.IsNullOrEmpty(v.Name))
                prevByName[v.Name] = v;
        }

        for (int i = 0; i < preserved.Count; i++)
        {
            var nextVoice = preserved[i];
            if (prevByName.TryGetValue(nextVoice.Name, out var prevVoice))
            {
                nextVoice.CopyStateFrom(prevVoice);
            }
        }

        for (int i = 0; i < dropped.Count; i++)
        {
            VoiceAllocator.ApplyFadeOut(dropped[i], sampleRate);
        }
    }

    /// <summary>
    /// Phase 38 Plan 38-03 D-38-04 — file-scope-edit detection. Compares the
    /// just-parsed source against <see cref="_lastParsedSource"/>; finds the
    /// first changed line; if the changed line falls OUTSIDE any active live
    /// block's body line range, fires the locked advisory wording per UI-SPEC
    /// line 334 with dedup <c>live-fscope-edit:&lt;filepath&gt;:&lt;line&gt;</c>.
    /// Does NOT auto-restart — Pitfall #12 "live session never dies mid-set"
    /// lock.
    /// </summary>
    /// <param name="filePath">Path to the edited file (used in dedup key).</param>
    /// <param name="newSource">The just-read source text post-edit.</param>
    /// <param name="activeBlocks">Snapshot of the engine's LiveBlockRegistry —
    /// the per-block <see cref="LiveBlockRegistration.Location"/> defines the
    /// start of each live-block body's source range.</param>
    protected void DetectFileScopeEdit(
        string filePath,
        string newSource,
        IReadOnlyDictionary<int, LiveBlockRegistration> activeBlocks)
    {
        if (string.IsNullOrEmpty(newSource)) return;

        var prevSource = _lastParsedSource;
        // First parse — record and skip (no prior text to diff against).
        if (prevSource == null)
        {
            _lastParsedSource = newSource;
            return;
        }
        if (prevSource == newSource)
        {
            return; // No change (debounce coalesced an identical save).
        }

        // Find the first 1-indexed line where the two sources differ.
        int firstChangedLine = FindFirstChangedLine(prevSource, newSource);
        _lastParsedSource = newSource;

        if (firstChangedLine < 1) return; // both files identical-with-trailing-nl, etc.

        // Determine whether the changed line is INSIDE any active live-block
        // body. For v1.5 we approximate the body range as [Location.Line + 1,
        // Location.Line + Body.Count + 1] since each statement typically
        // occupies one line. Future plans can thread per-statement line
        // ranges through LiveBlockStatement; the v1.5 heuristic is sufficient
        // for the composer's "I edited outside any live block" advisory.
        bool insideLiveBlock = false;
        foreach (var (_, reg) in activeBlocks)
        {
            int start = reg.Location.Line;
            int end = reg.Location.Line + Math.Max(1, reg.Body?.Count ?? 0) + 1;
            if (firstChangedLine >= start && firstChangedLine <= end)
            {
                insideLiveBlock = true;
                break;
            }
        }

        if (insideLiveBlock) return;

        // Fire the dedup'd advisory — yellow per UI-SPEC line 334.
        _panel?.PublishAdvisory(
            $"[live] file-scope edit detected outside live blocks at line {firstChangedLine} — restart 'flow watch' to apply",
            AdvisoryLevel.Warning,
            dedupKey: $"live-fscope-edit:{filePath}:{firstChangedLine}");
    }

    /// <summary>
    /// Returns the 1-indexed line number of the first line that differs
    /// between <paramref name="a"/> and <paramref name="b"/>; returns 0 if
    /// the two strings are equal or differ only in trailing newlines.
    /// </summary>
    private static int FindFirstChangedLine(string a, string b)
    {
        var linesA = a.Split('\n');
        var linesB = b.Split('\n');
        int min = Math.Min(linesA.Length, linesB.Length);
        for (int i = 0; i < min; i++)
        {
            if (linesA[i] != linesB[i]) return i + 1;
        }
        // One file has additional lines beyond the common prefix.
        if (linesA.Length != linesB.Length) return min + 1;
        return 0;
    }

    /// <summary>
    /// Phase 38 Plan 38-03 LIVE-02 — timeout-revert advisory per UI-SPEC line
    /// 330. Encapsulates the locked wording / level / dedup key so the
    /// StartRenderTask timeout branch and the test seam share one source of
    /// truth.
    /// </summary>
    /// <param name="line">Source line tagged in the advisory body + dedup
    /// key. v1.5 uses line 1 from the timeout branch (the worker has
    /// detached; per-block line tracking ships in a future plan).</param>
    protected void PublishTimeoutAdvisory(int line)
    {
        _panel?.PublishAdvisory(
            $"[live] evaluation timed out at 30s at line {line} — keeping previous version",
            AdvisoryLevel.Error,
            dedupKey: $"live-timeout:{line}");
    }

    /// <summary>
    /// Test-only seam: installs a <see cref="LiveStatusPanel"/> instance so
    /// test subclasses that don't call <see cref="Run"/> can still drive
    /// PublishAdvisory paths. Mirrors the WatchDebounceTests
    /// <c>CountingLiveReloadHarness</c> seam pattern.
    /// </summary>
    protected void InitPanelForTesting()
    {
        _panel ??= new LiveStatusPanel(cliArgs: Array.Empty<string>());
    }

    /// <summary>
    /// Renders a script using a fresh FlowEngine in capture mode.
    /// Returns the captured AudioBuffer and extracted MusicalContext.
    ///
    /// Plan 38-01: signature grew by a new <paramref name="perBlockBuffers"/>
    /// <c>out</c> parameter (RESEARCH §F line 500) — Plan 38-02 fills it from
    /// the <c>live { }</c> AST visitor; Plan 38-01 always emits <c>null</c>
    /// (the orchestration wraps the captured buffer in a sentinel BlockId=0
    /// dict on its own per D-38-01 whole-script swap).
    ///
    /// Audit-0609 D2-minimal: the <paramref name="engineOut"/> <c>out</c>
    /// parameter returns the live (not-yet-disposed) FlowEngine so the caller
    /// can pass it to <see cref="StagePendingBuffers"/> — which fires
    /// <see cref="PrngRegistry.ResetAtRenderBoundary"/> + the stale-closure gate —
    /// before disposing it. The caller MUST dispose <paramref name="engineOut"/>
    /// after staging; this method no longer disposes it internally.
    ///
    /// BODY PRESERVED BYTE-IDENTICAL from the Phase 28 baseline (D-38-06) —
    /// only the signature and disposal responsibility change.
    /// </summary>
    private static AudioBuffer? RenderScript(
        string filePath,
        out MusicalContext? musicalContext,
        out string? errors,
        out Dictionary<int, LiveBlockBuffer>? perBlockBuffers,
        out FlowEngine? engineOut)
    {
        musicalContext = null;
        errors = null;
        perBlockBuffers = null; // Plan 38-02 will fill from live{} AST visitor.
        engineOut = null;

        string source;
        try
        {
            source = File.ReadAllText(filePath);
        }
        catch (IOException ex)
        {
            errors = $"Could not read file: {ex.Message}";
            return null;
        }

        // Audit-0609 D2-minimal: engine is NOT wrapped in a using block here;
        // ownership is transferred to the caller (StartRenderTask or the initial
        // Run() path) which disposes it after calling StagePendingBuffers.
        var engine = new FlowEngine();
        engine.AudioManager.CaptureMode = true;

        engine.Execute(source, filePath);

        // Audit-0609 §5.8: populate errors from the reporter when execute fails.
        // Before this fix, parse/eval failures left errors == null and the caller
        // showed "no audio output detected" with no line number — useless in a
        // save-listen iteration loop.  All other front-ends (ScriptRunner, Repl,
        // CheckCommand) format the reporter the same way via Program.FormatErrorsForEmit.
        if (engine.ErrorReporter.HasErrors)
        {
            errors = Program.FormatErrorsForEmit(engine);
        }

        // Extract musical context from the execution
        musicalContext = engine.Context.GetMusicalContext();

        // Try to get captured buffer
        var buffer = engine.AudioManager.GetCapturedBuffer();

        if (buffer == null)
        {
            // Fallback: check if the last evaluated expression produced a Buffer
            // We already ran engine.Execute(source, filePath), so we just read the result
            if (!engine.ErrorReporter.HasErrors)
            {
                var result = engine.GetLastExpressionResult();
                if (result?.Data is AudioBuffer audioBuf)
                {
                    buffer = audioBuf;
                }
            }
        }

        // Transfer ownership to caller — they call StagePendingBuffers then Dispose.
        engineOut = engine;
        return buffer;
    }

    public void Dispose()
    {
        _cts?.Cancel();

        try
        {
            _playbackTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Best effort
        }

        // Dispose the trailing-edge debounce timer (Audit-0609 D3).
        lock (_debounceLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }

        _watcher?.Dispose();
        _cts?.Dispose();
        _streamingManager?.Dispose();
        _panel?.Dispose();
    }
}
