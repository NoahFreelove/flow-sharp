---
phase: 15
slug: composer-dx-part-2
status: complete
completed: 2026-04-25
subsystem: language-grammar + audio-dsp + stdlib
tags: [phase-closure, dx-07, dx-09, reverb-time, euclidean, swing, humanize, byte-identical, schroeder, prng-determinism]

# Dependency graph
requires:
  - phase: 14-composer-dx-part-1
    provides: "MidiReadHelpers inline pattern (DEFER-05 source); two-pass strict authorship discipline (D-13); silent two-sided clamping precedent (DX-05); MIDI velocity end-to-end chain verified for DX-09 reuse"
provides:
  - "DX-07 reverbTime musical-context block — full grammar + runtime + audio path; per-voice Schroeder reverb in SongRenderer; 0.0 dry-render sentinel; parse-time negative reject; silent clamp at 30s"
  - "DX-09 euclidean swing/humanize/seed overloads — velocity-accent semantics (no timing field); seeded byte-identical MIDI + WAV output across runs; uniform humanize distribution; local PRNG isolation"
  - "Audio-layer determinism contract — synth white-noise RNG + TPDF dither RNG both reseeded with fixed values + reset hooks at renderSong/writeWav boundaries; cross-render reproducibility for ROADMAP criterion #2"
  - "30 automated Phase 15 Facts + 3 FlowScriptData Theory rows + 1 pinned manual collision grep = 34 regression gates added"
