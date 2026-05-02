# Flow Language — v1.3 Requirements

**Milestone:** v1.3 Composer DX Tier B/C — Tuplets, DEFER closures, Tier B/C bundle
**Started:** 2026-04-26
**Source:** `.planning/research/SUMMARY.md` + `.planning/research/{STACK,FEATURES,ARCHITECTURE,PITFALLS}.md`

**Goal:** Close every DEFER-01..06 item carried from v1.2 and ship the Tier B/C composer DX bundle, with tuplet + arbitrary-duration note syntax as the lead capability.

REQ-ID numbering continues from v1.2 (last used: SPIKE-05, FIX-07a, TEST-04, DX-09, QOL-03). New categories `TUP-*`, `FRAC-*`, `PRAG-*`, `MICR-*`, `LINT-*`, `DICT-*` introduced this milestone.

**Locked decisions (from /gsd-new-milestone discussion):**
- D-01: Tuplet bracket syntax is `{N:M ...}` (braces)
- D-02: Pragmas are **file-scope only**, top-of-file only, NOT propagated via `use`
- D-03: Microtonal scope is **named-tunings wedge** (`enable justIntonation;` / `enable pythagorean;`); full Scala loader deferred to v1.4
- D-04: Gaussian humanize ships as a **separate `humanizeGaussian()` function** (preserves byte-identical determinism for existing uniform calls)
- D-05: MIDI TPQN cap when tuplets force auto-elevation is **9600**

---

## Active Requirements

### Foundation — Rational Duration Arithmetic

- [x] **FRAC-01
**: A new `Fraction(int Num, int Denom)` value type lives in `flow-lang/TypeSystem/`, normalizes via GCD on construction, supports addition / multiplication / equality / comparison, and never uses `double` arithmetic for tuplet duration math (Pitfall 1 mitigation). Ships with unit Facts pinning canonical examples (`1/3 + 1/3 + 1/3 == 1`, `2/4 == 1/2`, `3/12 == 1/4`).
- [x] **FRAC-02
**: `MusicalNoteData` gains optional `Fraction? DurationFraction` field that overrides the existing `DurationValue` enum when set. Existing power-of-2 path stays unchanged when the field is null. All ~70 existing `.flow` test scripts must remain byte-identical (regression gate via `cmp` on tutorial.flow + showcase.flow output).

### Tuplets & Arbitrary Fractional Durations

- [x] **TUP-01
**: A `{N:M element element element}q` tuplet bracket compiles to a `TupletElement` AST node (recursive — children are heterogeneous `NoteStreamElement`s including nested tuplets). Per D-01, brackets use `{ }`. Compiles to `MusicalNoteData` instances whose `DurationFraction` reflects the N:M ratio applied to the parent duration. Acceptance: `| {3:2 C4 D4 E4}q |` renders three notes that sum to one quarter note (i.e. each note is a duration of 1/3 quarter = 1/12 whole).
- [x] **TUP-02
**: `{N elem elem elem}` shorthand (no `:M`) defaults to the music21 convention (3-tuplet → 3:2, 5-tuplet → 5:4, 7-tuplet → 7:4 etc.). Acceptance: `{3 C4 D4 E4}q` is equivalent to `{3:2 C4 D4 E4}q`.
- [x] **TUP-03
**: Nested tuplets resolve correctly via accumulating `Fraction outerScale` propagation through the compiler. Acceptance: `| {3:2 C4 {3:2 D4 E4 F4}q G4}h |` renders 5 notes whose durations multiply through both tuplet ratios.
- [x] **TUP-04
**: `C4/N` arbitrary fractional duration syntax is accepted in note-stream context. `C4/12` is a 1/12 note (equivalent to triplet sixteenth at the appropriate tuplet bracket). Lexer disambiguates from arithmetic `/` by being inside `| ... |` note-stream context. Acceptance: `| C4/12 D4/12 E4/12 |` parses and renders three 1/12 notes.
- [x] **TUP-05
**: NoteStreamCompiler bar-fit validator accepts tuplet/fractional bars whose sum equals the time-signature value as a rational fraction (Pitfall 2 mitigation). Acceptance: `tempo 120 timesig 4/4 { | {3:2 C4 D4 E4}q {3:2 F4 G4 A4}q B4q C5q | }` validates clean (each tuplet sums to 1/4, plus 2 quarter notes = 4/4).
- [x] **TUP-06
**: MIDI export auto-elevates TPQN to `LCM(480, 2 × tuplet_denominators)`, capped at 9600 per D-05 (Pitfall 3 mitigation). Tuplets requiring TPQN > 9600 raise a clear error citing the cap. Acceptance: `{3:2 ...}` exports at TPQN=480 (480/3=160 ticks each, exact); `{5:4 ...}` at TPQN=480 (480/5=96 each, exact); `{7:8 ...}` auto-elevates to TPQN=3360 (480 × 7); `{11:13 ...}` raises a TPQN-cap error.
- [x] **TUP-07**: AUDIT-VERIFIED C5 (augment/diminish in `TransformFunctions.cs:239,261`) re-validated against tuplet-aware sequences (Pitfall 9 mitigation). New regression Fact: `augment(tupletSeq)` doubles the rational durations (each 1/12 becomes 1/6); `diminish(tupletSeq)` halves them (each 1/12 becomes 1/24).
- [x] **TUP-08
**: Per-note tuplet shorthand `C4/X:Y[suffix]` inside note streams. `C4/3:2` is one tuplet member at the 3:2 ratio (default level: quarter); `DurationFraction = suffix_fraction / X` of a whole. Optional level suffix (`w/h/q/e/s/t`, default `q`). Per-note instances are independent — mixed ratios in adjacent notes are legal. Y is preserved as the tuplet-ratio label and feeds the same TPQN auto-elevation path as bracket-form. Acceptance: `| C4/3:2 D4/3:2 E4/3:2 |` ≡ `| {3:2 C4 D4 E4}q |`; `| C4/5:4h |` = duration 1/10 whole; `| C4/0:2 |` raises parse error.

