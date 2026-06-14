# Voices and Tracks

Below the `Song` / `section` abstraction, Flow exposes a lower-level multi-track timeline built around `Voice` and `Track` values. Use this layer when you want precise control over beat placement, per-voice gain and pan, and polyphonic mixing.

Most voice/track functions live in `@composition` (add `use "@composition"`). The sequence-to-voice render helpers (`renderSequenceToVoices`, `renderBarToVoices`, `renderBarAtBeat`, `renderBarAtTime`) and `setMaxVoices` live in `@audio`.

## The Model

- A **Voice** is a `Buffer` positioned at a specific beat offset, with its own gain and pan.
- A **Track** is a collection of voices with an optional track-level offset, gain, and pan.
- A **Song** rendered through `renderSong` internally produces voices and tracks for you, but you can also build them yourself.

```
Buffer → Voice (at beat offset) → Track (collection) → rendered Buffer
```

## Creating Voices

```flow
use "@std"
use "@audio"
use "@composition"

Buffer note = (createSineTone 0.5 440.0 0.5)

Note: place the buffer at beat 0
Voice v = (createVoice note 0.0)
(setVoiceGain v 0.8)
(setVoicePan v -0.3)        Note: slightly left
(setVoiceOffset v 2.0)      Note: start at beat 2 instead
```

**Signatures**

| Function | Signature | Description |
|----------|-----------|-------------|
| `createVoice` | `(Buffer, Double) -> Voice` | Voice positioned at beat offset |
| `setVoiceGain` | `(Voice, Double) -> Void` | Voice gain (0.0 - 1.0) |
| `setVoicePan` | `(Voice, Double) -> Void` | Pan (-1.0 left, 0.0 center, +1.0 right) |
| `setVoiceOffset` | `(Voice, Double) -> Void` | Beat offset on the timeline |

Per-voice pan threads through both the synth path and the SFZ sampler path. In the SFZ path, the effective per-note pan is `clamp(region.Pan + voice.Pan, -1.0, +1.0)` — voice pan adds to any per-region pan baked into the SFZ patch.

## Creating Tracks

```flow
use "@std"
use "@audio"
use "@composition"

Buffer note = (createSineTone 0.5 440.0 0.5)
Voice v = (createVoice note 0.0)

Track t = (createTrack 44100 2)     Note: sample rate, channels

(addVoice t v)
(setTrackGain t 0.9)
(setTrackPan t 0.0)
(setTrackOffset t 0.0)
```

**Signatures**

| Function | Signature | Description |
|----------|-----------|-------------|
| `createTrack` | `(Int, Int) -> Track` | Empty track (sample rate, channels) |
| `addVoice` | `(Track, Voice) -> Void` | Append voice to track |
| `setTrackOffset` | `(Track, Double) -> Void` | Track-wide beat offset |
| `setTrackGain` | `(Track, Double) -> Void` | Track gain |
| `setTrackPan` | `(Track, Double) -> Void` | Track pan |

## Rendering a Track

```flow
use "@std"
use "@audio"
use "@composition"

Buffer note = (createSineTone 0.5 440.0 0.5)
Voice v = (createVoice note 0.0)
Track t = (createTrack 44100 2)
(addVoice t v)

Buffer rendered = (renderTrack t 8.0)    Note: render 8 beats worth of audio
```

The renderer sums all voices at their beat positions, applies per-voice and per-track gain/pan, and returns a stereo buffer.

## BPM and Beat Conversion

Beat placement depends on the global BPM. Set it before building a track timeline:

```flow
use "@std"
use "@audio"
use "@composition"

(setBPM 120.0)
Double bpm = (getBPM)

Int frames = (beatsToFrames 4.0 44100)     Note: frames for 4 beats
Double beats = (framesToBeats 88200 44100) Note: beats for 88200 frames
```

## Rendering Sequences to Voices

Instead of building a `Song` and calling `renderSong`, you can render a `Sequence` directly to voices:

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        Sequence mel = | C4q D4q E4q F4q |
        Voice[] voices = (renderSequenceToVoices mel "piano" 44100 120.0)
    }
}
```

**Signatures**

| Function | Signature | Description |
|----------|-----------|-------------|
| `renderSequenceToVoices` | `(Sequence, String, Int, Double) -> Voice[]` | Render sequence with synth |
| `renderBarToVoices` | `(Bar, String, Int, Double) -> Voice[]` | Render a single bar |
| `renderBarAtBeat` | `(Bar, Double, String, Int, Double) -> Voice[]` | Render at a beat offset |
| `renderBarAtTime` | `(Bar, Double, String, Int, Double) -> Voice[]` | Render at a time offset (seconds) |

The `String` synth argument accepts any of the [built-in synth names](Audio-and-Synthesis.md#built-in-synthesizers), any registered custom wavetable, or `"sampler:NAME"` for an SFZ patch bound earlier in the script.

## Polyphonic Voice Allocation

When rendering dense passages (many simultaneous notes), Flow allocates from a fixed voice pool. Two allocation policies are supported:

- **Legacy keep-loudest-N** — preserves the loudest active voices.
- **Steal-oldest pool** (default) — when the pool overflows, the active voice with the earliest onset is truncated at the new voice's onset, with a short fade. Ties break by original input index for deterministic two-run cmp-clean behavior.

Adjust the pool ceiling at runtime with `setMaxVoices` or scope it to a section with the `voicePool N { }` musical-context block:

```flow
use "@std"
use "@audio"

