# Pattern Transforms

Pattern transforms modify sequences in musically meaningful ways. They operate on `Sequence` values and return new `Sequence` values (or `Buffer`s for render-level transforms), making them chainable with the flow operator.

## Pitch Transforms

### transpose

Shifts all notes by a number of semitones or cents:

```flow
use "@std"

timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |

    Note: up 2 semitones
    Sequence up2 = mel -> transpose +2st

    Note: down 3 semitones
    Sequence down3 = mel -> transpose -3st

    Note: cents (rounded to nearest semitone)
    Sequence upCents = mel -> transpose +200c
}
```

### invert

Mirrors the pitch intervals around the first note:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Sequence inv = (invert mel)
    Note: intervals that went UP now go DOWN by the same amount
}
```

### retrograde

Reverses the order of notes:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
    Sequence ret = (retrograde mel)    Note: F4 E4 D4 C4
}
```

### up / down

Shifts notes by whole octaves:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |

    Sequence high     = mel -> up 1       Note: C5 D5 E5 F5
    Sequence low      = mel -> down 1     Note: C3 D3 E3 F3
    Sequence veryHigh = mel -> up 2       Note: C6 D6 E6 F6
}
```

## Duration Transforms

### augment

Doubles the duration of each note (toward whole notes):

```flow
timesig 4/4 {
    Sequence mel = | C4q D4q E4q F4q |
    Sequence aug = (augment mel)        Note: each quarter → half
}
```

### diminish

Halves the duration of each note (toward 32nd notes):

```flow
timesig 4/4 {
    Sequence mel = | C4h D4h |
    Sequence dim = (diminish mel)       Note: each half → quarter
}
```

## Repetition

### repeat

Repeats a sequence N times, optionally with cumulative transposition:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |

    Sequence twice  = mel -> repeat 2
    Sequence rising = mel -> repeat 3 +4st    Note: 3x, each 4 semitones higher
}
```

### concat

Joins two sequences end-to-end:

```flow
timesig 4/4 {
    Sequence a = | C4 D4 E4 F4 |
    Sequence b = | G4 A4 B4 C5 |
    Sequence joined = (concat a b)
}
```

## Dynamic Transforms

These transforms modify velocity rather than pitch.

### crescendo / decrescendo

Linear velocity ramps:

```flow
timesig 4/4 {
    Sequence mel     = | C4 D4 E4 F4 |
    Sequence growing = mel -> crescendo 0.25 0.875
    Sequence fading  = mel -> decrescendo 0.875 0.25
}
```

### swell

Rise-then-fall velocity curve:

```flow
timesig 4/4 {
    Sequence mel     = | C4 D4 E4 F4 G4 A4 B4 C5 |
    Sequence swelled = mel -> swell 0.25 0.875
    Note: starts soft, peaks in the middle, ends soft
}
```

## Tempo-Expression Transforms

These simulate tempo change via velocity shaping (the sequence stays metrically aligned).

### ritardando / accelerando

```flow
timesig 4/4 {
    Sequence slow = | C4 D4 E4 F4 | -> ritardando 0.7
    Sequence fast = | C4 D4 E4 F4 | -> accelerando 0.7
}
```

