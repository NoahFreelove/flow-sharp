# Standard Library

Flow's standard library is split across a small kernel of always-on C# built-ins and a set of opt-in `.flow` modules that you bring in with `use "@name"`. This page is the navigation index — it lists every module, what it provides, and which dedicated wiki page covers it in depth.

For the mechanics of `use` and local-file imports, see [Imports and Modules](Imports-and-Modules.md).

## Why `use "@std"` Matters

Without `use "@std"`, you only have raw language syntax (variables, prefix arithmetic via `(add)` etc., `proc`, note streams, context blocks). Essential functions like `print`, `str`, `concat`, `list`, `map`, `filter`, and all collection operations require the standard library.

```flow
use "@std"
(print "now print works")
```

## Module Index

| Module | Import | Provides | Wiki Page |
|--------|--------|----------|-----------|
| core (auto) | none | Arithmetic, comparisons, logic, math, random, `if`, `eval`, lambdas | [Language Basics](Language-Basics.md) |
| `@std` | `use "@std"` | I/O, `str`, `concat`, type conversion, transitively re-exports `@collections` and `@bars` | this page |
| `@collections` | `use "@collections"` | List operations (`head`, `tail`, `map`, `filter`, `reduce`, …) | [Collections](Collections.md) |
| `@bars` | `use "@bars"` | Bar/note primitives | [Note Streams](Note-Streams.md) |
| `@audio` | `use "@audio"` | Buffers, signal generation, envelopes, effects, playback, WAV/MIDI I/O, granular / stretch / pitchShift, vocalization | [Audio and Synthesis](Audio-and-Synthesis.md), [Effects](Effects.md), [Playback and Export](Playback-and-Export.md), [Vocalization](Vocalization.md) |
| `@notation` | `use "@notation"` | Note-value constants, `MusicalNote` constructors, `Bar` / `Sequence` builders, time-signature presets | [Note Streams](Note-Streams.md) |
| `@composition` | `use "@composition"` | DAW-style timeline (`setBPM`, `createVoice`, `createTrack`), polyrhythm, `tempoRamp`, `vary`, `renderSong` | [Voices and Tracks](Voices-and-Tracks.md), [Pattern Transforms](Pattern-Transforms.md), [Generative Music](Generative.md) |
| `@patterns` | `use "@patterns"` | 13 Tidal-style combinators on `Sequence` (`every`, `fast`, `slow`, `jux`, `sometimes`, …) | [Generative Music](Generative.md) |
| `@generative` | `use "@generative"` | Markov chains, L-systems, cellular automata, chaos maps | [Generative Music](Generative.md) |
| `@improv` | `use "@improv"` | `jam` chord-aware Markov improvisation + composer-editable style packs | [Generative Music](Generative.md) |
| `@sfz` | `use "@sfz"` | SFZ orchestral sampler — `loadSfz`, 20-entry GM symbol dict | [Audio and Synthesis](Audio-and-Synthesis.md) |
| `@notation-io` | `use "@notation-io"` | `writeMusicXML`, `writeLilyPond` export; `abc`, `mml` import | [Playback and Export](Playback-and-Export.md) |
| `@test` | `use "@test"` | Test framework + assertion procs | [Tips and Tricks](Tips-and-Tricks.md) |

`@std` automatically imports `@collections` and `@bars`, so you rarely need to import them separately.

### Runtime Gates

Two modules flip runtime gates on `ExecutionContext` when imported:

- **`@sfz`** sets `SfzEnabled = true`. Calling `loadSfz` without `use "@sfz"` first raises a `"loadSfz requires \`use \"@sfz\"\`"` error rather than a generic "function not found".
- **`@notation-io`** sets `NotationIoEnabled = true`. Calling `writeMusicXML` / `writeLilyPond` / `abc` / `mml` without the import behaves the same way.

The gates exist so the GM symbol dict (for `@sfz`) and the notation IO surface (for `@notation-io`) don't sit in scope for composers who never asked for them.

## Core Functions

These are always available (no `use` needed for some; `use "@std"` for the printable / convertible surface).

### I/O

| Function | Signature | Description |
|----------|-----------|-------------|
| `print` | `(String) -> Void` | Print string with newline |
| `input` | `() -> String` | Read a line from stdin |

