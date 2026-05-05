---
phase: 24
slug: scale-linting-flow-lsp
status: shipped
verified: 2026-05-04T18:00:00Z
verifier: gsd-executor (closure plan 24-05)
score: 3/3 ROADMAP success criteria + 23/23 locked decisions (D-01..D-23) + 68/68 Phase24 Facts + 8/8 ByteIdentical + 1/1 .flow smoke + 677/677 full suite
overrides_applied: 0
must_haves_verified: 6
must_haves_total: 6
deferred:
  - "LSP-aware editor visual squiggle rendering verification (HUMAN-UAT — wire format pinned via xUnit; rendering varies by editor)"
re_verification:
  previous_status: not yet verified
  previous_score: 0/3
  gaps_closed: []
  gaps_remaining: []
  regressions: []
gaps: []
---

# Phase 24: Scale Linting (flow-lsp) — Final Verification

**Closed:** 2026-05-04
**Plans shipped:** 6 (24-00 → 24-05)
**Goal:** Opt-in `enable scaleLint;` pragma emits Information-severity diagnostics for non-diatonic notes inside `key { ... }` contexts — zero flow-lang touch (achieved: one PragmaRegistry line).

## VERIFICATION PASSED

Phase 24 (Scale Linting, flow-lsp) goal-backward verified at the codebase level on 2026-05-04. Three LINT requirements (LINT-01 / LINT-02 / LINT-03) ship as a pure flow-lsp diagnostic-rule addition; the entire flow-lang touch is one line in `flow-lang/Lexing/PragmaRegistry.cs` registering the `"scaleLint"` pragma per Phase 21 D-17 reservation. All 3 ROADMAP success criteria are pinned by GREEN xUnit Facts at the analyzer + combined-publisher layers AND the integration smoke verifies pragma acceptance end-to-end at the runtime parse path. The Phase 18 byte-identical regression contract holds STRUCTURALLY (`examples/tutorial.flow` + `examples/showcase.flow` SHA256 unchanged vs pre-Phase-24 base `a5bab72`).

---

## Goal-Backward: ROADMAP Success Criteria → Cited Facts

