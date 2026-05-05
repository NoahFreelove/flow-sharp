# Phase 26: Op Standardization (Prefix-Only) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-04
**Phase:** 26-op-standardization-prefix-only
**Areas discussed:** Negation strategy, Builtin overload shape, Migration approach, Legacy-infix diagnostic
**Mode:** default interactive (no flags)

---

## Negation Strategy

### Q1: How should variable negation `-x` work after Phase 26?

| Option | Description | Selected |
|--------|-------------|----------|
| `(neg x)` only — strict prefix | Removes ALL infix `-`. `-x` becomes a parse error pointing to `(neg x)`. Maximally consistent. | |
| Parser shorthand `-IDENT` → `(neg IDENT)` | Lexer emits `Minus IDENT`; parser, at expression-start, rewrites to `FunctionCallExpression("neg", ...)`. User-facing `-x` still works as sugar. | ✓ |
| `(neg x)` only + sugar inside note streams | Strict outside note streams; existing typed-literal handling inside. | |

**User's choice:** Parser shorthand `-IDENT` → `(neg IDENT)`
**Notes:** Lower migration cost; consistent with the ergonomics-priority principle later articulated.

### Q2: Negative number literals — which syntactic positions trigger single-token lexing?

| Option | Description | Selected |
|--------|-------------|----------|
| Expression-start only (roadmap minimum) | `-5` is one token at expression-start, after `(`, after `,`. Mirrors existing `-3dB`/`-5st` rule. | |
| Roadmap positions + after `[` and `->` | Adds array literal element start and flow operator RHS. Every position is one where an expression must begin. | ✓ |
| Anywhere a unary expression could start (most permissive) | Most ergonomic but adds lexer complexity and edge cases. | |

**User's choice:** Roadmap positions + after `[` and `->`
**Notes:** Six total positions: expression-start, `(`, `,`, `[`, `->`, plus inside note streams (Q3).

### Q3: Inside note streams, should plain negative numbers (`-5`, `-3.14`) lex as single tokens?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — unify rule inside note streams too | Plain `-5` and `-3.14` lex as single tokens; existing typed literals (`-3dB`/`+50c`/`-5st`) keep per-suffix handling. | ✓ |
| No — note streams keep existing behavior only | Plain `-5` inside `\| ... \|` would be `Minus IntLiteral`, then a context error. | |

**User's choice:** Yes — unify rule inside note streams too
**Notes:** Future-proofs note-stream syntax for any feature taking signed numeric literals (velocity offsets, etc.).

### Q4: What about unary plus (`+x`, `+5`)?

| Option | Description | Selected |
|--------|-------------|----------|
| Drop entirely | `+5` and `+x` become parse errors. Simpler grammar. | |
| Keep as no-op shorthand | Parser silently strips leading `+`. Charitable interpretation; matches `+50c` semantics. | ✓ |
| Drop, but keep `+number` in declarations | Symmetric with negative literal rule. | |

**User's choice:** Keep as no-op shorthand
**Notes:** Aligns with charitable-interpretation memory; minimal grammar overhead.

---

## Builtin Overload Shape

### Q1: How should (add)/(sub)/(mul)/(div) cover the Int→Long→Float→Double→Number widening chain?

| Option | Description | Selected |
|--------|-------------|----------|
| Per-type, same-type only (5 per op) | Same-type only; rely on convertible scoring for mixed. | (initially walked back) |
| Per-type matrix incl. mixed (25 per op) | All cross-type combos pre-shipped as exact matches. | ✓ (then revised) |
| Single polymorphic Number op | Just `(add Number Number)`. Loses static-type return precision. | |

**User's choice:** Per-type matrix incl. mixed (25 per op) — REVISED to two-tier strategy after follow-up.
**Notes:** User clarified during follow-up: "We always prioritize ergonomics. If a user wants to multiply a float and a double, let them, but they can't expect it to be as fast as multiplying a float and a float. The easy cases we should always make as fast as possible, but the flexible cases don't have to be." Revised to two-tier: 5 same-type fast paths + convertible-scoring fallback for mixed.

### Q2: Return-type rule for mixed pairs

