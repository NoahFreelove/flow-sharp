using FlowLang.Audio;
using FlowLang.Core;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;

namespace FlowInterpreter;

/// <summary>
/// Orchestrates file watching, background rendering, streaming playback, and
/// bar-boundary buffer swapping for live-coding mode.
///
/// The playback loop continuously streams audio in chunks via WriteChunk.
/// When a file change is detected, a background FlowEngine renders the new version
/// in capture mode. At the next bar boundary the buffer is atomically swapped.
/// </summary>
public sealed class LiveReloadManager : IDisposable
{
    private readonly string _filePath;
    private readonly string? _deviceName;

    // Audio buffers -- accessed from multiple threads
    private float[]? _currentBuffer;
    private float[]? _pendingBuffer;
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

    private const int ChunkSamples = 4096;
    private const int CrossfadeSamples = 64;

    public LiveReloadManager(string filePath, string? deviceName = null)
    {
        _filePath = Path.GetFullPath(filePath);
        _deviceName = deviceName;
    }

    /// <summary>
    /// Main entry point. Performs initial render, starts streaming loop,
    /// sets up file watcher, and blocks until cancelled.
    /// </summary>
    public void Run()
    {
        // 1. Initial execution with capture mode
        var initialBuffer = RenderScript(_filePath, out var musicalContext, out var errors);

        if (initialBuffer == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Initial execution failed: {errors}");
            Console.Error.WriteLine("Cannot start live reload without a valid audio buffer.");
            Console.ResetColor();
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

            // Bar boundary check: swap pending buffer if at a bar boundary
            var pending = Volatile.Read(ref _pendingBuffer);
            if (pending != null)
            {
                var (isAtBoundary, barNumber) = CheckBarBoundary(position);
                if (isAtBoundary)
                {
                    var newBuf = Interlocked.Exchange(ref _pendingBuffer, null);
                    if (newBuf != null)
                    {
                        // Apply micro-crossfade to prevent clicks
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

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Reloaded at bar {barNumber}");
                        Console.ResetColor();
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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Audio write error: {ex.Message}");
                Console.ResetColor();
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
    /// Debounces with 500ms minimum interval.
    /// </summary>
    private void TriggerBackgroundRender()
    {
        var now = DateTime.Now;
        if ((now - _lastChangeTime).TotalMilliseconds < 500)
            return;
        _lastChangeTime = now;

        // Allow file write to complete
        Thread.Sleep(100);

        Task.Run(() =>
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Change detected, re-rendering...");
                Console.ResetColor();

                var capturedBuffer = RenderScript(_filePath, out var musicalContext, out var errors);

                if (capturedBuffer == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    if (!string.IsNullOrEmpty(errors))
                        Console.Error.WriteLine($"Error -- keeping previous version: {errors}");
                    else
                        Console.Error.WriteLine("No audio output detected -- playback continues with previous version.");
                    Console.ResetColor();
                    return;
                }

                // Store the pending musical context
                Interlocked.Exchange(ref _pendingMusicalContext, musicalContext);

                // Atomically set the pending buffer for swap at next bar boundary
                Interlocked.Exchange(ref _pendingBuffer, capturedBuffer.Data);

                // Update sample rate/channels if they changed
                _currentSampleRate = capturedBuffer.SampleRate;
                _currentChannels = capturedBuffer.Channels;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error -- keeping previous version: {ex.Message}");
                Console.ResetColor();
            }
        });
    }

    /// <summary>
    /// Renders a script using a fresh FlowEngine in capture mode.
    /// Returns the captured AudioBuffer and extracted MusicalContext.
    /// </summary>
    private static AudioBuffer? RenderScript(string filePath, out MusicalContext? musicalContext, out string? errors)
    {
        musicalContext = null;
        errors = null;

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

        // Save the current static PlaybackFunctions manager reference before creating
        // a new engine (which overwrites it via RegisterAllImplementations)
        var savedManager = PlaybackFunctions.GetManager();

        using var engine = new FlowEngine();
        engine.AudioManager.CaptureMode = true;

        engine.Execute(source, filePath);

        // Restore the original manager so the streaming loop's play() still works
        if (savedManager != null)
            PlaybackFunctions.SetManager(savedManager);

        // Extract musical context from the execution
        musicalContext = engine.Context.GetMusicalContext();

        // Try to get captured buffer
        var buffer = engine.AudioManager.GetCapturedBuffer();

        if (buffer == null)
        {
            // Also try ExecuteExpression as fallback for scripts that return a Buffer
            var result = engine.ExecuteExpression(source, filePath);
            if (result?.Data is AudioBuffer audioBuf)
            {
                buffer = audioBuf;
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
    }
}
