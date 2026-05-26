---
phase: 47-compile-target-flavors
plan: 04
subsystem: test
tags:
  - phase-47
  - wave-4
  - test-attribute
  - drywetmidi-wasm-compat
  - compile-target
  - web-build
requirements: [REQ-WEB-TARGET-04, REQ-WEB-TARGET-08]
dependency-graph:
  requires:
    - "Plan 47-01 (FLOW_WEB define + <Compile Remove> strip-list)"
    - "Plan 47-03 (FlowEngine.IsWebTarget + Parser/ModuleLoader gates + WebTargetGuardTests Desktop counterparts)"
  provides:
    - "FlowTargetFactAttribute xUnit subclass at flow-lang.Tests/Helpers/ — params string[] targets, #if FLOW_WEB-driven Skip"
    - "DryWetMidiWasmCompatTests — 2 [FlowTargetFact(\"Desktop\", \"Web\")] Facts pinning MidiFile.Write/Read + assembly-load round trip; Desktop 2/2 GREEN"
    - "WebTargetParserTests — 3 [FlowTargetFact(\"Web\")] Facts mirroring Plan 47-03 Desktop fixture (IsWebTarget=true / SupportsLiveBlocks=false / live block parse fails)"
    - "WebTargetModuleLoaderTests — 3 [FlowTargetFact(\"Web\")] Facts pinning @sfz + @osc charitable advisory + negative @notation-io non-strip"
  affects:
    - "Plan 47-05 (Mono.Cecil AssemblyReferenceScanTests) — FlowTargetFact attribute available for [FlowTargetFact(\"Web\")] gating of the reflective scan"
    - "Plan 47-06 (closer) — Phase 47 VERIFICATION sweep + Sfz/Osc-referencing test file tag sweep (18 files identified below; Plan 47-04 deliberately deferred per plan body)"
tech-stack:
  added: []
  patterns:
    - "xUnit FactAttribute subclass with #if FLOW_WEB-driven Skip property (no new package — Xunit.FactAttribute already referenced via xunit.v3 3.2.2)"
    - "DryWetMidi NoteOnEvent/NoteOffEvent constructor with SevenBitNumber positional args matches MidiExport.cs:466 convention (not the property-init shape from the plan body)"
    - "stderr capture via Console.SetError + StringWriter for ModuleLoader advisory assertion (mirrors WebTargetGuardTests.UseSfzImport_NoTargetAdvisory_OnDesktop)"
    - "Negative non-strip assertion for @notation-io: catches drift if IsStrippedOnWeb ever false-positive-flags BCL-only modules"
key-files:
  created:
    - "flow-lang.Tests/Helpers/FlowTargetFactAttribute.cs"
    - "flow-lang.Tests/Integration/Phase47/DryWetMidiWasmCompatTests.cs"
    - "flow-lang.Tests/Integration/Phase47/WebTargetParserTests.cs"
    - "flow-lang.Tests/Integration/Phase47/WebTargetModuleLoaderTests.cs"
  modified: []
decisions:
  - "D-47-13 honored: FlowTargetFactAttribute params string[] surface + #if FLOW_WEB ternary + Skip = \"Skipped on {current} — runs under: {targets}\". CurrentTarget exposed as public const (>= 2-occurrence verify gate from plan body)."
  - "D-47-04 + forward-look D-48-04 PARTIALLY resolved: DryWetMidi 8.0.3 confirmed working under FlowTarget=Desktop (2/2 smoke Facts GREEN). Web execution path is conditioned on the test project compiling under FlowTarget=Web — at Plan 47-04 close, that build cascade-fails on 50 errors in 18 Sfz/Osc-referencing test files (deferred to Plan 47-06 closer's tag sweep per plan body)."
  - "Test class Web-buildability documented: dotnet build flow-lang.Tests -p:FlowTarget=Web exits NON-ZERO with 50 compile errors in 18 files. flow-lang.dll Web build remains 0-error from Plan 47-03 (load-bearing acceptance gate preserved)."
  - "DryWetMidi NoteOnEvent/NoteOffEvent API shape corrected from plan body's property-init form to MidiExport.cs:466 positional-constructor form (NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)100)). The property-init shape in the plan body has Velocity uninitialized on NoteOnEvent which fails compile in DryWetMidi 8.0.3."
  - "MidiFile.Write format argument supplied via NAMED argument (`format: MidiFileFormat.SingleTrack`) per DryWetMidi 8.0.3 API — the format parameter is not the first positional after path."
metrics:
  duration: ~14min
  completed: 2026-05-26
  tasks_completed: 4
  files_created: 4
  files_modified: 0
  commits: 4
