---
phase: 36-sequence-algebra-generative
plan: 07
subsystem: standard-library
tags: [lsystem, generative, lindenmayer, symbol, reference-identity, deterministic, GEN-02, D-36-06, D-36-08]

# Dependency graph
requires:
  - phase: 36-sequence-algebra-generative
    plan: 01
    provides: "PrngRegistryNewRandomGateTests source-grep gate (Plan 36-07 passes it trivially — zero `new Random(` in LsystemFunctions.cs)"
  - phase: 36-sequence-algebra-generative
    plan: 02
    provides: "FunctionSignature.ParameterNames defaulted-positional field (every Lsystem builtin registration uses it)"
  - phase: 36-sequence-algebra-generative
    plan: 06
    provides: "MarkovModelData / MarkovModelType template (the reference-identity precedent + Phase 32 Tuning + Phase 33 Sfz pattern); stdlib type-name allowlist convention (Parser.IsTypeKeyword + TypeParser ParseType + helper map); Generative/ subdirectory + InvokeCallback lambda dispatch shape"
provides:
  - "@generative stdlib extension — lsystem / lsystemModel / lsystemGenerate / lsystemToSequence / lsystemEqual builtins (GEN-02)"
  - "first-class LsystemModel reference-identity value type (Runtime/LsystemModelData.cs + TypeSystem/SpecialTypes/LsystemModelType.cs) — 18th SpecialType, specificity 149"
  - "Value.LsystemModel factory in Runtime/Value.cs"
  - "LsystemFunctions.RegisterContextDependent wiring in FlowEngine.cs"
  - "DoS guard pattern (iteration cap with WarnOnce advisory) — reusable by Plan 36-08 cellular automata and Plan 36-09 chaos primitives that need their own bounded-iteration safeguards"
affects: [36-08, 36-09, 36-12]

# Tech tracking
tech-stack:
  added: []   # Hand-rolled C# per D-v1.5-06; standard System.Collections.Generic dictionaries — no new BCL types beyond what Plan 36-06 already pulled in
  patterns:
    - "Reference-identity value-data class (Pitfall 6 — Plan 36-06 precedent): plain class NOT record; structural compare via dedicated StructurallyEquals method and matching `(lsystemEqual a b)` Flow builtin; reference-identity Value wrapper via the Value.LsystemModel factory"
    - "Pure deterministic rewriting (no PRNG): the Plan 36-01 source-grep gate (`PrngRegistryNewRandomGateTests`) passes trivially for LsystemFunctions.cs; zero `// PRNG-SANCTIONED:` markers needed. Distinguishes this plan from 36-05 (stochastic Pattern combinators) and 36-06 (stochastic Markov surface)"
    - "DoS-bounded iteration: explicit MaxIterations constant + ClampIterationsWithAdvisory helper emit one-shot WarnOnce when composer exceeds the cap. Pattern reusable by Plan 36-08's cellular automata (which has analogous exponential-growth risk in the grid-width × steps product) and Plan 36-09's chaos primitives"
    - "Dual-shape rule values (Tuple<<Symbol, ...>> ∪ Symbol[]): the NormalizeRules helper at LsystemFunctions.cs walks the dict value-by-value and accepts both shapes via the IReadOnlyList<Value> CLR storage — Tuples and Arrays back the same Data shape, so the C# branch is a no-op type check. Composer ergonomic: composers writing rules as `<<#A, #B>>` (tuple literal) AND `(list #A #B)` (array builtin) both Just Work"
    - "Lambda dispatch via InvokeCallback (DictFunctions/PatternFunctions shape): lsystemToSequence invokes the composer's per-symbol mapper, branching on FunctionOverload.IsInternal between Implementation-call and ExecuteUserFunctionWithCaptures. Phase 36's third independent copy of this same shape (after DictFunctions:41-46 and PatternFunctions:106-115); a future refactor could lift it into FlowLang.Runtime as a shared helper"

