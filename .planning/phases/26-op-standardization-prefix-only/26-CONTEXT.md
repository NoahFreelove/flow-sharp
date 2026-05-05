# Phase 26: Op Standardization (Prefix-Only) - Context

**Gathered:** 2026-05-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Eliminate infix arithmetic (`+`, `-`, `*`, `/`) from the Flow expression grammar in favor of S-expression prefix builtins, aligning the entire language with the no-infix-operators philosophy (memory: `feedback_language_philosophy.md`). Three coordinated changes land together:

1. **AST/parser surgery.** Delete `flow-lang/Ast/Expressions/BinaryExpression.cs` (the `BinaryExpression` record + the `BinaryOperator` enum). Remove `ParseAdditive`, `ParseMultiplicative`, and the `ParseUnary` arithmetic branch in `Parser.cs`. `ParseFlowExpression` now calls `ParsePostfix` directly. `ExpressionEvaluator.EvaluateBinary` is deleted along with its dispatch case in the expression `switch`.
2. **Builtin completion.** Extend `(add)`/`(sub)`/`(mul)`/`(div)` from the existing 3-type coverage (Int/Float/Double) to the full numeric widening chain (Int → Long → Float → Double → Number). Ship `(neg)` as 5 per-type overloads. Ship `(idiv Int Int) → Int` for integer division. Existing `(concat String String)` is unchanged.
3. **Lexer extension.** Negative number literals (`-5`, `-3.14`) lex as single tokens at expression-start positions (after `=`/`:` in declarations + statement start), after `(`, after `,`, after `[`, and after `->`. Inside `| ... |` note streams, the same rule applies (in addition to the existing `-3dB`/`-5st`/`+50c` typed-literal handling). Outside those positions, `Minus`/`Plus` tokens still lex as before for note-stream typed literals and for the parser-shorthand `-IDENT → (neg IDENT)`.

Then migrate every in-repo `.flow` file (~82 of 97) from infix to prefix form via a one-shot tokenizer-based migration script, and update `CLAUDE.md` to remove the stale "==, !=, <, >" claim and document the prefix-only rule.

Locked by ROADMAP.md Phase 26 success criteria 1–5 and v1.3 dependency: this phase MUST precede Phase 26.1 so the new dict/tuple/symbol features inherit the prefix-only base.

