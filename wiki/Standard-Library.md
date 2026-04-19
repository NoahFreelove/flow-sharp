# Standard Library

Flow's standard library provides all built-in functions. Most programs need `use "@std"` at minimum.

## Why `use "@std"` Matters

Without `use "@std"`, you only have raw language syntax (variables, operators, `proc`, note streams, context blocks). Essential functions like `print`, `str`, `concat`, `add`, `sub`, `list`, `map`, `filter`, and all collection operations require the standard library.

```flow
use "@std"
(print "now print works")
```

## Modules

| Module | Import | Provides |
|--------|--------|----------|
| `@std` | `use "@std"` | Core + collections + bars (also auto-imports `@collections` and `@bars`) |
| `@collections` | `use "@collections"` | List operations (head, tail, map, filter, etc.) |
| `@audio` | `use "@audio"` | Audio creation, effects, playback, synthesis, MIDI/WAV I/O |
| `@notation` | `use "@notation"` | Musical notation (note durations, rests, time signatures) |
| `@bars` | `use "@bars"` | Bar/note operations |
| `@composition` | `use "@composition"` | Timeline, voice, track, polyrhythm, variation |

`@std` automatically imports `@collections` and `@bars`, so you rarely need to import them separately.

## Core Functions

### I/O

| Function | Signature | Description |
|----------|-----------|-------------|
| `print` | `(String) -> Void` | Print string with newline |

### String Operations

| Function | Signature | Description |
|----------|-----------|-------------|
| `str` | `(T) -> String` | Convert any value to string |
| `len` | `(String) -> Int` | String length |
| `concat` | `(String, String) -> String` | Concatenate strings |

`str` has overloads for Int, Float, Double, String, Bool, Note, Bar, Semitone, Cent, Millisecond, Second, Decibel, Array, Sequence, Chord, Section, Song.

### Arithmetic

| Function | Signature | Description |
|----------|-----------|-------------|
| `add` | `(Int, Int) -> Int` (plus Float / Double overloads) | Addition |
| `sub` | `(Int, Int) -> Int` | Subtraction |
| `mul` | `(Int, Int) -> Int` | Multiplication |
| `div` | `(Int, Int) -> Int` | Integer division (and Float/Double overloads) |
| `abs` | `(Int) -> Int` / `(Double) -> Double` | Absolute value |
| `min` | `(Int, Int) -> Int` / `(Double, Double) -> Double` | Minimum |
| `max` | `(Int, Int) -> Int` / `(Double, Double) -> Double` | Maximum |

Binary operators `+`, `-`, `*`, `/` also work for arithmetic expressions.

### Math

| Function | Signature | Description |
|----------|-----------|-------------|
| `sin` | `(Double) -> Double` | Sine |
| `cos` | `(Double) -> Double` | Cosine |
| `tan` | `(Double) -> Double` | Tangent |
| `sqrt` | `(Double) -> Double` | Square root |
| `floor` | `(Double) -> Int` | Round down |
| `ceil` | `(Double) -> Int` | Round up |
| `round` | `(Double) -> Int` | Round to nearest |
| `pow` | `(Double, Double) -> Double` | Exponentiation |
| `log` | `(Double) -> Double` | Natural log |
| `pi` | `() -> Double` | π |
| `tau` | `() -> Double` | 2π |

### Comparison

| Function | Signature | Description |
|----------|-----------|-------------|
| `equals` | `(T, T) -> Bool` | Loose equality (with type coercion) |
| `sequals` | `(T, T) -> Bool` | Strict equality (types must match) |
| `lt` | `(T, T) -> Bool` | Less than |
| `gt` | `(T, T) -> Bool` | Greater than |
| `lte` | `(T, T) -> Bool` | Less than or equal |
| `gte` | `(T, T) -> Bool` | Greater than or equal |

### Logical

| Function | Signature | Description |
|----------|-----------|-------------|
| `and` | `(Bool, Bool) -> Bool` | Eager AND |
| `and` | `(Lazy<Bool>, Lazy<Bool>) -> Bool` | Short-circuit AND |
| `or` | `(Bool, Bool) -> Bool` | Eager OR |
| `or` | `(Lazy<Bool>, Lazy<Bool>) -> Bool` | Short-circuit OR |
| `not` | `(Bool) -> Bool` | Logical NOT |
| `if` | `(Bool, Lazy<T>, Lazy<T>) -> T` | Conditional (lazy branches) |

### Type Conversion

