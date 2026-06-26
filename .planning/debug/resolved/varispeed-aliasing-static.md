---
slug: varispeed-aliasing-static
status: resolved
trigger: |
  Sampled-piano (and any varispeed-resampled sampled-instrument) renders carry
  an audible per-beat "static"/grit, worst in dense high-register passages
  (e.g. ragtime right hand). Confirmed NOT clipping, NOT a note-boundary click,
  NOT recorded attack noise, and NOT the release tail (separate dreamy issue,
  already solved via release=0.4s). High notes alias: HF roughness scales with
  pitch. User goal: reduce/remove the high-note aliasing so the sampled piano
  is clean and "true to the MIDI". User will verify by EAR at a checkpoint.
created: 2026-06-26
updated: 2026-06-26
---

# Debug Session: varispeed-aliasing-static

## Symptoms

**Expected:** A sampled-piano note (any pitch) renders clean — no gritty
broadband "static". Dense high-register passages stay clean.

**Actual:** A periodic "static/crackle, like a loose headphone aux" on every
beat in the rendered piano, worst where the right hand is high+dense. Organ
(pure synthesis, no resampling) is clean; sine clicks for a DIFFERENT reason
(note-end cutoff — out of scope here).

**Errors:** None — auditory only.

**Timeline:** Surfaced after the chord-dynamic-bar-doubling timing fix made the
notes line up correctly, exposing the per-onset grit. Rendering with a short
release (0.4s) removed the "dreamy" wash but the static remained.

**Reproduction:**
```
dotnet run --project flow-cli -- midi2flow ~/Downloads/ragtime.mid -o /tmp/r.flow
# render with crisp piano (release 0.4s) and listen: per-beat static in the high RH
# Or single-note HF test (see Evidence) — C7 is ~9x grittier than C4.
```

## Goal

Sampled-instrument varispeed resampling does not introduce audible aliasing
when shifting notes up in pitch. Acceptance:
1. HF-roughness of a high sampled-piano note (C7) drops to near the C4 ratio
   (currently C7 hf/rms ≈ 0.078 vs C4 ≈ 0.009 — ~9x; target: roughly flat
   across the range).
2. User confirms by ear: the per-beat static on the crisp (release=0.4s)
   ragtime piano render is gone / much reduced. (HUMAN-VERIFY checkpoint.)
3. Two-run cmp-clean preserved (deterministic — the resampler stays pure).
4. Existing sampled-instrument RMS baselines handled deliberately: either
   re-recorded (they legitimately change) with a documented note, or the
   change kept within SPEC-8 RMS tolerance (±0.5 dB / 100 ms). `dotnet test`
   green after baseline handling.
5. A test pins the anti-aliasing win (e.g. HF-energy above target Nyquist for
   an upshifted note is bounded / much lower than the linear-interp baseline).

## Root Cause (CONFIRMED by code read + measurement)

`FileIO.VarispeedResample` (flow-lang/StandardLibrary/Audio/FileIO.cs:376-393)
resamples by **linear interpolation only**:
```
newFrames = round(source.Frames / ratio);   // ratio = 2^(semitones/12)
srcPos = frame * ratio; s0,s1 = source[srcFrame], source[srcFrame+1];
result[frame] = s0 + frac*(s1 - s0);
```
For an upshift (target pitch ABOVE the nearest sample → `ratio > 1`) this
DECIMATES the source (steps through it faster, fewer output frames) with NO
anti-aliasing lowpass, so source content above the new Nyquist folds back as
aliasing → broadband grit that worsens with the shift amount. The sampled
piano bundle has sparse pitch points, so high notes need large upshifts →
large `semitonesShift` → audible per-onset static. This is the "static every
beat" in the chord-heavy high RH.

## Suspected Files / Fix Surface

