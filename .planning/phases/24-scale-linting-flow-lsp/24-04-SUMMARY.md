---
phase: 24-scale-linting-flow-lsp
plan: 04
subsystem: lsp
tags: [lsp, dependency-injection, publish-diagnostics, sibling-publisher, orchestrator-pattern, scale-lint, phase-24, wave-3]

# Dependency graph
requires:
  - phase: 24-scale-linting-flow-lsp/24-03
    provides: ScaleLintAnalyzer.Analyze static method returning IReadOnlyList<Diagnostic>
  - phase: 24-scale-linting-flow-lsp/24-00
    provides: ParseSession populating Ast.Pragmas (D-19 activation gate)
  - phase: 17-flow-language-server
    provides: IDiagnosticsPublisher / DiagnosticsPublisher (sibling pattern target), DocumentManager onParse closure pattern
provides:
  - IScaleLintPublisher analyzer-as-source interface (RESEARCH §Pattern 1 Shape A)
  - ScaleLintPublisher concrete adapter delegating to ScaleLintAnalyzer.Analyze
  - CombinedDiagnosticsPublisher orchestrator owning the single PublishDiagnostics call per parse cycle
  - Program.cs DI wiring with combined publisher invoked from DocumentManager onParse closure
  - End-to-end LSP wire-level acceptance for LINT-01 / LINT-02 (editor receives Information squiggles when `enable scaleLint;` declared)
affects:
  - 24-05 (phase closure with .flow integration smoke + REQUIREMENTS/ROADMAP/STATE updates)

# Tech tracking
tech-stack:
  added: []  # Zero new external dependencies — all hand-rolled
  patterns:
    - "Sibling-publisher orchestration: parse-error publisher and lint analyzer are two diagnostic sources, one orchestrator owns the wire-level publish"
    - "Analyzer-as-source: lint interface returns IReadOnlyList<Diagnostic> rather than publishing directly (deviates from IDiagnosticsPublisher analog)"
    - "Source-tag separation: parse errors keep Source='flow', scale-lint keeps Source='flow.scaleLint' — neither side rewrites the other"
    - "Empty-publish-clears-squiggles invariant pinned at source level (no Count/Any guard around PublishDiagnostics)"

key-files:
  created:
    - flow-lsp/Diagnostics/IScaleLintPublisher.cs
    - flow-lsp/Diagnostics/ScaleLintPublisher.cs
    - flow-lsp/Diagnostics/CombinedDiagnosticsPublisher.cs
    - flow-lang.Tests/Unit/Phase24/CombinedDiagnosticsPublisherFacts.cs
  modified:
    - flow-lsp/Program.cs

key-decisions:
  - "Sibling pattern (NOT replacement): existing IDiagnosticsPublisher/DiagnosticsPublisher kept registered because CombinedDiagnosticsPublisher.BuildAll reuses DiagnosticsPublisher.BuildDiagnostics for parse-error mapping"
  - "IScaleLintPublisher returns IReadOnlyList<Diagnostic> (does NOT publish) — deliberate shape deviation from IDiagnosticsPublisher analog so the orchestrator owns the single wire-level PublishDiagnostics call per LSP REPLACE semantics"
  - "Pitfall 6 source-level pin: Publish calls _server.TextDocument.PublishDiagnostics UNCONDITIONALLY; no Count/Any guard. Empty publish is the only way to clear stale squiggles"
  - "Static BuildAll exposed for unit-testable composition without standing up an ILanguageServerFacade — mirrors DiagnosticsPublisher.BuildDiagnostics convention"
  - "Source-tag separation pass-through: CombinedDiagnosticsPublisher does NOT rewrite Source on either side — parse errors keep 'flow', lint keeps 'flow.scaleLint'"

patterns-established:
  - "Sibling-publisher orchestration: when a new diagnostic source needs to publish alongside an existing one over an LSP REPLACE channel, build an orchestrator that owns the single wire-level call rather than chaining publishers"
  - "Analyzer-as-source interface shape: IFooPublisher.Analyze(...) returns IReadOnlyList<Diagnostic> for orchestrated wire-level merging"
  - "Source-grep acceptance criterion for invariants difficult to behavior-test: when stubbing an external API (e.g., ILanguageServerFacade) is disproportionate to the risk, pin the invariant at source-text level via grep"

