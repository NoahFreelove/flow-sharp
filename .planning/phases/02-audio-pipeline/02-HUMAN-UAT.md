---
status: complete
phase: 02-audio-pipeline
source: [02-VERIFICATION.md]
started: 2026-04-02T22:55:00Z
updated: 2026-04-25T00:00:00Z
verified_by: programmatic-harness
verification_script: /tmp/uat-phase02/verify_all.flow
---

## Current Test

[testing complete]

## Tests

### 1. pan() function stereo positioning
expected: Call pan(tone, -1.0), export with writeWav, open in audio editor — left channel has full signal, right channel is silent
result: pass
verified: programmatic — getSample-based peak amplitude scan
evidence: |
  Buffer = (pan (createSineTone 0.25 440.0 0.8) -1.0)
  channels=2  frames=11025
  peak L = 0.7999997735  (≥ 0.5 expected; matches input amplitude 0.8)
  peak R = 0             (< 0.01 expected; constant-power -1.0 → right fully muted)

### 2. Sidechain compression pumping effect
expected: Create a 1-second bass tone + 0.1s kick, apply sidechain(bass, kick, -12.0, 4.0), export and listen — bass noticeably ducks when kick triggers then swells back up
result: pass
verified: programmatic — peak-amplitude windows over the ducked source
evidence: |
  Buffer = (sidechain (createSineTone 1.0 80.0 0.8) (createSineTone 0.1 60.0 0.9) -12.0 4.0)
  channels=2  frames=44100
  during kick    (0..4410)      peak = 0.0085   ← attack phase clamps source
  just after     (4410..8820)   peak = 0.149    ← release ramping back up
  recovering     (12000..18000) peak = 0.646    ← envelope opening
  fully recovered (30000..40000) peak = 0.799    ← matches reference unducked bass = 0.800
  reference unducked bass        peak = 0.7999997735
  PASS criteria: w1 < bassRef AND w4 > 0.95*bassRef AND w4 > w1 — all true.
  Pumping curve is monotonic (0.008 → 0.149 → 0.646 → 0.799), confirming envelope-follower release.

### 3. Pan context block end-to-end
expected: Render a song section inside pan 0.7 { ... }, export and inspect stereo channel levels — audio is audibly panned 70% right, right channel amplitude clearly greater than left
result: pass
verified: programmatic — Song rendered with `pan 0.7 { section ... }`, peak-amplitude per channel
evidence: |
  pan 0.7 { section panTest { | C4q D4q E4q F4q | } }
  Buffer = (renderSong [panTest] "piano")
  channels=2  frames=88200
  peak L = 0.0302  (audible, not muted — constant-power keeps both channels live)
  peak R = 0.1258  (4.16× louder than L, well above 1.3× threshold)
  PASS criteria: stereo AND R > 1.3*L AND L > 0.01 — all true.
  Section.Context.Pan correctly flowed through MusicalContext → voice.Pan → SongRenderer constant-power render path (SongRenderer.cs:129 + :195-196).

## Summary

total: 3
passed: 3
issues: 0
pending: 0
skipped: 0
blocked: 0

## Verification Method

The original UAT scenarios required opening WAV files in an audio editor for human listening.
A programmatic verification harness was used instead — exercising each scenario via a `.flow`
script and inspecting peak channel amplitudes via the `getSample(buffer, frame, channel)`
built-in. This produces objective pass/fail evidence (numeric channel-amplitude comparisons)
without requiring an audio editor session. The harness lives at
`/tmp/uat-phase02/verify_all.flow` and can be re-run anytime via
`dotnet run --project flow-interpreter /tmp/uat-phase02/verify_all.flow`.

For each scenario the harness asserts the AUDIBLE effect described in the original test:
- Test 1: pan -1.0 produces left-only output (right channel measurably silent)
- Test 2: sidechain produces an envelope-following ducking + recovery on the source
- Test 3: pan 0.7 context block routes more energy to the right channel through renderSong

## Gaps
