# Flow Language

## What This Is

Flow is an interpreted, statically-typed programming language designed for music production. Written in C# (.NET 10), it features a flow operator (`->`) for function chaining, music-specific types (Note, Chord, Song, etc.), inline note stream syntax, musical context blocks, a full audio pipeline from composition to WAV export, real-time playback via PulseAudio, and MIDI import. It targets composers, producers, and creative coders who want a textual, scriptable approach to music creation.

## Core Value

Users can write musical ideas as code and hear them immediately — the language must faithfully translate musical notation into correct, playable audio.

## Current State

**Shipped:** v1.5 Stage, Studio, Web (2026-06-12) — 15 phases (35–49), 103 plans, 205 tasks. Milestone audit: `tech_debt` (0 unsatisfied requirements, cross-phase integration CLEAN). Tagged v1.5.0.

**Headline:** Flow became a real citizen of the music-software world — pattern matching + Rust-style diagnostics + a pure-Flow test framework, Tidal-style sequence algebra + generative + improv, granular/stretch/pitch-shift + stereo pan + sampler polish, a `live { }` block + modernized watch + REPL polish + mic input + OSC, MusicXML/LilyPond/ABC/MML interop, real-time MIDI + clock + JACK, module names + strict mode + Beat literals, and — the reach track — a Mono-WASM browser runtime with a real WebAudioBackend behind the greenfield flowlang.dev SvelteKit site (first audible .NET-in-WASM Flow, Firefox-confirmed).

**Deferred at close (composer-chosen 2026-06-08; shipped on machine-verified evidence + these as deferred debt):** real hardware MIDI/DAW/JACK UAT (Phase 40); JetBrains Marketplace publish + osx/win exec smoke + the v1.5.0 GitHub Release cut (Phase 41); Chrome/Safari audible audio (Phase 48); live Cloudflare Pages deploy + GitHub OAuth gist + cross-browser audio/visual/SR smoke (Phase 49). By-design defers: MIDI-RT-03 (CoreMIDI/WinMM) + LINK-01 (Ableton Link, GPL) → v1.6. Full list: STATE.md `## Deferred Items` + `.planning/milestones/v1.5-MILESTONE-AUDIT.md`.

**Next milestone:** not yet started — run `/gsd:new-milestone` (questioning → research → requirements → roadmap). Candidate v1.6 themes in `.planning/MILESTONES.md` `## v1.6 Backlog`.

<details>
<summary>v1.5 Stage, Studio, Web (shipped 2026-06-12)</summary>

**Goal:** Take Flow from "credible single-author public language" to "real citizen of the music-software world" — extending creative reach (live coding revamp, generative algebra, improv API), ecosystem interop (notation export, real-time MIDI, transport sync), and distribution (WASM runtime + browser site, cross-platform binaries, docs generator).

Delivered across Phases 35–49: pattern matching + `-> as name` + Rust-style diagnostics + pure-Flow test framework (35); 13 Tidal combinators + Markov/L-system/cellular/chaos generative + parameterized sections + `jam` improv + PrngRegistry (36); granular + time-stretch/pitch-shift + stereo pan + SFZ/sampler polish (37); `live { }` + watch + REPL polish + mic + OSC (38); MusicXML/LilyPond/ABC/MML notation interop (39); real-time MIDI + 24-PPQN clock + JACK transport (40); `flow doc` + WASAPI/CoreAudio + 5-RID binaries + JetBrains plugin + EDM showcase (41); type/stdlib audit (42); module names + qualified imports (43); `enable strict;` (44); Beat literal `Nb` + `beat-true-to-sig` pragma (45); bloat removal (46); `FlowTarget=Desktop|Web` (47); Mono-WASM runtime + WebAudioBackend (48); flowlang.dev SvelteKit site + skeuo playground (49).

- 104 tracked requirements; 0 unsatisfied at close; 2 by-design defers (MIDI-RT-03, LINK-01)
- Cross-phase integration CLEAN (7/7 seams wired; E2E flows byte-identical ×2)
- See: `.planning/MILESTONES.md` and `.planning/milestones/v1.5-*.md`