**In scope:**
- Delete `flow-lang/Ast/Expressions/BinaryExpression.cs` (record + enum).
- Remove `ParseAdditive`, `ParseMultiplicative`, the `Match(TokenType.Minus, TokenType.Plus)` branch of `ParseUnary` (lines 747–763 of `Parser.cs`), and rewire `ParseFlowExpression` to call `ParsePostfix` directly.
- Delete `EvaluateBinary` (lines 250–335 of `ExpressionEvaluator.cs`) and its `BinaryExpression` case in the `switch` at line 39.
- Add Long + Number same-type overloads to `(add)/(sub)/(mul)/(div)` in `flow-lang/StandardLibrary/BuiltInFunctions.cs` adjacent to lines 215–271.
- Add `(neg Int)`, `(neg Long)`, `(neg Float)`, `(neg Double)`, `(neg Number)` builtins (5 per-type overloads).
- Add `(idiv Int Int) → Int` for integer division (since `(div Int Int)` now auto-promotes to Double).
- Update `flow-lang/std.flow` with new `internal proc` declarations for every new overload (Long/Number variants of add/sub/mul/div, all `neg` overloads, `idiv`).
- Lexer (`flow-lang/Lexing/SimpleLexer.cs`) extension: detect expression-start positions and emit single-token `IntLiteral(-5)` / `FloatLiteral(-3.14)` / `DoubleLiteral(-3.14)` for `-DIGIT` / `+DIGIT` (unary `+` is silently absorbed; `+5` lexes as `5`). Inside `| ... |` note streams, the same rule applies.
- Parser shorthand: when `Minus` token appears at expression-start followed directly by an identifier, emit `FunctionCallExpression("neg", [VariableExpression])` instead of `BinaryExpression`. Unary `+IDENT` is silently stripped (parser produces `VariableExpression` only).
- Throwaway migration script `scripts/migrate-26.cs` (or `.csx`) that re-uses `flow-lang/Lexing/SimpleLexer.cs` to walk tokens and emit prefix-form output for every `Plus`/`Minus`/`Star`/`Slash` token between value-producing tokens. Idempotent — running twice produces no further diff.
- One-shot migration of ~82 in-repo `.flow` files (tests/, examples/, flow-lang/*.flow) committed atomically AFTER the parser/registry/lexer commit lands and the build is green.
- SHA256 hash gate during the migration commit: capture `examples/output/tutorial.{wav,mid}` + `examples/output/showcase.{wav,mid}` digests pre-migration, recompute post-migration, abort and bisect if any digest differs.
- `CLAUDE.md` update: remove the "==, !=, <, >, etc." claim (line 154 — comparisons aren't even infix today; the claim is stale) and document the prefix-only rule. No new `docs/migration-26.md` (per `project_pre_public_no_legacy_burden.md` — no external users).

**Out of scope (deferred or other phases):**
- `BinaryExpression` overloads for comparison ops (`==`, `!=`, `<`, `>`) — those don't exist in the AST today; CLAUDE.md is wrong. Comparison ops are already prefix (`(eq)`, `(lt)`, `(gt)` style) in the existing builtins. Phase 26 just removes the stale CLAUDE.md claim.
- Phase 26.1 features (Symbol primitive `#foo`, Tuple `<<a, b>>` literal, `~>` unpack, Dict<K,V>) — handled in the next phase, which depends on Phase 26's prefix-only base.
- Charitable migration hint diagnostic ("infix removed — use `(add a b)`") — explicitly NOT shipped per memory `project_pre_public_no_legacy_burden.md`. Generic 'unexpected token' parse error is acceptable.
- `docs/migration-26.md` for end users — not needed; no external users.
- Permanent `flow-lang.Tools/Migrate26` dotnet tool — not needed; throwaway script is sufficient.
- A new Phase 26 byte-identical regression xUnit test (e.g., `ByteIdenticalShowcasePrefixTests.cs`). Phase 18 + Phase 25 byte-identical tests already pin the determinism contract; the SHA256 hash gate during migration is the one-time check.
- `(neg Sequence)` / `(neg Note)` musical-inversion overloads — out of scope; `(neg)` is purely numeric. If musical inversion is wanted, that's a separate `(invert)` transform (which already exists in `TransformFunctions.cs`).
- Mixed-type cross-overload matrix (e.g., explicit `(add Int Long)`, `(add Float Double)`, all 25 per op). Same-type fast paths only; mixed types fall through OverloadResolver convertible scoring.
- Removing the unused Pidgin parser-combinator dependency from the csproj. Listed under "Future Requirements" in REQUIREMENTS.md as opportunistic cleanup; not bundled here.
- `flow-lsp` semantic-tokens update for the removed operators. The LSP already tokenizes via the same lexer; once `Plus`/`Minus`/`Star`/`Slash` no longer drive arithmetic, the LSP semantic-token classification updates automatically.

</domain>

<decisions>
## Implementation Decisions

### Negation Strategy (DA-1)

- **D-01:** Variable negation `-x` is implemented as a **parser shorthand**, not a `BinaryExpression`. When the parser sees a `Minus` token at expression-start followed directly by an identifier (no intervening whitespace gating, but the `Minus` must not have a value-producing token to its left), emit `FunctionCallExpression("neg", [VariableExpression(name)])`. User-facing: `-x` still works; under the hood it lowers to `(neg x)`. This preserves ergonomics (memory `feedback_ergonomics_priority.md`) without re-introducing `BinaryExpression`.
- **D-02:** Negative number literals (`-5`, `-3.14`) are lexed as **single tokens** at the following positions:
  - **Expression-start** — after `=` or `:` in variable declarations; as the first non-whitespace token of any statement; after a keyword that introduces an expression context (`return`, `Note:` body, etc.).
  - **After `(`** — first token inside an open paren (S-expr arg list or grouping).
  - **After `,`** — argument separators in function calls.
  - **After `[`** — array literal element start.
  - **After `->`** — flow operator right-hand side.
  - **Inside `| ... |` note streams** — unify the rule; `-5` and `-3.14` lex as single tokens here too. Already-special semantic literals (`-3dB`, `+50c`, `-5st`, `-N`/`+N` semitone tags) keep their per-suffix handling unchanged.
  Anywhere else, `Minus`/`Plus` lex as standalone tokens (note-stream typed literals + parser shorthand handle the remaining cases).
- **D-03:** Unary `+` is **kept as a no-op shorthand**. `+5` parses as `5`; `+x` parses as `x` (the `Plus` token is silently absorbed at expression-start). Matches `+50c` semantics in note streams; aligns with `feedback_charitable_interpretation.md` ("composers typing `+x` get the obvious meaning"). No `BinaryExpression` produced.
- **D-04:** Lex positions for negative literals follow the principle "every position where an expression must begin." Implementation strategy: track the previous-emitted-token type in `SimpleLexer`; when `_lastEmittedType` is one of `(LParen, Comma, LBracket, Arrow, Equals, Colon, statement-start, Pipe-open-of-note-stream)` AND the next chars match `[+-]\d`, emit `IntLiteral` / `FloatLiteral` / `DoubleLiteral` directly. Else fall through to `SingleChar(Minus)` / `SingleChar(Plus)`.

### Builtin Overload Shape (DA-2)

- **D-05:** Two-tier overload strategy (driven by user feedback `feedback_ergonomics_priority.md`):
  - **Fast path:** 5 same-type overloads per op: `(add Int Int) → Int`, `(add Long Long) → Long`, `(add Float Float) → Float`, `(add Double Double) → Double`, `(add Number Number) → Number`. Same for `sub`, `mul`, `div`. Implementations use direct C# operators on the underlying CLR primitive (e.g., `int + int` for `(add Int Int)`, no `Convert.*`, no boxing). Exact-match (+1000) hit at OverloadResolver. Result type narrows to input type — no surprise widening on the hot path.
  - **Flexible path:** mixed-type calls (e.g., `(add Float Double)`) fall through OverloadResolver's convertible scoring (+100). The narrower operand widens to the wider operand using existing Value coercion machinery; the wider type's same-type fast path executes. Composer pays one coercion + one fast-path call. No separate catch-all overload, no parser-side coercion.
- **D-06:** Mixed-type return rule: **wider operand wins** (Int < Long < Float < Double < Number). `(add Float Double) → Double`, `(add Long Number) → Number`, etc. Predictable per the existing widening chain.
- **D-07:** `(neg)` ships 5 per-type overloads: `(neg Int) → Int`, `(neg Long) → Long`, `(neg Float) → Float`, `(neg Double) → Double`, `(neg Number) → Number`. Return type matches input. No Sequence/Note overload (out of scope).
- **D-08:** `(div Int Int)` **auto-promotes to Double** (returns `Double`, not `Int`). The Int-truncation foot-gun is unergonomic; instead ship `(idiv Int Int) → Int` for integer division. `(div 1 2) → 0.5`; `(idiv 1 2) → 0`. All other same-type `(div)` overloads (`Long/Long`, `Float/Float`, `Double/Double`, `Number/Number`) are NOT promoted — they return their input type. Asymmetry is intentional and documented.
- **D-09:** `(concat String String)` is **already shipped** at `BuiltInFunctions.cs:200`. The String-Add path in the existing `EvaluateBinary` (lines 255–259) is removed entirely. Migrating any `"a" + b` becomes `(concat "a" b)`. Existing `(concat Array Array)` at `BuiltInFunctions.cs:481` is unchanged.
- **D-10:** Performance philosophy (per `feedback_ergonomics_priority.md` performance corollary): same-type fast paths target raw CLR primitives with no allocation; mixed-type paths only need to *work* — they don't need to match same-type speed. Flow is interpreted; do not over-optimize the flexible path.

### Migration Approach (DA-3)

- **D-11:** Migration tool: **throwaway tokenizer-based script** at `scripts/migrate-26.cs` (or `.csx`). Re-uses `flow-lang/Lexing/SimpleLexer.cs` to walk tokens; for every `Plus`/`Minus`/`Star`/`Slash` token between value-producing tokens, emits the prefix form (`(add A B)`, `(sub A B)`, `(mul A B)`, `(div A B)`). Handles nested arithmetic by recursive emission (`a + b * c` → `(add a (mul b c))` after precedence resolution). String concatenation (`"a" + b`) emits `(concat "a" b)`. Idempotent — running twice produces zero further diff. Skips strings, comments (`Note:` lines + `//` line comments), and existing prefix calls.
- **D-12:** Tool lifecycle: **one-shot, kept as historical record**. Script lives in `scripts/` for future similar migrations (e.g., Phase 26.1 dict literal). NOT a permanent dotnet tool; NOT shipped to end users (no external users per `project_pre_public_no_legacy_burden.md`).
- **D-13:** Commit granularity: **single mega-commit after parser change** (per user choice).
  - Commit 1 — "feat(26): prefix-only AST + builtin completion" — deletes `BinaryExpression.cs`, removes `ParseAdditive`/`ParseMultiplicative`/unary-arithmetic branch in `ParseUnary`/`EvaluateBinary`, adds Long+Number overloads + `(neg)` 5-pack + `(idiv)`, extends lexer for negative-literal positions + `+x` no-op + `-IDENT → (neg)` shorthand. After this commit, the build still compiles but EVERY `.flow` file with infix arithmetic FAILS to parse. (No tests run between commits — the green gate is the migration commit.)
  - Commit 2 — "chore(26): migrate all .flow files to prefix form" — runs `scripts/migrate-26.cs` over `tests/`, `examples/`, `flow-lang/*.flow`. Commits all 82 file rewrites at once. After this commit the entire test suite + tutorial + showcase parse and run.
  - Commit 3 — "docs(26): CLAUDE.md prefix-only rule + remove stale infix claim".
- **D-14:** Byte-identical guarantee gate: **pre/post SHA256 hash check** during commit 2. Run `dotnet run --project flow-interpreter examples/tutorial.flow` + `examples/showcase.flow` BEFORE migration; record SHA256 of `tutorial.wav`, `tutorial.mid`, `showcase.wav`, `showcase.mid`. Run again AFTER migration; abort and bisect if any digest differs. Phase 18 (`ByteIdenticalShowcaseTests.cs`) + Phase 25 (`ByteIdenticalShowcaseGaussianTests.cs`) regression tests are the persistent guards — they MUST stay GREEN through the rewrite. **No new Phase 26 byte-identical xUnit test** is added (avoids duplication).

### Legacy-Infix Diagnostic (DA-4)

- **D-15:** **Generic 'unexpected token' parse error** is sufficient. When the parser hits a stray `+`/`-`/`*`/`/` between value-producing tokens after Phase 26, fall through to standard `ReportError($"Unexpected token '{tok.Lexeme}' at line {tok.Location.Line} col {tok.Location.Column}")`. No charitable migration hint, no special detection logic, no `docs/migration-26.md`.
- **D-16:** Rationale: **Flow is pre-public** (memory `project_pre_public_no_legacy_burden.md`). The migration script handles every existing `.flow` file in one sweep. Nobody outside this repo has written Flow code; nobody will hit the 'unexpected token' error from legacy code. Anyone writing fresh code post-rewrite has the new `tutorial.flow` + `CLAUDE.md` as canonical references — a generic error is enough.
- **D-17:** **Deviation from `feedback_charitable_interpretation.md`** is intentional and bounded by D-16. The charitable-interpretation memory's edge-case clause ("does NOT apply to syntax that was just removed where the cost is borne entirely by us") is the active path here. If/when Flow ships publicly, revisit and add the migration hint.

### Claude's Discretion

- **Exact placement of negative-literal lex logic in `SimpleLexer.cs`** — planner picks between (a) extending the existing `case '+': case '-':` branch at lines 108–109 with a peek-back at `_lastEmittedType`, or (b) factoring out a `TryLexSignedNumber()` helper. Recommendation: (b) — testability and clarity outweigh the small refactor cost.
- **Naming of the integer-division builtin** — `(idiv)` is the working name. Alternatives: `(divInt)`, `(quot)`. `(idiv)` matches Common Lisp / Clojure / Scheme convention. Recommendation: keep `(idiv)`.
- **Whether `scripts/migrate-26.cs` is a `.cs` (compiled) or `.csx` (script)** — `.csx` is faster to iterate, no project file needed; `.cs` integrates with the solution. Recommendation: `.csx` invoked via `dotnet script` (or, if `dotnet script` isn't installed, a small standalone csproj under `scripts/Migrate26/`).
- **Order of Long vs Number registrations** — purely cosmetic. Recommendation: Long immediately after Float (matches the widening chain Int→Long→Float→Double→Number registration order).
- **Whether `(neg)` and `(idiv)` get `internal proc` declarations grouped under a new `// Negation` / `// Integer division` block in `std.flow` or appended to the existing `// arithmetic` block at lines 38–49.** Recommendation: append to existing block; one comment block per category is cleaner than per-builtin sub-blocks.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 26 Locked Requirements
- `.planning/ROADMAP.md` lines 207–217 — Phase 26 entry: goal, depends-on, requirements (STD-01/02/03), success criteria 1–5.
- `.planning/REQUIREMENTS.md` lines 124–158 — Traceability table. Note: STD-01/02/03 are referenced in ROADMAP.md but NOT yet listed in REQUIREMENTS.md's body — planner should add them under a new `### Operator Standardization` section to keep traceability consistent.
- `.planning/STATE.md` lines 1–20 — Current position: Phase 25 shipped; Phase 26 ready to plan; v1.3 milestone 8/10 phases complete.
- `.planning/PROJECT.md` lines 1–80 — v1.3 milestone goal, target features (note: ROADMAP describes prefix-only standardization as a "language consistency pass"; PROJECT.md frames it as "foundation").

### Existing Code This Phase Touches
- `flow-lang/Ast/Expressions/BinaryExpression.cs` — entire file deleted. Contains `BinaryExpression` record + `BinaryOperator` enum (4 values: Add/Subtract/Multiply/Divide).
- `flow-lang/Parsing/Parser.cs:713–728` — `ParseAdditive` method removed. Lines 730–745 — `ParseMultiplicative` removed. Lines 747–763 — `ParseUnary` arithmetic branch (`Match(TokenType.Minus, TokenType.Plus)`) removed; the method becomes a thin pass-through to `ParsePostfix` OR is deleted entirely if unused. Line 668 — `ParseFlowExpression`'s `ParseAdditive()` call rewires to `ParsePostfix()`.
- `flow-lang/Parsing/Parser.cs:121, 130, 140` — these references to `Minus`/`Plus` tokens belong to musical-context parsing (tempo/swing) and are PRESERVED. Confirm with planner before any edit.
- `flow-lang/Parsing/Parser.cs:450, 465, 527, 542, 556` — `Match(TokenType.Minus)` calls in tempo/swing/pan/gain context parsing — PRESERVED. These don't go through `ParseAdditive`.
- `flow-lang/Interpreter/ExpressionEvaluator.cs:39` — `BinaryExpression` switch case removed.
- `flow-lang/Interpreter/ExpressionEvaluator.cs:250–335` — entire `EvaluateBinary` method deleted (including the String-Add path at lines 255–259).
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:213–271` — existing add/sub/mul/div Int/Float/Double registrations. Add Long + Number registrations adjacent to these. Add `(neg)` 5-pack + `(idiv Int Int)` in the same block.
- `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` — used by registry; no edits expected unless overload resolution scoring needs adjustment.
- `flow-lang/TypeSystem/OverloadResolver.cs` — convertible scoring (+100) drives the mixed-type fallback path. No edits expected unless a Long/Number conversion path is missing.
- `flow-lang/Lexing/SimpleLexer.cs:108–111` — `Plus/Minus/Star/Slash` `SingleChar` cases. Extend `Plus/Minus` with the negative-literal-at-expression-start logic per D-04. `Star`/`Slash` handling is unchanged (they remain valid tokens for note-stream `/N` fractional duration syntax + `*` is otherwise unused outside arithmetic).
- `flow-lang/Lexing/SimpleLexer.cs:832` — `IsValidIdentChar` includes `+/-/*//` — preserved (these still appear in note-stream typed literals like `-3dB`).
- `flow-lang/std.flow:38–49` — existing arithmetic `internal proc` declarations. Append Long/Number variants of `add/sub/mul/div`; append `neg` 5-pack; append `idiv` declaration.

### Migration Targets
- `tests/*.flow` — 33 files contain infix arithmetic per `grep -lE "[a-zA-Z0-9)] [+*/-] [a-zA-Z0-9(]" tests/*.flow`. Full count of files needing migration is 82 of 97 across `tests/`, `examples/`, and `flow-lang/`.
- `flow-lang/std.flow`, `flow-lang/audio.flow`, `flow-lang/bars.flow`, `flow-lang/collections.flow`, `flow-lang/composition.flow`, `flow-lang/notation.flow`, `flow-lang/test.flow` — 7 stdlib `.flow` modules. The migration script handles them in the same sweep.
- `examples/tutorial.flow` lines 51–73 — visible infix demos (`Int sum = 10 + 25` style); migration replaces these with `(add 10 25)`. Note: lines 55–56 already document the prefix style ("Flow uses S-expression style: (functionName arg1 arg2)") — the migration removes the redundant infix illustration on lines 56–58.
- `examples/showcase.flow` — already largely prefix; only one or two infix sites expected.

### Test Patterns to Follow
- `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` — Phase 18 byte-identical baseline. Phase 26 does NOT add a sibling test (per D-14); this one + Phase 25's are the persistent guards.
- `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` — Phase 25 byte-identical regression. Same role as above for Gaussian humanize. MUST stay GREEN through the Phase 26 rewrite.
- Unit Facts under `flow-lang.Tests/Unit/Phase26/` — recommended new tests: `NegOverloadFacts.cs` (5 per-type overloads return correct type + value), `IntegerDivisionFacts.cs` (`(idiv 1 2) == 0`, `(div 1 2) == 0.5`), `MixedTypeArithmeticFacts.cs` (convertible-scoring widens correctly: `(add Float Double) → Double`, `(mul Long Number) → Number`).
- `flow-lang.Tests/Unit/Phase26/NegativeLiteralLexFacts.cs` (recommended new) — Theory `[InlineData]` matrix over the 6 lex positions (expression-start / `(` / `,` / `[` / `->` / inside-note-stream) confirming `-5` lexes as a single `IntLiteral` token, NOT as two tokens.

### Project Memory (CLAUDE.md auto-memory)
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/feedback_language_philosophy.md` — the canonical source for "Keep functional S-expression style, no infix operators, Haskell-inspired". This phase IS the enforcement mechanism for that memory.
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/feedback_ergonomics_priority.md` — drove D-01 (parser shorthand for `-IDENT`), D-03 (unary `+` kept), D-05 (two-tier overload strategy, fast same-type + flexible mixed), D-08 (`(div Int Int)` auto-promote to Double + ship `(idiv)`), D-10 (performance corollary: easy cases fast, flexible cases flexible).
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/feedback_charitable_interpretation.md` — informed D-03 (`+x` silently absorbed) and D-04 (note-stream `+50c`/`-3dB` semantics preserved). Bounded by D-17 — the charitable-hint diagnostic is NOT shipped because of D-16/`project_pre_public_no_legacy_burden.md`.
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/project_pre_public_no_legacy_burden.md` — drove D-15 (generic 'unexpected token' is fine), D-16 (no `docs/migration-26.md`), D-12 (throwaway script vs permanent dotnet tool). Created during this discussion.

### Documentation Updates
- `CLAUDE.md` line 154 — current text: `| BinaryExpression | Binary operations (+, -, *, /, ==, !=, <, >, etc.) |`. Replace with: row deletion (BinaryExpression no longer exists) + a note in the AST table that arithmetic is via `(add)/(sub)/(mul)/(div)/(neg)/(idiv)` builtins. Comparison ops are already prefix; the stale `==/!=/<,>` claim is incorrect and is removed.
- `CLAUDE.md` Language Features section ("### Core") — line ~125 lists "Flow operator `->` for function chaining". Add "Prefix-only arithmetic via `(add)`/`(sub)`/`(mul)`/`(div)`/`(neg)`/`(idiv)` and `(concat)` builtins (no infix `+ - * /`)" as a sibling bullet.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **Existing `(add Int Int)` / `(add Float Float)` / `(add Double Double)`** registrations at `BuiltInFunctions.cs:215, 220, 256` — implementation lambdas in `StdLib.AddInt` / `StdLib.AddFloat` / `StdLib.AddDouble`. Phase 26's Long + Number overloads follow the exact same registration shape; the lambdas need new sibling methods (`StdLib.AddLong`, `StdLib.AddNumber`) under the same partial class.
- **Existing `(concat String String)`** at `BuiltInFunctions.cs:200` — covers the String-Add migration path with no new registration. The migration script emits `(concat a b)` whenever it finds string `+`.
- **Existing `(concat Array Array)`** at `BuiltInFunctions.cs:481` — unchanged. Note: name is overloaded across String pair AND Array pair; `concat` is one of the few cross-domain builtins. No edits.
- **Existing OverloadResolver convertible scoring** (`flow-lang/TypeSystem/OverloadResolver.cs`) — the `+100` "convertible" tier is exactly the path mixed-type calls take. Verify (during planning) that Int→Long→Float→Double→Number all have correct `CanConvertTo` chains; if any link is missing the mixed-type path won't resolve.
- **`SimpleLexer._lastEmittedType` (or equivalent)** — if not already tracked, add it. Knowing the previous token's type is the key to negative-literal lex disambiguation.
- **Existing typed-literal lex code for `-3dB`/`+50c`/`-5st`/`-Nst`/`+N`** in `SimpleLexer.cs` (in note-stream context) — already handles signed numeric prefixes inside `| ... |`. The new `-5` / `-3.14` rule for plain numbers can reuse the same prev-token gate.

### Established Patterns
- **`internal proc <name> (Type: param, ...)` in `std.flow`** — required `.flow`-side declaration for every new builtin. See `std.flow:38–49` for the existing arithmetic block. Phase 26 appends Long/Number variants + neg + idiv to this block.
- **Per-type overload registration via separate `FunctionSignature` instances** — the codebase convention. See lines 213–271 of `BuiltInFunctions.cs`: each numeric type gets its own `add{Type}Signature` and its own `Register("add", sig, lambda)` call. Phase 26 follows this for Long and Number additions.
- **Static `StdLib.{Operation}{Type}` helper methods** (`StdLib.AddInt`, etc.) — receive `IReadOnlyList<Value>`, extract operands, perform arithmetic on raw CLR primitives, return a new `Value`. The Phase 26 fast paths follow this exactly: e.g., `public static Value AddLong(IReadOnlyList<Value> args) => Value.Long((long)args[0].Data + (long)args[1].Data);`.
- **Number-type arithmetic via `BigInteger`** — the existing `EvaluateBinary` Number branch at lines 266–278 of `ExpressionEvaluator.cs` uses `BigInteger`. Phase 26's `StdLib.AddNumber` lifts that exact arithmetic into a static helper (extracting the BigInteger from each operand's Data).
- **Wave-based commit cadence** is NOT used here per D-13 — Phase 26 chooses a 3-commit single-mega-migration shape instead, because there's no intermediate state where the build can be green AND the tests can pass between commits 1 and 2.

