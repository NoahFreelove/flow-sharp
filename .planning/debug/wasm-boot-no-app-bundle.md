---
slug: wasm-boot-no-app-bundle
status: fix_implemented_pending_browser_uat
trigger: Phase 48 WASM runtime does not boot in a browser — `dotnet publish flow-lang/flow-lang.csproj -p:FlowTarget=Web -c Release` produces no bootable WASM app bundle (no dotnet.boot.js, no _framework/, no AppBundle/). Browser console reports "Flow runtime boot failed: Failed to load config file dotnet.boot.js". flow-lang is a LIBRARY project; library publish with RuntimeIdentifier=browser-wasm emits runtime + native intermediates but never runs the app-bundle generation step.
created: 2026-05-30T17:32:05Z
updated: 2026-05-30T19:40:00Z
phase: 48-wasm-runtime-webaudio-backend
related_plan: 48-06
---

# Debug Session: wasm-boot-no-app-bundle

## Symptoms

- **Expected behavior:** After `dotnet publish flow-lang/flow-lang.csproj -p:FlowTarget=Web -c Release`, serving the publish output and visiting `wasm/index.html` boots the Mono-WASM runtime, and clicking Run plays an audible 440 Hz tone via the WebAudio backend. The structured RunResult (stdout/errors[]) is visible in DevTools.
- **Actual behavior:** Runtime never boots. `flow-runtime.js`'s `import '../dotnet.js'` resolves (dotnet.js IS at publish root), but `dotnet.create()` then fetches its boot manifest `dotnet.boot.js` at the served root and 404s. No audio, no execution.
- **Error messages:**
  ```
  Flow runtime boot failed: Failed to load config file dotnet.boot.js
  TypeError: error loading dynamically imported module: http://localhost:8080/dotnet.boot.js
  ```
- **Timeline:** Surfaced 2026-05-30 on the first real-browser smoke (Plan 48-06 HUMAN-UAT, Chrome/Linux). Never worked in a browser — Plans 48-01..05 only presence-checked publish files on disk and ran `WasmEntry.RunFromJs` in-process on Desktop; the browser boot path was never exercised.
- **Reproduction:**
  1. `dotnet publish flow-lang/flow-lang.csproj -p:FlowTarget=Web -c Release`
  2. `cd flow-lang/bin/Release/net10.0/browser-wasm/publish/ && python3 -m http.server 8080`
  3. Visit `http://localhost:8080/wasm/index.html` in Chrome 120+ with DevTools open
  4. Observe boot failure (dotnet.boot.js 404) in console

## Confirmed Root Cause (VERIFIED EMPIRICALLY 2026-05-30)

`flow-lang` is a **library** project (`<Project Sdk="Microsoft.NET.Sdk">`, no app head, no `Main`, `OutputType=Library`). The Mono-WASM runtime-targets pack (`Microsoft.NET.Runtime.WebAssembly.Sdk`, supplied by the `wasm-tools` workload) IS engaged for the native publish, but its app-bundle/boot-manifest target is gated OFF for libraries.

Decisive gate, `Microsoft.NET.Runtime.WebAssembly.Sdk/10.0.8/Sdk/WasmApp.Common.targets`:
- Line 163: `<_IsLibraryMode Condition="'$(OutputType)' == 'Library' and '$(UsingMicrosoftNETSdkWebAssembly)' == 'true'">true</_IsLibraryMode>`
- Line 165: `<IsBrowserWasmProject Condition="... ('$(OutputType)' != 'Library' or '$(_IsLibraryMode)' == 'true')">true</IsBrowserWasmProject>`
- Lines 175-176: `<WasmGenerateAppBundle Condition="... ('$(OutputType)' != 'Library' or '$(_IsLibraryMode)' == 'true')">true</WasmGenerateAppBundle>` else `false`.

