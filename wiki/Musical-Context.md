# Musical Context

Musical context blocks set tempo, time signature, key, swing, and dynamics for the code inside them. They use a scoping model where inner blocks inherit and can override outer settings.

## Tempo

Sets the beats per minute (BPM):

```flow
use "@std"

tempo 120 {
    Note: everything in here is at 120 BPM
    (print "120 BPM")
}
```

Default tempo when not specified: **120 BPM**.

## Time Signature

Sets the meter using `numerator/denominator` syntax:

```flow
timesig 4/4 {
    Sequence fourFour = | C4 D4 E4 F4 |  Note: 4 quarter-note beats
}

timesig 3/4 {
    Sequence waltz = | C4 E4 G4 |  Note: 3 quarter-note beats
}

timesig 6/8 {
    Sequence compound = | C4 D4 E4 F4 G4 A4 |  Note: 6 eighth-note beats
}
```

Default time signature: **4/4**.

## Key

Sets the musical key for roman numeral resolution and scale operations:

```flow
key Cmajor {
    Sequence progression = | I IV V I |
    Note: I=C, IV=F, V=G in C major
}

key Aminor {
    Sequence progression = | i iv V i |
}
```

### Valid Keys

All 12 major keys: `Cmajor`, `Csharpmajor`/`Dbmajor`, `Dmajor`, `Dsharpmajor`/`Ebmajor`, `Emajor`, `Fmajor`, `Fsharpmajor`/`Gbmajor`, `Gmajor`, `Gsharpmajor`/`Abmajor`, `Amajor`, `Asharpmajor`/`Bbmajor`, `Bmajor`

All 12 minor keys: `Cminor`, `Csharpminor`/`Dbminor`, `Dminor`, `Dsharpminor`/`Ebminor`, `Eminor`, `Fminor`, `Fsharpminor`/`Gbminor`, `Gminor`, `Gsharpminor`/`Abminor`, `Aminor`, `Asharpminor`/`Bbminor`, `Bminor`

## Swing

Adds swing feel to rhythms. Value can be a percentage or a decimal (0.5 = straight, ~0.67 = triplet swing):

```flow
swing 55% {
    Note: subtle swing
}

swing 0.6 {
    Note: moderate swing
}
```

## Dynamics

Sets the default velocity for notes in scope:

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

Most musical code uses all three primary context blocks:

```flow
tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Note: Your musical code goes here
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

- Musical context is **push/pop scoped**: entering a block pushes new settings, exiting pops them
- `null` fields inherit from the parent scope
- Code outside any context block uses defaults (120 BPM, 4/4, no key, no swing)
- Note streams require at minimum a `timesig` context to determine bar duration

## When to Use Each Block

| Block | When Required |
|-------|---------------|
| `timesig` | Always, when using note streams (determines beat count per bar) |
| `tempo` | When rendering audio (determines actual playback speed) |
| `key` | When using roman numerals (`I`, `IV`, `V7`, etc.) or `scaleNotes` |
| `swing` | When you want swing feel applied to rhythms |
| `dynamics` | When you want a default velocity for all notes in scope |

## See Also

- [Note Streams](Note-Streams.md) - Writing inline notation
- [Chords and Harmony](Chords-and-Harmony.md) - Roman numerals need `key` context
- [Song Structure](Song-Structure.md) - Sections with musical context
