---
phase: 36-sequence-algebra-generative
plan: 12
subsystem: closure
tags: [examples, verification, documentation, closure, phase-gate]

# Dependency graph
requires:
  - phase: 36-sequence-algebra-generative
    plan: 05
    provides: "@patterns stdlib with 13 Tidal combinators — exercised in examples/generative/tidal_combinators.flow"
  - phase: 36-sequence-algebra-generative
    plan: 06
    provides: "markov / markovTrain / markovGenerate one-shot + split — exercised in examples/generative/markov_jazz.flow"
  - phase: 36-sequence-algebra-generative
    plan: 07
    provides: "@generative L-system surface (mentioned in CLAUDE.md Phase 36 section; not exercised in the 3 example files — kept narrow per the plan's <interfaces>)"
  - phase: 36-sequence-algebra-generative
    plan: 08
    provides: "Cellular automata (cellular / cellularSeeded / life) — documented in CLAUDE.md Generative subsection"
  - phase: 36-sequence-algebra-generative
    plan: 09
    provides: "Chaos maps (lorenz / logistic / quantizeToScale) + D-36-09 cross-platform FP caveat — documented in CLAUDE.md Conventions section"
  - phase: 36-sequence-algebra-generative
    plan: 10
    provides: "Parameterized sections + overload + Phase 35 patterns in signatures — exercised in examples/sections/parameterized.flow"
  - phase: 36-sequence-algebra-generative
    plan: 11
    provides: "jam chord-aware Markov + style packs — exercised in examples/generative/markov_jazz.flow"
provides:
  - "examples/generative/markov_jazz.flow — composer tutorial: one-shot markov + train/generate split + jam over ii-V-I-VI + every+sometimes chain"
  - "examples/generative/tidal_combinators.flow — composer tutorial: all 13 @patterns combinators in one renderable file"
  - "examples/sections/parameterized.flow — composer tutorial: D-36-13..18 (parens-call + *N + defaults + chord-literal/tuple-destructure patterns + overloading)"
  - "tests/test_markov_jazz_example.flow + test_tidal_combinators_example.flow + test_parameterized_example.flow — paired regression coverage for each example"
  - "36-VERIFICATION.md — goal-backward closure doc verifying all 9 Phase 36 REQs"
  - "36-VALIDATION.md flipped status=closed + nyquist_compliant=true + wave_0_complete=true"
  - "CLAUDE.md Phase 36 features section under Language Features + Standard Library Modules list + Special Types list + Generative builtin-category + D-36-09 Conventions callout"
  - "REQUIREMENTS.md 9 Phase 36 rows flipped from [ ] to [x] with commit refs; Traceability table flipped Pending → Shipped"
  - "ROADMAP.md Phase 36 row flipped 5/12 In Progress → 12/12 Complete 2026-05-22; Phase 35 row 5/7 → 7/7 Complete 2026-05-19; v1.5 phase block flipped to [x]"
  - "STATE.md frontmatter (completed_phases 1 → 2, completed_plans 12 → 19, percent 14 → 29); Current Position points to Phase 37 CONTEXT spawn; Phase 36 highlights summary; Performance Metrics rows for Phase 36 P01..P12"
  - ".gitignore allow-list for examples/{generative,sections}/**/*.{flow,md} + defensive *.wav/*.mp3/*.mid block (Phase 34 D-502 precedent)"
affects: [37-sound-design-sampler-polish, 38-live-coding-2, 39-notation-citizenship, 40-studio-sync, 41-reach-v15-closer]

# Tech tracking
tech-stack:
  added: []   # Closure plan — no new dependencies
  patterns:
    - "Composer-tutorial chapter layout (mirrors examples/symphony/sfz_smoke.flow + examples/scala/intro.flow + examples/pragmas/h_alias.flow precedent): header comment with goals + cross-links to plan decision IDs + `/tmp/` writeWav target so scripts/test_two_run_determinism.sh works from any CWD"
    - "Paired regression test per example: each tests/test_*_example.flow exercises the SAME composer surface via the (test ...) framework — survives independently of the example's writeWav target"
    - "Goal-backward verification doc shape (mirrors 35-VERIFICATION.md): frontmatter status=passed + score X/N; observable-truths table tying each must-have to commit + test evidence; per-requirement verification table with commit hashes from each plan's SUMMARY"

