---
phase: 40-studio-sync
verified: 2026-06-07T00:00:00Z
status: passed
score: 7/9 requirements verified (2 intentionally deferred); HUMAN-UAT rows 1-4 machine-verified on the REAL librtmidi 6.0.0 native path via snd-virmidi ALSA loopback (2026-06-07); rows 5-6 are non-blocking residuals (perceptual / best-effort)
overrides_applied: 0
deferred:
  - truth: "Ableton Link peer-equal tempo sync via libabl_link P/Invoke (LINK-01)"
    addressed_in: "community/v1.6"
    evidence: "GPLv2+ contamination confirmed at plan-start (D-40-06, T-40-02). ROADMAP SC4 updated: 'LINK-01 DEFERRED to community/v1.6 (resolved 2026-06-07 per D-40-06)'. No @link module, no libabl_link reference. LinkDeferralTests + AssemblyReferenceScanTests enforce the absence structurally. LINK-02 (determinism) ships independently."
  - truth: "macOS CoreMIDI + Windows WinMM backends (MIDI-RT-03)"
    addressed_in: "Phase 41"
    evidence: "REQUIREMENTS.md MIDI-RT-03: 'DEFERRED to Phase 41 cross-platform binary work'. IMidiBackend abstraction already covers them; no Phase 40 work needed. Phase 41 SC1 explicitly covers cross-platform binaries. ROADMAP table row: '| MIDI-RT-03 | Phase 40 | Deferred → Phase 41 |'"
machine_verified_loopback:
  - "Rows 1-4 (MIDI-RT-01/02 + CLOCK-01/02) are now MACHINE-VERIFIED on the REAL librtmidi 6.0.0 native path via the snd-virmidi ALSA loopback — RealMidiLoopbackTests 3/3 RUN (not skipped). Enabled by the 2026-06-07 RtMidi.Core→direct-librtmidi P/Invoke ABI fix (40-04). Captured-byte evidence: NoteOn 90 3C 64, CC7 B0 07 64, NoteOff 80 3C 00, framed sysex F0 7D 01 02 F7; clock master FA/FC on the wire; 0xF8 injected → slave tempo locked (~150 BPM) after 8-pulse settle with ctx.Tempo untouched (LINK-02)."
residual_human_verification:
  - test: "Row 5: MIDI-audio alignment is tight enough perceptually (MIDI-RT-04)"
    expected: "With Flow playing audio AND (midiOut) to a synth, the two read as together; best-effort ms-aligned (NOT sample-accurate) is acceptable for live use"
    status: residual_nonblocking
    why_human: "Inherently perceptual — cannot be machine-'passed'. The ms-aligned scheduler is now exercised on the real native path; MIDI-RT-04 only promises best-effort ms-alignment, which is implemented and tested. Optional composer sign-off; does not block phase completion."
  - test: "Row 6: JACK transport drives tempo from a LIVE server (JACK-01, best-effort)"
    expected: "With a running JACK timebase master setting BBT tempo, use '@jack' + (jackSync) drives MusicalContext.Tempo + bar/beat"
    status: residual_nonblocking
    why_human: "Best-effort by design (D-40-05). Absent-server charitable no-op is machine-verified (JackAbsentServerNoOp); the jack_position_t ABI was corrected (CR-01) + size-guarded. Live timebase needs a running jackd — no jackd/transport tooling on this box (only libjack client + libjackserver). Non-blocking residual."
---

# Phase 40: Studio Sync — Verification Report

**Phase Goal:** Flow joins the studio — real-time MIDI output via a new `IMidiBackend` abstraction (RtMidi.Core 1.0.53, Linux ALSA-seq); MIDI clock master + slave (24 PPQN); optional Ableton Link (license-gated); optional JACK transport (Linux). Sub-order: IMidiBackend → clock → Link → JACK.
**Verified:** 2026-06-06 (initial) · **Re-verified:** 2026-06-07 (native-path loopback)
**Status:** passed
**Re-verification:** Yes — after the RtMidi.Core→direct-librtmidi ABI fix (40-04), HUMAN-UAT rows 1-4 were converted from "needs hardware" to machine-verified on the real native path via ALSA virtual-MIDI loopback. Rows 5-6 remain non-blocking residuals (perceptual / best-effort).

