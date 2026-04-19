---
phase: 9
slug: advanced-features
status: passed
nyquist_compliant: true
wave_0_complete: true
created: 2026-04-19
promoted: 2026-04-20
backfilled: true
---

# Phase 9 — Validation Strategy

> Retroactive validation contract for v1.1 Phase 9 (Advanced Features).
> Authored under TEST-04 (Phase 13 Nyquist Validation Backfill) via two-pass strict
> authorship per 13-CONTEXT D-13. Pass 1 draft below was written reading ONLY
> `v1.1-REQUIREMENTS.md` and the Phase 9 goal + success criteria from
> `v1.1-ROADMAP.md`; source code, test files, and prior phase SUMMARY.md files
> were NOT consulted during Pass 1. Pass 2 reconciles against shipped reality.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit.v3 3.2.2 |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` |
| **Quick run command** | `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase09"` |
| **Full suite command** | `dotnet test flow-sharp.sln` |
| **Estimated runtime** | ~20 seconds full suite |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter` scoped to the just-touched Fact class
- **After every plan wave:** Run `dotnet test flow-sharp.sln`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 09-backfill-01 | 13-04 | 1 | AUDIO-08 | — | `tempoRamp` produces ritardando (more frames than constant fast) and accelerando (fewer frames than constant slow) | integration (Theory) | `RequiredSentinels["test_tempo_ramp.flow"]` via `FlowScriptTests.RunsToCompletion` | ✅ | ✅ green |
| 09-backfill-02 | 13-04 | 1 | QOL-02 | — | `examples/tutorial.flow` runs via FlowEngineRunner.RunFile with errorCount == 0 | integration | `dotnet test --filter "FullyQualifiedName~TutorialTests"` | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `flow-lang.Tests/Integration/Phase09/` — created by plan 13-04 (contains `TutorialTests.cs`)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Audible tempo transition character (gradual, not abrupt) | AUDIO-08 | Perceptual — requires listening to confirm smooth deceleration/acceleration | Render a tempoRamp via `dotnet run --project flow-interpreter tests/test_tempo_ramp.flow`, play output WAV, confirm gradual BPM change |
| Tutorial pedagogical clarity (guides user from basics to full songs) | QOL-02 | Subjective — requires reading the tutorial as a new user | Open `examples/tutorial.flow`, verify each section builds on prior concepts |

---

## Observable Invariants

Each invariant is a concrete check that would fail if the feature were removed:

1. **AUDIO-08:** stdout of `tests/test_tempo_ramp.flow` contains three boolean-result strings proving the ritardando/accelerando invariants: (a) tempoRamp produces non-zero buffer, (b) ritardando produces more frames than constant-fast (i.e., `tempoRamp(seq, 120, 80)` slower than `tempoRamp(seq, 120, 120)`), (c) accelerando produces fewer frames than constant-slow. Exact sentinel strings pinned by Pass 2.
2. **QOL-02:** `FlowEngineRunner.RunFile("examples/tutorial.flow")` returns `errorCount == 0` (tutorial script runs to completion without interpreter errors under the post-Phase-12 stability-fix codebase).

---

## Pass 1 Draft

*Authored by reading ONLY `.planning/milestones/v1.1-REQUIREMENTS.md` + the Phase 9 goal/success criteria from `.planning/milestones/v1.1-ROADMAP.md`. Source code, SUMMARY.md files, and existing test files NOT consulted.*

For each requirement: the assertion text the author expected to write.

- **AUDIO-08:** expected `tempoRamp(sequence, startBPM, endBPM) -> Buffer` to produce a buffer that, when rendered at `startBPM → endBPM`, has a frame-count different from a constant-tempo render. For ritardando (slowing down, e.g., `tempoRamp(seq, 120, 80)`), the total duration should be LONGER than at the faster constant tempo (fewer notes per second = more seconds total). For accelerando (speeding up), the total duration should be SHORTER than at the slower constant tempo. An existing `tests/test_tempo_ramp.flow` script should encode these invariants and emit pass/fail sentinels.
- **QOL-02:** expected an `examples/tutorial.flow` script that walks a new user from basic expressions through note streams, sections, and full song creation. The script must run without interpreter errors — if it doesn't, there is no tutorial to speak of. Pin: `FlowEngineRunner.RunFile("examples/tutorial.flow")` returns `errorCount == 0`.

---

## Pass 2 Implementation Map

*Authored after empirical verification against shipped code and existing test scripts.*

- **AUDIO-08:** `flow-lang.Tests/FlowScriptData.cs::RequiredSentinels["test_tempo_ramp.flow"]` — pinned on three empirical boolean-result strings captured via `dotnet run --project flow-interpreter tests/test_tempo_ramp.flow`:
  - `"Test 1 - tempoRamp produces non-zero buffer: true"`
  - `"Test 2 - Ritardando produces more frames than constant fast: true"`
  - `"Test 3 - Accelerando produces fewer frames than constant slow: true"`

  The script (`tests/test_tempo_ramp.flow`) encodes the ritardando/accelerando invariants as in-script boolean tests printed via `(concat "Test N - …: " (str testN))` (Bool `str` formats as `"true"`/`"false"`). Empirical frame counts for context:
  - `constFast(120 BPM)`: 88200 frames (= 4 quarter notes × 0.5s × 44100Hz)
  - `ritardando(120→80)`: 105840 frames (> 88200 ✓)
  - `accelerando(80→120)`: 105840 frames (< `constSlow(80)` = 132300 ✓)
  - `constSlow(80 BPM)`: 132300 frames

  Existing `FlowScriptTests.RunsToCompletion` Theory row exercises this script; the sentinel append converts it from an `errorCount==0` gate to a substring-pinned regression gate. Any future refactor that reverts tempoRamp to a naive constant-BPM render flips Test 2 or Test 3 to `"false"` and the row fails.

- **QOL-02:** `flow-lang.Tests/Integration/Phase09/TutorialTests.cs::TutorialRunsToCompletion` — loads `examples/tutorial.flow` via `FlowEngineRunner.RunFile`, asserts `ok == true` AND `errorCount == 0`. CWD pivot mirrors `FlowScriptTests.cs:19-24` (the tutorial writes to `/tmp/flow_tutorial_output.wav` — absolute path — so pivot is defensive, not functional). Runtime ~1s. Verified GREEN under HEAD (post-Phase-12 stability fixes); no Skip required; no Ultra-Important Finding required.

---

## Divergences

*Record of Pass 1-vs-Pass-2 mismatches per 13-CONTEXT D-14. Mirrors `12-VERIFICATION.md §Key Discrepancy Notes` format.*

**No divergences — AUDIO-08 and QOL-02 were both literally testable as drafted.**

Pass 1 drafted the AUDIO-08 invariants ("ritardando should produce more frames, accelerando should produce fewer") and the QOL-02 invariant ("tutorial runs without errors") from REQUIREMENTS.md + ROADMAP.md wording alone. Pass 2 empirical reality:

- The shipped `tests/test_tempo_ramp.flow` already encodes the exact three invariants Pass 1 hypothesized, in the exact boolean-result-concat idiom Pass 1 anticipated. Three sentinel strings match verbatim. No assertion-text adjustment needed.
- The shipped `examples/tutorial.flow` (348 lines, from Phase 9 Plan 09-02) runs cleanly under the post-Phase-12 codebase — the stability fixes of Phase 12 did not regress the tutorial. Exit code 0, empty stderr, WAV file produced at `/tmp/flow_tutorial_output.wav`. The defensive `[Fact(Skip = …)]` branch documented in the plan was NOT triggered — no deferral to Phase 16 QOL-03 needed for correctness (the separate stylistic-drift observation in `v1.1-MILESTONE-AUDIT.md` about legacy `Note:` comments / `exportWav` usage remains a Phase 16 concern, but does not block QOL-02 validation).

This is the second Phase 13 plan (after 13-01) to produce zero Divergences. The v1.1 audit and post-v1.1 stability work had already reconciled Phase 9's feature behavior with its stated requirements.

---

## Ultra-Important Finding

None. `examples/tutorial.flow` runs GREEN under HEAD; no deferral to Phase 16 (QOL-03) required for QOL-02 validation. Phase 16 remains scoped to tutorial feature-refresh (exercising v1.1+v1.2 features end-to-end per ROADMAP Phase 16 goal), independent of this pin.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** 2026-04-20 (Plan 13-04 Pass 2 — 77/77 `dotnet test flow-sharp.sln` GREEN)
