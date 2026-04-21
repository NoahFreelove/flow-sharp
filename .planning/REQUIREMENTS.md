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

- [x] **FIX-05**: `init([])` throws `InvalidOperationException("Cannot get init of empty array")` instead of silently returning `[]` — matches `head`/`last` semantics (C6 from audit, `Collections.cs:84-92`). *(Completed 2026-04-19 in Phase 12 Plan 02, commit 6e5a960; regression coverage in `flow-lang.Tests/Unit/CollectionsTests.cs`.)* **Shipped 6e5a960.**
- [x] **FIX-06**: `Thunk.Force()` caches any exception thrown by the evaluator and re-raises it on subsequent `Force()` calls, so failed thunks do not silently return null or retry with null evaluator (C7 from audit, `Thunk.cs:27-46`). *(Completed 2026-04-19 in Phase 12 Plan 03, commit 557923a; `Thunk` refactored to `Lazy<Value>` with `LazyThreadSafetyMode.ExecutionAndPublication`; regression coverage in `flow-lang.Tests/Unit/ThunkTests.cs`.)* **Shipped 557923a.**

### Stability — Contingent on Spike Outcome

**Spike outcome:** see `.planning/phases/11-audit-spike/11-VERIFICATION.md`. FIX-07 split below per D-04; Dismissed claims closed without sub-requirement (marker-only closure).

- [x] **FIX-07a** (closes SPIKE-01, Confirmed): fix `ExecuteMusicalContext` body-skip — seven early `return;` statements inside the `try` block (Interpreter.cs lines 152, 165, 179, 225, 241, 256, 264) exit before the body loop at `Interpreter.cs:271-285` runs, so any validation error silently drops the block body. The `try/finally` frame balance at `Interpreter.cs:287-290` is correct and must NOT be altered. Fix shipped in Phase 12 Plan 04: replaced each `return;` with `break;` so the body loop executes under partial/default musical context. Regression test: `tests/spike/c1-musical-context-body.flow` (RED in Phase 11 per D-08, flipped GREEN 2026-04-19). Source: `flow-lang/Interpreter/Interpreter.cs:292` AUDIT-VERIFIED marker now reads `2026-04-19: C1 — Fixed (returns→breaks); body now runs under partial/default context`. Commits: 327aa3c (fix + sentinel) + fd9d801 (unit tests).

Dismissed claims (closed by inline AUDIT-VERIFIED markers; no Phase 12 action required): C2, C3, C4, C5.

### Test Unblocking

