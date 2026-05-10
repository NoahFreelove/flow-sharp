# Flow Operator (`->`)

The flow operator `->` is Flow's pipe operator. It passes the left-hand value as the first argument to the right-hand function, enabling clean, readable chains.

## Basic Syntax

```
value -> function
```

is equivalent to:

```
(function value)
```

## How It Works

The flow operator is a **parse-time transform**. When the parser sees `x -> func(arg)`, it rewrites it to `func(x, arg)` as a `FunctionCallExpression`. There is no special runtime concept — it's pure syntactic sugar.

## Single-Argument Piping

```flow
use "@std"

"Hello, World!" -> print
Note: equivalent to: (print "Hello, World!")

5 -> str -> print
Note: equivalent to: (print (str 5))
```

## Multi-Argument Piping

The piped value becomes the **first** argument. Additional arguments follow the function name:

```flow
use "@std"

"Hello" -> concat " World" -> print
Note: equivalent to: (print (concat "Hello" " World"))
```

## Chaining Multiple Functions

Chains read left-to-right, making data transformations intuitive:

```flow
use "@std"

"Start" -> concat " Middle" -> concat " End" -> print
Note: prints "Start Middle End"
```

## Musical Transform Chains

The flow operator is particularly natural for musical transformations:

```flow
use "@std"

timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |

    Note: Transpose up 2 semitones
    Sequence t = mel -> transpose +2st

    Note: Shift up 1 octave
    Sequence high = mel -> up 1

    Note: Repeat 3 times with cumulative transposition
    Sequence rising = mel -> repeat 3 +4st
}
```

## Effect Chains

Audio effects chain naturally with `->`:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 0.5 440.0 0.5)

Double negThree = (sub 0.0 3.0)
Buffer processed = tone -> lowpass 1000.0 -> reverb 0.3 -> gain negThree
```

This is equivalent to:

```flow
Buffer processed = (gain (reverb (lowpass tone 1000.0) 0.3) negThree)
```

The flow operator version reads in the natural signal-processing order: filter, then reverb, then gain.

## Expression Transform Chains

Combine musical transforms with `->`:

```flow
timesig 4/4 {
    Sequence mel = | C4 D4 E4 F4 |

    Note: Humanize then crescendo
    Sequence expressive = mel -> humanize 0.1 -> crescendo 0.3 0.9
}
```

## With Lambdas

Lambdas work as pipe targets:

```flow
use "@std"

Function doubler = fn Int n => (mul n 2)
Function tripler = fn Int n => (mul n 3)

Int result = 5 -> doubler
(print (str result))  Note: 10

Int chained = 3 -> doubler -> tripler
(print (str chained))  Note: 18
```

## Comparison: Pipe vs Nested Calls

| Style | Code |
|-------|------|
| Nested | `(gain (reverb (lowpass tone 1000.0) 0.3) -3.0)` |
| Piped | `tone -> lowpass 1000.0 -> reverb 0.3 -> gain -3.0` |

The piped version reads left-to-right in the order operations are applied.

## When to Use `->` vs Parenthesized Calls

- Use `->` for **linear chains** where data flows through a series of transformations
- Use parenthesized calls `(func arg)` for **branching logic**, nested calls, or when the piped argument isn't the first parameter

## See Also

- [Functions](Functions.md) - Function declarations and lambdas
- [Effects](Effects.md) - Audio effect chains
- [Pattern Transforms](Pattern-Transforms.md) - Musical transform chains
