# Phase 40 — Deferred / Out-of-Scope Items

## Pre-existing full-suite flake: WASM + Phase47 build-smoke tests (NOT a Plan 40-01 regression)

**Discovered during:** Plan 40-01 execution (full Desktop suite regression check).

**Symptom:** Running the ENTIRE `flow-lang.Tests` suite (`dotnet test flow-lang.Tests`,
xUnit default cross-collection parallelism) intermittently fails 2-3 tests:
- `Phase48.WasmSynchronousExecutionTests.RunFromJs_SimpleScript_RunsToCompletion_PopulatedStdout_NoErrors`
- `Phase48.WasmSynchronousExecutionTests.RunFromJs_ToneRender_RunsToCompletion_NoCancel`
- `Phase47.BuildConditioningSmokeTests.DefaultBuild_ExitCodeIsZero_AndImpliesDesktop`

**Proof it is PRE-EXISTING (not caused by Plan 40-01):** the full suite run with ALL
Phase40 tests EXCLUDED (`--filter "FullyQualifiedName!~Phase40"`) STILL fails the same
2 WASM tests + the Phase47 build smoke. All three PASS in isolation (3/3).

**Root cause (two independent pre-existing races):**
1. `WasmEntry.RunFromJs` redirects the process-wide `Console.Out`/`Console.Error`
   (D-48-15) and so do many Flow-engine tests (via `FlowEngineRunner`). xUnit
   parallelizes distinct test COLLECTIONS, and `Console.Set*` is process-global, so a
   Console-redirecting class in collection A can capture a WASM test's stdout while
   they run concurrently. `WasmEntryConsoleCollection` only serializes WASM-vs-WASM; it
   does NOT serialize against the `FlowScripts` / `ConsoleCapture` collections.
2. `BuildConditioningSmokeTests` shells out `dotnet build`, which contends with the
   test host's own concurrent build/restore.

**What Plan 40-01 DID do (in-scope):** the two new Console-redirecting Phase40 test
classes (`VirtualMidiTests`, `OfflineRenderDeterminismTests`) were placed in
`[Collection(WasmEntryConsoleCollection.Name)]` so they run SERIALLY with the WASM
tests — Phase40 + all WASM console tests is green 3×/3× in isolation. Plan 40-01 does
NOT introduce or worsen the underlying cross-collection race.

**Recommended fix (out of scope — touches Phase47/Phase48 test infra, not Plan 40-01
files):** unify every Console-redirecting / `dotnet build`-shelling test class under a
single non-parallel collection (or set an assembly-level
`[assembly: CollectionBehavior(DisableTestParallelization = true)]` for the console-
sensitive subset). Suggested owner: a Phase 41 / test-infra cleanup pass.

## Plan 40-04 update (RtMidi.Core → direct librtmidi P/Invoke)

**Re-confirmed PRE-EXISTING:** the same 2 WASM `RunFromJs_*` Console-race failures were
observed on CLEAN `dev` (Plan-40-04 changes stashed) in a full-suite run — so they are
NOT introduced by the ABI fix. They reproduce intermittently (~1 in 2 full-suite runs)
and PASS 2/2 in isolation; a subsequent full run with the Plan-40-04 changes applied was
fully green (2255 passed, 0 failed). Still the same cross-collection Console-redirection
race; recommendation unchanged.

**What Plan 40-04 DID do (in-scope, Rule 1 test-infra fix):** the new real-hardware
`RealMidiLoopbackTests` spawn CPU-heavy `amidi` child processes + busy-poll a real
librtmidi input. Run in parallel they jittered the real-time Stopwatch deltas that the
in-process `ClockSlaveTests` / `ClockMasterTests` derive BPM from (one full-suite run
saw `ClockSlaveDrivesTempo` read 89.8 BPM vs. its 100–150 BPM band). Fix: all three
classes (`RealMidiLoopbackTests`, `ClockSlaveTests`, `ClockMasterTests`) joined
`[Collection(WasmEntryConsoleCollection.Name)]` so the CPU-heavy real-hardware class
never runs alongside the timing-sensitive clock tests. After the fix the clock-timing
flake no longer reproduces; Phase 40 is 45/45 green in isolation and the full suite is
green when the unrelated WASM Console race does not fire.