---

# Phase 47 Plan 04: FlowTargetFact Attribute + Web-Side Test Coverage Summary

## One-liner

Lands the `FlowTargetFactAttribute` xUnit subclass + 3 Phase 47 test files (DryWetMidi WASM-compat smoke + Web-side Parser live-block gate + Web-side ModuleLoader stripped-module gate) — `dotnet test` Desktop run now executes 16 Phase47 Facts GREEN and skips 6 Web-only Facts with documented `Skipped on Desktop — test runs under: Web` reason; DryWetMidi 8.0.3 confirmed working under Desktop (2/2 smoke Facts GREEN) with Web execution deferred to Plan 47-06 closer's Sfz/Osc test-file tag sweep.

## What Was Done

### Task 1 — FlowTargetFactAttribute helper (commit `8adc89c`)

Created `flow-lang.Tests/Helpers/FlowTargetFactAttribute.cs` (50 LOC) per D-47-13. Subclass of `Xunit.FactAttribute` with:

- `params string[] targets` constructor accepting one or more of `"Desktop"` / `"Web"`.
- `public const string CurrentTarget` resolved via `#if FLOW_WEB` → `"Web"` else `"Desktop"`. Constant-folded by the C# compiler so callers can branch on it without runtime cost.
- Defensive `ArgumentException` on empty target list.
- Sets inherited `Skip` property to `"Skipped on {CurrentTarget} — test runs under: {string.Join(\", \", targets)}"` when `CurrentTarget` is not in the supplied target list.

Source assertion verification:

```text
grep -c "public sealed class FlowTargetFactAttribute" → 1 ✓
grep -c "#if FLOW_WEB" → 1 ✓
grep -c "CurrentTarget" → 3 ✓ (≥ 2 required by plan)
```

Build verification:

- `dotnet build flow-lang.Tests -p:FlowTarget=Desktop` (default) → exit 0 ✓ (82 warnings, 0 errors)
- `dotnet build flow-lang.Tests -p:FlowTarget=Web` → exit NON-ZERO (50 errors in 18 files — documented as known-limitation; plan acceptance criteria explicitly permit this path failing on test-project cascade)

The 18 test files that cascade-fail under FlowTarget=Web (deferred to Plan 47-06 tag sweep):

| Directory | Files |
|-----------|-------|
| `Integration/Phase33/` | `SfzArticulationTests.cs` |
| `Integration/Phase37/` | `DrumPitchShiftAutoTests.cs`, `SfzDrumsLoadTest.cs`, `SfzHardSwitchRegression.cs`, `SfzPanCompositionTests.cs`, `SfzPanRetrofitTests.cs`, `SfzRoundRobinDeterminismTests.cs`, `SfzRoundRobinTests.cs`, `SfzVelocityCrossfadeTests.cs` |
| `Integration/Phase38/` | `OscBundleDepthCapTests.cs`, `OscBundleTests.cs`, `OscLoopbackTests.cs`, `OscRateLimitTests.cs`, `OscTypeTagInferenceTests.cs` |
| `Unit/Phase33/` | `SfzLoopCrossfadeTests.cs`, `SfzParserTests.cs`, `SfzRegionMatchTests.cs`, `SfzTypeFacts.cs` |

All 18 reference stripped Sfz* / Rug.Osc / OSC types directly — they need either `[FlowTargetFact("Desktop")]` tags wrapping every Fact in those files OR file-level `#if !FLOW_WEB` exclusion. Plan 47-04's plan body explicitly defers this to the Plan 47-06 closer's "tracking-file sweep where the test-suite-green check on both targets surfaces any miss."

### Task 2 — DryWetMidi WASM-compat smoke test (commit `f51e58d`)

Created `flow-lang.Tests/Integration/Phase47/DryWetMidiWasmCompatTests.cs` (79 LOC). Two `[FlowTargetFact("Desktop", "Web")]` methods:

| Fact | Asserts |
|------|---------|
| `MidiFile_WriteAndRead_RoundTripsMinimalSmf` | Build minimal MIDI file (header + `SetTempoEvent(500_000)` 120 BPM + `NoteOnEvent` C4@100 + `NoteOffEvent` C4@480-ticks-later) → `MidiFile.Write(tmpPath, format: MidiFileFormat.SingleTrack)` → file exists + non-empty → `MidiFile.Read(tmpPath)` → not-null + `GetTrackChunks()` not-empty. Temp file cleanup in `finally` |
| `DryWetMidiAssembly_IsLoadable` | `typeof(MidiFile).Assembly` resolves + `.GetName().Name == "Melanchall.DryWetMidi"`. Type-load failure under FlowTarget=Web would throw `TypeLoadException` here |

