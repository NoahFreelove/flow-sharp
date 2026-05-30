---
phase: 45-beat-literal-syntax-true-to-sig-pragma
status: passed
nyquist_compliant: true
ships: Nb-beat-literal + beat-true-to-sig-pragma + (beat N)-constructor-migration + 2-composer-tutorials
production_code_changes: lexer (TokenType + 2 suffix branches + PragmaScanner hyphen) + BeatLiteralExpression AST + Parser arm + ExpressionEvaluator switch arm + ExecutionContext.BeatTrueToSig + FlowEngine.ApplyBeatTrueToSigPragma + ModuleLoader save-set-restore + BeatConstructorFunctions.RegisterContextDependent + str(Beat) overload + ProcDeclaration.IsBeatTrueToSig per-proc capture + Interpreter push/pop
date: 2026-05-29
plans_complete: 6
plans_total: 6
requirements:
  - REQ-BEAT-LEX-01
  - REQ-BEAT-LEX-02
  - REQ-BEAT-LEX-03
  - REQ-BEAT-LEX-04
  - REQ-BEAT-PRAGMA-HYPHEN-01
  - REQ-BEAT-AST-01
  - REQ-BEAT-AST-02
  - REQ-BEAT-AST-03
  - REQ-BEAT-AST-04
  - REQ-BEAT-PRAGMA-01
  - REQ-BEAT-PRAGMA-02
  - REQ-BEAT-PRAGMA-03
  - REQ-BEAT-PRAGMA-04
  - REQ-BEAT-CONSTRUCTOR-01
  - REQ-BEAT-CONSTRUCTOR-02
  - REQ-BEAT-TEST-01
  - REQ-BEAT-TEST-02
  - REQ-BEAT-TEST-03
  - REQ-BEAT-TEST-04
  - REQ-BEAT-TEST-05
  - REQ-BEAT-TEST-06
  - REQ-BEAT-TEST-07
  - REQ-BEAT-DOC-01
  - REQ-BEAT-DOC-02
  - REQ-BEAT-DOC-03
  - REQ-BEAT-DOC-04
phase_45_fixture_count: 66
flow_smoke_scripts: 4
tutorial_files: 2
committed_baselines: 2
---

# Phase 45: Beat Literal Syntax & True-to-Sig Pragma — Verification

**Verified:** 2026-05-29
**Status:** CLOSED — all 26 REQ-BEAT-NN requirements shipped; 66/66 Phase 45 fixtures GREEN; zero regression to Phase 44 strict (275/275) or Phase 26.1 DICT (33/33).
**Plans completed:** 6 / 6 (45-01 + 45-02 + 45-03 + 45-04 + 45-05 + 45-06 this closer)
**Branch:** sequential executor on `dev`.

## Closure Summary

Phase 45 closed the Beat-ergonomics gap Phase 43 left open. Composers can now write Beat values with a first-class `Nb` literal (`0.5b` / `2b` / `-1b`) — matching the rest of the music-type family (`100ms` / `2.5s` / `+50c` / `+2st` / `440Hz`) — and opt into the `enable beat-true-to-sig;` file pragma that retunes both the literal and the `(beat N)` constructor to the active time signature's beat unit.

- **Wave 1 (45-01):** Lexer foundation — `TokenType.BeatLiteral` + signed/unsigned suffix branches + PragmaScanner hyphen-gap closure.
- **Wave 2 (45-02 + 45-03):** `BeatLiteralExpression` AST + Parser arm; pragma plumbing (`PragmaRegistry` + `ExecutionContext.BeatTrueToSig` + `FlowEngine.ApplyBeatTrueToSigPragma` + `ModuleLoader` save-set-restore).
- **Wave 3 (45-04 + 45-05):** `EvaluateBeatLiteral` switch arm + multiplier formula + `str(Beat)` overload; `(beat N)` constructor migration to `RegisterContextDependent` + DICT-01 regression.
- **Wave 4 (45-06, this closer):** Cross-file boundary pair + 2 composer tutorials + committed WAV baselines + CLAUDE.md edits + tracking sweep + this deliverable. **Plus a Rule 1 fix** that was load-bearing for the cross-file boundary (see §5).

