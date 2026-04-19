---
phase: 11-audit-spike
plan: 05
subsystem: transforms
tags: [audit-spike, transforms, augment, diminish, dismissal, empirical, d-06]

requires:
  - phase: planning
    provides: "CODEBASE-AUDIT-2026-04-18.md §1 C5 claim, ARCHITECTURE.md + PITFALLS.md conflicting interpretations, CONTEXT.md D-06 empirical-test mandate"
provides:
  - "Empirical confirmation that augment/diminish semantic swap (C5) is a false positive"
  - "Greppable `AUDIT-VERIFIED 2026-04-18: C5` markers at TransformFunctions.cs:239 (Augment) and :261 (Diminish)"
  - "tests/spike/c5-augment-diminish.flow GREEN regression guard using visualize ASCII piano-roll observable"
affects: [12-stability-fixes, 11-VERIFICATION.md]

tech-stack:
  added: []
  patterns:
    - "AUDIT-VERIFIED marker pattern (D-02): single-line grep-anchored comment at claim site (second application with TWO markers for a paired-function claim)"
    - "Spike-test convention (D-05/D-08): tests/spike/cN-<slug>.flow exercising the exact claim path"
    - "Visualize-based observability: when pipeline-level observables (renderSong+getFrames) are dominated by padding, fall back to the visualize ASCII piano-roll whose per-note width reflects actual duration"

key-files:
  created:
    - tests/spike/c5-augment-diminish.flow
  modified:
    - flow-lang/StandardLibrary/Transforms/TransformFunctions.cs

key-decisions:
  - "C5 dismissed by empirical test (D-06 mandate honored): visualize output shows augment produces a 4-column `####` bar (half note) and diminish produces a 1-column `#` bar (eighth note) against the 2-column `##` quarter-note control — musically correct in both directions"
  - "Two markers, not one. Unlike C3 (one marker, symmetric loop-guard) and C4 (one marker with verdict text naming both sites), C5 has two independently-sized functions 20 lines apart in the same file. Plan acceptance criteria explicitly required TWO markers and ROADMAP criterion 4 permits this (the markers are comment-only additions)."
  - "Observability pivot: the plan suggested renderSequence+length on the resulting Buffer, but renderSong+getFrames yields bar-padded frame counts (88200 frames for any single-note sequence at 120 BPM/4:4) so it cannot distinguish durations. Adapted to `visualize` which renders per-note width at 2 cols/beat. Frame counts retained in the test as a pipeline health check only."

patterns-established:
  - "Paired-function claim handling: when an audit claim names two sibling functions (e.g. augment line 247 AND diminish line 268), emit two AUDIT-VERIFIED markers — one per function — rather than a single marker referencing both. Keeps the grep surface self-describing (each marker carries its own verdict and evidence snippet) and keeps line-based tooling accurate."
  - "Observability-pivot pattern: when the plan's primary observable is dominated by unrelated state (here, bar-time padding in renderSong), grep-verify available stdlib primitives before authoring the test and adapt to the simplest observable that distinguishes the outcomes. visualize is a lightweight, text-based primitive that's ideal for duration-sensitive claims."

requirements-completed: [SPIKE-05]

duration: ~5min
completed: 2026-04-18
---

# Phase 11 Plan 05: SPIKE-05 — C5 augment/diminish semantic swap — Dismissed

**Empirical `.flow` spike (mandated by D-06 regardless of code reading) confirms C5 is a false positive. `augment` correctly LENGTHENS note durations (quarter → half) and `diminish` correctly SHORTENS them (quarter → eighth). Architecture agent was right; pitfalls agent misread the `NoteValueType.Value` enum ordering (WHOLE=0 → THIRTYSECOND=5), not noticing that `-1` at line 247 moves toward WHOLE which is LONGER, and `+1` at line 268 moves toward THIRTYSECOND which is SHORTER.**

## Verdict

**Dismissed.** Both `augment` and `diminish` produce musically correct output. No FIX-07e sub-requirement needed. No BREAKING CHANGE migration comms triggered.

## Performance

- **Duration:** ~5 min
- **Tasks:** 2
- **Files modified:** 2 (1 created, 1 modified)

## Empirical Evidence

### Test command

```
dotnet run --project flow-interpreter tests/spike/c5-augment-diminish.flow
```

### Stdout (trimmed of build banner/warnings)

