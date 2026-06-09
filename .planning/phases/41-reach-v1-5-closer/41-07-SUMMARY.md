---
phase: 41-reach-v1-5-closer
plan: 07
subsystem: testing
tags: [showcase, edm, determinism, rms-regression, pattern-matching, euclidean, granular, live-coding, midi-out, bookkeeping]

# Dependency graph
requires:
  - phase: 35-language-foundation
    provides: pattern matching `(match ...)` over chord/numeral/index scrutinees
  - phase: 36-sequence-algebra-generative
    provides: seeded `(euclidean ...)` + `@patterns` `fast` combinator + PrngRegistry writeWav-boundary reseed
  - phase: 37-sound-design-sampler-polish
    provides: `(granular ...)` DSP with PrngRegistry-routed jitter
  - phase: 38-live-coding-2.0
    provides: `live <quantize> { }` block (D-v1.5-07 determinism opt-out)
  - phase: 40-studio-sync
    provides: `@midi` + `midiOut(song, port)` real-time MIDI (play-path only)
provides:
  - "examples/edm/pulse.flow — ~60s EDM third-genre showcase exercising all five v1.5 headline primitives"
  - "flow-lang.Tests/baselines/Phase41/showcase.wav — committed RMS regression baseline (byte-stable)"
  - "Phase41ShowcaseRmsTests LIVE + GREEN (SPEC-8 ±0.5 dB / 100 ms vs baseline)"
  - "README.md ## Showcase section (three-genre table + five-primitive checklist)"
  - "D-19 bookkeeping reconciliation: WASM-01/02/03 -> Shipped (carved 47-49); ROADMAP Phase 39 + 48 rows corrected"
affects: [v1.5-milestone-close, github-release-v1.5.0]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Showcase render/demo split (Pitfall 5): pinned seeded offline render (writeWav/writeMidi) + commented live/midiOut demo section so a headless render never hits the nondeterministic surface"
    - "Showcase RMS pin reads back the WAV the script's own writeWav writes (render buffer is context-scoped) — HeldNoteRmsTests read-back precedent, not a global-binding grab"

key-files:
  created:
    - examples/edm/pulse.flow
    - flow-lang.Tests/baselines/Phase41/showcase.wav
  modified:
    - flow-lang.Tests/Integration/Phase41/Phase41ShowcaseRmsTests.cs
    - README.md
    - .gitignore
    - .planning/REQUIREMENTS.md
    - .planning/ROADMAP.md

key-decisions:
  - "Genre = EDM (auto-mode resolved the D-11 decision checkpoint to the recommended default — best fit for the feature checklist, maximal contrast with v1.4's symphony + ragtime)"
  - "Showcase synth = saw (signature EDM sound; wavetable variants are named warm/bright/buzz, not 'wavetable')"
  - "writeWav/writeMidi targets are absolute /tmp/pulse.* (markov_jazz convention) so the determinism harness resolves from any CWD"
  - "RMS test redirects the script's /tmp/pulse.wav literal to a unique temp file per run (hermetic + parallel-safe) and reads it back, rather than grabbing a context-scoped global binding"

patterns-established:
  - "examples/<genre>/ directories get an explicit .gitignore allow-list block + D-502 rendered-audio re-ignore (mirrors examples/{generative,dsp,live}/)"

requirements-completed: [SHOWCASE-01]

# Metrics
duration: 32min
completed: 2026-06-08
---

# Phase 41 Plan 07: v1.5 Third-Genre Showcase (EDM) + Determinism Pin + D-19 Reconciliation Summary

**~60s EDM showcase (`examples/edm/pulse.flow`) exercising all five v1.5 headline primitives with a clean pinned-render / live-demo split, byte-identical two-run cmp-clean on WAV+MIDI, a committed RMS baseline + GREEN regression test, a README `## Showcase` section, and the D-19 WASM/ROADMAP bookkeeping reconciliation.**

## Performance

- **Duration:** ~32 min
- **Started:** 2026-06-08T00:26:00Z (approx)
- **Completed:** 2026-06-08T00:58:09Z
- **Tasks:** 3 (+ pre-authorized genre checkpoint resolved to EDM)
- **Files modified/created:** 7

## Accomplishments

