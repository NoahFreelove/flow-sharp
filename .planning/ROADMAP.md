# Roadmap: Flow Language

## Milestones

- ~~**v1.0 MVP**~~ — Phases 1-5 (shipped 2026-04-03)
- ✅ **v1.1 Polish & Foundations** — Phases 6-10 (shipped 2026-04-18) — see `milestones/v1.1-ROADMAP.md`
- ✅ **v1.2 Stability & Composer DX** — Phases 11-17 (shipped 2026-04-26) — see `milestones/v1.2-ROADMAP.md`
- ✅ **v1.3 Composer DX Tier B/C** — Phases 18-27 (with 26.1 + 26.2 inserted, shipped 2026-05-10)
- ✅ **v1.4 Audio Fidelity, Distribution & Public Showcase** — Phases 28-34 (shipped 2026-05-16) — runtime-fidelity rewrite (per-voice polyphony, articulation system, richer instrument timbres), distribution wedge (`flow` CLI + formal install + MIDI↔Flow conversion), LSP polish + JetBrains plugin scaffolding, full Scala (`.scl`) microtonal loader, full SFZ orchestral sampler, and the curated symphony showcase ("In Five Voices") + ragtime companion ("Stride & Stomp") as the milestone closer (pre-public → public pivot). Release: [v1.4.0](https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0)
- ✅ **v1.5 Stage, Studio, Web** — Phases 35-49 (shipped 2026-06-12) — citizenship + reach: pattern matching + Rust-style diagnostics + pure-Flow test framework (35), Tidal-style sequence algebra + generative + improv (36), granular/stretch/pitch-shift + stereo pan + sampler polish (37), `live { }` + watch + REPL polish + mic + OSC (38), MusicXML/LilyPond/ABC/MML notation interop (39), real-time MIDI + clock + JACK transport (40), cross-platform binaries + `flow doc` + JetBrains plugin + EDM showcase (41), type/stdlib audit (42), module names + qualified imports (43), `enable strict;` (44), Beat literal `Nb` + `beat-true-to-sig` pragma (45), bloat removal (46), `FlowTarget=Desktop|Web` compile-target flavors (47), Mono-WASM runtime + WebAudioBackend (48), flowlang.dev SvelteKit site + skeuo playground (49). Ableton Link DEFERRED (GPL → v1.6). See `milestones/v1.5-ROADMAP.md`; audit `milestones/v1.5-MILESTONE-AUDIT.md`.

## Phases

<details>
<summary>v1.0 MVP (Phases 1-5) — SHIPPED 2026-04-03</summary>

- [x] **Phase 1: Language Foundations** — Add loops, string interpolation, iteration guards, and sequence visualization (completed 2026-04-01)
- [x] **Phase 2: Audio Pipeline** — Add sample loading, stereo panning, sidechain compression, and polyphonic voice allocation (completed 2026-04-02)
- [x] **Phase 3: Synthesis & MIDI Export** — Add custom oscillator definitions and MIDI file export (completed 2026-04-02)
- [x] **Phase 4: Composition Tools** — Add chord progression DSL, polyrhythm support, and probabilistic pattern variation (completed 2026-04-02)
- [x] **Phase 5: Live Coding** — Add beat-synced live reload with playback state preservation (completed 2026-04-03)

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

<details>
<summary>✅ v1.2 Stability & Composer DX (Phases 11-17) — SHIPPED 2026-04-26</summary>

