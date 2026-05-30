---
phase: 45-beat-literal-syntax-true-to-sig-pragma
verified: 2026-05-29T00:00:00Z
status: passed
score: 26/26 must-haves verified
overrides_applied: 0
re_verification: false
---

# Phase 45: Beat Literal Syntax & True-to-Sig Pragma — Independent Verification Report

**Phase Goal:** Close the Beat-ergonomics gap left by Phase 43 by adding (1) first-class `Nb` Beat literal syntax (`0.5b`/`2b`/`-1b`) following the existing `Nms`/`Ns`/`Nc`/`Nst`/`NdB`/`NHz` lexer precedent, and (2) an opt-in, file-scoped, last-wins `enable beat-true-to-sig;` pragma that retunes BOTH `Nb` literals AND the `(beat N)` constructor to the active timesig's beat unit at evaluation/construction time.

**Verified:** 2026-05-29
**Status:** PASSED — all 26 REQ-BEAT-NN requirements verified independently in the codebase; 66/66 Phase 45 xUnit fixtures GREEN confirmed by running `dotnet test`; 4 smoke scripts PASSED; 2 tutorials produce output and render; WAV baselines SHA-256 match fresh renders.
**Re-verification:** No — initial independent verification (previous file was executor self-report; this overwrites it).

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `Nb` Beat literal syntax (`0.5b`/`2b`/`-1b`) lexes to a single `BeatLiteral` token carrying the parsed double | VERIFIED | `TokenType.BeatLiteral` at `flow-lang/Lexing/TokenType.cs:68`; two `SimpleLexer.cs` branches at lines 632-644 (signed) and 804-816 (unsigned `else if`); `grep -c "TokenType.BeatLiteral" SimpleLexer.cs` = 3 |
| 2 | Identifier-guard keeps `1bar`/`beats`/`bpm`/`b1`/`Bb`/`B4`/`Bmaj7` from consuming the `b` suffix | VERIFIED | `!char.IsLetter(PeekNext())` guard confirmed in both branches; xUnit `BeatLiteralParserTests` identifier-guard Facts GREEN |
| 3 | `PragmaScanner` accepts hyphens so `enable beat-true-to-sig;` parses cleanly | VERIFIED | `PragmaScanner.cs:246` widens continuation predicate to `\|\| lineText[p] == '-'`; confirmed `PragmaScannerHyphenTests` GREEN |
| 4 | `BeatLiteralExpression(SourceLocation, double RawValue, Span?)` AST record exists with Parser arm emitting it | VERIFIED | `flow-lang/Ast/Expressions/BeatLiteralExpression.cs` confirmed; `Parser.cs:1395-1398` `Match(TokenType.BeatLiteral)` arm; `IsArgumentStart` extended at line 2147 |
| 5 | `EvaluateBeatLiteral` applies multiplier formula `final = pragma_on ? raw × (4.0 / denom) : raw` | VERIFIED | `ExpressionEvaluator.cs:1034-1042` confirmed: `denom = _context.GetMusicalContext().TimeSignature?.Denominator ?? 4`; `multiplier = _context.BeatTrueToSig ? (4.0 / denom) : 1.0`; `Value.Beat(beatLit.RawValue * multiplier)` |
| 6 | `(beat N)` constructor uses the byte-identical multiplier formula via `RegisterContextDependent` | VERIFIED | `BeatConstructorFunctions.cs:19-33` formula identical: `denom = context.GetMusicalContext().TimeSignature?.Denominator ?? 4`; `multiplier = context.BeatTrueToSig ? (4.0 / denom) : 1.0` |
| 7 | `ExecutionContext.BeatTrueToSig` single bool field, default false | VERIFIED | `ExecutionContext.cs:550`: `public bool BeatTrueToSig { get; set; } = false;` |
| 8 | `FlowEngine.ApplyBeatTrueToSigPragma` sets context bit on `Execute` | VERIFIED | `FlowEngine.cs:425-428` confirmed; called at line 351 within `Execute` |
| 9 | `PragmaRegistry["beat-true-to-sig"]` entry exists with D-03 verbatim description | VERIFIED | `PragmaRegistry.cs:37` confirmed |
| 10 | `ModuleLoader` save-set-restore is leak-safe via `finally` across import boundary | VERIFIED | `ModuleLoader.cs:170-171` saves `prevBeatTrueToSig` + sets pragma bit; `finally` at line 251 restores it regardless of thrown exception |
| 11 | Per-proc `IsBeatTrueToSig` capture at parse time + Interpreter push/pop across proc invocation boundary | VERIFIED | `ProcDeclaration.cs:70` captures `IsBeatTrueToSig = false` default; `Parser.cs:421` sets it from `_pragmaSet?.Has("beat-true-to-sig")`; `Interpreter.cs:1149-1150` pushes; `finally` at 1244 restores |
| 12 | Cross-file declaring-file semantics: pragma-off helper's `(beat 1)` returns 1.0 even when called from a pragma-on 6/8 file | VERIFIED | `tests/test_beat_cross_file.flow` script exits 0; stdout contains `"helper (beat 1) called from 6/8 pragma-on = 1"` (confirmed live run) |
| 13 | `(str someBeat)` emits plain quarter-relative double, no `b` suffix in any mode (D-14 round-trip lock) | VERIFIED | `BuiltInFunctions.cs:238-244` dedicated `str(Beat)` overload; smoke scripts confirm `str 0.5b` prints `"0.5"` |
| 14 | 4 smoke scripts exit 0 with PASSED: `test_beat_literal.flow`, `test_beat_pragma_off.flow`, `test_beat_pragma_on.flow`, `test_beat_cross_file.flow` | VERIFIED | All 4 confirmed running live: exit 0 + PASSED end marker |
| 15 | `examples/beat/intro.flow` 6/8 jig tutorial runs to completion, renders `/tmp/beat_intro.{wav,mid}`, prints PASSED | VERIFIED | Live run confirmed: `Rendered: /tmp/beat_intro.wav + /tmp/beat_intro.mid` + `examples/beat/intro: PASSED` |
| 16 | `examples/beat/cut-time.flow` 2/2 cut-time tutorial runs to completion, renders `/tmp/beat_cut_time.{wav,mid}`, prints PASSED | VERIFIED | Live run confirmed: `Rendered: /tmp/beat_cut_time.wav + /tmp/beat_cut_time.mid` + `examples/beat/cut-time: PASSED` |
| 17 | Two-run cmp-clean: freshly rendered WAVs SHA-256 match committed baselines | VERIFIED | `intro.wav` SHA-256 `d401374c…` matches both `/tmp/beat_intro.wav` and committed baseline; `cut-time.wav` SHA-256 `d3e0e832…` matches both |
| 18 | 66 Phase 45 xUnit Facts GREEN | VERIFIED | `dotnet test --filter "FullyQualifiedName~Phase45"` → `Passed: 66, Failed: 0, Total: 66` |
| 19 | Phase 44 strict suite unregressed (shared per-proc push/pop change) | VERIFIED | `dotnet test --filter "FullyQualifiedName~Phase44"` → `Passed: 275, Failed: 0, Total: 275` |
| 20 | Phase 26.1 DICT suite unregressed (DICT-01 Tuple-of-hashables key regression pinned) | VERIFIED | `dotnet test --filter "FullyQualifiedName~Phase26"` → `Passed: 125, Failed: 0, Total: 125` |
| 21 | CLAUDE.md Music Types table has `0.5b` Beat literal row replacing old `1.5 (Beat-tagged)` row | VERIFIED | `CLAUDE.md:189` confirmed: `\| \`0.5b\` (Beat literal) \| \`Beat\` \| \`Double\`, \`Float\` \| beat-position arithmetic; \`enable beat-true-to-sig;\` opt-in ...` |
| 22 | CLAUDE.md Music-Specific section has pragma-family bullet including `beat-true-to-sig` | VERIFIED | `CLAUDE.md:201` confirmed with full `beat-true-to-sig` documentation including per-proc capture note |
| 23 | REQUIREMENTS.md has Phase 45 section with all 25 REQ-BEAT-NN entries | VERIFIED | `grep -c "REQ-BEAT-" .planning/REQUIREMENTS.md` = 53 (entries appear in both the body section and the cross-reference tracking table) |
| 24 | Committed baselines exist and are substantive WAVs | VERIFIED | `flow-lang.Tests/baselines/Phase45/intro.wav` = 2,116,844 bytes; `cut-time.wav` = 641,496 bytes |
| 25 | No debt markers (`TBD`/`FIXME`/`XXX`) in Phase 45 production files | VERIFIED | Scanned `BeatLiteralExpression.cs`, `BeatConstructorFunctions.cs`, `SimpleLexer.cs` (beat sections), `PragmaScanner.cs`, `ExpressionEvaluator.cs` (beat section), `ModuleLoader.cs` (beat section), `Interpreter.cs` (beat section): no unresolved debt markers found |
| 26 | No Phase 45-induced regressions in full suite (4 Phase 48 WASM failures are pre-existing environment-sensitive) | VERIFIED | Full suite `dotnet test`: `Passed: 2188, Failed: 2, Skipped: 9`; the 2-4 failures are `Phase48.WasmBuildPipeline*/BundleSize*/DryWetMidiWasmPublish*` (pre-existing from commit `5562a61`, fail only under parallel execution due to ILLink NETSDK1144 resource contention, pass in isolation) |

