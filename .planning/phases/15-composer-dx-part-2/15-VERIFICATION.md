---
status: passed
phase: 15
phase_name: composer-dx-part-2
closed: 2026-04-25
verification_source: plan-15-07-closure
must_haves_verified: 5
must_haves_total: 5
deferred: []
---

# Phase 15: Composer DX Part 2 — Verification

**Phase:** 15
**Status:** Complete
**Closed:** 2026-04-25 (plan 15-07 closure commit)

Authoritative record that ROADMAP Phase 15 success criteria #1-#5 are
observable via committed Facts + `.flow` Theory rows + one pinned manual
collision grep.

---

## Criteria → Artifact Map

| ROADMAP # | What Must Be True | Observable Via | Commit |
|-----------|-------------------|----------------|--------|
| #1 | euclidean swing is velocity-accent (not timing) | Fact `EuclideanSwingTests.Swing_ChangesVelocity_NotTiming` (F-21) | `302f950` (Plan 04 squashed) |
| #1 | euclidean swing accents on-beats (positive) / off-beats (negative) | Facts F-09, F-10, F-11, F-12, F-13 | `302f950` (Plan 04 squashed) |
| #1 | euclidean humanize perturbs velocity within ±humanize | Facts F-14, F-15, F-16, F-17, F-18 | `302f950` (Plan 04 squashed) |
| #2 | Byte-identical MIDI across runs with identical seed | Fact `EuclideanByteIdenticalTests.SameSeed_ByteIdenticalMidi` (F-19) — empirical velocity bytes `[122, 70, 108]` on net10.0.107 | `10c9557` |
| #2 | Byte-identical WAV across runs with identical seed | Fact `EuclideanByteIdenticalTests.SameSeed_ByteIdenticalWav` (F-20) — 352844 bytes both runs; required reseeding two pre-existing static unseeded RNGs (synth white-noise + TPDF dither) | `af09ce5` |
| #3 | `reverbTime` block sets per-voice RT60 via `Reverb.Apply` (Schroeder RT60→feedback, cap 0.99) | Facts F-06, F-07, F-08 | `302f950` (Plans 02-03 squashed) |
| #3 | rejects negative; treats 0 as dry (REFRAMED per D-02) | Fact F-03 (`Parse_Negative_ParseError`) + F-02 (`Zero_ShortCircuitsReverb`) + supporting `Parse_Zero_ProducesDry` | `302f950` (Plans 02-03 squashed) |
| #4 | Nested `reverbTime` resolves through `GetMusicalContext` 8-field walk + early-break | Facts F-05 (`Nested_WithGain_Independent`), F-22 (`GetMusicalContext_AllFieldsResolvedSearchesReverbTime`), F-23 (`Nested_InsideTempoAndKey_Resolves`) | `302f950` (Plan 02 squashed) |
| #5 | Pre-landing collision grep — zero stdlib/`.flow`/`examples` collisions | F-24 manual transcript below | (this closure plan commit) |

---

## Criterion Reframes

**Criterion #3** — wording updated 2026-04-20 per CONTEXT D-02:

- **Original:** "rejects negative or zero values with a clear error"
- **Reframed:** "rejects negative values with a parse-time error; treats `reverbTime 0` as the dry-render sentinel (voice renderer short-circuits)"
- **Rationale:** CONTEXT D-02 is the locked user decision; the original
  ROADMAP wording contradicted the charitable-interpretation philosophy
  (see user memory `feedback_charitable_interpretation.md` —
  "Prefer silent-and-documented assumptions over errors; music > rigid
  correctness"). This reframe follows the Phase 12 TEST-03 precedent and
  Phase 14 DX-06 precedent (audit-vs-reality reconciliation via CONTEXT
  authority).
- **Audit-trail preservation:** the original "rejects negative or zero"
  wording remains visible inside the reframe note in ROADMAP.md line 181
  so the historical contradiction stays auditable rather than being
  silently overwritten. Same pattern as Phase 14's `*Original
  audit-trail:*` preamble for DX-06.

---

## F-24 Pre-Landing Collision Grep — Transcript

Recipe (matches 15-VALIDATION.md F-24 row + 15-CONTEXT.md scope):

```bash
grep -rn "reverbTime" examples/ tests/ flow-lang/*.flow
```

Executed verbatim 2026-04-25 at phase closure:

```
$ grep -rn "reverbTime" examples/ tests/ flow-lang/*.flow
tests/test_reverb_time.flow:4:// Phase 15 DX-07 — reverbTime end-to-end render sanity.
tests/test_reverb_time.flow:8:        reverbTime 2.5 {
tests/test_reverb_time.flow:14:            (print "reverbTime 2.5: PASSED")
tests/test_reverb_time.flow:17:        // D-02: reverbTime 0 is the dry sentinel — output should be byte-identical
tests/test_reverb_time.flow:18:        // to the same render without a reverbTime wrapper.
tests/test_reverb_time.flow:19:        reverbTime 0 {
tests/test_reverb_time.flow:25:            (print "reverbTime 0 dry: PASSED")
```

**Result:** 7 hits — **all inside the single file `tests/test_reverb_time.flow`**
(the DX-07 Plan-03 end-to-end script itself). Zero hits in
`flow-lang/*.flow` stdlib modules; zero hits in `examples/`. **No
identifier collisions.**

Implementation references in `flow-lang/**/*.cs` (Parser.cs,
Interpreter.cs, MusicalContext.cs, SimpleLexer.cs, TokenType.cs,
ExecutionContext.cs, MusicalContextStatement.cs) are **outside the grep
scope** — that is the DX-07 implementation surface, not a user-visible
identifier clash. The scope is specifically `flow-lang/*.flow`
(user-visible stdlib Flow modules) per the CONTEXT-D-21 / 15-VALIDATION.md
F-24 contract, mirroring the Phase 14 D-21 collision-grep pattern
(re-surfaced in 14-VERIFICATION.md).

---

## Commit Hash Manifest

Plans 01-04 were squashed into the `302f950` checkpoint commit during the
PC-transfer hand-off (per its commit message: *"Squashed handoff commit
for PC transfer. Collapses 89 granular commits into one clean checkpoint.
Full per-plan commit history preserved in `.planning/phases/*/SUMMARY.md`
files."*). The per-plan worktree commits cited in each SUMMARY.md
(`893534f`, `dc825d4`, `ad3a0f9`, `8a0a868`, `89dea8d`, `9886dc5`,
`0b15647`, `7b71adc`, `1437376b`, `db5576fb`) live in the squashed
ancestor history; the rebased mainline reference is `302f950`. Plans
05-07 land as discrete commits.

| Plan | Commit(s) | Content |
|------|-----------|---------|
| 15-01 | `302f950` (squashed) | Wave 0 scaffolding — `flow-lang.Tests/Unit/Phase15/` + `Integration/Phase15/` directories, `Shared/MidiReadHelpers.cs` (closes DEFER-05), `tests/output/.gitignore`, 3 placeholder `.flow` scripts wired to FlowScriptData Theory rows |
| 15-02 | `302f950` (squashed) | DX-07 grammar + runtime — 7 files (`MusicalContextStatement`, `MusicalContext`, `ExecutionContext`, `TokenType`, `SimpleLexer`, `Parser`, `Interpreter`) + `ReverbTimeContextTests` (7 Facts: F-01, F-03, F-04, F-05, F-22, F-23 + `Parse_Zero_ProducesDry`) |
| 15-03 | `302f950` (squashed) | DX-07 audio path — `Reverb.Apply(rt60)` Schroeder overload (feedback cap 0.99) + `ProcessChannel` strict refactor (SHA-256 byte-equivalence pin) + `SongRenderer` per-voice reverb (exact-0 short-circuit) + `tests/test_reverb_time.flow` real body + `ReverbApplyRt60Tests` (3 Facts incl. F-06) + `ReverbTimeRenderTests` (3 Facts: F-02, F-07, F-08) |
| 15-04 | `302f950` (squashed) | DX-09 overloads — 2 new `euclidean` `FunctionSignatures` (4-arg swing, 6-arg swing/humanize/seed) via `RegisterContextDependentFunctions` + `std.flow` declarations + `steps>1024` guard + `EuclideanSwingTests` (6 Facts: F-09..F-13, F-21) + `EuclideanHumanizeTests` (6 Facts: F-14..F-18 + `SameSeed_ProducesIdenticalVelocities`) + `FlowEngineRunner.GetVariable` accessor |
| 15-05 | `10c9557` (test, F-19) + `af09ce5` (fix, F-20 + audio-RNG seeding bundled) | DX-09 byte-identical regression — `EuclideanByteIdenticalTests` with empirical Pass-2 velocity bytes `[122, 70, 108]` (F-19) + 352844-byte WAV cross-file `SequenceEqual` (F-20). Audio-layer determinism gap-fix bundled per Phase-14 D-13 divergence-bundle clause: `SynthUtils.cs` (synth white-noise RNG seed `0x55EED` + `ResetNoiseRng()` hook) + `FileIO.cs` (TPDF dither RNG seed `0xD17E2` + per-`ExportWavInternal` reseed) + `SongRenderer.cs` (3 reset calls at `RenderSong*` entry) |
| 15-06 | `bc331f6` (test) + `116aad8` (test) | DX-09 end-to-end `.flow` scripts replacing Plan-01 placeholders — `tests/test_euclidean_swing.flow` (positive + negative swing, two `renderSong`+`writeWav` calls) + `tests/test_euclidean_humanize.flow` (identical-seed dual-write with two `writeMidi` calls). FlowScriptData Theory rows transitioned from placeholder-GREEN to real-usage-GREEN with sentinel contracts unchanged. |
| 15-07 | this closure commit | Phase closure — ROADMAP criterion #3 reframe per D-02 + REQUIREMENTS DX-07/DX-09 Shipped markers + 15-VERIFICATION.md (this file) + 15-VALIDATION.md promotion to `nyquist_compliant: true` + 15-SUMMARY.md + STATE.md/ROADMAP.md update + DEFER-05 strikethrough in `14-deferred-items.md` |

Supporting per-plan documentation commits (SUMMARY rollups + tracking):

- `9de49b0` — `docs(15-05): complete DX-09 byte-identical determinism plan`
- `8b94c92` — `docs(15-06): complete DX-09 end-to-end .flow scripts plan`

---

## Fact Count Rollup

| Source | Count | Status |
|--------|-------|--------|
| `Unit/Phase15/ReverbTimeContextTests` (Plan 02) | 7 | GREEN |
| `Unit/Phase15/ReverbApplyRt60Tests` (Plan 03) | 3 | GREEN |
| `Integration/Phase15/ReverbTimeRenderTests` (Plan 03) | 3 | GREEN |
| `Unit/Phase15/EuclideanSwingTests` (Plan 04) | 6 | GREEN |
| `Unit/Phase15/EuclideanHumanizeTests` (Plan 04) | 6 | GREEN |
| `Integration/Phase15/EuclideanByteIdenticalTests` (Plan 05) | 2 | GREEN |
| FlowScriptData Theory rows (3 new `.flow` scripts) | 3 | GREEN |
| **Phase 15 automated total** | **30** | GREEN |
| F-24 manual collision grep (this plan) | 1 | pinned above |
| **Phase 15 grand total (incl. manual)** | **31** | GREEN/pinned |

Pre-Phase-15 baseline: 257 Facts (post-Plan-01 Wave 0). Post-Phase-15
close: 287 Facts (`dotnet test flow-sharp.sln` 287/287 GREEN at this
closure plan's HEAD). Net delta: +30 Facts (matches the per-plan rollup).

---

## Divergences

Aggregate from per-plan SUMMARYs (full detail in each
`15-NN-SUMMARY.md` §Deviations / §Decisions Made):

- **Plan 02:** probe function name had to drop the `_` prefix
  (`__probeMusicalContext` → `probeMusicalContext`) because Flow's lexer
  reserves `_` as the rest marker (`TokenType.Underscore`). One Rule-1
  test-author bug. Probe pattern adopted: direct
  `InternalRegistry.Register` + `GlobalFrame.DeclareFunction` —
  test-only, zero production-code change, does not touch `std.flow`
  (which Plan 04 was editing concurrently).
- **Plan 03:** four Rule-1 observable refinements during Pass-2 reality
  check: (1) F-06 calibrated at `rt60=1.0s + 10ms RMS window` instead of
  the plan's `rt60=2.0s + single-sample probe at frame 88200` (Schroeder
  + damping makes single-sample probes unreliable); (2) F-02 switched
  from raw-byte WAV `SequenceEqual` to `trailingRms within 10%` (FileIO
  TPDF dither RNG was unseeded — diverged at byte 49); (3) F-07/F-08
  switched from trailing-RMS amplification to
  `CountDivergentPcmSamples > 50%` (per-voice reverb truncates at voice
  buffer boundary — song-trailing region doesn't see the tail);
  (4) `Buffer buf = ...` collided with `TokenType.Buf` keyword — renamed
  to `rendered1`/`rendered2`. RESEARCH Open Q 3 locked at feedback cap
  0.99 (not 0.98). Strict-refactor SHA-256 byte-equivalence pin
  `4FA63B25F7444215...C68A222C7E8` for the existing `Reverb.Apply(roomSize)`
  overload via ephemeral `/tmp/HashCapture` console probe.
- **Plan 04:** F-17 base velocity for `dynamics ff` empirically pinned
  at `0.875` (not the plan's drafted `0.98` speculation — actual value
  per `Parser.NoteStream.TryParseDynamicMarking` line 344). F-16
  humanize narrowed from `0.5` → `0.3` so the perturbed range
  `[0.33, 0.93]` stays inside `[0, 1]` and the D-12 clamp doesn't
  inflate the top bucket as a confound. F-18 RNG-consumer was `vary` as
  drafted (which uses local `new Random` per VariationFunctions.cs:71)
  — local-PRNG isolation property still cleanly observable.
- **Plan 05:** **Pass-1 outcome split** between F-19 (Outcome A — GREEN
  on first run; in-process determinism extended through DryWetMidi
  serialization without gap-fix) and F-20 (Outcome B — RED on first run
  with same byte length / divergence at byte 49; minimal 5-line
  audio-layer gap-fix bundled per Phase-14 D-13 clause). **Two
  pre-existing static unseeded `Random` fields fixed:** synth
  white-noise RNG (`SynthUtils.cs`, fresh discovery — undocumented
  prior) + TPDF dither RNG (`FileIO.cs`, Plan-15-03 documented but
  worked-around). Empirical .NET pin: SDK `10.0.107`,
  `Microsoft.NETCore.App 10.0.7`. Velocity bytes
  `[122, 70, 108]` for
  `euclidean(3, 8, "C4", swing=0.3, humanize=0.1, seed=42)`. WAV byte
  length 352844 (16-bit stereo PCM, 3-hit Sequence under tempo 120 / 4/4).
- **Plan 06:** zero deviations requiring deviation rules. Wave-0
  placeholder rewrite protocol validated end-to-end (T-15-14
  mitigation): `WAVE-0 PLACEHOLDER` marker grep precedes overwrite,
  sentinel lines preserved verbatim, FlowScriptData unchanged. Two-layer
  DX-09 determinism gating now formalized: Plan-04 in-process Value
  Fact + Plan-05 cross-file xUnit byte-equality (F-19) + Plan-06
  script-level `(print "two runs byte-identical: PASSED")` sentinel form
  three independent independence-checks at three different layers.
  Optional `cmp` byte-identity smoke after the Plan-06 script run also
  PASSED (`rc=0`).
- **Plan 07 (this plan):** zero functional deviations. One audit-trail
  observation: ROADMAP criterion #3 reframe necessarily preserves the
  string "rejects negative or zero" inside the quoted "Original wording"
  reframe note (the plan's strict acceptance criterion of `grep -Fc
  "rejects negative or zero" ... returns 0` was overridden by the
  audit-trail-preservation requirement, mirroring Phase-14 DX-06's
  `*Original audit-trail:*` preamble pattern). The reframe is fully
  observable via the three positive criteria added by Task 1a
  (`grep -c "Reframed 2026-04-20 per CONTEXT D-02"` returns 1;
  `grep -Fc "dry-render sentinel"` returns 1; ROADMAP wording now
  matches the shipped Plan-02/Plan-03 reality).

---

## Threat Flags

None. The phase shipped no surface beyond the `<threat_model>` registry
in each plan; F-24 grep confirms no inadvertent identifier-name leak
into user-authored `.flow` files. T-15-14 (placeholder-rewrite blast
radius) was mitigated by Plan-01's `WAVE-0 PLACEHOLDER` marker convention
and Plan-06's pre-overwrite `grep -q` check.

---

## Deferred Items Summary

- **DEFER-05** (`14-deferred-items.md` §DEFER-05) — `Shared/MidiReadHelpers.cs`
  promotion. **CLOSED 2026-04-21 by Phase 15 Plan 01** (helper at
  `flow-lang.Tests/Shared/MidiReadHelpers.cs`); strikethrough applied to
  `14-deferred-items.md` by this closure plan. Two consumers active:
  Phase 14 `DynamicsMidiVelocityTests` (refactored from inline) + Phase
  15 `EuclideanByteIdenticalTests` (F-19 first new use). `grep -rn
  "MidiFile.Read" flow-lang.Tests/` returns exactly 2 lines, both inside
  `Shared/MidiReadHelpers.cs` itself — zero duplicate call sites leaked.
- **DEFER-03** (`14-deferred-items.md` §DEFER-03) — pragma / `enable`
  language construct. **Still OPEN.** Blocks Gaussian humanize
  distribution per CONTEXT D-11 (Phase 15 ships uniform only) and
  blocks DEFER-02 H-alias.
- **DEFER-02** (`14-deferred-items.md` §DEFER-02) — H = B note-stream
  alias. **Still OPEN.** Depends on DEFER-03 shipping first.
- **DEFER-04** (`14-deferred-items.md` §DEFER-04) — multi-letter
  enharmonic-edge respelling (E↔Fb / F↔E# / B↔Cb / C↔B#). **Still OPEN.**
- **DEFER-06** (`14-deferred-items.md` §DEFER-06) — `slice`
  negative-from-end indexing (Pythonic). **Still OPEN.**
- **No new deferred items introduced by Phase 15.**

---

## Sign-off

- [x] All 5 ROADMAP success criteria observable via committed Facts +
      .flow Theory rows + 1 pinned manual grep
- [x] Pre-landing collision grep transcript pinned verbatim above (F-24)
- [x] All atomic commit hashes recorded; squashed-checkpoint provenance
      documented for Plans 01-04
- [x] Full suite green at phase close — 287/287 (`dotnet test
      flow-sharp.sln --nologo`)
- [x] 15-VALIDATION.md promoted to `nyquist_compliant: true`
- [x] REQUIREMENTS.md DX-07 + DX-09 rows flipped to Shipped with commit
      manifests
- [x] STATE.md + ROADMAP.md updated; DEFER-05 closed in
      `14-deferred-items.md` via strikethrough

*Phase 15 closed: 2026-04-25*
