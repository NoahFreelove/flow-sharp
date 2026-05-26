---
phase: 47-compile-target-flavors
plan: 01
subsystem: build
tags:
  - phase-47
  - wave-1
  - foundation
  - msbuild
  - compile-target
requirements: [REQ-WEB-TARGET-01, REQ-WEB-TARGET-02, REQ-WEB-TARGET-03]
dependency-graph:
  requires: []
  provides:
    - "FlowTarget=Desktop|Web MSBuild property"
    - "FLOW_WEB preprocessor symbol"
    - "Web-conditional <Compile Remove> strip list (7 source paths)"
    - "Web-conditional Samples/** + sfz.flow + osc.flow publish-skip"
    - "Rug.Osc PackageReference gated to FlowTarget != Web"
    - "BuildConditioningSmokeTests xUnit fixture (3 Facts)"
  affects:
    - "Plan 47-02 (WebAudioBackend stub) — consumes FLOW_WEB define + IAudioBackend strip"
    - "Plan 47-03 (FlowEngine + BuiltInFunctions #if !FLOW_WEB guards) — closes 13 Web-build compile errors at FlowEngine.cs / SongRenderer.cs / TestSnapshot.cs / ExecutionContext.cs / Value.cs consuming Sfz* + Network/Osc*"
    - "Plan 47-04 (DryWetMidi WASM-compat + [FlowTargetFact] sweep) — consumes FLOW_WEB define"
    - "Plan 47-05 (Mono.Cecil AssemblyReferenceScanTests) — consumes Web-built flow-lang.dll"
    - "Plan 47-06 (closer) — flips WebBuild_ExitCodeIsZero Fact to GREEN"
tech-stack:
  added: []
  patterns:
    - "MSBuild conditional ItemGroup (<ItemGroup Condition='...'>) — first instance in this repo"
    - "Asymmetric preprocessor symbol (FLOW_WEB only; absence implies Desktop) — matches .NET ecosystem convention (NETSTANDARD2_0, etc.)"
    - "Nested dotnet build via Process.Start in xUnit Fact for build-conditioning acceptance"
key-files:
  created:
    - "flow-lang.Tests/Integration/Phase47/BuildConditioningSmokeTests.cs"
  modified:
    - "flow-lang/flow-lang.csproj"
decisions:
  - "D-47-01 honored: single source of truth in flow-lang.csproj — no separate flow-lang.Web.csproj"
  - "D-47-02 honored: FLOW_WEB preprocessor symbol; absence implies Desktop (asymmetric)"
  - "D-47-03 honored: Conditional ItemGroup with <Compile Remove> runs at MSBuild eval time before C# compile"
  - "D-47-11 honored: Samples/** belt-and-suspenders strip via <Content Remove> + <None Remove>"
  - "PATTERNS.md §Discrepancies 1, 4, 5 honored: actual file paths over CONTEXT.md prose (no Osc/ subdir, no Live/ subdir, Live cluster STAYS — strip would cascade-break Parser/Interpreter/ExecutionContext)"
metrics:
  duration: 12min
  completed: 2026-05-26
  tasks_completed: 2
  files_created: 1
  files_modified: 1
  commits: 2
---

# Phase 47 Plan 01: MSBuild Compile-Target Conditioning Foundation Summary

## One-liner

