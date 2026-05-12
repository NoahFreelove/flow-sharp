---
slug: staccato-grace-note-artifact
status: fixed
trigger: |
  During Phase 28 manual UAT, composer reported every staccato note in
  examples/output/ragtime_polyphony.wav bar 2 sounds like it has a brief
  pre-attack "grace note" before the main note. Normal/legato/accent notes
  do NOT exhibit this — the artifact is staccato-specific. Phase 28's
  locked rule is Staccato = 25% duration + sustain=0 + release×0.5, and
  all 9 shipping synths route through SynthUtils.GenerateArticulationADSR
  (flow-lang/StandardLibrary/Audio/SynthUtils.cs:160-210). 985/985 unit
  tests pass — ArticulationRulesTests has ±5% audible-duration tolerance
  (likely below perceptual artifact threshold). This bug BLOCKS Phase 28
  UAT sign-off in .planning/phases/28-midi-audio-polyphony-articulation-rewrite/28-VERIFICATION.md.

  Related but orthogonal: composer also reported flow-midi importer mis-quantizes
  Q-E-E rhythm to E-rest-E-rest-E (Bug B in /gsd-debug briefing). That is
  NOT Phase 28 (ragtime_imported.flow has zero articulation markers) — it
  lives in flow-midi/Conversion/Quantizer.cs. Will be filed as a separate
  /gsd-debug session after this one resolves.
created: 2026-05-10
updated: 2026-05-10
---

# Debug Session: staccato-grace-note-artifact

## Symptoms

**Expected behavior:**
A staccato-marked note (e.g. `C5q stacc` in a Flow note stream) should
render as a single short pluck: one attack, fast decay, no audible
secondary event. Per Phase 28 locked rules: rendered duration = 25% of
authored duration, envelope sustain = 0, release = baseRelease × 0.5.

**Actual behavior:**
Every staccato note has an audible "grace note" pre-attack — a brief blip
BEFORE the actual note. Composer described it as: "Every staccato note
specifically has the grace note, not normal notes." Normal/legato/accent
notes render cleanly with a single attack.

**Error messages:** None — failure is purely auditory. All xUnit tests
(985/985) pass, including:
  - ArticulationRulesTests (Plan 28-02) — ±5% audible-duration tolerance
  - PerSynthArticulationTests (Plan 28-03) — verifies routing only
  - ArticulationVelocityTests (Plan 28-02) — ±2 velocity units

**Reproduction:**
```bash
dotnet run --project flow-interpreter examples/tests/ragtime_polyphony.flow
```

## Current Focus

```yaml
hypothesis: |
  RESOLVED. The root cause was NOT in the SPEC-5 articulation envelope —
  it was in Parser.ParseNoteStream (flow-lang/Parsing/Parser.NoteStream.cs:38)
  which, on multi-line bar lists, silently injected an empty whole-bar rest
  between every adjacent pair of content bars. The composer's "grace note"
  percept came from the C2w bass voice attack at each rendered staccato-bar
  onset arriving after a 2-second silent rest bar — the bass thump
  perceptually grafted onto the C5 staccato as a brief grace-note-like blip.
test: |
  RagtimeFixture_MultiLineFourBars_CompilesToFourBars (and 5 sibling Facts) in
  flow-lang.Tests/Unit/Phase28/StaccatoGraceNoteRegressionTests.cs:
  asserts `Sequence.Bars.Count == 4` for the 4-line ragtime fixture.
  Pre-fix: 7 bars. Post-fix: 4 bars.
expecting: |
  Tests pin the bar-count contract; new baselines for the two ragtime WAV
  RmsRegression tests committed in flow-lang.Tests/baselines/Phase28/.
next_action: |
  Composer re-listens to examples/output/ragtime_polyphony.wav and
  confirms the perceptual "grace note" is gone. Then flips Phase 28
  UAT checkboxes at 28-VERIFICATION.md:62-63 / 84 / 102.
specialist_hint: general
reasoning_checkpoint: null
tdd_checkpoint: null
```

## Suspect Locations

(retained for reference — final cause was Parser.NoteStream.cs:49)

- `flow-lang/Parsing/Parser.NoteStream.cs:49` — **ROOT CAUSE**. `Match(TokenType.Pipe)` unconditionally saved current bar (even when `currentBarElements.Count == 0` and bars list already had content), inserting empty rest bars between adjacent content bars in multi-line layouts.
- `flow-lang/StandardLibrary/Audio/SynthUtils.cs:160-210` — `GenerateArticulationADSR()` (originally suspected, dismissed by `PianoStaccato_EnvelopeMultiplierHasExactlyOnePeak` regression test).
- `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs` — also dismissed.
- `BarRenderer.cs` — Staccato 25% duration multiplier correctly applied; not the cause.

## Constraints

- Phase 28 test suite is 985/985 GREEN three runs in a row — fix MUST NOT regress.
- Phase 22 LegatoFacts (8/8) and Phase 18/25/27 ByteIdentical determinism tests (14/14) must stay GREEN.
- Repo is pre-public — breaking changes can land in a single commit, no deprecation needed.
- Project memory: charitable interpretation; ergonomics-first; prefix-only arithmetic (no infix `+ - * /`).
- Run `dotnet build` from repo root; tests via `dotnet test flow-lang.Tests` (or scoped Theory filter).
- DO NOT flip the manual-UAT checkboxes at .planning/phases/28-midi-audio-polyphony-articulation-rewrite/28-VERIFICATION.md:62-63 / 84 / 102 until composer re-listens and confirms the fix.

