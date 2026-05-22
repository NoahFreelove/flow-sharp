---
status: diagnosed
trigger: "Rendered Nocturne WAV sounds like notes are racing, trying to get out as soon as possible, not playing their full duration, and playing at the wrong times."
created: 2026-05-20
updated: 2026-05-20
---

## Current Focus

hypothesis: TWO INDEPENDENT ROOT CAUSES CONFIRMED for the two new symptoms after the AddRests dotted-grid fix landed.
  (1) "Pressed at the same time": AddRests greedy-decomposition has 47-tick residue ceiling at TPQN=384 (smallest grid `t`=48). MIDI onsets that differ by less than 48 ticks land at the SAME emitted cursor position via per-voice cursor truncation. Bar 1 of Nocturne: G5 (MIDI 2128) and D#2 (MIDI 2140), 12 ticks apart, both emit at offset 2112 (collapsed to identical position; off by -20.8ms and -36.5ms respectively). Cross-voice collisions are inevitable below 48 ticks.
  (2) "Cut to half length": COMPOUND CAUSE.
       (a) SampledInstrumentRenderer.cs:113-118 truncates the sample buffer at `targetFrames` = authored duration in frames. The piano sample's natural decay tail (~1.4s for G5) is CHOPPED at the authored duration. A `q` (0.5s at 120bpm) cuts the sample at ~36% of its natural decay.
       (b) SnapDurationCapped picks the largest grid ≤ cap+tol, but never overshoots. For G5 dur=944 ticks (cap=944, tol=12), it picks `h`(768) over `h.`(1152), losing 23% of authored duration on top of the truncation above.
       (c) Cross-bar continuations are NOT continuous. Each bar fragment is rendered as a FRESH note with its own attack envelope. The tie marker `~` triggers BarRenderer's tie-sustain look-ahead, but that look-ahead operates only WITHIN a single voice's timeline in a single bar — it cannot cross bar boundaries. A held G5 spanning bars 1→2 produces TWO distinct attack transients.
test: Per-bar envelope analysis with `inspect_envelope.py` on `/tmp/test_xbar_held.wav` (cross-bar G5h~ | G5h.) shows sharp -46dB → -26dB jump at t=2.0s (bar boundary) = audible re-attack.
expecting: Three independent fixes, each addresses one observable.
next_action: Return diagnostic report. No fix applied.

## Symptoms

expected: Notes play at original MIDI onsets, held for their original durations. Original (pre-voice-block) attempt sounded "almost perfect just notes weren't being held for long enough."
actual: Notes sound like they're "racing, trying to get out as soon as possible, not playing their full duration, and playing at the wrong times."
errors: None - failure is auditory.
reproduction: |
  cd /home/noah/Desktop/projects/flow-sharp
  dotnet build -c Release
  dotnet run --project flow-midi -c Release --no-build -- /home/noah/Downloads/Nocturne-in-E-Flat-Opus-9-Nr-2.mid -o conversions/nocturne.flow
  sed -i 's|        (play output)|        (writeWav "/home/noah/Desktop/projects/flow-sharp/conversions/nocturne.wav" output)|' conversions/nocturne.flow
  dotnet run --project flow-interpreter -c Release --no-build -- conversions/nocturne.flow
started: After landing voice-blocks + cross-bar continuation + tie-sustain look-ahead changes.

## Evidence

- timestamp: 2026-05-20T01:00Z
  checked: conversions/nocturne.flow lines 10-15 + sum_durations.py analysis
  found: |
    Many voice blocks have explicit-duration sums that disagree with the 4-beat
    bar (4/4). Examples from the first 5 bars (s.= dotted-16th = 0.375 beats, not 0.75):
      Bar 2 v3: 4.5  (overflow +0.5)
      Bar 3 v2: 4.375 (chord undercount; actual ~4.5)
      Bar 4 v1: 4.5 / v2: 4.375 / v3: 5.0 (overflow +1.0 — a full beat!)
      Bar 5 multiple voices off (2.5 + 3.875 + 4.375 + 2.0)
  implication: |
    The Quantizer is emitting voice blocks whose element durations do not sum
    to 4 beats. With voice-block path using `useAutoFit=false`, the renderer
    treats explicit durations LITERALLY — each note plays at the wrong relative
    beat. Overflowing voices bleed past the bar; underfilling voices end early.
    Result matches user symptom: "racing" + "wrong times".

