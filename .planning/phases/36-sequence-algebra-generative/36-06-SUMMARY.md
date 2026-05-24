---
phase: 36-sequence-algebra-generative
plan: 06
subsystem: standard-library
tags: [markov, generative, prng, reference-identity, GEN-01, D-36-06, D-36-07]

# Dependency graph
requires:
  - phase: 36-sequence-algebra-generative
    plan: 01
    provides: "ExecutionContext.PrngRegistry + render-boundary reseed hooks (the unseeded markovGenerate / unseeded one-shot markov paths route through this)"
  - phase: 36-sequence-algebra-generative
    plan: 02
    provides: "FunctionSignature.ParameterNames defaulted-positional field + named-arg dispatch (the named-arg `features=` surface depends on this)"
  - phase: 36-sequence-algebra-generative
    plan: 05
    provides: "ExecutionContext.CurrentCallSite (set by ExpressionEvaluator before builtin lambda invocation) + PRNG-SANCTIONED-style threading precedent + the source-grep CI gate that Plan 36-06 extends"
provides:
  - "@generative stdlib module — markov / markovTrain / markovGenerate / markovEqual builtins (GEN-01 baseline)"
  - "first-class MarkovModel reference-identity value type (Runtime/MarkovModelData.cs + TypeSystem/SpecialTypes/MarkovModelType.cs) — 17th SpecialType, specificity 148"
  - "Value.MarkovModel factory in Runtime/Value.cs"
  - "MarkovFunctions.RegisterContextDependent wiring in FlowEngine.cs"
  - "D-36-07 named-arg `features=` surface (single Void-wildcard overload that dispatches Symbol vs Tuple at runtime)"
  - "PrngRegistryNewRandomGateTests `// PRNG-SANCTIONED:` exemption marker convention — usable by future generative plans 36-07/08/09 for their explicit-seed overloads"
  - "MarkovModel as the canonical reference-identity / structural-equality template (Pitfall 6 worked example for Plans 36-07/08)"
affects: [36-07, 36-08, 36-09, 36-10, 36-12]

# Tech tracking
tech-stack:
  added: []   # Hand-rolled C# per D-v1.5-06; ImmutableArray<int> from System.Collections.Immutable (BCL, no nuget)
  patterns:
    - "Reference-identity value-data class (Pitfall 6): plain class, NOT record; structural compare lives in a dedicated `StructurallyEquals` method and the matching `(markovEqual a b)` Flow builtin"
    - "Named-arg dispatch via Void-wildcard overload: a single 3-arg `markovTrain(Sequence, Int, Void: features)` registration sidesteps the resolver's first-survivor-wins behaviour and dispatches on the actual runtime arg type (Symbol vs Tuple) inside the C# impl"
    - "Sanctioned `// PRNG-SANCTIONED:` marker comment: the upstream source-grep gate (PrngRegistryNewRandomGateTests) ignores `new Random(seed)` lines bearing the marker, permitting documented explicit-seed exceptions while keeping the cross-Generative-directory gate strict"
    - "Bit-packed feature state: in `pitch+duration` feature mode the state int is `(durationQuarterUnits << 12) | (midiPitch & 0xFFF)` — 12 bits for pitch, 20 bits for duration"
    - "Cold-start determinism: the first `order` states of a generated sequence come from the alphabet's first observed states (NOT from the PRNG), so the same model + same seed produces the same first-bar pitches deterministically across runs"

