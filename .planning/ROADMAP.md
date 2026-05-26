# Roadmap: Flow Language

## Milestones

- ~~**v1.0 MVP**~~ — Phases 1-5 (shipped 2026-04-03)
- ✅ **v1.1 Polish & Foundations** — Phases 6-10 (shipped 2026-04-18) — see `milestones/v1.1-ROADMAP.md`
- ✅ **v1.2 Stability & Composer DX** — Phases 11-17 (shipped 2026-04-26) — see `milestones/v1.2-ROADMAP.md`
- ✅ **v1.3 Composer DX Tier B/C** — Phases 18-27 (with 26.1 + 26.2 inserted, shipped 2026-05-10)
- ✅ **v1.4 Audio Fidelity, Distribution & Public Showcase** — Phases 28-34 (shipped 2026-05-16) — runtime-fidelity rewrite (per-voice polyphony, articulation system, richer instrument timbres), distribution wedge (`flow` CLI + formal install + MIDI↔Flow conversion), LSP polish + JetBrains plugin scaffolding, full Scala (`.scl`) microtonal loader, full SFZ orchestral sampler, and the curated symphony showcase ("In Five Voices") + ragtime companion ("Stride & Stomp") as the milestone closer (pre-public → public pivot). Release: [v1.4.0](https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0)
- 🚧 **v1.5 Stage, Studio, Web** — Phases 35-49 (started 2026-05-17) — citizenship + reach milestone: pattern matching + Rust-style diagnostics + pure-Flow test framework (Phase 35), Tidal-style sequence algebra + generative primitives + improv API (Phase 36), granular synthesis + time-stretch/pitch-shift + stereo pan + sampler polish (Phase 37), `live { ... }` block + modernized watch + REPL polish + audio input + OSC (Phase 38), MusicXML/LilyPond export + ABC/MML import (Phase 39), real-time MIDI + clock + Link + JACK transport sync (Phase 40), cross-platform binaries + `flow doc` + JetBrains Marketplace publish + third-genre showcase (Phase 41 — WASM playground bullet carved out to Phase 47-49), type system + stdlib audit (Phase 42), module names + qualified imports (Phase 43), `enable strict;` mode (Phase 44), Beat literal `Nb` + `enable beat-true-to-sig;` pragma (Phase 45), codebase bloat removal (Phase 46), compile-target flavors `FlowTarget=Desktop|Web` (Phase 47), WASM runtime + WebAudioBackend (Phase 48), flowlang.dev SvelteKit site + skeuomorphic playground (Phase 49). Phases 42-44 added 2026-05-24 to address stdlib growth pressure; Phase 45 added 2026-05-24 as follow-up to Phase 43; Phase 46 added 2026-05-24 as cleanup pass. Phases 47-49 added 2026-05-25 to carve Phase 41's WASM playground bullet into a full distribution + reach track: compile-target flavors strip incompatible features from Web builds (SFZ, live-coding, OSC, mic-in, native audio backends, MIDI hardware, REPL), WASM runtime gets a new `WebAudioBackend` per [research finding #2](.planning/research/) (offline-render → AudioBuffer pattern, NOT AudioWorklet — no .NET-in-WASM prior art for AudioWorklet driving), flowlang.dev becomes a SvelteKit site on Cloudflare Pages with skeuomorphic visual design + docs synced from `wiki/` + GitHub gist share. 66 v1.5 requirements across the 7 original phases (35-41) + 9 new REQ-AUDIT-NN added with Phase 42 closure on 2026-05-24 (75 total v1.5 requirements; Phase 43 + 44 + 45 + 46 + 47 + 48 + 49 requirements still TBD at their plan-phase). **Phase 42 SHIPPED 2026-05-24** — `42-AUDIT.md` deliverable feeds Phase 43 + Phase 44. **Phase 44 SHIPPED 2026-05-25**.

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

## v1.5 Stage, Studio, Web (Phases 35-41) — in progress

Citizenship + reach milestone over the already-shipped v1.4 base. Across 7 phases (35–41) Flow adds 23 picked features + 4 v1.4 carryovers + housekeeping to take Flow from "credible single-author public language" to "real citizen of the music-software world" alongside TidalCycles, Sonic Pi, Strudel, and SuperCollider — extending creative reach (live coding revamp, generative algebra, improv API), ecosystem interop (notation export, real-time MIDI, transport sync), and distribution (WASM playground, cross-platform binaries, docs generator). 66 requirements across 7 phases. Pre-traction no-deprecation latitude is ACTIVE — breaking changes ship in one commit with in-repo migrators.

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
- [ ] **Phase 40: Studio Sync** — IMidiBackend abstraction mirroring IAudioBackend (RtMidi.Core 1.0.53 for ALSA-seq + CoreMIDI + WinMM), MIDI clock master + slave (24 PPQN), Ableton Link (license-gated per D-v1.5-04), JACK transport (Linux opt-in)
- [ ] **Phase 41: Reach + v1.5 Closer** — cross-platform binaries (linux-x64/arm64, osx-x64/arm64, win-x64), WASAPI + CoreAudio backends, `flow doc` generator with example execution, JetBrains Marketplace publish, third-genre showcase (jazz/EDM/death metal). **WASM playground bullet superseded 2026-05-25 by Phases 47-49** (carved into compile-target flavors + WASM runtime + flowlang.dev site track).
- [x] **Phase 42: Type System & Stdlib Audit** — Reflective audit of FlowType ↔ FunctionSignature graph + clamp/advisory/charitable inventory + .flow caller cross-reference; ships `42-AUDIT.md` deliverable with 7 gap-class sections + 53 routing tags (→ Phase 43 module/naming, → Phase 44 strict-mode Axis B sites, → v1.6-backlog); anchor finding: `BeatType` is the sole coercible orphan. **Zero production code touched — read-only audit phase** (invariant gate-enforced via empty production diff). Closed 9 REQ-AUDIT-NN across 4 plans; 26/26 Phase 42 fixtures GREEN. (completed 2026-05-24)
- [x] **Phase 43: Module Names & Qualified Imports** — file-level `module math` declaration + qualified `math.sin` access; depends on Phase 42 AUDIT.md §1/§2/§5a routing (completed 2026-05-24)
- [x] **Phase 44: Strict Mode** — `enable strict;` file pragma; Axis A type-coercion rejection + Axis B input-perimeter clamp errors + Bool-if/String-print discipline; depends on Phase 42 AUDIT.md §2 explicit-conversion-builtin shapes + §6a 13 input-perimeter clamps + §6b 117 advisory sites (completed 2026-05-25)

### Phase Details

### Phase 35: Language Foundation

**Goal**: Pattern matching, multi-line diagnostics, a pure-Flow test framework, and `-> as name` chain naming all land — unblocking every later phase. Composer can write `(match seq | Cmaj7 => "I" | Dm7 => "ii" | _ => "other")`, see Rust-style multi-line error diagnostics with source-quoted spans, write `(test "name" body)` blocks that run via `flow test`, and name intermediate values mid-chain with `seq -> (transpose 2) as melody -> render`. Phase also closes v1.4 housekeeping carryover.
**Depends on**: Nothing (first v1.5 phase — dependency root per D-v1.5-10). Span migration / diagnostics renderer runs first within the phase, then test framework, then pattern matching, then `-> as name`.
**Requirements**: LANG-01, LANG-02, LANG-03, LANG-04, TEST-01, TEST-02, HK-01, HK-02, HK-03, HK-04
**Success Criteria** (what must be TRUE):

  1. Composer can write a `match` expression with literal / constructor / wildcard / guard arms and music-aware extractors (chord quality `Cmaj7`, roman numeral `V7`, articulation `#staccato`); non-exhaustive matches WARN to stderr and fall through to `Void` (charitable interpretation per D-v1.5-05); `enable matchExhaustive;` pragma promotes to errors. (LANG-01, LANG-02)
  2. Composer can write `seq -> (transpose 2) as melody -> (legato 0.5) as legato-melody -> render` and reference `melody` / `legato-melody` as intermediate-value bindings without breaking the chain. (LANG-03)
  3. Parse errors and runtime type mismatches render as Rust-style multi-line diagnostics — source-quoted span, caret pointer, label, secondary notes, and "did you mean?" Levenshtein suggestions for unknown identifiers in scope. (LANG-04)
  4. Composer can write `tests/test_foo.flow` containing `(test "name" body)` blocks with `(assert ...)`, `(assertEq a b)`, `(assertNotesMatch seqA seqB)`, `(assertBytesEqual buf1 buf2)`, and `(assertWithinDb buf1 buf2 0.5dB)` primitives; `flow test [path]` discovers and runs them with hermetic isolation (musical context stack + voice pool + PRNG state + ExecutionContext bindings reset between tests in a single FlowEngine process). (TEST-01, TEST-02)
  5. v1.4 housekeeping cleared: humanizeGaussian voice-block bug fixed (HK-01); Phase 17 HUMAN-UAT rows 1-3 closed (HK-02); Phase 04 VERIFICATION.md gaps closed (HK-03); CLAUDE.md "Public as of v1.4" footnote rewritten to match the post-public deprecation framing (HK-04).

**Plans**: 7 plans

- [x] 35-01-PLAN.md — Span migration foundation (LANG-04 prereq)
- [x] 35-02-PLAN.md — v1.4 housekeeping closeout (HK-01..04)
- [x] 35-03-PLAN.md — Rust-style diagnostics renderer (LANG-04)
- [x] 35-04-PLAN.md — Pure-Flow test framework + flow test CLI (TEST-01, TEST-02)
- [x] 35-05-PLAN.md — Pattern matching foundation (LANG-01)
- [x] 35-06-PLAN.md — Music-aware pattern extractors + exhaustiveness (LANG-02)
- [x] 35-07-PLAN.md — -> as name chain naming (LANG-03)

### Phase 36: Sequence Algebra & Generative

