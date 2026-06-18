# Functions

Flow supports procedure declarations (`proc`), lambda functions, closures, function overloading, named arguments, function-type annotations, and higher-order functions.

## Procedure Declarations

Functions are declared with `proc` and terminated with `end proc`. Parameter types use the `Type: name` shape:

```flow
use "@std"

proc double (Int: x)
    (mul x 2)
end proc

Int result = (double 7)
(print (str result))  Note: 14
```

### Implicit Returns

The last non-void expression in a `proc` body is automatically the return value. Flow collects **every** non-void expression evaluated; the rules are:

- 0 collected -> `Void`
- 1 collected -> that value
- 2+ collected -> an **array** of all collected values

```flow
use "@std"

proc myAdd (Int: a, Int: b)
    (add a b)
end proc

Int sum = (myAdd 3 4)
(print (str sum))  Note: 7
```

To force a `Void` return from a body that would otherwise collect non-void expressions, use the explicit `(Nothing)` builtin as the last statement. A trailing void expression (like `(print ...)`) returns `Void` **only when no prior non-void expression was already collected** — if one was, the collected value(s) are still returned (1 collected -> that value, 2+ collected -> an array). When in doubt, use `(Nothing)` to be explicit.

### Explicit Returns

Use `return` to return early with a value. An explicit `return X` clears the collected list and short-circuits with `X`:

```flow
use "@std"

proc abs (Int: x)
    (if (lt x 0) lazy ((sub 0 x)) lazy (x))
end proc

Int a = (abs (neg 5))
(print (str a))  Note: 5
```

`return` is **not** valid inside `lazy((...))` — use the implicit-return form above instead. An explicit `return X` at the top level of a proc body works; there is **no** bare `return` (without a value) — write `(return (Nothing))` to return Void explicitly.

### Multiple Parameters

```flow
use "@std"

proc greet (String: name, String: greeting)
    (concat greeting (concat ", " name))
end proc

String msg = (greet "Flow" "Hello")
(print msg)  Note: Hello, Flow
```

## Calling Functions

Functions are called with parentheses around the call:

```flow
Int result = (double 5)
(print (str result))
```

### Optional Parentheses

Function calls can omit outer parentheses when arguments are simple literals or identifiers on the same line. This works at statement position for both builtins and user procs:

```flow
use "@std"

proc square (Int: n)
    (mul n n)
end proc

Int s1 = (square 4)
print "hello"            Note: same as (print "hello") at statement position
```

Complex parenthesized expressions as arguments still require explicit call syntax. A bare identifier with a zero-arg overload also auto-calls — `print` works as both a value reference and a call.

## Named Arguments

