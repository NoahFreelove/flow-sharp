# Song Structure

Flow provides `section` declarations and `Song` expressions to organize music into named parts with an arrangement order.

## Sections

A section is a named block of musical code containing one or more sequences:

```flow
section intro {
    Sequence melody = | C4 E4 G4 C5 |
}
```

### Multiple Sequences

Sections can contain multiple sequences (e.g., a lead melody and a bass line):

```flow
section verse {
    Sequence lead = | E4 E4 F4 G4 |
    Sequence bass = | C3 C3 G3 G3 |
}
```

### Empty Sections

Sections can be empty (useful for rests or structural placeholders):

```flow
section bridge {
}
```

### Sections with Musical Context

Sections inherit the musical context they're declared in:

```flow
key Cmajor {
    section outro {
        Sequence melody = | I IV V I |
    }
}
```

## Song Expressions

A `Song` arranges sections in order using bracket syntax:

```flow
section intro { Sequence mel = | C4 E4 G4 C5 | }
section verse { Sequence mel = | E4 E4 F4 G4 | }
section chorus { Sequence mel = | G4 A4 G4 E4 | }

Song mySong = [intro verse chorus]
(print (str mySong))
```

### Repeat Counts

Use `*N` to repeat a section:

```flow
Song mySong = [intro verse*2 chorus verse chorus*2]
```

This plays: intro, verse, verse, chorus, verse, chorus, chorus.

## Parameterized Sections

Sections can declare typed parameters with optional default values, so the same musical idea can be reused with different roots, transpositions, or repeat counts. Parameters use the full pattern syntax (typed bindings, tuple destructure, music-aware extractors):

```flow
section verse(Note root, Int repeats = 2) {
    Sequence mel = | root D4 E4 F4 |
}

section chorus(Chord on = Cmaj7) {
    Sequence bigChord = | on |
}
```

Call them from a `Song` expression with either positional or named arguments — defaults are honored for any trailing argument that isn't supplied:

```flow
Song s = [verse(C4)                Note: uses default repeats=2
          verse(D4, 4)             Note: positional override
          verse(root=E4, repeats=1) Note: named-arg form
          chorus]                  Note: uses default on=Cmaj7
```

Combine with the `*N` repetition operator — it works on both zero-arg and parameterized calls:

```flow
Song looped = [verse(C4)*3 chorus*2]
```

### Section Overloading

Multiple `section verse(...)` declarations with different signatures coexist — the overload resolver picks the highest-specificity match at call time:

```flow
section verse(Note root) {
    Sequence mel = | root D4 E4 F4 |
}

section verse(Note root, Int reps) {
    Sequence mel = | root D4 E4 F4 |
    Sequence echo = | root |
}

Song s = [verse(C4) verse(C4, 3)]      Note: dispatches to each overload
```

The resolver dispatches by argument count and types at call time, so `verse(C4)` selects the single-`Note` overload and `verse(C4, 3)` selects the two-argument one.

Per-call args bind in a fresh stack frame that **inherits the call-site's musical context**, so a `verse(C4)` call inside a `key Aminor { ... }` block sees Aminor as its active key.

## Song Functions

### getSections

Get the section names from a song:

```flow
use "@std"

section intro { Sequence mel = | C4 E4 G4 C5 | }
section verse { Sequence mel = | E4 E4 F4 G4 | }
section chorus { Sequence mel = | G4 A4 G4 E4 | }

Song mySong = [intro verse chorus]
Strings sections = (getSections mySong)
(print (str sections))  Note: ["intro", "verse", "chorus"]
```

### getSection

`getSection(Song, String)` pulls one named `Section` back out of a `Song` so you can inspect it:

```flow
use "@std"

section mySection {
    Sequence lead = | C4 D4 E4 F4 |
    Sequence bass = | C3 G3 C3 G3 |
}

Song mySong = [mySection]
Section s = (getSection mySong "mySection")
(print (str s))         Note: Section[mySection, 2 sequences]
```

### sectionSequences

