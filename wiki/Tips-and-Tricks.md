# Tips and Tricks

Practical advice, idioms, and gotchas for writing Flow code.

## Always Import @std

Almost every program needs:

```flow
use "@std"
```

Without it, you can't use `print`, `str`, `concat`, `add`, `sub`, `list`, `map`, `filter`, or any other standard library function.

## Optional Parentheses

Function calls with literal arguments can omit outer parentheses:

```flow
proc square (Int: n)
    (mul n n)
end proc

Int s = square 4    Note: works with literal
Int s = (square x)  Note: parens needed for variable args
```

This is syntactic sugar — the parser recognizes bare identifier followed by literals.

## Line Continuation

Use `\` at the end of a line to continue on the next line:

```flow
String long = (concat "Hello" \
    " World")
```

## Semicolons for Multiple Statements

Put multiple statements on one line with `;`:

```flow
Int a = 1; Int b = 2; Int c = (add a b)
(print (str a)); (print (str b)); (print (str c))
```

## Comments

Flow recognizes several comment styles — pick whichever fits the surrounding context:

```flow
Note: Chapter divider or longer prose — must start at column 0 of the line's content.
// C-style line comment, works anywhere on a line.
Int x = 5  // inline comment
Int y = 7  Note: inline `Note:` comment is fine after a statement

TODO: still need to add a sax voice here
FIXME: this transpose is one semitone off
; classic Lisp-style — only when in column 0; mid-line `;` is a statement separator
```

`Note:`, `TODO:`, and `FIXME:` all run to end of line and are equivalent to `//` for the lexer. `;` is BOTH a column-0 comment marker AND a mid-line statement separator — context decides.

## Prefix-Only Arithmetic

Flow has no infix arithmetic. `1 + 2` is a parse error; the parser will suggest the prefix form:

```flow
Int sum   = (add 10 25)
Double prod = (mul 3.0 4.5)
Int diff  = (sub 100 37)
Int truncated = (idiv 10 3)   Note: integer division
Double neg = (neg 3.14)        Note: unary negation
String s = (concat "Hi, " "there")
```

For negative numeric LITERALS at the start of an expression (after `(`, `=`, `,`, `|`, etc.) the lexer accepts `-3` or `-2.5` as a single token. But `Int x = a - b` is still a parse error — use `(sub a b)`.

## Printing Values

Convert to string or use string interpolation:

```flow
use "@std"

Int x = 42
(print (str x))              Note: prints "42"
(print (str 3.14))
(print (str true))

Note: Interpolation is usually cleanest
(print $"value: {x}")
(print $"total: {(add x 100)}")

Note: Traditional concat
(print (concat "Value: " (str x)))
```

## Debugging

Use `print` liberally for debugging:

```flow
use "@std"

Sequence mel = | C4 D4 E4 F4 |
(print (str mel))  Note: see the sequence representation

Buffer buf = (renderSong song "piano")
(print (concat "Frames: " (str (getFrames buf))))
```

For waveforms or sequences, `(visualize buf)` and `(visualize seq)` render ASCII piano rolls / waveforms straight to stdout.

## `flow check` Before Committing

For a fast syntax-only sanity check, run:

```bash
flow check path/to/script.flow
```

It parses (and currently also executes) the script and exits with a non-zero code on errors — handy in pre-commit hooks. A pure parse-only mode is on the v1.5+ backlog.

## Flow Operator Idioms

### Effect Chain (most common)

```flow
Buffer final = raw -> lowpass 2000.0 -> reverb 0.3 -> fadeOut 0.5
```

### Transform Chain

```flow
Sequence processed = mel -> transpose +2st -> repeat 2 -> humanize 0.1
```

### String Building

```flow
"Hello" -> concat " World" -> print
```

### Naming Intermediate Results (`as`)

When a later step in a chain needs to reuse the value from an earlier step, bind it with `as`:

```flow
Int x = 5 -> (mul 2) as doubled
            -> (add doubled)     Note: 10 + 10 = 20

Buffer mix = dry -> (gain -3dB) as quiet
                -> (mix quiet)   Note: quiet self-mixed back in
```

The bound name is visible from its `as` clause forward in the same chain AND to the next statement in the same block.

### Tuple-Unpack with `~>`

When you have a tuple and want to pass each slot as a separate positional argument, use `~>` instead of `->`:

```flow
proc add3(Int: a, Int: b, Int: c)
    (add (add a b) c)
end proc

Int sum = <<1, 2, 3>> ~> add3      Note: lowers to (add3 1 2 3)
```

On a non-tuple left-hand side, `~>` falls through to plain `->` semantics — so it's safe inside generic code where you don't know whether the value is a tuple yet. The runtime equivalent is `(unpack tup func)`.

## Named Arguments

Most builtins now accept named arguments. Reach for them when:

- the call has 4+ positional slots, OR
- you want to skip middle defaults, OR
- the call is part of a config file and clarity beats brevity.

```flow
Note: positional form — concise once you know the signature
Sequence solo = (jam chords #jazz 4 "Cmajor" 1234 2)

Note: named form — self-documenting; safe to reorder
Sequence solo = (jam over=chords style=#blues seed=42 length=8 key="Cmajor")

Note: skip middle defaults
Buffer g = (granular tone 50ms 20Hz 0.3 windowing=#gaussian)
```

Positional + named can mix in one call as long as positional come first.

## Match Expressions

Use `match` for clean discriminated dispatch — way more readable than nested `if`:

```flow
String tonality = (match (chord "G")
                    | V => "dominant"
                    | I => "tonic"
                    | _ => "other")

Note: bindings + guards
String sign = (match n
                | x when (gt x 0) => "pos"
                | 0 => "zero"
                | _ => "neg")
```

Patterns: literal (`1`, `"hello"`, `Cmaj7`, `V7`), wildcard (`_`), binding (bare identifier `n`), and guards (`pat when (predicate)`). Music-aware constructor patterns include chord literals and roman numerals.

Add `enable matchExhaustive;` at the top of the file to promote non-exhaustive matches from warning to error.

## Tuple Destructuring

When a value is naturally a fixed-size record, return / pass a tuple and destructure it:

```flow
Tuple<<Note, Int>> noteAndCount = <<C4, 8>>
<<Note root, Int reps>> = noteAndCount
Note: now `root` and `reps` are normal locals
```

Type annotations on each slot are optional but help readability.

## Musical Context Nesting Pattern

Always nest context blocks in a consistent order:

```flow
tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Note: Your code here
        }
    }
}
```

You only need the blocks you actually use:
- `timesig` is required for note streams
- `key` is required for roman numerals
- `tempo` is required for audio rendering

Other available context blocks: `swing 0.6 { }`, `voicePool 32 { }`, `sustainPedal { }`, `tuning t { }`, `pan { }`, `gain { }`, `reverbTime { }`, `dynamics { }`, `rit { }`, `accel { }`. They all nest the same way and inherit from outer scopes.

## `gain` vs `volume` — Pick by Semantic Intent

Both scale a Buffer's amplitude. The function NAME documents the unit:

```flow
Buffer attenuated = (gain buf -6dB)       Note: decibels
Buffer half       = (volume buf 0.5)      Note: linear multiplier
```

Footgun: `(gain buf 0.5)` is 0.5 dB attenuation (about 5.9% softer), NOT 50% volume. Use `volume` when you mean a linear factor, `gain` when you mean dB. `volume` rejects negative values — for dB attenuation, use `gain` with a negative number or a `-NdB` literal.

## Piano Sustain Pedal Simulation

When rendering piano, lengthen the release tail with the `release=` named arg:

```flow
Buffer warm = (renderSong song "piano" release=2.5s)
```

Default is 1.5 seconds; range is clamped to `[0.05s, 10.0s]`. Pair with `sustainPedal { }` (which extends individual notes) for the lushest result.

## Voice-Block Polyphony

When a single sequence needs simultaneous voices (e.g. a stride bass under a melody), use `{voice ...}` blocks inside the bar:

```flow
Sequence stride = | {voice C4w} {voice C5q D5q E5q F5q} |
```

Both voices share the bar's onset and mix additively. Same render path for audio AND MIDI — voice blocks export as overlapping NoteOn events at the parent's tick. Reach for separate `Sequence` variables when the voices have independent musical identity (e.g. lead vs bass line that the composer thinks of as different instruments); reach for voice blocks for tightly-coupled inner-voice writing.

## Charitable Interpretation

Flow's stdlib follows a "charitable" philosophy: degenerate inputs return reasonable defaults plus a one-shot stderr advisory, rather than throwing. You can prototype without paranoid input validation:

- `(stretch buf 0.0)` → returns input + advisory (NOT a divide-by-zero crash)
- `(every 0 cb seq)` → returns input + advisory
- `(jam unknownChords #fakestyle 0 "Cmajor" 0 9)` → falls back to a usable Sequence
- `(abc malformedInput)` → drops unrecognized tokens with `[abc]` advisory; never throws

This means stdlib functions almost never need to be wrapped in `try`-like guards.

## PRNG Determinism

All stochastic builtins route their random number generators through a single registry keyed by `(SourceLocation, generator-name)`. Two runs of the same script at the same git SHA produce byte-identical WAV output — even when calls like `(sometimes 0.5 cb seq)` or `(humanize seq 0.1)` are unseeded.

```flow
Note: Unseeded — Flow picks a stable seed from the source position.
Sequence varied = (sometimes 0.4 (fn Sequence s => (rev s)) seq)

Note: Seeded — exact bit-for-bit control across processes / platforms.
Sequence seeded = (markov corpus 2 16 42)
```

Use unseeded for ergonomics during composition; seed (`seed=N` or the trailing integer arg) when you need cross-process reproducibility.

Exception: `lorenz` and `logistic` (chaos maps) preserve same-platform two-run determinism only — chained FP arithmetic amplifies platform-specific quirks across Linux/macOS/Windows.

## Identity Fast-Paths in DSP

Several Phase 37 builtins return the input buffer byte-identical when called with no-op parameters. Safe to write generic code that conditionally stretches / pitch-shifts:

```flow
Buffer maybeStretched = (stretch buf factor)      Note: factor=1.0 → input verbatim
Buffer maybeShifted   = (pitchShift buf cents)    Note: cents=0 / 0c → input verbatim
Buffer maybeLoaded    = (loadWav "x.wav" 0)       Note: 0 semitones → byte-identical to (loadWav "x.wav")
```

No need to branch on "if shift != 0" in caller code.

## Pattern Syntax in Section Signatures

Parameterized sections support the full pattern surface, including tuple destructure and music-aware extractors:

```flow
Note: Plain typed binding
section verse(Note root) { Sequence inner = | root +4st +7st | }

Note: Compact destructure when an arg is naturally a record
section verse(<<Note root, Int reps>>) { ... }

Note: Music-aware extractor — fires only when called with a Cmaj7 literal
section verse(Cmaj7) { ... }

Note: Defaults
section verse(Note root = C4, Int reps = 2) { ... }

Note: Call sites (Song expression)
Song s = [
    verse(C4)
    verse(<<D4, 3>>)
    verse(Cmaj7)*3       Note: *N repetition operator
    chorus               Note: legacy zero-arg form stays valid
]
```

Multiple `section verse(...)` declarations with different signatures coexist as overloads — the resolver picks the highest-specificity match at the call site.

## Style Pack Overrides

`jam` ships three style packs (`#jazz`, `#blues`, `#classical`) as plain Flow files at `flow-lang/improv/styles/*.flow`. Override the shipped versions by dropping a same-named file at `~/.config/flow/styles/<name>.flow` — user packs win (last-write-wins).

```flow
Note: ~/.config/flow/styles/jazz.flow
Note: see flow-lang/improv/styles/README.md for the dict shape
```

This is how you teach `jam` your own idioms without recompiling Flow.

## Scala Microtuning

Drop a Scala `.scl` file anywhere on disk, then load + apply with the string-literal sugar:

```flow
tempo 100 {
    timesig 4/4 {
        tuning "tunings/partch_43.scl" {
            section a { Sequence mel = | C4q E4q G4q B4q | }
        }
    }
}
```

Three composer surfaces compose identically — `tuning t { }` (identifier), `tuning (loadScala "x.scl") { }` (inline call), `tuning "x.scl" { }` (sugar). The last-wins rule applies between pragmas (`enable justIntonation;` etc.) and inline `tuning { }` blocks — the innermost active scope wins.

For non-octave scales (Carlos Alpha, Bohlen-Pierce), the period auto-adopts from the .scl — no need for an explicit `.kbm`.

## Live Reload with `flow watch`

For iterative composition, run:

```bash
flow watch path/to/script.flow
```

Changes are quantized to the next bar boundary and crossfaded over 64 samples. If a re-render fails, the previous version keeps playing — no silence-on-error gap.

## MIDI Round-Trip with `flow midi2flow`

To pull an existing MIDI file into Flow source:

```bash
flow midi2flow song.mid -o song.flow
```