</details>

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
- ✓ Pattern matching (`(match … | pat => body | _ => …)` literal/wildcard/binding/guard + music-aware chord/numeral/articulation extractors; charitable non-exhaustive WARN, `enable matchExhaustive;` to error) + `-> as name` chain naming + Rust-style multi-line diagnostics + pure-Flow test framework (`flow test`) — v1.5 Phase 35 (LANG-01..04, TEST-01/02, HK-01..04)
- ✓ Tidal-style sequence algebra (13 `@patterns` combinators) + generative primitives (Markov/L-system/cellular/Lorenz-logistic in `@generative`) + parameterized sections + chord-aware `jam` improv + universal named args + `PrngRegistry` determinism — v1.5 Phase 36 (PAT-01/02, GEN-01..05, SECT-01, IMPROV-01)
- ✓ Granular synthesis + hand-rolled time-stretch/pitch-shift (`#vocoder`/`#psola`/`#auto`) + per-voice constant-power stereo pan + SFZ stereo retrofit + sampler polish (round-robin, velocity crossfade, per-articulation envelopes, warmer piano, more flute, sampled drums) — v1.5 Phase 37 (DSP-01..03, MIX-01/02, SAMP-01..03, PIANO-01, FLUTE-01, DRUM-01)
- ✓ Live coding 2.0: `live <quantize> { }` hot-swap + modernized `flow watch` panel + PrettyPrompt REPL (LSP completion, `:help fn`, history, `(visualize seq)`) + mic input (PA_STREAM_RECORD) + OSC client/server (Rug.Osc) — v1.5 Phase 38 (LIVE-01..03, REPL-01..04, AUDIO-IN-01/02, OSC-01/02)
- ✓ Notation interop: MusicXML 3.1 + LilyPond export, ABC 2.1 + MML PC-98 import (`@notation-io`) — v1.5 Phase 39 (XML-01/02, LILY-01, ABC-01/02, MML-01)
- ✓ Real-time MIDI output + 24-PPQN clock master/slave (direct librtmidi P/Invoke) + best-effort JACK transport — v1.5 Phase 40 (MIDI-RT-01/02/04, CLOCK-01/02, LINK-02, JACK-01; MIDI-RT-03 + LINK-01 deferred by design)
- ✓ `flow doc` generator + WASAPI/CoreAudio backends + 5-RID self-contained binaries + JetBrains plugin build + EDM showcase — v1.5 Phase 41 (DOC-01/02, WASAPI-01, COREAUDIO-01, BIN-01, JET-01, SHOWCASE-01; Marketplace publish + osx/win smoke = deferred HUMAN-UAT)
- ✓ Type/stdlib audit (`42-AUDIT.md`) + `module math` names + qualified imports + `enable strict;` mode + Beat literal `Nb` + `enable beat-true-to-sig;` + codebase bloat removal — v1.5 Phases 42–46 (REQ-AUDIT-*, REQ-MOD-*, REQ-STRICT-*, REQ-BEAT-*)
- ✓ `FlowTarget=Desktop|Web` compile-target conditioning + Mono-WASM `flow-lang` runtime + real JSImport `WebAudioBackend` + frozen `flow-runtime.js` API + flowlang.dev SvelteKit site (Home/Docs/Playground/Showcase, skeuomorphic) — v1.5 Phases 47–49 (REQ-WEB-TARGET-*, REQ-WASM-*/WEBAUDIO-*, REQ-SITE-*; live deploy + cross-browser audio = deferred HUMAN-UAT)

### Active

**v1.5 shipped 2026-06-12.** No active milestone — run `/gsd:new-milestone` to populate v1.6 requirements. Candidate themes parked in `.planning/MILESTONES.md` `## v1.6 Backlog` (e.g. pattern-match decision-tree backend, AudioWorklet/AnalyserNode, live-gist auto-rebuild, full-LSP Monaco, custom domain, piano EQ/sympathetic resonance, per-live-block quantize timelines).

**Deferred by design (→ v1.6):**
- MIDI-RT-03: CoreMIDI (macOS) + WinMM (Windows) real-time MIDI backends — same `IMidiBackend` abstraction
- LINK-01: Ableton Link transport sync — GPLv2+ contamination hazard; awaits clean-room/re-licensed binding (D-40-06)

### Out of Scope

- GUI/DAW interface — Flow is a text-first language; visual editing is a separate project
- VST/AU plugin hosting — too complex for interpreter; focus on built-in synthesis
- Multi-user collaboration — single-user tool
- ~~Cloud/web deployment~~ — **revised v1.5**: Flow now runs in the browser (Mono-WASM runtime + flowlang.dev playground, Phases 47–49). The CLI remains the primary surface; the web playground is a reach/demo surface, not a hosted multi-tenant service.

## Context

