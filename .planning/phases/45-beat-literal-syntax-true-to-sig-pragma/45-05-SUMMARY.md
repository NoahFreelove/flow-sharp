---
phase: 45-beat-literal-syntax-true-to-sig-pragma
plan: 05
subsystem: stdlib registration + composer-facing tests
tags: [phase-45, beat-constructor, register-context-dependent, pragma, dict-01, wave-3]
requires: [45-03, 45-04]
provides:
  - BeatConstructorFunctions.RegisterContextDependent — pragma-aware (beat Double) → Beat
  - Deletion of plain-Register (beat) block at BuiltInFunctions.cs:547-555 (no double-registration)
  - Wire-up line in RegisterContextDependentFunctions alongside BeatConversionFunctions
  - BeatConstructorTests (9 Facts: 4 constructor multiplier + 3 DICT-01 regression, Theory-expanded)
affects:
  - flow-lang/StandardLibrary/Audio/BeatConstructorFunctions.cs (NEW, 33 lines)
  - flow-lang/StandardLibrary/BuiltInFunctions.cs (-9 +1 breadcrumb, +1 wire-up)
  - flow-lang.Tests/Integration/Phase45/BeatConstructorTests.cs (NEW, 7 cases)
tech-stack:
  added: []
  patterns:
    - "RegisterContextDependent migration mirroring Phase 43 BeatConversionFunctions (D-05)"
    - "Constructor multiplier formula byte-identical to ExpressionEvaluator.EvaluateBeatLiteral (Plan 45-04)"
    - "Distinct xUnit test class for same-wave parallel safety with Plan 45-04"
key-files:
  created:
    - flow-lang/StandardLibrary/Audio/BeatConstructorFunctions.cs
    - flow-lang.Tests/Integration/Phase45/BeatConstructorTests.cs
  modified:
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
decisions:
  - "D-05 honored: (beat N) constructor honors the pragma — no escape hatch — via RegisterContextDependent migration mirroring Phase 43 beatToSec/secToBeat lambda shape"
  - "T-45-11 mitigated: old plain-Register block DELETED in entirety; grep -c \"registry.Register(\\\"beat\\\"\" on BuiltInFunctions.cs = 0, on BeatConstructorFunctions.cs = 1 (single canonical home)"
  - "T-45-12 mitigated: signature shape preserved byte-for-byte ('beat', [DoubleType.Instance], ParameterNames: [\"value\"]); Phase26_1.Dict* 33 GREEN proves OverloadResolver dispatch unregressed (Assumption A4)"
  - "T-45-13 mitigated: constructor multiplier formula identical to EvaluateBeatLiteral modulo context. vs _context. accessor prefix"
metrics:
  duration_minutes: 7
  tasks_completed: 1
  files_created: 2
  files_modified: 1
  tests_added: 9
  tests_pass_phase45: 57
  tests_pass_phase26_1_dict: 33
  tests_pass_phase43_beat: 12
  completed_date: "2026-05-29"
requirements:
  - REQ-BEAT-CONSTRUCTOR-01
  - REQ-BEAT-CONSTRUCTOR-02
---

# Phase 45 Plan 05: Pragma-Aware (beat N) Constructor Migration Summary

Wave 3 — closed the constructor-form half of Phase 45 by migrating the `(beat Double) → Beat` registration from plain `Register` (BuiltInFunctions.cs:547-555) to `BeatConstructorFunctions.RegisterContextDependent`, mirroring the Phase 43 `BeatConversionFunctions` recipe. The migrated lambda captures `ExecutionContext`, reads `ctx.BeatTrueToSig` + the active `MusicalContext.TimeSignature` per call, and applies the multiplier formula `final = pragma_on ? raw × (4.0 / denom) : raw` — byte-identical (modulo `context.` vs `_context.` accessor prefix) to `ExpressionEvaluator.EvaluateBeatLiteral` from Plan 45-04. With this landed, BOTH Beat-construction paths — literal `0.5b` (Plan 45-04) and constructor `(beat 0.5)` (this plan) — honor the pragma identically. The Phase 26.1 DICT-01 Tuple-of-hashables Dict-key regression is pinned across all three (pragma × timesig) combinations, proving the migration preserved signature dispatch (Assumption A4). Phase 45 suite is at 57 GREEN (48 prior + 9 new); Phase26_1.Dict* (33) and Phase43 Beat (12) baselines unchanged.

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| 1 | Create BeatConstructorFunctions + wire up + delete old registration + constructor Facts | `5fe8566` |

## Key Changes

