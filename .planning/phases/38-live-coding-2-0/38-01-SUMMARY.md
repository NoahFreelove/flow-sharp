---
phase: 38-live-coding-2-0
plan: 01
subsystem: live-coding
tags: [live, watch, ansi, debounce, panel, livereload, ui]
requires:
  - flow-interpreter/LiveReloadManager.cs (Phase 28 baseline — 389 lines)
  - flow-lang/Diagnostics/RenderingDiagnostics.cs (Phase 23 WarnOnce surface)
  - flow-lang/Runtime/FlowConfig.cs (Phase 30 reset surface)
provides:
  - LiveStatusPanel — public ANSI 4-row + plain-line dual-mode renderer
  - AdvisoryLevel enum (Info, Success, Warning, Error)
  - LiveBlockDisplay record (Quantize, Line, LastSwapBar, SecondsSinceSwap)
  - LiveBlockBuffer record (BlockId, Bytes, Length) — internal, consumed by Plan 38-02
  - LiveReloadManager (un-sealed) — OnRenderTriggered + InvokeTriggerForTesting test seams
  - LiveReloadManager.DebounceMs constant = 200
affects:
  - flow-interpreter/LiveReloadManager.cs (orchestration rewrite, BYTE-IDENTICAL preservation of CheckBarBoundary + ApplyCrossfade + RenderScript body)
  - flow-interpreter/flow-interpreter.csproj (added flow-lsp ProjectReference for Plan 38-04 prep)
  - flow-lang.Tests/flow-lang.Tests.csproj (added flow-interpreter ProjectReference)
tech-stack:
  added: []
  patterns:
    - "ANSI CSI escape sequences (\\u001b[ ... letter) emitted only when isColorEnabled"
    - "Color-disable detection (UI-SPEC lines 113-118): NO_COLOR / --no-color / TERM=dumb / Console.IsOutputRedirected"
    - "RESEARCH §E Option A — Task.Run + Wait(TimeSpan) for cooperative-orphan 30s wall-clock cap"
    - "WarnOnce dedup-key sentinels: live-timeout / live-write / live-init-fail / live-parse / live-exception"
    - "System.Threading.Timer at 500ms period for 2 Hz heartbeat (sticky-advisory clear + ago suffix), off the audio thread per Pitfall #21"
key-files:
  created:
    - flow-interpreter/LiveStatusPanel.cs (429 LOC)
    - flow-lang.Tests/Integration/Phase38/WatchDebounceTests.cs (109 LOC)
    - flow-lang.Tests/Integration/Phase38/AnsiPanelRenderTests.cs (146 LOC)
    - flow-lang.Tests/Integration/Phase38/PanelTtyFallbackTests.cs (146 LOC)
  modified:
    - flow-interpreter/LiveReloadManager.cs (389 → 614 LOC; orchestration rewritten; primitives preserved byte-identical)
    - flow-interpreter/flow-interpreter.csproj (+1 ProjectReference)
    - flow-lang.Tests/flow-lang.Tests.csproj (+1 ProjectReference)
decisions:
  - "Test fixups landed alongside production (commit d4f14f3) — driven by production behavior, not authored speculatively in Task 1"
  - "AnsiPanelRenderTests strip ANSI CSI escapes before substring-checking; the panel inserts dim/reset BETWEEN labels and values per UI-SPEC Typography table"
  - "PanelTtyFallbackTests construct ESC at runtime via (char)0x1B to dodge the C# `\\x` hex-escape variable-length parsing ambiguity"
  - "Class un-sealed to enable test subclassing (CountingLiveReloadHarness); OnRenderTriggered is `protected virtual`; InvokeTriggerForTesting is `protected internal` for cross-assembly access"
  - "PublishPanelState currently stubs poolSize=32 + empty instrument dict — Plan 38-03 will hook VoiceAllocator.LastPoolSizeUsedForTests"
metrics:
  duration: "~1.5h"
  completed: 2026-05-24T03:13:10Z
  tasks: 3
  files_created: 4
  files_modified: 3
---

# Phase 38 Plan 01: Modernized `flow watch` (LIVE-02 + ANSI Panel + 200ms Debounce) Summary

Rewrote `flow-interpreter/LiveReloadManager.cs` orchestration to ship the modernized `flow watch` watch-mode UX: 4-row ANSI live status panel with TTY-detection fallback, 200ms file-watch debounce (down from 500ms), 30s wall-clock cap on each live re-render via `Task.Run + Wait(TimeSpan)` (RESEARCH §E Option A), and a per-block pending-buffer dictionary scaffolded for Plan 38-02's `live { }` block consumption. The Phase 28/29/33 byte-identical bar-boundary detection + 64-sample equal-power crossfade + capture-mode FlowEngine render primitives are preserved byte-identical (D-38-06 contract).

