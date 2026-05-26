# Phase 43: Module Names & Qualified Imports — Research

**Researched:** 2026-05-24
**Domain:** Flow Language interpreter — lexer/parser/AST extension, ModuleLoader registry plumbing, ExpressionEvaluator dispatch order, OverloadResolver music-type backfill, charitable-advisory wiring
**Confidence:** HIGH (all findings verified directly against the codebase at HEAD; CONTEXT.md decisions are load-bearing and copied verbatim below)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Syntax**
- **D-01:** `module <name>` declaration is the first non-comment statement of a `.flow` file. The `module` keyword becomes reserved. Module names follow identifier rules (`[a-zA-Z_][a-zA-Z0-9_]*`). Files without a `module` declaration are NOT registered for qualified access; their procs still export into the unqualified namespace as today (preserves existing composer-script + test-script behavior).
- **D-02:** Qualified access uses the existing `x.y` `MemberAccessExpression` AST node. No new AST surface. Disambiguation between module-qualified call (`math.sin(0.5)` → call `sin` from `math`) and existing instance-member access (`chord.root` → field/method on `Chord` value) happens at evaluation time in `ExpressionEvaluator`: the LHS is checked against the module registry FIRST; if the LHS is a registered module name, dispatch as a module-qualified function call; otherwise fall through to the existing member-access dispatch path. Backward-compatible — existing `chord.root` / `song.sections` / `<<a, b>>@0` patterns are unaffected.
- **D-03:** Reserved keyword choice: **`module`**. Familiar from Haskell / OCaml / F# / Rust. Single obvious form. No alternatives (`mod`, `namespace`, `pkg`, etc.).

**Resolution & Collision**
- **D-04:** **Last-import-wins** for unqualified names. When `use "@a"` and `use "@b"` both export `sin`, the later `use` line's `sin` wins for unqualified calls. Emit one-shot stderr advisory:
  ```
  [module] 'sin' from 'b' shadows 'sin' from 'a' — qualify with 'a.sin' or 'b.sin' to disambiguate
  ```
- **D-05:** Module registration happens at `use` time. The `ModuleLoader` already loads the file and runs its top-level statements; it should additionally parse the `module <name>` declaration and register `name → ExportedProcSet` in a process-global `ModuleRegistry`. `use "@x"` continues to expose the procs in the caller's `ExecutionContext` UNQUALIFIED (back-compat); the qualified-access path is an additional lookup.
- **D-06:** Duplicate module-name registrations (two files declare `module math`): emit one-shot advisory `[module] duplicate module name 'math' — last load wins`.

**Stdlib Module Assignments**
- **D-07:** Migrate the 13 `flow-lang/*.flow` stdlib files. The recommended default:

  | File | Module | Notes |
  |------|--------|-------|
  | `audio.flow` | `audio` | obvious |
  | `bars.flow` | `bars` | obvious |
  | `collections.flow` | `collections` | obvious |
  | `composition.flow` | `composition` | obvious |
  | `generative.flow` | `generative` | obvious |
  | `improv.flow` | `improv` | obvious |
  | `notation-io.flow` | `notation` | export/import (MusicXML, LilyPond, ABC, MML) |
  | `notation.flow` | `notes` | mostly bar-level Note helpers — renamed |
  | `osc.flow` | `osc` | obvious |
  | `patterns.flow` | `patterns` | obvious |
  | `sfz.flow` | `sfz` | obvious |
  | `std.flow` | _no module declaration_ | always-on prelude — keeps existing unqualified-only behavior |
  | `test.flow` | `test` | obvious |

  The `notation.flow` ↔ `notation-io.flow` collision is the only real conflict — see §"`notation.flow` vs `notation-io.flow` Resolution" below for the recommendation.

**Beat Backfill (AUDIT §7a HIGH-priority concurrent work)**
- **D-08:** New free builtins:
  - `(beatToSec Beat) → Second` — reads active `tempo` from `ExecutionContext.MusicalContext` stack.
  - `(secToBeat Second) → Beat` — reads active `tempo`.

  When no `tempo` is active, default to `120 BPM` AND emit one-shot stderr advisory `[beatToSec] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)`. Two-run cmp-clean preserved (default is deterministic).
- **D-09:** Beat-companion overloads ONLY where AUDIT §1 lists them:
  - `delay(Buffer, Beat)` — implemented as `delay(buf, beatToSec(beat))`.
  - `renderBarAtBeat(Sequence, Beat)` — converts to seconds before invoking the existing bar-render path.

  Do NOT speculatively add Beat overloads elsewhere.
- **D-10:** `AuditHarnessTests.OrphanList_ContainsBeatType` (line ~232) currently `Assert.Contains("BeatType", snap.CoercibleOrphans)`. After Phase 43 ships the Beat-companion overloads, this flips to `Assert.DoesNotContain("BeatType", snap.CoercibleOrphans)`. Update test in lockstep with the production change.

**Migration Tooling**
- **D-11:** Pre-traction no-deprecation latitude ACTIVE. Breaking syntax ships in one commit. In-repo `.flow` migrators are sufficient. Migration touches the 13 stdlib files + any test scripts that surface a regression.
- **D-12:** No composer-facing `flow migrate` CLI subcommand.

### Claude's Discretion

- Where the registry lives (process-global vs. per-FlowEngine). Either is defensible — planner picks based on existing `InternalFunctionRegistry` patterns.
- Exact stderr advisory wording (pattern `[module] ...` is fixed; specific phrasing planner-discretion).
- Whether the module-registry lookup happens in `ExpressionEvaluator` directly or via a `MemberAccessResolver` helper class.
- Test-fixture file layout: `flow-lang.Tests/Integration/Phase43/` per Phase 42's precedent.

### Deferred Ideas (OUT OF SCOPE)

- **`pitchShift(Buffer, Hertz)` → v1.6-backlog** — Hertz-shift semantics differ from cents-relative; better as a new builtin `pitchShiftTo(Buffer, Hertz, refHz)`.
- **Cross-module re-exports** (`module foo re-exports @bar`) — v1.6+.
- **Module aliasing on import** (`use "@math" as m`) — v1.6+.
- **Composer-facing `flow migrate` subcommand** — v1.6+ or when traction appears.
- **Strict-mode flips of advisories from D-04 / D-06 / D-08** — Phase 44.
- **Whether `notation.flow` should merge into `notation-io.flow`** — flagged in D-07; this RESEARCH recommends rename-to-`notes`, see §"`notation.flow` vs `notation-io.flow` Resolution".

</user_constraints>

<phase_requirements>
## Phase Requirements

REQUIREMENTS.md does not yet list Phase 43 REQs. Proposed REQ-MOD-NN synthesis (planner may rewire):

| ID | Description | Research Support |
|----|-------------|------------------|
| REQ-MOD-01 | `module <name>` declaration — first non-comment statement of a `.flow` file. Reserved keyword `module`. Identifier-rule names. Files without a declaration are NOT registered (back-compat). | §"Parser Surface" + §"Lexer Surface" |
| REQ-MOD-02 | Qualified access `module.fn` evaluated via existing `MemberAccessExpression` AST. Registry-first lookup; instance-member fallback. | §"ExpressionEvaluator Dispatch Order" |
| REQ-MOD-03 | `use "@x"` registers `x → ExportedProcSet` in a process-global `ModuleRegistry` AND continues to inject procs unqualified (back-compat). | §"ModuleLoader Registry Hook" |
| REQ-MOD-04 | Unqualified collision = last-import-wins + one-shot advisory `[module] '{name}' from '{b}' shadows ... — qualify with '{a}.{name}' or '{b}.{name}'`. | §"Charitable Advisory Wiring" |
| REQ-MOD-05 | Duplicate module-name registration = one-shot advisory `[module] duplicate module name '{name}' — last load wins`. | §"Charitable Advisory Wiring" |
| REQ-MOD-06 | 13 stdlib `.flow` files migrated per D-07 table. `notation.flow` → `module notes`; `notation-io.flow` → `module notation`; `std.flow` stays declaration-less. | §"Stdlib Migration Plan" |
| REQ-MOD-07 | `(beatToSec Beat) → Second` builtin reads `ExecutionContext.GetMusicalContext().Tempo ?? 120.0`; default-120-BPM path emits one-shot advisory. | §"Beat Backfill Implementation" |
| REQ-MOD-08 | `(secToBeat Second) → Beat` builtin — symmetric pair. Same 120-BPM default + advisory. | §"Beat Backfill Implementation" |
| REQ-MOD-09 | `delay(Buffer, Beat, Double, Double)` overload — registered via existing `RegisterContextDependent` pattern, internally delegates to `Delay.Apply(buf, beatToSec(beat), feedback, mix)`. | §"Beat-Companion Overload Recipe" |
| REQ-MOD-10 | `renderBarAtBeat(Bar, Beat, String, Int, Double)` overload — registered alongside the existing Double overload; converts Beat→Second→beat-offset internally. | §"Beat-Companion Overload Recipe" |
| REQ-MOD-11 | `AuditHarnessTests.OrphanList_ContainsBeatType` polarity flip in the SAME commit as REQ-MOD-09/10 — Phase 42 fixture becomes the regression gate. | §"Audit Harness Polarity Flip" |
| REQ-MOD-12 | New xUnit fixtures at `flow-lang.Tests/Integration/Phase43/` cover: parser tests for `module <name>`, dispatch tests for module-qualified vs instance-member access, ModuleLoader tests for registry + duplicate advisory, Beat builtin tests (default-tempo path + advisory dedup), `.flow` integration round-trip. | §"Validation Architecture" |