Because this project uses plain `Microsoft.NET.Sdk` (NOT `Microsoft.NET.Sdk.WebAssembly`), `UsingMicrosoftNETSdkWebAssembly` is empty → `_IsLibraryMode` is empty → `IsBrowserWasmProject` and `WasmGenerateAppBundle` both default to **false**. So `_WasmGenerateAppBundle` never runs, `dotnet.boot.js` / `_framework/` / `AppBundle/` are never written, and `dotnet.create()` 404s on its boot manifest.

## Goal

Make `FlowTarget=Web` produce a **bootable** WASM app bundle (emit `dotnet.boot.js` + framework layout), reconcile `flow-runtime.js`'s `../dotnet.js` import + `wasm/` placement + `index.html` against the new bundle layout, and add a publish-output "boot-manifest-exists" assertion. Verify by **republish → serve → boot in a real browser**. This in-phase repair gates Plan 48-07 (the Phase 48 closer).

## Key Files

- `flow-lang/flow-lang.csproj` — FlowTarget=Web PropertyGroup/ItemGroup (D-48-01..05 build pipeline)
- `flow-lang/wasm/flow-runtime.js` — ES module, `import '../dotnet.js'` (D-48-12)
- `flow-lang/wasm/index.html` — dev smoke harness, D-48-09 user-gesture autoplay chain
- `flow-lang/wasm/trim-roots.xml` — trimmer roots
- `flow-lang/Runtime/WasmEntry.cs` — `[JSExport]` boundary (RunFromJs/PlayFromJs)
- `flow-lang.Tests/Integration/Phase48/WasmBuildPipelineTests.cs` — `WasmPublish_ProducesAppBundle` MISNAMED; checks only dotnet.js/flow-lang.dll/dotnet.native.wasm, NOT dotnet.boot.js → this is exactly the defect-class gap.

## Constraints

- Must NOT break the Desktop target (`FlowTarget=Desktop` default — all P/Invoke/SFZ/OSC/live-coding intact).
- Single-source-of-truth Pattern A preferred over a separate project IF a library can emit a boot bundle.
- Zero new composer-facing NuGet packages where possible (Mono-WASM ships in .NET 10 SDK).
- Preserve bundle-size budget (D-48-05) and two-run determinism (D-48-16).

## Current Focus

- hypothesis: Forcing `WasmGenerateAppBundle=true` + `IsBrowserWasmProject=true` + `WasmMainJSPath` on the existing `Microsoft.NET.Sdk` library, gated under FlowTarget=Web, triggers the runtime-pack's app-bundle target and emits a bootable `_framework/dotnet.boot.js` WITHOUT swapping SDKs or adding a project. CONFIRMED by experiment.
- test: `dotnet publish ... -p:WasmGenerateAppBundle=true -p:IsBrowserWasmProject=true -p:WasmMainJSPath=wasm/flow-runtime.js`
- expecting: AppBundle/ with _framework/dotnet.boot.js + dotnet.js + dotnet.native.wasm + flow-lang.wasm. CONFIRMED.
- next_action: RESOLVED — human approved the minimal-lever fix; implemented + verified (see Resolution). Remaining: human real-browser re-smoke to fully close 48-06 (cannot be automated).

## Evidence

