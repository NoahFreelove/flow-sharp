# Roadmap: Flow Language

## Milestones

- ~~**v1.0 MVP**~~ — Phases 1-5 (shipped 2026-04-03)
- ✅ **v1.1 Polish & Foundations** — Phases 6-10 (shipped 2026-04-18) — see `milestones/v1.1-ROADMAP.md`
- ✅ **v1.2 Stability & Composer DX** — Phases 11-17 (shipped 2026-04-26) — see `milestones/v1.2-ROADMAP.md`
- ✅ **v1.3 Composer DX Tier B/C** — Phases 18-27 (with 26.1 + 26.2 inserted, shipped 2026-05-10)
- ✅ **v1.4 Audio Fidelity, Distribution & Public Showcase** — Phases 28-34 (shipped 2026-05-16) — runtime-fidelity rewrite (per-voice polyphony, articulation system, richer instrument timbres), distribution wedge (`flow` CLI + formal install + MIDI↔Flow conversion), LSP polish + JetBrains plugin scaffolding, full Scala (`.scl`) microtonal loader, full SFZ orchestral sampler, and the curated symphony showcase ("In Five Voices") + ragtime companion ("Stride & Stomp") as the milestone closer (pre-public → public pivot). Release: [v1.4.0](https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0)
- 🚧 **v1.5 Stage, Studio, Web** — Phases 35-44 (started 2026-05-17) — citizenship + reach milestone: pattern matching + Rust-style diagnostics + pure-Flow test framework (Phase 35), Tidal-style sequence algebra + generative primitives + improv API (Phase 36), granular synthesis + time-stretch/pitch-shift + stereo pan + sampler polish (Phase 37), `live { ... }` block + modernized watch + REPL polish + audio input + OSC (Phase 38), MusicXML/LilyPond export + ABC/MML import (Phase 39), real-time MIDI + clock + Link + JACK transport sync (Phase 40), WASM playground + cross-platform binaries + `flow doc` + JetBrains Marketplace publish + third-genre showcase (Phase 41), type system + stdlib audit (Phase 42), module names + qualified imports (Phase 43), `enable strict;` mode (Phase 44). Phases 42-44 added 2026-05-24 to address stdlib growth pressure (collisions, dead-end types, charitable-default escape hatch). 66 requirements across 7 original phases + Phase 42-44 TBD at plan-phase.

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
- [ ] **Phase 38: Live Coding 2.0** — `live <quantize> { ... }` block + modernized watch mode + REPL polish (LSP-backed completion, `?fn` help, multiline + history search, piano-roll preview) + audio input via PA_STREAM_RECORD + OSC server/client (Rug.Osc 1.2.5)
- [x] **Phase 39: Notation Citizenship** — MusicXML 3.1 partwise export (MuseScore reference consumer per D-v1.5-08), LilyPond text emit, ABC 2.1 + abc2midi import, MML PC-98 common-core import (completed 2026-05-23)
- [ ] **Phase 40: Studio Sync** — IMidiBackend abstraction mirroring IAudioBackend (RtMidi.Core 1.0.53 for ALSA-seq + CoreMIDI + WinMM), MIDI clock master + slave (24 PPQN), Ableton Link (license-gated per D-v1.5-04), JACK transport (Linux opt-in)
- [ ] **Phase 41: Reach + v1.5 Closer** — WASM playground (Mono-WASM jiterpreter, ≤15 MB compressed), cross-platform binaries (linux-x64/arm64, osx-x64/arm64, win-x64), WASAPI + CoreAudio backends, `flow doc` generator with example execution, JetBrains Marketplace publish, third-genre showcase (jazz/EDM/death metal)

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

- [ ] 38-03-PLAN.md — State preservation across reload: Voice.Name + DiffByVoiceName + LambdaCaptureAuditor + PRNG reseed at swap (LIVE-03, LIVE-02)
- [ ] 38-06-PLAN.md — OSC: Rug.Osc + oscSend/oscListen/oscStop/oscBundle/oscSendBundle + charitable type-tag inference (OSC-01..02)

**Wave 4** *(blocked on Wave 3 completion)*

- [ ] 38-07-PLAN.md — Closer (5 examples + 4 paired tests + 38-VERIFICATION + ROADMAP/STATE/REQUIREMENTS/CLAUDE sweep)

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
**Requirements**: WASM-01, WASM-02, WASM-03, WASAPI-01, COREAUDIO-01, BIN-01, DOC-01, DOC-02, JET-01, SHOWCASE-01
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
| 38. Live Coding 2.0 | v1.5 | 4/7 | In Progress|  |
| 39. Notation Citizenship | v1.5 | 0/0 | Not started | - |
| 40. Studio Sync | v1.5 | 0/0 | Not started | - |
| 41. Reach + v1.5 Closer | v1.5 | 0/0 | Not started | - |
| 42. Type System & Stdlib Audit | v1.5 | 0/0 | Not started | - |
| 43. Module Names & Qualified Imports | v1.5 | 0/0 | Not started | - |
| 44. Strict Mode | v1.5 | 0/0 | Not started | - |

