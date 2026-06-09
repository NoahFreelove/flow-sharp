# Quick Start

Get up and running with Flow in minutes.

## Install (recommended)

Flow ships as a prebuilt self-contained binary — no .NET SDK needed on the install side.

```bash
# Per-user install — no sudo required
curl -fsSL https://raw.githubusercontent.com/NoahFreelove/flow-sharp/main/scripts/install.sh | bash
```

This drops the runtime at `~/.local/share/flow/` and symlinks `flow` into `~/.local/bin/`. Add `~/.local/bin` to `$PATH` if it isn't already.

For a system-wide install (writes to `/usr/local/`, may require `sudo`):

```bash
./scripts/install.sh --system
```

`scripts/install.sh -h` lists every flag.

## Build from source (optional)

If you want to hack on the interpreter itself, you'll need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/NoahFreelove/flow-sharp
cd flow-sharp
dotnet build
```

## Run a Script

Create a file called `hello.flow`:

```flow
use "@std"

(print "Hello, Flow!")

Int x = 5
Int y = 10
Int sum = (add x y)
(print $"Sum: {sum}")
```

Run it:

```bash
flow run hello.flow
# or, from a source build:
dotnet run --project flow-interpreter hello.flow
```

## Start the REPL

```bash
flow repl
```

The REPL auto-imports `@std`, `@audio`, and `@collections`. Type Flow expressions interactively and see results immediately. Use `:help`, `:clear`, `:stop`, or `:quit` for built-in commands. In a real terminal it supports Tab completion (LSP-backed), Ctrl+R reverse history search, and PrettyPrompt editing; persistent history is stored at `~/.config/flow/history`. Piped or redirected stdin falls back to the legacy line reader (CI-safe).

## Watch Mode

Automatically re-render a script when the file changes — reloads quantize to the next bar boundary with a 64-sample crossfade so playback never glitches. The terminal shows a four-row live status panel with tempo/bar, active blocks, voice usage, and sticky advisories.

```bash
flow watch path/to/script.flow
```

See [Live Coding](Live-Coding.md) for `live { }` blocks, the `flow watch` status panel, and the determinism trade-off.

## Evaluate an Expression

```bash
flow eval 'Int x = 5; (print (str x))'
```

## Your First Melody

Create `melody.flow`:

```flow
use "@std"
use "@audio"

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            section intro {
                Sequence melody = | C4 E4 G4 C5 |
            }

            Song song = [intro]
            Buffer buf = (renderSong song "piano")
            (exportWav buf "melody.wav")
            (print "Exported melody.wav!")
        }
    }
}
```

```bash
flow run melody.flow
```

This renders a simple C major arpeggio using the (sample-based) piano and exports it as a WAV file.

## A Taste of Newer Features

Tuples, dicts, symbols, named args, and pattern matching all land in the core language:

```flow
use "@std"

Note: tuple + destructuring
<<Int x, Int y>> = <<3, 4>>
(print $"x={x} y={y}")

Note: dict + named-arg friendly builtins
Dict<Symbol, Int> bpms = (dict #verse 120 #chorus 140)
Int v = (getOr bpms #bridge 100)

Note: pattern matching with music-aware extractors
String label = (match Cmaj7
                | Cmaj7 => "tonic"
                | Dm    => "ii"
                | _     => "other")
(print label)
```

## Standalone Flow Editor (Optional)

A desktop GUI for editing and running Flow scripts is included in the repo as `flow-editor`:

```bash
dotnet run --project flow-editor
```

This is optional — everything in the language is usable from the CLI.

## Important: The Standard Library

Most Flow programs need to start with:

```flow
use "@std"
```

This imports the core standard library, which provides essentials like `print`, `str`, `concat`, `add`, `sub`, `mul`, `div`, `list`, `dict`, `map`, `filter`, and many more. `@std` transitively pulls in `@collections` and `@bars`.

For audio features, also import:

```flow
use "@audio"
```

Other opt-in modules: `@notation`, `@composition`, `@sfz`, `@patterns`, `@generative`, `@improv`, `@notation-io`, `@test`. See [Imports and Modules](Imports-and-Modules.md) for the full list and what each one unlocks.

## Running Tests

Flow now ships a pure-Flow test framework via `@test`:

```bash
# Run every test_*.flow file under tests/
flow test tests/

# Run a single test file
flow test tests/test_comprehensive.flow
```

A test file uses `(test "name" body)`, `(assert)`, `(assertEq)`, and `(assertWithinDb)`. Each test gets a hermetic snapshot/restore of mutable engine state so cases don't bleed into each other.

The legacy "run-the-script-and-check-exit-code" pattern still works:

```bash
# Run all legacy .flow test scripts
for test in tests/test_*.flow; do flow run "$test"; done
```

## See Also

- [Language Basics](Language-Basics.md) - Variables, tuples, dicts, pattern matching, comments
- [Note Streams](Note-Streams.md) - Write music inline
- [Loops](Loops.md) - `for`, `while`, `break`, `continue`
- [String Interpolation](String-Interpolation.md) - `$"..."` syntax
- [Imports and Modules](Imports-and-Modules.md) - All 15 stdlib modules
- [Examples](Examples.md) - Complete working programs
