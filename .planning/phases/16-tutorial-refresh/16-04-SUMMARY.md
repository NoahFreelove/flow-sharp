---
phase: 16-tutorial-refresh
plan: 04
subsystem: docs
tags: [showcase, qol-03, ambient, reverb-time, euclidean-humanize, dynamics, midi-velocity, wow-factor, v1.2]

# Dependency graph
requires:
  - phase: 16-tutorial-refresh
    provides: examples/output/ directory with .gitignore (16-01 Wave 1)
  - phase: 15-composer-dx-part-2
    provides: reverbTime context (DX-07), euclidean 6-arg humanize+seed (DX-09), byte-identical reproducibility contract
  - phase: 14-composer-dx-part-1
    provides: dynamics inline markings + crescendo curve (DX-08)
provides:
  - Fresh v1.2 ambient mood piece showcase (44 lines, drop-in "wow listen" demo)
  - Anchor demonstration of reverbTime + euclidean humanize + dynamics in a single short composition
  - Byte-identical WAV + MIDI export to examples/output/flow_showcase.{wav,mid}
affects: [phase-16-05 closure, future v1.3 marketing demos, README updates]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Showcase composition pattern — sparse euclidean pulse + crescendo pad + inline-dynamics melody, all wrapped in one reverbTime block"
    - "Inline dynamics marker (`| mp _ _ E5q ... |`) avoids dynamics-block scoping pitfall when Sequence must remain visible at outer scope"

key-files:
  created: []
  modified:
    - examples/showcase.flow

key-decisions:
  - "Aminor at 72 BPM with i / VI / iv / v whole-note pad (A3 → F3 → D3 → E3) for ambient bed — natural-minor cycle sounds settled with default settings"
  - "Inline `mp` velocity marker on the melody bar instead of a wrapping `dynamics mp { ... }` block — keeps melody Sequence visible to the section/reverbTime scope (avoids the dynamics-block scope leak pattern)"
  - "Both crescendo curve (pad: 0.18 → 0.6) AND inline mp dynamics used — exceeds the OR constraint; gives the showcase richer velocity contour"
  - "Euclidean params (5 hits / 16 steps / A2 / swing 0.18 / humanize 0.12 / seed 7) — sparse-but-felt low pulse on the tonic, fixed seed pins byte-identical reproduction"
  - "renderSong with 'strings' instrument per CONTEXT D-03 — sustained timbre lets the reverbTime tail bloom naturally"
  - "Lowpass 2800 Hz + gain -4dB tail-shaping for a soft, warm finish — keeps the piece pleasant under any default playback level"

patterns-established:
  - "Showcase tone: zero `Note:` chapter dividers, three short atmospheric `//` comments only — reads like a piece of code that produces music, not a feature checklist"
  - "Output route: examples/output/flow_showcase.{wav,mid} — co-located with tutorial outputs (CONTEXT D-06)"

requirements-completed: [QOL-03]

# Metrics
duration: 2min
completed: 2026-04-25
---

# Phase 16 Plan 04: Showcase Refresh Summary

**v1.2 ambient mood piece — 44-line showcase anchored on reverbTime 3.2 + euclidean 5/16 humanize (seed 7) + mp dynamics & crescendo curve, rendering through "strings" preset to byte-identical WAV+MIDI**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-04-25T23:32:18Z
- **Completed:** 2026-04-25T23:35:00Z
- **Tasks:** 1
- **Files modified:** 1 (examples/showcase.flow)

## Accomplishments

- Replaced 84-line Phase 1-4 teaching showcase with fresh 44-line v1.2 ambient piece
- All three v1.2 anchor features integrated audibly: `reverbTime 3.2 { ... }` wraps the whole piece; `(euclidean 5 16 A2 0.18 0.12 7)` sparse pulse with humanize and fixed seed; inline `mp` melody dynamics + `crescendo 0.18 0.6` pad curve
- Outputs land at exact paths required by CONTEXT D-06: `examples/output/flow_showcase.wav` (2.35 MB) + `examples/output/flow_showcase.mid` (200 B)
- Tone preserved per CONTEXT §specifics: zero chapter dividers, three short atmospheric `//` comments, banner+exit-print bracket the piece
- Byte-identical reproduction across runs (Phase 15 contract): WAV and MIDI both `cmp`-clean across two consecutive renders

## Task Commits

1. **Task 1: Rewrite examples/showcase.flow as v1.2 ambient mood piece** — `1c3b723` (feat)

## Files Created/Modified

