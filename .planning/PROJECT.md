# Flow Language

## What This Is

Flow is an interpreted, statically-typed programming language designed for music production. Written in C# (.NET 10), it features a flow operator (`->`) for function chaining, music-specific types (Note, Chord, Song, etc.), inline note stream syntax, musical context blocks, a full audio pipeline from composition to WAV export, real-time playback via PulseAudio, and MIDI import. It targets composers, producers, and creative coders who want a textual, scriptable approach to music creation.

## Core Value

Users can write musical ideas as code and hear them immediately — the language must faithfully translate musical notation into correct, playable audio.

## Current State

**Shipped:** v1.4 Audio Fidelity, Distribution & Public Showcase (2026-05-16)

**Next milestone:** TBD — see `.planning/MILESTONES.md`

<details>
<summary>v1.4 Audio Fidelity, Distribution & Public Showcase (shipped 2026-05-16)</summary>

Delivered: per-voice polyphony + Phase 28 articulation envelopes (staccato/legato/accent/marcato/tenuto), Phase 29 sampled tonal instruments (piano/brass/sax/strings/flute/bell via CC-BY 4.0 University-of-Iowa MIS bundle), Phase 30 self-contained `flow` CLI binary (~40 MB) + install.sh + XDG config + 11-subcommand surface + MIDI↔Flow round-trip ±1 tick, Phase 31 LSP polish (4 closed gaps) + JetBrains plugin scaffolding, Phase 32 full Scala (`.scl`) microtonal tuning loader + `tuning t { ... }` musical-context block, Phase 33 SFZ orchestral sampler (blessed: VSCO Community CE 1.1.0, opt-in via `use "@sfz"`), Phase 34 curated symphony showcase ("In Five Voices") + ragtime companion ("Stride & Stomp") as the v1.4 closer + pre-public → public pivot.

- All v1.4 SPEC / REQ / SYM-01..05 requirements Complete (see REQUIREMENTS.md cross-inserts for Phase 30 + Phase 33 + Phase 34)
- 52 plans across Phases 28-34 (7 phases total)
- Release: [v1.4.0](https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0) — 5 assets (symphony.mp3+wav, ragtime.mp3+wav, flow-linux-x64.tar.gz)
- See: `.planning/MILESTONES.md` and the per-phase SUMMARYs under `.planning/phases/`

</details>

<details>
<summary>v1.3 Composer DX Tier B/C (shipped 2026-05-10)</summary>

Delivered: tuplets `{N:M ...}` + arbitrary fractional durations (`C4/12`), DEFER-01..06 closures (range, H-alias via pragma system, multi-letter enharmonics, slice negative indexing, Gaussian humanize), Tier B/C composer DX bundle (arpeggio params, chord inversions/voicings, delay sync, microtonal wedge via JI/Pythagorean/equal-temperament pragmas, scale linting, legato/portamento, snap-to-grid quantize, varispeed loadWav), foundational language consistency pass (prefix-only arithmetic; symbols + tuples + generic dicts), and music-type ergonomics + FX overloads (Hertz literals, volume/gain split, Ms/Sec/Decibel coercion).

- 41 requirements across 12 phases (Phases 18-27 plus inserted 26.1/26.2)
- See: `.planning/MILESTONES.md`

</details>

<details>
<summary>v1.2 Stability & Composer DX (shipped 2026-04-26)</summary>

Delivered: stable interpreter (init/Thunk/musical-context body fixes), Tier A + Tier B composer DX (slice, flat literals + enharmonic, MIDI velocity preservation end-to-end, reverbTime context block, euclidean swing/humanize with byte-identical output), retroactive Nyquist validation for v1.1 phases, tutorial + showcase refresh exercising every v1.1 + v1.2 feature, Flow Language Server + VSCode extension (per-platform self-contained VSIX with bundled stdlib).

- 18 of 18 requirements Complete (5 SPIKE + 13 fix/test/DX/QOL)
- 41 plans across Phases 11–17
- 4 deferred items at close (1 debug, 1 quick task, 3 Phase 17 HUMAN-UAT, 1 Phase 04 verification gap) — recorded in STATE.md
- 6 forward-deferred DX items (DEFER-01..06)
- See: `.planning/MILESTONES.md` and `.planning/milestones/v1.2-*.md`

