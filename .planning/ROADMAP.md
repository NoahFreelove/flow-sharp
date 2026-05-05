# Roadmap: Flow Language

## Milestones

- ~~**v1.0 MVP**~~ — Phases 1-5 (shipped 2026-04-03)
- ✅ **v1.1 Polish & Foundations** — Phases 6-10 (shipped 2026-04-18) — see `milestones/v1.1-ROADMAP.md`
- ✅ **v1.2 Stability & Composer DX** — Phases 11-17 (shipped 2026-04-26) — see `milestones/v1.2-ROADMAP.md`
- 🚧 **v1.3 Composer DX Tier B/C** — Phases 18-27 (in progress)

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

Lead capability: tuplets `{N:M ...}` + arbitrary fractional note durations (`C4/12`). Closes DEFER-01..06 from v1.2, ships the Tier B/C composer DX bundle (arpeggio params, chord voicings, delay sync, microtonal wedge, scale linting, legato/portamento, snap-to-grid quantize, varispeed loadWav), and lands a foundational language consistency pass: prefix-only arithmetic standardization (Phase 26) followed by symbols + tuples + generic dicts (Phase 26.1). 38 requirements across 11 phases.

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
- [ ] **Phase 24: Scale Linting (flow-lsp)** — Opt-in `enable scaleLint;` pragma emits Information-severity diagnostics for non-diatonic notes inside `key { ... }` contexts
- [ ] **Phase 25: Gaussian Humanize (LAST PRNG phase)** — `humanizeGaussian()` Box-Muller transform; preserves v1.2 byte-identical determinism contract for existing uniform `humanize()`
- [ ] **Phase 26: Op Standardization (Prefix-Only)** — Eliminate infix `+ - * /`; add `(add)`/`(sub)`/`(mul)`/`(div)`/`(neg)`/`(concat)` builtins covering numeric widening chain; remove `BinaryExpression`/`BinaryOperator` AST nodes; migrate stdlib + ~70 .flow tests; foundation for Phase 26.1
- [ ] **Phase 26.1: Symbols + Tuples + Dicts (INSERTED)** — Symbol primitive (`#foo`), Tuple type (`<<a, b, c>>` literal, `~>` unpack op, destructuring, `@N` indexing, per-position types), generic `Dict<K, V>` with hashable keys (Int/Long/Float/String/Symbol/Note/Chord/Tuple); dicts via `(dict K V ...)` + `(dictTuple <<K,V>> ...)` builtins (no literal syntax)
- [ ] **Phase 27: Tutorial + Showcase Refresh** — `examples/tutorial.flow` + `examples/showcase.flow` exercise every v1.3 feature end-to-end (including prefix-only arithmetic, symbols, tuples, dicts); byte-identical determinism re-pinned

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
**Plans**: TBD
**UI hint**: yes

