# Phase 29: Instrument Realism — Specification

**Created:** 2026-05-10
**Ambiguity score:** 0.13
**Requirements:** 8 locked

## Goal

The nine shipping flow-lang synthesizers (Piano, Brass, Sax, Drums, Bell, Flute, Organ, Strings, Wavetable) become noticeably more realistic via a hybrid approach: tonal instruments (Piano, Brass, Sax, Strings, Flute, Bell) gain sample-based playback backed by a curated CC0/Freesound sample library bundled in-repo (≤ 5 MB total) with eager-load on `renderSong` entry; percussion (Drums) and synth-leads (Organ, Wavetable) get hand-rolled DSP improvements. Phase 28's locked articulation envelope rules apply ON TOP of the sample (sample provides timbre, articulation envelope shapes attack/sustain/release). Phase closure is gated by a blind A/B listening test in which the composer correctly identifies Phase 29 output as more realistic than Phase 28 baseline on ≥ 5 of 6 per-instrument fixtures.

## Background

Today (post-Phase 28 baseline — assumes Phase 28 has shipped first per declared dependency):

**Synthesizer surface:**
- All 9 synthesizers in `flow-lang/StandardLibrary/Audio/Synthesizers/` use hand-rolled additive/wavetable synthesis. Total LOC across synths = 705 lines. Largest is `DrumSynthesizer.cs` (164 lines); smallest is `BrassSynthesizer.cs` (40 lines). Each implements `INoteSynthesizer.RenderNote(MusicalNoteData, int sampleRate, double durationBeats, double bpm, RenderTuning)`.
- Realism gap: hand-rolled sine-partial synthesis cannot capture the noise components, formant transitions, and inharmonicity nuances of recorded acoustic instruments. Composers describe the output as "synth-y" or "MIDI-from-1995"; pleasant for tutorial/demo use but not production-realistic.
- Phase 28 lands per-instrument articulation-aware envelopes — but envelopes alone cannot make a sine-based piano sound recorded.

**Sampler primitive:**
- `loadWav(path)` and `loadWav(path, semitones|ratio)` already exist (Phase 22 DX-15, `flow-lang/StandardLibrary/Audio/FileIO.cs:290-314`). Linear-interpolation varispeed pitch-shift is in place. The sampler primitive needed to play recorded samples already ships.

**No bundled sample library:**
- No `assets/samples/`, no `flow-lang/Samples/` directory. Repo currently contains zero binary audio assets. Composers using `loadWav` supply their own samples.

**License/distribution baseline:**
- Repo is pre-public with no legacy users (per `project_pre_public_no_legacy_burden` memory). License decisions can land cleanly without deprecation windows. Sample library license must be public-domain or CC0 to avoid attribution complexity for end users.

**Phase 28 dependency chain:**
- `Articulation.Legato` enum value, voice-block syntax, voice-pool allocation, multi-track MIDI, locked envelope rules per articulation — all assumed shipped by Phase 28 closure before Phase 29 starts.

This phase does NOT change the public language surface (instrument names like `"piano"`/`"brass"`/`"sax"` continue to work identically in `renderSong song "piano"`). The implementation behind those names switches to sample+envelope rendering for tonal instruments and improved synthesis for the rest.

## Requirements

1. **Hybrid sampler + improved-synth split per-instrument**: Six tonal instruments (Piano, Brass, Sax, Strings, Flute, Bell) become sample-based; three non-tonal-or-synth instruments (Drums, Organ, Wavetable) stay synthesizer-based with quality improvements.
   - Current: All 9 instruments use hand-rolled additive synthesis
   - Target: `INoteSynthesizer` implementations for Piano/Brass/Sax/Strings/Flute/Bell delegate to a `SampledInstrumentRenderer` (new class) that selects the closest-pitched sample for the requested note and varispeed-shifts to the exact pitch using the existing `loadWav(path, ratio)` infrastructure. Drums + Organ + Wavetable retain their existing `RenderNote` paths, with hand-rolled improvements applied (richer harmonic content, better envelope shapes, formant-style filtering where applicable)
   - Acceptance: Each of the 6 tonal instruments produces output via the new `SampledInstrumentRenderer` path (verified by an internal counter or test hook); each of the 3 retained-synth instruments produces output via its existing class path with measurable spectral improvement (harmonic-richness ratio increase ≥ 20% vs Phase 28 baseline)