**API correction (deviation from plan body):** The plan body's property-init shape (`new NoteOnEvent { Channel = ..., NoteNumber = ..., Velocity = ... }`) fails to compile under DryWetMidi 8.0.3 because `NoteOnEvent`/`NoteOffEvent` don't expose all those properties as settable. Adopted the existing `MidiExport.cs:466` positional-constructor convention:

```csharp
new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)100) { Channel = (FourBitNumber)0 }
new NoteOffEvent((SevenBitNumber)60, (SevenBitNumber)0) { Channel = (FourBitNumber)0, DeltaTime = 480 }
```

`MidiFile.Write` format argument also supplied via named arg `format: MidiFileFormat.SingleTrack`.

Desktop run:

```text
dotnet test --filter "FullyQualifiedName~DryWetMidiWasmCompatTests"
Passed!  - Failed: 0, Passed: 2, Skipped: 0, Total: 2, Duration: 38 ms
```

**Web run status (forward-look for Plan 47-06):** The test project does not currently compile under FlowTarget=Web due to the 18 cascade-failing Sfz/Osc files. Once Plan 47-06 closes that gap, these 2 Facts execute on Web and the result determines whether `writeMidi` ships in the Web build (PASS) or becomes a parse error per D-48-04 (FAIL). Plan 47-04's deliverable is the *test that resolves the question*, not the resolution itself.

### Task 3 — WebTargetParserTests (commit `92b022a`)

Created `flow-lang.Tests/Integration/Phase47/WebTargetParserTests.cs` (58 LOC). Three `[FlowTargetFact("Web")]` methods mirroring Plan 47-03's Desktop counterparts:

| Fact | Asserts on Web |
|------|---------------|
| `IsWebTarget_IsTrue_OnWebBuild` | `FlowEngine.IsWebTarget == true` (mirror of `WebTargetGuardTests.IsWebTarget_IsFalse_OnDesktopBuild`) |
| `SupportsLiveBlocks_IsFalse_OnWebBuild` | `FlowEngine.SupportsLiveBlocks == false` (mirror of Desktop counterpart) |
| `LiveBlock_FailsToParse_OnWeb_WithCharitableDiagnostic` | `engine.Execute("live 1bar { (print 1); }", "<test>")` returns false (the Plan 47-03 Task 4 Rust-style ParseException routes through ErrorReporter, surfacing as Execute=false) |

Desktop run (all 3 should SKIP):

```text
dotnet test --filter "FullyQualifiedName~WebTargetParserTests"
Skipped! - Failed: 0, Passed: 0, Skipped: 3, Total: 3, Duration: 8 ms
```

Skip reasons (verified at runtime):

- `Skipped on Desktop — test runs under: Web`

### Task 4 — WebTargetModuleLoaderTests (commit `a3d8537`)

Created `flow-lang.Tests/Integration/Phase47/WebTargetModuleLoaderTests.cs` (93 LOC). Three `[FlowTargetFact("Web")]` methods (2 positive + 1 negative):

| Fact | Asserts on Web |
|------|---------------|
| `UseSfzImport_EmitsCharitableAdvisory_OnWeb` | `use "@sfz";` → `Execute=false` AND stderr contains `"[target] module '@sfz' unavailable on Web target"` (Plan 47-03 Task 3 ModuleLoader gate) |
| `UseOscImport_EmitsCharitableAdvisory_OnWeb` | Symmetric for `@osc` |
| `UseNotationIoImport_Succeeds_OnWeb` | NEGATIVE — `use "@notation-io";` succeeds AND stderr does NOT contain `"[target] module '@notation-io' unavailable"`. Verifies the strip list is correctly SCOPED — XmlWriter-based MusicXML/LilyPond/ABC/MML stay on Web |

Stderr capture pattern mirrors `WebTargetGuardTests.UseSfzImport_NoTargetAdvisory_OnDesktop` from Plan 47-03 Task 5 (Console.SetError → StringWriter → restore in finally).

Desktop run (all 3 SKIP):

```text
dotnet test --filter "FullyQualifiedName~WebTargetModuleLoaderTests"
Skipped! - Failed: 0, Passed: 0, Skipped: 3, Total: 3, Duration: 8 ms
```

## Acceptance Verification

### Source-grep assertions

