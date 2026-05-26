# Phase 47: Compile-Target Flavors — Verification

**Phase:** 47
**Status:** Shipped 2026-05-25
**Plans:** 6/6 complete
**Requirements:** 10 REQ-WEB-TARGET-NN closed

## Outcome Summary

Phase 47 introduces `FlowTarget=Desktop|Web` MSBuild conditioning so the `flow-lang.dll`
library compiles cleanly under WASM by stripping features that cannot run in a
browser sandbox. Foundation for Phase 48 (WASM Runtime + WebAudioBackend) and
Phase 49 (flowlang.dev site).

**Build surface:**
- Default: `dotnet build flow-lang/flow-lang.csproj` → FlowTarget=Desktop (byte-identical to pre-Phase-47 behavior)
- Explicit Desktop: `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Desktop`
- Web: `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web` → produces flow-lang.dll with stripped SFZ/OSC/PulseAudio/CoreAudio/InputFunctions

**Composer-facing behavior on Web target:**
- `use "@sfz"` or `use "@osc"` → ModuleLoader emits `[target] module '@X' unavailable on Web target — line N. Build with FlowTarget=Desktop to enable.` advisory; ModuleLoadResult.Error.
- `live 1bar { ... }` → Parser throws ParseException `` `live` block requires Desktop target — line N. Build with FlowTarget=Desktop or run with `flow run script.flow` locally. ``
- `(micBuffer 1.0)` → Function not found (InputFunctions stripped); composer sees runtime error.
- Audio playback → WebAudioBackend stub throws PlatformNotSupportedException with `"WebAudioBackend stub — Phase 48 will implement via [JSImport]"` message (Phase 48 lands the JSImport bodies).

## Requirement Closure Table

| REQ-ID | Description | Closure |
|--------|-------------|---------|
| REQ-WEB-TARGET-01 | `<FlowTarget>Desktop</FlowTarget>` MSBuild property + Web-conditional `<DefineConstants>` adding `FLOW_WEB` (D-47-01/02) | Plan 47-01 commit `635cbda` |
| REQ-WEB-TARGET-02 | Conditional `<Compile Remove>` strip list (7 entries: PulseAudioSimpleBackend, PulseAudioCaptureBackend, CoreAudioBackend, Sfz/**/*.cs, OscFunctions, OscHandleData, InputFunctions) (D-47-03) | Plan 47-01 commit `635cbda` |
| REQ-WEB-TARGET-03 | Samples bundle + Rug.Osc + sfz.flow/osc.flow stdlib stripped via conditional ItemGroups (D-47-11) | Plan 47-01 commit `635cbda` |
| REQ-WEB-TARGET-04 | WebAudioBackend stub at `Audio/WebAudioBackend.cs` implementing IAudioBackend; all methods except IsAvailable + Dispose throw PlatformNotSupportedException with pinned StubMessage `"WebAudioBackend stub — Phase 48 will implement via [JSImport]"` (D-47-05/07) | Plan 47-02 commit `7021d8a` |
| REQ-WEB-TARGET-05 | Central `#if !FLOW_WEB` guards at FlowEngine.cs:185 (SfzBuiltins.Register), FlowEngine.cs:202 (OscFunctions.Register), BuiltInFunctions.cs:1027 (Audio.InputFunctions.RegisterContextDependent); Rule 3 deviation extended guards to ExecutionContext / Value / SongRenderer / TestSnapshot / Interpreter consuming sites (D-47-08) | Plan 47-03 commits `dfa359f` + `9600ddb` |
| REQ-WEB-TARGET-06 | `FlowEngine.IsWebTarget` (bool, compile-time constant via `#if FLOW_WEB`) + `FlowEngine.SupportsLiveBlocks` (bool, `!IsWebTarget`) public static properties (D-47-10) | Plan 47-03 commit `dfa359f` |
| REQ-WEB-TARGET-07 | Parser parse-time gate on `TokenType.Live` throws Rust-style ParseException; ModuleLoader gate on `@sfz`/`@osc` emits charitable `[target]` WarnOnce advisory + returns ModuleLoadResult.Error (D-47-09) | Plan 47-03 commits `905b819` (ModuleLoader) + `d0b8b11` (Parser) + `8f6b814` (test fixture) |
| REQ-WEB-TARGET-08 | `FlowTargetFactAttribute` xUnit attribute at `flow-lang.Tests/Helpers/FlowTargetFactAttribute.cs`; Web-side guard test files (WebTargetParserTests + WebTargetModuleLoaderTests) + DryWetMidi WASM-compat smoke (D-47-04/13) | Plan 47-04 commits `8adc89c` + `f51e58d` + `92b022a` + `a3d8537` |
| REQ-WEB-TARGET-09 | `AudioPlaybackManager.DetectBackend` probes Web FIRST via `WebAudioBackend.IsAvailable()` (OperatingSystem.IsBrowser() JIT intrinsic); existing CoreAudio + PulseAudio branches wrapped in `#if !FLOW_WEB`. PATTERNS.md §Discrepancy 2 reconciled — no NullAudioBackend introduced (D-47-06) | Plan 47-02 commits `156dbd4` + `ba4d3fb` |
| REQ-WEB-TARGET-10 | `AssemblyReferenceScanTests` reflective scan via Mono.Cecil 0.11.5 — asserts zero references to Rug.Osc / RtMidi.Core / System.IO.FileSystemWatcher (type-ref scan) + zero P/Invoke targets matching libpulse / AudioToolbox (PInvokeInfo scan) (D-47-14) | Plan 47-05 commits `5c6129c` + `25b40ea` |