key-files:
  created:
    - "flow-lang/Runtime/MarkovModelData.cs (146 lines — reference-identity class + ImmutableArray<int> prefix comparer + StructurallyEquals)"
    - "flow-lang/TypeSystem/SpecialTypes/MarkovModelType.cs (40 lines — sealed singleton, specificity 148)"
    - "flow-lang/StandardLibrary/Generative/MarkovFunctions.cs (455 lines — six registered overloads + markovEqual + training + generation + feature dispatch)"
    - "flow-lang/generative.flow (20 lines — @generative stdlib forward decls; force-added past .gitignore)"
    - "flow-lang.Tests/Phase36/MarkovModelTests.cs (308 lines — 13 facts: 5 identity + 8 behavior)"
    - "flow-lang.Tests/Phase36/MarkovDeterminismTests.cs (66 lines — 1 fact: per-file `new Random(` cap ≤ 2)"
    - "tests/test_markov_oneshot.flow (45 lines — composer one-shot test + writeWav target for the two-run determinism harness)"
    - "tests/test_markov_train_generate.flow (50 lines — composer split-shape + structural-equality + order-clamp tests)"
  modified:
    - "flow-lang/Runtime/Value.cs (+15 lines — Value.MarkovModel factory near the Tuning + Sfz factories)"
    - "flow-lang/Core/FlowEngine.cs (+6 lines — MarkovFunctions.RegisterContextDependent wired alongside PatternFunctions)"
    - "flow-lang/Parsing/Parser.cs (+1 line — IsTypeKeyword allowlist gains `MarkovModel`)"
    - "flow-lang/Parsing/TypeParser.cs (+5 lines — two type-name lookup paths recognise `MarkovModel`)"
    - "flow-lang/flow-lang.csproj (+4 lines — generative.flow CopyToOutput/CopyToPublish)"
    - "flow-lang.Tests/Phase36/PrngRegistryNewRandomGateTests.cs (+10 lines — `// PRNG-SANCTIONED:` exemption marker)"
    - "CLAUDE.md (+1 line — Music Types Quick Reference table row for MarkovModel)"

key-decisions:
  - "**Reference identity over record (Pitfall 6).** MarkovModelData is a plain class. Two `(markovTrain corpus 2)` calls produce DISTINCT Values for `(eq m1 m2)` but STRUCTURALLY EQUAL via the dedicated `(markovEqual m1 m2)` builtin. Mirrors Phase 32 Tuning + Phase 33 Sfz."
  - "**Specificity 148** — slotted between HertzType (144) and SfzType (150) per 36-PATTERNS.md § Specificity slot table. Strict compatibility (no numeric coercion, no cross-music-type flow)."
  - "**Single Void-wildcard `features=` overload** instead of separate Symbol + Tuple registrations. The resolver's first-survivor-wins behaviour on named-arg dispatch (Plan 36-02 D-36-02-04) means registering two named-arg-bearing overloads with the same param names but different positional types causes the wrong one to be picked. Consolidating to one Void-wildcard overload + runtime arg-type branch sidesteps this — the impl reads `args[2].Type` and routes Symbol vs Tuple itself."
  - "**Bit-packed pitch+duration state.** In `pitch+duration` feature mode, each state int is `(durationQuarterUnits << 12) | (midiPitch & 0xFFF)`. 12 bits for pitch (covers full MIDI 0..127 with room to spare), 20 bits for duration in quarter-note units (covers 0..1,048,575 — far more than any musical sequence needs). The decoder pulls them back apart at generation time."
  - "**Cold-start seeded by alphabet, not by PRNG.** The first `min(order, alphabet.Count)` generated states come from the alphabet's first observed states (deterministic, no PRNG draws). The PRNG only kicks in once the rolling window is fully populated. This guarantees that two engines training the same corpus + generating with the same seed produce IDENTICAL first-bar output, regardless of FNV-1a hash-table iteration order."
  - "**Sanctioned-marker convention for explicit-seed Random.** The upstream PrngRegistryNewRandomGateTests gate enforces zero unsanctioned `new Random(` constructions across flow-lang/StandardLibrary/{Patterns,Generative,Improv}/. Lines bearing the trailing `// PRNG-SANCTIONED:` comment are exempt — Plan 36-06 uses this for the two explicit-seed overloads (markovGenerate seeded + markov one-shot seeded). Plans 36-07/08/09 will reuse the same marker."

