---
phase: 42
slug: type-system-stdlib-audit
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-24
---

# Phase 42 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (existing `flow-lang.Tests/`) + standalone audit console programs (Phase 42 ships ~50-LOC harnesses, not production code) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (existing); new audit harness lives at `flow-lang.Tests/Audit/` or as a standalone console under `tools/audit/` (planner decides) |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "Category=Audit" --logger "console;verbosity=minimal"` |
| **Full suite command** | `dotnet test flow-lang.Tests` (full existing suite — must stay green; Phase 42 changes nothing production) |
| **Estimated runtime** | Audit: ~5–10s · Full suite: ~60–90s |

---

## Sampling Rate

- **After every task commit:** Run audit-category quick command
- **After every plan wave:** Run full suite (`dotnet test flow-lang.Tests`) — guards the "Phase 42 ships AUDIT.md only, zero production changes" invariant
- **Before `/gsd:verify-work`:** Full suite must be green AND AUDIT.md must exist with non-zero gap rows
- **Max feedback latency:** ~10s (audit run); ~90s (full suite, after each wave)

---

## Per-Task Verification Map

*Filled in by the planner once tasks are enumerated. Skeleton rows below — replace with concrete plan/task IDs after PLAN.md files are written.*

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 42-01-01 | 01 | 1 | REQ-AUDIT-01 | — | Harness enumerates every `FlowType` instance and every `FunctionSignature` parameter/return type without throwing | unit | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~AuditHarness"` | ❌ W0 | ⬜ pending |
| 42-02-01 | 02 | 2 | REQ-AUDIT-02 | — | AUDIT.md emitted with the 5 gap classes (orphaned / missing-conversion / asymmetric-pair / dead-end / overload-gap) and severity scoring | source | `test -s .planning/phases/42-type-system-stdlib-audit/42-AUDIT.md && grep -c '^## ' .planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` | ❌ W0 | ⬜ pending |
| 42-03-01 | 03 | 3 | REQ-AUDIT-03 | — | Existing 70+ `.flow` test scripts and `flow-lang.Tests` suite all still green (no production regressions from audit-only phase) | integration | `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done && dotnet test flow-lang.Tests` | ✅ | ⬜ pending |

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Audit/AuditHarnessTests.cs` — stubs for REQ-AUDIT-01 (graph extractor must enumerate without throwing)
- [ ] `flow-lang.Tests/Audit/AuditReportTests.cs` — stubs for REQ-AUDIT-02 (AUDIT.md schema sanity: required sections present)
- [ ] No new framework install — xUnit already wired

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| AUDIT.md prioritization is composer-impact-relevant | REQ-AUDIT-04 | "Severity" is a judgment call — the harness can categorize gaps mechanically but cannot decide which orphan most hurts a composer's ergonomics; needs human review | Read AUDIT.md sections, confirm each P1 entry has a concrete composer scenario (e.g., "Beat→Second missing breaks `tempo 120 { (delay buf 1.5) }`"), confirm each gap is routed to Phase 43, Phase 44, or v1.6-backlog with rationale |
| AUDIT.md findings cross-checked against `.flow` stdlib | REQ-AUDIT-05 | A function that *looks* dead-end in C# may be the only API for a `.flow` stdlib module (e.g. `audio.flow`, `sfz.flow`); the audit harness greps these but human spot-check confirms no false-positive deprecation candidates | For each "dead-end builtin" entry, grep `flow-lang/*.flow` for the function name; confirm the AUDIT.md row matches the grep result (true dead-end vs. .flow-only consumer) |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s (audit) / 90s (full suite)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
