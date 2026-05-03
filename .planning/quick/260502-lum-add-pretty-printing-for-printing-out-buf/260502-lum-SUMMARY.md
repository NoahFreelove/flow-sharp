---
phase: 260502-lum-add-pretty-printing-for-printing-out-buf
plan: 01
subsystem: standard-library/audio
tags:
  - debug
  - audio
  - buffer
  - printer
  - hex-dump
  - waveform
requirements_complete:
  - PRETTY-BUFFER-01
  - HEX-DUMP-01
dependency-graph:
  requires:
    - flow-lang/StandardLibrary/Audio/AudioCore.cs (AudioBuffer)
    - flow-lang/StandardLibrary/InternalFunctionRegistry.cs
    - flow-lang/StandardLibrary/VisualizationFunctions.cs (pattern reference)
  provides:
    - prettyBuffer(Buffer) -> Void
    - bufferHex(Buffer) -> Void
    - bufferHex(Buffer, Int, Int) -> Void
  affects:
    - flow-lang/StandardLibrary/BuiltInFunctions.cs (one-line registration)
    - flow-lang/std.flow (three internal proc declarations)
tech-stack:
  added: []
  patterns:
    - "Mirror VisualizationFunctions.Register shape for new printer builtins"
    - "Charitable clamping over exceptions for out-of-range slice arguments"
key-files:
  created:
    - flow-lang/StandardLibrary/BufferPrinter.cs
    - tests/test_buffer_printing.flow
  modified:
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/std.flow
decisions:
  - "Hex dump uses lowercase hex (xxd-style) with 16 bytes/row, double-space mid-row separator, ASCII gutter, and a trailing end-offset line for easy length read-off."
  - "ASCII waveform sized 60x11 (smaller than visualize's 80x20) so prettyBuffer fits in a typical terminal alongside the header block."
  - "Charitable clamping (no exceptions) for bufferHex slice arguments — matches the project's silent-and-documented assumptions memory."
  - "dBFS floor at 1e-12 (~ -240 dBFS) avoids '-Infinity' for silent buffers; prints '-inf' instead."
  - "Internal proc declarations live in std.flow alongside the existing visualize(Buffer) precedent rather than audio.flow."
metrics:
  duration: ~10 minutes
  tasks_completed: 2
  files_created: 2
  files_modified: 2
  completed: 2026-05-02
commits:
  - 94620a7 feat(260502-lum): wire BufferPrinter builtins + end-to-end test
  - d9f939e feat(260502-lum): add BufferPrinter with prettyBuffer + bufferHex builtins
---

# Quick Task 260502-lum: Buffer Pretty-Printer + Hex Dump Summary

Added two new built-in printer functions for the `Buffer` type so composers
can debug buffers without having to round-trip through `writeWav` and an
external waveform viewer.

## What Was Built

| Function                              | Returns | Purpose                                                                                |
| ------------------------------------- | ------- | -------------------------------------------------------------------------------------- |
| `prettyBuffer(Buffer)`                | `Void`  | Multi-line header (frames, channels, sample rate, duration, peak, RMS in dBFS) + 60x11 ASCII waveform |
| `bufferHex(Buffer)`                   | `Void`  | Classic hex-editor dump of all float samples as little-endian IEEE-754 32-bit bytes    |
| `bufferHex(Buffer, Int, Int)`         | `Void`  | Slice variant — `offset` and `length` in bytes, silently clamped to buffer's byte range |

Both functions return `Void` (side-effect printers, like `print` and
`visualize`). Empty buffers print `(empty buffer)` and never throw.
Out-of-range slice arguments silently clamp (per the project's
charitable-interpretation memory). No change to `Value.ToString` or any
existing `str(...)` overload — purely additive surface area.

## Sample Output

### prettyBuffer

For a 64-frame mono buffer at 48 kHz with first six samples set to
`0.0, 0.25, 0.5, 0.75, 1.0, -0.5`:

```
Buffer:
  frames      : 64
  channels    : 1 (mono)
  sample rate : 48000 Hz
  duration    : 0.001 s
  peak        : 1.0000 (0.00 dBFS)
  rms         : 0.1822  (-14.79 dBFS)
|    *                                                       |
|   *                                                        |
|  *                                                         |
| *                                                          |
|                                                            |
|*-----******************************************************|
|                                                            |
|     *                                                      |
|                                                            |
|                                                            |
|                                                            |
```

### bufferHex (full)

```
00000000  00 00 00 00 00 00 80 3e  00 00 00 3f 00 00 40 3f  |.......>...?..@?|
00000010  00 00 80 3f 00 00 00 bf  00 00 00 00 00 00 00 00  |...?............|
...
000000f0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  |................|
00000100
```

The first row shows the IEEE-754 little-endian encodings of `0.0`, `0.25`,
`0.5`, `0.75` — exactly matching the eyeball check in the plan.

### bufferHex (offset 16, length 32)

```
00000010  00 00 80 3f 00 00 00 bf  00 00 00 00 00 00 00 00  |...?............|
00000020  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  |................|
00000030
```

### bufferHex (offset 9999, length 16) — silent empty slice

```
(empty slice)
```

### bufferHex (offset -8, length 16) — silent clamp to 0

