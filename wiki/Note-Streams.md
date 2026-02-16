# Note Streams

Note streams are Flow's inline musical notation. They provide a concise syntax for writing melodies, rhythms, and chord progressions directly in your code.

## Basic Syntax

Note streams are delimited by `|` pipe characters and evaluate to a `Sequence`:

```flow
use "@std"

timesig 4/4 {
    Sequence melody = | C4 D4 E4 F4 |
    (print (str melody))
}
```

Each note is a pitch name (A-G), optional accidental, and octave number. Notes without explicit durations are automatically fitted to fill the bar based on the time signature.

## Note Names

Notes use standard pitch notation:

| Component | Options | Example |
|-----------|---------|---------|
| Pitch | A, B, C, D, E, F, G | `C`, `D`, `G` |
| Accidental | `s` (sharp), `f` (flat) | `Cs4`, `Bf3` |
| Octave | 0-10 (default: 4) | `C4`, `A3`, `G5` |

```flow
timesig 4/4 {
    Sequence sharps = | Cs4 Ds4 Fs4 Gs4 |
    Sequence flats = | Bf3 Ef4 Af4 Df5 |
}
```

## Duration Suffixes

Append a letter to specify note duration:

| Suffix | Duration | Beats (in 4/4) |
|--------|----------|-----------------|
| `w` | Whole | 4 |
| `h` | Half | 2 |
| `q` | Quarter | 1 |
| `e` | Eighth | 0.5 |
| `s` | Sixteenth | 0.25 |
| `t` | Thirty-second | 0.125 |

```flow
timesig 4/4 {
    Sequence mixed = | C4h D4q E4q |   Note: half + quarter + quarter = 4 beats
    Sequence fast = | C4e D4e E4e F4e G4e A4e B4e C5e |  Note: 8 eighths = 4 beats
}
```

### Auto-Fit Duration

Notes without a duration suffix are automatically sized to fill the bar evenly:

```flow
timesig 4/4 {
    Sequence four = | C4 D4 E4 F4 |    Note: each gets quarter duration
    Sequence three = | C4 E4 G4 |      Note: each gets ~1.33 beats
}

timesig 3/4 {
    Sequence waltz = | C4 E4 G4 |      Note: each gets quarter duration
}
```

## Dotted Notes

Append `.` after the duration suffix to extend a note by 50%:

```flow
timesig 4/4 {
    Sequence dotted = | C4q. D4e E4h |  Note: 1.5 + 0.5 + 2 = 4 beats
}
```

## Rests

Use `_` for rests. Rests can have duration suffixes too:

```flow
timesig 4/4 {
    Sequence withRest = | C4 _ E4 F4 |      Note: auto-fit rest
    Sequence specificRest = | C4q _q E4q F4q | Note: quarter rest
}
```

## Tied Notes

Use `~` after a note to tie it into the next note (legato):

```flow
timesig 4/4 {
    Sequence tied = | C4h~ D4h |  Note: C4 sustains, then D4
}
```

## Cent Offsets (Microtonal)

Use `+Nc` or `-Nc` for microtonal adjustments in cents (100 cents = 1 semitone):

```flow
timesig 4/4 {
    Sequence microtonal = | C4+50c D4 E4-25c F4 |
}
```

## Chord Brackets

Enclose notes in `[ ]` to play them simultaneously:

```flow
timesig 4/4 {
    Sequence chords = | [C4 E4 G4]q [D4 F4 A4]q [E4 G4 B4]q [C4 E4 G4]q |
}
```

## Named Chords in Note Streams

Use chord symbols directly:

```flow
timesig 4/4 {
    Sequence progression = | Cmaj7 Am7 Dm Gdom7 |
}
```

See [Chords and Harmony](Chords-and-Harmony.md) for all chord types.

## Roman Numerals

Within a `key` context, use roman numerals for scale-degree chords:

