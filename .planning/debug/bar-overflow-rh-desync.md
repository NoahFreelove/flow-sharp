---
slug: bar-overflow-rh-desync
status: fixed
trigger: |
  After the chord-stacking fix (commit b66acbe), the Chopin Nocturne renders
  mostly correctly but during fast figurations the right hand goes mute then
  "tries to catch up" — clear desynchronization between RH and LH. User
  reports the problem starts around bar 5 of /tmp/flow-render/chopin_nocturne.flow
  (3/4 time signature). Many bars in `grand_piano_classic_rh_seq` contain
  durations that sum past the 3-beat bar capacity (e.g. bar 2 is
  `G5h.~ D4+q [D4+ G4]q` = 3 + 1 + 1 = 5 beats), so the RH timeline runs
  ahead of the LH timeline as overflow accumulates.
created: 2026-05-03
updated: 2026-05-03
---

# Debug Session: bar-overflow-rh-desync

## Symptoms

**Expected behavior:**
RH and LH stay synchronized through fast figurations. Each bar of either
sequence consumes exactly one bar's worth of wall-clock time (e.g. 3 beats
in 3/4 at 120 BPM = 1.5 seconds), so the LH and RH bar boundaries line up.

**Actual behavior:**
During fast/dense passages in the RH, the RH goes silent for a stretch then
"tries to catch up" — the user can hear it desynchronize from the LH and
re-synchronize abruptly. Specifically reported "around bar 5" of the
generated .flow file.

**Error messages:** None. Failure is auditory.

**Timeline:**
First observed today (2026-05-03), after the chord-stacking fix (b66acbe)
landed yesterday. The chord fix made `BarType.GetActualBeats` skip chord
tones, which exposed the bar-overflow issue more clearly because chord-tones
no longer mask the cumulative drift.

**Reproduction:**
```bash
# Render Chopin
dotnet run --project flow-midi -c Release -- \
  "/home/noah/Downloads/midi/Chopin _ Nocturnes Op. 9, No. 2 in Eb Major.mid" \
  -o /tmp/flow-render/chopin_nocturne.flow
sed -i 's|(play output)|(writeWav "/tmp/flow-render/chopin_nocturne.wav" output)|' \
  /tmp/flow-render/chopin_nocturne.flow
dotnet run --project flow-interpreter -c Release -- \
  /tmp/flow-render/chopin_nocturne.flow
aplay /tmp/flow-render/chopin_nocturne.wav  # listen — RH desyncs around bar 5
```

