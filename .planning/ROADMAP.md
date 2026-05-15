# Roadmap: Flow Language

## Milestones

- ~~**v1.0 MVP**~~ — Phases 1-5 (shipped 2026-04-03)
- ✅ **v1.1 Polish & Foundations** — Phases 6-10 (shipped 2026-04-18) — see `milestones/v1.1-ROADMAP.md`
- ✅ **v1.2 Stability & Composer DX** — Phases 11-17 (shipped 2026-04-26) — see `milestones/v1.2-ROADMAP.md`
- 🚧 **v1.3 Composer DX Tier B/C** — Phases 18-27 (with 26.1 + 26.2 inserted, in progress)
- 🚧 **v1.4 Audio Fidelity, Distribution & Public Showcase** — Phases 28-34 (in progress; Phase 28 shipped 2026-05-10) — runtime-fidelity rewrite (per-voice polyphony, articulation system, richer instrument timbres), distribution wedge (`flow` CLI + formal install + MIDI↔Flow conversion), LSP polish + VSCode marketplace publish + JetBrains stretch, full Scala (`.scl`) microtonal loader, full SFZ orchestral sampler, and a curated short symphony showcase as the milestone closer (pre-public → public pivot)

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

## v1.3 Composer DX Tier B/C (Phases 18-27) — in progress

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
- [ ] **Phase 26: Op Standardization (Prefix-Only)** — Eliminate infix `+ - * /`; add `(add)`/`(sub)`/`(mul)`/`(div)`/`(neg)`/`(concat)` builtins covering numeric widening chain; remove `BinaryExpression`/`BinaryOperator` AST nodes; migrate stdlib + ~70 .flow tests; foundation for Phase 26.1
- [x] **Phase 26.1: Symbols + Tuples + Dicts (INSERTED)** — Symbol primitive (`#foo`), Tuple type (`<<a, b, c>>` literal, `~>` unpack op, destructuring, `@N` indexing, per-position types), generic `Dict<K, V>` with hashable keys (Int/Long/Float/String/Symbol/Note/Chord/Tuple); dicts via `(dict K V ...)` + `(dictTuple <<K,V>> ...)` builtins (no literal syntax) — Shipped 2026-05-09 (Waves 0-5: ac3b926 + 35474ed + 6549116 + d628870 + daaa023 + closure)
- [x] **Phase 26.2: Music Type Ergonomics + FX Overloads (INSERTED)** — Music-type numeric compatibility completion (Ms/Sec/Hertz IsCompatibleWith Double|Float; Semitone stays Int-only); FX music-typed overloads (delay-Ms, compress/sidechain-Decibel-Ms, reverb-Second, lowpass/highpass/bandpass-Hertz, createXxxTone-Hertz family); new Hertz type with `800Hz`/`1.5kHz` literals; new `volume(Buffer, Double)` linear-multiplier function alongside `gain` (which stays dB-only); 2 pre-existing RED DecibelBeatNumericCompatFacts closed via Value.ConvertTo Double-arm + audio.flow gain(Decibel) forward decl — Shipped 2026-05-10 (Waves 0-5: 45b01fb + 4f92c24 + 28158cc + dfbfa1f + 6df301e + 86bdd15)
- [x] **Phase 27: Tutorial + Showcase Refresh** — examples/tutorial.flow + examples/showcase.flow exercise every v1.3 feature end-to-end (prefix-only arithmetic, symbols, tuples, dicts, tuplets, fractional durations, microtonal/scale-lint pragma documentation, DX-10..15, range/multi-letter enharmonics/negative slice, humanizeGaussian, Phase 26.2 volume/gain split + Hertz literals + Ms-FX overloads + Second-decay reverb); 2 companion files under examples/pragmas/ (h_alias.flow + microtonal_ji.flow); byte-identical determinism preserved (Phase 18/25 sentinels + new Phase 27 ByteIdenticalPragmaTests 4 facts); CLAUDE.md gains Music Types Quick Reference table — Shipped 2026-05-10 (Waves 1-5: 995ff67 + dbffbec + eadbd9f + e15c5be + ace6416)

### Phase Details

### Phase 18: Foundation — Rational Duration Arithmetic
**Goal**: Rational arithmetic primitive lands so all subsequent tuplet / fractional / delay-sync / quantize math is drift-free
**Depends on**: Nothing (first v1.3 phase)
**Requirements**: FRAC-01, FRAC-02
**Success Criteria** (what must be TRUE):
  1. A new `Fraction(int Num, int Denom)` value type exists in `flow-lang/TypeSystem/`, normalizes via GCD on construction, and supports `+ / × / == / <` without ever using `double` arithmetic (FRAC-01)
  2. Unit Facts pin canonical examples: `1/3 + 1/3 + 1/3 == 1`, `2/4 == 1/2`, `3/12 == 1/4` (FRAC-01)
  3. `MusicalNoteData` exposes optional `Fraction? DurationFraction` that overrides the existing `DurationValue` enum when set; null leaves the existing power-of-2 path unchanged (FRAC-02)
  4. All ~70 existing `.flow` test scripts continue to pass with byte-identical output; `tutorial.flow` + `showcase.flow` regression gate via `cmp` is clean (FRAC-02)
**Plans**: 2 plans
- [x] 18-01-PLAN.md — Ship Fraction rational-arithmetic primitive (FRAC-01) + 9 unit Facts; Fraction.cs at flow-lang/TypeSystem/ (helper, not a FlowType) — Shipped 2092f32
- [x] 18-02-PLAN.md — Wire Fraction? DurationFraction into MusicalNoteData via defaulted-parameter pattern + GetBeats branch (FRAC-02); 6 unit Facts + 4 byte-identical integration Facts (tutorial.flow + showcase.flow WAV+MIDI two-runner) — Shipped ba8534a

