# Phase 19: Tuplets & Arbitrary Fractional Durations — Research

**Researched:** 2026-04-26
**Domain:** DSL syntax extension — bracket-form `{N:M ...}` tuplets + arbitrary `C4/N` and `C4/X:Y` per-note fractional duration syntax in note-stream context, with rational `Fraction` propagation through `NoteStreamCompiler`, MIDI TPQN auto-elevation, charitable bar-overflow, and AUDIT-VERIFIED C5 re-validation.
**Confidence:** HIGH — every load-bearing claim is grounded in directly-read source files (commit ba8534a HEAD); SPEC + CONTEXT are LOCKED so research focuses exclusively on HOW.

## Summary

Phase 19 lands the lead capability of v1.3 on top of Phase 18's foundation (`Fraction` struct + `MusicalNoteData.DurationFraction` field, both DORMANT until this phase activates them). The work splits cleanly along five existing chokepoints in the codebase:

1. **`SimpleLexer.cs`** — `{` and `}` already tokenize as `TokenType.LBrace`/`TokenType.RBrace` (line 111-112). Note-stream context dispatch happens at the **parser** level, not the lexer — `ParseNoteStream` already has a working pattern (Pipe / LParen / LBracket / NoteLiteral) that makes adding `{` a single-arm extension. The note-name-with-fractional-suffix lexer state extension is the only genuine lexer-level work, and it lives inside the existing `ScanNumberOrSpecialLiteral`/`ScanIdentifierOrKeyword` rewind-by-one trick already established at line 660.
2. **`Parser.NoteStream.cs`** — single new dispatch arm on `LBrace` plus `TryParseDurationSuffix` extension to accept `/N` and `/X:Y` forms.
3. **`NoteStreamCompiler.cs`** — recursive `CompileTupletElement(TupletElement, ..., Fraction outerScale)` propagating `Fraction` multiplicatively through nested children. Single new method; existing per-element compile paths reused. Bar-fit validator extends `CalculateAutoFitDuration` with rational-sum tracking and an `Overflow` truncation branch emitting `ErrorReporter.ReportInfo`.
4. **`MidiExport.cs`** — single pre-export pass over the Song collecting tuplet denominators, computing `LCM(480, 2 × union(denoms))`, capped at 9600. `TicksPerQuarterNote` const at line 17 becomes a per-export computed value.
5. **`TransformFunctions.cs:239,261`** — `Augment`/`Diminish` gain a tuplet-aware branch (when `note.DurationFraction.HasValue`, double or halve the rational) and the AUDIT-VERIFIED comment is refreshed. New regression Facts pin the rational doubling/halving.

The recursive `Fraction outerScale` propagation pattern (RESEARCH/ARCHITECTURE.md §2) is the single load-bearing design choice — it localises tuplet scaling state in the recursive descent and avoids parallel "are we inside a tuplet?" stacks in every consumer.

**Primary recommendation:** Land plans in the order locked by CONTEXT D-08..D-13 (19-01 bracket AST+parser+compiler → 19-02 lexer per-note `/N`/`/X:Y` → 19-03 + 19-04 in parallel → 19-05 closure). Treat the Phase 18 dormancy contract as inviolate — the byte-identical determinism gate already shipped in Phase 18 (4 integration Facts at `flow-lang.Tests/Integration/Phase18/ByteIdentical{Tutorial,Showcase}Tests.cs`) is the test that proves Phase 19 didn't silently drift any non-tuplet output. Run that gate after every plan commit.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions (verbatim from CONTEXT.md)

**TUP-08 — Per-note shorthand AST shape**
- **D-01:** `C4/X:Y[suffix]` extends the existing `NoteElement` record with an optional `(int Num, int Denom)? TupletRatio` field. No new AST record type. Compiler treats per-note tuplet-ratio'd notes as synthetic 1-element tuplet members at compile time, computing `DurationFraction = (suffix_fraction) / Num` of a whole. The `Denom` is preserved on the `MusicalNoteData` (new field on `MusicalNoteData` if not already present from FRAC-02) for MIDI TPQN computation but does NOT enter the per-note duration math.
- **D-02:** Per-note instances are independent — no implicit grouping, no consecutive-must-match rule. `| C4/3:2 D4/5:4 E4/3:2 |` is legal.

**TUP-05 — Bar overflow truncation algorithm**
- **D-03:** When the rational sum of bar element durations exceeds the time-signature value, the bar-fit validator **truncates the boundary-crossing element's duration** to fit, then drops all subsequent elements. Algorithm: walk elements left-to-right accumulating `Fraction sum`. When `sum + element.duration > timesig`, set the element's effective `DurationFraction = timesig - sum`, accumulate to the boundary, then drop remaining elements. Emit `ErrorReporter.ReportInfo` once per overflowing bar.
- **D-04:** Tuplet brackets WITHOUT explicit duration suffix raise a parse error (`"Tuplet bracket requires explicit duration suffix"`).

**TUP-06 — MIDI TPQN auto-elevation**
- **D-05:** TPQN computation lives in **MidiExport.cs as a single pre-export pass over the Song**. Walk all `MusicalNoteData` collecting `union(tuplet_denominators)` (drawn from `DurationFraction.Denominator` AND `TupletRatio.Numerator` when present). Compute `requiredTPQN = LCM(480, 2 × union)`. Set `DryWetMidi.MidiFile.TimeDivision` to `requiredTPQN`. Scale all delta-times by `requiredTPQN / 480`.
- **D-06:** TPQN-cap error message: `"MIDI export requires TPQN={requiredTPQN}, exceeds cap 9600 (locked v1.3 D-05). Tuplet ratios in this song: [{sorted_unique_X:Y_list}]"`.
- **D-07:** When NO tuplets present, TPQN stays at 480.

**Plan Structure (5 plans, wave-parallel)**
- **D-08:** Plan 19-01 — Tuplet bracket AST + parser + compiler (TUP-01..03)
- **D-09:** Plan 19-02 — Lexer support for `/N` and `/X:Y[suffix]` (TUP-04 + TUP-08)
- **D-10:** Plan 19-03 — Bar-fit validator with charitable overflow + Info diagnostic (TUP-05)
- **D-11:** Plan 19-04 — MIDI export TPQN auto-elevation (TUP-06)
- **D-12:** Plan 19-05 — TUP-07 audit re-validation + closure
- **D-13:** Wave parallelism — 19-01 → 19-02 → (19-03 ‖ 19-04) → 19-05

**Test Strategy**
- **D-14:** xUnit Facts under `flow-lang.Tests/Unit/Phase19/` (mirrors Phase 13/14/15/17/18 convention).
- **D-15:** Two-pass strict authorship applies to TUP-07 only.
- **D-16:** Byte-identical determinism regression gate via `cmp` against tutorial.flow + showcase.flow baselines after each plan commit.

**Infrastructure Reuse**
- **D-17:** `ErrorReporter.ReportInfo(string, SourceLocation?)` already exists at `flow-lang/Diagnostics/ErrorReporter.cs:43` (verified — see §Codebase Grounding below).
- **D-18:** Phase 18 closed at commit `ba8534a` — Plans 19-01..05 must verify this is HEAD's ancestor before starting.
- **D-19:** Existing `DurationSuffixMap` at `NoteStreamCompiler.cs:29` (`w/h/q/e/s/t`) reused unchanged.

### Claude's Discretion (verbatim from CONTEXT.md)

- Exact field layout of `TupletElement` record (probably: `SourceLocation Location, int Numerator, int? Denominator, IReadOnlyList<NoteStreamElement> Children, string DurationSuffix`)
- Music21 shorthand lookup table contents (3→3:2, 5→5:4, 6→6:4, 7→7:4, 9→9:8, plus 2/4/8/10/11)
- TPQN cap `9600` implementation: hard-coded constant in MidiExport.cs vs config-readable
- Whether `19-DISCUSSION-LOG.md` companion file is generated alongside CONTEXT.md (yes — already shipped)
- Per-plan verification gate format

### Deferred Ideas — OUT OF SCOPE (verbatim from CONTEXT.md)

