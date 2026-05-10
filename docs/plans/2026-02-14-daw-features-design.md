# Flow DAW Features Design

## Vision

Make Flow a genuinely open, extensible music language that programmer-musicians can use to compose Jazz, Classical, and Pop — all in the same program. The language should feel musical, not just programmatic. Composition should be concise, feedback should be instant, and the creative tools should reward exploration.

Flow's mission is to make music production less gated by proprietary DAW software. The more open and extensible the language, the more community potential — users sharing `.flow` modules with custom transforms, scales, voicings, and generative algorithms.

## Target User

Programmer-musicians: developers who make music and want code-level control. Comfortable with text, want precision, but expect the language to think in musical structures.

## Primary Focus

Arrangement and composition — layering tracks, building song structure, expressing musical ideas concisely. Sound design and live performance are secondary concerns.

## Success Criteria

Can we make Jazz, Classical, and Pop all in the same program? Jazz needs swing, complex harmony, and improvisation-like generative tools. Classical needs counterpoint, dynamic time signatures, and expressive notation. Pop needs repetitive structure, hooks, and groove.

---

## 1. Musical Context Stack

### Problem

Flow has a programming context (variables, call stack) but no musical context. Notes exist in isolation — every musical property (time signature, tempo, swing, key) must be specified explicitly on every operation.

### Design

Introduce scoped blocks that push musical state onto the execution context's stack frame, just like variables. Notes and sequences inside a block automatically inherit the active musical context. Nested scopes inherit from parent and can override.

```flow
timesig 4/4 {
    // notes here auto-distribute into 4/4 bars

    timesig 3/4 {
        // waltz section
    }
    // back to 4/4
}

tempo 120 {
    // 120 BPM here

    tempo 80 {
        // slower section
    }
}

swing 60% {
    // jazzy feel applied to all notes in scope
}

key Cmajor {
    // scale-degree shortcuts available
}
```

Blocks compose naturally:

```flow
tempo 120 {
    timesig 4/4 {
        swing 55% {
            // jazz ballad context
        }
    }
}
```

### Implementation Notes

- Each context type (`timesig`, `tempo`, `swing`, `key`) becomes a new statement type in the AST
- The interpreter pushes/pops these onto the `StackFrame`, similar to variable scoping
- Note stream parsing and sequence rendering read the active context to determine bar structure, playback speed, rhythmic displacement, and available scale degrees
- Future addition: `tuning` blocks for microtonal temperaments (equal 24, just intonation, etc.)

---

## 2. Note Streams

### Problem

Building bars note-by-note with function calls is tedious. Writing `createMusicalNote`, `addNoteToBar`, `createMusicalBar` for every note is ceremony that obscures the musical intent.

### Design

A note stream literal uses `| ... |` delimiters. Notes are space-separated. The active time signature determines how notes auto-fit into bars.

**Basic note streams:**

```flow
timesig 4/4 {
    Sequence melody = | C4 D4 E4 F4 | G4 A4 B4 C5 |
}
```

**Auto-fit behavior:**
- 4 notes in 4/4 = each is a quarter note
- 8 notes in 4/4 = each is an eighth note
- 3 notes in 3/4 = each is a quarter note
- Explicit durations override auto-fit

**Duration suffixes:**

| Suffix | Duration |
|--------|----------|
| `w` | Whole |
| `h` | Half |
| `q` | Quarter |
| `e` | Eighth |
| `s` | Sixteenth |
| `t` | Thirty-second |

```flow
| C4w |                    // whole note
| C4h D4h |               // two half notes
| C4q D4q E4q F4q |       // four quarters
| C4e D4e E4e F4e G4e A4e B4e C5e |  // eight eighths
```

**Rests:**

`_` is a rest. Duration follows auto-fit rules, or specify explicitly.

```flow
| C4q _ D4q _ |            // quarter, rest, quarter, rest
| C4h _ |                  // half note, half rest
| _ _ _ C4q |              // three beats rest, pickup note
| _h C4q D4q |             // explicit half rest
```

