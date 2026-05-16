---
status: partial
phase: 34-symphony-showcase-v1-4-closer-pre-public-public-pivot
source: [34-VERIFICATION.md, 34-CONTEXT.md D-801..D-803, 34-01-PLAN.md Task 3]
started: 2026-05-16T17:55:50Z
updated: 2026-05-16T18:20:00Z
iterations:
  - id: 1
    composer_feedback: "mixing issues; melody clobbered in places; flutes should shine more than the super loud bass"
    response: "boosted flute 0.85→1.0; dropped cello 0.75→0.45 + horn 0.65→0.40 (sustained bass bed was masking the lead); dropped violin doubling 1.0→0.85 to give the flute clearer headroom in A'; trimmed master reverb wet 0.30→0.20 so the 2.5s tail stops smearing whole-note pads into the next bar's melodic onset. Determinism preserved (D-702 holds on iteration #2 render)."
---

## Current Test

[awaiting human (composer) listening + sign-off]

The plan 34-01 executor has produced the initial canonical render of
`examples/symphony/symphony.flow` -- a ~62.4s ABA single-movement piece
for 5 VSCO Community CE 1.1.0 SFZ instruments (violin, cello, flute, horn,
timpani) -- and verified the technical gates:

- Render exits 0 on two consecutive invocations
- `cmp` confirms byte-identical determinism (D-702 contract preserved
  end-to-end through the real VSCO-CE library)
- Duration 62.4s is in the D-101 [45, 75]s window
- All 5 D-301 articulation tokens (`>` / `stacc` / `ten` / `leg` / `marc`)
  fire on at least one note; `{voice ...}{voice ...}` polyphony block
  fires in section A'; `{3:2 D5 E5 F5}q` tuplet fires once on the flute
  ornament in section B; `(transpose ... 12)` lifts the violin theme an
  octave up in A'

What remains is the **composer subjective UAT** -- the three D-802
conditions below MUST resolve to `result: pass` before plan 34-01 closes
and Phase 34 advances to plan 34-02 (which commits the post-UAT canonical
source per D-902).

## How to listen

Render outputs are at `examples/output/symphony.wav` (already produced by
the executor) and `examples/output/symphony.mid`. To re-render:

```bash
flow render examples/symphony/symphony.flow -o ignored.wav
# (the -o flag is ignored at Phase 30 -- the .flow source's writeWav
#  call is the real output path, written relative to cwd)
```

Or via the development dotnet path (if `flow` is not on $PATH yet):

```bash
dotnet run --project flow-cli -c Release -- render examples/symphony/symphony.flow -o ignored.wav
```

Then play with either of:

```bash
aplay examples/output/symphony.wav             # ALSA
flow play examples/symphony/symphony.flow      # PulseAudio via the Phase 30 CLI
```

## Tests

### 1. Composer "would publicly share this" sign-off (D-802 condition 1)

expected: composer listens to the rendered symphony end-to-end (~62s)
  and signs off with the verbatim ROADMAP success-criterion phrasing
  -- "postable on GitHub quality" or "I would publicly share this".

setup:
1. Render via `flow render examples/symphony/symphony.flow -o ignored.wav`
   (or the dotnet variant above).
2. Play via `aplay examples/output/symphony.wav`.
3. Listen end-to-end at least once. Then optionally re-listen with the
   `examples/symphony/symphony.flow` source open in an editor to map
   audible moments to source lines.

expected_outcome: composer affirms in plain English. If unsatisfied,
  describe the issue in plain English ("violin too loud in A'", "B
  section transition feels abrupt", "timpani lands a beat too early",
  etc.) and the executor (or planner) adjusts `symphony.flow` and
  re-renders. The iteration loop has no arbitrary cap per D-801; the
  plan does not close until this row resolves `pass`.

why_human: subjective quality judgement -- no automated proxy possible.
  This is the load-bearing D-802 condition 1 sign-off.

result: pending -- awaiting composer playback

### 2. Audible articulation differentiation (D-802 condition 2)

expected: composer A/B-listens the canonical mix against an
  all-articulations-stripped variant; canonical is audibly more
  expressive (the `>` accent reads louder, the `stacc` reads
  shorter, the `ten` reads slightly held, the `leg` reads bound,
  the `marc` reads punchy).

setup:
1. The canonical render at `examples/output/symphony.wav` is already
   in place (Task 2 produced it).
2. Create the stripped variant:
   ```bash
   sed -E -e 's/( |\t)(stacc|ten|leg|marc)( |\||$)/\1\3/g' \
          -e 's/([A-G][#b]?[0-9](w|h|q|e|s|t)(\.)?)>/\1/g' \
          examples/symphony/symphony.flow \
          > /tmp/symphony_no_articulation.flow
   # Also redirect the writeWav target so it doesn't clobber the canonical:
   sed -i 's|examples/output/symphony.wav|/tmp/symphony_no_articulation.wav|g' \
       /tmp/symphony_no_articulation.flow
   sed -i 's|examples/output/symphony.mid|/tmp/symphony_no_articulation.mid|g' \
       /tmp/symphony_no_articulation.flow
   ```
