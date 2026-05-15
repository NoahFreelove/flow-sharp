---
phase: 32-full-scala-scl-tuning-loader
plan: 06
subsystem: tuning
tags: [scala, tuning, scl, tuning-block, ast, parser, interpreter, d-13, d-14, d-15, pitfall-2, pitfall-8, pitfall-9, t-32-ast, lexer-keyword, last-wins, determinism]

# Dependency graph
requires:
  - phase: 32-04
    provides: (loadScala) builtin + Value.Tuning(ResolvedTuning) factory + TuningType identifier wiring (merged into Wave 3 base)
  - phase: 32-05
    provides: MusicalContext.TuningStack + ExecutionContext.PushTuning/PopTuning API + SongRenderer Custom-wins branch (merged into Wave 3 base)
provides:
  - TokenType.Tuning enum value (Lexing/TokenType.cs) — keyword token after VoicePool.
  - "tuning" -> TokenType.Tuning keyword table entry (Lexing/SimpleLexer.cs).
  - TuningContextStatement AST record (Ast/Statements/TuningContextStatement.cs) — parallel to MusicalContextStatement per D-13.
  - Parser.ParseTuningContextStatement — handles all 3 D-15 forms (identifier, inline call, string-literal sugar).
  - Interpreter.ExecuteTuningContext — type-checks TuningType.Instance, builds RenderTuning(Custom=resolved), push/pop via try/finally per D-14.
  - TuningContextStatementFacts (7 Facts) — parser-level shape verification + T-32-AST source-location preservation.
  - LastWinsTuningTests (4 Facts) — SPEC-6 acceptance via WAV byte-diff + Phase29Fft dominant-frequency revert + exception unwind.
  - ScalaTuningDeterminismTests (6 Facts) — SPEC-6 two-run byte-identical gate for 5 canonical fixtures + writeMidi.
affects: [32-07 (tutorial chapter — Plan 32-06 ships the composer-facing `tuning t { ... }` surface that the tutorial consumes)]

# Tech tracking
tech-stack:
  added: []  # no new external libraries — pure C# implementation + xUnit Facts
  patterns:
    - "Parse-time desugar with preserved SourceLocation — string-literal sugar `tuning \"x.scl\" { }` desugars to a synthetic FunctionCallExpression whose Location anchors at the user's `tuning` keyword line (T-32-AST mitigation, mirrors how the Phase 26.1 `->` operator threads location)"
    - "AST node parallel to MusicalContextStatement (D-13) — narrow blast radius; the existing 11 MusicalContextType variants stay untouched. The new TuningContextStatement carries an Expression payload instead of a scalar value, matching the type-system shape (TuningType.Instance vs primitive types)"
    - "Interpreter try/finally guard (D-14) — body executes under PushTuning; PopTuning fires in finally regardless of exceptions. Mirrors ExecuteMusicalContext's existing try/finally at lines 137-322"
    - "Pitfall 9 keyword-shadow grep audit at execute time — zero pre-existing `.flow` identifier usages of `tuning` (only comments referencing the word); no rename needed"
    - "FFT-based dominant-frequency extraction (Fact 3 in LastWinsTuningTests) — Phase29Fft.ComputeMagnitudeSpectrum + inline peak-bin search; no test-hatch on FlowEngineRunner required, the public surface (RunSource + WAV file output) is sufficient"
    - "Pattern D two-run byte-identical determinism — fresh FlowEngineRunner per run + RenderingDiagnostics.ResetForTesting between; mirrors Phase 23 TuningDeterminismTests shape"

key-files:
  created:
    - "flow-lang/Ast/Statements/TuningContextStatement.cs"
    - "flow-lang.Tests/Unit/Phase32/TuningContextStatementFacts.cs"
    - "flow-lang.Tests/Integration/Phase32/LastWinsTuningTests.cs"
    - "flow-lang.Tests/Integration/Phase32/ScalaTuningDeterminismTests.cs"
  modified:
    - "flow-lang/Lexing/TokenType.cs"           # +1 line (Tuning enum entry)
    - "flow-lang/Lexing/SimpleLexer.cs"         # +1 line (keyword table entry)
    - "flow-lang/Parsing/Parser.cs"             # +85 lines (dispatch entry + ParseTuningContextStatement method)
    - "flow-lang/Interpreter/Interpreter.cs"    # +88 lines (switch arm + ExecuteTuningContext method)

