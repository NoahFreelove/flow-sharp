# Language Basics

Flow is a statically-typed, interpreted language. Every variable has a type, statements are separated by newlines or semicolons, and comments use the `Note:` keyword.

## Comments

Comments begin with `Note:` and extend to the end of the line:

```flow
Note: This is a comment
Int x = 5  Note: inline comment
```

## Variables

Variables are declared with a type annotation:

```flow
Int x = 5
Float pi = 3.14
String name = "Flow"
Bool active = true
```

### Reassignment

Variables can be reassigned after declaration:

```flow
Int x = 10
x = 20
x = x + 5
(print (str x))  Note: prints 25
```

## Semicolons

Statements are separated by newlines. Semicolons allow multiple statements on one line:

```flow
Int a = 1; Int b = 2; Int c = 3
(print (str a)); (print (str b)); (print (str c))
```

## Primitive Types

| Type | Description | Example |
|------|-------------|---------|
| `Int` | 32-bit integer | `42` |
| `Long` | 64-bit integer | - |
| `Float` | 32-bit float | `3.14` |
| `Double` | 64-bit float | `3.14` |
| `String` | Text | `"hello"` |
| `Bool` | Boolean | `true`, `false` |
| `Number` | Arbitrary precision | - |

### Numeric Widening

Flow supports implicit numeric widening:

```
Int -> Long -> Float -> Double -> Number
```

An `Int` can be used wherever a `Double` is expected, for example.

## Special (Music) Types

| Type | Description | Example |
|------|-------------|---------|
| `Note` | Musical pitch | `C4`, `F#5` |
| `Chord` | Harmonic chord | `Cmaj7`, `Dm` |
| `Sequence` | Ordered bars | `\| C4 D4 E4 F4 \|` |
| `Bar` | Musical measure | - |
| `Section` | Named song part | `section intro { ... }` |
| `Song` | Arrangement | `[intro verse chorus]` |
| `Buffer` | Audio samples | - |
| `Semitone` | Pitch offset | `+2st` |
| `Cent` | Microtonal offset | `+50c` |
| `Millisecond` | Time in ms | `100ms` |
| `Second` | Time in seconds | `2.5s` |
| `Decibel` | Gain in dB | `-3dB` |
| `Beat` | Musical beat | - |
| `NoteValue` | Note duration | - |
| `TimeSignature` | Meter | - |
| `MusicalNote` | Note with duration | - |
| `Voice` | Positioned audio | - |
| `Track` | Voice collection | - |
| `Envelope` | Amplitude shape | - |

## Arrays

Arrays are typed collections:

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5)
String[] names = (list "Alice" "Bob" "Charlie")
```

Array indexing uses `@`:

```flow
Int first = nums@0
Int second = nums@1
```

See [Collections](Collections.md) for full array operations.

## Operators

### Arithmetic (Binary)

```flow
Int sum = 3 + 4       Note: 7
Int diff = 10 - 3     Note: 7
Int prod = 5 * 6      Note: 30
Int quot = 15 / 4     Note: 3 (integer division)
```

### Arithmetic (Function-style)

```flow
use "@std"

Int sum = (add 3 4)
Int diff = (sub 10 3)
Int prod = (mul 5 6)
Int quot = (div 15 4)
```

### Comparison

Comparisons are function calls:

```flow
use "@std"

Bool eq = (equals 5 5)      Note: true
Bool lt = (lt 3 5)           Note: true
Bool gt = (gt 10 5)          Note: true
Bool lte = (lte 3 3)         Note: true
Bool gte = (gte 5 3)         Note: true
Bool seq = (sequals 5 5)     Note: strict equals (type must match)
```

### Logical

```flow
use "@std"

Bool a = (and true false)    Note: false
Bool b = (or true false)     Note: true
Bool c = (not true)          Note: false
```

`and` and `or` support lazy evaluation with `Lazy` arguments.

### String Concatenation

```flow
use "@std"

String greeting = (concat "Hello, " "World!")
```

## Control Flow

### Conditional (if)

`if` is a function that takes a boolean and two `lazy` expressions. You **must** wrap both branches with `lazy ()`:

```flow
use "@std"

Int x = 10

Note: Returning a value from if
String result = (if (gt x 5) lazy ("big") lazy ("small"))
(print result)  Note: prints "big"

Note: Side-effect branches
(if (gt x 5) lazy ((print "big")) lazy ((print "small")))
```

Both branches must be provided. The non-taken branch is not evaluated because `lazy ()` defers evaluation until forced. Without `lazy`, arguments are eagerly evaluated and the overload won't match.

### Lazy Evaluation

`lazy (expr)` creates a deferred value (a thunk) that is not evaluated until forced with `eval`:

```flow
use "@std"

Lazy<Void> deferred = lazy ((print "hello"))
Note: nothing printed yet

(eval deferred)  Note: now prints "hello"
```

`if`, `and`, and `or` accept `Lazy` parameters to enable short-circuit evaluation. This is why their arguments must be wrapped in `lazy ()`.

## Type Annotations

Function type annotations use parenthesized arrow syntax:

```flow
(Int => Int) doubler = fn Int n => (mul n 2)
(Int, Int => Int) adder = fn Int a, Int b => (add a b)
(Void => Int) constant = fn => 42
```

## Type Aliases

`Strings` is an alias for `String[]`, and `Voids` is an alias for `Void[]` (any array).

## Scoping

Variables declared inside blocks (functions, musical context blocks, sections) are scoped to that block. Inner scopes can access variables from outer scopes.

## See Also

- [Functions](Functions.md) - Procedures and lambdas
- [Flow Operator](Flow-Operator.md) - The `->` pipe
- [Collections](Collections.md) - Arrays and list operations
- [Musical Context](Musical-Context.md) - Musical context blocks