- `flow-lang/StandardLibrary/Audio/FileIO.cs:376` `VarispeedResample` — THE fix
  site. Add an anti-aliasing lowpass before/within decimation when `ratio > 1`
  (cutoff ≈ (sampleRate/2)/ratio), and/or replace linear interp with a
  band-limited / windowed-sinc / polyphase resampler. (Cubic alone reduces but
  does not eliminate decimation aliasing — a pre-decimation lowpass is the
  correct lever.) Keep it pure/deterministic (two-run cmp-clean).
- Callers to weigh for blast radius (KEY SCOPING DECISION — see below):
  - `flow-lang/StandardLibrary/Audio/SampleCache.cs:271` GetVarispeed (tonal
    piano/brass/sax/strings/flute/bell — Phase 29/37).
  - `flow-lang/StandardLibrary/Audio/Sfz/SfzSampleCache.cs:89` GetVarispeed
    (SFZ sampler — Phase 33).
  - `flow-lang/StandardLibrary/Audio/FileIO.cs:348,366` — the `varispeed` /
    pitch `loadWav` builtins. NOTE: Phase 37 documented "loadWav varispeed
    unaffected" as a contract; a global change touches it.

## Current Focus

- ROOT CAUSE CONFIRMED 2026-06-26 (direct measurement): envelope/tail boundary
  discontinuity in SampledInstrumentRenderer.Render. The Phase 28 ADSR envelope is
  applied only to [0, authoredFrames) and its 0.05s RELEASE phase ramps the signal
  to ~0 at frame authoredFrames. The release-tail loop then starts at frame
  authoredFrames with level=1.0, multiplying the RAW (un-enveloped) sample → the
  signal JUMPS from ~0 back up to the full raw sample value in ONE sample. Measured
  on a loud isolated 16th note (D4, f): clean ~0.0001 through frames 3760-3779, then
  +0.02654 step at frame 3780 (= authoredFrames, t=0.0857s), then smooth decay. Step
  magnitude = raw sample amplitude at the authored-end frame → LARGE for short notes
  (authored end early in the still-loud sample), SUB-THRESHOLD for long isolated
  notes (sample has decayed by their authored end → why isolated quarters sounded
  clean). Ragtime RH is dense short notes whose authored-ends align on the beat grid
  → a per-beat train of steps that SUM (louder/lower notes → bigger raw values),
  reaching 0.069 (-23 dBFS) in the full mix = the audible "static." Baked into the
  WAV (render-side), matching user Q2.
- next_action: write falsification test (boundary jump must be ~neighbor-diff, not
  100x), confirm RED on current code, apply fix (envelope/tail continuity), confirm
  GREEN + re-measure ragtime event count drop, dotnet test + baseline handling, then
  human-verify CHECKPOINT (no commit/archive).

- AA RESAMPLER REVERTED 2026-06-26 (user decision Q1): the falsified decimation-
  aliasing fix is gone from the working tree (3 source files restored to HEAD,
  5-test fixture deleted), `dotnet build` green, blast radius zero. Falsification
  recorded in `.planning/debug/knowledge-base.md` so it is NOT re-attempted; the
  AA resampler is revivable for extreme-upshift quality only.
- User clarification Q2: static is heard ONLY by playing the rendered WAV in a
  media player (NOT the live `play`/PulseAudio path) → the grit is BAKED INTO the
  render → RENDER-side defect, not a real-time underrun.
- PIVOT (active): onset-transient hypothesis. hypothesis: per-beat "static" is the
  SUM of multiple simultaneous sampled-note onsets landing on the same beat (chords
  now stack after the timing fix) — non-zero first-frame DC / abrupt sample-start
  steps correlate and stack into an audible per-beat click that single isolated
  onsets (jump ≤43) never showed.
- next_action: (1) measure the first-frame (sample-start) amplitude of each cached
  sampled-piano layer — quantify the per-note onset step; (2) measure how those
  steps SUM across simultaneous onsets in a dense ragtime beat; (3) write a
  falsification test for the onset-sum hypothesis BEFORE any fix; (4) evaluate a
  ~2-5ms onset (and/or offset) declick fade on the SAMPLED render path; keep
  two-run cmp-clean. If confirmed + fix candidate → human-verify CHECKPOINT (no
  commit/archive). If falsified → INVESTIGATION INCONCLUSIVE / decision checkpoint.

