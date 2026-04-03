---
phase: 04
slug: composition-tools
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-02
---

# Phase 04 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | .flow test scripts (no unit test framework) |
| **Config file** | none — standalone .flow scripts in tests/ |
| **Quick run command** | `dotnet run --project flow-interpreter tests/test_progression.flow` |
| **Full suite command** | `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done` |
| **Estimated runtime** | ~60 seconds (full suite) |

---

## Sampling Rate

- **After every task commit:** Run quick command for the relevant test file
- **After every plan wave:** Run full suite
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 04-01-01 | 01 | 1 | COMP-01 | integration | `dotnet run --project flow-interpreter tests/test_progression.flow` | W0 | pending |
| 04-01-02 | 01 | 1 | COMP-02 | integration | `dotnet run --project flow-interpreter tests/test_progression.flow` | W0 | pending |
| 04-02-01 | 02 | 1 | COMP-03 | integration | `dotnet run --project flow-interpreter tests/test_polyrhythm.flow` | W0 | pending |
| 04-03-01 | 03 | 1 | COMP-04 | integration | `dotnet run --project flow-interpreter tests/test_variation.flow` | W0 | pending |

*Status: pending / green / red / flaky*

---

## Wave 0 Requirements

- [ ] `tests/test_progression.flow` — test chord progression DSL and voice leading
- [ ] `tests/test_polyrhythm.flow` — test polyrhythmic layering
- [ ] `tests/test_variation.flow` — test probabilistic pattern variation

*Created as part of plan execution tasks.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Voice leading sounds smooth | COMP-02 | Perceptual audio quality | Render progression, listen for smooth voice movement vs jumpy octaves |
| Polyrhythm cycles correctly | COMP-03 | Perceptual rhythmic alignment | Render 3/4+4/4 polyrhythm, listen for correct cycling and alignment |
| Variations sound musically related | COMP-04 | Perceptual musical quality | Generate variations, listen for recognizable relationship to original |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
