# Phase 32: Full Scala (`.scl`) Tuning Loader - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-13
**Phase:** 32-full-scala-scl-tuning-loader
**Areas discussed:** Tuning internal repr, KBM defaults, Ratio vs cents norm, Tuning context stacking

---

## Tuning internal representation

### Q1 — Step storage in `ResolvedTuning`

| Option | Description | Selected |
|--------|-------------|----------|
| Tagged union per step | `Either<double cents, (int, int) ratio>`; preserves JI exact-ratio precision | |
| Eager-normalize to cents | `double[] StepCents`; ratios convert at parse time via `1200 * log2(n/d)` | ✓ |
| Always store as ratio | Force-fit cents to nearest rational | |

**User's choice:** Eager-normalize to cents
**Notes:** Single uniform representation. JI fans lose round-trip precision but the math downstream of the parser is uniform.

### Q2 — Pre-computed frequency table

| Option | Description | Selected |
|--------|-------------|----------|
| Pre-compute 128-entry table | `double[128] MidiToHz` at load time; O(1) render-time lookup | ✓ |
| Compute on demand | Math runs per NoteToFrequency call | |
| Lazy-memoized | Empty array, populate on first access | |

**User's choice:** Pre-compute 128-entry table
**Notes:** ~1 KB per Tuning; mirrors Phase 23 `ChromaticRatioTable` static-table pattern.

### Q3 — PitchConversion integration

| Option | Description | Selected |
|--------|-------------|----------|
| Extend `RenderTuning.Custom` field | Optional `ResolvedTuning?` on existing record struct; null → 12-TET short-circuit | ✓ |
| New `NoteToFrequency(note, ResolvedTuning)` overload | Parallel API; doubles threading work | |
| Replace RenderTuning with discriminated union | Most uniform; biggest blast radius | |

**User's choice:** Extend `RenderTuning` with `ResolvedTuning? Custom`
**Notes:** Preserves Phase 23 Pattern A (single render-time entry point); 13 synthesizer call sites untouched; byte-identical 12-TET short-circuit still fires for default RenderTuning.

### Q4 — Description string handling

| Option | Description | Selected |
|--------|-------------|----------|
| Capture + expose in `(str t)` | Field stores .scl line 1 verbatim; `(str t)` renders it | ✓ |
| Capture but don't display | Field exists but `(str t)` shows only structural data | |
| Skip entirely | Discard at parse time | |

**User's choice:** Capture + expose in `(str t)`
**Notes:** Useful for composer debugging + (print) round-trip. ~50 bytes per Tuning.

---

## KBM defaults

### Q1 — Default mapping factory location

| Option | Description | Selected |
|--------|-------------|----------|
| Synthetic Kbm via `ScalaKbmParser.Default()` | Internal model always has KBM; no nullable field | ✓ |
| Nullable Kbm field on Tuning | PitchConversion branches on null | |
| Hardcoded fallback inside PitchConversion | Scatters the default across two files | |

**User's choice:** Synthetic Kbm via `ScalaKbmParser.Default()`
**Notes:** Cleaner invariants; no branching in hot path.

### Q2 — Tonic resolution under active `key` block

| Option | Description | Selected |
|--------|-------------|----------|
| KBM wins — tonic stays at MIDI 60 | Tuning math is independent of musical key | ✓ |
| key block wins — tonic shifts to D | Override KBM tonic with active key letter | |
| Compose — KBM defines structure, key shifts anchor | Most flexible, most complex | |

**User's choice:** KBM wins — tonic stays at MIDI 60
**Notes:** `key` block remains a Phase 23 scale-degree concern; mirrors how DAW tuning plugins work. Avoids the "D in Bohlen-Pierce" semantic surprise.

### Q3 — Default KBM period when Tuning is non-octave

| Option | Description | Selected |
|--------|-------------|----------|
| Default KBM auto-adopts the Tuning's period | `ScalaKbmParser.Default(tuning)` takes tuning param | ✓ |
| Raise error for non-octave without explicit .kbm | Strict but blocks 1-arg loadScala for Carlos Alpha | |
| KBM period wins — stretch/squash Tuning steps | Math nonsensical | |

**User's choice:** Default KBM auto-adopts the Tuning's period
**Notes:** Dissolves the mismatch class entirely; preserves 1-arg ergonomics for non-octave scales.

### Q4 — Unmapped MIDI key behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Silent + one-shot stderr advisory | Render zeros + WarnOnce diagnostic | ✓ |
| Hard error at render time | Stricter; breaks renders that worked yesterday | |
| Fall back to 12-TET | Audible weird transitions between tuned + 12-TET notes | |

**User's choice:** Silent + one-shot stderr advisory
**Notes:** Honors Scala spec intent for `x` keymap entries. Reuses Phase 23 `RenderingDiagnostics.WarnOnce` pattern.

---

## Ratio vs cents normalization

### Q1 — Negative cents handling

| Option | Description | Selected |
|--------|-------------|----------|
| Accept verbatim — store as negative double | `2^(stepCents/1200)` math handles naturally | ✓ |
| Accept but clamp to ascending | Violates SPEC constraint | |
| Reject as malformed | Blocks legitimate archive files | |

**User's choice:** Accept verbatim
**Notes:** Required for Carlos Alpha and similar descending-step scales.

