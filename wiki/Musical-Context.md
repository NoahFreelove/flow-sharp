# Musical Context

Musical context blocks set tempo, time signature, key, swing, dynamics, pan, gain, and tempo ramps for the code inside them. They use a scoping model where inner blocks inherit from outer blocks and can override specific settings.

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
| `ppp` | ~0.1 |
| `pp` | ~0.2 |
| `p` | ~0.35 |
| `mp` | ~0.5 |
| `mf` | ~0.63 |
| `f` | ~0.75 |
| `ff` | ~0.875 |
| `fff` | ~1.0 |

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
- Note streams require a `timesig` context to determine bar duration.

## When to Use Each Block

| Block | When Required |
|-------|---------------|
| `timesig` | When using note streams (determines beat count per bar) |
| `tempo` | When rendering audio (determines actual playback speed) |
| `key` | When using roman numerals, progressions, or `scaleNotes` |
| `swing` | When you want swing feel applied to rhythms |
| `dynamics` | When you want a default velocity for all notes in scope |
| `pan` | When you want a section positioned in the stereo field |
| `gain` | When you want a passage rendered quieter/louder |
| `rit`, `accel` | When you want tempo interpolation inside the block |

## See Also

- [Note Streams](Note-Streams.md) - Writing inline notation
- [Chords and Harmony](Chords-and-Harmony.md) - Roman numerals need `key` context
- [Song Structure](Song-Structure.md) - Sections with musical context
- [Effects](Effects.md) - Buffer-level pan and gain
- [Pattern Transforms](Pattern-Transforms.md) - `tempoRamp` and related transforms