| Function | Signature | Description |
|----------|-----------|-------------|
| `intToDouble` | `(Int) -> Double` | Int → Double |
| `doubleToInt` | `(Double) -> Int` | Double → Int (truncates) |
| `stringToInt` | `(String) -> Int` | Parse Int (returns Void on failure) |
| `stringToDouble` | `(String) -> Double` | Parse Double (returns Void on failure) |

### Lazy and Control

| Function | Signature | Description |
|----------|-----------|-------------|
| `eval` | `(Lazy<T>) -> T` | Force evaluation of a lazy value |
| `setMaxIterations` | `(Int) -> Void` | Cap loop iterations (safety limit) |

### Random

| Function | Signature | Description |
|----------|-----------|-------------|
| `?` | `() -> Float` | Random float [0, 1) |
| `??` | `() -> Float` | Seeded random float [0, 1) |
| `??set` | `(Int) -> Void` | Set the seeded RNG state |
| `??reset` | `() -> Void` | Reset the seeded RNG |

## Collection Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `list` | `(...T) -> T[]` | Create array from varargs |
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
| `reduce` | `(T[], U, (U, T) => U) -> U` | Fold with accumulator |
| `each` | `(T[], T => Void) -> Void` | Apply for side effects |
| `range` | `(Int, Int) -> Int[]` | Integer range `[lo, hi)` |
| `zip` | `(T[], U[]) -> [T, U][]` | Pair elements |

See [Collections](Collections.md) for details.

## Audio Functions

### Buffer Operations

| Function | Signature | Description |
|----------|-----------|-------------|
| `createBuffer` | `(Int, Int, Int) -> Buffer` | Create buffer (frames, channels, sample rate) |
| `getFrames` / `getChannels` / `getSampleRate` | `(Buffer) -> Int` | Buffer metadata |
| `getSample` | `(Buffer, Int, Int) -> Float` | Get sample (frame, channel) |
| `setSample` | `(Buffer, Int, Int, Double) -> Void` | Set sample |
| `fillBuffer` | `(Buffer, Double) -> Void` | Fill with constant |
| `copyBuffer` | `(Buffer) -> Buffer` | Deep copy |
| `sliceBuffer` | `(Buffer, Int, Int) -> Buffer` | Extract slice |
| `appendBuffers` | `(Buffer, Buffer) -> Buffer` | Concatenate end-to-end |
| `scaleBuffer` | `(Buffer, Double) -> Buffer` | Multiply all samples by gain |
| `mix` | `(Buffer, Buffer) -> Buffer` | Mix at unity gain |
| `mixBuffers` | `(Buffer, Buffer, Double, Double) -> Buffer` | Mix with per-source gains |
| `fadeIn` | `(Buffer, Double) -> Buffer` | Linear fade-in (seconds) |
| `fadeOut` | `(Buffer, Double) -> Buffer` | Linear fade-out (seconds) |

### Signal Generation

| Function | Signature | Description |
|----------|-----------|-------------|
| `createOscillatorState` | `(Double, Int) -> OscillatorState` | Oscillator state |
| `resetPhase` | `(OscillatorState) -> Void` | Reset phase |
| `generateSine` / `generateSaw` / `generateSquare` / `generateTriangle` | `(Buffer, OscillatorState, Double) -> Void` | Fill buffer with waveform |
| `createSineTone` / `createSawTone` / `createSquareTone` / `createTriangleTone` | `(Double, Double, Double) -> Buffer` | Ready-to-use tone (duration, frequency, amplitude) |
| `oscillator` | `(String, Array)` / `(String, Function)` / `(String, Function, Int)` | Register a custom wavetable oscillator |

### Envelopes

| Function | Signature | Description |
|----------|-----------|-------------|
| `createAR` | `(Double, Double, Int) -> Envelope` | Attack-Release envelope |
| `createADSR` | `(Double, Double, Double, Double, Int) -> Envelope` | ADSR envelope |
| `applyEnvelope` | `(Buffer, Envelope) -> Buffer` | Apply envelope shape |

### Effects

| Function | Signature | Description |
|----------|-----------|-------------|
| `reverb` | `(Buffer, Double) -> Buffer` / `(Buffer, Double, Double, Double) -> Buffer` | Reverb |
| `lowpass` / `highpass` | `(Buffer, Double) -> Buffer` | Filters (cutoff Hz) |
| `bandpass` | `(Buffer, Double, Double) -> Buffer` | Band-pass filter |
| `compress` | `(Buffer, Double, Double) -> Buffer` / `(Buffer, Double, Double, Double, Double) -> Buffer` | Compressor |
| `sidechain` | `(Buffer, Buffer, Double, Double) -> Buffer` / `(… , Double, Double)` | Sidechain compressor |
| `delay` | `(Buffer, Double, Double, Double) -> Buffer` | Feedback delay |
| `gain` | `(Buffer, Double) -> Buffer` | Gain (dB) |
| `pan` | `(Buffer, Double) -> Buffer` | Stereo pan (-1 .. +1) |

