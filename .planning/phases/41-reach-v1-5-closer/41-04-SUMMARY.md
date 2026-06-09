---
phase: 41-reach-v1-5-closer
plan: 04
subsystem: infra
tags: [wasapi, naudio, audio-backend, coreaudio, web-strip, supply-chain, iaudiobackend, p-invoke]

# Dependency graph
requires:
  - phase: 41-01
    provides: "WasapiBackendAvailabilityTests + CoreAudioBackendAvailabilityTests Wave-0 stubs; 41-HUMAN-UAT.md with the Windows-audible pending row"
  - phase: 47-compile-target-flavors
    provides: "FlowTarget=Desktop|Web Web-strip discipline (#if !FLOW_WEB + <Compile Remove> + AssemblyReferenceScanTests forbidden-prefix gate)"
provides:
  - "flow-lang/Audio/WasapiBackend.cs — Windows WASAPI IAudioBackend over NAudio.Wasapi 2.3.0 (pull->push bridge via BufferedWaveProvider + WasapiOut)"
  - "AudioPlaybackManager.DetectBackend Windows branch (probe-gated, before the PulseAudio probe) + IsAudioAvailable Windows roll-up"
  - "NAudio.Wasapi 2.3.0 pinned EXACTLY, Desktop-only, in the AssemblyReferenceScanTests forbidden-prefix gate"
  - "CoreAudioBackend.cs confirmed compile-clean + IsAvailable() false on Linux (verify-not-build, unmodified)"
affects: [41-binaries-cross-compile, 41-HUMAN-UAT, v1.5-milestone-close]

# Tech tracking
tech-stack:
  added: ["NAudio.Wasapi 2.3.0 (Desktop-only; pulls NAudio.Core 2.3.0 transitively)"]
  patterns:
    - "NAudio pull-model -> IAudioBackend blocking-push bridge via BufferedWaveProvider"
    - "Three-layer Web-strip for a new desktop-only native dep: Desktop-only PackageReference + <Compile Remove> + #if !FLOW_WEB guard, with the forbidden-prefix gate shipped in the SAME commit"
    - "Exact-version pin (no range) as a supply-chain mitigation against an anomalous registry version (22.0.0 trap)"

key-files:
  created:
    - "flow-lang/Audio/WasapiBackend.cs"
  modified:
    - "flow-lang/flow-lang.csproj"
    - "flow-lang/Audio/AudioPlaybackManager.cs"
    - "flow-lang.Tests/Integration/Phase47/AssemblyReferenceScanTests.cs"
    - "flow-lang.Tests/Integration/Phase41/WasapiBackendAvailabilityTests.cs"
    - "flow-lang.Tests/flow-lang.Tests.csproj"

key-decisions:
  - "NAudio.Wasapi pinned EXACTLY to 2.3.0 (no +/range) — the registry's anomalous 22.0.0 is a supply-chain pin trap (T-41-04-SC); restored 2.3.0 confirmed on disk"
  - "Exclusive-mode share resolution kept local (ResolveShareMode() => Shared) rather than expanding the SPEC-4-locked five-key FlowConfig surface — a config-schema change is out of this plan's scope (Rule 4 avoidance)"
  - "CoreAudioBackend.cs left UNMODIFIED (COREAUDIO-01 verify-not-build, D-18) — confirmed compile-clean + false-on-Linux by the passing 41-01 availability test"
  - "Web Compile-Remove'd both backend availability tests from the test project (Rule 3 fix — the 41-01 CoreAudio test broke `dotnet test -p:FlowTarget=Web`)"

patterns-established:
  - "WASAPI backend: float[] -> byte[] via Buffer.BlockCopy -> BufferedWaveProvider.AddSamples -> WasapiOut.Play() -> block-poll BufferedBytes/PlaybackState until drained, CancellationToken honored"
  - "A new IAudioBackend slots into DetectBackend as an OS-gated branch ordered by platform priority, probe-gated by a static IsAvailable() that returns false (never throws) off-platform"

requirements-completed: [WASAPI-01, COREAUDIO-01]

# Metrics
duration: 7min
completed: 2026-06-08
---

# Phase 41 Plan 04: Windows WASAPI Audio Backend Summary

**Windows audio output via `WasapiBackend.cs` over NAudio.Wasapi 2.3.0 (pull-model `WasapiOut` bridged to the blocking-push `IAudioBackend` contract through a `BufferedWaveProvider`), wired into `DetectBackend` as a probe-gated Windows branch, Web-stripped three ways, with NAudio.Wasapi pinned EXACTLY to 2.3.0 against the 22.0.0 registry trap — audible Windows verification honestly left as a pending HUMAN-UAT row.**

## Performance

