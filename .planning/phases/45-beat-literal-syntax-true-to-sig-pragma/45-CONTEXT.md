# Phase 45: Beat Literal Syntax & True-to-Sig Pragma - Context

**Gathered:** 2026-05-25
**Status:** Ready for planning

<domain>
## Phase Boundary

Close the Beat-ergonomics gap from Phase 43. Composers get a first-class `Nb` literal (matching the rest of the music-type family — `Nms`/`Ns`/`Nc`/`Nst`/`NdB`/`NHz`) plus an opt-in `enable beat-true-to-sig;` file pragma that retunes literal meaning to the active time signature's beat unit instead of always-quarter. Phase 42 AUDIT.md §1 identified `BeatType` as the sole coercible orphan; Phase 43 backfilled four Beat-typed builtins (`beatToSec` / `secToBeat` / `delay(Buffer, Beat)` / `renderBarAtBeat(Bar, Beat)`); Phase 45 closes the literal-syntax half so composers can finally write Beat values ergonomically (`0.5b` instead of `(beat 0.5)`).

**Three coordinated changes:**

- **`Nb` literal syntax** (lowercase `b`; `B` is reserved as the German-notation Bb pitch root and would collide with `dB`/Decibel). Defaults: `1b = 1 quarter note = 60/bpm seconds`, matching MIDI/DAW convention. No conflict with note `B` (which requires octave digit `B4`) or with NoteValue letters (`q/h/w/e/s` — none use `b`). Lexer follows the existing `Nms`/`Ns`/`Nc`/`Nst`/`NdB`/`NHz` precedent: single-char `b` suffix in `ScanNumberOrSpecialLiteral`, signed `+Nb`/`-Nb` at expression-start in `TryLexTypedLiteral`.

- **`enable beat-true-to-sig;` file pragma**, opt-in, file-scoped, last-wins. Matches the existing `enable justIntonation;` / `enable pythagorean;` / `enable equalTemperament;` / `enable matchExhaustive;` / `enable strict;` (Phase 44) family. When active, `Nb` literals AND `(beat N)` constructor calls multiply by `4.0/denominator` at evaluation time, reading active `MusicalContext.TimeSignature`. In `timesig 6/8 {}` with pragma on: `1b = 1 eighth`; in `timesig 2/2 {}`: `1b = 1 half`; in default `4/4` (or no active timesig): unchanged (`1b = 1 quarter`). Gives composers the musician-intuition path for non-quarter meters (jigs, cut time, irregular meters) without breaking existing tempo/BPM/MIDI semantics.

- **Pragma affects literal CONSTRUCTION only.** Beat values stored are always quarters internally — the eval-time multiplier resolves to a quarter-relative double before the `Value.Beat` is constructed. All Phase 43 builtins (`beatToSec` / `secToBeat` / `delay(Buffer, Beat)` / `renderBarAtBeat`), the 8 `secondsPerBeat = 60.0/bpm` sites (`SongRenderer.cs:361` / `Timeline.cs:49` / `PlaybackFunctions.cs:382` / etc.), MIDI `microsPerBeat`, and `Voice.OffsetBeats` remain unchanged. Pure parse-and-eval-time desugar.

**Cross-file consistency.** Beat values that flow from a `beat-true-to-sig` file to a non-pragma file retain their pre-converted quarter value — semantically consistent because internal storage is always quarters regardless of file. Same file-scope semantics as Phase 44 D-03: the declaring file's pragma bit governs construction; consumers never re-interpret.

</domain>

<decisions>
## Implementation Decisions

### AST Representation (Area 1)

- **D-01:** New `BeatLiteralExpression(double RawValue, SourceLocation Loc)` AST record in `flow-lang/Ast/Expressions/`, alongside `ChordLiteralExpression` / `NoteStreamExpression` / `SongExpression` / `SymbolLiteralExpression` / `TupleLiteralExpression`. Carries the raw source double exactly as written (`0.5` for `0.5b`). `ExpressionEvaluator` gets a new switch arm that computes the final quarter-relative double at eval time: `final = pragma_on ? raw × (4.0 / current_timesig_denom) : raw`. Rejected: tagging a `needsBeatPragmaMultiplier` flag on a generic `LiteralExpression` — bleeds music-specific context into the universal literal node and diverges from the established own-AST-record pattern. Rejected: lex-time multiplier — pragma + timesig context aren't reachable at lex time.