(setMaxVoices 32)                Note: default

Note: large chord: 8 simultaneous notes
tempo 120 {
    key Cmajor {
        Sequence dense = | [C3 E3 G3 B3 C4 E4 G4 B4]w |
        Voice[] voices = (renderSequenceToVoices dense "piano" 44100 120.0)
    }
}

Note: very constrained pool (testing voice stealing)
(setMaxVoices 4)

Note: per-section pool override — only this section uses 16 voices
voicePool 16 {
    section busy {
        Sequence wash = | [C2 E2 G2 C3 E3 G3 C4 E4 G4 C5 E5 G5]w |
    }
}
```

Pool size is clamped to the range `[1, 256]`; default is `32`.

## Voice-Block Polyphony

For multi-voice writing within a single sequence, use `{voice ...}` blocks inside a note stream. Each voice shares the parent bar's onset and renders in parallel — the same render path drives both audio output and MIDI export, so voice blocks become per-note `<voice>N</voice>` tags or sibling `<<{ } \\ { }>>` voices in MusicXML / LilyPond:

```flow
timesig 4/4 {
    key Cmajor {
        Sequence twoVoice = | {voice C4w} {voice C5q D5q E5q F5q} |
    }
}
```

## Sustain Pedal

The `sustainPedal { }` block extends every note's audio buffer inside it by a 2-second sustain tail — pair it with the piano's `release=` knob on `renderSong` for full sustain-pedal-sim control:

```flow
sustainPedal {
    section ringing {
        Sequence chord = | [C4 E4 G4]w |
    }
}
```

## Polyrhythm

Overlay two sequences with different time signatures, aligned at LCM (or an explicit beat count):

```flow
use "@std"
use "@audio"
use "@composition"

tempo 120 {
    timesig 4/4 {
        Sequence three = | C4q E4q G4q |       Note: 3-beat pattern
        Sequence four  = | D4q D4q D4q D4q |   Note: 4-beat pattern
        Buffer poly  = (polyrhythm three four)         Note: LCM = 12 beats
        Buffer poly8 = (polyrhythm three four 8)       Note: explicit length (Int, not Double)
    }
}
```

## Multi-Track Example

Build a two-track piece by hand:

```flow
use "@std"
use "@audio"
use "@composition"

(setBPM 120.0)

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Sequence lead = | C4 E4 G4 C5 |
            Sequence bass = | C3 G3 C3 G3 |

            Voice[] leadVoices = (renderSequenceToVoices lead "piano" 44100 120.0)
            Voice[] bassVoices = (renderSequenceToVoices bass "piano" 44100 120.0)

            Track leadTrack = (createTrack 44100 2)
            (each leadVoices (fn Voice v => (addVoice leadTrack v)))
            (setTrackGain leadTrack 0.9)
            (setTrackPan leadTrack 0.2)

            Track bassTrack = (createTrack 44100 2)
            (each bassVoices (fn Voice v => (addVoice bassTrack v)))
            (setTrackGain bassTrack 0.8)
            (setTrackPan bassTrack -0.2)

            Buffer leadBuf = (renderTrack leadTrack 4.0)
            Buffer bassBuf = (renderTrack bassTrack 4.0)
            Buffer mixed = (mix leadBuf bassBuf)

            (writeWav "two_track.wav" mixed)
        }
    }
}
```

## When to Use Voice/Track vs Song/Section

| You want... | Use |
|-------------|-----|
| To arrange named parts, repeats, instruments | `section` / `Song` / `renderSong` |
| Multi-voice writing within a single sequence | `{voice ...}` blocks in a note stream |
| To place individual buffers at arbitrary beat offsets | `Voice` / `Track` |
| To mix pre-rendered audio assets (samples, TTS, synthesized WAVs) | `Voice` / `Track` |
| To hand-tune per-note pan/gain | `Voice` on a `Track` |
| To cap polyphony for an entire piece | `setMaxVoices` |
| To cap polyphony for one section | `voicePool N { }` block |

## See Also

- [Song Structure](Song-Structure.md) - Higher-level arrangement
- [Audio and Synthesis](Audio-and-Synthesis.md) - Buffer creation, synthesizers, SFZ sampler
- [Effects](Effects.md) - Panning, gain, and other per-buffer effects
- [Musical Context](Musical-Context.md) - `voicePool`, `sustainPedal`, `pan`, `gain` blocks
