---
phase: 22-tier-b-c-composer-dx-bundle
plan: 01
subsystem: harmony
tags: [arpeggio, harmony, overload, charitable-interpretation, dx-10]

requires:
  - phase: 14-composer-dx-part-1
    provides: existing 2-arg arpeggio(Chord, String) registration in HarmonyFunctions
  - phase: 18-foundation-rational-duration-arithmetic
    provides: byte-identical regression gate (Tutorial + Showcase)
provides:
  - "4-arg arpeggio(Chord, NoteValue, String, String) overload with rate + direction + pattern parameters"
  - "ApplyDirection helper supporting up/down/updown/downup with apex/nadir-skip semantics"
  - "Charitable random→up fallback (Pitfall 7) preserving byte-identical determinism in v1.3"
affects: [22-02-voicings, 22-03-delay-sync, 22-04-quantize, 22-05-legato-portamento, 22-06-loadwav-varispeed, 22-07-closure]

tech-stack:
  added: []
  patterns:
    - "Sibling-overload registration alongside existing 2-arg signature (preserves byte-identical regression)"
    - "Charitable fallback in switch-default (no error path on unknown direction string)"
    - "Pattern arg accepted at signature but consumed by future expansion (deferred routing)"

key-files:
  created:
    - flow-lang.Tests/Unit/Phase22/ArpeggioFacts.cs
    - tests/test_dx_arpeggio.flow
  modified:
    - flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs
    - flow-lang/std.flow
    - flow-lang.Tests/FlowScriptData.cs

key-decisions:
  - "DX-10 4-arg arpeggio extends in place: existing 2-arg registration kept byte-identical, sibling 4-arg registered alongside (per CONTEXT D-08 / Anti-Patterns: do NOT create arpeggio2)"
  - "ApplyDirection switch default arm returns input unchanged — charitable interpretation per CLAUDE.md memory; unknown direction strings fall through to up rather than throwing"
  - "random direction maps to up in v1.3 (RESEARCH Pitfall 7); seeded random arpeggio deferred to v1.4 to preserve byte-identical determinism"
  - "chord-tone / scale-tone patterns route to linear in v1.3 (RESEARCH Assumption A8 / REQUIREMENTS line 105); pattern arg accepted at signature but consumed for future expansion only"

patterns-established:
  - "Pattern: Phase 22 sibling-overload registration — new overload added immediately after existing same-name signature, preserves regression-test bytes for the existing 2-arg path"
  - "Pattern: ToLowerInvariant() preferred over ToLower() in switch dispatch for culture-stable matching"

requirements-completed: [DX-10]

duration: 5min
completed: 2026-05-02
---

# Phase 22 Plan 01: DX-10 4-arg Arpeggio Overload Summary

**4-arg `arpeggio(Chord, NoteValue, direction, pattern)` extends the existing 2-arg signature with rate + direction + pattern, preserving byte-identical determinism via charitable random→up fallback.**

## Performance

- **Duration:** ~5 min (293 s wall clock)
- **Started:** 2026-05-02T18:43:28Z
- **Completed:** 2026-05-02T18:48:21Z
- **Tasks:** 3 (RED + GREEN + verify)
- **Files modified:** 5 (3 created, 2 modified, plus FlowScriptData sentinel)

## Accomplishments

- 4-arg `arpeggio(Chord, NoteValue, String, String)` registered alongside the existing 2-arg signature
- `ApplyDirection` helper handles up / down / updown (apex-skip) / downup (nadir-skip) ordering
- `random` direction falls back to `up` in v1.3 — preserves byte-identical determinism (RESEARCH Pitfall 7)
- `chord-tone` / `scale-tone` pattern strings route to linear ordering (RESEARCH Assumption A8 / REQUIREMENTS line 105)
- Charitable interpretation: unknown direction strings fall through to `up` (no error path)
- All 8 ArpeggioFacts GREEN; `tests/test_dx_arpeggio.flow` exits 0 with sentinel
- ByteIdentical regression gate 6/6 GREEN (Tutorial WAV+MIDI, Showcase WAV+MIDI, Euclidean WAV+MIDI)
- Full test suite 423/423 GREEN — zero regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Wave 0 RED — Failing ArpeggioFacts + DX-10 smoke** — `d583738` (test)
   - 8 xUnit Facts: 7 RED (4-arg overload), 1 GREEN (existing 2-arg regression gate)
   - `tests/test_dx_arpeggio.flow` smoke script with `DX-10 arpeggio: PASSED` sentinel
2. **Task 2: Wave 1 GREEN — Implement DX-10 4-arg overload** — `6500412` (feat)
   - `arpeggioFullSig` registration in HarmonyFunctions.cs
   - `ApplyDirection` private static helper
   - `internal proc arpeggio (Chord: chord, NoteValue: rate, String: direction, String: pattern)` in std.flow
   - All 8 ArpeggioFacts flipped GREEN
