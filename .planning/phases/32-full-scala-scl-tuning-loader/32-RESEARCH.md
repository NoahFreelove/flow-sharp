# Phase 32: Full Scala (`.scl`) Tuning Loader — Research

**Researched:** 2026-05-13
**Domain:** .scl/.kbm format parsing + tuning-aware render integration on top of Phase 23 wedge
**Confidence:** HIGH on codebase integration; MEDIUM on .scl edge cases (some not explicitly nailed by the Huygens-Fokker spec); LOW on the precise filename for the 12-tone 5-limit JI fixture (one of the 5 named fixtures has an unverified archive path)

## Summary

Phase 32 layers a full Scala `.scl` / `.kbm` loader on top of the Phase 23 named-tunings wedge. The SPEC + 32-CONTEXT.md lock the design tightly enough that this phase reduces to four implementation blocks: (1) the parsers (.scl + .kbm) producing a new `ResolvedTuning` value, (2) the Flow-side `Tuning` type + `(loadScala ...)` builtin, (3) the `tuning <expr> { ... }` musical-context block (AST + parser + interpreter dispatch), and (4) the rendering integration via a new `RenderTuning.Custom` field that takes the byte-identical 12-TET short-circuit when null. The hard work is the format edge cases (the Huygens-Fokker spec is informal — several common forms are not explicitly nailed, e.g. ratios with spaces around the slash) and re-wiring how Phase 23 pragmas push state, because the current implementation does NOT use a `Stack<RenderTuning>` — pragmas write `MusicalContext.Tuning` (a scalar) on the `GlobalFrame` exactly once. CONTEXT D-12's "Stack<RenderTuning>" is therefore a STRUCTURAL CHANGE, not an interface preservation; this is the single highest-blast-radius task in the plan.