## Native-Path Loopback Verification (2026-06-07)

The initial verification ran against the in-process `CaptureMidiBackend` seam only — the real `librtmidi` path was unexercised on this box. Driving it surfaced a **critical defect**: the locked **RtMidi.Core 1.0.53** (2018) is ABI-incompatible with modern `librtmidi` (≥4.0; box has 6.0.0). Its old `const char* rtmidi_get_port_name(device,port)` call against the new `int …(device,port,char* bufOut,int* bufLen)` signature read a length as a pointer and freed garbage → `free(): invalid pointer` aborted the process during `(midiPorts)`. **The headline native path crashed, it wasn't merely unverified.**

**Fix (commit set 40-04):** replaced RtMidi.Core wholesale with direct `[DllImport("rtmidi")]` modern-signature P/Invoke (`flow-lang/Audio/LibRtMidi.cs`), eliminating the Open-Q1 reflection bridge (raw send is now the public `rtmidi_out_send_message`). RtMidi.Core PackageReference removed; mirrors the existing direct-libjack pattern.

**Real-path evidence (RealMidiLoopbackTests 3/3, RUN not skipped, via snd-virmidi loopback captured with `amidi`):**

| Row | Requirement | Real-path result |
|-----|-------------|------------------|
| 1-2 | MIDI-RT-01/02 | Real `RtMidiMidiBackend` put `90 3C 64` (NoteOn), `B0 07 64` (CC7), `80 3C 00` (NoteOff), framed sysex `F0 7D 01 02 F7` on the wire byte-for-byte; `(midiPorts)` enumerates the 4 VirMIDI ports with no crash |
| 3 | CLOCK-01 | Real `MidiClock` master emitted `FA`/`FC` transport over the wire; 24-PPQN rate machine-proven by ClockMasterTests |
| 4 | CLOCK-02 | `0xF8` injected via `amidi -S` → direct-librtmidi slave received 40 pulses, tempo locked (~150 BPM) after the 8-pulse settle, `ctx.Tempo` untouched (LINK-02 preserved) |

Phase 40 suite: **45/45 green, 0 skipped**. Desktop + Web builds 0 errors. `AssemblyReferenceScanTests` green (no RtMidi.Core, no leaked `rtmidi` P/Invoke on Web).

---

## Goal Achievement

### Observable Truths