3. **Task 3: Wave 1 — Smoke run + byte-identical regression gate** — `5b9f9eb` (chore, verification-only empty commit)
   - `dotnet run --project flow-interpreter tests/test_dx_arpeggio.flow` → exit 0, sentinel printed
   - ByteIdentical 6/6 GREEN; full suite 423/423 GREEN

## Files Created/Modified

- `flow-lang.Tests/Unit/Phase22/ArpeggioFacts.cs` (created) — 8 xUnit Facts pinning DX-10 acceptance behavior; uses `FlowEngineRunner.GetVariable` to inspect `SequenceData.Bars[0].MusicalNotes` directly
- `tests/test_dx_arpeggio.flow` (created) — Smoke script exercising up/down/updown directions over Cmaj7 at QUARTER + EIGHTH rates
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` (modified) — Added `arpeggioFullSig` registration after the existing 2-arg signature; added `ApplyDirection` private static helper
- `flow-lang/std.flow` (modified) — Added `internal proc arpeggio (Chord: chord, NoteValue: rate, String: direction, String: pattern)` declaration alongside the existing 2-arg declaration
- `flow-lang.Tests/FlowScriptData.cs` (modified) — Added `test_dx_arpeggio.flow` sentinel entry to `RequiredSentinels` dictionary

## Decisions Made

- **Sibling-overload registration**: The 4-arg overload is registered immediately after the existing 2-arg signature in `HarmonyFunctions.Register` rather than created in a new file `Arpeggio.cs`. This matches CONTEXT D-08 / Anti-Patterns ("extend in place") and keeps the 2-arg byte-identical for the regression gate.
- **`ToLowerInvariant()` over `ToLower()`**: Chosen for culture-stable string matching in `ApplyDirection` (the existing 2-arg arpeggio uses `ToLower()`, but new code follows the more rigorous convention).
- **Charitable random fallback**: Per RESEARCH Pitfall 7 and project memory, `random` direction maps to `up` in v1.3 instead of throwing or instantiating an RNG. Seeded random arpeggio is deferred to v1.4 — once the design covers seed propagation, byte-identical regression can be re-affirmed.
- **Pattern arg consumed for future expansion**: `pattern` is read from args[3] and explicitly discarded (`_ = pattern`) so the linter / reader sees that the parameter is intentional. RESEARCH Assumption A8 documents the deferred chord-tone / scale-tone routing.

## Deviations from Plan

**Total deviations:** 1 minor (documentation / counting)
**Impact on plan:** None — verification still GREEN; documentation update inline.

### Documentation correction

**1. [Documentation] Plan referenced "ByteIdentical 19/19" but actual count is 6**
- **Found during:** Task 3 (verification gate)
- **Issue:** Plan's `<verification>` and `<acceptance_criteria>` blocks reference `ByteIdenticalTutorialTests + ByteIdenticalShowcaseTests 19/19 GREEN`. The actual byte-identical regression gate consists of 6 tests across 3 classes: `ByteIdenticalTutorialTests` (2: WAV + MIDI), `ByteIdenticalShowcaseTests` (2: WAV + MIDI), `EuclideanByteIdenticalTests` (2: WAV + MIDI).
- **Fix:** Documented actual count (6/6) in Task 3 commit message and this summary. No code change required.
- **Files modified:** none (commit message + this SUMMARY only)
- **Verification:** `dotnet test --filter ByteIdentical --list-tests` enumerates 6 tests; all 6 GREEN.

## Issues Encountered

- **`tests/` directory is gitignored**: First `git add tests/test_dx_arpeggio.flow` failed because `.gitignore` line 7 (`tests/`) blocks the path. Resolved with `git add -f` — matches the convention used by recent phases (test_range.flow, test_enharmonic_edges.flow are tracked but the gitignore rule still applies to new files).

## Next Phase Readiness

- DX-10 sibling-overload pattern established for the rest of Phase 22 (DX-12 delay NoteValue overload, DX-15 loadWav semitones/ratio overloads can follow the same registration shape)
- ApplyDirection helper is reusable for any future direction-keyed sequence transform (lookahead candidate: arpeggio variants emitted from chord-progression DSL in a v1.4+ phase)
- Byte-identical regression gate proven robust under arpeggio extension — confidence boost for downstream Phase 22 plans touching tempo/timing math (DX-12, DX-13)
- 6 plans remain in Phase 22 (22-02 through 22-07); none depend on this plan's outputs (per plan frontmatter `depends_on: []`)

## Self-Check: PASSED

Files verified:
- FOUND: `flow-lang.Tests/Unit/Phase22/ArpeggioFacts.cs`
- FOUND: `tests/test_dx_arpeggio.flow`
- FOUND: `.planning/phases/22-tier-b-c-composer-dx-bundle/22-01-SUMMARY.md`

Commits verified:
- FOUND: `d583738` (Task 1 RED)
- FOUND: `6500412` (Task 2 GREEN)
- FOUND: `5b9f9eb` (Task 3 verification)

---
*Phase: 22-tier-b-c-composer-dx-bundle*
*Completed: 2026-05-02*