### Phase 19: Tuplets & Arbitrary Fractional Durations
**Goal**: Lead capability — composers can write triplets, quintuplets, septuplets, nested tuplets, and arbitrary fractional durations like `C4/12`, with correct WAV + MIDI output
**Depends on**: Phase 18 (rational arithmetic — binding pre-ordering #1)
**Requirements**: TUP-01, TUP-02, TUP-03, TUP-04, TUP-05, TUP-06, TUP-07, TUP-08
**Success Criteria** (what must be TRUE):
  1. Composer can write `| {3:2 C4 D4 E4}q |` and three notes render summing to one quarter note (TUP-01)
  2. Composer can write `{3 C4 D4 E4}q` shorthand and it is equivalent to `{3:2 C4 D4 E4}q` per music21 convention (TUP-02)
  3. Composer can nest tuplets like `| {3:2 C4 {3:2 D4 E4 F4}q G4}h |` and inner durations multiply through both ratios correctly (TUP-03)
  4. Composer can write `| C4/12 D4/12 E4/12 |` arbitrary-denominator fractional durations inside note streams; bar-fit validator accepts rational-fraction tuplet bars (TUP-04, TUP-05)
  5. MIDI export auto-elevates TPQN to `LCM(480, tuplet_denoms)` capped at 9600; `{7:8}` exports at TPQN=3360, `{11:13}` raises a clear cap error (TUP-06)
  6. `augment(tupletSeq)` doubles rational durations and `diminish(tupletSeq)` halves them; AUDIT-VERIFIED C5 marker re-validated against tuplet-aware sequences (TUP-07)
**Plans**: 5 plans
- [x] 19-01-PLAN.md — TupletElement AST + parser + compiler (TUP-01/02/03) — Shipped a7f94ef
- [x] 19-02-PLAN.md — Per-note `/N` + `/X:Y[suffix]` (TUP-04 + TUP-08) — Shipped 9aae23c
- [x] 19-03-PLAN.md — Bar-fit validator + charitable overflow (TUP-05) — Shipped 3679ab4
- [x] 19-04-PLAN.md — MIDI TPQN auto-elevation (TUP-06) — Shipped dbc6f30
- [x] 19-05-PLAN.md — TUP-07 augment/diminish + Phase 19 closure — Shipped e2cdbe5

### Phase 20: Cheap DEFER Closures + Multi-letter Enharmonic Edges
**Goal**: Three v1.2-deferred items land cleanly: range stdlib, slice negative-from-end indexing, and multi-letter enharmonic edges (DEFER-04 must precede DEFER-02/03 per binding pre-ordering #3)
**Depends on**: Nothing (independent of Phase 18/19; runs in parallel candidate)
**Requirements**: DEFER-01, DEFER-04, DEFER-05
**Success Criteria** (what must be TRUE):
  1. Composer can call `(range 0 5)` → `[0, 1, 2, 3, 4]` and `(range 0 10 2)` → `[0, 2, 4, 6, 8]`; negative step iterates backward (DEFER-01)
  2. `enharmonic(E4)` → `Fb4`, `enharmonic(F4)` → `E#4`, `enharmonic(B4)` → `Cb5`, `enharmonic(C4)` → `B#3` round-trip correctly for every chromatic note (DEFER-04)
  3. Composer can call `(slice [1, 2, 3, 4, 5] -3 5)` → `[3, 4, 5]` and `(slice [1, 2, 3, 4, 5] 0 -1)` → `[1, 2, 3, 4]` Python-style negative-from-end (DEFER-05)
  4. v1.2 silent two-sided clamp behavior is replaced by negative-from-end semantics; existing positive-index call sites continue to work; collision grep over `tests/` empty for `slice(.*, .*, -.*)` patterns (DEFER-05)
**Plans**: 4 plans
- [x] 20-01-PLAN.md — DEFER-01 range(Int, Int[, Int]) stdlib registration + 8 RangeTests Facts + test_range.flow (shipped d0d17db)
- [x] 20-02-PLAN.md — DEFER-04 multi-letter enharmonic edges (E↔Fb, F↔E#, B↔Cb, C↔B#) + Phase14 NoKey_NaturalEdgeRespells migration + 24 EnharmonicEdgesTests Facts + test_enharmonic_edges.flow (shipped d835336)
- [x] 20-03-PLAN.md — DEFER-05 slice negative-from-end Python normalization + 10 SliceNegativeTests Facts + test_slice_negative.flow (shipped edd20b1)
- [x] 20-04-PLAN.md — Closure (REQUIREMENTS/ROADMAP/STATE/VERIFICATION + 14-deferred-items DEFER-04/06 strikethrough + 12-deferred-items DEFER-01 strikethrough; FlowScriptData.cs:57 stale pin removal already absorbed in 20-01 d0d17db per Rule 3 deviation)

### Phase 21: Pragma System + H-Alias
**Goal**: File-scope `enable <pragma>;` infrastructure lands per Haskell precedent (D-02), unblocking H-as-B alias and (in later phases) microtonal tunings + scale linting
**Depends on**: Phase 20 (DEFER-04 multi-letter enharmonics — H♯ resolves through B# = C natural; binding pre-ordering #3)
**Requirements**: PRAG-01, PRAG-02, DEFER-02/03
**Success Criteria** (what must be TRUE):
  1. Composer can declare `enable <featureName>;` at top of `.flow` files only; pragmas after the first non-pragma statement raise a parse error; lexer pre-scan extracts pragmas before main lexing (PRAG-01)
  2. `PragmaRegistry` is a closed set — unknown pragma names raise a clear error citing the known list (PRAG-01)
  3. `use` imports do NOT propagate pragmas — importing a module that declares `enable hAsB;` does NOT enable `hAsB` in the importing file (PRAG-02)
  4. With `enable hAsB;` declared, `H4q` parses identically to `B4q` inside note streams; outside note streams `Int H = 5;` continues to compile as an identifier (DEFER-02/03)
**Plans**: 3 plans
- [x] 21-01-PLAN.md — Pragma plumbing (PRAG-01 + PRAG-02): PragmaScanner + PragmaSet + PragmaRegistry + Parser/SimpleLexer/Program/FlowEngine/ModuleLoader integration + 15+ unit/integration Facts — Shipped 60f7f18
- [x] 21-02-PLAN.md — H-alias substitution (DEFER-02/03): Token.OriginalText (D-15) + SimpleLexer.TryParseNote H→B gated on enable hAsB; (D-13/D-14) + 9 HAliasFacts + tightened PragmaIsolationFacts + test_h_alias.flow + test_h_identifier.flow — Shipped 05c2174
- [x] 21-03-PLAN.md — Closure (REQUIREMENTS/ROADMAP/STATE/VERIFICATION + 14-deferred-items DEFER-02/DEFER-03 strikethrough) — Shipped 2026-04-26
**UI hint**: yes

### Phase 22: Tier B/C Composer DX Bundle
**Goal**: Six independently shippable Tier B/C composer DX features land — arpeggio parameters, chord voicings, delay sync, snap-to-grid quantize, legato/portamento, and varispeed WAV pitch-shift
**Depends on**: Phase 18 (DX-12 delay sync uses `Fraction` for tempo math; DX-13 quantize uses Fraction grid resolution)
**Requirements**: DX-10, DX-11, DX-12, DX-13, DX-14, DX-15
**Success Criteria** (what must be TRUE):
  1. Composer can call `(arpeggio Cmaj7 q "up" "linear")` and get the expected 4-note ascending arpeggio at quarter-note rate; direction (`up`/`down`/`updown`/`downup`/`random`) and pattern (`linear`/`chord-tone`/`scale-tone`) selectable (DX-10)
  2. Composer can call `inversion(Cmaj, 1)` → `[E4, G4, C5]` and `voicing(Cmaj7, "drop2")` lowers the 2nd-from-top note by an octave (DX-11)
  3. `tempo 120 { ... delay(buf, e, 0.5, 0.4) ... }` produces an eighth-note-synced delay (250ms at 120 BPM); existing ms-rate overload unchanged (DX-12)
  4. Pre-humanized euclidean output snaps cleanly to a 1/16 grid via `quantize(seq, e, 1.0, 0.0)` at strength=1; swing parameter -1..1 applied (DX-13)
  5. MIDI export of `portamento(seq, 100ms)` includes CC65=127 + CC5 events per Sweetwater MIDI spec; `legato(seq, overlap)` extends note durations by overlap factor (DX-14)
  6. `loadWav("kick.wav", 12)` returns a buffer one octave higher (sample count halved, frequency doubled) compared to `loadWav("kick.wav")`; default `loadWav(path)` unchanged (DX-15)
**Plans**: 7 plans
- [x] 22-01-PLAN.md — DX-10 4-arg arpeggio (rate + direction + pattern) — Shipped 6500412
- [x] 22-02-PLAN.md — DX-15 varispeed loadWav (Int semitones + Double ratio) — Shipped 95582e7
- [x] 22-03-PLAN.md — DX-11 inversion + drop2/drop3/open/close/spread voicings — Shipped 5fba059
- [x] 22-04-PLAN.md — DX-12 NoteValue delay overload synced to MusicalContext.Tempo — Shipped 98da48e
- [x] 22-05-PLAN.md — DX-13 quantize with OnsetOffset onset-shift mechanism (Pitfall 9 identity) — Shipped d3f5350
- [x] 22-06-PLAN.md — DX-14 legato + portamento via DurationOverlap/PortamentoMs fields — Shipped d2bde5d
- [x] 22-07-PLAN.md — Closure (REQUIREMENTS/ROADMAP/STATE/VERIFICATION) — Shipped + closure

### Phase 23: Microtonal Tuning (Wedge)
**Goal**: Named-tunings wedge ships per D-03 — `enable justIntonation;` / `enable pythagorean;` / `enable equalTemperament;` change `Note → frequency` lookup; transforms remain pitch-class agnostic (binding pre-ordering #4 — own phase, highest blast radius)
**Depends on**: Phase 21 (microtonal activates via the Phase 21 pragma system)
**Requirements**: MICR-01, MICR-02, MICR-03
**Success Criteria** (what must be TRUE):
  1. With `enable justIntonation;` declared, `play(C4 E4)` produces frequency ratio 5:4 (1.25) instead of 12-TET `~1.2599`; `pythagorean` and `equalTemperament` named tunings also ship (MICR-01)
  2. `transpose(seq, 5)` produces the same MIDI pitch numbers under every tuning; only rendered frequencies differ (MICR-02)
  3. Tuning system applies at render-time only — existing `transpose`, `invert`, `retrograde`, `augment`, `diminish` transforms remain pitch-class-based and tuning-agnostic (MICR-02)
  4. Unknown tuning names raise a clear error pointing at the documented v1.4 Scala-loader expansion (MICR-03)
**Plans**: 5 plans
- [x] 23-01-PLAN.md — Closed enums (TuningSystem, Mode) + RenderTuning value object (Pattern A locked) + 14 ratio tables (7 JI + 7 Pythagorean modes) + Wave 1 canonical Facts (5/4 JI third, 81/64 Pythagorean third, Eb≠D# spelling, cent additivity) — Shipped b6b916b + 39ef570
- [x] 23-02-PLAN.md — Pragma registration + MusicalContext.Tuning + FlowEngine bridge + tuning-aware PitchConversion overload + Pattern A threading through 13 synthesizers + ByteIdenticalDefaultTuning regression + MICR-01/02/03 end-to-end acceptance Facts — Shipped 47d7718 + f6b00ba + 470c3cb + 8190fb2
- [x] 23-03-PLAN.md — RenderingDiagnostics one-shot warning channel + ScaleDatabase 5-church-mode extension (D-04, ValidKeys 34→119) + D-11 enharmonic + D-13 writeMidi non-12-TET warnings + writeMidi context-dependent registration migration — Shipped 4ea0927 + 3e6a3ba
- [x] 23-04-PLAN.md — Five .flow tuning smoke scripts (test_tuning_ji/pythagorean/equal/transpose_invariant/determinism) + TuningDeterminismTests Integration Facts (JI/explicit-EqualTemperament/Pythagorean two-run byte-identical pin via WARNING-5 inline sources) — Shipped ba27282 + 4f85eaf
- [x] 23-05-PLAN.md — Closure (REQUIREMENTS/ROADMAP/STATE/VERIFICATION + 14-deferred-items D-11/D-13 strikethrough) — Shipped + closure

### Phase 24: Scale Linting (flow-lsp)
**Goal**: Opt-in `enable scaleLint;` pragma activates flow-lsp scale linting that surfaces non-diatonic notes inside `key { ... }` contexts as Information-severity squiggles — zero flow-lang touch
**Depends on**: Phase 21 (LINT-01 activated via the pragma system; flow-lsp only — can run parallel to Phase 23)
**Requirements**: LINT-01, LINT-02, LINT-03
**Success Criteria** (what must be TRUE):
  1. With `enable scaleLint;` declared, editing `key Cmajor { | C4 D4 E4 F#4 G4 | }` shows an Information-severity squiggle on `F#4` (LINT-01)
  2. Without `enable scaleLint;`, a key-block with non-diatonic notes produces zero scale-lint diagnostics — opt-in only, never default-on (LINT-02)
  3. Scale linting respects nested key contexts — innermost active key wins for diagnostic computation (`key Cmajor { key Aminor { | F#4 | } }` does NOT flag F#4 against C major) (LINT-03)
**Plans**: 6 plans
- [x] 24-00-PLAN.md — Wave 0 ParseSession pragma-scan widen + ParseSessionPragmaFacts (closes Phase 17/21 latent hAsB-in-LSP bug) — Shipped 6bcc697
- [x] 24-01-PLAN.md — PragmaRegistry one-line add + Phase 21 fact migration + PragmaRegistryScaleLintFacts — Shipped 354a4de + 52a3dff
- [x] 24-02-PLAN.md — DiatonicSpellings 119-entry hardcoded map + DiatonicSpellingsFacts (12 Theory rows + 5 Facts) — Shipped 94ccdaf + 9eae7ae
- [x] 24-03-PLAN.md — ScaleLintAnalyzer + 14 Facts + 7-row mode Theory pinning LINT-01/02/03 + D-01..D-23 — Shipped 3c18795 + 3d9233a
- [x] 24-04-PLAN.md — IScaleLintPublisher + ScaleLintPublisher + CombinedDiagnosticsPublisher + Program.cs DI wiring + 5 Facts — Shipped 0dc9a99 + b0b9971 + 96ab39c
- [x] 24-05-PLAN.md — tests/test_scale_lint.flow + REQUIREMENTS/ROADMAP/STATE/VERIFICATION closure
**UI hint**: yes

### Phase 25: Gaussian Humanize (LAST PRNG phase)
**Goal**: `humanizeGaussian()` ships as a separate function (D-04) so existing uniform `humanize()` keeps the v1.2 byte-identical determinism contract for tutorial.flow + showcase.flow (binding pre-ordering #5 — must be the LAST PRNG-touching phase)
**Depends on**: Phases 18-24 (must be the last PRNG-touching phase per Pitfall 6 mitigation)
**Requirements**: DEFER-06
**Success Criteria** (what must be TRUE):
  1. Composer can call `humanizeGaussian(seq, 0.1, 42)` and get Gaussian-distributed velocity perturbation via Box-Muller transform; same seed produces deterministic velocity bytes pinned by Fact (DEFER-06)
  2. Existing `humanize(seq, 0.1, 42)` produces identical bytes to v1.2 — uniform path UNCHANGED, byte-identical determinism contract preserved across two consecutive runs (DEFER-06)
  3. Two consecutive runs of `showcase.flow` (now including a Gaussian-humanize call site) produce cmp-clean WAV + MIDI output (DEFER-06)
**Plans**: 5 plans
- [x] 25-00-PLAN.md — Wave 0 test scaffolding (HumanizeGaussianFacts skeleton + ByteIdenticalShowcaseGaussianTests skeleton + smoke .flow + FlowScriptData entry) — Shipped 646425e + bcabebb + 1ae0796 + 528cfe1
- [x] 25-01-PLAN.md — MusicalNoteData.With(velocity:) helper extension + 4 NoteTypeWithVelocityFacts (RESEARCH critical-bug avoidance precondition) — Shipped 5efb23f + b9017fc
- [x] 25-02-PLAN.md — humanizeGaussian + RegisterHumanizeGaussian + NextGaussianSample + std.flow declaration + 7 D-23 Facts GREEN — Shipped 9c3553e + a928628 + 3cc3a11
- [x] 25-03-PLAN.md — Showcase wrap (D-20) + tutorial chapter (D-22) + smoke .flow real body + Phase25 byte-identical Facts GREEN — Shipped 24fd415 + ab08b37 + 8be8c66 + 5169db8
- [x] 25-04-PLAN.md — Closure: REQUIREMENTS/ROADMAP/STATE/VERIFICATION + Phase 18 byte-identical regression confirmation — Shipped 2026-05-04

### Phase 26: Op Standardization (Prefix-Only)
**Goal**: Eliminate infix arithmetic operators in favor of S-expression prefix builtins, aligning the entire language with the no-infix-operators philosophy (MEMORY: feedback_language_philosophy). Removes `BinaryExpression`/`BinaryOperator` AST nodes; adds `(add)`/`(sub)`/`(mul)`/`(div)`/`(neg)`/`(concat)` builtins covering the full numeric widening chain (Int → Long → Float → Double → Number); migrates all stdlib + ~70 .flow tests.
**Depends on**: Nothing (foundation — must precede Phase 26.1 so the new dict/tuple/symbol features inherit the prefix-only base)
**Requirements**: STD-01, STD-02, STD-03
**Success Criteria** (what must be TRUE):
  1. Parser no longer accepts infix `+ - * /` outside negative number literals; `ParseAdditive` and `ParseMultiplicative` removed; `BinaryExpression` + `BinaryOperator` deleted (STD-01)
  2. Builtins `(add)`, `(sub)`, `(mul)`, `(div)` ship with overloads for Int, Long, Float, Double, Number; `(neg x)` for runtime negation; `(concat String String)` for string concatenation (STD-02)
  3. Negative number literals (`-5`, `-3.14`) lex as single tokens at expression-start, after `(`, after `,` — mirroring existing `-3dB`/`+50c`/`-5st` precedent (STD-02)
  4. All ~70 existing `test_*.flow` scripts migrated to prefix form and pass with byte-identical output; `tutorial.flow` + `showcase.flow` cmp-clean across two consecutive runs (STD-03)
  5. CLAUDE.md updated to remove the stale "==, !=, <, >" claim and document prefix-only rule (STD-03)
**Plans**: 5 plans
- [x] 26-01-PLAN.md — Wave 0 RED: 7 Phase26 fact files (NewOverloadFacts, NegOverloadFacts, IntegerDivisionFacts, MixedTypeArithmeticFacts, NegativeLiteralLexFacts, UnaryMinusShorthandFacts, InfixRejectedFacts) + Migrate26 csproj scaffold — Shipped 86fa69a
- [x] 26-02-PLAN.md — Wave 1 GREEN mega-commit (D-13): delete BinaryExpression.cs + ParseAdditive/ParseMultiplicative/ParseUnary arithmetic; add ParseUnaryShorthand; lexer _lastEmittedType + TryLexSignedNumber (music-context excluded); EvaluateBinary delete + EvaluateFunctionCall coercion fix; 14 new builtin registrations + 12 StdLib helpers + std.flow Long/Number/neg/idiv decls — Shipped 86fa69a
- [x] 26-03-PLAN.md — Wave 2: scripts/Migrate26 walker implementation (token-stream rewrite, precedence climber, note-stream skip, defensive concat) + idempotence smoke test — Shipped 86fa69a + a5a026e (Wave 2.1 walker fix)
- [x] 26-04-PLAN.md — Wave 3: mass migration of 8 tracked .flow files + in-session byte-identical gate (showcase.wav + .mid identical pre/post) + persistent xUnit guard verification (6 PASS / 2 deferred to fix-omissions phase) — Shipped 2d3efe1
- [x] 26-05-PLAN.md — Wave 4: CLAUDE.md prefix-only rule (line 148 lambda + line 175 AST row delete + Core bullet) + REQUIREMENTS/ROADMAP/STATE closure + 26-VERIFICATION.md final report — Shipped TBD

### Phase 26.1: Symbols + Tuples + Dicts (INSERTED)
**Goal**: Three tightly-coupled language additions land together — Symbol primitive type (`#foo` syntax, interned), Tuple type (`<<a, b, c>>` literal with per-position types and arity, `~>` unpack flow operator, destructuring assignment, `@N` indexing), and generic `Dict<K, V>` with hashable keys (Int, Long, Float, String, Symbol, Note, Chord, Tuple-of-hashables). Dicts surface via builtins only (no literal syntax — preserves S-expr style and avoids `{...}` collision with Phase 19 tuplets).
**Depends on**: Phase 26 (must inherit prefix-only philosophy from Op Standardization)
**Requirements**: SYM-01, TUP-09, TUP-10, TUP-11, DICT-01, DICT-02, DICT-03
**Success Criteria** (what must be TRUE):
  1. Symbols: `#foo` lexes as `SymbolLiteral`; `Symbol` type registered; equality is pointer-compare (interned); usable as `Dict` key; strict separation from String (`(eq #foo "foo")` is false) (SYM-01)
  2. Tuples: `<<C4, q>>` literal; `<<>>` empty + `<<x>>` singleton valid; type annotation `<<Note, Beat>>` mirrors literal; `tup@0`/`@1` indexing; `<<a, b>> = foo()` destructure (assignment only — proc/lambda params deferred); immutable; structural equality (TUP-09)
  3. Flow ops: `~>` unpacks tuple into multi-arg call; on non-tuple LHS, `~>` behaves identically to `->` (charitable interpretation per memory) (TUP-10)
  4. `(unpack tuple func)` runtime builtin — first-class S-expression-style equivalent of `~>` for value-level / dynamic-dispatch / HOF-composition use (mirrors Lisp's `(apply f args)`); ships alongside `~>`, not as a replacement (TUP-11)
  5. Dicts: `Dict<K, V>` Java-generic style; `(dict K V K V ...)` flat constructor; `(dictTuple <<K,V>> <<K,V>> ...)` tuple-pair constructor; immutable; allowed key types: Int/Long/Float/String/Symbol/Note/Chord/Tuple-of-hashables; disallowed-key annotations rejected at type-check time (DICT-01)
  6. Dict ops: 14-op surface — `(dict)`, `(dictTuple)`, `(get d k)`, `(getOr d k default)`, `(set d k v)`, `(remove d k)`, `(has d k)`, `(keys d)`, `(values d)`, `(size d)`, `(merge d1 d2)` (last-write-wins), `(each d cb)`, `(map d cb)`, `(filter d pred)`; all mutations return new dicts; insertion order preserved (DICT-02)
  7. Iteration: `(each d callback)` yields `<<key, value>>` tuples, callable via `~>` for multi-arg lambda dispatch (DICT-03)
  8. Float NaN keys: NaN-equals-NaN special-case scoped to Dict-internal eq only; Flow's general `(eq nan nan)` continues to follow IEEE 754
  9. Existing `tutorial.flow` + `showcase.flow` remain cmp-clean (no regression to byte-identical determinism)
**Plans**: 6 plans
- [x] 26.1-01-PLAN.md — Wave 0 RED scaffolding (10 Fact stubs + 9 .flow stubs + FlowScriptData entry) — Shipped ac3b926 + d98ed21
- [x] 26.1-02-PLAN.md — Wave 1: Symbol primitive (SYM-01) + IsHashable() virtual on FlowType — Shipped 35474ed
- [x] 26.1-03-PLAN.md — Wave 2: Tuple type (TUP-09) literal/index/destructure/AnyArity sentinel — Shipped 6549116
- [x] 26.1-04-PLAN.md — Wave 3: ~> flow op (TUP-10) + (unpack) runtime (TUP-11) — Shipped d628870
- [x] 26.1-05-PLAN.md — Wave 4: Dict<K, V> + 14-op surface (DICT-01/02/03) — Shipped daaa023
- [x] 26.1-06-PLAN.md — Wave 5: Closure (REQUIREMENTS/ROADMAP/STATE/CLAUDE.md/26.1-VERIFICATION.md) — Shipped 2026-05-09

### Phase 26.2: Music Type Ergonomics + FX Overloads (INSERTED)
**Goal**: Special music types (Decibel, Beat, Cent, Semitone, Millisecond, Second) are currently a documentation lie — they read nicely in code but can't be passed to the audio FX functions that should accept them (Decibel + Beat have ZERO numeric compatibility; Millisecond/Second only convert to each other; only Cent is fully functional). Phase normalizes type compatibility (Cent precedent), registers music-type overloads on every FX builtin where the parameter is conceptually musical, and decides the `gain` dB-vs-linear policy (charitable interpretation per memory — likely splits into `gain(Buffer, Decibel)` for dB and `volume(Buffer, Float)` for linear). Surfaced from quick task 260504-v6j follow-up: a user typed `(gain rendered -12dB)` and got "No matching overload for function 'gain' with argument types (Buffer, Decibel)" — the special types are useless if they can't reach the functions named after them.
**Depends on**: Nothing (orthogonal to 26 / 26.1; can ship in parallel)
**Requirements**: ERG-01, ERG-02, ERG-03, ERG-04, ERG-05
**Success Criteria** (what must be TRUE):
  1. All six music types implement `IsCompatibleWith` / `CanConvertTo` against their primitive numeric counterpart (CentType precedent); Decibel + Beat accept Double / Float; Millisecond / Second / Semitone reach Double / Float / Int sites consistently (ERG-01)
  2. Music-type overloads registered for `gain(Buffer, Decibel)`, `delay(Buffer, Millisecond, ...)`, `compress(Buffer, ..., Millisecond, Millisecond)`, `sidechain(..., Millisecond, Millisecond)` and any other FX site where a music type is the natural read (ERG-02)
  3. `gain` dB-vs-linear policy decided and documented in CLAUDE.md; if the split path is chosen, `volume(Buffer, Float)` ships alongside `gain(Buffer, Decibel)` and the bare-`Double` overload's behavior is locked down (ERG-03)
  4. Optional: `Hertz` type with `440Hz` / `1.5kHz` literal syntax + lowpass/highpass/bandpass music-type overloads (ERG-04)
  5. New facts cover every overload + compatibility path (TDD discipline; mirrors existing music-type fact files)
  6. Existing flow-lang.Tests pass; `tutorial.flow` + `showcase.flow` remain cmp-clean (byte-identical determinism preserved)
**Plans**: 6 plans
- [x] 26.2-01-PLAN.md — Wave 0 RED scaffolding (6 *Facts.cs files) + Value.ConvertTo Double-arm patch (closes the 2 RED DecibelBeat facts) — Shipped 45b01fb + 0d61413 + 50add6d
- [x] 26.2-02-PLAN.md — Wave 1: Ms/Sec/Hertz IsCompatibleWith(Double|Float) + Value.Hertz factory + new HertzType (ERG-01 + ERG-04 type-level) — Shipped 4f92c24 + f12d648 + e4b71f0
- [x] 26.2-03-PLAN.md — Wave 2: Hz/kHz lexer arms + Parser HertzLiteral route + audio.flow gain(Decibel) forward decl (ERG-04 + ERG-05 closure) — Shipped d655c65 + 28158cc + d3ce16e
- [x] 26.2-04-PLAN.md — Wave 3: FX music-typed overloads (delay-Ms, compress/sidechain-Decibel-Ms, reverb-Second, lowpass/highpass/bandpass-Hertz, createXxxTone-Hertz family) — ERG-02 + ERG-04 — Shipped dfbfa1f + 821e9d0 + af23658
- [x] 26.2-05-PLAN.md — Wave 4: volume(Buffer, Double) linear-multiplier alternative to gain(dB) — ERG-03 (D-04..D-07) — Shipped 6df301e + 00a5a41
- [x] 26.2-06-PLAN.md — Wave 5: Closure (REQUIREMENTS/ROADMAP/STATE/CLAUDE.md/26.2-VERIFICATION.md) — Shipped 86bdd15

### Phase 27: Tutorial + Showcase Refresh
**Goal**: `examples/tutorial.flow` and `examples/showcase.flow` demonstrate every v1.3 feature end-to-end with byte-identical determinism; v1.1 + v1.2 chapters preserved (last per v1.2 precedent — Phase 16 was the v1.2 tutorial-refresh closer)
**Depends on**: Phases 18-26.2 (every v1.3 feature must be live before tutorial can demonstrate it, including Phase 26 prefix-op standardization, Phase 26.1 symbols/tuples/dicts, and Phase 26.2 music-type FX overloads)
**Requirements**: QOL-04
**Success Criteria** (what must be TRUE):
  1. examples/tutorial.flow demonstrates EVERY v1.3 feature end-to-end: tuplets `{3:2 ...}q` + fractional `C4/12` + nested tuplets, range, multi-letter enharmonics, negative slice, `enable hAsB;` pragma (companion file demo), arpeggio/voicings/NoteValue-rate-delay/quantize/legato/portamento/varispeed (prose-only since varispeed lives on loadWav and tutorial ships no sample), named-tuning microtonal pragmas (companion file demo), scale-lint pragma (print-only mention; flow-lsp owns surface), humanizeGaussian, prefix-only arithmetic via `(add)`/`(sub)`/`(mul)`/`(div)`/`(idiv)`/`(neg)`/`(concat)`, symbol `#foo`, tuple `<<a, b>>` with `~>` unpack + `(unpack)` runtime, generic `Dict<K, V>` 14-op surface, AND Phase 26.2 surface — `volume(buf, linear)` vs `gain(buf, dB)` split, Hertz literals `440Hz`/`1.5kHz`, Ms-typed FX overloads (`delay`/`compress`/`sidechain`), Second-decay `(reverb buf mix 1.8s)`, Hertz-overloaded filters + Hertz-overloaded `createSineTone` signal-generator demo (saw/square/triangle mechanically equivalent — not separately demoed) (QOL-04)
  2. Both scripts run to completion (exit 0) producing non-empty WAV + MIDI output to `examples/output/` (QOL-04)
  3. Byte-identical determinism contract holds across two consecutive runs (cmp-clean) for both `tutorial.flow` and `showcase.flow` (QOL-04)
  4. Existing v1.1 + v1.2 chapters preserved — no regressions to prior tutorial coverage (QOL-04)
**Plans**: 5 plans
- [x] 27-01-PLAN.md — Wave 1: Language-feature weaves (Symbols 1.5, Tuples + ~> 4.5, Dict 4.6, prefix-arithmetic prose ch.2, Hertz + Ms-FX inline ch.9, gain-vs-volume own chapter 9.5, Second-decay reverb append ch.16) — Shipped 995ff67
- [x] 27-02-PLAN.md — Wave 2: Music-feature batch chapter 19.5 (sub-section A tuplets+fractional, B microtonal+scale-lint pragma prose, C DX-10..15 bundle, D misc small wins) + Congratulations bullet list expansion — Shipped dbffbec
- [x] 27-03-PLAN.md — Wave 3: Tutorial graduation song refactor with Phase 26.2 audible features (D-103) + showcase.flow REPLACE with v1.3 polyrhythmic-minimal piece + companion files h_alias.flow + microtonal_ji.flow under examples/pragmas/ — Shipped eadbd9f
- [x] 27-04-PLAN.md — Wave 4: Phase27ByteIdenticalPragmaTests.cs (4 facts mirroring Phase18/ByteIdenticalShowcaseTests verbatim) — Shipped e15c5be
- [x] 27-05-PLAN.md — Wave 5: Closure (REQUIREMENTS QOL-04 rewrite + CLAUDE.md Music Types Quick Reference + ROADMAP/STATE/VERIFICATION/SUMMARY + Open Questions RESOLVED flip) — Shipped ace6416

## v1.4 Audio Fidelity, Distribution & Public Showcase (Phases 28-34) — in progress

Three intertwined threads close v1.4 and pivot Flow from pre-public to public:

1. **Audio fidelity** (Phases 28, 29, 33) — runtime polyphony rewrite + first-class articulation; modest realism pass on existing synths; full SFZ orchestral sampler for serious composition.
2. **Distribution + tooling** (Phases 30, 31, 32) — `flow` CLI + formal install + MIDI↔Flow conversion; LSP polish, VSCode marketplace publish, JetBrains plugin (stretch); full Scala (`.scl`) tuning loader.
3. **Public showcase** (Phase 34, milestone closer) — short symphony rendered entirely from Flow source via the SFZ sampler; the headline artifact of v1.4 and the moment Flow stops being pre-public.

v1.3's byte-identical determinism contract is preserved in shape (two-run cmp-clean) but pinned bytes change because the rendered output legitimately differs.

### Phase 28: MIDI + Audio Polyphony & Articulation Rewrite
**Goal**: Overlapping notes in dense polyphonic writing (ragtime stride, piano with sustained pedal under inner voices, contrapuntal lines) render at their authored duration in BOTH MIDI export and WAV rendering — no truncation, no cut-off, no inaudible-shortening. Articulation becomes a first-class note attribute (staccato/legato/accent/marcato) with explicit semantic duration + velocity rules, replacing the current implicit duration-only model.
**Depends on**: Phase 27 closure (v1.3 ships clean before v1.4 architecture work begins)
**Requirements**: SPEC-1..SPEC-9 (see 28-SPEC.md)
**Status**: Complete (shipped 2026-05-10; UAT signed off after staccato-grace-note-artifact parser fix — see `.planning/debug/staccato-grace-note-artifact.md`)
**Success Criteria** (what must be TRUE):
  1. Ragtime test fixture (stride pattern: whole-note left-hand bass under syncopated right-hand eighth-notes) renders with the bass note audibly sustaining for its full duration in both MIDI and WAV — VERIFIED at xUnit level via HeldNoteRmsTests + VoiceBlockRenderTests; pending composer ear-check
  2. Staccato note attribute renders shorter than its authored duration with audible separation; legato attribute connects adjacent notes without retrigger; accent boosts velocity; marcato combines accent + staccato — VERIFIED via ArticulationRulesTests + ArticulationVelocityTests + PerSynthArticulationTests (78 facts)
  3. Existing flow scripts that don't use articulation attributes continue to render identically (or with audibly-better polyphony — content-equivalent, byte-different) — VERIFIED via Phase 22 LegatoFacts + Phase 18/25/27 ByteIdentical two-run determinism (22 facts)
  4. Voice allocation uses a pool model (round-robin or steal-oldest) rather than one-track-per-note explosion — IMPLEMENTED via VoiceAllocator.AllocateWithPool with steal-oldest policy (Plan 28-05); voicePool 1..256 block; default 32
  5. Both `flow-midi/` MIDI export and `flow-lang/StandardLibrary/Audio/` WAV rendering share the same articulation/polyphony model — no divergent contracts — VERIFIED via MultiTrackMidiTests + RagtimeFixtureTests reading MIDI back via DryWetMidi
**Plans**: 7/7 complete (28-01 through 28-07)

### Phase 29: Instrument Realism — ✅ Complete (2026-05-12)
**Goal**: Built-in instruments (piano, brass, sax, drums) sound noticeably more realistic — closer to a real recording or a high-quality VST than the current synthesizer output. Approach (sample-based library, improved synthesis, hybrid) is decided in /gsd-spec-phase 29.
**Closure:** see `.planning/phases/29-instrument-realism/29-VERIFICATION.md`. SPEC D-29 Gates A and D shipped as judgment-call passes (documented amendments); B/C/E strict-pass. 5 v1.5 backlog seeds captured: flute sample expansion, sampled-instrument articulation envelopes, three-velocity piano, sampled drums path, PerSynthArticulationTests cleanup.
**Depends on**: Phase 28 (articulation system + polyphony model must exist before per-sample articulation rendering can hook in)
**Requirements**: SPEC-1..SPEC-8 (see 29-SPEC.md)
**Success Criteria** (what must be TRUE):
  1. Side-by-side audible comparison test: rendering the v1.3 graduation song before vs after Phase 29 demonstrates a clear realism improvement (subjective UAT — manual listening verification per phase precedent)
  2. All existing instruments (piano, brass, sax, drums) get the realism pass; no instrument is left at v1.3-quality fidelity
  3. Articulation attributes from Phase 28 render audibly per-instrument (staccato piano sounds like a staccato piano, not just a shortened sustain)
  4. Build size + repo size impact bounded ≤ 5 MB CC0 sample bundle
  5. Existing flow scripts continue to render (instrument names + signatures unchanged — internal implementation upgraded)
**Plans**: 7 plans (29-01 through 29-07) — see `.planning/phases/29-instrument-realism/`

### Phase 30: Flow CLI + Formal Install
**Goal**: Ship a `flow` binary so Flow becomes installable + usable without cloning the repo. Adds `flow run|eval|repl|watch|play|render|flow2midi|midi2flow|check|version|new` subcommand surface; XDG config; install script targeting `/usr/local/bin/flow`; bundles stdlib alongside the binary so `use "@audio"` etc. resolves post-install. Closes Bug B (flow-midi cluster: 480-tick quarter mis-snap, RH/LH heuristic, leading-empty-bar emission) at the Quantizer + FlowGenerator layer; SPEC-6 round-trip pinned by 3 CC0 fixtures.
**Depends on**: Phase 28 closure (articulation/polyphony stable; CLI wraps stable runtime)
**Requirements**: REQ-1, REQ-2, REQ-3, REQ-4, REQ-5, REQ-6, REQ-7, REQ-8
**Success Criteria** (what must be TRUE):
  1. `flow run path/to/script.flow` works after install without cloning the repo or running `dotnet run`
  2. Install script produces a working binary at `/usr/local/bin/flow` (Linux primary)
  3. `midi2flow input.mid -o output.flow` round-trips a MIDI file into editable Flow source via the existing `flow-midi` parser
  4. `flow render input.flow -o out.wav` writes a WAV; `flow flow2midi input.flow -o out.mid` writes MIDI
  5. Existing `dotnet run --project flow-interpreter ...` invocations continue to work during transition
**Plans**: 9 plans
- [x] 30-01-PLAN.md — flow-cli project scaffold + System.CommandLine 2.0.7 root + 11-subcommand stub registry — Shipped fa66c38 + b57a1e8 + ae6acae
- [x] 30-02-PLAN.md — 10 real subcommand handlers + Midi2FlowStubCommand + embedded scaffold template — Shipped 48761cb + bc9bb8c + 8bcc8c0 + ebb6802 + dac4dad
- [x] 30-03-PLAN.md — FlowConfig singleton in flow-lang/Runtime + Tomlyn 2.3.2 loader + 4 propagation hooks + 8 Facts — Shipped 475838c + f8ca1ed + a34c904 + 8116b2f + a37b7ab
- [x] 30-04-PLAN.md — dotnet publish profile + scripts/publish.sh + 38 MB published binary + stdlib CopyToPublishDirectory + AppContext.BaseDirectory fix for single-file — Shipped 675506d + fc6fead + 4481979
- [x] 30-05-PLAN.md — scripts/install.sh + scripts/test-install.sh (8 s smoke) + scripts/uninstall.sh — Shipped c31f36d + 984fa39 + 07227b4
- [x] 30-06-PLAN.md — flow-midi.Tests xUnit project + MidiFixtureBuilder + 8 RED-on-HEAD fact classes pinning Bug B Defects 1/2/3 — Shipped a78054a + a6c93bc + 81d2729 + dc6161f
- [x] 30-07-PLAN.md — Quantizer.cs: SnapDurationCapped tolerance band + AddRests count cap + leading-bar trim + DELETE AddSplitTracks (6 RED facts flip GREEN) — Shipped b79fd87 + 2aed0eb + 63eb787 + 24daaff
- [x] 30-08-PLAN.md — FlowGenerator.cs: `bool roundTrip` mode (drop (play output), explicit durations, trackN naming, section "roundtrip", `Song s = [roundtrip]` marker) — Shipped a7170dd
- [x] 30-09-PLAN.md — flow midi2flow real handler + 3 CC0 fixtures + Midi2FlowRoundTripTests + writeMidi denominator double-encoding fix + closure — Shipped 303bddd + 9801b9e + a026afb + (closure)

### Phase 31: LSP Enhancements + JetBrains Stretch
**Goal**: Close four `flow-lsp` gaps (diagnostics severity expansion, context-aware completion filtering, varargs visibility in signature help / hover, grammar enhancements for new comment forms + function-call coloring) and add JetBrains plugin scaffolding via LSP4IJ. VSCode Marketplace + OpenVSX publish deferred to v1.5 per SPEC Round 1 decision. Stretch: built plugin .zip attached to v1.4 release tag if all 6 mandatory areas land green.
**Depends on**: Phase 28 closure (articulation tokens like `leg` need LSP completion support); Phase 30 closure (LSP ships alongside the formal install; Phase 31 adds the `flow lsp` subcommand)
**Requirements**: SPEC-1, SPEC-2, SPEC-3, SPEC-4, SPEC-5, SPEC-6, SPEC-7
**Success Criteria** (what must be TRUE):
  1. flow-lsp emits structured-severity diagnostics: Warning (UnusedImport, ShadowedVariable), Information (UnreachableSection, ScaleLint default-on per CONTEXT D-03)
  2. CompletionHandler filters suggestions by what the current file `use`d (no longer suggests `arpeggio` if `@harmony` is not imported); pragma-filter for note-stream H-aliases; musical-context boost for roman numerals inside `key { }` blocks
  3. Varargs functions render `name: Type…` with Unicode U+2026 ellipsis in signature help, hovers, and completion tooltips (per CONTEXT D-01/D-02)
  4. SimpleLexer + TextMate grammar recognize `;` (position-sensitive Option A per D-11) / `Note:` / `TODO:` / `FIXME:` as comments; existing `Note:` already shipping per RESEARCH §Summary finding
  5. TextMate grammar distinguishes `(funcName ...)` head positions (entity.name.function.flow) from bare identifier references (variable.other.flow)
  6. All 70+ in-repo `.flow` fixtures parse + render under the new lexer; Phase 18/25/27/28 ByteIdentical contracts preserved; zero source-text migrations required under D-11 Option A (RESEARCH grep audit confirms)
  7. JetBrains plugin scaffolding lands UNCONDITIONALLY (CONTEXT D-10); if all 6 mandatory areas GREEN, gradlew buildPlugin produces a .zip attached to v1.4 release tag; if not, scaffolding ready for v1.5 follow-up
**Plans**: 9 plans
- [x] 31-01-PLAN.md — Wave-0 scaffolding: `flow lsp` subcommand (Pitfall 7 resolution) + StdlibSymbolIndex.ProcsForModule helper + LspFixtures.StdlibIndex helper + 31-DECISIONS.md (D-11 + D-12) — Shipped c1e0a5d + b7202b9 + 82e06d5
- [x] 31-02-PLAN.md — 3 new analyzers (UnusedImport / UnreachableSection / ShadowedVariable) + ScaleLint default-on per D-03 + CombinedDiagnosticsPublisher wiring (SPEC-1) — Shipped 161755c + e259845 + 078a3f7
- [x] 31-03-PLAN.md — SimpleLexer: 3 new arms `;` / `TODO:` / `FIXME:` (Option A position-sensitive per D-11); Phase31LexerCommentFormsTests (SPEC-4 lexer side) — Shipped fdd1b5e + dd81c87
- [x] 31-04-PLAN.md — CompletionHandler.BuildItems: FilterByImports + FilterByPragmas + BoostByMusicalContext (SPEC-2) — Shipped cb0b30b + cd141c8
- [x] 31-05-PLAN.md — LspMappings.FormatSignature + BuildParameters using Unicode U+2026 per D-01/D-02; HoverHandler + SignatureHelpHandler wiring (SPEC-3) — Shipped 592a55a + fb3f611
- [ ] 31-06-PLAN.md — flow.tmLanguage.json: 4 new comment scopes + function-call vs variable-ref split; regenerated grammar snapshots (SPEC-4 grammar side + SPEC-5)
- [ ] 31-07-PLAN.md — Empirical migration audit: grep + smoke-run every in-repo .flow + byte-identical regression suite; 31-MIGRATION-AUDIT.md (SPEC-6)
- [ ] 31-08-PLAN.md — JetBrains plugin scaffolding (flow-jetbrains/ Gradle + plugin.xml + FlowLanguageServerFactory.kt); manual UAT for stretch verdict (SPEC-7)
- [ ] 31-09-PLAN.md — Closure: 31-VERIFICATION.md + REQUIREMENTS.md / ROADMAP.md / STATE.md updates + VSCode dev-host manual smoke closes Phase 17 HUMAN-UAT rows 1-3 (rows 4-5 stay DEFERRED to v1.5)


### Phase 32: Full Scala (`.scl`) Tuning Loader
**Goal**: Add `enable customTuning("path/to/tuning.scl");` pragma (or equivalent surface) that loads and parses Scala-format tuning files. Closes the v1.3 D-03 deferral. Enables arbitrary microtonal tuning beyond the 3 named-tunings wedge from Phase 23.
**Depends on**: Phase 23 closure (named-tunings infrastructure exists; Scala loader extends it)
**Requirements**: TBD (assigned during /gsd-spec-phase 32)
**Success Criteria** (what must be TRUE):
  1. A `.scl` file with N steps loads and produces a `RenderTuning` value compatible with the existing Phase 23 pipeline
  2. Common public Scala archive files (e.g. partch.scl, slendro.scl, just-intonation.scl) parse without error
  3. Malformed `.scl` files raise clear errors pointing at the offending line
  4. Loaded tunings work end-to-end: pitch conversion, MIDI export advisory warning (per Phase 23 D-13), transform invariance per MICR-02
  5. Phase 23's named-tunings continue to work unchanged (`enable justIntonation;` / `pythagorean;` / `equalTemperament;`)
**Plans**: TBD

### Phase 33: SFZ Orchestral Sampler
**Goal**: Multi-sample sampler subsystem capable of consuming real orchestral sample libraries (SFZ format). Region matching by (pitch, velocity), in-zone resample for pitch shifts beyond the nearest sample, sustain looping for held notes, velocity layers via SFZ region selection. Foundation for the symphony showcase (Phase 34). Builds on Phase 22's `loadWav` varispeed primitive and Phase 29's modest sampler infrastructure.
**Depends on**: Phase 29 closure (modest sampler scaffolding ships first); Phase 28 (articulation system) for per-articulation envelope shaping
**Requirements**: TBD (assigned during /gsd-spec-phase 33)
**Success Criteria** (what must be TRUE):
  1. SFZ parser handles the common subset (`sample`, `lokey`/`hikey`/`pitch_keycenter`, `lovel`/`hivel`, `loop_mode`/`loop_start`/`loop_end`, `ampeg_attack`/`ampeg_release`, `volume`, `pan`, `<region>`/`<group>`/`<global>`)
  2. At least one free orchestral library (VSCO Community / Versilian / Sonatina) loads + plays correctly
  3. Held notes loop their sustain region cleanly (no clicks at loop boundaries)
  4. Velocity layers select the right region per note velocity; out-of-range notes resample from the nearest pitched sample
  5. Composer surface for sampler instruments is locked (e.g. `loadSfz("path.sfz")` builtin or `"sampler:name"` instrument string)
  6. Existing synth-based instruments (piano/brass/sax/drums/strings/organ/bell) continue to work unchanged
**Plans**: TBD

### Phase 34: Symphony Showcase (v1.4 closer — pre-public → public pivot)
**Goal**: A curated short symphony (30-90 seconds, 3-6 instruments) rendered entirely from Flow source code via the SFZ sampler from Phase 33. Polished mix, code screenshots, README updates pointing at the showcase. The headline artifact of v1.4 and the moment Flow stops being pre-public — once the clip is public, the demonstrated API surface becomes effectively frozen.
**Depends on**: Phases 28, 29, 30, 31, 32, 33 (every other v1.4 feature must be locked first; the piece may demonstrate any of them)
**Requirements**: TBD (assigned during /gsd-spec-phase 34)
**Success Criteria** (what must be TRUE):
  1. A short symphony renders end-to-end from Flow source (`examples/symphony/`) via the SFZ sampler with no runtime errors
  2. Composer signs off that the rendered audio is "postable on GitHub" quality — manual UAT, blind evaluation against a reference recording of similar instrumentation
  3. Code screenshots capture the source paired with audible features (musical context blocks, note streams, transforms, sampler instruments, articulation, polyphony)
  4. README.md updated with a prominent showcase link + clip embed; `examples/symphony/README.md` documents how to reproduce the render
  5. v1.4 milestone closure: ROADMAP/STATE/REQUIREMENTS marked complete; public release tag (`v1.4.0`) cut; first public-facing announcement ready
**Plans**: TBD

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
| 18. Foundation — Rational Duration Arithmetic | v1.3 | 0/2 | Not started | - |
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
| 29. Instrument Realism | v1.4 | 0/7 | Plans ready (gated on CC0 sample curation) | - |
| 30. Flow CLI + Formal Install | v1.4 | 9/9 | Complete   | 2026-05-11 |
| 31. LSP Enhancements + JetBrains Stretch | v1.4 | 1/9 | In progress (Wave 0 complete) | - |
| 32. Full Scala (.scl) Tuning Loader | v1.4 | 0/N | Spec pending | - |
| 33. SFZ Orchestral Sampler | v1.4 | 0/N | Spec pending | - |
| 34. Symphony Showcase (v1.4 closer) | v1.4 | 0/N | Spec pending | - |
