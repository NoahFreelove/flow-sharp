# Feature Landscape

**Domain:** Music programming language (interpreted, statically-typed, composition-focused)
**Researched:** 2026-03-29
**Comparable systems:** SuperCollider, Sonic Pi, ChucK, Tidal Cycles, Strudel, Csound, Faust, FoxDot

## Current State of Flow

Flow already has a strong foundation: note streams, chord literals, roman numeral resolution, musical context blocks (tempo/key/timesig/swing), section/song structure, 4 synthesizers, DSP effects, pattern transforms, euclidean rhythms, random choice, WAV export, real-time playback, and MIDI import. The language is further along than most hobby music languages. The gaps below are what separates it from being genuinely useful for real composition work.

---

## Table Stakes

Features users expect from any music programming language that claims to support composition and playback. Missing any of these makes the language feel incomplete or broken for its target audience.

| Feature | Why Expected | Complexity | Flow Status | Notes |
|---------|--------------|------------|-------------|-------|
| **Loop constructs (for/while)** | Every programming language has iteration; `map`/`each` are not sufficient for imperative patterns like "play this 8 times with variation" | Low | Missing | SuperCollider has `do`, `collect`, `while`; Sonic Pi has `times`, `loop`; ChucK has `for`, `while`. Flow's functional `map`/`each` exist but explicit loops are expected for procedural music logic. |
| **Sample loading (loadWav)** | Loading audio files is fundamental -- drums, textures, found sound. Every music language supports this. | Medium | Missing | ChucK: `SndBuf`; Sonic Pi: `sample`; SuperCollider: `Buffer.read`; Tidal: `sample` is the primary way to make sound. Without this, Flow is synthesis-only, which severely limits usability. |
| **MIDI output/export** | Users need to get compositions into DAWs. MIDI export is the universal interchange format for musical data. | Medium | Missing (import exists) | Flow has MIDI import but no export. SuperCollider, ChucK, and Sonic Pi all support MIDI out. At minimum, export to .mid file; real-time MIDI out is a bonus. |
| **Per-voice panning / basic spatial** | Stereo placement is fundamental to mixing. Every multi-voice system needs pan control. | Low | Missing | Even basic `pan` as a per-voice parameter (-1.0 to 1.0) is expected. Csound and SuperCollider have full spatialization; Flow needs at minimum stereo panning per voice. |
| **Voice allocation / polyphony** | Playing multiple notes simultaneously with proper voice management. Currently song rendering handles multiple voices but the model is implicit. | Medium | Partial | SuperCollider uses SynthDef + Synth nodes with explicit voice management. Flow's song renderer handles multi-voice but users need explicit control: voice count limits, voice stealing policy. |
| **String interpolation** | Basic language ergonomics. Printing debug info or generating output requires string building. | Low | Missing | Every modern language has this. `"tempo is {tempo}"` or equivalent. Trivial to implement in the lexer/parser. |
| **Sidechain compression** | Standard mixing technique. The "pumping bass" effect is ubiquitous in electronic music. | Low | Missing | Implementation: a compressor whose gain reduction is driven by a separate input signal. Requires routing one buffer's envelope to control another buffer's dynamics. |

## Differentiators

