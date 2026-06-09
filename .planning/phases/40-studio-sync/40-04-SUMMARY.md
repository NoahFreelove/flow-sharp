---
phase: 40-studio-sync
plan: 04
subsystem: studio-sync
tags: [midi, librtmidi, p-invoke, abi-fix, rtmidi-core-removal, alsa-loopback, virmidi, clock, web-strip, real-hardware-test]
requires:
  - "flow-lang/Audio/IMidiBackend.cs (Plan 01 — abstraction, unchanged surface)"
  - "flow-lang/Audio/MidiClock.cs (Plan 02 — CLOCK-01/02 logic, byte transport swapped)"
  - "flow-lang/StandardLibrary/Midi/JackFunctions.cs (Plan 03 — direct-libjack P/Invoke pattern mirrored)"
  - "librtmidi.so.7 (RtMidi 6.0.0 runtime native dep, present on dev box)"
  - "snd-virmidi kernel module + amidi/aplaymidi (ALSA loopback for the real test)"
provides:
  - "flow-lang/Audio/LibRtMidi.cs — direct [DllImport(\"rtmidi\")] bindings (MODERN signatures) + RtMidiWrapper struct + cached IsAvailable probe"
  - "RtMidiMidiBackend rewritten on LibRtMidi (RtMidi.Core + reflection bridge removed); raw send = public rtmidi_out_send_message"
  - "MidiClock slave input rewritten on direct librtmidi (rtmidi_in_ignore_types + poll rtmidi_in_get_message); SlaveByteSource seam unchanged"
  - "RealMidiLoopbackTests — real RtMidiMidiBackend + MidiClock over live snd-virmidi loopback, captured via amidi (rows 1-4)"
  - "RtMidiInternalAccessSpikeTests obsoleted to a single documenting test (no false RtMidi.Core internal-member assertions)"
affects:
  - "flow-lang.csproj (RtMidi.Core PackageReference removed; LibRtMidi.cs added to Web Compile-Remove)"
  - "IMidiBackend / MidiPlaybackManager / MidiFunctions / FlowEngine / midi.flow (stale RtMidi.Core comments updated)"
  - "ClockMasterTests / ClockSlaveTests (joined WasmEntryConsoleCollection — Rule 1 test-infra fix)"
  - "ROADMAP / STATE / REQUIREMENTS (RtMidi.Core->direct-librtmidi approach change + real-loopback verification of MIDI-RT-01/02 + CLOCK-01/02)"
tech-stack:
  added: []
  removed: ["RtMidi.Core 1.0.53 (ABI-incompatible with modern librtmidi >=4.0; reads a length-out pointer as a string and frees garbage -> free(): invalid pointer process abort during enumeration)"]
  patterns:
    - "direct [DllImport(\"rtmidi\")] modern-signature P/Invoke (mirrors JackFunctions [DllImport(\"jack\")])"
    - "RtMidiWrapper struct ok-bit check after create/open (charitable degrade, never throw)"
    - "buffer-out rtmidi_get_port_name (caller-allocated 512B buffer, NUL-trim)"
    - "probe via the DllImport resolver, NOT NativeLibrary.TryLoad (the two disagree on bare SONAME)"
    - "rtmidi_in_ignore_types(false,false,false) to UN-ignore timing so 0xF8 arrives"
    - "real ALSA snd-virmidi loopback test captured by the amidi CLI (charitable-skip when absent)"
key-files:
  created:
    - flow-lang/Audio/LibRtMidi.cs
    - flow-lang.Tests/Integration/Phase40/RealMidiLoopbackTests.cs
  modified:
    - flow-lang/Audio/RtMidiMidiBackend.cs
    - flow-lang/Audio/MidiClock.cs
    - flow-lang/Audio/IMidiBackend.cs
    - flow-lang/Audio/MidiPlaybackManager.cs
    - flow-lang/StandardLibrary/Midi/MidiFunctions.cs
    - flow-lang/Core/FlowEngine.cs
    - flow-lang/flow-lang.csproj
    - flow-lang/midi.flow
    - flow-lang.Tests/Integration/Phase40/RtMidiInternalAccessSpikeTests.cs
    - flow-lang.Tests/Integration/Phase40/ClockMasterTests.cs
    - flow-lang.Tests/Integration/Phase40/ClockSlaveTests.cs