### String Operations

| Function | Signature | Description |
|----------|-----------|-------------|
| `str` | `(T) -> String` | Convert any value to string |
| `len` | `(String) -> Int` | String length |
| `len` | `(T[]) -> Int` | Array length |
| `concat` | `(String, String) -> String` | Concatenate strings |
| `concat` | `(T[], T[]) -> T[]` | Concatenate arrays |

`str` ships with 20+ overloads — Int, Long, Float, Double, Number, String, Bool, Note, Symbol, Bar, Semitone, Cent, Millisecond, Second, Decibel, Array, Sequence, Chord, Section, Song, Tuning, and more.

### Arithmetic (prefix-only)

Flow has no infix `+ - * /`. Use the prefix builtins:

| Function | Signature | Description |
|----------|-----------|-------------|
| `add` / `sub` / `mul` / `div` | `(Int, Int)` plus Float / Double / Long / Number overloads | Same-type fast paths |
| `neg` | `(Int) -> Int` / `(Double) -> Double` | Unary negation |
| `idiv` | `(Int, Int) -> Int` | Integer (floor) division |
| `abs` / `min` / `max` | `(Int, Int)` / `(Double, Double)` | Standard ops |

### Math

| Function | Signature | Description |
|----------|-----------|-------------|
| `sin` / `cos` / `tan` | `(Double) -> Double` | Trig |
| `sqrt` / `pow` / `log` | `(Double) -> Double` | Exponential / log |
| `floor` / `ceil` / `round` | `(Double) -> Int` | Rounding |
| `pi` / `tau` | `() -> Double` | π and 2π |

### Comparison and Logic

| Function | Signature | Description |
|----------|-----------|-------------|
| `equals` / `sequals` | `(T, T) -> Bool` | Loose / strict equality |
| `lt` / `gt` / `lte` / `gte` | `(T, T) -> Bool` | Ordering |
| `and` / `or` | `(Bool, Bool)` + lazy overload | Short-circuit via `Lazy<Bool>` |
| `not` | `(Bool) -> Bool` | Logical NOT |
| `if` | `(Bool, Lazy<T>, Lazy<T>) -> T` | Lazy-branch conditional |
| `eval` | `(Lazy<T>) -> T` | Force a lazy thunk |

### Type Conversion

| Function | Signature | Description |
|----------|-----------|-------------|
| `intToDouble` / `doubleToInt` | `(Int) -> Double` / `(Double) -> Int` | Numeric coercion |
| `stringToInt` / `stringToDouble` | `(String) -> Int` / `(String) -> Double` | Parse — returns `Void` on failure |

### Random

| Function | Signature | Description |
|----------|-----------|-------------|
| `?` | `() -> Float` | Random float `[0, 1)` |
| `??` | `() -> Float` | Seeded random float |
| `??set` | `(Int) -> Void` | Set the seeded RNG state |
| `??reset` | `() -> Void` | Reset to initial seeded state |

For algorithmic / Markov / Tidal-style randomness, see [Generative Music](Generative.md). All stochastic builtins in `@patterns` / `@generative` / `@improv` route through a per-render PRNG registry instead of these legacy globals.

## Collections (`@collections`)

See [Collections](Collections.md) for the deep-dive.

| Function | Signature | Description |
|----------|-----------|-------------|
| `list` | `(...T) -> T[]` | Create array from varargs |
| `head` / `tail` / `last` / `init` | array operations | Common destructuring |
| `empty` / `length` / `len` | predicates / size | |
| `reverse` / `take` / `drop` | array ops | |
| `append` / `prepend` / `concat` | array building | |
| `contains` | `(T[], T) -> Bool` | Membership |
| `map` / `filter` / `reduce` / `each` | `(T[], …) -> …` | Higher-order ops |
| `range` | `(Int, Int) -> Int[]` | Integer range `[lo, hi)` |
| `zip` | `(T[], U[]) -> [T, U][]` | Pair elements |

### Dict — 14-op surface

`Dict<K, V>` is a generic, insertion-order-preserving dictionary. Keys may be `Int`, `Long`, `Float`, `String`, `Symbol`, `Note`, `Chord`, or tuples of hashables. Construct via:

```flow
Dict<Symbol, Int> d = (dict #a 1 #b 2 #c 3)
Dict<Symbol, Int> d2 = (dictTuple <<#a, 1>> <<#b, 2>>)
```

| Op | Signature | Description |
|----|-----------|-------------|
| `get` | `(Dict, K) -> V` | Lookup (errors if missing) |
| `getOr` | `(Dict, K, V) -> V` | Lookup with default |
| `set` | `(Dict, K, V) -> Dict` | Insert / update |
| `remove` | `(Dict, K) -> Dict` | Delete a key |
| `has` | `(Dict, K) -> Bool` | Membership test |
| `keys` / `values` | `(Dict) -> Array` | All keys / values in insertion order |
| `size` | `(Dict) -> Int` | Entry count |
| `merge` | `(Dict, Dict) -> Dict` | RHS keys win |
| `each` / `map` / `filter` | `(Dict, Function)` | Higher-order ops |

## Audio (`@audio`)

See [Audio and Synthesis](Audio-and-Synthesis.md), [Effects](Effects.md), and [Playback and Export](Playback-and-Export.md) for full coverage. This is a navigation summary.

### Buffer creation and inspection

| Function | Signature | Notes |
|----------|-----------|-------|
| `createBuffer` | `(Int frames, Int channels, Int sampleRate) -> Buffer` | |
| `silence` | `(Double seconds) -> Buffer` | Zero-filled buffer |
| `getFrames` / `getChannels` / `getSampleRate` | `(Buffer) -> Int` | |
| `getSample` / `setSample` | sample I/O | |
| `fillBuffer` / `copyBuffer` / `sliceBuffer` | buffer mutation / copy | |
| `appendBuffers` / `mix` / `mixBuffers` | combining | `mix` is unity gain; `mixBuffers` is per-source gain |
| `scaleBuffer` / `fadeIn` / `fadeOut` | shaping | |

### Signal generation

| Function | Signature |
|----------|-----------|
| `createSineTone` / `createSawTone` / `createSquareTone` / `createTriangleTone` | `(Double dur, Double freq, Double amp)` |
| `generateSine` / `generateSaw` / `generateSquare` / `generateTriangle` | `(Buffer, OscillatorState, Double freq)` |
| `oscillator` | Register a custom wavetable: `(String name, Function gen[, Int tableSize])` or `(String name, Array table)` |

`createSineTone` and friends also accept a `Hertz` literal: `(createSineTone 0.5 440Hz 0.5)`.

### Envelopes

| Function | Signature |
|----------|-----------|
| `createAR` | `(Double attack, Double release, Int sr) -> Envelope` |
| `createADSR` | `(Double attack, Double decay, Double sustain, Double release, Int sr) -> Envelope` |
| `applyEnvelope` | `(Buffer, Envelope) -> Buffer` |

### Effects

See [Effects](Effects.md).

| Function | Signature | Notes |
|----------|-----------|-------|
| `reverb` | `(Buffer, Double decay)` + 4-arg overload | |
| `lowpass` / `highpass` | `(Buffer, Double|Hertz cutoff)` | |
| `bandpass` | `(Buffer, Double|Hertz center, Double|Hertz bandwidth)` | |
| `compress` / `sidechain` | dynamics processors | |
| `delay` | `(Buffer, Double time, Double feedback, Double mix)` | |
| `gain` | `(Buffer, Double|Decibel)` | **dB** — negative attenuates |
| `volume` | `(Buffer, Double)` | **Linear** — `0.5 = half amplitude` |
| `pan` | `(Buffer, Double)` | Stereo pan `-1..+1` |
| `granular` | `(Buffer, grain, density, jitter[, Symbol windowing])` | 3 overloads — `Hann` / `Gaussian` / `Tukey` windowing |
| `stretch` | `(Buffer, Double factor[, mode, frameSize, hopSize, overlap, transientThreshold, pitchPeriod, windowSize])` | 8-arity prefix-ladder; `#vocoder` / `#psola` / `#auto` |
| `pitchShift` | `(Buffer, Double|Cent|Semitone cents[, mode, …])` | **24 overloads** — 3 cents-types × 8 arity steps |

### Playback