key-files:
  created:
    - "flow-lang/Runtime/LsystemModelData.cs (129 lines — reference-identity class + StructurallyEquals + cross-context ValuesEqual helper)"
    - "flow-lang/TypeSystem/SpecialTypes/LsystemModelType.cs (41 lines — sealed singleton, specificity 149)"
    - "flow-lang/StandardLibrary/Generative/LsystemFunctions.cs (340 lines — 5 registered builtins + NormalizeRules + ExpandAxiom + ClampIterationsWithAdvisory + InvokeCallback)"
    - "flow-lang.Tests/Phase36/LsystemModelTests.cs (304 lines — 12 facts: 5 identity + 7 behavior)"
    - "flow-lang.Tests/Phase36/LsystemDeterminismTests.cs (90 lines — 1 source-grep gate fact + 1 one-shot ≡ model+generate fact)"
    - "tests/test_lsystem_oneshot.flow (60 lines — 3 composer tests + renderable WAV target for the two-run determinism harness)"
    - "tests/test_lsystem_train_generate.flow (44 lines — 5 composer tests covering split shape + structural equality + iteration clamp)"
  modified:
    - "flow-lang/Runtime/Value.cs (+15 lines — Value.LsystemModel factory adjacent to Value.MarkovModel)"
    - "flow-lang/Core/FlowEngine.cs (+5 lines — LsystemFunctions.RegisterContextDependent wired alongside MarkovFunctions)"
    - "flow-lang/Parsing/Parser.cs (+1 line — IsTypeKeyword allowlist gains `LsystemModel`)"
    - "flow-lang/Parsing/TypeParser.cs (+4 lines — both type-name lookup paths recognise `LsystemModel`)"
    - "flow-lang/generative.flow (+11 lines — five new internal proc forward decls for the L-system surface)"
    - "CLAUDE.md (+1 line — Music Types Quick Reference table row for LsystemModel)"

key-decisions:
  - "**Rule values: Symbol[] over Tuple<<Symbol,...>> at the type-annotation layer.** Plan's `<interfaces>` block suggested `Dict<Symbol, Array[Symbol]>` OR `Tuple<<Symbol, ...>>` — both work at runtime since both back IReadOnlyList<Value>. At the composer-facing type annotation layer the cleaner choice is `Dict<Symbol, Symbol[]>` because heterogeneous tuple arities (`<<#A, #B>>` is 2-tuple, `<<#A>>` is 1-tuple, both common in L-system rules) would force the composer onto a wildcard `Dict<Symbol, Tuple>` annotation — which the TypeParser rejects (Tuple requires explicit `<<>>` arity). The `(list)` builtin produces a `Symbol[]` and accepts arbitrary arity uniformly. Documented in generative.flow's forward-decl comment that BOTH shapes work at runtime (the C# NormalizeRules walks IReadOnlyList<Value> regardless of source)."
  - "**Iteration cap: 20.** Per RESEARCH §Security Domain. Lindenmayer algae growth at iteration 20 is F(22) ≈ 17,711 symbols — well below any OOM threshold, well above any musical need. The plan's must-haves pinned this number; Plan 36-07 implements it as `MaxIterations` constant with a one-shot WarnOnce advisory at the call site. Composer asking for 100 gets a clamped result + a friendly heads-up rather than an error."
  - "**Terminal-symbol passthrough.** Canonical Lindenmayer semantics: a symbol not present as a rule key in the dict passes through unchanged on every iteration. This is the load-bearing semantic of L-systems and is what makes axioms like `#A → #A #X` produce `[#A, #X, #X, #X, ...]` over consecutive iterations (the `#X` never changes). Implemented in ExpandAxiom's else branch — `next.Add(sym)` when LookupRule returns null."
  - "**LsystemModelData.Iterations stored at train time.** The plan's `<interfaces>` block specified the model captures the iteration count at construction so two-model structural compare includes iteration intent. Plan 36-07 defaults this to 0 at `(lsystemModel axiom rules)` time (the composer hasn't committed to an iteration count yet); the count is overridden at generation time via `(lsystemGenerate model iterations)`. This is consistent with how Plan 36-06's MarkovModelData captures the Markov order at train time — Markov order also affects structural compare."
  - "**No PRNG, no PRNG-SANCTIONED marker.** L-system rewriting is purely deterministic. The Plan 36-01 source-grep gate (PrngRegistryNewRandomGateTests) passes trivially for LsystemFunctions.cs. The `// PRNG-SANCTIONED:` marker convention from Plan 36-06 is reserved for stochastic primitives' explicit-seed overloads; Plan 36-07's LsystemDeterminismTests.NoNewRandomInLsystemFunctions enforces a strict zero-hit cap (no sanctioned exceptions). If v1.6 adds stochastic rule overloading the gate xmldoc points to the Plan 36-06 marker pattern."
  - "**`(list)` over `<<>>` for rule values in tests.** Composer surface accepts BOTH but the test-script type-annotation layer requires uniformity. Heterogeneous tuple arities can't be assigned to a single `Dict<Symbol, Tuple<<Symbol, Symbol>>>` annotation — the second 1-tuple rule fails the type check. Using `(list #A #B)` and `(list #A)` produces uniform `Symbol[]` values that fit `Dict<Symbol, Symbol[]>` cleanly. This is a Test-Script Ergonomic decision; composer-facing usage can use whichever feels more natural (the C# NormalizeRules accepts both)."

