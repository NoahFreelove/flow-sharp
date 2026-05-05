---
phase: 24
slug: scale-linting-flow-lsp
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-04
---

# Phase 24 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (.NET 10) + .flow integration smokes |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (already references both `flow-lang` and `flow-lsp` — no Wave 0 framework install needed) |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase24"` |
| **Full suite command** | `dotnet test` then `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done` |
| **Estimated runtime** | ~30s xUnit Phase 24 filter; ~2m full xUnit + ~3m .flow smokes |

---

## Sampling Rate

- **After every task commit:** Run quick filter (`dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase24"`)
- **After every plan wave:** Run full xUnit suite (`dotnet test`)
- **Before `/gsd-verify-work`:** Full suite + Phase 18 byte-identical regression on `tutorial.flow` / `showcase.flow` must be green
- **Max feedback latency:** ~30 seconds for the per-task filter

---

## Per-Task Verification Map

> Filled by planner. Each task ships with an `<automated>` verify command (xUnit Fact name or `.flow` script invocation) OR an explicit Wave 0 dependency.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 24-00-* | 24-00 | 0 | LINT-01 (precondition) | — | ParseSession scans pragmas in LSP | unit | `dotnet test --filter "FullyQualifiedName~ParseSessionPragmaFacts"` | ❌ W0 | ⬜ pending |
| 24-01-* | 24-01 | 1 | LINT-02 (gate) | — | `scaleLint` is a known pragma | unit | `dotnet test --filter "FullyQualifiedName~PragmaRegistryScaleLintFacts"` | ❌ W0 | ⬜ pending |
| 24-02-* | 24-02 | 1 | LINT-01 (helper) | — | DiatonicSpellings returns correct 7-note set per (root,mode) | unit | `dotnet test --filter "FullyQualifiedName~DiatonicSpellingsFacts"` | ❌ W0 | ⬜ pending |
| 24-03-* | 24-03 | 2 | LINT-01 / LINT-03 (analyzer) | — | Analyzer flags only non-diatonic NoteElement / ChordElement / RandomChoice / Tuplet recursions; respects D-21 innermost-key | unit | `dotnet test --filter "FullyQualifiedName~ScaleLintAnalyzerFacts"` | ❌ W0 | ⬜ pending |
| 24-04-* | 24-04 | 3 | LINT-01 / LINT-02 (wiring) | — | didChange publishes parse errors + lint as a single LSP `publishDiagnostics`; pragma-absent → zero scaleLint diagnostics | unit + integration | `dotnet test --filter "FullyQualifiedName~CombinedDiagnosticsPublisherFacts"` | ❌ W0 | ⬜ pending |
| 24-05-* | 24-05 | 4 | LINT-01 / LINT-02 / LINT-03 (acceptance) | — | `tests/test_scale_lint.flow` smoke + REQUIREMENTS / ROADMAP / STATE / VERIFICATION closure | integration | `dotnet run --project flow-interpreter tests/test_scale_lint.flow && grep -q "LINT-01.*✓" .planning/phases/24-scale-linting-flow-lsp/24-VERIFICATION.md` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Unit/Phase24/ParseSessionPragmaFacts.cs` — exercises that `ParseSession.Parse(source)` populates `ParseResult.Ast.Pragmas` (validates the precondition fix, also covers the latent `enable hAsB;` LSP gap surfaced by research)
- [ ] `flow-lang.Tests/Unit/Phase24/PragmaRegistryScaleLintFacts.cs` — asserts `PragmaRegistry.KnownPragmas` contains `"scaleLint"`; migrates the closed-set assertion patterns from `Phase21/PragmaRegistryFacts.cs:28,39`
- [ ] `flow-lang.Tests/Unit/Phase24/DiatonicSpellingsFacts.cs` — `[Theory]` over the 17 roots × 7 modes = 119 entries; per-mode spot Facts for spelling-aware E#4/Gb4 corner cases
- [ ] `flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerFacts.cs` — covers D-06..D-15 element traversal (recurse vs SKIP), D-22 fail-open on unknown modes, D-23 silence when no key block exists
- [ ] `flow-lang.Tests/Unit/Phase24/CombinedDiagnosticsPublisherFacts.cs` — single-publish merge invariant (parse errors + lint share one `publishDiagnostics` call); empty-publish-clears-squiggles preserved; covers the LINT-01/02/03 wiring acceptance via `BuildAll` static composer + the `ScaleLintAnalyzerFacts` end-to-end traversal in 24-03
- [ ] `tests/test_scale_lint.flow` — opt-in smoke (LINT-01 verbatim with `enable scaleLint; key Cmajor { | C4 D4 E4 F#4 G4 | }`), opt-out smoke (no pragma → zero diagnostics, LINT-02), nested-key smoke (`key Cmajor { key Gmajor { | F#4 | } }` produces zero diagnostics, LINT-03) — combined with `ScaleLintAnalyzerFacts` (24-03) + `CombinedDiagnosticsPublisherFacts` (24-04), this provides the full end-to-end coverage that an `ScaleLintIntegrationFacts.cs` would otherwise duplicate

*Existing infrastructure covers test framework + LSP host bootstrap. No new csproj or NuGet package required (D-04 "zero flow-lang touch" beyond the one PragmaRegistry line).*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Information-severity squiggle renders in editor under `flow.scaleLint` source filter | LINT-01 (visual) | LSP wire format is asserted via xUnit; the actual rendering is editor-controlled and varies (VS Code, Neovim, Helix) | Open `tests/test_scale_lint.flow` in a flow-lsp-aware editor with `enable scaleLint;` declared; confirm `F#4` shows an Information-severity squiggle (typically blue/teal underline). Confirm `Source: flow.scaleLint` filter hides scale-lint diagnostics independently of parse errors. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (the 7 Wave-0 files above)
- [ ] No watch-mode flags (`dotnet test` is one-shot; `.flow` smokes are one-shot)
- [ ] Feedback latency < 30s for the Phase24 filter
- [ ] Phase 18 byte-identical regression gate verified (`tutorial.flow` / `showcase.flow` SHA256 unchanged after merge — D-04 "zero flow-lang touch" invariant)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