**Dots and ties:**

```flow
| C4q. E4e F4h |           // dotted quarter + eighth + half = 4 beats
| C4h~ C4h |               // tied half notes across bar line
```

**Simultaneous notes (chords in bracket syntax):**

```flow
| [C4 E4 G4]q [F4 A4 C5]q [G4 B4 D5]h |
```

**Accidentals and microtonal:**

```flow
| C4 D#4 Eb4 F4 |          // sharps and flats
| C##4 Dbb4 |              // double sharp, double flat
| C4+50c D4-25c |          // cent offsets for microtonal
```

**What this compiles to:** The parser transforms a note stream into a `Sequence` of `Bar` objects using the active musical context. Changing the time signature re-distributes the notes automatically within that scope.

---

## 3. Pattern Transforms

### Problem

Composers constantly derive new material from existing ideas — transposition, inversion, retrograde, augmentation. These operations should be first-class, chainable with the flow operator.

### Design

```flow
Sequence melody = | C4 D4 E4 F4 G4 |

melody -> transpose 5st        // up 5 semitones
melody -> transpose 50c        // microtonal: up 50 cents
melody -> invert               // mirror intervals around first note
melody -> retrograde           // reverse note order
melody -> augment              // double all durations
melody -> diminish             // halve all durations
melody -> up 1                 // up one octave
melody -> down 2               // down two octaves
```

**Chaining:**

```flow
Sequence theme = | C4q E4q G4q C5q |

Sequence development = theme
    -> concat (theme -> invert)
    -> concat (theme -> invert -> retrograde)

Sequence modulated = development -> transpose 7st
```

**Repetition with cumulative variation:**

```flow
Sequence riff = | C4e _ E4e G4e |

riff -> repeat 4                        // repeat 4 times
riff -> repeat 4 (transpose 2st)        // +2st each iteration: original, +2, +4, +6
```

**Dynamics:**

```flow
melody -> crescendo -12dB 0dB           // gradually louder
melody -> decrescendo 0dB -18dB         // fade out
melody -> accent [1, 0, 0, 0]           // accent pattern (weights)
```

**Slicing:**

```flow
Sequence phrase = | C4 D4 E4 F4 | G4 A4 B4 C5 |

phrase -> bars 1 1           // first bar only
phrase -> bars 2 2           // second bar only
phrase -> take 4             // first 4 notes, reflow into bars
phrase -> drop 2             // remove first 2 notes, reflow
```

---

## 4. Generative & Probabilistic Features

### Problem

Music benefits from controlled randomness — humanization, variation, exploration. Flow already has `?` (random) and `??` (seeded random). These should extend into note streams and transforms.

### Design Philosophy

`?` is fire-and-forget random, initialized once with state never reset. `??` is auditionable-then-freezable — keep randomizing until you find something you like, then freeze the seed with `??set`. This convention applies consistently everywhere.

**Random note choice:**

```flow
// Equal probability
| (? C4 D4 E4 G4 A4) q |

// Weighted: C4 50%, E4 30%, G4 20%
| (? C4:50 E4:30 G4:20) q |

// Seeded — audition, then freeze
| (?? C4 D4 E4 F4 G4) q |
```

**Random from scales/chords:**

```flow
key Cmajor {
    | (? key) (? key) (? key) (? key) |        // random scale tone
    | (? Cmaj7) (? Cmaj7) (? Cmaj7) (? Cmaj7) | // random chord tone
}
```

**Probabilistic rests — notes that sometimes play:**

```flow
| C4?70 D4?70 E4?70 F4?70 |      // 70% chance each note plays, 30% rest
| C4??70 D4??70 E4??70 F4??70 |   // seeded version
```

**Random transforms:**

```flow
Sequence base = | C4 E4 G4 C5 |

base -> transpose (? -5st 5st)     // random in range
base -> transpose (?? -5st 5st)    // seeded random in range
```

**Euclidean rhythms:**

