# Roadmap: Flow Language

## Milestones

- ~~**v1.0 MVP**~~ - Phases 1-5 (shipped 2026-04-03)
- **v1.1 Polish & Foundations** - Phases 6-10 (in progress)

## Phases

<details>
<summary>v1.0 MVP (Phases 1-5) - SHIPPED 2026-04-03</summary>

- [x] **Phase 1: Language Foundations** - Add loops, string interpolation, iteration guards, and sequence visualization (completed 2026-04-01)
- [x] **Phase 2: Audio Pipeline** - Add sample loading, stereo panning, sidechain compression, and polyphonic voice allocation (completed 2026-04-02)
- [x] **Phase 3: Synthesis & MIDI Export** - Add custom oscillator definitions and MIDI file export (completed 2026-04-02)
- [x] **Phase 4: Composition Tools** - Add chord progression DSL, polyrhythm support, and probabilistic pattern variation (completed 2026-04-02)
- [x] **Phase 5: Live Coding** - Add beat-synced live reload with playback state preservation (completed 2026-04-03)

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
- [x] 01-02-PLAN.md -- Add string interpolation with $"...{expr}..." syntax
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
**Plans**: 2 plans

Plans:
- [x] 04-01-PLAN.md -- Chord progression DSL with voice leading (progression keyword, parser, ProgressionCompiler)
- [x] 04-02-PLAN.md -- Polyrhythm layering and probabilistic pattern variation (polyrhythm, vary built-ins)

### Phase 5: Live Coding
**Goal**: Users can edit Flow scripts during playback and hear changes take effect at musically appropriate moments without interruption
**Depends on**: Phase 4
**Requirements**: LIVE-01, LIVE-02
**Success Criteria** (what must be TRUE):
  1. User can edit a .flow file while `--watch` playback is running and hear the new version start at the next bar boundary (not mid-bar)
  2. Playback continues seamlessly across reloads -- no audible gap, click, or restart from the beginning
  3. If the edited file has a syntax error, playback continues with the previous valid version and the error is displayed in the terminal
**Plans**: 2 plans

Plans:
- [x] 05-01-PLAN.md -- Streaming playback infrastructure, capture mode, and LiveReloadManager
- [x] 05-02-PLAN.md -- Wire LiveReloadManager into Program.cs and end-to-end verification

</details>

### v1.1 Polish & Foundations (In Progress)

**Milestone Goal:** Fix critical bugs that break user scripts, improve developer experience with missing language features, then expand music production capabilities.

- [ ] **Phase 6: Diagnostics & Bug Fixes** - Add verbose logging, fix Sequence overloads, section capture, error masking, and static manager isolation
- [ ] **Phase 7: Developer Experience** - Add line comments, math stdlib, writeWav rename, and REPL auto-imports
- [ ] **Phase 8: Audio Production** - Add mix function, synth presets (strings, organ, bell), and per-section gain
- [ ] **Phase 9: Advanced Features** - Add tempo ramp transform and interactive tutorial
- [x] **Phase 10: Vocalization** - Add formant vocal synthesis and external TTS hook (completed 2026-04-04)

## Phase Details

### Phase 6: Diagnostics & Bug Fixes
**Goal**: The interpreter correctly handles transforms on sequences, captures bare expressions in sections, reports errors honestly, and provides diagnostic output for debugging
**Depends on**: Phase 5
**Requirements**: QOL-01, FIX-01, FIX-02, FIX-03, FIX-04
**Success Criteria** (what must be TRUE):
  1. User can run `flow --verbose script.flow` and see registered functions, loaded modules, and type resolution details in the terminal output
  2. User can call `transpose(sequence, 2)` and `vary(sequence, 0.5)` on a Sequence value and get a correctly transposed/varied result (no overload resolution failure)
  3. User can write a bare note stream inside a `section` block (without assigning to a variable) and hear it rendered as audio, not silence
  4. When a function is not found, the user sees a clear "function not found" error instead of the program silently continuing or showing a misleading success message
  5. Watch mode reloads do not cause audio playback to break due to static manager state being overwritten by background engine instances
**Plans**: 2 plans

Plans:
- [x] 06-01-PLAN.md -- Add --verbose diagnostic flag and fix static manager isolation in watch mode
- [x] 06-02-PLAN.md -- Fix Sequence overload resolution, section bare expressions, and error masking

