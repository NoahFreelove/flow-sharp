# Phase 24: Scale Linting (flow-lsp) - Research

**Researched:** 2026-05-04
**Domain:** flow-lsp static analysis pass + closed-set pragma activation
**Confidence:** HIGH (every recommendation grounded in inspected code at cited line numbers)

## Summary

CONTEXT.md is unusually detailed — 23 locked decisions (D-01..D-23) plus a tight Claude's Discretion list. This research therefore focuses entirely on the eight discretion items called out in the spawn brief, each grounded in inspected file/line evidence rather than re-derived from training data. All claims are `[VERIFIED]` against the actual codebase unless explicitly tagged otherwise.

The phase is genuinely small: one mechanical line in `flow-lang/Lexing/PragmaRegistry.cs:16` and a self-contained analysis pass living entirely under `flow-lsp/`. The non-trivial work is (a) deriving 30 diatonic-spelling sets across 6 roots × 7 modes (treating them as a hardcoded map per the existing `TuningTables` precedent), (b) wiring the analyzer into the existing `didChange` → `DiagnosticsPublisher.Publish` pipeline without breaking the empty-publish-clears-squiggles invariant, and (c) writing xUnit Facts that exercise spelling-aware corner cases (`E#4` in Cmajor, `Gb4` in Cmajor, nested-key innermost-wins).

**One planning-critical gap was discovered:** `flow-lsp/ParseSession.cs:22` instantiates `new Parser(tokens, er)` — the 2-arg overload — and never calls `PragmaScanner.Scan` upstream. As a result, `parseResult.Ast.Pragmas` is currently always `PragmaSet.Empty` in the LSP. **D-19 cannot work as written until ParseSession is widened to run the same pragma-scan-then-parse pipeline that `FlowEngine.Run()` already uses at `flow-lang/Core/FlowEngine.cs:70-82`.** This is not an alternative-path question — it's a precondition. Documented in §Common Pitfalls below and called out as a Wave 0 task.

**Primary recommendation:** Sibling `IScaleLintPublisher` interface invoked alongside `IDiagnosticsPublisher` from the existing `DocumentManager` onParse callback in `flow-lsp/Program.cs:40-52`. Hardcoded 30-key × 7-mode `DiatonicSpellings` map (210 string arrays) following the `TuningTables` precedent. Tests under `flow-lang.Tests/Unit/Phase24/` mirroring the existing Phase 17 LSP convention (no new test project). Single combined `tests/test_scale_lint.flow` smoke + xUnit per-mode coverage. Lower-pitch-first ordering for alternative suggestions. Analyzer runs on every parse (including partial parses with errors) — no caching of per-key sets across `didChange` calls.

## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Spelling-aware diatonic check — letter+accidental compared against the active key's diatonic spelling set, not just the pitch-class set. `E#4` flagged in Cmajor even though pitch-class 5 IS diatonic.
- **D-02:** All 7 church modes produce diagnostics — `key <root>{major,minor,dorian,phrygian,lydian,mixolydian,locrian}` via `ScaleDatabase.TryParseKeyWithMode`.
- **D-03:** Pentatonic / blues / harmonic-minor / melodic-minor / whole-tone / octatonic — OUT OF SCOPE.
- **D-04:** 7-mode diatonic-spelling helper lives in `flow-lsp/Diagnostics/DiatonicSpellings.cs` — private to flow-lsp. Honors "zero flow-lang touch" verbatim.
- **D-05:** Helper signature `static IReadOnlyList<string> GetDiatonicSpellings(string rootNote, Mode mode)` (or `IReadOnlySet<string>`).
- **D-06..D-10:** Element traversal — `NoteElement` always checked; `ChordElement` recursed; `NoteElement` with `CentOffset` checked at base note (cents irrelevant); `RandomChoiceElement` recursed; `TupletElement` recursed (including nested).
- **D-11..D-14:** SKIP — `RomanNumeralElement` (diatonic-by-construction), `NamedChordElement` (intentional declarative notation), `VariableReferenceElement` (statically undecidable), `RestElement` (no pitch).
- **D-15:** Notes inside `| ... |` but outside any `key { }` block — zero diagnostics. Detection via `NoteStreamContext.FindEnclosingKey` returning null.
- **D-16:** Diagnostic message format — three branches (standard / spelling-aware-pitch-class-match / same-pitch-class-different-spelling).
- **D-17:** Token-wide squiggle range — analyzer walks `ParseResult.Tokens` to find matching `NoteLiteral`, builds Range from `(line-1, col-1)` to `(line-1, col-1 + Token.Text.Length)`. Use `Token.OriginalText` for message text per Phase 21 D-15.
- **D-18:** Diagnostic `Source` string is `"flow.scaleLint"` (not `"flow"`) — enables editor-toggle independent filtering.
- **D-19:** Activation gate is `parseResult.Ast.Pragmas.Has("scaleLint")`. Analyzer is invoked unconditionally per `didChange` but short-circuits when pragma absent.
- **D-20:** REPL pragma scope is per-line (inherits Phase 21 D-07). LSP doesn't run inside REPL — symmetry-only.
- **D-21:** REUSE `NoteStreamContext.FindEnclosingKey` VERBATIM at `flow-lsp/NoteStream/NoteStreamContext.cs:43`.
- **D-22:** When innermost key is non-parseable by `TryParseKeyWithMode` (e.g., `key Eblues { }`), zero diagnostics for that block — silent fail-open.
- **D-23:** When `enable scaleLint;` declared but no `key { ... }` block exists, emit zero diagnostics — no meta-diagnostic.

### Claude's Discretion

1. Pipeline integration shape: extend `DiagnosticsPublisher.Publish` vs widen `FlowError.Source` vs new `IScaleLintPublisher` interface
2. Diatonic-spelling derivation: 30-key hardcoded map vs circle-of-fifths algorithm
3. Test placement: `flow-lang.Tests/Unit/Phase24/` vs new `flow-lsp.Tests/` project
4. Per-mode acceptance smokes vs single combined `.flow` smoke
5. Alternative-pitch suggestion ordering convention
6. Run analyzer on partial-parse output vs only clean parses
7. Cache per-key diatonic-spelling set across `didChange` calls
8. Validation Architecture (Nyquist) — dimensions / test types this phase needs

### Deferred Ideas (OUT OF SCOPE)

- Pentatonic / blues / harmonic-minor / melodic-minor / whole-tone / octatonic scales
- Quick-fix code actions ("respell as F4", "wrap in `key Gmajor { }`")
- Borrowed-chord / modal-mixture analysis (bVII in major, neapolitan, secondary dominants)
- Roman numeral mismatch warnings
- Hover-rich diagnostic detail
- Default-on scale linting — explicit anti-feature per REQUIREMENTS.md line 113
- "Did you mean a different mode?" mode-suggestion hint
- CLI lint mode (`dotnet run --project flow-lsp -- --lint path/to/file.flow`)
- Configurable diagnostic severity
- Standalone notes outside `| ... |` note streams

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| LINT-01 | `enable scaleLint;` activates lint; `key Cmajor { \| C4 D4 E4 F#4 G4 \| }` shows Information squiggle on F#4 | Standard Stack §Diatonic Spellings + Architecture §Pipeline §Range Computation |
| LINT-02 | Without `enable scaleLint;`, zero scale-lint diagnostics — never default-on | Architecture §Pragma Activation Gate (D-19 short-circuit) |
| LINT-03 | Innermost active key wins (`key Cmajor { key Aminor { \| F#4 \| } }` checks against Aminor) | Architecture §Innermost-Key Resolution (reuses `NoteStreamContext.FindEnclosingKey`) |

## Project Constraints (from CLAUDE.md)

| Directive | Source | Application to Phase 24 |
|-----------|--------|-------------------------|
| .NET 10, file-scoped namespaces | CLAUDE.md §C# Conventions | All new files under `flow-lsp/Diagnostics/` use file-scoped namespace |
| AST nodes are records, pattern-match dispatch | CLAUDE.md §C# Conventions | Analyzer dispatches `NoteStreamElement` via `switch` expression, not visitor pattern |
| No new external dependencies | CLAUDE.md §Guiding Principle | Hardcoded spellings map; no NuGet additions |
| Existing .flow scripts byte-identical | CLAUDE.md §Constraints | Phase 24 is flow-lsp-only with one mechanical flow-lang line; tutorial.flow / showcase.flow byte-identical guaranteed by D-04 zero-runtime-touch |
| Closed-enum / closed-set design | CLAUDE.md house style | `Mode` enum (already closed) + `PragmaRegistry.KnownPragmas` (already closed); add one entry |
| Charitable interpretation memory | `~/.claude/projects/.../feedback_charitable_interpretation.md` | Drives D-22 (silent on `key Eblues { }`), D-23 (no meta-diagnostic), D-12/D-13 skip Roman numerals + chord literals |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Pragma name registration | flow-lang Lexing | — | `PragmaRegistry.KnownPragmas` is the closed-set authority; one-line add is the only flow-lang touch |
| Pragma extraction → `PragmaSet` | flow-lang Lexing (`PragmaScanner`) | — | Already wired by Phase 21 — the LSP just needs to call it |
| AST production (`Program.Pragmas`, note-stream elements, `MusicalContextStatement`) | flow-lang Parsing | — | Existing path; LSP consumes results read-only |
| Pragma-scan-then-parse pipeline | flow-lsp ParseSession | — | **Currently missing**; needs widening to mirror `FlowEngine.Run()` lines 66-82 |
| Diatonic-spelling derivation | flow-lsp Diagnostics (new `DiatonicSpellings.cs`) | — | "Zero flow-lang touch" per D-04; if a second consumer emerges, promote later |
| Innermost-key resolution at source offset | flow-lsp NoteStream (existing `NoteStreamContext.FindEnclosingKey`) | — | Phase 17 D-11 already shipped; reused VERBATIM |
| AST traversal (find `MusicalContextStatement(Key)` blocks → `NoteStreamExpression` children) | flow-lsp Diagnostics (new `ScaleLintAnalyzer.cs`) | — | Pure read of `Program.Statements`; no runtime |
| Diagnostic publish (LSP wire) | flow-lsp Handlers (existing `DiagnosticsPublisher` or new sibling) | — | OmniSharp.LanguageServer.Protocol.PublishDiagnostics |
| Activation gating | flow-lsp Diagnostics (new `ScaleLintAnalyzer`) | — | Reads `parseResult.Ast.Pragmas.Has("scaleLint")` — short-circuit at top of analyzer |

