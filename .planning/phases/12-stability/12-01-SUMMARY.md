---
phase: 12-stability
plan: 01
subsystem: testing
tags: [xunit, test-harness, scaffolding, regression-tests]
type: execute
wave: 1
requires:
  - flow-lang/Core/FlowEngine.cs (unchanged — consumed API surface)
  - flow-lang/Diagnostics/ErrorReporter.cs (unchanged — consumed API surface)
  - tests/ directory (55 .flow scripts)
provides:
  - flow-lang.Tests project (first xUnit test project in repo)
  - FlowEngineRunner fixture (in-process driver with stdout/stderr capture)
  - FlowScriptTests Theory harness (wrap-as-Theory migration of all .flow scripts)
  - FlowScriptData MemberData glob + ExpectedErrorScripts/RequiredSentinels dicts
affects:
  - flow-sharp.sln (added flow-lang.Tests project reference)
tech-stack:
  added:
    - xunit.v3 3.2.2 (test framework)
    - xunit.runner.visualstudio 3.1.5 (VSTest adapter)
    - Microsoft.NET.Test.Sdk 17.13.0
    - coverlet.collector 6.0.2
  patterns:
    - "wrap-as-Theory: each .flow script becomes a [Theory] data row"
    - "Collection-based parallelism gate (DisableParallelization=true) for Console.SetOut safety"
    - "MemberData glob driven by Directory.EnumerateFiles"
    - "ExpectedErrorScripts dict for intentional-error assertions"
    - "RequiredSentinels dict for RED→GREEN-flip test rows (spike/c1)"
key-files:
  created:
    - flow-lang.Tests/flow-lang.Tests.csproj
    - flow-lang.Tests/Fixtures/FlowEngineRunner.cs
    - flow-lang.Tests/FlowScriptTests.cs
    - flow-lang.Tests/FlowScriptData.cs
  modified:
    - flow-sharp.sln (added flow-lang.Tests entry + ProjectConfigurationPlatforms rows via dotnet sln add)
decisions:
  - "xunit.v3 3.2.2 + xunit.runner.visualstudio 3.1.5 (fallback applied: xunit.v3.runner.visualstudio 3.2.2 does not exist on nuget.org)"
  - "FlowEngineRunner.FlushErrorsToStderr mirrors flow-interpreter/Program.cs:78 so ExpectedErrorScripts substring assertions work against FormatErrors() output"
  - "FlowScriptTests sets Environment.CurrentDirectory to repo root so relative-path scripts (test_wav_loading, test_full_song) resolve as they do under dotnet run"
  - "Row count is 55 (not plan-estimated 51) due to 4 demo/song .flow scripts at tests/ root (demo_expressive_piano, demo_feature_showcase, demo_vocal_listen, mary-had-a-little-lamb) which all pass cleanly"
  - "ExpectedErrorScripts includes test_custom_oscillator.flow and test_full_song.flow as pre-fix baselines; plan 12-05 will remove these entries after the underlying fixes land (if overload + exportWav auto-mkdir)"
metrics:
  duration: "~20 min"
  completed: "2026-04-19T14:13:46Z"
  tasks_total: 2
  tasks_completed: 2
  files_created: 4
  files_modified: 1
  commits: 2
---

# Phase 12 Plan 01: xUnit Harness + wrap-as-Theory Migration Summary

**One-liner:** Scaffolded the first xUnit test project in the repo (flow-lang.Tests) and wrapped all 55 .flow integration scripts as [Theory] rows via MemberData glob; spike/c1 lands RED deliberately pending FIX-07a in plan 12-04.

## What Was Built

### Task 1 — xUnit project scaffold + solution wiring
- Created `flow-lang.Tests/flow-lang.Tests.csproj` targeting `net10.0` with `RootNamespace=FlowLang.Tests`, `IsTestProject=true`
- PackageReferences: `Microsoft.NET.Test.Sdk` 17.13.0, `xunit.v3` 3.2.2, `xunit.runner.visualstudio` 3.1.5, `coverlet.collector` 6.0.2
- ProjectReference to `flow-lang`
- Created `flow-lang.Tests/Fixtures/FlowEngineRunner.cs` — in-process FlowEngine driver that captures `Console.Out`/`Console.Error` into `StringWriter` instances and exposes `RunFile(path)` / `RunSource(src)` returning `(bool Success, string Stdout, string Stderr, int ErrorCount)`
- Wired into `flow-sharp.sln` via `dotnet sln add` (GUID 7765B99F-0694-45E5-9E99-EFF722C869E2 auto-generated)
- **Commit:** `8ab4694` — `test(12-01): scaffold flow-lang.Tests xUnit project + wire into flow-sharp.sln`

