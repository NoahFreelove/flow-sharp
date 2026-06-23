---
slug: sustain-pedal-no-effect
status: resolved
trigger: |
  The `sustainPedal { ... }` musical-context block has no effect on rendered
  audio. Wrapping a section in `sustainPedal { }` produces byte-identical
  output to not wrapping it, when it should extend every note's rendered
  duration by MusicalContext.SustainTailSeconds (2.0s) so notes "ring through
  subsequent attacks." Discovered while making the playground "Abide With Me"
  demo less abrupt — sustainPedal was tried as a fix and produced a byte-
  identical render, so the demo shipped with organ+reverb instead.
created: 2026-06-22
updated: 2026-06-22
---

# Debug Session: sustain-pedal-no-effect

## Symptoms

**Expected behavior:**
Wrapping notes/sections in a `sustainPedal { ... }` block should extend every
note's rendered audio buffer by `MusicalContext.SustainTailSeconds` (2.0s) so
the notes ring through subsequent attacks, like a piano sustain pedal. The
rendered WAV should differ (longer/fuller) from the same script without the
`sustainPedal` wrapper.

**Actual behavior:**
The rendered WAV is byte-identical (verified via 50ms-window RMS profiling)
whether or not the `sustainPedal { }` wrapper is present. The pedal has zero
audible/measurable effect.

**Error messages:** None. Render completes cleanly (exit 0); the failure is a
silent no-op.

**Timeline:**
Observed 2026-06-22 during the "Abide With Me" playground-demo work. Likely as
old as the sustainPedal feature itself — no example/test exercised it with a
section wrapped in the block until now.

**Reproduction:**
```bash
# A: with pedal
cat > /tmp/sp_on.flow <<'EOF'
use "@std"
use "@audio"
tempo 88 { timesig 4/4 {
    sustainPedal {
        section t { Sequence s = | mf C4q C4q C4q C4q | mf C4w | }
    }
    Song song = [t]
    Buffer b = (renderSong song "piano")
    (writeWav "/tmp/sp_on.wav" b)
}}
EOF
# B: without pedal — identical except the sustainPedal {/} wrapper lines removed
dotnet run --project flow-interpreter /tmp/sp_on.flow
dotnet run --project flow-interpreter /tmp/sp_off.flow
cmp /tmp/sp_on.wav /tmp/sp_off.wav   # BUG: they are identical
```
NOTE: the sampled "piano" note is only ~1.2s, so to see the tail effect prefer
a synthesized sustaining instrument OR confirm the extension at the buffer
level (frame count / Voice buffer length) rather than relying on piano audio.
Use "organ" or assert on `section.Context.SustainPedal` + rendered Voice frame
counts to avoid the sample-length confound (see external memory
project_sampled_piano_short_sustain).

## Code Path Already Traced

- Parser: `flow-lang/Parsing/Parser.cs:231` (Match SustainPedal) →
  ParseMusicalContextStatement(MusicalContextType.SustainPedal); value
  synthesized at Parser.cs:1042-1043.
- Interpreter: `flow-lang/Interpreter/Interpreter.cs:391-396` sets
  `musicalCtx.SustainPedal = true`.
- MusicalContext: `flow-lang/Runtime/MusicalContext.cs:121` (SustainPedal
  bool?), `:130` (SustainTailSeconds=2.0), `:206` (SustainPedal copied in a
  clone/With path — verify ALL push/pop/clone paths copy the bool?).
- SongRenderer: `flow-lang/StandardLibrary/Audio/SongRenderer.cs:424` reads
  `section.Context?.SustainPedal == true` → passes `sustainActive` into
  `SequenceRenderer.RenderSequenceToVoicesWithPool(..., sustainActive)`
  (:438-441).
- SequenceRenderer:
  `flow-lang/StandardLibrary/Audio/SequenceRenderer.cs:93-119` threads
  `sustainPedalActive` into `BarRenderer.RenderBarAtBeat(..., sustainPedalActive)`.
- BarRenderer: `flow-lang/StandardLibrary/Audio/BarRenderer.cs:159` computes
  `sustainTailBeats` from SustainTailSeconds and (presumably) extends note
  duration.

## Current Focus