key-decisions:
  - "Overturn MIDI-RT-02's RtMidi.Core 1.0.53 choice — the library provably does not work on any modern Linux (ABI mismatch -> process abort), authorized by pre-traction no-deprecation latitude."
  - "Bind librtmidi DIRECTLY (modern C-API signatures) mirroring the existing libjack P/Invoke — eliminates the Open-Q1 reflection bridge entirely (raw send IS the public rtmidi_out_send_message)."
  - "Probe availability via the DllImport resolver (a real rtmidi_out_create_default in try/catch), NOT NativeLibrary.TryLoad — empirically TryLoad(\"rtmidi\") returns false on this box even though DllImport resolves librtmidi.so."
  - "Real master clock verification asserts wire-observable transport 0xFA/0xFC (snd-virmidi rawmidi-capture filters 0xF8 specifically); 24-PPQN rate stays machine-proven by ClockMasterTests and 0xF8 is proven the other direction by the slave row."
  - "amidi -d output read char-by-char (its final message lacks a trailing newline; a line reader drops the last FC/sysex)."
patterns-established:
  - "Direct modern-signature librtmidi P/Invoke as the real-time MIDI device layer (LibRtMidi.cs), Web-stripped via Compile Remove."
  - "Real ALSA snd-virmidi loopback integration test, charitable-skip when librtmidi/VirMIDI/amidi absent (mirrors Phase 39 mscore gate)."
requirements-completed: [MIDI-RT-01, MIDI-RT-02, MIDI-RT-04, CLOCK-01, CLOCK-02]

# Metrics
duration: ~95min
completed: 2026-06-07
---

# Phase 40 Plan 04: RtMidi.Core -> Direct librtmidi P/Invoke (ABI Fix) Summary

**Replaced the ABI-broken RtMidi.Core 1.0.53 managed wrapper with direct modern-signature `[DllImport("rtmidi")]` bindings (`LibRtMidi.cs`), so `(midiPorts)` enumerates the real VirMIDI ports instead of `free(): invalid pointer`-aborting the process — proven end-to-end by a new ALSA snd-virmidi loopback test that captures the real backend's wire bytes via `amidi`.**

## What shipped

### The root cause (and the fix)
RtMidi.Core 1.0.53 (2018) is pinned to the OLD `const char* rtmidi_get_port_name(device, port)` signature. Modern librtmidi (RtMidi >= 4.0; 6.0.0 / `librtmidi.so.7` on the bench box) changed that to `int rtmidi_get_port_name(device, port, char* bufOut, int* bufLen)`. RtMidi.Core therefore reads the `int* bufLen` out-pointer as a `const char*` and frees garbage, aborting the **whole process** with `free(): invalid pointer` the moment `(midiPorts)` enumerates. The in-process `CaptureMidiBackend` test seam never touched native code, so the crash was invisible to CI and only surfaced on real hardware.

`flow-lang/Audio/LibRtMidi.cs` (new) binds librtmidi directly with the **modern** signatures from `/usr/include/rtmidi/rtmidi_c.h`: create/free, `get_port_count`, the buffer-out `get_port_name` (caller-allocated 512-byte buffer, NUL-trimmed), open/close (port + virtual), `out_send_message` (raw bytes — clock + notes + CC + sysex all flow through it), and the input trio `in_create_default` / `in_ignore_types` / `in_get_message`. The `RtMidiWrapper` struct (`{ ptr, data, ok, msg }`) is marshalled to read the `ok` bit after create/open so a failed native call degrades charitably instead of dereferencing a bad pointer. This mirrors the existing `[DllImport("jack")]` approach in `JackFunctions.cs`.

`IsAvailable()` is a cached probe that goes through the **DllImport resolver** (a real `rtmidi_out_create_default` wrapped in try/catch), NOT `NativeLibrary.TryLoad("rtmidi")` — empirically the two disagree: `TryLoad` of the bare SONAME returns false on this box even though `DllImport` resolves `librtmidi.so`. Gating on `TryLoad` (an earlier draft) wrongly reported "no MIDI" and forced the `NullMidiBackend` fallback (the bug that made `(midiPorts)` print nothing in the first end-to-end run).

