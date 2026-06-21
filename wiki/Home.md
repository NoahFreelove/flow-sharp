# Flow Language Wiki

**Flow** is a statically-typed, interpreted programming language designed for music production. It combines functional programming primitives with music-specific syntax and semantics, providing a great path from composition to audio export and notation interchange. The interpreter is written in C# targeting .NET 10.

## Key Features

### Language Core

- **Static typing** with several primitive and music-aware types

- **Flow operator (`->`)** for function chaining; **tuple-unpack flow (`~>`)** for tuple unpacking

- **Tuples** `<<a, b, c>>` with per-position types, structural equality, and destructuring assignment

- **Generic `Dict<K, V>`** with insertion-order preservation and 14-op surface

- **Symbol primitive** `#foo` — interned, pointer-equality, distinct from String

- **Pattern matching** — `(match scrutinee | pat => body | _ => default)` with music-aware patterns

- **Universal named arguments** — `(jam over=chords style=#jazz length=8)` works on any function with named params

- **Loops** — `for` / `while` with `break` / `continue`

- **String interpolation** — `$"text {expr}"` with escape support

- **Lambdas, closures, higher-order functions, function-type annotations**

### Notation & Composition

- **Inline note streams** (`| C4 D4 E4 F4 |`) with durations, dynamics, articulation, ornaments

- **Musical context blocks** — `tempo`, `timesig`, `key`, `swing`, `voicePool`, `tuning`, `pan`, `gain`, `dynamics`, `rit`, `accel`

- **Harmony** — chord literals, roman numerals, scales, arpeggios, voice-led `progression | I IV V I |`

- **Song structure** — sections (parameterized + overloaded), arrangements with `*N` repeats, multi-track voices, polyphony

### Generative & Expression

- **Pattern transforms** — transpose, invert, retrograde, augment, diminish, humanize, trill, tremolo, vary, polyrhythm, and more

- **Tidal-style combinators** — `every`, `fast`, `slow`, `jux`, `degrade`, `sometimes`, `palindrome` (via `@patterns`)

- **Generative primitives** — Markov chains, L-systems, cellular automata, chaos maps, Euclidean rhythms (via `@generative`)

- **Chord-aware improvisation** — `(jam over=chords style=#jazz)` with composer-editable style packs (via `@improv`)

### Synthesis & Audio

- **Built-in synthesizers** — sample-based piano (4 velocity layers), brass, sax, flute, strings, bell, organ, drums; raw oscillators; user-defined wavetables

- **SFZ orchestral sampler** — load VSCO Community Edition or any SFZ patch via `loadSfz`

- **Microtonal tuning** — cent offsets in note streams, Scala `.scl` loader, named-tuning pragmas, `tuning { }` blocks

- **Audio effects** — Schroeder reverb, biquad filters, compressor, sidechain, delay, gain, constant-power panning, fades; granular synthesis, time-stretch, pitch-shift

- **Vocalization** — formant-synthesized vowels/syllables and a TTS hook

### Export, Playback & Tooling

- **Playback** — blocking `play`, non-blocking `stream`, loop, preview

- **Export** — WAV (16/24/32-bit), Standard MIDI Files, MusicXML 3.1, LilyPond 2.24+

- **Import** — WAV, MIDI (via the `flow midi2flow` CLI), ABC 2.1, PC-98 MML

- **Tooling** — `flow` CLI (`run` / `eval` / `repl` / `watch` / `play` / `render` / `flow2midi` / `midi2flow` / `check` / `new` / `version` / `lsp` / `test` / `doc`), VSCode extension, LSP for Neovim / Helix / Emacs / Zed


## Wiki Pages

### Getting Started

- [Quick Start](Quick-Start.md) — Install, run your first script, hear your first melody

### Core Language

- [Language Basics](Language-Basics.md) — Variables, types, tuples, dicts, symbols, match, named args, comments, scoping
- [Functions](Functions.md) — Procedures, lambdas, closures, overloading, named args, function types
- [Flow Operator](Flow-Operator.md) — The `->` pipe operator, `~>` tuple unpack, `as NAME` chain naming
- [Collections](Collections.md) — Arrays, `Dict<K, V>`, and list operations
- [Loops](Loops.md) — `for`, `while`, `break`, `continue`
- [String Interpolation](String-Interpolation.md) — `$"..."` syntax

### Music Features

- [Note Streams](Note-Streams.md) — Inline musical notation
- [Musical Context](Musical-Context.md) — Tempo, key, time signature, swing, dynamics, pan, gain, voicePool, tuning, rit/accel
- [Chords and Harmony](Chords-and-Harmony.md) — Chord literals, roman numerals, scales
- [Chord Progressions](Chord-Progressions.md) — Voice-led `progression | I IV V I |`
- [Song Structure](Song-Structure.md) — Sections (including parameterized), songs, arrangements

### Expression and Transforms

- [Pattern Transforms](Pattern-Transforms.md) — Transpose, invert, retrograde, humanize, trill, tremolo, vary, polyrhythm, tempoRamp
- [Dynamics and Expression](Dynamics-and-Expression.md) — Dynamics, articulation, ornaments
- [Generative Music](Generative.md) — Euclidean rhythms, Markov, L-systems, cellular automata, chaos maps, Tidal combinators, `jam`

### Audio

- [Audio and Synthesis](Audio-and-Synthesis.md) — Buffers, oscillators, envelopes, synthesizers, custom wavetables, SFZ sampler
- [Effects](Effects.md) — Reverb, filters, compressor, sidechain, delay, gain, panning, granular, stretch, pitch-shift
- [Voices and Tracks](Voices-and-Tracks.md) — Multi-track timeline rendering, voice-pool polyphony
- [Vocalization](Vocalization.md) — Formant-synthesized singing and TTS
- [Visualization](Visualization.md) — ASCII piano-roll and waveform
- [Playback and Export](Playback-and-Export.md) — `play`, `stream`, WAV / MIDI / MusicXML / LilyPond export, loading WAVs, ABC / MML import
- Live Coding — `live { }` blocks, `flow watch` status panel, hot-swap, determinism trade-off *(wiki page pending)*
- OSC and MIDI — `@osc` (send/listen/pump), `@midi` (hardware output, clock), `@jack` (transport sync) *(wiki page pending)*

### Reference

- [Standard Library](Standard-Library.md) — Modules and complete function reference
- [Imports and Modules](Imports-and-Modules.md) — The `use` statement, the 15 stdlib modules, runtime gates
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
            Buffer final = rendered -> reverb 0.3 -> fadeOut 0.5s

            (writeWav "my_song.wav" final)
            (writeMidi "my_song.mid" mySong)
            (print $"rendered {(getFrames final)} frames")
        }
    }
}
```

## See Also

- [README](https://github.com/NoahFreelove/flow-sharp/blob/main/README.md) — Install, build, showcase
- [FEATURES.md](https://github.com/NoahFreelove/flow-sharp/blob/main/FEATURES.md) — Complete feature inventory with status (Fully / Partial / Not yet)
- [examples/](https://github.com/NoahFreelove/flow-sharp/tree/main/examples) — Tutorial chapters covering DSP, generative, improv, notation, tuning, sections, symphony, ragtime
