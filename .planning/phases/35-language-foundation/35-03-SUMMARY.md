---
phase: 35-language-foundation
plan: 03
subsystem: language-foundation
tags: [diagnostics, rust-style-renderer, levenshtein, did-you-mean, source-map, error-reporter, pragma-registry, match-exhaustive, lang-04]

# Dependency graph
requires:
  - phase: 35-language-foundation
    plan: 01
    provides: "Span(start, end) record + SourceMap per-engine registry + populated Spans on every Token + AST construction site"
  - phase: 21-pragma-system-h-alias
    provides: "PragmaRegistry KnownPragmas dict + private LevenshteinDistance Wagner-Fischer impl (lifted in this plan)"
provides:
  - "FlowDiagnostic(Level, Message, Primary Span, Labels, Notes, Suggestion?) record + DiagnosticLabel sub-record + Create/Warning/Info static factories"
  - "DiagnosticRenderer.Render(diagnostic, sourceMap, useColor) producing multi-line Rust-style string per RESEARCH §Example 4"
  - "LevenshteinHelper public static class — LevenshteinDistance + SuggestNearest(typed, candidates, threshold?) with default max(2, len/3) threshold and LCP/alphabetical tie-break"
  - "ErrorReporter.Report(FlowDiagnostic) overload + HasDiagnostics property + Diagnostics list + FormatDiagnostics(SourceMap, useColor) emit"
  - "PragmaRegistry.SuggestNearest delegates to LevenshteinHelper (single source of truth) + matchExhaustive entry in KnownPragmas (unblocks Plan 35-06 enforcement)"
  - "ExpressionEvaluator unknown-identifier path emits FlowDiagnostic with Levenshtein-derived Suggestion drawn from in-scope variables + InternalRegistry builtins"
  - "VariableExpression construction in Parser uses identifier token's full Span (was Span.At zero-width) so caret line sizes to identifier width"
  - "flow-interpreter Program.FormatErrorsForEmit picks FormatDiagnostics when HasDiagnostics, falls back to FormatErrors; wired across 4 emit sites (Program×2 + ScriptRunner + Repl)"
affects: [35-06-pattern-matching-exhaustiveness, 35-07-as-name-chain-binding, 38-lsp-polish-future]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Extract-and-delegate refactor (LevenshteinHelper lifted from PragmaRegistry; both call sites converge on the public helper)"
    - "Defaulted-parameter renderer flag (useColor:true default; useColor:false for golden-file comparisons)"
    - "Parallel-collection coexistence (FlowError + FlowDiagnostic both flow through ErrorReporter mid-migration; top-level emit picks the rich format when present)"
    - "Pre-populated golden-file baselines (ANSI-stripped plain ASCII committed under flow-lang.Tests/baselines/Phase35/diagnostics/)"

key-files:
  created:
    - flow-lang/Diagnostics/FlowDiagnostic.cs
    - flow-lang/Diagnostics/DiagnosticRenderer.cs
    - flow-lang/Diagnostics/LevenshteinHelper.cs
    - flow-lang.Tests/Phase35/DiagnosticRendererGoldenTests.cs
    - flow-lang.Tests/Phase35/LevenshteinSuggestionTests.cs
    - flow-lang.Tests/Phase35/DiagnosticTtyTests.cs
    - flow-lang.Tests/Phase35/MultiErrorRenderingTests.cs
    - flow-lang.Tests/Phase35/ReplDiagnosticTests.cs
    - flow-lang.Tests/baselines/Phase35/diagnostics/.gitkeep
    - flow-lang.Tests/baselines/Phase35/diagnostics/unknown_identifier.txt
    - flow-lang.Tests/baselines/Phase35/diagnostics/type_mismatch.txt
  modified:
    - flow-lang/Diagnostics/ErrorReporter.cs
    - flow-lang/Lexing/PragmaRegistry.cs
    - flow-lang/Interpreter/ExpressionEvaluator.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang.Tests/flow-lang.Tests.csproj
    - flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs
    - flow-interpreter/Program.cs
    - flow-interpreter/Repl.cs
    - flow-interpreter/ScriptRunner.cs