`FlowTarget=Desktop|Web` MSBuild property + Web-conditional `<Compile Remove>` strip list (7 source files + Samples/** + 2 .flow files) + Rug.Osc-when-not-Web gate, pinned by 3-Fact xUnit smoke fixture that shells out to `dotnet build` and asserts exit codes.

## What Was Done

### Task 1 — flow-lang.csproj structural edit (commit `635cbda`)

Four coordinated additions to `flow-lang/flow-lang.csproj`:

1. **`<FlowTarget>Desktop</FlowTarget>`** appended to the existing top-level `<PropertyGroup>` (line 8). Single source of truth per D-47-01; defaults to Desktop so no-flag `dotnet build` preserves byte-identical behavior.

2. **Web-conditional `<PropertyGroup>`** appending `;FLOW_WEB` to `$(DefineConstants)` when `'$(FlowTarget)' == 'Web'`. Asymmetric — no `FLOW_DESKTOP` constant, matches .NET ecosystem convention (`NETSTANDARD2_0` family).

3. **Web-conditional `<ItemGroup>`** with 7 `<Compile Remove>` entries strictly reconciled to actual codebase paths via 47-PATTERNS.md §Discrepancies:

   | Stripped File | Reason |
   |---|---|
   | `Audio\PulseAudioSimpleBackend.cs` | `[DllImport("libpulse-simple")]` — unavailable in browser |
   | `Audio\PulseAudioCaptureBackend.cs` | same; Phase 38 micBuffer capture path |
   | `Audio\CoreAudioBackend.cs` | `[DllImport("AudioToolbox")]` — unavailable in browser |
   | `StandardLibrary\Audio\Sfz\**\*.cs` | 8-file glob (SfzBuiltins/SfzData/SfzLoopMode/SfzParseException/SfzParser/SfzRegion/SfzRenderer/SfzSampleCache) — external sample dep, browser sandbox prevents arbitrary file loads |
   | `StandardLibrary\Network\OscFunctions.cs` | Raw UDP unavailable; WebRTC DataChannel is v1.6+ |
   | `StandardLibrary\Network\OscHandleData.cs` | OSC handle value type — orphaned without OscFunctions |
   | `StandardLibrary\Audio\InputFunctions.cs` | `(micBuffer)` builtin — bound to PulseAudio capture P/Invoke |

   Plus `<Content Remove="Samples\**" />` + `<None Remove="Samples\**" />` per D-47-11 (belt-and-suspenders against MSBuild SDK implicit-include behavior on the 3.05 MB U-Iowa MIS bundle), and `<None Remove="sfz.flow" />` + `<None Remove="osc.flow" />` to skip Web publish of stripped stdlib `.flow` files (their CopyToOutputDirectory directives would otherwise ship them despite their backing builtins being absent).

4. **Rug.Osc PackageReference** moved out of the unconditional `<ItemGroup>` into a new `'$(FlowTarget)' != 'Web'` conditional. OscFunctions.cs + OscHandleData.cs are stripped on Web, so Rug.Osc would be a dead reference otherwise. DryWetMidi + Pidgin remain unconditional (DryWetMidi WASM-compat verified in Plan 47-04; Pidgin is reference-only per CLAUDE.md).

**Honored cascade-break analysis from 47-PATTERNS.md §"DO NOT STRIP":**
`Ast/Statements/LiveBlockStatement.cs`, `Runtime/LiveBlockRegistry.cs`, `Interpreter/LambdaCaptureAuditor.cs` STAY in the Web build (their consumers at `Parser.cs:221` / `Interpreter.cs:133` / `ExecutionContext.cs:292` cannot be stripped without breaking the core interpreter). Plan 47-03 substitutes a parse-time `throw new ParseException` at the `Match(TokenType.Live)` dispatch instead.

### Task 2 — BuildConditioningSmokeTests xUnit fixture (commit `883c894`)

Created `flow-lang.Tests/Integration/Phase47/BuildConditioningSmokeTests.cs` (84 LOC) with 3 `[Fact]` methods that shell out to nested `dotnet build` invocations and assert exit code 0:

- **`DesktopBuild_ExitCodeIsZero`** — `-p:FlowTarget=Desktop` (explicit)
- **`DefaultBuild_ExitCodeIsZero_AndImpliesDesktop`** — no `-p` flag (default behavior per D-47-01)
- **`WebBuild_ExitCodeIsZero`** — `-p:FlowTarget=Web` (load-bearing for Phase 48 dependency)

`FindRepoRoot()` walks up from `AppContext.BaseDirectory` until `flow-lang/flow-lang.csproj` is found, so the fixture is cwd-independent (works from `dotnet test` invocations at any directory). 120-second timeout covers a cold restore. Plain `[Fact]` — these validate the build conditioning itself, not a Web-target runtime feature; `[FlowTargetFact]` lands in Plan 47-04.

## Acceptance Verification

### Source grep assertions (all from Task 1's acceptance_criteria block)

| Assertion | Expected | Actual |
|---|---|---|
| `grep -c '<FlowTarget>Desktop</FlowTarget>'` | 1 | **1** ✓ |
| `grep -c "Condition=...FlowTarget.. == .Web."` | ≥ 2 | **2** ✓ |
| `grep "FLOW_WEB"` matches `$(DefineConstants);FLOW_WEB` | yes | **yes** ✓ |
| `grep -c 'Compile Remove'` | 7 | **7** ✓ |
| `grep -c 'None Remove="Samples'` | 1 | **1** ✓ |
| `grep -c 'None Remove="sfz.flow"'` | 1 | **1** ✓ |
| `grep -c 'None Remove="osc.flow"'` | 1 | **1** ✓ |

### Build assertions

| Build invocation | Expected | Actual |
|---|---|---|
| `dotnet build flow-lang -p:FlowTarget=Desktop` | exit 0 | **exit 0** ✓ |
| `dotnet build flow-lang` (no flag) | exit 0 | **exit 0** ✓ |
| `dotnet build flow-lang -p:FlowTarget=Web` | exit 0 OR fail only on stripped-symbol refs | **fails with 13 errors, ALL on stripped symbols** ✓ (deferred to Plans 47-02 + 47-03) |

### xUnit fixture results

| Fact | Result |
|---|---|
| `DesktopBuild_ExitCodeIsZero` | **GREEN** ✓ (verified 2026-05-26, 910ms) |
| `DefaultBuild_ExitCodeIsZero_AndImpliesDesktop` | **GREEN** ✓ (verified 2026-05-26, 934ms) |
| `WebBuild_ExitCodeIsZero` | **expected RED until Plan 47-03 closes** ✓ (documented in inline test comment + acceptance criteria) |

## Web Build Errors (Deferred to Plans 47-02/47-03)

`dotnet build -p:FlowTarget=Web` currently fails with 13 compile errors. All are CS0234/CS0246 references to symbols from stripped files, classified per acceptance criteria as "must list ONLY symbols from stripped files":

| Consuming Site | Missing Symbol | Resolution Plan |
|---|---|---|
| `Core/FlowEngine.cs:9` | `FlowLang.StandardLibrary.Audio.Sfz` namespace | Plan 47-03 (#if !FLOW_WEB guard) |
| `Core/FlowEngine.cs:31,90` | `SfzSampleCache` field declaration | Plan 47-03 |
| `Runtime/ExecutionContext.cs:547` | `FlowLang.StandardLibrary.Audio.Sfz` | Plan 47-03 |
| `Runtime/Value.cs:71` | `FlowLang.StandardLibrary.Audio.Sfz` | Plan 47-03 |
| `Runtime/Value.cs:115` | `FlowLang.StandardLibrary.Network` namespace | Plan 47-03 |
| `StandardLibrary/Audio/SongRenderer.cs:6,687,688,699×2` | `SfzRenderer`, `SfzData` types | Plan 47-03 |
| `StandardLibrary/TestFramework/TestSnapshot.cs:4,60` | `SfzData` type | Plan 47-03 |

These are EXACTLY the symbols the strip-list targets — Plan 47-03 wraps each consuming call site in `#if !FLOW_WEB` to close them. No spurious unrelated errors surfaced — Plan 47-01's csproj edit is mechanically correct.

## Deviations from Plan

None — plan executed exactly as written. The Plan 47-01 author had already reconciled 47-PATTERNS.md §Discrepancies into the action steps, so the strip-list paths landed verbatim. No Rule 1/2/3 auto-fixes triggered. No Rule 4 architectural escalations.

## Decisions Made

- **Honored D-47-01 single-csproj posture** — `<FlowTarget>` lives in `flow-lang/flow-lang.csproj` PropertyGroup, not a sibling `.csproj`. Single source of truth; future Phase 41 cross-platform binaries can reuse the `Condition=` pattern.

- **Honored D-47-02 asymmetric symbol** — only `FLOW_WEB` is defined; Desktop implies absence. Matches the .NET ecosystem convention (`NETSTANDARD2_0` family). Avoids `FLOW_DESKTOP` definition churn at every guard site.

- **Honored cascade-break analysis from PATTERNS.md** — Live cluster (`LiveBlockStatement.cs`, `LiveBlockRegistry.cs`, `LambdaCaptureAuditor.cs`) NOT in strip-list. Their consumers in core interpreter (`Parser.cs:221`, `Interpreter.cs:133`, `ExecutionContext.cs:292`) would cascade-break the Web build. Plan 47-03 substitutes a parse-time `throw new ParseException` at the `Match(TokenType.Live)` dispatch — composer gets a Rust-style diagnostic on the source line.

- **Honored D-47-11 belt-and-suspenders Samples strip** — though no existing `<None Update="Samples/**" />` entry exists today, MSBuild SDK implicit-include behavior may pull `Samples/**` into the publish output. Explicit `<Content Remove>` + `<None Remove>` guarantee Web omits the 3.05 MB U-Iowa MIS bundle (~20% of WASM budget per Phase 48 D-48-03).

## Threat Flags

None — Phase 47 is a build-time refactor. Per Plan 47-01's `<threat_model>` block, no new attack surface introduced. Rug.Osc reference is MOVED (not added) to a conditional ItemGroup; no new supply-chain dependency.

## Known Stubs

None — Task 2's test fixture has 1 expected-RED fact (`WebBuild_ExitCodeIsZero`) which is not a stub but a forward-looking acceptance gate that flips GREEN once Plans 47-02 + 47-03 close. The inline `// Phase 47 D-47-01..03` comment explicitly documents this expectation.

## Files Touched

```
flow-lang/flow-lang.csproj                                    (+48 lines)
flow-lang.Tests/Integration/Phase47/BuildConditioningSmokeTests.cs  (NEW, 84 lines)
```

## Commits

| Hash | Type | Description |
|---|---|---|
| `635cbda` | build | Add FlowTarget=Desktop\|Web MSBuild conditioning to flow-lang.csproj |
| `883c894` | test | Pin FlowTarget=Desktop\|Web build modes via Process.Start exit-code Facts |

## Self-Check: PASSED

- File `flow-lang/flow-lang.csproj` exists and contains `<FlowTarget>Desktop</FlowTarget>` ✓
- File `flow-lang.Tests/Integration/Phase47/BuildConditioningSmokeTests.cs` exists ✓
- Commit `635cbda` present in `git log` ✓
- Commit `883c894` present in `git log` ✓
- `dotnet build flow-lang -p:FlowTarget=Desktop` exits 0 ✓
- `dotnet build flow-lang` (no flag) exits 0 ✓
- `dotnet build flow-lang -p:FlowTarget=Web` fails only on stripped-symbol refs (13 errors, all `Sfz*` / `Network.*`) ✓
- 2 of 3 xUnit Facts GREEN (`DesktopBuild` + `DefaultBuild`); `WebBuild` expected RED until Plan 47-03 ✓
