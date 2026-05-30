---
phase: 46-codebase-bloat-removal
plan: 05
subsystem: testing
tags: [progression-dsl, voice-leading, showcase, legacy-docs, keep-treatment, xunit]

# Dependency graph
requires:
  - phase: 15-euclidean
    provides: EuclideanSwingTests structured-Sequence assertion idiom (FlowEngineRunner.RunSource + GetVariable)
provides:
  - ProgressionDslTests (5 Facts) — first-ever unit coverage for the kept Progression DSL (progression | I IV V |)
  - Non-rendered progression demo in examples/showcase.flow (byte-identical flow_showcase.wav)
  - Comment-only legacy keep-notes on 5 superseded-but-kept surfaces (Timeline/Track/Bars/bars.flow/composition.flow)
affects: [46-codebase-bloat-removal closer, future showcase-polish pass, Phase 48 WasmDeterminismTests isolation follow-up]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Declare-outside / assign-inside a key { } block so a block-scoped progression Sequence lands in the global frame for GetVariable probing"
    - "Option A non-rendered demo: showcase a language surface without feeding it into the Song render graph, preserving WAV byte-identity"

key-files:
  created:
    - flow-lang.Tests/Unit/Phase46/ProgressionDslTests.cs
    - .planning/phases/46-codebase-bloat-removal/deferred-items.md
  modified:
    - examples/showcase.flow
    - flow-lang/StandardLibrary/Audio/Timeline.cs
    - flow-lang/StandardLibrary/Audio/Track.cs
    - flow-lang/StandardLibrary/Bars.cs
    - flow-lang/bars.flow
    - flow-lang/composition.flow

key-decisions:
  - "D-12 showcase chose Option A (non-rendered demo) over Option B (replace pad + refresh baseline) — pure-cleanup phase keeps flow_showcase.wav byte-identical (9a4b4648... before and after)"
  - "D-16 notes are plain comments only — NO [Obsolete]/[Deprecated] attributes, NO stderr advisories (pre-traction, per project no-deprecation latitude)"

patterns-established:
  - "Pattern 1: Kept-but-superseded surfaces carry a one-to-two-line comment-only 'legacy / superseded by X — kept as a usable surface' note pointing at the canonical path"

requirements-completed: [CLEAN-12, CLEAN-16]

# Metrics
duration: 18min
completed: 2026-05-30
---

# Phase 46 Plan 05: Progression DSL Invest + Legacy Keep-Notes Summary

**Added the first-ever 5-Fact ProgressionDslTests for the kept `progression | I IV V |` DSL plus a non-rendered showcase demo (byte-identical WAV), and tagged 5 superseded-but-kept surfaces with comment-only legacy notes — no deprecation attributes.**

## Performance

- **Duration:** ~18 min
- **Started:** 2026-05-30
- **Completed:** 2026-05-30
- **Tasks:** 2
- **Files modified:** 6 (2 created, 6 modified incl. deferred-items.md)

## Accomplishments

- **D-12 (CLEAN-12):** `flow-lang.Tests/Unit/Phase46/ProgressionDslTests.cs` — 5 xUnit Facts mirroring the `EuclideanSwingTests` idiom: (1) `progression | I IV V I |` → 4 bars; (2) `:N` bar-count suffix (`| I:2 V |`) → 3 bars; (3) `voices 4` modifier → exactly 4 voiced notes per bar; (4) no-key error case (ErrorCount > 0 + "progression requires an active key context" stderr substring); (5) voice-leading determinism (two fresh engines → identical MIDI-pitch sequence). All 5 GREEN.
- **D-12 showcase:** `examples/showcase.flow` gains a non-rendered `Sequence progDemo = progression | I IV V I |` + a `(print ...)` describing it, placed alongside the hand-written pad but kept OUT of `section showcase`/`Song`. Verified byte-identical render: `flow_showcase.wav` SHA-256 `9a4b4648ea78d638aa0c118998943b65c83011a126690f5db5badf54b6ee8398` both before and after the change.
- **D-16 (CLEAN-16):** Comment-only "legacy / superseded by X — kept as a usable surface" notes added to Timeline.cs + Track.cs (→ Song/Section `SongRenderer`), Bars.cs + bars.flow (→ `| C4 D4 E4 |` note-stream literal / `NoteStreamCompiler`), and composition.flow (legacy fluent Track wrappers → Song/Section). `std.flow:6 use "@bars"` import retained (D-11). NO `[Obsolete]`/`[Deprecated]` attributes, NO stderr advisories.

## Task Commits

1. **Task 1: Add ProgressionDslTests + non-rendered showcase demo (D-12)** — `65a024b` (test)
2. **Task 2: Add comment-only legacy notes to Track/Timeline + bars.flow (D-16)** — `32c6db8` (docs)

_Note: Task 1 is a single test commit — the Progression DSL is an EXISTING kept feature (D-01), so this plan adds coverage rather than driving new behavior; the tests are GREEN on first run against the existing implementation (no RED/GREEN split applies)._

## Files Created/Modified

