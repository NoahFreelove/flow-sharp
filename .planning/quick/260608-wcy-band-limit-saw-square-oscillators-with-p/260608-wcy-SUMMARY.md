---
phase: quick-260608-wcy
plan: 01
subsystem: audio-synthesis
tags: [oscillator, polyblep, band-limiting, anti-aliasing, sound-design, dsp]
requires: []
provides:
  - "PolyBLEP-band-limited Saw + Square oscillators (Desktop + Web targets)"
  - "BlepOscillator.PolyBlep residual helper in NoteSynthesizer.cs"
  - "Regenerated Phase46 saw/square byte-guard oracle (bit-exact, band-limited contract)"
  - "Phase29 saw + square harmonic-richness floor assertions"
  - "Re-pinned Phase41 EDM showcase baseline (band-limited render)"
affects:
  - "Every renderSong/play/writeWav using \"saw\" or \"square\" instruments"
tech-stack:
  added: []        # zero new packages — hand-rolled float math per the no-library DSP convention
  patterns: ["PolyBLEP (Välimäki/Pekonen) 2-sample band-limited step residual"]
key-files:
  created: []
  modified:
    - "flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs"
    - "flow-lang.Tests/Unit/Phase46/NoteSynthesizerByteGuardTests.cs"
    - "flow-lang.Tests/Integration/Phase29/HarmonicRichnessTests.cs"
    - "flow-lang.Tests/baselines/Phase41/showcase.wav"
decisions:
  - "Square harmonic-richness floor documented at 0.15 (its odd-only 2..8 sweep caps near 0.17 by the waveform's nature) plus a band-limited-tracks-naive within-0.02 invariant — NOT the saw's 0.20 (which would be a false claim about a square's spectrum). Verified by measuring the naive pre-band-limiting square = 0.1715 vs band-limited 0.1697."
  - "Phase41 baseline re-pinned even though the RMS-windowed test stays green either way (worst window delta 0.066 dB << 0.5 dB) — keeps the byte artifact current with the band-limited render path."
metrics:
  duration: "~25 min"
  completed: 2026-06-08
  tasks: 3
  files: 4
---

# Phase quick-260608-wcy: PolyBLEP Band-Limit Saw + Square Oscillators Summary

PolyBLEP-band-limited the `saw` and `square` oscillators (v1.6 "Sound Design 2.0", D-37-09 pulled forward) — folded aliasing energy above Nyquist is removed on low notes while every legit sub-Nyquist harmonic survives; sine + triangle left byte-identical; two-run cmp-clean and full deterministic regression preserved.

## What Changed

### Task 1 — Production: PolyBLEP saw + square (`NoteSynthesizer.cs`, commit `7e618b8`)

Added an `internal static class BlepOscillator` with the standard 2-branch PolyBLEP residual:

```csharp
public static double PolyBlep(double t, double dt)
{
    if (t < dt)            { t = t / dt;         return t + t - t * t - 1.0; } // start of period
    else if (t > 1.0 - dt) { t = (t - 1.0) / dt; return t * t + t + t + 1.0; } // end of period
    else                     return 0.0;
}
```

- **`dt = frequency / sampleRate`** is the per-sample residual width. The absolute-time `phase = (frequency * t) % 1.0` formula (the byte-determinism contract) is **preserved verbatim** — only the per-sample *value* gains the BLEP correction.
- **Saw (ONE correction):** the sawtooth has a single +1 reset discontinuity at the 0/1 phase wrap → `value = naive - PolyBlep(phase, dt)` where `naive = 2·phase − 1`. Subtracting the residual rounds the reset over the two samples straddling it.
- **Square (TWO corrections):** the square has a rising +1 edge at the 0/1 wrap and a falling −1 edge at phase 0.5 → `value = naive + PolyBlep(phase, dt) - PolyBlep((phase + 0.5) % 1.0, dt)` where `naive = phase < 0.5 ? 1 : −1`. The falling edge is corrected at the half-period-shifted phase.
- Amplitude scalars unchanged (`0.2 · velocity` for both) — only the spectral *shape* changes, not gross level.
- **SineSynthesizer + TriangleSynthesizer render loops textually unchanged.** Sine is pure; triangle's 1/n² rolloff barely aliases (would need polyBLAMP — out of scope).
- Pure deterministic float math: no RNG, no clock, no incremental phase accumulator.

Both `dotnet build flow-lang -p:FlowTarget=Desktop` and `-p:FlowTarget=Web` exit **0** (oscillators are core, present on both targets).

### Task 2 — Tests: regenerate byte guards + add richness floor (commit `18a0e8d`)

