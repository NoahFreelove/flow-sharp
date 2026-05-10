# Pattern Transforms

Pattern transforms modify sequences in musically meaningful ways. They operate on `Sequence` values and return new `Sequence` values, making them chainable with the flow operator.

## transpose

Shifts all notes by a number of semitones:

```flow
use "@std"

timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |

    Note: Up 2 semitones (C->D, D->E, E->F#, F->G)
    Sequence up2 = mel -> transpose +2st

    Note: Down 3 semitones
    Sequence down3 = mel -> transpose -3st
}
```

Transposition works with both semitone (`+2st`) and cent (`+200c`) values. Cent values are rounded to the nearest semitone.

**Music theory**: Transposition preserves the interval relationships between notes while shifting the entire passage to a new pitch level.

## invert

Mirrors the pitch intervals around the first note:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Sequence inv = (invert mel)
    Note: C4 stays, D4->Bb3, E4->Ab3, F4->G3
    Note: intervals that went UP now go DOWN by the same amount
}
```

**Music theory**: Inversion flips the melodic contour. Where the original rises, the inversion falls by the same interval, and vice versa.

## retrograde

Reverses the order of notes:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Sequence ret = (retrograde mel)
    Note: F4 E4 D4 C4
}
```

**Music theory**: Retrograde plays the melody backwards. Combined with inversion, it creates a retrograde inversion — a technique used extensively in serial music.

## augment

Doubles the duration of each note (moving toward whole notes):

```flow
timesig 4/4 {
    Sequence mel = | C4q D4q E4q F4q |
    Sequence aug = (augment mel)
    Note: each quarter becomes a half note
}
```

**Music theory**: Augmentation stretches the rhythm, making the passage feel slower and more expansive.

## diminish

Halves the duration of each note (moving toward 32nd notes):

```flow
timesig 4/4 {
    Sequence mel = | C4h D4h |
    Sequence dim = (diminish mel)
    Note: each half becomes a quarter note
}
```

**Music theory**: Diminution compresses the rhythm, making the passage feel faster and more energetic.

## up / down

Shifts notes by whole octaves:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |

    Sequence high = mel -> up 1     Note: C5 D5 E5 F5
    Sequence low = mel -> down 1    Note: C3 D3 E3 F3
    Sequence veryHigh = mel -> up 2 Note: C6 D6 E6 F6
}
```

**Music theory**: Octave displacement changes the register while preserving the pitch class. Useful for creating bass lines from melodies or vice versa.

## repeat

Repeats a sequence N times:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |

    Note: Simple repeat
    Sequence rep = mel -> repeat 2
    Note: plays the melody twice

    Note: Repeat with cumulative transposition
    Sequence rising = mel -> repeat 3 +4st
    Note: plays 3 times, each time transposed 4 semitones higher
}
```

**Music theory**: Repetition with transposition creates sequence patterns — a fundamental compositional technique where a musical idea is restated at different pitch levels.

## concat

Joins two sequences end-to-end:

```flow
timesig 4/4 {
    Sequence a = | C4 D4 E4 F4 |
    Sequence b = | G4 A4 B4 C5 |
    Sequence joined = (concat a b)
}
```

## Chaining Transforms

Transforms can be chained with the flow operator or nested calls:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |

    Note: Transpose then retrograde (via nesting)
    Sequence chained = (retrograde (transpose mel +5st))

    Note: Up an octave then repeat (via pipe)
    Sequence high = mel -> up 1 -> repeat 2
}
```

## Dynamic Transforms

These transforms modify velocity (dynamics) rather than pitch:

### crescendo

Linear velocity increase across the sequence:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Sequence growing = mel -> crescendo 0.25 0.875
    Note: velocity rises from 0.25 (pp) to 0.875 (ff)
}
```

### decrescendo

Linear velocity decrease:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Sequence fading = mel -> decrescendo 0.875 0.25
}
```

### swell

Velocity rises to a peak in the middle then falls:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 G4 A4 B4 C5 |
    Sequence swelled = mel -> swell 0.25 0.875
    Note: starts soft, peaks in the middle, ends soft
}
```

## Tempo Expression Transforms

### ritardando

Simulates a gradual slowdown via velocity reduction:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Sequence slowing = mel -> ritardando 0.7
}
```

### accelerando

Simulates a gradual speedup via velocity increase:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Sequence speeding = mel -> accelerando 0.7
}
```

### fermata

Doubles the duration of a specific note (by index):

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Sequence held = mel -> fermata 2
    Note: the 3rd note (index 2, E4) is held longer
}
```

## Humanize

Adds random velocity variation for a more natural, human-played feel:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Sequence human = mel -> humanize 0.2
    Note: amount 0.0-1.0, controls variation range (0-20%)
}
```

## Trill

Rapid alternation between a note and an upper neighbor:

```flow
timesig 4/4 {
    Sequence mel = | C4h E4h |
    Sequence trilled = mel -> trill +2st
    Note: each note alternates with the note 2 semitones above
}
```

## Tremolo

Repeats each note rapidly N times with proportionally shorter duration:

```flow
timesig 4/4 {
    Sequence mel = | C4h E4h |
    Sequence trem = mel -> tremolo 4
    Note: each note repeated 4 times at 1/4 the duration
}
```

## Transform Summary

| Transform | Syntax | Effect |
|-----------|--------|--------|
| `transpose` | `mel -> transpose +Nst` | Shift pitch by semitones |
| `invert` | `(invert mel)` | Mirror intervals around first note |
| `retrograde` | `(retrograde mel)` | Reverse note order |
| `augment` | `(augment mel)` | Double durations |
| `diminish` | `(diminish mel)` | Halve durations |
| `up` | `mel -> up N` | Shift up N octaves |
| `down` | `mel -> down N` | Shift down N octaves |
| `repeat` | `mel -> repeat N` | Repeat N times |
| `repeat` | `mel -> repeat N +Mst` | Repeat with transposition |
| `concat` | `(concat a b)` | Join sequences |
| `crescendo` | `mel -> crescendo start end` | Rising velocity |
| `decrescendo` | `mel -> decrescendo start end` | Falling velocity |
| `swell` | `mel -> swell edge peak` | Rise-then-fall velocity |
| `ritardando` | `mel -> ritardando amount` | Gradual slowdown |
| `accelerando` | `mel -> accelerando amount` | Gradual speedup |
| `fermata` | `mel -> fermata index` | Hold note at index |
| `humanize` | `mel -> humanize amount` | Random velocity variation |
| `trill` | `mel -> trill +Nst` | Rapid alternation |
| `tremolo` | `mel -> tremolo N` | Rapid repetition |

## See Also

- [Flow Operator](Flow-Operator.md) - Chaining transforms with `->`
- [Dynamics and Expression](Dynamics-and-Expression.md) - Dynamics and articulation
- [Note Streams](Note-Streams.md) - Creating sequences to transform
