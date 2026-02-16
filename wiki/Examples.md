# Examples

Complete, runnable Flow programs demonstrating various features.

## Hello World

```flow
use "@std"

(print "Hello, Flow!")
```

## Variables and Arithmetic

```flow
use "@std"

Int x = 5
Int y = 10
Int sum = x + y
(print (concat "Sum: " (str sum)))

Int a = 1; Int b = 2; Int c = 3
(print (str a)); (print (str b)); (print (str c))

Note: Reassignment
Int counter = 0
counter = counter + 1
counter = counter + 1
(print (concat "Counter: " (str counter)))
```

## Functions and Lambdas

```flow
use "@std"

Note: Procedure with implicit return
proc double (Int: x)
    x * 2
end proc

(print (str (double 7)))  Note: 14

Note: Lambda functions
Function tripler = fn Int n => (mul n 3)
(print (str (tripler 4)))  Note: 12

Note: Function type annotation
(Int, Int => Int) adder = fn Int a, Int b => (add a b)
(print (str (adder 3 4)))  Note: 7

Note: Chaining with flow operator
Int result = 5 -> double -> tripler
(print (str result))  Note: 30
```

## Collections and Functional Programming

```flow
use "@std"
use "@collections"

Int[] nums = (list 1 2 3 4 5)

Note: Map - double each number
Int[] doubled = (map nums (fn Int n => (mul n 2)))
(print (str doubled))

Note: Filter - keep numbers greater than 3
Int[] big = (filter nums (fn Int n => (gt n 3)))
(print (str big))

Note: Reduce - sum all numbers
Int total = (reduce nums 0 (fn Int acc, Int n => (add acc n)))
(print (concat "Sum: " (str total)))

Note: Each - print each number
(each nums (fn Int n => (print (str n))))

Note: Closures capture variables
Int factor = 10
Int[] scaled = (map (list 1 2 3) (fn Int n => (mul n factor)))
(print (str scaled))
```

## Simple Melody

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        section melody {
            Sequence mel = | C4 D4 E4 F4 | G4 A4 B4 C5 |
        }

        Song song = [melody]
        Buffer buf = (renderSong song "piano")
        (exportWav buf "simple_melody.wav")
        (print "Exported simple_melody.wav!")
    }
}
```

## Chord Progression

```flow
use "@std"
use "@audio"

tempo 100 {
    timesig 4/4 {
        key Cmajor {
            section progression {
                Note: Using roman numerals
                Sequence chords = | I IV V I |
            }

            Song song = [progression]
            Buffer buf = (renderSong song "piano")
            (exportWav buf "chords.wav")
            (print "Exported chords.wav!")
        }
    }
}
```

## Full Song with Sections

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

            Song fullSong = [intro verse*2 chorus bridge verse chorus*2 outro]
            Buffer rendered = (renderSong fullSong "piano")
            Buffer final = rendered -> fadeIn 0.3 -> fadeOut 0.5
            (exportWav final "full_song.wav")

            Int frames = (getFrames final)
            Int duration = (div frames 44100)
            (print (concat "Duration: ~" (concat (str duration) "s")))
        }
    }
}
```

## Expressive Piano Piece

```flow
use "@std"
use "@audio"

tempo 72 {
    timesig 4/4 {
        key Cmajor {
            Note: Opening - gentle start with crescendo
            section intro {
                Sequence phrase1 = | pp C4q E4q G4q C5q | -> crescendo 0.2 0.7
                Sequence phrase2 = | mf E4q G4q B4q E5q | -> humanize 0.1
            }

            Note: Development - louder with articulation
            section development {
                Sequence theme = | f C4q> D4q E4q stacc F4q |
                Sequence variation = theme -> transpose +4st -> decrescendo 0.8 0.4
            }

            Note: Climax - fortissimo with swell
            section climax {
                Sequence peak = | C4q E4q G4q C5q E5q G5q C6q C6q | -> swell 0.5 1.0
            }

            Note: Resolution - quiet ending
            section resolution {
                Sequence ending = | mp G4q E4q C4h | -> ritardando 0.5
                Sequence finalChord = | pp C4w |
            }

            Song piece = [intro development climax resolution]
            Buffer rendered = (renderSong piece "piano")
            Buffer final = rendered -> fadeIn 0.3 -> fadeOut 0.5
            (exportWav final "expressive_piano.wav")

            Int frames = (getFrames final)
            Int duration = (div frames 44100)
            (print (concat "Duration: ~" (concat (str duration) "s")))
        }
    }
}
```

## Pattern Transforms

```flow
use "@std"

timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |

    Note: Transpose up 2 semitones
    Sequence t = mel -> transpose +2st
    (print (concat "Transposed: " (str t)))

    Note: Invert (mirror intervals)
    Sequence inv = (invert mel)
    (print (concat "Inverted: " (str inv)))

    Note: Retrograde (reverse)
    Sequence ret = (retrograde mel)
    (print (concat "Retrograde: " (str ret)))

    Note: Augment (double durations)
    Sequence aug = (augment mel)
    (print (concat "Augmented: " (str aug)))

    Note: Diminish (halve durations)
    Sequence dim = (diminish mel)
    (print (concat "Diminished: " (str dim)))

    Note: Octave shift
    Sequence high = mel -> up 1
    (print (concat "Up octave: " (str high)))

    Note: Repeat with transposition
    Sequence rising = mel -> repeat 3 +4st
    (print (concat "Rising: " (str rising)))

    Note: Chained transforms
    Sequence chain = (retrograde (transpose mel +5st))
    (print (concat "Chained: " (str chain)))
}
```

