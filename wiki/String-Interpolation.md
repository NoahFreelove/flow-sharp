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

Anything inside `{ ... }` is evaluated as a full Flow expression:

```flow
use "@std"

Int a = 3
Int b = 4
(print $"sum is {a + b}")              Note: sum is 7

Int y = 10
(print $"y doubled is {y * 2}")        Note: y doubled is 20

Int[] items = (list 1 2 3)
(print $"count: {items -> length}")    Note: count: 3
```

## Supported Value Types

Any value that can be converted to a string works inside an interpolation. The expression is automatically stringified via `str`:

```flow
use "@std"

Int count = 5
Double pi = 3.14
Bool active = true
String name = "Flow"

(print $"count: {count}, pi: {pi}, active: {active}, name: {name}")
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
$"y doubled is {y * 2}" -> print
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

Buffer buf = (createSineTone 0.5 440.0 0.5)
(print $"frames: {(getFrames buf)}, channels: {(getChannels buf)}")
```

### Render Status

```flow
Buffer rendered = (renderSong song "piano")
Int frames = (getFrames rendered)
Int seconds = (div frames 44100)
(print $"rendered {seconds}s of audio")
```

### Labels in Loops

```flow
use "@std"

for Int i in (range 0 5) {
    (print $"iteration {i}")
}
```

## See Also

- [Language Basics](Language-Basics.md) - Core string operations
- [Tips and Tricks](Tips-and-Tricks.md) - Formatting idioms
