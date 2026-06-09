---
phase: 40-studio-sync
plan: 01
subsystem: real-time-midi
tags: [midi, rtmidi, backend, alsa-seq, web-strip, determinism, gm-routing]
requires:
  - "flow-lang/Audio/IAudioBackend.cs (interface-for-interface template)"
  - "flow-lang/StandardLibrary/Network/OscFunctions.cs (Register + gate + handle template)"
  - "flow-lang/StandardLibrary/Notation/InstrumentRouting.cs:45 (ResolveGmProgram, D-40-02 verbatim)"
  - "RtMidi.Core 1.0.53 (NuGet, MIT; native librtmidi.so NOT bundled)"
provides:
  - "IMidiBackend + IMidiOutputHandle abstraction (MIDI-RT-01)"
  - "RtMidiMidiBackend (ALSA-seq, #if !FLOW_WEB) + NullMidiBackend fallback"
  - "MidiPlaybackManager (probe + lifecycle, NullMidiBackend fallthrough — never throws)"
  - "@midi builtin surface: midiPorts/openMidiOutput/midiOut/midiNoteOn/midiNoteOff/midiCC/midiSysex"
  - "AudioBuffer.PlaybackStartTime alignment origin seam (MIDI-RT-04)"
  - "MidiDeviceType + MidiDeviceData + Value.MidiDevice (ref-identity handle, D-40-03)"
  - "CaptureMidiBackend in-process test seam (for Plan 02 ClockMaster/Slave tests)"
  - "RESOLVED Open Q1 clock-access strategy (see Clock Access Strategy section)"
affects:
  - "flow-lang.csproj (RtMidi.Core dep + Web strip-list)"
  - "ExecutionContext / TestSnapshot (MidiEnabled gate)"
  - "FlowEngine (MidiFunctions.Register), ModuleLoader (@midi Web advisory)"
  - "TypeParser + Parser (MidiDevice type-name)"
tech-stack:
  added: ["RtMidi.Core 1.0.53 (MIT, Desktop-only)"]
  patterns: ["#if !FLOW_WEB guard + Compile Remove", "reflection into RtMidi.Core internal raw-byte path", "charitable NullMidiBackend fallback", "ref-identity handle (OscHandle model)"]
key-files:
  created:
    - flow-lang/Audio/IMidiBackend.cs
    - flow-lang/Audio/NullMidiBackend.cs
    - flow-lang/Audio/RtMidiMidiBackend.cs
    - flow-lang/Audio/MidiPlaybackManager.cs
    - flow-lang/StandardLibrary/Midi/MidiFunctions.cs
    - flow-lang/StandardLibrary/Midi/MidiDeviceData.cs
    - flow-lang/TypeSystem/SpecialTypes/MidiDeviceType.cs
    - flow-lang/midi.flow
    - flow-lang.Tests/Integration/Phase40/RtMidiInternalAccessSpikeTests.cs
    - flow-lang.Tests/Integration/Phase40/CaptureMidiBackend.cs
    - flow-lang.Tests/Integration/Phase40/MidiBackendTests.cs
    - flow-lang.Tests/Integration/Phase40/VirtualMidiTests.cs
    - flow-lang.Tests/Integration/Phase40/OfflineRenderDeterminismTests.cs
  modified:
    - flow-lang/flow-lang.csproj
    - flow-lang/StandardLibrary/Audio/AudioCore.cs
    - flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs
    - flow-lang/Runtime/Value.cs
    - flow-lang/Runtime/ExecutionContext.cs
    - flow-lang/Runtime/ModuleLoader.cs
    - flow-lang/Core/FlowEngine.cs
    - flow-lang/StandardLibrary/TestFramework/TestSnapshot.cs
    - flow-lang/Parsing/TypeParser.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang.Tests/flow-lang.Tests.csproj
    - flow-lang.Tests/Integration/Phase47/AssemblyReferenceScanTests.cs
decisions:
  - "Open Q1 = strategy (a) reflection into RtMidi.Core internal _outputDevice bridge (no second native coupling)"
  - "MidiDeviceType specificity 152 (above OscHandle=151)"
  - "overrides= named-arg = Dict<String,Int> name->channel; program still derives from name"
  - "midiPorts returns Void (prints port names) rather than an Array — charitable, side-effect-light"
metrics:
  tasks_completed: 3
  files_created: 13
  files_modified: 12
  phase40_tests: "18/18 green"
  duration: "~1 session"
  completed: "2026-06-07"
---

