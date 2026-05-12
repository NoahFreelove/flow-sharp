# Phase 32: Full Scala (`.scl`) Tuning Loader — Specification

**Created:** 2026-05-10
**Ambiguity score:** 0.12
**Requirements:** 7 locked

## Goal

Add a Scala-format tuning loader that extends Phase 23's named-tunings wedge (just intonation, Pythagorean, equal temperament) to arbitrary user-supplied tuning systems. The composer surface is a new builtin `(loadScala "path/to/tuning.scl")` returning a first-class `Tuning` value plus a new `tuning t { section ... }` musical-context block applying it. Full Scala spec support: cents values + ratio values, optional `.kbm` keyboard mapping files for tonic + key-mapping, non-octave-repeating scales (Bohlen-Pierce, Carlos Alpha), negative cents (descending), and `!` line comments. Five canonical archive fixtures committed in-repo as the acceptance battery. Closes the v1.3 D-03 deferral.

## Background

Today (post-Phase 28 baseline):

**Phase 23 tuning infrastructure (already shipped):**
- `flow-lang/StandardLibrary/Audio/Tuning/RenderTuning.cs` — `readonly record struct RenderTuning(TuningSystem System, Mode Mode, char TonicLetter, int TonicAlteration)`. Default value triggers the byte-identical 12-TET short-circuit.
- `TuningSystem` enum: `EqualTemperament`, `JustIntonation`, `Pythagorean`.
- `Mode` enum: 7 church modes.
- `ChromaticRatioTable`, `RatioMath`, `TuningTables` — JI + Pythagorean ratio tables, mode-shift math.
- `PitchConversion.NoteToFrequency(note, RenderTuning)` — single entry point threaded through all 13 synthesizers per Pattern A.
- 5 file-scope pragmas in `PragmaRegistry`: `hAsB`, `justIntonation`, `pythagorean`, `equalTemperament`, `scaleLint`. All NO-ARG.
- `RenderingDiagnostics.WarnOnce` one-shot stderr advisory pattern for non-12-TET MIDI export and tuning-aware transform invariance (MICR-02).

**Phase 23 limitations:**
- Only 3 named tunings ship; users can't define custom ratio sets.
- Pragmas are file-scope, set once at top-of-file, no per-section variation.
- No support for non-12-TET scales (e.g. 22-note srutis, 43-tone Partch).

**Scala format (open ASCII spec from Huygens-Fokker Foundation):**
- `.scl` files: free-form ASCII. First non-blank-non-comment line = description string. Second line = step count (positive integer). Subsequent lines = step values, one per line. Each step is either a cents value (`100.0`, signed real) OR a ratio (`3/2`, two positive integers). Lines starting with `!` are comments.
- `.kbm` keyboard mapping files (optional companion): specify which MIDI key number maps to which scale degree, where the tonic sits, and how the scale wraps. Without KBM, default mapping = tonic on MIDI 60 (middle C).
- Non-octave-repeating: the FINAL step in the .scl file is the period (octave-equivalent). Most scales have 2:1 as the final step (true octave); Bohlen-Pierce has 3:1 (tritave); Carlos Alpha has ~78 cents (no period at all, just a chromatic step set).

**Scala archive (huygens-fokker.org/scala):**
- ~4,000 .scl files, public-domain release per the archive maintainers.
- Canonical fixtures: `partch_43.scl`, `slendro.scl`, `carlos_alpha.scl`, `pythagorean_12.scl`, `just_5limit.scl` — each ~1 KB or less.

**Closes v1.3 D-03 deferral:** "Microtonal scope is named-tunings wedge; full Scala loader deferred to v1.4." Phase 32 ships the full loader. ROADMAP success criterion #4 for Phase 23 points at "the documented v1.4 Scala-loader expansion (MICR-03)" — this phase IS that expansion.

## Requirements

