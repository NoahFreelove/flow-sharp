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

## Song Functions

### getSections

Get the section names from a song:

```flow
use "@std"

Strings sections = (getSections mySong)
(print (str sections))  Note: ["intro", "verse", "chorus"]
```

### sectionSequences

Get the sequence names within a section:

```flow
use "@std"

section mySection {
    Sequence lead = | C4 D4 E4 F4 |
    Sequence bass = | C3 G3 C3 G3 |
}

Strings seqs = (sectionSequences mySection)
(print (str seqs))
```

### str

Convert a song or section to its string representation:

```flow
use "@std"

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
            Buffer buf = (renderSong song "piano")
            (exportWav buf "song.wav")
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
| Drums | `"drums"`, `"drum"` | Percussive, pitched drums |
| Sine | `"sine"` | Clean sine wave |

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
            (exportWav final "full_song.wav")
            (print "Song exported!")
        }
    }
}
```

## When to Use Sections

- **Organizing composition**: Break music into logical parts (intro, verse, chorus, bridge, outro)
- **Reuse**: Reference the same section multiple times with `*N` repeats
- **Clarity**: Named sections make the arrangement readable at a glance

## See Also

- [Note Streams](Note-Streams.md) - Writing sequences within sections
- [Musical Context](Musical-Context.md) - Context blocks around sections
- [Audio and Synthesis](Audio-and-Synthesis.md) - Rendering and synthesizers
- [Playback and Export](Playback-and-Export.md) - Playing and exporting songs
