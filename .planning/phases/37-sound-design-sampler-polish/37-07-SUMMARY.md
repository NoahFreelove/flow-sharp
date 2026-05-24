---
phase: 37-sound-design-sampler-polish
plan: 07
subsystem: phase-closer
tags: [closer, tutorial-examples, verification, validation-flip, roadmap-state-requirements-sweep, claude-md, b1-lock-honored]

# Dependency graph
requires:
  - phase: 37-sound-design-sampler-polish
    plan: 01
    provides: DSP-01 granular builtin (composer tutorial #1 subject) + 23-file Wave 0 scaffold (all activated through Plans 37-01..37-06)
  - phase: 37-sound-design-sampler-polish
    plan: 02
    provides: DSP-02 stretch + DSP-03 pitchShift (composer tutorial #2 subject) + StretchEngine identity fast-path + #auto advisory
  - phase: 37-sound-design-sampler-polish
    plan: 03
    provides: MIX-01 audit-pin + MIX-02 SFZ retrofit + SAMP-01/02/03 (5 REQs verified in 37-VERIFICATION)
  - phase: 37-sound-design-sampler-polish
    plan: 04
    provides: PIANO-01 + composer UAT auto-approved in 37-HUMAN-UAT.md (D-37-12)
  - phase: 37-sound-design-sampler-polish
    plan: 05
    provides: FLUTE-01 A4 sample point + variant-locked LICENSE.md
  - phase: 37-sound-design-sampler-polish
    plan: 06
    provides: DRUM-01 + W7 LOCK + Phase 33 SFZ surface inheritance for sampled drums
  - phase: 36-sequence-algebra-generative
    plan: 12
    provides: PATTERN reference for closer shape — examples/{generative,sections}/ allow-list precedent + paired regression test idiom + VERIFICATION.md frontmatter shape (Pitfall 1 paths-in-/tmp, Pitfall 2 no-render-builtin, Pitfall 3 flow-op arg order)
provides:
  - 2 composer-facing tutorial chapters at examples/dsp/ — granular.flow (DSP-01 surface) + stretch_pitchshift.flow (DSP-02 + DSP-03 surface)
  - 2 paired regression tests at tests/ — exercises lazy((...)) Phase 35 test framework idiom (B1 LOCK honored; zero (test "..." (begin ...)) at test-arg position)
  - 37-VERIFICATION.md goal-backward closure doc — 11/11 REQs verified with per-plan commit refs; locked-decisions audit (D-37-01..16 + OQ1..OQ5 + B1/B2/W4/W7 LOCKs)
  - 37-VALIDATION.md frontmatter status: closed + nyquist_compliant: true + wave_0_complete: true + Approval signed off 2026-05-23
  - ROADMAP.md Phase 37 row 6/7 In Progress → 7/7 Complete 2026-05-23; 7 plan-list rows carry commit refs
  - STATE.md frontmatter completed_phases 2 → 3; completed_plans 19 → 26; percent 29 → 43; Current Position → Phase 38 CONTEXT spawn; Performance Metrics gains 7 Phase 37 P01..P07 rows; new "Phase 37 highlights" block
  - REQUIREMENTS.md 11 active-requirement rows flipped [ ] → [x] with commit-ref closure notes; Traceability table 11 rows flipped Pending → Shipped
  - CLAUDE.md gains 7 Music-Specific bullets covering all Phase 37 surfaces; Standard Library Modules audio.flow/sfz.flow updated; Built-in Function Categories Audio Effects bullet documents Phase 37 surface; "Known sampled-instrument quirks" marks Phase 29 v1.5 follow-ups CLOSED
  - .gitignore Phase 37 examples/dsp/ allow-list (mirrors Phase 36 36-12 + Phase 34 D-502 defensive *.wav/*.mp3/*.mid block)
affects: [phase-38-live-coding-2.0 (next phase ready to spawn)]

# Tech tracking
tech-stack:
  added: []  # zero external packages added by 37-07 — pure docs + .flow + .gitignore
  patterns:
    - "Composer tutorial-chapter shape mirrors examples/scala/intro.flow + examples/generative/markov_jazz.flow + examples/sections/parameterized.flow (Phase 32/36 precedent): banner + use blocks + numbered Note: sections + writeWav to /tmp/"
    - "Paired regression test shape mirrors tests/test_markov_jazz_example.flow (Phase 36 36-12 normative): (test \"name\" lazy((...))) wrapping per B1 LOCK; uses @test stdlib + assertEq + assert + assertBytesEqual + getFrames + gt + lt + mul builtins"
    - "/tmp/ writeWav target convention (Phase 36 36-12 §key-decisions): scripts/test_two_run_determinism.sh resolves the writeWav target relative to the script's directory; examples/dsp/<file>.flow writing to examples/output/foo.wav would resolve to examples/dsp/examples/output/foo.wav which the harness cannot find"
    - "No standalone render builtin: examples use createSineTone + granular/stretch/pitchShift + mix + writeWav (NOT renderSong — that's for Sequence-driven multi-instrument songs)"
    - "Flow `->` arg-order pitfall avoidance: examples use named intermediate Buffer variables (g2_wet, g2_panned) instead of `seq -> (every 4 cb)` chains because `->` prepends LHS as first arg, breaking combinators that expect seq LAST"
    - "VERIFICATION.md frontmatter shape (verified/status/score/overrides_applied/re_verification block) mirrors 36-VERIFICATION.md exactly"
    - "VALIDATION.md frontmatter status flip pattern (draft → closed; nyquist_compliant false → true; wave_0_complete false → true; closed: date added)"
    - "ROADMAP.md sweep pattern: Progress table row flip + plan-list rows with commit refs (mirrors Phase 36 36-12 closure)"
    - "STATE.md highlights block insertion above the previous phase's highlights (creates a reverse-chronological phase highlights log)"
    - "REQUIREMENTS.md two-flip pattern: active-requirements [ ] → [x] + closure note; Traceability table Pending → Shipped with commits"
    - "CLAUDE.md Music-Specific bullets are the primary composer-facing surface doc — each Phase 37 REQ gets its own bullet citing the LOCK / decision that anchors it"

key-files:
  created:
    - examples/dsp/granular.flow
    - examples/dsp/stretch_pitchshift.flow
    - tests/test_granular_example.flow
    - tests/test_stretch_pitchshift_example.flow
    - .planning/phases/37-sound-design-sampler-polish/37-VERIFICATION.md
    - .planning/phases/37-sound-design-sampler-polish/37-07-SUMMARY.md
  modified:
    - .gitignore                                                             # +13 / -0 lines: examples/dsp/ allow-list + defensive WAV/MP3/MID block
    - .planning/phases/37-sound-design-sampler-polish/37-VALIDATION.md       # frontmatter status flip + per-task verify column flip + Wave 0 checkboxes
    - .planning/ROADMAP.md                                                   # Phase 37 detail-section plan-list rows + Progress table row 6/7 → 7/7
    - .planning/STATE.md                                                     # frontmatter progress block + Current Position + Resume Instructions + Phase 37 highlights + 7 Performance Metrics rows
    - .planning/REQUIREMENTS.md                                              # 11 active-requirement [ ] → [x] + Traceability 11 rows Pending → Shipped
    - CLAUDE.md                                                              # 7 Music-Specific bullets + Standard Library Modules + Built-in Function Categories + Known quirks update

key-decisions:
  - "B1 LOCK honored (revision pass 2/3): every (test ...) body in tests/test_granular_example.flow + tests/test_stretch_pitchshift_example.flow wraps in lazy((...)) per the tests/test_markov_jazz_example.flow:24-25 normative reference. Zero (test \"...\" (begin ...)) at test-arg position. grep evidence: lazy(( count = 4 + 5 = 9 across the two test files; (test \"...\" (begin pattern count = 0 + 0."
  - "writeWav targets /tmp/ per Phase 36 36-12 §Auto-fixed Issue 1 (path-resolution pitfall). granular.flow → /tmp/granular_demo.wav; stretch_pitchshift.flow → /tmp/stretch_pitchshift_demo.wav. Both pass scripts/test_two_run_determinism.sh harness."
  - "No standalone `render` builtin used — Phase 36 36-12 §Auto-fixed Issue 2. Examples use createSineTone + granular/stretch/pitchShift + mix + writeWav pipeline directly. Composer learning: granular/stretch/pitchShift are Buffer-in / Buffer-out effects, NOT Sequence-driven multi-voice renders."
  - "Flow `->` operator NOT used in examples — Phase 36 36-12 §Auto-fixed Issue 3 (LHS-prepended-as-first-arg pitfall). Examples use named intermediate Buffer variables (g2_wet, g2_panned, mix1/mix2/mix3) for clarity + correctness."
  - "Test framework uses available builtins only: assertEq + assert + assertBytesEqual + getFrames + gt + lt + mul. The plan's prose proposed bytesEqual + length but those don't exist as Flow builtins — `length(Buffer)` is absent (getFrames is the canonical accessor) and `bytesEqual(Buffer, Buffer)` is absent (assertBytesEqual is an assertion only, not a Bool-returning predicate). The replacement assertions exercise the same composer surface (jitter=0.0 deterministic, frame-count equality across windowings, identity fast-paths)."
  - "VERIFICATION.md anti-patterns table gains a NEW Phase 37 entry: `(test \"...\" (begin ...))` substitution silently breaks the Phase 35 test framework because (begin ...) evaluates immediately at registration time. The B1 LOCK locked the lazy((...)) idiom into PLAN scaffolding so future closers inherit it."
  - "STATE.md progress arithmetic (per plan SUCCESS_CRITERIA): completed_phases 2 → 3 (+ Phase 37); completed_plans 19 → 26 (+ Plans 37-01..37-07); percent 29 → 43 (3/7 = 42.86% → 43% rounded). total_plans stays at 26 — v1.5 milestone metric is currently the running count of plans completed in shipped phases, not the projected v1.5 total."
  - "Total CLAUDE.md Phase 37 mentions = 10 (≥3 acceptance gate met); granular/pitchShift mentions = 4 (≥2 acceptance gate met). Coverage spans Music-Specific bullets + Standard Library Modules + Built-in Function Categories + Known quirks update."
  - "Pre-existing 34-failure baseline preserved (zero new regressions). Full dotnet test --no-build reports 1574 PASS / 34 FAIL / 0 SKIPPED — same set documented in deferred-items.md (Phase 28 PerSynthArticulationTests FFT cosine drift × 17 + Phase 28 RagtimeFixtureTests RMS drift × 2 + Phase 30 FlowMidi quantizer × 2 + Phase 35 MatchExhaustivenessDefaultTests × 2 + the misc additional Phase 28/29 sampled-instrument articulation deltas). Plan 37-01 SUMMARY established the baseline at 34; plans 37-02/03/04/06 all confirmed the failure-set unchanged. Triage belongs in a dedicated cleanup plan or v1.6 — out of scope per executor SCOPE BOUNDARY rule."

requirements-completed: []  # 37-07 closes the phase via verification/sweep only — REQ closure was done by upstream plans

# Metrics
duration: 30m
completed: 2026-05-23
---

# Phase 37 Plan 07: Sound Design + Sampler Polish — Phase Closer Summary

**Phase 37 closure plan: ships 2 composer-facing tutorial chapters at examples/dsp/ + 2 paired regression tests + 37-VERIFICATION.md + 37-VALIDATION.md frontmatter flip + ROADMAP/STATE/REQUIREMENTS sweep + CLAUDE.md Phase 37 features section, mirroring the Phase 36 Plan 36-12 closer shape per D-37-01.**

## One-liner

Phase 37 closure: 2 tutorial chapters (`examples/dsp/granular.flow` + `examples/dsp/stretch_pitchshift.flow`) + 2 paired regression tests honoring B1 LOCK `lazy((...))` idiom + goal-backward 37-VERIFICATION.md (11/11 REQs) + 37-VALIDATION.md flipped closed + ROADMAP/STATE/REQUIREMENTS/CLAUDE.md swept; Phase 37 ships 11/11 REQs across 7 plans; Phase 38 (Live Coding 2.0) unblocked.

## Performance

- **Duration:** ~30 minutes
- **Started:** 2026-05-23 (Wave 5 closer, sole executor)
- **Completed:** 2026-05-23T15:57:51Z
- **Tasks:** 2 atomic commits
- **Files modified:** 12 (6 created + 6 modified)

## Task Commits

1. **Task 1: composer tutorial chapters + paired regression tests + .gitignore allow-list** — `091d158` (feat)
2. **Task 2: 37-VERIFICATION.md + 37-VALIDATION.md flip + ROADMAP/STATE/REQUIREMENTS sweep + CLAUDE.md Phase 37 section** — `82a0db9` (docs)

## Files Created/Modified

### Tutorial chapters (NEW)
- `examples/dsp/granular.flow` — composer-facing tutorial for DSP-01 granular synthesis. 5-section walkthrough: default knobs → jitter override + Gaussian windowing → composability with reverb + pan → mix + writeWav. Writes to `/tmp/granular_demo.wav`.
- `examples/dsp/stretch_pitchshift.flow` — composer-facing tutorial for DSP-02 + DSP-03. 7-section walkthrough: source Buffer → #vocoder / #psola / #auto stretch → +5st pitchShift → identity fast-paths (factor=1.0, 0c) → 3-way mix + writeWav. Writes to `/tmp/stretch_pitchshift_demo.wav`.

### Paired regression tests (NEW)
- `tests/test_granular_example.flow` — 3 facts via the Phase 35 `(test "name" lazy((...)))` framework: (1) default-knob granular produces non-empty Buffer, (2) `jitter=0.0` deterministic (byte-equal across two calls), (3) Hann vs Gaussian windowing produce same frame count.
- `tests/test_stretch_pitchshift_example.flow` — 4 facts: (1) `stretch base 1.0` identity fast-path byte-equal, (2) `pitchShift base 0c` identity fast-path byte-equal, (3) `stretch base 2.0 #vocoder` grows frame count above 1× baseline, (4) frame count stays below 3× ceiling (vocoder overlap-add slack).

### Closure planning docs
- `.planning/phases/37-sound-design-sampler-polish/37-VERIFICATION.md` (NEW) — goal-backward closure doc, 11 observable-truths table + 35-row required-artifacts table + 11-row per-requirement-verification table + behavioral spot-checks + locked-decisions verification (D-37-01..16 + OQ1..OQ5 + D-v1.5-03/06/09 + B1/B2/W4/W7 LOCKs) + anti-patterns + v1.6 follow-ups + Next-Phase Readiness (Phase 38 inheritance).
- `.planning/phases/37-sound-design-sampler-polish/37-VALIDATION.md` (MODIFIED) — frontmatter `status: draft → closed`, `nyquist_compliant: false → true`, `wave_0_complete: false → true`, `closed: 2026-05-23` added; per-task verification map status column ⬜ pending → ✅ green on all 25 rows; Wave 0 checkboxes [ ] → [x]; Approval signed off 2026-05-23.
- `.planning/ROADMAP.md` (MODIFIED) — Phase 37 detail-section plan list 37-07 flipped [ ] → [x] + all 7 plan-list rows now carry commit refs; Progress table row `37. Sound Design + Sampler Polish | v1.5 | 6/7 | In Progress |` → `| 7/7 | Complete | 2026-05-23 |`.
- `.planning/STATE.md` (MODIFIED) — frontmatter `progress` block: completed_phases 2 → 3, completed_plans 19 → 26, percent 29 → 43; `stopped_at` + `last_updated` + `last_activity` bumped; Current Position → Phase 38 CONTEXT spawn; Resume Instructions (top) rewritten for Phase 38; **NEW** "Phase 37 highlights" block (8 bullets); Performance Metrics gains 7 Phase 37 P01..P07 rows.
- `.planning/REQUIREMENTS.md` (MODIFIED) — 11 active-requirement rows (DSP-01..03, MIX-01..02, SAMP-01..03, PIANO-01, FLUTE-01, DRUM-01) flipped `[ ] → [x]` with per-row closure notes citing plan commits; Traceability table 11 rows flipped `Pending → Shipped (Plan 37-XX — commits)`.
- `CLAUDE.md` (MODIFIED) — 7 NEW Music-Specific bullets covering all Phase 37 surfaces (granular DSP-01, stretch + pitchShift DSP-02/03, MIX-01/02 + SAMP-01/02/03, PIANO-01 4-way crossfade + release knob, FLUTE-01 A4, DRUM-01 W7 LOCK); "Known sampled-instrument quirks (v1.5 backlog)" updated to mark Phase 29 v1.5 follow-ups CLOSED; Standard Library Modules `audio.flow` + `sfz.flow` entries gained Phase 37 references; Built-in Function Categories Audio Effects (Audio/DSP/) bullet documents the new granular/stretch/pitchShift surface + DSP utility classes.

### Repo config
- `.gitignore` (MODIFIED) — Phase 37 examples/dsp/ allow-list (mirrors Phase 36 36-12 examples/{generative,sections}/ precedent): `!examples/dsp/ !examples/dsp/** !examples/dsp/**/*.flow !examples/dsp/**/*.md` + defensive *.wav/*.mp3/*.mid block (Phase 34 D-502 defensive enforcement).

## Two-Run Cmp-Clean Determinism Evidence

Phase 18/25/27/28/29/33/36 inheritance preserved on both Plan 37-07 tutorial chapters:

| Example | SHA-256 (Run A == Run B) | Result |
|---------|---|---|
| `examples/dsp/granular.flow` | `76877a3c90cffa190f4960430df626a28055b1d75f11a5d274f12ae9a832871a` | PASS — byte-identical across 2 consecutive renders |
| `examples/dsp/stretch_pitchshift.flow` | `5676da6d8570d213e3512c0f77ea3f1732ed9f63a68528891e4297aa75c64f93` | PASS — byte-identical across 2 consecutive renders |

Harness command:
```bash
bash scripts/test_two_run_determinism.sh examples/dsp/<file>.flow \
    --render-cmd "dotnet run --project flow-cli --no-build -- run <SCRIPT>"
```

## B1 LOCK Evidence (Revision Pass 2/3)

Plan 37-07 specified the B1 LOCK as PLAN-PRESCRIBED — every `(test ...)` body MUST wrap in `lazy((...))` per the `tests/test_markov_jazz_example.flow:24-25` normative reference. Grep evidence post-commit:

| Acceptance check | Required | Actual | File |
|---|---:|---:|---|
| `lazy((` matches | ≥ 3 | 4 | `tests/test_granular_example.flow` |
| `lazy((` matches | ≥ 3 | 5 | `tests/test_stretch_pitchshift_example.flow` |
| `^\s*\(test "[^"]+"\s+\(begin` matches | == 0 | 0 | `tests/test_granular_example.flow` |
| `^\s*\(test "[^"]+"\s+\(begin` matches | == 0 | 0 | `tests/test_stretch_pitchshift_example.flow` |

Zero `(begin ...)` substitutions at test-arg position. Bodies that need multiple statements use `lazy((begin ... ))` (outer wrapper deferred; inner `begin` evaluated when the runner forces the thunk) — Plan 37-07 didn't need this multi-statement form in practice; all 7 facts are single-assertion.

## Test Results

| Test command | Result |
|---|---|
| `dotnet run --project flow-cli -- test tests/test_granular_example.flow` | 3/3 PASS (granular_default_produces_nonempty_buffer, granular_jitter_zero_is_deterministic, granular_windowing_options_produce_same_frame_count) |
| `dotnet run --project flow-cli -- test tests/test_stretch_pitchshift_example.flow` | 4/4 PASS (stretch_factor1_is_identity, pitchshift_0cents_is_identity, stretch_2x_grows_frame_count_within_slack, stretch_2x_frame_count_below_3x_ceiling) |
| `dotnet build -c Debug` | 0 Errors / 32 pre-existing Warnings |
| `dotnet test --no-build` (full suite) | 1574 PASS / 34 FAIL / 0 SKIPPED. The 34 failures are the pre-existing Phase 28 PerSynthArticulationTests (FFT cosine drift) + Phase 28 RagtimeFixtureTests (RMS drift) + Phase 30 FlowMidi quantizer + Phase 35 MatchExhaustivenessDefaultTests baseline documented in deferred-items.md — NOT introduced by Phase 37. Zero new regressions. |

## Phase 37 Cumulative Test Count

| Subsystem | Plan | xUnit facts (this plan) | Composer .flow tests |
|---|---|---:|---:|
| WindowFunctions / GranularSynthesis / GranularDeterminism | 37-01 | 8 | — |
| Stretch / PitchShift (Vocoder/PsolaTransient/AutoAdvisory/Identity/PitchShift) | 37-02 | 11 (rounded — 13 total per Plan 37-02 SUMMARY) | — |
| MIX-01 RMS regression + MIX-02 SfzPanRetrofit/Composition + SAMP-01 RoundRobin/Determinism + SAMP-02 VelocityCrossfade/HardSwitchRegression + SAMP-03 StaccatoEnergy | 37-03 | 13 | — |
| PIANO-01 SampleCacheLayers/ReleaseKnob + Phase37RmsRegression | 37-04 | 5 | — |
| FLUTE-01 SampleCache/D5Crossover | 37-05 | 2 | — |
| DRUM-01 SfzDrumsLoad/DrumPitchShiftAuto | 37-06 | 7 | — |
| Plan 37-07 paired regression tests | 37-07 | — | 7 (3 + 4) |
| **Phase 37 total** |  | **46** xUnit facts | **7** composer .flow facts |

Cross-suite regression posture preserved: Phase 33 SFZ suite 72/72 GREEN; Phase 28 articulation rules + velocity 17/17 GREEN; Phase 27 byte-identical pragma 4/4 GREEN.

## Carryovers Logged for v1.6

All explicit deferrals captured in 37-VERIFICATION.md §"v1.6 Follow-ups Logged (NOT v1.5 gaps)":

- **PIANO-01 Option B escalation** (Plan 37-03 SUMMARY decisions): reserved if future ragtime UAT iteration flags the staccato gap. Plan 37-04 SUMMARY confirms Path 1 (synthesized mp via RMS-interpolation α=0.6) held; Path 2 (more chromatic pitch points) + Path 3 (re-open D-37-10 source) NOT triggered.
- **FLUTE D5 sample point** (Plan 37-05 SUMMARY composer verdict): A4 chosen over D5 per RESEARCH §Pattern 10; if future composer listening flags D5 still gappy, a 4th flute sample point at D5 can land in v1.6.
- **Sparse-named-arg call ergonomics** (Plan 37-02 SUMMARY Known Limitations): OverloadResolver requires positional+named=arity AND Signature.Equals ignores ParameterNames. v1.6 resolver-relaxation plan can default unbound slots to Void.
- **Auto-mode HPS rendering cost** (Plan 37-02 SUMMARY): current `StretchEngine.ProcessAuto` renders both engines for the whole buffer then per-output-frame selects. v1.6 can render only the chosen engine per-frame with careful boundary cross-fading.
- **PSOLA octave-error edge cases** (Plan 37-02 SUMMARY): YIN handles standard speech + music well but pathological inputs (very low-pitch near minTau bound, glissandos through octave boundaries) may still produce octave errors. Composer bypass via `pitchPeriodOverride=`.
- **SAMP-03 Option B full per-frame curve overlay** (Plan 37-03 SUMMARY): reserved escalation.
- **`(str Sfz)` overload** (Plan 37-06 SUMMARY): composer cannot currently `(print (str drums))` to inspect a loaded patch. Phase 33-era limitation; v1.6 candidate.
- **String-overload percussion opt-in builtin** (Plan 37-06 SUMMARY): composers using `loadSfz("/path/to/X.sfz")` bypass path cannot activate percussion routing. v1.6 candidate: `(asPercussion sfz)` wrapper.
- **GM-StylePerc.sfz sample-load validation** (Plan 37-06 SUMMARY): VSCO-CE 1.1.0's `default_path=Percussion\` cascade doesn't fully resolve in every install snapshot. Charitable silent fallback shipped; v1.6 can validate sample paths up-front.
- **Bundled-piano variant of ragtime fixture** (Plan 37-04 + 37-HUMAN-UAT.md "Gaps"): Phase 37 closer (this plan) chose NOT to fork; composer can re-listen + override per 37-HUMAN-UAT "Composer Re-Listen Log" any time.
- **34 pre-existing test failures** (deferred-items.md): triage belongs in a dedicated cleanup plan; out of scope per executor SCOPE BOUNDARY rule.

## Phase 38 (Live Coding 2.0) Inheritance from Phase 37

Documented in 37-VERIFICATION.md §"Next-Phase Readiness":

- **Granular DSP-01 composability with live mic input (AUDIO-IN-01):** composer can pipe `(micBuffer 1.0)` through `(granular buf grain=50ms density=20Hz jitter=0.3)` for real-time texture creation
- **Stretch/pitchShift in live blocks (LIVE-01):** `live 1bar { ... (stretch buf 1.5 mode=#auto) ... }` works out-of-the-box; PrngRegistry render-boundary reseed holds across live re-evaluations
- **Per-voice pan for live mix experiments (LIVE-01..03):** composer authors `voice.Pan` per-voice inside a `live` block; MIX-01 (synth) + MIX-02 (SFZ) both honor the per-call attribute
- **B2 unconditional stereo lock:** Phase 38's REPL `(inspect seq)` piano-roll preview can assume SFZ-rendered output is ALWAYS stereo (centered = equal L/R at √0.5) — no legacy mono-when-pan-default-0 branch
- **W7 LOCK dict-symbol-driven semantic flag pattern:** future Phase 38 live-block dict-symbols (e.g. `#live`, `#quantize`) can follow the same load-time semantic-flag pattern documented in Plan 37-06 W7 LOCK

## Deviations from Plan

### None requiring auto-fix.

The plan executed exactly as written for both tasks. Three minor adjustments from prose-vs-reality:

1. **Test framework builtins** — the plan's example test scaffolding suggested `bytesEqual` + `length(Buffer)` builtins. Reality: `assertBytesEqual` is an assertion (NOT a Bool-returning predicate), and `length(Buffer)` doesn't exist (canonical accessor is `getFrames(Buffer)`). The test files use the actual @test stdlib surface — `assertBytesEqual` for the identity fast-path checks + `getFrames` + `assert (gt ...)` + `assertEq` for frame-count checks. The composer surface exercised is identical to what the plan describes (jitter=0.0 determinism, frame-count invariants under windowing options, identity fast-paths byte-equal). Documented in key-decisions block.

2. **Two-run determinism harness invocation** — `flow` is not on PATH in the worktree; harness invocation used `--render-cmd "dotnet run --project flow-cli --no-build -- run <SCRIPT>"` to substitute the system `flow render` default. Both SHA-256s came back byte-identical across consecutive runs, satisfying the determinism gate without modification to the underlying contract. (The harness's `--render-cmd` flag exists precisely for this scenario; documented in scripts/test_two_run_determinism.sh §Usage.)

3. **STATE.md `total_plans` arithmetic** — the plan's frontmatter said `completed_plans 19 → 26` AND implicitly left `total_plans: 26` unchanged. That math is internally consistent (`completed 19 → 26 of total 26 = 73% → 100% for the shipped phases`) but reflects a "shipped-phase-only" running count rather than a v1.5 milestone projection. Honored verbatim as the plan declared.

No Rule 1/2/3 auto-fixes triggered. No scope creep.

## Issues Encountered

- **34 pre-existing test failures persist** — same set documented across Plan 37-01..37-06 SUMMARYs' `Issues Encountered` sections (Phase 28 PerSynthArticulation FFT × 17 + Phase 28 RagtimeFixtureTests × 2 + Phase 30 FlowMidi quantizer × 2 + Phase 35 match exhaustiveness × 2 + additional Phase 28/29 sampled-instrument articulation deltas). Plan 37-07 closer does NOT address these per executor SCOPE BOUNDARY rule — they need a dedicated cleanup plan or v1.6 triage. Tracked in `deferred-items.md`.

- **CONTEXT date drift mid-execution** — the worktree clock advanced from 2026-05-22 to 2026-05-23 during execution (Plan 37-01 SUMMARY's completion is 2026-05-22; subsequent Plan 37-02..37-06 SUMMARYs all show 2026-05-23). Closer used 2026-05-23 throughout (consistent with Plans 37-02 through 37-06 and the orchestrator's `last_updated` field). VERIFICATION.md timestamp is the actual UTC at execution time.

## Threat Flags

None — no new security surface introduced by Plan 37-07 (closer is pure docs + .flow + .gitignore additions). The threat model from the plan frontmatter:

- T-37-07-01 (writeWav path resolution under harness): MITIGATED — both examples target `/tmp/`; harness verified byte-identical
- T-37-07-02 (standalone `render` builtin nonexistent): MITIGATED — examples use `createSineTone` + DSP-effect + `mix` + `writeWav` pipeline
- T-37-07-03 (Flow `->` arg-order mismatch on combinator chains): MITIGATED — examples use named intermediate variables, NOT `->` chains
- T-37-07-04 (REQUIREMENTS.md or ROADMAP.md flip omits a REQ row): MITIGATED — 11 REQs explicit in frontmatter + Task 2 grep-acceptance verified
- T-37-07-05 (Two-run cmp-clean broken on examples — PrngRegistry inheritance): MITIGATED — Plans 37-01 + 37-02 guarantee PrngRegistry routing; harness verified byte-identical SHA-256s
- T-37-07-06 (B1 anti-pattern: `(begin ...)` at test-arg position silently breaks tests): MITIGATED — plan PRESCRIBED `lazy((...))` idiom; grep acceptance criteria verified zero `(begin ...)` substitutions
- T-37-07-SC (npm/pip/cargo installs): N/A — Plan 37-07 ships zero external packages

## Self-Check: PASSED

**Files exist on disk:**

- FOUND: examples/dsp/granular.flow
- FOUND: examples/dsp/stretch_pitchshift.flow
- FOUND: tests/test_granular_example.flow
- FOUND: tests/test_stretch_pitchshift_example.flow
- FOUND: .planning/phases/37-sound-design-sampler-polish/37-VERIFICATION.md
- FOUND: .planning/phases/37-sound-design-sampler-polish/37-07-SUMMARY.md (this file)

**Commits exist:**

- FOUND: 091d158 (Task 1 — examples + tests + .gitignore)
- FOUND: 82a0db9 (Task 2 — VERIFICATION + VALIDATION flip + ROADMAP + STATE + REQUIREMENTS + CLAUDE.md)

**Verification gates:**

- `dotnet build -c Debug` → 0 errors
- `dotnet test --no-build` → 1574 PASS / 34 FAIL / 0 SKIPPED — same pre-existing baseline; zero new regressions
- `bash scripts/test_two_run_determinism.sh examples/dsp/granular.flow` → PASS (SHA-256 `76877a3c...`)
- `bash scripts/test_two_run_determinism.sh examples/dsp/stretch_pitchshift.flow` → PASS (SHA-256 `5676da6d...`)
- `dotnet run --project flow-cli -- test tests/test_granular_example.flow` → 3/3 PASS
- `dotnet run --project flow-cli -- test tests/test_stretch_pitchshift_example.flow` → 4/4 PASS
- All Task 2 acceptance criteria grep counts verified post-commit (Phase 37 mention count = 10 in CLAUDE.md; 11/11 REQ closure; etc.)

---

*Phase: 37-sound-design-sampler-polish*
*Plan: 07 (Closer — examples + 37-VERIFICATION.md + ROADMAP/STATE/REQUIREMENTS/CLAUDE.md sweep)*
*Completed: 2026-05-23*
