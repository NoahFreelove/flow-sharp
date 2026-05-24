---
phase: 43-module-names-qualified-imports
plan: 01
subsystem: lexer-parser-ast
tags:
  - phase-43
  - lexer
  - parser
  - ast
  - module-system
requirements:
  - REQ-MOD-01
dependency_graph:
  requires: []
  provides:
    - TokenType.Module reserved keyword
    - ModuleDeclarationStatement AST record
    - ParseModuleDeclaration parser branch
    - First-non-comment-statement position constraint
  affects:
    - Lexer keyword switch (adjacent to Tuning/Live)
    - Parser.ParseStatement dispatch cascade
    - Parser state (new _seenNonModuleNonCommentStatement flag)
tech_stack:
  added: []
  patterns:
    - Reserved-keyword add (Pattern 1)
    - New AST Record (Pattern 2)
    - ParserStatement Parse Method (Pattern 3)
    - Position-constraint enforcement via parser-state flag
key_files:
  created:
    - flow-lang/Ast/Statements/ModuleDeclarationStatement.cs
    - flow-lang.Tests/Integration/Phase43/ModuleDeclarationParserTests.cs
  modified:
    - flow-lang/Lexing/TokenType.cs
    - flow-lang/Lexing/SimpleLexer.cs
    - flow-lang/Parsing/Parser.cs
decisions:
  - Position constraint wired in Parse() driver — flag flips after each non-Module statement is appended, ParseStatement gates on it
  - Mid-file `module` errors are REPORTED via ErrorReporter (charitable advance) — not THROWN — so a single bad declaration doesn't halt the whole parse
  - `ParseModuleDeclaration` consumes `module` token in the gate branch (Advance), then Expects Identifier — failed Expect throws ParseException which the outer Parse loop catches and converts to a reported error
metrics:
  duration: 22min
  completed_date: 2026-05-24
---

# Phase 43 Plan 01: Module Names — Lexer + Parser + AST Surface Summary

Land the syntactic surface for Phase 43's `module <name>` top-of-file declaration: lexer reserves the `module` keyword, parser produces a `ModuleDeclarationStatement` AST node at `Statements[0]` when the declaration is the first non-comment statement, and the position constraint (D-01) is enforced via a parser-state flag flipped in the `Parse()` driver loop.

## What Shipped

| Surface | Where | Contains |
|---------|-------|----------|
| `TokenType.Module` enum value | `flow-lang/Lexing/TokenType.cs` (line 31) | New keyword token, adjacent to `Tuning,`/`Live,` |
| `"module" => TokenType.Module` keyword switch arm | `flow-lang/Lexing/SimpleLexer.cs` (line 897) | Standard identifier-to-keyword resolution |
| `ModuleDeclarationStatement` AST record | `flow-lang/Ast/Statements/ModuleDeclarationStatement.cs` (NEW) | `SourceLocation Location, string Name, Span? Span = null` |
| `ParseModuleDeclaration()` private method | `flow-lang/Parsing/Parser.cs` (after `ParseImportStatement`) | Captures keyword location, `Expect`s Identifier, emits AST node |
| `ParseStatement` dispatch + position-constraint gate | `flow-lang/Parsing/Parser.cs` (in the `ParseStatement` cascade) | `Check(TokenType.Module)` → gate on `_seenNonModuleNonCommentStatement`, then dispatch |
| `_seenNonModuleNonCommentStatement` parser-state flag | `flow-lang/Parsing/Parser.cs` (private field, set in `Parse()` driver) | Tracks "first non-comment statement" position invariant |
| Wave 0 xUnit fixture | `flow-lang.Tests/Integration/Phase43/ModuleDeclarationParserTests.cs` (NEW) | 5 Facts covering REQ-MOD-01 |

## How the Position Constraint Was Wired (ParseStatement vs Parse Driver)

**Both layers cooperate** — the flag is flipped in the `Parse()` driver after each successfully appended non-Module non-Comment statement; the gate is checked in `ParseStatement` at the `module` keyword dispatch site:

```csharp
// Parse() driver — flag-flip site
var stmt = ParseStatement();
if (stmt != null)
{
    statements.Add(stmt);
    if (stmt is not ModuleDeclarationStatement)
        _seenNonModuleNonCommentStatement = true;
}

// ParseStatement — gate site (after Comment skip, before Proc check)
if (Check(TokenType.Module))
{
    if (_seenNonModuleNonCommentStatement)
    {
        var badLoc = CurrentToken.Location;
        _errorReporter.ReportError(
            "module declaration must be the first non-comment statement of the file",
            badLoc);
        Advance();                          // consume `module`
        if (Check(TokenType.Identifier))
            Advance();                      // consume name token (skip past the bad declaration)
        return null;
    }
    Advance(); // consume `module`
    return ParseModuleDeclaration();
}
```

This split honors the plan's Pattern 3 guidance: comments never reach the flag-flip site (`ParseStatement` returns `null` on `TokenType.Comment` before any flag-flip logic), so `// header note\nmodule audio` correctly accepts the declaration AFTER the comment. The constraint message is REPORTED (not thrown) so a single bad declaration doesn't halt the rest of the parse — important for IDE / `flow watch` use cases per the project's soft-failure error model.