## Commits

| Task | Commit | Description |
|------|--------|-------------|
| 1 | `ccba90f` | test(38-01): Wave 0 xUnit scaffolds (debounce + ANSI panel + TTY fallback) |
| 2 | `8fbc127` | feat(38-01): LiveStatusPanel with 4-row ANSI + plain-line dual mode |
| 3 | `d4f14f3` | feat(38-01): rewrite LiveReloadManager orchestration |

## Tasks Completed

### Task 1 — Phase 38 test directory + 3 Wave 0 xUnit scaffolds

Created `flow-lang.Tests/Integration/Phase38/` directory (verified missing per PATTERNS.md Critical Audit) with three xUnit test files following the Phase 37 `GranularSynthesisTests` precedent: `[Collection("FlowScripts")]`, `IDisposable` ctor/Dispose calling `RenderingDiagnostics.ResetForTesting() + FlowConfig.Reset()`.

- **WatchDebounceTests** (109 LOC): asserts `DebounceMs == 200`; rapid changes 50ms apart coalesce to one render trigger; rapid changes 220ms apart fire twice. Uses a `CountingLiveReloadHarness` subclass overriding the `OnRenderTriggered` seam so the test doesn't boot FlowEngine.
- **AnsiPanelRenderTests** (146 LOC): asserts 4-row layout with 2 live blocks; row 2 omitted when zero blocks (UI-SPEC line 145); `PublishAdvisory` populates row 4 with surface prefix. Strips ANSI CSI escapes before substring-checking (panel inserts dim/reset BETWEEN labels and values).
- **PanelTtyFallbackTests** (146 LOC): asserts plain-line `[watch] tempo=N timesig=N/N bar=N voices=N/M` shape (UI-SPEC line 178); `NO_COLOR=1` wins the color-disable race even when `forceTtyMode: true`; identical state publishes emit exactly once.

Added `flow-interpreter` ProjectReference to `flow-lang.Tests.csproj` so tests reach the `LiveReloadManager` + `LiveStatusPanel` API.

