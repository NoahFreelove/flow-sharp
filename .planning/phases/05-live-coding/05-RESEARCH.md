# Phase 5: Live Coding - Research

**Researched:** 2026-04-02
**Domain:** Real-time audio playback with beat-synced hot reload, .NET threading, PulseAudio streaming
**Confidence:** HIGH

## Summary

This phase transforms the existing `--watch` mode from a stop-and-restart mechanism into a seamless live-coding environment where file saves trigger bar-boundary-quantized audio swaps. The core challenge is architectural: the current playback model is "render entire buffer, write to PulseAudio, drain, done" -- a one-shot blocking design. Live coding requires continuous streaming playback with the ability to atomically swap the source buffer at bar boundaries while the audio output loop continues uninterrupted.

The existing codebase provides strong foundations: `FlowEngine` is self-contained and can be instantiated independently for background rendering, `FileSystemWatcher` with debounce is already implemented in `RunWithWatch`, and `MusicalContext` provides tempo/time-signature data needed for bar boundary calculation. The main new work is (1) a continuous playback loop that reads from a swappable buffer reference, (2) bar-boundary timing logic, and (3) a `LiveReloadManager` class that orchestrates the file-watch-render-swap pipeline.

**Primary recommendation:** Build a `LiveReloadManager` in `flow-interpreter` that owns a continuous playback loop (writing audio in chunks via PulseAudio), a `volatile` or `Interlocked`-swapped buffer reference, and a background `Task` for re-execution. The playback loop tracks its position in samples and checks for a pending buffer swap at each bar boundary.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Reload triggers on file save (existing `FileSystemWatcher` with 500ms debounce).
- **D-02:** New version takes effect at the **next bar boundary** -- the most musically natural transition point. Current bar finishes playing, then new audio starts at bar N+1.
- **D-03:** Bar boundary calculation uses tempo and time signature from the currently-playing version's `MusicalContext`. Latency is 0 to (beats-per-bar - 1) beats depending on where in the bar the save occurs.
- **D-04:** Pre-render + swap strategy: while old version plays its final bar(s), the new version is parsed, executed, and rendered to a complete audio buffer in a **background thread**.
- **D-05:** At the bar boundary, the playback system atomically swaps the buffer pointer from old to new. Zero gap, no crossfade needed.
- **D-06:** The new version always starts playback from bar 1 (beginning of the rendered buffer). No attempt to resume from the "same position" in the song -- the user edits represent a new composition.
- **D-07:** If the new file has syntax or runtime errors, playback **continues with the last valid version**. The error is printed to terminal with line/column info.
- **D-08:** No playback interruption on error -- the user fixes the error, saves again, and the next valid version swaps in normally.
- **D-09:** Terminal output distinguishes reload success vs error: success shows "Reloaded at bar N", error shows the error message with a clear indicator that old version continues.
- **D-10:** Full re-execution on every reload -- the entire script is re-run from scratch. No incremental/section-only diffing.
- **D-11:** No state persists between reloads. Each reload is a clean execution.
- **D-12:** The background thread gets its own `FlowEngine` instance to avoid thread-safety issues with the main execution context.
- **D-13:** The live reload system lives in `flow-interpreter` (not `flow-lang`) since it orchestrates the engine, not the language runtime.
- **D-14:** Existing `RunWithWatch` in `Program.cs` is refactored into a `LiveReloadManager` class that encapsulates the watch loop, background rendering, bar-boundary timing, and buffer swapping.
- **D-15:** `AudioPlaybackManager` needs a method to swap the playback buffer atomically (thread-safe). This is the one change needed in `flow-lang`.