**Goal**: Composer can write Tidal-style pattern algebra over `Sequence` values (12 combinators that compose via `->`), generate musical material from Markov chains / L-systems / cellular automata / Lorenz attractors as first-class stdlib primitives, parameterize sections with positional args, and improvise chord-aware Markov solos over a progression — all with deterministic seeding routed through the new PrngRegistry.
**Depends on**: Phase 35 (parameterized-section destructuring uses match patterns; D-v1.5-10). Phase 36 ↔ Phase 37 are commutative; milestone orders 36 first per PROJECT.md.
**Requirements**: PAT-01, PAT-02, GEN-01, GEN-02, GEN-03, GEN-04, GEN-05, SECT-01, IMPROV-01
**Success Criteria** (what must be TRUE):

  1. Composer can chain `seq -> (every 4 (fast 2)) -> (sometimes rev) -> (jux (transpose 7))` and the 12 Tidal-style combinators (every/fast/slow/chunk/phase/rev/jux/sometimes/often/rarely/degrade/superimpose) all compose cleanly via existing `->`. Failures (zero-length sequence, divide-by-zero rate) charitably interpreted with stderr advisory. (PAT-01, PAT-02)
  2. Composer can call `(markov corpus 2 16 seed)`, `(lsystem #A {<<#A, <<#A #B>>>>} 4)`, `(cellular 30 16 32 seed)` / `(life 16 16 32 seed)`, `(lorenz 10.0 28.0 2.667 200 seed)` / `(logistic 3.9 200 seed)` and get a deterministic `Sequence` (or `Array[Double]` for chaos maps quantized via `(quantizeToScale series scale)`). (GEN-01, GEN-02, GEN-03, GEN-04)
  3. All GEN-* primitives route their RNGs through `Runtime/PrngRegistry` keyed by `(SourceLocation, generator-name)`. Two consecutive runs at the same git SHA produce byte-identical WAV+MIDI output (two-run cmp-clean determinism contract per Phase 18/25/27/33 inheritance). Lorenz cross-platform FP divergence documented as platform-specific limitation; same-platform two-run cmp-clean preserved. (GEN-05)
  4. Composer can write `section verse(Note root, Int repeats) { ... }` and call it as `[verse(C4, 2) verse(G4, 1) chorus]` — section args bind in a synthetic stack frame on call, closure over outer musical context preserved. Existing zero-arg `section verse { ... }` form unchanged. (SECT-01)
  5. Composer can call `(jam over=chords style=#bebop length=8bars seed=N)` and get a chord-aware melodic Sequence respecting chord tones on strong beats and scale tones on weak beats; deterministic when `seed` provided; `#jazz`, `#blues`, `#classical` baseline rule packs ship. (IMPROV-01)

**Plans**: 12 plans (all complete 2026-05-22)

- [x] 36-01-PLAN.md — PrngRegistry foundation + render-boundary hooks + two-run determinism harness (GEN-05) — `164483d` / `5a234f1` / `bca3dec`
- [x] 36-02-PLAN.md — Universal named-argument syntax (lexer + parser + OverloadResolver) — D-36-11 foundation, no backfill yet (PAT-01) — `9f415c6` / `22508a9` / `e332462` / `4fc0854`
- [x] 36-03-PLAN.md — Named-arg backfill across BuiltInFunctions.cs + Collections + Bars + DictFunctions (PAT-01) — `3e5de25` / `3df3a68` / `cc8b8ac`
- [x] 36-04-PLAN.md — Named-arg backfill across Audio + DSP + Transforms + Composition + Harmony + TestFramework (PAT-01) — `a74827f` / `efcf8e5` / `90e2353`
- [x] 36-05-PLAN.md — @patterns stdlib: 13 Tidal combinators (every/fast/slow/chunk/phase/rev/jux/sometimes/iter/palindrome/degrade/superimpose + sparseSeq) (PAT-01, PAT-02, GEN-05) — `a0f9882` / `4ddbf86` / `c823c83`
- [x] 36-06-PLAN.md — Markov primitives + MarkovModel reference-identity type (GEN-01, GEN-05) — `3628c64` / `89bd359` / `2a9067a`
- [x] 36-07-PLAN.md — L-system primitives with Symbol alphabet + LsystemModel reference-identity type (GEN-02, GEN-05) — `28091f1` / `e4b93ba` / `3bac210`
- [x] 36-08-PLAN.md — Cellular automata: 1D elementary CA + 2D Game of Life (GEN-03, GEN-05) — `6ea3f7f` / `292585c` / `c1c3a32` / `8478f11`
- [x] 36-09-PLAN.md — Chaos maps: Lorenz + logistic + quantizeToScale (GEN-04, GEN-05) — `57b0633` / `f96b5b2` / `061f2ab` / `f77e66a`
- [x] 36-10-PLAN.md — Parameterized sections + section overload via Phase 35 pattern dispatch + Rust-style diagnostics (SECT-01) — `e935991` / `d0ddfb9` / `ac07132` / `c02aa12`
- [x] 36-11-PLAN.md — jam builtin + StyleRegistry + 3 baseline rule packs (jazz/blues/classical) (IMPROV-01, GEN-05) — `4e8957d` / `1291b87` / `f9dc75f`
- [x] 36-12-PLAN.md — Composer-facing examples + Phase 36 VERIFICATION + ROADMAP/STATE/REQUIREMENTS closure (all 9 reqs) — `727b3ea` + closure commit

### Phase 37: Sound Design + Sampler Polish

**Goal**: Closes 4 v1.4 carryovers (stereo pan polish, SFZ round-robin, sampled drums, more flute samples, warmer piano) plus ships granular synthesis + independent time-stretch + pitch-shift as first-class native-citizen builtins composable with the existing DSP rack. Largest phase of v1.5 — plans may subdivide per PROJECT.md note.
**Depends on**: Phase 35 (test framework needed for RMS-windowed regression coverage). Phase 37 ↔ Phase 36 commutative. **Pre-plan audit required at CONTEXT spawn** (D-v1.5-09): confirm whether per-voice stereo pan in synth-path is shipped (PROJECT.md says v1.0 Phase 2) — likely scope is SFZ-renderer-only retrofit. Audit `Audio/SongRenderer.cs` + `Audio/Sfz/SfzRenderer.cs`.
**Requirements**: DSP-01, DSP-02, DSP-03, MIX-01, MIX-02, SAMP-01, SAMP-02, SAMP-03, PIANO-01, FLUTE-01, DRUM-01
**Success Criteria** (what must be TRUE):

  1. Composer can call `(granular buf grain=50ms density=20Hz jitter=0.3 windowing=#hann)` and get a granular texture Buffer composable with existing reverb/gain/pan/filter; Hann (default), Gaussian, and Tukey windowing supported; jitter PRNG routed through PrngRegistry per D-v1.5-06. (DSP-01)
  2. Composer can call `(stretch buf 2.0 mode=#auto)` to time-stretch without pitch change and `(pitchShift buf +5st mode=#auto)` to pitch-shift without time change; `#vocoder` (phase vocoder for harmonic), `#psola` (for percussive), `#auto` (HPS transient detector picks per-frame). Existing `loadWav` varispeed call sites unaffected. Hand-rolled per D-v1.5-03 (RubberBand rejected). (DSP-02, DSP-03)
  3. Every voice has a per-voice `Pan` attribute (range -1.0 to +1.0) applied via constant-power law (`left = cos((pan+1)*π/4)`, `right = sin((pan+1)*π/4)`) before the SongRenderer additive-mix stage; SFZ sampled instruments respect the same pan attribute via SfzRenderer stereo retrofit. (MIX-01, MIX-02)
  4. SFZ round-robin opcodes (`seq_position` / `seq_length`) parsed; equal-power velocity-layer crossfade via `xfin_lovel`/`xfin_hivel`/`xfout_*` opcodes; per-articulation envelope multipliers (multiplicative stack on top of Phase 28 locked rules) close the "staccato sampled path sounds thinner than synth path" gap. Round-robin index deterministic across runs (seeded from voice ordinal, not wall-clock). (SAMP-01, SAMP-02, SAMP-03)
  5. Ragtime UAT iteration #2 follow-ups close: warmer piano timbre + ≥4 VSCO velocity layers per pitch point (PIANO-01); ≥1 additional flute sample point between G4 and G5 (likely D5 or A4) closing the D5 timbre-crossover gap (FLUTE-01); sampled drums via SampledInstrumentRenderer with transient-preserving pitch shift (PSOLA for transients, vocoder for sustain — same `#auto` hierarchy as DSP-02/03). (DRUM-01)
  6. Two-run cmp-clean determinism contract preserved for non-`live` paths; RMS-windowed regression (±0.5 dB / 100ms per SPEC-8) holds for behavior changes that legitimately move bytes (e.g. new pan applied to existing voices, sample-path envelope multipliers).

**Plans**: 7 plans

- [x] 37-01-PLAN.md — DSP foundation + granular synthesis (DSP-01); absorbs all shared utilities (WindowFunctions, Fft, Hps) + Wave 0 test scaffolds for the whole phase — `b724d33` / `818e539` / `0d44e9c`
- [x] 37-02-PLAN.md — Stretch + pitchShift (DSP-02, DSP-03); hand-rolled phase vocoder + PSOLA + Hps #auto dispatch — `db92da6` / `75d922a` / `3daffe4`
- [x] 37-03-PLAN.md — SFZ retrofit + MIX-01 verification (MIX-01, MIX-02, SAMP-01, SAMP-02, SAMP-03); round-robin + xfin/xfout + SAMP-03 multipliers + per-voice pan wire-up — `729cb4a` / `e985b83` / `add3e6a` / `b6ceaed` / `e40cd3e`
- [x] 37-04-PLAN.md — PIANO-01 sample asset expansion (4 velocity layers + synthesized mp + release= knob) — `af8395f` / `6560ee6` / `7f3ad4e`
- [x] 37-05-PLAN.md — FLUTE-01 A4 sample point (closes D5 timbre crossover) — `681908c` / `3686e19`
- [x] 37-06-PLAN.md — DRUM-01 via VSCO-CE GM-StylePerc.sfz + #auto pitch shift (depends on Plan 37-02) — `75878a0` / `7eaf410`
- [x] 37-07-PLAN.md — Closer (examples + 37-VERIFICATION.md + ROADMAP/STATE/REQUIREMENTS/CLAUDE.md sweep) — Wave 5 closer (this commit)

### Phase 38: Live Coding 2.0