reasoning_checkpoint:  # FALSIFIED — kept for the record; superseded below.
  hypothesis: "FileIO.VarispeedResample uses linear interpolation with no anti-aliasing lowpass; for upshift (ratio>1) it decimates the source so source content above the new (lower) Nyquist folds back as broadband aliasing → the per-beat 'static' that worsens with pitch (larger upshift)."
  confirming_evidence:
    - "Measured sustain-region HF roughness climbs monotonically with pitch: C4=0.009 -> C7=0.078 (~9x), tracking upshift amount (C7 needs +12 st off the C6/MIDI-84 sample)."
    - "Code read: VarispeedResample computes newFrames=round(frames/ratio) and linear-interpolates with zero pre-decimation filtering."
    - "Isolated piano note is clean at onset/offset (jumps <=43); grit is sustained, ruling out boundary clicks/attack noise — consistent with steady-state aliasing."
  falsification_test: "Band-limiting the source to the post-decimation Nyquist (cutoff=1/ratio) before/within resampling must drop C7 hf/rms toward the C4 ~0.009 level. If hf/rms stays ~0.078 after band-limited resampling, the hypothesis is wrong (grit is in the source sample, not introduced by resampling)."
  fix_rationale: "Replace linear interp with band-limited windowed-sinc interpolation for ratio>1 only — the sinc cutoff pulled to 1/ratio IS the missing anti-aliasing lowpass; addresses the root cause (decimation aliasing), not a symptom. ratio<=1 keeps linear interp (no aliasing introduced on downshift; avoids dulling + baseline churn)."
  blind_spots: "Post-fix hf/rms not yet measured (will measure). RMS baselines may or may not exceed SPEC-8 ±0.5 dB/100 ms — empirical, will run dotnet test. Cross-platform FP last-bit reproducibility of the trig-heavy kernel is outside the same-platform two-run contract (matches all existing DSP)."

reasoning_checkpoint_v2:  # CONFIRMED 2026-06-26 by direct waveform measurement.
  hypothesis: "SampledInstrumentRenderer.Render creates a step discontinuity at frame authoredFrames of every note: the ADSR envelope (baseRelease=0.05) is applied only to [0, authoredFrames) and ramps to ~0 there, but the release-tail loop restarts at level=1.0 multiplying the RAW sample → signal jumps from ~0 back to full sample amplitude. Short dense notes (ragtime RH) place this jump early in the still-loud sample and align it to the beat grid → summed per-beat steps = audible 'static'."
  confirming_evidence:
    - "Direct waveform dump of an isolated loud 16th (f D4): frames 3760-3779 all ~+0.0001 (envelope release floor), then frame 3780 (=authoredFrames, t=0.0857s) jumps +0.02654 then decays smoothly. 2nd-difference at the boundary is the dominant single-sample event."
    - "Full ragtime top transients are decaying-tail-then-step or step-then-decaying-tail at -23 dBFS with 2nd-diff ~+0.07; 780 events cluster on the 175bpm eighth grid (phase histogram peaks in bins 0-1)."
    - "Isolated single notes AND loud 4-note chords (quarter duration) end at -78 dBFS with ZERO |1st-diff|>0.02 events — falsifies onset-summing and tail-end truncation; the defect needs the SHORT authored window of dense notes to land the boundary jump while the sample is still loud."
    - "Voice-pool sweep (8/32/256) produces BYTE-IDENTICAL output (780 events each) — falsifies voice-stealing; per-sequence active voice count never exceeds the pool."
  falsification_test: "Render a short loud Normal piano note via SampledInstrumentRenderer; the single-sample jump at the envelope/tail boundary (frame authoredFrames) must be comparable to its smooth-tail neighbor diffs, not ~10-100x larger. If the boundary jump stays anomalously large after the continuity fix, the boundary is not the cause."
  fix_rationale: "Make the envelope and the exponential release tail meet CONTINUOUSLY: drop the redundant 0.05s ADSR release on the sampled path (baseRelease=0) so the envelope ends at the sustain level, and start the tail-decay loop at the envelope's boundary value (envelope[authoredFrames-1]) rather than a hard 1.0. For Normal (sustain=1.0) the tail rings out from 1.0 (ring-out preserved, boundary continuous); for Staccato (sustain=0) the tail starts at 0 (short detached note preserved, boundary continuous). Addresses the discontinuity directly — not a cosmetic declick."
  blind_spots: "Sampled-instrument RMS baselines (Phase28/37) may shift beyond SPEC-8 ±0.5 dB/100ms because removing the 0.05s release notch adds a little energy near each note end — will run dotnet test and re-baseline deliberately if needed. SFZ path (SfzRenderer) is separate and NOT touched; if it has the same boundary pattern that is a distinct follow-up (user's render is 'piano' = sampled, not SFZ). Two-run cmp-clean preserved (still pure/deterministic)."

