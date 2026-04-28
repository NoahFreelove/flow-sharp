# Pitfalls Research — v1.3 Tuplets, Fractional Durations, Pragmas, Tier B/C Composer DX

**Domain:** Static-typed music DSL — extending an existing interpreter with non-power-of-2 duration math, scoped feature flags, microtonal tuning state, and a determinism contract that must survive new PRNG draws.
**Researched:** 2026-04-26
**Confidence:** HIGH (mostly grounded in concrete codebase artifacts and well-documented industry conventions; LOW only on Lilypond/Music21 anecdotal "tuplet doesn't fit" behavior, where I rely on documented behavior + library defaults rather than running them)
**Scope:** Pitfalls **specific to v1.3** as it lands on top of the v1.2 codebase. Generic interpreter pitfalls (lexer modes, parser recovery) are out of scope unless the new syntax aggravates them.

---

## Critical Pitfalls

### Pitfall 1: Floating-point drift in tuplet duration math

**What goes wrong:**
Tuplet ratios `(3:2 ...)` and arbitrary fractional durations like `C4/12` introduce values that are **not finite in binary** (1/3 = 0.333..., 1/12 = 0.0833...). The current `NoteValueType.ToFraction()` returns `double` for power-of-2 durations only — every value (1.0, 0.5, 0.25, …, 0.03125) is exactly representable. The moment a triplet enters the math, two notes that *should* sum to a quarter note (3 × q/3 = q) sum to `0.24999999999999997` or `0.25000000000000006` depending on operation order. Bar validation fires false-positive overflows. MIDI export rounds half the triplet up and half down, producing audible jitter at the start of bar N+1.

**Why it happens:**
- `NoteStreamCompiler.cs:259` does `double fraction = NoteValueType.ToFraction(noteVal); if (isDotted) fraction *= 1.5; explicitBeats += fraction * timeSig.Denominator;`. Multiplying a triplet fraction (1/3) into this chain compounds error.
- `NoteStreamCompiler.cs:275` then does `double remainingBeats = totalBeats - explicitBeats` and compares against zero — a single bit of drift makes "exactly fits" look like "slight overflow", silently triggering the `remainingBeats <= 0 ? totalBeats : remainingBeats` branch (auto-fit elements get the wrong duration).
- `MidiExport.cs:195` does `long durationTicks = (long)(beats * TicksPerQuarterNote)` — a flat truncation cast that drops the fractional remainder. Three triplet quarters at TPQN=480 become `(long)(0.333... * 480 * 3) = (long)479.999... = 479`, losing one tick.

**How to avoid:**
1. **Introduce a `Rational` (numerator, denominator) struct** in `flow-lang/TypeSystem/` and use it as the canonical duration currency end-to-end (compiler, validator, MIDI export, audio renderer). `BigInteger` for num/denom is overkill — `int` suffices because the LCM of `{2,3,5,7}` raised to reasonable powers stays well within `Int64` (e.g. `2^5 · 3^3 · 5^2 · 7 = 151200`).
2. **Keep `double` only at the audio-rendering boundary** where samples-per-note is computed, and do the multiplication as `(long)((numerator * SampleRate * 60) / (denominator * BPM))` — single division, no accumulating drift.
3. **Add `Rational ToRational(NoteValueType.Value)` and `Rational ToRational(int num, int denom)` helpers**. Migrate `NoteValueType.ToFraction` callers in this order: `NoteStreamCompiler` → `MidiExport` → `BarData` → `SongRenderer`. The migration is mechanical because `ToFraction` is called in a small, localized set of hot paths.
4. **Equality comparisons must use rational equality, not `Math.Abs(a - b) < epsilon`.** Epsilon comparisons silently hide bugs in tuplet nesting.

**Warning signs:**
- A test like `| (3:2 C4 D4 E4)q |` produces 3 notes whose `DurationTicks` sum is *off by 1 or 2* from the bar boundary in the exported MIDI.
- `cmp -l output1.mid output2.mid` succeeds across runs (determinism intact) but `MidiCsvDump output.mid` shows triplet notes start at ticks 0/160/320 (good) or 0/160/319 (bad — drift).
- `examples/showcase.flow` byte-identical determinism passes, but introducing a single triplet bar and re-running fails the determinism check by 1 sample at the bar boundary.
- A nested tuplet like `(3:2 (5:4 C4 D4 E4 F4 G4)e D4 E4)q` produces a visualize/piano-roll output where the inner notes don't tile flush.

**Phase to address:**
Phase 1 of v1.3 — the `Rational` type and `Rational`-based duration arithmetic must land **before** any tuplet or fractional-duration syntax is parsed. Otherwise every downstream feature is built on sand. Suggested phase name: `Foundation: Rational Duration Arithmetic`.

**AUDIT-VERIFIED markers affected:** None directly invalidated, but **introduces a new invariant** that must be marked: `// AUDIT-VERIFIED 2026-XX-XX: Rational arithmetic — duration sums exact for {2,3,5,7,9,11,12,16}-ratio tuplets; see tests/spike/v1.3-rational-tuplet-exactness.flow`.

---

### Pitfall 2: Bar validation breakage when tuplets don't divide cleanly

**What goes wrong:**
Today, `NoteStreamCompiler` validates a bar by summing element fractions against the time signature. Tuplets and `C4/12` introduce sums that **do** equal a clean fraction of the bar (e.g. 3 triplets = 1 quarter) **only if the tuplet group itself fits a power-of-2 duration**. A user can easily write `| (3:2 C4 D4)q F4 G4 |` — a duplet (2:3 = "2 in the time of 3") wrapped in a 3:2 ratio that math-wise wants 2/3 of a quarter, leaving the bar 0.083 beats short. Or `| C4/5 D4/5 E4/5 F4/5 G4/5 |` — five fifth-notes that *should* fill a whole note but only if every "fifth" is exactly 1/5 (rational, not double). The current behavior silently truncates or extends without telling the user, and the charitable-interpretation memory in CLAUDE.md ("music > rigid correctness") will be cited as justification, masking real bugs.

**Why it happens:**
`NoteStreamCompiler.cs:275` already has the precedent: `if (remainingBeats <= 0) remainingBeats = totalBeats;` — silent overflow recovery. This pattern will be copy-pasted into the tuplet code and become "if tuplet doesn't fit, just truncate" with no diagnostic.

**How to avoid:**
1. **Define the contract explicitly in 1-FOUNDATION-SPEC.md before any code lands.** Three options, pick one:
   - **(A) Strict validation, soft failure**: emit a `Console.Error.WriteLine` warning (matches the existing `varRef` undefined-variable pattern at `NoteStreamCompiler.cs:551,562,577`) and silently truncate. Most consistent with charitable-interpretation memory.
   - **(B) Strict validation, hard failure**: raise an error in `ErrorReporter` and skip the bar. Breaks the soft-failure contract.
   - **(C) Auto-extend the bar**: extend the `TimeSignatureData` for that bar only. Most musically charitable but breaks downstream MIDI export which assumes uniform time signature per section.
   **Recommendation:** (A) with a single, consistent `Warning: bar at line N tuplet sum {sum_rational} does not match time signature {timesig}; truncating/extending` message. Goes through `ErrorReporter` so `--verbose` users see it but soft-failure behavior is preserved.
2. **Three-state validation result**: `Exact | Short | Long`. Short = pad with rest. Long = truncate with warning. Exact = fall through. Encapsulate in a method `BarFitResult ValidateBarFit(Rational sum, TimeSignatureData timeSig)` and unit-test it against every primitive tuplet ratio (2/3, 3/4, 4/5, 5/6, 6/7, 7/8, 7/9, 11/12).
3. **MIDI export must read the validation result, not re-compute.** Today `MidiExport.cs:259` does its own beat math via `maxBeats * TicksPerQuarterNote`. Sharing the validation result avoids "compiler said fit, exporter says overflow" inconsistencies.
4. **Tutorial.flow + showcase.flow must NOT use ambiguous tuplet sums** — pick examples where the tuplet exactly fills its parent duration.

