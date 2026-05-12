---
phase: 260509-qqe-fix-phase-26-deferred-blockers
plan: 01
subsystem: interpreter + corpus
tags: [phase-26, fix-omissions, blocker-1, blocker-3, coercion, idiv, str, void-wildcard]
requires:
  - "flow-lang/Interpreter/ExpressionEvaluator.cs (existing D-05/D-06 coercion loop)"
  - "flow-lang/TypeSystem/ArrayType.cs"
  - "flow-lang/TypeSystem/PrimitiveTypes/VoidType.cs"
  - "flow-lang/StandardLibrary/BuiltInFunctions.cs:197 (str(Void[]) registration — UNCHANGED)"
provides:
  - "Void[] wildcard pass-through in EvaluateFunctionCall coercion loop"
  - "Phase26.StrTypedArrayFacts permanent regression guard (3 theories)"
  - "6 (div Int Int) -> (idiv Int Int) site rewrites in tests/examples corpus"
  - "Phase 18 ByteIdentical Tutorial guards GREEN (was RED at Phase 26 closure)"
affects:
  - "flow-lang/Interpreter/ExpressionEvaluator.cs"
  - "flow-lang.Tests/Unit/Phase26/StrTypedArrayFacts.cs (new)"
  - "tests/test_comments.flow:35"
  - "examples/long_demo.flow:356, 440, 441"
  - "tests/demo_expressive_piano.flow:39"
  - "tests/demo_feature_showcase.flow:231"
  - ".planning/STATE.md"
  - ".planning/phases/26-op-standardization-prefix-only/26-VERIFICATION.md"
  - ".planning/phases/26-op-standardization-prefix-only/.continue-here.md (deleted)"
  - ".planning/phases/26-op-standardization-prefix-only/deferred-items.md (new)"
tech_stack:
  added: []
  patterns:
    - "Strategy A coercion guard: skip ConvertTo when signature parameter is ArrayType(VoidType) — wildcards accept calls without transforming runtime storage"
key_files:
  created:
    - "flow-lang.Tests/Unit/Phase26/StrTypedArrayFacts.cs"
    - ".planning/phases/26-op-standardization-prefix-only/deferred-items.md"
  modified:
    - "flow-lang/Interpreter/ExpressionEvaluator.cs"
    - "tests/test_comments.flow"
    - "examples/long_demo.flow"
    - "tests/demo_expressive_piano.flow"
    - "tests/demo_feature_showcase.flow"
    - ".planning/STATE.md"
    - ".planning/phases/26-op-standardization-prefix-only/26-VERIFICATION.md"
  deleted:
    - ".planning/phases/26-op-standardization-prefix-only/.continue-here.md"
decisions:
  - "Float[] InlineData substituted with Double[] in StrTypedArrayFacts (pre-existing parser quirk: floating literals lex as Double; both share the (str X[]) Void[] wildcard pass-through path so the regression guard is faithful)"
  - "String[] InlineData uses variable references (`String[] ys = [a, b]`) instead of string literals (`[\"a\", \"b\"]`) — pre-existing parser quirk where bare-letter strings inside an array literal lex as Note[]"
  - "test_error_masking.flow and test_musical_context_errors.flow added to the smoke-loop intentional-error allow-list alongside test_iteration_guard.flow (each documents an expected non-zero exit in its own header — they were miscategorized under Blocker 3 in the closure verification report)"
  - "DecibelBeatNumericCompatFacts (2/8 FAIL) determined out-of-scope per deviation Rule scope-boundary — pre-existing at Phase 26 closure HEAD; logged to deferred-items.md"
metrics:
  duration: "~14 minutes (Task 1 build+test+commit, Task 2 corpus edits + smoke loop, Task 3 docs)"
  completed: "2026-05-09"
---

# Phase 260509-qqe: fix-phase-26-deferred-blockers Summary

Closes the two interpreter omissions deferred at Phase 26 closure (commit 3f59376) — Blocker 1 (Void[] wildcard-coercion crash on `(str X[])`) and Blocker 3 (Int-typed `(div Int Int)` returning Double) — via three atomic commits. Phase 18 ByteIdentical Tutorial guards flip RED → GREEN; full ByteIdentical filter now 8/8 PASS. v1.3 milestone advances from "shipped-with-known-omissions" to "shipped" with three phases remaining (26.1, 26.2, 27).

## Commit SHAs

| # | Type | SHA | Title |
|---|------|-----|-------|
| 1 | fix | **75fb694** | `fix(phase-26): (str X[]) Void[] wildcard pass-through in coercion loop` |
| 2 | fix | **3285d19** | `fix(phase-26): (div Int Int) -> (idiv ...) at Int-typed assignment sites` |
| 3 | docs | **d88a6ab** | `docs(phase-26): close fix-omissions — STATE clean + VERIFICATION sign-off + .continue-here.md removed` |

