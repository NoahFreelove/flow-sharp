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

- [x] **DX-10**: `arpeggio(chord, rate, direction, pattern)` extends existing `arpeggio` with rate (NoteValue or Fraction) + direction (`"up" / "down" / "updown" / "downup" / "random"`) + pattern (`"linear" / "chord-tone" / "scale-tone"`). Acceptance: `(arpeggio Cmaj7 q "up" "linear")` produces the expected 4-note ascending arpeggio at quarter-note rate. — Shipped 6500412
- [x] **DX-11**: Chord inversions and voicings via `inversion(chord, n)` and `voicing(chord, "drop2" | "drop3" | "open" | "close" | "spread")`. Acceptance: `inversion(Cmaj, 1)` returns `[E4, G4, C5]` (first inversion); `voicing(Cmaj7, "drop2")` lowers the 2nd-from-top note by an octave. — Shipped 5fba059
- [x] **DX-12**: `delay(buffer, noteValueRate, feedback, mix)` overload accepts a NoteValue (or Fraction) as the delay time, computed from active tempo (Pitfall 1 — uses Fraction for sync math). Existing ms-rate overload stays unchanged. Acceptance: `tempo 120 { ... delay(buf, e, 0.5, 0.4) ... }` produces an eighth-note-synced delay (250ms at 120 BPM). — Shipped 98da48e
- [x] **DX-13**: `quantize(sequence, resolution, strength, swing)` snaps note onsets to a grid. Resolution is a NoteValue or Fraction; strength is 0–1 (0=no quantize, 1=hard quantize); swing is -1 to 1. Acceptance: pre-humanized euclidean output snaps cleanly to a 1/16 grid at strength=1. — Shipped d3f5350
- [x] **DX-14**: Legato and portamento articulations: `legato(sequence, overlap)` extends note durations by overlap factor; `portamento(sequence, glideTime)` emits MIDI CC65 (portamento on/off) + CC5 (portamento time) per Sweetwater MIDI spec. Acceptance: MIDI export of `portamento(seq, 100ms)` includes CC65=127 + CC5=64-ish events. — Shipped d2bde5d
- [x] **DX-15**: `loadWav(path, semitones)` and `loadWav(path, ratio)` overloads varispeed-pitch-shift the loaded buffer via OLA + linear/sinc resample. Existing `loadWav(path)` unchanged (defaults to 0 semitones / ratio 1.0). Acceptance: `loadWav("kick.wav", 12)` returns a buffer one octave higher (sample count halved, frequency doubled) compared to `loadWav("kick.wav")`. — Shipped 95582e7

### Microtonal Tuning (Wedge)

- [x] **MICR-01**: Per D-03, three named tunings ship via pragma: `enable justIntonation;` (5-limit JI), `enable pythagorean;` (3-limit), `enable equalTemperament;` (12-TET, default — explicit form for clarity). When active, `Note → frequency` lookup at `PitchConversion.NoteToFrequency` consults the active tuning system instead of the hard-coded `2^((n-69)/12)·440Hz`. Pragma is file-scope per D-02. Acceptance: `enable justIntonation; ...` followed by `play(C4 E4)` produces frequency ratio 5:4 (1.25) instead of 12-TET ~1.2599 (`Math.Pow(2, 4/12)`). — Shipped f6b00ba
- [x] **MICR-02**: Tuning system applies at render-time only (Pitfall 5 mitigation). Existing `transpose`, `invert`, `retrograde`, `augment`, `diminish` transforms remain pitch-class-based and tuning-agnostic. Acceptance: `transpose(seq, 5)` produces the same MIDI pitch numbers under every tuning; only the rendered frequencies differ. — Shipped 8190fb2
- [x] **MICR-03**: Full Scala (`.scl`) loader documented as deferred to v1.4. Pragma registry rejects unknown tunings with a clear error pointing at the documented future expansion. — Shipped 47d7718

### Scale Linting (flow-lsp only)

- [x] **LINT-01**: Per D-02, `enable scaleLint;` pragma activates flow-lsp scale linting. When active, flow-lsp emits `Diagnostic { Severity = Information }` for any note in a `key Cmajor { ... }` context that is non-diatonic. Existing diagnostic plumbing reused — zero flow-lang touch. Acceptance: editing `key Cmajor { | C4 D4 E4 F#4 G4 | }` shows an Information-severity squiggle on `F#4`. — Shipped Phase 24 plans 24-00..24-04
- [x] **LINT-02**: Scale linting is opt-in (Pitfall 8 mitigation — never default-on). Without `enable scaleLint;`, flow-lsp emits zero scale-lint diagnostics. Acceptance Fact: a key-block with non-diatonic notes produces zero scale-lint diagnostics when the pragma is absent. — Shipped Phase 24 plans 24-00..24-04
- [x] **LINT-03**: Scale linting respects nested key contexts (key inside key inside section). Innermost active key wins for diagnostic computation. Acceptance: `key Cmajor { key Gmajor { | F#4 | } }` does NOT flag F#4 (Gmajor is the innermost active key, F# is diatonic in Gmajor). — Shipped Phase 24 plans 24-00..24-04