### Integration Points
- **`Parser.ParseFlowExpression` at line 668** — the single edit point that severs the `ParseAdditive` chain. After Phase 26: `var left = ParsePostfix()`. Inside the `while (Match(TokenType.Arrow))` loop body, line 689 `args.Add(ParseAdditive())` becomes `args.Add(ParsePostfix())`.
- **`ExpressionEvaluator.Evaluate` switch at line 39** — remove the `BinaryExpression bin => EvaluateBinary(bin)` case. After deletion, `BinaryExpression.cs` can be deleted because no callers remain.
- **`SimpleLexer.cs:108–109` (case `+`/`-`)** — extend with the prev-token-gate for negative-literal emission. The fall-through to `SingleChar(TokenType.Plus/Minus)` remains as the default for note-stream typed literals + parser-shorthand `-IDENT`.
- **`flow-lang/std.flow:38–49`** — append new declarations to the arithmetic block. Without a `.flow`-side declaration, registry registration is invisible to user scripts (lesson from Phase 25 D-25).
- **`scripts/migrate-26.cs`** — links to `flow-lang/Lexing/SimpleLexer.cs` (re-uses the lexer; does NOT re-implement). This means the migration script must run AFTER commit 1 (parser change) but BEFORE the migrated files exist, which is fine — the lexer still recognizes `Plus/Minus/Star/Slash` tokens; it's the PARSER that no longer accepts them.
- **No `flow-lsp` touch** — semantic tokenization for `+/-/*//` was a passive function of the lexer; once the parser ignores those tokens, the LSP analysis updates downstream automatically. Confirm during execution by opening a `.flow` file in VSCode after Phase 26 ships and verifying no stale syntax-highlighting for legacy infix.

