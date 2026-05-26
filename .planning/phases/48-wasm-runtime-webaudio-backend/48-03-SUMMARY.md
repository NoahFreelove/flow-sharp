---
phase: 48-wasm-runtime-webaudio-backend
plan: 03
subsystem: webaudio-backend
tags: [wasm, webaudio, jsimport, jsexport, audio-context, mono-wasm, interop]
requirements: [REQ-WEBAUDIO-01, REQ-WEBAUDIO-02, REQ-WEBAUDIO-03, REQ-WEBAUDIO-04]
dependency-graph:
  requires:
    - "Plan 48-01 (FlowTarget=Web publish pipeline + trim-roots.xml preserving WebAudioBackend type)"
    - "Phase 47 D-47-05 (WebAudioBackend stub class with PINNED IAudioBackend signatures)"
    - "Phase 47 D-47-06 (AudioPlaybackManager Web-first probe ordering — unchanged in Phase 48)"
    - "Phase 47 D-47-07 (OperatingSystem.IsBrowser() JIT intrinsic — Pattern B runtime branching, NOT preprocessor)"
  provides:
    - "flow-lang/Audio/FlowRuntimeInterop.cs — 5 [JSImport] partial-static declarations binding C#↔JS for AudioContext lifecycle + stereo Float32 marshal + stop/close/resume (the boundary surface Plan 48-04's flow-runtime.js implements against)"
    - "flow-lang/Audio/WebAudioBackend.cs — real Mono-WASM implementation replacing Phase 47 stub bodies; lifecycle (one AudioContext per FlowEngine, lazy on Initialize); stereo promotion (D-48-07); 30s wall-clock cap (D-48-10); charitable Desktop fallback (D-48-11)"
    - "flow-lang.Tests/Integration/Phase48/WebAudioBackendIntegrationTests.cs — 8 plain [Fact]s pinning PromoteToStereo + Dispose-safety + Desktop fallback contract (all PASS cross-target)"
    - "<AllowUnsafeBlocks>true</AllowUnsafeBlocks> in flow-lang.csproj — required by [JSImport] source generator (SYSLIB1074); no runtime unsafe code executes on Desktop (every callsite OperatingSystem.IsBrowser()-gated)"
    - "Phase 47 WebAudioBackendStubTests.cs DELETED — its 3 stub-throw assertions inverted under Phase 48; the 4 still-valid Desktop invariants re-covered in the new Phase 48 file"
  affects:
    - "Plan 48-04 will materialize flow-lang/wasm/flow-runtime.js with setModuleImports('flow-runtime', {...}) wiring the 5 JS-side exports matching FlowRuntimeInterop's [JSImport] names"
    - "Plan 48-06 HUMAN-UAT browser smoke exercises the full audio path end-to-end ((play (createSineTone 440Hz 1.0 0.5)) → audible tone in Chrome/Firefox/Safari)"
    - "Phase 49 SvelteKit playground 'Run' button onclick handler calls both runtime.run(source) AND audioContext.resume() per D-48-09 — Phase 48 does NOT call resume() from PlayBuffer"
tech-stack:
  added: []
  patterns:
    - "[JSImport]/[JSExport] modern .NET 10 interop (D-48-06) — replaces Blazor's Microsoft.JSInterop pattern; BCL-provided attributes available on all targets; source-generator emits the marshalling shim regardless of target"
    - "Pattern B runtime branching via OperatingSystem.IsBrowser() (D-47-07) — NOT #if FLOW_WEB inside the class; entire file compiles on both targets; interop calls gated at runtime"
    - "Stereo promotion before [JSImport] marshal (D-48-07) — mono → 2-channel duplication in C# so JS-side has no channel-count branching; mirrors Phase 37 B2 LOCK posture"
    - "Pattern C 30-second wall-clock cap via Task.Run + Wait(TimeSpan) (D-48-10) — same shape as Phase 38 LIVE-02 LiveReloadManager:82,470-499"
    - "Pattern D RenderingDiagnostics.WarnOnce '[runtime]' prefix advisory family — new addition to the {live, target, tuning} family per CLAUDE.md ## Conventions"
    - "Span<byte> + MemoryMarshal.AsBytes workaround for SYSLIB1072 (source-generated JS interop does not support Span<float>) — zero-copy at boundary, JS-side reinterprets via new Float32Array(bytes.buffer, bytes.byteOffset, byteLength / 4) in Plan 48-04"
