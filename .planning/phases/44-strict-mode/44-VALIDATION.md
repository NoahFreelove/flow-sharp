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
| 44-00-* | 00 | 1 | REQ-STRICT-08 | T-44-00-02 | Wave 0 manifest deliverable — strict-error-manifest.csv (~126 in-scope rows + 5 carve-outs) drives every xUnit `[Theory]` in Plans 44-05/06/07; StrictErrorManifestLoader partitions HIGH/MED/LOW + carve-out subsets | unit | `dotnet test --filter "FullyQualifiedName~Phase44.StrictErrorManifestSanityTests"` | ❌ W0 | ⬜ pending |
| 44-01-* | 01 | 1 | REQ-STRICT-01 | — | `enable strict;` pragma recognized; unknown typo gets levenshtein suggestion; pragma-position regression for `enable strict;` after first statement | unit | `dotnet test --filter "FullyQualifiedName~Phase44.PragmaRegistryStrictTests"` | ❌ W0 | ⬜ pending |
| 44-01-* | 01 | 1 | REQ-STRICT-02 | — | `ctx.StrictMode=true` set after PragmaScanner detects `enable strict;`; strict file importing non-strict module runs imported procs non-strict | unit | `dotnet test --filter "FullyQualifiedName~Phase44.ExecutionContextStrictModeTests|FullyQualifiedName~Phase44.ModuleLoaderStrictPropagationTests"` | ❌ W0 | ⬜ pending |
| 44-02-* | 02 | 2 | REQ-STRICT-02 | — | ProcDeclaration.IsStrict AST capture per declaring file + Interpreter push/pop on proc entry | unit | `dotnet test --filter "FullyQualifiedName~Phase44.ProcDeclarationStrictAstTests"` | ❌ W0 | ⬜ pending |
| 44-02-* | 02 | 2 | REQ-STRICT-03 | — | `ctx.CallerStrictMode` snapshotted at call dispatch into stdlib (D-05 two-field design) | unit | `dotnet test --filter "FullyQualifiedName~Phase44.CallerStrictModeSnapshotTests"` | ❌ W0 | ⬜ pending |
| 44-03-* | 03 | 3 | REQ-STRICT-04 | — | OverloadResolver disables +100 convertible tier when ctx.StrictMode=true; covers BOTH CanConvertTo + IsCompatibleWith implicit conversions (Pitfall 1) | unit | `dotnet test --filter "FullyQualifiedName~Phase44.OverloadResolverStrictTierTests"` | ❌ W0 | ⬜ pending |
| 44-04-* | 04 | 2 | REQ-STRICT-05 | — | 6 forward conv builtins (db/hz/ms/sec/cents/semitones) accept all 4 numeric + idempotent on tagged target | unit | `dotnet test --filter "FullyQualifiedName~Phase44.ExplicitConversionForwardTests"` | ❌ W0 | ⬜ pending |
| 44-04-* | 04 | 2 | REQ-STRICT-06 | — | 4 reverse extractors (double/float/int/long) accept all 6 tagged music types | unit | `dotnet test --filter "FullyQualifiedName~Phase44.ExplicitConversionReverseTests"` | ❌ W0 | ⬜ pending |
| 44-05-* | 05 | 3 | REQ-STRICT-07 | — | 13 §6a clamp sites error in strict with verbatim message `[strict] <tag> <issue>` | unit (Theory) | `dotnet test --filter "FullyQualifiedName~Phase44.Axis_B_ClampSiteTests"` | ❌ W0 | ⬜ pending |
| 44-05-* | 05 | 3 | REQ-STRICT-07 | — | Inventory regression: exactly 13 input-perimeter Math.Clamp sites remain (mirrors Phase 42 ClampGrepConsistencyTests) | unit | `dotnet test --filter "FullyQualifiedName~Phase44.Phase44ClampGrepConsistencyTests"` | ❌ W0 | ⬜ pending |
| 44-06-* | 06 | 4 | REQ-STRICT-08 | — | HIGH-priority advisory sites (~79: SFZ + patterns + render + match + DSP) error in strict | unit (Theory) | `dotnet test --filter "FullyQualifiedName~Phase44.Axis_B_AdvisorySiteTests_High"` | ❌ W0 | ⬜ pending |
| 44-07-* | 07 | 5 | REQ-STRICT-08 | — | MED/LOW advisory sites (~34: chaos + generative + abc + mml + tuning + osc + audio-in + piano + midi + harmony) error in strict | unit (Theory) | `dotnet test --filter "FullyQualifiedName~Phase44.Axis_B_AdvisorySiteTests_MedLow"` | ❌ W0 | ⬜ pending |
| 44-08-* | 08 | 3 | REQ-STRICT-10 | — | Pre-strict bug fix: non-strict `(print 42)` auto-strs via `(str x)`; non-strict `if Int x` truthy-coerces; `(not)` registered (RESEARCH A6); D-12 last-truthy `(and 1 "foo")` → `"foo"`, `(or false 42)` → `42` | unit | `dotnet test --filter "FullyQualifiedName~Phase44.PrintCharitablyTests|FullyQualifiedName~Phase44.IfTruthyCoerceTests|FullyQualifiedName~Phase44.NotBuiltinTests|FullyQualifiedName~Phase44.AndOrLastTruthyTests"` | ❌ W0 | ⬜ pending |
| 44-09-* | 09 | 4 | REQ-STRICT-09 | — | Strict `(and Int Int)`/`(or Int Int)`/`(not Int)`/`if Int` all error; strict returns Bool from logical ops | unit | `dotnet test --filter "FullyQualifiedName~Phase44.AxisCBoolRequiredTests"` | ❌ W0 | ⬜ pending |
| 44-09-* | 09 | 4 | REQ-STRICT-11 | — | Cross-type `(gt 1 1.0)`, `(lt 1 1.0)`, `(gte 1 1.0)`, `(lte 1 1.0)` all error in strict; `(equals 1 1.0)` returns false strict | unit | `dotnet test --filter "FullyQualifiedName~Phase44.CrossTypeComparisonStrictTests"` | ❌ W0 | ⬜ pending |
| 44-10-* | 10 | 4 | REQ-STRICT-12 | — | `enable strict;` file with `live { }` block: body runs strict on re-eval; `[live] entering` advisory stays charitable (carve-out) | unit | `dotnet test --filter "FullyQualifiedName~Phase44.LiveBlockStrictTests"` | ❌ W0 | ⬜ pending |
| 44-10-* | 10 | 4 | REQ-STRICT-13 | — | REPL `:strict on` / `:strict off` toggles `ctx.StrictMode`; sticky session | unit | `dotnet test --filter "FullyQualifiedName~Phase44.ReplStrictMetaCommandTests"` | ❌ W0 | ⬜ pending |
| 44-11-* | 11 | 6 | REQ-STRICT-14 | — | All `tests/strict/test_*.flow` files run to completion; `showcase_strict.flow` renders | integration | `dotnet test --filter "FullyQualifiedName~Phase44.StrictFlowScriptSuiteTests"` | ❌ W0 | ⬜ pending |
| 44-11-* | 11 | 6 | REQ-STRICT-15 | — | Two-run cmp-clean determinism preserved after strict-mode introduction (Theory over all 7 strict fixtures; no PRNG sites added; Axis B advisory→error path is mechanical) | integration | `dotnet test --filter "FullyQualifiedName~Phase44.Phase44TwoRunDeterminismTests"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*Note:* Task IDs `44-NN-*` reflect plan-file pre-counting; final task-level IDs assigned by planner at PLAN.md emission.

---

## Wave 0 Requirements

The Phase 44 test directory does not exist today; entire suite is Wave 0:

### xUnit Negative Tests (~126 verbatim string assertions)

- [ ] `flow-lang.Tests/Integration/Phase44/` directory creation (Plan 44-00 Task 1; Phase 43 integration-layout convention)
- [ ] `flow-lang.Tests/Integration/Phase44/StrictModeNegativeTests.cs` — ~126 Facts pinning error strings (xUnit `[Theory]` + `[InlineData]`; strings from AUDIT §6a Column 5 + per-site §6b sentinels with `[strict]` prefix; sourced via StrictErrorManifestLoader)
- [ ] `flow-lang.Tests/Integration/Phase44/ExplicitConversionForwardTests.cs` + `ExplicitConversionReverseTests.cs` — forward + reverse direction matrix (24 forward + 24 reverse = 48 cases)
- [ ] `flow-lang.Tests/Integration/Phase44/OverloadResolverStrictTierTests.cs` — Axis A: confirm +100 disabled, +500 + +1000 preserved
- [ ] `flow-lang.Tests/Integration/Phase44/PrintCharitablyTests.cs` — pre-strict bug fix: non-strict `(print Int x)` auto-strs
- [ ] `flow-lang.Tests/Integration/Phase44/IfTruthyCoerceTests.cs` — pre-strict bug fix: non-strict `if Int x` truthy-coerces
- [ ] `flow-lang.Tests/Integration/Phase44/NotBuiltinTests.cs` — `(not)` builtin registered (RESEARCH A6) + non-strict charitable truthy + strict Bool-required
- [ ] `flow-lang.Tests/Integration/Phase44/AndOrLastTruthyTests.cs` — D-12 non-strict `(and)`/`(or)` last-truthy semantics ((and 1 "foo") → "foo", (or false 42) → 42)
- [ ] `flow-lang.Tests/Integration/Phase44/CallerStrictModeSnapshotTests.cs` — D-05 two-field design
- [ ] `flow-lang.Tests/Integration/Phase44/ModuleLoaderStrictPropagationTests.cs` — D-03 per-declaring-file
- [ ] `flow-lang.Tests/Integration/Phase44/PragmaRegistryStrictTests.cs` — D-04 registry entry + typo suggestion + Phase 21 pragma-position regression for `strict`
- [ ] `flow-lang.Tests/Integration/Phase44/LiveBlockStrictTests.cs` — D-15 strict-in-live-blocks
- [ ] `flow-lang.Tests/Integration/Phase44/ReplStrictMetaCommandTests.cs` — D-16 sticky session + `:strict on/off`
- [ ] `flow-lang.Tests/Integration/Phase44/AxisCBoolRequiredTests.cs` — D-12 strict Bool requirement
- [ ] `flow-lang.Tests/Integration/Phase44/CrossTypeComparisonStrictTests.cs` — D-11 comparison error / equals false
- [ ] `flow-lang.Tests/Integration/Phase44/DictTypeStrictRegressionTests.cs` — D-13 Dict lookup type-strict regression-pin
- [ ] `flow-lang.Tests/Integration/Phase44/Phase44TwoRunDeterminismTests.cs` — REQ-STRICT-15 cmp-clean preservation (Theory over all 7 `tests/strict/*.flow` fixtures per W10)
- [ ] `flow-lang.Tests/Integration/Phase44/StrictFlowScriptSuiteTests.cs` — REQ-STRICT-14 integration phase-gate (Process.Start dotnet run per file)
- [ ] `flow-lang.Tests/Integration/Phase44/Phase44ClampGrepConsistencyTests.cs` — mirrors Phase 42 ClampGrepConsistencyTests; pins exactly 13 input-perimeter clamps remain
- [ ] `flow-lang.Tests/Integration/Phase44/CarveOutsPreservedTests.cs` — Plan 44-07 anti-Pitfall-2 regression-pin (5 carve-out sites still WarnOnce in both modes)

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