# Phase 40 Plan 01: Real-Time MIDI-Out Spine Summary

The must-ship MIDI-out spine for Flow: an `IMidiBackend` abstraction parallel to `IAudioBackend`, an RtMidi.Core ALSA-seq backend with a charitable `NullMidiBackend` fallback, the opt-in `@midi` builtin surface (high-level GM-routed `midiOut` + low-level `midiNoteOn`/`midiCC`/`midiSysex` escape hatch), the `AudioBuffer.PlaybackStartTime` alignment seam, full Web-strip discipline, the LINK-02 offline-render determinism invariant, and — the highest-value output — the **resolved Open Q1 clock-access strategy** that Plan 40-02 builds its clock send/receive on.

## Clock Access Strategy (Open Q1 — RESOLVED for Plan 40-02)

**Strategy (a): reflection into the RtMidi.Core internal bridge. No second native coupling. Strategy (c) C-API P/Invoke is NOT needed.**

RtMidi.Core 1.0.53's public managed API (`IMidiOutputDevice` — 13 typed `Send(in XxxMessage)` overloads) has **no** MIDI-clock / real-time message type (no `TimingClockMessage`/`Start`/`Continue`/`Stop`). The raw-byte path that sends the four clock bytes (0xF8 clock / 0xFA start / 0xFB continue / 0xFC stop) is `internal`. The Task-1 spike (`RtMidiInternalAccessSpikeTests`, 4 Facts, all green) empirically pinned the **full bridge from a public handle to the raw-byte path** against the managed assembly metadata:

```
Public  RtMidi.Core.Devices.IMidiOutputDevice        (from info.CreateDevice())
  └─ implemented by internal RtMidi.Core.Devices.MidiOutputDevice
     └─ private field:  IRtMidiOutputDevice _outputDevice
        └─ method:      bool SendMessage(byte[] message)   ← clock bytes go here
```

Plan 02's clock SEND uses this exact seam (already wired in `RtMidiMidiBackend.RtMidiOutputHandle.ResolveRawSendBridge` + `SendRaw`):

```csharp
// cache once per opened device:
FieldInfo bridge = walk publicDevice.GetType()(+BaseType) for
    GetField("_outputDevice", BindingFlags.NonPublic | BindingFlags.Instance);
object rawDev = bridge.GetValue(publicDevice);          // IRtMidiOutputDevice
MethodInfo send = rawDev.GetType().GetMethod("SendMessage", ... byte[]);  // bool(byte[])
// per pulse:
send.Invoke(rawDev, new object[] { new byte[] { 0xF8 } });
```

Plan 02's clock SLAVE (CLOCK-02) uses the symmetric internal input seam, also pinned by the spike:
`internal IRtMidiInputDevice.Message : EventHandler<byte[]>` — the raw incoming MIDI byte stream (0xF8 pulses included).

**Upgrade fragility (Pitfall 7):** RtMidi.Core is PINNED to exactly 1.0.53. The spike's GUARD Facts (`RtMidiInternalSendMessage_Reachable`, `RtMidiOutputBridgeField_Reachable`, `RtMidiInternalInputMessage_Reachable`, `RtMidiPublicApi_HasNoTypedClockMessage`) fail LOUDLY at CI if a future bump renames the internals or adds a typed clock message — so the clock path never silently dies at a live gig. Strategy (c) `[DllImport("rtmidi")] rtmidi_out_send_message` remains the documented fallback ONLY if a future upgrade removes the internal seam; the guard Facts are the trip-wire. The strategy is recorded verbatim in the top-of-file comment of `RtMidiInternalAccessSpikeTests.cs` and in `RtMidiMidiBackend.cs`.

## What Shipped

### Task 1 — SPIKE + dependency + Web-strip gate (commit `100ffd9`)
- RtMidi.Core 1.0.53 added to the `!= 'Web'` ItemGroup (Desktop-only; never enters the WASM closure).
- `RtMidiInternalAccessSpikeTests` (4 guard Facts) resolved Open Q1 (above).
- `AssemblyReferenceScanTests.ForbiddenTypeRefPrefixes` += `JackSharp` (RtMidi.Core already present per D-47-14).