### Task 2 — wrap-as-Theory migration
- Created `flow-lang.Tests/FlowScriptData.cs` with:
  - `GetFlowScripts()` MemberData source — globs `tests/**/*.flow` recursively, skips `std.flow`
  - `FindTestsRoot()` walks up from `AppContext.BaseDirectory` to locate the `tests/` directory
  - `ExpectedErrorScripts` dict: 7 entries covering intentional-error regression scripts + 2 pre-fix baselines
  - `RequiredSentinels` dict: spike/c1 body-execution evidence (4 sentinel strings)
- Created `flow-lang.Tests/FlowScriptTests.cs` with `FlowScriptsCollection` + `[Collection("FlowScripts")]` to serialize Theory rows (prevents `Console.SetOut` races)
- Theory `RunsToCompletion(relativePath)` sets `Environment.CurrentDirectory` to repo root inside a try/finally, then invokes FlowEngineRunner and asserts on expected-error substrings OR error count == 0, plus sentinel presence where registered
- **Commit:** `6868f9e` — `test(12-01): wrap-as-Theory migration of .flow scripts via MemberData glob`

## Results

- **Discovery:** 55 Theory rows registered (46 `test_*.flow` + 5 `spike/c*.flow` + 4 demo/song `.flow` scripts)
- **Pass/fail:** 54 passed, 1 failed — exactly matching plan D-11 (only spike/c1 RED)
- **spike/c1-musical-context-body.flow:** RED as designed. The `RequiredSentinels` assertion for `c1-probe1-body-ran` fails because `ExecuteMusicalContext` returns early on validation error and skips the body loop. This flips GREEN in plan 12-04 when FIX-07a converts 7 `return;` → `break;`.
- **Build:** `dotnet build flow-sharp.sln` exits 0 with zero errors (5 pre-existing warnings from flow-editor/flow-lang)
- **Test runtime:** ~15 seconds for all 55 rows (serialized)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] xunit.v3.runner.visualstudio 3.2.2 does not exist on nuget.org**
- **Found during:** Task 1 (`dotnet build` after initial csproj scaffolding)
- **Issue:** `NU1101: Unable to find package xunit.v3.runner.visualstudio`. The canonical Microsoft docs suggest this package but it was never published.
- **Fix:** Applied the fallback documented in the plan — substituted `xunit.runner.visualstudio` 3.1.5 (the shared v2/v3 VSTest adapter).
- **Files modified:** flow-lang.Tests/flow-lang.Tests.csproj (package name + version)
- **Commit:** `8ab4694`

**2. [Rule 3 - Blocking] ErrorReporter has no ErrorCount property**
- **Found during:** Task 1 (post-csproj build of fixture)
- **Issue:** `CS1061: 'ErrorReporter' does not contain a definition for 'ErrorCount'`. The plan's FlowEngineRunner template called `_engine.ErrorReporter.ErrorCount`, but the real API exposes `Errors` (IReadOnlyList) and `HasErrors` (bool).
- **Fix:** Changed to `_engine.ErrorReporter.Errors.Count`.
- **Files modified:** flow-lang.Tests/Fixtures/FlowEngineRunner.cs
- **Commit:** `8ab4694`

**3. [Rule 2 - Missing critical functionality] FlowEngine.Execute does not write errors to stderr**
- **Found during:** Task 2 (first test run — ExpectedErrorScripts assertions failed because stderr was empty)
- **Issue:** `FlowEngine.Execute()` collects errors in `ErrorReporter` but does NOT write them anywhere. The interpreter entry-point (`flow-interpreter/Program.cs:78`) formats and writes errors to `Console.Error` after Execute returns false. The Theory harness needed stderr content to assert against.
- **Fix:** Added `FlushErrorsToStderr()` to FlowEngineRunner that calls `_engine.ErrorReporter.FormatErrors()` and writes to `_stderr` after each RunFile/RunSource invocation — mirrors flow-interpreter's behavior.
- **Files modified:** flow-lang.Tests/Fixtures/FlowEngineRunner.cs
- **Commit:** `6868f9e`

**4. [Rule 3 - Blocking] Relative-path scripts fail when CWD is the xUnit bin directory**
- **Found during:** Task 2 (test_wav_loading.flow failed with "Could not find tests/test_output_roundtrip.wav")
- **Issue:** Several scripts write/read via relative paths (`tests/test_output_roundtrip.wav`, `tests/output/test_full_song.wav`) that resolve against the process CWD. Under xUnit, CWD is `flow-lang.Tests/bin/Debug/net10.0/` — not the repo root — so these paths don't resolve.
- **Fix:** Inside the Theory, wrap the invocation in a try/finally that sets `Environment.CurrentDirectory` to the repo root (parent of `tests/`) and restores it afterward. This matches the `dotnet run --project flow-interpreter tests/foo.flow` invocation pattern the scripts were authored against.
- **Files modified:** flow-lang.Tests/FlowScriptTests.cs
- **Commit:** `6868f9e`

