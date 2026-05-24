# Piano Samples — License

License: CC-BY 4.0
Source: https://theremin.music.uiowa.edu/MISpiano.html
Attribution: University of Iowa Electronic Music Studios

Files in this directory:
- C2_pp.wav — from Piano.pp.C2 (trimmed to 1.5s, mixed to mono, 16-bit/44.1kHz)
- C2_mf.wav — from Piano.mf.C2 (Phase 37 PIANO-01 — Plan 37-04)
- C2_ff.wav — from Piano.ff.C2
- C3_pp.wav — from Piano.pp.C3
- C3_mf.wav — from Piano.mf.C3 (Phase 37 PIANO-01)
- C3_ff.wav — from Piano.ff.C3
- C4_pp.wav — from Piano.pp.C4
- C4_mf.wav — from Piano.mf.C4 (Phase 37 PIANO-01)
- C4_ff.wav — from Piano.ff.C4
- C5_pp.wav — from Piano.pp.C5
- C5_mf.wav — from Piano.mf.C5 (Phase 37 PIANO-01)
- C5_ff.wav — from Piano.ff.C5
- C6_pp.wav — from Piano.pp.C6
- C6_mf.wav — from Piano.mf.C6 (Phase 37 PIANO-01)
- C6_ff.wav — from Piano.ff.C6

Phase 37 PIANO-01 (Plan 37-04) — synthesized mp layer:
- C{2,3,4,5,6}_mp — NOT on disk. Synthesized at SampleCache eager-load time
  via signed-RMS interpolation between the pp and mf layers at each pitch
  point, with alpha=0.6 (mf-leaning weighting). Per Plan 37-04 D-37-09 lock +
  RESEARCH §Pattern 9 Path 1 (A5). The mp synthesis is deterministic — same
  alpha + same pp/mf source produces byte-identical mp across renders,
  preserving the Phase 28 / 29 / 33 two-run cmp-clean determinism contract.

Original recordings: University of Iowa Musical Instrument Samples (MIS) project.
Conversion: 24-bit stereo AIFF → 16-bit mono WAV via ffmpeg, 2026-05-11.
Phase 37 mf samples added 2026-05-22 (same source, same conversion pipeline).
