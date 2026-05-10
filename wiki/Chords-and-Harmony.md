# Chords and Harmony

Flow has built-in support for chord literals, chord analysis functions, roman numeral resolution, arpeggios, and scale operations.

## Chord Literals

Chords are created using standard chord symbol notation:

```flow
Chord c1 = Cmaj      Note: C major
Chord c2 = Dm        Note: D minor
Chord c3 = Gdom7     Note: G dominant 7th
Chord c4 = Cmaj7     Note: C major 7th
Chord c5 = Am7       Note: A minor 7th
Chord c6 = Bdim      Note: B diminished
Chord c7 = Caug      Note: C augmented
Chord c8 = Dsus2     Note: D sus2
Chord c9 = Asus4     Note: A sus4
```

### Chord Qualities

| Suffix | Quality | Intervals |
|--------|---------|-----------|
| `maj` | Major | 1-3-5 |
| `m` | Minor | 1-b3-5 |
| `dim` | Diminished | 1-b3-b5 |
| `aug` | Augmented | 1-3-#5 |
| `dom7` | Dominant 7th | 1-3-5-b7 |
| `maj7` | Major 7th | 1-3-5-7 |
| `m7` | Minor 7th | 1-b3-5-b7 |
| `sus2` | Suspended 2nd | 1-2-5 |
| `sus4` | Suspended 4th | 1-4-5 |
| `add9` | Add 9th | 1-3-5-9 |
| `9` | Dominant 9th | 1-3-5-b7-9 |
| `6` | Major 6th | 1-3-5-6 |
| `m6` | Minor 6th | 1-b3-5-6 |

### Sharp and Flat Notation

Use `s` for sharp and `f` for flat in chord roots:

```flow
Chord cs = Csmaj    Note: C# major
Chord bf = Bfm      Note: Bb minor
Chord fs = Fsmaj7   Note: F# major 7th
```

> **Important**: `G7` is parsed as the note G at octave 7, not a G dominant 7th chord. Use `Gdom7` for the chord.

## Chord Functions

```flow
use "@std"

Chord c = Cmaj7

Note: Get the root note
String root = (chordRoot c)
(print (concat "Root: " root))  Note: C

Note: Get the quality
String quality = (chordQuality c)
(print (concat "Quality: " quality))  Note: maj7

Note: Get all notes
Strings notes = (chordNotes c)
(print (concat "Notes: " (str (len notes))))  Note: 4
```

## Chords in Note Streams

Chord symbols can be used directly in note streams:

```flow
timesig 4/4 {
    Sequence progression = | Cmaj7 Am7 Dm Gdom7 |
    (print (str progression))
}
```

Each chord expands to its component notes played simultaneously, with auto-fit duration.

## Roman Numerals

Within a `key` context, roman numerals represent scale-degree chords:

```flow
key Cmajor {
    timesig 4/4 {
        Note: In C major: I=C, IV=F, V=G
        Sequence progression = | I IV V I |

        Note: Lowercase = minor chords: ii=Dm, vi=Am
        Sequence minor = | ii V7 I |
    }
}
```

### Roman Numeral Reference

| Numeral | Degree | Default Quality |
|---------|--------|-----------------|
| `I` / `i` | 1st (tonic) | Major / minor |
| `II` / `ii` | 2nd (supertonic) | Major / minor |
| `III` / `iii` | 3rd (mediant) | Major / minor |
| `IV` / `iv` | 4th (subdominant) | Major / minor |
| `V` / `v` | 5th (dominant) | Major / minor |
| `VI` / `vi` | 6th (submediant) | Major / minor |
| `VII` / `vii` | 7th (leading) | Major / minor |

Uppercase = major, lowercase = minor. Extensions like `V7` add dominant 7th.

### Resolving Numerals Programmatically

```flow
use "@std"

Chord resolved = (resolveNumeral "V" "Cmajor")
(print (str resolved))  Note: G major

Chord dominant7 = (resolveNumeral "V7" "Cmajor")
(print (str dominant7))  Note: G dominant 7th
```

## Arpeggios

Generate arpeggiated sequences from chords:

```flow
use "@std"

Sequence arpUp = (arpeggio Cmaj "up")
Sequence arpDown = (arpeggio Cmaj "down")
Sequence arpUpDown = (arpeggio Cmaj "updown")
```

## Scale Operations

Get the notes of any scale:

```flow
use "@std"

Strings cMajor = (scaleNotes "Cmajor")
(print (concat "C major: " (str (len cMajor))))  Note: 7 notes

Strings aMinor = (scaleNotes "Aminor")
(print (concat "A minor: " (str (len aMinor))))  Note: 7 notes
```

## Chord Bracket Notation

For custom voicings, use bracket notation in note streams:

```flow
timesig 4/4 {
    Note: Custom voicing - not limited to standard chord shapes
    Sequence custom = | [C4 E4 G4]q [D4 F4 A4]q [E4 G4 B4]q [C4 E4 G4]q |
}
```

## Section Query Functions

```flow
use "@std"

section intro { Sequence mel = | C4 E4 G4 C5 | }
section verse { Sequence mel = | E4 F4 G4 A4 | }

Song mySong = [intro verse]

Note: Get section names from a song
Strings sections = (getSections mySong)
(print (str sections))
```

## See Also

- [Note Streams](Note-Streams.md) - Using chords in note streams
- [Musical Context](Musical-Context.md) - Key context for roman numerals
- [Song Structure](Song-Structure.md) - Sections with chord progressions
- [Pattern Transforms](Pattern-Transforms.md) - Transposing chord sequences