**(a) `NoteSynthesizerByteGuardTests.cs`** — `ExpectedSaw()` and `ExpectedSquare()` regenerated to mirror the PolyBLEP arithmetic element-for-element (added a private `PolyBlep` mirror; saw `naive − blep(phase)`, square `naive + blep(phase) − blep((phase+0.5)%1)`). The guard stays **bit-exact** (`BitConverter.SingleToInt32Bits` compare) — NOT loosened; only the oracle math moved in lockstep with the production loop. `ExpectedSine()` and `ExpectedTriangle()` unchanged. Class doc header updated to record the saw/square regeneration as the expected, intentional band-limiting change; sine/triangle wording kept.

**(b) `HarmonicRichnessTests.cs`** — two new `[Fact]`s, no existing fact touched, no production code touched.

### Task 3 — Baseline + determinism + full sweep (commit `d4a7d7f`)

Re-rendered `examples/edm/pulse.flow` (`renderSong song "saw"`) and re-pinned `flow-lang.Tests/baselines/Phase41/showcase.wav` to the band-limited render. Ran the full suite.

## Reported Results (per task constraints)

### 1. PolyBLEP residual + saw/square wiring
Standard Välimäki/Pekonen 2-sample residual (above). Saw = ONE correction (subtract at the wrap); Square = TWO corrections (+ at rising wrap, − at falling 0.5 edge). `dt = frequency / sampleRate`.

### 2. Measured harmonic-richness ratios (proving the floor holds)

`Phase29Fft.HarmonicRichnessRatio` (Goertzel, 2nd..8th partials, skips ≥ Nyquist), A4 = 440 Hz, 2.0 beat / 120 bpm / 44100, velocity 0.7:

| Synth | Band-limited richness | Naive (pre-BL) richness | Floor asserted | Verdict |
|-------|----------------------:|------------------------:|---------------:|---------|
| **saw** | **0.5232** (0.5253 @ C4, 0.5272 @ A2) | 0.5274 | `≥ 0.20` | clears by ~2.6× |
| **square** | **0.1697** (0.1711 @ C4, 0.1714 @ A2) | 0.1715 | `≥ 0.15` + `|BL − naive| ≤ 0.02` | clears; delta 0.0018 |

**Key verification (not assumed):** a square wave has **odd-only** harmonics — its even partials (2f, 4f, 6f, 8f) are ~0 by the waveform's nature, so the helper's 2nd..8th sweep only captures 3f/5f/7f, capping a square's *measurable* ratio near (1/9 + 1/25 + 1/49) ≈ 0.172 **regardless of band-limiting**. I confirmed the naive pre-band-limiting square measured 0.1715 vs the band-limited 0.1697 — a 0.0018 delta that is aliased fold-back removal, **not** harmonic loss. So the square fact asserts `≥ 0.15` (margin under its intrinsic 0.17 ceiling) AND asserts the band-limited ratio tracks the naive ratio within 0.02 (the *real* invariant: legit sub-Nyquist harmonics preserved). The saw's `≥ 0.20` is genuinely meaningful (clears at 0.52). This was documented down to the measured truth rather than tolerance-loosened to force a pass (see Deviations).

### 3. Two-run determinism SHA
`bash scripts/test_two_run_determinism.sh examples/edm/pulse.flow --render-cmd "dotnet run --project flow-cli -- run <SCRIPT>"` → **PASS (identical SHA-256)**:

```
Run A: 64754dc9ce8531b1695d5ea718fd31a92086b57cefbcb6b9dc856b98908e9369
Run B: 64754dc9ce8531b1695d5ea718fd31a92086b57cefbcb6b9dc856b98908e9369
```

Two consecutive band-limited renders are byte-identical — PolyBLEP is deterministic float math, two-run cmp-clean held.

### 4. Which baselines shifted (before/after + cause)

**Only `flow-lang.Tests/baselines/Phase41/showcase.wav`** was regenerated — it is the only RMS baseline whose render uses `"saw"`/`"square"`.

| | old baseline | new (band-limited) baseline |
|---|---|---|
| SHA-256 | `a2c095c4…` | `64754dc9…` |
| size | 10,874,216 B (2,718,543 frames / 2ch / 44100Hz) | identical dimensions |

**Cause:** the cleaner band-limited saw spectrum (aliased fold-back energy above Nyquist removed). **Honest note on magnitude:** the worst-case 100 ms windowed RMS delta old→new is only **0.066 dB** — well within SPEC-8 ±0.5 dB, so `Phase41ShowcaseRmsTests` stays green *either* baseline. Band-limiting cleans the *spectrum* but barely moves *per-window energy* because a saw's loudness is dominated by its strong, untouched low harmonics. The baseline was re-pinned anyway (per task constraints) so the byte artifact reflects the actual current band-limited render path; the test passes against it.

**No other RMS baseline shifted** (as the planner predicted): Phase28 baselines render `"sine"`, Phase37 `"piano"`, Phase45 `"brass"`/`"flute"` — none touch saw/square. All verified green against their existing baselines (oscillator-relevant + Phase28 set ran 3/3 consecutive 126/126 green). No tolerance loosened anywhere.

