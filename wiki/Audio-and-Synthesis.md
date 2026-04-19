# Audio and Synthesis

Flow provides a complete audio pipeline from buffer creation through synthesis to final output. Most audio functions require `use "@audio"`.

## Buffers

A `Buffer` is a container for audio sample data with frames, channels, and a sample rate.

### Creating Buffers

```flow
use "@std"
use "@audio"

Note: raw buffer (frames, channels, sample rate)
Buffer buf = (createBuffer 44100 2 44100)    Note: 1 second of stereo

Note: convenience tone generators (duration seconds, frequency Hz, amplitude)
Buffer sine     = (createSineTone     0.5 440.0 0.5)
Buffer saw      = (createSawTone      0.5 440.0 0.5)
Buffer square   = (createSquareTone   0.5 440.0 0.5)
Buffer triangle = (createTriangleTone 0.5 440.0 0.5)
```

### Buffer Properties

```flow
Int sr       = (getSampleRate buf)
Int frames   = (getFrames buf)
Int channels = (getChannels buf)
```

### Sample Access

```flow
Float s = (getSample buf 0 0)            Note: frame 0, channel 0
(setSample buf 0 0 0.5)
(fillBuffer buf 0.0)
```

### Buffer Manipulation

```flow
Buffer c       = (copyBuffer buf)
Buffer slice   = (sliceBuffer buf 0 22050)
Buffer joined  = (appendBuffers buf1 buf2)
Buffer scaled  = (scaleBuffer buf 0.5)
Buffer mixed   = (mix buf1 buf2)
Buffer mixed2  = (mixBuffers buf1 buf2 0.7 0.3)
Buffer fadedIn = buf -> fadeIn 0.5
Buffer fadedOt = buf -> fadeOut 0.5
```

### Loading WAV Files

```flow
Buffer loaded = (loadWav "sample.wav")
Int frames = (getFrames loaded)
```

See [Playback and Export](Playback-and-Export.md) for exporting.

## Oscillators

Generate basic waveforms by stepping an oscillator through a buffer:

```flow
use "@audio"

OscillatorState osc = (createOscillatorState 440.0 44100)
Buffer buf = (createBuffer 44100 1 44100)

(generateSine     buf osc 0.5)
(generateSaw      buf osc 0.5)
(generateSquare   buf osc 0.5)
(generateTriangle buf osc 0.5)

(resetPhase osc)
```

## Custom Oscillators (Wavetables)

Register your own wavetable-based oscillator and use it by name in `renderSong` and friends:

### From an Array

```flow
use "@std"
use "@audio"

Int tableSize = 2048
Double tableSizeD = (intToDouble tableSize)
Float[] sawTable = []
Int i = 0
while (lt i tableSize) {
    Double id = (intToDouble i)
    Double sample = (id / tableSizeD) * 2.0 - 1.0
    sawTable = (append sawTable sample)
    i = (add i 1)
}

(oscillator "customsaw" sawTable)
```

### From a Lambda

```flow
Function triGen = fn Int sz => (map (range 0 sz)
    (fn Int idx => ((idx -> intToDouble) / (sz -> intToDouble) * 4.0 - 2.0)))

(oscillator "customtri" triGen)
```

### Custom Table Size

```flow
(oscillator "customhighres" triGen 8192)
```

### Using a Custom Oscillator

Once registered, the name works anywhere a built-in synth name is accepted:

```flow
Song song = [mySection]
Buffer buf = (renderSong song "customsaw")
```

## Envelopes

Shape the amplitude of a buffer over time.

### AR (Attack-Release)

```flow
use "@audio"

Envelope ar = (createAR 0.01 0.5 44100)     Note: attack, release (s), sample rate
(applyEnvelope buf ar)
```

### ADSR (Attack-Decay-Sustain-Release)

```flow
use "@audio"

Envelope adsr = (createADSR 0.01 0.1 0.7 0.3 44100)
(applyEnvelope buf adsr)
```

