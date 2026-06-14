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
| `ppp` | 0.125 | Pianississimo |
| `pp`  | 0.25  | Pianissimo |
| `p`   | 0.375 | Piano |
| `mp`  | 0.5   | Mezzo-piano |
| `mf`  | 0.625 | Mezzo-forte (default) |
| `f`   | 0.75  | Forte |
| `ff`  | 0.875 | Fortissimo |
| `fff` | 1.0   | Fortississimo |
| `sfz` | 0.95  | Sforzando — sets velocity only in note streams (see note below) |
| `fp`  | 0.75  | Forte-piano |

Dynamic markings are **sticky**: once set, they apply to every following note in the stream until the next marking. They propagate through pitch transforms (`transpose`, `retrograde`, etc.) — see [Velocity Preservation](#velocity-preservation) below.

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
    Sequence stac  = | C4q stacc D4q E4q F4q |  Note: staccato (short)
    Sequence ten   = | C4q ten   D4q E4q F4q |  Note: tenuto (sustained)
    Sequence marc  = | C4q marc  D4q E4q F4q |  Note: marcato (strongly accented)
    Sequence legArt = | C4q leg  D4q E4q F4q |  Note: legato (overlapping)
}
```

| Articulation | Keyword | Locked envelope effect |
|-------------|---------|--------|
| Accent | `>` (suffix) | +0.30 velocity (clamped) |
| Staccato | `stacc` | 25% duration, sustain = 0, release × 0.5 |
| Marcato | `marc` | 25% duration + Accent's +0.30 velocity boost |
| Tenuto | `ten` | 100% duration, release × 1.2 (soft tail) |
| Legato | `leg` | 110% duration + crossfade overlap into next note |
| Sforzando | `sfz` (dynamic) | Velocity 0.95 + 1.5×→1.0× envelope spike over the first 15% of frames (spike fires only via the `Articulation.Sforzando` internal path; see note below) |

The envelope rules are **locked** across all 9 shipping synthesizers (piano, brass, sax, drums, bell, flute, organ, strings, wavetable) — drums opt out of articulation shaping. The sampled-instrument path (piano, brass, sax, strings, flute, bell, SFZ) additionally applies per-articulation A/D/S/R multipliers on top of the locked shape, so e.g. a sampled-piano staccato gets the duration cut plus a brighter decay shaping.

> The per-note `leg` articulation is distinct from the `legato(seq, overlap)` transform — the articulation drives envelope shaping per note; the transform sets `DurationOverlap` for the whole sequence. They compose: a note with `leg` AND `legato(seq, 0.5)` applied renders at 1.0 × 1.10 × 1.5 = 1.65 of its authored duration.

> **`sfz` as a dynamic marking vs. articulation:** Writing `| sfz C4q |` in a note stream sets the note's velocity to 0.95 via the dynamic-marking path (`TryParseDynamicMarking`). The 1.5×→1.0× envelope spike in the table above fires only through the `Articulation.Sforzando` internal enum path — that path is not currently exposed as an in-stream prefix keyword. In practice, `| sfz C4q |` gives you the loud attack; the envelope spike shape is applied by the synthesizer only for notes that arrive with `Articulation.Sforzando` set.

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

Two flavors of velocity jitter ship:

### humanize (uniform)

Adds subtle random velocity variation for a natural, "human-played" feel. Uniform distribution, shared (non-deterministic) RNG — frozen for backward compatibility:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Sequence human = mel -> humanize 0.2
    Note: amount 0.0-1.0 controls the variation range
    Note: 0.2 = up to 4% velocity variation (0.2 * 20%)
}
```

### humanizeGaussian (Box-Muller, seeded)

A deterministic, Gaussian-distributed alternative. Takes an explicit seed, so two runs at the same seed are byte-identical. Recurses correctly into voice blocks:

```flow
timesig 4/4 {
    Sequence mel = | {voice C4w} {voice E4q G4q B4q C5q} |
    Sequence h1  = (humanizeGaussian mel 0.3 42)    Note: seed = 42
    Sequence h2  = (humanizeGaussian mel 0.3 42)    Note: identical to h1
}
```

Prefer `humanizeGaussian` for any sequence you plan to render reproducibly (showcases, regression tests, etc.).

## Gain vs. Volume

Two buffer-level gain primitives ship — they differ only in how they interpret their second argument, so the function name documents your intent:

- `gain(buf, dB)` — the second argument is in **decibels**. Negative attenuates, positive amplifies.
- `volume(buf, multiplier)` — the second argument is a **linear multiplier**. `0.5` = half-amplitude; `2.0` = double-amplitude.

```flow
use "@audio"

Note: snippet — rendered must already be defined (e.g. from renderSong or createSineTone)
Buffer quieter = rendered -> gain -6dB         Note: half loudness (perceptual)
Buffer doubled = rendered -> volume 2.0        Note: double the sample magnitude
```

Both emit clipping warnings to stderr when post-multiplication samples exceed 1.0. `volume` rejects negative values — use `gain` for dB attenuation.

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
            Buffer result = (renderSong piece "piano")
        }
    }
}
```

## See Also

- [Note Streams](Note-Streams.md) - Inline dynamics and articulation syntax
- [Pattern Transforms](Pattern-Transforms.md) - All transform functions
- [Musical Context](Musical-Context.md) - Dynamics context blocks
- [Generative Music](Generative.md) - `vary` for stochastic expression