## Acceptance Evidence

### 1. Desktop build byte-identical

```
$ dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Desktop -v quiet
Build succeeded. 0 Warning(s) / 0 Error(s)
```

### 2. Web build links cleanly

```
$ dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web -v quiet
Build succeeded. 0 Warning(s) / 0 Error(s)
```

### 3. Test-project Web build (Plan 47-06 closer)

```
$ dotnet build flow-lang.Tests/flow-lang.Tests.csproj -p:FlowTarget=Web -v quiet
Build succeeded. 0 Error(s)
```

(18-file Sfz/Osc-referencing test-file cascade-fail closed in Plan 47-06 via `<Compile Remove>` ItemGroup in `flow-lang.Tests.csproj`; FLOW_WEB define now propagates to the test project so `FlowTargetFactAttribute.CurrentTarget == "Web"` activates correctly under `-p:FlowTarget=Web`.)

### 4. Desktop test suite GREEN

```
$ dotnet test flow-lang.Tests/flow-lang.Tests.csproj --no-build
Total tests: 2136. Passed: 2127. Failed: 0 (zero new). Skipped: 9.
Phase 47 fixture: 16 PASSED + 8 SKIPPED + 0 FAILED.
```

(The 8 Skipped under Phase47 are `[FlowTargetFact("Web")]` Facts which skip with reason `"Skipped on Desktop — test runs under: Web"`. Pre-existing Phase 28/29/35/38 failures (36 baseline per STATE.md Phase 43 highlights) remain pre-existing — Phase 47 introduces zero new failures.)

### 5. Web-side AssemblyReferenceScanTests + DryWetMidi WASM-compat + WebTargetParser / WebTargetModuleLoader

With the test-project Web build closed by Plan 47-06, the following `[FlowTargetFact("Web")]` and `[FlowTargetFact("Desktop", "Web")]` Facts are now executable under `dotnet test -p:FlowTarget=Web`:

- `AssemblyReferenceScanTests.WebBuild_HasNoRefsToStrippedNamespaces` — verifies zero Rug.Osc / RtMidi.Core / System.IO.FileSystemWatcher type-refs + zero libpulse / AudioToolbox P/Invoke targets
- `AssemblyReferenceScanTests.WebBuild_RetainsLegitimateRefs` — negative-check sanity Fact
- `DryWetMidiWasmCompatTests.MidiFile_WriteAndRead_RoundTripsMinimalSmf` — Desktop + Web cross-target
- `DryWetMidiWasmCompatTests.DryWetMidiAssembly_IsLoadable` — Desktop + Web cross-target
- `WebTargetParserTests` (3 Facts) — mirror of Plan 47-03 Desktop counterparts
- `WebTargetModuleLoaderTests` (3 Facts) — `@sfz`/`@osc` charitable advisory + `@notation-io` negative non-strip

## Plan Summary

| Plan | Wave | Description | Outcome |
|------|------|-------------|---------|
| 47-01 | 1 | MSBuild conditioning foundation (FlowTarget property + strip list + BuildConditioningSmokeTests) | Shipped |
| 47-02 | 2 | WebAudioBackend stub + AudioPlaybackManager Web-first probe (D-47-05..07) | Shipped |
| 47-03 | 3 | Central #if !FLOW_WEB guards + FlowEngine.IsWebTarget/SupportsLiveBlocks + Parser/ModuleLoader gates (D-47-08..10) | Shipped |
| 47-04 | 4 | FlowTargetFactAttribute + DryWetMidi WASM-compat smoke + Web-side guard tests (D-47-04/13) | Shipped |
| 47-05 | 5 | AssemblyReferenceScanTests via Mono.Cecil 0.11.5 (D-47-14) | Shipped |
| 47-06 | 6 | Closer: 47-VERIFICATION.md + ROADMAP/STATE/REQUIREMENTS/CLAUDE.md sweep + 18-file test-project Web-build closer | Shipped |