For actual tempo interpolation in the rendered audio, see [`tempoRamp`](#tempo-ramp-buffer-level) below, or the [`rit` / `accel` context blocks](Musical-Context.md).

### fermata

Doubles the duration of a specific note (by index):

```flow
timesig 4/4 {
    Sequence mel  = | C4 D4 E4 F4 |
    Sequence held = mel -> fermata 2      Note: index 2 = E4, held longer
}
```

## Articulation Transforms

These shape how notes connect or detach without changing their authored durations.

### legato

Sets a `DurationOverlap` on every note in the sequence — overlap is a multiplier (`0.0` = no overlap, `0.5` = each note runs 50% into the next):

```flow
timesig 4/4 {
    Sequence mel = | C4q D4q E4q F4q |
    Sequence smooth = mel -> legato 0.5
}
```

> Distinct from the per-note `leg` articulation in [note streams](Note-Streams.md), which drives envelope shaping. Both compose.

### portamento

Adds a glide time (in milliseconds) between consecutive notes — the engine sweeps pitch over `glideMs` at the start of each note:

```flow
timesig 4/4 {
    Sequence mel = | C4q E4q G4q C5q |
    Sequence glissy = mel -> portamento 50ms
}
```

## Quantize

Snap notes to a metric grid with adjustable strength and optional swing. Requires a `timesig` context (the active time signature drives subdivision math):

```flow
use "@std"

timesig 4/4 {
    Sequence loose = | C4 D4q. E4e F4 |

    Note: hard-quantize to eighth-note grid
    Sequence tight = (quantize loose e 1.0 0.0)

    Note: 50% pull to sixteenths, with light swing
    Sequence laidback = (quantize loose s 0.5 0.2)
}
```

`strength` is clamped to `[0, 1]` and `swing` to `[-1, 1]`. At `strength=0, swing=0` the transform is a byte-identical no-op.

## Texture and Ornamentation

### humanize / humanizeGaussian

Two flavors of velocity jitter:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |

    Note: uniform jitter, frozen non-deterministic RNG
    Sequence h1 = mel -> humanize 0.2

    Note: Gaussian (Box-Muller), seeded — deterministic
    Sequence h2 = (humanizeGaussian mel 0.2 42)
}
```

`humanizeGaussian` recurses correctly into voice blocks; prefer it for reproducible renders. See [Dynamics and Expression](Dynamics-and-Expression.md#humanize) for the full discussion.

### trill

Rapid alternation between each note and its upper neighbor at a specified interval:

```flow
timesig 4/4 {
    Sequence mel     = | C4h E4h |
    Sequence trilled = mel -> trill +2st
}
```

### tremolo

Rapid repetition of each note:

```flow
timesig 4/4 {
    Sequence mel  = | C4h E4h |
    Sequence trem = mel -> tremolo 4     Note: each note repeated 4 times
}
```

## Generative / Stochastic

### vary

Mutate a sequence randomly. Six overloads cover the combinations of mutation type, seed, and scale constraint — supported mutation types are `"pitch"`, `"rhythm"`, `"rest"`, and `"velocity"`:

```flow
use "@std"

Sequence s = | C4 D4 E4 F4 G4 |

Sequence v1 = s -> vary(0.3)
Sequence v2 = (vary s 0.5 "pitch")
Sequence v3 = (vary s 0.5 "pitch" 42)                Note: seeded
Sequence v4 = (vary s 0.5 "pitch" "Cmajor")          Note: diatonic
Sequence v5 = (vary s 0.5 "pitch" "Cmajor" 42)       Note: diatonic + seeded
Sequence v6 = (vary s 0.5 "rhythm" 42)               Note: seeded rhythm mutation
```

See [Generative Music](Generative.md) for details.

### polyrhythm (buffer-level)

Overlay two sequences with different time signatures. Returns a `Buffer`:

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 3/4 {
        Sequence waltz = | A3 E4 E4 |
        timesig 4/4 {
            Sequence quarters = | C4 C4 C4 C4 |
            Buffer mixed = (polyrhythm waltz quarters)
            (exportWav mixed "poly.wav")
        }
    }
}
```

## Tidal-Style Combinators

Beyond the core transforms above, a separate `@patterns` module ships 13 Tidal-style combinators (`every`, `fast`, `slow`, `chunk`, `phase`, `rev`, `iter`, `palindrome`, `jux`, `superimpose`, `sometimes`, `degrade`, `sparseSeq`). They operate on whole sequences in units of bars and chain naturally with `->`:

```flow
use "@patterns"

timesig 4/4 {
    Sequence base = | C4 D4 E4 F4 | G4 A4 B4 C5 |

    Note: every 4th bar, double-speed it
    Sequence shifted = base -> (every 4 (fn Sequence s => (fast s 2.0)))

    Note: probabilistic drop
    Sequence sparse  = (sometimes 0.3 (fn Sequence s => (degrade s)) base)
}
```

See [Generative Music](Generative.md) for the full list and per-combinator docs.