**Warning signs:**
- A test stream `| (3:2 C4 D4)q |` produces a bar where the visualize piano roll shows a third note appearing or two notes being stretched without warning to stderr.
- The byte-identical determinism check for tuplet examples in tutorial.flow fails because the bar-fit branch is non-deterministic (e.g. depends on dictionary iteration order over elements).
- A user pastes a working Lilypond-style tuplet (e.g. from a transcription) and the resulting Flow output is rhythmically wrong with no diagnostic.

**Phase to address:**
Phase 2 of v1.3 — `Tuplet syntax + bar fit validator`. The `ValidateBarFit` method must come with a 30-line unit test matrix (Phase14-style `flow-lang.Tests/Unit/Phase{NN}/BarFitValidatorTests.cs`) before tuplet syntax is exposed to users.

**AUDIT-VERIFIED markers affected:**
The C1 fix (`Interpreter.cs:303` `return;`→`break;` for partial-context body) sets a precedent: silent partial behavior is acceptable when the alternative is dropping music. Tuplet bar-fit warning logic should reference C1's "music > rigid correctness" reasoning explicitly to keep the trail.

---

### Pitfall 3: MIDI tick precision insufficient for common tuplets

**What goes wrong:**
`MidiExport.cs:17` hardcodes `TicksPerQuarterNote = 480`. This works for 3:2 (480/3=160 exact) and 5:4 (480/5=96 exact) and 6 (480/6=80 exact) and 8 (60 exact), but **fails** for:
- 7-tuplets: 480/7 ≈ 68.57 → quantize loses 0.86 tick × 7 = 6 ticks per beat → audible.
- 11-tuplets: 480/11 ≈ 43.6 → loses 6.6 ticks per beat.
- 13-tuplets: same problem.
- Mixed tuplets in a triplet: a 5:4 inside a 3:2 quarter wants `480/3/5 = 32` ticks → exact, but `480/3/7 = 22.86` → not.

Once a 7-tuplet enters a piece, MIDI export emits drifted timestamps that **don't** match the WAV render (where audio samples are computed in floating point), violating the "WAV and MIDI represent the same score" contract that v1.2 implicitly established with `tutorial.flow` byte-identical determinism.

**Why it happens:**
The standard SMF default is 480 TPQN because it cleanly divides 2, 3, 4, 5, 6, 8, 10, 12, 15, 16, 20, 24, 30, 32, 48, 60, 80, 96, 120, 160, 240. It does NOT divide 7, 11, 13. This is a known SMF-as-an-art quirk — even Logic Pro and Cubase use 960 or higher when 7-tuplets are imported.

**How to avoid:**
1. **Auto-elevate `TicksPerQuarterNote` based on tuplet content of the score.** Compute LCM of all denominators present + 480 (or use a fixed safe value). Suggested formula: `TPQN = 480 × LCM(unique tuplet denominators) / GCD(480, LCM)`. For a piece with only 3-tuplets and 5-tuplets, this stays at 480. With 7-tuplets, it elevates to 3360 (480 × 7). With 7 and 11, elevates to 36960 (480 × 7 × 11).
2. **Cap the elevation at 65535** (SMF spec limit for ticks-per-quarter when bit 15 is 0). If a piece needs higher, that's a real error, not a charitable one — raise it loudly because no DAW will import it correctly anyway.
3. **Document the elevation in the MIDI file's Track 0 marker event** so external tools can see "this file uses TPQN=3360 to support 7-tuplets accurately".
4. **Test matrix:** for each (TPQN, tuplet ratio) combination, verify `(TPQN × num) % denom == 0`. Make this a Theory in `flow-lang.Tests/Unit/Phase{NN}/MidiTickPrecisionTests.cs`.
5. **Don't store TPQN as `private const int`** — the constant in `MidiExport.cs:17` must become a per-export computed value.

**Warning signs:**
- A 7-tuplet piece exports to MIDI; loading it back into Flow via the MIDI parser (`flow-midi/Conversion/Quantizer.cs:444` uses `tolerance = Math.Max(tpqn / 48, 1)`) round-trips with ticks-shifted notes.
- A user reports "my triplet plays cleanly but my septuplet is off" — direct evidence of TPQN=480 limitation.
- The MIDI VALIDATION pin (cmp-clean across runs) holds for v1.2 examples but breaks the moment a tuplet test is added — diagnostic is "the rounding direction depends on the piece's tempo".

**Phase to address:**
Phase 3 of v1.3 — `Tuplet MIDI export with auto-elevated TPQN`. Co-located with the `Rational` migration of MIDI export. Verification step: round-trip every primitive tuplet (3:2, 5:4, 7:8, 11:8, 13:16) and assert tick math is exact.

**AUDIT-VERIFIED markers affected:** None directly. Establishes a new invariant: `// AUDIT-VERIFIED: TPQN auto-elevated to LCM(tuplet_denoms)·480; assert (TPQN × n_dur_ticks) % denom == 0 for every note exported`.

---

### Pitfall 4: Pragma system leaking across `use` imports

**What goes wrong:**
Pragmas like `enable H_alias` are seductively simple to implement as a `HashSet<string>` on `ExecutionContext`. But the existing `ModuleLoader.cs` runs imports **in the caller's context** (CLAUDE.md "Module imports execute in the caller's context — no separate scope/namespace isolation"). This means: if file `library.flow` declares `enable strict_tuplets` and is loaded by `main.flow`, the pragma now applies to the main file — even though main.flow never asked for it. Worse: the order of `use` statements becomes load-bearing for parsing semantics, which is a famous Haskell-extension footgun (e.g. `OverloadedStrings` leaking through re-exports).

The Flow `use` mechanism is a single-pass include — it does not even have the boundary that Haskell's module system has, so the leakage is *guaranteed* unless explicitly prevented.

**Why it happens:**
Existing convenience pattern — `MusicalContext` is a stack with push/pop scoping. Reusing this pattern naively for pragmas means "pragmas push on use, never pop", because `use` has no closing brace. The mental model "pragma is just a flag" hides the fact that pragmas change parse-time behavior — and parse-time behavior is per-file, not per-context-stack.

**How to avoid:**
1. **Pragmas are file-scoped, full stop.** Each `.flow` file gets its own pragma set, populated only by `enable` declarations in that file. `use` does NOT propagate pragmas. This matches Rust's `#![feature(...)]` semantics (file-scoped, not crate-scoped at the use site) — the Right Thing.
2. **Implementation:** `ParseResult` carries `EnabledPragmas: HashSet<string>`, populated by the parser when it sees an `enable foo` statement at module top. The parser passes this to a per-file `NoteStreamCompiler` factory. Globally, `ExecutionContext` does NOT know about pragmas.
3. **Explicit conflict detection at parse time.** Two pragmas that disagree (e.g. `enable H_alias` plus `enable strict_pitch_letters` if those existed) raise a compile-time error. Empty-grep the existing codebase for "pragma collision" patterns — there are none, this is greenfield, so do it right.
4. **Each pragma carries a one-line behavior summary in source.** A registry like `static class PragmaRegistry { public static readonly Dictionary<string, PragmaDoc> Pragmas = …}` makes new pragmas discoverable and self-documenting. Mirror the v1.2 `BuiltInDocs` 104-entry pattern.
5. **The LSP must surface pragmas in completion + hover.** When user types `enable `, completions should list available pragmas with their docstrings.
6. **Reject `enable` outside top-of-file.** Pragmas inside proc bodies or sections are a slippery slope — Haskell allows it, regrets it. Flow should not.

