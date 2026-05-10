# Standard Library

Flow's standard library provides all built-in functions. Most programs need `use "@std"` at minimum.

## Why `use "@std"` Matters

Without `use "@std"`, you only have raw language syntax (variables, operators, `proc`, note streams). Essential functions like `print`, `str`, `concat`, `add`, `sub`, `list`, `map`, `filter`, and all collection operations require the standard library.

```flow
use "@std"
(print "Now I can use print!")
```

## Modules

| Module | Import | Provides |
|--------|--------|----------|
| `@std` | `use "@std"` | Core functions + collections + bars (imports @collections and @bars) |
| `@collections` | `use "@collections"` | List operations (head, tail, map, filter, etc.) |
| `@audio` | `use "@audio"` | Audio creation, effects, playback, synthesis |
| `@notation` | `use "@notation"` | Musical notation (note durations, rests, time signatures) |
| `@bars` | `use "@bars"` | Bar/note operations |
| `@composition` | `use "@composition"` | Timeline, voice, track convenience functions |

`@std` automatically imports `@collections` and `@bars`, so you rarely need to import them separately.

## Core Functions

### I/O

| Function | Signature | Description |
|----------|-----------|-------------|
| `print` | `(String) -> Void` | Print string with newline |
| `input` | `() -> String` | Read line from stdin |

### String Operations

| Function | Signature | Description |
|----------|-----------|-------------|
| `str` | `(T) -> String` | Convert any value to string |
| `len` | `(String) -> Int` | String length |
| `concat` | `(String, String) -> String` | Concatenate strings |

`str` has overloads for: Int, Float, Double, String, Bool, Note, Bar, Semitone, Cent, Millisecond, Second, Decibel, Array, Sequence, Chord, Section, Song.

### Arithmetic

| Function | Signature | Description |
|----------|-----------|-------------|
| `add` | `(Int, Int) -> Int` | Addition |
| `sub` | `(Int, Int) -> Int` | Subtraction |
| `mul` | `(Int, Int) -> Int` | Multiplication |
| `div` | `(Int, Int) -> Int` | Integer division |
| `add` | `(Double, Double) -> Double` | Double addition |
| `sub` | `(Double, Double) -> Double` | Double subtraction |
| `mul` | `(Double, Double) -> Double` | Double multiplication |
| `div` | `(Double, Double) -> Double` | Double division |

Note: Binary operators `+`, `-`, `*`, `/` also work for arithmetic expressions.

### Comparison

| Function | Signature | Description |
|----------|-----------|-------------|
| `equals` | `(Void, Void) -> Bool` | Loose equality (with type coercion) |
| `sequals` | `(Void, Void) -> Bool` | Strict equality (type must match) |
| `lt` | `(Void, Void) -> Bool` | Less than |
| `gt` | `(Void, Void) -> Bool` | Greater than |
| `lte` | `(Void, Void) -> Bool` | Less than or equal |
| `gte` | `(Void, Void) -> Bool` | Greater than or equal |

### Logical

| Function | Signature | Description |
|----------|-----------|-------------|
| `and` | `(Bool, Bool) -> Bool` | Logical AND |
| `or` | `(Bool, Bool) -> Bool` | Logical OR |
| `not` | `(Bool) -> Bool` | Logical NOT |
| `if` | `(Bool, Lazy, Lazy) -> T` | Conditional (lazy evaluation) |

`and` and `or` also have lazy overloads that short-circuit evaluation.

### Type Conversion

| Function | Signature | Description |
|----------|-----------|-------------|
| `intToDouble` | `(Int) -> Double` | Int to Double |
| `doubleToInt` | `(Double) -> Int` | Double to Int (truncates) |
| `stringToInt` | `(String) -> Int\|Void` | Parse string to Int |
| `stringToDouble` | `(String) -> Double\|Void` | Parse string to Double |

### Lazy Evaluation

| Function | Signature | Description |
|----------|-----------|-------------|
| `eval` | `(Lazy) -> T` | Force evaluation of lazy value |

### Random

| Function | Signature | Description |
|----------|-----------|-------------|
| `?` | `() -> Float` | Random float 0.0-1.0 |
| `??` | `() -> Float` | Seeded random float |
| `??set` | `(Int) -> Void` | Set random seed |
| `??reset` | `() -> Void` | Reset seeded random |

