---
phase: 05-live-coding
plan: 01
subsystem: audio
tags: [live-reload, streaming-playback, pulseaudio, bar-boundary, atomic-swap, capture-mode]

requires:
  - phase: 02-audio-features
    provides: AudioPlaybackManager, IAudioBackend, PulseAudioSimpleBackend, PlaybackFunctions

provides:
  - WriteChunk streaming method on IAudioBackend for chunk-level audio output
  - EnsureInitialized convenience method for streaming callers
  - CaptureMode on AudioPlaybackManager for background rendering without PulseAudio
  - LiveReloadManager orchestrating file-watch, background render, streaming playback, bar-boundary swap

affects: [05-live-coding]

tech-stack:
  added: []
  patterns: [capture-mode-rendering, atomic-buffer-swap, streaming-playback-loop, bar-boundary-detection]

key-files:
  created:
    - flow-interpreter/LiveReloadManager.cs
  modified:
    - flow-lang/Audio/IAudioBackend.cs
    - flow-lang/Audio/PulseAudioSimpleBackend.cs
    - flow-lang/Audio/AudioPlaybackManager.cs
    - flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs

key-decisions:
  - "WriteChunk uses local clamped buffer to avoid full-array allocation while still clamping"
  - "Bar boundary detection uses within-one-chunk tolerance for reliable swap timing"
  - "Micro-crossfade of 64 samples (~1.5ms) at swap point to prevent audible clicks"
  - "Background render uses fresh FlowEngine with CaptureMode to avoid PulseAudio contention"

patterns-established:
  - "CaptureMode pattern: set AudioPlaybackManager.CaptureMode=true, execute script, retrieve buffer via GetCapturedBuffer()"
  - "Streaming playback: EnsureInitialized then loop WriteChunk calls with caller-managed position"
  - "Atomic swap: Interlocked.Exchange for pending buffer, Volatile.Read/Write for current buffer"

requirements-completed: [LIVE-01, LIVE-02]

duration: 3min
completed: 2026-04-03
---

# Phase 5 Plan 1: Live Coding Infrastructure Summary

**Streaming playback loop with atomic bar-boundary buffer swapping and capture-mode background rendering for beat-synced live reload**

## Performance

- **Duration:** 3 min
- **Started:** 2026-04-03T15:17:11Z
- **Completed:** 2026-04-03T15:20:49Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- Added WriteChunk and EnsureInitialized to IAudioBackend/PulseAudioSimpleBackend for chunk-level streaming playback
- Built CaptureMode on AudioPlaybackManager so background FlowEngine instances render without touching PulseAudio
- Created LiveReloadManager (389 lines) with continuous streaming loop, bar-boundary detection, atomic buffer swap, FileSystemWatcher debounce, micro-crossfade, and error-resilient background rendering

## Task Commits

Each task was committed atomically:

1. **Task 1: Add streaming support to audio backend and capture mode** - `855f78c` (feat)
2. **Task 2: Create LiveReloadManager with streaming playback and bar-boundary swap** - `cd5a741` (feat)

## Files Created/Modified
- `flow-lang/Audio/IAudioBackend.cs` - Added WriteChunk and EnsureInitialized interface methods
- `flow-lang/Audio/PulseAudioSimpleBackend.cs` - Implemented WriteChunk with clamping, no drain; EnsureInitialized with param matching
- `flow-lang/Audio/AudioPlaybackManager.cs` - Added CaptureMode, GetCapturedBuffer, SetCapturedBuffer for background rendering
- `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs` - CaptureMode intercepts in PlayBuffer, PlaySequence, LoopBufferInfinite, LoopBufferN
- `flow-interpreter/LiveReloadManager.cs` - Full live-reload orchestrator with streaming loop, bar-boundary swap, file watching

## Decisions Made
- WriteChunk allocates a small clamped chunk buffer rather than clamping the full source array -- trades a small allocation for avoiding mutation of the caller's buffer
- Bar boundary tolerance is one chunk width (4096 samples) to ensure swaps happen reliably without requiring exact sample alignment
- 64-sample crossfade (~1.5ms at 44100Hz) blends old/new buffers at swap point to prevent zero-crossing clicks
- Background render creates a fresh FlowEngine per reload (per D-12) with CaptureMode=true to avoid PulseAudio contention

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed Value.RawValue to Value.Data**
- **Found during:** Task 2 (LiveReloadManager)
- **Issue:** Plan referenced `Value.RawValue` but the actual property name is `Value.Data`
- **Fix:** Changed `result?.RawValue` to `result?.Data` in the fallback buffer extraction
- **Files modified:** flow-interpreter/LiveReloadManager.cs
- **Verification:** Build succeeded
- **Committed in:** cd5a741 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Minor property name correction. No scope change.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- LiveReloadManager is ready to be wired into Program.cs (Plan 2 scope)
- All streaming and capture infrastructure in place for live-coding mode activation
- Existing tests pass without regression

---
*Phase: 05-live-coding*
*Completed: 2026-04-03*