### Task 2 — backend abstraction + manager + alignment seam (commit `bb8b401`)
- `IMidiBackend` + `IMidiOutputHandle` (ListPorts/OpenOutput/SendNoteOn/Off/CC/Program/Sysex/Raw/Close + PortChanged) — charitable contract (never null/never throw).
- `RtMidiMidiBackend` (#if !FLOW_WEB): `IsAvailable()` probe (catches DllNotFoundException), `ToRtChannel` 0→1-based mapping (Pitfall 3), `ToRtKey`, typed Send for note/CC/program, `SendRaw` via the Open-Q1 reflection bridge.
- `NullMidiBackend`: silent no-op fallback (Web-resident) so a live session never dies on missing librtmidi.so.
- `MidiPlaybackManager`: lock+lazy GetBackend; `DetectBackend` returns NullMidiBackend (does NOT throw — the one deviation from AudioPlaybackManager per Open Q2); IsMidiAvailable; Dispose.
- `AudioBuffer.PlaybackStartTime` nullable Stopwatch-tick origin (default null), stamped in `PlaybackFunctions.PlaySamples` the instant before `backend.Play` (MIDI-RT-04 — buffer-relative ms alignment, NOT sample-accurate).
- `CaptureMidiBackend` in-process loopback test seam (records sent bytes; the load-bearing seam for VirtualMidiTests + Plan 02 clock tests).

### Task 3 — @midi builtin surface (commit `78705e3`)
- `MidiDeviceType` (#if !FLOW_WEB, specificity 152) + `MidiDeviceData` + `Value.MidiDevice` (ref-identity handle, D-40-03).
- `MidiFunctions`: `midiPorts`/`openMidiOutput`/`midiOut(Song|Sequence)`/`midiNoteOn`/`midiNoteOff`/`midiCC`/`midiSysex` + `__enableMidiModule` marker.
- **GM routing (D-40-02 VERBATIM):** `midiOut` calls `InstrumentRouting.ResolveGmProgram(seqName)` per sequence → `SendProgramChange(ToRtChannel(channel), gmProgram)` then streams notes; drum* → ch9. `overrides=` named-arg `Dict<String,Int>` (name→channel) layers on top.
- **Input validation (T-40-01/T-40-04):** channel clamped 0..15, pitch/vel/CC 0..127, sysex length-capped 64 KiB — clamp + `[midi]` WarnOnce, never throw to native. Dead handle → no-op.
- **3-site opt-in gate (D-40-04):** ExecutionContext.MidiEnabled (+ snapshot/restore + TestSnapshot); FlowEngine register at the OSC `#if !FLOW_WEB` site; ModuleLoader `IsStrippedOnWeb |= @midi`.
- `MidiDevice` type-name registered in TypeParser (#if !FLOW_WEB) + Parser.IsTypeKeyword (string-only) so `MidiDevice dev = (openMidiOutput ...)` parses.
- `midi.flow` opt-in module (force-added past `*.flow` gitignore like osc.flow).
- Web-strip: `StandardLibrary/Midi/**`, `MidiDeviceType.cs`, `RtMidiMidiBackend.cs`, `<None Remove="midi.flow">` (T-40-03).
- LINK-02 `OfflineRenderDeterminismTests` (writeWav byte-identical with vs without @midi state) + clamp + sysex-cap + GM-routing + module-gate + lib-absent smoke tests.

## Verification

| Gate | Result |
|------|--------|
| `dotnet build flow-lang -p:FlowTarget=Desktop` | exit 0 |
| `dotnet build flow-lang -p:FlowTarget=Web` | exit 0 (RtMidi.Core absent from Web closure) |
| Phase40 test suite (Desktop) | 18/18 green |
| `AssemblyReferenceScan` under `-p:FlowTarget=Web` | 2/2 green (RtMidi.Core/JackSharp absent) |
| Phase40 + all WASM console tests (isolation, 3×) | 24/24 green |
| `.flow` smoke `use "@midi"` + `(midiPorts)` on lib-absent box | runs, prints, never throws (NullMidiBackend) |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] RtMidi.Core `IMidiOutputDeviceInfo` namespace import**
- **Found during:** Task 2 (first Desktop build of RtMidiMidiBackend).
- **Issue:** `IMidiOutputDeviceInfo` lives in `RtMidi.Core.Devices.Infos`, not `RtMidi.Core.Devices`.
- **Fix:** added `using RtMidi.Core.Devices.Infos;`.
- **Commit:** `bb8b401`.

**2. [Rule 3 - Blocking] `ExecutionContext` ambiguous reference**
- **Found during:** Task 3 (first Desktop build of MidiFunctions).
- **Issue:** `ExecutionContext` is ambiguous between `FlowLang.Runtime` and `System.Threading`.
- **Fix:** fully-qualified as `FlowLang.Runtime.ExecutionContext` (matches OscFunctions).
- **Commit:** `78705e3`.

**3. [Rule 3 - Blocking] `TestSnapshot.MidiEnabled` missing**
- **Found during:** Task 3 build. ExecutionContext.Snapshot/Restore use a `TestSnapshot` POCO, not a new ExecutionContext — adding the gate to the field/snapshot/restore needed a matching `TestSnapshot.MidiEnabled` member.
- **Fix:** added `public bool MidiEnabled { get; init; } = false;` to `TestSnapshot.cs`.
- **Commit:** `78705e3`.

**4. [Rule 2 - Missing critical functionality] `MidiDevice` type-name not parseable**
- **Found during:** Task 3 tests. `MidiDevice dev = (openMidiOutput ...)` failed to parse ("Unexpected token Assign '='") — the parser's `IsTypeKeyword` allowlist + `TypeParser` did not recognize `MidiDevice`, so the declaration was un-parseable (the planned `MidiDevice` annotation in midi.flow + composer scripts would never work).
- **Fix:** registered `MidiDevice` in `TypeParser` (both arms, `#if !FLOW_WEB`) and `Parser.IsTypeKeyword` (string-only check, safe on Web — @midi import is rejected before any decl parses). Mirrors the Phase 38 `OscHandle` precedent.
- **Commit:** `78705e3`.

**5. [Rule 1 - Bug, test infra] Console-redirection cross-collection race**
- **Found during:** full Desktop suite run. The two new Console-redirecting Phase40 test classes (`VirtualMidiTests`, `OfflineRenderDeterminismTests`) raced the Phase 48 WASM tests (both redirect process-wide `Console.Out`/`Error`; xUnit parallelizes across collections).
- **Fix:** placed both Phase40 classes in `[Collection(WasmEntryConsoleCollection.Name)]` so they serialize with the WASM tests. Phase40 + all WASM console tests now green 3×/3× in isolation.
- **Commit:** `78705e3`.

### midi.flow gitignore
`*.flow` is gitignored (`.gitignore:10`); existing stdlib modules (osc.flow, std.flow) are force-added. `midi.flow` was force-added the same way — it is an intentional stdlib module, not a generated artifact.

## Deferred / Out-of-Scope Issues

**Pre-existing full-suite Console/build race (NOT a Plan 40-01 regression).** Running the entire `flow-lang.Tests` suite intermittently flakes 2-3 tests:
`Phase48.WasmSynchronousExecutionTests.RunFromJs_SimpleScript_*` / `*_ToneRender_*` and `Phase47.BuildConditioningSmokeTests.DefaultBuild_ExitCodeIsZero_*`. **Proven pre-existing:** the full suite with ALL Phase40 tests EXCLUDED still fails the same tests; all pass in isolation (3/3). Root cause = the same cross-collection process-wide `Console.Set*` race (WasmEntryConsoleCollection only serializes WASM-vs-WASM, not against `FlowScripts`/`ConsoleCapture`) + `BuildConditioningSmokeTests` shelling `dotnet build` concurrent with the test host build. Plan 40-01 does NOT introduce or worsen it (and serialized its own Console tests with the WASM collection). Recommended fix (touches Phase47/Phase48 test infra, out of Plan 40-01 scope): unify Console-redirecting / build-shelling classes under one non-parallel collection, or set assembly-level `DisableTestParallelization` for that subset. Logged in `.planning/phases/40-studio-sync/deferred-items.md`.

## Honest Scope Notes

- **MIDI-RT-04 alignment is buffer-relative ms, NOT sample-accurate** (40-RESEARCH Pitfall 5). The blocking PulseAudio Simple push API has no pull-model callback. `PlaybackStartTime` is the origin seam; the actual scheduled MIDI dispatch thread keyed off it is a Plan 02 concern (clock). No verification claims sample accuracy.
- **CLOCK-01/02 are NOT in this plan** — only the access strategy is resolved. `midi.flow` deliberately omits `clockMaster`/`clockSlave` decls (Plan 02 adds them).
- **Real hardware / virtual-MIDI is HUMAN-UAT** (D-40-07). Automated tests use the in-process `CaptureMidiBackend` seam; the real-ALSA end-to-end path charitable-skips when `librtmidi.so`/`snd-virmidi` are absent (they are absent on this dev box).

## Self-Check: PASSED
- All 13 created files exist on disk.
- All 3 task commits (`100ffd9`, `bb8b401`, `78705e3`) exist in git history.