| Function | Signature | Description |
|----------|-----------|-------------|
| `play` | `(Buffer) -> Void` / `(Sequence) -> Void` | Blocking |
| `stream` | `(Buffer) -> Void` / `(Sequence) -> Void` | Non-blocking |
| `loop` | `(Buffer) -> Void` / `(Buffer, Int)` | Loop |
| `preview` | `(Buffer) -> Void` | Low-quality preview |
| `stop` | `() -> Void` | Stop playback |
| `audioDevices` / `setAudioDevice` / `isAudioAvailable` | | |

### WAV / MIDI I/O

| Function | Signature | Notes |
|----------|-----------|-------|
| `writeWav` | `(String path, Buffer)` + bit-depth overload | Path-first |
| `exportWav` | `(Buffer, String path)` + bit-depth overload | Buffer-first |
| `loadWav` | `(String path)` + semitone / ratio varispeed overloads | Identity short-circuit at semitones=0 / ratio=1.0 |
| `writeMidi` | `(String path, Song)` | Multi-track Standard MIDI File export |

### Timeline and Voice (`@composition`)

| Function | Signature | Description |
|----------|-----------|-------------|
| `setBPM` / `getBPM` | tempo control | |
| `beatsToFrames` / `framesToBeats` | conversion | |
| `createVoice` / `setVoiceGain` / `setVoicePan` / `setVoiceOffset` | voice ops | |
| `createTrack` / `addVoice` / `setTrackGain` / `setTrackPan` / `setTrackOffset` | track ops | |
| `renderTrack` | `(Track, Double beats) -> Buffer` | |
| `setMaxVoices` | `(Int) -> Void` | Polyphonic voice-pool size |
| `renderSong` | `(Song, String synth)` / `(Song, Function customInstr)` / `(Song, String, Second release)` | `release=` named arg controls sustain-pedal tail length |

### Vocalization

See [Vocalization](Vocalization.md).

| Function | Signature | Description |
|----------|-----------|-------------|
| `sing` | `(String, Note, Double) -> Buffer` | Formant-synthesized vowel / syllable |
| `tts` | `(String) -> Buffer` | External TTS → buffer |
| `setTtsCommand` | `(String) -> Void` | Configure TTS command template |

### Visualization

See [Visualization](Visualization.md).

| Function | Signature | Description |
|----------|-----------|-------------|
| `visualize` | `(Sequence) -> Void` / `(Buffer) -> Void` | ASCII piano-roll / waveform |
| `prettyBuffer` | `(Buffer) -> Void` | Header + 60×11 waveform |
| `bufferHex` | `(Buffer) -> Void` / `(Buffer, Int, Int) -> Void` | Hex dump |

## Notation (`@notation`)

See [Note Streams](Note-Streams.md) for the inline `| ... |` syntax; `@notation` is the imperative-style API.

| Function | Signature | Description |
|----------|-----------|-------------|
| `createMusicalNote` | `(Note, NoteValue) -> MusicalNote` | |
| `createRest` | `(NoteValue) -> MusicalNote` | |
| `createTimeSignature` | `(Int, Int) -> TimeSignature` | |
| `createMusicalBar` / `createEmptyMusicalBar` | `(...) -> Bar` | |
| `createSequence` / `addBarToSequence` | `(...) -> Sequence` | |
| `noteToFrequency` | `(Note) -> Double` | A4 = 440 |
| `noteValueToBeats` / `validateBarDuration` / `getRemainingBeats` / `wouldFit` | bar arithmetic | |

Plus note-value constants `WHOLE`, `HALF`, `QUARTER`, `EIGHTH`, `SIXTEENTH`, `THIRTYSECOND` and time-signature presets `TS_4_4`, `TS_3_4`, `TS_6_8`, `TS_2_4`, `TS_5_4`, `TS_9_8`, `TS_12_8`.

## Bars (`@bars`)

| Function | Signature | Description |
|----------|-----------|-------------|
| `createBar` / `createBarWithNote` / `createBarFromNotes` | `(...) -> Bar` | |
| `addNoteToBar` / `tryAddNoteToBar` | bar mutation | |
| `getNoteFromBar` / `barLength` | bar inspection | |
| `setTimeSignature` / `getTimeSignature` | bar metadata | |

## Harmony

