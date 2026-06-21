# Flow Operators (`->`, `~>`)

Flow ships two pipe operators. `->` is the single-argument pipe — the workhorse for chaining transforms. `~>` is the tuple-unpack pipe — it spreads a tuple's slots into a multi-argument call.

## Basic Syntax (`->`)

```
value -> function
```

is equivalent to:

```
(function value)
```

## How It Works

The `->` operator is a **parse-time transform**. When the parser sees `x -> func(arg)`, it rewrites it to `func(x, arg)` as a `FunctionCallExpression`. There is no special runtime concept — it's pure syntactic sugar.

`~>` is the exception: tuple arity isn't known at parse time, so it always emits a `TupleUnpackFlowExpression` and the unpack happens at evaluation time. (Non-tuple LHS falls through to plain `->` semantics — see [Tuple-Unpack `~>`](#tuple-unpack-).)

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

Buffer tone = (createSineTone 440Hz 0.5 0.5)

Buffer processed = tone -> lowpass 1000Hz -> reverb 0.3 -> gain -3dB
```

This is equivalent to:

```flow
Buffer processed = (gain (reverb (lowpass tone 1000Hz) 0.3) -3dB)
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

## Naming Intermediates with `as NAME`

A chain step followed by `as NAME` binds the result of that step to a name in the current scope — useful when you want to debug or branch off mid-chain without breaking the pipeline:

```flow
use "@std"
use "@audio"

Buffer tone = (createSineTone 440Hz 0.5 0.5)

Buffer final = tone
            -> lowpass 1200Hz as filtered
            -> reverb 0.3      as wet
            -> gain -3dB

(print $"filtered frames: {(getFrames filtered)}")
(print $"wet frames:      {(getFrames wet)}")
```

`as NAME` is right-associative with `->` — only the `EXPR -> CALL as NAME -> ...` form is supported; `as` on the RHS of a stray `->` with no call doesn't apply.

## Tuple-Unpack (`~>`)

`~>` unpacks a tuple LHS into a multi-arg call. When the LHS isn't a tuple, it falls through to plain `->` (single-arg pipe) semantics:

```flow
use "@std"

proc renderHit (Note: pitch, Note: dur)
    (print $"hit: {pitch} {dur}")
end proc

Tuple<<Note, Note>> entry = <<C4, D4>>
entry ~> renderHit                    Note: (renderHit C4 D4)

Note: Non-tuple LHS — falls through to `->`
Int x = 5
proc doubleIt (Int: n)
    (mul n 2)
end proc
Int r = x ~> doubleIt                 Note: (doubleIt 5) — same as `->`
```

> **Tuple type annotations** require the `Tuple<<T1, T2>>` prefix — bare `<<T1, T2>>` on the left-hand side of an assignment is parsed as a destructure pattern, not a typed declaration.

There is also a runtime equivalent — `(unpack tuple func)` — which mirrors Lisp's `(apply f args)`:

```flow
Tuple<<Int, Int, Int>> trip = <<1, 2, 3>>
proc add3 (Int: a, Int: b, Int: c)
    (add a (add b c))
end proc

Int sum = (unpack trip add3)          Note: 6 — same as `trip ~> add3`
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
| Nested | `(gain (reverb (lowpass tone 1000Hz) 0.3) -3dB)` |
| Piped | `tone -> lowpass 1000Hz -> reverb 0.3 -> gain -3dB` |

The piped version reads left-to-right in the order operations are applied.

## When to Use `->` vs `~>` vs Parenthesized Calls

- Use `->` for **linear chains** where data flows through a series of transformations
- Use `~>` (or `(unpack ...)`) when you have a tuple whose slots should populate a multi-arg call
- Use parenthesized calls `(func arg1 arg2)` for **branching logic**, nested calls, or when the piped argument isn't the first parameter

## See Also

- [Language Basics](Language-Basics.md#tuples) - Tuple literals and destructuring
- [Functions](Functions.md) - Function declarations, lambdas, named args
- [Effects](Effects.md) - Audio effect chains
- [Pattern Transforms](Pattern-Transforms.md) - Musical transform chains
