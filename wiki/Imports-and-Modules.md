# Imports and Modules

Flow uses `use` statements to import modules, making their functions and variables available in the current scope.

## Basic Import Syntax

```flow
use "@std"         Note: import standard library module
use "mylib.flow"   Note: import local file
```

## Standard Library Modules

Prefix module names with `@` to import from the standard library directory:

```flow
use "@std"           Note: core + collections + bars
use "@collections"   Note: list operations
use "@audio"         Note: audio/synthesis/effects/playback
use "@notation"      Note: musical notation helpers
use "@bars"          Note: bar operations
use "@composition"   Note: timeline/voice/track helpers
```

### What @std Includes

`@std` is the recommended default import. It automatically imports `@collections` and `@bars`, providing:

- I/O: `print`, `input`
- String: `str`, `len`, `concat`
- Arithmetic: `add`, `sub`, `mul`, `div`
- Comparison: `equals`, `sequals`, `lt`, `gt`, `lte`, `gte`
- Logic: `and`, `or`, `not`, `if`
- Type conversion: `intToDouble`, `doubleToInt`, `stringToInt`, `stringToDouble`
- Collections: `list`, `head`, `tail`, `map`, `filter`, `reduce`, `each`, etc.
- Random: `?`, `??`, `??set`, `??reset`

### When to Import Additional Modules

| Need | Import |
|------|--------|
| Audio buffers, synthesis, effects, playback | `use "@audio"` |
| Musical notation helpers | `use "@notation"` |
| Timeline/voice/track functions | `use "@composition"` |

## Local File Imports

Import files using relative paths:

```flow
use "mylib.flow"
use "utils/helpers.flow"
```

### Library File Example

Create `helpers.flow`:

```flow
proc helper (Int: x)
    x * 2 + 1
end proc

Int sharedConstant = 42
```

Use it from another file:

```flow
use "helpers.flow"

Int result = (helper 5)
(print (str result))         Note: 11
(print (str sharedConstant)) Note: 42
```

## Execution in Caller's Scope

Imported modules execute in the caller's scope — there is no namespace isolation. All functions and variables from the imported file become directly available:

```flow
use "mylib.flow"
Note: everything defined in mylib.flow is now accessible
Note: no prefix needed (no mylib.functionName, just functionName)
```

This means:
- Functions from different imports can shadow each other
- Variables are shared between the importer and imported module
- Be careful with name conflicts across modules

## Circular Import Detection

Flow detects and prevents circular imports. If `a.flow` imports `b.flow` and `b.flow` imports `a.flow`, an error is raised.

## Module Resolution

- `@` prefix resolves to the standard library directory (where the `.flow` stdlib files live alongside the `flow-lang` project)
- Paths without `@` are resolved relative to the importing file's directory

## Common Patterns

### Typical Script Header

```flow
use "@std"
use "@audio"
```

### Musical Composition Header

```flow
use "@std"
use "@audio"
use "@notation"
use "@composition"
```

### Internal Proc Declarations

Standard library `.flow` files use `internal proc` to declare the signatures of C# built-in functions:

```flow
Note: from collections.flow
internal proc head (Voids: arr)
internal proc tail (Voids: arr)
internal proc map (Voids: arr, Function: f)
```

These declarations make the C# built-in functions visible to the Flow type checker. You don't need to use `internal proc` in your own code — it's a mechanism for bridging C# implementations into Flow.

## See Also

- [Standard Library](Standard-Library.md) - Complete function reference
- [Quick Start](Quick-Start.md) - Getting started with imports
- [Tips and Tricks](Tips-and-Tricks.md) - Avoiding common import pitfalls