### Claude's Discretion
- Exact threading model (Task-based vs dedicated thread for background rendering)
- Whether to use `Interlocked.Exchange` or a lock for the buffer swap
- How to calculate remaining time until next bar boundary from the playback position
- Whether `LiveReloadManager` exposes events/callbacks for the terminal UI or handles it directly
- Crossfade duration (if any small crossfade is needed to avoid zero-crossing clicks)

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| LIVE-01 | Watch mode reloads code at bar boundaries (beat-synced) during playback | Bar boundary calculation from tempo/timesig, continuous playback loop with swap points, LiveReloadManager architecture |
| LIVE-02 | Live reload preserves playback state (does not restart from beginning) | Continuous streaming playback loop that never stops; atomic buffer swap at bar boundary; old buffer plays to completion of current bar |
</phase_requirements>

## Architecture Patterns

### Current Playback Architecture (What Must Change)

The current `Play()` method in `PulseAudioSimpleBackend` is **one-shot blocking**: it receives a complete `float[]` buffer, pins it, writes it to PulseAudio in 4096-sample chunks, then drains and returns. The `loop()` built-in calls `Play()` repeatedly in a while loop. The `RunWithWatch` method calls `engine.StopAudio()` (which flushes PulseAudio) then re-executes from scratch -- causing an audible gap.

```
CURRENT: File change -> StopAudio() -> [GAP] -> Execute() -> play(buffer) -> done
TARGET:  File change -> Background Execute+Render -> [swap at bar N] -> seamless
```

### Recommended Architecture: Continuous Streaming Playback

```
flow-interpreter/
  LiveReloadManager.cs    # Orchestrates watch + render + swap
  Program.cs              # Refactored RunWithWatch delegates to LiveReloadManager

flow-lang/
  Audio/
    AudioPlaybackManager.cs   # Add SwapBuffer method + streaming support
    IAudioBackend.cs          # Add PlayStreaming or modify Play for callback model
    PulseAudioSimpleBackend.cs # Implement continuous chunk-writing loop
```

### Pattern 1: Continuous Streaming Playback Loop

**What:** Instead of one-shot `Play(entireBuffer)`, the playback runs in a dedicated thread that continuously reads from a "current buffer" reference and writes chunks to PulseAudio. When the buffer ends, it loops back to the start (continuous looping playback -- necessary for live coding).

**When to use:** Always in live/watch mode. Normal `play()` calls remain one-shot.

**Key insight:** PulseAudio Simple API's `pa_simple_write` is blocking -- it blocks until the audio server accepts the chunk. This is actually ideal for a streaming loop because it provides natural timing: the write call returns when PulseAudio is ready for more data, effectively pacing the loop at real-time speed.

```csharp
// Conceptual streaming loop (runs on dedicated thread)
void StreamingLoop(CancellationToken ct)
{
    const int chunkSamples = 4096; // ~93ms at 44100Hz stereo
    int position = 0; // current sample position in buffer
    
    while (!ct.IsCancellationRequested)
    {
        // Read current buffer atomically
        var buffer = Volatile.Read(ref _currentBuffer);
        if (buffer == null) { Thread.Sleep(10); continue; }
        
        // Check for pending swap at bar boundary
        if (_pendingBuffer != null && IsBarBoundary(position, buffer))
        {
            buffer = Interlocked.Exchange(ref _pendingBuffer, null)!;
            Volatile.Write(ref _currentBuffer, buffer);
            position = 0; // D-06: start from beginning
            OnReloaded(barNumber);
        }
        
        // Write next chunk
        int remaining = buffer.Data.Length - position;
        int chunkSize = Math.Min(chunkSamples, remaining);
        
        // Write chunk to PulseAudio (blocks until accepted)
        WriteChunk(buffer.Data, position, chunkSize);
        position += chunkSize;
        
        // Loop when buffer ends
        if (position >= buffer.Data.Length)
            position = 0;
    }
}
```

### Pattern 2: Bar Boundary Detection

**What:** Given a sample position in the buffer, determine whether it falls on a bar boundary. Bar duration in samples = `(beatsPerBar * 60.0 / tempo) * sampleRate * channels`.

