# Roadmap: Flow Language

## Milestones

- ~~**v1.0 MVP**~~ — Phases 1-5 (shipped 2026-04-03)
- ✅ **v1.1 Polish & Foundations** — Phases 6-10 (shipped 2026-04-18) — see `milestones/v1.1-ROADMAP.md`
- 📋 **v1.2** — to be planned via `/gsd-new-milestone`

## Phases

<details>
<summary>v1.0 MVP (Phases 1-5) — SHIPPED 2026-04-03</summary>

- [x] **Phase 1: Language Foundations** — Add loops, string interpolation, iteration guards, and sequence visualization (completed 2026-04-01)
- [x] **Phase 2: Audio Pipeline** — Add sample loading, stereo panning, sidechain compression, and polyphonic voice allocation (completed 2026-04-02)
- [x] **Phase 3: Synthesis & MIDI Export** — Add custom oscillator definitions and MIDI file export (completed 2026-04-02)
- [x] **Phase 4: Composition Tools** — Add chord progression DSL, polyrhythm support, and probabilistic pattern variation (completed 2026-04-02)
- [x] **Phase 5: Live Coding** — Add beat-synced live reload with playback state preservation (completed 2026-04-03)

### Phase 1: Language Foundations
**Goal**: Users can write iterative, debuggable Flow scripts with loop constructs, formatted output, and visual feedback on their sequences
**Depends on**: Nothing (first phase)
**Requirements**: LANG-01, LANG-02, LANG-03, LANG-04, VIS-01
**Plans**: 3 plans

Plans:
- [x] 01-01-PLAN.md -- Add for/while loops, break/continue, and iteration guards
- [x] 01-02-PLAN.md -- Add string interpolation with $"...{expr}..." syntax
- [x] 01-03-PLAN.md -- Add ASCII piano-roll sequence visualization

### Phase 2: Audio Pipeline
**Goal**: Users can load audio samples, position sounds in the stereo field, apply sidechain compression, and play polyphonic arrangements without voice clipping
**Depends on**: Phase 1
**Requirements**: AUDIO-01, AUDIO-02, AUDIO-03, AUDIO-04
**Plans**: 3 plans

Plans:
- [x] 02-01-PLAN.md -- WAV file loading (loadWav) and sidechain compression
- [x] 02-02-PLAN.md -- Stereo panning (pan function, Voice.Pan bug fix, pan context block)
- [x] 02-03-PLAN.md -- Polyphonic voice allocation with configurable limits and stealing

### Phase 3: Synthesis & MIDI Export
**Goal**: Users can define their own oscillator waveforms in Flow code and export compositions as standard MIDI files
**Depends on**: Phase 2
**Requirements**: SYNTH-01, SYNTH-02, MIDI-01, MIDI-02
**Plans**: 2 plans

Plans:
- [x] 03-01-PLAN.md -- Custom oscillator definitions (WavetableSynthesizer + oscillator() built-in)
- [x] 03-02-PLAN.md -- MIDI file export (DryWetMidi + writeMidi built-in)

### Phase 4: Composition Tools
**Goal**: Users can write chord progressions with automatic voicing, layer polyrhythmic patterns, and generate probabilistic variations of sequences
**Depends on**: Phase 3
**Requirements**: COMP-01, COMP-02, COMP-03, COMP-04
**Plans**: 2 plans

Plans:
- [x] 04-01-PLAN.md -- Chord progression DSL with voice leading (progression keyword, parser, ProgressionCompiler)
- [x] 04-02-PLAN.md -- Polyrhythm layering and probabilistic pattern variation (polyrhythm, vary built-ins)

### Phase 5: Live Coding
**Goal**: Users can edit Flow scripts during playback and hear changes take effect at musically appropriate moments without interruption
**Depends on**: Phase 4
**Requirements**: LIVE-01, LIVE-02
**Plans**: 2 plans

Plans:
- [x] 05-01-PLAN.md -- Streaming playback infrastructure, capture mode, and LiveReloadManager
- [x] 05-02-PLAN.md -- Wire LiveReloadManager into Program.cs and end-to-end verification

</details>

<details>
<summary>✅ v1.1 Polish & Foundations (Phases 6-10) — SHIPPED 2026-04-18</summary>

- [x] **Phase 6: Diagnostics & Bug Fixes** — --verbose flag, Sequence overload fixes, section bare expressions, error masking (completed 2026-04-04)
- [x] **Phase 7: Developer Experience** — // line comments, math stdlib, writeWav, REPL auto-imports (completed 2026-04-04)
- [x] **Phase 8: Audio Production** — mix(), per-section gain, strings/organ/bell synth presets (completed 2026-04-04)
- [x] **Phase 9: Advanced Features** — tempoRamp, interactive tutorial (completed 2026-04-04)
- [x] **Phase 10: Vocalization** — formant sing() + external TTS hook (completed 2026-04-04)

Full details: `milestones/v1.1-ROADMAP.md` · Audit: `milestones/v1.1-MILESTONE-AUDIT.md`

</details>

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Language Foundations | v1.0 | 3/3 | Complete | 2026-04-01 |
| 2. Audio Pipeline | v1.0 | 3/3 | Complete | 2026-04-02 |
| 3. Synthesis & MIDI Export | v1.0 | 2/2 | Complete | 2026-04-02 |
| 4. Composition Tools | v1.0 | 2/2 | Complete | 2026-04-02 |
| 5. Live Coding | v1.0 | 2/2 | Complete | 2026-04-03 |
| 6. Diagnostics & Bug Fixes | v1.1 | 2/2 | Complete | 2026-04-04 |
| 7. Developer Experience | v1.1 | 2/2 | Complete | 2026-04-04 |
| 8. Audio Production | v1.1 | 2/2 | Complete | 2026-04-04 |
| 9. Advanced Features | v1.1 | 2/2 | Complete | 2026-04-04 |
| 10. Vocalization | v1.1 | 2/2 | Complete | 2026-04-04 |