```
00000000  00 00 00 00 00 00 80 3e  00 00 00 3f 00 00 40 3f  |.......>...?..@?|
00000010
```

## Architecture Notes

`BufferPrinter.cs` mirrors the shape of `VisualizationFunctions.cs`:

- A static `Register(InternalFunctionRegistry registry)` entry point that
  registers each overload by `FunctionSignature`.
- One static method per overload (`PrettyBuffer`, `BufferHex`,
  `BufferHexSlice`) matching `Func<IReadOnlyList<Value>, Value>`.
- Direct use of `args[0].As<AudioBuffer>()` — no reflection, no boxing
  beyond what `Value` already does.

The hex encoder uses `MemoryMarshal.AsBytes(buf.Data.AsSpan())` to view
the float buffer as a byte span without allocation, then `.ToArray()` for
the dump body. A `Debug.Assert(BitConverter.IsLittleEndian)` documents the
assumption that every platform Flow targets is little-endian, so no
byte-swapping is needed.

The arity-1 vs arity-3 overload of `bufferHex` is disambiguated by argument
count alone — no `Void`-wildcard tricks required, because the existing
`InternalFunctionRegistry.SignaturesMatch` checks exact arity for
non-varargs signatures.

## Deviations from Plan

### 1. [Rule 3 — Blocking] Added internal proc declarations to std.flow

- **Found during:** Task 2, first test run
- **Issue:** `prettyBuffer` and `bufferHex` were registered with the
  `InternalFunctionRegistry`, but the interpreter still reported
  `Function 'prettyBuffer' not found`. The plan's `<interfaces>` section
  did not mention that internal builtins also require a corresponding
  `internal proc` declaration in `flow-lang/std.flow` (or another loaded
  `.flow` module) for `Interpreter.ExecuteProcDeclaration` to bind the
  signature to the registry implementation at parse time.
- **Fix:** Added three new lines to `flow-lang/std.flow` next to the
  existing `internal proc visualize (Buffer: b)` declaration:
  - `internal proc prettyBuffer (Buffer: b)`
  - `internal proc bufferHex (Buffer: b)`
  - `internal proc bufferHex (Buffer: b, Int: offset, Int: length)`
- **Files modified:** `flow-lang/std.flow`
- **Commit:** 94620a7

### 2. [Lexer adjustment] Renamed test variable `buf` -> `ramp`

- **Found during:** Task 2, first test run
- **Issue:** `Buffer buf = ...` failed to parse with
  `Expected variable name. Got Buf 'buf'`. The lexer reserves `buf` (and
  variants) as a `Buf` token, presumably for a Buffer-related literal.
- **Fix:** Renamed the test's primary buffer variable from `buf` to
  `ramp`, which has the bonus benefit of describing the data more
  accurately.
- **Files modified:** `tests/test_buffer_printing.flow` (only ever existed
  with the renamed variable; never committed under the old name).

### 3. [Test policy] Force-added test file (`git add -f`)

- **Found during:** Task 2 commit
- **Issue:** `tests/` is globally gitignored, so `git add tests/...`
  silently ignores the file. All 74 existing tracked tests must have been
  added with `-f`.
- **Fix:** Used `git add -f tests/test_buffer_printing.flow`, matching the
  project's existing convention for tracked test scripts.
- **Files modified:** None (gitignore left as-is).

## Verification

| Check                                              | Result |
| -------------------------------------------------- | ------ |
| `dotnet build` (whole solution)                    | 0 errors |
| `dotnet run --project flow-interpreter tests/test_buffer_printing.flow` | exit 0 |
| `dotnet run --project flow-interpreter tests/test_math.flow`            | exit 0 (regression check) |
| `dotnet run --project flow-interpreter tests/test_fade.flow`            | exit 0 (regression check) |
| `dotnet run --project flow-interpreter tests/test_chords.flow`          | exit 0 (regression check) |
| `dotnet run --project flow-interpreter tests/test_comments.flow`        | exit 0 (regression check) |
| Hex eyeball check (`00 00 00 00 / 00 00 80 3e / 00 00 00 3f / 00 00 40 3f`) | matches IEEE-754 LE for 0.0, 0.25, 0.5, 0.75 |
| Empty buffer handling (both functions print `(empty buffer)`)           | pass |
| Out-of-range slice (offset 9999) prints `(empty slice)`                 | pass |
| Negative offset silently clamps to 0                                    | pass |

## Commits

| Hash    | Message                                                                       |
| ------- | ----------------------------------------------------------------------------- |
| d9f939e | `feat(260502-lum): add BufferPrinter with prettyBuffer + bufferHex builtins` |
| 94620a7 | `feat(260502-lum): wire BufferPrinter builtins + end-to-end test`            |

## Self-Check: PASSED

- `flow-lang/StandardLibrary/BufferPrinter.cs` exists (FOUND)
- `tests/test_buffer_printing.flow` exists (FOUND)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` modified (FOUND `BufferPrinter.Register`)
- `flow-lang/std.flow` modified (FOUND three new `internal proc` lines)
- Commit d9f939e in `git log` (FOUND)
- Commit 94620a7 in `git log` (FOUND)
