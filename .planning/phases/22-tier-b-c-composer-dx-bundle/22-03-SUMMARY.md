---
phase: 22-tier-b-c-composer-dx-bundle
plan: 03
subsystem: harmony
tags: [voicing, inversion, drop2, drop3, open, close, spread, charitable-interpretation, dx-11]

requires:
  - phase: 14-composer-dx-part-1
    provides: NoteType.Parse + NoteType.Format canonical "+/-" accidental round-trip
  - phase: 18-foundation-rational-duration-arithmetic
    provides: byte-identical regression gate (Tutorial WAV+MIDI, Showcase WAV+MIDI, Euclidean WAV+MIDI)
  - phase: 22-tier-b-c-composer-dx-bundle
    provides: 22-01 sibling-overload registration pattern; 22-02 Wave 0 RED stub pattern
provides:
  - "inversion(Chord, Int) -> Chord — rotates n lowest notes up an octave each"
  - "voicing(Chord, String) -> Chord — dispatches to drop2/drop3/open/close/spread named voicings"
  - "Voicings static class in flow-lang/StandardLibrary/Harmony/ — pure ChordData -> ChordData transforms"
  - "Charitable D-07 paths for every voicing: incomplete chord OR unknown name OR out-of-range n returns input unchanged"
  - "D-08 doc-comment compliance: every voicing function cites 'See Phase 22 CONTEXT D-07' (7 occurrences in Voicings.cs)"
affects: [22-04-delay-sync, 22-05-quantize, 22-06-legato-portamento, 22-07-closure]

tech-stack:
  added: []
  patterns:
    - "Static helper class registered at the TOP of HarmonyFunctions.Register so chord-shape transforms are visible before the chord-using helpers"
    - "Charitable D-07: every voicing branch short-circuits to `return input` when the minimum-note-count requirement isn't met OR the name is unknown — no error path on chord-shape transforms"
    - "NoteType round-trip canonicalization: every octave manipulation goes through Parse + Format so the '+'/'-' accidental form is preserved exactly (Pitfall 5)"

key-files:
  created:
    - flow-lang/StandardLibrary/Harmony/Voicings.cs
    - flow-lang.Tests/Unit/Phase22/VoicingFacts.cs
    - tests/test_dx_voicings.flow
  modified:
    - flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs
    - flow-lang/std.flow
    - flow-lang.Tests/FlowScriptData.cs

key-decisions:
  - "DX-11 ships a NEW file Voicings.cs (sibling to ChordParser.cs) rather than extending HarmonyFunctions.cs in place — voicing semantics are a self-contained chord-shape transform tier and the file boundary makes the D-07 charitable contract easier to grep"
  - "Voicings.Register is the FIRST line of HarmonyFunctions.Register so the chord-shape transforms are visible to subsequent harmony helpers in the same registration pass; existing chord/arpeggio registrations remain in their original order below"
  - "Open voicing on a triad raises the MIDDLE note (index 1) an octave — chosen for the canonical [C E G] -> [C G E5] visual spread; documented in Voicings.Open doc comment"
  - "Close voicing collapses notes that sit MORE than 12 semitones above the root via a `while (midi - rootMidi > 12) oct--` loop — works for arbitrary input voicings, not just the inverse of `open`"
  - "Spread voicing raises only the HIGHEST note an octave (one-step widening) — keeps the transform idempotent-ish and predictable; composers can stack `(spread (spread chord))` for wider spacing"
  - "Wave 0 RED stub used `NotImplementedException`-throwing static methods so the test file compiles while every Fact remains RED (mirrors 22-02 Wave 0 stub pattern)"

patterns-established:
  - "Pattern: chord-shape transform tier file — sibling to ChordParser.cs, pure static class on ChordData, registered via Register(InternalFunctionRegistry) wired from HarmonyFunctions.Register"
  - "Pattern: D-08 doc-comment compliance — every public/private voicing helper cites 'See Phase 22 CONTEXT D-07' so users (and future maintainers) can grep their way to the explanation when they hit the unchanged-result case"
  - "Pattern: charitable interpretation as switch-default — `_ => input` in the Voicing(name) dispatch table makes the unknown-name case visually adjacent to the recognized cases and impossible to miss when adding a new voicing"

requirements-completed: [DX-11]

duration: 4min
completed: 2026-05-02
---

# Phase 22 Plan 03: DX-11 Inversion + Voicing Summary

**`inversion(Chord, Int)` and `voicing(Chord, String)` ship five named voicings (drop2/drop3/open/close/spread) with charitable D-07 fallback when chords lack the required note count.**

## Performance

- **Duration:** ~4 min (246 s wall clock)
- **Started:** 2026-05-02T19:01:17Z
- **Completed:** 2026-05-02T19:05:23Z
- **Tasks:** 3 (RED + GREEN + verify)
- **Files modified:** 6 (3 created, 3 modified)