## Tempo Ramp (buffer-level)

### tempoRamp

Renders a sequence with a linearly interpolated tempo from a start to an end BPM. Returns a `Buffer`:

```flow
use "@std"
use "@audio"

timesig 4/4 {
    key Cmajor {
        Sequence seq = | C4 D4 E4 F4 |

        Note: ritardando (120 → 80)
        Buffer slowing   = (tempoRamp seq 120.0 80.0)

        Note: accelerando (80 → 120)
        Buffer speeding  = (tempoRamp seq 80.0 120.0)

        Note: with instrument override
        Buffer instrBuf  = (tempoRamp seq 120.0 80.0 "piano")
    }
}
```

Ritardando produces more frames than a constant-fast render; accelerando produces fewer frames than a constant-slow render.

## Chaining Transforms

Transforms chain naturally with the flow operator:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |

    Sequence chain1 = (retrograde (transpose mel +5st))
    Sequence chain2 = mel -> up 1 -> repeat 2 -> humanize 0.1
}
```

## Transform Summary

| Transform | Type | Syntax | Effect |
|-----------|------|--------|--------|
| `transpose` | Sequence | `mel -> transpose +Nst` | Shift pitch by semitones / cents |
| `invert` | Sequence | `(invert mel)` | Mirror intervals around first note |
| `retrograde` | Sequence | `(retrograde mel)` | Reverse note order |
| `augment` | Sequence | `(augment mel)` | Double durations |
| `diminish` | Sequence | `(diminish mel)` | Halve durations |
| `up` | Sequence | `mel -> up N` | Up N octaves |
| `down` | Sequence | `mel -> down N` | Down N octaves |
| `repeat` | Sequence | `mel -> repeat N` | Repeat N times |
| `repeat` | Sequence | `mel -> repeat N +Mst` | Repeat with transposition |
| `concat` | Sequence | `(concat a b)` | Join sequences |
| `crescendo` | Sequence | `mel -> crescendo start end` | Rising velocity |
| `decrescendo` | Sequence | `mel -> decrescendo start end` | Falling velocity |
| `swell` | Sequence | `mel -> swell edge peak` | Rise-then-fall velocity |
| `ritardando` | Sequence | `mel -> ritardando amount` | Slowdown feel (via velocity) |
| `accelerando` | Sequence | `mel -> accelerando amount` | Speedup feel (via velocity) |
| `fermata` | Sequence | `mel -> fermata index` | Hold note at index |
| `legato` | Sequence | `mel -> legato overlap` | Set `DurationOverlap` per note |
| `portamento` | Sequence | `mel -> portamento 50ms` | Pitch glide between notes |
| `quantize` | Sequence | `(quantize mel res strength swing)` | Snap to metric grid (needs `timesig`) |
| `humanize` | Sequence | `mel -> humanize amount` | Random velocity variation (uniform, frozen RNG) |
| `humanizeGaussian` | Sequence | `(humanizeGaussian mel amount seed)` | Gaussian velocity jitter (seeded) |
| `trill` | Sequence | `mel -> trill +Nst` | Rapid alternation |
| `tremolo` | Sequence | `mel -> tremolo N` | Rapid repetition |
| `vary` | Sequence | `mel -> vary(prob)` | Random mutation (6 overloads) |
| `polyrhythm` | Buffer | `(polyrhythm a b)` | Overlay sequences (LCM cycle) |
| `tempoRamp` | Buffer | `(tempoRamp seq startBpm endBpm)` | Rendered tempo interpolation |
| `every` / `fast` / `slow` / ... | Sequence | `(every n cb mel)` etc. | [Tidal-style combinators](Generative.md) (opt-in via `use "@patterns"`) |

## See Also

- [Flow Operator](Flow-Operator.md) - Chaining with `->`
- [Dynamics and Expression](Dynamics-and-Expression.md) - Dynamics and articulation
- [Note Streams](Note-Streams.md) - Creating sequences
- [Generative Music](Generative.md) - `vary`, `euclidean`, random choice
- [Musical Context](Musical-Context.md) - `rit` / `accel` context blocks
