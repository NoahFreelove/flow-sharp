---
phase: 20
phase_slug: cheap-defer-closures-multi-letter-enharmonic-edges
date: 2026-04-26
nyquist_compliant: true
---

# Phase 20 Validation Strategy

Extracted from `20-RESEARCH.md` §Validation Architecture — separate file per Nyquist Dimension 8e gate.

## Test Framework

| Property | Value |
|---|---|
| Framework | xUnit.v3 (existing flow-lang.Tests project) |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` (existing) |
| Quick run command | `dotnet test --filter "FullyQualifiedName~Phase20"` |
| Full suite command | `dotnet test flow-sharp.sln` |
| Baseline at Phase 20 start | 340/340 GREEN (post-Phase-19) |

## Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DEFER-01 | `(range 0 5)` returns Array[Int] of length 5 with values 0..4 | unit | `dotnet test --filter "FullyQualifiedName~Phase20.RangeTests.TwoArg_DefaultStep"` | ❌ Wave 0 (created in 20-01) |
| DEFER-01 | `(range 0 10 2)` returns Array[Int] of length 5 with values 0,2,4,6,8 | unit | `dotnet test --filter "FullyQualifiedName~Phase20.RangeTests.ThreeArg_PositiveStep"` | ❌ Wave 0 (created in 20-01) |
| DEFER-01 | `(range 5 0 -1)` returns Array[Int] of length 5 with values 5..1 | unit | `dotnet test --filter "FullyQualifiedName~Phase20.RangeTests.NegativeStep_IteratesBackward"` | ❌ Wave 0 (created in 20-01) |
| DEFER-01 | `(range 0 0)` returns empty Array[Int] | unit | `dotnet test --filter "FullyQualifiedName~Phase20.RangeTests.EmptyWhenStartEqualsEnd"` | ❌ Wave 0 (created in 20-01) |
| DEFER-01 | `(range 5 0)` (default step +1, no progress) returns empty | unit | `dotnet test --filter "FullyQualifiedName~Phase20.RangeTests.UnsatisfiableReturnsEmpty"` | ❌ Wave 0 (created in 20-01) |
| DEFER-01 | `range` step=0 throws | unit | `dotnet test --filter "FullyQualifiedName~Phase20.RangeTests.ZeroStepThrows"` | ❌ Wave 0 (created in 20-01) |
| DEFER-01 | `test_custom_oscillator.flow` runs to completion (Test 4 unblocked) | integration | `dotnet test --filter "FullyQualifiedName~test_custom_oscillator"` | ✅ existing (FlowScriptData.cs ExpectedErrorScripts entry REMOVED in 20-04) |
| DEFER-04 | `enharmonic(E4)` returns `Fb4` | unit | `dotnet test --filter "FullyQualifiedName~Phase20.EnharmonicEdgesTests.NoKey_E4_RespellsFb4"` | ❌ Wave 0 (created in 20-02) |
| DEFER-04 | `enharmonic(F4)` returns `E#4` | unit | `dotnet test --filter "FullyQualifiedName~Phase20.EnharmonicEdgesTests.NoKey_F4_RespellsEsharp4"` | ❌ Wave 0 (created in 20-02) |
| DEFER-04 | `enharmonic(B4)` returns `Cb5` (octave +1) | unit | `dotnet test --filter "FullyQualifiedName~Phase20.EnharmonicEdgesTests.NoKey_B4_RespellsCb5"` | ❌ Wave 0 (created in 20-02) |
| DEFER-04 | `enharmonic(C4)` returns `B#3` (octave −1) | unit | `dotnet test --filter "FullyQualifiedName~Phase20.EnharmonicEdgesTests.NoKey_C4_RespellsBsharp3"` | ❌ Wave 0 (created in 20-02) |
| DEFER-04 | Round-trip pitch equivalence for every chromatic note | unit (Theory) | `dotnet test --filter "FullyQualifiedName~Phase20.EnharmonicEdgesTests.RoundTrip_PitchEquivalent"` | ❌ Wave 0 (created in 20-02) |
| DEFER-04 | D, G, A naturals remain unchanged | unit | `dotnet test --filter "FullyQualifiedName~Phase20.EnharmonicEdgesTests.NoKey_NonEdgeNaturalsUnchanged"` | ❌ Wave 0 (created in 20-02) |
| DEFER-04 | Phase14/EnharmonicTests.cs NoKey_NaturalUnchanged_* MIGRATE (4 Facts → invert) | unit | `dotnet test --filter "FullyQualifiedName~Phase14.EnharmonicTests"` | ✅ existing (rename + assertion-flip in 20-02 atomic commit) |
| DEFER-04 | In-key diatonic preserves spelling (D-USER-B) | unit | `dotnet test --filter "FullyQualifiedName~Phase20.EnharmonicEdgesTests.InKey_Fmajor_E4_PreservesDiatonic"` | ❌ Wave 0 (created in 20-02) |
| DEFER-05 | `(slice [1,2,3,4,5] -3 5)` returns `[3,4,5]` | unit | `dotnet test --filter "FullyQualifiedName~Phase20.SliceNegativeTests.Array_NegativeStart_FromEnd"` | ❌ Wave 0 (created in 20-03) |
| DEFER-05 | `(slice [1,2,3,4,5] 0 -1)` returns `[1,2,3,4]` | unit | `dotnet test --filter "FullyQualifiedName~Phase20.SliceNegativeTests.Array_NegativeEnd_FromEnd"` | ❌ Wave 0 (created in 20-03) |
| DEFER-05 | `slice` Sequence overload accepts negative-from-end | unit | `dotnet test --filter "FullyQualifiedName~Phase20.SliceNegativeTests.Sequence_NegativeStart_FromEnd"` | ❌ Wave 0 (created in 20-03) |
| DEFER-05 | Extreme negative clamps post-normalization (D-USER-D) | unit | `dotnet test --filter "FullyQualifiedName~Phase20.SliceNegativeTests.Array_ExtremeNegativeStartClampsToZero"` | ❌ Wave 0 (created in 20-03) |
| DEFER-05 | Existing Phase14/SliceTests.cs Facts continue to pass | unit | `dotnet test --filter "FullyQualifiedName~Phase14.SliceTests"` | ✅ existing (no migration — verification matrix proves cases coincide) |
| DEFER-05 | Repository grep `slice.*,.*,.*-` empty in tests/ | manual | `grep -rn "slice.*,.*,.*-" tests/ \| wc -l` | ✅ verified at research time |
| All | Full xUnit suite GREEN | regression | `dotnet test flow-sharp.sln` | ✅ existing 340/340 baseline |
| All | All `tests/*.flow` Theory rows GREEN | integration | `dotnet test --filter "FullyQualifiedName~FlowScriptTests"` | ✅ existing harness |
| All | Phase 18 byte-identical regression gate | regression | `dotnet test --filter "FullyQualifiedName~Phase18"` returns 19 passed | ✅ existing (binding gate per plan) |