`applyEnvelope` returns a new buffer.

## Built-in Synthesizers

Pass one of these names to `renderSong`, `renderSequenceToVoices`, or `tempoRamp`:

| Name | Aliases | Character |
|------|---------|-----------|
| `"piano"` | — | Percussive hammer-like attack with warm decay |
| `"brass"` | `"horn"` | Bold, sustained tone with rich harmonics |
| `"sax"` | `"saxophone"` | Reed-like character with a slightly nasal tone |
| `"flute"` | — | Pure, breathy tone with soft attack |
| `"organ"` | — | Sustained, multi-partial timbre |
| `"strings"` | — | Smooth bowed-instrument-like timbre |
| `"bell"` | — | Inharmonic bell / chime character |
| `"drums"` | `"drum"` | Percussive synthesis; pitch maps to drum kit (low=kick, mid=snare, high=hat) |
| `"sine"` | — | Clean sine wave; useful for testing |

Plus any custom wavetable registered via `oscillator`.

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section melody {
                Sequence mel = | C4 E4 G4 C5 |
            }
            Song song = [melody]

            Buffer piano   = (renderSong song "piano")
            Buffer strings = (renderSong song "strings")
            Buffer organ   = (renderSong song "organ")
            Buffer bell    = (renderSong song "bell")
        }
    }
}
```

## Rendering Pipeline

The rendering pipeline converts musical structures to audio:

```
Song → Sections → Sequences → Bars → MusicalNotes → Synthesizer → Voices → Track → Buffer
```

1. A **Song** is split into its section arrangement.
2. Each **Section** provides sequences.
3. Each **Sequence** contains bars.
4. Each **Bar** contains musical notes with pitch, duration, velocity, articulation.
5. The **Synthesizer** renders each note to audio samples.
6. Notes are placed on a timeline as voices, gathered into tracks, and mixed.

### Direct Sequence → Buffer

If you don't need the `Song` layer, you can render a sequence directly:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Voice[] voices = (renderSequenceToVoices mel "piano" 44100 120.0)
}
```

See [Voices and Tracks](Voices-and-Tracks.md) for assembling voices into a final buffer.

### Custom Instrument Lambdas

`renderSong` accepts a Flow `Function` as the instrument argument, letting you write a custom per-note synthesizer:

```flow
Function myInstr = fn Note pitch, Double seconds => (createSineTone seconds (noteToFrequency pitch) 0.5)
Buffer buf = (renderSong song myInstr)
```

## BPM and Timeline

```flow
use "@audio"

(setBPM 120.0)
Double bpm = (getBPM)

Int frames = (beatsToFrames 4.0 44100)
Double beats = (framesToBeats 88200 44100)
```

## Voice and Track System

For lower-level control, Flow exposes a voice/track timeline. See [Voices and Tracks](Voices-and-Tracks.md) for a full walkthrough.

```flow
Voice v = (createVoice myBuffer 0.0)
(setVoiceGain v 0.8)
(setVoicePan v -0.5)

Track t = (createTrack 44100 2)
(addVoice t v)
Buffer rendered = (renderTrack t 8.0)
```

`setMaxVoices N` caps the polyphonic voice pool used during rendering.

## Vocalization

Synthesize vowels or consonant-vowel syllables via formants, or invoke an external TTS engine. See [Vocalization](Vocalization.md).

```flow
Buffer vocal = (sing "ah" C4 0.5)
```

## See Also

- [Effects](Effects.md) - Audio effect chains
- [Playback and Export](Playback-and-Export.md) - Playing, streaming, exporting WAV/MIDI
- [Song Structure](Song-Structure.md) - Song/section organization
- [Voices and Tracks](Voices-and-Tracks.md) - Multi-track timeline
- [Vocalization](Vocalization.md) - Formant synthesis and TTS
- [Visualization](Visualization.md) - ASCII piano roll and waveform output
