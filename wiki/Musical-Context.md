# Musical Context

Musical context blocks set tempo, time signature, key, swing, dynamics, pan, gain, reverb time, voice pool size, sustain pedal state, tuning, and tempo ramps for the code inside them. They use a push/pop scoping model — inner blocks inherit from outer blocks and can override specific settings; exiting a block restores the previous state.

The full set of context-block keywords is:

> `tempo`, `timesig`, `key`, `swing`, `dynamics`, `pan`, `gain`, `reverbTime`, `rit`, `accel`, `voicePool`, `sustainPedal`, `tuning`

These are all **reserved** — they can't be redefined as `proc` or variable names.

## Tempo

Sets the beats per minute (BPM):

```flow
use "@std"

tempo 120 {
    (print "120 BPM context")
}
```

Default when not set: **120 BPM**.

## Time Signature

Sets the meter as `numerator/denominator`:

```flow
timesig 4/4 {
    Sequence fourFour = | C4 D4 E4 F4 |
}

timesig 3/4 {
    Sequence waltz = | C4 E4 G4 |
}

timesig 6/8 {
    Sequence compound = | C4 D4 E4 F4 G4 A4 |
}
```

Default: **4/4**.

### Common-Time and Cut-Time Shorthand

The capital `C` is shorthand for 4/4 — it lowers to 4/4 at parse time so the renderer, MIDI export, and musical-context stack all see identical data to the explicit form:

```flow
timesig C {
    Sequence sameAs44 = | C4 D4 E4 F4 |
}
```

Cut time (alla breve, 2/2) has three interchangeable shorthands — `C/`, `C|`, and the cent sign `¢` — all of which lower to `2/2`:

```flow
timesig C/ { Sequence a = | C4 D4 | }    Note: 2/2
timesig C| { Sequence b = | C4 D4 | }    Note: 2/2
timesig ¢  { Sequence c = | C4 D4 | }    Note: 2/2
```

These glyphs are only consumed in the `timesig` position, so `/` and `|` keep their normal meaning everywhere else.

## Key

Sets the musical key for roman numeral resolution and scale operations:

```flow
key Cmajor {
    Sequence prog = | I IV V I |
}

key Aminor {
    Sequence prog = | i iv V i |
}
```

### Valid Keys

All 12 major keys — `Cmajor`, `Csharpmajor` / `Dbmajor`, `Dmajor`, `Dsharpmajor` / `Ebmajor`, `Emajor`, `Fmajor`, `Fsharpmajor` / `Gbmajor`, `Gmajor`, `Gsharpmajor` / `Abmajor`, `Amajor`, `Asharpmajor` / `Bbmajor`, `Bmajor`.

All 12 minor keys — same pattern with `minor` (e.g., `Aminor`, `Fsharpminor`).

## Swing

Adds swing feel to rhythms. Value accepts decimals or percentages (`0.5` = straight, ~`0.67` / `66%` = triplet swing):

```flow
swing 55% {
    Note: subtle swing
}

swing 0.6 {
    Note: moderate swing
}
```

The `N%` form divides by 100 at parse time — `swing 55%` and `swing 0.55` are equivalent through the rest of the pipeline.

## Dynamics

Sets a default velocity for notes in scope:

```flow
dynamics f {
    Sequence loud = | C4 D4 E4 F4 |

    dynamics pp {
        Sequence soft = | C4 D4 E4 F4 |
    }
}
```

| Marking | Velocity |
|---------|----------|
| `ppp` | 0.125 |
| `pp`  | 0.25 |
| `p`   | 0.375 |
| `mp`  | 0.5 |
| `mf`  | 0.625 |
| `f`   | 0.75 |
| `ff`  | 0.875 |
| `fff` | 1.0 |

## Pan

Sets stereo placement for audio rendered inside the block. Range is `-1.0` (hard left) to `+1.0` (hard right):

```flow
use "@audio"

pan -0.5 {
    Note: section will be panned left
    section leftChannel {
        Sequence mel = | C4 E4 G4 |
    }
}

pan 0.5 {
    Note: section will be panned right
    section rightChannel {
        Sequence mel = | G4 B4 D5 |
    }
}
```

`pan` is also a [buffer-level effect](Effects.md) for applying panning to a single `Buffer`.

## Gain

Applies a gain factor to audio rendered inside the block (values in the 0.0 - 1.0 range):

```flow
use "@audio"

gain 0.3 {
    section quietPart {
        Sequence mel = | C4q E4q G4q C5q |
    }
}
```

`gain` is also available as a [buffer-level effect](Effects.md) using decibel values.

## Reverb Time

Wraps audio rendered inside the block with a global reverb tail of the given RT60 (in seconds):

```flow
use "@audio"

reverbTime 2.5 {
    section cathedral {
        Sequence chant = | C4w E4w G4w C5w |
    }
}
```

Must be non-negative. Use `0` to disable.

## Voice Pool

Sets the maximum number of simultaneously active voices for the block. When the voice count exceeds the pool, Flow truncates the active voice with the earliest onset (steal-oldest policy) with a 5 ms fade to avoid clicks:

```flow
voicePool 16 {
    Note: rendering inside this block uses at most 16 simultaneous voices
    section denseTexture {
        Sequence layered = | [C3 E3 G3 C4 E4 G4 C5 E5 G5]w |
    }
}
```