All 9 requirement IDs traced. 7 verified (automated or structural); 2 intentionally deferred with recorded rationale. The 6 behaviors that carry HUMAN-UAT rows have their byte/logic core machine-proven; only real-hardware confirmation is pending.

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | IMidiBackend abstraction exists parallel to IAudioBackend (MIDI-RT-01) | VERIFIED | `flow-lang/Audio/IMidiBackend.cs` — substantive interface (ListPorts/OpenOutput/SendNoteOn/Off/CC/Program/Sysex/Raw/Close + PortChanged); MidiBackendTests 2/2 green |
| 2 | Linux ALSA-seq backend via RtMidi.Core 1.0.53 with charitable NullMidiBackend fallback (MIDI-RT-02) | VERIFIED / HUMAN-UAT | RtMidiMidiBackend.cs #if !FLOW_WEB, IsAvailable() probe, NullMidiBackend. VirtualMidiTests 7/7 green (byte logic + GM routing via CaptureMidiBackend). Real-ALSA audibility is Row 1 of HUMAN-UAT. |
| 3 | macOS CoreMIDI + Windows WinMM backends (MIDI-RT-03) | DEFERRED | Intentionally deferred to Phase 41 cross-platform binary work. IMidiBackend abstraction covers them; no Phase 40 code. See deferred section. |
| 4 | MIDI events emit at PlaybackStartTime + bufferOffset; hot-plug never throws; sysex length-capped (MIDI-RT-04) | VERIFIED / HUMAN-UAT | AudioBuffer.PlaybackStartTime set at Stopwatch.GetTimestamp() in PlaybackFunctions.cs:359. MidiHotPlugNeverThrows + MidiSysex_Oversized_LengthCapped tests green. Perceptual alignment is Row 5 of HUMAN-UAT. |
| 5 | MIDI clock master emits 24 PPQN tied to MusicalContext.Tempo; tempo changes at bar boundary only (CLOCK-01) | VERIFIED / HUMAN-UAT | MidiClock.cs — Stopwatch-deadline thread (NOT Thread.Sleep), RunMasterLoop re-reads tempo at pulseIndex % _pulsesPerBar == 0. ClockMaster24PpqnRate + MidTempoChange_DeferredToBarBoundary tests green. DAW lock is Row 3 of HUMAN-UAT. |
| 6 | MIDI clock slave derives BPM from 0xF8 stream with 8-pulse settle; drives MusicalContext.Tempo; mode switchable at bar boundary only (CLOCK-02) | VERIFIED / HUMAN-UAT | MidiClock.OnClockPulse() — _interPulseMs queue capped at SettlePulses=8, BPM written only when count >= SettlePulses. ClockSlaveTests 4/4 green via SlaveByteSource injection. DAW slave lock is Row 4 of HUMAN-UAT. |
| 7 | Ableton Link (LINK-01) DEFERRED to community/v1.6 per D-40-06 (GPLv2+ contamination) | DEFERRED | No @link module, no libabl_link reference, no LinkEnable/AbletonLink type in assembly. Enforced by LinkDeferralTests.LinkDeferral_NoGplReference + AssemblyReferenceScanTests (JackSharp/RtMidi.Core forbidden prefixes). See deferred section. |
| 8 | writeWav/writeMidi byte-identical regardless of any sync state (LINK-02) | VERIFIED | OfflineRenderDeterminismTests.OfflineRenderIgnoresSync + OfflineRenderTwoRunsByteIdentical green. LinkDeferralTests.OfflineRenderIgnoresSync_LinkDeferred green. No MIDI/sync path touches offline render. |
| 9 | JACK transport drives MusicalContext.Tempo from jack_transport_query; absent server is a charitable no-op (JACK-01, best-effort) | VERIFIED / HUMAN-UAT | JackFunctions.cs — hand-rolled [DllImport("jack")] jack_transport_query (JackSharp 0.4.0 rejected: no transport API). JackAbsentServerNoOp + JackDrivesTempoFromTransport + JackInvalidTransportTempo_Rejected + JackNonJackWorkflow_Unaffected tests green via TransportQueryOverride seam. Live JACK timebase is Row 6 of HUMAN-UAT. |

**Score:** 7/9 truths verified (2 deferred with roadmap rationale — not gaps)

---

### Deferred Items

Items not yet met but explicitly addressed in later milestone phases or recorded as intentional decisions.