### Phase 7: Developer Experience
**Goal**: Users can write cleaner, more expressive Flow scripts with comments, math functions, consistent naming, and a frictionless REPL
**Depends on**: Phase 6
**Requirements**: DX-01, DX-02, DX-03, DX-04
**Success Criteria** (what must be TRUE):
  1. User can add `// this is a comment` on any line and it is ignored by the interpreter (does not cause parse errors)
  2. User can call `sin(1.0)`, `cos(0.0)`, `abs(-5)`, `sqrt(16.0)`, `min(3, 7)`, `max(3, 7)`, `floor(3.7)`, `ceil(3.2)`, and reference `pi` and `tau` constants
  3. User can call `writeWav("out.wav", buffer)` as the primary export function, and `exportWav` still works as a backwards-compatible alias
  4. User can start the REPL and immediately call `print`, `play`, `map`, and other stdlib/audio/collections functions without any `use` statements
**Plans**: 2 plans

Plans:
- [x] 07-01-PLAN.md -- Add // line comments and math standard library (sin, cos, abs, sqrt, etc.)
- [x] 07-02-PLAN.md -- Add writeWav as primary export name and REPL auto-imports

### Phase 8: Audio Production
**Goal**: Users can layer audio buffers, use new instrument timbres, and control per-section volume in song arrangements
**Depends on**: Phase 7
**Requirements**: AUDIO-05, AUDIO-06, AUDIO-07
**Success Criteria** (what must be TRUE):
  1. User can call `mix(buffer1, buffer2)` to layer two audio buffers and hear the combined result
  2. User can render a Song with sections that have individual gain levels (e.g., a quiet intro and a loud chorus) and hear the volume differences
  3. User can set instrument to "strings", "organ", or "bell" in a voice/section and hear a distinct, musically appropriate timbre for each
**Plans**: 2 plans

Plans:
- [x] 08-01-PLAN.md -- Add mix(Buffer, Buffer) function and gain musical context block
- [x] 08-02-PLAN.md -- Add strings, organ, and bell synthesizer presets

### Phase 9: Advanced Features
**Goal**: Users can create gradual tempo transitions and learn the language through a guided tutorial
**Depends on**: Phase 8
**Requirements**: AUDIO-08, QOL-02
**Success Criteria** (what must be TRUE):
  1. User can call `tempoRamp(sequence, 120, 80)` and hear a gradual deceleration from 120 BPM to 80 BPM across the rendered buffer (not an abrupt change)
  2. User can run the interactive tutorial script and be guided from basic expressions through note streams, sections, and full song creation with explanations at each step
**Plans**: 2 plans

Plans:
- [x] 09-01-PLAN.md -- Implement tempoRamp built-in for gradual BPM transitions
- [x] 09-02-PLAN.md -- Create interactive tutorial script teaching Flow from basics to full songs

### Phase 10: Vocalization
**Goal**: Users can add formant-based vocal synthesis and external TTS integration to compositions, producing standard AudioBuffers mixable with instrumental tracks
**Depends on**: Phase 8
**Requirements**: VOC-01, VOC-02
**Success Criteria** (what must be TRUE):
  1. User can call `sing("ah", C4, 2.0)` and get a recognizable formant-synthesized vowel as an AudioBuffer
  2. User can call `sing("na", C4, 1.0)` and get a syllable with consonant onset + vowel
  3. User can call `tts("hello")` and get an AudioBuffer from an external TTS engine (or a clear error if not installed)
  4. Vocal output can be mixed with instrumental tracks using `mix()`
**Plans**: 2 plans

Plans:
- [x] 10-01-PLAN.md -- Formant synthesis engine (FormantData, FormantSynthesizer, ConsonantSynthesizer)
- [x] 10-02-PLAN.md -- TTS hook, VocalizationFunctions registration, audio.flow declarations, and integration test

## Progress

**Execution Order:**
Phases execute in numeric order: 6 -> 7 -> 8 -> 9 -> 10

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Language Foundations | v1.0 | 3/3 | Complete | 2026-04-01 |
| 2. Audio Pipeline | v1.0 | 3/3 | Complete | 2026-04-02 |
| 3. Synthesis & MIDI Export | v1.0 | 2/2 | Complete | 2026-04-02 |
| 4. Composition Tools | v1.0 | 2/2 | Complete | 2026-04-02 |
| 5. Live Coding | v1.0 | 2/2 | Complete | 2026-04-03 |
| 6. Diagnostics & Bug Fixes | v1.1 | 0/2 | Planning complete | - |
| 7. Developer Experience | v1.1 | 0/2 | Planning complete | - |
| 8. Audio Production | v1.1 | 0/2 | Planning complete | - |
| 9. Advanced Features | v1.1 | 0/2 | Planning complete | - |
| 10. Vocalization | v1.1 | 2/2 | Complete    | 2026-04-04 |
