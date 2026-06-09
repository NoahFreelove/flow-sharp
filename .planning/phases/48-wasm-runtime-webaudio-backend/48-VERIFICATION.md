# Phase 48: WASM Runtime + WebAudioBackend — Verification

**Phase:** 48
**Status:** Shipped 2026-06-05
**Plans:** 7/7 complete
**Requirements:** 15 closed (REQ-WASM-BUILD-01..05 + REQ-WEBAUDIO-01..04 + REQ-WASM-API-01..03 + REQ-WASM-DRYWET-01 + REQ-WASM-SIZE-01 + REQ-WASM-DET-01)

## Outcome Summary

Phase 48 is the first runnable browser build of Flow. It compiles `flow-lang.dll` under
.NET 10 Mono-WASM via the Phase 47 `FlowTarget=Web` conditioning, ships a real
`WebAudioBackend` that drives the browser `AudioContext` through `[JSImport]`/`[JSExport]`,
and freezes a small ES-module API (`flow-runtime.js`) for Phase 49's SvelteKit playground
tab to consume. This was the single biggest feasibility risk in the v1.5 milestone —
research surfaced no .NET-in-WASM prior art for AudioWorklet driving, so v1 ships the
conservative offline-render → `AudioBuffer` → `AudioBufferSourceNode` pattern (D-48-01).
That risk is now cleared: a composer in Firefox heard a 440 Hz tone come out of the browser
on a Run-button click, autoplay-correct (silent until the gesture).

`WebAudioBackend` swaps Phase 47's seven stub-throw method bodies for live Mono-WASM
implementations: lazy one-AudioContext-per-engine lifecycle (D-48-08), constant-power
mono→stereo promotion before marshal (D-48-07), and a charitable `IsAvailable()` /
`Initialize()` fallback on non-browser hosts (D-48-11). `WasmEntry.cs` exposes four
`[JSExport]` methods plus the `RunResult` / `RunError` POCOs with structured-error
marshalling (D-48-14) and a stdout/stderr split (D-48-15), serialized through a
source-generated `JsonSerializerContext` so the camelCase + null-omission JSON survives
`TrimMode=full`.

Bundle size came in well under budget — **3.07 MB compressed Brotli** (10.99 MB
uncompressed) at the canonical Plan 48-05 measurement, against a 15 MB D-48-05 target and
a 20 MB hard cap → MONOLITHIC SHIP, no lazy-load needed for v1.5. (The on-disk
`48-BUNDLE-SIZE.md` was auto-regenerated 2026-05-31 during the boot-fix work and records an
even smaller **1.63 MB Brotli / 5.38 MB uncompressed** for the post-fix Webcil AppBundle
layout — both numbers are comfortably under budget; see Known Caveat 3.) HUMAN-UAT closed
APPROVED-WITH-FOLLOWUP: **1/3 browser rows PASS (Firefox), 1 deferred (Chrome audio
re-smoke), 1 skipped (Safari, no macOS)**.

## Build Surface

```bash
# Publish the browser-WASM AppBundle (Release):
dotnet publish flow-lang/flow-lang.csproj -p:FlowTarget=Web -c Release
# → flow-lang/bin/Release/net10.0/browser-wasm/AppBundle/

# Serve locally + boot in a browser:
cd flow-lang/bin/Release/net10.0/browser-wasm/AppBundle/
python3 -m http.server 8080
# visit http://localhost:8080/index.html  → click Run → audible 440 Hz tone
```

The published AppBundle layout (verified on disk, post-boot-fix):

```
AppBundle/
  flow-runtime.js        ← hand-written ES module (the Phase 49 API surface)
  index.html             ← dev-smoke harness (NOT shipped to flowlang.dev)
  package.json           ← { "type": "module" }
  _framework/
    dotnet.js            ← Mono-WASM loader entry
    dotnet.boot.js       ← boot manifest (mainAssemblyName: flow-lang.dll); HTTP 200
    dotnet.native.wasm   ← Mono runtime
    flow-lang.wasm       ← Webcil-encoded main assembly
    System.*.wasm        ← Webcil-encoded framework assemblies
```

