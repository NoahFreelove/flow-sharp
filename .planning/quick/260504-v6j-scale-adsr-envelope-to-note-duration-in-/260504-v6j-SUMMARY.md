---
phase: quick-260504-v6j
plan: 01
subsystem: audio
tags: [adsr, envelope, synthesizer, dsp, regression-fix]

# Dependency graph
requires:
  - phase: existing
    provides: "EnvelopeProcessor.GenerateADSRCurve and the eight synths that funnel through SynthUtils.GenerateADSR"
provides:
  - "Short notes (32nd-note staccato, MIDI-imported quick passages) now render with a complete attack+decay+release envelope shape — no zero-frame release, no abrupt non-zero cutoff click"
  - "Release loop now lands the final sample at exactly 0 (was sustain/N) for ALL note lengths, eliminating a half-sample-shaped silent residue at every note tail"
  - "Six xUnit regression facts in flow-lang.Tests/Unit/QuickFixes/AdsrShortNoteEnvelopeFacts.cs pinning the new short-note shape contract"
affects: [piano, brass, sax, flute, strings, organ, wavetable, chopin-nocturne-rendering, midi-conversion-quality]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Envelope phase scale-to-fit: when requested attack+decay+release exceeds totalFrames, scale all three proportionally and dump leftover floor-rounding into release"

key-files:
  created:
    - flow-lang.Tests/Unit/QuickFixes/AdsrShortNoteEnvelopeFacts.cs
  modified:
    - flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs

key-decisions:
  - "Scale-to-fit instead of clamp: proportional shrink preserves the envelope SHAPE (just compressed in time) and guarantees release > 0 for any reasonable totalFrames"
  - "Floor-rounding leftover (1-3 frames) goes to release so the final sample lands at exactly 0"
  - "Release loop body now uses t = (i+1)/N instead of i/N so the final sample is exactly 0 for every note length, not just short ones"
  - "GenerateARCurve has the same latent clamp bug but is OUT OF SCOPE — no real-world AR caller in the current synth set passes a + r > totalFrames for typical note durations"

patterns-established:
  - "Phase-scale-to-fit for time-tied envelopes: when a phase budget exceeds the available frames, scale phases proportionally; allocate floor-rounding leftover to the phase whose tail must land at zero"

requirements-completed:
  - QUICK-260504-v6j

# Metrics
duration: ~25 min
completed: 2026-05-04
---

# Quick 260504-v6j: Scale ADSR Envelope to Note Duration

**Replace additive-clamp logic in `GenerateADSRCurve` with proportional scale-to-fit so short notes (32nd-note staccato, MIDI-imported quick passages) render with a complete attack+decay+release shape instead of attack+truncated-decay+cliff.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-05-05T02:12:00Z (approx)
- **Completed:** 2026-05-05T02:37:11Z
- **Tasks:** 1 auto + 1 human-verify (deferred — see below)
- **Files modified:** 2

## Accomplishments

- Root-cause fix to `EnvelopeProcessor.GenerateADSRCurve`: when a note's frame count is shorter than `attack + decay + release`, all three phases now scale down proportionally instead of release being clamped to zero
- Six new xUnit regression facts (`AdsrShortNoteEnvelopeFacts.cs`) covering: short-note release tail, short-note attack preservation, no-cliff invariant, long-note shape preservation, medium-note edge case, and zero-duration safety
- Release loop body adjusted (`t = (i+1)/N`) so the final sample lands at exactly 0 for every note length — long-note correctness improvement that came along for free
- Full `flow-lang.Tests` suite (697 tests) passes with zero regressions
- `dotnet build` clean (zero new warnings introduced; pre-existing nullable warnings unchanged)

## Task Commits

Each task was committed atomically:

1. **Task 1 (RED):** add failing facts for short-note ADSR release tail — `59a9b08` (test)
2. **Task 1 (GREEN):** scale ADSR envelope to note duration so short notes have a release tail — `ae97f09` (fix)

**Plan/SUMMARY metadata commit:** handled by orchestrator after this run

## Files Created/Modified