```flow
Sequence rhythm = euclidean(5, 8, C4)           // 5 hits in 8 steps
Sequence clave = euclidean(3, 8, C4) -> rotate 2 // with rotation
```

**Markov chains:**

```flow
Sequence source = | C4 D4 E4 D4 C4 D4 E4 F4 E4 D4 C4 |

Sequence generated = source -> markov 16       // generate 16 notes
Sequence generated = source -> markov?? 16     // seeded version
```

**Shuffle and permute:**

```flow
Sequence motif = | C4 E4 G4 B4 |

motif -> shuffle         // random reorder (?)
motif -> shuffle??       // seeded reorder
motif -> permute 3       // deterministic permutation index
```

---

## 5. Chords & Harmony

### Problem

Jazz, Classical, and Pop are all harmony-driven. Writing chords as `[C4 E4 G4]` every time is workable but doesn't match how musicians think — in chord symbols and scale degrees.

### Design

**Chord literals expand to `Note[]`:**

```flow
Cmaj  Dm  Edim  Faug                 // triads
Cmaj7  Dm7  G7  Bdim7  Am7b5        // sevenths
C9  Dm11  G13                        // extensions
G7#9  G7b13  C7alt                   // altered
C/E  G/B  Dm7/A                      // slash chords (bass note)
Csus2  Csus4  G7sus4                 // suspended
```

Default octave is 4. Override: `Cmaj:3`, `Dm7:5`.

**Chords in note streams:**

```flow
timesig 4/4 {
    Sequence changes = | Dm7 G7 Cmaj7 Cmaj7 |      // jazz ii-V-I
    Sequence pop = | C G Am F | C G Am F |           // pop progression
}
```

**Voicings:**

```flow
Cmaj7 -> voicing close       // C4 E4 G4 B4 (default, tight)
Cmaj7 -> voicing open        // C3 G3 E4 B4 (spread)
Cmaj7 -> voicing drop2       // C3 B3 E4 G4 (jazz guitar)
Cmaj7 -> voicing drop3       // C3 E3 B3 G4
Cmaj7 -> voicing shell       // C4 E4 B4 (root, 3rd, 7th only)
```

**Roman numeral notation inside key context:**

```flow
key Cmajor {
    Sequence prog = | I IV V I |         // Cmaj Fmaj Gmaj Cmaj
    Sequence jazz = | ii V I vi |        // Dm7 G7 Cmaj7 Am7
}

key Aminor {
    Sequence prog = | i iv v i |         // Am Dm Em Am
}
```

Uppercase = major, lowercase = minor. The `key` context determines root and quality.

**Arpeggiation:**

```flow
Cmaj7 -> arpeggio up          // C4 E4 G4 B4 as sequential notes
Cmaj7 -> arpeggio down        // B4 G4 E4 C4
Cmaj7 -> arpeggio updown      // C4 E4 G4 B4 G4 E4

| Dm7 G7 Cmaj7 | -> arpeggio up
// expands each chord into arpeggiated notes
```

**Chord transforms:**

```flow
Sequence changes = | Dm7 G7 Cmaj7 |

changes -> transpose 5st              // Gm7 C7 Fmaj7
changes -> parallel minor             // Dm7 G7 Cm7
changes -> retrograde                 // Cmaj7 G7 Dm7
```

---

## 6. Song Structure

### Problem

Music has form — verse, chorus, bridge, AABA, 12-bar blues. Flow currently has no concept of structure above the sequence/track level.

### Design

**Named sections:**

```flow
section intro {
    timesig 4/4 {
        tempo 120 {
            Sequence pads = | Cmaj7 | Cmaj7 | Fmaj7 | Fmaj7 |
            Sequence bass = | C3q _ _ _ | C3q _ _ _ | F3q _ _ _ | F3q _ _ _ |
        }
    }
}

section verse {
    timesig 4/4 {
        tempo 120 {
            Sequence melody = | C4 D4 E4 G4 | A4 G4 E4 D4 |
            Sequence chords = | Am F | C G |
        }
    }
}

section chorus {
    timesig 4/4 {
        tempo 125 {
            Sequence melody = | C5h G4h | A4h E4h | F4 G4 A4 C5 | G4w |
            Sequence chords = | C G | Am F | Dm G | C C |
        }
    }
}
```

