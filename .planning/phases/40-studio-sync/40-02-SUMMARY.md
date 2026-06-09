---
phase: 40-studio-sync
plan: 02
subsystem: real-time-midi
tags: [midi, clock, 24-ppqn, master, slave, stopwatch-timing, ref-identity-handle, web-strip, determinism]
requires:
  - "flow-lang/Audio/IMidiBackend.cs (Plan 01 — IMidiOutputHandle.SendRaw clock-byte path)"
  - "flow-lang/Audio/RtMidiMidiBackend.cs (Plan 01 — Open-Q1 reflection SendRaw bridge)"
  - "flow-lang/StandardLibrary/Network/OscFunctions.cs (StartListener/StopListener lifecycle template)"
  - "flow-lang/StandardLibrary/Network/OscHandleData.cs + OscHandleType.cs (ref-identity handle template)"
  - "flow-lang/Runtime/MusicalContext.cs:43 (Tempo) + :180 (IsValidTempo)"
  - "flow-lang.Tests/Integration/Phase40/CaptureMidiBackend.cs (Plan 01 in-process seam idea)"
provides:
  - "MidiClock — 24 PPQN master timing thread (Stopwatch deadline + spin-wait) + slave 0xF8 capture (CLOCK-01/02)"
  - "ClockHandleType (ref-identity, specificity 153) + ClockHandleData + ClockMode + Value.ClockHandle factory"
  - "@midi clock surface: clockMaster(MidiDevice) / clockSlave(String) / clockStop(ClockHandle)"
  - "SlaveByteSource test seam (synthetic 0xF8 injection, no ALSA) + RtMidiInputBridge real-hardware input seam"
  - "ClockHandle type-name in TypeParser + Parser.IsTypeKeyword"
affects:
  - "flow-lang/Core/FlowEngine.cs (MidiClockFunctions.Register at the #if !FLOW_WEB site)"
  - "flow-lang/flow-lang.csproj (MidiClock.cs + ClockHandleType.cs Web Compile-Remove)"
  - "flow-lang/midi.flow (clockMaster/clockSlave/clockStop decls)"
tech-stack:
  added: []
  patterns: ["dedicated Stopwatch-deadline timing thread (NO Thread.Sleep in pulse loop)", "OSC StartListener/StopListener lifecycle reuse (slave)", "reference-identity handle (OscHandle model)", "#if !FLOW_WEB guard + Compile Remove", "test-seam byte injection (SlaveByteSource)"]
key-files:
  created:
    - flow-lang/Audio/MidiClock.cs
    - flow-lang/StandardLibrary/Midi/MidiClockFunctions.cs
    - flow-lang/StandardLibrary/Midi/ClockHandleData.cs
    - flow-lang/TypeSystem/SpecialTypes/ClockHandleType.cs
    - flow-lang.Tests/Integration/Phase40/ClockMasterTests.cs
    - flow-lang.Tests/Integration/Phase40/ClockSlaveTests.cs
  modified:
    - flow-lang/Runtime/Value.cs
    - flow-lang/Core/FlowEngine.cs
    - flow-lang/Parsing/TypeParser.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang/midi.flow
    - flow-lang/flow-lang.csproj
decisions:
  - "ClockHandleType specificity 153 (above MidiDevice=152, OscHandle=151) per D-40-03 discretion"
  - "Clock stop via handle-based (clockStop ClockHandle) — models oscStop; no separate lifecycle"
  - "Master takes a live MusicalContext + re-reads .Tempo at each bar boundary; tests pass a mutable MusicalContext, production passes context.GetMusicalContext() snapshot"
  - "Slave 8-pulse settle = average the last 8 inter-pulse deltas; Tempo written only once the window is full (so a single jittery pulse never lurches)"
  - "Mode switch ships the bar-boundary GATE contract (RequestModeSwitch/AtBarBoundary); actual master↔slave thread re-spin is composer-driven (stop old handle, start new)"
  - "Real-hardware slave input via RtMidiInputBridge reflecting MidiInputDevice._inputDevice → internal IRtMidiInputDevice.Message (symmetric to Plan-01 output bridge); automated tests use the SlaveByteSource injection seam"
metrics:
  tasks_completed: 2
  files_created: 6
  files_modified: 6
  phase40_tests: "25/25 green (18 Plan 01 + 7 new clock)"
  duration: "~1 session"
  completed: "2026-06-07"
---

# Phase 40 Plan 02: MIDI Clock Master + Slave (24 PPQN) Summary

