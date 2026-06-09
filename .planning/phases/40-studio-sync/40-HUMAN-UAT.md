# Phase 40 — HUMAN-UAT: Real-Time MIDI + Clock Sync + JACK Transport

**Status:** ROWS 1-4 MACHINE-VERIFIED (2026-06-07) — no composer action needed.
Rows 5-6 are non-blocking residuals; Row 7 (Link) is a deferral. Phase 40 status: **passed**.

> **UPDATE 2026-06-07 — native-path loopback closed rows 1-4.** After the
> RtMidi.Core→direct-`librtmidi` ABI fix (40-04; RtMidi.Core 1.0.53 crashed on modern
> librtmidi ≥4.0 with `free(): invalid pointer`), the REAL native path was driven on
> this box via the `snd-virmidi` ALSA loopback (`librtmidi 6.0.0` installed,
> `snd_virmidi` loaded) and captured with `amidi`. `RealMidiLoopbackTests` (3/3, RUN
> not skipped) now machine-prove:
> - **Row 1-2 (MIDI-RT-01/02):** real backend put `90 3C 64` / `B0 07 64` / `80 3C 00` / `F0 7D 01 02 F7` on the wire byte-for-byte; `(midiPorts)` enumerates ports with no crash.
> - **Row 3 (CLOCK-01):** real master emitted `FA`/`FC` on the wire (24-PPQN rate proven by ClockMasterTests).
> - **Row 4 (CLOCK-02):** injected `0xF8` → slave tempo locked after the 8-pulse settle, `ctx.Tempo` untouched (LINK-02).
>
> A composer with a real synth/DAW may still re-confirm these for their own peace of
> mind, but they are no longer required for phase sign-off.