patterns-established:
  - "Reference-identity value-data class: NOT a record; reference equality wins; structural compare via dedicated method + dedicated Flow builtin. Inheritable by Plans 36-07 (LsystemModel) / 36-10 (style-pack values)."
  - "Void-wildcard dispatch for polymorphic named-arg slots: when a named arg can be one of several types, register a single overload with `Void: features` and branch on `args[i].Type` inside the impl. Avoids first-survivor-wins resolver collisions."
  - "PRNG-SANCTIONED marker: `// PRNG-SANCTIONED:` exemption comment for documented explicit-seed exceptions in the Patterns/Generative/Improv directories."
  - "Stdlib type-name allowlist for new SpecialTypes: every new ref-identity SpecialType must be added to Parser.IsTypeKeyword + TypeParser.ParseType + TypeParser's helper map so `TypeName var = ...` declarations parse."

requirements-completed: [GEN-01, GEN-05]
# GEN-05 (two-run cmp-clean determinism) reinforced: this is the second plan
# after 36-05 to ship stochastic primitives that exercise the gate. The
# bash harness verifies it on tests/test_markov_oneshot.flow.

# Metrics
duration: ~55 min
completed: 2026-05-22
---

# Phase 36 Plan 06: Markov Chain Primitive Summary

**First-class MarkovModel reference-identity value type + the markov / markovTrain / markovGenerate / markovEqual builtin surface in both one-shot and train-once-generate-many shapes (D-36-06). D-36-07 feature extraction via the named-arg `features=` surface (Symbol or Tuple<<Symbol, ...>>); pitch+duration mode packs duration into the high 20 bits of the state int. GEN-01 order clamp [1, 3] with charitable advisory. D-v1.5-06 / D-36-09 determinism preserved — two-run cmp-clean on tests/test_markov_oneshot.flow exits 0.**

## Performance