- timestamp: 2026-05-30T17:40Z — `dotnet msbuild ... -getProperty` on FlowTarget=Web: `UsingMicrosoftNETSdkWebAssembly=""`, `OutputType=Library`, `WasmGenerateAppBundle=false`, `_IsLibraryMode=""`, `IsBrowserWasmProject=""`. Root cause confirmed at the MSBuild-property level.
- timestamp: 2026-05-30T17:45Z — baseline `dotnet publish -p:FlowTarget=Web -c Release` exits 0; publish/ is FLAT and contains dotnet.js / dotnet.native.wasm / dotnet.runtime.js / flow-lang.dll PLUS leaked build intermediates (driver.c, corebindings.c, libmonosgen-2.0.a, emcc-link.rsp, *.h, wasm-props.json). NO dotnet.boot.js, NO _framework/, NO AppBundle/. `find` for dotnet.boot.js returns nothing. Symptom reproduced on disk.
- timestamp: 2026-05-30T17:55Z — EXPERIMENT: `dotnet publish -p:FlowTarget=Web -c Release -p:WasmGenerateAppBundle=true -p:IsBrowserWasmProject=true -p:WasmMainJSPath=wasm/flow-runtime.js` exits 0 and logs `Generated app bundle at .../browser-wasm/AppBundle/`. AppBundle/_framework/ contains dotnet.boot.js (5561 bytes, `mainAssemblyName: flow-lang.dll`), dotnet.js, dotnet.native.wasm, dotnet.runtime.js, dotnet.native.js, and all assemblies as `.wasm` (Webcil: flow-lang.wasm, System.*.wasm, Melanchall.DryWetMidi.wasm). AppBundle/ root has flow-runtime.js (copied via WasmMainJSPath) + package.json `{ "type":"module" }`. The minimal-lever fix is VIABLE without an SDK swap or separate project.
- timestamp: 2026-05-30T18:00Z — Layout reconciliation needed: (1) AppBundle places `flow-runtime.js` at AppBundle/ ROOT and runtime under AppBundle/_framework/, so flow-runtime.js's `import '../dotnet.js'` is wrong for the bundle (should be `./_framework/dotnet.js`). (2) index.html header (lines 11-13) + module comment (lines 66-67) were ALREADY written for an `AppBundle/wasm/` layout that the csproj never actually produced — neither the old `../dotnet.js` nor the `AppBundle/wasm/` assumption matches the generated bundle. (3) csproj currently CopyToPublishDirectory's flow-runtime.js+index.html into publish/wasm/ of the FLAT tree, divorced from the AppBundle tree.
- timestamp: 2026-05-30T18:05Z — Stdlib VFS note (follow-up, does NOT block boot): `ModuleLoader.ResolveStdlibPath` (line 280) resolves `@std`/etc. via `AppContext.BaseDirectory` + File IO. In WASM that's the Emscripten VFS root; `.flow` files must be mounted there for any script using `use "@std"`. The 48-06 smoke script `(play (createSineTone 440Hz 1.0 0.5))` imports no module, so boot + tone verification is unblocked; mounting stdlib into the bundle VFS is a separable concern.
- timestamp: 2026-05-30T19:10Z — SECOND, DISTINCT defect surfaced on the human re-smoke (boot defect FIXED + committed 08140bb; page now boots). Clicking Run on the default script `(play (createSineTone 440Hz 1.0 0.5))` threw `[runtime] JsonSerializerIsReflectionDisabled`. Cause CONFIRMED at source: `WasmEntry.cs:283` called `JsonSerializer.Serialize(result, _jsonOptions)` with a plain reflection-based `JsonSerializerOptions` (old line 132). `FlowTarget=Web` sets `<TrimMode>full</TrimMode>`, which DISABLES reflection-based System.Text.Json in the trimmed WASM build → the serializer throws at runtime in-browser. SAME "Desktop ≠ trimmed/browser" defect class as the boot bug: the Desktop in-process tests (WasmDeterminismTests) call RunFromJs where reflection JSON is ENABLED, so they never exercised the trimmed serializer path and did NOT catch it.
- timestamp: 2026-05-30T19:20Z — FIX implemented: added source-generated `FlowWasmJsonContext : JsonSerializerContext` (`[JsonSourceGenerationOptions(PropertyNamingPolicy=CamelCase, DefaultIgnoreCondition=WhenWritingNull)]` + `[JsonSerializable(typeof(RunResult))]` + `[JsonSerializable(typeof(RunError))]`) in WasmEntry.cs; switched the serialize call to `JsonSerializer.Serialize(result, FlowWasmJsonContext.Default.RunResult)`; retired the now-unused reflection-based `_jsonOptions` field (grep confirmed line 283 was its only consumer). Did NOT take the `JsonSerializerIsReflectionEnabledByDefault=true` escape hatch (would fight TrimMode=full + risk IL2026). Audited WasmEntry for other reflection-dependent paths reachable from RunFromJs/PlayFromJs (Activator/Type.GetType/GetProperty/other JsonSerializer): NONE found; the only JsonSerializer use in the whole library was this one call site.
- timestamp: 2026-05-30T19:25Z — VERIFICATION (green build alone NOT treated as sufficient): (1) `dotnet build -p:FlowTarget=Desktop` exit 0 (source-gen attributes compile on both targets; P/Invoke/SFZ/OSC/live-coding intact). (2) `dotnet publish -p:FlowTarget=Web -c Release` exit 0 with ZERO IL2026/IL3050 trim warnings (source-gen eliminated the reflection-JSON trim concern; the only residual IL warning is the PRE-EXISTING IL2075 at ExpressionEvaluator.cs:960, unrelated). (3) `AppBundle/_framework/dotnet.boot.js` still emitted (prior-cycle boot gate intact). (4) Web deps.json forbidden-reference scan clean (no Rug.Osc/RtMidi/libpulse/AudioToolbox).
- timestamp: 2026-05-30T19:30Z — REGRESSION NET STRENGTHENED: added `WasmJsonSerializationTests` (Desktop-runnable, browser-free) — `SourceGenContext_Serializes_CamelCase_WithNullOmission` round-trips RunResult through `FlowWasmJsonContext.Default.RunResult` and asserts camelCase keys + null-omission (wav/midi/sourceSnippet omitted) + no PascalCase leak; `RunFromJs_Produces_CamelCase_RunResult_Shape` mirrors it end-to-end through the export. This catches the JsonSerializerIsReflectionDisabled defect class without a browser by pinning the source-gen CONTRACT.
- timestamp: 2026-05-30T19:35Z — TEST-INFRA RACE fixed (surfaced while strengthening the net, pre-existing latent): (a) adding a second RunFromJs-calling class exposed a process-wide `Console.SetOut`/`SetError` redirection race between RunFromJs-calling classes running in xUnit cross-class parallel → intermittent WasmDeterminismTests stdout-leak failure. Fixed by `WasmEntryConsoleCollection` serializing both classes. (b) The three FlowTarget=Web publish-shellout classes (WasmBuildPipelineTests/BundleSizeBudgetTests/DryWetMidiWasmPublishTests) raced on the shared `obj/.../browser-wasm` intermediate when run in parallel → intermittent `MarshalingPInvokeScanner: BadImageFormatException: Image is too small` (MSB4018). Fixed by `WasmWebPublishCollection` serializing the three. Each class passed in isolation before the fix; no assertion weakened. Full Phase48 suite now 21/21 green across 3 consecutive from-clean runs (was intermittently 19/21).
- timestamp: 2026-05-30T19:38Z — BUNDLE SIZE (from test-written 48-BUNDLE-SIZE.md): uncompressed 5,302,572 bytes (5.06 MB) / Brotli 1,611,266 bytes (1.54 MB) / ratio 30.4% → D-48-05 MONOLITHIC SHIP. Neutral-to-slightly-smaller vs the prior cycle's 5.24 MB uncompressed / 1.59 MB Brotli — source-gen added no bloat, as predicted.

