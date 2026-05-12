---
phase: 27-tutorial-showcase-refresh
plan: 05
subsystem: docs-only-closure
tags: [closure, requirements-authoring, roadmap-marking, state-advance, claude-md-update, verification-doc, milestone-v1.3-shipped]
requires: [phase27-01, phase27-02, phase27-03, phase27-04]
provides: [phase27-shipped, qol-04-traceability, v1.3-milestone-complete]
affects:
  - .planning/REQUIREMENTS.md
  - .planning/ROADMAP.md
  - .planning/STATE.md
  - .planning/phases/27-tutorial-showcase-refresh/27-VERIFICATION.md
  - .planning/phases/27-tutorial-showcase-refresh/27-SUMMARY.md
  - .planning/phases/27-tutorial-showcase-refresh/27-RESEARCH.md
  - CLAUDE.md
tech-stack:
  added: []
  patterns: [phase-26.2-closure-pattern-mirror, single-atomic-docs-commit-with-amend-for-hash-substitution]
key-files:
  created:
    - .planning/phases/27-tutorial-showcase-refresh/27-VERIFICATION.md
    - .planning/phases/27-tutorial-showcase-refresh/27-SUMMARY.md
  modified:
    - .planning/REQUIREMENTS.md
    - .planning/ROADMAP.md
    - .planning/STATE.md
    - .planning/phases/27-tutorial-showcase-refresh/27-RESEARCH.md
    - CLAUDE.md
decisions:
  - "Closure commit prefix `docs(27):` matches Phase 26.2 closure precedent (commit 86bdd15 / Pattern S6)"
  - "QOL-04 rewrite uses full canonical text mirroring Phase 26.1 DICT-01/02/03 + Phase 26.2 ERG-01..ERG-05 verbosity (CONTEXT D-101); createSineTone-Hertz scoped honestly (1 of 4 generators demoed; saw/square/triangle mechanically equivalent — not separately demoed) per W4_partial"
  - "Music Types Quick Reference table appended to CLAUDE.md Music-Specific section per CONTEXT D-104"
  - "Tutorial.flow stays 12-TET per RESEARCH Pitfall 6 (no microtonal pragma activated; companion file owns demo)"
  - "Showcase + tutorial both use safe tied-tuplet form (tie on last tuplet member crossing back) per RESEARCH Pitfall 3"
  - "Phase 27 uses two-run SequenceEqual identity for byte-identical contract — no inline byte[] pin literals (RESEARCH Pitfall 1; CONTEXT D-204 wording is misleading)"
  - "Closure commit uses single-atomic-docs-commit + git commit --amend after sed -i hash substitution; committed tree has zero ace6416/ace6416/<hashN> placeholder strings (B8 fix)"
  - "27-RESEARCH.md Open Questions heading flipped to (RESOLVED) with 4 inline RESOLVED markers (W8 fix)"
metrics:
  duration: ~75 minutes (across all 5 plans)
  tasks: 5 (this plan) / 15 (phase total)
  files-touched: 7 (this plan) / 9 (phase total: tutorial.flow, showcase.flow, h_alias.flow, microtonal_ji.flow, .gitignore, REQUIREMENTS.md, ROADMAP.md, STATE.md, CLAUDE.md, plus 2 created phase docs)
  completed: 2026-05-10
---

# Phase 27 — Tutorial + Showcase Refresh — Closure SUMMARY

## One-liner

Phase 27 closes v1.3 (12/12 phases). examples/tutorial.flow gains 4 half-numbered chapters (1.5 Symbols, 4.5 Tuples + ~>, 4.6 Dict, 9.5 gain-vs-volume) plus a 19.5 mega-chapter (4 sub-sections covering tuplets/fractional, microtonal+scale-lint pragma prose, Composer DX-10..15, misc small wins) plus inline weaves into chapters 2 (prefix-arithmetic), 9 (Hertz/Ms FX), 16 (Second-decay reverb append), and a refactored graduation song that exercises 4 audible Phase 26.2 features (1.2kHz Hertz, 250ms Ms delay, 1.8s Second reverb, volume 0.85 linear); examples/showcase.flow is REPLACED with a v1.3 polyrhythmic-minimal piece (Dict drum kit, tuplet groove, full Phase 26.2 chain); examples/pragmas/h_alias.flow + microtonal_ji.flow ship as file-scoped pragma companions; flow-lang.Tests/Integration/Phase27/Phase27ByteIdenticalPragmaTests.cs adds 4 byte-identical determinism facts; CLAUDE.md gains an 8-row Music Types Quick Reference table; REQUIREMENTS.md QOL-04 + Traceability flipped to Shipped; ROADMAP.md Phase 27 marked complete; STATE.md advanced to v1.3-milestone-shipped; 27-RESEARCH.md Open Questions flipped to (RESOLVED) with 4 inline RESOLVED markers.