key-decisions:
  - "Renderer takes explicit useColor:bool rather than internally reading Console.IsOutputRedirected — keeps the function pure-deterministic, lets golden tests force plain ASCII, lets callers wrap in their own TTY-detection precedent (Program.cs:77 Console.ForegroundColor wrapping is preserved)"
  - "FlowError stays UNCHANGED as the legacy single-line fallback path — Span migration is progressive; ExpressionEvaluator type-mismatch / array-index / chord-literal sites continue to emit FlowError for v1.5 and migrate later"
  - "Top-level FormatErrorsForEmit concatenates rich + legacy when both are present — mid-migration period where some emit sites still call ReportError needs both outputs visible to the composer"
  - "Levenshtein candidate set for unknown-identifier diagnostics = union of in-scope variables + all InternalRegistry builtins (not just frame-chain function overloads) so prefix-only arithmetic + stdlib + harmony + transform names all become candidates"
  - "VariableExpression construction in Parser.cs:1164 now uses PreviousToken.EffectiveSpan instead of Span.At(location) — without this, caret was 1 char wide regardless of identifier length (defeats the entire point of Span migration for this diagnostic)"
  - "ErrorReporter.HasDiagnostics flag is the dispatch key for FormatErrorsForEmit — checks _diagnostics.Count > 0 rather than HasErrors (because HasErrors is set by both FlowDiagnostic AND FlowError reports)"
  - "matchExhaustive registered in PragmaRegistry.KnownPragmas in THIS plan even though the enforcement lives in Plan 35-06 — analogous to Phase 21 hAsB / Phase 23 justIntonation pattern of registering pragma scaffolding ahead of consumption"

patterns-established:
  - "Phase 35 LANG-04 Wave 2a Rust-style diagnostic renderer: FlowDiagnostic + DiagnosticRenderer + LevenshteinHelper provide the renderer-level surface; ErrorReporter parallel-collection + FormatDiagnostics provide the accumulator-level surface; flow-interpreter FormatErrorsForEmit + Console.ForegroundColor wrapping provide the top-level-emit surface. Subsequent plans (35-06 / 35-07) construct FlowDiagnostic at new emit sites; the rendering and emit pipeline is shared."

requirements-completed: [LANG-04]

# Metrics
duration: 50min
completed: 2026-05-19
---

# Phase 35 Plan 03: Rust-Style Diagnostic Renderer Summary

**LANG-04 Wave 2a — composer DX leap: unknown identifiers and span-bounded errors now render as Rust-style multi-line diagnostics with source-quoted spans, caret pointers, secondary notes, and Levenshtein-derived `did you mean '...'?` suggestions. Plan 35-01's Span infrastructure pays off here as the renderer consumes Span.End - Span.Start to size the caret line. Also registers `matchExhaustive` pragma scaffolding so Plan 35-06's exhaustiveness handling can land without re-touching PragmaRegistry.**

## Performance

- **Duration:** 50 min
- **Started:** ~2026-05-19T00:47Z
- **Completed:** 2026-05-19T01:37Z
- **Tasks:** 3 (Wave 0 test stubs → diagnostic stack implementation → end-to-end wiring)
- **Files created:** 11 (3 source + 5 xUnit fact classes + 2 baselines + 1 gitkeep)
- **Files modified:** 9 (ErrorReporter / PragmaRegistry / ExpressionEvaluator / Parser / 3 flow-interpreter / 1 csproj / 1 Phase 21 fact)

## Rendered Diagnostic Sample

Smoke-test fixture (`/tmp/diag_demo.flow`):
```flow
// trigger unknown identifier
(print transpos)
```

`dotnet run --project flow-interpreter /tmp/diag_demo.flow 2>&1`:
```
error: unknown identifier 'transpos'
  --> /tmp/diag_demo.flow:2:8
   |
 2 | (print transpos)
   |        ^^^^^^^^ not found in scope
   |
   = help: did you mean 'transpose'?
```

(ANSI escapes — red `error:` + carets, cyan `help:` — present in TTY mode; auto-suppressed when stderr is redirected per the existing `Console.ForegroundColor` precedent.)

## Accomplishments

- **FlowDiagnostic record + DiagnosticRenderer shipped.** New `FlowDiagnostic(Level, Message, Primary Span, IReadOnlyList<DiagnosticLabel> Labels, IReadOnlyList<string> Notes, string? Suggestion)` with Create/Warning/Info factories mirroring FlowError. DiagnosticRenderer.Render is a pure static method returning a string — caller decides whether to color (useColor flag) so the renderer is deterministic and golden-file friendly.