2. **Curated CC0/Freesound sample library bundled in-repo (≤ 5 MB)**: A vetted public-domain or CC0 sample set lands at `flow-lang/Samples/` with per-instrument subdirectories and per-instrument `LICENSE.md` files documenting the source.
   - Current: No bundled samples; no `flow-lang/Samples/` directory
   - Target: `flow-lang/Samples/{piano,brass,sax,strings,flute,bell}/` directories each containing the strategically-pitched samples (e.g. piano: pp + ff layers at C2, C3, C4, C5, C6; brass: single mf layer at A3, A4, A5; etc.). Each subdirectory contains a `LICENSE.md` listing source URL, license type (CC0 / public domain), and attribution if required (CC0 requires no attribution but link-back is courtesy). Total binary size ≤ 5 MB measured via `du -sh flow-lang/Samples/`. Specific sample selection per instrument is locked in `/gsd-discuss-phase 29` based on what's actually available within the size cap
   - Acceptance: `du -sh flow-lang/Samples/` reports ≤ 5 MB; each tonal-instrument subdirectory has at least one `.wav` file + `LICENSE.md`; CI test parses each `LICENSE.md` to verify it has `License:` and `Source:` lines

3. **Two-tier velocity coverage: piano (pp + ff), others single-velocity**: Piano gets two velocity layers (soft + loud) with crossfade between; other tonal instruments use a single mezzo-forte sample with amplitude scaling.
   - Current: No velocity layers (synthesizers scale amplitude only)
   - Target: Piano `SampledInstrumentRenderer` checks note velocity; if velocity ≤ 0.5 it crossfades pp-heavy / ff-light; if > 0.5 it crossfades pp-light / ff-heavy. Crossfade formula: `output = (1 - v_normalized) × pp_sample + v_normalized × ff_sample` where `v_normalized = note.Velocity` clamped to [0, 1]. Other tonal instruments: single mf sample × `note.Velocity` linear amplitude scaling
   - Acceptance: Piano rendering at velocity 0.2 (very soft) vs velocity 0.95 (very loud) produces buffers with measurably different spectral envelopes (cosine similarity < 0.92 — different timbre, not just amplitude). Other tonal instruments: same comparison shows cosine similarity ≥ 0.92 (same timbre, different amplitude)

4. **Eager sample loading on `renderSong` entry**: When `renderSong(song, instrument)` is called, all (instrument, pitch) pairs needed for the song are pre-loaded and cached for the FlowEngine instance lifetime; no first-use latency mid-render.
   - Current: No samples loaded; would otherwise be lazy on first `loadWav` call (which opens file each time)
   - Target: New `SampleCache` singleton (per FlowEngine instance) keyed by `(instrument, midiPitch)`. `renderSong` walks the song's note set, computes the unique (instrument, MIDI pitch) tuples, and loads + caches each before rendering begins. Subsequent `renderSong` calls in the same process reuse the cache. Cache eviction = FlowEngine disposal
   - Acceptance: A test renders a 10-note piece twice in the same FlowEngine instance; the second render's elapsed time is ≥ 30% faster than the first (cache hit on second render). A diagnostic counter or log line confirms zero file-system reads during the second render

5. **Phase 28 articulation envelopes apply on top of sample**: The Phase-28-locked articulation rules (Staccato 25%, Legato 110%+crossfade, Accent +30% velocity, Marcato Staccato+Accent, Tenuto 100%+soft-release, Sforzando +50%-spike) shape the sample's amplitude envelope after sample selection.
   - Current (post-Phase-28): Articulation envelopes apply to synthesizer-rendered buffers only
   - Target: `SampledInstrumentRenderer` invokes the same envelope-shaping logic Phase 28 introduced for synthesizers. Sample provides the natural attack-and-decay shape of the recorded instrument; the articulation envelope multiplies on top to enforce the locked duration/velocity rules. For Staccato (25% audible duration): the sample plays for 25% of authored duration with a fast release applied at the cut-point (smoothing out any audible click)
   - Acceptance: A piano C4q rendered with each of the 6 articulations (Staccato, Tenuto, Legato, Accent, Marcato, Sforzando) under Phase 29 produces 6 distinct buffers. RMS-thresholded audible duration matches Phase 28's locked rules within ±5% per articulation. Plus subjective UAT confirms timbre is now sample-based, not synth-based

