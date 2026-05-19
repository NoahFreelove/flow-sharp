---
phase: 35-language-foundation
plan: 05
subsystem: interpreter
tags: [pattern-matching, ast, parser, evaluator, lang-01]

requires:
  - phase: 35-language-foundation
    provides: "Plan 35-01 Span + SourceMap records; Plan 35-04 (test ...) framework for composer-facing tests"
provides:
  - "(match scrutinee | pat => body | ... | _ => default) form usable in any expression position"
  - "Pattern AST family (Pattern base + 5 subtypes + MatchArm value record) under flow-lang/Ast/Patterns/"
  - "MatchExpression AST node (Ast/Expressions/) wired through ExpressionEvaluator dispatch"
  - "PatternMatcher runtime — naive linear scan per D-v1.5-11; decision-tree compile deferred to v1.6"
  - "Note-stream `|` disambiguation: `| C4 D4 |` outside (match ...) still parses as NoteStream (Pitfall 2 preserved)"
  - "ConstructorPattern carries IsChordLiteral / IsRomanNumeral / IsArticulationSymbol flags ready for Plan 35-06"
  - "Composer-facing regression at tests/test_pattern_matching.flow runnable via `flow test`"
affects: [35-06 music-aware extractors + matchExhaustive policy, 35-07 -> as name, 36 SECT-01 destructuring, 39 articulation emit, 40 MIDI event dispatch]

tech-stack:
  added: []  # zero new external dependencies — pure additive language extension
  patterns:
    - "Pattern AST family — parallel to AstNode (does NOT inherit); lives in own Ast/Patterns/ folder"
    - "Match-arm Pipe disambiguation by parser-state — no new flag; `(match` open paren is the disambiguator"
    - "Naive linear scan for v1.5; back-end swap to decision-tree compile in v1.6 is API-compatible (D-v1.5-11)"

key-files:
  created:
    - flow-lang/Ast/Patterns/Pattern.cs
    - flow-lang/Ast/Patterns/LiteralPattern.cs
    - flow-lang/Ast/Patterns/WildcardPattern.cs
    - flow-lang/Ast/Patterns/BindingPattern.cs
    - flow-lang/Ast/Patterns/ConstructorPattern.cs
    - flow-lang/Ast/Patterns/GuardPattern.cs
    - flow-lang/Ast/Patterns/MatchArm.cs
    - flow-lang/Ast/Expressions/MatchExpression.cs
    - flow-lang/Interpreter/PatternMatcher.cs
    - flow-lang.Tests/Phase35/MatchLexerTests.cs
    - flow-lang.Tests/Phase35/MatchParserTests.cs
    - flow-lang.Tests/Phase35/PatternAstTests.cs
    - flow-lang.Tests/Phase35/MatchRuntimeTests.cs
    - tests/test_pattern_matching.flow
  modified:
    - flow-lang/Lexing/TokenType.cs
    - flow-lang/Lexing/SimpleLexer.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang/Parsing/TypeParser.cs
    - flow-lang/Interpreter/ExpressionEvaluator.cs

key-decisions:
  - "Naive linear scan (D-v1.5-11) over Jacobs/Peterse decision-tree compile — expected arm count is 3-15; v1.6 can swap the back-end with zero composer-visible change"
  - "ConstructorPattern uses three discriminator flags (IsChordLiteral / IsRomanNumeral / IsArticulationSymbol) rather than 3 separate subtype records — Plan 35-06 sets flags from parser-side token recognition; runtime dispatch reads them in PatternMatcher.MatchConstructor"
  - "Note-stream `|` vs. match-arm `|` disambiguator is the `(match` open paren — no per-parser-state flag needed because ParseNoteStream only fires from primary-expression-start (Parser.cs:1057 unchanged) and match-arm Pipe is consumed inside ParseMatch's loop body, never reaching the primary dispatcher"
  - "TypeParser.LooksLikeFunctionType cheap-disambiguates `(match …)` with arm `=>` arrows from genuine function-type annotations `(Int => Int)` via a lookahead: when LParen is followed by TokenType.Match, refuse to claim function-type shape"
  - "TryLexSignedNumber's expression-start set gains TokenType.Match + TokenType.When so `(match -5 ...)` lexes `-5` as a single signed IntLiteral token"
  - "Non-exhaustive match handling is silent Value.Void() in Plan 35-05 — Plan 35-06 layers the matchExhaustive pragma + WARN-vs-error policy at the marker comment in EvaluateMatch"

