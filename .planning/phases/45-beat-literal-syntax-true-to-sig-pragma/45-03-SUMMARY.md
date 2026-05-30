---
phase: 45-beat-literal-syntax-true-to-sig-pragma
plan: 03
subsystem: runtime + pragma-plumbing
tags: [phase-45, pragma, beat-true-to-sig, execution-context, module-loader, wave-2]
requires: [phase-45-plan-01-lexer-pragma-scanner]
provides:
  - PragmaRegistry KnownPragmas["beat-true-to-sig"] entry (D-03 verbatim)
  - ExecutionContext.BeatTrueToSig single bool field (default false)
  - FlowEngine.ApplyBeatTrueToSigPragma helper + Execute invocation
  - ModuleLoader save-set-restore for BeatTrueToSig (finally-protected)
  - BeatTrueToSigPragmaTests (10 Facts)
affects:
  - flow-lang/Lexing/PragmaRegistry.cs (+1 line)
  - flow-lang/Runtime/ExecutionContext.cs (+31 lines, 1 field + section header + xmldoc)
  - flow-lang/Core/FlowEngine.cs (+22 lines, helper + invocation)
  - flow-lang/Runtime/ModuleLoader.cs (+9 lines, save-set-restore)
  - flow-lang.Tests/Integration/Phase45/ (NEW: BeatTrueToSigPragmaTests.cs)
tech-stack:
  added: []
  patterns:
    - "File-scope pragma bit on ExecutionContext mirroring Phase 44 StrictMode (D-04)"
    - "ModuleLoader save-set-restore in finally — Anti-Pattern 1 (never mutate without paired restore)"
    - "Single-field design per Pitfall 3 — no CallerBeatTrueToSig companion (Phase 45 has no leaf-clamp-site asymmetry)"
key-files:
  created:
    - flow-lang.Tests/Integration/Phase45/BeatTrueToSigPragmaTests.cs
  modified:
    - flow-lang/Lexing/PragmaRegistry.cs
    - flow-lang/Runtime/ExecutionContext.cs
    - flow-lang/Core/FlowEngine.cs
    - flow-lang/Runtime/ModuleLoader.cs
decisions:
  - "D-03 honored: ['beat-true-to-sig'] entry with verbatim description added after the ['strict'] line"
  - "D-04 honored: single bool ExecutionContext.BeatTrueToSig field, default false; FlowEngine helper + ModuleLoader push/pop mirror StrictMode discipline"
  - "Pitfall 3 honored: NO CallerBeatTrueToSig field — only an xmldoc prose reference documenting the single-field rationale"
metrics:
  duration_minutes: 14
  tasks_completed: 2
  files_created: 1
  files_modified: 4
  tests_added: 10
  tests_pass_phase45_pragma: 10
  tests_pass_phase44_regression: 11
  completed_date: "2026-05-30"
requirements:
  - REQ-BEAT-PRAGMA-01
  - REQ-BEAT-PRAGMA-02
  - REQ-BEAT-PRAGMA-03
  - REQ-BEAT-PRAGMA-04
---

# Phase 45 Plan 03: beat-true-to-sig Pragma Plumbing Summary

Wave 2 runtime plumbing — registered the `enable beat-true-to-sig;` pragma in `PragmaRegistry`, added the single `ExecutionContext.BeatTrueToSig` bool field, wired `FlowEngine.ApplyBeatTrueToSigPragma` alongside `ApplyStrictPragma`, and added the `ModuleLoader` save-set-restore (finally-protected) so the pragma bit is file-scoped and never leaks across `use` imports. 10 xUnit Facts pin pragma registration + context-bit semantics + cross-file restore (incl. throw-path). Mirrors Phase 44 `StrictMode` discipline exactly; single-field design per Pitfall 3 (no `CallerBeatTrueToSig` companion).

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| 1 | Register pragma + add ExecutionContext field + FlowEngine helper + 10-Fact test file | `7372ce3` |
| 2 | ModuleLoader save-set-restore for BeatTrueToSig | `84df903` |

## Key Changes