6. **Drums / Organ / Wavetable retain synthesis with measurable improvements**: The 3 non-sampler-path instruments get hand-rolled DSP improvements: richer partial sets, better envelope shapes, formant or wavetable upgrades where appropriate.
   - Current: Each uses its existing simple synthesis path
   - Target: Drums — multi-component synthesis (kick = body sine + click transient + body decay; snare = body resonance + noise + tonal layer; hi-hat = filtered noise + transient; rim = pitched click + body); Organ — formant-shaped vowel-like timbres (e.g. "Aaaa" formant set per pipe); Wavetable — adds 2-3 new wavetable types (e.g. "warm" = soft saw, "bright" = pulse train, "buzz" = supersaw stack). Improvements MUST be measurable: harmonic-richness ratio (sum of partial energies above fundamental ÷ fundamental energy) increases ≥ 20% vs Phase 28 baseline for each of the 3 instruments
   - Acceptance: Spectral analysis of each non-sampler instrument shows ≥ 20% increase in harmonic-richness ratio over Phase 28 baseline. Test fixture renders one note per instrument and the test computes harmonic-richness ratio via FFT magnitude spectrum integration

7. **Blind A/B UAT with 6 per-instrument fixtures + statistical pass threshold**: Phase closure runs a composer self-A/B blind test on 6 per-instrument fixtures with randomized labeling and a sealed answer key.
   - Current: No realism-quality test infrastructure
   - Target: 6 fixtures committed at `examples/tests/realism_ab/{piano,brass,sax,strings,flute,drums}.flow` (≤ 2 bars each, 5-10 seconds rendered audio per fixture, focused on the instrument's strongest qualities — piano arpeggio, brass fanfare, sax line, strings sustained chord, flute melody, drums rock fill). Closure script renders each fixture under (a) Phase 28 baseline + (b) Phase 29 output to `examples/output/realism_ab/A_{fixture}.wav` + `B_{fixture}.wav` with randomized A/B mapping per fixture and an `answer_key.txt` listing which letter is Phase 29 per fixture (sealed in a separate commit or git note). Composer listens to all 12 WAVs, writes A or B for each fixture in `29-VERIFICATION.md`, then runs an unseal command that compares answers to the key. Pass = composer correctly identifies Phase 29 on ≥ 5 of 6 fixtures
   - Acceptance: `29-VERIFICATION.md` "Blind A/B Sign-off" section contains 6 composer answers + the unsealed key + a "Correct: N/6" tally where N ≥ 5. Phase cannot close while N < 5

8. **All 5 closure gates pass before phase complete**: Phase 29 closure requires (a) blind A/B pass, (b) repo size delta < 5 MB, (c) per-instrument license docs exist, (d) full unit suite GREEN, (e) per-fixture reflection paragraph written.
   - Current: Closure gates are per-phase ad-hoc; no Phase 29 contract yet
   - Target: `29-VERIFICATION.md` contains 5 explicit gate sections, each with verification command + checkbox:
     - **Gate A** (A/B pass): unsealed answer-key tally ≥ 5/6 correct
     - **Gate B** (size cap): `du -sh flow-lang/Samples/` ≤ 5 MB
     - **Gate C** (license): every `flow-lang/Samples/{instrument}/LICENSE.md` exists with `License:` + `Source:` lines
     - **Gate D** (tests green): `dotnet test flow-lang.Tests --nologo` exits 0
     - **Gate E** (reflection): one-paragraph reflection per fixture (6 paragraphs total) explaining which timbral aspects improved
   - Acceptance: All 5 gates pass; closure commit cannot land while any gate fails. Closure plan's verify block runs each gate command and asserts pass

## Boundaries

**In scope:**
- `flow-lang/Samples/` directory with 6 tonal-instrument subdirectories + per-instrument `LICENSE.md`
- New `SampledInstrumentRenderer` class implementing `INoteSynthesizer` for sampled paths
- New `SampleCache` singleton with eager-load-on-renderSong + per-FlowEngine-instance lifetime
- Modified `PianoSynthesizer` / `BrassSynthesizer` / `SaxSynthesizer` / `StringsSynthesizer` / `FluteSynthesizer` / `BellSynthesizer` to delegate to `SampledInstrumentRenderer`
- Improved `DrumSynthesizer` / `OrganSynthesizer` / `WavetableSynthesizer` (hand-rolled DSP improvements with measurable harmonic-richness gain)
- Two-tier piano velocity layers (pp + ff with crossfade); single-velocity for other tonal
- Eager-load sample resolution on `renderSong` entry
- Phase 28 articulation envelope application on top of sample output
- Six A/B test fixtures at `examples/tests/realism_ab/`
- A/B testing infrastructure (randomized labeling + sealed answer key + unseal command)
- `29-VERIFICATION.md` with 5-gate closure contract
- License audit test (CI parses every sample-LICENSE.md)
- Repo size CI gate (`du -sh flow-lang/Samples/` ≤ 5 MB)

**Out of scope:**
- Lazy-downloaded extended sample library (the "tier-2" library outside the repo) — deferred to v1.5; spec-time decision was 5 MB hard cap, not two-tier in/out
- Per-articulation samples (e.g. `piano-staccato-C4.wav`) — Phase 28 envelopes ON TOP of sustain samples is the locked approach; per-articulation samples deferred to v1.5+
- Three-velocity-layer piano (pp / mf / ff) — Phase 29 ships pp + ff only
- Velocity layers for instruments other than piano — single-velocity only this phase
- Sample format expansion (FLAC, OGG, MP3) — only WAV in this phase; matches existing `loadWav` infrastructure
- Multi-mic / multi-position samples — single-mic samples only
- Round-robin sampling (multiple samples per pitch to avoid machine-gun repetition) — single sample per (pitch, velocity-layer)
- Per-key release samples ("piano sustain pedal release noise") — deferred to v1.5
- Sample-format compression / streaming — bundled samples are uncompressed WAV; eager-loaded into memory
- New instruments — Phase 29 stays at the existing 9; new instruments are a separate v1.5 concern
- User-facing API changes — instrument names (`"piano"`, `"brass"`, etc.) and `renderSong` signature unchanged
- Compile-time sample resolution / build-time check — eager-load-at-renderSong is the locked policy
- Per-FlowEngine-instance cache invalidation triggers (file-watcher on samples) — cache reload requires FlowEngine restart this phase
- External listener panel UAT — composer self-A/B is the locked mechanism
- FFT spectral comparison vs reference recordings — manual UAT only is the locked metric
- Sample-pack distribution as a separate package — bundled in-repo this phase

**Adjacent problems excluded:**
- Vocaloid voice synthesis improvements — separate Vocalization stack; not part of Phase 29
- Real-time audio playback latency vs. file-write performance — cache load happens on `renderSong`, not playback
- Sample-rate conversion for samples recorded at non-44.1 kHz — bundled samples must all be 44.1 kHz mono or stereo to match `AudioBuffer` defaults; rate-mismatch handling deferred
- DAW-importable sample formats (SFZ, EXS24, Kontakt) — only raw WAV samples; format converters deferred
- AI-generated samples (e.g. neural synth output) — using only recorded acoustic samples this phase

## Constraints

- **Repo size cap: 5 MB** for `flow-lang/Samples/` (hard limit, CI-enforced via `du -sh`). Sample selection during plan-phase must fit within this cap. If a candidate library cannot fit, drop pitch coverage (use sparser pitch sampling + more varispeed reach) before exceeding the cap.
- **License: CC0 OR CC-BY (with attribution).** Originally CC0-only; relaxed 2026-05-11 after the composer surfaced that CC0 pitch+dynamic-specific samples are scarce (Freesound CC0 results are predominantly full tunes, not isolated pitches at named velocities). Each `LICENSE.md` must declare the license type explicitly (`License: CC0`, `License: CC-BY 3.0`, etc.) with source URL. CC-BY samples additionally require an `Attribution:` line naming the original creator; a bundle-wide `flow-lang/Samples/CREDITS.md` aggregates attributions so end users see one consolidated credit list. CC-BY-SA and CC-BY-NC remain excluded (share-alike and non-commercial both create downstream complications).
- **Sample format: 44.1 kHz / 16-bit PCM mono WAV** (matching `AudioBuffer` defaults and `loadWav` infrastructure). Stereo samples are converted to mono on bundle (lossless mix-to-mono). Other rates / depths converted at bundle time.
- **Pitch coverage strategy**: Piano = 5 pitches at C2/C3/C4/C5/C6 (one octave intervals; varispeed reach ≤ ±6 semitones from nearest sample → cleanest pitch shift quality). Other tonal instruments = 2-3 pitches each at strategically chosen octaves (e.g. brass: A3 + A4 + A5; sax: F4 + C5; strings: D3 + D4 + D5; flute: G4 + G5; bell: C5). Exact pitches locked in `/gsd-discuss-phase 29` based on actual sample availability.
- **Determinism**: Phase 29's runtime must be deterministic — running the same script twice produces byte-identical output (Phase 28's two-run gate continues to apply). Sample-load order, cache hits, varispeed math must all be deterministic.
- **No new external dependencies**: Sample loading uses existing `loadWav` infrastructure. No new NuGet packages, no new build-step plugins, no audio decoders beyond what `FileIO.cs` already supports.
- **Eager-load-on-renderSong policy**: First call to `renderSong` in a FlowEngine instance loads all samples needed for that song. Subsequent `renderSong` calls reuse cache. No file I/O during render proper.
- **Phase 28 dependency**: Phase 29 cannot start before Phase 28 closure (Articulation.Legato enum, voice-blocks, multi-track MIDI, locked envelopes must exist). If Phase 28 ships partially or with defects, Phase 29 plan-phase blocks until Phase 28 is GREEN.
- **Test runtime budget**: Sample-cache load test + varispeed accuracy test + articulation-on-sample test must run within 60 seconds total. Use small in-test sample assets (≤ 100 KB each) for unit-level coverage; full-bundle samples are only loaded in integration tests.
- **Closure: all 5 gates required**. No "force close" exemption — gates are belt-and-suspenders.

