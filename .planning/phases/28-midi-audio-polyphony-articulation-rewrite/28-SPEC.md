# Phase 28: MIDI + Audio Polyphony & Articulation Rewrite — Specification

**Created:** 2026-05-10
**Ambiguity score:** 0.15
**Requirements:** 9 locked

## Goal

Overlapping notes within a single Flow sequence (held whole-note pedal under syncopated 16th-note inner voices, ragtime-style stride patterns, contrapuntal lines in one phrase) render at their authored duration in BOTH MIDI export and WAV synthesis — no truncation, no audible cut-off — and the six existing articulations (Staccato, Tenuto, Marcato, Accent, Sforzando, plus a NEW first-class `Legato` value) become per-instrument-aware envelope-shape modifiers with locked semantics, replacing the current implicit duration-and-velocity-only model. `writeMidi` emits a multi-track Standard MIDI File where each Flow `Sequence` within a section becomes its own MIDI track with independent program changes.

## Background

Today (post-Phase 27 baseline):

**Polyphony surface:**
- A `Sequence` is a strictly sequential timeline. `BarRenderer.cs:54-110` walks `bar.ToTimeline()` and advances `offsetBeats` cumulatively per note. Within one sequence, you cannot write a held whole note plus an inner running line — the next note's onset is always `prevOnset + prevDuration`.
- Polyphony exists ACROSS sequences within a section: each `Sequence` produces its own `List<Voice>` and `SongRenderer` mixes voices additively. Composers wanting ragtime stride must split LH and RH into two sequences manually.
- Chord syntax (`[C E G]q`) stacks tones at the same onset using `IsChordTone=true` + `effectiveTick = leadBarTick` (`MidiExport.cs:303-312`); useful for vertical chords, useless for one-voice-holds-while-another-runs.

**Articulation surface:**
- `Articulation` enum: `Normal`, `Staccato`, `Tenuto`, `Marcato`, `Accent`, `Sforzando` (`flow-lang/TypeSystem/SpecialTypes/NoteType.cs:200`). NO `Legato` value — legato lives separately as a transform `legato(Sequence, Double)` (Phase 22 DX-14) that mutates `DurationOverlap`.
- Note-stream syntax: `>` = Accent, `stacc` = Staccato, `ten` = Tenuto, `marc` = Marcato (`Parser.NoteStream.cs:394-417`). `Sforzando` has no syntax.
- Render-time effect (`BarRenderer.cs:67-77`): Staccato truncates buffer to 50% duration; Marcato to 80%; Normal/Tenuto/Accent/Sforzando do not modify duration. `NoteStreamCompiler.cs:670-674` applies +velocity for Accent / Marcato / Sforzando.
- Synthesizers (e.g. `PianoSynthesizer.cs`) all use the SAME ADSR envelope (attack 0.003, decay 0.6, sustain 0.12, release 0.3) regardless of articulation. Staccato sounds dry/cut-off because the decay tail is squeezed into the truncated buffer rather than the synth shaping a proper short percussive envelope.

**MIDI export surface:**
- `writeMidi` produces a 2-track SMF: Track 0 = conductor (tempo/timesig/key meta), Track 1 = ALL notes from ALL sequences across ALL sections. Single program-change at start (GM 0 = piano). `MidiExport.cs:246-251`.
- DAW workflows lose per-voice routing because every sequence collapses into one channel.
- Microtonal MIDI is documented as deferred (Phase 23 D-13 advisory warning); stays deferred this phase.

**Determinism surface:**
- Phase 18, 25, 27 byte-identical tests run scripts twice and assert `bytes1.SequenceEqual(bytes2)` — content-agnostic two-run gates. They will continue to pass under Phase 28 as long as Phase 28's runtime is itself deterministic.
- Pre-Phase-28 vs post-Phase-28 byte equivalence is NOT preserved — Phase 28 legitimately changes rendered output (better articulation envelopes, multi-track MIDI, single-sequence polyphony all change bytes). Tests that pin pre-Phase-28 bytes are migrated to RMS-window comparison.

This phase rewrites the polyphony model, articulation rendering, and MIDI export topology together because they share the underlying `BarRenderer → Voice → SongRenderer/MidiExport` data flow — splitting them creates contradictory partial states.

## Requirements

