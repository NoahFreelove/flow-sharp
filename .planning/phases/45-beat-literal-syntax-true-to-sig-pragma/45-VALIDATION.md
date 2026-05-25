---
phase: 45
slug: beat-literal-syntax-true-to-sig-pragma
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-25
---

# Phase 45 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from 45-RESEARCH.md §Validation Architecture (6 signals × ≥2 sample points each).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (already in use; `flow-lang.Tests/flow-lang.Tests.csproj`) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` |
| **Quick run command** | `dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase45"` |
| **Full suite command** | `dotnet test` |
| **`.flow` integration smoke** | `for test in tests/test_beat_*.flow; do dotnet run --project flow-interpreter "$test"; done` |
| **Estimated runtime** | ~20s quick / ~120s full |

---

## Sampling Rate

- **After every task commit:** Run quick command (Phase45 filter, ~20s)
- **After every plan wave:** Run full suite + `.flow` smoke
- **Before `/gsd:verify-work`:** Full suite must be green + `.flow` smoke must exit 0
- **Max feedback latency:** ~20 seconds (quick) — well under any practical bound for the 6-wave plan

---

## Per-Task Verification Map

> Populated by gsd-planner after PLAN.md files are generated. The 6 signals from
> RESEARCH.md (Lexer / AST / Pragma / Multiplier / Constructor / Tutorial-smoke)
> map to ~50 xUnit cases + 4 `.flow` smoke scripts. Each task's `<acceptance_criteria>`
> must reference at least one Fact/Theory or `.flow` smoke command.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| _populated post-planning_ | | | | | | | | | |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Phase45/BeatLiteralParserTests.cs` — stubs for REQ-BEAT-LEX-NN + REQ-BEAT-AST-NN
- [ ] `flow-lang.Tests/Phase45/BeatTrueToSigPragmaTests.cs` — stubs for REQ-BEAT-PRAGMA-NN + REQ-BEAT-CONSTRUCTOR-NN + multiplier Theory grid
- [ ] xUnit + .NET 10 already installed (no framework install needed; existing test project covers all phase requirements)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `examples/beat/intro.flow` renders musically-correct 6/8 jig | REQ-BEAT-DOC-NN | Subjective musical correctness (does it sound like a 6/8 jig?) — automated tests pin multiplier formula + WAV byte-identity, not aesthetics | Run `dotnet run --project flow-interpreter examples/beat/intro.flow`; listen to output WAV; confirm pragma-on vs pragma-off comparison produces audibly different rhythms in 6/8 context |
| `examples/beat/cut-time.flow` renders cut-time feel | REQ-BEAT-DOC-NN | Same — aesthetic check on `1b = half` in 2/2 | Same procedure for `examples/beat/cut-time.flow` |
| CLAUDE.md music-types table reads cleanly with Beat row added | REQ-BEAT-DOC-NN | Doc-readability check | Read updated CLAUDE.md §Music Types Quick Reference; confirm Beat row matches sibling row formatting + IsCompatibleWith column accurate |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 20s (quick)
- [ ] `nyquist_compliant: true` set in frontmatter (after planner populates verification map)

**Approval:** pending
