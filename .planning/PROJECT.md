# Flow Language

## What This Is

Flow is an interpreted, statically-typed programming language designed for music production. Written in C# (.NET 9), it features a flow operator (`->`) for function chaining, music-specific types (Note, Chord, Song, etc.), inline note stream syntax, musical context blocks, a full audio pipeline from composition to WAV export, real-time playback via PulseAudio, and MIDI import. It targets composers, producers, and creative coders who want a textual, scriptable approach to music creation.

## Core Value

Users can write musical ideas as code and hear them immediately — the language must faithfully translate musical notation into correct, playable audio.

## Requirements

### Validated

- ✓ Lexer/parser pipeline for Flow syntax — existing
- ✓ Static type system with music-specific types (Note, Chord, Sequence, Song, etc.) — existing
- ✓ Flow operator (`->`) for function chaining — existing
- ✓ Proc declarations with implicit returns — existing
- ✓ Lambda functions and closures — existing
- ✓ Musical context blocks (tempo, timesig, key, swing) — existing
- ✓ Note stream expressions with durations, rests, ties, dots, cent offsets — existing
- ✓ Chord literals and roman numeral resolution — existing
- ✓ Section/Song structure with repeats — existing
- ✓ Pattern transforms (transpose, invert, retrograde, augment, diminish, etc.) — existing
- ✓ Audio synthesis (piano, brass, sax, drums) — existing
- ✓ DSP effects (reverb, filter, compressor, delay, gain) — existing
- ✓ WAV export — existing
- ✓ Real-time playback via PulseAudio — existing
- ✓ MIDI import/conversion to Flow code — existing
- ✓ REPL with watch mode — existing
- ✓ Module imports (`use`) — existing
- ✓ Standard library (collections, audio, notation, composition) — existing
- ✓ Dynamic transforms (crescendo, decrescendo, swell, ritardando, accelerando) — existing
- ✓ Ornaments (trill, tremolo) and articulations — existing
- ✓ Generative features (euclidean rhythms, random choice) — existing
- ✓ Basic editor with live highlighting — existing

### Active

- [ ] Fix remaining audio rendering bugs (envelope edge cases, sample rate validation)
- [ ] Fix interpreter edge cases (parser flow expression handling, type system gaps)
- [ ] Add polyphonic voice allocation for richer arrangements
- [ ] Add custom oscillator definitions via user functions
- [ ] Add sidechain compression effect
- [ ] Add spatial audio / per-voice panning
- [ ] Add sample import (loadWav)
- [ ] Add pattern variation / probabilistic generation
- [ ] Add polyrhythm support (overlapping time signatures)
- [ ] Add chord progression DSL with auto-voicing
- [ ] Add beat-synced live reload for hot coding
- [ ] Add MIDI output/export
- [ ] Add sequence visualization (piano-roll ASCII)
- [ ] Add loop constructs (for/while)
- [ ] Add string interpolation

### Out of Scope

- GUI/DAW interface — Flow is a text-first language; visual editing is a separate project
- VST/AU plugin hosting — too complex for interpreter; focus on built-in synthesis
- Multi-user collaboration — single-user tool
- Cloud/web deployment — desktop CLI tool

## Context

- Brownfield project with 70+ test files, comprehensive standard library
- Audio backend is PulseAudio (Linux); abstracted via IAudioBackend for future portability
- Parser is hand-written recursive descent (not generated)
- Recent work focused on MIDI conversion, expressive notation (dynamics, articulations, ghost/grace notes), and editor integration
- Known bug areas: envelope division by zero on zero-length phases, some tests fail due to missing `use "@std"` imports, parser edge cases with flow expressions
- 5 bugs were just fixed: decrescendo velocity, retrograde bar order, division by zero crash, swell edge case, MIDI key signature mapping

## Constraints

- **Runtime**: .NET 9 — all code must target net9.0
- **Platform**: Linux primary (PulseAudio dependency), but IAudioBackend abstraction exists for portability
- **Dependencies**: Minimal — only Pidgin parser combinator (referenced but not used for main parser)
- **Performance**: Real-time audio playback requires efficient buffer operations; no GC pressure in hot paths
- **Compatibility**: Existing .flow scripts and test suite must continue to work

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Flow operator is parse-time transform | Simplifies runtime; no special flow concept needed | ✓ Good |
| Hand-written recursive descent parser | Full control over error recovery and music-specific syntax | ✓ Good |
| Immutable AST records | Thread safety, simplicity | ✓ Good |
| Overload resolution with specificity scoring | Enables natural function polymorphism for music types | ✓ Good |
| Musical context as scoped stack | Natural nesting (tempo inside key inside timesig) | ✓ Good |
| PulseAudio via P/Invoke | Direct, low-latency; but Linux-only | ⚠️ Revisit for portability |
| Soft-failure error model | Programs continue after errors; better REPL experience | ✓ Good |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition:**
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone:**
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-04-01 after initialization*