- **LevenshteinHelper extraction completed.** Wagner-Fischer DP impl lifted verbatim from `PragmaRegistry.cs:60-84` into `FlowLang.Diagnostics.LevenshteinHelper` (public). `SuggestNearest(typed, candidates, threshold?)` accepts any candidate set; tie-break order is longest common prefix → ordinal alphabetical. PragmaRegistry.SuggestNearest now delegates to the helper — both call sites converge on a single source of truth.

- **ErrorReporter extended without breaking the legacy path.** New `Report(FlowDiagnostic)` overload + `HasDiagnostics` + `Diagnostics` + `FormatDiagnostics(SourceMap, useColor)` surface. Legacy `Report(FlowError)` + `Errors` + `FormatErrors()` unchanged. Both collections coexist mid-migration — emit sites with rich Span context use FlowDiagnostic; sites without it continue to call FlowError.

- **ExpressionEvaluator unknown-identifier path migrated.** `EvaluateVariable` now constructs FlowDiagnostic with span-rich caret, label "not found in scope", and Levenshtein-derived suggestion. Candidate set = union of `CurrentFrame.GetAllAccessibleVariables().Keys` + `InternalRegistry.EnumerateSignatures()` names — so prefix-only `(add)`/`(sub)`/`(transpose)`/etc. all become did-you-mean candidates.

- **VariableExpression caret-width bug fixed.** Parser was constructing `VariableExpression` with `Span.At(location)` (zero-width), defeating the renderer's caret-sizing. Changed to `PreviousToken.EffectiveSpan` so the caret line correctly spans the identifier's full width (verified via smoke test — 8 carets under `transpos`).

- **Top-level emit wired across 4 sites.** New `Program.FormatErrorsForEmit(engine)` helper picks `FormatDiagnostics` when `HasDiagnostics`, falls back to `FormatErrors`, concatenates when both are present. Wired in `Program.RunFromString` + `Program.RunFromStdin` + `ScriptRunner.RunScript` + `Repl.Run`. The existing `Console.ForegroundColor = ConsoleColor.Red; ... ResetColor()` wrapping pattern is preserved (TTY auto-suppression by .NET handles redirected stderr).

- **matchExhaustive pragma registered.** Added to `PragmaRegistry.KnownPragmas` per the Phase 21 hAsB / Phase 23 justIntonation precedent. Plan 35-06's evaluator can now query `pragmaSet.Has("matchExhaustive")` without re-touching PragmaRegistry. Updated Phase 21 PragmaRegistryFacts.AlphabetizedKnownNames_ReturnsCsvSorted to reflect the 6-entry registry.

- **Pre-populated golden baselines committed.** `flow-lang.Tests/baselines/Phase35/diagnostics/unknown_identifier.txt` + `type_mismatch.txt` carry the exact ANSI-stripped expected output. MSBuild CopyToOutputDirectory rule added so xUnit reads them from `AppContext.BaseDirectory`. Two-line CSV update in PragmaRegistryFacts captures the matchExhaustive addition.

- **Zero regression contract held.** Full xUnit suite: 1280 pass + 26 pre-existing fail — IDENTICAL pre-existing failure count to Plan 35-01's SUMMARY (24 Phase 28 PerSynthArticulation FFT + 2 Phase 28 RagtimeFixture RMS). `.flow` regression loop: 84 PASS + 4 intentional-error scripts FAIL (identical pre-existing mix to Plan 35-02 tip). Two-run cmp-clean determinism on `examples/tutorial.flow`: SHA-256 `f2c3b2b3...` byte-identical to Plan 35-01's pinned sentinel.

## Task Commits

Each task was committed atomically with a per-task pre-commit HEAD assertion (per worktree-agent-protocol):

1. **Task 1: Wave 0 failing test stubs** — `209748d` (test) — 5 xUnit fact classes + baseline directory under `flow-lang.Tests/Phase35/` + `flow-lang.Tests/baselines/Phase35/diagnostics/`. Initially RED (compile error on `FlowDiagnostic` / `DiagnosticRenderer` / `LevenshteinHelper` / `DiagnosticLabel` / `ErrorReporter.HasDiagnostics`+`Diagnostics`+`FormatDiagnostics` not existing).