Each sequence within a section is a separate voice/track. They layer together when rendered.

**Arrangement:**

```flow
Song track = [intro verse chorus verse chorus chorus]
```

One line describes the entire song form.

**Repetition and variation:**

```flow
Song track = [intro verse*2 chorus verse chorus*2 outro]

// Section inheritance — copy parent, override specifics
section chorus_final : chorus {
    tempo 130 {}
}

Song track = [intro verse chorus verse chorus chorus_final]
```

**Standard form templates:**

```flow
Song blues = blues12 Cmajor {
    bar 9 = | V |                // turnaround variation
}

Song standard = aaba {
    section a { ... }
    section b { ... }
}

Song folk = strophic verse 4     // verse repeated 4 times
```

**Rendering:**

```flow
Song mysong = [intro verse chorus verse chorus outro]

Buffer result = mysong -> render
result -> exportWav "mysong.wav"

// Or in one chain
[intro verse chorus verse chorus outro] -> render -> exportWav "mysong.wav"
```

---

## 7. Lambda Functions

### Problem

The `fn` syntax is declared in `notation.flow` but not implemented in the parser/interpreter. This blocks `@notation` from loading and prevents higher-order composition — passing transforms as values, writing custom map/filter over musical material.

### Design

```flow
// Single parameter
fn Note pitch => (createMusicalNote pitch QUARTER)

// Multiple parameters
fn Note pitch, Int octave => (transpose pitch (octave * 12)st)

// No parameters
fn => (? C4 E4 G4)

// Assigned to a typed variable
(Note => Note) transpose5 = fn Note n => (n -> transpose 5st)
```

Type signature: `(Note => Note)` for single param, `(Note, Int => Note)` for multiple.

**With collections:**

```flow
Note[] notes = (list C4 D4 E4 F4 G4)

Note[] raised = notes -> map (fn Note n => (n -> transpose 5st))
Note[] high = notes -> filter (fn Note n => (gt n C4))
```

**With sequences:**

```flow
Sequence melody = | C4 D4 E4 F4 | G4 A4 B4 C5 |

melody -> mapNotes (fn Note n => (? n (n -> transpose 3st)))
melody -> mapBars (fn Bar b => (b -> reverse))
```

**As variation engine:**

```flow
Sequence riff = | C4 E4 G4 C5 |

riff -> repeat 4 (fn Sequence s, Int i => (s -> transpose (i * 2)st))
// iteration 0: original, 1: +2st, 2: +4st, 3: +6st
```

**Short form for single expressions:**

```flow
notes -> map (fn Note n => n -> transpose 5st)
```

### Why This Matters

Lambdas turn Flow from a language where you call built-in transforms into one where you define your own. Users can share custom transforms, scales, voicings, and generative algorithms as `.flow` modules — the foundation of a community ecosystem.

---

## 8. Playback & Rendering

### Problem

Flow is currently write-render-export-listen. You have to leave the language to hear anything. That feedback loop kills creativity.

### Design

**Core primitive:**

```flow
Buffer tone = (createSineTone 1.0 440.0 0.5)
tone -> play
```

`play` sends a buffer to the system audio device. Works in scripts and the REPL.

**Overloaded for all musical types:**

```flow
melody -> play          // Sequence
intro -> play           // Section
[intro verse chorus] -> play  // Song
Cmaj7 -> play           // Chord
C4 -> play              // Single note
```

Everything that can become audio can be played.

**REPL as musical sketchpad:**

```
flow> timesig 4/4 { | C4 D4 E4 F4 | } -> play
  Playing... (1.6s)

flow> ??set 42
flow> | (?? C4 D4 E4 G4 A4) (?? C4 D4 E4 G4 A4) | -> play
  Playing... (0.8s)
// Like it — seed 42 is locked in.
```

**Playback control:**