### DEFER Closures from v1.2

- [x] **DEFER-01
**: `range(Int, Int) → Array[Int]` and `range(Int, Int, Int) → Array[Int]` (with step) registered in stdlib. Standard semantics: start inclusive, end exclusive, default step=1, negative step iterates backward. Empty array when range is unsatisfiable. Acceptance: `(range 0 5)` → `[0, 1, 2, 3, 4]`; `(range 0 10 2)` → `[0, 2, 4, 6, 8]`; `(range 5 0 -1)` → `[5, 4, 3, 2, 1]`.
- [x] **DEFER-04
**: Multi-letter enharmonic edges resolved in `HarmonyFunctions.Enharmonic`: E↔Fb, F↔E#, B↔Cb, C↔B# round-trip correctly (Pitfall 10 mitigation; must precede DEFER-02/03). Acceptance: `enharmonic(E4)` → `Fb4`; `enharmonic(Fb4)` → `E4`; `enharmonic(F4)` → `E#4`; `enharmonic(E#4)` → `F4`; `enharmonic(B4)` → `Cb5`; `enharmonic(C4)` → `B#3`. Round-trip Fact: `enharmonic(enharmonic(n))` returns a note pitch-equivalent to `n` for every chromatic note.
- [x] **DEFER-05
**: `slice(Sequence/Array, start, end)` accepts negative-from-end indices Python-style. `arr@-1` returns the last element; `slice(arr, -3, _)` returns the last 3. Acceptance: `(slice [1, 2, 3, 4, 5] -3 5)` → `[3, 4, 5]`; `(slice [1, 2, 3, 4, 5] 0 -1)` → `[1, 2, 3, 4]`. **Note:** This is a behavioral change to v1.2's silent two-sided clamp (Pitfall 10). Documentation updates the slice contract; existing positive-index call sites unchanged.

### Pragma System & H-Alias

- [x] **PRAG-01**: A pragma system accepts `enable <featureName>;` declarations at the top of `.flow` files only (per D-02; lines after the first non-pragma statement raise a parse error). Lexer pre-scan extracts pragmas before main lexing (Pitfall 4 mitigation). `PragmaRegistry` is a closed set — unknown pragma names raise a clear error citing the known list.
- [x] **PRAG-02**: Pragmas do NOT propagate across `use` imports (per D-02; Pitfall 4 mitigation). Acceptance Fact: importing a module that uses `enable hAsB;` does NOT enable `hAsB` in the importing file unless the importing file also declares it.
- [x] **DEFER-02/03**: `enable hAsB;` pragma activates `H` as a `B` alias inside note-stream context (`| ... |`) only. `H4q` parses identically to `B4q`. Outside note streams, `H` remains a usable identifier (`Int H = 5;` continues to compile). Acceptance: `enable hAsB; ... | H4q B4q |` produces two identical notes; `Int H = 5;` continues to compile.

