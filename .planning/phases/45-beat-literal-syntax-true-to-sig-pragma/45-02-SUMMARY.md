---
phase: 45-beat-literal-syntax-true-to-sig-pragma
plan: 02
subsystem: ast + parser
tags: [phase-45, parser, ast, beat-literal, wave-2]
requires: [45-01]
provides:
  - BeatLiteralExpression AST record (SourceLocation + double RawValue + Span?)
  - Parser.ParsePrimary BeatLiteral arm emitting BeatLiteralExpression (D-09)
  - IsArgumentStart literal-token-set extended with TokenType.BeatLiteral
  - SimpleLexer.TryLexAngleAngle isExprStart extended with TokenType.BeatLiteral (Rule 1 fix)
  - 5 AST-shape Facts in BeatLiteralParserTests.cs
affects:
  - flow-lang/Ast/Expressions/BeatLiteralExpression.cs (NEW, 17 lines)
  - flow-lang/Parsing/Parser.cs (+12 lines, 2 edits)
  - flow-lang/Lexing/SimpleLexer.cs (+6 lines, 1 edit — Rule 1 tuple-close fix)
  - flow-lang.Tests/Integration/Phase45/BeatLiteralParserTests.cs (+123 lines, 5 Facts)
tech-stack:
  added: []
  patterns:
    - "Own-AST-record for music literals (SymbolLiteralExpression precedent) — single-property + Loc record, eval-time-resolved"
    - "Dedicated ParsePrimary arm casting Token.Value to double (D-09) — diverges from flat LiteralExpression(text) used by Cent/Time/Decibel/Hertz"
    - "Music-literal value-end token added to TryLexAngleAngle isExprStart set so a tuple literal can close immediately after the literal"
key-files:
  created:
    - flow-lang/Ast/Expressions/BeatLiteralExpression.cs
  modified:
    - flow-lang/Parsing/Parser.cs
    - flow-lang/Lexing/SimpleLexer.cs
    - flow-lang.Tests/Integration/Phase45/BeatLiteralParserTests.cs
decisions:
  - "D-01 honored: BeatLiteralExpression own AST record (SourceLocation, double RawValue, Span?) mirroring SymbolLiteralExpression byte-for-byte except Name→RawValue"
  - "D-09 honored: Parser.ParsePrimary emits BeatLiteralExpression via (double)PreviousToken.Value! cast, NOT flat LiteralExpression — raw double survives to eval time (REQ-BEAT-AST-02)"
  - "REQ-BEAT-AST-03 closed: IsArgumentStart literal-token-set includes TokenType.BeatLiteral so Nb appears at expression-start + as a function arg"
metrics:
  duration_minutes: 18
  tasks_completed: 1
  files_created: 1
  files_modified: 3
  tests_added: 5
  tests_pass_phase45: 21
  tests_pass_phase26: 125
  completed_date: "2026-05-30"
---

# Phase 45 Plan 02: BeatLiteralExpression AST Record + Parser Emit Summary

Wave 2 — landed the `BeatLiteralExpression` AST record and wired `Parser.ParsePrimary` to emit it for every `BeatLiteral` token (NOT a flat `LiteralExpression`), so the raw source double survives to eval time where Wave 4's pragma multiplier will read it. Extended the `IsArgumentStart` literal-token-set with `TokenType.BeatLiteral` for expression-start + function-arg positions. 5 new AST-shape Facts pin the record shape across variable initializer, function arg, flow-op chain, arithmetic operand, and tuple element; full Phase 45 suite at 21 GREEN with zero regression.

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| 1 | Create BeatLiteralExpression AST record + parser arm + literal-token-set extension + 5 AST-shape Facts | `121eb30` |

## Key Changes

### `flow-lang/Ast/Expressions/BeatLiteralExpression.cs` (NEW, 17 lines)

Final shape per D-01 — mirrors `SymbolLiteralExpression.cs` byte-for-byte except `string Name` → `double RawValue`:

```csharp
public record BeatLiteralExpression(
    SourceLocation Location,
    double RawValue,
    Span? Span = null
) : Expression(Location);
```

xmldoc (verbatim from 45-PATTERNS.md §"flow-lang/Ast/Expressions/BeatLiteralExpression.cs") references D-01, the multiplier formula `final = pragma_on ? raw × (4.0 / denom) : raw`, and cross-refs to `ExecutionContext.BeatTrueToSig` + `MusicalContext.TimeSignature` + the future `ExpressionEvaluator.EvaluateBeatLiteral`.

### `flow-lang/Parsing/Parser.cs` (+12 lines)