## Accomplishments

- DX-11 closed: `inversion(Chord, Int)` and `voicing(Chord, String)` both registered as Flow built-ins
- Five named voicings — drop2, drop3, open, close, spread — implemented in a new `Voicings` static class in `flow-lang/StandardLibrary/Harmony/`
- Charitable D-07 paths verified for every shape: incomplete chord (drop2/drop3 < 4 notes; open/close/spread < 3), out-of-range n (`n <= 0 || n >= NoteNames.Length`), and unknown voicing names — all return the input chord unchanged with no error and no warning
- D-08 doc-comment compliance: every voicing helper cites "See Phase 22 CONTEXT D-07" — `grep -c "See Phase 22 CONTEXT D-07"` returns **7** (≥ 5 required)
- Pitfall 5 canonicalization: every octave manipulation routes through `NoteType.Parse` + `NoteType.Format` so the `"+"` accidental form round-trips exactly (verified via the `NoteNames_PreserveCanonicalAccidental` Fact on `F#dim` -> `["F4+", "A4", "C5"]` -> inversion 1 -> `["A4", "C5", "F5+"]`)
- 17 VoicingFacts GREEN; `tests/test_dx_voicings.flow` exits 0 with `DX-11 voicings: PASSED` sentinel
- Smoke output: `(inversion Cmaj 1) -> Cmaj [E4 G4 C5]`, `(voicing Cmaj7 "drop2") -> Cmaj7 [G3 C4 E4 B4]`, `(voicing Cmaj "drop2") -> Cmaj [C4 E4 G4]` (D-07 unchanged)
- ByteIdentical regression gate **6/6** GREEN (Tutorial WAV+MIDI, Showcase WAV+MIDI, Euclidean WAV+MIDI) — existing chord-using scripts unaffected
- Full test suite **454/454** GREEN — zero regressions (was 436/436 at 22-02 close + 17 new VoicingFacts + 1 new sentinel theory row = 454)

## Task Commits

Each task was committed atomically:

1. **Task 1: Wave 0 RED — Failing VoicingFacts + DX-11 smoke** — `67a8c52` (test)
   - 17 xUnit Facts (12+ required by plan): 15 direct C# helper assertions + 2 engine-eval gates
   - `tests/test_dx_voicings.flow` smoke script with `DX-11 voicings: PASSED` sentinel
   - `flow-lang.Tests/FlowScriptData.cs` sentinel entry
   - `Voicings.cs` Wave 0 RED stub (`NotImplementedException`) so the test project compiles while every Fact stays RED
2. **Task 2: Wave 2 GREEN — Implement Voicings.cs** — `5fba059` (feat)
   - `Voicings.Inversion(chord, n)` rotates n lowest notes up an octave each
   - `Voicings.Voicing(chord, name)` switch-dispatches to Drop2/Drop3/Open/Close/Spread
   - 7 doc comments cite "See Phase 22 CONTEXT D-07" (D-08 compliance)
   - `Voicings.Register(registry)` wired at the top of `HarmonyFunctions.Register`
   - `flow-lang/std.flow` declares `internal proc inversion (Chord: chord, Int: n)` and `internal proc voicing (Chord: chord, String: name)`
   - All 17 VoicingFacts flipped GREEN
3. **Task 3: Wave 2 — Smoke run + byte-identical regression gate** — `822149d` (chore, verification-only empty commit)
   - `dotnet run --project flow-interpreter tests/test_dx_voicings.flow` -> exit 0, sentinel printed, all three chord prints match expected values
   - VoicingFacts 17/17 GREEN; ByteIdentical 6/6 GREEN; full suite 454/454 GREEN

## Files Created/Modified