reasoning_checkpoint:
  hypothesis: "ExecutionContext.GetMusicalContext() frame-merge loop drops the SustainPedal bool? — it merges TimeSignature/Tempo/Swing/Key/Velocity/Pan/Gain/ReverbTime/TuningStack/VoicePoolSize via `??=` but has NO `resolved.SustainPedal ??= frame.MusicalContext.SustainPedal` line. So when `sustainPedal { }` sets SustainPedal=true on its own frame and the inner `section` snapshots context via GetMusicalContext (Interpreter.cs:830), the merged snapshot has SustainPedal=null. SongRenderer.cs:424 `section.Context?.SustainPedal == true` is false → the correctly-threaded extension at BarRenderer.cs:157-161 never activates."
  confirming_evidence:
    - "ExecutionContext.cs:928-953 merge loop lists 10 fields; SustainPedal is absent."
    - "MusicalContext.Clone() (MusicalContext.cs:206) DOES copy SustainPedal — proving the field is real and the OMISSION is in the merge path, not the type."
    - "BarRenderer.cs:157-161 correctly applies sustainTailBeats to durationBeats when sustainPedalActive=true; SequenceRenderer.cs:107-114 + SongRenderer.cs:438-441 correctly thread the flag. The whole render-side chain is correct — only the context-resolution input is wrong."
    - "Repro: organ render (sustaining synth, no piano sample confound) byte-identical 962224 bytes with vs without sustainPedal { } wrapper."
  falsification_test: "Add `resolved.SustainPedal ??= frame.MusicalContext.SustainPedal;` to the merge loop. If sp_on.wav becomes LONGER/different from sp_off.wav, hypothesis confirmed. If still identical, hypothesis wrong."
  fix_rationale: "Adds the one missing field to the frame-merge resolution — the root cause is a missing line in the inheritance chain, not a logic error. Matches the existing `??=` innermost-wins pattern for every other context field. Scripts without a sustainPedal block have SustainPedal=null on every frame, so `resolved.SustainPedal` stays null → zero behavioral change → byte determinism preserved."
  blind_spots: "Must confirm the early-break `if (resolved.X != null ...) break;` short-circuit (cs:955-960) does not need SustainPedal added — it shouldn't, since not all fields are ever set, but adding SustainPedal to it would over-eagerly break; leave it out. Must run RMS baselines to confirm no script-without-pedal byte drift."

- next_action: Apply fix at ExecutionContext.cs merge loop; rebuild; re-run repro (expect sp_on != sp_off); run RMS/baseline + determinism tests.

## Constraints

- Charitable-interpretation project (CLAUDE.md) — don't throw.
- Fix MUST preserve two-run cmp-clean determinism and MUST NOT change byte
  output for scripts WITHOUT sustainPedal (RMS baselines under
  flow-lang.Tests/baselines/ are regression-sensitive).
- Real renderer/context fix — NOT editing the MIDI exporter.
- .NET 10 / C# 13, file-scoped namespaces, record AST nodes.

## Evidence

- timestamp: 2026-06-22
  checked: BarRenderer.cs:157-161 (extension apply site)
  found: `if (sustainPedalActive) { durationBeats += sustainTailBeats; }` — the
    extension IS applied when the flag is true. The note buffer genuinely
    lengthens.
  implication: SECONDARY hypothesis (dead extension) ELIMINATED. The render-side
    apply logic is correct.

- timestamp: 2026-06-22
  checked: SequenceRenderer.cs:107-114, SongRenderer.cs:424,438-441 (threading)
  found: `sustainActive = section.Context?.SustainPedal == true` →
    RenderSequenceToVoicesWithPool(..., sustainActive) → RenderBarAtBeat(...,
    sustainPedalActive). Full chain threads the flag correctly.
  implication: The render-side INPUT is whatever section.Context.SustainPedal is.
    Bug must be upstream — section.Context never gets SustainPedal=true.

- timestamp: 2026-06-22
  checked: Interpreter.cs:252,396,421,830 (context flow) +
    ExecutionContext.GetMusicalContext() merge loop (cs:913-975)
  found: ExecuteMusicalContext makes a FRESH `new MusicalContext()` per block,
    sets ONLY SustainPedal=true, stores it on the pushed frame. The inner
    section snapshots via `_context.GetMusicalContext()` (cs:830). That merge
    loop (cs:928-953) inherits 10 fields via `??=` but has NO SustainPedal line.
  implication: ROOT CAUSE. SustainPedal is dropped during frame-merge resolution.
    section.Context.SustainPedal stays null → SongRenderer no-ops.

