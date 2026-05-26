# Phase 44 — Deferred Items

Out-of-scope discoveries made during Plan 44-11 execution. Not caused by
Plan 44-11; documented here for future quick-task pickup.

## Pre-existing test failures (5)

Verified pre-existing by checking that Plan 44-11's Task 1 + Task 2 commits
do NOT touch the affected files. These failures predate Plan 44-11 and are
out-of-scope per the SCOPE BOUNDARY rule (only auto-fix issues DIRECTLY
caused by current task's changes).

### 1. `Phase26_1.SymbolFacts.StrictSeparation_SymbolNeqString`
- **Error:** `equals overload equals(Void, Void) not registered`
- **Cause:** Plan 44-09 migrated `equals` from `RegisterStdLib` to a
  context-dependent registration inside `RegisterStdLibStrictAware`. The
  test's `BuildRegistry` helper only calls `BuiltInFunctions.RegisterAllImplementations`
  which does not register the context-dependent `equals` signature.
- **Pattern:** Same shape as Plan 44-05's test-harness drift for
  HumanizeGaussianFacts + TransformInvarianceFacts (Rule 1 auto-fix at
  that time). Equivalent fix: wire the context-dep path into SymbolFacts'
  BuildRegistry.
- **Quick-task scope:** ~5 LOC change in `SymbolFacts.BuildRegistry`.

### 2. `Phase26_1.SymbolFacts.EqualsBuiltinReturnsTrueForSameSymbol`
- Same root cause as #1.

### 3. `Phase35.FlowTestCliTests.FlowTestRunsAllRegisteredTests`
- **Error:** `flow.dll missing at .../flow-cli/bin/Debug/net10.0/flow.dll`
- **Cause:** Test environment dependency — `flow-cli/flow-cli.csproj`
  must be built before this Fact runs. Not in the default `dotnet test`
  build chain.
- **Workaround:** Run `dotnet build flow-cli/flow-cli.csproj` before
  `dotnet test`. Not a regression; not Plan 44-11's responsibility.

### 4. `Phase35.FlowTestCliTests.FailingTestExitsNonZero`
- Same root cause as #3.

### 5. `Phase38.OscLoopbackTests.RoundTrip_127001_EphemeralPort_PreservesPayload`
- Network test — likely flaky in CI / sandbox environments where loopback
  socket binding behavior differs. Not caused by Plan 44-11.

## Manifest line numbers stale

Plan 44-05 noted manifest line numbers are stale after Wave 3 rewrites.
Plan 44-11 does NOT update them — out-of-scope (a future quick task can
resync if forward drift becomes painful).

## Note on running tests

When running `dotnet test --filter Category=Phase44`, all 206 Phase 44
tests are GREEN. The 5 failures above only surface in the broader
`FullyQualifiedName!~Phase44` run.
