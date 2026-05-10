# Voices and Tracks

Below the `Song` / `section` abstraction, Flow exposes a lower-level multi-track timeline built around `Voice` and `Track` values. Use this layer when you want precise control over beat placement, per-voice gain and pan, and polyphonic mixing.

Most voice/track functions live in `@audio` (some convenience helpers are in `@composition`).

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

## Creating Tracks

```flow
use "@audio"

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
use "@audio"

Buffer rendered = (renderTrack t 8.0)    Note: render 8 beats worth of audio
```

The renderer sums all voices at their beat positions, applies per-voice and per-track gain/pan, and returns a stereo buffer.

## BPM and Beat Conversion

Beat placement depends on the global BPM. Set it before building a track timeline:

```flow
use "@audio"

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

## Polyphonic Voice Allocation

When rendering dense passages (many simultaneous notes), Flow allocates voices from a fixed pool. If the pool is exhausted, oldest voices are stolen. Adjust the pool size with `setMaxVoices`:

```flow
use "@std"
use "@audio"

(setMaxVoices 32)    Note: default

Note: large chord: 8 simultaneous notes
tempo 120 {
    key Cmajor {
        Sequence dense = | [C3 E3 G3 B3 C4 E4 G4 B4]w |
        Voice[] voices = (renderSequenceToVoices dense "piano" 44100 120.0)
    }
}

Note: very constrained pool (testing voice stealing)
(setMaxVoices 4)
```

## Multi-Track Example

Build a two-track piece by hand:

```flow
use "@std"
use "@audio"

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

            (exportWav mixed "two_track.wav")
        }
    }
}
```

## When to Use Voice/Track vs Song/Section

| You want... | Use |
|-------------|-----|
| To arrange named parts, repeats, instruments | `section` / `Song` / `renderSong` |
| To place individual buffers at arbitrary beat offsets | `Voice` / `Track` |
| To mix pre-rendered audio assets (e.g. samples, TTS, synthesized WAVs) | `Voice` / `Track` |
| To hand-tune per-note pan/gain | `Voice` on a `Track` |

## See Also

- [Song Structure](Song-Structure.md) - Higher-level arrangement
- [Audio and Synthesis](Audio-and-Synthesis.md) - Buffer creation, synthesizers
- [Effects](Effects.md) - Panning and gain at the buffer level