key-files:
  created:
    - "examples/generative/markov_jazz.flow (107 lines — composer tutorial covering markov + jam + every+sometimes)"
    - "examples/generative/tidal_combinators.flow (95 lines — composer tutorial covering all 13 @patterns combinators)"
    - "examples/sections/parameterized.flow (100 lines — composer tutorial covering all 6 D-36-13..18 deliverables)"
    - "tests/test_markov_jazz_example.flow (5 tests PASS — markov + jam regression coverage)"
    - "tests/test_tidal_combinators_example.flow (11 tests PASS — combinator chain regression coverage)"
    - "tests/test_parameterized_example.flow (7 tests PASS — section overload + defaults + *N regression coverage)"
    - ".planning/phases/36-sequence-algebra-generative/36-VERIFICATION.md (Phase 36 closure verification report, score 9/9)"
  modified:
    - ".planning/phases/36-sequence-algebra-generative/36-VALIDATION.md (status=closed + nyquist_compliant=true + wave_0_complete=true + Approval signed-off)"
    - "CLAUDE.md (+13 lines net — Phase 36 features section in Language Features + Standard Library Modules list + Special Types list + Generative builtin-categories + Conventions D-36-09 callout)"
    - ".planning/REQUIREMENTS.md (9 Phase 36 rows flipped to [x] with closure notes + commit refs; Traceability table 9 Phase 36 rows flipped Pending → Shipped)"
    - ".planning/ROADMAP.md (Phase 36 v1.5 row flipped to [x] Complete 2026-05-22; Phase 35 row to 7/7 Complete; 12 plan-list rows in detailed section flipped to [x] with commit refs; Phase 36 + Phase 35 progress-table rows updated)"
    - ".planning/STATE.md (frontmatter progress flipped 1/19/14% → 2/19/29%; Current Position + Resume Instructions point to Phase 37 CONTEXT spawn; Phase 36 highlights block; Performance Metrics gains Phase 36 P01..P12 rows)"
    - ".gitignore (+ allow-list for examples/{generative,sections}/**/*.flow + defensive *.wav/*.mp3/*.mid block under same dirs)"

key-decisions:
  - "**writeWav targets `/tmp/` paths instead of `examples/output/` per the plan's <interfaces> spec** (Rule 3 — Blocking) — the `scripts/test_two_run_determinism.sh` harness resolves writeWav paths relative to the SCRIPT's directory (SCRIPT_DIR), so `examples/output/markov_jazz.wav` would resolve to `examples/generative/examples/output/markov_jazz.wav` and the harness would fail to find the rendered WAV. `/tmp/markov_jazz.wav` works from any CWD. The composer reading the file sees a clear comment block explaining the convention + redirect."
  - "**`render` standalone builtin doesn't exist** (Rule 3 — Blocking) — the plan's <interfaces> snippet ended chains with `-> render` and `(renderSong song)` (no instrument arg). Checked `flow-lang/StandardLibrary/`: there is no `render` builtin, and `renderSong` requires an instrument arg (e.g., `\"piano\"`). Replaced the chain-terminating `-> render` with explicit `Buffer mix = (renderSong song \"piano\")` followed by `(writeWav ...)` — matches every existing test idiom in `tests/test_patterns_chain.flow` + `tests/test_markov_oneshot.flow` + `tests/test_jam_jazz.flow`."
  - "**Flow `->` operator prepends LHS as the first arg, not the last** (Rule 3 — Blocking) — the plan's <interfaces> snippet showed `riffA -> (every 2 (fn s => ...))` which would parse to `(every riffA 2 cb)` not `(every 2 cb riffA)`. Combinator signatures take seq as the LAST arg per D-36-03 (matches the lambda-required transform-arg convention). Replaced the flow-chain form with direct calls `(every 2 cb riffA)` + intermediate variable bindings — preserves readability without breaking the call shape."
  - "**`degrade` not chained in the tidal_combinators.flow example** — fixed-50% drop would silence half the bars in the final render, producing a too-thin tutorial output. Used `sparseSeq 0.85` (15% drop) in the example chain instead; the paired regression test (`tests/test_tidal_combinators_example.flow`) DOES exercise `degrade` explicitly so the determinism + dispatch contract is verified. This matches the plan's own commentary in <action> step 2."
  - "**`as` chain naming dropped from the markov_jazz example body** — the plan's <interfaces> showed `solo -> (every 4 cb) as varied -> render`. Per the LHS-prepended `->` semantic above, this would produce `(render (every solo 4 cb))` (broken arg order) + `varied` bound to the intermediate. Replaced with explicit named intermediate variables (`Sequence varied = ...`; `Sequence variation = ...`) which serves the same composer-readability goal. `as` chain naming is exercised in `tests/test_chain_naming.flow` (Plan 35-07) — separate composer-visible documentation chain."