</phase_requirements>

## Summary

Phase 43 adds a small parser surface (`module <name>` top-of-file statement + `module` reserved keyword), a tiny runtime registry hook (`ModuleLoader` parses the declaration and registers `name → ExportedProcSet` in a process-global `ModuleRegistry`), and a single dispatch-order change in `ExpressionEvaluator.EvaluateMemberAccess` (registry-first lookup, instance-member fallback). The mechanism is intentionally narrow — `use "@x"` continues to expose procs unqualified by default, so composers don't pay any ergonomic tax for the new disambiguation tool until they choose to qualify a call.

Concurrent work closes the AUDIT §1 Beat-orphan anchor finding: two new context-aware builtins (`beatToSec` / `secToBeat`) reading `ExecutionContext.GetMusicalContext().Tempo ?? 120.0` (the established Phase 22 `RegisterContextDependent` pattern at `EffectsFunctions.cs:359-389`), plus two Beat-companion overloads on `delay` and `renderBarAtBeat`. The Phase 42 `OrphanList_ContainsBeatType` fixture flips polarity in the same commit and becomes the regression gate.

**Primary recommendation:** Land the changes in this fixed dependency order — (1) add `module` TokenType + lexer keyword + `ModuleDeclarationStatement` AST + `ParseModuleDeclaration` parser branch; (2) add `ModuleRegistry` on `ExecutionContext`, extend `ModuleLoader.LoadModule` to capture the first-statement `module` and register the exports; (3) extend `ExpressionEvaluator.EvaluateMemberAccess` with the registry-first branch; (4) ship `beatToSec` / `secToBeat` + `RegisterContextDependent` Beat-overloads (delay + renderBarAtBeat); (5) migrate the 13 stdlib `.flow` files in one commit; (6) flip the Phase 42 audit fixture in the same commit as step 4. Each step independently testable; (3) and (4) can ship in parallel waves.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `module <name>` syntax | Lexer + Parser | AST (new `ModuleDeclarationStatement` record) | Single new keyword + a top-of-file statement form; smallest surface that preserves D-01 semantics. |
| Module registration | Runtime (`ModuleLoader` + new `ModuleRegistry` field on `ExecutionContext`) | — | `use` already runs the module's top-level statements via `Interpreter.Execute`; we just need to capture the first declaration. |
| Qualified-access dispatch | Interpreter (`ExpressionEvaluator.EvaluateMemberAccess`) | — | D-02 reuses `MemberAccessExpression`; only the dispatcher needs a registry-first check. |
| Collision detection + advisory | Runtime (`ModuleLoader` or `ExecutionContext` symbol-registration site) | Diagnostics (`RenderingDiagnostics.WarnOnce`) | Last-import-wins is a charitable-default; advisory routed through the 117-site WarnOnce infrastructure. |
| Beat conversion builtins | StandardLibrary (`Audio/MusicalConversions.cs` is the obvious home — already has tempo-aware conversions) | Runtime (`ExecutionContext.GetMusicalContext()`) | Pitfall 3 from AUDIT: pure `FlowType` methods can't read runtime context; must be a builtin. |
| Beat-companion overloads | StandardLibrary (existing `EffectsFunctions.cs` for `delay`; `BuiltInFunctions.cs` for `renderBarAtBeat`) | `OverloadResolver` (specificity-scoring) | Phase 26.2 ERG-02 precedent: add a new `FunctionSignature` row, delegate to existing lambda after beat→seconds conversion. |

## Standard Stack

### Core (no new packages; this phase touches interpreter internals + stdlib)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| (none) | — | All Phase 43 work is internal to `flow-lang/` — no new NuGet packages. | Matches Phase 42's "zero new packages" posture and Flow's strict-minimum-deps rule (CLAUDE.md "External deps: only Pidgin + Melanchall.DryWetMidi"). |

### Supporting (existing infrastructure used as building blocks)