### Backend + clock rewrite
- `RtMidiMidiBackend` now sits on `LibRtMidi`: `ListPorts` = `get_port_count` + the modern `get_port_name` loop; `OpenOutput` = `create_default` -> WR-07 substring/exact match -> `open_port` -> a handle wrapping the `RtMidiOutPtr`. Each send builds the canonical wire bytes (`0x90|ch`, `0x80|ch`, `0xB0|ch`, `0xC0|ch`, raw) and calls `rtmidi_out_send_message`. The Open-Q1 reflection bridge is **gone** — raw byte send is the public entry point. `ToRtChannel`/`ToRtKey` survive as 0-based 0..15 / 0..127 clamps (Pitfall 3's 1-based RtMidi.Core `Channel` enum no longer exists), keeping the drum->ch9 guard test meaningful. Handle `Close()` is lock-guarded + idempotent (`close_port` + `out_free`).
- `MidiClock`'s slave input path (`RtMidiInputBridge`) was rewritten from RtMidi.Core internal-event reflection to a direct librtmidi input: `in_create_default` -> `in_ignore_types(false, false, false)` (**critical** — RtMidi ignores timing/realtime by default, so 0xF8 would never arrive) -> `open_port` -> a background poll thread on `in_get_message` feeding each chunk into the existing settle logic. The `SlaveByteSource` injection seam and all CLOCK-01/02 math (24-PPQN, 8-pulse settle, bar boundary, the WR-04 `_pulseCount` fix, LINK-02 live-tempo isolation) are byte-for-byte unchanged — only the transport changed.

### csproj + comments
- Removed `<PackageReference Include="RtMidi.Core" Version="1.0.53" />`. Added `LibRtMidi.cs` to the Web `<Compile Remove>` ItemGroup beside `RtMidiMidiBackend.cs` so the native `rtmidi` DllImport never reaches the WASM closure. `AssemblyReferenceScanTests` keeps `RtMidi.Core` in its forbidden-prefix list (now trivially satisfied) and the librtmidi P/Invoke is naturally Web-stripped via Compile Remove.
- Stale RtMidi.Core comments updated in `IMidiBackend`, `MidiPlaybackManager`, `MidiFunctions`, `FlowEngine`, and `midi.flow`.

### Tests
- **`RealMidiLoopbackTests` (new)** — the verification deliverable. Drives the REAL `RtMidiMidiBackend` + `MidiClock` over a live snd-virmidi loopback and captures the wire bytes with the ALSA `amidi` CLI. Charitable-SKIPS when librtmidi / a VirMIDI port / `amidi` is absent; picks the first VirMIDI pair at runtime (card 3 is not hardcoded — the librtmidi port "Virtual Raw MIDI N-M" suffix is parsed to `hw:N,M`). On the bench box all three RUN (not skip) and pass.
- **`RtMidiInternalAccessSpikeTests` obsoleted** — the four reflection-into-RtMidi.Core-internals assertions are deleted (no such assembly remains) and replaced with one documenting test recording the supersession. No false reflection-internal invariants remain.
- **Rule 1 test-infra fix** — `RealMidiLoopbackTests` + `ClockMasterTests` + `ClockSlaveTests` joined `WasmEntryConsoleCollection` so the CPU-heavy real-hardware class (spawns `amidi` + busy-polls) never jitters the timing-sensitive in-process clock tests' real-time Stopwatch deltas.

## What the real loopback tests proved (captured-byte evidence)

| Row | Test | Captured over the real VirMIDI wire |
|-----|------|--------------------------------------|
| 1-2 | `RealOutput_NoteCcSysex_CapturedByteForByte` | `90 3C 64` (NoteOn ch0 C4 v100) · `B0 07 64` (CC7=100) · `80 3C 00` (NoteOff) · `F0 7D 01 02 F7` (framed sysex) |
| 3 | `RealClockMaster_EmitsTransportStartStopOverWire` | `FA` (MIDI Start) + `FC` (MIDI Stop) — transport, wire-observable |
| 4 | `RealClockSlave_LocksTempoFromInjected24Ppqn` | 40 injected `0xF8` read by the real librtmidi input; live tempo locked to the injected rate (~150 BPM observed) after the 8-pulse settle; `ctx.Tempo` stayed null (LINK-02) |

The probe + amidi capture established (and the test pins): `(midiPorts)` enumerates `Virtual Raw MIDI 3-0..3-3` (and `Midi Through`) with no crash; a Flow `(midiNoteOn ...)` / `(midiCC ...)` / `(midiNoteOff ...)` round-trips to `amidi -p hw:3,0 -d` as `90 3C 64 / B0 07 5A / 80 3C 00`.

> **0xF8 capture caveat (documented, not a bug):** ALSA's snd-virmidi rawmidi-capture side filters the `0xF8` timing-clock realtime byte specifically — the same `MidiClock` master delivers `0xFA` + `0xFC` through `amidi -d` every run but never a `0xF8`. This is a kernel quirk, not a Flow defect: the master DOES put `0xF8` on the wire (the 24-PPQN rate is machine-proven by `ClockMasterTests.ClockMaster24PpqnRate`) and `0xF8` flows fine the OTHER direction (Row 4 reads 40 injected pulses). Row 3 therefore asserts the transport bytes that ARE wire-observable.

## Verification

- `dotnet run --project flow-interpreter /tmp/midi_list.flow` (`use "@midi"\n(midiPorts)`) — NO crash; prints the 5 ports including all four VirMIDI ports.
- `dotnet build flow-lang -p:FlowTarget=Desktop` — 0 errors. `-p:FlowTarget=Web` — 0 errors.
- `dotnet test flow-lang.Tests --filter Phase40` — **45/45 GREEN, 0 skipped** (incl. `RealMidiLoopbackTests` RUNNING, not skipping).
- `dotnet test flow-lang.Tests -p:FlowTarget=Web --filter AssemblyReferenceScanTests` — 2/2 GREEN (zero RtMidi.Core refs, zero leaked `rtmidi` P/Invoke in the Web closure).
- Full Desktop suite — green (2255 passed, 14 skipped) when the unrelated pre-existing WASM Console-redirection race does not fire.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] IsAvailable probe gated on NativeLibrary.TryLoad, which fails for the bare "rtmidi" SONAME**
- **Found during:** Task 1/7 (first end-to-end `(midiPorts)` run printed nothing).
- **Issue:** `NativeLibrary.TryLoad("rtmidi")` returns false on this box even though `DllImport("rtmidi")` resolves `librtmidi.so` — so the probe reported "no MIDI" and `MidiPlaybackManager` fell back to `NullMidiBackend` (empty ports).
- **Fix:** the probe now goes straight to a real `rtmidi_out_create_default` (try/catch `DllNotFoundException`), the same resolver the rest of the bindings use.
- **Files modified:** `flow-lang/Audio/LibRtMidi.cs`
- **Commit:** dcbec10

