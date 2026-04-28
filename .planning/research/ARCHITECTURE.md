# Architecture Research — Flow v1.3 Integration

**Domain:** Music DSL interpreter — adding tuplets, arbitrary fractional durations, DEFER closures, Tier B/C composer DX
**Researched:** 2026-04-26
**Confidence:** HIGH (codebase fully read; canonical-music-engine reference verified via music21 docs)

---

## 1. Existing Pipeline (Recap, Annotated for v1.3 Touch Points)

```
.flow source
   │
   ▼
SimpleLexer ────────────────────────► Token[]   ← (1) duration suffix tokenization
   │                                              ← (2) "/12" arbitrary-duration lexeme
   │                                              ← (3) `enable` keyword (pragma)
   │                                              ← (4) `H` letter (becomes BLetter under pragma)
   ▼
Parser  ────────────────────────────► AST       ← (5) ParseNoteStream → TupletElement
   │  (Parser.NoteStream.cs)                    ← (6) ParsePragmaStatement
   │                                            ← (7) chord inversion suffix /3, /5
   ▼
Interpreter / ExecutionContext                  ← (8) PragmaTable on ExecutionContext
   │                                            ← (9) MusicalContext.Tuning (new field)
   ▼
NoteStreamCompiler  ─────────────────► SequenceData
   │                                            ← (10) tuplet duration scaling (rational)
   │                                            ← (11) arbitrary fractional durations
   ▼
SongRenderer / SequenceRenderer                 ← (12) microtonal Tuning lookup site
   │
   ▼
Synthesizers  → AudioBuffer  → WAV / Playback / MIDI
   ↑                                            ← (13) PitchConversion.NoteToFrequency
                                                       (microtonal pluggability lives here)

flow-lsp  (parser-only consumer of flow-lang)   ← (14) scale linting walks AST + key-context
```

---

## 2. Question 1 — AST Strategy for Tuplets

### Recommendation: `TupletElement` is a NEW `NoteStreamElement` record, recursive in its children

```csharp
// Add to flow-lang/Ast/Expressions/NoteStreamExpression.cs
public record TupletElement(
    SourceLocation Location,
    int Numerator,                           // "3" in 3:2
    int Denominator,                         // "2" in 3:2
    IReadOnlyList<NoteStreamElement> Children, // recursive — children may themselves be TupletElements
    string? DurationSuffix,                  // outer suffix: (3:2 ...)q  applies q to the WHOLE tuplet group
    bool IsDotted
) : NoteStreamElement(Location);
```

**Why recursive (not flat):**
- Nested tuplets compose naturally — `(3:2 (5:4 C4 D4 E4 F4 G4) D4 E4)q` becomes one outer ratio applied to a child that has its own ratio. Compiler handles by multiplying scaling factors down the tree. This matches music21's `Tuplet` model where each note carries an ordered list of active Tuplets.
- Children are heterogeneous — a tuplet may contain a `NoteElement`, a `RestElement`, a `ChordElement`, even another `TupletElement`. Reusing `NoteStreamElement` as the child type means zero new code for "what's allowed inside a tuplet" — the same parser branches dispatch.
- Source-location and source-length tracking already required for editor-highlighting; the recursive model lets us highlight either the whole tuplet group or any single child.

**Why NOT a flat encoding (e.g. tuplet-start/tuplet-end pseudo-elements):**
- The current parser is recursive descent; emitting paired marker tokens would force every consumer (`NoteStreamCompiler`, `SequenceRenderer`, the LSP semantic-token encoder) to maintain a parallel "are we inside a tuplet?" stack. Recursive AST localizes that state inside `CompileTupletElement`.

### NoteStreamCompiler scaling under nested tuplets

Add a single new method that takes a *scaling factor* the same way `autoFitDuration` is currently threaded:

```csharp
// flow-lang/Runtime/NoteStreamCompiler.cs
private void CompileTupletElement(
    TupletElement tuplet,
    NoteValueType.Value? autoFitDuration,
    MusicalContext context,
    ExecutionContext? execCtx,
    List<MusicalNoteData> output,
    Fraction outerScale)   // NEW — accumulated tuplet scaling from ancestor TupletElements
{
    // 3:2 means "play 3 notes in the time of 2"  →  each child duration ×  2/3
    var scale = outerScale * new Fraction(tuplet.Denominator, tuplet.Numerator);

    // The TupletElement's own DurationSuffix is the GROUP duration — i.e. (3:2 ...)q
    // means "the whole group occupies one quarter note". Resolve outer first:
    var groupDuration = ResolveDuration(tuplet.DurationSuffix, autoFitDuration);

    // groupDuration is the wall-clock duration of all N children combined,
    // distributed equally before scaling. Each child gets groupDuration / N as
    // its base, then the tuplet scaling redistributes so that N children fit
    // into M-notes' worth of time.

    foreach (var child in tuplet.Children) {
        switch (child) {
            case TupletElement nested:
                CompileTupletElement(nested, autoFitDuration, context, execCtx, output, scale);
                break;
            case NoteElement n:
                var note = CompileNoteElement(n, autoFitDuration, context);
                output.Add(note.WithScaledDuration(scale));   // see §3 for WithScaledDuration
                break;
            // ... rest same as outer switch
        }
    }
}
```