- `flow-lang.Tests/Unit/Phase46/ProgressionDslTests.cs` (created) — 5 Facts covering 4-bar output, `:N` suffix, `voices N`, no-key error, voice-leading determinism
- `examples/showcase.flow` (modified) — non-rendered progression demo + print line
- `flow-lang/StandardLibrary/Audio/Timeline.cs` (modified) — legacy keep-note on the `Timeline` `<summary>`
- `flow-lang/StandardLibrary/Audio/Track.cs` (modified) — legacy keep-note on the `Track` class header
- `flow-lang/StandardLibrary/Bars.cs` (modified) — legacy keep-note on the `Bars` `<summary>`
- `flow-lang/bars.flow` (modified) — legacy keep-note in the `Note:` file header
- `flow-lang/composition.flow` (modified) — legacy keep-note in the `Note:` file header
- `.planning/phases/46-codebase-bloat-removal/deferred-items.md` (created) — logs the pre-existing Phase48.WasmDeterminismTests full-suite isolation flake

## Decisions Made

- **Option A for the showcase demo** (non-rendered) — chosen per RESEARCH §D-12 recommendation + Open Q1; keeps this pure-cleanup phase byte-identical. Option B (replace the pad, refresh the RMS baseline) is deferred to a future showcase-polish pass.
- **Comment-only D-16 notes** — no deprecation machinery, honoring the project's pre-traction no-deprecation latitude.

## Deviations from Plan

### Rule 1 — Test-harness scoping fix (during Task 1, before commit)

**1. [Rule 1 - Bug] RunProg helper read a block-scoped variable from the global frame**
- **Found during:** Task 1 (initial test run)
- **Issue:** The RESEARCH §D-12 idiom sketch declared `Sequence s` INSIDE the `key Cmajor { }` block. Variables declared inside a musical-context block are scoped to that block, so `FlowEngineRunner.GetVariable("s")` (which reads `GlobalFrame`) threw `Variable 's' not found` — 4 of 5 Facts RED.
- **Fix:** Changed the `RunProg` helper to declare the binding at top level (`Sequence s = | C4q |`) then ASSIGN the progression inside the `key` block (`s = progression | ... |`), so the compiled Sequence lands in the global frame. Verified the assign-inside-block pattern populates `s` against the live interpreter before re-running.
- **Files modified:** flow-lang.Tests/Unit/Phase46/ProgressionDslTests.cs
- **Verification:** All 5 Facts GREEN after the fix.
- **Committed in:** `65a024b` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 test-harness bug). Caught and fixed within Task 1 before commit; no scope creep.
**Impact on plan:** The fix is a test-helper correction only; the production Progression DSL is unchanged.

## Issues Encountered

- **Phase48.WasmDeterminismTests full-suite flake (PRE-EXISTING, out of scope):** The full `dotnet test` run reports 2 failures — both in `Phase48.WasmDeterminismTests` — but both PASS in isolation (`--filter "FullyQualifiedName~WasmDeterminismTests"` → 2/2 green). These tests share a lazy-init `_sharedEngine` and redirect `Console.SetOut`/`SetError`, and race with other Console-redirecting fixtures under full-suite parallel execution. This plan's diff (vs spawn base `7992435`) touches zero WASM/WasmEntry/Console-redirection production code, so the failures are not caused by Plan 46-05. Logged to `46/deferred-items.md` for a Phase 48 isolation follow-up; not fixed (scope boundary).

## Verification Results

- **D-12:** ProgressionDslTests 5/5 GREEN. `dotnet run --project flow-interpreter examples/showcase.flow` runs clean (prints the demo line). `flow_showcase.wav` byte-identical (`9a4b4648...`) before and after the showcase edit — render graph unchanged.
- **D-16:** All 5 surfaces carry a `[Ll]egacy` comment (grep confirms). `grep "Obsolete\|Deprecated"` over the 5 files returns NONE. `std.flow:6 use "@bars"` retained. `dotnet build flow-lang/flow-lang.csproj` → 0 errors. `dotnet build` (solution) → 0 errors.
- **Anti-scope KEEPs (per D-01):** D-10 (Track/Timeline), D-11 (bars.flow), D-13 (OscillatorState/Envelope), D-14 (audio.flow buffer convenience), D-15 (`preview`) are all UNTOUCHED by removal in this plan; D-17 phase-breadth honored (audit §3 out of scope). This plan only ADDS a test, a non-rendered demo line, and comment-only docs.

## Threat Flags

None — no new network endpoints, auth paths, file-access patterns, or schema changes. This plan adds a unit test, a non-rendered demo line, and comment-only documentation; no production logic changed.

## Known Stubs

None — no hardcoded empty values, placeholder text, or unwired data sources introduced.

## Next Phase Readiness

- Progression DSL now has real unit coverage; the kept-surface legacy notes give future readers the canonical path.
- Phase 46 closer can proceed. One deferred item recorded for a Phase 48 test-isolation follow-up (does not block Phase 46).

## Self-Check: PASSED

- All created/modified files exist on disk (9/9 FOUND).
- Both task commits present in git history (`65a024b`, `32c6db8`).

---
*Phase: 46-codebase-bloat-removal*
*Completed: 2026-05-30*
