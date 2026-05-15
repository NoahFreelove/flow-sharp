# Phase 32 — Deferred Items

Out-of-scope discoveries logged during plan execution. Per the executor's
SCOPE BOUNDARY rule, pre-existing failures in unrelated files are NOT fixed
by the current plan — they're logged here for a future cleanup pass.

## Pre-existing failures observed during Plan 32-02 (Wave 1) execution

- **`Phase28.PerSynthArticulationTests.PerSynthArticulation_NormalVsArticulated_FFTCosineDifferentiable`** — 24 parameterized variants fail (every synth × art combination tried). Failure is in the FFT cosine differentiability check, completely unrelated to Phase 32 .scl/.kbm parsing.
- **`Phase28.RagtimeFixtureTests.Ragtime_Synthetic_RmsRegression`** — RMS deviation in window 0 exceeds the ±0.5 dB tolerance (SPEC-8 default).
- **`Phase28.RagtimeFixtureTests.Ragtime_MapleLeaf_RmsRegression`** — same RMS deviation pattern (-22.83 dB expected, -23.91 dB got, delta 1.07 dB).

These failures were verified pre-existing in the worktree baseline (`efd875c4362efac4894fe38f48dcb9539f5d7349`) before any Plan 32-02 changes were committed. The failures persist in the current HEAD because Phase 32 Plan 02 only adds new files under `flow-lang/StandardLibrary/Audio/Tuning/` + `flow-lang.Tests/Unit/Phase32/` — no Phase 28 code was touched.

Recommended owner: a future Phase 28 maintenance pass (likely needs a baseline regeneration after a synth-rendering change crept in pre-Phase 32).
