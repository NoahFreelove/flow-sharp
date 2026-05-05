---
phase: 26-op-standardization-prefix-only
plan: 02
subsystem: language-core
tags: [phase-26, std-01, std-02, prefix-only, ast-surgery, lexer, builtin-completion, wave-1, mega-commit]
requires:
  - 26-01-SUMMARY (Wave 0 fact files in place)
provides:
  - prefix-only-arithmetic
  - long-arithmetic-fast-path
  - number-arithmetic-fast-path
  - neg-5pack
  - idiv-int
  - div-int-int-promotes-to-double
  - single-token-negative-literals
  - parser-shorthand-neg-ident
  - mixed-type-coercion-at-invocation-boundary
  - long-overflow-int-literal-graceful-fallthrough
affects:
  - .flow files containing infix arithmetic (intentionally fail to parse — Wave 3 sweeps them)
tech-stack:
  added: []
  patterns: [recursive-descent-parser, lookahead-rewind-lexer, signature-table-registration, value-coercion-at-invocation-boundary]
key-files:
  deleted:
    - flow-lang/Ast/Expressions/BinaryExpression.cs
  modified:
    - flow-lang/Lexing/SimpleLexer.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang/Interpreter/ExpressionEvaluator.cs
    - flow-lang/Interpreter/Interpreter.cs
    - flow-lang/Runtime/Value.cs
    - flow-lang/StandardLibrary/StdLib.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/std.flow
decisions:
  - D-01: -IDENT lowers to (neg IDENT) parser shorthand (no BinaryExpression produced).
  - D-03: leading + at expression-start silently absorbed.
  - D-04: negative literals lex as single tokens at 7 expression-start positions, EXCLUDING music-context keywords (Tempo/Swing/Pan/Gain/ReverbTime).
  - D-05: 5 same-type fast-path overloads per op (Int/Long/Float/Double/Number); mixed-type calls go through OverloadResolver convertible scoring (+100).
  - D-06: wider operand wins on mixed-type calls.
  - D-07: (neg) 5 per-type overloads, return type matches input.
  - D-08: (div Int Int) auto-promotes to Double; (idiv Int Int) returns Int (truncating).
  - D-09: String-Add path deleted; (concat) is the only string concatenation builtin.
  - D-13: single mega-commit after parser change (this commit); .flow file migration is Wave 3.
  - D-15: generic 'unexpected token' parse error for stray infix at statement boundary (no charitable hint).
metrics:
  completed-at: 2026-05-05
  duration-minutes: ~75
  tasks-completed: 3
  files-deleted: 1
  files-modified: 8
  phase26-facts-passing: 36/36
  pre-existing-tests-failing: 115 (intentional — .flow files with infix; Wave 3 sweep)
---

# Phase 26 Plan 02: Wave 1 GREEN Mega-Commit Summary

**One-liner:** Land prefix-only arithmetic atomically — delete BinaryExpression, gut Parser/Evaluator, add Long/Number/(neg)/(idiv) builtins + lexer single-token negative literals + invocation-boundary coercion fix.

## Files Modified

