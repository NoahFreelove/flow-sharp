# Phase 45: Beat Literal Syntax & True-to-Sig Pragma - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-25
**Phase:** 45-beat-literal-syntax-true-to-sig-pragma
**Areas discussed:** AST representation, Pragma scope ((beat N) under pragma), Signed +Nb / -Nb at expression-start, Str round-trip + tutorial scope, Test infrastructure

---

## Area 1: AST Representation for `Nb` Literal

| Option | Description | Selected |
|--------|-------------|----------|
| New `BeatLiteralExpression(RawValue, Loc)` | Own AST record alongside ChordLiteral/SongLiteral/SymbolLiteral/TupleLiteral. Carries raw source double; ExpressionEvaluator switch arm reads MusicalContext.TimeSignature + ExecutionContext.BeatTrueToSig at eval time. Clean separation; mirrors existing music-literal AST pattern. | ✓ |
| Tag flag on generic `LiteralExpression` | Reuse LiteralExpression with `needsBeatPragmaMultiplier` bool. Less code but bleeds music-specific concern into universal literal node; diverges from established own-record pattern. | |
| Lex-time-only, no eval-time lookup | Bake multiplier into token at lex time. Rejected: pragma + timesig context aren't reachable at lex time. | |

**User's choice:** New `BeatLiteralExpression(RawValue, Loc)` (Recommended)
**Notes:** Locked as D-01. The raw-value-plus-eval-time-context pattern is the only one that supports cross-file semantics (declaring file's pragma governs construction; consumers see quarter-relative double). Multiplier formula `raw × 4/denom` lives in ExpressionEvaluator switch arm. With pragma off OR default 4/4, multiplier is identity (1.0) — pragma activation doesn't corrupt scripts that never set a timesig.

---

## Area 2: Pragma Scope — `(beat N)` Constructor Under Pragma

| Option | Description | Selected |
|--------|-------------|----------|
| No escape hatch | Both `Nb` and `(beat N)` always honor the pragma. Composers wanting raw-quarter semantics split into non-pragma helper file. Smallest surface; aligns with pre-traction latitude. | ✓ |
| Add `(beatRaw N)` builtin | Explicit raw-quarter constructor available in both modes. More expressive but adds mental overhead (two constructors with subtly different semantics). | |
| Make `(beat N)` always raw; only `Nb` honors pragma | Reverses ROADMAP lock. DSL surface (`Nb`) honors context; explicit constructor (`(beat N)`) is escape hatch. Listed for completeness. | |

**User's choice:** No escape hatch (Recommended)
**Notes:** Locked as D-05. `(beat N)` registration at `BuiltInFunctions.cs:553` migrates from plain `Register` to `RegisterContextDependent` (Phase 43 D-08 precedent). Lambda receives ExecutionContext, reads `ctx.BeatTrueToSig` + active TimeSignature, applies multiplier. Pre-traction latitude (`project_pre_public_no_legacy_burden`) means we ship the smallest surface; `(beatRaw N)` follow-up reserved for if a real composer reports the need.

---

## Area 3: Signed `+Nb` / `-Nb` at Expression-Start

| Option | Description | Selected |
|--------|-------------|----------|
| Follow precedent: signed allowed both forms | Mirrors `+/-NdB`/`+/-Nst`/`+/-Nc`/`+/-Nms`/`+/-Ns`/`+/-NHz` — signed at expression-start via TryLexTypedLiteral; unsigned via ScanNumberOrSpecialLiteral. Runtime accepts negative beats as valid doubles. | ✓ |
| Unsigned only | Only `ScanNumberOrSpecialLiteral` gets `b` branch. `-2b` lexes as operator `-` + literal `2b`. Breaks music-literal-family precedent. | |
| Signed at expression-start, error on actually-negative value at construction | Lex signed for consistency but reject at runtime. Guard with no clear benefit. | |

**User's choice:** Follow precedent: signed allowed both forms (Recommended)
**Notes:** Locked as D-06/D-07/D-08. Lexer adds two branches: `TryLexTypedLiteral` for `+/-Nb` (signed at expression-start), `ScanNumberOrSpecialLiteral` for bare `Nb` (unsigned). Both use guard `Peek() == 'b' && !char.IsLetter(PeekNext())` matching existing `c` suffix's identifier-disambiguation. Keeps `1bar` lexing as `1` + `bar` identifier; keeps `2beats` lexing as `2` + `beats` identifier. Runtime accepts negative Beat values as valid doubles — no rejection guard.

---

## Area 4 (Sub-A): `(str someBeat)` Round-Trip

| Option | Description | Selected |
|--------|-------------|----------|
| Leave unchanged — emits plain double | `(str (beat 0.5))` keeps emitting `"0.5"`. Reason: emitting `"0.5b"` breaks round-trip under pragma (`0.5b` in 6/8 evaluates to 0.25 quarters; re-parsing `"0.25b"` re-multiplies to 0.125). Composer treats Beat as tagged double for printing. | ✓ |
| Emit `"Nb"` suffix, document round-trip caveat | Better composer ergonomics for `(print)` debugging. Cost: round-trip surprise under pragma + xUnit regression update for any existing tests asserting bare `"0.5"`. | |
| Emit canonical quarter form `"0.5 quarters"` | Verbose but unambiguous. Least ergonomic. | |

**User's choice:** Leave unchanged — emits plain double (Recommended)
**Notes:** Locked as D-14. Round-trip caveat exists under pragma; emitting suffix-form would make it worse, not better. If composer pressure surfaces, ships in a one-commit follow-up — possibly via a separate `(strFull someBeat)` variant that always emits canonical form alongside the literal-form `(str)`.

---

## Area 4 (Sub-B): Tutorial Scope

| Option | Description | Selected |
|--------|-------------|----------|
| Single 6/8 jig file | `examples/beat/intro.flow` showing pragma-off vs pragma-on. Matches `examples/scala/intro.flow` / `examples/sections/parameterized.flow` precedent. | |
| Two files — jig + cut-time | `intro.flow` (6/8 jig) + `cut-time.flow` (`timesig 2/2`). Demonstrates pragma across both common non-quarter meters. | ✓ |
| Three files — jig + cut-time + irregular meter | Adds 5/4 or 7/8 sample for stronger acceptance surface. More code. | |

**User's choice:** Two files — jig + cut-time
**Notes:** Locked as D-12. `examples/beat/intro.flow` (6/8 jig) + `examples/beat/cut-time.flow` (2/2 cut-time, `1b = half`). CLAUDE.md music-types table gets a new Beat row + one-line pragma-family-expansion mention (D-13). Irregular meter (5/4, 7/8) coverage moves to xUnit Facts (`BeatTrueToSigPragmaTests.cs`) instead of dedicated tutorial files — composer-facing surface stays focused on the two common non-quarter meters.

---

## Area 5: Test Infrastructure

| Option | Description | Selected |
|--------|-------------|----------|
| Two-track: positive `.flow` + xUnit Facts | Mirrors Phase 44 D-14 + Phase 43 REQ-MOD-12 precedent. `tests/test_beat_*.flow` exercise composer-facing scenarios; xUnit Facts pin lexer + AST + pragma + multiplier formula. | ✓ |
| `.flow` tests only | Cover via positive scripts. Loses fine-grained AST + lexer coverage. | |
| xUnit-only, no positive `.flow` tests | Cover via Facts. Loses end-to-end integration smoke. | |

**User's choice:** Two-track: positive `.flow` + xUnit Facts (Recommended)
**Notes:** Locked as D-11. Two `.flow` test files (`tests/test_beat_literal.flow` lexer-parser smoke + `tests/test_beat_pragma_off.flow` + `tests/test_beat_pragma_on.flow` + `tests/test_beat_cross_file.flow`). Two xUnit files (`flow-lang.Tests/Phase45/BeatLiteralParserTests.cs` + `BeatTrueToSigPragmaTests.cs`). Multiplier formula validated across 4/4, 6/8, 2/2, 5/4, 7/8 in Facts (irregular meters covered here rather than in tutorial files).

---

## Claude's Discretion

- Exact placement of `b` suffix branch in `TryLexTypedLiteral` ordering (between `+/-Nst` and `+/-NdB`, or elsewhere) — single-char `b` with non-letter guard is conflict-free among current suffixes; ordering is cosmetic.
- Whether to add a `BeatLiteralFacts.cs` regression file pinning Phase 26.1 DICT-01 acceptance of `(beat N)` as Dict-key constructor, or bundle into `BeatTrueToSigPragmaTests.cs`. Cheap insurance recommended.
- Order of execution (lexer vs parser vs evaluator vs pragma registry vs constructor migration). Plan-phase decides wave breakdown.
- Whether to vendor `flow-lang.Tests/baselines/Phase45/` audio baselines for the two tutorial WAVs — two-run cmp-clean preservation mandatory; commitment of reference renders is plan-phase's call.

## Deferred Ideas

- **`(beatRaw N)` escape hatch** — explicit raw-quarter constructor. Deferred per D-05; ships in one-commit follow-up if composer pressure surfaces.
- **`(str someBeat)` emitting `"0.5b"` suffix form** — deferred per D-14; round-trip semantics question must be resolved first.
- **REPL `:beat-true-to-sig on/off` sticky meta-command** — deferred per D-15; pragma is file-scope, REPL is ephemeral.
- **Dotted-rhythm `Nb.` syntax** — deferred per D-17; composers write `0.75b` directly.
- **Tied-Beat-literal syntax `Nb~`** — note-stream `~` is for tied notes; Beat literals can use prefix-only arithmetic. Deferred indefinitely.
- **Irregular-meter dedicated tutorial** (5/4 / 7/8) — coverage moved to xUnit Facts; tutorial stays focused on 6/8 + 2/2.
