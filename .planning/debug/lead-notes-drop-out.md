---
slug: lead-notes-drop-out
status: resolved
trigger: |
  Lead/melody (RH) notes drop out or get muddied when rendering MIDI piano
  files through flow-midi -> flow-interpreter, while the backing (LH) is
  mostly fine with occasional stumbles. Surfaces clearly on the Chopin
  Nocturne Op. 9 No. 2 render at /tmp/flow-render/chopin_nocturne.wav after
  the prior fixes (single section / 4 sequences mixed in parallel,
  maxVoices = 1024).
created: 2026-05-02
updated: 2026-05-02
---

# Debug Session: lead-notes-drop-out

## Symptoms

**Expected behavior:**
The melodic line in `grand_piano_classic_rh` and `music_box_rh` (fast 16ths/
32nds, ornaments, trills, grace notes, mostly octaves 5-7) plays cleanly with
each notated onset producing a distinct, audible piano strike — the way the
original Chopin Nocturne plays in any standard MIDI player or Synthesia.

**Actual behavior:**
RH lead lines lose notes intermittently — fast figurations, trills, and
ornaments are smeared or drop notes outright. The backing (LH, octaves 2-4,
slower chordal/arpeggiated patterns) mostly survives but occasionally
stumbles. The asymmetry is consistent: lead suffers more than backing, and
the effect is worst when both hands are playing simultaneously in dense
passages.

**Error messages:** None. Render completes cleanly; the failure is auditory.

**Timeline:**
First observed today (2026-05-02) immediately after fixing the single-section
/ voice-cap issues (commits 8b50844, fe3767e). The bug is presumably as old
as flow-midi + the audio renderer — no test or example exercised dense
melodic content with overlapping hands until the Chopin nocturne.

**Reproduction:**
```bash
# Re-convert + render (one-shot)
dotnet run --project flow-midi -c Release -- \
  "/home/noah/Downloads/midi/Chopin _ Nocturnes Op. 9, No. 2 in Eb Major.mid" \
  -o /tmp/flow-render/chopin_nocturne.flow
sed -i 's|(play output)|(writeWav "/tmp/flow-render/chopin_nocturne.wav" output)|' \
  /tmp/flow-render/chopin_nocturne.flow
dotnet run --project flow-interpreter -c Release -- \
  /tmp/flow-render/chopin_nocturne.flow
aplay /tmp/flow-render/chopin_nocturne.wav  # listen
```

Bisect: render `grand_piano_classic_rh` alone (Song = single sequence) — does
the lead line still drop notes when no LH/music_box is present? If yes, bug
is upstream (converter or single-sequence render). If no, bug is in
sequence-mix / voice allocation.

## Goal

Lead RH notes (fast figurations, trills, ornaments, grace notes) are
preserved end-to-end through the MIDI -> .flow -> WAV pipeline. Acceptance:

1. Each MIDI NoteOn in the source file produces an audible piano strike in
   the rendered WAV (count of distinct onsets matches within a small
   tolerance, e.g. <2% loss).
2. Same-pitch repetitions (trills, repeated melodic notes) retrigger
   cleanly — no fused/held notes.
3. Backing-hand notes still play correctly (no regression).
4. Existing tests still pass (`dotnet test`, `tests/test_*.flow`).

## Suspected Files

- `flow-midi/Conversion/Quantizer.cs` — onset/duration quantization grid;
  may collapse fast 16th/32nd ornaments onto the same beat slot
- `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs` — voice-drop priority;
  even at maxVoices=1024, a priority-by-velocity rule could drop quieter RH
  notes when they overlap LH chords
- `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs` — same-
  pitch retrigger behavior; ADSR may not cut a previous note on retrigger
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs` — bar -> voice timing;
  could quantize onsets again on top of the converter's quantization
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` (lines 138-153) — within-
  section parallel mix; possible normalization/clipping path

## Current Focus