## Eliminated

- timestamp: 2026-05-30T17:50Z — "Just set `WasmGenerateAppBundle=true` alone" is INSUFFICIENT in isolation: `IsBrowserWasmProject` was also empty for the library, so the app-bundle AfterTargets hook (`WasmTriggerPublishAppAfterThisTarget`) would not be wired. The experiment set all three (`WasmGenerateAppBundle`+`IsBrowserWasmProject`+`WasmMainJSPath`); that combination is what fired the target.
- timestamp: 2026-05-30T17:35Z — Switching to `Microsoft.NET.Sdk.WebAssembly` SDK is NOT available as a standalone SDK in this install (it ships with the Blazor/`Microsoft.NET.Sdk.Web` stack, not the `wasm-tools` workload). The `_IsLibraryMode=true` native path is therefore not reachable without pulling the Blazor SDK — heavier than the property-forcing approach, which works today.

## Resolution

- **root_cause:** `flow-lang` is a plain `Microsoft.NET.Sdk` LIBRARY project, so the Mono-WASM runtime pack's app-bundle gate in `WasmApp.Common.targets` (`_IsLibraryMode` requires `UsingMicrosoftNETSdkWebAssembly == 'true'`, which is empty under the plain SDK) left `IsBrowserWasmProject` + `WasmGenerateAppBundle` defaulting to FALSE. The `_WasmGenerateAppBundle` target never ran, so no `dotnet.boot.js` / `_framework/` / `AppBundle/` was produced and the browser's `dotnet.create()` 404'd on its boot manifest.

