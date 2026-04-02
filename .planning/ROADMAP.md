# Roadmap: Flow Language

## Overview

This milestone takes Flow from a capable music notation language to a full-featured music production tool. We start by filling language gaps (loops, string interpolation) that unblock iteration patterns, then expand the audio pipeline with samples, panning, and polyphony. Next we add custom synthesis and MIDI export, followed by advanced composition features (chord DSL, polyrhythm, probabilistic patterns). The riskiest feature -- beat-synced live reload -- goes last when the foundation is solid.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Language Foundations** - Add loops, string interpolation, iteration guards, and sequence visualization
- [x] **Phase 2: Audio Pipeline** - Add sample loading, stereo panning, sidechain compression, and polyphonic voice allocation (completed 2026-04-02)
- [ ] **Phase 3: Synthesis & MIDI Export** - Add custom oscillator definitions and MIDI file export
- [ ] **Phase 4: Composition Tools** - Add chord progression DSL, polyrhythm support, and probabilistic pattern variation
- [ ] **Phase 5: Live Coding** - Add beat-synced live reload with playback state preservation

## Phase Details

### Phase 1: Language Foundations
**Goal**: Users can write iterative, debuggable Flow scripts with loop constructs, formatted output, and visual feedback on their sequences
**Depends on**: Nothing (first phase)
**Requirements**: LANG-01, LANG-02, LANG-03, LANG-04, VIS-01
**Success Criteria** (what must be TRUE):
  1. User can iterate over a list with `for` and accumulate results (e.g., build a sequence by looping over note names)
  2. User can write a `while` loop that terminates on a condition, and the REPL halts a runaway loop after hitting the iteration guard
  3. User can embed expressions in strings (e.g., `"tempo is {bpm}"`) and see interpolated output from `print`
  4. User can pipe a Sequence to a visualization function and see a piano-roll ASCII grid in the terminal showing pitch vs. time
**Plans**: 3 plans

Plans:
- [x] 01-01-PLAN.md -- Add for/while loops, break/continue, and iteration guards
- [ ] 01-02-PLAN.md -- Add string interpolation with $"...{expr}..." syntax
- [x] 01-03-PLAN.md -- Add ASCII piano-roll sequence visualization

### Phase 2: Audio Pipeline
**Goal**: Users can load audio samples, position sounds in the stereo field, apply sidechain compression, and play polyphonic arrangements without voice clipping
**Depends on**: Phase 1
**Requirements**: AUDIO-01, AUDIO-02, AUDIO-03, AUDIO-04
**Success Criteria** (what must be TRUE):
  1. User can call `loadWav("kick.wav")` and use the returned buffer in compositions (mix, effects, playback)
  2. User can call `pan(buffer, -1.0)` through `pan(buffer, 1.0)` to position a voice left-to-right in stereo output
  3. User can apply sidechain compression to a bass buffer triggered by a kick buffer, producing the characteristic pumping effect
  4. User can render a Song with 8+ simultaneous notes and hear clean polyphony with configurable voice limits and voice stealing
**Plans**: 3 plans

Plans:
- [x] 02-01-PLAN.md -- WAV file loading (loadWav) and sidechain compression
- [x] 02-02-PLAN.md -- Stereo panning (pan function, Voice.Pan bug fix, pan context block)
- [x] 02-03-PLAN.md -- Polyphonic voice allocation with configurable limits and stealing

### Phase 3: Synthesis & MIDI Export
**Goal**: Users can define their own oscillator waveforms in Flow code and export compositions as standard MIDI files
**Depends on**: Phase 2
**Requirements**: SYNTH-01, SYNTH-02, MIDI-01, MIDI-02
**Success Criteria** (what must be TRUE):
  1. User can define a custom oscillator via a Flow proc (wavetable approach) and use it as an instrument in Song rendering
  2. Custom oscillators work with the existing voice allocation and effects pipeline (no special-casing required by the user)
  3. User can call `writeMidi("output.mid", song)` and open the resulting file in any DAW or MIDI player
  4. Exported MIDI files contain correct tempo, time signature, key signature, and per-note velocities matching the Flow source
**Plans**: 2 plans

Plans:
- [x] 03-01-PLAN.md -- Custom oscillator definitions (WavetableSynthesizer + oscillator() built-in)
- [x] 03-02-PLAN.md -- MIDI file export (DryWetMidi + writeMidi built-in)

### Phase 4: Composition Tools
**Goal**: Users can write chord progressions with automatic voicing, layer polyrhythmic patterns, and generate probabilistic variations of sequences
**Depends on**: Phase 3
**Requirements**: COMP-01, COMP-02, COMP-03, COMP-04
**Success Criteria** (what must be TRUE):
  1. User can write a chord progression using a DSL (e.g., `progression | I IV vi V |`) and get auto-generated voicings as playable sequences
  2. Adjacent chords in a progression use voice leading (notes move by minimal intervals rather than jumping between octaves)
  3. User can overlay two sequences with different time signatures (e.g., 3/4 over 4/4) and hear them cycle correctly against each other
  4. User can generate variations of a pattern where notes are probabilistically altered, producing musically related but non-identical sequences each time
**Plans**: TBD

Plans:
- [ ] 04-01: TBD
- [ ] 04-02: TBD
- [ ] 04-03: TBD

### Phase 5: Live Coding
**Goal**: Users can edit Flow scripts during playback and hear changes take effect at musically appropriate moments without interruption
**Depends on**: Phase 4
**Requirements**: LIVE-01, LIVE-02
**Success Criteria** (what must be TRUE):
  1. User can edit a .flow file while `--watch` playback is running and hear the new version start at the next bar boundary (not mid-bar)
  2. Playback continues seamlessly across reloads -- no audible gap, click, or restart from the beginning
  3. If the edited file has a syntax error, playback continues with the previous valid version and the error is displayed in the terminal
**Plans**: TBD

Plans:
- [ ] 05-01: TBD
- [ ] 05-02: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3 -> 4 -> 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Language Foundations | 0/3 | Not started | - |
| 2. Audio Pipeline | 3/3 | Complete   | 2026-04-02 |
| 3. Synthesis & MIDI Export | 0/2 | Not started | - |
| 4. Composition Tools | 0/3 | Not started | - |
| 5. Live Coding | 0/2 | Not started | - |