Pair with `(writeMidi "out.mid" song)` in the regenerated `.flow` file for a clean round-trip. Useful for sketching with a DAW + finishing in Flow.

## Common Pitfalls

### 1. Forgetting `use "@std"`

```flow
Note: ERROR: print is not defined
(print "hello")

Note: FIX:
use "@std"
(print "hello")
```

### 2. Accidentals: `+`/`-` vs `s`/`f`

Notes and chord roots use different accidental syntaxes:

```flow
Note: note literal — use + and -
Note cSharp = C4+
Note bFlat  = B3-

Note: chord symbol — use s and f
Chord cSharpMaj = Csmaj
Chord bFlatMin  = Bfm
```

This is a common source of confusion.

### 3. G7 vs Gdom7

`G7` is parsed as the note G at octave 7, not a G7 chord:

```flow
Note: This is a NOTE, not a chord:
Note g7note = G7

Note: This is the CHORD:
Chord g7chord = Gdom7
```

### 4. Missing Musical Context for Note Streams

```flow
Note: May not work correctly without timesig:
Sequence mel = | C4 D4 E4 F4 |

Note: Correct:
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
}
```

### 5. Name Conflicts with Imports

Since imports execute in caller's scope with no namespacing, be careful with common names:

```flow
use "lib_a.flow"
use "lib_b.flow"
Note: if both define a function called "process", the second one wins
```

### 6. Snapshot Closure Capture

Lambdas capture variables at creation time, not at call time:

```flow
use "@std"

Int x = 10
Function f = fn Int n => (add n x)
x = 999
Int result = (f 5)  Note: 15, not 1004 (captured x=10)
```

### 7. Comparison Operators are Functions

There are no `==`, `<`, `>` operators. Use function calls:

```flow
use "@std"

Note: Wrong (this would be infix arithmetic, which is a parse error):
Note: Int result = x == 5

Note: Right:
Bool result = (equals x 5)
Bool isLess = (lt x 5)
Bool isMore = (gt x 5)
```

### 8. Division by Zero

Division by zero returns Void rather than crashing:

```flow
use "@std"

Int result = (div 10 0)  Note: reports error, returns Void
```

### 9. Reserved Context-Block Keywords

`tempo`, `timesig`, `key`, `swing`, `voicePool`, `tuning`, `sustainPedal`, `pan`, `gain`, `reverbTime`, `dynamics`, `rit`, `accel` are reserved — you cannot redefine them as proc or variable names. This is a hard rule (no `enable` to relax it).

## Array Indexing with @

Use `@` instead of `[]` brackets for array access:

```flow
Int[] nums = (list 10 20 30)
Int first = nums@0
Int second = nums@1
Int last = nums@-1        Note: negative indexes count from the end
```

`slice(arr, start, end)` returns a sub-array.

## Rendering Audio: The Full Pattern

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Note: 1. Define sections
            section intro {
                Sequence mel = | C4 E4 G4 C5 |
            }

            Note: 2. Arrange into song
            Song song = [intro]

            Note: 3. Render
            Buffer buf = (renderSong song "piano")

            Note: 4. Process
            Buffer final = buf -> reverb 0.3 -> fadeOut 0.5

            Note: 5. Export
            (writeWav "output.wav" final)
        }
    }
}
```

## Type Annotations for Lambdas

Use arrow syntax for precise typing:

```flow
(Int => Int) doubler = fn Int n => (mul n 2)
(Int, Int => Int) adder = fn Int a, Int b => (add a b)
(Void => Int) thunk = fn => 42
```

The generic `Function` type works too but provides less type safety.

## Euclidean Rhythms

Generate evenly-distributed patterns:

```flow
use "@std"

Note: 3 hits spread across 8 steps, using C4
Sequence euclid = (euclidean 3 8 C4)
(print (str euclid))
```

## Loops for Stateful Work

For counting, accumulating, or early-exit patterns, use `for` / `while`:

```flow
use "@std"

Int total = 0
for Int n in (list 1 2 3 4 5) {
    total = (add total n)
}
(print $"total: {total}")
```

See [Loops](Loops.md).

## See Also

- [Quick Start](Quick-Start.md) - Getting started
- [Language Basics](Language-Basics.md) - Fundamentals
- [Examples](Examples.md) - Complete working programs
- [Loops](Loops.md) - `for`, `while`, `break`, `continue`
- [String Interpolation](String-Interpolation.md) - `$"..."` syntax