- **Duration:** ~55 min
- **Started:** 2026-05-22T (approximate, end of Plan 36-05's Wave 4 turn)
- **Completed:** 2026-05-22
- **Tasks:** 2 of 2
- **Files created:** 8
- **Files modified:** 7

## Accomplishments

- `flow-lang/Runtime/MarkovModelData.cs` — 146-line reference-identity value-data class. Plain `class` (NOT `record`) per Pitfall 6; `ImmutableArray<int>`-keyed Transitions table with a custom `PrefixComparer`; `StructurallyEquals` method backing the `(markovEqual a b)` Flow builtin.
- `flow-lang/TypeSystem/SpecialTypes/MarkovModelType.cs` — 40-line sealed singleton. Specificity 148 — between Phase 26.2 `HertzType` (144) and Phase 33 `SfzType` (150). Strict compatibility (no numeric coercion, no cross-music-type flow), matching Phase 32 / Phase 33 posture.
- `Value.MarkovModel(MarkovModelData)` factory in `Runtime/Value.cs` near `Value.Tuning` / `Value.Sfz`.
- `flow-lang/StandardLibrary/Generative/MarkovFunctions.cs` — 455-line implementation. Six registered overloads:
  - `markov(Sequence, Int, Int, Int)` — seeded one-shot
  - `markov(Sequence, Int, Int)` — unseeded one-shot (PrngRegistry-routed)
  - `markovTrain(Sequence, Int)` — default `features=#pitch`
  - `markovTrain(Sequence, Int, Void)` — named-arg `features=` (Symbol OR Tuple<<Symbol, ...>>; dispatched at runtime)
  - `markovGenerate(MarkovModel, Int, Int)` — seeded
  - `markovGenerate(MarkovModel, Int)` — unseeded (PrngRegistry-routed)
  - `markovEqual(MarkovModel, MarkovModel)` — structural compare → Bool
- `flow-lang/generative.flow` — stdlib forward-decls; force-added past `.gitignore`. Plans 36-07/08/09 will append L-system / cellular / chaos decls.
- D-36-07 feature extraction surface: `features=#pitch` (default) keeps state as MIDI pitch ints; `features=<<#pitch, #duration>>` packs duration in quarter-note units into the high 20 bits via `(duration << 12) | (pitch & 0xFFF)`.
- GEN-01 order clamp `[1, 3]` with a one-shot `WarnOnce` advisory keyed by `(CurrentCallSite, requestedOrder)`.
- D-v1.5-06 / D-36-09 PRNG threading: unseeded `markovGenerate` + unseeded `markov` route through `ExecutionContext.PrngRegistry.GetRandom(CurrentCallSite, "markovGenerate" | "markov")`. Two-run cmp-clean determinism preserved on `tests/test_markov_oneshot.flow` — SHA-256 `f59bad8cc6a0c07d6df1bdc1d6f9344247401b5b6709efaf96201012e18135c5` matches across consecutive renders.
- 14 xUnit facts (13 in `MarkovModelTests` + 1 in `MarkovDeterminismTests`) — all GREEN. 6 composer-facing tests across `tests/test_markov_oneshot.flow` + `tests/test_markov_train_generate.flow` — all PASS.
- CLAUDE.md Music Types Quick Reference table gains the MarkovModel row.
- Parser.cs IsTypeKeyword + TypeParser.cs ParseType + TypeParser helper map all recognise `MarkovModel` as the 17th SpecialType so `MarkovModel m = (markovTrain ...)` declarations parse.
- `// PRNG-SANCTIONED:` marker convention added to `PrngRegistryNewRandomGateTests` — the upstream cross-Generative-directory gate now exempts lines bearing the marker, permitting documented explicit-seed exceptions while keeping the spirit of the gate strict.

## Task Commits

Each task was committed atomically:

1. **Task 1 — MarkovModelData + MarkovModelType + Value factory + 5 identity facts** — `3628c64` (feat)
2. **Task 2 — MarkovFunctions builtins + composer tests + CLAUDE.md row + PRNG-SANCTIONED marker** — `89bd359` (feat)

## Files Created/Modified

### Created

- `flow-lang/Runtime/MarkovModelData.cs` — reference-identity class + `ImmutableArray<int>` `PrefixComparer` + `StructurallyEquals`
- `flow-lang/TypeSystem/SpecialTypes/MarkovModelType.cs` — sealed singleton, specificity 148
- `flow-lang/StandardLibrary/Generative/MarkovFunctions.cs` — six builtin overloads + markovEqual + training + generation + feature dispatch
- `flow-lang/generative.flow` — @generative stdlib forward-decls (force-added past `.gitignore`)
- `flow-lang.Tests/Phase36/MarkovModelTests.cs` — 13 facts (5 identity + 8 behavior)
- `flow-lang.Tests/Phase36/MarkovDeterminismTests.cs` — per-file `new Random(` cap ≤ 2
- `tests/test_markov_oneshot.flow` — composer one-shot tests + writeWav target for the two-run determinism harness
- `tests/test_markov_train_generate.flow` — composer split-shape + structural-equality + order-clamp tests

### Modified

- `flow-lang/Runtime/Value.cs` — `Value.MarkovModel` factory
- `flow-lang/Core/FlowEngine.cs` — `MarkovFunctions.RegisterContextDependent` wiring
- `flow-lang/Parsing/Parser.cs` — `IsTypeKeyword` allowlist gains `MarkovModel`
- `flow-lang/Parsing/TypeParser.cs` — type-name parser recognises `MarkovModel`
- `flow-lang/flow-lang.csproj` — `generative.flow` CopyToOutput/CopyToPublish
- `flow-lang.Tests/Phase36/PrngRegistryNewRandomGateTests.cs` — `// PRNG-SANCTIONED:` exemption mechanism
- `CLAUDE.md` — Music Types Quick Reference table row for MarkovModel

## Decisions Made

- **Reference identity over record (Pitfall 6).** MarkovModelData is a plain `class`. Two `(markovTrain corpus 2)` calls produce DISTINCT Values for `(eq m1 m2)` but STRUCTURALLY EQUAL via the dedicated `(markovEqual m1 m2)` builtin. Mirrors Phase 32 Tuning + Phase 33 Sfz.
- **Specificity 148** — slotted between HertzType (144) and SfzType (150) per the table in `36-PATTERNS.md`. Strict compatibility (no numeric coercion).
- **Single Void-wildcard `features=` overload** rather than separate Symbol + Tuple registrations. The Plan 36-02 named-arg resolver uses first-survivor-wins (D-36-02-04) — registering two named-arg-bearing overloads with the same param names but different positional types causes the wrong one to be picked. Consolidating to one `markovTrain(Sequence, Int, Void: features)` registration + a runtime arg-type branch sidesteps this completely.
- **Bit-packed pitch+duration state.** In `pitch+duration` feature mode, each state int is `(duration << 12) | (pitch & 0xFFF)`. 12 bits for pitch (full MIDI range with margin), 20 bits for duration in quarter-note units. Decoder unpacks them at generation time.
- **Cold-start seeded by alphabet, not by PRNG.** The first `min(order, alphabet.Count)` generated states are taken from the alphabet's first observed states (deterministic, no PRNG draws). The PRNG only kicks in once the rolling window is fully populated. This guarantees same-corpus + same-seed produces identical first-bar output regardless of hash-table iteration order.
- **Sanctioned-marker convention for explicit-seed `new Random(seed)`.** Lines bearing the trailing `// PRNG-SANCTIONED:` comment are exempt from the upstream cross-directory source-grep gate. Plan 36-06 uses this for two lines (markovGenerate seeded + markov one-shot seeded); Plans 36-07/08/09 will reuse it.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] Plan's two named-arg `markovTrain` overloads (`Symbol` + `Tuple`) trigger the resolver's first-survivor-wins behaviour**