| Assertion | Expected | Actual |
|-----------|----------|--------|
| `grep -c "public sealed class FlowTargetFactAttribute" Helpers/FlowTargetFactAttribute.cs` | 1 | **1** ✓ |
| `grep -c "#if FLOW_WEB" Helpers/FlowTargetFactAttribute.cs` | 1 | **1** ✓ |
| `grep -c "CurrentTarget" Helpers/FlowTargetFactAttribute.cs` | ≥ 2 | **3** ✓ |
| `grep -c "FlowTargetFact" DryWetMidiWasmCompatTests.cs` | ≥ 2 | **3** ✓ (using + 2 attrs) |
| `test -f WebTargetParserTests.cs` | exists | **exists** ✓ |
| `test -f WebTargetModuleLoaderTests.cs` | exists | **exists** ✓ |

### Build assertions

| Build invocation | Expected | Actual |
|------------------|----------|--------|
| `dotnet build flow-lang.Tests` (Desktop default) | exit 0 | **exit 0** ✓ |
| `dotnet build flow-lang.Tests -p:FlowTarget=Web` | exit 0 OR documented cascade-fail | **exit NON-ZERO — 50 errors in 18 Sfz/Osc files, documented** ✓ |
| `dotnet build flow-lang -p:FlowTarget=Web` (Plan 47-03 acceptance) | exit 0 | **exit 0** ✓ (preserved) |

### xUnit fixture results

