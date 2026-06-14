# Chord Progressions

Flow provides a dedicated `progression` expression that compiles a sequence of roman numerals into a voice-led chord `Sequence`. It's similar to writing chord symbols in a note stream, but it applies an automatic voice-leading algorithm that chooses nearby voicings for smoother motion.

## Basic Syntax

Progressions are expressions, not statements. Use them anywhere a `Sequence` is expected:

```flow
use "@std"

key Cmajor {
    Sequence chords = progression | I IV V |
    (print (str chords))
}
```

A `key` context is required — the roman numerals resolve relative to the active key.

### Delimited by Pipes

Like note streams, a progression body is delimited by `|`:

```flow
progression | I IV V I |
```

Uppercase numerals = major chords, lowercase = minor. Extensions (e.g., `V7`, `iim7`) work just like roman numerals inside note streams.

## Bar Count Suffix

Append `:N` to a numeral to hold it for `N` bars:

```flow
use "@std"

key Cmajor {
    Sequence chords = progression | I:2 IV vi V:2 |
    Note: I for 2 bars, IV for 1, vi for 1, V for 2
}
```

Without `:N`, each numeral gets one bar.

## Voice Count

By default, progressions use as many voices as the largest chord in the progression (3 for triads, 4 for 7ths, etc.). To force a denser voicing, add `voices N`:

```flow
use "@std"

key Cmajor {
    Sequence thick = progression voices 4 | I IV V |
}
```

Voice counts above the triad add chord tones (7th, 9th) where appropriate. For broader polyphony control across an entire passage (independent of individual chord voicing), see the [`voicePool` context block](Musical-Context.md#voice-pool).

## Minor Keys

In a minor key, lowercase numerals become the natural choices:

```flow
use "@std"

key Aminor {
    Sequence minor = progression | i iv v i |
}
```

## Voice Leading

The progression compiler applies **nearest-neighbor voice leading**:

- Bass line follows the chord root, constrained to a bass range (~MIDI 36–55)
- Upper voices are rearranged between chords to minimize motion (within ~MIDI 48–84)
- Each chord is emitted as a whole-note block chord in the output sequence

This produces smoother results than raw roman numeral chords in a note stream, which would always use root-position voicings.

## Combining with Transforms

A `progression` returns a `Sequence`, so all standard transforms apply:

```flow
use "@std"

key Cmajor {
    Sequence base = progression | I IV V I |
    Sequence up2  = base -> transpose(2)
    Sequence ret  = (retrograde base)
    Sequence loop = base -> repeat 4
}
```

## Rendering a Progression

```flow
use "@std"
use "@audio"

tempo 100 {
    timesig 4/4 {
        key Cmajor {
            section hook {
                Sequence chords = progression | I vi IV V |
            }

            Song song = [hook*4]
            Buffer rendered = (renderSong song "piano")
            Buffer final = rendered -> reverb 0.3 -> fadeOut 0.5
            (writeWav "ivvi_iv_v.wav" final)
        }
    }
}
```

## progression vs. Note-Stream Chords

Both approaches can produce chord sequences — pick based on what you want:

| You want... | Use |
|-------------|-----|
| Auto voice-leading between chords | `progression \| I IV V I \|` |
| Fixed chord shapes in root position | Note-stream roman numerals: `\| I IV V I \|` |
| Custom voicings (e.g. drop-2, specific inversions) | Bracket chord notation: `\| [C4 E4 G4] [F4 A4 C5] \|` |
| Named chord symbols | Chord literals in a note stream: `\| Cmaj7 Am7 Dm7 G7 \|` |

## Common Progressions

```flow
use "@std"

key Cmajor {
    Sequence ionian    = progression | I IV V I |
    Sequence deceptive = progression | I IV V vi |
    Sequence twoFiveOne = progression | ii V I |
    Sequence circle    = progression | I vi ii V |
    Sequence blues     = progression | I:4 IV:2 I:2 V:1 IV:1 I:2 |
}

key Aminor {
    Sequence andalusian = progression | i VII VI V |
    Sequence naturalMin = progression | i iv v i |
}
```

## See Also

- [Chords and Harmony](Chords-and-Harmony.md) - Chord literals, roman numerals, scales
- [Musical Context](Musical-Context.md) - Setting `key` for progressions
- [Pattern Transforms](Pattern-Transforms.md) - Transforming progression sequences
- [Song Structure](Song-Structure.md) - Putting progressions into sections
