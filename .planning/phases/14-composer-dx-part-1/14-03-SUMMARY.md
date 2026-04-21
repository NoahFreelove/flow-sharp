---
phase: 14-composer-dx-part-1
plan: 03
subsystem: testing
tags: [midi, velocity, dynamics, crescendo, integration-test, two-pass-strict, drywetmidi, regression-test]

# Dependency graph
requires:
  - phase: 12-stability
    provides: "Stable dynamics pipeline (Interpreter.cs→NoteStreamCompiler.cs→MidiExport.cs) established end-to-end"
  - phase: 13-nyquist-validation-backfill
    provides: "Two-pass strict authorship pattern (D-13), observable-value pin convention (D-11), and Integration/Phase{NN}/ test directory layout (D-09)"
provides:
  - "Observable-value regression pin for MIDI velocity bytes on the full dynamics→MIDI export chain"
  - "New .flow script tests/test_dynamics_midi_velocity.flow exercising crescendo 0.25→0.75 over 5 notes with writeMidi output"
  - "xUnit Fact flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs reading MIDI via DryWetMidi and asserting exact velocity byte sequence [31, 47, 63, 79, 95]"
  - "F-01 confirmation: the Phase-12-closed velocity chain (Interpreter→MusicalContext→NoteStreamCompiler→MidiExport) is wired end-to-end with zero gap-fix work required"
  - "Empirical evidence that Pass 2 matched Pass 1 REQUIREMENTS-drafted expectations verbatim — third zero-divergence outcome in the two-pass strict series (after 13-01 and 13-04)"
affects: [15-composer-dx-part-2, 16-tutorial-refresh, DX-09 euclidean humanize, future MIDI-export work]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Melanchall.DryWetMidi 8.0.3 MidiFile.Read + GetNotes + (byte)Note.Velocity read path for Integration Facts"
    - "Two-pass strict authorship (Phase 13 D-13) applied to a code-phase (not pure-docs) regression test"
    - "Environment.CurrentDirectory cd-to-repo-root pattern for writeMidi relative-path resolution in Facts"
    - "Inline MIDI-read helper (not promoted to Shared) until a second Fact duplicates the call shape"

key-files:
  created:
    - "tests/test_dynamics_midi_velocity.flow — 5-note crescendo 0.25→0.75 with writeMidi to tests/output/dynamics_velocity.mid"
    - "flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs — xUnit Fact asserting velocity bytes equal [31, 47, 63, 79, 95]"
  modified: []

key-decisions:
  - "Outcome A (GREEN on first run): F-01 confirmed, zero plumbing changes required"
  - "Pass 1 draft from REQUIREMENTS.md DX-08 wording alone produced byte-exact match with Pass 2 reality — no Divergence"
  - "Inline MIDI-read helper retained (no Shared/MidiReadHelpers.cs promotion) — defer until a second Fact duplicates"
  - "Zero new NuGet packages (DryWetMidi 8.0.3 already in flow-lang.csproj transitively available to flow-lang.Tests)"

patterns-established:
  - "Pattern: Two-pass strict authorship works cleanly on code-phase plans (not only docs-phase backfills)"
  - "Pattern: When v1.1 audit + Phase 12 stability have already reconciled requirements-vs-reality, Pass 1 and Pass 2 match verbatim — third consecutive zero-divergence plan"
  - "Pattern: RESEARCH §Velocity Chain Audit trace of Interpreter.cs:184-191 → NoteStreamCompiler.cs:324/341 → TransformFunctions.cs:401-529 → MidiExport.cs:191-199 was predictively accurate"

requirements-completed: [DX-08]

# Metrics
duration: ~4min
completed: 2026-04-20
---

# Phase 14 Plan 03: DX-08 MIDI Velocity Regression Summary

**End-to-end velocity byte regression pin at [31, 47, 63, 79, 95] via DryWetMidi read-back — Pass 2 GREEN on first run (F-01 confirmed); zero plumbing changes, plan shipped in a single Pass 1 commit.**

## Outcome

**Outcome A** — GREEN on first run. The Pass 1 draft's expected velocity byte sequence matched real code output exactly. No gap-fix work required, no Divergence entry, no Pass 2 commit.

## Performance

- **Duration:** ~4 min
- **Started:** 2026-04-20T15:09:00Z
- **Completed:** 2026-04-20T15:13:19Z
- **Tasks:** 2 (Task 1 Pass 1 draft, Task 2 Pass 2 reconcile)
- **Files created:** 2
- **Files modified:** 0

## Accomplishments

- DX-08 observable-value regression pin now gates the full dynamics→MIDI export chain
- F-01 hypothesis confirmed empirically: Phase-12-closed velocity pipeline is wired end-to-end with no missing plumbing
- Third consecutive zero-divergence plan in the two-pass strict series (after 13-01 and 13-04) — the pattern demonstrates that when requirements-vs-reality has been reconciled, Pass 1 drafted expectations match verbatim