**Primary recommendation:** Split the work into ~7 plans: (1) Wave 0 — test infrastructure + fixture downloads + LICENSE.md; (2) `ResolvedTuning` value object + `RenderTuning.Custom` field; (3) `ScalaParser` (.scl) + tests; (4) `ScalaKbmParser` (.kbm) + `Default(ResolvedTuning)` factory + tests; (5) `TuningType` + `(loadScala)` builtin + InternalFunctionRegistry wiring; (6) `TuningContextStatement` AST + parser + interpreter + stack refactor of `MusicalContext.Tuning`; (7) Acceptance + last-wins pragma interaction + Phase 23 regression sweep + tutorial chapter. Wave 0 is strictly fixture sourcing + LICENSE attribution and unblocks every other wave.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| `.scl` text parsing | Parser (`StandardLibrary/Audio/Tuning/ScalaParser.cs`) | — | Pure text → value-object transformation; lives alongside `ChromaticRatioTable` |
| `.kbm` text parsing | Parser (`StandardLibrary/Audio/Tuning/ScalaKbmParser.cs`) | — | Same shape as ScalaParser; both produce parts of `ResolvedTuning` |
| `ResolvedTuning` value object | Tuning value layer (`StandardLibrary/Audio/Tuning/ResolvedTuning.cs`) | — | Sealed class; carries StepCents[] + PeriodCents + Description + Ratios{} + MidiToHz[128] + Kbm |
| Flow-side `Tuning` type | Type system (`TypeSystem/SpecialTypes/TuningType.cs`) | Wraps `ResolvedTuning` | 15th SpecialType; reference equality (per D's discretion) |
| `(loadScala "path")` + `(loadScala "scl" "kbm")` builtins | StandardLibrary registration (`BuiltInFunctions.cs` or new file under `StandardLibrary/Audio/Tuning/`) | InternalFunctionRegistry | Two-overload pair; reads file via System.IO.File.ReadAllText |
| `tuning <expr> { ... }` AST | AST (`Ast/Statements/TuningContextStatement.cs`) | Parser dispatch in `Parsing/Parser.cs` | Parallel node to `MusicalContextStatement` per D-13 |
| `tuning` keyword tokenization | Lexer (`Lexing/TokenType.cs` + `Lexing/SimpleLexer.cs:855-887`) | — | Add `Tuning` to TokenType enum + keyword table |
| `tuning` block interpretation | Interpreter (`Interpreter/Interpreter.cs:135` dispatch) | ExecutionContext | New case alongside MusicalContextStatement |
| Tuning state on render | `MusicalContext` + `ExecutionContext` | `SongRenderer.ResolveRenderTuning` | D-12 stack refactor; readers consume via `GetMusicalContext()` |
| Pitch math | `PitchConversion.NoteToFrequency(MusicalNoteData, RenderTuning)` | — | Single entry point; add branch on `RenderTuning.Custom != null` |
| Diagnostics (unmapped MIDI key) | `Diagnostics/RenderingDiagnostics.WarnOnce` | — | Reuse Phase 23 pattern (D-08) |

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SPEC-1 | `(loadScala "path")` builtin returns first-class `Tuning` value | New `TuningType`/`ResolvedTuning` (Codebase Integration §Type System); builtin registration mirrors Phase 29 sample loaders via `InternalFunctionRegistry.Register` |
| SPEC-2 | `tuning t { section ... }` musical-context block applies a Tuning | `TuningContextStatement` AST + Parser dispatch + Interpreter case (Codebase Integration §Parser/Interpreter) |
| SPEC-3 | Core .scl parser: cents, ratios, comments, descriptions | Domain Context §.scl Format Reference; existing `RatioMath` reused for cents conversion (D-01) |
| SPEC-4 | `.kbm` keyboard mapping support | Domain Context §.kbm Format Reference; ScalaKbmParser.Default(ResolvedTuning) factory per D-05/D-07 |
| SPEC-5 | Non-octave-repeating scale support (Bohlen-Pierce, Carlos Alpha) | `PeriodCents` field (D-10); reference values from Fixture Sourcing §Reference Hz; carlos_alpha.scl confirmed non-octave (period = 1404¢) |
| SPEC-6 | Last-wins pragma + canonical archive fixture battery (5 in-repo) | Codebase Integration §Pragma Bridge; Fixture Sourcing §Sources |
| SPEC-7 | Clear error semantics for malformed input | New `ScalaParseException` extending `ParseException` at `Parsing/TypeParser.cs:335`; format `{file}:{line}:{col} — expected X got 'Y'` |

## User Constraints (from CONTEXT.md)

### Locked Decisions

**Tuning value internal representation:**
- D-01: `StepCents` = `double[]`; ratios convert at parse time via `1200.0 * Math.Log2((double)n/d)`. Single uniform representation downstream of the parser.
- D-02: Pre-compute 128-entry `MidiToHz` table at load time. `ResolvedTuning.MidiToHz` is `double[128]`. Render-time `NoteToFrequency` becomes an O(1) array lookup. ~1 KB per Tuning.
- D-03: Extend `RenderTuning` with `ResolvedTuning? Custom` field. When `Custom` is non-null, render reads `Custom.MidiToHz[note]`; when null, falls through to Phase 23 logic. All 13 synthesizer call sites stay untouched.
- D-04: Capture `Description` string (verbatim first non-comment line). `(str t)` renders `Tuning("<description>", N steps, period X:Y)`.

**KBM defaults (when no `.kbm` is loaded):**
- D-05: `ScalaKbmParser.Default(ResolvedTuning t)` static factory. Internal model is ALWAYS "has KBM"; no nullable Kbm field.
- D-06: KBM wins for tonic placement; `key` block stays orthogonal. Tuning math reads tonic from KBM (default = MIDI 60, A4=440 Hz at MIDI 69, period-per-octave).
- D-07: Default KBM auto-adopts the Tuning's period (carlos_alpha → period ≈ 1404¢ ≈ 5/2, NOT 1200¢).
- D-08: Unmapped MIDI keys render as silence + one-shot stderr advisory `[tuning] note X unmapped under '<description>' — rendered as rest` via `RenderingDiagnostics.WarnOnce`.

**Ratio vs cents normalization:**
- D-09: Negative cents accepted verbatim; `2^(stepCents/1200)` math naturally produces ratio < 1 (descending pitch).
- D-10: Period extracted to dedicated field. `ResolvedTuning.PeriodCents` is a separate `double`; `StepCents[]` carries only N-1 intra-period steps. Length(StepCents) == "pitches per period minus the period itself".
- D-11: Preserve original ratio form for ratio inputs only. `ResolvedTuning.Ratios` = `Dictionary<int, (int Num, int Den)>` keyed by step index; ratio inputs land here, cents inputs don't.

**Tuning context stacking:**
- D-12: Replace `MusicalContext.Tuning` (nullable scalar) with `Stack<RenderTuning> TuningStack`. **Phase 23 D-05's non-stacked rationale is explicitly superseded.**
- D-13: New `TuningContextStatement` AST node parallel to `MusicalContextStatement` (NOT a 6th `MusicalContextType` enum variant — value-shape diverges, see §Pitfalls).
- D-14: Blocks force-close at REPL eval boundary. Pragmas remain sticky across REPL evals (per Phase 23 D-08 extension); blocks remain ephemeral.
- D-15: `tuning <expr> { ... }` accepts three forms — identifier, inline function call, string-literal sugar (which desugars to `(loadScala "...")` at parse time).

### Claude's Discretion (from CONTEXT.md)

- **Error class hierarchy:** `ScalaParseException` extends `ParseException` at `Parsing/TypeParser.cs:335`. `ScalaKbmParseException` likewise. Reuses Flow's `{file}:{line}:{col} — expected X got 'Y'` format.
- **Fixture sourcing:** 5 canonical archive files committed under `flow-lang.Tests/fixtures/scala/` with co-located `LICENSE.md`. 3 negative-case fixtures hand-authored.
- **Tuning value mutability:** `sealed class ResolvedTuning` with readonly fields. Flow-facing `Tuning` value uses reference identity by default.
- **PitchConversion entry-point pattern:** Extend `PitchConversion.cs` in-place; single conditional at top of function checking `RenderTuning.Custom != null`. Remains the SOLE entry point.
- **Tuning block + voicePool interaction:** Independent musical-context blocks; nest in either order with no interaction.

### Deferred Ideas (OUT OF SCOPE)

- Live-edit reload of .scl files via FileSystemWatcher
- `.sf2` (SoundFont) loader
- MTS (MIDI Tuning Standard) per-channel pitch-bend MIDI export
- Caching parsed .scl content across `(loadScala ...)` calls
- In-source tuning literal `(tuningFromCents [...])`
- Tuning interpolation / morphing
- Per-instrument tuning override within a section
- GUI tuning picker
- Octave stretching parameters
- Multi-period scales

## Project Constraints (from CLAUDE.md)

- **.NET 10 target only.** No new NuGet packages permitted for this phase. Hand-roll the `.scl` + `.kbm` parsers. `[VERIFIED: CLAUDE.md "Constraints" section]`
- **File-scoped namespaces.** All new files under `FlowLang.*` for the library or `FlowInterpreter` for the console app. `[VERIFIED]`
- **AST nodes are `record` types** (immutable). `TuningContextStatement` must be a `record`. `[VERIFIED]`
- **No infix arithmetic.** Prefix-only via `(add)`/`(sub)`/`(mul)`/`(div)` builtins. Parser produces `FunctionCallExpression` not `BinaryExpression`. `[VERIFIED]`
- **Pattern matching (`switch` expressions) for node dispatch** rather than visitor pattern. New `TuningContextStatement` adds a switch case in `Interpreter.cs:97-131`. `[VERIFIED]`
- **GSD workflow enforcement.** No direct edits outside a GSD command. `[VERIFIED]`

## Domain Context

### .scl Format Reference (Huygens-Fokker spec, verified 2026-05-13)

The Huygens-Fokker spec is at `https://www.huygens-fokker.org/scala/scl_format.html`. The spec is informal — several common forms are not explicitly nailed. The following table captures verified rules + the rules that need a charitable interpretation:

| Rule | Verified status | Citation |
|------|----------------|----------|
| First non-comment line is description | `[CITED: scl_format.html]` "The first (non comment) line contains a short description of the scale" |
| Long lines tolerated in description | `[CITED]` "long lines are possible and should not give a read error" |
| Empty description encoded as empty line | `[CITED]` "If there is no description, there should be an empty line." |
| Second non-comment line is step count | `[CITED]` "The second line contains the number of notes" |
| Step count `0` is valid | `[CITED]` "The lower limit is 0, which is possible since degree 0 of 1/1 is implicit." |
| Step count is unsigned positive integer | `[CITED implicit]` "lower limit is 0"; signed forms not mentioned (`+12` and `-12` should be REJECTED) |
| Value with `.` is cents, else ratio | `[CITED]` "If the value contains a period, it is a cents value, otherwise a ratio" |
| Ratios have exactly one slash | `[CITED]` "Ratios are written with a slash, and only one." |
| Integers without period/slash = ratio with implicit `/1` | `[CITED]` "Integer values with no period or slash should be regarded as such, for example '2' should be taken as '2/1'." |
| Numerator/denominator support up to 2³¹−1 | `[CITED]` "should be supported to at least 2³¹−1 = 2147483647" |
| Negative ratios invalid | `[CITED]` "Negative ratios are meaningless and should give a read error." |
| Negative cents valid | `[CITED implicit]` "-5.0" appears in valid examples list |
| Text after a valid pitch value tolerated | `[CITED]` "Anything after a valid pitch value should be ignored. Space or horizontal tab characters are allowed and should be ignored." So `100.0 cents`, `100.0 C#`, `100.0 # comment` all parse to `100.0¢` |
| Lines starting with `!` are comments | `[CITED implicit]` "non comment lines"; `!` is the standard Scala comment marker per the spec preamble + all examples |
| Final step is the period | `[ASSUMED + corroborating evidence]` Spec says "first note of 1/1 or 0.0 cents is implicit"; archive examples ALWAYS terminate with `2/1` (octave-repeating) or a clearly-period-shaped value (carlos_alpha terminates at 1404¢ for the 9× perfect-fifth alpha period). NOT explicitly stated in spec but de-facto universal |
| Step count line counts the EXPLICIT step values (period INCLUDED) | `[VERIFIED via fixtures]` partch_43.scl declares "43" and has 43 step lines INCLUDING `2/1` as the last. slendro declares "5" with 5 lines ending in `2/1`. Scope: step count = number of post-implicit-1/1 entries in the file |
| Leading blank lines before description? | `[ASSUMED — charitable]` Spec does not address; recommendation: skip blank lines before the description (charitable parsing). |
| Leading whitespace on description line? | `[ASSUMED — charitable]` Spec does not explicitly address; recommendation: tolerate (consume into description text verbatim, trim trailing whitespace). |
| Spaces around `/` in ratios (e.g. `3 / 2`)? | `[ASSUMED]` Spec is silent. Examples consistently use `3/2` without spaces. Recommendation: REJECT spaces around `/` to keep the parser tight; surface a precise error if users write `3 / 2`. |
| Scientific notation in cents (e.g. `1.5e2`)? | `[ASSUMED]` Spec is silent. No real archive file uses scientific notation. Recommendation: REJECT (parser uses `double.TryParse` with `NumberStyles.Float & ~NumberStyles.AllowExponent`, or equivalent). |
| Comma-decimal cents (`100,5`)? | `[ASSUMED]` Spec is silent. Recommendation: REJECT (parse with `CultureInfo.InvariantCulture` only — period-decimal mandatory). |
| Description is exactly one line | `[CITED]` "The description is only one line." |

**Files that have negative cents in real use:** Per the spec wording, negative cents in real archive files are rare. `carlos_alpha.scl` does NOT use negative cents (all 18 values are 78, 156, …, 1404). The spec wording in the SPEC for "Carlos Alpha and similar" referring to negative cents is technically incorrect — Carlos Alpha is a non-octave scale with all-positive ascending steps. Still, D-09 mandates we accept negative cents verbatim because some other archive files (descending scales) do use them.

### .kbm Format Reference (Huygens-Fokker spec — assembled from forum + spec fragments)

The official spec page for `.kbm` is **not currently available** on the Huygens-Fokker site (404 on the canonical anchor). The most authoritative reconstruction is the modartt.com Pianoteq forum thread (`https://forum.modartt.com/viewtopic.php?id=5724`) which mirrors what the Scala application's documentation describes. The fields are:

| Position | Field | Type | Meaning |
|----------|-------|------|---------|
| 1 | Size of map | int | Number of mapping entries that follow (0 = linear mapping, scale degree N maps to MIDI middle+N) |
| 2 | First MIDI to retune | int (0..127) | Lowest MIDI key the mapping applies to |
| 3 | Last MIDI to retune | int (0..127) | Highest MIDI key the mapping applies to |
| 4 | Middle note | int (0..127) | MIDI note where the FIRST entry of the mapping lands; i.e. "degree 0" of the scale |
| 5 | Reference note | int (0..127) | MIDI note assigned the reference frequency (typically 69 for A4) |
| 6 | Reference frequency | float (Hz) | Frequency assigned to the reference note (typically 440.0) |
| 7 | Formal octave (period) | int | Scale degree to consider as the formal octave/period — typically 0 in canonical Scala usage to mean "use the .scl's last step (the period)"; some sources allow other values, but in practice it's the same as the .scl's step count or 0 |
| 8..end | Mapping entries | int or `x` | One per "Size of map" lines; `x` (literal letter x) means the MIDI key is unmapped (no sound per D-08) |

Comments use `!` in column 1 (same as `.scl`).

**Example canonical default `.kbm`** (from the modartt forum post):
```
! Size of map:
0
! First MIDI note number to retune:
0
! Last MIDI note number to retune:
127
! Middle note:
60
! Reference note:
69
! Reference frequency:
440.000000
! Formal octave:
0
! Mapping (empty when size=0).
```

A `size=0` map is a "linear mapping" — MIDI key N maps to scale degree `(N - middleNote) mod stepCount`, period-shifted by the formal octave each wrap. This is the `ScalaKbmParser.Default(ResolvedTuning)` factory's output (D-05/D-07).

**Unmapped encoding:** Literal lowercase `x` (no `X`, no `?`). Treated as silence per D-08. `[CITED: modartt forum]`

**Formal-octave field semantics:** The integer in field 7 is "Scale degree to consider as formal octave" — this is essentially the step index whose pitch defines the period of repetition (typically `0` meaning "use scale degree N = step count, i.e. the final step in the .scl which IS the period"). Setting it to a non-zero value would let a .kbm "shorten" the effective period to a sub-period of the .scl — an exotic use we do NOT need to support in Phase 32 (treat any non-zero value as `0` charitable, or reject with a clear error per `ScalaKbmParseException`).

**KBM ↔ non-octave .scl interaction:** Per D-07, the synthetic default KBM auto-adopts the tuning's period. If a user provides a real `.kbm` whose `formal octave` references a different period than the loaded `.scl`'s final-step period, the existing rule (D-06) is "KBM wins for tonic placement; tuning math always uses `PeriodCents` from the .scl". The KBM's formal-octave field thus only re-anchors the wrap-point on the MIDI keyboard — the .scl's period remains the pitch period. This is consistent with how Carlos Alpha + a default-octave KBM produce a non-octave keyboard.

`[CITED: modartt.com forum (https://forum.modartt.com/viewtopic.php?id=5724) + Sevish blog (https://sevish.com/2017/mapping-microtonal-scales-keyboard-scala/) — corroborating sources]`

## Codebase Integration Map

Every file the planner needs to touch, with line ranges and call-site counts.

### Tuning value layer (new + extend)

- **NEW** `flow-lang/StandardLibrary/Audio/Tuning/ResolvedTuning.cs` — `sealed class` carrying:
  - `string Description` (D-04)
  - `double[] StepCents` (D-01, length N-1 intra-period — period NOT included per D-10)
  - `double PeriodCents` (D-10, dedicated field)
  - `IReadOnlyDictionary<int, (int Num, int Den)> Ratios` (D-11, ratio inputs only)
  - `ScalaKbm Kbm` (D-05 — always present, default factory fills in when no .kbm loaded)
  - `double[] MidiToHz` (D-02, length 128, pre-computed at load time)
  - `ToString()` override producing the `Tuning("<desc>", N steps, period X:Y)` format (D-04)
- **NEW** `flow-lang/StandardLibrary/Audio/Tuning/ScalaKbm.cs` — `sealed class` holding the 7 fields above + the optional mapping entries. The internal model always has a KBM; `Default(ResolvedTuning)` produces the synthetic one (D-05/D-07).
- **EXTEND** `flow-lang/StandardLibrary/Audio/Tuning/RenderTuning.cs:11-18` (currently a 4-field `readonly record struct`). Add `ResolvedTuning? Custom` 5th positional parameter; default factory `RenderTuning.Default` continues to produce `Custom = null` (preserves byte-identical 12-TET short-circuit per Phase 23 Pitfall 6).
  - **Blast radius for the `record struct` change:** anywhere a `new RenderTuning(...)` constructor is invoked. Grep result:
    - `flow-lang/StandardLibrary/Audio/SongRenderer.cs:184` — `new RenderTuning(ctx.Tuning.Value, mode, tonicLetter, tonicAlteration)` — needs to pass `null` for the new positional param OR call the renamed constructor.
    - `flow-lang/StandardLibrary/Audio/Tuning/RenderTuning.cs:17` — `RenderTuning.Default` factory.
    - Test files: ≥4 sites in `flow-lang.Tests/Unit/Phase23/*.cs` (e.g. `PitchConversionTuningFacts.cs:28`).
  - **Recommendation:** Make `Custom` an optional positional parameter (records support this), so the existing 4-arg construction continues to compile.

### Pitch math (in-place extension)

- **EXTEND** `flow-lang/StandardLibrary/Audio/PitchConversion.cs:57-93` (the 2-arg `NoteToFrequency(MusicalNoteData, RenderTuning)` overload). Add a NEW branch at the top of the function body:
  ```csharp
  if (tuning.Custom is not null)
  {
      int midi = GetMidiNote(note.NoteName, note.Octave, note.Alteration);
      // Bounds-clamp: GetMidiNote returns a signed int; MIDI domain is 0..127.
      if (midi < 0 || midi > 127) return 0.0;  // out of MIDI range = silence
      double hz = tuning.Custom.MidiToHz[midi];
      // D-08 unmapped key signal: ResolvedTuning records 0.0 for unmapped slots
      // AND the loader fires the WarnOnce advisory at .scl/.kbm load time, so the
      // render path here is just a numeric lookup. Silence drops out naturally.
      if (note.CentOffset.HasValue && note.CentOffset.Value != 0.0 && hz > 0.0)
          hz *= RatioMath.CentOffsetMultiplier(note.CentOffset.Value);
      return hz;
  }
  // ... existing Phase 23 logic (EqualTemperament short-circuit + JI/Pyth fallback)
  ```
  This preserves Pattern A — `NoteToFrequency` remains the SOLE entry point.

### Runtime / interpreter integration

- **REFACTOR** `flow-lang/Runtime/MusicalContext.cs:62-69` — replace the scalar `TuningSystem? Tuning { get; set; }` field with `Stack<RenderTuning> TuningStack { get; }` per D-12. Update `ToString()` (line 137) and `Clone()` (line 79).
- **REFACTOR** `flow-lang/Runtime/ExecutionContext.cs:195-238` (`GetMusicalContext()`). The current implementation reads `frame.MusicalContext.Tuning ??= ...` (line 213). After D-12, `Tuning` (scalar) is gone. The resolution becomes: walk the call stack top-to-bottom; if any frame's MusicalContext has a non-empty `TuningStack`, return its `.Peek()`; else return `RenderTuning.Default`. Note: this changes the resolution shape — `GetMusicalContext()` returns a `MusicalContext`, but the TUNING field within that returned object now needs to expose `RenderTuning` (or a derived value) instead of `TuningSystem?`. Pick one of:
  - **Option A:** Replace `MusicalContext.Tuning` with `RenderTuning ActiveTuning { get; set; }` (single resolved value) and have the resolution walk return the top-of-stack value.
  - **Option B:** Add a new `ActiveTuning` getter on `MusicalContext` alongside keeping the field private; readers consume `ctx.ActiveTuning`. (Recommended — narrower blast radius on Phase 23 readers.)
- **REFACTOR** `flow-lang/Runtime/ExecutionContext.cs:280-293` (`SetTuning`). Phase 23 `SetTuning(TuningSystem?)` writes the scalar on `GlobalFrame.MusicalContext.Tuning`. Phase 32 changes the signature to `SetTuning(RenderTuning)` (or `PushTuning`/`PopTuning`) and writes onto the new `TuningStack` on the global frame.
- **REFACTOR** `flow-lang/Core/FlowEngine.cs:148-157` (`ApplyTuningPragma`). Currently maps `program.Pragmas.Has("justIntonation")` → `_context.SetTuning(TuningSystem.JustIntonation)`. After D-12, this becomes a `Push` onto `TuningStack` of the resolved `RenderTuning` (which needs a tonic + mode — same logic as `SongRenderer.ResolveRenderTuning` currently does at section-render time). The push happens once before interpretation and is never popped (D-12 file-scope pragma semantics).
  - **CRITICAL — REPL sticky behavior (D-14 contract):** Phase 23 D-07/D-08 say pragma absence does NOT reset previous REPL tuning. The current implementation honors this via the `if (tuning is null) return;` no-op at line 289. The Phase 32 push-based implementation must preserve this: if the new program has NO tuning pragma, do nothing — leave the existing pragma push on the stack. If the new program HAS a tuning pragma, REPLACE the file-scope tuning. Since blocks force-close at REPL eval boundary (D-14), the only thing surviving across evals is the bottom-of-stack pragma frame.
  - **Recommendation:** Maintain a single "file-scope tuning frame" at the bottom of the stack; pragma push replaces this single frame; blocks push above it and force-pop at eval boundary.

### Parser dispatch

- **EXTEND** `flow-lang/Parsing/Parser.cs:102-153` (the musical-context dispatch table). Today there are 11 entries (Timesig, Tempo, Swing, Key, Dynamics, Rit, Accel, Pan, Gain, ReverbTime, VoicePool). Add a 12th: `tuning <expr> { ... }`. Three differences from the existing dispatch:
  1. The new variant does NOT use `ParseMusicalContextStatement(MusicalContextType.X)`. Per D-13, it routes to `ParseTuningContextStatement()` which produces a `TuningContextStatement` AST node (NOT a `MusicalContextStatement`).
  2. The grammar after the `tuning` keyword is an EXPRESSION (identifier / function-call / string-literal), not a scalar literal. Per D-15: route to `ParseExpression()` and let the type checker/evaluator complain if the result isn't a `Tuning` value.
  3. String-literal sugar (D-15): if the next token after `tuning` is a `StringLiteral`, desugar at parse time to `(loadScala <stringLiteral>)`. This is the same parse-time transform pattern as the flow operator `->`.
- **EXTEND** `flow-lang/Lexing/TokenType.cs:7-36` — add `Tuning` to the enum (after `VoicePool`).
- **EXTEND** `flow-lang/Lexing/SimpleLexer.cs:855-887` — add `"tuning" => TokenType.Tuning` to the keyword table.
- **EXTEND** `flow-lang/Parsing/Parser.cs:247` — if `tuning` should be allowed as a procedure name (mirroring how `pan`/`gain`/`tempo` already are), add it to the `Check(TokenType.Pan) || Check(TokenType.Gain) || ...` allowlist. Per the SPEC "No back-compat constraint" line 139, this is OPTIONAL — pre-public lean accepts the break.

### AST

- **NEW** `flow-lang/Ast/Statements/TuningContextStatement.cs` — record parallel to `MusicalContextStatement.cs:14-20`:
  ```csharp
  public record TuningContextStatement(
      SourceLocation Location,
      Expression TuningExpr,
      IReadOnlyList<Statement> Body
  ) : Statement(Location);
  ```

### Interpreter dispatch

- **EXTEND** `flow-lang/Interpreter/Interpreter.cs:97-131` (the `ExecuteStatement` switch). Add a new case:
  ```csharp
  case TuningContextStatement tctx:
      ExecuteTuningContext(tctx);
      break;
  ```
- **NEW** `ExecuteTuningContext` method paralleling `ExecuteMusicalContext` (Interpreter.cs:135-323). Shape: evaluate `tctx.TuningExpr` → expect a `Tuning` value → push the underlying `RenderTuning` (constructed from `ResolvedTuning`) onto the TuningStack → push a stack frame → run body → pop frame → pop tuning. The `try/finally` shape mirrors `ExecuteMusicalContext:137-322`.

### Type system

- **NEW** `flow-lang/TypeSystem/SpecialTypes/TuningType.cs` — sealed class extending `FlowType`, singleton `.Instance`, `Name = "Tuning"`, GetSpecificity returns a unique value (look at `SequenceType.GetSpecificity() == 134` and `SongType.GetSpecificity() == 140`; pick something between, e.g. 137). Pattern reference: `SongType.cs`. Runtime data class `TuningData` (or use `ResolvedTuning` directly) wrapping the `ResolvedTuning` reference.

### Builtin registration

- **NEW** `flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs` (or extend `BuiltInFunctions.cs`). Register two overloads of `loadScala`:
  - `loadScala(String) → Tuning`
  - `loadScala(String, String) → Tuning` (the 2nd arg is the .kbm path)
- Wired via `InternalFunctionRegistry.Register(signature, args => { ... })` pattern (see `BuiltInFunctions.cs` for examples). File I/O uses `System.IO.File.ReadAllText`; parsers handle the rest.

### Diagnostics

- **REUSE** `flow-lang/Diagnostics/RenderingDiagnostics.cs:29-36` `WarnOnce(sentinelKey, message)`. Two new sentinel keys:
  - D-08 unmapped MIDI key: sentinel key per `(description, midiNote)` pair so the same description doesn't spam each unmapped note — actually per D-08 read, ONE sentinel per `(description)` suffices since the message says "note X unmapped under '<description>'". Recommendation: one fire per .scl description per process.
  - Phase 23 D-13 advisory continues unchanged. Phase 32 must verify it still fires under custom tunings (the `RenderTuning.Custom != null` case must trigger `[midi]` warning the same as `TuningSystem != EqualTemperament` did).

### Pragma bridge — Phase 23 → Phase 32 transition

- `flow-lang/Lexing/PragmaRegistry.cs:16-24` — KnownPragmas dictionary stays unchanged. Pragma names + descriptions don't move.
- `flow-lang/Lexing/PragmaScanner.cs:35-77` — `LooksLikeTuningName` substring whitelist (`tun`, `scal`, …) was the Phase 23 MICR-03 deferral pointer. Now that the Scala loader SHIPS, the pointer text in the error message ("Full Scala (.scl) loader is documented as deferred to v1.4") needs UPDATING to point at the new builtin (`(loadScala "path")`). Single string update — low risk.
- `flow-lang/Core/FlowEngine.cs:148-157` — see above; switches from `SetTuning(TuningSystem)` to a push onto `TuningStack`.

### Readers of MusicalContext.Tuning (the blast radius for D-12 refactor)

All sites that today read `MusicalContext.Tuning` and need updating per D-12:

| File:Line | Purpose | Action |
|-----------|---------|--------|
| `flow-lang/Runtime/MusicalContext.cs:62-69,89,137` | Field declaration + Clone + ToString | Refactor field; update Clone + ToString |
| `flow-lang/Runtime/ExecutionContext.cs:213,223,287-293` | Inheritance resolution + SetTuning | Refactor to stack |
| `flow-lang/Core/FlowEngine.cs:148-157` | Pragma bridge | Refactor push semantics |
| `flow-lang/StandardLibrary/Audio/SongRenderer.cs:162-185` | `ResolveRenderTuning` | Refactor to read top-of-stack |
| `flow-lang/StandardLibrary/Audio/MidiExport.cs:164,199-204` | D-13 advisory | Verify still fires under `RenderTuning.Custom != null` |
| `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:49,56,60` | Enharmonic warning + tuning-aware lookup | Refactor to read top-of-stack |
| `flow-lang/StandardLibrary/Audio/Vocalization/VocalizationFunctions.cs:86` | Sing routing | Uses `SongRenderer.ResolveRenderTuning` — automatic |
| `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:264-265` | MICR-02 doc comment | No code change; comment update only |

**8 sites total; 5 require code changes.** Plus 5+ test files that construct or assert on `MusicalContext.Tuning` directly.

## Fixture Sourcing & Reference Values

### Source archive

The Huygens-Fokker Foundation maintains the canonical Scala archive at `https://www.huygens-fokker.org/docs/scales.zip` (version 94, March 2026, ~5350 files). A GitHub mirror is available at `https://github.com/narenratan/scala_scale_archive` (version 93, January 2025) which conveniently provides raw-URL access to individual `.scl` files at the path `https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/<filename>.scl`.

**License status:** `[ASSUMED]` The Huygens-Fokker downloads page (`https://www.huygens-fokker.org/scala/downloads.html`) does NOT include an explicit license statement for the archive. The SPEC + CONTEXT both treat the archive as "public-domain release per the archive maintainers," which is a community understanding (Manuel Op de Coul + John H. Chalmers are the named curators per the archive intro page), but is not explicitly stated by the Foundation. **Pitfall — surface to the user before shipping:** if a strict open-source licensing audit is run, the attribution wording may need to be softened from "public-domain release" to "released for free use per the archive maintainers" or to cite the long-standing community consensus. The fixture LICENSE.md should attribute Manuel Op de Coul (`coul@huygens-fokker.org`) and the Huygens-Fokker Foundation.

### Verified fixture URLs

| Fixture (SPEC name) | Archive filename | Raw URL | Period | Step count | Step type | Verified? |
|---------------------|------------------|---------|--------|------------|-----------|-----------|
| `partch_43.scl` | `partch_43.scl` | `https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/partch_43.scl` | 2/1 (octave) | 43 | ratio-only | `[VERIFIED 2026-05-13]` content fetched, final step IS `2/1` |
| `slendro.scl` | `slendro.scl` | `https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/slendro.scl` | 2/1 (octave) | 5 | cents + final `2/1` | `[VERIFIED 2026-05-13]` content fetched |
| `carlos_alpha.scl` | `carlos_alpha.scl` | `https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/carlos_alpha.scl` | ~1404¢ (non-octave) | 18 | cents-only | `[VERIFIED 2026-05-13]` content fetched, last step = 1404.00000¢, NOT 2/1 |
| `pythagorean_12.scl` | `pyth_12.scl` (NOTE: archive uses `pyth_12.scl`, not `pythagorean_12.scl`) | `https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/pyth_12.scl` | 2/1 (octave) | 12 | ratio-only | `[VERIFIED 2026-05-13]` content fetched |
| `just_5limit.scl` | `ji_12.scl` (canonical 12-tone JI; archive file labelled "Basic JI with 7-limit tritone") OR `ptolemy.scl` (7-tone strict 5-limit) | `https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/ji_12.scl` | 2/1 (octave) | 12 | ratio-only | `[VERIFIED 2026-05-13]` content fetched for `ji_12.scl`; a file literally named `just_5limit.scl` does NOT exist in the archive |

**Filename divergence flagged for the planner:**
- The SPEC names `pythagorean_12.scl`. The archive ships `pyth_12.scl`. Recommendation: commit the file IN-REPO under the SPEC-mandated name `pythagorean_12.scl` (rename at commit time; preserve content verbatim). Document the original archive filename inside the file's comment header so the audit trail is intact.
- The SPEC names `just_5limit.scl`. The archive does NOT contain that exact filename. Closest matches: `ji_12.scl` (Robert Rich's 12-tone JI with 7-limit tritone, 5-limit on the diatonic) OR `ptolemy.scl` (7-tone strict 5-limit). Recommendation: use `ji_12.scl` content (it IS the canonical Scala JI dodecaphonic fixture used in tutorials), commit under SPEC name `just_5limit.scl`, document the rename in the file's `!` comment header AND in `LICENSE.md`. SURFACE TO USER if they specifically want strict 5-limit (would need `ptolemy.scl` 7-tone instead, breaking the "all 12-tone-or-larger" implicit symmetry).

**Open question (file SURFACE TO USER):** Should fixture filenames in-repo match the SPEC (rename at commit) or the archive (preserve original)? The Open Questions section below carries this.

### Verified contents (use these as parser self-tests)

**partch_43.scl** (first 10 lines verbatim):
```
! PARTCH_43.scl
!
Harry Partch's 43-tone pure scale
 43
!
 81/80
 33/32
 21/20
 16/15
 12/11
```
(43 step lines total, ending `2/1`. Lines 1-2 are `!` comments. Line 3 is the description. Line 4 is the step count. Line 5 is a `!` comment. Lines 6-48 are step values. Note: step lines have a leading space — per spec "Space or horizontal tab characters are allowed and should be ignored.")

**carlos_alpha.scl** (verbatim):
```
! carlos_alpha.scl
!
Wendy Carlos' Alpha scale with perfect fifth divided in nine
 18
!
 78.00000
 156.00000
 234.00000
...
 1404.00000
```
Period = 1404.00000¢. All cents-only.

**slendro.scl** (verbatim):
```
! slendro.scl
!
Observed Javanese Slendro scale, Helmholtz/Ellis p. 518, nr.94
 5
!
 228.00000
 484.00000
 728.00000
 960.00000
 2/1
```
Mixed cents + final-ratio period. Period = `2/1`. Note: step count = 5 includes the `2/1` final-step period.

**pyth_12.scl** (verbatim):
```
! pyth_12.scl
!
12-tone Pythagorean scale
 12
!
 2187/2048
 9/8
 32/27
 81/64
 4/3
 729/512
 3/2
 6561/4096
 27/16
 16/9
 243/128
 2/1
```

**ji_12.scl** (verbatim):
```
! ji_12.scl
!
Basic JI with 7-limit tritone. Robert Rich: Geometry
 12
!
 16/15
 9/8
 6/5
 5/4
 4/3
 7/5
 3/2
 8/5
 5/3
 9/5
 15/8
 2/1
```

### Reference Hz values for the ±0.1 cents acceptance test

The SPEC requires Bohlen-Pierce / Carlos Alpha ascending sequence frequencies to match Huygens-Fokker reference values within ±0.1 cents. There are NO pre-computed reference Hz tables in the archive — the archive ships scale RATIOS/CENTS, and the implementer derives Hz at render time from the tonic anchor (A4 = 440 Hz at MIDI 69 per default KBM).

**Recommended derivation pattern for the test:**

For a tuning `t` with `StepCents[]` of length N-1, `PeriodCents = P`, default KBM (middleNote=60, refNote=69, refHz=440.0, period-per-octave-or-period):

```
tonicHz = 440.0 / 2^((69 - 60) / 12.0)  // 12-TET tonic placement before tuning takes over
                                         //   = 261.6256... Hz (A4 → C4 in 12-TET)
                                         // BUT: per D-06 the KBM tonic uses the reference frequency
                                         //   computed via the .scl pitch class system, not 12-TET fall-through.
                                         // Correct formula:
                                         //   refHzCanonical = 440.0 (refNote=69 in default KBM)
                                         //   middleHz = refHz × 2^(-(refNote - middleNote) × PeriodCents / 12 / 1200)
                                         //   simplified for octave-period: middleHz = 440 / 2^((69-60)/12) ≈ 261.6256 Hz
midi60Hz = middleHz  // by construction
midi(60+i)Hz = middleHz × 2^(StepCents[i-1] / 1200)  for i = 1..N-1
midi(60+N)Hz = middleHz × 2^(PeriodCents / 1200)     // first wrap into next period
```

**Worked example — carlos_alpha at MIDI 60..65:**
- middleHz ≈ 261.6256 Hz
- MIDI 60 = step 0 = middleHz × 2^0 = 261.6256 Hz
- MIDI 61 = step 1 = middleHz × 2^(78/1200) = 261.6256 × 1.04600 ≈ 273.665 Hz
- MIDI 62 = step 2 = middleHz × 2^(156/1200) ≈ 286.262 Hz
- MIDI 63 = step 3 = middleHz × 2^(234/1200) ≈ 299.439 Hz
- ...
- MIDI 78 = step 18 = middleHz × 2^(1404/1200) = middleHz × ~3.2003 ≈ 837.225 Hz (this is the period wrap, NOT 2× = 523.25 Hz like an octave)

**Worked example — partch_43 at MIDI 60..62:**
- middleHz ≈ 261.6256 Hz
- MIDI 60 = step 0 = 261.6256 Hz
- MIDI 61 = step 1 (81/80 = 1.0125) = 264.696 Hz
- MIDI 62 = step 2 (33/32 = 1.03125) = 269.802 Hz

**Recommendation for the acceptance test:** Use the formula above to compute expected Hz from the .scl file directly inside the test (the test acts as a self-test of the reference computation). Compare rendered freq against `expected × 2^(±0.1/1200) = expected × ~1.0000578` tolerance band. This makes the test self-contained and avoids the "where do reference values come from" rabbit hole.

### License attribution (Phase 29 precedent)

Phase 29's `flow-lang/Samples/CREDITS.md` establishes the in-repo attribution pattern. Phase 32 mirror at `flow-lang.Tests/fixtures/scala/LICENSE.md`:

```markdown
# Scala Tuning Fixtures — Credits

The 5 canonical `.scl` files in this directory are sourced from the
**Huygens-Fokker Foundation Scala scale archive** (Manuel Op de Coul, curator).

| Fixture | Original archive filename | Source URL |
| --- | --- | --- |
| `partch_43.scl` | `partch_43.scl` | https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/partch_43.scl |
| `slendro.scl` | `slendro.scl` | https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/slendro.scl |
| `carlos_alpha.scl` | `carlos_alpha.scl` | https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/carlos_alpha.scl |
| `pythagorean_12.scl` | `pyth_12.scl` (renamed for clarity; content verbatim) | https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/pyth_12.scl |
| `just_5limit.scl` | `ji_12.scl` (renamed for clarity; content verbatim — Robert Rich's "Basic JI with 7-limit tritone", 5-limit on the diatonic) | https://raw.githubusercontent.com/narenratan/scala_scale_archive/main/scl/ji_12.scl |

**Attribution:** the Scala scale archive is maintained by Manuel Op de Coul
(coul@huygens-fokker.org) and the Huygens-Fokker Foundation. The archive's
~5350 files are released for free use per the long-standing community
understanding documented on https://www.huygens-fokker.org/scala/. Files in
this directory are verbatim copies (cents/ratio values unchanged).

The 3 negative-case fixtures (`malformed_step_count.scl`, `malformed_cents.scl`,
`malformed_kbm.kbm`) are hand-authored minimal repros for parser error-path
tests and are released under the same terms as the Flow project itself.
```

## Test Patterns

The planner should mirror these patterns for Phase 32 tests.

### Pattern A — Parser unit Facts (no Flow runtime)

Reference: `flow-lang.Tests/Unit/Phase23/PitchConversionTuningFacts.cs:1-83`. Pattern: pure xUnit `[Fact]` methods that construct domain objects directly (no `FlowEngineRunner`), call the system under test, and assert with `Assert.Equal(..., precision: N)` for numerics.

Phase 32 mirror: `flow-lang.Tests/Unit/Phase32/ScalaParserFacts.cs`. Each fixture parses + asserts:
- `parsed.StepCents.Length == expectedStepCount - 1` (per D-10, period extracted)
- `parsed.PeriodCents == expectedPeriod ± 1e-9` (`2/1` → 1200.0 exact; ratios convert via Math.Log2)
- `parsed.Description == "..."` (verbatim string match)
- Ratios dictionary contents for ratio-only files
- Negative-case fixtures throw `ScalaParseException` with expected `{file}:{line}:{col}` text

### Pattern B — Fixture-loading integration test

Reference: `flow-lang.Tests/Integration/Phase23/ByteIdenticalDefaultTuningTests.cs` (inline source) + `flow-lang.Tests/Integration/Phase28/RagtimeFixtureTests.cs` (on-disk script fixture). Pattern: `[Collection("FlowScripts")]` + `FlowEngineRunner.RunSource(...)` with WAV output to `/tmp/flow_p32_*.wav`.

Phase 32 mirror: `flow-lang.Tests/Integration/Phase32/ScalaLoaderFixtureTests.cs`. Each test loads one of the 5 fixtures via Flow source:
```csharp
var (ok, _, stderr, _) = runner.RunSource(@"
use ""@audio""
tempo 120 {
    timesig 4/4 {
        Tuning t = (loadScala ""flow-lang.Tests/fixtures/scala/partch_43.scl"")
        tuning t {
            section s { Sequence m = | C4q D4q E4q | }
        }
        Song song = [s]
        Buffer b = (renderSong song ""sine"")
        (writeWav ""/tmp/flow_p32_partch_43.wav"" b)
    }
}");
Assert.True(ok && File.Exists("/tmp/flow_p32_partch_43.wav"));
```

### Pattern C — Frequency-comparison Facts (FFT or direct buffer inspection)

Reference: `flow-lang.Tests/Helpers/Phase29Fft.cs:33-80` (Goertzel single-frequency energy). For Phase 32's ±0.1 cents acceptance test, the simpler approach is direct buffer inspection at the resolved-tuning level — compute expected Hz from the parsed `ResolvedTuning.MidiToHz[]` directly and compare against `PitchConversion.NoteToFrequency` output, rather than running an FFT on rendered audio.

Phase 32 mirror: `flow-lang.Tests/Unit/Phase32/NonOctavePitchFacts.cs`. Direct assertion path:
```csharp
[Fact]
public void CarlosAlpha_MidiAscending_FrequenciesMatchSpecValues_Within01Cents()
{
    var t = ScalaParser.Parse(File.ReadAllText("flow-lang.Tests/fixtures/scala/carlos_alpha.scl"));
    var resolved = new ResolvedTuning(t, ScalaKbmParser.Default(t));
    double middleHz = resolved.MidiToHz[60];
    for (int i = 0; i < 18; i++)
    {
        double expected = middleHz * Math.Pow(2.0, t.StepCents[i] / 1200.0); // i==0..16; i==17 is the period wrap
        double actual   = resolved.MidiToHz[60 + i + 1];
        double centsDiff = 1200.0 * Math.Log2(actual / expected);
        Assert.True(Math.Abs(centsDiff) < 0.1, $"step {i+1}: {centsDiff:F4}¢ deviation");
    }
}
```
(For an FFT-based test, see Phase29Fft.cs; not recommended here because the spec asks for cents precision, not perceptual envelope.)

### Pattern D — Byte-identical determinism test

Reference: `flow-lang.Tests/Integration/Phase23/TuningDeterminismTests.cs:36-110`. Pattern: `[Collection("FlowScripts")]` + two `FlowEngineRunner` instances + `RenderingDiagnostics.ResetForTesting()` between runs + `File.ReadAllBytes()` SequenceEqual.

Phase 32 mirror: `flow-lang.Tests/Integration/Phase32/ScalaTuningDeterminismTests.cs`. Each test runs the same inline source twice and asserts WAV byte equality. CRITICAL: per WARNING-4 (verified in Phase 23), call `RenderingDiagnostics.ResetForTesting()` in ctor/Dispose AND between sequential runs. The Phase 32 D-08 unmapped-key advisory uses the same dedup mechanism — it will leak between test runs without reset.

### Pattern E — Last-wins pragma interaction test

Reference: `flow-lang.Tests/Unit/Phase23/WriteMidiWarningFacts.cs:90-113` ("WarnsOnlyOnce"). Phase 32's last-wins test follows the same `[Collection("FlowScripts")]` shape but with explicit byte-comparison across nested sections:

```csharp
[Fact]
public void LastWins_JIPragma_WithPartchBlock_InsideBytesDifferFromOutside()
{
    // Render section_a (inside partch) and section_b (outside, JI active) to separate WAVs.
    // Assert: bytes differ. Bonus: spectral envelope comparison via Phase29Fft.
}
```

### Pattern F — Parser error format Facts

Reference: existing `ParseException` thrown from `TypeParser.cs:335`; tests in `Unit/Phase23/UnknownTuningPragmaFacts.cs` (style only, content irrelevant). Format pattern: `Assert.Throws<ScalaParseException>(() => ScalaParser.Parse(...))` + `Assert.Contains("{filepath}:{line}:{col} —", ex.Message)`.

## Validation Architecture

Phase 32 has `workflow.nyquist_validation: true`. Include the test dimensions below in VALIDATION.md.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (.NET 10) — same as all other flow-lang.Tests phases |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` (Microsoft.NET.Test.Sdk + xunit + xunit.runner.visualstudio) |
| Quick run command | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase32" -v minimal` |
| Full suite command | `dotnet test flow-lang.Tests -v minimal` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test type | Automated command | File exists? |
|--------|----------|-----------|-------------------|-------------|
| SPEC-1 | `(loadScala "path")` parses + returns Tuning value | unit + integration | `dotnet test --filter "ClassName~LoadScalaBuiltinFacts"` | ❌ Wave 0 |
| SPEC-1 | `Tuning` type registered; `(str t)` produces description | unit | `dotnet test --filter "ClassName~TuningTypeFacts"` | ❌ Wave 0 |
| SPEC-2 | `tuning t { section ... }` parses + dispatches | unit + integration | `dotnet test --filter "ClassName~TuningContextStatementFacts"` | ❌ Wave 0 |
| SPEC-2 | Stack-based tuning resolution last-wins | integration | `dotnet test --filter "FullyQualifiedName~LastWins"` | ❌ Wave 0 |
| SPEC-3 | 5 canonical fixtures parse without error | unit | `dotnet test --filter "ClassName~ScalaParserFacts"` | ❌ Wave 0 |
| SPEC-3 | Description, step count, ratios, cents all extracted correctly | unit | (above class) | ❌ Wave 0 |
| SPEC-4 | `.kbm` 2-arg overload alters pitch mapping | unit + integration | `dotnet test --filter "ClassName~ScalaKbmParserFacts"` | ❌ Wave 0 |
| SPEC-4 | Default KBM matches Phase 23 12-TET tonic behavior | unit | (above class) | ❌ Wave 0 |
| SPEC-5 | carlos_alpha frequencies within ±0.1 cents | unit | `dotnet test --filter "ClassName~NonOctavePitchFacts"` | ❌ Wave 0 |
| SPEC-5 | Negative cents produce descending intervals | unit | (above class) | ❌ Wave 0 |
| SPEC-6 | Last-wins pragma interaction (JI outside / Partch inside) | integration | `dotnet test --filter "FullyQualifiedName~LastWins"` | ❌ Wave 0 |
| SPEC-6 | Phase 23 unit tests stay GREEN (regression sweep) | unit + integration | `dotnet test --filter "FullyQualifiedName~Phase23" --no-build` | ✅ existing |
| SPEC-6 | Two-run byte-identical determinism | integration | `dotnet test --filter "ClassName~ScalaTuningDeterminismTests"` | ❌ Wave 0 |
| SPEC-7 | Error format `{file}:{line}:{col} — expected X got 'Y'` | unit | `dotnet test --filter "ClassName~ScalaParserErrorFacts"` | ❌ Wave 0 |
| SPEC-7 | 3 negative-case fixtures trigger errors | unit | (above class) | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase32" -v minimal`
- **Per wave merge:** `dotnet test flow-lang.Tests -v minimal` (full suite — must not increase the 62 pre-existing Phase 28 PerSynthArticulation failures)
- **Phase gate:** full suite GREEN minus the 62 pre-existing failures; Phase 23 sub-suite (`--filter "FullyQualifiedName~Phase23"`) MUST be 100% GREEN (any regression here = blocker)

### Wave 0 Gaps

- [ ] `flow-lang.Tests/Unit/Phase32/` — empty; new directory
- [ ] `flow-lang.Tests/Integration/Phase32/` — empty; new directory
- [ ] `flow-lang.Tests/fixtures/scala/` — empty; new directory for fixtures + LICENSE.md
  - [ ] `partch_43.scl` (verified raw URL above)
  - [ ] `slendro.scl` (verified raw URL above)
  - [ ] `carlos_alpha.scl` (verified raw URL above)
  - [ ] `pythagorean_12.scl` (rename of `pyth_12.scl` — content verified)
  - [ ] `just_5limit.scl` (rename of `ji_12.scl` — content verified; OPEN QUESTION on naming, see below)
  - [ ] `malformed_step_count.scl` (hand-author: `! bad\n!\n descr\n -5\n!\n 2/1\n` — negative step count)
  - [ ] `malformed_cents.scl` (hand-author: insert `foo` on a step line)
  - [ ] `malformed_kbm.kbm` (hand-author: set reference frequency to `-50`)
  - [ ] `LICENSE.md` (content drafted above)
- [ ] No framework install needed — xUnit + .NET 10 already present
- [ ] `flow-lang.Tests/baselines/Phase32/` — NOT needed; Phase 32 tests are tolerance-based (±0.1 cents) and parser-correctness-based; no RMS baselines required (per Phase 28 SPEC-8 precedent — RMS baselines are for behavioural changes that legitimately shift bytes; Phase 32 INTRODUCES a new path that does not have a meaningful "before" baseline)

## Pitfalls & Constraints

### Pitfall 1 — Stack refactor blast radius (D-12)

**What goes wrong:** The CONTEXT and SPEC both speak of replacing `MusicalContext.Tuning` with a `Stack<RenderTuning>`. The codebase today does NOT have a stack — Phase 23 D-05 explicitly chose a single top-level scalar field per file scope. **8 files read `MusicalContext.Tuning` directly** (see Codebase Integration Map). D-12 supersedes Phase 23 D-05, but does NOT remove the readers' need to consume a single resolved value at render time.

**Why it happens:** The stack refactor is structural — every Phase 23 reader has to switch from "read scalar, fallback to 12-TET" to "peek top-of-stack, return Default if empty." Plus the pragma bridge in `FlowEngine.cs:148-157` switches from `SetTuning(TuningSystem)` to a stack push. The REPL D-14 sticky-pragma + ephemeral-blocks invariant adds a wrinkle.

**How to avoid:** Make the stack refactor a SINGLE dedicated plan early in the wave order. Don't entangle it with the parser work. Add a thin compatibility shim — keep a single `ActiveTuning` getter on `MusicalContext` that reads `TuningStack.Peek()` (or `Default` if empty). All 8 readers consume `ActiveTuning`; only the pragma bridge + the new `TuningContextStatement` interpreter case touch the stack directly.

**Warning signs:** Phase 23 tests turn red. Specifically `TuningDeterminismTests` + `PitchConversionTuningFacts` + `ByteIdenticalDefaultTuningTests` + `WriteMidiWarningFacts` + `EnharmonicWarningFacts`. Any of these breaking = the stack refactor changed observable behavior.

### Pitfall 2 — Phase 23 D-08 REPL stickiness vs D-14 block ephemeral

**What goes wrong:** Phase 23 D-08 (REPL pragma persistence) + Phase 32 D-14 (REPL blocks force-close) need to coexist cleanly. A naive stack implementation will EITHER lose the pragma push on REPL eval boundary OR leak block pushes across evals.

**Why it happens:** The natural implementation of "REPL eval boundary clears the stack" loses pragma stickiness. The natural implementation of "stack is fully persistent" leaks blocks.

**How to avoid:** Treat the bottom-of-stack slot specially — the FILE-SCOPE PRAGMA frame. It's pushed at most once per program execution; never popped at REPL boundary. Blocks push above it; the REPL eval boundary pops the stack back down to the bottom frame (or empty). Equivalent to "remembering the floor and not popping past it."

**Warning signs:** A REPL session that pushes `tuning partch { ... }` without closing the block ends up with subsequent unrelated REPL lines still rendering under Partch. The Phase 23 sticky-pragma Facts (e.g. one in `TuningDeterminismTests`) start passing for the wrong reason — they happen to put Partch on the stack from an earlier test that didn't clean up.

### Pitfall 3 — `RenderTuning` record-struct equality (D-03 extension)

**What goes wrong:** `RenderTuning` is a `readonly record struct` (Phase 23 RenderTuning.cs:11). Records auto-generate `Equals` based on all positional parameters. Adding `ResolvedTuning? Custom` (a reference type) as a 5th positional parameter means `RenderTuning.Equals` compares Custom by REFERENCE equality (which is fine per the SPEC's reference-identity contract). BUT: the byte-identical 12-TET short-circuit at `PitchConversion.cs:66` (`tuning.System == TuningSystem.EqualTemperament`) does NOT check `Custom == null` — under D-03, if someone constructed `new RenderTuning(EqualTemperament, ..., custom: someResolved)` and that's plausible if the wedge is misapplied to a custom tuning, the short-circuit fires and Custom is silently ignored.

**Why it happens:** RenderTuning has TWO axes now — the Phase 23 wedge (System ∈ {EQ, JI, Pyth}) AND the Phase 32 custom (Custom ∈ {null, ResolvedTuning}). These should be MUTUALLY EXCLUSIVE.

**How to avoid:** At the `PitchConversion.cs:62` short-circuit, check `Custom is null AND System == EqualTemperament` — only then take the byte-identical 12-TET path. Add a debug assertion `Debug.Assert(!(Custom != null && System != EqualTemperament))` or simply document the invariant. Recommendation: `Custom != null` implies the wedge System is irrelevant; treat as "the custom tuning wins, regardless of System value."

**Warning signs:** `RenderTuning.Default == new RenderTuning(EQ, Major, 'C', 0, custom: someResolved)` evaluates to false (good — record equality includes Custom) — but if the short-circuit at PitchConversion checks only System, it produces wrong frequencies. Catch with: `CustomTuningOverridesSystem` Fact.

### Pitfall 4 — Step count line counts the period or not? (Spec ambiguity)

**What goes wrong:** Per the verified spec quotes: "The second line contains the number of notes" + "the first note of 1/1 or 0.0 cents is implicit." This LEAVES AMBIGUOUS whether the step count includes the final-period line. Reading the partch_43 fixture verbatim: `43` is declared, and there are 43 step lines INCLUDING `2/1`. So step count = explicit-step-lines-following = N (including period). The "implicit 1/1" is the 0th step — not counted in the declared number.

**Why it happens:** Different parser implementations disagree. The pattern `[implicit 1/1, step1, step2, …, stepN-1, period]` would naturally lead someone to write step count = N-1 OR step count = N. Real archive files use step count = N (including period).

**How to avoid:** Document in `ScalaParser` clearly: "step count = number of step values after the count line, INCLUDING the period (which is the final value)." Parser asserts: number of value-lines read == declared count. Period is `StepCents[N-1]` after parsing; planner then EXTRACTS it to `PeriodCents` per D-10, leaving `ResolvedTuning.StepCents` with `N-1` intra-period entries.

**Warning signs:** Partch_43 parses as 44 steps OR carlos_alpha parses as 17 steps — both off-by-one symptoms.

### Pitfall 5 — Filename divergence between SPEC and archive

**What goes wrong:** SPEC names `pythagorean_12.scl` and `just_5limit.scl`; the archive ships `pyth_12.scl` and (no exact match — closest is `ji_12.scl`).

**Why it happens:** SPEC was written without verifying archive filenames.

**How to avoid:** Use the SPEC-mandated names in-repo, document the rename in LICENSE.md and inside the file's `!` comment header. Surface the just_5limit ambiguity to the user — there's a real semantic question (`ji_12.scl` is 5-limit on the diatonic but has a 7-limit tritone vs `ptolemy.scl` is strict 5-limit but only 7-tone).

**Warning signs:** Audit / licensing review flags the rename as misattribution. Mitigation: file's first `!` comment line states the original archive filename verbatim.

### Pitfall 6 — D-13 advisory must continue to fire under custom tunings

**What goes wrong:** Phase 23 D-13 says `writeMidi` emits a stderr advisory when `MusicalContext.Tuning != EqualTemperament`. After Phase 32, the predicate "is custom tuning active?" is `RenderTuning.Custom != null` (D-03). The MIDI export code at `flow-lang/StandardLibrary/Audio/MidiExport.cs:199-204` must be updated to fire under either condition.

**Why it happens:** Existing code checks `musicalCtx?.Tuning is TuningSystem activeTuning && activeTuning != TuningSystem.EqualTemperament` — Phase 32 introduces a new path (`Custom != null`) that this predicate does NOT detect.

**How to avoid:** Update `MidiExport.cs:199-204` (and the parallel `HarmonyFunctions.cs:56-60` enharmonic guard) to check the resolved `RenderTuning` not the raw `TuningSystem` enum. Specifically: the predicate becomes `renderTuning.Custom != null || renderTuning.System != EqualTemperament`.

**Warning signs:** Phase 23 `WriteMidiWarningFacts` Pass; new "WriteMidi under custom Scala tuning emits advisory" Fact fails. Add this Fact to the acceptance battery.

### Pitfall 7 — Phase 28 PerSynthArticulation FFT pre-existing 62 failures

**What goes wrong:** The `flow-lang.Tests` suite currently has 62 known-failing tests in `Unit/Phase28/PerSynthArticulationTests` + parts of `Integration/Phase28/*` + `FlowScriptTests.RunsToCompletion` (per `phases/31-lsp-enhancements-jetbrains-stretch/deferred-items.md`). These are unrelated to tuning — they're a Phase 28 articulation-envelope regression Phase 31 catalogued and Phase 29 v1.5 backlog owns.

**Why it happens:** Pre-existing baseline before Phase 32 work begins. NOT a Phase 32 regression.

**How to avoid:** Phase 32 phase gate is "62 known-failing tests does not INCREASE." Document the baseline 62 count at Phase 32 start. After each plan, re-run full suite; the delta from 62 must be 0 (or negative if Phase 32 accidentally fixes some).

**Warning signs:** Full-suite failure count rises above 62. Investigate immediately — likely a Phase 32 regression masquerading as Phase 28 fallout.

### Pitfall 8 — Two-run byte-identical determinism contract

**What goes wrong:** Per CLAUDE.md "Conventions" section, Phase 28 dropped pre-Phase-28 byte-identical determinism BUT preserved two-run determinism (same git SHA → same bytes). Phase 32 must keep this contract for both standard 12-TET output AND new custom-tuning output.

**Why it happens:** Hand-rolled parsers can introduce non-determinism via Dictionary iteration order, floating-point precision drift across builds, or hash-randomization-affected lookups.

**How to avoid:** Use `SortedDictionary` or insertion-order-preserving `Dictionary` (the .NET 10 default Dictionary preserves insertion order for iteration). Parse cents with `CultureInfo.InvariantCulture` so `1.5` always reads as 1.5 regardless of locale. The MidiToHz pre-compute uses fixed math (Math.Log2 / Math.Pow) — deterministic per IEEE 754. Test: Phase 32 determinism Facts run the same partch_43 → WAV twice and assert byte equality.

**Warning signs:** ScalaTuningDeterminismTests fail randomly. Investigate Dictionary iteration / parsing locale.

### Pitfall 9 — `tuning` keyword shadowing user identifier

**What goes wrong:** Per SPEC line 139, `tuning` becomes a reserved keyword. Any pre-existing user script using `tuning` as a variable / function name BREAKS. The SPEC accepts this pre-public.

**Why it happens:** Trade-off — keyword-style block syntax > extensibility for user identifiers.

**How to avoid:** Grep all `.flow` files in the repo for `tuning` as an identifier before Phase 32 implementation. Recommended grep: `rg "\btuning\b" --type-add 'flow:*.flow' -t flow` — search reveals usage. If any test or example file uses it, rename. Update CLAUDE.md "Language Features" section to add `tuning` to the keyword list (currently doesn't mention `voicePool`/`tuning`/etc. — same omission as voicePool from Phase 28).

**Warning signs:** Existing .flow scripts in `examples/` or `tests/` fail to parse after the keyword lands.

## Open Questions

These are decisions NOT locked by SPEC or CONTEXT — surface to the user before execution.

1. **`pythagorean_12.scl` and `just_5limit.scl` archive filename mismatch.** The SPEC names files that don't exist in the archive under those exact names. Recommended resolution: commit the verified `pyth_12.scl` and `ji_12.scl` contents IN-REPO under the SPEC-mandated names (`pythagorean_12.scl` / `just_5limit.scl`), documenting the rename in LICENSE.md AND in the file's `!` comment header. **Surface to user — is this rename acceptable, or do they want the original archive filenames preserved (which would require an SPEC update)?**

2. **`just_5limit.scl` semantic ambiguity.** The closest archive matches are `ji_12.scl` (12-tone, 5-limit-on-diatonic-with-7-limit-tritone) and `ptolemy.scl` (7-tone strict 5-limit). The SPEC name "just_5limit" implies the latter, but the SPEC's "5 canonical 12-tone-or-larger fixtures" framing implies the former. Recommended: ship `ji_12.scl` contents (12 entries, 5-limit-dominant). **Surface to user — is the 7-limit tritone in step 6 (`7/5`) acceptable, or do they want strict 5-limit (necessitating a 7-tone fixture)?**

3. **Huygens-Fokker license wording.** The archive does NOT have an explicit license statement on its download page. The community treats it as freely usable, but a strict open-source audit might flag this. Recommended LICENSE.md wording (above) softens "public-domain release" to "released for free use per the long-standing community understanding" + attribution. **Surface to user — is the softened wording acceptable, or do they want a more conservative path (e.g. fewer fixtures, only those with explicit free-use statements; OR vendoring just the cents/ratio values without the original file headers)?**

4. **Ratio with spaces around slash (`3 / 2`).** The spec is silent. Recommended: REJECT (parser tight). **Surface to user — should the parser tolerate `3 / 2` as a synonym for `3/2` (charitable), or reject (strict)?**

5. **Scientific notation in cents (`1.5e2`).** The spec is silent; no archive file uses it. Recommended: REJECT (no `NumberStyles.AllowExponent` in the parse call). **Surface to user — strict reject, or charitable accept?**

6. **Comma-decimal cents (`100,5`).** The spec is silent. Recommended: REJECT (parse with `CultureInfo.InvariantCulture` only). **Surface to user — strict reject?**

7. **`tuning` keyword as a procedure name.** Phase 32's lexer keyword addition will shadow `tuning` as a user identifier (SPEC line 139 accepts this). Existing parser allows other context keywords (`pan`/`gain`/`tempo`) as proc names (Parser.cs:247). **Surface to user — should `tuning` be added to that allowlist (i.e. callable as `proc tuning(...)` even though it's a keyword), or fully reserved?** Recommendation: fully reserved — cleaner break, less code, surfaces the keyword to LSP completions.

8. **Tutorial chapter addition.** SPEC line 107 leaves this to planner's discretion. **Surface to user — do they want a `Tutorial Chapter N: Scala microtonal tunings` added (extends `examples/tutorial.flow`), or is this a v1.5 polish item?**

9. **Pre-Phase-28 byte-identical determinism vs Phase 32.** CLAUDE.md says pre-Phase-28 baselines are GONE. Phase 32's new path is a fresh introduction (no "before" baseline). **Should Phase 32 establish a NEW baseline file (carlos_alpha render committed under baselines/Phase32/), or rely solely on tolerance-based assertions?** Recommendation: tolerance-only — the cents-precision assertion is more robust against future dither / mix changes than a fixed-byte baseline.

10. **D-08 unmapped-key advisory cardinality.** SPEC + CONTEXT say one-shot advisory via WarnOnce. The sentinel key dimension is ambiguous — fire once per `(description)`? per `(description, midiNote)`? per process? Recommendation: once per `(description)` per process (matches D-13 pattern of "one warning per tuning name per process"). **Surface to user if they want different cardinality.**

## Assumptions Log

| # | Claim | Section | Risk if wrong |
|---|-------|---------|---------------|
| A1 | Leading blank lines before description are tolerated | Domain Context §.scl Format | Parser rejects archive files that happen to start with blank lines; mitigation low (no real-world file does this) |
| A2 | Leading whitespace on the description line is tolerated | Domain Context §.scl Format | Parser strips leading whitespace; mitigation: round-trip the verbatim line, including leading whitespace |
| A3 | Spaces around `/` in ratios are REJECTED | Domain Context §.scl Format | Real-world file fails to parse; mitigation: tolerance is easy to add later if found necessary |
| A4 | Scientific notation in cents is REJECTED | Domain Context §.scl Format | Same as A3 |
| A5 | Comma-decimal cents is REJECTED | Domain Context §.scl Format | Same as A3 |
| A6 | Step count includes the period entry (verified via partch_43 + slendro fixture content) | Domain Context §.scl Format | Off-by-one — but VERIFIED by content fetch, so risk is LOW |
| A7 | Scala archive is "public domain"-like; LICENSE.md attribution wording per Phase 29 precedent | Fixture Sourcing | Licensing audit flags vendoring; mitigation: softened wording |
| A8 | `ji_12.scl` content satisfies the `just_5limit.scl` SPEC requirement | Fixture Sourcing | Audit / user surfaces strict-5-limit requirement; mitigation: swap to `ptolemy.scl` (7-tone) |
| A9 | `.kbm` format from modartt forum + Sevish blog matches actual Scala application's expected format | Domain Context §.kbm | Real .kbm file from archive fails to parse; mitigation: hand-test against at least one real .kbm before shipping |
| A10 | KBM "Formal octave" field semantics — typically `0` meaning "use .scl's final-step period as wrap" | Domain Context §.kbm | Non-zero values produce surprising mapping; mitigation: reject non-zero with clear error in Phase 32 (defer support to v1.5) |
| A11 | `tuning` keyword shadowing in pre-existing .flow scripts can be detected with a grep | Pitfall §9 | Some scripts have the identifier; mitigation: rename before shipping |
| A12 | Phase 32 introduces no NEW failures beyond the 62 pre-existing Phase 28 ones | Pitfall §7 | Hidden regression masks as Phase 28 fallout; mitigation: explicit delta-tracking |

## Environment Availability

Phase 32 is purely code/test changes within an existing .NET 10 solution. No new tools required.

| Dependency | Required by | Available | Version | Fallback |
|------------|-------------|-----------|---------|----------|
| .NET 10 SDK | All code | `[ASSUMED — verified in repo state]` | net10.0 (csproj) | — |
| Internet access (for fixture download at plan-time) | Wave 0 fixture commit | `[ASSUMED]` | — | Could vendor manually if archive becomes unreachable |
| `curl` or equivalent | One-time fixture fetch during Wave 0 | `[VERIFIED]` curl tested above | 8.x | wget |

No missing dependencies block execution. No fallbacks required.

## Sources

### Primary (HIGH confidence)

- Huygens-Fokker .scl format spec: `https://www.huygens-fokker.org/scala/scl_format.html` — verified 2026-05-13, source for all CITED format rules
- Phase 23 CONTEXT.md (`.planning/phases/23-microtonal-tuning-wedge/23-CONTEXT.md`) — supersession of D-05 + preserved D-13, D-08 extension
- Phase 32 SPEC.md + CONTEXT.md (in this phase directory) — locked requirements + 15 implementation decisions
- Codebase reads (all line ranges in Codebase Integration Map verified by direct file inspection 2026-05-13):
  - `flow-lang/StandardLibrary/Audio/Tuning/RenderTuning.cs:11-18`
  - `flow-lang/StandardLibrary/Audio/PitchConversion.cs:57-93`
  - `flow-lang/Runtime/MusicalContext.cs:42-69,89,137`
  - `flow-lang/Runtime/ExecutionContext.cs:195-238,280-293`
  - `flow-lang/Core/FlowEngine.cs:120-157`
  - `flow-lang/Lexing/PragmaRegistry.cs:1-85`
  - `flow-lang/Lexing/PragmaScanner.cs:35-77`
  - `flow-lang/Lexing/SimpleLexer.cs:855-887`
  - `flow-lang/Lexing/TokenType.cs:7-36`
  - `flow-lang/Parsing/Parser.cs:102-153,500-694`
  - `flow-lang/Parsing/TypeParser.cs:335-338`
  - `flow-lang/Ast/Statements/MusicalContextStatement.cs:1-21`
  - `flow-lang/Interpreter/Interpreter.cs:97-323`
  - `flow-lang/StandardLibrary/Audio/SongRenderer.cs:140-185`
  - `flow-lang/StandardLibrary/Audio/MidiExport.cs:164,199-204`
  - `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:49-60`
  - `flow-lang/Diagnostics/RenderingDiagnostics.cs:1-51`
  - `flow-lang.Tests/Helpers/Phase29Fft.cs:33-80`
  - `flow-lang.Tests/Helpers/RmsRegressionTests.cs:1-80`
  - `flow-lang.Tests/Unit/Phase23/PitchConversionTuningFacts.cs:1-83`
  - `flow-lang.Tests/Unit/Phase23/WriteMidiWarningFacts.cs:1-170`
  - `flow-lang.Tests/Integration/Phase23/ByteIdenticalDefaultTuningTests.cs:1-123`
  - `flow-lang.Tests/Integration/Phase23/TuningDeterminismTests.cs:1-80`
  - `flow-lang.Tests/Integration/Phase28/RagtimeFixtureTests.cs:1-80`
  - `flow-lang.Tests/Fixtures/FlowEngineRunner.cs:1-60`
  - `flow-lang/Samples/CREDITS.md` (Phase 29 license precedent)

### Secondary (MEDIUM confidence — multi-source corroboration)

- `.kbm` format: cross-checked between `https://forum.modartt.com/viewtopic.php?id=5724` (modartt user forum) and `https://sevish.com/2017/mapping-microtonal-scales-keyboard-scala/` (Sevish blog tutorial). The official Huygens-Fokker `.kbm` page is currently 404; community sources agree on field order + meaning.
- Scala archive content for fixtures: corroborated between the Huygens-Fokker archive (verified URL on downloads page) and the GitHub mirror at `https://github.com/narenratan/scala_scale_archive` (which is a verbatim mirror). All 4 verified-content fixtures (partch_43, carlos_alpha, slendro, pyth_12) fetched from the mirror match expected scale values from independent music-theory references (Helmholtz/Ellis for slendro, Wikipedia for Pythagorean ratios).

### Tertiary (LOW confidence — needs validation)

- License status of the Huygens-Fokker archive — community understanding, not formally stated by the Foundation. See Open Questions §3.
- `just_5limit.scl` semantic — fixture choice is a judgment call. See Open Questions §2.

## Metadata

**Confidence breakdown:**
- Codebase integration map: HIGH — all line numbers verified by direct file read.
- .scl format core: HIGH — directly cited from spec.
- .scl format edge cases (spaces around slash, scientific notation, comma-decimal): LOW — spec is silent, decisions are charitable judgment calls.
- .kbm format: MEDIUM — community sources agree; no canonical first-party spec page currently accessible.
- Fixture sourcing: HIGH for 3 fixtures, MEDIUM for 2 (filename rename + just_5limit semantic).
- Reference Hz values for ±0.1 cents: HIGH — derivation formula is unambiguous; recommendation is to compute in-test rather than pin pre-computed values.
- Test patterns: HIGH — all referenced from existing Phase 23 / Phase 28 / Phase 29 tests.
- Pitfalls: HIGH — D-12 stack refactor is the dominant risk; the other 8 are real but manageable.

**Research date:** 2026-05-13
**Valid until:** 2026-06-13 (codebase is stable mid-v1.4; Scala spec hasn't changed in ~30 years; fixtures sourced from archive v93 Jan 2025 mirror, v94 March 2026 ships at HF directly with no spec changes)

## RESEARCH COMPLETE