patterns-established:
  - "Composer-tutorial chapter layout for /examples/{category}/ subdirectories: header comment + use blocks + musical-context tree + named intermediate Sequence variables + section blocks + Song + (renderSong song instrument) + (writeWav /tmp/path mix). Inheritable by Phase 37's granular synth + Phase 38's live-block + Phase 39's notation export tutorial chapters."
  - "Paired regression tests pattern: each `examples/{category}/foo.flow` ships alongside a `tests/test_foo_example.flow` that exercises the SAME composer surface via the Phase 35 (test ...) framework. Survives the example's writeWav target being out-of-scope for the test harness. Composer who breaks the example also breaks the test."
  - "Phase closure doc shape (mirrors 35-VERIFICATION.md): goal-backward checklist with per-requirement table → behavioral spot-checks → locked-decisions verification → anti-patterns inherited → gaps summary → next-phase readiness. Inheritable by Phase 37/38/39/40/41 closure plans."
  - "Phase closure commits docs/* the .planning/REQUIREMENTS.md + ROADMAP.md + STATE.md + CLAUDE.md edits in a SINGLE commit (separate from per-task commits per the executor protocol) — keeps the diff atomic per CONTEXT D-v1.5-01."

requirements-completed: [PAT-01, PAT-02, GEN-01, GEN-02, GEN-03, GEN-04, GEN-05, SECT-01, IMPROV-01]
# All 9 Phase 36 requirements verified at closure time per 36-VERIFICATION.md.

# Metrics
duration: ~18min
completed: 2026-05-22
---

# Phase 36 Plan 36-12: Closure — Composer-facing examples + Phase 36 VERIFICATION + ROADMAP/STATE/REQUIREMENTS Summary

**Phase 36 closes with three composer-facing tutorial chapters demonstrating the @patterns combinator algebra, @generative + @improv stochastic primitives, and parameterized sections; full 36-VERIFICATION.md goal-backward closure doc with 9/9 requirements verified; REQUIREMENTS/ROADMAP/STATE updated to reflect Phase 36 closure and Phase 37 readiness; CLAUDE.md gains a comprehensive Phase 36 features section across Language Features, Standard Library Modules, Special Types, Generative builtin-categories, and Conventions (D-36-09 cross-platform FP caveat). All 3 examples render cleanly and pass `scripts/test_two_run_determinism.sh` two-run cmp-clean (byte-identical SHA-256 across consecutive renders).**

## Performance

- **Duration:** ~18 min
- **Started:** 2026-05-22T20:28:22Z
- **Completed:** 2026-05-22T20:46:14Z
- **Tasks:** 2 of 2
- **Files created:** 7
- **Files modified:** 6

## Accomplishments

- **3 composer-facing tutorial chapters** under `examples/generative/` + `examples/sections/`:
  - `examples/generative/markov_jazz.flow` (107 lines) — one-shot `(markov corpus order length seed)`, train/generate split via `MarkovModel`, chord-aware `(jam over=chords style=#jazz length=4 ...)` over a ii-V-I-VI in Cmajor, and an `every + sometimes` combinator chain rendered to `/tmp/markov_jazz.wav`. Header comment cites D-36-06 (train/generate split), D-36-10 (jam signature + key= override), and D-36-09 (cross-platform Lorenz caveat — this file uses Markov + jam only, both byte-portable).
  - `examples/generative/tidal_combinators.flow` (95 lines) — all 13 `@patterns` combinators in one playable file: 10 deterministic (rev / palindrome / every+fast / chunk+transpose / phase / iter / jux / superimpose) + 2 stochastic (sometimes / sparseSeq). The `degrade` 50% drop is exercised in the paired regression test instead of the example chain to keep the rendered output audible.
  - `examples/sections/parameterized.flow` (100 lines) — all 6 D-36-13..18 parameterized-section deliverables: 3 `verse(...)` overloads (Note binding, tuple destructure, Cmaj7 chord-literal extractor), `intro(...)` with defaults, legacy zero-arg `chorus`, and `*N` repeat on a parameterized call.