3. Render the stripped variant:
   ```bash
   dotnet run --project flow-cli -c Release -- render \
       /tmp/symphony_no_articulation.flow -o ignored.wav
   ```
4. A/B-listen:
   ```bash
   aplay examples/output/symphony.wav            # canonical
   aplay /tmp/symphony_no_articulation.wav       # stripped
   ```

expected_outcome: composer can hear that the canonical mix is audibly
  more expressive -- staccato shorter than legato, accent louder than
  unmarked, marcato hits punchier, tenuto slightly bound. If the
  difference is not audible, the executor investigates whether
  individual articulation envelope shaping is being eclipsed by the
  master reverb (D-402 wet 0.3, decay 2.5s) -- common UAT adjustment
  is to lower wet to 0.2 / 0.15 or shorten decay to 1.5s.

why_human: perceptual judgement -- automated RMS / spectral checks do
  not capture "is it audibly more expressive".

result: pending -- awaiting composer A/B playback

### 3. Audible polyphony (D-802 condition 3)

expected: composer picks out the simultaneous voices in the A' section
  `{voice ...}{voice ...}` cello voicing block. The bars containing
  `| {voice D3w} {voice A3h F3h} | ...` should produce intelligible
  parallel lines (held bass + inner harmony motion), not a single
  muddied chord, audible under the transposed violin theme.

setup:
1. Identify the A' section in the source -- it is the third (A')
   instance in `Song piece = [themeA*2 transitionAB themeB*2
   transitionBAPrime themeAPrime*2]`. Roughly the last 12s of the
   62.4s render.
2. `aplay examples/output/symphony.wav` and focus on the last ~12s
   of the playback.
3. Optional: bandpass-filter to the cello range and listen again to
   isolate the polyphony cleanly:
   ```bash
   sox examples/output/symphony.wav /tmp/cello_band.wav bandpass 200 2.0
   aplay /tmp/cello_band.wav
   ```

expected_outcome: composer can hear the cello inner voices as distinct
  lines, not a single chord, under the violin lead. If the polyphony
  reads muddied, common UAT adjustments per RESEARCH Pitfall 3 are
  to lower the master reverb wet from 0.3 to 0.2, lower master decay
  from 2.5s to 1.5s, or boost the cello balance from 0.75 to 0.85.

why_human: perceptual judgement -- needs trained ears to distinguish
  parallel voice motion from a held chord.

result: pending -- awaiting composer A' playback

## How to sign off

When all three rows above resolve `pass` in your subjective ear:

1. Flip frontmatter `status: partial` → `status: closed`.
2. Update `updated:` timestamp to ISO-8601 now.
3. Update each of the 3 test rows from `result: pending -- ...` to
   `result: pass -- <one-sentence composer affirmation in plain English>`.
4. Update the Summary block below (passed: 3, pending: 0).
5. Reply to the orchestrator with `approved` to advance to plan 34-02.

If you choose a different working title than `In Five Voices`, rename
the file's top comment header in `examples/symphony/symphony.flow` to
match. The filename `symphony.flow` stays per D-501.

Optional cleanup once signed off:
- `rm /tmp/symphony_no_articulation.{flow,wav,mid}` -- the D-802
  condition 2 A/B fixture is a one-shot UAT artifact per Deferred
  Ideas (a permanent stripped example is a v1.5 docs-polish slot).

## Summary

total: 3
passed: 0
issues: 0
pending: 3
skipped: 0
blocked: 0

## Gaps

- The render emits non-fatal stderr advisories about a few unrecognized
  SFZ opcodes (`ampeg_dynamic`, `tune`, `seq_length`, `seq_position`,
  `group_label`) and two unused sub-sample paths (`LDFlute_susvib_C3`,
  `Timpani5_Hit_v4_rr2`). These are the documented Phase 33 common-subset
  parser behavior and do not affect the audible output for the symphony
  (the unused samples are pitch ranges the symphony does not use). The
  advisories are stable across the two-run determinism check.
- The composer-UAT A/B variant generation in test 2 above relies on a
  `sed`-based articulation stripper. If a future symphony.flow iteration
  introduces additional articulation tokens (`cresc`, `decresc`, future
  Phase 28+ surface), update the sed pattern.

## Resume signal

Composer replies `approved` after flipping `status: closed` and all 3
test rows to `result: pass -- ...`. The orchestrator then advances to
plan 34-02 per D-902 (commits the canonical post-UAT symphony.flow as
the clean-history ship).