- [x] **Phase 11: Audit Spike** — Reproduce or close C1–C5 audit claims with failing tests or documented dismissals (completed 2026-04-19; 1 Confirmed C1, 4 Dismissed C2–C5)
- [x] **Phase 12: Stability** — Ship confirmed bug fixes (C6 → FIX-05, C7 → FIX-06, C1 → FIX-07a), reframe TEST-03 around real failures (if-overload + auto-mkdir), and unblock the failing test suite (completed 2026-04-19)
- [x] **Phase 13: Nyquist Validation Backfill** — Retroactive VALIDATION.md for v1.1 phases 6-9 + Phase 10 promoted to nyquist_compliant (completed 2026-04-20)
- [x] **Phase 14: Composer DX Part 1** — `slice`, flat-literal surface + `enharmonic()`, MIDI velocity regression end-to-end (completed 2026-04-20)
- [x] **Phase 15: Composer DX Part 2** — Euclidean swing/humanize + `reverbTime` context block (completed 2026-04-25; full suite 287/287)
- [x] **Phase 16: Tutorial Refresh** — `examples/tutorial.flow` + `examples/showcase.flow` demonstrate v1.1 + v1.2 features end-to-end with byte-identical determinism (completed 2026-04-25)
- [x] **Phase 17: Flow Language Server** — LSP + VSCode extension delivering syntax highlighting, diagnostics, and intelligent completion/hover suggestions (completed 2026-04-20; 3 manual-smoke rows tracked as pending HUMAN-UAT, marketplace publish deferred to first release tag)

Full details: `milestones/v1.2-ROADMAP.md`

</details>

## v1.3 Composer DX Tier B/C (Phases 18-27) — SHIPPED 2026-05-10

Lead capability: tuplets `{N:M ...}` + arbitrary fractional note durations (`C4/12`). Closes DEFER-01..06 from v1.2, ships the Tier B/C composer DX bundle (arpeggio params, chord voicings, delay sync, microtonal wedge, scale linting, legato/portamento, snap-to-grid quantize, varispeed loadWav), lands a foundational language consistency pass — prefix-only arithmetic standardization (Phase 26) followed by symbols + tuples + generic dicts (Phase 26.1) — and resolves the music-type ergonomics gap surfaced after Phase 25 (Phase 26.2). 41 requirements across 12 phases.

**Locked decisions** (from `/gsd-new-milestone` discussion):

- D-01: Tuplet bracket syntax is `{N:M ...}` (braces)
- D-02: Pragmas are file-scope only, top-of-file only, NOT propagated via `use`
- D-03: Microtonal scope is named-tunings wedge; full Scala loader deferred to v1.4
- D-04: Gaussian humanize ships as separate `humanizeGaussian()` (preserves byte-identical determinism)
- D-05: MIDI TPQN cap when tuplets force auto-elevation is 9600

### Phase Summary