| Fixture | Result |
|---------|--------|
| `DryWetMidiWasmCompatTests` (Desktop) | **2/2 GREEN** ✓ |
| `WebTargetParserTests` (Desktop) | **3/3 SKIPPED** ✓ (correct skip behavior) |
| `WebTargetModuleLoaderTests` (Desktop) | **3/3 SKIPPED** ✓ (correct skip behavior) |
| Phase47 fixture (all 22 facts) | **16 PASSED, 6 SKIPPED, 0 FAILED** ✓ |
| `WebTargetGuardTests` (Plan 47-03) | **4/4 GREEN** ✓ (no regression) |
| `BuildConditioningSmokeTests` (Plan 47-01) | **3/3 GREEN** ✓ (no regression) |
| `WebAudioBackendStubTests` (Plan 47-02) | **7/7 GREEN** ✓ (no regression) |
| Full Desktop suite | **2127 PASSED, 7 SKIPPED, 0 FAILED** ✓ (Plan 47-03 baseline was 2125 + 2 from this plan's DryWetMidi smoke = 2127) |

## Deviations from Plan

### Rule 1 auto-fix — DryWetMidi NoteOnEvent/NoteOffEvent API shape

**Trigger:** Plan body code sample at Task 2 used property-initializer syntax `new NoteOnEvent { Channel = ..., NoteNumber = ..., Velocity = ... }`. Under DryWetMidi 8.0.3, `NoteOnEvent` exposes `NoteNumber` + `Velocity` but the property-init shape with all three properties does not compile (Velocity is constructor-set, the property-init shape leaves it uninitialized which would throw at write time).

**Issue:** Code as written in plan body would fail compile on Desktop.

**Fix:** Adopted the existing convention from `flow-lang/StandardLibrary/Audio/MidiExport.cs:466` — positional constructor `new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)100)` with `Channel` set via property init only. Same for `NoteOffEvent`. Also added `format:` named argument to `MidiFile.Write` per the DryWetMidi 8.0.3 method signature.

**Files modified:** Task 2's own file (`DryWetMidiWasmCompatTests.cs`) — not a separate fix.

**Commit:** `f51e58d` (Task 2 commit itself).

### Documented limitation — test project Web-buildability

Per plan body's acceptance criteria explicit phrasing: "either exits 0 OR fails on flow-lsp/flow-midi/flow-interpreter cascade (documented as known-limitation — Plan 47-04 only requires flow-lang.dll Web build to succeed, not the test project)."

**Outcome:** Test project build under FlowTarget=Web fails with 50 errors in 18 Sfz/Osc-referencing files. flow-lang.dll Web build remains exit 0 (Plan 47-03 acceptance gate preserved). The 18-file deferral is documented in the Task 1 commit message + this Summary for Plan 47-06's closer tag sweep.

This is NOT a Rule 1/2/3/4 deviation — the plan body explicitly anticipates and permits it.

### No Rule 2 / Rule 3 / Rule 4 escalations

No missing-functionality additions (Rule 2 not triggered — defensive `ArgumentException` was already in the plan code sample). No blocking issues that needed package installs (Rule 3 not triggered). No architectural escalations (Rule 4 not triggered).

## Authentication Gates

None.

## Decisions Made

- **Honored D-47-13** — FlowTargetFactAttribute with `params string[] targets` + `#if FLOW_WEB` ternary + descriptive Skip reason. `CurrentTarget` exposed as `public const string` for downstream test classes (`grep -c CurrentTarget` returns 3, satisfying plan's ≥ 2 verify gate).
- **Honored D-47-04 + forward-look D-48-04** — DryWetMidi smoke test ships with cross-target `[FlowTargetFact("Desktop", "Web")]` annotation. Desktop verifies DryWetMidi APIs reachable + working (2/2 GREEN). Web execution is conditioned on Plan 47-06's tag sweep landing first.
- **Rule 1 deviation honored** — DryWetMidi NoteOnEvent/NoteOffEvent API shape corrected from plan body's property-init form to MidiExport.cs:466 positional-constructor form (the plan body's shape would fail compile).
- **Plan body's "deferred to Plan 47-06" framing honored** — Did NOT exhaustively re-tag existing Phase 33/Phase 37/Phase 38 test files with `[FlowTargetFact("Desktop")]`. Plan 47-04's plan body explicitly defers this. Plan 47-06 closer sweep tackles the 18-file list documented above.

## Threat Flags

None. Per Plan 47-04 threat register: no new attack surface introduced. Test-attribute + smoke tests, no new runtime input perimeter. DryWetMidi 8.0.3 already referenced from Phase 1; no new package addition.

## Known Stubs

None — Plan 47-04 is mechanically complete for its Desktop-side deliverable. The Web-side execution of the 6 `[FlowTargetFact("Web")]` Facts is deferred to Plan 47-06's tag sweep that fixes the 18-file cascade-fail under FlowTarget=Web — tracked as a load-bearing Plan 47-06 closer task, not a stub.

## Files Touched

```text
flow-lang.Tests/Helpers/FlowTargetFactAttribute.cs                                  (NEW, 50 LOC)
flow-lang.Tests/Integration/Phase47/DryWetMidiWasmCompatTests.cs                    (NEW, 79 LOC)
flow-lang.Tests/Integration/Phase47/WebTargetParserTests.cs                         (NEW, 58 LOC)
flow-lang.Tests/Integration/Phase47/WebTargetModuleLoaderTests.cs                   (NEW, 93 LOC)
```

## Commits

| Hash | Type | Description |
|------|------|-------------|
| `8adc89c` | feat | wire FlowTargetFactAttribute helper for compile-target test discrimination |
| `f51e58d` | test | pin DryWetMidi 8.0.3 WASM-compat smoke (2 cross-target Facts) |
| `92b022a` | test | pin Web-side Parser live-block gate (3 Web-only Facts) |
| `a3d8537` | test | pin Web-side ModuleLoader @sfz/@osc gate + @notation-io non-strip (3 Web-only Facts) |

## Self-Check: PASSED

- File `flow-lang.Tests/Helpers/FlowTargetFactAttribute.cs` exists with `FlowTargetFactAttribute` + `#if FLOW_WEB` + `CurrentTarget` ✓
- File `flow-lang.Tests/Integration/Phase47/DryWetMidiWasmCompatTests.cs` exists with 2 `[FlowTargetFact("Desktop", "Web")]` methods ✓
- File `flow-lang.Tests/Integration/Phase47/WebTargetParserTests.cs` exists with 3 `[FlowTargetFact("Web")]` methods ✓
- File `flow-lang.Tests/Integration/Phase47/WebTargetModuleLoaderTests.cs` exists with 3 `[FlowTargetFact("Web")]` methods ✓
- Commit `8adc89c` present in `git log` ✓
- Commit `f51e58d` present in `git log` ✓
- Commit `92b022a` present in `git log` ✓
- Commit `a3d8537` present in `git log` ✓
- `dotnet build flow-lang.Tests -p:FlowTarget=Desktop` exits 0 ✓
- `dotnet build flow-lang.Tests -p:FlowTarget=Web` documented as cascade-fail on 18 Sfz/Osc files (per plan acceptance criteria — flow-lang.dll Web build itself still exit 0) ✓
- `DryWetMidiWasmCompatTests` 2/2 GREEN on Desktop ✓
- `WebTargetParserTests` 3/3 SKIPPED on Desktop with documented reason ✓
- `WebTargetModuleLoaderTests` 3/3 SKIPPED on Desktop with documented reason ✓
- `WebTargetGuardTests` 4/4 GREEN (Plan 47-03 no regression) ✓
- `BuildConditioningSmokeTests` 3/3 GREEN (Plan 47-01 no regression) ✓
- `WebAudioBackendStubTests` 7/7 GREEN (Plan 47-02 no regression) ✓
- Full Desktop test suite: 2127 PASSED, 7 SKIPPED, 0 FAILED (+2 vs Plan 47-03 baseline 2125, attributable to DryWetMidi smoke on Desktop) ✓