patterns-established:
  - "Defaulted-Span migration (Plan 35-01 idiom) extended to the new Patterns/ family: every Pattern subtype + MatchArm + MatchExpression carries `Span? Span = null` as the last positional parameter"
  - "MatchExpression placement: lives in Ast/Expressions/ (it IS an expression) — only its sub-pattern children live in Ast/Patterns/"
  - "Discriminator flag pattern on ConstructorPattern for music-aware extractor extensibility — Plan 35-06 sets flags, runtime dispatches on flags, zero churn to the AST record shape"
  - "Per-arm fresh-bindings-dict + push/pop frame around arm body — implements Pitfall 6 binding-scope contract; bindings die with arm body, never leak"

requirements-completed: [LANG-01]

duration: ~70min
completed: 2026-05-18
---

# Phase 35 Plan 35-05: Pattern Matching Foundation Summary

**Pattern matching core lands: `(match scrutinee | pat => body | ... | _ => default)` with literal / wildcard / binding / guard arms; naive linear scan per D-v1.5-11; music-aware extractors + exhaustiveness policy deferred to Plan 35-06.**

## Performance

- **Duration:** ~70 min (Wave 0 stubs → AST family + lexer keywords → parser → runtime + summary)
- **Started:** 2026-05-18 (executor session)
- **Completed:** 2026-05-18
- **Tasks:** 4
- **Files created:** 14
- **Files modified:** 5

## Accomplishments
- Wave 3 of Phase 35 closed: pattern matching as a first-class expression form is now usable in any expression position (`Int v = (match x | 1 => 10 | _ => 0)`, `(match seq | n => (transpose n 2) | _ => seq)`, etc.).
- Pattern AST family lives in its own `Ast/Patterns/` folder distinct from `Ast/Expressions/` and `Ast/Statements/`, per Phase 35 RESEARCH §Recommended Project Structure. The MatchExpression itself stays in `Ast/Expressions/` (it IS an expression — only sub-pattern children are Pattern-family nodes).
- Note-stream `| C4 D4 |` continues to parse correctly outside `(match ...)` — Pitfall 2 disambiguation holds via the structural parser-state rule (the `(match` open paren is the disambiguator).
- Match-arm bindings die with the arm-body frame (Pitfall 6) — `(match 42 | n => n)` followed by `(print n)` errors with `n undefined` in the enclosing scope.
- ConstructorPattern ships with three discriminator init-only flags (`IsChordLiteral` / `IsRomanNumeral` / `IsArticulationSymbol`) so Plan 35-06 can drop in music-aware extractors without touching the AST record shape.
- 16 new xUnit facts (3 MatchLexer + 4 MatchParser + 4 PatternAst + 5 MatchRuntime) all GREEN; composer-facing `tests/test_pattern_matching.flow` registers 6 tests via Plan 35-04's `(test ...)` framework and all 6 PASS via `flow test`.

## Task Commits

Each task was committed atomically:

1. **Task 1: Wave 0 failing test stubs** — `b9aa4c7` (test)
2. **Task 2: Pattern AST family + match/when lexer keywords** — `3e6fe49` (feat)
3. **Task 3: ParseMatch + ParsePattern with note-stream `|` disambiguation** — `1f0138b` (feat)
4. **Task 4: PatternMatcher + EvaluateMatch dispatch (naive linear scan)** — `1b94095` (feat)

## Files Created/Modified

### Created (14)
- **AST family (8):** `flow-lang/Ast/Patterns/Pattern.cs`, `LiteralPattern.cs`, `WildcardPattern.cs`, `BindingPattern.cs`, `ConstructorPattern.cs`, `GuardPattern.cs`, `MatchArm.cs`; `flow-lang/Ast/Expressions/MatchExpression.cs`
- **Runtime (1):** `flow-lang/Interpreter/PatternMatcher.cs` — naive linear scan, static dispatcher
- **Tests (5):** `flow-lang.Tests/Phase35/MatchLexerTests.cs` (3 facts), `MatchParserTests.cs` (4 facts), `PatternAstTests.cs` (4 facts), `MatchRuntimeTests.cs` (5 facts); `tests/test_pattern_matching.flow` (6 composer-facing tests)

