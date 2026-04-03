---
phase: 04-composition-tools
verified: 2026-04-02T00:00:00Z
status: gaps_found
score: 6/8 must-haves verified
gaps:
  - truth: "COMP-01 and COMP-02 are reflected as complete in REQUIREMENTS.md"
    status: failed
    reason: "REQUIREMENTS.md still marks COMP-01 and COMP-02 as Pending (unchecked) even though both are fully implemented in code. The traceability table also shows 'Pending' for both."
    artifacts:
      - path: ".planning/REQUIREMENTS.md"
        issue: "Lines 31-32 show [ ] COMP-01 and [ ] COMP-02; lines 99-100 show 'Pending' in traceability table"
    missing:
      - "Update REQUIREMENTS.md line 31: change '- [ ] **COMP-01**' to '- [x] **COMP-01**'"
      - "Update REQUIREMENTS.md line 32: change '- [ ] **COMP-02**' to '- [x] **COMP-02**'"
      - "Update REQUIREMENTS.md line 99: change 'Pending' to 'Complete' for COMP-01"
      - "Update REQUIREMENTS.md line 100: change 'Pending' to 'Complete' for COMP-02"
  - truth: "vary() rhythm mutation splits notes into correct subdivisions (e.g., quarter -> two eighths)"
    status: failed
    reason: "VariationFunctions.MutateRhythm uses wrong integer values in the switch. NoteValueType.Value enum is: WHOLE=0, HALF=1, QUARTER=2, EIGHTH=3, SIXTEENTH=4. The switch uses case 1=>2, 2=>4, 4=>8, 8=>16 which maps DurationValue=2 (QUARTER) to halfDuration=4 (SIXTEENTH), not EIGHTH(3). A quarter note gets split into two sixteenth notes instead of two eighth notes."
    artifacts:
      - path: "flow-lang/StandardLibrary/Composition/VariationFunctions.cs"
        issue: "MutateRhythm switch at line 253: case values treat DurationValue as beat fractions (1=whole, 2=half, 4=quarter) instead of NoteValueType enum integers (WHOLE=0, HALF=1, QUARTER=2, EIGHTH=3, SIXTEENTH=4)"
    missing:
      - "Fix MutateRhythm switch to use correct enum values: case 0 => 1 (WHOLE->HALF), case 1 => 2 (HALF->QUARTER), case 2 => 3 (QUARTER->EIGHTH), case 3 => 4 (EIGHTH->SIXTEENTH)"
human_verification:
  - test: "Play a chord progression and verify voice leading"
    expected: "progression | I IV vi V | in key Cmajor produces chords where upper voices move by minimal intervals (e.g., C major -> F major: E stays near E, G moves to F or A)"
    why_human: "Cannot verify audio quality or voice-leading smoothness without playback"
  - test: "Play a polyrhythm and verify cycle alignment"
    expected: "polyrhythm(waltz_3_4, groove_4_4) produces audio where the 3/4 and 4/4 patterns cycle together over 12 beats (LCM of 3 and 4)"
    why_human: "Audio output verification requires playback"
---

# Phase 4: Composition Tools Verification Report

**Phase Goal:** Users can write chord progressions with automatic voicing, layer polyrhythmic patterns, and generate probabilistic variations of sequences
**Verified:** 2026-04-02
**Status:** gaps_found
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | User can write `progression \| I IV vi V \|` inside a key block and get a Sequence value | VERIFIED | `ParseProgressionExpression` in Parser.cs, `EvaluateProgression` in ExpressionEvaluator.cs, `ProgressionCompiler.Compile` returns `SequenceData` wrapped in `Value.Sequence` |
| 2 | Adjacent chords use voice leading with minimal upper-voice movement | VERIFIED | `ApplyVoiceLeading` in ProgressionCompiler.cs finds nearest chord tone for each upper voice using greedy nearest-neighbor search across MIDI range 48-84 |
| 3 | User can overlay sequences with different time signatures via `polyrhythm()` and get a Buffer | VERIFIED | `PolyrhythmFunctions.Polyrhythm` renders each sequence independently, calculates LCM of time signature numerators, loops voices, and mixes via `SongRenderer.MixVoicesToStereoBuffer` |
| 4 | User can generate probabilistic variations via `vary(sequence, 0.3)` | VERIFIED | `VariationFunctions` registers six overloads, `ApplyVariation` iterates bars/notes and applies mutation by probability |
| 5 | Pitch mutations stay diatonic when key parameter is provided | VERIFIED | `MutatePitchDiatonic` calls `ScaleDatabase.GetScaleNotes(keyContext)` and shifts by scale degree index, line 167 |
| 6 | Progression outside key block produces clear error message | VERIFIED | `EvaluateProgression` checks `context.Key == null` and reports: "progression requires an active key context (use `key Cmajor { ... }`)" |
| 7 | COMP-01 and COMP-02 reflected as complete in REQUIREMENTS.md | FAILED | REQUIREMENTS.md lines 31-32 still show `[ ]` unchecked; traceability table lines 99-100 still show 'Pending' |
| 8 | vary() rhythm mutation splits notes into correct subdivisions | FAILED | `MutateRhythm` switch uses wrong integer values. `case 2 => 4` maps QUARTER(2) to SIXTEENTH(4), should map to EIGHTH(3) |

