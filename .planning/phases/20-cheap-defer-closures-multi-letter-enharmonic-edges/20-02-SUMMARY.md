---
phase: 20-cheap-defer-closures-multi-letter-enharmonic-edges
plan: 02
subsystem: harmony
tags: [defer-04, enharmonic, edges, migration, harmony]
requires:
  - Phase 14 plan 14-02 (HarmonyFunctions.Enharmonic + ComputeFlippedSpelling helper — natural-edge switch reuses LetterUp/Down/NaturalSemitoneOf for inverse path)
  - Phase 18 byte-identical regression gate (must stay green — structural invariant since DEFER-04 touches no audio path)
  - Plan 20-01 (DEFER-01 closure already shipped — wave 1)
provides:
  - HarmonyFunctions.Enharmonic naturals branch with 5-line edge switch (E↔Fb, F↔E#, B↔Cb octave+1, C↔B# octave-1)
  - In-key branch reordered to fire BEFORE natural-edge so diatonic preservation wins (D-USER-B / Phase 14 D-04 precedent extended to naturals)
  - Phase20/EnharmonicEdgesTests.cs (11 [Fact] + 1 [Theory] expanded to 13 InlineData rows = 24 total)
  - Phase14/EnharmonicTests.cs migration: 4 NoKey_NaturalUnchanged_* Facts renamed → NoKey_NaturalEdgeRespells_* with inverted assertions
  - tests/test_enharmonic_edges.flow integration test (6 sentinels)
  - FlowScriptData.cs RequiredSentinels entry for test_enharmonic_edges.flow
affects:
  - Phase 21 (Pragma System + H-alias) is now unblocked — DEFER-04 was the binding pre-ordering #3 dependency
  - tests/test_enharmonic.flow (existing Phase 14 file) — UNCHANGED by this plan; lines 6-7 silently print Fb4 / B#3 instead of E4 / C4 post-DEFER-04 but the Theory row has no RequiredSentinels so it stays GREEN (Pitfall 2 documented behavior)
tech-stack:
  added: []
  patterns:
    - "Switch-on-letter pattern in C# (idiomatic .NET expression switch with discard pattern for default branch — D/G/A unchanged)"
    - "In-key branch reordering: TryEnharmonicInKey moved before the alteration==0 fast path so diatonic preservation wins for naturals AND accidentals (extension of Phase 14 D-04)"
    - "Stdout-token extraction helper (last whitespace-separated token) for round-trip Theory body — runs (enharmonic input) twice via two FlowEngineRunner instances and asserts MIDI-equality"
key-files:
  created:
    - flow-lang.Tests/Unit/Phase20/EnharmonicEdgesTests.cs (11 Facts + 1 Theory, 233 lines)
    - tests/test_enharmonic_edges.flow (26 lines, 6 sentinels)
  modified:
    - flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs (Enharmonic naturals branch — 5-line switch + in-key branch reordered + XML doc updated)
    - flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs (4 Facts renamed + assertions inverted + class XML doc updated with migration note)
    - flow-lang.Tests/FlowScriptData.cs (RequiredSentinels entry for test_enharmonic_edges.flow appended after the 20-01 test_range entry)
decisions:
  - "In-key branch order: moved BEFORE the alteration==0 fast path. The plan's <interfaces> example shows the in-key branch firing only on the alteration!=0 path, but the success_criteria require `key Fmajor { (enharmonic E4) } → 'E4'` (E is diatonic). Without the reorder, the natural-edge would respell E4→Fb4 even inside Fmajor, violating D-USER-B. Rule 2 deviation (correctness — diatonic preservation is a contract, not an optimization)."
  - "Theory row count: plan said '1 [Theory]' but the InlineData spec listed 13 chromatic notes verbatim. xUnit treats each InlineData as a distinct test row, so the Theory expanded to 13. EnharmonicEdgesTests passing count is 24 = 11 Facts + 13 Theory rows (NOT 12). Plan's <acceptance_criteria> said '12/12 GREEN' which is faithfully covered by 24/24."
  - "Phase14 docstring breadcrumbs: the migrated Facts retain `Previously NoKey_NaturalUnchanged_*` lines in their XML doc comments to preserve the audit trail. Plan's literal-grep acceptance criterion ('does NOT contain NoKey_NaturalUnchanged_C4') is satisfied at the Fact-name level (zero `public void NoKey_NaturalUnchanged_*` methods) but technically violated at the docstring-text level. Migration shape (a) per Pitfall 1 explicitly endorsed audit-trail preservation, so the docstring breadcrumbs are intentional. Reported here for transparency."
metrics:
  duration: ~5min
  tasks: 2 (Task 1 + Task 2 bundled in atomic commit per plan)
  files: 5 (2 created, 3 modified)
  completed: 2026-04-26
---

# Phase 20 Plan 02: DEFER-04 multi-letter enharmonic edges Summary

DEFER-04 closure shipped: `enharmonic` naturals at letter-boundary edges (E/F/B/C) now respell to their multi-letter neighbor (E↔Fb same octave, F↔E# same octave, B↔Cb octave +1, C↔B# octave -1). D/G/A naturals continue to return unchanged. In-key diatonic preservation wins over edge respelling per D-USER-B. Atomically committed alongside the Phase 14 NoKey_NaturalUnchanged_* → NoKey_NaturalEdgeRespells_* migration (rename + re-pin), 24 new Phase20 EnharmonicEdgesTests, and a 6-sentinel integration script.

## What Was Built

**`HarmonyFunctions.Enharmonic`** naturals branch — `if (alteration == 0)` block now dispatches via letter switch:
- `'E'` → `Value.Note(NoteType.Format('F', octave, -1))` (Fb same octave, MIDI preserved)
- `'F'` → `Value.Note(NoteType.Format('E', octave, +1))` (E# same octave, MIDI preserved)
- `'B'` → `Value.Note(NoteType.Format('C', octave + 1, -1))` (Cb in next octave, MIDI preserved)
- `'C'` → `Value.Note(NoteType.Format('B', octave - 1, +1))` (B# in previous octave, MIDI preserved)
- `_` → `Value.Note(NoteType.Format(letter, octave, 0))` (D/G/A unchanged)

The accidental fall-through path (`alteration != 0`) continues to use `ComputeFlippedSpelling` unchanged — the existing helper already handles inverse cases (Fb4 → E4, B#3 → C4, E#4 → F4, Cb5 → B4) correctly via `LetterUp`/`LetterDown`.

**In-key branch reordered**: `TryEnharmonicInKey` now fires BEFORE the alteration==0 natural-edge so diatonic preservation wins for both naturals and accidentals. `key Fmajor { (enharmonic E4) }` returns "E4" (E is diatonic in F major), not "F4-". Phase 14 D-04 precedent (in-key respelling for accidentals) is now structurally extended to naturals — see Decisions for rationale.

**Phase20/EnharmonicEdgesTests.cs** (new, `[Collection("FlowScripts")]`) — 11 [Fact] + 1 [Theory] (13 InlineData rows = 13 test rows) = 24 total Facts:
1. `NoKey_E4_RespellsFb4` — E4 → "F4-"
2. `NoKey_F4_RespellsEsharp4` — F4 → "E4+"
3. `NoKey_B4_RespellsCb5` — B4 → "C5-" (octave +1)
4. `NoKey_C4_RespellsBsharp3` — C4 → "B3+" (octave -1)
5. `NoKey_D4_Unchanged` — D4 stays D4 (no edge per D-USER-C)
6. `NoKey_G4_Unchanged` — G4 stays G4
7. `NoKey_A4_Unchanged` — A4 stays A4
8. `RoundTrip_PitchEquivalent` ([Theory], 13 InlineData rows) — chromatic-12 + Db4/C#4/D#4/F#4/Bb4 round-trip via two enharmonic calls; MIDI-equality assertion (NOT string-equality per Pitfall 8)
9. `NoKey_Fb4_RoundTripsToE4` — pins ComputeFlippedSpelling LetterDown inverse path
10. `NoKey_Bsharp3_RoundTripsToC4` — pins ComputeFlippedSpelling LetterUp inverse path (octave bump)
11. `InKey_Fmajor_E4_PreservesDiatonic` — E is diatonic in Fmajor → in-key branch returns "E4" (D-USER-B)
12. `InKey_Bbmajor_E4_FallsThroughToEdge` — E is chromatic in Bbmajor → falls through to natural-edge → "F4-"

**Phase14/EnharmonicTests.cs** migration (rename + re-pin per 20-RESEARCH Pitfall 1, shape (a)):
- `NoKey_NaturalUnchanged_C4` → `NoKey_NaturalEdgeRespells_C4` — Assert.Contains("B3+")
- `NoKey_NaturalUnchanged_E4` → `NoKey_NaturalEdgeRespells_E4` — Assert.Contains("F4-")
- `NoKey_NaturalUnchanged_B4` → `NoKey_NaturalEdgeRespells_B4` — Assert.Contains("C5-")
- `NoKey_NaturalUnchanged_F4` → `NoKey_NaturalEdgeRespells_F4` — Assert.Contains("E4+")

Plus class-level XML doc block documenting the migration and per-Fact "Previously NoKey_NaturalUnchanged_*" breadcrumbs preserving the v1.2-deferred audit trail.

**`tests/test_enharmonic_edges.flow`** (new, 26 lines) — 6 sentinels:
1. `F4-` (E4 → Fb4)
2. `E4+` (F4 → E#4)
3. `C5-` (B4 → Cb5)
4. `B3+` (C4 → B#3)
5. `DGA naturals unchanged: ok`
6. `test_enharmonic_edges: PASSED`

Includes a `key Bbmajor { (enharmonic E4) }` block exercising the in-key chromatic fall-through path (E is chromatic in Bbmajor → falls to natural-edge → re-emits "F4-").

**`FlowScriptData.cs`** — RequiredSentinels entry pinning all 6 sentinels for the Theory row at `test_enharmonic_edges.flow`.

## Verification

- **Phase20.EnharmonicEdgesTests**: 24/24 GREEN (11 Facts + 13 Theory rows)
- **Phase14.EnharmonicTests**: 9/9 GREEN (5 unchanged: FlatToSharp_Db4, SharpToFlat_Fsharp3, InKey_Dbmajor_CsharpRespells, InKey_Cmajor_FsharpFallsBack, DoubleSharp_NonInvolutive_FdoubleSharp + 4 migrated)
- **FlowScriptTests**: 65/65 GREEN (added test_enharmonic_edges.flow row; was 64 pre-plan)
- **Phase18 byte-identical regression gate**: 19/19 GREEN — DEFER-04 does not touch DurationFraction or audio path; structural invariant preserved
- **Full xUnit suite**: 374/374 GREEN (349 prior + 24 new EnharmonicEdgesTests + 1 new FlowScriptTests Theory row = 374)
- **Atomic commit**: `d835336` (5 files: HarmonyFunctions.cs + EnharmonicEdgesTests.cs + EnharmonicTests.cs + test_enharmonic_edges.flow + FlowScriptData.cs)
- **`tests/test_enharmonic.flow` unchanged**: `git diff tests/test_enharmonic.flow` returns no output — Pitfall 2 silent stdout drift pattern intact

## Round-Trip Theory Coverage (13 chromatic notes)

| Input | MIDI | First enharmonic | Second enharmonic | Final MIDI |
|-------|------|------------------|-------------------|------------|
| C4    | 60   | B3+ (B#3)        | C4                | 60         |
| D4    | 62   | D4               | D4                | 62         |
| E4    | 64   | F4- (Fb4)        | E4                | 64         |
| F4    | 65   | E4+ (E#4)        | F4                | 65         |
| G4    | 67   | G4               | G4                | 67         |
| A4    | 69   | A4               | A4                | 69         |
| B4    | 71   | C5- (Cb5)        | B4                | 71         |
| C5    | 72   | B4+ (B#4)        | C5                | 72         |
| Db4   | 61   | C4+ (C#4)        | Db4               | 61         |
| C#4   | 61   | D4- (Db4)        | C#4               | 61         |
| D#4   | 63   | E4- (Eb4)        | D#4               | 63         |
| F#4   | 66   | G4- (Gb4)        | F#4               | 66         |
| Bb4   | 70   | A4+ (A#4)        | Bb4               | 70         |

All 13 rows MIDI-equal the input — pitch-equivalence holds. (String-equivalence does not in general — that's the Pitfall 8 caveat.)

## Deviations from Plan

### Rule 2 — In-key branch reorder (correctness)

**Found during:** Task 1, Step 1 (HarmonyFunctions.cs natural-edge switch)

**Issue:** The plan's `<interfaces>` block depicted the in-key branch firing ONLY on the `alteration != 0` path — meaning a natural input like E4 inside `key Fmajor` would skip TryEnharmonicInKey entirely and hit the new natural-edge switch, respelling E4 → Fb4. But the plan's `<success_criteria>` and the Phase 14 D-04 precedent (D-USER-B) require diatonic preservation: `key Fmajor { (enharmonic E4) }` must return "E4" because E is diatonic in F major. Test 11 (`InKey_Fmajor_E4_PreservesDiatonic`) would have gone RED.

**Fix:** Reordered the Enharmonic body so `TryEnharmonicInKey` fires BEFORE the `if (alteration == 0)` natural-edge switch. Now the in-key branch handles both naturals and accidentals symmetrically — only chromatic-in-key inputs fall through to the natural-edge or sharp-flat flip.

**Files modified:** flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs (Enharmonic body — moved 8 lines up)

**Commit:** d835336 (bundled atomically; never landed inconsistent state on HEAD)

### Rule 1 — Theory row expansion (count discrepancy)

**Found during:** Task 1, Step 2 (EnharmonicEdgesTests.cs authoring)

**Issue:** Plan said "12 Facts (11 Facts + 1 Theory)" and `<acceptance_criteria>` claimed `Phase20.EnharmonicEdgesTests returns 12/12 GREEN`. But the [Theory] InlineData spec in `<action>` listed 13 chromatic notes verbatim, and xUnit counts each InlineData as a distinct test row → 13 Theory rows. Effective Fact count is 24 = 11 [Fact] + 13 InlineData rows.

**Fix:** Authored EnharmonicEdgesTests.cs with exactly 11 [Fact] + 1 [Theory] (13 InlineData rows) per the literal spec. The 24/24 GREEN result faithfully covers the plan's 12/12 intent — if anything, it over-delivers by adding 12 extra test-row coverage.

**Files modified:** none (plan spec internally inconsistent; chose literal InlineData spec over headline count)

**Impact on full-suite count:** 374 instead of plan's projected 360. Still zero regression — both new and migrated Facts are GREEN.

### Reported (not a deviation per se) — Phase14 docstring audit-trail breadcrumbs

The 4 migrated Facts in `flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs` retain `/// Previously NoKey_NaturalUnchanged_* — see class XML doc for migration rationale.` lines in their XML docstrings. The plan's `<acceptance_criteria>` said:

> File `flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs` does NOT contain `NoKey_NaturalUnchanged_C4` (renamed away)

A literal grep finds 5 occurrences of `NoKey_NaturalUnchanged_` (1 in class doc + 4 in per-Fact "Previously" notes), all inside XML comments — zero `public void NoKey_NaturalUnchanged_*` methods exist. The plan's `<phase_context>` migration shape (a) explicitly endorses audit-trail preservation:

> Migration choice (D-USER-F): Per 20-RESEARCH Pitfall 1, two acceptable migration shapes — (a) rename + re-pin, (b) delete + replace. We choose (a) because it preserves the Phase14 directory's v1.2-deferred audit trail.

Spirit (no Fact methods named `NoKey_NaturalUnchanged_*`) satisfied; literal text grep technically violated. Documented for transparency rather than removed because removing the breadcrumbs would defeat the purpose of choosing migration shape (a).

## Auth Gates

None.

## Hand-off Notes

**Plan 20-03 (DEFER-05 slice negative-from-end):** Independent file (`Collections.SliceArray` / `SliceSequence`). Can run immediately. No dependency on this plan beyond the Phase 18 byte-identical invariant which remains GREEN.

**Plan 20-04 (closure migration):** No items added or removed by this plan. Standard closure work remains:
- REQUIREMENTS.md DEFER-04 row flip from Pending → Shipped d835336
- ROADMAP.md progress update for Phase 20
- deferred-items.md DEFER-04 strikethrough closure note (mirrors Phase 14 14-04 pattern, already established in 14-deferred-items.md)
- 14-deferred-items.md DEFER-04 entry: append closure note "Closed by Phase 20 plan 02 / commit d835336"

**Phase 21 (Pragma System + H-alias):** UNBLOCKED. The binding pre-ordering #3 dependency on DEFER-04 is now satisfied — H-sharp can respell to B# = C natural via the natural-edge switch. Phase 21 planning can proceed.

## Phase 18 Byte-Identical Gate

19/19 Phase18 Facts GREEN. DEFER-04 does not interact with `MusicalNoteData.DurationFraction`, `Fraction`, `GetBeats`, `SongRenderer`, `MidiExport`, or any audio path — the gate is a structural invariant. The natural-edge switch only modifies the string output of `enharmonic()`, which is an isolated stdlib call with no audio-pipeline integration.

## Self-Check: PASSED

- [x] flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs — `DEFER-04` literal present (`grep -c "DEFER-04" ...` returns 2)
- [x] flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs — `'B' => Value.Note(NoteType.Format('C', octave + 1, -1))` regex matches
- [x] flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs — `'C' => Value.Note(NoteType.Format('B', octave - 1, +1))` regex matches
- [x] flow-lang.Tests/Unit/Phase20/EnharmonicEdgesTests.cs — file exists, namespace `FlowLang.Tests.Unit.Phase20`, `[Collection("FlowScripts")]`
- [x] EnharmonicEdgesTests.cs — 11 [Fact] methods + 1 [Theory] (13 InlineData rows)
- [x] flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs — `NoKey_NaturalEdgeRespells_C4/E4/B4/F4` Fact methods present
- [x] flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs — zero `public void NoKey_NaturalUnchanged_*` Fact methods (only docstring breadcrumbs preserved)
- [x] tests/test_enharmonic_edges.flow — file exists, 6 sentinels, key Bbmajor block
- [x] flow-lang.Tests/FlowScriptData.cs — RequiredSentinels entry for `test_enharmonic_edges.flow` present
- [x] tests/test_enharmonic.flow — UNCHANGED (`git diff tests/test_enharmonic.flow` returns empty)
- [x] git log --oneline | grep d835336 → commit `feat(20-02): DEFER-04 multi-letter enharmonic edges...` exists
- [x] dotnet test flow-sharp.sln → 374/374 GREEN
- [x] dotnet test --filter "FullyQualifiedName~Phase20.EnharmonicEdgesTests" → 24/24 GREEN
- [x] dotnet test --filter "FullyQualifiedName~Phase14.EnharmonicTests" → 9/9 GREEN
- [x] dotnet test --filter "FullyQualifiedName~Phase18" → 19/19 GREEN
- [x] dotnet run --project flow-interpreter tests/test_enharmonic_edges.flow → exit 0, all 6 sentinels in stdout
