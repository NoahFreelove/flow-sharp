# Tips and Tricks

Practical advice, idioms, and gotchas for writing Flow code.

## Always Import @std

Almost every program needs:

```flow
use "@std"
```

Without it, you can't use `print`, `str`, `concat`, `add`, `sub`, `list`, `map`, `filter`, or any other standard library function.

## Negative Numbers

Flow doesn't have negative number literals. Use `sub` to create negative values:

```flow
use "@std"

Int negative = (sub 0 5)         Note: -5
Double negFloat = (sub 0.0 3.14) Note: -3.14

Note: Common for dB values in audio
Double negSix = (sub 0.0 6.0)
Buffer quieter = (gain buf negSix)
```

## Optional Parentheses

Function calls with literal arguments can omit outer parentheses:

```flow
proc square (Int: n)
    n * n
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

Comments start with `Note:` — no `//` or `#` syntax:

```flow
Note: This is a comment
Int x = 5  Note: inline comment
```

## Printing Values

Always convert to string before printing:

```flow
use "@std"

Int x = 42
(print (str x))     Note: prints "42"
(print (str 3.14))  Note: prints "3.14"
(print (str true))  Note: prints "true"

Note: Concatenate for labeled output
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

## Common Pitfalls

### 1. Forgetting `use "@std"`

```flow
Note: ERROR: print is not defined
(print "hello")

Note: FIX:
use "@std"
(print "hello")
```

### 2. G7 vs Gdom7

`G7` is parsed as the note G at octave 7, not a G7 chord:

```flow
Note: This is a NOTE, not a chord:
Note g7note = G7

Note: This is the CHORD:
Chord g7chord = Gdom7
```

### 3. Missing Musical Context for Note Streams

```flow
Note: May not work correctly without timesig:
Sequence mel = | C4 D4 E4 F4 |

Note: Correct:
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |
}
```

### 4. Name Conflicts with Imports

Since imports execute in caller's scope with no namespacing, be careful with common names:

```flow
use "lib_a.flow"
use "lib_b.flow"
Note: if both define a function called "process", the second one wins
```

### 5. Snapshot Closure Capture

Lambdas capture variables at creation time, not at call time:

```flow
use "@std"

Int x = 10
Function f = fn Int n => (add n x)
x = 999
Int result = (f 5)  Note: 15, not 1004 (captured x=10)
```

### 6. Comparison Operators are Functions

There are no `==`, `<`, `>` operators. Use function calls:

```flow
use "@std"

Note: Wrong (this is arithmetic, not comparison):
Note: Int result = x == 5

Note: Right:
Bool result = (equals x 5)
Bool isLess = (lt x 5)
Bool isMore = (gt x 5)
```

### 7. Division by Zero

Division by zero returns Void rather than crashing:

```flow
use "@std"

Int result = (div 10 0)  Note: reports error, returns Void
```

## Array Indexing with @

Use `@` instead of `[]` brackets for array access:

```flow
Int[] nums = (list 10 20 30)
Int first = nums@0
Int second = nums@1
```

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
            (exportWav final "output.wav")
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

## See Also

- [Quick Start](Quick-Start.md) - Getting started
- [Language Basics](Language-Basics.md) - Fundamentals
- [Examples](Examples.md) - Complete working programs
