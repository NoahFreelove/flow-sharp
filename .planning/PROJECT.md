# Flow Language

## What This Is

Flow is an interpreted, statically-typed programming language designed for music production. Written in C# (.NET 10), it features a flow operator (`->`) for function chaining, music-specific types (Note, Chord, Song, etc.), inline note stream syntax, musical context blocks, a full audio pipeline from composition to WAV export, real-time playback via PulseAudio, and MIDI import. It targets composers, producers, and creative coders who want a textual, scriptable approach to music creation.

## Core Value

Users can write musical ideas as code and hear them immediately — the language must faithfully translate musical notation into correct, playable audio.

## Current State

**Shipped:** v1.1 Polish & Foundations (2026-04-18)

**Active:** v1.2 Stability & Composer DX (started 2026-04-18)

## Current Milestone: v1.2 Stability & Composer DX

**Goal:** Fix the 7 critical bugs surfaced by the 2026-04-18 audit, unblock the failing test suite, then ship the Tier A composer DX bundle and refresh the tutorial so v1.1 + v1.2 capabilities are discoverable.

**Target features:**
- Critical bug fixes (C1–C7 from `CODEBASE-AUDIT-2026-04-18.md`)
- Test unblocking (`range(Int, Int)`, `break`/`continue`, `bpm`/`createStereoTrack`/`renderBars`)
- Retroactive Nyquist validation for v1.1 phases 6–9
- Tier A DX bundle (sequence slicing, enharmonic helpers, `reverbTime` context, MIDI velocity from dynamics, euclidean swing/humanize)
- Tutorial refresh demonstrating v1.1 + v1.2 features

<details>
<summary>v1.1 Polish & Foundations (shipped 2026-04-18)</summary>

Delivered: diagnostics (--verbose), overload-resolution fixes, honest error reporting, // line comments, math stdlib, writeWav/REPL auto-imports, mix() + per-section gain, three synth presets (strings/organ/bell), tempoRamp, formant-based sing() + external TTS.

- 15 of 16 requirements Complete, 1 Invalid (FIX-04 — premise did not hold in current architecture)
- See: `.planning/MILESTONES.md` and `.planning/milestones/v1.1-*.md`

</details>

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
- ✓ Vocal synthesis (formant-based sing(), external TTS hook) — v1.1 Phase 10
- ✓ Polyphonic voice allocation — v1.0 Phase 2
- ✓ Custom oscillator definitions — v1.0 Phase 3
- ✓ Sidechain compression — v1.0 Phase 2
- ✓ Spatial audio / per-voice panning — v1.0 Phase 2
- ✓ Sample import (loadWav) — v1.0 Phase 2
- ✓ Pattern variation / probabilistic generation — v1.0 Phase 4
- ✓ Polyrhythm support — v1.0 Phase 4
- ✓ Chord progression DSL with auto-voicing — v1.0 Phase 4
- ✓ Beat-synced live reload — v1.0 Phase 5
- ✓ MIDI output/export — v1.0 Phase 3
- ✓ Sequence visualization (piano-roll ASCII) — v1.0 Phase 1
- ✓ Loop constructs (for/while) — v1.0 Phase 1
- ✓ String interpolation — v1.0 Phase 1
- ✓ `--verbose` diagnostic flag — v1.1 Phase 6
- ✓ Sequence/Semitone/Cent overload widening — v1.1 Phase 6
- ✓ Bare-expression capture in sections (incl. nested context blocks) — v1.1 Phase 6 + audit-driven fix
- ✓ Honest error reporting (no more function-not-found masking) — v1.1 Phase 6
- ✓ `//` line comments — v1.1 Phase 7
- ✓ Math stdlib (sin/cos/tan/abs/sqrt/min/max/floor/ceil/round/pow/log/pi/tau) — v1.1 Phase 7
- ✓ `writeWav` primary + `exportWav` alias — v1.1 Phase 7
- ✓ REPL auto-imports (@std, @audio, @collections) — v1.1 Phase 7
- ✓ `mix(Buffer, Buffer)` — v1.1 Phase 8
- ✓ Per-section gain musical context — v1.1 Phase 8
- ✓ Synth presets: strings, organ, bell — v1.1 Phase 8
- ✓ `tempoRamp(seq, startBPM, endBPM)` — v1.1 Phase 9
- ✓ Interactive tutorial script — v1.1 Phase 9
- ✓ `slice(Sequence, Int, Int)` + `slice(Array[T], Int, Int)` with silent two-sided clamping — v1.2 Phase 14 (DX-05)
- ✓ Flat-letter note literals (`Db4`, `Eb4`, `Gb4`, `Ab4`, `Bb4`, `Cb4`, `Fb4`) + `enharmonic(Note) → Note` — v1.2 Phase 14 (DX-06, H-alias deferred)
- ✓ MIDI velocity regression for `dynamics`/`crescendo`/`decrescendo`/`swell` (byte-pinned gradient) — v1.2 Phase 14 (DX-08)
- ✓ Language Server + VSCode extension (syntax highlighting, live diagnostics, completion, hover, signature help, go-to-def, note-stream-aware roman-numeral completion) — v1.2 Phase 17 (D-01..D-15; rows 4-5 of manual smoke deferred to first release tag)

