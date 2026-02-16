# Flow Language Wiki

**Flow** is a statically-typed, interpreted programming language designed for music production. It combines general-purpose programming with music-specific syntax and semantics, providing a seamless path from composition to audio export.

## Key Features

- **Static typing** with music-aware types (Note, Chord, Sequence, Song, Bar, etc.)
- **Flow operator (`->`)** for elegant function chaining and effect pipelines
- **Inline note streams** (`| C4 D4 E4 F4 |`) with duration, dynamics, and articulation
- **Musical context blocks** (tempo, key, time signature, swing, dynamics)
- **Pattern transforms** (transpose, invert, retrograde, augment, diminish)
- **Built-in synthesizers** (piano, brass, sax, flute, drums)
- **Audio effects** (reverb, filters, compressor, delay, gain)
- **Song structure** with sections, arrangements, and repeat counts
- **Harmony support** with chord literals, roman numerals, and scale operations
- **WAV export** and real-time playback

## Wiki Pages

### Getting Started
- [Quick Start](Quick-Start.md) - Install, build, and run your first script

### Core Language
- [Language Basics](Language-Basics.md) - Variables, types, operators, comments, scoping
- [Functions](Functions.md) - Procedures, lambdas, closures, overloading
- [Flow Operator](Flow-Operator.md) - The `->` pipe operator
- [Collections](Collections.md) - Arrays and list operations

### Music Features
- [Note Streams](Note-Streams.md) - Inline musical notation
- [Musical Context](Musical-Context.md) - Tempo, key, time signature, swing, dynamics
- [Chords and Harmony](Chords-and-Harmony.md) - Chord literals, roman numerals, scales
- [Song Structure](Song-Structure.md) - Sections, songs, and arrangements

### Expression and Transforms
- [Pattern Transforms](Pattern-Transforms.md) - Transpose, invert, retrograde, and more
- [Dynamics and Expression](Dynamics-and-Expression.md) - Dynamics, articulation, ornaments

### Audio
- [Audio and Synthesis](Audio-and-Synthesis.md) - Buffers, oscillators, envelopes, synthesizers
- [Effects](Effects.md) - Reverb, filters, compressor, delay, gain
- [Playback and Export](Playback-and-Export.md) - Play, loop, preview, WAV export

### Reference
- [Standard Library](Standard-Library.md) - Modules and complete function reference
- [Imports and Modules](Imports-and-Modules.md) - The `use` statement and module system
- [Tips and Tricks](Tips-and-Tricks.md) - Idioms, shorthands, and common pitfalls
- [Examples](Examples.md) - Complete working programs

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
            section chorus {
                Sequence lead = | I IV V I |
            }

            Song mySong = [intro chorus*2]
            Buffer rendered = (renderSong mySong "piano")
            (exportWav rendered "my_song.wav")
        }
    }
}
```