Any function with named parameters can be called using `name=value` syntax. Positional args must precede every named arg (same rule as Python / C#):

```flow
use "@std"
use "@generative"
use "@notation"

Sequence base = | C4 D4 E4 F4 G4 |

Note: All-named call (once a named arg appears, all remaining args must be named)
Sequence m = (markov corpus=base order=2 length=8 seed=42)

Note: All-positional — also fine
Sequence m2 = (markov base 2 8 42)

Note: Mixed — positional args first, then named
Sequence m3 = (markov base 2 length=8 seed=1)
```

About 150 builtin signatures have parameter names backfilled. Named args dispatch through the same `OverloadResolver` as positional ones — names are matched against `FunctionSignature.ParameterNames`.

Duplicate named args and "positional after named" are both reported as errors. The `features=` named arg belongs on `markovTrain`, not the one-shot `markov` overload.

## Lambda Functions

Lambdas are anonymous functions created with `fn`. Body is a single expression by default:

```flow
use "@std"

Note: Single parameter
Function doubler = fn Int n => (mul n 2)
Int r = (doubler 5)  Note: 10

Note: Multiple parameters
Function adder = fn Int a, Int b => (add a b)
Int s = (adder 3 4)  Note: 7

Note: Zero parameters
Function getFortyTwo = fn => 42
Int answer = (getFortyTwo)  Note: 42
```

### Multi-Statement Lambda Bodies

To run multiple statements inside a lambda, wrap the body in `( ... )` starting with a typed declaration. The parser detects this when `(` is followed by a type keyword:

```flow
use "@std"

Function classify = fn Int n => (
    Int doubled = (mul n 2)
    String label = (if (gt doubled 10) lazy ("big") lazy ("small"))
    label
)

String result = (classify 7)
(print result)        Note: "big"
```

### Function Type Annotations

Use parenthesized arrow syntax for precise type annotations:

```flow
(Int => Int) tripler = fn Int n => (mul n 3)
(Int, Int => Int) multiplier = fn Int a, Int b => (mul a b)
(Void => Int) constVal = fn => 99
```

The generic `Function` type also works as a catch-all:

```flow
Function myFunc = fn Int n => (mul n 2)
```

## Closures

Lambdas capture variables from their enclosing scope at the time of creation (snapshot capture):

```flow
use "@std"

Int x = 10
Function addX = fn Int n => (add n x)
Int result = (addX 5)  Note: 15

Note: Snapshot: changing x after creation doesn't affect the lambda
x = 999
Int result2 = (addX 5)  Note: still 15 (captured x=10)
```

## Higher-Order Functions

Functions can take other functions as arguments:

```flow
use "@std"
use "@collections"

Int[] nums = (list 1 2 3 4 5)

Note: Map - transform each element
Int[] doubled = (map nums (fn Int n => (mul n 2)))
(print (str doubled))  Note: [2, 4, 6, 8, 10]

Note: Filter - keep elements matching predicate
Int[] big = (filter nums (fn Int n => (gt n 3)))
(print (str big))  Note: [4, 5]

Note: Reduce - fold with accumulator
Int total = (reduce nums 0 (fn Int acc, Int n => (add acc n)))
(print (str total))  Note: 15

Note: Each - side effects
(each nums (fn Int n => (print (str n))))
```

`map`, `filter`, `each`, and `size` are overloaded on both **arrays** and **dicts** — see [Collections](Collections.md).

## Function Overloading

Multiple functions can share the same name with different parameter types. Flow's overload resolver picks the best match by scoring candidates: exact match (+1000), compatible type (+500), convertible type (+100). `Void` parameters act as wildcards.

```flow
use "@std"

Note: str() works on Int, Float, String, Bool, Note, Symbol, Sequence, etc.
(print (str 42))       Note: "42"
(print (str 3.14))     Note: "3.14"
(print (str true))     Note: "true"
(print (str #foo))     Note: "#foo"
```

User code can overload too — just declare multiple procs with the same name and different signatures.

## Varargs and Plural-Form Parameters

Flow has two ways to declare variable-length argument lists.

### Plural Form (Array Type Sugar)

Adding `s` to any type name creates an array type:

```flow
use "@std"

Note: These two are equivalent
Int[] nums = (list 1 2 3)
Ints nums2 = (list 4 5 6)

Note: In proc params, plural form means "caller passes an array"
proc sumAll (Ints: numbers)
    (reduce numbers 0 (fn Int acc, Int n => (add acc n)))
end proc

Int[] data = (list 1 2 3 4 5)
Int total = (sumAll data)
```

### Ellipsis Varargs (`...`)

`Type...` declares a varargs parameter — the caller passes individual args; the function receives them as an array:

```flow
proc showAll (Void...: items)
    (each items (fn Void item => (print (str item))))
end proc

(showAll 1 "two" true)
```

The standard library uses both. For example: `list` uses `Void...: items`; `head` uses `Voids: arr`.

## Lambdas with the Flow Operator

Lambdas work naturally with `->`:

```flow
use "@std"

Function doubler = fn Int n => (mul n 2)
Function tripler = fn Int n => (mul n 3)

Int result = 3 -> doubler -> tripler
(print (str result))  Note: 18 (3*2=6, 6*3=18)
```

## Nested Lambda Calls

```flow
use "@std"

Function doubler = fn Int n => (mul n 2)
Function tripler = fn Int n => (mul n 3)

Function compose = fn Int n => (doubler (tripler n))
Int result = (compose 2)
(print (str result))  Note: 12 (2*3=6, 6*2=12)
```

## Internal Procs (stdlib bridging)

Standard library `.flow` files use `internal proc` to declare the signatures of C# built-in functions:

```flow
internal proc head (Voids: arr)
internal proc map (Voids: arr, Function: callback)
```

These are signature-only forward declarations — they make the C# built-in functions visible to the Flow type checker. You don't need `internal proc` in your own code unless you're bridging your own native implementations.

## See Also

- [Flow Operator](Flow-Operator.md) - Chaining with `->`, tuple unpack with `~>`, `as NAME`
- [Collections](Collections.md) - `map`, `filter`, `reduce`, `Dict<K, V>`
- [Language Basics](Language-Basics.md) - Tuples, dicts, pattern matching, named args
- [Imports and Modules](Imports-and-Modules.md) - The `use` statement and stdlib modules