Always-on (registered as C# builtins).

| Function | Signature | Description |
|----------|-----------|-------------|
| `chordNotes` | `(Chord) -> String[]` | Notes in chord |
| `chordRoot` / `chordQuality` | `(Chord) -> String` | Chord parts |
| `arpeggio` | `(Chord, String) -> Sequence` | `"up"`, `"down"`, `"updown"` |
| `scaleNotes` | `(String) -> String[]` | Scale note names |
| `resolveNumeral` | `(String, String) -> Chord` | Roman numeral → chord |
| `getSections` / `sectionSequences` | `(Song) -> String[]` / `(Section) -> String[]` | Song introspection |

See [Chords and Harmony](Chords-and-Harmony.md) and [Chord Progressions](Chord-Progressions.md).

## Transforms (always-on)

See [Pattern Transforms](Pattern-Transforms.md) for the deep dive.

| Function | Signature |
|----------|-----------|
| `transpose` | `(Sequence, Semitone)` / `(Sequence, Cent)` |
| `invert` / `retrograde` | `(Sequence) -> Sequence` |
| `augment` / `diminish` | duration scaling |
| `up` / `down` | `(Sequence, Int) -> Sequence` |
| `repeat` | `(Sequence, Int)` / `(Sequence, Int, Semitone)` |
| `concat` | `(Sequence, Sequence) -> Sequence` |
| `crescendo` / `decrescendo` / `swell` | velocity shaping |
| `ritardando` / `accelerando` | tempo feel |
| `fermata` / `trill` / `tremolo` | embellishment |
| `humanize` | `(Sequence, Double)` — uniform velocity jitter |
| `humanizeGaussian` | `(Sequence, Double, Int seed)` — Box-Muller, seeded, voice-block-aware |
| `legato` | `(Sequence, Double)` — duration overlap |

## Composition

Loaded via `@composition`.

| Function | Signature | Description |
|----------|-----------|-------------|
| `euclidean` | `(Int, Int, Note)` + swing / humanize overloads | Bjorklund rhythm |
| `vary` | `(Sequence, Double)` + typed / seeded / diatonic overloads | Stochastic mutation |
| `polyrhythm` | `(Sequence, Sequence)` / `(…, Int)` | LCM overlay |
| `tempoRamp` | `(Sequence, Double, Double)` / `(…, String)` | Rendered tempo interpolation |
| `renderSequenceToVoices` / `renderBarToVoices` / `renderBarAtBeat` / `renderBarAtTime` | low-level voice rendering | |

## Pattern Combinators (`@patterns`)

13 Tidal-style combinators on `Sequence`. Cycle unit is bars; transform-arg combinators are lambda-required; degenerate inputs return input + advisory (never throws). See [Generative Music](Generative.md) for full coverage.

| Combinator | Signature |
|------------|-----------|
| `every` | `(Int n, Function cb, Sequence seq) -> Sequence` |
| `fast` / `slow` | `(Sequence, Double factor) -> Sequence` |
| `chunk` | `(Int n, Function cb, Sequence seq) -> Sequence` |
| `phase` | `(Double offset, Sequence seq) -> Sequence` |
| `rev` / `palindrome` | `(Sequence) -> Sequence` |
| `iter` | `(Int n, Sequence seq) -> Sequence` |
| `jux` / `superimpose` | `(Function cb, Sequence seq) -> Sequence` |
| `sometimes` | `(Double prob, Function cb, Sequence seq)` + default-prob overload |
| `degrade` | `(Sequence) -> Sequence` (fixed 50% drop) |
| `sparseSeq` | `(Double prob, Sequence seq) -> Sequence` |

## Generative (`@generative`)

Markov, L-system, cellular automata, chaos maps. See [Generative Music](Generative.md).

| Function | Signature |
|----------|-----------|
| `markov` / `markovTrain` / `markovGenerate` / `markovEqual` | Markov chains, train+generate split, structural compare |
| `lsystem` / `lsystemModel` / `lsystemGenerate` / `lsystemToSequence` / `lsystemEqual` | L-systems |
| `cellular` / `cellularSeeded` / `life` | 1D + 2D cellular automata |
| `lorenz` / `logistic` / `quantizeToScale` | Chaos maps + scale bridge |

## Improv (`@improv`)

See [Generative Music](Generative.md).

| Function | Signature |
|----------|-----------|
| `jam` | 6 arity overloads of `jam(over[, style, length, key, seed, order]) -> Sequence` |
| `registerStyle` | `(Symbol name, Dict pack) -> Void` |
| `listStyles` | `() -> Array[Symbol]` |

Shipped style packs: `#jazz`, `#blues`, `#classical` (composer-editable Flow files under `flow-lang/improv/styles/`).

## SFZ Orchestral Sampler (`@sfz`)

Opt-in via `use "@sfz"` (flips the `SfzEnabled` runtime gate). See [Audio and Synthesis](Audio-and-Synthesis.md).

| Function | Signature | Description |
|----------|-----------|-------------|
| `loadSfz` | `(Symbol) -> Sfz` | Resolves against the 20-entry GM dict + `sfz_root` config |
| `loadSfz` | `(String path) -> Sfz` | Absolute path bypass |

After loading, dispatch via `renderSong song "sampler:NAME"`. The dict ships `#violin`, `#viola`, `#cello`, `#contrabass`, `#flute`, `#oboe`, `#clarinet`, `#bassoon`, `#trumpet`, `#horn`, `#trombone`, `#tuba`, `#piano`, `#harp`, `#timpani`, `#drums`, plus 4 placeholders for instruments not in VSCO-CE (`#choir`, `#guitar`, `#harpsichord`, `#celeste`) which point composers at the absolute-path overload.

## Notation IO (`@notation-io`)

Opt-in via `use "@notation-io"` (flips the `NotationIoEnabled` runtime gate). See [Playback and Export](Playback-and-Export.md).

| Function | Signature | Notes |
|----------|-----------|-------|
| `writeMusicXML` | `(String path, Song) -> Void` | MusicXML 3.1 partwise, MuseScore-compatible |
| `writeLilyPond` | `(String path, Song) -> Void` | LilyPond 2.24+ text |
| `abc` | `(String source) -> Section` / `(String source) -> Array[Section]` | ABC 2.1 subset + abc2midi extensions; multi-tune `X:1`/`X:2` returns an array |
| `mml` | `(String source) -> Sequence` | PC-98 MML common core |

Distinct from `@notation`, which provides the musical-notation primitives (bar/sequence construction).

## Test Framework (`@test`)

Phase 35 test framework. See `tests/test_test_library.flow` for the legacy pure-Flow assertion API as well.

| Function | Signature | Notes |
|----------|-----------|-------|
| `test` | `(String name, Lazy<Void> body) -> Void` | Wrap body with `lazy(...)` so it's deferred until the runner forces it |
| `assert` | `(Bool) -> Void` | |
| `assertEq` | `(T, T) -> Void` | |
| `assertNotesMatch` | `(Sequence, Sequence) -> Void` | Structural sequence compare |
| `assertBytesEqual` | `(Buffer, Buffer) -> Void` | Byte-identical buffer compare |
| `assertWithinDb` | `(Buffer, Buffer, Decibel tolerance) -> Void` | Perceptual tolerance for things that legitimately change bytes |

Legacy procs `assertTrue`, `assertEqual`, `runTest(String, Function)`, and `summary` remain available for older test suites.

## See Also

- [Imports and Modules](Imports-and-Modules.md) — `use` mechanics, local-file imports, module resolution
- [Language Basics](Language-Basics.md) — types, operators, scoping
- [Collections](Collections.md) — `@collections` deep dive
- [Audio and Synthesis](Audio-and-Synthesis.md) — `@audio` deep dive
- [Effects](Effects.md) — `@audio` DSP surface
- [Playback and Export](Playback-and-Export.md) — `play` / `stream` / WAV / MIDI / `@notation-io`
- [Pattern Transforms](Pattern-Transforms.md) — `transpose`, `invert`, `retrograde`, `humanize`, `polyrhythm`, `tempoRamp`, …
- [Generative Music](Generative.md) — `@patterns`, `@generative`, `@improv`, Euclidean, `vary`
- [Voices and Tracks](Voices-and-Tracks.md) — timeline API
- [Vocalization](Vocalization.md) — `sing`, `tts`
- [Visualization](Visualization.md) — `visualize`, `prettyBuffer`, `bufferHex`