requirements-completed: [LINT-01, LINT-02]

# Metrics
duration: 4min
completed: 2026-05-04
---

# Phase 24 Plan 04: IScaleLintPublisher + CombinedDiagnosticsPublisher + Program.cs DI Wiring Summary

**Wire scale-lint into the LSP publish pipeline as a sibling-publisher orchestrated through CombinedDiagnosticsPublisher; parse errors and scale-lint diagnostics now merge into a single PublishDiagnostics call per parse cycle, preserving Source-tag separation and the empty-publish-clears-squiggles invariant.**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-05-04T17:35:50Z
- **Completed:** 2026-05-04T17:39:35Z
- **Tasks:** 3
- **Files created:** 4 (3 production + 1 test)
- **Files modified:** 1 (flow-lsp/Program.cs)

## Accomplishments
- Shipped `IScaleLintPublisher` analyzer-as-source interface returning `IReadOnlyList<Diagnostic>` (RESEARCH §Pattern 1 Shape A)
- Shipped `ScaleLintPublisher` thin DI-mockable adapter delegating to `ScaleLintAnalyzer.Analyze` (Plan 24-03 output)
- Shipped `CombinedDiagnosticsPublisher` orchestrator owning the single `_server.TextDocument.PublishDiagnostics` call per parse cycle, with both static `BuildAll` (unit-testable) and instance `Publish` (wire-level) entry points
- Wired DI registrations in `flow-lsp/Program.cs` and replaced `diag.Publish(uri, result.Errors)` with `combined.Publish(uri, result, text)` in the `DocumentManager` onParse closure (close-race guard preserved)
- 5 RED→GREEN Facts pin the merge invariant: empty-list (clears squiggles), parse-error tag separation ("flow"), lint tag separation ("flow.scaleLint"), pragma-absent silence (LINT-02 wire-level), Pitfall 6 empty-publish path
- Preserved sibling pattern: existing `IDiagnosticsPublisher` / `DiagnosticsPublisher` untouched; flow-lang/ entirely untouched
- Phase 17 LSP regression: 117/117 pass; Phase 24 (all plans): 68/68 pass; full suite: 673/673 pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Add 5 RED Facts pinning the combined-publisher merge invariant** — `0dc9a99` (test)
2. **Task 2: Ship IScaleLintPublisher + ScaleLintPublisher + CombinedDiagnosticsPublisher** — `b0b9971` (feat)
3. **Task 3: Wire CombinedDiagnosticsPublisher into Program.cs DI + onParse closure** — `96ab39c` (feat)

## Files Created/Modified

### Created

- `flow-lsp/Diagnostics/IScaleLintPublisher.cs` — Analyzer-as-source interface returning `IReadOnlyList<Diagnostic>`. Documents the deliberate shape deviation from `IDiagnosticsPublisher` analog (returns rather than publishes) so the orchestrator owns the single wire-level publish call.
- `flow-lsp/Diagnostics/ScaleLintPublisher.cs` — Thin DI-mockable adapter delegating to `ScaleLintAnalyzer.Analyze`. The D-19 short-circuit (`Ast.Pragmas.Has("scaleLint")`) lives inside the analyzer; this class adds zero decision logic.
- `flow-lsp/Diagnostics/CombinedDiagnosticsPublisher.cs` — Orchestrator owning the single `_server.TextDocument.PublishDiagnostics` call per parse cycle. Static `BuildAll(ParseResult, string)` for unit tests; instance `Publish(DocumentUri, ParseResult, string)` for wire-level dispatch. Source-tag separation pass-through (parse errors keep `"flow"`; lint keeps `"flow.scaleLint"`). Pitfall 6 source-level pin: `PublishDiagnostics` invoked unconditionally — no `if (merged.Count > 0)` or `if (merged.Any())` guard.
- `flow-lang.Tests/Unit/Phase24/CombinedDiagnosticsPublisherFacts.cs` — 5 Facts: `BuildAll_NoErrorsNoLint_ReturnsEmpty`, `CombinedPublish_ParseErrorsTagged_Flow`, `CombinedPublish_ScaleLintTagged_FlowScaleLint`, `BuildAll_PragmaAbsent_NoLintDiagnostics`, `BuildAll_PragmaAbsentWithKeyBlock_ReturnsEmpty_ClearsStaleSquiggles`. RED proven before Task 2; all GREEN after.