key-files:
  created:
    - "flow-lang/Audio/FlowRuntimeInterop.cs (118 LOC, 5 [JSImport] partials, [SupportedOSPlatform(\"browser\")])"
    - "flow-lang.Tests/Integration/Phase48/WebAudioBackendIntegrationTests.cs (145 LOC, 8 [Fact]s)"
  modified:
    - "flow-lang/Audio/WebAudioBackend.cs (78 LOC → 320 LOC; +6 instance fields; 7 stub-throw bodies replaced with real impl + PromoteToStereo public static helper)"
    - "flow-lang/flow-lang.csproj (+7 lines for <AllowUnsafeBlocks>true</AllowUnsafeBlocks> inside the top-level PropertyGroup — applies to both Desktop and Web)"
  deleted:
    - "flow-lang.Tests/Integration/Phase47/WebAudioBackendStubTests.cs (was 82 LOC, 7 [Fact]s — 3 stub-throw assertions inverted under Phase 48; 4 still-valid Desktop invariants re-covered in the new Phase 48 IntegrationTests)"
decisions:
  - "SYSLIB1072 workaround chosen: marshal Float32 samples as Span<byte> via MemoryMarshal.AsBytes (zero-copy reinterpret). Source-gen JS interop supports Span<byte>/Span<int>/Span<double> for [JSMarshalAs<JSType.MemoryView>] but NOT Span<float> directly. JS-side wraps the byte view as `new Float32Array(bytes.buffer, bytes.byteOffset, byteLength / 4)` — Plan 48-04 owns this side."
  - "<AllowUnsafeBlocks>true</AllowUnsafeBlocks> added to the top-level <PropertyGroup> (NOT only the FlowTarget=Web conditional) because the [JSImport] source generator emits the marshalling shim on every target. Desktop never EXECUTES it (every callsite is OperatingSystem.IsBrowser() gated) but the SOURCE must compile on both targets per Pattern B."
  - "Phase 47 WebAudioBackendStubTests.cs deleted outright (not inverted-in-place). Rationale: the file's name + class-level XMLdoc explicitly documented it as covering the *stub* contract. With the stub gone, the name is misleading. The 4 still-valid invariants on Desktop (IsAvailable=false, Dispose no-op, Name=WebAudio, IsInitialized=false-before-Initialize) are re-covered in the new Phase 48 IntegrationTests with up-to-date XMLdoc."
  - "WriteChunk throws NotSupportedException explicitly (not silent no-op). D-48-01 makes offline-render canonical for v1; streaming is v1.6 backlog per D-48-02. A noisy throw at first call is more discoverable than a silent skip — composers hitting it learn the v1 scope boundary immediately."
  - "Initialize charitable fallback on Desktop returns false (not throws). D-48-11. AudioPlaybackManager.DetectBackend never picks WebAudioBackend on Desktop in production, but a hostile direct construction gets a clean false instead of PlatformNotSupportedException. Different from Phase 47 D-47-05 which threw — Phase 48 inverts this stub-throw to charitable-fallback because Phase 48 owns the real contract; throwing only on Desktop would conflate 'wrong host' with 'real init failed on Web'."
metrics:
  duration: "~9 minutes (start 2026-05-26T03:12:41Z, end 2026-05-26T03:21:39Z, 538 seconds wall-clock)"
  completed: 2026-05-26
  tasks: 3
  files_created: 2
  files_modified: 2
  files_deleted: 1
  loc_total: 583
  test_count_added: 8
  test_pass_added: 8
  test_fail_added: 0
  phase48_fixture_total: "15 PASS / 0 FAIL / 0 SKIP (was 7 — Plan 48-01 + 48-02; +8 from Plan 48-03)"
  phase47_fixture_total: "9 PASS / 8 SKIP / 0 FAIL (was 16 + 8; -7 from WebAudioBackendStubTests.cs deletion)"
  desktop_build_status: "exit 0"
  web_build_status: "exit 0"
  web_publish_status: "exit 0"
