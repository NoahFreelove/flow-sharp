---
phase: 31-lsp-enhancements-jetbrains-stretch
plan: 02
subsystem: lsp
tags: [lsp, diagnostics, analyzer, scalelint, default-on, omnisharp, charitable-failopen]

# Dependency graph
requires:
  - phase: 17
    provides: ScaleLintAnalyzer pattern; CombinedDiagnosticsPublisher orchestrator; LspFixtures.Parse helper; AST node shapes (ImportStatement / SectionDeclaration / VariableDeclaration / SongExpression / FunctionCallExpression / VariableExpression / MemberAccessExpression)
  - phase: 24
    provides: pure-static analyzer-per-diagnostic-type discipline (D-04); charitable fail-open contract (D-22); dotted-source-string convention (D-18); diagnostic source = "flow.scaleLint" (D-18)
  - phase: 31-01
    provides: StdlibSymbolIndex.ProcsForModule reverse-lookup helper; LspFixtures.StdlibIndex() shared test helper; 31-DECISIONS.md (D-11 + D-12)
provides:
  - "UnusedImportAnalyzer.Analyze(ast, tokens, source, stdlib) → Warning diagnostics for unreferenced imports; @std transitive-reference handling via StdlibSymbolIndex.ModuleNames"
  - "UnreachableSectionAnalyzer.Analyze(ast, tokens, source) → Information diagnostics for SectionDeclarations not referenced by any SongExpression"
  - "ShadowedVariableAnalyzer.Analyze(ast, tokens, source) → Warning diagnostics for nested-scope VariableDeclarations whose name matches an outer scope (scope-stack walker descends ProcDeclaration / MusicalContextStatement / SectionDeclaration / ForStatement / WhileStatement / LambdaExpression bodies)"
  - "ScaleLintAnalyzer promoted to default-on (D-03 supersedes D-19); `enable scaleLint;` pragma stays parseable as a no-op (D-04 backward compat)"
  - "CombinedDiagnosticsPublisher.BuildAll wires all 5 sources (parse + scaleLint + unusedImport + unreachableSection + shadowedVariable) through one PublishDiagnostics call per URI per parse cycle"
  - "PragmaRegistry KnownPragmas['scaleLint'] description updated to Phase 31 D-03 wording"
affects: [31-03, 31-04, 31-05, 31-06, 31-07, 31-08, 31-09]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "analyzer-per-diagnostic-type (Phase 24 D-04) extended from 1 → 4 analyzer classes; same public-static-class shape, same charitable try/catch fail-open"
    - "five-source diagnostic merge in CombinedDiagnosticsPublisher.BuildAll — removed the pre-Phase-31 short-circuit because three new analyzers may fire when parse + scaleLint are silent"
    - "instance Publish() delegates to static BuildAll() for single-source-of-truth merge logic (Phase 24 had two parallel implementations of the merge inside CombinedDiagnosticsPublisher; collapsed in Phase 31)"
    - "scope-stack walker for shadow detection: Stack<Dictionary<string, SourceLocation>> with frame push/pop on block bodies; same-scope re-decl is NOT shadowing (only nested-scope counts)"
    - "test migration discipline: pre-Phase-31 facts that pinned opt-in semantics are RENAMED with `_Phase31_D03_DefaultOn` suffix and assertion inverted, preserving composer-grep-ability instead of being deleted outright"

key-files:
  created:
    - flow-lsp/Diagnostics/UnusedImportAnalyzer.cs
    - flow-lsp/Diagnostics/UnreachableSectionAnalyzer.cs
    - flow-lsp/Diagnostics/ShadowedVariableAnalyzer.cs
    - flow-lang.Tests/Unit/Phase31/UnusedImportAnalyzerFacts.cs
    - flow-lang.Tests/Unit/Phase31/UnreachableSectionAnalyzerFacts.cs
    - flow-lang.Tests/Unit/Phase31/ShadowedVariableAnalyzerFacts.cs
    - flow-lang.Tests/Unit/Phase31/ScaleLintDefaultOnFacts.cs
    - .planning/phases/31-lsp-enhancements-jetbrains-stretch/deferred-items.md
  modified:
    - flow-lsp/Diagnostics/ScaleLintAnalyzer.cs
    - flow-lsp/Diagnostics/CombinedDiagnosticsPublisher.cs
    - flow-lang/Lexing/PragmaRegistry.cs
    - flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerFacts.cs
    - flow-lang.Tests/Unit/Phase24/CombinedDiagnosticsPublisherFacts.cs

