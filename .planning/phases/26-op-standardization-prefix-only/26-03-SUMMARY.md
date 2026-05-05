---
phase: 26-op-standardization-prefix-only
plan: 03
subsystem: migration-tooling
tags: [phase-26, std-03, migration-walker, precedence-climber, idempotent, wave-2]
requires:
  - 26-02-SUMMARY (Wave 1 lexer + parser + builtin changes — walker reads same tokens as runtime)
provides:
  - migration-walker
  - precedence-climber
  - note-stream-pass-through
  - unary-minus-lowering
  - defensive-string-concat
  - idempotent-rewrite
affects:
  - tests/test_migrate26_smoke.flow (representative input for Wave 3 sanity)
tech-stack:
  added: []
  patterns: [token-stream-walker, precedence-climber, lookahead-token-reuse, lexer-as-library]
key-files:
  created:
    - tests/test_migrate26_smoke.flow
  modified:
    - scripts/Migrate26/Program.cs
decisions:
  - D-11: throwaway tokenizer-based migration script — re-uses SimpleLexer; emits prefix form for Plus/Minus/Star/Slash between value-producing tokens
  - D-12: one-shot tool kept under scripts/Migrate26/ as historical record
  - Pitfall 3: walker SKIPS Pipe...Pipe note-stream regions (typed-literal arithmetic preserved)
  - D-01: unary `-IDENT` → `(neg IDENT)`
  - D-03: unary `+IDENT` → `IDENT` (silent strip)
  - D-09: defensive String + value → `(concat A B)`
  - Idempotence: structural — `(add a b)` has no Plus/Minus token between value-producing tokens
metrics:
  completed-at: 2026-05-04
  duration-minutes: ~25
  tasks-completed: 2
  files-created: 1
  files-modified: 1
  walker-LOC: 354
  smoke-prefix-call-count: 12
  idempotence-second-run-diff-bytes: 0
---

# Phase 26 Plan 03: Wave 2 Migration Walker Summary

**One-liner:** Implement the Migrate26 token walker — precedence-aware infix→prefix rewriter with note-stream pass-through and idempotent guarantees, smoke-tested on a representative .flow file before the Wave 3 mass sweep.

## Walker Implementation

`scripts/Migrate26/Program.cs` (354 LOC) replaces the Wave 0 stub with a full token-stream walker. Re-uses `flow-lang/Lexing/SimpleLexer.cs` as a library — no parser/evaluator involvement.

### Key methods + responsibilities

