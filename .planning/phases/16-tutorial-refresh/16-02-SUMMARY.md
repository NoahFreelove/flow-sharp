---
phase: 16-tutorial-refresh
plan: 02
subsystem: docs
tags: [tutorial, qol-03, v1.2-features, slice, enharmonic, flat-literals, reverb-time, dynamics, midi-velocity, euclidean, write-midi, dual-export]

requires:
  - phase: 16-tutorial-refresh
    plan: 01
    provides: "examples/tutorial.flow with v1.1 chapters in place + examples/output/.gitignore ready to receive runtime artifacts"
  - phase: 15-composer-dx-part-2
    provides: "v1.2 features shipped (slice Sequence overload, reverbTime, dynamics MIDI velocity, euclidean swing/humanize 6-arg) with byte-identical seed determinism"
  - phase: 14-composer-dx-part-1
    provides: "v1.2 baseline features shipped (slice Array overload, enharmonic + flat literals, dynamics block form, crescendo/decrescendo)"
provides:
  - "examples/tutorial.flow with 5 new v1.2 feature chapters (14-18): Slicing, Enharmonic & Flat Spellings, Reverb Time, MIDI Velocity with Dynamics, Euclidean Rhythms"
  - "Dual writeWav + writeMidi export from the same Song value at the bottom of the graduation chapter (CONTEXT D-04)"
  - "examples/output/flow_tutorial.wav (5 MB) and examples/output/flow_tutorial.mid (770 B) generated on every run"
  - "Congratulations bullet list extended with 6 v1.2 lines (slice, enharmonic+flats, reverbTime, dynamics MIDI, euclidean, dual writeWav/writeMidi)"
affects: [16-03-graduation-piece-refactor, 16-04-showcase-refresh, 16-05-verification-snapshot]

tech-stack:
  added: []
  patterns:
    - "Same Song value feeds both writeWav (after rendering to Buffer) and writeMidi (Song direct) — single-source dual-format export pattern"
    - "v1.2 feature chapters use literal-path strings inline in writeWav/writeMidi calls so the regex grep gates pass on the same line as the call"
    - "Charitable interpretation D-16: enharmonic snippet uses Db4/F#3 spellings that the implementation handles cleanly (DEFER-04 edge cases avoided)"

key-files:
  created:
    - .planning/phases/16-tutorial-refresh/16-02-SUMMARY.md
  modified:
    - examples/tutorial.flow

key-decisions:
  - "Inlined the export paths as literal strings ('examples/output/flow_tutorial.wav' / '.mid') in the writeWav/writeMidi calls rather than using path variables. The plan's <interfaces> EXAMPLE used variables (wavPath/midPath), but the plan's <verify><automated> regex (writeWav.*examples/output/flow_tutorial\\.wav) requires the path on the same line as the call. Inlining satisfies both intents — verifiable AND demonstrative — and removes two unused variable declarations."
  - "Cluster placement chosen for the v1.2 chapters (chapters 14-18 in pedagogical order: Slicing -> Enharmonic -> ReverbTime -> Dynamics-MIDI -> Euclidean) per plan <interfaces> ALTERNATIVE PLACEMENT recommendation. Each chapter builds on prior concepts; by the Euclidean chapter the user has seen sequences, velocity, seeds, and MIDI export."
  - "Graduation piece interior left untouched per plan scope — Plan 16-03 will refactor the sunrise piece to integrate reverbTime + euclidean + per-section gain + tempoRamp audibly. This plan ONLY rewrites the bottom export block."
  - "reverbTime snippet uses 2.0s tail (musically tasteful per CONTEXT D-16); the dry sentinel reverbTime 0 is mentioned in a Note: prose line but never invoked in a runnable snippet."

patterns-established:
  - "Tutorial chapter expansion preserves existing narrative arc (CONTEXT D-01) — new chapters slot between existing 13 and 14, voice synthesis renumbered 14 -> 19 and graduation 15 -> 20 without touching their bodies."
  - "Dual export pattern at the end of a Song graduation example: render Song to Buffer through effects chain, write Buffer via writeWav, write the same Song value directly via writeMidi — both artifacts land in examples/output/ and are .gitignore'd."
  - "Per-feature prose-comment naming: every feature appears by name in at least one Note: comment so ROADMAP criterion #3 grep can verify it (slice, enharmonic, reverbTime, dynamics + crescendo, euclidean + swing + humanize)."