| Component | Location | Purpose | When to Use |
|-----------|----------|---------|-------------|
| `RenderingDiagnostics.WarnOnce(sentinelKey, message)` | `flow-lang/Diagnostics/RenderingDiagnostics.cs:29` | One-shot stderr advisory with per-process per-key dedup. | D-04 collision advisory + D-06 duplicate-module advisory + D-08 default-120-BPM advisory. 117 existing sites — well-precedented. |
| `ExecutionContext.GetMusicalContext()` | `flow-lang/Runtime/ExecutionContext.cs:440` | Walks the call stack to resolve the active `MusicalContext`. Returns `Tempo ?? null` from the resolved frame. | `beatToSec` / `secToBeat` read `.Tempo ?? 120.0` — exactly mirrors `EffectsFunctions.cs:378`. |
| `RegisterContextDependent` pattern | `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs:359-389` (delay NoteValue overload) | Closure captures `ExecutionContext` so each call reads tempo fresh. Closures bypass the stateless `args =>` shape. | `beatToSec` and `secToBeat` must use this pattern. `RegisterContextDependentFunctions` is called from `BuiltInFunctions.cs` (Phase 22 wiring). |
| `InternalFunctionRegistry.Register(name, signature, lambda)` | `flow-lang/StandardLibrary/InternalFunctionRegistry.cs:14` | Adds an overload row. Multiple `Register` calls with the same name and distinct signatures = multiple overloads. | All four new signatures (`beatToSec`, `secToBeat`, `delay(Buffer, Beat, ...)`, `renderBarAtBeat(Bar, Beat, ...)`). |
| `FunctionSignature` with `ParameterNames` | `flow-lang/TypeSystem/FunctionSignature.cs` | Phase 36 D-36-11 — every builtin now has parameter-name metadata for named-arg dispatch. | All four new signatures MUST include `ParameterNames:` per the ~150-builtin backfill convention. |
| `MemberAccessExpression(Object, MemberName)` | `flow-lang/Ast/Expressions/MemberAccessExpression.cs` | Existing 3-field record; `Object` is an `Expression` (often `VariableExpression`), `MemberName` is the RHS identifier. | D-02 reuses this verbatim; no schema change. |
| `ModuleLoader.LoadModule` | `flow-lang/Runtime/ModuleLoader.cs:48` | Resolves path, lexes/parses/executes the file, tracks circular-import set. | Extended to inspect the parsed `Program.Statements[0]` for a `ModuleDeclarationStatement` and call `context.ModuleRegistry.Register(...)`. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| New `ModuleDeclarationStatement` AST record | Stuff `module <name>` into existing `ExpressionStatement` with a tagged literal | A real AST node is clearer for downstream LSP / `flow doc` consumers (Phase 41 DOC-01). Cost is one new file — negligible. **RECOMMENDED: new AST record.** |
| Process-global static `ModuleRegistry` | Per-`ExecutionContext` field (mirrors `PrngRegistry`, `LiveBlockRegistry`, `StyleRegistry`) | Per-context = clean test isolation, matches every other Phase 28+/36+ registry pattern. Process-global = simpler but breaks `FlowEngine.SnapshotState()` (Phase 35 TEST-02). **RECOMMENDED: per-ExecutionContext field**, mirrors lines 91/141/156/177 of `ExecutionContext.cs`. |
| `MemberAccessResolver` helper class | Inline check inside `EvaluateMemberAccess` | The dispatcher is already a switch-style cascade (`obj.Data is Voice`, `Track`, `ChordData`, ...). Adding a registry check at the top of the method is consistent. Helper class is over-engineering for one branch. **RECOMMENDED: inline.** |
| `ExportedProcSet` as separate type | Use `StackFrame` directly (modules already execute into the caller's context) | Phase 43 needs a *namespaced* view that doesn't pollute the unqualified scope. Cheapest: store a `Dictionary<string, Value>` snapshot of the *delta* in user-defined functions added during the `use` call, captured by diffing the `StackFrame.Functions` count before/after `Interpreter.Execute(program)`. |

**Installation:**
No new packages. All changes are internal to `flow-lang/`.

**Version verification:** N/A — no new external deps.

## Package Legitimacy Audit

> **Not applicable** — Phase 43 installs no external packages.

## Architecture Patterns

### System Architecture Diagram

```
.flow source file
       │
       ▼
┌─────────────────────────────────────────────────┐
│  Lexer (SimpleLexer.cs)                          │
│   • "module" → TokenType.Module (NEW)            │
│   • everything else unchanged                    │
└─────────────────────────────────────────────────┘
       │ Token[]
       ▼
┌─────────────────────────────────────────────────┐
│  Parser (Parser.cs)                              │
│   • ParseStatement: if (Match(TokenType.Module)) │
│       return ParseModuleDeclaration() (NEW)      │
│   • Allowed ONLY as Statements[0] of a Program   │
│     (errors otherwise — D-01 semantics)          │
│   • module.fn — falls through existing           │
│     MemberAccessExpression path (no parser       │
│     change required per D-02)                    │
└─────────────────────────────────────────────────┘
       │ AST (Program with optional leading
       │      ModuleDeclarationStatement)
       ▼
┌─────────────────────────────────────────────────┐
│  ModuleLoader.LoadModule (NEW BRANCH)            │
│   • Detect Statements[0] is ModuleDeclaration   │
│     → register name → ExportedProcSet            │
│     in context.ModuleRegistry (NEW)              │
│   • Snapshot StackFrame.Functions BEFORE         │
│     Interpreter.Execute; diff AFTER to build     │
│     the ExportedProcSet                          │
│   • Duplicate name → WarnOnce advisory (D-06)    │
└─────────────────────────────────────────────────┘
       │ ExecutionContext updated:
       │   • ModuleRegistry["math"] = procs
       │   • StackFrame still gets unqualified procs
       │     (back-compat per D-05)
       ▼
┌─────────────────────────────────────────────────┐
│  Interpreter.ExecuteImport (unchanged)           │
└─────────────────────────────────────────────────┘

═══════════════════════════════════════════════════
At call time:

`math.sin(0.5)`
       │
       ▼
┌─────────────────────────────────────────────────┐
│  ExpressionEvaluator.EvaluateMemberAccess (NEW   │
│  REGISTRY-FIRST BRANCH)                          │
│                                                  │
│  IF member.Object is VariableExpression(name)    │
│     AND context.ModuleRegistry.Contains(name)    │
│     AND parent expression is FunctionCall:       │
│       → look up MemberName in module's procs    │
│       → return Function Value (caller invokes)  │
│  ELSE:                                           │
│     → existing instance-member dispatch         │
│       (Voice / Track / Chord / Bar / Section /  │
│        Song / reflection fallback)              │
└─────────────────────────────────────────────────┘

═══════════════════════════════════════════════════
Beat backfill (concurrent work):

`(delay buf 0.5b 0.3 0.5)`  where `0.5b` is BeatType
       │
       ▼
┌─────────────────────────────────────────────────┐
│  OverloadResolver.Resolve                        │
│   • Candidates for "delay": 3 existing           │
│     (Double, Millisecond, NoteValue) + 1 NEW    │
│     (Beat). Specificity scoring picks the Beat  │
│     overload at +1000 (exact match).             │
└─────────────────────────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────────────────┐
│  New Beat-overload lambda (captures context)     │
│   1. seconds = context.GetMusicalContext()      │
│                  .Tempo ?? 120.0 (advisory if   │
│                  default fires)                  │
│   2. delayMs = beatValue * (60000.0 / bpm)      │
│   3. Delay.Apply(buf, delayMs, feedback, mix)   │
└─────────────────────────────────────────────────┘
```

### Recommended Project Structure

```
flow-lang/
├── Ast/Statements/
│   └── ModuleDeclarationStatement.cs       # NEW — 3-field record (Location, Name, Span?)
├── Lexing/
│   ├── SimpleLexer.cs                       # MODIFIED — add "module" → TokenType.Module
│   └── TokenType.cs                         # MODIFIED — add `Module` enum value
├── Parsing/
│   └── Parser.cs                            # MODIFIED — ParseModuleDeclaration + Statements[0] enforcement
├── Runtime/
│   ├── ExecutionContext.cs                  # MODIFIED — add `ModuleRegistry` property
│   ├── ModuleRegistry.cs                    # NEW — Dictionary<string, ExportedProcSet> with thread-safety
│   └── ModuleLoader.cs                      # MODIFIED — capture module declaration + register
├── Interpreter/
│   ├── ExpressionEvaluator.cs               # MODIFIED — registry-first branch in EvaluateMemberAccess
│   └── Interpreter.cs                       # MODIFIED — handle ModuleDeclarationStatement (no-op at exec time; loader consumed it)
├── StandardLibrary/Audio/
│   └── MusicalConversions.cs                # MODIFIED — add beatToSec + secToBeat builtins via RegisterContextDependent
├── StandardLibrary/Audio/
│   ├── EffectsFunctions.cs                  # MODIFIED — add delay(Buffer, Beat, ...) overload in RegisterContextDependent
│   └── BuiltInFunctions.cs                  # MODIFIED — add renderBarAtBeat(Bar, Beat, ...) overload
├── *.flow                                    # MODIFIED — 12 stdlib files get `module <name>` declarations (std.flow stays declarationless)
└── notation.flow → renamed conceptually     # `module notes` per D-07 (file stays at notation.flow)

flow-lang.Tests/Integration/Phase43/
├── ModuleDeclarationParserTests.cs          # NEW — first-statement-only enforcement, identifier-rule names
├── ModuleRegistryDispatchTests.cs           # NEW — math.fn routes through registry; chord.root falls through
├── ModuleCollisionAdvisoryTests.cs          # NEW — last-import-wins + dedup-once stderr advisory
├── BeatBuiltinTests.cs                      # NEW — beatToSec/secToBeat with explicit tempo + default-120 advisory
└── BeatCompanionOverloadTests.cs            # NEW — delay(Beat) + renderBarAtBeat(Beat) round-trip

flow-lang.Tests/Integration/Phase42/
└── AuditHarnessTests.cs                     # MODIFIED — OrphanList_DoesNotContainBeatType (polarity flip per D-10)
```

### Pattern 1: Reserved-Keyword Add (precedent: `tempo`, `live`, `tuning`)

**What:** Add a keyword that the lexer maps to a dedicated `TokenType`. The parser then dispatches on the new token type during `ParseStatement`.
**When to use:** Any time a new top-level syntactic form needs special handling that an identifier-followed-by-arg doesn't cover.
**Example:**
```csharp
// SimpleLexer.cs line ~874 — add "module" to the keyword dictionary:
var type = text switch
{
    "module" => TokenType.Module,    // NEW — Phase 43 D-03
    "proc"   => TokenType.Proc,
    "use"    => TokenType.Use,
    "live"   => TokenType.Live,
    // ... existing keywords ...
};

// TokenType.cs line ~31 — add the enum value:
public enum TokenType
{
    // ... existing tokens ...
    Module,     // Phase 43 (D-03) — module <name> top-of-file declaration
    Live,
    Match,
    // ... existing ...
}

// Parser.cs ParseStatement (line ~89, BEFORE TokenType.Proc check):
if (Match(TokenType.Module))
    return ParseModuleDeclaration();
```

### Pattern 2: New AST Record (precedent: every Phase 35-39 AST add)

**What:** Add a new record type in `Ast/Statements/` (or `Ast/Expressions/`) following the existing `record` shape with `SourceLocation Location` and `Span? Span = null` positional fields.
**Source:** `flow-lang/Ast/Statements/ImportStatement.cs`
```csharp
namespace FlowLang.Ast.Statements;

public record ModuleDeclarationStatement(
    SourceLocation Location,
    string Name,
    Span? Span = null) : Statement(Location);
```

### Pattern 3: ParserStatement Parse Method (precedent: `ParseImportStatement` at Parser.cs:651)

```csharp
private ModuleDeclarationStatement ParseModuleDeclaration()
{
    var location = PreviousToken.Location;  // 'module' keyword
    var nameTok = Expect(TokenType.Identifier, "Expected module name after 'module' keyword");
    return new ModuleDeclarationStatement(
        location,
        nameTok.Text,
        Span: new Span(location, PreviousToken.Location));
}
```

**Position constraint (D-01) enforcement:** The check that `module <name>` MUST be the first non-comment statement happens at the top of `Parser.Parse()` — track whether we've seen any non-comment, non-module statement; on subsequent `module` tokens, emit a parse error at the keyword's location.

### Pattern 4: Per-ExecutionContext Registry Field (precedent: `PrngRegistry`, `LiveBlockRegistry`, `StyleRegistry`)

**Source:** `flow-lang/Runtime/ExecutionContext.cs:141, 156, 177`
```csharp
// ExecutionContext.cs — add alongside existing registries:

/// <summary>
/// Phase 43 (D-05) — per-context module registry keyed by module name. Populated
/// by ModuleLoader at `use` time when the loaded file declares `module <name>` as
/// its first non-comment statement. Files without a declaration are absent here
/// (back-compat per D-01). Read at qualified-access dispatch time by
/// ExpressionEvaluator.EvaluateMemberAccess (registry-first branch per D-02).
///
/// Mirrors PrngRegistry / LiveBlockRegistry / StyleRegistry shape — singleton per
/// ExecutionContext, lifecycle bound to FlowEngine instance.
/// </summary>
public ModuleRegistry ModuleRegistry { get; } = new();
```

### Pattern 5: ModuleLoader Registry Hook (NEW work driven by D-05)

```csharp
// ModuleLoader.cs — inside LoadModule, after Interpreter.Execute(program):

// Phase 43 (D-05): if the program declares `module <name>` as its first
// non-comment statement, register the module name → exported-procs.
if (program.Statements.Count > 0
    && program.Statements[0] is ModuleDeclarationStatement modDecl)
{
    // Diff the StackFrame.Functions before/after to capture only the procs
    // that THIS module's execution contributed (filters out transitively
    // imported procs via `use "@std"` etc.)
    // ... (snapshot logic) ...

    var exportedProcs = /* diffed proc-name → Value dict */;

    if (context.ModuleRegistry.Contains(modDecl.Name))
    {
        RenderingDiagnostics.WarnOnce(
            $"module-dup:{modDecl.Name}",
            $"[module] duplicate module name '{modDecl.Name}' — last load wins");
    }
    context.ModuleRegistry.Register(modDecl.Name, exportedProcs);
}
```

**Snapshot-and-diff strategy:** Mirror Phase 38's `LambdaCaptureAuditor` snapshot pattern. Before calling `interpreter.Execute(program)`, capture the current set of function-name keys in `context.GlobalFrame.Functions`. After execution, compute the set difference — those are the procs THIS module's execution added. (Note: existing transitive `use "@std"` adds procs to the same frame, but they were already there from a prior call OR will be picked up only ONCE because `_loadedModules` dedup at line 53 prevents re-execution.)

### Pattern 6: ExpressionEvaluator Registry-First Branch (D-02)

**Source location:** `flow-lang/Interpreter/ExpressionEvaluator.cs:627`
```csharp
private Value EvaluateMemberAccess(MemberAccessExpression member)
{
    // Phase 43 (D-02): registry-first branch — when LHS is a bare identifier
    // that matches a registered module name, return the named proc as a
    // Function Value. Falls through to instance-member dispatch otherwise.
    //
    // KEY DETAIL: peek at the AST shape BEFORE evaluating member.Object —
    // a bare `math` identifier is NOT a value (the variable isn't declared
    // anywhere), so the existing code path would error. By short-circuiting
    // here, we avoid the spurious "Variable 'math' not found" error.
    if (member.Object is VariableExpression varExpr
        && _context.ModuleRegistry.TryGetProc(varExpr.Name, member.MemberName, out var procValue))
    {
        return procValue!;  // Function Value; caller (function-call evaluator) invokes it
    }

    // Existing path unchanged: evaluate LHS, switch on Data type, fall through to reflection
    var obj = Evaluate(member.Object);
    // ... existing 75 lines ...
}
```

**Dispatch order rationale (D-02 spec):** Registry-first because (a) it's the cheaper check (dict lookup vs. potentially-failing variable evaluation), (b) it produces clearer errors (unknown member on a registered module says "no proc 'sin' in module 'math'"; falling through to instance-member would say "Variable 'math' not found"), and (c) it preserves all existing instance-member access patterns (`chord.root`, `song.sections`, `voice.Pan`) because those LHSes evaluate to non-null values that don't have entries in the registry.

### Anti-Patterns to Avoid

- **Don't add `BeatType.CanConvertTo(SecondType)` override** — Pitfall 3 in AUDIT.md. Pure-function `FlowType` methods have NO runtime context access; they can't read the active tempo. The conversion MUST be a builtin that reads `ExecutionContext.GetMusicalContext()`.
- **Don't speculatively add Beat overloads to `reverb` / `stretch` / `pitchShift` / `granular`** — D-09 tight scope. AUDIT explicitly names only `delay` and `renderBarAtBeat`; everything else lives in `feedback_ergonomics_priority` "don't bloat the API for hypothetical needs" territory.
- **Don't make `module` declaration optional via auto-derivation from filename** — D-01 explicitly requires the keyword. Filename-derived auto-naming would be magic-by-default; the explicit declaration is the ergonomically-honest path.
- **Don't error on duplicate module names** — D-06 is "advisory, last-load wins". Hard error breaks the charitable-default Flow contract (memory `feedback_charitable_interpretation`).
- **Don't gate the registry-first lookup on parent expression being a FunctionCall** — that's a refinement worth considering for error clarity, but the dispatcher currently doesn't have parent-context. Keep it simple: registry lookup → return Function Value → caller's existing function-call evaluator handles invocation.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| One-shot stderr advisory with dedup | Custom `HashSet<string>` + `Console.Error.WriteLine` | `RenderingDiagnostics.WarnOnce(sentinelKey, message)` at `flow-lang/Diagnostics/RenderingDiagnostics.cs:29` | 117 existing sites use this. Thread-safe + test-resettable via `ResetForTesting()`. |
| Reading the active tempo with default fallback | Custom `MusicalContext` walking | `context.GetMusicalContext().Tempo ?? 120.0` (the canonical idiom at `EffectsFunctions.cs:378`, `Interpreter.cs:200`, `Interpreter.cs:210`) | Established Phase 22 pattern. Tempo walks the full call-stack frame chain. |
| Function-overload registration + specificity scoring | Custom signature-matching | `OverloadResolver.Resolve` + `InternalFunctionRegistry.Register` with multiple `FunctionSignature` rows | Exact-match +1000 / IsCompatibleWith +500 / CanConvertTo +100 already does the right thing for `delay(Buffer, Beat, ...)` vs `delay(Buffer, Double, ...)`. Beat is `IsCompatibleWith Double` (BeatType.cs:25-28), so a bare-Double call still wins +1000 against its Double overload; a Beat-typed literal wins +1000 against the new Beat overload. No tiebreaker ambiguity. |
| Process-wide registry storage | Static `Dictionary<string, ...>` somewhere | A `ModuleRegistry` instance property on `ExecutionContext` (mirrors `PrngRegistry`, `LiveBlockRegistry`, `StyleRegistry`) | FlowEngine `SnapshotState`/`RestoreState` (Phase 35 TEST-02) snapshots all context state per test; a static registry would leak between hermetic tests. |

**Key insight:** Phase 43 has shockingly little new infrastructure — almost every primitive it needs already exists. The work is wiring + a tiny new registry field + one new AST record + one parser branch + one dispatcher branch. The hard part is sequencing the commits so each one is independently verifiable.

## Runtime State Inventory

> This phase is a code-and-syntax extension, NOT a rename/refactor. The Runtime State Inventory is mostly N/A. But the stdlib migration touches 13 `.flow` files (text edits), so a small inventory applies:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — Phase 43 introduces no new persistent data shapes. ModuleRegistry is in-memory only, lifecycle-bound to FlowEngine. | None. |
| Live service config | None — Flow is single-process. | None. |
| OS-registered state | None. | None. |
| Secrets/env vars | None. | None. |
| Build artifacts | The `flow-lang/bin/{Debug,Release}/net10.0/*.flow` published copies (visible at `/home/noah/Desktop/projects/flow-sharp/flow-lang/bin/Debug/net10.0/std.flow` etc.) will need refresh after the stdlib migration. | `dotnet build` will regenerate these via the `CopyToPublishDirectory=PreserveNewest` rule. No manual action needed. |
| Test scripts referencing module-collision-prone names | Grep result: zero matches for `module` keyword usage in `.flow` source (only docstrings). Test scripts in `tests/test_*.flow` reference module names in COMMENTS only. | None — but the migration pass should run the full `tests/test_*.flow` sweep to confirm. Per D-11, that's the gate for the migration commit. |

**Nothing found in category:** Verified by grep — no `.flow` script declares `module` as a proc, variable, or identifier today. The reserved-keyword add is fully back-compat for existing scripts.

## Common Pitfalls

### Pitfall 1: Parser ambiguity — `module foo` vs user-defined `module` variable

**What goes wrong:** A composer writes `Int module = 5;` at the top of a script. The new `module` keyword breaks this script.
**Why it happens:** Reserved keywords by definition consume the identifier.
**How to avoid:** Confirmed via grep — zero existing `.flow` source declares `module` as anything. Per D-11 pre-traction no-deprecation latitude, this is a clean break. The migration commit's verification step runs the full `tests/test_*.flow` sweep; any post-add regression flags here.
**Warning signs:** A test script fails with "Expected module name after 'module' keyword" instead of "Variable 'module' not found".

### Pitfall 2: MemberAccessExpression dispatch regression — `chord.root` breaks

**What goes wrong:** The registry-first branch eats an evaluation that should have gone to the existing `ChordData` instance-member dispatch.
**Why it happens:** If the registry lookup is too permissive (e.g., matches a member name that exists in some module AND is also a Chord field).
**How to avoid:** Gate the registry lookup on `member.Object is VariableExpression varExpr` AND `ModuleRegistry.Contains(varExpr.Name)`. Chord values are LITERAL expressions or function-call results — the LHS is NOT a bare variable expression for those, so the registry path doesn't even consider them. Test fixture `ModuleRegistryDispatchTests` must explicitly cover `chord.root`, `song.sections`, `voice.Pan`, `bar.Count` round-tripping post-Phase-43.
**Warning signs:** `tests/test_*.flow` scripts that exercise `chord.root` or `song.sections` start failing.

### Pitfall 3: `~>` tuple-unpack-flow interaction with member access

**What goes wrong:** `<<chord, voice>> ~> chord.root` — the tuple-unpack operator combined with member access. If `chord` (as a tuple slot name) shadows the registry?
**Why it happens:** Phase 26.1 `~>` desugars to a tuple destructure + call. The LHS of the resulting member access could in theory match a registered module.
**How to avoid:** `~>` desugars at parse time into a `TupleUnpackFlowExpression`. The member access on the destructured value happens AFTER the destructure — the LHS at evaluation time is the bound variable, which evaluates to a real value, not a bare identifier in `VariableExpression` form. Wait — `chord` IS a `VariableExpression`. The gate `ModuleRegistry.Contains(varExpr.Name)` saves us: if there's no module named `chord` (and there won't be — composer-named tuple slots can shadow module names ONLY if the composer literally declares `module chord` somewhere, which they almost certainly won't), the path falls through.

**The real escape hatch:** Composers who DO name a module `chord` and a tuple slot `chord` in the same scope get unqualified-access shadow-warning semantics. This is the same case as ordinary variable shadowing — Flow is lenient about it today, charitable-default per memory. Document the corner case; don't fight it.
**Warning signs:** A composer reports "my `chord.root` calls started routing to a `module chord` instead of the Chord variable." If this happens (unlikely), composer renames either the module or the variable.

### Pitfall 4: WarnOnce dedup across hot-reload (Phase 38 live block)

**What goes wrong:** A `live { }` block re-evaluates on file save (Phase 38). The collision/duplicate-module advisory should fire ONCE per block-entry, but `RenderingDiagnostics.WarnOnce`'s per-process dedup would suppress all but the first.
**Why it happens:** `live { }` opts OUT of the two-run cmp-clean determinism contract per D-v1.5-07. Hot-reload re-runs the body, which includes `use` statements at the top of the file. Subsequent reloads should still emit the duplicate-module advisory because the composer's state across saves may have changed.
**How to avoid:** `live { }` blocks call `RenderingDiagnostics.ResetForTesting()` is NOT the right answer (it's marked test-only and would suppress unrelated advisories). The right answer: keys the dedup sentinel by `(module-name, advisory-type)` not by `(module-name, file-line)`. For a `live` reload that re-runs the same module-name → same key → still deduped. Composer SEES the advisory on the FIRST reload after their save introduced the collision, subsequent reloads with the same collision stay quiet. This is the desired behavior.

Note: This matches Phase 38's existing `[live]` advisory dedup behavior (`flow-lang/Diagnostics/RenderingDiagnostics.cs` is shared infrastructure).
**Warning signs:** A composer saves a file 10 times in a row and gets 10 advisory lines in their watch panel.

### Pitfall 5: Phase 42 audit fixture polarity flip — temporal coupling

**What goes wrong:** The Phase 43 commit that adds `delay(Buffer, Beat, ...)` and `renderBarAtBeat(Bar, Beat, ...)` overloads makes `BeatType` exit the orphans list. But `AuditHarnessTests.OrphanList_ContainsBeatType` still asserts `Assert.Contains("BeatType", snap.CoercibleOrphans)`. The test fails the moment Phase 43 lands.
**Why it happens:** D-10 explicitly states the test polarity flips in lockstep with the production change.
**How to avoid:** The plan MUST ensure that the wave/task that adds the Beat-companion overloads ALSO modifies `flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs` to flip `Assert.Contains` → `Assert.DoesNotContain` (with updated XML-comment explaining the historical context). This is a SAME-COMMIT atomic change — don't split across waves.
**Warning signs:** Plan-checker flags a fixture that was passing before this phase started failing as a "verification gap". The fix is in the plan, not a real gap.

### Pitfall 6: `notation.flow` rename vs the `notation-io` collision

**What goes wrong:** Two stdlib files both want the name `notation`. `notation.flow` is the older (Phase 14-ish) bar-level note helpers; `notation-io.flow` is the Phase 39 MusicXML/LilyPond/ABC/MML export/import surface.
**Why it happens:** D-07 names this as a real conflict and proposes rename `notation.flow → module notes` (file stays at the same path, declaration changes the registered name).
**How to avoid:** Take the rename. `notes` is more accurate — that file is mostly `whole`/`half`/`quarter`/`eighth` helper procs and Note duration constants. Composers don't `use "@notation"` for it today (it's transitively loaded via `@std`), so the rename has zero composer-facing impact. `notation-io.flow` legitimately deserves the `module notation` name (it IS the notation export/import surface).

**Alternative considered:** Merge `notation.flow` into `notation-io.flow`. **Rejected:** `notation.flow` is transitively loaded via `@std` (it's part of the always-on prelude); `notation-io.flow` is OPT-IN via `use "@notation-io"` (Phase 39 D-39-01 explicit gate). Merging would force the heavy notation-export surface into every script's prelude. Keep them separate; rename to `notes`.

**Per D-07 final decision:** `module notes` in `flow-lang/notation.flow`; `module notation` in `flow-lang/notation-io.flow`.
**Warning signs:** A composer types `notation.writeMusicXML(...)` and gets "module 'notation' has no proc 'writeMusicXML'" — this works after the rename because `notation-io.flow` declares `module notation`.

### Pitfall 7: ModuleLoader.AlreadyLoaded short-circuit + first-time registration

**What goes wrong:** ModuleLoader caches loaded modules (line 53: `if (_loadedModules.Contains(resolvedPath)) return AlreadyLoaded`). On a second `use "@audio"` from a different consumer file, the load is skipped — but the `module audio` registration ALSO needs to be skipped (it already happened the first time). Easy to mis-wire so the second call double-registers.
**Why it happens:** Two-stage process: (1) load file + execute statements, (2) register module name. Both must happen on the FIRST load only.
**How to avoid:** Place the registry hook INSIDE the `if (_loadedModules.Contains(resolvedPath))` short-circuit — return `AlreadyLoaded` immediately. The first-load path is the ONLY place that runs `Interpreter.Execute(program)` AND should be the only place that calls `context.ModuleRegistry.Register(name, exports)`. Test fixture `ModuleRegistryDispatchTests.RegistersOnceAcrossMultipleUseStatements` pins this.
**Warning signs:** D-06 duplicate-module advisory fires even when only one file declares `module math` (because two `use "@math"` lines double-registered).

### Pitfall 8: Default-120-BPM advisory two-run cmp-clean preservation

**What goes wrong:** A script calls `(beatToSec 1.0)` outside any `tempo` block. The 120-BPM default fires + advisory emits. Re-running the same script produces the same WAV — but the advisory only fires on the FIRST run (per-process dedup).
**Why it happens:** Two-run cmp-clean requires byte-identical output. WAV output is the same (default is deterministic). Advisory output to stderr is NOT part of the WAV comparison, but it IS part of the cmp-clean discipline if tests redirect stderr.
**How to avoid:** The cmp-clean contract applies to AUDIO output (WAV bytes), not stderr. `RenderingDiagnostics.WarnOnce` writes to `Console.Error`. Phase 28/29/33 cmp-clean tests redirect `Console.Out` only — `Console.Error` is captured separately and tested with a different assertion shape (`WasWarnedForTesting(sentinelKey)`). The advisory wiring is byte-clean.

Verify: Phase 38's `WasWarnedForTesting` API (`flow-lang/Diagnostics/RenderingDiagnostics.cs:60`) is the established test mechanism. Phase 43's `BeatBuiltinTests.DefaultTempoAdvisoryFiresOncePerProcess` should consume this API directly.
**Warning signs:** A two-run cmp-clean test ass-erts byte-identity on stderr-redirected output and fails on the second run because the advisory was deduped.

## Code Examples

### Reading the active tempo with default fallback (canonical Phase 22 idiom)

```csharp
// Source: flow-lang/StandardLibrary/Audio/EffectsFunctions.cs:378
// (and Interpreter.cs:200, Interpreter.cs:210 — three established sites)
double bpm = context.GetMusicalContext().Tempo ?? 120.0;

// Phase 43 D-08: when default fires, emit one-shot advisory
if (context.GetMusicalContext().Tempo == null)
{
    RenderingDiagnostics.WarnOnce(
        "beatToSec-no-tempo",
        "[beatToSec] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)");
}
```

### Beat-companion overload registration (delay)

```csharp
// flow-lang/StandardLibrary/Audio/EffectsFunctions.cs — add to RegisterContextDependent
// alongside the existing delay(Buffer, NoteValue, Double, Double) overload:

var delayBeatSig = new FunctionSignature("delay",
    [BufferType.Instance, BeatType.Instance, DoubleType.Instance, DoubleType.Instance],
    ParameterNames: ["buf", "beats", "feedback", "mix"]);
registry.Register("delay", delayBeatSig, args =>
{
    var buffer = args[0].As<AudioBuffer>();
    double beats = args[1].As<double>();      // BeatType backs Double per BeatType.cs:25-28
    float feedback = (float)args[2].As<double>();
    float mix = (float)args[3].As<double>();

    // Read tempo with 120-BPM default + advisory (D-08)
    double? tempo = context.GetMusicalContext().Tempo;
    double bpm = tempo ?? 120.0;
    if (tempo == null)
    {
        RenderingDiagnostics.WarnOnce(
            "delay-beat-no-tempo",
            "[delay] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)");
    }

    double delayMs = beats * (60_000.0 / bpm);

    if (buffer.Frames == 0)
        return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

    var result = Delay.Apply(buffer, (float)delayMs, feedback, mix);
    return Value.Buffer(result);
});
```

### `beatToSec` builtin registration

```csharp
// flow-lang/StandardLibrary/Audio/MusicalConversions.cs — new file or extend existing

public static void RegisterContextDependent(
    InternalFunctionRegistry registry,
    ExecutionContext context)
{
    // beatToSec(Beat) → Second
    var beatToSecSig = new FunctionSignature("beatToSec",
        [BeatType.Instance],
        ParameterNames: ["beats"]);
    registry.Register("beatToSec", beatToSecSig, args =>
    {
        double beats = args[0].As<double>();
        double? tempo = context.GetMusicalContext().Tempo;
        double bpm = tempo ?? 120.0;
        if (tempo == null)
        {
            RenderingDiagnostics.WarnOnce(
                "beatToSec-no-tempo",
                "[beatToSec] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)");
        }
        double seconds = beats * (60.0 / bpm);
        return Value.Second(seconds);
    });

    // secToBeat(Second) → Beat — symmetric
    var secToBeatSig = new FunctionSignature("secToBeat",
        [SecondType.Instance],
        ParameterNames: ["seconds"]);
    registry.Register("secToBeat", secToBeatSig, args =>
    {
        double seconds = args[0].As<double>();
        double? tempo = context.GetMusicalContext().Tempo;
        double bpm = tempo ?? 120.0;
        if (tempo == null)
        {
            RenderingDiagnostics.WarnOnce(
                "secToBeat-no-tempo",
                "[secToBeat] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)");
        }
        double beats = seconds * (bpm / 60.0);
        return Value.Beat(beats);
    });
}
```

### `module` declaration in a stdlib `.flow` file (post-migration)

```flow
Note: Phase 36 Plan 36-05 — @patterns stdlib module
Note: 13 Tidal-style sequence combinators (D-36-01 hybrid)

module patterns

use "@std"

internal proc every (Int: n, Function: cb, Sequence: seq)
internal proc fast (Sequence: seq, Double: factor)
...
```

### `AuditHarnessTests` polarity flip (D-10)

```csharp
// flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs — Phase 43 commit edits:

[Fact]
public void OrphanList_DoesNotContainBeatType()  // RENAMED from OrphanList_ContainsBeatType
{
    var snap = _snapshot.Value;
    Assert.DoesNotContain("BeatType", snap.CoercibleOrphans);
    // Phase 43 closure context:
    // Before Phase 43, BeatType was the SOLE coercible orphan (AUDIT.md §1 anchor).
    // Phase 43 shipped `delay(Buffer, Beat, ...)` + `renderBarAtBeat(Bar, Beat, ...)`
    // + `beatToSec(Beat)` + `secToBeat(Second)` — Beat now has consumers, so the
    // orphan-detection rule (coercible AND zero signatures accept it) no longer
    // applies. If a future refactor drops the Beat-companion overloads, this
    // fact will fail with "BeatType found in CoercibleOrphans" — the
    // expected failure mode that protects against accidental regression.
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `use "@x"` injects procs into unqualified scope only | `use "@x"` injects unqualified AND registers `x → ExportedProcSet` for qualified lookup | Phase 43 | Composers can disambiguate collisions without changing their default ergonomic path. |
| `BeatType` is an orphan (no signatures accept Beat) | Beat is a first-class consumer in `delay`, `renderBarAtBeat`, `beatToSec`, `secToBeat` | Phase 43 | AUDIT §1 anchor finding closed; Phase 42 fixture flips polarity. |
| Tempo-to-seconds conversion is implicit (NoteValue rate or hand-rolled `(div 60.0 bpm)`) | Explicit `beatToSec` + `secToBeat` builtins available wherever a composer needs them | Phase 43 | Phase 28 BarRenderer-internal conversion stays untouched; this is a new SURFACE for composer-level code. |

**Deprecated/outdated:**
- (none — Phase 43 is purely additive)

## Assumptions Log

> This research was conducted by direct codebase inspection at HEAD plus reading CONTEXT.md / AUDIT.md / REQUIREMENTS.md. Almost every factual claim is verified. The assumptions below are decisions where multiple defensible paths exist — flagging for planner awareness.

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Per-`ExecutionContext` `ModuleRegistry` field is preferred over a process-global static. | §"Architecture Patterns" Pattern 4 | If planner chooses process-global, `FlowEngine.SnapshotState()`/`RestoreState()` from Phase 35 TEST-02 won't isolate tests cleanly. Recommendation is to mirror PrngRegistry / LiveBlockRegistry / StyleRegistry shape. Risk: low (planner is highly likely to follow the established pattern). |
| A2 | Snapshot-and-diff the `StackFrame.Functions` count before/after `Interpreter.Execute(program)` is the simplest way to compute `ExportedProcSet`. | §"ModuleLoader Registry Hook" | Alternative: walk `program.Statements` looking for `ProcDeclaration` nodes. The walk approach is more direct (doesn't depend on side-effect observation) and probably what the planner should choose. Either works — the diff approach handles dynamically-declared procs that the walk would miss, but Flow doesn't have those. Risk: low. |
| A3 | The `notation.flow` rename to `module notes` is the right resolution (vs. merging into `notation-io.flow`). | §"Pitfall 6" + D-07 | If planner prefers the merge, the always-on prelude grows by the heavy notation-IO surface — undesirable. Risk: low (the rename is captured in D-07 as the recommendation; this RESEARCH reinforces it). |
| A4 | The dispatcher-registry-first check is gated on `member.Object is VariableExpression varExpr` (NOT on arbitrary `Expression` LHS shapes). | §"Pattern 6 ExpressionEvaluator Registry-First Branch" | Tighter gating means composers can't write `(getModuleName).fn` to indirectly qualify. That's a feature, not a bug — it preserves all existing dispatch semantics and matches D-02's intent. Risk: low. |
| A5 | The `BeatType.IsCompatibleWith(DoubleType) = true` invariant means a bare-Double argument still wins +1000 against its Double overload, and a Beat-typed argument wins +1000 against the new Beat overload — no tiebreaker ambiguity. | §"Don't Hand-Roll" table row 3 | Verified via OverloadResolver.cs scoring (Exact +1000, IsCompatibleWith +500, CanConvertTo +100). The exact-type match always outranks the compatibility match. Risk: very low — pinned by `OverloadResolver.cs:200-247` exact-type-equal check. |
| A6 | Two-run cmp-clean for the default-120-BPM advisory works because stderr is captured separately and tested via `WasWarnedForTesting(key)` rather than byte-identity. | §"Pitfall 8" | If a test fixture in Phase 28/29/33 captures BOTH stdout AND stderr in its byte-comparison, the dedup would break the two-run contract. Verified: Phase 38 `RenderingDiagnostics.WasWarnedForTesting` is the established mechanism. Risk: low. |
| A7 | Adding `TokenType.Module` to the keyword switch is fully back-compat (zero `.flow` source uses `module` as an identifier today). | §"Pitfall 1" | Verified by grep — only docstrings reference "module" in `.flow` files. Risk: very low. |

## Open Questions

1. **Should the `ExportedProcSet` capture inherit transitively imported procs (e.g., `audio.flow` does `use "@std"` — does `audio` module's set include `print`)?**
   - What we know: D-05 says "register name → ExportedProcSet". The SCOPE of "exported" is unspecified.
   - What's unclear: If `audio.flow` `use "@std"`s, calling `audio.print(...)` could either work (transitive) or fail (this-file-only).
   - Recommendation: **this-file-only** — diff the proc-name set after running the module file MINUS the proc-name set that was already present from prior `use` calls. This matches Haskell / OCaml module semantics (you re-export explicitly, you don't auto-cascade). The Phase 43 deferred-ideas list includes `re-exports @bar` for v1.6+, so this-file-only is the consistent answer.

2. **What's the right error shape when a composer types `math.nope` and `math` is registered but `nope` isn't a proc in `math`?**
   - What we know: Current dispatcher's unknown-member path errors as `Type 'X' has no member 'Y'` (`ExpressionEvaluator.cs:710`).
   - What's unclear: Module-context should produce a different message — `module 'math' has no proc 'nope'`. Probably cheaper to compose than parse.
   - Recommendation: Add a dedicated `ReportUnknownModuleProc(moduleName, procName, location)` helper. Plan-discretion on exact wording. Use Levenshtein-suggest from Phase 35's `DiagnosticRenderer` for "did you mean?" — that's a nice ergonomic win but not strictly required for Phase 43.

3. **What happens when `module math` declared in a file that doesn't have any procs? (e.g., a constants-only file.)**
   - What we know: `math.flow` would register `math → {}` (empty ExportedProcSet).
   - What's unclear: Should this be an advisory? A composer typing `math.somevar` would fail "module 'math' has no proc 'somevar'", which is technically correct.
   - Recommendation: No special-casing. Empty module sets are valid (constants-only files exist). The error message at call site is informative enough.

## Environment Availability

This phase has zero external dependencies — all work is internal to the existing `flow-lang/` codebase. The only "tool" needed is `dotnet build` + `dotnet test`, both of which are pre-existing. SKIPPED.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (.NET 10 / xUnit 2.x) — existing |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` |
| Quick run command | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase43"` |
| Full suite command | `dotnet test flow-lang.Tests` + `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| REQ-MOD-01 | Parser accepts `module foo` as Statements[0]; rejects mid-file | unit | `dotnet test --filter "ModuleDeclarationParserTests"` | ❌ Wave 0 |
| REQ-MOD-02 | `math.sin` routes through registry; `chord.root` falls through | unit | `dotnet test --filter "ModuleRegistryDispatchTests"` | ❌ Wave 0 |
| REQ-MOD-03 | `use "@x"` registers `x → procs` AND injects unqualified | unit | `dotnet test --filter "ModuleRegistryDispatchTests.RegistersAndInjectsUnqualified"` | ❌ Wave 0 |
| REQ-MOD-04 | Unqualified collision = last-wins + one-shot advisory | unit | `dotnet test --filter "ModuleCollisionAdvisoryTests"` | ❌ Wave 0 |
| REQ-MOD-05 | Duplicate module-name = one-shot advisory | unit | `dotnet test --filter "ModuleCollisionAdvisoryTests.DuplicateModuleAdvisory"` | ❌ Wave 0 |
| REQ-MOD-06 | 13 stdlib `.flow` files migrated; full test-script sweep passes | integration | `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done` | ✅ (existing test-script sweep is the verification gate) |
| REQ-MOD-07 | `beatToSec(Beat)` reads tempo; default-120 advisory fires once | unit | `dotnet test --filter "BeatBuiltinTests.BeatToSec"` | ❌ Wave 0 |
| REQ-MOD-08 | `secToBeat(Second)` symmetric | unit | `dotnet test --filter "BeatBuiltinTests.SecToBeat"` | ❌ Wave 0 |
| REQ-MOD-09 | `delay(Buffer, Beat, ...)` round-trips correctly | unit | `dotnet test --filter "BeatCompanionOverloadTests.Delay"` | ❌ Wave 0 |
| REQ-MOD-10 | `renderBarAtBeat(Bar, Beat, ...)` round-trips correctly | unit | `dotnet test --filter "BeatCompanionOverloadTests.RenderBarAtBeat"` | ❌ Wave 0 |
| REQ-MOD-11 | Phase 42 audit fixture polarity flipped | unit | `dotnet test --filter "AuditHarnessTests.OrphanList_DoesNotContainBeatType"` | ✅ (existing file, MODIFIED in same commit as REQ-MOD-09/10) |
| REQ-MOD-12 | All new Phase 43 fixtures pass | unit | `dotnet test --filter "Phase43"` | ❌ Wave 0 (folder creation) |

### Sampling Rate

- **Per task commit:** `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase43" --no-build` (after a one-time `dotnet build`)
- **Per wave merge:** `dotnet test flow-lang.Tests` (full suite — confirms zero regression against Phase 28-42 tests; 34 pre-existing failures documented in Phase 42 `deferred-items.md` continue to be excluded by filename per Phase 42 precedent)
- **Phase gate:** Full xUnit suite green (modulo pre-existing 34 failures) + every `tests/test_*.flow` script PASS

### Wave 0 Gaps

- [ ] `flow-lang.Tests/Integration/Phase43/` directory creation
- [ ] `ModuleDeclarationParserTests.cs` — REQ-MOD-01 coverage
- [ ] `ModuleRegistryDispatchTests.cs` — REQ-MOD-02/03 coverage
- [ ] `ModuleCollisionAdvisoryTests.cs` — REQ-MOD-04/05 coverage
- [ ] `BeatBuiltinTests.cs` — REQ-MOD-07/08 coverage
- [ ] `BeatCompanionOverloadTests.cs` — REQ-MOD-09/10 coverage
- [ ] **Modified** `flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs` — REQ-MOD-11 polarity flip (SAME COMMIT as REQ-MOD-09/10)
- [ ] No framework installation needed (xUnit + project is already wired)
- [ ] No shared fixtures needed (each test is self-contained, mirrors Phase 36-42 fixture style)

### Pre-existing Failures Posture

Per Phase 42 `deferred-items.md` + `STATE.md`, there are 34 pre-existing Phase 28/29/35/38 failures from spawn commit `c4cd738`. Phase 43 follows the same "filename-based exclusion" posture as Phase 42 — these failures are NOT introduced by Phase 43, and the full-suite gate uses the same baseline. Plan-checker should record this baseline expectation in Phase 43's VERIFICATION.md.

## Security Domain

> Flow Language has no auth/session/external-input perimeter that ASVS categories meaningfully address. Phase 43 changes are interpreter-internal — no user-facing IO surface. The closest analog is:

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | N/A |
| V3 Session Management | no | N/A |
| V4 Access Control | no | N/A |
| V5 Input Validation | partial | Module name lex-time validation (`[a-zA-Z_][a-zA-Z0-9_]*` per D-01); parser rejects invalid forms with explicit error. Identifier-rule sanity already enforced by the existing lexer keyword scan. |
| V6 Cryptography | no | N/A |

### Known Threat Patterns for Flow Interpreter (Phase 43 scope)

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Parser DoS via malicious module name (e.g., 100k characters) | DoS | Existing `MaxParseDepth = 500` + lexer-level identifier length is implicitly bounded by file size; no special-casing needed. |
| Circular import + module-name collision interaction | DoS | `ModuleLoader._currentlyLoading` already detects circular imports; module-name collision is charitable (advisory, not abort). |
| Reflection-based member-access bypass | Tampering | Registry-first dispatch is a CHECK, not a security boundary; existing reflection fallback (line 700 of ExpressionEvaluator) is preserved. Phase 43 changes the dispatch ORDER, not the available targets. |

## Sources

### Primary (HIGH confidence)

- `flow-lang/Parsing/Parser.cs` — ParseImportStatement at line 651; ParseStatement structure at line 77-263; Match/Expect helpers throughout.
- `flow-lang/Lexing/SimpleLexer.cs` — keyword switch at line 874-919; `lastEmittedType` line 22.
- `flow-lang/Lexing/TokenType.cs` — full enum at lines 6-106; Tuning/Live/Match/As precedents at 30-40.
- `flow-lang/Runtime/ModuleLoader.cs` — entire file (200 lines); LoadModule at line 48; ResolveStdlibPath at 144; AdditionalSearchPaths at 36.
- `flow-lang/Runtime/ExecutionContext.cs` — registry-field precedents at lines 91 (SectionRegistry), 121 (TestRegistry), 129 (SymbolInternTable), 141 (PrngRegistry), 156 (LiveBlockRegistry), 177 (StyleRegistry); GetMusicalContext at line 440.
- `flow-lang/Runtime/MusicalContext.cs` — Tempo field at line 43; ActiveTuning pattern at line 128.
- `flow-lang/Interpreter/ExpressionEvaluator.cs` — EvaluateMemberAccess at line 627-708; existing dispatch switch on Voice/Track/Chord/Bar/Section/Song/reflection-fallback.
- `flow-lang/TypeSystem/SpecialTypes/BeatType.cs` — entire 36-line file; IsCompatibleWith with Double/Float at line 25-28.
- `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` — RegisterDelay at line 291-307; RegisterContextDependent NoteValue overload at 359-389 (canonical pattern for Beat overload).
- `flow-lang/StandardLibrary/Audio/InputFunctions.cs:78-84` — micBuffer Second + Double overload pair as the canonical two-overload registration pattern.
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:1477-1492` — renderBarAtBeat existing signature.
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs:239-346` — multiple RenderBarAtBeat overloads (showing the underlying static-method surface).
- `flow-lang/Diagnostics/RenderingDiagnostics.cs` — entire 64-line file; WarnOnce at line 29; WasWarnedForTesting at line 60; ResetForTesting at line 47.
- `flow-lang/Ast/Expressions/MemberAccessExpression.cs` — entire 13-line record.
- `flow-lang/Ast/Statements/ImportStatement.cs` — entire 12-line record (template for `ModuleDeclarationStatement`).
- `flow-lang/Runtime/Value.cs` — factory methods at 23-52; `Value.Beat(value)` at 42; `Value.Second(value)` at 37.
- `flow-lang/TypeSystem/FunctionSignature.cs:140-175` — CalculateSpecificity scoring; exact +1000 / compatible +500 / convertible +100.
- `flow-lang/TypeSystem/OverloadResolver.cs:200-247` — Resolve method; specificity tiebreaker behavior.
- `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` — entire 153-line file; Register at line 14; EnumerateSignatures at 133.
- `flow-lang/Core/FlowEngine.cs:160-225` — FlowEngine construction; RegisterContextDependent wiring sites; module loader instantiation.
- `flow-lang/Interpreter/Interpreter.cs:1071-1082` — ExecuteImport.
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` — §1 Beat orphan anchor finding at lines 15-19; §2 missing conversions at 41-46; §7a Phase 43 routing table at 199-200.
- `flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs:231-242` — OrphanList_ContainsBeatType (the D-10 polarity-flip target).
- `flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs:124-178` — Harness snapshot construction (informs the Phase 43 test fixture style).
- `flow-lang/*.flow` — all 13 stdlib files confirmed present via ls; sample headers inspected (improv.flow, generative.flow, notation.flow, notation-io.flow, std.flow).

### Secondary (MEDIUM confidence)

- N/A — every load-bearing claim is verified by direct codebase inspection at HEAD.

### Tertiary (LOW confidence)

- N/A.

## Metadata

**Confidence breakdown:**
- User constraints (CONTEXT.md): HIGH — copied verbatim from CONTEXT.md as the authoritative source.
- Standard stack: HIGH — every recommendation maps to an existing pattern in the codebase, with file path and line numbers verified.
- Architecture: HIGH — module-loader / registry / dispatcher integration verified against `ModuleLoader.cs`, `ExecutionContext.cs`, `ExpressionEvaluator.cs`.
- Beat backfill: HIGH — `EffectsFunctions.cs:359-389` is the canonical pattern; `BeatType` semantics verified at the source file.
- Pitfalls: HIGH — each pitfall is traced to a specific line of existing code or a Phase 28/29/35/36/38/42 precedent.
- Audit fixture polarity flip: HIGH — fixture and assertion lines pinned at `AuditHarnessTests.cs:231-242`.

**Research date:** 2026-05-24
**Valid until:** 2026-06-24 (30 days — Flow interpreter internals are stable; the only external risk is a Phase 40+ MIDI/Studio-Sync work landing concurrently that could touch `ExecutionContext.cs`. Probability low — Phase 40 is orthogonal per STATE.md.)
