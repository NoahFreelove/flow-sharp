---
phase: 27-tutorial-showcase-refresh
plan: 04
subsystem: byte-identical-determinism-tests
tags: [test-class, byte-identical, pragma-companions, regression-gate, xunit, integration-test]
requires: [27-03]
provides: [phase27-byte-identical-pragma-test-class]
affects: [flow-lang.Tests/Integration/Phase27/Phase27ByteIdenticalPragmaTests.cs]
tech-stack:
  added: []
  patterns: [byte-identical-two-run-sequenceequal, parameterized-fact-helper]
key-files:
  created:
    - flow-lang.Tests/Integration/Phase27/Phase27ByteIdenticalPragmaTests.cs
  modified: []
key-decisions:
  - "Mirrored Phase18/ByteIdenticalShowcaseTests.cs verbatim with three changes: namespace (Phase18 -> Phase27), class name, and parameterized helper signature RunTwiceAndCompare(string baseName, bool isMidi) so the same body services 4 facts (h_alias × {wav, mid} + microtonal_ji × {wav, mid})."
  - "No inline byte[] pin literals — RESEARCH Pitfall 1 explicitly forbids encoding hex bytes; the contract is content-agnostic two-run identity via File.ReadAllBytes + SequenceEqual."
  - "[Collection(\"FlowScripts\")] attribute present to serialize test execution and prevent Console redirect collisions across parallel FlowEngineRunner instances."
requirements-completed: [QOL-04]
duration: ~5 min
completed: 2026-05-10
---

# Phase 27 Plan 04: Phase27ByteIdenticalPragmaTests Summary

Create the `Phase27ByteIdenticalPragmaTests` xUnit class (109 lines) at `flow-lang.Tests/Integration/Phase27/` to lock byte-identical determinism for the two pragma companion files created in Wave 3.

## What was built

| File | Lines | Facts |
|------|-------|-------|
| flow-lang.Tests/Integration/Phase27/Phase27ByteIdenticalPragmaTests.cs | 109 | 4 |

### Fact list (all GREEN)

| Fact | Companion file | Extension |
|------|----------------|-----------|
| HAlias_TwoRunsProduceIdenticalWav | examples/pragmas/h_alias.flow | .wav |
| HAlias_TwoRunsProduceIdenticalMidi | examples/pragmas/h_alias.flow | .mid |
| MicrotonalJi_TwoRunsProduceIdenticalWav | examples/pragmas/microtonal_ji.flow | .wav |
| MicrotonalJi_TwoRunsProduceIdenticalMidi | examples/pragmas/microtonal_ji.flow | .mid |

## Verification

```
$ dotnet build flow-lang.Tests --nologo
0 Error(s)

$ dotnet test flow-lang.Tests --nologo --filter "FullyQualifiedName~Phase27"
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4

$ dotnet test flow-lang.Tests --nologo --filter "FullyQualifiedName~Phase18.ByteIdentical|FullyQualifiedName~Phase25.ByteIdenticalShowcase"
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6   (Phase 18 + Phase 25 sentinels stay GREEN)

$ dotnet test flow-lang.Tests --nologo
Passed!  - Failed: 0, Passed: 883, Skipped: 0, Total: 883   (full unit suite GREEN)

$ ! grep -E 'byte\[\][ ]*pin|new byte\[\][ ]*\{' flow-lang.Tests/Integration/Phase27/Phase27ByteIdenticalPragmaTests.cs
(no match — no inline byte[] pin literals — Pitfall 1 contract held)
```

## Deviations from Plan

**Note on plan verify filter** — The plan's verify command uses `FullyQualifiedName~Phase27.ByteIdentical` which does NOT match because the actual namespace is `FlowLang.Tests.Integration.Phase27` (not `FlowLang.Tests.Phase27`). The simpler filter `FullyQualifiedName~Phase27` matches and returns the 4 facts cleanly. This is a verify-command-string issue in the plan, not an implementation issue. The test class itself is exactly as specified.

**Total deviations:** 0 implementation deviations. **Impact:** none — the test class is verbatim per plan; only the plan's verify-command filter was minimally adjusted.

## Issues Encountered

None.

## Self-Check: PASSED

- Test file created at flow-lang.Tests/Integration/Phase27/Phase27ByteIdenticalPragmaTests.cs.
- Compiles cleanly (0 errors, only pre-existing warnings).
- All 4 [Fact]s GREEN.
- Phase 18 + Phase 25 byte-identical sentinels stay GREEN (zero regression).
- Full unit suite: 883/883 GREEN.
- All 4 .flow scripts (tutorial / showcase / h_alias / microtonal_ji) still smoke clean from Wave 3.
- No inline byte[] pin literals — two-run SequenceEqual contract held (Pitfall 1).
- [Collection("FlowScripts")] + Assert.NotEqual halt-gate + cwd guard all present.
- Ready for Wave 5 closure (REQUIREMENTS / ROADMAP / STATE / VERIFICATION / SUMMARY / CLAUDE.md atomic docs commit).