| # | Criterion (ROADMAP) | Cited Fact / Smoke | Verified at Run | Commit |
|---|--------------------|-----|---|---|
| 1 | `enable scaleLint;` declared, editing `key Cmajor { \| C4 D4 E4 F#4 G4 \| }` shows an Information-severity squiggle on `F#4` (LINT-01) | `ScaleLintAnalyzerFacts.NonDiatonic_FsharpInCmajor_FlagsOneDiagnostic` (Plan 24-03 — analyzer-level: exactly one Diagnostic, Severity.Information, Source `flow.scaleLint`, Range covers F#4 token) + `CombinedDiagnosticsPublisherFacts.CombinedPublish_ScaleLintTagged_FlowScaleLint` (Plan 24-04 — wire-level Source separation) + `tests/test_scale_lint.flow` integration smoke (Plan 24-05 — closed-set pragma membership + clean parse/render) | xUnit 68/68 GREEN + smoke exit 0 with `test_scale_lint: PASSED` | 3c18795 + 96ab39c + 27a9b19 |
| 2 | Without `enable scaleLint;`, key-block with non-diatonic notes produces zero scale-lint diagnostics — opt-in only, never default-on (LINT-02) | `ScaleLintAnalyzerFacts.PragmaAbsent_NeverFlags_LINT02` (Plan 24-03 — analyzer short-circuits at D-19 entry: `Ast.Pragmas.Has("scaleLint")`) + `CombinedDiagnosticsPublisherFacts.BuildAll_PragmaAbsent_NoLintDiagnostics` + `BuildAll_PragmaAbsentWithKeyBlock_ReturnsEmpty_ClearsStaleSquiggles` (Plan 24-04 — wire-level Pitfall 6 empty-publish-clears-squiggles invariant) | xUnit GREEN | 3c18795 + 96ab39c |
| 3 | Scale linting respects nested key contexts — innermost active key wins for diagnostic computation (LINT-03 — corrected wording: `key Cmajor { key Gmajor { \| F#4 \| } }` does NOT flag F#4, F# IS diatonic in Gmajor) | `ScaleLintAnalyzerFacts.NestedKeys_InnermostWins_NoFlag` (Plan 24-03 — D-21 reuses `NoteStreamContext.FindEnclosingKey` verbatim from Phase 17, brace-depth-tracked innermost-key resolver) + `tests/test_scale_lint.flow` nested-key block | xUnit GREEN + smoke exit 0 | 3c18795 + 27a9b19 |

**Score: 3/3 ROADMAP success criteria verified at the codebase level.**

---

## Test Gates (executor ran fresh on 2026-05-04)

| Gate | Command | Expected | Observed | Status |
|---|---|---|---|---|
| Build | `dotnet build` | 0 errors | 0 errors / pre-existing warnings only | PASS |
| Phase 24 filter | `dotnet test --filter "FullyQualifiedName~Phase24"` | 68/68 GREEN | 68 passed / 0 failed | PASS |
| ByteIdentical contract | `dotnet test --filter "FullyQualifiedName~ByteIdentical"` | 8/8 GREEN (Tutorial WAV+MIDI, Showcase WAV+MIDI, Euclidean WAV+MIDI, ByteIdenticalDefaultTuning ×2) | 8 passed / 0 failed / 6s | PASS |
| Full suite | `dotnet test` | 677/677 GREEN | 677 passed / 0 failed / 24s | PASS |
| Phase 18 byte-identical SHA256 | `git show a5bab72:examples/tutorial.flow \| sha256sum` vs HEAD | unchanged | `e39d5db4...` (both) | PASS |
| Phase 18 byte-identical SHA256 | `git show a5bab72:examples/showcase.flow \| sha256sum` vs HEAD | unchanged | `97100948...` (both) | PASS |
| `.flow` smoke: scale lint | `dotnet run --project flow-interpreter tests/test_scale_lint.flow` | exit 0 + `test_scale_lint: PASSED` | exit 0; `test_scale_lint: PASSED` | PASS |
| `.flow` smoke produces WAV | `ls -la /tmp/flow_test_scale_lint.wav` | non-empty file | 705,644 bytes | PASS |
| flow-lang touch invariant | `git diff a5bab72..HEAD -- flow-lang/ \| wc -l` | minimal (one PragmaRegistry line) | one-line touch only | PASS |

**All 9 automated gates GREEN.**

---

## Decision Coverage D-01..D-23 — Implementation Sites Verified

| Decision | Description | Implementation Site | Status |
|----------|-------------|---------------------|--------|
| D-01 | Spelling-aware diatonic check (E# in Cmajor flagged AND Eb in Cmajor flagged — pitch-class match insufficient) | `flow-lsp/Diagnostics/DiatonicSpellings.cs` 119-entry hardcoded map; `ScaleLintAnalyzer` uses string spelling membership not pitch-class membership | PASS |
| D-02 | All 7 church modes covered (Major / Minor / Dorian / Phrygian / Lydian / Mixolydian / Locrian) | `DiatonicSpellings.cs` 17 roots × 7 modes = 119 entries; `ScaleDatabase.TryParseKeyWithMode` (Phase 23 D-04) reused for mode resolution | PASS |
| D-03 | Pentatonic / blues / harmonic-minor / melodic-minor / whole-tone OUT OF SCOPE | `DiatonicSpellings.cs` covers exactly 7 modes — no additional mode entries; D-22 fail-open for unrecognized modes | PASS |
| D-04 | Diatonic-spelling helper lives in flow-lsp (zero flow-lang touch) | `flow-lsp/Diagnostics/DiatonicSpellings.cs` private to flow-lsp; flow-lang receives only the one PragmaRegistry line | PASS |
| D-05 | Helper signature returns 7-string spelling set per (root, mode) | `DiatonicSpellings.GetSpellings` returns `IReadOnlyList<string>` of canonical letter+accidental strings | PASS |
| D-06 | NoteElement always checked | `ScaleLintAnalyzer` NoteElement dispatch path; `NonDiatonic_FsharpInCmajor_FlagsOneDiagnostic` Fact GREEN | PASS |
| D-07 | ChordElement recursed (each contained NoteElement checked independently) | `ScaleLintAnalyzer` ChordElement recursion; `ChordElement_NonDiatonicComponent_FlagsOneDiagnostic` Fact GREEN | PASS |
| D-08 | NoteElement with CentOffset — diatonicity by base note | `ScaleLintAnalyzer` cent-offset path; `CentOffset_BaseNoteDiatonic_NoFlag` Fact GREEN | PASS |
| D-09 | RandomChoiceElement recursed | `ScaleLintAnalyzer` RandomChoiceElement recursion; pinned by Phase24 Facts | PASS |
| D-10 | TupletElement recursed | `ScaleLintAnalyzer` TupletElement recursion; pinned by Phase24 Facts | PASS |
| D-11 | RomanNumeralElement SKIP | `ScaleLintAnalyzer` skip path for Roman numerals (diatonic-by-construction); pinned by Phase24 Facts | PASS |
| D-12 | NamedChordElement SKIP | `ScaleLintAnalyzer` skip path for chord literals (intentional declarative notation); pinned by Phase24 Facts | PASS |
| D-13 | VariableReferenceElement SKIP | `ScaleLintAnalyzer` skip path for variable refs (statically undecidable); pinned by Phase24 Facts | PASS |
| D-14 | RestElement SKIP (no pitch) | `ScaleLintAnalyzer` skip path for rests | PASS |
| D-15 | Notes outside any enclosing `key { }` block — zero diagnostics | `ScaleLintAnalyzer` null-key-context path; `FindEnclosingKey` returning null short-circuits | PASS |
| D-16 | Helpful diagnostic message format with adjacent in-scale alternatives | `ScaleLintAnalyzer` message construction; pinned by Phase24 Facts | PASS |
| D-17 | Token-wide squiggle range from `Token.Text.Length` | `ScaleLintAnalyzer.GetTokenRange`; range pinning Fact GREEN | PASS |
| D-18 | Diagnostic Source = `"flow.scaleLint"` | `ScaleLintAnalyzer` constructs Diagnostic with `Source = "flow.scaleLint"`; `CombinedDiagnosticsPublisherFacts.CombinedPublish_ScaleLintTagged_FlowScaleLint` Fact GREEN | PASS |
| D-19 | Activation gate: `Ast.Pragmas.Has("scaleLint")` short-circuits at analyzer entry | `ScaleLintAnalyzer.Analyze` D-19 short-circuit; `PragmaAbsent_NeverFlags_LINT02` Fact GREEN | PASS |
| D-20 | REPL pragma scope per-line (informational; LSP doesn't run inside REPL) | Inherits Phase 21 D-07; LSP boundary unaffected | PASS (informational) |
| D-21 | Reuse `NoteStreamContext.FindEnclosingKey` verbatim for innermost-key resolution | `ScaleLintAnalyzer` calls `FindEnclosingKey(ast, tokens, source, position)` — zero new traversal logic; `NestedKeys_InnermostWins_NoFlag` Fact GREEN | PASS |
| D-22 | Unrecognized inner key (e.g., `key Eblues`) — fail open with zero diagnostics | `ScaleLintAnalyzer` mode-parse-failure path emits empty list | PASS |
| D-23 | Pragma declared but no `key { }` block exists — zero diagnostics (no meta-diagnostic) | `ScaleLintAnalyzer` natural traversal: no key block → no element falls inside any context → empty list | PASS |

**Score: 23/23 locked decisions D-01..D-23 verified by direct codebase inspection.**

---

## must_haves Audit (per plan)

Verified that every plan's `must_haves.truths` block has shipping evidence:

| Plan | must_haves Coverage | Evidence | Status |
|------|---------------------|----------|--------|
| 24-00 (ParseSession pragma-scan widen) | `Program.Pragmas` populated in LSP context; latent Phase 17/21 hAsB-LSP bug closed; ParseSessionPragmaFacts pin regression | `flow-lsp/ParseSession.cs` Parse method widened; `ParseSessionPragmaFacts` GREEN | PASS |
| 24-01 (PragmaRegistry one-line add) | `PragmaRegistry.KnownPragmas["scaleLint"]` registered; Phase 21 fact migration with `futureUnknownPragma` sentinel; alphabetized CSV | `flow-lang/Lexing/PragmaRegistry.cs:16` registration; `PragmaRegistryScaleLintFacts` + migrated `PragmaRegistryFacts` GREEN | PASS |
| 24-02 (DiatonicSpellings helper) | 119-entry hardcoded map (17 roots × 7 modes); spelling-aware membership; D-01 invariant pinned | `flow-lsp/Diagnostics/DiatonicSpellings.cs`; `DiatonicSpellingsFacts` 12 Theory + 5 Facts GREEN | PASS |
| 24-03 (ScaleLintAnalyzer + dispatch) | AST-walking analyzer; D-01..D-23 dispatch logic; LINT-01/02/03 acceptance pinned | `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs`; `ScaleLintAnalyzerFacts` 14 Facts + 7-row mode Theory GREEN | PASS |
| 24-04 (sibling-publisher orchestration + DI) | IScaleLintPublisher analyzer-as-source interface; CombinedDiagnosticsPublisher orchestrator; Pitfall 6 source-level pin (no Count/Any guard); source-tag separation pass-through | `flow-lsp/Diagnostics/IScaleLintPublisher.cs` + `ScaleLintPublisher.cs` + `CombinedDiagnosticsPublisher.cs`; `flow-lsp/Program.cs` DI wired; `CombinedDiagnosticsPublisherFacts` 5 Facts GREEN; sibling pattern (existing IDiagnosticsPublisher untouched) | PASS |
| 24-05 (closure docs + integration smoke) | tests/test_scale_lint.flow ships and runs clean; REQUIREMENTS/ROADMAP/STATE updated; this VERIFICATION.md | `tests/test_scale_lint.flow` exit 0 with PASSED sentinel; REQUIREMENTS/ROADMAP/STATE flipped to shipped | PASS |

**Score: 6/6 plans' must_haves shipped with codebase evidence.**

---

## REQ-ID Traceability

| REQ-ID | SPEC acceptance | Pinning Artifacts | Status |
|--------|----------------|-------------------|--------|
| LINT-01 | `enable scaleLint;` + `key Cmajor { \| C4 D4 E4 F#4 G4 \| }` shows Information-severity squiggle on F#4 | `ScaleLintAnalyzerFacts.NonDiatonic_FsharpInCmajor_FlagsOneDiagnostic` + `CombinedDiagnosticsPublisherFacts.CombinedPublish_ScaleLintTagged_FlowScaleLint` + `tests/test_scale_lint.flow` smoke | Shipped Phase 24 plans 24-00..24-04 |
| LINT-02 | Pragma absent → zero scale-lint diagnostics regardless of key-block content | `ScaleLintAnalyzerFacts.PragmaAbsent_NeverFlags_LINT02` + `CombinedDiagnosticsPublisherFacts.BuildAll_PragmaAbsent_NoLintDiagnostics` + `CombinedDiagnosticsPublisherFacts.BuildAll_PragmaAbsentWithKeyBlock_ReturnsEmpty_ClearsStaleSquiggles` (Pitfall 6 source-level invariant) | Shipped Phase 24 plans 24-00..24-04 |
| LINT-03 | Nested key contexts: innermost active key wins (corrected wording — `key Cmajor { key Gmajor { \| F#4 \| } }` does NOT flag F#4) | `ScaleLintAnalyzerFacts.NestedKeys_InnermostWins_NoFlag` + `NoteStreamContext.FindEnclosingKey` reused verbatim per D-21 + `tests/test_scale_lint.flow` nested block | Shipped Phase 24 plans 24-00..24-04 |

REQUIREMENTS.md lines 79–81 + 151–153 confirm all three rows marked `[x]` / `Shipped Phase 24 plans 24-00..24-04`.

---

## Cross-cutting Concerns

| Concern | Resolution |
|---------|------------|
| Phase 17/21 latent bug — `enable hAsB;` not honored in LSP because ParseSession.Parse skipped pragma-scan stage | CLOSED by Plan 24-00 ParseSession.Parse widen mirroring FlowEngine pragma-scan pipeline; ParseSessionPragmaFacts pin the regression. Side benefit beyond Phase 24's own scope — restores Phase 21 H-alias parity in flow-lsp. |
| Phase 21 PragmaRegistryFacts negative assertion + alphabetized CSV migration | MIGRATED in Plan 24-01: sentinel `futureUnknownPragma` replaces stale `scaleLint` (now a known pragma). Alphabetized CSV: `equalTemperament, hAsB, justIntonation, pythagorean, scaleLint`. Phase 23's lower-bound count Fact (`>= 4`) still passes after Phase 24's add (count grew 4 → 5). |
| Phase 18 byte-identical regression (`examples/tutorial.flow` / `examples/showcase.flow`) | CONFIRMED CLEAN — SHA256 unchanged vs pre-Phase-24 base `a5bab72`: tutorial.flow `e39d5db4...`, showcase.flow `97100948...`. Neither file declares `enable scaleLint;`, so the new PragmaRegistry entry is unreachable at runtime; the analyzer + publisher live in flow-lsp (the LSP server is not invoked by flow-interpreter). ByteIdenticalFacts: 8/8 GREEN. |
| Closed-set growth pattern (KnownPragmas dictionary count over milestones) | KnownPragmas count: 1 (Phase 21 — `hAsB`) → 4 (Phase 23 — `+ justIntonation, pythagorean, equalTemperament`) → 5 (Phase 24 — `+ scaleLint`). The closed-set design + Phase 21's lower-bound assertion pattern accommodates incremental growth without churn. |
| Sibling-publisher pattern (Plan 24-04) preserves Phase 17 LSP infrastructure | Existing `IDiagnosticsPublisher` / `DiagnosticsPublisher` registrations in `flow-lsp/Program.cs` UNTOUCHED; `CombinedDiagnosticsPublisher` reuses `DiagnosticsPublisher.BuildDiagnostics` for parse-error mapping. Replacement would force duplication and risk Phase 17 regression. Phase 17 LSP filter shows 117/117 GREEN at closure. |

---

## Per-Plan Summary

| Plan | Outcome |
|------|---------|
| 24-00 | `flow-lsp/ParseSession.cs` Parse widened to mirror FlowEngine pragma-scan pipeline; `Program.Pragmas` now populated in LSP context; latent Phase 17/21 `enable hAsB;`-not-honored-in-LSP bug closed. ParseSessionPragmaFacts pin the regression. Shipped 6bcc697. |
| 24-01 | `flow-lang/Lexing/PragmaRegistry.cs:16` extended with `["scaleLint"] = "..."` entry per Phase 21 D-17 reservation — the only flow-lang change in Phase 24. PragmaRegistryScaleLintFacts pin the registration; Phase 21 PragmaRegistryFacts migrated for closed-set growth. Shipped 354a4de + 52a3dff. |
| 24-02 | `flow-lsp/Diagnostics/DiatonicSpellings.cs` 119-entry hardcoded map (17 roots × 7 church modes); spelling-aware membership per D-01 (Eb in Cmajor AND E# in Cmajor both flagged). DiatonicSpellingsFacts: 12 Theory rows + 5 Facts GREEN. Shipped 94ccdaf + 9eae7ae. |
| 24-03 | `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` AST-walking analyzer; D-01..D-23 element-type dispatch logic; D-19 short-circuit; D-21 reuses `NoteStreamContext.FindEnclosingKey` verbatim. ScaleLintAnalyzerFacts: 14 Facts + 7-row mode Theory pin LINT-01/02/03 + D-01..D-23. Shipped 3c18795 + 3d9233a. |
| 24-04 | `IScaleLintPublisher` analyzer-as-source interface + `ScaleLintPublisher` thin DI-mockable adapter + `CombinedDiagnosticsPublisher` orchestrator owning the single wire-level publish per parse cycle. Source-tag separation pass-through; Pitfall 6 source-level pin (no Count/Any guard); sibling pattern (existing IDiagnosticsPublisher untouched). Program.cs DI wired; `combined.Publish(uri, result, text)` replaces `diag.Publish(uri, result.Errors)` inside close-race guard. CombinedDiagnosticsPublisherFacts: 5 Facts GREEN. Shipped 0dc9a99 + b0b9971 + 96ab39c. |
| 24-05 | `tests/test_scale_lint.flow` integration smoke pinning LINT-01 / LINT-03 patterns end-to-end at runtime parse path; closed-set membership integration check (D-12 unknown-pragma error if registry add was missed). REQUIREMENTS LINT-01/02/03 flipped to Shipped + LINT-03 wording bug closed (Aminor → Gmajor). ROADMAP Phase 24 row Complete + 6/6 plans + Phase 25 unblocked. STATE current_phase advanced to 25 with closure anchor. This 24-VERIFICATION.md report. Shipped 27a9b19 + dbaa14e + e4745d4 + (this commit). |

---

## Behavioral Spot-Checks (executor-run)

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Scale-lint pragma accepted by closed registry | `dotnet run --project flow-interpreter tests/test_scale_lint.flow` | exit 0; `test_scale_lint: PASSED` | PASS |
| F#4 in Cmajor flagged at analyzer level | `dotnet test --filter "FullyQualifiedName~NonDiatonic_FsharpInCmajor_FlagsOneDiagnostic"` | Passed | PASS |
| Pragma absent → zero diagnostics | `dotnet test --filter "FullyQualifiedName~PragmaAbsent_NeverFlags_LINT02"` | Passed | PASS |
| Innermost key wins (Gmajor inner) | `dotnet test --filter "FullyQualifiedName~NestedKeys_InnermostWins_NoFlag"` | Passed | PASS |
| Source tag separation: scale-lint tagged `flow.scaleLint` | `dotnet test --filter "FullyQualifiedName~CombinedPublish_ScaleLintTagged_FlowScaleLint"` | Passed | PASS |
| Phase 18 byte-identical regression: examples/tutorial.flow unchanged | `git show a5bab72:examples/tutorial.flow \| sha256sum && sha256sum examples/tutorial.flow` | both `e39d5db4...` | PASS |
| Phase 18 byte-identical regression: examples/showcase.flow unchanged | `git show a5bab72:examples/showcase.flow \| sha256sum && sha256sum examples/showcase.flow` | both `97100948...` | PASS |

---

## Anti-Pattern Scan (executor-run)

| File | Pattern | Severity | Disposition |
|------|---------|----------|-------------|
| `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` | TODO/FIXME comments | n/a | None found in modified scope |
| `flow-lsp/Diagnostics/DiatonicSpellings.cs` | Empty implementations / placeholders | n/a | None — 119 entries fully populated |
| `flow-lsp/Diagnostics/CombinedDiagnosticsPublisher.cs` | Pitfall 6 violation (Count/Any guard around PublishDiagnostics) | n/a | NONE — verified at source level by Plan 24-04 (`grep -B 1 'PublishDiagnostics' \| grep -E 'if.*\(.*Count\|if.*\(.*Any'` returns no matches) |
| All Phase 24 facts | `[Fact(Skip=...)]` skip markers | n/a | None — verified all 68 facts run unconditionally (no skips) |
| flow-lang touch beyond one PragmaRegistry line | `git diff a5bab72..HEAD -- flow-lang/` | n/a | One-line touch only — zero unintended flow-lang changes |

No blockers, no warnings, no info items. The phase ships clean.

---

## Manual UAT (Outstanding — non-blocking)

| Behavior | Why Manual | Test Instructions | Status |
|----------|------------|-------------------|--------|
| Information-severity squiggle renders in editor under `Source: flow.scaleLint` filter | LSP wire format is asserted via xUnit; the actual rendering varies by editor (VS Code / Neovim / Helix) | Open `tests/test_scale_lint.flow` in a flow-lsp-aware editor with `enable scaleLint;` declared; confirm `F#4` shows an Information-severity squiggle (typically blue/teal underline). Confirm `Source: flow.scaleLint` filter hides scale-lint diagnostics independently of parse errors. | DEFERRED to v1.3 milestone HUMAN-UAT roll-up (Phase 17 precedent) |

This does **not** block phase closure — tracked alongside v1.2-era Phase 17 HUMAN-UAT items.

---

## Deferred-Items Handoff

No new deferred items introduced by Phase 24. Out-of-scope items per CONTEXT `<deferred>` section:

- Pentatonic / blues / harmonic-minor / melodic-minor / whole-tone scale support (future phase or v1.4)
- Quick-fix code actions (Phase 17 precedent — defers to a future code-actions phase)
- Borrowed-chord / modal-mixture / Roman-numeral mismatch warnings (future "harmonic-aware lint" phase)
- Hover-rich diagnostic detail (DA-6 chose helpful inline message instead)
- Configurable diagnostic severity (not in REQ; future config flag if needed)
- "Did you mean a different mode?" mode-suggestion (interesting future enhancement)
- CLI lint mode (would require promoting `DiatonicSpellings.cs` to flow-lang; YAGNI now)
- Default-on scale linting (explicit anti-feature per REQUIREMENTS.md line 113)

---

## Final Acceptance — Phase 24 Closes

- [x] All 3 ROADMAP success criteria verified by GREEN xUnit Facts at the analyzer + combined-publisher layers AND a GREEN `.flow` integration smoke
- [x] All 23 locked decisions D-01..D-23 verified by direct codebase inspection
- [x] All 3 REQ-IDs (LINT-01, LINT-02, LINT-03) marked `Shipped Phase 24 plans 24-00..24-04` in REQUIREMENTS.md (with LINT-03 wording bug corrected: Aminor → Gmajor)
- [x] ROADMAP.md Phase 24 row marked complete (Shipped 2026-05-04, 6/6 plans)
- [x] STATE.md milestone progress 6/10 → 7/10 phases for v1.3; current focus advanced to Phase 25
- [x] Full xUnit suite 677/677 GREEN, 0 failures, 0 skips (`dotnet test`)
- [x] Phase 24 filter 68/68 GREEN (`dotnet test --filter "FullyQualifiedName~Phase24"`)
- [x] Phase 17 LSP filter 117/117 GREEN — sibling-publisher pattern preserves existing infrastructure
- [x] Phase 18 byte-identical regression contract preserved: 8/8 ByteIdenticalFacts GREEN; SHA256 unchanged for examples/tutorial.flow + examples/showcase.flow vs pre-Phase-24 base a5bab72
- [x] `tests/test_scale_lint.flow` integration smoke exit 0 with `test_scale_lint: PASSED` sentinel; `/tmp/flow_test_scale_lint.wav` produced
- [x] Build is clean: 0 errors (`dotnet build`)
- [x] Zero flow-lang touch beyond one PragmaRegistry line confirmed via `git diff a5bab72..HEAD -- flow-lang/`
- [x] Anti-pattern scan clean — no TODO/FIXME/stub/skip markers in modified scope; Pitfall 6 source-level pin verified

---

## Approval

_Reserved for /gsd-verify-work output._

---

*Phase: 24-scale-linting-flow-lsp*
*Verified: 2026-05-04 (executor closure plan 24-05)*
*Verifier: Claude (gsd-executor)*
*Goal: opt-in `enable scaleLint;` pragma activates flow-lsp scale linting — zero flow-lang touch beyond one PragmaRegistry line — ACHIEVED*