- [x] **Phase 18: Foundation — Rational Duration Arithmetic** — `Fraction` struct + `MusicalNoteData.DurationFraction`; foundation for tuplets and fractional durations — Shipped 2026-04-26 (commits 2092f32 + ba8534a)
- [x] **Phase 19: Tuplets & Arbitrary Fractional Durations** — `{N:M ...}` brackets + `C4/N` syntax + `C4/X:Y[suffix]` per-note shorthand, nested tuplets, bar-fit validator, auto-elevated MIDI TPQN (cap 9600) — Shipped 2026-04-26 (commits a7f94ef + 9aae23c + 3679ab4 + dbc6f30 + e2cdbe5)
- [x] **Phase 20: Cheap DEFER Closures + Multi-letter Enharmonic Edges** — `range(Int, Int[, Int])`, slice negative-from-end, multi-letter enharmonic edges (E↔Fb, F↔E#, B↔Cb, C↔B#) — Shipped 2026-04-26 (commits d0d17db + d835336 + edd20b1 + closure)
- [x] **Phase 21: Pragma System + H-Alias** — `enable <pragma>;` file-scope pragma infrastructure (Haskell-precedent), DEFER-02/03 H-as-B alias inside note streams — Shipped 2026-04-26 (commits 60f7f18 + 05c2174 + closure)
- [x] **Phase 22: Tier B/C Composer DX Bundle** — arpeggio params, chord inversions/voicings, delay sync to NoteValue, snap-to-grid quantize, legato/portamento articulations, varispeed loadWav — Shipped 2026-05-02 (commits 6500412, 95582e7, 5fba059, 98da48e, d3f5350, d2bde5d)
- [x] **Phase 23: Microtonal Tuning (Wedge)** — Named-tunings via pragma (`enable justIntonation;` / `enable pythagorean;` / `enable equalTemperament;`); Pattern A `RenderTuning` value object threaded through `PitchConversion.NoteToFrequency` + 13 synthesizers; 7 JI + 7 Pythagorean mode-keyed ratio tables; `ScaleDatabase.TryParseKeyWithMode` 5-church-mode extension; `RenderingDiagnostics` one-shot warnings for D-11/D-13; transforms remain MIDI-pitch invariant per MICR-02 — Shipped 2026-05-03 (commits b6b916b + 39ef570 + 47d7718 + f6b00ba + 470c3cb + 8190fb2 + 4ea0927 + 3e6a3ba + ba27282 + 4f85eaf + closure)
- [x] **Phase 24: Scale Linting (flow-lsp)** — Opt-in `enable scaleLint;` pragma emits Information-severity diagnostics for non-diatonic notes inside `key { ... }` contexts — Shipped 2026-05-04 (zero flow-lang touch beyond one PragmaRegistry line)
- [x] **Phase 25: Gaussian Humanize (LAST PRNG phase)** — `humanizeGaussian()` Box-Muller transform; preserves v1.2 byte-identical determinism contract for existing uniform `humanize()` — Shipped 2026-05-04 (commits 528cfe1 + b9017fc + 3cc3a11 + 5169db8 + closure)
- [x] **Phase 26: Op Standardization (Prefix-Only)** — Eliminate infix `+ - * /`; add `(add)`/`(sub)`/`(mul)`/`(div)`/`(neg)`/`(concat)` builtins covering numeric widening chain; remove `BinaryExpression`/`BinaryOperator` AST nodes; migrate stdlib + ~70 .flow tests; foundation for Phase 26.1 — Shipped 2026-05-09
- [x] **Phase 26.1: Symbols + Tuples + Dicts (INSERTED)** — Symbol primitive (`#foo`), Tuple type (`<<a, b, c>>` literal, `~>` unpack op, destructuring, `@N` indexing, per-position types), generic `Dict<K, V>` with hashable keys (Int/Long/Float/String/Symbol/Note/Chord/Tuple); dicts via `(dict K V ...)` + `(dictTuple <<K,V>> ...)` builtins (no literal syntax) — Shipped 2026-05-09 (Waves 0-5: ac3b926 + 35474ed + 6549116 + d628870 + daaa023 + closure)
- [x] **Phase 26.2: Music Type Ergonomics + FX Overloads (INSERTED)** — Music-type numeric compatibility completion (Ms/Sec/Hertz IsCompatibleWith Double|Float; Semitone stays Int-only); FX music-typed overloads (delay-Ms, compress/sidechain-Decibel-Ms, reverb-Second, lowpass/highpass/bandpass-Hertz, createXxxTone-Hertz family); new Hertz type with `800Hz`/`1.5kHz` literals; new `volume(Buffer, Double)` linear-multiplier function alongside `gain` (which stays dB-only); 2 pre-existing RED DecibelBeatNumericCompatFacts closed via Value.ConvertTo Double-arm + audio.flow gain(Decibel) forward decl — Shipped 2026-05-10 (Waves 0-5: 45b01fb + 4f92c24 + 28158cc + dfbfa1f + 6df301e + 86bdd15)
- [x] **Phase 27: Tutorial + Showcase Refresh** — examples/tutorial.flow + examples/showcase.flow exercise every v1.3 feature end-to-end (prefix-only arithmetic, symbols, tuples, dicts, tuplets, fractional durations, microtonal/scale-lint pragma documentation, DX-10..15, range/multi-letter enharmonics/negative slice, humanizeGaussian, Phase 26.2 volume/gain split + Hertz literals + Ms-FX overloads + Second-decay reverb); 2 companion files under examples/pragmas/ (h_alias.flow + microtonal_ji.flow); byte-identical determinism preserved (Phase 18/25 sentinels + new Phase 27 ByteIdenticalPragmaTests 4 facts); CLAUDE.md gains Music Types Quick Reference table — Shipped 2026-05-10 (Waves 1-5: 995ff67 + dbffbec + eadbd9f + e15c5be + ace6416)

## v1.4 Audio Fidelity, Distribution & Public Showcase (Phases 28-34) — SHIPPED 2026-05-16

Three intertwined threads closed v1.4 and pivoted Flow from pre-public to public:

1. **Audio fidelity** (Phases 28, 29, 33) — runtime polyphony rewrite + first-class articulation; modest realism pass on existing synths; full SFZ orchestral sampler for serious composition.
2. **Distribution + tooling** (Phases 30, 31, 32) — `flow` CLI + formal install + MIDI↔Flow conversion; LSP polish, VSCode marketplace publish, JetBrains plugin (stretch); full Scala (`.scl`) tuning loader.
3. **Public showcase** (Phase 34, milestone closer) — short symphony rendered entirely from Flow source via the SFZ sampler; the headline artifact of v1.4 and the moment Flow stopped being pre-public.

v1.3's byte-identical determinism contract is preserved in shape (two-run cmp-clean) but pinned bytes changed because the rendered output legitimately differs.

### Phase Summary

- [x] **Phase 28: MIDI + Audio Polyphony & Articulation Rewrite** — per-voice polyphony + 5 articulation tokens (staccato/legato/accent/marcato/tenuto) with locked envelope rules across 9 shipping synthesizers; `voicePool 32 { ... }` musical-context block; multi-track MIDI export — Shipped 2026-05-10
- [x] **Phase 29: Instrument Realism** — sampled tonal instruments (piano/brass/sax/strings/flute/bell) via CC-BY 4.0 University-of-Iowa MIS bundle (3.05 MB, 21 WAVs) + `SampledInstrumentRenderer` layering Phase 28 articulation envelopes — Shipped 2026-05-12
- [x] **Phase 30: Flow CLI + Formal Install** — `flow` self-contained Linux x64 binary (~40 MB) + 11 subcommands + install.sh + XDG config (5 functional keys) + MIDI↔Flow round-trip (±1 tick on 3 CC0 fixtures via Quantizer + FlowGenerator rewrite) — Shipped 2026-05-11
- [x] **Phase 31: LSP Enhancements + JetBrains Stretch** — 4 closed LSP gaps (completion filtering + varargs rendering + comment-form handling + scale-lint diagnostics wiring) + JetBrains plugin scaffolding (stretch goal MET) — Shipped 2026-05-12
- [x] **Phase 32: Full Scala (.scl) Tuning Loader** — `(loadScala "path")` builtin + `tuning t { ... }` musical-context block + `Tuning` first-class music type + optional `.kbm` keyboard mapping + ±0.1¢ Carlos Alpha/Bohlen-Pierce acceptance — Shipped 2026-05-15
- [x] **Phase 33: SFZ Orchestral Sampler** — `use "@sfz"` opt-in gate + 19-entry GM symbol dict + 13-opcode common-subset parser + SfzRenderer with 441-frame equal-power crossfade + blessed external library VSCO Community CE 1.1.0 — Shipped 2026-05-16
- [x] **Phase 34: Symphony Showcase (v1.4 closer)** — "In Five Voices" symphony + "Stride & Stomp" ragtime companion + v1.4.0 annotated tag + GitHub Release with 5 labeled assets + docs/announcements/v1.4.0.md + top-level README ## Showcase section — Shipped 2026-05-16

Full details for the Phase 18–34 detail sections were preserved in `.planning/phases/{18..34}/` per-phase planning artifacts.

## v1.5 Stage, Studio, Web (Phases 35-49) — SHIPPED 2026-06-12

Citizenship + reach milestone over the already-shipped v1.4 base. Across 15 phases (35–49) Flow adds 23 picked features + 4 v1.4 carryovers + housekeeping to take Flow from "credible single-author public language" to "real citizen of the music-software world" alongside TidalCycles, Sonic Pi, Strudel, and SuperCollider — extending creative reach (live coding revamp, generative algebra, improv API), ecosystem interop (notation export, real-time MIDI, transport sync), and distribution (WASM playground, cross-platform binaries, docs generator). 104 tracked requirements across 15 phases. Pre-traction no-deprecation latitude is ACTIVE — breaking changes ship in one commit with in-repo migrators.

**Locked decisions** (from `/gsd-new-milestone` discussion + research synthesis):

- D-v1.5-01: Pre-traction no-deprecation latitude is ACTIVE — breaking syntax/builtin changes ship in single commits; in-repo migrators only.
- D-v1.5-02: WASM playground ships on Mono-WASM jiterpreter, NOT NativeAOT-LLVM. Reflection-heavy `InternalFunctionRegistry` would require source-generator pass — deferred to v1.6.
- D-v1.5-03: Phase vocoder hand-rolled (RubberBand GPL hazard rejected, same posture as Phase 29 SPEC-2 license discipline).
- D-v1.5-04: Ableton Link integration license-gated — Phase 40 plan-start requires legal review of GPLv2+/commercial dual-license posture. If conflict, Link deferred to community contribution.
- D-v1.5-05: Pattern matching exhaustiveness — non-exhaustive matches WARN to stderr and fall through to Void (charitable interpretation rule). Composer opts INTO strict via `enable matchExhaustive;` pragma.
- D-v1.5-06: Generative primitive determinism — all PRNG-driven calls route through new `PrngRegistry` keyed by `(SourceLocation, generator-name)`; unseeded calls reseed at `renderSong`/`writeWav` boundary. Lorenz cross-platform FP divergence documented as platform-specific limitation.
- D-v1.5-07: Live block determinism opt-out — `live { ... }` blocks emit a stderr advisory on every entry explicitly noting they opt OUT of the two-run cmp-clean determinism contract. 30s wall-clock evaluation cap + 200ms file-watch debounce + bar-boundary swap.
- D-v1.5-08: MusicXML reference consumer is MuseScore. Articulation decision table locked: Accent→`<accent/>`, Marcato→`<strong-accent/>`, Staccato→`<staccato/>`, Tenuto→`<tenuto/>`, Sforzando→`<dynamics><sfz/></dynamics>`, Legato→slur spans (NOT per-note `<legato/>`).
- D-v1.5-09: Stereo pan audit gap (PROJECT.md says shipped v1.0 Phase 2; v1.4 backlog says open) — resolve at Phase 37 CONTEXT spawn; likely synth-path shipped + SFZ sampler-path mono-only.
- D-v1.5-10: Phase 35 is the dependency root — pattern matching used in Phase 36 destructuring + Phase 40 MIDI dispatch + Phase 39 articulation emit. Span migration runs first within Phase 35; test framework runs second.
- D-v1.5-11: Pattern-matching backend lands as naive linear scan in Phase 35; Jacobs/Peterse decision-tree compile deferred to v1.6 per D-v1.5-01. Backend swap is internal-only (no composer-visible API change).

### Phase Summary

- [x] **Phase 35: Language Foundation** (completed 2026-05-19) — Pattern matching (literal + constructor + wildcard + guard + music-aware extractors), Rust-style multi-line diagnostics, pure-Flow test framework + `flow test` CLI, `-> as name` chain naming, and v1.4 housekeeping (humanizeGaussian voice-block bug, Phase 17 HUMAN-UAT rows 1-3, Phase 04 verification gaps, CLAUDE.md "Public as of v1.4" footnote revision)
- [x] **Phase 36: Sequence Algebra & Generative** (completed 2026-05-22) — 13 Tidal-style combinators on `Sequence` in `@patterns` stdlib (every/fast/slow/chunk/phase/rev/iter/palindrome/jux/superimpose + sometimes/degrade/sparseSeq), generative primitives in `@generative` stdlib (Markov + train/generate split + MarkovModel ref-identity type, L-system + LsystemModel, cellular automata 1D + 2D Conway, Lorenz/logistic chaos maps + quantizeToScale bridge), parameterized sections (SECT-01 — full Phase 35 pattern syntax in signatures + overloading + defaults + `*N` repeat + Rust-style diagnostics), chord-aware Markov improvisation in `@improv` stdlib (`jam` + 3 baseline Flow-file style packs + XDG override discovery), universal named-argument syntax (D-36-11) with ~150-builtin backfill, and the new `Runtime/PrngRegistry` foundation routing PRNG by (SourceLocation, name) preserving two-run cmp-clean determinism
- [x] **Phase 37: Sound Design + Sampler Polish (largest phase)** — Granular synthesis (Hann/Gaussian/Tukey), independent time-stretch + pitch-shift via hand-rolled phase vocoder + PSOLA + #auto HPS detector, stereo pan + SFZ-renderer stereo retrofit, sampler polish bundle (SFZ round-robin, velocity-layer crossfade, per-articulation envelope multipliers, warmer piano timbre, more flute samples, sampled drums) (completed 2026-05-23)
- [x] **Phase 38: Live Coding 2.0** — `live <quantize> { ... }` block + modernized watch mode + REPL polish (LSP-backed completion, `:help fn` per D-38-09 overrides `?fn`, multiline + history search, `(inspect seq)`/`(visualize seq)` alias pair per D-38-10) + audio input via PA_STREAM_RECORD + OSC server/client (Rug.Osc 1.2.5 + PrettyPrompt 4.1.1) (completed 2026-05-24)
- [x] **Phase 39: Notation Citizenship** — MusicXML 3.1 partwise export (MuseScore reference consumer per D-v1.5-08), LilyPond text emit, ABC 2.1 + abc2midi import, MML PC-98 common-core import (completed 2026-05-23)
- [x] **Phase 40: Studio Sync** (execution complete 2026-06-07 — MIDI + clock spine SHIPPED machine-proven; hardware/DAW behaviors PENDING HUMAN-UAT; Link DEFERRED per D-40-06 GPL; JACK best-effort per D-40-05; **+Plan 40-04 CRITICAL ABI fix 2026-06-07**) — `IMidiBackend` abstraction mirroring `IAudioBackend` (ALSA-seq via **direct librtmidi P/Invoke** — Plan 40-04 REPLACED RtMidi.Core 1.0.53, whose 2018 ABI `free(): invalid pointer`-crashes on modern librtmidi ≥4.0 during `(midiPorts)` enumeration; CoreMIDI + WinMM deferred to Phase 41 = MIDI-RT-03), MIDI clock master + slave (24 PPQN), JACK transport best-effort (`jackSync` via hand-rolled `jack_transport_query` — JackSharp 0.4.0 has no transport API). Ableton Link DEFERRED to community/v1.6 (GPLv2+ contamination, D-40-06 / D-v1.5-04); LINK-02 determinism shipped as the offline-render invariant. Phase40 suite 45/45 GREEN, incl. `RealMidiLoopbackTests` (real snd-virmidi ALSA loopback, captured via `amidi` — proves MIDI-RT-01/02 + CLOCK-01/02 on the native path). Closure: `40-VERIFICATION.md` (9-ID trace) + `40-04-SUMMARY.md` (ABI fix) + `40-HUMAN-UAT.md` (real synth/DAW/JACK rows)
- [x] **Phase 41: Reach + v1.5 Closer** — cross-platform binaries (linux-x64/arm64, osx-x64/arm64, win-x64), WASAPI + CoreAudio backends, `flow doc` generator with example execution, JetBrains Marketplace publish, third-genre showcase (jazz/EDM/death metal). **WASM playground bullet superseded 2026-05-25 by Phases 47-49** (carved into compile-target flavors + WASM runtime + flowlang.dev site track). (completed 2026-06-08)
- [x] **Phase 42: Type System & Stdlib Audit** — Reflective audit of FlowType ↔ FunctionSignature graph + clamp/advisory/charitable inventory + .flow caller cross-reference; ships `42-AUDIT.md` deliverable with 7 gap-class sections + 53 routing tags (→ Phase 43 module/naming, → Phase 44 strict-mode Axis B sites, → v1.6-backlog); anchor finding: `BeatType` is the sole coercible orphan. **Zero production code touched — read-only audit phase** (invariant gate-enforced via empty production diff). Closed 9 REQ-AUDIT-NN across 4 plans; 26/26 Phase 42 fixtures GREEN. (completed 2026-05-24)
- [x] **Phase 43: Module Names & Qualified Imports** — file-level `module math` declaration + qualified `math.sin` access; depends on Phase 42 AUDIT.md §1/§2/§5a routing (completed 2026-05-24)
- [x] **Phase 44: Strict Mode** — `enable strict;` file pragma; Axis A type-coercion rejection + Axis B input-perimeter clamp errors + Bool-if/String-print discipline; depends on Phase 42 AUDIT.md §2 explicit-conversion-builtin shapes + §6a 13 input-perimeter clamps + §6b 117 advisory sites (completed 2026-05-25)
- [x] **Phase 45: Beat Literal Syntax & True-to-Sig Pragma** — first-class `Nb` Beat literal (`0.5b`/`2b`/`-1b`) + opt-in `enable beat-true-to-sig;` file pragma retuning literal + `(beat N)` constructor to active timesig's beat unit (×4/denominator at construction time); closes the Beat-ergonomics gap left by Phase 43. 26 REQ-BEAT-NN across 6 plans; 66 Phase 45 fixtures GREEN + 2 composer tutorials with two-run cmp-clean WAV baselines (completed 2026-05-29)
- [x] **Phase 46: Codebase Bloat Removal** — paid down accumulated cruft from 40+ phases acting on the read-only `CODEBASE-BLOAT-AUDIT-2026-05-24.md` deliverable (~1,100 LOC upper-bound across 7 deletable files + secondary items, zero high-confidence false-positives); 6/6 plans, VERIFICATION passed, one atomic test-suite-green gate (completed 2026-05-24)
- [x] **Phase 47: Compile-Target Flavors** — `FlowTarget=Desktop|Web` MSBuild conditioning so `flow-lang.dll` compiles cleanly under WASM by stripping browser-incompatible features (P/Invoke audio backends, SFZ, OSC, mic-in, RtMidi, samples); Mono.Cecil reflective invariant gate; foundation for Phase 48. 10 REQ-WEB-TARGET-NN across 6 plans (SHIPPED 2026-05-25)
- [x] **Phase 48: WASM Runtime + WebAudioBackend** — `flow-lang` ships under .NET 10 Mono-WASM via `FlowTarget=Web`; real `WebAudioBackend` (JSImport `AudioContext` + `AudioBufferSourceNode`); frozen 5-export `flow-runtime.js` ES module API for Phase 49; 3.07 MB Brotli monolithic bundle. 15 REQs across 7 plans; HUMAN-UAT Firefox-audible confirmed (SHIPPED 2026-06-05)
- [x] **Phase 49: flowlang.dev Site** — greenfield SvelteKit 2 / Svelte 5 / Tailwind v4 site on Cloudflare Pages (Home/Docs/Playground/Showcase) in a skeuomorphic visual system, playground consuming the frozen Phase 48 runtime. 9/9 plans built + green in CI (vitest 70/70, playwright 275/275, lhci ≥0.9 ×4, axe 0-critical); 24 REQ-SITE-* (20 automated-closed, 4 pending). EXECUTION COMPLETE — pending HUMAN-UAT + live CF deploy + GitHub OAuth gist (2026-06-05)

### Phase Details

Full per-phase details (goals, dependencies, success criteria, plan lists) are archived in `.planning/milestones/v1.5-ROADMAP.md`. Closure artifacts live under each `.planning/phases/<NN>-*/` directory (VERIFICATION.md / SUMMARY.md / HUMAN-UAT.md).

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
| 11. Audit Spike | v1.2 | 6/6 | Complete | 2026-04-19 |
| 12. Stability | v1.2 | 6/6 | Complete | 2026-04-19 |
| 13. Nyquist Validation Backfill | v1.2 | 5/5 | Complete | 2026-04-20 |
| 14. Composer DX Part 1 | v1.2 | 4/4 | Complete | 2026-04-20 |
| 15. Composer DX Part 2 | v1.2 | 7/7 | Complete | 2026-04-25 |
| 16. Tutorial Refresh | v1.2 | 5/5 | Complete | 2026-04-25 |
| 17. Flow Language Server | v1.2 | 8/8 | Complete (HUMAN-UAT deferred) | 2026-04-20 |
| 18. Foundation — Rational Duration Arithmetic | v1.3 | 2/2 | Complete | 2026-04-26 |
| 19. Tuplets & Arbitrary Fractional Durations | v1.3 | 5/5 | Complete | 2026-04-26 |
| 20. Cheap DEFER Closures + Multi-letter Enharmonic Edges | v1.3 | 4/4 | Complete | 2026-04-26 |
| 21. Pragma System + H-Alias | v1.3 | 3/3 | Complete   | 2026-05-01 |
| 22. Tier B/C Composer DX Bundle | v1.3 | 7/7 | Complete   | 2026-05-02 |
| 23. Microtonal Tuning (Wedge) | v1.3 | 5/5 | Complete   | 2026-05-04 |
| 24. Scale Linting (flow-lsp) | v1.3 | 6/6 | Complete   | 2026-05-04 |
| 25. Gaussian Humanize (LAST PRNG phase) | v1.3 | 5/5 | Complete   | 2026-05-04 |
| 26. Op Standardization (Prefix-Only) | v1.3 | 5/5 | Complete   | 2026-05-09 |
| 26.1. Symbols + Tuples + Dicts | v1.3 | 6/6 | Complete   | 2026-05-09 |
| 26.2. Music Type Ergonomics + FX Overloads | v1.3 | 6/6 | Complete   | 2026-05-10 |
| 27. Tutorial + Showcase Refresh | v1.3 | 5/5 | Complete    | 2026-05-10 |
| 28. MIDI + Audio Polyphony & Articulation Rewrite | v1.4 | 7/7 | Complete   | 2026-05-10 |
| 29. Instrument Realism | v1.4 | 7/7 | Complete | 2026-05-12 |
| 30. Flow CLI + Formal Install | v1.4 | 9/9 | Complete   | 2026-05-11 |
| 31. LSP Enhancements + JetBrains Stretch | v1.4 | 9/9 | Complete | 2026-05-12 |
| 32. Full Scala (.scl) Tuning Loader | v1.4 | 7/7 | Complete    | 2026-05-15 |
| 33. SFZ Orchestral Sampler | v1.4 | 7/7 | Complete    | 2026-05-16 |
| 34. Symphony Showcase (v1.4 closer) | v1.4 | 6/6 | Complete | 2026-05-16 |
| 35. Language Foundation | v1.5 | 7/7 | Complete | 2026-05-19 |
| 36. Sequence Algebra & Generative | v1.5 | 12/12 | Complete | 2026-05-22 |
| 37. Sound Design + Sampler Polish | v1.5 | 7/7 | Complete    | 2026-05-23 |
| 38. Live Coding 2.0 | v1.5 | 7/7 | Complete    | 2026-05-24 |
| 39. Notation Citizenship | v1.5 | 5/5 | Complete    | 2026-05-23 |
| 40. Studio Sync | v1.5 | 4/3 | Complete    | 2026-06-07 |
| 41. Reach + v1.5 Closer | v1.5 | 7/7 | Complete (HUMAN-UAT deferred) | 2026-06-08 |
| 42. Type System & Stdlib Audit | v1.5 | 4/4 | Complete    | 2026-05-24 |
| 43. Module Names & Qualified Imports | v1.5 | 5/5 | Complete    | 2026-05-24 |
| 44. Strict Mode | v1.5 | 12/12 | Complete    | 2026-05-25 |
| 45. Beat Literal Syntax & True-to-Sig Pragma | v1.5 | 6/6 | Complete    | 2026-05-30 |
| 46. Codebase Bloat Removal | v1.5 | 6/6 | Complete    | 2026-05-30 |
| 47. Compile-Target Flavors | v1.5 | 6/6 | Complete | 2026-05-25 |
| 48. WASM Runtime + WebAudioBackend | v1.5 | 7/7 | Complete    | 2026-06-05 |
| 49. flowlang.dev SvelteKit + Playground | v1.5 | 9/9 | Built — pending HUMAN-UAT + live deploy (NOT shipped) |  |

_Per-phase v1.5 detail (Phases 35–49) archived in `.planning/milestones/v1.5-ROADMAP.md`._
