# Phase 40: Studio Sync - Context

**Gathered:** 2026-06-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Flow stops being only an offline renderer + standalone player and becomes a participant in a live studio rig. This phase delivers:

- A new **`IMidiBackend`** abstraction parallel to `IAudioBackend` for **real-time MIDI output** to hardware synths and DAW tracks (Linux ALSA-seq primary via RtMidi.Core 1.0.53).
- **MIDI clock** master + slave (24 PPQN) for tempo sync with drum machines / hardware sequencers.
- **Ableton Link** (license-gated) for peer-equal cross-application LAN tempo sync.
- **JACK transport** (Linux opt-in) for pro-audio composers.

DryWetMidi 8.0.3 stays for **offline** MIDI file I/O only — it has no Linux device I/O upstream, so RtMidi.Core is the load-bearing real-time replacement. macOS CoreMIDI + Windows WinMM backends (MIDI-RT-03) are deliberately deferred to Phase 41 cross-platform binary work.

**Out of scope (own future phase / deferred):** WebMIDI, CoreMIDI/WinMM backends, a general MIDI-input builtin surface beyond clock slave, MIDI 2.0 / MPE.

</domain>

<decisions>
## Implementation Decisions

### Composer-Facing MIDI Surface
- **D-40-01:** Ship **both** surfaces, high-level first. `(midiOut song "port")` / `(midiOut seq "port")` is the primary, common-case path. Low-level event builtins — `(midiNoteOn dev ch pitch vel)`, `(midiNoteOff dev ch pitch)`, `(midiCC dev ch ctrl val)`, `(midiSysex dev data)` — are the escape hatch for live / `@improv` / generative note-by-note control. Port discovery via `(midiPorts)`; open via `(openMidiOutput "port") → MidiDevice`. This mirrors the existing `audioDevices`/`setAudioDevice` + playback surface and satisfies "easy cases fast, flexible cases flexible" (see `[[feedback_ergonomics_priority]]`).
- **D-40-02:** `(midiOut song "port")` reuses the **Phase 28 `writeMidi` GM prefix-match routing VERBATIM** (piano*→prog 0, brass*/horn*→56, sax*→65, flute*→73, string*→48, organ*→19, bell*→14, drum*→ch 9 percussion) so a hardware port sounds **identical to the exported `.mid` file** — no surprise. An **explicit per-sequence override** is available for multitimbral hardware (e.g. drum machine on ch 10, lead synth on ch 1). Prefer a **named-arg** form (Phase 36 D-36-11 universal named args) over a new builtin; exact override shape is planner discretion.

### Sync Activation Surface
- **D-40-03:** Sync is enabled via **opt-in builtins / toggles, NOT musical-context blocks**: `(clockMaster device)` / `(clockSlave "port")`, `(linkEnable)` / `(linkDisable)`, `(jackSync)`. These are **stateful session modes** (master ⊕ slave switchable only at a bar boundary; slave *drives* `MusicalContext.Tempo`) — not body-scoped, so they don't fit the tempo/key/swing push-pop block family. They return **reference-identity handles mirroring `OscHandle`** (Phase 38 D-38-16) for stop / mode-switch lifecycle. Consistent with OSC's `oscListen`/`oscStop` opt-in surface and the roadmap's literal `(jackSync)`.
- **D-40-04:** Real-time MIDI/sync is gated behind **opt-in modules at fine granularity**: `use "@midi"` enables MIDI out + clock (RtMidi.Core dep); **separate** `use "@link"` and `use "@jack"` so the **license-gated** (`libabl_link`) and **Linux-only** (JackSharp) native deps are **never force-loaded** when unused. Mirrors `@osc`/`@sfz`. On the Web target, `use` of any of these → the charitable ModuleLoader advisory already wired in Phase 47 (D-47-09); RtMidi.Core stays in the Web strip-list and the `AssemblyReferenceScanTests` forbidden-prefix list (D-47-14, already present).

### Scope & Priority
- **D-40-05:** **Must-ship spine** = `IMidiBackend` + Linux ALSA-seq real-time MIDI out (MIDI-RT-01/02/04) + MIDI clock master/slave (CLOCK-01/02). **Link (LINK-01/02) and JACK (JACK-01) are best-effort** — ship only if they fit cleanly this phase; otherwise defer to community / v1.6. Pragmatic for pre-traction, single-author, Linux-primary (`[[project_pre_public_no_legacy_burden]]`).
- **D-40-06:** **Link license posture = conservative.** Research does a **brief** license check at plan-start only. If there is **any** MIT-contamination ambiguity (GPL derivative-work via P/Invoke, any bundling of the GPL binary), **defer LINK-01/02 to community/v1.6 immediately** — do not spend deep research budget. Default to not-shipping over risking Flow's MIT license. Per D-v1.5-04.