**Score:** 6/8 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `flow-lang/Ast/Expressions/ProgressionExpression.cs` | ProgressionExpression and ProgressionElement AST record types | VERIFIED | 23 lines; both records present with Numeral, BarCount, VoiceCount fields |
| `flow-lang/Runtime/ProgressionCompiler.cs` | Compiles ProgressionExpression into SequenceData with voice leading | VERIFIED | 355 lines; contains Compile, InitializeVoices, ApplyVoiceLeading, BuildBar |
| `flow-lang/StandardLibrary/Composition/PolyrhythmFunctions.cs` | polyrhythm() built-in with LCM calculation | VERIFIED | 120 lines; class PolyrhythmFunctions with Lcm/Gcd helpers and voice looping |
| `flow-lang/StandardLibrary/Composition/VariationFunctions.cs` | vary() built-in with four mutation types and diatonic pitch support | VERIFIED (with bug) | 379 lines; six overloads, four mutation types, diatonic path via ScaleDatabase; rhythm mutation values incorrect |
| `tests/test_progression.flow` | Integration test for progression DSL | NOT COMMITTED | In .gitignore; exists in worktree per SUMMARY but not tracked |
| `tests/test_voice_leading.flow` | Integration test for voice leading | NOT COMMITTED | In .gitignore; exists in worktree per SUMMARY but not tracked |
| `tests/test_polyrhythm.flow` | Integration test for polyrhythm | NOT COMMITTED | In .gitignore; exists in worktree per SUMMARY but not tracked |
| `tests/test_variation.flow` | Integration test for vary() | NOT COMMITTED | In .gitignore; exists in worktree per SUMMARY but not tracked |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `SimpleLexer.cs` | `TokenType.cs` | `"progression" => TokenType.Progression` | VERIFIED | Line 595 of SimpleLexer.cs |
| `Parser.cs` | `ProgressionExpression.cs` | `ParseProgressionExpression` method | VERIFIED | Lines 763-1010 of Parser.cs; method calls `ScaleDatabase.IsRomanNumeral` for validation |
| `ExpressionEvaluator.cs` | `ProgressionCompiler.cs` | `EvaluateProgression` dispatch | VERIFIED | Line 46: `ProgressionExpression progression => EvaluateProgression(progression)`; line 457 creates `ProgressionCompiler` and calls `Compile` |
| `ProgressionCompiler.cs` | `ScaleDatabase.cs` | `ResolveRomanNumeral` | VERIFIED | Line 63 of ProgressionCompiler.cs |
| `PolyrhythmFunctions.cs` | `SequenceRenderer.cs` | `RenderSequenceToVoices` | VERIFIED | Lines 55-56 of PolyrhythmFunctions.cs |
| `PolyrhythmFunctions.cs` | `SongRenderer.cs` | `MixVoicesToStereoBuffer` (internal) | VERIFIED | Line 76 of PolyrhythmFunctions.cs; SongRenderer line 92 confirmed `internal` modifier |
| `VariationFunctions.cs` | `ScaleDatabase.cs` | `GetScaleNotes` for diatonic pitch mutation | VERIFIED | Line 167 of VariationFunctions.cs |
| `BuiltInFunctions.cs` | `PolyrhythmFunctions.cs` | `Composition.PolyrhythmFunctions.Register` | VERIFIED | Line 49 of BuiltInFunctions.cs |
| `BuiltInFunctions.cs` | `VariationFunctions.cs` | `Composition.VariationFunctions.Register` | VERIFIED | Line 50 of BuiltInFunctions.cs |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|---------------------|--------|
| `ProgressionCompiler.cs` | `SequenceData` sequence | `ScaleDatabase.ResolveRomanNumeral` -> chord tones -> MIDI pitch arithmetic | Yes -- resolves roman numerals to real chord notes, applies voice leading, builds MusicalNoteData | FLOWING |
| `PolyrhythmFunctions.cs` | `AudioBuffer` result | `SequenceRenderer.RenderSequenceToVoices` -> `SongRenderer.MixVoicesToStereoBuffer` | Yes -- renders each sequence to Voice objects, mixes to stereo buffer | FLOWING |
| `VariationFunctions.cs` | `SequenceData` result | Iterates input bars/notes, applies mutations, creates new MusicalNoteData | Yes -- builds new SequenceData; pitch via `ScaleDatabase.GetScaleNotes` or MIDI arithmetic | FLOWING (rhythm values incorrect) |

### Behavioral Spot-Checks

