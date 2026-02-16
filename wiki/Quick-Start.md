# Quick Start

Get up and running with Flow in minutes.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Build

```bash
git clone <repo-url>
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
Int sum = x + y
(print (concat "Sum: " (str sum)))
```

Run it:

```bash
dotnet run --project flow-interpreter hello.flow
```

## Start the REPL

```bash
dotnet run --project flow-interpreter
```

The REPL lets you type Flow expressions interactively and see results immediately.

## Watch Mode

Automatically re-run a script when the file changes:

```bash
dotnet run --project flow-interpreter -- --watch path/to/script.flow
```

## Evaluate an Expression

```bash
dotnet run --project flow-interpreter -e 'Int x = 5; (print (str x))'
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
dotnet run --project flow-interpreter melody.flow
```

This renders a simple C major arpeggio using the piano synthesizer and exports it as a WAV file.

## Important: The Standard Library

Most Flow programs need to start with:

```flow
use "@std"
```

This imports the standard library, which provides essential functions like `print`, `str`, `concat`, `add`, `sub`, `mul`, `div`, `list`, `map`, `filter`, and many more. Without it, you only have raw language syntax.

For audio features, also import:

```flow
use "@audio"
```

See [Standard Library](Standard-Library.md) for the full list of modules.

## Running Tests

Flow's test suite is a collection of `.flow` scripts in the `tests/` directory:

```bash
# Run a single test
dotnet run --project flow-interpreter tests/test_comprehensive.flow

# Run all tests
for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done
```

## See Also

- [Language Basics](Language-Basics.md) - Learn the fundamentals
- [Note Streams](Note-Streams.md) - Write music inline
- [Examples](Examples.md) - Complete working programs