2. **Task 2: FlowDiagnostic + DiagnosticRenderer + LevenshteinHelper + ErrorReporter extension + matchExhaustive entry** — `08be481` (feat) — diagnostic stack landed; PragmaRegistry delegates to LevenshteinHelper; baselines pre-populated; csproj CopyToOutputDirectory rule added; Phase 21 CSV-known-names fact updated for the matchExhaustive registry entry. Wave 0 facts flip to 15/15 GREEN.

3. **Task 3: Wire ExpressionEvaluator unknown-identifier + Parser VariableExpression Span + flow-interpreter Program/ScriptRunner/Repl emit** — `8e24765` (feat) — end-to-end wiring. Smoke test produces rust-style output. Full test suite + .flow regression + tutorial determinism all GREEN.

## Golden-Baseline File List

```
flow-lang.Tests/baselines/Phase35/diagnostics/
├── .gitkeep                      (folder anchor)
├── unknown_identifier.txt        (8-line fixture per RESEARCH §Example 4 — header + location + 3 pipe-prefixed source/caret/blank rows + note + help row)
└── type_mismatch.txt             (7-line fixture — header + location + 3 pipe-prefixed source/caret/blank rows + note row; no suggestion)
```

MSBuild CopyToOutputDirectory rule (`<Link>baselines\Phase35\diagnostics\%(Filename)%(Extension)</Link>`) ensures the test runtime reads from `bin/Debug/net10.0/baselines/Phase35/diagnostics/`.

## Levenshtein Threshold + Tie-Break Behavior Summary

| Aspect | Value |
|---|---|
| **Algorithm** | Wagner-Fischer DP, two-row rolling array, O(n*m) time / O(m) space — verbatim lift from Phase 21 `PragmaRegistry.cs:60-84` |
| **Default threshold** | `Math.Max(2, typed.Length / 3)` — matches Phase 21 choice + RESEARCH § Pitfall 5 recommendation |
| **Caller-override** | `SuggestNearest(typed, candidates, threshold:N)` accepts explicit threshold for callers that want a tighter/looser cut |
| **Edge cases** | typed null/empty → null; candidates empty → null; null entries within candidates skipped |
| **Tie-break 1** | Longest common prefix to typed wins (e.g. typed=`tra`, ties=`tray`/`trab` both LCP=3 → continue to tie-break 2) |
| **Tie-break 2** | Ordinal alphabetical — first wins (e.g. `trab` < `tray` → `trab` returned) |
| **Suggestion cardinality** | ONE — never multiple, per RESEARCH § Pitfall 5 ("Did-you-mean shadow rule") |
| **Convergence** | Both `PragmaRegistry.SuggestNearest` AND new diagnostic renderer's unknown-identifier suggestions consume the same `LevenshteinHelper.SuggestNearest` — single source of truth |

## Files Created/Modified

### Created
- `flow-lang/Diagnostics/FlowDiagnostic.cs` — `record FlowDiagnostic(Level, Message, Primary Span, Labels, Notes, Suggestion?)` + nested `DiagnosticLabel(Span Span, string Text)` sub-record + Create/Warning/Info factories.
- `flow-lang/Diagnostics/DiagnosticRenderer.cs` — `public static class` with `Render(diagnostic, sourceMap, useColor:true)` producing multi-line Rust-style output per RESEARCH §Example 4. ANSI palette (red error / yellow warning / cyan info+note+help) gated by useColor flag.
- `flow-lang/Diagnostics/LevenshteinHelper.cs` — `public static class` with `LevenshteinDistance(a, b)` (Wagner-Fischer DP) + `SuggestNearest(typed, candidates, threshold?)` (default threshold + LCP/alphabetical tie-break).
- `flow-lang.Tests/Phase35/DiagnosticRendererGoldenTests.cs` — 2 golden-file facts.
- `flow-lang.Tests/Phase35/LevenshteinSuggestionTests.cs` — 6 facts (3 required by plan + 3 edge-case extras).
- `flow-lang.Tests/Phase35/DiagnosticTtyTests.cs` — 2 facts (useColor on/off).
- `flow-lang.Tests/Phase35/MultiErrorRenderingTests.cs` — 2 facts (blank-line separator + empty-when-none).
- `flow-lang.Tests/Phase35/ReplDiagnosticTests.cs` — 3 facts (`<eval>` + `<stdin>` sentinel resolution + missing-source graceful degrade).
- `flow-lang.Tests/baselines/Phase35/diagnostics/.gitkeep` + `unknown_identifier.txt` + `type_mismatch.txt` — committed golden baselines.

