# Collections

Flow ships two general-purpose collection types: **arrays** (ordered, indexable, homogeneous) and **dictionaries** (`Dict<K, V>`, insertion-order preserved). Most collection functions require `use "@std"` (which transitively imports `@collections`).

## Creating Arrays

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5)
String[] names = (list "Alice" "Bob" "Charlie")
Int[] empty = (list)

Note: Array literal syntax also works — comma OR space-separated
Int[] inline   = [10, 20, 30]
Int[] spaceSep = [10 20 30]
```

## Array Indexing

Use `@` to access elements by index (0-based). `@` indexing is a core feature — no import needed. Negative indices count from the end; use `(neg N)` because bare `-N` does not parse:

```flow
Int[] nums = (list 10 20 30)
Int first = nums@0       Note: 10
Int second = nums@1      Note: 20
Int last = nums@(neg 1)  Note: 30 (negative index via (neg 1))
```

## Inspection

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5)

Int count    = (len nums)           Note: 5
Bool isEmpty = (empty nums)         Note: false
Bool has3    = (contains nums 3)    Note: true
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

Int[] rest = (tail nums)         Note: [2, 3, 4, 5]
Int[] front = (init nums)        Note: [1, 2, 3, 4]
Int[] firstTwo = (take nums 2)   Note: [1, 2]
Int[] lastThree = (drop nums 2)  Note: [3, 4, 5]
Int[] middle = (slice nums 1 4)  Note: [2, 3, 4] (half-open)
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

Int[] oneToFive = (range 1 6)        Note: [1, 2, 3, 4, 5] (half-open)
Int[] evens     = (range 0 20 2)     Note: [0, 2, ..., 18] (3-arg step)
```

## Zip

`zip` is not yet registered in the engine. Pair elements manually via `range` + index access until it ships:

```flow
use "@std"

Int[] a = (list 1 2 3)
Int[] b = (list 10 20 30)

Note: zip is not yet available — pair elements by index using range
Int[] indices = (range 0 (len a))
(each indices (fn Int i => (print $"{a@i} -> {b@i}")))
Note: prints "1 -> 10", "2 -> 20", "3 -> 30"
```

## Higher-Order Functions

### `map`

Transform each element:

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5)
Int[] doubled = (map nums (fn Int n => (mul n 2)))
(print (str doubled))  Note: [2, 4, 6, 8, 10]
```

### `filter`

Keep elements matching a predicate:

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5)
Int[] big = (filter nums (fn Int n => (gt n 3)))
(print (str big))  Note: [4, 5]
```

### `reduce`

Fold an array with an accumulator:

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5)
Int total = (reduce nums 0 (fn Int acc, Int n => (add acc n)))
(print (str total))  Note: 15
```

### `each`

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

Note: Filter even numbers, double them, sum them
Int[] evens   = (filter nums (fn Int n => (equals 0 (sub n (mul (idiv n 2) 2)))))
Int[] doubled = (map evens (fn Int n => (mul n 2)))
Int total     = (reduce doubled 0 (fn Int acc, Int n => (add acc n)))
(print (str total))
```

## Dictionaries (`Dict<K, V>`)

`Dict<K, V>` is a generic map that preserves insertion order. Allowed key types: `Int`, `Long`, `Float`, `String`, `Symbol`, `Note`, `Chord`, `Beat`, and tuples whose components are themselves hashable. Values are unrestricted.

### Creating a Dict

```flow
use "@std"

Note: Alternating key, value, key, value, ...
Dict<Symbol, Int> bpms = (dict #verse 120 #chorus 140 #bridge 100)

Note: From tuples of <<K, V>>
Dict<String, Note> roots = (dictTuple <<"verse", C4>> <<"chorus", G4>>)
```

### 14-Op Surface

