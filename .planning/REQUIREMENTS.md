# Requirements: Flow Language

**Defined:** 2026-04-01
**Core Value:** Users can write musical ideas as code and hear them immediately — the language must faithfully translate musical notation into correct, playable audio.

## v1 Requirements

Requirements for this milestone. Each maps to roadmap phases.

### Language Completeness

- [x] **LANG-01**: User can iterate over collections with `for` loop construct
- [x] **LANG-02**: User can write conditional loops with `while` construct
- [x] **LANG-03**: User can use string interpolation to embed expressions in strings
- [x] **LANG-04**: User can add iteration guards (max iterations) to prevent infinite loops in REPL

### Audio Pipeline

- [x] **AUDIO-01**: User can load WAV files as audio buffers via `loadWav` function
- [x] **AUDIO-02**: User can control stereo panning per voice/buffer with `pan` function
- [x] **AUDIO-03**: User can apply sidechain compression driven by a trigger buffer
- [x] **AUDIO-04**: User can allocate polyphonic voices with configurable voice limits and stealing

### Synthesis

- [x] **SYNTH-01**: User can define custom oscillator waveforms via Flow procs (wavetable approach)
- [x] **SYNTH-02**: Custom oscillators integrate with existing instrument/voice pipeline

### Composition

- [x] **COMP-01**: User can write chord progressions with a DSL that auto-generates voicings
- [x] **COMP-02**: Chord DSL resolves voice leading (minimal movement between chords)
- [x] **COMP-03**: User can write polyrhythmic patterns with overlapping time signatures
- [x] **COMP-04**: User can generate probabilistic pattern variations from a source sequence

### MIDI

- [x] **MIDI-01**: User can export a Song/Sequence to a standard MIDI file via `writeMidi`
- [x] **MIDI-02**: MIDI export preserves tempo, time signature, key, and note velocities

### Visualization

- [x] **VIS-01**: User can visualize sequences as piano-roll ASCII art in the terminal

### Live Coding

- [x] **LIVE-01**: Watch mode reloads code at bar boundaries (beat-synced) during playback
- [x] **LIVE-02**: Live reload preserves playback state (does not restart from beginning)

## v1.1 Requirements

Requirements for milestone v1.1: Polish & Foundations.

### Bug Fixes

- [ ] **FIX-01**: Sequence type resolves correctly in overload matching (transpose, vary, and all transforms work with Sequence arguments)
- [ ] **FIX-02**: Bare expressions inside sections are captured as anonymous sequences (no silent 0-frame renders)
- [ ] **FIX-03**: Error reporter distinguishes fatal vs non-fatal errors and does not mask function-not-found failures as success
- [ ] **FIX-04**: Background FlowEngine instances do not clobber static PlaybackFunctions manager (proper isolation)

### Developer Experience

- [ ] **DX-01**: Lexer supports `//` line comments (skipped like whitespace)
- [ ] **DX-02**: Math standard library: sin, cos, abs, sqrt, min, max, floor, ceil, pi, tau
- [x] **DX-03**: `writeWav` function added as primary name, `exportWav` kept as alias for backwards compatibility
- [x] **DX-04**: REPL mode auto-imports @std, @audio, @collections without explicit use statements

### Audio Production

- [ ] **AUDIO-05**: `mix(buffer1, buffer2)` layers two audio buffers by summing samples
- [ ] **AUDIO-06**: Per-section gain control in song rendering
- [x] **AUDIO-07**: Three new synth presets: strings (detuned saws), organ (Hammond additive), bell (Risset inharmonic partials)
- [x] **AUDIO-08**: Tempo ramp transform: `tempoRamp(sequence, startBPM, endBPM) -> Buffer` for gradual tempo changes

### Quality of Life

- [ ] **QOL-01**: `--verbose` flag shows registered functions, loaded modules, and type resolution details
- [ ] **QOL-02**: Interactive tutorial — guided .flow script teaching the language from basics to full songs

### Vocalization

- [x] **VOC-01**: User can call `sing(phoneme, note, duration)` and get a formant-synthesized vocal AudioBuffer for 5 vowels (ah, ee, eh, oh, oo) and 3 consonant syllables (na, ta, sa)
- [x] **VOC-02**: User can call `tts(text)` to generate speech audio via an external TTS command, and `setTtsCommand(cmd)` to configure the TTS engine

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

### v1.0 (Shipped)

| Requirement | Phase | Status |
|-------------|-------|--------|
| LANG-01 | Phase 1 | Complete |
| LANG-02 | Phase 1 | Complete |
| LANG-03 | Phase 1 | Complete |
| LANG-04 | Phase 1 | Complete |
| VIS-01 | Phase 1 | Complete |
| AUDIO-01 | Phase 2 | Complete |
| AUDIO-02 | Phase 2 | Complete |
| AUDIO-03 | Phase 2 | Complete |
| AUDIO-04 | Phase 2 | Complete |
| SYNTH-01 | Phase 3 | Complete |
| SYNTH-02 | Phase 3 | Complete |
| MIDI-01 | Phase 3 | Complete |
| MIDI-02 | Phase 3 | Complete |
| COMP-01 | Phase 4 | Complete |
| COMP-02 | Phase 4 | Complete |
| COMP-03 | Phase 4 | Complete |
| COMP-04 | Phase 4 | Complete |
| LIVE-01 | Phase 5 | Complete |
| LIVE-02 | Phase 5 | Complete |

### v1.1 (In Progress)

| Requirement | Phase | Status |
|-------------|-------|--------|
| QOL-01 | Phase 6 | Pending |
| FIX-01 | Phase 6 | Pending |
| FIX-02 | Phase 6 | Pending |
| FIX-03 | Phase 6 | Pending |
| FIX-04 | Phase 6 | Pending |
| DX-01 | Phase 7 | Pending |
| DX-02 | Phase 7 | Pending |
| DX-03 | Phase 7 | Complete |
| DX-04 | Phase 7 | Complete |
| AUDIO-05 | Phase 8 | Pending |
| AUDIO-06 | Phase 8 | Pending |
| AUDIO-07 | Phase 8 | Complete |
| AUDIO-08 | Phase 9 | Complete |
| QOL-02 | Phase 9 | Pending |
| VOC-01 | Phase 10 | Complete |
| VOC-02 | Phase 10 | Complete |

**Coverage:**
- v1.0 requirements: 19 total, 19 mapped, 0 unmapped
- v1.1 requirements: 16 total, 16 mapped, 0 unmapped

---
*Requirements defined: 2026-04-01*
*Last updated: 2026-04-03 -- VOC-01, VOC-02 added for Phase 10*