## Evidence

- timestamp: 2026-06-26T00:00Z
  source: per-pitch HF-roughness measurement (isolated piano notes, sine synth ruled out)
  observation: |
    Sustain-region HF roughness (mean |2nd difference| / RMS) climbs with pitch:
      C4=0.009  C6=0.025  A5#=0.021  E6=0.035  C7=0.078  (C7 ≈ 9x C4)
    A single isolated piano note is clean at onset/attack/offset (jumps ≤43,
    attack HF == sustain HF) — so the grit is NOT a boundary click or attack
    noise; it is sustained aliasing that grows with upshift amount.
- timestamp: 2026-06-26T00:05Z
  source: code read — FileIO.VarispeedResample + SampleCache.GetVarispeed
  observation: |
    GetVarispeed computes semitonesShift = targetMidi - NearestSamplePitch, then
    FileIO.VarispeedResample(raw, 2^(shift/12)). VarispeedResample is pure linear
    interpolation with no anti-alias filter → confirmed decimation-aliasing on
    upshift. Same path used by SFZ sampler and the loadWav varispeed builtin.

- timestamp: 2026-06-26T(fix)Z
  source: implemented FileIO.VarispeedResampleAntiAliased (band-limited windowed-sinc,
           lobes=16, Blackman, cutoff=1/ratio; ratio<=1 delegates to linear) + 5 unit
           tests (flow-lang.Tests/Unit/VarispeedAntiAliasFacts.cs, all PASS)
  observation: |
    In isolation the new resampler is correct: on a 2x upshift it suppresses an
    above-new-Nyquist sine to <25% of the linear-interp RMS, preserves in-band
    content (>88%), is byte-identical for downshift/unity, and is deterministic.
    So the kernel works. The render path DOES use it (cmp of new-vs-old C7 render
    differs at byte 119). But the audible effect is negligible (next entry).

- timestamp: 2026-06-26T(falsify-1)Z
  source: A/B render measurement, isolated C7 (C6 sample, +12 st, ratio 2.0)
  observation: |
    new(anti-aliased) vs old(linear) C7: difference signal RMS = -45.3 dB rel to
    signal. Per-band A/B ratios ~1.0. The C6 piano sample's energy is concentrated
    below 6 kHz (2-6kHz band = 4e-7; 6-11kHz = 4e-9, ~100x down), so there is almost
    nothing above the new Nyquist to alias at ratio 2. hf/rms unchanged (0.0776 vs
    pre-fix 0.078). => decimation aliasing is NOT a meaningful contributor here.

