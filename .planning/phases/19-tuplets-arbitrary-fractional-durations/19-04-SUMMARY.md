---
phase: 19-tuplets-arbitrary-fractional-durations
plan: 04
subsystem: midi-export
tags: [tuplets, midi-export, tpqn, lcm, cap-error, atomic-export]
requirements_completed: [TUP-06]
dependency_graph:
  requires:
    - flow-lang/Ast/Expressions/NoteStreamExpression.cs::TupletElement (Plan 19-01 a7f94ef)
    - flow-lang/Ast/Expressions/NoteStreamExpression.cs::NoteElement.TupletRatio (Plan 19-01 a7f94ef)
    - flow-lang/Runtime/NoteStreamCompiler.cs CompileTupletElement / CompileNoteElement TupletRatio branch (Plans 19-01 a7f94ef + 19-02 9aae23c)
    - flow-lang/TypeSystem/Fraction.cs (Phase 18 FRAC-01, commit 2092f32)
    - flow-lang/TypeSystem/SpecialTypes/NoteType.cs MusicalNoteData.DurationFraction (Phase 18 FRAC-02, commit ba8534a)
  provides:
    - flow-lang/StandardLibrary/Audio/MidiExport.cs::ComputeRequiredTpqn (pre-export pass)
    - flow-lang/StandardLibrary/Audio/MidiExport.cs::Gcd / Lcm helpers
    - flow-lang/StandardLibrary/Audio/MidiExport.cs::MaxTpqn = 9600 const
    - Per-export ticksPerQuarter local threading through ExportMidiInternal + CalculateSectionLengthTicks
  affects:
    - Plan 19-05 (TUP-07 augment/diminish) — independent (touches TransformFunctions); uses no MidiExport surface
tech_stack:
  added: []
  patterns:
    - Pre-export single-pass walk over SongData.SectionRegistry.Values -> Sequences.Values -> Bars -> MusicalNotes
    - HashSet<int> denominator union (deterministic sort via OrderBy for error message)
    - LCM accumulation with `2 × denom` defensive factor (CONTEXT D-05 verbatim)
    - Atomic-export contract — cap error fires BEFORE MidiFile allocation, before any disk I/O
    - Phase 18 byte-identical preservation via `denominators.Count == 0 → return 480` short-circuit
    - Per-export local supersedes module-level const at 4 multiplication sites + 1 TimeDivision setter; const remains as the BASELINE inside ComputeRequiredTpqn
key_files:
  created:
    - flow-lang.Tests/Unit/Phase19/MidiTpqnElevationTests.cs (6 Facts, 178 lines)
  modified:
    - flow-lang/StandardLibrary/Audio/MidiExport.cs (+82 insertions / -7 deletions; 6 logical edits as per plan)