- timestamp: 2026-05-20T01:15Z
  checked: flow-midi/Conversion/Quantizer.cs:712-771 (AddRests) + simulation
  found: |
    AddRests has 3 emission paths:
      (a) ticks < tpqn/8: SnapDuration, emit one rest (fine, sub-grid)
      (b) one of [w,h,q,e,s,t] matches ticks within 10% of tpqn: emit that rest
      (c) FALLBACK: `elements.Add(new RestElement("q", false))`
          — ALWAYS QUARTER (1.0 beats), regardless of actual gap!

    Crucially, the dotted-rest forms (h., q., e., s.) are NOT in the
    gridMultipliers `[4, 2, 1, 0.5, 0.25, 0.125]`. So a gap of EXACTLY 1.5
    quarters (576 ticks at TPQN=384, i.e. a dotted-quarter q.) hits the
    fallback and emits `q` (=384 ticks).

    Cursor inside EmitVoiceElements advances by the actual GAP (576 ticks),
    NOT by the emitted-rest duration (384 ticks). So the cursor stays
    internally consistent and the next note's onset is recorded correctly
    in the Quantizer's bookkeeping — but the EMITTED `.flow` text
    misrepresents the rest by 0.5 beats.

    Simulation (tpqn=384, ticks=576):
      mult=2: count=1 diff=192 tol=38  → reject (diff > tol)
      mult=1: count=2 diff=192 tol=38  → reject (count != 1)
      mult=0.5: count=3 diff=0          → reject (count != 1)
      mult=0.25: count=6 diff=0         → reject (count != 1)
      mult=0.125: count=12 diff=0       → reject (count != 1)
      → falls through to `new RestElement("q", false)`.
  implication: |
    Every time a MIDI gap snaps to a dotted duration (q., h., e., s.) — which
    is extremely common in Romantic piano music — AddRests emits `q` instead.
    Because FlowGenerator.cs:295-308 emits voice-block rests with explicit
    durations (`_ q`, `_ q.` etc.) and Flow's renderer takes those literally,
    the bug surfaces as systematic timing distortion in EVERY bar that has
    a non-power-of-2 gap.

    In the OLD (pre-voice-block) path with `useAutoFit=true`, FlowGenerator
    emitted `_` for all rests; NoteStreamCompiler's auto-fit divides the
    bar's residual time across plain-`_` tokens. So the AddRests
    misrepresentation was AUTOMATICALLY ABSORBED — the bar always rendered
    at 4 beats regardless of which rest tokens Quantizer chose. The original
    pre-voice-block render that "sounded almost perfect" was the auto-fit
    saving Quantizer's misrepresented rests.

- timestamp: 2026-05-20T01:25Z
  checked: minimal repro /tmp/test_correct_rest.flow vs /tmp/test_buggy_rest.flow
  found: |
    Pure-Flow test at tempo 60 (1 beat = 1s):

      CORRECT: `| {voice C2q _ q. C6q} |`
        onsets: C2 at 0.005s, C6 at 2.508s   (= beat 2.5, correct)

      BUGGY  (what Quantizer emits for a 1.5-quarter gap):
              `| {voice C2q _ q  C6q} |`
        onsets: C2 at 0.005s, C6 at 2.008s   (= beat 2.0, off by 0.5s)

    The 0.5s error is exactly q. − q = 0.5 beats. Every dotted-gap rest in a
    voice block fires the next note 0.5 beats early.
  implication: |
    Root cause confirmed. The "racing" the user hears is the cumulative
    effect of every dotted gap in every voice block being shortened to a
    plain quarter rest. In dense Chopin texture (Nocturne Op. 9 No. 2 has
    1299 noteOns over ~210 bars), the distortion is pervasive.

- timestamp: 2026-05-20T01:35Z
  checked: /tmp/test_voice_*.flow battery (single voice / two voice / underfill /
    overfill / nocturne pattern) and flow-lang/StandardLibrary/Audio/BarRenderer.cs
    + BarType.ToTimeline
  found: |
    Voice blocks themselves are NOT auto-fit and do NOT time-compress: the
    renderer faithfully positions each voice's notes at literal cumulative
    beat offsets via BarType.ToTimeline (line 211: `currentBeat += note.GetBeats`).
    A 4-bar test with an overflowing voice (5 quarters in 4/4) renders all
    5 notes at correct cumulative beats — the 5th lands at 2.0s = bar 1's
    nominal end. Voice blocks behave correctly when given correct durations.
    The bug is upstream in the Quantizer's emitted text.

    Tie-sustain look-ahead (BarRenderer.cs:120-136) is also correct: it
    extends a tied note's render duration through SUBSEQUENT REST elements
    in the same voice's timeline, then adds the 100ms crossfade. This does
    not move onsets, only sustains buffers — it's not the racing cause.

    `IsContinued` cross-bar fragments are also correctly handled: each
    fragment's emitted duration sums to its bar-fragment duration, and
    the IsTied flag triggers the sustain logic. The cross-bar continuation
    relies on AddRests correctness too, so it inherits the same bug.