Initial build state: RED (the panel + un-sealed harness don't exist yet — expected TDD signal).

### Task 2 — LiveStatusPanel.cs

Created `flow-interpreter/LiveStatusPanel.cs` (429 LOC) implementing the UI-SPEC §"ANSI Live Status Panel" (lines 122-180) as a standalone top-level class in the `FlowInterpreter` namespace.

Public API surface:
- `ctor(TextWriter? out, bool forceTtyMode, IReadOnlyList<string>? cliArgs)`
- `PublishState(double tempo, (int, int) timesig, int bar, IReadOnlyList<LiveBlockDisplay> blocks, int activeVoices, int poolSize, IReadOnlyDictionary<string, int> perInstrumentCount)`
- `PublishAdvisory(string body, AdvisoryLevel level, string? dedupKey)`
- `Dispose()` releasing the 2 Hz heartbeat Timer
- `enum AdvisoryLevel { Info, Success, Warning, Error }`
- `record LiveBlockDisplay(Quantize, Line, LastSwapBar, SecondsSinceSwap)`

ANSI mode:
- Row 1: `<dim>Tempo: <reset>120 BPM | <dim>TimeSig: <reset>4/4 | <dim>Bar: <reset>47`
- Row 2: `Live blocks: live 1bar @ L12 (last swap bar 47, Xs ago) | ...` — OMITTED when zero blocks per UI-SPEC line 145
- Row 3: `Voices: 8/32 | piano:3 brass:2 strings:3` — descending count, alphabetic tie-break (Phase 28 voice-allocator precedent)
- Row 4: sticky advisory, auto-cleared after 8s via heartbeat

Plain-line mode:
- One `[watch] tempo=N timesig=N/N bar=N voices=N/M` line per state change (no-op on identical state)
- Advisory bodies emit unchanged (caller's `[prefix]` already carries the cue)

The 2 Hz heartbeat `Timer` only spins in real-terminal mode (`_writesToStdout && _isColorEnabled`) so test-writer mode doesn't allocate a thread. Heartbeat ticks off the audio thread per Pitfall #21.

`PublishAdvisory` routes through `RenderingDiagnostics.WarnOnce(dedupKey, body)` when a key is supplied so the stderr advisory and the row-4 sticky stay in sync per UI-SPEC line 367.

### Task 3 — LiveReloadManager orchestration rewrite

Rewrote `flow-interpreter/LiveReloadManager.cs` (389 → 614 LOC) per the locked orchestration changes; PRESERVED `CheckBarBoundary` + `ApplyCrossfade` + `RenderScript` BODY byte-identical (D-38-06).

Key changes:
- `DebounceMs = 200` public const (down from 500ms hardcoded literal). Verified by `WatchDebounceTests.DebounceMs_Is200_NotLegacy500`.
- Class un-sealed to support `CountingLiveReloadHarness` subclassing.
- `protected virtual void OnRenderTriggered()` — testable seam (default dispatches `StartRenderTask`).
- `protected internal void InvokeTriggerForTesting()` — cross-assembly hook for the harness.
- `Dictionary<int, LiveBlockBuffer> _pendingPerBlock` replaces the single `_pendingBuffer` field. Plan 38-01 uses sentinel `BlockId = 0` for whole-script swap mode (D-38-01); Plan 38-02 will populate real per-`live{}`-block ids from the AST visitor. Adjacent `internal sealed record LiveBlockBuffer(int BlockId, float[] Bytes, int Length)` declared at the file top.
- 30s wall-clock cap via `Task.Run + Wait(TimeSpan.FromSeconds(30))` per **RESEARCH §E Option A**. Workers exceeding 30s leak as orphans — accepted per D-38-07 / T-38-22 with the required inline `// RESEARCH §E Option A: workers that exceed 30s leak as orphans — acceptable for v1.5 per D-38-07` comment.
- Timeout dispatch publishes `Warning`-level advisory with dedup key `live-timeout:{filepath}` per UI-SPEC Advisory Catalog line 330; the previous `_currentBuffer` keeps playing (no swap).
- `LiveStatusPanel` field installed at `Run()` entry; constructed with `Environment.GetCommandLineArgs()` so the `--no-color` gate works end-to-end.
- All 3 prior `Console.ForegroundColor` blocks replaced with `_panel.PublishAdvisory(...)` calls (7 call sites total): success swap (Success), audio write error (Error + dedup), initial-fail (Error + dedup), exception (Error + dedup), parse error (Error + dedup), no-audio-output (Error + dedup), timeout (Warning + dedup).
- `RenderScript` signature grew by `out Dictionary<int, LiveBlockBuffer>? perBlockBuffers` per RESEARCH §F line 500; Plan 38-01 always emits `null` (the orchestration wraps the captured buffer in a sentinel-id dict on its own). Body unchanged.

## Verification

| Check | Result |
|-------|--------|
| `dotnet build` | Build succeeded |
| `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase38"` | 9/9 GREEN |
| `dotnet test ... --filter "Phase37|Phase38"` | 58/58 GREEN (Phase 28/29/33 byte-identical contract preserved) |
| `grep -c "DebounceMs = 200" LiveReloadManager.cs` | 1 |
| `grep -nE "(CheckBarBoundary|ApplyCrossfade|RenderScript)"` | All 3 present |
| `grep -c "_panel.PublishAdvisory"` | 7 (≥3 required) |
| `grep -n "Wait(RenderTimeout)"` | Present at line 456 |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] C# `\x` hex-escape parsing ambiguity in test source**
- **Found during:** Task 3 test run
- **Issue:** The Plan's test specification used `"\x1b["` to assert absence of ANSI escapes. C# parses `\x` as up to 4 hex digits — `\x1b[` reads `1b` then stops at `[`, yielding ESC + `[`. But xUnit's error renderer stripped the literal ESC byte from the displayed needle, suggesting either Roslyn collapsed the escape or the test runner display dropped the control char. Either way the assertion was failing on plain-line output (which had no ANSI).
- **Fix:** Constructed ESC at runtime via `new string((char)0x1B, 1)` so the source file stays pure ASCII and the runtime string is unambiguously 1 char. Switched `Assert.DoesNotContain` to `Assert.False(output.Contains(Esc), ...)` to keep the assertion message readable.
- **Files modified:** `flow-lang.Tests/Integration/Phase38/PanelTtyFallbackTests.cs`
- **Commit:** `d4f14f3`

**2. [Rule 2 - Missing test infrastructure] Test assertions assumed contiguous field text**
- **Found during:** Task 3 test run
- **Issue:** AnsiPanelRenderTests expected `Assert.Contains("Tempo: 120 BPM", output)` to find the literal substring. The panel correctly inserts `<dim>Tempo: <reset>120 BPM` per UI-SPEC Typography table ("labels dim, values default"), so the visible string is `Tempo: 120 BPM` but the raw output has ESC sequences between `Tempo: ` and `120`.
- **Fix:** Added a `StripAnsi` helper (regex `\[[0-9;]*[A-Za-z]`) and substring-checked the stripped visible text. Aligns with the plan's intent ("contains row 1 substring `Tempo: 120 BPM`") while honoring the panel's correct ANSI emission.
- **Files modified:** `flow-lang.Tests/Integration/Phase38/AnsiPanelRenderTests.cs`
- **Commit:** `d4f14f3`

**3. [Rule 3 - Blocking issue] `internal` accessibility insufficient for cross-assembly subclassing**
- **Found during:** Task 3 build
- **Issue:** The plan called for `internal void InvokeTriggerForTesting()` on `LiveReloadManager`. `flow-lang.Tests` is a separate assembly with no `InternalsVisibleTo` declaration in `flow-interpreter`, so the test couldn't reach the method.
- **Fix:** Changed to `protected internal void InvokeTriggerForTesting()` so cross-assembly test subclasses inherit access.
- **Files modified:** `flow-interpreter/LiveReloadManager.cs`
- **Commit:** `d4f14f3`

### Wave-2/3 Prep (success-criteria item)

Added `flow-lsp` ProjectReference to `flow-interpreter/flow-interpreter.csproj` at Plan 38-01 to avoid a wave-2/3 csproj race when Plan 38-04 ships the in-process LSP per D-38-12. Documented inline with a `<!-- Phase 38 Plan 38-04 prep ... -->` comment.

## Plan 38-02 Hooks

Plan 38-02 (parser + `live { }` AST + multi-block independent swap) will consume:
- `LiveBlockBuffer` record (already declared adjacent to the class)
- `Dictionary<int, LiveBlockBuffer> _pendingPerBlock` dict (Plan 38-01 ships with sentinel id 0; Plan 38-02 populates real ids)
- `LiveStatusPanel.PublishState(blocks: List<LiveBlockDisplay>, ...)` (Plan 38-01 always passes empty list; Plan 38-02 enumerates from AST)
- `RenderScript(...)` signature's `out Dictionary<int, LiveBlockBuffer>? perBlockBuffers` param (Plan 38-01 always emits null; Plan 38-02 fills from AST visitor)

## Plan 38-03 Hooks

Plan 38-03 (state preservation + voice-pool name-key + 30s CancellationToken + stale-closure) will consume:
- `PublishPanelState(barNumber)` (currently stubs `poolSize=32`, empty instrument dict; Plan 38-03 hooks `VoiceAllocator.LastPoolSizeUsedForTests` + per-instrument count introspection)
- The 30s `RenderTimeout` constant + Option A `Task.Run + Wait` wrap (Plan 38-03 may upgrade Option A → cooperative CancellationToken plumb if HUMAN-UAT reports worker accumulation per D-38-07 / T-38-22)
- The `live-stale-closure:{name}:{line}` dedup key shape (already documented in the UI-SPEC Advisory Catalog; Plan 38-03 wires the detection)

## Threat Surface

No new threats introduced beyond the plan's `<threat_model>`:
- **T-38-21 (DoS via FileSystemWatcher flood):** mitigated by `DebounceMs = 200` gate at `TriggerBackgroundRender`; verified by `WatchDebounceTests.RapidSaves_Within200ms_CoalesceIntoOneRender`.
- **T-38-22 (Worker-task accumulation):** accepted per D-38-07; documented in `StartRenderTask` inline comment + summary deferred-items above.
- **T-38-08 (ANSI escape injection):** mitigated — `PublishAdvisory` writes via `Console.Error.WriteLine` through `WarnOnce`; panel renders only sanitized state values it computes from `MusicalContext`. No file-path-derived strings reach ANSI emission.

## Self-Check: PASSED

- [x] flow-interpreter/LiveStatusPanel.cs exists (429 LOC ≥ 200 min)
- [x] flow-interpreter/LiveReloadManager.cs rewritten (614 LOC; primitives preserved)
- [x] flow-lang.Tests/Integration/Phase38/WatchDebounceTests.cs exists (109 LOC)
- [x] flow-lang.Tests/Integration/Phase38/AnsiPanelRenderTests.cs exists (146 LOC)
- [x] flow-lang.Tests/Integration/Phase38/PanelTtyFallbackTests.cs exists (146 LOC)
- [x] DebounceMs constant = 200 (1 grep hit)
- [x] Wait(RenderTimeout) with 30s TimeSpan present
- [x] _panel.PublishAdvisory call count ≥ 3 (7 actual)
- [x] CheckBarBoundary + ApplyCrossfade + RenderScript primitives all present
- [x] Commits exist in git log: ccba90f, 8fbc127, d4f14f3
- [x] flow-interpreter.csproj has flow-lsp ProjectReference (Plan 38-04 prep)
- [x] All 9 Phase 38 tests GREEN; Phase 37 + Phase 38 combined 58/58 GREEN (byte-identical contract preserved)
