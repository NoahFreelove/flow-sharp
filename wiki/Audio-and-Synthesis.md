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

Note: tone generators also accept Hertz-typed frequencies
Buffer hi = (createSineTone 0.5 1.5kHz 0.5)
Buffer lo = (createSineTone 0.5 220Hz  0.5)
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

`loadWav` reads 16/24/32-bit PCM and auto-resamples to 44100 Hz. Optional varispeed overloads apply a pitch shift at load time (identity short-circuits at `semitones=0` / `ratio=1.0`):

```flow
Buffer loaded   = (loadWav "sample.wav")
Buffer up5      = (loadWav "sample.wav" 5)        Note: +5 semitones (Int)
Buffer downOct  = (loadWav "sample.wav" 0.5)      Note: half-speed = down one octave (Double ratio)
Int frames      = (getFrames loaded)
```

See [Playback and Export](Playback-and-Export.md) for exporting WAV/MIDI/notation.

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

Raw oscillator waveforms (`sine`, `saw`/`sawtooth`, `square`, `triangle`) are aliased and naive — no anti-aliasing by design. Use them for testing or chiptune-style work; reach for the wavetable variants (`warm`, `bright`, `buzz`) or sample-based instruments for production.

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

When you render through `renderSong`, every note also receives an articulation-aware envelope on top of the synth's natural amplitude curve — see [Articulations](Articulations.md) for the per-articulation shaping table (Staccato shortens + drops sustain, Marcato boosts velocity, Sforzando spikes the attack, etc.).

## Built-in Synthesizers

Pass one of these names to `renderSong`, `renderSequenceToVoices`, or `tempoRamp`:

| Name | Aliases | Character |
|------|---------|-----------|
| `"sine"` | — | Clean sine; useful for testing |
| `"saw"` | `"sawtooth"` | Naive sawtooth (no anti-alias) |
| `"square"` | — | Naive square (no anti-alias) |
| `"triangle"` | — | Naive triangle (no anti-alias) |
| `"piano"` | — | Sample-based — 4 velocity layers (pp / mp / mf / ff) at 5 pitch points |
| `"brass"` | `"horn"` | Sample-based — mf layer with linear velocity scaling |
| `"sax"` | `"saxophone"` | Sample-based — reedy, slightly nasal mf timbre |
| `"flute"` | — | Sample-based — G4 / A4 / G5 sample points |
| `"strings"` | `"string"` | Sample-based — smooth bowed-instrument tone |
| `"bell"` | — | Sample-based — inharmonic bell / chime |
| `"organ"` | — | Synthesis — 6 drawbar partials (16'/8'/5⅓'/4'/2⅔'/2') + 3-formant vowel filter bank |
| `"drums"` | `"drum"` | Synthesis — pitch maps to drum kit (low=kick, mid=snare, high=hat); built-in per-MIDI-key recipes |
| `"warm"` | — | Wavetable — additive sawtooth with boosted 2nd–6th partials (vintage-pad timbre) |
| `"bright"` | — | Wavetable — DC-removed 10%-duty pulse train (piercing leads / chiptune) |
| `"buzz"` | — | Wavetable — 1/√n supersaw stack (edge-of-clipping buzzy timbre) |

Plus any custom wavetable registered via `oscillator`, and any SFZ patch loaded via the `"sampler:NAME"` prefix (see below).

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
            Buffer warm    = (renderSong song "warm")
        }
    }
}
```

### Sample Bundle

The sample-based instruments load from a CC-BY 4.0 University of Iowa MIS bundle that ships with the binary (≈3 MB / 21 WAVs / 44.1 kHz mono). Per-instrument credits live in `flow-lang/Samples/{instrument}/LICENSE.md`. Samples eager-load on the first `renderSong` call and are cached for subsequent renders in the same process.

### Sustain-Pedal Tail (`release=`)

The piano synth honors an optional `release=` named argument (a `Second`-typed sustain-pedal-sim tail length, default `1.5s`, clamped to `[0.05s, 10.0s]`):

```flow
Buffer ringing = (renderSong song "piano" release=3.0s)
Buffer dry     = (renderSong song "piano" release=0.2s)
```

Other instruments accept the knob harmlessly (ignored).

## Rendering Pipeline

The rendering pipeline converts musical structures to audio:

```
Song → Sections → Sequences → Bars → MusicalNotes → Synthesizer → Voices → Track → Buffer
```

1. A **Song** is split into its section arrangement.
2. Each **Section** provides sequences.
3. Each **Sequence** contains bars (and voice-blocks, for polyphonic passages).
4. Each **Bar** contains musical notes with pitch, duration, velocity, articulation.
5. The **Synthesizer** renders each note to audio samples.
6. Notes are placed on a timeline as voices, gathered into tracks, mixed, and written out.

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

`renderSong` accepts a Flow `Function` as the instrument argument, letting you write a per-note synthesizer in Flow itself. The lambda contract is `(MusicalNote pitch, Double seconds, Double bpm) -> Buffer`:

```flow
Function myInstr = fn MusicalNote pitch, Double seconds, Double bpm =>
    (createSineTone seconds (noteToFrequency pitch) 0.5)
Buffer buf = (renderSong song myInstr)
```

### SFZ Orchestral Sampler (Opt-In)

For higher-fidelity orchestral parts, opt in to the SFZ surface and bind a patch to a name, then dispatch via the `"sampler:NAME"` prefix:

```flow
use "@std"
use "@audio"
use "@sfz"

Sfz violin = (loadSfz #violin)              Note: 20-entry GM dict lookup
Sfz custom = (loadSfz "/path/to/my.sfz")    Note: absolute-path bypass

tempo 120 {
    timesig 4/4 {
        key Dminor {
            section opening {
                Sequence mel = | D4 F4 A4 D5 |
            }
            Song song = [opening]
            Buffer buf = (renderSong song "sampler:violin")
        }
    }
}
```

The 20-entry GM dict covers strings (`#violin`, `#viola`, `#cello`, `#contrabass`), woodwinds (`#flute`, `#oboe`, `#clarinet`, `#bassoon`), brass (`#trumpet`, `#horn`, `#trombone`, `#tuba`), keys/plucked (`#piano`, `#harp`), and percussion (`#timpani`, `#drums` → VSCO-CE `GM-StylePerc.sfz` with transient-preserving pitch shift). Symbol lookups resolve against the `sfz_root` directory configured in `~/.config/flow/config.toml`; the blessed external library is VSCO Community Edition 1.1.0 (CC-BY 4.0, not vendored — composer installs separately). Four GM slots (`#choir`, `#guitar`, `#harpsichord`, `#celeste`) are not bundled with VSCO-CE 1.1.0 and require the absolute-path overload.

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

`setMaxVoices N` caps the polyphonic voice pool used during rendering; the `voicePool N { }` musical-context block does the same locally.

## Vocalization

Synthesize vowels or consonant-vowel syllables via formants, or invoke an external TTS engine. See [Vocalization](Vocalization.md).

```flow
Buffer vocal = (sing "ah" C4 0.5)
```

## See Also

- [Effects](Effects.md) - Audio effect chains (filters, reverb, granular, time-stretch, pitch-shift)
- [Playback and Export](Playback-and-Export.md) - Playing, streaming, exporting WAV/MIDI/notation
- [Song Structure](Song-Structure.md) - Song/section organization
- [Voices and Tracks](Voices-and-Tracks.md) - Multi-track timeline, polyphony, voice pool
- [Vocalization](Vocalization.md) - Formant synthesis and TTS
- [Visualization](Visualization.md) - ASCII piano roll and waveform output