## ByteIdentical Guard Counts

| Filter | Before | After |
|--------|--------|-------|
| Phase18.ByteIdentical (Showcase + Tutorial) | 2/4 PASS | **4/4 PASS** |
| Phase23.ByteIdenticalDefaultTuning | 2/2 PASS | 2/2 PASS |
| Phase25.ByteIdenticalShowcaseGaussian | 2/2 PASS | 2/2 PASS |
| **Combined ByteIdentical filter** | **6/8 PASS** | **8/8 PASS** |
| Phase26.StrTypedArrayFacts (NEW) | n/a | **3/3 PASS** |

## Smoke Loop Counts

| Path | Before (Phase 26 closure) | After (Phase 260509-qqe) |
|------|---------------------------|--------------------------|
| Total .flow files in scope | 94 | 91 (corpus changed slightly between then and now) |
| Pass | 75 | **88** |
| Intentional-error fixtures correctly skipped | 0 (all 3 miscategorized as Blocker 3) | **3** (test_iteration_guard, test_error_masking, test_musical_context_errors) |
| Unintended failures | **19** (all Blocker 1 or Blocker 3) | **0** |

## Per-File `(div Int Int) → (idiv ...)` Site List (Task 2)

| File | Line | Change |
|------|------|--------|
| `tests/test_comments.flow` | 35 | `Int d = (div 10 2)` → `Int d = (idiv 10 2)` |
| `examples/long_demo.flow` | 356 | `Int mainSec = (div mainFrames 44100)` → `Int mainSec = (idiv mainFrames 44100)` |
| `examples/long_demo.flow` | 440 | `Int totalSec    = (div totalFrames sampleRate)` → `Int totalSec    = (idiv totalFrames sampleRate)` |
| `examples/long_demo.flow` | 441 | `Int totalMin    = (div totalSec 60)` → `Int totalMin    = (idiv totalSec 60)` |
| `tests/demo_expressive_piano.flow` | 39 | `Int duration = (div totalFrames 44100)` → `Int duration = (idiv totalFrames 44100)` |
| `tests/demo_feature_showcase.flow` | 231 | `Int durationSec = (div totalFrames sampleRate)` → `Int durationSec = (idiv totalFrames sampleRate)` |

Total: **6 sites across 4 files** — exactly matches the planner's audit.

`(div ...)` sites preserved (verified post-edit): `tests/test_lambdas.flow:45`, `tests/test_custom_oscillator.flow:16, 58, 86`, `tests/test_migrate26_smoke.flow:11, 15` (Float/Double/lambda contexts — D-08 makes these correct as-is).

`.continue-here.md` deletion: **confirmed deleted** in commit d88a6ab. `git status` returned clean tree post-commit.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] StrTypedArrayFacts InlineData adjusted for pre-existing parser quirks**
- **Found during:** Task 1 Step 1.3 verification
- **Issue:** The plan's literal sources (`Float[] zs = [1.0, 2.0, 3.0]` and `String[] ys = ["a", "b", "c"]`) hit pre-existing array-literal type-inference quirks orthogonal to Blocker 1: floating-point literals lex as Double (not Float) and bare-letter strings inside an array literal lex as Note[] (not String[]). Both quirks predate Phase 26.
- **Fix:** Substituted Double[] for Float[] (both share the (str X[]) Void[] wildcard pass-through path, so the regression guard is faithful to Blocker 1's mechanism) and used variable references for the String[] case. The Int[] theory is unchanged.
- **Result:** 3/3 StrTypedArrayFacts PASS; the regression guard is permanent and pins Strategy A correctness.
- **Commit:** 75fb694

**2. [Rule 3 - Blocking] Smoke-loop intentional-error allow-list extended**
- **Found during:** Task 2 smoke-loop verification
- **Issue:** Two negative-test fixtures (`test_error_masking.flow`, `test_musical_context_errors.flow`) explicitly document an expected non-zero exit in their headers — they're intentional-error tests, just like `test_iteration_guard.flow` (already in the plan's allow-list). The Phase 26 closure verification report miscategorized them under Blocker 3 even though they have ZERO `(div ...)` content. They are byte-identical to their Phase 26 closure HEAD versions; their behavior has not changed.
- **Fix:** Extended the local smoke-loop allow-list (used only for verification, not committed to source) from 1 file to 3 files. Documented in 26-VERIFICATION.md Smoke Loop section: "0 unintended failures (88 pass + 3 intentional-error fixtures correctly excluded)".
- **Result:** Smoke loop reports `fails=0` — Blockers 1+3 fully closed.
- **Files modified:** `.planning/phases/26-op-standardization-prefix-only/26-VERIFICATION.md` Smoke Loop section, `.planning/STATE.md` Status paragraph.
- **Commit:** d88a6ab

