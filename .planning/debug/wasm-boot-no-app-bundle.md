---
slug: wasm-boot-no-app-bundle
status: fix_implemented_pending_browser_uat
trigger: Phase 48 WASM runtime does not boot in a browser — `dotnet publish flow-lang/flow-lang.csproj -p:FlowTarget=Web -c Release` produces no bootable WASM app bundle (no dotnet.boot.js, no _framework/, no AppBundle/). Browser console reports "Flow runtime boot failed: Failed to load config file dotnet.boot.js". flow-lang is a LIBRARY project; library publish with RuntimeIdentifier=browser-wasm emits runtime + native intermediates but never runs the app-bundle generation step.
created: 2026-05-30T17:32:05Z
updated: 2026-05-30T18:55:00Z
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
