# Phase 26 — Deferred Items

Out-of-scope discoveries surfaced during the fix-omissions quick-task
(260509-qqe) that are NOT regressions of Blockers 1+3 and were already
failing at Phase 26 closure HEAD (3f59376).

## DecibelBeatNumericCompatFacts — 2/8 FAIL (pre-existing)

**Test class:** `flow-lang.Tests/Unit/QuickFixes/DecibelBeatNumericCompatFacts.cs`

**Failing theories:**

1. `DecibelBeatNumericCompatFacts.GainWithDecibelLiteral_ResolvesAndProducesSameOutputAsDoubleEquivalent`
   - Error: `Cannot convert Flow type 'Decibel' with underlying CLR type 'Double' to Flow target type 'Double'`
   - Suggests Decibel→Double coercion path is missing or mis-ordered.

2. `DecibelBeatNumericCompatFacts.GainWithPositiveDecibelLiteral_AppliesExpectedLinearGain`
   - Error: `Assert.Equal() Failure: Values differ — Expected: 0, Actual: 1` (downstream of #1).

**State at multiple HEADs (verified):**

- HEAD~2 / `02a0319` (pre-Task-1 baseline, == Phase 26 closure 3f59376): **2 FAIL**
- HEAD~1 / `75fb694` (post-Task 1 Blocker 1 fix): **2 FAIL**
- HEAD / `3285d19` (post-Task 2 Blocker 3 fix): **2 FAIL**

**Conclusion:** Not caused by Blocker 1 or Blocker 3 fixes. Pre-existing at
Phase 26 closure. Out of scope per the fix-omissions plan
(`.planning/quick/260509-qqe-fix-phase-26-deferred-blockers-str-x-coe/260509-qqe-PLAN.md`)
which surfaces only Blockers 1 and 3 from `.continue-here.md`.

**Why deferred:** the fix-omissions plan's scope is precise (Blockers 1+3
only) and the user's framing for the closure is "very little was broken
... not critical." Decibel/Double coercion is a separate semantic concern
in the QuickFixes harness, not the Phase 26 prefix-only migration.

**Recommended owner:** a future quick-task or Phase 26.A-level follow-up
that can examine Value coercion paths between Decibel and Double in
isolation. Likely touches `flow-lang/TypeSystem/SpecialTypes/DecibelType.cs`
and `flow-lang/Runtime/Value.cs` (the ConvertTo dispatch for special-type
→ primitive). Possibly orthogonal to (str X[]) Void[] fix landed in this
quick-task.

**ByteIdentical guards remain GREEN at HEAD:** Phase 18 Showcase + Tutorial,
Phase 23 DefaultTuning, Phase 25 ShowcaseGaussian — all 8/8 PASS. The
Decibel issue does not gate the v1.3 milestone closure.