decisions:
  - "ticksPerQuarter local in ExportMidiInternal threaded as a third parameter (int) into CalculateSectionLengthTicks rather than recomputing inside the helper. The ComputeRequiredTpqn pass runs ONCE per export (deterministic, idempotent), and the helper's single call site is in the same method body — threading is the minimal-surface refactor."
  - "(short) cast added on the TimeDivision setter (line 153). DryWetMidi's TicksPerQuarterNoteTimeDivision ctor takes short. Original const-int form compiled implicitly because const literal 480 has compile-time short conversion; runtime int requires explicit cast. Cast is safe by construction because MaxTpqn=9600 < short.MaxValue=32767. Inline comment cites the bound."
  - "Cap-error message renders denominators as the sorted unique set of DurationFraction.Denom values (e.g. [7, 11] for the multi-ratio test) — not the user-typed X:Y pairs. The CONTEXT D-06 'X:Y list' wording was reconciled at task time: MidiExport walks compiler output (MusicalNoteData), which only carries the Denom; this surface is sufficient to fail and points the composer at the offending denominators. Acceptable per plan §interfaces 'Implementation note on the X:Y list format'."
  - "Cap-error trigger example chosen as `{7:8 ...}q {11:13 ...}q` in the same song — denoms={7,11}, requiredTPQN = LCM(480, 14, 22) = LCM(3360, 22) = 3360 × 11 = 36960 > 9600. Single-ratio {11:13} alone yields denoms={11}, requiredTPQN = LCM(480, 22) = 5280 < 9600 (would NOT trigger). Plan §interfaces flagged this discrepancy with SPEC and pre-resolved by switching to multi-ratio. The Fact docstring documents the math."
  - "Test strategy: Strategy A end-to-end via FlowEngineRunner — `.flow` source matches user-facing acceptance, integrates with existing FlowEngineRunner / DryWetMidi reading patterns. `[Collection(\"FlowScripts\")]` per Phase 14/15/17 convention serializes Console.SetOut. Each Fact authors a tiny .flow snippet (musical context blocks → section → Song → writeMidi to temp path), reads back the SMF header via `MidiFile.Read(path)`, asserts `TimeDivision.TicksPerQuarterNote`."
  - "Test scripts use nested `tempo { timesig { ... } }` blocks (canonical Flow syntax verified against tests/test_midi_export.flow), with top-level `section verse { ... }` declarations + `Song song = [verse]` + `(writeMidi \"...\" song)`. The plan template's `Section verse = section verse { s }` form is NOT canonical — corrected at task time."
  - "Test scripts require `use \"@std\"` + `use \"@audio\"` to import the `writeMidi` proc (declared in flow-lang/audio.flow:408 as an internal binding to MidiExport.WriteMidi). First test run failed with `Function 'writeMidi' not found` until the use directives were added. Auto-fixed at Task 2 (Rule 3 - Blocking)."
metrics:
  duration: ~7 min
  completed_date: 2026-04-26
  tasks_completed: 2
  files_changed: 2
  facts_added: 6
  full_suite_pre: 329
  full_suite_post: 335
---

# Phase 19 Plan 04: MIDI Export TPQN Auto-Elevation Summary

**One-liner:** Pre-export single-pass over `SongData.SectionRegistry.Values` collecting `MusicalNoteData.DurationFraction.Denom` values; `requiredTPQN = LCM(480, 2 × union(denoms))` either elevates the SMF header's TPQN (capped at 9600) or raises a clear composer-facing cap error before any disk I/O — preserving Phase 18 byte-identical contract for non-tuplet songs through a `denominators.Count == 0` short-circuit.

## Outcome

The Flow MIDI export now adapts its tick resolution to the tuplet content of each song:

```flow
| {3:2 C4 D4 E4}q |          // denoms={3} → LCM(480, 6) = 480 → TPQN unchanged
| {5:4 C4 D4 E4 F4 G4}q |    // denoms={5} → LCM(480, 10) = 480 → TPQN unchanged
| {7:8 ... 7 children}q |    // denoms={7} → LCM(480, 14) = 3360 → AUTO-ELEVATED
| {7:8} {11:13} same song |  // denoms={7,11} → 36960 > 9600 → CAP ERROR (atomic, no partial file)
| C4q D4q E4q |              // zero tuplets → TPQN unchanged at 480 (Phase 18 contract held)
| C4/7:8 D4/7:8 ... |        // per-note tuplet shorthand — same TPQN math as bracket form
```

Each `writeMidi` call runs the pre-export pass exactly once. When tuplets are absent, the pass short-circuits to the BASELINE 480 — the existing 4 multiplication sites + TimeDivision setter compute byte-identically to pre-Phase-19.

## ComputeRequiredTpqn Pass Shape

The verified hierarchy walked is:

```
SongData.SectionRegistry.Values (Dictionary<string, SectionData>.Values)
  → SectionData.Sequences.Values (Dictionary<string, SequenceData>.Values)
    → SequenceData.Bars (List<BarData>)
      → BarData.MusicalNotes (List<MusicalNoteData>)
        → MusicalNoteData.DurationFraction (FlowLang.TypeSystem.Fraction?)
          → Fraction.Denom (int)
```