- **Duration:** ~7 min
- **Started:** 2026-06-08T00:00:13Z
- **Completed:** 2026-06-08T00:06:45Z
- **Tasks:** 3 (+ 1 pre-approved checkpoint)
- **Files modified:** 6 (1 created, 5 modified)

## Accomplishments

- **`WasapiBackend.cs`** — full `internal sealed class WasapiBackend : IAudioBackend`, `#if !FLOW_WEB`-guarded, structured after `PulseAudioSimpleBackend`/`CoreAudioBackend`. `static IsAvailable()` returns `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` and catches Platform/DllNotFound defensively (false on Linux, no crash — T-41-04-PINVOKE). NAudio pull→push bridge: 32-bit IEEE-float `WaveFormat`, `BufferedWaveProvider` (5 s buffer, `DiscardOnBufferOverflow=false`), `WasapiOut(Shared, 100 ms)`, `float[]→byte[]` via `Buffer.BlockCopy`, `Play()` then block-poll `BufferedBytes`/`PlaybackState` until drained with `CancellationToken` early-out. `WriteChunk` streams without draining; `GetDevices` enumerates render endpoints via `MMDeviceEnumerator` (empty on failure); `SetDevice` best-effort no-op like the siblings.
- **`DetectBackend` Windows branch** — probe-gated `WasapiBackend.IsAvailable()` inserted inside `#if !FLOW_WEB`, ordered before the PulseAudio probe (RESEARCH Pattern 4); `IsAudioAvailable` roll-up gains the parallel Windows branch so Windows reports an available backend.
- **Supply-chain + Web-strip discipline** — NAudio.Wasapi pinned EXACTLY `2.3.0` (no range) under the Desktop-only ItemGroup; `WasapiBackend.cs` Web `<Compile Remove>`'d; `"NAudio"` added to `AssemblyReferenceScanTests.ForbiddenTypeRefPrefixes` in the SAME commit as the PackageReference (Pitfall 3). Restored version confirmed `2.3.0` on disk (NOT 22.0.0). The Web-strip gate (`AssemblyReferenceScanTests` under `FlowTarget=Web`) is GREEN — NAudio absent from the WASM closure.
- **COREAUDIO-01 verify-not-build** — `CoreAudioBackend.cs` left UNMODIFIED (D-18); confirmed compile-clean and `IsAvailable()==false` on this Linux host by the passing 41-01 `CoreAudioBackendAvailabilityTests`.

## Task Commits

1. **Task 1: NAudio.Wasapi 2.3.0 Desktop-only + Web-strip + forbidden-prefix gate** — `29cfd7f` (chore)
2. **Task 2: Implement WasapiBackend over NAudio WasapiOut + live availability test** — `d0b52ea` (feat)
3. **Task 3: Slot into DetectBackend + Web-strip the backend availability tests** — `a3355ff` (feat)

**Plan metadata:** (final docs commit — this SUMMARY + STATE + ROADMAP)

## Files Created/Modified

- `flow-lang/Audio/WasapiBackend.cs` (created) — Windows WASAPI `IAudioBackend` over NAudio `WasapiOut`; `#if !FLOW_WEB`-guarded.
- `flow-lang/flow-lang.csproj` — NAudio.Wasapi 2.3.0 Desktop-only PackageReference + `WasapiBackend.cs` Web `<Compile Remove>`.
- `flow-lang/Audio/AudioPlaybackManager.cs` — `DetectBackend` Windows branch + `IsAudioAvailable` Windows roll-up.
- `flow-lang.Tests/Integration/Phase47/AssemblyReferenceScanTests.cs` — `"NAudio"` forbidden-prefix entry.
- `flow-lang.Tests/Integration/Phase41/WasapiBackendAvailabilityTests.cs` — `Skip` removed; LIVE assertion `IsAvailable()==false` on Linux, no throw.
- `flow-lang.Tests/flow-lang.Tests.csproj` — Web `<Compile Remove>` for both Phase41 backend availability tests (Rule 3 fix).

## Decisions Made

