# Phase 28: MIDI + Audio Polyphony & Articulation Rewrite — Context

**Gathered:** 2026-05-10
**Status:** Ready for planning
**Source:** PRD Express Path (28-SPEC.md)

<domain>
## Phase Boundary

Rewrites the polyphony model, articulation rendering, and MIDI export topology together because they share the underlying `BarRenderer → Voice → SongRenderer/MidiExport` data flow. This phase delivers:

1. Single-sequence polyphony via `{voice ...}` blocks inside note streams
2. Held-note non-truncation (notes render their authored duration without buffer cut-off)
3. First-class `Articulation.Legato` enum value + `leg` token (preserves existing `legato(Sequence, Double)` transform)
4. Locked per-articulation envelope rules (Staccato 25% / Tenuto 100% + soft release / Legato 110% + crossfade / Accent +30% velocity / Marcato Stacc+Accent / Sforzando spike-then-decay)
5. Per-instrument articulation envelopes for ALL 9 shipping synthesizers (Piano, Brass, Sax, Drums, Bell, Flute, Organ, Strings, Wavetable)
6. Multi-track MIDI export — one MIDI track per Flow `Sequence` per section, per-track program change
7. Voice-pool allocation (default 32 voices, range 1..256) with `voicePool N { ... }` musical-context block + steal-oldest policy
8. RMS-windowed regression test infrastructure (±0.5 dB / 100ms windows + frame-count exact match)
9. Two ragtime test fixtures (synthetic + Joplin's "Maple Leaf Rag" first 8 bars) + manual UAT sign-off

This phase is the first phase of v1.4. It depends on Phase 27 closure.

</domain>

<decisions>
## Implementation Decisions

All decisions below are LOCKED — they come from the SPEC's interview log and requirements. Do NOT re-litigate during planning.

### Voice-Block Syntax
- **Locked syntax:** `| {voice C4w} {voice C5q D5q E5q F5q} |`
- Multiple voice blocks per bar allowed (≥2)
- Mixing voice blocks with regular notes outside the blocks is supported (regular notes form an implicit "voice 0" running in parallel)
- Compiler emits N parallel timelines that share the bar's onset (offset 0) and run independently

### Held-Note Non-Truncation
- A note's audio buffer is generated for `duration × articulation_factor + envelope_release_tail` samples
- Subsequent events do NOT cause truncation
- Voice mixing in `SongRenderer` handles overlap additively (existing behavior, now defended by tests)

### First-Class Legato
- `Articulation.Legato` added to enum at `flow-lang/TypeSystem/SpecialTypes/NoteType.cs`
- Note-stream syntax adds `leg` token (mirroring `stacc`/`ten`/`marc`)
- Existing `legato(Sequence, Double)` transform preserved as bulk-application convenience that internally sets per-note `Articulation.Legato` + `DurationOverlap=overlap`
- Phase 22 LegatoFacts tests must stay GREEN

### Locked Articulation Envelope Rules (cross-instrument generic layer)
- **Staccato**: audible duration = 25% of authored; velocity unchanged; envelope = sharp attack (1.5× synth's normal attack speed) + zero sustain + fast release
- **Tenuto**: audible duration = 100% of authored; velocity unchanged; envelope = synth-default + soft release (1.2× normal release)
- **Legato**: audible duration = 110% of authored (10% overlap into next note); velocity unchanged; envelope = crossfade-style (next note's attack overlaps tail)
- **Accent**: audible duration = 100% of authored; velocity = +30% (clamp ≤ 1.0); envelope = synth-default
- **Marcato**: audible duration = 25% of authored (Staccato-shortened); velocity = +30% (Accent-boosted); envelope = Staccato envelope
- **Sforzando**: audible duration = 100% of authored; velocity = base + 50% spike at attack, decay back to base over the first 15% of duration; envelope = synth-default

Tolerance for unit tests: ±5% audible-duration, ±2 MIDI velocity units.

### Per-Instrument Articulation Envelopes
- ALL 9 shipping synthesizers implement articulation-aware envelope rendering: Piano, Brass, Sax, Drums, Bell, Flute, Organ, Strings, Wavetable
- `INoteSynthesizer` interface gains an articulation parameter (or articulation reaches synthesizers via the existing `MusicalNoteData note` parameter) — exact transport mechanism is **planner discretion** within these constraints:
  - Each synth must implement per-articulation envelope variants
  - Drum synth's articulation rules are no-ops (drums are inherently percussive)
  - Acceptance: `Normal` vs `Staccato` rendered FFT cosine similarity < 0.95
- 54 facts total = 6 articulations × 9 synths

### Multi-Track MIDI Export
- Track 0 = conductor (unchanged: tempo/timesig/key meta)
- Tracks 1..N = one per Flow `Sequence`, where N = `Σ(section.Sequences.Count)` across all sections
- Each track gets its own ProgramChange event matching the synthesizer name registered for that sequence (piano → GM 0, brass → GM 56, sax → GM 65, drums → channel 9 GM 0)
- Sequence ordering preserved (insertion order from `SectionData.Sequences` Dictionary)
- Cross-section sequences with the SAME NAME (e.g. "melody" in both intro AND chorus) are CONCATENATED onto the same track (existing repeat-aware ordering preserved)
- Acceptance: `MidiFile.Chunks.Count == 1 + uniqueSequenceCount`

### Voice-Pool Allocation
- Default pool size: **32** voices per section (LOCKED)
- Per-section override via `voicePool N { ... }` musical-context block (analogous to `tempo`/`timesig`)
- Range: 1 ≤ N ≤ 256; N > 256 raises a clear interpreter error
- When pool exhausted: **steal-oldest** policy (the voice with the longest elapsed playtime gets remaining samples truncated and reused)
- Stress test: 50 simultaneously-onset notes caps Voice count at 32 using steal-oldest
- Cross-section voice-pool persistence is OUT of scope (pool resets at section boundary)

### Test Approach: RMS-Windowed Regression
- Drop pre-vs-post-Phase-28 byte-identical contracts (the runtime LEGITIMATELY changes bytes due to articulation envelopes + multi-track MIDI + single-sequence polyphony)
- Preserve two-run determinism (same script run twice at same git SHA produces byte-identical bytes)
- New `RmsRegressionTests` infrastructure under `flow-lang.Tests/Helpers/`
- Test pattern: `AssertRmsWithinTolerance(rendered, baselineWavPath, windowMs: 100, toleranceDb: 0.5)`
- Default tolerance: **±0.5 dB / 100ms windows** (LOCKED); per-test override allowed with declared inline reason
- Frame count must match exactly
- Baselines committed under `flow-lang.Tests/baselines/Phase28/` as small reference WAV files (≤ 10 sec each)
- Negative test required: intentional regression (e.g. setting Staccato to 100% duration instead of 25%) must fail with a clear "RMS deviation in window 4 (300-400ms): expected -12.3 dB, got -7.1 dB" diagnostic
- Phase 18/25/27 ByteIdentical tests that pin specific pre-Phase-28 byte values are MIGRATED to RMS-window assertions; the two-run-cmp-clean determinism contract is preserved separately

### Ragtime Fixtures + Manual UAT
- Two test fixtures committed:
  - (a) `examples/tests/ragtime_polyphony.flow` — synthetic 4-bar piece exercising held + running, staccato + sustained pedal, legato across tuplets, mixed-articulation chord
  - (b) `examples/tests/maple_leaf_opening.flow` — first 8 bars of Joplin's "Maple Leaf Rag" (1899, public domain) hand-transcribed, exercising LH stride + RH syncopated melody
- Render-baseline WAVs committed to `flow-lang.Tests/baselines/Phase28/`
- After Wave-N execution, composer renders both fixtures, listens, and signs off in `28-VERIFICATION.md` with explicit "Audibly correct: ✓" line per fixture
- Phase cannot close while either UAT checkbox is unchecked

### Backward Compatibility
- Existing `.flow` scripts that don't use new `voice` block, don't author `leg` token, and don't call `voicePool` continue to render audibly equivalent or audibly improved output
- `legato(Sequence, Double)` transform unchanged
- `examples/tutorial.flow` and `examples/showcase.flow` continue to run to exit 0 with non-empty WAV/MID output
- Phase 18/25/27 two-run determinism tests stay GREEN
- `flow-midi/Midi/MidiParser.cs` is NOT touched (export-side only changes)

### Claude's Discretion (Implementation Details NOT Locked by SPEC)

- Internal data structure for voice-pool (List + priority vs heap vs ring buffer) — planner picks
- Whether `INoteSynthesizer` gains an articulation parameter directly or articulation flows through `MusicalNoteData` — planner picks
- Order of waves within the phase (parser/lexer first vs envelopes first vs MIDI first) — planner picks based on dependency chain analysis
- Internal naming of helper classes (e.g. `SampledInstrumentRenderer` referenced in orchestrator notes is illustrative — pick names that fit the existing `*Renderer` / `*Synthesizer` pattern)
- Test file organization within `flow-lang.Tests/Integration/Phase28/` — planner picks
- Whether to introduce a single `ArticulationEnvelopeShaper` helper or inline per-synth envelope tweaks — planner picks within the constraint that all 9 synths must end up implementing per-articulation envelopes

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 28 SPEC (the source of truth for this phase)
- `.planning/phases/28-midi-audio-polyphony-articulation-rewrite/28-SPEC.md` — 9 falsifiable requirements, locked decisions, ambiguity 0.15

### Project Instructions
- `CLAUDE.md` — project instructions, build/run commands, architecture, conventions, dependencies (DryWetMidi 8.0.3 is the only allowed external dep)
- `.planning/ROADMAP.md` — Phase 28 entry, Phase 27/29 dependencies, v1.4 milestone

### Polyphony / Voice Layer (REWRITTEN by this phase)
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs` — currently walks `bar.ToTimeline()` cumulatively; must support voice-block parallel timelines (lines 54-110); 100ms tied-overlap mitigation at lines 81-86
- `flow-lang/StandardLibrary/Audio/Voice.cs` — Voice value/data type
- `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs` — already 85 lines; gets pool + steal-oldest
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — multi-pass rendering; mixes voices additively (preserve)
- `flow-lang/StandardLibrary/Audio/SequenceRenderer.cs` — sequence rendering layer

### Articulation / Synthesis Layer (REWRITTEN — per-articulation envelopes)
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` — synthesizer dispatch
- `flow-lang/StandardLibrary/Audio/SynthUtils.cs` — shared synth helpers
- `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs` — ADSR envelope shaping
- `flow-lang/StandardLibrary/Audio/Envelope.cs` — envelope value type
- `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs` — currently hard-codes attack 0.003, decay 0.6, sustain 0.12, release 0.3 (lines 51-53)
- `flow-lang/StandardLibrary/Audio/Synthesizers/BrassSynthesizer.cs`
- `flow-lang/StandardLibrary/Audio/Synthesizers/SaxSynthesizer.cs`
- `flow-lang/StandardLibrary/Audio/Synthesizers/DrumSynthesizer.cs` (articulation = no-op for drums)
- `flow-lang/StandardLibrary/Audio/Synthesizers/BellSynthesizer.cs`
- `flow-lang/StandardLibrary/Audio/Synthesizers/FluteSynthesizer.cs`
- `flow-lang/StandardLibrary/Audio/Synthesizers/OrganSynthesizer.cs`
- `flow-lang/StandardLibrary/Audio/Synthesizers/StringsSynthesizer.cs`
- `flow-lang/StandardLibrary/Audio/Synthesizers/WavetableSynthesizer.cs`

### Articulation Type
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` — `Articulation` enum at line 200; ADD `Legato` value here

### Parser / Lexer / Compiler (note-stream + voice block + voicePool block)
- `flow-lang/Parsing/Parser.NoteStream.cs` — `>` = Accent, `stacc` = Staccato, `ten` = Tenuto, `marc` = Marcato (lines 394-417); ADD `leg` token + `{voice ...}` block parsing
- `flow-lang/Runtime/NoteStreamCompiler.cs` — applies +velocity for Accent / Marcato / Sforzando (lines 670-674); ADD per-articulation envelope rule application
- `flow-lang/Lexing/SimpleLexer.cs` — token recognition; may need `voice` / `voicePool` / `leg` keyword handling

### MIDI Export (REWRITTEN — multi-track topology)
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` — currently single Track 1 (lines 246-251); chord-tone stacking at lines 303-312; REWRITE to one track per Sequence

### Existing Test Patterns (model after these)
- `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` — pattern for two-run determinism gates; existing byte-pin assertions migrate to RMS-window where applicable

### Existing Tutorial / Showcase (must continue to render to exit 0)
- `examples/tutorial.flow`
- `examples/showcase.flow`

</canonical_refs>

<specifics>
## Specific Ideas

- Voice-block parser: extend `Parser.NoteStream.cs` to recognize `{voice ...}` token-pair as a parallel-timeline sub-stream
- `Articulation.Legato` enum value insertion at `NoteType.cs:200` (existing enum location)
- `voicePool N { ... }` musical-context block — analogous to existing `tempo`/`timesig` blocks (push/pop on the `MusicalContext` stack)
- Per-articulation envelope shaping at the synthesizer layer — Staccato attack 1.5× synth-default + zero sustain; Tenuto release 1.2× normal; Sforzando spike-then-decay over first 15% of duration
- Multi-track MIDI: each Flow `Sequence` becomes a SMF MIDI track; cross-section same-name sequences concatenate onto the same track preserving repeat ordering
- Steal-oldest voice-pool: when 33rd note onsets in a 32-voice pool, the voice with longest elapsed playtime is truncated; remaining samples replaced with new note's render
- RMS test pattern: `AssertRmsWithinTolerance(rendered, baselineWavPath, windowMs: 100, toleranceDb: 0.5)`
- Phase 28 baseline directory: `flow-lang.Tests/baselines/Phase28/`
- Two ragtime fixtures: `examples/tests/ragtime_polyphony.flow` (synthetic) + `examples/tests/maple_leaf_opening.flow` (first 8 bars of Maple Leaf Rag, 1899, public domain)

</specifics>

<deferred>
## Deferred Ideas

These items are explicitly OUT of scope for Phase 28 (per SPEC `Boundaries -> Out of scope` and `Adjacent problems excluded`):

- **Microtonal MIDI per-channel pitch-bend** — stays deferred per Phase 23 D-13; revisit in v1.5
- **Sampler / sample-based instruments** — Phase 29 (Instrument Realism) territory
- **flow-lsp diagnostics for voice-block syntax errors** — flow-lsp owns its own surface; Phase 28 emits clear interpreter errors; flow-lsp work picks up parser hooks in v1.5+
- **Round-trip MIDI import upgrade** — Phase 28 only changes EXPORT side; flow-midi import polyphony detection stays a v1.5 concern
- **Real-time playback latency optimization** — voice-pool size + steal-oldest are about audio correctness; PulseAudio engine performance untouched
- **Articulation timing humanization** — staccato-slightly-ahead-of-beat etc. is a separate humanize-extension concern
- **Polyrhythm beyond voice-blocks** — full per-voice independent time signatures (e.g. 3/4 LH against 4/4 RH simultaneously) is a v1.5 concern; voice-blocks share the bar's time signature this phase
- **Pre-Phase-28 byte-identical baseline preservation** — tutorial/showcase output legitimately changes bytes
- **Cross-section voice-pool persistence** — held note crossing section boundary; pool resets at section boundary (documented as future work)
- **General DSP improvements** (better reverb, new effects) — Phase 29 / v1.5 concern
- **MIDI 2.0 / per-note expression** — SMF 1.x output only
- **Instrument-specific control surfaces** (pedal CC for piano sustain) — composer authors CC events post-export in DAW

</deferred>

---

*Phase: 28-midi-audio-polyphony-articulation-rewrite*
*Context gathered: 2026-05-10 via PRD Express Path from 28-SPEC.md*
