# Phase 42 — Deferred Items

Pre-existing test failures discovered during Plan 02 full-suite verification.
These failures are **out of scope** for Phase 42 (which only adds shell
scripts + an xUnit fixture under flow-lang.Tests/Integration/Phase42/) and
are present at the worktree spawn commit `c4cd738` (`docs(42): create phase
plan`) BEFORE any Phase 42 work was done.

## Failures observed (36 total, full suite run 2026-05-24)

- **FlowLang.Tests.Phase35.FlowTestCliTests** — `FailingTestExitsNonZero`,
  `FlowTestRunsAllRegisteredTests` (2)
- **FlowLang.Tests.Phase35.MatchExhaustivenessDefaultTests** —
  `NonExhaustiveDefaultWarnsAndReturnsVoid` (1)
- **FlowLang.Tests.Unit.Phase28.PerSynthArticulationTests** —
  `PerSynthArticulation_NormalVsArticulated_FFTCosineDifferentiable` across
  multiple (synth, articulation) Theory rows (~24)
- **FlowLang.Tests.Integration.Phase29.ArticulationOnSampleTests** —
  `Piano_Articulation_AudibleContentRatio_MatchesPhase28EnvelopeShape`
  across multiple (Articulation, ratio) Theory rows (~9)

## Disposition

Plan 02 does not touch:

- `flow-lang/StandardLibrary/` (any file)
- `flow-lang/TypeSystem/` (any file)
- `flow-lang/*.flow` (any stdlib module)
- any flow-lang.Tests/ file outside `Integration/Phase42/`

…so these failures cannot be caused by Plan 02. They originate in earlier
Phase 28/29/35 work and should be triaged by their respective phase owners.
The Phase 42 task list explicitly forbids production-code edits, so these
failures will not be addressed in this plan.

## Phase 42 Plan 02 fixture status

`flow-lang.Tests.Integration.Phase42.ClampGrepConsistencyTests` — all 6
facts PASS in 330 ms. New Phase 42 work introduces zero new failures.