The default pool size is **32**; the range is `[1, 256]`. The policy is fully deterministic (tiebreaker is the original input index), so two-run output stays byte-identical.

## Sustain Pedal

Extends every note's rendered buffer by a 2.0 s sustain tail — the digital equivalent of holding down a piano damper pedal:

```flow
use "@audio"

sustainPedal {
    section dreamy {
        Sequence arp = | C4q E4q G4q B4q C5q E5q G5q B5q |
    }
}
```

Pair with the `release=` named arg on `renderSong` for finer-grained control over the decay tail length:

```flow
Buffer result = (renderSong song "piano" release=3.0s)
```

## Tuning

Applies a non-12-TET tuning to its body — Scala `.scl` files load into a `Tuning` value, then a `tuning { ... }` block scopes that tuning to the contained code. Three composer-facing surface forms:

The paths below use the bundled test-fixture `.scl` files. Supply any valid `.scl` path from your own tuning library or from the [Scala scale archive](http://www.huygens-fokker.org/scala/scales.zip).

```flow
use "@std"
use "@audio"

Note: 1. identifier-bound variable
Tuning partch = (loadScala "flow-lang.Tests/fixtures/scala/partch_43.scl")
tuning partch {
    section microtonal { Sequence mel = | C4 D4 E4 F4 | }
}

Note: 2. inline call
tuning (loadScala "flow-lang.Tests/fixtures/scala/carlos_alpha.scl") {
    section nonOctave { Sequence climb = | C4 D4 E4 F4 G4 A4 B4 C5 | }
}

Note: 3. string-literal sugar (desugars to form 2 at parse time)
tuning "flow-lang.Tests/fixtures/scala/just_5limit.scl" {
    section justIntonationBlock { Sequence mel = | C4 D4 E4 F4 | }
}
```

Tuning blocks compose last-wins with the file-scope `enable justIntonation;` / `pythagorean;` / `equalTemperament;` pragmas. Non-octave-repeating scales (Carlos Alpha, Bohlen-Pierce) auto-adopt the loaded scale's period.

## Ritardando / Accelerando

Wrap a passage in a `rit` block to interpolate tempo downward to a target BPM, or `accel` to speed up:

```flow
tempo 120 {
    rit 60 {
        Note: tempo ramps from 120 down to 60 inside this block
        Sequence ending = | C4h G3h |
    }
}

tempo 80 {
    accel 140 {
        Note: tempo ramps from 80 up to 140 inside this block
        Sequence intro = | C4q D4q E4q F4q |
    }
}
```

See also the [`tempoRamp` transform](Pattern-Transforms.md) which produces a rendered buffer directly.

## Nesting and Inheritance

Context blocks nest naturally. Inner blocks inherit from outer blocks and can override specific settings:

```flow
tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Note: 120 BPM, 4/4, C major
            Sequence mel = | C4 D4 E4 F4 |

            key Aminor {
                Note: still 120 BPM, 4/4, but now A minor
                Sequence mel2 = | A4 B4 C5 D5 |
            }
        }
    }
}
```

## Typical Pattern

Most musical code wraps everything in `tempo` → `timesig` → `key`:

```flow
tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Sequence melody = | C4 E4 G4 C5 |
        }
    }
}
```

## Deeply Nested Example

```flow
tempo 100 {
    timesig 6/8 {
        key Aminor {
            swing 55% {
                dynamics mf {
                    Sequence mel = | A4 C5 E5 A5 C6 E6 |
                }
            }
        }
    }
}
```

## Scoping Rules

- Musical context is **push/pop scoped**: entering a block pushes new settings, exiting pops them.
- Unspecified fields inherit from the parent scope.
- Code outside any context block uses defaults (120 BPM, 4/4, no key, no swing).
- Note streams use the active `timesig` context to determine bar duration; if none is set, the default 4/4 applies silently.

## When to Use Each Block

| Block | When Required |
|-------|---------------|
| `timesig` | When you want a meter other than the default 4/4 (note streams default to 4/4 if none is set) |
| `tempo` | When rendering audio (determines actual playback speed) |
| `key` | When using roman numerals, progressions, or `scaleNotes` |
| `swing` | When you want swing feel applied to rhythms |
| `dynamics` | When you want a default velocity for all notes in scope |
| `pan` | When you want a section positioned in the stereo field |
| `gain` | When you want a passage rendered quieter/louder |
| `reverbTime` | When you want a global reverb tail (RT60 seconds) |
| `voicePool` | When you need to cap simultaneous-voice count for dense textures |
| `sustainPedal` | When you want piano-pedal-style 2 s sustain tails |
| `tuning` | When using a non-12-TET tuning (Scala `.scl` file) |
| `rit`, `accel` | When you want tempo interpolation inside the block |

## See Also

- [Note Streams](Note-Streams.md) - Writing inline notation
- [Chords and Harmony](Chords-and-Harmony.md) - Roman numerals need `key` context
- [Song Structure](Song-Structure.md) - Sections with musical context
- [Effects](Effects.md) - Buffer-level pan and gain
- [Pattern Transforms](Pattern-Transforms.md) - `tempoRamp` and related transforms
