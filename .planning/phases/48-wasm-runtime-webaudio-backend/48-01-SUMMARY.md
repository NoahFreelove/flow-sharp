---
phase: 48-wasm-runtime-webaudio-backend
plan: 01
subsystem: build-pipeline
tags: [wasm, mono-wasm, jiterpreter, trim, msbuild, flow-target-web]
provides:
  - "flow-lang.csproj FlowTarget=Web extended with Mono-WASM publish properties (RuntimeIdentifier=browser-wasm, jiterpreter, invariant globalization, TrimMode=full, TrimmerRootDescriptor)"
  - "flow-lang/wasm/trim-roots.xml — 42 preserve=all entries pinning FlowType subclasses + music-type singletons + interop boundary types (Value, AudioBuffer, WebAudioBackend)"
  - "flow-lang.Tests/Integration/Phase48/WasmBuildPipelineTests.cs — 3-Fact xUnit smoke pinning dotnet publish -p:FlowTarget=Web -c Release exit 0 + bundle structure + bundle size measurement"
  - "Verified Mono-WASM publish pipeline end-to-end: dotnet publish exits 0, dotnet.js + flow-lang.dll + dotnet.native.wasm produced, bundle size 10.8 MB shipped-artifact (well under 30 MB hard cap)"
requires:
  - "Phase 47 D-47-01..03 FlowTarget=Desktop|Web MSBuild conditioning (single source of truth — extends rather than supplants)"
  - "Phase 47 D-47-05 WebAudioBackend stub class (interop boundary preserved by trim-roots.xml ahead of Plan 48-03 real implementation)"
  - ".NET 10 SDK with wasm-tools workload installed (Mono-WASM publish toolchain — automatic Rule 3 fix during Task 1 verify)"
affects:
  - "Every subsequent Plan 48-NN consumes the FlowTarget=Web publish target this plan ships"
  - "Plan 48-03 WebAudioBackend [JSImport]/[JSExport] implementation requires Value + AudioBuffer + WebAudioBackend types preserved via trim-roots.xml"
  - "Plan 48-05 bundle-size budget consumes the 10.8 MB measurement from Task 3 — well under the 15 MB compressed target, so lazy-loading deferred unless v1.6 budget changes"
tech_stack:
  added:
    - "wasm-tools workload (.NET 10 SDK, version 10.0.8 — installed via dotnet workload restore during Task 1 verify; not a NuGet package, ships in SDK)"
  patterns:
    - "Mono-WASM jiterpreter for reflection-friendly .NET in browser (D-48-01 — NOT NativeAOT-LLVM, that's v1.6 stretch)"
    - "TrimMode=full with TrimmerRootDescriptor to pin FlowType.Instance static getters (48-PATTERNS.md §Discrepancy 3 — real risk is *Type.Instance accessors, NOT InternalFunctionRegistry which has zero reflection)"
    - "Single-source-of-truth Phase 47 conditional ItemGroup extension (Pattern A — no new csproj, no new conditional block, just extends existing FlowTarget=Web blocks)"
    - "Library-publish flat layout (publish/dotnet.js direct) — discovered to be the actual Mono-WASM library output shape vs the Blazor-app AppBundle nesting"
key_files:
  created:
    - "flow-lang/wasm/trim-roots.xml (74 lines, 42 preserve=\"all\" entries — base FlowType + 21 SpecialTypes + 16 PrimitiveTypes + ArrayType + 3 interop boundary types)"
    - "flow-lang.Tests/Integration/Phase48/WasmBuildPipelineTests.cs (161 lines, 3 Facts, modelled on Phase 47 BuildConditioningSmokeTests.cs pattern)"
  modified:
    - "flow-lang/flow-lang.csproj (+24 lines inside existing Phase 47 FlowTarget=Web PropertyGroup + ItemGroup — 6 new properties + 1 new TrimmerRootDescriptor)"