After this phase:

- `Beat b = 0.5b` works at expression-start, as a function argument, in flow-op chains, as an arithmetic operand, and as a tuple element.
- `enable beat-true-to-sig;` + `timesig 6/8 { Beat b = 1b }` constructs `Value.Beat(0.5)` (one eighth); `timesig 2/2` constructs `Value.Beat(2.0)` (one half); default 4/4 is identity.
- A pragma-OFF helper proc's `(beat N)` stays raw quarters even when called from a pragma-ON file (file-scope per D-04 / Pitfall 3).
- `(str someBeat)` prints the plain quarter-relative double — no `b` suffix (D-14 round-trip lock).

## §1 Phase 45 Requirements Closure Table

| Req ID | Description | Plan | Evidence | Status |
|--------|-------------|------|----------|--------|
| REQ-BEAT-LEX-01 | `TokenType.BeatLiteral` enum case | 45-01 | `BeatLiteralParserTests` (lex Facts) | ✅ |
| REQ-BEAT-LEX-02 | Unsigned `Nb` lexes via `ScanNumberOrSpecialLiteral` w/ identifier-guard | 45-01 | `BeatLiteralParserTests.Lexes_*` + `tests/test_beat_literal.flow` | ✅ |
| REQ-BEAT-LEX-03 | Signed `+Nb`/`-Nb` lexes via `TryLexTypedLiteral`; negatives accepted (D-08) | 45-01 | `BeatTrueToSigPragmaTests.MultiplierFormula_NegativePassthrough` | ✅ |
| REQ-BEAT-LEX-04 | 7 lexer-shape Facts (accept + reject identifier collisions) | 45-01 | `BeatLiteralParserTests` (7 lex Facts) | ✅ |
| REQ-BEAT-PRAGMA-HYPHEN-01 | `PragmaScanner` accepts hyphens in pragma identifiers | 45-01 | `PragmaScannerHyphenTests` (4 Facts) | ✅ |
| REQ-BEAT-AST-01 | `BeatLiteralExpression(SourceLocation, double RawValue, Span?)` own record | 45-02 | `BeatLiteralParserTests.AstShape*` | ✅ |
| REQ-BEAT-AST-02 | Parser emits `BeatLiteralExpression` (raw double survives to eval) | 45-02 | `BeatLiteralParserTests.AstShapeAssignedToVariable` | ✅ |
| REQ-BEAT-AST-03 | `IsArgumentStart` + tuple-close token-sets extended w/ BeatLiteral | 45-02 | `BeatLiteralParserTests.AstShapeAsFunctionArg` / `AstShapeInTuple` | ✅ |
| REQ-BEAT-AST-04 | `EvaluateBeatLiteral` switch arm + multiplier formula | 45-04 | `BeatTrueToSigPragmaTests.MultiplierFormula_*` (13 Facts) | ✅ |
| REQ-BEAT-PRAGMA-01 | `PragmaRegistry["beat-true-to-sig"]` D-03 verbatim | 45-03 | `BeatTrueToSigPragmaTests.PragmaRegistryEntry` / `LevenshteinSuggestion` | ✅ |
| REQ-BEAT-PRAGMA-02 | `ExecutionContext.BeatTrueToSig` single bool field | 45-03 | `BeatTrueToSigPragmaTests.BeatTrueToSig_DefaultsFalse` / `_Settable` | ✅ |
| REQ-BEAT-PRAGMA-03 | `FlowEngine.ApplyBeatTrueToSigPragma` helper + Execute call | 45-03 | `BeatTrueToSigPragmaTests.PragmaSetsContextBit` / `AbsenceLeavesBitFalse` | ✅ |
| REQ-BEAT-PRAGMA-04 | `ModuleLoader` save-set-restore (finally, file-scope) | 45-03 | `BeatTrueToSigPragmaTests.CrossFileRestore*` (4 Facts) | ✅ |
| REQ-BEAT-CONSTRUCTOR-01 | `(beat N)` → `RegisterContextDependent` pragma-aware | 45-05 | `BeatConstructorTests.BeatConstructor_PragmaOn_*` | ✅ |
| REQ-BEAT-CONSTRUCTOR-02 | DICT-01 Tuple-of-hashables key regression (pragma × timesig) | 45-05 | `BeatConstructorTests.Dict01Regression_*` | ✅ |
| REQ-BEAT-TEST-01 | `tests/test_beat_literal.flow` lex+parse+eval smoke | 45-04 | script exit 0 + PASSED | ✅ |
| REQ-BEAT-TEST-02 | `tests/test_beat_pragma_off.flow` identity across timesigs | 45-04 | script exit 0 + PASSED | ✅ |
| REQ-BEAT-TEST-03 | `tests/test_beat_pragma_on.flow` multiplier matrix | 45-04 | script exit 0 + PASSED | ✅ |
| REQ-BEAT-TEST-04 | Cross-file pragma-on entry + pragma-off helper pair | 45-06 | `BeatTrueToSigPragmaTests.CrossFileSmokeFact` + `tests/test_beat_cross_file{,_helper}.flow` | ✅ |
| REQ-BEAT-TEST-05 | 13 multiplier-matrix xUnit Facts | 45-04 | `BeatTrueToSigPragmaTests.MultiplierFormula_*` | ✅ |
| REQ-BEAT-TEST-06 | 4 `(str Beat)` round-trip Facts + 9 constructor/DICT Facts | 45-05 + 45-06 | `BeatTrueToSigPragmaTests.Str*` + `BeatConstructorTests` | ✅ |
| REQ-BEAT-TEST-07 | Two-run cmp-clean tutorial WAVs + committed baselines | 45-06 | `BeatTrueToSigPragmaTests.TutorialTwoRunCmpClean_*` / `TutorialMatchesBaseline_*` | ✅ |
| REQ-BEAT-DOC-01 | CLAUDE.md Music Types table — D-13 `0.5b` row REPLACE | 45-06 | `grep '0.5b.*Beat literal' CLAUDE.md` | ✅ |
| REQ-BEAT-DOC-02 | CLAUDE.md Music-Specific pragma family bullet | 45-06 | `grep 'beat-true-to-sig' CLAUDE.md` | ✅ |
| REQ-BEAT-DOC-03 | `examples/beat/intro.flow` 6/8 jig tutorial | 45-06 | script exit 0 + PASSED + WAV/MIDI | ✅ |
| REQ-BEAT-DOC-04 | `examples/beat/cut-time.flow` 2/2 cut-time tutorial | 45-06 | script exit 0 + PASSED + WAV/MIDI | ✅ |