A SvelteKit consumer (Phase 49 preview) copies `AppBundle/` into `static/wasm/` and
dynamically imports `flow-runtime.js` on the playground tab — see `48-PHASE49-HANDOFF.md`.

## Composer-facing behavior on Web target (extends Phase 47)

- `(play buffer)` → routes to the real `WebAudioBackend` → audible via browser `AudioContext`.
- `(print "...")` → captured in `RunResult.stdout`, surfaced to the JS caller (D-48-15).
- Advisories `[X] ...` (charitable-interpretation messages) → captured in `RunResult.stderr` (D-48-15).
- Parse / eval / runtime errors → `RunResult.errors[]` structured array (D-48-14; `{ kind, message, line?, column?, sourceSnippet? }`).
- 30s wall-clock cap (D-48-10) → **hard cap on Desktop; best-effort (synchronous, non-preemptive) in single-threaded WASM** — a runaway script hangs its own tab, exactly like synchronous single-threaded JS. The `"cancel"` `RunError.kind` stays DEFINED in the contract but is not raised in-browser (see Known Caveat 1).
- `use "@sfz"` / `use "@osc"` → ModuleLoader charitable advisory (Phase 47 D-47-09); stripped stdlib procs (`micBuffer` / `loadSfz`) charitably skipped on Web (commit `b46589c`).
- `live { ... }` block → parse-time error inherited from Phase 47 D-47-09.

## Requirement Closure Table

| REQ-ID | Description | Closure |
|--------|-------------|---------|
| REQ-WASM-BUILD-01 | `dotnet publish -p:FlowTarget=Web -c Release` produces a browser-WASM AppBundle; bundle size measured + recorded | Plan 48-01 commits `74ac158` + `447662f`; AppBundle emit fixed in Plan 48-06 commit `08140bb` |
| REQ-WASM-BUILD-02 | `trim-roots.xml` (`TrimmerRootDescriptor`) preserves FlowType subclasses + 21 SpecialTypes + 16 PrimitiveTypes + ArrayType + AudioBuffer + Value + WebAudioBackend (PATTERNS.md Discrepancy 3 — NOT InternalFunctionRegistry, which has zero reflection use) | Plan 48-01 commit `c6dc20d` |
| REQ-WASM-BUILD-03 | `WasmEnableJiterpreter` + `InvariantGlobalization` + `HybridGlobalization=false` + `TrimMode=full` enabled under FlowTarget=Web (D-48-01..03) | Plan 48-01 commit `74ac158` |
| REQ-WASM-BUILD-04 | `WasmEmitSymbolMap` gated to Debug only (D-48-04) | Plan 48-01 commit `74ac158` |
| REQ-WASM-BUILD-05 | 3 culture-sensitive call sites → `*Invariant` (HarmonyFunctions:441 + ScaleDatabase:182,233) for invariant-globalization mode; `CultureInvariantSweepTests` CI gate (D-48-03) | Plan 48-02 commits `8ab1de6` + `a4f726d` |
| REQ-WASM-DRYWET-01 | DryWetMidi 8.0.3 WASM-compat smoke green via `DryWetMidiWasmPublishTests` (Mono.Cecil scan of POST-PUBLISH `flow-lang.dll` confirms the reference is retained reachably); `writeMidi` ships on Web (D-48-17 confirmed branch) | Plan 48-02 commit `a4f726d` |
| REQ-WEBAUDIO-01 | `FlowRuntimeInterop.cs` with 5 `[JSImport(..., "flow-runtime")]` partial-static methods (createAudioContext / playStereoFloat32 / stopSource / closeContext / resumeContext) | Plan 48-03 commit `2e15d22` |
| REQ-WEBAUDIO-02 | `WebAudioBackend` stub-to-real swap; Phase 47 D-47-05 IAudioBackend signatures pinned byte-identical; Phase 47 stub tests retired | Plan 48-03 commits `4c7792a` + `028a5e5` |
| REQ-WEBAUDIO-03 | Mono→stereo promotion via `PromoteToStereo` before marshal (D-48-07); 30s wall-clock cap via Task.Run + Wait on Desktop, amended to synchronous in single-threaded WASM (D-48-10) | Plan 48-03 commit `4c7792a`; sync amendment Plan 48-06 commits `a8c1911` + `805269c` |
| REQ-WEBAUDIO-04 | Charitable `IsBrowser()` / `IsAvailable()` false-return + log-on-Initialize-failure fallback (D-48-11); Dispose never throws | Plan 48-03 commit `4c7792a` |
| REQ-WASM-API-01 | `flow-runtime.js` ES module exports `loadFlowRuntime()` + 4 runtime methods + `resumeAudio()` convenience (D-48-12/13) | Plan 48-04 commit `9afdb26` |
| REQ-WASM-API-02 | `WasmEntry.cs` 4 `[JSExport]` methods (RunFromJs / PlayFromJs / StopFromJs / DisposeFromJs); `RunResult` + `RunError` POCOs (D-48-14); stdout/stderr split (D-48-15); source-gen JSON context for trim-safe serialization | Plan 48-04 commit `dad84b9`; source-gen JSON Plan 48-06 commit `5b80c01` |
| REQ-WASM-API-03 | `index.html` dev-smoke harness + Web-conditional `CopyToPublishDirectory` for flow-runtime.js + index.html; trim-roots entries for WasmEntry/RunResult/RunError | Plan 48-04 commit `9afdb26` |
| REQ-WASM-SIZE-01 | Bundle size measured + recorded in `48-BUNDLE-SIZE.md`; **3.07 MB compressed** (Plan 48-05 canonical) / 1.63 MB post-boot-fix re-measure; D-48-05 branch = MONOLITHIC SHIP | Plan 48-05 commits `b2645d5` + `538834c` |
| REQ-WASM-DET-01 | Two-run cmp-clean determinism preserved via `WasmDeterminismTests` (RunResult JSON byte-identical excluding `durationMs`) | Plan 48-05 commit `538834c` |

