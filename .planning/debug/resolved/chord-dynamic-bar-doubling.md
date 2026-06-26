---
slug: chord-dynamic-bar-doubling
status: resolved
trigger: |
  A note-stream bar that mixes a dynamic marking (f/ff/mf/...) with chords
  renders at ~2x its notated duration. Chord tones appear to advance the
  beat cursor instead of sharing one onset, but ONLY when a dynamic token is
  present in the bar. This desyncs midi2flow-converted piano: the chord-heavy
  right hand drags progressively behind the steady left hand ("drunk
  pianist"). Distinct from bar-overflow-rh-desync (fixed 2026-05-03) — there
  the .flow bars genuinely summed > nominal; HERE the .flow bars are
  well-formed (sum to exactly the bar capacity) yet the audio render doubles
  them. User goal: pin the exact root cause and fix it. A separate follow-up
  will add a `--no-dynamics` flag to `flow midi2flow` (user chose "both").
created: 2026-06-25
updated: 2026-06-25
---

# Debug Session: chord-dynamic-bar-doubling

## Symptoms

**Expected behavior:**
A 4/4 note-stream bar whose notated element durations sum to 4 beats should
render as exactly 4 beats of audio, whether or not it begins with a dynamic
marking and whether or not it contains chords. All parallel sequences in a
song stay bar-aligned.

**Actual behavior:**
A bar that contains BOTH a dynamic token (f/ff/mf/...) AND chords renders at
~2x (8 beats for a 4-beat bar). In a multi-sequence song the chord-dense
right-hand voice accumulates this doubling and drifts ~40 s late by the end
of a ~2.5 min piece, sounding like a drunk pianist (late, out-of-order,
apparent pauses). The simple single-note left hand is unaffected.

**Error messages:** None. Failure is auditory / timing-only.

**Timeline:**
Surfaced converting ~/Downloads/ragtime.mid (chord-heavy ragtime RH). The
related bar-overflow-rh-desync bug was fixed 2026-05-03; this is a different
mechanism (well-formed bars rendering long, not overfull bars).

**Reproduction (minimal, confirmed):**
Write this .flow and render with the interpreter:
```
use "@std"
use "@audio"
tempo 175 { timesig 4/4 { key Cmajor {
  section sec { Sequence s = | f [C4 E4 G4]e _ e [C4 E4 G4]e _ e [C4 E4 G4]e _ e [C4 E4 G4]e _ t A5+s. | <repeat the same bar 7 more times> | }
  Song s2 = [sec]
  (writeWav "/tmp/x.wav" s2 "sine")
} } }
```
Then read /tmp/x.wav duration. 8 copies of a 4-beat bar @175 BPM should be
10.97 s; the bug makes it 21.94 s (each bar doubled to 8 beats).
- Removing the leading `f` → renders correct 10.97 s.
- Replacing the chords with single notes → renders correct.
So the necessary combination is: a dynamic token + chords in the same bar.

Full repro: `dotnet run --project flow-cli -- midi2flow ~/Downloads/ragtime.mid -o out.flow`,
then render out.flow — `track4_seq` (dense RH voice) renders 194.1 s vs its
notated 153.6 s (ratio 1.26); the other 5 sequences render at ratio ~1.00.

## Goal

A well-formed note-stream bar renders at its notated duration regardless of
dynamics/chords. Acceptance:
1. The minimal repro bar renders at 4 beats (8 copies = 10.97 s @175 BPM),
   WITH the leading `f` present.
2. Re-converted ragtime.mid: every sequence renders within ~1% of nominal;
   RH and LH stay locked (no audible drift).
3. Dynamics still take effect on notes (velocity fidelity preserved) — the
   fix must not be "drop dynamics".
4. Existing tests stay green: `dotnet test` and `tests/test_*.flow` (modulo
   any pre-existing intentional error-tests).
5. Add a regression test pinning a dynamic+chord bar's rendered/compiled
   beat-count to its nominal capacity.

## Suspected Files

- `flow-lang/Parsing/Parser.NoteStream.cs:247-300` — chord-bracket parse
  (line 260 builds ChordElement; note it does NOT pass stickyVelocity) vs
  dynamic-marking parse (line 290-300). PRIME SUSPECT: a dynamic token in
  the element loop may be changing how the following chord is parsed, or the
  chord's tones are not getting the chord-tone flag downstream.
- `flow-lang/Lexing/SimpleLexer.cs` — music-literal-aware tokenizer. Check
  how `f [C4 E4 G4]e` tokenizes vs `[C4 E4 G4] q`; a preceding dynamic may
  change chord tokenization (the bug needs the dynamic adjacent to chords).
- `flow-lang/Runtime/NoteStreamCompiler.cs:892` CompileChordElement sets
  isChordTone:!first; check whether that flag survives when velocity
  interpolation (InterpolateVelocities, runs when dynamics vary) rewrites
  the MusicalNoteData list — InterpolateVelocities reconstructs notes via
  `new MusicalNoteData(...)` and may DROP the isChordTone flag.
- `flow-lang/TypeSystem/SpecialTypes/BarType.cs:187` ToTimeline — chord
  tones (IsChordTone) correctly do not advance currentBeat; so the bug is
  that tones arrive with IsChordTone=false when a dynamic is present.

## Current Focus

reasoning_checkpoint:
  hypothesis: |
    NoteStreamCompiler.InterpolateVelocities (NoteStreamCompiler.cs:525-526)
    rebuilds each interpolated MIDDLE note with the 12-arg MusicalNoteData ctor,
    which omits the 5 trailing fields — isChordTone, DurationFraction, OnsetOffset,
    DurationOverlap, PortamentoMs — so isChordTone resets to false. In
    BarType.ToTimeline (BarType.cs:199) a tone with IsChordTone=false advances
    currentBeat instead of sharing lastLeadOnset, so a bar with chords + ≥2
    distinct velocities (which a leading dynamic produces) doubles its beat-sum.
  confirming_evidence:
    - "Ctor signature NoteType.cs:292 — isChordTone is the 18th param, default false; the InterpolateVelocities call stops at sourceLength (12th arg)."
    - "BarType.ToTimeline:199 stacks at lastLeadOnset WITHOUT advancing currentBeat only when IsChordTone=true."
    - "Empirical: doubling repro (leading f + chords, 8 bars @175 BPM 4/4 sine) renders 21.943s = exactly 2x the 10.97s nominal."
    - "Eliminated set already proves: remove f -> correct; single notes + f -> correct; chords without dynamics -> correct."
    - "Same audit (NoteType.cs:332-339) already fixed this exact drop-on-reconstruct pattern for transforms by routing through With(...); InterpolateVelocities was the one path missed."
  falsification_test: |
    After routing the rebuild through notes[i].With(velocity: vel) (which copies
    isChordTone + the other 4 fields by construction), the same repro must render
    ~10.97s. If it still renders ~21.94s, the hypothesis is wrong.
  fix_rationale: |
    With(velocity: vel) overrides ONLY velocity and passes every other field through
    null-coalesce, so isChordTone (and DurationFraction/OnsetOffset/DurationOverlap/
    PortamentoMs) survive. Dynamics still take effect because velocity is still set.
    Both InterpolateVelocities call sites (line 253 main path, line 351 per-voice
    CompileVoiceBlock) invoke the SAME static method, so one edit fixes both.
  blind_spots: |
    Must confirm non-chord velocity-interpolation behavior is unchanged (the only
    semantic delta is preserved flags). Must run full tests/test_*.flow + dotnet test
    to catch any baseline that depended on the buggy reconstruction.

- hypothesis: STRONG LEAD — `NoteStreamCompiler.InterpolateVelocities`
  reconstructs each non-rest MusicalNoteData with `new MusicalNoteData(...)`
  but its constructor-arg list likely OMITS the `isChordTone` flag (defaults
  to false). Interpolation only runs when ≥2 distinct velocities exist in the
  bar — which a leading dynamic token + default-velocity chord tones
  produces. Result: after interpolation, chord tones lose IsChordTone → each
  advances the beat cursor in BarType.ToTimeline → bar doubles. This explains
  why the bug needs BOTH a dynamic (triggers interpolation) AND chords
  (something to lose the flag on), and why single-note bars are unaffected.
  NEEDS VERIFICATION against the actual InterpolateVelocities code
  (NoteStreamCompiler.cs:482-530) — check whether the reconstructed
  MusicalNoteData passes isChordTone through.

- next_action: RESOLVED. Human confirmed (2026-06-25) the re-rendered ragtime
  (153.6 s) sounds bar-locked — "everything sounds good", RH locked to LH, no
  drunk-pianist drift. Fix committed, session archived to resolved/, knowledge
  base updated. Two new observations (per-beat click/static + dreamy wash) were
  raised but are confirmed instrument-character (clean WAV, no clipping/regression)
  and tracked separately — see follow-up notes. Did NOT touch ~/Downloads/ragtime.flow.

## Evidence

- timestamp: 2026-06-25T00:00Z
  source: per-sequence WAV render measurements of converted ragtime.flow
  observation: |
    Rendered each of the 6 sequences alone (sine synth) and measured WAV
    duration vs nominal (bars x 4 beats @175 BPM):
      track1_seq 152.9s (nom 152.2) ratio 1.00
      track2_seq 155.9s (nom 153.6) ratio 1.01
      track3_seq 104.2s (nom 104.2) ratio 1.00
      track4_seq 194.1s (nom 153.6) ratio 1.26  <-- BROKEN
      track5_seq 141.4s (nom 141.3) ratio 1.00
      track6_seq 153.6s (nom 153.6) ratio 1.00
    Only track4 (a dense, chord-heavy RH voice) overruns. Text-parse of every
    bar in the .flow shows all bars sum to exactly 4.0 beats — so the .flow
    notation is correct; the renderer is the problem.

- timestamp: 2026-06-25T00:05Z
  source: token-by-token bisection of the exact bar106 of track4_seq
  observation: |
    bar106 = `f [G4 C5 E5]e _ e [C5 E5 G5]e _ e [C5+ E5 A5+]e _ e [C5+ E5 G5]e _ t A5+s.`
    renders as 8.00 beats (should be 4.0). Bisection: the bar renders correct
    (4.0) until the FINAL note is added, at which point it flips to 8.0 — i.e.
    the doubling is all-or-nothing for the whole bar, consistent with a
    per-bar compile step (InterpolateVelocities) re-marking tones, NOT a
    per-token duration error.

- timestamp: 2026-06-25T00:10Z
  source: controlled single-bar render tests (repeated bar x8, sine synth)
  observation: |
    - Remove leading `f` from bar106 -> 4.00 (FIXED).
    - Keep `f`, drop the final sharp -> still 8.00 (sharp irrelevant).
    - Replace chords with single notes (same rhythm), keep `f` -> 4.00
      (chords required).
    - `f C4 q C4 q C4 q C4 q` (dynamic + single notes, 4 beats) -> 4.00
      (single notes immune).
    - Stripping ALL dynamic tokens from the real track4_seq -> renders
      155.0 s (correct). PROVEN: dynamics are the trigger; the fix must keep
      dynamics working while not doubling chord bars.
    Net: doubling requires a dynamic token AND chords in the same bar.

- timestamp: 2026-06-25T00:15Z
  source: code read — BarType.ToTimeline + render path
  observation: |
    BarType.ToTimeline (BarType.cs:187) is correct: notes with
    IsChordTone=true do NOT advance currentBeat. RenderBarsToVoices
    (BarRenderer.cs:201) advances per bar by BarCapacityQuarters (fixed
    grid), and writeMidi path is correctly timed. So the doubling must come
    from chord tones arriving with IsChordTone=false. The chord parser
    (Parser.NoteStream.cs:260) and CompileChordElement
    (NoteStreamCompiler.cs:892 isChordTone:!first) look correct in isolation,
    which points at a LATER per-bar mutation that drops the flag —
    InterpolateVelocities is the prime suspect because it (a) only runs when
    velocities vary (a dynamic creates variation) and (b) reconstructs
    MusicalNoteData via `new MusicalNoteData(...)`.

## Eliminated

- Converter bar math / bar-overflow: ruled out — every generated .flow bar
  sums to exactly 4.0 beats (text-parsed). This is NOT a recurrence of
  bar-overflow-rh-desync.
- Sampled-piano varispeed buffer length: ruled out — bug reproduces
  identically with sine and organ synths (synth-independent).
- Tied notes (`~`): ruled out — stripping all `~` from track4_seq leaves it
  at 194.1 s (unchanged).
- MIDI export path: ruled out — writeMidi renders the song with correct
  timing (~447 quarters end-to-end). Only the audio render path doubles.
- Specific pitches / sharps / chord-note-count: ruled out — identical
  C-major triads reproduce the doubling once a dynamic + the dense
  chord+rest structure is present; sharps are irrelevant.

## Evidence (verification)

- timestamp: 2026-06-25T01:00Z
  source: code read — NoteStreamCompiler.cs:482-530 + NoteType.cs:292/325-368
  observation: |
    CONFIRMED the lead. InterpolateVelocities rebuilt each interpolated MIDDLE
    note with the 12-arg MusicalNoteData ctor (stops at sourceLength). The ctor
    has 18 params; the 5 trailing — durationFraction / onsetOffset /
    durationOverlap / portamentoMs / isChordTone — all defaulted, so isChordTone
    reset to false. The MusicalNoteData.With(...) builder already exists for
    exactly this (NoteType.cs:332-339 documents the 2026-06-09 audit routing
    transforms through With() to stop the same trailing-field drop);
    InterpolateVelocities was the one path the audit missed. BarType.ToTimeline:199
    only stacks (no cursor advance) when IsChordTone=true → dropped flag → cursor
    advances on chord tones → bar beat-sum balloons.

- timestamp: 2026-06-25T01:05Z
  source: empirical render measurement (pre-fix vs post-fix, sine synth)
  observation: |
    Doubling repro (leading f + 8 chord bars @175 BPM 4/4):
      PRE-FIX  21.943 s  (each 4-beat bar → 8 beats)
      POST-FIX 10.971 s  (correct nominal)
    Originally-broken ragtime track4_seq (dense chord RH):
      PRE-FIX  194.1 s (ratio 1.26)
      POST-FIX 153.621 s (ratio 1.00) — RH now bar-locked.
    Dedicated regression-test bar (f + 3 quarter-chords + plain D4q ×8 bars):
      PRE-FIX  27.429 s (10 beats/bar: 6 stray chord tones advance the cursor)
      POST-FIX 10.971 s. Confirms the test genuinely fails on pre-fix code.

- timestamp: 2026-06-25T01:10Z
  source: fix applied — NoteStreamCompiler.cs InterpolateVelocities
  observation: |
    Replaced `notes[i] = new MusicalNoteData(...12 args...)` with
    `notes[i] = notes[i].With(velocity: vel)`. With() overrides ONLY velocity and
    null-coalesces every other field through, so isChordTone (+ the other 4
    trailing fields) survive while dynamics still set velocity. One edit covers
    BOTH call sites (line 253 main note-stream path + line 351 per-voice
    CompileVoiceBlock) — they invoke the same static method.

- timestamp: 2026-06-25T01:20Z
  source: test suite — dotnet test + tests/test_*.flow
  observation: |
    Added flow-lang.Tests/Integration/Debug2026/ChordDynamicBarDoublingTests.cs
    (4 Facts: beat-count pin, IsChordTone-survives + velocity-still-interpolates,
    no-dynamic control, end-to-end render duration). All 4 pass.
    Full dotnet test: 2730 passed / 19 skipped / 1 FAILED. The 1 failure is
    Phase41ShowcaseRmsTests.Showcase_RmsWithinTolerance — PROVEN PRE-EXISTING and
    UNRELATED: it fails IDENTICALLY with my fix git-stashed (delta 1.06 dB in the
    100-200ms granular-riser window). pulse.flow has NO chords and uniform-velocity
    note streams, so InterpolateVelocities returns at the <2-distinct-velocities
    guard — my changed line is never reached for it. The failure is pre-existing
    cross-process PRNG non-determinism in pulse.flow's granular/euclidean render
    (two separate renders differ run-to-run, pre AND post fix; frames identical).
    All 136 tests/test_*.flow scripts run clean.

## Resolution

root_cause: |
  NoteStreamCompiler.InterpolateVelocities (NoteStreamCompiler.cs:525-526)
  reconstructed each interpolated MIDDLE note with the 12-arg MusicalNoteData
  constructor, which silently reset the 5 trailing fields — IsChordTone,
  DurationFraction, OnsetOffset, DurationOverlap, PortamentoMs — to their
  defaults. Dropping IsChordTone made interpolated chord tones advance the bar's
  beat cursor in BarType.ToTimeline instead of sharing the leading tone's onset,
  so a bar that held both a dynamic (→ ≥2 distinct velocities → interpolation
  runs) and chords (→ middle tones to lose the flag on) rendered at well over its
  notated duration. This is the same drop-on-reconstruct pattern the 2026-06-09
  transform audit fixed via MusicalNoteData.With(...); InterpolateVelocities was
  the single path that audit missed.

fix: |
  In InterpolateVelocities, rebuild the interpolated note through
  notes[i].With(velocity: vel) instead of the 12-arg ctor. With() overrides only
  velocity and passes every other field through by null-coalesce, so IsChordTone
  (and the other 4 trailing fields) are preserved by construction while dynamics
  still take effect. Single edit fixes both InterpolateVelocities call sites
  (main note-stream + per-voice CompileVoiceBlock).

verification: |
  - Minimal doubling repro: 21.943 s → 10.971 s (nominal). Leading f retained.
  - ragtime track4_seq: 194.1 s (1.26) → 153.621 s (1.00); RH bar-locked.
  - Dynamics preserved: regression test asserts middle-note velocities still vary
    (interpolation still runs) — fix does NOT disable dynamics.
  - New regression suite (4 Facts) passes; proven to FAIL on pre-fix code.
  - Full dotnet test: only pre-existing, unrelated Phase41 RMS-flakiness fails
    (fails identically with fix stashed). 136/136 flow scripts clean.
  - HUMAN audible confirmation (2026-06-25): user listened to the re-rendered
    ragtime (153.6 s) and confirmed the timing/drift bug is gone — "everything
    sounds good", right hand locked to the left hand, no more drunk-pianist drift.
  - NOTE: the single dotnet-test failure (Phase41ShowcaseRmsTests.Showcase_RmsWithinTolerance)
    is PRE-EXISTING and UNRELATED — proven to fail identically with this fix
    git-stashed (pre-existing cross-process PRNG non-determinism in pulse.flow,
    which has no chords / uniform velocities so the changed line is never reached).
    It does NOT block resolution.

files_changed:
  - flow-lang/Runtime/NoteStreamCompiler.cs (InterpolateVelocities → With(velocity:))
  - flow-lang.Tests/Integration/Debug2026/ChordDynamicBarDoublingTests.cs (new regression test)

## Notes for follow-up (out of scope for THIS session's fix)

- PRE-EXISTING (not introduced here): Phase41ShowcaseRmsTests.Showcase_RmsWithinTolerance
  is flaky — examples/edm/pulse.flow does not render two-run cmp-clean across
  separate processes (granular riser / euclidean jitter), so its committed RMS
  baseline drifts past ±0.5 dB run-to-run. Fails with this fix stashed too.
  Worth a separate session (seed audit on granular/euclidean reseed-at-boundary).


- After the bug fix lands, add a `--no-dynamics` flag to `flow midi2flow`
  (FlowGenerator.cs emits the per-bar dynamic tokens via VelocityToDynamic /
  sweep-0614 sticky-dynamic logic at FlowGenerator.cs:304-392; Midi2FlowCommand.cs
  is the CLI surface). User wants this as a robustness escape hatch.

- SEPARATE from this timing bug (raised at human-verify, NOT a regression from
  this fix): user reports a periodic per-beat click/static + an overall "dreamy"
  wash on the re-rendered ragtime. Independent WAV analysis confirms the render is
  CLEAN — peak 7931/32767 (~24%), zero clipped samples, no per-beat waveform
  discontinuities (only 18 small transients in 154 s). So this is NOT distortion,
  clipping, or a regression — it is the default sampled-piano instrument's
  character (U-Iowa samples carry recorded room ambience + per-note noise floor;
  dense overlapping chords now playing in CORRECT time stack that ambience/noise
  so it pulses with onset density). Instrument-character / synth-quality, tracked
  separately, out of scope for this session.
</content>
</invoke>
