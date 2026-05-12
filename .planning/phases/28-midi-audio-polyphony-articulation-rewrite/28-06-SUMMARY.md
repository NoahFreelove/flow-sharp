---
phase: 28-midi-audio-polyphony-articulation-rewrite
plan: 06
status: complete
requirements: [SPEC-2, SPEC-8]
self_check: PASSED
test_count_before: 970
test_count_after: 979
new_facts: 9
commits:
  - d6b2c13 test(28-06): RMS-windowed regression infra + 6 diagnostic facts
  - 5166beb feat(28-06): voice-block rendering + held-note + voice-block tests
key_files:
  created:
    - flow-lang.Tests/Helpers/WavReader.cs
    - flow-lang.Tests/Helpers/RmsRegressionTests.cs
    - flow-lang.Tests/Unit/Phase28/RmsRegressionDiagnosticTests.cs
    - flow-lang.Tests/Integration/Phase28/HeldNoteRmsTests.cs
    - flow-lang.Tests/Integration/Phase28/VoiceBlockRenderTests.cs
    - flow-lang.Tests/baselines/Phase28/staccato_baseline.wav
  modified:
    - flow-lang/StandardLibrary/Audio/BarRenderer.cs (read ParallelVoices)
    - flow-lang/StandardLibrary/Audio/MidiExport.cs (emit voice-block events)
    - .gitignore (allow baselines/**/*.wav and *.mid)
---

## Plan 06 — RMS Test Infra + Voice-Block Rendering + Migration Audit

### What shipped

#### SPEC-8 RMS-windowed regression infrastructure

1. **WavReader** (`flow-lang.Tests/Helpers/WavReader.cs`) — RIFF/WAVE
   parser supporting 16/24/32-bit PCM mono/stereo. Skips unknown chunks
   (LIST/bext/fact). Returns `FlowLang.AudioBuffer`.

2. **RmsRegressionTests.AssertRmsWithinTolerance** — frame-count exact
   match + per-window RMS dB diff with default ±0.5 dB / 100 ms band
   (SPEC-8 locked). Non-default tolerance requires `overrideReason`.
   Round-trips the rendered buffer through `FileIO.WriteWav` + `WavReader`
   so dither noise matches the baseline (without round-trip, fresh
   silence reads as -120 dB but baseline reads ~-91 dB → spurious diagnostic).
   Diagnostic format mirrors SPEC-8: `"RMS deviation in window N
   (XXXms-YYYms): expected -A dB, got -B dB (delta D dB exceeds tolerance T dB)"`.

3. **6 diagnostic facts** (`RmsRegressionDiagnosticTests`):
   - `WavReader_RoundTrip` — write/read sample-by-sample within 1.5e-4
   - `RmsRegression_PositiveBaseline` — pass case
   - `RmsRegression_NegativeDiagnostic` — Normal vs Staccato baseline →
     diagnostic message asserted
   - `RmsRegression_FrameCountMismatch` — different bar length →
     frame-count Assert fires before window iteration
   - `RmsRegression_ToleranceOverrideRequiresReason` — ArgumentException
     when override sans reason
   - `RmsRegression_ToleranceOverrideAcceptedWithReason` — passes when
     reason supplied

#### SPEC-1 voice-block rendering (closed gap from Plan 28-01)

Plan 28-01 added `BarData.ParallelVoices` but no renderer read it.
Plan 28-06 closes that loop:

4. **`BarRenderer.RenderBarToVoices`** — when `bar.ParallelVoices != null`,
   recurses into each voice as a child bar at offset 0; concatenates the
   resulting `Voice` list. SongRenderer's mix-to-stereo sums them →
   true polyphony.

5. **`MidiExport.ExportMidiInternal`** — when `bar.ParallelVoices != null`,
   walks each voice's `MusicalNotes` at the parent's `seqTick`, emitting
   per-voice NoteOn/NoteOff pairs with channel-stamped events. Voices
   produce overlapping events on the SAME track (consistent with SPEC-1
   acceptance c).

#### SPEC-1/SPEC-2 acceptance facts

6. **`HeldNoteRmsTests.HeldNote_NonTruncation`** — render the canonical
   `| {voice C2w} {voice C5q D5q E5q F5q} |` with the organ synth
   (sustain=1.0 isolates voice routing from natural decay), bandpass
   the C2 fundamental band (50-90 Hz), assert last-50ms RMS ≥ 50% of
   first-50ms RMS. Piano synth's 0.6-sec decay would mask voice-routing
   bugs — organ proves the held note is sustaining as intended.

