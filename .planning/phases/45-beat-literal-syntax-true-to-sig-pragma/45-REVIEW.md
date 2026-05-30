---
phase: 45-beat-literal-syntax-true-to-sig-pragma
reviewed: 2026-05-29T00:00:00Z
depth: standard
files_reviewed: 14
files_reviewed_list:
  - flow-lang/Ast/Expressions/BeatLiteralExpression.cs
  - flow-lang/Ast/Statements/ProcDeclaration.cs
  - flow-lang/Core/FlowEngine.cs
  - flow-lang/Interpreter/ExpressionEvaluator.cs
  - flow-lang/Interpreter/Interpreter.cs
  - flow-lang/Lexing/PragmaRegistry.cs
  - flow-lang/Lexing/PragmaScanner.cs
  - flow-lang/Lexing/SimpleLexer.cs
  - flow-lang/Lexing/TokenType.cs
  - flow-lang/Parsing/Parser.cs
  - flow-lang/Runtime/ExecutionContext.cs
  - flow-lang/Runtime/ModuleLoader.cs
  - flow-lang/StandardLibrary/Audio/BeatConstructorFunctions.cs
  - flow-lang/StandardLibrary/BuiltInFunctions.cs
  - flow-lang/StandardLibrary/StdLib.cs
findings:
  critical: 0
  warning: 1
  info: 2
  total: 3
status: issues_found
---

# Phase 45: Code Review Report

**Reviewed:** 2026-05-29
**Depth:** standard
**Files Reviewed:** 14
**Status:** issues_found

## Summary

Phase 45 adds `Nb` beat literals, the `BeatLiteralExpression` AST node + parser arm,
the file-scope `beat-true-to-sig` pragma (PragmaRegistry entry + `ExecutionContext.BeatTrueToSig`
bit + `FlowEngine.ApplyBeatTrueToSigPragma` + `ModuleLoader` save-set-restore + per-proc/per-lambda
push/pop), the `(beat N)` constructor migration to `RegisterContextDependent`, and a dedicated
`str(Beat)` overload.

This is a high-quality implementation. The areas the prompt flagged as risk-prone all hold up
under scrutiny:

- **Pragma save-set-restore is leak-safe.** Both `ModuleLoader.LoadModule` (import boundary,
  lines 170/251) and `Interpreter.ExecuteUserFunctionWithCaptures` (proc boundary, lines 1149/1244)
  save-before-set and restore-in-`finally`, balanced against `StrictMode` exactly. A body/import
  throw rebalances the bit. Verified by `tests/test_beat_cross_file.flow`: a `(beat 1)` in a
  pragma-OFF helper called from a pragma-ON 6/8 context correctly returns `1` (no multiplier),
  while a local `1b` in the same caller returns `0.5`.
- **Divide-by-zero is doubly guarded.** `TimeSignature?.Denominator ?? 4` defaults to identity 4/4,
  and the timesig parser rejects denominator 0 (`timesig 4/0` errors and falls back to 4/4 → prints
  `1`). No reachable divide-by-zero. Power-of-2 denominators make `4.0/denom` FP-exact, preserving
  two-run determinism.
- **Formula consistency holds.** `EvaluateBeatLiteral` (ExpressionEvaluator.cs:1041-1042) and
  `BeatConstructorFunctions.RegisterContextDependent` (lines 28-30) use the identical
  `pragma_on ? raw × (4.0/denom) : raw` expression.
- **Lexer identifier-guard is correct.** `Peek() == 'b' && !char.IsLetter(PeekNext())` protects
  `bar`/`beats`/`rule30b`/`test6b` etc.; `PeekNext()` returns `'\0'` at EOF so `2b` lexes. Signed
  `+1b`/`-2b` route through `TryLookAheadSpecialLiteral`. Tuple value-end (SimpleLexer.cs:450) lets
  `<<C4, 0.5b>>` and `<<C4, +1b>>` close correctly.
- **Lexical (not dynamic) capture for lambdas** is correct (ExpressionEvaluator.cs:836-840) and
  matches the `IsStrict` precedent.

Build succeeds under both Desktop and Web targets (BeatConstructorFunctions is pure logic, no
platform guards needed). All 66 Phase 45 xUnit tests pass; all four `tests/test_beat_*.flow`
smoke scripts produce the documented multiplier matrix; the full existing `.flow` suite shows no
real regressions (apparent "failures" are a grep-heuristic artifact — spot-checked clean).