- `flow-lang/StandardLibrary/Harmony/Voicings.cs` (created) — `Voicings` static class with `Register`, `Inversion`, `Voicing`, and five private voicing helpers (Drop2/Drop3/Open/Close/Spread); private `RaiseOctave`/`LowerOctave`/`CompareByPitch` helpers all route through `NoteType.Parse` + `NoteType.Format`
- `flow-lang.Tests/Unit/Phase22/VoicingFacts.cs` (created) — 17 xUnit Facts: FirstInversion_RaisesLowestNoteOctave, SecondInversion_RaisesTwoLowestNotes, Inversion_NEqualsZero/NGreaterEqualNoteCount/NegativeN_ReturnsUnchanged, Drop2_LowersSecondFromTop, Drop2/Drop3_OnTriad_ReturnsUnchanged, Drop3_LowersThirdFromTop, Open_OnTriad_DoublesRangeViaOctaveSpread, Close_ReturnsTightlyVoicedChord, Spread_OnTriad_DoublesRangeBetweenLowestHighest, Spread_OnDyad_ReturnsUnchanged, Voicing_UnknownName_ReturnsUnchanged, NoteNames_PreserveCanonicalAccidental, Inversion/Voicing_RegisteredViaEngine
- `tests/test_dx_voicings.flow` (created) — Smoke script: `(inversion Cmaj 1)`, `(voicing Cmaj7 "drop2")`, `(voicing Cmaj "drop2")` (D-07 unchanged), prints all three chord values plus `DX-11 voicings: PASSED`
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` (modified) — Single addition: `Voicings.Register(registry);` as the first line of `Register` body, with explanatory comment
- `flow-lang/std.flow` (modified) — Two new `internal proc` declarations next to existing arpeggio procs
- `flow-lang.Tests/FlowScriptData.cs` (modified) — `RequiredSentinels` entry for `test_dx_voicings.flow` pinning the `DX-11 voicings: PASSED` sentinel

## Decisions Made

- **New file Voicings.cs** (not extending HarmonyFunctions.cs in place): Voicing semantics are a self-contained chord-shape transform tier. The file boundary makes the D-07 charitable contract easier to grep (every match for "See Phase 22 CONTEXT D-07" lives in one file) and lines up with the PATTERNS.md guidance ("New File... Closest Analog: ChordParser.cs"). HarmonyFunctions.cs gets a single one-line addition (`Voicings.Register(registry);`) with an explanatory comment.
- **Voicings.Register is registered FIRST**: Placed at the top of `HarmonyFunctions.Register` so the chord-shape transforms are visible to any subsequent registration in the same pass (none today, but future plans may chain). Existing chord/arpeggio registrations stay in their original order below — no churn for the regression gate.
- **Open voicing raises the MIDDLE note (index 1)**: For triads `[C E G]` the canonical "open" voicing is `[C G E5]` (E moved up an octave, G now in the middle). For 4-note chords this becomes "second-from-bottom up an octave" which still produces the wider-spacing visual. Documented in `Voicings.Open` doc comment.
- **Close voicing collapses by walking from the root**: Implementation walks each non-root note and `while (midi - rootMidi > 12) oct--`, so it works on arbitrary inputs (not just the inverse of `open`). Idempotent on already-close chords.
- **Spread voicing raises ONLY the highest note**: One-step widening per spread call. Composers can stack `(spread (spread chord))` for wider spacing. Avoids the ambiguity of "how much spread is spread?".
- **Wave 0 RED stub uses NotImplementedException**: Mirrors the 22-02 LoadWavVarispeed pattern — keeps the test project compiling at Task 1 while every Fact stays RED. The Task 2 GREEN commit replaces the stub body in-place; the file is created in Task 1 and never deleted, so git history shows the file evolving rather than being deleted-and-recreated.
- **Charitable interpretation as switch-default**: `_ => input` in the `Voicing(name)` dispatch table makes the unknown-name case visually adjacent to the recognized cases and impossible to miss when adding a new voicing in a future phase. The same pattern is documented as PATTERNS.md "Charitable interpretation".

## Deviations from Plan

**Total deviations:** 2 minor (1 plan-vs-pattern reconciliation, 1 docs/counting echo from 22-01/22-02)
**Impact on plan:** None — both deviations follow established Phase 22 conventions; verification still GREEN.

### 1. [Rule 3 - Blocking, then Rule 1 - Plan-text] Wave 0 RED needed a stub Voicings.cs to compile

- **Found during:** Task 1 (RED scaffolding)
- **Issue:** Plan's Task 1 acceptance criteria require `dotnet build flow-lang.Tests/flow-lang.Tests.csproj` to succeed AND `dotnet test --filter VoicingFacts` to report >= 10 FAILED tests. But Task 1's `<files>` block only lists test files (`VoicingFacts.cs`, `test_dx_voicings.flow`, `FlowScriptData.cs`) — Voicings.cs is reserved for Task 2. Without a Voicings stub, VoicingFacts.cs fails to compile (references `Voicings.Inversion`, `Voicings.Voicing`), which means the test project fails to build, which fails the build acceptance gate.
- **Fix:** Created Voicings.cs in Task 1 as a Wave 0 RED stub (three methods that throw `NotImplementedException`). Mirrors the 22-02 LoadWavVarispeed Wave 0 pattern (Task 1 created `LoadWavSemitones` / `LoadWavRatio` / `VarispeedResample` stubs to keep the build green while assertions stayed RED).
- **Files modified:** `flow-lang/StandardLibrary/Harmony/Voicings.cs` (created in Task 1, body replaced in Task 2)
- **Verification:** Task 1 build succeeds; Task 1 test run reports 17 RED VoicingFacts (all throw `NotImplementedException` from the stub). Task 2 GREEN commit replaces the stub body — same file, no rename or delete-recreate.
- **Committed in:** `67a8c52` (Task 1 RED stub) -> `5fba059` (Task 2 GREEN body)

### 2. [Documentation] Plan referenced "ByteIdentical 19/19" but actual count is 6

- **Found during:** Task 3 (verification gate)
- **Issue:** Plan's `<verification>` and `<success_criteria>` blocks reference `ByteIdenticalTutorialTests + ByteIdenticalShowcaseTests stay 19/19 GREEN`. The actual byte-identical regression gate consists of 6 tests across 3 classes: `ByteIdenticalTutorialTests` (2: WAV + MIDI), `ByteIdenticalShowcaseTests` (2: WAV + MIDI), `EuclideanByteIdenticalTests` (2: WAV + MIDI). Same documentation lag observed and corrected in 22-01 and 22-02.
- **Fix:** Documented actual count (6/6) in Task 3 commit message and this summary. No code change required.
- **Files modified:** none (commit message + this SUMMARY only)
- **Verification:** `dotnet test --filter ByteIdentical` enumerates and runs 6 tests; all 6 GREEN.

## Issues Encountered

- **`tests/` directory is gitignored**: First `git add tests/test_dx_voicings.flow` would have been rejected because `.gitignore` line 7 (`tests/`) blocks the path. Resolved with `git add -f` — same convention as 22-01 (`test_dx_arpeggio.flow`) and 22-02 (`test_dx_loadwav_varispeed.flow`).

## Threat Surface — STRIDE Compliance

The plan's `<threat_model>` lists four mitigation dispositions. All four are honored by the GREEN implementation:

| Threat ID | Disposition | Mitigation in Voicings.cs |
|-----------|-------------|---------------------------|
| T-22-V5-04 (Tampering: voicing name string) | mitigate | Switch default `_ => input` returns input unchanged (D-07). Unknown name cannot crash runtime — verified by `Voicing_UnknownName_ReturnsUnchanged` Fact. |
| T-22-V5-05 (DoS: inversion n unbounded) | mitigate | `n <= 0 \|\| n >= input.NoteNames.Length` short-circuit returns input unchanged. No octave-overflow loop possible — verified by `Inversion_NGreaterEqualNoteCount_ReturnsUnchanged` and `Inversion_NegativeN_ReturnsUnchanged` Facts. |
| T-22-V5-06 (Tampering: NoteType.Format octave overflow) | accept | Existing NoteType.Format handles octave shifts within MIDI range; the only octave shifts in Voicings.cs are +/- 1 from already-valid input notes (Cmaj is `["C4","E4","G4"]` so RaiseOctave produces C5/D5/G5 — well within range). |
| T-22-V5-07 (Repudiation: chord re-sort nondeterminism) | mitigate | `notes.Sort(CompareByPitch)` uses deterministic `NoteType.ToMidiNote` comparison; no PRNG. ByteIdentical 6/6 GREEN confirms downstream determinism. |

## Next Phase Readiness

- DX-11 is the third of seven Phase 22 plans (22-01 DX-10 arpeggio + 22-02 DX-15 loadWav varispeed shipped Wave 1; 22-03 DX-11 closes Wave 2 alongside the running 22-02 sibling). 22-04 (DX-12 delay sync), 22-05 (DX-13 quantize), 22-06 (DX-14 legato/portamento), and 22-07 (closure) remain. None depend on this plan's outputs (per Phase 22 design — features are independently shippable).
- The chord-shape transform tier file (`Voicings.cs`) is now a clean precedent for any future plan that wants to extend chord operations without bloating `HarmonyFunctions.cs` (e.g., a hypothetical v1.4 `progression`/`voiceLeading` DSL could ship its own sibling file in `flow-lang/StandardLibrary/Harmony/`).
- The "D-08 doc-comment grep gate" pattern (every helper documents the charitable behavior with a citation) is a clean precedent for future phases that want their charitable-interpretation paths to be greppable from any working directory.
- Byte-identical regression gate (6/6) and full suite (454/454) prove the chord-shape transform tier did not perturb existing chord-using scripts — confirms the sibling-overload-and-sibling-file pattern is safe for downstream Phase 22 plans.

## Self-Check

Files verified:
- FOUND: `flow-lang/StandardLibrary/Harmony/Voicings.cs`
- FOUND: `flow-lang.Tests/Unit/Phase22/VoicingFacts.cs`
- FOUND: `tests/test_dx_voicings.flow`
- FOUND: `.planning/phases/22-tier-b-c-composer-dx-bundle/22-03-SUMMARY.md`

Commits verified:
- FOUND: `67a8c52` (Task 1 RED)
- FOUND: `5fba059` (Task 2 GREEN)
- FOUND: `822149d` (Task 3 verification)

## Self-Check: PASSED

---
*Phase: 22-tier-b-c-composer-dx-bundle*
*Completed: 2026-05-02*