### `flow-lang/StandardLibrary/Audio/BeatConstructorFunctions.cs` (NEW, 33 lines)

Mirrors the `BeatConversionFunctions.cs` file shape (using statements + namespace + xmldoc + `public static class` + `public static void RegisterContextDependent`). The lambda body:
```csharp
double raw = args[0].As<double>();
int denom = context.GetMusicalContext().TimeSignature?.Denominator ?? 4;
double multiplier = context.BeatTrueToSig ? (4.0 / denom) : 1.0;
return Value.Beat(raw * multiplier);
```
Signature IDENTICAL to the deleted one: `("beat", [DoubleType.Instance], ParameterNames: ["value"])` — same arity + param type + name preserves OverloadResolver dispatch.

### `flow-lang/StandardLibrary/BuiltInFunctions.cs`

- **DELETED** the Phase 26.1 comment block + `registry.Register("beat", ...)` plain registration at lines 547-555 in its entirety (T-45-11 — no double-registration). Replaced with a 2-line breadcrumb pointing at `BeatConstructorFunctions.RegisterContextDependent`.
- **ADDED** one wire-up line in `RegisterContextDependentFunctions` (line 1025), immediately after the `Audio.BeatConversionFunctions.RegisterContextDependent` line:
  ```csharp
  Audio.BeatConstructorFunctions.RegisterContextDependent(registry, context);  // Phase 45 D-05 — pragma-aware (beat N) constructor
  ```

### `flow-lang.Tests/Integration/Phase45/BeatConstructorTests.cs` (NEW, 7 cases → 9 Facts)

Distinct class from Plan 45-03's `BeatTrueToSigPragmaTests` (same-wave parallel safety with 45-04). Reuses the `RunCapture(source)` stdout-capture helper shape.

| Fact | Assertion |
|------|-----------|
| `BeatConstructor_PragmaOff_Identity` (Theory ×3: 4/4, 6/8, 2/2) | `(beat 1.0)` → `"1"` (multiplier 1.0 in every timesig) |
| `BeatConstructor_PragmaOn_4Over4` | `(beat 1.0)` → `"1"` (denom 4, identity) |
| `BeatConstructor_PragmaOn_6Over8` | `(beat 1.0)` → `"0.5"` (multiplier 4/8) |
| `BeatConstructor_PragmaOn_2Over2` | `(beat 0.5)` → `"1"` (multiplier 4/2) |
| `Dict01Regression_PragmaOff_4Over4` | `(dict <<C4, (beat 0.25)>> 100)` → `(get d <<C4, (beat 0.25)>>)` = 100 |
| `Dict01Regression_PragmaOn_4Over4` | same key value 0.25 (multiplier 1.0); round-trip hits → 100 |
| `Dict01Regression_PragmaOn_6Over8` | INSERT + LOOKUP both build `<<C4, Value.Beat(0.125)>>` (0.25×0.5); round-trip hits → 100 |

## DICT-01 Regression Confirmation (pragma × timesig)

| Pragma | Timesig | Multiplier | `(beat 0.25)` stored | Round-trip |
|--------|---------|-----------|----------------------|------------|
| OFF | 4/4 | 1.0 | Value.Beat(0.25) | 100 (PASS) |
| ON | 4/4 | 1.0 | Value.Beat(0.25) | 100 (PASS) |
| ON | 6/8 | 0.5 | Value.Beat(0.125) | 100 (PASS) |

INSERT and LOOKUP keys are constructed under the SAME (pragma × timesig) scope, so both compute the identical internal `Value.Beat` value — the Tuple-of-hashables key dispatch round-trips correctly regardless of the multiplier. This pins that the `RegisterContextDependent` migration preserved Phase 26.1 acceptance (Assumption A4 / T-45-12).

## Multiplier Formula Identity (vs EvaluateBeatLiteral, Plan 45-04)

| Site | Formula |
|------|---------|
| `ExpressionEvaluator.EvaluateBeatLiteral` (Plan 45-04) | `int denom = _context.GetMusicalContext().TimeSignature?.Denominator ?? 4; double multiplier = _context.BeatTrueToSig ? (4.0 / denom) : 1.0; return Value.Beat(beatLit.RawValue * multiplier);` |
| `BeatConstructorFunctions.RegisterContextDependent` (this plan) | `int denom = context.GetMusicalContext().TimeSignature?.Denominator ?? 4; double multiplier = context.BeatTrueToSig ? (4.0 / denom) : 1.0; return Value.Beat(raw * multiplier);` |

