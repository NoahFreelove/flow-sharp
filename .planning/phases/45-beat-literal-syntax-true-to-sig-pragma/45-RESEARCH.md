# Phase 45: Beat Literal Syntax & True-to-Sig Pragma - Research

**Researched:** 2026-05-25
**Domain:** Lexer surface + AST + ExecutionContext pragma plumbing + RegisterContextDependent migration
**Confidence:** HIGH (every reference verified against codebase at HEAD; all 17 D-NN decisions cited verbatim from CONTEXT.md)

## Summary

Phase 45 closes the literal-syntax half of the Beat-ergonomics gap that Phase 43 opened with `beatToSec` / `secToBeat` / `delay(Buffer, Beat)` / `renderBarAtBeat(Bar, Beat)`. Composers can now write `0.5b` instead of `(beat 0.5)` and opt into an `enable beat-true-to-sig;` file pragma that retunes the literal meaning to the active time signature's beat unit (in `timesig 6/8 {}` with pragma on, `1b = eighth`).

The implementation is small but cross-cutting: one new `TokenType.BeatLiteral` enum case + two lexer branches (signed in `TryLookAheadSpecialLiteral`, unsigned in `ScanNumberOrSpecialLiteral`) + one new `BeatLiteralExpression` AST record (breaking from the existing `LiteralExpression`-with-raw-text pattern because eval-time context lookup needs the raw double preserved) + one new switch arm in `ExpressionEvaluator` + one new `PragmaRegistry` entry + one new `ExecutionContext.BeatTrueToSig` boolean (mirrored from Phase 44's `StrictMode`) + ModuleLoader push/pop wiring + migration of the existing `(beat Double) → Beat` registration from plain `Register` to `RegisterContextDependent`. The multiplier formula is `pragma_on ? raw × (4.0 / denominator) : raw`, evaluated at literal/constructor invocation. **Internal storage stays quarter-relative** — every existing consumer (`SongRenderer:361`, `Timeline:49`, `PlaybackFunctions:382`, `MidiExport`) reads quarters unchanged.

**Primary recommendation:** Mirror Phase 44 D-02/D-03/D-04 pragma discipline exactly. The CONTEXT.md decision pack is already implementation-grade — research informs HOW, not WHETHER. 6-wave plan structure recommended (token → lexer/parser → AST/evaluator → pragma+context → constructor migration → tutorials/docs).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Area 1 — AST Representation**
- **D-01:** New `BeatLiteralExpression(double RawValue, SourceLocation Loc)` AST record in `flow-lang/Ast/Expressions/`, alongside `ChordLiteralExpression` / `NoteStreamExpression` / `SongExpression` / `SymbolLiteralExpression` / `TupleLiteralExpression`. Carries the raw source double exactly as written (`0.5` for `0.5b`). `ExpressionEvaluator` gets a new switch arm that computes the final quarter-relative double at eval time: `final = pragma_on ? raw × (4.0 / current_timesig_denom) : raw`. **Rejected:** tagging a `needsBeatPragmaMultiplier` flag on a generic `LiteralExpression` (bleeds music-specific context into the universal literal node; diverges from established own-AST-record pattern). **Rejected:** lex-time multiplier (pragma + timesig context aren't reachable at lex time).
- **D-02:** Eval-time TimeSig lookup reads `_context.ActiveMusicalContext.TimeSignature` (the current top-of-stack), defaulting to 4/4 when stack is empty. With pragma on + 4/4 default, the multiplier is `4/4 = 1.0` (identity) — pragma activation does not corrupt scripts that never set a timesig. With pragma off, the multiplier is `1.0` always (raw passes through).

**Area 2 — Pragma Scope**
- **D-03:** `enable beat-true-to-sig;` is the pragma name (hyphenated). Added to `flow-lang/Lexing/PragmaRegistry.cs:27` `KnownPragmas` dictionary with description: `"Opt-in: Nb literals and (beat N) constructor calls multiply by 4/denominator at eval time, reading active timesig. So in 'timesig 6/8 { }' with pragma on, 1b = 1 eighth. File-scoped, no propagation via use imports."`. Hyphenated form matches the kebab-case feel of `beat-true-to-sig` as it appears in ROADMAP — diverges from camelCase precedents (`justIntonation`/`matchExhaustive`) but pragma names are composer-facing English phrases and `beat-true-to-sig` reads cleanly in kebab.
- **D-04:** `ExecutionContext.BeatTrueToSig` boolean field, set by ModuleLoader when the file's pragma set includes `beat-true-to-sig`. Push/pop mirrors Phase 44 D-02's `StrictMode` discipline — value reflects the DECLARING file's pragma bit, not the caller's. Stdlib `.flow` files (`audio.flow` / `bars.flow` / `notation.flow` / etc.) do NOT enable the pragma, so any Beat literals they construct remain raw quarters.
- **D-05:** Both `Nb` literal and `(beat N)` constructor honor the pragma — no escape hatch. The existing registration at `BuiltInFunctions.cs:553` migrates to `RegisterContextDependent` (Phase 43 D-08 precedent — same mechanism as `beatToSec` / `secToBeat`) so the lambda has access to `ExecutionContext` and reads `ctx.BeatTrueToSig` + active `MusicalContext.TimeSignature` at call time. Composers wanting raw-quarter semantics under pragma split into a non-pragma helper file and import. Rationale: pre-traction no-deprecation latitude (`project_pre_public_no_legacy_burden`) means we ship the smallest surface; if a real composer reports needing a `(beatRaw N)` escape hatch, it ships in a one-commit follow-up.

**Area 3 — Lexer Surface**
- **D-06:** Signed `+Nb` / `-Nb` lex at expression-start via `flow-lang/Lexing/SimpleLexer.cs` `TryLexTypedLiteral` (line ~600 range). New branch follows the `+/-Nst` (608-621) / `+/-NdB` (637-650) pattern: append `b`, slice the numeric prefix, `double.TryParse`, emit `Token(TokenType.BeatLiteral, text, start, doubleVal, ...)`. Add new `TokenType.BeatLiteral` enum case alongside `SemitoneLiteral` / `DecibelLiteral` / `CentLiteral` / `HertzLiteral` / `TimeLiteral`.
- **D-07:** Unsigned `Nb` (including `0.5b`, `2b`, `1.0b`) lex via `ScanNumberOrSpecialLiteral` (line 688). Branch sits between the `c` suffix branch (line 766-776) and the `s` suffix branch (line 668-679) with guard `Peek() == 'b' && !char.IsLetter(PeekNext())` — matches the existing `c` suffix's `!char.IsLetter(PeekNext())` identifier-disambiguation pattern. This keeps `1bar` lexing as `1` + `bar` identifier; keeps `2beats` lexing as `2` + `beats` identifier; accepts `0.5b D4q` (Beat literal followed by anything non-letter).
- **D-08:** Runtime accepts negative Beat values as valid doubles — no rejection guard. `-2b` constructs `Value.Beat(-2.0)` regardless of mode. Musical semantics of negative beats (anticipation? rest offsetting?) are the composer's call; the language doesn't impose a musical interpretation. Mirrors how `-12dB` and `-50c` are accepted as valid doubles even though some sign conventions are ambiguous.

**Area 1 follow-on — Parser + Evaluator**
- **D-09:** Parser handles `BeatLiteral` token in `Parser.ParsePrimary` (or wherever `DecibelLiteral` / `CentLiteral` / `HertzLiteral` are currently handled). Emits a `BeatLiteralExpression(token.NumericValue, token.Loc)` instead of a flat `LiteralExpression(Value.Beat(...), BeatType.Instance)` — because eval-time context lookup needs the raw source value preserved through to eval.
- **D-10:** `ExpressionEvaluator` adds a switch arm for `BeatLiteralExpression`: `BeatLiteralExpression beatLit => EvaluateBeatLiteral(beatLit),` where `EvaluateBeatLiteral` reads `_context.BeatTrueToSig` + active `MusicalContext.TimeSignature`, computes the multiplier, and returns `Value.Beat(beatLit.RawValue * multiplier)`. Implementation lives in `ExpressionEvaluator.cs` directly (no new helper class) — the multiplier formula is two lines.

**Area 5 — Test Infrastructure**
- **D-11:** Two-track testing mirroring Phase 44 D-14 + Phase 43 REQ-MOD-12 precedent:
  - **Positive `.flow` tests:** `tests/test_beat_literal.flow` (lexer + parser smoke), `tests/test_beat_pragma_off.flow` (default `1b = quarter` across 4/4 / 6/8 / 2/2), `tests/test_beat_pragma_on.flow` (`enable beat-true-to-sig;` + multiplier behavior across 4/4 / 6/8 / 2/2 / 5/4 / 7/8), `tests/test_beat_cross_file.flow` (pragma-on file imports pragma-off file; verify Beat values flow as quarters).
  - **xUnit Facts** under `flow-lang.Tests/Phase45/` — see Validation Architecture below for the full Theory grid.
  - **Two-run cmp-clean** preserved — Phase 45 adds no PRNG sites; tutorial WAV/MIDI outputs deterministic.

**Area 4 — Tutorial + Documentation**
- **D-12:** Two tutorial files under `examples/beat/`:
  - `examples/beat/intro.flow` — 6/8 jig demonstrating with/without pragma. Renders MIDI + WAV. ~50-80 lines.
  - `examples/beat/cut-time.flow` — `timesig 2/2` showing `1b = half`. Renders MIDI + WAV.
- **D-13:** CLAUDE.md "Music Types Quick Reference" table gets a new row:
  ```
  | `0.5b` (Beat literal) | `Beat` | `Double`, `Float` | beat-position arithmetic; `enable beat-true-to-sig;` opt-in retunes literal to active timesig's beat unit (default 4/4 → `1b = quarter`) |
  ```
  Adjacent CLAUDE.md "Music-Specific" section gets a one-line addition mentioning the pragma family expansion.

**Surface Decisions Locked (no question, ROADMAP-derived)**
- **D-14:** `(str someBeat)` behavior UNCHANGED — emits plain double like `"0.5"`. Reason: emitting `"0.5b"` would break round-trip under `beat-true-to-sig` pragma.
- **D-15:** REPL `:beat-true-to-sig` toggle NOT added in Phase 45. Pragma is file-scope; REPL is ephemeral.
- **D-16:** Strict mode (Phase 44) interaction — `Nb` becomes the canonical way to write Beat values in `enable strict;` files. Phase 44 Axis A (no type coercion) disables the `Double → Beat` convertible-tier match, so composers MUST write `0.5b` under strict. Documented but NOT a Phase 45 implementation task.
- **D-17:** Dotted-rhythm `Nb.` syntax (e.g., `0.5b.` for dotted-beat = 0.75 quarters) NOT added. Composers can write `0.75b` directly. Note streams keep their own `q.`/`h.`/`w.` dotted-suffix language as that's a separate surface.

### Claude's Discretion

- Exact placement of the `b` suffix branch in `TryLexTypedLiteral` ordering — between `+/-Nst` and `+/-NdB`, or elsewhere. Plan-phase decides based on suffix-conflict analysis (single-char `b` with non-letter guard is conflict-free among current suffixes; ordering is cosmetic).
- Whether to add a `BeatLiteralFacts.cs` regression file pinning the existing `(beat N)` constructor's Phase 26.1 DICT-01 acceptance (`<<C4, (beat 0.25)>>` Dict-key shape) to confirm `RegisterContextDependent` migration doesn't regress dict-key tuple constructions. Recommended (cheap insurance).
- Order of execution (lexer vs parser vs evaluator vs pragma registry vs constructor migration). Plan-phase decides wave breakdown. Suggested ordering (from CONTEXT.md): (1) `TokenType.BeatLiteral` enum + tests; (2) lexer suffix branches + Parser AST emit + parser tests; (3) `ExecutionContext.BeatTrueToSig` field + `PragmaRegistry` entry + `ModuleLoader` push/pop + pragma tests; (4) `EvaluateBeatLiteral` switch arm + multiplier tests; (5) `(beat N)` constructor migration to `RegisterContextDependent`; (6) tutorial files + CLAUDE.md update.
- Whether to vendor `flow-lang.Tests/baselines/Phase45/` audio baselines for the two tutorial WAVs. Match Phase 28 baseline precedent if any rendered audio is involved (probably yes).

### Deferred Ideas (OUT OF SCOPE)

- **`(beatRaw N)` escape hatch** — explicit raw-quarter constructor for composers in `enable beat-true-to-sig;` files who want a per-call bypass. Deferred per D-05 pre-traction latitude.
- **`(str someBeat)` emitting `"0.5b"` suffix form** — would enable round-trip in pragma-off files but break it in pragma-on. Deferred per D-14.
- **REPL `:beat-true-to-sig on/off` sticky meta-command** — Phase 38 + Phase 44 D-16's `:strict` pattern could mirror here. Deferred per D-15.
- **Dotted-rhythm `Nb.` syntax** (e.g., `0.5b.` = 0.75 quarters in pragma-off, or 0.75 × `4/denom` quarters in pragma-on) — composers can write `0.75b` directly. Deferred per D-17.
- **Tied-Beat-literal syntax `Nb~`** — note streams already have `C4h~` for tied notes. Composer can write `(add 0.5b 0.5b)` or use sequence concatenation. Deferred indefinitely.
</user_constraints>

<phase_requirements>
## Phase Requirements

Phase 45 REQ-BEAT-NN IDs are TBD per CONTEXT.md (`ROADMAP.md` line "Requirements: TBD (defined at plan-phase)"). Recommended grouping below maps each D-NN to a requirement ID for the planner to drop into `.planning/REQUIREMENTS.md` and reference in plans.

| Proposed ID | Description | Source Decision | Research Support |
|----|-------------|------|----|
| REQ-BEAT-LEX-01 | `TokenType.BeatLiteral` enum case added alongside `SemitoneLiteral` / `DecibelLiteral` / `CentLiteral` / `TimeLiteral` / `HertzLiteral` | D-06 | `flow-lang/Lexing/TokenType.cs:61-65` (current music-literal enum cluster); planner adds line 66. |
| REQ-BEAT-LEX-02 | Signed `+Nb` / `-Nb` lexes at expression-start via `TryLookAheadSpecialLiteral` (the canonical function name; CONTEXT.md uses conceptual name `TryLexTypedLiteral`) | D-06 | `SimpleLexer.cs:538-686`; insertion point between existing `st` branch (608-621) and `c` branch (623-635), or anywhere in the sequential-`if`-with-rewind chain — order is cosmetic since each branch is first-char dispatch. Suffix-conflict-free (no existing branch starts with `b`). |
| REQ-BEAT-LEX-03 | Unsigned `Nb` lexes via `ScanNumberOrSpecialLiteral` with `Peek() == 'b' && !char.IsLetter(PeekNext())` guard | D-07 | `SimpleLexer.cs:688-789`; CONTEXT.md prescribes "between `c` (766-776) and `s` (778-788)". The chain is `else if`, so order DOES matter for shadowing — `c` and `s` branches both single-char-letter dispatch; inserting `b` as a new `else if` between them is mechanical. Identifier-collision examples enumerated below in Pitfall 2. |
| REQ-BEAT-LEX-04 | Negative Beat values (e.g., `-2b`) accepted without rejection | D-08 | Symmetric with `-50c` / `-12dB` precedent; runtime accepts any double. |
| REQ-BEAT-AST-01 | New `BeatLiteralExpression(SourceLocation Location, double RawValue, Span? Span = null)` record under `flow-lang/Ast/Expressions/` | D-01 | Closest precedent: `SymbolLiteralExpression.cs` (single-property `Name` + Loc + optional Span). Same shape; substitute `double RawValue` for `string Name`. |
| REQ-BEAT-AST-02 | Parser emits `BeatLiteralExpression` for `BeatLiteral` tokens in `ParsePrimary` (not flat `LiteralExpression`) | D-09 | `Parser.cs:1346-1367` shows existing music-literal arms route through `LiteralExpression(PreviousToken.Location, PreviousToken.Text, ...)`. Phase 45 breaks from this pattern only for Beat — reads `PreviousToken.Value as double` directly. Token.Value carries the parsed double per `Token.cs:30`. |
| REQ-BEAT-AST-03 | Parser also accepts `BeatLiteral` token in pattern-position match arms (Phase 35) and literal-token-set check at line 2103 | D-09 implicit | `Parser.cs:2103-2109` enumerates literal token types for type-routing context; `BeatLiteral` must be added to this `or`-chain (5 tokens listed today). |
| REQ-BEAT-AST-04 | `ExpressionEvaluator.Evaluate` switch dispatches `BeatLiteralExpression beatLit => EvaluateBeatLiteral(beatLit)` | D-10 | `ExpressionEvaluator.cs:37-58`. Insert alongside `SymbolLiteralExpression` (line 46). |
| REQ-BEAT-PRAGMA-01 | `["beat-true-to-sig"]` entry added to `PragmaRegistry.KnownPragmas` with description from D-03 verbatim | D-03 | `PragmaRegistry.cs:27-37`. Single-line addition between existing entries; ordinal sort produces alphabetized output via `AlphabetizedKnownNames()`. |
| REQ-BEAT-PRAGMA-02 | `ExecutionContext.BeatTrueToSig` boolean field added | D-04 | `ExecutionContext.cs:468` (current `StrictMode` field) is the closest precedent. New field declared near line 468; default `false`. NO `CallerBeatTrueToSig` snapshot needed — see Pitfall 3 (single-field design is sound for Phase 45). |
| REQ-BEAT-PRAGMA-03 | `FlowEngine.ApplyBeatTrueToSigPragma(program)` private method mirrors `ApplyStrictPragma(program)` | D-04 | `FlowEngine.cs:352-355`. New method one-liner: `_context.BeatTrueToSig = program.Pragmas.Has("beat-true-to-sig");`. Called from `Execute` (line ~296) alongside `ApplyStrictPragma`. Overwrites on every `Execute` (no persistence branch — same rationale as strict per `FlowEngine.cs:344-350`). |
| REQ-BEAT-PRAGMA-04 | `ModuleLoader.LoadModule` saves/restores `BeatTrueToSig` around imported file's `interpreter.Execute(program)` call | D-04 | `ModuleLoader.cs:117-204`. Mirrors the existing strict-mode save-set-restore (lines 125-126 set, line 203 restore in `finally`). Insert parallel save-set-restore for `BeatTrueToSig`. |
| REQ-BEAT-CONSTRUCTOR-01 | `(beat Double) → Beat` registration migrates from plain `Register` to `RegisterContextDependent` in a new `BeatConstructorFunctions` class (or extends `BeatConversionFunctions.cs`) | D-05 | Phase 43's `BeatConversionFunctions.cs:45-96` is the canonical recipe. Migration: lambda receives `args`, reads `context.BeatTrueToSig` + `context.GetMusicalContext().TimeSignature?.Denominator ?? 4`, computes multiplier, returns `Value.Beat(args[0].As<double>() * multiplier)`. Delete the existing `BuiltInFunctions.cs:547-555` block. Wire into `RegisterContextDependentFunctions` (line 1016) alongside `BeatConversionFunctions` (line 1023). |
| REQ-BEAT-CONSTRUCTOR-02 | Phase 26.1 DICT-01 acceptance (`<<C4, (beat 0.25)>>` Tuple-of-hashables Dict key) regression-pinned | D-05 + Claude's Discretion | xUnit Fact in `BeatTrueToSigPragmaTests.cs` or new `BeatLiteralFacts.cs`. Constructs the Dict tuple in pragma-off and pragma-on contexts; pins value equality. |
| REQ-BEAT-TEST-01 | Positive `.flow` smoke tests cover `0.5b` / `2b` / `1.0b` / `+1b` / `-2b` lexing | D-11 | `tests/test_beat_literal.flow`. Runs via `dotnet run --project flow-interpreter tests/test_beat_literal.flow`. |
| REQ-BEAT-TEST-02 | Positive `.flow` pragma-off integration: `1b` evaluates to `Value.Beat(1.0)` across `4/4` / `6/8` / `2/2` contexts | D-11 | `tests/test_beat_pragma_off.flow`. Verifies identity behavior. |
| REQ-BEAT-TEST-03 | Positive `.flow` pragma-on integration: multiplier formula `raw × 4/denom` across `4/4` (×1.0), `6/8` (×0.5), `2/2` (×2.0), `5/4` (×1.0), `7/8` (×0.5) | D-11 | `tests/test_beat_pragma_on.flow`. |
| REQ-BEAT-TEST-04 | Cross-file boundary: pragma-on file imports pragma-off file; Beat values flow as quarters | D-04 + D-11 | `tests/test_beat_cross_file.flow` + helper file. Verifies file-scope semantic from D-04. |
| REQ-BEAT-TEST-05 | xUnit `BeatLiteralParserTests.cs` pins lexer accept/reject + AST shape (`BeatLiteralExpression` not `LiteralExpression`) | D-11 | `flow-lang.Tests/Integration/Phase45/`. See Validation Architecture for the full Theory grid. |
| REQ-BEAT-TEST-06 | xUnit `BeatTrueToSigPragmaTests.cs` pins `PragmaRegistry` registration + `ExecutionContext.BeatTrueToSig` push/pop + multiplier formula matrix + `(beat N)` pragma-awareness + identity-mode behavior | D-11 | Same directory. Theory grid below. |
| REQ-BEAT-TEST-07 | Two-run cmp-clean determinism preserved (any rendered audio from tutorials produces identical SHA on second run) | D-11 | No PRNG sites added by Phase 45. Tutorial WAVs are pure synthesis — Phase 28 baseline precedent applies (commit reference renders if any audio is rendered). |
| REQ-BEAT-DOC-01 | CLAUDE.md "Music Types Quick Reference" table grows by one row | D-13 | Verbatim row from D-13 text above. |
| REQ-BEAT-DOC-02 | CLAUDE.md "Music-Specific" section gets a one-line pragma-family expansion mention | D-13 | One sentence — pragma list grows from 6 (hAsB / justIntonation / pythagorean / equalTemperament / scaleLint / matchExhaustive / strict — actually 7) to 8. |
| REQ-BEAT-DOC-03 | `examples/beat/intro.flow` 6/8 jig tutorial (50-80 lines) demonstrates with/without pragma. Renders MIDI + WAV. | D-12 | New directory `examples/beat/` matches `examples/dsp/` / `examples/scala/` / `examples/sections/` / `examples/generative/` precedent. |
| REQ-BEAT-DOC-04 | `examples/beat/cut-time.flow` `timesig 2/2` tutorial showing `1b = half` | D-12 | Same directory. |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Ergonomics first** — composer ergonomics override runtime efficiency, type strictness, and generality. D-01 (own AST record over flag-on-`LiteralExpression`) and D-05 (single pragma-aware constructor over two parallel forms) honor this.
- **Genre-agnostic** — D-12 jig (Celtic/Irish) + D-12 cut-time (orchestral/march) cover two different genre families.
- **Pre-traction no-deprecation discipline** — breaking syntax ships in one commit; no `flow migrate` subcommand. D-05/D-14/D-15/D-17 explicitly invoke this latitude.
- **Two-run cmp-clean determinism is MANDATORY** — Phase 45 adds NO PRNG sites; tutorial WAVs are pure synthesis. Existing PRNG-routed sites (`granular`, `markov`, `lsystem`, `jam`) are not touched.
- **`->` is a parse-time transform** — Phase 45 does not change parse-time mechanics; `0.5b -> (delay buf)` desugars to `(delay buf 0.5b)` at parse time as today.
- **Overload resolution** — `OverloadResolver` scores: exact +1000, compatible +500, convertible +100. `Beat` is `IsCompatibleWith(Double, Float)` per `BeatType.cs:25-28` — unchanged in Phase 45. Under Phase 44 strict, the convertible (+100) tier is disabled, which is why `Nb` becomes the canonical way to write Beat values under strict (D-16).
- **Music literals at expression-start lex as single tokens (ERG-05)** — `BeatLiteral` qualifies. Expression-start positions are `(`, `=`, `,`, `[`, `{`, `~>` per existing precedent.
- **Quarter-relative internal storage** — every existing `Beat` consumer (`SongRenderer.cs:361`, `Timeline.cs:49`, `PlaybackFunctions.cs:382`, `MidiExport`, `Voice.OffsetBeats`) assumes wrapped double is quarter-relative. Phase 45's pragma resolves at CONSTRUCTION; downstream consumers see no shape change.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Lex `+/-Nb` signed suffix at expression-start | Lexer (`SimpleLexer.TryLookAheadSpecialLiteral`) | — | Phase 26 D-04 ERG-05 pins music-literal tokens at expression-start; signed suffix follows the established `+/-Nst` / `+/-NdB` precedent. |
| Lex `Nb` unsigned suffix after digits | Lexer (`SimpleLexer.ScanNumberOrSpecialLiteral`) | — | Mirrors existing single-char `c` / `s` suffix branches with identifier-collision guard. |
| Carry raw double through Token | Lexer Token (`Token.Value` payload) | — | Existing pattern: every music-literal token carries the parsed double in `Value`. |
| Emit AST node | Parser (`ParsePrimary`) | — | Standard music-literal pattern. Diverges from sibling literals: emits `BeatLiteralExpression` (own record) not `LiteralExpression` (raw text). |
| Compute multiplier at eval time | Evaluator (`ExpressionEvaluator.EvaluateBeatLiteral`) | Runtime (`ExecutionContext.GetMusicalContext`) | Lookups happen at eval time because pragma + timesig context aren't reachable at lex/parse time. |
| Pragma recognition + file-scope push/pop | Runtime (`ModuleLoader.LoadModule`) | Engine (`FlowEngine.ApplyBeatTrueToSigPragma`) | Phase 21 + Phase 44 D-02/D-03 precedent: top-level Execute applies for the entry file; ModuleLoader applies for each imported file. |
| Pragma carrier field | Runtime (`ExecutionContext.BeatTrueToSig`) | — | File-scope, not block-scope; lives on context (not stack frame) per Phase 44 D-02 rationale (dynamic-scope semantics for imports). |
| `(beat N)` constructor pragma-awareness | Stdlib (`BeatConstructorFunctions.RegisterContextDependent`) | Runtime (ExecutionContext closure capture) | Phase 22 DX-12 + Phase 43 D-08 `RegisterContextDependent` recipe — lambda captures `context`, reads pragma + timesig per call. |
| Quarter-relative downstream consumption | Stdlib (`SongRenderer:361`, `Timeline:49`, `PlaybackFunctions:382`, `MidiExport`) | — | UNCHANGED. The multiplier resolves at construction; downstream sees a quarter-relative double regardless of pragma state. |

## Standard Stack

### Core

Phase 45 introduces NO new external dependencies. All new code uses existing first-party infrastructure.

| Library / Component | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `flow-lang.Lexing.SimpleLexer` | HEAD | Existing manual tokenizer with music-literal detection | Established home for all music-literal suffix branches |
| `flow-lang.Lexing.PragmaRegistry` | HEAD | Closed-set pragma registry | Phase 21 precedent — every file-scope pragma registers here |
| `flow-lang.Runtime.ExecutionContext` | HEAD | Runtime context owning `MusicalContext` stack + pragma bits | Phase 44 `StrictMode` precedent |
| `flow-lang.StandardLibrary.Audio.BeatConversionFunctions` | HEAD | Phase 43 `RegisterContextDependent` recipe | Identical pattern needed for the `(beat N)` constructor migration |
| `Melanchall.DryWetMidi` | 8.0.3 | MIDI SMF write | Only external dep in the project; tutorial files use `writeMidi`. Unchanged. |

### Supporting (NEW code Phase 45 adds)

| Component | Purpose | When to Use |
|---------|---------|-------------|
| `flow-lang/Ast/Expressions/BeatLiteralExpression.cs` | NEW — AST node for `Nb` literals carrying raw double | Created in Wave 2 |
| `flow-lang/StandardLibrary/Audio/BeatConstructorFunctions.cs` (OR extend `BeatConversionFunctions.cs`) | NEW — `RegisterContextDependent` registration for pragma-aware `(beat N)` | Created in Wave 5 |
| `flow-lang.Tests/Integration/Phase45/BeatLiteralParserTests.cs` | NEW — xUnit Facts pinning lexer + parser | Created in Wave 2 |
| `flow-lang.Tests/Integration/Phase45/BeatTrueToSigPragmaTests.cs` | NEW — xUnit Facts pinning pragma + evaluator + constructor | Created in Wave 4 |
| `tests/test_beat_*.flow` (4 files) | NEW — positive `.flow` smoke tests | Created across Waves 2-5 |
| `examples/beat/intro.flow` + `examples/beat/cut-time.flow` | NEW — composer-facing tutorials | Created in Wave 6 |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `BeatLiteralExpression` (own AST record) | Flag-on-`LiteralExpression` (e.g., `bool NeedsBeatPragmaMultiplier`) | **Rejected per D-01.** Bleeds music-specific context into the universal literal node; diverges from established own-record pattern (ChordLiteralExpression / NoteStreamExpression / SongExpression / SymbolLiteralExpression / TupleLiteralExpression). |
| Eval-time multiplier (chosen) | Lex-time multiplier | **Rejected per D-01.** Pragma + timesig context aren't reachable at lex time. |
| `RegisterContextDependent` for `(beat N)` constructor (chosen) | Two parallel constructors: `(beat N)` (raw) + `(beatScaled N)` (pragma-aware) | **Rejected per D-05.** Pre-traction surface minimization wins; if composer pressure surfaces a per-call bypass need, ship `(beatRaw N)` in a one-commit follow-up. |
| `ctx.BeatTrueToSig` single field (chosen) | Two fields (`BeatTrueToSig` + `CallerBeatTrueToSig`) mirroring Phase 44's strict design | **Sound to choose single field per D-04.** Phase 44's two-field design exists to resolve the "stdlib stays charitable when called from non-strict, errors when called from strict" tension. Phase 45 has no equivalent — pragma affects construction-time multiplier ONLY in the DECLARING file's code. Stdlib `(beat N)` calls (from non-pragma files) compute multiplier from the executing file's pragma bit, which IS `ctx.BeatTrueToSig`. See Pitfall 3. |

**Installation:** No new packages.

**Version verification:** N/A — no new packages.

## Package Legitimacy Audit

> **Not applicable.** Phase 45 installs no external packages. The closest analog — Phase 43's Beat builtins — also added no packages. All new code uses first-party infrastructure (`flow-lang.Lexing`, `flow-lang.Runtime`, `flow-lang.StandardLibrary`, `flow-lang.Ast`).

| Package | Registry | Disposition |
|---------|----------|-------------|
| (none) | — | N/A |

## Architecture Patterns

### System Architecture Diagram

```
.flow source file
    │
    ▼
[PragmaScanner]  ── extracts `enable beat-true-to-sig;` into PragmaSet
    │
    ▼
[SimpleLexer]    ── recognizes Nb suffix via two paths:
    │             (a) signed `+/-Nb` at expression-start → TryLookAheadSpecialLiteral
    │             (b) unsigned `Nb` after digits → ScanNumberOrSpecialLiteral
    │             both emit Token(BeatLiteral, text, loc, doubleValue, span)
    │
    ▼
[Parser]         ── ParsePrimary matches BeatLiteral token
    │             emits BeatLiteralExpression(loc, rawValue, span)   ◄── NEW AST node
    │             (diverges from sibling music literals which route LiteralExpression)
    │
    ▼
[ExpressionEvaluator]  ── switch dispatch
    │             BeatLiteralExpression beatLit => EvaluateBeatLiteral(beatLit)   ◄── NEW arm
    │
    │             EvaluateBeatLiteral:
    │               1. denom = ctx.GetMusicalContext().TimeSignature?.Denominator ?? 4
    │               2. multiplier = ctx.BeatTrueToSig ? (4.0 / denom) : 1.0
    │               3. return Value.Beat(beatLit.RawValue * multiplier)
    │
    ▼
[Value.Beat(quarters)]   ── quarter-relative double, INTERNAL STORAGE UNCHANGED
    │
    ▼
[Downstream consumers — UNCHANGED]
    │  ├── SongRenderer.cs:361   secondsPerBeat = 60.0/bpm
    │  ├── Timeline.cs:49        beat ↔ seconds arithmetic
    │  ├── PlaybackFunctions.cs:382  voice offset math
    │  ├── MidiExport            microsPerBeat SMF tempo events
    │  └── Voice.OffsetBeats     scheduling
    │
    ▼
[WAV output] / [MIDI output] / [real-time playback]

──────────────────────────────────────────────────────────────────────────

(beat N) constructor call path (Wave 5 migration):

    (beat 0.5)
        │
        ▼
    [InternalFunctionRegistry]  resolves to RegisterContextDependent lambda  ◄── MIGRATED
        │
        ▼
    Lambda body (same multiplier formula as EvaluateBeatLiteral):
        1. denom = context.GetMusicalContext().TimeSignature?.Denominator ?? 4
        2. multiplier = context.BeatTrueToSig ? (4.0 / denom) : 1.0
        3. return Value.Beat(args[0].As<double>() * multiplier)

──────────────────────────────────────────────────────────────────────────

Pragma push/pop discipline (mirrors Phase 44 StrictMode):

    FlowEngine.Execute(source)
        │
        ├── ApplyTuningPragma(program)
        ├── ApplyStrictPragma(program)              ── _context.StrictMode = pragmas.Has("strict")
        └── ApplyBeatTrueToSigPragma(program)       ── _context.BeatTrueToSig = pragmas.Has("beat-true-to-sig")  ◄── NEW
            │
            ▼
        interpreter.Execute(program)

    ModuleLoader.LoadModule(path)
        │
        ├── prevStrict = context.StrictMode
        ├── context.StrictMode = pragmaSet.Has("strict")
        ├── prevBeat = context.BeatTrueToSig                              ◄── NEW
        ├── context.BeatTrueToSig = pragmaSet.Has("beat-true-to-sig")     ◄── NEW
        │   try {
        │       interpreter.Execute(program)
        │   } finally {
        │       context.StrictMode = prevStrict
        │       context.BeatTrueToSig = prevBeat                          ◄── NEW
        │   }
```

### Recommended Project Structure

```
flow-lang/
├── Ast/Expressions/
│   └── BeatLiteralExpression.cs              ◄── NEW (REQ-BEAT-AST-01)
├── Lexing/
│   ├── SimpleLexer.cs                        ◄── EDIT (REQ-BEAT-LEX-02, REQ-BEAT-LEX-03)
│   ├── TokenType.cs                          ◄── EDIT (REQ-BEAT-LEX-01)
│   └── PragmaRegistry.cs                     ◄── EDIT (REQ-BEAT-PRAGMA-01)
├── Parsing/
│   └── Parser.cs                             ◄── EDIT (REQ-BEAT-AST-02, REQ-BEAT-AST-03)
├── Interpreter/
│   └── ExpressionEvaluator.cs                ◄── EDIT (REQ-BEAT-AST-04)
├── Runtime/
│   ├── ExecutionContext.cs                   ◄── EDIT (REQ-BEAT-PRAGMA-02)
│   └── ModuleLoader.cs                       ◄── EDIT (REQ-BEAT-PRAGMA-04)
├── Core/
│   └── FlowEngine.cs                         ◄── EDIT (REQ-BEAT-PRAGMA-03)
└── StandardLibrary/Audio/
    ├── BeatConstructorFunctions.cs           ◄── NEW (REQ-BEAT-CONSTRUCTOR-01)
    │                                           OR extend BeatConversionFunctions.cs
    └── BuiltInFunctions.cs                   ◄── EDIT — DELETE lines 547-555 + wire new registration

flow-lang.Tests/Integration/Phase45/          ◄── NEW DIR
├── BeatLiteralParserTests.cs                 ◄── NEW (REQ-BEAT-TEST-05)
└── BeatTrueToSigPragmaTests.cs               ◄── NEW (REQ-BEAT-TEST-06)

tests/                                         ◄── EDIT — add 4 new .flow files
├── test_beat_literal.flow                    ◄── NEW (REQ-BEAT-TEST-01)
├── test_beat_pragma_off.flow                 ◄── NEW (REQ-BEAT-TEST-02)
├── test_beat_pragma_on.flow                  ◄── NEW (REQ-BEAT-TEST-03)
└── test_beat_cross_file.flow                 ◄── NEW (REQ-BEAT-TEST-04)

examples/beat/                                 ◄── NEW DIR (REQ-BEAT-DOC-03 / REQ-BEAT-DOC-04)
├── intro.flow                                ◄── NEW
└── cut-time.flow                             ◄── NEW

CLAUDE.md                                      ◄── EDIT (REQ-BEAT-DOC-01, REQ-BEAT-DOC-02)
```

### Pattern 1: Music-Literal Lexer Branch

**What:** Each music-literal suffix sits in `SimpleLexer` as a self-contained branch in one of two functions: `TryLookAheadSpecialLiteral` (signed, expression-start) or `ScanNumberOrSpecialLiteral` (unsigned, after digits).

**When to use:** Any new single-char or multi-char suffix that produces a tagged literal.

**Example (existing `c` cent suffix, from SimpleLexer.cs:623-635 and 766-776):**

```csharp
// Signed branch (TryLookAheadSpecialLiteral, lines 623-635):
// Try "c" suffix (cent - microtone)
if (!IsAtEnd() && Peek() == 'c' && !char.IsLetter(PeekNext()))
{
    sb.Append(Advance());
    text = sb.ToString();
    string numberPart = text.Substring(0, text.Length - 1);
    if (double.TryParse(numberPart, out double centValue))
    {
        return new Token(TokenType.CentLiteral, text, start, centValue, Span: new Span(start, CurrentLocation()));
    }
}

// Unsigned branch (ScanNumberOrSpecialLiteral, lines 766-776):
else if (Peek() == 'c' && !char.IsLetter(PeekNext()))
{
    sb.Append(Advance());
    var text = sb.ToString();
    string numberPart = text.Substring(0, text.Length - 1);
    if (double.TryParse(numberPart, out double centValue))
    {
        return new Token(TokenType.CentLiteral, text, start, centValue, Span: new Span(start, CurrentLocation()));
    }
}
```

**Phase 45 BeatLiteral applies the exact same shape, substituting `'c'` → `'b'` and `CentLiteral` → `BeatLiteral`.**

### Pattern 2: Own-Record AST + Dedicated Evaluator Arm

**What:** Music-literal AST nodes that need eval-time context lookup get their own `record` type and dedicated evaluator method.

**When to use:** When the value cannot be resolved at parse time (e.g., needs runtime pragma state, needs musical context, needs Symbol intern table).

**Example (SymbolLiteralExpression, from `Ast/Expressions/SymbolLiteralExpression.cs`):**

```csharp
public record SymbolLiteralExpression(
    SourceLocation Location,
    string Name,
    Span? Span = null
) : Expression(Location);
```

**Phase 45 `BeatLiteralExpression` parallels this exactly:**

```csharp
public record BeatLiteralExpression(
    SourceLocation Location,
    double RawValue,
    Span? Span = null
) : Expression(Location);
```

### Pattern 3: `RegisterContextDependent` for Pragma-Aware Builtins

**What:** Builtins that need to read `ExecutionContext` state per-call register through `RegisterContextDependent` rather than plain `Register`. The lambda closure captures the `context` parameter and reads fresh state on every call.

**When to use:** Any builtin where the same input produces different output based on runtime context (active tempo, active timesig, active tuning, pragma bits, OSC enable, etc.).

**Example (Phase 43 BeatConversionFunctions.cs:50-75):**

```csharp
public static void RegisterContextDependent(
    InternalFunctionRegistry registry,
    FlowLang.Runtime.ExecutionContext context)
{
    var beatToSecSig = new FunctionSignature(
        "beatToSec",
        [BeatType.Instance],
        ParameterNames: ["beats"]);
    registry.Register("beatToSec", beatToSecSig, args =>
    {
        double beats = args[0].As<double>();
        double bpm = context.GetMusicalContext().Tempo ?? 120.0;
        // ... advisory walk omitted ...
        double seconds = beats * (60.0 / bpm);
        return Value.Second(seconds);
    });
}
```

**Phase 45 `BeatConstructorFunctions.RegisterContextDependent` applies the same recipe (D-05 migration):**

```csharp
public static void RegisterContextDependent(
    InternalFunctionRegistry registry,
    FlowLang.Runtime.ExecutionContext context)
{
    var beatSig = new FunctionSignature(
        "beat",
        [DoubleType.Instance],
        ParameterNames: ["value"]);
    registry.Register("beat", beatSig, args =>
    {
        double raw = args[0].As<double>();
        int denom = context.GetMusicalContext().TimeSignature?.Denominator ?? 4;
        double multiplier = context.BeatTrueToSig ? (4.0 / denom) : 1.0;
        return Value.Beat(raw * multiplier);
    });
}
```

### Pattern 4: File-Scope Pragma Push/Pop via ModuleLoader

**What:** File-scope pragmas (e.g., `enable strict;`, `enable justIntonation;`, `enable matchExhaustive;`) live in `ExecutionContext` fields. ModuleLoader saves the caller's bit, sets the imported file's bit, runs the import, then restores in a `finally` block.

**Phase 44 precedent (ModuleLoader.cs:117-204):**

```csharp
var prevStrict = context.StrictMode;
context.StrictMode = pragmaSet.Has("strict");
try
{
    interpreter.Execute(program);
    // ... ModuleRegistry registration ...
}
finally
{
    // Anti-Pattern 1 — never mutate StrictMode without a paired restore.
    context.StrictMode = prevStrict;
}
```

**Phase 45 adds a parallel save/set/restore for `BeatTrueToSig`** at the same insertion site (line 125-126 and 203).

### Pattern 5: Top-Level Execute Pragma Application

**What:** `FlowEngine.Execute(source)` applies file-scope pragmas BEFORE `interpreter.Execute(program)` runs. Each pragma has its own `Apply*Pragma(program)` method.

**Phase 44 precedent (FlowEngine.cs:289-296, 352-355):**

```csharp
// In Execute():
ApplyTuningPragma(program);
// Phase 44 Plan 44-01 D-02: file-scope strict-mode bit.
ApplyStrictPragma(program);
// ...
_interpreter.Execute(program);

// Helper method:
private void ApplyStrictPragma(Ast.Program program)
{
    _context.StrictMode = program.Pragmas.Has("strict");
}
```

**Phase 45 adds `ApplyBeatTrueToSigPragma(program)` parallel to `ApplyStrictPragma`** — single-line body, overwrites on every Execute (no persistence branch; same rationale per `FlowEngine.cs:344-350`).

### Anti-Patterns to Avoid

- **Routing `BeatLiteral` token through `LiteralExpression(text)` like sibling music literals.** This is the existing pattern for `CentLiteral` / `TimeLiteral` / etc — at eval time, `TryParseSpecialLiteral` re-parses the text suffix and builds the `Value`. For Beat we explicitly DON'T do this (D-01): the raw double must be preserved through to eval so the multiplier can apply with current pragma + timesig state. Use `BeatLiteralExpression(rawValue)` instead.
- **Reading `_context.ActiveMusicalContext.TimeSignature` directly.** The CONTEXT.md uses this name conceptually; the actual accessor is `_context.GetMusicalContext().TimeSignature` (returns the resolved-with-three-tier-fallback `MusicalContext` instance, then `.TimeSignature` is a `TimeSignatureData?`). Default 4/4 comes from `ParseTimesigOrDefault(FlowConfig.Active.DefaultTimesig)` at `ExecutionContext.cs:852`, so `denom` is reliably non-null. Defensive `?? 4` is harmless belt-and-suspenders.
- **Adding `CallerBeatTrueToSig` snapshot field mirroring Phase 44's two-field design.** Phase 44 needs two fields because stdlib leaf clamp sites must distinguish "the file I was DECLARED in was strict" from "the caller was strict". Phase 45 has no leaf-site asymmetry — the multiplier reads at the EXECUTING file's pragma bit (which IS `ctx.BeatTrueToSig`), and that's the semantically right answer for both `Nb` literals and `(beat N)` calls. Single field is sufficient per D-04.
- **Inserting the `b` branch in `ScanNumberOrSpecialLiteral` without preserving the `else if` chain.** The unsigned scanner uses `else if` (lines 715-789). Each branch is single-char-letter-first dispatch; the chain order is functionally significant (the FIRST matching `else if` wins). Inserting `b` must be a new `else if` block, NOT a separate `if` (which would change semantics for subsequent branches).
- **Forgetting `BeatLiteral` in the literal-token-set check at `Parser.cs:2103-2109`.** That `or`-chain is consumed by other parser paths (e.g., pattern-matching arms in Phase 35). New literal tokens must be added there too.
- **Treating `BeatLiteral` as valid inside note streams (`| C4q 0.5b D4q |`).** D-17 defers `Nb.` and tied-Beat surface — confirm `Nb` is only a TOP-LEVEL expression-form (after `(`, `=`, `,`, `[`, `{`, `~>` per ERG-05). Note streams keep their own `q.`/`h.`/`w.` dotted-suffix language and do NOT accept `Nb` tokens inside `| ... |`. Verify by reading `NoteStreamCompiler`'s accepted-token set.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Pragma carrier | Custom carrier type | `PragmaSet` + `KnownPragmas` registry | Phase 21 D-02 + Phase 44 already-solved this. Registering a new entry is one line. |
| File-scope state push/pop | Custom `IDisposable` scope guard | Save/set-in-`try`/restore-in-`finally` per ModuleLoader.cs:125-203 | Phase 44 already battle-tested this exact pattern; Anti-Pattern 1 documents it. |
| Time-signature lookup | Walking the stack frame chain manually | `context.GetMusicalContext().TimeSignature` | Phase 28+ helper resolves three-tier fallback (call-stack → FlowConfig → 120/(4,4)) in one call; result is memoized per `ExecutionContext.cs:33-41`. |
| Pragma-aware builtin registration | Custom registration entry point | `RegisterContextDependent` lambda | Phase 22 DX-12 + Phase 43 D-08 precedent. Closure-captures `context`; reads fresh per call. |
| Beat → quarters multiplier formula | Inline at every consumer site | Compute once at construction (literal or `(beat N)` call) | D-01/D-05/D-10 ALL converge here. Internal storage stays quarter-relative; no downstream consumer needs to know about the pragma. |
| AST node for literals needing eval-time context | Flag-on-`LiteralExpression` | Dedicated `record` type | D-01 + established pattern (ChordLiteralExpression, NoteStreamExpression, etc.). |

**Key insight:** Phase 45 is mostly a re-use of pragma+context-dependent infrastructure already battle-tested in Phases 21, 22, 28, 30, 32, 43, 44. The only genuinely new piece is the `BeatLiteralExpression` AST record — and even that copies `SymbolLiteralExpression`'s shape.

## Common Pitfalls

### Pitfall 1: `c` Suffix Identifier Collision Pattern

**What goes wrong:** Without the `!char.IsLetter(PeekNext())` guard, `0.5b` would greedy-match the prefix of `0.5bar`, and `1bar` would lex as `BeatLiteral` + `ar`.

**Why it happens:** Single-char music-literal suffixes share their first letter with many composer identifiers. The `c` suffix (cent) already faced this — anything starting with `c` (e.g., `chord`, `clamp`, `concat`) would shadow.

**How to avoid:** Use the exact guard pattern from `SimpleLexer.cs:624` and `:766` — `Peek() == 'b' && !char.IsLetter(PeekNext())`. When the lookahead is a letter, the scanner falls through to regular-number lex + the letter becomes the start of an identifier.

**Warning signs:** Identifier tokens that lex with leading-digit prefixes (e.g., `1bar` → `[IntLiteral(1), Identifier("bar")]`); composer-named procs starting with `b` getting consumed.

**Validation:** xUnit Theory in `BeatLiteralParserTests.cs` enumerates all `b`-starting identifier collisions: `bar`, `bars`, `beat`, `beats`, `bpm`, `buf`, `buffer`, `b1`, `Bb`, `Bmaj7` (chord literal — separate token), `B4` (note literal — separate token). Each must lex as `[IntLiteral|FloatLiteral, Identifier]` (or `[Identifier]` for `bN` where leading char is letter). See Validation Architecture below.

### Pitfall 2: Note-Stream Surface NOT Touched

**What goes wrong:** Adding `Nb` as a valid note-stream token (e.g., `| C4q 0.5b D4q |`) would conflict with D-17's deferral of `Nb.` dotted syntax AND with existing dotted-rhythm `q.`/`h.`/`w.` suffix language.

**Why it happens:** Note streams have their own grammar (NoteStreamCompiler) that accepts a restricted token set: notes, chord brackets, rests, durations, ties. Adding `Nb` would create grammatical ambiguity (is `0.5b` a beat-position offset, a duration, a rest with weird marking?).

**How to avoid:** D-17 explicitly defers `Nb.` and tied-Beat-literal syntax. The Phase 45 surface is TOP-LEVEL only — after `(`, `=`, `,`, `[`, `{`, `~>` per ERG-05. Inside `| ... |`, the existing duration suffixes (`q`/`h`/`w`/`e`/`s` + dotted `.` + ties `~`) remain the only grammar. Confirm by reading `NoteStreamCompiler.cs` and `SimpleLexer`'s note-stream lex mode — `BeatLiteral` tokens should never appear inside `| ... |` blocks.

**Warning signs:** Note-stream parse errors mentioning `BeatLiteral`; ambiguous lex output where `|` -> `|` brackets contain `BeatLiteral` tokens.

**Validation:** xUnit Theory in `BeatLiteralParserTests.cs` — `| C4q 0.5b D4q |` is a SYNTAX ERROR (assert via `ErrorReporter`); positive case `Beat b = 0.5b` and `(delay buf 0.5b 0.5 0.4)` both lex `BeatLiteral` correctly.

### Pitfall 3: Single-Field vs Two-Field Pragma Design

**What goes wrong:** Mirroring Phase 44's two-field `StrictMode` + `CallerStrictMode` design without understanding WHY Phase 44 needs both. Adding a `CallerBeatTrueToSig` snapshot would be DEAD CODE — nothing in Phase 45 reads "the caller's pragma bit at the moment of dispatch."

**Why it happens:** Pattern-matching by example. Phase 44's two-field design is canonical for "stdlib stays charitable when called from non-strict, errors when called from strict" — but Phase 45 has no equivalent leaf-site asymmetry. The multiplier reads at the EXECUTING file's pragma bit, which is `ctx.BeatTrueToSig`, and that's the semantically right answer because:
- `Nb` literal lexes IN the declaring file → evaluator reads `ctx.BeatTrueToSig` which IS the declaring file's bit (set by ModuleLoader push/set/restore).
- `(beat N)` call evaluates IN the declaring file → same.
- Stdlib `(beat N)` calls (from `audio.flow` / `bars.flow` / etc.) read `ctx.BeatTrueToSig` while the importer's bit is active... 

**Subtle correctness question:** If `audio.flow` doesn't declare `beat-true-to-sig`, its top-level statements run with `ctx.BeatTrueToSig = false` (per D-04 + the file-scope push/pop). BUT — a proc declared in `audio.flow` that's CALLED from a pragma-on file runs with `ctx.BeatTrueToSig = ?`. The answer per Phase 44 D-03: it's the DECLARING file's bit (file-scope semantics). For Phase 45 this means stdlib procs calling `(beat 0.5)` get raw-quarter behavior regardless of who calls them.

**How to avoid:** Single field per D-04. NO `CallerBeatTrueToSig`. The plan-phase verifier confirms this by checking that `ctx.BeatTrueToSig` reads happen at `EvaluateBeatLiteral` and inside the `BeatConstructorFunctions` lambda only — no leaf clamp sites consume it.

**Validation:** xUnit cross-file test (`tests/test_beat_cross_file.flow` + helper file) verifies that a stdlib-like helper file with no pragma is unaffected by the importer's pragma. Specifically: a pragma-on file imports a no-pragma helper file containing `proc bumpBeat Beat b => (beat 1)`; the call returns `Value.Beat(1.0)` regardless of caller's `timesig 6/8 {}` context.

### Pitfall 4: Identity Behavior in 4/4 with Pragma-On

**What goes wrong:** A composer enables `beat-true-to-sig` thinking it'll always-multiply, but in default 4/4 (or no active timesig) the multiplier is `4/4 = 1.0` — identity. They might wonder "why isn't my pragma doing anything?"

**Why it happens:** D-02 explicitly says: "with pragma on + 4/4 default, the multiplier is `4/4 = 1.0` (identity) — pragma activation does not corrupt scripts that never set a timesig." This is BY DESIGN (the most-common case must not break), but it's a footgun for composers expecting a global behavior change.

**How to avoid:** Tutorial files (`examples/beat/intro.flow` 6/8 jig + `examples/beat/cut-time.flow` 2/2) must use non-4/4 timesigs to make the multiplier visible. CLAUDE.md row (D-13) explicitly says "default 4/4 → `1b = quarter`" to signal the no-op behavior.

**Validation:** xUnit `BeatTrueToSigPragmaTests.cs` Theory pins identity-mode in 4/4 (pragma-on AND pragma-off both produce `Value.Beat(1.0)` for `1b`).

### Pitfall 5: `(str someBeat)` Round-Trip Breakage

**What goes wrong:** If `(str (beat 0.5))` emitted `"0.5b"`, then `(eval "0.5b")` under `beat-true-to-sig` in 6/8 would multiply → 0.25 quarters, then `(str)` of that emits `"0.25b"`, then re-eval re-multiplies → 0.125. Round-trip is not stable.

**Why it happens:** Pragma + timesig context affects construction semantics; canonical-form printing must either (a) include enough context to disambiguate, OR (b) print the raw quarters-internal value as plain double.

**How to avoid:** D-14 locks: `(str someBeat)` emits plain double like `"0.5"`. No `"b"` suffix. Composers continue to treat `Beat` as a tagged double for printing. If composer pressure surfaces a need for canonical-form printing for debugging, ship a `(strFull beat)` variant in a one-commit follow-up.

**Validation:** xUnit Fact pinning `(str (beat 0.5))` returns `"0.5"` (no `"b"`) in BOTH pragma modes.

### Pitfall 6: ModuleLoader Restore in `finally` Block

**What goes wrong:** Forgetting to wrap the `BeatTrueToSig` set in a `try { ... } finally { restore }` block leaks the imported file's bit into subsequent statements in the importer.

**Why it happens:** Save-set-restore is easy to forget; without the `finally`, an `Execute` that throws or that triggers `ErrorReporter` short-circuit leaves the field in the wrong state.

**How to avoid:** Mirror Phase 44's exact wiring at `ModuleLoader.cs:125-126` and `:203` — set BEFORE the `try`, restore in the `finally`. Phase 44 PATTERNS.md Anti-Pattern 1 documents this as load-bearing.

**Validation:** xUnit Fact constructs a sequence: pragma-off file `use`-s a pragma-on file that throws (e.g., `(throw "boom")` if such exists, or a parse-error path); confirms the importer's `ctx.BeatTrueToSig` is restored to `false` after the throw.

### Pitfall 7: PragmaScanner Sees Hyphenated Name

**What goes wrong:** The pragma name `beat-true-to-sig` contains hyphens. Most other registered pragmas are camelCase (`justIntonation`, `matchExhaustive`, `equalTemperament`) or single-token (`strict`, `hAsB`, `scaleLint`, `pythagorean`). Hyphens are unusual.

**Why it happens:** D-03 deliberately chose hyphenated form because pragma names are composer-facing English phrases and `beat-true-to-sig` reads cleanly in kebab.

**How to avoid:** Verify PragmaScanner accepts hyphens in pragma names. If `enable <pragma>;` parser uses an identifier-character class, hyphens may not be accepted today. Confirm by reading `PragmaScanner.cs` and pre-empt any rejection.

**Validation:** xUnit Fact pins `enable beat-true-to-sig;` parses without error AND triggers `pragmaSet.Has("beat-true-to-sig") == true`. Also pin `enable bea-true-to-sig;` (typo) emits Levenshtein "did you mean beat-true-to-sig?" suggestion via `PragmaRegistry.SuggestNearest`.

**Action item for plan-phase:** Add a Wave-1 sub-task to read `PragmaScanner.cs` and verify the lex accepts hyphens. If not, this becomes a load-bearing parser change (small but in scope).

## Code Examples

Verified patterns from official sources (this repo at HEAD):

### Example 1: `BeatLiteralExpression` AST record

```csharp
// flow-lang/Ast/Expressions/BeatLiteralExpression.cs  (NEW per REQ-BEAT-AST-01)
// Source pattern: flow-lang/Ast/Expressions/SymbolLiteralExpression.cs

using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// A beat literal expression like <c>0.5b</c>, <c>2b</c>, <c>-1b</c> (Phase 45 D-01).
/// Carries the raw source double exactly as written; the multiplier formula
/// <c>final = pragma_on ? raw × (4.0 / denom) : raw</c> applies at eval time
/// in <c>ExpressionEvaluator.EvaluateBeatLiteral</c>, reading
/// <see cref="FlowLang.Runtime.ExecutionContext.BeatTrueToSig"/> +
/// <see cref="FlowLang.Runtime.MusicalContext.TimeSignature"/>.
/// </summary>
public record BeatLiteralExpression(
    SourceLocation Location,
    double RawValue,
    Span? Span = null
) : Expression(Location);
```

### Example 2: Lexer signed branch (`+/-Nb`)

```csharp
// flow-lang/Lexing/SimpleLexer.cs — TryLookAheadSpecialLiteral
// INSERTION POINT: after the existing "st" branch (lines 608-621),
// before the "c" branch (lines 623-635) — order is cosmetic.
// Source pattern: lines 608-621 (st) and 637-650 (dB).

// Try "b" suffix (beat literal — Phase 45 D-06)
if (!IsAtEnd() && Peek() == 'b' && !char.IsLetter(PeekNext()))
{
    sb.Append(Advance());
    text = sb.ToString();

    string numberPart = text.Substring(0, text.Length - 1);
    if (double.TryParse(numberPart, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double beatValue))
    {
        return new Token(TokenType.BeatLiteral, text, start, beatValue,
                         Span: new Span(start, CurrentLocation()));
    }
}
```

### Example 3: Lexer unsigned branch (`Nb`)

```csharp
// flow-lang/Lexing/SimpleLexer.cs — ScanNumberOrSpecialLiteral
// INSERTION POINT: as a new `else if` block between the "c" branch
// (lines 766-776) and the "s" branch (lines 778-788).
// Source pattern: lines 766-776 (c).

// Try "b" suffix (beat literal — Phase 45 D-07)
else if (Peek() == 'b' && !char.IsLetter(PeekNext()))
{
    sb.Append(Advance());
    var text = sb.ToString();

    string numberPart = text.Substring(0, text.Length - 1);
    if (double.TryParse(numberPart, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double beatValue))
    {
        return new Token(TokenType.BeatLiteral, text, start, beatValue,
                         Span: new Span(start, CurrentLocation()));
    }
}
```

### Example 4: Parser emit

```csharp
// flow-lang/Parsing/Parser.cs — ParsePrimary (or equivalent)
// INSERTION POINT: alongside the existing music-literal arms (lines 1346-1367).
// Pattern DIVERGES from CentLiteral/TimeLiteral/etc which emit LiteralExpression(text):
// Beat emits the dedicated record because the raw double must survive to eval time.

if (Match(TokenType.BeatLiteral))
{
    // Phase 45 D-09: Token.Value carries the parsed double; preserve through to eval.
    double rawValue = (double)PreviousToken.Value!;
    return new BeatLiteralExpression(PreviousToken.Location, rawValue,
                                     Span: PreviousToken.EffectiveSpan);
}
```

### Example 5: Evaluator arm

```csharp
// flow-lang/Interpreter/ExpressionEvaluator.cs — Evaluate switch (lines 37-58)
// INSERTION POINT: alongside SymbolLiteralExpression arm (line 46).

return expr switch
{
    LiteralExpression lit => EvaluateLiteral(lit),
    // ...
    SymbolLiteralExpression symLit => EvaluateSymbolLiteral(symLit),
    BeatLiteralExpression beatLit => EvaluateBeatLiteral(beatLit),   // NEW
    LambdaExpression lambda => EvaluateLambda(lambda),
    // ...
};

// New method:
private Value EvaluateBeatLiteral(BeatLiteralExpression beatLit)
{
    // Phase 45 D-10 multiplier formula:
    //   final = pragma_on ? raw × (4.0 / denom) : raw
    int denom = _context.GetMusicalContext().TimeSignature?.Denominator ?? 4;
    double multiplier = _context.BeatTrueToSig ? (4.0 / denom) : 1.0;
    return Value.Beat(beatLit.RawValue * multiplier);
}
```

### Example 6: Pragma registry entry

```csharp
// flow-lang/Lexing/PragmaRegistry.cs:27-37
// Phase 45 D-03 — single-line insertion at end of KnownPragmas dict.

public static readonly IReadOnlyDictionary<string, string> KnownPragmas =
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["hAsB"] = "...",
        ["justIntonation"] = "...",
        ["pythagorean"] = "...",
        ["equalTemperament"] = "...",
        ["scaleLint"] = "...",
        ["matchExhaustive"] = "...",
        ["strict"] = "...",
        // Phase 45 D-03:
        ["beat-true-to-sig"] = "Opt-in: Nb literals and (beat N) constructor calls multiply by 4/denominator at eval time, reading active timesig. So in 'timesig 6/8 { }' with pragma on, 1b = 1 eighth. File-scoped, no propagation via use imports."
    };
```

### Example 7: `(beat N)` constructor migration

```csharp
// flow-lang/StandardLibrary/Audio/BeatConstructorFunctions.cs (NEW)
// OR extend BeatConversionFunctions.cs with this method.
// Source pattern: BeatConversionFunctions.cs:45-96.

using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.StandardLibrary.Audio;

public static class BeatConstructorFunctions
{
    /// <summary>
    /// Phase 45 D-05 — migrates the (beat Double) → Beat constructor from
    /// plain Register to RegisterContextDependent so the lambda can read
    /// ctx.BeatTrueToSig + ctx.GetMusicalContext().TimeSignature at call time.
    /// Mirrors the Phase 43 BeatConversionFunctions recipe.
    /// </summary>
    public static void RegisterContextDependent(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
        var sig = new FunctionSignature("beat", [DoubleType.Instance],
            ParameterNames: ["value"]);
        registry.Register("beat", sig, args =>
        {
            double raw = args[0].As<double>();
            int denom = context.GetMusicalContext().TimeSignature?.Denominator ?? 4;
            double multiplier = context.BeatTrueToSig ? (4.0 / denom) : 1.0;
            return Value.Beat(raw * multiplier);
        });
    }
}

// Wire-up in BuiltInFunctions.cs:1016+:
public static void RegisterContextDependentFunctions(
    InternalFunctionRegistry registry,
    FlowLang.Runtime.ExecutionContext context)
{
    // ... existing wiring ...
    Audio.BeatConversionFunctions.RegisterContextDependent(registry, context);
    Audio.BeatConstructorFunctions.RegisterContextDependent(registry, context);  // NEW (Phase 45)
    // ...
}

// And DELETE the existing lines 547-555 in BuiltInFunctions.cs:
// registry.Register("beat", new FunctionSignature("beat", [DoubleType.Instance], ...),
//     args => Value.Beat(args[0].As<double>()));
```

### Example 8: ModuleLoader push/pop wiring

```csharp
// flow-lang/Runtime/ModuleLoader.cs — LoadModule
// INSERTION POINT: lines 125-126 (set) and 203 (restore), parallel to StrictMode.

var prevStrict = context.StrictMode;
context.StrictMode = pragmaSet.Has("strict");
var prevBeatTrueToSig = context.BeatTrueToSig;                              // NEW
context.BeatTrueToSig = pragmaSet.Has("beat-true-to-sig");                  // NEW
try
{
    interpreter.Execute(program);
    // ... ModuleRegistry registration block ...
}
finally
{
    context.StrictMode = prevStrict;
    context.BeatTrueToSig = prevBeatTrueToSig;                              // NEW
}
```

### Example 9: FlowEngine top-level Execute wiring

```csharp
// flow-lang/Core/FlowEngine.cs — Execute (line ~296) + new private method.

// In Execute():
ApplyTuningPragma(program);
ApplyStrictPragma(program);
ApplyBeatTrueToSigPragma(program);    // NEW (Phase 45)
// ...
_interpreter.Execute(program);

// New helper method (mirrors ApplyStrictPragma at lines 352-355):
private void ApplyBeatTrueToSigPragma(Ast.Program program)
{
    _context.BeatTrueToSig = program.Pragmas.Has("beat-true-to-sig");
}
```

## State of the Art

Phase 45 is incremental and does not change the state of the art for any external technology.

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `(beat 0.5)` constructor only | `0.5b` literal + `(beat 0.5)` constructor (both pragma-aware) | Phase 45 | Composer ergonomics — short, type-safe, matches `Nms`/`Ns`/`Nc`/`Nst`/`NdB`/`NHz` family |
| `1b` always = quarter | `1b` = quarter (default) OR active-timesig beat unit (opt-in) | Phase 45 | Musician-intuition path for non-quarter meters; opt-in preserves back-compat |
| `(beat N)` registered plain | `(beat N)` registered via `RegisterContextDependent` | Phase 45 | Constructor now reads pragma + timesig context |

**Deprecated/outdated:** Nothing deprecated in Phase 45. Pre-traction no-deprecation latitude (`project_pre_public_no_legacy_burden`) is ACTIVE — breaking changes ship in one commit; no `flow migrate` subcommand.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `PragmaScanner` already accepts hyphenated pragma names like `beat-true-to-sig` | Pitfall 7 | If not, plan-phase must add a small parser change to accept hyphens in pragma identifier; Wave 3 acquires an extra task. Mitigation: read `PragmaScanner.cs` before Wave 3 starts. |
| A2 | The `else if` chain in `ScanNumberOrSpecialLiteral` (lines 715-789) is structured such that inserting a new `else if` block between existing arms is mechanically safe and order-preserving | Pattern 1 + Anti-Pattern note | If chain shape differs from what I've read, the inserted branch could be unreachable or could shadow `c`/`s`. Mitigation: Wave 2 includes a parser-level xUnit Theory that enumerates ALL music-literal suffix collisions before-and-after insertion. |
| A3 | `BeatLiteral` tokens never appear inside note streams (`| ... |`) | Pitfall 2 | If `NoteStreamCompiler` accepts `BeatLiteral` and produces unspecified behavior, composer scripts could mysteriously parse. Mitigation: Wave 2 xUnit Theory explicitly tests that `| C4q 0.5b D4q |` errors. |
| A4 | Phase 26.1 DICT-01 acceptance (`<<C4, (beat 0.25)>>` Dict-key shape) does not regress through `RegisterContextDependent` migration | REQ-BEAT-CONSTRUCTOR-02 | If the migration somehow changes signature dispatch (e.g., overload resolution sees a different signature), Dict construction could break. Mitigation: explicit xUnit regression fact in `BeatLiteralFacts.cs` or `BeatTrueToSigPragmaTests.cs`. Cheap insurance per CONTEXT Claude's Discretion. |

## Open Questions

1. **`PragmaScanner` hyphen acceptance**
   - What we know: existing registered pragmas are camelCase or single-token. `beat-true-to-sig` is the first hyphenated registered name.
   - What's unclear: whether the `enable <name>;` scanner accepts `-` in `<name>`.
   - Recommendation: Wave 1 first task — read `PragmaScanner.cs` and verify. If unaccepted, add a minimal scanner change to accept hyphens as part of the pragma identifier.

2. **`BeatLiteralExpression` Span construction in error paths**
   - What we know: `Token.EffectiveSpan` synthesizes a zero-width span when `Token.Span` is null. Newer (post-Phase-35) tokens have Span. Lexer post-Phase-35 should always populate Span.
   - What's unclear: whether the parser construction `new BeatLiteralExpression(loc, raw, Span: PreviousToken.EffectiveSpan)` produces correct diagnostic spans in all error cases.
   - Recommendation: mirror the exact pattern used by `SymbolLiteralExpression` in `Parser.cs:1366-1367` — same `EffectiveSpan` approach.

3. **Whether `examples/beat/` tutorial files should render committed baselines under `flow-lang.Tests/baselines/Phase45/`**
   - What we know: Phase 28+ baselines are committed because dither RNG is seeded deterministically; tutorial WAVs use pure synthesis → two-run cmp-clean preserved.
   - What's unclear: whether plan-phase wants a Phase 45 baselines directory committed.
   - Recommendation: yes — match Phase 28 precedent. Adds CI surface for any future regression that changes tutorial outputs unexpectedly.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Compile flow-lang.dll + tests | ✓ (assumed — project already builds) | 10.0 | — |
| `dotnet build` / `dotnet run` / `dotnet test` | Execute tests | ✓ | matches SDK | — |
| `Melanchall.DryWetMidi 8.0.3` | `writeMidi` in tutorial files | ✓ (already a project dep) | 8.0.3 | — |
| PulseAudio (Linux playback) | Tutorial WAV playback during composer use (NOT required for CI tests) | ✓ on dev box | system | Tests don't depend on playback; rendered WAVs use file-write path only |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** None.

## Validation Architecture

Per `.planning/config.json` `workflow.nyquist_validation: true`, this section maps every behavior to a sampling-rate-2+ measurement.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (already in use; `flow-lang.Tests/flow-lang.Tests.csproj`) |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` (no separate xunit.runner.json detected) |
| Quick run command | `dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase45"` |
| Full suite command | `dotnet test` (entire test project) |
| `.flow` integration smoke | `for test in tests/test_beat_*.flow; do dotnet run --project flow-interpreter "$test"; done` |

### Signal Inventory

Phase 45 has SIX distinct correctness signals; each needs ≥2 sample points per behavior boundary.

#### Signal 1 — Lexer Correctness (REQ-BEAT-LEX-01 / 02 / 03 / 04)

**Behavior boundary:** Token type assigned correctly for each input shape.

| Input | Expected token | Sample location |
|-------|----------------|-----------------|
| `0.5b` | `BeatLiteral(0.5)` | `BeatLiteralParserTests.LexUnsignedFractional` |
| `2b` | `BeatLiteral(2.0)` | `BeatLiteralParserTests.LexUnsignedInteger` |
| `1.0b` | `BeatLiteral(1.0)` | `BeatLiteralParserTests.LexUnsignedDecimalZero` |
| `+1b` | `BeatLiteral(1.0)` (signed) | `BeatLiteralParserTests.LexSignedPositive` |
| `-2b` | `BeatLiteral(-2.0)` (signed) | `BeatLiteralParserTests.LexSignedNegative` |
| `+0.5b` | `BeatLiteral(0.5)` (signed fractional) | `BeatLiteralParserTests.LexSignedFractional` |
| `-0.25b` | `BeatLiteral(-0.25)` (signed fractional) | `BeatLiteralParserTests.LexSignedFractionalNegative` |
| `1bar` | `[IntLiteral(1), Identifier("bar")]` — IDENTIFIER GUARD | `BeatLiteralParserTests.LexNotConsumedByIdentifierBar` |
| `2beats` | `[IntLiteral(2), Identifier("beats")]` | `BeatLiteralParserTests.LexNotConsumedByIdentifierBeats` |
| `0.5bpm` | `[FloatLiteral(0.5), Identifier("bpm")]` | `BeatLiteralParserTests.LexNotConsumedByIdentifierBpm` |
| `b1` | `[Identifier("b1")]` | `BeatLiteralParserTests.LexBStartingIdentifier` |
| `Bb` | flat note literal (existing behavior) | `BeatLiteralParserTests.LexBbStillFlatNote` |
| `B4` | note literal (existing) | `BeatLiteralParserTests.LexB4StillNoteLiteral` |
| `Bmaj7` | ChordLiteral (existing) | `BeatLiteralParserTests.LexBmaj7StillChordLiteral` |
| `0.5b D4q` | `[BeatLiteral(0.5), Identifier("D4q")]` (post-lexer phase distinguishes note duration) | `BeatLiteralParserTests.LexFollowedByNoteToken` |

**Sampling rate:** ≥2 sample points per category (positive fractional, positive integer, signed pos, signed neg, identifier-guard non-collision, existing-token preservation). Total: 15 Facts/Theory cases.

#### Signal 2 — AST Shape Correctness (REQ-BEAT-AST-01 / 02 / 03 / 04)

**Behavior boundary:** Parser emits `BeatLiteralExpression`, NOT `LiteralExpression`.

| Source | Expected AST root | Sample location |
|--------|-------------------|-----------------|
| `Beat b = 0.5b` | `VariableDeclaration` containing `BeatLiteralExpression(0.5)` | `BeatLiteralParserTests.AstShapeAssignedToVariable` |
| `(delay buf 0.5b 0.5 0.4)` | `FunctionCallExpression` with arg[1] = `BeatLiteralExpression(0.5)` | `BeatLiteralParserTests.AstShapeAsFunctionArg` |
| `0.5b -> (delay buf 0.5 0.4)` | After flow desugar: `FunctionCallExpression(delay, [buf, BeatLiteralExpression(0.5), 0.5, 0.4])` | `BeatLiteralParserTests.AstShapeViaFlowOperator` |
| `(add 0.5b 0.5b)` | `FunctionCallExpression(add, [BeatLiteralExpression(0.5), BeatLiteralExpression(0.5)])` | `BeatLiteralParserTests.AstShapeAsArithmeticOperand` |
| `<<C4, 0.5b>>` | `TupleLiteralExpression([Note, BeatLiteralExpression(0.5)])` | `BeatLiteralParserTests.AstShapeInTuple` (Phase 26.1 DICT-01 reuse) |

**Sampling rate:** ≥2 sample points per position kind (assignment, call arg, flow LHS, arithmetic operand, tuple element). Total: 5 Facts.

#### Signal 3 — Pragma Boundary (REQ-BEAT-PRAGMA-01 / 02 / 03 / 04)

**Behavior boundary:** File-scope `ExecutionContext.BeatTrueToSig` reflects declaring file's pragma; restored on cross-file exit.

| Scenario | Expected | Sample location |
|----------|----------|-----------------|
| `enable beat-true-to-sig;` at top of file | `ctx.BeatTrueToSig == true` after `Execute` returns | `BeatTrueToSigPragmaTests.PragmaSetsContextBit` |
| File without pragma | `ctx.BeatTrueToSig == false` after `Execute` | `BeatTrueToSigPragmaTests.AbsenceLeavesBitFalse` |
| Pragma-off file `use`-s pragma-on file | After `use` returns, caller's `ctx.BeatTrueToSig == false` (restored) | `BeatTrueToSigPragmaTests.CrossFileRestoreToFalse` |
| Pragma-on file `use`-s pragma-off file | After `use` returns, caller's `ctx.BeatTrueToSig == true` (restored) | `BeatTrueToSigPragmaTests.CrossFileRestoreToTrue` |
| Pragma-on file `use`-s file that throws | After throw, caller's `ctx.BeatTrueToSig == true` (finally-restore semantics) | `BeatTrueToSigPragmaTests.CrossFileRestoreAfterThrow` |
| `PragmaRegistry.KnownPragmas["beat-true-to-sig"]` | Present, description matches D-03 text verbatim | `BeatTrueToSigPragmaTests.PragmaRegistryEntry` |
| `enable bea-true-to-sig;` (typo) | Errors with `did-you-mean beat-true-to-sig?` | `BeatTrueToSigPragmaTests.LevenshteinSuggestion` |

**Sampling rate:** ≥2 sample points per direction (pragma-off, pragma-on, throw-during-import). Total: 7 Facts.

#### Signal 4 — Multiplier Formula (REQ-BEAT-AST-04 + REQ-BEAT-CONSTRUCTOR-01)

**Behavior boundary:** Computed `Value.Beat` is `raw × multiplier` where `multiplier = pragma_on ? 4/denom : 1.0`.

**Theory grid:** {timesig} × {pragma state} × {raw value} = ≥10 sample points (Theory)

| timesig | pragma | raw `Nb` | Expected `Value.Beat` quarters |
|---------|--------|----------|--------------------------------|
| `4/4` | off | `1b` | `1.0` |
| `4/4` | on | `1b` | `1.0` (identity, denom=4 → multiplier=1) |
| `6/8` | off | `1b` | `1.0` (unchanged) |
| `6/8` | on | `1b` | `0.5` (multiplier = 4/8 = 0.5) |
| `6/8` | on | `2b` | `1.0` |
| `6/8` | on | `0.5b` | `0.25` |
| `2/2` | on | `1b` | `2.0` (multiplier = 4/2 = 2.0) |
| `2/2` | on | `0.5b` | `1.0` |
| `5/4` | on | `1b` | `1.0` (identity in /4 denominators) |
| `7/8` | on | `1b` | `0.5` |
| `4/4` | on | `-2b` | `-2.0` (negative passthrough) |
| no timesig active | on | `1b` | `1.0` (default 4/4 → identity) |
| `4/4` | on | `(beat 1)` constructor | `1.0` (same multiplier applies through `RegisterContextDependent`) |
| `6/8` | on | `(beat 1)` constructor | `0.5` |
| `6/8` | off | `(beat 1)` constructor | `1.0` |

**Sampling rate:** ≥2 sample points per (timesig, pragma) cell with extra coverage on the 6/8 row. Both literal and constructor paths covered. Total: 15 Theory cases.

#### Signal 5 — Phase 26.1 DICT-01 Regression (REQ-BEAT-CONSTRUCTOR-02)

**Behavior boundary:** `<<C4, (beat 0.25)>>` continues to work as a Dict key under both pragma modes.

| Scenario | Expected | Sample location |
|----------|----------|-----------------|
| Pragma-off, `4/4` | `Dict<<Note, Beat>, Int> d = (dict <<C4, (beat 0.25)>> 100)` round-trips | `BeatTrueToSigPragmaTests.Dict01RegressionPragmaOff` |
| Pragma-on, `4/4` | Same (multiplier=1, value identical) | `BeatTrueToSigPragmaTests.Dict01RegressionPragmaOn4Over4` |
| Pragma-on, `6/8` | Dict key is `<<C4, Value.Beat(0.125)>>` (multiplier=0.5 applied) | `BeatTrueToSigPragmaTests.Dict01RegressionPragmaOn6Over8` |

**Sampling rate:** ≥2 sample points per pragma state. Total: 3 Facts.

#### Signal 6 — `(str someBeat)` Round-Trip Lock (D-14)

**Behavior boundary:** `(str (beat 0.5))` emits `"0.5"` (no `"b"` suffix) regardless of pragma mode.

| Scenario | Expected | Sample location |
|----------|----------|-----------------|
| Pragma-off | `(str (beat 0.5))` → `"0.5"` | `BeatTrueToSigPragmaTests.StrEmitsPlainDoublePragmaOff` |
| Pragma-on, `4/4` | `(str (beat 0.5))` → `"0.5"` | `BeatTrueToSigPragmaTests.StrEmitsPlainDoublePragmaOn4Over4` |
| Pragma-on, `6/8` | `(str (beat 1.0))` → `"0.5"` (multiplier applied at construction; str shows quarter value) | `BeatTrueToSigPragmaTests.StrEmitsQuarterValuePragmaOn6Over8` |
| Pragma-on, `2/2` | `(str (beat 0.5))` → `"1.0"` (multiplier 2.0 applied) | `BeatTrueToSigPragmaTests.StrEmitsQuarterValuePragmaOn2Over2` |

**Sampling rate:** ≥2 per pragma state. Total: 4 Facts.

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| REQ-BEAT-LEX-01 | TokenType.BeatLiteral exists | unit | `dotnet test --filter "TokenTypeEnumContainsBeatLiteral"` | Wave 0 |
| REQ-BEAT-LEX-02 | Signed `+/-Nb` lexes | unit | `dotnet test --filter "BeatLiteralParserTests.LexSigned*"` | Wave 0 |
| REQ-BEAT-LEX-03 | Unsigned `Nb` + identifier-guard | unit | `dotnet test --filter "BeatLiteralParserTests.LexUnsigned*\|LexNotConsumed*"` | Wave 0 |
| REQ-BEAT-LEX-04 | Negative beat values accepted | unit | `dotnet test --filter "LexSignedNegative\|LexSignedFractionalNegative"` | Wave 0 |
| REQ-BEAT-AST-01 | BeatLiteralExpression record exists | unit | `dotnet build` (compile check) + `dotnet test --filter "AstShape*"` | Wave 0 |
| REQ-BEAT-AST-02 | Parser emits BeatLiteralExpression | unit | `dotnet test --filter "BeatLiteralParserTests.AstShape*"` | Wave 0 |
| REQ-BEAT-AST-03 | Literal-token-set includes BeatLiteral | unit | `dotnet test --filter "AstShapeAsArithmeticOperand\|AstShapeInTuple"` | Wave 0 |
| REQ-BEAT-AST-04 | EvaluateBeatLiteral arm | unit | `dotnet test --filter "BeatTrueToSigPragmaTests.MultiplierFormula*"` | Wave 0 |
| REQ-BEAT-PRAGMA-01 | PragmaRegistry entry | unit | `dotnet test --filter "PragmaRegistryEntry\|LevenshteinSuggestion"` | Wave 0 |
| REQ-BEAT-PRAGMA-02 | ctx.BeatTrueToSig field | unit | `dotnet test --filter "PragmaSetsContextBit\|AbsenceLeavesBitFalse"` | Wave 0 |
| REQ-BEAT-PRAGMA-03 | FlowEngine.ApplyBeatTrueToSigPragma | unit | `dotnet test --filter "PragmaSetsContextBit"` | Wave 0 |
| REQ-BEAT-PRAGMA-04 | ModuleLoader push/pop | unit | `dotnet test --filter "CrossFileRestore*"` | Wave 0 |
| REQ-BEAT-CONSTRUCTOR-01 | (beat N) RegisterContextDependent | unit | `dotnet test --filter "MultiplierFormula.*Constructor"` | Wave 0 |
| REQ-BEAT-CONSTRUCTOR-02 | Phase 26.1 DICT-01 regression | unit | `dotnet test --filter "Dict01Regression*"` | Wave 0 |
| REQ-BEAT-TEST-01 .. 04 | Positive .flow scripts run to completion | integration | `for t in tests/test_beat_*.flow; do dotnet run --project flow-interpreter "$t"; done` | Wave 0 |
| REQ-BEAT-TEST-07 | Two-run cmp-clean for tutorial WAVs | integration | Run `examples/beat/intro.flow` twice; diff resulting WAV. SHA must match. | Wave 0 |
| REQ-BEAT-DOC-01 / 02 / 03 / 04 | CLAUDE.md + tutorials present | docs | Manual review at phase-gate; existence check via `test -f` | Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase45"` — runs Phase 45 xUnit Facts only (~50 cases; <10s)
- **Per wave merge:** `dotnet test` — entire test suite
- **Per phase gate:** Full suite + `for t in tests/test_beat_*.flow; do dotnet run --project flow-interpreter "$t"; done` + render `examples/beat/intro.flow` twice + SHA-compare. Confirms two-run cmp-clean.

### Wave 0 Gaps

- [ ] Create `flow-lang.Tests/Integration/Phase45/BeatLiteralParserTests.cs` — covers Signals 1 + 2 (~20 Theory/Fact cases)
- [ ] Create `flow-lang.Tests/Integration/Phase45/BeatTrueToSigPragmaTests.cs` — covers Signals 3 + 4 + 5 + 6 (~30 Theory/Fact cases)
- [ ] Create 4 `.flow` files under `tests/test_beat_*.flow` (smoke + pragma-off + pragma-on + cross-file)
- [ ] Optionally create `flow-lang.Tests/baselines/Phase45/` directory and commit tutorial WAV baselines (recommended — Phase 28 precedent)
- [ ] No framework install needed — xUnit already wired

*(All gaps closeable in normal phase waves; no upstream framework work required.)*

## Security Domain

> Phase 45 is a language-surface ergonomics phase touching the lexer + AST + evaluator + pragma plumbing. No network surface, no auth, no file I/O beyond existing `writeWav` / `writeMidi` patterns. ASVS analysis below for completeness.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | N/A — language runtime, no user auth |
| V3 Session Management | no | N/A |
| V4 Access Control | no | N/A — pragma is composer's own opt-in |
| V5 Input Validation | minimal | Lexer `double.TryParse` with `InvariantCulture` rejects malformed numeric prefixes; identifier-guard rejects letter-collisions. New surface inherits existing Lexer guards. |
| V6 Cryptography | no | N/A |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Lexer-induced DoS via deeply nested literals | Denial of Service | Existing Phase 36 T-36-17 / Phase 39 D-39-19 DoS-cap precedent — Phase 45 adds no recursive lex paths; immune. |
| Cross-file pragma leak (importer's bit corrupted by importee) | Tampering (state corruption) | `try { … } finally { restore }` push/pop discipline; Phase 44 Anti-Pattern 1. Pitfall 6 above. xUnit cross-file restore-after-throw test pins behavior. |
| Multiplier overflow on extreme raw values | Denial of Service (NaN/Inf propagation) | `double` arithmetic with finite `denom ∈ {1, 2, 4, 8, 16, ...}` — multiplier ∈ {0.0625, 0.125, 0.25, 0.5, 1, 2, 4, ...}. No overflow risk for any realistic `raw`. Tests pin extremes (`-1e300b`, `1e300b`) for documentation. |
| Slopsquatted dependency | Tampering (supply chain) | N/A — Phase 45 adds no external packages. |

## Sources

### Primary (HIGH confidence — verified at repo HEAD)

- `.planning/phases/45-beat-literal-syntax-true-to-sig-pragma/45-CONTEXT.md` — All 17 D-NN decisions; quoted verbatim in `<user_constraints>` block above.
- `.planning/phases/44-strict-mode/44-CONTEXT.md` — D-02 / D-03 / D-04 pragma push/pop precedent; D-14 two-track testing precedent.
- `.planning/phases/43-module-names-qualified-imports/43-CONTEXT.md` — D-08 `RegisterContextDependent` precedent.
- `CLAUDE.md` — project goals, conventions, Music Types Quick Reference table (Beat row addition target).
- `flow-lang/Lexing/SimpleLexer.cs` lines 75-200, 510-820 (NextToken dispatch, signed-typed-literal `TryLookAheadSpecialLiteral`, unsigned `ScanNumberOrSpecialLiteral`).
- `flow-lang/Lexing/TokenType.cs` lines 61-65 (music-literal enum cluster).
- `flow-lang/Lexing/Token.cs` lines 26-49 (Token record with `Value` payload).
- `flow-lang/Lexing/PragmaRegistry.cs` (closed-set KnownPragmas dict; Levenshtein suggester).
- `flow-lang/Ast/Expressions/SymbolLiteralExpression.cs` (closest precedent for `BeatLiteralExpression` shape).
- `flow-lang/Parsing/Parser.cs` lines 1340-1367 (music-literal Match arms); lines 2103-2109 (literal-token-set check).
- `flow-lang/Interpreter/ExpressionEvaluator.cs` lines 35-58 (switch dispatch); lines 61-119 (TryParseSpecialLiteral re-parse path).
- `flow-lang/Runtime/ExecutionContext.cs` lines 439-495 (Phase 44 StrictMode + CallerStrictMode fields); lines 790-862 (GetMusicalContext three-tier resolution).
- `flow-lang/Runtime/ModuleLoader.cs` lines 75-204 (LoadModule with StrictMode push/pop precedent at lines 117-204).
- `flow-lang/Runtime/MusicalContext.cs` lines 42-50 (TimeSignature field on MusicalContext).
- `flow-lang/TypeSystem/SpecialTypes/TimeSignatureType.cs` lines 6-52 (TimeSignatureData.Numerator/Denominator).
- `flow-lang/TypeSystem/SpecialTypes/BeatType.cs` (entire file — type contract unchanged in Phase 45).
- `flow-lang/Runtime/Value.cs` line 42 (`public static Value Beat(double value) => new(value, BeatType.Instance);`).
- `flow-lang/StandardLibrary/Audio/BeatConversionFunctions.cs` (Phase 43 RegisterContextDependent canonical recipe).
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` lines 547-555 ((beat Double) current registration to migrate); lines 1016-1033 (RegisterContextDependentFunctions wire-up site).
- `flow-lang/Core/FlowEngine.cs` lines 289-355 (Execute pragma application order; ApplyStrictPragma helper).
- `.planning/config.json` (`workflow.nyquist_validation: true` confirmed).

### Secondary (MEDIUM confidence)

- None — Phase 45 is fully bounded by repo state; no external sources consulted.

### Tertiary (LOW confidence)

- None.

## Metadata

**Confidence breakdown:**

- Standard stack: HIGH — every recommended component verified in-repo at HEAD with file:line refs.
- Architecture: HIGH — all patterns traced to specific Phase 21/22/26.1/28/30/32/43/44 precedents in the codebase.
- Pitfalls: HIGH — every pitfall traced to either an existing pattern (Pitfall 1, 5, 6) or an explicit CONTEXT.md decision (Pitfall 2, 3, 4, 7).
- Validation: HIGH — every signal has ≥2 sample points; Theory grids enumerated case-by-case.

**Research date:** 2026-05-25
**Valid until:** 2026-06-24 (~30 days; stable scope — no fast-moving external dependencies)

---

## RESEARCH COMPLETE

**Phase:** 45 - Beat Literal Syntax & True-to-Sig Pragma
**Confidence:** HIGH

### Key Findings

- Phase 45 is mechanically small but cross-cutting: 6 files edited + 2 files created (AST record + constructor function class) + 4 `.flow` tests + 2 xUnit test files + 2 tutorial files + CLAUDE.md update. All patterns precedented by Phases 21/22/26.1/28/30/32/43/44.
- Single-field `ExecutionContext.BeatTrueToSig` design is sound (D-04). The two-field strict-mode design exists because Phase 44 needs to distinguish "DECLARED-in-strict" from "CALLED-from-strict" at leaf clamp sites — Phase 45 has no equivalent asymmetry; the multiplier reads the EXECUTING file's pragma bit and that's the semantically right answer.
- `BeatLiteralExpression` AST record breaks from the existing music-literal `LiteralExpression(text)` pattern (used by `CentLiteral`/`TimeLiteral`/`DecibelLiteral`/`HertzLiteral`) because eval-time multiplier needs the raw double preserved. The Phase 26.1 `SymbolLiteralExpression` is the exact shape precedent.
- The actual `MusicalContext` accessor is `context.GetMusicalContext().TimeSignature` (returns three-tier-fallback resolved instance with memoization) — NOT the conceptual name `ctx.ActiveMusicalContext.TimeSignature` used in CONTEXT.md. Defensive `?? 4` is harmless belt-and-suspenders since the default 4/4 is already guaranteed.
- The unsigned lexer scanner uses `else if` chaining (not sequential `if`-with-rewind like the signed one) — order of insertion in `ScanNumberOrSpecialLiteral` matters. Insert `b` branch as a new `else if` between existing `c` and `s` branches.
- One small open question: `PragmaScanner.cs` must accept hyphens in `enable beat-true-to-sig;`. Worth a Wave 1 first-task verification before proceeding to deeper plan tasks.

### File Created

`/home/noah/Desktop/projects/flow-sharp/.planning/phases/45-beat-literal-syntax-true-to-sig-pragma/45-RESEARCH.md`

### Confidence Assessment

| Area | Level | Reason |
|------|-------|--------|
| Standard Stack | HIGH | No new packages; all components verified at file:line in repo |
| Architecture | HIGH | All patterns traced to specific Phase 21/22/26.1/28/30/32/43/44 sites |
| Pitfalls | HIGH | Every pitfall has a verifying xUnit Theory in Validation Architecture |
| Validation | HIGH | 6 signals × ≥2 sample points each = ~50 xUnit cases enumerated |
| Pragma scanner hyphen-acceptance | MEDIUM | Untested in this research session; flagged as Open Question 1 + Pitfall 7 with Wave 1 verification task |

### Open Questions

1. Does `PragmaScanner.cs` accept hyphens in pragma names? Wave 1 first task verifies.
2. Should `flow-lang.Tests/baselines/Phase45/` commit tutorial WAV baselines? Recommend yes (Phase 28 precedent).
3. Should the `(beat N)` constructor migration land in a new `BeatConstructorFunctions.cs` file or extend `BeatConversionFunctions.cs`? Plan-phase's call — either pattern fits.

### Ready for Planning

Research complete. Planner can break this into the recommended 6-wave structure (CONTEXT.md Claude's Discretion ordering) or compress to 4-5 waves by combining doc/tutorial waves. Suggested dependency graph:

- **Wave 1** (foundation, no deps): TokenType.BeatLiteral + lexer suffix branches + lexer-only xUnit + PragmaScanner hyphen-acceptance verification.
- **Wave 2** (depends on Wave 1): BeatLiteralExpression AST record + Parser ParsePrimary arm + parser xUnit + literal-token-set update.
- **Wave 3** (depends on Wave 1, parallel with Wave 2): PragmaRegistry entry + ExecutionContext.BeatTrueToSig field + FlowEngine.ApplyBeatTrueToSigPragma + ModuleLoader push/pop + pragma xUnit.
- **Wave 4** (depends on Waves 2+3): ExpressionEvaluator.EvaluateBeatLiteral switch arm + multiplier formula xUnit Theory grid (Signal 4).
- **Wave 5** (depends on Wave 3): (beat N) RegisterContextDependent migration + Phase 26.1 DICT-01 regression xUnit + constructor multiplier xUnit.
- **Wave 6** (depends on all): examples/beat/intro.flow + cut-time.flow + CLAUDE.md update + .planning/REQUIREMENTS.md REQ-BEAT-NN entries.