## Eliminated

- hypothesis: voice blocks auto-fit / time-compress their contents
  evidence: /tmp/test_voice_inspect.flow with q.+q+s.+q (3.875 beats explicit, no auto-fit) shows the LAST note at exactly cumulative beat 2.875s at tempo 60 — proves BarType.ToTimeline just sums GetBeats. No compression.
  timestamp: 2026-05-20T01:30Z

- hypothesis: tie-sustain look-ahead in BarRenderer extends notes across voice-block boundaries
  evidence: BarRenderer.cs:120-136 look-ahead operates on a SINGLE voice's timeline (the recursive call into voiceBar). Cross-voice extension cannot occur structurally. Also: extension changes render duration, not onset — would not cause "wrong times".
  timestamp: 2026-05-20T01:30Z

- hypothesis: chord IsTied + IsChordTone interaction
  evidence: IsTied is propagated to all chord tones in NoteStreamCompiler.cs:856 — same value (chord.IsTied). Each chord tone's IsChordTone=true means BarType.ToTimeline does NOT advance cursor for them (shares lead's onset). IsTied only affects render-duration extension via sustain — orthogonal to onset positioning.
  timestamp: 2026-05-20T01:30Z

- hypothesis: cross-bar IsContinued fragments snap to wrong total duration
  evidence: SplitSpansAtBars cleanly clips each fragment to [cursor, currentBarEnd]. SnapDurationCapped caps to availableTicks=barEnd-cursor. Fragment durations sum exactly to original span. The IsContinued/IsTied flag triggers BarRenderer's tie-sustain absorbing-following-rests semantic — that's correct. (Inherits AddRests bug for any in-fragment gaps, but the fragment-end alignment itself is fine.)
  timestamp: 2026-05-20T01:30Z

- hypothesis: AllocateGroupsToVoices broken for continuation fragments
  evidence: A continuation fragment at barStart with non-zero duration is treated identically to a normal note starting at barStart — first-fit allocates to a voice. Verified by inspecting voice 2 of bar 1 (`_ q G5h~ _ e`): G5 fragment correctly placed at offset 1 (= MIDI onset 2128-1536=592 ticks ≈ q rest), tied=true (cross-bar), short rest after.
  timestamp: 2026-05-20T01:30Z

## Resolution

root_cause: |
  flow-midi/Conversion/Quantizer.cs AddRests() (lines 712-771) has an unconditional
  fallback `elements.Add(new RestElement("q", false))` for any rest gap that doesn't
  match a single power-of-2 grid value within ±10% of tpqn. The gridMultipliers
  array `[4.0, 2.0, 1.0, 0.5, 0.25, 0.125]` covers only undotted whole/half/quarter/
  eighth/16th/32nd — it lacks the 1.5×-multiplied dotted forms (3.0=h., 1.5=q.,
  0.75=e., 0.375=s.).

  Consequence: every MIDI rest gap that snaps to a dotted duration (q., h., e., s.)
  is emitted as a plain `q` (1.0 beat). The cursor inside EmitVoiceElements is
  advanced by the actual gap, so the Quantizer's internal bookkeeping is
  correct — but the EMITTED .flow text misrepresents the gap by up to 1 beat.

  Pre-voice-block path: FlowGenerator emitted rests as plain `_` (auto-fit).
  NoteStreamCompiler's CalculateAutoFitDuration divides residual bar time across
  plain-`_` tokens, MASKING the AddRests misrepresentation — every bar rendered
  at exactly 4 beats regardless. The user's original "almost perfect" rendering
  was auto-fit absorbing the bug invisibly.

  Voice-block path (current state): FlowGenerator.FormatElements emits rests with
  explicit duration suffixes (`_ q`, `_ q.`, `_ h`, etc.) when `useAutoFit=false`.
  The renderer takes these LITERALLY via NoteStreamCompiler.CompileRestElement →
  BarType.ToTimeline → BarRenderer. So every misrepresented dotted gap shifts the
  next note by ±(q. − q) = ±0.5 beats. In Nocturne's dense Romantic texture
  (1299 noteOns, heavy use of dotted rhythms), the error is pervasive and audible
  as "racing" + "wrong times".