### Playback

| Function | Signature | Description |
|----------|-----------|-------------|
| `play` | `(Buffer) -> Void` / `(Sequence) -> Void` | Play (blocking) |
| `stream` | `(Buffer) -> Void` / `(Sequence) -> Void` | Play asynchronously |
| `loop` | `(Buffer) -> Void` / `(Buffer, Int) -> Void` | Loop indefinitely / N times |
| `preview` | `(Buffer) -> Void` | Low-quality preview |
| `stop` | `() -> Void` | Stop playback |
| `audioDevices` | `() -> String[]` | List devices |
| `setAudioDevice` | `(String) -> Bool` | Set output device |
| `isAudioAvailable` | `() -> Bool` | Check backend |

### WAV / MIDI I/O

| Function | Signature | Description |
|----------|-----------|-------------|
| `exportWav` | `(Buffer, String) -> Void` / `(Buffer, String, Int) -> Void` | Export WAV (optional bit depth) |
| `writeWav` | `(String, Buffer) -> Void` / `(String, Buffer, Int) -> Void` | Path-first WAV export |
| `loadWav` | `(String) -> Buffer` | Load WAV file |
| `writeMidi` | `(String, Song) -> Void` | Export Song to Standard MIDI File |

### Timeline and Voice

| Function | Signature | Description |
|----------|-----------|-------------|
| `setBPM` | `(Double) -> Void` | Set global BPM |
| `getBPM` | `() -> Double` | Get current BPM |
| `beatsToFrames` | `(Double, Int) -> Int` | Beats → frames |
| `framesToBeats` | `(Int, Int) -> Double` | Frames → beats |
| `createVoice` | `(Buffer, Double) -> Voice` | Voice at beat offset |
| `setVoiceGain` / `setVoicePan` / `setVoiceOffset` | `(Voice, Double) -> Void` | Voice parameters |
| `createTrack` | `(Int, Int) -> Track` | Empty track |
| `addVoice` | `(Track, Voice) -> Void` | Add voice |
| `setTrackGain` / `setTrackPan` / `setTrackOffset` | `(Track, Double) -> Void` | Track parameters |
| `renderTrack` | `(Track, Double) -> Buffer` | Render track over N beats |
| `setMaxVoices` | `(Int) -> Void` | Set polyphonic voice pool size |

### Vocalization

| Function | Signature | Description |
|----------|-----------|-------------|
| `sing` | `(String, Note, Double) -> Buffer` | Formant-synthesized vowel / syllable |
| `tts` | `(String) -> Buffer` | External TTS → buffer |
| `setTtsCommand` | `(String) -> Void` | Configure TTS command template |

### Visualization

| Function | Signature | Description |
|----------|-----------|-------------|
| `visualize` | `(Sequence) -> Void` | ASCII piano-roll |
| `visualize` | `(Buffer) -> Void` | ASCII waveform |

## Harmony Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `chordNotes` | `(Chord) -> String[]` | Notes in chord |
| `chordRoot` | `(Chord) -> String` | Root note |
| `chordQuality` | `(Chord) -> String` | Quality string |
| `arpeggio` | `(Chord, String) -> Sequence` | Arpeggiate (`"up"`, `"down"`, `"updown"`) |
| `scaleNotes` | `(String) -> String[]` | Scale note names |
| `resolveNumeral` | `(String, String) -> Chord` | Roman numeral → chord |
| `getSections` | `(Song) -> String[]` | Section names |
| `sectionSequences` | `(Section) -> String[]` | Sequence names within a section |

## Transform Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `transpose` | `(Sequence, Semitone) -> Sequence` / `(Sequence, Cent) -> Sequence` | Shift pitch |
| `invert` | `(Sequence) -> Sequence` | Mirror intervals |
| `retrograde` | `(Sequence) -> Sequence` | Reverse note order |
| `augment` | `(Sequence) -> Sequence` | Double durations |
| `diminish` | `(Sequence) -> Sequence` | Halve durations |
| `up` / `down` | `(Sequence, Int) -> Sequence` | Octave shift |
| `repeat` | `(Sequence, Int) -> Sequence` / `(Sequence, Int, Semitone) -> Sequence` | Repeat (optionally with transposition) |
| `concat` | `(Sequence, Sequence) -> Sequence` | Join sequences |
| `crescendo` | `(Sequence, Double, Double) -> Sequence` | Rising velocity |
| `decrescendo` | `(Sequence, Double, Double) -> Sequence` | Falling velocity |
| `swell` | `(Sequence, Double, Double) -> Sequence` | Rise-then-fall |
| `ritardando` / `accelerando` | `(Sequence, Double) -> Sequence` | Slow-/speed-up feel |
| `fermata` | `(Sequence, Int) -> Sequence` | Hold note at index |
| `humanize` | `(Sequence, Double) -> Sequence` | Random velocity variation |
| `trill` | `(Sequence, Semitone) -> Sequence` | Rapid alternation |
| `tremolo` | `(Sequence, Int) -> Sequence` | Rapid repetition |