### Modified
- `flow-lang/Diagnostics/ErrorReporter.cs` — added `Report(FlowDiagnostic)` overload, `HasDiagnostics` property, `Diagnostics` accessor, `FormatDiagnostics(SourceMap, useColor)` method. Legacy `Report(FlowError)` + `Errors` + `FormatErrors` + `Clear` (now clears both collections) unchanged in shape.
- `flow-lang/Lexing/PragmaRegistry.cs` — `SuggestNearest` delegates to `LevenshteinHelper.SuggestNearest`. Removed the now-redundant private `LevenshteinDistance`. Added `matchExhaustive` entry to `KnownPragmas` dict.
- `flow-lang/Interpreter/ExpressionEvaluator.cs` — `EvaluateVariable` unknown-id path replaces `ReportError` with `FlowDiagnostic.Report`. Added `FlowLang.Core` using for `Span.At`.
- `flow-lang/Parsing/Parser.cs` — `VariableExpression` construction now uses `PreviousToken.EffectiveSpan` instead of `Span.At(location)`.
- `flow-lang.Tests/flow-lang.Tests.csproj` — `CopyToOutputDirectory` rule added for `baselines/Phase35/diagnostics/*.txt`.
- `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` — `AlphabetizedKnownNames_ReturnsCsvSorted` updated to 6-entry CSV reflecting `matchExhaustive` addition.
- `flow-interpreter/Program.cs` — new `internal static FormatErrorsForEmit(engine)` helper; `RunFromString` + `RunFromStdin` route emit through it.
- `flow-interpreter/Repl.cs` — `Run` loop emits via `Program.FormatErrorsForEmit` so REPL `<repl>` sentinel-keyed source quotes through the rich renderer.
- `flow-interpreter/ScriptRunner.cs` — `RunScript` emits via `Program.FormatErrorsForEmit`.

## Decisions Made

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | Renderer takes explicit `useColor:bool` rather than internally reading `Console.IsOutputRedirected` | Keeps the function pure-deterministic (golden tests assert exact strings); callers wrap in their own TTY-detection precedent (`Program.cs:77` `Console.ForegroundColor = Red`); .NET auto-suppresses ANSI on redirected stderr — so the existing wrapping pattern handles TTY-vs-pipe without the renderer needing to know. |
| 2 | FlowError stays UNCHANGED as the legacy single-line fallback path | Span migration is progressive. ExpressionEvaluator type-mismatch / array-index / chord-literal / and ~10 other emit sites continue to use `ReportError` for v1.5. Mid-migration coexistence is the PATTERNS.md Bucket 2a § ErrorReporter.cs Notable Departures convention. |
| 3 | `FormatErrorsForEmit` concatenates rich + legacy when both are present | A single failing script can trigger BOTH the new unknown-identifier diagnostic AND a downstream legacy "Function not found" cascade. The composer needs to see both — the unknown identifier is the root cause and the cascade is collateral. |
| 4 | Levenshtein candidate set = union of in-scope variables + ALL InternalRegistry builtins | Prefix-only arithmetic (`add`, `sub`, `mul`) + harmony (`chordNotes`, `arpeggio`) + transforms (`transpose`, `invert`) all become did-you-mean candidates. Restricting to frame-chain function overloads would miss the bulk of the builtin surface. |
| 5 | VariableExpression construction now uses `PreviousToken.EffectiveSpan` instead of `Span.At(location)` | Without this, caret was 1 char wide regardless of identifier length — defeats the entire point of Span migration for this diagnostic. The lexer's identifier token Span is exactly what the renderer needs. |
| 6 | `ErrorReporter.HasDiagnostics` flag is the dispatch key (not `HasErrors`) | `HasErrors` is set by both FlowDiagnostic AND FlowError reports — using it for dispatch would mean a legacy-only error path still tries to render via the (empty) Diagnostics list. `HasDiagnostics` is true only when rich-format diagnostics exist. |
| 7 | `matchExhaustive` registered in THIS plan even though enforcement lives in Plan 35-06 | Mirrors Phase 21 (hAsB) / Phase 23 (justIntonation/pythagorean/equalTemperament) precedent — registry scaffolding lands ahead of consumption. The closed-set design (D-12) requires the registry to know about the pragma name before the lexer can accept it. |
| 8 | Test 5 (`TieBrokenByLongestCommonPrefixThenAlphabetical`) tie-breaks `trab` over `tray` (both LCP=3) by ordinal alphabetical | `trab` < `tray` ordinal-comparing. The plan's plain-English description in Task 1's behavior section was ambiguous; the chosen tie-break order matches the implementation. |