- [x] **TEST-01** (CLOSED as audit false positive, 2026-04-19): The audit claimed `range(Int, Int) → Array[Int]` was missing, but empirical verification during Phase 12 Plan 05 discovered `range` is NOT registered in the stdlib either (confirmed at `.planning/phases/12-stability/deferred-items.md` DEFER-01). However, the audit's underlying claim that this blocked `tests/test_custom_oscillator.flow` was wrong — `test_custom_oscillator.flow` Tests 1/2/3 are blocked by the `if`-overload (covered under TEST-03 reframing), and Test 4's `(range 0 sz)` call is a separate, pre-existing stdlib gap orthogonal to the Phase 12 scope. DEFER-01 forwards `range` implementation to a future phase. Retained as an audit-trail entry — the codebase audit conflated two distinct problems (if-overload blocking Tests 1-3 vs range-missing blocking Test 4) under a single requirement, and the "blocks test_custom_oscillator" framing was empirically over-broad.
- [x] **TEST-02** (CLOSED as audit false positive, 2026-04-19): `break`/`continue` are ALREADY interpreted at `Interpreter.cs:120-124`, `321-322`, `354-355` via `BreakSignal`/`ContinueSignal`. Empirically verified via `tests/test_while_loop.flow` producing the expected `5,3,0,0,1,0,3` output (Theory row GREEN in plan 12-01's wrap-as-Theory harness). No code change required. Retained as an audit-trail entry — the codebase audit mis-read the existing implementation.
- [x] **TEST-03** (REFRAMED 2026-04-19): The audit's claim that `bpm()`/`createStereoTrack`/`renderBars` are missing is empirically wrong — all three are implemented. The ACTUAL failures in `tests/test_full_song.flow` and `tests/test_custom_oscillator.flow` are (a) missing `if(Bool, Void, Void)` wildcard overload in the stdlib, blocking strict-arg `if` call sites at `test_custom_oscillator.flow:42` (String branches) and `:57` (Double branches); and (b) `exportWav` (and, by extension, `writeWav` — both route through `ExportWavInternal`) not auto-creating parent directories, blocking `test_full_song.flow:158-159`'s `"tests/output/test_full_song.wav"` write. Both fixed in Plan 12-05: **Shipped 9afbe7a** (if-overload) and **Shipped c09cd82** (ExportWavInternal auto-mkdir). test_full_song.flow runs to completion, writing 352,844-byte WAV; test_custom_oscillator.flow Tests 1/2/3 pass.
- [x] **TEST-04** (Shipped 21e773d, 2026-04-19): Retroactive Nyquist validation — `.planning/phases/06-diagnostics-bug-fixes/`, `07-developer-experience/`, `08-audio-production/`, `09-advanced-features/` each gained a `VALIDATION.md` at `nyquist_compliant: true`; `10-vocalization/10-VALIDATION.md` promoted from draft to `nyquist_compliant: true` (commit 21e773d). 16 observable-value pins across 5 plans verified; `dotnet test flow-sharp.sln` green at 81/81 (baseline 68 pre-Phase-13 + 13 new Facts across 13-01..13-05). Per-plan commits: 13-01 ff901fa+4cf0ccd+39d53f3, 13-02 fb1a1ae+ed64dec+9d7575f, 13-03 ea1d95a+511085f+b077491, 13-04 ade6fbd+1a41ada+1cb508d, 13-05 331d059+81f348c+21e773d.

### Composer DX — Tier A

- [x] **DX-05** (Shipped 4528407, 2026-04-20): `slice(Sequence, Int, Int) → Sequence` and `slice(Array[T], Int, Int) → Array[T]`. Start inclusive, end exclusive. Bar-level for Sequence. Silent two-sided clamping matching `take`/`drop` (CONTEXT D-01 — both overloads atomic per D-02). Regression: `flow-lang.Tests/Unit/Phase14/SliceTests.cs` (Array + Sequence Facts) + `tests/test_slice.flow` Theory row. Phase 14 Plan 01.
- [x] **DX-06** (REFRAMED 2026-04-20): Original audit-trail wording preserved below; shipped scope covers **extended flat-literal surface** (arbitrary `b`/`#`/`+`/`-` composition with any int net alteration, on either side of octave digits — CONTEXT D-07/D-08/D-09) + **`enharmonic(Note) → Note`** key-context-aware respelling (CONTEXT D-03/D-04/D-05/D-06). The `H` alias clause is **deferred to a future pragma phase** (CONTEXT D-10/D-11/D-12); see `.planning/phases/14-composer-dx-part-1/deferred-items.md` for the H-alias requirement, pragma system design sketch, and candidate `enable` keyword. Chord-vs-note lexer dispatch reordered to keep `Bb7` tokenizing as ChordLiteral under the extended Parse surface (regression gate `LexerTests.Bb7_NewBehavior_IsNote` pins the new Bb7-as-Note behavior; existing chord literals `Dm`/`Cmaj7`/`Am7`/`Bdim`/`Csmaj`/`Bfm` remain ChordLiterals, regression gated by `LexerTests.*_IsChord` Facts). Pre-landing collision grep empty across `*.flow` files (ROADMAP criterion 5 — transcript in 14-VERIFICATION.md). **Shipped d2edc90** (flat literals + lexer reorder) + **Shipped 2490c9c** (enharmonic) — Phase 14 Plan 02.

  *Original audit-trail:* Enharmonic helpers — `Db`, `Eb`, `Gb`, `Ab`, `Bb`, `Cb`, `Fb` flat literals accepted by `NoteType.Parse` and normalized to existing `(letter, octave, alteration)` triples; `H` accepted as `B` alias **only within note-stream context** (`| … |`); `enharmonic(Note) → Note` built-in returns pitch-equivalent spelling. Must NOT break existing identifier `H` as a variable name in ordinary code.
- [ ] **DX-07**: `reverbTime <seconds> { … }` musical context block — sets per-voice reverb RT60; applied during voice rendering via `Reverb.Apply` with RT60→feedback mapping. Mirrors existing `gain`/`pan` context pattern. Pre-landing identifier audit: no collision with existing `reverbTime` usage in `examples/`, `tests/`, stdlib `.flow` files.
- [x] **DX-08** (Shipped 152e593, 2026-04-20): MIDI velocity from dynamic transforms preserved end-to-end. Regression Fact `DynamicsMidiVelocityTests.Crescendo_EmitsExpectedVelocityGradient` reads MIDI via DryWetMidi 8.0.3 `MidiFile.Read` + `NotesManagingUtilities.GetNotes` and asserts the velocity byte array `[31, 47, 63, 79, 95]` for `crescendo(0.25, 0.75)` over 5 notes (observable-value pin per Phase 13 D-11). Chain verified via two-pass strict authorship (CONTEXT D-13); Pass 2 outcome recorded in 14-03-SUMMARY.md (Outcome A — GREEN on first run, zero-divergence). Phase 14 Plan 03.
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
| FIX-05 | Phase 12 | Shipped 6e5a960 |
| FIX-06 | Phase 12 | Shipped 557923a |
| FIX-07a | Phase 12 | Shipped 327aa3c (fix) + fd9d801 (tests) |
| TEST-01 | Phase 12 | Closed (audit false positive — `range` implementation deferred to DEFER-01) |
| TEST-02 | Phase 12 | Closed (audit false positive — already implemented at Interpreter.cs:120-124) |
| TEST-03 | Phase 12 | Shipped 9afbe7a + c09cd82 (reframed per CONTEXT D-01) |
| TEST-04 | Phase 13 | Shipped 21e773d (closed 2026-04-19) |
| DX-05 | Phase 14 | Shipped 4528407 |
| DX-06 | Phase 14 | Shipped d2edc90 + 2490c9c (reframed per CONTEXT D-19; H-alias deferred — see deferred-items.md DEFER-02/03) |
| DX-08 | Phase 14 | Shipped 152e593 (two-pass strict per CONTEXT D-13, Outcome A — GREEN on first run) |
| DX-07 | Phase 15 | Pending |
| DX-09 | Phase 15 | Pending |
| QOL-03 | Phase 16 | Pending |

---

*Last updated: 2026-04-20 — Phase 14 Composer DX Part 1 closed; DX-05 / DX-06 / DX-08 shipped. DX-06 H-alias clause deferred to future pragma phase (see `.planning/phases/14-composer-dx-part-1/deferred-items.md` + 14-04-PLAN.md).*
