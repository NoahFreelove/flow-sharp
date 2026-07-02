# OSC and MIDI

Flow can talk to other software and hardware in real time: send and receive OSC messages, drive external synths and DAW tracks over MIDI, act as a MIDI clock master or slave, and read JACK transport. These are three separate opt-in modules — `@osc`, `@midi`, and `@jack`.

> **Desktop-only.** All three modules are stripped on the Web target. In the browser playground, `use "@osc"` / `use "@midi"` / `use "@jack"` produce a charitable "unavailable on Web target" advisory. The realtime MIDI and JACK paths are **Linux only** today (see honest scope below).

## OSC (`use "@osc"`)

OSC (Open Sound Control) lets Flow send and receive typed messages over UDP — useful for driving visualizers, hardware, TouchOSC/Max/Pd patches, or another Flow process. Backed by Rug.Osc 1.2.5 (MIT).

### Sending

```flow
use "@std"
use "@osc"

(oscSend "127.0.0.1" 9000 "/synth/freq" 440.0)
(oscSend "127.0.0.1" 9000 "/synth/gate" true)
(oscSend "127.0.0.1" 9000 "/label" "verse" 3)
```

`oscSend` takes a fixed `host`, `port`, and `path`, then any number of payload arguments. Each argument's OSC type tag is inferred by a **smallest-tag-that-fits** rule:

| Flow value | OSC tag |
|---|---|
| `Int` | `,i` |
| `Long` | `,h` |
| `Float` | `,f` |
| `Double` | `,d` |
| `String` / `Symbol` | `,s` |
| `Bool` | `,T` / `,F` |
| `Buffer` | `,b` (blob) |

Force a wider type with an explicit cast at the call site (`(toLong 1)`, `1.5d`).

### Buffer Blobs (FLO1)

When you send a `Buffer`, Flow prepends a 12-byte header — the ASCII magic `FLO1` plus channel count and sample rate — so a stereo / 48 kHz buffer round-trips correctly. A headerless foreign blob decodes charitably as mono / 44100 Hz with a one-shot advisory.

### Receiving

Handlers are **not** run on the background receive thread. `oscListen` queues matching messages; you drain the queue on the foreground thread with `oscPump` inside a poll loop:

```flow
use "@std"
use "@osc"

OscHandle sub = (oscListen 9000 "/synth/freq" (fn Double f => (print $"freq = {f}")))

Int i = 0
while (lt i 1000) {
    Int fired = (oscPump)   Note: runs queued handlers, returns how many fired
    i = (add i 1)
}

(oscStop sub)
```

`oscStop` shuts the listener down cleanly (no spurious receive errors). A per-path rate limit of **200 Hz** (drop-newest sample-and-hold, 5 ms window) protects against message floods — excess messages on the same path are silently dropped.

### Bundles

Build a bundle from leaf messages and dispatch it atomically:

```flow
use "@osc"

OscHandle a = (oscMsg "/a" 1)
OscHandle b = (oscMsg "/b" 2.5)
OscHandle bundle = (oscBundle a b)
(oscSendBundle "127.0.0.1" 9000 bundle)
```

Bundles dispatch both directions with the timetag honored on receive. Nesting is capped at depth 8 (a DoS guard) — deeper bundles collapse with a stderr advisory.

### OSC Surface

| Function | Signature | Notes |
|---|---|---|
| `oscSend` | `(String host, Int port, String path, ...args) -> Void` | Type-tag inference on payload |
| `oscListen` | `(Int port, String path, Function handler) -> OscHandle` | Queues; does not run on bg thread |
| `oscPump` | `() -> Int` | Drains queued handlers; returns count fired |
| `oscStop` | `(OscHandle) -> Void` | Clean shutdown |
| `oscMsg` | `(String path, ...args) -> OscHandle` | Single-message packet (for bundles or direct send) |
| `oscBundle` | `(...packets) -> OscHandle` | Bundle construction |
| `oscSendBundle` | `(String host, Int port, OscHandle bundle) -> Void` | Dispatch a bundle |

## Realtime MIDI (`use "@midi"`)

`@midi` sends MIDI to hardware synths and DAW tracks. It is backed by direct librtmidi P/Invoke over ALSA-seq.

> **Honest scope.** Realtime MIDI is **Linux only** today. It requires the system library `librtmidi-dev` (`apt install librtmidi-dev`). When librtmidi is absent, the backend falls back to a silent `NullMidiBackend` — calls become no-ops, never errors, so a live session never dies. macOS (CoreMIDI) and Windows (WinMM) realtime MIDI are deferred to a later phase; the same `IMidiBackend` abstraction will cover them. Timing is **best-effort ms-aligned, not sample-accurate**. This is distinct from MIDI *file* export (`writeMidi`), which is cross-platform and covered in [Playback and Export](Playback-and-Export.md).

