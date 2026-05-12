---
phase: 30-flow-cli-formal-install
plan: 06
subsystem: flow-midi
tags: [test-infra, bug-b, midi-import, quantizer, flow-generator, red-target, xunit]
requires:
  - 30-01 (flow-cli scaffold — solution shape baseline)
provides:
  - flow-midi.Tests xUnit project added to flow-sharp.sln
  - MidiFixtureBuilder fluent in-memory fixture helper (reachable via InternalsVisibleTo)
  - 13 xUnit facts on HEAD: 5 GREEN (smoke + canonical-rhythm baselines), 8 RED (Bug B defects 1/2/3 codified)
  - GREEN target for Plan 30-07 (Quantizer rework) and Plan 30-08 (FlowGenerator emit)
affects:
  - flow-midi/flow-midi.csproj (added InternalsVisibleTo)
  - flow-sharp.sln (new project entry, 12 ProjectConfigurationPlatforms lines, GUID 3E810968-859B-4880-8C4F-A3E01CF9476D)
tech-stack-added:
  - xunit.v3 3.2.2 (consistent with flow-lang.Tests)
  - Microsoft.NET.Test.Sdk 17.13.0
  - xunit.runner.visualstudio 3.1.5
  - coverlet.collector 6.0.2
patterns:
  - Fluent builder synthesizes MidiFile in-memory (no on-disk fixtures)
  - RED-by-design facts: failing assertion = pinned defect; turning GREEN = production-code fix landed
  - InternalsVisibleTo grants test assembly access to internal record types in flow-midi without changing accessibility
key-files:
  created:
    - flow-midi.Tests/flow-midi.Tests.csproj
    - flow-midi.Tests/Fixtures/MidiFixtureBuilder.cs
    - flow-midi.Tests/Unit/Phase30/HarnessSmokeFacts.cs
    - flow-midi.Tests/Unit/Phase30/QuantizerSnapDurationTests.cs
    - flow-midi.Tests/Unit/Phase30/QuantizerRoundingTests.cs
    - flow-midi.Tests/Unit/Phase30/FlowGeneratorStructureTests.cs
  modified:
    - flow-midi/flow-midi.csproj
    - flow-sharp.sln
decisions:
  - "Use dotnet sln add (not hand-edit) — produces correct ProjectConfigurationPlatforms entries; GUID is stable across re-runs"
  - "Tests reach internal flow-midi record types via InternalsVisibleTo(\"flow-midi.Tests\") rather than promoting types to public; flow-midi is an Exe with no library consumers, so keeping its types internal preserves encapsulation"
  - "Use availableTicks-jitter (next-note-starts-1-tick-early) instead of rawDuration-jitter for Defect 1 RED facts: tracing showed SnapDurationCapped takes availableTicks (not capped) as its cap, so only ticking the GAP triggers the strict-rejection bug"
  - "Each RED fact's failure message names the SPEC requirement or production-code line responsible — Plan 30-07/08 implementers see the GREEN target inline"
metrics:
  duration: 9m
  completed: 2026-05-11
  tasks: 3
  commits: 3
  files-created: 6
  files-modified: 2
  facts-added: 13
  facts-green-on-head: 5
  facts-red-on-head: 8
---

# Phase 30 Plan 06: flow-midi Test Infrastructure + Bug B Defect Pinning Summary

Stand up `flow-midi.Tests` as the FIRST gate of Bug B closure (RESEARCH Layer 1 — test
infrastructure precedes algorithm rewrites). Plans 30-07 and 30-08 now have an executable
GREEN target — 8 failing facts whose assertion messages name the exact production-code
defects they pin.

## What Shipped

### 1. `flow-midi.Tests` xUnit project (Task 1, commit `a78054a`)

- `flow-midi.Tests/flow-midi.Tests.csproj` — net10.0, `RootNamespace=FlowMidi.Tests`,
  `IsTestProject=true`. References xunit.v3 3.2.2, Test.Sdk 17.13.0, runner.visualstudio 3.1.5,
  coverlet.collector 6.0.2. ProjectReferences `flow-midi` + `flow-lang`.
- `flow-midi/flow-midi.csproj` gained `<InternalsVisibleTo Include="flow-midi.Tests" />`
  so the test assembly reaches the internal record types in `Midi/MidiTypes.cs`
  (`MidiFile`, `MidiTrack`, `NoteOnEvent`, etc.) and the internal `static class Quantizer`
  + `static class FlowGenerator`.
- `flow-sharp.sln` gained one Project declaration + 12 ProjectConfigurationPlatforms lines
  (GUID `3E810968-859B-4880-8C4F-A3E01CF9476D`). Added via `dotnet sln add` rather than
  hand-edit.

### 2. `MidiFixtureBuilder` + GREEN harness smoke facts (Task 2, commit `a6c93bc`)