### 5. Sine/triangle bytes unchanged
SineSynthesizer + TriangleSynthesizer render loops were not edited (textually verbatim). The Phase46 `ExpectedSine()`/`ExpectedTriangle()` oracles were left frozen and both byte-guard facts pass bit-exact, confirming zero drift.

## Full-Suite Pass/Fail Counts

- **Targeted (oscillator-relevant) suite: deterministically GREEN.** Phase46 byte guard 5/5; Phase29 harmonic richness 8/8 (6 existing + 2 new); Phase41 showcase 1/1; combined oscillator + Phase28 RMS set **126/126, three consecutive runs**.
- **Full `dotnet test`: 2283 passed / 2 failed / 14 skipped** on the parallel run. **Both failures are the documented pre-existing WASM `Console.SetOut` cross-collection redirection race** (`WasmSynchronousExecutionTests.RunFromJs_*`) — they pass **2/2 in isolation**, their captured-stdout failure mode shows another collection's `PASS <run-0>::t4…` output leaking in, and they exercise `(print "hi")` / `createSineTone` (the **sine** path — untouched), never saw/square. This race is recorded in `STATE.md:48` and `.planning/phases/40-studio-sync/deferred-items.md` as out-of-scope, re-confirmed on clean `dev`. Re-running with `-parallel none` flipped the failure set (1 fail: a FlowMidi quantizer / ClockSlave *timing* test instead) — the non-deterministic failure set across runs is the definitive signature of pre-existing parallelism/timing flakes, not a regression from this change.
- Skip count unchanged at **14** (Phase39 MusicXML round-trip charitable-skips when `mscore` absent, etc.).

Per the deviation-rule SCOPE BOUNDARY, the pre-existing flakes are out of scope (not caused by this task's changes); they were diagnosed, attributed, and left as the already-tracked deferred items rather than touched.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug in my own new test] Square harmonic-richness floor encoded a false assumption**
- **Found during:** Task 2 (verify step). The plan's must-have said "saw + square must STILL clear the ≥20% harmonic-richness floor" and the critical invariant said "Verify, don't assume."
- **Issue:** My first cut asserted both saw and square `≥ 0.20`. The square fact went RED at **0.170**. Investigation (probing both the naive and band-limited square at A4/C4/A2) proved this is **not** a band-limiting defect: a square wave has only odd harmonics, so the helper's 2nd..8th-partial sweep caps a square's measurable ratio near 0.172 *by the waveform's nature* — the naive pre-band-limiting square measures 0.1715, the band-limited 0.1697 (a 0.0018 fold-back-removal delta, not harmonic loss). A 0.20 floor for a square would be a **false claim about a square's spectrum**, not a real regression gate.
- **Fix:** Saw keeps the meaningful `≥ 0.20` (clears at 0.52). Square asserts `≥ 0.15` (documented margin under its 0.17 odd-harmonic ceiling) **plus** the real invariant — `|band-limited − naive| ≤ 0.02` (reconstructs the naive square inline and proves band-limiting preserved the in-band harmonics). Documented to the measured truth in code comments; **no tolerance loosened to force a pass** (per critical invariant).
- **Files modified:** `flow-lang.Tests/Integration/Phase29/HarmonicRichnessTests.cs`
- **Commit:** `18a0e8d`

### Observations (no action — out of scope)

- The Phase41 showcase RMS test does not strictly *require* baseline regeneration (worst window delta 0.066 dB < 0.5 dB). Re-pinned per task constraints anyway so the artifact is byte-current. Not a deviation — explicit instruction.
- Full-suite parallel run surfaces the pre-existing WASM Console-redirection race (2 fails) and, under serial, intermittent FlowMidi/ClockSlave timing flakes (1 fail). Both pre-existing, tracked, unrelated to oscillators (SCOPE BOUNDARY — not fixed, already in `deferred-items.md`).

## Self-Check: PASSED

- Production file modified: `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` — FOUND, contains `PolyBlep`, both build targets exit 0.
- Byte-guard test: `flow-lang.Tests/Unit/Phase46/NoteSynthesizerByteGuardTests.cs` — FOUND, contains `PolyBlep`, 5/5 green.
- Richness test: `flow-lang.Tests/Integration/Phase29/HarmonicRichnessTests.cs` — FOUND, contains `Saw_HarmonicRichness_ClearsFloor_AfterBandLimiting` + `Square_…`, 8/8 green.
- Baseline: `flow-lang.Tests/baselines/Phase41/showcase.wav` — FOUND, SHA `64754dc9…`, Phase41 test 1/1 green.
- Commits FOUND: `7e618b8` (feat), `18a0e8d` (test), `d4a7d7f` (baseline).
- Two-run determinism: byte-identical (`64754dc9…` ×2).
- Sine/triangle: untouched, byte-guard green.