### `flow-lang/Lexing/PragmaRegistry.cs` (line 37)
Added one trailing entry to `KnownPragmas` immediately after `["strict"]`:
```csharp
["beat-true-to-sig"] = "Opt-in: Nb literals and (beat N) constructor calls multiply by 4/denominator at eval time, reading active timesig. So in 'timesig 6/8 { }' with pragma on, 1b = 1 eighth. File-scoped, no propagation via use imports."
```
Verbatim D-03 description. Ordinal sort places it under "b..." in `AlphabetizedKnownNames()`.

### `flow-lang/Runtime/ExecutionContext.cs` (line 550)
New `// ===== Phase 45 — beat-true-to-sig pragma (D-03 / D-04) =====` section inserted AFTER the Phase 44 `StrictAdvisoryDedup` field (end of the strict-mode section). The xmldoc explicitly documents the Pitfall 3 single-field rationale:
```csharp
public bool BeatTrueToSig { get; set; } = false;
```
NO companion `CallerBeatTrueToSig` field — the only occurrence of that identifier in the codebase is the prose reference inside this field's xmldoc (`<c>CallerBeatTrueToSig</c>`).

### `flow-lang/Core/FlowEngine.cs` (lines 351 + 425)
1. Invocation in `Execute` immediately after `ApplyStrictPragma(program);` (line 351).
2. `ApplyBeatTrueToSigPragma(Ast.Program program)` helper (line 425) mirroring `ApplyStrictPragma`'s single-line shape — overwrites the bit on every Execute (no persistence branch).

### `flow-lang/Runtime/ModuleLoader.cs` (lines 170-171 set + 251 restore)
Parallel save-set-restore alongside the existing `StrictMode` push/pop:
- `var prevBeatTrueToSig = context.BeatTrueToSig;` + `context.BeatTrueToSig = pragmaSet.Has("beat-true-to-sig");` BEFORE the `try` (lines 170-171).
- `context.BeatTrueToSig = prevBeatTrueToSig;` INSIDE the existing `finally` (line 251) — runs even when the imported Execute throws (Anti-Pattern 1 / T-45-06 mitigation).

### `flow-lang.Tests/Integration/Phase45/BeatTrueToSigPragmaTests.cs` (NEW, 10 Facts)
- Task 1 (6): `PragmaRegistryEntry`, `PragmaSetsContextBit`, `AbsenceLeavesBitFalse`, `LevenshteinSuggestion`, `BeatTrueToSig_DefaultsFalse`, `BeatTrueToSig_Settable`.
- Task 2 (4): `CrossFileRestoreToFalse`, `CrossFileRestoreToTrue`, `CrossFileRestoreAfterThrow`, `StdlibImportLeavesBitUnchanged`.
Uses `NewContext()` (ErrorReporter + InternalFunctionRegistry → fresh ExecutionContext) + `FlowEngine` end-to-end + tempdir cross-file authoring (mirrors `Phase44.ModuleLoaderStrictPropagationTests`).

## Acceptance Criteria

| Criterion | Status |
|-----------|--------|
| `PragmaRegistry.KnownPragmas["beat-true-to-sig"]` present, D-03 verbatim | PASS |
| `ExecutionContext.BeatTrueToSig` bool field, default false | PASS (`grep -c "public bool BeatTrueToSig"` = 1) |
| No `CallerBeatTrueToSig` FIELD (single-field design) | PASS (only an xmldoc prose reference; no `public bool CallerBeatTrueToSig`) |
| `FlowEngine.ApplyBeatTrueToSigPragma` helper + invocation | PASS (`grep -c` = 2: line 351 invocation + line 425 declaration) |
| `ModuleLoader` save-set-restore finally-protected | PASS (`prevBeatTrueToSig` = 2; `context.BeatTrueToSig` = 3: save-RHS + set + restore) |
| 10 Phase 45 pragma Facts GREEN | PASS (10/10) |
| Phase 44 ModuleLoader/PragmaRegistry regression GREEN | PASS (11/11 for the combined filter) |
| Build succeeds | PASS (0 errors, pre-existing warnings only) |

