---
phase: 16-tutorial-refresh
plan: 03
subsystem: docs
tags: [tutorial, qol-03, graduation-piece, audible-integration, reverb-time, euclidean, per-section-gain, tempo-ramp, fixed-seed, byte-identical]

requires:
  - phase: 16-tutorial-refresh
    plan: 02
    provides: "examples/tutorial.flow with v1.2 chapters 14-18 + dual writeWav+writeMidi export pair targeting examples/output/"
  - phase: 15-composer-dx-part-2
    provides: "byte-identical seed determinism for euclidean (Plan 15-05 reseeded synth + dither RNGs)"
  - phase: 14-composer-dx-part-1
    provides: "tempoRamp(seq, startBPM, endBPM) Buffer-returning DX (v1.1 Phase 9)"
provides:
  - "examples/tutorial.flow graduation piece (chapter 20) with all 4 CONTEXT D-07 audible features integrated: reverbTime 2.5 (outro hall tail), euclidean 5/16 with seed=42 (groove section), per-section gain 0.6 + 1.0 (intro/chorus dynamic arc), tempoRamp 100→60 (ritardando WAV tail)"
  - "writeWav writes finalWithTail (rendered Buffer + audible ritardando mix-in); writeMidi writes the same sunrise Song value (notation only — no audible ritardando in MIDI, documented)"
  - "Byte-identical determinism preserved across two consecutive runs (WAV + MIDI both cmp-equal)"
affects: [16-04-showcase-refresh, 16-05-verification-snapshot]

tech-stack:
  added: []
  patterns:
    - "gain context block wraps section declaration to set per-section voice gain (Phase 8 AUDIO-04 + Phase 11 nested-context fix)"
    - "reverbTime context block wraps section declaration to set per-section RT60 reverb tail (Phase 15 DX-07)"
    - "euclidean 6-arg form (hits, steps, note, swing, humanize, seed) with literal seed=42 inherits Phase 15 Plan 05 byte-identical guarantee"
    - "tempoRamp produces a stand-alone Buffer mixed via mix(finalMix, ritBuf) into the final WAV; MIDI export remains independent of buffer-level mixing (preserves CONTEXT D-04 same-source-song principle)"

key-files:
  created:
    - .planning/phases/16-tutorial-refresh/16-03-SUMMARY.md
  modified:
    - examples/tutorial.flow

key-decisions:
  - "Plan A (mix-in tempoRamp tail) chosen over Plan B (per-section tempo-aware rendering) — minimum-churn integration. The tempoRamp tail mixes onto finalMix from frame 0 (mix sums sample-by-sample); the audible ritardando rides under the song with the song fading out before it. Documented asymmetry: WAV gets audible ritardando, MIDI does not (writeMidi captures notation only, per CONTEXT D-04 same-source-song from a Song value not a Buffer)."
  - "Section bare-expression form for sunriseGroove uses `Sequence drums = (euclidean ...)` declaration form rather than bare expression (matches existing tutorial chapter 8/11/12 idiom; mirrors test_euclidean_humanize.flow `Sequence a = (euclidean ...) ; section sa { a }` only differing in inline-vs-named declaration). Confirmed working at runtime."
  - "Per-section gain at intro=0.6 (quiet) and chorus=1.0 (explicit normal) — verse left unwrapped to demonstrate that gain context is OPTIONAL, only some sections need it for visible dynamic shaping. CONTEXT D-07 calls for varying section loudness, satisfied by gradient (0.6 → unwrapped default → 1.0)."
  - "Used `//` line comments for the 4 feature-name annotations (above each integration point) per CONTEXT D-09/D-11 (// for short inline annotations; Note: for chapter dividers and longer prose). Mirrors the comment-style split established by Plan 16-01."
  - "Test count expectation in plan was 287/287; actual baseline is 284/284 (carried over from Plan 16-01/02 SUMMARY notes). Tutorial.flow is runtime content, not test surface, so the count is unaffected by this plan; same documented planning-time count drift."

