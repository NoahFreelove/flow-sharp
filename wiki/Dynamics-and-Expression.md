# Dynamics and Expression

Flow provides comprehensive support for musical expression through dynamics markings, articulation, ornaments, and velocity/tempo transforms.

## Dynamic Markings

Dynamic markings control note velocity (loudness). They can be used inline in note streams or as context blocks.

### Inline Dynamics

Place dynamic markings before notes to set their velocity:

```flow
timesig 4/4 {
    Sequence contrast = | ff C4 D4 pp E4 F4 |
    Note: C4 and D4 are loud, E4 and F4 are soft
}
```

| Marking | Velocity | Italian Name |
|---------|----------|--------------|
| `ppp` | ~0.10 | Pianississimo |
| `pp` | ~0.20 | Pianissimo |
| `p` | ~0.35 | Piano |
| `mp` | ~0.50 | Mezzo-piano |
| `mf` | ~0.63 | Mezzo-forte (default) |
| `f` | ~0.75 | Forte |
| `ff` | ~0.875 | Fortissimo |
| `fff` | ~1.0 | Fortississimo |

### Inline Crescendo/Decrescendo

Use `cresc` and `decresc` between dynamic markings for smooth transitions:

```flow
timesig 4/4 {
    Note: Gradual crescendo from pp to ff
    Sequence growing = | pp C4 cresc D4 E4 ff F4 |
    Note: D4 and E4 get interpolated velocities between pp and ff

    Note: Gradual decrescendo from ff to pp
    Sequence fading = | ff G4 decresc F4 E4 pp D4 |
}
```

### Dynamics Context Block

Set a default velocity for all notes in scope:

```flow
dynamics f {
    Note: All notes here default to forte
    Sequence loud = | C4 D4 E4 F4 |

    dynamics pp {
        Note: Inner block overrides to pianissimo
        Sequence soft = | C4 D4 E4 F4 |
    }
}
```

## Articulation

Articulation marks change how individual notes are played:

### Accent (`>`)

Append `>` to a note for emphasis (higher velocity):

```flow
timesig 4/4 {
    Sequence accented = | C4q> D4q E4q F4q> |
    Note: C4 and F4 are accented
}
```

### Named Articulations

Place articulation keywords before the affected note:

```flow
timesig 4/4 {
    Sequence stac = | C4q stacc D4q E4q F4q |  Note: staccato (short)
    Sequence ten = | C4q ten D4q E4q F4q |      Note: tenuto (sustained)
    Sequence marc = | C4q marc D4q E4q F4q |    Note: marcato (strongly accented)
}
```

| Articulation | Keyword | Effect |
|-------------|---------|--------|
| Staccato | `stacc` | Short, detached |
| Tenuto | `ten` | Full value, sustained |
| Marcato | `marc` | Strongly accented |
| Accent | `>` (suffix) | Emphasis |

## Ornaments

### Ghost Notes

Very soft notes (velocity ~0.15), used for subtle rhythmic feel:

```flow
timesig 4/4 {
    Sequence ghosty = | C4 (ghost D4) E4 F4 |
    Note: D4 is barely audible, like a "felt" note
}
```

Ghost notes default to a short (sixteenth) duration. They're commonly used in jazz and funk drumming/bass to add groove without overpowering the beat.

### Grace Notes

Quick ornamental notes (32nd duration, velocity ~0.5) placed before the main note:

```flow
timesig 4/4 {
    Sequence graceful = | (grace B3) C4 D4 E4 F4 |
    Note: B3 plays as a quick pick-up into C4
}
```

Grace notes are decorative pitches that "lean into" the following note.

### Trill

Rapid alternation between a note and its upper neighbor:

```flow
timesig 4/4 {
    Sequence mel = | C4h E4h |
    Sequence trilled = mel -> trill +2st
    Note: C4 alternates rapidly with D4 (2 semitones up)
    Note: E4 alternates rapidly with F#4
}
```

### Tremolo

Rapid repetition of each note:

```flow
timesig 4/4 {
    Sequence mel = | C4h E4h |
    Sequence trem = mel -> tremolo 4
    Note: each note repeated 4 times at 1/4 the original duration
}
```

## Velocity Transforms

### crescendo

Gradually increases velocity across the sequence:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Sequence growing = mel -> crescendo 0.25 0.875
    Note: linearly from pp (0.25) to ff (0.875)
}
```

### decrescendo

Gradually decreases velocity:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Sequence dying = mel -> decrescendo 0.875 0.25
}
```

### swell

Velocity rises to the middle then falls back:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 G4 A4 B4 C5 |
    Sequence swelled = mel -> swell 0.25 0.875
    Note: starts at 0.25, peaks at 0.875 in the middle, returns to 0.25
}
```

## Tempo Expression

### ritardando

Simulates a gradual slowdown (via velocity reduction):

```flow
timesig 4/4 {
    Sequence mel = | mf C5q B4q A4q G4q |
    Sequence slowing = mel -> ritardando 0.7
}
```

### accelerando

Simulates a gradual speedup (via velocity increase):

```flow
timesig 4/4 {
    Sequence mel = | mf C4q D4q E4q F4q |
    Sequence speeding = mel -> accelerando 0.7
}
```

### fermata

Doubles the duration of a specific note:

```flow
timesig 4/4 {
    Sequence mel = | mf C4q D4q E4q F4q |
    Sequence held = mel -> fermata 2
    Note: E4 (index 2) gets double duration
}
```

## Humanize

Adds subtle random velocity variation for a natural, "human-played" feel:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Sequence human = mel -> humanize 0.2
    Note: amount 0.0-1.0 controls the variation range
    Note: 0.2 = up to 4% velocity variation (0.2 * 20%)
}
```

## Velocity Preservation

Velocity and articulation are preserved through pitch transforms:

```flow
timesig 4/4 {
    Sequence expressive = | ff C4q> D4q pp E4q stacc F4q |

    Note: Transposing preserves dynamics and articulation
    Sequence transposed = expressive -> transpose +2st

    Note: Retrograde preserves them too
    Sequence reversed = (retrograde expressive)
}
```

## Combining Expression Techniques

```flow
use "@std"
use "@audio"

tempo 72 {
    timesig 4/4 {
        key Cmajor {
            section intro {
                Sequence phrase = | pp C4q E4q G4q C5q | -> crescendo 0.2 0.7
                Sequence melody = | mf E4q G4q B4q E5q | -> humanize 0.1
            }

            section development {
                Sequence theme = | f C4q> D4q E4q stacc F4q |
                Sequence variation = theme -> transpose +4st -> decrescendo 0.8 0.4
            }

            section climax {
                Sequence peak = | C4q E4q G4q C5q E5q G5q C6q C6q | -> swell 0.5 1.0
            }

            section ending {
                Sequence resolve = | mp G4q E4q C4h | -> ritardando 0.5
                Sequence finalChord = | pp C4w |
            }

            Song piece = [intro development climax ending]
            Buffer buf = (renderSong piece "piano")
        }
    }
}
```

## See Also

- [Note Streams](Note-Streams.md) - Inline dynamics and articulation syntax
- [Pattern Transforms](Pattern-Transforms.md) - All transform functions
- [Musical Context](Musical-Context.md) - Dynamics context blocks