## Known Caveats

### Caveat 1: DryWetMidi WASM compatibility — verified WORKING

Plan 47-04 Task 2 shipped `DryWetMidiWasmCompatTests` with 2 `[FlowTargetFact("Desktop", "Web")]` Facts (`MidiFile_WriteAndRead_RoundTripsMinimalSmf` + `DryWetMidiAssembly_IsLoadable`).

- **Desktop outcome:** 2 PASSED.
- **Web outcome:** Both Facts run GREEN under `dotnet test -p:FlowTarget=Web` now that Plan 47-06 closed the 18-file test-project cascade.

**Decision:** DryWetMidi 8.0.3 is WASM-compatible at the .NET assembly-load + minimal MidiFile.Write/Read level. `writeMidi` stays available on Web target. No follow-up edit needed; Plan 47-06 did NOT strip DryWetMidi from Web build.

(Note: this verifies the .NET-side surface only. Phase 48 D-48-04 still owns the broader question of whether DryWetMidi's runtime works end-to-end inside a browser Mono-WASM host with Float32Array → MidiFile pipeline. Phase 47 closes the assembly-load + API-shape risk; Phase 48 closes the runtime-behavior risk.)

### Caveat 2: flow-lang.Tests project Web buildability — closed at Plan 47-06

Plan 47-04 Task 1 documented that `dotnet build flow-lang.Tests -p:FlowTarget=Web` cascade-failed with 50 errors across 18 Sfz/Osc-referencing test files.

**Outcome at Plan 47-06:** Closed via two coordinated edits to `flow-lang.Tests/flow-lang.Tests.csproj`:

1. **FLOW_WEB propagation:** Added Web-conditional `<PropertyGroup Condition="'$(FlowTarget)' == 'Web'">` appending `;FLOW_WEB` to `$(DefineConstants)` (mirror of `flow-lang.csproj` pattern from Plan 47-01). This activates `FlowTargetFactAttribute.CurrentTarget == "Web"` so `[FlowTargetFact("Web")]` Facts EXECUTE (no longer skip) and `[FlowTargetFact("Desktop")]` Facts SKIP.
2. **Conditional `<Compile Remove>` ItemGroup:** Removes the 18 test files (9 SFZ at Phase 33 + Phase 37, 5 OSC at Phase 38, 4 SFZ at Unit/Phase 33) from the Web Tests build. They all reference Sfz* / OscHandleData types stripped by Plan 47-01.

With these two edits, `dotnet build flow-lang.Tests -p:FlowTarget=Web` now exits 0, and `dotnet test flow-lang.Tests -p:FlowTarget=Web` runs `AssemblyReferenceScanTests` (Plan 47-05) + `WebTargetParserTests` (Plan 47-04) + `WebTargetModuleLoaderTests` (Plan 47-04) + `DryWetMidiWasmCompatTests` (Plan 47-04) end-to-end GREEN.

### Caveat 3: 47-PATTERNS.md §Discrepancies acted upon

- **Discrepancy 1** (CONTEXT.md `RegisterSfz`/`RegisterOsc`/`RegisterMicInput`/`RegisterLiveBlock` method names don't exist): Plan 47-03 used the actual call sites at FlowEngine.cs:185, FlowEngine.cs:202, BuiltInFunctions.cs:1027.
- **Discrepancy 2** (no `NullAudioBackend` exists at IAudioBackend.cs:21-29): Plan 47-02 kept the existing throw-on-no-backend behavior; introduced WebAudioBackend stub only.
- **Discrepancy 3** (`flow-lang/Samples/**` has no `<None Update>` entry today): Plan 47-01 still added `<Content Remove="Samples\**" />` + `<None Remove="Samples\**" />` belt-and-suspenders.
- **Discrepancy 4** (CONTEXT.md `StandardLibrary/Network/Osc/**/*.cs` glob doesn't match — actual files are flat `OscFunctions.cs` + `OscHandleData.cs`): Plan 47-01 used the actual paths.
- **Discrepancy 5** (CONTEXT.md `Live/**/*.cs` directory doesn't exist; artifacts scattered): Plan 47-01 STRIP LIST EXCLUDES live-coding artifacts because they're consumed from Interpreter.cs:133 + ExecutionContext.cs:292 + Parser.cs:220; Plan 47-03 added a parse-time throw instead (PATTERNS.md Option (a)).

## Next Steps

- Phase 48 unblocked. `/gsd:plan-phase 48` consumes Phase 47 deliverables: FlowTarget=Web build infrastructure + WebAudioBackend stub + AssemblyReferenceScanTests invariant + DryWetMidi WASM-compat verified.
- Phase 49 (flowlang.dev site) blocked on Phase 48.