decisions:
  - "Library publish produces flat publish/ layout (not Blazor-app AppBundle/_framework/ nesting) — WasmBuildPipelineTests.LocateWasmFrameworkDir checks both paths for forward compatibility"
  - "wasm-tools workload installed mid-task as automatic Rule 3 fix (not blocker — orchestrator pre-authorized via 'KNOWN POTENTIAL ISSUES' note in critical_context)"
  - "Bundle size measured at 10.8 MB browser-shipped artifacts (.dll + .wasm + .js + .dat + .flow + .json + .md) — excludes 29.5 MB of .a static archives (build-only, not shipped) and ~50 KB build artifacts"
  - "Test asserts < 30 MB hard cap (D-48-05) — current bundle has 19.7 MB margin to spare; Plan 48-05 lazy-loading deferred (v1.6 if needed)"
metrics:
  duration: "~7 minutes (start 2026-05-26T02:51:00Z, end 2026-05-26T02:58:01Z)"
  completed: 2026-05-26
  tasks: 3
  files_created: 2
  files_modified: 1
  bundle_size_uncompressed: "10,796,004 bytes (10.3 MiB / 10.8 MB shipped artifacts)"
  bundle_size_30mb_cap_margin: "20,661,276 bytes (19.7 MiB) — 65.7% headroom"
  publish_wall_clock: "~8 seconds (Mono-WASM publish — well under the 10-minute test timeout)"
  test_count: 3
  test_pass: 3
  test_fail: 0
---

# Phase 48 Plan 01: WASM Build Pipeline Foundation Summary

WASM publish pipeline lands: `dotnet publish flow-lang/flow-lang.csproj -p:FlowTarget=Web -c Release` exits 0 and produces a 10.8 MB Mono-WASM bundle (`dotnet.js` + `flow-lang.dll` + `dotnet.native.wasm`) — well under the 15 MB compressed / 30 MB uncompressed cap. The single biggest feasibility risk of the v1.5 milestone is now de-risked.

## Goal

Extend Phase 47's `FlowTarget=Web` MSBuild conditioning into a full Mono-WASM publish pipeline, pin acceptance in xUnit, and de-risk the foundation every subsequent Plan 48-NN depends on. Per the orchestrator's critical_context note: "if `dotnet publish flow-lang -p:FlowTarget=Web -c Release` succeeds with a real AppBundle, the rest of Phase 48 is well-bounded engineering."

## What Shipped

### Task 1 — `flow-lang/flow-lang.csproj` extended (commit `74ac158`)

Six new properties added to the existing `<PropertyGroup Condition="'$(FlowTarget)' == 'Web'">` block (extends Phase 47 D-47-01..03 single-source-of-truth pattern — no new conditional):

| Property | Value | Decision | Purpose |
|----------|-------|----------|---------|
| `<RuntimeIdentifier>` | `browser-wasm` | D-48-01 | Activates Mono-WASM publish pipeline |
| `<WasmEnableJiterpreter>` | `true` | D-48-01 | Reflection-friendly runtime (Blazor pattern, NOT NativeAOT) |
| `<InvariantGlobalization>` | `true` | D-48-03 | Saves ~10 MB ICU bundle |
| `<HybridGlobalization>` | `false` | D-48-03 | Explicit pin (defaults could change across SDK versions) |
| `<TrimMode>` | `full` | D-48-02 | Aggressive trim — strips unused BCL |
| `<WasmEmitSymbolMap Condition="'$(Configuration)' == 'Debug'">` | `true` | D-48-04 | Debug-only — Release strips for size + minor security |

One new entry inside the existing `<ItemGroup Condition="'$(FlowTarget)' == 'Web'">`:

```xml
<TrimmerRootDescriptor Include="wasm\trim-roots.xml" />
```

### Task 2 — `flow-lang/wasm/trim-roots.xml` (commit `c6dc20d`)

42 `preserve="all"` entries pinning the types Mono-WASM's trim analyzer might over-strip:

| Category | Count | Examples |
|----------|-------|----------|
| Base class | 1 | `FlowType` |
| SpecialTypes (music types) | 21 | `NoteType`, `ChordType`, `HertzType`, `TuningType`, `MarkovModelType`, `LsystemModelType`, ... |
| PrimitiveTypes (scalars) | 16 | `IntType`, `DoubleType`, `StringType`, `BoolType`, `SymbolType`, `VoidType`, ... |
| Array | 1 | `ArrayType` |
| Interop boundary | 3 | `AudioBuffer`, `Value`, `WebAudioBackend` |
| **Total** | **42** | (sanity range 25-50 per plan) |

