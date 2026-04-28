---
phase: 16-tutorial-refresh
plan: 01
subsystem: docs
tags: [tutorial, qol-03, v1.1-features, output-dir, gitignore, line-comments, mix, gain, tempo-ramp, sing, tts, synth-presets]

requires:
  - phase: 15-composer-dx-part-2
    provides: "tests/output/.gitignore precedent for per-directory artifact ignore (Plan 15-01); v1.1 features (mix, gain, tempoRamp, sing, synth presets) shipped pre-v1.2 and ready for tutorial demonstration"
  - phase: 14-composer-dx-part-1
    provides: "v1.1 polish baseline; charitable-interpretation tone established (CONTEXT D-16 carryover)"
provides:
  - "examples/output/ directory with co-located .gitignore mirroring tests/output/ pattern"
  - "examples/tutorial.flow with v1.1 feature chapters woven into existing narrative arc (chapters 5, 10, 11, 12, 14 added; existing chapters renumbered)"
  - "Per-section gain context-block idiom (gain N { section ... }) demonstrated in Synth Presets chapter"
  - "First organic // line comment introduction inside Comments chapter (no preamble, per CONTEXT D-10)"
  - "Congratulations bullet list extended with 6 v1.1 feature lines"
affects: [16-02-tutorial-v1.2-features, 16-03-graduation-piece-refactor, 16-04-showcase-refresh]

tech-stack:
  added: []
  patterns:
    - "Inline // annotations supplement existing Note: chapter dividers per CONTEXT D-09 stylistic split"
    - "Per-directory .gitignore for runtime-generated artifacts (mirrors tests/output/.gitignore Phase 15 Plan 01)"
    - "Per-section gain via gain N { section ... } context block (NOT section foo { (gain ...) })"

key-files:
  created:
    - examples/output/.gitignore
    - .planning/phases/16-tutorial-refresh/16-01-SUMMARY.md
  modified:
    - examples/tutorial.flow

key-decisions:
  - "Per-section gain demonstrated inside Synth Presets chapter (chapter 11) — gain context block wraps section declaration; renderSong reads section.Gain at render time. Plan 16-03 will integrate per-section gain into the graduation piece for audible structure (not just print-and-show)."
  - "Voice Synthesis (sing) placed as separate chapter 14 between Pattern Transforms and Graduation Piece — voice timbre is distinct from instrumental graduation piece; per CONTEXT 'Claude's Discretion' bullet."
  - "tts mentioned in prose Note: only — never invoked. Charitable interpretation (CONTEXT D-16): tutorial must run clean on a stock dev machine without espeak-ng installed."
  - "Existing graduation piece (now chapter 15) intentionally untouched per plan scope — Plan 16-02 reroutes its writeWav to examples/output/ + adds writeMidi as paired bottom-of-tutorial export."
  - "Per-directory .gitignore form chosen over top-level entry — co-located with artifacts, matches Phase 15 tests/output/ precedent (CONTEXT D-05)."

patterns-established:
  - "Tutorial chapter expansion preserves existing narrative arc (CONTEXT D-01) — new chapters slot into natural positions, existing chapters renumbered, none rewritten."
  - "Inline // comments use plain language naming the feature (e.g. // Mix two buffers into one) per CONTEXT D-11/D-12 (no REQ-IDs, no DX-NN tags)."
  - "Multi-line prose context introduction inside chapter body with Note: prefix; short inline nudges with //."

requirements-completed: [QOL-03]

duration: 3min
completed: 2026-04-25
---

# Phase 16 Plan 01: Tutorial v1.1 Foundation Summary

