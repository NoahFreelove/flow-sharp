# Phase 35: Language Foundation — Research

**Researched:** 2026-05-18
**Domain:** Compiler/interpreter internals — lexer, parser, AST, diagnostics, test framework, pattern matching, parser-level chain transforms
**Confidence:** HIGH for codebase-internal claims (verified by reading source); MEDIUM for external references (decision-tree paper, rustc diagnostic style — well-cited but applied via hand-roll)

## Summary

Phase 35 is the v1.5 dependency root. Four language features plus four v1.4 housekeeping items land here in a locked internal sequence (D-v1.5-10): **span migration → diagnostics renderer + test framework (parallel) → pattern matching → `-> as name`**. Every later v1.5 phase depends on at least one Phase 35 deliverable (Phase 36 needs match patterns for `SECT-01` destructuring; Phase 39 needs match patterns for articulation emit; Phase 40 needs match patterns for MIDI event dispatch; every phase needs the test framework for RMS-windowed regression coverage).

The codebase is well-prepared for this work. AST nodes are already `record` types with a `Location` field on every node (extending to `Span` is mechanical). `ErrorReporter` + `FlowError` already collect errors rather than throw. A defaulted-parameter migration precedent exists from Phase 22 (`MusicalNoteData` constructor has 17+ optional params added across phases). A Levenshtein implementation already lives in `PragmaRegistry.cs` and can be lifted. The pragma system from Phase 21 provides the exact plumbing pattern needed for `enable matchExhaustive;` (PragmaSet is per-file, threaded into the parser/lexer, queried via `pragmaSet.Has("name")`).

The four housekeeping items are surgical: HK-01 is a known bug with a documented repro (humanizeGaussian zeroes audio when wrapping voice blocks); HK-02 is essentially a documentation update (rows 1-3 already passed via Phase 31 UAT — the closure is recording the cross-reference); HK-03 has two concrete code+doc gaps in Phase 04's VERIFICATION.md; HK-04 is a CLAUDE.md prose rewrite per the rewritten `project_pre_public_no_legacy_burden` memory.

**Primary recommendation:** Plan as **8 plans across 5 waves**, with span migration strictly sequenced first (every other plan depends on it), housekeeping interleaved as a parallel wave 1 (independent of language work), test framework and diagnostics renderer parallel in wave 2 (both consume Span; neither blocks the other), pattern matching in wave 3 (consumes spans + tests), `-> as name` in wave 4 (consumes everything). Use the Phase 22 defaulted-parameter pattern for the Span migration — add `Span? Span = null` as the LAST positional ctor param on every AST record and Token, preserving every existing call site verbatim.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Span tracking on tokens | Lexing (`flow-lang/Lexing/`) | — | Tokens know their input position; Span is just `start_location + end_location` at lex time |
| Span tracking on AST | Parsing (`flow-lang/Parsing/`) | AST (`flow-lang/Ast/`) | Parser knows start + end of every production; Spans live ON the AST records, set BY the parser |
| Diagnostic rendering | Diagnostics (`flow-lang/Diagnostics/`) | Core (`flow-lang/Core/SourceMap`) | Renderer consumes a `FlowError` + Span + source text; needs a source-text registry keyed by file path |
| Did-you-mean suggestion | Diagnostics | Runtime (`StackFrame.GetAllAccessibleVariables`) | Levenshtein over the active scope's identifier list; runtime owns the scope |
| Pattern AST | AST (new `Ast/Patterns/` folder) | Parsing | Patterns are a NEW AST family distinct from Expressions/Statements; parser constructs them; evaluator consumes them |
| Match expression evaluation | Interpreter (`ExpressionEvaluator`) | Runtime (decision-tree compile output) | Evaluator runs the compiled decision tree against a runtime `Value`; tree compile happens at AST-build (or first-evaluation) time |
| Music-aware pattern extractors | StandardLibrary (`Harmony`, `Transforms`) | AST (`Patterns/ConstructorPattern`) | The extractors (chord-quality match, roman-numeral match, articulation match) reuse existing `ChordParser`, `HarmonyFunctions`, and `Articulation` machinery — no new music logic needed |
| `(test "name" body)` registration | StandardLibrary (`BuiltInFunctions`) | Interpreter (Lazy evaluation) | Same pattern as existing `(if cond then else)` — `LazyType` wrappers on body params defer execution |
| `flow test` CLI subcommand | flow-cli (`Commands/TestCommand.cs`) | StandardLibrary (test registry) | New subcommand mirrors existing `CheckCommand` shape; test registration writes to ExecutionContext; CLI walks `tests/test_*.flow` and runs registered tests |
| Hermetic isolation snapshot | Runtime (`ExecutionContext.SnapshotState/RestoreState`) | StandardLibrary (`SynthUtils.ResetNoiseRng`, voice-pool reset) | Snapshot lives on the orchestrator; called helpers reset their own statics |
| `-> as name` parser transform | Parsing (`Parser.cs`) | Interpreter (no new evaluator) | Pure parse-time desugar — `seq -> f as melody` becomes `Sequence melody = (f seq); melody` (a statement-group). Same shape as the existing `->` parse-time transform |
| Pragma `matchExhaustive` plumbing | Lexing (`PragmaRegistry`) | Interpreter (match evaluator reads `pragmaSet.Has`) | Identical to Phase 21's `hAsB` / Phase 23's `justIntonation` pragma plumbing |
| HK-01 humanizeGaussian bug fix | StandardLibrary (`TransformFunctions.HumanizeGaussian`) | Runtime (`NoteStreamCompiler` or `BarData.ParallelVoices`) | Bug is voice-block iteration zeroing duration; fix targets the iteration logic — TransformFunctions does NOT iterate ParallelVoices, only `bar.MusicalNotes` (see Pitfall 8 below) |

## Standard Stack

### Core (existing, no changes — Phase 35 work is internal to these projects)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 10 | net10.0 | Runtime | Already in use across flow-lang, flow-interpreter, flow-cli, flow-lsp; nullable refs + file-scoped namespaces + record types are all leveraged |
| C# 13 | Latest | Language | Records + switch-expression dispatch already used throughout |
| xUnit.v3 | 3.2.2 | C# unit tests | Existing `flow-lang.Tests/` test infrastructure — used for testing the test framework (meta-tests) and for unit tests of parser/evaluator |

### Supporting (existing helpers being reused)

| Component | Location | Purpose | Reuse Plan |
|-----------|----------|---------|------------|
| `PragmaRegistry.LevenshteinDistance` | `flow-lang/Lexing/PragmaRegistry.cs:60-84` | Wagner-Fischer Levenshtein impl | Promote to public helper in `flow-lang/Diagnostics/LevenshteinHelper.cs` so the new SnippetRenderer + the existing PragmaRegistry both consume it. Pure extraction — no algorithm changes. |
| `PragmaSet.Has(name)` | `flow-lang/Lexing/PragmaSet.cs` | Per-file pragma query | Add `matchExhaustive` entry to `PragmaRegistry.KnownPragmas` dict; evaluator queries `pragmaSet.Has("matchExhaustive")` at match-expression evaluation time |
| `RenderingDiagnostics.WarnOnce` | `flow-lang/Diagnostics/RenderingDiagnostics.cs` | One-shot stderr advisory with dedup | Used by D-v1.5-05's exhaustiveness WARN (sentinel key `match-non-exhaustive:{Span}`) |
| `ErrorReporter` + `FlowError` | `flow-lang/Diagnostics/` | Existing accumulating error sink | Extended with rich `FlowDiagnostic` (multi-span, labels, notes, suggestions) — backward-compat by keeping `FlowError.ToString()` as the default fallback rendering |
| `LazyType` wrapper | `flow-lang/TypeSystem/PrimitiveTypes/LazyType.cs` | Defers evaluation for special forms | Used by `(test "name" body)` and `(match expr | pat => body | ...)` so bodies don't pre-evaluate |
| `Thunk` | `flow-lang/Runtime/Thunk.cs` | Memoizing deferred-eval wrapper | The Lazy-wrapped body args become Thunks; pattern arms force Thunks only when matched |
| `StackFrame.GetAllAccessibleVariables()` | `flow-lang/Runtime/StackFrame.cs:80` | Enumerates in-scope identifiers | Used by did-you-mean — feed the variable+function name list into Levenshtein |
| `ChordParser.IsChordSymbol` + `ChordParser.Parse` | `flow-lang/StandardLibrary/Harmony/` | Recognizes `Cmaj7`, `Dm`, `F#dim`, etc. | Reused by constructor patterns matching on chord-quality |
| `HarmonyFunctions.resolveNumeral` | `flow-lang/StandardLibrary/Harmony/` | Roman numerals → chord notes (key-aware) | Reused by constructor patterns matching scale degrees (`V7`, `vi`) |
| `Articulation` enum | `flow-lang/.../Articulation.cs` (in MusicalNoteData) | Note articulation kinds | Symbol literal `#staccato` / `#legato` / `#accent` patterns compare directly against this enum |
| `RmsRegressionTests.AssertRmsWithinTolerance` | `flow-lang.Tests/Helpers/RmsRegressionTests.cs` | ±0.5dB / 100ms RMS window check | The `(assertWithinDb a b 0.5dB)` Flow builtin wraps this C# helper |
| `System.CommandLine` (existing in flow-cli) | flow-cli/Program.cs | Subcommand dispatch | `flow test` is a new `Command` registered in `CommandRegistry.BuildAllCommands` |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-rolled diagnostic renderer | `annotate-snippets-rs` / `codespan-reporting` (Rust) | Not available for C#; would require P/Invoke or rewrite. Hand-roll is correct here — diagnostic rendering is ~300 LOC of straightforward string assembly per the rustc reference. |
| Extending SourceLocation with End | New `Span(start, end)` record alongside existing `SourceLocation` | Considered but adds duplication. Recommendation: introduce `Span(SourceLocation Start, SourceLocation End)` as a new record; SourceLocation stays unchanged (already used in 200+ call sites). Span becomes the field on Tokens + AST nodes; SourceLocation is what Span wraps. See Section A.1 for mechanics. |
| Decision-tree pattern compiler (Jacobs/Maranget) | Naive linear-scan match-arm evaluator | Naive is fine for v1.5 — Flow's expected match-arm count per expression is small (3-15). Recommendation: ship naive linear scan in Wave 3; if profiling shows hotspots in Phase 36's `SECT-01` destructuring or Phase 40's MIDI event dispatch, optimize to decision tree in v1.6. The pattern AST is identical either way, so this is a back-end swap with zero composer-visible surface impact. **REQUIREMENTS.md LANG-01 mentions "Decision-tree compile per Jules Jacobs / Yorick Peterse reference" — discuss-phase should confirm whether naive-first-decision-tree-later is acceptable, OR whether decision-tree-from-the-start is required.** Flagged as Open Question 1 below. |
| Per-test subprocess isolation | In-process snapshot/restore | TEST-02 explicitly rejects subprocess isolation as anti-pattern. Snapshot/restore is correct per the locked decision. |
| New AST node for `as name` | Annotate FlowExpression with optional `IntermediateName` field | REQUIREMENTS.md LANG-03 explicitly says "no new AST node — annotates FlowExpression". Confirmed correct — adds defaulted-parameter `string? IntermediateName = null` to FlowExpression record. |