## Standard Stack

### Core (Existing — No Changes)

| Library / Module | Version | Purpose | Why Standard |
|------|---------|---------|--------------|
| .NET 10 | net10.0 | Runtime | CLAUDE.md mandates net10.0 |
| OmniSharp.Extensions.LanguageServer | (existing in flow-lsp.csproj) | LSP wire types — `Diagnostic`, `Range`, `Position`, `DiagnosticSeverity` | Already shipped by Phase 17 D-01; no version change needed |
| xunit.v3 8.0.x | (per `flow-lang.Tests.csproj:13`) | Test framework | Already used by Phase 21/23 Facts; pattern locked |

### Reused Existing flow-lang APIs (READ-ONLY)

| Symbol | File | Purpose in Phase 24 |
|--------|------|---------------------|
| `PragmaRegistry.KnownPragmas` | `flow-lang/Lexing/PragmaRegistry.cs:16` | One-line add: `["scaleLint"] = "Inside `key { ... }` blocks, surface non-diatonic notes as Information-severity LSP diagnostics."` |
| `PragmaScanner.Scan(source, fileName, errors)` | `flow-lang/Lexing/PragmaScanner.cs:84` | Returns `(PragmaSet, transformedSource)`. ParseSession must call this BEFORE lex+parse to populate `Program.Pragmas` |
| `PragmaSet.Has(string)` | `flow-lang/Lexing/PragmaSet.cs:27` | Activation gate per D-19 |
| `Program.Pragmas` | `flow-lang/Ast/Program.cs:18` | Read by analyzer |
| `Parser(tokens, errorReporter, pragmaSet?)` | `flow-lang/Parsing/Parser.cs:33` | 3-arg overload exists; ParseSession must pass `pragmaSet` |
| `SimpleLexer(source, er, fileName?, pragmaSet?)` | `flow-lang/Lexing/SimpleLexer.cs:23-24` | 4-arg overload exists; ParseSession must pass `pragmaSet` (relevant for `enable hAsB;` if file declares it) |
| `ScaleDatabase.TryParseKeyWithMode(name, out root, out Mode)` | `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs:207` | Public; consumed by `DiatonicSpellings.GetDiatonicSpellings` for D-02 |
| `Mode` enum (Major / Minor / Dorian / Phrygian / Lydian / Mixolydian / Locrian) | `flow-lang/StandardLibrary/Audio/Tuning/Mode.cs:8-17` | Closed enum with 7 values exactly matching D-02 |
| `Token.Text` / `Token.OriginalText` / `Token.DiagnosticText` | `flow-lang/Lexing/Token.cs:19-32` | Token width and original spelling for D-17 |
| `NoteElement.NoteName` / `.Location` / `.CentOffset` | `flow-lang/Ast/Expressions/NoteStreamExpression.cs:15-25` | Element identity + location for traversal |
| `MusicalContextStatement(ContextType, Value, Body)` | `flow-lang/Ast/Statements/MusicalContextStatement.cs:14` | `ContextType=Key` discriminant; `Body` recursed |

### Reused Existing flow-lsp APIs (READ-ONLY)

| Symbol | File | Purpose in Phase 24 |
|--------|------|---------------------|
| `ParseSession.Parse(source, path)` → `ParseResult(Ast, Tokens, Errors)` | `flow-lsp/ParseSession.cs:18-23` | **WIDEN** (Wave 0) to run `PragmaScanner.Scan` upstream and pass `pragmaSet` to lexer + parser |
| `NoteStreamContext.FindEnclosingKey(ast, tokens, source, position)` | `flow-lsp/NoteStream/NoteStreamContext.cs:43` | REUSE VERBATIM per D-21 |
| `IDiagnosticsPublisher.Publish(uri, errors)` | `flow-lsp/Handlers/DiagnosticsPublisher.cs:14-17` | Existing parse-error pipeline; sibling pattern recommended (see Architecture) |
| `DocumentManager` onParse callback | `flow-lsp/Program.cs:40-52` | Single integration point; analyzer invocation injected here alongside existing `diag.Publish` |
| `LspMappings.ToSeverity` | `flow-lsp/LspMappings.cs:28-34` | Already maps `DiagnosticLevel.Info → DiagnosticSeverity.Information`; analyzer constructs `Diagnostic` directly per D-18 (skips `ToRange`) |

### New Files in Phase 24

| File | Purpose |
|------|---------|
| `flow-lsp/Diagnostics/DiatonicSpellings.cs` | Hardcoded 30-key × 7-mode spelling map per D-04/D-05 |
| `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` | AST traversal + diagnostic emission |
| `flow-lsp/Diagnostics/IScaleLintPublisher.cs` + `ScaleLintPublisher.cs` | Sibling publisher invoked from `DocumentManager` onParse alongside `IDiagnosticsPublisher` |
| `flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerFacts.cs` | xUnit Facts: per-mode positive + negative + spelling-aware corner cases |
| `flow-lang.Tests/Unit/Phase24/DiatonicSpellingsFacts.cs` | xUnit Facts: 30 spellings pinned exactly + Mode coverage |
| `flow-lang.Tests/Unit/Phase24/PragmaRegistryScaleLintFacts.cs` | xUnit Fact: `scaleLint` is now a known pragma; PragmaRegistryFacts.cs:28 negative assertion replaced |
| `flow-lang.Tests/Unit/Phase24/ParseSessionPragmaFacts.cs` | xUnit Facts: ParseSession produces `Ast.Pragmas` populated from source — covers Wave 0 |
| `tests/test_scale_lint.flow` | Combined .flow integration smoke (LINT-01 + LINT-02 + LINT-03) |

**Installation:** No package additions. All work in source files.

**Version verification:** `npm view` not applicable (no NuGet additions). Confirmed `flow-lsp.csproj` already references `OmniSharp.Extensions.LanguageServer` — version unchanged across this phase. `[VERIFIED: file inspection]`.

## Architecture Patterns

### System Architecture Diagram

```
                            User edits .flow file in VSCode
                                          │
                                          ▼
                            VSCode → flow-lsp (didChange)
                                          │
                                          ▼
                          DocumentManager.Update(uri, text)
                                          │
                                  150ms debounce
                                          │
                                          ▼
                    ┌──────────────────────────────────────────┐
                    │          ParseSession.Parse              │
                    │  ┌─────────────────────────────────────┐ │
                    │  │ Wave 0 widen: PragmaScanner.Scan ───┼─┼─► (PragmaSet, transformedSource)
                    │  └─────────────────────────────────────┘ │
                    │                  │                       │
                    │                  ▼                       │
                    │       SimpleLexer(transformedSource,     │
                    │                   er, path, pragmaSet)   │
                    │                  │                       │
                    │                  ▼                       │
                    │       Parser(tokens, er, pragmaSet)      │
                    │                  │                       │
                    │                  ▼                       │
                    │  ParseResult(Ast{Pragmas}, Tokens, Errors)
                    └──────────────────┬───────────────────────┘
                                       │
                            DocumentManager onParse callback
                                       │
                ┌──────────────────────┴────────────────────────┐
                │                                                │
                ▼                                                ▼
   IDiagnosticsPublisher.Publish                  IScaleLintPublisher.Publish
   (parse errors → "flow" Source)                 (scaleLint diagnostics → "flow.scaleLint" Source)
                │                                                │
                │                                                ▼
                │                          ┌─────────────────────────────────────────┐
                │                          │      ScaleLintAnalyzer.Analyze          │
                │                          │  ┌────────────────────────────────────┐ │
                │                          │  │ Gate: !Ast.Pragmas.Has("scaleLint")│ │
                │                          │  │   → return [] (D-19 short-circuit) │ │
                │                          │  └────────────────────────────────────┘ │
                │                          │                  │                      │
                │                          │                  ▼                      │
                │                          │  Walk Program.Statements →              │
                │                          │  find MusicalContextStatement(Key)      │
                │                          │  blocks → recurse into Body for         │
                │                          │  NoteStreamExpression nodes             │
                │                          │                  │                      │
                │                          │                  ▼                      │
                │                          │  For each note element:                 │
                │                          │  1. Compute element source offset       │
                │                          │  2. NoteStreamContext.FindEnclosingKey  │
                │                          │     → innermost key name (D-21)         │
                │                          │  3. ScaleDatabase.TryParseKeyWithMode   │
                │                          │     → (root, Mode) | silent if fail     │
                │                          │       (D-22)                            │
                │                          │  4. DiatonicSpellings.GetDiatonicSpellings(root, mode)
                │                          │     → 7-string set                      │
                │                          │  5. Spelling-aware membership check     │
                │                          │     (D-01); compute alternatives        │
                │                          │     (lower-first ordering, see Patterns)│
                │                          │  6. Find matching token by Location;    │
                │                          │     build Range from Token.Text.Length  │
                │                          │     (D-17); message uses                │
                │                          │     Token.OriginalText (D-15 from P21)  │
                │                          │  7. Emit Diagnostic with                │
                │                          │     Source="flow.scaleLint" (D-18)      │
                │                          └─────────────────────────────────────────┘
                │                                                │
                ▼                                                ▼
        publishDiagnostics                              publishDiagnostics
        (cleared if empty)                              (cleared if empty)
                │                                                │
                └────────────────────────┬───────────────────────┘
                                         ▼
                                    VSCode editor
                                  (squiggles render)
```