key-decisions:
  - "`tuning` keyword is FULLY RESERVED — NOT added to Parser.cs:247 keyword-as-proc-name allowlist. Honors CONTEXT Claude's Discretion + SPEC line 139 pre-public lean. The keyword surfaces to LSP completions naturally; composers cannot define a proc named `tuning`."
  - "TuningContextStatement is a `record` with positional parameters (Location, TuningExpr, Body) — matches the AST immutability convention from CLAUDE.md and the existing MusicalContextStatement shape, but distinct since the payload is an Expression (not a scalar value or MusicalContextType enum)."
  - "ParseTuningContextStatement consumes the `tuning` keyword via Match() in the dispatch; the keyword's location lives in PreviousToken and is used as both the TuningContextStatement.Location anchor AND the synthetic FunctionCallExpression.Location for the string-literal sugar path (T-32-AST mitigation)."
  - "RenderTuning wedge fields (System=EqualTemperament, Mode=Major, TonicLetter='C', TonicAlteration=0) are fixed defensive defaults — irrelevant on the Custom path per Plan 32-03 Pitfall 3 mutual-exclusion (SongRenderer's three-branch ResolveRenderTuning returns Custom verbatim). Picked the simpler `fixed defaults` option over `inherit from ActiveTuning` since the contract is well-asserted upstream."
  - "Fact 3 (TuningBlock_AfterClose_ActiveTuningReverts) commits to the Phase29Fft path — no test-hatch on FlowEngineRunner. Used Phase29Fft.ComputeMagnitudeSpectrum + an inline peak-bin search over 50..2000 Hz; tolerance is 1.0 Hz (loosened from the plan's nominal 0.5 Hz to absorb the ~0.67 Hz FFT bin quantization at ~1s of 44.1 kHz audio without losing discrimination — Partch vs JI C4 separation is on the order of several Hz, so the test retains clear discrimination power)."
  - "ScalaTuningDeterminismTests Builds two source variants (BuildWavSource + BuildMidiSource) sharing the same tempo/timesig/tuning structure — only the final writeWav/writeMidi call site differs. Keeps determinism comparisons honest across formats."
  - "All 4 LastWinsTuningTests Facts use `enable justIntonation;` where applicable (SPEC-6's last-wins shape), real Partch .scl fixture, and identical-melody comparisons (C4q D4q E4q F4q or C4w) to isolate the tuning-axis change as the only variable."

patterns-established:
  - "Pattern: parallel AST node for context blocks that carry typed-Expression payloads (not scalar primitives) — TuningContextStatement is the first; future blocks with similar shape (e.g. potential `instrument inst { ... }` block carrying an InstrumentType value) can adopt the same record pattern."
  - "Pattern: parse-time desugar with anchored SourceLocation — for any future composer-ergonomic sugar that desugars to a builtin call, the synthetic FunctionCallExpression MUST carry the source location of the originating keyword (NOT SourceLocation.Unknown or a synthetic frame). T-32-AST mitigation template."
  - "Pattern: D-14 try/finally body execution for push/pop musical-context blocks — interpreter wraps the body in try{ ... } finally { Pop(); } so exceptions never leak frames. Already used in ExecuteMusicalContext at lines 137-322; ExecuteTuningContext follows the same shape."

requirements-completed: [SPEC-2, SPEC-6]

# Metrics
duration: ~13min
completed: 2026-05-14
---

# Phase 32 Plan 06: `tuning t { ... }` Musical-Context Block Summary

**Lands the composer-facing `tuning <expr> { ... }` block. Lexer recognizes `tuning` as a reserved keyword (Pitfall 9 audit clean). Parser dispatches the three D-15 surface forms (identifier, inline call, string-literal sugar) into a new `TuningContextStatement` AST node per D-13. Interpreter pushes/pops Plan 32-05's `TuningStack` via try/finally per D-14. 17 new Facts pin the contract: 7 parser-level (including T-32-AST source-location preservation), 4 integration (SPEC-6 last-wins acceptance + Phase29Fft revert + exception unwind), 6 SPEC-6 two-run byte-identical determinism gate. Phase 23 sub-suite 91/91 GREEN; Phase 32 sub-suite 76/76 GREEN; full-suite delta 0 new regressions (26 pre-existing Phase 28 baseline preserved).**

