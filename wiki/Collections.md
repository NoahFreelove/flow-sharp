# Collections

Flow provides arrays (lists) with a full set of functional operations. Most collection functions require `use "@std"` or `use "@collections"`.

## Creating Arrays

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5)
String[] names = (list "Alice" "Bob" "Charlie")
Int[] empty = (list)
```

## Array Indexing

Use `@` to access elements by index (0-based):

```flow
Int[] nums = (list 10 20 30)
Int first = nums@0   Note: 10
Int second = nums@1  Note: 20
Int third = nums@2   Note: 30
```

## Inspection

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5)

Int count = (length nums)    Note: 5 (alias: len)
Bool isEmpty = (empty nums)  Note: false
Bool has3 = (contains nums 3) Note: true
```

## Accessing Elements

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5)

Int first = (head nums)   Note: 1
Int last = (last nums)    Note: 5
```

## Slicing

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5)

Int[] rest = (tail nums)     Note: [2, 3, 4, 5]
Int[] front = (init nums)    Note: [1, 2, 3, 4]
Int[] firstTwo = (take nums 2)  Note: [1, 2]
Int[] lastThree = (drop nums 2) Note: [3, 4, 5]
```

## Building Arrays

```flow
use "@std"

Int[] nums = (list 1 2 3)

Int[] withFour = (append nums 4)      Note: [1, 2, 3, 4]
Int[] withZero = (prepend 0 nums)     Note: [0, 1, 2, 3]
Int[] doubled = (concat nums nums)    Note: [1, 2, 3, 1, 2, 3]
Int[] flipped = (reverse nums)        Note: [3, 2, 1]
```

## Range

```flow
use "@std"

Int[] oneToFive = (range 1 6)    Note: [1, 2, 3, 4, 5]
```

## Zip

```flow
use "@std"

Int[] a = (list 1 2 3)
Int[] b = (list 10 20 30)
Int[][] zipped = (zip a b)  Note: [[1,10], [2,20], [3,30]]
```

## Higher-Order Functions

### map

Transform each element:

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5)
Int[] doubled = (map nums (fn Int n => (mul n 2)))
(print (str doubled))  Note: [2, 4, 6, 8, 10]
```

### filter

Keep elements matching a predicate:

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5)
Int[] big = (filter nums (fn Int n => (gt n 3)))
(print (str big))  Note: [4, 5]
```

### reduce

Fold an array with an accumulator:

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5)
Int total = (reduce nums 0 (fn Int acc, Int n => (add acc n)))
(print (str total))  Note: 15
```

### each

Apply a function to each element for side effects:

```flow
use "@std"

(each (list 10 20 30) (fn Int n => (print (str n))))
Note: prints 10, 20, 30 on separate lines
```

## Combining Operations

Use the flow operator to chain collection operations:

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5 6 7 8 9 10)

Note: Filter even numbers, double them, sum
Int[] evens = (filter nums (fn Int n => (equals 0 (sub n (mul (div n 2) 2)))))
Int[] doubled = (map evens (fn Int n => (mul n 2)))
Int total = (reduce doubled 0 (fn Int acc, Int n => (add acc n)))
(print (str total))
```

## Varargs and Plural Type Notation

Flow has two mechanisms for working with variable-length argument lists in function declarations.

### Plural Form (Array Type Sugar)

Adding `s` to any type name creates an array type. This works in both variable declarations and proc parameters:

```flow
use "@std"

Note: These two declarations are equivalent:
Int[] nums = (list 1 2 3)
Ints nums2 = (list 4 5 6)

Note: Works for any type:
Strings names = (list "Alice" "Bob")
Notes melody = (list C4 D4 E4)
Voids anything = (list 1 "two" true)
```

In proc parameters, the plural form declares an **array parameter** — the caller must pass an array:

```flow
proc sumAll (Ints: numbers)
    (reduce numbers 0 (fn Int acc, Int n => (add acc n)))
end proc

Int[] data = (list 1 2 3 4 5)
Int total = (sumAll data)  Note: pass an array
```

### Ellipsis Varargs (`...`)

The `...` syntax after a type in proc parameters declares a **varargs parameter** — the function collects remaining arguments into an array:

```flow
proc showAll (Void...: items)
    (each items (fn Void item => (print (str item))))
end proc

(showAll 1 "two" true)  Note: pass individual arguments
```

### Key Difference

| Syntax | In Parameter | Caller Passes |
|--------|-------------|---------------|
| `Ints: x` | Array parameter (`Int[]`) | A single array value |
| `Int...: x` | Varargs parameter | Individual arguments, collected into array |

The standard library uses both. For example:
- `list` uses `Void...: items` — you call `(list 1 2 3)` with individual args
- `head` uses `Voids: arr` — you call `(head myArray)` with an array

### Plural Forms Reference

Any type gets a plural form by appending `s`:

| Singular | Plural (Array) |
|----------|---------------|
| `Int` | `Ints` = `Int[]` |
| `String` | `Strings` = `String[]` |
| `Double` | `Doubles` = `Double[]` |
| `Bool` | `Bools` = `Bool[]` |
| `Note` | `Notes` = `Note[]` |
| `Void` | `Voids` = `Void[]` (any array) |
| `Buffer` | `Buffers` = `Buffer[]` |
| `Sequence` | `Sequences` = `Sequence[]` |

## Complete Function Reference

| Function | Signature | Description |
|----------|-----------|-------------|
| `list` | `(...T) -> T[]` | Create array from arguments |
| `length` / `len` | `(T[]) -> Int` | Array length |
| `head` | `(T[]) -> T` | First element |
| `tail` | `(T[]) -> T[]` | All except first |
| `last` | `(T[]) -> T` | Last element |
| `init` | `(T[]) -> T[]` | All except last |
| `empty` | `(T[]) -> Bool` | Is array empty? |
| `reverse` | `(T[]) -> T[]` | Reverse order |
| `take` | `(T[], Int) -> T[]` | First N elements |
| `drop` | `(T[], Int) -> T[]` | Drop first N |
| `append` | `(T[], T) -> T[]` | Add to end |
| `prepend` | `(T, T[]) -> T[]` | Add to start |
| `concat` | `(T[], T[]) -> T[]` | Concatenate arrays |
| `contains` | `(T[], T) -> Bool` | Element exists? |
| `map` | `(T[], T => U) -> U[]` | Transform elements |
| `filter` | `(T[], T => Bool) -> T[]` | Filter by predicate |
| `reduce` | `(T[], U, (U, T) => U) -> U` | Fold with accumulator |
| `each` | `(T[], T => Void) -> Void` | Apply for side effects |
| `range` | `(Int, Int) -> Int[]` | Integer range |
| `zip` | `(T[], U[]) -> [T,U][]` | Pair elements |

## See Also

- [Functions](Functions.md) - Lambdas and higher-order functions
- [Standard Library](Standard-Library.md) - Full function reference
- [Flow Operator](Flow-Operator.md) - Chaining operations with `->`