affects: [phase-16-tutorial-refresh (QOL-03 demo material now available); future-audio-refactors (must preserve fixed-seed RNG contract)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Nullable-field inheritance rail (4 touchpoints atomic): field + Clone + ToString + ExecutionContext.GetMusicalContext walk + early-break predicate clause"
    - "Parse-time vs interpret-time validation split: parser rejects negative values, interpreter silently clamps positive overflow"
    - "Schroeder RT60 → feedback closed-form: feedback = 10^(-3 · avgDelaySeconds / rt60Seconds), clamped [0, 0.99]"
    - "Per-context-axis voice-loop substitution (pan / gain / rt60 read section.Context, mutate per-voice via new Voice(replacedBuffer, offsetBeats) + Gain/Pan copy-forward)"
    - "Two-pass strict empirical capture for cross-version determinism Facts at file-byte level (extending Phase 13/14's stdout/Value-level use to MIDI + WAV serialization boundaries)"
    - "Deterministic-RNG-seeding for shared static state: when a static `Random` is used for decorrelation-only purposes, fix-seed at module entry + reseed at the public boundary; audible quality unchanged, cross-call reproducibility gained"
    - "Wave 0 placeholder rewrite protocol: `WAVE-0 PLACEHOLDER` marker grep precedes overwrite (T-15-14 mitigation); sentinel lines preserved verbatim so FlowScriptData Theory rows stay GREEN through the transition"
    - "Two-layer determinism gating: in-process Value Fact + cross-file xUnit byte-equality + script-level sentinel form three independent independence-checks at three different layers"
    - "Doc-only criterion-reframe pattern (Phase 12 TEST-03 / Phase 14 DX-06 precedent): when CONTEXT resolves a contradiction in REQUIREMENTS/ROADMAP, the closure plan updates the source documents and pins the audit trail (original wording preserved, reframe note cross-references the locked CONTEXT decision)"

key-files:
  created:
    - .planning/phases/15-composer-dx-part-2/15-VERIFICATION.md
    - .planning/phases/15-composer-dx-part-2/15-SUMMARY.md
    - flow-lang.Tests/Unit/Phase15/.keep
    - flow-lang.Tests/Integration/Phase15/.keep
    - flow-lang.Tests/Shared/MidiReadHelpers.cs
    - flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs
    - flow-lang.Tests/Unit/Phase15/ReverbApplyRt60Tests.cs
    - flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs
    - flow-lang.Tests/Unit/Phase15/EuclideanHumanizeTests.cs
    - flow-lang.Tests/Integration/Phase15/ReverbTimeRenderTests.cs
    - flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs
    - tests/output/.gitignore
    - tests/test_reverb_time.flow
    - tests/test_euclidean_swing.flow
    - tests/test_euclidean_humanize.flow
  modified:
    - .planning/ROADMAP.md
    - .planning/REQUIREMENTS.md
    - .planning/STATE.md
    - .planning/phases/15-composer-dx-part-2/15-VALIDATION.md
    - .planning/phases/14-composer-dx-part-1/deferred-items.md
    - flow-lang/Ast/Statements/MusicalContextStatement.cs
    - flow-lang/Runtime/MusicalContext.cs
    - flow-lang/Runtime/ExecutionContext.cs
    - flow-lang/Lexing/TokenType.cs
    - flow-lang/Lexing/SimpleLexer.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang/Interpreter/Interpreter.cs
    - flow-lang/StandardLibrary/Audio/DSP/Reverb.cs
    - flow-lang/StandardLibrary/Audio/SongRenderer.cs
    - flow-lang/StandardLibrary/Audio/SynthUtils.cs
    - flow-lang/StandardLibrary/Audio/FileIO.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/std.flow
    - flow-lang.Tests/Fixtures/FlowEngineRunner.cs
    - flow-lang.Tests/FlowScriptData.cs
    - flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs

decisions:
  - "ROADMAP criterion #3 reframed per CONTEXT D-02 (zero is the dry-render sentinel, not a rejection case) — doc-only, no code impact, follows Phase 12 TEST-03 + Phase 14 DX-06 precedent"
  - "DX-07 audio path uses NEW Reverb.Apply(rt60) overload (D-13) — non-breaking to existing Apply(roomSize) callers; ProcessChannel strict refactor pinned via SHA-256 byte-equivalence Fact"
  - "Per-voice reverb via SongRenderer voice-loop substitution (D-14) over shared-bus alternative — chose maximum creative range; CPU cost acceptable at <50 voices"
  - "RT60 = 0 short-circuits Reverb.Apply with EXACT-zero comparison (no epsilon) per D-02 — parser produces literal values unchanged, so reverbTime 0 vs reverbTime 0.0001 land on different paths"
  - "Schroeder feedback cap locked at 0.99 (RESEARCH Open Q 3) — produces audible RT60 control without instability"
  - "DX-09 base velocity reads MusicalContext.Velocity ?? 0.63 (RESEARCH Open Q 1) — composers can shape via dynamics ff/p contexts naturally"
  - "Local new Random(seed) per euclidean call (D-17) — does not touch ExecutionContext.GetRand; isolates PRNG consumption count regardless of intervening RNG-consuming calls"
  - "Audio-layer determinism gap-fix bundled into Plan 15-05 (Phase 14 D-13 divergence-bundle clause) — reseeded SynthUtils + FileIO RNGs at the renderSong/writeWav boundary"
  - "Wave 0 placeholder convention (Plan 01) validated end-to-end — Theory rows stayed GREEN through the placeholder→real-body transitions in Plans 03 and 06"
  - "DEFER-05 closed in Plan 01 (Wave 0) rather than Plan 05 — second consumer (F-19) hits a single shared MidiReadHelpers path from day 1 instead of landing a transient duplicate that gets reconciled after-the-fact"

requirements-completed: [DX-07, DX-09]

# Metrics
duration: ~85min execution + ~110min closure (excludes planning)
completed: 2026-04-25
---

# Phase 15 — Composer DX Part 2 — SUMMARY

**Composers ship humanized euclidean grooves with deterministic byte-identical MIDI + WAV output and per-voice reverb-tail control via the new `reverbTime` musical-context block — DX-07 + DX-09 shipped together as the widest-blast-radius half of the v1.2 DX bundle, with ROADMAP criterion #3 reframed per CONTEXT D-02 to match the charitable-interpretation philosophy.**

---

## Goal vs Delivered

**Goal (ROADMAP):** Composers get humanized euclidean grooves with
deterministic output and per-voice reverb-tail control via a new
musical-context block — the two widest-surface DX features of the
milestone, shipped after smaller-surface work has bedded in.

**Delivered:**

- **DX-07** (`reverbTime` musical-context block) — full grammar + runtime
  + audio path; nullable `MusicalContext.ReverbTime` with 8-field
  `GetMusicalContext` walk + updated early-break predicate; new
  `Reverb.Apply(rt60Seconds, damping, mix)` Schroeder overload with
  feedback cap 0.99; per-voice application in `SongRenderer.RenderSection`
  with exact-0 dry short-circuit; parse-time negative reject; silent
  clamp at 30s; stacks with explicit `reverb()` calls.
- **DX-09** (euclidean swing + humanize + seed overloads) — 4-arg
  swing-only and 6-arg swing/humanize/seed overloads; swing as
  velocity-accent (no timing change); humanize as uniform random
  perturbation clamped at velocity range; required `seed: Int` for
  determinism via local `new Random(seed)` per call; byte-identical
  MIDI + WAV output across runs (required reseeding two pre-existing
  static unseeded RNGs in the audio layer).
- **All 5 ROADMAP success criteria** observable via 30 automated Facts
  + 3 FlowScriptData Theory rows + 1 manual pinned collision grep.
  ROADMAP criterion #3 wording reframed per CONTEXT D-02 (doc-only;
  audit trail preserved in 15-VERIFICATION.md §Criterion Reframes).