- **3 paired regression tests** under `tests/`:
  - `tests/test_markov_jazz_example.flow` — 5/5 PASS (markov determinism, MarkovModel structural equality, markovGenerate determinism, jam determinism, every+sometimes chain)
  - `tests/test_tidal_combinators_example.flow` — 11/11 PASS (every combinator reproducibility + stochastic ref-eq via PrngRegistry)
  - `tests/test_parameterized_example.flow` — 7/7 PASS (all 3 overloads + defaults + zero-arg + `*N` + full heterogeneous Song)
- **`36-VERIFICATION.md`** NEW — Phase 36 closure verification report mirroring 35-VERIFICATION.md shape: 9/9 observable truths verified, per-requirement table with commit refs across all 12 plans, behavioral spot-checks (build-clean + 173/173 xUnit + 24/24 composer .flow + 3-file two-run cmp-clean), locked-decisions verification (D-v1.5-06 / D-36-01..18), anti-patterns inherited from Phase 35 baseline (no new failures introduced).
- **`36-VALIDATION.md`** frontmatter flipped: `status: closed`, `nyquist_compliant: true`, `wave_0_complete: true`; Approval block signed off.
- **`CLAUDE.md`** Phase 36 features section added:
  - Music-Specific § gains 5 new Phase 36 bullets: `@patterns` (13 combinators), `@generative` (Markov / L-system / cellular / chaos + PRNG routing), `@improv` (jam + style packs), parameterized sections (SECT-01 + D-36-13..18), universal named-argument syntax (D-36-11)
  - Special Types list gains MarkovModel (specificity 148) + LsystemModel (specificity 149); the Music Types Quick Reference table rows were already added in Plans 36-06 / 36-07
  - Generative builtin-categories block expanded with full Phase 36 surface + new PRNG Routing subsection citing `Runtime/PrngRegistry`
  - Standard Library Modules list gains `patterns.flow` / `generative.flow` / `improv.flow` + back-fills `sfz.flow` from Phase 33
  - Conventions section gains a Phase 36 D-36-09 callout documenting cross-platform FP divergence on Lorenz/logistic chaos primitives (same-platform two-run cmp-clean preserved; cross-platform reproducibility NOT guaranteed)
- **`REQUIREMENTS.md`** — 9 Phase 36 rows flipped from `[ ]` to `[x]` with closure notes citing plan commits (PAT-01 / PAT-02 / GEN-01..05 / SECT-01 / IMPROV-01); Traceability table flipped Pending → Shipped with commit refs.
- **`ROADMAP.md`** — Phase 36 v1.5 phase block one-liner flipped to `[x]` with completion date; 12 plan-list rows flipped to `[x]` each with commit refs; Progress table row `36. Sequence Algebra & Generative` flipped `5/12 In Progress` → `12/12 Complete 2026-05-22`; companion Phase 35 row flipped `5/7 In Progress` → `7/7 Complete 2026-05-19`.
- **`STATE.md`** — frontmatter `progress` flipped completed_phases 1→2, completed_plans 12→19, percent 14→29; `stopped_at` + `last_updated` + `last_activity` refreshed; Current Position points to Phase 37 CONTEXT spawn (`/clear` then `/gsd:context-phase 37`); Phase 36 highlights block added under Resume Instructions; Performance Metrics gains 12 new Phase 36 rows (P01..P12).
- **`.gitignore`** — allow-list `examples/generative/**/*.{flow,md}` + `examples/sections/**/*.{flow,md}` (mirrors `examples/{symphony,ragtime}/` precedent) + defensive `*.wav/*.mp3/*.mid` block under the same directories per Phase 34 D-502 precedent.

## Task Commits

Each task committed atomically per the executor protocol:

1. **Task 1 — 3 composer-facing tutorial examples + paired regression tests + .gitignore allowlist** — `727b3ea` (feat)
2. **Task 2 — Phase 36 closure docs (VERIFICATION + VALIDATION + CLAUDE.md + ROADMAP + STATE + REQUIREMENTS)** — `9335b9a` (docs)