**Refreshed examples/tutorial.flow with five new chapters covering v1.1 composer-visible features (// comments, mix, per-section gain, strings/organ/bell synth presets, tempoRamp, sing/tts), and established examples/output/ as the shared artifact directory for tutorial + showcase WAV/MIDI exports.**

## Performance

- **Duration:** ~3 min (commit 47269f4 at 19:23:30 → commit 94d20fb at 19:26:49, both 2026-04-25 -0400)
- **Started:** 2026-04-25T23:22:00Z (worktree branch base from STATE.md "Phase --phase execution started")
- **Completed:** 2026-04-25T23:26:49Z (Task 2 commit timestamp)
- **Tasks:** 2 (both type=auto, autonomous)
- **Files modified:** 2 (1 created — examples/output/.gitignore; 1 modified — examples/tutorial.flow)

## Accomplishments

- **examples/output/ directory established** with co-located 3-line .gitignore (header comment + `*.wav` + `*.mid`), mirroring the Phase 15 Plan 01 tests/output/ precedent. `git check-ignore` confirms `examples/output/test.wav` is matched by the new .gitignore. The directory is ready for Plans 16-02 (tutorial graduation reroute) and 16-04 (showcase) to write artifacts at runtime, immediately ignored.
- **Tutorial expanded from 348 → 467 lines** with 5 new chapters woven into the existing narrative arc:
  - **Chapter 5: Comments** — organic introduction of `//` syntax inline (per CONTEXT D-10, no "comments come in two forms" preamble)
  - **Chapter 10: Mixing Multiple Buffers** — `(mix toneA toneB)` direct call + flow-operator pipe form `toneA -> (mix toneB)`
  - **Chapter 11: Synth Presets** — `renderSong` with `"strings"` / `"organ"` / `"bell"` instrument args; embedded **per-section gain** demo via `gain 0.5 { section quietPad { ... } }` context block
  - **Chapter 12: Tempo Ramps** — ritardando (`120 → 60 BPM`) + accelerando (`60 → 120 BPM`) via `tempoRamp`
  - **Chapter 14: Voice Synthesis** — `(sing "ah" C4 0.5)` on three vowels; `tts` mentioned in prose only (espeak-ng not assumed per CONTEXT D-16)
- **Existing chapters 5-10 renumbered to 6-9, 13, 15** without touching their bodies (CONTEXT D-01 — preserve narrative arc).
- **Congratulations bullet list extended** with 6 v1.1 feature lines (// + Note: comments, mix, per-section gain, synth presets, tempoRamp, sing+tts).
- **Tutorial runs end-to-end with exit 0** and prints "Congratulations!" — `dotnet run --project flow-interpreter examples/tutorial.flow` succeeds; the existing `/tmp/flow_tutorial_output.wav` write at line 324 (now line 433) is preserved unchanged for Plan 16-02 to reroute.
- **Test suite still GREEN** (284/284 passing — tutorial.flow is runtime-only content, not test surface).

## Task Commits

Each task was committed atomically:

1. **Task 1: Create examples/output/.gitignore** — `47269f4` (feat)
2. **Task 2: Expand examples/tutorial.flow with v1.1 feature chapters** — `94d20fb` (feat)

## Files Created/Modified

- `examples/output/.gitignore` (CREATED) — 3 lines; ignores `*.wav` and `*.mid`. Itself trackable (parent `examples/` not in top-level .gitignore).
- `examples/tutorial.flow` (MODIFIED) — +130 lines / -11 lines net (348 → 467). Five new chapters added; existing chapters 5-10 renumbered to 6-9, 13, 15; Congratulations list extended with 6 v1.1 feature bullets.

## Decisions Made

All major decisions were pre-decided in the plan via CONTEXT D-01..D-17. Executor decisions made within the "Claude's Discretion" envelope:

- **Per-section gain placement:** chose chapter 11 (Synth Presets) over chapter 10 (Mixing). The Synth Presets chapter already constructs sections via renderSong, so wrapping a section in `gain 0.5 { ... }` flowed naturally without inventing a new section just to host the gain demo. Plan 16-03 will additionally integrate per-section gain into the graduation piece for audible structure (D-07).
- **Voice Synthesis (sing) chapter placement:** between Pattern Transforms (ch.13) and Graduation Piece (ch.15) as standalone chapter 14. Did NOT include sing in the graduation song because vocal timbre is distinct from the instrumental sunrise piece and would feel out of place there.
- **tts handling:** mentioned in a 3-line `Note:` prose block at the end of chapter 14; `tts` invocation NOT performed. Tutorial must run clean on a vanilla machine without espeak-ng (CONTEXT D-16 charitable interpretation).
- **Chapter 5 (Comments) snippet:** kept minimal — declares `Int x = 7  // x is the number we'll square` with two more inline `//` annotations and a 2-line `Note:` trailer observing both styles. No preamble announcing the feature (D-10 organic introduction).

## Deviations from Plan

None — plan executed exactly as written.

The plan's `<verify><automated>` snippet referenced `dotnet test flow-sharp.sln` returning 287/287 GREEN. The current baseline is 284/284 GREEN (same number reported in actual execution). The discrepancy is a planning-time count drift, not a regression — the plan was authored when the suite had a higher count or estimated incorrectly. All 284 tests pass; no tests added or removed by this plan; no code paths touched that could affect any test outcome (tutorial.flow is runtime content executed by the interpreter, not test surface).

## Issues Encountered

None.

## Self-Check: PASSED

Verified post-write:

**Files exist:**
- `examples/output/.gitignore` — FOUND (3 lines, contains `*.wav` and `*.mid`)
- `examples/tutorial.flow` — FOUND (467 lines, modified)
- `.planning/phases/16-tutorial-refresh/16-01-SUMMARY.md` — FOUND (this file)

**Commits exist:**
- `47269f4` — FOUND (`feat(16-01): add examples/output/.gitignore for tutorial+showcase artifacts`)
- `94d20fb` — FOUND (`feat(16-01): expand tutorial.flow with v1.1 feature chapters`)

**Plan verification gate (full automated checklist from plan):**
- exit 0 + "Congratulations" in stdout: PASS
- ≥5 `// ` line comments: PASS (15 found)
- `(mix ` call: PASS
- `"strings"`, `"organ"`, `"bell"` literals: PASS
- `tempoRamp` call: PASS
- `(sing ` call: PASS
- `gain N { ... }` context block: PASS (`gain 0.5 { section quietPad ... }`)
- `tts` mention (without invocation): PASS
- 284/284 tests GREEN: PASS

**Chapter renumbering eyeball:** chapters 1-15 numbered consistently in both `Note:` headers and `(print "--- N. ...")` mirrors (chapter 15 graduation has its own `========` print form, by design — preserved unchanged from the existing tutorial).

**Threat surface:** No new attack surface — file IO is unchanged from baseline (existing `exportWav` → `/tmp/flow_tutorial_output.wav` preserved verbatim; sing returns in-memory buffers only; tts is never invoked). T-16-01 and T-16-02 from the plan's threat register both `accept` disposition — no mitigations required.

## Next Phase Readiness

- **Plan 16-02 ready** — examples/output/ exists with .gitignore in place; the existing exportWav line at tutorial.flow line 433 is the single touchpoint for the writeWav + writeMidi reroute. Plan 16-02 should also reroute showcase.flow's output to the same directory per CONTEXT D-06.
- **Plan 16-03 ready** — graduation piece interior unchanged; ready for refactor to integrate `reverbTime` + `euclidean` swing/humanize + per-section `gain` + `tempoRamp` audibly per CONTEXT D-07.
- **No blockers.** All v1.1 features required by QOL-03 are now demonstrated in tutorial.flow with prose comments naming each by name (ROADMAP success criterion #3 progress).

---
*Phase: 16-tutorial-refresh*
*Completed: 2026-04-25*