### Modified

- `flow-lsp/Program.cs`:
  - Added `using FlowLsp.Diagnostics;`
  - Added `.AddSingleton<IScaleLintPublisher, ScaleLintPublisher>()` and `.AddSingleton<CombinedDiagnosticsPublisher>()` after the existing `IDiagnosticsPublisher` registration (sibling, not replacement)
  - Replaced `diag.Publish(uri, result.Errors)` with `combined.Publish(uri, result, text)` inside the `if (dm!.HasDocument(uri))` close-race guard
  - Preserved `users.Update(uri, result.Ast)` (Phase 17 UserSymbolIndex update)
  - Preserved `if (dm!.HasDocument(uri))` close-race guard (Phase 17 D-23 invariant)
  - Preserved existing `IDiagnosticsPublisher` and `DiagnosticsPublisher` registrations (sibling pattern; CombinedDiagnosticsPublisher.BuildAll reuses `DiagnosticsPublisher.BuildDiagnostics`)

## Decisions Made

- **Sibling pattern preserved** — existing `IDiagnosticsPublisher` and `DiagnosticsPublisher` stay registered untouched. The new orchestrator REUSES `DiagnosticsPublisher.BuildDiagnostics` for parse-error → Diagnostic mapping; replacement would force duplication.
- **IScaleLintPublisher.Analyze returns rather than publishes** (RESEARCH §Pattern 1 Shape A) — deviates from `IDiagnosticsPublisher.Publish(uri, errors)` shape because LSP `publishDiagnostics` REPLACES per-URI; if both sources tried to publish independently the second call would clobber the first. Inline doc-comment in `IScaleLintPublisher.cs` documents the shape deviation.
- **Source-tag separation is pass-through** — `CombinedDiagnosticsPublisher` does NOT rewrite `Source` on either side. Parse errors keep `"flow"` (set by `DiagnosticsPublisher.BuildDiagnostics`), scale-lint keeps `"flow.scaleLint"` (set by `ScaleLintAnalyzer`). Editors filter independently.
- **Pitfall 6 source-level pin** — instance `Publish` calls `_server.TextDocument.PublishDiagnostics` UNCONDITIONALLY. No `Count > 0` or `Any()` guard. Verifiable at source level: `grep -B 1 'PublishDiagnostics' flow-lsp/Diagnostics/CombinedDiagnosticsPublisher.cs | grep -E 'if.*\(.*Count|if.*\(.*Any'` returns no matches. Behavior-level testing of the wire path requires stubbing OmniSharp's full `ILanguageServerFacade` interface tree, which is disproportionate to the risk; the source-grep is the proportionate compensating control (per plan threat model T-24-04-05).
- **Static `BuildAll` exposed alongside instance `Publish`** — mirrors `DiagnosticsPublisher.BuildDiagnostics` convention so unit tests can exercise the merge logic without standing up an `ILanguageServerFacade`. Used by all 5 Facts in this plan.

## Deviations from Plan

None — plan executed exactly as written. Task 1 produced 5 Facts (matching the W3 revision noted in the plan); Task 2 shipped 3 production files turning RED→GREEN; Task 3 rewired Program.cs DI and onParse closure exactly per the plan's `<action>` block.