**Critical decision (per 48-PATTERNS.md §Discrepancy 3):** `InternalFunctionRegistry` is NOT enumerated. Audit 2026-05-25 confirmed zero reflection use (`grep` for `GetMethods` / `BindingFlags` / `Activator.CreateInstance` returned 0 hits). Registration is via explicit `Register(...)` calls statically reachable from FlowEngine constructor — trim-mode reachability analysis already covers it.

**Omitted by design:** `SfzType` and `OscHandleType`. Their `.cs` source files are stripped from the Web build per Phase 47 strip list — including them in trim-roots.xml would cause `IL2007: Could not resolve assembly` at trim time.

**Interop boundary correction (per 48-PATTERNS.md §Discrepancy 1):** `AudioBuffer.Data` (NOT `AudioBuffer.Samples` as CONTEXT.md repeatedly mis-referenced). Plan 48-03 will marshal `.Data` across the JS boundary.

### Task 3 — `flow-lang.Tests/Integration/Phase48/WasmBuildPipelineTests.cs` (commit `447662f`)

3 xUnit Facts pinning the WASM publish pipeline:

| Fact | What it asserts | Outcome |
|------|-----------------|---------|
| `WasmPublish_ExitCodeIsZero` | `dotnet publish flow-lang -p:FlowTarget=Web -c Release` exits 0 | PASS (~8s wall-clock) |
| `WasmPublish_ProducesAppBundle` | `dotnet.js` + `flow-lang.dll` + `dotnet.native.wasm` all present at publish output | PASS |
| `WasmBundle_UncompressedSize_MeasuredAndRecorded` | Sum of browser-shipped artifacts (.dll/.wasm/.js/.dat/.flow/.json/.md) under 30 MB hard cap | PASS — 10.8 MB measured |

Modelled directly on Phase 47's `BuildConditioningSmokeTests.cs` with three differences:
1. `publish` instead of `build` (WASM artifacts are publish-only output)
2. 10-minute `WaitForExit(600_000)` (Mono-WASM publish slow — jiterpreter generation alone ~30s; full AOT cross-compile of System.Private.CoreLib can hit 2-3 min)
3. `LocateWasmFrameworkDir` helper handles both library-publish flat layout AND Blazor-app `AppBundle/_framework/` nesting (forward-compatible)

All 3 tests run from the Desktop test runner — they shell out to a separate `dotnet publish` process, so they execute regardless of FLOW_WEB.

## Acceptance Criteria — All Pass

| Criterion | Status |
|-----------|--------|
| `<RuntimeIdentifier>browser-wasm</RuntimeIdentifier>` appears exactly once | PASS (grep = 1) |
| `<TrimMode>full</TrimMode>` appears exactly once | PASS (grep = 1) |
| `<InvariantGlobalization>true</InvariantGlobalization>` appears exactly once | PASS (grep = 1) |
| `<TrimmerRootDescriptor Include=...trim-roots.xml />` exactly once | PASS (grep = 1) |
| `dotnet build -p:FlowTarget=Desktop` exits 0 | PASS |
| `dotnet build -p:FlowTarget=Web` exits 0 | PASS |
| `dotnet publish -p:FlowTarget=Web -c Release` exits 0 | PASS |
| `xmllint --noout trim-roots.xml` exits 0 | PASS |
| trim-roots.xml entries between 25 and 50 | PASS (42) |
| `grep -c '<assembly fullname="flow-lang">' trim-roots.xml` returns 1 | PASS |
| `grep -c 'FlowLang.TypeSystem.SpecialTypes.NoteType' trim-roots.xml` returns 1 | PASS |
| `grep -c 'FlowLang.StandardLibrary.Audio.AudioBuffer' trim-roots.xml` returns 1 | PASS |
| `grep -c 'FlowLang.Runtime.Value' trim-roots.xml` returns 1 | PASS |
| `grep -c 'FlowLang.Audio.WebAudioBackend' trim-roots.xml` returns 1 | PASS |
| `grep -c 'SfzType' trim-roots.xml` returns 0 (stripped on Web) | PASS |
| `grep -c 'OscHandleType' trim-roots.xml` returns 0 (stripped on Web) | PASS |
| `grep -c 'InternalFunctionRegistry' trim-roots.xml` returns 0 (PATTERNS Discrepancy 3) | PASS |
| 3 [Fact] attributes in WasmBuildPipelineTests.cs | PASS (3) |
| `600_000` timeout in test file | PASS |
| Phase 48 test fixture: 3/3 PASS | PASS |
| Phase 47 fixture preserved (16 PASS + 8 SKIP + 0 FAIL on Desktop) | PASS |