## Acceptance Criteria

- [ ] `flow-lang/Samples/` directory exists with 6 tonal-instrument subdirectories (piano, brass, sax, strings, flute, bell)
- [ ] `du -sh flow-lang/Samples/` reports ≤ 5 MB
- [ ] Each tonal-instrument subdirectory contains at least one `.wav` file plus a `LICENSE.md` with `License:` and `Source:` lines (CC0 OR CC-BY); CC-BY entries additionally have an `Attribution:` line; a bundle-wide `flow-lang/Samples/CREDITS.md` aggregates all attributions
- [ ] `SampledInstrumentRenderer` class implementing `INoteSynthesizer` exists and is invoked by Piano/Brass/Sax/Strings/Flute/Bell synthesizers
- [ ] `SampleCache` singleton with per-FlowEngine-instance lifetime exists at `flow-lang/StandardLibrary/Audio/SampleCache.cs`
- [ ] Eager sample-resolution test: rendering a 10-note piece twice in the same FlowEngine instance shows ≥ 30% time speedup on the second render (cache hit)
- [ ] Piano velocity test: rendering C4q at velocity 0.2 vs 0.95 produces buffers with cosine spectral similarity < 0.92 (different timbre)
- [ ] Other tonal instruments velocity test: rendering at 0.2 vs 0.95 shows cosine similarity ≥ 0.92 (same timbre, different amplitude)
- [ ] Articulation × sample test: piano C4q rendered under each of 6 articulations produces 6 distinct buffers; audible duration matches Phase 28 locked rules within ±5%
- [ ] Drums / Organ / Wavetable harmonic-richness test: each of these 3 instruments shows ≥ 20% harmonic-richness-ratio increase vs Phase 28 baseline
- [ ] Six A/B test fixtures exist at `examples/tests/realism_ab/{piano,brass,sax,strings,flute,drums}.flow`
- [ ] A/B closure script renders each fixture twice (Phase 28 baseline + Phase 29) with randomized labeling + sealed answer key
- [ ] Composer A/B test: ≥ 5 of 6 fixtures correctly identified as Phase 29 in `29-VERIFICATION.md` "Blind A/B Sign-off"
- [ ] License audit test passes (CI parses every `flow-lang/Samples/{instrument}/LICENSE.md` and verifies required fields)
- [ ] `dotnet test flow-lang.Tests --nologo` exits 0 — full unit suite GREEN
- [ ] Six composer reflection paragraphs exist in `29-VERIFICATION.md`, one per A/B fixture
- [ ] All 5 closure gates checked in `29-VERIFICATION.md` (A/B + size + license + tests + reflection)
- [ ] Existing flow scripts (tutorial.flow, showcase.flow, every test_*.flow) continue to render without API errors (instrument names + signatures unchanged)
- [ ] Two-run determinism: rendering any test fixture twice in the same git SHA produces byte-identical output