The plan's frontmatter `must_haves.artifacts` listed 4 expected Facts (`BuildAll_BothPresent_UnionContent` was the 4th), but the plan body's `<acceptance_criteria>` and `<action>` block specified 5 Facts (replacing the synthetic `BothPresent` Fact with the more rigorous `BuildAll_PragmaAbsent_NoLintDiagnostics` and `BuildAll_PragmaAbsentWithKeyBlock_ReturnsEmpty_ClearsStaleSquiggles`). I followed the body specification (5 Facts) per the plan's own intent — the body is the authoritative artifact spec, and the body explicitly notes this revision.

## Issues Encountered

None — the plan was tightly specified, all required prior-wave outputs (ParseSession with Pragmas, DiatonicSpellings, ScaleLintAnalyzer) were already in place, and the IDiagnosticsPublisher analog provided a clean shape to mirror.

## User Setup Required

None — no external service configuration required. The combined publisher is internal LSP plumbing.

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet build` | 0 Error(s), 12 pre-existing xUnit warnings |
| `dotnet test --filter "FullyQualifiedName~CombinedDiagnosticsPublisherFacts"` | 5/5 passed |
| `dotnet test --filter "FullyQualifiedName~Phase24"` | 68/68 passed |
| `dotnet test --filter "FullyQualifiedName~Phase17"` | 117/117 passed (zero LSP regression) |
| Full suite | 673/673 passed |
| `grep -c 'using FlowLsp\.Diagnostics' flow-lsp/Program.cs` | 1 |
| `grep -c 'AddSingleton<IScaleLintPublisher' flow-lsp/Program.cs` | 1 |
| `grep -c 'AddSingleton<CombinedDiagnosticsPublisher' flow-lsp/Program.cs` | 1 |
| `grep -c 'combined\.Publish' flow-lsp/Program.cs` | 1 |
| `grep -c 'diag\.Publish(uri, result\.Errors)' flow-lsp/Program.cs` | 0 (replaced) |
| `grep -c 'users\.Update' flow-lsp/Program.cs` | 1 (preserved) |
| `grep -c 'HasDocument(uri)' flow-lsp/Program.cs` | 1 (close-race guard preserved) |
| `grep -c 'AddSingleton<IDiagnosticsPublisher>' flow-lsp/Program.cs` | 1 (sibling pattern) |
| Pitfall 6 source-level pin: `grep -B 1 'PublishDiagnostics' flow-lsp/Diagnostics/CombinedDiagnosticsPublisher.cs \| grep -E 'if.*\(.*Count\|if.*\(.*Any'` | (no matches — PASS) |
| `git diff --name-only flow-lsp/Handlers/` | (empty — DiagnosticsPublisher.cs unchanged) |
| `git diff --name-only flow-lang/` | (empty — flow-lang untouched) |

## Next Phase Readiness

- LSP wire-level acceptance for LINT-01 / LINT-02 achieved: an editor opening a `.flow` file with `enable scaleLint;` declared will receive Information-severity squiggles via the merged `publishDiagnostics` call.
- Plan 24-05 ready: phase closure with the `.flow` integration smoke + REQUIREMENTS/ROADMAP/STATE finalization.
- Sibling pattern keeps Phase 17 LSP infrastructure intact — no regression risk for completion / hover / signature-help / definition handlers.
- Phase 18 byte-identical regression: not applicable. All changes are LSP-only; flow-lang untouched; tutorial.flow / showcase.flow have no LSP path.

## Self-Check: PASSED

- [x] `flow-lsp/Diagnostics/IScaleLintPublisher.cs` exists
- [x] `flow-lsp/Diagnostics/ScaleLintPublisher.cs` exists
- [x] `flow-lsp/Diagnostics/CombinedDiagnosticsPublisher.cs` exists
- [x] `flow-lang.Tests/Unit/Phase24/CombinedDiagnosticsPublisherFacts.cs` exists
- [x] `flow-lsp/Program.cs` modified
- [x] Commit `0dc9a99` exists (Task 1 — test RED)
- [x] Commit `b0b9971` exists (Task 2 — feat GREEN)
- [x] Commit `96ab39c` exists (Task 3 — feat DI wiring)

---
*Phase: 24-scale-linting-flow-lsp*
*Plan: 04 (Wave 3)*
*Completed: 2026-05-04*
