---
slug: midi-import-quarter-quantize
status: investigating
trigger: |
  Composer rendered examples/ragtime.mid through flow-midi → examples/output/ragtime_imported.flow,
  then through flow-interpreter → examples/output/ragtime_imported.wav. After the 4-bar intro,
  the rhythm is wrong: the source authored pattern is <Quarter, Eighth, Eighth> in 4/4 common
  time, but renders as <Eighth, Eighth-rest, Eighth, Eighth-rest, Eighth> — i.e. quarters are
  being split into eighth + eighth-rest. Also reported "every note sounds like it has a grace
  note" but the staccato-grace-note-artifact root cause (parser phantom rest bars) does NOT
  apply here — confirmed zero `||` adjacency in the generated .flow, and a forced clean
  rebuild + re-render produces a byte-identical WAV (md5 c86218cdcd7c632fdafb1091fe6f9292),
  so what the composer hears is purely a flow-midi importer defect.
created: 2026-05-10
updated: 2026-05-10
---

# Debug Session: midi-import-quarter-quantize

## Symptoms

**Expected behavior:**
flow-midi converts a 4/4 source MIDI containing a <Quarter, Eighth, Eighth> rhythm into a
Flow note stream that, when rendered, produces the same rhythm: one quarter-note duration
followed by two eighth-note durations, with no spurious rests.

**Actual behavior:**
After the 4-bar intro of examples/ragtime.mid, each quarter note in the source is rendered
as Eighth + Eighth-rest, so <Q, E, E> becomes <E, E-rest, E, E-rest, E>. Composer described
it as "random pauses" and "every note sounds like it has a grace note" — the "grace note"
percept here is the leading-edge attack of the next eighth-note after a half-eighth silence,
NOT the parser-phantom-bar artifact that bug staccato-grace-note-artifact closed.

**Error messages:**
flow-midi emits: `Warning: 15 tempo changes found; using the first (BPM=175.0).` — known
limitation; tempo-change support is a separate concern. NOT the bug under investigation.

**Timeline:**
Reported 2026-05-10 during Phase 28 manual UAT, alongside the parser-phantom-bar bug. The
parser fix has zero effect here (verified: zero `||` in the generated .flow; md5-identical
WAV across pre-fix and post-fix renders).

**Reproduction:**
```bash
# Convert the source MIDI
dotnet run --project flow-midi -- examples/ragtime.mid examples/output/ragtime_imported.flow
# emits: Warning: 15 tempo changes found; using the first (BPM=175.0)

# The generated file ends with `(play output)` — patch to writeWav for repeatable A/B
sed -i 's|(play output)|(writeWav "examples/output/ragtime_imported.wav" output)|' \
  examples/output/ragtime_imported.flow

# Render
dotnet run --project flow-interpreter examples/output/ragtime_imported.flow

# Listen — after the 4-bar intro, the Q-E-E pattern is wrong.
# Compare to: examples/ragtime.mid played through any other MIDI player
# (Synthesia, timidity, an external DAW) — the source is rhythmically correct,
# so the defect is in the flow-midi conversion path.
```

## Current Focus