## Acceptance Evidence

### 1. Desktop build byte-identical (Phase 47 baseline preserved)

```
$ dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Desktop -v quiet
    0 Error(s)
```
No production code touched by the closer; Phase 47 + 48 Desktop behavior unchanged.

### 2. Web build links cleanly (Plan 48-01..06 acceptance)

```
$ dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web -v quiet
    24 Warning(s) (pre-existing trim-analysis IL warnings)
    0 Error(s)
```

### 3. WASM publish produces a bootable AppBundle (Plan 48-01 + 48-06)

`dotnet publish -p:FlowTarget=Web -c Release` exits 0 and emits `AppBundle/` with
`flow-runtime.js` + `index.html` + `package.json` at root and `dotnet.js` +
`dotnet.boot.js` + `flow-lang.wasm` + `System.*.wasm` under `_framework/`. The
boot manifest now serves HTTP 200 (`curl /_framework/dotnet.boot.js`), resolving the
2026-05-30 404 boot-blocker. The original library-publish-skips-app-bundle root cause was
fixed in Plan 48-06 commit `08140bb` (gated to the publish phase by `35dd537`).

### 4. WebAudioBackend stub-to-real swap (Plan 48-03)

`WebAudioBackendIntegrationTests` (8 Facts, GREEN on Desktop) pins the pure-C# helpers and
Desktop fallback paths: `MonoInput_PromotesToStereo_LengthDoubles` (D-48-07),
`PromoteToStereo_StereoInput_PassesThrough`, `Dispose_IsNoOpSafe_OnDesktop`,
`Initialize_ReturnsFalse_OnDesktop_CharitableFallback` (D-48-11),
`WriteChunk_ThrowsNotSupportedException_OnAnyTarget` (D-48-01), plus Name / IsInitialized /
IsAvailable carryforwards. The `[JSImport]`-backed runtime behavior is HUMAN-UAT (Plan 48-06).

### 5. Bundle size outcome (Plan 48-05)