MIDI clock master and slave built on the Plan 01 spine: a dedicated `Stopwatch`-deadline timing thread emitting 24 pulses-per-quarter (0xF8) tied to the active `MusicalContext.Tempo` with bar-boundary tempo application (CLOCK-01), and a slave that reuses the OSC listener lifecycle to derive BPM from an incoming 0xF8 stream with an 8-pulse settle and drive `MusicalContext.Tempo` (CLOCK-02). Both return reference-identity `ClockHandle` Values modeled on `OscHandle`, opt-in under `use "@midi"`, fully Web-stripped, and provably outside the offline-render determinism path (LINK-02).

## What Shipped

### Task 1 — Clock master timing core + ClockHandle type/data + clockMaster builtin (commit `d609c32`)

- **`MidiClock.cs` master half — the ONLY genuinely-new mechanism in Phase 40 (no codebase analog per 40-PATTERNS §No Analog Found).** A dedicated background `Thread` (`IsBackground`, `AboveNormal` priority) runs a `Stopwatch`-deadline loop: `SpinUntil` coarse-sleeps (cancellation-aware `WaitHandle.WaitOne`) until ~2 ms out, then `Thread.SpinWait` for the final sub-ms — **NOT `Thread.Sleep`** (Pitfall 4 — ~1-15 ms Linux granularity would smear the tempo). Emits `0xFA` (start) on enable, `0xF8` per pulse at `60000/(BPM·24)` ms, `0xFC` (stop) on dispose. Tempo is re-read **only at each bar boundary** (pulse index % pulsesPerBar == 0), so a mid-bar `Tempo` change is deferred to the next downbeat. Clock bytes route through `IMidiOutputHandle.SendRaw` (the Plan-01 Open-Q1 reflection bridge).
- **`ClockHandleType`** (ref-identity sealed singleton, specificity **153** above MidiDevice=152 / OscHandle=151, D-40-03 discretion), **`ClockHandleData`** (`required`-init record holding the live `MidiClock` + `Cts` + `ClockMode` discriminator, modeling `OscHandleData`), **`ClockMode`** enum (Master ⊕ Slave), and **`Value.ClockHandle`** factory — all `#if !FLOW_WEB` + Compile-Removed on Web.
- **`MidiClockFunctions.clockMaster(MidiDevice) → ClockHandle`** + **`clockStop(ClockHandle) → Void`** (handle-based stop modeling `oscStop` → `StopListener`: Cancel → join(1s) charitably). Registered at the `#if !FLOW_WEB` FlowEngine site beside `MidiFunctions.Register`. `ClockHandle` type-name added to TypeParser (both arms) + `Parser.IsTypeKeyword`. `clockMaster`/`clockStop` decls added to `midi.flow`.
- **`ClockMasterTests`** (3 Facts via an in-process `TimestampingHandle`): `ClockMaster24PpqnRate` (pulse count == 24-PPQN for the observed run span + inter-pulse mean ≈ 60/BPM/24 within ±3 ms + 0xFA-first/0xFC-last); `MidTempoChange_DeferredToBarBoundary` (a mid-bar 60→240 BPM change leaves in-bar deltas at the slow rate); `ClockHandle_RefIdentity_AndCleanDispose`.

### Task 2 — Clock slave + clockSlave builtin + master⊕slave bar-boundary switch (commit `aa1930d`)

- **`MidiClock.cs` slave half** (shipped with the Task 1 file): `StartSlave` reuses the OSC `StartListener` lifecycle verbatim — a background `Task` + `Cts.Token.Register(dispose)` to break a blocked receive (Pitfall 5). `OnIncomingBytes`/`OnClockPulse` count 0xF8 pulses, derive BPM from inter-pulse `Stopwatch` deltas, and apply the **8-pulse settle** (average the last 8 deltas; write `MusicalContext.Tempo` only once the window is full, validated via `IsValidTempo`). `RequestModeSwitch`/`AtBarBoundary` gate the master⊕slave switch to bar boundaries (CLOCK-02).
- **`SlaveByteSource` test seam** (synthetic 0xF8 injection, no ALSA — modeling OSC's `DispatchPacketForTesting`/`HandlerInvokeOverride`) + **`RtMidiInputBridge`** real-hardware seam (reflects `MidiInputDevice._inputDevice` → internal `IRtMidiInputDevice.Message : EventHandler<byte[]>`, the symmetric Open-Q1 input path pinned by the Plan-01 spike's `RtMidiInternalInputMessage_Reachable` guard Fact). Charitable: absent port / missing lib → dead handle + WarnOnce.
- **`MidiClockFunctions.clockSlave(String) → ClockHandle`** + `clockSlave` decl in `midi.flow`.
- **`ClockSlaveTests`** (4 Facts via `SlaveSourceOverride`): `ClockSlaveDrivesTempo` (synthetic stream → correct derived BPM written to Tempo); `EightPulseSettle_SmoothsSingleJitteryPulse` (one outlier delta does not lurch the averaged tempo); `ModeSwitch_HonoredOnlyAtBarBoundary` (deferred mid-bar, applied at the 96-pulse 4/4 boundary); `SlaveBindFailure_DeadHandle_NoThrow` (absent port → no throw, Tempo untouched).