## Deviations from Plan

### Rule 1 — Pragma-registry CSV fact update (test fix)

The `AlphabetizedKnownNames_ReturnsCsvSorted` fact in `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` asserts the exact CSV of known pragma names. Adding `matchExhaustive` (correctly per plan) caused the fact to fail with `Expected: "...justIntonation, pythagorean..." Actual: "...justIntonation, matchExhaustive, pythagorean..."`. This is a Rule 1 fix — the fact's intent is "lock down which pragmas exist," and Phase 35 intentionally adds a new pragma. Updated the fact's expected CSV to the 6-entry list and tightened the comment to reference Plan 35-03's matchExhaustive registration. Committed alongside Task 2.

### Rule 2 — VariableExpression Span widening (caret correctness)

During Task 3 smoke testing, the rendered diagnostic showed a single-character caret (`^`) under the 8-character `transpos` identifier — visually defeating the renderer. Root cause: `Parser.cs:1164` constructs `VariableExpression` with `Span.At(location)` (zero-width span). This is a Rule 2 fix (missing critical functionality) — the Span migration was supposed to provide proper caret widths, and the unknown-identifier path is the primary consumer. Changed to `PreviousToken.EffectiveSpan` so the identifier's full source span flows through. Committed as part of Task 3.

### No Rule 3 or Rule 4 deviations

No blocking issues encountered; no architectural changes required. All Span / SourceMap / FlowEngine.SourceMap infrastructure was already in place from Plan 35-01.

## Authentication Gates

None — Phase 35 work is internal to the language-tooling stack.

## Verification Results

### Phase 35 xUnit suite (`dotnet test --filter "FullyQualifiedName~Phase35"`)