```yaml
hypothesis: |
  flow-midi/Conversion/Quantizer.cs (or FlowGenerator.cs) snaps note durations to an
  eighth-note grid even when the source MIDI tick-distance between NoteOn pairs is exactly
  one quarter note (480 ticks at TPQN=480). Likely culprits:
  (a) Quantizer rounds duration to nearest eighth via Math.Round on a fractional-quarter
      value, producing 0.5 for a true quarter and getting interpreted downstream as
      "eighth + half-eighth gap" → "Eighth + Eighth-rest".
  (b) FlowGenerator's NoteValue selection table is missing the Quarter entry, so any duration
      ≥ Eighth that isn't Half/Whole gets emitted as multiple Eighths.
  (c) Truncation-vs-rounding boundary: an off-by-one tick anywhere in
      [duration_ticks, beat_position] arithmetic could shift quarter notes to fall on
      odd-numbered eighth-grid positions, forcing a rest insertion to keep bar boundaries.
test: |
  Construct a synthetic minimal MIDI fixture in flow-midi.Tests (or a new test project)
  encoding 4 quarter notes at TPQN=480 in 4/4. Convert to Flow via the production pipeline
  and assert: (1) the generated .flow contains exactly 4 quarter-note durations (`q`)
  and zero quarter-rests (`_q` or `_`-equivalents); (2) the rendered WAV has 4 attack peaks
  at expected beat positions (tick 0, 480, 960, 1440 in MIDI units; t=0s, 0.5s, 1.0s, 1.5s
  at BPM=120). Should be passing after the fix.
expecting: |
  Test will fail on HEAD with either: (a) fewer than 4 quarter durations in the .flow
  output (split into eighths), or (b) at least one eighth-rest in the output, or (c) the
  WAV showing 8 attack peaks instead of 4.
next_action: |
  1. Extend flow-midi/Diagnostics.cs with a tick-by-tick NoteOn dump (composer authorized
     bypassing GSD for this additive diagnostic). Add option like `--dump-events <N>` that
     prints first N NoteOns: absolute tick, channel, pitch, velocity, duration-to-matching
     NoteOff. Run on examples/ragtime.mid to see the actual rhythmic shape of the first
     8-16 bars and confirm the source has <Q, E, E> pattern as authored.
  2. Read flow-midi/Conversion/Quantizer.cs end-to-end. Identify the quantization grid
     resolution, the rounding/truncation behavior at fractional-quarter boundaries, and
     whether quarter-note durations get explicit handling or fall through to a
     "subdivide into eighths" branch.
  3. Read flow-midi/Conversion/FlowGenerator.cs. Check the NoteValue selection logic —
     does it have Quarter as a primary output, or is it inferring duration via
     repeated-eighth emission?
  4. Write failing xUnit test in flow-midi.Tests that exercises a synthetic 4-quarter-note
     MIDI fixture. Verify it fails. Then trace defect, patch, confirm pass.
  5. Re-run the full ragtime conversion + render and have composer re-listen.
specialist_hint: null
reasoning_checkpoint: null
tdd_checkpoint: null
```

## Suspect Locations

- `flow-midi/Conversion/Quantizer.cs` — primary suspect. Quantization grid + rounding logic.
- `flow-midi/Conversion/FlowGenerator.cs` — NoteValue selection / emission. Check whether
  Quarter is a primary output or if it's synthesized from multiple Eighths.
- `flow-midi/Diagnostics.cs` — needs tick-by-tick NoteOn dump capability for diagnostic;
  EXTEND with a new flag (composer authorized bypassing GSD for this additive enhancement).
- `flow-midi/Midi/MidiParser.cs` — confirm NoteOn/NoteOff pairing produces correct tick
  durations from the source file (sanity check; defect is more likely downstream in
  Quantizer, but rule this out first).
- `flow-lang/Runtime/NoteStreamCompiler.cs` — note-stream → Sequence compilation; verify
  the generated `q` tokens produce the right duration when rendered. Almost certainly NOT
  the bug (Phase 28 fixture renders `q` durations correctly), but include for completeness.

## Constraints

- This is flow-midi territory, NOT Phase 28. Will eventually be Phase 30 (Flow CLI + Formal
  Install + MIDI↔Flow conversion polish). Fix here is a pre-emptive precursor.
- The "15 tempo changes" warning is a known limitation — DO NOT chase tempo handling as
  part of this bug. The Q→E-rest-E split happens BPM-independent.
- Phase 28 test suite (992/992 GREEN) must stay GREEN — fix lives in flow-midi, not
  flow-lang, so should be isolated.
- Repo is pre-public — breaking changes can land in one commit.
- Project memory: charitable interpretation; ergonomics-first.

## Evidence

(none yet — populate during investigation)

## Eliminated

- Phase 28 parser phantom-bar bug (staccato-grace-note-artifact): VERIFIED ELIMINATED.
  Zero `||` adjacency in `examples/output/ragtime_imported.flow`; forced clean rebuild +
  re-render produces md5-identical WAV (`c86218cdcd7c632fdafb1091fe6f9292`) before AND
  after the parser fix. The defect heard in ragtime_imported.wav lives elsewhere.

## Resolution

(pending)