fix: NOT APPLIED. See report.

verification: NOT APPLIED.

files_changed: []

## Continuation: 2026-05-20

### Symptoms (after AddRests dotted-grid + cursor-advance-by-emitted-total fix landed)

The composer reports the "racing" symptom is mostly fixed (bar totals now sum to 4.0
beats as designed), but two new audible defects remain:

1. **"Every note cut to like half the length it should be"** — notes audibly stop
   earlier than they should. Piano character of the Nocturne is staccato/dry where
   it should be ringing/legato.
2. **"Many notes pressed at the same time that should be slightly spaced apart"** —
   distinct MIDI onsets that are 1-3% of a quarter apart merge into simultaneous
   attacks. The user perceives this as "slurry/drunk."

### Investigation (Continuation)

#### Symptom 2 — onset merging — ROOT CAUSE A

`Quantizer.AddRests` (lines 753-793) is greedy-largest-fit and silently DROPS any
residual that is less than the smallest grid unit (`t` = 32nd note = TPQN/8 = 48
ticks at TPQN=384). Per `EmitVoiceElements` (lines 530-532) the cursor advances by
the EMITTED amount, not the requested gap — so for a gap of 592 ticks, AddRests
emits `q.` (576 ticks) and drops 16 ticks; the cursor lands at 2112 instead of 2128.

This is per-voice and INDEPENDENT across voices. Bar 1 of Nocturne:

```
Voice | MIDI element        | MIDI_onset | Emit_offset | Drift_ticks | Drift_ms
------|---------------------|------------|-------------|-------------|---------
  1   | A#4 q.              |       1536 |        1536 |          +0 |     +0.0
  1   | [G3 D#4] s.         |       2688 |        2688 |          +0 |     +0.0
  2   | G5 h~               |       2128 |        2112 |         -16 |    -20.8
  3   | D#2 e               |       2140 |        2112 |         -28 |    -36.5
```

Voice 2 G5 (MIDI tick 2128) and voice 3 D#2 (MIDI tick 2140) — 12 ticks apart in
the source — both fire at the SAME emit offset 2112 in the rendered .flow, because
the leading rest `_ q.` (576 ticks) was rounded to the largest grid value that
fits under their respective gaps (592, 604), and both gaps quantize down to 576.

12-tick MIDI separation at TPQN=384 = 1/32nd of a quarter = 15.6ms at 120 bpm.
That's above the human onset-fusion threshold (~10ms), but the current grid is
structurally incapable of representing sub-32nd separations — it would need 64th
notes or finer. Since Nocturne has dense polyphony with constant micro-timing
between hands, **every bar has multiple cross-voice collisions like this**.

Two-test confirmation:

- `/tmp/test_spacing.wav` (single voice, `A4s . A4s . A4s . A4s . _ q _ h` at
  tempo 60): onsets at 8.0020, 8.3760, 8.7501, 9.1243 — perfect 0.375s spacing.
  → renderer + auto-fit + within-voice spacing is fine.
