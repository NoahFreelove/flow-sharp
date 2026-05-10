# Audio and Synthesis

Flow provides a complete audio pipeline from buffer creation through synthesis to final output. Most audio functions require `use "@audio"`.

## Buffers

A `Buffer` is a container for audio sample data with frames, channels, and a sample rate.

### Creating Buffers

```flow
use "@std"
use "@audio"

Note: Create a buffer (frames, channels, sampleRate)
Buffer buf = (createBuffer 44100 2 44100)  Note: 1 second of stereo audio

Note: Convenience functions
Buffer silence = (createSilence 1.0)       Note: 1 second of stereo silence
Buffer silenceMono = (createSilenceMono 1.0) Note: 1 second of mono silence
```

### Buffer Properties

```flow
Int frames = (getFrames buf)
Int channels = (getChannels buf)
Int sampleRate = (getSampleRate buf)
Bool mono = (isMono buf)
Bool stereo = (isStereo buf)
Bool compat = (buffersCompatible buf1 buf2)
```

### Sample Access

```flow
Float sample = (getSample buf 0 0)     Note: frame 0, channel 0
(setSample buf 0 0 0.5)                Note: set frame 0, channel 0 to 0.5
(fillBuffer buf 0.0)                   Note: fill entire buffer with 0.0
```

### Buffer Manipulation

```flow
Buffer copy = (copyBuffer buf)              Note: deep copy
Buffer slice = (sliceBuffer buf 0 22050)    Note: first half-second
Buffer joined = (appendBuffers buf1 buf2)   Note: concatenate
(scaleBuffer buf 0.5)                       Note: scale all samples by 0.5 (in-place)
```

## Oscillators

Generate basic waveforms using oscillator state:

```flow
use "@std"
use "@audio"

Note: Create oscillator (frequency, sampleRate)
OscillatorState osc = (createOscillatorState 440.0 44100)

Note: Generate waveforms into a buffer
Buffer buf = (createBuffer 44100 1 44100)
(generateSine buf osc 0.5)       Note: sine wave at amplitude 0.5
(generateSaw buf osc 0.5)        Note: sawtooth wave
(generateSquare buf osc 0.5)     Note: square wave
(generateTriangle buf osc 0.5)   Note: triangle wave

(resetPhase osc)  Note: reset oscillator phase to 0
```

### Convenience Tone Functions

```flow
use "@audio"

Note: Create a ready-to-use tone (duration, frequency, amplitude)
Buffer sine = (createSineTone 0.5 440.0 0.5)
Buffer saw = (createSawTone 0.5 440.0 0.5)
Buffer square = (createSquareTone 0.5 440.0 0.5)
Buffer triangle = (createTriangleTone 0.5 440.0 0.5)
```

## Envelopes

Shape the amplitude of a buffer over time:

### AR Envelope (Attack-Release)

```flow
use "@audio"

Note: (attackSeconds, releaseSeconds, sampleRate)
Envelope ar = (createAR 0.01 0.5 44100)
(applyEnvelope buf ar)
```

### ADSR Envelope (Attack-Decay-Sustain-Release)

```flow
use "@audio"

Note: (attack, decay, sustainLevel, release, sampleRate)
Envelope adsr = (createADSR 0.01 0.1 0.7 0.3 44100)
(applyEnvelope buf adsr)
```

`applyEnvelope` modifies the buffer in-place.

## Built-in Synthesizers

Flow includes several synthesizers accessible through `renderSong`:

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

            Buffer piano = (renderSong song "piano")
            Buffer brass = (renderSong song "brass")
            Buffer sax = (renderSong song "sax")
            Buffer drums = (renderSong song "drums")
            Buffer sine = (renderSong song "sine")
        }
    }
}
```

### Synthesizer Characters

| Synth | Aliases | Character |
|-------|---------|-----------|
| **Piano** | `"piano"` | Percussive hammer-like attack with warm, naturally decaying tone. Uses detuned oscillators for richness. |
| **Brass** | `"brass"`, `"horn"` | Bold, sustained tone with gradual attack. Rich harmonics for a horn-like quality. |
| **Saxophone** | `"sax"`, `"saxophone"` | Reed-like character with moderate attack and sustain. Slightly nasal harmonic content. |
| **Flute** | `"flute"` | Pure, breathy tone with soft attack. Minimal harmonics for a clean sound. |
| **Drums** | `"drums"`, `"drum"` | Percussive synthesis. Note pitch determines drum sound (low=kick, mid=snare, high=hi-hat). |
| **Sine** | `"sine"` | Clean sine wave. Useful for testing and pure tones. |

## Rendering Pipeline

The rendering pipeline converts musical structures to audio:

```
Song → Sections → Sequences → Bars → MusicalNotes → Synthesizer → Buffer
```

1. **Song** is split into its section arrangement
2. Each **Section** provides sequences
3. Each **Sequence** contains bars
4. Each **Bar** contains musical notes with pitch, duration, velocity, articulation
5. The **Synthesizer** renders each note to audio samples
6. Notes are positioned on a timeline and mixed to a final stereo buffer

### Direct Sequence Rendering

You can also render sequences directly without a song structure:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Voice[] voices = (renderSequence mel "piano" 44100 120.0)
}
```

## Voice and Track System

For lower-level control over audio positioning:

```flow
use "@audio"

Note: Create a voice (buffer positioned at a beat offset)
Voice v = (createVoice myBuffer 0.0)
(setVoiceGain v 0.8)
(setVoicePan v -0.5)   Note: pan left

Note: Create a track and add voices
Track t = (createTrack 44100 2)
(addVoice t v)
(setTrackGain t 0.9)

Note: Render track to buffer
Buffer rendered = (renderTrack t 8.0)  Note: 8 beats duration
```

## BPM and Timeline

```flow
use "@audio"

(setBPM 120.0)
Double bpm = (getBPM)

Note: Convert between beats and frames
Int frames = (beatsToFrames 4.0 44100)    Note: 4 beats at 44100 Hz
Double beats = (framesToBeats 88200 44100) Note: convert frames to beats
```

## Mixing Buffers

```flow
use "@audio"

Buffer mixed = (mixBuffers buf1 buf2 0.7 0.3)
Note: mix buf1 at 70% gain with buf2 at 30% gain
```

## See Also

- [Effects](Effects.md) - Audio effects processing
- [Playback and Export](Playback-and-Export.md) - Playing and saving audio
- [Song Structure](Song-Structure.md) - Song rendering