### Tier B/C Composer DX

- [x] **DX-10**: `arpeggio(chord, rate, direction, pattern)` extends existing `arpeggio` with rate (NoteValue or Fraction) + direction (`"up" / "down" / "updown" / "downup" / "random"`) + pattern (`"linear" / "chord-tone" / "scale-tone"`). Acceptance: `(arpeggio Cmaj7 q "up" "linear")` produces the expected 4-note ascending arpeggio at quarter-note rate.
- [ ] **DX-11**: Chord inversions and voicings via `inversion(chord, n)` and `voicing(chord, "drop2" | "drop3" | "open" | "close" | "spread")`. Acceptance: `inversion(Cmaj, 1)` returns `[E4, G4, C5]` (first inversion); `voicing(Cmaj7, "drop2")` lowers the 2nd-from-top note by an octave.
- [ ] **DX-12**: `delay(buffer, noteValueRate, feedback, mix)` overload accepts a NoteValue (or Fraction) as the delay time, computed from active tempo (Pitfall 1 — uses Fraction for sync math). Existing ms-rate overload stays unchanged. Acceptance: `tempo 120 { ... delay(buf, e, 0.5, 0.4) ... }` produces an eighth-note-synced delay (250ms at 120 BPM).
- [ ] **DX-13**: `quantize(sequence, resolution, strength, swing)` snaps note onsets to a grid. Resolution is a NoteValue or Fraction; strength is 0–1 (0=no quantize, 1=hard quantize); swing is -1 to 1. Acceptance: pre-humanized euclidean output snaps cleanly to a 1/16 grid at strength=1.
- [ ] **DX-14**: Legato and portamento articulations: `legato(sequence, overlap)` extends note durations by overlap factor; `portamento(sequence, glideTime)` emits MIDI CC65 (portamento on/off) + CC5 (portamento time) per Sweetwater MIDI spec. Acceptance: MIDI export of `portamento(seq, 100ms)` includes CC65=127 + CC5=64-ish events.
- [ ] **DX-15**: `loadWav(path, semitones)` and `loadWav(path, ratio)` overloads varispeed-pitch-shift the loaded buffer via OLA + linear/sinc resample. Existing `loadWav(path)` unchanged (defaults to 0 semitones / ratio 1.0). Acceptance: `loadWav("kick.wav", 12)` returns a buffer one octave higher (sample count halved, frequency doubled) compared to `loadWav("kick.wav")`.

### Microtonal Tuning (Wedge)

- [ ] **MICR-01**: Per D-03, three named tunings ship via pragma: `enable justIntonation;` (5-limit JI), `enable pythagorean;` (3-limit), `enable equalTemperament;` (12-TET, default — explicit form for clarity). When active, `Note → frequency` lookup at `PitchConversion.NoteToFrequency` consults the active tuning system instead of the hard-coded `2^((n-69)/12)·440Hz`. Pragma is file-scope per D-02. Acceptance: `enable justIntonation; ...` followed by `play(C4 E4)` produces frequency ratio 5:4 (1.25) instead of 12-TET ~1.2599 (`Math.Pow(2, 4/12)`).
- [ ] **MICR-02**: Tuning system applies at render-time only (Pitfall 5 mitigation). Existing `transpose`, `invert`, `retrograde`, `augment`, `diminish` transforms remain pitch-class-based and tuning-agnostic. Acceptance: `transpose(seq, 5)` produces the same MIDI pitch numbers under every tuning; only the rendered frequencies differ.
- [ ] **MICR-03**: Full Scala (`.scl`) loader documented as deferred to v1.4. Pragma registry rejects unknown tunings with a clear error pointing at the documented future expansion.

### Scale Linting (flow-lsp only)

