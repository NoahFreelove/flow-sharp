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
| Alteration | `+` / `#` sharp, `-` / `b` flat, `++` double sharp, `--` double flat | `C4+`, `B3-`, `F4##`, `Bb4` |

```flow
timesig 4/4 {
    Sequence sharps = | C4+ D4+ F4+ G4+ |
    Sequence flats  = | B3- E4- A4- D5- |
}
```

Accidentals stack arbitrarily — the net pitch is `(# count + + count) - (b count + - count)`. So `F##4` is F double-sharp at octave 4, and `Bb-+bbb` resolves the same way (4 flats minus 1 sharp = triple-flat B). Note literals also include the multi-letter enharmonic edges `E↔Fb` and `B↔Cb`.

> **Note**: Chord symbols use a different accidental convention: `s` for sharp and `f` for flat (e.g., `Csmaj7`, `Bfm`). See [Chords and Harmony](Chords-and-Harmony.md).

### H-as-B Alias (German Notation)

With `enable hAsB;` at the top of a file, `H` aliases to `B` and `Hb` (or `H-`) aliases to `Bb` — matching the German convention found in Bach et al.:

```flow
enable hAsB;
use "@std"

timesig 4/4 {
    Sequence bWvWunderbar = | H4 Hb4 H3 |   Note: same as | B4 Bb4 B3 |
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
| `x` | Sixty-fourth | 0.0625 |
| `y` | 128th | 0.03125 |

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

Use `_` for rests. A bare `_` auto-fits to fill its share of the bar, just like a note without a duration suffix. To give a rest a specific duration, place the suffix on the surrounding notes and let `_` fill the gap, or use the note value directly on a real note before or after:

```flow
timesig 4/4 {
    Sequence auto = | C4q _ E4q F4q |    Note: _ fills the remaining beat (quarter)
}
```

> **Note**: The lexer merges `_q`, `_h`, etc. into a single identifier, not a rest-plus-suffix token. Only a bare `_` is recognised as a rest inside note streams.

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

## Tuplets

Wrap notes in `{ }` with an explicit ratio `N:M` followed by a duration suffix to play N notes in the time of M:

```flow
timesig 4/4 {
    Note: triplet — 3 quarters in the time of 2
    Sequence trip = | {3:2 C4 E4 G4}q D4q E4q F4q |

    Note: quintuplet of eighths
    Sequence five = | {5:4 C4 D4 E4 F4 G4}e D4e E4e |
}
```

Or use the **music21 shorthand** `{N ...}q` — the ratio is inferred from a small lookup table (3->2, 5->4, 6->4, 7->4, 9->8, etc.; counts 2..11 supported):

```flow
timesig 4/4 {
    Sequence trip = | {3 C4 E4 G4}q D4q E4q F4q |    Note: same as {3:2 ...}q
}
```

Per-note tuplet ratios also work — useful inside otherwise straight bars:

```flow
timesig 4/4 {
    Note: C4 lasts the time of a /3:2 (triplet eighth)
    Sequence mixed = | C4/3:2q D4q E4q F4q |
}
```

For arbitrary fractional durations, use `C4/N` — the note lasts 1/N of a whole note:

```flow
timesig 4/4 {
    Sequence twelfth = | C4/12 D4/12 E4/12 F4/12 G4/12 A4/12 |
}
```

## Voice Blocks (Polyphony)

A `{voice ...}` block inside a bar declares a parallel voice. Multiple voice blocks within the same `|...|` share the bar's onset and play simultaneously — this is the canonical way to write polyphony:

```flow
timesig 4/4 {
    Sequence twoVoices = | {voice C4w} {voice C5q D5q E5q F5q} |
    Note: a held whole-note bass under a quarter-note melody
}
```

Voice blocks render identically through both audio and MIDI export — each voice becomes its own track on disk. They can carry their own articulation (`stacc`, `leg`, `ten`, `marc`, `>`), but not inline dynamics — dynamic markings inside `{voice ...}` cause a parse error. Place a sticky dynamic before the outer `|` bar to set the level for all voices in that bar:

```flow
timesig 4/4 {
    Sequence chorale = | mf {voice C4h E4h} {voice E5q stacc G5q stacc B5q stacc D6q>} |
}
```

> Voice blocks may not be nested inside other voice blocks. Tuplets inside a voice block work fine.

## Named Chords in Note Streams

Use chord symbols directly:

```flow
timesig 4/4 {
    Sequence prog = | Cmaj7 Am7 Dm7 G7 |
}
```

### Chord + Duration Fusion

A chord symbol with a **letter-bearing quality** (`maj7`, `m7`, `dim7`, `sus4`, `m`, `add9`, …) fuses with a trailing duration letter exactly like a note literal does — `Cmaj7q` is the Cmaj7 chord as a quarter, no space needed. Dots and ties attach as usual:

```flow
timesig 4/4 {
    Sequence prog = | Cmaj7q Am7q Dm7q Fsmaj7q |
    Sequence dotted = | Cmaj7q. Am7e Dm7h |
}
```

> **Bare-digit qualities do not fuse.** `Bb7`, `G7`, `C6` etc. are read as a note + octave (`Bb7` = B-flat in octave 7), because a bare number is ambiguous with an octave. So `Bb7q` is the *note* Bb7 as a quarter — write `Bfdom7 q` (note the space) if you mean the chord. Letter qualities like `Bbm7q` are unambiguous and fuse fine.

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

Dynamic markings are **sticky** — once placed they set the velocity for all following notes in the bar (and downstream bars) until another marking is seen:

```flow
timesig 4/4 {
    Sequence dynamic = | ff C4 D4 pp E4 F4 |    Note: first two loud, last two soft
}
```

| Marking | Velocity | Name |
|---------|----------|------|
| `ppp` | 0.125 | Pianississimo |
| `pp`  | 0.25  | Pianissimo |
| `p`   | 0.375 | Piano |
| `mp`  | 0.5   | Mezzo-piano |
| `mf`  | 0.625 | Mezzo-forte (default) |
| `f`   | 0.75  | Forte |
| `ff`  | 0.875 | Fortissimo |
| `fff` | 1.0   | Fortississimo |
| `sfz` | 0.95  | Sforzando (spike + envelope shape — see [Dynamics and Expression](Dynamics-and-Expression.md)) |
| `fp`  | 0.75  | Forte-piano |

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
    Sequence accented  = | C4q> D4q E4q F4q> |             Note: > = accent (suffix)
    Sequence staccato  = | C4q stacc D4q E4q F4q |
    Sequence tenuto    = | C4q ten D4q E4q F4q |
    Sequence marcato   = | C4q marc D4q E4q F4q |
    Sequence legato    = | C4q leg D4q E4q F4q |
}
```

| Articulation | Keyword | Effect (locked envelope) |
|-------------|---------|--------|
| Accent | `>` (suffix) | +0.30 velocity (clamped) |
| Staccato | `stacc` | 25% duration, sustain = 0, release × 0.5 |
| Marcato | `marc` | 25% duration + Accent's +0.30 velocity boost |
| Tenuto | `ten` | 100% duration, release × 1.2 (soft tail) |
| Legato | `leg` | 110% duration + crossfade overlap into next note |
| Sforzando | `sfz` dynamic | 1.5×->1.0× envelope spike over first 15% of frames |

Articulation marks accept on the same note as a duration suffix. Per-note articulations and dynamics propagate through pitch transforms (`transpose`, `retrograde`, etc.) — see [Dynamics and Expression](Dynamics-and-Expression.md) for the full reference.

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

Multi-line bars work too. Adjacent `|` pipes at line wraps collapse charitably — no empty bars get inserted:

```flow
timesig 4/4 {
    Sequence longMelody =
        | C4 D4 E4 F4
        | G4 A4 B4 C5
        | D5 C5 B4 A4 |
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