- LSP semantic-tokens for `{N:M ...}` syntax — flow-lsp gracefully degrades on unknown tokens
- Tuplet visualization in console-based ASCII piano-roll
- Tuplet-aware `humanize` / `humanizeGaussian` interaction (Phase 25 territory)
- WAV export TPQN equivalent (no analogous concept)
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TUP-01 | `{N:M ...}q` bracket compiles to recursive `TupletElement` AST → MusicalNoteData with rational `DurationFraction` | §1 AST shape, §2 parser dispatch on LBrace, §3 NoteStreamCompiler recursive `Fraction outerScale` |
| TUP-02 | `{N ...}q` shorthand defaults to music21 conventions (3→3:2, 5→5:4, 6→6:4, 7→7:4, 9→9:8 etc., counts 2-11) | §2 parser shorthand lookup; bounds-check raises `"…lookup-table bounds…"` on count ≥ 12 |
| TUP-03 | Nested tuplets compose via accumulating `Fraction outerScale` | §3 multiplicative propagation; pinned acceptance: 5-element [1/6, 1/18, 1/18, 1/18, 1/6] |
| TUP-04 | `C4/N` arbitrary fractional duration; `/0` parse error; `/1` whole note | §5 lexer rewind-and-extend pattern (mirrors duration-suffix split at SimpleLexer.cs:660); compiler computes `Fraction(1, N)` |
| TUP-05 | Bar-fit validator handles tuplet/fractional sums; overflow truncates + ReportInfo; tuplet-no-suffix is parse error | §7 algorithm; reuses `ErrorReporter.ReportInfo` at line 43 |
| TUP-06 | MIDI export auto-elevates TPQN = LCM(480, 2×union); cap 9600; clear error above | §6 single pre-export Song walk; `MidiFile.TimeDivision` setter; `(beats × TPQN)` scaling preserved |
| TUP-07 | AUDIT-VERIFIED C5 re-validated against tuplet sequences via augment/diminish Facts | §8 dual-path branching at `TransformFunctions.cs:239,261`; rational doubling/halving |
| TUP-08 | Per-note `C4/X:Y[suffix]` shorthand; mixed ratios legal; same TPQN path as bracket form | §5 lexer state machine; CONTEXT D-01 NoteElement extension |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

These directives have the same authority as locked decisions and any plan must comply:

- **Runtime**: .NET 10 — all code targets `net10.0` (xUnit Facts in `flow-lang.Tests/` already use this).
- **Charitable interpretation memory**: "music > rigid correctness" — directly drives D-03 silent-truncate-with-Info bar overflow. The .planning/MEMORY referenced in CLAUDE.md mandates: "Prefer silent-and-documented assumptions over errors." Bar overflow is the canonical example.
- **No new external dependencies**: zero NuGet additions in Phase 19 — `Fraction` already shipped from Phase 18; DryWetMidi 8.0.3 already pinned; no Box-Muller, no SoundTouch, no NWaves.
- **AST is records, immutable** — `TupletElement` follows the same pattern as the existing 9 `NoteStreamElement` records in `flow-lang/Ast/Expressions/NoteStreamExpression.cs`.
- **Pattern matching switch dispatch** — `NoteStreamCompiler.CompileBar` (line 75-138) already switches over the 9 element types via C# switch expression. Extending to `TupletElement` is a 10th case.
- **Existing power-of-2 path stays unchanged when DurationFraction is null** — Phase 18's invariant. Verified structurally: `MusicalNoteData.GetBeats` branches at TOP on `DurationFraction.HasValue` (NoteType.cs:268), with the existing power-of-2 path preserved verbatim below.
- **Static typing with type inference for some contexts** — Phase 19 introduces no new user-facing types; `Fraction` and `TupletRatio` stay C#-internal helpers.
- **Hand-rolled, no Pidgin** — manual recursive descent extends naturally; no parser-combinator library involvement.

## Architectural Responsibility Map

Phase 19 is single-tier (interpreter + audio pipeline). The "tier" mapping for this codebase distinguishes pipeline stages, not client/server:

| Capability | Primary Stage | Secondary Stage | Rationale |
|------------|--------------|------------------|-----------|
| `{N:M ...}q` token recognition | Lexer (SimpleLexer.cs) | — | `{`/`}` already tokenize as LBrace/RBrace at line 111-112; no lexer change needed for bracket form |
| `{N:M ...}` parser dispatch | Parser.NoteStream.cs | AST/NoteStreamExpression.cs | New dispatch arm on LBrace inside note-stream loop; new `TupletElement` record |
| `C4/N` and `C4/X:Y` token recognition | Lexer (SimpleLexer.cs) | — | Lexer rewind-and-extend mirrors the existing duration-suffix split at line 660 (note + `/N` becomes two tokens via position rewind) |
| Tuplet duration math | NoteStreamCompiler | TypeSystem/Fraction.cs | Single new method `CompileTupletElement(...,Fraction outerScale)`; consumes Phase 18 `Fraction` arithmetic primitive |
| Per-note tuplet ratio | NoteStreamCompiler | Ast.NoteElement | Synthetic 1-element tuplet at compile time per CONTEXT D-01 — no new code path |
| Bar-fit overflow | NoteStreamCompiler | Diagnostics/ErrorReporter | Extend `CalculateAutoFitDuration` flow; emit `ReportInfo` (line 43) |
| MIDI TPQN auto-elevation | StandardLibrary/Audio/MidiExport.cs | DryWetMidi `MidiFile.TimeDivision` | Pre-export Song walk + LCM math; `TicksPerQuarterNote` const becomes per-call computed |
| Augment/diminish on tuplets | StandardLibrary/Transforms/TransformFunctions.cs | TypeSystem/Fraction.cs | Branch on `note.DurationFraction.HasValue` — rational doubling/halving via `Fraction(2,1)` * f or `Fraction(1,2)` * f |
| Audio render of tuplet beats | (no Phase 19 work) | SongRenderer/BarRenderer | Already calls `MusicalNoteData.GetBeats(int)` (NoteType.cs:266) which branches on Fraction since Phase 18 — beats math flows through automatically |

**Critical:** every consumer of `MusicalNoteData.GetBeats` (BarRenderer:51,260; MidiExport:184,195,216,259; BarType:141,152,169 per Phase 18 SUMMARY) sees correct beats automatically once `DurationFraction` is non-null. Phase 19 only writes to that field; nothing else needs adapting at the audio-rendering tier. This is exactly why FRAC-01/FRAC-02 were the load-bearing prerequisite.

## Standard Stack

### Core (no additions in Phase 19; all already shipped)

