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

Flow note literals use `[A-G][octave][alteration]`:

| Component | Options | Example |
|-----------|---------|---------|
| Pitch | A, B, C, D, E, F, G (uppercase) | `C`, `D`, `G` |
| Octave | 0-10 (required when an alteration is used) | `C4`, `A3`, `G5` |
| Alteration | `+` sharp, `-` flat, `++` double sharp, `--` double flat | `C4+`, `B3-`, `F4++` |

```flow
timesig 4/4 {
    Sequence sharps = | C4+ D4+ F4+ G4+ |
    Sequence flats  = | B3- E4- A4- D5- |
}
```

> **Note**: Chord symbols use a different accidental convention: `s` for sharp and `f` for flat (e.g., `Csmaj7`, `Bfm`). See [Chords and Harmony](Chords-and-Harmony.md).

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
    Sequence mixed = | C4h D4q E4q |                         Note: 2 + 1 + 1 = 4 beats
    Sequence fast  = | C4e D4e E4e F4e G4e A4e B4e C5e |     Note: 8 eighths
}
```

### Auto-Fit Duration

Notes without a duration suffix are sized to fill the bar evenly:

```flow
timesig 4/4 {
    Sequence four  = | C4 D4 E4 F4 |    Note: each becomes a quarter
    Sequence three = | C4 E4 G4 |       Note: each becomes ~1.33 beats
}

timesig 3/4 {
    Sequence waltz = | C4 E4 G4 |       Note: each becomes a quarter
}
```

## Dotted Notes

Append `.` after the duration suffix to extend a note by 50%:

```flow
timesig 4/4 {
    Sequence dotted = | C4q. D4e E4h |    Note: 1.5 + 0.5 + 2 = 4 beats
}
```

## Rests

Use `_` for rests. Rests can have duration suffixes too:

```flow
timesig 4/4 {
    Sequence auto     = | C4 _ E4 F4 |         Note: auto-fit rest
    Sequence explicit = | C4q _q E4q F4q |     Note: quarter rest
}
```

## Tied Notes

Use `~` after a note to tie it into the next note (sustain without re-attack):

```flow
timesig 4/4 {
    Sequence tied = | C4h~ C4h |    Note: C4 sustains across the tie
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
    Sequence prog = | Cmaj7 Am7 Dm7 G7 |
}
```

See [Chords and Harmony](Chords-and-Harmony.md) for all chord types.

## Roman Numerals

Within a `key` context, use roman numerals for scale-degree chords:

```flow
key Cmajor {
    timesig 4/4 {
        Sequence mjr = | I IV V I |
        Sequence mnr = | ii V7 I |
    }
}
```

For automatic voice leading, see [Chord Progressions](Chord-Progressions.md).

## Dynamics in Note Streams

Dynamic markings set the velocity of following notes:

```flow
timesig 4/4 {
    Sequence dynamic = | ff C4 D4 pp E4 F4 |
}
```

| Marking | Velocity | Name |
|---------|----------|------|
| `ppp` | ~0.1 | Pianississimo |
| `pp` | ~0.2 | Pianissimo |
| `p` | ~0.35 | Piano |
| `mp` | ~0.5 | Mezzo-piano |
| `mf` | ~0.63 | Mezzo-forte (default) |
| `f` | ~0.75 | Forte |
| `ff` | ~0.875 | Fortissimo |
| `fff` | ~1.0 | Fortississimo |

### Inline `cresc` / `decresc`

Use `cresc` and `decresc` between dynamic endpoints. Unmarked notes get interpolated velocities:

```flow
timesig 4/4 {
    Sequence growing = | pp C4 cresc D4 E4 ff F4 |
    Sequence fading  = | ff G4 decresc F4 E4 pp D4 |
}
```

## Articulation in Note Streams

```flow
timesig 4/4 {
    Sequence accented  = | C4q> D4q E4q F4q> |         Note: > = accent (suffix)
    Sequence staccato  = | C4q stacc D4q E4q F4q |
    Sequence tenuto    = | C4q ten D4q E4q F4q |
    Sequence marcato   = | C4q marc D4q E4q F4q |
}
```

| Articulation | Keyword | Effect |
|-------------|---------|--------|
| Accent | `>` (suffix) | Velocity bump |
| Staccato | `stacc` | Shortened (~50% duration) |
| Tenuto | `ten` | Full sustain |
| Marcato | `marc` | Accented + slightly shortened |

## Ghost Notes

Very soft, ornamental notes (velocity ~0.15):

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
    Note: uniform random
    Sequence random = | (? C4 E4 G4) (? C4 E4 G4) (? C4 E4 G4) (? C4 E4 G4) |

    Note: weighted random (relative weights)
    Sequence weighted = | (? C4:50 E4:30 G4:20) (? C4:50 E4:30 G4:20) _ _ |

    Note: seeded random (deterministic)
    Sequence seeded = | (?? C4 E4 G4) (?? D4 F4 A4) (?? E4 G4 B4) (?? C4 E4 G4) |

    Note: rests as options
    Sequence sparse = | (? C4 _) (? E4 _) (? G4 _) (? C4 _) |
}
```

Use `(??set 42)` to set a seed and `(??reset)` to restore it. See [Generative Music](Generative.md) for more.

## Variable References

Use variables inside note streams. Lowercase identifiers are treated as variable references:

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

Prefix the first bar with the `pickup` keyword to mark it as an anacrusis:

```flow
timesig 4/4 {
    Sequence withPickup = pickup | E4q F4q | G4 A4 B4 C5 |
}
```

## Context Requirement

Note streams need at minimum a `timesig` context to determine bar length. For most music you also want `tempo` and `key`:

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
- [Chord Progressions](Chord-Progressions.md) - Voice-led `progression | ... |` syntax
- [Dynamics and Expression](Dynamics-and-Expression.md) - Dynamics and articulation details
- [Pattern Transforms](Pattern-Transforms.md) - Transforming sequences
- [Generative Music](Generative.md) - Random, Euclidean, variation