## Deviations from Plan

### Rule 3 Auto-fixes (blocking issues)

**1. [Rule 3 - Blocking] `wasm-tools` workload missing**
- **Found during:** Task 1 verify (first `dotnet build -p:FlowTarget=Web`)
- **Issue:** `error NETSDK1147: To build this project, the following workloads must be installed: wasm-tools`
- **Fix:** Ran `dotnet workload install wasm-tools` (installed v10.0.7), then `dotnet workload restore flow-lang/flow-lang.csproj -p:FlowTarget=Web` which upgraded to the SDK-band-matching v10.0.8.
- **Why automatic:** Orchestrator's critical_context explicitly authorized this: "If `dotnet publish` errors with `Microsoft.NET.Sdk.WebAssembly.Browser` missing, Task 1 should detect this and either: (a) Run `dotnet workload install wasm-tools` and retry, OR (b) Document the workload requirement..." — we did both.
- **Developer prerequisite documented:** Future contributors building the Web target will need `dotnet workload install wasm-tools` once per machine. To be documented in CLAUDE.md `## Build & Run Commands` section by Plan 48-07 closer.
- **Commit:** N/A (workload install is a machine-level action, not a repo commit)

**2. [Rule 3 - Blocking] PLAN.md must-have expected Blazor-app AppBundle layout, but flow-lang.csproj is a library**
- **Found during:** Task 3 (dry-run publish before writing tests)
- **Issue:** Plan must-have: `Published AppBundle directory exists at flow-lang/bin/Release/net10.0/browser-wasm/publish/AppBundle/ and contains _framework/dotnet.js`. Actual library-publish layout: `bin/Release/net10.0/browser-wasm/publish/dotnet.js` (flat — no `AppBundle/_framework/` nesting). This is because `Microsoft.NET.Sdk.WebAssembly.Browser` (Blazor app SDK) produces the nested layout but `Microsoft.NET.Sdk` (library SDK with browser-wasm RID) produces flat.
- **Fix:** Test's `LocateWasmFrameworkDir` helper checks BOTH paths — flat library layout first (the actual current behavior), then Blazor-app nesting (forward-compatible if Phase 49 turns this into a Blazor app SDK consumer or if SDK behavior changes).
- **Why automatic:** Plan's spirit ("publish exits 0 AND produces a usable WASM bundle") satisfied; only the specific filesystem path differs. Rule 3 auto-fix scope.
- **Commit:** `447662f`

### Rule 1 Auto-fixes (bugs)

None.

### Rule 2 Auto-fixes (missing critical functionality)

None.

### Rule 4 Architectural changes

None.

## Authentication Gates

None. Plan executed fully autonomously per `autonomous: true` frontmatter.

## Bundle Size Detail

| Category | Bytes | MB | Notes |
|----------|-------|----|----|
| Total publish output (all files) | 41,356,459 | 39.4 MB | Includes everything in publish/ |
| **Browser-shipped artifacts** | **10,796,004** | **10.3 MB** | `.dll` + `.wasm` + `.js` + `.dat` + `.flow` + `.json` + `.md` — what the browser actually loads |
| Static archives (`.a` files) | 29,551,972 | 28.2 MB | Build-only (Emscripten link inputs) — NOT shipped |
| Build artifacts (`.c`/`.h`/`.rsp`) | 50,755 | 0.05 MB | Emscripten driver scaffolding — NOT shipped |