- **Exact-pin over range** for NAudio.Wasapi (`2.3.0`, no `+`/`latest`) — the registry carries an anomalous `22.0.0` a careless resolution could pull; the pin + the same-commit forbidden-prefix gate are the T-41-04-SC / T-41-04-WEBDRIFT mitigations. Restored version verified `2.3.0` in `project.assets.json` and `~/.nuget/packages/naudio.wasapi/`.
- **Share-mode resolution kept local** (`ResolveShareMode() => AudioClientShareMode.Shared`). The plan suggested reading an exclusive-mode flag from `FlowConfig.Active`, but `FlowConfigPoco` has a SPEC-4-locked five-key surface with no exclusive-mode key. Adding a new config key is an architectural schema change (Rule 4) out of this plan's scope; Shared is the documented default (D-17) and the local hook is ready for a future config-wiring plan. No behavior change vs. the plan's stated default.
- **CoreAudioBackend.cs untouched** — COREAUDIO-01 is verify-not-build (D-18). Confirmed via empty `git diff` and the passing availability test.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Web Compile-Remove for both Phase41 backend availability tests**
- **Found during:** Task 3 (running `AssemblyReferenceScanTests` under `FlowTarget=Web`)
- **Issue:** `WasapiBackendAvailabilityTests` and `CoreAudioBackendAvailabilityTests` reference `FlowLang.Audio.WasapiBackend` / `CoreAudioBackend`, both `<Compile Remove>`'d from `flow-lang` on Web. Under `dotnet test -p:FlowTarget=Web` the test project failed to compile (`CS0103: name does not exist`). The 41-01 `CoreAudioBackendAvailabilityTests` was added WITHOUT a corresponding Web `<Compile Remove>`, so the Web test build was already broken before this plan — adding the WASAPI test surfaced it. This blocked verifying the NAudio forbidden-prefix Web-strip gate (a plan success criterion).
- **Fix:** Added `<Compile Remove>` entries for both Phase41 availability tests under the test project's `Condition="'$(FlowTarget)' == 'Web'"` ItemGroup, matching the established Phase 40 pattern (ClockMaster/Jack tests). These tests assert Linux-host probe behavior on the Desktop build; they have nothing to assert on Web.
- **Files modified:** `flow-lang.Tests/flow-lang.Tests.csproj`
- **Verification:** `dotnet test -p:FlowTarget=Web --filter AssemblyReferenceScanTests` now compiles and passes 2/2 (NAudio absent from the WASM closure); Desktop Phase41 suite still 13 passed / 0 failed.
- **Committed in:** `a3355ff` (Task 3 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking, Rule 3)
**Impact on plan:** The fix was necessary to run the plan's own Web-strip success criterion and also repaired a pre-existing 41-01 breakage of `dotnet test -p:FlowTarget=Web`. No scope creep — confined to test-project target-gating mirroring the Phase 40 precedent.

## Issues Encountered

- NAudio.Wasapi 2.3.0 ships a `netstandard2.0` facade that restores cross-platform on the Linux build host (assertion A4 from RESEARCH). Confirmed the facade XML exposes `WasapiOut(AudioClientShareMode, int)`, `MMDeviceEnumerator`, `AudioClientShareMode`, and the `PlaybackStopped` event, so the implementation compiles on Linux; the WASAPI COM calls only fire at runtime on Windows. Linux Desktop build stayed green after adding the reference (A4 verified).

## Known Stubs

None that block the plan goal. `SetDevice` returns false (runtime device switching unsupported for v1.5 — matches Pulse/CoreAudio); `ResolveShareMode` is a Shared-mode constant with a documented hook for future exclusive-mode config wiring (D-17 default behavior is fully delivered). Neither stub prevents Windows WASAPI playback.

## Threat Flags

None. No new network endpoints, auth paths, or trust-boundary surface introduced beyond the NAudio.Wasapi dependency already covered by the plan's `<threat_model>` (T-41-04-SC / -WEBDRIFT / -PINVOKE all mitigated and verified).

## User Setup Required

None at build time — NAudio.Wasapi restores from NuGet on Linux and runs only on Windows; no account/secret needed. **Windows WASAPI audible playback on real hardware remains a pending HUMAN-UAT row** (`41-HUMAN-UAT.md` Row 1, D-05). This plan delivered ONLY the machine half: `WasapiBackend.IsAvailable()==false` on Linux (no crash), both builds green, NAudio absent from the WASM closure. No claim is made that a Windows machine produces sound — that is the human gate.

## Next Phase Readiness

- WASAPI-01 + COREAUDIO-01 code complete and committed under the locked Web-strip + no-trim discipline. `DetectBackend` now resolves WASAPI on Windows, CoreAudio on macOS, PulseAudio on Linux, WebAudio on WASM.
- Ready for the cross-platform binaries plan (win-x64/osx publish) — the audio backends P/Invoke system libraries (no native cross-toolchain needed; managed-only `dotnet publish`).
- **Pending human gate (not a blocker for code, required for milestone close):** Windows WASAPI audible playback + win-x64 exec smoke (`41-HUMAN-UAT.md` Rows 1, 5).

## Self-Check: PASSED

- Created files exist: `flow-lang/Audio/WasapiBackend.cs`, `.planning/phases/41-reach-v1-5-closer/41-04-SUMMARY.md` — FOUND.
- Task commits exist: `29cfd7f`, `d0b52ea`, `a3355ff` — FOUND.

---
*Phase: 41-reach-v1-5-closer*
*Completed: 2026-06-08*
