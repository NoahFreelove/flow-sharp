---
phase: 43
slug: module-names-qualified-imports
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-24
---

# Phase 43 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (`flow-lang.Tests/`) + the existing `tests/test_*.flow` script suite (~123 happy-path scripts). New Phase 43 fixtures live at `flow-lang.Tests/Integration/Phase43/`. |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (existing — no changes required) |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase43" --logger "console;verbosity=minimal"` |
| **Full suite command** | `dotnet test flow-lang.Tests` (xUnit) + `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done` (`.flow` scripts) |
| **Estimated runtime** | Phase43 fixtures: ~5–15s · xUnit full: ~60–90s · `.flow` suite: ~120s |

**Known pre-existing failures (from Phase 42 base commit `c4cd738`):** 34 failures in Phase 28/29/35/38 (audio rendering, OSC loopback). Document in VERIFICATION.md §Known Caveats; do NOT count toward Phase 43 regression bar. Filename-based exclusion at the final-gate step.

---

## Sampling Rate

- **After every task commit:** Run quick command (`--filter "FullyQualifiedName~Phase43"`)
- **After every plan wave:** Run xUnit full suite (`dotnet test flow-lang.Tests`)
- **Before `/gsd:verify-work`:** xUnit full suite green (modulo pre-existing 34) + 123 `.flow` happy-path scripts green
- **Max feedback latency:** ~15s (quick) / ~90s (xUnit full) / ~120s (`.flow` suite)

---

## Per-Task Verification Map

*Filled in by the planner once tasks are enumerated. Skeleton rows below — planner replaces with concrete plan/task IDs.*

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 43-01-01 | 01 | 1 | REQ-MOD-01 | T-43-01 | `module` lexer token recognized only at file-scope head; parser rejects mid-file `module` declarations | unit | `dotnet test --filter "FullyQualifiedName~Phase43.ModuleLexerTests"` | ❌ W0 | ⬜ pending |
| 43-02-01 | 02 | 1 | REQ-MOD-02 | — | `ModuleRegistry` registers + dedupes module names; duplicate emits one-shot advisory | unit | `dotnet test --filter "FullyQualifiedName~Phase43.ModuleRegistryTests"` | ❌ W0 | ⬜ pending |
| 43-03-01 | 03 | 2 | REQ-MOD-03 | — | `ExpressionEvaluator.EvaluateMemberAccess` checks ModuleRegistry FIRST when LHS is identifier; falls through to instance-member otherwise | unit | `dotnet test --filter "FullyQualifiedName~Phase43.QualifiedAccessDispatchTests"` | ❌ W0 | ⬜ pending |
| 43-04-01 | 04 | 2 | REQ-MOD-04 + REQ-MOD-05 | — | `beatToSec` reads active tempo; defaults to 120 BPM + one-shot WarnOnce when absent; `secToBeat` symmetric | unit | `dotnet test --filter "FullyQualifiedName~Phase43.BeatConversionTests"` | ❌ W0 | ⬜ pending |
| 43-04-02 | 04 | 2 | REQ-MOD-06 | — | `delay(Buffer, Beat)` + `renderBarAtBeat(Sequence, Beat)` overloads route through `beatToSec` | unit | `dotnet test --filter "FullyQualifiedName~Phase43.BeatCompanionOverloadTests"` | ❌ W0 | ⬜ pending |
| 43-04-03 | 04 | 2 | REQ-MOD-07 (D-10 polarity flip) | — | Phase 42 `AuditHarnessTests.OrphanList_ContainsBeatType` → renamed `OrphanList_DoesNotContainBeatType`; passes against the live `FlowEngine` | unit | `dotnet test --filter "FullyQualifiedName~Phase42.AuditHarnessTests.OrphanList_DoesNotContainBeatType"` | ❌ W0 | ⬜ pending |
| 43-05-01 | 05 | 3 | REQ-MOD-08 | — | 12 stdlib `.flow` files have `module <name>` declarations per D-07 table; `std.flow` stays declaration-less | source | `for f in flow-lang/{audio,bars,collections,composition,generative,improv,notation-io,notes,osc,patterns,sfz,test}.flow; do head -3 "$f" \| grep -q '^module' \|\| echo "missing: $f"; done` | ❌ W0 | ⬜ pending |
| 43-05-02 | 05 | 3 | REQ-MOD-09 | — | `notation.flow` renamed to `flow-lang/notes.flow` (or merged into notation-io); collision resolved | source | `! test -f flow-lang/notation.flow OR head -3 flow-lang/notation.flow \| grep '^module notes'` | ❌ W0 | ⬜ pending |
| 43-06-01 | 06 | 3 | REQ-MOD-10 | — | All 123 `tests/test_*.flow` happy-path scripts pass + xUnit full suite green (modulo pre-existing 34) | integration | `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done && dotnet test flow-lang.Tests` | ❌ W0 | ⬜ pending |

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Integration/Phase43/ModuleLexerTests.cs` — stubs for REQ-MOD-01
- [ ] `flow-lang.Tests/Integration/Phase43/ModuleRegistryTests.cs` — stubs for REQ-MOD-02
- [ ] `flow-lang.Tests/Integration/Phase43/QualifiedAccessDispatchTests.cs` — stubs for REQ-MOD-03
- [ ] `flow-lang.Tests/Integration/Phase43/BeatConversionTests.cs` — stubs for REQ-MOD-04 + REQ-MOD-05
- [ ] `flow-lang.Tests/Integration/Phase43/BeatCompanionOverloadTests.cs` — stubs for REQ-MOD-06
- [ ] No new framework install — xUnit already wired

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Stdlib `module` migration doesn't surprise existing composer scripts | REQ-MOD-11 | The advisory wording + composer ergonomics are judgement calls — confirm a real composer-script run still feels right | Run `examples/symphony/symphony.flow`, `examples/ragtime/ragtime.flow`, and `examples/dsp/granular.flow` after Phase 43 ships. Confirm no `[module]` advisories fire from those scripts (they don't qualify; collisions only emerge if a composer imports two modules with overlapping exports). |
| Beat-default advisory wording (`[beatToSec] no active tempo — defaulting to 120 BPM`) reads well to a composer | REQ-MOD-12 | Wording UX is human-evaluation territory | Run a one-line `.flow` script with `(beatToSec 1.0b)` outside any `tempo` block; confirm stderr advisory fires once, contains "tempo", "120 BPM", and is non-condescending. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s (Phase43 quick) / 90s (xUnit full) / 120s (`.flow` suite)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