### Active

**v1.2 Stability & Composer DX (in progress):** see `.planning/REQUIREMENTS.md` for REQ-IDs.

**Deferred candidates (post-v1.2):**
- Extended audio formats (FLAC, OGG) — see v2 Requirements in archive
- Per-voice effects chains
- Real-time MIDI output to external devices
- Type inference for `var` declarations
- Pattern matching / switch expressions
- User-defined types / structs
- Cross-platform audio backend (WASAPI, CoreAudio)

### Out of Scope

- GUI/DAW interface — Flow is a text-first language; visual editing is a separate project
- VST/AU plugin hosting — too complex for interpreter; focus on built-in synthesis
- Multi-user collaboration — single-user tool
- Cloud/web deployment — desktop CLI tool

## Context

- Brownfield project with 70+ test files, comprehensive standard library
- Audio backend is PulseAudio (Linux); abstracted via IAudioBackend for future portability
- Parser is hand-written recursive descent (not generated)
- As of v1.1 close (2026-04-18): 10 shipped phases, full audio pipeline from composition → WAV export → playback, MIDI round-trip, vocal synthesis, and live-coding hot reload
- v1.1 close identified and fixed a section + nested-context + bare-expression composition bug (commit 2156690); `--verbose` diagnostics available via the CLI for future debugging sessions
- Carried tech debt: tutorial does not yet showcase v1.1 features; phases 6–9 lack individual VERIFICATION.md files; Nyquist validation incomplete across v1.1 phases

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
| TextWriter? null-object for opt-in diagnostics | Zero cost when off; extensible to file output later | ✓ Good (v1.1) |
| IsCompatibleWith widening on music types (Semitone ← Int, Cent ← Double) | Unblocks natural transform calls like transpose(seq, 2) | ✓ Good (v1.1) |
| Bare-expression capture via sink field through ExecuteMusicalContext | Supports arbitrarily nested musical-context blocks inside sections | ✓ Good (v1.1 audit fix) |
| Path-first arg convention for file exports | Matches common stdlib conventions | ✓ Good (v1.1) |
| Mono-to-stereo promotion in buffer ops | Simplifies mix() for heterogeneous buffers | ✓ Good (v1.1) |
| Bar-midpoint BPM interpolation for tempo ramps | Single-bar sequences get averaged BPM, avoids edge cases | ✓ Good (v1.1) |
| Parallel bandpass formant synthesis | Uses Csound tenor tables; recognizable vowel output | ✓ Good (v1.1) |
| External process + 30s timeout for TTS | Keeps interpreter resilient when engine missing | ✓ Good (v1.1) |

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
*Last updated: 2026-04-20 — Phase 14 complete (DX-05 slice, DX-06 flat literals + enharmonic, DX-08 MIDI velocity regression); H-alias deferred to future pragma phase*