## Generative Music

```flow
use "@std"

timesig 4/4 {
    Note: Random note selection
    Sequence random = | (? C4 E4 G4) (? C4 E4 G4) (? C4 E4 G4) (? C4 E4 G4) |
    (print (concat "Random: " (str random)))

    Note: Weighted random (C4 most likely)
    Sequence weighted = | (? C4:50 E4:30 G4:20) (? C4:50 E4:30 G4:20) _ _ |
    (print (concat "Weighted: " (str weighted)))

    Note: Seeded random (deterministic)
    (??set 42)
    Sequence seeded = | (?? C4 E4 G4) (?? D4 F4 A4) (?? E4 G4 B4) (?? C4 E4 G4) |
    (print (concat "Seeded: " (str seeded)))
}

Note: Euclidean rhythms
Sequence euclid1 = (euclidean 3 8 C4)
(print (concat "Euclidean 3/8: " (str euclid1)))

Sequence euclid2 = (euclidean 5 8 E4)
(print (concat "Euclidean 5/8: " (str euclid2)))
```

## Effect Processing Chain

```flow
use "@std"
use "@audio"

Note: Create a test tone
Buffer tone = (createSineTone 0.5 440.0 0.5)
(print (concat "Original frames: " (str (getFrames tone))))

Note: Apply effects chain
Double negThree = (sub 0.0 3.0)
Buffer processed = tone -> lowpass 1000.0 -> reverb 0.3 -> gain negThree
(print (concat "Processed frames: " (str (getFrames processed))))

Note: Individual effects
Buffer lp = (lowpass tone 800.0)
(print (concat "Lowpass: " (str (getFrames lp))))

Buffer hp = (highpass tone 200.0)
(print (concat "Highpass: " (str (getFrames hp))))

Buffer bp = (bandpass tone 200.0 2000.0)
(print (concat "Bandpass: " (str (getFrames bp))))

Buffer rev = (reverb tone 0.7 0.3 0.5)
(print (concat "Reverb: " (str (getFrames rev))))

Double negThreshold = (sub 0.0 12.0)
Buffer comp = (compress tone negThreshold 4.0)
(print (concat "Compressed: " (str (getFrames comp))))

Buffer del = (delay tone 250.0 0.4 0.5)
(print (concat "Delayed: " (str (getFrames del))))

Note: Export
(exportWav processed "processed.wav")
(print "Exported processed.wav!")
```

## Audio Synthesis from Scratch

```flow
use "@std"
use "@audio"

Note: Create an oscillator
OscillatorState osc = (createOscillatorState 440.0 44100)

Note: Generate a buffer of sine wave
Buffer buf = (createBuffer 44100 2 44100)
(generateSine buf osc 0.5)

Note: Apply an ADSR envelope
Envelope env = (createADSR 0.01 0.1 0.7 0.3 44100)
(applyEnvelope buf env)

Note: Add some reverb
Buffer wet = (reverb buf 0.4)

Note: Export
(exportWav wet "synth_from_scratch.wav")
(print "Exported synth_from_scratch.wav!")

Int frames = (getFrames wet)
(print (concat "Frames: " (str frames)))
```

## Multiple Synthesizers

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section melody {
                Sequence mel = | C4 E4 G4 C5 |
            }
            Song song = [melody]

            Note: Same melody with different instruments
            Buffer piano = (renderSong song "piano")
            Buffer brass = (renderSong song "brass")
            Buffer sax = (renderSong song "sax")
            Buffer drums = (renderSong song "drums")

            (exportWav piano "piano.wav")
            (exportWav brass "brass.wav")
            (exportWav sax "sax.wav")
            (exportWav drums "drums.wav")
            (print "Exported all instruments!")
        }
    }
}
```

## Waltz in 3/4

```flow
use "@std"
use "@audio"

tempo 90 {
    timesig 3/4 {
        key Aminor {
            section waltz {
                Sequence mel = | A4 C5 E5 |
            }
            section middle {
                Sequence mel = | D5 F5 A5 |
            }

            Song piece = [waltz*4 middle*2 waltz*2]
            Buffer buf = (renderSong piece "piano")
            Buffer final = buf -> reverb 0.4 -> fadeOut 0.5
            (exportWav final "waltz.wav")
            (print "Exported waltz.wav!")
        }
    }
}
```

## Ornaments and Expression

```flow
use "@std"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Note: Ghost notes - barely audible
            Sequence ghosty = | C4 (ghost D4) E4 F4 |
            (print (concat "Ghost: " (str ghosty)))

            Note: Grace notes - quick ornament
            Sequence graceful = | (grace B3) C4 D4 E4 F4 |
            (print (concat "Grace: " (str graceful)))

            Note: Trill - rapid alternation
            Sequence mel = | C4h E4h |
            Sequence trilled = mel -> trill +2st
            (print (concat "Trill: " (str trilled)))

            Note: Tremolo - rapid repetition
            Sequence trem = mel -> tremolo 4
            (print (concat "Tremolo: " (str trem)))

            Note: Humanize for natural feel
            Sequence natural = | C4 D4 E4 F4 | -> humanize 0.15
            (print (concat "Humanized: " (str natural)))
        }
    }
}
```

## See Also

- [Quick Start](Quick-Start.md) - Getting started
- [Tips and Tricks](Tips-and-Tricks.md) - Common idioms and pitfalls
- [Standard Library](Standard-Library.md) - Complete function reference