---

# Phase 48 Plan 03: WebAudioBackend Real Implementation Summary

## One-liner

WebAudioBackend stub bodies swapped for real Mono-WASM [JSImport]/[JSExport] implementation via a new FlowRuntimeInterop partial-static boundary class — 5 [JSImport] bindings (createAudioContext / playStereoFloat32 / stopSource / closeContext / resumeContext) bridge C# to the Plan 48-04 flow-runtime.js module; AudioContext lifecycle is lazy + one-per-engine (D-48-08); mono → stereo promotion happens in C# before marshal (D-48-07); 30-second wall-clock cap matches Phase 38 LIVE-02 pattern (D-48-10); Desktop fallback is charitable (D-48-11, returns false instead of throwing).

## Goal

Per Plan 48-03 objective: replace the Phase 47 `WebAudioBackend` stub method bodies with real Mono-WASM-driving implementations. Add a sibling `FlowRuntimeInterop.cs` partial-static class declaring the `[JSImport]` boundary surface. Wire AudioContext lifecycle (one per FlowEngine, lazy-created on Initialize), stereo promotion before marshal, and a 30-second `CancellationToken` wall-clock cap. Method signatures stay PINNED per Phase 47 D-47-05.

## What Shipped

### Task 1 — FlowRuntimeInterop.cs with 5 [JSImport] declarations (commit `2e15d22`)

New file `flow-lang/Audio/FlowRuntimeInterop.cs` (118 LOC). `internal static partial class FlowRuntimeInterop` decorated with `[SupportedOSPlatform("browser")]` so the C# compiler emits a CA1416 warning at any callsite invoked without a runtime `OperatingSystem.IsBrowser()` guard.

| [JSImport] | C# signature | JS name | Module | Purpose |
|------------|--------------|---------|--------|---------|
| `CreateAudioContext(int sampleRate)` | `→ JSObject` | `createAudioContext` | `flow-runtime` | Lazy per-engine AudioContext (D-48-08) |
| `PlayStereoFloat32(JSObject ctx, Span<byte>, int channels, int sampleRate)` | `→ JSObject` | `playStereoFloat32` | `flow-runtime` | One-shot Float32 marshal → AudioBufferSourceNode.start() |
| `StopSource(JSObject sourceNode)` | `→ void` | `stopSource` | `flow-runtime` | Revoke active node (idempotent) |
| `CloseContext(JSObject ctx)` | `→ void` | `closeContext` | `flow-runtime` | Dispose-time cleanup |
| `ResumeContext(JSObject ctx)` | `→ void` | `resumeContext` | `flow-runtime` | D-48-09 escape hatch for Phase 49 user-gesture chain (NEVER called from WebAudioBackend) |

**SYSLIB1072 reconciliation (auto-fix, see Deviations §Rule 3):** The plan body's PATTERNS.md sketch used `[JSMarshalAs<JSType.MemoryView>] Span<float> samples` for `PlayStereoFloat32`. The source generator does NOT support `Span<float>` directly — only `Span<byte>`, `Span<int>`, `Span<double>`. We marshal the Float32 samples as their raw byte view (via `MemoryMarshal.AsBytes(stereo.AsSpan())` at the WebAudioBackend callsite) and reinterpret on the JS side in Plan 48-04 via `new Float32Array(bytes.buffer, bytes.byteOffset, byteLength / 4)`. Zero-copy across the boundary per RESEARCH §5 invariant.

### Task 2 — WebAudioBackend.cs stub-to-real swap (commit `4c7792a`)

`flow-lang/Audio/WebAudioBackend.cs` grew from 78 LOC → 320 LOC. Phase 47 D-47-05 PINNED IAudioBackend signatures preserved byte-identical; only bodies changed:

| Method | Phase 47 stub | Phase 48 implementation |
|--------|--------------|-------------------------|
| `Initialize(rate, channels)` | `throw PNSE` | Lock + lazy `FlowRuntimeInterop.CreateAudioContext`; JSException caught + logged to stderr (T-48-11 mitigation); charitable false on Desktop (D-48-11) |
| `EnsureInitialized(rate, ch)` | `throw PNSE` | No-op if already initialized with matching params; else `Initialize` |
| `Play(float[], rate, ch, ct)` | `throw PNSE` | `PromoteToStereo` (D-48-07) → `Task.Run + Wait(30s)` (D-48-10) → `MemoryMarshal.AsBytes` → `FlowRuntimeInterop.PlayStereoFloat32`; stores returned `_activeSource` |
| `Stop()` | `throw PNSE` | Lock + `FlowRuntimeInterop.StopSource(_activeSource)`; idempotent; swallows JSException |
| `GetDevices()` | `throw PNSE` | Returns `["default"]` (WebAudio has no enumeration) |
| `SetDevice(name)` | `throw PNSE` | Accepts `"default"` (Ordinal compare); rejects anything else |
| `WriteChunk(...)` | `throw PNSE` | Throws `NotSupportedException` with v1.6 backlog reference per D-48-01 |
| `Dispose()` | true no-op | Lock + `FlowRuntimeInterop.CloseContext` + clears `_audioContext`/`_activeSource`; NEVER throws (swallows ALL exceptions per Phase 47 contract) |
| `IsInitialized` | `=> false` | `=> _audioContext != null && !_disposed` |
| **Added:** `PromoteToStereo(float[], int) → float[]` | n/a | Public static helper (test seam); reference-equal pass-through when `channels >= 2`; allocate + duplicate when mono |

**Six new instance fields** (lifecycle state per PulseAudioSimpleBackend analog):
```csharp
private JSObject? _audioContext;
private JSObject? _activeSource;
private int _sampleRate;
private int _channels;
private bool _disposed;
private readonly object _lock = new();
```

**Pattern B compliance:** Zero `#if FLOW_WEB` inside the class. Every `FlowRuntimeInterop.*` callsite is wrapped in `if (OperatingSystem.IsBrowser()) { #pragma warning disable CA1416; ...; #pragma warning restore CA1416; }`. Desktop still compiles cleanly; Desktop NEVER executes the interop branches.

**Phase 47 WebAudioBackendStubTests.cs deleted:** The file's 3 stub-throw assertions (`Initialize_ThrowsPlatformNotSupportedException`, `Play_ThrowsPlatformNotSupportedException_WithStubMessage`, `Stop_ThrowsPlatformNotSupportedException`) inverted under Phase 48 — Initialize charitably returns false on Desktop instead of throwing. The 4 still-valid Desktop invariants (`IsAvailable_ReturnsFalse_OnDesktop`, `Dispose_IsNoOp_DoesNotThrow`, `Name_IsWebAudio`, `IsInitialized_IsFalse_OnStub`) are re-covered in the new Phase 48 IntegrationTests.

### Task 3 — WebAudioBackendIntegrationTests.cs (commit `028a5e5`)

`flow-lang.Tests/Integration/Phase48/WebAudioBackendIntegrationTests.cs` (145 LOC, 8 `[Fact]`s — all PASS in ~36ms on Desktop):