### Installation

No new external packages. Phase 35 is purely internal C# work plus new `.flow` stdlib modules.

```bash
# No npm/dotnet add operations. All changes live in:
#   flow-lang/Ast/Patterns/         (new folder)
#   flow-lang/Ast/Expressions/      (extend records)
#   flow-lang/Ast/Statements/       (extend records)
#   flow-lang/Core/Span.cs          (new file)
#   flow-lang/Core/SourceMap.cs     (new file — keyed source-text registry)
#   flow-lang/Diagnostics/SnippetRenderer.cs  (new file)
#   flow-lang/Diagnostics/FlowDiagnostic.cs   (new file — richer than FlowError)
#   flow-lang/Diagnostics/LevenshteinHelper.cs (extracted from PragmaRegistry)
#   flow-lang/Lexing/SimpleLexer.cs (touch every `new Token(...)` site — 46 sites)
#   flow-lang/Lexing/Token.cs       (add Span field)
#   flow-lang/Lexing/PragmaRegistry.cs (add matchExhaustive entry)
#   flow-lang/Parsing/Parser.cs     (touch ~86 AST construction sites; add match/as-name parsing)
#   flow-lang/Parsing/Parser.NoteStream.cs (touch construction sites)
#   flow-lang/Interpreter/ExpressionEvaluator.cs (add MatchExpression case)
#   flow-lang/Runtime/ExecutionContext.cs (add Snapshot/RestoreState)
#   flow-lang/StandardLibrary/BuiltInFunctions.cs (register test + assert builtins)
#   flow-lang/StandardLibrary/Transforms/TransformFunctions.cs (HK-01 fix)
#   flow-lang/test.flow             (new stdlib module — composer-facing `(test ...)` API hooks)
#   flow-cli/Commands/TestCommand.cs (new subcommand)
#   .planning/phases/04-composition-tools/ (HK-03 close)
#   .planning/phases/17-flow-language-server/17-HUMAN-UAT.md (HK-02 doc update)
#   CLAUDE.md                       (HK-04 footnote rewrite)
```

**Version verification:** No external packages added. xUnit.v3 3.2.2 already in `flow-lang.Tests/flow-lang.Tests.csproj` (verified via Read). System.CommandLine already powers flow-cli (verified via Read).

## Package Legitimacy Audit

> Not applicable for Phase 35. This phase installs **zero new external packages**. All work is internal C# refactoring/additions plus new `.flow` stdlib modules. No `slopcheck` / `npm view` / `pip index versions` invocation is meaningful here. The closest analog — verifying that `xUnit.v3 3.2.2` is the version already pinned in `flow-lang.Tests.csproj` — was confirmed via Read.

## Architecture Patterns

### System Architecture Diagram

```
                          flow-lang Pipeline (post-Phase-35)
                          ═══════════════════════════════════

  source text ──► PragmaScanner ──► [PragmaSet, transformed source]
                                          │
                                          ▼
                                    SimpleLexer ────► List<Token>  ◄── EVERY Token has Span (start+end)
                                          │
                                          ▼
                                       Parser ──────► Program (AST)  ◄── EVERY AST node has Span
                                          │                              ◄── NEW: MatchExpression node
                                          │                              ◄── NEW: Ast/Patterns/ family
                                          │                              ◄── NEW: FlowExpression has IntermediateName? annotation
                                          ▼
                                    Interpreter ◄────────────┐
                                          │                  │
                                          ▼                  │
                                ExpressionEvaluator           │ runtime errors
                                          │                  │ now carry Span
                                          │                  │ and route through
                                          ▼                  │ SnippetRenderer
                                       Value                 │
                                          │                  │
                                          ▼                  │
                              ┌────────────────────┐         │
                              │ ErrorReporter      │         │
                              │ accumulates:       │         │
                              │  FlowDiagnostic[]  ├─────────┘
                              └────────┬───────────┘
                                       │
                                       ▼
                              SnippetRenderer
                              (queries SourceMap for the
                               source text matching each Span;
                               renders rust-style multi-line)
                                       │
                                       ▼
                              stderr (TTY → ANSI colors via
                                      Console.ForegroundColor;
                                      pipe → plain text)


                          Test Framework Flow
                          ═══════════════════

  $ flow test tests/                    ◄── new flow-cli subcommand
            │
            ▼
  TestCommand.Run(path)
            │
            ▼
  Glob tests/test_*.flow                ◄── per TEST-01 convention
            │
            ▼
  for each .flow file:
            │
            ▼
  using FlowEngine engine = new()
            │
            ▼
  engine.Execute(source)               ◄── `(test "name" body)` builtins register tests
            │                              into ExecutionContext.TestRegistry
            ▼
  for each registered test:
        engine.Context.SnapshotState() ◄── TEST-02: capture musical-context stack,
            │                              voice-pool size, PRNG state, bindings
            ▼
        invoke test body (a Lazy)       ◄── assertions throw AssertionException on fail
            │
            ▼
        record pass/fail + exception
            │
            ▼
        engine.Context.RestoreState()   ◄── TEST-02: reset to snapshot
            │
            ▼
  print per-test results + summary; exit 0 if all pass else 1


                          Pattern Match Evaluation
                          ════════════════════════

  (match expr | pat1 => body1 | pat2 => body2 | _ => body3)
            │
            ▼
  Parser builds:                       ◄── disambiguation: only inside (match ...) context
       MatchExpression(                    are `| pat => body` arms parsed; note-stream
         scrutinee: expr,                  `| C4 D4 |` only fires from primary-expression
         arms: [                           start position. See Section D.2 for the rule.
           MatchArm(LiteralPattern(...), body1),
           MatchArm(ConstructorPattern("Dm7"), body2),
           MatchArm(WildcardPattern, body3)
         ])
            │
            ▼
  ExpressionEvaluator.EvaluateMatch:
       1. value = Evaluate(scrutinee)
       2. for each arm:
            if PatternMatches(arm.pattern, value, bindings):
                return Evaluate(arm.body) in extended scope with bindings
       3. if no arm matched:
            if pragmaSet.Has("matchExhaustive"):
                _errorReporter.ReportError(...)
            else:
                RenderingDiagnostics.WarnOnce(
                    $"match-non-exhaustive:{matchExpr.Span}",
                    "warning: match expression non-exhaustive — fell through to Void")
                return Value.Void()
```

### Recommended Project Structure