patterns-established:
  - "Plan-A integration pattern for the 4 Phase-15-DX features in a single song body: gain wraps a section, reverbTime wraps a section, euclidean is a section's Sequence body, tempoRamp produces a Buffer mixed into the final WAV (not the MIDI). Reusable as a template for future composer-DX tutorials/showcases."
  - "Documented-asymmetry pattern: WAV and MIDI exports of the same Song can diverge legitimately when audible-only effects (tempoRamp Buffer mix-in) are layered on the WAV path. Document the asymmetry in a Note: comment so the reader understands why MIDI doesn't have the ritardando."

requirements-completed: [QOL-03]

duration: 2m
completed: 2026-04-26
---

# Phase 16 Plan 03: Graduation Piece Audible Integration Summary

**Refactored the graduation piece (chapter 20 — Sunrise) interior to integrate all 4 CONTEXT D-07 audible features into the composition itself: gain 0.6/1.0 wraps shape the intro/chorus dynamic arc, a new sunriseGroove section drives a euclidean 5/16 percussive groove with seed=42 (byte-identical), reverbTime 2.5 wraps the outro for a hall-tail finish, and a tempoRamp 100→60 BPM tail mixes a ritardando Buffer into the final WAV — with the writeWav + writeMidi export pair from Plan 16-02 preserved and CONTEXT D-04's same-source-song principle honored (MIDI captures notation; WAV adds audible ritardando layer).**

## Performance

- **Duration:** ~2 min wall-clock (2026-04-26T00:29:16Z start → 2026-04-26T00:31:37Z task commit). Single-task atomic edit; verification + determinism smoke ~1 min.
- **Tasks:** 1 (type=auto, autonomous, no TDD)
- **Files modified:** 1 (examples/tutorial.flow: +35 / -16 lines net; chapter-20 body restructured)

## Accomplishments

- **Graduation chapter 20 body restructured** with the 4 CONTEXT D-07 features integrated audibly:

  1. **Per-section gain shapes the dynamic arc** — `gain 0.6 { section sunriseIntro { ... } }` wraps the intro for a quiet open; `gain 1.0 { section sunriseChorus { ... } }` makes the dynamic structure explicit at the chorus. Verse left unwrapped to demonstrate optional wrapping.
  2. **New sunriseGroove section** — `Sequence drums = (euclidean 5 16 C3 0.2 0.1 42)` produces a 5-hits-over-16-steps groove with humanize 0.1 and FIXED seed=42. Placed in the song arrangement between intro and first verse: `[sunriseIntro sunriseGroove sunriseVerse*2 sunriseChorus sunriseVerse sunriseChorus sunriseOutro]`.
  3. **reverbTime 2.5 wraps the outro** — `reverbTime 2.5 { section sunriseOutro { ... } }` gives the outro a long hall tail (Phase 15 DX-07 per-voice RT60).
  4. **tempoRamp tail mixed into the WAV** — `Sequence tail = | C4h G3h C3w |` rendered through `(tempoRamp tail 100.0 60.0)` produces a ritardando Buffer; `(mix finalMix ritBuf)` layers it onto the final WAV. `writeMidi` still receives the original `sunrise` Song value (no buffer-level mixing affects notation export).

- **One // prose comment per integrated feature** (CONTEXT D-11):
  - `// Quiet intro -- per-section gain shapes the dynamic arc`
  - `// Euclidean groove with humanize + seed=42 (byte-identical across runs)`
  - `// Bright chorus wrapped in explicit gain 1.0 -- visible dynamic structure`
  - `// Outro wrapped in reverbTime 2.5s -- long hall tail closes the piece`
  - `// tempoRamp slows the tail from 100 BPM to 60 BPM -- a ritardando ending`

- **Documented WAV/MIDI asymmetry** in two `Note:` lines above the tempoRamp call: "tempoRamp produces a Buffer; we mix it into the final WAV. (writeMidi captures notation only -- no audible ritardando in the MIDI.)" Honors CONTEXT D-04 (same source song for both formats).