- `examples/showcase.flow` — full rewrite (84 → 44 lines); discards Phase 3 oscillator + Phase 4 progression DSL + vary in favor of v1.2 anchor features (reverbTime + euclidean humanize + dynamics/crescendo)

## Decisions Made

- Authored Aminor 72 BPM piece — A3/F3/D3/E3 natural-minor pad cycle is the safe ambient default per CONTEXT D-16 charitable interpretation; sounds settled with all-defaults playback
- Used inline `mp` marker in the melody bar instead of `dynamics mp { ... }` block. The block-form leaks scope: a Sequence declared inside `dynamics { ... }` is not visible at the enclosing section. Inline markers achieve the same per-note velocity floor without the scoping pitfall
- Combined crescendo + inline mp marker — the plan required dynamics OR crescendo OR decrescendo OR swell, but using both produces a richer, more musical velocity contour. Both still satisfy the single OR clause individually
- Chose "strings" preset over "organ"/"bell" — sustained string timbre lets the reverbTime 3.2 tail bloom most audibly; matches the ambient role described in CONTEXT D-03

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] dynamics block leaked Sequence scope, breaking reference downstream**
- **Found during:** Task 1 (first dotnet run after authoring the skeleton verbatim)
- **Issue:** First run produced `examples/showcase.flow:29:44: error: Variable 'melody' not found` — the `dynamics mp { Sequence melody = | ... | }` block scoped `melody` to the dynamics block; the section block downstream couldn't see it. Same scoping affected pad and pulse if declared inside any inner block
- **Fix:** Replaced `dynamics mp { Sequence melody = | _ _ E5q ... | }` with inline form `Sequence melody = | mp _ _ E5q ... |`. Inline `mp` marker sets per-note velocity floor at the bar level without introducing a scope barrier. Pad and pulse were already declared at outer scope so no further changes needed
- **Files modified:** examples/showcase.flow
- **Verification:** Second run exits 0, both WAV and MIDI produced non-empty
- **Committed in:** 1c3b723 (Task 1 commit, atomic with the fix)

---

**Total deviations:** 1 auto-fixed (1 bug — scope handling in dynamics block)
**Impact on plan:** Plan's `<interfaces>` section warned about dynamics-block scoping but the SUGGESTED SKELETON itself used the block form — fix substituted the inline marker which is functionally equivalent and structurally cleaner. No scope creep. Both required dynamics-related primitives still demonstrated (inline mp marker AND crescendo curve).

## Issues Encountered

- Test suite is 284/284 GREEN in this worktree, not 287/287 as the plan's automated verification expected. The 3-test gap is pre-existing relative to this plan's worktree base (cb796fc, the 16-01 SUMMARY-restore commit) — not a regression caused by Plan 16-04. Showcase is runtime-only, not test surface; no Fact pinned to it.

## Verification Evidence

- `dotnet build flow-sharp.sln` — succeeded (0 errors, 13 warnings, all pre-existing xUnit/VSTHRD analyzer notes)
- `dotnet run --project flow-interpreter examples/showcase.flow` — exits 0, banner + exit lines printed
- `examples/output/flow_showcase.wav` — 2,352,044 bytes
- `examples/output/flow_showcase.mid` — 200 bytes
- `dotnet test flow-sharp.sln --nologo` — 284 passed, 0 failed (worktree baseline maintained)
- Byte-identical reruns: `cmp run1 run2` clean for both WAV and MIDI (Phase 15 ROADMAP criterion #2 holds free)
- Hard-constraint grep gates: reverbTime / (euclidean / dynamics|crescendo / writeWav path / writeMidi path — all present
- Tone gate: zero `Note: -----` chapter dividers; line count 44 (≤110 cap)

## User Setup Required

None - no external service configuration required. The showcase generates self-contained WAV + MIDI; .gitignore from Plan 16-01 already excludes the artifacts.

## Next Phase Readiness

- Phase 16 Plan 05 (closure) can now treat showcase as shipped — audible v1.2 demo file ready alongside refreshed tutorial
- Showcase is suitable as a README demo target (`dotnet run examples/showcase.flow && aplay examples/output/flow_showcase.wav`) — no further refresh needed before v1.2 release tag
- No blockers carried forward

## Self-Check: PASSED

- examples/showcase.flow exists at expected path: FOUND
- Commit 1c3b723 in git log: FOUND
- Output paths examples/output/flow_showcase.{wav,mid} produced after run: FOUND (regenerated; .gitignore-excluded as designed)
- All 3 anchor v1.2 features present in source: FOUND (reverbTime, euclidean, dynamics+crescendo)

---
*Phase: 16-tutorial-refresh*
*Completed: 2026-04-25*