| Option | Description | Selected |
|--------|-------------|----------|
| Widen to the wider operand | Follows existing widening chain. `(add Int Double)` → Double. | ✓ |
| Always promote to Double for Int/Long/Float mixes | Simpler matrix; collapses non-Number mixes to Double. | |
| Always Number for all mixed pairs | Most conservative; never loses precision. | |

**User's choice:** Widen to the wider operand
**Notes:** Predictable per the chain rule.

### Q3: How should the mixed-type fallback path be implemented?

| Option | Description | Selected |
|--------|-------------|----------|
| Convertible-scoring widen to wider operand | Mixed call hits OverloadResolver +100 scoring; narrower widens; wider type's same-type fast path runs. | ✓ |
| Single catch-all `(add Number Number)` per op | Explicit but boxes for every mixed call. | |
| Parser-side coercion insertion | Couples parser to type system. | |

**User's choice:** Convertible-scoring widen to wider operand
**Notes:** Re-uses existing machinery; no new fallback code; reaches same fast path after one coercion.

### Q4: How should `(neg)` be overloaded?

| Option | Description | Selected |
|--------|-------------|----------|
| Per-type, 5 overloads | `(neg Int)`, `(neg Long)`, `(neg Float)`, `(neg Double)`, `(neg Number)`. Return type matches input. | ✓ |
| Single polymorphic `(neg Number)` | Boxes to Number for every call. Breaks `Int x = (neg 5)`. | |
| Per-type + Sequence overload | Adds musical inversion semantics (out of scope). | |

**User's choice:** Per-type, 5 overloads
**Notes:** Matches add/sub/mul/div pattern; no surprise widening on negation.

### Q5: Cross-type div — `(div 1 2)` returns 0 (truncation). Acceptable?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — Int/Int truncates, mixed widens | Matches C# semantics. | |
| Promote Int/Int div to Double automatically | More ergonomic; but asymmetric. | |
| Add separate `(idiv Int Int)` for integer division | `(div 1 2) → 0.5`; `(idiv 1 2) → 0`. Foot-gun resolved. | ✓ |

**User's choice:** Add separate `(idiv Int Int)` for integer division
**Notes:** Composer-friendly default; explicit opt-in for integer division. `(div Int Int)` auto-promotes to Double; other same-type `(div)` overloads unchanged.

---

## Migration Approach

### Q1: How should we migrate the 82 .flow files containing infix arithmetic?

| Option | Description | Selected |
|--------|-------------|----------|
| Tokenizer-based rewrite tool | Re-uses SimpleLexer; walks tokens; idempotent. | ✓ |
| Regex sed sweep + spot-check | Faster but high false-positive risk. | |
| Transitional dual-parse + grace commit | Multi-stage with deprecation warnings. | |
| Manual rewrite per phase wave | Highest fidelity but ≥3 hours of toil. | |

**User's choice:** Tokenizer-based rewrite tool
**Notes:** Idempotent + correct handling of note-stream typed literals + strings + comments.

### Q2: Where should the migration tool live?

| Option | Description | Selected |
|--------|-------------|----------|
| Permanent tool in flow-lang.Tools/Migrate26 | Solution sub-project; downstream-user-friendly. | |
| Throwaway script in scripts/migrate-26.cs | One-off; kept as historical record. | ✓ |
| Inline in test fixtures — no standalone tool | Migration via xUnit Theory. | |

**User's choice:** Throwaway script in scripts/migrate-26.cs
**Notes:** No external users — permanent tool unjustified. (Pre-public memory codified later.)

### Q3: Migration commit/wave granularity

| Option | Description | Selected |
|--------|-------------|----------|
| Single mega-commit after parser change | Atomic; large diff but easy bisect target. | ✓ |
| Wave by file class | 4 waves: parser+overloads / stdlib / examples / tests. | |
| Wave by directory | 3 waves; combines compiler + stdlib. | |

**User's choice:** Single mega-commit after parser change
**Notes:** Atomic semantics — between commit 1 (parser change) and commit 2 (migrated files), the build compiles but no .flow file parses, so wave-by-wave green gates aren't possible.

### Q4: Byte-identical guarantee through the rewrite

