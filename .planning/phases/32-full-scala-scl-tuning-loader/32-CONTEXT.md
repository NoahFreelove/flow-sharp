# Phase 32: Full Scala (`.scl`) Tuning Loader — Context

**Gathered:** 2026-05-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Add a Scala-format tuning loader that extends Phase 23's named-tunings wedge (just intonation, Pythagorean, equal temperament) to arbitrary user-supplied tuning systems. Two composer-facing surfaces ship together:

1. A new builtin `(loadScala "path/to/file.scl")` (+ 2-arg overload with companion `.kbm`) returning a first-class `Tuning` value.
2. A new `tuning <expr> { section ... }` musical-context block applying that value to the work inside.

Full Scala feature subset: cents + ratio step values, `.kbm` keyboard mapping files, non-octave-repeating scales (Bohlen-Pierce, Carlos Alpha), negative cents (descending), `!` line comments. Five canonical archive fixtures committed in-repo as the acceptance battery. Closes the v1.3 D-03 deferral.

</domain>

<spec_lock>
## Requirements (locked via SPEC.md)

**7 requirements are locked.** See `32-SPEC.md` for full requirements, boundaries, and acceptance criteria.

Downstream agents MUST read `32-SPEC.md` before planning or implementing. Requirements are not duplicated here.