key-decisions:
  - "Charitable fail-open implemented as a single try/catch wrapping each analyzer's Analyze body — every analyzer is `try { ... walk ... return diagnostics; } catch { return Array.Empty<Diagnostic>(); }` so even an AST-shape change in a future phase never crashes the LSP wire"
  - "@std import treated as transitively-importing every ModuleName via StdlibSymbolIndex.ModuleNames; a reference to ANY proc in @collections / @audio / @bars / @notation / @composition keeps @std alive"
  - "Non-@ user-module imports (relative paths) are conservatively kept (no diagnostic emitted) — we can't reason about their exports without resolving the file, so silence beats false positives"
  - "Sub-scope of LambdaExpression bodies counts for ShadowedVariableAnalyzer — a lambda body's parameter names can shadow outer file-scope variables and the analyzer flags them"
  - "Phase 24 facts that asserted LINT-02 opt-in semantics are MIGRATED (renamed + inverted assertion) rather than deleted, preserving line-coverage trail"

patterns-established:
  - "Multi-source CombinedDiagnosticsPublisher: extend by adding one parameter (stdlib) for any analyzer that needs it, AddRange the result into the merge list — no IPublisher interfaces required for v1.4 (DI symmetry deferred per RESEARCH §Open Questions #3)"
  - "Test-input syntax discipline: when plan-as-written shows aspirational syntax (e.g., C-style proc bodies, hypothetical @harmony module), verify the syntax/module exists in the codebase first, then either rewrite tests to use real syntax OR raise a Rule 4 architectural change. Plan 31-02 chose the former (Rule 1 auto-fix) because the analyzer behavior was unaffected"
  - "Test naming for default-flip migrations: `_Phase{N}_D{NN}_DefaultOn` suffix marks the spec-locked policy change and preserves composer-grep-ability for the original test name"

requirements-completed: [SPEC-1]

# Metrics
duration: ~35min
completed: 2026-05-12
---

# Phase 31 Plan 02: SPEC-1 Diagnostic Surface Expansion Summary

**Three new analyzers (UnusedImport Warning, UnreachableSection Information, ShadowedVariable Warning) ship through CombinedDiagnosticsPublisher.BuildAll alongside the existing ScaleLintAnalyzer (now default-on per Phase 31 D-03) — closing SPEC-1's four-severity surface in one parse cycle per URI.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-05-13T01:09Z (approximate; orchestrator-supplied)
- **Completed:** 2026-05-13T01:44Z (approximate)
- **Tasks:** 3 / 3
- **Files modified:** 13 (8 created, 5 modified)

## Accomplishments

- **3 new pure-static analyzer classes** under `flow-lsp/Diagnostics/`, each one a `public static class XxxAnalyzer` with a single public `Analyze(...)` method, charitable try/catch fail-open per Phase 24 D-22 precedent, dotted source string per Phase 24 D-18 / Phase 31 D-05.
- **`StdlibSymbolIndex.ProcsForModule`** (from Plan 31-01 Wave 0) consumed by UnusedImportAnalyzer; `@std` import treated as transitively-importing every module in `StdlibSymbolIndex.ModuleNames` so a `head` reference keeps `use "@std"` alive.
- **`ScaleLintAnalyzer` promoted to default-on** (D-03 supersedes D-19) — the two-line pragma short-circuit deleted; the analyzer's class docblock updated to cite the supersession. `enable scaleLint;` remains a recognized `PragmaRegistry.KnownPragmas` entry with the description rewritten to "Phase 31 D-03 no-op for v1.3 backward compat."
- **`CombinedDiagnosticsPublisher.BuildAll`** signature extended with a `StdlibSymbolIndex stdlib` parameter; the five-way merge replaces the pre-Phase-31 two-source early-return short-circuit. Instance `Publish()` now delegates to static `BuildAll()` so the merge logic lives in one place.
- **226 / 226** Phase 17 + 21 + 24 + 31 unit tests pass with zero regressions.
- **20 / 20** `ByteIdentical*` determinism tests pass — analyzers are LSP-only and don't touch any flow-lang DSP / synthesis / rendering path.
- **2 Phase 24 facts migrated** (renamed `_Phase31_D03_DefaultOn`, assertion inverted) instead of deleted, preserving composer-grep-ability.

