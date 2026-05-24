---
phase: 44
slug: strict-mode
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-24
---

# Phase 44 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Source: `44-RESEARCH.md` §"Validation Architecture".

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (latest in `flow-lang.Tests/flow-lang.Tests.csproj`) + `.flow` script test convention (output-verified, no unit framework) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` |
| **Quick run command** | `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "Category=Phase44"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30s for Phase 44 filter; ~2-3min for full suite |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "Category=Phase44"` (~30s)
- **After every plan wave:** Run `dotnet test` (full suite must stay green)
- **Before `/gsd:verify-work`:** Full suite green + `tests/strict/*.flow` all run to completion
- **Max feedback latency:** ~30 seconds per-task; ~3 minutes per-wave

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 44-01-* | 01 | 1 | REQ-STRICT-01 | — | `enable strict;` pragma recognized; unknown typo gets levenshtein suggestion | unit | `dotnet test --filter "FullyQualifiedName~PragmaRegistryStrictTests"` | ❌ W0 | ⬜ pending |
| 44-01-* | 01 | 1 | REQ-STRICT-02 | — | `ctx.StrictMode=true` set after PragmaScanner detects `enable strict;`; strict file importing non-strict module runs imported procs non-strict | unit | `dotnet test --filter "FullyQualifiedName~ExecutionContextStrictModeTests OR FullyQualifiedName~ModuleLoaderStrictPropagationTests"` | ❌ W0 | ⬜ pending |
| 44-01-* | 01 | 1 | REQ-STRICT-03 | — | `ctx.CallerStrictMode` snapshotted at call dispatch into stdlib | unit | `dotnet test --filter "FullyQualifiedName~CallerStrictModeSnapshotTests"` | ❌ W0 | ⬜ pending |
| 44-02-* | 02 | 1 | REQ-STRICT-04 | — | OverloadResolver disables +100 convertible tier when ctx.StrictMode=true; covers BOTH CanConvertTo + IsCompatibleWith implicit conversions (Pitfall 1) | unit | `dotnet test --filter "FullyQualifiedName~OverloadResolverStrictTierTests"` | ❌ W0 | ⬜ pending |
| 44-03-* | 03 | 1 | REQ-STRICT-05 | — | 6 forward conv builtins (db/hz/ms/sec/cents/semitones) accept all 4 numeric + idempotent on tagged target | unit | `dotnet test --filter "FullyQualifiedName~ExplicitConversionForwardTests"` | ❌ W0 | ⬜ pending |
| 44-03-* | 03 | 1 | REQ-STRICT-06 | — | 4 reverse extractors (double/float/int/long) accept all 6 tagged music types | unit | `dotnet test --filter "FullyQualifiedName~ExplicitConversionReverseTests"` | ❌ W0 | ⬜ pending |
| 44-04-* | 04 | 1 | REQ-STRICT-10 | — | Pre-strict bug fix: non-strict `(print 42)` auto-strs via `(str x)`; non-strict `if Int x` truthy-coerces | unit | `dotnet test --filter "FullyQualifiedName~PrintCharitablyTests OR FullyQualifiedName~IfTruthyCoerceTests"` | ❌ W0 | ⬜ pending |
| 44-05-* | 05 | 2 | REQ-STRICT-07 | — | 13 §6a clamp sites error in strict with verbatim message `[strict] <tag> <issue>` | unit (Theory) | `dotnet test --filter "FullyQualifiedName~Axis_B_ClampSiteTests"` | ❌ W0 | ⬜ pending |
| 44-05-* | 05 | 2 | REQ-STRICT-07 | — | Inventory regression: exactly 13 input-perimeter Math.Clamp sites remain (mirrors Phase 42 ClampGrepConsistencyTests) | unit | `dotnet test --filter "FullyQualifiedName~Phase44ClampGrepConsistencyTests"` | ❌ W0 | ⬜ pending |
| 44-06-* | 06 | 2 | REQ-STRICT-08 | — | HIGH-priority advisory sites (~79: SFZ + patterns + render + match + DSP) error in strict | unit (Theory) | `dotnet test --filter "FullyQualifiedName~Axis_B_AdvisorySiteTests_High"` | ❌ W0 | ⬜ pending |
| 44-07-* | 07 | 3 | REQ-STRICT-08 | — | MED/LOW advisory sites (~34: chaos + generative + abc + mml + tuning + osc + audio-in + piano + midi + harmony) error in strict | unit (Theory) | `dotnet test --filter "FullyQualifiedName~Axis_B_AdvisorySiteTests_MedLow"` | ❌ W0 | ⬜ pending |
| 44-08-* | 08 | 3 | REQ-STRICT-09 | — | Strict `(and Int Int)`/`(or Int Int)`/`(not Int)`/`if Int` all error; strict returns Bool from logical ops | unit | `dotnet test --filter "FullyQualifiedName~AxisCBoolRequiredTests"` | ❌ W0 | ⬜ pending |
| 44-08-* | 08 | 3 | REQ-STRICT-11 | — | Cross-type `(gt 1 1.0)`, `(lt 1 1.0)`, `(gte 1 1.0)`, `(lte 1 1.0)` all error in strict; `(equals 1 1.0)` returns false | unit | `dotnet test --filter "FullyQualifiedName~CrossTypeComparisonStrictTests"` | ❌ W0 | ⬜ pending |
| 44-09-* | 09 | 3 | REQ-STRICT-12 | — | `enable strict;` file with `live { }` block: body runs strict on re-eval; `[live] entering` advisory stays charitable (carve-out) | unit | `dotnet test --filter "FullyQualifiedName~LiveBlockStrictTests"` | ❌ W0 | ⬜ pending |
| 44-09-* | 09 | 3 | REQ-STRICT-13 | — | REPL `:strict on` / `:strict off` toggles `ctx.StrictMode`; sticky session | unit | `dotnet test --filter "FullyQualifiedName~ReplStrictMetaCommandTests"` | ❌ W0 | ⬜ pending |
| 44-10-* | 10 | 3 | REQ-STRICT-14 | — | All `tests/strict/test_*.flow` files run to completion; `showcase_strict.flow` renders | integration | `for f in tests/strict/test_*.flow; do dotnet run --project flow-interpreter "$f"; done` | ❌ W0 | ⬜ pending |
| 44-10-* | 10 | 3 | REQ-STRICT-15 | — | Two-run cmp-clean determinism preserved after strict-mode introduction (no PRNG sites added; Axis B advisory→error path is mechanical) | integration | `dotnet test --filter "FullyQualifiedName~Phase44TwoRunDeterminismTests"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*Note:* Task IDs `44-NN-*` reflect plan-file pre-counting; final task-level IDs assigned by planner at PLAN.md emission.

---

## Wave 0 Requirements

The Phase 44 test directory does not exist today; entire suite is Wave 0:

### xUnit Negative Tests (~126 verbatim string assertions)

- [ ] `flow-lang.Tests/Phase44/` directory creation
- [ ] `flow-lang.Tests/Phase44/StrictModeNegativeTests.cs` — ~126 Facts pinning error strings (xUnit `[Theory]` + `[InlineData]`; strings from AUDIT §6a Column 5 + per-site §6b sentinels with `[strict]` prefix)
- [ ] `flow-lang.Tests/Phase44/ExplicitConversionTests.cs` — forward + reverse direction matrix (24 forward + 24 reverse = 48 cases)
- [ ] `flow-lang.Tests/Phase44/OverloadResolverStrictTierTests.cs` — Axis A: confirm +100 disabled, +500 + +1000 preserved
- [ ] `flow-lang.Tests/Phase44/PrintCharitablyTests.cs` — pre-strict bug fix: non-strict `(print Int x)` auto-strs
- [ ] `flow-lang.Tests/Phase44/IfTruthyCoerceTests.cs` — pre-strict bug fix: non-strict `if Int x` truthy-coerces
- [ ] `flow-lang.Tests/Phase44/CallerStrictModeSnapshotTests.cs` — D-05 two-field design
- [ ] `flow-lang.Tests/Phase44/ModuleLoaderStrictPropagationTests.cs` — D-03 per-declaring-file
- [ ] `flow-lang.Tests/Phase44/PragmaRegistryStrictTests.cs` — D-04 registry entry + typo suggestion
- [ ] `flow-lang.Tests/Phase44/LiveBlockStrictTests.cs` — D-15 strict-in-live-blocks
- [ ] `flow-lang.Tests/Phase44/ReplStrictMetaCommandTests.cs` — D-16 sticky session + `:strict on/off`
- [ ] `flow-lang.Tests/Phase44/AxisCBoolRequiredTests.cs` — D-12 strict Bool requirement
- [ ] `flow-lang.Tests/Phase44/CrossTypeComparisonStrictTests.cs` — D-11 comparison error / equals false
- [ ] `flow-lang.Tests/Phase44/Phase44TwoRunDeterminismTests.cs` — REQ-STRICT-15 cmp-clean preservation
- [ ] `flow-lang.Tests/Phase44/Phase44ClampGrepConsistencyTests.cs` — mirrors Phase 42 ClampGrepConsistencyTests; pins exactly 13 input-perimeter clamps remain

### Positive `.flow` Smoke Tests

- [ ] `tests/strict/` directory creation
- [ ] `tests/strict/test_strict_axis_a_overload.flow` — composer uses explicit conversions; `(gain buf -12dB)` works
- [ ] `tests/strict/test_strict_axis_b_clamps.flow` — composer uses correctly-bounded args; no error
- [ ] `tests/strict/test_strict_explicit_conversions.flow` — exercise all 6 forward + 4 reverse builtins
- [ ] `tests/strict/test_strict_equality.flow` — strict equality + comparison semantics
- [ ] `tests/strict/test_strict_with_justintonation.flow` — confirm pragma composition (`enable strict;` + `enable justIntonation;`)
- [ ] `tests/strict/test_strict_dict_typecheck.flow` — D-13 baseline regression-pin
- [ ] `tests/strict/showcase_strict.flow` — ~16-bar piece demonstrating strict-mode composer ergonomics naturally

### Site-Inventory Manifest (Wave 0 deliverable)

- [ ] `.planning/phases/44-strict-mode/strict-error-manifest.csv` — Wave 0 reconciliation of the AUDIT count discrepancy (~120 grep vs ~117 AUDIT). Drives xUnit `[Theory]` + `[InlineData]` rows for the ~126 mechanical site-rewrites.

xUnit framework already installed via `flow-lang.Tests.csproj`. Existing `flow-lang.Tests/Integration/Phase42/ClampGrepConsistencyTests.cs` provides the template (file:line pinning + per-site verbatim string assertion).

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| REPL `:strict on` flips green status panel indicator | REQ-STRICT-13 | LiveStatusPanel ANSI rendering; redirect-aware fallback hides indicator outside TTY | Run `dotnet run --project flow-interpreter`, type `:strict on`, confirm panel updates; type `:strict off`, confirm reverts |
| Strict-mode `showcase_strict.flow` audio sounds correct | REQ-STRICT-14 | Audio rendering not byte-checkable across hardware; cmp-clean only verifies same-platform consistency | `dotnet run --project flow-interpreter tests/strict/showcase_strict.flow && play showcase_strict.wav` |
| Strict pragma + live-reload feedback latency under 200ms watch debounce | REQ-STRICT-12 + Phase 38 LIVE-02 | File-watch timing observed behaviorally, not byte-asserted | Run `dotnet run --project flow-interpreter -- --watch tests/strict/test_strict_axis_a_overload.flow`, edit file, confirm sub-200ms re-eval |

All other phase behaviors have automated verification via xUnit + integration scripts.

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (entire Phase44 test directory)
- [ ] No watch-mode flags (xUnit one-shot only)
- [ ] Feedback latency < 30s per-task
- [ ] `nyquist_compliant: true` set in frontmatter after planner finishes

**Approval:** pending — flip to `approved YYYY-MM-DD` after gsd-plan-checker verifies REQ→Plan→Task→Test traceability.