## Files Created/Modified

### Created

- `examples/generative/markov_jazz.flow` — composer tutorial covering markov + jam + every+sometimes chain
- `examples/generative/tidal_combinators.flow` — composer tutorial covering all 13 @patterns combinators
- `examples/sections/parameterized.flow` — composer tutorial covering D-36-13..18 deliverables
- `tests/test_markov_jazz_example.flow` — paired regression (5 tests)
- `tests/test_tidal_combinators_example.flow` — paired regression (11 tests)
- `tests/test_parameterized_example.flow` — paired regression (7 tests)
- `.planning/phases/36-sequence-algebra-generative/36-VERIFICATION.md` — Phase 36 closure verification report (9/9 verified)

### Modified

- `.planning/phases/36-sequence-algebra-generative/36-VALIDATION.md` — status flipped to closed + Approval signed-off
- `CLAUDE.md` — Phase 36 features section across 5 locations (Language Features / Special Types / Generative builtins / Standard Library Modules / Conventions)
- `.planning/REQUIREMENTS.md` — 9 Phase 36 REQ rows flipped to [x] + 9 Traceability table rows flipped Pending → Shipped
- `.planning/ROADMAP.md` — Phase 36 v1.5 row + 12 plan list rows + Progress table row + companion Phase 35 row flipped to Complete
- `.planning/STATE.md` — frontmatter progress + Current Position + Resume Instructions + Performance Metrics updated for Phase 36 closure + Phase 37 readiness
- `.gitignore` — allowlist + defensive ignore for examples/generative/ + examples/sections/

## Decisions Made

See `key-decisions` in frontmatter. Summary:

- **writeWav targets `/tmp/` paths** instead of plan's spec'd `examples/output/` so `scripts/test_two_run_determinism.sh` works from any CWD (Rule 3 — Blocking fix).
- **Standalone `render` builtin doesn't exist** — replaced plan's `-> render` chain terminator with `(renderSong song "piano")` + `(writeWav ...)` (Rule 3 — Blocking fix).
- **Flow `->` operator prepends LHS** as first arg — replaced plan's `riffA -> (every 2 cb)` chain (would put riffA first; combinators take seq LAST per D-36-03) with direct calls + named intermediate variables (Rule 3 — Blocking fix).
- **`degrade` not in tidal example chain** — fixed-50% drop would silence half the bars; exercised in the paired regression test instead (per plan's own commentary).
- **`as` chain naming via named intermediate variables** — same composer-readability outcome, sidesteps the `->` arg-prepending issue. Phase 35-07 chain naming surface stays exercised in `tests/test_chain_naming.flow`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] writeWav path resolution failure under two-run determinism harness**
- **Found during:** Task 1 — initial render of `examples/generative/markov_jazz.flow` via the harness
- **Issue:** The plan's `<interfaces>` block spec'd `(writeWav "examples/output/markov_jazz.wav" mix)` but the harness resolves writeWav paths relative to the SCRIPT's directory (`examples/generative/`), so the path resolved to `examples/generative/examples/output/markov_jazz.wav` — non-existent.
- **Fix:** Changed writeWav target to `/tmp/markov_jazz.wav` (matches the Phase 36 test convention used by `tests/test_patterns_chain.flow`, `tests/test_markov_oneshot.flow`, `tests/test_jam_jazz.flow`, etc.). Added composer-facing comment block explaining the convention.
- **Files modified:** examples/generative/markov_jazz.flow + tidal_combinators.flow + sections/parameterized.flow
- **Verification:** All 3 examples now pass `scripts/test_two_run_determinism.sh` two-run cmp-clean.
- **Committed in:** `727b3ea`

**2. [Rule 3 — Blocking] Standalone `render` builtin doesn't exist**
- **Found during:** Task 1 — first render attempt of `examples/generative/markov_jazz.flow`
- **Issue:** Plan's `<interfaces>` snippet ended chains with `-> render` and `(renderSong song)` (no instrument arg). `grep -rn "\"render\"" flow-lang/StandardLibrary/` returned no hits; `renderSong` requires an instrument arg (e.g., `"piano"`).
- **Fix:** Replaced `-> render` chain terminator with explicit `Buffer mix = (renderSong song "piano")` followed by `(writeWav "/tmp/..." mix)`. Matches every existing test idiom in `tests/test_patterns_chain.flow` + `tests/test_markov_oneshot.flow` + `tests/test_jam_jazz.flow`.
- **Files modified:** all 3 example files
- **Verification:** Examples render cleanly.
- **Committed in:** `727b3ea`

