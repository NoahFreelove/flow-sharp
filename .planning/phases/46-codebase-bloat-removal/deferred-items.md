# Phase 46 — Deferred / Out-of-Scope Items

Items discovered during execution that are NOT caused by this phase's changes and are
out of scope per the executor scope boundary (only auto-fix issues directly caused by the
current task's changes).

## Plan 46-05

### Phase48.WasmDeterminismTests — full-suite test-isolation flake (PRE-EXISTING)

- **Tests:** `FlowLang.Tests.Integration.Phase48.WasmDeterminismTests.SameSource_TwoRuns_IdenticalStdout`
  and `...SameSource_TwoRuns_IdenticalRunResultJson`.
- **Symptom:** Both FAIL when the full `dotnet test` suite runs (2 failures / 2193 passed /
  9 skipped), but both PASS when run in isolation
  (`--filter "FullyQualifiedName~WasmDeterminismTests"` → 2/2 green).
- **Root cause (not investigated to fix):** These tests exercise `WasmEntry.RunFromJs` via a
  lazy-init shared `_sharedEngine` and redirect `Console.SetOut`/`SetError` (per the Plan 48-05
  STATE.md notes). Other tests running in the same xUnit process (including the new
  `ProgressionDslTests` / `EuclideanSwingTests`, which redirect Console via `FlowEngineRunner`)
  can race on that shared global state under full-suite parallel execution.
- **Not caused by Plan 46-05:** This plan's diff (HEAD vs spawn base 7992435) touches only
  `examples/showcase.flow`, `flow-lang.Tests/Unit/Phase46/ProgressionDslTests.cs`, and 5
  comment-only legacy notes (Timeline.cs / Track.cs / Bars.cs / bars.flow / composition.flow).
  No WASM / WasmEntry / Console-redirection production code was modified.
- **Disposition:** Deferred — a Phase 48 follow-up should make `WasmDeterminismTests` resilient to
  shared-engine / Console-redirection ordering (e.g. dedicated xUnit collection that does not run
  concurrently with Console-redirecting fixtures, or per-test engine instantiation).