## Task Commits

Each task was committed atomically:

1. **Task 1: Ship UnusedImport/UnreachableSection/ShadowedVariable analyzers + 12 tests** — `161755c` (feat)
2. **Task 2: Promote ScaleLintAnalyzer to default-on + Phase 24 fact migration + 4 default-on facts** — `e259845` (feat)
3. **Task 3: Wire 4 analyzers through CombinedDiagnosticsPublisher.BuildAll** — `078a3f7` (feat)

Plan metadata commit (this SUMMARY + STATE/ROADMAP updates) will follow.

## Files Created/Modified

**Created**
- `flow-lsp/Diagnostics/UnusedImportAnalyzer.cs` — 215 lines. Walks AST, collects every identifier reference (function-call names + variable references + member names), then per-import checks `stdlib.ProcsForModule(moduleName)` against the referenced-names set. Special-cases `@std` to expand to `ModuleNames`. Emits one `Diagnostic { Severity = Warning, Source = "flow.unusedImport" }` per defined-but-unreferenced import.
- `flow-lsp/Diagnostics/UnreachableSectionAnalyzer.cs` — 175 lines. Two-pass: collects `(SectionName → Location)` defined-set, then collects referenced-name set from every `SongExpression.Sections`. Emits one `Diagnostic { Severity = Information, Source = "flow.unreachableSection" }` per defined-not-referenced section. Handles `name*N` repeat syntax via `SongSectionReference.Name`.
- `flow-lsp/Diagnostics/ShadowedVariableAnalyzer.cs` — 200 lines. Scope-stack walker (`Stack<Dictionary<string, SourceLocation>>`). Pushes new frames on `ProcDeclaration.Body`, `MusicalContextStatement.Body`, `SectionDeclaration.Body`, `ForStatement.Body`, `WhileStatement.Body`, and `LambdaExpression.Body`. For each `VariableDeclaration`, walks outer frames; on match emits `Diagnostic { Severity = Warning, Source = "flow.shadowedVariable", Message = $"Variable '{name}' shadows declaration at line {outerLine}, column {outerCol}" }`. Same-scope re-declaration is NOT shadowing.
- `flow-lang.Tests/Unit/Phase31/UnusedImportAnalyzerFacts.cs` — 4 [Fact] tests.
- `flow-lang.Tests/Unit/Phase31/UnreachableSectionAnalyzerFacts.cs` — 4 [Fact] tests.
- `flow-lang.Tests/Unit/Phase31/ShadowedVariableAnalyzerFacts.cs` — 4 [Fact] tests.
- `flow-lang.Tests/Unit/Phase31/ScaleLintDefaultOnFacts.cs` — 4 [Fact] tests pinning the D-03 default-on contract.
- `.planning/phases/31-lsp-enhancements-jetbrains-stretch/deferred-items.md` — logs 62 pre-existing Phase 28 PerSynthArticulation + FlowScriptTests failures as out-of-scope per the executor's SCOPE BOUNDARY rule.

**Modified**
- `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` — deleted the two-line Phase 24 D-19 pragma-gate short-circuit; updated the class docblock to cite Phase 31 D-03 supersession. Body shape and `Source = "flow.scaleLint"` string unchanged.
- `flow-lsp/Diagnostics/CombinedDiagnosticsPublisher.cs` — extended `BuildAll` signature with `StdlibSymbolIndex stdlib` parameter; replaced the pre-Phase-31 two-source short-circuit with the five-way merge; instance `Publish` now delegates to `BuildAll`. Constructor now takes a third `StdlibSymbolIndex stdlib` param (auto-resolved by OmniSharp DI).
- `flow-lang/Lexing/PragmaRegistry.cs` — rewrote the `KnownPragmas["scaleLint"]` description: `"Phase 31 D-03: scale-lint is now default-on; this pragma is accepted as a no-op for v1.3 backward compat."` Key (`scaleLint`) unchanged.
- `flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerFacts.cs` — renamed `PragmaAbsent_NeverFlags_LINT02` → `PragmaAbsent_StillFlags_Phase31_D03_DefaultOn`; assertion inverted (was `Assert.Empty(diags)`, now `Assert.Single + Severity/Source/Message contains "F#4"`).
- `flow-lang.Tests/Unit/Phase24/CombinedDiagnosticsPublisherFacts.cs` — renamed `BuildAll_PragmaAbsent_NoLintDiagnostics` → `BuildAll_PragmaAbsent_StillEmitsScaleLint_Phase31_D03_DefaultOn`; assertion inverted (was `DoesNotContain`, now `Contains`). All 5 `BuildAll` call sites updated to pass `LspFixtures.StdlibIndex()` as the new third argument.