Findings below are minor. No blockers.

## Warnings

### WR-01: `str(Beat)` round-trip is silently lossy under `beat-true-to-sig` — by design but undocumented at the call surface

**File:** `flow-lang/StandardLibrary/StdLib.cs:174-177` (`StrBeat`), interacts with `EvaluateBeatLiteral` at `flow-lang/Interpreter/ExpressionEvaluator.cs:1041-1042`

**Issue:** `StrBeat` emits the post-multiplier quarter-relative double with no `b` suffix
(`$"{args[0].As<double>()}"`). The XML-doc correctly notes that re-parsing the printed string under
the same pragma would re-multiply (e.g. `1b` in 6/8 → prints `0.5`; re-typing `0.5b` in 6/8 →
`0.25`). This is the intended contract (Beat is "a tagged double for printing"), and the chosen
plain-double form is the *least* surprising option. However, the lossiness is real: a composer who
copies a printed Beat value back into source under a non-4/4 meter with the pragma on gets a
different musical value, with no advisory. This is acceptable per the charitable-interpretation
philosophy, but it is a sharp edge worth a one-line composer-facing note in the `beat-true-to-sig`
tutorial / `examples/beat/` headers (the in-code XML-doc is contributor-facing only).

**Fix:** No code change required. Add a composer-facing caveat to `examples/beat/intro.flow` (and/or
the pragma's tutorial prose), e.g.:

```
Note: (str Beat) prints the quarter-relative double, NOT the source "Nb" form.
Note: Under enable beat-true-to-sig; in a non-4/4 meter, the printed value will
Note: re-multiply if pasted back as an Nb literal — print is for display, not round-trip.
```

Downgrade-resistant rationale for keeping this a WARNING (not INFO): it is a latent correctness
trap for the exact workflow the pragma encourages (compose-in-felt-beats, inspect, iterate).

## Info

### IN-01: Pragma-bit set happens before `PushFrame()`, outside the `try` (pre-existing pattern, inherited verbatim from Phase 44)

**File:** `flow-lang/Interpreter/Interpreter.cs:1149-1153`

**Issue:** `_context.BeatTrueToSig = proc.IsBeatTrueToSig` (line 1150) and the `prevBeatTrueToSig`
save (line 1149) execute *before* the `try` block opens at line 1155, with `_context.PushFrame()`
(line 1153) in between. If `PushFrame()` ever threw, the `finally` restore (line 1244) would not run
and both `StrictMode` and `BeatTrueToSig` would leak onto unwind. This is *not* a Phase 45
regression — it mirrors the existing `StrictMode` structure (lines 1138-1139) byte-for-byte, and the
recursion-depth guard above (lines 1119-1124) returns before any mutation, so the only gap is a
`PushFrame()` throw (not currently reachable). Flagged for awareness only; fixing would mean moving
both saves/sets inside the `try`, which is a Phase-44-scoped change, not Phase 45.

**Fix:** No action this phase. If revisited later, move `prevStrict`/`prevBeatTrueToSig` saves +
sets to the first lines inside the `try` so `PushFrame()` is covered by the same `finally`.

### IN-02: `?? 4` denominator fallback is dead under current `GetMusicalContext` contract

**File:** `flow-lang/Interpreter/ExpressionEvaluator.cs:1040` and `flow-lang/StandardLibrary/Audio/BeatConstructorFunctions.cs:28`

**Issue:** `GetMusicalContext()` always returns a non-null `TimeSignature` after its three-tier
fallback (call-stack → `FlowConfig.Active.DefaultTimesig` → hard-coded 4/4 at ExecutionContext.cs:885).
Therefore `TimeSignature?.Denominator ?? 4` can never take the `?? 4` branch in practice. This is
harmless defensive code and the right belt-and-suspenders posture (the `?? 4` documents the intended
identity default and survives any future change that lets `TimeSignature` go null), so no change is
recommended — noted only so a future reader does not mistake it for a reachable code path needing a
test.

**Fix:** None required. Optionally add a one-word comment (`// defensive — GetMusicalContext never
returns null TimeSignature today`) if clarity is desired.

---

_Reviewed: 2026-05-29_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