```
flow-lang/
├── Ast/
│   ├── Expressions/
│   │   ├── ... (existing 16 nodes, each gains Span field)
│   │   └── MatchExpression.cs            # NEW
│   ├── Statements/
│   │   └── ... (existing 14 nodes, each gains Span field)
│   └── Patterns/                         # NEW folder
│       ├── Pattern.cs                    # abstract record Pattern(Span Span)
│       ├── LiteralPattern.cs             # matches Int/Float/String/Bool/Note literals
│       ├── WildcardPattern.cs            # matches anything; binds nothing
│       ├── BindingPattern.cs             # matches anything; binds to a name
│       ├── ConstructorPattern.cs         # matches chord quality / roman numeral / symbol
│       ├── GuardPattern.cs               # wraps a pattern + a (Bool => Bool) guard
│       └── MatchArm.cs                   # record MatchArm(Pattern, Expression body)
├── Core/
│   ├── SourceLocation.cs                 # unchanged (single point)
│   ├── Span.cs                           # NEW: record Span(SourceLocation Start, SourceLocation End)
│   ├── SourceMap.cs                      # NEW: file-path → source-text registry
│   └── FlowEngine.cs                     # gains SnapshotState/RestoreState delegating to ExecutionContext
├── Diagnostics/
│   ├── ErrorReporter.cs                  # extended: accepts FlowDiagnostic (richer than FlowError)
│   ├── FlowError.cs                      # unchanged (legacy path)
│   ├── FlowDiagnostic.cs                 # NEW: record FlowDiagnostic(Level, Message, Span primary, List<Label> labels, List<Note> notes, List<Suggestion> suggestions)
│   ├── SnippetRenderer.cs                # NEW: FlowDiagnostic → multi-line ANSI/plain string
│   ├── LevenshteinHelper.cs              # NEW: extracted from PragmaRegistry
│   └── RenderingDiagnostics.cs           # unchanged (one-shot stderr advisory)
├── Lexing/
│   ├── Token.cs                          # gains Span field
│   ├── SimpleLexer.cs                    # every `new Token(...)` site updated; lexer tracks end-position per token
│   └── PragmaRegistry.cs                 # adds matchExhaustive entry
├── Parsing/
│   ├── Parser.cs                         # gains: ParseMatch, ParseAsNameAnnotation;
│   │                                     # every AST construction call site updated to pass Span
│   └── ...
├── Interpreter/
│   └── ExpressionEvaluator.cs            # gains: EvaluateMatch case + pattern-matching helper
├── Runtime/
│   ├── ExecutionContext.cs               # gains: SnapshotState/RestoreState; TestRegistry
│   └── ...
├── StandardLibrary/
│   ├── BuiltInFunctions.cs               # registers (test ...), (assert ...), (assertEq ...), (assertNotesMatch ...), (assertBytesEqual ...), (assertWithinDb ...)
│   ├── Transforms/
│   │   └── TransformFunctions.cs         # HK-01 fix — humanizeGaussian iterates ParallelVoices recursively
│   └── ...
└── test.flow                             # NEW @test stdlib module — proc wrappers / docs

flow-cli/
└── Commands/
    └── TestCommand.cs                    # NEW: `flow test [path]`

flow-lang.Tests/
├── Unit/
│   ├── SnippetRendererTests.cs           # NEW: golden-file diagnostic rendering tests
│   ├── PatternMatchingTests.cs           # NEW: parser + evaluator coverage
│   ├── ChainNamingTests.cs               # NEW: `-> as name` parser coverage
│   └── TestFrameworkMetaTests.cs         # NEW: C# tests that the Flow test framework works (no recursion)
└── Integration/
    └── Phase35/
        ├── SpanMigrationRegressionTests.cs  # NEW: full existing test suite must remain green
        └── HK01HumanizeGaussianBugTests.cs  # NEW: voice-block humanizeGaussian regression

tests/                                    # existing .flow scripts — Phase 35 does NOT convert them
└── (existing 70+ test_*.flow files unchanged; new tests use `(test ...)` framework)
```

### Pattern 1: Defaulted-Parameter Record Migration (Span migration)

**What:** Add `Span` as the LAST positional ctor param on every AST record + Token, defaulted to `Span.Unknown` so every existing constructor call site continues to compile.

**When to use:** Across the entire Span migration. Mirrors Phase 21's `OriginalText` field on Token and Phase 22's `DurationOverlap` / `PortamentoMs` fields on MusicalNoteData (both shipped without breaking existing call sites).

**Example:**
```csharp
// flow-lang/Core/Span.cs (NEW)
namespace FlowLang.Core;

public record Span(SourceLocation Start, SourceLocation End)
{
    public static Span Unknown { get; } = new(SourceLocation.Unknown, SourceLocation.Unknown);

    // Convenience: zero-width span at a single location (e.g., for tokens that
    // are single characters or for back-compat from existing single-Location call sites).
    public static Span At(SourceLocation loc) => new(loc, loc);

    public override string ToString() =>
        Start == End ? Start.ToString() : $"{Start}..{End}";
}

// flow-lang/Lexing/Token.cs (extended)
public record Token(
    TokenType Type,
    string Text,
    SourceLocation Location,        // KEEP for back-compat — many sites read .Location
    object? Value = null,
    string? OriginalText = null,
    Span? Span = null)              // NEW — defaulted; lexer fills it in
{
    // Compatibility helper: if Span is null, synthesize from Location
    public Span EffectiveSpan => Span ?? FlowLang.Core.Span.At(Location);
}

// flow-lang/Ast/Expressions/FunctionCallExpression.cs (extended)
public record FunctionCallExpression(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Expression> Arguments,
    Span? Span = null)              // NEW — defaulted
    : Expression(Location);
```

**Critical detail:** Keep `SourceLocation Location` on existing records as well. There are 200+ read-sites (LSP `ToRange`, error messages, etc.) and removing it forces a same-PR sweep of LSP + tests. Add `Span` as a defaulted SUPPLEMENT; do not replace `Location`. Long-term (post-traction) cleanup deferred to v1.6.

### Pattern 2: Special-Form Builtin (`(test "name" body)`)

**What:** Register a builtin whose body argument is `LazyType`-wrapped so the body executes only when intentionally invoked, not at call-site evaluation.

**When to use:** For `(test ...)` and for `(match ...)`'s body arms.

**Example:**
```csharp
// flow-lang/StandardLibrary/BuiltInFunctions.cs (analog of existing `if`)
var testSig = new FunctionSignature(
    "test",
    [StringType.Instance, new LazyType(VoidType.Instance)]);
registry.Register("test", testSig, args =>
{
    var name = args[0].As<string>();
    var bodyThunk = args[1].As<Thunk>();
    context.TestRegistry.Add(new TestRecord(name, bodyThunk));
    return Value.Void();
});
```

The `(if cond then else)` builtin at `BuiltInFunctions.cs:339-340` is the exact precedent — verified by reading the file.

### Pattern 3: Parser-Level Desugar for `-> as name`

**What:** `seq -> (transpose 2) as melody -> (legato 0.5) as legato-melody -> render` parses to a statement-block expression: assign the `seq -> (transpose 2)` result to a new in-scope variable `melody`, continue the chain.

**When to use:** Inside any expression position. The transform is pure parser, no AST node added.

**Example AST shape (annotated FlowExpression):**
```csharp
// flow-lang/Ast/Expressions/FlowExpression.cs (extended per LANG-03)
public record FlowExpression(
    SourceLocation Location,
    Expression Left,
    Expression Right,
    string? IntermediateName = null,  // NEW — set when parser sees `as name` after Right
    Span? Span = null);

// Evaluator behavior: after EvaluateFlowExpression produces the result Value,
// if IntermediateName is non-null, _context.DeclareVariable(IntermediateName, result)
// in the CURRENT frame. Subsequent expressions in the same enclosing scope can
// read it. Scope = enclosing block/function (same scope as a top-level
// `Sequence melody = ...` declaration).
```

### Anti-Patterns to Avoid