### High-Level Output

`midiOut` renders a `Song` or `Sequence` and streams it to a named port. It reuses the exact GM prefix-match routing from `writeMidi`, so a hardware port sounds **identical to the exported `.mid`**:

```flow
use "@std"
use "@midi"

(midiPorts)   Note: prints discovered output port names

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section lead { Sequence mel = | C4q E4q G4q C5q | }
            Song song = [lead]
            (midiOut song "My Synth Port")
        }
    }
}
```

GM routing: `piano*`→0, `brass*`/`horn*`→56, `sax*`→65, `flute*`→73, `string*`→48, `organ*`→19, `bell*`→14, `drum*`→channel 9 (percussion). Pass an `overrides=` Dict (sequence-name → channel) to remap channels for multitimbral hardware; the GM program still derives from the name. On close, `midiOut` flushes CC 123 (All Notes Off) per used channel — no stuck notes.

Channel / pitch / velocity / CC values are clamped to valid ranges with a `[midi]` advisory rather than throwing.

### Low-Level Escape Hatch

For live / generative use, open a device and send raw events yourself:

```flow
use "@midi"

MidiDevice dev = (openMidiOutput "My Synth Port")
(midiNoteOn  dev 0 60 100)   Note: channel 0, middle C, velocity 100
(midiNoteOff dev 0 60)
(midiCC      dev 0 74 64)    Note: filter cutoff
(midiSysex   dev someBuffer)
```

### MIDI Clock

Flow can be the clock master (send 24-PPQN pulses at the active tempo) or a slave (derive tempo from an incoming clock):

```flow
use "@midi"

MidiDevice dev = (openMidiOutput "My Drum Machine")
ClockHandle master = (clockMaster dev)     Note: sends 0xF8 / start / stop at active BPM
Note: ... play ...
(clockStop master)

ClockHandle slave = (clockSlave "DAW Clock Out")   Note: drives MusicalContext.Tempo
(clockStop slave)
```

Clock tempo is a `play` / `loop` / `preview`-only input — it **never** reaches `writeWav` / `writeMidi`, so offline renders stay deterministic regardless of external clock.

### MIDI Surface

| Function | Signature | Notes |
|---|---|---|
| `midiPorts` | `() -> Void` | Prints available output port names |
| `openMidiOutput` | `(String port) -> MidiDevice` | Reference-identity handle; dead handle if librtmidi absent |
| `midiOut` | `(Song\|Sequence, String port[, overrides=Dict]) -> Void` | GM-routed; flushes All-Notes-Off on close |
| `midiNoteOn` / `midiNoteOff` | `(MidiDevice, Int ch, Int pitch[, Int vel]) -> Void` | Low-level |
| `midiCC` | `(MidiDevice, Int ch, Int ctrl, Int val) -> Void` | Control change |
| `midiSysex` | `(MidiDevice, Buffer data) -> Void` | System-exclusive |
| `clockMaster` | `(MidiDevice) -> ClockHandle` | Emit MIDI clock at active tempo |
| `clockSlave` | `(String port) -> ClockHandle` | Follow incoming clock |
| `clockStop` | `(ClockHandle) -> Void` | Stop cleanly |

## JACK Transport (`use "@jack"`)

`jackSync` reads a running JACK server's transport position and BPM and drives the active `MusicalContext.Tempo` and bar/beat:

```flow
use "@std"
use "@jack"

(jackSync)   Note: reads JACK transport, updates Tempo/bar when a server is present
```

> **Honest scope.** JACK support is **best-effort** and Linux-only. It reads **transport position and tempo only** — no audio routing. It is backed by a hand-rolled `jack_transport_query` P/Invoke (JackSharp was evaluated and rejected — it exposes no transport API). With no JACK server running (or `libjack.so.0` absent), `jackSync` is a no-op: it emits a one-shot `[jack]` advisory, leaves the tempo untouched, and returns a dead handle. It never throws, so non-JACK workflows are unaffected. Out-of-range transport tempo (≤0 or >1000 BPM) is rejected, not written.

## Ableton Link

**Not shipped, and not planned.** Ableton Link is GPL-licensed; P/Invoking it from the MIT-licensed `flow-lang` would be a derivative-work contamination hazard, so no Link binding ships and none is scoped. A clean-room or re-licensed community binding would be welcome. This is a licensing decision, not a technical gap.

## See Also

- [Live Coding](Live-Coding.md) — Hot-swapping scripts with `flow watch`
- [Playback and Export](Playback-and-Export.md) — MIDI *file* export (`writeMidi`) and import
- [Imports and Modules](Imports-and-Modules.md) — `use` mechanics and the module list
- [Design Philosophy](Design-Philosophy.md) — Charitable interpretation and honest-scope shipping