7. **`VoiceBlockRenderTests.VoiceBlock_HeldPlusRunning`** — for each
   running pitch (C5/D5/E5/F5 at 0/0.5/1.0/1.5 sec), narrow-bandpass to
   that pitch's fundamental and assert RMS in its expected window > RMS
   in any other pitch's window. Per-pitch fingerprint avoids brittle
   attack-derivative thresholds.

8. **`VoiceBlockRenderTests.VoiceBlock_MidiNoteTickPositions`** — read
   back generated .mid; assert exactly 5 NoteOn events:
   - C4=60 + C5=72 simultaneous at tick 0
   - D5=74 at tick 480, E5=76 at tick 960, F5=77 at tick 1440
   - C4 NoteOff at tick 1920 (whole-note duration at TPQN 480)

#### Phase 18/25/27 byte-pin migration audit (Task 6)

Audited all `SequenceEqual` and `Assert.Equal.*[Bb]ytes` matches in
`flow-lang.Tests/Integration/Phase{18,25,27}/`. Every byte-pin test
classified as **TWO-RUN** (compares run1 vs run2 within the same git
SHA — preserves the determinism contract per SPEC-8 / Constraints).
**Zero PIN-against-committed-file tests found** — there's nothing to
migrate. All 14 ByteIdentical Tutorial/Showcase/PragmaCompanions tests
remain GREEN under Phase 28 (Phase 28's render output is deterministic
across two runs even though it differs from the pre-Phase-28 byte
content — which is the SPEC-8 design).

### Test parallelism note

`FileIO.WriteWav` uses a SHARED static dither RNG that's reset at start
of each export. Parallel tests writing WAVs interleave dither samples →
non-deterministic silent-window noise → spurious RMS-window diagnostics.
`RmsRegressionDiagnosticTests` joins the existing `"FlowScripts"` xUnit
Collection so its FileIO calls serialize against the broader engine-runner
pool. Three consecutive 979/979 GREEN runs verify the fix.

### Test counts

- Phase 28 facts: **105/105 GREEN** (4 + 17 + 55 + 5 + 9 + 9 + 6
  across Plans 01..06)
- Phase 18/25/27 ByteIdentical: **14/14 GREEN** (two-run determinism
  preserved)
- Full unit suite: **979/979 GREEN** (was 970 — +9 net new), three
  consecutive runs without flake.

### Self-Check: PASSED

Build clean, all targeted tests pass, full suite green three times in a
row, no architectural deviations from PLAN.md beyond the
test-isolation Collection annotation.

### Deviations

1. **Voice-block render gap closed in Plan 06 (not 02/03 as Plan 01
   SUMMARY anticipated)**: Plan 28-01's SUMMARY said "Renderer changes
   ship in Plans 02/03" but neither plan touched BarRenderer's
   ParallelVoices reading. Plan 28-06's HeldNote/VoiceBlock tests
   surfaced the gap; closed inline.

2. **Held-note synth choice (organ vs piano)**: PLAN's example fixture
   used `"piano"` but piano's 0.6-sec natural decay drops a 2-sec
   held note's amplitude well below 50% via natural decay alone,
   masking voice-routing bugs. Switched to organ (sustain=1.0,
   release=0.01) which isolates the routing from synth-envelope decay.

3. **Attack-detection algorithm (per-pitch RMS vs derivative)**: PLAN's
   approach used amplitude-derivative thresholds which were brittle
   on organ's flat envelope. Replaced with per-pitch narrow-bandpass
   RMS comparison — the running C5..F5 line's energy concentrates in
   each pitch's expected 0.5-sec window and is less in others. Same
   SPEC-1 acceptance, more robust algorithm.

4. **Test parallelism via Collection (not AsyncLocal as Plan 28-05's
   VoiceAllocator)**: FileIO's dither RNG is shared production code
   used by every test that writes a WAV. Making it AsyncLocal would
   change production behavior. The Collection annotation is the
   minimal-footprint test-only fix.

### Hand-off to dependent plans

- **Plan 28-07 (closure)** can spot-check the new RMS infra by listening
  to baselines under `flow-lang.Tests/baselines/Phase28/` (currently
  just `staccato_baseline.wav`). Voice-block render tests confirm the
  Phase 28 polyphony intent is intact across both audio + MIDI paths.