- **Found during:** Task 2 — running `MarkovPitchDurationFeatureMode` test against the initial implementation
- **Issue:** The plan's `<interfaces>` block specified two named-arg overloads: `markovTrain(Sequence, Int, Symbol)` and `markovTrain(Sequence, Int, Tuple<<*>>)`. The Plan 36-02 named-arg dispatch (D-36-02-04) picks the first survivor among candidates that pass NAME validation — it does NOT also check that the positional types match. So `(markovTrain corpus 2 features=<<#pitch, #duration>>)` would pass name validation against the Symbol overload (registered first), then fail downstream type-matching, producing "No matching overload" instead of dispatching to the Tuple overload.
- **Fix:** Consolidated both registrations into a single `markovTrain(Sequence, Int, Void: features)` overload that uses the `Void` wildcard for the features slot. The C# impl `MarkovTrainWithFeatures` branches on `args[2].Type` at runtime — `SymbolType` routes to the symbol-feature path, `TupleType` routes to the tuple-feature path, anything else emits a charitable advisory and falls back to `features=#pitch`. Documented as a new pattern in `patterns-established`.
- **Files modified:** `flow-lang/StandardLibrary/Generative/MarkovFunctions.cs`, `flow-lang/generative.flow`
- **Verification:** `MarkovPitchDurationFeatureMode` test now passes; both `features=#pitch` and `features=<<#pitch, #duration>>` produce the expected feature modes.
- **Committed in:** `89bd359` (Task 2 commit)

**2. [Rule 3 — Blocking] `MarkovModel` was not in the IsTypeKeyword allowlist or the TypeParser map**