| Library | Version | Purpose | Status |
|---------|---------|---------|--------|
| .NET 10 | net10.0 | Runtime | [VERIFIED: existing csproj] — Phase 18 ran 306/306 GREEN here |
| C# 13 | Latest | Records, pattern matching, file-scoped namespaces | [VERIFIED: existing patterns at NoteStreamExpression.cs:9,15] |
| `FlowLang.TypeSystem.Fraction` | Phase 18 | Rational arithmetic primitive (Num/Denom int) with +/*/</> ops, GCD-normalising ctor | [VERIFIED: commit 2092f32 at flow-lang/TypeSystem/Fraction.cs] |
| `MusicalNoteData.DurationFraction` | Phase 18 | Optional `Fraction?` field on every note, dormant until Phase 19 | [VERIFIED: commit ba8534a at NoteType.cs:244] |
| Melanchall.DryWetMidi | 8.0.3 | MIDI file write incl. `MidiFile.TimeDivision = new TicksPerQuarterNoteTimeDivision(int)` | [VERIFIED: MidiExport.cs:88] |
| xUnit.v3 + xunit.runner.visualstudio | 3.2.2 / 3.1.5 | Test framework (Phase 18 conventions) | [VERIFIED: existing flow-lang.Tests csproj] |

### No New Dependencies

Per CLAUDE.md "No new external dependencies" + .planning/PROJECT.md minimal-deps philosophy — Phase 19 ships zero NuGet additions. `Fraction` is hand-rolled (Phase 18). LCM/GCD math reuses the Euclidean GCD idiom from `Fraction.cs:39` (verified — same algorithm as `PolyrhythmFunctions.cs:117`).

### Alternatives Considered

| Instead of | Could Use | Tradeoff | Decision |
|------------|-----------|----------|----------|
| Hand-rolled LCM | `System.Numerics.BigInteger.GreatestCommonDivisor` | Pulls a heavy `BigInteger` for what is `int` math (denominators stay ≤ 13 per D-05 cap) | [DECIDED] hand-roll; sit it inside `MidiExport.cs` as a private static helper. Mirrors `Fraction.Gcd` style. |
| Recursive AST `TupletElement` | Flat tuplet-start/tuplet-end pseudo-elements | Forces every consumer to maintain a parallel "in tuplet?" stack | [DECIDED per ARCHITECTURE.md §2] recursive AST localises state inside `CompileTupletElement` |
| Extending `NoteValueType` enum with `TUPLET_THIRD` etc. | — | Doesn't compose under nesting; explodes combinatorially with dotted-tuplets | [REJECTED per ARCHITECTURE.md §3 Anti-Pattern 1 + Pitfall 1] |

## Architecture Patterns

### System Architecture Diagram

```
.flow source with tuplet syntax  (e.g.  | {3:2 C4 D4 E4}q |)
         │
         ▼
SimpleLexer  ───────────────────────►  Token[]
   │  • { → LBrace (existing line 111)
   │  • } → RBrace (existing line 112)
   │  • C4/12 → NoteLiteral + Slash + IntLiteral  (NEW: rewind-extend)
   │  • C4/3:2q → NoteLiteral + Slash + IntLiteral + Colon + IntLiteral + Identifier(q)
   │
   ▼
Parser.NoteStream  ─────────────────►  NoteStreamExpression
   │  ParseNoteStream() loop:
   │    NEW arm: if LBrace → ParseTupletBracket() → TupletElement
   │    EXISTING arm extended: NoteLiteral → also try /N or /X:Y suffix
   │
   ▼
NoteStreamCompiler  ────────────────►  SequenceData (BarData[] containing MusicalNoteData[])
   │  CompileBar switch dispatch:
   │    NEW case TupletElement → CompileTupletElement(elem, autoFit, ctx, exec, output, Fraction.One)
   │      └─ recurses into children with scale *= Fraction(Denominator, Numerator)
   │      └─ leaf NoteElement → MusicalNoteData with DurationFraction = scale × suffixFraction
   │  CalculateAutoFitDuration extended:
   │    NEW: walks elements with Fraction running sum; overflow → truncate boundary + ReportInfo
   │
   ▼
SongRenderer / BarRenderer  ─────────►  AudioBuffer
   │  Already calls MusicalNoteData.GetBeats(int) which branches on DurationFraction.HasValue
   │  (NoteType.cs:266 — Phase 18 wiring) — no Phase 19 change needed.
   │
   ▼
WAV (correct because GetBeats already branches on Fraction)
   ┊
   └────────────►  MidiExport (writeMidi)
                       │
                       ▼ NEW pre-export pass:
                       │   1. Walk every MusicalNoteData in song.SectionRegistry
                       │   2. Collect denominators from {DurationFraction.Denom, TupletRatio.Num}
                       │   3. requiredTPQN = LCM(480, 2 × union)
                       │   4. if requiredTPQN > 9600 → throw with locked message format
                       │   5. else MidiFile.TimeDivision = TicksPerQuarterNoteTimeDivision(requiredTPQN)
                       │   6. scale all (beats × TicksPerQuarterNote) sites by requiredTPQN/480
                       │
                       ▼
                       MIDI file with correct tuplet ticks (drift-free for ratios within cap)
```

### Recommended Project Structure (additions only)

```
flow-lang/
├── Ast/Expressions/
│   └── NoteStreamExpression.cs       # +TupletElement record (~12 lines), +TupletRatio field on NoteElement
├── Lexing/
│   └── SimpleLexer.cs                # extended note-literal scanner: /N + /X:Y[suffix] handling
├── Parsing/
│   └── Parser.NoteStream.cs          # +ParseTupletBracket(), +/N + /X:Y in TryParseDurationSuffix
├── Runtime/
│   └── NoteStreamCompiler.cs         # +CompileTupletElement(), +ValidateBarFit(), extended CalculateAutoFitDuration
├── StandardLibrary/Audio/
│   └── MidiExport.cs                 # TicksPerQuarterNote becomes per-export computed; +CollectDenominators + LCM
└── StandardLibrary/Transforms/
    └── TransformFunctions.cs         # Augment/Diminish branch on DurationFraction; AUDIT-VERIFIED comment refresh

flow-lang.Tests/
├── Unit/Phase19/                     # NEW directory (mirrors Phase18 layout)
│   ├── TupletBracketTests.cs         # TUP-01..03 + TUP-02 lookup table bounds
│   ├── FractionalDurationTests.cs    # TUP-04 (/N parsing) + TUP-08 (/X:Y per-note)
│   ├── BarFitOverflowTests.cs        # TUP-05 charitable truncate + ReportInfo
│   ├── MidiTpqnElevationTests.cs     # TUP-06 (3:2→480, 5:4→480, 7:8→3360, 11:13→cap error)
│   └── TupletAugmentDiminishTests.cs # TUP-07 rational doubling/halving Facts
└── Integration/Phase19/              # if needed for end-to-end .flow scripts (D-USER convention TBD at plan time)
```

### Pattern 1: TupletElement record

**What:** New `NoteStreamElement` subclass holding tuplet ratio + recursive children.
**When to use:** Bracket-form tuplets `{N:M ...}q` and (per CONTEXT D-01) treated as the synthesis target for per-note `C4/X:Y` shorthand.

```csharp
// flow-lang/Ast/Expressions/NoteStreamExpression.cs (ADD; mirrors existing 9 records)
public record TupletElement(
    SourceLocation Location,
    int Numerator,                              // "3" in 3:2 (or in shorthand {3 ...})
    int Denominator,                            // "2" in 3:2 (NULL-able only for shorthand pre-resolution; resolved at parse-time)
    IReadOnlyList<NoteStreamElement> Children,  // recursive; can contain another TupletElement
    string DurationSuffix,                      // "q" / "h" / etc. — REQUIRED per D-04
    bool IsDotted                               // outer dot ((3:2 ...)q.)
) : NoteStreamElement(Location);

// EXTEND NoteElement with optional per-note tuplet ratio (CONTEXT D-01)
public record NoteElement(
    SourceLocation Location,
    string NoteName,
    string? DurationSuffix,
    bool IsDotted,
    bool IsTied,
    double? CentOffset,
    double? Velocity,
    Articulation? ArticulationMark,
    (int Num, int Denom)? TupletRatio = null    // NEW — defaulted to null; existing call sites compile unchanged
) : NoteStreamElement(Location);
```

**Critical:** `Denominator` resolves at parse-time from the music21 lookup table when the user writes `{3 ...}` (shorthand). The AST stores the resolved integer pair, never a "shorthand?" flag — keeps the compiler simple.

### Pattern 2: Recursive `Fraction outerScale` propagation

**What:** Single new compiler method `CompileTupletElement` consumes accumulating scale.
**When to use:** When dispatching on `TupletElement` in the `CompileBar` switch (line 75-138).

```csharp
// flow-lang/Runtime/NoteStreamCompiler.cs (ADD)
private void CompileTupletElement(
    TupletElement tuplet,
    NoteValueType.Value? autoFitDuration,    // unused inside tuplet — auto-fit forbidden per D-04
    MusicalContext context,
    ExecutionContext? execCtx,
    List<MusicalNoteData> output,
    Fraction outerScale)                      // accumulator from ancestor TupletElements
{
    // Each child duration is multiplied by Denominator/Numerator (3:2 = play 3 in time of 2 = each is 2/3 size)
    var scale = outerScale * new Fraction(tuplet.Denominator, tuplet.Numerator);

    // Resolve outer suffix to a Fraction-of-whole
    var suffixFrac = SuffixToFraction(tuplet.DurationSuffix, tuplet.IsDotted);
    var groupFrac = suffixFrac;  // total wall-clock of all children combined

    foreach (var child in tuplet.Children)
    {
        switch (child)
        {
            case TupletElement nested:
                CompileTupletElement(nested, null, context, execCtx, output, scale);
                break;
            case NoteElement n:
                // each leaf gets DurationFraction = groupFrac × scale
                // (= one slot of the tuplet, in quarter-note units)
                var f = new Fraction(groupFrac.Num, groupFrac.Denom) * scale;
                // …convert f from "whole-note units" to "quarter-note units" by × 4
                var qf = new Fraction(f.Num * 4, f.Denom);
                var (name, oct, alt) = NoteType.Parse(n.NoteName);
                output.Add(new MusicalNoteData(name, oct, alt, /* DurationValue */ null,
                    isRest: false, /* … */, durationFraction: qf));
                break;
            // RestElement, ChordElement, NamedChord, Roman, … same treatment with isRest/multi-note as appropriate
        }
    }
}
```

**Plan-time finalisation:** the exact numerator-denominator factor depends on whether `DurationFraction` is in **whole-note units** (RESEARCH/ARCHITECTURE.md §3) or **quarter-note units** (Phase 18 SUMMARY 18-02 says `(double)f.Num × timeSigDenominator / (f.Denom × 4.0)` ⇒ quarter-note units, i.e. `f = 1/3` means "1/3 quarter"). **VERIFIED** at NoteType.cs:268 — quarter-note units. SPEC.md TUP-01 acceptance ("DurationFraction = 1/12 whole" = "1/3 quarter") is consistent. Plan 19-01 should use quarter-note units throughout to match Phase 18 wiring; the SPEC's "whole-note" prose is mathematically equivalent (1/12 whole = 1/3 quarter in 4/4 — `MusicalNoteData.GetBeats(4) = 1/3 × 4 / 4 = 1/3 beats`).

### Pattern 3: Lexer rewind-and-extend for `/N` + `/X:Y[suffix]`

**What:** Inside `ScanIdentifierOrKeyword` (`SimpleLexer.cs` line 689), after a successful note-name parse, peek for `/`. The existing duration-suffix split at line 660 (`_position--; _column--; return new Token(TokenType.NoteLiteral, …)`) is the precedent — same trick applied to the slash.
**When to use:** Note-stream context — but the lexer is mode-less. The disambiguation lives in the **parser** (note-stream context vs expression context); the lexer just emits tokens. Two options:

1. **Pure parser-side dispatch** (RECOMMENDED): lexer emits `NoteLiteral`, then `Slash`, then `IntLiteral`, then optionally `Colon` + `IntLiteral`, then optionally `Identifier(q)`. Parser inside `ParseNoteStream` peeks `Slash IntLiteral` after a `NoteLiteral` to construct `TupletRatio` / fractional-duration. Outside note streams, `C4 / 12` is already an existing `NoteLiteral Slash IntLiteral` triple — Phase 19 doesn't change this — so expression-context parsing of `C4 / 12` continues to mean "binary subtraction" of a note from 12 if it ever did. Spot-check confirmed: `tests/test_arithmetic.flow` has no such pattern (collision grep section below).
2. **Note-stream pseudo-mode in lexer**: not recommended — `Pipe` `|` toggling adds a new lexer state field; the existing parser dispatch is sufficient.

**Decision:** Plan 19-02 adopts option 1. Existing note-stream tokens already parse `C4q` via lexer rewind (line 660); the `/N` form is the parser's responsibility once tokens are split.

### Anti-Patterns to Avoid

(All cribbed from `.planning/research/ARCHITECTURE.md` §9 and `PITFALLS.md` §1-9 — re-iterated here for plan-time discoverability.)

- **Extending `NoteValueType` enum with tuplet slots** — doesn't compose, explodes combinatorially.
- **`double` for tuplet duration scaling** — accumulates ε per note; breaks Phase 18 byte-identical-determinism gate; breaks MIDI tick math at 7-tuplets; FORBIDDEN.
- **Storing `outerScale` on `MusicalContext` stack** — pollutes a runtime-context type with a parse-time concern; localise to `CompileTupletElement` parameter.
- **Hand-rolled tuplet bracket parser as a separate file** — keep it inside `Parser.NoteStream.cs` (single arm in the existing dispatch loop) per existing convention.
- **Dropping the auto-elevation cap in MIDI export** — removes the user-facing error path for `{11:13}` and produces undecoded files in DAWs.
- **Updating AUDIT-VERIFIED comment date in TUP-07 without authoring a new regression Fact** — silent invalidation per Pitfall 9; plan 19-05 must pair the comment update with new Facts.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Rational arithmetic | New `Rational` struct | **Phase 18 `Fraction`** at `flow-lang/TypeSystem/Fraction.cs` | Already shipped, normalising, value-equal, has `+ * < >` operators (`Fraction.cs:41-50`); zero new code |
| Storing rational duration on a note | New per-note duration class | **Phase 18 `MusicalNoteData.DurationFraction`** at `NoteType.cs:244` | Already shipped, defaulted-null, `GetBeats` already branches on it (`NoteType.cs:268`) |
| Info-severity diagnostic | New diagnostic level | **`ErrorReporter.ReportInfo(string, SourceLocation?)`** at `Diagnostics/ErrorReporter.cs:43` | Verified — already exists with the exact signature TUP-05 needs |
| Music21 shorthand lookup table | New TupletConvention enum | **Hard-coded `Dictionary<int, int>` inline** at the parser arm | Six entries (3,5,6,7,9 plus 2,4,8,10,11); flat data; SPEC.md §requirements TUP-02 names them |
| MIDI tick math | Recompute in NoteStreamCompiler too | **Reuse existing `(beats × TicksPerQuarterNote)` formula** at `MidiExport.cs:184,195,216,259` | Just replace constant `480` with per-export computed value; 4 sites, mechanical |
| Recursive Euclidean GCD | New file | **`Fraction.cs:39`** has it (`b == 0 ? a : Gcd(b, a % b)`) | Make a private static `Lcm` helper next to its sole caller in MidiExport.cs |
| LCM | Imported library | **`Lcm(a,b) = a × b / Gcd(a,b)`** | Two-line helper next to GCD; same idiom |

**Key insight:** Phase 18 was deliberately structured as a foundation phase (D-USER-04 dormancy). Phase 19's job is to **activate** that foundation — almost every "what should we use?" question has the same answer: "the Phase 18 thing." The risk is over-engineering by introducing parallel structures.

## Runtime State Inventory

> Phase 19 is greenfield syntax + new compiler paths. No rename, no migration, no string-replacement. Inventory not applicable. (For completeness: zero stored data, zero live-service config, zero OS-registered state, zero secrets/env-var renames, zero stale build artifacts to clean.)

## Common Pitfalls

(Numbered to match `.planning/research/PITFALLS.md` for cross-reference.)

### Pitfall 1: Floating-point drift in tuplet duration math

**What goes wrong:** Multiplying triplet 1/3 in `double` accumulates ε per note; bar validation false-positives; MIDI tick conversion drops a tick.
**Why it happens:** Triplet ratios are not finite in binary.
**How to avoid:** **Forbid `double` arithmetic in tuplet duration math.** Use `Fraction` end-to-end through `CompileTupletElement` and `ValidateBarFit`. Convert to `double` only at the audio sample boundary (already done in `MusicalNoteData.GetBeats` line 277 — `(double)f.Num × timeSigDenominator / (f.Denom × 4.0)`).
**Warning signs:** Phase 18 `ByteIdentical{Tutorial,Showcase}Tests.cs` Facts fail post-Plan-19-01; tuplet acceptance Fact gives an off-by-1 tick; nested tuplet acceptance gives a non-rational sum.

### Pitfall 2: Bar validation breakage

**What goes wrong:** Tuplet bars whose sum is rationally exact but `double`-fuzzily-off get silently truncated or extended without diagnostic.
**Why it happens:** Existing `CalculateAutoFitDuration` at line 206 uses `double` arithmetic and the `if (remainingBeats <= 0) remainingBeats = totalBeats;` pattern at line 275-276 silently swallows overflow.
**How to avoid:** Plan 19-03 introduces a sibling `ValidateBarFit(elements, timeSig) → BarFitResult { Exact | Truncated(Fraction sum, int truncatedAtIdx) }` that uses `Fraction` running sum. Existing `CalculateAutoFitDuration` is preserved for the non-tuplet path (Phase 18 byte-identity contract). The new method runs ONLY when at least one element produces a non-null `DurationFraction`.
**Warning signs:** A `{3:2 C4 D4 E4}q B4q C5q D5q E5q` (5/4 in a 4/4 bar) silently renders 5 notes instead of truncating; no `Info` diagnostic appears in `errorReporter.Errors`.

### Pitfall 3: MIDI tick precision insufficient at TPQN=480

**What goes wrong:** 7-tuplets and 11-tuplets produce drifted tick stamps because `480/7 ≈ 68.57` and `480/11 ≈ 43.6`.
**Why it happens:** Hardcoded `TicksPerQuarterNote = 480` at `MidiExport.cs:17`.
**How to avoid:** Plan 19-04 — pre-export pass computes `requiredTPQN = LCM(480, 2 × union(denoms))`, capped at 9600 (D-05), error above. **Crucially:** the multiplication-by-2 in `2 × union` is what handles the 5:4 case cleanly (480/5 = 96 exact already, but cross-products like `(7:8) × (5:4)` need the factor of 2). Acceptance Fact `MidiTpqnElevationTests.cs::TupletDenom7_ElevatesTo3360` pins `requiredTPQN = LCM(480, 2 × {7}) = LCM(480, 14) = 3360`.
**Warning signs:** A test 7-tuplet exports MIDI with tick stamps `0, 68, 137, 205, …` (drifted) instead of `0, 480, 960, 1440, …` (clean at TPQN=3360).

### Pitfall 9: AUDIT-VERIFIED C5 silently invalidated by tuplet sequences

**What goes wrong:** `Augment`/`Diminish` at `TransformFunctions.cs:239,261` operate on `DurationValue` (enum int) — they don't know about `DurationFraction`. After Phase 19, a `{3:2 C4 D4 E4}q` sequence has `DurationValue == null` and `DurationFraction == 1/12 whole`, so `Augment` no-ops. The C5 marker says "C5 — augment correct (lengthens)" but is no longer covered for tuplet sequences.
**Why it happens:** Phase 11/12's C5 audit verified the enum path; tuplets cross the verification fence.
**How to avoid:** Plan 19-05 — branch `Augment`/`Diminish` on `note.DurationFraction.HasValue` and apply `Fraction(2,1) × f` (augment) or `Fraction(1,2) × f` (diminish, equivalently `f * Fraction(1,2)` since `Fraction.*` is commutative). New regression Facts pin the rational doubling/halving. AUDIT-VERIFIED comment refreshed with `2026-04-NN: re-validated against tuplet sequences (Phase 19 TUP-07)`.
**Warning signs:** Augmenting `{3:2 C4 D4 E4}q` produces 3 notes still at 1/12 whole each; diminish does the same; no Fact catches it.

### Pitfall (Phase-19-specific): Lexer-vs-parser disambiguation for `/N`

**What goes wrong:** `C4 / 12` in expression context is already a valid token sequence (NoteLiteral + Slash + IntLiteral) — albeit semantically meaningless ("note divided by integer"). If we add lexer-level `/N` recognition, expression-context use of `C4 / 12` stops parsing as binary expression and starts emitting a NoteLiteral with stuck-on fractional duration.
**Why it happens:** The lexer is mode-less; note-stream-vs-expression context is enforced at the parser layer.
**How to avoid:** **Do NOT change the lexer for `/N` / `/X:Y` recognition.** The lexer continues to emit Slash + IntLiteral as separate tokens. The PARSER inside `ParseNoteStream` peeks `Slash IntLiteral` after a `NoteLiteral`/`Identifier`/`Underscore` and constructs the AST extension. Outside note streams (expression context), `Slash` continues to be `Star`/`Slash` binary precedence at `Parser.cs:732`. Verified: collision grep in §Pre-Landing Collision Grep below confirms zero `C4 / 12` patterns in expression context across the codebase.
**Warning signs:** A `tests/test_arithmetic.flow`-style file referencing `C4 / 12` (where `C4` is a numeric variable) starts behaving differently — but per the collision grep, no such file exists.

### Pitfall (Phase-19-specific): Per-note `C4/X:Y` colliding with random-choice weight syntax

**What goes wrong:** Random-choice `(? C4:50 E4:30)` uses `:` after a NoteLiteral inside parens for weight. `C4/X:Y` uses `:` after a Slash IntLiteral for tuplet ratio. Both are at note-stream level.
**Why it happens:** Same `:` token (Colon) reused.
**How to avoid:** Disambiguation is structural — `:` after `IntLiteral` (weight) vs `:` after `Slash IntLiteral` (tuplet). The random-choice arm at `Parser.NoteStream.cs:81-128` is gated by `(?` / `(??` lookahead at the OUTER `LParen`. The per-note `/X:Y` parsing happens AFTER a `NoteLiteral` token, never inside a random-choice paren. The AST shapes don't overlap. Plan 19-02 must include a Fact `FractionalDurationTests::RandomChoiceWeights_AndPerNoteTuplet_DoNotCollide` exercising `| (? C4:50 E4:50) D4/3:2 |` to pin both semantics.
**Warning signs:** A random-choice fact starts producing fractional-duration notes; or a per-note `C4/3:2` Fact starts producing weights.

## Code Examples

### Example 1: Bracket parser dispatch arm (extends `ParseNoteStream`)

```csharp
// flow-lang/Parsing/Parser.NoteStream.cs — ADD inside ParseNoteStream loop, BEFORE `if (Match(TokenType.LBracket))` at line 132
// Source: invented from existing patterns at lines 55-128 (LParen dispatch); cross-referenced ARCHITECTURE.md §2

// Tuplet bracket: {N:M ...}q  or  {N ...}q  (shorthand, M defaults from music21 table)
if (Check(TokenType.LBrace))
{
    var elemLoc = CurrentToken.Location;
    Advance(); // consume {

    var nToken = Expect(TokenType.IntLiteral, "Expected integer N in tuplet bracket");
    int n = (int)nToken.Value!;
    int? m = null;

    if (Match(TokenType.Colon))
    {
        var mToken = Expect(TokenType.IntLiteral, "Expected integer M after ':' in tuplet ratio");
        m = (int)mToken.Value!;
    }
    else
    {
        // Music21 shorthand lookup
        m = MusicTwentyOneShorthand.TryGetValue(n, out var lookup) ? lookup : (int?)null;
        if (m == null)
        {
            _errorReporter.ReportError(
                $"Tuplet shorthand {{N}} only supports counts 2-11 (got {n}); use explicit {{N:M}} form",
                elemLoc);
            // best-effort recovery
            m = n;
        }
    }

    // Recursively parse children (note-stream elements until RBrace)
    var children = ParseTupletChildren();   // helper: same dispatch as ParseNoteStream loop, terminating on RBrace
    Expect(TokenType.RBrace, "Expected '}' to close tuplet bracket");

    string? durSuffix = TryParseDurationSuffix();
    if (durSuffix == null)
    {
        _errorReporter.ReportError("Tuplet bracket requires explicit duration suffix", elemLoc);
        durSuffix = "q"; // best-effort recovery
    }
    bool isDotted = Match(TokenType.Dot);

    currentBarElements.Add(new TupletElement(elemLoc, n, m.Value, children, durSuffix, isDotted));
    continue;
}
```

`MusicTwentyOneShorthand` is a private static field on `Parser`:

```csharp
// Plan 19-01 — finalise per music21 docs lookup at https://music21.org/music21docs/usersGuide/usersGuide_19_duration2.html
private static readonly IReadOnlyDictionary<int, int> MusicTwentyOneShorthand = new Dictionary<int, int>
{
    { 2, 3 },   // duplet (2 in time of 3 — uncommon but valid)
    { 3, 2 },   // triplet
    { 4, 6 },   // (4 in time of 6 — quadruplet variant per music21)
    { 5, 4 },   // quintuplet
    { 6, 4 },   // sextuplet
    { 7, 4 },   // septuplet (some sources say 7:8 — confirm at plan time)
    { 8, 6 },
    { 9, 8 },
    { 10, 8 },
    { 11, 8 },
};
```

**[ASSUMED]** The exact 4→6, 7→4 vs 7→8, 8→6, 10→8, 11→8 entries — music21's `duration.standardNumeratorsAndDenominators` table is the canonical authority but varies by edition. Plan 19-01 must finalise via direct music21 docs query (CLAUDE.md doesn't lock these). SPEC.md TUP-02 names 3→3:2, 5→5:4, 6→6:4, 7→7:4, 9→9:8 explicitly — those five are LOCKED. The 2/4/8/10/11 entries are Claude's discretion (CONTEXT.md "Claude's Discretion" bullet 2).

### Example 2: TUP-08 per-note `/X:Y[suffix]` parser arm (extends `NoteLiteral` arm)

```csharp
// flow-lang/Parsing/Parser.NoteStream.cs — REPLACE the existing NoteLiteral arm at line 184-201

if (Check(TokenType.NoteLiteral))
{
    var noteToken = Advance();
    var elemLoc = noteToken.Location;
    string noteName = noteToken.Text;

    // NEW: peek for per-note fractional-duration suffix /N or /X:Y
    (int Num, int Denom)? tupletRatio = null;
    string? overrideDurSuffix = null;

    if (Match(TokenType.Slash))
    {
        var nToken = Expect(TokenType.IntLiteral, "Expected integer N after '/'");
        int n = (int)nToken.Value!;
        if (n < 1)
        {
            // TUP-04 acceptance: "C4/0" → "Duration denominator must be ≥ 1; got 0 at <line>:<col>"
            _errorReporter.ReportError(
                $"Duration denominator must be ≥ 1; got {n}",
                nToken.Location);
            n = 1; // best-effort recovery
        }

        if (Match(TokenType.Colon))
        {
            // TUP-08 form: C4/X:Y[suffix]
            var yToken = Expect(TokenType.IntLiteral, "Expected integer Y after ':' in per-note tuplet ratio");
            int y = (int)yToken.Value!;
            tupletRatio = (n, y);  // TUP-08 acceptance: C4/0:2 → "Tuplet ratio numerator X must be ≥ 1; got 0"
            // optional level suffix
            overrideDurSuffix = TryParseDurationSuffix();   // accepts "q", "h", etc., default null → quarter at compile
        }
        else
        {
            // TUP-04 form: C4/N — synthesise tuplet ratio and force whole-note level
            tupletRatio = (n, 1);   // X=N, Y=1 marker — compiler translates to DurationFraction = 1/N (whole-note units)
            // No optional suffix — /N is unambiguous and consumes a single integer.
        }
    }

    string? durSuffix = overrideDurSuffix ?? TryParseDurationSuffix();
    bool isDotted = durSuffix != null && Match(TokenType.Dot);
    bool isTied = Match(TokenType.Tilde);
    double? centOffset = null;
    if (Check(TokenType.CentLiteral))
        centOffset = (double)Advance().Value!;
    Articulation? articMark = TryParseArticulation();

    currentBarElements.Add(new NoteElement(elemLoc, noteName, durSuffix, isDotted, isTied,
        centOffset, stickyVelocity, articMark, tupletRatio));
    continue;
}
```

### Example 3: Compiler dispatch on extended `NoteElement.TupletRatio` (sketch)

```csharp
// flow-lang/Runtime/NoteStreamCompiler.cs — INSIDE CompileNoteElement (line 322)
private MusicalNoteData CompileNoteElement(NoteElement note, NoteValueType.Value? autoFitDuration, MusicalContext context)
{
    var (noteName, octave, alteration) = NoteType.Parse(note.NoteName);

    // EXISTING duration enum logic stays (line 325-338)
    int? durationValue;
    if (note.DurationSuffix != null && DurationSuffixMap.TryGetValue(note.DurationSuffix, out var noteVal))
        durationValue = (int)noteVal;
    else if (autoFitDuration != null)
        durationValue = (int)autoFitDuration.Value;
    else
        durationValue = (int)NoteValueType.Value.QUARTER;

    // NEW: compute DurationFraction if TupletRatio is present (TUP-04 + TUP-08)
    Fraction? durationFraction = null;
    if (note.TupletRatio.HasValue)
    {
        var (x, y) = note.TupletRatio.Value;
        // TUP-04: C4/N parsed as TupletRatio = (N, 1). DurationFraction = 1/N whole = 4/N quarter-units.
        // TUP-08: C4/X:Y[suffix] — DurationFraction = (suffix_fraction_quarter_units) / X. Y is metadata-only (TPQN feed).
        if (y == 1)
        {
            // TUP-04: 1/N whole = 4/N quarter
            durationFraction = new Fraction(4, x);
        }
        else
        {
            // TUP-08: suffix-aware. Default suffix is q → 1 quarter-unit per group.
            var suffixFrac = SuffixToQuarterFraction(note.DurationSuffix ?? "q", note.IsDotted);
            durationFraction = new Fraction(suffixFrac.Num, suffixFrac.Denom * x);
        }
    }

    // … rest of existing velocity / articulation logic (line 340-355)

    return new MusicalNoteData(noteName, octave, alteration, durationValue, isRest: false,
        centOffset: note.CentOffset, isTied: note.IsTied,
        velocity: velocity, articulation: articulation,
        isDotted: note.IsDotted, sourceLocation: note.Location, sourceLength: CalcSourceLength(note),
        durationFraction: durationFraction);
}
```

`SuffixToQuarterFraction` is a new private helper on `NoteStreamCompiler`:

```csharp
// quarter-note units; w=4q, h=2q, q=1q, e=1/2q, s=1/4q, t=1/8q (dotted multiplier ×3/2)
private static Fraction SuffixToQuarterFraction(string suffix, bool isDotted)
{
    var f = suffix switch
    {
        "w" => new Fraction(4, 1),
        "h" => new Fraction(2, 1),
        "q" => new Fraction(1, 1),
        "e" => new Fraction(1, 2),
        "s" => new Fraction(1, 4),
        "t" => new Fraction(1, 8),
        _ => new Fraction(1, 1),
    };
    return isDotted ? f * new Fraction(3, 2) : f;
}
```

### Example 4: TUP-06 MIDI TPQN auto-elevation pre-pass

```csharp
// flow-lang/StandardLibrary/Audio/MidiExport.cs — REPLACE line 17 const + line 88 + the 4 (beats × TicksPerQuarterNote) sites

// (1) Promote constant to a per-export computed value
private static int ComputeRequiredTpqn(SongData song)
{
    var denominators = new HashSet<int>();
    foreach (var (_, section) in song.SectionRegistry)
        foreach (var (_, sequence) in section.Sequences)
            foreach (var bar in sequence.Bars)
                foreach (var note in bar.MusicalNotes)
                {
                    if (note.DurationFraction.HasValue)
                        denominators.Add(note.DurationFraction.Value.Denom);
                    // CONTEXT D-05 also says: collect TupletRatio.Numerator
                    // … but TupletRatio is per-note metadata that we'd need to thread through MusicalNoteData
                    // For Plan 19-04 simplicity: synthesise from DurationFraction.Denom alone — equivalent for the
                    // bracket form (which always sets DurationFraction) and the per-note form (TUP-04 + TUP-08
                    // both produce non-null DurationFraction in NoteStreamCompiler).
                }
    if (denominators.Count == 0)
        return 480; // D-07: zero behaviour change for tuplet-free output

    int requiredTpqn = 480;
    foreach (var d in denominators)
        requiredTpqn = Lcm(requiredTpqn, 2 * d);

    if (requiredTpqn > 9600)
    {
        var sortedDenoms = denominators.OrderBy(d => d).ToArray();
        throw new InvalidOperationException(
            $"MIDI export requires TPQN={requiredTpqn}, exceeds cap 9600 (locked v1.3 D-05). " +
            $"Tuplet ratios in this song: [{string.Join(", ", sortedDenoms.Select(d => $"{d}"))}]");
    }
    return requiredTpqn;
}

private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
private static int Lcm(int a, int b) => a / Gcd(a, b) * b;

// (2) Inside ExportMidiInternal at line 87:
private static void ExportMidiInternal(string filepath, SongData song)
{
    int ticksPerQuarter = ComputeRequiredTpqn(song);   // NEW
    var midiFile = new MidiFile();
    midiFile.TimeDivision = new TicksPerQuarterNoteTimeDivision(ticksPerQuarter);
    // …
    // (3) Replace 4 existing call sites at lines 184, 195, 216, 259 — substitute `TicksPerQuarterNote` with local `ticksPerQuarter`
}
```

**Note on D-05 wording:** CONTEXT D-05 says the union includes "`TupletRatio.Numerator`". As implemented above, we collect from `DurationFraction.Denom` only — this is equivalent because Plan 19-01 always emits `DurationFraction` for any tuplet-derived note, and the denominator always carries the tuplet count in its denom. Plan 19-04 should validate this equivalence empirically by Fact (e.g. `MidiTpqnElevationTests::PerNoteSeptuplet_ElevatesTo3360` exercises `| C4/7:8 |` and confirms TPQN = 3360 same as bracket-form).

### Example 5: TUP-07 augment/diminish branch on `DurationFraction`

```csharp
// flow-lang/StandardLibrary/Transforms/TransformFunctions.cs — REPLACE Augment body at line 240

// AUDIT-VERIFIED 2026-04-18: C5 — augment correct (lengthens); observed A=#### vs Q=## columns in visualize
// AUDIT-VERIFIED 2026-04-NN: re-validated against tuplet sequences (Phase 19 TUP-07)
//   — see flow-lang.Tests/Unit/Phase19/TupletAugmentDiminishTests.cs (rational doubling pinned)
private static Value Augment(IReadOnlyList<Value> args)
{
    var seq = args[0].As<SequenceData>();

    var result = TransformNotes(seq, note =>
    {
        // NEW: rational branch when DurationFraction is set (TUP-07)
        if (note.DurationFraction.HasValue)
        {
            var doubled = note.DurationFraction.Value * new Fraction(2, 1);
            return new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                note.DurationValue, note.IsRest, note.CentOffset, note.IsTied, note.Velocity,
                note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength,
                durationFraction: doubled);
        }

        // EXISTING power-of-2 enum path — UNCHANGED
        if (!note.DurationValue.HasValue) return note;
        int newDur = note.DurationValue.Value - 1;
        if (newDur < (int)NoteValueType.Value.WHOLE)
        {
            Console.Error.WriteLine("Warning: augment clamped duration at whole note");
            newDur = (int)NoteValueType.Value.WHOLE;
        }
        return new MusicalNoteData(note.NoteName, note.Octave, note.Alteration, newDur,
            note.IsRest, note.CentOffset, note.IsTied, note.Velocity, note.Articulation,
            note.IsDotted, note.SourceLocation, note.SourceLength);
    });
    return Value.Sequence(result);
}
```

`Diminish` mirrors the pattern with `* new Fraction(1, 2)` instead of `* new Fraction(2, 1)`. Both Augment and Diminish line numbers (239, 261) get the same comment refresh.

## State of the Art

This phase doesn't supersede prior approaches in Flow's code — it activates a foundation. The relevant SOTA mapping:

| Old Approach (pre-Phase-19) | Current Approach | When Changed | Impact |
|------------------------------|------------------|--------------|--------|
| Power-of-2 NoteValueType enum only | `Fraction? DurationFraction` field overrides enum when set | Phase 18 (DORMANT) → Phase 19 (ACTIVE) | Tuplets, arbitrary `/N`, nested tuplets all become representable |
| Hardcoded `TicksPerQuarterNote = 480` | Per-export `ComputeRequiredTpqn(song)` with cap 9600 | Phase 19 Plan 19-04 | 7-tuplets, 11-tuplets export correctly; cap bound by audible-DAW-import field testing |
| `Augment`/`Diminish` enum-only | Branch on `DurationFraction.HasValue`; rational doubling/halving for tuplet sequences | Phase 19 Plan 19-05 | C5 audit re-validated; tuplet transforms preserve ratio |
| Bar-fit silent overflow recovery (`if (remainingBeats <= 0) remainingBeats = totalBeats;`) | New `ValidateBarFit` with rational sum + truncate-with-Info on overflow | Phase 19 Plan 19-03 | Composer sees diagnostic; charitable interpretation preserved |

**Deprecated/outdated:** Nothing — Phase 19 is purely additive. Existing scripts byte-identical (Phase 18 gate enforces).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Music21 shorthand entries 2/4/8/10/11 (CONTEXT.md "Claude's Discretion" bullet 2) | Code Examples §1 | LOW — SPEC.md TUP-02 acceptance only requires the 5 entries (3,5,6,7,9) it names; the others are conveniences. Plan 19-01 must verify final entries against music21 docs at plan-time. |
| A2 | Quarter-note units (not whole-note units) for `DurationFraction` semantics throughout Phase 19 | Pattern 2 — recursive scale | LOW — Phase 18 SUMMARY 18-02 §Decisions Made explicitly pins this ("quarter-note units (music21 convention)"). SPEC.md "1/12 whole" prose translates to "1/3 quarter" math (1/12 × 4 = 1/3). All Phase 18 Facts use quarter-note units. |
| A3 | TPQN=480 baseline + cap=9600 are hard-coded constants in MidiExport.cs (CONTEXT "Claude's Discretion" bullet 3) | Code Examples §4 | LOW — D-05 fixes cap at 9600; re-litigating would require new SPEC. |
| A4 | LCM helper lives next to its sole caller in MidiExport.cs (private static), not in `Fraction.cs` | Don't Hand-Roll table | LOW — Fraction's operator surface is deliberately minimal per Plan 18-01 SUMMARY decision; LCM is MIDI-export-specific math. |
| A5 | The `2 × union` factor in `LCM(480, 2 × union(denoms))` is necessary even though 480 already absorbs 2 | Code Example §4 | MEDIUM — SPEC TUP-06 acceptance pins `{7:8 ...}` → TPQN=3360 = 480×7. With 2× factor: LCM(480, 14) = 3360 ✓. Without 2× factor: LCM(480, 7) = 3360 ✓ (same answer because 480 = 2^5 × 3 × 5 already). Plan 19-04 must verify the 2× factor doesn't introduce redundancy via a Fact pinning `{5:4 ...}` → TPQN=480 (with 2× factor: LCM(480, 10) = 480 ✓; without: LCM(480, 5) = 480 ✓). The 2× factor is defensive against a future composer ratio that has only odd-power-of-2 in denom; Plan 19-04 may simplify to `LCM(480, union)` if all SPEC acceptance Facts pass. |
| A6 | Plan 19-02 lexer changes are PARSER-LEVEL only — no SimpleLexer.cs edits needed for `/N` recognition | Pattern 3 | MEDIUM — assumes existing tokens (NoteLiteral, Slash, IntLiteral, Colon, Identifier) cover the surface. Plan 19-02 must confirm via empirical try-it-and-see at plan time. If the lexer collapses `C4/12` into a single Identifier token (note + suffix), a rewind-and-extend at SimpleLexer.cs line 660 mirroring the `C4q` precedent is the fix. **Spot-check at line 660 says current behaviour is the latter — `C4q` rewinds 1 char to split the suffix.** Plan 19-02 should default to following this same precedent for `/`. |
| A7 | Existing `ChordParser.IsChordSymbol` does not match `{N:M …}` text fragments (collision-free for tuplet brackets) | §Pre-Landing Collision Grep | LOW — `{` is not a valid chord-symbol character; `ChordParser.IsChordSymbol` is letter-based. Plan 19-01 should still grep `{` in `tests/`, `examples/`, `flow-lang/*.flow` once at plan-time. |

If this table is empty: **NOT empty** — 7 assumptions logged. Plan 19-01 and 19-02 must confirm A1, A5, A6 at plan-time (single one-line empirical checks each).

## Pre-Landing Collision Grep (transcript template for Plan 19-01)

Per Phase 14 D-21 + CONTEXT D-08 last sentence, Plan 19-01 must paste empirical grep transcripts into `19-01-PLAN.md`. The expected outputs:

```bash
# 1. Brace tokens inside note-stream context
grep -nE "\| .*\{|\{[0-9]" tests/*.flow examples/*.flow flow-lang/*.flow 2>/dev/null
# Expected: empty — braces are unused inside note streams today.

# 2. Existing /N usage that could shadow tuplet syntax
grep -rnE "/[0-9]+" tests/*.flow examples/*.flow flow-lang/*.flow 2>/dev/null \
  | grep -vE "(timesig|//|\.flow:[0-9]+:.*str.*(3|5|6|7|9)/[0-9])"
# Expected: only timesig references (4/4, 3/4, 6/8 etc.) and string-literal echoes of "euclidean 3/8".

# 3. Identifiers that could collide with new tuplet keywords (none introduced in Phase 19)
# — Phase 19 introduces ZERO new keywords. Tuplet syntax is bracket-only + /N + /X:Y; none of
# these are identifier-class. No tests/*.flow can have a variable colliding because the syntax
# is pure punctuation + integer literals.

# 4. Confirmation: { and } already exist as TokenType.LBrace / TokenType.RBrace
grep -n "TokenType.LBrace\|TokenType.RBrace" flow-lang/Lexing/SimpleLexer.cs
# Expected: line 111-112 (LBrace/RBrace single-char dispatch).

# 5. Confirmation: Phase 18 closed at commit ba8534a
git log --oneline | grep -E "(ba8534a|2092f32)"
# Expected: both commits present in history.
```

Empty-grep #1 and #2 is the gate. Plan 19-01 records the transcript inline.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All build/test | ✓ | per project pin | — |
| `dotnet test flow-sharp.sln` | Per-plan verification | ✓ | Phase 18 ran 306/306 GREEN | — |
| `cmp` (POSIX) | Byte-identical determinism gate (D-16) | ✓ | system | — |
| `git` | Commit hash verification + collision grep | ✓ | system | — |

No new tools, no new services, no environment-variable changes. All tooling already exercised in Phase 18.

## Validation Architecture

> Phase 18's `nyquist_validation: true` is set in `.planning/config.json`; this section MUST be included.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit.v3 3.2.2 + xunit.runner.visualstudio 3.1.5 |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` (existing) |
| Quick run command | `dotnet test --filter "FullyQualifiedName~Phase19" --no-build` |
| Full suite command | `dotnet test flow-sharp.sln --no-build` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| TUP-01 | `{3:2 C4 D4 E4}q` → 3 MusicalNoteData each DurationFraction=1/12 whole (=1/3 quarter) | unit | `dotnet test --filter "FullyQualifiedName~TupletBracketTests::Triplet_QuarterGroup_ProducesThreeOneTwelfthNotes" -x` | ❌ Plan 19-01 |
| TUP-02 | `{3 ...}q` ≡ `{3:2 ...}q`; `{12 ...}` parse error | unit | `…TupletBracketTests::ShorthandThree_EquivalentToThreeTwo` + `…ShorthandTwelve_RaisesParseError` | ❌ Plan 19-01 |
| TUP-03 | Nested tuplet `{3:2 C4 {3:2 D4 E4 F4}q G4}h` → 5 notes [1/6, 1/18, 1/18, 1/18, 1/6] whole | unit | `…TupletBracketTests::NestedTriplet_OuterAndInnerComposeViaScaleAccumulation` | ❌ Plan 19-01 |
| TUP-04 | `\| C4/12 D4/12 E4/12 \|` → 3 notes 1/12 whole; `C4/0` parse error; `C4/1` whole note | unit | `…FractionalDurationTests::SlashTwelve_ProducesThreeOneTwelfthNotes` + `…SlashZero_RaisesParseError` + `…SlashOne_ProducesWholeNote` | ❌ Plan 19-02 |
| TUP-05 | `4/4` bar with `{3:2 C4 D4 E4}q {3:2 F4 G4 A4}q B4q C5q` validates exact; 5/4 overflow truncates+ReportInfo | unit | `…BarFitOverflowTests::FourQuarterBar_ExactSum_NoDiagnostic` + `…OverflowFiveFourths_TruncatesAtBoundary_EmitsInfo` | ❌ Plan 19-03 |
| TUP-06 | `{3:2}` → TPQN=480; `{7:8}` → TPQN=3360; `{11:13}` → cap error message | unit | `…MidiTpqnElevationTests::Triplet_StaysAt480` + `…Septuplet_ElevatesTo3360` + `…ElevenAgainstThirteen_RaisesCapError` | ❌ Plan 19-04 |
| TUP-07 | `augment([1/12, 1/12, 1/12])` → `[1/6, 1/6, 1/6]`; `diminish([1/12, 1/12, 1/12])` → `[1/24, 1/24, 1/24]` | unit | `…TupletAugmentDiminishTests::Augment_RationalDouble` + `…Diminish_RationalHalve` | ❌ Plan 19-05 (two-pass strict) |
| TUP-08 | `\| C4/3:2 D4/3:2 E4/3:2 \|` ≡ `\| {3:2 C4 D4 E4}q \|`; `C4/5:4h` → 1/10 whole; mixed ratios legal; `C4/0:2` parse error | unit | `…FractionalDurationTests::PerNoteThreeAgainstTwo_EquivalentToBracket` + `…PerNoteWithHalfSuffix_OneTenthWhole` + `…MixedRatios_AdjacentNotesLegal` + `…PerNoteZeroNumerator_RaisesParseError` | ❌ Plan 19-02 |
| Determinism | tutorial.flow + showcase.flow byte-identical post-Phase-19 | integration | Phase 18's `ByteIdentical{Tutorial,Showcase}Tests.cs` re-run after each Phase 19 plan commit | ✓ exists |
| AUDIT-VERIFIED C5 refresh | `TransformFunctions.cs:239,261` comment includes `2026-04-NN: re-validated against tuplet sequences (Phase 19 TUP-07)` | grep | `grep -c "Phase 19 TUP-07" flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` returns 2 | ❌ Plan 19-05 |

### Sampling Rate

- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Phase19" --no-build` (~1-2s)
- **Per wave merge:** `dotnet test flow-sharp.sln --no-build` (~25s — current Phase 18 baseline 306 + ~25-30 new Phase 19 Facts)
- **Phase gate (before /gsd-verify-work):** Full suite green AND Phase 18 byte-identical Facts re-run AND `cmp` against pre-Phase-19 baselines for tutorial.flow + showcase.flow output

### Wave 0 Gaps

- [ ] `flow-lang.Tests/Unit/Phase19/TupletBracketTests.cs` — covers TUP-01..03 (created in Plan 19-01)
- [ ] `flow-lang.Tests/Unit/Phase19/FractionalDurationTests.cs` — covers TUP-04 + TUP-08 (created in Plan 19-02)
- [ ] `flow-lang.Tests/Unit/Phase19/BarFitOverflowTests.cs` — covers TUP-05 (created in Plan 19-03)
- [ ] `flow-lang.Tests/Unit/Phase19/MidiTpqnElevationTests.cs` — covers TUP-06 (created in Plan 19-04)
- [ ] `flow-lang.Tests/Unit/Phase19/TupletAugmentDiminishTests.cs` — covers TUP-07 (created in Plan 19-05)

No framework install needed (xUnit.v3 already pinned). No `conftest.py`-equivalent shared fixture file needed (Phase 18 didn't need one either; xUnit Facts construct test data inline).

## Security Domain

Phase 19 is a parser/compiler/audio-pipeline change with zero authentication, session, access-control, cryptography, or external-service surface. The closest applicable ASVS category is **V5 Input Validation** — note-stream source is parsed, but the parsing surface is bounded by the lexer (no string-format mini-DSLs, no eval, no arbitrary code execution from user input).

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | n/a |
| V3 Session Management | no | n/a |
| V4 Access Control | no | n/a |
| V5 Input Validation | yes (light) | Existing `ErrorReporter.ReportError` for malformed `/0`, `{12 ...}` (no explicit lookup), missing duration suffix, etc. — all parse-time, pre-compile, no execution |
| V6 Cryptography | no | n/a |

### Known Threat Patterns for the parser/compiler stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Pathological recursion (deeply nested tuplets) | DoS | Existing `_parseDepth > MaxParseDepth = 500` guard at `Parser.cs:30` already covers — recursive tuplet parsing inherits this limit |
| Integer overflow in TPQN computation | Tampering | Cap at 9600 (well below `int.MaxValue`); GCD/LCM math stays in `int` per Phase 18 `Fraction` design |
| MIDI export raising on cap | Availability (false-positive) | Cap error is intentional (D-06) — composer-facing; not a security issue |

Nothing else in the standard threat catalog applies. Phase 19 is intentionally a "just rationals and integers" change.

## Sources

### Primary (HIGH confidence)

- `flow-lang/Ast/Expressions/NoteStreamExpression.cs` — read in full; 9 existing record types confirmed
- `flow-lang/Parsing/Parser.NoteStream.cs` — read in full; existing dispatch loop pattern confirmed
- `flow-lang/Parsing/Parser.cs` — top 200 lines + lines 391-415, 565-643, 730-733, 1067 — confirms LBrace/RBrace usage in section/musical-context/loop bodies
- `flow-lang/Lexing/SimpleLexer.cs` — lines 47-136, 287-300, 555-720 — confirms LBrace/RBrace at line 111-112 + duration-suffix rewind at line 660
- `flow-lang/Runtime/NoteStreamCompiler.cs` — read in full (648 lines); confirms `DurationSuffixMap` line 29, `CalculateAutoFitDuration` line 206, dispatch switch line 75-138
- `flow-lang/TypeSystem/Fraction.cs` — read in full (57 lines); operators + GCD confirmed
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` — lines 200-307 — confirms `DurationFraction` field at line 244, `GetBeats` branch at line 268, ctor signature line 246
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` — lines 1-100, 160-262 — confirms `TicksPerQuarterNote = 480` line 17 + 4 call sites at 184/195/216/259
- `flow-lang/Diagnostics/ErrorReporter.cs` — read in full (58 lines); `ReportInfo(string, SourceLocation?)` at line 43 verified
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` — lines 220-310 — confirms AUDIT-VERIFIED markers at line 239, 261; existing enum-int duration arithmetic
- `flow-lang/Core/SourceLocation.cs` — read in full (15 lines); record shape confirmed
- `.planning/phases/18-foundation-rational-duration-arithmetic/18-01-SUMMARY.md` — Fraction primitive shipped 2092f32; 9 Facts GREEN
- `.planning/phases/18-foundation-rational-duration-arithmetic/18-02-SUMMARY.md` — DurationFraction wired ba8534a; 19/19 Phase18 Facts GREEN; 306/306 full suite; D-USER-04 dormancy verified
- `.planning/research/ARCHITECTURE.md` §2 (TupletElement AST), §3 (Fraction migration), §9 (anti-patterns) — primary HOW reference
- `.planning/research/PITFALLS.md` Pitfalls 1, 2, 3, 9 — Critical pitfalls for this phase
- `.planning/phases/19-tuplets-arbitrary-fractional-durations/19-SPEC.md` — 8 LOCKED requirements (read in full)
- `.planning/phases/19-tuplets-arbitrary-fractional-durations/19-CONTEXT.md` — 19 LOCKED implementation decisions D-01..D-19 (read in full)
- `.planning/REQUIREMENTS.md` — TUP-01..08 traceability rows; binding pre-orderings
- `.planning/STATE.md` — Phase 18 closure verified at ba8534a

### Secondary (MEDIUM confidence)

- `.planning/research/FEATURES.md` — Lilypond/ABC/music21 tuplet conventions cross-reference (canonical sources cited)
- `flow-lang/StandardLibrary/Composition/PolyrhythmFunctions.cs:117` — Euclidean GCD idiom (re-confirmed via grep; same algorithm Fraction.cs uses)
- `.planning/RETROSPECTIVE.md §v1.2` — Determinism contract lessons (Pitfall 6 backref)

### Tertiary (LOW confidence)

- music21 shorthand entries 4/8/10/11 — derived from FEATURES.md cross-reference, not directly verified at music21 docs in this research session. Plan 19-01 must confirm. (See Assumption A1.)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every component is shipped (Fraction at 2092f32; DurationFraction at ba8534a; DryWetMidi 8.0.3 stable since v1.2 Phase 14)
- Architecture: HIGH — recursive `Fraction outerScale` pattern is RESEARCH/ARCHITECTURE.md §2 + verified consistent with Phase 18 quarter-note units
- Pitfalls: HIGH — Pitfalls 1/2/3/9 are direct quotes from PITFALLS.md; Phase-19-specific pitfalls (lexer-vs-parser disambiguation, random-choice colon collision) are derived from direct codebase reads
- Lexer/parser dispatch: HIGH — confirmed by reading SimpleLexer.cs and Parser.NoteStream.cs in full
- Music21 shorthand table contents: MEDIUM (5 entries LOCKED in SPEC, 5 entries deferred to Plan 19-01 verification)
- TPQN-elevation `2 × union` factor: MEDIUM (acceptance Facts will validate empirically; Plan 19-04 may simplify if redundant)

**Research date:** 2026-04-26
**Valid until:** 2026-05-26 (30 days for stable; Phase 18 just shipped, no fast-moving dependencies)

---
*Research for: Flow Phase 19 — Tuplets & Arbitrary Fractional Durations*
*Researched: 2026-04-26*
*Next step: /gsd-plan-phase 19 — split 8 requirements into 5 plans across 4 waves per CONTEXT D-08..D-13*