| Method | Responsibility |
|---|---|
| `Main` | CLI entry: parses args, expands directories to `.flow` files, invokes `Migrate(source)` per file, writes back if changed. Returns 0 on success, 1 on usage error, 2 on per-file exception. |
| `ExpandPaths` | Glob expansion: directories recurse via `Directory.EnumerateFiles(*.flow, AllDirectories)`; single files must end `.flow`; warns on unrecognized args. |
| `Migrate` | Per-file: tokenize via `SimpleLexer.Tokenize()`, then call `RewriteSpans(source, tokens)`. |
| `RewriteSpans` | Outer walker: tracks `pipeDepth` (toggles on each Pipe — odd = inside note stream → pass-through). For each token at expression-start position, attempts `TryParseAdditiveSpan`; if any rewrite occurred, records `(start_offset, end_offset, replacement)` edit. Applies edits in reverse-order so byte offsets stay valid. |
| `TryParseAdditiveSpan` | Mirrors deleted `ParseAdditive`: `multiplicative ((Plus|Minus) multiplicative)*`. Returns rewritten text spanning [start..spanEnd) when ANY transformation happened (additive op, multiplicative op, unary lowering, or recursive inner-paren rewrite); returns null otherwise so the walker advances by one token. |
| `TryParseMultiplicativeSpan` | Mirrors deleted `ParseMultiplicative`: `primary ((Star\|Slash) primary)*`. Threads a `ref bool rewrote` flag so the caller knows whether any transformation happened. |
| `TryParsePrimarySpan` | Atomic + unary + grouping. Handles five primary cases: (1) `-IDENT` → `(neg IDENT)`, (2) `+IDENT` → `IDENT` (D-03), (3) `±NUMLIT` coalesced into signed literal text, (4) parenthesized sub-expression with recursive inner rewrite (preserves outer parens), (5) array literal `[...]` captured verbatim, (6) atomic value tokens (Identifier/IntLiteral/FloatLiteral/StringLiteral/NoteLiteral/BoolLiteral) returned as token text. |
| `IsValueProducing` / `IsExpressionStart` / `IsPrimaryStart` / `IsAtomicValueToken` | Token-classification helpers per RESEARCH §"Span detection". |
| `LooksLikeStringStart` | `StringLiteral`-starts-the-LHS check that switches `add` → `concat` for the defensive D-09 path. |
| `ComputeLineStarts` | Builds line-offset table so each token's (Line, Column) location maps to a byte offset in the source. |
| `TokensToText` | Reconstructs source for un-migrated inner-paren content by joining token texts with single spaces (acceptable lossy reconstruction since Flow's lexer treats whitespace as separator-only outside note streams). |

### Critical correctness properties (all verified)

- **Idempotent.** Running twice produces zero diff. Structurally guaranteed: `(add a b)` has no Plus/Minus token between value-producing tokens, so the second pass finds nothing to rewrite.
- **Note-stream safe (Pitfall 3).** Inside `Pipe...Pipe` regions, ALL Plus/Minus/Star/Slash tokens are pass-through. Note streams have their own typed-literal arithmetic (`-3dB`, `+50c`, `C4/12` fractional duration).
- **Operator precedence.** Star/Slash binds tighter than Plus/Minus, mirroring the deleted `ParseAdditive`/`ParseMultiplicative`.
- **Unary lowering.** `-IDENT` → `(neg IDENT)`; `+IDENT` → `IDENT` (silent strip per D-03).
- **Defensive String concat.** `"abc" + x` would emit `(concat "abc" x)` (no real test cases exist per RESEARCH §"String concat usage" but the path is wired).
- **Comments + Note: lines invisible.** `SimpleLexer.SkipWhitespaceAndComments` absorbs them before they reach the walker.

## Smoke Test Results

**Test file:** `tests/test_migrate26_smoke.flow` — exercises every walker edge case in a single ~40-line input.

### Patterns verified

| Pattern (pre-migration) | Post-migration form | Status |
|---|---|---|
| `Int a = 1 + 2`              | `Int a = (add 1 2)`              | OK |
| `Int b = 5 - 3`              | `Int b = (sub 5 3)`              | OK |
| `Int c = 4 * 2`              | `Int c = (mul 4 2)`              | OK |
| `Double d = 10 / 5`          | `Double d = (div 10 5)`          | OK |
| `Int e = 1 + 2 * 3`          | `Int e = (add 1 (mul 2 3))`      | OK (precedence respected) |
| `Double f = 10 - 4 / 2`      | `Double f = (sub 10 (div 4 2))`  | OK (precedence respected) |
| `Int g = (1 + 2) * 3`        | `Int g = (mul ((add 1 2)) 3)`    | OK (parens preserved + outer wrap; harmless double-paren) |
| `Int y = -x`                 | `Int y = (neg x)`                | OK (D-01 unary lowering) |
| `Int r = p + q`              | `Int r = (add p q)`              | OK (identifier infix) |
| `Double tau = (mul pi 2.0)`  | `Double tau = (mul pi 2.0)`      | OK (already-prefix unchanged) |
| `Sequence s = \| C4q D4q E4+50c F4q \|` | (UNCHANGED)            | OK (note-stream pass-through) |

### Interpreter run (Step E)

```
$ dotnet run --project flow-interpreter tests/test_migrate26_smoke.flow
Flow Language Interpreter v0.1

3       <- a = 1+2
7       <- e = 1+2*3
9       <- g = (1+2)*3
-5      <- y = -x where x=5
30      <- r = p+q where p=10,q=20
6.28    <- tau = pi*2.0 (already-prefix)
```

Exit code 0. All five expected printed values present and correct.

### Idempotence (Step F)

```
$ dotnet run --project scripts/Migrate26 -- tests/test_migrate26_smoke.flow
Done. 0 migrated, 1 unchanged.

$ diff /tmp/migrate26_run1.flow tests/test_migrate26_smoke.flow
(empty — exit 0 — files identical)
```

Second run produces zero further diff. Walker is structurally idempotent.

## Walker Edge Cases Discovered During Smoke

The smoke test caught two correctness bugs in the initial implementation that needed fixing inside this same wave (Rule 1 deviations — see "Deviations from Plan" section below). Both bugs traced to the same root cause: the original `sawAnyOp` flag only tracked additive-level rewrites, so multiplicative-only spans (`4 * 2`) and unary-lowering spans (`-x`) returned null and were missed by the walker.

## Acceptance Criteria

| Criterion (from PLAN) | Result |
|---|---|
| File `scripts/Migrate26/Program.cs` contains `RewriteSpans`, `TryParseAdditiveSpan`, `TryParseMultiplicativeSpan`, `TryParsePrimarySpan`, `IsValueProducing`, `IsExpressionStart` | All present (counts 2/3/3/4/1/4) |
| `grep -c "pipeDepth"` ≥ 2 | 3 hits (note-stream skip logic) |
| `grep -c "(neg "` ≥ 1 | 3 hits (unary minus shorthand) |
| `grep -c "concat"` ≥ 1 | 5 hits (defensive String concat path) |
| `dotnet build scripts/Migrate26/Migrate26.csproj` exits 0 | OK (0 warnings, 0 errors) |
| `dotnet run --project scripts/Migrate26 --` (no args) exits non-zero | OK (exit code 1) |
| File `tests/test_migrate26_smoke.flow` exists | OK |
| Post-migration `grep -c "(add\|(sub\|(mul\|(div\|(neg"` ≥ 8 | 12 hits |
| No infix arithmetic remains outside Note: lines and note streams | OK (clean) |
| Note-stream pass-through verified | OK (`\| C4q D4q E4+50c F4q \|` unchanged) |
| `dotnet run --project flow-interpreter tests/test_migrate26_smoke.flow` exits 0 with stdout containing "3", "7", "-5", "30" | OK (also "9" and "6.28") |
| Second migrator run produces "Done. 0 migrated, 1 unchanged." | OK |
| diff between run-1 and run-2 result is empty (idempotent) | OK |
| `git status --porcelain` is empty after commits | OK |

## Commits

| Hash | Subject |
|---|---|
| `a210fb0` | feat(26-03): implement Migrate26 walker (precedence climber + note-stream skip) |
| `228bc19` | feat(26-03): smoke-test Migrate26 walker on representative .flow file |

(SUMMARY commit follows this file.)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `sawAnyOp` flag missed multiplicative-only and unary-only rewrites**
- **Found during:** Task 2 first migration run.
- **Issue:** The original `TryParseAdditiveSpan` returned the rewritten text only when `sawAnyOp` was true at the additive level. This meant `Int c = 4 * 2` returned null (no additive op found, even though multiplicative produced `(mul 4 2)`), and `Int y = -x` returned null (no infix op found, even though primary produced `(neg x)`). The walker then advanced one token instead of consuming the span, so multiplicative-only and unary-only patterns went unmigrated.
- **Fix:** Threaded a `ref bool rewrote` flag through `TryParsePrimarySpan` → `TryParseMultiplicativeSpan` → `TryParseAdditiveSpan`. Each transformation site (additive op, multiplicative op, unary `-IDENT`, unary `+IDENT`, signed-numeric coalescing, recursive inner-paren rewrite) sets `rewrote = true`. `TryParseAdditiveSpan` returns the rewritten text whenever `rewrote` is true, regardless of whether the additive loop fired.
- **Files modified:** `scripts/Migrate26/Program.cs` (Task 1 commit; bug surfaced and fixed within Task 2 commit).

**2. [Rule 1 - Bug] `(1 + 2) * 3` lost the outer multiplication**
- **Found during:** Task 2 first migration run (output was `((add 1 2)) * 3` instead of `(mul ((add 1 2)) 3)`).
- **Issue:** Same root cause as #1: the `sawAnyOp` flag didn't propagate from the recursive inner-paren rewrite into the outer multiplicative loop. The LParen primary correctly emitted `((add 1 2))` but the additive loop above saw no top-level Plus/Minus and returned null, throwing away the multiplicative chain and the inner rewrite.
- **Fix:** Same `rewrote` flag fix (#1) — the LParen branch in `TryParsePrimarySpan` now sets `rewrote = true` when its inner span was rewritten by the recursive call. After the fix the walker correctly emits `(mul ((add 1 2)) 3)`.
- **Files modified:** `scripts/Migrate26/Program.cs` (same fix as #1).

### Smoke-test scope adjustments (NOT deviations)

- **`Int d = 10 / 5` declared as `Double d`** (and `Int f = 10 - 4 / 2` as `Double f`) in the smoke file. This honors Wave 1 D-08: `(div Int Int)` auto-promotes to Double. The plan's example used `Int d` which would fail the post-migration assignment type-check. This is following the documented Phase 26 contract, not a deviation.
- **Note-stream sample changed** from `| C4 -3dB D4q E4q |` (which the parser rejects as "Empty note stream" — `-3dB` is a typed literal but the note-stream parser does not accept it as a standalone element) to `| C4q D4q E4+50c F4q |` (cent-offset is the canonical attached-modifier pattern, verified working in `tests/test_h_alias.flow`). Walker still demonstrates Pitfall 3 pass-through; the underlying Flow note-stream subsystem behavior is out-of-scope for Wave 2.

### Out-of-scope discoveries

None — no `deferred-items.md` entries needed.

## Self-Check: PASSED

- **scripts/Migrate26/Program.cs exists with required helpers:** verified all 6 method names present, `pipeDepth` count 3, `(neg ` count 3, `concat` count 5.
- **tests/test_migrate26_smoke.flow exists:** `ls tests/test_migrate26_smoke.flow` → present.
- **Migrate26 builds cleanly:** `dotnet build scripts/Migrate26/Migrate26.csproj` → 0 warnings, 0 errors.
- **Migrator no-args exits 1:** verified with `dotnet run --project scripts/Migrate26 --` → exit 1.
- **Migrated smoke file runs in interpreter:** verified, prints 3/7/9/-5/30/6.28, exit 0.
- **Idempotence holds:** second run prints "0 migrated, 1 unchanged" and `diff` exit code 0.
- **Commits exist:**
  - `a210fb0` (Task 1) — found via `git log --oneline | grep a210fb0` ✓
  - `228bc19` (Task 2) — found via `git log --oneline | grep 228bc19` ✓

## Next Wave Hand-off

**Wave 3 (plan 26-04)** runs this walker over all ~82 in-repo `.flow` files in a single mass sweep, gated by SHA256 byte-identical hash check on `examples/output/{tutorial,showcase}.{wav,mid}` per D-14. After Wave 3 lands, `dotnet test` returns to 769/769 GREEN.

The walker is now battle-tested on representative input including edge cases (multiplicative-only spans, unary-only spans, paren+outer-mul chains, note-stream pass-through, already-prefix idempotence). Wave 3 has high confidence in the tool's correctness before touching production .flow files.