| Option | Description | Selected |
|--------|-------------|----------|
| Pre/post hash check, no new test | One-time SHA256 gate during migration commit; Phase 18/25 tests carry the regression burden. | ✓ |
| Add Phase 26 byte-identical regression test | Mirror Phase 25 pattern; ~50 lines of new test scaffolding. | |
| Both — hash check AND new regression test | Maximum safety; minor duplication. | |

**User's choice:** Pre/post hash check, no new test
**Notes:** Phase 18 (`ByteIdenticalShowcaseTests.cs`) + Phase 25 (`ByteIdenticalShowcaseGaussianTests.cs`) already pin determinism; no need for a sibling Phase 26 test.

---

## Legacy-Infix Diagnostic

### Q1: What error message should the parser emit for stray `+`/`-`/`*`/`/` after Phase 26?

| Option | Description | Selected |
|--------|-------------|----------|
| Charitable migration hint with example | "infix `+` removed in v1.3 — use `(add a b)`. See: docs/migration-26.md". | |
| Hint with full op-name table | Full replacement table at every error site. | |
| Generic 'unexpected token' parse error | Standard parser fall-through. | ✓ |

**User's choice:** Generic 'unexpected token' parse error
**Notes:** Surprising vs. charitable-interpretation memory; rationale clarified in Q2.

### Q2: Confirmation — generic error rationale

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — fresh code expectation, strict error is fine | Migration tool sweeps existing code; new code is fresh. | |
| Yes, but ALSO add a one-liner footer link to docs/migration-26.md | Append to every parse error during migration window. | |
| Reconsider — prefer the charitable hint | Walk back to charitable migration hint. | |

**User's choice:** "Yes, the language isn't public yet so nobody has written any legacy code in it" (free-text)
**Notes:** This is a major contextual fact: Flow is pre-public, no external users, no legacy compatibility burden. Saved as project memory `project_pre_public_no_legacy_burden.md`. Implication: no `docs/migration-26.md`, no permanent dotnet migration tool, no charitable infix diagnostic, no deprecation cycles for breaking changes in this phase or future phases until public release.

---

## Claude's Discretion

User declined to drill into these areas; planner has flexibility:

- **Exact placement of negative-literal lex logic in `SimpleLexer.cs`** — extending the `case '+': case '-':` branch vs factoring out a `TryLexSignedNumber()` helper. Recommendation: helper, for testability.
- **Naming of the integer-division builtin** — `(idiv)` working name; alternatives `(divInt)`/`(quot)` rejected by convention (Lisp/Clojure precedent).
- **Whether `scripts/migrate-26.cs` is `.cs` (compiled) or `.csx` (script)** — recommendation: `.csx` via `dotnet script`, fall back to a small standalone csproj if the tool isn't installed.
- **Order of Long vs Number registrations** — cosmetic; recommendation: Long after Float to match the widening chain registration order.
- **Whether `(neg)` and `(idiv)` get a new `// Negation` block in `std.flow` or append to existing arithmetic block** — recommendation: append.

---

## Deferred Ideas

- **`(neg Sequence)` / `(neg Note)` musical-inversion overloads** — separate phase if the use case emerges; `invert(seq)` already exists for musical inversion.
- **Charitable migration hint diagnostic** — explicitly NOT shipped (Flow is pre-public). Revisit at first public release tag.
- **`docs/migration-26.md`** — not needed (no external users).
- **Permanent `flow-lang.Tools/Migrate26` dotnet tool** — not needed.
- **Mixed-type cross-overload matrix** — punted in favor of OverloadResolver convertible scoring; revisit if profiling shows convertible-scoring is a hot-path bottleneck.
- **Removing unused Pidgin parser-combinator dependency** — opportunistic cleanup; not bundled here.
- **Phase 26 byte-identical regression xUnit test** — Phase 18 + 25 tests are the persistent guards.
- **`(mod a b)` / `(rem a b)` / `(pow a b)`** — out of Phase 26 scope; would be a separate small builtin phase.
- **`flow-lsp` semantic-tokens explicit removal** — passive update via lexer; verify post-ship.
- **PROJECT.md milestone goal text update** mentioning Phase 26 explicitly — minor doc churn; planner may bundle into commit 3.
