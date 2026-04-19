# Phase 13: Nyquist Validation Backfill - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-19
**Phase:** 13-nyquist-validation-backfill
**Areas discussed:** Plan structure, Test strategy, Requirements-first authorship, Phase 10 path

---

## Plan Structure

### Q1: How should plans be split across the five v1.1 phases being validated?

| Option | Description | Selected |
|--------|-------------|----------|
| One plan per phase (Recommended) | 5 plans: 13-01..13-05 per phase. Max bisectability; parallelizable. | ✓ |
| Grouped by theme | 3 plans (6+7 / 8+9 / 10). Fewer plans, harder to bisect. | |
| Single aggregate plan | 1 plan, 5 VALIDATION.md files. Poor bisectability. | |
| One plan per phase + 13-06 rollup | 6 plans with final rollup matching Phase 12 structure. | |

**User's choice:** One plan per phase (no separate rollup — 13-05 carries TEST-04 closure)
**Notes:** Parallel-independent directories; Phase 11/12 bisectability convention preserved.

### Q2: Should plans execute in parallel or sequentially?

| Option | Description | Selected |
|--------|-------------|----------|
| All parallel wave 1, rollup wave 2 (Recommended) | Five plans Wave 1, no file overlap. | ✓ |
| Sequential in phase order | 13-01 → 13-02 → ... Slower but safer. | |
| Two waves by domain | Wave 1: phases 6+7. Wave 2: phases 8+9+10. | |

**User's choice:** All parallel Wave 1
**Notes:** Combined with Q1 choice — 5 plans all in Wave 1, no separate rollup plan. 13-05 is closing plan.

---

## Test Strategy

### Q1: Where should the retroactive validation tests live?

| Option | Description | Selected |
|--------|-------------|----------|
| Native xUnit Facts + existing .flow Theory rows (Recommended) | New tests in flow-lang.Tests/Unit/ or /Integration/. Existing Theory rows cited. | ✓ |
| Pure .flow scripts wrapped via FlowScriptData | Weaker for exact-value pinning. | |
| New tests only if no existing test covers | Catalog-first approach. | |
| Validation-only xUnit project | Overkill. | |

**User's choice:** Native xUnit Facts + existing Theory rows
**Notes:** Leverages Phase 12 infrastructure; enables precise pinning per ROADMAP criterion 3.

### Q2: How should the "observable value pin" per phase be chosen?

| Option | Description | Selected |
|--------|-------------|----------|
| Error message text OR numeric durations (Recommended) | Most robust; buffer hashes forbidden. | ✓ |
| Buffer byte hashes for audio, strings for non-audio | High-maintenance. | |
| Let each plan choose based on phase content | No global rule. | |

**User's choice:** Error message text OR numeric durations; buffer byte hashes forbidden.
**Notes:** Durable across DSP refactors. Zero-crossing for fundamental-frequency checks keeps minimal-deps philosophy.

---

## Requirements-First Authorship

### Q1: How strict should "requirements-first" be in practice?

| Option | Description | Selected |
|--------|-------------|----------|
| Two-pass strict (Recommended) | Pass 1: planner reads only REQUIREMENTS.md + goal. Pass 2: executor adjusts to reality, logs as finding. | ✓ |
| Spirit-only — planner reads everything | Faster; confirmation-bias risk. | |
| Hybrid — requirement-first for assertions, code-informed for paths | Middle ground. | |

**User's choice:** Two-pass strict
**Notes:** Matches Phase 11/12's pattern of catching audit false-positives via honest requirements-first authorship.

### Q2: Should findings that diverge from REQUIREMENTS.md be surfaced or quietly fixed?

| Option | Description | Selected |
|--------|-------------|----------|
| Surface in VALIDATION.md (Recommended) | §Divergences section mirroring Phase 12 §Empirical Overrides. | ✓ |
| Reframe REQUIREMENTS.md silently | No paper trail. | |
| Block as BLOCKER | Overkill for retroactive backfill. | |

**User's choice:** Surface in VALIDATION.md §Divergences
**Notes:** Preserves Phase 12 pattern. REQUIREMENTS.md is NOT edited by Phase 13 (keep as historical record).

---

## Phase 10 Path

### Q1: How should Phase 10's existing VALIDATION.md draft be closed?

| Option | Description | Selected |
|--------|-------------|----------|
| Promote by automating what's feasible (Recommended) | Automate buffer length, non-empty, fundamental freq via zero-crossing. Keep manual for perceptual & external-command items. nyquist_compliant: true. | ✓ |
| Explicit waiver — keep nyquist_compliant: false | Simpler; less engineering. | |
| Promote fully — automate both manual items | Crude automation of perceptual items would be brittle. | |

**User's choice:** Promote by automating what's feasible
**Notes:** Flips nyquist_compliant to true. Manual-only subsection preserved for truly subjective items.

### Q2: Should Phase 10 include an FFT dependency?

| Option | Description | Selected |
|--------|-------------|----------|
| No — zero-crossing / peak detection (Recommended) | Matches minimal-deps philosophy. | ✓ |
| Yes — System.Numerics or MathNet.Numerics | Breaks minimal-deps rule. | |
| Skip FFT — structural checks only | Simpler but less informative. | |

**User's choice:** No — zero-crossing / peak detection
**Notes:** Sample-level audio assertions without any new libraries. Hand-rolled `CountZeroCrossings` helper is fine.

---

## Claude's Discretion

- Exact xUnit Fact naming conventions
- Wording of §Observable Invariants subsection entries
- Whether to share zero-crossing helpers across tests
- QOL-02 tutorial validation pattern (Theory row vs smoke Fact)
- Phase 12 VALIDATION.md promotion is OUT of Phase 13 scope per ROADMAP

## Deferred Ideas

- v1.2 phase VALIDATION.md enrichment (Phases 11, 12 still nyquist_compliant: false)
- Cross-phase Nyquist rollup doc (v1.1-MILESTONE-AUDIT.md already serves)
- FFT-based harmonic analysis for Phase 10 (if v1.3 needs deeper checks)
- Migrating CLAUDE.md "Build & Run Commands" to describe dotnet test
- Refactoring flaky/wrong existing tests discovered during Pass 2
- FIX-04 INVALID retroactive acknowledgment beyond current `~` mark