- **Total:** 30 (Plan 35-01's 8 LexerSpan/AstSpan/SpanMigrationRegression + Plan 35-02's 7 HK01HumanizeGaussianVoiceBlocks/MutateRhythmEnumValues + Plan 35-03's 15 Diagnostic*/Levenshtein*/Multi*/Repl*)
- **Result:** 30/30 GREEN, 238ms

### Full xUnit suite (`dotnet test`)

- **Total:** 1306
- **Pass:** 1280
- **Fail:** 26 (ALL pre-existing per Plan 35-01 SUMMARY — 24 `Phase28.PerSynthArticulationTests.PerSynthArticulation_NormalVsArticulated_FFTCosineDifferentiable` parametric cases + 2 `Phase28.RagtimeFixtureTests.Ragtime_*_RmsRegression`)
- **Zero new failures introduced by this plan.**

### `.flow` script regression loop

- **Total:** 88 scripts under `tests/test_*.flow`
- **Pass:** 84
- **Fail:** 4 (all intentional-error scripts — `test_dict_type_errors.flow`, `test_error_masking.flow`, `test_iteration_guard.flow`, `test_musical_context_errors.flow`; each has a comment header documenting the expected non-zero exit)
- **Identical pass/fail mix to Plan 35-02 tip.**

### Determinism sentinel

- `examples/tutorial.flow` rendered to WAV twice in a row
- Run 1 SHA-256: `f2c3b2b3c2a9a8e7f631bd468444919f66c980f936d1c661895f3fb1ca8d6b39`
- Run 2 SHA-256: `f2c3b2b3c2a9a8e7f631bd468444919f66c980f936d1c661895f3fb1ca8d6b39`
- **Result:** PRESERVED — byte-identical to Plan 35-01's pinned sentinel (Phase 18/25/27/28 two-run cmp-clean determinism contract intact)

### Smoke test (Task 3 acceptance criterion)

- Fixture: `/tmp/diag_demo.flow` with `(print transpos)` on line 2
- `dotnet run --project flow-interpreter /tmp/diag_demo.flow 2>&1 | grep -E "did you mean|transpose|-->" | wc -l` → **2** (≥1 required)
- Lines matching: `--> /tmp/diag_demo.flow:2:8` location row AND `= help: did you mean 'transpose'?` suggestion row

## Blast Radius Confirmation

- **LANG-04 user-facing half satisfied.** Composer DX leap: unknown identifiers now render Rust-style with source quote + caret + did-you-mean. Sets the bar for downstream emit-site migrations (parser parse-errors, type-mismatch sites in ExpressionEvaluator).
- **Plan 35-06 UNBLOCKED.** `matchExhaustive` pragma registered in `KnownPragmas` — Plan 35-06's match-eval can query `pragmaSet.Has("matchExhaustive")` without re-touching PragmaRegistry. Pattern matches Phase 21 hAsB / Phase 23 justIntonation precedent.
- **Plan 35-07 UNBLOCKED.** `-> as name` chain binding will produce FlowExpression-derived AST that can now emit FlowDiagnostic with proper Span via the surface shipped here.
- **Future LSP polish phase (38) PREPARED.** Diagnostic renderer surface lets LSP highlight the FULL erroneous expression instead of a 1-char range (LspMappings.cs:22-26 hard-codes col+1 today — flagged for v1.5 LSP polish per RESEARCH §State of the Art).

## Known Pre-existing Failures (NOT caused by this plan)

Same 26 failures as Plan 35-01's SUMMARY — see that document for the full breakdown. Surfaces:

- `Phase28.PerSynthArticulationTests.PerSynthArticulation_NormalVsArticulated_FFTCosineDifferentiable` × 24 (synthesizer returning silence under articulation)
- `Phase28.RagtimeFixtureTests.Ragtime_*_RmsRegression` × 2 (RMS baseline drift in first 100ms window)

Plus 4 intentional-error `.flow` scripts. These are NOT regressions — Plan 35-03 is verifiably zero-regression in all areas it could affect (diagnostic infrastructure is purely additive; ExpressionEvaluator EvaluateVariable change is observationally backward-compatible because the new diagnostic still results in `Value.Void()` return + `HasErrors=true` like the prior FlowError path).

## Threat Flags

No new security-relevant surface introduced. Diagnostic renderer emits to local stderr only; no network egress; ANSI escape sequences in source-quote text are passed through (composer's own source) per the accepted disposition T-35-07 / T-35-08 / T-35-09 in the plan's threat register.

## Self-Check: PASSED

- [x] `flow-lang/Diagnostics/FlowDiagnostic.cs` exists (verified via `[ -f ... ]`)
- [x] `flow-lang/Diagnostics/DiagnosticRenderer.cs` exists
- [x] `flow-lang/Diagnostics/LevenshteinHelper.cs` exists
- [x] `flow-lang.Tests/baselines/Phase35/diagnostics/unknown_identifier.txt` + `type_mismatch.txt` exist and are non-empty
- [x] `flow-lang.Tests/Phase35/DiagnosticRendererGoldenTests.cs` + LevenshteinSuggestionTests.cs + DiagnosticTtyTests.cs + MultiErrorRenderingTests.cs + ReplDiagnosticTests.cs all exist
- [x] All 3 task commits present: `209748d` (Task 1), `08be481` (Task 2), `8e24765` (Task 3)
- [x] `grep -c "LevenshteinHelper" flow-lang/Lexing/PragmaRegistry.cs` returns ≥1 (delegation lands)
- [x] `grep -c "matchExhaustive" flow-lang/Lexing/PragmaRegistry.cs` returns ≥1 (KnownPragmas entry added)
- [x] `grep -c "FormatDiagnostics" flow-lang/Diagnostics/ErrorReporter.cs` returns ≥1
- [x] `grep -c "FormatDiagnostics" flow-interpreter/Program.cs` returns ≥1
- [x] `grep -c "FlowDiagnostic" flow-lang/Interpreter/ExpressionEvaluator.cs` returns ≥1
- [x] 30/30 Phase 35 xUnit facts pass GREEN
- [x] Pre-existing 26 failures unchanged (no new failures introduced)
- [x] Two-run cmp-clean determinism preserved (SHA byte-identical to Plan 35-01 sentinel)
- [x] Smoke test produces ≥1 match for `did you mean|transpose|-->`