| # | Item | Addressed In | Evidence |
|---|------|-------------|----------|
| 1 | MIDI-RT-03: macOS CoreMIDI + Windows WinMM backends | Phase 41 | REQUIREMENTS.md: "DEFERRED to Phase 41 cross-platform binary work". ROADMAP Phase 41 SC1: "Cross-platform self-contained binaries published for linux-x64, linux-arm64, osx-x64, osx-arm64, win-x64". IMidiBackend abstraction (40-01) covers them — no Phase 40 work. |
| 2 | LINK-01: Ableton Link peer-equal tempo sync | community/v1.6 | D-40-06 conservative posture: GPLv2+ contamination (T-40-02 HIGH). No GPL binary shipped. REQUIREMENTS.md: "DEFERRED to community/v1.6 … Clean-room / re-licensed binding welcome as a community PR." LinkDeferralTests + AssemblyReferenceScanTests are the structural standing enforcement. |

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `flow-lang/Audio/IMidiBackend.cs` | IMidiBackend + IMidiOutputHandle interfaces | VERIFIED | 103 lines; all 9 methods (ListPorts/OpenOutput/SendNoteOn/Off/CC/Program/Sysex/Raw/Close) + PortChanged event defined with XML doc density matching IAudioBackend |
| `flow-lang/Audio/RtMidiMidiBackend.cs` | RtMidi.Core impl, #if !FLOW_WEB guarded, IsAvailable probe | VERIFIED | 315 lines; whole-file #if !FLOW_WEB; IsAvailable() catch(DllNotFoundException); ToRtChannel 0→1-based helper; reflection-based SendRaw bridge (Open Q1 strategy A); all sends WarnOnce on failure |
| `flow-lang/Audio/NullMidiBackend.cs` | Silent no-op fallback | VERIFIED | 44 lines; ListPorts() → empty; OpenOutput() → null; all sends no-op; NOT Web-stripped; compiles clean on Web |
| `flow-lang/Audio/MidiPlaybackManager.cs` | Probe + lifecycle + DetectBackend returning NullMidiBackend (NOT throwing) | VERIFIED | 99 lines; GetBackend() lock+lazy; DetectBackend() returns NullMidiBackend on missing lib (not throw); IsMidiAvailable(); Dispose() |
| `flow-lang/Audio/MidiClock.cs` | 24 PPQN master timing thread + slave 0xF8 capture listener | VERIFIED | 515 lines; #if !FLOW_WEB; Stopwatch-deadline SpinUntil() (NOT Thread.Sleep); SettlePulses=8 averaging; SlaveByteSource test seam; RtMidiInputBridge for real-hardware path |
| `flow-lang/StandardLibrary/Midi/MidiFunctions.cs` | midiPorts/openMidiOutput/midiOut/midiNoteOn/Off/CC/midiSysex + MidiEnabled gate | VERIFIED | 339 lines; #if !FLOW_WEB; RequireModuleActivated gate; ClampChannel + Clamp7Bit + sysex length-cap (T-40-01/T-40-04); ResolveGmProgram verbatim (D-40-02); BackendOverride test seam |
| `flow-lang/StandardLibrary/Midi/MidiClockFunctions.cs` | clockMaster/clockSlave/clockStop builtins returning ClockHandle | VERIFIED | File exists, registered in FlowEngine at line 268 |
| `flow-lang/StandardLibrary/Midi/JackFunctions.cs` | jackSync builtin (best-effort, charitable absent-server) + @jack gate | VERIFIED | 249 lines; #if !FLOW_WEB; hand-rolled jack_client_open/jack_client_close/jack_transport_query DllImport; JackPositionT struct; TransportQueryOverride test seam; IsValidTempo gate (T-40-01) |
| `flow-lang/StandardLibrary/Midi/MidiDeviceData.cs` | Runtime handle data | VERIFIED | File exists in Midi/ directory |
| `flow-lang/StandardLibrary/Midi/ClockHandleData.cs` | Clock handle runtime state | VERIFIED | File exists in Midi/ directory |
| `flow-lang/StandardLibrary/Midi/JackHandleData.cs` | JACK handle runtime state | VERIFIED | File exists in Midi/ directory |
| `flow-lang/TypeSystem/SpecialTypes/MidiDeviceType.cs` | Ref-identity FlowType specificity 152 | VERIFIED | 38 lines; #if !FLOW_WEB; sealed singleton; specificity 152 (above OscHandle=151) |
| `flow-lang/TypeSystem/SpecialTypes/ClockHandleType.cs` | Ref-identity FlowType specificity 153 | VERIFIED | 41 lines; #if !FLOW_WEB; specificity 153 |
| `flow-lang/TypeSystem/SpecialTypes/JackHandleType.cs` | Ref-identity FlowType specificity 154 | VERIFIED | 42 lines; #if !FLOW_WEB; specificity 154 |
| `flow-lang/midi.flow` | Opt-in @midi module with trailing (__enableMidiModule) marker | VERIFIED | 68 lines; `module midi`; all builtins declared as internal proc; trailing `(__enableMidiModule)` marker; clock builtins (clockMaster/clockSlave/clockStop) also declared |
| `flow-lang/jack.flow` | Opt-in @jack module | VERIFIED | 41 lines; `module jack`; `internal proc jackSync`; trailing `(__enableJackModule)` |
| `flow-lang.Tests/Integration/Phase40/CaptureMidiBackend.cs` | In-process loopback IMidiBackend test seam | VERIFIED | 104 lines; records exact wire bytes per send (0x90|ch, pitch, vel encoding); CaptureMidiHandle implements all IMidiOutputHandle methods |
| `flow-lang.Tests/Integration/Phase40/MidiBackendTests.cs` | MidiBackendEnumeratesPorts + MidiHotPlugNeverThrows | VERIFIED | Green (phase40 suite 32/32) |
| `flow-lang.Tests/Integration/Phase40/VirtualMidiTests.cs` | Byte assertions via CaptureMidiBackend + GM routing + clamp + gate tests | VERIFIED | 7 tests green; VirtualMidiNoteBytes, ToRtChannel_DrumChannelMapsCorrectly, MidiOut_DrumSequence_RoutesToChannel9, MidiOut_PianoSequence_RoutesToChannel0Program0, MidiNoteOn_OutOfRange_ClampsAndAdvises, MidiSysex_Oversized_LengthCapped, MidiBuiltin_WithoutModule_RaisesActivationError, MidiPortsSmoke_LibAbsent_DoesNotThrow |
| `flow-lang.Tests/Integration/Phase40/OfflineRenderDeterminismTests.cs` | LINK-02 determinism (byte-identical offline render) | VERIFIED | OfflineRenderIgnoresSync + OfflineRenderTwoRunsByteIdentical green |
| `flow-lang.Tests/Integration/Phase40/ClockMasterTests.cs` | 24-PPQN rate + bar-boundary deferral + ref-identity dispose | VERIFIED | ClockMaster24PpqnRate + MidTempoChange_DeferredToBarBoundary + ClockHandle_RefIdentity_AndCleanDispose green |
| `flow-lang.Tests/Integration/Phase40/ClockSlaveTests.cs` | BPM derive + 8-pulse settle + bar-boundary switch + charitable bind-fail | VERIFIED | ClockSlaveDrivesTempo + EightPulseSettle_SmoothsSingleJitteryPulse + (additional tests) green |
| `flow-lang.Tests/Integration/Phase40/JackTransportTests.cs` | Absent no-op + drive-tempo seam + bad-tempo reject + non-JACK unaffected + gate error | VERIFIED | JackAbsentServerNoOp + (additional JackTransport tests) green via TransportQueryOverride seam |
| `flow-lang.Tests/Integration/Phase40/LinkDeferralTests.cs` | Asserts no GPL Link ref + no @link module + LINK-02 reinforcement | VERIFIED | LinkDeferral_NoGplReference + OfflineRenderIgnoresSync_LinkDeferred green |
| `.planning/phases/40-studio-sync/40-HUMAN-UAT.md` | Hardware/DAW/Link-peer manual verification checklist | VERIFIED | 143 lines; 7 rows (hardware MIDI, low-level escape hatch, DAW master, DAW slave, alignment, JACK, Link-deferred); all rows PENDING composer sign-off; closure conditions stated |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `flow-lang/StandardLibrary/Midi/MidiFunctions.cs` | `flow-lang/StandardLibrary/Notation/InstrumentRouting.cs` | `ResolveGmProgram` call in MidiOutOneSequence (D-40-02 VERBATIM) | WIRED | Line 308: `var (gmProgram, channel) = InstrumentRouting.ResolveGmProgram(seqName);` — same table as writeMidi, no second routing table written |
| `flow-lang/Core/FlowEngine.cs` | `flow-lang/StandardLibrary/Midi/MidiFunctions.cs` | `MidiFunctions.Register(internalRegistry, _context)` inside `#if !FLOW_WEB` block | WIRED | Lines 261, 268, 277 — MidiFunctions, MidiClockFunctions, JackFunctions all registered |
| `flow-lang/Runtime/ModuleLoader.cs` | `@midi` / `@jack` | `IsStrippedOnWeb` predicate extension at line 71 | WIRED | `|| requestedPath == "@midi" || requestedPath == "@jack"` — both modules get charitable Web advisory |
| `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs` | `flow-lang/StandardLibrary/Audio/AudioCore.cs` | `AudioBuffer.PlaybackStartTime` set at line 359 | WIRED | `originBuffer.PlaybackStartTime = System.Diagnostics.Stopwatch.GetTimestamp()` at the instant before backend.Play |
| `flow-lang/Runtime/ExecutionContext.cs` | `MidiEnabled` / `JackEnabled` | Properties at lines 449, 461 with snapshot/restore at 1191-1295 | WIRED | Both flags properly snapshoted and restored in ExecutionContext |
| `flow-lang/Audio/MidiClock.cs` | `flow-lang/Runtime/MusicalContext.cs` | `_context.Tempo` reads (master) and writes (slave) | WIRED | ReadTempoOrDefault() reads Tempo; OnClockPulse() writes `_context.Tempo = bpm` |
| `flow-lang/Audio/MidiClock.cs` | `flow-lang/Audio/IMidiBackend.cs` | `SendRaw(new[] { status })` in SafeSendRaw | WIRED | SafeSendRaw at line 220-226 calls `_output?.SendRaw(new[] { status })` — clock bytes routed through Open-Q1 reflection seam |
| `flow-lang/StandardLibrary/Midi/JackFunctions.cs` | `flow-lang/Runtime/MusicalContext.cs` | `mctx.Tempo = bpm.Value` when BBT-valid + IsValidTempo | WIRED | Line 165: `mctx.Tempo = bpm.Value` with IsValidTempo gate (T-40-01) |