## §2 xUnit Fact Counts

**Total Phase 45 fixtures: 66 GREEN** (across 4 test files + 1 category constant class):

| File | Facts | Coverage |
|------|-------|----------|
| `BeatLiteralParserTests.cs` | 21 | 7 lexer-shape (REQ-BEAT-LEX-01..04) + 5 AST-shape (REQ-BEAT-AST-01..03) + 9 supporting |
| `PragmaScannerHyphenTests.cs` | 4 | hyphen-gap closure (REQ-BEAT-PRAGMA-HYPHEN-01) |
| `BeatTrueToSigPragmaTests.cs` | 27 | 6 registry/context + 4 cross-file restore (PRAGMA-01..04) + 13 multiplier matrix (AST-04 / TEST-05) + 4 str round-trip (TEST-06) + 1 cross-file smoke (TEST-04) + 4 tutorial (TEST-07) — note Theory rows expand the raw `[Fact]`/`[Theory]` declaration count |
| `BeatConstructorTests.cs` | 7 declarations → 9 Theory-expanded | 4 constructor multiplier (CONSTRUCTOR-01) + 3 DICT-01 (CONSTRUCTOR-02) |
| `Phase45TestCategory.cs` | 0 | category constant |

Regression baselines preserved: Phase 44 strict **275/275 GREEN** (the shared per-proc `BeatTrueToSig` push/pop introduced zero strict regression); Phase 26.1 DICT **33/33 GREEN**; Phase 43 Beat conversion/companion **12/12 GREEN**.