```csharp
bool IsBarBoundary(int samplePosition, AudioBuffer buffer)
{
    // Bar duration in samples (interleaved, so multiply by channels)
    double secondsPerBar = (beatsPerBar * 60.0) / tempo;
    int samplesPerBar = (int)(secondsPerBar * buffer.SampleRate) * buffer.Channels;
    
    if (samplesPerBar <= 0) return true; // safety
    
    // Check if position is within one chunk of a bar boundary
    return (samplePosition % samplesPerBar) < chunkSize;
}
```

**Critical detail:** The check must account for chunk granularity. Since we write 4096 samples at a time (~46ms mono, ~23ms stereo at 44100Hz), the bar boundary detection should trigger when the current position is within one chunk of the boundary. This means the swap happens at most ~23ms before or after the exact boundary -- inaudible.

### Pattern 3: Background Render Pipeline

**What:** On file change, spawn a `Task` that creates a fresh `FlowEngine`, executes the script, extracts the rendered buffer, and sets it as the pending swap.

```csharp
async Task BackgroundRender(string filePath, CancellationToken ct)
{
    try
    {
        var source = await File.ReadAllTextAsync(filePath, ct);
        using var engine = new FlowEngine();
        ConfigureDevice(engine, _deviceName);
        
        var success = engine.Execute(source, filePath);
        
        if (!success)
        {
            // D-07: Error -- keep playing old version
            var errors = engine.ErrorReporter.FormatErrors();
            OnError(errors);
            return;
        }
        
        // Extract the rendered buffer from the engine
        // (Need a way to capture the buffer -- see Open Questions)
        var buffer = ExtractRenderedBuffer(engine);
        
        if (buffer != null)
        {
            // Set as pending -- streaming loop will pick it up at bar boundary
            Interlocked.Exchange(ref _pendingBuffer, buffer);
        }
    }
    catch (Exception ex)
    {
        OnError(ex.Message);
    }
}
```

### Pattern 4: Atomic Buffer Swap

**What:** Thread-safe exchange of the buffer reference using `Interlocked.Exchange`.

**Recommendation:** Use `Interlocked.Exchange` rather than a lock. The playback loop is latency-sensitive and must not be blocked. `Interlocked.Exchange` on reference types is lock-free on .NET and provides the needed atomicity.

```csharp
private AudioBuffer? _currentBuffer;
private AudioBuffer? _pendingBuffer;

// In streaming loop (reader):
var pending = Interlocked.Exchange(ref _pendingBuffer, null);
if (pending != null)
{
    Volatile.Write(ref _currentBuffer, pending);
    position = 0;
}

// In background render (writer):
Interlocked.Exchange(ref _pendingBuffer, newBuffer);
```

### Anti-Patterns to Avoid
- **Stop-and-restart playback:** This is what the current watch mode does. It causes an audible gap. The streaming loop must never stop.
- **Sharing FlowEngine across threads:** FlowEngine, ExecutionContext, and Interpreter are not thread-safe. Each background render must create its own instance (D-12).
- **Locking in the playback loop:** Any lock contention in the audio write path causes audible stuttering. Use lock-free patterns (`Volatile`, `Interlocked`) for the buffer swap.
- **Reading file immediately on FileSystemWatcher event:** Editors write in stages. The existing 500ms debounce (D-01) handles this. Additionally, the 100ms sleep before reading (already in `RunWithWatch`) should be kept.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Thread-safe reference swap | Custom lock wrapper | `Interlocked.Exchange<T>` | Lock-free, single atomic operation, built into .NET |
| File change debounce | Custom timer/queue | Existing `DateTime` comparison (500ms) in `RunWithWatch` | Already tested and working |
| Audio timing | Manual `Thread.Sleep` timing | PulseAudio blocking writes | `pa_simple_write` blocks naturally at audio rate -- no manual timing needed |
| Cancellation | Manual `bool` flags | `CancellationTokenSource` | Already used in `AudioPlaybackManager.StartPlayback()` |

## Common Pitfalls

