# Phase 46 — Deferred Items

Out-of-scope discoveries logged during execution (not caused by phase 46 changes).

## Pre-existing test failures (environment-dependent, unrelated to phase 46 cleanups)

Observed during plan 46-03 full `dotnet test` run (8 failed / 2182 passed / 9 skipped).
None reference the touched files (audio.flow createSineTone decls, ClampSamples, PulseAudioSimpleBackend, PlaybackFunctions). `FailingTestExitsNonZero` reproduced identically against base (pre-edit) D-08 files, confirming pre-existing.

- `FlowLang.Tests.Integration.Phase38.OscLoopbackTests.RoundTrip_127001_EphemeralPort_PreservesPayload` — OSC UDP loopback; no message within 2s (network/socket-dependent in sandbox).
- `FlowLang.Tests.Integration.Phase48.DryWetMidiWasmPublishTests.FlowLangDll_RetainsDryWetMidiReference` — requires WASM publish workload.
- `FlowLang.Tests.Integration.Phase48.WasmDeterminismTests.SameSource_TwoRuns_IdenticalStdout` — requires WASM toolchain.
- `FlowLang.Tests.Phase35.FlowTestCliTests.FailingTestExitsNonZero` — CLI subprocess spawn.
- `FlowLang.Tests.Phase35.FlowTestCliTests.FlowTestRunsAllRegisteredTests` — CLI subprocess spawn.
- (3 additional failures in the same Phase38/Phase48/Phase35 environment-dependent families.)

These are environment/toolchain failures, not behavior regressions from the D-05/D-08 cleanups.