## Verification

| Gate | Result |
|------|--------|
| `dotnet build flow-lang -p:FlowTarget=Desktop` | exit 0 |
| `dotnet build flow-lang -p:FlowTarget=Web` | exit 0 (MidiClock.cs + ClockHandleType.cs Compile-Removed) |
| `dotnet test --filter ClockMaster` | 3/3 green (CLOCK-01: 24-PPQN rate + bar-boundary deferral + ref-identity/dispose) |
| `dotnet test --filter ClockSlave` | 4/4 green (CLOCK-02: BPM derive + 8-pulse settle + bar switch + charitable bind fail) |
| `dotnet test --filter Phase40` | 25/25 green (18 Plan 01 + 7 new clock) |
| `dotnet test --filter Osc/Phase38/Phase40` (adjacent handle/listener infra) | 117 passed / 1 skip — no regression |
| `AssemblyReferenceScan` (Web — RtMidi.Core forbidden) | green |
| `OfflineRenderDeterminismTests` (LINK-02 — clock tempo never reaches writeWav/writeMidi) | green |
| `.flow` smoke `use "@midi"` + `clockMaster`/`clockStop`/`clockSlave` (lib-absent box) | runs, prints, charitable advisories, never throws |

## Deviations from Plan

None — plan executed as written. The two-task structure shipped exactly as specified: Task 1's `MidiClock.cs` already contains the slave half (the plan's Task 2 action says "extend `MidiClock.cs` with the slave half"), so Task 2's commit is the CLOCK-02 test pin plus the slave being reachable through `clockSlave` (already wired in Task 1's `MidiClockFunctions.cs`). This is the natural shape: the timing-core file is one unit, committed in Task 1; Task 2 commits the behavior-pinning tests for the slave path. No silent test weakening; no architectural changes (Rule 4 untriggered).

One minor in-task self-correction during Task 1 (not a plan deviation): the `ClockMaster24PpqnRate` pulse-count assertion initially used a fixed 4-quarter window that didn't account for the post-run sleep buffer (observed 111 vs. expected ≤108). Reworked to assert count against the OBSERVED first→last pulse span (`span/pulseInterval + 1` ± 4) — a stronger, timing-honest "24 pulses per quarter at the active tempo" check. RED (margin too tight) → GREEN.

## Honest Scope Notes

- **Real-hardware slave input (`RtMidiInputBridge`) is HUMAN-UAT (D-40-07).** Automated tests use the in-process `SlaveByteSource` injection seam; the real `librtmidi.so` + MIDI-input end-to-end path is exercised only on a box with the native lib + a connected clock master (absent on this dev box, so it charitable-degrades to a dead handle). This mirrors Plan 01's posture for the output path.
- **MIDI-RT-04 alignment stays buffer-relative ms (Plan 01 concern), not sample-accurate.** The clock timing thread is independent of audio-buffer alignment; CLOCK-01's accuracy is asserted against `Stopwatch` deltas (the clock's own timeline), which is the correct contract for a transport clock.
- **Master⊕slave switch ships the bar-boundary GATE** (`RequestModeSwitch`/`AtBarBoundary`), which is what CLOCK-02 verifies. The actual thread re-spin (tearing down a master to become a slave) is composer-driven: stop the old `ClockHandle`, start the new one. The gate is the load-bearing "switch only at a bar boundary" semantics.

## Self-Check: PASSED

- All 6 created files exist on disk (MidiClock.cs, MidiClockFunctions.cs, ClockHandleData.cs, ClockHandleType.cs, ClockMasterTests.cs, ClockSlaveTests.cs).
- Both task commits exist in git history (`d609c32` Task 1, `aa1930d` Task 2).
- All acceptance criteria met: CLOCK-01 (24-PPQN rate + bar-boundary deferral) + CLOCK-02 (BPM derive + 8-pulse settle + bar switch) green; Desktop + Web builds green; LINK-02 + AssemblyReferenceScan invariants green.
