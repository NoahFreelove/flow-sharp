# Phase 37 RMS-Windowed Regression Baselines

Plan 37-01 Wave 0 directory marker. Baselines materialize from later plans:

- Plan 37-03 (MIX-01 / D-37-03 / D-37-15) — pins the already-shipped
  synth-path per-voice constant-power pan formula against future regression.
  `mix_synth_path_pan.wav` and related.

- Plan 37-04 (PIANO-01 / D-37-12) — locks the ragtime warmth baseline after
  composer UAT iteration #2 sign-off. `ragtime_warmth.wav` and related.

- Plan 37-07 closer — top-level Phase 37 phase-gate baselines covering the
  combined surface (granular + stretch + pitchShift + SFZ retrofit + sample
  expansion).

Format: 44.1 kHz / 16-bit stereo WAV (matches `flow-lang.Tests/baselines/Phase28/`
precedent). Tolerance per SPEC-8: ±0.5 dB / 100 ms windows via
`flow-lang.Tests/Helpers/RmsRegressionTests.AssertRmsWithinTolerance` (or the
file-path overload `AssertWavMatchesBaseline`).