requirements-completed: [QOL-03]

duration: 50m
completed: 2026-04-26
---

# Phase 16 Plan 02: Tutorial v1.2 Features + Dual Export Summary

**Added 5 new v1.2 feature chapters (Slicing, Enharmonic & Flat Spellings, Reverb Time, MIDI Velocity with Dynamics, Euclidean Rhythms) to examples/tutorial.flow and rerouted the graduation piece's WAV-only export to a paired writeWav + writeMidi against examples/output/flow_tutorial.{wav,mid} fed from the same Song value — completing the v1.2 Tier-A coverage required by QOL-03 (CONTEXT D-01, D-04).**

## Performance

- **Duration:** ~50 min wall-clock (2026-04-25T23:32:03Z start -> 2026-04-26T00:21:35Z end). Actual editing/verification work was ~10 min; remainder was waiting on a transient `dotnet test flow-sharp.sln` CLR runner crash that required a per-project test rerun (`flow-lang.Tests` project alone) to collect 284/284 GREEN.
- **Tasks:** 1 (type=auto, autonomous)
- **Files modified:** 1 (examples/tutorial.flow: +159 lines / -10 lines net; 467 -> 616 lines)

## Accomplishments

- **5 new v1.2 feature chapters added between current chapter 13 (Pattern Transforms) and renumbered chapter 19 (Voice Synthesis):**
  - **Chapter 14: Slicing** — `(slice xs 1 4)` Array form with start-inclusive/end-exclusive semantics, silent two-sided clamping demo (`(slice xs 3 100)` -> tail; `(slice xs 3 2)` -> empty), AND Sequence form `(slice threeBars 1 2)` operating on bars.
  - **Chapter 15: Enharmonic & Flat Spellings** — flat literals `| Db4 Eb4 Gb4 Ab4 Bb4 |`, sharp literals `| C#4 F#4 G#5 |`, bare flats defaulting to octave 4 `| Bb Db Eb |`, `enharmonic Db4 -> C#4`, `enharmonic F#3 -> Gb3`, `enharmonic C4 -> C4` (natural pass-through), and the in-key respelling `key Dbmajor { (enharmonic C#4) -> Db4 }`. DEFER-04 edge cases (E↔Fb, B↔Cb, etc.) deliberately avoided per CONTEXT D-16.
  - **Chapter 16: Reverb Time** — `reverbTime 2.0 { section hall { ... } Song hallSong = [hall] Buffer wet = (renderSong hallSong "piano") }`. The dry sentinel `reverbTime 0` is documented in a Note: prose line but not run.
  - **Chapter 17: MIDI Velocity with Dynamics** — `Sequence cresc = baseLine -> crescendo 0.25 0.75` (linear gradient demo) AND the `dynamics ff { ... }` block form. Prose Note: explicitly names `crescendo`, `decrescendo`, `swell`, `dynamics`, MIDI, velocity, and the writeMidi byte-stream integration point.
  - **Chapter 18: Euclidean Rhythms** — both 4-arg `(euclidean 3 8 C4 0.3)` and 6-arg `(euclidean 3 8 C4 0.3 0.1 42)` overloads, plus the negative-swing idiom `(sub 0.0 0.2)` -> `(euclidean 5 16 C4 negSwing)` per CONTEXT D-17. Seed=42 used for byte-identical determinism per Phase 15.
- **Voice Synthesis renumbered 14 -> 19** and **Graduation Piece renumbered 15 -> 20** in both `Note:` headers and `(print "--- N. ...")` mirror lines. Bodies untouched.
- **Graduation piece export block rerouted** from a single `(exportWav finalMix "/tmp/flow_tutorial_output.wav")` call to a paired:
  ```flow
  (writeWav "examples/output/flow_tutorial.wav" finalMix)
  (writeMidi "examples/output/flow_tutorial.mid" sunrise)
  ```
  The same `sunrise` Song value (declared at line 561) feeds both — `writeWav` consumes the rendered+effected Buffer; `writeMidi` consumes the raw Song. CONTEXT D-04 dual-source export satisfied.