`sectionSequences(Song, String)` returns the **sequence names** within a named section, as a `String[]`. (A `sectionSequences(Section)` overload taking a runtime `Section` value also exists.)

```flow
use "@std"

section mySection {
    Sequence lead = | C4 D4 E4 F4 |
    Sequence bass = | C3 G3 C3 G3 |
}

Song mySong = [mySection]
String[] names = (sectionSequences mySong "mySection")
(print (str names))     Note: ["lead", "bass"]
```

An unknown section name returns an empty array with a one-shot `[sectionSequences]` advisory rather than erroring.

### str

Convert a song or section to its string representation:

```flow
use "@std"

section intro { Sequence mel = | C4 E4 G4 C5 | }
section verse { Sequence mel = | E4 E4 F4 G4 | }
Song mySong = [intro verse]
(print (str mySong))
```

## Rendering Songs

Use `renderSong` to convert a `Song` to an audio `Buffer`:

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section intro {
                Sequence melody = | C4 E4 G4 C5 |
            }
            section chorus {
                Sequence melody = | I IV V I |
            }

            Song song = [intro chorus]
            Buffer audio = (renderSong song "piano")
            (writeWav "song.wav" audio)
        }
    }
}
```

### Available Instruments

The second argument to `renderSong` selects the synthesizer:

| Name | Aliases | Character |
|------|---------|-----------|
| Piano | `"piano"` | Percussive attack with warm decay |
| Brass | `"brass"`, `"horn"` | Bold, sustained tone |
| Saxophone | `"sax"`, `"saxophone"` | Reed-like character |
| Flute | `"flute"` | Pure, breathy tone |
| Organ | `"organ"` | Sustained, multi-partial timbre |
| Strings | `"strings"` | Smooth bowed-instrument-like timbre |
| Bell | `"bell"` | Inharmonic bell / chime character |
| Drums | `"drums"`, `"drum"` | Percussive, pitched drums |
| Sine | `"sine"` | Clean sine wave |

Custom wavetables registered via `oscillator(name, ...)` also work here. You can also pass a Flow `Function` as a custom instrument — see [Audio and Synthesis](Audio-and-Synthesis.md).

## Complete Example

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section intro {
                Sequence melody = | C4 E4 G4 C5 |
            }

            section verse {
                Sequence lead = | E4 E4 F4 G4 |
                Sequence bass = | C3 C3 G3 G3 |
            }

            section chorus {
                Sequence lead = | G4 A4 G4 E4 |
            }

            section bridge {
                Sequence mel = | A4 G4 F4 E4 |
            }

            section outro {
                Sequence melody = | I IV V I |
            }

            Song fullSong = [intro verse*2 chorus bridge outro]
            Buffer rendered = (renderSong fullSong "piano")
            Buffer final = rendered -> fadeIn 0.3 -> fadeOut 0.5
            (writeWav "full_song.wav" final)
            (print "Song exported!")
        }
    }
}
```

## When to Use Sections

- **Organizing composition**: Break music into logical parts (intro, verse, chorus, bridge, outro)
- **Reuse**: Reference the same section multiple times with `*N` repeats
- **Clarity**: Named sections make the arrangement readable at a glance

## Exporting to MIDI

A `Song` can also be exported to a Standard MIDI File, preserving tempo, time signature, and key:

```flow
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section intro { Sequence mel = | C4 E4 G4 C5 | }
            section verse { Sequence lead = | E4 E4 F4 G4 | }
            Song fullSong = [intro verse*2]
            (writeMidi "my_song.mid" fullSong)
        }
    }
}
```

See [Playback and Export](Playback-and-Export.md).

## See Also

- [Note Streams](Note-Streams.md) - Writing sequences within sections
- [Musical Context](Musical-Context.md) - Context blocks around sections
- [Audio and Synthesis](Audio-and-Synthesis.md) - Rendering and synthesizers
- [Voices and Tracks](Voices-and-Tracks.md) - Lower-level multi-track rendering
- [Chord Progressions](Chord-Progressions.md) - Voice-led chord sequences
- [Playback and Export](Playback-and-Export.md) - Playing and exporting songs
