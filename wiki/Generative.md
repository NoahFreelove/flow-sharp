# Generative Music

Flow has several primitives for algorithmic and probabilistic music: Euclidean rhythms, weighted random choice, seeded randomness, sequence mutation (`vary`), and polyrhythm construction.

## Euclidean Rhythms

Distribute `k` hits evenly across `n` steps using the Bjorklund algorithm:

```flow
use "@std"

Sequence e38 = (euclidean 3 8 C4)
(print (str e38))    Note: X..X..X. style pattern

Sequence e58 = (euclidean 5 8 E4)
(print (str e58))
```

**Signature**: `(euclidean Int hits, Int steps, Note pitch) -> Sequence`

Common Euclidean patterns map to global rhythmic traditions:

| `hits` / `steps` | Feel |
|------------------|------|
| 3/8 | Cuban tresillo |
| 5/8 | West African bell pattern |
| 3/4 | Simple triplet |
| 7/16 | Afro-Cuban bembé |

## Random Choice in Note Streams

Pick a note at random from a set. This is syntax, not a function — it works only inside `| ... |` note streams.

### Uniform Random

`(? ...)` picks one of the listed notes with equal probability on each evaluation:

```flow
use "@std"

timesig 4/4 {
    Sequence random = | (? C4 E4 G4) (? C4 E4 G4) (? C4 E4 G4) (? C4 E4 G4) |
}
```

### Weighted Random

Append `:weight` to bias the selection:

```flow
timesig 4/4 {
    Sequence weighted = | (? C4:50 E4:30 G4:20) (? C4:50 E4:30 G4:20) _ _ |
}
```

Weights are relative, not percentages. `:50 :30 :20` is equivalent to `:5 :3 :2`.

### Including Rests

Rests (`_`) can be options too:

```flow
timesig 4/4 {
    Sequence sparse = | (? C4 _) (? E4 _) (? G4 _) (? C4 _) |
}
```

### Seeded Random

`(?? ...)` is a second, separately seeded RNG. It produces reproducible output once a seed is set.

```flow
use "@std"

(??set 42)
timesig 4/4 {
    Sequence seeded = | (?? C4 E4 G4) (?? D4 F4 A4) (?? E4 G4 B4) (?? C4 E4 G4) |
}
```

| Function | Effect |
|----------|--------|
| `(??set N)` | Set the seeded RNG state to `N` |
| `(??reset)` | Reset the seeded RNG to its initial state |
| `(??)` | Get a random `Float` from the seeded stream |
| `(?)` | Get a random `Float` from the unseeded stream |

## Sequence Mutation (`vary`)

`vary` applies random mutations to a sequence. You can choose what to mutate and whether to constrain output to a key.

```flow
use "@std"

Sequence s = | C4 D4 E4 F4 G4 |

Note: basic mutation (random type, 30% chance per note)
Sequence v1 = s -> vary(0.3)

Note: specific mutation type
Sequence v2 = (vary s 0.5 "pitch")
Sequence v3 = (vary s 0.5 "rhythm")
Sequence v4 = (vary s 0.5 "rest")
Sequence v5 = (vary s 0.5 "velocity")

Note: seeded (reproducible)
Sequence v6 = (vary s 0.5 42)

Note: seeded + type
Sequence v7 = (vary s 0.5 "pitch" 42)

Note: diatonic — constrain pitch mutations to a scale
Sequence v8 = (vary s 0.5 "pitch" "Cmajor")
Sequence v9 = (vary s 0.5 "pitch" "Cmajor" 42)
```

**Overloads**

| Signature | Description |
|-----------|-------------|
| `(Sequence, Double) -> Sequence` | Random mutation type |
| `(Sequence, Double, String) -> Sequence` | Specific type |
| `(Sequence, Double, Int) -> Sequence` | Seeded random type |
| `(Sequence, Double, String, Int) -> Sequence` | Seeded, specific type |
| `(Sequence, Double, String, String) -> Sequence` | Diatonic (type, key) |
| `(Sequence, Double, String, String, Int) -> Sequence` | Diatonic, seeded |

**Mutation types**

| Type | Effect |
|------|--------|
| `"pitch"` | Shift pitch up/down, optionally snapping to key |
| `"rhythm"` | Randomize note durations |
| `"rest"` | Replace note with rest |
| `"velocity"` | Randomize velocity |

## Polyrhythms

Overlay two sequences with different time signatures. `polyrhythm` figures out the cycle length (LCM of time signatures) and returns a mixed buffer:

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 3/4 {
        Sequence waltz = | A3 E4 E4 |
        timesig 4/4 {
            Sequence quarters = | C4 C4 C4 C4 |
            Buffer poly = (polyrhythm waltz quarters)
            (exportWav poly "polyrhythm.wav")
        }
    }
}
```

**Signatures**

| Signature | Description |
|-----------|-------------|
| `(polyrhythm Sequence, Sequence) -> Buffer` | Auto-align via LCM of time signatures |
| `(polyrhythm Sequence, Sequence, Int) -> Buffer` | Explicit beat count override |

## Combining Generative Techniques

Generative primitives compose cleanly:

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Note: a Euclidean hi-hat
            Sequence hat = (euclidean 5 8 C5)

            Note: a seeded melodic line
            (??set 7)
            Sequence lead = | (?? C4 E4 G4 B4) (?? D4 F4 A4) (?? E4 G4 B4) (?? F4 A4 C5) |

            Note: diatonic mutation on the lead
            Sequence varied = (vary lead 0.3 "pitch" "Cmajor" 7)

            section groove {
                Sequence a = hat
                Sequence b = varied
            }
            Song song = [groove*4]
            Buffer buf = (renderSong song "piano")
            (exportWav buf "generative.wav")
        }
    }
}
```

## See Also

- [Note Streams](Note-Streams.md) - Full note-stream syntax
- [Pattern Transforms](Pattern-Transforms.md) - Deterministic transforms
- [Chords and Harmony](Chords-and-Harmony.md) - Scales for diatonic variation
- [Loops](Loops.md) - Generating longer pieces imperatively
