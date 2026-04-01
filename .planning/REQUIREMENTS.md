# Requirements: Flow Language

**Defined:** 2026-04-01
**Core Value:** Users can write musical ideas as code and hear them immediately — the language must faithfully translate musical notation into correct, playable audio.

## v1 Requirements

Requirements for this milestone. Each maps to roadmap phases.

### Language Completeness

- [ ] **LANG-01**: User can iterate over collections with `for` loop construct
- [ ] **LANG-02**: User can write conditional loops with `while` construct
- [ ] **LANG-03**: User can use string interpolation to embed expressions in strings
- [ ] **LANG-04**: User can add iteration guards (max iterations) to prevent infinite loops in REPL

### Audio Pipeline

- [ ] **AUDIO-01**: User can load WAV files as audio buffers via `loadWav` function
- [ ] **AUDIO-02**: User can control stereo panning per voice/buffer with `pan` function
- [ ] **AUDIO-03**: User can apply sidechain compression driven by a trigger buffer
- [ ] **AUDIO-04**: User can allocate polyphonic voices with configurable voice limits and stealing

### Synthesis

- [ ] **SYNTH-01**: User can define custom oscillator waveforms via Flow procs (wavetable approach)
- [ ] **SYNTH-02**: Custom oscillators integrate with existing instrument/voice pipeline

### Composition

- [ ] **COMP-01**: User can write chord progressions with a DSL that auto-generates voicings
- [ ] **COMP-02**: Chord DSL resolves voice leading (minimal movement between chords)
- [ ] **COMP-03**: User can write polyrhythmic patterns with overlapping time signatures
- [ ] **COMP-04**: User can generate probabilistic pattern variations from a source sequence

### MIDI

- [ ] **MIDI-01**: User can export a Song/Sequence to a standard MIDI file via `writeMidi`
- [ ] **MIDI-02**: MIDI export preserves tempo, time signature, key, and note velocities

### Visualization

- [ ] **VIS-01**: User can visualize sequences as piano-roll ASCII art in the terminal

### Live Coding

- [ ] **LIVE-01**: Watch mode reloads code at bar boundaries (beat-synced) during playback
- [ ] **LIVE-02**: Live reload preserves playback state (does not restart from beginning)

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Extended Audio

- **EXT-01**: User can load and play audio samples in formats beyond WAV (FLAC, OGG)
- **EXT-02**: User can apply per-voice effects chains (not just global)
- **EXT-03**: Real-time MIDI output to external devices/DAWs

### Extended Language

- **EXTL-01**: Type inference for `var` declarations
- **EXTL-02**: Pattern matching / switch expressions
- **EXTL-03**: User-defined types / structs

### Platform

- **PLAT-01**: Cross-platform audio backend (Windows WASAPI, macOS CoreAudio)
- **PLAT-02**: LSP (Language Server Protocol) for IDE integration

## Out of Scope

| Feature | Reason |
|---------|--------|
| GUI/DAW interface | Flow is text-first; visual editing is a separate project |
| VST/AU plugin hosting | Too complex for interpreter; focus on built-in synthesis |
| Multi-user collaboration | Single-user tool |
| Cloud/web deployment | Desktop CLI tool |
| JIT compilation | Interpreter-based by design; wavetable approach avoids perf need |
| Video/visual generation | Audio-only domain |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| LANG-01 | Phase 1 | Pending |
| LANG-02 | Phase 1 | Pending |
| LANG-03 | Phase 1 | Pending |
| LANG-04 | Phase 1 | Pending |
| VIS-01 | Phase 1 | Pending |
| AUDIO-01 | Phase 2 | Pending |
| AUDIO-02 | Phase 2 | Pending |
| AUDIO-03 | Phase 2 | Pending |
| AUDIO-04 | Phase 2 | Pending |
| SYNTH-01 | Phase 3 | Pending |
| SYNTH-02 | Phase 3 | Pending |
| MIDI-01 | Phase 3 | Pending |
| MIDI-02 | Phase 3 | Pending |
| COMP-01 | Phase 4 | Pending |
| COMP-02 | Phase 4 | Pending |
| COMP-03 | Phase 4 | Pending |
| COMP-04 | Phase 4 | Pending |
| LIVE-01 | Phase 5 | Pending |
| LIVE-02 | Phase 5 | Pending |

**Coverage:**
- v1 requirements: 19 total
- Mapped to phases: 19
- Unmapped: 0

---
*Requirements defined: 2026-04-01*
*Last updated: 2026-03-29 after roadmap creation*