- [ ] **LINT-01**: Per D-02, `enable scaleLint;` pragma activates flow-lsp scale linting. When active, flow-lsp emits `Diagnostic { Severity = Information }` for any note in a `key Cmajor { ... }` context that is non-diatonic. Existing diagnostic plumbing reused — zero flow-lang touch. Acceptance: editing `key Cmajor { | C4 D4 E4 F#4 G4 | }` shows an Information-severity squiggle on `F#4`.
- [ ] **LINT-02**: Scale linting is opt-in (Pitfall 8 mitigation — never default-on). Without `enable scaleLint;`, flow-lsp emits zero scale-lint diagnostics. Acceptance Fact: a key-block with non-diatonic notes produces zero scale-lint diagnostics when the pragma is absent.
- [ ] **LINT-03**: Scale linting respects nested key contexts (key inside key inside section). Innermost active key wins for diagnostic computation. Acceptance: `key Cmajor { key Aminor { | F#4 | } }` does NOT flag F#4 (Aminor is the innermost active key, F# is non-diatonic in C major but... actually F is diatonic in Aminor; replace with realistic example in plan time).

### Gaussian Humanize (LAST PRNG phase)

- [ ] **DEFER-06**: Per D-04, a new `humanizeGaussian(sequence, amount, seed)` built-in applies Gaussian-distributed velocity perturbation via Box-Muller transform. Existing `humanize(...)` (uniform) UNCHANGED — preserves v1.2 byte-identical determinism contract for tutorial.flow + showcase.flow (Pitfall 6 mitigation). Acceptance: `humanizeGaussian(seq, 0.1, 42)` with seed=42 produces deterministic velocity bytes pinned by Fact; existing `humanize(seq, 0.1, 42)` produces identical bytes to v1.2.

### Dictionary Support

- [ ] **DICT-01**: A new `Dict[T]` type with String keys lives in `flow-lang/TypeSystem/SpecialTypes/`. S-expression style constructor `(dict "k1" v1 "k2" v2 ...)` builds a dict from interleaved key-value pairs (per memory: "Keep functional S-expression style, no infix operators"). Empty dict via `(dict)`. Type inference: value type T inferred from first value or annotated explicitly via `Dict[Note]`. Acceptance: `(dict "kick" C2 "snare" D4)` returns a `Dict[Note]` with size 2; `(dict)` returns an empty `Dict[Void]` or properly-inferred type.