### Gaussian Humanize (LAST PRNG phase)

- [x] **DEFER-06**: Per D-04, a new `humanizeGaussian(sequence, amount, seed)` built-in applies Gaussian-distributed velocity perturbation via Box-Muller transform. Existing `humanize(...)` (uniform) UNCHANGED — preserves v1.2 byte-identical determinism contract for tutorial.flow + showcase.flow (Pitfall 6 mitigation). Acceptance: `humanizeGaussian(seq, 0.1, 42)` with seed=42 produces deterministic velocity bytes pinned by Fact; existing `humanize(seq, 0.1, 42)` produces identical bytes to v1.2. — Shipped Phase 25 plans 25-00..25-04

### Operator Standardization

- [x] **STD-01**: Parser/AST cleanup. `BinaryExpression` record + `BinaryOperator` enum deleted from `flow-lang/Ast/Expressions/`. `ParseAdditive`/`ParseMultiplicative` methods removed from `Parser.cs`. `ParseUnary`'s arithmetic branch deleted; `ParseUnaryShorthand` handles D-01 `-IDENT → (neg IDENT)` and D-03 silent `+IDENT` strip. `EvaluateBinary` + its switch case deleted from `ExpressionEvaluator.cs`. Music-context Plus/Minus consumers (tempo/swing/pan/gain/reverbTime) PRESERVED. Acceptance: bare infix produces a parse error pointing the user at `(add)`/`(sub)`/`(mul)`/`(div)` per `InfixRejectedFacts`. — Shipped 86fa69a

- [x] **STD-02**: Builtin completion + lexer single-token negative literals. `(add)`/`(sub)`/`(mul)`/`(div)` ship 5 same-type overloads each (Int, Long, Float, Double, Number — D-05 fast paths via direct CLR primitives). `(neg)` ships 5 per-type overloads (D-07). `(idiv Int Int) → Int` ships per D-08. `(div Int Int)` auto-promotes to Double per D-08. Negative number literals `-5`/`-3.14` lex as single tokens at expression-start positions per D-02/D-04. Music-context keywords (tempo/swing/pan/gain/reverbTime) EXCLUDED from gate so `pan -0.5` continues to work (Pitfall 1). `(concat String String)` ships for explicit string concatenation. — Shipped 86fa69a

- [x] **STD-03**: Migrate all in-repo `.flow` files to prefix form; preserve in-session byte-identical `showcase.flow` output; CLAUDE.md updated. Throwaway tokenizer-based migration script at `scripts/Migrate26/`. 8 tracked `.flow` files migrated atomically (2d3efe1). Showcase WAV+MID byte-identical pre/post in-session; Phase 18/23/25 ByteIdenticalShowcase + DefaultTuning + ShowcaseGaussian xUnit guards GREEN. Tutorial-side guard FAILs are blocked by a pre-existing `(str Int[])` overload-coercion bug (orthogonal to Wave 3) — deferred. CLAUDE.md line 148 lambda example rewritten to prefix; line 175 BinaryExpression AST row deleted; new "Prefix-only arithmetic" bullet under Core Language Features. — Shipped 2d3efe1

### Symbols, Tuples, and (unpack)