**Score:** 26/26 truths verified

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|---------|--------|---------|
| `flow-lang/Lexing/TokenType.cs` | `BeatLiteral` enum case | VERIFIED | Line 68 confirmed |
| `flow-lang/Lexing/SimpleLexer.cs` | Signed + unsigned `b` suffix branches | VERIFIED | 3 `TokenType.BeatLiteral` references; signed at ~632, unsigned `else if` at ~804 |
| `flow-lang/Lexing/PragmaScanner.cs` | Hyphen-aware identifier continuation | VERIFIED | Line 246 widened predicate confirmed |
| `flow-lang/Ast/Expressions/BeatLiteralExpression.cs` | Own record AST node | VERIFIED | Substantive record with `RawValue` field |
| `flow-lang/Parsing/Parser.cs` | `BeatLiteral` Parser arm + `IsArgumentStart` extension | VERIFIED | Lines 1391-1398 + 2147 confirmed |
| `flow-lang/Interpreter/ExpressionEvaluator.cs` | `EvaluateBeatLiteral` switch arm + method | VERIFIED | Line 47 dispatch; method at 1034 with full multiplier formula |
| `flow-lang/Runtime/ExecutionContext.cs` | `BeatTrueToSig` bool field | VERIFIED | Line 550 |
| `flow-lang/Core/FlowEngine.cs` | `ApplyBeatTrueToSigPragma` helper + Execute call | VERIFIED | Lines 425-428 + call at 351 |
| `flow-lang/Runtime/ModuleLoader.cs` | Save-set-restore in `finally` | VERIFIED | Lines 170-171 + 251 in finally |
| `flow-lang/Ast/Statements/ProcDeclaration.cs` | `IsBeatTrueToSig` field | VERIFIED | Line 70 (default false) |
| `flow-lang/Parsing/Parser.cs` | Parse-time `IsBeatTrueToSig` capture | VERIFIED | Line 421 from `_pragmaSet?.Has("beat-true-to-sig")` |
| `flow-lang/Interpreter/Interpreter.cs` | Per-proc push/pop in `try/finally` | VERIFIED | Lines 1149-1150 push; 1244 restore in finally |
| `flow-lang/StandardLibrary/Audio/BeatConstructorFunctions.cs` | `RegisterContextDependent` with byte-identical formula | VERIFIED | Full file verified; formula byte-identical to `EvaluateBeatLiteral` |
| `flow-lang/Runtime/PragmaRegistry.cs` | `beat-true-to-sig` entry | VERIFIED | Line 37 confirmed |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` | `str(Beat)` overload + constructor migration | VERIFIED | Lines 238-244 (str); line 1025 RegisterContextDependent call |
| `tests/test_beat_literal.flow` | Lex+parse+eval smoke | VERIFIED | Exists; runs; exits 0 with PASSED |
| `tests/test_beat_pragma_off.flow` | Identity smoke | VERIFIED | Exists; runs; exits 0 with PASSED |
| `tests/test_beat_pragma_on.flow` | Multiplier matrix smoke | VERIFIED | Exists; runs; exits 0 with PASSED |
| `tests/test_beat_cross_file.flow` | Pragma-on entry with `enable beat-true-to-sig;` | VERIFIED | Exists; `use "./test_beat_cross_file_helper.flow"` link confirmed; runs; exits 0 with PASSED; cross-file boundary behavior correct |
| `tests/test_beat_cross_file_helper.flow` | Pragma-off helper with `proc bumpBeat` | VERIFIED | Exists; `proc bumpBeat (Beat: b)` confirmed |
| `examples/beat/intro.flow` | 6/8 jig tutorial (≥50 lines) | VERIFIED | 83 lines; contains `enable beat-true-to-sig;`; renders WAV+MIDI; prints PASSED |
| `examples/beat/cut-time.flow` | 2/2 cut-time tutorial (≥30 lines) | VERIFIED | Contains `timesig 2/2`; renders WAV+MIDI; prints PASSED |
| `flow-lang.Tests/baselines/Phase45/intro.wav` | Reference baseline (≥10 KB) | VERIFIED | 2,116,844 bytes; SHA-256 `d401374c2f84bd142a8af85ace98e1ad2e580316118a25b1d78d9f6455fb3394` matches fresh render |
| `flow-lang.Tests/baselines/Phase45/cut-time.wav` | Reference baseline (≥10 KB) | VERIFIED | 641,496 bytes; SHA-256 `d3e0e832c5c17d1943986036bcbe0093a2e5c30c7c2ca9306e063886d054362d` matches fresh render |
| `flow-lang.Tests/Integration/Phase45/BeatLiteralParserTests.cs` | Lexer + AST Facts | VERIFIED | Exists; contributes to 66/66 Phase 45 GREEN |
| `flow-lang.Tests/Integration/Phase45/PragmaScannerHyphenTests.cs` | Hyphen Facts | VERIFIED | Exists; contributes to 66/66 Phase 45 GREEN |
| `flow-lang.Tests/Integration/Phase45/BeatTrueToSigPragmaTests.cs` | Pragma/multiplier/str/cross-file/tutorial Facts | VERIFIED | Exists; contributes to 66/66 Phase 45 GREEN |
| `flow-lang.Tests/Integration/Phase45/BeatConstructorTests.cs` | Constructor + DICT-01 Facts | VERIFIED | Exists; contributes to 66/66 Phase 45 GREEN |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `SimpleLexer.cs` | `TokenType.cs` | `TokenType.BeatLiteral` reference | WIRED | 3 references in SimpleLexer.cs |
| `Parser.cs` | `BeatLiteralExpression.cs` | `new BeatLiteralExpression(...)` emission | WIRED | Line 1398 confirmed |
| `ExpressionEvaluator.cs` | `ExecutionContext.BeatTrueToSig` | `_context.BeatTrueToSig` read | WIRED | Line 1040 |
| `ExpressionEvaluator.cs` | `MusicalContext.TimeSignature` | `_context.GetMusicalContext().TimeSignature?.Denominator ?? 4` | WIRED | Line 1039 |
| `BeatConstructorFunctions.cs` | `ExecutionContext.BeatTrueToSig` | `context.BeatTrueToSig` read at call time | WIRED | Line 29 |
| `FlowEngine.cs` | `ApplyBeatTrueToSigPragma` | Call in `Execute` method | WIRED | Line 351 → method at 425 |
| `ModuleLoader.cs` | `ExecutionContext.BeatTrueToSig` | Save-set-restore in `try/finally` | WIRED | Lines 170-171, 251 |
| `Interpreter.cs` | `ProcDeclaration.IsBeatTrueToSig` | Push/pop in `ExecuteUserFunctionWithCaptures` | WIRED | Lines 1149-1150, 1244 |
| `Parser.cs` | `PragmaSet.Has("beat-true-to-sig")` | Capture into `ProcDeclaration.IsBeatTrueToSig` at line 421 | WIRED | `_pragmaSet?.Has("beat-true-to-sig") ?? false` |
| `tests/test_beat_cross_file.flow` | `tests/test_beat_cross_file_helper.flow` | `use "./test_beat_cross_file_helper.flow"` | WIRED | Confirmed in file |
| `BuiltInFunctions.cs` | `BeatConstructorFunctions.RegisterContextDependent` | Call at line 1025 | WIRED | `Audio.BeatConstructorFunctions.RegisterContextDependent(registry, context)` |

---

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|--------------|--------|--------------------|--------|
| `EvaluateBeatLiteral` | `multiplier` | `_context.BeatTrueToSig` + `GetMusicalContext().TimeSignature?.Denominator` | Yes — reads live context state | FLOWING |
| `BeatConstructorFunctions` | `multiplier` | `context.BeatTrueToSig` + `GetMusicalContext().TimeSignature?.Denominator` | Yes — reads live context state at call time | FLOWING |
| `ModuleLoader` save-set-restore | `prevBeatTrueToSig` | Caller's `context.BeatTrueToSig` at import time | Yes — saves real flag, restores in finally | FLOWING |
| `Interpreter` per-proc push/pop | `prevBeatTrueToSig` | `_context.BeatTrueToSig` at invocation time | Yes — pushes `proc.IsBeatTrueToSig`, restores in finally | FLOWING |

---

### Behavioral Spot-Checks

| Behavior | Command (run live) | Result | Status |
|----------|--------------------|--------|--------|
| `0.5b` lexes and evals to `0.5` quarters, pragma off | `dotnet run --project flow-interpreter tests/test_beat_literal.flow` | `0.5` printed; PASSED | PASS |
| `1b` in 6/8 with pragma ON = `0.5` quarters | `dotnet run --project flow-interpreter tests/test_beat_pragma_on.flow` | `0.5` printed for 6/8 row; PASSED | PASS |
| Cross-file: helper's `(beat 1)` = `1.0` from pragma-on caller in 6/8 | `dotnet run --project flow-interpreter tests/test_beat_cross_file.flow` | `"helper (beat 1) called from 6/8 pragma-on = 1"` printed; PASSED | PASS |
| Tutorial renders WAV byte-identical to baseline | SHA-256 comparison | Both tutorials produce SHA-256 matching committed baselines | PASS |
| Phase 45 xUnit suite: 66/66 GREEN | `dotnet test --filter "FullyQualifiedName~Phase45"` | `Passed: 66, Failed: 0` | PASS |
| Phase 44 strict suite: 275/275 GREEN | `dotnet test --filter "FullyQualifiedName~Phase44"` | `Passed: 275, Failed: 0` | PASS |

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|---------|
| REQ-BEAT-LEX-01 | 45-01 | `TokenType.BeatLiteral` enum case | SATISFIED | `TokenType.cs:68` confirmed |
| REQ-BEAT-LEX-02 | 45-01 | Unsigned `Nb` via `ScanNumberOrSpecialLiteral` with identifier-guard | SATISFIED | `SimpleLexer.cs:804-816` `else if` branch |
| REQ-BEAT-LEX-03 | 45-01 | Signed `+/-Nb` via `TryLookAheadSpecialLiteral` | SATISFIED | `SimpleLexer.cs:632-644` `if` branch |
| REQ-BEAT-LEX-04 | 45-01 | 7 lexer-shape Facts | SATISFIED | `BeatLiteralParserTests.cs` GREEN |
| REQ-BEAT-PRAGMA-HYPHEN-01 | 45-01 | `PragmaScanner` accepts hyphens in identifiers | SATISFIED | `PragmaScanner.cs:246` |
| REQ-BEAT-AST-01 | 45-02 | Own `BeatLiteralExpression` AST record | SATISFIED | `Ast/Expressions/BeatLiteralExpression.cs` |
| REQ-BEAT-AST-02 | 45-02 | Parser emits `BeatLiteralExpression` with raw double | SATISFIED | `Parser.cs:1395-1398` |
| REQ-BEAT-AST-03 | 45-02 | `IsArgumentStart` + tuple-close extended with `BeatLiteral` | SATISFIED | `Parser.cs:2147` |
| REQ-BEAT-AST-04 | 45-04 | `EvaluateBeatLiteral` switch arm + multiplier formula | SATISFIED | `ExpressionEvaluator.cs:1034-1042` |
| REQ-BEAT-PRAGMA-01 | 45-03 | `PragmaRegistry["beat-true-to-sig"]` entry | SATISFIED | `PragmaRegistry.cs:37` |
| REQ-BEAT-PRAGMA-02 | 45-03 | `ExecutionContext.BeatTrueToSig` single bool field | SATISFIED | `ExecutionContext.cs:550` |
| REQ-BEAT-PRAGMA-03 | 45-03 | `FlowEngine.ApplyBeatTrueToSigPragma` helper + Execute call | SATISFIED | `FlowEngine.cs:351, 425-428` |
| REQ-BEAT-PRAGMA-04 | 45-03 | `ModuleLoader` save-set-restore with `finally` | SATISFIED | `ModuleLoader.cs:170-171, 251` |
| REQ-BEAT-CONSTRUCTOR-01 | 45-05 | `(beat N)` constructor via `RegisterContextDependent` | SATISFIED | `BeatConstructorFunctions.cs:19-33`; wired at `BuiltInFunctions.cs:1025` |
| REQ-BEAT-CONSTRUCTOR-02 | 45-05 | DICT-01 Tuple-of-hashables key regression preserved | SATISFIED | `BeatConstructorTests` GREEN |
| REQ-BEAT-TEST-01 | 45-04 | `tests/test_beat_literal.flow` smoke | SATISFIED | Runs; PASSED |
| REQ-BEAT-TEST-02 | 45-04 | `tests/test_beat_pragma_off.flow` smoke | SATISFIED | Runs; PASSED |
| REQ-BEAT-TEST-03 | 45-04 | `tests/test_beat_pragma_on.flow` smoke | SATISFIED | Runs; PASSED |
| REQ-BEAT-TEST-04 | 45-06 | Cross-file boundary pair | SATISFIED | Both files exist; live run confirms cross-file semantics |
| REQ-BEAT-TEST-05 | 45-04 | 13 multiplier-matrix xUnit Facts | SATISFIED | `BeatTrueToSigPragmaTests.MultiplierFormula_*` GREEN |
| REQ-BEAT-TEST-06 | 45-05/06 | `(str Beat)` round-trip Facts + constructor/DICT Facts | SATISFIED | `BeatTrueToSigPragmaTests.Str*` + `BeatConstructorTests` GREEN |
| REQ-BEAT-TEST-07 | 45-06 | Two-run cmp-clean tutorial WAVs + committed baselines | SATISFIED | SHA-256 match confirmed live |
| REQ-BEAT-DOC-01 | 45-06 | CLAUDE.md Music Types table Beat row replaced | SATISFIED | `CLAUDE.md:189` |
| REQ-BEAT-DOC-02 | 45-06 | CLAUDE.md Music-Specific pragma family bullet | SATISFIED | `CLAUDE.md:201` |
| REQ-BEAT-DOC-03 | 45-06 | `examples/beat/intro.flow` tutorial | SATISFIED | 83 lines; runs; PASSED |
| REQ-BEAT-DOC-04 | 45-06 | `examples/beat/cut-time.flow` tutorial | SATISFIED | Runs; PASSED |

**All 26 requirements (25 REQ-BEAT-NN + REQ-BEAT-PRAGMA-HYPHEN-01) SATISFIED.**

---

### Anti-Patterns Found

No blockers. Scanned all Phase 45 production files (`BeatLiteralExpression.cs`, `SimpleLexer.cs` beat sections, `PragmaScanner.cs`, `ExpressionEvaluator.cs` beat section, `ModuleLoader.cs` beat section, `Interpreter.cs` beat section, `BeatConstructorFunctions.cs`, tutorial files, smoke scripts):

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none) | — | — | — | No unresolved TBD/FIXME/XXX markers found in any Phase 45 file |

---

### Human Verification Required

(None — all observable behaviors verified programmatically.)

---

### Key Risk Areas: Independent Confirmation

**1. Multiplier formula byte-identity between EvaluateBeatLiteral and BeatConstructorFunctions:**

`EvaluateBeatLiteral` (`ExpressionEvaluator.cs:1039-1041`):
```
int denom = _context.GetMusicalContext().TimeSignature?.Denominator ?? 4;
double multiplier = _context.BeatTrueToSig ? (4.0 / denom) : 1.0;
return Value.Beat(beatLit.RawValue * multiplier);
```

`BeatConstructorFunctions.RegisterContextDependent` (`BeatConstructorFunctions.cs:28-30`):
```
int denom = context.GetMusicalContext().TimeSignature?.Denominator ?? 4;
double multiplier = context.BeatTrueToSig ? (4.0 / denom) : 1.0;
return Value.Beat(raw * multiplier);
```

CONFIRMED byte-identical formula.

**2. Pragma save-set-restore leak-safety:**

- Import boundary (`ModuleLoader.cs`): `prevBeatTrueToSig` saved before `try`; set in `try`; restored in `finally` (lines 170, 171, 251). Paired with `prevStrict` restore at line 248 — same pattern. CONFIRMED leak-safe.
- Proc/lambda invocation boundary (`Interpreter.cs`): `prevBeatTrueToSig` saved before `PushFrame()`; `_context.BeatTrueToSig = proc.IsBeatTrueToSig` set; restored in the `finally` block that also calls `PopFrame()` and restores `StrictMode` (lines 1149, 1150, 1244). CONFIRMED leak-safe.

**3. Cross-file declaring-file semantics:**

Live run of `tests/test_beat_cross_file.flow` stdout:
```
test_beat_cross_file_helper: loaded
local 1b in 6/8 pragma-on = 0.5
helper (beat 1) called from 6/8 pragma-on = 1
test_beat_cross_file: PASSED
```

The helper's `(beat 1)` correctly returns `1.0` (not `0.5`) because `ProcDeclaration.IsBeatTrueToSig = false` was captured at the helper's parse time. CONFIRMED working.

**4. WAV baseline two-run cmp-clean:**

- `intro.wav` fresh render SHA-256: `d401374c2f84bd142a8af85ace98e1ad2e580316118a25b1d78d9f6455fb3394` — matches committed baseline exactly.
- `cut-time.wav` fresh render SHA-256: `d3e0e832c5c17d1943986036bcbe0093a2e5c30c7c2ca9306e063886d054362d` — matches committed baseline exactly.

CONFIRMED two-run cmp-clean deterministic.

**5. Phase 44 strict regression (shared per-proc push/pop path):**

`dotnet test --filter "FullyQualifiedName~Phase44"` → `Passed: 275, Failed: 0, Total: 275`. CONFIRMED no regression.

**6. Full suite failures are pre-existing and unrelated to Phase 45:**

The 2-4 failures in the full `dotnet test` run are all `Phase48.Wasm*` tests (NETSDK1144 ILLink resource contention under parallel execution). These pass in isolation (`Passed: 19, Failed: 0` when run via `--filter "FullyQualifiedName~Phase48"`). They originate from commit `5562a61` which predates Phase 45 work.

---

### Gaps Summary

No gaps. All 26 must-have truths verified against the codebase. Phase goal achieved.

---

_Verified: 2026-05-29_
_Verifier: Claude (gsd-verifier) — independent of executor self-report_