- Brownfield project with 70+ test files, comprehensive standard library
- Audio backend is PulseAudio (Linux); abstracted via IAudioBackend for future portability
- Parser is hand-written recursive descent (not generated)
- As of v1.1 close (2026-04-18): 10 shipped phases, full audio pipeline from composition → WAV export → playback, MIDI round-trip, vocal synthesis, and live-coding hot reload
- v1.1 close identified and fixed a section + nested-context + bare-expression composition bug (commit 2156690); `--verbose` diagnostics available via the CLI for future debugging sessions
- v1.2 close (2026-04-26): 41 plans across Phases 11–17 shipped — interpreter stability, Tier A + Tier B composer DX, retroactive Nyquist validation for v1.1 phases, tutorial+showcase exercising every v1.1 + v1.2 feature with byte-identical determinism, and Flow Language Server + VSCode extension
- Codebase at v1.2 close: ~83K LOC C# + 312 .flow files, 287/287 tests green
- Open at v1.2 close: 4 deferred items (1 debug session, 1 quick task, 3 Phase 17 HUMAN-UAT rows, 1 Phase 04 verification gap) — recorded in STATE.md Deferred Items
- v1.5 close (2026-06-12): 103 plans across Phases 35–49; `flow-lang` now compiles for both Desktop and Web (Mono-WASM) targets; greenfield `flow-site/` SvelteKit project added (TS/pnpm conventions, NOT C#); 7 primary C# projects (flow-lang/-interpreter/-cli/-lsp/-midi + two test suites). Milestone audit `tech_debt`: 0 unsatisfied requirements, integration CLEAN. ~36 pre-existing Phase 28/29/35/38 xUnit failures predate the v1.5 base (c4cd738); 0 new introduced by any v1.5 phase. Deferred human-UAT on Phases 40/41/48/49 recorded in STATE.md `## Deferred Items`.

## Constraints

- **Runtime**: .NET 10 — all code must target net10.0
- **Platform**: Linux primary (PulseAudio); macOS via CoreAudio, Windows via WASAPI (NAudio.Wasapi); browser via Mono-WASM WebAudioBackend (`FlowTarget=Web` strips P/Invoke/SFZ/OSC/MIDI/mic). IAudioBackend / IMidiBackend abstractions gate per-platform selection.
- **Dependencies**: Minimal & pinned — DryWetMidi 8.0.3 (MIDI SMF I/O, also retained on Web), Rug.Osc 1.2.5 (OSC, Desktop-only), NAudio.Wasapi 2.3.0 (Windows audio, Desktop-only), PrettyPrompt 4.1.1 (REPL). Pidgin removed 2026-06-09 (never used). librtmidi.so is a system prerequisite for `@midi` (not bundled). `flow-site/` uses pnpm/Vite/Svelte 5.
- **Performance**: Real-time audio playback requires efficient buffer operations; no GC pressure in hot paths
- **Compatibility**: Existing .flow scripts and test suite must continue to work; two-run cmp-clean determinism preserved for non-`live` render paths

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
| Pattern-match backend = naive linear scan (D-v1.5-11) | Composer-visible semantics fixed; decision-tree compile is internal swap | ✓ Good (v1.5) — deferred to v1.6 |
| `PrngRegistry` keyed by (SourceLocation, name) (D-v1.5-06) | Unseeded generative calls stay two-run cmp-clean; reseed at render boundary | ✓ Good (v1.5) |
| Hand-rolled DSP (vocoder/PSOLA/granular), reject RubberBand (D-v1.5-03) | GPL contamination hazard for MIT flow-lang.dll | ✓ Good (v1.5) |
| `live { }` opts OUT of determinism with stderr advisory (D-v1.5-07) | Honest: real-time hot-swap can't be byte-pinned; offline render stays deterministic | ✓ Good (v1.5) |
| Mono-WASM jiterpreter, NOT NativeAOT-LLVM (D-v1.5-02) | Reflection-heavy registry needs no source-gen pass; ships today | ✓ Good (v1.5) |
| `FlowTarget=Desktop|Web` MSBuild conditioning (Phase 47) | One codebase, browser-incompatible features `#if !FLOW_WEB`-stripped + Cecil-gated | ✓ Good (v1.5) |
| WebAudio via offline-render → AudioBuffer, NOT AudioWorklet | No .NET-in-WASM prior art for AudioWorklet driving; AudioBufferSourceNode works | ✓ Good (v1.5) |
| Frozen `flow-runtime.js` 5-export API consumed verbatim by flow-site | Phase 49 never edits the runtime; committed AppBundle → pure-Node CF build | ✓ Good (v1.5) |
| Ableton Link DEFERRED, not implemented (D-40-06) | GPLv2+ derivative-work hazard; clean-room community PR welcome | — Pending (v1.6) |
| Direct librtmidi P/Invoke, drop RtMidi.Core (Plan 40-04) | RtMidi.Core 1.0.53 ABI crashes modern librtmidi during port enum | ✓ Good (v1.5) |
| Ship v1.5 on machine-verified evidence + deferred human-UAT | Composer chose 2026-06-08 to defer hardware/deploy/marketplace gates | ✓ Good (v1.5) — debt tracked in STATE |

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
*Last updated: 2026-06-12 — after v1.5 Stage, Studio, Web milestone close (15 phases 35–49 shipped; 103 plans; audit tech_debt, 0 unsatisfied; tagged v1.5.0). Next: `/gsd:new-milestone`.*