- [x] **SYM-01**: A new `Symbol` primitive type lives in `flow-lang/TypeSystem/PrimitiveTypes/`. Lexer recognizes `#identifier` as a `SymbolLiteral` token and produces a `SymbolLiteralExpression` AST node. Equality is pointer-compare via global interning — `(eq #foo #foo)` is true on identical interns; `(eq #foo "foo")` is **false** (strict separation from String per discussion 2026-05-09 — Symbol's reason to exist IS the type distinction). Hashable; usable as `Dict<Symbol, V>` key. Acceptance: `(eq #foo #foo)` → true; `(eq #foo #bar)` → false; `(eq #foo "foo")` → false; `Dict<Symbol, Int> d = (dict #kick 60 #snare 70); (get d #kick)` → 60.

- [x] **TUP-09**: A new `Tuple` type lives in `flow-lang/TypeSystem/SpecialTypes/` with per-position types and arity. Literal syntax `<<a, b, c>>` (with `<<>>` empty + `<<x>>` singleton both valid). Type annotation `Tuple<<Note, Beat>>` mirrors literal. `tup@N` indexing matches the existing array-index `@` syntax (charitable per memory) with compile-time bounds checking when arity is known. Destructuring assignment `<<Note pitch, Beat dur>> = expr` works (proc/lambda parameter destructuring deferred to a later phase). Tuples are immutable; equality is structural (`<<1, 2>> == <<1, 2>>` is true). Tuple-of-hashables is a valid Dict key when every component is hashable; component-hashability rejection at type-check time. Acceptance: `<<C4, q>>` parses as `Tuple<<Note, Beat>>`; `<<>>@0` is a compile error; `<<C4>>@0` returns C4; `<<a, b>> = <<1, 2>>` binds a=1 b=2; `(eq <<1, 2>> <<1, 2>>)` → true.

- [x] **TUP-10**: New flow operator `~>` unpacks a tuple into a multi-arg call as a parse-time transform. `tup ~> func(extra)` becomes `func(tup@0, tup@1, ..., extra)` at parse time. On non-tuple LHS, `~>` falls through to behave identically to `->` (charitable per memory `feedback_charitable_interpretation`). Acceptance: given `proc add3(Int a, Int b, Int c)`, `<<1, 2, 3>> ~> add3` calls `(add3 1 2 3)`; given `Int x = 5`, `x ~> doubleIt` calls `(doubleIt 5)` (non-tuple → `->` semantics).

- [x] **TUP-11**: A new `(unpack tuple func)` runtime builtin applies an unpacked tuple to a function value — the S-expression-style first-class equivalent of `~>`. Mirrors Lisp/Scheme's `(apply f args)`. Ships **alongside** `~>`, not as a replacement; `~>` shines in chain syntax, `(unpack)` shines in dynamic-dispatch and HOF-composition patterns where the function is a `Function`-typed value. Type-checks the tuple's per-position types against the function's parameter types when both are statically known. Acceptance: `(unpack <<>> getFortyTwo)` → 42; `(unpack <<5>> doubler)` → 10; `(unpack <<C4, q>> renderHit)` ≡ `<<C4, q>> ~> renderHit`; `Function f = (get handlers eventType); (unpack event f)` works when `f` is a runtime `Function` value (dynamic dispatch). Implementation: ~30 LOC + 4-theory regression Fact (zero-arg, single-arg, multi-arg, dynamic-Function-value).

### Dictionary Support

- [x] **DICT-01**: A new generic `Dict<K, V>` type lives in `flow-lang/TypeSystem/SpecialTypes/`. Allowed key types are an 8-element allowlist: Int, Long, Float, String, Symbol, Note, Chord, Tuple-of-hashables (recursive — every component must be hashable). Disallowed key types are rejected at parse-time at the annotation site with a `ParseException` citing the allowlist. S-expression constructors: `(dict K V K V ...)` flat interleaved + `(dictTuple <<K,V>> <<K,V>> ...)` tuple-pair (memory: "Keep functional S-expression style, no infix operators"). Empty dict via `(dict)`. Type inference: `Dict<K, V>` annotation specifies K and V; runtime constructor narrows to the actual element types. Acceptance: `(dict #kick 90 #snare 70)` returns a `Dict<Symbol, Int>` with size 2; `Dict<Buffer, Int> bad = ...` raises a parse error. — Shipped daaa023

- [x] **DICT-02**: 14-op dict surface, all immutable (mutations return new dicts). `(get d k)` returns the value at `k` or `Value.Void()` (Flow's "Nothing" sentinel) when absent. `(getOr d k default)` returns `default` when absent. `(set d k v)` returns a NEW dict with `k → v`. `(remove d k)` returns a new dict without `k`. `(has d k)` → `Bool`. `(keys d)` → `Array[K]` in insertion order. `(values d)` → `Array[V]` in insertion order. `(size d)` → `Int`. `(merge d1 d2)` last-write-wins (d2 keys override d1). NaN-key special-case scoped to Dict-internal equality only (Float NaN as self) — Flow's general `(equals nan nan)` continues to follow IEEE 754 (returns false). Missing-key behavior is not an error per the charitable-interpretation memory. Acceptance: `(get (dict "kick" 1) "missing")` → `Nothing`; `(set d "kick" 2)` returns NEW dict, original `d` unchanged; `(merge (dict #a 1) (dict #a 2 #b 3))` → size 2, get #a returns 2. — Shipped daaa023

- [x] **DICT-03**: Functional iteration + introspection: `(each d cb)` yields `<<key, value>>` per entry and invokes the callback via `~>` semantics — the dict-side internally unpacks the tuple into 2 positional args so the user writes a normal `(fn Symbol k, Int v => ...)` 2-arg lambda (no lambda-side destructuring). `(map d cb)` returns `Dict<K, V'>` with values transformed (keys preserved). `(filter d pred)` returns `Dict<K, V>` with entries where `pred(K, V) → true`. INSERTION ORDER preserved across all ops (not hash order — preserves byte-identical determinism contract). Acceptance: `(keys (dict "kick" 1 "snare" 2 "hihat" 3))` → `["kick", "snare", "hihat"]` in insertion order; `(each)` over Dict invokes 2-arg lambda; Pitfall 6 — separate `(each Dict Function)` overload coexists with existing `(each Array Function)`. — Shipped daaa023

### Music Type Ergonomics + FX Overloads

- [x] **ERG-01**: Music-type numeric compatibility completeness. `Millisecond` and `Second` ship `IsCompatibleWith(Double|Float)` overrides (mirroring the existing `CentType.cs:24-27` precedent that `Decibel` and `Beat` adopted via QUICK-260504-w24). `Semitone` STAYS Int-only — semitones are whole-numbers-by-design; fractional pitch shifts go through `Cent`. Existing `Millisecond.CanConvertTo(Second)` + `Second.CanConvertTo(Millisecond)` cross-conversions PRESERVED — `(delay buf 0.1s ...)` continues to reach `delay(Buffer, Millisecond, ...)` via convertible-score 100. Acceptance: `MillisecondType.Instance.IsCompatibleWith(DoubleType.Instance)` returns true; `SecondType.Instance.IsCompatibleWith(FloatType.Instance)` returns true; `SemitoneType.Instance.IsCompatibleWith(DoubleType.Instance)` returns false (D-03 canary); `(delay buf 100.0 0.5 0.4)` and `(delay buf 100ms 0.5 0.4)` both resolve. — Shipped 4f92c24

- [x] **ERG-02**: FX overload registration on every site where the parameter is conceptually musical. New music-typed overloads ship for: `delay(Buffer, Millisecond, Double, Double)`, `compress(Buffer, Decibel, Double, Millisecond, Millisecond)`, `sidechain(Buffer, Buffer, Decibel, Double, Millisecond, Millisecond)`, `reverb(Buffer, Double, Second)`, `lowpass(Buffer, Hertz)` / `highpass(Buffer, Hertz)` / `bandpass(Buffer, Hertz, Hertz)`, `createSineTone(Double, Hertz, Double)` (C# overload), and Flow-side proc overloads for `createSawTone` / `createSquareTone` / `createTriangleTone` with Hertz frequency parameter. Bare-Double overloads PRESERVED — coexist via OverloadResolver exact-match scoring (1000 vs compat-500). Reverb-Second overload does NOT ambiguate with `reverb(Buffer, Double, Double)` per RESEARCH Pitfall 3 score arithmetic. Acceptance: `(compress buf -12dB 4.0 5ms 100ms)` produces per-sample-identical output to `(compress buf -12.0 4.0 5.0 100.0)` within 1e-6f; `(reverb buf 0.5 1.5)` and `(reverb buf 0.5 1.5s)` resolve to distinct overloads (no Ambiguous overload error). — Shipped dfbfa1f

- [x] **ERG-03**: `gain` dB-vs-linear policy decided + new `volume(Buffer, Double)` shipping. `gain` STAYS dB-only — both `gain(Buffer, Double)` (existing) and `gain(Buffer, Decibel)` (existing, shipped via QUICK-260504-w24 + audio.flow forward decl shipped here) treat second arg as decibels. New `volume(Buffer, Double)` treats second arg as linear multiplier (0.5 = half-amplitude, 2.0 = double-amplitude). Function name documents the unit; composer chooses by semantic intent. ONE overload — Float / Int / Long inputs reach it via existing primitive widening chain. Negative values rejected via `InvalidOperationException` (volume can't phase-invert; out of scope). Clipping warning emitted to stderr when post-multiplication samples exceed 1.0 (mirrors GainEffect shape). NO educational hint when `(gain buf 0.5)` is called with arg in `(0, 1)` — `(gain buf 0.5)` is a legitimate 0.5dB attenuation. CLAUDE.md updated to document the split. Acceptance: `(volume buf 0.5)` halves amplitude per non-zero sample; `(volume buf 2.0)` doubles + emits clipping warning; `(volume buf -0.5)` errors with InvalidOperationException; `(gain buf 0.5)` STAYS at 0.5dB attenuation (~5.9% louder than 0dB unity), NOT 50% as a linear interpretation would produce. — Shipped 6df301e

- [x] **ERG-04**: `Hertz` type ships with `Hz` + `kHz` literal syntax + filter / generator overloads. New `HertzType` in `flow-lang/TypeSystem/SpecialTypes/HertzType.cs` mirrors `CentType` exactly (sealed FlowType singleton, `IsCompatibleWith(Double|Float)`, `GetSpecificity()=144` unique among music types). Stored as a single canonical Hz double (kHz × 1000 at lex time — no unit-discriminator at runtime). Both `Hz` and `kHz` suffixes lex as single `HertzLiteral` tokens via three coordinated paths in `SimpleLexer.cs`: `ScanNumberOrSpecialLiteral` (unsigned), `TryLookAheadSpecialLiteral` (signed prefix), and the `TryLexAngleAngle` predecessor set bumped to include HertzLiteral so `<<800Hz, 1200Hz>>` tuples parse. `mHz` (millihertz) NOT shipped in 26.2 — defer until LFOs land. Hertz overload coverage spans filters (`lowpass` / `highpass` / `bandpass`) + signal generators (`createSineTone` C#; `createSawTone` / `createSquareTone` / `createTriangleTone` Flow-side proc overloads in audio.flow). NO PitchConversion APIs added — Open Question #1: `noteToFrequency` only RETURNS Hz, doesn't take Hz; no Hz-taking PitchConversion API exists. Acceptance: `Hertz freq = 800Hz; (eq freq 800.0)` is true; `Hertz freq = 1.5kHz; (eq freq 1500.0)` is true; `(lowpass buf 800Hz)` produces per-sample-identical output to `(lowpass buf 800.0)`. — Shipped f12d648 + d655c65 + 28158cc + dfbfa1f + 821e9d0

- [x] **ERG-05**: `-12dB` / `-100ms` / `-50c` / `+440Hz` literal lexing at expression-start positions closure (D-14). Root cause traced via RESEARCH Pitfall 1 to a missing `if (targetType is DoubleType) return Double(doubleVal);` arm in `Value.cs:155-161` (NOT a lexer bug as the original CONTEXT hypothesized). Defence-in-depth fix: (a) `Value.ConvertTo` Double-arm patch shipped in Wave 0 covers Decibel→Double / Beat→Double / Cent→Double / Ms→Double / Sec→Double / Hertz→Double coercion in any user-proc / lambda call site; (b) `audio.flow` `internal proc gain(Buffer: buffer, Decibel: gainDb)` forward declaration shipped in Wave 2 surfaces the dormant C# `gain(Buffer, Decibel)` registration (RESEARCH Pitfall 2) so the dedicated Decibel overload now wins resolution at exact-match score 1000. Both fix paths exist for redundancy. The 2 pre-existing failing `DecibelBeatNumericCompatFacts` (`GainWithDecibelLiteral_…` + `GainWithPositiveDecibelLiteral_…`) flip RED→GREEN. Sibling regression facts pin `+6dB`, `-100ms`, `-50c`, `+440Hz` at the `LParen-after` expression-start position. Acceptance: `(gain src -12dB)` produces per-sample-identical output to `(gain src -12.0)` within 1e-6f; `(transpose seq -50c)` resolves cleanly (Cent canary); `(lowpass buf +440Hz)` resolves cleanly. — Shipped 45b01fb + 28158cc

### Quality of Life

- [x] **QOL-04**: `examples/tutorial.flow` and `examples/showcase.flow` refreshed to demonstrate every v1.3 feature end-to-end. Language additions: prefix-only arithmetic via `(add)`/`(sub)`/`(mul)`/`(div)`/`(idiv)`/`(neg)`/`(concat)` (Phase 26 STD-01..03); `Symbol` primitive `#foo` (Phase 26.1 SYM-01); `Tuple <<a, b, c>>` literal + `tup@N` indexing + destructuring assignment + `~>` flow op + `(unpack)` runtime (Phase 26.1 TUP-09/10/11); generic `Dict<K, V>` 14-op surface — flat `(dict K V K V)` + tuple-pair `(dictTuple <<K,V>> ...)` constructors + `get`/`getOr`/`set`/`remove`/`has`/`keys`/`values`/`size`/`merge`/`each`/`map`/`filter` (Phase 26.1 DICT-01/02/03). Music features: tuplets `{3:2 ...}q` bracket + `{3 ...}q` shorthand + per-note `C4/12` fractional + `C4/3:2` per-note tuplet shorthand + nested tuplets (Phase 19 TUP-01..08); `range(Int, Int)` / `range(Int, Int, Int)` (Phase 20 DEFER-01); multi-letter enharmonics E↔Fb / F↔E# / B↔Cb / C↔B# (Phase 20 DEFER-04); negative slice `arr@-1` / `(slice arr -3 _)` Python-style (Phase 20 DEFER-05); `enable hAsB;` H-as-B alias pragma (Phase 21 PRAG-01/02 + DEFER-02/03); DX-10..15 composer DX bundle — `arpeggio` rate/direction/pattern, chord `inversion`+voicings, NoteValue-rate `delay` overload, `quantize` to grid, `legato`/`portamento`, varispeed-`loadWav` (Phase 22); microtonal pragmas `enable justIntonation;` / `pythagorean;` / `equalTemperament;` (Phase 23 MICR-01..03); scale-lint pragma `enable scaleLint;` print-only mention (Phase 24 LINT-01..03 — flow-lsp owns surface); `humanizeGaussian(seq, amount, seed)` Gaussian-bell velocity perturbation (Phase 25 DEFER-06). Phase 26.2 surface: `volume(Buffer, Double)` linear-multiplier alongside `gain` dB-only split (ERG-03); Hertz literals `440Hz` / `1.5kHz` with kHz canonical-Hz lex (ERG-04); Ms-typed FX overloads on `delay`/`compress`/`sidechain` (ERG-02); Second-decay `(reverb buf mix 1.5s)` (ERG-02); Hertz overloads on `lowpass`/`highpass`/`bandpass` filters + `createSineTone` signal-generator (ERG-04 — runnable demo for createSineTone-Hertz; the same Hertz overload pattern applies mechanically to `createSawTone`/`createSquareTone`/`createTriangleTone`, not separately demoed in tutorial); `(gain buf -12dB)` literal at expression-start positions (ERG-05); `Millisecond.IsCompatibleWith(Double|Float)` + `Second.IsCompatibleWith(Double|Float)` numeric-compat completeness (ERG-01). Companion files under `examples/pragmas/`: `h_alias.flow` (~38 lines, `enable hAsB;` demo) + `microtonal_ji.flow` (~42 lines, `enable justIntonation;` demo with frequency-ratio comparison print). Both tutorial + showcase scripts run to completion (exit 0) producing non-empty WAV + MIDI; byte-identical determinism contract holds across two consecutive runs (cmp-clean) — `Phase18ByteIdenticalTutorialTests` + `Phase18ByteIdenticalShowcaseTests` + `Phase25ByteIdenticalShowcaseGaussianTests` + new `Phase27ByteIdenticalPragmaTests` (4 facts pinning `h_alias.flow` + `microtonal_ji.flow` run-twice identity). CLAUDE.md Music Types Quick Reference table appended for composer + future-agent reference. v1.1 + v1.2 chapters preserved. — Shipped ace6416

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
| DX-10 | Phase 22 | Shipped 6500412 |
| DX-11 | Phase 22 | Shipped 5fba059 |
| DX-12 | Phase 22 | Shipped 98da48e |
| DX-13 | Phase 22 | Shipped d3f5350 |
| DX-14 | Phase 22 | Shipped d2bde5d |
| DX-15 | Phase 22 | Shipped 95582e7 |
| MICR-01 | Phase 23 | Shipped f6b00ba |
| MICR-02 | Phase 23 | Shipped 8190fb2 |
| MICR-03 | Phase 23 | Shipped 47d7718 |
| LINT-01 | Phase 24 | Shipped Phase 24 plans 24-00..24-04 |
| LINT-02 | Phase 24 | Shipped Phase 24 plans 24-00..24-04 |
| LINT-03 | Phase 24 | Shipped Phase 24 plans 24-00..24-04 |
| DEFER-06 | Phase 25 | Shipped Phase 25 plans 25-00..25-04 |
| STD-01 | Phase 26 | Shipped 86fa69a |
| STD-02 | Phase 26 | Shipped 86fa69a |
| STD-03 | Phase 26 | Shipped 2d3efe1 |
| SYM-01 | Phase 26.1 | Shipped 35474ed |
| TUP-09 | Phase 26.1 | Shipped 6549116 |
| TUP-10 | Phase 26.1 | Shipped d628870 |
| TUP-11 | Phase 26.1 | Shipped d628870 |
| DICT-01 | Phase 26.1 | Shipped daaa023 |
| DICT-02 | Phase 26.1 | Shipped daaa023 |
| DICT-03 | Phase 26.1 | Shipped daaa023 |
| ERG-01 | Phase 26.2 | Shipped 4f92c24 |
| ERG-02 | Phase 26.2 | Shipped dfbfa1f |
| ERG-03 | Phase 26.2 | Shipped 6df301e |
| ERG-04 | Phase 26.2 | Shipped f12d648 + d655c65 + 28158cc + dfbfa1f + 821e9d0 |
| ERG-05 | Phase 26.2 | Shipped 45b01fb + 28158cc |
| QOL-04 | Phase 27 | Shipped ace6416 |

---

## v1.4 Phase 30 — Flow CLI + Formal Install (cross-milestone insert)

v1.3 milestone shipped 2026-05-10 with Phase 27. The v1.4 milestone (Phases 28-34) opened
with Phase 28 (shipped 2026-05-10) and Phase 30 (shipped 2026-05-11). Full v1.4 REQ tracking
will move to its own REQUIREMENTS.md when `/gsd-new-milestone` is invoked; this section is
a Phase 30 anchor so cross-references from the SUMMARY / ROADMAP land somewhere stable.

REQ-IDs map 1:1 to `.planning/phases/30-flow-cli-formal-install/30-SPEC.md` requirements 1-8.

| REQ | Phase | Status |
|-----|-------|--------|
| REQ-1 (Unified `flow` binary, 11 subcommands) | Phase 30 | Shipped fa66c38 + 48761cb + 8bcc8c0 + 303bddd |
| REQ-2 (Self-contained Linux x64 single-file ≤120 MB; actual 38 MB) | Phase 30 | Shipped fc6fead |
| REQ-3 (install.sh per-user + --system, idempotent) | Phase 30 | Shipped c31f36d |
| REQ-4 (XDG ~/.config/flow/config.toml, 5 keys, all 4 optional wired) | Phase 30 | Shipped 475838c + f8ca1ed + a34c904 + 8116b2f |
| REQ-5 (midi2flow flat per-track output, AddSplitTracks deleted) | Phase 30 | Shipped 63eb787 + a7170dd + 303bddd |
| REQ-6 (Round-trip ±1 tick on 3 CC0 fixtures) | Phase 30 | Shipped a7170dd + a026afb |
| REQ-7 (test-install.sh smoke ≤60s; actual 8s) | Phase 30 | Shipped 984fa39 |
| REQ-8 (dotnet run --project flow-interpreter still works) | Phase 30 | Shipped (preserved across all 9 plans) |

---

## v1.4 Phase 33 — SFZ Orchestral Sampler (cross-milestone insert)

Phase 33 ships an opt-in SFZ-format orchestral sampler gated behind `use "@sfz"`,
so composers can load CC-licensed external libraries (blessed: VSCO Community CE 1.1.0)
via `loadSfz #violin` style calls without retrofitting the Phase 29 bundled-sample path.
Phase 33 is purely additive — Phase 29's `renderSong song "piano"` byte-identical
contract is preserved.

REQ-IDs map 1:1 to `.planning/phases/33-sfz-orchestral-sampler/33-SPEC.md` requirements 1-8;
all 8 are locked and ship in this phase. Status `locked` means: spec criterion is closed,
implementation lands in the cited Phase 33 plan(s), and a passing test gate exists in
`flow-lang.Tests/{Unit,Integration}/Phase33/`.

| SPEC | Phase | Status |
|------|-------|--------|
| SPEC-1 (`use "@sfz"` stdlib import gates the SFZ surface) | Phase 33 | Shipped 37dfea0 + 043d3a3 (Plan 33-05) + 20ee7d3 (Plan 33-07 sampler-side gate) |
| SPEC-2 (Symbol-keyed instrument lookup via shipped 19-entry GM dict + `sfz_root` config) | Phase 33 | Shipped 0d619fb (Plan 33-02 SfzRoot POCO) + 37dfea0 + 043d3a3 (Plan 33-05) |
| SPEC-3 (SFZ parser: 13-opcode common subset + 3 header types + `<control>` extension) | Phase 33 | Shipped a3c4150 + ad3d017 (Plan 33-04) |
| SPEC-4 (Region matching by `(pitch, velocity)` + nearest-pitch varispeed fallback) | Phase 33 | Shipped 718b0fa + afdbfab (Plan 33-06) |
| SPEC-5 (Equal-power 441-frame loop crossfade prevents audible boundary clicks) | Phase 33 | Shipped afdbfab (Plan 33-06 SfzRenderer + SfzLoopCrossfadeTests) |
| SPEC-6 (`Sfz` value type + `sampler:NAME` instrument dispatch + binding registry) | Phase 33 | Shipped 671254c + 0d619fb (Plan 33-02 SfzType) + d6681d4 + 20ee7d3 (Plan 33-07) |
| SPEC-7 (CI smoke renders synthetic fixture; non-empty + RMS > -40 dBFS + discontinuity ≤ 0.05) | Phase 33 | Shipped 9b13681 + 49dbc34 (Plan 33-01 fixture + repo-size gate) + 8772635 (Plan 33-08 SfzSmokeTests) |
| SPEC-8 (Phase 28 articulation envelope + `ampeg_attack` override apply on top of SFZ render) | Phase 33 | Shipped afdbfab (Plan 33-06 envelope hook) + 8772635 (Plan 33-08 SfzArticulationTests) |

Two-run byte-identical determinism contract (Phase 18/25/27 inheritance) preserved
end-to-end through the SFZ surface — verified by `Phase33.SfzDeterminismTests`
(shipped 8772635 in Plan 33-08). Phase 29 bundled-sample byte-identical regression
gate (`Phase29ByteIdenticalTests`) stays 6/6 green across all Phase 33 plans.

---

## v1.4 Phase 34 — Symphony Showcase (v1.4 closer — pre-public → public pivot)

Phase 34 ships the v1.4 headline artifacts — a curated ~60 s minimalist-orchestral symphony
("In Five Voices") for 5 VSCO Community CE 1.1.0 instruments rendered through the Phase 33
SFZ surface, plus a ~58 s solo-piano ragtime companion ("Stride & Stomp") added during
scope-expand for genre-agnostic demonstration — plus the public-facing release machinery
(v1.4.0 annotated tag + GitHub Release with 5 labeled assets: symphony.mp3+wav,
ragtime.mp3+wav, flow-linux-x64.tar.gz; top-level README.md `## Showcase` section with
user-attachments inline audio embed; docs/announcements/v1.4.0.md announcement draft) and
v1.4 milestone closure docs (PROJECT/ROADMAP/STATE/REQUIREMENTS/MILESTONES + CLAUDE.md +
external memory file rewrite).

REQ-IDs map 1:1 to the 5 ROADMAP Phase 34 success criteria, formalized as SYM-01..05 in
`.planning/phases/34-symphony-showcase-v1-4-closer-pre-public-public-pivot/34-RESEARCH.md`.

| SPEC | Phase | Status |
|------|-------|--------|
| SYM-01 (Symphony renders end-to-end via SFZ sampler, two-run cmp-clean) | Phase 34 | Shipped d684086 + 8e4ad6f + 62b16d5 (Plans 34-01 + 34-02) |
| SYM-02 (Composer "postable on GitHub" sign-off recorded in 34-HUMAN-UAT.md) | Phase 34 | Shipped 7b68647 + 463d240 (Plan 34-01 UAT iterations #2) |
| SYM-03 (Code paired with audible features: articulation, polyphony, voicePool, tuplets) | Phase 34 | Shipped d684086 + 8e4ad6f (Plan 34-01 — every Phase 28 articulation token + `{voice}` polyphony + `{3:2}` tuplet + voicePool 32) |
| SYM-04 (README.md showcase + user-attachments audio embed + examples/symphony/README.md reproduction) | Phase 34 | Shipped 62b16d5 + a00820d (Plans 34-02 + 34-03) |
| SYM-05 (v1.4.0 tag + GitHub Release + announcement draft + milestone closure) | Phase 34 | Shipped 4547204 (Plan 34-04 announcement) + Plan 34-05 (tag 66842d6e + Release on commit 74de69a, no repo-changes) + Plan 34-06 (this closure commit) |

Two-run byte-identical determinism contract (Phase 18/25/27/33 inheritance) preserved
end-to-end through the real VSCO-CE library — verified manually by composer at release
time per D-702.

Release: https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0

---

## v1.4 Milestone Closure (2026-05-16)

v1.4 Audio Fidelity, Distribution & Public Showcase shipped 2026-05-16.

**Phases:** 28 (MIDI + Audio Polyphony & Articulation Rewrite), 29 (Instrument Realism),
30 (Flow CLI + Formal Install), 31 (LSP Enhancements + JetBrains Stretch), 32 (Full Scala
(.scl) Tuning Loader), 33 (SFZ Orchestral Sampler), 34 (Symphony Showcase — v1.4 closer)

**Plans completed:** 52 across the 7 v1.4 phases (Phase 28 = 7, Phase 29 = 7, Phase 30 = 9,
Phase 31 = 9, Phase 32 = 7, Phase 33 = 7, Phase 34 = 6).

**Release:** https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0

**Headline artifacts:** examples/symphony/symphony.flow ("In Five Voices", D minor,
~60s, 5 VSCO-CE instruments) + examples/ragtime/ragtime.flow ("Stride & Stomp",
F major, ~58s, solo VSCO-CE UprightPiano).

**Pre-public → public pivot:** Flow's demonstrated v1.4 API surface is now effectively
public. Breaking changes hereafter require a deprecation cycle (see CLAUDE.md § Goals
"Public as of v1.4" footnote + the external memory file
`project_pre_public_no_legacy_burden.md` rewritten 2026-05-16 to reflect post-public
footing).

**v1.5 carryover candidates:** captured in `.planning/MILESTONES.md` v1.4 entry's
"Forward-deferred items" block + `34-HUMAN-UAT.md` ragtime `closed_with_followup` note
(warmer-piano timbre / SFZ velocity layers / humanizeGaussian voice-block bug); also
flute D5 timbre crossover gap, sampled drum transient-preserving pitch shift, stereo
panning across instruments, second showcase contrasting genre.

---

## Notes

- Phase numbering continues from v1.2 (last phase: 17). v1.3 starts at Phase 18.
- Five binding pre-ordering constraints from PITFALLS map into roadmap shape:
  1. FRAC-* MUST precede TUP-* (rational arithmetic before tuplet syntax) → Phase 18 → Phase 19
  2. PRAG-* MUST precede DEFER-02/03 (H-alias) AND LINT-* (scale lint) → Phase 21 → DEFER-02/03 in 21, LINT in Phase 24
  3. Audit/spike DEFER-04 MUST precede DEFER-02/03 (multi-letter enharmonics before H-alias) → Phase 20 (DEFER-04) → Phase 21 (DEFER-02/03)
  4. MICR-* MUST be its own phase (highest blast radius — even with wedge scope) → Phase 23
  5. DEFER-06 (Gaussian) MUST be the LAST PRNG-touching phase (byte-identical determinism) → Phase 25 (after all other PRNG-touching phases close)