- timestamp: 2026-06-26T(falsify-2)Z
  source: A/B render measurement, the ACTUAL ragtime (renderSong s "piano" 0.4s, 153s)
  observation: |
    new vs old whole-file difference = -61.7 dB rel to signal; every freq band
    A/B ~1.0. Ragtime RH is mostly octave-5 (744 notes) → small shifts (-5..+5 st)
    off the C5/C6 samples (many are DOWNSHIFTS, untouched by the fix); only a
    handful of octave-6/7 notes get large upshifts. The anti-aliasing fix is
    INAUDIBLE on the real material. This is the falsification condition I wrote in
    the reasoning_checkpoint: "grit is in the source sample, not introduced by
    resampling."

- timestamp: 2026-06-26T(metric-artifact)Z
  source: ORGAN (pure synthesis — NO resampling, aliasing IMPOSSIBLE) hf/rms vs pitch
  observation: |
    org_C4=0.0142  org_C6=0.1633  org_C7=0.5467  — the no-resample organ climbs
    hf/rms with pitch EVEN MORE STEEPLY than the sampled piano (C4=0.009, C6=0.025,
    C7=0.078). The mean|2nd-difference|/rms metric scales with frequency^2, so
    "HF roughness scales with pitch" is a METRIC ARTIFACT, not evidence of aliasing.
    The original root-cause confirmation rested on this confounded metric.

