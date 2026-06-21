# Examples

Complete, runnable Flow programs demonstrating various features. The Flow snippets on this page have been verified against the current desktop engine build. If you find one that has regressed, please file an issue.

For longer chapter-style tutorials, see the `examples/` directory in the repo (linked at the bottom of this page).

## Table of Contents

- [Core Language](#core-language)
  - [Hello World](#hello-world)
  - [String Interpolation](#string-interpolation)
  - [For Loop: Summing a Range](#for-loop-summing-a-range)
  - [While Loop with `break`](#while-loop-with-break)
  - [Variables and Arithmetic](#variables-and-arithmetic)
  - [Functions and Lambdas](#functions-and-lambdas)
  - [Match Expressions](#match-expressions)
  - [Tuples and `~>` Unpack](#tuples-and--unpack)
  - [Dicts](#dicts)
  - [Collections and Functional Programming](#collections-and-functional-programming)
- [Composition Basics](#composition-basics)
  - [Simple Melody](#simple-melody)
  - [Chord Progression](#chord-progression)
  - [Full Song with Sections](#full-song-with-sections)
  - [Expressive Piano Piece](#expressive-piano-piece)
  - [Voice-Block Polyphony](#voice-block-polyphony)
  - [Parameterized Sections](#parameterized-sections)
- [Transforms](#transforms)
  - [Pattern Transforms](#pattern-transforms)
  - [Tidal-Style Pattern Combinators](#tidal-style-pattern-combinators)
- [Generative](#generative)
  - [Random Choice + Euclidean](#random-choice--euclidean)
  - [Markov Chains](#markov-chains)
  - [L-Systems](#l-systems)
  - [Chord-Aware Improvisation with `jam`](#chord-aware-improvisation-with-jam)
- [Audio](#audio)
  - [Effect Processing Chain](#effect-processing-chain)
  - [Granular Synthesis](#granular-synthesis)
  - [Time-Stretch and Pitch-Shift](#time-stretch-and-pitch-shift)
  - [Audio Synthesis from Scratch](#audio-synthesis-from-scratch)
  - [Multiple Synthesizers](#multiple-synthesizers)
  - [Chord Progression with Voice Leading](#chord-progression-with-voice-leading)
  - [Loading a WAV Sample](#loading-a-wav-sample)
  - [Vocalization](#vocalization)
- [Microtonal](#microtonal)
  - [Just Intonation via Pragma](#just-intonation-via-pragma)
  - [Scala `.scl` Tuning](#scala-scl-tuning)
- [Notation IO](#notation-io)
  - [MusicXML Export](#musicxml-export)
  - [LilyPond Export](#lilypond-export)
  - [ABC Import](#abc-import)
  - [MML Import](#mml-import)
- [Misc](#misc)
  - [MIDI Export](#midi-export)
  - [Waltz in 3/4](#waltz-in-34)
  - [Ornaments and Expression](#ornaments-and-expression)
  - [SFZ Orchestral Sampler](#sfz-orchestral-sampler)
- [Where to Find More](#where-to-find-more)

---

## Core Language

### Hello World

```flow
use "@std"

(print "Hello, Flow!")
```

### String Interpolation

```flow
use "@std"

String name = "Flow"
Int bpm = 120
String keyName = "Cmajor"
(print $"playing at {bpm} bpm in {keyName} on {name}")
```

### For Loop: Summing a Range

```flow
use "@std"

Int total = 0
for Int n in (range 1 11) {
    total = (add total n)
}
(print $"sum 1..10 = {total}")    Note: 55
```

### While Loop with `break`

```flow
use "@std"

Note: Encode the exit condition in the while guard — break is not valid inside lazy().
Int i = 0
while (lt i 5) {
    i = (add i 1)
}
(print $"stopped at {i}")
```

### Variables and Arithmetic

```flow
use "@std"

Int x = 5
Int y = 10
Int sum = (add x y)
(print (concat "Sum: " (str sum)))

Int a = 1; Int b = 2; Int c = 3
(print (str a)); (print (str b)); (print (str c))

Note: Reassignment
Int counter = 0
counter = (add counter 1)
counter = (add counter 1)
(print (concat "Counter: " (str counter)))
```

Flow has NO infix arithmetic — `x + y` is a parse error. Use `(add)`, `(sub)`, `(mul)`, `(div)`, `(idiv)`, `(neg)`, `(concat)`.

### Functions and Lambdas

```flow
use "@std"

Note: Procedure with implicit return
proc double(Int: x)
    (mul x 2)
end proc

(print (str (double 7)))  Note: 14

Note: Lambda functions
Function tripler = fn Int n => (mul n 3)
(print (str (tripler 4)))  Note: 12

Note: Function type annotation
(Int, Int => Int) adder = fn Int a, Int b => (add a b)
(print (str (adder 3 4)))  Note: 7

Note: Chaining with flow operator
Int result = 5 -> double -> tripler
(print (str result))  Note: 30
```

### Match Expressions

```flow
use "@std"

proc describe(Int: n)
    (match n
       | 0 => "zero"
       | x when (gt x 0) => "positive"
       | _ => "negative")
end proc

(print (describe 5))       Note: positive
(print (describe 0))       Note: zero
(print (describe -3))      Note: negative

Note: Music-aware patterns — chord literals
key Cmajor {
    String role = (match (chord "G")
                     | V => "dominant"
                     | I => "tonic"
                     | _ => "other")
    (print role)           Note: dominant
}
```

Patterns: literal (`1`, `"hello"`, `Cmaj7`, `V7`), wildcard (`_`), binding (`n`), guards (`pat when (predicate)`). Add `enable matchExhaustive;` at the top of the file to error on non-exhaustive arms.

### Tuples and `~>` Unpack

```flow
use "@std"

Note: Tuples hold positionally-typed values.
Tuple<<Int, Int>> pair = <<10, 20>>
Int first = pair@0
Int second = pair@1
(print $"pair = <<{first}, {second}>>")

Note: Destructuring assignment.
<<Int a, Int b>> = pair
(print $"a={a}, b={b}")

Note: ~> unpacks a tuple into a multi-arg call.
proc add3(Int: x, Int: y, Int: z)
    (add (add x y) z)
end proc

Int sum = <<1, 2, 3>> ~> add3       Note: lowers to (add3 1 2 3)
(print $"sum = {sum}")

Note: On a non-tuple LHS, ~> falls through to -> semantics.
Note: double proc must be defined before use.
proc double(Int: x)
    (mul x 2)
end proc

Int chained = 5 ~> double           Note: same as 5 -> double
(print $"chained = {chained}")
```

### Dicts

```flow
use "@std"

Note: Generic Dict<K, V> with insertion-order iteration.
Dict<Symbol, Int> velocities = (dict #kick 90 #snare 70 #hihat 50)

(print $"size: {(size velocities)}")
(print $"kick: {(get velocities #kick)}")
(print $"missing: {(getOr velocities #cowbell 0)}")

Note: Update returns a new dict — Dict is immutable.
Dict<Symbol, Int> withRide = (set velocities #ride 60)

Note: Iterate in insertion order.
(each velocities (fn Symbol k, Int v => (print $"  {(str k)} = {v}")))

Note: Keys can be Int / Long / Float / String / Symbol / Note / Chord / hashable Tuples.
Dict<Note, Int> noteVel = (dict C4 90 E4 70 G4 80)
```

### Collections and Functional Programming

```flow
use "@std"

Int[] nums = [1, 2, 3, 4, 5]

Note: Map - double each number
Int[] doubled = (map nums (fn Int n => (mul n 2)))
(print (str doubled))

Note: Filter - keep numbers greater than 3
Int[] big = (filter nums (fn Int n => (gt n 3)))
(print (str big))

Note: Reduce - sum all numbers
Int total = (reduce nums 0 (fn Int acc, Int n => (add acc n)))
(print (concat "Sum: " (str total)))

Note: Each - print each number
(each nums (fn Int n => (print (str n))))

Note: Closures capture variables
Int factor = 10
Int[] scaled = (map [1, 2, 3] (fn Int n => (mul n factor)))
(print (str scaled))
```

---

## Composition Basics

### Simple Melody

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        section melody {
            Sequence mel = | C4 D4 E4 F4 | G4 A4 B4 C5 |
        }

        Song song = [melody]
        Buffer audio = (renderSong song "piano")
        (writeWav "simple_melody.wav" audio)
        (print "Exported simple_melody.wav!")
    }
}
```

### Chord Progression

```flow
use "@std"
use "@audio"

tempo 100 {
    timesig 4/4 {
        key Cmajor {
            section chordProg {
                Note: Using roman numerals
                Sequence chords = | I IV V I |
            }

            Song song = [chordProg]
            Buffer audio = (renderSong song "piano")
            (writeWav "chords.wav" audio)
            (print "Exported chords.wav!")
        }
    }
}
```

### Full Song with Sections

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section intro {
                Sequence melody = | C4 E4 G4 C5 |
            }

            section verse {
                Sequence lead = | E4 E4 F4 G4 |
                Sequence bass = | C3 C3 G3 G3 |
            }

            section chorus {
                Sequence lead = | G4 A4 G4 E4 |
            }

            section bridge {
                Sequence mel = | A4 G4 F4 E4 |
            }

            section outro {
                Sequence melody = | I IV V I |
            }

            Song fullSong = [intro verse*2 chorus bridge verse chorus*2 outro]
            Buffer rendered = (renderSong fullSong "piano")
            Buffer final = rendered -> fadeIn 0.3s -> fadeOut 0.5s
            (writeWav "full_song.wav" final)

            Int frames = (getFrames final)
            Int duration = (idiv frames 44100)
            (print (concat "Duration: ~" (concat (str duration) "s")))
        }
    }
}
```

### Expressive Piano Piece

```flow
use "@std"
use "@audio"

tempo 72 {
    timesig 4/4 {
        key Cmajor {
            Note: Opening - gentle start with crescendo
            section intro {
                Sequence phrase1 = | pp C4q E4q G4q C5q | -> crescendo 0.2 0.7
                Sequence phrase2 = | mf E4q G4q B4q E5q | -> humanize 0.1
            }

            Note: Development - louder with articulation
            section development {
                Sequence theme = | f C4q> D4q E4q stacc F4q |
                Sequence variation = theme -> transpose +4st -> decrescendo 0.8 0.4
            }

            Note: Climax - fortissimo with swell
            section climax {
                Sequence peak = | C4q E4q G4q C5q E5q G5q C6q C6q | -> swell 0.5 1.0
            }

            Note: Resolution - quiet ending
            section resolution {
                Sequence ending = | mp G4q E4q C4h | -> ritardando 0.5
                Sequence finalChord = | pp C4w |
            }

            Song piece = [intro development climax resolution]
            Buffer rendered = (renderSong piece "piano" release=2.5s)
            Buffer final = rendered -> fadeIn 0.3s -> fadeOut 0.5s
            (writeWav "expressive_piano.wav" final)
        }
    }
}
```

The `release=2.5s` named arg lengthens piano's sustain-pedal-sim tail (default 1.5s, range [0.05s, 10s]).

### Voice-Block Polyphony

Multiple voices that share a bar's onset, written inline:

```flow
use "@std"
use "@audio"

tempo 110 {
    timesig 4/4 {
        key Cmajor {
            section stride {
                Note: Bass voice (whole note) under a quarter-note melody.
                Sequence bar = | {voice C3w} {voice C5q D5q E5q F5q} |
            }
            Song song = [stride*2]
            Buffer audio = (renderSong song "piano")
            (writeWav "stride.wav" audio)
        }
    }
}
```

The two voices render additively in audio AND as overlapping NoteOn events in MIDI export. For voices the composer thinks of as different "lines", use separate `Sequence` variables instead.

### Parameterized Sections

Sections can take typed parameters with defaults, support overloading, and compose with the `*N` repetition operator:

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Note: Overload 1 - single Note binding
            section verse(Note root) {
                Sequence inner = | C4q E4q G4q B4q |
            }

            Note: Overload 2 - chord-literal pattern (music-aware)
            section verse(Cmaj7) {
                Sequence inner = | C4q E4q G4q B4q | C5q B4q G4q E4q |
            }

            Note: Default values
            section intro(Note root = C4, Int repeats = 2) {
                Sequence inner = | C4h E4h | G4h B4h |
            }

            section chorus {
                Sequence inner = | C4h E4h G4h B4h |
            }

            Song song = [
                intro()           Note: both defaults
                verse(C4)         Note: overload 1 wins
                verse(Cmaj7)      Note: overload 2 wins
                chorus            Note: legacy zero-arg form
                verse(C4)*3       Note: repetition operator
            ]
            Buffer mix = (renderSong song "piano")
            (writeWav "parameterized.wav" mix)
        }
    }
}
```

---

## Transforms

### Pattern Transforms

```flow
use "@std"

timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |

    Note: Transpose up 2 semitones
    Sequence t = mel -> transpose +2st
    (print (concat "Transposed: " (str t)))

    Note: Invert (mirror intervals)
    Sequence inv = (invert mel)
    (print (concat "Inverted: " (str inv)))

    Note: Retrograde (reverse)
    Sequence ret = (retrograde mel)
    (print (concat "Retrograde: " (str ret)))

    Note: Augment (double durations)
    Sequence aug = (augment mel)
    (print (concat "Augmented: " (str aug)))

    Note: Diminish (halve durations)
    Sequence dim = (diminish mel)
    (print (concat "Diminished: " (str dim)))

    Note: Octave shift
    Sequence high = mel -> up 1
    (print (concat "Up octave: " (str high)))

    Note: Repeat with transposition
    Sequence rising = mel -> repeat 3 +4st
    (print (concat "Rising: " (str rising)))

    Note: Chained transforms
    Sequence chain = (retrograde (transpose mel +5st))
    (print (concat "Chained: " (str chain)))
}
```

### Tidal-Style Pattern Combinators

Opt-in via `use "@patterns"`. 13 combinators operate on `Sequence`s; cycle unit is bars:

```flow
use "@std"
use "@audio"
use "@patterns"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Sequence base = | C4q D4q E4q F4q | G4q A4q B4q C5q |

            Note: every Nth bar: apply a transform
            Sequence v1 = (every 2 (fn Sequence s => (fast s 2.0)) base)

            Note: rotate bar order by 25%
            Sequence v2 = (phase 0.25 v1)

            Note: layer original + transposed copy
            Sequence v3 = (jux (fn Sequence s => (transpose s +12st)) v2)

            Note: 40% chance per bar of reversing
            Sequence v4 = (sometimes 0.4 (fn Sequence s => (rev s)) v3)

            section main { Sequence v = v4 }
            Song song = [main]
            Buffer mix = (renderSong song "piano")
            (writeWav "tidal.wav" mix)
        }
    }
}
```

Other combinators: `slow`, `chunk`, `rev`, `iter`, `palindrome`, `superimpose`, `degrade`, `sparseSeq`. Transform args are lambda-required.

---

## Generative

### Random Choice + Euclidean

```flow
use "@std"

timesig 4/4 {
    Note: Random note selection
    Sequence random = | (? C4 E4 G4) (? C4 E4 G4) (? C4 E4 G4) (? C4 E4 G4) |
    (print (concat "Random: " (str random)))

    Note: Weighted random (C4 most likely)
    Sequence weighted = | (? C4:50 E4:30 G4:20) (? C4:50 E4:30 G4:20) _ _ |
    (print (concat "Weighted: " (str weighted)))

    Note: Seeded random (deterministic — same output every run)
    Sequence seeded = | (?? C4 E4 G4) (?? D4 F4 A4) (?? E4 G4 B4) (?? C4 E4 G4) |
    (print (concat "Seeded: " (str seeded)))
}

Note: Euclidean rhythms (Bjorklund algorithm)
Sequence euclid1 = (euclidean 3 8 C4)
(print (concat "Euclidean 3/8: " (str euclid1)))

Sequence euclid2 = (euclidean 5 8 E4)
(print (concat "Euclidean 5/8: " (str euclid2)))
```

### Markov Chains

Train a Markov model from a short corpus, generate variations:

```flow
use "@std"
use "@audio"
use "@generative"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Note: 1. Teach the chain a step-wise scale shape
            Sequence corpus = | C4q D4q E4q F4q G4q F4q E4q D4q C4q |

            Note: 2. One-shot: (markov corpus order length seed)
            Sequence oneShot = (markov corpus 2 16 42)

            Note: 3. Or train once, generate many times
            MarkovModel m = (markovTrain corpus 2)
            Sequence riffA = (markovGenerate m 8 100)
            Sequence riffB = (markovGenerate m 8 200)

            section A { Sequence v = oneShot }
            section B { Sequence v = riffA }
            section C { Sequence v = riffB }

            Song song = [A B C]
            Buffer mix = (renderSong song "piano")
            (writeWav "markov.wav" mix)
        }
    }
}
```

Order is clamped to [1, 3]. Feature extraction via `features=#pitch` (default) or `features=<<#pitch, #duration>>` for richer tuple-keyed states.

### L-Systems

Lindenmayer-system Symbol rewriting -> notes:

```flow
use "@std"
use "@audio"
use "@generative"
use "@notation"

tempo 120 {
    timesig 4/4 {
        Note: Canonical "algae" rules:
        Note:   #A -> #A #B
        Note:   #B -> #A
        Dict<Symbol, Symbol[]> rules = (dict #A (list #A #B) #B (list #A))

        Note: Iteration 4 produces an 8-Symbol sequence
        Symbol[] expanded = (lsystem #A rules 4)

        Note: Map each Symbol to a note using (quarter C4) from @notation
        Sequence music = (lsystemToSequence expanded
                            (fn Symbol s =>
                              (if (equals s #A)
                                  (quarter C4)
                                  (quarter E4))))

        section main { Sequence v = music }
        Song song = [main]
        Buffer mix = (renderSong song "piano")
        (writeWav "lsystem.wav" mix)
    }
}
```

Iterations clamped to [0, 20] (DoS guard). Also available: `lsystemModel` / `lsystemGenerate` for the train-once-generate-many split.

### Chord-Aware Improvisation with `jam`

`jam` is a chord-aware Markov improviser — chord tones on strong beats, scale tones on weak, chromatic-passing notes per style pack:

```flow
use "@std"
use "@audio"
use "@improv"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Note: A ii-V-I-VI progression
            Sequence chords = | Dm7 | G7 | Cmaj7 | Am7 |

            Note: Sparse named args — any subset, in any order (incl. seed= and key=)
            Sequence solo = (jam over=chords style=#jazz length=4 key="Cmajor" seed=42)

            Note: Full positional form: (jam over style length key seed order)
            Sequence soloAlt = (jam chords #blues 4 "Cmajor" 100 2)

            section A { Sequence v = solo }
            Song song = [A]
            Buffer mix = (renderSong song "piano")
            (writeWav "jam.wav" mix)
        }
    }
}
```

Style packs ship at `flow-lang/improv/styles/*.flow` (`#jazz`, `#blues`, `#classical`). Override by dropping a same-named file at `~/.config/flow/styles/`. All parameters accept named args — `over=`, `style=`, `length=`, `key=`, `seed=`, `order=` — and you can pass any subset in any order; the rest fall back to their defaults. Positional calls still work too.

---

## Audio

### Effect Processing Chain

```flow
use "@std"
use "@audio"

Note: Create a test tone (Hertz-typed)
Buffer tone = (createSineTone 0.5 440Hz 0.5)
(print (concat "Original frames: " (str (getFrames tone))))

Note: Apply effects chain — Hertz / Decibel / Millisecond / Second literals
Buffer processed = tone -> lowpass 1.0kHz -> reverb 0.3 -> gain -3dB
(print (concat "Processed frames: " (str (getFrames processed))))

Note: Individual effects
Buffer lp = (lowpass tone 800Hz)
Buffer hp = (highpass tone 200Hz)
Buffer bp = (bandpass tone 200Hz 2000Hz)
Buffer rev = (reverb tone 0.7 0.3 0.5)
Buffer comp = (compress tone -12dB 4.0)
Buffer del = (delay tone 250ms 0.4 0.5)

(writeWav "processed.wav" processed)
(print "Exported processed.wav!")
```

### Granular Synthesis

```flow
use "@std"
use "@audio"

Note: Source: 1-second 440Hz sine
Buffer tone = (createSineTone 1.0 440Hz 0.5)

Note: Default Hann windowing, modest jitter
Buffer g1 = (granular tone 50ms 20Hz 0.3)

Note: Heavier jitter + Gaussian windowing — cloudier texture
Buffer g2 = (granular tone 80ms 15Hz 0.7 #gaussian)

Note: Compose with reverb + pan (negative literals in arg position need (neg ...))
Buffer wet = (reverb g2 0.5) -> pan (neg 0.3)

Buffer out = (mix g1 wet)
(writeWav "granular.wav" out)
```

Knobs: `grain` (Millisecond), `density` (Hertz), `jitter` (Double in [0, 1]), `windowing` (`#hann` default, `#gaussian`, `#tukey`). Unknown windowing Symbol falls back to Hann + stderr advisory.

### Time-Stretch and Pitch-Shift

```flow
use "@std"
use "@audio"

Buffer base = (createSineTone 2.0 440Hz 0.4)

Note: Time-stretch 2x without pitch change. Mode picks per-frame algorithm.
Buffer slow = (stretch base 2.0 #auto)         Note: HPS picks vocoder vs PSOLA
Buffer vocoder = (stretch base 2.0 #vocoder)   Note: phase-locked STFT — harmonic material
Buffer psola = (stretch base 2.0 #psola)       Note: TD-PSOLA — percussive material

Note: Pitch-shift up 5 semitones without time change.
Note: pitchShift accepts Double / Cent / Semitone for the cents arg.
Buffer up5 = (pitchShift base +5st #auto)
Buffer up500c = (pitchShift base +500c #auto)

Note: Identity fast-paths — return input byte-identical.
Buffer ident1 = (stretch base 1.0)
Buffer ident2 = (pitchShift base 0c)

Buffer out = (mix slow up5)
(writeWav "stretch_pitchshift.wav" out)
```

### Audio Synthesis from Scratch

```flow
use "@std"
use "@audio"

Note: Create an oscillator
OscillatorState osc = (createOscillatorState 440Hz 44100)

Note: Generate a buffer of sine wave
Buffer audio = (createBuffer 44100 2 44100)
(generateSine audio osc 0.5)

Note: Apply an ADSR envelope
Envelope env = (createADSR 0.01 0.1 0.7 0.3 44100)
(applyEnvelope audio env)

Note: Add some reverb
Buffer wet = (reverb audio 0.4)

(writeWav "synth_from_scratch.wav" wet)
(print "Exported synth_from_scratch.wav!")
```

### Multiple Synthesizers

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

            Note: Same melody, different instruments.
            Note: Piano / brass / sax / strings / flute / bell are sampled
            Note: (U-Iowa MIS); organ and drums are hand-rolled synthesis.
            Buffer piano   = (renderSong song "piano")
            Buffer brass   = (renderSong song "brass")
            Buffer sax     = (renderSong song "sax")
            Buffer flute   = (renderSong song "flute")
            Buffer organ   = (renderSong song "organ")
            Buffer strings = (renderSong song "strings")
            Buffer bell    = (renderSong song "bell")
            Buffer drums   = (renderSong song "drums")

            (writeWav "piano.wav"   piano)
            (writeWav "strings.wav" strings)
            (writeWav "bell.wav"    bell)
            (print "exported all instruments")
        }
    }
}
```

### Chord Progression with Voice Leading

```flow
use "@std"
use "@audio"

tempo 100 {
    timesig 4/4 {
        key Cmajor {
            section hook {
                Sequence chords = progression voices 4 | I:2 vi IV V:2 |
            }
            Song song = [hook*4]
            Buffer rendered = (renderSong song "piano")
            Buffer final = rendered -> reverb 0.3 -> fadeOut 0.5s
            (writeWav "progression.wav" final)
        }
    }
}
```

### Loading a WAV Sample

```flow
use "@std"
use "@audio"

Note: Plain load — 16/24/32-bit PCM, auto-resampled to 44.1 kHz
Buffer sample = (loadWav "kick.wav")

Note: Varispeed load — semitones or ratio overload
Buffer up3 = (loadWav "kick.wav" +3st)        Note: +3 semitones (Semitone literal)
Buffer half = (loadWav "kick.wav" 0.5)        Note: half-speed (octave down)

Buffer processed = sample -> pan 0.0 -> gain 0dB -> reverb 0.2
(writeWav "kick_processed.wav" processed)
```

### Vocalization

```flow
use "@std"
use "@audio"

Note: Formant vowel synthesis on a pitched note.
Buffer ah = (sing "ah" C4 0.5)
Buffer ee = (sing "ee" E4 0.5)
Buffer oh = (sing "oh" G4 0.5)

Buffer melody = ah -> appendBuffers ee -> appendBuffers oh
(writeWav "vocal_line.wav" melody)
```

---

## Microtonal

### Just Intonation via Pragma

```flow
enable justIntonation;

use "@std"
use "@audio"

Note: With the pragma active, C-E renders at the pure 5/4 ratio
Note: instead of the 12-TET ~1.2599 approximation.
tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section triad {
                Sequence arp = | C4q E4q G4q C5q |
                Sequence held = | [C4 E4 G4 C5]w |
            }
            Song song = [triad]
            Buffer audio = (renderSong song "piano")
            (writeWav "ji_triad.wav" audio)
        }
    }
}
```

Available tuning pragmas: `enable justIntonation;`, `enable pythagorean;`, `enable equalTemperament;` (the default). File-scoped, last-wins with inline `tuning { }` blocks.

### Scala `.scl` Tuning

```flow
use "@std"
use "@audio"

Note: Load any Scala .scl file (~5300 community tunings available).
Tuning partch = (loadScala "tunings/partch_43.scl")
(print $"Loaded: {(str partch)}")

tempo 100 {
    timesig 4/4 {
        Note: Identifier form
        tuning partch {
            section a { Sequence mel = | C4q D4q E4q F4q | }
        }

        Note: String-literal sugar — desugars to (loadScala "...") at parse time
        tuning "tunings/carlos_alpha.scl" {
            section b { Sequence mel = | C4q E4q G4q B4q | }
        }
    }
}
```

For non-octave-repeating scales (Carlos Alpha, Bohlen-Pierce) the period auto-adopts from the .scl — no explicit `.kbm` needed. Pass one as the 2nd arg to `loadScala` if you have one.

---

## Notation IO

### MusicXML Export

```flow
use "@std"
use "@notation-io"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section piano {
                Sequence mel = | C4q D4q E4q F4q | G4q A4q B4q C5q |
            }
            Song song = [piano]
            (writeMusicXML "song.musicxml" song)
            (print "MusicXML written — opens in MuseScore / Sibelius / Dorico / Finale")
        }
    }
}
```

Voice blocks export as per-note `<voice>N</voice>` tagging; microtonal pitches as decimal `<alter>` cent precision; articulations map per MuseScore convention.

### LilyPond Export

```flow
use "@std"
use "@notation-io"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section piano {
                Sequence mel = | C4q D4q E4q F4q | G4q A4q B4q C5q |
            }
            Song song = [piano]
            (writeLilyPond "song.ly" song)
            (print "Run `lilypond song.ly` to render PDF (requires lilypond 2.24+)")
        }
    }
}
```

### ABC Import

```flow
use "@std"
use "@notation-io"

String tune = "X:1
T:C Major Scale
M:4/4
L:1/4
K:Cmaj
C D E F | G A B c |"

Section parsed = (abc tune)
(print "ABC imported")
```

Supports ABC 2.1 subset + abc2midi extensions: standard headers (`X`, `T`, `M`, `L`, `K`, `Q`), accidentals (`^`/`_`/`=`), octave markers (`'`/`,`), modal keys (`Edor`, `Dmix`, `Aphr`, `Cmix`, `Glyd`, `Bphr`, `Floc`). Multi-tune files (`X:1`, `X:2`) return `Array[Section]`. Unknown ornaments dropped with `[abc]` stderr advisory.

### MML Import

```flow
use "@std"
use "@notation-io"

String chiptune = "T120 L8 O4 cdefga>cb<a>c"

Sequence parsed = (mml chiptune)
(print "MML imported")
```

Supports PC-98 MML common core: notes (case-insensitive), accidentals (`+`/`#`/`-`), absolute octave (`O<n>`), relative octave (`>`/`<`), tempo (`T<n>`), default length (`L<n>`), loops (`[...]<n>`, depth cap 16), dotted notes, ties (`&`). FM operator routing + drum-bank opcodes ignored with `[mml]` advisory.

---

## Misc

### MIDI Export

```flow
use "@std"
use "@audio"

tempo 140 {
    timesig 3/4 {
        key Gmajor {
            section waltz {
                Sequence v = | G4q B4q D5q | D5h G4q |
            }
            section ending { Sequence v = | G4h. | }

            Song song = [waltz waltz ending]
            (writeMidi "waltz.mid" song)
            (print "wrote waltz.mid")
        }
    }
}
```

Multi-track export with GM-program routing: sequence names prefix-match to instruments (`piano*`->0, `brass*`->56, `sax*`->65, `flute*`->73, `violin*`->40, `cello*`->42, `drum*`->0 on channel 9). One track per unique sequence name + a conductor track.

### Waltz in 3/4

```flow
use "@std"
use "@audio"

tempo 90 {
    timesig 3/4 {
        key Aminor {
            section waltz {
                Sequence mel = | A4 C5 E5 |
            }
            section middle {
                Sequence mel = | D5 F5 A5 |
            }

            Song piece = [waltz*4 middle*2 waltz*2]
            Buffer audio = (renderSong piece "piano")
            Buffer final = audio -> reverb 0.4 -> fadeOut 0.5s
            (writeWav "waltz.wav" final)
            (print "Exported waltz.wav!")
        }
    }
}
```

### Ornaments and Expression

```flow
use "@std"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Note: Ghost notes - barely audible
            Sequence ghosty = | C4 (ghost D4) E4 F4 |
            (print (concat "Ghost: " (str ghosty)))

            Note: Grace notes - quick ornament
            Sequence graceful = | (grace B3) C4 D4 E4 F4 |
            (print (concat "Grace: " (str graceful)))

            Note: Trill - rapid alternation
            Sequence mel = | C4h E4h |
            Sequence trilled = mel -> trill +2st
            (print (concat "Trill: " (str trilled)))

            Note: Tremolo - rapid repetition
            Sequence trem = mel -> tremolo 4
            (print (concat "Tremolo: " (str trem)))

            Note: Humanize for natural feel (Gaussian, seeded -> deterministic)
            Sequence natural = (humanizeGaussian | C4 D4 E4 F4 | 0.15 42)
            (print (concat "Humanized: " (str natural)))
        }
    }
}
```

### SFZ Orchestral Sampler

> **Desktop build only.** `use "@sfz"` is stripped on the web playground (`FlowTarget=Web`). Running this example also requires `sfz_root` set in `~/.config/flow/config.toml` pointing to your VSCO-CE (or compatible) sample library install.

For large external sample libraries (blessed: VSCO Community CE 1.1.0), use `@sfz`:

```flow
use "@audio"
use "@sfz"

Note: Resolve #violin against the 20-entry GM dict and your `sfz_root`
Note: setting in ~/.config/flow/config.toml.
Sfz violin = (loadSfz #violin)

tempo 100 {
    timesig 4/4 {
        key Cmajor {
            section opening {
                Sequence main = | C4q D4q E4q F4q G4h G4h C5w |
            }
            Song song = [opening]
            Note: "sampler:NAME" looks up the bound `violin` patch.
            Buffer mix = (renderSong song "sampler:violin")
            (writeWav "violin.wav" mix)
        }
    }
}
```

See [examples/symphony/sfz_smoke.flow](https://github.com/NoahFreelove/flow-sharp/blob/dev/examples/symphony/sfz_smoke.flow) for a full walkthrough, and `examples/symphony/symphony.flow` for the v1.4 showcase using 5 VSCO-CE instruments.

---

## Where to Find More

The `examples/` directory in the repo ships runnable tutorial chapters. Each file is heavily commented and runs end-to-end:

| Folder | What's inside |
|---|---|
| `examples/tutorial.flow` | Top-to-bottom language walkthrough — start here if you're new |
| `examples/showcase.flow` | Multi-feature demonstration |
| `examples/long_demo.flow` | Extended composition example |
| `examples/dsp/` | Granular (`granular.flow`), time-stretch + pitch-shift (`stretch_pitchshift.flow`) |
| `examples/generative/` | Markov chains (`markov_jazz.flow`), Tidal combinators (`tidal_combinators.flow`) |
| `examples/notation/` | MusicXML + LilyPond export, ABC + MML import (4 files in `to_*.flow` / `from_*.flow`) |
| `examples/pragmas/` | `enable hAsB;` (German B notation), `enable justIntonation;` (5-limit JI) |
| `examples/ragtime/` | "Stride & Stomp" — v1.4 showcase #2 (solo VSCO-CE UprightPiano, F major, ~58s) |
| `examples/scala/` | Scala `.scl` microtuning walkthrough |
| `examples/sections/` | Parameterized sections with defaults, overloads, and `*N` repetition |
| `examples/symphony/` | "In Five Voices" — v1.4 showcase #1 (5 VSCO-CE instruments, ABA D minor, ~60s) + `sfz_smoke.flow` |

Run any of them with:

```bash
dotnet run --project flow-interpreter examples/tutorial.flow
```

Or with the `flow` CLI binary (after `scripts/install.sh`):

```bash
flow run examples/dsp/granular.flow
```

## See Also

- [Quick Start](Quick-Start.md) - Getting started
- [Tips and Tricks](Tips-and-Tricks.md) - Common idioms and pitfalls
- [Standard Library](Standard-Library.md) - Complete function reference
- [Chord Progressions](Chord-Progressions.md) - Voice-led chord DSL
- [Generative Music](Generative.md) - Euclidean rhythms, variation, polyrhythms
- [Voices and Tracks](Voices-and-Tracks.md) - Multi-track timeline
- [Vocalization](Vocalization.md) - Formant synthesis
