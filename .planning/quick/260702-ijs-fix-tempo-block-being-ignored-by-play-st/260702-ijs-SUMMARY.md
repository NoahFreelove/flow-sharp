---
phase: quick-260702-ijs
plan: 01
subsystem: audio-playback
tags: [tempo, playback, musical-context, ergonomics, bugfix]
requires: [MusicalContext.Tempo, ExecutionContext.GetMusicalContext, Timeline.GetBPM]
provides: [play-Sequence-tempo-aware, stream-Sequence-tempo-aware]
affects: [flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs, flow-lang/Core/FlowEngine.cs, flow-lang/StandardLibrary/BuiltInFunctions.cs]
tech-stack:
  added: []
  patterns: [context-dependent-builtin-registration, resolve-on-originating-thread]
key-files:
  created:
    - flow-lang.Tests/Integration/Sweep0702/TempoAffectsPlaybackTests.cs
  modified:
    - flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs
    - flow-lang/Core/FlowEngine.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
decisions: [approach-A-context-dependent, move-not-reregister, resolve-bpm-on-calling-thread]
metrics:
  duration: ~15m
  completed: 2026-07-02
status: complete
---

# Phase quick-260702-ijs Plan 01: Fix tempo block ignored by play/stream Summary

`tempo N { (play seq) }` and `tempo N { (stream seq) }` now render at N BPM by resolving the BPM from the active `MusicalContext.Tempo` (the same seam SongRenderer/Interpreter already use), with a charitable fallback to `Timeline.GetBPM()` (default 120, `setBPM` honored).

## What changed

- **`PlaybackFunctions.cs`**: Moved the `play(Sequence)` and `stream(Sequence)` registrations out of `Register(registry, manager)` into a new `RegisterContextDependent(registry, manager, ExecutionContext context)`. Their lambdas resolve BPM via a shared `ResolveBpm(context)` helper — `context.GetMusicalContext().Tempo ?? Timeline.GetBPM([]).As<double>()`. `PlaySequence` and `StreamSequence` now take a resolved `double bpm` param; `PlaySequence` no longer reads `Timeline.GetBPM` directly (the root cause at old line 159 is deleted). `StreamSequence` captures `bpm` by value before `Task.Run`, so the background render honors the block tempo (the load-bearing reason Approach B was rejected). Both new `Register` calls carry `ParameterNames: ["seq"]`.
- **`FlowEngine.cs`**: Added `PlaybackFunctions.RegisterContextDependent(internalRegistry, _audioManager, _context)` right after `RegisterContextDependentFunctions`, mirroring the `GranularFunctions.Register` direct-call wiring.
- **`BuiltInFunctions.cs`** (`RegisterSignaturesOnly`): Added `Audio.PlaybackFunctions.RegisterContextDependent(proxy, dummyAudio, dummyContext)` so the LSP still enumerates the play/stream Sequence signatures.
- **New test** `TempoAffectsPlaybackTests.cs`: 3 CaptureMode facts (ratio, analytic mapping, no-tempo default-120 fallback).

## Deviations from Plan

**1. [Rule 1 — corrected assumption] Analytic frame count is 88200, not 176400.**
- **Found during:** Task 2 (test RED run).
- **Issue:** The plan assumed the 8-note stream `| C4 D4 E4 F4 G4 A4 B4 C5 |` spans 8 beats (→ 176400 frames @ 120 BPM). Bare undurated notes default to eighth notes, so the stream spans 4 beats → 88200 frames @ 120 BPM (observed).
- **Fix:** Pinned `PlaySequence_TempoBlock_MapsToAnalyticFrameCount` to 88200 ±0.1s and updated the `Stream` doc comment. The ratio test (tempo 480 ≈ 4× shorter than tempo 120) and the no-tempo default-120 test were unaffected and passed unchanged — the ratio invariant the fix actually proves is intact.
- **Files modified:** flow-lang.Tests/Integration/Sweep0702/TempoAffectsPlaybackTests.cs
- **Commit:** 7b890d5

**2. [Rule 3 — namespace qualifier] `PlaybackFunctions` unqualified in FlowEngine.cs.**
- **Issue:** The plan's literal `Audio.PlaybackFunctions...` in FlowEngine failed to compile — `Audio.` there resolves to `FlowLang.Audio` (playback infra), not `FlowLang.StandardLibrary.Audio`. FlowEngine already has `using FlowLang.StandardLibrary.Audio;`.
- **Fix:** Called `PlaybackFunctions.RegisterContextDependent(...)` unqualified, matching the sibling `GranularFunctions.Register` call. (BuiltInFunctions.cs keeps the `Audio.` prefix — correct there, since that file's namespace is `FlowLang.StandardLibrary`.)
- **Commit:** c1ec6f1

`RuntimeContext` (FlowEngine's `_context` type) is a `using`-alias for `FlowLang.Runtime.ExecutionContext`, so it binds to the `ExecutionContext context` param directly — no adaptation needed.

## Verification (actual results)

- `dotnet build flow-lang/flow-lang.csproj` → **0 Errors** (8 pre-existing warnings).
- `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web` → **0 Errors** (8 warnings).
- `dotnet test --filter ~TempoAffectsPlaybackTests` → **3 passed, 0 failed**.
- `dotnet test --filter ~ParameterNamesCoverageTest|~BuiltInFunctionsTests` → **31 passed, 0 failed** (invariant `registerCount == paramNamesCount + varArgsCount` intact; "play"/"stream" still enumerable).
- Smoke: `dotnet run --project flow-interpreter -- -e 'use "@audio"\ntempo 480 { (play | C4 D4 E4 F4 | ) }'` → **EXIT=0**, no throw (only pre-existing NU1701 Rug.Osc warnings).

## Out of scope (unchanged)

- The live flowlang.dev playground will NOT reflect this until `bash flow-site/scripts/sync-runtime.sh` regenerates the committed WASM bundle — a separate manual step intentionally NOT run here.
- `loop` / `preview` are Buffer-only overloads and were untouched.

## Commits

- c1ec6f1 — fix(quick-260702-ijs): resolve play/stream tempo from active MusicalContext
- 7b890d5 — test(quick-260702-ijs): tempo block scales play() rendered duration

## Self-Check: PASSED
- FOUND: flow-lang.Tests/Integration/Sweep0702/TempoAffectsPlaybackTests.cs
- FOUND: commit c1ec6f1
- FOUND: commit 7b890d5