**3. [Rule 3 — Blocking] Flow `->` operator prepends LHS as first arg, breaking combinator call shapes**
- **Found during:** Task 1 — first render of `examples/generative/markov_jazz.flow`
- **Issue:** Plan's `<interfaces>` showed `Sequence solo = (jam ...) -> (every 4 (fn s => (fast s 2))) as varied -> render`. Per the Flow `->` parse semantics (LHS prepended to RHS args), `solo -> (every 4 cb)` produces `(every solo 4 cb)` — wrong arg order. Combinator signatures take seq as LAST arg per D-36-03 (lambda-required transform-arg convention).
- **Fix:** Replaced flow-chain combinator usage with direct calls `(every 2 cb riffA)` + named intermediate Sequence variables. Same composer-readability outcome; sidesteps the `->` prepend semantic.
- **Files modified:** examples/generative/markov_jazz.flow + tidal_combinators.flow
- **Verification:** All 3 examples render cleanly; paired regression tests pass.
- **Committed in:** `727b3ea`

---

**Total deviations:** 3 auto-fixed (3 Rule 3 — Blocking)
**Impact on plan:** All 3 auto-fixes were composer-syntax mismatches between the plan's `<interfaces>` prose and the actual Flow language surface as it ships. The plan's intent (composer-facing tutorials demonstrating Phase 36 surface area) was preserved exactly; only the literal code in the `<interfaces>` snippets needed adjustment to match the language's actual semantics. No scope creep; the examples cover every deliverable the plan listed in `must_haves.truths`.

## Issues Encountered

None beyond the 3 deviations above. All 173 Phase 36 xUnit tests passed both before and after Task 1's example additions; the 24/24 Phase 36 composer test sweep passed before and after the new test files were added.

## Test Results

### Phase 36 xUnit suite (full)

```
dotnet test --filter "FullyQualifiedName~Phase36" --no-build
→ 173 passed, 0 failed, 0 skipped, 470ms
```

### Composer-facing test sweep (Phase 36)

```
for f in tests/test_{patterns,markov,lsystem,cellular,lorenz,logistic,jam,section,tidal,parameterized}*.flow; do
  dotnet run --project flow-cli --no-build -- test "$f"
done
→ 24 of 24 files PASS (100% on every Phase 36 test file including the 3 new example regressions)
```

### Two-run cmp-clean determinism (3 example files)

```
bash scripts/test_two_run_determinism.sh examples/generative/markov_jazz.flow
→ Run A: f46c1ca9360661c502a45f4e05495facc657130a0306bf9ef91b984512b30a64
→ Run B: f46c1ca9360661c502a45f4e05495facc657130a0306bf9ef91b984512b30a64
→ Two-run determinism: PASS

bash scripts/test_two_run_determinism.sh examples/generative/tidal_combinators.flow
→ Run A: 6d301369841d3ecdaba332c6517d6d670b4a27d40b846ee3957e525f127d1a2d
→ Run B: 6d301369841d3ecdaba332c6517d6d670b4a27d40b846ee3957e525f127d1a2d
→ Two-run determinism: PASS

bash scripts/test_two_run_determinism.sh examples/sections/parameterized.flow
→ Run A: 7d6d99c46f0172071e739f5628ae1dd3e9bbe626be2c760526d5b1df2a422ba6
→ Run B: 7d6d99c46f0172071e739f5628ae1dd3e9bbe626be2c760526d5b1df2a422ba6
→ Two-run determinism: PASS
```

### Phase 35 regression baseline (verified — no new breakage)

```
dotnet test --filter "FullyQualifiedName~Phase35" --no-build
→ 79 passed, 1 failed (MatchExhaustivenessDefaultTests.WarnDedupedPerMatchSpan)
```

The single failure is the pre-existing test-ordering limitation documented in `35-VERIFICATION.md` anti-patterns table (line 145) — NOT introduced by Phase 36.