- **ParsePrimary arm** (inserted between the `HertzLiteral` arm at line 1382-1383 and the `ChordLiteral` arm at line 1396): casts `(double)PreviousToken.Value!` directly so the raw payload survives to eval (D-09 / REQ-BEAT-AST-02), diverging from the flat `LiteralExpression(text)` routing used by Cent/Time/Decibel/Hertz:
  ```csharp
  if (Match(TokenType.BeatLiteral))
  {
      double rawValue = (double)PreviousToken.Value!;
      return new BeatLiteralExpression(PreviousToken.Location, rawValue,
                                       Span: PreviousToken.EffectiveSpan);
  }
  ```
- **Literal-token-set extension**: added `or TokenType.BeatLiteral` between `or TokenType.HertzLiteral` and `or TokenType.ChordLiteral` in the `IsArgumentStart` method (the literal-token-set the plan called `IsExpressionStartingToken`, now at line ~2141) per REQ-BEAT-AST-03.
- `grep -c "TokenType.BeatLiteral" flow-lang/Parsing/Parser.cs` = **2** ✓ (one ParsePrimary arm + one literal-token-set entry).

### `flow-lang/Lexing/SimpleLexer.cs` (+6 lines — Rule 1 fix, see Deviations)

Added `or TokenType.BeatLiteral` to the `TryLexAngleAngle` `isExprStart` set (the music-literal value-end cluster alongside Hertz/Time/Decibel/Cent/Semitone). Without it, a Beat literal as the final tuple element (`<<C4, 0.5b>>`) left the closing `>>` to fall through to two single `>` tokens and the tuple never closed.

### `flow-lang.Tests/Integration/Phase45/BeatLiteralParserTests.cs` (+123 lines, 5 Facts)

Added a `Parse(string)` helper (fresh `SimpleLexer` + `Parser` with `PragmaSet.Empty`, asserting `!reporter.HasErrors`) plus the 5 AST-shape Facts from 45-RESEARCH §Signal 2:

- `AstShapeAssignedToVariable` — `Beat b = 0.5b` → `VariableDeclaration.Value` is `BeatLiteralExpression(0.5)`
- `AstShapeAsFunctionArg` — `(delay sig 0.5b 0.5 0.4)` → call `Arguments[1]` is `BeatLiteralExpression(0.5)`
- `AstShapeViaFlowOperator` — `0.5b -> (delay sig 0.5 0.4)` → call `Arguments[0]` is `BeatLiteralExpression(0.5)`
- `AstShapeAsArithmeticOperand` — `(add 0.5b 0.5b)` → both `Arguments` are `BeatLiteralExpression(0.5)`
- `AstShapeInTuple` — `Tuple<<Note, Beat>> entry = <<C4, 0.5b>>` → `TupleLiteralExpression.Elements[1]` is `BeatLiteralExpression(0.5)`

## Acceptance Criteria

| Criterion | Status |
|-----------|--------|
| `BeatLiteralExpression.cs` compiles | ✓ (test project build 0 errors) |
| 5 AST-shape Facts PASS GREEN | ✓ (21/21 BeatLiteralParserTests) |
| All 15 lexer Facts from 45-01 still GREEN | ✓ (16 lexer Facts in the same file, all GREEN) |
| `grep -c "TokenType.BeatLiteral" Parser.cs` ≥ 2 | ✓ (= 2) |
| `grep -n "public record BeatLiteralExpression"` = 1 | ✓ (= 1) |
| Pre-existing parser suites zero new failures | ✓ (Phase26 125/125, Phase35 Pattern + Phase43 QualifiedAccess in 33/33 cross-run, Lexer/Pragma 108/108) |

## Verification

```bash
dotnet build flow-lang.Tests/                                    # 0 Error(s)
dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase45.BeatLiteralParserTests" --no-build
# Passed! - Failed: 0, Passed: 21, Skipped: 0, Total: 21

dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase45|FullyQualifiedName~Phase26.Tuple|FullyQualifiedName~Phase35.Pattern|FullyQualifiedName~Phase43.QualifiedAccess" --no-build
# Passed! - Failed: 0, Passed: 33, Skipped: 0, Total: 33

dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase26" --no-build
# Passed! - Failed: 0, Passed: 125, Skipped: 0, Total: 125

dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Lexer|FullyQualifiedName~Pragma" --no-build
# Passed! - Failed: 0, Passed: 108, Skipped: 0, Total: 108
```

## Deviations from Plan

### Auto-Fixed Issues

**1. [Rule 1 — Bug] Tuple-literal close fails after a trailing Beat literal**
- **Found during:** Task 1 verification (AstShapeInTuple Fact RED with `Unexpected token GreaterThan '>'`)
- **Issue:** `SimpleLexer.TryLexAngleAngle` (the `<<`/`>>` lexer) gates `>>` recognition on an `isExprStart` set of preceding-token types. The set listed every other music-literal value-end (`HertzLiteral`/`TimeLiteral`/`DecibelLiteral`/`CentLiteral`/`SemitoneLiteral`) but NOT the brand-new `BeatLiteral`. So `<<C4, 0.5b>>` lexed the `>>` as two single `GreaterThan` tokens and the tuple never closed — affecting both the unit test AND real `.flow` source (`Tuple<<Note, Beat>> e = <<C4, 0.5b>>`).
- **Fix:** Added `or TokenType.BeatLiteral` to the `isExprStart` set with a 4-line comment citing D-09 and the mirrored Cent/Time/Decibel/Hertz/Semitone precedent.
- **Files modified:** `flow-lang/Lexing/SimpleLexer.cs` (commit `121eb30`).
- **Scope note:** Directly caused by this task's BeatLiteral introduction — squarely within scope.

