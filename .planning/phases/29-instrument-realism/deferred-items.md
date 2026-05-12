# Phase 29 — Deferred Items

Items observed during execution that are out of scope for the current plan and
have been deferred. The phase-wave executor logs items here under the
**SCOPE BOUNDARY** rule — pre-existing failures and discoveries unrelated to
the current task changes are noted here, not auto-fixed.

## Phase 28 PerSynthArticulationTests — 26 failing rows

**Observed during:** Plan 29-06 final-verification sweep
**Suite:** `FlowLang.Tests.Unit.Phase28.PerSynthArticulationTests.PerSynthArticulation_NormalVsArticulated_FFTCosineDifferentiable`
**Status:** 26 failing rows (94/120 pass); pre-existing — first surfaced and
documented in the Plan 29-04 SUMMARY (which counted 20 failures at that wave).

**Why deferred:**

- The test suite bypasses `FlowEngine.CurrentSampleCache` — it instantiates
  synthesizers directly and calls `SynthUtils.GenerateArticulationADSR`
  in isolation. That code path is unrelated to the Phase 29 A/B fixtures,
  which all go through `renderSong → eager-load → SampleCache → renderer`.
- Plan 29-06 made no changes to `flow-lang/StandardLibrary/Audio/` or any
  synthesizer code (verified via `git diff --stat HEAD~9 HEAD -- flow-lang/`).
- The two new test files added in Plan 29-06 (`AbFixtureSmokeTests`,
  `Phase29ByteIdenticalTests`) are GREEN; the closure A/B render path is
  verified end-to-end with synthetic baselines.

**Next step:** Investigate as a separate plan in Phase 29 closure work
(Plan 29-07 or a follow-up cleanup plan). Likely candidates: FFT tolerance
drift introduced by recent articulation envelope changes, or a sample-rate
/ frame-count assumption that no longer holds for the bell synth at certain
articulation types.

## (No other deferred items.)