**Goal**: Modernized live-coding surface — composer wraps a section in `live 1bar { ... }`, edits the file mid-playback, and the new content hot-swaps at the next bar boundary without re-initializing voices or destroying playback state. REPL gets LSP-backed completion + `?fn` inline help + multiline editing + history search + ASCII piano-roll preview. Audio input from mic/line-in composes with DSP pipeline. OSC server/client opens Flow to the network.
**Depends on**: Phase 35 (test framework + pattern matching). MUST precede Phase 41 — WASM playground IS watch-mode-in-browser. Sub-order: modernized watch + live block FIRST → REPL polish → audio input → OSC.
**Requirements**: LIVE-01, LIVE-02, LIVE-03, REPL-01, REPL-02, REPL-03, REPL-04, AUDIO-IN-01, AUDIO-IN-02, OSC-01, OSC-02
**Success Criteria** (what must be TRUE):

  1. Composer can wrap hot-swappable code in `live <quantize> { ... }` (default `1bar`); on file save the block re-evaluates and swaps at the next quantize-unit boundary with a 64-sample equal-power crossfade. Live blocks emit a stderr advisory on every entry explicitly opting OUT of the two-run cmp-clean determinism contract (D-v1.5-07). 30s wall-clock evaluation cap (CancellationToken); 200ms file-watch debounce. (LIVE-01, LIVE-02)
  2. Voice-pool state preserved across live reload IF voice name still exists post-edit; musical context stack resets to file-scope; PRNG state reseeded at swap boundary; stale-closure detection raises a clear advisory rather than silently misbehaving. (LIVE-03)
  3. REPL has Tab completion sourced from in-process `flow-lsp` (token-heuristic fallback on partial-parse failure), `?transpose` inline help printing signature + doc-comment + 1-line example from `BuiltInDocs` (104 entries from Phase 31), multi-line editing with paren-balanced continuation prompt, Ctrl+R history search backed by `~/.config/flow/history`, and ASCII piano-roll on `(inspect seq)`. (REPL-01, REPL-02, REPL-03, REPL-04)
  4. Composer can read mic/line input as a Buffer via `(micBuffer duration)` (PulseAudio `PA_STREAM_RECORD`, auto-attenuated 20 dB on open to prevent feedback) and compose it with existing `mix`/`play`/`writeWav`/`granular` builtins. Sample-rate conversion to 44.1 kHz at capture-side. (AUDIO-IN-01, AUDIO-IN-02)
  5. Composer can run an OSC server (`(oscListen port path handler)`) rate-limited to 200 Hz per path and send OSC messages (`(oscSend host port path arg1 arg2 ...)`) with explicit OSC 1.0 type-tag conventions (`,f`/`,d`/`,i`/`,s`). Uses Rug.Osc 1.2.5. (OSC-01, OSC-02)

**Plans**: 7 plans

Plans:
**Wave 1**

- [x] 38-01-PLAN.md — Modernized watch + ANSI status panel + 200ms debounce + 30s CancellationToken (LIVE-02)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 38-02-PLAN.md — live { quantize } block AST + Lexer/Parser/Interpreter + LiveBlockRegistry (LIVE-01)
- [x] 38-04-PLAN.md — REPL polish: PrettyPrompt + in-process LSP completion + :help fn + (inspect seq) alias + articulation glyphs (REPL-01..04)
- [x] 38-05-PLAN.md — Audio input: PulseAudioCaptureBackend + micBuffer + -20 dB attenuation + 44.1 kHz resample (AUDIO-IN-01..02)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 38-03-PLAN.md — State preservation across reload: Voice.Name + DiffByVoiceName + LambdaCaptureAuditor + PRNG reseed at swap (LIVE-03, LIVE-02)
- [x] 38-06-PLAN.md — OSC: Rug.Osc + oscSend/oscListen/oscStop/oscBundle/oscSendBundle + charitable type-tag inference (OSC-01..02)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 38-07-PLAN.md — Closer (5 examples + 4 paired tests + 38-VERIFICATION + ROADMAP/STATE/REQUIREMENTS/CLAUDE sweep) — `c5b7bad` (Task 1 examples + tests) / `d5e8fcb` (Task 2 VERIFICATION + HUMAN-UAT + REQUIREMENTS sweep) / Wave 4 closer commit (Task 4 tracking-file sweep)

**UI hint**: yes

### Phase 39: Notation Citizenship

**Goal**: Flow becomes a citizen of the music-notation ecosystem — composer exports to MusicXML (consumed by MuseScore, Finale, Sibelius, Dorico, LilyPond) and LilyPond (engraving-quality printed scores), and imports from ABC notation (folk / Irish trad corpus on thesession.org) and MML (PC-98-era chiptune common core).
**Depends on**: Phase 35 (pattern matching used in articulation emit). Phase 39 is otherwise standalone — any time after Phase 35. Sub-order: MusicXML → LilyPond → ABC → MML (defer MML to v1.6 first if cuts needed).
**Requirements**: XML-01, XML-02, LILY-01, ABC-01, ABC-02, MML-01
**Success Criteria** (what must be TRUE):

  1. Composer can call `(writeMusicXML song "piece.xml")` and the output opens correctly in MuseScore (reference consumer per D-v1.5-08) — notes + durations + key + timesig + tempo + Phase 28 articulations (Accent→`<accent/>`, Marcato→`<strong-accent/>`, Staccato→`<staccato/>`, Tenuto→`<tenuto/>`, Sforzando→`<dynamics><sfz/></dynamics>`, Legato→slur spans NOT per-note) + multi-part for multi-track songs + microtonal pitches via `<alter>` with cent precision or text annotation fallback. (XML-01)
  2. CI gate: `mscore --convert-to mxl` on exported MusicXML round-trips structurally (note count + durations + pitches + articulations preserved). One-way Flow → XML; import deferred to v1.6 per FEATURES.md anti-feature lock. (XML-02)
  3. Composer can call `(writeLilyPond song "piece.ly")` and the output compiles via `lilypond -dno-print-pages` without engraver errors — multi-voice notation via `\new Voice` contexts inside `<< { ... } \\ { ... } >>`, tuplet brackets via `\tuplet N/M {...}` (nested tuplets flattened for engraver compatibility), microtonal pitches as cent-offset comments alongside nearest 12-TET notation. (LILY-01)
  4. Composer can call `(abc "X:1\nT:Reel\nM:4/4\nK:Dmaj\n|: A2 d2 fedB |...")` and get a `Section` or `Sequence`; ABC 2.1 subset + abc2midi extensions (modal keys `Edor`/`Dmix`/`Aphr`/etc. parsed); multi-tune files (`X:1`, `X:2`, ...) return `Array[Section]`; unknown ornaments/headers dropped with `[abc]` stderr advisory (charitable interpretation). Vendored `matthewcpp/ABCSharp` source. (ABC-01, ABC-02)
  5. Composer can call `(mml "T120 L4 O4 cdefga>c")` and get a `Sequence`; PC-98-era common core supported (notes, accidentals `+`/`#`/`-`, octave `O<n>`/`>`/`<`, length `L<n>`, tempo `T<n>`, loops `[...]<n>`); dialect-specific FM/drum opcodes ignored with stderr advisory. (MML-01)

**Plans**: 5 plans (all complete 2026-05-23)

- [x] 39-01-PLAN.md — @notation-io module + MusicXML export + XML-02 round-trip gate + InstrumentRouting D-39-20 extraction (XML-01, XML-02) — `4a838b4`
- [x] 39-02-PLAN.md — LilyPond export with Dutch pitch convention + slur grouping + microtonal comments (LILY-01) — `dfd719f`
- [x] 39-03-PLAN.md — ABC import (ABC 2.1 subset + abc2midi `Q:` + modal keys + charitable advisories) (ABC-01, ABC-02) — `c196023`
- [x] 39-04-PLAN.md — MML import (PC-98 common core + loop expansion with depth/total caps) (MML-01) — `474595e`
- [x] 39-05-PLAN.md — Composer-facing examples + Phase 39 VERIFICATION + CLAUDE.md sweep — `c60a2db`

### Phase 40: Studio Sync

**Goal**: Flow joins the studio — real-time MIDI output to hardware synths and DAW VST tracks via new `IMidiBackend` abstraction parallel to `IAudioBackend`; MIDI clock master + slave (24 PPQN) for sync with drum machines and hardware sequencers; optional Ableton Link for cross-application LAN sync; optional JACK transport for Linux pro-audio composers. Ableton Link license review required at plan-start per D-v1.5-04.
**Depends on**: Phase 35 (pattern matching used in MIDI event dispatch `(match msg | (noteOn n v) => ... | (cc n v) => ...)`). MUST precede Phase 41 — Web MIDI is just another IMidiBackend implementation. Sub-order: IMidiBackend Linux first → MIDI clock master + slave → Ableton Link (license-gated) → JACK transport.
**Requirements**: MIDI-RT-01, MIDI-RT-02, MIDI-RT-03, MIDI-RT-04, CLOCK-01, CLOCK-02, LINK-01, LINK-02, JACK-01
**Success Criteria** (what must be TRUE):

  1. Composer can call `(listMidiPorts)`, `(openMidiOutput "Roland JV-1080")`, and `device.SendNoteOn(channel, pitch, velocity)` / `.SendNoteOff` / `.SendControlChange` / `.SendSysex(data)` against an external hardware synth via the new `IMidiBackend` abstraction (RtMidi.Core 1.0.53). ALSA-seq backend (Linux primary, MIDI-RT-02), CoreMIDI + WinMM backends enabled in Phase 41 cross-platform binary work (MIDI-RT-03). DryWetMidi 8.0.3 remains for offline MIDI file I/O (verified: NO Linux device support upstream — RtMidi.Core is the load-bearing replacement). (MIDI-RT-01, MIDI-RT-02, MIDI-RT-03)
  2. MIDI events emit at `audioBuffer.PlaybackStartTime + bufferOffset` (NOT at queue time) — sample-accurate sync with audio. Sysex on separate best-effort queue. Hot-plug failures: log + retry + quiet-drop (NEVER throw — would break long `live` sessions). (MIDI-RT-04)
  3. Composer can enable MIDI clock master mode (emit 24 PPQN clock + start/stop/continue tied to active `MusicalContext.Tempo`; tempo changes apply at next bar boundary) OR slave mode (receive 24 PPQN clock from external master and drive `MusicalContext.Tempo`; 8-pulse settle on master tempo change). Mode (master XOR slave) switchable only at bar boundary. (CLOCK-01, CLOCK-02)
  4. Ableton Link integration (license-gated per D-v1.5-04): peer-equal tempo sync via libabletonlink P/Invoke; Link tempo is render-time input for playback ONLY (`play` / `loop` / `preview`) — NEVER applied to `writeWav` / `writeMidi` (offline render preserves deterministic output). Peer-disappear: latch last-seen tempo (no mid-piece fallback). CI test: byte-identical `writeWav` with Link peer connected vs without. If GPLv2+/commercial license review surfaces a conflict with Flow's MIT distribution at Phase 40 plan-start, LINK-01 + LINK-02 defer to community contribution (PR welcome, not shipped from upstream in v1.5). (LINK-01, LINK-02)
  5. Composer can opt into JACK transport sync (Linux only) — `(jackSync)` builtin; transport position drives `MusicalContext.Tempo` + bar/beat; absence of JACK server does not affect non-JACK workflows. macOS / Windows: JACK theoretically available but not shipped/tested in v1.5. (JACK-01)