- timestamp: 2026-06-26T(pivot-controlled)Z
  source: controlled micro-experiments rendered via renderSong "piano" 0.4s + WAV scan
  observation: |
    Isolated single note (G5q) AND loud 4-note chords ([B4 D5 G5 B5]q, f [C4 E4 G4 C5]q):
    ZERO |1st-diff|>0.02 events; tail ends at -78 dBFS (clean). => simultaneous-onset
    summing (a) and tail-end truncation (b') BOTH FALSIFIED — a chord onset and its
    natural tail are smooth. Voice-pool sweep on the full ragtime (voicePool 8 / 32 /
    256) → BYTE-IDENTICAL output (780 events each, same peak, same grid histogram) =>
    voice-stealing FALSIFIED (per-sequence active count never exceeds the pool).
- timestamp: 2026-06-26T(root-cause-confirmed)Z
  source: waveform dump of isolated loud 16th (f D4s) at the envelope/tail boundary
  observation: |
    authoredFrames for a 16th @175bpm ≈ 3780 (t=0.0857s). Dump:
      frames 3760-3779: +0.0001 +0.0001 +0.0001 +0.0001 -0.0000 +0.0000   (envelope release floor ~0)
      frame  3780:      +0.0265  <-- STEP (+0.02654 in one sample)
      frames 3781+:     +0.0250 +0.0234 +0.0217 +0.0197 ...                (smooth tail decay)
    The ADSR release ramps the signal to ~0 at authoredFrames; the tail loop then
    restarts at level=1.0 × RAW sample → jump back to full amplitude. CONFIRMS the
    envelope/tail boundary discontinuity. Step size = raw sample amplitude at the
    authored-end frame → large for short notes, sub-threshold for long ones.
- timestamp: 2026-06-26T(onset-grid)Z
  source: discontinuity scan of ragtime_new.wav (1st-difference)
  observation: |
    >0.05: only 17 events in 153s, randomly spaced (no per-beat pattern), max 0.069.
    >0.02: 784 events; inter-event gaps cluster at 171ms (eighth), 343ms (quarter),
    686ms (half) @ 175 BPM. >0.01: first events evenly spaced every ~0.343s (the
    quarter-note beat). => there ARE small BEAT-ALIGNED transients (note onsets),
    amplitude up to 0.069 (~-23 dBFS) which can exceed the local signal RMS
    (~-31 dBFS) in sparse moments. This is the most likely true source of the
    perceived "per-beat static" — onset transients, NOT resampling aliasing.

## Eliminated

- Clipping/distortion: ruled out — rendered WAV peak ~24%, zero clipped samples.
- Note-boundary click on piano: ruled out — isolated piano note onset/offset
  jumps ≤43 (clean). (Sine's note-END cutoff click of 2990 is a SEPARATE synth
  declick issue, not this bug.)
- Recorded attack/hammer noise in samples: ruled out — piano attack HF == sustain HF.
- Release/sustain wash ("dreamy"): SEPARATE and already solved via release=0.4s;
  static persists independent of release length.
- Voice stealing: implausible — offline render mixes additively; a full piece
  render showed only 18 large discontinuities total (no per-beat cutoffs).
- **Decimation aliasing in VarispeedResample as the AUDIBLE cause: ELIMINATED**
  (2026-06-26). Three independent measurements: (1) the correct anti-aliased
  resampler changes the real ragtime by only -61.7 dB (inaudible); (2) the C6
  piano sample has ~100x less energy above 6 kHz than below, so ratio-2 upshift
  has almost nothing to alias; (3) the hf/rms "scales with pitch" evidence is a
  metric artifact — the organ (no resampling) climbs hf/rms FASTER than the piano.
  NOTE: the anti-aliased resampler is still a correct, unit-tested quality
  improvement for EXTREME upshifts; it just does not fix THIS bug.

## Resolution

- root_cause: CONFIRMED (2026-06-26) — envelope/tail boundary discontinuity in the
  SAMPLED render path (SampledInstrumentRenderer.Render + EnvelopeProcessor rescale),
  NOT varispeed aliasing (falsified) and NOT onset-summing / tail-truncation / voice-
  stealing (all falsified by controlled experiment). Two coupled defects:
    (1) baseRelease=0.05 made the ADSR release ramp every note to ~0 at authoredFrames,
        but the exponential release-tail loop restarts at level=1.0 on the RAW sample →
        a step from ~0 back to full amplitude.
    (2) For SHORT notes (rescale path in GenerateADSRCurve), the floor-rounding leftover
        was dumped into the release ramp even when release was 0 → a 1-3 frame dip at
        the authored end, re-creating the seam.
  The step size = raw sample amplitude at the authored-end frame → large for short notes
  (ragtime RH), aligned on the beat grid, summing into the audible per-beat "static".
- fix: TWO coordinated edits (candidate — NOT committed, awaiting ear A/B):
    * SampledInstrumentRenderer.cs — baseRelease 0.05 → 0.0 (the exponential tail IS the
      release for sustained notes; envelope now ends at sustain, meeting the tail
      continuously). + explanatory comments.
    * EnvelopeProcessor.cs — in the rescale branch, route the floor-rounding leftover to
      RELEASE only when a release was requested (release>0, synth/SFZ/drum paths,
      byte-identical); when release==0 (sampled path) the leftover stays in SUSTAIN so
      the envelope ends at the sustain level. + explanatory comments.
  Staccato/Marcato (sustain=0) intentionally end at 0 and keep their pre-existing seam
  (NOT in the ragtime; documented follow-up — would need the articulation-dependent-tail
  redesign that changes the SampledArticulationTailWindowTests contract).
  files_changed (uncommitted):
    - flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs
    - flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs
    - flow-lang.Tests/Integration/Debug2026/SampledEnvelopeTailContinuityTests.cs (new falsification test)
- verification (machine, pre-ear):
    * Falsification test RED→GREEN: pre-fix boundary jump 0.0676 (5.0x neighbour slew) →
      post-fix within slack. Test exercises the rescale path (0.1-beat note).
    * Full ragtime (renderSong s "piano" 0.4s) discontinuity scan: max |2nd-difference|
      0.0709 → 0.0112; |2nd-diff|>0.03 events 198 → 0; >0.05 events 33 → 0. The audible
      single-sample clicks are eliminated; remaining |1st-diff|>0.02 events are smooth
      loud-passage slopes (near-zero 2nd-diff = real music). Peak unchanged (-12.2 dBFS).
    * Two-run cmp-clean preserved: identical SHA256 across two renders.
    * Audio/baseline test groups: 335/340 pass. The 5 failures are ALL sampled-instrument
      baselines that legitimately shifted (piano warmth RMS, mix_synth_path_pan RMS,
      Phase41 showcase RMS, Phase45 intro/cut-time byte-pinned SHA — flute/brass/piano,
      all SampledInstrumentRenderer). They reflect the removed per-note release dip.
  EAR A/B: PASSED (2026-06-26). User confirmed the per-beat static is GONE and the piano
    still sounds right. Orchestrator independent measurement corroborated: hard clicks
    (>0.05) 955 → 0; max discontinuity 0.29 → 0.046; two-run cmp-clean preserved.
  FINALIZED (2026-06-26):
    * Regenerated all 5 legitimately-shifted baselines deterministically (seeded dither):
      Phase37 piano_warmth_smoke RMS, Phase37 mix_synth_path_pan RMS, Phase41 showcase RMS,
      Phase45 intro + cut-time byte-pinned SHA. SPEC-8 ±0.5 dB/100 ms tolerance and the
      two-run cmp-clean contract were NOT loosened — the bytes shift for a correct reason
      (no per-note release dip). All 5 confirmed GREEN against their real assert branches.
    * Full `dotnet test`: flow-lang.Tests 2732 passed / 0 failed / 19 skipped (incl. all 5
      regenerated baselines + the new SampledEnvelopeTailContinuityTests). flow-midi.Tests
      19 passed / 2 failed — the 2 failures (FlowGeneratorStructureTests.One_Sequence_Per_
      Track_Channel_No_RH_LH_Suffix + QuantizerRoundingTests.Two_Octave_Range_Does_Not_
      Split_RH_LH) are CONFIRMED PRE-EXISTING: they fail identically on the stashed/clean
      tree, live in flow-midi (the MIDI→Flow converter, which does NOT reference flow-lang's
      audio path), and are the documented RH/LH-split polyphony follow-up — NOT caused by
      this fix.
    * Commit: 487f248 on dev (2 source files + new test + 5 regenerated baselines; unrelated
      42-AUDIT-data / 48-BUNDLE-SIZE.md / .wrangler churn deliberately NOT staged).
  OUT-OF-SCOPE FOLLOW-UP (do NOT do now): the Staccato/Marcato sustain=0 tail seam is a
    SEPARATE articulation-dependent-tail decision. Those articulations intentionally end at
    0 and keep their pre-existing boundary seam; closing it cleanly would need the
    articulation-dependent-tail redesign that changes the SampledArticulationTailWindowTests
    contract. Not present in the ragtime; tracked as a distinct future item.

- (superseded) prior leading hypothesis: beat-aligned note-ONSET transients summing.
- fix: AA resampler REVERTED 2026-06-26 (user decision Q1). It was a correct,
  deterministic, unit-tested (5/5) quality improvement for extreme upshifts but
  did NOT fix this bug (-61.7 dB / inaudible on the ragtime), so it is SHELVED, NOT
  SHIPPED. Working tree is clean of the AA work; blast radius zero. The kernel
  (Blackman windowed-sinc, lobes=16, cutoff=1/ratio, Option-B sampled-callers-only
  scoping) is documented in the knowledge base and revivable if extreme-upshift
  sample quality ever becomes a goal.
- verification: AA fix verified CORRECT in isolation (5/5 unit tests) but verified
  INEFFECTIVE on the real bug (-61.7 dB / inaudible on the ragtime). REVERTED:
  `git checkout` of FileIO.cs / SampleCache.cs / Sfz/SfzSampleCache.cs + deletion
  of VarispeedAntiAliasFacts.cs; `dotnet build flow-lang` green; falsification
  recorded in knowledge-base.md.

## Notes
- The user's chosen piano crispness is release=0.4s. Verify the static fix on
  THAT render (`renderSong s "piano" 0.4s`).
- HUMAN-VERIFY by ear is the acceptance gate (mirror the chord-dynamic fix flow):
  return to the orchestrator at the checkpoint so it can present the rendered
  before/after to the user.
- Out of scope: sine note-end declick; the already-committed timing fix; the
  already-committed --no-dynamics flag.
</content>