- [ ] **DICT-02**: Lookup and update operations: `(get d "k")` returns the value at key `"k"` or `Nothing` if missing (per CLAUDE.md charitable-interpretation memory — silent-and-documented over errors). `(getOr d "k" defaultValue)` returns default when key missing. `(set d "k" v)` returns a NEW dict with key updated (immutable update — Flow's record-style data model). `(remove d "k")` returns a new dict without that key. Missing-key behavior is not an error. Acceptance: `(get (dict "a" 1) "a")` → `1`; `(get (dict "a" 1) "missing")` → `Nothing`; `(set (dict "a" 1) "a" 2)` returns a new dict with `(get _ "a")` → `2`.

- [ ] **DICT-03**: Introspection and iteration: `(keys d)` → `Array[String]`, `(values d)` → `Array[T]`, `(has d "k")` → `Bool`, `(size d)` → `Int`. Functional iteration: `(each d (fn String key, T value => ...))` walks every entry in INSERTION ORDER (not hash order — preserves byte-identical determinism contract). `(map d (fn String key, T value => ...))` returns a new dict with values transformed. Acceptance: `(keys (dict "a" 1 "b" 2))` → `["a", "b"]` (insertion order); `(values _)` → `[1, 2]`; `(has _ "a")` → `true`; `(size _)` → `2`.

### Quality of Life

- [ ] **QOL-04**: `examples/tutorial.flow` and `examples/showcase.flow` refreshed to demonstrate every v1.3 feature end-to-end: tuplets `{3:2 ...}q`, fractional `C4/12`, range, multi-letter enharmonics (E↔Fb, F↔E#, B↔Cb, C↔B#), negative slice, `enable hAsB;` pragma, arpeggio/voicings/delay-sync/quantize/legato/portamento/varispeed-loadWav, named-tuning microtonal, scale-lint pragma, `humanizeGaussian`, **dict** `(dict "k" v)` + `(get d "k")`. Both scripts run to completion (exit 0) producing non-empty WAV + MIDI; byte-identical determinism contract holds across two consecutive runs (cmp-clean). Existing v1.1 + v1.2 chapters preserved.

---

## Future Requirements (deferred)

- **Full Scala (`.scl`) loader** — `tuning loadScala("path.scl") { ... }` musical-context block; deferred to v1.4 per D-03 (heavy: 18+ file blast radius for arbitrary tuning systems)
- **Phase-vocoder time-preserving pitch shift** for loadWav — explicit anti-feature for v1.3 (no clean single-file pure-C# implementation; varispeed-only ships in DX-15)
- **Auto-derived chord-tone / scale-tone arpeggio sequencing** beyond the basic `pattern` enum in DX-10
- **Block-scope pragmas** — deferred per D-02; file-scope only in v1.3
- **Audit §2 hardening** — overload ambiguity, bandpass Q unbounded, stereo voices played as mono, ChordParser sharp formatting, scale database brittleness, OverloadResolver top-2 tie check
- **Pidgin parser combinator dependency removal** — referenced but unused in csproj; opportunistic cleanup

## Out of Scope (for v1.3)

- v1.2 open audit items (debug session `function-overload-resolution-failures`, quick task pure-Flow test library, Phase 17 HUMAN-UAT 3 rows, Phase 04 verification gaps) — recorded in STATE.md Deferred Items, NOT pulled into v1.3 unless user explicitly opts in
- ABC `(p:q:r` counter-form tuplet syntax — anti-feature (bracket parens make `r` redundant)
- Default-on scale linting — anti-feature (composers expect non-diatonic notes by design)
- Global `H` lexer alias outside note streams — anti-feature (would break user identifiers)
- NAudio/CSCore/NWaves integration — minimal-deps philosophy stands
- New NuGet packages of any kind — confirmed unnecessary by SUMMARY.md research
- GUI/DAW interface, VST/AU hosting, multi-user collaboration, cloud deploy — project-level out-of-scope, unchanged from v1.2

---

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| FRAC-01 | Phase 18 | Shipped 2092f32 |
| FRAC-02 | Phase 18 | Shipped ba8534a |
| TUP-01 | Phase 19 | Shipped a7f94ef |
| TUP-02 | Phase 19 | Shipped a7f94ef |
| TUP-03 | Phase 19 | Shipped a7f94ef |
| TUP-04 | Phase 19 | Shipped 9aae23c |
| TUP-05 | Phase 19 | Shipped 3679ab4 |
| TUP-06 | Phase 19 | Shipped dbc6f30 |
| TUP-07 | Phase 19 | Shipped e2cdbe5 |
| TUP-08 | Phase 19 | Shipped 9aae23c |
| DEFER-01 | Phase 20 | Shipped d0d17db |
| DEFER-04 | Phase 20 | Shipped d835336 |
| DEFER-05 | Phase 20 | Shipped edd20b1 |
| PRAG-01 | Phase 21 | Shipped 60f7f18 |
| PRAG-02 | Phase 21 | Shipped 60f7f18 |
| DEFER-02/03 | Phase 21 | Shipped 05c2174 |
| DX-10 | Phase 22 | Complete |
| DX-11 | Phase 22 | Pending |
| DX-12 | Phase 22 | Pending |
| DX-13 | Phase 22 | Pending |
| DX-14 | Phase 22 | Pending |
| DX-15 | Phase 22 | Pending |
| MICR-01 | Phase 23 | Pending |
| MICR-02 | Phase 23 | Pending |
| MICR-03 | Phase 23 | Pending |
| LINT-01 | Phase 24 | Pending |
| LINT-02 | Phase 24 | Pending |
| LINT-03 | Phase 24 | Pending |
| DEFER-06 | Phase 25 | Pending |
| DICT-01 | Phase 26 | Pending |
| DICT-02 | Phase 26 | Pending |
| DICT-03 | Phase 26 | Pending |
| QOL-04 | Phase 27 | Pending |

---

## Notes

- Phase numbering continues from v1.2 (last phase: 17). v1.3 starts at Phase 18.
- Five binding pre-ordering constraints from PITFALLS map into roadmap shape:
  1. FRAC-* MUST precede TUP-* (rational arithmetic before tuplet syntax) → Phase 18 → Phase 19
  2. PRAG-* MUST precede DEFER-02/03 (H-alias) AND LINT-* (scale lint) → Phase 21 → DEFER-02/03 in 21, LINT in Phase 24
  3. Audit/spike DEFER-04 MUST precede DEFER-02/03 (multi-letter enharmonics before H-alias) → Phase 20 (DEFER-04) → Phase 21 (DEFER-02/03)
  4. MICR-* MUST be its own phase (highest blast radius — even with wedge scope) → Phase 23
  5. DEFER-06 (Gaussian) MUST be the LAST PRNG-touching phase (byte-identical determinism) → Phase 25 (after all other PRNG-touching phases close)