## Ambiguity Report

| Dimension          | Score | Min  | Status | Notes                                                                                          |
|--------------------|-------|------|--------|------------------------------------------------------------------------------------------------|
| Goal Clarity       | 0.92  | 0.75 | ✓      | Hybrid sampler + improved-synth split locked; per-instrument coverage all 9 in scope          |
| Boundary Clarity   | 0.80  | 0.70 | ✓      | 5 MB cap, CC0 only, no extended library, no per-articulation samples — explicit perimeter      |
| Constraint Clarity | 0.88  | 0.65 | ✓      | Hard size cap; license type locked; sample format/rate/depth pinned; eager-load policy fixed   |
| Acceptance Criteria| 0.88  | 0.70 | ✓      | A/B threshold 5/6 correct; 5 closure gates; 17 falsifiable checkboxes                          |
| **Ambiguity**      | 0.13  | ≤0.20| ✓      | Gate passed                                                                                    |

## Interview Log

| Round | Perspective       | Question summary                                                                | Decision locked                                                                              |
|-------|-------------------|---------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------|
| 1     | Researcher        | Realism approach — sampler, improved synth, hybrid?                              | Hybrid: sampler for tonal (Piano/Brass/Sax/Strings/Flute/Bell), improved synth for non-tonal |
| 1     | Researcher        | Sample source — bundled, lazy-downloaded, user-supplied?                         | Two-tier collapsed to 5 MB built-in only (per Round 2 size cap); extended library deferred  |
| 1     | Researcher        | Coverage — all 9 instruments or subset?                                          | All 9 get realism pass (samples for 6 tonal; improved synth for 3)                          |
| 2     | Simplifier        | How do we measure "realistic enough" — objective threshold?                      | Manual UAT only; blind A/B against Phase 28 baseline                                        |
| 2     | Simplifier        | Built-in sample bundle size cap — hard limit?                                   | 5 MB hard cap (CI-enforced via `du -sh`)                                                    |
| 2     | Simplifier        | Sample library source — which?                                                  | Curated CC0/Freesound mix per instrument; per-instrument LICENSE.md required                |
| 3     | Boundary Keeper   | Multi-velocity sampling — ship per-velocity layers?                              | Two-tier: piano (pp + ff) only; other tonal single-velocity                                 |
| 3     | Boundary Keeper   | Sample loading strategy — startup, lazy, or eager-on-renderSong?                 | Eager load on `renderSong` entry; per-FlowEngine-instance cache                             |
| 3     | Boundary Keeper   | Articulation × sampler interaction?                                              | Phase 28 articulation envelopes apply ON TOP of sample (sample = timbre; envelope = shape)  |
| 4     | Failure Analyst   | Blind A/B test mechanism — who listens, how?                                     | Composer self-A/B with randomized labeling; threshold ≥ 5 of 6 correct                     |
| 4     | Failure Analyst   | UAT test fixtures — how many, which?                                             | Six per-instrument fixtures (piano, brass, sax, strings, flute, drums)                      |
| 4     | Failure Analyst   | Closure prerequisites — what blocks closure?                                     | All 5 gates: A/B pass + size + license + tests + reflection                                 |

---

*Phase: 29-instrument-realism*
*Spec created: 2026-05-10*
*Next step: /gsd-discuss-phase 29 — implementation decisions (specific sample selection, SampledInstrumentRenderer architecture, varispeed quality tuning, per-instrument synthesis improvement details)*