## Evidence

- timestamp: 2026-05-10
  observation: |
    Rendered `examples/output/ragtime_polyphony.wav` via the production
    pipeline. WAV duration is 14.000s, NOT the expected 8s (4 bars ×
    2s at BPM 120, 4/4). Per-bar RMS readout:
      Bar 1 [0-2s]   RMS = 0.0348  (audible — source bar 1)
      Bar 2 [2-4s]   RMS = 0.0000  (SILENT — phantom empty bar)
      Bar 3 [4-6s]   RMS = 0.0254  (audible — source bar 2 staccato)
      Bar 4 [6-8s]   RMS = 0.0000  (SILENT — phantom empty bar)
      Bar 5 [8-10s]  RMS = 0.0360  (audible — source bar 3 legato)
      Bar 6 [10-12s] RMS = 0.0001  (SILENT — phantom empty bar)
      Bar 7 [12-14s] RMS = 0.0287  (audible — source bar 4 mixed)
- timestamp: 2026-05-10
  observation: |
    Compiled the synthetic ragtime sequence directly:
      multi-line 4-source-bars → "Sequence[7 bars, 28 beats total]"
      single-line 4-source-bars → "Sequence[4 bars, 16 beats total]"
    Layout choice changes the bar count — proof the parser inserts
    extras on adjacent `|` token pairs.
- timestamp: 2026-05-10
  observation: |
    Token stream for multi-line `| ... |\n  | ... |` is
        PIPE [bar1 tokens] PIPE PIPE [bar2 tokens] PIPE
    The two consecutive PIPEs are the closing `|` of bar 1 AND the
    opening `|` of bar 2. Pre-fix, the parser's second `Match(Pipe)`
    saw `currentBarElements.Count == 0` and pushed an empty
    NoteStreamBar — exactly the phantom rest bar seen in the WAV.

## Eliminated

- SynthUtils.GenerateArticulationADSR — staccato envelope curve has
  exactly 1 peak per `PianoStaccato_EnvelopeMultiplierHasExactlyOnePeak`.
- BarRenderer 25% duration multiplier — produces correct shortened
  buffers per the existing ArticulationRulesTests.
- PianoSynthesizer hammer transient — single rendered C5q-stacc through
  the synth shows the transient lives entirely within the first 123
  frames and decays cleanly into the main note's attack ramp.
- VoiceAllocator steal-oldest policy — 5 voices fit well inside the
  SPEC-7 locked default 32-voice pool; no truncation happens.

## Resolution

**Root cause:** `Parser.ParseNoteStream` in `flow-lang/Parsing/Parser.NoteStream.cs:49` saved an empty `NoteStreamBar` every time it matched `TokenType.Pipe`, including when the immediately preceding token was also a `|` (the natural shape of multi-line bar lists). For the 4-line synthetic ragtime fixture this doubled-then-some the bar count (4 → 7), grafting a 2-second silent rest bar before every content bar after the first. The bass-voice attack at each rendered content bar's onset arrived after a long silence, perceptually graced as a brief blip before the staccato — which is what the composer heard and reported as a "grace note pre-attack."

**Fix:** Added a charitable-interpretation guard before the save in `Parser.NoteStream.cs:68-73`:
```csharp
if (currentBarElements.Count == 0 && bars.Count > 0)
{
    // Adjacent PIPE after a saved content bar — treat as opening | of the
    // next bar, no save.
    continue;
}
```
Adjacent `|` tokens after a saved content bar collapse into a single bar boundary. Explicit `| _ |` rest bars are unaffected (the rest underscore makes `currentBarElements.Count == 1` when the closing `|` arrives).

**Tests added** (`flow-lang.Tests/Unit/Phase28/StaccatoGraceNoteRegressionTests.cs`):
1. `RagtimeFixture_MultiLineFourBars_CompilesToFourBars` — 4 source bars × 4 beats = 16 beats.
2. `MapleLeafFixture_MultiLineEightBars_CompilesToEightBars` — 8 source bars × 2 beats = 16 beats.
3. `RagtimeFixture_SingleLineFourBars_MatchesMultiLineBarCount` — layout invariance.
4. `ExplicitRestBar_PreservedInMultiLineLayout` — `| _ |` still counts.
5. `PickupNotation_PreservedThroughMultiLineParse` — pickup + main bar = 2 bars.
6. `PianoStaccato_EnvelopeMultiplierHasExactlyOnePeak` — pins the dismissed-not-broken SPEC-5 envelope contract.

**Baselines regenerated** (`flow-lang.Tests/baselines/Phase28/`):
- `ragtime_polyphony.wav` — 2469644 → 1411244 bytes (correct 4-bar render).
- `maple_leaf_opening.wav` — 4177936 → 2228252 bytes (correct 8-bar render).

**Suite status:** 992/992 GREEN across two consecutive runs (985 prior + 7 new regression Facts).

**Composer re-listen pending:** the perceptual fix needs human verification before flipping `28-VERIFICATION.md:62-63 / 84 / 102`.