- **D-02:** Eval-time TimeSig lookup reads `_context.ActiveMusicalContext.TimeSignature` (the current top-of-stack), defaulting to 4/4 when stack is empty. With pragma on + 4/4 default, the multiplier is `4/4 = 1.0` (identity) — pragma activation does not corrupt scripts that never set a timesig. With pragma off, the multiplier is `1.0` always (raw passes through).

### Pragma Scope (Area 2)

- **D-03:** `enable beat-true-to-sig;` is the pragma name (hyphenated). Added to `flow-lang/Lexing/PragmaRegistry.cs:27` `KnownPragmas` dictionary with description: `"Opt-in: Nb literals and (beat N) constructor calls multiply by 4/denominator at eval time, reading active timesig. So in 'timesig 6/8 { }' with pragma on, 1b = 1 eighth. File-scoped, no propagation via use imports."`. Hyphenated form matches the kebab-case feel of `beat-true-to-sig` as it appears in ROADMAP — diverges from camelCase precedents (`justIntonation`/`matchExhaustive`) but pragma names are composer-facing English phrases and `beat-true-to-sig` reads cleanly in kebab.

- **D-04:** `ExecutionContext.BeatTrueToSig` boolean field, set by ModuleLoader when the file's pragma set includes `beat-true-to-sig`. Push/pop mirrors Phase 44 D-02's `StrictMode` discipline — value reflects the DECLARING file's pragma bit, not the caller's. Stdlib `.flow` files (`audio.flow` / `bars.flow` / `notation.flow` / etc.) do NOT enable the pragma, so any Beat literals they construct remain raw quarters.

- **D-05:** Both `Nb` literal and `(beat N)` constructor honor the pragma — no escape hatch. The existing registration at `BuiltInFunctions.cs:553`:
  ```csharp
  registry.Register("beat", new FunctionSignature("beat", [DoubleType.Instance],
          ParameterNames: ["value"]),
      args => Value.Beat(args[0].As<double>()));
  ```
  migrates to `RegisterContextDependent` (Phase 43 D-08 precedent — same mechanism as `beatToSec` / `secToBeat`) so the lambda has access to `ExecutionContext` and reads `ctx.BeatTrueToSig` + active `MusicalContext.TimeSignature` at call time. Composers wanting raw-quarter semantics under pragma split into a non-pragma helper file and import. Rationale: pre-traction no-deprecation latitude (`project_pre_public_no_legacy_burden`) means we ship the smallest surface; if a real composer reports needing a `(beatRaw N)` escape hatch, it ships in a one-commit follow-up.

### Lexer Surface (Area 3)

- **D-06:** Signed `+Nb` / `-Nb` lex at expression-start via `flow-lang/Lexing/SimpleLexer.cs` `TryLexTypedLiteral` (line ~600 range). New branch follows the `+/-Nst` (line 608-621) / `+/-NdB` (line 637-650) pattern: append `b`, slice the numeric prefix, `double.TryParse`, emit `Token(TokenType.BeatLiteral, text, start, doubleVal, ...)`. Add new `TokenType.BeatLiteral` enum case alongside `SemitoneLiteral` / `DecibelLiteral` / `CentLiteral` / `HertzLiteral` / `TimeLiteral`.

- **D-07:** Unsigned `Nb` (including `0.5b`, `2b`, `1.0b`) lex via `ScanNumberOrSpecialLiteral` (line 688). Branch sits between the `c` suffix branch (line 766-776) and the `s` suffix branch (line 668-679) with guard `Peek() == 'b' && !char.IsLetter(PeekNext())` — matches the existing `c` suffix's `!char.IsLetter(PeekNext())` identifier-disambiguation pattern. This keeps `1bar` lexing as `1` + `bar` identifier; keeps `2beats` lexing as `2` + `beats` identifier; accepts `0.5b D4q` (Beat literal followed by anything non-letter).