## Composition Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `euclidean` | `(Int, Int, Note) -> Sequence` | Euclidean rhythm (hits, steps, pitch) |
| `vary` | `(Sequence, Double)` + typed / seeded / diatonic overloads | Stochastic mutation |
| `polyrhythm` | `(Sequence, Sequence) -> Buffer` / `(…, Int) -> Buffer` | Overlay sequences with different time signatures |
| `tempoRamp` | `(Sequence, Double, Double) -> Buffer` / `(…, String) -> Buffer` | Render with interpolated tempo |
| `renderSong` | `(Song, String) -> Buffer` / `(Song, Function) -> Buffer` | Render song with synth name or lambda instrument |
| `renderSequenceToVoices` | `(Sequence, String, Int, Double) -> Voice[]` | Render sequence to voices |
| `renderBarToVoices` | `(Bar, String, Int, Double) -> Voice[]` | Render bar to voices |
| `renderBarAtBeat` | `(Bar, Double, String, Int, Double) -> Voice[]` | Render bar at beat offset |
| `renderBarAtTime` | `(Bar, Double, String, Int, Double) -> Voice[]` | Render bar at time offset |

## Musical Notation Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `createMusicalNote` | `(Note, NoteValue) -> MusicalNote` | Note with duration |
| `createRest` | `(NoteValue) -> MusicalNote` | Rest with duration |
| `createTimeSignature` | `(Int, Int) -> TimeSignature` | Time signature |
| `createMusicalBar` | `(MusicalNote[], TimeSignature) -> Bar` | Bar from notes |
| `createEmptyMusicalBar` | `(TimeSignature) -> Bar` | Empty bar |
| `createSequence` | `() -> Sequence` | Empty sequence |
| `addBarToSequence` | `(Sequence, Bar) -> Sequence` | Add bar to sequence |
| `noteToFrequency` | `(Note) -> Double` | Note → Hz (A4 = 440) |
| `noteValueToBeats` | `(NoteValue, Int) -> Double` | Duration → beats |
| `validateBarDuration` | `(Bar, TimeSignature) -> Bool` | Check bar fits time signature |
| `getRemainingBeats` | `(Bar) -> Double` | Remaining capacity |
| `wouldFit` | `(Bar, MusicalNote) -> Bool` | Would note fit? |

## Bar Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `createBar` | `() -> Bar` | Empty bar |
| `createBarWithNote` | `(Note) -> Bar` | Bar with one note |
| `createBarFromNotes` | `(Note[]) -> Bar` | Bar from array |
| `addNoteToBar` | `(Bar, Note) -> Void` | Append note |
| `tryAddNoteToBar` | `(Bar, MusicalNote) -> Bool` | Try append |
| `getNoteFromBar` | `(Bar, Int) -> Note` | Note at index |
| `barLength` | `(Bar) -> Int` | Note count |
| `setTimeSignature` | `(Bar, Int, Int) -> Void` | Set time signature |
| `getTimeSignature` | `(Bar) -> String` | Get time signature string |

## Song Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `createSong` | `(String) -> Song` | Empty song |
| `addBarToSong` | `(Song, String) -> Void` / `(Song, String, Int) -> Void` / `(Song, Sequence) -> Void` | Add section reference or ad-hoc sequence |

## See Also

- [Imports and Modules](Imports-and-Modules.md) - How to import
- [Collections](Collections.md) - Detailed collection usage
- [Audio and Synthesis](Audio-and-Synthesis.md) - Audio details
- [Effects](Effects.md) - Effect details
- [Pattern Transforms](Pattern-Transforms.md) - Sequence transforms
- [Generative Music](Generative.md) - `vary`, `euclidean`, random
- [Voices and Tracks](Voices-and-Tracks.md) - Timeline API
- [Vocalization](Vocalization.md) - `sing` and `tts`
- [Visualization](Visualization.md) - `visualize`