`flow-midi.Tests/Fixtures/MidiFixtureBuilder.cs` — fluent in-memory MidiFile builder.

| Method                                                           | Produces                          |
| ---------------------------------------------------------------- | --------------------------------- |
| `WithFormat(int)` / `WithTpqn(int)`                              | configures Format + TPQN          |
| `WithTrackName(string)`                                          | sets current track's name         |
| `StartNewTrack()`                                                | begins a new MidiTrack            |
| `AddTempoEvent(double bpm, long tick = 0)`                       | `TempoEvent`                      |
| `AddTimeSignatureEvent(int n, int d, long tick = 0)`             | `TimeSignatureEvent`              |
| `AddKeySignatureEvent(int sf, bool minor, long tick = 0)`        | `KeySignatureEvent`               |
| `AddNote(channel, pitch, startTick, endTick, velocity = 100)`    | matched NoteOn + NoteOff pair     |
| `AddFourQuarterNotes(channel, basePitch)`                        | 4 contiguous Qs at TPQN, ascending C-D-E-F shape |
| `Build()`                                                        | stable-sorts events per track by AbsoluteTick, returns immutable `MidiFile` |

`flow-midi.Tests/Unit/Phase30/HarnessSmokeFacts.cs` (2 facts, GREEN on HEAD):
- `SuffixToTicks_Quarter_At_Tpqn_480_Returns_480` — pins the Quantizer tick constant.
- `Builder_Constructs_MidiFile_With_Expected_Events` — pins the builder's shape (4 events for 1 note).

### 3. RED-on-HEAD fact classes pinning Bug B defects (Task 3, commit `81d2729`)

| File                                  | Facts | RED | GREEN-baseline | Pins Defect                                                |
| ------------------------------------- | ----- | --- | -------------- | ---------------------------------------------------------- |
| `QuantizerSnapDurationTests.cs`       | 4     | 2   | 2              | Defect 1 — strict-cap rejects grid > availableTicks       |
| `QuantizerRoundingTests.cs`           | 3     | 3   | 0              | Defect 2 — leading empty bars + AddRests over-emission; Defect 3 — RH/LH pitch split |
| `FlowGeneratorStructureTests.cs`      | 4     | 3   | 1              | SPEC-5 emit shape — `(play output)` trailer, _rh/_lh suffixes, auto-fit elision |

#### `QuantizerSnapDurationTests.cs`

| Fact                                                                | Color | Why                                                                       |
| ------------------------------------------------------------------- | ----- | ------------------------------------------------------------------------- |
| `FourQuarterNotes_In_4_4_Produce_Four_Q_Tokens`                     | GREEN | Tick-clean 4-quarter input; canonical regression — must stay GREEN forever. |
| `Quarter_Note_With_One_Tick_Gap_Still_Snaps_To_Q`                   | RED   | Follower at tick 479 → availableTicks=479 < q-grid=480; falls to (`e`, true). |
| `Quarter_Eighth_Eighth_Pattern_Produces_Q_E_E_In_Order`             | GREEN | Tick-clean Q-E-E; pins composer's canonical rhythm contract.              |
| `Half_Note_When_Next_Note_Is_One_Tick_Early_Still_Snaps_To_H`       | RED   | Follower at tick 959 → availableTicks=959 < h-grid=960; falls to (`q`, true). |

#### `QuantizerRoundingTests.cs`

| Fact                                                    | Color | Why                                                                      |
| ------------------------------------------------------- | ----- | ------------------------------------------------------------------------ |
| `Empty_Leading_Bars_Are_Trimmed`                        | RED   | First note in bar 2 → HEAD emits bar 0 with rest-only contents (BarNumber=0). |
| `Rest_Of_Three_Quarters_Is_Few_Rests_Not_Many`          | RED   | 3-beat gap after a quarter → HEAD emits 3 RestElement entries (`AddRests`). |
| `Two_Octave_Range_Does_Not_Split_RH_LH`                 | RED   | 36-semitone-range single-channel → HEAD splits into `track_ch1_rh` + `track_ch1_lh`. |

#### `FlowGeneratorStructureTests.cs`

| Fact                                                                 | Color | Why                                                                  |
| -------------------------------------------------------------------- | ----- | -------------------------------------------------------------------- |
| `Generated_Output_Has_No_Play_Output_Trailer_When_Round_Trip_Mode`   | RED   | HEAD always emits `(play output)` at line 123 of FlowGenerator.cs.   |
| `One_Sequence_Per_Track_Channel_No_RH_LH_Suffix`                     | RED   | 2-channel × 3-octave fixture → HEAD emits 4 sequences (each split into _rh + _lh). |
| `No_Auto_Fit_Elision_When_All_Quarters_For_Round_Trip`               | RED   | Uniform-duration track → CanAutoFit returns true, elides every `q` suffix. |
| `Mixed_Q_E_Track_Has_Explicit_Durations_On_HEAD_Baseline`            | GREEN | Mixed-duration track defeats CanAutoFit; proves the explicit path exists. |

