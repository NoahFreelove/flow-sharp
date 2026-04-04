# Feature Landscape

**Domain:** v1.1 polish and foundations for a music production language
**Researched:** 2026-04-02
**Scope:** 8 specific features targeted for v1.1 milestone

## Table Stakes

Features users expect. Missing = product feels incomplete or broken.

| Feature | Why Expected | Complexity | Dependencies | Notes |
|---------|--------------|------------|--------------|-------|
| `//` line comments | Every language has comments. Flow currently has NONE -- users cannot annotate their code. This is the single most glaring omission. | Low | Lexer (`SimpleLexer.SkipWhitespaceAndComments`) | `SkipWhitespaceAndComments` already handles whitespace and line continuation. Add `// -> skip to EOL` check. ~5 lines of code. Must match `//` before single `/` reaches token dispatch, which is naturally handled since comment skipping runs first. |
| Math stdlib (sin, cos, abs, sqrt, min, max) | Any language doing audio/music needs basic math. Users writing custom oscillators or generative patterns need these. Zero math functions are registered despite `Math.*` used internally. | Low | `BuiltInFunctions.RegisterStdLib`, existing type system | Register thin wrappers around `System.Math`. Include: `sin`, `cos`, `tan`, `abs`, `sqrt`, `pow`, `log`, `exp`, `floor`, `ceil`, `round`, `min`, `max`, `pi`, `tau`. Overload `abs`/`min`/`max` for Int and Double. |
| `exportWav` -> `writeWav` rename | Codebase inconsistency: CLAUDE.md and .flow stdlib reference `writeWav`, but C# registration is `exportWav` (BuiltInFunctions.cs lines 387-397). Confuses every user. | Low | `BuiltInFunctions.cs`, test/script files using `exportWav` | Register both names pointing to same implementation. Print one-time deprecation warning for `exportWav`. Update all .flow files. |
| `mix()` for layering buffers | Audio layering is fundamental. `mixBuffers(a, b, 1.0, 1.0)` exists but the 4-arg signature is clunky for the common case. | Low | Existing `AudioCore.MixBuffers` | Add: `mix(Buffer, Buffer)` at equal gain (1.0, 1.0), and varargs-style `mix` taking an array of buffers. Keep `mixBuffers` as backward-compatible alias. |
| `--verbose` flag | Standard CLI debugging feature. When scripts produce unexpected output, users need visibility into overload resolution, module loading, and audio rendering decisions. | Low-Med | `Program.cs` (flag parsing), `FlowEngine`, `OverloadResolver` | Add `--verbose` / `-v` to `ParseFlags`. Thread verbosity through `FlowEngine`. Key areas: overload resolution candidates/scores, module loading paths, audio buffer sizes/sample rates. Output to stderr. |
| REPL auto-imports | REPL is unusable for quick experiments -- users must type `use "@std"` every time. Every mature REPL pre-loads the standard library. | Low | `Repl.cs`, existing module loader | Before REPL loop, execute `use "@std"` + `use "@audio"` + `use "@notation"`. Add `--bare` flag for clean sessions. Add `:reset` REPL command. |

## Differentiators

Features that set Flow apart. Not expected in generic languages, but valuable for music production.

| Feature | Value Proposition | Complexity | Dependencies | Notes |
|---------|-------------------|------------|--------------|-------|
| Per-section volume/gain in songs | Mix-level control within song arrangement: `Song s = [intro@0.8 verse chorus@1.2 outro@0.5]`. No text-based music language offers inline volume in arrangement syntax. | Medium | Parser (SongExpression), `SongRenderer`, `SectionReference` type | Best approach: syntax extension `section@volume` in `[...]` song expressions. `SectionReference` already has `Name` and `RepeatCount` -- add `Volume` (default 1.0). `RenderSection` returns a buffer; multiply by volume before `AppendBuffers`. |
| Tempo ramps (gradual BPM change) | Essential for expressive music. Abrupt tempo changes sound mechanical. `ritardando`/`accelerando` exist but only stretch note durations -- they do NOT change rendering BPM. | High | `MusicalContext`, `SongRenderer`, `NoteStreamCompiler` | Hardest feature on the list. Current rendering assumes constant BPM. True tempo ramps make beat-to-time mapping non-linear. See detailed notes below. |

## Anti-Features