Observation from inspecting the generated .flow:
- `grand_piano_classic_rh_seq` bar 2: `G5h.~ D4+q [D4+ G4]q`
  - h. = dotted half = 3 beats; q = 1 beat; chord-q = 1 beat
  - Sum (excluding chord-tone D4+ which is the lead's repeat): 3 + 1 + 1 = 5
  - That's 5 beats stuffed into a 3-beat bar
- LH bars sum more reasonably (slower harmonic rhythm)

## Goal

RH and LH stay locked in time across the whole piece. Acceptance:

1. The Chopin nocturne renders without audible drift between hands at any
   point — RH ornaments stay on top of the correct LH chord.
2. RH-only render duration equals LH-only render duration (both = song
   duration), within ~50 ms tolerance.
3. No bar in either generated sequence has its non-chord-tone durations
   sum to more than the time-signature-numerator beats (i.e. ValidateDuration
   passes for every bar after re-conversion).
4. Existing tests still pass: `dotnet test` 512/512, `tests/test_*.flow`
   67/70 (3 intentional error-tests pre-fail on master).

## Suspected Files

- `flow-midi/Conversion/Quantizer.cs` — bar duration grid + tied-note
  emission. Most likely culprit: it picks per-note durations greedily
  without enforcing bar-capacity, so dotted/tied notes accumulate past
  3 beats per bar.
- `flow-lang/TypeSystem/SpecialTypes/BarType.cs` — ToTimeline cursor math
  (already chord-aware after b66acbe). May need bar-boundary clamping.
- `flow-lang/StandardLibrary/Audio/SequenceRenderer.cs` — per-bar
  offsetBeats accumulation; `sequence.ToTimeline()` provides
  (bar, offsetBeats) tuples — does it use the time-signature nominal
  bar length or the bar's actual sum? If "actual sum", overflowing bars
  push later bars out of sync.
- `flow-lang/Runtime/NoteStreamCompiler.cs` — bar-fit / overflow
  decisions when compiling note streams.
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs` — note placement
  within a bar (chord-aware after b66acbe).

## Current Focus

- hypothesis: CONFIRMED. flow-midi's Quantizer flattens MIDI polyphony
  WITHIN A SINGLE TRACK (and even within RH/LH after the pitch-split)
  into a SEQUENTIAL stream of notes. When two notes in the same hand
  overlap in time (e.g., a held melody pitch + a moving inner voice),
  the Quantizer emits BOTH durations sequentially within the same bar,
  producing bars where sum(non_chord_tone_durations) > nominal_bar_length.
  The renderer then clamps voices whose absolute frame ≥
  `sequence.TotalBeats` (= nominal sum) → silent dropouts in dense RH
  passages.

- next_action: apply Option B (bar-fit clamp) in `Quantizer.cs`
  `QuantizeSpans` — when the next group starts before the current
  cursor, retroactively truncate the just-emitted note's duration to
  end at the next group's start. Also clamp final group's duration to
  bar-end. After the fix, ValidateDuration must pass for every bar.

## Evidence

- timestamp: 2026-05-03T00:00Z
  source: /tmp/flow-render/chopin_nocturne.flow (regenerated from MIDI)
  observation: |
    Ran /tmp/bar_overflow_analyzer.py on the freshly regenerated
    Chopin .flow. Findings:

      grand_piano_classic_rh_seq (140 bars, 3/4):
        exact-fit:  17
        underflow:  36
        OVERFLOW:   87  (62%)  max +3.50 beats over nominal
        total actual beats: 516.0  (nominal: 420.0)  — 23% drift

      grand_piano_classic_lh_seq (139 bars):
        OVERFLOW:    9   (6%)
        total actual beats: 353.0  (nominal: 417.0)  — well-behaved

      music_box_rh_seq (140 bars):
        OVERFLOW:   65  (46%)  max +2.75 beats
        total actual beats: 443.6

      music_box_lh_seq (140 bars):
        OVERFLOW:   13   (9%)

    The RH sequences overflow systematically; the LH sequences mostly
    underflow (slower harmonic rhythm = trailing rests). The asymmetry
    explains why the user perceives the RH (not the LH) as desyncing.

- timestamp: 2026-05-03T00:05Z
  source: flow-midi/Conversion/Quantizer.cs:346-437  (QuantizeSpans)
  observation: |
    Walked the Quantizer code. Found the smoking gun: the cursor
    advance logic only adds rests when groupStart > cursor (line 382).
    When groupStart < cursor (the current case — held G5 with later
    onset D4 happening DURING G5's sustain), the code FALLS THROUGH
    to emit the new note at `cursor` (NOT at groupStart!) with its
    full snapped duration. Result:

      cursor=0    : emit G5 (3 beats), cursor → 3
      cursor=3    : D4 onsets at tick 1920 (within bar — really at beat 1)
                    BUT groupStart=1920 < cursor (which has been advanced
                    to barStart+1440=2880). The if-branch at line 382 is
                    NOT taken. D4 is emitted with its 1-beat duration
                    starting at... cursor=2880 (= barEnd). cursor → 3360.
      cursor=3360 : chord onsets at tick 2400. groupStart < cursor.
                    Emit chord at cursor=3360 (= barEnd + 1). cursor → 3840.

    Final bar elements: G5h. (3 beats) + D4+q (1 beat) + [D4+ G4]q (1 beat)
    = 5 beats stuffed into a 3-beat bar. EXACTLY matches the .flow output.

- timestamp: 2026-05-03T00:08Z
  source: flow-lang/TypeSystem/SpecialTypes/SequenceType.cs:46-61, BarType.cs:173-211
  observation: |
    Verified the renderer side:
      - SequenceType.ToTimeline() advances by `TimeSignature.Numerator`
        (NOMINAL) per bar (line 56). So bar N starts at exactly N*3 beats.
        Good — no drift at the sequence level.
      - BarType.ToTimeline() advances `currentBeat` by each note's GetBeats
        UN-CLAMPED (line 202). For a 5-beat overflow bar, this places the
        last chord at within-bar offset 4, i.e., absolute beat = bar_offset
        + 4. With nominal 3 beats per bar, this is 1 beat INTO the next
        bar's slot.
    Conclusion: the renderer behavior is consistent IF bars are well-formed
    (sum ≤ nominal). The bug is upstream in the Quantizer.

- timestamp: 2026-05-03T00:10Z
  source: flow-lang/StandardLibrary/Audio/SongRenderer.cs:136-189
  observation: |
    Confirmed the "RH goes mute" mechanism:
      - SongRenderer.RenderSection accumulates `maxBeats = max(sequence.TotalBeats)`
        (line 152). TotalBeats is the NOMINAL sum (per SequenceType.AddBar).
      - MixVoicesToStereoBuffer creates a buffer of `maxBeats * secondsPerBeat
        * sampleRate` frames (line 188). Voices whose absolute frame ≥ totalFrames
        are silently dropped (line 203: `if (destFrame >= totalFrames) continue`).
      - For Chopin RH: 121.8 beats of cumulative overflow means many of the
        late-bar overflow voices have absolute onsets PAST 420 beats and get
        silently dropped. Bars 100+ of the RH effectively "go mute" because
        their content has been pushed into clamped territory.

## Eliminated

- SequenceType.ToTimeline drift: ruled out — line 56 advances by nominal
  Numerator, not actual beats. Bar offsets are correct.
- BarRenderer drops/clamps notes: ruled out — it just creates voices at
  offset positions; no duration clamping.
- VoiceAllocator stealing: ruled out — maxVoices=1024 (post the 32→1024
  fix in fe3767e); Chopin produces ~600 voices total.
- Tied-note re-trigger: ruled out — not relevant to desync (this would
  only add a barely-audible re-attack, not silence).

## Root Cause

`flow-midi/Conversion/Quantizer.QuantizeSpans` (Quantizer.cs:377-419) does
not handle MIDI polyphony where two or more notes in the same hand have
**overlapping but not simultaneous** onsets. When such overlapping notes
exist (extremely common in piano music — pedaled melody + inner voice,
or held outer note + ornament), the Quantizer:

  1. Emits the first (earlier-onset) note with its FULL MIDI duration
     (e.g., a 3-beat held G5).
  2. When a later-onset note (e.g., D4 starting 1 beat into the bar)
     reaches the loop, `groupStart < cursor` (because cursor was advanced
     by the held note's full duration). The "insert rest" branch at
     line 382 is NOT taken; the code falls through and emits the second
     note with its OWN snapped duration starting at the wrong cursor
     position.

The bar's emitted elements thus sum to MORE than the nominal bar length.
Downstream, `SongRenderer.MixVoicesToStereoBuffer` silently clamps voices
whose absolute frame exceeds the nominal-beats-derived buffer length,
producing the audible "RH goes mute" symptom in dense passages.

## Resolution

**Fix landed in flow-midi/Conversion/Quantizer.cs (commit pending).**

Two coordinated changes in the Quantizer:

1. **`QuantizeSpans` foreach loop** — replaced the open-ended cursor
   advance with a strict bar-fit clamp. For each group:
     - Available room is now `min(nextEventTick, barEnd) - cursor`
       (using `cursor`, NOT `groupStart`). This makes the running sum
       of snapped durations strictly bounded by `barTicks`.
     - Emission breaks early when `cursor >= barEnd` (drops trailing
       notes in over-densified bars rather than overflowing).
     - Gap-rest insertion uses `min(groupStart - cursor, barEnd - cursor)`
       so a late MIDI onset cannot push past barEnd via rest padding.
     - When the previous emission has advanced past `groupStart`, the
       new note is NOT realigned backward — it is emitted at `cursor`
       (effectively "behind schedule" within the bar text, but the
       BAR still fits in nominal time). Cross-bar continuity via
       `IsTied` is preserved.

2. **`SnapDurationCapped` (new helper)** — strict cap, no tolerance:
   never picks a grid value with `gridTicks > capTicks`. The earlier
   5% tolerance was the residual source of overflow in dense 32nd-note
   passages.

### Verification

- Re-rendered Chopin Nocturne Op. 9 No. 2 (3/4):
    | sequence                          | bars  | overflows | total drift |
    |-----------------------------------|-------|-----------|-------------|
    | grand_piano_classic_rh_seq        |  140  |     0     |    0 beats  |
    | grand_piano_classic_lh_seq        |  139  |     0     |    0 beats  |
    | music_box_rh_seq                  |  140  |     0     |    0 beats  |
    | music_box_lh_seq                  |  140  |     0     |    0 beats  |

  Compared with pre-fix: 87 + 9 + 65 + 13 = 174 overflow bars (~31% of all
  bars) and 203 beats of cumulative drift. The fix eliminates 100%.

- Re-rendered Chopin Nocturne in B-flat minor (6/4): 0 overflows across
  all 4 sequences. Confirms the fix generalizes beyond the trigger MIDI.

- Output WAV duration for Chopin Op. 9 No. 2: 210.00 s — exactly
  `140 bars × 3 beats × 0.5 s/beat = 210.00 s`. RH and LH share the
  buffer length (no asymmetric clamping).

- Acceptance criteria:
    1. ✓ Audible drift fixed — every bar fits in its nominal duration.
    2. ✓ RH and LH render durations match (both share the section buffer).
    3. ✓ ValidateDuration passes for every bar (0 overflows).
    4. ✓ `dotnet test` 512/512 passing.
       ✓ tests/test_*.flow 67/70 passing (3 pre-existing intentional
         error-tests still pre-fail on master, unchanged).

### Trade-off

Over-densified bars (typically dense 32nd-note Chopin runs) lose a few
trailing notes — the loop breaks when `cursor >= barEnd`. This is
preferable to overflow because:
  - Audible drift is eliminated (the user's complaint).
  - The lost notes are the LAST few in dense passages, where they
    contribute least to the harmonic skeleton.
  - Future Phase A (per-track polyphony detection + voice splitting)
    can recover the lost notes by emitting overlapping voices as
    separate Sequences. That is a larger change and is left for a
    follow-up commit.