- `flow-lang.Tests/Unit/QuickFixes/AdsrShortNoteEnvelopeFacts.cs` (CREATED) — Six `[Fact]` regression tests pinning the short-note ADSR contract:
  1. `ShortNote_HasNonZeroReleaseTail` — final sample is 0; preceding 5 frames form a strictly descending non-zero tail (proves a multi-sample release ramp exists, not a single-sample cliff)
  2. `ShortNote_HasNonZeroAttack` — attack phase still present in first 200 frames
  3. `ShortNote_NoAbruptCliff` — no two adjacent samples differ by more than 0.5 (catches the "missing release" cliff failure mode)
  4. `LongNote_PreservesExactFrameCounts` — 3-second note still has the exact attack/decay/sustain/release frame boundaries it always had
  5. `MediumNote_AttackPlusDecayJustExceedsBuffer_StillHasRelease` — boundary case where pre-fix logic gave 0 release frames; now has a real descending release tail
  6. `ZeroDurationNote_ReturnsAllZeroCurve_NoExceptions` — defensive guard for `totalFrames=0`
- `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs` (MODIFIED, lines 128-191) — Replaced `Math.Min(...)` clamp chain in `GenerateADSRCurve` with proportional scale-to-fit; adjusted release loop body so the final sample lands at exactly 0

## Synth Impact

The fix lives in `EnvelopeProcessor.GenerateADSRCurve`, which is reached by every synth that calls `SynthUtils.GenerateADSR`. The following 7 synths automatically benefit without per-synth changes:

| Synth | ADSR (a/d/s/r ms) | Status |
|-------|-------------------|--------|
| PianoSynthesizer (main) | 3 / 600 / 0.12 / 300 | Fixed (the immediate Chopin-nocturne reproducer) |
| PianoSynthesizer (transient) | 0.3 / 2 / 0 / 0.5 | Fixed but unaffected — ~3 ms total fits even tiny buffers |
| BrassSynthesizer | 120 / 100 / 0.7 / 150 | Fixed |
| SaxSynthesizer (main) | 30 / 60 / 0.75 / 100 | Fixed |
| SaxSynthesizer (breath) | 10 / 80 / 0.15 / 50 | Fixed |
| FluteSynthesizer | 60 / 80 / 0.65 / 120 | Fixed |
| StringsSynthesizer | 100 / 200 / 0.7 / 300 | Fixed |
| OrganSynthesizer | 5 / 10 / 1.0 / 10 | Fixed but rarely triggered — fits in ~1 ms buffers |
| WavetableSynthesizer | 5 / 50 / 0.7 / 50 | Fixed but rarely triggered — fits in ~5 ms buffers |

**Unaffected by design:**

- `DrumSynthesizer` — uses fixed-time envelopes (kick=301 ms, snare=171 ms, etc.) padded/trimmed to durationBeats; the drum's internal ADSR fits its own fixed buffer, so the bug never reproduces here
- `BellSynthesizer` — uses per-partial exponential decay with no `GenerateADSR` call

## Decisions Made

- **Scale-to-fit over clamp** — Proportional shrinking preserves the envelope SHAPE (just compressed in time) instead of producing degenerate phase counts. This matches the way real-world envelope hardware behaves at very short note durations (the "compressed shape" intuition).
- **Leftover floor-rounding goes to release** — Floor-truncating three integer frame counts can leave 1-3 frames unallocated. Adding them to release guarantees `attackFrames + decayFrames + releaseFrames + sustainFrames = totalFrames` exactly, and means the final sample is always inside the release loop (so it lands at 0 cleanly).
- **No backward-compat shim** — Per `memory:project_pre_public_no_legacy_burden`, no flag or opt-out. Short-note ADSR shape changes; that's the point.
- **Did NOT touch BarRenderer's 0.5x staccato multiplier** — Once the envelope scales correctly, staccato sounds right by construction. No need to adjust the multiplier.
- **Did NOT touch the MIDI converter** — Out of scope per pre-execution constraints; investigation already confirmed it's not the cause.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] Release loop body produced non-zero final sample**

- **Found during:** Task 1 (GREEN verification)
- **Issue:** After applying the planned scale-to-fit logic, `MediumNote_AttackPlusDecayJustExceedsBuffer_StillHasRelease` still failed with `Expected: 0 / Actual: 1.358e-05`. Investigation showed the existing release loop body uses `t = (float)i / releaseFrames`, so when `i = releaseFrames - 1` it produces `sustainLevel * (1 - (N-1)/N) = sustainLevel/N` — a tiny but non-zero value. For long notes this is ~9e-6 (inaudible); for short notes after the scale-to-fit it grows to ~1.35e-5 to 1.31e-4. The plan's `must_haves.truths` explicitly says "ends on amplitude 0.0 (no abrupt non-zero cutoff)", and the plan-spec'd test asserts strict `Assert.Equal(0.0f, curve[totalFrames - 1])`.
- **Fix:** Changed the release loop to compute `t = (float)(i + 1) / releaseFrames`. The final sample (`i = releaseFrames - 1`) now writes `sustainLevel * (1 - 1.0) = 0`. The first sample of release writes `sustainLevel * (1 - 1/N)` instead of `sustainLevel * (1 - 0)` — a half-sample shape shift that is acoustically inaudible and represents a strict improvement (the release ramp now actually reaches silence at its endpoint).
- **Files modified:** `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs` (release loop body)
- **Verification:** All 6 new facts pass; full 697-test suite passes (no existing test depended on the old residual-non-zero release endpoint).
- **Committed in:** `ae97f09` (Task 1 GREEN commit, alongside the planned scale-to-fit change)