- **D-08:** Runtime accepts negative Beat values as valid doubles — no rejection guard. `-2b` constructs `Value.Beat(-2.0)` regardless of mode. Musical semantics of negative beats (anticipation? rest offsetting?) are the composer's call; the language doesn't impose a musical interpretation. Mirrors how `-12dB` and `-50c` are accepted as valid doubles even though some sign conventions are ambiguous.

### Parser + Evaluator Integration (Area 1 follow-on)

- **D-09:** Parser handles `BeatLiteral` token in `Parser.ParsePrimary` (or wherever `DecibelLiteral` / `CentLiteral` / `HertzLiteral` are currently handled). Emits a `BeatLiteralExpression(token.NumericValue, token.Loc)` instead of a flat `LiteralExpression(Value.Beat(...), BeatType.Instance)` — because eval-time context lookup needs the raw source value preserved through to eval.

- **D-10:** `ExpressionEvaluator` adds a switch arm for `BeatLiteralExpression`:
  ```csharp
  BeatLiteralExpression beatLit => EvaluateBeatLiteral(beatLit),
  ```
  where `EvaluateBeatLiteral` reads `_context.BeatTrueToSig` + active `MusicalContext.TimeSignature`, computes the multiplier, and returns `Value.Beat(beatLit.RawValue * multiplier)`. Implementation lives in `ExpressionEvaluator.cs` directly (no new helper class) — the multiplier formula is two lines.

### Test Infrastructure (Area 5)

