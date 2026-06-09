# Phase 40: Studio Sync - Research

**Researched:** 2026-06-06
**Domain:** Real-time MIDI I/O + transport sync (MIDI clock 24 PPQN, Ableton Link, JACK) for an interpreted music language on .NET 10 / Linux-primary
**Confidence:** HIGH for codebase integration + library licenses; MEDIUM for the clock-timing mechanism (requires reaching RtMidi.Core's internal raw-byte layer — see Open Q1); LOW→DEFER for Ableton Link (GPLv2+ contamination, see §Link)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-40-01:** Ship **both** surfaces, high-level first. `(midiOut song "port")` / `(midiOut seq "port")` is the primary path. Low-level event builtins — `(midiNoteOn dev ch pitch vel)`, `(midiNoteOff dev ch pitch)`, `(midiCC dev ch ctrl val)`, `(midiSysex dev data)` — are the escape hatch for live / `@improv` / generative. Port discovery via `(midiPorts)`; open via `(openMidiOutput "port") → MidiDevice`. Mirrors `audioDevices`/`setAudioDevice` + the playback surface.
- **D-40-02:** `(midiOut song "port")` reuses the **Phase 28 `writeMidi` GM prefix-match routing VERBATIM** (piano*→0, brass*/horn*→56, sax*→65, flute*→73, string*→48, organ*→19, bell*→14, drum*→ch 9). An **explicit per-sequence override** for multitimbral hardware (prefer a **named-arg**; exact shape is planner discretion).
- **D-40-03:** Sync via **opt-in builtins/toggles, NOT musical-context blocks**: `(clockMaster device)`/`(clockSlave "port")`, `(linkEnable)`/`(linkDisable)`, `(jackSync)`. Stateful session modes (master ⊕ slave switch only at bar boundary; slave drives `MusicalContext.Tempo`). Return **reference-identity handles mirroring `OscHandle`** (D-38-16) for stop / mode-switch.
- **D-40-04:** Opt-in modules at fine granularity: `use "@midi"` (MIDI out + clock; RtMidi.Core dep), **separate** `use "@link"` and `use "@jack"` so license-gated (`libabl_link`) + Linux-only (JackSharp) deps never force-load. Mirror `@osc`/`@sfz`. Web `use` → charitable advisory; RtMidi.Core stays in the Web strip-list + AssemblyReferenceScanTests forbidden list.
- **D-40-05:** Must-ship spine = `IMidiBackend` + Linux ALSA-seq MIDI out (MIDI-RT-01/02/04) + clock master/slave (CLOCK-01/02). Link (LINK-01/02) and JACK (JACK-01) are **best-effort** — ship only if clean, else defer to community/v1.6.
- **D-40-06:** Link license posture = **CONSERVATIVE**. Brief license check only; if ANY MIT-contamination ambiguity → DEFER LINK-01/02 to community/v1.6. Default to not-shipping over risking Flow's MIT license.
- **D-40-07:** Dual verification — automated CI via virtual MIDI (snd-virmidi / RtMidi virtual ports / loopback), charitable-skip when absent (mirror Phase 39 `mscore` gate); PLUS a documented hardware/DAW HUMAN-UAT checklist.

### Carried Forward — Locked Upstream (do NOT re-decide)
- Library stack: RtMidi.Core 1.0.53 (MIDI), JackSharp 0.4.0 (JACK), `libabl_link` (Link). DryWetMidi 8.0.3 = offline file I/O only.
- `IMidiBackend` C# surface: `ListPorts`/`OpenOutput`/`SendNoteOn`/`SendNoteOff`/`SendControlChange`/`SendSysex`/`Close` + `PortChanged` callback (MIDI-RT-01).
- Clock mechanics: 24 PPQN; tempo changes apply at next bar boundary; slave 8-pulse settle on master tempo change; master⊕slave switch only at bar boundary (CLOCK-01/02).
- Latency: MIDI events emit at `audioBuffer.PlaybackStartTime + bufferOffset` (NOT queue time); sysex on separate best-effort queue (MIDI-RT-04).
- Determinism: Link/clock tempo are render-time inputs for `play`/`loop`/`preview` ONLY — NEVER applied to `writeWav`/`writeMidi`. Peer-disappear → latch last-seen tempo (LINK-02).
- Charitable failures: hot-plug / missing-server → log + retry + quiet-drop, NEVER throw.
- MIDI-in dispatch idiom: pattern matching (Phase 35), e.g. `(match msg | (noteOn n v) => ... | (cc n v) => ...)` (D-v1.5-10).
- Sub-order: IMidiBackend (Linux) → clock master+slave → Link (license-gated) → JACK.
- MIDI-RT-03 (CoreMIDI/WinMM) DEFERRED to Phase 41.

### Claude's Discretion
- Exact override syntax for per-sequence channel/program mapping (prefer named-arg).
- Handle type names + `GetSpecificity()` values for MidiDevice / clock / Link / JACK handles (model on OscHandle, specificity 151).
- Internal scheduling mechanism realizing the `PlaybackStartTime + bufferOffset` alignment.
- Whether clock master needs an explicit `(clockStop)` vs handle-based stop.
- Virtual-MIDI test mechanism (`snd-virmidi` vs RtMidi virtual ports vs loopback).

### Deferred Ideas (OUT OF SCOPE)
- WebMIDI (`WebMidiBackend`) — deferred to v1.6 by Phase 47.
- CoreMIDI (macOS) + WinMM (Windows) via RtMidi.Core — Phase 41 (MIDI-RT-03).
- General MIDI-input builtin surface beyond clock slave (e.g. `(midiListen port handler)`).
- Ableton Link if license review defers it (community PR welcome).
- JACK on macOS/Windows.
- MIDI 2.0 / MPE.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MIDI-RT-01 | `IMidiBackend` parallel to `IAudioBackend` — `ListPorts`/`OpenOutput`/`SendNoteOn`/`SendNoteOff`/`SendControlChange`/`SendSysex`/`Close` + `PortChanged` callback | RtMidi.Core `MidiDeviceManager.Default.OutputDevices` (`IEnumerable<IMidiOutputDeviceInfo>`) + `IMidiOutputDevice.Send(in NoteOnMessage/...)`; `PortChanged` is NOT a native event → poll-based wrapper (§Hot-plug). `IAudioBackend` shape (read in full) is the direct template. |
| MIDI-RT-02 | Linux ALSA-seq backend via RtMidi.Core 1.0.53 — primary platform | RtMidi.Core is a P/Invoke wrapper over a fork of `librtmidi`; ALSA support exists in the underlying RtMidi C++ but **`librtmidi.so` is NOT shipped in the NuGet package** (Open Q2 — load-bearing native-dep gap). `libasound.so.2` present on the dev box. |
| MIDI-RT-03 | macOS CoreMIDI + Windows WinMM | DEFERRED to Phase 41 per CONTEXT. RtMidi.Core advertises only Windows+macOS prebuilt; the same `IMidiBackend` abstraction covers them later. No Phase 40 work. |
| MIDI-RT-04 | Audio-MIDI latency alignment — emit at `audioBuffer.PlaybackStartTime + bufferOffset`; sysex on best-effort queue; hot-plug never throws | `AudioBuffer` (AudioCore.cs) has **NO `PlaybackStartTime` field today — must be introduced**. Integration seam = `PlaybackFunctions.PlaySamples` / the blocking `IAudioBackend.Play` call (§Integration Seam). |
| CLOCK-01 | MIDI clock master — 24 PPQN + start/stop/continue from `MusicalContext.Tempo`; tempo at next bar boundary | RtMidi.Core has **NO TimingClock/Start/Stop/Continue message type** (verified — see §Clock). Requires raw-byte 0xF8/0xFA/0xFB/0xFC send via the internal `IRtMidiOutputDevice.SendMessage(byte[])` (Open Q1). Timing via dedicated thread + `Stopwatch` spin-wait. |
| CLOCK-02 | MIDI clock slave — receive 24 PPQN, drive `MusicalContext.Tempo`; 8-pulse settle; mode switch at bar boundary | RtMidi.Core input has **no real-time/clock event** in the public API; raw bytes arrive only on the internal `IRtMidiInputDevice.Message` event (`EventHandler<byte[]>`). Same internal-access problem as CLOCK-01 (Open Q1). |
| LINK-01 | Ableton Link peer-equal tempo via libabl_link P/Invoke | **license-gated GPLv2+/commercial → RECOMMEND DEFER** (§Link). P/Invoking GPLv2+ from MIT is a derivative-work contamination hazard. |
| LINK-02 | Link tempo render-time only; never to writeWav/writeMidi; peer-disappear latch; CI byte-identical writeWav | The byte-identical-`writeWav` determinism gate is **writable and valuable even if Link itself is deferred** (§Validation — write it as a "no Link path touches offline render" invariant). |
| JACK-01 | JACK transport sync (Linux opt-in) via JackSharp 0.4.0 — transport drives tempo/bar/beat | JackSharp 0.4.0 is **MIT** (clean), targets net35 (net10 compat via net4x shim — Open Q3); `libjack.so.0` present on dev box. Best-effort per D-40-05. |
</phase_requirements>

## Summary

Phase 40 turns Flow into a live-studio participant. The codebase is unusually well-prepared: the `IAudioBackend` interface, `AudioPlaybackManager.DetectBackend()`/`IsAvailable()` probe, `PlaybackFunctions.Register` surface, the `OscHandle` reference-identity Value + `Cts.Token.Register(dispose)` listener lifecycle, the `InstrumentRouting.ResolveGmProgram` GM-routing table, and the Phase 47 Web-strip discipline (csproj `<ItemGroup Condition="'$(FlowTarget)' == 'Web'">` + `AssemblyReferenceScanTests` — which **already names `RtMidi.Core` as a forbidden prefix per D-47-14**) are all directly reusable templates. The must-ship spine (real-time MIDI out + clock) maps cleanly onto these.

Three findings reshape the plan and are the highest-value output of this research:

1. **RtMidi.Core's public managed API cannot send or receive MIDI clock.** `IMidiOutputDevice` exposes only typed channel/system-common `Send(in XxxMessage)` overloads — there is **no `TimingClockMessage`, `StartMessage`, `StopMessage`, or `ContinueMessage`**, and `IMidiInputDevice` has **no TimingClock/real-time event**. The raw-byte paths DO exist (`IRtMidiOutputDevice.SendMessage(byte[])` and `IRtMidiInputDevice.Message : EventHandler<byte[]>`) but both interfaces are `internal` to the RtMidi.Core assembly. CLOCK-01/02 therefore needs a decision (Open Q1): reach the internal layer via reflection, vendor/patch a thin source shim, or bind the RtMidi C-API directly for the four clock bytes. This is the single biggest planning risk and must be resolved at plan-start, not mid-execution.

2. **`AudioBuffer` has no `PlaybackStartTime` and the playback path is fully blocking/synchronous.** `PlaybackFunctions.PlaySamples` calls `backend.Play(samples, …, ct)` which blocks until the buffer drains. MIDI-RT-04's "emit at `PlaybackStartTime + bufferOffset`" requires introducing a scheduling concept that the current synchronous path does not have. The realistic v1.5 seam is to capture `PlaybackStartTime = Stopwatch wall-clock at the instant `backend.Play` begins` and dispatch scheduled MIDI events from a sibling timer thread keyed off that origin — NOT sample-accurate inside the audio callback (Flow has no pull-model audio callback; PulseAudio Simple is a blocking push API). Document this honestly: "buffer-relative ms accuracy, not sample-accurate."

3. **Ableton Link is GPLv2+/commercial dual-licensed.** P/Invoking it from MIT-licensed `flow-lang.dll` is a textbook GPL derivative-work contamination hazard. Per the conservative D-40-06 posture, **defer LINK-01/02 to community/v1.6**. RtMidi.Core (MIT) and JackSharp (MIT) are both clean.

**Primary recommendation:** Build the MIDI-out spine + clock on RtMidi.Core, resolving the internal-raw-byte access question (Open Q1) in the FIRST plan via a small spike. Mirror `IAudioBackend`/`OscHandle`/`InstrumentRouting` verbatim. Introduce `AudioBuffer.PlaybackStartTime` as a nullable wall-clock origin and schedule MIDI off a dedicated thread (best-effort ms alignment). **Defer Ableton Link** (GPL); keep JACK best-effort (MIT, clean, but net35-target compat is unverified — Open Q3). Gate everything behind `use "@midi"` / `use "@jack"` (drop `@link` if deferred) with the Phase 47 Web-strip discipline applied to every new file + PackageReference.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Real-time MIDI byte transmission | Native backend (`IMidiBackend` impl over RtMidi.Core) | — | Mirrors `IAudioBackend`; P/Invoke + native lib lives at the backend tier, never in the builtin surface |
| Port enumeration / device open | Backend manager (`MidiPlaybackManager`, sibling to `AudioPlaybackManager`) | Builtin surface (`midiPorts`/`openMidiOutput`) | Manager owns lifecycle + probe (`IsMidiAvailable`); builtins are thin Flow-facing wrappers |
| GM channel/program routing for `(midiOut song …)` | Shared routing table (`InstrumentRouting.ResolveGmProgram`) | Builtin surface | D-40-02 reuse verbatim — single source of truth across MIDI file export + real-time + MusicXML/LilyPond |
| Clock 24 PPQN timing | Dedicated clock thread (new `MidiClock` class) reading `MusicalContext.Tempo` | Backend (raw-byte send) | Timing must be off the audio + interpreter threads; reads tempo, writes bytes |
| Clock slave tempo capture | Background input listener (mirrors OSC `StartListener` Task + Cts) | `MusicalContext.Tempo` writer | Slave drives tempo; lifecycle = OscHandle pattern |
| Audio-MIDI alignment | Playback path (`PlaybackFunctions` / `AudioPlaybackManager`) | `AudioBuffer.PlaybackStartTime` | Alignment origin is set where audio playback begins |
| Sync handles (MidiDevice/clock/jack) | Reference-identity Value types (`MidiDeviceType` etc., model on `OscHandleType`) | — | D-40-03 lifecycle consistency with OSC |
| Opt-in module gating | `ModuleLoader` (`@midi`/`@jack` resolution + Web advisory) + `FlowEngine` register site (`#if !FLOW_WEB`) + ExecutionContext gate bool | — | Mirrors `@osc`/`@sfz` exactly |
| Web-target exclusion | `flow-lang.csproj` `<ItemGroup Condition="'$(FlowTarget)' == 'Web'">` + `AssemblyReferenceScanTests` | — | RtMidi.Core/JackSharp must never reach the WASM closure |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| RtMidi.Core | 1.0.53 | Real-time MIDI I/O (the load-bearing replacement for DryWetMidi, which has no Linux device I/O) | `[ASSUMED]` (CONTEXT-locked) Confirmed MIT-licensed, .NET Standard 2.0, P/Invoke wrapper over a fork of `librtmidi` (thestk/rtmidi). `[CITED: github.com/micdah/RtMidi.Core/blob/master/LICENSE]` |
| Melanchall.DryWetMidi | 8.0.3 | Offline MIDI **file** read/write ONLY (`writeMidi`) | Already referenced; stays. No real-time device path upstream. `[VERIFIED: codebase grep — flow-lang.csproj:103]` |

### Supporting (best-effort per D-40-05)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| JackSharp | 0.4.0 | JACK transport position/tempo read (JACK-01) | Linux opt-in `use "@jack"`. MIT. **net35 target — net10 compat via net4x shim is plausible but UNVERIFIED (Open Q3).** `libjack.so.0` present on dev box. `[CITED: github.com/residuum/JackSharp/blob/master/LICENSE]` |
| ~~libabl_link / abl_link~~ | — | Ableton Link (LINK-01/02) | **DO NOT SHIP.** GPLv2+/commercial dual-license → MIT contamination. DEFER to community/v1.6 (§Link). `[CITED: github.com/Ableton/link/blob/master/GNU-GPL-v2.0.md]` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| RtMidi.Core | `managed-midi` / `Commons.Music.Midi` (atsushieno) | Pure-managed ALSA/CoreMIDI/WinMM, no native `.so` to ship. Author's own CLAUDE.md note lists managed-midi as "past project — DryWetMidi wins"; but for **real-time device I/O** managed-midi avoids the `librtmidi.so`-not-shipped gap. **CONTEXT locks RtMidi.Core — do not re-litigate**, but the planner should note managed-midi as the v1.6 fallback if Open Q2 (native lib) proves intractable. `[ASSUMED]` |
| RtMidi.Core clock via internal reflection | Direct RtMidi C-API P/Invoke for 4 clock bytes | A ~30-line `[DllImport("rtmidi")]` for `rtmidi_out_send_message` sends 0xF8/0xFA/0xFB/0xFC without touching RtMidi.Core internals — but adds a second native-lib coupling. Weigh in Open Q1. `[ASSUMED]` |
| JackSharp | Hand-rolled `libjack` P/Invoke | JACK transport (`jack_transport_query`) is a small C surface; hand-rolling avoids the net35 compat risk. Only if JackSharp's net4x shim fails on net10 (Open Q3). `[ASSUMED]` |

**Installation (Desktop target only — never reaches Web closure):**
```bash
# Added to flow-lang.csproj under <ItemGroup Condition="'$(FlowTarget)' != 'Web'">
dotnet add flow-lang package RtMidi.Core --version 1.0.53
# Best-effort JACK (only if shipped):
dotnet add flow-lang package JackSharp --version 0.4.0
# Linux runtime/CI native prerequisite (NOT in the NuGet package — see Open Q2):
sudo apt-get install librtmidi-dev   # provides librtmidi.so + pulls libasound2
```

**Version verification:** RtMidi.Core 1.0.53 confirmed live on NuGet `[CITED: nuget.org/packages/RtMidi.Core]`. JackSharp 0.4.0 confirmed live, published 2018-03-09, zero dependencies `[CITED: nuget.org/packages/JackSharp/0.4.0]`. `dotnet 10.0.108` present on dev box. Registry slopcheck call could not reach the network this session — both packages are therefore tagged `[ASSUMED]` for slopcheck purposes (see Audit), but both are CONTEXT-locked, long-lived, and license-verified from their GitHub source.

## Package Legitimacy Audit

> slopcheck 0.6.1 is installed but its registry endpoint was unreachable this session. Per protocol, packages are tagged `[ASSUMED]` and the planner should gate each install behind a `checkpoint:human-verify` task. Mitigating context: both are CONTEXT-locked, both license files were read directly from GitHub source this session, both are years-old established packages.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| RtMidi.Core 1.0.53 | NuGet | ~since 2017 | established | github.com/micdah/RtMidi.Core | unreachable→`[ASSUMED]` | Approved (license MIT verified from source) — planner adds verify checkpoint |
| JackSharp 0.4.0 | NuGet | since 2018-03 | low (niche) | github.com/residuum/JackSharp (archived 2023) | unreachable→`[ASSUMED]` | Conditional (best-effort; verify net10 compat Open Q3) — planner adds verify checkpoint |
| Melanchall.DryWetMidi 8.0.3 | NuGet | established | very high | github.com/melanchall/drywetmidi | n/a (already shipping) | Already approved (in tree) |
| libabl_link (Ableton Link) | C/C++ source, not NuGet | — | — | github.com/Ableton/link | n/a | **REMOVED — GPLv2+ contamination (D-40-06)** |

**Packages removed due to license verdict:** Ableton Link (`libabl_link`) — GPLv2+/commercial, incompatible with Flow's MIT distribution.
**Packages flagged as suspicious:** none on legitimacy grounds. JackSharp flagged on **technical** grounds (net35 target, archived repo) — best-effort only.

## Architecture Patterns

### System Architecture Diagram

```
                         Flow source: use "@midi"
                                  │
                                  ▼
                    ModuleLoader.LoadModule("@midi")
                  (Web target → charitable advisory + Error)
                                  │  Desktop
                                  ▼
        ┌──────────────── @midi builtin surface ────────────────┐
        │ midiPorts / openMidiOutput / midiOut / midiNoteOn /    │
        │ midiCC / midiSysex / clockMaster / clockSlave         │
        └───────────────────────┬───────────────────────────────┘
                                 │
        high-level path          │           low-level path
   (midiOut song "port")         │     (midiNoteOn dev ch p v)
        │                        │                  │
        ▼                        ▼                  ▼
 InstrumentRouting        MidiPlaybackManager   MidiDevice handle
 .ResolveGmProgram   ┌──►(probe + lifecycle)◄──┐ (ref-identity Value,
 (D-40-02 reuse)     │    IsMidiAvailable()     │  models OscHandle)
        │            │           │              │
        ▼            │           ▼              │
  per-seq events ────┘   IMidiBackend impl ─────┘
        │                (RtMidiMidiBackend)
        │                       │ Send(in NoteOnMessage) [typed]
        │                       │ + raw 0xF8/FA/FB/FC [internal layer — Open Q1]
        ▼                       ▼
  ┌──────────────┐      ┌────────────────┐      ┌──────────────────┐
  │ MidiClock    │─────►│ RtMidi.Core    │◄─────│ Clock slave      │
  │ thread       │ 24   │ → librtmidi.so │ bytes│ listener (Task   │
  │ reads        │ PPQN │ → ALSA-seq     │      │ +Cts, OSC model) │
  │ MusicalCtx   │      │ (libasound.so) │      │ writes Tempo     │
  │ .Tempo       │      └────────┬───────┘      └──────────────────┘
  └──────────────┘               │
                                 ▼
                          Hardware synth / DAW

  Audio-MIDI alignment (MIDI-RT-04):
    PlaybackFunctions.PlaySamples → sets AudioBuffer.PlaybackStartTime
    = Stopwatch origin at backend.Play() start → MIDI scheduler dispatches
    events at (PlaybackStartTime + bufferOffset). Best-effort ms, NOT
    sample-accurate (no pull-model audio callback exists).

  Offline render (writeWav/writeMidi): NEVER touched by clock/Link tempo
    → byte-identical determinism preserved (LINK-02 invariant).
```

### Recommended Project Structure
```
flow-lang/
├── Audio/
│   ├── IMidiBackend.cs              # NEW — interface, parallels IAudioBackend
│   ├── NullMidiBackend.cs          # NEW — silent fallback, models NullAudioBackend pattern
│   ├── RtMidiMidiBackend.cs        # NEW — RtMidi.Core impl (#if !FLOW_WEB)
│   ├── MidiPlaybackManager.cs      # NEW — probe + lifecycle, sibling to AudioPlaybackManager
│   ├── MidiClock.cs                # NEW — 24 PPQN master thread + slave capture
│   └── (AudioCore.cs)              # EDIT — add AudioBuffer.PlaybackStartTime
├── StandardLibrary/Midi/           # NEW dir
│   ├── MidiFunctions.cs            # midiPorts/openMidiOutput/midiOut/midiNoteOn/...
│   ├── MidiClockFunctions.cs       # clockMaster/clockSlave (+ handles)
│   ├── MidiDeviceData.cs           # runtime state behind MidiDevice handle (models OscHandleData)
│   └── ClockHandleData.cs          # runtime state behind clock handle
│   └── (JackFunctions.cs)          # best-effort, #if !FLOW_WEB, use "@jack"
├── TypeSystem/SpecialTypes/
│   ├── MidiDeviceType.cs           # NEW — ref-identity Value, models OscHandleType (specificity 152+)
│   ├── ClockHandleType.cs          # NEW
│   └── (JackHandleType.cs)         # best-effort
├── midi.flow                       # NEW stdlib module (models osc.flow)
├── (jack.flow)                     # best-effort
└── flow-lang.csproj                # EDIT — add deps (non-Web ItemGroup) + Web strip-list entries
```

### Pattern 1: `IMidiBackend` parallel to `IAudioBackend`
**What:** A backend interface + manager + probe, mirroring the audio path exactly.
**When to use:** All real-time MIDI I/O (MIDI-RT-01/02).
**Example:**
```csharp
// Source: model on flow-lang/Audio/IAudioBackend.cs (read in full)
namespace FlowLang.Audio;

public interface IMidiBackend : IDisposable
{
    string Name { get; }
    bool IsInitialized { get; }
    IReadOnlyList<string> ListPorts();              // ← RtMidi MidiDeviceManager.Default.OutputDevices
    IMidiOutputHandle? OpenOutput(string port);     // null = charitable failure (NEVER throw)
    // PortChanged is poll-based — RtMidi.Core has no native hot-plug event (GitHub issue #18 unanswered)
    event Action<IReadOnlyList<string>>? PortChanged;
}

public interface IMidiOutputHandle : IDisposable
{
    void SendNoteOn(int channel, int pitch, int velocity);
    void SendNoteOff(int channel, int pitch);
    void SendControlChange(int channel, int controller, int value);
    void SendProgramChange(int channel, int program);  // needed for D-40-02 GM routing
    void SendSysex(byte[] data);                        // best-effort queue (MIDI-RT-04)
    void SendRaw(byte[] bytes);                         // clock 0xF8/FA/FB/FC — Open Q1
    void Close();
}
```

### Pattern 2: RtMidi.Core message send (typed channel-voice messages)
**What:** Map Flow note/CC/program events onto RtMidi.Core's `Send(in XxxMessage)` overloads.
**When to use:** Note/CC/program (everything EXCEPT clock).
**Example:**
```csharp
// Source: github.com/micdah/RtMidi.Core IMidiOutputDevice.cs (verified — all 13 Send overloads)
using RtMidi.Core;                 // MidiDeviceManager
using RtMidi.Core.Messages;        // NoteOnMessage, NoteOffMessage, ControlChangeMessage, ...
using RtMidi.Core.Enums;           // Channel, Key

var info = MidiDeviceManager.Default.OutputDevices
    .FirstOrDefault(d => d.Name.Contains(port, StringComparison.OrdinalIgnoreCase));
IMidiOutputDevice dev = info.CreateDevice();   // IMidiOutputDeviceInfo.CreateDevice()
dev.Open();
dev.Send(new NoteOnMessage(Channel.Channel1, Key.Key60, velocity: 100));
dev.Send(new ControlChangeMessage(Channel.Channel1, control: 7, value: 100));
dev.Send(new ProgramChangeMessage(Channel.Channel1, program: 56)); // GM brass for D-40-02
dev.Send(new SysExMessage(new byte[] { 0x7E, 0x7F, 0x09, 0x01 }));
dev.Close();
// NOTE: RtMidi.Core enums are 1-based Channel.Channel1..Channel16; Flow's GM
// routing uses 0-based channel (ch 0 / ch 9). Map carefully (off-by-one hazard).
```

### Pattern 3: Reference-identity sync handle (model on OscHandle)
**What:** Every sync entity (MidiDevice, clock, jack) returns a ref-identity Value with a `Cts`-driven dispose lifecycle.
**When to use:** All of D-40-03's stateful handles.
**Example:**
```csharp
// Source: flow-lang/TypeSystem/SpecialTypes/OscHandleType.cs (specificity 151) +
//         flow-lang/StandardLibrary/Network/OscHandleData.cs +
//         flow-lang/Runtime/Value.cs:123 Value.OscHandle(...)
public sealed class MidiDeviceType : FlowType
{
    private MidiDeviceType() { }
    public static MidiDeviceType Instance { get; } = new();
    public override string Name => "MidiDevice";
    public override int GetSpecificity() => 152;   // above OscHandle=151
    public override bool IsCompatibleWith(FlowType t) => t is MidiDeviceType;
    public override bool CanConvertTo(FlowType t) => t is MidiDeviceType;
}
// ClockHandleData mirrors OscHandleData: holds the clock thread + CancellationTokenSource;
// (clockMaster device) → returns ClockHandle; stop via handle (planner: explicit (clockStop)
// vs handle-dispose is Claude's discretion D-40-03).
```

### Pattern 4: Clock slave listener (model on OSC `StartListener`)
**What:** A background `Task` + `CancellationTokenSource` with `Cts.Token.Register(dispose)` to break the blocking receive.
**When to use:** CLOCK-02 (slave receiving 24 PPQN).
**Example:**
```csharp
// Source: flow-lang/StandardLibrary/Network/OscFunctions.cs:353-469 StartListener
//   - cts.Token.Register(() => { try { receiver.Dispose(); } catch { } });  // break blocked receive
//   - while (!cts.IsCancellationRequested) { packet = receiver.Receive(); ... }
//   - charitable: bind failure → WarnOnce + return sentinel "dead" handle, never throw
// Slave specifics: count 0xF8 pulses; every 24 = 1 quarter; derive BPM from
// inter-pulse Stopwatch deltas; apply 8-pulse settle (average last 8) before
// writing MusicalContext.Tempo. Mode (master⊕slave) switch only at bar boundary.
```

### Pattern 5: GM routing reuse (D-40-02 verbatim)
**What:** `(midiOut song "port")` resolves each sequence name to (GM program, channel) via the SAME table as `writeMidi`.
**Example:**
```csharp
// Source: flow-lang/StandardLibrary/Notation/InstrumentRouting.cs:45 (verbatim reuse)
var (gmProgram, channel) = InstrumentRouting.ResolveGmProgram(seq.Name);
dev.Send(new ProgramChangeMessage(ToRtChannel(channel), gmProgram));
// then stream each note as NoteOn/NoteOff at its scheduled offset.
// Per-sequence override (D-40-02, named-arg) layers ON TOP of this default,
// e.g. (midiOut song "port" overrides=(dict "lead" 0 "kick" 9)) — exact shape planner discretion.
```

### Anti-Patterns to Avoid
- **Sending clock via a typed message API.** RtMidi.Core has none; do NOT invent a fake `TimingClockMessage` — it won't compile against the library. Use raw bytes (Open Q1).
- **Blocking the interpreter thread for clock timing.** The clock MUST run on its own thread; spin-wait/`Stopwatch` for sub-ms, not `Thread.Sleep` (≥1ms jitter, OS-scheduler dependent).
- **Throwing on missing device/port/server.** Violates the charitable rule (long `live` sessions can't die). Return null/sentinel + `RenderingDiagnostics.WarnOnce`, mirror OSC bind-failure path.
- **Letting clock/Link tempo touch `writeWav`/`writeMidi`.** Breaks two-run determinism (LINK-02). Sync tempo is a `play`/`loop`/`preview`-only input.
- **Referencing RtMidi.Core/JackSharp from a non-`#if !FLOW_WEB` file.** `AssemblyReferenceScanTests` fires RED (D-47-14 already lists `RtMidi.Core`).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| MIDI byte framing / device open / port enum | Custom ALSA-seq P/Invoke for note/CC/program | RtMidi.Core typed `Send(in XxxMessage)` | CONTEXT-locked; battle-tested cross-platform; handles ALSA-seq client/port plumbing |
| GM program/channel routing | A second routing table | `InstrumentRouting.ResolveGmProgram` (verbatim) | D-40-02 mandates byte-identical behavior to `writeMidi`; single source of truth |
| Listener thread lifecycle / dispose-on-cancel | New threading scaffold | OSC `StartListener`/`StopListener` pattern (`Cts.Token.Register(dispose)`) | Proven to break blocking `Receive()`; charitable failure already handled |
| One-shot advisories | `Console.Error.WriteLine` ad hoc | `RenderingDiagnostics.WarnOnce(key, msg)` | Dedup + consistent `[midi]`/`[clock]` prefix convention |
| MIDI **file** write | Anything | DryWetMidi 8.0.3 (unchanged) | Stays for offline; not a Phase 40 concern |
| Reference-identity handle plumbing | New Value machinery | `Value.OscHandle` → add `Value.MidiDevice` factory in the same shape | Phase 32/33/36/38 precedent |

**Key insight:** The clock is the ONLY genuinely-new mechanism (24 PPQN timing + raw bytes). Everything else is assembling existing, proven Flow patterns. Concentrate planning/spike budget on Open Q1 + Q2.

## Runtime State Inventory

> Phase 40 is greenfield feature work, not a rename/refactor. This section is included only to record the one stateful concern: **session-mode state** (master⊕slave) is held in the new clock/handle objects, NOT persisted anywhere. No stored data, no OS-registered state, no env vars, no build artifacts carry MIDI state across runs.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — clock/device handles are in-memory ref-identity Values, discarded at process exit | None |
| Live service config | None in git. **Runtime: ALSA-seq client/port registration** is created/destroyed by RtMidi.Core per device open — ephemeral, auto-cleaned on `Close()`/Dispose | Ensure `Dispose` closes devices (mirror `AudioPlaybackManager.Dispose`) |
| OS-registered state | None persistent. (ALSA-seq ports are ephemeral per §above) | None |
| Secrets/env vars | None | None |
| Build artifacts | **`librtmidi.so` is a NATIVE runtime dependency NOT shipped in the NuGet** (Open Q2) — must be present on the target machine | Document in install.sh / CI provisioning; charitable fallback when absent |

## Common Pitfalls

### Pitfall 1: RtMidi.Core has no clock message type
**What goes wrong:** Plan assumes `dev.Send(new TimingClockMessage())` — it doesn't exist; build fails or someone fakes a no-op.
**Why it happens:** RtMidi.Core's public API is channel-voice + system-common only (verified: 13 `Send` overloads, none real-time).
**How to avoid:** Resolve Open Q1 at plan-start. Raw bytes 0xF8 (clock) / 0xFA (start) / 0xFB (continue) / 0xFC (stop) via the internal `IRtMidiOutputDevice.SendMessage(byte[])` (reflection or vendored shim) OR a direct RtMidi C-API P/Invoke.
**Warning signs:** A clock plan task that references a typed clock message; a CLOCK-01 verification that only checks "no exception thrown."

### Pitfall 2: `librtmidi.so` not present on Linux
**What goes wrong:** `MidiDeviceManager.Default` throws `DllNotFoundException` at first use; or silently finds zero ports.
**Why it happens:** RtMidi.Core ships prebuilt natives for Windows+macOS only; Linux requires a system `librtmidi.so` (this dev box has `libasound.so.2` + `libportmidi.so.2` but **NOT** `librtmidi.so`).
**How to avoid:** Probe like `PulseAudioSimpleBackend.IsAvailable()` (try a cheap call, catch `DllNotFoundException` → return false → `NullMidiBackend`). Document `apt install librtmidi-dev` in install/CI. CI virtual-MIDI gate charitable-skips when absent.
**Warning signs:** Tests pass on the author's box but enumerate zero ports in CI.

### Pitfall 3: Channel off-by-one (RtMidi 1-based vs Flow 0-based)
**What goes wrong:** Drums route to the wrong channel; GM ch9 percussion lands on ch10.
**Why it happens:** RtMidi.Core's `Channel` enum is `Channel1`..`Channel16` (1-based); `InstrumentRouting` returns 0-based (0, 9).
**How to avoid:** A single `ToRtChannel(int zeroBased)` helper; unit-test the drum→ch9 mapping explicitly.
**Warning signs:** Hardware drum machine silent or playing melodic on the percussion bus.

### Pitfall 4: Clock timing jitter from `Thread.Sleep`
**What goes wrong:** At 120 BPM, 24 PPQN = a pulse every ~20.8ms; `Thread.Sleep` granularity (~1-15ms on Linux) smears the tempo audibly.
**Why it happens:** Naive sleep-loop timing.
**How to avoid:** Dedicated thread, `Stopwatch`-based deadline scheduling with a short spin-wait for the final sub-ms (the Phase 38 `flow watch` 2Hz-heartbeat-off-the-audio-thread precedent + RESEARCH §E established this pattern). Apply tempo changes only at bar boundaries (re-read `MusicalContext.Tempo` at each downbeat).
**Warning signs:** Slaved drum machine drifts; CI rate assertion (24 pulses/quarter ±tolerance) flaps.

### Pitfall 5: `AudioBuffer.PlaybackStartTime` doesn't exist + path is blocking
**What goes wrong:** MIDI-RT-04 plan assumes a schedulable timeline; the actual `PlaySamples` blocks until drain.
**Why it happens:** PulseAudio Simple is a push/blocking API — there's no audio callback to hang sample-accurate MIDI off.
**How to avoid:** Add nullable `AudioBuffer.PlaybackStartTime` (a `long` Stopwatch tick or `DateTime`); set it the instant `backend.Play` begins; run MIDI dispatch on a sibling thread keyed off that origin. Be HONEST in docs + verification: "buffer-relative ms alignment, not sample-accurate." This satisfies MIDI-RT-04's intent (no queue-time emission) within the architecture's limits.
**Warning signs:** A verification claiming "sample-accurate" — unprovable on this audio path.

### Pitfall 6: Web build leaks RtMidi.Core / JackSharp
**What goes wrong:** WASM build pulls a native MIDI dep; `AssemblyReferenceScanTests` fires RED (or worse, ships).
**Why it happens:** A new file references RtMidi.Core without `#if !FLOW_WEB`, or the PackageReference is in the wrong ItemGroup.
**How to avoid:** PackageReferences under `<ItemGroup Condition="'$(FlowTarget)' != 'Web'">`; every new backend/builtin file `Compile Remove`'d in the Web ItemGroup; FlowEngine register call wrapped `#if !FLOW_WEB`; ModuleLoader `IsStrippedOnWeb` extended to `@midi`/`@jack`; add `JackSharp` to `ForbiddenTypeRefPrefixes` (RtMidi.Core already there).
**Warning signs:** `dotnet build -p:FlowTarget=Web` fails or `AssemblyReferenceScanTests` RED.

### Pitfall 7: Internal-access hack breaks on RtMidi.Core upgrade
**What goes wrong:** Reflection into `IRtMidiOutputDevice.SendMessage` breaks if the library renames internals.
**Why it happens:** Reaching `internal` members is upgrade-fragile.
**How to avoid:** If reflection is chosen (Open Q1), pin RtMidi.Core to exactly 1.0.53 and add a smoke test that fails loudly if the internal member signature changes. Prefer the vendored-shim or direct-C-API path if upgrade resilience matters.
**Warning signs:** Clock silently stops working after a routine dependency bump.

## Code Examples

### Web-strip wiring (csproj)
```xml
<!-- Source: flow-lang/flow-lang.csproj — add to EXISTING groups -->
<ItemGroup Condition="'$(FlowTarget)' != 'Web'">
  <PackageReference Include="Rug.Osc" Version="1.2.5" />     <!-- existing -->
  <PackageReference Include="RtMidi.Core" Version="1.0.53" /> <!-- NEW -->
  <!-- <PackageReference Include="JackSharp" Version="0.4.0" /> NEW if shipped -->
</ItemGroup>
<ItemGroup Condition="'$(FlowTarget)' == 'Web'">
  <!-- existing strips: OscFunctions.cs, OscHandleData.cs, PulseAudio*, Sfz/** ... -->
  <Compile Remove="Audio\RtMidiMidiBackend.cs" />
  <Compile Remove="Audio\MidiClock.cs" />
  <Compile Remove="StandardLibrary\Midi\**\*.cs" />
  <Compile Remove="TypeSystem\SpecialTypes\MidiDeviceType.cs" />  <!-- and Clock/Jack handle types -->
  <None Remove="midi.flow" />
  <!-- <None Remove="jack.flow" /> if shipped -->
</ItemGroup>
```

### ModuleLoader Web advisory extension
```csharp
// Source: flow-lang/Runtime/ModuleLoader.cs:56-62 IsStrippedOnWeb
private static bool IsStrippedOnWeb(string requestedPath) =>
    requestedPath == "@sfz" || requestedPath == "@osc"
    || requestedPath == "@midi" || requestedPath == "@jack";  // NEW
// The existing FlowEngine.IsWebTarget gate at LoadModule:86 then emits the
// charitable "[target] module '@midi' unavailable on Web target — line N.
// Build with FlowTarget=Desktop to enable." advisory + returns Error. No new code.
```

### FlowEngine register site (mirror the OSC guard exactly)
```csharp
// Source: flow-lang/Core/FlowEngine.cs:251-255 (OscFunctions.Register pattern)
#if !FLOW_WEB
    FlowLang.StandardLibrary.Midi.MidiFunctions.Register(internalRegistry, _context);
    FlowLang.StandardLibrary.Midi.MidiClockFunctions.Register(internalRegistry, _context);
    // FlowLang.StandardLibrary.Midi.JackFunctions.Register(...) if shipped
#endif
// + add ExecutionContext.MidiEnabled gate bool (model on OscEnabled at
//   ExecutionContext.cs:437 + the snapshot/restore at :1164/:1259) flipped by
//   a trailing (__enableMidiModule) marker in midi.flow.
```

### midi.flow module skeleton (model on osc.flow)
```
Note: MIDI — real-time output + clock, opt-in via `use "@midi"`
module midi
use "@std"
internal proc __enableMidiModule ()
internal proc midiPorts ()
internal proc openMidiOutput (String: port)
internal proc midiOut (Song: song, String: port)
internal proc midiNoteOn (MidiDevice: dev, Int: ch, Int: pitch, Int: vel)
internal proc midiNoteOff (MidiDevice: dev, Int: ch, Int: pitch)
internal proc midiCC (MidiDevice: dev, Int: ch, Int: ctrl, Int: val)
internal proc midiSysex (MidiDevice: dev, Buffer: data)
internal proc clockMaster (MidiDevice: device)
internal proc clockSlave (String: port)
(__enableMidiModule)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| DryWetMidi for all MIDI | DryWetMidi offline-file-only + RtMidi.Core real-time devices | This phase | DryWetMidi has no Linux device I/O upstream (verified); RtMidi.Core is the load-bearing real-time layer |
| No transport sync | MIDI clock + (deferred) Link + (best-effort) JACK | This phase | Flow becomes a studio citizen |
| RtMidi.Core "Win+Mac only" docs | Underlying RtMidi C++ supports ALSA; needs system `librtmidi.so` | longstanding | Linux works IF the native lib is installed — not auto-shipped |

**Deprecated/outdated:**
- `managed-midi` (atsushieno) — explicitly archived "past project" upstream; pure-managed but not chosen. Noted as v1.6 fallback if RtMidi native-lib gap proves intractable.
- Ableton Link integration in v1.5 — deferred (GPL); revisit only via community PR or a clean-room re-licensed binding.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | RtMidi.Core's internal `IRtMidiOutputDevice.SendMessage(byte[])` is reachable via reflection at 1.0.53 | §Clock / Open Q1 | Clock can't send → must vendor-shim or C-API P/Invoke; affects plan task count |
| A2 | `librtmidi.so` can be provisioned on CI + target Linux (`apt install librtmidi-dev`) | §Pitfall 2 / Open Q2 | If unavailable, MIDI-RT-02 only works on machines with the lib; CI gate charitable-skips |
| A3 | JackSharp 0.4.0 (net35) loads under net10 via the net4x compat shim | §Stack / Open Q3 | JACK-01 needs hand-rolled libjack P/Invoke instead; best-effort, deferrable |
| A4 | Best-effort ms-level MIDI alignment satisfies MIDI-RT-04's intent | §Pitfall 5 | If "sample-accurate" is strictly required, no path exists on the blocking PulseAudio Simple API — re-scope |
| A5 | RtMidi.Core enums are 1-based `Channel.Channel1..16`, `Key.Key0..127` | §Pattern 2 | Off-by-one in GM routing; caught by an explicit drum→ch9 test |
| A6 | RtMidi.Core 1.0.53 + JackSharp 0.4.0 pass slopcheck (registry unreachable this session) | §Audit | Both CONTEXT-locked + license-verified from source; planner adds verify checkpoint |
| A7 | Hot-plug = poll `MidiDeviceManager.Default.OutputDevices` (no native event) | §Pattern 1 | `PortChanged` must be a Flow-side polling wrapper, not an RtMidi event subscription |

## Open Questions

1. **How to send/receive MIDI clock bytes through RtMidi.Core (CLOCK-01/02)?**
   - What we know: typed `Send` overloads have no real-time message; raw `SendMessage(byte[])` + `Message : EventHandler<byte[]>` exist but are `internal` to RtMidi.Core.
   - What's unclear: cleanest access path — (a) reflection on the internal members, (b) a vendored ~50-line source shim exposing them, (c) a direct `[DllImport("rtmidi")]` for `rtmidi_out_send_message`/input callback.
   - Recommendation: **First plan ships a 1-task spike** to confirm (a) works at 1.0.53 (fastest, no extra native coupling) with a guard test (Pitfall 7); fall back to (c) if reflection is brittle. Resolve BEFORE writing clock tasks.

2. **Native `librtmidi.so` provisioning on Linux + CI (MIDI-RT-02).**
   - What we know: NuGet ships Win/Mac natives only; dev box lacks `librtmidi.so` but has `libasound.so.2`.
   - What's unclear: whether CI runners can `apt install librtmidi-dev`, and whether the `flow` self-contained binary (Phase 30) should bundle it.
   - Recommendation: `IsMidiAvailable()` probe (catch `DllNotFoundException`) → `NullMidiBackend`; document the apt prerequisite; CI virtual-MIDI gate charitable-skips when the lib (or `snd-virmidi`) is absent. Phase 41 cross-platform binaries revisit bundling.

3. **JackSharp net35 → net10 compatibility (JACK-01, best-effort).**
   - What we know: JackSharp 0.4.0 targets net35, zero deps, MIT, archived repo.
   - What's unclear: whether the net4x compat shim loads cleanly on net10 and whether `jack_transport_query` is exposed.
   - Recommendation: A 1-task compat spike; if it fails, hand-roll a minimal `libjack` P/Invoke for `jack_transport_query` (transport state + BPM only). JACK is best-effort (D-40-05) — defer with Link if neither path is clean.

4. **Per-sequence channel/program override shape (D-40-02, Claude's discretion).**
   - What we know: prefer a named-arg (D-36-11 universal named args are available).
   - What's unclear: dict-of-name→channel vs name→(channel,program) tuple.
   - Recommendation: planner picks; a `Dict<String, Int>` (name→channel) is the minimal ergonomic form; program can still derive from the name via `InstrumentRouting`.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `libasound.so.2` (ALSA) | RtMidi ALSA-seq backend | ✓ | system | — |
| `librtmidi.so` | RtMidi.Core P/Invoke (MIDI-RT-02) | ✗ | — | `apt install librtmidi-dev`; probe→`NullMidiBackend` if absent |
| `snd-virmidi` kernel module | Virtual-MIDI CI (D-40-07) | ✓ (`.ko` present, not loaded) | kernel 7.0.0-22 | `sudo modprobe snd-virmidi`; CI gate charitable-skips if unloadable |
| `aconnect` / `amidi` (ALSA-seq CLI) | Virtual-MIDI test harness | ✓ | system | — |
| `libjack.so.0` | JackSharp (JACK-01) | ✓ | system | — (JACK best-effort anyway) |
| `libabl_link` (Ableton Link) | LINK-01/02 | ✗ | — | **DEFER (GPL) — no fallback needed** |
| `dotnet` SDK | build | ✓ | 10.0.108 | — |

**Missing dependencies with no fallback:** none blocking the must-ship spine (MIDI works once `librtmidi.so` is installed; clock + slave are pure-managed timing + raw bytes).
**Missing dependencies with fallback:** `librtmidi.so` (apt-installable; probe→Null backend); `snd-virmidi` (modprobe; CI skips). Link binary deliberately absent (deferred).

## Validation Architecture

> nyquist_validation is enabled (config.json `workflow.nyquist_validation: true`).

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (C# integration/unit tests under `flow-lang.Tests/`) + `.flow` script smokes under `examples/`/`tests/` |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj`; per-target gating via `FlowTargetFactAttribute` (`flow-lang.Tests/Helpers/FlowTargetFactAttribute.cs`) |
| Quick run command | `dotnet test flow-lang.Tests --filter FullyQualifiedName~Phase40` |
| Full suite command | `dotnet test flow-lang.Tests` (Desktop) ; `dotnet build flow-lang -p:FlowTarget=Web` for the strip invariant |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MIDI-RT-01 | `IMidiBackend` enumerates ports; charitable null on absent device | unit | `dotnet test --filter MidiBackendEnumeratesPorts` | ❌ Wave 0 |
| MIDI-RT-02 | Notes/CC/program reach a virtual ALSA-seq port with correct bytes | integration (virtual MIDI) | `dotnet test --filter VirtualMidiNoteBytes` | ❌ Wave 0 (charitable-skip when snd-virmidi/librtmidi absent) |
| MIDI-RT-04 | Sysex on best-effort queue; hot-plug failure logs+continues, never throws | unit | `dotnet test --filter MidiHotPlugNeverThrows` | ❌ Wave 0 |
| CLOCK-01 | Clock master emits **24 pulses per quarter** at the active tempo (±tolerance) | integration (loopback/virtual) | `dotnet test --filter ClockMaster24PpqnRate` | ❌ Wave 0 |
| CLOCK-02 | Slave derives BPM from incoming 0xF8 stream + 8-pulse settle → writes `MusicalContext.Tempo` | unit (inject byte stream via test seam) | `dotnet test --filter ClockSlaveDrivesTempo` | ❌ Wave 0 |
| MIDI-RT-03 | (deferred Phase 41) | — | — | n/a |
| LINK-02 | `writeWav` byte-identical regardless of any sync state (no clock/Link path touches offline render) | determinism | `dotnet test --filter OfflineRenderIgnoresSync` | ❌ Wave 0 (writable even with Link deferred) |
| JACK-01 | (best-effort) transport query maps to tempo; absent server → no effect on non-JACK workflows | integration | `dotnet test --filter JackAbsentServerNoOp` | ❌ Wave 0 (charitable-skip) |
| Web-strip | RtMidi.Core/JackSharp absent from Web `flow-lang.dll` | invariant | extend `AssemblyReferenceScanTests.ForbiddenTypeRefPrefixes` (add `JackSharp`; `RtMidi.Core` already present) | ✅ exists (extend) |

### Sampling Rate
- **Per task commit:** `dotnet test --filter Phase40` (fast subset) + `dotnet build flow-lang -p:FlowTarget=Web` (strip check)
- **Per wave merge:** `dotnet test flow-lang.Tests` (full Desktop suite — preserve all prior phases green)
- **Phase gate:** Full suite green + Web build green + `40-VERIFICATION.md` + the `40-HUMAN-UAT.md` checklist authored before `/gsd:verify-work`

### Virtual-MIDI CI strategy (D-40-07)
- **Mechanism:** RtMidi virtual output port (RtMidi.Core can create a virtual ALSA-seq port) + an `aconnect`/`amidi`-driven capture, OR `snd-virmidi` loopback. **Simplest, most portable:** an **in-process loopback test seam** — a `CaptureMidiBackend` (mirrors OSC's `HandlerInvokeOverride` / `PulseAudioCaptureBackend` `CaptureOverride` test seams) that records sent byte arrays so byte/rate assertions need NO real ALSA. Use snd-virmidi only for the end-to-end HUMAN-UAT-adjacent gate.
- **Charitable-skip:** mirror `MusicXmlRoundTripTests.CharitableSkipWhenMscoreAbsent` — probe for `librtmidi.so`/`snd-virmidi`; if absent, `WarnOnce("midi-virtual-absent", …)` and PASS.
- **Rate assertion (CLOCK-01):** capture timestamped 0xF8 bytes for N quarters; assert pulse count == 24·N and inter-pulse Stopwatch mean ≈ (60/BPM/24) within tolerance.

### Wave 0 Gaps
- [ ] `flow-lang.Tests/Integration/Phase40/MidiBackendTests.cs` — MIDI-RT-01/04 (enumerate, charitable null, hot-plug never-throws)
- [ ] `flow-lang.Tests/Integration/Phase40/VirtualMidiTests.cs` — MIDI-RT-02 byte assertions via `CaptureMidiBackend` seam
- [ ] `flow-lang.Tests/Integration/Phase40/ClockMasterTests.cs` — CLOCK-01 24-PPQN rate
- [ ] `flow-lang.Tests/Integration/Phase40/ClockSlaveTests.cs` — CLOCK-02 byte-stream injection + 8-pulse settle
- [ ] `flow-lang.Tests/Integration/Phase40/OfflineRenderDeterminismTests.cs` — LINK-02 invariant (writable even if Link deferred)
- [ ] `CaptureMidiBackend` test seam (in-process loopback, models OSC `HandlerInvokeOverride`)
- [ ] Extend `AssemblyReferenceScanTests.ForbiddenTypeRefPrefixes` with `JackSharp`
- [ ] `40-HUMAN-UAT.md` — real hardware synth note-on + DAW clock sync rows (model Phase 48/49 HUMAN-UAT)

## Security Domain

> `security_enforcement` absent in config.json = enabled. Phase 40 is a local-IO / native-interop phase, not a network-auth surface; the relevant categories are input validation at the native boundary and the GPL-license supply-chain risk.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — (no auth surface) |
| V3 Session Management | no | — |
| V4 Access Control | no | — |
| V5 Input Validation | yes | Clamp/validate Flow→MIDI args at the builtin boundary: channel ∈ 0..15, pitch/vel/CC ∈ 0..127, sysex length-bounded. Follow Flow's charitable clamp+advisory (Phase 44 input-perimeter precedent) rather than throwing |
| V6 Cryptography | no | — |
| V12/Supply chain (license) | yes | **GPL contamination gate** — Ableton Link GPLv2+ must NOT link into MIT `flow-lang.dll` (D-40-06). `AssemblyReferenceScanTests` is the enforcement seam |

### Known Threat Patterns for {.NET native MIDI interop}
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malformed sysex / oversized byte array crashing native lib | Denial of Service | Length-cap + validate sysex bytes at the builtin boundary before handing to RtMidi (mirror OSC bundle depth cap DoS guard) |
| Out-of-range channel/pitch/velocity reaching native send | Tampering | Clamp at builtin (V5); never pass unvalidated Int straight to `Channel`/`Key` enums |
| GPL binary linked into MIT distribution | License compromise / legal | DEFER Link; `AssemblyReferenceScanTests` forbidden-prefix gate |
| Native `DllNotFoundException` killing a live session | Denial of Service | Probe + `NullMidiBackend` + WarnOnce (charitable, never throw) |

## Sources

### Primary (HIGH confidence)
- `flow-lang/Audio/IAudioBackend.cs`, `AudioPlaybackManager.cs`, `StandardLibrary/Audio/PlaybackFunctions.cs`, `StandardLibrary/Audio/AudioCore.cs` (AudioBuffer) — read in full; the integration templates
- `flow-lang/StandardLibrary/Network/OscFunctions.cs` + `OscHandleData.cs` + `TypeSystem/SpecialTypes/OscHandleType.cs` — listener lifecycle + ref-identity handle template
- `flow-lang/StandardLibrary/Notation/InstrumentRouting.cs:45` — GM routing table (D-40-02 verbatim reuse)
- `flow-lang/Runtime/MusicalContext.cs:43` (`Tempo`), `ModuleLoader.cs:56-93` (Web advisory), `Core/FlowEngine.cs:251-255` (OSC register guard), `ExecutionContext.cs:437` (OscEnabled gate)
- `flow-lang.Tests/Integration/Phase47/AssemblyReferenceScanTests.cs` — already lists `RtMidi.Core` forbidden (D-47-14)
- `flow-lang.Tests/Integration/Phase39/MusicXmlRoundTripTests.cs:87` — charitable-skip CI gate model
- `flow-lang/flow-lang.csproj:107-141` — Web-strip ItemGroup pattern
- RtMidi.Core source (verified API): `IMidiOutputDevice.cs` (13 typed Send overloads, no clock), `IMidiInputDevice.cs` (13 events, no real-time/clock), `IMidiDevice.cs` (Open/Close/IsOpen/Name, no event), `Unmanaged/Devices/IRtMidiOutputDevice.cs` (`internal SendMessage(byte[])`), `IRtMidiInputDevice.cs` (`internal event EventHandler<byte[]> Message`), `MidiDeviceManager.cs` (`.Default`, `OutputDevices`/`InputDevices`), `LICENSE` (MIT ×2)
- Environment probe (this dev box): `libasound.so.2`+`libjack.so.0` present, `librtmidi.so` absent, `snd-virmidi.ko` present, `aconnect`/`amidi` present, dotnet 10.0.108

### Secondary (MEDIUM confidence)
- `[CITED: nuget.org/packages/RtMidi.Core]` 1.0.53 live; `[CITED: nuget.org/packages/JackSharp/0.4.0]` MIT, net35, zero deps, 2018-03-09
- `[CITED: github.com/Ableton/link/blob/master/GNU-GPL-v2.0.md]` — Link is GPLv2+/commercial dual-license
- `[CITED: github.com/micdah/RtMidi.Core/issues/18]` — hot-plug device event requested, no built-in support (→ poll)
- RtMidi C++ clock example (raw 0xF8) `[CITED: github.com/thestk/rtmidi tests/midiclock.cpp]`

### Tertiary (LOW confidence)
- JackSharp `jack_transport_query` exposure — not verified from source (Open Q3); README sparse
- RtMidi.Core reflection-into-internal viability at 1.0.53 — inference, not run (Open Q1)

## Metadata

**Confidence breakdown:**
- Codebase integration (backends, handles, GM routing, Web-strip, module gating): HIGH — every template read in full
- Library licenses (RtMidi.Core MIT, JackSharp MIT, Link GPL): HIGH — read from source LICENSE files
- RtMidi.Core API for note/CC/program send: HIGH — verified from interface source
- Clock send/receive mechanism: MEDIUM — raw-byte paths confirmed but `internal`; access strategy is an Open Q1 spike
- Native `librtmidi.so` provisioning: MEDIUM — confirmed absent on dev box, apt-installable
- JACK net10 compat + transport API: LOW — best-effort, deferrable
- Ableton Link: resolved to DEFER (HIGH confidence in the recommendation, per D-40-06)

**Research date:** 2026-06-06
**Valid until:** 2026-07-06 (stable — libraries are pinned/old; only RtMidi.Core internal-layer behavior could shift on an unplanned upgrade, which Open Q1's guard test pins)