## Collection Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `list` | `(...T) -> T[]` | Create array (varargs) |
| `length` / `len` | `(T[]) -> Int` | Array length |
| `head` | `(T[]) -> T` | First element |
| `tail` | `(T[]) -> T[]` | All except first |
| `last` | `(T[]) -> T` | Last element |
| `init` | `(T[]) -> T[]` | All except last |
| `empty` | `(T[]) -> Bool` | Is empty? |
| `reverse` | `(T[]) -> T[]` | Reverse order |
| `take` | `(T[], Int) -> T[]` | First N elements |
| `drop` | `(T[], Int) -> T[]` | Drop first N |
| `append` | `(T[], T) -> T[]` | Add to end |
| `prepend` | `(T, T[]) -> T[]` | Add to start |
| `concat` | `(T[], T[]) -> T[]` | Concatenate arrays |
| `contains` | `(T[], T) -> Bool` | Contains element? |
| `map` | `(T[], T => U) -> U[]` | Transform elements |
| `filter` | `(T[], T => Bool) -> T[]` | Filter by predicate |
| `reduce` | `(T[], U, (U,T) => U) -> U` | Fold with accumulator |
| `each` | `(T[], T => Void) -> Void` | Apply for side effects |
| `range` | `(Int, Int) -> Int[]` | Integer range |
| `zip` | `(T[], U[]) -> [T,U][]` | Pair elements |

## Audio Functions

### Buffer Operations

| Function | Signature | Description |
|----------|-----------|-------------|
| `createBuffer` | `(Int, Int, Int) -> Buffer` | Create buffer (frames, channels, sampleRate) |
| `getFrames` | `(Buffer) -> Int` | Frame count |
| `getChannels` | `(Buffer) -> Int` | Channel count |
| `getSampleRate` | `(Buffer) -> Int` | Sample rate |
| `getSample` | `(Buffer, Int, Int) -> Float` | Get sample (frame, channel) |
| `setSample` | `(Buffer, Int, Int, Double) -> Void` | Set sample |
| `fillBuffer` | `(Buffer, Double) -> Void` | Fill with constant |
| `copyBuffer` | `(Buffer) -> Buffer` | Deep copy |
| `sliceBuffer` | `(Buffer, Int, Int) -> Buffer` | Extract slice |
| `appendBuffers` | `(Buffer, Buffer) -> Buffer` | Concatenate |
| `scaleBuffer` | `(Buffer, Double) -> Void` | Scale amplitude |
| `mixBuffers` | `(Buffer, Buffer, Double, Double) -> Buffer` | Mix with gains |

### Signal Generation

| Function | Signature | Description |
|----------|-----------|-------------|
| `createOscillatorState` | `(Double, Int) -> OscillatorState` | Create oscillator |
| `resetPhase` | `(OscillatorState) -> Void` | Reset phase |
| `generateSine` | `(Buffer, OscillatorState, Double) -> Void` | Sine wave |
| `generateSaw` | `(Buffer, OscillatorState, Double) -> Void` | Sawtooth wave |
| `generateSquare` | `(Buffer, OscillatorState, Double) -> Void` | Square wave |
| `generateTriangle` | `(Buffer, OscillatorState, Double) -> Void` | Triangle wave |

### Envelopes

| Function | Signature | Description |
|----------|-----------|-------------|
| `createAR` | `(Double, Double, Int) -> Envelope` | Attack-Release envelope |
| `createADSR` | `(Double, Double, Double, Double, Int) -> Envelope` | ADSR envelope |
| `applyEnvelope` | `(Buffer, Envelope) -> Void` | Apply envelope (in-place) |

### Effects

| Function | Signature | Description |
|----------|-----------|-------------|
| `reverb` | `(Buffer, Double) -> Buffer` | Reverb (roomSize) |
| `reverb` | `(Buffer, Double, Double, Double) -> Buffer` | Reverb (room/damp/mix) |
| `lowpass` | `(Buffer, Double) -> Buffer` | Low-pass filter |
| `highpass` | `(Buffer, Double) -> Buffer` | High-pass filter |
| `bandpass` | `(Buffer, Double, Double) -> Buffer` | Band-pass filter |
| `compress` | `(Buffer, Double, Double) -> Buffer` | Compressor |
| `compress` | `(Buffer, Double, Double, Double, Double) -> Buffer` | Full compressor |
| `delay` | `(Buffer, Double, Double, Double) -> Buffer` | Feedback delay |
| `gain` | `(Buffer, Double) -> Buffer` | Gain in dB |
| `fadeIn` | `(Buffer, Double) -> Buffer` | Linear fade-in |
| `fadeOut` | `(Buffer, Double) -> Buffer` | Linear fade-out |

### Playback

| Function | Signature | Description |
|----------|-----------|-------------|
| `play` | `(Buffer) -> Void` | Play buffer |
| `play` | `(Sequence) -> Void` | Render and play |
| `loop` | `(Buffer) -> Void` | Loop indefinitely |
| `loop` | `(Buffer, Int) -> Void` | Loop N times |
| `preview` | `(Buffer) -> Void` | Low-quality preview |
| `stop` | `() -> Void` | Stop playback |
| `audioDevices` | `() -> String[]` | List devices |
| `setAudioDevice` | `(String) -> Bool` | Set output device |
| `isAudioAvailable` | `() -> Bool` | Check audio backend |