**Plans**: TBD

### Phase 41: Reach + v1.5 Closer

**Goal**: WASM playground brings Flow to the browser as a Strudel-tier live-coding experience; cross-platform self-contained binaries close the long-deferred v1.0 Phase 41 gap (Mac + Windows users can finally run Flow locally); `flow doc` makes the ~200-builtin surface learnable; JetBrains Marketplace publish closes the Phase 31 scaffolding stretch; third-genre showcase (jazz / EDM / death metal) validates Flow's genre-agnostic claim alongside v1.4's symphony + ragtime. Last by construction — consumes every other v1.5 phase's surface.
**Depends on**: Every other v1.5 phase. WASM playground requires Phase 38 `live` block (the browser experience IS watch-mode-in-browser); cross-platform binaries require new IAudioBackend impls; Web MIDI requires Phase 40's IMidiBackend abstraction. Sub-order: `flow doc` first (purely additive, no platform dependency) → WASM playground → cross-platform binaries → JetBrains publish → third-genre showcase last (consumes everything). WASM ships on Mono-WASM jiterpreter NOT NativeAOT-LLVM per D-v1.5-02 (FlowEngine's reflection-heavy `InternalFunctionRegistry` would require a source-generator pass — deferred to v1.6). OwnAudioSharp macOS smoke-test required at Plan 01; fall back to hand-rolled CoreAudio P/Invoke if miniaudio latency unacceptable (>20ms round-trip for live coding).
**Requirements**: ~~WASM-01, WASM-02, WASM-03~~ (carved to Phase 47-49 — 2026-05-25), WASAPI-01, COREAUDIO-01, BIN-01, DOC-01, DOC-02, JET-01, SHOWCASE-01
**Success Criteria** (what must be TRUE):

  1. WASM playground hosted at a public URL (`flow-lang.example.dev` or similar) — composer types Flow source in browser editor, clicks Play, hears audio via Web Audio API (`KristofferStrube.Blazor.WebAudio` wrapper). Pairs with Phase 38 `live` block as watch-mode-in-browser. Share-via-URL with URL-encoded source (limit 8 KB). Bundle size ≤15 MB compressed (measured at Plan 01 dry-run; if exceeded, prune stdlib subset — lazy-load `@sfz` / `@notation-emit` / `@osc` — or lazy-load Phase 29 sample bundle on first sampled-instrument use). (WASM-01, WASM-02, WASM-03)
  2. Cross-platform self-contained binaries published for linux-x64, linux-arm64, osx-x64, osx-arm64, win-x64 via `dotnet publish -p:PublishSingleFile=true` — released as v1.5.0 tarballs (Linux/macOS) + zip (Windows) alongside the existing flow-linux-x64.tar.gz. Mac + Windows users can `flow run script.flow` locally without cloning. NAudio.Wasapi 2.3.0 backs Windows (single scoped `WasapiBackend.cs`); OwnAudioSharp 1.0.68 backs macOS (smoke-test Plan 01, hand-rolled CoreAudio fallback). (WASAPI-01, COREAUDIO-01, BIN-01)
  3. `flow doc` generates browsable reference site (HTML + Markdown) from `///` doc-comments (new lexer grammar additive to `//`) + proc signatures + builtin metadata from BuiltInDocs (104 Phase 31 entries). Content-hash incremental cache for re-gen. Code examples in `///` doc-comments execute via the test framework (TEST-01 hermetic isolation) — failures surface as `[example failed]` annotations; runnable examples double as regression tests. (DOC-01, DOC-02)
  4. JetBrains plugin published to Marketplace from the Phase 31 scaffolding — plugin.xml metadata + build.gradle.kts signing config (JetBrains marketplace cert) + CHANGELOG.md + plugin verifier CI against IntelliJ Platform 2024.3+. Direct-download fallback page (`docs/jetbrains/install.html`) if marketplace review delays. (JET-01)
  5. Third-genre showcase piece (jazz / EDM / death metal, composer's choice) — ~60s curated piece in `examples/<genre>/<piece>.flow` consuming features from Phases 35-40 (at minimum: pattern matching, one generative primitive, granular DSP or time-stretch, live block, real-time MIDI playback via new IMidiBackend). README.md `## Showcase` v1.5 section embeds inline-audio. v1.5.0 GitHub Release ships the audio + cross-platform binaries. Genre choice validates Flow's genre-agnostic claim alongside v1.4's symphony + ragtime. (SHOWCASE-01)
  6. Two-run cmp-clean determinism contract preserved for offline-render paths (`writeWav` / `writeMidi`) across every new platform. RMS-windowed regression (±0.5 dB / 100ms per SPEC-8) holds for the third-genre showcase across WAV + MIDI output.

**Plans**: TBD
**UI hint**: yes

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
| 39. Notation Citizenship | v1.5 | 0/0 | Not started | - |
| 40. Studio Sync | v1.5 | 0/0 | Not started | - |
| 41. Reach + v1.5 Closer | v1.5 | 0/0 | Not started | - |
| 42. Type System & Stdlib Audit | v1.5 | 4/4 | Complete    | 2026-05-24 |
| 43. Module Names & Qualified Imports | v1.5 | 5/5 | Complete    | 2026-05-24 |
| 44. Strict Mode | v1.5 | 12/12 | Complete    | 2026-05-25 |
| 45. Beat Literal Syntax & True-to-Sig Pragma | v1.5 | 0/0 | Not started | - |
| 46. Codebase Bloat Removal | v1.5 | 0/0 | Not started | - |
| 47. Compile-Target Flavors | v1.5 | 6/6 | Complete | 2026-05-25 |
| 48. WASM Runtime + WebAudioBackend | v1.5 | 5/7 | In Progress|  |
| 49. flowlang.dev SvelteKit + Playground | v1.5 | 0/0 | Not started | - |

### Phase 42: Type System & Stdlib Audit — SHIPPED 2026-05-24

**Goal**: Graphify-driven sweep of the FlowType ↔ builtin-signature graph to surface orphaned types, missing conversions, asymmetric pairs (e.g., `Beat` arithmetic exists but no `Beat → Second` at tempo context), and dead-end builtins (the historical "Decibel type exists but no function accepts it" pattern). Produces prioritized `AUDIT.md` gap list that feeds Phases 43 + 44. Cheapest of the v1.5 closeout trio — runs first because strict mode (Phase 44) needs every clamp/courtesy/advisory site inventoried up front, and module naming (Phase 43) benefits from knowing which stdlib functions collide today.
**Depends on**: None. Informs Phases 43 + 44.
**Requirements**: REQ-AUDIT-01, REQ-AUDIT-02, REQ-AUDIT-03, REQ-AUDIT-04, REQ-AUDIT-05, REQ-AUDIT-06, REQ-AUDIT-07, REQ-AUDIT-08, REQ-AUDIT-09
**Deliverable**: `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` (277 lines, 9 sections, 53 routing tags — feeds Phase 43 module/naming + new builtins, Phase 44 strict-mode Axis B sites + explicit-conversion builtins, v1.6-backlog candidates)
**Plans:** 4/4 plans complete

Plans:

- [x] 42-01-PLAN.md — Reflective audit harness + xUnit self-check (REQ-AUDIT-01/02/03/06) — `3c74e70` / `e47f7b4`
- [x] 42-02-PLAN.md — Clamp/advisory grep extractor + .flow caller index (REQ-AUDIT-04/05/07) — `a0858f4` / `763a9fc`
- [x] 42-03-PLAN.md — AUDIT.md synthesis + composer review checkpoint (REQ-AUDIT-02/04/05/06/07/08/09) — `76972b4` / `2cca3fd` / `d512158`
- [x] 42-04-PLAN.md — Closer: VERIFICATION + ROADMAP/STATE/REQUIREMENTS sweep + full-suite gate (REQ-AUDIT-03/09)

**Cross-cutting constraints:**

- Existing flow-lang.Tests suite remains green — zero production code touched (invariant gate-enforced via `git diff --stat -- flow-lang/StandardLibrary/ flow-lang/TypeSystem/ "flow-lang/*.flow"` at every commit boundary; verified empty at closer time against both the Wave 1 spawn commit `c4cd738` and the Wave 3 base `82d83a8`)
- All Phase 42 fixtures (`AuditHarnessTests` 9 + `ClampGrepConsistencyTests` 6 + `AuditReportShapeTests` 11 = 26 facts) GREEN; pre-existing Phase 28/29/35/38 failures from spawn commit `c4cd738` remain pre-existing — Phase 42 introduces zero new failures (see `.planning/phases/42-type-system-stdlib-audit/deferred-items.md`)

### Phase 43: Module Names & Qualified Imports

**Goal**: Address growing stdlib name-collision pressure (already feeling it with `gain` vs `volume`; `math.sin` vs other `sin` is the imminent case) by introducing file-level module declarations (e.g., `module math` at top of `.flow` files) and qualified access (`math.sin`). Unqualified-by-default with explicit qualification as the escape hatch — ergonomics-first per `feedback_ergonomics_priority`, composers shouldn't have to type `math.sin` for everything. Existing `use "@x"` import mechanism extends to register module names; collisions across imported modules resolved by qualified-access fallback.
**Depends on**: Phase 42 (audit informs which stdlib functions need namespace separation first — likely candidates: math/audio/harmony/transforms).
**Requirements**: REQ-MOD-01, REQ-MOD-02, REQ-MOD-03, REQ-MOD-04, REQ-MOD-05, REQ-MOD-06, REQ-MOD-07, REQ-MOD-08, REQ-MOD-09, REQ-MOD-10, REQ-MOD-11, REQ-MOD-12
**Plans:** 5/5 plans complete

Plans:

**Wave 1**

- [x] 43-01-PLAN.md — Lexer/Parser/AST module-declaration surface (REQ-MOD-01)
- [x] 43-02-PLAN.md — ModuleRegistry + ExecutionContext property (REQ-MOD-02)
- [x] 43-04-PLAN.md — Beat backfill (beatToSec/secToBeat + delay/renderBarAtBeat Beat overloads) + Phase 42 audit polarity flip (REQ-MOD-07/08/09/10/12, D-10)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 43-03-PLAN.md — ModuleLoader hook + dispatcher + collision/shadow advisories (REQ-MOD-02/03/04/05/11)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 43-05-PLAN.md — 12-file stdlib migration + final regression bar + tracking sweep (REQ-MOD-06/09/11/12)

### Phase 44: Strict Mode

**Goal**: Opt-in "don't be like JavaScript" mode for composers writing reliable Flow code (test fixtures, shared snippets, large pieces). `enable strict;` file pragma (matches `enable justIntonation;` precedent — file-scoped, no stdlib propagation; stdlib stays charitable so strict files can still call it). Two axes of strictness, both at the input perimeter — internal algorithm behavior (PSOLA, HPS, phase vocoder, voice allocation, Markov internals) unchanged:

  - **Axis A — No type coercions.** `OverloadResolver`'s convertible (+100) tier is disabled in strict files; only exact (+1000) and compatible (+500) match. `(gain buf -12.0)` → error in strict; `(gain buf -12dB)` required. `(reverb buf 2.5)` → error; `(reverb buf 2.5s)` required. `(add 1 2.5)` → error; `(add (float 1) 2.5)` required. Requires new explicit-conversion builtins: `(db x)`, `(hz x)`, `(ms x)`, `(sec x)`, `(cents x)`, `(semitones x)` — verify `(float x)`/`(int x)`/`(double x)`/`(long x)` coverage at planning time.
  - **Axis B — Input-domain clamps become errors.** Every `Math.Clamp` + courtesy-fallback site in stdlib gets `if (ctx.StrictMode) throw; else clamp+advisory`. Examples: `markov order=5` → error (not clamp-to-3), `lsystem iterations=25` → error (not clamp-to-20), `cellular width=2048` → error (not clamp-to-1024), `granular windowing=#unknown` → error (not Hann fallback), `[tuning] unmapped MIDI key` → error (not rest+advisory), `seq_length>100` SFZ → error (not clamp+WarnOnce), `[abc]` unknown ornament → error (not drop+advisory).
  - **Truthy + stringy strictness.** `if` requires Bool, `print` requires String, `(equals 1 1.0)` returns `false` in strict (no numeric coercion); cross-type equality blocked.

**Pre-strict bug fix bundled in this phase**: `print` is registered today with signature `[StringType.Instance]` (`flow-lang/StandardLibrary/BuiltInFunctions.cs:150-154`), so `(print 42)` already fails overload resolution — contradicts ergonomics-first philosophy. Phase 44 fixes non-strict `print`/`if` to be charitable (auto-str / truthy-coerce), then strict re-tightens them. Composers get the right default in both modes.

**Tension flag**: Axis B contradicts `feedback_charitable_interpretation` head-on. Resolved by file-scoped opt-in — charitable behavior remains the default for all non-strict files (including the entire stdlib). Phase plan must preserve charitable behavior as the default everywhere; strict is purely an additive switch.

**Depends on**: Phase 42 (audit provides the clamp/advisory site inventory needed to confidently enumerate Axis B sites — missing any one regresses the strict contract). Phase 43 optional but useful for organizing strict-mode test files.
**Requirements**: REQ-STRICT-01, REQ-STRICT-02, REQ-STRICT-03, REQ-STRICT-04, REQ-STRICT-05, REQ-STRICT-06, REQ-STRICT-07, REQ-STRICT-08, REQ-STRICT-09, REQ-STRICT-10, REQ-STRICT-11, REQ-STRICT-12, REQ-STRICT-13, REQ-STRICT-14, REQ-STRICT-15
**Plans:** 12/12 plans complete

Plans:

**Wave 1**

- [x] 44-00-PLAN.md — Wave 0 test infrastructure + strict-error-manifest.csv (~126 in-scope rows + 5 carve-outs) + grep extractor + Phase44 Category trait
- [x] 44-01-PLAN.md — PragmaRegistry + ExecutionContext.StrictMode + ApplyStrictPragma + ModuleLoader per-imported-file push/restore (REQ-STRICT-01, REQ-STRICT-02)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 44-02-PLAN.md — ProcDeclaration.IsStrict AST capture + Interpreter push/pop + ExpressionEvaluator CallerStrictMode snapshot (REQ-STRICT-02, REQ-STRICT-03)
- [x] 44-04-PLAN.md — 6 forward + 24 reverse explicit-conversion builtins (REQ-STRICT-05, REQ-STRICT-06)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 44-03-PLAN.md — OverloadResolver Axis A tier-disable (BOTH Pitfall 1 clauses dropped) (REQ-STRICT-04)
- [x] 44-05-PLAN.md — 13 §6a input-perimeter clamp sites flip to [strict] errors + Phase44ClampGrepConsistencyTests (REQ-STRICT-07)
- [x] 44-08-PLAN.md — Pre-strict bug fix: Void-wildcard print/if/not + AutoStr + (not) builtin registration + D-12 non-strict (and)/(or) last-truthy semantics (REQ-STRICT-10)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 44-06-PLAN.md — Axis B HIGH-priority advisory rewrites (~50 sites: SFZ + Patterns + Render + Match + DSP) (REQ-STRICT-08)
- [x] 44-09-PLAN.md — Axis C strict: Bool-required (and)/(or) + cross-type comparison errors + (equals 1 1.0) returns false strict + D-13 Dict regression-pin (REQ-STRICT-09, REQ-STRICT-11)
- [x] 44-10-PLAN.md — REPL :strict on/off sticky meta-command + LiveBlockStrictTests (REQ-STRICT-12, REQ-STRICT-13)

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 44-07-PLAN.md — Axis B MED+LOW advisory rewrites (~65 sites: Generative + Improv + Notation + OSC + Tuning + Harmony + InputFunctions + MidiExport) + CarveOutsPreservedTests (REQ-STRICT-08)

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 44-11-PLAN.md — tests/strict/ positive .flow suite + showcase_strict.flow + Phase44TwoRunDeterminismTests + StrictFlowScriptSuiteTests (REQ-STRICT-14, REQ-STRICT-15)

### Phase 45: Beat Literal Syntax & True-to-Sig Pragma

**Goal**: Close the Beat-ergonomics gap left by Phase 43. Phase 42 audit correctly identified `BeatType` as the sole coercible orphan; Phase 43 added 4 Beat-typed builtins (`beatToSec` / `secToBeat` / `delay(Buffer, Beat)` / `renderBarAtBeat(Bar, Beat)`) but composers still can't write Beat values ergonomically because `(beat 0.5)` is wordier than `0.5b` and there's no surface syntax matching the rest of the music-type family (every other music type has a literal — see CLAUDE.md music-types table). This phase finishes what Phase 43 started.

  - **`Nb` literal syntax** (lowercase `b`; `B` reserved to avoid `dB`/Decibel ambiguity). Defaults: `1b = 1 quarter note = 60/bpm seconds`, matching MIDI/DAW convention. No conflict with note `B` (which requires octave `B4`) or NoteValue letters `q/h/w/e/s` (none use `b`). Lexer follows existing `Nms`/`Ns`/`Nc`/`Nst`/`NdB`/`NHz` precedent.
  - **`enable beat-true-to-sig;` file pragma**, opt-in, file-scoped, last-wins semantics matching the `enable justIntonation;` / `pythagorean;` / `equalTemperament;` family. When active, `Nb` literals AND `(beat N)` constructor calls multiply by `4.0/denominator` at evaluation time, reading active `MusicalContext.TimeSignature`. So in `timesig 6/8 { }` with the pragma on: `1b = 1 eighth`; in `timesig 2/2 { }`: `1b = 1 half`. Default `4/4` context unchanged. Gives composers the musician-intuition path for non-quarter time signatures (jigs, cut time, irregular meters) without breaking existing tempo/BPM/MIDI semantics.
  - **Pragma affects literal CONSTRUCTION only.** Beat values stored are always quarters internally — the eval-time multiplier resolves to a quarter-relative double before the `Value.Beat` is constructed. All Phase 43 builtins, the 8 `secondsPerBeat = 60.0/bpm` sites (SongRenderer / VoiceAllocator / Timeline / PlaybackFunctions / MidiExport / SynthUtils / etc.), MIDI `microsPerBeat`, and `Voice.OffsetBeats` remain unchanged. Pure parse/eval-time desugar.
  - **Cross-file consistency.** Beat values that flow from a `beat-true-to-sig` file to a non-pragma file retain their pre-converted quarter value — semantically consistent because internal storage is always quarters regardless of file.

**Implementation surface**: Lexer `Nb` token + Parser BeatLiteral expression + ExpressionEvaluator context-lookup at BeatLiteral / `(beat N)` evaluation sites + ModuleLoader pragma registration → `ExecutionContext.BeatTrueToSig` flag + tutorial in `examples/beat/` (6/8 jig with/without pragma) + CLAUDE.md music-types table update.

**Depends on**: Phase 43 (Beat builtins shipped). Independent of Phase 44 (Strict Mode) — ordered after 44 only because 44 planning is active in a parallel session and we want to avoid contention. Phase 45 could in principle execute in parallel with Phase 44 once 44's plans are locked.
**Requirements**: REQ-BEAT-LEX-01..04, REQ-BEAT-AST-01..04, REQ-BEAT-PRAGMA-01..04, REQ-BEAT-PRAGMA-HYPHEN-01, REQ-BEAT-CONSTRUCTOR-01..02, REQ-BEAT-TEST-01..07, REQ-BEAT-DOC-01..04
**Plans:** 6 plans

Plans:

**Wave 1**

- [ ] 45-01-PLAN.md — Lexer foundation: TokenType.BeatLiteral + signed/unsigned suffix branches + PragmaScanner hyphen gap closure (REQ-BEAT-LEX-01..04, REQ-BEAT-PRAGMA-HYPHEN-01)

**Wave 2** *(blocked on Wave 1 completion)*

- [ ] 45-02-PLAN.md — BeatLiteralExpression AST + Parser arm + literal-token-set (REQ-BEAT-AST-01..03)
- [ ] 45-03-PLAN.md — PragmaRegistry entry + ExecutionContext.BeatTrueToSig + FlowEngine.ApplyBeatTrueToSigPragma + ModuleLoader push/pop (REQ-BEAT-PRAGMA-01..04)

**Wave 3** *(blocked on Wave 2 completion)*

- [ ] 45-04-PLAN.md — EvaluateBeatLiteral switch arm + multiplier formula + 3 composer .flow smokes (REQ-BEAT-AST-04, REQ-BEAT-TEST-01..03)
- [ ] 45-05-PLAN.md — (beat N) constructor migration to RegisterContextDependent + DICT-01 regression (REQ-BEAT-CONSTRUCTOR-01..02)

**Wave 4** *(blocked on Wave 3 completion)*

- [ ] 45-06-PLAN.md — Cross-file pair + tutorials + audio baselines + CLAUDE.md + REQUIREMENTS.md / ROADMAP.md / STATE.md sweep + 45-VERIFICATION.md (REQ-BEAT-TEST-04..07, REQ-BEAT-DOC-01..04)

### Phase 46: Codebase Bloat Removal

**Goal**: Pay down accumulated cruft from 40+ phases of organic growth before v1.5 closes. Acts on the read-only audit deliverable at `.planning/research/CODEBASE-BLOAT-AUDIT-2026-05-24.md` (general-purpose agent sweep, 2026-05-24). Audit found ~1,100 LOC removable upper-bound across 7 deletable files + several secondary items, with **zero high-confidence false-positives** (anti-findings section explicitly preserves intentional patterns: per-synth delegation shells, hand-rolled DSP, music-type singletons, Pidgin reference, flow-lang/flow-interpreter split, charitable fallbacks, CC-BY 4.0 sample assets).

**High-priority targets** (locked, low-risk):

  1. **NoteSynthesizer.cs:24-182 deduplication** — ~80 LOC of private `BeatsToSeconds` + `CreateSilence` + oscillator loops that already exist in `SynthUtils`. Every other synth uses `SynthUtils` correctly; the primitive synths kept private copies. Pure delete-and-redirect.
  2. **`Fixtures/` + `fixtures/` directory case-collision** — VERIFIED both exist on disk as distinct directories. Silent macOS APFS / Windows NTFS breakage waiting to happen; 6 C# test files reference each casing. Merge urgently (highest-priority risk reduction item).
  3. **Track/Timeline/DAW-multitrack stack removal** — `Timeline.cs` (265 LOC) + `Track.cs` + 11 wrapper procs in `composition.flow`, consumed by exactly one test (`tests/test_full_song.flow`). Superseded by Song/Section in Phase 28. ~380 LOC removable.
  4. **TimelineMap editor-highlighting plumbing** — ~250 LOC across parallel `RenderSongWithTimeline` overload + matching paths in BarRenderer/SequenceRenderer/SongRenderer. Zero callers in flow-lsp/flow-interpreter/flow-cli/tests. **Decision required at plan-phase**: confirm with flow-lsp roadmap (does v1.6 LSP plan to consume it?) — if yes, keep; otherwise remove.
  5. **audio.flow quadruple-declared oscillator wrappers** — `createSineTone`/`createSawTone`/`createSquareTone`/`createTriangleTone` each declared 4× (internal proc forward-decl + Flow proc body, both ×2 for Hertz overload). C# decls are dead weight because the Flow wrappers always intercept. Collapse to single declaration each.

**Secondary targets** (address opportunistically, plan-phase decides ordering):

  - `bars.flow` legacy API — zero composer usage post note-streams
  - `preview` builtin — registered but unused
  - `exportWav` legacy alias paralleling canonical `writeWav`
  - Pre-Phase-35 `test.flow` assertion legacy half (one test uses it)
  - `ProgressionExpression`/`ProgressionCompiler` — one composer call site, no unit tests (decision needed: add tests or remove)
  - 2× Phase35 diagnostics `.txt` baselines (orphaned)

**Anti-scope** (DO NOT TOUCH — audit-preserved intentional patterns):

  - Per-synth delegation shells (≤25-line by design per Phase 29 "Sample-based tonal instruments")
  - Hand-rolled DSP (Fft/WindowFunctions/Psola/PhaseVocoder/Hps/PitchShiftEngine — NWaves/RubberBand rejected deliberately)
  - Charitable-interpretation fallbacks (core philosophy per `feedback_charitable_interpretation`)
  - Music-type singletons (DecibelType/MillisecondType/CentType boilerplate — documented tradeoff at `CentType.cs:24-27`)
  - Pidgin package reference (intentional unused per CLAUDE.md)
  - `flow-lang` ↔ `flow-interpreter` project split
  - `flow-lang/Samples/` data assets (CC-BY 4.0 U-Iowa MIS bundle)

**Quantification** (per audit upper-bound): ~1,100 LOC removable, 7 files deletable (Timeline.cs, Track.cs, TimelineMap.cs, bars.flow, Bars.cs, 2 diagnostics baselines), zero stale TODO/FIXME/HACK markers.

**Acceptance**: Full test suite (`flow-lang.Tests` + every `tests/test_*.flow` script + Phase 28 RMS-windowed baselines + two-run cmp-clean determinism contract) remains green post-cleanup. No behavior changes — pure removal of dead/duplicate code. Cleanup commits stay atomic per target to allow selective revert if a removal regresses something subtle.

**Depends on**: None — pure cleanup. Independent of Phases 44 + 45 currently in flight; could parallelize with either once their plans lock. Bundled as one phase (vs scattered `/gsd:quick` tasks) because targets share regression-risk surface (synthesizers, song rendering, test infrastructure) and one atomic test-suite-green gate per cleanup is more economical than running it 12+ times.
**Requirements**: TBD (defined at plan-phase)
**Plans:** 0 plans

Plans:

- [ ] TBD (run /gsd-plan-phase 46 to break down)

### Phase 47: Compile-Target Flavors — SHIPPED 2026-05-25

**Goal**: Introduce `FlowTarget=Desktop|Web` msbuild conditioning so the flow-lang library can compile cleanly under WASM by stripping features that cannot run in a browser sandbox (native P/Invoke backends, file system watchers, raw UDP sockets, REPL, large sample assets). Foundation for Phase 48 — without target flavors, the WASM build cannot link.

**Strip-list for `FlowTarget=Web`** (locked 2026-05-25):

  1. **Native audio backends** — `Audio/PulseAudioSimpleBackend.cs`, `Audio/PulseAudioCaptureBackend.cs`, `Audio/CoreAudioBackend.cs` (P/Invoke targets unavailable in browser). Replaced by new `Audio/WebAudioBackend.cs` (Phase 48 implements; Phase 47 ships only the file scaffolding + `IAudioBackend.IsAvailable()` probing via `OperatingSystem.IsBrowser()`).
  2. **SFZ orchestral sampler** — `StandardLibrary/Audio/Sfz/**`, `sfz.flow` stdlib, `@sfz` opt-in module. Reason: external sample dependency, 100s of MB potential, browser sandboxing.
  3. **U-Iowa MIS sample bundle** — `flow-lang/Samples/**` (3.05 MB / 21 WAVs / 44.1 kHz 16-bit mono). Phase 29 sampled tonal instruments (piano/brass/sax/strings/flute/bell) fall back to synthesis-only on Web target (existing synthesis paths are byte-identical; `SampledInstrumentRenderer` short-circuits when sample bundle is absent).
  4. **Live coding** — `live { }` block AST + `Runtime/LiveBlockRegistry.cs` + `Interpreter/LambdaCaptureAuditor.cs` + `flow-interpreter/LiveStatusPanel.cs` + `LiveReloadManager`. Reason: requires `FileSystemWatcher` which doesn't exist in browser. Composers who write `live { }` blocks in Web-target code get a parse error pointing at the line + advisory "live blocks require Desktop target — open in `flow run` locally".
  5. **Audio input** — `Audio/PulseAudioCaptureBackend.cs` + `StandardLibrary/Audio/InputFunctions.cs` + `(micBuffer)` builtin. Reason: PulseAudio P/Invoke. v1.6 backlog: optional `getUserMedia` integration as a Web-only path.
  6. **OSC server/client** — `StandardLibrary/Network/Osc/**` + `osc.flow` + `@osc` opt-in module + Rug.Osc reference. Reason: raw UDP sockets unavailable in browser; WebRTC DataChannel is a different model and v1.6 backlog.
  7. **MIDI hardware I/O** (Phase 40 IMidiBackend impls) — RtMidi.Core ALSA-seq/CoreMIDI/WinMM stripped from Web build. Reason: hardware port access. v1.6 backlog: WebMIDI as a new `IMidiBackend` impl in a Web-only `WebMidiBackend.cs`.
  8. **REPL + `flow watch` + `flow test` + `flow doc` CLIs** — `flow-interpreter/` project has no Web target; web build is library-only. Reason: not language features, just don't ship those CLI entry points on Web.

**What stays in Web target** (≈85% of language surface): full core language (lexer, parser, AST, evaluator, type system, pattern matching, sections, lambdas, all music types), all synthesis (sine/saw/square/triangle/noise + drums/organ/wavetable), all DSP (reverb, lowpass, highpass, bandpass, compress, delay, gain, volume, granular, stretch, pitchShift), Phase 36 patterns/generative/improv stdlibs, MusicXML/LilyPond/ABC/MML export (as browser downloads via Blob URL), MIDI file write via DryWetMidi (pending WASM-compatibility verification at Plan 47-04).

**Implementation surface**: New top-level `<FlowTarget>` MSBuild property in `flow-lang/flow-lang.csproj` (default `Desktop`); conditional `<DefineConstants>$(DefineConstants);FLOW_WEB</DefineConstants>` for `FlowTarget=Web`; conditional `<Compile Remove="..." />` for strip-list files; `<None Remove="Samples/**" />` to skip embedded resources; `#if !FLOW_WEB` guards on `RegisterSfz` / `RegisterOsc` / `RegisterMicInput` / `RegisterLiveBlock` calls in `BuiltInFunctions.cs` `RegisterAll`; new `WebAudioBackend.cs` stub class with `IsAvailable() => OperatingSystem.IsBrowser()` returning false until Phase 48 wires the JSImport; `flow-lang.Tests.csproj` runs Desktop-only by default with a new opt-in `FlowTarget=Web` test pass gating against compile-only smoke (no audio assertions — Phase 48 owns those).

**Acceptance**: `dotnet build flow-lang -p:FlowTarget=Web` succeeds with no errors. Resulting assembly contains zero references to `libpulse-simple`/`AudioToolbox`/`Rug.Osc`/`FileSystemWatcher`/`RtMidi.Core` (verified via `ildasm` or `Mono.Cecil` reference scan in the test). `dotnet build flow-lang -p:FlowTarget=Desktop` (default) preserves byte-identical behavior for every existing test under the v1.5 two-run cmp-clean determinism contract (Phase 28 RMS baselines + 287/287 test fixtures + every `tests/test_*.flow` script). Web-target assembly size measured + recorded as Plan 47-05 baseline for Phase 48 budget tracking.

**Depends on**: Nothing (Phase 47 is a pure refactor of build-time conditioning). Foundation for Phase 48 (which needs the FLOW_WEB define to ship its WebAudioBackend implementation). Phase 47 ↔ Phase 49 are commutative; we order 47 first because Phase 48 depends on it and Phase 49 consumes Phase 48.
**Requirements**: REQ-WEB-TARGET-01, REQ-WEB-TARGET-02, REQ-WEB-TARGET-03, REQ-WEB-TARGET-04, REQ-WEB-TARGET-05, REQ-WEB-TARGET-06, REQ-WEB-TARGET-07, REQ-WEB-TARGET-08, REQ-WEB-TARGET-09, REQ-WEB-TARGET-10
**Plans:** 6/6 plans complete

Plans:

**Wave 1**

- [x] 47-01-PLAN.md — MSBuild conditioning foundation (FlowTarget property + strip list + BuildConditioningSmokeTests) — `635cbda` / `883c894`

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 47-02-PLAN.md — WebAudioBackend stub + AudioPlaybackManager Web-first probe (D-47-05..07) — `7021d8a` / `156dbd4` / `ba4d3fb`

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 47-03-PLAN.md — Central #if !FLOW_WEB guards + FlowEngine.IsWebTarget/SupportsLiveBlocks + Parser/ModuleLoader gates (D-47-08..10) — `dfa359f` / `9600ddb` / `905b819` / `d0b8b11` / `8f6b814`

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 47-04-PLAN.md — FlowTargetFactAttribute + DryWetMidi WASM-compat smoke + Web-side guard tests (D-47-04/13) — `8adc89c` / `f51e58d` / `92b022a` / `a3d8537`

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 47-05-PLAN.md — AssemblyReferenceScanTests via Mono.Cecil 0.11.5 (D-47-14) — `5c6129c` / `25b40ea`

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 47-06-PLAN.md — Closer: 47-VERIFICATION.md + ROADMAP/STATE/REQUIREMENTS/CLAUDE.md sweep + 18-file test-project Web build closer — `4ce8074` (VERIFICATION) + closer commits (this plan)

### Phase 48: WASM Runtime + WebAudioBackend

**Goal**: Build flow-lang under .NET 10 Mono-WASM with `FlowTarget=Web`, ship a `WebAudioBackend` that pushes rendered audio through the browser's `AudioContext`, and produce a deployable JS bundle that Phase 49 consumes from its SvelteKit playground tab. Single biggest feasibility risk in the v1.5 milestone — research surfaced no .NET-in-WASM prior art for AudioWorklet driving, so v1 ships the conservative offline-render → `AudioBuffer` → `AudioBufferSourceNode` pattern.

**Locked architectural decisions** (per 2026-05-25 research pass, sources in `.planning/research/`):

  - **D-48-01: Offline-render path, not AudioWorklet** (research finding #1 + #2). `[JSImport]`/`[JSExport]` interop is main-thread only ([dotnet/runtime#85592](https://github.com/dotnet/runtime/issues/85592)); .NET code cannot run inside an `AudioWorkletProcessor`'s 3ms/128-frame budget. Pattern: render full song to `Float32Array` in .NET, marshal once via `[JSExport]`, hand to `AudioBufferSourceNode.start()`. Matches Flow's existing composition → WAV buffer → play pipeline exactly. Tradeoff: no real-time hot-swap (Phase 47 already strips `live { }` from Web — consistent). Pre-rendered playback acceptable for v1.
  - **D-48-02: AudioWorklet + SharedArrayBuffer ring-buffer is v1.6 stretch** (research finding #3). Requires COOP/COEP headers (Cloudflare Pages supports natively — Phase 49 wires headers). Pattern: .NET on a worker thread fills SAB ring, JS-side `AudioWorkletProcessor` (plain JS, not WASM-hosted .NET) reads from the SAB ring. Defer to v1.6 unless Phase 48 has slack at closer.
  - **D-48-03: Bundle target ≤15 MB compressed is optimistic-aim, not commitment** (research finding #4). .NET 9 framework bundle is ~2 MB Brotli'd after trimming; jiterpreter adds runtime memory cost not bundle size. Flow itself (interpreter + stdlib `.flow` files + DryWetMidi) is the swing factor. Phase 29 sample bundle (3.05 MB) is stripped per Phase 47 — already saves 20% of budget. Plan 48-01 dry-run measures actual size; if >15 MB, lazy-load `@notation-io` + `@patterns` + `@generative` + `@improv` stdlibs on demand from a separate JS bundle (composer code that `use "@x"` triggers a fetch).
  - **D-48-04: DryWetMidi WASM compatibility is a real risk** (research finding #5 implicit). DryWetMidi 8.0.3 targets .NET Standard 2.0 — should WASM-compile but needs verification. Plan 48-02 ships a 10-line smoke test that compiles DryWetMidi under FlowTarget=Web; if it fails, strip MIDI file write from Web (`writeMidi` becomes parse error pointing composer at Desktop target). v1.6 backlog: hand-rolled MIDI writer for Web.
  - **D-48-05: Autoplay policy + first-gesture pattern**. `AudioContext.resume()` must be called inside a user gesture (research finding #5b). Playground first-paint shows a "Run" button that, when clicked, both compiles+executes Flow code AND `AudioContext.resume()`s in the same gesture handler. No autoplay anywhere (matches Phase 49 D-49-01 "no autoplay" composer-UX decision).
  - **D-48-06: GC pauses are not a concern in offline render** (research finding #5a). Mono-WASM GC pauses are irrelevant when we render the entire song to a Float32Array in one shot — the render itself isn't real-time. Only relevant for the v1.6 SharedArrayBuffer streaming stretch.
  - **D-48-07: WebAudioBackend uses `[JSImport]` for `AudioContext` + `decodeAudioData` + buffer marshalling**. New file `flow-lang/Audio/WebAudioBackend.cs` (Phase 47 ships the stub; Phase 48 implements). `IsAvailable()` returns `OperatingSystem.IsBrowser()`. `PlayBuffer(AudioBuffer)` calls `[JSImport]`-bound JS that creates an `AudioBufferSourceNode`, copies the Float32Array via the JS interop boundary (one allocation per playback), and starts it. `Dispose()` revokes the node.
  - **D-48-08: JS-side glue at `flow-lang/wasm/flow-runtime.js`** (new file). Exports a tiny ES module API: `await loadFlowRuntime()` returns `{ run(source) → Promise<{wav?: Float32Array, midi?: Uint8Array, stdout: string, errors: string[]}>, play(wav) → AudioBufferSourceNode, stop()}`. Phase 49 consumes this directly. No framework lock-in.

**Implementation surface**: New `flow-lang.csproj` target — `<RuntimeIdentifier>browser-wasm</RuntimeIdentifier>` + `<WasmEnableJiterpreter>true</WasmEnableJiterpreter>` + `<WasmStripILAfterAOT>true</WasmStripILAfterAOT>` + `<InvariantGlobalization>true</InvariantGlobalization>` (no ICU bundle needed) + `<TrimMode>full</TrimMode>` with explicit `<TrimmerRootDescriptor>` for the reflection-heavy `InternalFunctionRegistry` (per D-v1.5-02 carryforward — registry is reflection-based, source-generator pass deferred to v1.6); `WebAudioBackend.cs` implementing `IAudioBackend` via `[JSImport]`/`[JSExport]`; `flow-runtime.js` glue + ES module exports; `flow-lang/wasm/index.html` for local smoke testing (NOT shipped — Phase 49 owns the real UI); CI gate measuring compressed bundle size at Plan 48-05 closer.

**Acceptance**: (1) `flow-lang.dll` + Mono-WASM artifacts build under FlowTarget=Web with zero errors; (2) `flow-runtime.js` API loads in Chrome 120+, Firefox 121+, Safari 17+; (3) `await runtime.run('(play (createSineTone 440Hz 1.0 0.5))')` produces audible 440 Hz tone via WebAudio (verified by hand on each browser at Plan 48-04 HUMAN-UAT); (4) `(writeMidi)` either produces a valid Float32Array→MIDI export OR fails with a clear "not available on Web target" parse-error per D-48-04 verification; (5) compressed bundle size ≤15 MB OR the closer documents what was stripped to make it fit; (6) two-run cmp-clean determinism contract preserved (same Flow source → byte-identical Float32Array output across runs at same SHA, browser-platform independent).

**Depends on**: Phase 47 (needs FlowTarget=Web build to exist). Phase 48 ↔ Phase 49 are commutative for SCAFFOLDING work (Phase 49 can build the SvelteKit shell + docs sync independently), but Phase 49's playground tab cannot wire to a real runtime until Phase 48 ships `flow-runtime.js`. Ordering: 48 fully completes before Phase 49 starts the playground-tab work; Phase 49 may run in parallel for non-playground work (marketing pages, docs, navigation, design system).
**Requirements**: REQ-WASM-BUILD-01..05 (build pipeline), REQ-WEBAUDIO-01..04 (backend), REQ-WASM-API-01..03 (JS glue), REQ-WASM-SIZE-01 (bundle budget), REQ-WASM-DET-01 (determinism), REQ-WASM-DRYWET-01 (DryWetMidi compat).
**Plans:** 4/7 plans executed

Plans:

- [x] 48-01-PLAN.md — WASM build pipeline foundation (csproj + trim-roots.xml + WasmBuildPipelineTests)
- [x] 48-02-PLAN.md — DryWetMidi WASM publish smoke + culture-invariant sweep (D-48-17 closed: DryWetMidi reference retained per Mono.Cecil scan of published flow-lang.dll; 3 ToUpper/ToLower sites converted to *Invariant; 4/4 new Facts PASS)
- [x] 48-03-PLAN.md — WebAudioBackend real implementation ([JSImport] + stereo promotion + 30s cap)
- [x] 48-04-PLAN.md — flow-runtime.js ES module + WasmEntry.cs [JSExport] + index.html dev harness
- [ ] 48-05-PLAN.md — Bundle size budget + two-run determinism pin
- [ ] 48-06-PLAN.md — HUMAN-UAT browser smoke (Chrome / Firefox / Safari) — autonomous:false
- [ ] 48-07-PLAN.md — Closer (VERIFICATION + Phase 49 handoff + planning-artifact flips)

### Phase 49: flowlang.dev Site

**Goal**: Ship a SvelteKit website on Cloudflare Pages that markets Flow, hosts its docs (synced from the existing `wiki/` directory), and houses an interactive playground tab consuming the Phase 48 WASM runtime. Skeuomorphic visual design (locked 2026-05-25 — composer wants the tactile music-software tradition: Logic Pro wood panels, Reason racks, GarageBand knobs — NOT generic AI-template glassmorphism). Distribution + reach milestone closer for v1.5 alongside Phase 41's cross-platform binaries.

**Locked design decisions** (2026-05-25 ideation session, sources in `.planning/explore/`):

  - **D-49-01: No autoplay anywhere**. Composer-UX call — autoplay is "annoying". Every audio play is user-gesture-initiated. Matches Phase 48 D-48-05 first-gesture pattern.
  - **D-49-02: Static code blocks site-wide, NOT inline playgrounds on every doc page**. Doc and marketing pages render Flow code as syntax-highlighted text with an "Open in playground" button that deep-links into the playground tab with the snippet pre-loaded. Reason: WASM runtime is too heavy to boot on every page; keeps marketing/docs pages snappy and only the playground tab loads the runtime.
  - **D-49-03: GitHub gist for share-links**. "Save to gist" button creates a real gist under the user's GitHub account via OAuth. Reason: offloads hosting/moderation/abuse to GitHub; gets composers a real artifact under their account. Tradeoff: requires GitHub OAuth, not anonymous. URL leaves the site (`gist.github.com/...`) but that's the price for zero-backend storage. v1.6 backlog: anonymous fallback via URL fragment encoding (`/play#code=...`) for users without GitHub.
  - **D-49-04: SvelteKit on Cloudflare Pages, deployed to a `*.pages.dev` URL first**. Domain `flowlang.dev` is taken by a different language (JVM-based, unrelated to Flow). Free CF Pages URL (`flow-music.pages.dev` or similar — Plan 49-01 picks the name) used at v1.5 ship; user may grab a real domain later. CF Pages chosen over GitHub Pages because it supports custom COOP/COEP headers natively (Phase 48 v1.6 stretch needs them; GH Pages doesn't without workarounds).
  - **D-49-05: Docs synced from `wiki/`**. 26 hand-written markdown files (`Language-Basics.md`, `Chords-and-Harmony.md`, `Playback-and-Export.md`, `Generative.md`, `Note-Streams.md`, `Dynamics-and-Expression.md`, `Visualization.md`, `Quick-Start.md`, `Song-Structure.md`, `String-Interpolation.md`, `Standard-Library.md`, `Tips-and-Tricks.md`, `Functions.md`, `Effects.md`, `Collections.md`, `Pattern-Transforms.md`, `Vocalization.md`, `Home.md`, `Voices-and-Tracks.md`, `Musical-Context.md`, `Examples.md`, `Chord-Progressions.md`, `Loops.md`, `Audio-and-Synthesis.md`, `Imports-and-Modules.md`, `Flow-Operator.md`) live as the GitHub wiki repo. Site build step pulls them at deploy time (git submodule OR `git clone --depth 1` in build script). Rejected: hand-rewriting docs from scratch — wiki already exists; rejected: `flow doc` generator — that's a Phase 41 deliverable and runs against builtins, not concept prose. v1.6 may unify the two.
  - **D-49-06: Skeuomorphic visual direction**. NOT generic AI-vibecoded glassmorphism. References: Logic Pro wood-panel rack-mount aesthetic, Reason rack views, GarageBand instrument knobs, vintage synth hardware (Moog, ARP, Sequential Circuits). Tactile materials — wood, brushed metal, paper, fabric grilles. Real-feeling drop shadows, embossed buttons, satin highlights. Discriminating choice — does not match modern dev-tool defaults (Rust docs, Bun docs, Astro docs), but reinforces Flow's music-software DNA. Visual design treatment locked at Plan 49-02; full design system + token list at the same plan.
  - **D-49-07: Top-level navigation** — Home / Docs / Playground / Showcase / GitHub. Five-tab structure. "Home" is marketing landing (Flow value prop + 3-5 hero examples with "Open in playground" CTAs + showcase reel). "Docs" is the wiki-synced documentation index + per-topic pages. "Playground" is the interactive editor + WASM runtime + share. "Showcase" is curated `.flow` pieces from `examples/` + `flow-lang/improv/styles/` + community submissions (v1.6) with embedded audio. "GitHub" is an external link to the repo.
  - **D-49-08: No autoplay on Home either** — hero examples render static syntax-highlighted code with a "Play in playground" button. The button deep-links to the playground tab with the example pre-loaded AND auto-clicks Run on arrival (one-gesture chain — counts as user gesture per D-48-05).
  - **D-49-09: Mobile-responsive but not mobile-first**. Composers are mostly on desktop. Mobile = read docs + browse showcase + see playground UI; mobile editing is best-effort, not a target. Showcase audio plays inline on mobile (user-gesture-initiated per D-49-01).
  - **D-49-10: Accessibility** — full keyboard navigation, screen-reader labels on every interactive element, prefers-reduced-motion respects the skeuomorphic animations (knobs etc. become flat).

**Site information architecture**:

  ```
  /                         → Home (marketing landing)
  /docs                     → Docs index (synced wiki TOC)
  /docs/[slug]              → Per-page docs (synced wiki page)
  /playground               → WASM playground (editor + console + audio out + share)
  /playground#code=BASE64   → Playground with pre-loaded snippet
  /showcase                 → Showcase gallery (curated pieces)
  /showcase/[slug]          → Showcase piece detail page (audio + source)
  ```

**Implementation surface**: New top-level `flow-site/` directory in the repo (sibling to `flow-lang/` + `flow-interpreter/` + `flow-jetbrains/` + `flow-cli/`). SvelteKit 2.x + Svelte 5 + TypeScript + Tailwind CSS (for skeuomorphic utility classes layered with custom CSS for materials/textures). `wiki/` synced via build-time `git submodule update --init --depth 1` OR `git clone` script in `flow-site/scripts/sync-wiki.sh`. Code highlighting via `shiki` with a custom Flow grammar (extends TextMate grammar from `flow-lsp/` Phase 17 work). Playground UI uses Monaco Editor (already mature; matches Flow's existing LSP surface conceptually). Audio output via Phase 48 `flow-runtime.js` ES module. GitHub gist OAuth via Cloudflare Workers (1 worker, `flow-site/workers/gist-auth.ts`, ≤50 LOC). Skeuomorphic design tokens at `flow-site/src/lib/design/tokens.css`. CI gate: Lighthouse score ≥90 on Performance/Accessibility/Best-Practices/SEO for /, /docs, /playground.

**Acceptance**: (1) `https://flow-music.pages.dev` (or chosen `*.pages.dev` name) serves the site; (2) every wiki page rendered as a doc page with working navigation between them; (3) playground tab loads Phase 48 WASM runtime + executes Flow code + plays audio via WebAudio + saves snippets to GitHub gists; (4) Home page hero examples have working "Open in playground" CTAs; (5) skeuomorphic visual design landed across the site, dark mode toggle works, prefers-reduced-motion respected; (6) Lighthouse ≥90 on all four axes for / + /docs + /playground; (7) mobile-responsive at 320px viewport width; (8) full keyboard-only nav.

**Depends on**: Phase 48 (for `flow-runtime.js`). Phase 49 may run in parallel with Phase 48 for non-playground work (Home, Docs, Showcase, design system, gist auth worker) — wire playground tab to live runtime only after Phase 48 closer. Phase 47 not directly required (Phase 48 absorbs it) but transitively needed.
**Requirements**: TBD (defined at plan-phase — anchor candidates: REQ-SITE-IA-01..05 for navigation + IA, REQ-SITE-DESIGN-01..04 for skeuomorphic visual system, REQ-SITE-DOCS-01..03 for wiki sync, REQ-SITE-PLAYGROUND-01..05 for editor + runtime integration, REQ-SITE-SHARE-01..02 for GitHub gist OAuth, REQ-SITE-A11Y-01..03 for accessibility, REQ-SITE-PERF-01 for Lighthouse, REQ-SITE-RESPONSIVE-01 for mobile).
**Plans:** 0 plans

Plans:

- [ ] TBD (run /gsd-plan-phase 49 to break down)