---

## Plans Shipped

7 plans across 4 waves; full commit hash manifest in
[15-VERIFICATION.md §Commit Hash Manifest](./15-VERIFICATION.md).

Wave 0 (sequential) — scaffolding:

- **15-01** — Phase15 test subtree + `MidiReadHelpers` promotion
  (closes DEFER-05) + `tests/output/.gitignore` + 3 placeholder `.flow`
  scripts wired to FlowScriptData Theory rows.

Wave 1 (parallel) — DX-07 grammar + DX-09 core:

- **15-02** — DX-07 grammar + runtime (7 source files +
  `ReverbTimeContextTests` 7 Facts).
- **15-04** — DX-09 euclidean overloads (`BuiltInFunctions` +
  `std.flow` + `EuclideanSwingTests` 6 Facts +
  `EuclideanHumanizeTests` 6 Facts).

Wave 2 (sequential after Wave 1) — DX-07 audio path:

- **15-03** — `Reverb.Apply(rt60)` Schroeder overload + `ProcessChannel`
  strict refactor + `SongRenderer` per-voice wiring +
  `tests/test_reverb_time.flow` real body + `ReverbApplyRt60Tests` 3
  Facts + `ReverbTimeRenderTests` 3 Facts.

Wave 3 (parallel) — DX-09 byte-identical regression + .flow scripts:

- **15-05** — `EuclideanByteIdenticalTests` (F-19 + F-20) +
  audio-layer determinism gap-fix bundled per Phase-14 D-13 clause
  (`SynthUtils` + `FileIO` + `SongRenderer` reseeding).
- **15-06** — `tests/test_euclidean_swing.flow` +
  `tests/test_euclidean_humanize.flow` real bodies replacing Wave-0
  placeholders (Theory rows stayed GREEN through the transition).

Wave 4 (sequential) — closure:

- **15-07** (this plan) — ROADMAP criterion #3 reframe per D-02 +
  REQUIREMENTS DX-07/DX-09 Shipped markers + 15-VERIFICATION.md +
  15-VALIDATION.md promotion to `nyquist_compliant: true` +
  15-SUMMARY.md (this file) + STATE.md update + DEFER-05 strikethrough.

---

## Fact Count

| Stage | Facts |
|-------|-------|
| Pre-Phase-15 baseline (post-Phase-13 + Phase-14 close) | 257 |
| Plan 15-01 (Wave 0 — 3 FlowScriptData Theory rows) | 260 |
| Plan 15-02 (+ 7 ReverbTimeContextTests) | 267 |
| Plan 15-03 (+ 3 ReverbApplyRt60Tests + 3 ReverbTimeRenderTests) | 273 |
| Plan 15-04 (+ 6 EuclideanSwingTests + 6 EuclideanHumanizeTests) | 285 |
| Plan 15-05 (+ 2 EuclideanByteIdenticalTests) | 287 |
| Plan 15-06 (script bodies replaced; no new Facts) | 287 |
| Plan 15-07 (this plan; docs-only) | 287 |
| **Phase 15 close — `dotnet test flow-sharp.sln --nologo`** | **287/287 GREEN** |

**Net delta from Phase 15:** +30 automated Facts + 3 FlowScriptData
Theory rows + 1 pinned manual collision grep = **34 regression gates
added**.