### Modified (5)
- `flow-lang/Lexing/TokenType.cs` — added `Match` + `When` enum entries to the keyword block
- `flow-lang/Lexing/SimpleLexer.cs` — added `"match" => TokenType.Match`, `"when" => TokenType.When` to the keyword table; added Match + When to TryLexSignedNumber's expression-start set so `(match -5 ...)` lexes -5 as a single signed IntLiteral
- `flow-lang/Parsing/Parser.cs` — added `(match` detection in the parenthesized-expression branch + new `ParseMatch(openParenLocation)` + `ParsePattern()` methods (~150 lines); `using FlowLang.Ast.Patterns;` added
- `flow-lang/Parsing/TypeParser.cs` — LooksLikeFunctionType cheap-rejects `(match …)` via a 1-token lookahead so the match-arm `=>` arrow doesn't falsely trigger function-type parsing
- `flow-lang/Interpreter/ExpressionEvaluator.cs` — added `MatchExpression matchEx => EvaluateMatch(matchEx)` switch arm + `EvaluateMatch` method (~30 lines) with PushFrame/PopFrame around arm body; `using FlowLang.Ast.Patterns;` added

## Decisions Made

The 6 key decisions are listed in the frontmatter `key-decisions` block (naive linear scan over decision-tree compile; flag-vs-subtype for ConstructorPattern; parser-state-only disambiguation; TypeParser lookahead; signed-literal expression-start expansion; silent-Void non-exhaustive policy).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] TypeParser.LooksLikeFunctionType false-positive on `(match …)`**
- **Found during:** Task 3 (ParseMatch wiring)
- **Issue:** Plan as written assumed the parenthesized-form branch in ParsePrimary would always be the entry point for `(match …)`. But before ParsePrimary runs, `ParseStatement` first checks if the statement starts with a type (variable declaration). `IsTypeKeyword` for `LParen` calls `TypeParser.LooksLikeFunctionType`, which scans for a `=>` at depth 1. Match arms ALL carry `=>`, so the function-type detector falsely claimed the entire match expression was a function-type annotation, producing "Expected type name but got Match 'match'".
- **Fix:** Added a cheap 1-token lookahead at the top of `LooksLikeFunctionType`: when LParen is followed by `TokenType.Match`, refuse to claim function-type shape so ParseStatement falls through to expression-statement parsing. Mirrors the existing keyword sniffs in ParseStatement (Pan/Gain/ReverbTime/VoicePool).
- **Files modified:** `flow-lang/Parsing/TypeParser.cs`
- **Verification:** MatchParserTests.SimpleMatchParses + BindingPatternParses + GuardPatternParses all flip GREEN.
- **Committed in:** `1f0138b` (Task 3 commit)

**2. [Rule 3 - Blocking issue] Signed-literal lexer rejected `-5` after `match` keyword**
- **Found during:** Task 4 (MatchRuntimeTests.GuardPatternFiresOnlyWhenGuardTrue negative case)
- **Issue:** `(match -5 | n when (gt n 0) => "pos" | _ => "neg")` failed to parse — the lexer's `TryLexSignedNumber` only fires when `_lastEmittedType` is in an explicit expression-start whitelist, and `TokenType.Match` was not in the set. The lexer emitted Minus + IntLiteral(5) instead of the single signed-IntLiteral(-5) token the parser expected for the scrutinee position.
- **Fix:** Added `TokenType.Match` and `TokenType.When` to TryLexSignedNumber's expression-start set. The scrutinee position after `match` and the guard-RHS position after `when` are both legitimate places for a signed literal.
- **Files modified:** `flow-lang/Lexing/SimpleLexer.cs`
- **Verification:** MatchRuntimeTests.GuardPatternFiresOnlyWhenGuardTrue (both pos and neg cases) flips GREEN.
- **Committed in:** `1b94095` (Task 4 commit)