Per the plan's §interfaces verification step (Step 1), all four type definitions were confirmed at task time via `grep` over `flow-lang/TypeSystem/SpecialTypes/`:

| Type | Property | Shape |
|------|----------|-------|
| `SongData` | `SectionRegistry` | `Dictionary<string, SectionData>` |
| `SectionData` | `Sequences` | `Dictionary<string, SequenceData>` |
| `SequenceData` | `Bars` | `List<BarData>` |
| `BarData` | `MusicalNotes` | `List<MusicalNoteData>` |
| `MusicalNoteData` | `DurationFraction` | `FlowLang.TypeSystem.Fraction?` (nullable) |

The walk uses `.Values` on the two `Dictionary<>` levels and direct enumeration on the two `List<>` levels. SectionRegistry is the canonical denominator surface (NOT `song.Sections` — that's the arrangement order with possible repeats; SectionRegistry is the unique-by-name underlying registry).

## Cap-Error Message Verbatim

For the multi-ratio cap test (`{7:8 ...}q {11:13 ...}q`), the rendered cap-error message is:

```
MIDI export requires TPQN=36960, exceeds cap 9600 (locked v1.3 D-05). Tuplet ratios in this song: [7, 11]
```

Sorted-denominator format (NOT user-typed X:Y pairs) — see decision §3 above. The `OrderBy(x => x).ToArray()` + `string.Join(", ", ...)` rendering is deterministic and points the composer at the offending denominators.

The error fires from `ComputeRequiredTpqn` at the TOP of `ExportMidiInternal` (line 149), BEFORE the `new MidiFile()` allocation at line 151 — atomic, no partial file written. The Fact `LargeRatioCombination_RaisesCapError` pins this contract via `Assert.False(File.Exists(outPath))`.

## Math Verification

Confirmed by Fact and by hand:

| Ratio set | Denoms | LCM step-by-step | requiredTPQN | Action |
|-----------|--------|------------------|--------------|--------|
| `{3:2}` | {3} | LCM(480, 6) = 480 (480 already divisible by 6) | **480** | unchanged |
| `{5:4}` | {5} | LCM(480, 10) = 480 (480 already divisible by 10) | **480** | unchanged |
| `{7:8}` | {7} | LCM(480, 14): gcd(480,14)=2; LCM=480×7=3360 | **3360** | elevate |
| `{7:8}` + `{11:13}` | {7, 11} | LCM(480,14)=3360; LCM(3360,22): gcd=2, LCM=3360×11=36960 | **36960** | **cap error** (> 9600) |
| `\| C4q D4q E4q \|` (no tuplets) | {} | short-circuit | **480** | unchanged (Phase 18 byte-identical) |
| `\| C4/7:8 ... \|` (per-note) | {7} | same as bracket {7:8} | **3360** | elevate (TUP-08 parity with bracket-form) |

## Edits Inventory (MidiExport.cs)

| Edit | Site | Before | After |
|------|------|--------|-------|
| A | top usings | 5 usings | +`using System.Linq;` (for `OrderBy` / `string.Join` LINQ) |
| B | post-`TicksPerQuarterNote` const | empty | +`MaxTpqn = 9600` const + Gcd/Lcm helpers + `ComputeRequiredTpqn(SongData)` (~50 lines) |
| C | top of `ExportMidiInternal` | `var midiFile = new MidiFile();` | +`int ticksPerQuarter = ComputeRequiredTpqn(song);` ABOVE the `new MidiFile()` |
| D | TimeDivision setter | `new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote)` | `new TicksPerQuarterNoteTimeDivision((short)ticksPerQuarter)` (with bound-justification comment) |
| E1 | rest-tick advance | `* TicksPerQuarterNote` | `* ticksPerQuarter` |
| E2 | note-duration ticks | `* TicksPerQuarterNote` | `* ticksPerQuarter` |
| E3 | bar-tick advance | `* TicksPerQuarterNote` | `* ticksPerQuarter` |
| F1 | `CalculateSectionLengthTicks` call site | 2 args `(sectionData, sectionTimeSigDenom)` | 3 args `(sectionData, sectionTimeSigDenom, ticksPerQuarter)` |
| F2 | `CalculateSectionLengthTicks` signature | 2 params | 3 params (added `int ticksPerQuarter`) + xmldoc note |
| F3 | inside `CalculateSectionLengthTicks` | `* TicksPerQuarterNote` | `* ticksPerQuarter` |

After all edits: 0 occurrences of `\* TicksPerQuarterNote` remain (the const stays only in `ComputeRequiredTpqn` as the BASELINE return value + LCM seed).

## Facts Shipped (6)

All in `flow-lang.Tests/Unit/Phase19/MidiTpqnElevationTests.cs` (gated by `[Collection("FlowScripts")]` per Phase 14/15/17 convention):

| # | Test | Pinned |
|---|------|--------|
| 1 | `Triplet_StaysAt480` | `{3:2 C4 D4 E4}q ...` → TPQN=480; LCM(480, 6)=480 |
| 2 | `Quintuplet_StaysAt480` | `{5:4 C4 D4 E4 F4 G4}q ...` → TPQN=480; LCM(480, 10)=480 |
| 3 | `Septuplet_ElevatesTo3360` | `{7:8 C4 D4 E4 F4 G4 A4 B4}q ...` → TPQN=3360; LCM(480, 14)=3360 |
| 4 | `LargeRatioCombination_RaisesCapError` | `{7:8 ...}q {11:13 ...}q` → cap-error in stderr containing "exceeds cap 9600" / "locked v1.3 D-05" / "Tuplet ratios in this song:"; output file does NOT exist (atomic) |
| 5 | `ZeroTuplets_StaysAt480` | `\| C4q D4q E4q F4q \|` → TPQN=480; CONTEXT D-07 short-circuit; Phase 18 byte-identical contract preserved structurally |
| 6 | `PerNoteSeptuplet_ElevatesTo3360` | `\| C4/7:8 D4/7:8 ... \|` → TPQN=3360; TUP-08 per-note path produces same DurationFraction.Denom=7 as bracket form |

Each non-cap Fact uses a `RunAndReadTpqn` helper that authors a Flow source via heredoc (with `{{OUTPATH}}` placeholder), runs it via `FlowEngineRunner.RunSource`, asserts the run succeeded, then reads the resulting `.mid` file via `MidiFile.Read(path)` and casts `TimeDivision` to `TicksPerQuarterNoteTimeDivision` to extract the ticks-per-quarter integer.

The cap-error Fact uses an inline pattern (no helper) to capture stderr explicitly and assert `File.Exists(outPath) == false` for the atomic-export contract.

## Phase 18 Byte-Identical Regression Gate

**HELD.** `dotnet test --filter "FullyQualifiedName~Phase18.ByteIdentical"` reports **4/4 passed** post-commit (Tutorial WAV + MIDI + Showcase WAV + MIDI). Both `examples/tutorial.flow` and `examples/showcase.flow` contain ZERO tuplet syntax (verified at task time via `grep -nE "\{[0-9]+(:[0-9]+)?\s|/[0-9]+:[0-9]"` returning empty). For these songs, ComputeRequiredTpqn's `denominators.Count == 0 → return 480` short-circuit guarantees `ticksPerQuarter == TicksPerQuarterNote == 480`, and the 4 multiplication sites + TimeDivision setter produce byte-identical output to pre-Phase-19.

Cumulative Phase 18: **19/19 passed**.

## Cumulative Test Counts

| Phase | Pre-19-04 | This plan | Post-19-04 |
|-------|-----------|-----------|------------|
| Phase 19 | 23 (8 19-01 + 9 19-02 + 6 19-03) | +6 | **29** |
| Phase 18 | 19 | 0 | **19** |
| Full suite | 329 (= 306 baseline + 23) | +6 | **335** (= 306 + 29) |

Plans 19-03 (TUP-05 bar-fit validator, commit `3679ab4`) and 19-04 (TUP-06 MIDI TPQN, this plan) ran in Wave 3 in parallel; Plan 19-03 had already landed on master when this plan began (verified via `git log --oneline`). No file overlap — 19-03 touched `NoteStreamCompiler.cs`, this plan touched `MidiExport.cs` only.

## Deviations from Plan

**Auto-fixed Issues**

**1. [Rule 3 - Blocking] DryWetMidi `TicksPerQuarterNoteTimeDivision` ctor takes `short`, not `int`**
- **Found during:** Task 1 first build attempt
- **Issue:** `dotnet build` reported `error CS1503: Argument 1: cannot convert from 'int' to 'short'` at the `TimeDivision = new TicksPerQuarterNoteTimeDivision(ticksPerQuarter)` line. The original `TicksPerQuarterNote` const compiled implicitly because const literal `480` has a compile-time `short` conversion; runtime `int` requires an explicit cast.
- **Fix:** Added `(short)ticksPerQuarter` cast at the TimeDivision setter site (line 153). Cast is safe by construction because `MaxTpqn=9600 < short.MaxValue=32767`. Inline comment cites the bound: `// ticksPerQuarter is bounded by MaxTpqn (9600) which fits short.MaxValue (32767).`
- **Files modified:** `flow-lang/StandardLibrary/Audio/MidiExport.cs` (1 line, plus comment)
- **Severity:** Minor — caught at build time, fix is one keyword.

**2. [Rule 3 - Blocking] Test scripts need `use "@std"` + `use "@audio"` to import `writeMidi`**
- **Found during:** Task 2 first test run
- **Issue:** All 6 Facts failed with `Script failed: errorCount=1, stderr=<test>:8:21: error: Function 'writeMidi' not found`. The `writeMidi` proc is declared in `flow-lang/audio.flow:408` as an internal binding to `MidiExport.WriteMidi` and is NOT auto-loaded — `use` directives are required (verified against `tests/test_midi_export.flow:1-2`).
- **Fix:** Added `use "@std"` + `use "@audio"` to all 6 Fact source strings via `replace_all = true` on the common `tempo 120 {` prefix.
- **Files modified:** `flow-lang.Tests/Unit/Phase19/MidiTpqnElevationTests.cs` (6 sites, replace_all)
- **Severity:** Minor — catches the standard Flow module-import idiom that the plan template did not show.

**3. [Rule 3 - Blocking] Plan template's `Section verse = section verse { s }` is NOT canonical Flow syntax**
- **Found during:** Test source authoring (anticipated from §interfaces "confirm at task time" note)
- **Issue:** Plan §action Step 2 showed `Sequence s = | ... |` followed by `Section verse = section verse { s }`. The actual canonical form (verified against `tests/test_midi_export.flow:13-22`) is a top-level `section verse { ... }` declaration containing the note-stream directly, followed by `Song song = [verse]` + `(writeMidi "..." song)`.
- **Fix:** All 6 Fact source strings use the canonical form: `tempo 120 { timesig 4/4 { section verse { | ... | } Song song = [verse] (writeMidi "..." song) } }`.
- **Files modified:** `flow-lang.Tests/Unit/Phase19/MidiTpqnElevationTests.cs` (initial authorship — never landed broken)
- **Severity:** Significant for plan template, zero impact on shipped code (caught pre-build).

No Rule 4 architectural decisions. No auth gates. No bugs surfacing in unrelated files (out-of-scope detection clean).

## Atomic Commit

**`dbc6f30`** — `feat(19-04): TUP-06 MIDI TPQN auto-elevation + 9600 cap error`

Files: 2 (1 modified + 1 created)
Insertions: 260 lines
Deletions: 8 lines
No accidental file deletions (post-commit `git diff --diff-filter=D HEAD~1 HEAD` empty).

## Phase 19 Forward-Readiness

This plan completes Wave 3 of Phase 19. Plan 19-05 (TUP-07 augment/diminish regression + AUDIT-VERIFIED comment refresh + closure) can now proceed:

- **Plan 19-05 (TUP-07 augment/diminish):** Touches `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (AUDIT-VERIFIED comment update at lines 239,261) + creates `flow-lang.Tests/Unit/Phase19/TupletAugmentDiminishTests.cs`. Independent of this plan's MidiExport surface — uses Fraction `*` operator from Phase 18 + DurationFraction propagation from Plans 19-01/02. Closure plan also writes `19-VERIFICATION.md` rollup with criteria-to-artifact mapping + commit hash manifest (this plan's commit `dbc6f30` rolls in there), flips REQUIREMENTS.md traceability for TUP-01..08, advances STATE.md.

## Self-Check: PASSED

- ✓ `flow-lang/StandardLibrary/Audio/MidiExport.cs` exists; key markers verified via grep:
  - `private const int MaxTpqn = 9600` (1 hit, line 26)
  - `private static int Gcd` (1 hit, line 32)
  - `private static int Lcm` (1 hit, line 37)
  - `private static int ComputeRequiredTpqn` (1 hit, line 50)
  - `MIDI export requires TPQN=` (1 hit, line 72)
  - `exceeds cap` + `locked v1.3 D-05` (1 hit, line 72)
  - `Tuplet ratios in this song:` (1 hit, line 73)
  - `int ticksPerQuarter = ComputeRequiredTpqn(song)` (1 hit, line 149)
  - `TicksPerQuarterNoteTimeDivision((short)ticksPerQuarter)` (1 hit, line 153)
  - 4 multiplication sites + TimeDivision setter all use `ticksPerQuarter`; 0 `* TicksPerQuarterNote` remain
- ✓ `flow-lang.Tests/Unit/Phase19/MidiTpqnElevationTests.cs` exists; 6 [Fact] attributes; namespace `FlowLang.Tests.Unit.Phase19`; `[Collection("FlowScripts")]` gating
- ✓ All 6 test method names present (Triplet_StaysAt480, Quintuplet_StaysAt480, Septuplet_ElevatesTo3360, LargeRatioCombination_RaisesCapError, ZeroTuplets_StaysAt480, PerNoteSeptuplet_ElevatesTo3360)
- ✓ 3 `Assert.Equal(480, tpqn)` (Triplet, Quintuplet, ZeroTuplets); 2 `Assert.Equal(3360, tpqn)` (Septuplet bracket, per-note)
- ✓ 1 `exceeds cap 9600` substring assertion (cap-error Fact)
- ✓ Build clean: `dotnet build flow-sharp.sln` → 0 errors, 11 pre-existing warnings (out of scope)
- ✓ Phase19.MidiTpqnElevationTests: 6/6 passed
- ✓ Phase19 cumulative: 29/29 passed (8 + 9 + 6 + 6)
- ✓ Phase18 byte-identical regression: 19/19 passed (held)
- ✓ Full suite: 335/335 passed (= 306 + 29)
- ✓ Commit `dbc6f30` exists in HEAD: `git log --oneline -1` returns `dbc6f30 feat(19-04): TUP-06 MIDI TPQN auto-elevation + 9600 cap error`
- ✓ No accidental file deletions: `git diff --diff-filter=D --name-only HEAD~1 HEAD` empty

---

*Phase: 19-tuplets-arbitrary-fractional-durations*
*Plan: 19-04*
*Atomic commit: dbc6f30*
*Completed: 2026-04-26*