**Bundle size cap analysis (D-48-05):**

- 15 MB compressed target ≈ 30 MB uncompressed after Brotli
- Current: 10.3 MB shipped uncompressed → ~5 MB Brotli'd (estimate based on typical 2:1 ratio for IL + WASM)
- **Margin to 30 MB hard cap: 19.7 MB (65.7% headroom)**
- **Margin to 15 MB compressed target: ~10 MB**

Plan 48-05 lazy-loading **deferred** unless v1.6 budget changes — comfortable headroom on both axes.

## Trimmer Warnings (Phase 48 Carry-forward)

One `IL2075` trim-analysis warning surfaces at publish time (also visible at `FlowTarget=Web` build time once `TrimMode=full` is active):

```
flow-lang/Interpreter/ExpressionEvaluator.cs(953,9): Trim analysis warning IL2075:
  FlowLang.Interpreter.ExpressionEvaluator.EvaluateMemberAccess(MemberAccessExpression):
  'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicProperties'
  in call to 'System.Type.GetProperty(String)'. The return value of method
  'System.Object.GetType()' does not have matching annotations.
```

**Source:** `flow-lang/Interpreter/ExpressionEvaluator.cs:953`, `EvaluateMemberAccess`. The interpreter calls `someValue.GetType().GetProperty(name)` to support instance-member-access surface like `chord.Root`, `voice.Pan`, `song.SectionCount`. This is the ONE reflection site in flow-lang's hot path.

**Tracked for Plan 48-05:** Either (a) add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]` annotations on the receiver types (Voice/Chord/Song/etc.) so trim can preserve their property metadata, OR (b) replace the `GetProperty` reflection with a hand-rolled switch dispatch keyed by the type-of-receiver + property-name (eliminates reflection entirely). Decision deferred — depends on whether Plan 48-04 cross-browser tests hit the codepath at runtime.

**NOT a blocker for Plan 48-01** — the publish succeeds, the warning is informational, and at most it means `chord.Root` etc. might fail at runtime under aggressive trim. Plan 48-03 will exercise the path at runtime; if it breaks, the fix lands there.

## Performance Notes

- First `dotnet publish` (cold workload restore + AOT cross-compile): ~30s
- Second `dotnet publish` (warm Emscripten cache): ~8s
- Test run (3 Facts, each invokes a separate `dotnet publish`): ~12s total (warm cache after first)
- **Test budget:** 3 × 10-min timeout = 30 min hard ceiling per run. Actual wall-clock comfortably under at ~12s.

## Phase 48 Status After Plan 01

- Plan 48-01 ✓ COMPLETE
- Plans 48-02..48-07 → unblocked (foundation pinned in xUnit)

The load-bearing question of Phase 48 — "does Mono-WASM publish of flow-lang actually work?" — is answered YES, end-to-end, in xUnit. Every subsequent Plan 48-NN can now assume a working `dotnet publish -p:FlowTarget=Web -c Release` pipeline.

## Self-Check: PASSED

Verified before completion:

- `flow-lang/flow-lang.csproj` — modified, 6 new properties + 1 TrimmerRootDescriptor inside existing Phase 47 conditional blocks: FOUND
- `flow-lang/wasm/trim-roots.xml` — created, 74 lines, 42 preserve="all" entries, xmllint valid: FOUND
- `flow-lang.Tests/Integration/Phase48/WasmBuildPipelineTests.cs` — created, 161 lines, 3 [Fact]s: FOUND
- Commit `74ac158` (Task 1 csproj): FOUND in git log
- Commit `c6dc20d` (Task 2 trim-roots.xml): FOUND in git log
- Commit `447662f` (Task 3 WasmBuildPipelineTests): FOUND in git log
- `dotnet publish -p:FlowTarget=Web -c Release` exits 0: VERIFIED (~8s wall-clock)
- Bundle size 10.8 MB shipped under 30 MB cap: VERIFIED
- Phase 48 fixture: 3/3 PASS, Phase 47 fixture: 16 PASS + 8 SKIP + 0 FAIL (preserved): VERIFIED
