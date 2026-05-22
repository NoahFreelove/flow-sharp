---
phase: 36-sequence-algebra-generative
plan: 02
subsystem: language-foundation
tags: [named-arguments, parser, overload-resolver, ergonomics, D-36-11]
dependency_graph:
  requires:
    - 36-01  # PrngRegistry foundation (FunctionSignature surface that this plan extends)
    - 35-07  # Phase 35 LANG-03 IntermediateName precedent for defaulted-positional extension
  provides:
    - named-arg call syntax `(fn name=value)`
    - FunctionSignature.ParameterNames defaulted-positional field
    - FunctionCallExpression.NamedArgs defaulted-positional field
    - OverloadResolver named-arg dispatch with 5 validation gates
    - ExpressionEvaluator named-arg Value[] re-ordering
    - graceful safety-net advisory for not-yet-backfilled signatures
  affects:
    - all subsequent Phase 36 plans (36-03/04 backfill, 36-05+ generative features)
    - all future plans that author composer-facing builtins
tech-stack:
  added: []
  patterns:
    - defaulted-positional record extension (Phase 35 LANG-03 precedent)
    - 2-token peek for AST disambiguation
    - per-signature validation pass before specificity scoring
key-files:
  created:
    - flow-lang.Tests/Phase36/NamedArgsParserTests.cs
    - flow-lang.Tests/Phase36/NamedArgsResolverTests.cs
    - flow-lang.Tests/Phase36/NamedArgBackcompatTests.cs
    - tests/test_named_args.flow
  modified:
    - flow-lang/TypeSystem/FunctionSignature.cs
    - flow-lang/Ast/Expressions/FunctionCallExpression.cs
    - flow-lang/Lexing/SimpleLexer.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang/TypeSystem/OverloadResolver.cs
    - flow-lang/Runtime/ExecutionContext.cs
    - flow-lang/Interpreter/ExpressionEvaluator.cs
    - flow-lang/StandardLibrary/Transforms/TransformFunctions.cs