### Q2 — Period field location

| Option | Description | Selected |
|--------|-------------|----------|
| Extract to dedicated PeriodCents field | StepCents[] carries only N-1 intra-period steps | ✓ |
| Keep period at `StepCents[^1]` | Match file format literally | |
| Both — duplicate | Sync invariant to maintain | |

**User's choice:** Extract to dedicated `PeriodCents` field
**Notes:** Cleaner render-time math; `StepCents.Length` matches "pitches per period" without off-by-one.

### Q3 — Original ratio form preservation

| Option | Description | Selected |
|--------|-------------|----------|
| Don't preserve — cents only | Single source of truth; show cents in (str t) | |
| Preserve all as `string[] OriginalForm` | Parallel array doubles memory | |
| Preserve only for ratios — `Dictionary<int, (int, int)>` | Hybrid; cents inputs unaffected, ratio inputs retain exact form | ✓ |

**User's choice:** Preserve only for ratios
**Notes:** Useful for JI fans in `(str t)` and error messages; small overhead.

---

## Tuning context stacking

### Q1 — Stack architecture

| Option | Description | Selected |
|--------|-------------|----------|
| Convert MusicalContext.Tuning to push/pop stack | Pragma pushes once; blocks push/pop | ✓ |
| Keep field + add parallel TuningStack | Two parallel mechanisms | |
| Deprecate field; route pragmas through stack | Most uniform; larger blast radius | |

**User's choice:** Convert field to push/pop stack
**Notes:** Phase 23 D-05's non-stacked rationale is superseded. Last-wins falls out from stack semantics naturally.

### Q2 — AST node design

| Option | Description | Selected |
|--------|-------------|----------|
| New `TuningContextStatement` parallel to MusicalContextStatement | Narrow blast radius; separate dispatch | ✓ |
| Extend MusicalContextStatement with Tuning kind | Consolidated dispatch | |
| Generic ContextStatement<T> with TypeArg | Most uniform; biggest refactor | |

**User's choice:** New `TuningContextStatement` parallel node
**Notes:** Existing MusicalContextStatement variants carry scalar primitives; `tuning` carries a Tuning-typed expression. Parallel nodes keep value-shape and dispatch clean.

### Q3 — REPL state for `tuning` blocks

| Option | Description | Selected |
|--------|-------------|----------|
| Blocks always close at end of eval | Force-close stack to pre-eval state | ✓ |
| Blocks stay open until explicitly closed across evals | More flexible; invariant-breaking | |
| Block becomes sticky like pragma on missing `}` | Surprise behavior | |

**User's choice:** Blocks always close at end of eval
**Notes:** Pragmas remain sticky per Phase 23 D-08; blocks remain ephemeral.

### Q4 — `tuning <expr>` form

| Option | Description | Selected |
|--------|-------------|----------|
| Any Tuning-typed expression | Identifier, inline call, etc. all work | |
| Variable reference only | Force composers to name tunings | |
| Variable OR string-literal shortcut | Identifier + inline call + string sugar (desugar to loadScala) | ✓ |

**User's choice:** Variable OR string-literal shortcut (and any Tuning-typed expression)
**Notes:** Three forms coexist:
- `tuning partch { ... }` (identifier)
- `tuning (loadScala "x.scl") { ... }` (inline call)
- `tuning "x.scl" { ... }` (string-literal sugar, desugars at parse time)

All route through `TuningContextStatement.TuningExpr` — single code path, three surface affordances.

---

## Claude's Discretion

Areas not directly asked but inferred from the SPEC + the decisions above:

- **Error class hierarchy:** `ScalaParseException` (and `ScalaKbmParseException`) extend the existing `ParseException` at `flow-lang/Parsing/TypeParser.cs:335`. Reuses Flow's `{file}:{line}:{col} — expected X got 'Y'` format.
- **Fixture sourcing:** 5 canonical archive files pulled byte-identical from huygens-fokker.org/scala; 3 negative-case fixtures hand-authored. Single co-located `LICENSE.md` cites the archive's public-domain release.
- **Tuning value mutability:** `sealed class ResolvedTuning` with readonly fields; `Tuning` Flow value wraps a reference; equality follows reference identity (no caching per SPEC out-of-scope).
- **PitchConversion entry-point:** extend `flow-lang/StandardLibrary/Audio/PitchConversion.cs` in-place; branch on `RenderTuning.Custom != null` at the top of `NoteToFrequency` — remains the SOLE tuning-math entry point.
- **Block nesting interaction with Phase 28 `voicePool`:** independent musical-context blocks; nest in either order; no interaction.

## Deferred Ideas

- Live-edit reload of `.scl` files (FileSystemWatcher)
- Caching parsed `.scl` across multiple `(loadScala ...)` calls in the same FlowEngine instance
- `(tuningFromCents [...])` in-source tuning literal
- Tuning interpolation / morphing between values
- Per-instrument tuning override inside a section
- Octave stretching parameters
- MTS (MIDI Tuning Standard) per-channel pitch-bend MIDI export
- Scala SoundFont (.sf2) loader
- GUI tuning picker
- `Tuning` structural equality builtin (currently reference identity)
- LSP completions / hovers for `(loadScala ...)` — owned by Phase 31's import-filter pattern; file a follow-up if it misses
