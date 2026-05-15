# Phase 31 — Deferred Out-of-Scope Discoveries

Out-of-scope failures observed during Plan 31-02 execution. These are
pre-existing failures unrelated to LSP work; logged here per the executor's
SCOPE BOUNDARY rule.

## Phase 28 PerSynthArticulation FFT regression (62 failures)

- **Tests:** `FlowLang.Tests.Unit.Phase28.PerSynthArticulationTests.*` (24)
  + `FlowLang.Tests.Integration.Phase28.*` + `FlowLang.Tests.FlowScriptTests.RunsToCompletion`
- **Pre-existing as of commit `11e3942`** (Phase 31 Plan 31-01 close-out).
- **Out of scope for Plan 31-02** — all changes here are diagnostic-only LSP
  paths; flow-lang DSP / synthesizer rendering is untouched.
- **Suggested follow-up:** Phase 28 investigation OR Phase 29 sampled-instrument
  follow-up — likely a side effect of the synthesis vs sample envelope split
  documented in CLAUDE.md (Phase 29 v1.5 backlog item).

No action taken in Plan 31-02; logged here for surfacing.