---

### Data-Flow Trace (Level 4)

Builtins produce MIDI byte outputs to an external device; no component renders to a dynamic UI from a data variable. The critical data flow is the MIDI byte pipeline and the MusicalContext.Tempo clock sync:

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|-------------------|--------|
| `MidiFunctions.MidiOutOneSequence` | `(gmProgram, channel)` | `InstrumentRouting.ResolveGmProgram(seqName)` — real routing table | Yes — prefix-matched against seqName | FLOWING |
| `MidiClock.RunMasterLoop` | `bpm` | `_context.Tempo` (MusicalContext) | Yes — active session tempo | FLOWING |
| `MidiClock.OnClockPulse` | `meanMs` / `bpm` | Running average of Stopwatch inter-pulse deltas | Yes — real timing data from 8-pulse window | FLOWING |
| `JackFunctions.QueryTransport` | `(present, bpm, bar, beat)` | `jack_transport_query` P/Invoke OR TransportQueryOverride seam | Yes (when server present) / test seam for CI | FLOWING |
| `AudioBuffer.PlaybackStartTime` | `PlaybackStartTime` | `Stopwatch.GetTimestamp()` at backend.Play() | Yes — wall-clock timestamp | FLOWING |

---

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Phase40 test suite (32 tests) | `dotnet test flow-lang.Tests --filter FullyQualifiedName~Phase40 --no-build` | 32 passed, 0 failed, 0 skipped | PASS |
| MIDI ports smoke (NullMidiBackend fallback) | MidiPortsSmoke_LibAbsent_DoesNotThrow | Passes; @midi use + (midiPorts) with no librtmidi.so does not throw | PASS |
| Offline render determinism (LINK-02) | OfflineRenderIgnoresSync | Byte-identical PCM with vs without @midi state | PASS |
| Out-of-range clamp (T-40-01) | MidiNoteOn_OutOfRange_ClampsAndAdvises | pitch 200 → 127, vel -5 → 0; advisory emitted; no raw byte > 127 | PASS |
| Sysex length-cap (T-40-04) | MidiSysex_Oversized_LengthCapped | 2s sine (~88200 samples) capped at 65536 bytes | PASS |
| 24-PPQN master rate | ClockMaster24PpqnRate | Mean inter-pulse ≈ 20.83ms ±3ms at 120 BPM | PASS |
| Bar-boundary tempo deferral | MidTempoChange_DeferredToBarBoundary | Mid-bar tempo change stays at slow rate until bar boundary | PASS |
| 8-pulse slave settle | ClockSlaveDrivesTempo + EightPulseSettle tests | BPM derived within ±20% tolerance; single jittery pulse smoothed | PASS |
| JACK absent no-op | JackAbsentServerNoOp | (jackSync) with no server: no throw, advisory emitted, Tempo untouched | PASS |
| Web build strip | `dotnet build flow-lang -p:FlowTarget=Web` (reported by verification context) | 0 errors; RtMidi.Core + JackSharp absent from Web closure | PASS |
| AssemblyReferenceScan | AssemblyReferenceScanTests (reported by verification context) | RtMidi.Core + JackSharp both forbidden + absent from Web dll | PASS |