- **fix (MINIMAL-LEVER, human-approved):** Force the runtime-pack app-bundle target ON via three properties inside the existing `FlowTarget=Web` PropertyGroup — `WasmGenerateAppBundle=true`, `IsBrowserWasmProject=true`, `WasmMainJSPath=wasm/flow-runtime.js` — plus `WasmMainHTMLPath=wasm/index.html` to drop the dev-smoke harness at the bundle root. Single-source-of-truth Pattern A: no SDK swap, no second project. Reconciled the three serve artifacts against the real generated layout (verified empirically, NOT guessed):
  - `flow-lang/wasm/flow-runtime.js` — import fixed `'../dotnet.js'` → `'./_framework/dotnet.js'` (flow-runtime.js sits at `AppBundle/` root, dotnet.js under `AppBundle/_framework/`). D-48-12 ES module + D-48-09 user-gesture chain + 5-name `setModuleImports('flow-runtime', …)` surface + `RunResult` JSON.parse path all preserved.
  - `flow-lang/wasm/index.html` — serve-path header comments corrected from the never-produced `AppBundle/wasm/` layout to the real `AppBundle/` root; in-file `import './flow-runtime.js'` confirmed correct (same dir).
  - `flow-lang/flow-lang.csproj` — old flat-tree `CopyToPublishDirectory` hooks for flow-runtime.js/index.html set to `Never` (the runtime pack now owns the AppBundle copy via WasmMainJSPath/WasmMainHTMLPath); trim-roots wiring intact.

- **layout learned:** App-bundle mode emits assemblies as **Webcil `.wasm`** (`flow-lang.wasm`, `Melanchall.DryWetMidi.wasm`), NOT `.dll`; the bootable bundle lands at `bin/Release/net10.0/browser-wasm/AppBundle/` (where `WasmAppDir` = `$(OutputPath)/AppBundle` defaults), a SIBLING of the old flat `publish/` tree (the flat tree still emits real PE `.dll`s alongside).