| Fact | Asserts | Source |
|------|---------|--------|
| `MonoInput_PromotesToStereo_LengthDoubles` | mono → 2× length; per-index L=R=mono | D-48-07 base contract |
| `PromoteToStereo_StereoInput_PassesThrough` | `ReferenceEquals(stereo, promoted)` | Cheap-stereo optimization invariant |
| `Dispose_IsNoOpSafe_OnDesktop` | double-dispose without exception | Phase 47 D-47-05 carryforward |
| `Name_IsWebAudio` | `backend.Name == "WebAudio"` | Carryforward from deleted Phase 47 stub tests |
| `IsInitialized_IsFalse_BeforeInitializeCall` | `IsInitialized == false` before Init | D-48-08 lazy lifecycle |
| `IsAvailable_ReturnsFalse_OnDesktop` | `WebAudioBackend.IsAvailable() == false` on Desktop | D-47-07 carryforward |
| `Initialize_ReturnsFalse_OnDesktop_CharitableFallback` | `Initialize(44100, 2) == false` on Desktop (no exception) | D-48-11 |
| `WriteChunk_ThrowsNotSupportedException_OnAnyTarget` | NSE with "WriteChunk" + "v1.6" in message | D-48-01 explicit reject |

Plain `[Fact]` (NOT `[FlowTargetFact("Web")]`) — these exercise pure-C# helpers + Desktop-side fallback paths, NOT [JSImport]-backed runtime behavior. The full WebAudio end-to-end (Initialize succeeds + Play emits audible tone) is HUMAN-UAT in Plan 48-06 browser smoke.

## Acceptance Criteria — All Pass

| Criterion | Status |
|-----------|--------|
| `grep -c 'internal static partial class FlowRuntimeInterop'` returns 1 | **PASS** |
| `grep -c '\[JSImport('` returns 5 | **PASS** (5) |
| `grep -c 'SupportedOSPlatform("browser")'` returns 1 | **PASS** (1 attribute usage + 1 mention in XMLdoc — grep counts 2; intent of one declaration satisfied — see Deviations §grep-noise) |
| `grep -c 'JSMarshalAs<JSType.MemoryView>'` returns 1 | **PASS** |
| `grep -c '"flow-runtime"'` returns 5 in FlowRuntimeInterop.cs | **PASS** (5 [JSImport] usages + 1 XMLdoc mention — grep counts 6; intent satisfied) |
| `dotnet build flow-lang -p:FlowTarget=Desktop` exits 0 | **PASS** (0 Error, 8 Warning — pre-existing) |
| `dotnet build flow-lang -p:FlowTarget=Web` exits 0 | **PASS** (0 Error, 6 Warning — pre-existing including IL2075) |
| `dotnet publish flow-lang -p:FlowTarget=Web -c Release` exits 0 | **PASS** (Plan 48-01 acceptance preserved) |
| `grep -c 'throw new PlatformNotSupportedException' WebAudioBackend.cs` returns 0 | **PASS** |
| `grep -c 'public static float\[\] PromoteToStereo' WebAudioBackend.cs` returns 1 | **PASS** |
| `grep -c 'private JSObject?' WebAudioBackend.cs` >= 2 | **PASS** (2) |
| `grep -c 'FlowRuntimeInterop\.' WebAudioBackend.cs` >= 4 | **PASS** (6 — Create, Play, Stop, Close — multiple use sites) |
| `grep -cE '(NotSupportedException.*WriteChunk|WriteChunk.*NotSupportedException)' WebAudioBackend.cs` >= 1 | **PASS** (1) |
| `grep -c 'TimeSpan.FromSeconds(30)' WebAudioBackend.cs` returns 1 | **PASS** (1) |
| `grep -c '#if FLOW_WEB' WebAudioBackend.cs` returns 0 | **PASS** (Pattern B compliance) |
| `[Fact]` count in WebAudioBackendIntegrationTests >= 3 | **PASS** (8) |
| Phase 48 IntegrationTests PASS rate >= 3 | **PASS** (8/8) |
| `ReferenceEquals` fact (Fact 2) passes | **PASS** |
| Phase 48 fixture total | **PASS** (15 PASS / 0 FAIL / 0 SKIP) |
| Phase 47 fixture preserved (modulo deleted stub-tests) | **PASS** (9 PASS / 8 SKIP / 0 FAIL — was 16+8; -7 from WebAudioBackendStubTests delete; the 4 invariants re-covered) |
| WasmBuildPipelineTests no regression | **PASS** (3/3 PASS — Plan 48-01 invariant preserved) |