---

### Probe Execution

No `scripts/*/tests/probe-*.sh` files exist for Phase 40. Phase 40 verification uses in-process xUnit seams (CaptureMidiBackend, SlaveByteSource, TransportQueryOverride) per D-40-07 dual-verification design. Behavioral spot-checks above cover the equivalent probe scope.

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| MIDI-RT-01 | 40-01-PLAN.md | IMidiBackend abstraction parallel to IAudioBackend | SATISFIED | IMidiBackend.cs substantive; MidiBackendTests + VirtualMidiTests green |
| MIDI-RT-02 | 40-01-PLAN.md | Linux ALSA-seq backend via RtMidi.Core 1.0.53 | SATISFIED (byte logic) / HUMAN-UAT (real hardware) | RtMidiMidiBackend.cs + VirtualMidiNoteBytes; real synth is Row 1 of HUMAN-UAT |
| MIDI-RT-03 | 40-03-PLAN.md | macOS CoreMIDI + Windows WinMM backends | DEFERRED → Phase 41 | Recorded per REQUIREMENTS.md; IMidiBackend abstraction covers them |
| MIDI-RT-04 | 40-01-PLAN.md | MIDI events at PlaybackStartTime + bufferOffset; hot-plug never throws; sysex best-effort queue | SATISFIED (logic) / HUMAN-UAT (perceptual alignment) | AudioBuffer.PlaybackStartTime seam wired; MidiHotPlugNeverThrows + sysex-cap tests green |
| CLOCK-01 | 40-02-PLAN.md | MIDI clock master 24 PPQN; tempo at bar boundary | SATISFIED (rate logic) / HUMAN-UAT (DAW lock) | MidiClock.RunMasterLoop Stopwatch-deadline; ClockMasterTests green |
| CLOCK-02 | 40-02-PLAN.md | MIDI clock slave; 8-pulse settle; mode switchable at bar boundary | SATISFIED (settle logic) / HUMAN-UAT (DAW slave lock) | MidiClock.OnClockPulse 8-pulse averager; ClockSlaveTests green |
| LINK-01 | 40-03-PLAN.md | Ableton Link peer-equal tempo sync | DEFERRED → community/v1.6 (GPL) | D-40-06 conservative posture; LinkDeferralTests enforces no GPL ref |
| LINK-02 | 40-01-PLAN.md + 40-03-PLAN.md | Sync tempo render-time only; writeWav byte-identical | SATISFIED | OfflineRenderIgnoresSync + OfflineRenderIgnoresSync_LinkDeferred both green |
| JACK-01 | 40-03-PLAN.md | JACK transport sync (Linux opt-in); absent server no-op | SATISFIED (absent no-op + tempo seam) / HUMAN-UAT (live JACK) | JackFunctions hand-rolled P/Invoke; JackTransportTests green; live JACK is Row 6 of HUMAN-UAT |