```
Flow Language Interpreter v0.1

=== C5: augment/diminish empirical probe ===
c5-quarter-viz:
 C4 |##      |
    +--------+
     1 2 3 4 
c5-quarter-frames: 88200
c5-augmented-viz:
 C4 |####    |
    +--------+
     1 2 3 4 
c5-augmented-frames: 88200
c5-diminished-viz:
 C4 |#       |
    +--------+
     1 2 3 4 
c5-diminished-frames: 88200
c5-probe-complete
```

Exit code: `0`. stderr: empty (no exceptions, no "augment clamped" warning, no "diminish clamped" warning).

### Observation table

| Signal               | Control (Quarter) | augment(Quarter)  | diminish(Quarter) |
|----------------------|-------------------|-------------------|-------------------|
| visualize bar width  | 2 columns (`##`)  | 4 columns (`####`) | 1 column (`#`)    |
| Implied duration     | QUARTER           | HALF (longer)     | EIGHTH (shorter)  |
| getFrames @ 120 BPM 4/4 | 88200 frames   | 88200 frames      | 88200 frames      |
| Enum direction       | 2 (QUARTER)       | 1 (HALF, -1 step) | 3 (EIGHTH, +1 step) |

The visualize column widths are the load-bearing signal. Frame counts are all 88200 because `renderSong` pads each section to its time-signature's bar boundary (4/4 at 120 BPM = 2 seconds = 88200 frames at 44.1 kHz), so buffer length tracks bar count rather than per-note duration. This is a property of the song-rendering pipeline, NOT of augment/diminish. The test documents both signals and notes this explicitly.

### Mapping to the verdict table in the test

From the `c5-augment-diminish.flow` verdict table:
- W_A (4) > W_Q (2) > W_D (1) → **augment lengthens, diminish shortens — musically correct** → **C5 Dismissed**.

## Reasoning confirming the empirical result

```csharp
// TransformFunctions.cs:247 (inside Augment)
int newDur = note.DurationValue.Value - 1; // toward WHOLE=0

// TransformFunctions.cs:268 (inside Diminish)
int newDur = note.DurationValue.Value + 1; // toward THIRTYSECOND=5
```

With `NoteValueType.Value` declared as `WHOLE=0, HALF=1, QUARTER=2, EIGHTH=3, SIXTEENTH=4, THIRTYSECOND=5` (NoteValueType.cs:22-30), the arithmetic is:

- `augment(QUARTER=2)` = `2 - 1` = `1` = **HALF** (longer, musically correct)
- `diminish(QUARTER=2)` = `2 + 1` = `3` = **EIGHTH** (shorter, musically correct)

The visualize output confirms the runtime agrees with this static reading. The pitfalls researcher likely assumed enum values increase with duration (WHOLE=5, THIRTYSECOND=0) which would invert the verdict — but the actual ordering makes longer durations have smaller numeric values, so `-1` is the lengthening operation.

## Why the plan's suggested observable (frame count) did not work

The plan's primary suggestion was "render to buffer, print the frame count; longer sequence = longer note". At first pass:

- Plan predicted: quarter=22050 frames, augment→44100, diminish→11025.
- Observed: all three = 88200 frames.

Root cause: `renderSong(Song, String) -> Buffer` (SongRenderer.cs:82) walks `song.Sections`, renders each section to a bar-padded buffer via `RenderSection`, and concatenates. A section containing a single quarter note in a 4/4 bar renders to a full bar of audio (2 s at 120 BPM = 88200 frames at 44.1 kHz stereo), with silence padding the last 3 beats. Augmenting the quarter to a half still fits within the bar, and diminishing it to an eighth leaves more silence, but the bar itself remains 4 beats / 2 seconds, so the buffer length is identical.

`renderSequence` returns `Voice[]` rather than a `Buffer` (notation.flow:201), so it doesn't expose a `getFrames`-style observable, and there's no `head(Sequence)` → `Bar` primitive to enable stringification of individual bars (confirmed by running `(head sQuarter)` which errors with "No matching overload for function 'head' with argument types (Sequence)").

`str(Sequence)` (BuiltInFunctions.cs:115) prints `Sequence[N bars, M beats total]` — uniform across augment/diminish because bar count and total beats are time-signature-level properties, not duration-level.

The `visualize` built-in (VisualizationFunctions.cs:32) renders at 2 columns per beat and explicitly uses each note's `GetBeats(timeSigDenom)` for its per-note width, which is exactly the signal that varies with augment/diminish. Confirmed with a quick probe: quarter → 2 columns, augment → 4, diminish → 1. Signal locked in.