---

**Total deviations:** 1 auto-fixed (1 bug — Rule 1)
**Impact on plan:** Necessary for the plan's own stated `must_haves.truths` and test contract. Strict improvement: long-note final samples now land at exactly 0 instead of ~9e-6, with no other behavioral change. Reasoning recorded in the source comment.

## Issues Encountered

None — RED → GREEN cycle clean, no blockers.

## Out-of-Scope Findings (Logged, NOT Fixed)

- **GenerateARCurve has the same latent clamp pattern** at line 99 of `EnvelopeProcessor.cs`: `releaseFrames = Math.Min(releaseFrames, totalFrames - attackFrames)`. The fix could be applied symmetrically (scale-to-fit for AR), but no current AR caller in the synth set is known to pass `a + r > totalFrames` for typical note durations, so there's no concrete reproducer. Per scope-boundary rule, NOT fixed in this quick. Recommended follow-up if AR-using synths land in v1.4+.
- **Plan's `done` criterion grep** (`grep -n "Math.Min(releaseFrames" ... returns nothing`) does NOT pass literally because of the AR clamp at line 99. The criterion was clearly intended to confirm the ADSR clamp is gone — and it IS gone (the ADSR `Math.Min(releaseFrames, ...)` is replaced by the scale-to-fit block at lines 137-159). Only the AR clamp remains.

## User Setup Required

None — pure code change, no external configuration.

## Human-Verify Checkpoint (Task 2 — DEFERRED to user)

Per pre-execution constraints, the executor did not perform the audio listening test. The user is asked to verify:

1. **Build is fresh:** `dotnet build` — confirmed PASS by executor (zero new warnings).
2. **Convert the Chopin nocturne MIDI to a Flow score and render** (per plan steps 2-3):
   ```bash
   dotnet run --project flow-midi -- "$HOME/Downloads/midi/Chopin _ Nocturnes Op. 9, No. 2 in Eb Major.mid" /tmp/chopin.flow
   dotnet run --project flow-interpreter /tmp/chopin.flow
   ```
3. **Listen for:**
   - Short staccato runs and grace notes — do they have a perceptible attack-decay-release shape, or do they still sound clipped/clicky?
   - Audible clicks/pops at note boundaries that weren't there before? (The fix should reduce these, not introduce new ones.)
   - Sustained quarter/half/whole notes — do they sound the same as before? (They should — long notes preserve their shape; only the very-final-sample-residue changed from ~9e-6 to 0, which is inaudible.)
4. **Spot-check Brass/Sax** if convenient: `grep -l "brass\|sax" tests/*.flow | head -3` then run those scripts.

**Resume signal:** Reply "approved" if the nocturne sounds better and no regressions on long notes. Otherwise describe what you hear and which synth/passage demonstrates the issue.

## Next Phase Readiness

- Short-note envelope shape is correct across all main ADSR-using synths.
- v1.4 candidate work (per `.planning/seeds/v1.4-candidates.md`) can proceed.
- If symmetric AR scale-to-fit is desired later, the pattern is now established in `GenerateADSRCurve` and trivially portable.

## Self-Check: PASSED

- File `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs` exists at expected path: FOUND
- File `flow-lang.Tests/Unit/QuickFixes/AdsrShortNoteEnvelopeFacts.cs` exists at expected path: FOUND
- Commit `59a9b08` (RED test commit): FOUND in git log
- Commit `ae97f09` (GREEN fix commit): FOUND in git log
- All 697 tests pass: VERIFIED
- `dotnet build` zero errors, zero new warnings: VERIFIED
- Buggy clamp `releaseFrames = Math.Min(releaseFrames, totalFrames - attackFrames - decayFrames)` removed from `GenerateADSRCurve`: VERIFIED (replaced by scale-to-fit block at lines 137-159)

---
*Quick task: 260504-v6j*
*Completed: 2026-05-04*