## Sampling Rate

- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Phase20"` (fast, ~2s for new Facts)
- **Per wave merge:** `dotnet test flow-sharp.sln` (full suite, ~17s baseline + new Facts)
- **Phase gate:** Full suite GREEN before `/gsd-verify-work`. Plus repository grep transcript in VERIFICATION.md and Phase 18 regression-gate confirmation.

## Wave 0 Gaps

- [ ] `flow-lang.Tests/Unit/Phase20/` directory creation (in 20-01 Task 1)
- [ ] `flow-lang.Tests/Unit/Phase20/RangeTests.cs` — covers DEFER-01 (≥6 Facts) — created in 20-01
- [ ] `flow-lang.Tests/Unit/Phase20/EnharmonicEdgesTests.cs` — covers DEFER-04 (≥7 Facts: 4 edges + round-trip Theory + non-edge naturals + in-key diatonic) — created in 20-02
- [ ] `flow-lang.Tests/Unit/Phase20/SliceNegativeTests.cs` — covers DEFER-05 (≥4 Facts: array negative-start, array negative-end, sequence negative, extreme-negative clamp) — created in 20-03
- [ ] `tests/test_range.flow` — `.flow` integration test — created in 20-01
- [ ] `tests/test_enharmonic_edges.flow` — `.flow` integration test (NEW file, do NOT edit existing test_enharmonic.flow per Pitfall 2) — created in 20-02
- [ ] `tests/test_slice_negative.flow` — `.flow` integration test (NEW file) — created in 20-03
- [ ] Framework install: NONE — existing xUnit infrastructure covers all phase requirements

## Sentinel Pinning

Per CLAUDE.md "Tests are .flow scripts ... verified by their console output" precedent:

- `tests/test_range.flow` prints `range ok len=5,5,5` (one per acceptance case)
- `tests/test_enharmonic_edges.flow` prints `edge ok E→Fb F→E# B→Cb C→B#`
- `tests/test_slice_negative.flow` prints `slice neg ok len=3,4,1`

These sentinels are required-substring entries in `flow-lang.Tests/FlowScriptData.cs` so the existing Theory harness gates them.

## Coverage Cap

| REQ | Acceptance criteria count (from REQUIREMENTS.md) | Test count (this strategy) |
|-----|---|---|
| DEFER-01 | 3 examples + standard semantics | ≥6 Facts |
| DEFER-04 | 6 examples + round-trip property | ≥7 Facts (1 Theory pins round-trip across 12 chromatic pitches × 2 octaves) |
| DEFER-05 | 2 examples + behavioral change | ≥4 Facts |
| **Total** | 11 acceptance items | **≥17 Facts + 3 .flow Theory rows** |

Coverage ratio: 17/11 = 1.55× — exceeds Nyquist 1× minimum.