</details>

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
- ✓ Tutorial + showcase refresh demonstrating v1.1 + v1.2 features end-to-end (`examples/tutorial.flow` 348→635 lines, `examples/showcase.flow` rewritten as ambient mood piece, paired WAV+MIDI export to `examples/output/`) — v1.2 Phase 16 (QOL-03)
- ✓ Scala (`.scl`) tuning loader with `(loadScala "path")` builtin + `tuning t { ... }` musical-context block, full Scala feature subset (cents + ratio steps, `.kbm` keyboard mapping, non-octave scales, negative cents, `!` line comments), 5 canonical archive fixtures + 3 malformed parser-error fixtures, ±0.1¢ Carlos Alpha / Bohlen-Pierce acceptance, last-wins integration with Phase 23 pragmas, D-13 MIDI-export dual-axis advisory, byte-identical two-run determinism — v1.4 Phase 32 (SPEC-1..SPEC-7)
- ✓ SFZ orchestral sampler: `Sfz` first-class type + `(loadSfz #symbol)` / `(loadSfz "path")` builtins + 19-entry GM dict in `@sfz` opt-in stdlib module + `"sampler:NAME"` instrument string dispatched in SongRenderer + 12 new GM-program entries in MidiExport + SfzParser (14-opcode whitelist with `<control>` + `default_path` cascade) + SfzRenderer (equal-power 441-frame crossfade, Phase 28 articulation envelope hook) + SfzSampleCache (per-engine, ordinal-sorted deterministic eager-load) + synthetic 19 KB smoke fixture for CI + VSCO-CE 1.1.0 path audit — v1.4 Phase 33 (SPEC-1..SPEC-8; HUMAN-UAT pending for real-library playback)

### Active

**v1.2 shipped 2026-04-26.** Active requirements list will be repopulated by `/gsd-new-milestone` (v1.3).

**Deferred candidates (post-v1.2):**
- Triplet/tuplet syntax + arbitrary fractional note durations (conversation trigger 2026-04-26)
- DEFER-01: `range(Int, Int) → Array[Int]` stdlib registration
- DEFER-02/03: `H` note-stream-only `B` alias via pragma system
- DEFER-04: Multi-letter enharmonic edges (E↔Fb, F↔E#, B↔Cb, C↔B#)
- DEFER-05: Slice negative-from-end indexing
- DEFER-06: Gaussian humanize distribution
- Tier B/C composer DX (arpeggio params, chord inversions, delay sync to note values, microtonal ratios, scale linting, legato/portamento, snap-to-grid)
- Audit §2 hardening (overload ambiguity, bandpass Q unbounded, stereo voices played as mono, ChordParser sharp formatting, scale database brittleness, OverloadResolver top-2 tie check)
- Pidgin parser combinator dependency removal (referenced but unused)
- Extended audio formats (FLAC, OGG)
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
- v1.2 close (2026-04-26): 41 plans across Phases 11–17 shipped — interpreter stability, Tier A + Tier B composer DX, retroactive Nyquist validation for v1.1 phases, tutorial+showcase exercising every v1.1 + v1.2 feature with byte-identical determinism, and Flow Language Server + VSCode extension
- Codebase at v1.2 close: ~83K LOC C# + 312 .flow files, 287/287 tests green
- Open at v1.2 close: 4 deferred items (1 debug session, 1 quick task, 3 Phase 17 HUMAN-UAT rows, 1 Phase 04 verification gap) — recorded in STATE.md Deferred Items

## Constraints

- **Runtime**: .NET 10 — all code must target net10.0
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
| Audit Spike isolated as own phase | Researcher disagreement on C1–C5 — pure investigation before any production code change | ✓ Good (v1.2) |
| `Thunk` → `Lazy<Value>` with ExecutionAndPublication | Single BCL primitive satisfies failure-cache + thread safety | ✓ Good (v1.2) |
| Charitable interpretation as load-bearing | `reverbTime 0` is dry sentinel, not error; 4 criterion-moot/reframe events across milestone | ✓ Good (v1.2) |
| Two-pass strict authorship | Pass 1 from REQUIREMENTS, Pass 2 reality check; format/signature drift caught pre-commit | ✓ Good (v1.2) |
| Schroeder closed-form `feedback = 10^(-3·avgDelay/RT60)` for reverbTime | Maps user-facing seconds to feedback coefficient with no parameter sweep | ✓ Good (v1.2) |
| HUMAN-UAT for non-blocking checkpoints | Phase 17 manual-smoke rows 1-3 deferred without faking pass; rows 4-5 explicitly deferred to first release tag | ✓ Good (v1.2) |
| Determinism contract end-to-end | Synth white-noise + TPDF dither RNGs reseeded at renderSong/writeWav boundaries; byte-identical WAV+MIDI two consecutive runs | ✓ Good (v1.2) |
| LSP project references flow-lang directly | `flow-lsp` reuses lexer/parser/error reporter; no shadow language model | ✓ Good (v1.2) |
| Per-platform self-contained VSIX with bundled stdlib | Avoids server-locator complexity; users get one .vsix per platform | ✓ Good (v1.2) |

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
*Last updated: 2026-05-16 after v1.4 milestone closure (Phase 34 — symphony showcase + public pivot) — v1.4 shipped*
