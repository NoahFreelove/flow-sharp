---
phase: 21
slug: pragma-system-h-alias
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-26
---

# Phase 21 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (existing `flow-lang.Tests` project) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase21"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~5–15s (Phase21 filter) / ~30–60s (full suite) |
| **Integration script harness** | `tests/test_*.flow` files run via `dotnet run --project flow-interpreter <path>`; success = exit 0 (no errors emitted) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase21"` (~5–15s)
- **After every plan wave:** Run `dotnet test` (full suite)
- **Before `/gsd-verify-work`:** Full suite must be green AND `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done` exits clean AND `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests` green
- **Max feedback latency:** 60s

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 21-01-* | 21-01 | 1 | PRAG-01 | T-21-01 (closed-set rejects unknown) | PragmaRegistry refuses non-allowlisted names | unit | `dotnet test --filter "FullyQualifiedName~PragmaScannerFacts"` | ❌ W0 | ⬜ pending |
| 21-01-* | 21-01 | 1 | PRAG-01 | — | — | unit | `dotnet test --filter "FullyQualifiedName~PragmaRegistryFacts"` | ❌ W0 | ⬜ pending |
| 21-01-* | 21-01 | 1 | PRAG-02 | T-21-02 (parse-time isolation) | Pragmas never enter ExecutionContext; per-file PragmaScanner in ModuleLoader | integration | `dotnet test --filter "FullyQualifiedName~PragmaIsolationFacts"` + `tests/test_pragma_isolation.flow` | ❌ W0 | ⬜ pending |
| 21-02-* | 21-02 | 2 | DEFER-02/03 | — | — | unit | `dotnet test --filter "FullyQualifiedName~HAliasFacts"` | ❌ W0 | ⬜ pending |
| 21-02-* | 21-02 | 2 | DEFER-02/03 | — | — | integration | `dotnet run --project flow-interpreter tests/test_h_alias.flow` | ❌ W0 | ⬜ pending |
| 21-02-* | 21-02 | 2 | DEFER-02/03 | — | — | integration | `dotnet run --project flow-interpreter tests/test_h_identifier.flow` | ❌ W0 | ⬜ pending |
| 21-03-* | 21-03 | 3 | (closure) | — | — | regression | `dotnet test` (full suite) + `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done` + `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests` | ✅ EXISTS | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs` — 6+ unit Facts (pre-scan algorithm, prefix accept, top-of-file enforcement, line-number preservation, duplicate silence)
- [ ] `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` — 4+ Facts (closed-set membership, alphabetized list, Levenshtein suggestion, IsKnown)
- [ ] `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs` — 7+ Facts (H acceptance under pragma, full coverage Hb/H#/+50c/dotted/tied, without-pragma rejection, Hmaj outside-stream unchanged, Token original-text preservation)
- [ ] `flow-lang.Tests/Integration/Phase21/PragmaIsolationFacts.cs` — 1 Fact: two-file fixture verifying PRAG-02 non-propagation
- [ ] `tests/test_pragma_isolation.flow` + `tests/test_pragma_isolation_module.flow` — paired fixture
- [ ] `tests/test_h_alias.flow` — DEFER-02/03 acceptance script
- [ ] `tests/test_h_identifier.flow` — `Int H = 5;` continues to compile
- [ ] No new framework install — xUnit + .NET test infra already in place per Phase 19 / 20 precedent.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| (none) | — | All Phase 21 behaviors have automated coverage above | — |

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter (set after Wave 0 lands + plan-checker green)

**Approval:** pending