**2. [Rule 3 — Blocking] Plan's `new PragmaSet()` does not compile**
- **Found during:** Task 1 test-helper compile
- **Issue:** The plan body's `Parse` helper snippet used `new PragmaSet()`, but `PragmaSet` has no parameterless constructor (`PragmaSet(IReadOnlySet<string> Enabled, IReadOnlyList<PragmaDeclarationSite>)`).
- **Fix:** Used `PragmaSet.Empty` (the canonical no-pragma carrier, already used by the file's existing `Tokenize` helper). The AST-shape Facts exercise the pragma-OFF path; Wave 4 adds pragma-ON eval tests.
- **Files modified:** `flow-lang.Tests/Integration/Phase45/BeatLiteralParserTests.cs` (commit `121eb30`).

**3. [Rule 3 — Blocking] Plan's example identifier `buf` is a reserved type keyword**
- **Found during:** Task 1 AstShapeAsFunctionArg Fact RED (`Unexpected token Buf 'buf'`)
- **Issue:** The plan's Test 2/3 source `(delay buf 0.5b ...)` uses `buf`, which lexes as the reserved `Buf` type keyword (SimpleLexer.cs:952), not an identifier — so it cannot appear as a function argument.
- **Fix:** Substituted identifier `sig` in both the function-arg and flow-op Facts. The AST-shape assertion (Arguments[1] / Arguments[0] is `BeatLiteralExpression`) is unaffected by the identifier choice.
- **Files modified:** `flow-lang.Tests/Integration/Phase45/BeatLiteralParserTests.cs` (commit `121eb30`).
- **Also:** AstShapeInTuple uses the canonical `Tuple<<Note, Beat>> entry = <<...>>` annotation form (per `tests/test_tuple_literal.flow`) rather than the plan's bare `<<C4, 0.5b>>`, which at statement-start parses as a tuple-destructure target — not a tuple-literal expression.

### Out-of-Scope Discoveries

None. Pattern-matching parser (Phase 35) `LiteralPattern`/`ConstructorPattern` arms were deliberately NOT extended for BeatLiteral — Beat literals in pattern position are out of scope for this plan (not in the 5 must-have test cases, not a must-have truth).

## Stub Tracking

None. The AST record + parser arm are fully wired. `BeatLiteralExpression` will throw `NotSupportedException` at eval time until Wave 4 adds the `EvaluateBeatLiteral` switch arm — this is the deliberate parse-only Wave 2 boundary documented in the plan's `<objective>` and threat register T-45-05 (disposition: accept).

## Downstream

Wave 4 (Plan 45-04) will add the `ExpressionEvaluator` switch arm:
```csharp
BeatLiteralExpression beatLit => EvaluateBeatLiteral(beatLit),
```
where `EvaluateBeatLiteral` reads `_context.BeatTrueToSig` + active `MusicalContext.TimeSignature`, computes `multiplier = pragma_on ? (4.0 / denom) : 1.0`, and returns `Value.Beat(beatLit.RawValue * multiplier)`. The `double RawValue` landed here is the exact payload that formula consumes (REQ-BEAT-AST-02 — no text re-parse at eval time).

## Self-Check: PASSED

- File existence:
  - `flow-lang/Ast/Expressions/BeatLiteralExpression.cs` — FOUND (created)
  - `flow-lang/Parsing/Parser.cs` — FOUND (modified)
  - `flow-lang/Lexing/SimpleLexer.cs` — FOUND (modified)
  - `flow-lang.Tests/Integration/Phase45/BeatLiteralParserTests.cs` — FOUND (modified)
- Commit existence:
  - `121eb30` (Task 1) — FOUND in `git log`
- Plan deliverables:
  - BeatLiteralExpression record `(SourceLocation, double RawValue, Span?)` → present (1 record)
  - Parser ParsePrimary arm → present (`grep -c TokenType.BeatLiteral` = 2 incl. literal-token-set)
  - IsArgumentStart literal-token-set includes BeatLiteral → present
  - 5 AST-shape Facts GREEN → present (21/21 BeatLiteralParserTests)
  - dotnet build → 0 errors
  - No regression to Phase26 / Phase35.Pattern / Phase43.QualifiedAccess / Lexer / Pragma suites