### Recommended Project Structure

```
flow-lsp/
  Diagnostics/                  # NEW directory in this phase
    DiatonicSpellings.cs        # 30-key × 7-mode hardcoded map (D-04, D-05)
    ScaleLintAnalyzer.cs        # AST traversal + Diagnostic emission
    IScaleLintPublisher.cs      # Interface mirroring IDiagnosticsPublisher
    ScaleLintPublisher.cs       # OmniSharp facade impl
  ParseSession.cs               # MODIFIED — add PragmaScanner.Scan stage (Wave 0)
  Program.cs                    # MODIFIED — DI registration + onParse callback wires analyzer
flow-lang/
  Lexing/
    PragmaRegistry.cs           # MODIFIED — add ["scaleLint"] = "..." to KnownPragmas
flow-lang.Tests/Unit/Phase24/   # NEW directory
  ScaleLintAnalyzerFacts.cs
  DiatonicSpellingsFacts.cs
  PragmaRegistryScaleLintFacts.cs
  ParseSessionPragmaFacts.cs
tests/
  test_scale_lint.flow          # NEW integration smoke
```

### Pattern 1: Sibling Publisher (PIPELINE INTEGRATION RECOMMENDATION — discretion item #1)

**What:** Introduce `IScaleLintPublisher` mirroring `IDiagnosticsPublisher`. Both publish into the SAME LSP `publishDiagnostics` URI but use different `Source` strings (`"flow"` vs `"flow.scaleLint"`). Caveat: per LSP semantics, multiple `publishDiagnostics` calls REPLACE, not merge — so the implementation must aggregate parse-error and scale-lint diagnostics into a SINGLE publish per URI per parse cycle. The "sibling publisher" is therefore a logical separation, not a wire-level separation.

**When to use:** Whenever the LSP needs to emit diagnostics that belong to a different conceptual layer (here: opt-in static analysis vs. always-on parse errors).

**Why sibling over the alternatives:**

| Option | Pros | Cons | Verdict |
|--------|------|------|---------|
| **Extend `DiagnosticsPublisher.Publish` to accept a second `IReadOnlyList<Diagnostic>`** | Localized change; one publish call | Couples the parse-error type (`FlowError`) and the LSP-native type (`Diagnostic`) into one method signature; analyzer would need to construct `FlowError` instances anyway, defeating D-18's `Source="flow.scaleLint"` distinction | REJECT — leaks LSP-native types into the existing `FlowError` channel |
| **Widen `FlowError` with a `Source` field** | Single channel, single publisher | Pollutes `flow-lang` (`FlowError` lives in `flow-lang/Diagnostics/`) — violates D-04 "zero flow-lang touch beyond one PragmaRegistry line" | **REJECT — breaks D-04** |
| **Sibling `IScaleLintPublisher`** | Zero `flow-lang` change; analyzer constructs `Diagnostic` directly per D-18; mockable for tests; same shape as existing `IDiagnosticsPublisher` | Two interfaces injected at same call site; merge-into-one-publish responsibility lives in the call site (the `DocumentManager` onParse callback) | **RECOMMENDED** |

**Wire-level merge requirement:** Since LSP `publishDiagnostics` REPLACES per-URI, the onParse callback at `flow-lsp/Program.cs:40-52` must compose:
```
var parseDiagnostics = DiagnosticsPublisher.BuildDiagnostics(result.Errors);
var scaleLintDiagnostics = analyzer.Analyze(result.Ast, result.Tokens, text);
// Single publish with the union — keeps both visible simultaneously
combinedPublisher.Publish(uri, parseDiagnostics, scaleLintDiagnostics);
```
Implementation: have `ScaleLintPublisher` accept the parse-error diagnostics list as input rather than re-running BuildDiagnostics, OR introduce a `CombinedDiagnosticsPublisher` that owns the single PublishDiagnostics call. Plan must pick one — recommendation: a `CombinedDiagnosticsPublisher` orchestrator owns the single `_server.TextDocument.PublishDiagnostics` call; `IDiagnosticsPublisher` and `IScaleLintPublisher` become "diagnostic source" interfaces that produce diagnostic lists rather than publishing them. This is a refactor of the existing pipeline but keeps responsibilities clean and gives the analyzer one easy mock injection point in tests.

**Example:**
```csharp
// flow-lsp/Diagnostics/IScaleLintPublisher.cs
public interface IScaleLintPublisher
{
    IReadOnlyList<Diagnostic> Analyze(ParseResult result, string source);
}

// flow-lsp/Diagnostics/ScaleLintPublisher.cs
public sealed class ScaleLintPublisher : IScaleLintPublisher
{
    public IReadOnlyList<Diagnostic> Analyze(ParseResult result, string source)
    {
        // D-19 short-circuit
        if (!result.Ast.Pragmas.Has("scaleLint"))
            return Array.Empty<Diagnostic>();
        return ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, source);
    }
}
```

`[VERIFIED: flow-lsp/Handlers/DiagnosticsPublisher.cs:14-59 inspected; LSP publishDiagnostics REPLACE semantics is the documented OmniSharp wire behavior — confirmed by existing comment at line 52: "MUST publish even when empty — that is how LSP clears prior markers."]`

### Pattern 2: Hardcoded Diatonic Spellings (DERIVATION STRATEGY — discretion item #2)

**What:** A static map keyed by `(root, mode)` returning a fixed `IReadOnlyList<string>` of 7 spelling strings.

**When to use:** Closed-set domain where readability + auditability beat code-golf algorithm. Matches the Phase 23 `TuningTables.cs` precedent which uses 14 hardcoded `ChromaticRatioTable.Build` calls (Just × 7 modes + Pythagorean × 7 modes) at `flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs:72-188` `[VERIFIED]`.

**Why hardcoded over circle-of-fifths algorithm:**