requirements-completed: [GEN-02, GEN-05]
# GEN-02 (L-system primitive) — primary delivery.
# GEN-05 (two-run cmp-clean determinism) — reinforced via
# scripts/test_two_run_determinism.sh tests/test_lsystem_oneshot.flow exit 0.

# Metrics
duration: ~40 min
completed: 2026-05-22
---

# Phase 36 Plan 07: L-system Primitive Summary

**First-class LsystemModel reference-identity value type + the lsystem / lsystemModel / lsystemGenerate / lsystemToSequence / lsystemEqual builtin surface in both one-shot and train-once-generate-many shapes (D-36-06). D-36-08 Symbol alphabet pick (Phase 26.1) for type safety and tuple composability. T-36-17 DoS guard via 20-iteration cap with WarnOnce advisory. GEN-02 + GEN-05 — two-run cmp-clean on tests/test_lsystem_oneshot.flow exits 0 with SHA-256 509d3994... identical across runs.**

## Performance

- **Duration:** ~40 min
- **Started:** 2026-05-22T (immediately after Plan 36-06's merge)
- **Completed:** 2026-05-22
- **Tasks:** 2 of 2
- **Files created:** 7
- **Files modified:** 6

## Accomplishments

- `flow-lang/Runtime/LsystemModelData.cs` — 129-line reference-identity value-data class. Plain `class` (NOT `record`) per Pitfall 6; axiom + rules dict + iterations field; `StructurallyEquals` method backing the `(lsystemEqual a b)` Flow builtin; cross-context `ValuesEqual` helper that compares Symbol Values by reference-identity OR underlying-string equality (covers both interned single-context Symbols and cross-context test fixtures).
- `flow-lang/TypeSystem/SpecialTypes/LsystemModelType.cs` — 41-line sealed singleton. Specificity 149 — between Plan 36-06's `MarkovModelType` (148) and Phase 33's `SfzType` (150). Strict compatibility (no numeric coercion, no cross-music-type flow), matching Phase 32 / Phase 33 / Phase 36-06 posture.
- `Value.LsystemModel(LsystemModelData)` factory in `Runtime/Value.cs` near `Value.MarkovModel`.
- `flow-lang/StandardLibrary/Generative/LsystemFunctions.cs` — 340-line implementation. Five registered builtins:
  - `lsystem(Symbol, Dict<*>, Int) → Symbol[]` — one-shot (axiom + rules + iterations)
  - `lsystemModel(Symbol, Dict<*>) → LsystemModel` — build once, reuse
  - `lsystemGenerate(LsystemModel, Int) → Symbol[]` — generate from model with iteration override
  - `lsystemToSequence(Symbol[], Function) → Sequence` — composer mapper invocation per symbol
  - `lsystemEqual(LsystemModel, LsystemModel) → Bool` — structural compare
- T-36-17 DoS guard: iteration count clamped to `[0, 20]` with one-shot `WarnOnce` advisory at the call site.
- Charitable interpretation per D-v1.5-05: non-Symbol rule keys/values dropped with advisory; bad mapper return types skipped with advisory; never throws on degenerate input.
- Lambda dispatch via the third independent copy of the `InvokeCallback(context, FunctionOverload, args)` shape (after `DictFunctions:41-46` and `PatternFunctions:106-115`) — the implicit pattern across the Phase 36 stdlib.
- 14 xUnit facts: 5 identity + 7 behavior in `LsystemModelTests` + 2 in `LsystemDeterminismTests` — all GREEN.
- 8 composer-facing tests across `tests/test_lsystem_oneshot.flow` + `tests/test_lsystem_train_generate.flow` — all PASS via `flow test`.
- CLAUDE.md Music Types Quick Reference table gains the LsystemModel row documenting reference identity, the structural-equality builtin, and the iteration cap.
- Parser.cs IsTypeKeyword + TypeParser.cs ParseType + helper map all recognise `LsystemModel` as the 18th SpecialType so `LsystemModel m = ...` declarations parse.
- `bash scripts/test_two_run_determinism.sh tests/test_lsystem_oneshot.flow` PASSES — SHA-256 `509d3994ad80172dfb2eb63ae217fc3253682ad434698829b8bdde9bd7c6fb4b` identical across consecutive renders.

## Task Commits

Each task was committed atomically:

1. **Task 1 — LsystemModelData + LsystemModelType + Value factory + 5 identity facts** — `28091f1` (feat)
2. **Task 2 — LsystemFunctions builtins + composer tests + CLAUDE.md row + iteration-cap DoS guard** — `e4b93ba` (feat)

## Files Created/Modified

### Created

- `flow-lang/Runtime/LsystemModelData.cs` — reference-identity class + `StructurallyEquals` + cross-context `ValuesEqual`
- `flow-lang/TypeSystem/SpecialTypes/LsystemModelType.cs` — sealed singleton, specificity 149
- `flow-lang/StandardLibrary/Generative/LsystemFunctions.cs` — five builtin registrations + iteration cap + charitable advisories
- `flow-lang.Tests/Phase36/LsystemModelTests.cs` — 12 facts (5 identity + 7 behavior)
- `flow-lang.Tests/Phase36/LsystemDeterminismTests.cs` — source-grep gate + one-shot/model-generate equivalence
- `tests/test_lsystem_oneshot.flow` — composer one-shot tests + writeWav target for the two-run determinism harness
- `tests/test_lsystem_train_generate.flow` — composer split-shape + structural-equality + iteration-clamp tests

### Modified

- `flow-lang/Runtime/Value.cs` — `Value.LsystemModel` factory
- `flow-lang/Core/FlowEngine.cs` — `LsystemFunctions.RegisterContextDependent` wiring
- `flow-lang/Parsing/Parser.cs` — `IsTypeKeyword` allowlist gains `LsystemModel`
- `flow-lang/Parsing/TypeParser.cs` — type-name parser recognises `LsystemModel`
- `flow-lang/generative.flow` — five `internal proc` forward decls for the L-system surface
- `CLAUDE.md` — Music Types Quick Reference table row for LsystemModel

## Decisions Made

- **Rule values: Symbol[] over Tuple<<Symbol,...>> at the type-annotation layer.** The plan's `<interfaces>` block suggested either rule-value shape; runtime accepts both (NormalizeRules walks IReadOnlyList<Value>). The clean choice at the composer-facing type annotation is `Symbol[]` because heterogeneous tuple arities can't share a single type annotation — `Dict<Symbol, Symbol[]>` accepts mixed-arity rules via `(list #A #B)` and `(list #A)` uniformly.
- **Iteration cap: 20.** Per RESEARCH §Security Domain. Lindenmayer algae at iteration 20 produces F(22) ≈ 17,711 symbols — well below any OOM threshold, well above any musical need. Implemented as `MaxIterations` constant + `ClampIterationsWithAdvisory` helper.
- **Terminal-symbol passthrough.** Canonical Lindenmayer semantics: a symbol with no rule entry passes through unchanged on every iteration. This is the load-bearing semantic of L-systems.
- **LsystemModelData.Iterations defaults to 0 at train time.** The model captures iteration intent for structural compare; composer overrides at generation time. Consistent with how Plan 36-06's MarkovModelData captures the Markov order at train time.
- **No PRNG, no `// PRNG-SANCTIONED:` marker.** L-system rewriting is purely deterministic. The source-grep gate (`LsystemDeterminismTests.NoNewRandomInLsystemFunctions`) enforces a strict zero-hit cap.
- **`(list)` over `<<>>` for rule values in tests.** Test-script ergonomic — heterogeneous tuple arities can't satisfy a single `Dict<Symbol, Tuple<...>>` annotation. `(list)` produces uniform `Symbol[]` values.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] Plan's `<interfaces>` block suggested `Dict<Symbol, Array[Symbol]>` annotation, but the TypeParser rejects bare `Tuple` (which the rule values literally are at runtime)**

- **Found during:** Task 2 — first attempt to write the composer test with `Dict<Symbol, Tuple>` annotation
- **Issue:** The plan's open question in Task 2 step 7 explicitly flagged this: "rule values may need to be `Array[Symbol]` OR `Sequence` (Symbol-typed) — pick `Array[Symbol]` per REQ wording + Phase 26.1 array shape. The `<<#A, #B>>` literal form is Phase 26.1 Tuple syntax; if Tuple-of-Symbol works for rule values, accept it; otherwise convert to Array[Symbol] in the registration." Heterogeneous tuple arities can't satisfy a single Dict value-type annotation; the TypeParser also rejects bare `Tuple` without `<<...>>` arity.
- **Fix:** (a) NormalizeRules accepts BOTH tuple- and array-shaped values via the shared IReadOnlyList<Value> CLR storage; (b) generative.flow's forward decl annotates rules as `Dict<Symbol, Symbol[]>` (the cleanest composer-facing annotation that works); (c) test scripts use the `(list)` builtin to produce uniform Symbol[] rule values. Composer can still use Tuple literals at the runtime call site — the C# wildcard registration `new DictType(VoidType, VoidType)` accepts any DictType.
- **Files modified:** `flow-lang/StandardLibrary/Generative/LsystemFunctions.cs` (NormalizeRules), `flow-lang/generative.flow` (forward decl annotations + comment documenting the dual-shape acceptance), `tests/test_lsystem_oneshot.flow` + `tests/test_lsystem_train_generate.flow` (use `(list)` for rule values)
- **Verification:** All 13 Lsystem xUnit facts + 8 composer tests pass.
- **Committed in:** `e4b93ba` (Task 2 commit)
- **Documented as:** This SUMMARY's key-decisions entry "Rule values: Symbol[] over Tuple..." + generative.flow's comment block.

**2. [Rule 1 — Bug] `eq` is not the correct Flow builtin name — `equals` is**

- **Found during:** Task 2 — first run of `LsystemToSequenceMapperInvocation` test failed with `Function 'eq' not found`
- **Issue:** The plan's `<interfaces>` block showed example Flow code with `(eq s #A)`. The actual Flow builtin name is `equals` (StdLib.Equals registered at BuiltInFunctions.cs:433).
- **Fix:** Test scripts use `(equals s #A)` instead of `(eq s #A)`.
- **Files modified:** `flow-lang.Tests/Phase36/LsystemModelTests.cs`, `tests/test_lsystem_oneshot.flow`
- **Verification:** LsystemToSequenceMapperInvocation passes; the composer one-shot test's mapper lambda fires correctly.
- **Committed in:** `e4b93ba` (Task 2 commit)

**3. [Rule 3 — Blocking] `LsystemModel` was not in the IsTypeKeyword allowlist or the TypeParser map**

- **Found during:** Task 2 — anticipated from Plan 36-06's analogous deviation
- **Issue:** Same as Plan 36-06's deviation #2 — new SpecialTypes need three registration sites in the parser to allow `TypeName var = ...` declarations.
- **Fix:** Added `LsystemModel` to (a) `Parser.IsTypeKeyword` allowlist, (b) `TypeParser.ParseType` switch arm, (c) `TypeParser` helper map at `~line 340`.
- **Files modified:** `flow-lang/Parsing/Parser.cs`, `flow-lang/Parsing/TypeParser.cs`
- **Verification:** All Lsystem behavior tests pass; `LsystemModel m = ...` declarations parse.
- **Committed in:** `e4b93ba` (Task 2 commit)

---

**Total deviations:** 3 auto-fixed (Rule 1 ×1 — builtin name confusion; Rule 3 ×2 — both blocking the test surface from working).
**Impact on plan:** All three auto-fixes preserved the plan's contract — none changed the composer-facing surface semantics, the algorithm, or the determinism guarantee. The dual-shape rule-value acceptance (#1) is a strictly-additive composer ergonomic; the builtin-name fix (#2) is conventional plumbing; the type-name allowlist (#3) is the same conventional plumbing Plan 36-06 documented for any new SpecialType.

## Issues Encountered

**Pre-existing orphan working-tree changes.** Inherited from `36-01-SUMMARY.md` § "Issues Encountered" and re-confirmed in `36-05-SUMMARY.md` and `36-06-SUMMARY.md`. The worktree base contains uncommitted modifications to `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs`, `flow-midi/Conversion/FlowGenerator.cs`, etc., that predate this worktree's spawn. They cause ~32 net failures in `Phase28.PerSynthArticulationTests`, `Phase29.ArticulationOnSampleTests`, etc. Per the destructive_git_prohibition I cannot roll them back; per the deviation rules' SCOPE BOUNDARY they are out of scope for Plan 36-07. The orchestrator should resolve them at merge time.

**In-scope test results:**

| Suite | Pass/Total | Status |
|-------|------------|--------|
| Phase 36 (full — incl. 36-01..06 + 36-07) | 104/104 | green |
| Phase 35 (language foundation regression) | 80/80 | unchanged |
| Lsystem surface (Plan 36-07 facts) | 14/14 | green |
| Two-run cmp-clean determinism on tests/test_lsystem_oneshot.flow | SHA match | green |
| Composer tests (test_lsystem_oneshot + test_lsystem_train_generate) | 8/8 | green |

## Self-Check: PASSED

**Files asserted:**

- `[ -f flow-lang/Runtime/LsystemModelData.cs ]` → FOUND
- `[ -f flow-lang/TypeSystem/SpecialTypes/LsystemModelType.cs ]` → FOUND
- `[ -f flow-lang/StandardLibrary/Generative/LsystemFunctions.cs ]` → FOUND
- `[ -f flow-lang.Tests/Phase36/LsystemModelTests.cs ]` → FOUND
- `[ -f flow-lang.Tests/Phase36/LsystemDeterminismTests.cs ]` → FOUND
- `[ -f tests/test_lsystem_oneshot.flow ]` → FOUND
- `[ -f tests/test_lsystem_train_generate.flow ]` → FOUND

**Commits asserted:**

- `28091f1` (Task 1) → FOUND in `git log --oneline`
- `e4b93ba` (Task 2) → FOUND in `git log --oneline`

**No-regression assertions:**

- Phase 36: 104/104 PASS (was 91 in Plan 36-06; +13 from Plan 36-07's xUnit facts; +0 from Plan 36-07 changes regressing prior plans)
- Phase 35: 80/80 PASS (unchanged from Plan 36-06 baseline)
- Two-run cmp-clean: SHA-256 `509d3994ad80172dfb2eb63ae217fc3253682ad434698829b8bdde9bd7c6fb4b` identical across 2 renders of `tests/test_lsystem_oneshot.flow`
- Source-grep gates: 0 unsanctioned `new Random(` in Generative directory (Plan 36-06's 2 sanctioned hits in MarkovFunctions.cs unaffected; LsystemFunctions.cs has 0 hits total — no sanctioned-marker needed)

## What This Unblocks

- **Plan 36-08 — Cellular automata primitive** — same patterns inheritable: reference-identity model class (`CellularModelData`), `CellularModelType` singleton, `Value.CellularModel` factory, sanctioned-marker for explicit-seed overloads (CA has a seed-driven 30%-density 2D Life fill per RESEARCH §Pattern 4), DoS-bounded iteration count guard (CA has both width × steps exponential risk and 2D grid risk — same MaxIterations clamp shape applies).
- **Plan 36-09 — Chaos primitive (Lorenz / logistic)** — DoS-bounded iteration count guard pattern reused for `length` arg. Chaos primitives are mostly deterministic (Lorenz initial conditions derived from seed — Plan 36-09 will likely use PRNG-SANCTIONED markers for the initial-condition derivation).
- **Plan 36-12 — Phase 36 GEN-05 phase gate** — `tests/test_lsystem_oneshot.flow` joins `tests/test_patterns_chain.flow` (Plan 36-05) + `tests/test_markov_oneshot.flow` (Plan 36-06) as a third canonical two-run cmp-clean target.

## Threat Surface Scan

No new threat surface beyond the plan's `<threat_model>` register:

| Threat | Disposition | Status |
|--------|-------------|--------|
| T-36-17 (DoS / iterations > 20 → exponential explosion) | mitigate | ✓ `MaxIterations = 20` + `ClampIterationsWithAdvisory` emits WarnOnce |
| T-36-18 (Integrity / rule dict mutability across model reuse) | mitigate | ✓ Rules stored as `IReadOnlyDictionary` at LsystemModelData construction; class is immutable post-construction (no setters) |

No new threat flags emerged.

---

*Phase: 36-sequence-algebra-generative*
*Plan: 07*
*Completed: 2026-05-22*