- **verification (automated — green build alone was NOT treated as sufficient):**
  - `dotnet publish -p:FlowTarget=Web -c Release` exit 0 AND `AppBundle/_framework/dotnet.boot.js` confirmed on disk (`mainAssemblyName: flow-lang.dll`), with `dotnet.js` + `dotnet.native.wasm` + `flow-lang.wasm` + `Melanchall.DryWetMidi.wasm` in a coherent servable layout.
  - `flow-runtime.js` import path + `index.html` load path confirmed against the real generated files in the bundle.
  - `dotnet build -p:FlowTarget=Desktop` exit 0 (P/Invoke / SFZ / OSC / live-coding intact).
  - Phase48 fixtures pass: `WasmBuildPipelineTests` (3/3 — the misnamed `WasmPublish_ProducesAppBundle` is now `WasmPublish_ProducesBootableAppBundle` asserting `dotnet.boot.js` EXISTS — the regression gate that would have caught the original defect), `DryWetMidiWasmPublishTests` (2/2 — Cecil scan reads the flat-tree PE `flow-lang.dll`; bundle presence-checks Webcil `flow-lang.wasm`), `BundleSizeBudgetTests` (2/2).
  - Web build forbidden-reference invariant confirmed clean (deps.json scan: only DryWetMidi + Mono runtime pack; NO Rug.Osc / RtMidi / libpulse / AudioToolbox). Phase47 `AssemblyReferenceScanTests` are `[FlowTargetFact("Web")]`-gated and skip on the Desktop runner (pre-existing, unchanged).
  - **Bundle size:** AppBundle `_framework/` = **1.59 MB Brotli / 5.24 MB uncompressed** → D-48-05 `MONOLITHIC SHIP`. This MEASURES THE BOOTABLE BUNDLE (Webcil), vs. the prior 3.07 MB-Brotli flat-publish-tree number (which also counted ICU `.dat` + stdlib `.flow`); not a regression — comfortably under the 15 MB target / 20 MB hard cap.

- **files_changed:**
  - `flow-lang/flow-lang.csproj`
  - `flow-lang/wasm/flow-runtime.js`
  - `flow-lang/wasm/index.html`
  - `flow-lang.Tests/Integration/Phase48/WasmBuildPipelineTests.cs`
  - `flow-lang.Tests/Integration/Phase48/DryWetMidiWasmPublishTests.cs`
  - `flow-lang.Tests/Integration/Phase48/BundleSizeBudgetTests.cs`

- **STILL REQUIRED to fully close 48-06 (NOT done by this agent — cannot be):** A real-browser re-smoke (Plan 48-06 HUMAN-UAT Chrome/Firefox rows) — serve `bin/Release/net10.0/browser-wasm/AppBundle/` and load `index.html`, confirm boot + audible 440 Hz tone. The automated `dotnet.boot.js`-exists assertion is the strongest available proxy, NOT full closure.

- **separate follow-up (explicitly NOT solved here, per human decision):** stdlib `.flow` VFS mounting for in-browser `use "@std"` (ModuleLoader.ResolveStdlibPath resolves via AppContext.BaseDirectory + File IO = Emscripten VFS root). The 48-06 smoke script imports no module, so boot + tone is verifiable without it.

---

## Resolution — SECOND DEFECT (JsonSerializerIsReflectionDisabled), same Plan 48-06 gate

- **root_cause:** `WasmEntry.RunFromJs` serialized its `RunResult` via reflection-based System.Text.Json (`JsonSerializer.Serialize(result, _jsonOptions)`, plain `JsonSerializerOptions`). The `FlowTarget=Web` publish sets `<TrimMode>full</TrimMode>`, which disables reflection-based serialization in the trimmed WASM build, so the serializer threw `JsonSerializerIsReflectionDisabled` at runtime in the browser the moment Run was clicked. Identical "Desktop ≠ trimmed/browser" defect class as the boot bug — masked because the Desktop in-process tests run with reflection JSON enabled and never touched the trimmed serializer path.

