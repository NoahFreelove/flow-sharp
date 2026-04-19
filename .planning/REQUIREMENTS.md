# Flow Language — v1.2 Requirements

**Milestone:** v1.2 Stability & Composer DX
**Started:** 2026-04-18
**Source audit:** `.planning/CODEBASE-AUDIT-2026-04-18.md`
**Research:** `.planning/research/SUMMARY.md`

**Goal:** Verify then fix the critical bugs surfaced by the 2026-04-18 codebase audit, unblock the failing test suite, ship the Tier A composer DX bundle, and refresh the tutorial so v1.1 + v1.2 capabilities are discoverable.

REQ-ID numbering continues from v1.1 (last used: FIX-04, DX-04, AUDIO-08, QOL-02). New categories `SPIKE-*` and `TEST-*` introduced this milestone.

---

## Active Requirements

### Audit Spike (must complete before stability work)

Two research agents disagreed on whether audit claims C1–C5 describe real bugs. These requirements produce either (a) a failing test that reproduces the bug, or (b) a documented dismissal. Requirements FIX-05 through FIX-09 below are conditional on the spike outcome.

- [ ] **SPIKE-01**: Reproduce or close C1 — musical-context frame leak / early-return body skip. Produce either a failing `.flow` test demonstrating unbalanced context stack (or skipped block body after validation error) OR a written dismissal citing the try/finally at `Interpreter.cs:133,286`.
- [ ] **SPIKE-02**: Reproduce or close C2 — `_returnValue` short-circuits error-path statements (`Interpreter.cs:73-74`). Produce either a failing test OR dismissal noting `_returnValue` is only set by `ReturnStatement`.
- [ ] **SPIKE-03**: Reproduce or close C3 — envelope div-by-zero (`EnvelopeProcessor.cs:108,120,150,156,169`). Produce a `.flow` script with sub-frame attacks/releases that crashes OR dismissal proving the `for (int i=0; i<Nframes; i++)` guard.
- [ ] **SPIKE-04**: Reproduce or close C4 — fade div-by-zero (`BufferHelpers.cs:130,159`). Same format as SPIKE-03.
- [ ] **SPIKE-05**: Reproduce or close C5 — `augment`/`diminish` semantic swap (`TransformFunctions.cs:247,268`). Produce a regression test showing `augment(quarter)` either yields half (correct) or eighth (swapped), and document which direction the code currently goes.

### Stability — Confirmed Bugs

- [ ] **FIX-05**: `init([])` throws `InvalidOperationException("Cannot get init of empty array")` instead of silently returning `[]` — matches `head`/`last` semantics (C6 from audit, `Collections.cs:84-92`).
- [ ] **FIX-06**: `Thunk.Force()` caches any exception thrown by the evaluator and re-raises it on subsequent `Force()` calls, so failed thunks do not silently return null or retry with null evaluator (C7 from audit, `Thunk.cs:27-46`).

### Stability — Contingent on Spike Outcome

**Spike outcome:** see `.planning/phases/11-audit-spike/11-VERIFICATION.md`. FIX-07 split below per D-04; Dismissed claims closed without sub-requirement (marker-only closure).

- [ ] **FIX-07a** (closes SPIKE-01, Confirmed): fix `ExecuteMusicalContext` body-skip — seven early `return;` statements inside the `try` block (Interpreter.cs lines 151, 164, 178, 224, 240, 255, 263) exit before the body loop at `Interpreter.cs:270-284` runs, so any validation error silently drops the block body. The `try/finally` frame balance at `Interpreter.cs:286-289` is correct and must NOT be altered. Proposed fix: replace each `return;` with `break;` so the body loop executes under partial/default musical context. Regression test: `tests/spike/c1-musical-context-body.flow` (committed RED in Phase 11 per D-08). Source: `flow-lang/Interpreter/Interpreter.cs:292` (AUDIT-VERIFIED marker). Ships in Phase 12 with a behavior-preserving fix that turns the RED test green.

Dismissed claims (closed by inline AUDIT-VERIFIED markers; no Phase 12 action required): C2, C3, C4, C5.

### Test Unblocking

- [ ] **TEST-01**: `range(Int, Int) → Array[Int]` built-in implemented (exclusive upper bound, matching Python/Rust convention); `tests/test_custom_oscillator.flow` passes without modification.
- [ ] **TEST-02**: `break` and `continue` statements are executed by the interpreter in `for`/`while` loops (parsed already, not interpreted); `tests/test_while_loop.flow:37-54` passes without modification.
- [ ] **TEST-03**: `bpm()`, `createStereoTrack`, and `renderBars` either implemented as real wrappers OR removed from `tests/test_full_song.flow` with the test rewritten to exercise the intended song-rendering path using shipped built-ins.
- [ ] **TEST-04**: Retroactive Nyquist validation — `.planning/phases/06-diagnostics-bug-fixes/`, `07-developer-experience/`, `08-audio-production/`, `09-advanced-features/` each gain a `VALIDATION.md` satisfying the Nyquist checklist; phase 10 draft updated to `nyquist_compliant: true` or explicit waiver.