| Option | Pros | Cons | Verdict |
|--------|------|------|---------|
| **30-key × 7-mode hardcoded map (210 lines of literal data)** | Auditable at a glance — composer can scan and verify Eb major's diatonic set is `{Eb, F, G, Ab, Bb, C, D}`; matches existing `TuningTables` style; trivial to test (one Fact per key); zero algorithm-correctness risk | Bulkier source file; new modes/keys require manual addition | **RECOMMENDED — explicit beats clever for closed sets** |
| **Circle-of-fifths algorithm (~30 lines)** | Smaller; generalizes to non-church modes if Phase 24+ scope grows | Algorithm-correctness risk for edge cases (e.g., F# major has E# at the 7th, not F natural — easy to get wrong); review burden shifts from "scan the map" to "trust the algorithm"; tests would still need to pin every (root, mode) anyway, so test surface unchanged | REJECT — saves ~150 lines but adds bug surface |

**Coverage:** "30 keys" = 12 chromatic root names actually-spelled (C, C#, Db, D, D#, Eb, E, F, F#, Gb, G, G#, Ab, A, A#, Bb, B = 17, but only the 15 "musically standard" key signatures of the circle of fifths plus 2 enharmonic doubles ship). For 7 modes × ~15 musical roots = ~105 entries. Some keys (e.g., G# major with 8 sharps including Fx) are theoretical edge cases — recommendation: ship the 12 + 3 = 15 roots that `ScaleDatabase.NoteToSemitone` already accepts (`flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs:33-42`: C, Csharp, Db, D, Dsharp, Eb, E, F, Fsharp, Gb, G, Gsharp, Ab, A, Asharp, Bb, B = 17 entries) `[VERIFIED]`. With 7 modes that gives 119 entries. CONTEXT calls it "30-key" loosely — interpretation is "all roots ScaleDatabase accepts × all 7 modes" = 119 entries. Plan-time decision: ship exactly those 119 entries to match the `TryParseKeyWithMode` accept-set.

**Example:**
```csharp
// flow-lsp/Diagnostics/DiatonicSpellings.cs
internal static class DiatonicSpellings
{
    private static readonly Dictionary<(string Root, Mode Mode), string[]> Map = new()
    {
        // C major: C D E F G A B
        [("C", Mode.Major)]      = new[] { "C", "D", "E", "F", "G", "A", "B" },
        [("C", Mode.Minor)]      = new[] { "C", "D", "Eb", "F", "G", "Ab", "Bb" },
        [("C", Mode.Dorian)]     = new[] { "C", "D", "Eb", "F", "G", "A", "Bb" },
        [("C", Mode.Phrygian)]   = new[] { "C", "Db", "Eb", "F", "G", "Ab", "Bb" },
        [("C", Mode.Lydian)]     = new[] { "C", "D", "E", "F#", "G", "A", "B" },
        [("C", Mode.Mixolydian)] = new[] { "C", "D", "E", "F", "G", "A", "Bb" },
        [("C", Mode.Locrian)]    = new[] { "C", "Db", "Eb", "F", "Gb", "Ab", "Bb" },
        // F major: F G A Bb C D E (the canonical b̂7 = Bb spelling — A# would flag in F major)
        [("F", Mode.Major)]      = new[] { "F", "G", "A", "Bb", "C", "D", "E" },
        // E dorian: E F# G A B C# D (D-05 example case)
        [("E", Mode.Dorian)]     = new[] { "E", "F#", "G", "A", "B", "C#", "D" },
        // ... 110 more entries ...
    };

    /// <summary>
    /// D-05: returns the 7 letter+accidental strings that are diatonic in the given key/mode.
    /// Spelling-aware (D-01): in C major, "F#" and "E#" are BOTH not in the set even though
    /// "E#" sounds the same as "F" (which IS in the set). Returns null when the (root, mode)
    /// pair is not in the closed set — analyzer treats null as "silent fail-open" per D-22.
    /// </summary>
    public static IReadOnlySet<string>? GetDiatonicSpellings(string root, Mode mode) =>
        Map.TryGetValue((root, mode), out var arr)
            ? new HashSet<string>(arr, StringComparer.Ordinal)
            : null;
}
```

`[CITED: TuningTables hardcoded precedent at flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs:72-188 — 14 tables. Phase 24 follows the same pattern but at the spelling-set layer.]`

### Pattern 3: Per-Mode Acceptance Tests in xUnit + Single Combined .flow Smoke (TEST PLACEMENT + COVERAGE — discretion items #3 and #4)

**What:** Use existing `flow-lang.Tests/Unit/Phase24/` directory (mirroring Phase 17 `Unit/Phase17/Lsp*Tests.cs` convention `[VERIFIED]`) for fine-grained xUnit Facts. Use a single `tests/test_scale_lint.flow` for end-to-end integration smoke pinned to LINT-01/02/03 verbatim acceptance text.

**When to use:** Existing test convention; no new project boundary needed.

**Why this split:**

| Option | Pros | Cons | Verdict |
|--------|------|------|---------|
| **Tests under `flow-lang.Tests/Unit/Phase24/`** | Mirrors existing Phase 17 (LSP-specific Facts), Phase 21 (PragmaRegistry/PragmaScanner Facts), Phase 23 (PragmaTuning/SpellingAware Facts) conventions; csproj already references both flow-lang and flow-lsp at `flow-lang.Tests/flow-lang.Tests.csproj:19-20` `[VERIFIED]` | None — convention is locked | **RECOMMENDED** |
| **New `flow-lsp.Tests/` project** | Notional separation of LSP from interpreter tests | Phase 17's 19 LSP test files all live under `Unit/Phase17/`; introducing a new project breaks the precedent and adds a fourth project to the solution for no benefit | REJECT — gratuitous |

**Per-mode coverage:**

| Option | Pros | Cons | Verdict |
|--------|------|------|---------|
| **7 separate `tests/test_scale_lint_<mode>.flow` files** | Each mode gets a clear isolated smoke | 7 small files multiplied by future similar phases creates test-script bloat; integration loop runs all of them anyway | REJECT — repetitive |
| **Single combined `tests/test_scale_lint.flow` + xUnit per-mode Facts** | One smoke file pins LINT-01/02/03 verbatim; xUnit per-mode coverage is dense, fast, and lives in code, not text scripts | None — best of both worlds | **RECOMMENDED** |

**Example xUnit Facts (matching Phase 23 `SpellingAwareTuningFacts.cs` style):**
```csharp
// flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerFacts.cs
[Fact]
public void NonDiatonic_FsharpInCmajor_FlagsOneDiagnostic()
{
    var src = "enable scaleLint;\nkey Cmajor { | C4 D4 E4 F#4 G4 | }";
    var result = LspFixtures.Parse(src);  // assumes Wave 0 ParseSession widening done
    var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
    Assert.Single(diags);
    Assert.Equal(DiagnosticSeverity.Information, diags[0].Severity);
    Assert.Equal("flow.scaleLint", diags[0].Source);
    Assert.Contains("F#4 not diatonic in Cmajor", diags[0].Message);
}

[Fact]
public void SpellingAware_EsharpInCmajor_Flags_PitchClassMatchHint()
{
    var src = "enable scaleLint;\nkey Cmajor { | E#4 | }";
    var result = LspFixtures.Parse(src);
    var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
    Assert.Single(diags);
    Assert.Contains("pitch-class matches F", diags[0].Message);
}

[Fact]
public void NestedKeys_InnermostWins_NoFlag()
{
    // F#4 IS diatonic in Gmajor (the inner key) — D-21 says inner key wins
    var src = "enable scaleLint;\nkey Cmajor { key Gmajor { | F#4 | } }";
    var result = LspFixtures.Parse(src);
    var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
    Assert.Empty(diags);
}

[Fact]
public void PragmaAbsent_NeverFlags_LINT02()
{
    var src = "key Cmajor { | C4 D4 E4 F#4 G4 | }";  // no enable scaleLint;
    var result = LspFixtures.Parse(src);
    var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
    Assert.Empty(diags);
}

[Theory]
[InlineData("Cmajor", "F#4")]
[InlineData("Aminor", "G#4")]
[InlineData("Dorian".Replace("Dorian", "Edorian"), "F4")]  // Edorian has F# diatonic; F natural flags
// ... per-mode smokes ...
public void EachMode_FlagsExpectedNonDiatonic(string keyName, string nonDiatonicNote) { /* ... */ }
```

`[VERIFIED: flow-lang.Tests/Unit/Phase17/NoteStreamContextTests.cs:14-46 — `LspFixtures.Parse` pattern; flow-lang.Tests/Unit/Phase23/SpellingAwareTuningFacts.cs — Theory/InlineData not used there but standard xUnit pattern.]`

### Pattern 4: Lower-Pitch-First Alternative Ordering (SUGGESTION ORDERING — discretion item #5)

**What:** When a non-diatonic note is exactly midway between two diatonic neighbors (e.g., `F#` in Cmajor sits between F and G, both 1 semitone away), the alternatives in the diagnostic message are ordered **lower-first**: `"F#4 not diatonic in Cmajor (try F4 or G4)"`.

**When to use:** Whenever D-16's standard branch fires.

**Why lower-first over alternatives:**

| Option | Pros | Cons | Verdict |
|--------|------|------|---------|
| **Lower-first (F4 or G4)** | Deterministic; matches musical reading order (low → high in score notation); test assertion is unambiguous | None | **RECOMMENDED** |
| **Nearest-first** | When note is closer to one neighbor (asymmetric), preferred-resolution intuition | Requires tie-breaking when distance is equal — collapses back to lower-first or alphabetical | REJECT — degenerates |
| **Preferred-resolution-direction** (e.g., F#4 → G4 because chromatic-leading-tone-up is common) | Music-theory-correct in some idioms | Idiom-specific (jazz vs classical disagree); over-engineered for an Information-severity hint | REJECT — too clever |

**Algorithm:**
1. Compute note's MIDI pitch.
2. Find diatonic note in the active mode whose MIDI pitch is the largest value < the non-diatonic pitch (lower neighbor).
3. Find diatonic note whose MIDI pitch is the smallest value > the non-diatonic pitch (upper neighbor).
4. Format as `"(try {lower} or {upper})"` — always in this order.
5. If only one neighbor exists (note is at scale boundary, rare), output `"(try {neighbor})"`.
6. For the spelling-aware-pitch-class-match branch (e.g., `E#4` in Cmajor), the message specifically suggests the pitch-class-equivalent in-scale note: `"(try F4)"` — only one suggestion. Per D-16 verbatim.

### Pattern 5: Always-Run on Partial Parses (PARTIAL PARSE BEHAVIOR — discretion item #6)

**What:** The analyzer runs on every parse cycle, including partial parses where `ParseResult.Errors` is non-empty.

**Why:**
- The soft-failure error model documented at `flow-lsp/NoteStream/NoteStreamContext.cs:11-20` `[VERIFIED]` and CLAUDE.md §Key Design Decisions ("Error accumulation: ErrorReporter collects errors rather than throwing") means the AST is mostly complete even when there are parse errors. Composers actively typing benefit from continuous lint feedback.
- The analyzer already null-checks element types (D-12/D-13/D-14 SKIP rules use type-pattern matching), so a malformed `NoteStreamElement` simply doesn't match any branch and emits no diagnostic — fail-safe.
- D-22 already specifies "silent fail-open" for unknown keys, which generalizes to "silent fail-open for any AST imperfection".

**When NOT to run:** Never. The analyzer is pure read-only over the AST and tokens; it cannot crash the LSP server.

### Pattern 6: No Per-Key Caching (CACHING DECISION — discretion item #7)

**What:** Recompute `DiatonicSpellings.GetDiatonicSpellings(root, mode)` on every analyzer invocation, even when the user types in the same key block repeatedly.

**Why:**
- The set is 7 strings; lookup in a static `Dictionary` is O(1).
- The 150ms `didChange` debounce already bounds analyzer-call frequency to ≤7Hz per active document.
- Caching introduces invalidation complexity (when does the cache flush — file save? key change? text edit?) that has no measurable performance payoff at this size.
- `[VERIFIED: flow-lsp/Handlers/TextDocumentSyncHandler.cs:53-60 — full sync per D-03; DocumentManager.Update is the only per-keystroke entry point]`. No hot path that would benefit.

**Default: don't cache.** Confirmed by inspection. If a future profile shows lint as a measurable bottleneck on very large files, the cache lives in `DiatonicSpellings.cs` (one method) and adds 30 dictionary entries. YAGNI now.

### Anti-Patterns to Avoid

- **DON'T parse the key name yourself** — call `ScaleDatabase.TryParseKeyWithMode` per D-02. Two separate parse paths will drift `[VERIFIED: flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs:207]`.
- **DON'T modify `LspMappings.ToRange`** — the existing 1-character default at `flow-lsp/LspMappings.cs:21-26` is correct for parse errors `[VERIFIED]`. The analyzer per D-17 builds its own `Range` using `Token.Text.Length`.
- **DON'T build `FlowError` instances and route through `IDiagnosticsPublisher`** — D-18 requires `Source="flow.scaleLint"` which the existing `BuildDiagnostics` hardcodes to `"flow"` at line 42 `[VERIFIED]`. Construct `Diagnostic` directly.
- **DON'T re-parse the source in the analyzer** — `ParseResult.Tokens` is already in hand; re-instantiating `SimpleLexer` requires an `ErrorReporter` and duplicates work `[VERIFIED: NoteStreamContext.cs:18-22 makes the same point]`.
- **DON'T let the analyzer emit on Roman numerals or named chord literals** — D-11/D-12 SKIP. Composers writing `Bbmaj7` in Cmajor are deliberately reaching for it; flagging it would clobber editors per CONTEXT specifics line 169.
- **DON'T add a `flow-lsp` dependency on `Microsoft.Extensions.DependencyInjection` beyond what's already there** — Phase 17 D-01 already wires DI; reuse the existing pattern at `flow-lsp/Program.cs:19-29`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Pragma activation gate | Custom string-search of source | `parseResult.Ast.Pragmas.Has("scaleLint")` | Already populated by `PragmaScanner.Scan` once `ParseSession` is widened; reading `Has` is O(1); D-19 verbatim |
| Mode parsing | New mode-suffix detector | `ScaleDatabase.TryParseKeyWithMode` | Already handles longer-suffix-first ordering (`mixolydian` before `lydian`); `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs:207-236` |
| Innermost-key resolution at offset | Re-implement brace-depth tracking | `NoteStreamContext.FindEnclosingKey` | Phase 17 D-11 shipped; handles cursor-after-closed-block and brace-depth-tracking; `flow-lsp/NoteStream/NoteStreamContext.cs:43` (D-21 verbatim) |
| LSP severity mapping | Hardcoded ints for `DiagnosticSeverity` | `LspMappings.ToSeverity(DiagnosticLevel.Info)` | Already at `flow-lsp/LspMappings.cs:28-34` |
| Original-spelling preservation through H→B | New token field | `Token.OriginalText` / `Token.DiagnosticText` | Phase 21 D-15 already shipped; `flow-lang/Lexing/Token.cs:24-32` |
| 1-based → 0-based source location math | New conversion utility | Reuse `LspMappings.ToRange` math pattern (`Math.Max(0, loc.Line - 1)`, etc.) | Already in `flow-lsp/LspMappings.cs:21-26` |
| Note-letter / accidental extraction from `NoteName` string | Custom regex on note name | Build a small parser inline (small enough to inline, but follow `ChordParser` pattern at `flow-lang/StandardLibrary/Harmony/ChordParser.cs`) | The note-element NoteName format ("C4", "D#5", "Ebb3") is documented at `flow-lang/Ast/Expressions/NoteStreamExpression.cs:17`; spelling extraction needs letter + accidental, dropping the octave — write a small static helper in `DiatonicSpellings.cs` |

**Key insight:** Phase 24 is unique in that ALL of the heavy lifting was done by prior phases. The analyzer is a pure orchestration layer wiring existing primitives.

## Runtime State Inventory

> Greenfield phase — all new code lives in flow-lsp under new files, with one purely-additive entry to `PragmaRegistry.KnownPragmas`. The existing test `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs:28` has a negative assertion `Assert.False(PragmaRegistry.IsKnown("scaleLint"))` that will need to be updated when the registry adds the entry. This is the ONLY pre-existing runtime state Phase 24 changes.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — no databases, no on-disk caches | None |
| Live service config | None — flow-lsp is invoked per-file via stdio | None |
| OS-registered state | None | None |
| Secrets/env vars | None | None |
| Build artifacts | None — adding source files only | None |
| **Test pin migration** | `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs:28` asserts `Assert.False(PragmaRegistry.IsKnown("scaleLint"))` | **Update this Fact** — the `scaleLint` literal must be replaced with another sentinel unknown name (e.g., `"futureUnknownPragma"`) so the negative-assertion intent survives. This mirrors the migration done at line 27 when Phase 23 added `justIntonation`. |

## Common Pitfalls

### Pitfall 1: ParseSession is missing the PragmaScanner stage (PLANNING-CRITICAL)

**What goes wrong:** D-19 says the analyzer reads `parseResult.Ast.Pragmas.Has("scaleLint")`. But `flow-lsp/ParseSession.cs:22` currently calls `new Parser(tokens, er)` (2-arg overload) without ever running `PragmaScanner.Scan(source, ...)`. The 2-arg `Parser` ctor defaults `pragmaSet` to `PragmaSet.Empty` `[VERIFIED: flow-lang/Parsing/Parser.cs:33-37]`. Result: `Program.Pragmas` is always `PragmaSet.Empty` in the LSP, regardless of what the file actually declares. The analyzer's D-19 short-circuit ALWAYS fires, and lint never runs.

**Why it happens:** Phase 17 shipped `ParseSession` before Phase 21 added pragmas. Phase 23 didn't need to touch `flow-lsp/ParseSession.cs` because tuning pragmas affect rendering, not parsing or LSP analysis. Phase 24 is the first phase where the LSP must observe a pragma's presence.

**How to avoid:** **Wave 0 task:** widen `ParseSession.Parse` to mirror `FlowEngine.Run()` lines 66-82 `[VERIFIED]`:

```csharp
// flow-lsp/ParseSession.cs (Wave 0 widen)
public ParseResult Parse(string source, string? path)
{
    var er = new ErrorReporter();
    var (pragmaSet, transformedSource) = PragmaScanner.Scan(source, path, er);
    var tokens = new SimpleLexer(transformedSource, er, path, pragmaSet).Tokenize();
    var ast = new Parser(tokens, er, pragmaSet).Parse();
    return new ParseResult(ast, tokens, er.Errors.ToList());
}
```

**Warning signs:** Any test that does `Assert.True(parseResult.Ast.Pragmas.Has(...))` against `LspFixtures.Parse(...)` output WITHOUT the Wave 0 widen will fail. The existing `flow-lang.Tests/Unit/Phase17/` tests don't exercise pragmas, so this latent bug hasn't surfaced. Phase 24 forces it.

**Side effect (positive):** Wave 0 widening is also the correct fix for `enable hAsB;` in LSP-edited files — currently the LSP doesn't honor `enable hAsB;` either, so a file declaring `enable hAsB; ... | H4q |` would show a diagnostic-source spurious "unknown identifier H4q" in the editor today. Wave 0 fixes that quietly. Add an xUnit Fact `ParseSession_EnableHAsB_LexesH4qAsNote` to pin this regression in Phase 24.

### Pitfall 2: PragmaRegistryFacts negative assertion will break

**What goes wrong:** `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs:28` asserts `Assert.False(PragmaRegistry.IsKnown("scaleLint"))` — meant to be a placeholder reminding devs that scaleLint is a future Phase 24 entry `[VERIFIED]`. The same file's `AlphabetizedKnownNames_ReturnsCsvSorted` Fact at line 39 currently expects exactly `"equalTemperament, hAsB, justIntonation, pythagorean"` — adding `scaleLint` reorders the CSV.

**Why it happens:** Closed-set growth pattern; explicitly anticipated by Phase 23 D-08 comment.

**How to avoid:**
- Replace line 28's `"scaleLint"` literal with a sentinel like `"futureUnknownPragma"` (mirrors the migration done for `justIntonation` per the comment at line 25-27).
- Update line 39's expected CSV to include `scaleLint` in correctly-sorted position. Ordinal sort puts `scaleLint` between `pythagorean` and the existing entries: `"equalTemperament, hAsB, justIntonation, pythagorean, scaleLint"` (`p` < `s`).
- `flow-lang.Tests/Unit/Phase23/PragmaTuningFacts.cs:49` says count `>= 4` (upper-unconstrained per WARNING-3 comment); Phase 24 adds one more so count becomes 5. No assertion update needed there.

**Warning signs:** `dotnet test` red on `PragmaRegistryFacts.AlphabetizedKnownNames_ReturnsCsvSorted` after the one-line `PragmaRegistry.cs` add.

### Pitfall 3: Token-to-element matching by Location

**What goes wrong:** D-17 says "the analyzer walks `ParseResult.Tokens` to find the `NoteLiteral` token whose `Token.Location` matches the offending `NoteElement.Location`". `NoteElement.Location` is a `SourceLocation(Line, Column)` `[VERIFIED: flow-lang/Core/SourceLocation.cs:6]`. But there can be MULTIPLE `NoteLiteral` tokens at the same line (e.g., `| C4 D4 E4 |`). The match must be on (Line, Column) BOTH, not Line alone.

**Why it happens:** Looking up "the token at line X" rather than "the token at exact (line, column)" gives false positives.

**How to avoid:** Use full `SourceLocation` equality (or Line+Column tuple match). Build a `Dictionary<SourceLocation, Token>` once per `Analyze` call if the analyzer needs many lookups, or just iterate `Tokens` linearly — the lists are small.

**Warning signs:** Test like "two non-diatonic notes on the same line both flagged with correct ranges" — if the second one points at the wrong column, the lookup is line-only.

### Pitfall 4: Cent offsets must not flag

**What goes wrong:** D-08 says diatonicity decided by base note. But `NoteElement.NoteName` already excludes the cent suffix `[VERIFIED: flow-lang/Ast/Expressions/NoteStreamExpression.cs:17 — NoteName is "C4", "D#5", "Ebb3" without cents]`; the cents live in a separate `CentOffset` field on the same record. So if the analyzer keys off `NoteName` only, cents are correctly excluded by construction. Watch out: a future change that conflated them would silently regress this.

**How to avoid:** Use `NoteElement.NoteName` (already cent-stripped). Never read `CentOffset` for diatonicity. Pin a Fact: `E4plus50c_InCmajor_Silent` and `Ebplus50c_InCmajor_FlagsBaseSpelling`.

### Pitfall 5: NoteStreamContext.FindEnclosingKey takes a Position, not an offset

**What goes wrong:** `FindEnclosingKey(ast, tokens, source, Position cursor)` `[VERIFIED: flow-lsp/NoteStream/NoteStreamContext.cs:43-48]`. The analyzer iterates AST elements and gets `SourceLocation(Line, Column)` per element — these need conversion to `OmniSharp.LanguageServer.Protocol.Models.Position(line, char)` (0-based). Phase 17 already does the 1-based-to-0-based math at `LspMappings.cs:23-25` `[VERIFIED]`.

**How to avoid:** Reuse the `Math.Max(0, loc.Line - 1)` / `Math.Max(0, loc.Column - 1)` pattern. Don't write a new conversion helper.

### Pitfall 6: Empty publish must still happen when analyzer returns empty diagnostic list

**What goes wrong:** When the user removes a non-diatonic note, the analyzer returns `[]`. If the publisher only calls `PublishDiagnostics` when the list is non-empty, the previous squiggle stays on screen.

**Why it happens:** `flow-lsp/Handlers/DiagnosticsPublisher.cs:52` already documents this rule for parse errors. New publisher MUST follow the same contract.

**How to avoid:** Test: `RemovingNonDiatonicNote_ClearsDiagnostic`. Assert `_server.TextDocument.PublishDiagnostics` was called with empty `Container<Diagnostic>` after the edit.

## Code Examples

### Pragma Registry One-Line Add

```csharp
// flow-lang/Lexing/PragmaRegistry.cs:16 — modify ONLY the dictionary literal
public static readonly IReadOnlyDictionary<string, string> KnownPragmas =
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["hAsB"] = "Inside note streams, accept 'H' as a synonym for 'B' (German notation).",
        ["justIntonation"] = "5-limit just-intonation render-time tuning rooted at active key tonic (default C major).",
        ["pythagorean"] = "3-limit Pythagorean (chain-of-fifths) render-time tuning rooted at active key tonic.",
        ["equalTemperament"] = "12-tone equal temperament (default). Explicit form for tooling-visible intent.",
        ["scaleLint"] = "Inside `key { ... }` blocks, surface non-diatonic notes as Information-severity LSP diagnostics."  // ← NEW
    };
```
`[Source: flow-lang/Lexing/PragmaRegistry.cs:11-23 inspected; new entry follows existing one-line description style verbatim per CONTEXT specifics line 173]`

### ScaleLintAnalyzer Skeleton

```csharp
// flow-lsp/Diagnostics/ScaleLintAnalyzer.cs (NEW)
using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Lexing;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.StandardLibrary.Harmony;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using FlowProgram = FlowLang.Ast.Program;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace FlowLsp.Diagnostics;

internal static class ScaleLintAnalyzer
{
    public static IReadOnlyList<Diagnostic> Analyze(
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source)
    {
        // D-19 short-circuit — opt-in only
        if (!ast.Pragmas.Has("scaleLint"))
            return Array.Empty<Diagnostic>();

        var diagnostics = new List<Diagnostic>();
        WalkStatements(ast.Statements, ast, tokens, source, diagnostics);
        return diagnostics;
    }

    private static void WalkStatements(
        IReadOnlyList<Statement> stmts,
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source,
        List<Diagnostic> diagnostics)
    {
        foreach (var stmt in stmts)
        {
            switch (stmt)
            {
                case MusicalContextStatement m:
                    WalkStatements(m.Body, ast, tokens, source, diagnostics);
                    break;
                case ProcDeclaration p:
                    WalkStatements(p.Body, ast, tokens, source, diagnostics);
                    break;
                case SectionDeclaration s:
                    WalkStatements(s.Body, ast, tokens, source, diagnostics);
                    break;
                case ExpressionStatement es when es.Expression is NoteStreamExpression ns:
                    WalkNoteStream(ns, ast, tokens, source, diagnostics);
                    break;
                case VariableDeclaration vd when vd.Value is NoteStreamExpression ns:
                    WalkNoteStream(ns, ast, tokens, source, diagnostics);
                    break;
            }
        }
    }

    private static void WalkNoteStream(
        NoteStreamExpression ns,
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source,
        List<Diagnostic> diagnostics)
    {
        foreach (var bar in ns.Bars)
            foreach (var elem in bar.Elements)
                CheckElement(elem, ast, tokens, source, diagnostics);
    }

    private static void CheckElement(
        NoteStreamElement elem,
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source,
        List<Diagnostic> diagnostics)
    {
        switch (elem)
        {
            // D-06: NoteElement always checked
            case NoteElement n:
                CheckNote(n.NoteName, n.Location, ast, tokens, source, diagnostics);
                break;
            // D-07: ChordElement recursed
            case ChordElement c:
                foreach (var note in c.Notes)
                    CheckNote(note, c.Location, ast, tokens, source, diagnostics);
                break;
            // D-09: RandomChoiceElement recursed
            case RandomChoiceElement r:
                foreach (var (note, _) in r.Choices)
                    CheckNote(note, r.Location, ast, tokens, source, diagnostics);
                break;
            // D-10: TupletElement recursed (incl. nested)
            case TupletElement t:
                foreach (var child in t.Children)
                    CheckElement(child, ast, tokens, source, diagnostics);
                break;
            // D-11/D-12/D-13/D-14 SKIP: RomanNumeralElement, NamedChordElement,
            // VariableReferenceElement, RestElement — no case branch, nothing emitted
        }
    }

    private static void CheckNote(
        string noteName,
        FlowLang.Core.SourceLocation loc,
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source,
        List<Diagnostic> diagnostics)
    {
        // D-21: innermost-key resolution via NoteStreamContext
        var pos = new Position(Math.Max(0, loc.Line - 1), Math.Max(0, loc.Column - 1));
        var keyName = NoteStream.NoteStreamContext.FindEnclosingKey(ast, tokens, source, pos);
        if (keyName is null) return;  // D-15: no enclosing key, silent

        // D-02: parse key+mode
        if (!ScaleDatabase.TryParseKeyWithMode(keyName, out var root, out var mode))
            return;  // D-22: silent fail-open on unparseable key

        // D-04/D-05: 7-string diatonic spelling set
        var spellings = DiatonicSpellings.GetDiatonicSpellings(root!, mode);
        if (spellings is null) return;  // unknown (root, mode) — silent fail-open

        // D-01: spelling-aware membership check
        var spelling = ExtractSpelling(noteName);  // strips octave, returns letter+accidental
        if (spellings.Contains(spelling)) return;  // diatonic — no diagnostic

        // Build Diagnostic: Range from token width (D-17), Source "flow.scaleLint" (D-18),
        // Severity Information, Message via D-16 branches
        var diag = BuildDiagnostic(noteName, loc, keyName, root!, mode, spellings, tokens);
        diagnostics.Add(diag);
    }

    // ... ExtractSpelling, BuildDiagnostic, FindAlternatives helpers ...
}
```
`[Source: traversal pattern derived from NoteStreamContext.cs:185-210 (existing AST walk style); FindEnclosingKey signature inspected at flow-lsp/NoteStream/NoteStreamContext.cs:43; element types from flow-lang/Ast/Expressions/NoteStreamExpression.cs:9-152]`

### Wave 0 — ParseSession Widen

```csharp
// flow-lsp/ParseSession.cs (MODIFIED — Wave 0)
public ParseResult Parse(string source, string? path)
{
    var er = new ErrorReporter();
    // Wave 0: mirror FlowEngine.Run() pragma-scan-then-parse pipeline so
    // Program.Pragmas is populated for the LSP. Required precondition for
    // Phase 24 D-19 activation gate.
    var (pragmaSet, transformedSource) = PragmaScanner.Scan(source, path, er);
    var tokens = new SimpleLexer(transformedSource, er, path, pragmaSet).Tokenize();
    var ast = new Parser(tokens, er, pragmaSet).Parse();
    return new ParseResult(ast, tokens, er.Errors.ToList());
}
```
`[Source: existing FlowEngine.Run() pipeline at flow-lang/Core/FlowEngine.cs:66-82; SimpleLexer 4-arg ctor at flow-lang/Lexing/SimpleLexer.cs:23-24; Parser 3-arg ctor at flow-lang/Parsing/Parser.cs:33]`

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| LSP analyzers re-implementing the parser | flow-lsp consumes `flow-lang.Parser` directly via `ParseSession` | Phase 17 D-01 (2026-04-20) | Phase 24 inherits — single source of truth for AST shape |
| Pragmas living in runtime state | `Program.Pragmas` populated at parse time | Phase 21 D-08 (2026-04-26) | Phase 24's D-19 activation gate exists |
| Spelling vs pitch-class equivalence treated identically | Spelling-aware tables key on `(Letter, Alteration)` | Phase 23 D-09 (2026-05-03) | D-01 spelling-aware lint inherits the precedent |
| Mode parsing limited to major/minor | `TryParseKeyWithMode` recognizes 5 church modes | Phase 23 D-04 (2026-05-03) | D-02 7-mode support is a free reuse |

**Deprecated/outdated (none for this phase):**
- All required precursors shipped in Phases 17, 21, 23. No deprecated APIs to migrate.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The CONTEXT.md "30-key" framing means "all 17 roots in `ScaleDatabase.NoteToSemitone` × 7 modes = 119 entries" rather than the literal 30 | Pattern 2 §Coverage | If the planner reads "30-key" as exactly 30 (e.g., 15 sharps + 15 flats), some roots get omitted; rare keys like `Asharp dorian` would silently fail-open per D-22. Recommendation: confirm with user during plan-checking, or default to the 119-entry interpretation since it matches what `TryParseKeyWithMode` already accepts. `[ASSUMED]` |
| A2 | The diagnostic message format `"<note> not diatonic in <key> (try <alt1> or <alt2>)"` from D-16 implies `<note>` is the user-typed text (use `Token.OriginalText` per D-15 / D-17), not the canonical text | Pattern 4 + Code Examples | If the wrong text is used, a composer typing `H4q` under `enable hAsB; enable scaleLint;` would see `B4q not diatonic` instead of `H4q not diatonic`. CONTEXT D-17 explicitly says "composers see the spelling they typed", so this is a re-statement of D-17 — `[VERIFIED via D-17]`. |
| A3 | The combined-publish refactor (CombinedDiagnosticsPublisher) is acceptable in Phase 24 scope | Pattern 1 | If the planner prefers to NOT refactor the existing publisher and instead route both diagnostic types through `IDiagnosticsPublisher.Publish` with a widened parameter list, Pattern 1 needs to drop down to the second-best option. Recommendation: present both options in the plan and let plan-checker / user pick. `[ASSUMED]` |
| A4 | "Per-mode" coverage in xUnit means 7 separate Theory/InlineData rows or 7 Facts, not just one combined Fact | Pattern 3 | If only one combined Fact is shipped, regression in any one mode (e.g., Locrian's diminished fifth incorrectly listed as diatonic) might pass review. Recommendation: 1 Fact per mode minimum. `[ASSUMED reasonable]` |

## Open Questions (RESOLVED)

1. **Should the Wave 0 ParseSession widen ship as Plan 24-01, or as a prerequisite Plan 24-00 (Wave 0)?**
   - What we know: The widen is a precondition for D-19 to work at all.
   - What's unclear: Whether the planner treats it as a separate atomic plan or folds it into the first analyzer plan.
   - Recommendation: Separate Plan 24-01 (or 24-00) titled "Wave 0: ParseSession pragma-scan widen" so that one commit cleanly ships the precondition and a Fact pinning `EnableHAsB_Lexes_H4q_AsNote` (positive regression that flow-lsp now honors `enable hAsB;`). Subsequent plans (analyzer skeleton, spellings map, integration, smoke .flow) build on top.
   - **RESOLVED:** Separate Plan 24-00 (Wave 0). Ships ParseSession widen + `ParseSessionPragmaFacts` (using `hAsB` to avoid RED-cascade with Plan 24-01's PragmaRegistry change).

2. **Does the existing `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs:28` literal need to migrate to a sentinel string, or can it be deleted?**
   - What we know: The Fact is an intentional negative-assertion placeholder.
   - What's unclear: Whether the team prefers sentinel-replacement (preserves the test's intent) or deletion (drops a test that's now obsolete).
   - Recommendation: Sentinel-replacement (matches the prior migration done for `justIntonation` per the comment at lines 25-27).
   - **RESOLVED:** Sentinel-replacement in Plan 24-01 Step 3. Line 28 sentinel becomes `futureUnknownPragma` (preserves negative-assertion intent); line 39 CSV updates to include `scaleLint`.

3. **Is the `IsReadOnlySet<string>` vs `string[]` choice for `GetDiatonicSpellings` return type material?**
   - What we know: D-05 lists both as candidates.
   - What's unclear: Whether membership-check perf matters for 7-string sets.
   - Recommendation: `IReadOnlySet<string>` — `Contains` is O(1) regardless. Minor perf payoff; clearer intent (membership semantics, not iteration semantics).
   - **RESOLVED:** `IReadOnlySet<string>?` (nullable to encode D-22 fail-open: returns `null` for unrecognized modes). Plan 24-02 ships this signature.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All builds + tests | ✓ | net10.0 | — |
| OmniSharp.Extensions.LanguageServer | flow-lsp existing reference | ✓ | (existing in csproj) | — |
| xunit.v3 | flow-lang.Tests | ✓ | 3.2.2 | — |
| dotnet test (full suite) | Phase gate | ✓ | per .NET 10 | — |
| `tests/test_*.flow` integration loop | Per-feature smoke | ✓ | bash + `dotnet run --project flow-interpreter` | — |

**Missing dependencies with no fallback:** None.
**Missing dependencies with fallback:** None.
`[VERIFIED: ls invocations against flow-lang.Tests/flow-lang.Tests.csproj and tests/]`

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` (no separate xunit config — convention-based) |
| Quick run command | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase24"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| LINT-01 | `enable scaleLint;` activates lint; F#4 in Cmajor flagged at Information severity | unit | `dotnet test --filter "FullyQualifiedName~ScaleLintAnalyzerFacts.NonDiatonic_FsharpInCmajor_FlagsOneDiagnostic"` | ❌ Wave 0 |
| LINT-01 | Smoke: full pipeline emits diagnostic via VS Code wire | integration .flow | `dotnet run --project flow-interpreter tests/test_scale_lint.flow` (asserts via console output that `enable scaleLint;` declaration parses without error and that the same script without the pragma also runs clean — flow-interpreter doesn't have an LSP frontend, so the smoke verifies the pragma is accepted by the closed registry) | ❌ Wave 0 |
| LINT-02 | Without pragma, zero scale-lint diagnostics regardless of non-diatonic content | unit | `dotnet test --filter "FullyQualifiedName~ScaleLintAnalyzerFacts.PragmaAbsent_NeverFlags_LINT02"` | ❌ Wave 0 |
| LINT-03 | Innermost key wins (`key Cmajor { key Gmajor { \| F#4 \| } }` does not flag F#4) | unit | `dotnet test --filter "FullyQualifiedName~ScaleLintAnalyzerFacts.NestedKeys_InnermostWins_NoFlag"` | ❌ Wave 0 |
| D-01 spelling-aware | `E#4` in Cmajor → flagged with pitch-class-match hint | unit | `dotnet test --filter "FullyQualifiedName~ScaleLintAnalyzerFacts.SpellingAware_EsharpInCmajor_Flags_PitchClassMatchHint"` | ❌ Wave 0 |
| D-02 all 7 modes | Each of 7 modes flags expected non-diatonic note | unit (Theory) | `dotnet test --filter "FullyQualifiedName~ScaleLintAnalyzerFacts.EachMode_FlagsExpectedNonDiatonic"` | ❌ Wave 0 |
| D-08 cents-irrelevant | `E4+50c` in Cmajor silent; `Eb4+50c` flags base spelling | unit | `dotnet test --filter "FullyQualifiedName~ScaleLintAnalyzerFacts.CentOffset_*"` | ❌ Wave 0 |
| D-11/D-12/D-13/D-14 SKIPs | Roman numerals, named chord literals, variable refs, rests never flagged | unit | `dotnet test --filter "FullyQualifiedName~ScaleLintAnalyzerFacts.Skip_*"` | ❌ Wave 0 |
| D-17 token-width range | Range spans full token, not just 1 character | unit | `dotnet test --filter "FullyQualifiedName~ScaleLintAnalyzerFacts.Range_SpansFullTokenWidth"` | ❌ Wave 0 |
| D-18 source filter | Diagnostic.Source equals "flow.scaleLint" | unit | `dotnet test --filter "FullyQualifiedName~ScaleLintAnalyzerFacts.Source_IsFlowScaleLint"` | ❌ Wave 0 |
| D-22 silent fail-open | `key Eblues { ... }` (unparseable mode) emits zero diagnostics for that block | unit | `dotnet test --filter "FullyQualifiedName~ScaleLintAnalyzerFacts.UnparseableKey_SilentFailOpen"` | ❌ Wave 0 |
| Wave 0 ParseSession widen | `Program.Pragmas.Has("scaleLint")` is true after parsing source declaring the pragma | unit | `dotnet test --filter "FullyQualifiedName~ParseSessionPragmaFacts.Parse_EnableScaleLint_PopulatesPragmas"` | ❌ Wave 0 |
| Wave 0 hAsB regression | LSP now honors `enable hAsB;` (latent bug fix) — `H4q` under the pragma lexes as a NoteLiteral, not an Identifier | unit | `dotnet test --filter "FullyQualifiedName~ParseSessionPragmaFacts.Parse_EnableHAsB_LexesH4qAsNoteLiteral"` | ❌ Wave 0 |
| PragmaRegistry growth | `IsKnown("scaleLint")` returns true; `KnownPragmas.Count >= 5` | unit | `dotnet test --filter "FullyQualifiedName~PragmaRegistryScaleLintFacts"` | ❌ Wave 0 |
| Phase 18 byte-identical regression | `tutorial.flow` and `showcase.flow` produce byte-identical WAV+MIDI before and after Phase 24 | integration | `cmp examples/output/tutorial.wav.before examples/output/tutorial.wav.after` (existing `ByteIdenticalTutorialTests` / `ByteIdenticalShowcaseTests` Facts under flow-lang.Tests/Integration/) | ✅ existing |

### Sampling Rate
- **Per task commit:** `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase24"` (target ≤30s)
- **Per wave merge:** `dotnet test` (full suite)
- **Phase gate:** Full suite green AND `for f in tests/test_*.flow; do dotnet run --project flow-interpreter "$f" || exit 1; done` exits 0 before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `flow-lsp/ParseSession.cs` — widen to run `PragmaScanner.Scan` (preconditional refactor — covers Wave 0 latent bug fix)
- [ ] `flow-lang.Tests/Unit/Phase24/ParseSessionPragmaFacts.cs` — new file pinning ParseSession populates `Program.Pragmas` and lexes `H4q` correctly under `enable hAsB;`
- [ ] `flow-lang.Tests/Unit/Phase24/PragmaRegistryScaleLintFacts.cs` — new file pinning `scaleLint` known + count growth
- [ ] `flow-lang.Tests/Unit/Phase24/DiatonicSpellingsFacts.cs` — new file pinning per-(root, mode) spellings
- [ ] `flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerFacts.cs` — new file with all D-NN behavioral pins
- [ ] `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs:28,39` — migration: replace `"scaleLint"` literal with sentinel; update CSV to include `scaleLint`
- [ ] `tests/test_scale_lint.flow` — integration smoke covering LINT-01/02/03 acceptance pin verbatim
- [ ] No new framework install — xunit.v3 3.2.2 already present

## Security Domain

> `security_enforcement` is not present in `.planning/config.json` — treating as enabled per the default. Phase 24 is a static-analysis pass over already-parsed user source code in an offline LSP server. No network egress, no untrusted input beyond what `flow-lang.Parser` already accepts. Threat surface is minimal but documented for completeness.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | LSP runs over stdio; no auth surface |
| V3 Session Management | no | Per-document state in `DocumentManager`; no user sessions |
| V4 Access Control | no | No multi-user model |
| V5 Input Validation | yes | Source code is treated as untrusted; `PragmaScanner.LooksLikeTuningName` already documents `T-23-02-05` "max name length bounds inner loop" mitigation `[VERIFIED: PragmaScanner.cs:55-56]`. Phase 24 inherits — analyzer reads `NoteElement.NoteName` (already parser-validated to a known shape) and `Token.Text` (lexer-validated). No string-built SQL, no shell-out, no eval. |
| V6 Cryptography | no | No crypto |

### Known Threat Patterns for flow-lsp

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Adversarial source file with extremely long pragma name → DoS via Levenshtein O(nm) | DoS | Already mitigated upstream by Phase 21's closed-set bound (`PragmaRegistry.cs:41` `LevenshteinDistance` and `PragmaScanner.cs:58` `LevenshteinSmall`) — the closed-set max name length bounds the inner loop regardless of input length `[VERIFIED]`. Phase 24 reuses; no new mitigation needed. |
| Adversarial source file with deeply nested `key { key { key { ... } } }` → stack overflow in analyzer recursion | DoS | `WalkStatements` recurses into `MusicalContextStatement.Body`. The parser already enforces statement-depth limits (no explicit guard inspected, but the practical limit is the user typing into a 1MB file). Recommendation: spot-check parser depth limit; if absent, add an iterative walk. **OPEN — not researched in depth; flag for plan-checker.** `[ASSUMED low risk based on typical .flow file sizes]` |
| Source file with malformed UTF-8 → analyzer crash | Tampering | `flow-lang.Parser` already handles encoding normalization upstream; analyzer consumes `string` (already-decoded). No new surface. |

## Sources

### Primary (HIGH confidence — VERIFIED via file inspection)
- `flow-lang/Lexing/PragmaRegistry.cs:1-84` — closed-set registry, KnownPragmas dictionary, Levenshtein helper
- `flow-lang/Lexing/PragmaScanner.cs:84` — `Scan(source, fileName, errors) → (PragmaSet, transformedSource)` pipeline entry
- `flow-lang/Lexing/PragmaSet.cs:14-37` — `PragmaSet`, `PragmaSet.Empty`, `PragmaSet.Has`, `PragmaDeclarationSite`
- `flow-lang/Lexing/SimpleLexer.cs:23-24` — 4-arg ctor with `pragmaSet`
- `flow-lang/Parsing/Parser.cs:33-37` — 3-arg ctor with `pragmaSet?` defaulted to `PragmaSet.Empty`
- `flow-lang/Lexing/Token.cs:19-32` — Token record with `OriginalText`, `DiagnosticText` helper
- `flow-lang/Ast/Program.cs:15-25` — Program record with `Pragmas` field, 2-arg back-compat ctor
- `flow-lang/Ast/Expressions/NoteStreamExpression.cs:9-152` — full element hierarchy
- `flow-lang/Ast/Statements/MusicalContextStatement.cs:8-20` — `MusicalContextType.Key`, `Body` field
- `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs:33-42, 207-236` — `NoteToSemitone` (17 root spellings), `TryParseKeyWithMode`
- `flow-lang/StandardLibrary/Audio/Tuning/Mode.cs:8-17` — closed `Mode` enum (7 values exactly)
- `flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs:72-188` — hardcoded 14-table precedent
- `flow-lang/Core/FlowEngine.cs:66-82` — canonical pragma-scan-then-parse pipeline
- `flow-lang/Core/SourceLocation.cs:6-8` — `SourceLocation(Line, Column, FileName?)`
- `flow-lsp/ParseSession.cs:18-30` — current 2-arg Parser instantiation (Wave 0 fix target)
- `flow-lsp/Program.cs:19-60` — DI registration + onParse callback site
- `flow-lsp/NoteStream/NoteStreamContext.cs:25-262` — `IsInsideNoteStream`, `FindEnclosingKey` (D-21 reuse target)
- `flow-lsp/Handlers/DiagnosticsPublisher.cs:14-60` — `IDiagnosticsPublisher`, `BuildDiagnostics`, empty-publish-clears contract
- `flow-lsp/Handlers/TextDocumentSyncHandler.cs:25-85` — full-sync didChange wiring
- `flow-lsp/LspMappings.cs:13-35` — `ToRange`, `ToSeverity`
- `flow-lang.Tests/flow-lang.Tests.csproj:11-21` — xunit.v3 3.2.2, references both flow-lang and flow-lsp
- `flow-lang.Tests/Unit/Phase17/LspFixtures.cs:9-13` — Parse helper used by all LSP tests
- `flow-lang.Tests/Unit/Phase17/NoteStreamContextTests.cs:14-50` — Position-based test pattern
- `flow-lang.Tests/Unit/Phase17/DiagnosticsHandlerTests.cs:18-60` — BuildDiagnostics test pattern
- `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs:14-56` — closed-set growth test pattern; line 28 migration target
- `flow-lang.Tests/Unit/Phase23/PragmaTuningFacts.cs:19-64` — multi-pragma registration test pattern
- `flow-lang.Tests/Unit/Phase23/SpellingAwareTuningFacts.cs:11-51` — spelling-aware Fact pattern (precedent for D-01 lint)

### Secondary (MEDIUM confidence — referenced from documentation)
- CONTEXT.md `flow-sharp/.planning/phases/24-scale-linting-flow-lsp/24-CONTEXT.md:1-204` — Phase 24 user decisions
- CONTEXT.md `flow-sharp/.planning/phases/17-flow-language-server/17-CONTEXT.md:46` — D-11 enclosing-key brace-depth precedent
- CONTEXT.md `flow-sharp/.planning/phases/21-pragma-system-h-alias/21-CONTEXT.md:53,55,59` — D-13 lex-time substitution, D-15 OriginalText, D-17 PragmaRegistry reservation
- CONTEXT.md `flow-sharp/.planning/phases/23-microtonal-tuning-wedge/23-CONTEXT.md:38-50` — D-04 mode parser, D-09 spelling-aware tables
- REQUIREMENTS.md `flow-sharp/.planning/REQUIREMENTS.md:79-81, 113` — LINT-01/02/03 + anti-feature line
- ROADMAP.md `flow-sharp/.planning/ROADMAP.md:175-184` — Phase 24 goal + 3 success criteria

### Tertiary (LOW confidence — none)
- No external WebSearch / library-docs lookups required. Phase 24 is fully self-contained within the existing codebase. `[CITED: project's minimal-deps philosophy from CLAUDE.md §Guiding Principle]`

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every API signature inspected at cited line
- Architecture (Pipeline integration shape): HIGH — Pattern 1 is recommended with full evidence; alternatives explicitly evaluated
- Architecture (Spelling derivation): HIGH — Pattern 2 mirrors verified `TuningTables` precedent
- Architecture (Test placement): HIGH — Pattern 3 mirrors verified Phase 17/21/23 conventions
- Architecture (Per-mode coverage): MEDIUM — Pattern 3 recommendation is sound but discretion; planner may legitimately choose 7 small files
- Architecture (Alternative ordering): MEDIUM — Pattern 4 is opinionated; "lower-first" is one of three reasonable choices
- Architecture (Partial parses): HIGH — Pattern 5 derives from documented soft-failure model
- Architecture (Caching): HIGH — Pattern 6 derives from inspected debounce + small-set evidence
- Pitfalls: HIGH — Pitfall 1 (ParseSession gap) is verified by direct inspection of `flow-lsp/ParseSession.cs:22`; Pitfall 2 verified at `PragmaRegistryFacts.cs:28,39`
- Validation Architecture: HIGH — every required test type derives from D-NN locked decisions

**Research date:** 2026-05-04
**Valid until:** 2026-06-03 (30 days — codebase is stable; no fast-moving externals)
