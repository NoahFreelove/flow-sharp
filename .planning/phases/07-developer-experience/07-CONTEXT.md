# Phase 7: Developer Experience - Context

**Gathered:** 2026-04-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Add `//` line comments to the lexer, math standard library functions (sin, cos, abs, sqrt, min, max, floor, ceil, pi, tau), rename `exportWav` to `writeWav` as primary with backwards-compatible alias, and auto-import standard modules in REPL mode.

</domain>

<decisions>
## Implementation Decisions

### Line Comments
- **D-01:** Add `//` line comment support in `SimpleLexer.SkipWhitespaceAndComments()`. When `/` is followed by another `/`, skip to end of line.
- **D-02:** Comments are treated like whitespace — completely invisible to the parser. No AST node needed.
- **D-03:** Only `//` single-line comments. No `/* */` block comments (keep it simple, functional style).

### Math Functions
- **D-04:** Register math functions as built-in functions in `BuiltInFunctions.cs` (new `RegisterMath` method). Not a loadable module — always available like `print`/`str`.
- **D-05:** Functions: `sin(Double) -> Double`, `cos(Double) -> Double`, `tan(Double) -> Double`, `abs(Double) -> Double`, `abs(Int) -> Int`, `sqrt(Double) -> Double`, `min(Double, Double) -> Double`, `min(Int, Int) -> Int`, `max(Double, Double) -> Double`, `max(Int, Int) -> Int`, `floor(Double) -> Int`, `ceil(Double) -> Int`, `round(Double) -> Int`.
- **D-06:** Constants: `pi` and `tau` as zero-argument functions — `(pi)` returns 3.14159... and `(tau)` returns 6.28318... Fits the S-expression style naturally.
- **D-07:** All math functions wrap `System.Math` / `System.MathF`. No new dependencies.
- **D-08:** Add `internal proc` declarations in a new `math.flow` stdlib file, auto-imported by `std.flow`.

### writeWav Rename
- **D-09:** Register `writeWav(String, Buffer)` as the primary name. Argument order is `(path, buffer)` to match `writeMidi(path, song)`.
- **D-10:** Keep `exportWav(Buffer, String)` as a backwards-compatible alias with the original argument order.
- **D-11:** Add `internal proc writeWav(String: filepath, Buffer: buffer)` to `audio.flow`.

### REPL Auto-Imports
- **D-12:** When running in REPL mode (no script file), auto-execute `use "@std"`, `use "@audio"`, `use "@collections"` before the first user input.
- **D-13:** Script mode (file argument) is unchanged — explicit imports required for reproducibility.
- **D-14:** Implementation: add auto-import logic in `FlowEngine` or `Program.cs` REPL loop, before first user input is processed.

### Claude's Discretion
- Whether `math.flow` is a new file or math declarations go into `std.flow` directly
- Exact placement of comment check in lexer (before or after token boundary check)
- Whether to add `pow(Double, Double) -> Double` and `log(Double) -> Double` alongside the others
- REPL auto-import mechanism (pre-execute source strings vs. direct module loading)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Lexer
- `flow-lang/Lexing/SimpleLexer.cs` — `SkipWhitespaceAndComments()` method and `/` token handling. Comment check goes here.

### Built-in Functions
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — Registration point for math functions. Add `RegisterMath` method.
- `flow-lang/StandardLibrary/Audio/FileIO.cs` — Existing `exportWav` implementation. `writeWav` wraps the same C# method.
- `flow-lang/audio.flow` — Add `internal proc writeWav` declaration here.
- `flow-lang/std.flow` — May import `@math` if separate file, or contain math declarations directly.

### REPL
- `flow-interpreter/Program.cs` — REPL loop. Auto-import logic goes here or in FlowEngine.
- `flow-lang/Core/FlowEngine.cs` — `Execute()` method. May need an `AutoImport()` helper.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SkipWhitespaceAndComments()` in SimpleLexer — already handles whitespace skipping; comment check is a natural extension
- `BuiltInFunctions.RegisterStdlib()` pattern — math functions follow the same registration pattern
- `FileIO.ExportWav` / `FileIO.ExportWavWithBitDepth` — the C# implementations that `writeWav` will call
- `ModuleLoader` — handles `use "@name"` imports; REPL auto-import uses the same mechanism

### Established Patterns
- Built-in functions: `FunctionSignature` + lambda in `Register*` methods
- `internal proc` declarations in `.flow` files map to C# implementations
- REPL runs `engine.Execute(line)` per input line

### Integration Points
- `SimpleLexer.cs`: Add `//` comment handling
- `BuiltInFunctions.cs`: Add `RegisterMath` method
- `audio.flow`: Add `writeWav` internal proc
- `Program.cs`: Add REPL auto-import before first user input

</code_context>

<specifics>
## Specific Ideas

- `//` comments are the single most requested missing feature — every test script uses `Note:` which is confusing
- Math functions enable building wavetables with trig: `(sin (* phase 6.283))` instead of external computation
- `(pi)` and `(tau)` as zero-arg functions feel natural in Flow's style
- REPL should feel like a playground — no setup needed to start experimenting

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 07-developer-experience*
*Context gathered: 2026-04-04*
