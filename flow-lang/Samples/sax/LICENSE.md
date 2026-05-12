# Sax Samples — License

License: CC-BY 4.0
Source: https://theremin.music.uiowa.edu/MIS-Pitches-2012/MISEbAltoSaxophone2012.html
Attribution: University of Iowa Electronic Music Studios

Files in this directory:
- F4.wav — from AltoSax.NoVib.ff.F4 (trimmed to 1.5s, mixed to mono, 16-bit/44.1kHz)
- C5.wav — from AltoSax.NoVib.ff.C5

Instrument: Eb alto saxophone. Iowa labels these by written pitch; the file naming
in this directory (F4 / C5) is the SLOT label used by the SampledInstrumentRenderer.
The renderer treats whatever the file contains as the labeled concert pitch; if the
underlying sound is at a different concert pitch due to the sax transposition,
varispeed math compensates transparently.

Original recordings: University of Iowa Musical Instrument Samples (MIS) project.
Conversion: 24-bit stereo AIFF → 16-bit mono WAV via ffmpeg, 2026-05-11.