### Pitfall 1: Buffer Extraction from FlowEngine
**What goes wrong:** The background `FlowEngine` executes the script, which calls `play()` or `renderSong()`. But `play()` tries to actually play audio through PulseAudio. In the background thread, we want the buffer, not playback.
**Why it happens:** `play()` is a side-effect function -- it renders AND plays. There's no "render only" path that returns the buffer without playing.
**How to avoid:** Two options: (a) The script uses `renderSong()` which returns a `Buffer` value -- capture the last expression value from the engine. (b) Intercept the `play()` call in the background engine's `AudioPlaybackManager` so it captures the buffer instead of playing it. Option (a) is simpler and requires scripts to use `renderSong` instead of `play` in live mode. Option (b) is more user-friendly but more complex. **Recommendation:** Support both -- if the script's last expression is a Buffer, use that. If the script calls `play()`, intercept it in the background engine via a "capture mode" flag on `AudioPlaybackManager`.
**Warning signs:** Background render appears to succeed but no buffer is captured; or background render tries to play audio on the already-in-use PulseAudio connection.

### Pitfall 2: PulseAudio Connection Sharing
**What goes wrong:** Two threads try to write to the same PulseAudio connection simultaneously, causing corruption or errors.
**Why it happens:** The background `FlowEngine` creates its own `AudioPlaybackManager`, which creates its own PulseAudio connection. If the script calls `play()` in the background thread, it would try to open a second PulseAudio stream.
**How to avoid:** The background engine's `AudioPlaybackManager` should be in "capture mode" -- it does NOT connect to PulseAudio. Only the main streaming loop holds the PulseAudio connection. The background engine intercepts `play`/`loop` calls and stores the buffer instead.
**Warning signs:** "PulseAudio: Failed to connect" errors during background rendering.

### Pitfall 3: Bar Boundary Drift with Tempo Changes
**What goes wrong:** The bar boundary calculation uses the old version's tempo (D-03). If the new version has a different tempo, the first bar of the new version starts at the old tempo's bar boundary, but the audio was rendered at the new tempo. This is musically correct per the decisions (old tempo determines WHEN to swap, new version determines WHAT plays).
**Why it happens:** By design -- but implementers might accidentally use the new tempo for boundary calculation.
**How to avoid:** Store `beatsPerBar` and `tempo` from the currently-playing version's musical context alongside the current buffer. Only update these when the buffer swaps.
**Warning signs:** Swap timing feels wrong when tempo changes between versions.

### Pitfall 4: Chunk Boundary vs Bar Boundary Alignment
**What goes wrong:** The chunk size (4096 samples = ~46ms at 44100/mono or ~23ms at 44100/stereo) doesn't evenly divide bar duration. The swap can only happen at chunk boundaries, not exact sample-level bar boundaries.
**Why it happens:** We write in fixed-size chunks for efficiency.
**How to avoid:** This is acceptable -- 23ms jitter is imperceptible. But the bar boundary check must account for it by checking if the position is within one chunk of the boundary, not at the exact boundary sample. Alternatively, split the last chunk of a bar into two writes: one up to the bar boundary (from old buffer), one from the bar boundary (from new buffer).
**Warning signs:** Occasional off-by-one-chunk timing; rare audible click at swap point.

### Pitfall 5: Zero-Crossing Clicks at Swap Point
**What goes wrong:** When the buffer swaps, the last sample of the old buffer and the first sample of the new buffer may have very different amplitudes, causing an audible click.
**Why it happens:** The old buffer ends mid-waveform at the bar boundary; the new buffer starts at its own beginning which may be at a different amplitude.
**How to avoid:** Apply a very short crossfade (1-2ms, ~44-88 samples) at the swap point. Fade out the last few samples of the old bar, fade in the first few samples of the new buffer. This is simple and inaudible. Alternatively, since the swap happens at a bar boundary and many songs have near-zero amplitude at bar starts (downbeat), this may be unnecessary in practice. **Recommendation:** Implement a micro-crossfade (64 samples / ~1.5ms) as insurance -- trivial to implement, guarantees no clicks.
**Warning signs:** Audible pop/click when saving changes during loud sustained notes.