## §3 .flow Smoke Inventory

**4 composer-facing smoke scripts** (runner `for t in tests/test_beat_*.flow; do dotnet run --project flow-interpreter "$t"; done` — all exit 0 with `PASSED`):

| Script | Purpose |
|--------|---------|
| `tests/test_beat_literal.flow` | lex+parse+eval of `0.5b`/`2b`/`1b`/`-2b` |
| `tests/test_beat_pragma_off.flow` | identity (`1b = 1`) across 4/4 / 6/8 / 2/2 |
| `tests/test_beat_pragma_on.flow` | multiplier matrix across 4/4 / 6/8 / 2/2 / 5/4 / 7/8 |
| `tests/test_beat_cross_file.flow` + `_helper.flow` | cross-file boundary (pragma-on entry imports pragma-off helper; helper's `(beat 1)` = raw 1.0) |

**2 tutorial files** (run end-to-end, render MIDI + WAV, print PASSED):

| Tutorial | Demonstrates |
|----------|-------------|
| `examples/beat/intro.flow` | 6/8 jig — 4/4 identity vs 6/8 `1b = eighth`; renders a jig melody + bass to `/tmp/beat_intro.{wav,mid}` |
| `examples/beat/cut-time.flow` | 2/2 cut time — `1b = half`, `0.5b = quarter`; renders a march to `/tmp/beat_cut_time.{wav,mid}` |

## §4 Two-Run Cmp-Clean Evidence

Phase 45 adds NO PRNG sites (no `granular`/`markov`/`lsystem`/`jam`); tutorial WAVs are pure deterministic synthesis. Both tutorials produce byte-identical WAV across two runs (verified at closer; SHA-256 over file bytes):

| Tutorial | WAV | SHA-256 (committed baseline) |
|----------|-----|------------------------------|
| `examples/beat/intro.flow` | `flow-lang.Tests/baselines/Phase45/intro.wav` | `d401374c2f84bd142a8af85ace98e1ad2e580316118a25b1d78d9f6455fb3394` |
| `examples/beat/cut-time.flow` | `flow-lang.Tests/baselines/Phase45/cut-time.wav` | `d3e0e832c5c17d1943986036bcbe0093a2e5c30c7c2ca9306e063886d054362d` |

**Matching policy:** raw SHA-256 byte equality (NOT RMS-windowed tolerance) — Phase 45 has no stochastic compute, so exact determinism is the contract. `TutorialTwoRunCmpClean_*` Facts SHA-equate two consecutive renders; `TutorialMatchesBaseline_*` Facts SHA-equate a fresh render against the committed baseline. Per the T-45-15 disposition (`accept`): if a FUTURE phase legitimately changes synth output (Phase 28 articulation-rewrite precedent), that phase's plan regenerates these Phase 45 baselines.

## §5 Known Caveats

### Cross-file boundary required a Rule 1 fix (Plan 45-06)

The plan's load-bearing must-have truth — *"calling `(bumpBeat (beat 0))` from the entry returns `Value.Beat(1.0)` regardless of entry's 6/8 timesig"* — was **broken at plan-spawn**. Plan 45-03 wired the pragma bit through `ModuleLoader`'s file-LOAD save-set-restore, but NOT through proc-INVOCATION. Because `(beat N)` is a `RegisterContextDependent` builtin reading the LIVE `ctx.BeatTrueToSig`, a helper proc declared in a pragma-off file but invoked from a pragma-on file read the caller's (wrong) bit — the helper's `(beat 1)` returned 0.5, not 1.0.

**Fixed (Rule 1):** Added `ProcDeclaration.IsBeatTrueToSig` (parse-time capture from the declaring file's `PragmaSet`, mirroring Phase 44 `ProcDeclaration.IsStrict`) + per-proc push/pop in `Interpreter.ExecuteUserFunctionWithCaptures` (same try/finally as the strict-bit push/pop) + lexical capture on synthetic lambda ProcDeclarations in `EvaluateLambda`. This is the EXACT pattern Phase 44 established for strict mode — not an architectural change. Verified: `tests/test_beat_cross_file.flow` now prints `helper (beat 1) ... = 1`. Phase 44 strict 275/275 GREEN confirms the shared proc-execution-path change did not regress strict.

### `(str Beat)` round-trip lock (D-14) reconfirmed

`(str someBeat)` emits the plain quarter-relative double (no `b` suffix) in every mode. This is a deliberate lock, NOT an omission: emitting `"0.5b"` would break round-trip under the pragma (re-parsing `"0.25b"` in 6/8 re-multiplies to 0.125). Deferred per D-14 — a literal-form printer (`strFull`?) ships in a one-commit follow-up only if a composer reports needing it.

### Deferred ideas re-tracked for v1.6

Per CONTEXT.md decisions, these stay deferred (no Phase 45 implementation): `(beatRaw N)` escape hatch (D-05), `(str)` `"0.5b"` literal-form printing (D-14), REPL `:beat-true-to-sig on/off` sticky toggle (D-15), dotted-rhythm `Nb.` syntax (D-17 — composers write `0.75b` directly), tied-Beat-literal `Nb~` (deferred indefinitely). D-16 (Phase 44 strict interaction) is documentary carry-forward — strict's Axis A already covers `Nb` as the canonical Beat form in strict files; no Phase 45 implementation task.

## §6 Phase 45 Metrics

| Plan | Tasks | Commits | Net surface |
|------|-------|---------|-------------|
| 45-01 | 2 | `d6d0731` / `fffd82f` | TokenType + 2 lexer branches + PragmaScanner hyphen + 7 lex Facts |
| 45-02 | 1 | `121eb30` | BeatLiteralExpression AST + Parser arm + 5 AST Facts (+ Rule 1 tuple-close fix) |
| 45-03 | 2 | `7372ce3` / `84df903` | PragmaRegistry + ExecutionContext field + FlowEngine helper + ModuleLoader save-set-restore + 10 Facts |
| 45-04 | 2 | `8ec7145` / `d62c64d` | EvaluateBeatLiteral + str(Beat) overload + 13 Facts + 3 smokes (+ 2 stale-test Rule 1 fixes) |
| 45-05 | 1 | `5fe8566` | BeatConstructorFunctions.RegisterContextDependent + 9 Facts |
| 45-06 | 3 | `4a0a041` / `308c37a` / (this closer) | ProcDeclaration.IsBeatTrueToSig + cross-file pair + 4 str Facts + cross-file smoke + 2 tutorials + 2 baselines + 4 tutorial Facts + CLAUDE.md + tracking sweep + this deliverable |

**Totals:** 6 plans, 11 tasks, 66 Phase 45 xUnit Facts GREEN, 4 `.flow` smokes, 2 tutorials, 2 committed WAV baselines, 26 REQ-BEAT-NN closed. Zero new NuGet packages. Two-run cmp-clean preserved.

## Verification commands (reproducibility)

```bash
# Phase 45 xUnit fixtures (66 Facts)
dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase45" --no-build

# Phase 44 strict regression (shared per-proc push/pop change)
dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase44" --no-build

# 4 composer .flow smokes
for t in tests/test_beat_*.flow; do dotnet run --project flow-interpreter "$t"; done

# 2 tutorials (render WAV + MIDI)
dotnet run --project flow-interpreter examples/beat/intro.flow
dotnet run --project flow-interpreter examples/beat/cut-time.flow

# Two-run cmp-clean (baselines committed)
sha256sum flow-lang.Tests/baselines/Phase45/intro.wav flow-lang.Tests/baselines/Phase45/cut-time.wav

# Doc grep pins
grep -c "0.5b" CLAUDE.md
grep -c "beat-true-to-sig" CLAUDE.md
grep -c "REQ-BEAT-LEX-01" .planning/REQUIREMENTS.md
```