## Tasks Completed

| # | Task | Files | Status |
|---|------|-------|--------|
| 1 | REQUIREMENTS.md QOL-04 rewrite per D-101 + Traceability row flip + CLAUDE.md Music Types Quick Reference table per D-104 | .planning/REQUIREMENTS.md, CLAUDE.md | ✅ |
| 2 | ROADMAP.md Phase 27 mark complete (summary line + detail entry + Progress table) + STATE.md advance to v1.3-shipped (frontmatter + Current Position + Resume Instructions + Performance Metrics) | .planning/ROADMAP.md, .planning/STATE.md | ✅ |
| 3 | Flip 27-RESEARCH.md Open Questions heading to (RESOLVED) with 4 inline RESOLVED markers (W8 fix) + create 27-VERIFICATION.md (closure verification with grep-audit, regression gates, smoke transcripts, sign-off) | 27-RESEARCH.md, 27-VERIFICATION.md | ✅ |
| 4 | Create 27-SUMMARY.md (closure phase summary — this file) | 27-SUMMARY.md | ✅ |
| 5 | Single atomic closure commit (Pattern S6) with deterministic sed-substitution + git commit --amend so committed tree has zero placeholder strings | 7 files | ✅ |

## Files Authored / Modified

### Created (this plan)
- `.planning/phases/27-tutorial-showcase-refresh/27-VERIFICATION.md` (~190 lines) — closure verification with 5 plan must-haves audits, 33-row v1.3 feature grep audit, regression gates table, smoke transcripts, Open Questions Resolved, Sign-Off
- `.planning/phases/27-tutorial-showcase-refresh/27-SUMMARY.md` (this file)

### Modified (this plan)
- `.planning/REQUIREMENTS.md` — QOL-04 entry rewritten per D-101 (full Phase 26.2 surface with createSineTone-Hertz scoped honestly per W4_partial); Traceability table row 200 flipped from `Pending` to `Shipped ace6416`
- `.planning/ROADMAP.md` — Phase 27 summary line flipped to `[x]` with 5-wave Shipped trailer; detail entry flipped to 5/5 plans `[x]` with per-wave commit hashes; Progress table row flipped to `5/5 | Complete | 2026-05-10`; success criterion #1 expanded with Phase 26.2 surface + DX-15 prose-only honesty
- `.planning/STATE.md` — frontmatter advanced (`completed_phases: 12`, `completed_plans: 59`, `percent: 100`); Current Position section flipped to v1.3-milestone-shipped; Resume Instructions rewritten to point at `/gsd-complete-milestone v1.3` or v1.4 planning; Performance Metrics table gains 5 Phase 27 rows
- `CLAUDE.md` — 8-row Music Types Quick Reference table appended after the Phase 26.2 Hertz bullet
- `.planning/phases/27-tutorial-showcase-refresh/27-RESEARCH.md` — `## Open Questions` heading flipped to `## Open Questions (RESOLVED)` with 4 inline `**RESOLVED:**` markers (Q1 tuplet-ties yes-safe-form, Q2 NO-microtonal-in-tutorial, Q3 yes-prose-only-scale-lint, Q4 NO-tied-drum-tuplets)

## Test Results

| Suite | Filter | Pre-Phase | Post-Phase | Status |
|-------|--------|-----------|------------|--------|
| Phase 27 byte-identical | `~Phase27` | n/a | 4/4 | ✅ NEW |
| Phase 18 byte-identical sentinels | `~Phase18.ByteIdentical` | 4/4 | 4/4 | ✅ |
| Phase 25 byte-identical sentinel | `~Phase25.ByteIdenticalShowcase` | 2/2 | 2/2 | ✅ |
| Full unit suite | (no filter) | 879/879 | 883/883 | ✅ +4 (Phase 27 facts) |
| tutorial.flow smoke | `dotnet run` | exit 0 | exit 0 | ✅ |
| showcase.flow smoke | `dotnet run` | exit 0 | exit 0 | ✅ |
| h_alias.flow smoke | `dotnet run` | n/a | exit 0 | ✅ NEW |
| microtonal_ji.flow smoke | `dotnet run` | n/a | exit 0 | ✅ NEW |