### Phase 42: Type System & Stdlib Audit

**Goal**: Graphify-driven sweep of the FlowType ↔ builtin-signature graph to surface orphaned types, missing conversions, asymmetric pairs (e.g., `Beat` arithmetic exists but no `Beat → Second` at tempo context), and dead-end builtins (the historical "Decibel type exists but no function accepts it" pattern). Produces prioritized `AUDIT.md` gap list that feeds Phases 43 + 44. Cheapest of the v1.5 closeout trio — runs first because strict mode (Phase 44) needs every clamp/courtesy/advisory site inventoried up front, and module naming (Phase 43) benefits from knowing which stdlib functions collide today.
**Depends on**: None. Informs Phases 43 + 44.
**Requirements**: TBD (defined at plan-phase)
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd-plan-phase 42 to break down)

### Phase 43: Module Names & Qualified Imports

**Goal**: Address growing stdlib name-collision pressure (already feeling it with `gain` vs `volume`; `math.sin` vs other `sin` is the imminent case) by introducing file-level module declarations (e.g., `module math` at top of `.flow` files) and qualified access (`math.sin`). Unqualified-by-default with explicit qualification as the escape hatch — ergonomics-first per `feedback_ergonomics_priority`, composers shouldn't have to type `math.sin` for everything. Existing `use "@x"` import mechanism extends to register module names; collisions across imported modules resolved by qualified-access fallback.
**Depends on**: Phase 42 (audit informs which stdlib functions need namespace separation first — likely candidates: math/audio/harmony/transforms).
**Requirements**: TBD (defined at plan-phase)
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd-plan-phase 43 to break down)

### Phase 44: Strict Mode

**Goal**: Opt-in "don't be like JavaScript" mode for composers writing reliable Flow code (test fixtures, shared snippets, large pieces). `enable strict;` file pragma (matches `enable justIntonation;` precedent — file-scoped, no stdlib propagation; stdlib stays charitable so strict files can still call it). Two axes of strictness, both at the input perimeter — internal algorithm behavior (PSOLA, HPS, phase vocoder, voice allocation, Markov internals) unchanged:

  - **Axis A — No type coercions.** `OverloadResolver`'s convertible (+100) tier is disabled in strict files; only exact (+1000) and compatible (+500) match. `(gain buf -12.0)` → error in strict; `(gain buf -12dB)` required. `(reverb buf 2.5)` → error; `(reverb buf 2.5s)` required. `(add 1 2.5)` → error; `(add (float 1) 2.5)` required. Requires new explicit-conversion builtins: `(db x)`, `(hz x)`, `(ms x)`, `(sec x)`, `(cents x)`, `(semitones x)` — verify `(float x)`/`(int x)`/`(double x)`/`(long x)` coverage at planning time.
  - **Axis B — Input-domain clamps become errors.** Every `Math.Clamp` + courtesy-fallback site in stdlib gets `if (ctx.StrictMode) throw; else clamp+advisory`. Examples: `markov order=5` → error (not clamp-to-3), `lsystem iterations=25` → error (not clamp-to-20), `cellular width=2048` → error (not clamp-to-1024), `granular windowing=#unknown` → error (not Hann fallback), `[tuning] unmapped MIDI key` → error (not rest+advisory), `seq_length>100` SFZ → error (not clamp+WarnOnce), `[abc]` unknown ornament → error (not drop+advisory).
  - **Truthy + stringy strictness.** `if` requires Bool, `print` requires String, `(equals 1 1.0)` returns `false` in strict (no numeric coercion); cross-type equality blocked.

**Pre-strict bug fix bundled in this phase**: `print` is registered today with signature `[StringType.Instance]` (`flow-lang/StandardLibrary/BuiltInFunctions.cs:150-154`), so `(print 42)` already fails overload resolution — contradicts ergonomics-first philosophy. Phase 44 fixes non-strict `print`/`if` to be charitable (auto-str / truthy-coerce), then strict re-tightens them. Composers get the right default in both modes.

**Tension flag**: Axis B contradicts `feedback_charitable_interpretation` head-on. Resolved by file-scoped opt-in — charitable behavior remains the default for all non-strict files (including the entire stdlib). Phase plan must preserve charitable behavior as the default everywhere; strict is purely an additive switch.

**Depends on**: Phase 42 (audit provides the clamp/advisory site inventory needed to confidently enumerate Axis B sites — missing any one regresses the strict contract). Phase 43 optional but useful for organizing strict-mode test files.
**Requirements**: TBD (defined at plan-phase)
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd-plan-phase 44 to break down)