1. **`(loadScala "path")` builtin returns a first-class `Tuning` value**: New builtin produces a Flow value representing a parsed Scala tuning.
   - Current: No way to load custom tuning; `RenderTuning` is C#-only
   - Target: New builtin `(loadScala "path/to/file.scl")` with overload `(loadScala "scl-path" "kbm-path")` for keyboard-mapping. Returns a Flow `Tuning` value (new type wrapping a richer internal `ResolvedTuning` than Phase 23's `RenderTuning` record — supports arbitrary step count, period override, KBM mapping). New `Tuning` type registered in `TypeSystem/SpecialTypes/TuningType.cs`. Tuning values are immutable; composable with the `tuning { ... }` block (Requirement 2)
   - Acceptance: `Tuning t = (loadScala "examples/scala/partch_43.scl")` parses + compiles; `(print (str t))` produces a human-readable description (step count + period + first-few-step preview)

2. **`tuning t { section ... }` musical-context block applies a Tuning**: New musical-context block extending the existing tempo/timesig/key/swing/reverbTime stack.
   - Current: Tuning is set ONLY via file-scope pragma (`enable justIntonation;` etc.). No per-section variation.
   - Target: New AST node `TuningContextStatement` (parallel to `MusicalContextStatement`), new parser rule, new interpreter dispatch that pushes a `RenderTuning` value (derived from the `Tuning` arg) onto `ExecutionContext.MusicalContextStack`. Stacks just like `tempo X { ... }` — inner scope inherits + overrides outer. **Last-wins semantics**: if `enable justIntonation;` is at top-of-file AND a `tuning customScala { section ... }` block is active, the section uses the custom tuning; outside the block, JI is active. Mirrors how musical-context blocks stack today
   - Acceptance: A `.flow` script with `enable justIntonation; tempo 120 { tuning partch { section a { ... } } section b { ... } }` renders section `a` under Partch tuning and section `b` under JI tuning. Verified by capturing rendered samples + cross-comparing spectrograms

3. **Core .scl parser**: Hand-rolled parser handles cents values, ratio values, comments, descriptions.
   - Current: No .scl parser exists
   - Target: New `flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs`. Parses the .scl format per the Huygens-Fokker spec:
     - First non-blank-non-`!` line: description string (captured but not load-blocking)
     - Second non-blank-non-`!` line: step count (integer; must be > 0)
     - Subsequent step lines: cents (e.g. `100.0`, `-50.5`) OR ratio (e.g. `3/2`, `9/8`)
     - Lines starting with `!` are comments; blank lines tolerated
     - Negative cents represent descending steps (rare; Carlos Alpha and similar)
     - The FINAL step is the period (octave-equivalent for octave-repeating scales; tritave or other for non-octave-repeating)
   - Acceptance: 5 canonical fixtures parse without error. Step counts, period values, and per-step ratios extracted match reference values from the Scala archive metadata

4. **.kbm keyboard mapping support**: Optional companion file parses + applies tonic + key mapping.
   - Current: No keyboard mapping; tonic is implicit (defaults to whatever the active `key` musical-context names)
   - Target: New `ScalaKbmParser.cs`. `.kbm` format per the Huygens-Fokker spec: map size (integer), first/last MIDI key, middle key (where the reference frequency lives), reference frequency (e.g. 440 Hz for A4), formal octave repetition (degree count per period), then `map size` lines of integer keymap entries (which scale degree each MIDI key gets). When loaded via `(loadScala "tuning.scl" "tuning.kbm")`, the Tuning value carries the keyboard mapping internally; rendering applies the mapping at note-to-frequency conversion. Without a KBM, default mapping: tonic on MIDI 60 (middle C), 440 Hz reference at MIDI 69 (A4), period-per-octave
   - Acceptance: A test fixture loading a .scl + matching .kbm produces a different pitch mapping than loading the .scl alone (default mapping); verified by frequency comparison at a non-tonic MIDI note

5. **Non-octave-repeating scale support**: Bohlen-Pierce (3:1 tritave) + Carlos Alpha (no period) render correctly.
   - Current: Phase 23 assumes 2:1 octave equivalence; the `ChromaticRatioTable` is sized at 12 entries per octave
   - Target: `Tuning` value carries a `Period` field. When the FINAL step in .scl is `2/1` or `1200.0`, period = octave (Phase 23 behavior). When it's `3/1` or `1901.955`, period = tritave (Bohlen-Pierce). When step values don't span a clean period, the Tuning is FLAT — each MIDI note maps to the step at `(midiNote - tonic) % stepCount`, no period-folding. Pitch conversion math becomes Step-relative rather than octave-relative for non-octave Tunings
   - Acceptance: Bohlen-Pierce fixture (`carlos_alpha.scl` or `bp_13.scl`) renders an ascending sequence with audibly correct non-octave intervals; frequencies match the Huygens-Fokker reference values within ±0.1 cents

6. **Last-wins pragma interaction + canonical archive fixture battery**: 5 in-repo fixtures pass parse + render.
   - Current: No fixtures; no pragma interaction rules
   - Target:
     - **Fixtures** (committed to `flow-lang.Tests/fixtures/scala/`):
       - `partch_43.scl` — Harry Partch's 43-tone scale, octave-repeating, ratio-only
       - `slendro.scl` — Indonesian gamelan, octave-repeating, cents-only
       - `carlos_alpha.scl` — Wendy Carlos Alpha, non-octave (~78 cent step set)
       - `pythagorean_12.scl` — 12-step Pythagorean, ratio-only
       - `just_5limit.scl` — 12-step 5-limit JI, ratio-only
     - **License**: Single `LICENSE.md` co-located at `flow-lang.Tests/fixtures/scala/LICENSE.md` citing the Scala archive (huygens-fokker.org) and the public-domain release per the archive maintainers
     - **Pragma interaction (last-wins)**: When `enable justIntonation;` (or any of the 3 named-tuning pragmas) is at top-of-file AND a `tuning customScala { ... }` block is active inside, the block's tuning wins inside the block; the pragma applies outside. No errors raised; pure stacking semantics
   - Acceptance: All 5 fixtures parse + render via `(loadScala "...")` + `tuning t { ... }`. Last-wins interaction test asserts a script with `enable justIntonation; tuning partch { ... } ` renders different bytes inside vs outside the block (compared via spectral envelope)

7. **Clear error semantics for malformed input**: Errors point at the offending line + column with actionable diagnostic text.
   - Current: No error format
   - Target: `ScalaParser` collects `(lineNumber, columnNumber)` as it scans. Errors raise `ScalaParseException` with message format: `{filepath}:{line}:{col} — {what was expected} got '{what was found}'`. Examples:
     - `partch_43.scl:17:8 — expected cents value or ratio, got 'foo'`
     - `slendro.scl:3:1 — expected step count (positive integer), got '0'`
     - `bad.kbm:5:1 — expected reference frequency (positive Hz), got '-50'`
   - Error surface mirrors SimpleLexer's existing diagnostic style (file:line:col prefix). Whole-file pass-or-fail: any error aborts the load; no partial loads
   - Acceptance: 3 negative-case fixtures committed (`malformed_step_count.scl`, `malformed_cents.scl`, `malformed_kbm.kbm`) trigger errors with the expected format. Unit tests assert error message includes line + column + actionable description

## Boundaries

**In scope:**
- `(loadScala "path")` + `(loadScala "scl-path" "kbm-path")` builtins
- New `Tuning` Flow value type (`TypeSystem/SpecialTypes/TuningType.cs`)
- `tuning t { ... }` musical-context block with parser + AST + interpreter dispatch
- `ScalaParser.cs` for .scl format (cents, ratios, comments, descriptions, negative values)
- `ScalaKbmParser.cs` for .kbm keyboard mapping
- Non-octave-repeating scale support via `Period` field on Tuning + flat (non-period-folding) scale type
- 5 canonical Scala archive fixtures + LICENSE.md in `flow-lang.Tests/fixtures/scala/`
- 3 negative-case malformed fixtures for error-path tests
- Unit tests for parser + KBM + non-octave + pragma interaction + error semantics
- New tutorial chapter or chapter addition demonstrating `(loadScala ...)` + `tuning { ... }` (if planner finds the tutorial benefits; otherwise pragma docs are sufficient)

**Out of scope:**
- Live-edit reload of .scl files via FileSystemWatcher — Phase 32 loads once at builtin-call time
- Scala SoundFont format (.sf2) — different format, deferred to v1.5+
- MTS (MIDI Tuning Standard) per-channel pitch-bend MIDI export — Phase 23 D-13 advisory continues; MTS-MIDI deferred (parallel concern to Phase 28 multi-track MIDI)
- Caching parsed .scl content across multiple `(loadScala ...)` calls in the same FlowEngine instance — Phase 32 re-parses each time; cache deferred unless profiling shows hot loop
- User-edited tuning blob in source (e.g. `(tuningFromCents [100.0, 200.0, 300.0, ...])` builtin) — Phase 32 is file-based only; in-source tuning deferred
- Tuning interpolation / morphing between two Tunings — deferred
- Per-instrument tuning override within a section — Phase 32's tuning block applies to all instruments rendering that section
- GUI for picking a tuning from a UI — CLI only, source only
- Octave stretching parameters (independent of period) — deferred
- Multi-period scales (more than one period in the .scl file) — Scala spec doesn't actually permit this; final step is THE period

**Adjacent problems excluded:**
- Phase 23 named-tuning pragmas continue to work unchanged. Phase 32 doesn't remove or repurpose them.
- Transform invariance per MICR-02 continues — `transpose`, `invert`, etc. still operate on MIDI pitches not absolute frequencies
- MIDI export under custom Scala tunings still emits 12-TET pitches per Phase 23 D-13 advisory warning; this phase doesn't change MIDI export
- LSP completions for `(loadScala ...)` — Phase 31 (LSP enhancements) owns LSP work; Phase 32 just adds the builtin and trusts Phase 31 to surface it via the import-filter

## Constraints

- **Linux x64 dev path**: matches Phase 30 / 31 platform scope
- **.NET 10**: no new external NuGet packages — hand-rolled .scl/.kbm parsers; no LINQ-heavy hot loops (parser is one-shot, called rarely)
- **Last-wins pragma interaction**: Phase 23 named-tuning pragmas and Phase 32 `tuning { ... }` blocks coexist with stacking semantics; no errors raised on combination
- **Two-run byte-identical determinism**: same script + same `.scl`/`.kbm` files + same git SHA → byte-identical WAV/MID. Phase 28's two-run gate continues
- **Test runtime budget**: parser unit tests (10 fixtures × parse + render small sample) ≤ 15 seconds total
- **File size budget**: 5 in-repo fixtures + 3 negative-case fixtures total ≤ 100 KB
- **No KBM = default mapping**: tonic on MIDI 60, A4 reference at MIDI 69 = 440 Hz, period-per-octave
- **Whole-file pass-or-fail on errors**: malformed `.scl` aborts the load; no partial-load mode
- **Negative cents support**: signed cents values are valid; downward steps preserved (used by Carlos Alpha and similar)
- **Phase 23 D-13 advisory preserved**: MIDI export under non-12-TET (including Phase 32 custom tunings) emits 12-TET pitches + one-shot stderr warning
- **No back-compat constraint**: per Phase 31 precedent, the `tuning { ... }` keyword may shadow an existing user identifier named `tuning` in some scripts; pre-public lean accepts the break (any v1.3 scripts using `tuning` as an identifier name need rename)

## Acceptance Criteria

- [ ] `(loadScala "path")` builtin registered + callable from `.flow` scripts
- [ ] `(loadScala "scl-path" "kbm-path")` 2-arg overload registered + callable
- [ ] Returns a `Tuning` value; `Tuning` type appears in `TypeSystem/SpecialTypes/`
- [ ] `tuning t { section ... }` musical-context block parses + executes
- [ ] All 5 canonical fixtures parse without error: `partch_43.scl`, `slendro.scl`, `carlos_alpha.scl`, `pythagorean_12.scl`, `just_5limit.scl`
- [ ] `.kbm` companion file loaded via 2-arg builtin alters pitch mapping vs default
- [ ] Non-octave-repeating Bohlen-Pierce / Carlos Alpha fixture renders ascending sequence with frequencies within ±0.1 cents of reference values
- [ ] Negative cents values produce descending intervals (verified via frequency comparison)
- [ ] `!` line comments skipped during parse (verified via fixture with embedded comments)
- [ ] Last-wins pragma interaction: `enable justIntonation; tuning partch { ... }` renders Partch inside the block, JI outside; verified by spectral envelope comparison
- [ ] Malformed `.scl` raises error with format `{file}:{line}:{col} — {expected} got '{found}'`; 3 negative-case fixtures exercise the error path
- [ ] License attribution: `flow-lang.Tests/fixtures/scala/LICENSE.md` cites Huygens-Fokker / Scala archive public-domain release
- [ ] Phase 23 named-tuning pragmas (`enable justIntonation;`, `pythagorean;`, `equalTemperament;`) continue to work unchanged; existing Phase 23 unit tests stay GREEN
- [ ] Phase 23 D-13 advisory warning continues to fire on `writeMidi` under custom Scala tunings (verified via WriteMidiWarningFacts extension)
- [ ] Two-run byte-identical determinism: same script + same fixtures + same git SHA → identical WAV/MID
- [ ] Full unit suite GREEN
- [ ] `examples/tutorial.flow` (if planner chooses) demonstrates `(loadScala "...")` + `tuning t { ... }` end-to-end with audible output

## Ambiguity Report

| Dimension          | Score | Min  | Status | Notes                                                                                          |
|--------------------|-------|------|--------|------------------------------------------------------------------------------------------------|
| Goal Clarity       | 0.93  | 0.75 | ✓      | Builtin + block surface locked; full Scala feature subset locked                               |
| Boundary Clarity   | 0.85  | 0.70 | ✓      | Live-reload, SF2, MTS-MIDI, in-source tuning, GUI all explicit-deferred                        |
| Constraint Clarity | 0.85  | 0.65 | ✓      | Error format locked file:line:col; whole-file pass-or-fail; no KBM = MIDI60 tonic A4=440 Hz   |
| Acceptance Criteria| 0.85  | 0.70 | ✓      | 5 canonical fixtures named; ±0.1 cent tolerance; 3 negative fixtures; pragma-interaction test |
| **Ambiguity**      | 0.12  | ≤0.20| ✓      | Gate passed                                                                                    |

## Interview Log

| Round | Perspective       | Question summary                                                              | Decision locked                                                                                                                    |
|-------|-------------------|-------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------|
| 1     | Researcher        | Composer surface — pragma-with-arg, builtin, or block?                        | Builtin `(loadScala "path")` + new `tuning t { ... }` musical-context block (preserves no-arg pragma convention)                  |
| 1     | Researcher        | Scala feature subset?                                                         | All 4: core .scl (cents + ratios), .kbm keyboard mapping, non-octave-repeating, negative cents + comments                          |
| 1     | Researcher        | Test fixture battery?                                                         | 5 canonical Scala archive fixtures: partch_43, slendro, carlos_alpha, pythagorean_12, just_5limit                                  |
| 2     | Boundary Keeper   | Malformed .scl error semantics?                                               | Clear error pointing at line:column with actionable description; whole-file pass-or-fail                                          |
| 2     | Boundary Keeper   | Interaction with Phase 23 pragmas?                                            | Last-wins inside the block; pragma applies outside; mirrors existing musical-context stacking                                      |
| 2     | Boundary Keeper   | Fixture licensing?                                                            | In-repo with per-file LICENSE.md attribution citing Scala archive public-domain release                                            |

---

*Phase: 32-full-scala-scl-tuning-loader*
*Spec created: 2026-05-10*
*Next step: /gsd-discuss-phase 32 — implementation decisions (Tuning value internal representation, KBM-default-when-missing handling, ratio-vs-cents internal normalization, error class hierarchy)*