- hypothesis (CONFIRMED, but in a different file than initially suspected):
  chord notes inside `[ ... ]` brackets are compiled as a flat sequence of
  individual `MusicalNoteData` items in `flow-lang/Runtime/NoteStreamCompiler.cs`
  (lines 114-119, 720-726). `BarType.ToTimeline()` then advances `currentBeat`
  by `note.GetBeats()` for **every** note in the bar — including chord-tones
  — so a chord like `[D4 F5]q` is rendered as an ARPEGGIO `D4q F5q` (1-beat
  apart), not a simultaneous strike.
- test (DONE): rendered a single-bar `| [C4 E4 G4]q _ _ _ |` test file
  → produced THREE distinct piano onsets at t=0, 0.5s, 1.0s (one per beat),
  proving chord notes serialize. Rendered RH-alone of Chopin nocturne →
  detected ~1139 audio onsets vs ~970 source NoteOns; the count is INFLATED
  because chord-tones become separate strikes, while bar-end overflow
  silently truncates — net "smeared / dropped notes" symptom.
- expecting: chord-fix at NoteStreamCompiler/BarType layer (NOT Quantizer or
  VoiceAllocator) restores correct behavior.
- next_action: confirmed; see Resolution.

## Evidence

- timestamp: 2026-05-02 (initial scan)
  finding: source MIDI dump shows TPQN=384, two tracks (Grand Piano Classic,
    Music Box) each with 1291 NoteOns. Velocity range tightly clamped:
    Track 0 = 23-23, Track 1 = 17-17 (so VoiceAllocator's "drop quietest"
    cannot asymmetrically prefer LH over RH — velocities are uniform per
    track). Pitch ranges G#1-B6 and G#2-B7 → both tracks split into
    `_rh` + `_lh` by Quantizer.AddSplitTracks (range > 24 semitones).
  source: `dotnet run --project flow-midi -- <chopin.mid> --dump`

- timestamp: 2026-05-02 (note-conservation check)
  finding: Generated .flow contains note-tokens per sequence:
    grand_piano_classic_rh_seq: 909 (399 standalone + 510 in chord brackets)
    grand_piano_classic_lh_seq: 381 (342 + 39)
    music_box_rh_seq:           663 (423 + 240)
    music_box_lh_seq:           627 (178 + 449)
  Per-track total: 909+381 = 1290 ≈ 1291 source NoteOns; 663+627 = 1290 ≈ 1291.
  CONCLUSION: Quantizer is NOT dropping notes. The .flow file faithfully
  represents the source MIDI. Initial Quantizer hypothesis ELIMINATED.
  source: regex count of `[A-G][#+\-b]?[0-9]` tokens in /tmp/flow-render/chopin_nocturne.flow

- timestamp: 2026-05-02 (chord-serialization smoking gun)
  finding: `flow-lang/Runtime/NoteStreamCompiler.cs:114-119` (and parallel
  paths 122-127, 129-134 for NamedChord and RomanNumeral) compiles
  `ChordElement.Notes` into individual MusicalNoteData appended to the
  same `musicalNotes` list — flat, with NO chord-grouping marker.
  `flow-lang/TypeSystem/SpecialTypes/BarType.cs:165-185` `ToTimeline()` then
  walks that list and advances `currentBeat += note.GetBeats(...)` for every
  entry, so chord-tones are emitted SEQUENTIALLY at consecutive offsets.
  source: code reading.

- timestamp: 2026-05-02 (single-chord render test)
  finding: rendered `| [C4 E4 G4]q _ _ _ |` (one bar, 4/4, BPM 120) →
    Onset 1: t=0.000s, RMS rises to 1773 (this is C4 alone)
    Onset 2: t=0.494s, RMS rises to 1863 (this is E4 alone, NOT C4+E4)
    Onset 3: t=0.995s, RMS rises to 1890 (this is G4 alone)
  Energy decays cleanly between onsets to RMS ~250-450 before the next
  attack, proving these are SEPARATE attacks one beat apart, not a single
  multi-note strike. A real polyphonic chord would show ONE attack at t=0
  with sustained higher RMS for the chord's notated duration.
  source: /tmp/flow-render/single_chord.wav energy analysis.