## Test Count Delta

| Test | After Task 1 | After Task 2 |
|------|--------------|--------------|
| `NoModuleDeclaration_ParsesAsBefore` (lex-only smoke, back-compat) | GREEN | GREEN |
| `ModuleDeclarationFirst_ProducesModuleDeclarationStatement` | RED | GREEN |
| `CommentsBeforeModuleDeclaration_AcceptDeclaration` | RED | GREEN |
| `ModuleNameNumericLiteral_ParseErrors` | RED | GREEN |
| `ModuleDeclarationAfterProc_ParseErrors` | RED | GREEN |

After Task 1: 1/5 GREEN (lex-only smoke). After Task 2: **5/5 GREEN**.

## Back-Compat Verification

- `dotnet build flow-lang/flow-lang.csproj` — 0 errors, 8 warnings (all pre-existing).
- `dotnet test flow-lang.Tests` — 1751 passed / 35 failed / 1 skipped / 1787 total. All 35 failures are in pre-existing Phase 28/29/35/38 baselines (`PerSynthArticulationTests` FFT, `RagtimeFixtureTests` RMS, `ArticulationOnSampleTests` Piano, `FlowTestCliTests` + `MatchExhaustivenessDefaultTests`, `OscLoopbackTests`). **Zero new failures introduced by Phase 43.**
- 127 `.flow` test scripts under `tests/test_*.flow` — 4 expected non-zero-exit failures (`test_dict_type_errors.flow`, `test_error_masking.flow`, `test_iteration_guard.flow`, `test_musical_context_errors.flow` — these scripts test error reporting and rely on non-zero exit; non-failures of the contract). The remaining 123 scripts pass without regression. No `module` keyword collisions in any existing `.flow` source (RESEARCH Pitfall 1 grep verification confirmed empty match set — every prior hit was in `// Note:` line comments, not code).

## Deviations from Plan

**None.** Plan 43-01 executed exactly as written. Both Pattern 1 (Reserved-Keyword Add), Pattern 2 (New AST Record), and Pattern 3 (ParserStatement Parse Method) followed verbatim from the RESEARCH / PATTERNS guidance. The position-constraint enforcement uses the recommended Pattern 3 line 348 approach (parser-state flag flipped in driver, gated in ParseStatement) — the plan's `<action>` block called out that the cleanest insertion site is in `Parse()`, and that's where the flip lives.

## Phase 43 Surface — What's Next

This plan lands the SYNTACTIC surface only. Wave 2+ subsequent plans will:

- **43-02 (Wave 2 — registry):** Add a `ModuleRegistry` (process-global or per-FlowEngine — TBD) keyed by module name → `ExportedProcSet`. `ModuleLoader` parses the `ModuleDeclarationStatement` at `use` time and registers the name.
- **43-03 (Wave 2 — dispatch):** Extend `ExpressionEvaluator.MemberAccessExpression` dispatch with a registry-lookup-first branch per D-02. Falls through to existing instance-member resolution on miss (back-compat preserved).
- **43-04 (Wave 3 — stdlib migration):** Add `module <name>` declarations to the 13 `flow-lang/*.flow` stdlib files per D-07 (rename `notation.flow` → `notes` module to avoid collision with `notation-io.flow`).
- **43-05 (Wave 3 — Beat backfill):** Add `(beatToSec Beat) → Second` + `(secToBeat Second) → Beat` context-aware builtins (D-08) + `delay(Buffer, Beat)` + `renderBarAtBeat(Sequence, Beat)` Beat-companion overloads (D-09). Flip the Phase 42 `AuditHarnessTests` Beat-orphan-pin polarity per D-10.

## Commits

| # | Hash | Type | Summary |
|---|------|------|---------|
| 1 | `e156dcc` | test | Module token + AST record + Wave 0 parser test scaffold (4 files, +194 LOC) |
| 2 | `13c6b9e` | feat | ParseModuleDeclaration + first-non-comment position constraint (1 file, +62 LOC) |

## Self-Check: PASSED

- **Files created exist:**
  - `flow-lang/Ast/Statements/ModuleDeclarationStatement.cs` — FOUND
  - `flow-lang.Tests/Integration/Phase43/ModuleDeclarationParserTests.cs` — FOUND
- **Files modified contain expected text:**
  - `flow-lang/Lexing/TokenType.cs` contains `Module,` — FOUND
  - `flow-lang/Lexing/SimpleLexer.cs` contains `"module" => TokenType.Module` — FOUND
  - `flow-lang/Parsing/Parser.cs` contains `ParseModuleDeclaration` + `_seenNonModuleNonCommentStatement` + `new ModuleDeclarationStatement` — FOUND
- **Commits exist:**
  - `e156dcc` — FOUND
  - `13c6b9e` — FOUND
- **Tests:**
  - `dotnet test --filter "FullyQualifiedName~Phase43.ModuleDeclarationParserTests"` — 5/5 PASSED
  - Full `flow-lang.Tests` — 1751 passed, 35 pre-existing failures, **0 new failures**