</code_context>

<specifics>
## Specific Ideas

- **Two-tier overload performance metaphor** — user articulated as "easy cases fast, flexible cases flexible". The fast path is direct C# arithmetic on raw primitives (no `Convert.*`, no boxing, no method indirection through Value coercion). The flex path goes through the existing OverloadResolver + Value widening — slower, but correct. This is now codified as the performance corollary in `feedback_ergonomics_priority.md`.
- **`(idiv)` as the ergonomic escape valve for integer division** — the user's reaction to "Int/Int truncates, Int/Double widens" was to add a separate builtin so the common case (`(div 1 2)`) returns the expected `0.5` and the niche case (`(idiv 1 2)`) returns `0` explicitly. Foot-gun resolved. Asymmetric only on `(div Int Int)` — `(div Long Long)` still returns Long, `(div Float Float)` still returns Float, etc.
- **`-x` parser shorthand example** — example of D-01 in action. After Phase 26: `Int x = 5;\nInt y = -x;` parses as `Int y = (neg x);` — `BinaryExpression` is never produced. Implementation: in `Parser.ParseUnary` (or its successor `ParsePostfix`), check `Match(TokenType.Minus)` at expression-start positions ONLY; if next token is an identifier, emit `FunctionCallExpression("neg", [VariableExpression])`; if next token is a number literal, the lexer should have already produced a single negative-literal token so the parser never sees `Minus` here.
- **Migration script idempotence test** — run `scripts/migrate-26.cs` twice on the same file; the second run produces zero diff. This is the smoke test for the script before sweeping all 82 files. Bug in idempotence usually means the script is re-emitting `(add (add a b) c)` instead of recognizing already-prefix calls.
- **CLAUDE.md prefix-only rule wording** — recommended phrasing (not binding, planner can adjust): "**Prefix-only arithmetic.** Flow has no infix `+`, `-`, `*`, `/`. Arithmetic is via the builtins `(add a b)`, `(sub a b)`, `(mul a b)`, `(div a b)`, `(neg x)`, and `(idiv a b)` for integer division. Numeric types widen Int → Long → Float → Double → Number; mixed-type calls widen the narrower operand. String concatenation uses `(concat a b)`. Comparison operators were already prefix (`(eq)`, `(lt)`, `(gt)`, etc.)."
- **Tutorial.flow lines 55–58 specific edit** — the existing tutorial documents BOTH styles. After Phase 26, lines 55–58 are replaced with a single line emphasizing the prefix-only rule. The educational note "Flow uses S-expression style" stays; the dual-style example is removed.