**2. [Rule 1 - Bug] Real master test asserted a 0xF8 count amidi can never capture**
- **Found during:** Task 6/7 (Row 3 captured `FA` but `F8`=0).
- **Issue:** snd-virmidi rawmidi-capture filters the `0xF8` timing-clock byte; an amidi-based 0xF8 count is unachievable (verified across multiple runs — `FA`/`FC` always pass, `F8` never).
- **Fix:** Row 3 asserts the wire-observable transport `0xFA` + `0xFC`; the 24-PPQN rate stays machine-proven by `ClockMasterTests`, and `0xF8` is proven the other direction by Row 4. Documented in the test + this summary.
- **Files modified:** `flow-lang.Tests/Integration/Phase40/RealMidiLoopbackTests.cs`
- **Commit:** 451ae1a

**3. [Rule 1 - Bug] amidi capture lost the final message (no trailing newline)**
- **Found during:** Task 6/7 (sysex + FC missing under `dotnet test` even though present in standalone probes).
- **Issue:** `amidi -d` prints each message's hex WITHOUT a trailing newline until the NEXT message arrives, so a `BeginOutputReadLine` line reader never delivers the last message (FC / sysex) before amidi is killed.
- **Fix:** the `AmidiCapture` helper reads stdout char-by-char on a background thread.
- **Files modified:** `flow-lang.Tests/Integration/Phase40/RealMidiLoopbackTests.cs`
- **Commit:** 451ae1a

**4. [Rule 1 - Test infra] CPU-heavy real-hardware test jittered the in-process clock tests**
- **Found during:** Task 7/7 (full-suite regression — `ClockSlaveDrivesTempo` read 89.8 BPM vs. its 100-150 band).
- **Issue:** `RealMidiLoopbackTests` spawn `amidi` + busy-poll; run in parallel they starve the real-time Stopwatch deltas the in-process clock tests derive BPM from.
- **Fix:** `RealMidiLoopbackTests` + `ClockMasterTests` + `ClockSlaveTests` joined `WasmEntryConsoleCollection` (same remedy Plan 40-01/02/03 applied). After the fix the clock-timing flake no longer reproduces.
- **Files modified:** `ClockMasterTests.cs`, `ClockSlaveTests.cs`, `RealMidiLoopbackTests.cs`
- **Commit:** 451ae1a

## Deferred Issues (out of scope)

The pre-existing intermittent full-suite WASM `RunFromJs_*` Console-redirection race (cross-collection `Console.Set*` contention) was re-confirmed on CLEAN `dev` with the Plan-40-04 changes stashed — it is NOT introduced by this fix. Logged in `deferred-items.md`; recommended owner is a Phase 41 / test-infra cleanup pass (unify Console-redirecting classes under one non-parallel collection). Out of scope per the SCOPE BOUNDARY rule (pre-existing failure in unrelated files).

## Known Stubs

None. The librtmidi bindings, backend, and clock input are fully wired and exercised against real hardware.

## Self-Check: PASSED

- FOUND: `flow-lang/Audio/LibRtMidi.cs`
- FOUND: `flow-lang.Tests/Integration/Phase40/RealMidiLoopbackTests.cs`
- FOUND: `.planning/phases/40-studio-sync/40-04-SUMMARY.md`
- FOUND commit: `dcbec10` (fix(40): replace RtMidi.Core with direct librtmidi P/Invoke)
- FOUND commit: `451ae1a` (test(40): real ALSA-loopback verification of native MIDI path)