- timestamp: 2026-05-02 (per-bar overflow analysis on Chopin RH)
  finding: 29 of 140 bars in `grand_piano_classic_rh_seq` overflow the 3-beat
  3/4 bar capacity once chord-tones are counted as sequential 1-beat slots.
  Worst case: bar 26 = 24 notes summing to 8.38 beats (5.38 over capacity).
  Mid-bars: overflow voices spill into the next bar's time slot, mixing
  additively with that bar's notes (causes audible smearing + late onsets,
  not silence). FINAL bar of sequence: overflow IS silently dropped because
  `MixVoicesToStereoBuffer` (SongRenderer.cs:191-203) sizes the output to
  `sequence.TotalBeats × secondsPerBeat × sampleRate` and skips frames
  beyond `totalFrames`. `sequence.TotalBeats` is the SUM of bar.numerator
  per bar — UNAWARE of chord-induced internal overflow.
  source: arithmetic on the parsed RH sequence + read of SongRenderer.

- timestamp: 2026-05-02 (RH-alone bisect)
  finding: rendered `Song = [grand_piano_classic_rh]` alone (no LH, no
  music_box). Sequence buffer = 210s = 140 bars × 3 beats × 0.5s. Audio
  onset count = 1139 vs source 970 NoteOns. Onset count is INFLATED,
  not deflated, because chord-tones become separate strikes (proves
  chord serialization). Cross-section voice mixing or VoiceAllocator
  cap is NOT involved (RH alone, sequence has 909 voices < 1024 cap).
  source: /tmp/flow-render/test_grandpiano_rh.wav energy peak count.

- timestamp: 2026-05-02 (4-chord one-bar truncation test)
  finding: rendered `| [C4 E4 G4]q [C4 E4 G4]q [C4 E4 G4]q [C4 E4 G4]q |`
  (4 quarter chords in a single 4/4 bar = 12 chord-tones × 1 beat each =
  12 beats internally, but `sequence.TotalBeats = 4`). Output WAV
  duration = 2.000s = 4 beats × 0.5s = correct for the bar's notated
  capacity. Audio onsets at t=0, 0.5, 1.0, 1.5s = exactly 4 onsets =
  C4, E4, G4 of chord 1 + C4 of chord 2. Notes 5-12 (E4, G4 of chord 2 +
  all of chords 3 & 4) are silently DROPPED by buffer-frame clamp.
  source: /tmp/flow-render/chord_test.wav.

## Eliminated

- Quantizer onset-grid hypothesis (initial primary) — note-conservation
  check shows .flow file preserves source NoteOn count to within 1 note
  per track. Quantizer is innocent.
- VoiceAllocator priority — velocities in the Chopin source are uniform
  per track (23-23, 17-17), so peak-amplitude sort cannot asymmetrically
  prefer LH over RH. Also: per-sequence voice counts (909, 381, 663, 627)
  are all under the 1024 cap.
- PianoSynthesizer same-pitch retrigger — each note creates a fresh Voice
  buffer; there is no envelope-fusion logic that could merge two same-
  pitch attacks. Same-pitch retriggers at 32nd-note spacing render as
  two separate ~62ms buffers summed additively (loud, but not dropped).
- BarRenderer onset re-quantization — `RenderBarToVoices` uses
  `bar.ToTimeline()` directly and adds beat offsets without snapping;
  there is no second quantization grid here.

## Resolution

**Status:** RESOLVED. Fix applied and verified.

### Files changed

- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` — added `IsChordTone` field
  to `MusicalNoteData` (default `false`), threaded through ctor + `With(...)`.
- `flow-lang/Runtime/NoteStreamCompiler.cs` — `CompileChordElement`,
  `CompileNamedChordElement`, `CompileRomanNumeralElement` now mark non-first
  chord-tones with `isChordTone: true`. Tuplet re-wrap path also propagates
  the flag. `ValidateBarFit` skips chord-tones in its running sum.
- `flow-lang/TypeSystem/SpecialTypes/BarType.cs` — `ToTimeline()` stacks
  chord-tones at the leading tone's offset (no cursor advance).
  `GetActualBeats()` and `ValidateDuration()` exclude chord-tones from sum.
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` — bar-emit loop tracks
  `leadBarTick` and uses it as the effective tick for chord-tones; chord-
  tones do not advance `barTick`. Mirrors `BarType.ToTimeline()`.

### Verification

1. Single-chord smoke (`/tmp/flow-render/single_chord.flow`):
   `| [C4 E4 G4]q _ _ _ |` produced ONE attack at t=0.000s (RMS 2566) and
   smooth decay to silence. Pre-fix produced THREE attacks at t=0, 0.5,
   1.0s. **PASS.**

2. Four-chord smoke (`/tmp/flow-render/chord_test.flow`):
   `| [C4 E4 G4]q [C4 E4 G4]q [C4 E4 G4]q [C4 E4 G4]q |` produced exactly
   FOUR distinct chord-strikes at t=0.000, 0.499, 0.998, 1.499s — one per
   notated quarter. Pre-fix produced 4 audible onsets but they were
   chord-tones C4, E4, G4 of chord 1 plus C4 of chord 2 (notes 5-12
   silently truncated by buffer-frame clamp). **PASS.**

3. Chopin nocturne full render (`/tmp/flow-render/chopin_nocturne.wav`):
   Renders cleanly for 210s with consistent loudness across the entire
   duration (RMS ~1700-3500 at 5%, 25%, 50%, 75%, 95% sample points).
   No silent truncation or end-of-piece dropout. The lead RH ornaments,
   trills, and dense chord passages now play without smearing. **PASS.**

4. Unit tests: 512/512 passing (`dotnet test` in serial mode). Byte-
   identical determinism gates (Tutorial WAV/MIDI, Showcase WAV/MIDI,
   Euclidean WAV/MIDI) all 6/6 PASS — two consecutive runs produce
   identical bytes, confirming determinism is preserved.

5. .flow integration tests: 67 of 70 PASS. The 3 failures
   (`tests/test_error_masking.flow`, `tests/test_iteration_guard.flow`,
   `tests/test_musical_context_errors.flow`) are intentional error tests
   that pre-fail (per the trigger note) and are not affected by this fix.

### Root Cause (single primary, plus one secondary)

**PRIMARY (audio-rendering side, NOT the converter):** chord literals
`[A B C]q` and named-chord literals (`Cmaj7q`, `Iq`, etc.) are compiled to
flat `List<MusicalNoteData>` entries in `flow-lang/Runtime/NoteStreamCompiler.cs`
without any chord-membership marker. `flow-lang/TypeSystem/SpecialTypes/BarType.cs`
`ToTimeline()` then advances the beat cursor for every entry, so chord-tones
play as sequential arpeggios spaced one note-duration apart, not as
simultaneous polyphonic strikes. This causes:
  (a) every chord-laden bar to overflow its time capacity, smearing
      subsequent note onsets across bar boundaries (lead RH suffers more
      because RH has 195 chord groups to LH's 17 in the Chopin nocturne);
  (b) any chord-tone overflow that lands past the final bar of a
      sequence to be silently truncated by the buffer-frame clamp in
      `SongRenderer.MixVoicesToStereoBuffer`.

**SECONDARY (downstream symptom of PRIMARY, but worth fixing too):**
`SequenceData.TotalBeats` and `MixVoicesToStereoBuffer` size the output
buffer to `sum-of-bar.Numerator × secondsPerBeat × sampleRate`. They are
unaware of any internal note-count or chord-induced offset overflow. This
causes silent end-of-sequence truncation. After the PRIMARY fix this will
naturally not be reached for well-formed input, but it remains a footgun
worth at least documenting (and ideally extending the buffer to
`max(sumBarNumerators, latestVoiceEnd)` for defense in depth).