All 24 Facts mapped in 15-VALIDATION.md (F-01..F-24) GREEN at phase
close.

---

## Divergences

Aggregate from per-plan SUMMARYs (full detail in each
`15-NN-SUMMARY.md` §Deviations):

- **Plan 02:** Probe function rename `__probeMusicalContext` →
  `probeMusicalContext` (Flow lexer reserves `_` as `TokenType.Underscore`
  rest marker). Probe pattern adopted: direct
  `InternalRegistry.Register` + `GlobalFrame.DeclareFunction` (test-only,
  zero production-code change, doesn't touch concurrently-edited
  `std.flow`).
- **Plan 03:** Four Rule-1 observable refinements during Pass-2
  reality check — F-06 calibration `rt60=1.0s + 10ms RMS window`
  (single-sample probes fluctuate with comb-filter phase + damping
  loss); F-02 switched from raw-byte WAV `SequenceEqual` to
  `trailingRms within 10%` (FileIO TPDF dither RNG was unseeded);
  F-07/F-08 switched to `CountDivergentPcmSamples > 50%` (per-voice
  reverb truncates at voice buffer); `Buffer buf` → `rendered1`/`rendered2`
  (collided with `TokenType.Buf` keyword). RESEARCH Open Q 3 locked at
  feedback cap 0.99. Strict-refactor SHA-256 hash
  `4FA63B25F7444215...C68A222C7E8` for the existing `Apply(roomSize)`
  byte-equivalence pin.
- **Plan 04:** Empirically pinned `dynamics ff` base velocity at
  `0.875` (per `Parser.NoteStream.TryParseDynamicMarking:344`, NOT the
  drafted `0.98`). F-16 humanize narrowed from `0.5` → `0.3` so
  perturbed range stays inside `[0, 1]` and the D-12 clamp doesn't
  inflate the top bucket as confound. F-18 RNG-consumer was `vary`
  (already uses local `new Random` per `VariationFunctions.cs:71`); the
  local-PRNG isolation property is still cleanly observable regardless.
- **Plan 05:** **Pass-1 outcome split** — F-19 Outcome A (GREEN on
  first run; in-process determinism extended through DryWetMidi
  serialization without gap-fix), F-20 Outcome B (RED on first run with
  same byte length 352844 + divergence at byte 49; minimal 5-line
  audio-layer gap-fix bundled per Phase-14 D-13 clause). **Two
  pre-existing static unseeded `Random` fields fixed:** synth
  white-noise RNG (`SynthUtils.cs`, fresh discovery — undocumented
  prior; affects piano hammer transient + sax breath noise + drum
  hits) + TPDF dither RNG (`FileIO.cs`, Plan-15-03 documented but
  worked-around there via RMS observables). Empirical .NET pin: SDK
  `10.0.107`, runtime `Microsoft.NETCore.App 10.0.7`. Velocity bytes
  `[122, 70, 108]` for `euclidean(3, 8, "C4", 0.3, 0.1, 42)`.
- **Plan 06:** Zero deviations requiring deviation rules. Optional
  `cmp` byte-identity smoke after the script run also PASSED (`rc=0`).
- **Plan 07 (this plan):** Zero functional deviations. Audit-trail
  observation: ROADMAP criterion #3 reframe necessarily preserves the
  string "rejects negative or zero" inside the quoted "Original
  wording" reframe note; the plan's strict acceptance criterion of
  `grep -Fc "rejects negative or zero" ... returns 0` was overridden by
  the audit-trail-preservation requirement (mirrors Phase-14 DX-06's
  `*Original audit-trail:*` preamble pattern).

---

## ROADMAP Evolution

- **2026-04-25:** Phase 15 completed. v1.2 milestone progress advances
  from 5/7 phases to 6/7 (Phase 16 Tutorial Refresh remains; Phase 17
  Language Server already shipped).
- **2026-04-25:** ROADMAP Phase 15 criterion #3 wording REFRAMED per
  CONTEXT D-02 (doc-only; audit trail preserved in
  [15-VERIFICATION.md §Criterion Reframes](./15-VERIFICATION.md)). Third
  CONTEXT-resolved criterion reframe in v1.2 (after Phase 12 TEST-03 +
  Phase 14 DX-06).