**3. [Rule 1 - Bug] xUnit tests referenced wrong stdlib function names**
- **Found during:** Task 4 (MatchRuntimeTests + MatchParserTests authoring)
- **Issue:** Initial test fixtures used `(greater n 0)` and the executor body `(mul n 2)` without `use "@std"`. The interpreter reported `Function 'greater' not found` and `Function 'mul' not found`. The actual std.flow exposes `gt` (not `greater`).
- **Fix:** Renamed all references from `(greater ...)` to `(gt ...)` in MatchRuntimeTests, MatchParserTests, and tests/test_pattern_matching.flow; added `use "@std"` to MatchRuntimeTests Eval inputs that need builtins.
- **Files modified:** `flow-lang.Tests/Phase35/MatchRuntimeTests.cs`, `flow-lang.Tests/Phase35/MatchParserTests.cs`, `tests/test_pattern_matching.flow`
- **Verification:** All 5 MatchRuntimeTests + all 4 MatchParserTests + 6 composer-facing tests GREEN.
- **Committed in:** `1b94095` (Task 4 commit; the test text changes traveled with the supporting code so they're squashed under the same commit; MatchParserTests text update first appeared in `1f0138b`)

## Verification Results

### xUnit
- **MatchLexerTests:** 3/3 GREEN (Match keyword, When keyword, Underscore unchanged)
- **MatchParserTests:** 4/4 GREEN (SimpleMatchParses, BindingPatternParses, GuardPatternParses, NoteStreamStillParsesOutsideMatch)
- **PatternAstTests:** 4/4 GREEN (AllPatternKindsHaveSpan, MatchArmIsValueRecord, MatchExpressionLivesInExpressionsFolder, ConstructorPatternFlagsDefaultFalse)
- **MatchRuntimeTests:** 5/5 GREEN (FirstMatchWins, WildcardMatchesAnything, BindingPatternBindsScrutinee, BindingDoesNotLeakToEnclosingScope, GuardPatternFiresOnlyWhenGuardTrue)
- **Plan 35-05 total:** 16/16 facts GREEN
- **Phase 35 pre-existing facts:** still GREEN (LexerSpan + AstSpan + Span migration regression + FlowDiagnostic + TestFramework — no regression from Plan 35-05)
- **Full xUnit suite:** 1311/1337 PASS, 26 pre-existing failures (24 Phase28.PerSynthArticulationTests baseline drift + 2 Phase28.RagtimeFixtureTests + 2 Phase35.FlowTestCliTests) — verified pre-existing on dev's HEAD 25e57a7 before any of this plan's changes.

### Composer-facing .flow
- `tests/test_pattern_matching.flow` via `flow test`: 6/6 PASS (literal int / literal string / wildcard / binding / guard-true / guard-false)
- Legacy `flow run` smoke path: registers all 6 tests, prints `ALL PATTERN-MATCHING TESTS REGISTERED` sentinel — exit 0
- All 86 other `tests/test_*.flow` scripts: same pass/fail set as on dev (4 pre-existing failures: `test_dict_type_errors`, `test_error_masking`, `test_iteration_guard`, `test_musical_context_errors` — all unrelated to Plan 35-05)

## Downstream Unblocked

- **Plan 35-06 (LANG-02 music-aware extractors + exhaustiveness policy):** Can drop in `ChordParser.Parse` dispatch via `ConstructorPattern.IsChordLiteral`, roman-numeral resolution via `IsRomanNumeral`, and articulation-symbol matching via `IsArticulationSymbol`. The non-exhaustive policy (D-v1.5-05) wires at the marker comment in `EvaluateMatch` — read `_context.ProgramPragmaSet.Has("matchExhaustive")` (the pragma was already registered in Plan 35-03's PragmaRegistry sweep) and either ReportError or `RenderingDiagnostics.WarnOnce`.
- **Plan 35-07 (LANG-03 `-> as name`):** Independent path — pattern matching infrastructure is orthogonal.
- **Phase 36 SECT-01 (destructuring):** Can reuse the Pattern AST family directly — destructuring assignment is BindingPattern + ConstructorPattern in a non-match context.
- **Phase 39 articulation emit / Phase 40 MIDI event dispatch:** Both rely on pattern matching against music-typed scrutinees; Plan 35-06's music-aware extractors are the bridge.

## Threat Flags

No new trust boundaries introduced — pattern matching is a purely internal language extension. T-35-14 (note-stream `|` disambiguation regression) + T-35-15 (binding scope leak) are mitigated and gated by xUnit facts: MatchParserTests.NoteStreamStillParsesOutsideMatch + MatchRuntimeTests.BindingDoesNotLeakToEnclosingScope.

## Self-Check: PASSED

Created files verified present:
- `flow-lang/Ast/Patterns/Pattern.cs`, `LiteralPattern.cs`, `WildcardPattern.cs`, `BindingPattern.cs`, `ConstructorPattern.cs`, `GuardPattern.cs`, `MatchArm.cs` — all FOUND
- `flow-lang/Ast/Expressions/MatchExpression.cs` — FOUND
- `flow-lang/Interpreter/PatternMatcher.cs` — FOUND
- `flow-lang.Tests/Phase35/MatchLexerTests.cs`, `MatchParserTests.cs`, `PatternAstTests.cs`, `MatchRuntimeTests.cs` — all FOUND
- `tests/test_pattern_matching.flow` — FOUND

Commits verified present in git log:
- `b9aa4c7` (Task 1 Wave 0), `3e6fe49` (Task 2 AST + lexer), `1f0138b` (Task 3 parser), `1b94095` (Task 4 runtime) — all FOUND