This adaptation is the plan's explicit fallback clause ("If `renderSequence` or `length` on a Buffer is not a real built-in, adapt by using whatever sequence/buffer length primitive the stdlib exposes — grep first") applied with visualize as the chosen fallback. Test still produces both signals so future auditors can see the padding behavior too.

## Inline Markers

Both markers added to `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs`:

### Marker 1 — Augment

- **File:** `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs`
- **Line:** 239 (within required [237, 241] window)
- **Text:** `// AUDIT-VERIFIED 2026-04-18: C5 — augment correct (lengthens); observed A=#### vs Q=## columns in visualize (tests/spike/c5-augment-diminish.flow)`
- **Precedes:** `private static Value Augment(IReadOnlyList<Value> args)` at line 240

### Marker 2 — Diminish

- **File:** `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs`
- **Line:** 261 (within required [258, 262] window)
- **Text:** `// AUDIT-VERIFIED 2026-04-18: C5 — diminish correct (shortens); observed D=# vs Q=## columns in visualize (tests/spike/c5-augment-diminish.flow)`
- **Precedes:** `private static Value Diminish(IReadOnlyList<Value> args)` at line 262

### Checks

- `grep -c "AUDIT-VERIFIED 2026-04-18: C5" flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` → `2` (required: exactly 2)
- `git diff --stat cd2a9d6^..cd2a9d6 -- flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` → `2 insertions(+), 0 deletions(-)` (required: +2/-0)
- No modifications to the Augment or Diminish method bodies.
- `dotnet build flow-lang/flow-lang.csproj` succeeds: 0 errors, 3 pre-existing nullability warnings identical to 11-01..11-04 runs.

## BREAKING CHANGE trigger? — No

Verdict is Dismissed. Phase 12 FIX-07e is NOT required, and ROADMAP Phase 12 success-criterion 4 (BREAKING-CHANGE migration comms with `augmentV1`/`diminishV1` aliases) is NOT triggered. The semantics the audit feared would flip under a fix are the semantics the code already has — no migration story needed.

## Task Commits

1. **Task 1: Author tests/spike/c5-augment-diminish.flow — MANDATORY empirical test per D-06** — `4c0e826` (test)
2. **Task 2: Record TWO inline AUDIT-VERIFIED comments in TransformFunctions.cs** — `cd2a9d6` (docs)

_Final metadata commit (this SUMMARY) will follow this doc._

## Files Created/Modified