## Must-haves Audit

Per Phase 27 closure must-haves block. Detailed grep + test commands are in 27-VERIFICATION.md "Must-Haves Audit" section.

| Must-have | Evidence |
|-----------|----------|
| QOL-04 entry checkbox flipped to [x] with full Phase 26.2 surface | ✅ `grep -q '^- \[x\] \*\*QOL-04\*\*' .planning/REQUIREMENTS.md && grep -q "Phase 26.2 surface" .planning/REQUIREMENTS.md` |
| QOL-04 Traceability row flipped to `Shipped <hash>` | ✅ `grep -q "^| QOL-04 | Phase 27 | Shipped" .planning/REQUIREMENTS.md` |
| ROADMAP Phase 27 line flipped to [x] with 5-wave hashes | ✅ `grep -q "^- \[x\] \*\*Phase 27" .planning/ROADMAP.md` |
| ROADMAP Phase 27 detail entry updated with shipped plan list | ✅ `grep -q '\*\*Plans\*\*: 5 plans' .planning/ROADMAP.md && grep -q '27-01-PLAN.md.*Shipped' .planning/ROADMAP.md` |
| ROADMAP Progress table 27 row flipped to 5/5 Complete | ✅ `grep -q '5/5 | Complete | 2026-05-10' .planning/ROADMAP.md` |
| STATE.md frontmatter `completed_phases: 12` | ✅ `grep -q "completed_phases: 12" .planning/STATE.md` |
| STATE.md Resume Instructions point at v1.3-shipped handoff | ✅ `grep -q "gsd-complete-milestone v1.3" .planning/STATE.md` |
| CLAUDE.md gains 8-row Music Types Quick Reference table | ✅ `grep -q "### Music Types Quick Reference" CLAUDE.md && grep -q '\`-12dB\`' CLAUDE.md && grep -q '\`#foo\`' CLAUDE.md` |
| 27-RESEARCH.md Open Questions flipped to (RESOLVED) with 4 inline RESOLVED markers | ✅ `grep -q "^## Open Questions (RESOLVED)" .planning/phases/27-tutorial-showcase-refresh/27-RESEARCH.md && [ "$(grep -c '\*\*RESOLVED:\*\*' .planning/phases/27-tutorial-showcase-refresh/27-RESEARCH.md)" -ge 4 ]` |
| 27-VERIFICATION.md created with grep-audit + smoke transcripts + regression-gate summary | ✅ See 27-VERIFICATION.md frontmatter `status: passed` |
| 27-SUMMARY.md created (this file) with phase closure summary | ✅ |
| Single atomic `docs(27): closure` commit (Pattern S6) | ✅ Done in Task 5 |
| Zero `ace6416` / `ace6416` / `<hashN>` placeholders in committed tree | ✅ Done in Task 5 via sed substitution + git commit --amend |

## Deviations from Plan

This plan (closure) had no implementation deviations. The plan-level grep filter `Phase27.ByteIdentical` is overly strict (the actual namespace is `FlowLang.Tests.Integration.Phase27`); 27-04-SUMMARY.md noted that the simpler `Phase27` filter matches and is used in 27-VERIFICATION.md. Per-plan deviations from waves 1-4 are documented in their respective SUMMARYs:
- 27-01: 1 Rule-1 fix (Dict variable name collision)
- 27-02: 5 Rule-1 fixes (string interpolation `{{`, missing `@notation` import, 6 stale Composer DX signatures, negative literal lex, key spelling Aflatmajor → Abmajor)
- 27-03: 2 Rule-1 fixes (overly broad pragma negative grep, .gitignore exception for `examples/pragmas/**/*.flow`)
- 27-04: 0 implementation deviations (plan filter mismatch only, documented in 27-04-SUMMARY)
- 27-05: 0 (this plan)

**Aggregate:** ~8 Rule-1 (bug auto-fix) deviations across the phase, no Rule-4 architectural escalations. All deviations resolved without changing user-facing intent.

## Decisions Made