## Task Commits

1. **Task 1: Pass 1 Draft** — `152e593` (test)
   - Subject: `test(14-03): DX-08 draft — two-pass strict pass 1 (velocity gradient regression)`
   - Created tests/test_dynamics_midi_velocity.flow and flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs
   - Drafted from REQUIREMENTS.md DX-08 wording alone per CONTEXT D-13 (no reading of Interpreter.cs / NoteStreamCompiler.cs / TransformFunctions.cs / MidiExport.cs during draft)

2. **Task 2: Pass 2 Reconcile** — no additional commit (Outcome A)
   - Ran `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase14.DynamicsMidiVelocityTests"` → GREEN on first invocation
   - Ran full suite `dotnet test flow-sharp.sln` → 81/81 PASSED (pre-14-03 baseline 79 + new Fact + new Theory row = 81)
   - Verified `tests/output/dynamics_velocity.mid` written (101 bytes)
   - No source changes required; Pass 1 commit is the sole commit for this plan

## Files Created

- **`tests/test_dynamics_midi_velocity.flow`** — 17-line .flow script:
  - `use "@std"` + `use "@audio"` prelude
  - `tempo 120 { timesig 4/4 { ... } }` nesting (mirrors tests/test_midi_export.flow)
  - `Sequence base = | C4 D4 E4 F4 G4 |` — deterministic 5-note sequence
  - `Sequence curve = base -> crescendo 0.25 0.75` — explicit numeric dynamic bounds bypass `dynamics f` velocity-constant ambiguity
  - `section s { curve }` + `Song song = [s]` + `(writeMidi "tests/output/dynamics_velocity.mid" song)`
  - Terminal sentinel `(print "dynamics_velocity: PASSED")` for FlowScriptData Theory-row default-gate

- **`flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs`** — 60-line xUnit Fact:
  - Namespace `FlowLang.Tests.Integration.Phase14` (Phase 13 D-09 convention)
  - `[Collection("FlowScripts")]` — serializes Console.SetOut across parallel Facts
  - `using Melanchall.DryWetMidi.Core;` + `using Melanchall.DryWetMidi.Interaction;` — DryWetMidi 8.0.3 read API
  - Sets `Environment.CurrentDirectory` to repo root via `FlowScriptData.FindTestsRoot()` + `.Parent` so `writeMidi` resolves relative paths predictably
  - Pre-creates `tests/output/` safety net + deletes stale MIDI file for idempotency
  - Calls `runner.RunFile(testScript)` and asserts `success`, `errorCount == 0`, `File.Exists(outputMidi)`
  - `MidiFile.Read(outputMidi).GetNotes().Select(n => (byte)n.Velocity).ToArray()` — reads velocity bytes
  - `Assert.Equal(new byte[] { 31, 47, 63, 79, 95 }, velocities)` — observable-value pin
  - `try/finally` restores `Environment.CurrentDirectory` to `originalCwd`

## Decisions Made