### Verification
- **D-40-07:** **Dual verification.** (a) **Automated CI** via virtual MIDI (ALSA `snd-virmidi` / RtMidi virtual ports / loopback): ports enumerate, note/CC/sysex bytes are correct, clock master emits **24 PPQN at the right rate**, slave drives tempo, and `writeWav` is **byte-identical with/without a Link peer** (the LINK-02 CI gate). **Charitable-skip** when virtual MIDI is unavailable (mirrors Phase 39's `mscore` round-trip gate). (b) A documented **hardware/DAW HUMAN-UAT checklist** (real synth note-on, DAW clock sync, Link peer) — like the Phase 49 human gates. Honest split between machine-proven and human-confirmed.

### Carried Forward — Locked Upstream (do NOT re-decide)
- **Library stack:** RtMidi.Core 1.0.53 (MIDI), JackSharp 0.4.0 (JACK), `libabl_link` (Link). DryWetMidi 8.0.3 = offline file I/O only.
- **`IMidiBackend` C# method surface:** `ListPorts`/`OpenOutput`/`SendNoteOn`/`SendNoteOff`/`SendControlChange`/`SendSysex`/`Close` + `PortChanged` callback (MIDI-RT-01).
- **Clock mechanics:** 24 PPQN; tempo changes apply at next bar boundary (no mid-bar jumps to slaves); slave 8-pulse settle on master tempo change; master⊕slave switch only at bar boundary (CLOCK-01/02).
- **Latency:** MIDI events emit at `audioBuffer.PlaybackStartTime + bufferOffset` (NOT queue time); sysex on a separate best-effort queue (MIDI-RT-04).
- **Determinism contract:** Link/clock tempo are render-time inputs for `play`/`loop`/`preview` ONLY — NEVER applied to `writeWav`/`writeMidi`. Peer-disappear → latch last-seen tempo (LINK-02).
- **Charitable failures:** hot-plug / missing-server → log + retry + quiet-drop, **never throw** (long `live` sessions can't die). See `[[feedback_charitable_interpretation]]`.
- **MIDI-in dispatch idiom:** pattern matching (Phase 35), e.g. `(match msg | (noteOn n v) => ... | (cc n v) => ...)` (D-v1.5-10).
- **Sub-order:** IMidiBackend (Linux) → clock master+slave → Link (license-gated) → JACK.

### Claude's Discretion
- Exact override syntax for per-sequence channel/program mapping (prefer named-arg).
- Handle type names + `GetSpecificity()` values for MidiDevice / clock / Link / JACK handles (model on OscHandle).
- Internal scheduling mechanism that realizes the `PlaybackStartTime + bufferOffset` alignment.
- Whether clock master needs an explicit `(clockStop)` vs handle-based stop.
- Virtual-MIDI test mechanism choice (`snd-virmidi` vs RtMidi virtual ports vs loopback).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase requirements & decisions
- `.planning/ROADMAP.md` → "### Phase 40: Studio Sync" — goal, the 5 success criteria, dependency note, sub-order.
- `.planning/REQUIREMENTS.md` §"Real-Time MIDI (Phase 40)" / §"MIDI Clock (Phase 40)" / §"Ableton Link (Phase 40, license-gated)" / §"JACK Transport (Phase 40, Linux opt-in)" — MIDI-RT-01..04, CLOCK-01/02, LINK-01/02, JACK-01, plus D-v1.5-04 (Link license gate) + D-v1.5-10 (pattern-matching MIDI dispatch).
- `.planning/phases/47-compile-target-flavors/47-CONTEXT.md` — D-47-05/06 (`IAudioBackend` probe pattern + `NullAudioBackend` fallback), D-47-08 (central `RegisterAll()` `#if !FLOW_WEB` guard site), D-47-09 (charitable ModuleLoader advisory), D-47-14 (`AssemblyReferenceScanTests` already forbids `RtMidi.Core` in the Web build — keep MIDI deps Desktop-only).

### Existing code to mirror / integrate with
- `flow-lang/Audio/IAudioBackend.cs` — the interface `IMidiBackend` parallels; `NullAudioBackend` is the silent-fallback model for a `NullMidiBackend`.
- `flow-lang/Audio/AudioPlaybackManager.cs` — backend lifecycle + auto-detect (`PickBackend`); model for a MIDI backend manager.
- `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs` — `play`/`loop`/`stream`/`preview`/`stop`/`audioDevices`/`setAudioDevice`/`isAudioAvailable` registration + the surface `midiOut`/`midiPorts`/`openMidiOutput` should echo; also the integration point for `PlaybackStartTime + bufferOffset` MIDI emission.
- `flow-lang/Runtime/MusicalContext.cs` — `Tempo` (`double?`) is what clock master reads and slave drives.
- `flow-lang/StandardLibrary/Network/OscFunctions.cs` + `OscHandleData.cs` + `osc.flow` — opt-in `use "@osc"` module + `OscHandle` reference-identity Value (D-38-16) + `Cts.Token.Register` dispose pattern; the template for `@midi`/`@link`/`@jack` + their handles.
- `flow-lang/Audio/PulseAudioCaptureBackend.cs` — Phase 38 native-backend sibling-class pattern (RtMidi.Core is a managed NuGet wrapper rather than raw P/Invoke, but the backend-class shape carries over).
- Phase 28 `writeMidi` GM prefix-match routing (in MIDI export / `SongRenderer` path) — reuse verbatim for D-40-02.

### Project conventions
- `CLAUDE.md` — Phase 28 "Multi-track MIDI export" GM routing table; "Audio backend abstraction" design decision; charitable-interpretation rule; C# conventions; Web-target guard locations.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`IAudioBackend` + `NullAudioBackend` + `AudioPlaybackManager.PickBackend()`** — directly model `IMidiBackend`, `NullMidiBackend`, and the MIDI backend manager / probe.
- **`PlaybackFunctions` registration block** — the `play`/`loop`/`preview`/`audioDevices`/`setAudioDevice` family is the surface shape for `midiOut`/`midiPorts`/`openMidiOutput`.
- **`OscHandle` (reference-identity Value, specificity 151, D-38-16)** + its `Cts.Token.Register(dispose)` lifecycle — model for `MidiDevice` / clock / Link / JACK handles.
- **Phase 28 GM prefix-match routing** — reused verbatim for real-time channel/program mapping (D-40-02).
- **`WarnOnce` / `RenderingDiagnostics` one-shot advisory** — for hot-plug / quiet-drop / unmapped messaging.

### Established Patterns
- **Opt-in module gating:** OSC/SFZ register conditionally and load via `use "@x"`; Phase 47 D-47-08 guards the call in `BuiltInFunctions.cs::RegisterAll` with `#if !FLOW_WEB`; ModuleLoader emits a charitable advisory on Web (D-47-09). `@midi`/`@link`/`@jack` follow this exactly.
- **Web strip discipline:** `AssemblyReferenceScanTests` (Mono.Cecil, D-47-14) already forbids `RtMidi.Core` in the Web `flow-lang.dll`; the csproj `<ItemGroup Condition="'$(FlowTarget)' == 'Web'">` strip-list must exclude the new MIDI backend files + the RtMidi.Core PackageReference must not reach the Web closure.
- **Charitable degradation:** missing port/server → advisory + Void, never throw (Phase 47 D-47-09, `[[feedback_charitable_interpretation]]`).
- **Determinism gate via test:** Phase 39's charitable-skip CI gate (`mscore`) is the model for the virtual-MIDI CI tests + the byte-identical-`writeWav`-with-Link gate.

### Integration Points
- New `flow-lang/Audio/IMidiBackend.cs` + an ALSA-seq backend (RtMidi.Core) + a MIDI backend manager (new, or extend `AudioPlaybackManager`).
- New stdlib builtins (e.g. `StandardLibrary/Midi/`) + `midi.flow` / `link.flow` / `jack.flow` modules; `FlowEngine` wires the guarded `Register*()` call at the D-47-08 site.
- MIDI emission hook in the playback path to realize `PlaybackStartTime + bufferOffset` alignment (MIDI-RT-04).
- `flow-lang.csproj` — RtMidi.Core 1.0.53 (+ JackSharp 0.4.0 if shipped) PackageReferences added to the Web strip-list; the existing `AssemblyReferenceScanTests` forbidden list already names `RtMidi.Core`.

</code_context>

<specifics>
## Specific Ideas

- The headline ergonomic: `(midiOut song "Roland JV-1080")` should "just work" and sound the same out the MIDI port as the rendered `.mid` file.
- Low-level event builtins exist specifically to serve **live coding / `@improv` / generative** use — driving notes from a running session, not just batch playback.
- Sync handles should feel like `OscHandle` (reference identity, dispose-on-cancel) so the lifecycle story is consistent across the network/studio surface.

</specifics>

<deferred>
## Deferred Ideas

- **WebMIDI** as a `WebMidiBackend` `IMidiBackend` impl for the Web target — already deferred to v1.6 by Phase 47.
- **CoreMIDI (macOS) + WinMM (Windows)** backends via RtMidi.Core — Phase 41 cross-platform binary work (MIDI-RT-03).
- **General MIDI-input builtin surface** beyond clock slave (e.g. `(midiListen port handler)` for controller input driving generative patches) — pattern-matching dispatch is ready, but the input builtin is out of Phase 40 scope; revisit if a composer asks.
- **Ableton Link** if the license review defers it (community PR welcome, not shipped from upstream in v1.5).
- **JACK on macOS / Windows** — theoretically available, not shipped/tested in v1.5.
- **MIDI 2.0 / MPE** — out of scope for this milestone.

None of the above were scope creep raised mid-discussion — they are the natural Phase-40-adjacent boundaries.

</deferred>

---

*Phase: 40-studio-sync*
*Context gathered: 2026-06-06*
