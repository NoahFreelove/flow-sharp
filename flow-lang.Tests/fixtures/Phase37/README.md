# Phase 37 Test Fixtures

Plan 37-01 Wave 0 directory marker. Plan 37-02 Task 1 generates the synthetic
WAV fixtures listed below programmatically at first test run (per the Phase 33
fixture-generator precedent at `flow-lang.Tests/fixtures/sfz-smoke/`).

Planned fixtures:

- `sine_440.wav` — 5-second sustained 440 Hz sine, 44.1 kHz / 16-bit mono.
  Vocoder smoke fixture (DSP-02 #vocoder mode): pure stationary harmonic
  content exercises Laroche-Dolson identity phase-locking without transient
  noise interference.

- `kick_hit.wav` — 200 ms transient kick-drum body, 44.1 kHz / 16-bit mono.
  PSOLA smoke fixture (DSP-02 #psola mode): exercises the transient-preserving
  path. Onset position must drift ≤ 5 ms after a 2× time-stretch per
  StretchPsolaTransientTests.

- `mixed.wav` — 5-second mixed sine + kick material, 44.1 kHz / 16-bit mono.
  HPS smoke fixture (DSP-02 #auto mode): drives Fitzgerald median-filter
  separator's per-frame harmonic-vs-percussive decision; the StretchAuto
  advisory must report a non-trivial split (neither 0% nor 100%).