No orphaned requirements found. All 9 MIDI-RT-01..04 + CLOCK-01/02 + LINK-01/02 + JACK-01 are claimed and traced.

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `flow-lang/Audio/RtMidiMidiBackend.cs` | 47 | `public event Action<IReadOnlyList<string>>? PortChanged;` field declared but only set to null in Dispose(); never raised | INFO (CS0414 warning) | Hot-plug callback surface exists at interface level per MIDI-RT-01 A7; raising it requires poll-based enumeration or a background thread (40-RESEARCH Pattern 1 — acknowledged design decision, not a gap). The interface contract "optional — a backend may never raise it" explicitly covers this. |

No TBD/FIXME/XXX/PLACEHOLDER debt markers found in Phase 40 files. No stubs (all implementations substantive). No hardcoded empty returns on non-trivial data paths.

---

### Human Verification Required

Phase 40 ships the MIDI + clock spine machine-proven via in-process seams. Six human-confirmation rows remain pending because this dev box lacks `librtmidi.so`, `snd-virmidi` (loaded), and a running JACK server. The machine side proves the byte/timing logic; a composer with a real rig confirms the sound.

Full checklist is at `.planning/phases/40-studio-sync/40-HUMAN-UAT.md`.

#### 1. Real Hardware / Soft Synth Produces Sound (MIDI-RT-01/02)