- **`examples/edm/pulse.flow`** — a 61.64s EDM piece (eight four-bar sections @ 128 BPM) that *actually uses* all five SHOWCASE-01 primitives: Phase 35 `(match idx ...)` bassline selection, Phase 36 SEEDED `(euclidean 7 16 ... 1305)` kick + `(euclidean 4 16 ... 808)` clap, Phase 37 `(granular swell 60ms 18Hz 0.4)` riser, Phase 38 `live 1bar { }`, and Phase 40 `midiOut(song, port)`.
- **Clean render/demo split (Pitfall 5, D-v1.5-07)** — the pinned offline render (`writeWav` + `writeMidi`) is fully seeded → two-run cmp-clean holds for **both** WAV (SHA `a2c095c4…`) and MIDI (SHA `1ad0b7f9…`). The `live` block + real-time `midiOut` live in a commented demo section that a headless render never executes.
- **RMS regression pinned** — `flow-lang.Tests/baselines/Phase41/showcase.wav` committed (byte-stable via seeded dither); `Phase41ShowcaseRmsTests` turned from a Skip-stub to a LIVE GREEN test (SPEC-8 ±0.5 dB / 100 ms). Full Phase 41 subset: **26/26 passed, 0 skipped**.
- **README `## Showcase`** — three-genre table (symphony / jazz / EDM), the five-primitive checklist, render/demo + determinism explanation, render commands, and the v1.5.0 Release note (Release stays a human gate).
- **D-19 reconciliation** — REQUIREMENTS.md WASM-01/02/03 traceability flipped `Phase 41 | Pending` → `Phase 47-49 | Shipped (carved 2026-05-25 …)` with checkbox annotations; ROADMAP Phase 39 `0/0 Not started` → `5/5 Complete 2026-05-23` and Phase 48 `5/7 In Progress` → `7/7 Complete 2026-06-05`. Phase 41's own row untouched; no production code changed.

## Task Commits

1. **Task 1: Author the showcase piece (render + demo split)** — `61e18bb` (feat) — includes the `.gitignore` `examples/edm/` allow-list block
2. **Task 2: Pin RMS baseline + LIVE test + README ## Showcase** — `46ed735` (feat)
3. **Task 3: D-19 bookkeeping reconciliation** — `799bf98` (docs)

**Plan metadata:** _this commit_ (docs: complete plan)

## Files Created/Modified

- `examples/edm/pulse.flow` — the EDM showcase (created)
- `flow-lang.Tests/baselines/Phase41/showcase.wav` — RMS regression baseline (created)
- `flow-lang.Tests/Integration/Phase41/Phase41ShowcaseRmsTests.cs` — Skip-stub → LIVE GREEN (modified)
- `README.md` — new `## Showcase` section (modified)
- `.gitignore` — `examples/edm/` allow-list + D-502 audio re-ignore (modified)
- `.planning/REQUIREMENTS.md` — WASM-01/02/03 carve-out reconciliation (modified)
- `.planning/ROADMAP.md` — Phase 39 + 48 progress-row corrections (modified)

## Decisions Made