### Out-of-scope Discoveries (Logged, Not Fixed)

**3. [Rule scope-boundary] DecibelBeatNumericCompatFacts 2/8 FAIL — pre-existing**
- **Found during:** Task 3 Step 3.1 full-suite test before docs
- **Issue:** `DecibelBeatNumericCompatFacts.GainWithDecibelLiteral_ResolvesAndProducesSameOutputAsDoubleEquivalent` and `GainWithPositiveDecibelLiteral_AppliesExpectedLinearGain` fail with "Cannot convert Flow type 'Decibel' with underlying CLR type 'Double' to Flow target type 'Double'". Verified present at all three HEADs: pre-Task-1 baseline (02a0319, == Phase 26 closure 3f59376), post-Task-1 (75fb694), post-Task-2 (3285d19) — same 2/8 fail count, same error messages. Not caused by Blockers 1+3 fixes; orthogonal to Phase 26 prefix-only migration.
- **Action:** Logged to `.planning/phases/26-op-standardization-prefix-only/deferred-items.md` per the deviation-rules scope boundary ("Only auto-fix issues DIRECTLY caused by the current task's changes"). Recommended future quick-task to investigate Decibel→Double Value coercion path.
- **Impact on this quick-task:** None on the primary gates (ByteIdentical 8/8 GREEN, Phase26.StrTypedArrayFacts 3/3 GREEN, smoke loop 0 unintended fails). The plan's "dotnet test full suite reports 0 failures" gate would technically have failed both before and after this quick-task — those 2 failures pre-date the work and are the only failures in the entire suite (779/781 pass elsewhere).

## Decisions Made

- **Strategy A over Strategy B for Blocker 1.** Plan-mandated. The fix lives in the resolver (coercion loop), not in `BuiltInFunctions.cs:197`'s `str(Void[])` registration nor in `ArrayType.CanConvertTo`. Other call sites depend on `ArrayType.CanConvertTo`'s existing semantics.
- **Option A (hand-fix) over Option B (smarter migrator) for Blocker 3.** Plan-mandated. Six explicit per-site edits with a no-blanket-replace policy. The migrator (`scripts/Migrate26/`) was not touched. Per `.continue-here.md` Blocker 3, Option B (smarter walker with AST awareness) was the alternative; Option A is preferred because it leaves the user's "very little was broken" framing intact.
- **Float[] regression case substituted with Double[].** See Deviation #1 above. Faithful to Blocker 1's mechanism; documented inline in StrTypedArrayFacts.cs class-level XML doc.
- **STATE.md status `shipped` (not `idle`).** Plan-suggested. Phase 25's closure used `shipped`; the SDK schema accepted it without complaint.

## Self-Check: PASSED

- [x] flow-lang/Interpreter/ExpressionEvaluator.cs — exists, contains `ArrayType { ElementType: VoidType }` guard (verified)
- [x] flow-lang.Tests/Unit/Phase26/StrTypedArrayFacts.cs — exists, 53 lines, 3 theories all PASS
- [x] tests/test_comments.flow:35 — `Int d = (idiv 10 2)` (verified by grep)
- [x] examples/long_demo.flow:356, 440, 441 — all `(idiv ...)` (verified)
- [x] tests/demo_expressive_piano.flow:39 — `Int duration = (idiv totalFrames 44100)` (verified)
- [x] tests/demo_feature_showcase.flow:231 — `Int durationSec = (idiv totalFrames sampleRate)` (verified)
- [x] Commit 75fb694 — present in `git log --oneline -5` (verified)
- [x] Commit 3285d19 — present in `git log --oneline -5` (verified)
- [x] Commit d88a6ab — present in `git log --oneline -5` (verified)
- [x] .continue-here.md — deleted (confirmed via `test -e ... && echo EXISTS || echo deleted`)
- [x] STATE.md `status: shipped` (verified)
- [x] 26-VERIFICATION.md `status: complete` (verified)
- [x] ByteIdentical filter 8/8 PASS at HEAD (verified post-Task-3 commit, 6 s elapsed)
- [x] StrTypedArrayFacts 3/3 PASS at HEAD (verified)
- [x] Repro from .continue-here.md Blocker 1 — exits 0, prints `[1, 2, 3]` (verified)
- [x] No latent `Int IDENT = (div ...)` sites — `grep -rn` returns 0 matches (verified)
- [x] Working tree clean post-Commit 3 — `git status --short` empty (verified)

## Note for Orchestrator

The orchestrator handles the docs commit (PLAN/SUMMARY/STATE-table) separately as the fourth and final commit per the standard /gsd:quick flow. This SUMMARY.md is one of the inputs to that orchestrator commit, alongside any STATE.md table updates the orchestrator chooses to apply.