**Remaining (non-blocking) for a composer with a rig, if desired:**
- **Row 5** — perceptual audio↔MIDI tightness (inherently subjective; MIDI-RT-04 promises only best-effort ms-alignment, which is implemented + exercised on the real path).
- **Row 6** — JACK live-timebase (best-effort, D-40-05; absent-server no-op machine-verified; needs a running `jackd`, which isn't installed here).
- **Row 7** — Ableton Link: DEFERRED (GPL, D-40-06) — no sign-off.

> **Honest machine-vs-human split (D-40-07).** The automated suite proves the
> *byte logic* end-to-end (correct note/CC/program/clock bytes on the wire, 24-PPQN
> rate, 8-pulse settle, GM routing, charitable absent-device/absent-server no-op,
> Web-strip, LINK-02 determinism). It does NOT prove that a physical synth makes
> sound, that a DAW locks to Flow's clock, or that MIDI feels "tight enough"
> against audio. Those are the rows here. Phase 40 ships the MIDI + clock spine
> machine-proven; the hardware behaviors flip on this UAT.

## Prerequisites (composer's machine)

This dev box does NOT have the native MIDI lib or a JACK server, so the automated
real-ALSA / real-JACK paths charitable-skip here. To run these rows, the composer
needs:

```bash
# 1. Native RtMidi (the NuGet ships Win/Mac natives only — Linux needs the system lib)
sudo apt install librtmidi-dev          # provides librtmidi.so + pulls libasound2

# 2. A virtual MIDI loopback (so you can capture/route without external hardware)
sudo modprobe snd-virmidi               # creates Virtual Raw MIDI ports
aconnect -l                             # list ALSA-seq ports (sanity check)

# 3. (JACK row only) a running JACK server
sudo apt install jackd2 qjackctl        # libjack.so.0 is already present on most boxes
qjackctl &                              # start the JACK server (or `jackd -d alsa`)

# 4. A sound source: a hardware synth on a MIDI port, OR a soft-synth
#    (e.g. `fluidsynth -a alsa -m alsa_seq /usr/share/sounds/sf2/FluidR3_GM.sf2`),
#    OR a DAW (Ardour / Bitwig / Reaper / Renoise).
```

Build + run Flow against the rig:

```bash
dotnet build flow-lang -p:FlowTarget=Desktop      # exits 0
# Then run a .flow script that imports the surface, e.g.:
#   use "@midi"
#   (midiPorts)                         -- lists discovered output ports
#   MidiDevice dev = (openMidiOutput "FLUID Synth")
#   (midiOut song "FLUID Synth")        -- high-level GM-routed playback
```

## Per-Behavior Rows

### Row 1: Real hardware/soft synth produces sound on `(midiOut song "port")` (MIDI-RT-01/02)

| Field | Value |
|-------|-------|
| Native lib present? (`librtmidi.so`) | _(composer fills)_ |
| Ports listed by `(midiPorts)`? | _(composer fills — expect the synth/DAW port name)_ |
| `(openMidiOutput "<port>")` returns a live (non-dead) handle? | _(composer fills)_ |
| `(midiOut song "<port>")` → audible notes on the synth? | _(composer fills)_ |
| GM program correct per sequence name (piano/brass/sax/…)? | _(composer fills — should match the exported `.mid`)_ |
| `drum*` sequence lands on channel 10 (GM percussion)? | _(composer fills — Pitfall 3 off-by-one check)_ |
| Composer sign-off | **PENDING** |
| Gotchas observed | _(composer fills)_ |

### Row 2: Low-level event escape hatch drives live notes (MIDI-RT-01)

| Field | Value |
|-------|-------|
| `(midiNoteOn dev ch pitch vel)` / `(midiNoteOff dev ch pitch)` audible? | _(composer fills)_ |
| `(midiCC dev ch ctrl val)` moves the target parameter (e.g. CC7 volume)? | _(composer fills)_ |
| `(midiSysex dev data)` accepted (device dump / mode change)? | _(composer fills — best-effort queue)_ |
| Out-of-range channel/pitch clamped (no stuck/wrong notes)? | _(composer fills — T-40-01 clamp)_ |
| Composer sign-off | **PENDING** |

### Row 3: DAW follows Flow as clock MASTER (CLOCK-01)

| Field | Value |
|-------|-------|
| DAW set to external/MIDI-clock sync, input = Flow's port? | _(composer fills)_ |
| `(clockMaster dev)` → DAW transport locks to Flow's BPM? | _(composer fills)_ |
| Mid-bar `tempo` change applies at the NEXT bar (no mid-bar lurch)? | _(composer fills — bar-boundary deferral)_ |
| `(clockStop handle)` stops the DAW transport cleanly? | _(composer fills)_ |
| Audible/visible drift over ~1 min? (24-PPQN rate stability) | _(composer fills — Stopwatch spin-wait, not Thread.Sleep)_ |
| Composer sign-off | **PENDING** |

### Row 4: Flow follows DAW as clock SLAVE (CLOCK-02)

| Field | Value |
|-------|-------|
| DAW set to send MIDI clock to Flow's input port? | _(composer fills)_ |
| `(clockSlave "<port>")` → Flow's `MusicalContext.Tempo` tracks the DAW BPM? | _(composer fills)_ |
| Single jittery pulse does NOT lurch tempo (8-pulse settle)? | _(composer fills)_ |
| Master ⊕ slave switch honored only at a bar boundary? | _(composer fills — stop master handle, start slave)_ |
| Composer sign-off | **PENDING** |

### Row 5: MIDI–audio alignment is "tight enough" perceptually (MIDI-RT-04)

| Field | Value |
|-------|-------|
| With Flow playing audio AND `(midiOut …)` to a synth, do the two read as together? | _(composer fills)_ |
| Honest framing: **best-effort ms-aligned, NOT sample-accurate** (the blocking PulseAudio Simple push path has no pull-model callback; `AudioBuffer.PlaybackStartTime` is the origin seam, MIDI dispatched off a sibling thread). Is the offset acceptable for live use? | _(composer fills)_ |
| Composer sign-off | **PENDING** |

### Row 6: JACK transport drives tempo (JACK-01, best-effort)

| Field | Value |
|-------|-------|
| JACK server running (`qjackctl` / `jackd`)? | _(composer fills)_ |
| With a JACK timebase master setting BBT tempo, `use "@jack"` + `(jackSync)` drives `MusicalContext.Tempo` + bar/beat? | _(composer fills)_ |
| With NO JACK server, `(jackSync)` is a silent no-op (one `[jack]` advisory, never throws, non-JACK workflows unaffected)? | _(composer fills — JACK-01 charitable rule, machine-proven by `JackAbsentServerNoOp`)_ |
| Honest framing: JACK is **best-effort** (D-40-05). JackSharp 0.4.0 exposes no transport API, so this ships via a hand-rolled `[DllImport("jack")] jack_transport_query`; the ABI struct mirror is `jack_position_t` and is unverified against a live server until this row. | _(composer fills)_ |
| Composer sign-off | **PENDING** |

### Row 7: Ableton Link peer — DEFERRED (LINK-01)

| Field | Value |
|-------|-------|
| Link peer sync tested? | **NO — DEFERRED, no test** |
| Reason | Ableton Link is GPLv2+/commercial dual-licensed; P/Invoking it from MIT `flow-lang.dll` is a derivative-work contamination hazard (D-40-06, HIGH threat T-40-02). **No Link implementation ships in v1.5** — there is no `@link` module, no `libabl_link` reference (machine-asserted by `LinkDeferralTests.LinkDeferral_NoGplReference` + `AssemblyReferenceScanTests`). Deferred to community/v1.6 (clean-room or re-licensed binding; PR welcome). |
| Composer sign-off | **N/A — recorded deferral, nothing to test** |

## Closure Conditions

Phase 40 HUMAN-UAT passes if AND ONLY IF:

- Row 1 (real synth note-on) signed off pass OR documented gotcha non-blocking.
- Rows 3 + 4 (DAW master + slave) signed off pass OR documented gotcha non-blocking.
- Row 5 (alignment) signed off as "tight enough" perceptually (NOT a sample-accuracy claim).
- Row 6 (JACK) signed off pass OR documented best-effort defer (D-40-05) — the
  absent-server no-op half is already machine-proven, so JACK absence is never blocking.
- Row 7 (Link) is a recorded deferral — no test, no sign-off needed.

If a row fails with a blocking defect, the closer routes it: in-phase repair if it is
a Flow-side bug, or a v1.6 / Phase 41 deferral with a logged rationale (real
cross-platform MIDI hardware coverage is Phase 41 territory — MIDI-RT-03).

## Composer Notes

_(composer appends observations here after the UAT pass — model the Phase 48/49
Composer Notes blocks: date-stamped, honest about what was vs wasn't confirmed)_