- **Replacing SourceLocation with Span everywhere in one PR.** Touches 200+ read-sites in LSP + tests + interpreter. Use defaulted-parameter supplement instead (Pattern 1).
- **Compiling pattern decision trees in v1.5.** REQUIREMENTS.md mentions the Jacobs/Peterse reference but the expected match-arm count in Flow is small (3-15 per match). Naive linear scan is simpler, easier to debug, and faster to ship. Flag the decision-tree path as a v1.6 optimization in CONTEXT. **Open Question 1 — confirm with composer at discuss-phase.**
- **Introducing per-test subprocess isolation.** TEST-02 explicitly rejects this. In-process Snapshot/RestoreState is correct.
- **Adding a new AST node for `as name`.** REQUIREMENTS.md LANG-03 explicitly forbids this. Annotate the existing FlowExpression record.
- **Trying to make `match` exhaustiveness work for open types like `Chord`.** Chord quality is an open string set (`"maj7"`, `"dim"`, but also user-extensible). Conservative exhaustiveness only flags non-exhaustive when (a) the scrutinee type has a finite known case-set AND (b) the user did not include a wildcard arm. For open types, only "no wildcard arm" triggers the warning. See Section D.5.
- **Converting existing 70+ `tests/test_*.flow` files to the new framework in Phase 35.** Scope creep. Existing scripts stay as-is (CLAUDE.md still cites the "verified by their console output (success = no errors)" convention). New tests opt into the framework. v1.5 backlog can carry "convert existing tests" as a low-priority cleanup.
- **Hand-rolling the Levenshtein algorithm a second time in SnippetRenderer.** Extract `PragmaRegistry.LevenshteinDistance` to `Diagnostics/LevenshteinHelper.cs` and make both call sites consume it.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Levenshtein distance | A second copy in SnippetRenderer | Extract `PragmaRegistry.LevenshteinDistance` to `Diagnostics/LevenshteinHelper.cs` | Already correct + tested via Phase 21 |
| Source-text registry | Re-reading files from disk every time a diagnostic renders | `SourceMap` keyed by file path; lexer registers source on entry | REPL has no file path — special-case `<eval>` / `<stdin>` / `<repl>` to in-memory text |
| TTY detection | Manual `[31m` ANSI strings | `Console.ForegroundColor = ConsoleColor.Red; ... Console.ResetColor()` | Existing pattern in `Program.cs:77`, `LiveReloadManager.cs:61`. .NET handles TTY-vs-pipe automatically. |
| Test framework infra | xUnit-style attribute discovery in Flow | `(test "name" body)` builtin + ExecutionContext.TestRegistry | Pure-Flow surface per TEST-01; reuses existing LazyType + Thunk machinery |
| Lazy evaluation for special forms | New AST node for `match` arms | `LazyType` parameter + Thunk + force-on-match | Identical to existing `(if cond then else)` plumbing |
| Pragma plumbing for `enable matchExhaustive;` | New per-file flag mechanism | Add `"matchExhaustive"` to `PragmaRegistry.KnownPragmas`; query `pragmaSet.Has(...)` at match-eval time | Phase 21 (`hAsB`), Phase 23 (`justIntonation`, `pythagorean`, `equalTemperament`), Phase 24 (`scaleLint`) all use this exact pattern |
| RMS-windowed comparison for `(assertWithinDb ...)` | New audio-comparison logic | `RmsRegressionTests.AssertRmsWithinTolerance` (existing C# helper, SPEC-8 locked tolerance) | Already correct + tested across Phase 28/29/33 |
| Chord-quality pattern matching | New chord parser | `ChordParser.IsChordSymbol` + `ChordParser.Parse` | Already correct |
| Roman-numeral pattern matching | New roman-numeral logic | `HarmonyFunctions.resolveNumeral` (key-context-aware) | Already correct |

**Key insight:** Phase 35's "infrastructure" half (span migration, diagnostics, test framework, pragma plumbing) is largely *extension and extraction* of existing well-tested machinery, not invention. The "language feature" half (pattern matching, `as name`) is the more conceptually novel work — and even there, music-aware extractors reuse `ChordParser` / `HarmonyFunctions` rather than inventing new music logic.

## Runtime State Inventory

> Phase 35 does NOT rename or remove any existing surface (D-v1.5-01 latitude — but Phase 35 specifically adds new features, doesn't break existing). The Span migration is purely ADDITIVE (defaulted parameters). No runtime state inventory needed.

**Nothing found in any category:** None. Phase 35 work is purely additive language/tooling extension — no datastores keyed on renamed strings, no service configs to migrate, no OS-registered names, no secrets, no installed packages to rename. Verified by reading REQUIREMENTS.md LANG-01..04, TEST-01..02, HK-01..04 — none describe a rename/refactor of an existing user-visible surface.

## Common Pitfalls

### Pitfall 1: Span-migration sweep misses one AST construction site

**What goes wrong:** A new AST node is constructed with `Span.Unknown` (because the caller forgot to pass span). At runtime, diagnostics for errors in that node render with `?:?` instead of the source location.

**Why it happens:** Defaulted parameter — easy to forget. ~86 AST construction sites in Parser.cs + Parser.NoteStream.cs (verified via grep).

**How to avoid:** After Wave 1 lands, grep `new (FunctionCall|Literal|Variable|Flow|Lambda|...|Section|Variable)(Expression|Statement|Declaration)` and verify every match either passes `Span:` explicitly OR is in a test file constructing a synthetic AST. Add a CI lint (xUnit fact) that walks the parser output for a sample test corpus and asserts every node has a non-Unknown Span.

**Warning signs:** Diagnostics rendering as `<unknown>:0:0: error: ...` for parser-produced errors.

### Pitfall 2: Pattern-match `|` disambiguation against note-stream `|`

**What goes wrong:** Parser sees `| Cmaj7 => "I"` and tries to lex it as a note-stream open delimiter (note stream syntax is `| C4 D4 |`).

**Why it happens:** Both use TokenType.Pipe. Disambiguation is context-sensitive.

**How to avoid:** Match arms are ONLY parsed inside `(match scrutinee | ... )` — a parenthesized form. The parser enters "match-arms mode" only after consuming `(match expr`. Note streams are parsed only from primary-expression start position (see `Parser.cs:1044`). The `(match` open paren is the disambiguator — the parser KNOWS it's in match-arms mode when it sees the next `|`.

Concrete rule: inside `ParseMatch`, after consuming the scrutinee expression, expect `|` repeatedly to introduce each arm; never enter `ParseNoteStream` here. Outside `ParseMatch`, a `|` at primary-expression start still triggers `ParseNoteStream`. Verified safe by reading `Parser.cs:1043-1047`.

**Warning signs:** Confusing parser errors like "expected note or '_' in random choice" when the composer wrote a match expression.

### Pitfall 3: Hermetic-isolation surface gets missed

**What goes wrong:** Test A sets a musical-context tempo block, test B inherits the tempo and fails subtly.

**Why it happens:** State leakage points beyond the obvious ones (musical context stack, voice pool, PRNG, bindings):

- `ExecutionContext.SymbolInternTable` (Phase 26.1)
- `ExecutionContext.SfzInstruments` / `SfzPatchRegistry` / `SfzDiagnostics` (Phase 33)
- `ExecutionContext.ResolvedSfzRoot` (Phase 33 — first-read cache)
- `SynthUtils.Rng` static (`SynthUtils.ResetNoiseRng()` resets it)
- `RenderingDiagnostics._emitted` static (`RenderingDiagnostics.ResetForTesting()` resets it)
- `FlowEngine.CurrentSampleCache` / `CurrentSfzSampleCache` / `CurrentExecutionContext` statics
- `FlowConfig.Active` (mutable singleton — Phase 30 / Phase 33 Pitfall 2)
- `AudioPlaybackManager` (audio backend state)
- `ExecutionContext.FixedRandSeed` / `FixedGen` / `Gen`

**How to avoid:** Build a single `ExecutionContext.SnapshotState()` that captures everything via reflection-friendly explicit calls (no reflection — write each capture). `RestoreState()` reinstates. Mirror the existing `RenderingDiagnostics.ResetForTesting()` and `SynthUtils.ResetNoiseRng()` patterns. **List every static-mutable site as a checklist in the plan.** Open Question 4 — discuss-phase should confirm whether tests need to also reset the AudioPlaybackManager / SFZ static accessors, or whether those are tolerated.

**Warning signs:** Test pass/fail flakiness depending on test order. Diff between `flow test tests/foo.flow` and `flow test tests/` outcomes.

### Pitfall 4: `enable matchExhaustive;` only affects the file it's declared in

**What goes wrong:** Composer declares `enable matchExhaustive;` in `mod1.flow`, imports `mod2.flow` via `use`, mod2's match expressions are NOT held to the strict standard.

**Why it happens:** Per Phase 21 PRAG-02 (verified via `PragmaSet.cs:8-10`), pragmas do NOT propagate across `use` imports — each imported file gets its own PragmaSet.

**How to avoid:** Document this in the `enable matchExhaustive;` pragma description (added to `PragmaRegistry.KnownPragmas`). This is consistent with Phase 21 + Phase 23 pragma semantics and is the intended behavior — each file independently opts into strict mode.

**Warning signs:** Composer surprised that imported module didn't error on non-exhaustive match.

### Pitfall 5: Did-you-mean suggestion shadows the actual unknown identifier

**What goes wrong:** Composer typed `transpos` (typo for `transpose`); SnippetRenderer suggests `transpose`, but composer-defined `transpoz` also exists in scope and is what they meant.

**Why it happens:** Levenshtein returns the closest match; if multiple identifiers tie or both score within threshold, picking only one can be misleading.

**How to avoid:** Mirror Phase 21's `PragmaRegistry.SuggestNearest` (single best within `max(2, len/3)` threshold). Show at most ONE suggestion. Threshold should be conservative — rust uses ≤3 absolute. Recommend `max(2, typed.Length / 3)` per the existing PragmaRegistry impl (verified via Read). If two candidates tie at the same distance, prefer the one with the most common prefix; if still tied, alphabetical.

**Warning signs:** Composer reports confusing "did you mean X" when X is unrelated.

### Pitfall 6: Match-arm bindings escape to outer scope

**What goes wrong:** `(match seq | (cons head tail) => ... | _ => ...)` — the `head` and `tail` bindings leak past the match expression and pollute the enclosing scope.

**Why it happens:** If the evaluator declares bindings on the current frame, they persist after the match returns.

**How to avoid:** Push a temporary frame for arm-body evaluation, declare bindings in it, pop on completion. Mirrors the existing `PushFrame`/`PopFrame` lifecycle in `ExecutionContext.cs:171-191`. The match expression returns the body's Value, but the bindings die with the frame.

**Warning signs:** Variable-shadowing bugs in code that uses match.

### Pitfall 7: `seq -> (transpose 2) as melody -> (legato melody 0.5)` — name visible during own chain step?

**What goes wrong:** The composer writes the chain expecting `melody` to be referenceable in subsequent chain steps, but the parser hasn't yet bound it.

**Why it happens:** Order-of-evaluation in the parser-level desugar.

**How to avoid:** The parser desugar must declare `melody` BEFORE evaluating subsequent chain steps. Concretely, `LHS -> RHS as name -> NEXT` should desugar to:
```
{ Type name = (RHS LHS);    // type inferred from RHS return type
  (NEXT name) }              // NEXT runs with `name` in scope
```
Recommend desugaring incrementally: each `as name` introduces a new in-scope binding that subsequent `->` steps and the same-statement continuation can read. The composer-visible model: "the `as` clause makes the result available under the given name from this point onward in the enclosing scope, until end of statement."

Verified the existing `->` parse-time transform shape via `Parser.cs:1044` + `FlowExpression.cs` — the recommendation is consistent with the existing single-pass parser.

**Warning signs:** "Unknown identifier 'melody'" errors when the composer thinks the chain bound it.

### Pitfall 8: HK-01 fix — humanizeGaussian doesn't iterate ParallelVoices

**What goes wrong (existing bug):** Wrapping a sequence containing `{voice ...}{voice ...}` voice blocks in `(humanizeGaussian seq 0.03 314)` produces a 44-byte WAV (header only — no audio renders).

**Why it happens (root cause, verified via Read of `TransformFunctions.cs:931-962` + `BarData.ParallelVoices` referenced in CLAUDE.md):** `HumanizeGaussian` iterates only `bar.MusicalNotes` per outer bar. When the bar carries `ParallelVoices` (Phase 28 voice-block polyphony), the inner voices are stored in `bar.ParallelVoices`, NOT in `bar.MusicalNotes`. The function constructs `new BarData(newNotes, bar.TimeSignature!)` — which drops the ParallelVoices entirely. The resulting bar has empty MusicalNotes (because the originals were in ParallelVoices) AND empty ParallelVoices (because they weren't copied) → silent render.

**How to avoid:** Two-line fix conceptually:
1. When constructing the output BarData, preserve `bar.ParallelVoices` (either by passing through unchanged, OR by recursively humanizing each voice sub-sequence).
2. Per composer intent (humanize should affect ALL voices), recursive is correct: each voice in ParallelVoices is itself a list-of-notes; humanize them with the SAME seed-derived RNG so the same composer-visible determinism holds.

**Warning signs:** Tiny WAVs (header-only ~44 bytes) when humanize is applied to voice-block content. The repro is already documented in `examples/ragtime/ragtime.flow` git history per `project_v15_backlog` memory.

**Verification approach:** Add `tests/test_humanize_voice_block.flow` that constructs a 2-voice block, applies humanizeGaussian, renders to WAV, asserts the WAV is > 44 bytes AND contains non-silent samples.

### Pitfall 9: Match decision-tree compile would block on edge cases not in the paper

**What goes wrong:** Implementing the Jacobs/Peterse algorithm faithfully requires resolving open questions around music-aware extractors (chord quality matching, roman-numeral matching) that don't fit standard ADT pattern matching cleanly.

**Why it happens:** The paper assumes sum-types (`Cons | Nil`). Flow's `Chord` is a struct with a string-typed `Quality` field — pattern matching `Cmaj7` against `Chord{Root='C', Quality="maj7"}` is structural-equality on field, not constructor dispatch.

**How to avoid:** Ship naive linear scan in v1.5 (Open Question 1). It composes naturally with chord-quality matching: each arm runs its match-predicate against the scrutinee, first match wins. Decision tree is a v1.6 optimization with no composer-visible surface change.

**Warning signs:** Plan estimates ballooning past 1 day for the pattern-matching plan.

### Pitfall 10: `(test ...)` body executes at registration time, defeating the framework

**What goes wrong:** `(test "name" (body))` evaluates body during registration, not during test runs. Hermetic isolation between tests is meaningless if all bodies already ran.

**Why it happens:** Without `LazyType` wrapping, the parenthesized `body` evaluates eagerly per Flow's strict semantics.

**How to avoid:** Type-sign the `test` builtin with `LazyType(VoidType.Instance)` for the body parameter (same as `if`'s then/else branches at `BuiltInFunctions.cs:339`). The body arrives as a `Thunk` which is `.Force()`-d only when the test framework decides to run it.

**Warning signs:** `flow test` prints test pass/fail summary BEFORE any test bodies have actually run; per-test snapshot/restore appears to do nothing.

## Code Examples

### Example 1: New Span record + extended Token

```csharp
// flow-lang/Core/Span.cs (NEW)
// Source: Pattern lifted from Phase 21 Token.OriginalText defaulted-param precedent
namespace FlowLang.Core;

public record Span(SourceLocation Start, SourceLocation End)
{
    public static Span Unknown { get; } = new(SourceLocation.Unknown, SourceLocation.Unknown);
    public static Span At(SourceLocation loc) => new(loc, loc);
    public static Span Between(SourceLocation start, SourceLocation end) => new(start, end);

    public override string ToString() =>
        Start == End ? Start.ToString() : $"{Start}..{End}";
}
```

### Example 2: Match expression evaluation (naive linear scan)

```csharp
// flow-lang/Interpreter/ExpressionEvaluator.cs (additions)
// Source: pattern mirrors existing EvaluateFlowExpression at the same file
private Value EvaluateMatch(MatchExpression match)
{
    var scrutinee = Evaluate(match.Scrutinee);

    foreach (var arm in match.Arms)
    {
        var bindings = new Dictionary<string, Value>();
        if (PatternMatches(arm.Pattern, scrutinee, bindings))
        {
            _context.PushFrame();
            try
            {
                foreach (var (name, value) in bindings)
                    _context.DeclareVariable(name, value);
                return Evaluate(arm.Body);
            }
            finally { _context.PopFrame(); }
        }
    }

    // Non-exhaustive — D-v1.5-05 charitable interpretation
    if (_context.ProgramPragmaSet.Has("matchExhaustive"))
    {
        _errorReporter.ReportError(
            FlowDiagnostic.NonExhaustiveMatch(match.Span, scrutinee.Type));
        return Value.Void();
    }

    RenderingDiagnostics.WarnOnce(
        $"match-non-exhaustive:{match.Span}",
        $"warning: match expression at {match.Span} non-exhaustive — fell through to Void");
    return Value.Void();
}

private bool PatternMatches(Pattern pattern, Value scrutinee, Dictionary<string, Value> bindings)
{
    return pattern switch
    {
        WildcardPattern => true,
        BindingPattern b => Bind(b.Name, scrutinee, bindings),
        LiteralPattern lit => Value.Equals(scrutinee, lit.Value),
        ConstructorPattern ctor => MatchConstructor(ctor, scrutinee, bindings),
        GuardPattern guard => PatternMatches(guard.Inner, scrutinee, bindings)
                              && EvaluateGuard(guard.GuardExpression),
        _ => throw new NotSupportedException($"Unknown pattern: {pattern.GetType().Name}")
    };
}

private bool MatchConstructor(ConstructorPattern ctor, Value scrutinee, Dictionary<string, Value> bindings)
{
    // Music-aware extractors:
    //   - Chord literal (Cmaj7, Dm) matches Chord values by Root + Quality
    //   - Roman numeral (V7) resolved against current key context, then matches Chord
    //   - Symbol literal (#staccato) matches Notes by Articulation enum
    // Falls back to structural equality for non-music constructors.
    if (ctor.IsChordLiteral)
        return MatchChordQuality(ctor.Name, scrutinee);
    if (ctor.IsRomanNumeral)
        return MatchRomanNumeral(ctor.Name, scrutinee);
    if (ctor.IsArticulationSymbol)
        return MatchArticulation(ctor.Name, scrutinee);
    return Value.Equals(scrutinee, ctor.ResolvedConstructor);
}
```

### Example 3: Test framework registration + run loop

```csharp
// flow-lang/StandardLibrary/BuiltInFunctions.cs (additions)
// Source: precedent at BuiltInFunctions.cs:339 (`if` builtin with LazyType arg)

public static void RegisterTestFramework(
    InternalFunctionRegistry registry,
    Runtime.ExecutionContext context)
{
    // (test "name" body)
    var testSig = new FunctionSignature("test",
        [StringType.Instance, new LazyType(VoidType.Instance)]);
    registry.Register("test", testSig, args =>
    {
        var name = args[0].As<string>();
        var bodyThunk = args[1].As<Thunk>();
        context.TestRegistry.Add(new TestRecord(name, bodyThunk));
        return Value.Void();
    });

    // (assert cond)
    var assertSig = new FunctionSignature("assert", [BoolType.Instance]);
    registry.Register("assert", assertSig, args =>
    {
        if (!args[0].As<bool>()) throw new AssertionException("assert failed");
        return Value.Void();
    });

    // (assertEq a b) — generic equality
    var assertEqSig = new FunctionSignature("assertEq",
        [VoidType.Instance, VoidType.Instance]);   // Void wildcards (existing OverloadResolver convention)
    registry.Register("assertEq", assertEqSig, args =>
    {
        if (!Value.Equals(args[0], args[1]))
            throw new AssertionException($"assertEq failed: {args[0]} != {args[1]}");
        return Value.Void();
    });

    // (assertWithinDb buf1 buf2 toleranceDb) — wraps RmsRegressionTests
    var assertWithinDbSig = new FunctionSignature("assertWithinDb",
        [BufferType.Instance, BufferType.Instance, DecibelType.Instance]);
    registry.Register("assertWithinDb", assertWithinDbSig, args =>
    {
        var a = args[0].As<AudioBuffer>();
        var b = args[1].As<AudioBuffer>();
        var tolerance = args[2].As<double>();   // Decibel coerces to Double
        // Wraps the existing C# helper at flow-lang.Tests/Helpers/RmsRegressionTests.cs
        // Need to lift the comparison logic out of the test assembly into flow-lang
        // (the test-assembly helper depends on Xunit.Assert; the Flow builtin needs
        // a comparison helper that throws AssertionException instead).
        var deviation = RmsComparator.MaxWindowDeviationDb(a, b, windowMs: 100.0);
        if (deviation > tolerance)
            throw new AssertionException(
                $"assertWithinDb failed: max RMS deviation {deviation:F2} dB > tolerance {tolerance:F2} dB");
        return Value.Void();
    });

    // (assertNotesMatch seqA seqB), (assertBytesEqual buf1 buf2) — analogous shape
}
```

```csharp
// flow-cli/Commands/TestCommand.cs (NEW)
// Source: pattern mirrors CheckCommand.cs verbatim
internal static class TestCommand
{
    public static Command Build()
    {
        var pathArg = new Argument<string?>("path")
        {
            Description = "Path to test file or directory (default: tests/)",
            Arity = ArgumentArity.ZeroOrOne
        };

        var cmd = new Command("test", "Run Flow test files");
        cmd.Add(pathArg);
        cmd.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathArg) ?? "tests/";
            var files = Directory.Exists(path)
                ? Directory.GetFiles(path, "test_*.flow", SearchOption.TopDirectoryOnly)
                : new[] { path };

            int passed = 0, failed = 0;
            foreach (var file in files)
            {
                using var engine = new FlowEngine();
                var source = File.ReadAllText(file);
                engine.Execute(source, file);    // (test ...) builtins register tests

                foreach (var test in engine.Context.TestRegistry)
                {
                    engine.Context.SnapshotState();
                    try
                    {
                        test.BodyThunk.Force();
                        Console.WriteLine($"  PASS  {file}::{test.Name}");
                        passed++;
                    }
                    catch (AssertionException ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  FAIL  {file}::{test.Name}: {ex.Message}");
                        Console.ResetColor();
                        failed++;
                    }
                    finally
                    {
                        engine.Context.RestoreState();
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Total: {passed + failed}; Passed: {passed}; Failed: {failed}");
            return failed == 0 ? 0 : 1;
        });
        return cmd;
    }
}
```

### Example 4: Rust-style diagnostic rendering shape

```
error: unknown identifier 'transpos'
  --> tests/test_chain.flow:7:14
   |
 7 | seq -> (transpos 2)
   |              ^^^^^^^ not found in scope
   |
   = note: tried looking in: enclosing function 'main', module 'std', module 'audio'
   = help: did you mean 'transpose'?
```

The renderer composes lines:
1. Header: `error: <message>`
2. Location: `  --> <file>:<line>:<col>`
3. Pipe-prefixed source quote: `\n N | <source line>` (N is line number, right-aligned)
4. Caret line: `   | <spaces><carets up to span length> <label>`
5. Optional secondary `note:` lines (each as `   = note: <text>`)
6. Optional suggestion: `   = help: did you mean '<suggestion>'?`

Color (TTY only, via `Console.ForegroundColor`):
- Red: `error:` keyword + carets
- Yellow: `warning:` keyword + carets
- Cyan: `note:` / `help:` prefixes
- Default: source quote + labels

## State of the Art

| Old Approach (current Flow) | Current Approach (post-Phase-35) | When Changed | Impact |
|-----------------------------|----------------------------------|--------------|--------|
| Single-line `{file}:{line}:{col}: error: {msg}` | Multi-line rust-style with source quote, caret, label, notes, suggestion | Phase 35 | Composer DX leap — locates errors instantly without context-switching to the editor |
| AST nodes carry single `SourceLocation` (start only) | AST nodes carry `Span` (start + end) | Phase 35 | LSP can highlight the FULL erroneous expression instead of a 1-char range (LspMappings.cs:22-26 hard-codes col+1 today). Diagnostic carets span the full bad construct. |
| Tests via shell loop `for test in tests/test_*.flow; do dotnet run ...; done` (per CLAUDE.md) | `flow test [path]` with hermetic isolation, structured pass/fail output, exit codes | Phase 35 | Tests become first-class. Existing 70+ scripts stay as smoke tests; new tests opt into the framework with rich assertions. |
| No pattern-matching in Flow | `(match expr | pat => body | ...)` with music-aware extractors | Phase 35 | Enables Phase 36 destructuring, Phase 39 articulation emit, Phase 40 MIDI event dispatch — entire downstream v1.5 stack |
| No mid-chain naming — composer must break the chain to capture an intermediate | `seq -> (transpose 2) as melody -> ... -> (mix melody other)` | Phase 35 | Composer ergonomics — fewer intermediate `Type x = ...; Type y = ...;` lines |

**Deprecated/outdated:**
- The single-column LSP `Range(loc, col+1)` (LspMappings.cs:22-26) becomes deprecated post-Span-migration — should expand to use the full Span. Not a Phase 35 deliverable but flag for v1.5 LSP polish phase (Phase 38's REPL polish work touches LSP).
- The shell-loop test invocation in CLAUDE.md "How to Run Tests" line should be replaced (post-Phase 35) with `flow test tests/`. CLAUDE.md update is a documentation cleanup item.

## Project Constraints (from CLAUDE.md)

CLAUDE.md directives that constrain Phase 35:

| Directive | Constraint on Phase 35 |
|-----------|------------------------|
| **Functional S-expression style; no infix arithmetic** | Match expression body and assertion functions must use prefix builtins (`(add x y)`, `(equals a b)`) — confirmed already required, no special action |
| **Charitable interpretation: silent-and-documented assumptions over errors** | Direct support for D-v1.5-05 — non-exhaustive match WARN+fall-through is THE canonical charitable interpretation. `(fast seq 0)` precedent (zero rate → unchanged sequence + stderr advisory) generalizes naturally. |
| **Ergonomics always wins** | `-> as name` is pure ergonomics — no functional capability gain, just less friction. Justifies the implementation complexity. |
| **Genre-agnostic, music-only scope** | Pattern matching's music-aware extractors (chord quality, roman numeral, articulation) are mandatory — pattern matching that only matched language constructs (Int, String, Bool) would fail the "music-only justification" filter. LANG-02 enforces this. |
| **Pre-traction no-deprecation latitude ACTIVE (D-v1.5-01)** | Span migration may add Span as a new field without keeping a legacy "no-Span" code path. AST records can rev shape in one commit per the existing pattern. |
| **Pre-Phase-28 byte-identical determinism dropped; two-run determinism preserved; RMS-windowed ±0.5dB / 100ms tolerance** | `(assertWithinDb)` is the Flow-surface expression of this contract — directly wraps `RmsRegressionTests.AssertRmsWithinTolerance`. The framework formalizes existing C# behavior. |
| **GSD Workflow Enforcement: file-changing tools only via GSD commands** | Phase 35 work happens via `/gsd:execute-phase 35` — no direct edits |
| **Goals: easy cases fast; flexible cases flexible** | Naive pattern-match decision (Open Question 1): "easy cases fast" supports linear-scan with small arm count; "flexible cases flexible" supports decision-tree later if profiling demands |

No CLAUDE.md directive blocks any Phase 35 deliverable.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit.v3 3.2.2 (already in flow-lang.Tests/) for C# tests; new pure-Flow test framework (TEST-01) for `.flow` tests |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` (existing, no changes) |
| Quick run command (per-task commit) | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase35"` |
| Full suite command (per-wave merge) | `dotnet test` (runs all 1003+ existing tests) + `for f in tests/test_*.flow; do dotnet run --project flow-interpreter "$f"; done` (existing convention) |
| Phase 35 framework self-test | `dotnet run --project flow-cli -- test tests/test_test_framework.flow` (meta-test of the new framework via the new framework — runnable AFTER Wave 2 lands) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| LANG-01 | Pattern matching expression with literal/constructor/wildcard/guard arms | unit | `dotnet test --filter "FullyQualifiedName~PatternMatchingTests"` | ❌ Wave 3 |
| LANG-01 | Non-exhaustive WARN+fallthrough; `enable matchExhaustive;` promotes to error | unit | `dotnet test --filter "FullyQualifiedName~MatchExhaustivenessTests"` | ❌ Wave 3 |
| LANG-01 | Match arms independent — no C-style fall-through | unit | `dotnet test --filter "Name~ArmsIndependent"` | ❌ Wave 3 |
| LANG-02 | Chord quality / roman numeral / articulation symbol extractors | unit | `dotnet test --filter "FullyQualifiedName~MusicAwarePatternsTests"` | ❌ Wave 3 |
| LANG-03 | `-> as name` parses; binds visible in subsequent chain steps | unit | `dotnet test --filter "FullyQualifiedName~ChainNamingTests"` | ❌ Wave 4 |
| LANG-03 | `as name` scope visibility (until end of enclosing block/function) | unit | `dotnet test --filter "Name~AsNameScope"` | ❌ Wave 4 |
| LANG-04 | Rust-style diagnostic rendering with source quote + caret + label + notes + suggestion | golden-file | `dotnet test --filter "FullyQualifiedName~SnippetRendererTests"` | ❌ Wave 2 |
| LANG-04 | Did-you-mean Levenshtein suggestion within max(2, len/3) distance | unit | `dotnet test --filter "Name~DidYouMean"` | ❌ Wave 2 |
| LANG-04 | Span migration: all 1003+ existing tests remain GREEN | regression | `dotnet test` (full suite) | ✅ existing |
| LANG-04 | Every AST node carries non-Unknown Span post-parse | unit | `dotnet test --filter "Name~AllNodesHaveSpan"` | ❌ Wave 1 |
| TEST-01 | `(test "name" body)` registers; body deferred via Thunk | unit | `dotnet test --filter "FullyQualifiedName~TestFrameworkMetaTests"` | ❌ Wave 2 |
| TEST-01 | All 5 assert primitives: `assert`, `assertEq`, `assertNotesMatch`, `assertBytesEqual`, `assertWithinDb` | unit | `dotnet test --filter "Name~AssertPrimitives"` | ❌ Wave 2 |
| TEST-01 | `flow test [path]` discovers + runs tests; exit code reflects pass/fail | integration | `dotnet test --filter "FullyQualifiedName~FlowTestCommandTests"` | ❌ Wave 2 |
| TEST-02 | Hermetic isolation — musical context stack reset between tests | unit | `dotnet test --filter "Name~HermeticMusicalContext"` | ❌ Wave 2 |
| TEST-02 | Hermetic isolation — voice pool, PRNG, bindings reset | unit | `dotnet test --filter "Name~HermeticVoicePoolPrngBindings"` | ❌ Wave 2 |
| TEST-02 | Test order independence — randomize order, all still pass | unit | `dotnet test --filter "Name~TestOrderIndependent"` | ❌ Wave 2 |
| HK-01 | humanizeGaussian over voice block produces non-empty WAV with audible samples | regression | `dotnet test --filter "FullyQualifiedName~HK01HumanizeGaussianBugTests"` + `tests/test_humanize_voice_block.flow` | ❌ Wave 1 |
| HK-02 | Phase 17 HUMAN-UAT.md rows 1-3 status reflects closure | manual-only | Read `.planning/phases/17-flow-language-server/17-HUMAN-UAT.md`; verify rows 1-3 show `[pass-via-phase-31-uat]` + `closed_via: Phase 31 Plan 31-08 UAT` (already true — confirm + document in plan summary) | ✅ already closed; cleanup-only |
| HK-03 | Phase 04 REQUIREMENTS.md COMP-01/COMP-02 checkboxes + VariationFunctions.MutateRhythm enum fix | regression | `grep "\[x\] \*\*COMP-01" .planning/REQUIREMENTS.md` + `dotnet test --filter "Name~MutateRhythmEnumValues"` | ❌ Wave 1 |
| HK-04 | CLAUDE.md "Public as of v1.4" footnote matches `project_pre_public_no_legacy_burden` framing | manual-only | `grep -A 8 "Public as of v1.4" CLAUDE.md` + visual verify against `~/.claude/projects/.../memory/project_pre_public_no_legacy_burden.md` | ❌ Wave 1 |

### Sampling Rate

- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Phase35"` (Phase 35 unit tests only — fast; < 10s expected)
- **Per wave merge:** `dotnet test` (full 1003+ unit suite) + existing `.flow` script run loop. Span migration's blast radius makes the full suite mandatory at every wave merge.
- **Phase gate:** Full suite green + new `flow test tests/` green + all manual-only items (HK-02, HK-04) checked off in VERIFICATION.md.

### Wave 0 Gaps

- [ ] `flow-lang.Tests/Unit/SnippetRendererTests.cs` — golden-file diagnostic output (LANG-04)
- [ ] `flow-lang.Tests/Unit/PatternMatchingTests.cs` — LANG-01 parser + evaluator
- [ ] `flow-lang.Tests/Unit/MatchExhaustivenessTests.cs` — D-v1.5-05 charitable + strict modes
- [ ] `flow-lang.Tests/Unit/MusicAwarePatternsTests.cs` — LANG-02 chord / roman / articulation
- [ ] `flow-lang.Tests/Unit/ChainNamingTests.cs` — LANG-03
- [ ] `flow-lang.Tests/Unit/TestFrameworkMetaTests.cs` — TEST-01/02 (C# tests that the Flow test framework works — meta-tests via xUnit, NOT recursive `(test ...)`)
- [ ] `flow-lang.Tests/Unit/SpanMigrationTests.cs` — every AST node has non-Unknown Span after parse (LANG-04 Span retrofit safety net)
- [ ] `flow-lang.Tests/Integration/Phase35/HK01HumanizeGaussianBugTests.cs` — voice-block regression
- [ ] `flow-lang.Tests/Helpers/RmsComparator.cs` — extracted public-facing RMS comparison helper that the `(assertWithinDb)` Flow builtin can call (current `RmsRegressionTests` depends on Xunit.Assert)
- [ ] `flow-lang/Diagnostics/SnippetRenderer.cs` — implementation
- [ ] `flow-lang/Diagnostics/FlowDiagnostic.cs` — rich diagnostic record (primary span + labels + notes + suggestions)
- [ ] `flow-lang/Diagnostics/LevenshteinHelper.cs` — extracted from PragmaRegistry
- [ ] `flow-lang/Core/Span.cs` — new Span record
- [ ] `flow-lang/Core/SourceMap.cs` — keyed source-text registry
- [ ] `flow-lang/Ast/Patterns/` directory + 6 pattern types
- [ ] `flow-lang/Ast/Expressions/MatchExpression.cs` — new AST node
- [ ] `flow-lang/test.flow` — composer-facing `@test` module wrappers (per TEST-01 "new @test stdlib module")
- [ ] `flow-cli/Commands/TestCommand.cs` — `flow test` subcommand
- [ ] `tests/test_humanize_voice_block.flow` — HK-01 regression (composer-facing)
- [ ] `tests/test_test_framework.flow` — TEST-01 dogfooding test

*Framework install: not needed — xUnit.v3 3.2.2 already pinned.*

## Security Domain

> Phase 35 is purely internal language-tooling work. Surface area is parser/interpreter/test-CLI — no network, no file I/O beyond the existing module-loader, no credential handling. ASVS categories below confirmed not-applicable for this phase.

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | n/a — no auth surface |
| V3 Session Management | no | n/a — no session surface |
| V4 Access Control | no | n/a — single-user CLI tool |
| V5 Input Validation | partial | Existing — Parser already rejects malformed input; new pattern parser inherits that posture. `flow test [path]` accepts a filesystem path — sanitize via `System.IO.Path.GetFullPath` (already done by `CheckCommand`'s `FileInfo` argument; `TestCommand` mirrors). |
| V6 Cryptography | no | n/a — no crypto surface. The PRNG reset path uses `System.Random` which is documented (per Phase 25) as non-cryptographic. |

### Known Threat Patterns for the C# language-tooling stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal in `flow test ../../etc/passwd` | Information Disclosure | `FileInfo` arg type (existing CheckCommand pattern) + `Path.GetFullPath` normalization. No file-system writes by `flow test`; reads only `.flow` files matching `test_*.flow` glob. |
| Source-text-injection in diagnostic rendering | Tampering | None needed — diagnostic renderer outputs to local stderr only; no network egress; ANSI escape sequences in source-quote text are passed through (composer's own source). Acceptable per single-user CLI threat model. |
| Test framework state leakage between tests | Integrity (test correctness) | `ExecutionContext.SnapshotState/RestoreState` per TEST-02 (the entire point of the feature) |

## Sources

### Primary (HIGH confidence)

- `/home/noah/Desktop/projects/flow-sharp/CLAUDE.md` — project-wide invariants
- `/home/noah/Desktop/projects/flow-sharp/.planning/REQUIREMENTS.md` lines 1-42 — v1.5 locked decisions + Phase 35 REQs (verbatim)
- `/home/noah/Desktop/projects/flow-sharp/.planning/ROADMAP.md` lines 110-139 — Phase 35 framing + dependency map
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Ast/AstNode.cs` — confirms abstract `record AstNode(SourceLocation Location)` base + Expression / Statement subclasses
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Ast/Expressions/FunctionCallExpression.cs`, `LiteralExpression.cs`, `FlowExpression.cs` — confirms record shape with `SourceLocation Location` as positional param
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Lexing/Token.cs` — confirms defaulted-parameter precedent (`OriginalText` added without breaking existing 4-arg calls)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Lexing/SimpleLexer.cs` — 46 `new Token(...)` sites confirmed via grep; keyword table at line 850 (no `as` keyword currently)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Lexing/PragmaRegistry.cs` — Levenshtein impl at lines 60-84; `KnownPragmas` dict at 16-24
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Lexing/PragmaSet.cs` — per-file scope; threaded into Parser ctor (Phase 21 D-05)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Parsing/Parser.cs` — 1444 lines; `new <NodeType>(...)` sites total ~86 across Parser.cs + Parser.NoteStream.cs (verified via grep)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Parsing/Parser.cs:1043-1047` — note-stream `|` parsing happens ONLY from primary-expression start position (disambiguation context)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Diagnostics/ErrorReporter.cs` + `FlowError.cs` — current single-line error format; extension point clear
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Diagnostics/RenderingDiagnostics.cs` — one-shot stderr advisory with dedup; pattern for D-v1.5-05's match-non-exhaustive warning
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Runtime/ExecutionContext.cs` — confirms call-stack, `ResetGen`, no existing Snapshot/Restore API; current static-mutable surface enumerated for Pitfall 3
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/Runtime/StackFrame.cs:80` — `GetAllAccessibleVariables()` for did-you-mean scope enumeration
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/BuiltInFunctions.cs:339-347` — `if`/`ifStrict` LazyType + non-Lazy overloads; exact precedent for `(test ...)` and `(match ...)` body deferral
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:931-962` — HK-01 root cause: `HumanizeGaussian` iterates `bar.MusicalNotes` only, ignores `bar.ParallelVoices`; output `BarData(newNotes, ...)` drops voice blocks
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/TypeSystem/SpecialTypes/NoteType.cs:292` — `MusicalNoteData` constructor with 17+ defaulted params — Phase 22 precedent for extending records without breaking call sites
- `/home/noah/Desktop/projects/flow-sharp/flow-lang.Tests/Helpers/RmsRegressionTests.cs:25-56` — SPEC-8 ±0.5dB / 100ms tolerance reference for `(assertWithinDb)`
- `/home/noah/Desktop/projects/flow-sharp/flow-cli/Commands/CheckCommand.cs` — pattern for new `TestCommand`
- `/home/noah/Desktop/projects/flow-sharp/flow-interpreter/Program.cs:77,87,105,115,146` — `Console.ForegroundColor` TTY-handling precedent
- `/home/noah/Desktop/projects/flow-sharp/.planning/phases/17-flow-language-server/17-HUMAN-UAT.md` — confirms HK-02 rows 1-3 already closed via Phase 31 Plan 31-08 (status `[pass-via-phase-31-uat]`)
- `/home/noah/Desktop/projects/flow-sharp/.planning/phases/04-composition-tools/04-VERIFICATION.md` lines 6-25 — HK-03 specific gaps (COMP-01/COMP-02 checkbox + MutateRhythm enum bug)
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/project_v15_backlog.md` — HK-01 documented repro
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/project_pre_public_no_legacy_burden.md` — HK-04 target framing (CLAUDE.md footnote should match this rewritten memory)

### Secondary (MEDIUM confidence)

- [rustc-dev-guide: Errors and lints](https://rustc-dev-guide.rust-lang.org/diagnostics.html) — diagnostic format components: error level + code, main message, file location, source snippet, carets/dashes, primary/secondary labels, notes, suggestions
- [annotate-snippets-rs](https://github.com/rust-lang/annotate-snippets-rs) — Rust crate now powers rustc's diagnostics; format reference (not directly usable from C# but informs SnippetRenderer design)
- [codespan-reporting](https://github.com/brendanzab/codespan) — alternative Rust crate; `Files` trait inspires `SourceMap` design
- [Jules Jacobs: How to compile pattern matching (2021)](https://julesjacobs.com/notes/patternmatching/patternmatching.pdf) — paper cited by REQUIREMENTS.md LANG-01; the decision-tree compile target
- [Maranget: Compiling Pattern Matching to Good Decision Trees (2008)](http://moscova.inria.fr/~maranget/papers/ml05e-maranget.pdf) — foundational paper; algorithmic basis for Jacobs/Peterse
- [Yorick Peterse: pattern-matching-in-rust](https://gitlab.com/yorickpeterse/pattern-matching-in-rust/-/tree/main/jacobs2021) — practical Rust impl; algorithmic reference (not a dependency)

### Tertiary (LOW confidence — none used)

Phase 35 research did not rely on unverified single-source claims. Every claim about Flow internals was verified by reading the actual source.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Naive linear-scan pattern matcher is sufficient for v1.5 (decision-tree compile deferred to v1.6) | Architecture Patterns + Pitfall 9 | If composer uses match in a hot path (e.g., per-sample audio dispatch), linear scan could measurably slow render. Mitigation: profile during Phase 36 SECT-01 destructuring and Phase 40 MIDI dispatch; if hotspot found, swap backend (no surface impact). **Open Question 1.** |
| A2 | Test framework state-reset surface is the 11 items enumerated in Pitfall 3 | Pitfall 3 + Validation Architecture | If a 12th state-mutating static is missed, tests show order-dependent flakiness. Mitigation: write `TestOrderIndependent` test fact that randomizes 20 runs of the suite and asserts identical outcomes. |
| A3 | `as` is safe to introduce as contextual non-keyword | Pitfall 7 + LANG-03 analysis | Verified via grep — `as` is NOT in the lexer's keyword table (line 850-891 in SimpleLexer.cs). Risk is composers having user code with `as` as a variable name; they break in one commit per D-v1.5-01 latitude. |
| A4 | The Phase 22 defaulted-parameter pattern works for Span migration with no LSP / test breakage | Pattern 1 + Pitfall 1 | LSP's `LspMappings.ToRange(SourceLocation)` (LspMappings.cs:22-26) reads `SourceLocation` directly — unchanged by adding Span. Risk: any code that reads ResolvedType or other fields via destructuring positional records — none found in grep. |
| A5 | `(test ...)` with `LazyType` body wrapping works structurally like existing `(if ...)` | Pattern 2 + Example 3 | `(if)` is the precedent at BuiltInFunctions.cs:339. Risk: lazy semantics subtle interaction with implicit-return collector if test bodies have non-Void trailing expressions. Mitigation: test framework wraps body invocation in a new frame; ImplicitReturnCollector results discarded. |
| A6 | `RmsRegressionTests.AssertRmsWithinTolerance` can be extracted to a non-test-assembly C# helper for the `(assertWithinDb)` Flow builtin | Code Examples + Wave 0 Gaps | Current impl depends on `Xunit.Assert.Equal`. Refactor: extract pure comparison method returning `(maxDeviation, windowIdx)`; existing xUnit helper continues to assert; Flow builtin throws `AssertionException`. Both call sites consume the pure helper. |
| A7 | Pattern matching arms run with bindings in a pushed frame; bindings die on pop | Pitfall 6 + Example 2 | Standard scoping semantics; matches existing PushFrame/PopFrame lifecycle. Risk: none — well-precedented. |
| A8 | Hermetic isolation does NOT need to reset `AudioPlaybackManager` state (audio backend stays open across tests) | Pitfall 3 | Tests typically don't trigger live playback — they call `writeWav` or `renderSong` and compare buffers. Risk: a test that does `(play seq)` could leak. Mitigation: discuss-phase Open Question 4 — do we add `(stopAudio)` to the SnapshotState reset, or document it as "tests must not call live playback"? |
| A9 | Phase 17 HUMAN-UAT.md rows 1-3 are ALREADY closed per Phase 31 Plan 31-08 — HK-02 is documentation cleanup only | HK-02 in Validation Architecture | Verified via Read of 17-HUMAN-UAT.md frontmatter (`status: closed`, `closed_via: Phase 31 Plan 31-08 UAT`). HK-02's task is probably "confirm closure is reflected in roadmap + STATE.md" — clarify at discuss-phase. **Open Question 3.** |
| A10 | The `@test` stdlib module per TEST-01 is a thin wrapper for composer ergonomics; the BUILT-INs live in C# | Architecture + Code Examples | TEST-01 says "Assert primitives ship in new `@test` stdlib module". Interpretation: the `(test)`, `(assert)`, etc. functions are registered as C# builtins (in `BuiltInFunctions.cs`), and `flow-lang/test.flow` exists as a placeholder `.flow` module so `use "@test"` works (per `@std` precedent — `std.flow` is a re-export file). Risk: composer might expect more docstrings/wrappers in test.flow. Mitigation: write minimal test.flow to satisfy `use "@test"`; expand later as composer requests. |

**If discuss-phase confirms all 4 open questions land as recommended above:** Assumptions A1, A3, A8, A9 collapse from `[ASSUMED]` to `[VERIFIED via composer decision]`.

## Open Questions (RESOLVED)

All five questions were resolved 2026-05-18 during Phase 35 plan-checker iteration. Resolutions are recorded inline below and encoded in the locked decisions D-v1.5-11 (Q1) and the plan actions themselves (Q2-Q5). No further composer input required; planning may proceed.

1. **Pattern-matching backend: naive linear scan in v1.5, decision-tree in v1.6 — confirm?** **RESOLVED (2026-05-18):** Naive linear scan in Phase 35; Jacobs/Peterse decision-tree compile deferred to v1.6. Authorized by composer + locked as **D-v1.5-11** in REQUIREMENTS.md / ROADMAP.md. Rationale: aligns with CLAUDE.md "Ergonomics first / make the easy cases fast"; naive backend is surface-equivalent; D-v1.5-01 pre-traction no-deprecation latitude allows the v1.6 internal swap with no API change. Plan 35-05 ships naive scan.

2. **Hermetic isolation: which static-mutable sites get reset between tests?** **RESOLVED (2026-05-18):** Reset the full 11-site set from Pitfall 3 (ExecutionContext bindings + musical-context stack + PRNG state including SynthUtils.Rng + SymbolInternTable + voice pool + Sfz statics + FlowConfig.Active + RenderingDiagnostics dedup). Do NOT reset `AudioPlaybackManager` — instead document "tests must not trigger live playback (`play`/`loop`/`preview`)" as a Pitfall in CLAUDE.md's test-framework section. Plan 35-04 Task 2 encodes the snapshot/restore surface; Plan 35-04 final task adds the CLAUDE.md pitfall note.

3. **HK-02 closure mechanism — is doc-update sufficient?** **RESOLVED (2026-05-18):** Documentation-only closure is sufficient. Phase 17 HUMAN-UAT.md rows 1-3 already show `status: closed` / `closed_via: Phase 31 Plan 31-08 UAT`. HK-02 task is: (a) verify the closure markers exist, (b) update REQUIREMENTS.md HK-02 checkbox to checked with a reference to Phase 31 Plan 31-08 as closure mechanism, (c) update STATE.md phase-17 status if it disagrees. Plan 35-02 encodes this as a 3-step task.

4. **`(test ...)` body trailing-expression semantics — Void enforced or implicit-return-collected and discarded?** **RESOLVED (2026-05-18):** Silently discard the implicit-return-collected value. No advisory. Rationale: aligns with CLAUDE.md "Ergonomics first" + the existing implicit-return-collector contract (multiple non-Void trailing values already become an array — adding test-specific warnings would be a special case). If a composer wants assertion-by-return-value, they explicitly call `(assert ...)`. Plan 35-04 Task 1 (TestFramework registration) encodes this — the `(test ...)` builtin's body evaluator discards the implicit-return value without inspection.

5. **`-> as name` precedence + nesting — `seq -> (f) as a -> (g a) as b -> render` vs `seq -> ((f) as a) -> ...`?** **RESOLVED (2026-05-18):** `as` is right-associative-with-`->`. Keyword sequence is `EXPR -> CALL as NAME -> ...` only. Parenthesized form `(EXPR as NAME)` not supported in Phase 35 (no compelling use case; deferred indefinitely). Plan 35-07 parser only accepts `as NAME` immediately after a `-> CALL` site; any other position is a parse error with a diagnostic suggesting the supported form.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Build + test | ✓ | net10.0 | — |
| `dotnet test` | flow-lang.Tests | ✓ | matches SDK | — |
| `dotnet run` | flow-interpreter | ✓ | matches SDK | — |
| xUnit.v3 3.2.2 | flow-lang.Tests existing fact suite | ✓ | 3.2.2 | — |
| System.CommandLine | flow-cli subcommand registration | ✓ | (project ref via flow-cli.csproj) | — |
| PulseAudio | Audio playback tests | ✓ (Linux dev env) | system | Mock backend exists for CI |

**Missing dependencies with no fallback:** None — Phase 35 is purely internal language tooling work.

**Missing dependencies with fallback:** None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all components either already in use (Token/PragmaSet/LazyType/StackFrame patterns) or trivial extensions (Span as defaulted record param)
- Architecture: HIGH for span migration + test framework + diagnostics renderer (precedents in-codebase); MEDIUM for pattern matching (new conceptual surface — bounded by naive-linear-scan decision)
- Pitfalls: HIGH — every pitfall is traced to specific code lines or memory documentation
- HK-01..04: HIGH — each housekeeping item has a documented root cause, code site, or doc location

**Research date:** 2026-05-18
**Valid until:** 2026-06-17 (30 days — Flow internals stable; rustc diagnostic format stable; decision-tree paper unchanged since 2021)