## Deviations from Plan

### Rule 3 Auto-fixes (blocking issues)

**1. [Rule 3 - Blocking] `[JSImport]` source generator requires `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` (SYSLIB1074)**

- **Found during:** Task 1 verify (first `dotnet build flow-lang -p:FlowTarget=Desktop`)
- **Issue:** `error SYSLIB1074: JSImportAttribute requires unsafe code. Project must be updated with '<AllowUnsafeBlocks>true</AllowUnsafeBlocks>'.` Plus `error CS0227: Unsafe code may only appear if compiling with /unsafe`.
- **Fix:** Added `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` to the top-level `<PropertyGroup>` in `flow-lang/flow-lang.csproj` (NOT the FlowTarget=Web conditional). Rationale: the [JSImport] source generator emits the marshalling shim on every target — the SOURCE must compile under both Desktop and Web. Desktop never EXECUTES it (every callsite is OperatingSystem.IsBrowser() gated per Pattern B), so no runtime unsafe code runs on Desktop.
- **Why automatic:** Pattern B + the [JSImport] BCL attribute being available-on-all-targets requires this. The diagnostic itself points to the exact fix.
- **Commit:** `2e15d22`

**2. [Rule 3 - Blocking] `Span<float>` is not supported by `[JSMarshalAs<JSType.MemoryView>]` source generator (SYSLIB1072)**

- **Found during:** Task 1 verify (first `dotnet build flow-lang -p:FlowTarget=Desktop`)
- **Issue:** `error SYSLIB1072: Type global::System.Span<float> is not supported by source-generated JavaScript interop. The generated source will not handle marshalling of parameter 'samples'.` The plan body's PATTERNS.md sketch (line 144) called for `[JSMarshalAs<JSType.MemoryView>] Span<float> samples`. The source generator supports only `Span<byte>` / `Span<int>` / `Span<double>` (and their `ArraySegment` equivalents) for MemoryView marshalling.
- **Fix:** Changed `PlayStereoFloat32`'s `samples` parameter to `Span<byte> samplesAsBytes`. WebAudioBackend.Play.cs marshals via `MemoryMarshal.AsBytes(stereo.AsSpan())` (zero-copy reinterpret). Plan 48-04 will wrap the byte view JS-side as `new Float32Array(bytes.buffer, bytes.byteOffset, byteLength / 4)`. The boundary stays zero-copy per RESEARCH §5; the on-the-wire shape is the same; only the C# type changes from semantic Float32 to raw bytes.
- **Why automatic:** SYSLIB1072 is a hard error from the source generator; no runtime fix possible. The PATTERNS.md sketch was conceptual, not tested — first contact with the source-gen surfaced the limitation.
- **Documented:** XMLdoc on `PlayStereoFloat32` cites SYSLIB1072 + the JS-side reinterpret expectation. Plan 48-04 will honor the contract.
- **Commit:** `2e15d22`

### Rule 1 Auto-fixes (bugs)

None.

### Rule 2 Auto-fixes (missing critical functionality)

None.

### Rule 4 Architectural changes

None. The plan body's PATTERNS.md sketch was conceptual; the SYSLIB1072 + SYSLIB1074 fixes are bookkeeping at the source-generator interface, not architectural drift.

### Stale-test cleanup (Plan acceptance criterion called out)

**Phase 47 `WebAudioBackendStubTests.cs` deleted (commit `4c7792a`).**

Per Plan 48-03 Task 2 acceptance criterion: *"The pre-existing Phase 47 WebAudioBackendStubTests either continue to pass OR Phase 48-03 supersedes them with a stale-test cleanup commit ... Executor's call; document in SUMMARY."*

**Decision: DELETE.** Rationale:
- The file's class-level XMLdoc explicitly documented it as covering the *stub contract*. With the stub bodies replaced by real implementations, the name + intent are misleading.
- 3 of its 7 facts inverted (the throw-assertions for Initialize/Play/Stop). Phase 48 makes those methods either succeed (browser) or charitably fall back (Desktop), never throw `PlatformNotSupportedException`.
- The remaining 4 facts cover invariants that survive on Desktop (IsAvailable=false, Dispose no-op, Name=WebAudio, IsInitialized=false-before-Initialize). These are re-covered with up-to-date XMLdoc in the new Phase 48 `WebAudioBackendIntegrationTests.cs`.