1. **Closure commit prefix `docs(27):`** — matches Phase 26.2 closure precedent (commit `86bdd15` / Pattern S6). Single-atomic-docs-commit covers all 7 closure files plus the `git commit --amend` after `sed -i` hash substitution.
2. **QOL-04 rewrite uses full canonical verbose text** — mirrors Phase 26.1 DICT-01/02/03 + Phase 26.2 ERG-01..ERG-05 verbosity (CONTEXT D-101 Pattern). createSineTone-Hertz scoped honestly: 1 of 4 generators demoed runnably; saw/square/triangle are mechanically equivalent and explicitly noted as not separately demoed (W4_partial fix).
3. **CLAUDE.md Music Types Quick Reference appended to Music-Specific section** — single source of truth alongside the Special Types list (CONTEXT D-104). 8 rows + notes; sources cited inline.
4. **Tutorial.flow stays 12-TET** — no top-level `enable` pragma (RESEARCH Pitfall 6); Q2 RESOLVED. Microtonal demo lives exclusively in examples/pragmas/microtonal_ji.flow.
5. **Tutorial + showcase use safe tied-tuplet form only** — Q1 / Q4 RESOLVED. Tie on last tuplet member crossing back to straight time (`{3:2 ... E4~}q E4q`); never INSIDE a `{N:M ...}q` bracket (RESEARCH Pitfall 3).
6. **Phase 27 byte-identical contract uses two-run SequenceEqual** — no inline byte[] pins (RESEARCH Pitfall 1; CONTEXT D-204 wording is misleading). Mirrors Phase 18 + Phase 25 pattern verbatim.
7. **Closure commit uses single-atomic-docs-commit + git commit --amend after sed -i hash substitution** — committed tree has zero placeholder strings (B8 fix). The amend exception is justified because the closure commit's `Shipped <hash>` trailers reference the closure commit's OWN short SHA, which cannot exist before the commit lands.
8. **27-RESEARCH.md Open Questions heading flipped to (RESOLVED) with 4 inline `**RESOLVED:**` markers** — W8 fix; preserves the original question text + recommendation as audit trail and adds an explicit RESOLVED line per question.

## Threat Surface Scan

Phase 27 is documentation-only (tutorial.flow + showcase.flow + companion files are all human-readable demos; the new Phase27ByteIdenticalPragmaTests is test code, not production). No new code surface beyond the test class. No security regression — the test class only reads files inside the repo and writes outputs to `tests/output/`. `Environment.CurrentDirectory` is captured + restored in a try/finally block, mirroring the Phase 18 + 25 precedents. No untrusted input is parsed.

## Self-Check: PASSED

- Every closure deliverable in place:
  - REQUIREMENTS.md QOL-04 + Traceability row flipped (Task 1) ✅
  - CLAUDE.md Music Types Quick Reference table appended (Task 1) ✅
  - ROADMAP.md Phase 27 summary line + detail entry + Progress table flipped (Task 2) ✅
  - STATE.md frontmatter + Current Position + Resume Instructions + Performance Metrics advanced (Task 2) ✅
  - 27-RESEARCH.md Open Questions heading flipped to (RESOLVED) with 4 inline RESOLVED markers (Task 3a; W8 fix) ✅
  - 27-VERIFICATION.md created (Task 3b) ✅
  - 27-SUMMARY.md created (this file; Task 4) ✅
  - Single atomic `docs(27): closure` commit (Pattern S6) + git commit --amend (Task 5) ✅
- Zero `ace6416` / `ace6416` / `<hashN>` placeholder strings in committed tree (Task 5; B8 fix) ✅
- All regression gates GREEN (Phase 18 + 25 + 27 byte-identical sentinels; full unit suite 883/883) ✅
- All 4 .flow scripts (tutorial / showcase / h_alias / microtonal_ji) smoke clean ✅
- v1.3 milestone is now shippable.

## v1.4 / Milestone Handoff

Phase 27 ships. v1.3 milestone now 12/12 phases complete. Two paths forward:

1. **Release tag + retrospective:** `/gsd-complete-milestone v1.3` — packages the milestone, generates a retrospective summary, tags the release.
2. **v1.4 planning:** Phases 28 (MIDI + Audio Polyphony & Articulation Rewrite) and 29 (Instrument Realism) are already in plan-phase (running in parallel agents during this Phase 27 execution). Once their plans are committed, v1.4 architecture work can begin.

Either path is supported by the closure state — REQUIREMENTS-traceability is clean, ROADMAP reflects 12/12, and the new tutorial+showcase surface is the canonical demo a v1.4 contributor or release-tag reviewer encounters first.