Features to explicitly NOT build in this milestone.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Block comments (`/* ... */`) | Adds lexer complexity (nesting, unterminated detection) for minimal gain. Line comments cover 99% of use cases. | Ship `//` only. Revisit if requested. |
| Full logging framework (levels, files, config) | Overkill for an interpreter CLI. | `--verbose` prints to stderr. One boolean. No log levels. |
| Math as a module (`use "@math"`) | Math functions are fundamental like `print` and `str`. | Register directly in `RegisterStdLib`, always available. |
| Dynamic tempo via variable binding | `tempo myVar { ... }` with runtime-changing `myVar` would require lazy tempo evaluation across the entire pipeline. | Use `tempoRamp(start, end)` for gradual changes. Use per-section `tempo X { ... }` blocks for step changes. |
| Auto-normalization on mix | Silently changes levels, making gain staging impossible to reason about. | `mix()` sums at equal gain. Provide separate `normalize(buffer)`. |

## Feature Dependencies

```
// comments       -> (none, fully independent)
Math stdlib       -> (none, fully independent)
exportWav rename  -> (none, fully independent)
mix()             -> existing mixBuffers infrastructure
--verbose         -> (none, CLI-level change)
REPL auto-imports -> existing module loader + stdlib .flow files
Per-section volume -> Parser SongExpression + SongRenderer
Tempo ramps       -> MusicalContext + NoteStreamCompiler + SongRenderer (deep pipeline)
```

No circular dependencies. The first six features are fully independent -- can be implemented in any order or in parallel. Per-section volume requires a parser change. Tempo ramps touch the deepest layers.

## Implementation Complexity Tiers

### Tier 1: Trivial (< 30 min each, < 20 lines of code)
- **`//` line comments** -- add check in `SkipWhitespaceAndComments` (line 777 of SimpleLexer.cs)
- **`exportWav` -> `writeWav` rename** -- register alias in BuiltInFunctions.cs, update .flow files

### Tier 2: Simple (1-2 hours each, 20-80 lines of code)
- **Math stdlib** -- register ~15 functions as thin `Math.*` wrappers
- **`mix()` overloads** -- 2-3 new signatures wrapping existing `MixBuffers`
- **REPL auto-imports** -- execute `use` statements before REPL loop starts

### Tier 3: Moderate (2-4 hours, multiple files)
- **`--verbose` flag** -- flag parsing trivial, but threading verbosity through FlowEngine/Interpreter/OverloadResolver touches ~5 files
- **Per-section volume** -- parser extension for `@volume` in song expressions + gain application in SongRenderer

### Tier 4: Complex (1+ days, architectural impact)
- **Tempo ramps** -- non-linear beat-to-time mapping affects NoteStreamCompiler and SongRenderer core loops

## MVP Recommendation

**Phase 1 (quick wins -- all Tier 1+2):** Ship together. All table stakes, independent, low-risk, immediately improve usability.

1. `//` line comments
2. Math stdlib functions
3. `exportWav` -> `writeWav` rename (keep `exportWav` as deprecated alias)
4. `mix()` convenience overloads
5. REPL auto-imports

**Phase 2 (DX):** `--verbose` flag.

**Phase 3 (music features):** Per-section volume.

**Defer to later milestone:** Tempo ramps. Existing `ritardando`/`accelerando` cover the common case. True BPM ramps require deep pipeline changes best done in a dedicated "expressive rendering" milestone.

## Detailed Implementation Notes

### `//` Line Comments

Current: `SkipWhitespaceAndComments` (SimpleLexer.cs line 777) handles whitespace and line continuation only.

Add after the whitespace check, before `Note:` check:
```csharp
else if (c == '/' && PeekNext() == '/')
{
    while (!IsAtEnd() && Peek() != '\n') Advance();
}
```

Safe because: string scanning happens in `ScanString`/`ScanInterpolatedString` which do not call this method, so `//` inside strings is unaffected. Comments are consumed as whitespace before `NextToken` ever sees `/`.

### Math Stdlib Functions

