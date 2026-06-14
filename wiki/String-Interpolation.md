# String Interpolation

Interpolated strings let you embed expressions directly inside a string literal. Prefix a string with `$` and wrap expressions in `{ }`.

## Basic Syntax

```flow
use "@std"

Int x = 42
(print $"x is {x}")            Note: x is 42

String name = "Flow"
(print $"hello {name}!")        Note: hello Flow!
```

The `$` prefix activates interpolation. Without it, braces are literal characters and the string is not interpolated.

## Expressions Inside Braces

Anything inside `{ ... }` is evaluated as a full Flow expression. Use the prefix arithmetic builtins for math:

```flow
use "@std"

Int a = 3
Int b = 4
(print $"sum is {(add a b)}")              Note: sum is 7

Int y = 10
(print $"y doubled is {(mul y 2)}")        Note: y doubled is 20

Int[] items = (list 1 2 3)
(print $"count: {items -> len}")           Note: count: 3
```

## Supported Value Types

Any value that can be converted to a string works inside an interpolation. The expression is converted to its string representation automatically. Note that for `Note` values, direct interpolation (`{pitch}`) includes surrounding quotes (e.g. `"C4"`); use `(str pitch)` inside the braces to get the bare name (`C4`) instead:

```flow
use "@std"

Int count = 5
Double pi = 3.14
Bool active = true
String name = "Flow"
Symbol style = #jazz
Note pitch = C4

(print $"count: {count}, pi: {pi}, active: {active}")
(print $"name: {name}, style: {style}, pitch: {(str pitch)}")
```

## Multiple Interpolations

Mix text and expressions freely:

```flow
use "@std"

Int bpm = 120
String mykey = "Cmajor"
(print $"playing at {bpm} bpm in {mykey}")
```

## Expression-Only

A string with nothing but an interpolation still works:

```flow
use "@std"

Int val = 99
(print $"{val}")    Note: 99
```

## Escaping Braces

Literal `{` and `}` characters inside an interpolated string are written as `\{` and `\}`:

```flow
use "@std"

(print $"set theory: \{a, b, c\}")
Note: prints: set theory: {a, b, c}
```

The same backslash escapes that work in plain strings — `\n`, `\r`, `\t`, `\"`, `\\` — also work inside `$"..."`.

## Nested Braces Are Not Supported

The interpolation lexer matches braces at depth-1 only. If you need a brace-bearing literal inside an expression (or want to interpolate a string that itself contains an interpolation), break it out into a variable first:

```flow
use "@std"

String inner = $"depth-{1}"
(print $"value: {inner}")        Note: value: depth-1
```

A bare nested `{` inside the interpolated expression position reports a parse error: *Nested braces not supported in string interpolation*.

## Assigning to a Variable

Interpolated strings are ordinary `String` values:

```flow
use "@std"

Int score = 100
String msg = $"score: {score}"
(print msg)
```

## With the Flow Operator

Interpolated strings work as pipe arguments too:

```flow
use "@std"

Int y = 10
$"y doubled is {(mul y 2)}" -> print
```

## No Interpolation Fallback

A `$"..."` string with no `{ }` blocks behaves exactly like a plain string:

```flow
use "@std"

(print $"no interpolation here")    Note: no interpolation here
```

Plain `"..."` strings (without `$`) leave `{ }` untouched:

```flow
use "@std"

(print "regular {braces} ok")    Note: regular {braces} ok
```

## Common Uses

### Debug Prints

```flow
use "@std"
use "@audio"

Buffer myBuf = (createSineTone 0.5 440.0 0.5)
(print $"frames: {(getFrames myBuf)}, channels: {(getChannels myBuf)}")
```

### Render Status

```flow
use "@std"
use "@audio"

section verse {
    | C4q D4q E4q F4q |
}

Song song = [verse]
Buffer rendered = (renderSong song "piano")
Int frames = (getFrames rendered)
Int seconds = (idiv frames 44100)
(print $"rendered {seconds}s of audio")
```

### Labels in Loops

```flow
use "@std"

for Int i in (range 0 5) {
    (print $"iteration {i}")
}
```

### Dict + Tuple Reporting

```flow
use "@std"

Dict<Symbol, Int> bpms = (dict #verse 120 #chorus 140)
for Symbol k in (keys bpms) {
    (print $"{k} = {(get bpms k)}")
}

<<Int x, Int y>> = <<3, 4>>
(print $"point: ({x}, {y})")
```

## See Also

- [Language Basics](Language-Basics.md) - Core string operations, tuples, dicts
- [Functions](Functions.md) - Named-argument calls inside interpolations
- [Tips and Tricks](Tips-and-Tricks.md) - Formatting idioms