Net coverage delta: 7 facts removed, 8 facts added; 4 invariants preserved, 4 new contracts added (PromoteToStereo length/passthrough, Initialize-charitable-false, WriteChunk-NSE-explicit).

### Grep-noise on acceptance criteria

Two acceptance criteria in the plan body use `grep -c` counts that include XMLdoc-comment mentions:

- `grep -c 'SupportedOSPlatform("browser")'` returns 2 (1 attribute usage + 1 XMLdoc cross-reference) — plan said 1
- `grep -c '"flow-runtime"'` returns 6 in FlowRuntimeInterop.cs (5 [JSImport] usages + 1 XMLdoc cross-reference) — plan said 5

The intent (one `[SupportedOSPlatform("browser")]` attribute on the class; one `"flow-runtime"` per [JSImport] declaration) is satisfied. The doc-comment cross-references improve maintainability — future contributors reading the file understand the module-name handshake without grepping the source generator's output. Not blocking; documented here for completeness.

## Authentication Gates

None. Plan executed fully autonomously per `autonomous: true` frontmatter.

## Decisions Made

- **`<AllowUnsafeBlocks>true</AllowUnsafeBlocks> at top-level PropertyGroup**, not the FlowTarget=Web conditional. The [JSImport] source generator emits unsafe code on EVERY target; the conditional approach would have failed Desktop build. Desktop's compiled output is safe at runtime because every interop callsite is OperatingSystem.IsBrowser()-gated per Pattern B.

- **`Span<byte>` over `double[]` workaround for SYSLIB1072.** Considered `[JSMarshalAs<JSType.Array<JSType.Number>>] double[]` but that doubles memory + costs an explicit copy for the float→double widening. `Span<byte>` via `MemoryMarshal.AsBytes` is zero-copy + zero-allocation; JS-side reinterprets the same ArrayBuffer.

- **WriteChunk explicit `NotSupportedException` over silent no-op.** D-48-01 makes offline-render canonical for v1 WebAudio. Composers hitting WriteChunk on Web get a clear, actionable error message pointing at v1.6 backlog (D-48-02 SharedArrayBuffer streaming). A silent skip would have masked the v1 scope boundary.

- **Initialize returns charitable `false` on Desktop, NOT throw.** Phase 47 D-47-05 had the stub throw PNSE; Phase 48 inverts to charitable-fallback because Phase 48 owns the real contract. Throwing on Desktop would conflate "wrong host" with "real init failed under Web" — the false-return discriminates cleanly.

- **Phase 47 WebAudioBackendStubTests.cs DELETED, not inverted in-place.** The filename + class XMLdoc embedded the now-stale "stub" framing. Better to start fresh at the Phase 48 IntegrationTests file with correct framing.

## Threat Flags

None. Plan 48-03's threat register (T-48-08..12) tracked but no new attack surface introduced — the [JSImport] boundary IS new surface but its threats were all `accept` dispositions (T-48-08 typed-data marshalling has no script-injection vector; T-48-09 JS module ships with us, not user-controllable; T-48-10/11/12 mitigations all wired per the plan).

## Known Stubs

None. Every Phase 48-03 surface is real (no `=null` placeholder paths, no "coming soon" UI strings). The `ResumeContext` [JSImport] binding is genuinely unused from `WebAudioBackend.cs` (D-48-09 documents this — Phase 49 wires it from the playground UI), but it's a declared boundary, not a stub: the binding exists so the runtime API can expose it cleanly.

## Trimmer / Build Warnings

| Warning | Source | Tracked |
|---------|--------|---------|
| `IL2075` (System.Type.GetProperty) | `flow-lang/Interpreter/ExpressionEvaluator.cs:953` | Pre-existing Phase 48-01 carryforward; not introduced by Plan 48-03. Tracked for Plan 48-05 narrow-scope. |
| `CS0105 / CS8765 / CS8602 / CS8604 / CS0219` | Various pre-existing files | All pre-existing; not introduced by Plan 48-03. |

**No new trimmer warnings from Plan 48-03's additions.** The `[JSImport]` partials in `FlowRuntimeInterop.cs` are trim-friendly (statically reachable from `WebAudioBackend` instance methods); `WebAudioBackend` itself is preserved by `flow-lang/wasm/trim-roots.xml` per Plan 48-01.

## Files Touched

```text
flow-lang/Audio/FlowRuntimeInterop.cs                                                (NEW, 118 LOC)
flow-lang/Audio/WebAudioBackend.cs                                                   (MODIFIED, 78 → 320 LOC)
flow-lang/flow-lang.csproj                                                           (MODIFIED, +7 lines AllowUnsafeBlocks)
flow-lang.Tests/Integration/Phase48/WebAudioBackendIntegrationTests.cs               (NEW, 145 LOC)
flow-lang.Tests/Integration/Phase47/WebAudioBackendStubTests.cs                      (DELETED, was 82 LOC)
```

## Commits

| Hash | Type | Description |
|------|------|-------------|
| `2e15d22` | feat | add FlowRuntimeInterop with 5 [JSImport] declarations |
| `4c7792a` | feat | swap WebAudioBackend stub bodies for real [JSImport] impl |
| `028a5e5` | test | pin PromoteToStereo + Dispose-safety + Desktop fallback |

## Phase 48 Status After Plan 03

- Plan 48-01 ✓ COMPLETE — WASM publish pipeline foundation (10.8 MB bundle)
- Plan 48-02 ✓ COMPLETE — DryWetMidi reachability + invariant-globalization safety
- Plan 48-03 ✓ COMPLETE — WebAudioBackend real implementation + [JSImport] boundary
- Plans 48-04..48-07 → unblocked

The C# boundary surface for the WASM runtime is now real. Plan 48-04 will materialize `flow-lang/wasm/flow-runtime.js` against the 5 [JSImport] names this plan committed (`createAudioContext`, `playStereoFloat32`, `stopSource`, `closeContext`, `resumeContext`). Plan 48-06's browser HUMAN-UAT will validate the end-to-end audio path.

## Self-Check: PASSED

Verified before completion:

- `flow-lang/Audio/FlowRuntimeInterop.cs` — created, 118 LOC, 5 [JSImport]: FOUND
- `flow-lang/Audio/WebAudioBackend.cs` — modified, 320 LOC, no PNSE throws, PromoteToStereo public static: FOUND
- `flow-lang/flow-lang.csproj` — modified, AllowUnsafeBlocks=true at top-level: FOUND
- `flow-lang.Tests/Integration/Phase48/WebAudioBackendIntegrationTests.cs` — created, 145 LOC, 8 [Fact]s: FOUND
- `flow-lang.Tests/Integration/Phase47/WebAudioBackendStubTests.cs` — deleted (stale-stub cleanup): VERIFIED (file does not exist)
- Commit `2e15d22` (Task 1 FlowRuntimeInterop) in git log: FOUND
- Commit `4c7792a` (Task 2 WebAudioBackend swap) in git log: FOUND
- Commit `028a5e5` (Task 3 IntegrationTests) in git log: FOUND
- `dotnet build flow-lang -p:FlowTarget=Desktop` exits 0: VERIFIED
- `dotnet build flow-lang -p:FlowTarget=Web` exits 0: VERIFIED
- `dotnet publish flow-lang -p:FlowTarget=Web -c Release` exits 0: VERIFIED
- Phase 48 fixture: 15/15 PASS, Phase 47 fixture: 9 PASS + 8 SKIP + 0 FAIL (preserved modulo deleted stub tests): VERIFIED
- WasmBuildPipelineTests Plan 48-01 regression: 3/3 PASS preserved: VERIFIED