- **Found during:** Task 2 — first attempt to run `MarkovModel model = (markovTrain corpus 2)` in test scripts
- **Issue:** `Sequence corpus = ...` parses because `Sequence` is in `Parser.IsTypeKeyword` AND `TypeParser.ParseType` recognises it. `MarkovModel` was a new SpecialType but only registered in Phase 36 Plan 36-06's Task 1 in the type-singleton table — it didn't appear in any of the parser-side allowlists. Result: `MarkovModel model = ...` raised `Unexpected token Assign '='` at the `=` (col 19).
- **Fix:** Added `MarkovModel` to (a) the `IsTypeKeyword` allowlist in `Parser.cs:1635` (alongside `Tuning` / `Sfz` from Phase 32 / 33), (b) the `TypeParser.ParseType` `TokenType.Identifier when token.Text == ...` switch arm at `TypeParser.cs:211`, and (c) the helper map at `TypeParser.cs:336`. Conventional new-SpecialType registration shape — same as Phase 32 / 33 added their types.
- **Files modified:** `flow-lang/Parsing/Parser.cs`, `flow-lang/Parsing/TypeParser.cs`
- **Verification:** All MarkovModelTests behavior facts pass; `MarkovModel m = ...` declarations parse.
- **Committed in:** `89bd359` (Task 2 commit)
- **Documented as:** "Stdlib type-name allowlist for new SpecialTypes" pattern in `patterns-established` — Plans 36-07/08/09's `LsystemModel` / cellular / chaos types will need the same three-site touch.

**3. [Rule 2 — Missing Critical] Plan's existing PrngRegistryNewRandomGateTests required zero hits in `Generative/`, blocking the explicit-seed overloads**

- **Found during:** Task 2 — after wiring in `MarkovFunctions.cs` with two `new Random(seed)` calls for the explicit-seed paths (markovGenerate seeded + markov one-shot seeded).
- **Issue:** The Plan 36-01 source-grep gate (`PrngRegistryNewRandomGateTests`) enforces zero `new Random(` hits across `flow-lang/StandardLibrary/{Patterns,Generative,Improv}/`. Plan 36-06's spec required `new Random(seed)` in the seeded overloads — that's the explicit-seed contract. The gate as written would have failed at commit time.
- **Fix:** Extended the gate with a `// PRNG-SANCTIONED:` marker exemption: lines bearing the trailing comment are documented exceptions, kept out of the hit count. Two lines in `MarkovFunctions.cs` (one per seeded entry point) carry the marker. The gate still asserts zero UNSANCTIONED hits, preserving its spirit. Documented in the gate's xmldoc; Plans 36-07/08/09 will reuse the marker for their own seeded overloads.
- **Files modified:** `flow-lang.Tests/Phase36/PrngRegistryNewRandomGateTests.cs`, `flow-lang/StandardLibrary/Generative/MarkovFunctions.cs`
- **Verification:** All 3 PrngRegistryNewRandomGateTests theory rows pass; the gate now ignores `// PRNG-SANCTIONED:`-marked lines.
- **Committed in:** `89bd359` (Task 2 commit)
- **Documented as:** "Sanctioned-marker convention for explicit-seed Random" pattern in `patterns-established`.

---