- timestamp: 2026-06-22
  checked: MusicalContext.Clone() (cs:193-219)
  found: Clone DOES copy SustainPedal (cs:206). The type is correct; only the
    GetMusicalContext frame-merge omits it — an inconsistency between the two
    context-propagation paths.
  implication: Fix is a single missing `??=` line in the merge loop, not a
    type/Clone change. The debug-file note pointing at MusicalContext.cs:206 was
    a red herring (that path is correct).

- timestamp: 2026-06-22
  checked: Reproduction with synthesized "organ" (no sampled-piano confound)
  found: sp_on.wav and sp_off.wav are byte-identical (962224 bytes each, cmp
    clean) — sustainPedal { } has zero effect.
  implication: Bug confirmed reproducible; not a sample-length artifact.

## Eliminated

- hypothesis: BarRenderer.cs:159 computes sustainTailBeats but never applies it
    (dead extension)
  evidence: BarRenderer.cs:160 does `durationBeats += sustainTailBeats;` inside
    the `if (sustainPedalActive)` guard — the extension is applied.
  timestamp: 2026-06-22

- hypothesis: MusicalContext clone/Clone() path drops the SustainPedal bool?
  evidence: MusicalContext.Clone() cs:206 explicitly copies SustainPedal. The
    drop is in ExecutionContext.GetMusicalContext frame-merge, not Clone.
  timestamp: 2026-06-22

## Resolution

root_cause: |
  ExecutionContext.GetMusicalContext() resolves the active musical context by
  walking the call stack and merging each frame's MusicalContext fields with
  `??=` (innermost-wins). The merge loop inherited 10 fields (TimeSignature,
  Tempo, Swing, Key, Velocity, Pan, Gain, ReverbTime, TuningStack,
  VoicePoolSize) but had NO line for the SustainPedal bool?. `sustainPedal { }`
  sets SustainPedal=true on its own freshly-pushed frame (Interpreter.cs:396);
  the `section` nested inside snapshots context via GetMusicalContext
  (Interpreter.cs:830), which dropped the flag. So section.Context.SustainPedal
  was always null, and SongRenderer.cs:424 `section.Context?.SustainPedal ==
  true` was always false — the otherwise-correct render-side extension
  (SequenceRenderer -> BarRenderer.cs:157-161) never fired. MusicalContext.Clone()
  copied the flag correctly; only the frame-merge resolution path omitted it.
fix: |
  Added `resolved.SustainPedal ??= frame.MusicalContext.SustainPedal;` to the
  GetMusicalContext frame-merge loop in ExecutionContext.cs, matching the
  existing innermost-wins `??=` inheritance pattern. NOT added to the early-break
  short-circuit condition (which tests all-fields-resolved). Renderer/context
  fix only; MIDI exporter untouched.
verification: |
  - Organ repro: sp_on.wav now DIFFERS from sp_off.wav (first divergence byte
    118563) where before the fix they were byte-identical. Pedal extension now
    fires.
  - No-pedal determinism: sp_off.flow two-run cmp-clean (byte-identical).
  - Pre-existing failure RULED OUT as mine: Phase41ShowcaseRmsTests fails on
    PRISTINE dev (fix git-stashed) with the identical 1.06 dB window-1 delta —
    pulse.flow has no sustainPedal block; baseline drifted independently of
    this fix. Restored fix afterward.
  - Suites green WITH the fix: 81 RMS-regression/determinism/musical-context/
    swing tests pass; 377 Phase28/Phase37/VoicePool/Section/Tuning/Reverb/
    SustainPedal tests pass (includes the 3 new facts).
  - Regression test added: flow-lang.Tests/Unit/QuickFixes/
    SustainPedalContextInheritanceFacts.cs (3 facts — render-side frame-count
    growth, context-resolution true-reaches-section, no-block-leaves-unset
    determinism guard). All 3 pass.
files_changed:
  - flow-lang/Runtime/ExecutionContext.cs
  - flow-lang.Tests/Unit/QuickFixes/SustainPedalContextInheritanceFacts.cs (new)
commit: b2576f2 (fix(sustain-pedal): inherit SustainPedal flag down the context frame chain)
human_uat_remaining: |
  Audible confirmation that sustainPedal { } now rings notes through subsequent
  attacks in a real workflow (e.g. the "Abide With Me" playground demo). The
  structural extension is machine-verified; the perceptual end is the open human
  gate.