- `tests/spike/c5-augment-diminish.flow` — created (4610 bytes). Runs augment(quarter) and diminish(quarter), captures visualize output AND renderSong frame count for both transforms vs. the quarter-note control. Includes verdict table correlating visualize widths to the Dismissed / Confirmed / Broken outcomes.
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` — modified. `+2 insertions, 0 deletions`. Two AUDIT-VERIFIED comment lines only, no logic change.

## Decisions Made

- **Honored D-06 despite code-reading being sufficient.** A careful read of the enum ordering (WHOLE=0 at the top, THIRTYSECOND=5 at the bottom) combined with `-1`/`+1` arithmetic already settles the claim statically. D-06 explicitly forbids reasoning-only dismissal for C5, so an empirical test was authored anyway — which turned out to be valuable, because it also surfaced the frame-count-padding behavior that a naive empirical approach would have misinterpreted.
- **Two markers, not one (vs. C4's single marker with dual-site text).** C4 had two division sites inside functions with identical loop-guard structure ~30 lines apart in the same file; one marker could honestly summarize both sites' shared rationale. C5 has two distinct functions each with its own enum arithmetic (`-1` vs `+1`), and the plan explicitly required two markers in the acceptance criteria. Keeping them separate also makes the grep surface accurate: a future audit grepping for "augment" won't miss the Augment marker, and same for diminish.
- **visualize over renderSequence or head(Sequence).** `renderSequence` returns `Voice[]`, not a `Buffer`, so it doesn't compose with `getFrames`. `head(Sequence) -> Bar` isn't registered (probed, errors with "No matching overload"). `str(Sequence)` is uniform across transforms. visualize hits the sweet spot: pure text observable, per-note width derived from `note.GetBeats(timeSigDenom)`, zero additional dependencies. The test documents the decision in its header block.
- **Frame counts retained in the test (even though uniform).** They serve as a pipeline health check — a regression that broke renderSong/getFrames for augmented or diminished sequences would change those numbers. Cheap signal worth keeping.
- **Kept the test GREEN (exit 0, no stderr).** Per D-08, a Dismissed claim has its test land green. If C5 had been Confirmed, the test would instead land RED by asserting the correct musical direction so Phase 12 could flip it green. No such assertion was needed here — the test is descriptive (prints evidence) rather than prescriptive, which matches the GREEN convention established by 11-02/11-03/11-04.

## Deviations from Plan

**1. [Rule 3 — Observability pivot] Switched from `renderSequence`+length-on-buffer to `visualize` ASCII output.**
- **Found during:** Task 1 draft execution (first run showed all three buffers at 88200 frames — same value for quarter/augment/diminish).
- **Issue:** The plan's suggested primary observable (frame count via renderSequence-then-length-on-Buffer) cannot distinguish the three outcomes because (a) `renderSequence` returns `Voice[]` not Buffer, and (b) routing through `renderSong`+`getFrames` yields bar-padded frame counts that are uniform across transforms at single-bar sequences.
- **Fix:** Used `visualize` which renders at 2 columns per beat and whose per-note width reflects actual duration. Quarter=2 cols, augment→4 cols, diminish→1 col — unambiguous. Frame counts retained as secondary signal for pipeline health.
- **Sanctioned by plan:** Yes. The plan's `<flow-transform-api>` block explicitly says "If `renderSequence` or `length` (on a Buffer) is not a real built-in, adapt by using whatever sequence/buffer length primitive the stdlib exposes — grep first. If buffer length is inaccessible, fall back to printing the sequence directly ... or parsing the string representation for duration codes." I extended that adaptive clause to include visualize, whose role is closer in spirit to what the plan wanted.
- **Files modified:** `tests/spike/c5-augment-diminish.flow` (the adapted test is what was committed; no intermediate version landed in git).
- **Commit:** `4c0e826` (the adapted Task 1 commit itself).

This is a Rule-3 adaptation (choose the primitive that works) not a Rule-4 architectural change (no new stdlib primitives introduced, no audit scope changes).

## Issues Encountered

- `tests/` is gitignored (`.gitignore:7: tests/`, `.gitignore:13: *.flow`). Resolved using `git add -f`, matching the established Phase 11 convention (used by 11-01 `2b59433`, 11-02 `b01359f`, 11-03 `0720fb7`, 11-04 `57293b9`). Not a deviation — this is the cross-plan convention.
- First attempt at `(head sQuarter)` to retrieve a Bar for stringification failed: `No matching overload for function 'head' with argument types (Sequence)`. No such stdlib primitive exists. Informational — helped select visualize.

## Self-Check

- `tests/spike/c5-augment-diminish.flow` present (`ls -la` confirms, 4610 bytes).
- Commit `4c0e826` reachable (`git log --oneline` confirms).
- Commit `cd2a9d6` reachable (`git log --oneline` confirms).
- `grep -c "AUDIT-VERIFIED 2026-04-18: C5" flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` == `2`.
- Marker 1 at line 239 (within required [237, 241] window, directly above `Augment` at line 240).
- Marker 2 at line 261 (within required [258, 262] window, directly above `Diminish` at line 262).
- Both markers include numeric evidence (A=####, D=#, Q=##).
- `git diff --stat cd2a9d6^..cd2a9d6 -- flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` == `2 insertions(+), 0 deletion(-)`.
- `dotnet build flow-lang/flow-lang.csproj` passes with 0 errors, 3 pre-existing unrelated nullability warnings.
- Spike stdout contains required labels: `c5-quarter-frames:`, `c5-augmented-frames:`, `c5-diminished-frames:` each followed by a numeric line, plus matching `c5-*-viz` blocks.
- Verdict derived from visualize observation: W_A (4) > W_Q (2) > W_D (1) → Dismissed.
- D-06 observed: empirical test exists and carries verdict-settling runtime data, even though static code-reading alone would suffice.

## Self-Check: PASSED

## Next Action

→ **Closed.** No Phase 12 fix task needed for C5. The `11-VERIFICATION.md` row for C5 will read:

| Claim | Verdict   | Evidence                                                                                                            | Next Action |
|-------|-----------|---------------------------------------------------------------------------------------------------------------------|-------------|
| C5    | Dismissed | `tests/spike/c5-augment-diminish.flow` + `TransformFunctions.cs:239` and `TransformFunctions.cs:261` (two markers)  | Closed      |

Per D-04, this claim does NOT produce a `FIX-07e` sub-requirement; the REQUIREMENTS.md split at the end of Phase 11 should drop C5 from the stability-contingent queue. Per ROADMAP Phase 12 success criterion 4, no BREAKING CHANGE migration is triggered.

---
*Phase: 11-audit-spike*
*Completed: 2026-04-18*