- **fix:** Replaced reflection-based serialization with a SOURCE-GENERATED `System.Text.Json` context (the canonical AOT/WASM-safe pattern):
  - `flow-lang/Runtime/WasmEntry.cs` — added `internal partial class FlowWasmJsonContext : JsonSerializerContext` with `[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]` + `[JsonSerializable(typeof(RunResult))]` + `[JsonSerializable(typeof(RunError))]`; switched the serialize call to `JsonSerializer.Serialize(result, FlowWasmJsonContext.Default.RunResult)`; retired the now-unused reflection `_jsonOptions` field. The D-48-14/15 JSON shape (camelCase keys + null-omission for wav/midi/sourceSnippet) is preserved byte-for-byte — `JsonKnownNamingPolicy.CamelCase` is the source-gen equivalent of `JsonNamingPolicy.CamelCase`. Did NOT enable `JsonSerializerIsReflectionEnabledByDefault` (rejected — fights TrimMode=full, risks IL2026/bloat).
  - `flow-lang/wasm/trim-roots.xml` — refreshed the stale comment (the roots were said to be "reflected on by JsonSerializer (IL2026 at WasmEntry.cs:283)"; now documents the source-gen path + why the roots are still kept).
  - Audited every reflection-dependent path reachable from the [JSExport] surface (RunFromJs/PlayFromJs) — Activator / Type.GetType / GetProperty / other JsonSerializer calls: NONE; this was the only JsonSerializer use in the library.

- **regression net (browser-free proxy + infra hardening):**
  - `flow-lang.Tests/Integration/Phase48/WasmJsonSerializationTests.cs` (NEW) — pins serialization through `FlowWasmJsonContext.Default.RunResult` + asserts camelCase / null-omission / no-PascalCase-leak, plus an end-to-end `RunFromJs` shape assertion. Catches this defect class on the Desktop runner without a browser.
  - `flow-lang.Tests/Integration/Phase48/WasmEntryConsoleCollection.cs` (NEW) — serial xUnit collection for RunFromJs-calling classes (process-wide Console.SetOut redirection race).
  - `flow-lang.Tests/Integration/Phase48/WasmWebPublishCollection.cs` (NEW) — serial xUnit collection for FlowTarget=Web publish-shellout classes (shared obj/ intermediate race → MarshalingPInvokeScanner BadImageFormatException). Applied to WasmBuildPipelineTests / BundleSizeBudgetTests / DryWetMidiWasmPublishTests.
  - Both races were PRE-EXISTING latent flakiness; each affected class passed in isolation. Full Phase48 suite now 21/21 green across 3 consecutive from-clean runs.

- **verification:** Desktop build exit 0; Web publish exit 0 with ZERO IL2026/IL3050 (only the pre-existing unrelated IL2075 remains); `dotnet.boot.js` still emitted (prior boot gate intact); Web deps.json forbidden-ref scan clean; Phase48 suite 21/21. Bundle: 5.06 MB uncompressed / 1.54 MB Brotli — MONOLITHIC SHIP, neutral-to-smaller vs prior 5.24 / 1.59 MB.

- **files_changed:**
  - `flow-lang/Runtime/WasmEntry.cs`
  - `flow-lang/wasm/trim-roots.xml`
  - `flow-lang.Tests/Integration/Phase48/WasmJsonSerializationTests.cs` (new)
  - `flow-lang.Tests/Integration/Phase48/WasmEntryConsoleCollection.cs` (new)
  - `flow-lang.Tests/Integration/Phase48/WasmWebPublishCollection.cs` (new)
  - `flow-lang.Tests/Integration/Phase48/WasmDeterminismTests.cs` (+[Collection])
  - `flow-lang.Tests/Integration/Phase48/WasmBuildPipelineTests.cs` (+[Collection])
  - `flow-lang.Tests/Integration/Phase48/BundleSizeBudgetTests.cs` (+[Collection])
  - `flow-lang.Tests/Integration/Phase48/DryWetMidiWasmPublishTests.cs` (+[Collection])

- **STILL REQUIRED to fully close Plan 48-06 (cannot be automated):** real-browser re-smoke — publish `-p:FlowTarget=Web -c Release`, serve `flow-lang/bin/Release/net10.0/browser-wasm/AppBundle/`, load `index.html` in Chrome/Firefox, click Run, expect an AUDIBLE 440 Hz tone + structured stdout/errors in DevTools, with NO `JsonSerializerIsReflectionDisabled`. The source-gen contract test is the strongest available browser-free proxy, NOT full closure.