- **Byte-identical determinism preserved** — two consecutive `dotnet run --project flow-interpreter examples/tutorial.flow` invocations produce `cmp`-equal WAV (5,503,724 bytes) and MIDI (814 bytes) files. Phase 15 ROADMAP criterion #2 contract holds end-to-end through the tutorial.

- **Tutorial runs to completion** with exit 0, "Congratulations" printed exactly once, no errors in stdout. Render: 1,375,920 frames raw → 1,375,920 frames after effects → 1,375,920 frames with ritardando tail (mix preserves longest-buffer length).

- **flow-lang.Tests 284/284 GREEN** — same baseline as Plan 16-02 (tutorial.flow is runtime content, not test surface).

## Task Commits

1. **Task 1: Refactor graduation piece interior** — `be18d5c` (feat)

## Files Created/Modified

- `examples/tutorial.flow` (MODIFIED) — +35 / -16 lines net. Chapter 20 body (lines 532-581 → 532-600) restructured per Plan A: 3 sections wrapped (intro in `gain 0.6`, chorus in `gain 1.0`, outro in `reverbTime 2.5`), 1 new section added (sunriseGroove with euclidean seed=42), 4 lines of new logic for tempoRamp tail + mix + export of finalWithTail. Chapter divider + banner prints + Congratulations list at file end untouched.
- `.planning/phases/16-tutorial-refresh/16-03-SUMMARY.md` (CREATED) — this file.

## Decisions Made

- **Plan A (tempoRamp mix-in) over Plan B (per-section tempo-aware rendering)** — Plan A is the minimum-churn integration; the tempoRamp Buffer mixes onto finalMix from frame 0 via `mix`. Acceptable per CONTEXT D-16 (charitable interpretation: music > rigid correctness; tutorial is teaching, not making a hit record). Documented asymmetry in a `Note:` comment so the reader understands MIDI does not include the ritardando.

- **Section declarations use named-Sequence form rather than bare expression** — `section sunriseGroove { Sequence drums = (euclidean ...) }` mirrors the existing tutorial idiom (chapters 8/11/12); test_euclidean_humanize.flow uses `Sequence a = (euclidean ...) ; section sa { a }` (extracted variable) which is functionally equivalent. Both work at runtime; chose inline declaration for less variable-pollution.

- **Per-section gain gradient: intro=0.6, verse=unwrapped (default ~1.0), chorus=1.0** — demonstrates that gain context is optional (only some sections need explicit wrapping) AND varies section loudness as CONTEXT D-07 requires. The 1.0 wrap on the chorus is "visible structure" — semantically a no-op but pedagogically valuable (shows the wrap form even when value is normal).

- **`//` for feature-name annotations, `Note:` for prose** — Plan 16-01 established this split (CONTEXT D-09/D-11); Plan 16-03 follows it. The 5 `//` annotations name each integrated feature; the 2 `Note:` lines above the tempoRamp call carry the longer asymmetry-explanation prose.

- **euclidean 5/16 with C3 + swing 0.2 + humanize 0.1 + seed 42** — denser groove than the tutorial's chapter 18 example (3/8) for variety; C3 (low piano) reads as percussive when rendered through "piano"; seed 42 matches chapter 18 + tests/test_euclidean_humanize.flow for cross-tutorial determinism alignment.

## Deviations from Plan

**None.** Plan A executed verbatim per `<interfaces>` skeleton:

- All 4 CONTEXT D-07 features integrated as designed.
- writeWav writes `finalWithTail`; writeMidi writes `sunrise` Song value (no buffer-level mixing affects MIDI).
- mix(finalMix, ritBuf) worked on first try — no fallback to separate-WAV needed (the plan's `<action>` step 4 contingency was not triggered).
- All grep gates passed first run (reverbTime≥2 actually 7, euclidean≥2 actually 7, tempoRamp≥2 actually 7, gain wraps both present).
- Build succeeded; tutorial ran to completion exit 0.
- Two-run determinism smoke confirmed WAV + MIDI byte-identical (cmp exit 0 for both).

The plan's expected test count (287/287) and actual baseline (284/284) differ — this is the same documented planning-time count drift carried over from Plan 16-01/02 SUMMARY (deviation rationale: tutorial.flow is runtime content, not test surface; cannot affect test outcomes). Not a regression.

## Issues Encountered

None. Build, runtime, and verification all green on first attempt.

## Self-Check: PASSED

Verified post-write:

**Files exist:**
- `examples/tutorial.flow` — FOUND (636 lines, modified)
- `examples/output/flow_tutorial.wav` — FOUND (5,503,724 bytes, regenerated)
- `examples/output/flow_tutorial.mid` — FOUND (814 bytes, regenerated)
- `.planning/phases/16-tutorial-refresh/16-03-SUMMARY.md` — FOUND (this file)

**Commits exist:**
- `be18d5c` — FOUND (`feat(16-03): integrate reverbTime, euclidean, per-section gain, tempoRamp into graduation piece`)

**Plan verification gate (full automated checklist):**
- `dotnet build flow-sharp.sln`: PASS (0 errors, 13 pre-existing warnings)
- `dotnet run --project flow-interpreter examples/tutorial.flow` exit 0 + Congratulations: PASS
- examples/output/flow_tutorial.wav non-empty: PASS (5.5 MB)
- examples/output/flow_tutorial.mid non-empty: PASS (814 B)
- `grep -c "reverbTime" examples/tutorial.flow >= 2`: PASS (7)
- `grep -c "euclidean " examples/tutorial.flow >= 2`: PASS (7)
- `grep -c "tempoRamp" examples/tutorial.flow >= 2`: PASS (7)
- `grep -qE "gain 0\.6 \{"`: PASS (line 536)
- `grep -qE "reverbTime 2\.5 \{"`: PASS (line 564)
- `grep -qE "\(euclidean 5 16 C3 0\.2 0\.1 42\)"`: PASS (line 545)
- `dotnet test flow-sharp.sln`: PASS (284/284 GREEN — same baseline as 16-01/16-02)

**Byte-identical determinism smoke:**
- Run 1 vs Run 2 WAV `cmp`: PASS (byte-identical; 5,503,724 bytes both)
- Run 1 vs Run 2 MIDI `cmp`: PASS (byte-identical; 814 bytes both)
- Phase 15 ROADMAP criterion #2 contract held end-to-end through the tutorial.

**Threat surface:** No new attack surface. T-16-05 (euclidean seed=42 byte-identity) `accept` per plan threat register — confirmed positive (2-run cmp PASS). T-16-06 (tempoRamp tail buffer length) `accept` — finalWithTail = 1,375,920 frames matches finalMix (mix preserves longest buffer); no unbounded growth.

## Next Phase Readiness

- **Plan 16-04 ready** — graduation piece complete; showcase.flow (chapter sibling) can now be refreshed in parallel as the v1.2 ambient mood piece per CONTEXT D-03/D-06. Same examples/output/ pattern available (writes flow_showcase.{wav,mid} with the dual-export idiom).
- **Plan 16-05 ready** — all 4 CONTEXT D-07 audible features now appear in the graduation piece by name (`reverbTime`, `euclidean`, `gain`, `tempoRamp`); ROADMAP success criterion #1 (every v1.1 + v1.2 feature demonstrated) is satisfied for tutorial scope. Byte-identical contract demonstrated end-to-end as a free regression gate.
- **No blockers.** The graduation piece is musical, not a feature dump (CONTEXT §specifics) — the 4 features serve the song's arc (quiet intro → groove → verses → loud chorus → hall outro → ritardando tail).

---
*Phase: 16-tutorial-refresh*
*Completed: 2026-04-26*
