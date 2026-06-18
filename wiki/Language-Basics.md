# Language Basics

Flow is a statically-typed, interpreted language. Every variable has a type, statements are separated by newlines or semicolons, and there are five comment styles for different positions in a line.

## Comments

Flow accepts five comment markers. All five run from their marker to the end of the line:

```flow
Note: This is a comment — works at line start AND as a trailing inline comment
TODO: also a comment to EOL (must start at line content)
FIXME: same — must start at line content
// This is also a comment, anywhere on the line
; A Lisp-style line comment — must be at column 0 (mid-line `;` is a statement separator)
Int x = 5  Note: inline comment — trailing Note: works anywhere on the line
Int y = 7  // inline comment
```

`TODO:` and `FIXME:` must start at the beginning of a line's content (leading whitespace allowed). `Note:` and `//` work anywhere on the line — both at line start and as trailing inline comments. Column-0 `;` must be at column 0.

A backslash at end-of-line is a **line continuation** — it joins the next physical line into the current logical line while preserving line numbers for diagnostics:

```flow
Int total = (add 1 \
             (add 2 \
              3))
```

## Variables

Variables are declared with a type annotation:

```flow
Int x = 5
Float pi = 3.14
String name = "Flow"
Bool active = true
Symbol tag = #verse
```

### Default Values

A variable declared without an initializer takes a type-appropriate default (`0`, `""`, `false`, empty array, etc.):

```flow
Int count           Note: count = 0
String message      Note: message = ""
Bool flag           Note: flag = false
Int[] nums          Note: nums = []
```

### Reassignment

Variables can be reassigned after declaration. Use the prefix arithmetic builtins for math (Flow has no infix `+ - * /`):

```flow
use "@std"

Int x = 10
x = 20
x = (add x 5)
(print (str x))     Note: prints 25
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
| `Number` | Arbitrary precision (BigInteger) | - |
| `Symbol` | Interned identity literal | `#foo` |
| `Lazy` | Deferred / memoized expression | `lazy (expr)` |
| `Function` | Callable value | `fn Int n => (mul n 2)` |
| `Buffer` | Audio samples | - |

### Numeric Widening

Flow supports implicit numeric widening:

```
Int -> Long -> Float -> Double -> Number
```

An `Int` can be used wherever a `Double` is expected. Integer literals that exceed `Int` range automatically fall through to `Long`, then `BigInteger` (`Number`) — no overflow error.

## Special (Music) Types

| Type | Description | Example |
|------|-------------|---------|
| `Note` | Musical pitch | `C4`, `F4+` (F sharp) |
| `Chord` | Harmonic chord | `Cmaj7`, `Dm` |
| `Sequence` | Ordered bars | `\| C4 D4 E4 F4 \|` |
| `Bar` | Musical measure | - |
| `Section` | Named song part | `section intro { ... }` |
| `Song` | Arrangement | `[intro verse chorus]` |
| `Semitone` | Pitch offset | `+2st`, `-3st` |
| `Cent` | Microtonal offset | `+50c`, `-25c` |
| `Millisecond` | Time in ms | `100ms` |
| `Second` | Time in seconds | `2.5s` |
| `Decibel` | Gain in dB | `+6dB`, `-12dB` |
| `Hertz` | Frequency | `440Hz`, `1.5kHz` |
| `Beat` | Musical beat | - |
| `NoteValue` | Note duration | - |
| `TimeSignature` | Meter | - |
| `MusicalNote` | Note with duration | - |
| `Voice` | Positioned audio | - |
| `Track` | Voice collection | - |
| `Envelope` | Amplitude shape | - |
| `Tuning` | Scala tuning (reference identity) | `(loadScala "x.scl")` |
| `Sfz` | SFZ sampler patch (reference identity) | `(loadSfz #violin)` |
| `MarkovModel` / `LsystemModel` | Generative models (reference identity) | `(markovTrain ...)` |

### Note Literals

Notes use `[A-G][octave][alteration]`:

| Syntax | Meaning |
|--------|---------|
| `C4` | C natural, octave 4 (middle C) |
| `C4+` | C sharp, octave 4 |
| `C4-` | C flat, octave 4 |
| `C4++` | C double sharp |
| `C4--` | C double flat |
| `C4+50c` | C4 + 50 cents (microtonal) |

Chord-symbol accidentals use `s` and `f` instead (for example, `Csmaj7`, `Bfm`). See [Chords and Harmony](Chords-and-Harmony.md).

## Symbols

A `Symbol` is an interned identity literal — like Ruby's `:foo` or Clojure's `:foo`. Two `#foo` literals always compare equal by reference, and a Symbol is **strictly distinct** from the string `"foo"`:

```flow
use "@std"

Symbol style = #jazz
Symbol same = #jazz
(print (str (equals style same)))     Note: true

Note: Symbols and Strings are NOT interchangeable
(print (str (equals #foo "foo")))     Note: false
```

Symbols are the natural choice for tags, enum-like flags, and `Dict` keys.

## Arrays

Arrays are typed collections:

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5)
String[] names = (list "Alice" "Bob" "Charlie")
Int[] inline = [10, 20, 30]      Note: array literal — comma OR space separated
```

Array indexing uses `@`. For negative (from-end) indices, bind the value to a variable first — bare negative literals after `@` do not parse:

```flow
Int first = nums@0
Int negOne = (sub 0 1)
Int last = nums@negOne    // last element
```

Plural type names are shorthand for arrays: `Ints` = `Int[]`, `Notes` = `Note[]`, `Strings` = `String[]`, `Voids` = `Void[]` (any), etc.

See [Collections](Collections.md) for full array (and `Dict`) operations.

## Tuples

Tuples group fixed-arity heterogeneous values. They're written with `<<` and `>>`, with per-position types:

```flow
use "@std"

Tuple<<Int, Int>> point = <<3, 4>>
Tuple<<String, Note, Bool>> mix = <<"snare", C4, true>>

Note: Empty and singleton arities are both valid
Tuple<<>> nothing = <<>>
Tuple<<Int>> solo = <<42>>

Note: Index with @N
Int x = point@0
Int y = point@1
```

> **Typed tuple declarations require the `Tuple<<...>>` prefix.** Bare `<<Type, Type>> name = ...` is parsed as a destructure assignment — the `Tuple<<...>>` prefix is required when you want a typed variable binding.

Tuples compare by **structural equality** (`(equals <<1, 2>> <<1, 2>>)` is true).

### Destructuring Assignment

Unpack a tuple into named bindings with `<<...>> =`:

```flow
<<Int x, Int y>> = <<3, 4>>
(print $"x={x} y={y}")

Note: Per-slot types are optional
<<a, b, c>> = <<1, "two", true>>
```

### Tuple-Unpack with `~>`

The `~>` flow operator unpacks a tuple into a multi-arg call. Non-tuple LHS falls through to plain `->` semantics:

```flow
proc renderHit (Note: pitch, Note: dur)
    (print $"hit: {pitch} {dur}")
end proc

Tuple<<Note, Note>> entry = <<C4, D4>>
entry ~> renderHit                    Note: equivalent to (renderHit C4 D4)
```

There's also a runtime equivalent — `(unpack tuple func)` — which mirrors Lisp's `(apply f args)`. See [Flow Operator](Flow-Operator.md).

## Dictionaries

Generic `Dict<K, V>` preserves insertion order. Allowed key types: `Int`, `Long`, `Float`, `String`, `Symbol`, `Note`, `Chord`, `Beat`, and tuples of any of these.

```flow
use "@std"