**In scope (from SPEC.md):**
- `(loadScala "path")` + `(loadScala "scl-path" "kbm-path")` builtins
- New `Tuning` Flow value type (`TypeSystem/SpecialTypes/TuningType.cs`)
- `tuning t { ... }` musical-context block with parser + AST + interpreter dispatch
- `ScalaParser.cs` for .scl format (cents, ratios, comments, descriptions, negative values)
- `ScalaKbmParser.cs` for .kbm keyboard mapping
- Non-octave-repeating scale support via `Period` field on Tuning + flat (non-period-folding) scale type
- 5 canonical Scala archive fixtures + `LICENSE.md` in `flow-lang.Tests/fixtures/scala/`
- 3 negative-case malformed fixtures for error-path tests
- Unit tests for parser + KBM + non-octave + pragma interaction + error semantics
- New tutorial chapter or chapter addition demonstrating `(loadScala ...)` + `tuning { ... }` (planner's discretion)

**Out of scope (from SPEC.md):**
- Live-edit reload of .scl files via FileSystemWatcher
- SoundFont (.sf2) format
- MTS (MIDI Tuning Standard) per-channel pitch-bend MIDI export
- Caching parsed .scl content across `(loadScala ...)` calls in the same FlowEngine instance
- User-edited tuning blob in source (`(tuningFromCents [...])`)
- Tuning interpolation / morphing
- Per-instrument tuning override within a section
- GUI for picking a tuning
- Octave stretching parameters
- Multi-period scales

</spec_lock>

<decisions>
## Implementation Decisions

### Tuning value internal representation

- **D-01: Step storage = eager-normalize to cents.** `ResolvedTuning.StepCents` is `double[]`. Ratio inputs `n/d` convert at parse time via `1200.0 * Math.Log2((double)n/d)`. Single uniform representation downstream of the parser; JI fans lose some round-trip precision (e.g. `3/2` becomes `701.955…`) but the math is uniform.
- **D-02: Pre-compute 128-entry MIDI→Hz table at load time.** `ResolvedTuning.MidiToHz` is `double[128]`, populated from `StepCents` + `PeriodCents` + KBM + reference frequency. Render-time `PitchConversion.NoteToFrequency` becomes an O(1) array lookup. ~1 KB per Tuning. Mirrors Phase 23's `ChromaticRatioTable` static-table pattern.
- **D-03: Extend `RenderTuning` with `ResolvedTuning? Custom` field.** When `Custom` is non-null, `PitchConversion.NoteToFrequency(note, RenderTuning)` reads `Custom.MidiToHz[note]` directly; when null, falls through to existing 12-TET / JI / Pythagorean logic. All 13 synthesizer call sites stay untouched (Phase 23 D-05 Pattern A preserved). Byte-identical 12-TET short-circuit still fires (default `RenderTuning` has `Custom = null`).
- **D-04: Capture `Description` string.** `ResolvedTuning.Description` stores the verbatim first non-comment line from the `.scl`. `(str t)` renders `Tuning("<description>", N steps, period X:Y)`. ~50 bytes per Tuning; round-trippable in composer logs.

### KBM defaults (when no `.kbm` is loaded)

- **D-05: `ScalaKbmParser.Default(ResolvedTuning t)` static factory.** Internal model is ALWAYS "has KBM"; no nullable `Kbm` field, no per-call branching in `PitchConversion`. Factory takes the parsed `ResolvedTuning` so the synthetic KBM can adopt the tuning's period (see D-07).
- **D-06: KBM wins for tonic placement; `key` block stays orthogonal.** When `key Dmajor { tuning partch { ... } }` is active, `key` continues to drive scale-degree semantics (roman numerals, etc.) but the tuning math reads tonic from the KBM (default = MIDI 60, A4=440 Hz at MIDI 69, period-per-octave). Mirrors how DAW tuning plugins work — tuning is independent of key signature. For non-octave scales (Bohlen-Pierce), the concept of "D major" doesn't map cleanly; keeping `key` orthogonal avoids that surprise class entirely.
- **D-07: Default KBM auto-adopts the Tuning's period.** When the loaded `.scl` is non-octave-repeating (e.g. `carlos_alpha.scl` has period ≈78 cents), `ScalaKbmParser.Default(tuning)` produces a KBM with `Period = tuning.Period`. Default KBM is always consistent with its tuning; the period-mismatch edge case is dissolved structurally.
- **D-08: Unmapped MIDI keys render as silence + one-shot stderr advisory.** Honors the Scala spec's intent for `x` keymap entries ("no sound"). Render emits zero samples for the note's duration; advisory `[tuning] note X unmapped under '<description>' — rendered as rest` fires via `RenderingDiagnostics.WarnOnce` (Phase 23 pattern).

### Ratio vs cents normalization

- **D-09: Negative cents accepted verbatim.** `StepCents[i]` may be negative; `PitchConversion`'s `2^(stepCents/1200)` math naturally produces a ratio < 1 (descending pitch). Required for Carlos Alpha and similar.
- **D-10: Period extracted to dedicated field.** `ResolvedTuning.PeriodCents` is a separate `double`; `StepCents[]` carries only the N-1 intra-period steps (NOT the period itself). Render code reads from one OR the other — never both. Step-count semantics (`StepCents.Length`) match "number of pitches per period" without an off-by-one.
- **D-11: Preserve original ratio form for ratio inputs only.** `ResolvedTuning.Ratios` is a `Dictionary<int, (int Num, int Den)>` keyed by step index; ratio inputs land here, cents inputs don't. `(str t)` and error messages can show exact ratio form for JI fans; cents inputs unaffected. Small overhead.

### Tuning context stacking

- **D-12: Replace `MusicalContext.Tuning` (nullable scalar) with `Stack<RenderTuning> TuningStack`.** Pragma `enable justIntonation;` pushes once at file scope and is never popped; `tuning t { ... }` block push/pop wraps inner work. Last-wins falls out naturally from stack semantics — render-time code reads `TuningStack.Peek()` (or default 12-TET when empty). **Phase 23 D-05's non-stacked rationale is explicitly superseded** now that stacking IS needed.
- **D-13: New `TuningContextStatement` AST node parallel to `MusicalContextStatement`.** Dedicated AST node carrying `Expression TuningExpr` + body block. Reason for the parallel node (not a 6th `MusicalContextType` enum variant): the existing `MusicalContextStatement` variants (Tempo/Timesig/Swing/Key/Dynamics/Rit/Accel/Pan/Gain/ReverbTime) all carry SCALAR primitive values; `tuning` carries a `Tuning`-typed expression. Parallel nodes keep value-shape and dispatch clean. Narrow blast radius — `MusicalContextStatement` and its parser stay untouched.
- **D-14: Blocks force-close at REPL eval boundary.** Even if a user types `tuning partch { section a { ... }` with no closing `}` at the REPL, the eval boundary pops the stack back to its pre-eval state. Pragmas remain sticky across REPL evals (per Phase 23 D-08); blocks remain ephemeral. Mirrors how `tempo`/`timesig` blocks already behave.
- **D-15: `tuning <expr> { ... }` accepts three forms.** All route through `TuningContextStatement.TuningExpr`:
  - `tuning partch { ... }` — identifier evaluating to a previously-bound `Tuning`-typed variable.
  - `tuning (loadScala "x.scl") { ... }` — inline function call returning `Tuning`.
  - `tuning "x.scl" { ... }` — string-literal sugar that desugars at parse time to `(loadScala "x.scl")`. Error semantics for the literal form surface at the `tuning "x"` line, not at a separate (synthetic) load call.

### Research-surfaced decisions (added 2026-05-13 after `gsd-phase-researcher`)

- **D-16: Fixture filenames + content.** Commit verified Huygens-Fokker archive contents IN-REPO under the SPEC-mandated names: archive `pyth_12.scl` → `pythagorean_12.scl`; archive `ji_12.scl` → `just_5limit.scl`. Document the rename in `flow-lang.Tests/fixtures/scala/LICENSE.md` AND in the destination file's `!` comment header. The `ji_12.scl` content (12 entries, 5-limit-dominant with a 7-limit tritone `7/5` at step 6) is accepted as the `just_5limit.scl` fixture — matches the SPEC's "5 canonical 12-tone-or-larger fixtures" framing.
- **D-17: License attribution wording = softened community-use.** `LICENSE.md` reads "Released for free use per the long-standing community understanding" + Huygens-Fokker attribution + archive URL + original-filename-to-in-repo-filename mapping. Mirrors how other open-source projects vendor Scala archive files. No upstream contact required.
- **D-18: Parser strictness = strict reject on three optional formats** the Scala spec is silent on:
  - `3 / 2` (whitespace around slash) — REJECT
  - `1.5e2` (scientific notation in cents) — REJECT
  - `100,5` (comma-decimal cents) — REJECT
  Parser uses `CultureInfo.InvariantCulture` and does NOT pass `NumberStyles.AllowExponent`. Tolerance can be added later if a real-world file surfaces; tightening would break callers.
- **D-19: Tutorial chapter included in Phase 32 scope.** Add either a new chapter to `examples/tutorial.flow` OR a focused new file (e.g. `examples/scala/intro.flow`) demonstrating `(loadScala "…")` + `tuning t { … }` end-to-end. ~30–60 lines of Flow source; single plan task; locks in the composer-facing surface as part of the phase. Renders audibly distinct output (the acceptance criterion in SPEC §13).

### Claude's Discretion

These weren't asked but flow from the SPEC + the decisions above. Planner may refine.

- **Error class hierarchy.** `ScalaParseException` extends `flow-lang/Parsing/TypeParser.cs:335`'s existing `ParseException`. `ScalaKbmParseException` likewise. Reuses Flow's established `{file}:{line}:{col} — expected X got 'Y'` format. No new base class.
- **Fixture sourcing.** The 5 canonical archive files (`partch_43.scl`, `slendro.scl`, `carlos_alpha.scl`, `pythagorean_12.scl`, `just_5limit.scl`) are pulled byte-identical from huygens-fokker.org/scala and committed under `flow-lang.Tests/fixtures/scala/` with a single co-located `LICENSE.md` citing the archive's public-domain release. 3 negative-case fixtures (`malformed_step_count.scl`, `malformed_cents.scl`, `malformed_kbm.kbm`) are hand-authored minimal repros.
- **Tuning value mutability.** `sealed class ResolvedTuning` with readonly fields throughout. The Flow-facing `Tuning` value (in `TypeSystem/SpecialTypes/TuningType.cs`) wraps a `ResolvedTuning` reference; equality follows reference identity by default (two `(loadScala "x.scl")` calls produce distinct values even with identical content — Phase 32 doesn't cache per the SPEC out-of-scope list).
- **PitchConversion entry-point pattern.** `flow-lang/StandardLibrary/Audio/PitchConversion.cs` already hosts `NoteToFrequency`. Extend that file in-place; no parallel API. Branch on `RenderTuning.Custom != null` at the top of the function (single conditional cost; remains the SOLE entry point).
- **Tuning block + voicePool interaction.** Phase 28 `voicePool N { ... }` and Phase 32 `tuning t { ... }` are independent musical-context blocks; they nest in either order with no interaction.
- **Phase 32 baseline strategy = tolerance-only.** No fixed-byte `baselines/Phase32/*.wav` baseline committed. Acceptance asserts per-step frequency within ±0.1 cents of reference values (SPEC Requirement 5) and last-wins spectral-envelope difference (SPEC Requirement 6 acceptance) via RMS regression helpers. Pre-Phase-28 byte-identical baseline contract is already relaxed (`CLAUDE.md` § Conventions); two-run-cmp-clean determinism is preserved at the I/O layer per SPEC constraint.
- **Unmapped-key advisory cardinality = once per `(description)` per process.** Matches Phase 23 D-13's "one warning per tuning name per process" pattern. `RenderingDiagnostics.WarnOnce` sentinel keyed by the Tuning's `Description` string.
- **`tuning` keyword = fully reserved** (NOT added to the Parser.cs:247 keyword-as-proc-name allowlist). Per SPEC line 139 pre-public-lean accepting the break. Cleaner break, less code, surfaces the keyword to LSP completions naturally.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Locked requirements

- `.planning/phases/32-full-scala-scl-tuning-loader/32-SPEC.md` — **Locked requirements** (7 reqs, ambiguity 0.12). MUST read before planning.

### Roadmap + project intent

- `.planning/ROADMAP.md` §"Phase 32: Full Scala (`.scl`) Tuning Loader" — Goal, dependencies, success criteria
- `.planning/REQUIREMENTS.md` — Phase 23 MICR-02/MICR-03 entries (Phase 32 closes MICR-03)

### Phase 23 prior decisions this phase amends or builds on

- `.planning/phases/23-microtonal-tuning-wedge/23-CONTEXT.md` §"Implementation Decisions" — D-05 (top-level field, NOW SUPERSEDED by Phase 32 D-12), D-08 (REPL sticky pragmas, EXTENDED by Phase 32 D-14 for blocks), D-13 (MIDI export 12-TET advisory, PRESERVED unchanged)

### Phase 23 code that Phase 32 extends

- `flow-lang/StandardLibrary/Audio/Tuning/RenderTuning.cs` — `readonly record struct RenderTuning`; **extend with `ResolvedTuning? Custom` field** (D-03)
- `flow-lang/StandardLibrary/Audio/Tuning/ChromaticRatioTable.cs` — Phase 23 ratio tables; pattern reference for `MidiToHz[128]` layout (D-02)
- `flow-lang/StandardLibrary/Audio/Tuning/RatioMath.cs` — existing cents/ratio math helpers; reuse for `n/d → cents` conversion in parser
- `flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs` — JI / Pythagorean ratio tables; pattern reference for fixed table layouts
- `flow-lang/StandardLibrary/Audio/PitchConversion.cs` — single-entry-point `NoteToFrequency(note, RenderTuning)`; **extend in-place** with the `Custom != null` branch (D-03, Claude's Discretion)

### Runtime / interpreter integration

- `flow-lang/Runtime/MusicalContext.cs:62–67` — top-level `Tuning` field (Phase 23 D-05); **convert to `Stack<RenderTuning> TuningStack`** (D-12)
- `flow-lang/Parsing/Parser.cs:104–143` — `ParseMusicalContextStatement` dispatch table for tempo/timesig/swing/key/etc.; **add parallel `ParseTuningContextStatement`** entry point (D-13)
- `flow-lang/Ast/Statements/` — home for new `TuningContextStatement` record (D-13)
- `flow-lang/Interpreter/Interpreter.cs` — dispatch case for `TuningContextStatement` (push/pop on `TuningStack`)
- `flow-lang/Runtime/PragmaRegistry.cs` (or wherever Phase 23's 5 pragmas live) — pragma handler for `justIntonation` / `pythagorean` / `equalTemperament` now PUSHES on `TuningStack` instead of setting the scalar field (D-12)

### Type system

- `flow-lang/TypeSystem/SpecialTypes/` — pattern home for the new `TuningType.cs`. Closest analogs: `SequenceType.cs`, `SongType.cs` (reference-typed, composite values)
- `flow-lang/Parsing/TypeParser.cs:335` — existing `ParseException`; **extend for `ScalaParseException` + `ScalaKbmParseException`** (Claude's Discretion)

### Diagnostics

- `flow-lang/StandardLibrary/Audio/RenderingDiagnostics.cs` (or wherever `WarnOnce` is hosted) — Phase 23 one-shot stderr advisory pattern; **reused for D-08 (unmapped MIDI key)** and preserved for D-13 (MIDI export under custom tuning)

### Fixture + license home

- `flow-lang.Tests/fixtures/scala/` — **new directory** for 5 canonical + 3 negative fixtures + `LICENSE.md` (Claude's Discretion)

### Project intel

- `CLAUDE.md` §"Music Types Quick Reference" — pattern for special-type literal/coercion table; planner may add a `Tuning` row
- `CLAUDE.md` §"Standard Library Modules" — `audio.flow` is the likely surface to re-export `loadScala` if a convenience alias is wanted

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`PitchConversion.NoteToFrequency(note, RenderTuning)`** — single-entry-point pattern from Phase 23; Phase 32's custom tuning math threads through this same call site via the new `Custom` field (no parallel API).
- **`RatioMath.cs`** — existing `cents = 1200 * log2(ratio)` helper; reuse in `ScalaParser` ratio→cents conversion (D-01).
- **`ChromaticRatioTable.cs`** — pattern for a fixed-size MIDI lookup table; mirror its shape for `ResolvedTuning.MidiToHz[128]` (D-02).
- **`RenderingDiagnostics.WarnOnce`** — one-shot stderr advisory utility; reuse for D-08 (unmapped key) and D-13 preservation (MIDI export under custom tuning).
- **`MusicalContext` push/pop stacks** — `Tempo`, `TimeSignature`, `Key`, `Swing`, `ReverbTime` (and Phase 28 `VoicePool`) already use stack push/pop. The new `TuningStack` mirrors this shape exactly (D-12).
- **`ParseMusicalContextStatement` dispatch** — existing pattern for parsing `tempo X { ... }`-style blocks. `ParseTuningContextStatement` parallel function follows the same shape (D-13).
- **`ParseException` at `Parsing/TypeParser.cs:335`** — existing exception base with `{file}:{line}:{col}` format; extend for ScalaParseException (Claude's Discretion).

### Established Patterns

- **Phase 23 Pattern A** — single render-time entry point (`NoteToFrequency`) for ALL tuning math. Phase 32 preserves Pattern A by routing through `RenderTuning.Custom` rather than introducing a parallel API.
- **`readonly record struct` for value-typed render config; `class` for variable-length tuning data** — Phase 23 `RenderTuning` is a `readonly record struct` because it's stack-allocated and lives in hot paths. Phase 32's `ResolvedTuning` is a `sealed class` because it carries variable-length `StepCents[]` + `Ratios` dictionary + 128-entry table; struct copy would be expensive.
- **Eager-precompute static tables** — Phase 23's `ChromaticRatioTable` precomputes all named-tuning ratios at static-ctor time. Phase 32's `MidiToHz[128]` follows the same eager-table pattern, scoped to instance ctor rather than static ctor (D-02).
- **One-shot diagnostics via `WarnOnce`** — Phase 23 D-13 advisory + Phase 32 D-08 unmapped-key advisory share the same UX pattern.
- **Push-once-never-pop for file-scope pragmas; push/pop for blocks** — `enable justIntonation;` pushes the JI tuning at file scope and never pops; `tuning t { ... }` push/pop balances within block scope (D-12).
- **CC-attribution co-located with fixtures** — Phase 29 sample bundle established this; Phase 32 mirrors with `flow-lang.Tests/fixtures/scala/LICENSE.md`.

### Integration Points

- **`RenderTuning` extension** — single field add (`ResolvedTuning? Custom`); default value remains the byte-identical 12-TET short-circuit trigger
- **`MusicalContext` refactor** — replace `Tuning` nullable scalar with `Stack<RenderTuning>` (D-12); update all readers + writers
- **Parser dispatch table** — add `tuning` keyword case in `Parser.cs:104–143` parallel block (D-13)
- **Pragma handlers** — Phase 23's `justIntonation` / `pythagorean` / `equalTemperament` / `equalTemperament` handlers switch from setting the scalar to pushing on the stack (D-12)
- **`TypeSystem/SpecialTypes/`** — new `TuningType.cs` (15th special type)
- **Test fixtures directory** — new `flow-lang.Tests/fixtures/scala/` with 5 canonical + 3 negative + LICENSE.md
- **`InternalFunctionRegistry`** — register `(loadScala "path")` + `(loadScala "scl" "kbm")` builtins
- **Diagnostics** — D-08 advisory wires into `RenderingDiagnostics.WarnOnce`
- **CLAUDE.md** — planner may extend the Music Types Quick Reference table with a `Tuning` row

</code_context>

<specifics>
## Specific Ideas

- **Composer-facing example:** `enable justIntonation; tempo 120 { tuning partch { section a { ... } } section b { ... } }` — section `a` renders under Partch tuning; section `b` renders under JI (the pragma baseline). This shape is the acceptance fixture for SPEC Requirement 6's last-wins test (spectral envelope comparison).
- **Three composer surfaces for the block form** (D-15): variable reference, inline call, and string-literal sugar. Each routes through the same `TuningContextStatement.TuningExpr` evaluator — single code path, three surface affordances. The string-literal form is the "I just want to play with carlos_alpha right now" ergonomic shortcut.
- **Non-octave-repeating verification target:** `carlos_alpha.scl` is the headline non-octave fixture. Acceptance is ±0.1 cents on ascending sequence frequencies vs Huygens-Fokker reference values (SPEC Requirement 5). Period-per-step semantics (KBM auto-adopting the tuning's period, D-07) is what makes this work without a custom .kbm.
- **REPL ergonomics matter** (D-14): the composer should be able to iterate on tunings interactively. Pragmas stay sticky (write once, hear differences across many evals); blocks stay ephemeral (no accidental "I forgot a `}` and now every render is in Partch").

</specifics>

<deferred>
## Deferred Ideas

The following came up during discussion or are listed in the SPEC out-of-scope section — captured here so future phases / a v1.5 backlog pass don't lose them:

- **Live-edit reload of .scl files** via FileSystemWatcher (parallel to Phase 14 beat-synced live reload) — deferred to a future "live tuning" phase.
- **Caching parsed .scl across `(loadScala ...)` calls** — Phase 32 re-parses each call; cache when profiling shows a hot loop. (SPEC out-of-scope.)
- **In-source tuning literal** — `(tuningFromCents [100.0, 200.0, 300.0, ...])` builtin for composers who don't want to author a `.scl` file. v1.5+.
- **Tuning interpolation / morphing** between two `Tuning` values. v1.5+.
- **Per-instrument tuning override** inside a section. v1.5+.
- **Octave stretching parameters** independent of period. v1.5+.
- **MTS (MIDI Tuning Standard) per-channel pitch-bend MIDI export** — Phase 23 D-13 advisory continues; MTS-MIDI is its own phase.
- **Scala SoundFont (.sf2) loader** — different format; deferred to v1.5+ (likely paired with Phase 33 SFZ work).
- **GUI tuning picker** — out of scope for Flow as CLI-first / source-first.
- **`Tuning` structural equality builtin** — Claude's Discretion landed on reference identity for v1.4; structural compare (does this `partch` equal that `partch`?) is a v1.5 nice-to-have.
- **LSP completions / hovers for `(loadScala ...)`** — Phase 31 (LSP enhancements) owns LSP work via the import-filter pattern; Phase 32 just ships the builtin and trusts Phase 31's existing surface. If LSP misses it, file a Phase 31 follow-up.

</deferred>

---

*Phase: 32-full-scala-scl-tuning-loader*
*Context gathered: 2026-05-13*