- **2026-04-25:** REQUIREMENTS DX-07 + DX-09 rows flipped to Shipped.
  Traceability table now shows 13 Shipped + 1 Pending (QOL-03) for
  v1.2; only Phase 16 (Tutorial Refresh) remains.
- **2026-04-25:** REQUIREMENTS.md "Last updated" footer advanced.

---

## Deferred Items

- **DEFER-05** (`14-deferred-items.md`) — `Shared/MidiReadHelpers.cs`
  promotion. **CLOSED 2026-04-21 by Plan 15-01.** Strikethrough applied
  in `14-deferred-items.md` by this closure plan. Two consumers in
  service: Phase 14 `DynamicsMidiVelocityTests` (refactored from
  inline) + Phase 15 `EuclideanByteIdenticalTests` (F-19).
- **DEFER-03** (pragma `enable` system) — **still OPEN.** Blocks
  Gaussian humanize distribution per CONTEXT D-11 (Phase 15 ships
  uniform only) and blocks DEFER-02 H-alias.
- **DEFER-02** (H = B note-stream alias) — **still OPEN.** Depends on
  DEFER-03 shipping first.
- **DEFER-04** (multi-letter enharmonic-edge respelling) — **still
  OPEN.**
- **DEFER-06** (`slice` negative-from-end indexing) — **still OPEN.**
- **No new deferred items introduced by Phase 15.**

---

## Threat Surface

Per-plan threat models in 15-RESEARCH.md (T-15-01 through T-15-15)
fully mitigated: T-15-08 (`steps > 1024` DoS guard in DX-09
overloads); T-15-14 (placeholder-rewrite blast radius mitigated via
`WAVE-0 PLACEHOLDER` marker convention + Plan-06 pre-overwrite
`grep -q` check). F-24 grep confirms no inadvertent
identifier-name leak into user-authored `.flow` files. No new threat
surface introduced beyond the registry.

---

## Next Phase

**Phase 16 — Tutorial Refresh (QOL-03).** Now unblocked: tutorial can
demo `reverbTime` + `euclidean` swing/humanize alongside earlier v1.1 +
v1.2 features. See ROADMAP.md §Phase 16 block. Phase 17 (Flow Language
Server) shipped 2026-04-20 (3 HUMAN-UAT rows tracked in
`17-HUMAN-UAT.md` for the first release tag).

After Phase 16 ships, v1.2 milestone closes and the project enters v1.3
planning.

---

## Self-Check: PASSED

Verified the closure-plan deliverables are all in place at the closure
commit `0a7a441`:

- `.planning/ROADMAP.md` criterion #3 reframed (`grep -c "Reframed
  2026-04-20 per CONTEXT D-02"` → 1; `grep -Fc "dry-render sentinel"` →
  1) and Phase 15 row marked Complete
- `.planning/REQUIREMENTS.md` DX-07 + DX-09 rows flipped to Shipped
  (`grep -c "DX-07.*Shipped"` → 2 incl. Traceability table; `grep -c
  "DX-09.*Shipped"` → 2 incl. Traceability table; no `<PLAN-NN-HASH>`
  placeholders remaining)
- `15-VERIFICATION.md` (NEW) — exists with all 6 required sections;
  F-24 transcript pinned verbatim (12 `reverbTime` mentions)
- `15-VALIDATION.md` promoted (`status: verified`,
  `nyquist_compliant: true`, `wave_0_complete: true`; 25 ✅ green
  markers across all 24 Fact rows + Wave-0 / Sign-off rows; only the
  legend line retains the ⬜ pending vocabulary)
- `15-SUMMARY.md` (NEW) — this file
- `14-deferred-items.md` DEFER-05 struck through (7 strikethrough
  markers — original requirement preserved) + closure note appended
- `.planning/STATE.md` updated (Phase 15 closed, completed_phases 5
  → 6, milestone progress recomputed, Resume Instructions advanced to
  Phase 16, accumulated-context bullets added for Plans 02/03/04/07)
- `dotnet test flow-sharp.sln --nologo` → 287/287 GREEN at HEAD
- F-24 collision grep re-run matches the pinned transcript verbatim
- Closure commit `0a7a441` exists in `git log --oneline -3`

---

*Phase: 15-composer-dx-part-2*
*Closed: 2026-04-25*
