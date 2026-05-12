---
phase: 28-midi-audio-polyphony-articulation-rewrite
plan: 07
status: complete-pending-uat
requirements: [SPEC-9]
self_check: PASSED
test_count_before: 979
test_count_after: 985
new_facts: 6
commits:
  - (this commit) feat(28-07): ragtime fixtures + RagtimeFixtureTests + closure docs
key_files:
  created:
    - examples/tests/ragtime_polyphony.flow
    - examples/tests/maple_leaf_opening.flow
    - flow-lang.Tests/baselines/Phase28/ragtime_polyphony.wav
    - flow-lang.Tests/baselines/Phase28/maple_leaf_opening.wav
    - flow-lang.Tests/Integration/Phase28/RagtimeFixtureTests.cs
    - .planning/phases/28-midi-audio-polyphony-articulation-rewrite/28-VERIFICATION.md
  modified:
    - flow-lang/Parsing/Parser.NoteStream.cs (IsEndOfNoteStream allows LBrace continuation)
    - flow-lang.Tests/Helpers/RmsRegressionTests.cs (split AudioBuffer + WAV-path overloads)
    - .planning/ROADMAP.md (Phase 28 entry → 7/7 + Ready-for-UAT)
    - .planning/STATE.md (milestone v1.4 + Phase 28 status)
    - CLAUDE.md (voice blocks, articulation, multi-track MIDI, voicePool, RMS conventions)
---

## Plan 07 — Ragtime Fixtures + UAT-Ready Closure Docs

### What shipped

#### Ragtime fixtures (Tasks 1-3)

1. **`examples/tests/ragtime_polyphony.flow`** — synthetic 4-bar fixture
   exercising the four canonical Phase 28 scenarios:
   - Bar 1: held `C2w` under quarter-note inner voice (`C5q E5q G5q E5q`)
   - Bar 2: held `C2w` under staccato run (`C5q stacc D5q stacc E5q stacc F5q stacc`)
   - Bar 3: held `C2w` under legato run (`C5q leg D5q leg E5q leg F5q leg`)
   - Bar 4: mixed-articulation single-line (`C4q stacc D4q ten E4q > F4q marc`)

2. **`examples/tests/maple_leaf_opening.flow`** — first 8 bars of Scott
   Joplin's Maple Leaf Rag (1899, public domain). Stride pattern:
   alternating bass + mid-chord on each beat; right-hand syncopated
   melody. Voice blocks express LH stride + RH melody as parallel voices.

3. **Baseline WAVs committed**: `flow-lang.Tests/baselines/Phase28/ragtime_polyphony.wav`
   (2.4 MB) + `maple_leaf_opening.wav` (4.0 MB) — deterministic via the
   dither RNG seed (Phase 15 Plan 05). Used as RMS regression pins.

#### Voice-block parser hot-fix

Discovered while running the synthetic ragtime fixture: `IsEndOfNoteStream`
was rejecting `LBrace` as a stream-continuation token, so multi-bar
note streams that started a bar with `{voice ...}` parsed as
end-of-stream after the prior bar's closing pipe. Fixed at
`Parser.NoteStream.cs:458-462` — `LBrace` added to the
note-stream-element token list. Both voice blocks AND tuplet brackets
benefit (both start with `{`).

#### RmsRegressionTests refactor (file-path overload)

Discovered while pinning the Maple Leaf RMS regression: the original
`AssertRmsWithinTolerance(rendered, baseline)` overload double-dithers
when the rendered audio came from a Flow script that already wrote a
WAV via `(writeWav ...)`. The original wrote the buffer back to a temp
WAV via `FileIO.WriteWav` (second dither pass) then compared with the
baseline, producing 6 dB drift in some windows.

Split the helper into two overloads:
- `AssertRmsWithinTolerance(AudioBuffer, baseline)` — when the rendered
  buffer is fresh-from-synthesizer (NOT yet through FileIO.WriteWav);
  round-trips through dither so both compared buffers carry the same
  noise floor.
- `AssertWavMatchesBaseline(renderedPath, baselinePath)` — when both
  WAVs are already on disk (Flow script wrote one, baseline is the
  committed pin). Single read+compare, no double-dither.

Both overloads share the same `ValidateOverride` helper for the
non-default-tolerance-requires-overrideReason check.

#### RagtimeFixtureTests (Task 4)

6 integration facts pinning end-to-end Phase 28 acceptance:
- `Ragtime_SyntheticFixture_Renders` — exit 0 + non-empty WAV+MID
- `Ragtime_MapleLeaf_Renders` — exit 0 + non-empty WAV+MID
- `Ragtime_Synthetic_RmsRegression` — `AssertWavMatchesBaseline`
  ±0.5 dB / 100ms
