# Phase 26.1 — Deferred Items

Out-of-scope discoveries logged during plan execution. These are NOT touched in 26.1; they belong to a future phase or a bug-fix quick task.

## 1. DecibelBeatNumericCompatFacts — 2 pre-existing failures

**Discovered during:** Plan 26.1-01 (Wave 0) final test verification, 2026-05-09

**Where:** `flow-lang.Tests/Unit/QuickFixes/DecibelBeatNumericCompatFacts.cs`
- `GainWithDecibelLiteral_ResolvesAndProducesSameOutputAsDoubleEquivalent` (FAILED)
- `GainWithPositiveDecibelLiteral_AppliesExpectedLinearGain` (FAILED, line 131)

**Pre-existing:** Verified by checking out the parent commit `95bd946` (before any Phase 26.1 work) — same 2 failures present. NOT introduced by Wave 0 scaffolding.

**In-scope?** No. Plan 26.1-01 only adds new Skipped Fact stubs and stub `.flow` scripts that print PASSED. Cannot affect existing Decibel/Beat numeric resolution.

**Suggested follow-up:** A Phase 26.1 closure quick-task or a separate fix-omissions phase (analogous to `quick-260509-qqe`) should investigate. Likely a regression from a recent overload-resolution change (Phase 26 prefix-only arithmetic landing? — needs investigation).

**Triage hints:**
- Check git blame on `flow-lang.Tests/Unit/QuickFixes/DecibelBeatNumericCompatFacts.cs:131`
- Check whether `gain` builtin's Decibel overload still resolves correctly under `OverloadResolver`
- The "Equivalent to Double" test suggests the linear-gain math is computing a different value when input is a Decibel literal vs. a Double — may indicate Decibel→Double conversion regressed
