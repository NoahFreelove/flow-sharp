# Loops

Flow supports `for` and `while` loops with `break` and `continue` for flow control. Loops are statements — they execute side effects and don't return values.

## For Loops

Iterate over the elements of an array with `for T name in collection { ... }`:

```flow
use "@std"

Int[] nums = (list 1 2 3 4 5)
for Int n in nums {
    (print (str n))
}
```

The loop variable is declared with its type (no `:` required here) and is scoped to the loop body. `break` and `continue` only work inside `for` or `while` — using them outside raises a parse error.

### Iterating a Range

Combine with `range` to loop a fixed number of times. `range` is half-open: `(range 0 10)` produces `[0, 1, ..., 9]`. The 3-arg overload `(range start end step)` lets you control the stride:

```flow
use "@std"

for Int i in (range 0 10) {
    (print (str i))
}

for Int i in (range 0 20 2) {     Note: 0, 2, 4, ..., 18
    (print (str i))
}
```

### Nested For Loops

```flow
use "@std"

for Int i in [1, 2, 3] {
    for Int j in [10, 20] {
        (print (str (mul i j)))
    }
}
```

### Empty Arrays

Looping over an empty array runs the body zero times:

```flow
use "@std"

Int[] nothing = []
for Int x in nothing {
    (print "never runs")
}
```

### Looping Over Notes

The loop variable can be any type:

```flow
use "@std"

Note[] melody = (list C4 D4 E4 F4)
for Note n in melody {
    (print (str n))
}
```

### Looping Over Dict Entries

`(each dict cb)` is usually clearer than building keys/values arrays, but a `for` loop over `(keys d)` works too:

```flow
use "@std"

Dict<Symbol, Int> bpms = (dict #verse 120 #chorus 140)
for Symbol k in (keys bpms) {
    (print $"{k} = {(get bpms k)}")
}
```

## While Loops

Execute a block repeatedly while a condition is true. Use the prefix arithmetic builtins for counter math:

```flow
use "@std"

Int count = 0
while (lt count 5) {
    count = (add count 1)
}
(print (str count))    Note: 5
```

### Counting to a Target

Encode the exit condition directly in the `while` guard — this is the idiomatic pattern in Flow:

```flow
use "@std"

Int i = 0
while (lt i 5) {
    i = (add i 1)
}
(print (str i))
```

> **Note:** `break` and `continue` come in two forms — the bare statement keywords (`break` / `continue` on their own line) and the prefix-call builtins `(break)` / `(continue)`. The call form is what lets you gate them with `(if ...)` (see below). Encoding the exit condition directly in the `while` guard is still the cleanest pattern when it fits.

### Countdown

```flow
use "@std"

Int counter = 10
while (gt counter 0) {
    counter = (sub counter 1)
}
```

## `break`

`break` exits the innermost enclosing loop immediately. As a bare statement it sits on its own line; as the `(break)` call form it can be conditionally fired from inside an `(if cond then else)` — use the **3-argument** `if` so both branches are lazy-wrapped (a 2-arg `if` won't resolve):

```flow
use "@std"

Int seen = 0
for Int n in (range 1 100) {
    (if (gt n 5) (break) (print (str n)))   Note: stop once n exceeds 5
    seen = (add seen 1)
}
(print (concat "saw " (str seen)))          Note: 5
```

In nested loops, `(break)` only exits the innermost loop. Called outside any loop it is a charitable no-op with a one-shot `[break]` advisory — it never crashes the script.

## `continue`

`continue` skips the rest of the current iteration and proceeds to the next. Like `break`, it has a bare-statement form and a `(continue)` call form for conditional use:

```flow
use "@std"

Note: keep only the last three iterations
Int kept = 0
for Int n in (range 1 11) {
    (if (lt n 8) (continue) (print "tail"))
    kept = (add kept 1)
}
(print (concat "kept " (str kept)))         Note: 3 (n = 8, 9, 10)

Note: bare-statement form — anything after `continue` is skipped
Int ci = 0
while (lt ci 3) {
    ci = (add ci 1)
    continue
    (print "never runs")
}
```

## Iteration Safety Limit

Loops have a configurable maximum iteration count (default 10000) to prevent runaway execution. Adjust with:

```flow
use "@std"

(setMaxIterations 1000000)
```

When the limit is exceeded, the interpreter reports `Iteration limit of N exceeded in for loop` (or `while loop`) and halts that loop. Long-running batch loops may need to raise the cap.

## Loops and Music

Loops combine naturally with note-building patterns. For example, to build sequences programmatically:

```flow
use "@std"

Int[] intervals = (list 0 2 4 5 7 9 11 12)
for Int semitones in intervals {
    Sequence step = | C4 | -> transpose semitones
    (print (str step))
}
```

For rhythmic variations, use seeded randomness inside loops:

```flow
use "@std"

(??set 42)
for Int i in (range 0 4) {
    Sequence bar = | (?? C4 E4 G4) (?? C4 E4 G4) |
    (print (str bar))
}
```

## Comparison: Recursion vs Loops vs Combinators

Flow supports recursion too, but loops are usually clearer for imperative counting, and collection combinators (`map` / `filter` / `reduce`) are often more idiomatic for transformations:

```flow
use "@std"

Note: imperative loop
Int sum = 0
for Int n in (range 1 11) {
    sum = (add sum n)
}
(print (str sum))    Note: 55

Note: functional reduce
Int sumF = (reduce (range 1 11) 0 (fn Int acc, Int n => (add acc n)))
(print (str sumF))   Note: 55
```

Pick whichever reads more naturally. Loops shine for stateful iteration or early exit; combinators win for pure transformations.

## See Also

- [Language Basics](Language-Basics.md) - Variables, prefix arithmetic, pattern matching, scoping
- [Collections](Collections.md) - `map`, `filter`, `reduce` alternatives, and `Dict<K, V>`
- [Generative Music](Generative.md) - Using loops alongside Markov / L-systems / Tidal combinators