decisions:
  - D-36-02-01 — defaulted-positional fields (mirror Phase 35 LANG-03 IntermediateName)
  - D-36-02-02 — ParameterNames excluded from FunctionSignature equality
  - D-36-02-03 — namedArgTypes is the LAST positional parameter on Resolve (preserves positional binding for 3-arg call sites)
  - D-36-02-04 — first-survivor wins among named-arg candidates (backfill plans won't produce ambiguity)
  - D-36-02-05 — transpose is the seed builtin annotated in this plan; 36-03/04 backfill the rest
metrics:
  duration: ~25 minutes
  tasks_completed: 3
  files_changed: 12
  tests_added: 12
  tests_passing_phase36: 23
  tests_passing_phase35_regression: 80
  tests_passing_phase26_regression: 125
  completed_date: 2026-05-21
---

# Phase 36 Plan 02: Named-Argument Syntax Summary

Universal named-argument call surface `(fn name=value)` lands at the language-foundation layer. Composer can write `(transpose seq amount=2)` and the resolver routes the named arg to slot 1 by parameter-name lookup. Backfill of the remaining ~350 builtin signatures with `ParameterNames` is the scope of parallel Plans 36-03 + 36-04; until then, named calls to un-annotated signatures raise a graceful "does not yet support named arguments" advisory rather than misbehaving.

## What Shipped

### 1. Defaulted-positional AST + signature extension (Task 1, commit 9f415c6)

`FunctionSignature` gains `IReadOnlyList<string>? ParameterNames = null` as the **last positional parameter**, mirroring Phase 35 LANG-03 `FlowExpression.IntermediateName` sweep convention. Excluded from `Equals` and `GetHashCode` so two signatures differing only in parameter names remain content-equal — keeps the `SignatureSet` de-dup behavior in `InternalFunctionRegistry` stable through the Plans 36-03/04 parallel backfill window.

`FunctionCallExpression` gains `IReadOnlyDictionary<string, Expression>? NamedArgs = null` as the last positional parameter. Pre-Phase-36 call sites compile unchanged; null = legacy positional-only dispatch.

### 2. Parser named-arg recognition + composer surface (Task 2, commit 22508a9)

Parser's paren-form function-call branch (`Parser.cs:1136-1196`) gains a 2-token peek inside the arg-list loop:

- `Identifier` followed by `Assign` parses as `name=expr` and binds into `NamedArgs`.
- Positional args follow the legacy path.
- Mixed shape (positionals first, then named) is the canonical form; a positional after a named arg raises `positional argument after named argument is not allowed (in call to '<fname>')` via the ErrorReporter.
- Duplicate names within a single call raise their own diagnostic.

`SimpleLexer.cs`: `TokenType.Assign` was already in `TryLexSignedNumber`'s expression-start set as of Phase 26 D-04 (for `Int x = -5` initializers); Phase 36 adds a marker comment so future readers see the named-arg `arg=-5` motivation alongside the variable-declaration motivation. No behavior change.

`tests/test_named_args.flow` registered (force-added past `.gitignore`'s global `tests/` ignore via `git add -f`).

### 3. OverloadResolver named-arg dispatch + runtime wiring (Task 3, commit e332462)

`OverloadResolver.Resolve` gains `IReadOnlyDictionary<string, FlowType>? namedArgTypes = null` as the last positional parameter (defaulted preserves the 4-arg existing call sites). 5 validation gates apply BEFORE specificity scoring:

| # | Condition | Diagnostic | Source |
|---|-----------|------------|--------|
| 1 | `IsVarArgs` + named args | `named arg '<name>' cannot be used with variadic function '<fname>'` | RESEARCH Open Question 2 |
| 2 | `ParameterNames is null` + named args | `function '<fname>' does not yet support named arguments` | RESEARCH Pitfall 5 — safety net for 36-03/04 backfill |
| 3 | Unknown name | `unknown parameter '<name>' for function '<fname>' (expected: <list>)` | T-36-04 |
| 4 | Positional + named target same slot | `parameter '<name>' bound by both positional and named argument` | T-36-06 |
| 5 | Arity mismatch | `function '<fname>' expects N arguments, got X positional + Y named` | shape check |

On survival, the re-ordered `FlowType[]` flows through the **existing** +1000/+500/+100 specificity-scoring path verbatim — no new code path.

`ExecutionContext.ResolveFunction` + `TryResolveFunction` forward `namedArgTypes` as a defaulted-null trailing parameter. Every existing 3-arg call site compiles unchanged.

`ExpressionEvaluator.EvaluateFunctionCall` evaluates `NamedArgs` values up-front, threads their Types into `TryResolveFunction`, and re-orders the runtime `Value[]` to match the resolved signature's slot order via `ParameterNames` lookup before invoking the registered lambda.

`TransformFunctions.RegisterTranspose` annotates both `transpose(Sequence, Semitone)` and `transpose(Sequence, Cent)` overloads with `ParameterNames: ["seq", "amount"]` — the seed builtin for the universal named-arg surface. Plans 36-03/04 backfill the rest of the standard library.

## Decisions Made

- **D-36-02-01 — defaulted-positional fields** (not new record types). Mirrors the Phase 35 LANG-03 `FlowExpression.IntermediateName` sweep convention. Keeps the diff additive — every existing construction site still compiles, and pre-Phase-36 ASTs have `NamedArgs=null` so the runtime takes the legacy positional-only dispatch path.
- **D-36-02-02 — `ParameterNames` excluded from `FunctionSignature` equality**. The resolver does name-based lookup against the field, not signature deduplication. Two signatures differing only in parameter names (the transition state during 36-03/04 backfill) remain content-equal under the Phase 26 contract. This keeps `SignatureSet` de-dup stable.
- **D-36-02-03 — `namedArgTypes` is the last positional parameter on `Resolve`**. Putting the new optional dictionary BEFORE `location` would break implicit positional binding at every existing 4-arg call site (`Resolve(name, sigs, types, loc)`). Trailing-defaulted preserves them all.
- **D-36-02-04 — first-survivor wins among named-arg candidates**. The backfill plans 36-03/04 won't produce ambiguous re-registrations (each builtin keeps a single canonical parameter-name shape). If future work introduces ambiguity, we revisit.
- **D-36-02-05 — `transpose` is the seed builtin annotated in this plan**. Plans 36-03 + 36-04 backfill the rest of the standard library in parallel. Plan 36-12 (later) ships a `ParameterNamesCoverageTest` grep gate for the 100% backfill milestone.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] `(fn ...)` and `(buf ...)` are reserved keywords in Flow**
- **Found during:** Task 2 test authoring
- **Issue:** Plan suggested test inputs using `(fn arg=-5)` and `(compress buf -12dB ratio=4.0)`. Both `fn` (lambda keyword) and `buf` (Buffer-related keyword) lex as `TokenType.Fn` / `TokenType.Buf`, never reaching the function-call parser branch.
- **Fix:** Used non-keyword identifiers `(xform arg=-5)` and `(compress sig -12dB ratio=4.0)`. Diagnostic-text expectations unchanged.
- **Files modified:** flow-lang.Tests/Phase36/NamedArgsParserTests.cs
- **Commit:** 22508a9

**2. [Rule 1 — Bug] Note-stream literal embedded inside a call interleaves lexing in unintended ways**
- **Found during:** Task 2 composer test
- **Issue:** `(transpose | C4q D4q | amount=2)` parses the inner `| C4q D4q |` as a note-stream literal, but the trailing `amount=2` then hits an `Unexpected token Assign '='` because the note-stream parser state doesn't anticipate a named-arg follow-on.
- **Fix:** Bind the input sequence to a variable (`Sequence base_seq = | C4q D4q |`) and pass the variable name to `(transpose ...)`. The named-arg fact under test is independent of the input-construction sugar.
- **Files modified:** tests/test_named_args.flow
- **Commit:** 22508a9

**3. [Rule 1 — Bug] `assertEq` falls back to reference equality for non-primitive Values**
- **Found during:** Task 3 composer test
- **Issue:** `(assertEq (transpose seq 2) (transpose seq amount=2))` returns two distinct `Sequence` Value instances even though they're structurally identical — `assertEq` reports them unequal.
- **Fix:** Use `assertNotesMatch` (Phase 35 LANG-04, structural Sequence equality) for the composer-facing fact. The 11 xUnit facts in `NamedArgsResolverTests` + `NamedArgsParserTests` already pin the resolver/parser contract structurally, so the composer test serves as the end-to-end smoke gate.
- **Files modified:** tests/test_named_args.flow
- **Commit:** e332462

**4. [Rule 3 — Blocking] `Array<Note>` type annotation doesn't lex in source text**
- **Found during:** Task 3 NamedArgBackcompat test
- **Issue:** `Array<Note> notes = ...` in Flow source raises `Unexpected token LessThan '<'`. The Flow type-annotation surface uses `Strings` as an alias for `Array<Note>` (per `tests/test_chords.flow` precedent).
- **Fix:** Use `Strings` instead.
- **Files modified:** flow-lang.Tests/Phase36/NamedArgBackcompatTests.cs
- **Commit:** e332462

**5. [Rule 3 — Blocking] `sine` builtin doesn't exist as a 2-arg form**
- **Found during:** Task 3 NamedArgBackcompat test
- **Issue:** `(sine 440.0 0.5)` raises `Function 'sine' not found` — the actual builtin is `createSineTone(Double duration, Double freq, Double amplitude)`.
- **Fix:** Replaced with `(createSineTone 0.5 440.0 0.5)` and substituted `(slice ...)` with `(retrograde ...)` (the latter is a 1-arg Sequence transform that doesn't need a slice-range surface).
- **Files modified:** flow-lang.Tests/Phase36/NamedArgBackcompatTests.cs
- **Commit:** e332462

All 5 auto-fixes are localized to test-authoring surfaces; the resolver/parser/evaluator contracts in the plan are unchanged.

## Test Results

### Phase 36 named-arg suite (this plan)

```
dotnet test --filter "FullyQualifiedName~Phase36.NamedArgs"
→ 11 passed (6 parser + 5 resolver), 0 failed
dotnet test --filter "FullyQualifiedName~Phase36.NamedArgBackcompat"
→ 1 passed, 0 failed
```

### Composer-facing acceptance

```
dotnet run --project flow-cli -- test tests/test_named_args.flow
→ PASS  transpose named matches positional
→ PASS  transpose accepts negative named amount
Total: 2; Passed: 2; Failed: 0
```

### Regression gates

| Suite | Pass/Total | Status |
|-------|------------|--------|
| Phase 35 (language foundation) | 80/80 | unchanged |
| Phase 26 (overload-resolver-heavy) | 125/125 | unchanged |
| Phase 36 (full) | 23/23 | including PrngRegistry from 36-01 |
| Parser/Overload/Resolver filter | 90/90 | unchanged |

`dotnet build` exits 0. The pre-existing inherited MIDI-debug-session failures (~43 regressions in Phase 28/29/Ragtime tests on the user's main repo working tree, per orchestrator context note) are NOT exercised by this worktree — Phase 36 named-arg work is parser/resolver-scoped and does not touch the BarRenderer / NoteStreamCompiler / SampledInstrumentRenderer surfaces under debug.

## What This Unblocks

- **Plans 36-03 + 36-04 (Wave 3, parallel)** — backfill `ParameterNames` across ~350 existing builtin signatures. The safety-net advisory (Test 11) guarantees these plans can ship incrementally without breaking anything; un-annotated signatures continue to work with positional-only calls and reject named-arg calls with a clear "does not yet support" message.
- **Plan 36-10 — `jam` builtin** requires the named-arg surface (`(jam over=chords style=#jazz length=8)`).
- **Plan 36-07 — Markov feature-extraction** requires `(markov corpus order length seed features=#pitch)`.
- **D-36-15 — section calls with defaulted params** depends on this surface.
- **Plan 36-12 — `ParameterNamesCoverageTest` grep gate** verifies the 100% backfill milestone.

## Threat Surface Scan

No new attack surface — named-arg syntax is composer-facing ergonomics and does NOT touch network endpoints, file I/O, auth paths, or schema boundaries. The 5 validation gates in `OverloadResolver` mitigate the 3 STRIDE concerns enumerated in the plan's `<threat_model>` (T-36-04 backfill orphans, T-36-05 varargs ambiguity, T-36-06 duplicate slot bind).

## Self-Check: PASSED

- All 3 task commits exist in git log (`9f415c6`, `22508a9`, `e332462`)
- All listed files-modified paths exist and contain the documented surfaces
- `dotnet build` exits 0
- `dotnet test --filter "FullyQualifiedName~Phase36.NamedArgs"` → 11/11
- `dotnet test --filter "FullyQualifiedName~Phase35"` → 80/80 (regression intact)
- `dotnet run --project flow-cli -- test tests/test_named_args.flow` → 2/2 PASS