| Function | Signature(s) | C# Mapping |
|----------|-------------|------------|
| `sin(Double) -> Double` | 1 | `Math.Sin` |
| `cos(Double) -> Double` | 1 | `Math.Cos` |
| `tan(Double) -> Double` | 1 | `Math.Tan` |
| `abs(Int) -> Int` | 2 | `Math.Abs(int)` |
| `abs(Double) -> Double` | | `Math.Abs(double)` |
| `sqrt(Double) -> Double` | 1 | `Math.Sqrt` |
| `pow(Double, Double) -> Double` | 1 | `Math.Pow` |
| `log(Double) -> Double` | 1 | `Math.Log` (natural) |
| `exp(Double) -> Double` | 1 | `Math.Exp` |
| `floor(Double) -> Int` | 1 | `(int)Math.Floor` |
| `ceil(Double) -> Int` | 1 | `(int)Math.Ceiling` |
| `round(Double) -> Int` | 1 | `(int)Math.Round` |
| `min(Int, Int) -> Int` | 2 | `Math.Min(int,int)` |
| `min(Double, Double) -> Double` | | `Math.Min(double,double)` |
| `max(Int, Int) -> Int` | 2 | `Math.Max(int,int)` |
| `max(Double, Double) -> Double` | | `Math.Max(double,double)` |
| `pi() -> Double` | 0-arg | `Math.PI` |
| `tau() -> Double` | 0-arg | `Math.Tau` |

### `exportWav` -> `writeWav`

Register both names pointing to `Audio.FileIO.ExportWav` and `Audio.FileIO.ExportWavWithBitDepth`. Update all .flow files that reference `exportWav`. The old name continues working.

### `mix()` Overloads

Add to `RegisterAudio`:
- `mix(Buffer, Buffer) -> Buffer` -- calls `MixBuffers` with gains 1.0, 1.0
- `mix(Buffer, Buffer, Double, Double) -> Buffer` -- alias for `mixBuffers`

Keep `mixBuffers` registered for backward compatibility.

### REPL Auto-Imports

In `Repl.cs`, after engine creation and before the input loop:
```csharp
_engine.Execute("use \"@std\"\nuse \"@audio\"\nuse \"@notation\"", "<repl-init>");
```

Add `--bare` CLI flag to skip. Add `:imports` command to list loaded modules.

### `--verbose` Flag

Add to `CliFlags` record. Pass to `FlowEngine` as a property. Key diagnostic points:
- **OverloadResolver**: candidates considered, specificity scores, winner (most valuable for debugging)
- **ModuleLoader**: file paths resolved and loaded
- **SongRenderer**: section durations, buffer sizes, instrument assignments

All output to stderr so it does not interfere with script stdout.

### Per-Section Volume

Extend `SectionReference` (wherever it is defined) with `double Volume = 1.0`. Parser recognizes `section@0.8` in song `[...]` expressions (similar to `section*N` for repeats). In `SongRenderer.RenderSong`, after `RenderSection` returns a buffer, scale all samples by `sectionRef.Volume`.

Syntax: `@` followed by a float literal. Can combine with repeats: `chorus*3@0.9`.

### Tempo Ramps

Current: `MusicalContext.Tempo` is a single double. All beat-to-sample conversion uses `samples = (beats / bpm) * 60 * sampleRate`.

For true tempo ramps, beat-to-time mapping becomes: `time(B) = integral_0^B (60 / bpm(b)) db`. This affects `NoteStreamCompiler` and `SongRenderer.MixVoicesToStereoBuffer`.

Practical v1.1 approach: Do NOT implement continuous ramps. Instead, support per-section tempo (which already works via `tempo X { ... }` inside sections) and document it as the recommended pattern. Defer continuous ramps to a later milestone where the rendering pipeline can be rearchitected with tempo-as-function.

If continuous ramps are demanded: add `tempoRamp(startBPM, endBPM)` as a new musical context block. Internally, discretize to small steps (e.g., per-beat tempo changes) rather than true integration. This approximation is perceptually indistinguishable and avoids rewriting the core rendering loop.

## Sources

- Codebase: `SimpleLexer.cs` line 777 (no comment handling), `BuiltInFunctions.cs` lines 387-397 (`exportWav` registration, no math functions), `AudioCore.cs` line 169 (existing `MixBuffers`), `SongRenderer.cs` (constant BPM, no per-section volume), `Program.cs` (current CLI flags, no `--verbose`), `Repl.cs` (no auto-imports)
- `TransformFunctions.cs` lines 531-537: `ritardando`/`accelerando` as note-duration transforms (not BPM ramps)
- `.planning/PROJECT.md`: v1.1 target feature list