**Single source of truth:** the outer `CompileBar` switch dispatches to `CompileTupletElement(...)` with `outerScale = Fraction.One`. Recursion handles nesting with no special cases.

---

## 3. Question 2 — Duration Representation

### Recommendation: Option (b) — extend `MusicalNoteData` with a `Fraction DurationFraction` field. Keep `NoteValue` enum for backward compatibility.

This is the canonical approach used by [music21](https://music21.org/music21docs/usersGuide/usersGuide_19_duration2.html), the most actively developed open-source music engine. From their docs:

> "music21 Durations are almost always measured in Quarter Notes... When a note's quarterLength is set to 0.8, it's represented as Fraction(4, 5), with the full name 'Quarter Quintuplet (4/5 QL)'."

### Why option (a) — extending the enum — is wrong:

- `TUPLET_THIRD`, `TUPLET_FIFTH`, etc. don't compose. A 5:4 of a 7:6 of a quarter has no enum slot. You'd need a combinatorial explosion.
- Dotted tuplets (a dotted triplet eighth) would force a Cartesian product of dot-bits × tuplet-types.
- Lilypond and music21 both rejected this approach for the same reason.

### Why option (c) — `double DurationScale` — is wrong:

- `Math.Pow(2, -1.0/3)` is irrational on the binary side and rational on the musical side. Two `(3:2 ...)` triplets concatenated must sum to *exactly* one half note. Floating point will accumulate ε that breaks `BarData.ValidateDuration()` by ~1e-15 every bar — silent under noise floor for one bar, catastrophic for a 1000-bar piece because the deterministic-WAV byte-pin tests in v1.2 will diverge.
- MIDI export through DryWetMidi uses integer ticks-per-quarter (`PPQN`, default 480). Tuplets need exact integer division — 480 / 3 = 160, clean. With doubles you'd accumulate drift before tick conversion.

### Concrete shape:

```csharp
// flow-lang/TypeSystem/SpecialTypes/NoteType.cs (MusicalNoteData)
public class MusicalNoteData {
    public char NoteName { get; }
    public int Octave { get; }
    public int Alteration { get; }

    // Existing — keep for backward compatibility
    public int? DurationValue { get; }   // NoteValue enum int
    public bool IsDotted { get; }

    // NEW — when set, this OVERRIDES DurationValue + IsDotted for actual playback duration
    // For non-tuplet, non-arbitrary-fraction notes, this stays null and the enum path runs.
    public Fraction? DurationFraction { get; }   // in quarter-note units (matches music21)

    // Existing
    public bool IsRest { get; }
    public double? CentOffset { get; }
    // ...

    public double GetBeats(int timeSigDenominator) {
        if (DurationFraction.HasValue) {
            // DurationFraction is in quarter-note units. Convert to beats:
            // beats = quarter-notes × (timeSigDenominator / 4)
            return (double)DurationFraction.Value * timeSigDenominator / 4.0;
        }
        // existing enum path
        if (!DurationValue.HasValue) return 1.0;
        double fraction = NoteValueType.ToFraction((NoteValueType.Value)DurationValue.Value);
        if (IsDotted) fraction *= 1.5;
        return fraction * timeSigDenominator;
    }
}
```

### What `Fraction` to use:

.NET 10 has no built-in `Fraction`. Three options:

1. **Hand-roll a 50-line `readonly record struct Fraction`** in `flow-lang/TypeSystem/Fraction.cs`. With `int Numerator, int Denominator`, normalized to lowest terms via `Gcd` in the constructor. Operations: `+ - * /`, implicit cast to/from int, explicit cast to `double`. This is the minimal-dependency choice — matches the project's "Guiding Principle: Minimal Dependencies" (STACK.md).

2. **`System.Numerics.BigInteger` numerator/denominator.** Overkill: Flow durations stay within `int` range (PPQN 480 × 1024 bars × 32 = ~1.5e7).

3. **External library (Fractions, BigRational).** Violates STACK.md guiding principle.

**Recommendation: hand-roll.** ~50 LOC, zero dependency, easy to optimize. Place it under `flow-lang/TypeSystem/Fraction.cs` (sibling to `ArrayType.cs`). Consider it a primitive numeric helper — it does NOT need to be a `FlowType`.

### Migration plan (zero-disruption):

- All existing `MusicalNoteData` constructors take 0 new required parameters; `DurationFraction` is optional and null by default.
- `NoteStreamCompiler` for plain `NoteElement` continues to set `DurationValue` only (DurationFraction stays null).
- `NoteStreamCompiler.CompileTupletElement` sets `DurationFraction` to `(parentDurationFraction × tuplet ratio)` and leaves `DurationValue` at the *closest* enum value (for visual/MIDI hints) — but `GetBeats` prefers `DurationFraction` when present.
- All 70+ existing test files keep their byte-identical WAV output because the `DurationFraction == null` branch runs the existing math unchanged.

### Where Fraction propagates through the pipeline:

| Site | Behavior |
|------|----------|
| `MusicalNoteData.GetBeats` | Branches on `DurationFraction.HasValue` |
| `BarData.GetActualBeats` | Already calls `GetBeats` — no change needed |
| `BarRenderer.RenderBarToVoices` (line 51, 260) | Already calls `GetBeats` — no change needed |
| `MidiExport` | Convert `Fraction` to PPQN ticks: `(int)(frac.Num × 480 × 4 / frac.Den)` |
| `SequenceRenderer` | Uses `GetBeats` indirectly — no change |

This is why the rational-arithmetic decision is *load-bearing*: it touches one chokepoint (`GetBeats`) and the rest of the pipeline is unaffected.

---

## 4. Question 3 — Pragma System Architecture

### Recommendation: **Per-file pragmas during parsing, materialized into a `PragmaTable` on the `Parser`, then snapshotted onto each `Program` AST**. NOT on `ExecutionContext`.

### Reasoning:

- **The `H` alias is a LEXER concern** — it changes how the character `H` is tokenized inside note streams (BLetter vs Identifier vs Error). By the time `ExecutionContext` exists, lexing is done. Per-execution context arrives too late.
- **DEFER-04 multi-letter enharmonic edges are an INTERPRETER concern** — the existing `Enharmonic` runtime function would just check a flag.

So pragmas have two scopes that map to two pipeline stages. Treat them uniformly:

```csharp
// flow-lang/Runtime/PragmaTable.cs (NEW)
public sealed class PragmaTable {
    private readonly HashSet<string> _enabled = new(StringComparer.OrdinalIgnoreCase);
    public bool IsEnabled(string pragma) => _enabled.Contains(pragma);
    public void Enable(string pragma) => _enabled.Add(pragma);
    public PragmaTable Clone() { var c = new PragmaTable(); foreach (var p in _enabled) c.Enable(p); return c; }
}
```

### Lifecycle:

1. **Parser** maintains a `PragmaTable` instance during parsing. The `enable H_alias;` statement is parsed top-of-file (or anywhere — it takes effect at point-of-encounter going forward). Parser builds the table and attaches a snapshot to the `Program` AST: `Program.Pragmas`.
2. **Lexer-affecting pragmas** (`H_alias`) are problematic because lexing is done before parsing. Solution: do a **two-pass approach for lexer-affecting pragmas only** — a tiny pre-lex pass that scans for `enable H_alias;` lines via regex (line-anchored, BEFORE any non-pragma statement) and toggles a `LexerPragmas` struct passed into `SimpleLexer`. This is a 20-line pre-scanner, not a real second parse. Haskell's `LANGUAGE` pragmas use the same trick — they're regex-extractable BEFORE the GHC lexer runs.
3. **Interpreter-affecting pragmas** (`enable_enharmonic_edges`) flow naturally — `Program.Pragmas` is read by the interpreter at startup and copied into a `PragmaTable` field on `ExecutionContext`.

### Layering decision:

| Pragma | Stage that reads it | Storage |
|--------|---------------------|---------|
| `H_alias` | Lexer | `LexerPragmas` struct passed to `SimpleLexer.ctor` |
| `enable_enharmonic_edges` | Interpreter (HarmonyFunctions) | `ExecutionContext.Pragmas` |
| Future tuning pragmas (`enable_just_intonation`) | Interpreter | `ExecutionContext.Pragmas` |

### `use` import behavior:

- **Pragmas are file-scoped, NOT inherited by importing module.** Same as Haskell `LANGUAGE` pragmas. If a stdlib module enables an experimental syntax, that doesn't infect the user's main file.
- **Pragmas DO survive import in the imported module's own scope** — i.e. when interpreting code from `inner.flow`, that file's pragmas are active for the duration of its execution.
- Implementation: `Program.Pragmas` is per-AST. The `ModuleLoader` reads pragmas from the imported `Program` and pushes them onto a stack on `ExecutionContext`, popping when import returns.

### Why not global compiler flags (Scala-style)?

- A single REPL session might run multiple files with different pragma needs. Global flags break this.
- Watch mode reloads files independently. File-scoped pragmas re-evaluate on reload; global flags would persist stale state.

---

## 5. Question 4 — Microtonal Tuning Architecture

### The pluggability seam is `PitchConversion.NoteToFrequency`

Currently (`flow-lang/StandardLibrary/Audio/PitchConversion.cs`):
```csharp
public static double NoteToFrequency(char noteName, int octave, int alteration) {
    int midiNote = GetMidiNote(noteName, octave, alteration);
    return 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);   // ← hardcoded 12-TET
}
```

This is called from EVERY synthesizer (`PianoSynthesizer.cs`, `BrassSynthesizer.cs`, etc. — 11 call sites confirmed via grep). Routing tuning through this single function means **zero changes to synthesizers**.

### Recommendation: introduce `ITuningSystem` abstraction; lookup happens in `PitchConversion`, sourcing the active tuning from `MusicalContext`.

```csharp
// flow-lang/StandardLibrary/Audio/Tuning/ITuningSystem.cs (NEW)
public interface ITuningSystem {
    string Name { get; }
    double NoteToFrequency(char noteName, int octave, int alteration, double? centOffset);
}

public sealed class EqualTemperament : ITuningSystem { /* current behavior */ }
public sealed class JustIntonation : ITuningSystem {
    private readonly char _tonicLetter;
    private readonly int _tonicAlteration;
    // 5-limit ratios from tonic: 1/1, 16/15, 9/8, 6/5, 5/4, 4/3, 45/32, 3/2, 8/5, 5/3, 16/9, 15/8
    public double NoteToFrequency(...) { /* ratio table relative to tonic */ }
}
public sealed class CustomRatioTuning : ITuningSystem {
    private readonly Fraction[] _ratios;   // 12 ratios from tonic (one per chromatic step)
    // ...
}
```

### Where the tuning lookup happens:

```csharp
// flow-lang/StandardLibrary/Audio/PitchConversion.cs (MODIFIED)
public static double NoteToFrequency(char noteName, int octave, int alteration,
                                     double? centOffset = null, ITuningSystem? tuning = null) {
    tuning ??= EqualTemperament.Instance;   // default preserves existing behavior
    return tuning.NoteToFrequency(noteName, octave, alteration, centOffset);
}
```

### Where the tuning is set:

- **MusicalContext** gains `ITuningSystem? Tuning { get; set; }`.
- New context block: `tuning just C { ... }`, `tuning equal { ... }`, `tuning ratios [1, 16/15, 9/8, ...] C { ... }`.
- The synth call site reads it: `var tuning = context.GetMusicalContext().Tuning ?? EqualTemperament.Instance; PitchConversion.NoteToFrequency(..., tuning);`.

### Where tuning does NOT live:

- **NOT on Voice** — voices are post-pitch-resolution. By the time you have a `Voice`, frequency is baked in.
- **NOT on Synthesizer** — synthesizers should be pitch-agnostic (they take a frequency in Hz). Putting tuning on the synthesizer would force every preset to know about every tuning.
- **NOT in `renderSong`** — that's after sequence compilation. By then, if we deferred tuning to render time, we'd need to plumb it through every intermediate buffer.

### Critical detail: the lookup site needs to receive `MusicalContext`.

Currently synthesizers call `PitchConversion.NoteToFrequency(note)` with no context awareness. This requires plumbing `MusicalContext` (or just the active `ITuningSystem`) into `BarRenderer.RenderBarToVoices`. The good news: `BarRenderer` already receives `MusicalContext` indirectly via `bpm` parameter — extending its signature to accept the tuning is a one-call-site change at each renderer entry point.

**Touch points for tuning:**
- `Audio/PitchConversion.cs` — accept tuning param
- `Audio/Tuning/` — new directory for tuning systems
- `Runtime/MusicalContext.cs` — add `Tuning` field
- `Audio/BarRenderer.cs` — propagate tuning to `NoteToFrequency` calls
- 11 synthesizer files — each needs the call signature updated to pass tuning through (mechanical)
- `Parsing/Parser.cs` — parse `tuning <name> [args] { ... }` block as a new musical-context variant
- `Interpreter/Interpreter.cs` — push/pop tuning on the musical-context stack

This is **medium blast radius** — wide but mechanical.

---

## 6. Question 5 — Scale Linting Integration

### Recommendation: **Pure `flow-lsp` logic that walks the AST + tracks key blocks via a parser-level scope analyzer**. Do NOT add a "lint mode" to the interpreter.

### Why not in flow-lang interpreter:

- The interpreter is execution-time. Scale linting is an authoring-time concern. Running the interpreter to lint forces evaluating arbitrary user code — slow, side-effectful (writes WAV files, plays audio).
- Already in v1.2 the LSP makes a deliberate decision: it references `flow-lang` for the lexer/parser/error reporter ONLY. Adding interpreter-mode coupling reverses that win.
- The interpreter would have to grow an "abstract evaluation" mode that doesn't render audio but does track musical context — non-trivial new code path.

### MusicalContext propagation in flow-lsp (parse-only world):

`flow-lsp` already has `NoteStreamContext.FindEnclosingKey` (`flow-lsp/NoteStream/NoteStreamContext.cs`) which uses brace-depth scanning over the token list to find the innermost `key Cmajor { ... }` block enclosing a cursor position. This pattern generalizes:

```csharp
// flow-lsp/Diagnostics/ScaleLinter.cs (NEW)
public static class ScaleLinter {
    public static IEnumerable<Diagnostic> Lint(Program ast, IReadOnlyList<Token> tokens) {
        // For each NoteStreamExpression in the AST:
        //   1. Walk up through parent statements to find enclosing musical-context blocks
        //   2. Collect key {x}, tempo, etc. into a synthetic MusicalContext (parse-time only)
        //   3. For each NoteElement in the stream: check NoteType.GetMidiNote against
        //      ScaleDatabase.GetScaleNotes(key)
        //   4. Emit Diagnostic (Severity.Warning) for out-of-key notes
    }
}
```

The trick is that AST currently lacks parent pointers. Two options:

1. **Build a parent map** during the diagnostic pass — single AST walk producing `Dictionary<NoteStreamExpression, IReadOnlyList<MusicalContextStatement>>`. This is a ~30-line traversal.

2. **Use the brace-depth-on-tokens approach** that `NoteStreamContext` already uses — just replace "find enclosing key" with "synthesize MusicalContext from enclosing context blocks". This is more uniform with v1.2 code.

**Recommend option 2** — reuses the `NoteStreamContext` pattern, no new AST traversal infrastructure.

### Pragma to disable linting:

`enable strict_scale_lint;` (default off, opt-in warning). Or inverse: `disable scale_lint;` per-file. This is the FIRST place pragmas pay off — gives users escape hatch when they want chromatic passages.

---

## 7. Question 6 — File-Level Touch Map

### Legend
- **N** = New file
- **M** = Modified file
- **HBR** = High blast radius (3+ files)
- **MBR** = Medium blast radius (1-2 files)
- **LBR** = Low blast radius (0-1 files outside its own directory)

### Feature 1: Tuplets (`(3:2 C4 D4 E4)q`) — **HBR (8 files)**

| File | Change |
|------|--------|
| `flow-lang/Lexing/SimpleLexer.cs` | M — recognize `:` between integers inside `(` as tuplet ratio (already a token, but tighten to context) |
| `flow-lang/Parsing/Parser.NoteStream.cs` | M — add tuplet branch in `(` dispatch (sibling to ghost/grace/?/??) |
| `flow-lang/Ast/Expressions/NoteStreamExpression.cs` | M — add `TupletElement` record |
| `flow-lang/TypeSystem/Fraction.cs` | N — hand-rolled rational struct |
| `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` | M — add `Fraction? DurationFraction` to `MusicalNoteData` |
| `flow-lang/Runtime/NoteStreamCompiler.cs` | M — `CompileTupletElement` recursive method, propagate scale through children |
| `flow-lang/StandardLibrary/Audio/MidiExport.cs` | M — convert `DurationFraction` to PPQN ticks for tuplets |
| `flow-lsp/Semantic/SemanticTokensEncoder.cs` | M — colorize tuplet ratio numbers as `number` token |
| `flow-lang/StandardLibrary/BuiltInDocs.cs` | M — document tuplet syntax for hover |

### Feature 2: Arbitrary fractional durations (`C4/12`) — **MBR (3 files)**

| File | Change |
|------|--------|
| `flow-lang/Lexing/SimpleLexer.cs` | M — extend note-literal regex to recognize `/N` after note name as arbitrary-duration suffix |
| `flow-lang/Parsing/Parser.NoteStream.cs` | M — `TryParseDurationSuffix` accepts `/12` form alongside `q`, `e`, etc. |
| `flow-lang/Runtime/NoteStreamCompiler.cs` | M — when duration is `/N`, compute `Fraction(4, N)` (since N=12 means twelfth-note = 4/12 quarter-units) and set `MusicalNoteData.DurationFraction` |

Note: the `Fraction` struct from Feature 1 is a prerequisite — these two features SHARE that infrastructure.

### Feature 3: `range(Int, Int)` and `range(Int, Int, Int)` — **LBR (1 file)**

| File | Change |
|------|--------|
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` | M — register two new `FunctionSignature` entries; existing `range` is in `BuiltInDocs.cs:38` so docs already describe it |

This is a 15-LOC addition. Truly trivial.

### Feature 4: Pragma system (`enable H_alias;`) — **HBR (5-6 files)**

| File | Change |
|------|--------|
| `flow-lang/Runtime/PragmaTable.cs` | N — pragma storage |
| `flow-lang/Lexing/SimpleLexer.cs` | M — pre-scan for lexer-affecting pragmas (~20 LOC), pass `LexerPragmas` struct in ctor |
| `flow-lang/Parsing/Parser.cs` | M — parse `enable <name>;` statement, accumulate into `Program.Pragmas` |
| `flow-lang/Ast/Statements/PragmaStatement.cs` | N — AST node |
| `flow-lang/Ast/Program.cs` | M — add `IReadOnlyList<PragmaStatement> Pragmas` field |
| `flow-lang/Runtime/ExecutionContext.cs` | M — add `PragmaTable Pragmas` field, populate from `Program.Pragmas` at startup |
| `flow-lang/Runtime/ModuleLoader.cs` | M — push/pop pragma scope on `use` |

### Feature 5: Multi-letter enharmonic edges (DEFER-04, E↔Fb etc.) — **LBR (1 file)**

| File | Change |
|------|--------|
| `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` | M — gate the existing `D-05: naturals return unchanged` early-exit on `pragma enharmonic_edges`. When pragma on, fall through to compute Fb/E#/Cb/B# spellings. |

### Feature 6: Slice negative-from-end indexing (DEFER-05) — **LBR (1 file)**

| File | Change |
|------|--------|
| `flow-lang/StandardLibrary/Collections.cs` | M — in `SliceArray` and `SliceSequence` (line 157, 185), interpret negative `start`/`end` as offset-from-end before clamping |

### Feature 7: Gaussian humanize (DEFER-06) — **LBR (1 file)**

| File | Change |
|------|--------|
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` | M — extend `BuildEuclideanSequence` (called from `RegisterEuclideanOverloads`) to accept distribution flag; or add 7-arg overload. Box-Muller transform for Gaussian sample. ~20 LOC. |

### Feature 8: Arpeggio parameters — **LBR (1 file)**

| File | Change |
|------|--------|
| `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` | M — add overloads `arpeggio(Chord, String dir, NoteValue rate)` and `arpeggio(Chord, String dir, NoteValue rate, String pattern)` (e.g. "1357", "1537"). Modify the existing `arpeggio` (line 309) to be the no-rate default. |

### Feature 9: Chord inversions/voicings — **MBR (3 files)**

| File | Change |
|------|--------|
| `flow-lang/StandardLibrary/Harmony/ChordParser.cs` | M — recognize `/3`, `/5`, `/7` suffix and `:1`, `:2` voicing markers; populate new field on `ChordData` |
| `flow-lang/TypeSystem/SpecialTypes/ChordType.cs` (ChordData) | M — add `int Inversion` field |
| `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` | M — `chordNotes` reads inversion and rotates note list accordingly |

### Feature 10: Delay sync to NoteValue — **MBR (2 files)**

| File | Change |
|------|--------|
| `flow-lang/StandardLibrary/Audio/DSP/Delay.cs` | M — overload `Apply(buffer, NoteValue dur, double bpm, feedback, mix)` that converts to ms via `60000 / bpm × ToFraction(dur) × 4` |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` | M — register the new `delay` overload signature; tempo/bpm comes from `MusicalContext.Tempo` ?? 120 |

### Feature 11: Microtonal ratios — **HBR (10+ files)**

See §5 above for full detail. Affects every synthesizer, `PitchConversion`, `MusicalContext`, `Parser`, `Interpreter`. **Highest blast radius of any v1.3 feature.**

| File | Change |
|------|--------|
| `flow-lang/StandardLibrary/Audio/Tuning/ITuningSystem.cs` | N |
| `flow-lang/StandardLibrary/Audio/Tuning/EqualTemperament.cs` | N |
| `flow-lang/StandardLibrary/Audio/Tuning/JustIntonation.cs` | N |
| `flow-lang/StandardLibrary/Audio/Tuning/CustomRatioTuning.cs` | N |
| `flow-lang/StandardLibrary/Audio/PitchConversion.cs` | M — accept tuning param |
| `flow-lang/Runtime/MusicalContext.cs` | M — add `Tuning` field |
| `flow-lang/Parsing/Parser.cs` | M — parse `tuning <name> [args] { ... }` block |
| `flow-lang/Interpreter/Interpreter.cs` | M — handle new context block |
| `flow-lang/StandardLibrary/Audio/BarRenderer.cs` | M — propagate tuning into synth calls |
| 11× `flow-lang/StandardLibrary/Audio/Synthesizers/*.cs` | M — accept tuning, pass through to `PitchConversion` |

### Feature 12: Scale linting — **MBR (3 files in flow-lsp; 0 in flow-lang)**

| File | Change |
|------|--------|
| `flow-lsp/Diagnostics/ScaleLinter.cs` | N |
| `flow-lsp/Handlers/DiagnosticsPublisher.cs` | M — invoke `ScaleLinter.Lint` and merge with parse diagnostics |
| `flow-lsp/NoteStream/NoteStreamContext.cs` | M (small) — extract `FindEnclosingKey` brace-walk into a reusable `FindEnclosingMusicalContext` returning a synthetic `MusicalContext` |

### Feature 13: Legato/portamento articulations — **MBR (3 files)**

| File | Change |
|------|--------|
| `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` | M — extend `Articulation` enum with `Legato`, `Portamento` |
| `flow-lang/Parsing/Parser.NoteStream.cs` | M — `TryParseArticulation` recognizes new tokens (`leg`, `port`) |
| `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs` | M — `Legato` extends release into next note's attack; `Portamento` adds frequency glide |
| `flow-lang/StandardLibrary/Audio/MidiExport.cs` | M — emit MIDI CC65 (portamento on/off) for portamento-marked notes |

### Feature 14: Snap-to-grid quantize — **LBR (1 file)**

| File | Change |
|------|--------|
| `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` | M — add `quantize(Sequence, NoteValue grid) → Sequence`. Walks bar-by-bar, snaps each note's onset to nearest grid multiple. Operates in beat-space using existing `BarData.ToTimeline`. |

### Feature 15: WAV pitch-shift on load — **LBR (1-2 files)**

| File | Change |
|------|--------|
| `flow-lang/StandardLibrary/Audio/FileIO.cs` (loadWav) | M — accept optional `Semitone` shift; resample via simple linear interpolation OR retain length via PSOLA. Recommend simple resample first — pitch + length both change, document this trade-off. |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` | M — register new `loadWav(String, Semitone)` overload |

### Blast-radius summary

| Feature | Files | Blast |
|---------|-------|-------|
| Tuplets | 9 | HBR |
| Arbitrary durations | 3 | MBR (shares Fraction with tuplets) |
| range | 1 | LBR |
| Pragma system | 6-7 | HBR |
| Enharmonic edges | 1 | LBR |
| Slice negative | 1 | LBR |
| Gaussian humanize | 1 | LBR |
| Arpeggio params | 1 | LBR |
| Chord inversions | 3 | MBR |
| Delay sync | 2 | MBR |
| Microtonal | 18+ | HBR (highest) |
| Scale linting | 3 (flow-lsp) | MBR |
| Legato/portamento | 4 | MBR |
| Snap-to-grid | 1 | LBR |
| WAV pitch-shift | 2 | LBR |

---

## 8. Question 7 — Suggested Build Order

### Build-order graph (→ = depends on)

```
                       Fraction struct (foundation)
                         /            \
                Tuplets         Arbitrary durations
                   ↓                      ↓
          Arpeggio rate=tuplet      Snap-to-grid (uses fractions)
                   ↓
              Delay sync (uses NoteValue + tempo)


       PragmaTable infrastructure
              /     \
       H_alias    Enharmonic edges (DEFER-04)
                       ↓
                Scale linting (uses pragma to disable)


            range stdlib            (independent)
            Slice negative         (independent)
            Gaussian humanize      (independent)
            Chord inversions       (independent)
            WAV pitch-shift        (independent)
            Legato/portamento      (independent)


                  Microtonal (independent BUT highest blast radius —
                              schedule as dedicated phase)
```

### Recommended phase ordering

**Phase A — Foundation (Fraction + Tuplets + Arbitrary durations)**
- Ship `Fraction` struct first
- Tuplets and arbitrary durations land together (they share `MusicalNoteData.DurationFraction`)
- Tutorial test: `(3:2 C4 D4 E4)q` plays as triplet, byte-identical determinism preserved
- Feeds: arpeggio params (next phase), delay sync (next phase), snap-to-grid (later phase)

**Phase B — Pragma infrastructure**
- `PragmaTable`, `PragmaStatement` AST, lexer pre-scan
- Land BEFORE H_alias and enharmonic-edges (DEFER-02/03 + DEFER-04)
- Once pragma works, those two land as 1-LOC each (just check the flag)

**Phase C — DEFER closures (cheap, mostly independent)**
- range (DEFER-01), slice negative (DEFER-05), Gaussian humanize (DEFER-06) — all LBR, no dependencies
- H_alias (DEFER-02/03), enharmonic-edges (DEFER-04) — depend on Phase B pragma
- This phase clears all 6 DEFER items in one swoop

**Phase D — Tier B/C composer DX (uses tuplets from Phase A)**
- Arpeggio params (depends on tuplet rate semantics from Phase A)
- Chord inversions (independent)
- Delay sync (uses NoteValue + tempo — could ship before tuplets, no hard dep)
- Snap-to-grid (uses Fraction from Phase A)
- Legato/portamento (independent)
- WAV pitch-shift (independent)

**Phase E — Microtonal**
- Highest blast radius. Dedicate a single phase.
- Land tuning context block, ITuningSystem, EqualTemperament (default), JustIntonation, CustomRatioTuning
- Verify all 11 synth presets still produce byte-identical 12-TET output when tuning is unset

**Phase F — Scale linting (flow-lsp only)**
- Depends on Phase B pragma (for `enable strict_scale_lint`)
- Pure flow-lsp work, no flow-lang touch
- Can run in parallel with Phase E

### Critical-path observations

- **`Fraction` is the root dependency.** Cannot ship tuplets, arbitrary durations, snap-to-grid, or precise delay-sync math without it.
- **Pragma system is the second root.** Without it, DEFER-02/03 (H_alias) and DEFER-04 (enharmonic edges) and Phase F (scale lint opt-in) are blocked.
- **Microtonal is independent but expensive.** Don't bundle with anything else — it's its own phase to keep blast-radius reviewable.
- **DEFER closures cluster well.** Six items, cheap, mostly independent — landing them in one phase gives a satisfying "DEFER list cleared" milestone moment.
- **Tuplets unlock arpeggio rate semantics.** When `arpeggio(chord, "up", q)` ships, users will immediately want `arpeggio(chord, "up", (3:2 q))` — that already works for free if tuplets land first because the parameter is just a NoteValue with a Fraction backing.

### Anti-suggestion: do NOT do tuplets and microtonal in the same phase

Both are HBR. Their blast radii overlap on `MusicalContext` and `BarRenderer`. Reviewing one set of changes is hard enough; reviewing both at once is a code-review trap. Keep them in separate phases even though they're technically independent.

---

## 9. Anti-Patterns to Avoid

### Anti-Pattern 1: Adding tuplet support by extending `NoteValue` enum

**What people do:** Add `TUPLET_THIRD = 6, TUPLET_FIFTH = 7, ...` to the enum.
**Why wrong:** Doesn't compose under nested tuplets; explodes combinatorially with dotted-tuplets; breaks MIDI tick math.
**Do this instead:** Add `Fraction? DurationFraction` to `MusicalNoteData` and let `GetBeats` branch on it.

### Anti-Pattern 2: Pragma table on `ExecutionContext` only

**What people do:** Put pragma flags on `ExecutionContext`, set them at runtime from `enable` statements.
**Why wrong:** Lexer-affecting pragmas (`H_alias`) need to fire BEFORE the parser runs, let alone the interpreter. Runtime pragma table can't reach back into the lexer.
**Do this instead:** Two-stage pragma model — lexer pragmas via 20-line pre-scanner; interpreter pragmas on `ExecutionContext`. Same `PragmaTable` storage type used in both places.

### Anti-Pattern 3: Tuning system on the synthesizer

**What people do:** Each synthesizer (`PianoSynthesizer`, etc.) takes a `tuning` parameter and converts pitch internally.
**Why wrong:** 11 synthesizers × every tuning system = combinatorial test surface. Synthesizers should be pitch-blind (they take Hz).
**Do this instead:** Keep tuning at `PitchConversion.NoteToFrequency` — single chokepoint, synthesizers stay pitch-agnostic.

### Anti-Pattern 4: Scale linting in the interpreter

**What people do:** Add a `--lint` mode to flow-interpreter that runs the interpreter abstractly.
**Why wrong:** Forces creating an "abstract evaluation" interpreter mode (huge new code path). Violates v1.2's clean separation: flow-lsp consumes lexer/parser only.
**Do this instead:** Pure flow-lsp pass walking AST + brace-depth scanning of tokens for context resolution. Reuses the Phase 17 `NoteStreamContext` pattern.

### Anti-Pattern 5: `double` for tuplet duration scaling

**What people do:** Multiply durations by `2.0/3.0` in floats.
**Why wrong:** Accumulates ε per note. v1.2 byte-identical determinism contract breaks. MIDI tick conversion drifts.
**Do this instead:** `Fraction.Multiply` keeps integers in lowest terms.

---

## 10. Integration Points (Summary Table)

### Internal Boundaries Affected

| Boundary | v1.3 Feature That Crosses It | Communication Pattern |
|----------|------------------------------|------------------------|
| Lexer ↔ Parser | Pragma `H_alias` (lexer-affecting) | Pre-lex pragma scan; LexerPragmas struct passed into ctor |
| Parser ↔ Interpreter | All pragmas, all new AST nodes | AST node fields; `Program.Pragmas` snapshot |
| Interpreter ↔ NoteStreamCompiler | Tuplet scaling factor | New `outerScale: Fraction` parameter on `CompileTupletElement` |
| MusicalContext ↔ Synthesizer | Tuning system | New `ITuningSystem` field on MusicalContext, propagated through BarRenderer |
| flow-lang ↔ flow-lsp | Scale linting | flow-lsp re-uses `ScaleDatabase.GetScaleNotes`, walks tokens itself for context |
| flow-lang ↔ DryWetMidi | Tuplet ticks | `Fraction → PPQN ticks` conversion in `MidiExport` |

### External Services

None new. DryWetMidi already integrated since v1.2 (Phase 14 DX-08). Microtonal feature uses no external library — hand-rolled tuning table.

---

## 11. Open Risks (Flagged for Roadmap)

1. **`Fraction` arithmetic perf** — note streams in long compositions might create many Fraction instances. Mitigate by making `Fraction` a `readonly record struct` (stack-allocated, no GC). Confidence: HIGH this is sufficient based on existing `MusicalNoteData` allocation patterns.

2. **Tuplet auto-fit interaction** — `CalculateAutoFitDuration` (NoteStreamCompiler.cs:206) is the trickiest existing logic. Tuplet children inside an auto-fit bar need their *combined* time to count as one auto-fit slot, not N. Recommend: tuplets always require explicit duration on the group `(3:2 C4 D4 E4)q` — disallow auto-fit-inside-tuplet for v1.3, document as "explicit only" constraint. Lift constraint in v1.4 if users complain.

3. **Microtonal cents interaction with tuning** — existing `centOffset` is a *post-pitch* adjustment (added to MIDI cents). With non-12-TET tuning, "+50c" becomes ambiguous: 50 cents of equal-temperament, or 50 cents of the active tuning's step? Recommend: cents always mean equal-temperament cents (Hz-multiplicative `2^(c/1200)`), applied AFTER tuning lookup. Document this clearly. Charitable-interpretation principle from MEMORY.md says: "music > rigid correctness" — this is the user-friendly default.

4. **Lexer pre-scan for pragmas** — feels hacky. The actual risk is a `enable` keyword inside a string literal getting matched by the regex. Mitigate: regex must be line-anchored AND require pragma to appear before any non-comment, non-pragma statement. Same constraint Haskell uses for `LANGUAGE` pragmas.

5. **flow-lsp sync with new AST nodes** — every new `NoteStreamElement` type requires a new branch in `SemanticTokensEncoder` for proper colorization. Add a "new AST node added" checklist item to flow-lsp PR template.

---

## Sources

- [music21 Advanced Durations & Tuplets](https://music21.org/music21docs/usersGuide/usersGuide_19_duration2.html) — canonical Fraction-based duration model with tuplet ratio multiplication; verified MIT/active-maintenance reference
- [music21 duration.py source](https://github.com/cuthbertLab/music21/blob/master/music21/duration.py) — `DurationTuple` (type, dots, quarterLength) implementation pattern
- [music21j Tuplet class docs](http://tarmo.uuu.ee/varia/failid/komp/music21j/doc/music21.duration.Tuplet.html) — JavaScript port confirms ratio semantics ("5-in-the-place-of-4 means 4/5ths as long")
- [Formalizing Time Units to Handle Symbolic Music Durations](https://arxiv.org/pdf/2310.14952) — academic paper on rational-time representation in music engines
- Codebase: `flow-lang/Runtime/NoteStreamCompiler.cs`, `flow-lang/Ast/Expressions/NoteStreamExpression.cs`, `flow-lang/TypeSystem/SpecialTypes/NoteType.cs`, `flow-lang/Parsing/Parser.NoteStream.cs`, `flow-lang/Runtime/ExecutionContext.cs`, `flow-lang/Runtime/MusicalContext.cs`, `flow-lang/StandardLibrary/Audio/PitchConversion.cs`, `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs`, `flow-lsp/NoteStream/NoteStreamContext.cs` — all read in full during research

---
*Architecture research for: Flow v1.3 — tuplets, arbitrary durations, DEFER closures, Tier B/C composer DX*
*Researched: 2026-04-26*