### Phase 25: Gaussian Humanize (LAST PRNG phase)
**Goal**: `humanizeGaussian()` ships as a separate function (D-04) so existing uniform `humanize()` keeps the v1.2 byte-identical determinism contract for tutorial.flow + showcase.flow (binding pre-ordering #5 — must be the LAST PRNG-touching phase)
**Depends on**: Phases 18-24 (must be the last PRNG-touching phase per Pitfall 6 mitigation)
**Requirements**: DEFER-06
**Success Criteria** (what must be TRUE):
  1. Composer can call `humanizeGaussian(seq, 0.1, 42)` and get Gaussian-distributed velocity perturbation via Box-Muller transform; same seed produces deterministic velocity bytes pinned by Fact (DEFER-06)
  2. Existing `humanize(seq, 0.1, 42)` produces identical bytes to v1.2 — uniform path UNCHANGED, byte-identical determinism contract preserved across two consecutive runs (DEFER-06)
  3. Two consecutive runs of `showcase.flow` (now including a Gaussian-humanize call site) produce cmp-clean WAV + MIDI output (DEFER-06)
**Plans**: TBD

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
**Plans**: TBD

### Phase 26.1: Symbols + Tuples + Dicts (INSERTED)
**Goal**: Three tightly-coupled language additions land together — Symbol primitive type (`#foo` syntax, interned), Tuple type (`<<a, b, c>>` literal with per-position types and arity, `~>` unpack flow operator, destructuring assignment, `@N` indexing), and generic `Dict<K, V>` with hashable keys (Int, Long, Float, String, Symbol, Note, Chord, Tuple-of-hashables). Dicts surface via builtins only (no literal syntax — preserves S-expr style and avoids `{...}` collision with Phase 19 tuplets).
**Depends on**: Phase 26 (must inherit prefix-only philosophy from Op Standardization)
**Requirements**: SYM-01, TUP-09, TUP-10, DICT-01, DICT-02, DICT-03
**Success Criteria** (what must be TRUE):
  1. Symbols: `#foo` lexes as `SymbolLiteral`; `Symbol` type registered; equality is pointer-compare (interned); usable as `Dict` key (SYM-01)
  2. Tuples: `<<C4, q>>` literal; `<<>>` empty + `<<x>>` singleton valid; type annotation `<<Note, Beat>>` mirrors literal; `tup@0`/`@1` indexing; `<<a, b>> = foo()` destructure; immutable (TUP-09)
  3. Flow ops: `~>` unpacks tuple into multi-arg call; on non-tuple LHS, `~>` behaves identically to `->` (charitable interpretation per memory) (TUP-10)
  4. Dicts: `Dict<K, V>` Java-generic style; `(dict K V K V ...)` flat constructor; `(dictTuple <<K,V>> <<K,V>> ...)` tuple-pair constructor; immutable; allowed key types: Int/Long/Float/String/Symbol/Note/Chord/Tuple-of-hashables (DICT-01)
  5. Dict ops: `(get d k)`, `(set d k v)`, `(has d k)`, `(keys d)`, `(values d)`, `(size d)`, `(merge d1 d2)`, `(remove d k)`; all mutations return new dicts (DICT-02)
  6. Iteration: `(each d callback)` yields `<<key, value>>` tuples, callable via `~>` for multi-arg lambda dispatch (DICT-03)
  7. Existing `tutorial.flow` + `showcase.flow` remain cmp-clean (no regression to byte-identical determinism)
**Plans**: TBD

### Phase 27: Tutorial + Showcase Refresh
**Goal**: `examples/tutorial.flow` and `examples/showcase.flow` demonstrate every v1.3 feature end-to-end with byte-identical determinism; v1.1 + v1.2 chapters preserved (last per v1.2 precedent — Phase 16 was the v1.2 tutorial-refresh closer)
**Depends on**: Phases 18-26.1 (every v1.3 feature must be live before tutorial can demonstrate it, including Phase 26 prefix-op standardization and Phase 26.1 symbols/tuples/dicts)
**Requirements**: QOL-04
**Success Criteria** (what must be TRUE):
  1. `examples/tutorial.flow` demonstrates tuplets `{3:2 ...}q`, fractional `C4/12`, range, multi-letter enharmonics, negative slice, `enable hAsB;` pragma, arpeggio/voicings/delay-sync/quantize/legato/portamento/varispeed-loadWav, named-tuning microtonal, scale-lint pragma, `humanizeGaussian`, prefix-only arithmetic via `(add)`/`(sub)`/`(mul)`/`(div)`, symbol `#foo`, tuple `<<a, b>>` with `~>` unpack, generic `Dict<K, V>` keyed by `Note` and `Symbol` via `(dict K V ...)` + `(get d k)` (QOL-04)
  2. Both scripts run to completion (exit 0) producing non-empty WAV + MIDI output to `examples/output/` (QOL-04)
  3. Byte-identical determinism contract holds across two consecutive runs (cmp-clean) for both `tutorial.flow` and `showcase.flow` (QOL-04)
  4. Existing v1.1 + v1.2 chapters preserved — no regressions to prior tutorial coverage (QOL-04)
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
| 24. Scale Linting (flow-lsp) | v1.3 | 0/N | Not started | - |
| 25. Gaussian Humanize (LAST PRNG phase) | v1.3 | 0/N | Not started | - |
| 26. Op Standardization (Prefix-Only) | v1.3 | 0/N | Not started | - |
| 26.1. Symbols + Tuples + Dicts | v1.3 | 0/N | Not started | - |
| 27. Tutorial + Showcase Refresh | v1.3 | 0/N | Not started | - |