| Op | Signature | Description |
|----|-----------|-------------|
| `dict` | `(... K V K V) -> Dict<K, V>` | Construct from key/value pairs |
| `dictTuple` | `(... <<K, V>>) -> Dict<K, V>` | Construct from tuple pairs |
| `get` | `(Dict, K) -> V` | Fetch value (errors if missing) |
| `getOr` | `(Dict, K, V) -> V` | Fetch value or fall back to default |
| `set` | `(Dict, K, V) -> Dict` | Return new dict with key set |
| `remove` | `(Dict, K) -> Dict` | Return new dict with key removed |
| `has` | `(Dict, K) -> Bool` | Key exists? |
| `keys` | `(Dict) -> K[]` | Insertion-ordered keys |
| `values` | `(Dict) -> V[]` | Insertion-ordered values |
| `size` | `(Dict) -> Int` | Number of entries |
| `merge` | `(Dict, Dict) -> Dict` | Right-biased merge |
| `each` | `(Dict, (K, V) => Void) -> Void` | Iterate for side effects |
| `map` | `(Dict, (K, V) => U) -> Dict<K, U>` | Transform values |
| `filter` | `(Dict, (K, V) => Bool) -> Dict` | Keep entries matching predicate |

### Examples

```flow
use "@std"

Dict<Symbol, Int> bpms = (dict #verse 120 #chorus 140)

Int v = (get bpms #verse)               Note: 120
Int fb = (getOr bpms #outro 100)        Note: 100 (fallback)
Dict<Symbol, Int> bigger = (set bpms #outro 90)
Dict<Symbol, Int> withoutChorus = (remove bpms #chorus)

(each bpms (fn Symbol k, Int v => (print $"{k} = {v}")))
```

### Overloaded with Arrays

`each`, `map`, `filter`, and `size` are overloaded on **both** arrays and dicts — the resolver dispatches on argument types. The same function call shape works for either collection.

## Tuples (Brief)

Tuples (`<<a, b, c>>`) live in [Language Basics](Language-Basics.md#tuples) — they're a fixed-arity, heterogeneous shape distinct from arrays. They support per-position types, structural equality, destructuring assignment (`<<x, y>> = expr`), and the tuple-unpack flow operator `~>`.

## Varargs and Plural Type Notation

Flow has two mechanisms for working with variable-length argument lists in function declarations.

### Plural Form (Array Type Sugar)

Adding `s` to any type name creates an array type:

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
- `list` uses `Void...: items` — call `(list 1 2 3)` with individual args
- `head` uses `Voids: arr` — call `(head myArray)` with an array

### Plural Forms Reference

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

## Complete Array Function Reference

| Function | Signature | Description |
|----------|-----------|-------------|
| `list` | `(...T) -> T[]` | Create array from arguments |
| `len` | `(T[]) -> Int` | Array length |
| `head` | `(T[]) -> T` | First element |
| `tail` | `(T[]) -> T[]` | All except first |
| `last` | `(T[]) -> T` | Last element |
| `init` | `(T[]) -> T[]` | All except last |
| `empty` | `(T[]) -> Bool` | Is array empty? |
| `reverse` | `(T[]) -> T[]` | Reverse order |
| `take` | `(T[], Int) -> T[]` | First N elements |
| `drop` | `(T[], Int) -> T[]` | Drop first N |
| `slice` | `(T[], Int, Int) -> T[]` | Half-open slice |
| `append` | `(T[], T) -> T[]` | Add to end |
| `prepend` | `(T, T[]) -> T[]` | Add to start |
| `concat` | `(T[], T[]) -> T[]` | Concatenate arrays |
| `contains` | `(T[], T) -> Bool` | Element exists? |
| `map` | `(T[], T => U) -> U[]` | Transform elements |
| `filter` | `(T[], T => Bool) -> T[]` | Filter by predicate |
| `reduce` | `(T[], U, (U, T) => U) -> U` | Fold with accumulator |
| `each` | `(T[], T => Void) -> Void` | Apply for side effects |
| `range` | `(Int, Int[, Int]) -> Int[]` | Half-open integer range with optional step |

## See Also

- [Language Basics](Language-Basics.md) - Tuples and dicts in the type system
- [Functions](Functions.md) - Lambdas, higher-order functions, named args
- [Standard Library](Standard-Library.md) - Full function reference
- [Flow Operator](Flow-Operator.md) - Chaining operations with `->`, tuple unpack with `~>`
