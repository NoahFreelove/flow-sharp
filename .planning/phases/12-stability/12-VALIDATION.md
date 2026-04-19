---
phase: 12
slug: stability
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-19
---

# Phase 12 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (NEW — `flow-lang.Tests` project introduced by plan 12-01) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (Wave 0 installs) |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~{TestClass}"` |
| **Full suite command** | `dotnet test flow-sharp.sln` |
| **Estimated runtime** | ~30–60 seconds (70+ `.flow` Theory cases + native unit tests) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter` scoped to the test class for the just-touched fix
- **After every plan wave:** Run `dotnet test flow-sharp.sln`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 12-01-* | 01 | 1 | TEST-01, TEST-02, TEST-03 | — | xUnit harness runs; all 70+ `.flow` Theory cases report pass/fail; spike c1 lands RED (expected until 12-04) | integration | `dotnet test flow-sharp.sln` | ❌ W0 | ⬜ pending |
| 12-02-* | 02 | 2 | FIX-05 | — | `init([])` throws InvalidOperationException with message matching head/last format | unit | `dotnet test --filter "FullyQualifiedName~InitEmptyThrows"` | ❌ W0 | ⬜ pending |
| 12-03-* | 03 | 2 | FIX-06 | — | Failed Thunk.Force() re-throws captured exception with original stack on repeat calls | unit | `dotnet test --filter "FullyQualifiedName~ThunkFailureCache"` | ❌ W0 | ⬜ pending |
| 12-04-* | 04 | 2 | FIX-07 | — | 7 return→break replacements in ExecuteMusicalContext; spike c1 flips RED→GREEN; test_musical_context_errors still passes | unit+integration | `dotnet test --filter "FullyQualifiedName~MusicalContextBody"` | ❌ W0 | ⬜ pending |
| 12-05-* | 05 | 2 | TEST-03 | — | `if(Bool, Void, Void)` wildcard overload registered; `exportWav` auto-creates parent dir | integration | `dotnet test --filter "FullyQualifiedName~CustomOscillator\|FullSong"` | ❌ W0 | ⬜ pending |
| 12-06-* | 06 | 3 | TEST-01, TEST-02, TEST-03 | — | REQUIREMENTS.md updated; 12-VERIFICATION.md lists all FIX-* commit hashes | doc | `grep -E "TEST-0[123]" .planning/REQUIREMENTS.md` | ⬜ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/flow-lang.Tests.csproj` — xUnit harness (xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, coverlet.collector)
- [ ] `flow-lang.Tests/FlowEngineFixture.cs` — in-process FlowEngine runner with stdout/stderr capture
- [ ] `flow-lang.Tests/FlowScriptTheory.cs` — `[Theory]` + `[ClassData]` or glob-based DataAttribute wrapping `tests/test_*.flow` and `tests/spike/c*.flow`
- [ ] `flow-lang.Tests/FixTests/` — native xUnit unit tests for FIX-05/FIX-06/FIX-07a
- [ ] `flow-sharp.sln` — add `flow-lang.Tests` project reference

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| v1.1 soft-failure semantics preserved end-to-end (ROADMAP success criterion 5) | FIX-07 | Covered by automated tests AND human-readable by running `test_musical_context_errors.flow` manually post-fix to confirm stderr + body output | `dotnet run --project flow-interpreter tests/test_musical_context_errors.flow` |
| Bisectability across FIX-* commits (ROADMAP success criterion 3) | FIX-05, FIX-06, FIX-07 | Git-archaeology check best done manually | `git log --oneline 12.. -- flow-lang/ \| grep -E "FIX-(05\|06\|07a)"` — each FIX lands as its own commit |

---

## Observable Invariants (from RESEARCH.md Validation Architecture)

Each invariant is a concrete check that would fail if the fix were removed:

1. **FIX-05:** `init([])` call produces stderr containing `"Cannot get init of empty array"` (matches head/last format)
2. **FIX-06:** Calling `Thunk.Force()` twice on a throwing evaluator returns the same `ExceptionDispatchInfo`-captured exception on both calls with original stack trace preserved
3. **FIX-06:** Evaluator delegate is invoked exactly once even when it throws (memoization semantics)
4. **FIX-07a:** `test_custom_oscillator.flow` exits code 0 post-fix
5. **FIX-07a:** `test_full_song.flow` exits code 0 post-fix
6. **FIX-07a:** `tests/spike/c1-musical-context-body.flow` exits code 0 post-fix (was RED in 12-01)
7. **FIX-07a:** Grep `flow-lang/Interpreter/Interpreter.cs` inside ExecuteMusicalContext for the literal count of `return;` statements: must be 0 (was 7 pre-fix)
8. **FIX-07a:** `// AUDIT-VERIFIED 2026-04-19: C1 — Fixed (returns→breaks)` marker appears at the fix site
9. **FIX-07a:** `test_musical_context_errors.flow` continues to exit code 0 (soft-failure contract preserved)
10. **Test framework:** `dotnet test flow-sharp.sln` completes with 70+ Theory cases executed (non-zero count reported)
11. **12-05:** `if(true, "a", "b")` and `if(true, 1.0, -1.0)` both evaluate without overload-resolution error

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