```flow
melody -> playAt 140           // preview at different tempo
melody -> play bars 3 8        // play bars 3 through 8
melody -> loop                 // repeat until Ctrl+C
melody -> loop 4               // repeat 4 times
```

**Preview mode:**

```flow
mysong -> preview              // mono, 22050Hz, no dithering — fast
```

**Live reload:**

```
$ flow --watch mysong.flow
  Watching mysong.flow...
  Playing...
  [save file]
  Change detected, re-rendering...
  Playing...
```

**Audio device selection:**

```flow
(audioDevices) -> print
(setAudioDevice "Built-in Output")
```

```
$ flow --device "Headphones" mysong.flow
```

### Audio Backend Architecture

An abstraction layer (`IAudioBackend`) with platform-specific implementations:

```
Flow built-in functions (play, loop, preview)
    |
    v
IAudioBackend (interface)
    |
    +-- PipeWireBackend    (Linux, primary)
    +-- PulseAudioBackend  (Linux, fallback)
    +-- CoreAudioBackend   (macOS, future)
    +-- WasapiBackend      (Windows, future)
```

PipeWire is the primary Linux backend — it's the modern default and speaks the PulseAudio protocol. True PulseAudio as fallback for older systems. macOS and Windows backends slot in later without changing any Flow code.

---

## 9. Effects & Processing

### Problem

Dry oscillator tones don't sound like music. A minimal effects set is needed to make rendered output sound musical — but this is not a full effects suite. Arrangement and composition are the focus, not sound design.

### Design

**Reverb:**

```flow
buffer -> reverb 0.3
buffer -> reverb room:0.5 damping:0.7 mix:0.3
```

**Filters:**

```flow
buffer -> lowpass 800Hz
buffer -> highpass 200Hz
buffer -> bandpass 400Hz 1200Hz
```

**Compression:**

```flow
buffer -> compress threshold:-20dB ratio:4
```

**Gain:**

```flow
buffer -> gain -6dB
buffer -> gain 3dB
```

**Delay:**

```flow
buffer -> delay 375ms feedback:0.4 mix:0.3

tempo 120 {
    buffer -> delay 1/8 feedback:0.3 mix:0.25    // tempo-synced
}
```

**Effect chains (flow operator):**

```flow
melody -> render
    -> highpass 80Hz
    -> compress threshold:-18dB ratio:3
    -> reverb 0.25
    -> gain -3dB
    -> exportWav "final.wav"
```

**Per-track effects in sections:**

```flow
section verse {
    Sequence melody = | A4 C5 E5 D5 | C5 A4 G4 E4 |
        -> effects (reverb 0.3) (delay 1/8 feedback:0.2)

    Sequence bass = | E2q _ G2q _ |
        -> effects (compress threshold:-15dB ratio:6) (lowpass 400Hz)
}
```

### Deliberately Excluded (for now)

- Parametric EQ — lowpass/highpass/bandpass covers most needs
- Distortion/saturation — sound design, not composition
- Chorus/flanger/phaser — nice-to-have modulation effects
- Sidechain compression — advanced routing, defer to later

More effects can be added as community-contributed `.flow` modules or future built-ins.

---

## Implementation Priority

Suggested order based on dependencies and impact:

| Priority | Feature | Rationale |
|----------|---------|-----------|
| 1 | Musical Context Stack | Foundation — everything else depends on scoped musical state |
| 2 | Note Streams | Highest-impact UX improvement, depends on context stack |
| 3 | Lambda Functions | Unblocks `@notation`, enables higher-order composition |
| 4 | Pattern Transforms | Combinatorial power, builds on note streams |
| 5 | Chords & Harmony | Musical expressiveness, builds on note streams and key context |
| 6 | Playback (IAudioBackend) | Instant feedback, biggest infrastructure lift |
| 7 | Song Structure | Arrangement-level composition, builds on sections and sequences |
| 8 | Generative Features | Surprise and discovery, builds on `?`/`??` and note streams |
| 9 | Effects & Processing | Polish — makes output sound musical |