- `Ragtime_MapleLeaf_RmsRegression` — same for Maple Leaf
- `Ragtime_Synthetic_MultiTrackMidi` — `MidiFile.Chunks.Count >= 2`
- `Ragtime_TwoRunDeterminism` — two runs of synthetic fixture produce
  byte-identical WAV AND MIDI (preserves Phase 18/25/27 contract under
  Phase 28's render path)

#### 28-VERIFICATION.md (Task 5) + ROADMAP.md / STATE.md / CLAUDE.md (Tasks 7-9)

- `28-VERIFICATION.md` written with 25-line SPEC acceptance checklist
  (23 boxes auto-checked from xUnit pins; 2 pending composer manual UAT)
  + Manual UAT Sign-off section with the two ragtime fixtures + optional
  DAW round-trip + closure block.
- ROADMAP.md Phase 28 entry → `7/7 complete` + `Status: READY-FOR-UAT`;
  Progress table row updated.
- STATE.md → milestone bumped from v1.3 to v1.4; Phase 28 status updated
  with resume instructions for the composer's UAT step.
- CLAUDE.md Language Features list extended with 5 new Phase 28 bullets
  (voice blocks, Articulation.Legato, locked envelope rules, multi-track
  MIDI, voicePool); Conventions section gains the RMS regression note.

#### Final verification (Task 10)

- `dotnet build` clean (0 warnings, 0 errors)
- `dotnet test flow-lang.Tests` GREEN: 985/985 pass, 28 sec
- All 4 smoke scripts (tutorial.flow, showcase.flow,
  ragtime_polyphony.flow, maple_leaf_opening.flow) exit 0 with non-empty
  WAV/MID output

### Manual UAT (Task 6) — DEFERRED

The plan's Task 6 (composer manual UAT) is BLOCKING for closure.
Cannot be performed by gsd-executor — requires real-speakers ear-checking
on a human's audio system. Two checkboxes remain `[ ]` in
28-VERIFICATION.md:
- `ragtime_polyphony.flow` listened — held notes sustain, articulations distinct
- `maple_leaf_opening.flow` listened — stride pattern audible, RH/LH separation clear

Composer next step: render both fixtures, listen, edit
28-VERIFICATION.md to flip both checkboxes from `[ ]` to `[x]` and fill
in sign-off dates. ROADMAP.md and STATE.md will then be flipped from
"Ready-for-UAT" to "Complete" with the closing date.

### Test counts

- Phase 28 facts: **111/111 GREEN** (4 + 17 + 55 + 5 + 9 + 9 + 6 + 6
  across Plans 01-07)
- Phase 22 LegatoFacts: **8/8 GREEN**
- Phase 18/25/27 ByteIdentical two-run determinism: **14/14 GREEN**
- Full unit suite: **985/985 GREEN**, three consecutive runs

### Self-Check: PASSED (autonomous portion)

Build clean, all targeted tests pass, full suite green three times,
no architectural deviations from PLAN beyond the parser hot-fix
(IsEndOfNoteStream LBrace continuation) which was a latent bug in
Plan 28-01 surfaced by the multi-bar voice-block fixture and is
strictly additive (no existing test changed behavior).

### Deviations

1. **Parser hot-fix in Plan 07 (not Plan 01)** — `IsEndOfNoteStream`
   missing `LBrace` continuation was a latent bug from Plan 28-01 that
   only surfaces with multi-bar voice-block fixtures. Plan 28-01's
   single-bar VoiceBlockParserTests passed because they didn't cross a
   bar boundary. Plan 28-07's fixtures forced the issue.

2. **RmsRegressionTests split into two overloads (not in Plan 06)** —
   the AudioBuffer overload's round-trip dither was correct for
   from-scratch synthesizer output (Plan 06's diagnostic tests) but
   wrong for already-on-disk output (Plan 07's ragtime fixtures). The
   file-path overload is the right tool for end-to-end script output.

3. **Mixed-articulation Bar 4 of synthetic ragtime simplified** —
   PLAN.md showed `[C4 E4 G4]q stacc [C4 E4 G4]q ten [C4 E4 G4]q. > [C4 E4 G4]q sfz`
   but the parser doesn't accept articulation tokens after chord
   brackets `[...]q`. Replaced with single-note articulations `C4q
   stacc D4q ten E4q > F4q marc` — exercises the same per-articulation
   audible differentiation. Sforzando has no parser articulation token
   in v1.4 (verified at the helper level via Plan 03's
   Sforzando_GenerateArticulationADSR_SpikesLeading15Percent fact).

### Hand-off

After composer flips both UAT checkboxes:
1. Edit ROADMAP.md row to `Complete` + add closing date
2. Edit STATE.md status to `complete`
3. Final closure commit: `docs(28-07): closure — UAT sign-off`
4. Phase 29 (Instrument Realism) plans already exist on dev —
   `/gsd-execute-phase 29` to begin