## Code Examples

### Bar Duration Calculation
```csharp
// Source: MusicalContext defaults + TimeSignatureData
// tempo: BPM (double), beatsPerBar: numerator from TimeSignature
// sampleRate: typically 44100, channels: typically 2

double secondsPerBeat = 60.0 / tempo;
double secondsPerBar = secondsPerBeat * beatsPerBar;
int framesPerBar = (int)(secondsPerBar * sampleRate);
int samplesPerBar = framesPerBar * channels; // interleaved
```

### Micro-Crossfade at Swap Point
```csharp
const int crossfadeSamples = 64; // ~1.5ms at 44100Hz

// Apply to the chunk spanning the bar boundary
for (int i = 0; i < crossfadeSamples; i++)
{
    float fadeOut = 1.0f - (float)i / crossfadeSamples; // old buffer
    float fadeIn = (float)i / crossfadeSamples;          // new buffer
    
    outputChunk[i] = oldSample[barEnd + i] * fadeOut + newSample[i] * fadeIn;
}
```

### LiveReloadManager Skeleton
```csharp
// Source: Design from CONTEXT.md decisions
public class LiveReloadManager : IDisposable
{
    private readonly string _filePath;
    private readonly string? _deviceName;
    private AudioBuffer? _currentBuffer;
    private AudioBuffer? _pendingBuffer;
    private double _currentTempo = 120.0;
    private int _currentBeatsPerBar = 4;
    private CancellationTokenSource? _cts;
    private Task? _playbackTask;
    private Task? _renderTask;
    private FileSystemWatcher? _watcher;
    
    // File change -> debounce -> background render
    // Streaming loop -> bar boundary check -> swap
    // Error -> print, keep playing
    // Ctrl+C -> stop playback -> second Ctrl+C -> exit
}
```

### Capturing Buffer from Background Engine
```csharp
// Option: AudioPlaybackManager "capture mode"
public class AudioPlaybackManager : IDisposable
{
    // ... existing fields ...
    private AudioBuffer? _capturedBuffer;
    public bool CaptureMode { get; set; }
    
    // When CaptureMode is true, play() stores the buffer instead of playing it
    public AudioBuffer? GetCapturedBuffer()
    {
        var buf = _capturedBuffer;
        _capturedBuffer = null;
        return buf;
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| .NET Thread + manual sync | Task-based async with `Interlocked` | .NET 6+ | Simpler code, better thread pool utilization |
| `lock` for all thread safety | `Volatile.Read/Write` + `Interlocked.Exchange` for single-ref swaps | Always available, now best practice | Lock-free in hot path, no contention risk |
| `Thread.Sleep` for audio timing | Blocking `pa_simple_write` as natural pacer | PulseAudio Simple API design | No manual timing, jitter-free audio output |

## Open Questions

1. **Buffer extraction strategy**
   - What we know: Scripts may use `play()`, `loop()`, or `renderSong()`. We need the audio buffer without triggering PulseAudio playback.
   - What's unclear: Whether all live-coding scripts will consistently use one pattern. A user might write `play(renderSong(song, "piano"))` or just `renderSong(song, "piano")` as the last expression.
   - Recommendation: Implement "capture mode" on AudioPlaybackManager. When enabled, `play()` and `loop()` store the buffer instead of playing. The background FlowEngine enables capture mode. After execution, retrieve the captured buffer. This handles any script pattern transparently. If no buffer was captured (script doesn't produce audio), show a warning.

2. **Musical context extraction from background engine**
   - What we know: We need tempo and time signature from the NEW version to store alongside the swapped buffer (for the NEXT swap's bar boundary calculation).
   - What's unclear: How to reliably extract the "global" musical context after execution. The context stack is scoped and may be empty after execution completes.
   - Recommendation: After background execution, call `engine.Context.GetMusicalContext()` which resolves defaults (120 BPM, 4/4). Store this alongside the pending buffer. When the swap occurs, update the streaming loop's tempo/timesig values.

3. **Handling scripts that don't produce audio**
   - What we know: A user might save a file that has syntax but no `play`/`renderSong` call. The background render succeeds but produces no buffer.
   - What's unclear: Should the current playback stop? Continue? Show a warning?
   - Recommendation: If no buffer is captured after successful execution, show a terminal message "No audio output detected -- playback continues with previous version." Keep the old buffer playing.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | .flow test scripts (no unit test framework) |
| Config file | none |
| Quick run command | `dotnet run --project flow-interpreter tests/test_<name>.flow` |
| Full suite command | `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| LIVE-01 | Bar-boundary reload during playback | integration / manual | Manual: start `--watch`, edit file, verify timing | No -- Wave 0 |
| LIVE-02 | Playback continuity across reloads | integration / manual | Manual: listen for gaps/clicks during reload | No -- Wave 0 |
| LIVE-01 (unit) | Bar boundary calculation correctness | unit (.flow) | `dotnet run --project flow-interpreter tests/test_bar_boundary.flow` | No -- Wave 0 |
| LIVE-01 (unit) | LiveReloadManager buffer swap logic | manual-only | Requires audio hardware + timing verification | N/A |