**Warning signs:**
- A test file imports a stdlib module and parsing semantics change (e.g. `H` is suddenly recognized in a file that didn't enable the alias). This is the canary — if any v1.3 test has the pattern `use "@something"` followed by parser behavior changing, the pragma model is broken.
- The v1.2 determinism contract (`tutorial.flow` cmp-clean) breaks because a pragma is enabled in one stdlib file but not another and the parse output depends on file load order.
- An LSP user reports completion suggesting `H` notes in a file that doesn't have the alias enabled — the LSP ignored pragma scoping.

**Phase to address:**
Phase 4 of v1.3 — `Pragma system + enable keyword (file-scoped)`. The phase plan must explicitly say "pragmas are file-scoped, do NOT propagate via use" and include a test that verifies non-propagation: `tests/v1.3-pragma-isolation.flow` imports a file that enables `H_alias` and asserts `H` is **not** parsed as a note in the importer.

**AUDIT-VERIFIED markers affected:** None. New invariant: `// AUDIT-VERIFIED: pragma scope is file-only; ModuleLoader does NOT propagate EnabledPragmas across import boundaries`.

---

### Pitfall 5: Microtonal tuning state vs. transpose/invert/retrograde transforms

**What goes wrong:**
A `tuning JustIntonation { … }` musical-context block is mathematically interesting only if the rest of the pipeline respects pitch *names* (E♭ ≠ D♯ in JI) rather than reducing to MIDI numbers. Today, the entire transform pipeline (`StandardLibrary/Transforms/TransformFunctions.cs`) operates on integer semitone offsets — `transpose(seq, +2)` literally adds 2 to a chromatic-number representation. In 12-TET this is correct; in 5-limit JI, transposing a "C major pure" up a tone gives "D major pure" only if you re-spell the new tonic, otherwise you get D-natural-pitched-like-C-rooted (a wolf-fifth setup). Even worse: `invert(seq)` reflects pitches around an axis, and in JI that axis itself has a specific pitch ratio — the inversion creates new ratios that may not be in the tuning table.

The risk is shipping a `tuning` block that *parses* but produces rhythmically correct, microtonally **wrong** audio because the transform pipeline blindly does MIDI-number math on JI-named pitches.

**Why it happens:**
- `Note` type currently stores `(noteName, octave, alteration)` where alteration is an `int` semitone offset. JI requires storing the spelling (E♭ vs D♯) **separately** from the cent offset, because they map to different ratios.
- `TransformFunctions.Invert` (and friends) computes inversion at the MIDI-number level. There's no notion of "preserve enharmonic spelling" at the transform layer.
- `MusicalContext` currently has no `Tuning` field; adding it is easy. Making transforms respect it is hard.

**How to avoid:**
1. **Separate concerns: tuning is a render-time concern, transforms are a score-time concern.** The score stores spelled notes (`Eb` vs `D#`) untouched. The audio renderer (`SongRenderer.cs` instrument-by-instrument) consults the active tuning at render time and converts spelled notes to Hz. Transforms operate on the score level only.
2. **Microtonal MIDI export is `pitch wheel + base note`, not a remapping.** DryWetMidi supports `PitchBendEvent`. For JI, write the base 12-TET note + a per-note pitch bend that compensates the cent offset. This requires a dedicated MIDI channel per voice (each channel has its own pitch wheel).
3. **DEFER-04 multi-letter enharmonic edges (E↔Fb, F↔E#, B↔Cb, C↔B#) become a hard prerequisite for JI.** In JI, F♭ and E are *different pitches*. The Phase 14 enharmonic work made `enharmonic(Note) → Note` a function — extend it to know that the conversion is **destructive** for non-12-TET tunings, and emit a warning if called inside a tuning block.
4. **Add a `tuning` musical-context block that propagates only at render time**. Specifically: `MusicalContext.Tuning` is read by `SongRenderer` and synthesizers (which translate `(name, octave, alteration, centOffset, tuning)` → Hz) but is **invisible** to `transpose`/`invert`/`retrograde`. The transforms see a `Sequence` of spelled notes; the tuning is applied at the audio stage.
5. **Document explicitly: scale linting (DEFER feature 12) interacts with tuning.** "Out of key" in JI means "uses a ratio not in the tuning table" — different from 12-TET out-of-key. Phase plans must call this out.

**Warning signs:**
- A piece with `key Cmajor { tuning JustIntonation { ... }}` rendered with `transpose(seq, 2)` produces D-major audio that sounds *different* from a piece with `key Dmajor { tuning JustIntonation { ... }}` of the same notes. The two should be byte-identical (assuming PRNG seeded). If not, transforms are double-applying tuning.
- The MIDI export of a JI piece sounds pure on Flow's renderer but plays as 12-TET on every external player — because pitch bend events were not emitted. This is a **silent regression** for users who export-and-share.
- `enharmonic(Eb4)` returns `D#4` inside a JI block, changing the rendered pitch by ~21 cents. The Flow user is shocked.

**Phase to address:**
Phase 5 of v1.3 — `Microtonal tuning context block`. Must come **after** the multi-letter enharmonic edges (DEFER-04) so spelled pitches survive the pipeline. JI is **not MVP** for v1.3; ship `tuning EqualTemperament` (no-op) first to validate the plumbing, then add JI as a follow-up phase.

**AUDIT-VERIFIED markers affected:** Phase 14 DX-06 (enharmonic dispatch reorder) gains a new invariant: `// AUDIT-VERIFIED: enharmonic() preserves cent-offset in 12-TET; warns if MusicalContext.Tuning != EqualTemperament`.

---

### Pitfall 6: Determinism contract broken by Gaussian humanize PRNG draw count

**What goes wrong:**
v1.2 closed with the byte-identical determinism contract: `tutorial.flow` and `showcase.flow` produce cmp-clean WAV+MIDI output across two consecutive runs. The contract holds because every PRNG (TPDF dither in `FileIO.cs:11`, synth white-noise in `SynthUtils.cs:13`, euclidean humanize in `BuiltInFunctions.cs:1219`) is reseeded at known boundaries, and the **draw count** is fixed by the algorithm. Switching humanize from uniform to Gaussian sounds innocent — same number of inputs, same range. **It is not.** A Gaussian draw via Box-Muller costs **two** uniform draws per sample (and potentially loops on `u1==0`); the Marsaglia polar method costs an *unbounded* expected number of draws (it rejects samples until they fall in the unit circle, ~1.27 expected draws but variable). Either replacement breaks the deterministic draw count for any *new* humanize call site, and worse, makes the contract sensitive to changes in the Gaussian implementation across .NET patch versions.

**Why it happens:**
v1.2 RETROSPECTIVE.md item 3: "Determinism contracts only hold if every PRNG in the chain is seeded." Gaussian replacement is the canary case: same seed, different draw count → different downstream PRNG state → different downstream samples, even though the Gaussian itself is reproducible. If the v1.3 humanize change uses `Random.Shared.NextDouble()` for the second uniform draw (skipping the seed), the contract silently breaks.

**How to avoid:**
1. **Make the Gaussian distribution explicit and stable across .NET versions**: ship Flow's own Box-Muller implementation in a static `MathUtils.NextGaussian(Random rng, double mean, double stddev)` helper. Two `rng.NextDouble()` draws per sample, no looping. This pins the draw count.
2. **The new humanize overload is additive, NOT replacing.** `humanize(seq, amount, seed)` (uniform) stays as is — `BuiltInFunctions.cs:1180`. Add `humanizeGaussian(seq, stddev, seed)` as a **new function**. This means existing scripts that use the uniform-humanize path are byte-identical unchanged.
3. **The `showcase.flow` byte-identical determinism check must be re-pinned** with a `humanizeGaussian` call site introduced. Two runs of the new showcase must `cmp -l` clean. The pin file is `examples/output/showcase.wav.sha256` and `examples/output/showcase.mid.sha256` — both must be updated in the same commit that introduces the call site.
4. **Audit every other non-uniform stdlib draw** — `tempoRamp` (does it use a curve PRNG?), `vary` (`VariationFunctions.cs`), `arpeggio` if it gets a `randomize` parameter. Each must declare its PRNG protocol in a one-line comment: `// PRNG: 2 NextDouble() draws per output sample, seeded via local Random(seed)`.
5. **Add a determinism CI check that runs the entire `examples/` directory twice and cmp's outputs.** Make this part of the v1.3 phase that introduces the Gaussian humanize.

**Warning signs:**
- Two consecutive runs of `showcase.flow` produce `cmp -l output1.wav output2.wav | wc -l` > 0 after the Gaussian humanize lands. **Stop the milestone, revert, fix.**
- The Gaussian implementation uses `Math.Log(rng.NextDouble())` and never bounds u1 away from 0 → on rare seeds, log(0) = -∞, NaN propagates, audio output has clicks. (This is a non-determinism *because* it's environment-dependent.)
- A test that says "humanize amount 0.5 produces velocities in [0.13, 0.87]" passes for uniform but fails for Gaussian (Gaussian has unbounded tails — must be clamped, not assumed). Mismatched expectations between the docstring and the implementation.

**Phase to address:**
Phase 6 of v1.3 — `Gaussian humanize distribution (DEFER-06)`. Must **come AFTER** every other v1.3 feature that adds PRNG draw sites (tuplet randomization, variation expansion). Otherwise reseeding becomes a moving target.

**AUDIT-VERIFIED markers affected:**
The v1.2 PRNG-reseed markers (`SynthUtils.cs:13`, `FileIO.cs:11`) must be **augmented** with a Gaussian-humanize-seed marker:
```csharp
// AUDIT-VERIFIED 2026-XX-XX: Gaussian humanize uses Box-Muller (2 uniform draws per
// sample, no rejection); local Random(seed) per call; bytes pinned by
// examples/output/showcase.wav.sha256 (two-run cmp-clean).
```

---

### Pitfall 7: LSP regressions on incomplete tuplet/fraction syntax mid-typing

**What goes wrong:**
v1.2 shipped a working LSP (`flow-lsp/`) with semantic tokens, hover, completion, signature help, go-to-def. Every user keystroke during typing produces a partial parse — until v1.2, partial parses were limited to mature constructs (incomplete chord, incomplete `proc`). Tuplets like `(3:2 C4 D4 E4)q` introduce a new partial state: `(3` (parses as expression), `(3:` (parses as labeled call?), `(3:2` (looks like a slice or a ratio), `(3:2 C4` (now it's clearly a tuplet but only after the second token).

Two failure modes:
- **Semantic token churn**: highlight flips between "operator" and "tuplet-marker" multiple times per keystroke — visible to the user as flickering colors.
- **Diagnostic spam**: every keystroke during tuplet entry produces a fresh "expected expression after `:`" error, scrolling the diagnostics panel.

For `C4/12`, the partial states are even worse because `/` is the existing division operator. Mid-typing `C4/`, the parser cannot know if this is `Note divided by something` (currently nonsense) or a fractional duration (new syntax). Even when complete, `C4/12` is ambiguous in expression contexts where `C4` is a variable and `/` is division.

**Why it happens:**
- The LSP runs the same parser as the interpreter (`flow-lsp` references `flow-lang` directly per CLAUDE.md). Parser error recovery for the new syntax must be excellent because the LSP runs it on every keystroke.
- `flow-lsp/Symbols/KeywordIndex.cs` is a static list. Adding new keywords requires updating it; missing it means completion silently drops new keywords.
- The Flow parser is recursive descent (CLAUDE.md "Hand-written recursive descent parser") — partial-input handling is per-grammar-rule, not automatic.

**How to avoid:**
1. **The fractional duration suffix (`/12`) is recognized only inside note streams (between `|` delimiters), NEVER in general expression context.** Inside note streams, `/` after a note is unambiguously a fraction marker. Outside, it remains division. This **must** be enforced in the lexer's mode (note-stream mode vs. expression mode) — `SimpleLexer` already has this dichotomy; extend it.
2. **Tuplet `(N:M ...)` parses only inside note streams.** In expression context, `(3:2 ...)` either errors or is reserved for future use. Lexer/parser mode-switching prevents most ambiguities.
3. **The parser's tuplet rule must accept incomplete tuplets gracefully.** Specifically: on encountering `(`, look ahead for `INT_LITERAL ':' INT_LITERAL`. If it doesn't match, fall through to the existing random-choice / chord parsing. If it matches but children are missing, emit a single "incomplete tuplet" diagnostic (not one per missing child).
4. **Semantic token output for partial tuplets defaults to "regexp"-like neutral coloring until the closing `)` is seen.** Avoids per-keystroke flicker.
5. **Update `KeywordIndex.cs`** with the new tokens: `enable`, `tuning`, `legato`, `portamento` (if added), `progression` already exists. Add to the static array. Verify completion flow: `flow-lsp/Handlers/CompletionHandler.cs:43` reads `_keywords` and surfaces them — confirm the new keywords flow through.
6. **Snapshot test the LSP**: type `(3` → `(3:` → `(3:2` → `(3:2 C` → `(3:2 C4` → `(3:2 C4)q` and assert the diagnostic count never exceeds 1 at any intermediate step. Mirror the existing `flow-lsp.Tests/` patterns.

**Warning signs:**
- VSCode shows red squigglies under typed-but-not-yet-complete tuplets after every keystroke (more than 1 active diagnostic at a time).
- Semantic tokens flicker between two coloring schemes during typing.
- Completing `enable ` shows an empty list (KeywordIndex.cs not updated).
- `Go to Definition` on a function inside a tuplet says "no definition found" — symbol resolution lost the body of the tuplet.

**Phase to address:**
Phase 7 of v1.3 — `LSP support for tuplet/fraction/pragma syntax`. Must run **concurrently** with Phase 2 (tuplet syntax) and Phase 4 (pragma system). Recommended: have the parser-changes phase update both `flow-lang` and `flow-lsp` together, with snapshot tests before each commit.

**AUDIT-VERIFIED markers affected:**
None directly. Add marker: `// AUDIT-VERIFIED: LSP partial-tuplet diagnostic count ≤ 1 at every keystroke; tested via flow-lsp.Tests/Integration/Phase{NN}/PartialTupletDiagnosticTests.cs`.

---

### Pitfall 8: Lexer/parser collisions with existing 70+ test files

**What goes wrong:**
Existing test files in `tests/` and the larger test corpus use:
- `[C4 E4 G4]q` — chord brackets (could clash with `[3 2 ...]` if tuplets re-use square brackets)
- `(? C4 E4 G4)` — random choice
- `(?? C4 E4 G4)` — seeded random choice
- `4/4` time signatures (`Parser.cs:439`)
- `C4` followed by `/` followed by another expression — e.g. `Int x = C4 / 2` if `C4` were a variable name (rare but possible — `tests/test_arithmetic.flow` could have a numeric named `C4`)
- Free identifiers like `enable`, `tuning`, `legato`, `portamento`, `voicing`, `inversion`, `quantize`, `progression` (`progression` is already a TokenType — `TokenType.cs:24`)

Concrete collisions found by inspection:
- **`(3:2 ...)` does NOT clash with `(? ...)`** because `?` is the disambiguator. Safe.
- **`[3 2 C4]q` would clash** with chord-bracket parsing if Flow chooses square-bracket tuplet syntax. Recommendation: keep tuplets parenthesized — `(3:2 ...)` — and reserve brackets for chords. Avoids this collision.
- **`C4/12`** is fine in note streams (the lexer is in note-stream mode and `C4` is a NoteLiteral, `/` becomes part of duration scanning). It is ambiguous in expressions if `C4` is also used as a variable name. Inspect `tests/test_*.flow` for variables named after notes (`C4 = 5; …`). Quick grep needed.
- **`enable`** keyword. Grep `tests/` for any function or variable named `enable`. If any, rename the keyword to `pragma` or `feature` (which Rust/Haskell precedent supports) before landing.
- **`tuning`** keyword. Same grep. Likely safe (rare identifier name).
- **`legato`, `portamento`, `voicing`, `inversion`, `quantize`** — all candidates for collision. Phase 14 already had this exact issue with `H`, `Db`, `enharmonic`, `reverbTime` (RETROSPECTIVE.md "Reserved-keyword collisions caught at fact-authoring time"), forcing mid-execution renames.

**Why it happens:**
v1.2 RETROSPECTIVE.md item 5: "Reserved-token grep gates should be plan-time, not execution-time." The exact pattern caught Phase 14 mid-flight. v1.3 has 5+ new keywords across multiple features — the surface area is bigger.

**How to avoid:**
1. **Pre-landing collision grep gate is mandatory for every phase**, recorded as VERIFICATION.md output (matching the v1.2 Phase 14 DX-06 pattern). Specific greps:

```bash
# Per phase, run these BEFORE writing the parser change:
git grep -wnE 'enable|tuning|legato|portamento|voicing|inversion|quantize' \
  -- 'tests/*.flow' 'examples/*.flow' 'flow-lang/*.flow'
git grep -wnE '\bC[0-9]\b|\bD[0-9]\b|\bE[0-9]\b' \
  -- 'tests/*.flow' 'examples/*.flow'   # Notes used as identifiers
git grep -nE '/[0-9]+\b' -- 'tests/*.flow' 'examples/*.flow'  # / 12 patterns
git grep -nE '\(3:2|\(5:4|\(7:8|\(N:M' -- 'tests/*.flow' 'examples/*.flow'  # tuplet syntax
```

2. **Empty-grep output is committed as evidence** in the phase's VERIFICATION.md. If the grep is non-empty, the phase plan resolves it (rename, mode-scope, defer the keyword) before landing.
3. **Mode-scope every new syntax**: tuplet `(N:M ...)` and fractional `/N` ONLY parse inside note streams. Pragmas `enable foo` ONLY at file top. Tuning `tuning Foo { … }` ONLY as a musical-context statement (not as a free expression).
4. **Run the entire `tests/` directory after each phase lands** — if even one test breaks because of new lexer/parser behavior, the phase has a regression.

**Warning signs:**
- Phase plan does not include a collision-grep transcript in VERIFICATION.md → reject in two-pass strict authorship review.
- A single test in `tests/` breaks after lexer changes — direct evidence that mode-scoping is leaking.
- `examples/tutorial.flow` byte-identical determinism breaks after lexer/parser changes — evidence that the new lexer is emitting different tokens for unchanged input.

**Phase to address:**
Every phase that introduces new syntax (Phase 2 tuplets, Phase 4 pragmas, Phase 5 tuning) must include the collision grep as a checklist item. Roadmapper: copy the v1.2 Phase 14 DX-06 grep template into every v1.3 syntax-touching phase.

**AUDIT-VERIFIED markers affected:**
None directly. Add per-phase: `// AUDIT-VERIFIED: collision grep for {keyword_list} returns empty over tests/, examples/, flow-lang/*.flow at commit {sha}`.

---

### Pitfall 9: Existing AUDIT-VERIFIED markers re-triggered by v1.3 changes

**What goes wrong:**
There are six `AUDIT-VERIFIED` markers in the live source (excluding worktree copies):

| File:line | Audit | Subject | Re-trigger risk in v1.3 |
|-----------|-------|---------|-------------------------|
| `Interpreter.cs:75` | C2 | `_returnValue` short-circuit guard correct | LOW — DEFER closures may add new return-value sites; verify guard still applies. |
| `Interpreter.cs:303` | C1 | `ExecuteMusicalContext` `return;`→`break;` body runs under partial context | **MEDIUM** — adding `tuning` and `reverbTime`-style context blocks adds new context types; verify each goes through the same `break;` path. |
| `EnvelopeProcessor.cs:105` | C3 | Loop body only runs when `frames > 0` | LOW — envelope processing not directly touched by v1.3, unless legato adds new envelope shaping. |
| `BufferHelpers.cs:128` | C4 | Fade-in/out guard for short durations | LOW — unless WAV-import (DEFER feature 14) introduces a fade pre-pass. |
| `TransformFunctions.cs:239,261` | C5 | augment/diminish lengthens/shortens correctly | **HIGH** — both functions today work in power-of-2 NoteValue space. Tuplet support means augment/diminish must preserve tuplet ratios (augmenting a triplet quarter to a triplet half). The semantics need re-validation. |

**Why it happens:**
The C5 markers were verified by the visualize column-width test (`tests/spike/c5-augment-diminish.flow`) for power-of-2 durations. Tuplets break the column-width assumption (a triplet quarter is 2/3 the width of a regular quarter). The original verification does not cover tuplet inputs — `AUDIT-VERIFIED` is **silently invalidated** if `augment(triplet_seq) != triplet_half_seq`.

The C1 marker promises that the partial-context body **runs**. Adding `tuning JustIntonation { ... }` introduces a context where the body must run AND the tuning must be in scope. If the implementation forgets to set up the tuning before running the body (or re-orders the existing context-setup-then-body path), C1 is silently reverted.

**How to avoid:**
1. **Every phase that touches a file with an `AUDIT-VERIFIED` marker must explicitly assess the marker's continued validity.** Include in the phase plan: `## AUDIT-VERIFIED markers in scope` section listing affected markers and asserting each one's verification still holds OR providing a new test that re-verifies it.
2. **Specifically for C5 (augment/diminish):** Phase 2 (tuplet syntax) must include a new test `tests/spike/v1.3-augment-tuplet.flow` that augments a `(3:2 C4 D4 E4)q` and asserts the result is `(3:2 C4 D4 E4)h` (triplet ratio preserved, duration doubled). If this test passes, update the C5 marker comment to reference the new test alongside the existing one.
3. **Specifically for C1 (musical-context body):** The `tuning` block (Phase 5) must extend `ExecuteMusicalContext` to handle `Tuning` field — but the `break;` instead of `return;` semantics MUST be preserved for the new field. Add a test `tests/spike/v1.3-tuning-context-body.flow` that verifies a partial-tuning context still runs its body.
4. **Maintain a per-milestone "AUDIT-VERIFIED touch log"**: when a v1.3 commit modifies a file with an existing marker, the commit message references the marker number ("Touches AUDIT-VERIFIED C5; new test tests/spike/v1.3-augment-tuplet.flow re-validates").

**Warning signs:**
- Diff for any v1.3 commit shows changes to `Interpreter.cs:303` block, `TransformFunctions.cs:239-261`, or `BufferHelpers.cs:128` without a corresponding `tests/spike/v1.3-*.flow` re-validation test.
- The C5 spike test `tests/spike/c5-augment-diminish.flow` is run after augment/diminish changes and passes — but no tuplet-specific augment/diminish test exists. C5 is **silently obsolete**.
- A v1.3 commit removes an `AUDIT-VERIFIED` marker without explanation. Audit-trail-as-code semantics require these to be preserved or replaced, never silently deleted.

**Phase to address:**
Every phase that touches a marked file. Specifically: Phase 2 (tuplets) → C5; Phase 5 (tuning) → C1; Phase 14 if WAV-import lands → C4. Make the AUDIT-VERIFIED check part of the two-pass strict authorship Pass-2 reality check.

**AUDIT-VERIFIED markers affected:**
- C1 (Interpreter.cs:303) — at risk if Phase 5 (tuning) extends musical-context handling.
- C5 (TransformFunctions.cs:239,261) — at risk if Phase 2 (tuplet augment/diminish) lands without re-verification.

---

### Pitfall 10: DEFER closure breaks the "deferred = will work later" implicit contract

**What goes wrong:**
v1.2 closed with DEFER-01..06 as forward-deferred items. The implicit contract was "these will be addressed in v1.3 cleanly." But each DEFER has a hidden dependency or subtle interaction that, if not surfaced upfront, will be discovered mid-execution:

- **DEFER-01 (`range(Int, Int)`):** Looks trivial — just register an overload. **Hidden dependency:** existing `range` is `range(Int)` and `range(Int, Int, Int)`. Adding `range(Int, Int)` puts the OverloadResolver in a position where `range(0, 5)` is ambiguous between "0 through 5" vs "the existing 3-arg with default step". The PROJECT.md mentions "Audit §2 hardening (overload ambiguity)" — DEFER-01 lands ON THIS surface.
- **DEFER-02/03 (`H` alias via pragma):** Cannot land without DEFER-04 enharmonic edges. Rationale: if `H` becomes `B`, then `H♭` becomes `B♭`, and B♭ is already a valid note. But `H♯` becomes `B♯`, which only works if multi-letter enharmonic edges are present — otherwise B♯ has no defined alteration > +1.
- **DEFER-04 (multi-letter enharmonics):** Enharmonic conversion is destructive in microtonal tunings (Pitfall 5). Must coordinate with Pitfall 5 mitigation (warn when `enharmonic()` is called inside non-12-TET).
- **DEFER-05 (slice negative-from-end):** Existing `slice(Sequence, Int, Int)` silently two-sided clamps (Phase 14 DX-05). Adding negative indices means a `-1` previously clamped to `0` (silent) now means `length-1` (different). This is a **breaking change** for any test that passed -1 by accident.
- **DEFER-06 (Gaussian humanize):** Pitfall 6 in detail.

**Why it happens:**
DEFER closures are deceptively framed as "small follow-up tasks." In reality, each one carries a 2-3 phase dependency chain that the original deferral didn't surface. v1.2 RETROSPECTIVE.md item 1: "Disputes warrant a spike phase" — same logic applies, deferred items warrant a planning spike.

**How to avoid:**
1. **The first phase of v1.3 is a DEFER spike**: walk every deferred item, identify hidden dependencies, write 1-2 paragraphs per item assessing complexity. Output: a per-DEFER design note in `.planning/research/DEFER-{01..06}.md`. This becomes the entry criterion for the actual implementation phases.
2. **DEFER-05 silent clamp → negative semantics is a breaking change**. The phase plan must:
   - Audit `tests/` for any `slice(seq, X, -1)` style call — empty-grep the codebase.
   - Decide: is the new semantics (-1 = length-1) opt-in via pragma, or a hard cut-over? Recommendation: hard cut-over, because no existing test should rely on negative indices clamping (they'd have been written explicitly with positive indices).
3. **DEFER-01 overload ambiguity** must be resolved by either:
   - Removing `range(Int, Int, Int)` first (breaking) — bad.
   - Renaming the 3-arg form to `rangeStep` — clean.
   - Documenting that the 3-arg form is preferred when given 3 args (this is what OverloadResolver already does because of specificity scoring) — verify with a test.
4. **Order DEFERs by dependency**: DEFER-04 (enharmonic edges) → DEFER-02/03 (H alias) → DEFER-05 (slice negatives) → DEFER-01 (range) → DEFER-06 (Gaussian). Microtonal tuning (Tier B/C) **after** DEFER-04.

**Warning signs:**
- A v1.3 phase plan for "DEFER-01" is written as `[ ] Register range(Int, Int) overload` with no other items. Direct evidence that the spike was skipped — the plan misses overload-ambiguity, audit §2 hardening, and tests-side clamping behavior.
- DEFER-02/03 lands before DEFER-04, and the `H♯` notation produces unexpected behavior.
- Existing tests break because `slice(seq, X, -1)` silently changed semantics.

**Phase to address:**
v1.3 Phase 0 — `DEFER spike + dependency mapping`. No production code changes; pure investigation. Outputs design notes that bind subsequent phase plans.

**AUDIT-VERIFIED markers affected:**
None new. The DX-05 marker (slice silent clamp, Phase 14) is **invalidated** by DEFER-05 negative-index support; the phase that lands DEFER-05 must remove the marker and add a new one documenting the new contract.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Use `double` for tuplet duration math instead of `Rational` | Faster to ship Phase 2 by ~1 plan | Every downstream feature inherits drift; Pitfall 1 + Pitfall 3 amplify; LSP partial-fix becomes triple-fix | **Never** for v1.3 — `Rational` is foundational |
| Reuse `MusicalContext` stack for pragmas | One existing mechanism, no new code | Pragmas leak across files (Pitfall 4); breaks Haskell-precedent expectations; hard to undo once pragmas-are-stack is shipped | **Never** |
| Hardcode TPQN=480 and "round to nearest tick" for 7-tuplets | Avoids per-export TPQN computation | Audible MIDI drift; round-trip test failures | Only if v1.3 explicitly excludes 7+-tuplets (and that's documented in tutorial.flow) |
| Skip the LSP update in the same commit as new syntax | Smaller diff per commit | Two-day window where typing new syntax in VSCode produces error-spam; sets the "new feature feels broken" first impression | Only if a follow-up commit lands in the same PR |
| Use `Random.Shared` for Gaussian secondary draw | Simpler implementation | Determinism contract silently breaks (Pitfall 6) | **Never** — every PRNG draw must be from a seeded `Random` instance |
| Add new keyword without collision grep | Faster phase plan authoring | Mid-execution rename (precedent: Phase 14 `H`/`end`/`buf`/`_`) → Rule 3 deviation, ~30min lost per occurrence | Only if grep is empty (which is the gate) |
| Implement microtonal tuning at the transform layer instead of the render layer | One place to put the math | Transforms become tuning-aware → can't reuse them across tunings; Pitfall 5 cascades | **Never** — render-time only |
| Defer `tuning EqualTemperament` no-op block | "JI is the interesting case, ship that first" | Every test for the tuning block is JI-coupled; 12-TET regressions hide for months | Ship 12-TET no-op first, JI second |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| **DryWetMidi tick math** | Cast `double * TPQN` to `long` and trust it | Use rational tick math from the start: `ticks = (numerator * TPQN) / denominator` with integer arithmetic; assert `(numerator * TPQN) % denominator == 0` (auto-elevate TPQN if not) |
| **DryWetMidi pitch bend** | Use single channel for all microtonal voices | One MIDI channel per voice when pitch bend is active; otherwise the bend on voice A bends voice B too |
| **DryWetMidi key signature events** | Emit a key signature for every section change | Only at song start + at sections that actually change key (existing v1.2 code already handles this; verify tuplet-bearing sections still go through the right path) |
| **PulseAudio buffer alignment** | Render tuplet audio as separate buffers and concat | Render at sample-level granularity into a single buffer indexed by rational tick → samples; concat causes 1-2 sample boundary clicks at each tuplet edge |
| **VSCode extension VSIX bundling** | New keywords don't appear in completions because the per-platform VSIX hasn't been rebuilt | Rebuild all four VSIXs (linux-x64, win-x64, osx-x64, osx-arm64) in the same commit that adds keywords; verify by `grep` inside one VSIX |
| **MIDI import (`flow-midi/`)** | Quantizer assumes power-of-2 durations | Quantizer must handle imported tuplets (TPQN tolerance is already in `Quantizer.cs:444` — `tpqn / 48` — verify it's correct for elevated TPQN values) |
| **Module imports (`use`)** | Pragmas treated as runtime feature → leak via context | File-scoped at parser level, never enter `ExecutionContext` (Pitfall 4) |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Rational arithmetic on every audio sample | Audible drop-outs at high voice count | `Rational` only at score-and-MIDI level; convert to `double` at the audio boundary, once per note | When a song has > 200 simultaneous voices with tuplets |
| Auto-elevated TPQN to 36960 (7×11×480) on every export | MIDI export latency on long pieces | Cap TPQN at 9600 unless explicitly opted in via pragma; warn when the cap binds | When a piece uses 7- and 11-tuplets simultaneously |
| `MusicalContext.Tuning` table lookup per sample | Synth render slowdown | Pre-compute Hz table at section start; index by spelled-pitch → Hz | When the tuning table has > 12 entries (custom temperaments) |
| Box-Muller Gaussian on every humanize step | Compile-time slowdown — but only at score build, not audio | Acceptable; humanize is called once per note, not once per sample | Never (humanize is bounded by note count) |
| LSP re-parse on every keystroke for files with 100+ tuplets | Editor lag | Existing v1.2 LSP already incrementally parses; verify tuplet rule doesn't add a quadratic backtrack | When a file has > 200 nested tuplets (real-world: never) |
| Bar-fit validator runs N² over elements | Compile-time slowdown | The validator is linear in elements (sum + compare); ensure no inadvertent quadratic check | Never if implemented correctly |

---

## Security Mistakes

(Largely not applicable — Flow is a single-user CLI tool with no network surface. Two cases worth noting.)

| Mistake | Risk | Prevention |
|---------|------|------------|
| TPQN auto-elevation accepts unbounded LCM input | DoS via crafted .flow file with `(2:1 ... (3:1 ... (5:1 ... (7:1 ... (11:1 ... (13:1 ...)))))` → TPQN attempts to be 6,469,693,230 (overflows int) | Cap LCM growth at a fixed ceiling (suggested: 9600, matching existing safety pattern in `BuiltInFunctions.cs:1118` `steps > 1024`); error if exceeded |
| Pragmas are arbitrary identifiers from the source file | Future risk: a pragma that disables type checking could be enabled by an untrusted .flow file | Pragma registry is closed-set (PragmaRegistry static dict); unknown `enable foo` is an error, not a no-op |

---

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Tuplet duration that "should" fit a bar but doesn't (off by 1/12 of a beat) silently truncates | User sees their composition randomly missing notes; no diagnostic | Warn (charitable: still play, but tell them) — see Pitfall 2 option (A) |
| `enable H_alias` works in one file but not another, with no visual indicator | User confused why `H` is sometimes a note, sometimes an error | LSP semantic-token coloring distinguishes "H-as-note" from "H-as-identifier" based on file's enabled pragmas |
| Microtonal tuning doesn't propagate to MIDI export — exports as 12-TET silently | User thinks their JI piece sounds good but external tools play it 12-TET | Either: emit pitch-bend events (correct), or warn at export time "JI tuning detected; MIDI export will be 12-TET unless `--midi-pitchbend` enabled" |
| Negative slice indices silently change behavior between versions | v1.2 user upgrading to v1.3 sees different output for `slice(seq, 0, -1)` | Document the breaking change in CHANGELOG; add a v1.3 "Breaking changes" section to tutorial.flow with the rationale |
| `(? C4 E4 G4)` with seeded RNG and Gaussian humanize produces different output across .NET patch versions | "Code is the score" contract violated | Pin Gaussian implementation in Flow source (Box-Muller in `MathUtils.cs`), don't rely on `Random.NextGaussian` if/when .NET adds it |
| User writes `C4/12` outside a note stream as an arithmetic expression and gets a parse error | Confusion: "but C4/12 worked yesterday in my note stream" | Error message says "fractional note durations are only valid inside note streams; did you mean to wrap with `\| ... \|`?" |

---

## "Looks Done But Isn't" Checklist

For phase closure of every v1.3 phase:

- [ ] **Tuplets**: byte-identical determinism check passes for tutorial.flow + showcase.flow with at least one tuplet example added — verify `cmp -l` clean over two consecutive runs.
- [ ] **Tuplets**: a tuplet that doesn't divide cleanly into the bar produces a single warning (not zero, not five) — tested.
- [ ] **Fractional durations**: `C4/12` parses inside note streams and errors gracefully outside — tested both.
- [ ] **Pragmas**: `use "@something"` does NOT propagate enabled pragmas — tested with two-file fixture.
- [ ] **Pragmas**: pragmas are listed in LSP completion when typing `enable ` — manually verified in VSCode.
- [ ] **MIDI tick precision**: TPQN auto-elevates for 7-tuplet pieces; 7×480=3360 verified in exported file's TimeDivision header.
- [ ] **Tuning**: `tuning EqualTemperament { ... }` is a no-op (byte-identical to no tuning block) — tested.
- [ ] **Tuning**: `transpose(seq, 2)` inside a JI tuning block does not double-apply the tuning — tested by comparing audio output to a JI piece written in the new key directly.
- [ ] **Gaussian humanize**: same seed → same WAV bytes across two consecutive runs; bytes differ from uniform-humanize same seed (proving the new function actually does something).
- [ ] **DEFER-05 negative slice**: collision grep over `tests/` empty for `slice(.*, .*, -.*)` patterns; new behavior verified.
- [ ] **DEFER-04 multi-letter enharmonics**: `Fb`, `E#`, `Cb`, `B#` parse as expected and produce correct MIDI numbers (Fb = E = 64 in C4 octave; E# = F = 65; Cb = B (octave below) = 59; B# = C (octave above) = 72).
- [ ] **Collision grep transcripts**: every phase's VERIFICATION.md contains the empty-grep evidence for new keywords.
- [ ] **AUDIT-VERIFIED markers**: every modified file's existing markers are explicitly re-asserted or replaced.
- [ ] **Existing 70+ test files**: all pass after every phase lands; no `tests/test_*.flow` regressions.
- [ ] **xUnit harness**: 287/287 → 287+N/287+N green where N = new tests added in v1.3.
- [ ] **LSP**: typing partial tuplet syntax never produces > 1 active diagnostic; semantic tokens stable.
- [ ] **examples/showcase.flow**: includes at least one tuplet, one fractional duration, one pragma; cmp-clean across runs.
- [ ] **examples/tutorial.flow**: each new feature has a "try this" section with expected audible output.
- [ ] **PROJECT.md Validated section**: each closed feature moved from Active to Validated with phase reference.
- [ ] **MILESTONES.md**: v1.3 entry added with shipped date, phases, plans, and DEFER-01..06 marked closed.

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Floating-point drift introduced (Pitfall 1) | **HIGH** — touches every duration-handling site | (1) Revert tuplet syntax commit; (2) Land `Rational` type as its own commit; (3) Re-land tuplet syntax on top of `Rational`. Cannot retrofit drift fixes — must be foundational. |
| Bar-fit warnings missing/spammy (Pitfall 2) | LOW | Adjust the warning message; add unit tests for the three-state validator |
| TPQN insufficient (Pitfall 3) | MEDIUM | Compute LCM at export time; re-export reference MIDI files; update `examples/output/*.mid` byte pins |
| Pragma leaks via use (Pitfall 4) | **HIGH** — semantics break user files | (1) Hotfix release that explicitly resets pragmas at file boundary; (2) Document breaking change; (3) Add CI test for pragma isolation |
| Tuning interacts wrong with transforms (Pitfall 5) | **HIGH** — every JI piece sounds wrong | (1) Disable JI tuning in next patch (revert to 12-TET no-op); (2) Move tuning to render layer; (3) Re-enable JI in following milestone |
| Determinism broken by Gaussian (Pitfall 6) | MEDIUM | (1) Identify the unseeded draw; (2) Reseed; (3) Re-pin showcase.flow byte-hashes; (4) Add CI determinism check |
| LSP regression (Pitfall 7) | LOW-MEDIUM | (1) Hotfix VSIX with parser-error-recovery patch; (2) Republish per-platform VSIXs; (3) Add LSP snapshot tests |
| Test breakage from collision (Pitfall 8) | LOW | Rename the offending keyword (precedent: Phase 14 `s`/`e`, `xyz`, `rendered1`/`rendered2`) |
| AUDIT-VERIFIED silently invalidated (Pitfall 9) | LOW-MEDIUM | (1) Add the missing re-validation test; (2) Update marker comment; (3) Document in retrospective |
| DEFER hidden dependency exploded mid-execution (Pitfall 10) | MEDIUM | (1) Pause execution; (2) Run the missed spike retroactively; (3) Re-author phase plan; (4) Resume |

---

## Pitfall-to-Phase Mapping

Suggested phase ordering for v1.3 (informs roadmap):

| Phase # | Phase Name | Addresses Pitfalls | Verification |
|---------|-----------|--------------------|--------------|
| 0 | DEFER spike + dependency mapping | 10 | All six DEFER design notes written; dependency DAG documented |
| 1 | Foundation: Rational duration arithmetic | 1, 3 | `Rational` type added; `NoteValueType.ToFraction` callers migrated; round-trip exactness test passes for {2,3,4,5,6,7,8,9,11,12} ratios |
| 2 | Tuplet syntax `(N:M ...)` + bar-fit validator | 1, 2, 8 | Tuplet parses inside note streams only; `ValidateBarFit` 3-state result; collision grep empty; existing tests green; AUDIT-VERIFIED C5 re-asserted |
| 3 | Tuplet MIDI export + auto-elevated TPQN | 3 | TPQN auto-elevates per-piece; round-trip exact for all primitive ratios; cap at 9600 |
| 4 | Pragma system + `enable` keyword (file-scoped) | 4, 8 | File-scope test passes; LSP completion lists pragmas; collision grep empty for `enable`; v1.3-pragma-isolation test green |
| 5 | DEFER-04 multi-letter enharmonic edges | 10 | Fb/E#/Cb/B# parse; MIDI numbers correct; AUDIT-VERIFIED noted |
| 6 | DEFER-02/03 H alias (depends on Phase 4 + Phase 5) | 4, 10 | `enable H_alias` works file-scoped; `H♯` resolves correctly; collision grep over tests empty |
| 7 | DEFER-05 slice negative-from-end | 10 | Existing tests green (collision grep); negative-index test passes |
| 8 | DEFER-01 range(Int, Int) | 10 | Overload-ambiguity audit confirms no regression; tests green |
| 9 | Tier B/C: Arpeggio params + chord inversions/voicings | (no major pitfall) | Standard tests; audio output reasonable |
| 10 | Tier B/C: Delay sync to note values | (no major pitfall) | `delay(buf, "q")` produces same audio as `delay(buf, beats_to_ms("q"))` |
| 11 | Microtonal tuning context block (12-TET no-op only) | 5 | `tuning EqualTemperament` byte-identical to no block; transforms unchanged |
| 12 | Microtonal: JustIntonation tuning | 5 | Pitch bend events emitted in MIDI export; transform-tuning interaction test green; AUDIT-VERIFIED C1 re-asserted |
| 13 | Tier B/C: Scale linting (out-of-key warnings) | (interacts with 5) | Warnings respect tuning context |
| 14 | Tier B/C: Legato/portamento articulations | 9 | Envelope shaping changes don't break C3/C4 markers |
| 15 | Tier B/C: Snap-to-grid quantize | (no major pitfall) | Quantize result deterministic |
| 16 | Tier B/C: WAV pitch-shift on load | 9 | C4 fade markers re-asserted if buffer pre-pass added |
| 17 | DEFER-06 Gaussian humanize (LAST — depends on all PRNG sites) | 6 | Box-Muller in MathUtils; showcase.flow updated + cmp-clean across runs |
| 18 | Tutorial + showcase refresh + closure | (all) | Every v1.3 feature in tutorial.flow with audible output; showcase byte-identical determinism re-pinned |

**Critical orderings:**
- Phase 1 (Rational) **must** come before Phase 2 (tuplet syntax). Pitfall 1 root cause.
- Phase 4 (pragma) **must** come before Phase 6 (H alias). Pitfall 10 hidden dependency.
- Phase 5 (multi-letter enharmonic) **must** come before Phase 6 (H alias) and Phase 12 (JI). Pitfalls 5, 10.
- Phase 17 (Gaussian) **must** come last. Pitfall 6 — every other PRNG site must be locked first.

---

## Sources

**Codebase artifacts (HIGH confidence — directly inspected):**
- `flow-lang/Runtime/NoteStreamCompiler.cs` — bar validation logic, auto-fit duration computation, PRNG draw sites
- `flow-lang/TypeSystem/SpecialTypes/NoteValueType.cs` — power-of-2 duration enum, `ToFraction()` returning double
- `flow-lang/StandardLibrary/Audio/MidiExport.cs:17` — TicksPerQuarterNote = 480 hardcoded; line 195 uses `(long)(beats * TicksPerQuarterNote)`
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:1180-1280` — euclidean humanize PRNG protocol (D-09..D-17 invariants)
- `flow-lang/Lexing/SimpleLexer.cs` — note-stream-mode dispatch, `/` token handling
- `flow-lang/Lexing/TokenType.cs` — existing keyword list (Pickup, Progression, Pan, Gain, ReverbTime exist; enable/tuning/legato don't)
- `flow-lsp/Symbols/KeywordIndex.cs` — static keyword list for LSP completion
- `flow-lang/StandardLibrary/Audio/SynthUtils.cs:13-22` — synth white-noise RNG seeding contract
- `flow-lang/StandardLibrary/Audio/FileIO.cs:11-13,76` — TPDF dither RNG seeding contract
- `flow-lang/Interpreter/Interpreter.cs:75,303` — AUDIT-VERIFIED C2/C1 markers
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:239,261` — AUDIT-VERIFIED C5 markers
- `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs:105` — AUDIT-VERIFIED C3
- `flow-lang/StandardLibrary/Audio/BufferHelpers.cs:128` — AUDIT-VERIFIED C4
- `flow-midi/Conversion/Quantizer.cs:444` — TPQN tolerance pattern

**Planning artifacts (HIGH confidence — directly inspected):**
- `.planning/PROJECT.md` — current state, validated/active/deferred lists, key decisions
- `.planning/MILESTONES.md` — v1.0/v1.1/v1.2 history, DEFER-01..06 origin
- `.planning/RETROSPECTIVE.md` — top lessons (especially #5 reserved-token grep at plan time, #1 disputes warrant spike, #3 determinism contracts need every PRNG seeded)
- `CLAUDE.md` — charitable-interpretation memory, language philosophy, soft-failure error model precedent

**Industry conventions (MEDIUM confidence — well-documented but not freshly verified):**
- DryWetMidi `TicksPerQuarterNoteTimeDivision` SMF spec — TPQN range is 1..32767 (15-bit value when bit 15 is 0)
- Standard MIDI File 480 TPQN convention — Logic Pro / Cubase / FL Studio defaults
- Box-Muller transform — pinned implementation, not algorithmic uncertainty
- Haskell `LANGUAGE` pragma file-scoping — well-documented Haskell ecosystem convention
- Rust `#![feature(...)]` crate-attribute file-scoping — cf. https://doc.rust-lang.org/reference/attributes.html

**Anecdotal / inferred (LOW confidence):**
- Lilypond and Music21 tuplet "doesn't fit" behavior — described from documentation, not freshly run. Lilypond emits a warning and proceeds; Music21 throws. Flow's recommended behavior (Pitfall 2 option A) sits between, biased toward Lilypond per charitable-interpretation memory.

---

*Pitfalls research for: Flow v1.3 Composer DX Tier B/C — tuplets, fractional durations, pragmas, microtonal tuning, Gaussian humanize, and DEFER-01..06 closures*
*Researched: 2026-04-26*