- **Outcome A accepted (no Pass 2 commit)** — per plan STEP 4, if GREEN on first run "the plan ships with just the Pass 1 commit from Task 1". No empty-diff verification commit required by project convention; the plan closes with a single commit plus this SUMMARY.md.
- **Shared helper promotion deferred** — per CONTEXT §Claude's Discretion, the inline `MidiFile.Read(...).GetNotes().Select(...).ToArray()` helper is NOT promoted to `flow-lang.Tests/Shared/MidiReadHelpers.cs`. Promotion candidates: Phase 15 DX-09 (euclidean humanize) may duplicate the same read path, at which point extraction becomes natural. Phase 14 Plan 03 ships with the inline helper.
- **Force-add required for tests/*.flow** — the repo's .gitignore ignores `tests/` and `*.flow` wholesale, but existing `tests/*.flow` files are tracked. Used `git add -f tests/test_dynamics_midi_velocity.flow` to add the new script. This is the established pattern (confirmed via `git ls-files tests/test_crescendo.flow` showing tracked status).

## Deviations from Plan

None — plan executed exactly as written. Pass 1 draft was committed verbatim; Pass 2 ran GREEN on first invocation; no source changes, no additional commits, no Divergences.

## Divergences

None. Expected velocity byte sequence `[31, 47, 63, 79, 95]` matched Pass 2 reality byte-for-byte.

This is the third consecutive zero-divergence plan in the two-pass strict series (13-01, 13-04, 14-03), validating the protocol: when the v1.1 audit + Phase 12 stability have already reconciled requirements-vs-reality, Pass 1 and Pass 2 match verbatim.

## F-01 Confirmation

RESEARCH §"DX-08 Velocity Chain Audit" hypothesized the chain `Interpreter.cs:184-191 → NoteStreamCompiler.cs:324,341 → TransformFunctions.cs:401-529 → MidiExport.cs:191-199` was wired end-to-end. Pass 2 confirms:

- `crescendo(seq, 0.25, 0.75)` over 5 non-rest notes produces per-note `Velocity` values `[0.250, 0.375, 0.500, 0.625, 0.750]` (TransformFunctions.Crescendo linear interpolation)
- `MidiExport.cs` velocity byte emission `(byte)Math.Clamp((int)(v * 127), 1, 127)` produces `[31, 47, 63, 79, 95]` (truncation, not round)
- DryWetMidi 8.0.3 `MidiFile.Read` + `GetNotes()` + `(byte)Note.Velocity` reads back those exact bytes

No plumbing was missing. F-01 is empirically closed.

## Test Counts Delta

- **Pre-14-03 baseline:** 79 tests (post-Phase-13 close)
- **Post-14-03:** 81 tests (+1 Fact: `Phase14.DynamicsMidiVelocityTests.Crescendo_EmitsExpectedVelocityGradient`; +1 auto-globbed Theory row: `FlowScriptTests.RunsToCompletion(relativePath: "test_dynamics_midi_velocity.flow")`)
- **Regression check:** 0 pre-existing tests flipped RED; full `dotnet test flow-sharp.sln` suite 81/81 GREEN

## Issues Encountered

None. One procedural note: `.gitignore` ignores `tests/` globally, but existing tests/*.flow files are explicitly tracked in the repo. `git add -f` is the correct path for new tests/*.flow files, and this was anticipated in the plan text (references to `git add` outside the declared `files_modified`).

## Success Criteria Check

- [x] DX-08 success criterion 4 (ROADMAP): `.flow` script using `dynamics`/`crescendo`/`decrescendo`/`swell` exports MIDI with velocity bytes in 1–127 range with expected gradient; regression test asserts the velocity byte sequence. **Pinned** by `DynamicsMidiVelocityTests.Crescendo_EmitsExpectedVelocityGradient` asserting `new byte[] { 31, 47, 63, 79, 95 }`.
- [x] CONTEXT D-13 two-pass strict authorship executed end-to-end. **Pinned** by commit `152e593` existing prior to Pass 2 verification + this SUMMARY.md recording Outcome A.
- [x] CONTEXT D-14 purpose-built test (not modification of tests/test_dynamics.flow or tests/test_crescendo.flow). **Pinned** by `git show --stat HEAD` showing only two NEW files created; no diffs to pre-existing dynamics scripts.
- [x] CONTEXT D-15 Fact location convention. **Pinned** by namespace `FlowLang.Tests.Integration.Phase14` + file at `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs`.
- [x] No new NuGet packages. **Pinned** by no diff to `flow-lang.Tests.csproj` (DryWetMidi 8.0.3 inherited via `<ProjectReference Include="..\flow-lang\flow-lang.csproj" />`).

## Phase-Level Verification Checks

- [x] `dotnet build flow-sharp.sln` exits 0 after Pass 1 commit (5 warnings, 0 errors — warnings pre-existing, unrelated)
- [x] `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase14.DynamicsMidiVelocityTests"` — GREEN (1/1 passed)
- [x] `dotnet test flow-sharp.sln` (full suite) — 81/81 GREEN
- [x] `tests/output/dynamics_velocity.mid` present after Fact run (101 bytes, DryWetMidi-readable)
- [x] `tests/test_dynamics_midi_velocity.flow` Theory row GREEN with `dynamics_velocity: PASSED` sentinel + clean stderr
- [x] 14-03-SUMMARY.md written with Outcome A note
- [x] No pre-existing Fact flipped RED

## Next Phase Readiness

- DX-08 closed; REQUIREMENTS.md Traceability row ready to flip from "Pending" to "Shipped 152e593" (to be executed by plan 14-04)
- DX-05 (slice) and DX-06 (flats + enharmonic) still in flight on parallel worktree agents 14-01 and 14-02; 14-03 is independent and adds no file-level coupling
- Phase 15 DX-09 (euclidean humanize) will likely reuse the DryWetMidi read path — recommended to promote MIDI-read helper to `flow-lang.Tests/Shared/MidiReadHelpers.cs` during plan 15-XX when the second usage arrives

## Self-Check

- [x] `tests/test_dynamics_midi_velocity.flow` — FOUND (verified via `test -f`)
- [x] `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs` — FOUND (verified via `test -f`)
- [x] Commit `152e593` — FOUND (verified via `git log --oneline -5`)
- [x] Output file `tests/output/dynamics_velocity.mid` — FOUND (101 bytes, verified via `ls -la`)

## Self-Check: PASSED

---

*Phase: 14-composer-dx-part-1*
*Plan: 03 — DX-08 MIDI velocity regression*
*Completed: 2026-04-20*
*Outcome: A (GREEN on first run — F-01 confirmed, zero-divergence)*