- **D-11:** Two-track testing mirroring Phase 44 D-14 + Phase 43 REQ-MOD-12 precedent:
  - **Positive `.flow` tests:** `tests/test_beat_literal.flow` (lexer + parser smoke), `tests/test_beat_pragma_off.flow` (default `1b = quarter` across 4/4 / 6/8 / 2/2), `tests/test_beat_pragma_on.flow` (`enable beat-true-to-sig;` + multiplier behavior across 4/4 / 6/8 / 2/2 / 5/4 / 7/8), `tests/test_beat_cross_file.flow` (pragma-on file imports pragma-off file; verify Beat values flow as quarters).
  - **xUnit Facts** under `flow-lang.Tests/Phase45/`:
    - `BeatLiteralParserTests.cs` — pins lexer accepts `0.5b` / `2b` / `1.0b` / `+1b` / `-2b`; rejects `1bar` / `1beats` / `b1` (identifier collisions); pins `BeatLiteralExpression` AST shape; pins signed/unsigned token routing through the two lexer entry points.
    - `BeatTrueToSigPragmaTests.cs` — pins `enable beat-true-to-sig;` registers in `PragmaRegistry`; pins `ExecutionContext.BeatTrueToSig` push/pop file-scope semantics (matching Phase 44 D-02's `StrictMode` test pattern); pins multiplier formula `raw × 4/denom` across 4/4 / 6/8 / 2/2 / 5/4 / 7/8; pins `(beat N)` constructor pragma-awareness via `RegisterContextDependent`; pins identity behavior in pragma-off mode.
  - **Two-run cmp-clean** preserved — Phase 45 adds no PRNG sites; tutorial WAV/MIDI outputs deterministic (D-v1.5-06 PrngRegistry contract irrelevant here, no stochastic primitives invoked).

### Tutorial + Documentation (Area 4)

- **D-12:** Two tutorial files under `examples/beat/`:
  - `examples/beat/intro.flow` — 6/8 jig demonstrating with/without pragma. Shows: (a) pragma-off baseline (`1b = quarter` even in 6/8 — composer must manually use `0.5b` for an eighth), (b) `enable beat-true-to-sig;` flipping `1b` to mean "one eighth" under `timesig 6/8 {}`. Renders MIDI + WAV. ~50-80 lines.
  - `examples/beat/cut-time.flow` — `timesig 2/2` showing `1b = half`. Demonstrates the same pragma applied to a different non-quarter meter. Renders MIDI + WAV.
- **D-13:** CLAUDE.md "Music Types Quick Reference" table gets a new row:
  ```
  | `0.5b` (Beat literal) | `Beat` | `Double`, `Float` | beat-position arithmetic; `enable beat-true-to-sig;` opt-in retunes literal to active timesig's beat unit (default 4/4 → `1b = quarter`) |
  ```
  Adjacent CLAUDE.md "Music-Specific" section gets a one-line addition mentioning the pragma family expansion (`tempo`/`timesig`/`key`/`swing`/`voicePool`/`tuning` block keywords unchanged; pragma list grows by one).

### Surface Decisions Locked (no question, ROADMAP-derived)

- **D-14:** `(str someBeat)` behavior UNCHANGED — emits plain double like `"0.5"`. Reason: emitting `"0.5b"` would break round-trip under `beat-true-to-sig` pragma (`0.5b` in 6/8 evaluates to 0.25 quarters; re-parsing `"0.25b"` under same pragma re-multiplies to 0.125 — different value). Composers continue to treat `Beat` as a tagged double for printing. If a real composer reports wanting literal-form printing for debugging, ships in a one-commit follow-up.

- **D-15:** REPL `:beat-true-to-sig` toggle NOT added in Phase 45. Pragma is file-scope; REPL is ephemeral. Composer enables via writing `enable beat-true-to-sig;` at the REPL (multi-line entry). If composer pressure surfaces a sticky-session use case, mirror Phase 44 D-16's `:strict on/off` family in a one-commit follow-up.

- **D-16:** Strict mode (Phase 44) interaction — `Nb` becomes the canonical way to write Beat values in `enable strict;` files. Phase 44 Axis A (no type coercion) disables the `Double → Beat` convertible-tier match, so a strict file calling `(delay buf 0.5 0.5 0.4)` (where the second arg is meant to be a Beat) would error; composer must write `(delay buf 0.5b 0.5 0.4)` under strict. This carry-forward is documented but NOT a Phase 45 implementation task — strict's Axis A already covers it.

- **D-17:** Dotted-rhythm `Nb.` syntax (e.g., `0.5b.` for dotted-beat = 0.75 quarters) NOT added. Composers can write `0.75b` directly. Note streams keep their own `q.`/`h.`/`w.` dotted-suffix language as that's a separate surface. Deferred until composer pressure surfaces.

### Claude's Discretion

- Exact placement of the `b` suffix branch in `TryLexTypedLiteral` ordering — between `+/-Nst` and `+/-NdB`, or elsewhere. Plan-phase decides based on suffix-conflict analysis (single-char `b` with non-letter guard is conflict-free among current suffixes; ordering is cosmetic).
- Whether to add a `BeatLiteralFacts.cs` regression file pinning the existing `(beat N)` constructor's Phase 26.1 DICT-01 acceptance (`<<C4, (beat 0.25)>>` Dict-key shape) to confirm `RegisterContextDependent` migration doesn't regress dict-key tuple constructions. Recommended (cheap insurance); plan-phase may bundle into `BeatTrueToSigPragmaTests.cs`.
- Order of execution (lexer vs parser vs evaluator vs pragma registry vs constructor migration). Plan-phase decides wave breakdown. Suggested ordering: (1) `TokenType.BeatLiteral` enum + tests; (2) lexer suffix branches + Parser AST emit + parser tests; (3) `ExecutionContext.BeatTrueToSig` field + `PragmaRegistry` entry + `ModuleLoader` push/pop + pragma tests; (4) `EvaluateBeatLiteral` switch arm + multiplier tests; (5) `(beat N)` constructor migration to `RegisterContextDependent`; (6) tutorial files + CLAUDE.md update.
- Whether to vendor `flow-lang.Tests/baselines/Phase45/` audio baselines for the two tutorial WAVs. Two-run cmp-clean preservation is mandatory; whether to commit reference renders is plan-phase's call (probably yes — match Phase 28 baseline precedent if any rendered audio is involved).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 45 Roadmap Anchor
- `.planning/ROADMAP.md` §"Phase 45: Beat Literal Syntax & True-to-Sig Pragma" — goal statement + the three-coordinated-changes structure (`Nb` literal, `enable beat-true-to-sig;` pragma, construction-only multiplier) + Phase 43 + Phase 44 ordering notes.
- `.planning/REQUIREMENTS.md` (Phase 45 REQ-BEAT-NN entries to be defined at plan-phase per ROADMAP line "Requirements: TBD (defined at plan-phase)").

### Phase 43 Beat Backfill (Prior Phase Deliverable)
- `.planning/phases/43-module-names-qualified-imports/43-CONTEXT.md` — D-08 `RegisterContextDependent` pattern locked for context-aware builtins; D-10 atomic polarity flip rule (rename Phase 42 fact in same commit as the surface lands).
- `.planning/phases/43-module-names-qualified-imports/43-VERIFICATION.md` §REQ-MOD-07, REQ-MOD-08, REQ-MOD-09, REQ-MOD-10 — closure evidence for the four Beat-typed builtins; Phase 45 `(beat N)` migration follows the same `RegisterContextDependent` lambda shape used in `beatToSec` / `secToBeat`.
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` §1 BeatType-orphan anchor finding — what Phase 43 closed (`Beat` no longer orphan) and Phase 45 reinforces (`Beat` now has ergonomic literal form).

### Phase 44 Strict Mode (Just Shipped, Carry-Forward)
- `.planning/phases/44-strict-mode/44-CONTEXT.md` §"Pragma Surface (Area 1)" D-02/D-03/D-04 — file-scope semantics + `ExecutionContext.<field>` push/pop discipline + single-line `PragmaRegistry` registration. Phase 45 D-03/D-04 mirror this pattern exactly.
- `.planning/phases/44-strict-mode/44-CONTEXT.md` §"Test Infrastructure (Area 4.3)" D-14 — two-track positive `.flow` + xUnit Facts. Phase 45 D-11 mirrors.
- `.planning/phases/44-strict-mode/44-CONTEXT.md` §"Live Coding + REPL Interaction (Area 4.3)" D-16 — `:strict on/off` REPL meta-command pattern. Phase 45 D-15 deliberately does NOT mirror (pragma is file-scope, REPL ephemeral); referenced for the deferral rationale.

### Pragma System (Phase 21 + 35 + 44 Precedent)
- `flow-lang/Lexing/PragmaRegistry.cs` — closed-set registry. D-03 adds the `["beat-true-to-sig"]` entry on the same line range as Phase 44's `["strict"]` (line 36 in current state).
- `flow-lang/Lexing/PragmaScanner.cs` — `enable <pragma>;` parser; D-12 unknown-pragma error wiring already covers `beat-true-to-sig` once registered.
- `flow-lang/Lexing/PragmaSet.cs` — Phase 21 D-02 pragma carrier type; `ExecutionContext.BeatTrueToSig` flips when `PragmaSet.IsEnabled("beat-true-to-sig")` returns true.
- `flow-lang/Runtime/MusicalContext.cs:97` — block-scope pragma precedent comment (`enable justIntonation;`); Phase 45 D-04 mirrors this push/pop model at file scope (not block scope — file-scope per D-04).

### Lexer Surface
- `flow-lang/Lexing/SimpleLexer.cs:600-685` — `TryLexTypedLiteral` signed-typed-literal entry point. D-06 adds the `+/-Nb` branch following the `+/-Nst` (608-621) and `+/-NdB` (637-650) patterns.
- `flow-lang/Lexing/SimpleLexer.cs:688-820` — `ScanNumberOrSpecialLiteral` unsigned-typed-literal scanner. D-07 adds the `b` branch following the `c` (766-776) pattern with `!char.IsLetter(PeekNext())` identifier-disambiguation guard.

### AST + Evaluator
- `flow-lang/Ast/Expressions/` — own-record pattern for music literals (`ChordLiteralExpression`, `NoteStreamExpression`, `SongExpression`, `SymbolLiteralExpression`, `TupleLiteralExpression`). D-01 adds `BeatLiteralExpression.cs` here.
- `flow-lang/Interpreter/ExpressionEvaluator.cs` — switch dispatch over AST → `Value`. D-10 adds the `BeatLiteralExpression` switch arm.
- `flow-lang/Parsing/Parser.cs` — `ParsePrimary` (or equivalent music-literal handlers). D-09 routes the `BeatLiteral` token through `BeatLiteralExpression`.

### Beat Type + Constructor
- `flow-lang/TypeSystem/SpecialTypes/BeatType.cs` — type-system anchor (Phase 26.1 DICT-01 + Phase 43 `IsCompatibleWith(Double|Float)`). Unchanged in Phase 45.
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:547-555` — current `(beat Double) → Beat` constructor registration. D-05 migrates this to `RegisterContextDependent` (pattern from Phase 43's `beatToSec` registration).
- `flow-lang/Runtime/Value.cs` — `Value.Beat(double)` constructor wrapper. D-10's `EvaluateBeatLiteral` returns through this.

### MusicalContext + Time Signature
- `flow-lang/Runtime/MusicalContext.cs:42-50` — `TimeSignatureData? TimeSignature { get; set; }` push/pop stack. D-02/D-10 read this for the multiplier formula. Default 4/4 when null (matches existing semantics across `SongRenderer` / `Timeline` / `PlaybackFunctions`).
- `flow-lang/Runtime/ExecutionContext.cs` — runtime context owning the `MusicalContext` stack + Phase 44's `StrictMode` field. D-04 adds `BeatTrueToSig` boolean here, same access pattern as `StrictMode`.

### Runtime + Internal Storage Invariants
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs:361` — `double secondsPerBeat = 60.0 / bpm`. Quarter-relative; Phase 45 does NOT touch.
- `flow-lang/StandardLibrary/Audio/Timeline.cs:49-66` — `secondsPerBeat` arithmetic both directions. Quarter-relative; Phase 45 does NOT touch.
- `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs:382-393` — voice offset math. Quarter-relative; Phase 45 does NOT touch.
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` — `microsPerBeat` SMF tempo events. Quarter-relative; Phase 45 does NOT touch.

### Project-Level Constraints
- `CLAUDE.md` "Music Types Quick Reference" table — D-13 adds the Beat row.
- `CLAUDE.md` "Music-Specific" section — D-13 one-line mention of pragma family expansion.
- External memory `project_pre_public_no_legacy_burden` (rewritten 2026-05-17) — pre-traction no-deprecation latitude. D-05 / D-14 / D-15 / D-17 explicitly invoke this latitude (one-commit follow-ups acceptable; surface minimization preferred over hypothetical-future-composer accommodation).
- External memory `feedback_ergonomics_priority` — ergonomics wins over implementation complexity. D-01 (own AST record over flag-on-LiteralExpression) and D-05 (single pragma-aware constructor over two parallel forms) honor this.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`PragmaRegistry.KnownPragmas`** dictionary (`flow-lang/Lexing/PragmaRegistry.cs:27`) — single-line entry, immutable registration. Phase 44 demonstrated the pattern in commit `[44 commits]` adding `["strict"]`.
- **`RegisterContextDependent` lambda shape** (Phase 22 DX-12 + Phase 43 D-08) — Phase 45 D-05 migrates the `(beat N)` constructor to this. The lambda receives `ExecutionContext` parameter, reads `ctx.BeatTrueToSig` + `ctx.ActiveMusicalContext.TimeSignature`, computes the multiplier inline.
- **`TryLexTypedLiteral`** (`SimpleLexer.cs:600-685`) — established signed-typed-literal entry point with 6 existing branches (`+/-NHz`, `+/-Nst`, `+/-Nc`, `+/-NdB`, `+/-Nms`, `+/-Ns`). D-06 adds the 7th (`+/-Nb`).
- **`ScanNumberOrSpecialLiteral`** (`SimpleLexer.cs:688-820`) — unsigned-typed-literal scanner with 6 existing suffix branches. D-07 adds the 7th (`Nb`).
- **AST record pattern** — Phase 26.1's `SymbolLiteralExpression` (single-property `Symbol` + Loc) is the closest precedent for `BeatLiteralExpression` (single-property `RawValue` + Loc).
- **Phase 44 `ExecutionContext.StrictMode` push/pop discipline** — bracketed `try { ctx.StrictMode = newBit; ... } finally { ctx.StrictMode = prevBit; }` at file-load and proc-entry sites. D-04 mirrors for `BeatTrueToSig`.

### Established Patterns
- **File-scope pragma push/pop via `ModuleLoader`** — set when loading the file's pragma set, popped when leaving. Phase 44 D-02 + Phase 21 D-02 set the precedent. Phase 45 D-04 follows.
- **Quarter-relative internal storage** — every existing `Beat` consumer in stdlib assumes the wrapped double is quarter-relative (`SongRenderer.cs:361`, `Timeline.cs:49`, `MidiExport`, `Voice.OffsetBeats`). Phase 45's pragma resolves at CONSTRUCTION; downstream consumers see no shape change.
- **Own AST record for music literals** — `ChordLiteralExpression` / `NoteStreamExpression` / `SongExpression` / `SymbolLiteralExpression` / `TupleLiteralExpression`. Phase 45 D-01 adds `BeatLiteralExpression`.
- **`default 4/4`** fallback — every existing site that reads `MusicalContext.TimeSignature` checks for null and assumes 4/4. Phase 45 D-02 multiplier formula handles the null case as identity (`4/4 = 1.0`).

### Integration Points
- **`Parser.ParsePrimary`** (or `ParseLiteral`) — where `DecibelLiteral` / `CentLiteral` / `HertzLiteral` / `TimeLiteral` tokens become `LiteralExpression` Values today. D-09 splits `BeatLiteral` off into its own `BeatLiteralExpression` emit path.
- **`InternalFunctionRegistry.Register` vs `RegisterContextDependent`** — current `(beat N)` registration uses plain `Register`; D-05 migration to `RegisterContextDependent` is the integration point. Same one-line API swap as Phase 43's `beatToSec` / `secToBeat`.
- **`ExpressionEvaluator` switch dispatch** — D-10 adds one switch arm (~5-line body). Sits among the existing music-literal arms.
- **`flow-lang.Tests/Phase45/`** — new test directory mirroring `Phase42/`, `Phase43/`, `Phase44/`. xUnit Facts auto-discovered.
- **`tests/test_*.flow`** — composer-facing positive scripts. Test runner is `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done`.
- **`examples/beat/`** — new directory for tutorial files; matches `examples/dsp/` / `examples/scala/` / `examples/sections/` / `examples/generative/` precedent.

</code_context>

<specifics>
## Specific Ideas

- **6/8 jig as canonical demonstration** — the literal example that closes the ergonomics gap. Composer writes a real Irish/Celtic-feel piece in `timesig 6/8 {}` with `enable beat-true-to-sig;` so `1b = eighth` matches musician intuition.
- **2/2 cut-time as the second example** — orchestral / march feel where `1b = half` matches musician intuition for "feel it in 2".
- **Mirror Phase 44 D-14's two-track test pattern exactly** — same xUnit directory structure (`Phase45/`), same parallel positive-`.flow` + Facts split, same two-run cmp-clean preservation language.
- **AST record matches Phase 26.1 `SymbolLiteralExpression` precedent** — single-property + Loc, immutable record.

</specifics>

<deferred>
## Deferred Ideas

- **`(beatRaw N)` escape hatch** — explicit raw-quarter constructor for composers in `enable beat-true-to-sig;` files who want a per-call bypass. Deferred per D-05 pre-traction latitude. If a real composer reports needing it, ships in a one-commit follow-up.
- **`(str someBeat)` emitting `"0.5b"` suffix form** — would enable round-trip in pragma-off files but break it in pragma-on. Deferred per D-14. If composer pressure surfaces, ships in a one-commit follow-up after the round-trip semantics question is resolved (possibly via a `(strFull someBeat)` variant that always emits canonical form).
- **REPL `:beat-true-to-sig on/off` sticky meta-command** — Phase 38 + Phase 44 D-16's `:strict` pattern could mirror here. Deferred per D-15 — pragma is file-scope, REPL is ephemeral. Composer writes `enable beat-true-to-sig;` at the REPL entry-point if needed.
- **Dotted-rhythm `Nb.` syntax** (e.g., `0.5b.` = 0.75 quarters in pragma-off, or 0.75 × `4/denom` quarters in pragma-on) — composers can write `0.75b` directly. Deferred per D-17.
- **Tied-Beat-literal syntax `Nb~`** — note streams already have `C4h~` for tied notes. Composer can write `(add 0.5b 0.5b)` or use sequence concatenation. Deferred indefinitely.

</deferred>

---

*Phase: 45-beat-literal-syntax-true-to-sig-pragma*
*Context gathered: 2026-05-25*