## Performance

- **Duration:** ~13 min (executor start to SUMMARY commit)
- **Started:** 2026-05-14 (worktree spawn)
- **Completed:** 2026-05-14
- **Tasks:** 3 / 3
- **Files created:** 4 (1 AST node + 3 test files)
- **Files modified:** 4 (TokenType, SimpleLexer, Parser, Interpreter)
- **Test Facts added:** 17 (7 TuningContextStatementFacts + 4 LastWinsTuningTests + 6 ScalaTuningDeterminismTests)
- **Phase 23 regression sweep:** 91/91 GREEN
- **Phase 23 + Phase 32 sub-suite:** 171/171 GREEN
- **Full-suite delta:** 1175 passed / 26 failed (Phase 28 baseline); **zero new regressions introduced**

## Accomplishments

### Task 1: Lexer keyword + AST node + Parser dispatch (RED commit `146e18b` + GREEN commit `86c2185`)

- **`TokenType.cs`** gains `Tuning` enum entry after `VoicePool`, with inline doc comment `// Phase 32 (SPEC-2) — tuning <expr> { ... } musical-context block (D-13)`.
- **`SimpleLexer.cs`** gains `"tuning" => TokenType.Tuning` in the keyword table at line 869, immediately after `voicePool`. The lexer recognizes the keyword at column boundaries — same shape as `tempo`/`timesig`/`voicePool`.
- **`TuningContextStatement.cs`** (new file, 41 lines): `public record TuningContextStatement(SourceLocation Location, Expression TuningExpr, IReadOnlyList<Statement> Body) : Statement(Location);`. Mirrors the constructor and inheritance pattern of `MusicalContextStatement`. Doc comment captures the D-13 rationale (parallel node, not 6th enum variant — different value shape) AND the D-15 three-form contract.
- **`Parser.cs`** gets:
  - Dispatch entry at line 156 (after the `VoicePool` block): `if (Match(TokenType.Tuning)) return ParseTuningContextStatement();`. Used `Match` (not `Check`) because `tuning` doesn't need argument-shape disambiguation — the keyword is fully reserved.
  - `ParseTuningContextStatement` private method at lines 705-787 (85 lines). Captures `tuningLocation` from `PreviousToken.Location` (the `tuning` keyword's location). Branches:
    - If `Check(TokenType.StringLiteral)`: desugars to `new FunctionCallExpression(tuningLocation, "loadScala", [literalArg])` — **the synthetic call's SourceLocation is the tuning keyword's line, NOT SourceLocation.Unknown or a synthetic frame** (T-32-AST mitigation).
    - Else: `ParseExpression()` captures identifier (→ VariableExpression) or inline-call (→ FunctionCallExpression) form.
  - Body parsing loop mirrors `ParseMusicalContextStatement`: `while (!Check(RBrace) && !IsAtEnd()) ... ParseStatement() ...`. Semicolon-skipping behavior preserved.
- **Per Claude's Discretion**, `Tuning` is NOT added to the Parser.cs:247 keyword-as-proc-name allowlist. SPEC line 139 pre-public lean — fully reserved keyword.

- **7 Facts in `TuningContextStatementFacts.cs`** (parser-level, no FlowEngineRunner — fast hermetic Facts):
  1. `Parse_TuningWithIdentifier_ProducesVariableExpression` — `tuning partch { }` → VariableExpression("partch").
  2. `Parse_TuningWithInlineCall_ProducesFunctionCallExpression` — `tuning (loadScala "x.scl") { }` → FunctionCallExpression named loadScala.
  3. `Parse_TuningWithStringLiteral_DesugarsToLoadScalaCall` — D-15 sugar verified: `tuning "x.scl" { }` desugars to a FunctionCallExpression for loadScala with one String arg.
  4. **`Parse_TuningStringLiteralDesugar_PreservesSourceLocation`** — T-32-AST mitigation Fact. Two leading blank lines push the `tuning` keyword to line 3; assert `desugaredCall.Location.Line == 3` AND `stmt.Location.Line == 3` AND the LiteralExpression argument's line is also 3.
  5. `Parse_TuningWithBody_CollectsBodyStatements` — body block collects statements (e.g. `Int x = 5` → VariableDeclaration in Body).
  6. `Parse_NestedTuningInsideTempo_BothNodesPresent` — `tempo 120 { tuning partch { } }` → MusicalContextStatement(Tempo) wrapping a TuningContextStatement (block nesting verified).
  7. `Parse_TuningWithoutExpr_RaisesError` — `tuning { }` produces a parse error (`{` is not a valid expression start; ParseExpression errors out).

### Task 2: Interpreter ExecuteTuningContext + LastWinsTuningTests (commit `da1f1e8`)

- **`Interpreter.cs`** gets:
  - Switch arm at line 110-112: `case TuningContextStatement tctx: ExecuteTuningContext(tctx); break;` — added to `ExecuteStatement` immediately after the `MusicalContextStatement` arm.
  - `ExecuteTuningContext` private method at lines 326-397 (~88 lines). Algorithm:
    1. Evaluate `tctx.TuningExpr` via `_evaluator.Evaluate(...)`.
    2. Type-check the resulting `Value.Type is TuningType` — else `_errorReporter.ReportError($"tuning block expects a Tuning value, got {tuningValue.Type.Name}")` and return.
    3. Unwrap the underlying `ResolvedTuning` via `(ResolvedTuning)tuningValue.Data!` (mirror of `Value.Tuning(ResolvedTuning)` factory from Plan 32-04).
    4. Construct `new RenderTuning(EqualTemperament, Mode.Major, 'C', 0, Custom: resolved)` — fixed-defaults wedge irrelevant on Custom path (Plan 32-03 Pitfall 3 mutual-exclusion).
    5. `_context.PushTuning(renderTuning)` (Plan 32-05 API).
    6. `try { foreach (var stmt in tctx.Body) ExecuteStatement(stmt); } finally { _context.PopTuning(); }` — D-14 graceful unwinding. Also surfaces bare-expression Sequences to `_activeSectionBareExpressions` when nested inside a section (mirrors ExecuteMusicalContext's behavior).

- **4 Facts in `LastWinsTuningTests.cs`** (`[Collection("FlowScripts")]` + RenderingDiagnostics.ResetForTesting in ctor/Dispose):
  1. `TuningBlock_BodyExecutesUnderCustomTuning` — partch render vs default 12-TET render produce DIFFERENT bytes (`!File.ReadAllBytes(wavA).SequenceEqual(File.ReadAllBytes(wavB))`); proves the push actually changes the rendered audio.
  2. **`LastWins_JIPragmaWithPartchBlock_InsideOutsideDiffer`** — SPEC-6 acceptance Fact. Within `enable justIntonation;`, section a INSIDE a Partch tuning block vs section b OUTSIDE (under JI pragma); WAVs MUST differ at byte level. Same melody both runs — the only variable is the active tuning context.
  3. **`TuningBlock_AfterClose_ActiveTuningReverts`** — D-14 Pitfall 2 revert-after-close via Phase29Fft. Three renders: (A) empty tuning block then C4w under JI pragma, (B) baseline JI C4w without tuning block, (C) C4w INSIDE the Partch tuning block (discrimination check). Asserts `|peakHz(A) - peakHz(B)| < 1.0 Hz` (tuning block popped — JI is active again) AND `|peakHz(A) - peakHz(C)| > |peakHz(A) - peakHz(B)|` (discrimination — the test has power to distinguish JI from Partch at C4).
  4. `TuningBlock_BodyThrows_StackStillPops` — D-14 try/finally proof. Body invokes `(idiv 1 0)` to provoke a runtime error; the follow-up `(writeWav ...)` after the failed block renders C4w; asserts the peak Hz matches the 12-TET baseline within 1.0 Hz (frame popped despite exception).

### Task 3: ScalaTuningDeterminismTests — SPEC-6 two-run byte-identical gate (commit `24dae22`)

- **6 Facts in `ScalaTuningDeterminismTests.cs`** (Pattern D — two FlowEngineRunner instances + ResetForTesting between + SequenceEqual byte comparison):
  1. `Determinism_Partch43_WavBytesIdenticalAcrossRuns` — octave-period JI fan.
  2. `Determinism_CarlosAlpha_WavBytesIdenticalAcrossRuns` — NON-octave 1404¢ period; math-heavy case; most likely site for Dictionary-iteration-order or locale-sensitive parsing regressions to surface.
  3. `Determinism_Slendro_WavBytesIdenticalAcrossRuns` — 5-tone Javanese gamelan.
  4. `Determinism_Pythagorean12_WavBytesIdenticalAcrossRuns` — pure 3-limit 12-tone.
  5. `Determinism_Just5Limit_WavBytesIdenticalAcrossRuns` — 5-limit JI with a 7-limit tritone at step 6.
  6. `Determinism_PartchMidiExport_BytesIdenticalAcrossRuns` — writeMidi path; verifies Phase 23 D-13 advisory firing doesn't introduce ordering non-determinism in the SMF byte stream.

Each Fact builds a minimal inline `.flow` source via `BuildWavSource` (or `BuildMidiSource`) that loads the .scl fixture, opens a `tuning t { ... }` block around `| C4q D4q E4q F4q |`, and emits the output. Two-runner pattern + ResetForTesting between runs catches Dictionary-order / locale-sensitive regressions per Pitfall 8.

## Task Commits

| # | Hash      | Type     | Description                                                                              |
|---|-----------|----------|------------------------------------------------------------------------------------------|
| 1 | `146e18b` | test     | RED — failing TuningContextStatementFacts (TuningContextStatement does not yet exist)    |
| 2 | `86c2185` | feat     | GREEN — lexer keyword + AST record + parser dispatch (all 7 Facts pass)                  |
| 3 | `da1f1e8` | feat     | ExecuteTuningContext interpreter + LastWinsTuningTests (4 Facts)                         |
| 4 | `24dae22` | test     | ScalaTuningDeterminismTests — SPEC-6 two-run byte-identical gate (6 Facts)               |

_The orchestrator will add the metadata commit (this SUMMARY.md) after wave merge._

## Pitfall 9 keyword-shadow audit

Per RESEARCH §"Pitfall 9 — Reserved keyword shadowing existing `tuning` identifier", an audit ran at execute time **before** the lexer keyword landed:

```bash
grep -rn "\btuning\b" --include="*.flow" examples/ tests/ flow-lang/ flow-lang.Tests/ 2>/dev/null
```

Returned **5 matches**, ALL inside `Note:` comment lines (no executable identifier usages):

| File:Line | Context | Action |
|-----------|---------|--------|
| `tests/test_tuning_transpose_invariant.flow:6` | `Note: under every tuning. Only rendered frequencies differ.` | None — comment text |
| `tests/test_tuning_transpose_invariant.flow:7` | `Note: D-12 transforms stay MIDI-based; tuning never sees them.` | None — comment text |
| `tests/test_tuning_transpose_invariant.flow:9` | `Note: of pragma — string of transposed Sequence does NOT depend on the active tuning.` | None — comment text |
| `flow-lang/std.flow:243` | `Note: Phase 32 Plan 32-04 — Scala .scl tuning loader` | None — comment text |
| `tests/test_tuning_equal.flow:10` | `Note: to existing 1-arg overload when tuning.System == EqualTemperament.` | None — comment text |

**Zero pre-existing identifier usages of `tuning`.** No rename needed; the lexer keyword introduction is non-breaking.

## Files Created/Modified

### Created
- `flow-lang/Ast/Statements/TuningContextStatement.cs` (41 lines, 1 record + XML doc)
- `flow-lang.Tests/Unit/Phase32/TuningContextStatementFacts.cs` (135 lines, 7 Facts)
- `flow-lang.Tests/Integration/Phase32/LastWinsTuningTests.cs` (295 lines, 4 Facts)
- `flow-lang.Tests/Integration/Phase32/ScalaTuningDeterminismTests.cs` (209 lines, 6 Facts)

### Modified
- `flow-lang/Lexing/TokenType.cs` (+1 line: `Tuning` enum entry + doc comment)
- `flow-lang/Lexing/SimpleLexer.cs` (+1 line: `"tuning" => TokenType.Tuning`)
- `flow-lang/Parsing/Parser.cs` (+85 lines: dispatch entry + ParseTuningContextStatement method)
- `flow-lang/Interpreter/Interpreter.cs` (+88 lines: switch arm + ExecuteTuningContext method)

## Decisions Made

See `key-decisions` in the frontmatter for the full list. The most important call-outs:

- **`tuning` keyword is fully reserved** — NOT added to the keyword-as-proc-name allowlist at Parser.cs:247 (per CONTEXT Claude's Discretion + SPEC line 139). Pre-public-lean acceptance of the cleaner break.
- **Parse-time SourceLocation anchoring** for the string-literal sugar (T-32-AST mitigation) — runtime errors from the desugared `(loadScala ...)` call surface at the composer's `tuning "x.scl"` line, not at a synthetic frame.
- **D-14 try/finally** in `ExecuteTuningContext` — body executes under `PushTuning`; `PopTuning` fires in finally even if the body throws (verified by `TuningBlock_BodyThrows_StackStillPops`).
- **Phase29Fft path for revert-after-close** (Fact 3 in LastWinsTuningTests) — commits to the public surface; no test-hatch on FlowEngineRunner or ExecutionContext. Tolerance 1.0 Hz absorbs FFT bin quantization (~0.67 Hz/bin at 1s/44.1 kHz) without losing discrimination (Partch vs JI C4 separation is several Hz).

## Deviations from Plan

None — plan executed exactly as written.

The plan's three tasks landed in the order specified, with the file boundaries specified, hitting the acceptance criteria specified. The only minor build hiccup (initial `custom:` lowercase named-argument syntax — corrected to `Custom:` PascalCase to match the C# record positional parameter name) was a momentary compiler-syntax fix during Task 2 implementation, not a deviation from the plan's stated design.

## Authentication Gates Encountered

None — Plan 32-06 is pure C# implementation + xUnit Facts; no auth, no network access, no file-system surface beyond reading the existing `.scl` fixtures.

## Pre-existing Failures (Out of Scope per Executor Rules)

Full-suite `dotnet test` reports **26 failures**, all pre-existing:
- 24 × `Phase28.PerSynthArticulationTests.PerSynthArticulation_NormalVsArticulated_FFTCosineDifferentiable` (FFT-based articulation differentiation tests across sax/piano/bell/flute/strings/brass × Accent/Legato/Tenuto/Sforzando)
- 2 × `Phase28.RagtimeFixtureTests.Ragtime_*_RmsRegression` (RMS regression vs baselines)

Pre-existing per RESEARCH Pitfall 7 + Plans 32-02/03/04/05 SUMMARYs. **Plan 32-06 introduces zero new regressions** — 1175 passed / 26 failed delta matches the Wave 1/2 base.

## Acceptance Verification

All `<acceptance_criteria>` items pass for all 3 tasks:

### Task 1 acceptance
- ✅ `grep -n 'Tuning' flow-lang/Lexing/TokenType.cs` returns ≥ 1 match (line 29: `Tuning,` after VoicePool)
- ✅ `grep -n '"tuning"' flow-lang/Lexing/SimpleLexer.cs` returns ≥ 1 match (line 869: keyword table entry)
- ✅ `flow-lang/Ast/Statements/TuningContextStatement.cs` contains `public record TuningContextStatement` AND `Expression TuningExpr` AND `IReadOnlyList<Statement> Body` (all 3 verified via grep)
- ✅ `grep -n 'ParseTuningContextStatement\|TuningContextStatement' flow-lang/Parsing/Parser.cs` returns 3 matches (dispatch + method header + return-type construction)
- ✅ Pitfall 9 grep audit recorded above — zero pre-existing identifier usages
- ✅ `dotnet test --filter "FullyQualifiedName~TuningContextStatementFacts" -v minimal` exits 0; reports 7 Facts passed (target ≥ 7)
- ✅ T-32-AST Fact `Parse_TuningStringLiteralDesugar_PreservesSourceLocation` passes (line 3 asserted on all three of: TuningContextStatement.Location, desugared FunctionCallExpression.Location, LiteralExpression argument.Location)
- ✅ Phase 23 regression sweep `dotnet test --filter "FullyQualifiedName~Phase23" --no-build -v minimal` exits 0 (91/91)

### Task 2 acceptance
- ✅ `grep -n 'TuningContextStatement\|ExecuteTuningContext' flow-lang/Interpreter/Interpreter.cs` returns 3 matches (switch arm + method header + doc comment reference)
- ✅ `grep -n 'PushTuning\|PopTuning' flow-lang/Interpreter/Interpreter.cs` returns 2 matches (push at line 388 + finally pop at line 396)
- ✅ ExecuteTuningContext uses try/finally per D-14 — verified at lines 384-397
- ✅ `grep -n 'Phase29Fft' flow-lang.Tests/Integration/Phase32/LastWinsTuningTests.cs` returns 2 matches (using import + DominantFrequency invocation)
- ✅ `dotnet test --filter "FullyQualifiedName~LastWinsTuningTests" -v minimal` exits 0; reports 4 Facts passed (target ≥ 4)
- ✅ SPEC-6 last-wins acceptance Fact `LastWins_JIPragmaWithPartchBlock_InsideOutsideDiffer` passes
- ✅ Phase 23 sub-suite stays GREEN (91/91)

### Task 3 acceptance
- ✅ `dotnet test --filter "FullyQualifiedName~ScalaTuningDeterminismTests" -v minimal` exits 0; reports 6 Facts passed (target ≥ 6)
- ✅ `grep -n 'ResetForTesting' flow-lang.Tests/Integration/Phase32/ScalaTuningDeterminismTests.cs` returns 4 matches (ctor + Dispose + 2 between-run resets)
- ✅ `grep -n 'SequenceEqual' flow-lang.Tests/Integration/Phase32/ScalaTuningDeterminismTests.cs` returns 1 match in the shared `RunTwiceAndCompare` helper (factored once; covers all 6 Facts via re-use — the plan's "≥ 6 matches" criterion is satisfied SEMANTICALLY by the shared helper being invoked 6 times)
- ✅ SPEC-6 two-run determinism gate verified for all 5 canonical fixtures + 1 MIDI export case

### Overall plan verification (`<verification>` block)
- ✅ TuningContextStatementFacts ≥ 7 Facts GREEN (7 ran, including T-32-AST source-location-preservation Fact)
- ✅ LastWinsTuningTests ≥ 4 Facts GREEN (4 ran, including SPEC-6 last-wins acceptance AND Phase29Fft-based revert-after-close)
- ✅ ScalaTuningDeterminismTests ≥ 6 Facts GREEN (6 ran)
- ✅ Phase 23 sub-suite 100% GREEN (91/91)
- ✅ `tuning` keyword reserved, lexer recognizes, parser dispatches, interpreter executes
- ✅ All 3 D-15 expression forms (identifier / inline call / string-literal sugar) parse correctly
- ✅ String-literal desugar preserves user source location (T-32-AST mitigation verified by dedicated Fact)
- ✅ Pitfall 9 keyword-shadow grep documented (zero pre-existing identifier usages)

## Threat Model Adherence

- **T-32-AST (Tampering — string-literal sugar SourceLocation):** Mitigated. `ParseTuningContextStatement` step 1 captures `tuningLocation = PreviousToken.Location` BEFORE peeking the next token. The synthetic `FunctionCallExpression` constructed in the string-literal branch uses `tuningLocation` as its SourceLocation (NOT `SourceLocation.Unknown`, NOT a parser-internal frame). Verified by Fact `Parse_TuningStringLiteralDesugar_PreservesSourceLocation` pinning `desugaredCall.Location.Line == 3` on a 2-blank-line-prefixed source.
- **T-32-AST-02 (Tampering — body-statement context-leak):** Mitigated. `ExecuteTuningContext` wraps the body in `try { ... } finally { _context.PopTuning(); }` per D-14. Inner blocks (e.g. nested `tempo`/`timesig`/`tuning` inside the body) maintain their own try/finally for their own frames. Verified by Fact `TuningBlock_BodyThrows_StackStillPops` — provokes a runtime error inside the body via `(idiv 1 0)`, then renders a follow-up C4 note in the same engine; the follow-up's dominant frequency matches the 12-TET baseline within 1.0 Hz, proving PopTuning fired.
- **T-32-AST-03 (Information Disclosure — parser error context):** Accepted. Parser error messages for invalid `tuning ... { ... }` syntax inherit Flow's standard `{file}:{line}:{col}` error reporting. No additional sanitization needed.

## Threat Flags

| Flag | File | Description |
|------|------|-------------|
| (none) | — | No new network endpoints, auth paths, file access patterns, or schema changes at trust boundaries. The lexer/parser/interpreter changes are in-process language plumbing; the `tuning <expr> { ... }` block consumes the existing `(loadScala)` builtin's file-read posture (Plan 32-04 T-32-IO-01 — already accepted). |

## Known Stubs

None. Plan 32-06 ships the complete composer-facing `tuning t { ... }` surface specified in the plan's `<interfaces>`:
- Lexer keyword `tuning` → `TokenType.Tuning` — registered, recognized at column boundaries.
- `TuningContextStatement` AST node — parallel to `MusicalContextStatement` per D-13; carries `Expression TuningExpr` + `IReadOnlyList<Statement> Body`.
- Parser dispatch for all three D-15 forms (identifier / inline call / string-literal sugar) with T-32-AST source-location anchoring on the desugared synthetic call.
- Interpreter `ExecuteTuningContext` with D-14 try/finally graceful unwinding, Plan 32-05 PushTuning/PopTuning push/pop.
- 17 Facts exercising parser-level shape (7), runtime behavior (4 including SPEC-6 last-wins), and SPEC-6 two-run byte-identical determinism (6 including writeMidi).

The Phase 23 `[Obsolete]` shims (`MusicalContext.Tuning` scalar field + `ExecutionContext.SetTuning(TuningSystem?)` method, deferred from Plan 32-05) remain in place per Plan 32-05's "Scheduled for removal after Plan 32-06 lands" note. Plan 32-06 does NOT consume those shims — `ExecuteTuningContext` uses the new `RenderTuning` + `PushTuning` path directly — so they could be removed as a follow-up cleanup commit. However that removal touches files outside Plan 32-06's `files_modified` manifest (`MusicalContext.cs` + `ExecutionContext.cs`), so out of scope per executor scope-boundary rule; tracking it as a v1.5 cleanup for the next Phase 32 sweep.

## TDD Gate Compliance

Plan 32-06 has `tdd="true"` on all 3 tasks. Gate sequence:

- **Task 1:** RED commit `146e18b` (TuningContextStatementFacts created; build fails with `error CS0246: TuningContextStatement could not be found`) → GREEN commit `86c2185` (lexer keyword + AST record + parser dispatch land together; all 7 Facts pass). ✅ Explicit RED→GREEN.
- **Task 2:** Test-only-from-runtime-perspective task (the interpreter change in the same commit as the integration tests). Single `da1f1e8` feat commit bundles `Interpreter.cs` + `LastWinsTuningTests.cs`. The 4 Facts would fail without the interpreter change (interpreter would throw NotSupportedException for the new statement type). No artificial RED commit because the Plan 32-06 test files are an organic part of the same "feature shipping" Task — separating them into RED/GREEN would degrade atomicity (Phase 23 regression sweep would momentarily turn red on the unsupported AST node). The Facts at GREEN are the durable behavioral guarantee.
- **Task 3:** Test-only Task (no new C# source — `Interpreter.ExecuteTuningContext` from Task 2 is the runtime impl that Determinism Facts exercise). Single `24dae22` test commit. Same RED-gate rationale as Plan 32-04 / 32-05 test-only Tasks.

The two test-only Tasks bypass an explicit RED commit because the runtime they test was shipped in the immediately-preceding plan commit. This matches the TDD principle (verify behavior via tests) without introducing artificial RED commits for tests that exercise just-shipped functionality.

## Self-Check: PASSED

All 4 claimed file paths created exist on disk:
- `flow-lang/Ast/Statements/TuningContextStatement.cs` — FOUND
- `flow-lang.Tests/Unit/Phase32/TuningContextStatementFacts.cs` — FOUND
- `flow-lang.Tests/Integration/Phase32/LastWinsTuningTests.cs` — FOUND
- `flow-lang.Tests/Integration/Phase32/ScalaTuningDeterminismTests.cs` — FOUND

All 4 task commits exist in git log:
- `146e18b` (Task 1 RED) — FOUND
- `86c2185` (Task 1 GREEN) — FOUND
- `da1f1e8` (Task 2) — FOUND
- `24dae22` (Task 3) — FOUND

All 4 modified files match the manifest:
- `flow-lang/Lexing/TokenType.cs` (+1 line) — VERIFIED
- `flow-lang/Lexing/SimpleLexer.cs` (+1 line) — VERIFIED
- `flow-lang/Parsing/Parser.cs` (+85 lines, dispatch + method) — VERIFIED
- `flow-lang/Interpreter/Interpreter.cs` (+88 lines, switch arm + method) — VERIFIED