## Verification

```bash
dotnet build flow-lang.Tests/                 # 0 Error(s), 81 pre-existing warnings
dotnet test --filter "FullyQualifiedName~Phase45.BeatTrueToSigPragmaTests"
#   Passed!  - Failed: 0, Passed: 10, Skipped: 0, Total: 10
dotnet test --filter "FullyQualifiedName~Phase44.ModuleLoaderStrictPropagationTests|FullyQualifiedName~Phase44.PragmaRegistryStrictTests|FullyQualifiedName~Phase21.PragmaIsolation"
#   Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11
```

## Deviations from Plan

### Grep-expectation reconciliations (no code defect)

**1. `grep -c "CallerBeatTrueToSig"` returns 1, not 0.**
- The plan's acceptance criterion expected 0 to prove the single-field design (no companion FIELD). The verbatim 45-PATTERNS.md xmldoc the plan instructed me to use for the `BeatTrueToSig` field intentionally references `CallerBeatTrueToSig` in prose (`Single-field design (NO companion <c>CallerBeatTrueToSig</c>)`). The intent of the criterion — no `public bool CallerBeatTrueToSig` field — is fully satisfied: `grep 'CallerBeatTrueToSig\s*{\s*get'` returns empty. T-45-07 mitigation (no two-field dead-code surface) holds. The single match is a documentation reference REQUIRED by the plan's own PATTERNS block.

**2. `grep -c "context.BeatTrueToSig"` in ModuleLoader returns 3, not 2.**
- The plan expected 2 (set + restore), assuming the save line used a bare `var prev`. The canonical save-set-restore is `var prevBeatTrueToSig = context.BeatTrueToSig;` (save references it on the RHS) + `context.BeatTrueToSig = pragmaSet.Has(...)` (set) + `context.BeatTrueToSig = prevBeatTrueToSig;` (restore) = 3 occurrences. This is the exact PATTERNS.md shape and matches the StrictMode precedent structure; the plan's count assumption was off by the save-RHS read. No defect.

**3. Verify command syntax adjusted.**
- The plan's `dotnet build flow-lang/ flow-lang.Tests/` errors under this SDK (MSB1008: one project per build). Built `flow-lang.Tests/` (which transitively builds `flow-lang`) — equivalent coverage, 0 errors.

### Out-of-Scope Discoveries
None. Pre-existing uncommitted changes (`.planning/.continue-here.md` deletion, `.planning/config.json` modification) were present in the working tree at plan start and were deliberately NOT staged into either task commit — they are unrelated to this plan.

## Stub Tracking
None. All committed code is fully wired (registry entry is concrete; context field is read by Wave 3's `EvaluateBeatLiteral` and Wave 4's `(beat N)` migration; ModuleLoader push/pop is the live save-set-restore).

## Threat Flags
None. Runtime-internal pragma state, file-scoped, no new network/auth/file-access surface. T-45-06 (cross-import leak) and T-45-07 (two-field dead code) both mitigated as designed and pinned by Facts.

## Self-Check: PASSED

- File existence:
  - `flow-lang/Lexing/PragmaRegistry.cs` — FOUND (modified)
  - `flow-lang/Runtime/ExecutionContext.cs` — FOUND (modified)
  - `flow-lang/Core/FlowEngine.cs` — FOUND (modified)
  - `flow-lang/Runtime/ModuleLoader.cs` — FOUND (modified)
  - `flow-lang.Tests/Integration/Phase45/BeatTrueToSigPragmaTests.cs` — FOUND (created)
- Commit existence:
  - `7372ce3` (Task 1) — FOUND in git log
  - `84df903` (Task 2) — FOUND in git log
- Deliverables: pragma registered + context field + FlowEngine helper + ModuleLoader save-set-restore + 10 Facts GREEN + zero regression to Phase 44/Phase 21 pragma suites.

Ready for Wave 3 (Plan 45-04 — `EvaluateBeatLiteral` switch arm consuming `ctx.BeatTrueToSig`).