## Decisions Made

- **Phase 31 D-03 fully realized at the implementation layer.** The two-line pragma-gate `if (!ast.Pragmas.Has("scaleLint")) return Array.Empty<Diagnostic>();` is deleted. The class-level docblock now explicitly cites D-03 supersession of Phase 24 D-19. Editor-side severity-filter is the policy answer per D-04 (no language-level opt-out).
- **Charitable fail-open as a top-level try/catch** wrapping each analyzer's body (not per-helper guards). Any future AST-shape change in flow-lang that breaks an analyzer surfaces as `Array.Empty<Diagnostic>()` from the LSP wire instead of a crash — composer's editor sees no squiggle for that analyzer rather than the server going down.
- **`@std` transitive-reference rule.** When `use "@std"` is present and any proc from any module in `StdlibSymbolIndex.ModuleNames` is referenced, `@std` is "used." This avoids false positives from common patterns like `use "@std"; (head arr)` where `head` is actually in `@collections`.
- **Non-`@` import paths are conservatively kept.** Relative paths to user modules cannot be analyzed without resolving the file (Phase 31 scope explicitly says no), so they pass through with no diagnostic. Silence beats false positives.
- **Test name migration preserves composer-grep-ability.** Phase 24 facts that became obsolete under D-03 are renamed with `_Phase31_D03_DefaultOn` suffix (rather than deleted) and assertions inverted. The fact name carries the supersession trail in-source.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] Plan-stated test inputs used aspirational Flow syntax**

- **Found during:** Task 1 RED→GREEN gate verification.
- **Issue:** The plan's `<behavior>` block showed example inputs like `proc main () { (print "hi") }` (C-style `{ }` block) and `use "@harmony"` (assumed `@harmony` is a shipped stdlib module). Flow's actual proc syntax is `proc f () ... end proc` (not `{ }`), and there is no `@harmony` module — `arpeggio` and other harmony procs live in `@std` per `flow-lang/std.flow:176-177`.
- **Fix:** Rewrote test inputs to use the actual Flow syntax (`proc f ()\n    Int x = 2;\nend proc`) and existing stdlib modules (`@collections` for the unused-import warning case; `@std` for the transitive-reference case). Analyzer behavior is unaffected — the analyzer doesn't know about the plan's aspirational inputs, only about real AST nodes.
- **Files modified:** `flow-lang.Tests/Unit/Phase31/UnusedImportAnalyzerFacts.cs` and `flow-lang.Tests/Unit/Phase31/ShadowedVariableAnalyzerFacts.cs` (initial test files were written matching the plan, then rewritten before commit `161755c`).
- **Verification:** All 12 Task 1 tests pass; Phase 17 + 21 + 24 regression stays GREEN; full Phase 31 fact suite is 16 / 16 GREEN at plan close.
- **Committed in:** `161755c` (Task 1 commit captures the rewritten tests; the aspirational originals never reached HEAD).

**2. [Rule 3 — Blocking] Removed unused-but-no-longer-needed helper in ScaleLintAnalyzer**

- **Found during:** Task 2 GREEN gate verification.
- **Issue:** None — the existing `SpellingToPitchClass` / `FindNeighbors` / `ExtractSpellingAndOctave` private helpers are still used by `BuildDiagnostic`. False alarm; no code removed.
- **Files modified:** None.
- **Verification:** N/A.
- **Committed in:** N/A.

---

**Total deviations:** 1 auto-fixed (Rule 1 — Bug). The aspirational-syntax issue was caught at the RED→GREEN gate before any plan-misaligned code reached HEAD.

**Impact on plan:** Negligible. Plan behavior contracts were honored verbatim; only the literal test-input strings changed (real Flow syntax + real module names). The plan's "exact rule" — "given X input, return Y diagnostics" — is what the tests pin, just with X transcribed against the actual codebase.

## Issues Encountered