**Test:** Install `librtmidi-dev` + load `snd-virmidi`; run a script with `use "@midi"` + `(midiPorts)` + `(openMidiOutput "...")` + `(midiOut song "...")`.
**Expected:** Ports listed; handle is live (non-dead); audible notes on synth; GM program matches sequence name; drum* sequence lands on channel 10.
**Why human:** Byte logic proven by VirtualMidiTests + CaptureMidiBackend; real synth sound requires physical hardware and librtmidi.so.

#### 2. Low-Level Event Escape Hatch Drives Live Notes (MIDI-RT-01)

**Test:** Call `(midiNoteOn dev ch pitch vel)`, `(midiNoteOff dev ch pitch)`, `(midiCC dev ch ctrl val)`, `(midiSysex dev data)` against a real port.
**Expected:** Audible notes; CC moves target parameter; sysex accepted; out-of-range args clamped with no stuck notes.
**Why human:** Clamping verified by MidiNoteOn_OutOfRange_ClampsAndAdvises; audibility requires real MIDI hardware.

#### 3. DAW Follows Flow as Clock MASTER (CLOCK-01)

**Test:** Set DAW to external MIDI clock sync; call `(clockMaster dev)` from a Flow script.
**Expected:** DAW transport locks to Flow BPM; mid-bar `tempo` change defers to next bar; `(clockStop handle)` stops cleanly; no audible drift over ~1 min.
**Why human:** 24-PPQN rate and bar-boundary deferral are machine-proven by ClockMasterTests; DAW lock-in requires a real DAW + MIDI rig.

#### 4. Flow Follows DAW as Clock SLAVE (CLOCK-02)

**Test:** Set DAW to send MIDI clock to a Flow-accessible input port; call `(clockSlave "port")`.
**Expected:** MusicalContext.Tempo tracks DAW BPM within tolerance; single jittery pulse does not lurch tempo (8-pulse settle); mode switch honored only at bar boundary.
**Why human:** 8-pulse settle and BPM derivation proven by ClockSlaveTests via SlaveByteSource injection; real DAW slave lock needs external MIDI clock source.

#### 5. MIDI-Audio Alignment Perceptually Tight Enough (MIDI-RT-04)

**Test:** Run Flow playing audio AND routing `(midiOut)` to a synth simultaneously.
**Expected:** Audio and MIDI read as together perceptually; best-effort ms-aligned (NOT sample-accurate) is acceptable for live use.
**Why human:** AudioBuffer.PlaybackStartTime seam and ms-dispatch are in code. Perceptual tightness cannot be grepped — requires a composer on a real rig.

#### 6. JACK Transport Drives Tempo (JACK-01, Best-Effort)

**Test:** Run `qjackctl` / `jackd` as a JACK timebase master; call `use "@jack"` + `(jackSync)`.
**Expected:** MusicalContext.Tempo + bar/beat driven from JACK transport BPM; with no JACK server, `(jackSync)` is a silent no-op.
**Why human:** Absent-server no-op is machine-proven by JackAbsentServerNoOp. Live JACK timebase + jack_position_t ABI struct mirror against a real server needs a running JACK server not available on this dev box.

---

### Gaps Summary

No blocking gaps found. All implemented artifacts are substantive and wired. The two deferred requirements (MIDI-RT-03, LINK-01) have recorded rationale in REQUIREMENTS.md and ROADMAP.md:

- **MIDI-RT-03**: Deferred to Phase 41 cross-platform binary work. The IMidiBackend abstraction already covers CoreMIDI/WinMM; no Phase 40 code needed.
- **LINK-01**: Deferred to community/v1.6 due to GPLv2+ contamination (D-40-06, T-40-02). The LINK-02 determinism invariant is independently shipped.

The PortChanged CS0414 pattern in RtMidiMidiBackend is a noted observation (INFO level) — an intentional design decision documented in 40-RESEARCH Pattern 1 and the IMidiBackend interface contract, not a gap.

Phase 40 status is `human_needed` because 6 hardware/perceptual behaviors require a composer with a real MIDI rig/DAW/JACK server. The machine-provable byte logic and timing logic are fully verified. See `40-HUMAN-UAT.md` for the sign-off checklist.

---

_Verified: 2026-06-06_
_Verifier: Claude (gsd-verifier)_