```flow
key Cmajor {
    timesig 4/4 {
        Sequence progression = | I IV V I |
        Sequence minor = | ii V7 I |
    }
}
```

See [Chords and Harmony](Chords-and-Harmony.md) for details.

## Dynamics in Note Streams

Dynamic markings set the velocity of following notes:

```flow
timesig 4/4 {
    Sequence dynamic = | ff C4 D4 pp E4 F4 |
}
```

| Marking | Velocity | Description |
|---------|----------|-------------|
| `ppp` | ~0.1 | Pianississimo |
| `pp` | ~0.2 | Pianissimo |
| `p` | ~0.35 | Piano |
| `mp` | ~0.5 | Mezzo-piano |
| `mf` | ~0.63 | Mezzo-forte (default) |
| `f` | ~0.75 | Forte |
| `ff` | ~0.875 | Fortissimo |
| `fff` | ~1.0 | Fortississimo |

Inline `cresc` and `decresc` create gradual transitions between dynamic levels:

```flow
timesig 4/4 {
    Sequence swell = | pp C4 cresc D4 E4 ff F4 |
}
```

## Articulation in Note Streams

```flow
timesig 4/4 {
    Sequence accented = | C4q> D4q E4q F4q> |       Note: > = accent
    Sequence staccato = | C4q stacc D4q E4q F4q |   Note: staccato
    Sequence tenuto = | C4q ten D4q E4q F4q |       Note: tenuto
    Sequence marcato = | C4q marc D4q E4q F4q |     Note: marcato
}
```

## Ghost Notes

Very soft, ornamental notes with velocity ~0.15:

```flow
timesig 4/4 {
    Sequence ghosty = | C4 (ghost D4) E4 F4 |
}
```

## Grace Notes

Quick ornamental note (32nd duration) before the main note:

```flow
timesig 4/4 {
    Sequence graceful = | (grace B3) C4 D4 E4 F4 |
}
```

## Random Choice

Pick a random note from options:

```flow
timesig 4/4 {
    Note: Uniform random
    Sequence random = | (? C4 E4 G4) (? C4 E4 G4) (? C4 E4 G4) (? C4 E4 G4) |

    Note: Weighted random (weights as percentages)
    Sequence weighted = | (? C4:50 E4:30 G4:20) (? C4:50 E4:30 G4:20) _ _ |

    Note: Seeded random (deterministic)
    Sequence seeded = | (?? C4 E4 G4) (?? D4 F4 A4) (?? E4 G4 B4) (?? C4 E4 G4) |

    Note: Random with rests as options
    Sequence sparse = | (? C4 _) (? E4 _) (? G4 _) (? C4 _) |
}
```

Use `(??set 42)` to set a seed and `(??reset)` to reset for reproducibility.

## Variable References

Use variables inside note streams:

```flow
Note root = C4
timesig 4/4 {
    Sequence mel = | root D4 E4 F4 |
}
```

## Multi-Bar Streams

Separate bars with `|`:

```flow
timesig 4/4 {
    Sequence twoBar = | C4 D4 E4 F4 | G4 A4 B4 C5 |
    (print (str twoBar))
}
```

## Pickup Bars

A bar before the first `|` is treated as a pickup (anacrusis) bar:

```flow
timesig 4/4 {
    Sequence withPickup = E4q F4q | G4 A4 B4 C5 |
}
```

## Context Requirement

Note streams need at minimum a `timesig` context. For best results, wrap them in `tempo`, `timesig`, and `key` blocks:

```flow
tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Sequence mel = | C4 D4 E4 F4 |
        }
    }
}
```

## See Also

- [Musical Context](Musical-Context.md) - Setting tempo, key, and time signature
- [Chords and Harmony](Chords-and-Harmony.md) - Chord notation and roman numerals
- [Dynamics and Expression](Dynamics-and-Expression.md) - Dynamics and articulation details
- [Pattern Transforms](Pattern-Transforms.md) - Transforming sequences