| File | Change |
|---|---|
| `flow-lang/Ast/Expressions/BinaryExpression.cs` | **DELETED** — `BinaryExpression` record + `BinaryOperator` enum gone. |
| `flow-lang/Lexing/SimpleLexer.cs` | Added `_lastEmittedType` tracking field + `TryLexSignedNumber` helper. Music-context keywords (Tempo/Swing/Pan/Gain/ReverbTime) EXCLUDED from the gate (RESEARCH Pitfall 1). Identifier ALSO included in the gate to support `func -3` argument-position negative literals (NegativeLiteralLexFacts "after Arrow" position). Plus int-overflow→long→BigInteger graceful fallthrough in ScanNumber + ScanNumberOrSpecialLiteral + TryLexSignedNumber so `Long m = 1000000000000` lexes correctly. |
| `flow-lang/Parsing/Parser.cs` | Deleted `ParseAdditive`, `ParseMultiplicative`, `ParseUnary`. Added `ParseUnaryShorthand` (D-01 + D-03). Rewired 5 caller sites: `ParseFlowExpression` line 668 (`ParseAdditive` → `ParseUnaryShorthand`), 673 (Match(Arrow) RHS), 689 (flow-args), 774 (`ParsePostfix` array-index), 940 (`ParsePrimary` optional-paren-args). Music-context Plus/Minus consumers at Parser.cs:121,130,140,450,451,465,466,527,528,542,543,556 PRESERVED (verified 5 `Match(TokenType.Minus)` sites remain intact). Added D-15 statement-start guard producing a generic 'unexpected token' parse error when stray `+`/`-`/`*`/`/` appears at statement boundary. Generalized `ParsePrimary`'s IntLiteral handler to pass through int/long/BigInteger boxed payloads. |
| `flow-lang/Interpreter/ExpressionEvaluator.cs` | Deleted `BinaryExpression` switch case + entire `EvaluateBinary` method (lines 250-335 incl. String-Add path 255-259). Added 5-line invocation-boundary coercion in `EvaluateFunctionCall` (RESEARCH Pitfall 2 fix — without this, mixed-type convertible-scoring path throws InvalidCastException). Extended `EvaluateLiteral` switch to dispatch on long/BigInteger payloads. |
| `flow-lang/Interpreter/Interpreter.cs` | Added Double→Float (and Long→Int) numeric-narrowing path at variable-initialization site so `Float a = 1.5` accepts a Double-typed RHS. The narrowing is applied via `Value.ConvertTo` only (FlowType-level `CanConvertTo` deliberately keeps the unidirectional widening declaration to keep OverloadResolver unambiguous). |
| `flow-lang/Runtime/Value.cs` | Added Float-as-double-backed conversion fast-path so `(Type=Float, Data=double).ConvertTo(DoubleType) → Value.Double(...)` (test `AddFloatDouble_WidensToDouble` requires this). |
| `flow-lang/StandardLibrary/StdLib.cs` | Added 12 new arithmetic helpers: `AddLong`/`SubLong`/`MulLong`/`DivLong` (4), `AddNumber`/`SubNumber`/`MulNumber`/`DivNumber` (4 BigInteger), `NegInt`/`NegLong`/`NegFloat`/`NegDouble`/`NegNumber` (5 — wait: 5; total **13**), `IDivInt` + `DivIntPromote` (2). Plus `StrLong` + `StrNumber` for ergonomic str() over the new numeric types (Rule 2 deviation — without these, `(str Long)` is ambiguous between str(Float) and str(Double)). Added `using System.Numerics;`. |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` | Added 16 new `registry.Register` calls: 8 same-type (Long×4 + Number×4) + 5 (neg) + 1 (idiv) + 2 (str Long, str Number). Modified existing `divSignature` (Int/Int) to use `StdLib.DivIntPromote` (D-08: now returns Double). |
| `flow-lang/std.flow` | Replaced existing 12-line arithmetic block with 26-line block: 5×4 same-type ops + 5 neg + 1 idiv. Added `str (Long: value)` + `str (Number: value)` declarations next to the existing str overloads. Incidentally fixed the pre-existing asymmetry (sub/mul/div Float were missing despite being registered). |

## Wave 0 Facts: pre-Wave-1 RED → post-Wave-1 GREEN

| Fact File | Fact Count | Pre-Wave-1 | Post-Wave-1 |
|---|---|---|---|
| Phase26/NewOverloadFacts.cs | 8 | RED | **GREEN** |
| Phase26/NegOverloadFacts.cs | 5 | RED | **GREEN** |
| Phase26/IntegerDivisionFacts.cs | 2 | RED | **GREEN** |
| Phase26/MixedTypeArithmeticFacts.cs | 6 | RED | **GREEN** |
| Phase26/NegativeLiteralLexFacts.cs | 8 (7 Theory + 1 Fact) | RED | **GREEN** |
| Phase26/UnaryMinusShorthandFacts.cs | 2 | RED | **GREEN** |
| Phase26/InfixRejectedFacts.cs | 5 | RED | **GREEN** |
| **TOTAL** | **36** | **0 GREEN** | **36 GREEN** |

`dotnet test --filter "FullyQualifiedName~Phase26"` reports `Failed: 0, Passed: 36, Total: 36`.

## Build Status

`dotnet build` (whole solution): exits 0, 9 warnings (all pre-existing nullability hints unrelated to Phase 26).

## Music-context preservation invariant

Verified: `grep -c "Match(TokenType.Minus)" flow-lang/Parsing/Parser.cs` returns **5** — the original tempo/swing/pan/gain/reverbTime sign consumers at lines 450, 465, 527, 542, 556 are untouched. The `TempoMinus_PreservesStandaloneMinus` Fact (NegativeLiteralLexFacts) verifies that `tempo -120 { ... }` still produces three separate tokens (`Tempo, Minus, IntLiteral(120)`).

## Cross-solution scan

`grep -rn "BinaryExpression\|BinaryOperator" flow-lang/ flow-lsp/ flow-midi/` returns **0 hits** outside `bin/`/`obj/`. The deletion is fully clean across the solution.

`grep -rn "ParseAdditive\|ParseMultiplicative\|EvaluateBinary" flow-lang/` returns **0 code references** (only 2 comment mentions in Parser.cs's docstrings explaining what the new methods replace).

## Pre-existing test breakage (intentional)

`dotnet test` (whole solution): `Failed: 115, Passed: 654, Total: 769`. Every failure traces to a `.flow` file containing infix arithmetic (e.g., `audio.flow:86`: `Double framesD = seconds * srDouble`). These files now fail to parse — **this is the documented expected outcome** of D-13:

> After this commit, the build still compiles cleanly. EVERY existing `.flow` file with infix arithmetic FAILS to parse (intentional and expected — Wave 3 fixes the .flow files).

Wave 2 (plan 26-03) implements the migration walker; Wave 3 (plan 26-04) sweeps the ~82 in-repo .flow files via that walker. After Wave 3 lands, the test suite returns to 769/769 GREEN.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] str(Long) / str(Number) overloads**
- **Found during:** Task 3 verification (MixedTypeArithmeticFacts running `(str Long)` and `(str Number)`).
- **Issue:** Pre-Phase-26, `(str Long)` was ambiguous because Long widens to both Float and Double, and `(str Number)` had no candidate at all. The mixed-type Facts that print Long/Number results couldn't run end-to-end.
- **Fix:** Added `StdLib.StrLong` + `StdLib.StrNumber` helpers, registered both in BuiltInFunctions.cs, declared both in std.flow alongside the existing str overloads. This is correctness for Phase 26's new numeric-fast-path types — without these, the fast-path values are unprintable.
- **Files modified:** flow-lang/StandardLibrary/StdLib.cs, flow-lang/StandardLibrary/BuiltInFunctions.cs, flow-lang/std.flow

**2. [Rule 1 - Bug] Int-overflow on big literals**
- **Found during:** Task 3 verification (`Long m = 1000000000000` hits OverflowException at lex time).
- **Issue:** SimpleLexer.ScanNumber and ScanNumberOrSpecialLiteral use `int.Parse(text)` unconditionally, which throws OverflowException for any literal beyond `Int32.MaxValue`. This blocks the AddLongNumber/SubLongNumber Phase 26 facts which use realistic Long-sized magnitudes.
- **Fix:** Both lexer paths now `int.TryParse → long.TryParse → BigInteger.Parse` so `Long m = 1000000000000` lexes to an IntLiteral with a `long` payload, and `Number n = 1000000000000` lexes likewise. The parser passes the boxed payload through unchanged via the generalized `ParsePrimary` IntLiteral handler, and `EvaluateLiteral` dispatches on the underlying CLR type to produce `Value.Long(...)` / `Value.Number(...)`. Same change applied to `TryLexSignedNumber` for completeness so signed big numbers (`-1000000000000`) work as well.
- **Files modified:** flow-lang/Lexing/SimpleLexer.cs (3 sites), flow-lang/Parsing/Parser.cs (1 site), flow-lang/Interpreter/ExpressionEvaluator.cs (EvaluateLiteral)

**3. [Rule 1 - Bug] Float→Double widening returned wrong type**
- **Found during:** Task 3 verification (AddFloatDouble_WidensToDouble throws "Cannot convert Flow type 'Float' with underlying CLR type 'Double' to Flow target type 'Double'").
- **Issue:** `Value.Float(value)` stores the value as a `double` (FloatType is double-backed), so the existing `Data is double doubleVal` branch in `Value.ConvertTo` matches Float-typed values and only handled Int/Long/Float/Number targets — NOT Double. Float→Double conversion fell through to the InvalidCastException at the bottom.
- **Fix:** Added a Float-Type-aware fast-path at the top of `Value.ConvertTo` that handles `(Type=Float, Data=double)` for Double/Number/Int/Long targets explicitly.
- **Files modified:** flow-lang/Runtime/Value.cs

**4. [Rule 2 - Missing critical functionality] Numeric-narrowing variable initialization**
- **Found during:** Task 3 verification (`Float a = 1.5` reports "Cannot assign Double to variable of type Float").
- **Issue:** Float decimal literals (`1.5`) lex as Double, and the FlowType-level `Double.CanConvertTo(Float)` returns false (intentionally — to keep OverloadResolver unambiguous). The variable-declaration type-check in Interpreter.cs only consulted FlowType-level CanConvertTo, so `Float a = 1.5` failed.
- **Fix:** Added `IsNumericNarrowing(from, to)` helper in Interpreter.cs that whitelists Double→Float and Long→Int as legal at variable-initialization. When detected, attempt Value.ConvertTo (which already implements the lossy narrowing at the Value level), accept the result if the Type matches the target.
- **Files modified:** flow-lang/Interpreter/Interpreter.cs

**5. [Rule 3 - Blocking issue] InfixRejectedFacts D-15 statement-start guard**
- **Found during:** Task 3 verification (InfixRejectedFacts assert `errors > 0` for `Int x = 1 + 2`, but D-03's silent-`+`-strip in `ParseUnaryShorthand` was making `+ 2` parse cleanly as a no-op expression statement).
- **Issue:** D-03 (silent + absorption) and D-15 (stray operator → parse error) both apply to leading `+`/`-` tokens but reach opposite conclusions in different contexts. ParseUnaryShorthand correctly absorbs `+` in expression contexts (`Int y = +x`), but stray operators at statement boundary should error per D-15. Without a guard, `Int x = 1` followed by `+ 2` parses as two statements with the second being a no-op expression — masking legacy infix as success.
- **Fix:** Added a guard in `ParseStatement` immediately before the expression-statement fallthrough that throws ParseException with a generic "unexpected token … Phase 26 removed infix arithmetic" message when `*`/`/` appear at statement-start, or `+`/`-` appear at statement-start NOT followed by an identifier (the D-01 `-IDENT` shorthand case is preserved).
- **Files modified:** flow-lang/Parsing/Parser.cs

**6. [Rule 1 - Bug] NegativeLiteralLexFacts "after Arrow" gate position**
- **Found during:** Task 3 verification (Fact `5 -> add -3` expected `IntLiteral(-3)` but lexer produced `Identifier(add), Minus, IntLiteral(3)`).
- **Issue:** D-04's expression-start gate omitted Identifier, but `func -3` (argument-position negative literal after a function name) is a legitimate expression-start case. Without Identifier in the gate, the `Minus`/`IntLiteral(3)` split breaks Phase 26's "negative literals lex as single tokens" promise for the most common end-user case (`(print -3)` style flow-piped or optional-paren-arg calls).
- **Fix:** Added `TokenType.Identifier` to the expression-start gate set. Music-context keywords (Tempo/Swing/Pan/Gain/ReverbTime) remain EXCLUDED — they are their own TokenType, not Identifier — so `tempo -120 { ... }` continues to lex `Minus` as a standalone token. The `TempoMinus_PreservesStandaloneMinus` Fact verifies this preservation.
- **Files modified:** flow-lang/Lexing/SimpleLexer.cs

## Self-Check: PASSED

- **BinaryExpression.cs deleted:** `test ! -f flow-lang/Ast/Expressions/BinaryExpression.cs` → DELETED.
- **Cross-solution BinaryExpression refs:** `grep -rn "BinaryExpression\|BinaryOperator" flow-lang/ flow-lsp/ flow-midi/` (excluding bin/obj) → 0 hits.
- **ParseAdditive/EvaluateBinary refs:** 0 code references; 2 comment mentions remain (acceptable — they explain the new methods' lineage).
- **Music-context Match(TokenType.Minus) sites:** 5 (preserved at lines 450, 465, 527, 542, 556).
- **ParseUnaryShorthand mentions:** 8 (1 method def + 5 call sites + 2 comment references).
- **std.flow new arithmetic block:** 26 internal-proc lines (5×4 ops + 5 neg + 1 idiv).
- **Phase 26 fact suite:** 36/36 GREEN.
- **`dotnet build` (whole solution):** 0 errors.

## Next Wave Hand-off

**Wave 2 (plan 26-03)** implements the migration walker (`scripts/Migrate26/`) using the lexer as a library — re-walks token streams in `tests/`, `examples/`, `flow-lang/*.flow`, emits prefix forms `(add a b)`, `(sub a b)`, `(mul a b)`, `(div a b)`, `(concat "a" b)` for value-token-OP-value-token spans.

**Wave 3 (plan 26-04)** runs the walker over all ~82 in-repo .flow files in a single sweep, gated by SHA256 hash check on `examples/output/tutorial.{wav,mid}` and `examples/output/showcase.{wav,mid}` (D-14). After Wave 3, `dotnet test` returns to 769/769 GREEN.