Behavioral spot-checks skipped: .NET 9 SDK not available in environment; only .NET 8.0 present. Cannot run `dotnet run` to test behavior. Code-level verification substituted above.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| COMP-01 | 04-01-PLAN.md | User can write chord progressions with a DSL that auto-generates voicings | SATISFIED (code) / STALE (docs) | Full pipeline implemented: lexer keyword, parser, AST, ProgressionCompiler with InitializeVoices. REQUIREMENTS.md not updated. |
| COMP-02 | 04-01-PLAN.md | Chord DSL resolves voice leading (minimal movement between chords) | SATISFIED (code) / STALE (docs) | `ApplyVoiceLeading` in ProgressionCompiler uses greedy nearest-neighbor. REQUIREMENTS.md not updated. |
| COMP-03 | 04-02-PLAN.md | User can write polyrhythmic patterns with overlapping time signatures | SATISFIED | PolyrhythmFunctions with LCM calculation and independent sequence rendering |
| COMP-04 | 04-02-PLAN.md | User can generate probabilistic pattern variations from a source sequence | SATISFIED (with caveat) | VariationFunctions with six overloads; rhythm mutation has incorrect enum values but other three mutation types (pitch, rest, velocity) are correct |

**Orphaned requirements:** None. All four COMP-0x requirements are claimed by plans and have implementation evidence.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `flow-lang/StandardLibrary/Composition/VariationFunctions.cs` | 253-259 | MutateRhythm switch uses beat-fraction integers (1, 2, 4, 8) instead of NoteValueType enum values (WHOLE=0, HALF=1, QUARTER=2, EIGHTH=3, SIXTEENTH=4) | Warning | "rhythm" mutation type maps quarter notes to two sixteenth notes instead of two eighth notes; all other mutation types (pitch, rest, velocity) are unaffected |
| `.planning/REQUIREMENTS.md` | 31-32, 99-100 | COMP-01 and COMP-02 still marked as `[ ]` Pending despite full implementation committed | Warning | Stale documentation; does not affect runtime behavior |

### Human Verification Required

#### 1. Voice Leading Audio Quality

**Test:** Run `dotnet run --project flow-interpreter` with a script containing:
```
key Cmajor {
  Sequence chords = progression | I IV vi V |
  chords -> renderSequence -> writeWav("progression_test.wav")
}
```
Open the WAV file in a DAW or audio viewer and inspect the four-voice chord block. Verify upper voices (alto, tenor, soprano) move by small intervals rather than jumping across octaves between each chord change.
**Expected:** C major -> F major transition should show the E voice staying near E (moving to F or staying at E), not jumping to F an octave away.
**Why human:** Voice leading smoothness is a perceptual quality judgment; the algorithm is correctly coded but correctness of the musical output requires listening.

#### 2. Polyrhythm Cycle Alignment

**Test:** Build and run a polyrhythm test:
```
key Cmajor {
  timesig 3/4 { Sequence waltz = | C4q E4q G4q | }
  timesig 4/4 { Sequence groove = | C4q D4q E4q F4q | }
  Buffer mixed = polyrhythm(waltz, groove)
  mixed -> writeWav("polyrhythm_test.wav")
}
```
Verify the output WAV is 12 beats long (LCM of 3 and 4) and both patterns repeat correctly to fill that duration.
**Expected:** 12-beat output; waltz plays 4 times (3 beats x 4), groove plays 3 times (4 beats x 3).
**Why human:** Cycle length and loop correctness require auditioning the audio or inspecting WAV metadata.

### Gaps Summary

Two gaps require attention:

**Gap 1: REQUIREMENTS.md not updated for COMP-01 and COMP-02.** The chord progression DSL (COMP-01) and voice leading algorithm (COMP-02) are fully implemented and committed (commits 6d85690, 43a74e7). However, REQUIREMENTS.md lines 31-32 still show `[ ]` unchecked checkbox and the traceability table shows 'Pending'. This is a documentation staleness issue -- no code changes required, only REQUIREMENTS.md edits.

**Gap 2: Rhythm mutation in `vary()` uses wrong enum ordinals.** `VariationFunctions.MutateRhythm` at line 253 uses a switch with cases `1=>2, 2=>4, 4=>8, 8=>16` treating `DurationValue` as if it were a traditional music denominator (whole=1, half=2, quarter=4, eighth=8). But the `NoteValueType.Value` enum is `WHOLE=0, HALF=1, QUARTER=2, EIGHTH=3, SIXTEENTH=4`. The practical consequence: a quarter note (`DurationValue=2`) gets split into two sixteenth notes (halfDuration=4, which maps to `SIXTEENTH`) instead of two eighth notes (which would be `halfDuration=3`). The fix is to change the switch to `case 0 => 1, case 1 => 2, case 2 => 3, case 3 => 4`. This does not block COMP-04 since three of the four mutation types (pitch, rest, velocity) work correctly.

---

_Verified: 2026-04-02_
_Verifier: Claude (gsd-verifier)_