`BundleSizeBudgetTests` (2 Facts) shells the publish, Brotli-compresses every
browser-shipped artifact at `CompressionLevel.SmallestSize`, and hard-asserts the total
under the 20 MB cap. Canonical Plan 48-05 measurement: **3,074,392 bytes (3.07 MB) Brotli /
10,991,903 bytes uncompressed**. Top contributors: `dotnet.native.wasm`,
`System.Private.CoreLib.wasm`, `flow-lang.wasm`. D-48-05 branch auto-selected: MONOLITHIC
SHIP. `48-BUNDLE-SIZE.md` self-regenerates on every test run (latest on-disk re-measure:
1.63 MB Brotli / 5.38 MB uncompressed for the post-boot-fix Webcil layout).

### 6. Two-run determinism preserved (Plan 48-05)

`WasmDeterminismTests` (2 Facts): `SameSource_TwoRuns_IdenticalStdout` +
`SameSource_TwoRuns_IdenticalRunResultJson` (strips the legitimate `durationMs` jitter field,
then byte-compares the remaining JSON). Both GREEN — the D-48-16 contract holds for the Web
target. Source is pure arithmetic + print, so the D-36-09 chaos-primitive cross-platform
caveat does not apply to this fixture.

### 7. HUMAN-UAT browser smoke (Plan 48-06)

| Row | Browser | OS | Sine tone audible? | Autoplay-correct? | Sign-off |
|-----|---------|----|--------------------|--------------------|----------|
| 1 | Chrome 120+ | Linux | not re-tested (boot blocker fixed + HTTP-verified) | n/a | **DEFERRED** → v1.6 |
| 2 | Firefox 121+ | Linux | **YES** — 440 Hz tone on Run click | **yes** — silent until gesture (D-48-09) | **PASS 2026-06-05** |
| 3 | Safari 17+ | macOS | n/a | n/a | **SKIPPED** — no macOS available |