### Sampling Rate
- **Per task commit:** `dotnet build && dotnet run --project flow-interpreter tests/test_bar_boundary.flow`
- **Per wave merge:** Full test suite
- **Phase gate:** Full suite green + manual live-coding session test

### Wave 0 Gaps
- [ ] `tests/test_bar_boundary.flow` -- test bar duration calculation with various tempos/time signatures (can test the math via built-in functions if exposed)
- [ ] Manual test procedure document for live-coding session verification (since automated audio continuity testing is impractical)

**Note:** Live-coding is inherently difficult to test automatically. The primary verification is a manual session where a developer runs `--watch`, edits a file during playback, and confirms seamless bar-boundary transitions. Unit tests can cover the bar boundary math but not the audio continuity.

## Sources

### Primary (HIGH confidence)
- `flow-interpreter/Program.cs` -- Current `RunWithWatch()` implementation (lines 126-194), debounce logic, Ctrl+C handling
- `flow-lang/Audio/AudioPlaybackManager.cs` -- Playback lifecycle, `StartPlayback()` CancellationToken pattern, `StopPlayback()`
- `flow-lang/Audio/PulseAudioSimpleBackend.cs` -- PulseAudio P/Invoke: `pa_simple_write` blocking behavior, 4096-sample chunk size, connection lifecycle
- `flow-lang/Audio/IAudioBackend.cs` -- Backend interface: `Play()`, `Stop()`, `Initialize()`
- `flow-lang/Core/FlowEngine.cs` -- Engine construction (self-contained), `Execute()` pipeline, `AudioManager` property
- `flow-lang/Runtime/MusicalContext.cs` -- Tempo, TimeSignature, defaults (120 BPM, 4/4)
- `flow-lang/Runtime/ExecutionContext.cs` -- `GetMusicalContext()` resolution from stack
- `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs` -- `PlayBuffer`, `LoopBufferInfinite` patterns, `PlaySamples` helper
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` -- `RenderSong()` produces `AudioBuffer`

### Secondary (MEDIUM confidence)
- .NET `Interlocked.Exchange<T>` semantics -- well-documented, standard pattern for lock-free reference swap
- PulseAudio Simple API blocking write behavior -- confirmed by code inspection of `PulseAudioSimpleBackend.Play()`
- `FileSystemWatcher` behavior on Linux -- known to work via inotify; existing implementation already handles debounce

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new dependencies, all .NET built-in threading primitives
- Architecture: HIGH -- clear pattern (streaming loop + atomic swap), well-understood PulseAudio behavior from existing code
- Pitfalls: HIGH -- identified from direct code analysis of current playback path and threading constraints

**Research date:** 2026-04-02
**Valid until:** 2026-05-02 (stable -- no external dependency changes expected)