Dict<Symbol, Int> bpms = (dict #verse 120 #chorus 140 #bridge 100)

Int v   = (get bpms #verse)            Note: 120
Int fb  = (getOr bpms #outro 80)       Note: 80 (default)
Dict<Symbol, Int> bigger = (set bpms #outro 90)
Bool has = (has bpms #verse)           Note: true
Int n   = (size bpms)
Symbol[] ks = (keys bpms)
Int[] vs    = (values bpms)
```

`dict` takes alternating key/value pairs; `dictTuple` takes `<<K, V>>` tuples instead. Higher-order ops (`each`, `map`, `filter`) and `size` are overloaded — they work on both arrays and dicts; the resolver picks based on argument types.

See [Collections](Collections.md) for the full 14-op surface.

## Arithmetic and Other Operators

### Arithmetic

Arithmetic is **prefix-only** via standard-library builtins — there are no infix `+ - * /` operators (the parser will reject stray infix and suggest the prefix form).

```flow
use "@std"

Int sum  = (add 3 4)       Note: 7
Int diff = (sub 10 3)      Note: 7
Int prod = (mul 5 6)       Note: 30
Double q = (div 15 4)      Note: 3.75 — (div Int Int) auto-promotes to Double
Int qi   = (idiv 15 4)     Note: 3 — integer (truncating) division
Int neg  = (neg 5)         Note: -5

String greeting = (concat "Hello, " "World!")
```

Negative numeric literals (`-3`, `-2.5`, `-12dB`) lex as single tokens at expression-start positions (after `(`, `,`, `=`, `|`, etc.) — `(add x -3)` works as written.

### Comparison

Comparisons are function calls:

```flow
use "@std"

Bool eq  = (equals 5 5)      Note: true (compatible types)
Bool seq = (sequals 5 5)     Note: true (strict — types must match exactly)
Bool lt  = (lt 3 5)          Note: true
Bool gt  = (gt 10 5)         Note: true
Bool lte = (lte 3 3)         Note: true
Bool gte = (gte 5 3)         Note: true
```

### Logical

```flow
use "@std"

Bool a = (and true false)    Note: false
Bool b = (or true false)     Note: true
Bool c = (not true)          Note: false
```

`and` and `or` accept `Lazy` arguments for short-circuit evaluation.

## Control Flow

### Conditional (`if`)

`if` is a function that takes a boolean and two `lazy` expressions. You **must** wrap both branches with `lazy ()`:

```flow
use "@std"

Int x = 10

Note: returning a value
String result = (if (gt x 5) lazy ("big") lazy ("small"))
(print result)    Note: "big"

Note: side-effect branches
(if (gt x 5) lazy ((print "big")) lazy ((print "small")))
```

Without `lazy`, arguments are eagerly evaluated and the overload won't match.

### Pattern Matching

`match` is the structured alternative to chained `if`s. It scrutinizes a value against one or more arms:

```flow
use "@std"

Int n = 3
String label = (match n
                | 0 => "zero"
                | 1 => "one"
                | _ => "many")
(print label)

Note: Music-aware patterns — chord literals match by chord identity
String roleName = (match Cmaj7
                   | Cmaj7 => "tonic"
                   | Dm    => "ii"
                   | _     => "other")

Note: Binding patterns capture the matched value
String sign = (match n
               | x when (lt x 0) => "negative"
               | 0               => "zero"
               | x               => "positive")
```

Patterns supported: `_` wildcard, literal patterns (Int / Float / String / Bool / Note), binding patterns (bare identifier), constructor patterns (chord literals, roman numerals, articulation symbols), and `when (...)` guards.

To make non-exhaustive matches a hard error instead of a warning, add this pragma at the top of the file:

```flow
enable matchExhaustive;
```

### Loops

Flow supports `for` and `while` loops with `break` and `continue`:

```flow
use "@std"

Int sum = 0
for Int n in (list 1 2 3 4 5) {
    sum = (add sum n)
}

Int count = 0
while (lt count 5) {
    count = (add count 1)
}
```

Loops have a configurable maximum iteration count (default 10000) to prevent runaway execution — call `(setMaxIterations N)` to raise it. See [Loops](Loops.md) for the full reference.

### Lazy Evaluation

`lazy (expr)` creates a deferred value (a thunk) that is not evaluated until forced with `eval`. The result is memoized:

```flow
use "@std"

Lazy<Void> deferred = lazy ((print "hello"))
Note: nothing printed yet
(eval deferred)    Note: now prints "hello"
```

`if`, `and`, and `or` accept `Lazy` parameters to enable short-circuit evaluation.

## String Interpolation

Prefix a string with `$` and wrap expressions in `{ }`:

```flow
use "@std"

Int x = 42
(print $"x is {x}")                  Note: x is 42

Int a = 3
Int b = 4
(print $"sum is {(add a b)}")        Note: sum is 7
```

See [String Interpolation](String-Interpolation.md) for details (including `\{` / `\}` escapes).

## Named Arguments

Any function with named parameters can be called using `name=value` syntax. Positional and named args can mix, but every positional arg must precede every named one:

```flow
use "@std"
use "@improv"

Sequence chords = | Cmaj7 Am7 Dm7 G7 |

Note: All named (3-arg form — no seed)
Sequence solo = (jam over=chords style=#jazz length=8)

Note: Positional with seed — (jam over style length key seed)
Sequence seeded = (jam chords #jazz 8 "Cmajor" 42)

Note: Mixed — positional first, rest named
Sequence solo2 = (jam chords style=#blues length=16)
```

> `seed=` is not a valid standalone named argument for `jam` — it requires `key` to also be provided. Use `(jam over style length key seed)` positionally when you need a seed, or omit it to get a random result.

About 150 builtin signatures have parameter names backfilled. See [Functions](Functions.md#named-arguments).

## Function Type Annotations

Use parenthesized arrow syntax for precise function types:

```flow
(Int => Int) doubler = fn Int n => (mul n 2)
(Int, Int => Int) adder = fn Int a, Int b => (add a b)
(Void => Int) constant = fn => 42
```

## Type Aliases (Plural Shorthand)

Any type name ending in `s` refers to the array form: `Ints` = `Int[]`, `Strings` = `String[]`, `Voids` = `Void[]`, `Notes` = `Note[]`, etc.

## Scoping

Variables declared inside blocks (functions, musical context blocks, sections, loops) are scoped to that block. Inner scopes can access variables from outer scopes (lexical scope).

Proc calls use call-boundary frames: **a proc body cannot read or write its caller's locals** — only its own parameters, locals, and globals are in scope. Globals remain readable and writable from any proc. Lambdas capture by snapshot at the point they are created (lexical capture, not dynamic lookup).

Note: quoted string literals are always `String` values — `"10s"` is the string `"10s"`, not a `Second`. Music-typed values come only from bare tokens: `10s`, `C4`, `-3dB`, `440Hz`, etc.

## Pragmas

Pragmas are file-scope toggles at the top of a file. Each module has its own `PragmaSet` — pragmas don't leak across `use` imports:

```flow
enable hAsB;             // H aliases to B in note streams (German notation)
enable justIntonation;   // 5-limit JI tuning rooted at active key tonic
enable matchExhaustive;  // promote non-exhaustive match warnings to errors
```

> `Note:` comments do not work as trailing inline comments on `enable` pragma lines. The pragma scanner only recognizes `//` after `enable NAME;` — use `//` for all trailing comments on pragma lines.

Unknown pragmas error with a did-you-mean suggestion.

## See Also

- [Functions](Functions.md) - Procedures, lambdas, named args, function types
- [Flow Operator](Flow-Operator.md) - The `->` pipe, `~>` tuple unpack, `as NAME`
- [Collections](Collections.md) - Arrays, `Dict<K, V>`, list operations
- [Loops](Loops.md) - `for`, `while`, `break`, `continue`
- [String Interpolation](String-Interpolation.md) - `$"..."` syntax
- [Musical Context](Musical-Context.md) - Musical context blocks
- [Imports and Modules](Imports-and-Modules.md) - All 15 stdlib modules