Firefox PASS is the load-bearing evidence: .NET-in-WASM → browser `AudioContext` audio
works end-to-end. Per Closure Conditions ("signed off as pass OR documented gotcha
non-blocking"), this closes Phase 48 HUMAN-UAT. Chrome/Safari follow-ups routed to the v1.6
backlog by this closer (`.planning/MILESTONES.md`).

## Plan Summary

| Plan | Wave | Description | Outcome |
|------|------|-------------|---------|
| 48-01 | 1 | WASM build pipeline foundation (csproj + trim-roots.xml + WasmBuildPipelineTests) | Shipped |
| 48-02 | 2 | DryWetMidi WASM publish smoke + culture-invariant sweep (D-48-17 confirmed, D-48-03) | Shipped |
| 48-03 | 3 | WebAudioBackend real impl ([JSImport] + stereo promotion + 30s cap) | Shipped |
| 48-04 | 4 | flow-runtime.js ES module + WasmEntry.cs [JSExport] + index.html dev harness | Shipped |
| 48-05 | 5 | Bundle size budget + two-run determinism pin | Shipped |
| 48-06 | 5 | HUMAN-UAT browser smoke + 9-commit in-phase boot-blocker repair | Shipped (APPROVED-WITH-FOLLOWUP) |
| 48-07 | 6 | Closer: VERIFICATION + Phase 49 handoff + planning-artifact flips | Shipped |

## Decision Trace (D-48-NN)

| D-NN | Decision | Outcome |
|------|----------|---------|
| D-48-01 | Offline-render → AudioBuffer path, NOT AudioWorklet (Mono-WASM jiterpreter) | Confirmed; Plan 48-03 ships offline-render via WebAudioBackend.Play; AudioWorklet deferred to v1.6 |
| D-48-02 | `TrimMode=full` + explicit `TrimmerRootDescriptor` | Confirmed; PATTERNS.md Discrepancy 3 reconciled — descriptor preserves FlowType subclasses + AudioBuffer + Value + WebAudioBackend, NOT InternalFunctionRegistry (zero reflection use, audit-confirmed) |
| D-48-03 | `InvariantGlobalization=true` + `HybridGlobalization=false` | Confirmed; 3 ToUpper/ToLower sites → `*Invariant`; `CultureInvariantSweepTests` CI gate; Turkish-I hazard closed |
| D-48-04 | `WasmEmitSymbolMap=true` Debug-only | Confirmed; Release strips the symbol map |
| D-48-05 | Lazy-load Phase 36/39 stdlibs only if bundle > 15 MB | NOT NEEDED — MONOLITHIC SHIP at 3.07 MB compressed; lazy-load reserved for v1.6 |
| D-48-06 | `[JSImport]`/`[JSExport]` for the JS↔C# boundary | Confirmed; FlowRuntimeInterop (5 imports) + WasmEntry (4 exports); source-gen JSON added (Plan 48-06) for trim-safety |
| D-48-07 | Stereo audio always (mono promoted before marshal) | Confirmed; `PromoteToStereo` runs before `MemoryMarshal.AsBytes` marshal; matches Phase 37 B2 LOCK |
| D-48-08 | One AudioContext per FlowEngine, lazy on first play | Confirmed; cached in flow-runtime.js `_audioContext`; closed on dispose |
| D-48-09 | `resume()` inside the user-gesture chain (backend never calls it) | Confirmed; `resumeAudio()` convenience creates+resumes the context in the gesture frame (Plan 48-06 commit `a5ae19f`); Firefox UAT confirmed autoplay-correct |
| D-48-10 | 30s wall-clock evaluation cap | AMENDED — hard cap on Desktop, best-effort (synchronous, non-preemptive) in single-threaded WASM (Task.Run+Wait deadlocks the one thread); `"cancel"` kind stays defined but not raised in-browser |
| D-48-11 | `IsAvailable()` = `IsBrowser()` && JSInterop available; charitable fallback | Confirmed; Desktop returns false; Initialize logs + returns false charitably |
| D-48-12 | `flow-runtime.js` is an ES module (not UMD/CommonJS) | Confirmed; `package.json` `{ "type": "module" }`; SvelteKit consumes natively |
| D-48-13 | Minimal API surface (5 exports + dispose) | Confirmed; loadFlowRuntime + run + play + stop + dispose + resumeAudio convenience |
| D-48-14 | Errors bubble structured, not strings | Confirmed; `RunError { kind, message, line?, column?, sourceSnippet? }`; `kind` ∈ parse/eval/runtime/cancel/platform-not-supported |
| D-48-15 | stdout + stderr captured separately | Confirmed; per-call StringWriter redirect with finally-restore (T-48-14) |
| D-48-16 | Two-run cmp-clean determinism preserved | Confirmed; `WasmDeterminismTests` strips `durationMs`, byte-compares the rest |
| D-48-17 | DryWetMidi WASM-compat verified at Plan 48-02 | Confirmed COMPATIBLE branch — reference retained reachably (Mono.Cecil post-publish scan); `writeMidi` ships on Web; no hand-rolled fallback needed |
| D-48-18 | If DryWetMidi compatible, MIDI download = Uint8Array in RunResult.midi | Confirmed contract; `RunResult.Midi` field present; Phase 49 wires Blob download |
| D-48-19 | MusicXML / LilyPond / ABC / MML all WASM-compatible (hand-rolled, no deps) | Confirmed by inspection; `@notation-io` stays available on Web; strings → Phase 49 download UI |

## Known Caveats

### Caveat 1: D-48-10 30s cap is non-preemptive in single-threaded WASM

The hard 30s wall-clock cap is enforceable only on Desktop. Mono-WASM is single-threaded by
default (dotnet/runtime#85592), so the Phase 38-era `Task.Run + Wait(30s)` pattern deadlocks
(the worker task queues to the one main thread that `Wait` then blocks). Plan 48-06 rewrote
`RunFromJs` and `WebAudioBackend.Play` to run synchronously on the calling thread (commits
`a8c1911` + `805269c`). The accepted tradeoff: a runaway Flow script hangs its own browser
tab exactly like any synchronous single-threaded JS. The `"cancel"` `RunError.kind` stays
DEFINED in the D-48-14 contract (field names + kinds are pinned for JS + tests) but is no
longer raised in-browser. v1.6 worker-thread WASM (with COOP/COEP) could restore preemption.

### Caveat 2: Chrome audio re-smoke + Safari smoke deferred to v1.6

HUMAN-UAT closed APPROVED-WITH-FOLLOWUP with Firefox PASS. Chrome's original boot blocker
(`dotnet.boot.js` 404) was ROOT-CAUSED + FIXED + HTTP-verified, but the human audio
ear-check was not re-run in Chrome this session (Firefox proves the same engine path, so it
is non-blocking). Safari was SKIPPED — no macOS on the Linux-only dev machine. Both routed to
the v1.6 backlog in `.planning/MILESTONES.md` by this closer.

### Caveat 3: Bundle size — two valid measurements

The canonical Plan 48-05 measurement (2026-05-26) is **3.07 MB Brotli / 10.99 MB
uncompressed** — the figure every Plan 48-05/48-06 SUMMARY cites and the basis for the
MONOLITHIC SHIP decision. `48-BUNDLE-SIZE.md` self-regenerates on each test run and was last
overwritten 2026-05-31 during the boot-fix work, recording **1.63 MB Brotli / 5.38 MB
uncompressed** for the post-fix Webcil AppBundle layout (full-trim Webcil encoding shrank the
shipped `.wasm` set). Both are far under the 15 MB target; the smaller post-fix number only
strengthens the MONOLITHIC SHIP conclusion. No lazy-load needed for v1.5.

### Caveat 4: Cross-platform chaos-primitive determinism (D-36-09 carryforward)

`lorenz` / `logistic` chaos primitives remain same-platform-deterministic only — chained
FP arithmetic diverges across CPU/browser FP implementations after ~50 iterations. The
WasmDeterminismTests fixture uses pure arithmetic + print (no chaos), so it holds
cross-platform; but any Phase 49 cross-browser byte-cmp of Lorenz/logistic output MUST be
excluded from shared-baseline comparison per the CLAUDE.md Conventions contract.

### Caveat 5: flow-runtime.js relative-path layout is AppBundle-shaped

`flow-runtime.js` imports `./_framework/dotnet.js` (descends into the boot dir). The verified
post-fix serve path is `browser-wasm/AppBundle/` + `/index.html` (NOT the pre-fix
`publish/` + `/wasm/index.html` documented in the original 48-HUMAN-UAT.md Setup block — left
as a historical note with a correction pointer). Phase 49 must preserve the AppBundle's
`flow-runtime.js`-at-root + `_framework/`-sibling layout when copying into SvelteKit `static/`.

## Test Suite Outcome

Phase 48 added the following xUnit Facts across its plans:

| Plan | Test file | Facts |
|------|-----------|-------|
| 48-01 | `WasmBuildPipelineTests` | 3 |
| 48-02 | `DryWetMidiWasmPublishTests` (2) + `CultureInvariantSweepTests` (2) | 4 |
| 48-03 | `WebAudioBackendIntegrationTests` | 8 |
| 48-04 | (no new Facts — surface ships; HUMAN-UAT in 48-06) | 0 |
| 48-05 | `BundleSizeBudgetTests` (2) + `WasmDeterminismTests` (2) | 4 |
| 48-06 | (HUMAN-UAT browser smoke — human checkpoint, not xUnit) | 0 |
| **Total** | | **19 new Facts** |

Phase 48 fixture: **19/19 PASS** on Desktop. Phase 47 fixture preserved (9 PASS / 8 SKIP / 0
FAIL on Desktop; the 8 SKIP are `[FlowTargetFact("Web")]` Facts). Plan 48-03 retired 7
Phase 47 stub-throw Facts (inverted by the stub-to-real swap) and added 8 — coverage strictly
improved. Zero new NuGet packages across all 7 plans (Mono-WASM ships in the .NET 10 SDK;
Brotli + System.Text.Json + JsonNode + `[JSImport]`/`[JSExport]` are all BCL).

## Next Steps

- Phase 49 (flowlang.dev SvelteKit site) unblocked. `/gsd:plan-phase 49` consumes
  `48-PHASE49-HANDOFF.md` (the flow-runtime.js API contract) + the published AppBundle.
- v1.6 backlog (logged in `.planning/MILESTONES.md`): Chrome/Chromium WASM audio re-smoke;
  Safari WASM smoke; AudioWorklet + SharedArrayBuffer streaming (D-48-02); NativeAOT-LLVM
  via InternalFunctionRegistry source-gen (D-v1.5-02); worker-thread WASM for a real
  preemptive 30s cap.