**5. [Rule 2 - Missing critical functionality] ExpectedErrorScripts under-populated for intentional-error scripts**
- **Found during:** Task 2 (test_iteration_guard.flow and spike/c2-return-value-short-circuit.flow failed with unexpected errorCount>0)
- **Issue:** These scripts intentionally emit errors as part of their regression-probe purpose, but they weren't in the plan's seed dictionary.
- **Fix:** Added two ExpectedErrorScripts entries: `test_iteration_guard.flow` → `"Iteration limit"`, `spike/c2-return-value-short-circuit.flow` → `"Function 'nonExistentFn' not found"`.
- **Files modified:** flow-lang.Tests/FlowScriptData.cs
- **Commit:** `6868f9e`

**6. [Rule 2 - Pre-fix baseline] test_custom_oscillator and test_full_song registered as known-broken**
- **Found during:** Task 2 (planner had called these out as the targets of plan 12-05)
- **Issue:** Per CONTEXT D-01 / D-02, these two scripts are genuinely broken pre-fix (missing `if(Bool,String,String)` overload + missing auto-mkdir in `exportWav`). Without ExpectedErrorScripts entries the Theory rows would fail, but per plan 12-01 success criteria only spike/c1 should be RED.
- **Fix:** Added pre-fix-baseline entries with a comment noting plan 12-05 will remove them. The Theory row now asserts the known-broken surface explicitly.
- **Files modified:** flow-lang.Tests/FlowScriptData.cs
- **Commit:** `6868f9e`

### Unchanged from plan

- xunit.v3 3.2.2 primary framework package (accepted, no v2 fallback needed for the test framework itself — only the vsix runner needed substitution)
- CollectionDefinition serialization pattern per D-09
- spike/c1 RED-by-design via RequiredSentinels dict per D-11
- Commit boundaries (2 atomic commits) per plan `<commits>`

## Key Links Established

- `flow-lang.Tests/FlowScriptTests.cs` → `flow-lang.Tests/Fixtures/FlowEngineRunner.cs` via `new FlowEngineRunner()` inside each Theory row
- `flow-lang.Tests/FlowScriptTests.cs` → `flow-lang.Tests/FlowScriptData.cs` via `[MemberData(nameof(FlowScriptData.GetFlowScripts), MemberType = typeof(FlowScriptData))]`
- `flow-lang.Tests/Fixtures/FlowEngineRunner.cs` → `flow-lang/Core/FlowEngine.cs` via `new FlowEngine(verbose)`
- `flow-sharp.sln` → `flow-lang.Tests/flow-lang.Tests.csproj` via `dotnet sln add`

## Commits

| Task | Description | Commit |
|------|-------------|--------|
| 1 | Scaffold xUnit project + fixture + solution wiring | `8ab4694` |
| 2 | Wrap-as-Theory harness + data provider | `6868f9e` |

## Known Issues

- `spike/c1-musical-context-body.flow` is RED by design — this is the intended state per D-11. Plan 12-04 ships the FIX-07a returns→breaks edit which flips the sentinels on. Do NOT "fix" the RED state by mutating the assertion — bisectability requires the green-flip to be pinned to the 12-04 commit.
- 5 pre-existing C# compiler warnings (nullability) in flow-lang remain. Out of scope for this plan.
- NuGet advisory NU1903 on Tmds.DBus.Protocol 0.21.2 transitive dep in flow-editor. Out of scope.

## Downstream Dependencies

- Plan 12-02 (FIX-05): Will add `CollectionsTests.cs` to flow-lang.Tests/Unit/
- Plan 12-03 (FIX-06): Will add `ThunkTests.cs` to flow-lang.Tests/Unit/
- Plan 12-04 (FIX-07a): Will flip spike/c1 Theory row RED→GREEN by editing Interpreter.cs; adds `InterpreterTests.cs` (aka `ExecuteMusicalContextTests`)
- Plan 12-05: Will remove `test_custom_oscillator.flow` and `test_full_song.flow` entries from ExpectedErrorScripts after the underlying fixes land

## Self-Check: PASSED

Verified files and commits exist:

```
FOUND: flow-lang.Tests/flow-lang.Tests.csproj
FOUND: flow-lang.Tests/Fixtures/FlowEngineRunner.cs
FOUND: flow-lang.Tests/FlowScriptTests.cs
FOUND: flow-lang.Tests/FlowScriptData.cs
FOUND: flow-sharp.sln (modified to include flow-lang.Tests)
FOUND: commit 8ab4694 (Task 1)
FOUND: commit 6868f9e (Task 2)
```
