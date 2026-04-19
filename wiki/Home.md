# Flow Language Wiki

**Flow** is a statically-typed, interpreted programming language designed for music production. It combines general-purpose programming with music-specific syntax and semantics, providing a seamless path from composition to audio export. The interpreter is written in C# targeting .NET 9.

## Key Features

- **Static typing** with music-aware types (`Note`, `Chord`, `Sequence`, `Song`, `Bar`, `Voice`, `Track`, `Buffer`, etc.)
- **Flow operator (`->`)** for elegant function chaining and effect pipelines
- **Inline note streams** (`| C4 D4 E4 F4 |`) with durations, dynamics, articulation, ornaments
- **Musical context blocks** — `tempo`, `timesig`, `key`, `swing`, `dynamics`, `pan`, `gain`, `rit`, `accel`
- **Pattern transforms** — transpose, invert, retrograde, augment, diminish, humanize, trill, tremolo, vary, and more
- **Chord progressions with voice leading** (`progression | I IV V I |`)
- **Built-in synthesizers** — piano, brass, sax, flute, organ, bell, strings, drums, sine, and custom wavetables
- **Audio effects** — reverb, filters, compressor, sidechain, delay, gain, panning, fades
- **Song structure** — sections, arrangements with repeats, multi-track voices
- **Harmony** — chord literals, roman numerals, scales, arpeggios
- **Generative features** — Euclidean rhythms, weighted / seeded random choice, diatonic variation, polyrhythms
- **Vocalization** — formant-synthesized vowels/syllables and a TTS hook
- **Loops, string interpolation, lambdas, closures, higher-order functions**
- **Playback** — blocking `play`, non-blocking `stream`, loop, preview
- **Export** — WAV (16/24/32-bit) and Standard MIDI Files

## Wiki Pages

### Getting Started

- [Quick Start](Quick-Start.md) — Install, build, and run your first script

### Core Language

- [Language Basics](Language-Basics.md) — Variables, types, operators, comments, scoping
- [Functions](Functions.md) — Procedures, lambdas, closures, overloading
- [Flow Operator](Flow-Operator.md) — The `->` pipe operator
- [Collections](Collections.md) — Arrays and list operations
- [Loops](Loops.md) — `for`, `while`, `break`, `continue`
- [String Interpolation](String-Interpolation.md) — `$"..."` syntax

### Music Features

- [Note Streams](Note-Streams.md) — Inline musical notation
- [Musical Context](Musical-Context.md) — Tempo, key, time signature, swing, dynamics, pan, gain, rit/accel
- [Chords and Harmony](Chords-and-Harmony.md) — Chord literals, roman numerals, scales
- [Chord Progressions](Chord-Progressions.md) — Voice-led `progression | I IV V I |`
- [Song Structure](Song-Structure.md) — Sections, songs, arrangements

### Expression and Transforms

- [Pattern Transforms](Pattern-Transforms.md) — Transpose, invert, retrograde, humanize, trill, tremolo, vary, polyrhythm, tempoRamp
- [Dynamics and Expression](Dynamics-and-Expression.md) — Dynamics, articulation, ornaments
- [Generative Music](Generative.md) — Euclidean rhythms, random choice, `vary`, polyrhythms

### Audio

- [Audio and Synthesis](Audio-and-Synthesis.md) — Buffers, oscillators, envelopes, synthesizers, custom wavetables
- [Effects](Effects.md) — Reverb, filters, compressor, sidechain, delay, gain, panning
- [Voices and Tracks](Voices-and-Tracks.md) — Multi-track timeline rendering
- [Vocalization](Vocalization.md) — Formant-synthesized singing and TTS
- [Visualization](Visualization.md) — ASCII piano-roll and waveform
- [Playback and Export](Playback-and-Export.md) — `play`, `stream`, WAV export, MIDI export, loading WAVs

### Reference

- [Standard Library](Standard-Library.md) — Modules and complete function reference
- [Imports and Modules](Imports-and-Modules.md) — The `use` statement and module system
- [Tips and Tricks](Tips-and-Tricks.md) — Idioms, shorthands, and common pitfalls
- [Examples](Examples.md) — Complete working programs

## Quick Example

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section intro {
                Sequence melody = | C4q E4q G4q C5q | -> crescendo 0.2 0.7
            }
            section hook {
                Sequence chords = progression | I IV V I |
            }

            Song mySong = [intro hook*2]
            Buffer rendered = (renderSong mySong "piano")
            Buffer final = rendered -> reverb 0.3 -> fadeOut 0.5

            (exportWav final "my_song.wav")
            (writeMidi "my_song.mid" mySong)
            (print $"rendered {(getFrames final)} frames")
        }
    }
}
```