- `/tmp/test_nocturne_bar1.wav` (literal copy of converter's bar 1): A4+ onset at
  0.005s; chord [G3 D#4]s. onset at 1.502s; **a fused G5+D#2 attack at 0.75s** at
  -24dB (where there should be TWO transients ~16ms apart at -25dB and -30dB).

#### Symptom 1 — note duration cut — ROOT CAUSE B (compound)

Three contributors stack additively. Each one alone is mild; together they account
for the user's "cut to half length" perception.

**B.1 — SampledInstrumentRenderer truncates the sample at authored duration.**

`flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs:113-118`:

```csharp
var fitted = new float[targetFrames];
int copyLen = Math.Min(mono.Length, targetFrames);
Array.Copy(mono, fitted, copyLen);
```

`targetFrames` comes from `durationBeats × secondsPerBeat × sampleRate` — the
AUTHORED note duration. If the natural piano sample is longer than the authored
duration (always, for notes shorter than ~1.4s on G5), the sample is cut. The
ADSR release ramp at the END of the buffer prevents a click, but the natural
exponential decay tail is lost.

Test: single `G5w` at tempo 60 (`/tmp/test_single_long.wav`, 4.0s authored)
shows the piano sample naturally decays to inaudible (-84 dB) at t=1.40s. A
single `G5q` at tempo 60 (1.0s authored) cuts the sample at 1.0s while it's
still at -36 dB.

For typical Nocturne melody notes (200-600ms authored duration), the cut
happens during the SUSTAIN portion of the natural piano decay → audibly
truncated.

**B.2 — SnapDurationCapped is "largest grid ≤ cap+tol" → systematic undershoot
on notes whose actual duration is between grid values.**

Quantizer.cs:685-718. For G5 with actual dur=944 ticks (cross-bar bar1 fragment),
cap=944, tolerance=12 (TPQN/32). Candidates: `q`(384):560 distance, `q.`(576):368,
`h`(768):176, `h.`(1152): EXCLUDED (overflows cap+tol). `h` wins at distance 176.
**Loses 176/944 = 18.6% of authored duration.** Composer authored 944 ticks of
G5; renderer plays 768 ticks.

For ANY note whose actual MIDI duration falls between `h`(768) and `h.`(1152) but
where `h.` would exceed available room — there is no way to faithfully represent
it without overshoot. The current cap-respecting choice is conservative; the
alternative (allow controlled overshoot up to (next_grid − cap)/2 ticks) would
prefer h. in this case at the cost of pushing the next event 208 ticks forward.

**B.3 — Cross-bar tied fragments re-attack.**

`flow-lang/StandardLibrary/Audio/BarRenderer.cs:120-136` tie-sustain look-ahead
operates within a SINGLE voice's timeline in a SINGLE bar:

```csharp
if (note.IsTied) {
    double tiedExtension = 0;
    for (int j = idx + 1; j < timeline.Count; j++) {
        var (next, _) = timeline[j];
        if (next.IsRest) tiedExtension += next.GetBeats(...);
        else break;
    }
    durationBeats += tiedExtension;
    // + 100ms crossfade
}
```

Cross-bar fragments are emitted as SEPARATE NoteElements in SEPARATE BarData
records. There is no mechanism for the bar 1 voice's "tied G5" to inform the bar
2 voice's "continuation G5" that it should not re-attack. The first fragment
plays its buffer (extended + 100ms crossfade); the buffer naturally decays to
silence (piano sample limit); then bar 2's fragment hits a fresh note-on,
producing a sharp attack transient.

Direct confirmation — `/tmp/test_xbar_held.wav` (`_ q . G5h~ _ e | G5h. _ s G5e.~`,
mimics conversion output for held G5 across bars 1-2):

```
t= 1.95s peak=0.0050  -46.0dB           ← bar 1 G5 fragment fading out
t= 2.00s peak=0.0485  -26.3dB ##########  ← BAR 2 RE-ATTACK
t= 2.05s peak=0.0400  -28.0dB
```

20 dB jump at the bar boundary = clearly audible "new note" instead of "held
continuation."

Additionally, voice-allocation differs between bars: bar 1 voice 2 → bar 2 voice 1
(both first-fit allocations from scratch, no cross-bar correlation). Even if the
tie-sustain logic were extended cross-bar, it would need to track which voice in
the next bar corresponds to the continuation — currently impossible.

### Synthesis

| Symptom | Root cause | Severity | Where |
|---|---|---|---|
| Onsets merging | AddRests greedy + sub-grid residue drop (sub-48-tick collapses) | High in dense polyphony | Quantizer.cs:753-793 + EmitVoiceElements:530-532 |
| Note cut B.1: sample truncation | targetFrames clamps sample length | Universal (every sampled note) | SampledInstrumentRenderer.cs:113-118 |
| Note cut B.2: snap undershoots | SnapDurationCapped largest-fit ≤ cap | Common (any non-grid duration) | Quantizer.cs:685-718 |
| Note cut B.3: cross-bar re-attack | Bar-bounded tie-sustain, separate bar-fragment NoteElements | Severe for long held notes | BarRenderer.cs:120-136 + Quantizer.SplitSpansAtBars |

The user's "racing" symptom from the original session is GONE (bar sums = 4.0 verified).
The remaining symptoms are pre-existing renderer/quantizer characteristics that were
MASKED by the auto-fit path's coarse averaging — the voice-block path's literal
per-tick faithfulness exposes them.

### Suggested Fix Directions (NOT applied)

**For Symptom 2 (onset merging):**
- Option A: Extend `RestGrid` with 64th (`x` or unused suffix) and 128th (`y`)
  values. Need lexer/parser support for new duration tokens — non-trivial scope.
- Option B: Allow controlled overshoot in `AddRests` — emit the grid value
  closest to remaining (not just ≤ remaining), then advance cursor by emitted.
  This shifts subsequent events forward by ≤ 24 ticks at worst. Net effect:
  micro-timing preserved, total bar duration stays close (next bar's downbeat
  may be early by ≤24 ticks but that's still <30ms, below human detection
  threshold).
- Option C: Anchor each emission at the actual MIDI onset (groupStart) instead
  of cursor. Compute rest BACKWARD from groupStart to cursor. Same tradeoff
  but eliminates accumulating drift entirely. Risk: bar internal sum no longer
  matches barTicks exactly, depending on rest-grid choices.

**For Symptom 1.B.1 (sample truncation):**
- Add a fixed-duration natural-decay tail (e.g., +500ms of un-enveloped sample
  data) after the authored-duration buffer, so the buffer naturally rings into
  silence even for short notes. Bar overlap during the tail is handled by
  SongRenderer's additive mix. The Phase 28 100ms crossfade for tied notes
  could be the model: extend by 500ms instead of 100ms for ALL non-staccato
  notes, with the envelope ramping the tail to silence to avoid a release click.
- This is the standard "sustain pedal" effect for piano — Romantic music
  assumes pedal-down throughout, so this would match composer intent better.

**For Symptom 1.B.2 (snap undershoot):**
- Allow `SnapDurationCapped` to choose grid values that OVERSHOOT cap by ≤
  tolerance × 3 (~36 ticks at TPQN=384, ~47ms at 120bpm). Pair with cursor
  advance by snappedDuration (already done) so the next event is pushed
  forward proportionally. Within-bar net duration stays bounded.

**For Symptom 1.B.3 (cross-bar re-attack):**
- Track cross-bar continuation explicitly: when emitting a span that crosses
  a bar boundary, emit a SINGLE NoteElement with full original duration in
  bar 1, and emit a "rest" placeholder in bar 2 that the renderer recognizes
  as "do nothing, this slot belongs to a previous bar's overflow." This
  requires plumbing through BarRenderer to know about cross-bar voice
  ownership. Alternative: emit no extra fragment in bar 2 at all, and rely
  on the bar 1 voice's buffer being long enough to overflow into bar 2's
  render time (SongRenderer.MixVoicesToStereoBuffer's `destFrame` already
  supports this — see SongRenderer.cs:298 `if (destFrame >= totalFrames) continue;`,
  but the fragment is already emitted as a second note so the second attack
  fires anyway).
- Simpler: in Quantizer.SplitSpansAtBars + EmitVoiceElements, when a span is
  IsContinued, in the CONTINUING bar emit nothing for that note (no fragment),
  and instead extend bar 1's emission to the full original duration. Let the
  buffer overflow into bar 2's render window via SongRenderer's per-voice
  positioning. Risk: bar 2's voice allocation might place a NEW note at the
  same time that needs the now-occupied slot — but voice blocks render
  additively so this is fine.

### Files Investigated (Continuation)

- `flow-midi/Conversion/Quantizer.cs` (lines 685-793: SnapDurationCapped + AddRests + RestGrid)
- `flow-midi/Conversion/Quantizer.cs` (lines 485-589: EmitVoiceElements cursor/snap interaction)
- `flow-midi/Conversion/Quantizer.cs` (lines 420-475: SplitSpansAtBars + AllocateGroupsToVoices)
- `flow-midi/Conversion/FlowGenerator.cs` (lines 236-314: FormatBar + FormatElements with useAutoFit=false)
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs` (lines 36-160: tie-sustain look-ahead, voice-block recursion)
- `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs` (lines 50-133: sample truncation + envelope shaping)
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` (lines 214-321: RenderSection + MixVoicesToStereoBuffer)
- `flow-lang/StandardLibrary/Audio/SynthUtils.cs` (lines 130-210: GenerateArticulationADSR — release ramp)
- `flow-lang/TypeSystem/SpecialTypes/BarType.cs` (lines 182-220: ToTimeline + IsChordTone handling)

### Test Files

- `/tmp/test_spacing.wav` — confirms within-voice s. spacing renders correctly
- `/tmp/test_xbar_tied.wav` + `/tmp/test_xbar_untied.wav` — confirms tied/untied
  cross-bar rendering is byte-identical (tie has no effect across bars for piano)
- `/tmp/test_single_long.wav` — confirms G5 sample natural decay = 1.4s
- `/tmp/test_quarter.wav` — confirms G5 sample is truncated at authored 1.0s
- `/tmp/test_xbar_held.wav` — confirms cross-bar re-attack (20dB jump at bar boundary)
- `/tmp/test_nocturne_bar1.wav` — confirms G5+D#2 micro-onset collapse to single attack