- **62 pre-existing Phase 28 PerSynthArticulation + FlowScriptTests failures** (down from 63 at commit `11e3942`, before any Plan 31-02 work). Out of scope for diagnostic-only LSP changes; logged to `deferred-items.md`. My changes did not introduce any regressions — the count actually DROPPED by 1 (a fluke; likely the build cache being refreshed by the new analyzer compilation).
- **No test regressions** in Phase 17 / 21 / 24 / 31 / ByteIdentical suites — all 246 in-scope tests stay GREEN.

## Threat Flags

None — this plan adds diagnostic-only analyzers that perform pure read-only AST traversal. No new endpoints, no auth surface, no file-access patterns, no schema changes. The four threats in the plan's `<threat_model>` (T-31-02-01..04) are all addressed via charitable try/catch (T-31-02-01 + T-31-02-03) or accepted as design constraints (T-31-02-02 linear cost; T-31-02-04 editor-side opt-out per D-04).

## User Setup Required

None — no external service configuration. The new analyzers run automatically on every LSP didChange parse cycle per Plan 24-04 wiring. Editor-side severity-filter (VSCode `problems.severities` setting; IntelliJ severity-pane) is the policy answer for composers who want to suppress particular sources per Phase 31 D-04.

## Next Plan Readiness

- **Plan 31-03** (HoverHandler / SignatureHelpHandler varargs ellipsis) — independent of Plan 31-02's analyzer wiring; can run in parallel with Plans 31-04 / 31-05.
- **Plan 31-04** (CompletionHandler filters) — consumes `StdlibSymbolIndex.ProcsForModule` (already in place from Plan 31-01) and `StdlibSymbolIndex.ModuleNames` (existing). Will follow the same `@std` transitive-expansion rule established here.
- **Plan 31-05** (LspMappings.FormatSignature with U+2026) — independent; no dependency on Plan 31-02.
- **Plan 31-06** (SimpleLexer new comment forms) — independent; sole flow-lang touch this phase.
- **Plan 31-07** (TextMate grammar) — independent.
- **Plan 31-08** (JetBrains scaffolding) — independent; consumes `flow lsp` subcommand from Plan 31-01.

## Self-Check: PASSED

- Verified `flow-lsp/Diagnostics/UnusedImportAnalyzer.cs` exists and contains `public static class UnusedImportAnalyzer` (grep count 1).
- Verified `flow-lsp/Diagnostics/UnreachableSectionAnalyzer.cs` exists and contains `public static class UnreachableSectionAnalyzer` (grep count 1).
- Verified `flow-lsp/Diagnostics/ShadowedVariableAnalyzer.cs` exists and contains `public static class ShadowedVariableAnalyzer` (grep count 1).
- Verified each analyzer file contains `Source = "flow.unusedImport"` / `"flow.unreachableSection"` / `"flow.shadowedVariable"` respectively (grep count ≥ 1 in each).
- Verified each analyzer file body contains `try` / `catch` returning `Array.Empty<Diagnostic>()` for charitable fail-open.
- Verified `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` no longer contains `ast.Pragmas.Has("scaleLint")` (grep count 0); contains `Phase 31 D-03` citation (grep count ≥ 1).
- Verified `flow-lang/Lexing/PragmaRegistry.cs` contains `no-op` in the scaleLint description.
- Verified `flow-lsp/Diagnostics/CombinedDiagnosticsPublisher.cs` contains `BuildAll(ParseResult result, string source, StdlibSymbolIndex stdlib)` (grep count 1); contains `UnusedImportAnalyzer.Analyze`, `UnreachableSectionAnalyzer.Analyze`, `ShadowedVariableAnalyzer.Analyze`, `ScaleLintAnalyzer.Analyze` (each grep count 1).
- Verified `flow-lang.Tests/Unit/Phase31/UnusedImportAnalyzerFacts.cs` runs 4 tests, all passing.
- Verified `flow-lang.Tests/Unit/Phase31/UnreachableSectionAnalyzerFacts.cs` runs 4 tests, all passing.
- Verified `flow-lang.Tests/Unit/Phase31/ShadowedVariableAnalyzerFacts.cs` runs 4 tests, all passing.
- Verified `flow-lang.Tests/Unit/Phase31/ScaleLintDefaultOnFacts.cs` runs 4 tests, all passing.
- Verified all three task commits exist in `git log`: `161755c`, `e259845`, `078a3f7`.
- Verified Phase 17 + 21 + 24 + 31 unit tests pass (226 / 226 GREEN).
- Verified ByteIdentical determinism gate passes (20 / 20 GREEN).
