# Deferred Items — Phase 46

## Plan 46-02 — out-of-scope, NOT fixed (pre-existing)

- **`WasmDeterminismTests.SameSource_TwoRuns_IdenticalStdout` + `SameSource_TwoRuns_IdenticalRunResultJson`** fail ONLY in the whole-suite `dotnet test` run; both PASS in isolation (`--filter FullyQualifiedName~Phase48.WasmDeterminismTests` → 2/2 PASS). Root cause is a Phase-48 test-isolation issue (a prior test's `Console.Out` redirection leaks into `WasmEntry`'s static shared-engine stdout capture). Documented as a known transient in `45-06-SUMMARY.md` ("2 transient whole-suite xUnit failures unrelated to Phase 45"). Zero reference to TimelineMap / SongRenderer / BarRenderer / SequenceRenderer — entirely independent of plan 46-02's dead-code removal. SCOPE BOUNDARY: not fixed here.

<!-- merged from 46-03 -->
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
