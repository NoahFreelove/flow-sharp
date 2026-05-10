# Functions

Flow supports procedure declarations (`proc`), lambda functions, closures, function overloading, and higher-order functions.

## Procedure Declarations

Functions are declared with `proc` and terminated with `end proc`:

```flow
proc double (Int: x)
    x * 2
end proc

Int result = (double 7)
(print (str result))  Note: 14
```

### Implicit Returns

The last non-void expression in a `proc` body is automatically the return value:

```flow
proc add (Int: a, Int: b)
    a + b
end proc

Int sum = (add 3 4)  Note: 7
```

### Explicit Returns

Use `return` to return early:

```flow
proc abs (Int: x)
    (if (lt x 0) (return (sub 0 x)) x)
end proc
```

### Multiple Parameters

```flow
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

When calling a function with literal arguments, parentheses are optional:

```flow
proc square (Int: n)
    n * n
end proc

Int s1 = square 4        Note: works with literal
Int s2 = (square val)    Note: parens needed for variable args
```

## Lambda Functions

Lambdas are anonymous functions created with `fn`:

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

### Function Type Annotations

Use arrow syntax for precise type annotations:

```flow
(Int => Int) tripler = fn Int n => (mul n 3)
(Int, Int => Int) multiplier = fn Int a, Int b => (mul a b)
(Void => Int) constVal = fn => 99
```

The generic `Function` type also works:

```flow
Function myFunc = fn Int n => n * 2
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

## Function Overloading

Multiple functions can share the same name with different parameter types. Flow's overload resolver picks the best match:

```flow
use "@std"

Note: str() works on Int, Float, String, Bool, Note, etc.
(print (str 42))       Note: "42"
(print (str 3.14))     Note: "3.14"
(print (str true))     Note: "true"
```

The resolver scores candidates: exact match (+1000), compatible type (+500), convertible type (+100).

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

## See Also

- [Flow Operator](Flow-Operator.md) - Chaining with `->`
- [Collections](Collections.md) - `map`, `filter`, `reduce`, and more
- [Standard Library](Standard-Library.md) - All built-in functions