- **Genre = EDM.** The plan's `checkpoint:decision` was pre-authorized in `--auto` mode to the D-11 recommended default. EDM is the strongest fit for the feature checklist and maximally contrasts v1.4's symphony + ragtime.
- **Synth = `saw`.** `wavetable` is not a valid `renderSong` instrument name (the wavetable variants are `warm`/`bright`/`buzz`); `saw` is the signature EDM lead/bass timbre and synthesis-based (no sample-load dependency).
- **Absolute `/tmp/pulse.*` writeWav/writeMidi targets** so the determinism harness (which runs the script with an arbitrary CWD and prepends the script's directory to relative targets) resolves the output from any directory — the established `examples/generative/markov_jazz.flow` convention.
- **RMS test reads back the written WAV** (redirecting the `/tmp/pulse.wav` literal to a unique per-run temp file) rather than grabbing a global binding — the showcase render buffer is scoped inside `tempo/timesig/key` blocks and is not visible in the global frame.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `wavetable` synth name rejected by `renderSong`**
- **Found during:** Task 1 (first render attempt)
- **Issue:** `(renderSong song "wavetable")` threw `Unknown synthesizer type: wavetable` — the registered names are `sine/saw/square/triangle/piano/brass/sax/flute/strings/organ/bell/drums` plus the custom wavetable variants `warm/bright/buzz`, not the bare `wavetable`.
- **Fix:** Switched to `"saw"` (signature EDM timbre) in both the render path and the commented live-demo block.
- **Files modified:** `examples/edm/pulse.flow`
- **Verification:** `dotnet run -- run examples/edm/pulse.flow` renders cleanly (WAV+MIDI emitted).
- **Committed in:** `61e18bb` (Task 1 commit)

**2. [Rule 3 - Blocking] `examples/edm/pulse.flow` blocked by the global `*.flow` .gitignore**
- **Found during:** Task 1 (first `git add`)
- **Issue:** Line 10 of `.gitignore` globally ignores `*.flow`; every example genre dir needs an explicit allow-list block (the established `examples/{pragmas,scala,generative,sections,dsp,notation,live}/` precedent).
- **Fix:** Added an `examples/edm/` allow-list block + the D-502 defensive re-ignore of rendered `.wav/.mid/.mp3` (writeWav target is `/tmp/`).
- **Files modified:** `.gitignore`
- **Verification:** `git add examples/edm/pulse.flow` stages the file.
- **Committed in:** `61e18bb` (Task 1 commit)

**3. [Rule 3 - Blocking] RMS test `GetVariable("finalMix")` — variable not found**
- **Found during:** Task 2 (first test run)
- **Issue:** The showcase render buffer is declared inside the script's `tempo/timesig/key` context blocks, so it is scoped to that block and not present in the global frame `FlowEngineRunner.GetVariable` reads.
- **Fix:** Rewrote the test to redirect the script's `/tmp/pulse.wav` writeWav literal to a unique per-run temp file and read that WAV back (HeldNoteRmsTests read-back pattern), comparing via the `AssertWavMatchesBaseline` file-path overload (both files already dithered → single-read, no double-dither).
- **Files modified:** `flow-lang.Tests/Integration/Phase41/Phase41ShowcaseRmsTests.cs`
- **Verification:** Test GREEN against the committed baseline; full Phase 41 subset 26/26.
- **Committed in:** `46ed735` (Task 2 commit)

---

**Total deviations:** 3 auto-fixed (all Rule 3 - blocking). **Impact on plan:** all three were mechanical fit-to-codebase corrections (valid synth name, gitignore allow-list, context-scoped-variable read-back). No scope creep; the plan's intent (deterministic seeded render, pinned RMS, README, D-19) is delivered exactly as written.

## Issues Encountered

- The `flow render` verb requires an explicit `-o` (it is not optional as the plan's verify line implied); used `flow run` for the documented render flow and `flow render <SCRIPT> -o <OUT>` (via `--render-cmd`) for the determinism harness.

## Known Stubs

None. The showcase's `live` block + real-time `midiOut` are intentionally **commented out** (not stubbed): they are the demo-section surface that opts out of determinism (D-v1.5-07) and needs `flow watch` / real MIDI hardware — documented inline as such, and not part of the pinned render. This is the sanctioned render/demo split (Pitfall 5), not an unimplemented stub.

## Threat Flags

None. The plan's threat register declared no new runtime surface; the showcase is a `.flow` source artifact + a baseline WAV + tracking-doc edits. The one mitigated threat (T-41-07-DETERM) is satisfied: every stochastic call in the offline render path is explicitly seeded and routed through PrngRegistry, pinned by the two-run harness + `Phase41ShowcaseRmsTests`.

## User Setup Required

None - no external service configuration required.

**Pending human gate (NOT done here, by design):** cutting the **v1.5.0 GitHub Release** (D-04) is a human-pushed outward-facing publish gate — the showcase audio ships *alongside* the cross-platform binaries in that Release. This plan stages the artifact + the README note; it does not cut the Release. Tracked in `41-HUMAN-UAT.md` row 7.

## Next Phase Readiness

- SHOWCASE-01 is complete; the v1.5 genre-agnostic claim is now proven across three contrasting genres (symphony / jazz / EDM).
- Remaining v1.5 milestone-close work is human-gated only: the v1.5.0 GitHub Release cut (D-04), JetBrains Marketplace publish (D-03), and cross-platform/hardware HUMAN-UAT (D-05) + Phase 40 + Phase 49 sign-offs.

## Self-Check: PASSED

- Created files verified on disk: `examples/edm/pulse.flow`, `flow-lang.Tests/baselines/Phase41/showcase.wav`, `flow-lang.Tests/Integration/Phase41/Phase41ShowcaseRmsTests.cs`, `41-07-SUMMARY.md`.
- Task commits verified in git history: `61e18bb`, `46ed735`, `799bf98`.
- Gates: Web build 0 errors; two-run cmp-clean PASS (WAV+MIDI); Phase 41 test subset 26/26 GREEN; README `## Showcase` present; D-19 reconciliation verified.

---
*Phase: 41-reach-v1-5-closer*
*Completed: 2026-06-08*
