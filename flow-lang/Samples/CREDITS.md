# Sample Library — Credits

Flow's built-in instrument samples are sourced from the **University of Iowa
Electronic Music Studios — Musical Instrument Samples (MIS) project** under
the CC-BY 4.0 license.

All 21 samples in `flow-lang/Samples/` derive from the MIS project:

| Instrument | Files | Source page |
| --- | --- | --- |
| Piano | 10 (C2/C3/C4/C5/C6 × pp/ff) | https://theremin.music.uiowa.edu/MISpiano.html |
| Brass (trumpet) | 3 (A3/A4/A5) | https://theremin.music.uiowa.edu/MIS-Pitches-2012/MISBbTrumpet2012.html |
| Sax (Eb alto) | 2 (F4/C5) | https://theremin.music.uiowa.edu/MIS-Pitches-2012/MISEbAltoSaxophone2012.html |
| Strings (violin + viola) | 3 (D3/D4/D5) | https://theremin.music.uiowa.edu/MIS-Pitches-2012/MISViolin2012.html + https://theremin.music.uiowa.edu/MIS-Pitches-2012/MISViola2012.html |
| Flute | 2 (G4/G5) | https://theremin.music.uiowa.edu/MIS-Pitches-2012/MISFlute2012.html |
| Bell (plastic) | 1 (C5) | https://theremin.music.uiowa.edu/MIS-Pitches-2012/MISBells2012.html |

**Attribution required**: any redistribution of Flow that includes the built-in
sample bundle MUST credit the University of Iowa Electronic Music Studios.

Conversion: original 24-bit stereo AIFF files were converted to 16-bit mono WAV
at 44.1 kHz and trimmed to 1.5 s (or 2.0 s for sustained brass/strings) via
ffmpeg on 2026-05-11. No further processing (no EQ, no reverb, no normalization)
was applied — the SampledInstrumentRenderer in flow-lang applies all per-note
shaping (Phase 28 articulation envelopes + varispeed pitch shift) at render time.

Per-instrument LICENSE.md files in each subdirectory provide more detail.