### WAV Export

| Function | Signature | Description |
|----------|-----------|-------------|
| `exportWav` | `(Buffer, String) -> Void` | Export 16-bit WAV |
| `exportWav` | `(Buffer, String, Int) -> Void` | Export with bit depth |

### Timeline

| Function | Signature | Description |
|----------|-----------|-------------|
| `setBPM` | `(Double) -> Void` | Set global BPM |
| `getBPM` | `() -> Double` | Get current BPM |
| `beatsToFrames` | `(Double, Int) -> Int` | Beats to sample frames |
| `framesToBeats` | `(Int, Int) -> Double` | Frames to beats |

## Harmony Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `chordNotes` | `(Chord) -> String[]` | Notes in chord |
| `chordRoot` | `(Chord) -> String` | Root note |
| `chordQuality` | `(Chord) -> String` | Quality string |
| `arpeggio` | `(Chord, String) -> Sequence` | Arpeggiate (up/down/updown) |
| `scaleNotes` | `(String) -> String[]` | Scale note names |
| `resolveNumeral` | `(String, String) -> Chord` | Roman numeral to chord |
| `getSections` | `(Song) -> String[]` | Section names |
| `sectionSequences` | `(Section) -> String[]` | Sequence names |

## Transform Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `transpose` | `(Sequence, Semitone) -> Sequence` | Shift by semitones |
| `transpose` | `(Sequence, Cent) -> Sequence` | Shift by cents |
| `invert` | `(Sequence) -> Sequence` | Mirror intervals |
| `retrograde` | `(Sequence) -> Sequence` | Reverse note order |
| `augment` | `(Sequence) -> Sequence` | Double durations |
| `diminish` | `(Sequence) -> Sequence` | Halve durations |
| `up` | `(Sequence, Int) -> Sequence` | Shift up N octaves |
| `down` | `(Sequence, Int) -> Sequence` | Shift down N octaves |
| `repeat` | `(Sequence, Int) -> Sequence` | Repeat N times |
| `repeat` | `(Sequence, Int, Semitone) -> Sequence` | Repeat with transposition |
| `concat` | `(Sequence, Sequence) -> Sequence` | Join sequences |
| `crescendo` | `(Sequence, Double, Double) -> Sequence` | Rising velocity |
| `decrescendo` | `(Sequence, Double, Double) -> Sequence` | Falling velocity |
| `swell` | `(Sequence, Double, Double) -> Sequence` | Rise-then-fall velocity |
| `ritardando` | `(Sequence, Double) -> Sequence` | Gradual slowdown |
| `accelerando` | `(Sequence, Double) -> Sequence` | Gradual speedup |
| `fermata` | `(Sequence, Int) -> Sequence` | Hold note at index |
| `humanize` | `(Sequence, Double) -> Sequence` | Random velocity variation |
| `trill` | `(Sequence, Semitone) -> Sequence` | Rapid alternation |
| `tremolo` | `(Sequence, Int) -> Sequence` | Rapid repetition |

## Musical Notation Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `createMusicalNote` | `(Note, NoteValue) -> MusicalNote` | Note with duration |
| `createRest` | `(NoteValue) -> MusicalNote` | Rest with duration |
| `createTimeSignature` | `(Int, Int) -> TimeSignature` | Time signature |
| `createMusicalBar` | `(MusicalNote[], TimeSignature) -> Bar` | Bar from notes |
| `createSequence` | `() -> Sequence` | Empty sequence |
| `addBarToSequence` | `(Sequence, Bar) -> Sequence` | Add bar |
| `renderSequence` | `(Sequence, String, Int, Double) -> Voice[]` | Render sequence |
| `noteToFrequency` | `(Note) -> Double` | Note to Hz |
| `euclidean` | `(Int, Int, Note) -> Sequence` | Euclidean rhythm |
| `renderSong` | `(Song, String) -> Buffer` | Render full song |

## Bar Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `createBar` | `() -> Bar` | Empty bar |
| `createBarWithNote` | `(Note) -> Bar` | Bar with one note |
| `createBarFromNotes` | `(Note[]) -> Bar` | Bar from array |
| `addNoteToBar` | `(Bar, Note) -> Void` | Add note to bar |
| `getNoteFromBar` | `(Bar, Int) -> Note` | Get note at index |
| `barLength` | `(Bar) -> Int` | Note count |
| `setTimeSignature` | `(Bar, Int, Int) -> Void` | Set time signature |
| `getTimeSignature` | `(Bar) -> String` | Get time signature string |

## See Also

- [Imports and Modules](Imports-and-Modules.md) - How to import modules
- [Collections](Collections.md) - Detailed collection usage
- [Audio and Synthesis](Audio-and-Synthesis.md) - Audio function details
- [Effects](Effects.md) - Effect function details