### HEAD failing-fact count for Plan 30-07/08 green-progress tracking

**`dotnet test flow-midi.Tests` exits non-zero on HEAD with the following totals:**

- 13 total facts
- 5 GREEN (2 harness + 3 baselines pinning the working paths)
- **8 RED** — the GREEN target for Plans 30-07 + 30-08

Filtered breakdown for grep:
```text
Failed:     8, Passed:     5, Skipped:     0, Total:    13
```

`flow-lang.Tests` remains 992/992 GREEN — no cross-project regression. Whole-solution
`dotnet build` exits 0.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking issue] Worktree branch was off the spawn-time base**

- **Found during:** Initial worktree state validation
- **Issue:** The worktree branch HEAD was on `be8c966` (a parallel-history v1.3.0 squashed tag),
  not on the spawn-time base `68f6d2d`. As a result `.planning/`, `.claude/`, and `flow-cli/`
  trees were missing — the plan file itself was not visible.
- **Fix:** Ran the `<worktree_branch_check>` block's prescribed `git reset --hard 68f6d2d`.
  This restored the expected base tree including the planning artifacts. No commits were lost
  (no prior agent commits existed on this branch).
- **Files modified:** none (working tree only; reset was non-destructive)
- **Commit:** n/a (no commit needed for environment fix)

**2. [Rule 1 — Bug in test design] Half-note RED fact's fixture didn't actually trigger Defect 1**

- **Found during:** Task 3 iteration
- **Issue:** Initial fixture for `Half_Note_With_One_Tick_Jitter` placed the second note at tick 960
  (a clean half-bar boundary), giving `availableTicks = 960 ≥ h-grid = 960` — strict-cap passes,
  fact was GREEN-on-HEAD instead of RED.
- **Root cause:** Tracing revealed that `SnapDurationCapped` receives `availableTicks` as its
  cap (not the locally-computed `capped` = min(rawDur, availableTicks)). Defect 1 only fires
  when the GAP to the next event is jittered, not when the note's own duration is jittered.
- **Fix:** Renamed to `Half_Note_When_Next_Note_Is_One_Tick_Early_Still_Snaps_To_H`. Placed
  the follower at tick 959 (1 tick early). `availableTicks` drops to 959 → h-grid=960 > 959 →
  strictly rejected → falls to (`q`, true). Confirmed RED.
- **Files modified:** flow-midi.Tests/Unit/Phase30/QuantizerSnapDurationTests.cs (in same commit `81d2729`)
- **Commit:** `81d2729`

### Authentication Gates

None — no auth needed for any task.

### Architectural Decisions

None — this plan adds test infrastructure only; production code (Quantizer.cs / FlowGenerator.cs)
is intentionally untouched. Plans 30-07 + 30-08 own those rewrites.

## Self-Check

Verifications run before producing this Summary:

**Files exist (all 6 newly-created + 2 modified):**
- `flow-midi.Tests/flow-midi.Tests.csproj` — FOUND
- `flow-midi.Tests/Fixtures/MidiFixtureBuilder.cs` — FOUND
- `flow-midi.Tests/Unit/Phase30/HarnessSmokeFacts.cs` — FOUND
- `flow-midi.Tests/Unit/Phase30/QuantizerSnapDurationTests.cs` — FOUND
- `flow-midi.Tests/Unit/Phase30/QuantizerRoundingTests.cs` — FOUND
- `flow-midi.Tests/Unit/Phase30/FlowGeneratorStructureTests.cs` — FOUND
- `flow-midi/flow-midi.csproj` (modified) — FOUND
- `flow-sharp.sln` (modified) — FOUND

**Commits exist:**
- `a78054a` (Task 1: scaffold flow-midi.Tests project) — FOUND
- `a6c93bc` (Task 2: MidiFixtureBuilder + GREEN HarnessSmokeFacts) — FOUND
- `81d2729` (Task 3: RED-on-HEAD facts pinning Bug B defects 1/2/3) — FOUND

**Build state:** `dotnet build flow-sharp.sln` exits 0 with 0 errors.

**Test state on HEAD (for Plans 30-07/08 baseline):**
- `dotnet test flow-midi.Tests --filter "FullyQualifiedName~HarnessSmokeFacts"` → 2 passed / 0 failed ✓
- `dotnet test flow-midi.Tests --filter "FullyQualifiedName~QuantizerSnapDuration|FullyQualifiedName~QuantizerRounding|FullyQualifiedName~FlowGeneratorStructure"` → 8 RED / 3 GREEN / 11 total ✓
- `dotnet test flow-lang.Tests` → 992 passed / 0 failed ✓

## Self-Check: PASSED