### Composer DX — Tier A

- [ ] **DX-05**: `slice(Sequence, Int, Int) → Sequence` and `slice(Array[T], Int, Int) → Array[T]`. Start inclusive, end exclusive. Bar-level for Sequence; out-of-range clamps (not throws) to match `take`/`drop`. No new files required.
- [ ] **DX-06**: Enharmonic helpers — `Db`, `Eb`, `Gb`, `Ab`, `Bb`, `Cb`, `Fb` flat literals accepted by `NoteType.Parse` and normalized to existing `(letter, octave, alteration)` triples; `H` accepted as `B` alias **only within note-stream context** (`| … |`); `enharmonic(Note) → Note` built-in returns pitch-equivalent spelling. Must NOT break existing identifier `H` as a variable name in ordinary code.
- [ ] **DX-07**: `reverbTime <seconds> { … }` musical context block — sets per-voice reverb RT60; applied during voice rendering via `Reverb.Apply` with RT60→feedback mapping. Mirrors existing `gain`/`pan` context pattern. Pre-landing identifier audit: no collision with existing `reverbTime` usage in `examples/`, `tests/`, stdlib `.flow` files.
- [ ] **DX-08**: MIDI velocity from dynamic transforms preserved end-to-end — `dynamics { }` musical context propagates to `MusicalNoteData.Velocity` at compile time in `NoteStreamCompiler`; `crescendo`/`decrescendo`/`swell` envelope values (already written to `Velocity`) reach `MidiExport.cs:192` and map to MIDI velocity 1–127 without loss. Regression test: write a `.flow` script with `dynamics`, export MIDI, assert velocity bytes.
- [ ] **DX-09**: `euclidean(hits, steps, note, swing)` and `euclidean(hits, steps, note, swing, humanize, seed)` overloads — swing applied as velocity accent on on-beats; humanize perturbs velocity within `±humanize`; required `seed: Int` parameter for deterministic output; no new `MusicalNoteData` timing field (micro-timing deferred to v1.3).

### Quality of Life

- [ ] **QOL-03**: `examples/tutorial.flow` refreshed to demonstrate v1.1 + v1.2 features end-to-end: `//` line comments, `writeWav`, `mix`, per-section `gain`, `strings`/`organ`/`bell` synth presets, `tempoRamp`, `sing`/`tts`, plus new v1.2: `slice`, enharmonic helpers, `reverbTime`, MIDI velocity export, `euclidean` swing/humanize. Tutorial runs to completion without errors and produces audible WAV + MIDI output.

---

## Future Requirements (deferred)

- **Micro-timing humanize** — new `MusicalNoteData.TimingOffset` field; deferred to v1.3 so data model change lands cleanly
- **MidiExport velocity rest-threshold** (audit §3 minor issue) — deferred; can ship standalone
- **Audit §2 major bugs** — overload ambiguity, bandpass Q unbounded, stereo voices played as mono, ChordParser sharp formatting, scale database brittleness, trill/tremolo duration math, OverloadResolver top-2 tie check — candidates for v1.3 or a dedicated hardening milestone
- **Tier B/C DX features** — arpeggio parameters, chord inversions/voicings, delay sync to note values, scale linting, legato/portamento, snap-to-grid, microtonal ratios, WAV pitch-shift on load
- **Pidgin dependency removal** — referenced but unused in csproj

## Out of Scope (for v1.2)

- NAudio/CSCore/NWaves integration — minimal-dependencies philosophy stands; hand-rolled DSP is the canonical path
- DryWetMidi 9.0.0-prerelease upgrade — stability milestone, no pre-release deps
- New NuGet packages of any kind — confirmed unnecessary by STACK.md research
- GUI/DAW interface, VST/AU hosting, multi-user collaboration, cloud deploy — project-level out-of-scope, unchanged from v1.1
- Real-time MIDI controller input — live-performance category, v2+

---

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| SPIKE-01 | Phase 11 | Complete |
| SPIKE-02 | Phase 11 | Complete |
| SPIKE-03 | Phase 11 | Complete |
| SPIKE-04 | Phase 11 | Complete |
| SPIKE-05 | Phase 11 | Complete |
| FIX-05 | Phase 12 | Pending |
| FIX-06 | Phase 12 | Pending |
| FIX-07a | Phase 12 | Pending |
| TEST-01 | Phase 12 | Pending |
| TEST-02 | Phase 12 | Pending |
| TEST-03 | Phase 12 | Pending |
| TEST-04 | Phase 13 | Pending |
| DX-05 | Phase 14 | Pending |
| DX-06 | Phase 14 | Pending |
| DX-08 | Phase 14 | Pending |
| DX-07 | Phase 15 | Pending |
| DX-09 | Phase 15 | Pending |
| QOL-03 | Phase 16 | Pending |

---

*Last updated: 2026-04-19 — Phase 11 Audit Spike closed; FIX-07 split per D-04*