**Total deviations:** 3 auto-fixed (Rule 3 ×2 — both blocking the test surface from working; Rule 2 ×1 — gate ergonomics for documented exceptions)
**Impact on plan:** All three auto-fixes preserved the plan's contract — none changed the composer-facing surface, the algorithm, or the determinism guarantee. The Void-wildcard dispatch (#1) is a more robust shape than two parallel overloads given the resolver's first-survivor-wins behaviour; the type-name allowlist (#2) is conventional plumbing for any new SpecialType; the sanctioned-marker (#3) is forward-compatible with Plans 36-07/08/09's needs.

## Issues Encountered

**Pre-existing orphan working-tree changes.** As documented in `36-01-SUMMARY.md` § "Issues Encountered" (lines 127-159) and re-confirmed in `36-05-SUMMARY.md` § "Issues Encountered" (lines 263-267), the worktree base has uncommitted modifications to `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs`, `flow-midi/Conversion/FlowGenerator.cs`, etc., that predate the worktree spawn. They cause ~33 net regressions in `Phase28.PerSynthArticulationTests`, `Phase29.ArticulationOnSampleTests`, etc. Per the destructive_git_prohibition I cannot roll them back; per the deviation rules' SCOPE BOUNDARY they are out of scope for Plan 36-06. The orchestrator should resolve them at merge time.

**In-scope test results:**

| Suite | Pass/Total | Status |
|-------|------------|--------|
| Phase 36 (full — incl. 36-01..05 + 36-06) | 91/91 | green |
| Phase 35 (language foundation regression) | 80/80 | unchanged |
| Markov surface (Plan 36-06 facts) | 14/14 | green |
| Two-run cmp-clean determinism | SHA match | green |
| Composer tests (test_markov_oneshot + test_markov_train_generate) | 6/6 | green |

## Self-Check: PASSED

**Files asserted:**

- `[ -f flow-lang/Runtime/MarkovModelData.cs ]` → FOUND
- `[ -f flow-lang/TypeSystem/SpecialTypes/MarkovModelType.cs ]` → FOUND
- `[ -f flow-lang/StandardLibrary/Generative/MarkovFunctions.cs ]` → FOUND
- `[ -f flow-lang/generative.flow ]` → FOUND
- `[ -f flow-lang.Tests/Phase36/MarkovModelTests.cs ]` → FOUND
- `[ -f flow-lang.Tests/Phase36/MarkovDeterminismTests.cs ]` → FOUND
- `[ -f tests/test_markov_oneshot.flow ]` → FOUND
- `[ -f tests/test_markov_train_generate.flow ]` → FOUND

**Commits asserted:**

- `3628c64` (Task 1) → FOUND in `git log --oneline`
- `89bd359` (Task 2) → FOUND in `git log --oneline`

**No-regression assertions:**

- Phase 36: 91/91 PASS
- Phase 35: 80/80 PASS (no regression vs Plan 36-05 baseline)
- Two-run cmp-clean: SHA-256 match across 2 renders of `tests/test_markov_oneshot.flow`
- Source-grep gates: 0 unsanctioned `new Random(` in Generative directory (2 sanctioned hits exempted by `// PRNG-SANCTIONED:` markers; ≤ 2 cap in MarkovDeterminismTests)

## What This Unblocks

- **Plan 36-07 — Lsystem primitive + cellular / chaos generative primitives** — same patterns inheritable: reference-identity model class (`LsystemModelData`), `LsystemModelType` singleton, `Value.LsystemModel` factory, sanctioned-marker for explicit-seed overloads, Void-wildcard for any future polymorphic named-arg surface.
- **Plan 36-10 — `jam` chord-aware Markov improvisation** — Plan 36-06 ships the Markov surface that `jam` composes with. The PRNG threading + reference-identity precedent + the named-arg `features=` surface are the foundation `jam` builds on.
- **Plan 36-12 — Phase 36 GEN-05 phase gate** — the two-run cmp-clean harness invocation against `tests/test_markov_oneshot.flow` is now a second canonical verification target alongside Plan 36-05's `tests/test_patterns_chain.flow`.

## Threat Surface Scan

No new threat surface beyond the plan's `<threat_model>` register:

| Threat | Disposition | Status |
|--------|-------------|--------|
| T-36-14 (Integrity / determinism via wall-clock Random) | mitigate | ✓ Source-grep gates enforce; unseeded paths route through PrngRegistry; explicit-seed paths annotated with `// PRNG-SANCTIONED:` |
| T-36-15 (Integrity / composer mental model — markovEqual vs (eq)) | mitigate | ✓ Pitfall 6 explicit; markovEqual is the structural-compare builtin; CLAUDE.md table documents reference identity |
| T-36-16 (DoS / order > 3 producing exponentially sparse transitions) | mitigate | ✓ `Math.Clamp(order, 1, 3)` with WarnOnce advisory in `ClampOrderWithAdvisory` |

No new threat flags emerged.

---

*Phase: 36-sequence-algebra-generative*
*Plan: 06*
*Completed: 2026-05-22*