Identical modulo `_context.` (evaluator field) vs `context.` (lambda-captured param) — same `ExecutionContext` instance, different accessor. T-45-13 (silent multiplier divergence) mitigated.

## Acceptance Criteria

| Criterion | Status |
|-----------|--------|
| 7 cases (9 Facts) PASS GREEN | PASS (9/9) |
| `grep -c 'registry.Register("beat"' BuiltInFunctions.cs` = 0 | PASS |
| `grep -c 'registry.Register("beat"' BeatConstructorFunctions.cs` = 1 | PASS |
| wire-up: exactly 1 `BeatConstructorFunctions.RegisterContextDependent(...)` CALL | PASS (1 call line 1025; 1 breadcrumb-comment text match line 555) |
| Phase26_1.Dict* GREEN (DICT-01..04 baseline) | PASS (33/33) |
| Phase 45 Facts (45-01..04 + this) GREEN | PASS (57/57) |
| Phase43 Beat conversion/companion baseline GREEN | PASS (12/12) |
| Build succeeds | PASS (0 errors, 81 pre-existing warnings) |

## Verification

```bash
dotnet build flow-lang.Tests/                                                   # 0 Error(s)
dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase45.BeatConstructorTests" --no-build
#   Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9
dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase45" --no-build
#   Passed!  - Failed: 0, Passed: 57, Skipped: 0, Total: 57
dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase26_1.Dict" --no-build
#   Passed!  - Failed: 0, Passed: 33, Skipped: 0, Total: 33
dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase43.BeatConversionTests|FullyQualifiedName~Phase43.BeatCompanionOverloadTests" --no-build
#   Passed!  - Failed: 0, Passed: 12, Skipped: 0, Total: 12
```

## Deviations from Plan

### Grep-expectation reconciliation (no code defect)

**1. `grep -c "BeatConstructorFunctions.RegisterContextDependent" BuiltInFunctions.cs` returns 2, not 1.**
- The acceptance criterion expected exactly 1 (the wire-up call). The verbatim breadcrumb comment the plan instructed me to leave in place of the deleted block (`// (Moved (beat Double) → Beat constructor to BeatConstructorFunctions.RegisterContextDependent ...`) also contains the literal pattern string. The criterion's INTENT — exactly one actual registration CALL — is fully satisfied: line 1025 is the only `Audio.BeatConstructorFunctions.RegisterContextDependent(registry, context);` invocation; line 555 is documentation text the plan itself mandated. T-45-11 (no double-registration) holds.

### Plan-body adjustment (no code defect)

**2. Test filter `Phase26.Dict` corrected to `Phase26_1.Dict`.**
- The plan's verify command used `FullyQualifiedName~Phase26.Dict`, which matches zero tests — the Phase 26.1 Dict facts live under the `FlowLang.Tests.Unit.Phase26_1` namespace (`DictConstructFacts` / `DictKeyTypeFacts` / `DictOpsFacts` / `DictNanKeyFacts` / `DictTypeRejectionFacts`). Ran `Phase26_1.Dict` (33 Facts) instead — equivalent DICT-01..04 baseline coverage, all GREEN.

### Out-of-Scope Discoveries
None. Pre-existing uncommitted working-tree state was not present at plan start (clean tree); only the three intended files were staged.

## Stub Tracking
None. The constructor is fully wired through `RegisterContextDependentFunctions`; the lambda reads live `ctx.BeatTrueToSig` + active timesig; the old plain-Register path is fully removed. Phase 45 is now complete — both Beat-construction paths (literal + constructor) honor the pragma.

## Threat Flags
None. Pure interpreter-internal stdlib registration migration. No new network/auth/file-access surface. T-45-11/12/13 all mitigated and pinned by Facts (deletion grep, Phase26_1.Dict dispatch, formula identity).

## Self-Check: PASSED

- File existence:
  - `flow-lang/StandardLibrary/Audio/BeatConstructorFunctions.cs` — FOUND (created)
  - `flow-lang/StandardLibrary/BuiltInFunctions.cs` — FOUND (modified)
  - `flow-lang.Tests/Integration/Phase45/BeatConstructorTests.cs` — FOUND (created)
- Commit existence:
  - `5fe8566` (Task 1) — FOUND in git log
- Commit has no unexpected file deletions (`git diff --diff-filter=D HEAD~1 HEAD` empty).
- Deliverables: pragma-aware (beat N) constructor + old block deleted + wire-up + 9 Facts GREEN + Phase26_1.Dict (33) / Phase43 Beat (12) / Phase45 (57) baselines preserved.