</specifics>

<deferred>
## Deferred Ideas

### Out of Phase 26 Scope
- **`(neg Sequence)` / `(neg Note)` musical-inversion overloads** — would conflate numeric negation with musical inversion (which already exists as `invert(seq)` in `TransformFunctions.cs`). Not requested; future phase if a use case emerges.
- **Charitable migration hint diagnostic** ("infix removed in v1.3 — use `(add a b)`") — explicitly NOT shipped per D-15/D-16 (Flow is pre-public). Revisit if/when Flow ships publicly. Tracked in `project_pre_public_no_legacy_burden.md`.
- **`docs/migration-26.md` for end users** — not needed for the same reason.
- **Permanent `flow-lang.Tools/Migrate26` dotnet tool** — not needed.
- **Mixed-type cross-overload matrix** (e.g., explicit `(add Int Long)`, `(add Float Double)` registrations) — punted in favor of OverloadResolver convertible scoring per D-05. If profiling later shows convertible-scoring overhead is noticeable on hot paths, revisit and add specific mixed overloads as targeted fast paths.
- **Removing the unused Pidgin parser-combinator dependency** — listed under "Future Requirements" in REQUIREMENTS.md. Opportunistic cleanup; not bundled in Phase 26.
- **Phase 26 byte-identical regression xUnit test** (`ByteIdenticalShowcasePrefixTests.cs`) — D-14 chose the SHA256 hash gate during migration commit instead. Phase 18 + 25 tests are the persistent guards.
- **`(mod a b)` modulo / `(rem a b)` remainder builtins** — Flow doesn't currently have modulo at all (no infix `%`, no builtin). Not in Phase 26 scope; if needed, a separate small phase adds them. Likely useful for euclidean rhythm internals but currently `BigInteger %` is implemented inline in `BuiltInFunctions.cs` for that purpose.
- **`(pow a b)` exponentiation builtin** — same status. Not in Phase 26 scope.
- **`flow-lsp` semantic-tokens explicit removal of `+/-/*//` operator class** — passive update via lexer; verify post-ship but no proactive change planned.
- **Updating the v1.3 milestone goal text in `PROJECT.md`** to mention Phase 26 standardization explicitly — minor documentation churn; planner may bundle into commit 3 or skip.

### Reviewed Todos (not folded)

None — `gsd-sdk query todo.match-phase 26` was not invoked during this discussion (no todo file pre-existed for this phase). Planner may run it during plan-phase as a standard cross-check.

</deferred>

---

*Phase: 26-op-standardization-prefix-only*
*Context gathered: 2026-05-04 via /gsd-discuss-phase 26 (default interactive mode)*