- **Closing message updated** to point at the new examples/output/ paths and add a "Open the MIDI in any DAW that imports SMF" nudge.
- **Congratulations bullet list extended with 6 v1.2 lines** (slicing, enharmonic+flats, reverbTime, dynamics MIDI, euclidean, dual writeWav+writeMidi).
- **Tutorial runs end-to-end with exit 0** producing 5,080,364-byte WAV and 770-byte MIDI files at examples/output/. "Congratulations!" prints exactly once. No errors in stdout.
- **flow-lang.Tests 284/284 GREEN** (tutorial.flow is runtime content, not test surface; same baseline as Plan 16-01).

## Task Commits

1. **Task 1: Add v1.2 feature chapters + dual export reroute** — `5bf93c9` (feat)

## Files Created/Modified

- `examples/tutorial.flow` (MODIFIED) — +159 / -10 lines net (467 -> 616). Five new chapters (14-18) inserted; chapters 14-15 renumbered to 19-20; export block rerouted from /tmp/exportWav to examples/output/{writeWav,writeMidi}; Congratulations list extended with 6 v1.2 bullets; closing message updated for dual-format paths.
- `.planning/phases/16-tutorial-refresh/16-02-SUMMARY.md` (CREATED) — this file.

## Decisions Made

- **Path inlining vs variables in export block.** The plan's `<interfaces>` EXAMPLE used `String wavPath = "..."; (writeWav wavPath finalMix)` (variables), but the plan's `<verify><automated>` regex `writeWav.*examples/output/flow_tutorial\\.wav` requires the literal path on the same line as the call. Resolved by inlining the path strings into the calls — both demonstrative AND verifiable, two fewer variable declarations. This is a Rule 3 auto-fix (resolves a verification-blocking pattern mismatch).
- **Pedagogical order within the v1.2 cluster.** Per CONTEXT "Claude's Discretion" bullet, ordered chapters 14-18 by audible/conceptual buildup: Slicing (data manipulation, smallest concept) -> Enharmonic & Flat Spellings (notation polish) -> Reverb Time (audio post-processing) -> Dynamics MIDI (note-level metadata) -> Euclidean Rhythms (rhythm + accent + jitter + seed; combines all prior concepts).
- **Charitable interpretation rules followed throughout** (CONTEXT D-16):
  - Slicing chapter uses indices `(1, 4)` (clean), `(3, 100)` (over-clamp), `(3, 2)` (empty) — no negative-from-end (DEFER-06).
  - Enharmonic chapter uses Db4/Eb4/Gb4/F#3/G#5 spellings; in-key example uses `key Dbmajor` -> `enharmonic C#4 -> Db4`. NO E↔Fb / B↔Cb / C↔B# / F↔E# (DEFER-04 still open per 15-VERIFICATION).
  - reverbTime chapter uses 2.0s tail in the runnable snippet; 0.0 dry sentinel mentioned in prose only.
  - Dynamics chapter uses `crescendo 0.25 0.75` over 5 notes (Phase 14 DX-08 verified gradient).
  - Euclidean chapter uses seed=42 for reproducibility; negative-swing via `(sub 0.0 0.2)` per CONTEXT D-17.
- **S-expression style preserved (CONTEXT D-15).** No infix operators introduced; `(sub 0.0 0.2)` for negative literals, `(slice xs 1 4)` form throughout.

## Deviations from Plan

