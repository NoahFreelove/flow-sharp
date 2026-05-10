# Chords and Harmony

Flow has built-in support for chord literals, chord analysis functions, roman numeral resolution, arpeggios, scale operations, and voice-led chord progressions.

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
| `m` / `min` | Minor | 1-b3-5 |
| `dim` | Diminished | 1-b3-b5 |
| `aug` | Augmented | 1-3-#5 |
| `7` / `dom7` | Dominant 7th | 1-3-5-b7 |
| `maj7` | Major 7th | 1-3-5-7 |
| `m7` / `min7` | Minor 7th | 1-b3-5-b7 |
| `dim7` | Diminished 7th | 1-b3-b5-bb7 |
| `m7f5` | Half-diminished (m7♭5) | 1-b3-b5-b7 |
| `sus2` | Suspended 2nd | 1-2-5 |
| `sus4` | Suspended 4th | 1-4-5 |
| `add9` | Add 9th | 1-3-5-9 |
| `9` | Dominant 9th | 1-3-5-b7-9 |
| `6` | Major 6th | 1-3-5-6 |
| `m6` | Minor 6th | 1-b3-5-6 |

### Sharp and Flat Notation in Chord Symbols

Chord roots use `s` for sharp and `f` for flat:

```flow
Chord cs = Csmaj     Note: C# major
Chord bf = Bfm       Note: Bb minor
Chord fs = Fsmaj7    Note: F# major 7th
```

> This is **different** from note literal accidentals, which use `+` and `-` (e.g., `C4+` = C# at octave 4). See [Note Streams](Note-Streams.md).

> `G7` is parsed as the note G at octave 7, not a G dominant 7th chord. Use `Gdom7` (or `G7` in a context where a chord is explicit, like inside a note stream) for the chord.

## Chord Functions

```flow
use "@std"

Chord c = Cmaj7

String root = (chordRoot c)                 Note: "C"
String quality = (chordQuality c)           Note: "maj7"
String[] notes = (chordNotes c)             Note: ["C", "E", "G", "B"]
(print $"root: {root}, quality: {quality}, count: {notes -> length}")
```

## Chords in Note Streams

Chord symbols can be used directly in note streams. Each chord expands to its component notes played simultaneously, with auto-fit duration:

```flow
timesig 4/4 {
    Sequence prog = | Cmaj7 Am7 Dm7 G7 |
    (print (str prog))
}
```

## Roman Numerals

Within a `key` context, roman numerals represent scale-degree chords:

```flow
key Cmajor {
    timesig 4/4 {
        Note: in C major: I=C, IV=F, V=G
        Sequence mjr = | I IV V I |

        Note: lowercase = minor: ii=Dm, vi=Am
        Sequence mix = | ii V7 I |
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

Uppercase = major, lowercase = minor. Extensions like `V7` add a dominant 7th.

### Resolving Numerals Programmatically

```flow
use "@std"

Chord v = (resolveNumeral "V" "Cmajor")
(print (str v))      Note: G major

Chord v7 = (resolveNumeral "V7" "Cmajor")
(print (str v7))     Note: G dominant 7th
```

## Chord Progressions with Voice Leading

For smoother voice leading between chords, use the dedicated `progression` expression:

```flow
use "@std"

key Cmajor {
    Sequence chords = progression | I IV V I |
    Sequence thick  = progression voices 4 | I:2 IV vi V:2 |
}
```

See [Chord Progressions](Chord-Progressions.md) for the full reference, including bar-count suffixes, voice counts, and voice-leading behavior.

## Arpeggios

Generate arpeggiated sequences from chords:

```flow
use "@std"

Sequence up     = (arpeggio Cmaj "up")
Sequence down   = (arpeggio Cmaj "down")
Sequence updown = (arpeggio Cmaj "updown")
```

## Scale Operations

Get the note names of any scale:

```flow
use "@std"

String[] cMajor = (scaleNotes "Cmajor")      Note: 7 notes
String[] aMinor = (scaleNotes "Aminor")      Note: 7 notes
(print (str cMajor))
```

Scales are useful for constraining diatonic mutation with `vary`:

```flow
Sequence varied = (vary mel 0.3 "pitch" "Cmajor")
```

See [Generative Music](Generative.md).

## Chord Bracket Notation (Custom Voicings)

For custom voicings not limited to standard shapes, use bracket notation in a note stream:

```flow
timesig 4/4 {
    Sequence custom = | [C4 E4 G4]q [D4 F4 A4]q [E4 G4 B4]q [C4 E4 G4]q |
}
```

## Section Query Functions

```flow
use "@std"

section intro { Sequence mel = | C4 E4 G4 C5 | }
section verse { Sequence mel = | E4 F4 G4 A4 | }

Song mySong = [intro verse]

String[] sections = (getSections mySong)
(print (str sections))               Note: ["intro", "verse"]

String[] seqs = (sectionSequences intro)
(print (str seqs))                   Note: ["mel"]
```

## See Also

- [Note Streams](Note-Streams.md) - Using chords in note streams
- [Chord Progressions](Chord-Progressions.md) - Voice-led `progression | ... |`
- [Musical Context](Musical-Context.md) - Key context for roman numerals
- [Song Structure](Song-Structure.md) - Sections with chord progressions
- [Pattern Transforms](Pattern-Transforms.md) - Transposing chord sequences
- [Generative Music](Generative.md) - Diatonic variation
