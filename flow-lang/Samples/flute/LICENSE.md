# Flute Samples — License

License: CC-BY 4.0
Source: https://theremin.music.uiowa.edu/MIS-Pitches-2012/MISFlute2012.html
Attribution: University of Iowa Electronic Music Studios

Files in this directory:
- G4.wav — from Flute.vib.ff.G4 (trimmed to 1.5s, mixed to mono, 16-bit/44.1kHz)
- A4.wav — from Flute.vib.ff.A4 (trimmed to 1.5s, mixed to mono, 16-bit/44.1kHz)
- G5.wav — from Flute.vib.ff.G5

Instrument: Concert flute, vibrato, fortissimo. The SampledInstrumentRenderer
varispeed-shifts G4 / A4 / G5 to cover the full flute range. A4 (Phase 37
FLUTE-01, Plan 37-05) closes the D5 timbre crossover gap per RESEARCH §Pattern 10
— a flute note at D5 (MIDI 74) now varispeed-shifts from A4 (5 semitones away)
instead of G4 (7 semitones), reducing audible varispeed timbre distortion in the
flute's most expressive low-to-mid register.

Original recordings: University of Iowa Musical Instrument Samples (MIS) project.
Conversion: 24-bit stereo AIFF → 16-bit mono WAV via ffmpeg, 2026-05-11 (G4, G5),
2026-05-23 (A4).
