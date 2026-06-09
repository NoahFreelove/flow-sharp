# Imports and Modules

Flow uses `use` statements to import modules, making their functions and variables available in the current scope.

## Basic Import Syntax

```flow
use "@std"         Note: import a standard-library module (prefix @)
use "mylib.flow"   Note: import a local file (relative path)
```

## Standard Library Modules

Prefix module names with `@` to import from the standard library directory. Flow ships 15 first-party modules:

| Import | Purpose |
|--------|---------|
| `use "@std"` | Core builtins (transitively pulls in `@collections` + `@bars`) |
| `use "@collections"` | List operations: `head`, `tail`, `map`, `filter`, `reduce`, ... |
| `use "@bars"` | Simple bar/note operations |
| `use "@notation"` | Musical-notation primitives (durations, rests, bar/sequence builders) |
| `use "@audio"` | Buffer creation, signal generation, effects, playback, WAV/MIDI export |
| `use "@composition"` | Timeline / voice / track helpers |
| `use "@sfz"` | SFZ orchestral sampler (`loadSfz`, GM symbol dict, `"sampler:NAME"` dispatch) — Desktop only |
| `use "@patterns"` | 13 Tidal-style combinators on `Sequence` (`every`, `fast`, `jux`, `degrade`, ...) |
| `use "@generative"` | Markov chains, L-systems, cellular automata, chaos maps, `quantizeToScale` |
| `use "@improv"` | `jam` chord-aware Markov improvisation + composer-editable style packs |
| `use "@notation-io"` | MusicXML 3.1 + LilyPond 2.24+ export, ABC 2.1 + PC-98 MML import |
| `use "@osc"` | Open Sound Control send/receive (`oscSend`/`oscListen`/`oscPump`/...) — Desktop only |
| `use "@midi"` | Real-time MIDI output + clock (`midiOut`/`midiNoteOn`/`clockMaster`/...) — Desktop only |
| `use "@jack"` | JACK transport sync (`jackSync`) — Desktop only, Linux |
| `use "@test"` | Pure-Flow test framework (`test`, `assert`, `assertEq`, `assertWithinDb`, ...) |

### What `@std` Includes

`@std` is the recommended default import. It transitively imports `@collections` and `@bars`, providing:

- I/O: `print`, `input`
- String: `str`, `len`, `concat`
- Arithmetic (prefix-only): `add`, `sub`, `mul`, `div`, `idiv`, `neg`
- Comparison: `equals`, `sequals`, `lt`, `gt`, `lte`, `gte`
- Logic: `and`, `or`, `not`, `if`
- Math: `sin`, `cos`, `tan`, `sqrt`, `floor`, `ceil`, `round`, `log`, `pow`, `abs`, `min`, `max`, `pi`, `tau`
- Type conversion: `intToDouble`, `doubleToInt`, `stringToInt`, `stringToDouble`
- Collections: `list`, `head`, `tail`, `map`, `filter`, `reduce`, `each`, `range`, `zip`, `slice`, ...
- Dict: `dict`, `dictTuple`, `get`, `getOr`, `set`, `remove`, `has`, `keys`, `values`, `size`, `merge`
- Tuples: `unpack`
- Random: `?`, `??`, `??set`, `??reset`
- Loop control: `setMaxIterations`

### When to Import Additional Modules

| Need | Import |
|------|--------|
| Audio buffers, synthesis, effects, playback, WAV/MIDI | `use "@audio"` |
| Musical notation builders (note durations, rests, time signatures) | `use "@notation"` |
| Timeline / voice / track helpers | `use "@composition"` |
| SFZ orchestral sampler (VSCO Community Edition, custom SFZ patches) | `use "@sfz"` |
| Tidal-style sequence combinators | `use "@patterns"` |
| Markov / L-systems / cellular automata / chaos maps | `use "@generative"` |
| Chord-aware Markov improvisation | `use "@improv"` |
| MusicXML / LilyPond export, ABC / MML import | `use "@notation-io"` |
| OSC send/receive (with external apps / SuperCollider / etc.) | `use "@osc"` (Desktop only) |
| Real-time MIDI output to hardware synths / DAW; MIDI clock | `use "@midi"` (Desktop only) |
| JACK transport position + tempo sync | `use "@jack"` (Desktop only, Linux) |
| Pure-Flow test framework | `use "@test"` |

### Runtime Gates

Several modules flip runtime feature flags when imported, not just by declaring procs:

- `@sfz` flips `ExecutionContext.SfzEnabled = true` and populates the 20-entry GM symbol dict. Without `use "@sfz"`, calls to `loadSfz` fail with `loadSfz requires \`use "@sfz"\``.
- `@notation-io` flips `ExecutionContext.NotationIoEnabled = true`. Without `use "@notation-io"`, calls to `writeMusicXML` / `writeLilyPond` / `abc` / `mml` fail with a similar guard.
- `@osc` flips `ExecutionContext.OscEnabled = true`. OSC builtins require this; a `__enableOscModule` marker in the module file sets it.
- `@midi` flips `ExecutionContext.MidiEnabled = true` similarly.

The marker line that flips the flag lives at the bottom of each module file — composers don't call it directly.

## Per-Module Pragma Isolation

Pragmas (`enable hAsB;`, `enable justIntonation;`, `enable matchExhaustive;`, etc.) are **file-scoped**. Each imported file gets its own `PragmaSet` — pragmas don't leak across `use` boundaries. A `match` expression compiled inside `lib.flow` carries `lib.flow`'s pragma set, not the importer's.

## Local File Imports

Import files using relative paths:

```flow
use "mylib.flow"
use "utils/helpers.flow"
```

### Library File Example

Create `helpers.flow`:

```flow
use "@std"

proc helper (Int: x)
    (add (mul x 2) 1)
end proc

Int sharedConstant = 42
```

Use it from another file:

```flow
use "@std"
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

Flow detects and prevents circular imports. If `a.flow` imports `b.flow` and `b.flow` imports `a.flow`, the import is short-circuited (idempotent) — re-importing an already-loaded module is a silent no-op, not an error.

## Module Resolution

- `@` prefix resolves to the standard library directory (where the `.flow` stdlib files live alongside the `flow-lang` project; configurable via `stdlib_search_path` in `~/.config/flow/config.toml`)
- Paths without `@` are resolved relative to the importing file's directory

## Common Patterns

### Typical Script Header

```flow
use "@std"
use "@audio"
```

### Composition Header

```flow
use "@std"
use "@audio"
use "@notation"
use "@composition"
```

### Generative Header

```flow
use "@std"
use "@audio"
use "@patterns"
use "@generative"
use "@improv"
```

### Notation IO Header

```flow
use "@std"
use "@audio"
use "@notation-io"
```

### Internal Proc Declarations

Standard library `.flow` files use `internal proc` to declare the signatures of C# built-in functions:

```flow
Note: from collections.flow
internal proc head (Voids: arr)
internal proc tail (Voids: arr)
internal proc map (Voids: arr, Function: f)
```

These declarations make the C# built-in functions visible to the Flow type checker. You don't need to use `internal proc` in your own code — it's the bridging mechanism between C# implementations and Flow.

## See Also

- [Standard Library](Standard-Library.md) - Complete function reference
- [Quick Start](Quick-Start.md) - Getting started with imports
- [Tips and Tricks](Tips-and-Tricks.md) - Avoiding common import pitfalls
- [Language Basics](Language-Basics.md#pragmas) - File-scoped pragmas
