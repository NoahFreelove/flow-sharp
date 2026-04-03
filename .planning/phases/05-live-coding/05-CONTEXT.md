# Phase 5: Live Coding - Context

**Gathered:** 2026-04-03
**Status:** Ready for planning

<domain>
## Phase Boundary

Add beat-synced live reload to the existing `--watch` mode. When a user saves a .flow file during playback, the new version is parsed, executed, and rendered in a background thread, then swapped in at the next bar boundary. Playback continues seamlessly with no audible gap. Syntax/runtime errors keep the old version playing and display the error.

</domain>

<decisions>
## Implementation Decisions

### Reload Trigger & Quantization
- **D-01:** Reload triggers on file save (existing `FileSystemWatcher` with 500ms debounce).
- **D-02:** New version takes effect at the **next bar boundary** — the most musically natural transition point. Current bar finishes playing, then new audio starts at bar N+1.
- **D-03:** Bar boundary calculation uses tempo and time signature from the currently-playing version's `MusicalContext`. Latency is 0 to (beats-per-bar - 1) beats depending on where in the bar the save occurs.

### Playback Continuity
- **D-04:** Pre-render + swap strategy: while old version plays its final bar(s), the new version is parsed, executed, and rendered to a complete audio buffer in a **background thread**.
- **D-05:** At the bar boundary, the playback system atomically swaps the buffer pointer from old to new. Zero gap, no crossfade needed.
- **D-06:** The new version always starts playback from bar 1 (beginning of the rendered buffer). No attempt to resume from the "same position" in the song — the user edits represent a new composition.

### Error Handling During Playback
- **D-07:** If the new file has syntax or runtime errors, playback **continues with the last valid version**. The error is printed to terminal with line/column info.
- **D-08:** No playback interruption on error — the user fixes the error, saves again, and the next valid version swaps in normally.
- **D-09:** Terminal output distinguishes reload success vs error: success shows "Reloaded at bar N", error shows the error message with a clear indicator that old version continues.

### Scope of Hot Reload
- **D-10:** Full re-execution on every reload — the entire script is re-run from scratch. No incremental/section-only diffing. This was initially considered but rejected because sections can have cross-dependencies (shared variables, musical context blocks, custom oscillator registrations, probabilistic functions like `vary`).
- **D-11:** No state persists between reloads. Each reload is a clean execution. This ensures predictable behavior — what you see in the script is exactly what plays.
- **D-12:** The background thread gets its own `FlowEngine` instance to avoid thread-safety issues with the main execution context.

### Architecture
- **D-13:** The live reload system lives in `flow-interpreter` (not `flow-lang`) since it orchestrates the engine, not the language runtime.
- **D-14:** Existing `RunWithWatch` in `Program.cs` is refactored into a `LiveReloadManager` class that encapsulates the watch loop, background rendering, bar-boundary timing, and buffer swapping.
- **D-15:** `AudioPlaybackManager` needs a method to swap the playback buffer atomically (thread-safe). This is the one change needed in `flow-lang`.

### Claude's Discretion
- Exact threading model (Task-based vs dedicated thread for background rendering)
- Whether to use `Interlocked.Exchange` or a lock for the buffer swap
- How to calculate remaining time until next bar boundary from the playback position
- Whether `LiveReloadManager` exposes events/callbacks for the terminal UI or handles it directly
- Crossfade duration (if any small crossfade is needed to avoid zero-crossing clicks)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Existing Watch Mode
- `flow-interpreter/Program.cs` — `RunWithWatch()` method (lines 123-190). Current implementation: FileSystemWatcher, debounce, stop+restart. This is what gets refactored.
- `flow-interpreter/Program.cs` — `ExecuteScript()` method. The per-reload execution entry point.

### Audio Playback
- `flow-lang/Audio/AudioPlaybackManager.cs` — Manages audio backend lifecycle. Needs atomic buffer swap method.
- `flow-lang/Audio/IAudioBackend.cs` — Backend abstraction. PulseAudio implementation writes samples in a loop.
- `flow-lang/Audio/PulseAudioSimpleBackend.cs` — PulseAudio P/Invoke implementation. The actual audio output loop.

### Engine
- `flow-lang/Core/FlowEngine.cs` — Orchestrates lexing → parsing → interpretation. Background thread needs its own instance.
- `flow-lang/Runtime/MusicalContext.cs` — Tempo and time signature for bar boundary calculation.
- `flow-lang/Runtime/ExecutionContext.cs` — Execution state. NOT shared between threads (D-12).

### Song Rendering
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — `RenderSong` produces the AudioBuffer that gets swapped.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `FileSystemWatcher` setup in `RunWithWatch` — debounce, Ctrl+C handling, event wiring. Keep this, wrap in LiveReloadManager.
- `FlowEngine` — self-contained pipeline. Creating a second instance for background rendering is straightforward.
- `AudioPlaybackManager.StopAudio()` — already exists for stopping playback. Swap is the new capability.
- `MusicalContext.Tempo` and `MusicalContext.TimeSignature` — bar duration = (numerator / denominator) * (60 / tempo) seconds.

### Established Patterns
- `Program.cs` uses `FlowEngine` as the single orchestration point
- Audio backend writes interleaved stereo samples in a blocking loop
- Error accumulation via `ErrorReporter` — errors don't throw, they collect

### Integration Points
- `Program.cs RunWithWatch()`: Refactor into `LiveReloadManager`
- `AudioPlaybackManager`: Add `SwapBuffer(AudioBuffer newBuffer)` method
- `PulseAudioSimpleBackend`: May need to support buffer swap during playback loop

</code_context>

<specifics>
## Specific Ideas

- The experience should feel like live coding in SuperCollider or Sonic Pi — save and hear changes on the next bar
- Terminal feedback should be minimal but informative: "Reloaded at bar 5" or "Error on line 14 — keeping previous version"
- First Ctrl+C stops playback, second exits (existing behavior, keep it)

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 05-live-coding*
*Context gathered: 2026-04-03*