1. **Single-sequence polyphony via voice-block syntax**: Composers can write `| {voice C4w} {voice C5q D5q E5q F5q} |` to render two parallel voices within one bar of one sequence.
   - Current: Cannot express held + running notes in one sequence; must split into 2 sequences manually
   - Target: New `voice` block parser/runtime accepts `{voice <notes>}` inside a note-stream; compiler emits N parallel timelines that share the bar's onset (offset 0) and run independently. Multiple voice blocks per bar are allowed (≥2). Mixing voice blocks with regular notes outside the blocks is supported (regular notes form an implicit "voice 0" that runs in parallel with the explicit blocks)
   - Acceptance: A `.flow` script with `Sequence stride = | {voice C4w} {voice C5q D5q E5q F5q} |` renders to a 4-beat WAV in which (a) the C4 fundamental's RMS is non-zero across the entire bar, AND (b) each of C5/D5/E5/F5 produces a distinct attack transient at beats 0, 1, 2, 3, AND (c) MIDI export emits NoteOn(C4) at tick 0, NoteOff(C4) at tick = 4×TPQN, AND a parallel sequence of NoteOn/NoteOff pairs for C5..F5 at quarter-note ticks. Verified by both `dotnet run` smoke + RMS regression test

2. **Held-note non-truncation**: Notes whose authored duration exceeds the time to the next event in their voice render fully without buffer truncation.
   - Current: Even across sequences, certain edge cases (per the user's bug report) cut off held notes — the `BarRenderer.cs:81-86` 100ms tied-overlap is a partial mitigation but does not generalize
   - Target: A note's audio buffer is generated for `duration × articulation_factor + envelope_release_tail` samples; subsequent events do NOT cause truncation. Voice mixing in `SongRenderer` handles overlap additively (existing behavior, now defended by tests)
   - Acceptance: Test fixture `examples/tests/polyphony_held_note.flow` containing `Sequence held = | C2w |` and `Sequence runs = | C5q D5q E5q F5q |` in the same section renders such that C2's RMS in the final 50ms of the bar is ≥ 50% of C2's RMS in the first 50ms (i.e. the note is still audibly sustaining at the end). Verified by RMS regression

3. **First-class Legato articulation**: `Articulation.Legato` is added to the enum with locked envelope semantics; the existing `legato(Sequence, Double)` transform is preserved as a bulk-application convenience that internally sets the per-note attribute.
   - Current: Legato lives only as a transform (Phase 22 DX-14); the enum lacks a Legato value; per-note legato attribution requires `legato(seq, 0.1)` over the whole sequence
   - Target: `Articulation.Legato` added to enum; note-stream syntax adds `leg` token (mirroring `stacc`/`ten`/`marc`); compiler and BarRenderer apply Legato semantics per-note; `legato(seq, overlap)` transform unchanged but now equivalent to setting `Articulation.Legato` + `DurationOverlap=overlap` on every note in the sequence
   - Acceptance: `| C4q leg D4q leg E4q F4q |` parses without error, the compiled MusicalNoteData has `Articulation.Legato` for C4 and D4, `Articulation.Normal` for E4 and F4. Tests verify the parser accepts `leg`, the enum contains `Legato`, the existing transform still works

4. **Locked articulation envelope rules (cross-instrument generic layer)**: The six articulations have falsifiable per-articulation duration + velocity rules applied at the BarRenderer / NoteStreamCompiler layer.
   - Current: Staccato = 50% duration; Marcato = 80% duration + velocity boost; Accent / Sforzando = velocity boost (no duration change); Tenuto = full duration; no Legato per-note attribute
   - Target (LOCKED rules — exact values land in SPEC):
     - **Staccato**: audible duration = 25% of authored; velocity unchanged; envelope = sharp attack (1.5× synth's normal attack speed) + zero sustain + fast release
     - **Tenuto**: audible duration = 100% of authored; velocity unchanged; envelope = synth-default + soft release (1.2× normal release)
     - **Legato**: audible duration = 110% of authored (10% overlap into next note); velocity unchanged; envelope = crossfade-style (next note's attack overlaps tail)
     - **Accent**: audible duration = 100% of authored; velocity = +30% (clamp to ≤ 1.0); envelope = synth-default
     - **Marcato**: audible duration = 25% of authored (Staccato-shortened); velocity = +30% (Accent-boosted); envelope = Staccato envelope
     - **Sforzando**: audible duration = 100% of authored; velocity = base + 50% spike at attack, decay back to base over the first 15% of duration; envelope = synth-default
   - Acceptance: A unit test asserts that for an authored `C4q` (1 beat at BPM 120 = 0.5s) under each articulation, the rendered buffer's audible-duration (RMS-thresholded) and peak velocity match the locked rule within tolerance (±5% duration, ±2 MIDI velocity units)

5. **Per-instrument articulation envelopes**: Every shipping synthesizer (Piano, Brass, Sax, Drums, Bell, Flute, Organ, Strings, Wavetable — 9 total) implements articulation-aware envelope rendering, NOT just the generic duration multiplier.
   - Current: All synthesizers use a single fixed ADSR regardless of articulation; PianoSynthesizer.cs:51-53 hard-codes `attack: 0.003, decay: 0.6, sustain: 0.12, release: 0.3`
   - Target: `INoteSynthesizer` interface gains an articulation parameter (or articulation reaches synthesizers via the existing `MusicalNoteData note` parameter — design is locked in discuss-phase). Each synth implements per-articulation envelope variants. A staccato piano produces a punchy, percussive sound (sharp hammer + short body); a staccato sax produces a tongued attack with quick release; etc. Drum synth's articulation rules are no-ops (drums are inherently percussive)
   - Acceptance: For each of the 9 synthesizers, a unit test renders the same note (C4q) under Normal vs Staccato vs Legato and asserts that the resulting buffers' spectral envelopes (FFT magnitude bins) differ measurably (cosine similarity < 0.95 between Normal/Staccato pair). Plus manual UAT listening confirms audible timbre differentiation per instrument

6. **Multi-track MIDI export**: `writeMidi` emits one MIDI track per Flow `Sequence` within each section, with independent program changes per track.
   - Current: All notes go to single Track 1; one program change at file start (`MidiExport.cs:250-251`)
   - Target: Track 0 = conductor (unchanged); Tracks 1..N = one per Sequence, where N = `Σ(section.Sequences.Count)` across all sections. Each track gets its own ProgramChange event matching the synthesizer name registered for that sequence's render (piano → GM 0, brass → GM 56, sax → GM 65, drums → GM channel 9 / program 0, etc.). Sequence ordering preserved (insertion order from `SectionData.Sequences` Dictionary). Cross-section sequences with the same name (e.g. "melody" in intro AND chorus) are CONCATENATED onto the same track (existing repeat-aware ordering preserved)
   - Acceptance: A multi-sequence test fixture exports to a `.mid` file whose `MidiFile.Chunks.Count == 1 + uniqueSequenceCount`. Loading the file in DryWetMidi confirms each non-conductor track contains only NoteOn/NoteOff for one named sequence. Manual UAT: import into a DAW (LMMS, Reaper, or equivalent) and confirm each Flow sequence appears as a routable track

7. **Voice-pool allocation policy**: WAV rendering uses a bounded voice pool with steal-oldest policy when exhausted, replacing the current "every note creates a new Voice object" pattern.
   - Current: `BarRenderer.RenderBarToVoices` creates an unbounded `List<Voice>` — one Voice per note. For dense polyphonic writing (e.g. 100+ overlapping voices), memory grows unbounded; CPU mixing scales O(N) per output sample
   - Target: `VoiceAllocator` (already present at `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs`, 85 lines) gets a pool with default size 32 voices per section, configurable per-section via `voicePool N { ... }` musical-context block (analogous to `tempo`/`timesig`). When pool exhausted: steal-oldest (the voice with the longest elapsed playtime gets its remaining samples truncated and reused). Pool size 32 is the locked default — fits human hand polyphony (10 fingers × 3 hand-overlap maximum) plus headroom
   - Acceptance: A stress test fixture with 50 simultaneously-onset notes renders without exception; voice count peaks at 32; the oldest 18 voices are stolen rather than the newest 18 dropped. Verified by counting Voice instances during render via diagnostic counter

8. **Test approach: RMS-windowed regression with frame-count exact match**: Byte-identical determinism is dropped; tests assert RMS energy in 100ms time windows is within ±0.5 dB of a pinned baseline, plus rendered frame count matches exactly.
   - Current: Phase 18/25/27 ByteIdentical tests use `bytes1.SequenceEqual(bytes2)` content-agnostic two-run gates
   - Target: New `RmsRegressionTests` infrastructure in `flow-lang.Tests/Helpers/`. Test pattern: `AssertRmsWithinTolerance(rendered, baselineWavPath, windowMs: 100, toleranceDb: 0.5)`. Baselines committed under `flow-lang.Tests/baselines/Phase28/` as small reference WAV files (≤ 10 sec each). Existing two-run determinism contract preserved (tests run scripts twice and assert exact byte match — proves Phase 28's runtime is itself deterministic). Per-test override mechanism allowed: tests requiring looser tolerance must declare it inline with reason
   - Acceptance: At least one test in the new suite passes with the locked tolerance band; at least one test exists where intentional regression (e.g. setting Staccato to 100% duration instead of 25%) fails the test with a clear "RMS deviation in window 4 (300-400ms): expected -12.3 dB, got -7.1 dB" diagnostic

9. **Manual UAT sign-off + ragtime fixtures**: Phase closure requires composer manual listening sign-off documented in `28-VERIFICATION.md`.
   - Current: Phase 27 closure has manual UAT for tutorial/showcase sound; Phase 17 closure deferred HUMAN-UAT
   - Target: Two test fixtures committed: (a) `examples/tests/ragtime_polyphony.flow` — synthetic 4-bar piece exercising held + running, staccato + sustained pedal, legato across tuplets, mixed-articulation chord; (b) `examples/tests/maple_leaf_opening.flow` — first 8 bars of Joplin's "Maple Leaf Rag" (1899, public domain) hand-transcribed, exercising LH stride + RH syncopated melody. After Wave-N execution: composer renders both fixtures, listens, and signs off in `28-VERIFICATION.md` with an explicit "Audibly correct: ✓" line per fixture. Render-baseline WAVs are committed to `flow-lang.Tests/baselines/Phase28/`
   - Acceptance: `28-VERIFICATION.md` contains a "Manual UAT Sign-off" section with two checkboxes: `[x] ragtime_polyphony.flow listened — held notes sustain, articulations distinct` and `[x] maple_leaf_opening.flow listened — stride pattern audible, RH/LH separation clear`. Phase cannot close while either checkbox is unchecked

## Boundaries

**In scope:**
- Voice-block syntax `{voice ...}` inside note-streams; parser, lexer, NoteStreamCompiler integration
- New `Articulation.Legato` enum value + `leg` note-stream token + parser support
- Locked articulation envelope rules (Requirement 4) at BarRenderer / NoteStreamCompiler layer
- Per-instrument articulation envelopes for all 9 shipping synthesizers
- Multi-track MIDI export (one track per sequence, per-track program change)
- Voice-pool allocation with default 32-voice pool + `voicePool N { ... }` musical-context block + steal-oldest policy
- RMS-window regression test infrastructure (`flow-lang.Tests/Helpers/RmsRegressionTests`)
- Two ragtime test fixtures + committed baseline WAVs
- Manual UAT sign-off in `28-VERIFICATION.md`
- Update `examples/tutorial.flow` chapter on articulation if Phase 27's tutorial chapter benefits from re-render under new envelope rules (planner discretion)

**Out of scope:**
- Microtonal MIDI per-channel pitch-bend — stays deferred per Phase 23 D-13; revisit in v1.5
- Sampler / sample-based instruments — that is Phase 29 (Instrument Realism)
- New flow-lsp diagnostics for voice-block syntax errors — flow-lsp owns its own surface; Phase 28 emits clear interpreter errors and Phase 29+ flow-lsp work picks up the parser hooks
- Round-trip MIDI import upgrade — Phase 28 only changes EXPORT side; flow-midi MIDI import improvements (better polyphony detection in imported MIDI) are a separate v1.5 concern
- Real-time playback latency optimization — voice-pool size and steal-oldest are about audio correctness; PulseAudio playback engine performance is not touched
- Articulation timing humanization — adding human-feel timing variation per articulation (e.g. staccato slightly ahead of beat) is a separate humanize-extension concern, not Phase 28
- Polyrhythm beyond voice-blocks — full per-voice independent time signatures (e.g. 3/4 LH against 4/4 RH simultaneously) is a v1.5 concern; voice-blocks share the bar's time signature this phase
- Pre-Phase-28 byte-identical baseline preservation for tutorial/showcase output — those files will sound legitimately better but produce different bytes; Phase 28 closure rewrites Phase 18/25/27 ByteIdentical tests to use RMS-window assertions where they pinned bytes (the two-run-cmp-clean determinism contract is preserved separately)
- Cross-section voice-pool persistence (a held note crossing section boundaries) — pool resets at section boundary; section-bridging held notes documented as future work

**Adjacent problems excluded:**
- General DSP improvements (better reverb, new effects) — Phase 29 / v1.5 concern
- MIDI 2.0 / per-note expression — out of scope; SMF 1.x output only
- Instrument-specific control surfaces (pedal CC for piano sustain) — composer can author CC events post-export in DAW; Phase 28 stays SMF-baseline

## Constraints

- **Determinism preserved (two-run cmp-clean)**: Phase 28's runtime must itself be deterministic — running the same script twice at the same git SHA must produce byte-identical output. The byte-equivalence DROPPED is across-Phase-boundary (pre-Phase-28 bytes != post-Phase-28 bytes). Tests verify both: existing two-run tests still pass; new RMS tests pin behavior over time.
- **No new external dependencies**: Voice-pool, articulation envelopes, multi-track MIDI all build on existing `Melanchall.DryWetMidi 8.0.3` and the project's hand-rolled DSP. RMS test infrastructure uses existing FFT-free sample-iteration code.
- **Backward compatibility**: Existing `.flow` scripts that don't use the new `voice` block, don't author Legato attributes via `leg` token, and don't call `voicePool` continue to render audibly equivalent or audibly improved output (Phase 28 default rules apply, but they're tuned to preserve perceptual equivalence for non-articulated notes). Scripts that DO use new features get the new behavior.
- **Test runtime budget**: New per-articulation per-synth tests (6 articulations × 9 synths = 54 fact baselines) must run within 30 seconds total; individual fact ≤ 0.5 sec. Use small WAV durations (0.5 sec rendered audio) to stay within budget.
- **Voice-pool default size 32**: Locked default; `voicePool N { ... }` block accepts 1 ≤ N ≤ 256. N > 256 raises a clear interpreter error.
- **RMS tolerance default ±0.5 dB / 100ms**: Locked default; per-test override syntax provided for tests where envelope shaping legitimately exceeds tolerance.
- **flow-midi import unaffected**: Phase 28 changes are export-side only; `flow-midi/Midi/MidiParser.cs` is not touched.

## Acceptance Criteria

- [ ] `Sequence stride = | {voice C4w} {voice C5q D5q E5q F5q} |` parses without error
- [ ] Rendered WAV from the above shows held C4 sustaining (RMS in last 50ms ≥ 50% of first 50ms) AND distinct attacks at 4 quarter-note positions for C5..F5
- [ ] MIDI export from the same script produces NoteOn(C4)/NoteOff(C4) at tick 0 and tick 4×TPQN, AND parallel NoteOn/NoteOff pairs for C5..F5 at quarter-note ticks, in two separate tracks
- [ ] `Articulation.Legato` exists in `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` enum
- [ ] Note-stream parser accepts `leg` token; `| C4q leg D4q |` compiles to MusicalNoteData with Articulation.Legato on C4
- [ ] Each of the 6 articulations (Staccato/Tenuto/Legato/Accent/Marcato/Sforzando) produces a rendered buffer matching the locked rule within ±5% audible-duration and ±2 velocity units (54 facts: 6 articulations × 9 synths)
- [ ] All 9 shipping synthesizers (Piano, Brass, Sax, Drums, Bell, Flute, Organ, Strings, Wavetable) have articulation-aware envelope rendering
- [ ] `writeMidi` produces a multi-track SMF: `MidiFile.Chunks.Count == 1 + uniqueSequenceCount`; each non-conductor track contains only one named sequence's events
- [ ] Per-track MIDI program changes match the synthesizer name registered for that sequence (piano → GM 0, brass → GM 56, sax → GM 65, drums → channel 9 GM 0)
- [ ] `voicePool 16 { ... }` musical-context block parses and applies the pool size limit during render
- [ ] Stress test with 50 simultaneous note onsets caps Voice count at the pool size (default 32) using steal-oldest policy
- [ ] `RmsRegressionTests` infrastructure exists at `flow-lang.Tests/Helpers/`; baselines committed at `flow-lang.Tests/baselines/Phase28/`
- [ ] At least one positive RMS regression test passes with locked ±0.5 dB / 100ms tolerance
- [ ] At least one negative RMS test demonstrates the diagnostic (intentional regression triggers a clear window-by-window dB-deviation message)
- [ ] Two test fixtures exist: `examples/tests/ragtime_polyphony.flow` (synthetic) + `examples/tests/maple_leaf_opening.flow` (real ragtime)
- [ ] `28-VERIFICATION.md` contains "Manual UAT Sign-off" section with both ragtime fixtures' checkboxes checked before phase closure
- [ ] Existing `legato(Sequence, Double)` transform continues to work; Phase 22 LegatoFacts tests stay GREEN
- [ ] Existing Phase 18 / 25 / 27 ByteIdentical two-run tests stay GREEN (Phase 28 runtime is itself deterministic); their per-byte assertions pinning specific values are migrated to RMS-window assertions where applicable
- [ ] Full unit suite GREEN (zero regressions; existing pass count preserved aside from the migrated byte-pin tests)
- [ ] `examples/tutorial.flow` and `examples/showcase.flow` continue to run to exit 0 with non-empty WAV/MID output (legitimate audio change is acceptable; render-failure is not)

## Ambiguity Report

| Dimension          | Score | Min  | Status | Notes                                                                                  |
|--------------------|-------|------|--------|----------------------------------------------------------------------------------------|
| Goal Clarity       | 0.90  | 0.75 | ✓      | All 4 user-described failure modes scoped; locked syntax, locked rules, locked tests   |
| Boundary Clarity   | 0.78  | 0.70 | ✓      | Microtonal MIDI deferred; sampler deferred; flow-lsp deferred; cross-section pool deferred |
| Constraint Clarity | 0.85  | 0.65 | ✓      | RMS ±0.5 dB / 100ms locked; pool size 32 default + 1..256 range; runtime budget pinned |
| Acceptance Criteria| 0.85  | 0.70 | ✓      | 19 pass/fail criteria; UAT required; fixtures named (synthetic + Maple Leaf)          |
| **Ambiguity**      | 0.15  | ≤0.20| ✓      | Gate passed                                                                            |

## Interview Log

| Round | Perspective       | Question summary                                                            | Decision locked                                                                                  |
|-------|-------------------|-----------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------|
| 1     | Researcher        | Which user-visible failure modes are in scope?                              | All 4: held+running single-sequence, held truncation, staccato sound, articulation distinctness |
| 1     | Researcher        | Multi-MIDI-channel separation — essential or optional?                      | Essential — multiple tracks/channels required (DAW round-tripping)                              |
| 1     | Researcher        | Byte-identical determinism contract — keep or drop?                         | Drop pre/post-Phase-28 byte equivalence; keep two-run determinism; tests use fuzzy-match RMS    |
| 2     | Simplifier        | Single-sequence polyphony syntax — which form?                              | `| {voice C4w} {voice C5q D5q E5q F5q} |` voice-block form                                       |
| 2     | Simplifier        | Articulation envelope rules — how prescriptive?                             | Lock rules per articulation (Staccato 25%, Legato 110%+crossfade, etc.)                          |
| 2     | Simplifier        | Test approach for non-byte-identical output?                                | RMS energy in 100ms windows + frame count exact match                                            |
| 3     | Boundary Keeper   | Per-instrument articulation tuning — in or out?                             | IN — each of 9 synths gets articulation-aware envelopes                                          |
| 3     | Boundary Keeper   | Microtonal MIDI per-channel pitch-bend — in or deferred?                    | Deferred (Phase 23 D-13 advisory stays in place)                                                |
| 3     | Boundary Keeper   | Existing legato(Sequence, Double) transform — fate?                         | Keep + add Articulation.Legato attribute; both forms coexist                                    |
| 4     | Failure Analyst   | Define ragtime test fixture concretely?                                     | Both: synthetic `ragtime_polyphony.flow` + real Maple Leaf opening                              |
| 4     | Failure Analyst   | RMS tolerance band — how strict?                                            | ±0.5 dB / 100ms (per-test override allowed)                                                     |
| 4     | Failure Analyst   | Manual UAT — required, optional, skipped?                                   | Required — listen + sign-off in 28-VERIFICATION.md before phase closure                         |

---

*Phase: 28-midi-audio-polyphony-articulation-rewrite*
*Spec created: 2026-05-10*
*Next step: /gsd-discuss-phase 28 — implementation decisions (voice-block parser approach, envelope contract design, MIDI track ordering, pool-allocator data structure)*
