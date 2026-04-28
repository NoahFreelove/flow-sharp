# Phase 19: Tuplets & Arbitrary Fractional Durations - Context

**Gathered:** 2026-04-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Composers can write tuplets (`{N:M ...}` brackets), arbitrary fractional note durations (`C4/N`), and per-note tuplet shorthand (`C4/X:Y[suffix]`) inside note streams. NoteStreamCompiler emits rational-fraction-backed `MusicalNoteData`. Bar-fit validator handles tuplet/fractional sums charitably (silent-truncate + Info diagnostic on overflow). MIDI export auto-elevates TPQN as needed (cap 9600). AUDIT-VERIFIED C5 (augment/diminish) re-validated against tuplet sequences.

This phase is the lead capability for v1.3. Depends hard on Phase 18 (`Fraction` struct + `MusicalNoteData.DurationFraction` field — binding pre-ordering #1 from REQUIREMENTS.md "Notes" section).

</domain>

<spec_lock>
## Requirements (locked via SPEC.md)

**8 requirements are locked.** See `19-SPEC.md` for full requirements, boundaries, and acceptance criteria.

Downstream agents MUST read `19-SPEC.md` before planning or implementing. Requirements are not duplicated here.

**In scope (from SPEC.md):**
- `TupletElement` AST record (recursive children, optional `:M` denominator, required duration suffix)
- `{N:M ...}` and `{N ...}` lexer/parser/compiler dispatch inside note-stream context only
- `C4/N` arbitrary-denominator note-stream lexer/parser support (only inside `| ... |`)
- `C4/X:Y[suffix]` per-note tuplet shorthand (TUP-08; same lexer state as `/N`, extended with optional `:Y` and optional level suffix)
- `CalculateAutoFitDuration` extension to handle rational-bar-fit math (without auto-fit inside tuplets)
- Bar-overflow charitable-truncate behavior + Info diagnostic
- MIDI export TPQN auto-elevation logic + 9600 cap error
- `augment`/`diminish` regression Facts on tuplet sequences + AUDIT-VERIFIED marker refresh
- Pre-landing collision grep transcript for `{`, `}` over `tests/`, `examples/`, `flow-lang/*.flow`

**Out of scope (from SPEC.md):**
- The `Fraction` struct itself — Phase 18 (FRAC-01)
- `MusicalNoteData.DurationFraction` field declaration — Phase 18 (FRAC-02)
- Pragma system / `enable` keyword — Phase 21
- Multi-letter enharmonic edges — Phase 20
- Microtonal tuning — Phase 23
- Scale linting — Phase 24
- Gaussian humanize — Phase 25
- LSP semantic-tokens / completion / hover updates for tuplet syntax — follow-up phase if needed
- Auto-fit duration inside tuplet brackets — locked NO (D-06 in spec)
- Hard-error bar overflow — locked NO (D-07 in spec)
- Tutorial demonstration of tuplets — Phase 26 (QOL-04)

</spec_lock>

<decisions>
## Implementation Decisions

### TUP-08 — Per-note shorthand AST shape

- **D-01:** `C4/X:Y[suffix]` extends the existing `NoteElement` record with an optional `(int Num, int Denom)? TupletRatio` field. No new AST record type. Compiler treats per-note tuplet-ratio'd notes as synthetic 1-element tuplet members at compile time, computing `DurationFraction = (suffix_fraction) / Num` of a whole. The `Denom` is preserved on the `MusicalNoteData` (new field on `MusicalNoteData` if not already present from FRAC-02) for MIDI TPQN computation but does NOT enter the per-note duration math. Rationale: minimal AST surface change, single discriminated-union member type for "a note with a duration", consistent with how `IsDotted` and `IsTied` already extend `NoteElement` without separate types.

- **D-02:** Per-note instances are independent — no implicit grouping, no consecutive-must-match rule (locked in SPEC.md TUP-08 text). `| C4/3:2 D4/5:4 E4/3:2 |` is legal and produces three independent `MusicalNoteData` instances each with its own `TupletRatio`.

### TUP-05 — Bar overflow truncation algorithm

- **D-03:** When the rational sum of bar element durations exceeds the time-signature value, the bar-fit validator **truncates the boundary-crossing element's duration** to fit, then drops all subsequent elements. Algorithm: walk elements left-to-right accumulating `Fraction sum`. When `sum + element.duration > timesig`, set the element's effective `DurationFraction = timesig - sum`, accumulate to the boundary, then drop remaining elements. Emit `ErrorReporter.ReportInfo` once per overflowing bar with message format `"Bar overflow: sum {actual_sum} exceeds time-signature {timesig}; truncated to fit at {boundary_element}"`. Preserves byte-identical determinism — same input always yields same truncation. Rationale: preserves leading content fidelity (most likely the user's primary intent); only the tail loses information; matches CLAUDE.md charitable-interpretation memory ("music > rigid correctness").

- **D-04:** Tuplet brackets WITHOUT explicit duration suffix raise a parse error (`"Tuplet bracket requires explicit duration suffix"`) per locked SPEC.md TUP-05 D-06. Auto-fit inside tuplets is NOT supported in v1.3.

### TUP-06 — MIDI TPQN auto-elevation

- **D-05:** TPQN computation lives in **MidiExport.cs as a single pre-export pass over the Song**. Walk all `MusicalNoteData` collecting `union(tuplet_denominators)` (drawn from `DurationFraction.Denominator` AND `TupletRatio.Numerator` when present). Compute `requiredTPQN = LCM(480, 2 × union)`. Set `DryWetMidi.MidiFile.TimeDivision` (file-level header — SMF spec) to `requiredTPQN`. Scale all delta-times by `requiredTPQN / 480`. Single computation per `writeMidi` call.

- **D-06:** TPQN-cap error message format (locked from SPEC.md TUP-06 acceptance): `"MIDI export requires TPQN={requiredTPQN}, exceeds cap 9600 (locked v1.3 D-05). Tuplet ratios in this song: [{sorted_unique_X:Y_list}]"`. Raised before writing any MIDI file (atomic — no partial export).

- **D-07:** When NO tuplets present in the song (existing test scripts, post-Phase-19), TPQN stays at the existing default of 480 — zero behavior change for tuplet-free output. Verified via byte-identical regression gate.

### Plan Structure (5 plans, wave-parallel)

- **D-08:** **Plan 19-01 — Tuplet bracket AST + parser + compiler.**
  Files: `flow-lang/Ast/Expressions/NoteStreamExpression.cs` (add `TupletElement` record + extend `NoteElement` with `(int, int)? TupletRatio` field), `flow-lang/Parsing/Parser.cs` (or `Parser.NoteStream.cs` if split exists — discover at plan time) parser dispatch on `{` token inside note-stream context, `flow-lang/Runtime/NoteStreamCompiler.cs` (recursive `CompileTupletElement` with accumulating `Fraction outerScale`). Covers TUP-01, TUP-02, TUP-03 (the bracket-form trio). One commit. Includes pre-landing collision grep for `{` / `}` over `tests/`, `examples/`, `flow-lang/*.flow` per Phase 14 D-21 precedent (transcript pasted into 19-01-PLAN.md).

- **D-09:** **Plan 19-02 — Lexer support for `/N` and `/X:Y[suffix]` per-note duration syntax.**
  Files: `flow-lang/Lexing/SimpleLexer.cs` (note-stream context lexer state extension to recognize `/N` and `/X:Y[suffix]` after a note name), `flow-lang/Parsing/Parser.cs` parser handling of the new tokens, `flow-lang/Runtime/NoteStreamCompiler.cs` (per-note compile path). Covers TUP-04 + TUP-08. One commit. Depends on 19-01 (NoteElement field additions).

- **D-10:** **Plan 19-03 — Bar-fit validator with charitable overflow + Info diagnostic.**
  Files: `flow-lang/Runtime/NoteStreamCompiler.cs` (`CalculateAutoFitDuration` extension + new `ValidateBarFit` method), `flow-lang/Diagnostics/ErrorReporter.cs` USE only (`ReportInfo` already exists at line 43, no API change). Covers TUP-05. One commit. Depends on 19-01 + 19-02 (needs both bracket-form and per-note durations populated).

- **D-11:** **Plan 19-04 — MIDI export TPQN auto-elevation.**
  Files: `flow-lang/Audio/MidiExport.cs` (pre-export pass over Song collecting denominators, TimeDivision adjustment, delta-time scaling, cap-error path). Covers TUP-06. One commit. Depends on 19-01 (needs TupletElement + TupletRatio fields populated to walk).

- **D-12:** **Plan 19-05 — TUP-07 audit re-validation + closure.**
  Files: NEW `flow-lang.Tests/Unit/Phase19/TupletAugmentDiminishTests.cs` (Facts pinning rational-double/halve), `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (AUDIT-VERIFIED comment update at lines 239,261 with `2026-04-NN: re-validated against tuplet sequences (Phase 19 TUP-07)`), `.planning/phases/19-tuplets-arbitrary-fractional-durations/19-VERIFICATION.md` rollup with criteria-to-artifact mapping + commit hash manifest, REQUIREMENTS.md traceability flips for TUP-01..08, STATE.md advance. Covers TUP-07 + closure. One commit. **Strictly last** — depends on all prior 19-XX commits existing.

- **D-13:** **Wave parallelism:** 19-01 → 19-02 → (19-03 + 19-04 in parallel) → 19-05. Plan 19-03 and 19-04 touch independent files (NoteStreamCompiler vs MidiExport) so they can run concurrently after 19-02 lands.

### Test Strategy

- **D-14:** xUnit Facts under `flow-lang.Tests/Unit/Phase19/` directory (matching v1.2 Phase 14/15/17 convention from D-15 in 14-CONTEXT.md). Filenames: `TupletBracketTests.cs`, `FractionalDurationTests.cs`, `BarFitOverflowTests.cs`, `MidiTpqnElevationTests.cs`, `TupletAugmentDiminishTests.cs`. Plus `.flow` Theory rows in existing harness for end-to-end coverage.

- **D-15:** **Two-pass strict authorship** (Phase 13 D-13 / Phase 14 D-13 precedent) applies to TUP-07. Pass 1 drafts the augment/diminish Facts from REQUIREMENTS.md TUP-07 wording alone (input `[1/12, 1/12, 1/12]` → output `[1/6, 1/6, 1/6]` and `[1/24, 1/24, 1/24]`); Pass 2 lands against real `TransformFunctions` code. If Pass 2 produces unexpected divergence, log under §Divergences in 19-05-PLAN.md. Other plans (19-01..04) use single-pass authorship — the syntax/runtime work has no pre-existing implementation to verify against.

- **D-16:** **Byte-identical determinism regression gate.** Before opening Phase 19 plans, capture baseline `examples/output/flow_tutorial.{wav,mid}` + `examples/output/flow_showcase.{wav,mid}` byte hashes from HEAD (Phase 17 close). After each plan commit, re-run and `cmp` against baseline. Any divergence → STOP and root-cause before proceeding (per v1.2 Phase 15 D-12 precedent + RETROSPECTIVE.md determinism contract lesson).

### Infrastructure Reuse

- **D-17:** `ErrorReporter.ReportInfo(string, SourceLocation?)` already exists at `flow-lang/Diagnostics/ErrorReporter.cs:43` — TUP-05 overflow diagnostic plumbs in directly, no API change.

- **D-18:** `Fraction` struct + `MusicalNoteData.DurationFraction` field arrive from Phase 18. Plans 19-01..05 must verify Phase 18 closed (commit hash check) before their first task.

- **D-19:** Existing `DurationSuffixMap` at `NoteStreamCompiler.cs:29` (`w/h/q/e/s/t`) is reused unchanged. Tuplet brackets and per-note `/X:Y[suffix]` use the same suffix vocabulary for the optional level.

### Claude's Discretion

- Exact field layout of `TupletElement` record (probably: `SourceLocation Location, int Numerator, int? Denominator, IReadOnlyList<NoteStreamElement> Children, string DurationSuffix`) — finalize at plan-time based on parser/compiler ergonomics
- Music21 shorthand lookup table contents (3→3:2, 5→5:4, 6→6:4, 7→7:4, 9→9:8, plus 2/4/8/10/11) — confirm against music21 docs at plan time; SPEC.md says counts 2-11
- TPQN cap `9600` implementation: hard-coded constant in MidiExport.cs vs config-readable — likely hard-coded with TODO for future config-readable if needed
- Whether `19-DISCUSSION-LOG.md` companion file is generated alongside this CONTEXT.md (workflow convention says yes — handled in commit step)
- Per-plan verification gate format (xUnit count + smoke transcripts vs single-Fact pass) — follow Phase 15 / Phase 17 plan templates

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### This phase's requirements
- `.planning/phases/19-tuplets-arbitrary-fractional-durations/19-SPEC.md` — Locked requirements, boundaries, acceptance criteria. MUST read before planning.

### Milestone-level
- `.planning/REQUIREMENTS.md` — v1.3 milestone requirements with traceability table (TUP-01..08 mapped to Phase 19; D-01..D-09 milestone-level locked decisions)
- `.planning/ROADMAP.md` §Phase 19 — phase goal + success criteria + plan list
- `.planning/research/SUMMARY.md` — v1.3 research synthesis (5 binding pre-orderings, zero new deps)
- `.planning/research/STACK.md` — Hand-rolled `Fraction` struct rationale; zero NuGet additions
- `.planning/research/FEATURES.md` — Tuplet syntax conventions across Lilypond/ABC/music21
- `.planning/research/ARCHITECTURE.md` — Integration map; recursive `Fraction outerScale` propagation pattern
- `.planning/research/PITFALLS.md` — Pitfall 1 (floating-point drift), Pitfall 2 (bar validation breakage), Pitfall 3 (MIDI TPQN insufficient), Pitfall 9 (AUDIT-VERIFIED C5 risk)

### Project-level
- `CLAUDE.md` — Architecture summary; charitable-interpretation memory ("music > rigid correctness") binding for D-03
- `.planning/PROJECT.md` — Minimal-deps philosophy (zero new external dependencies for v1.3)
- `.planning/RETROSPECTIVE.md` §v1.2 — Lessons #1 (audit-spike-as-its-own-phase), #3 (determinism contracts only hold if every PRNG is seeded — applies analogously to "every duration computation must be rational")

### Codebase grounding
- `flow-lang/Runtime/NoteStreamCompiler.cs` — Bar-fit / auto-fit duration logic (target for TUP-05)
- `flow-lang/Ast/Expressions/NoteStreamExpression.cs` — `NoteStreamElement` discriminated union (target for TUP-01 and TUP-08)
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:211` — `MusicalNoteData` (gains `DurationFraction` from Phase 18; consumed here)
- `flow-lang/Diagnostics/ErrorReporter.cs:43` — `ReportInfo` API used by TUP-05 overflow diagnostic
- `flow-lang/Audio/MidiExport.cs` — TPQN target for TUP-06 (DryWetMidi `MidiFile.TimeDivision`)
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:239,261` — AUDIT-VERIFIED C5 markers (target for TUP-07 update)

### Prior phase precedents
- `.planning/phases/14-composer-dx-part-1/14-CONTEXT.md` — Plan structure pattern (D-16..D-20), pre-landing collision grep recipe (D-21)
- `.planning/phases/15-composer-dx-part-2/15-CONTEXT.md` — Two-pass strict authorship pattern (D-13)
- `.planning/phases/13-nyquist-validation-backfill/` — Phase-scoped Integration Facts directory convention (`flow-lang.Tests/Unit/Phase{NN}/`)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`ErrorReporter.ReportInfo`** at `flow-lang/Diagnostics/ErrorReporter.cs:43` — already supports `(string message, SourceLocation? location)`. TUP-05 charitable-truncate Info diagnostic plumbs in without API change.
- **`DurationSuffixMap`** at `flow-lang/Runtime/NoteStreamCompiler.cs:29` — `w/h/q/e/s/t` lookup table reused for both bracket-form `{N:M ...}q` and per-note `/X:Y[suffix]` lookup.
- **`NoteValueType.ToFraction`** at NoteType.cs — converts existing enum durations to fractions (already returns `double` today; Phase 18 likely converts/extends to return `Fraction`).
- **`MusicalNoteData` constructor** at `NoteType.cs:234` — already has 12 parameters; Phase 18 will add `DurationFraction`. Per-note `TupletRatio` field needs adding here too in Plan 19-01 OR 19-02 (preferred: 19-02 alongside the `/X:Y` lexer work since they're tightly coupled).
- **DryWetMidi 8.0.3** — `MidiFile.TimeDivision` is the SMF-header TPQN field; setter is the integration point for TUP-06.

### Established Patterns

- **AST is records, immutable** (`NoteStreamElement` is `abstract record`). `TupletElement` follows the same pattern.
- **Pattern matching switch dispatch** in compiler over discriminated union — `NoteStreamCompiler.CompileBar` already switches over 9 element types; gains a 10th case for `TupletElement` (or possibly stays at 9 if we extend `NoteElement` with `TupletRatio` rather than adding a new type — only `TupletElement` is genuinely new for the bracket form).
- **Auto-fit divides bar evenly** at NoteStreamCompiler.cs:206 — TUP-05 extends this pathway with rational-sum validation. Existing `double`-based math is preserved when no tuplets/fractions present (zero-disruption; Phase 18 path).
- **Pre-landing collision grep** as plan-time one-shot (Phase 14 D-21). Plan 19-01 follows this pattern for `{` `}` token introduction.
- **Two-pass strict authorship** for backfill/regression-pin Facts (Phase 13 D-13, Phase 14 D-13). Applies to TUP-07.
- **Phase-scoped Integration test directories** at `flow-lang.Tests/Unit/Phase{NN}/` (Phase 13 onward).

### Integration Points

- **`SongRenderer`** consumes `MusicalNoteData.GetBeats(int timeSigDenominator)` to compute frame counts (NoteType.cs:253). Extension: when `DurationFraction` is non-null, `GetBeats` returns `DurationFraction × timeSigDenominator` (Phase 18 work). Phase 19 doesn't touch `SongRenderer` — beat math flows through automatically.
- **`writeMidi` / `writeWav`** call sites unchanged. TUP-06 lives entirely inside `MidiExport.cs` pre-export pass.
- **REPL + watch mode** unaffected — note-stream syntax extensions parse cleanly when not present (existing tests stay byte-identical).
- **flow-lsp** — existing parser-error diagnostics handle incomplete tuplet typing gracefully (Phase 17 graceful-degradation pattern). Semantic-tokens won't recognize `{` `}` initially — explicit Phase 19 out-of-scope; LSP follow-up phase if user feedback warrants.

</code_context>

<specifics>
## Specific Ideas

- **D-09 (decision provenance):** TUP-08 (`C4/X:Y[suffix]` per-note shorthand) was added during the SPEC.md authoring round in response to user's "Can we have a variable duration suffix? like x:y" message. User confirmed Option B (`C4/X:Y` per-note) over alternatives (A: trailing `C4q D4 N:M`, C: Lilypond `C4q*N/M`). Mixed adjacent ratios (`C4/3:2 D4/5:4 E4/3:2`) are explicitly legal — independent per-note semantics, no consecutive-grouping rule.
- **Charitable bar overflow (D-03):** User explicitly chose silent-truncate + Info diagnostic over hard error, per CLAUDE.md memory. The truncation algorithm trims the boundary-crossing element rather than dropping it entirely — preserves user-typed leading content.
- **Music21 alignment:** TUP-02's shorthand convention table (`{3 ...}q ≡ {3:2 ...}q`) follows music21's standard; researcher's FEATURES.md cited the canonical mapping at `music21/duration.py`.
- **TPQN cap rationale (D-05 milestone-level + D-06 phase-level):** 9600 chosen because no DAW imports correctly above this in field testing (per researcher SUMMARY.md). 32767 is the SMF spec hard limit. 1920 was rejected because it blocks 7:N, 11:N, 13:N — viable composer expressions.

</specifics>

<deferred>
## Deferred Ideas

None surfaced during this discussion that aren't already deferred to other v1.3 phases or to v1.4. Specifically:

- **LSP semantic-tokens for `{N:M ...}` syntax** — out of scope for Phase 19; flow-lsp gracefully degrades on unknown tokens (Phase 17 pattern). Possible follow-up phase if composer feedback warrants.
- **Tuplet visualization in console-based ASCII piano-roll** — out of scope; existing piano-roll renders the rational durations as fractional cell counts, sufficient for v1.3.
- **Tuplet-aware `humanize` / `humanizeGaussian` interaction** — Gaussian humanize lands in Phase 25 (DEFER-06). When it lands, behavior on tuplet sequences should be tested but is not Phase 19's responsibility.
- **WAV export TPQN equivalent** — WAV is sample-based, not tick-based; no analogous concept. Tuplet rendering reaches WAV via the existing `MusicalNoteData.GetBeats` path (Phase 18 extension to handle Fraction).

</deferred>

---

*Phase: 19-tuplets-arbitrary-fractional-durations*
*Context gathered: 2026-04-26*
*Next step: /gsd-plan-phase 19 — break 8 requirements into 5 plans across 3 waves*