**1. [Rule 3 - Verification mismatch] Inlined export paths instead of using variables.**
- **Found during:** Initial verification grep after first authoring pass.
- **Issue:** Plan body's `<interfaces>` EXAMPLE used `String wavPath = "..."; (writeWav wavPath finalMix)` form. The plan's `<verify><automated>` block requires the regex `writeWav.*examples/output/flow_tutorial\\.wav` to match on a single line — the variable form fails this gate because the path string is on the declaration line, not the call line.
- **Fix:** Inlined the path strings directly in the writeWav and writeMidi calls. The print messages updated to use literal strings as well (no longer reference the dropped variables). This satisfies both the regex gate and the demonstrative intent.
- **Files modified:** examples/tutorial.flow (lines 573-580).
- **Commit:** 5bf93c9 (only commit for this plan; all changes folded into the single Task 1 commit).

The plan's expected test count (287/287) and the actual baseline (284/284) differ. This is the same planning-time count drift documented in 16-01-SUMMARY (deviation rationale: tutorial.flow is runtime content, not test surface; cannot affect test outcomes). Not a regression — same number as before this plan.

## Issues Encountered

- **`dotnet test flow-sharp.sln --nologo` produced a CLR fatal error** ("Internal CLR error. (0x80131506)") on two consecutive runs. The error occurs at the .NET runtime layer in the solution-level test orchestration, not in our changes (which only touch examples/tutorial.flow runtime content). Resolved by running the test project directly: `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --nologo -v minimal` reports "Passed! - Failed: 0, Passed: 284, Skipped: 0, Total: 284, Duration: 17 s". This is an environment / test-runner concurrency issue unrelated to plan scope; documented here for the verifier.

## Self-Check: PASSED

Verified post-write:

**Files exist:**
- `examples/tutorial.flow` — FOUND (616 lines)
- `examples/output/flow_tutorial.wav` — FOUND (5,080,364 bytes)
- `examples/output/flow_tutorial.mid` — FOUND (770 bytes)
- `.planning/phases/16-tutorial-refresh/16-02-SUMMARY.md` — FOUND (this file)

**Commits exist:**
- `5bf93c9` — FOUND (`feat(16-02): add v1.2 feature chapters and dual WAV+MIDI export to tutorial`)

**Plan verification gate (full automated checklist from plan):**
- exit 0 + "Congratulations" in stdout: PASS
- examples/output/flow_tutorial.wav non-empty: PASS (5 MB)
- examples/output/flow_tutorial.mid non-empty: PASS (770 B)
- `(slice ` call: PASS
- `(enharmonic ` call: PASS
- `Db4|Bb4|Eb4` flat literals: PASS
- `reverbTime` keyword: PASS
- `crescendo` keyword: PASS
- `dynamics` keyword: PASS
- `(euclidean ` call: PASS
- `writeMidi.*examples/output/flow_tutorial\.mid` regex: PASS
- `writeWav.*examples/output/flow_tutorial\.wav` regex: PASS
- No binary artifacts tracked in examples/output/: PASS (only `.gitignore` tracked)
- 284/284 tests GREEN (per-project run): PASS

**Threat surface:** No new attack surface. T-16-03 (filesystem write) and T-16-04 (euclidean steps DoS) both `accept` per plan threat register — euclidean values used (steps=8, 16) are far below the shipped 1024-step guard from Phase 15 Plan 04; writeWav/writeMidi auto-mkdir from Phase 12 FIX-07a handles the examples/output/ directory creation if it didn't already exist.

## Next Phase Readiness

- **Plan 16-03 ready** — graduation piece interior unchanged (sunriseIntro/sunriseVerse/sunriseChorus/sunriseOutro sections at lines 535-559 untouched). Plan 16-03 can refactor those sections to integrate `reverbTime`, `euclidean`, per-section `gain`, and `tempoRamp` audibly while the dual export at the bottom keeps writing to examples/output/.
- **Plan 16-04 ready** — examples/output/ established and writes work; showcase.flow can adopt the same dual-export pattern.
- **Plan 16-05 ready** — every v1.2 Tier-A feature now appears by name in at least one prose comment in tutorial.flow (ROADMAP success criterion #3 satisfied for tutorial scope).
- **No blockers.** All 5 v1.2 features required by QOL-03 are demonstrated; the tutorial is the executable proof that v1.2 features work end-to-end on a stock dev machine.

---
*Phase: 16-tutorial-refresh*
*Completed: 2026-04-26*