Features that would set Flow apart from comparable systems. Not expected, but create real value.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Chord progression DSL with auto-voicing** | Flow already has chord literals and roman numerals. Adding voice leading, inversions, and drop voicings would make Flow uniquely powerful for harmonic composition. No other music programming language has this as a first-class DSL. | High | Sonic Pi has basic `chord()` but no voice leading. This is a genuine differentiator -- Flow's `key` context + roman numerals are the perfect foundation. |
| **Beat-synced live reload** | Modify code, hear changes on the next bar boundary. Sonic Pi's `live_loop` and Tidal's cycle-synced eval are the gold standard. | High | Flow's watch mode reloads on file change but likely restarts playback. True beat-sync means: detect change, wait for next bar/cycle boundary, hot-swap the relevant section. |
| **Custom oscillator definitions** | Let users define oscillators as Flow functions that generate sample buffers, not just pick from piano/brass/sax/drums. | Medium | SuperCollider's SynthDef is the benchmark. Flow's approach should be simpler: a `proc` that takes frequency + duration and returns a Buffer. |
| **Pattern variation / probabilistic generation** | Beyond `(? ...)` random choice: Markov chains, weighted pattern mutation, conditional branching within note streams. | Medium | Tidal Cycles excels here with `degrade`, `sometimes`, `often`, `rarely`. Flow should add probability-weighted transforms. |
| **Polyrhythm support** | Overlapping time signatures / different cycle lengths playing simultaneously. | High | Tidal's core strength -- polymeter syntax. Flow needs ability to layer sequences with different time signatures. |
| **Sequence visualization (piano-roll ASCII)** | See what you composed before rendering audio. Instant feedback loop. | Low | No comparable music language does this well in a terminal. Low complexity, high perceived value. |

## Anti-Features

Features to explicitly NOT build in this milestone.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Full DAW / GUI editor** | Flow is text-first by design. Building a DAW is a multi-year project. | Improve REPL feedback. Export to DAW-compatible formats (WAV, MIDI). |
| **VST/AU plugin hosting** | Complex plugin hosting framework, sandbox security, platform-specific APIs. Enormous complexity. | Focus on built-in synthesis and sample loading. Use MIDI out to drive VST instruments in a DAW. |
| **Real-time audio input** | Requires low-latency audio I/O, monitoring, feedback prevention. Different architecture. | Flow is a composition language, not a live effects processor. |
| **Multi-user collaboration** | Network sync, conflict resolution, permissions. | Share .flow files via git. |
| **Complex OOP / class system** | Flow uses `proc` and simple types. Classes/inheritance would bloat the language. | Keep proc + type system. Add simple record types only if needed. |

## Feature Dependencies

```
Loop constructs -----> Pattern Variation (loops are the natural place to apply probabilistic transforms)
                \---> Chord Progression DSL (iteration over progressions)

Sample Loading -----> Sidechain Compression (need a kick drum sample to drive sidechain)
               \---> Richer drum patterns (sample-based drums > synth drums)

Voice Allocation ---> Custom Oscillators (voices use oscillators)
                \---> Polyrhythm (parallel time grids per voice)

String Interpolation (standalone -- no dependencies)
Per-Voice Panning (standalone -- math on existing stereo buffers)
Sequence Visualization (standalone -- reads existing data structures)
MIDI Export (standalone -- requires DryWetMidi package)
Beat-Synced Reload (standalone -- extends existing watch mode)
```

## MVP Recommendation

### Phase 1 -- Language Foundations (unblock everything else)
1. **Loop constructs (for/while)** -- Low complexity, unblocks iteration patterns
2. **String interpolation** -- Low complexity, quality of life
3. **Sequence visualization** -- Low complexity, high impact on feedback loop

### Phase 2 -- Audio Pipeline Expansion
4. **Sample import (loadWav)** -- Opens sample-based composition
5. **Panning / spatial audio** -- Stereo mixing
6. **Sidechain compression** -- Production technique
7. **Polyphonic voice allocation** -- Richer arrangements

### Phase 3 -- Creative Features
8. **Custom oscillator definitions** -- Programmable synthesis
9. **Pattern variation / probabilistic generation** -- Generative music
10. **MIDI export** -- Interoperability with DAWs

### Phase 4 -- Advanced Features
11. **Chord progression DSL** -- Music theory as syntax
12. **Polyrhythm support** -- Complex rhythmic structures
13. **Beat-synced live reload** -- Live coding experience

**Defer:** Polyrhythm and beat-synced live reload are highest complexity with most risk. They may need their own research spikes.

## Sources

- Existing codebase analysis: `flow-lang/StandardLibrary/`, `flow-lang/Runtime/`, `flow-lang/Parsing/`
- PROJECT.md active requirements list
- Comparable systems: Sonic Pi, TidalCycles, SuperCollider, ChucK, Csound, Faust