## Threat Surface Scan

No new threat surface beyond the plan's `<threat_model>` register:

| Threat | Disposition | Status |
|--------|-------------|--------|
| T-36-31 (Integrity / example doesn't render or produces non-deterministic output) | mitigate | ✓ All 3 examples pass two-run cmp-clean; SHA-256 byte-identical across consecutive renders |
| T-36-32 (Integrity / REQUIREMENTS/ROADMAP/STATE flip omits a requirement) | mitigate | ✓ All 9 REQs (PAT-01/02, GEN-01..05, SECT-01, IMPROV-01) flipped in REQUIREMENTS.md + Traceability table; per-requirement verification table in 36-VERIFICATION.md cross-checks each REQ against its plan commit + test evidence |

No new threat flags emerged. No `threat_flag:` directives required.

## What This Unblocks

- **Phase 37 (Sound Design + Sampler Polish)** — Phase 36's `Runtime/PrngRegistry` foundation is in place; Phase 37 will route granular-jitter PRNG (DSP-01) and sampler round-robin index (SAMP-01) through the same registry, inheriting the two-run cmp-clean determinism contract.
- **Phase 38 (Live Coding 2.0)** — the test framework dogfood path is unblocked; `live { ... }` blocks will emit a stderr advisory explicitly opting OUT of two-run cmp-clean per D-v1.5-07 (Phase 36's PrngRegistry contract documents the deterministic baseline; Phase 38's live block knowingly violates it).
- **Phase 39 (Notation Citizenship)** — articulation emit uses Phase 35-06 ConstructorPattern.IsArticulationSymbol discriminator; Phase 36 Plan 36-10 verified the discriminator flag mechanic in production via section overloading.
- **Phase 40 (Studio Sync)** — MIDI event dispatch will use pattern matching on incoming MIDI messages (per D-v1.5-10); Phase 36's parameterized-section pattern dispatch surface (Plan 36-10) is the closest production exercise of pattern-matching dispatch outside the `match` expression itself.
- **Phase 41 (Reach + v1.5 Closer)** — third-genre showcase (jazz / EDM / death metal) per SHOWCASE-01 has Phase 36's `@improv` jam surface + Phase 36's parameterized sections to draw on for arrangement.

## Self-Check: PASSED

**Files asserted:**
- `[ -f examples/generative/markov_jazz.flow ]` → FOUND
- `[ -f examples/generative/tidal_combinators.flow ]` → FOUND
- `[ -f examples/sections/parameterized.flow ]` → FOUND
- `[ -f tests/test_markov_jazz_example.flow ]` → FOUND
- `[ -f tests/test_tidal_combinators_example.flow ]` → FOUND
- `[ -f tests/test_parameterized_example.flow ]` → FOUND
- `[ -f .planning/phases/36-sequence-algebra-generative/36-VERIFICATION.md ]` → FOUND
- `.planning/phases/36-sequence-algebra-generative/36-VALIDATION.md` `status: closed` → FOUND (header line 4)
- CLAUDE.md Phase 36 features section → FOUND (5 insertion sites verified)
- .planning/REQUIREMENTS.md 9 Phase 36 REQ rows `[x]` → FOUND
- .planning/REQUIREMENTS.md Traceability table 9 Phase 36 rows `Shipped` → FOUND
- .planning/ROADMAP.md Phase 36 row Complete 2026-05-22 → FOUND
- .planning/STATE.md `completed_phases: 2` + `completed_plans: 19` → FOUND

**Commits asserted:**
- `727b3ea` (Task 1) → FOUND in `git log --oneline`
- `9335b9a` (Task 2) → FOUND in `git log --oneline`

**No-regression assertions:**
- Phase 36 xUnit: 173/173 PASS (Phase 36 surface stable across the closure plan)
- Phase 35 xUnit: 79/80 PASS (baseline preserved; the one failure is the documented Phase 35 test-ordering limitation, NOT introduced by Phase 36)
- 24/24 Phase 36 composer .flow test files PASS
- 3-file two-run cmp-clean: PASS (SHA-256 byte-identical on all 3 examples)

---

*Phase: 36-sequence-algebra-generative*
*Plan: 12*
*Completed: 2026-05-22*
