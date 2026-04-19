---
phase: 13
slug: nyquist-validation-backfill
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-19
---

# Phase 13 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Phase 13 is pure docs + test-backfill — minimal Phase-13-own validation per CONTEXT D-24.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (existing `flow-lang.Tests` project — established by Phase 12) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (no changes needed) |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~{NewValidationTestClass}"` |
| **Full suite command** | `dotnet test flow-sharp.sln` |
| **Estimated runtime** | ~30–60 seconds (adds ~7 new Facts to existing 68-test baseline) |

---

## Sampling Rate

- **After every task commit:** `dotnet test --filter` scoped to the just-touched test class (or the full suite if only `RequiredSentinels` entries were added)
- **After every plan wave:** `dotnet test flow-sharp.sln`
- **Before `/gsd-verify-work`:** Full suite must be green; every new `*-VALIDATION.md` must exist per presence-check
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 13-01-* | 01 | 1 | TEST-04 (Phase 6 slice) | — | 06-VALIDATION.md created; §Per-Task map covers QOL-01, FIX-01, FIX-02, FIX-03; FIX-02×AUDIO-06 integration Fact exists and passes | doc+unit | `test -f .planning/phases/06-diagnostics-bug-fixes/06-VALIDATION.md && dotnet test --filter ~Phase06` | ❌ W0 | ⬜ pending |
| 13-02-* | 02 | 1 | TEST-04 (Phase 7 slice) | — | 07-VALIDATION.md created; §Per-Task map covers DX-01..04; existing Theory rows cited with tightened sentinels | doc+unit | `test -f .planning/phases/07-developer-experience/07-VALIDATION.md && dotnet test --filter ~Phase07` | ❌ W0 | ⬜ pending |
| 13-03-* | 03 | 1 | TEST-04 (Phase 8 slice) | — | 08-VALIDATION.md created; §Per-Task map covers AUDIO-05/06/07; per-section gain ratio pin verified | doc+unit | `test -f .planning/phases/08-audio-production/08-VALIDATION.md && dotnet test --filter ~Phase08` | ❌ W0 | ⬜ pending |
| 13-04-* | 04 | 1 | TEST-04 (Phase 9 slice) | — | 09-VALIDATION.md created; §Per-Task map covers AUDIO-08, QOL-02; tempoRamp length pin + tutorial exit-0 pin verified | doc+unit | `test -f .planning/phases/09-advanced-features/09-VALIDATION.md && dotnet test --filter ~Phase09` | ❌ W0 | ⬜ pending |
| 13-05-* | 05 | 1 | TEST-04 (Phase 10 promotion + TEST-04 closure) | — | 10-VALIDATION.md frontmatter `nyquist_compliant: true`; VOC-01 88200-sample pin Fact passes; REQUIREMENTS.md TEST-04 row marked Complete | doc+unit | `grep "nyquist_compliant: true" .planning/phases/10-vocalization/10-VALIDATION.md && dotnet test --filter ~Phase10 && grep "\[x\] \*\*TEST-04" .planning/REQUIREMENTS.md` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Integration/` directory — created by whichever Wave-1 plan needs it first (13-01 for FIX-02×AUDIO-06 Integration Fact)
- [ ] Shared helper: `flow-lang.Tests/Integration/AudioTestHelpers.cs` — if any plan needs zero-crossing / peak-detection utilities (Phase 10 may share into this location)

*All other infrastructure already exists — Phase 12 established csproj, FlowEngineRunner fixture, and FlowScriptData glob.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Each new VALIDATION.md satisfies the sign-off checklist | TEST-04 | Human reviewer verifies the six sign-off boxes at the bottom of each created `*-VALIDATION.md` after Pass 2 | Read each of `06-VALIDATION.md`, `07-VALIDATION.md`, `08-VALIDATION.md`, `09-VALIDATION.md`, `10-VALIDATION.md`; confirm each item checked or explicitly justified |
| §Divergences content is accurate | TEST-04 | Requires cross-referencing with REQUIREMENTS.md wording | Spot-check 1–2 Divergence entries per phase against v1.1-REQUIREMENTS.md |

---

## Observable Invariants (Phase 13 own Nyquist pin)

Each invariant is a concrete check that would fail if Phase 13's outputs were removed:

1. **13-01:** `06-VALIDATION.md` exists with `phase: 6` frontmatter and `nyquist_compliant: true`
2. **13-02:** `07-VALIDATION.md` exists with `phase: 7` frontmatter and `nyquist_compliant: true`
3. **13-03:** `08-VALIDATION.md` exists with `phase: 8` frontmatter and `nyquist_compliant: true`
4. **13-04:** `09-VALIDATION.md` exists with `phase: 9` frontmatter and `nyquist_compliant: true`
5. **13-05:** `10-VALIDATION.md` frontmatter has `nyquist_compliant: true` (promoted from false)
6. **13-05:** `.planning/REQUIREMENTS.md` contains `[x] **TEST-04`
7. **Full suite green:** `dotnet test flow-sharp.sln` exits 0 with ≥ 75 tests (existing 68 + ≥ 7 new Facts per RESEARCH.md count)
8. **FIX-02×AUDIO-06 regression gate:** new Integration Fact for `section { gain N { | notes | } }` asserts non-zero frame count (per RESEARCH.md pitfall — existing test prints PASSED regardless)
9. **VOC-01 sample-count pin:** `sing("ah", C4, 2.0)` returns buffer with `Samples.Count == 88200` (per CONTEXT D-18)

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter (after plans complete)

**Approval:** pending
