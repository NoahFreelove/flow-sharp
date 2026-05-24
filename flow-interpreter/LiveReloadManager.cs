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
internal sealed record LiveBlockBuffer(int BlockId, float[] Bytes, int Length);

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

    // Current playback state (from the playing version)
    private double _currentTempo = 120.0;
    private int _currentBeatsPerBar = 4;
    private int _currentSampleRate = 44100;
    private int _currentChannels = 2;

    // Infrastructure
    private CancellationTokenSource? _cts;
    private Task? _playbackTask;
    private FileSystemWatcher? _watcher;
    private DateTime _lastChangeTime = DateTime.MinValue;

    // Playback manager for the streaming loop (NOT capture mode)
    private AudioPlaybackManager? _streamingManager;

    // Phase 38 Plan 38-01: ANSI live status panel + structured stderr advisories.
    // Constructed lazily at Run() entry so test subclasses that never call
    // Run() (e.g. WatchDebounceTests) don't allocate a heartbeat Timer.
    private LiveStatusPanel? _panel;

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

        // 1. Initial execution with capture mode
        var initialBuffer = RenderScript(_filePath, out var musicalContext, out var errors, out _);

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
                        var newBuf = consumed.Values.First().Bytes;

                        // Apply micro-crossfade to prevent clicks (Phase 28 primitive — preserved byte-identical).
                        ApplyCrossfade(buffer, position, newBuf, 0);

                        Volatile.Write(ref _currentBuffer, newBuf);
                        buffer = newBuf;

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
    /// Debounces with <see cref="DebounceMs"/> minimum interval (D-38-05 — 200ms).
    /// </summary>
    private void TriggerBackgroundRender()
    {
        var now = DateTime.Now;
        if ((now - _lastChangeTime).TotalMilliseconds < DebounceMs)
            return;
        _lastChangeTime = now;

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
                var workerTask = Task.Run(() =>
                {
                    capturedBuffer = RenderScript(_filePath, out musicalContext, out errors, out perBlockBuffers);
                });

                if (!workerTask.Wait(RenderTimeout))
                {
                    // Timeout: dispatch a Warning-level advisory + KEEP previous
                    // buffer (no swap). The worker continues running in the
                    // background as an orphan — see Option A comment above.
                    _panel?.PublishAdvisory(
                        $"[live] evaluation timed out at 30s — keeping previous version",
                        AdvisoryLevel.Warning,
                        dedupKey: $"live-timeout:{_filePath}");
                    return;
                }

                if (capturedBuffer == null)
                {
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
                var swap = new Dictionary<int, LiveBlockBuffer>
                {
                    [0] = new LiveBlockBuffer(
                        BlockId: 0,
                        Bytes: capturedBuffer.Data,
                        Length: capturedBuffer.Data.Length),
                };
                lock (_pendingLock)
                {
                    _pendingPerBlock = swap;
                }

                // Update sample rate/channels if they changed
                _currentSampleRate = capturedBuffer.SampleRate;
                _currentChannels = capturedBuffer.Channels;
            }
            catch (Exception ex)
            {
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
    /// Renders a script using a fresh FlowEngine in capture mode.
    /// Returns the captured AudioBuffer and extracted MusicalContext.
    ///
    /// Plan 38-01: signature grew by a new <paramref name="perBlockBuffers"/>
    /// <c>out</c> parameter (RESEARCH §F line 500) — Plan 38-02 fills it from
    /// the <c>live { }</c> AST visitor; Plan 38-01 always emits <c>null</c>
    /// (the orchestration wraps the captured buffer in a sentinel BlockId=0
    /// dict on its own per D-38-01 whole-script swap).
    ///
    /// BODY PRESERVED BYTE-IDENTICAL from the Phase 28 baseline (D-38-06) —
    /// only the signature grows.
    /// </summary>
    private static AudioBuffer? RenderScript(
        string filePath,
        out MusicalContext? musicalContext,
        out string? errors,
        out Dictionary<int, LiveBlockBuffer>? perBlockBuffers)
    {
        musicalContext = null;
        errors = null;
        perBlockBuffers = null; // Plan 38-02 will fill from live{} AST visitor.

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

        using var engine = new FlowEngine();
        engine.AudioManager.CaptureMode = true;

        engine.Execute(source, filePath);

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

        _watcher?.Dispose();
        _cts?.Dispose();
        _streamingManager?.Dispose();
        _panel?.Dispose();
    }
}
