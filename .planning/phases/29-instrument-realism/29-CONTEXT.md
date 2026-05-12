# Phase 29: Instrument Realism - Context

**Gathered:** 2026-05-10
**Status:** Ready for planning
**Source:** PRD Express Path (29-SPEC.md)

<domain>
## Phase Boundary

Phase 29 makes the nine shipping flow-lang synthesizers (Piano, Brass, Sax, Drums, Bell, Flute, Organ, Strings, Wavetable) noticeably more realistic via a hybrid approach:

- **Six tonal instruments** (Piano, Brass, Sax, Strings, Flute, Bell) gain sample-based playback backed by a curated CC0/Freesound sample library bundled in-repo (≤ 5 MB) with eager-load on `renderSong` entry.
- **Three percussion / synth-lead instruments** (Drums, Organ, Wavetable) get hand-rolled DSP improvements — richer partials, better envelope shapes, formant filters, more wavetable variants.
- **Phase 28's locked articulation envelope rules apply ON TOP of the sample**: sample provides timbre, articulation envelope shapes attack/sustain/release.
- **Phase closure is gated by a blind A/B listening test**: composer correctly identifies Phase 29 output as more realistic than Phase 28 baseline on ≥ 5 of 6 per-instrument fixtures.

The public language surface is unchanged — instrument names (`"piano"`, `"brass"`, etc.) and `renderSong` signature remain identical. Only the internal rendering path switches per-instrument.

</domain>

<decisions>
## Implementation Decisions

Every decision below is **locked from 29-SPEC.md**. Do not re-litigate during planning.

### Architecture: Hybrid Sampler + Improved Synth Split (REQ-1)
- **D-01 [hybrid-split]** Six tonal instruments (Piano, Brass, Sax, Strings, Flute, Bell) become sample-based via a new `SampledInstrumentRenderer` class implementing `INoteSynthesizer`.
- **D-02 [retain-synth]** Three instruments (Drums, Organ, Wavetable) retain their existing class paths with hand-rolled DSP improvements (≥ 20% harmonic-richness gain vs Phase 28 baseline).
- **D-03 [delegation]** Modified `PianoSynthesizer` / `BrassSynthesizer` / `SaxSynthesizer` / `StringsSynthesizer` / `FluteSynthesizer` / `BellSynthesizer` delegate to `SampledInstrumentRenderer`.
- **D-04 [varispeed]** `SampledInstrumentRenderer` uses existing `loadWav(path, ratio)` infrastructure (Phase 22 DX-15 in `flow-lang/StandardLibrary/Audio/FileIO.cs:290-314`) for linear-interpolation pitch shift.

### Sample Library (REQ-2)
- **D-05 [bundle-path]** Curated public-domain / CC0 sample library at `flow-lang/Samples/{piano,brass,sax,strings,flute,bell}/`.
- **D-06 [size-cap]** Hard 5 MB total cap on `flow-lang/Samples/`. Measured via `du -sh`. CI-enforced.
- **D-07 [license]** **CC0 or equivalent public-domain ONLY**. CC-BY (attribution required) is NOT acceptable for the bundled set. Each instrument subdir has a `LICENSE.md` with `License:` and `Source:` lines.
- **D-08 [format]** 44.1 kHz / 16-bit PCM mono WAV. Stereo samples mixed to mono on bundle. Other rates / depths converted at bundle time.
- **D-09 [pitch-coverage]** Piano: 5 pitches at C2/C3/C4/C5/C6 (one octave intervals). Other tonal: 2-3 pitches each (e.g. brass A3/A4/A5; sax F4/C5; strings D3/D4/D5; flute G4/G5; bell C5). Varispeed reach ≤ ±6 semitones from nearest sample for cleanest pitch-shift quality. Exact pitches subject to actual sample availability — research locks them.

### Velocity Layers (REQ-3)
- **D-10 [piano-velocity]** Piano gets two velocity layers: pp (soft) + ff (loud). Crossfade formula: `output = (1 - v_normalized) × pp_sample + v_normalized × ff_sample` where `v_normalized = note.Velocity` clamped to [0, 1].
- **D-11 [other-velocity]** Other tonal instruments use a single mezzo-forte sample with linear amplitude scaling by `note.Velocity`.
- **D-12 [no-three-tier]** Three-velocity-layer piano (pp / mf / ff) explicitly DEFERRED to v1.5+.

### Sample Loading (REQ-4)
- **D-13 [eager-load]** New `SampleCache` singleton (per FlowEngine instance) keyed by `(instrument, midiPitch)`.
- **D-14 [renderSong-entry]** `renderSong(song, instrument)` walks the song's note set, computes unique (instrument, MIDI pitch) tuples, and loads + caches each before rendering begins. Zero file-system reads during render proper.
- **D-15 [cache-lifetime]** Cache eviction = FlowEngine disposal. Subsequent `renderSong` calls in same process reuse cache.
- **D-16 [cache-location]** New file: `flow-lang/StandardLibrary/Audio/SampleCache.cs`.

### Articulation × Sample (REQ-5)
- **D-17 [envelopes-on-top]** Phase 28's locked articulation rules (Staccato 25%, Legato 110%+crossfade, Accent +30% velocity, Marcato Staccato+Accent, Tenuto 100%+soft-release, Sforzando +50%-spike) shape the sample's amplitude envelope after sample selection.
- **D-18 [staccato-cut]** For Staccato (25% audible duration): the sample plays for 25% of authored duration with a fast release applied at the cut-point (smoothing out any audible click).
- **D-19 [reuse-phase28]** `SampledInstrumentRenderer` invokes the same envelope-shaping logic Phase 28 introduced for synthesizers — do not duplicate envelope logic.

### Drums / Organ / Wavetable Improvements (REQ-6)
- **D-20 [drums-multicomponent]** Drums: multi-component synthesis (kick = body sine + click transient + body decay; snare = body resonance + noise + tonal layer; hi-hat = filtered noise + transient; rim = pitched click + body).
- **D-21 [organ-formants]** Organ: formant-shaped vowel-like timbres (e.g. "Aaaa" formant set per pipe).
- **D-22 [wavetable-variants]** Wavetable: 2-3 new wavetable types (e.g. "warm" = soft saw, "bright" = pulse train, "buzz" = supersaw stack).
- **D-23 [richness-threshold]** Each must show ≥ 20% harmonic-richness ratio (sum of partial energies above fundamental ÷ fundamental energy) increase vs Phase 28 baseline.

### Blind A/B UAT (REQ-7)
- **D-24 [fixture-count]** Six A/B fixtures at `examples/tests/realism_ab/{piano,brass,sax,strings,flute,drums}.flow`. ≤ 2 bars each, 5-10 seconds rendered audio per fixture. Focused on instrument's strongest qualities (piano arpeggio, brass fanfare, sax line, strings sustained chord, flute melody, drums rock fill).
- **D-25 [randomized-labeling]** Closure script renders each fixture under (a) Phase 28 baseline + (b) Phase 29 to `examples/output/realism_ab/A_{fixture}.wav` + `B_{fixture}.wav` with **randomized A/B mapping per fixture**.
- **D-26 [sealed-key]** `answer_key.txt` listing which letter is Phase 29 per fixture, sealed in a separate commit or git note.
- **D-27 [pass-threshold]** Composer correctly identifies Phase 29 on ≥ 5 of 6 fixtures.
- **D-28 [no-external-listeners]** External listener panel UAT explicitly EXCLUDED — composer self-A/B is the locked mechanism.

### Closure Gates (REQ-8)
- **D-29 [five-gates]** All 5 closure gates required (no force-close exemption):
  - Gate A (A/B): unsealed answer-key tally ≥ 5/6 correct
  - Gate B (size): `du -sh flow-lang/Samples/` ≤ 5 MB
  - Gate C (license): every `flow-lang/Samples/{instrument}/LICENSE.md` exists with `License:` + `Source:` lines
  - Gate D (tests): `dotnet test flow-lang.Tests --nologo` exits 0
  - Gate E (reflection): one paragraph per fixture (6 paragraphs) explaining which timbral aspects improved
- **D-30 [verification-doc]** All 5 gates checked in `29-VERIFICATION.md`. Closure plan's verify block runs each gate command and asserts pass.

### Determinism Continuity
- **D-31 [two-run-determinism]** Phase 29's runtime must be deterministic — running the same script twice produces byte-identical output (Phase 28's two-run gate continues to apply). Sample-load order, cache hits, varispeed math must all be deterministic.

### No External Dependencies
- **D-32 [no-new-deps]** Sample loading uses existing `loadWav` infrastructure. No new NuGet packages, no new build-step plugins, no audio decoders beyond what `FileIO.cs` already supports.

### Phase 28 Dependency
- **D-33 [phase28-dep]** Phase 29 cannot start before Phase 28 closure (Articulation.Legato enum, voice-blocks, multi-track MIDI, locked envelopes must exist). Phase 29 PLAN-PHASE may run in parallel with Phase 28 plan-phase, but execute-phase 29 blocks until Phase 28 is GREEN.

### Test Runtime Budget
- **D-34 [test-budget]** Sample-cache load test + varispeed accuracy test + articulation-on-sample test must run within 60 seconds total. Use small in-test sample assets (≤ 100 KB each) for unit-level coverage; full-bundle samples are only loaded in integration tests.

### Public API Stability
- **D-35 [api-unchanged]** Instrument names (`"piano"`, `"brass"`, etc.) and `renderSong` signature unchanged. Existing flow scripts (tutorial.flow, showcase.flow, every test_*.flow) continue to render without API errors.

### Claude's Discretion
The PRD covers WHAT must ship. The following implementation details are at planner / executor discretion:
- Exact internal API surface of `SampledInstrumentRenderer` (constructor signature, method visibility, internal helpers)
- Exact file layout inside `SampleCache.cs` (key struct, dictionary type, lock strategy)
- Specific FFT library / approach for harmonic-richness measurement (NOT a new dependency — must use hand-rolled DFT or existing math)
- Per-instrument harmonic-richness baseline measurement methodology (must be deterministic and reproducible, but exact algorithm is open)
- Concrete sample selection — research must identify specific CC0 sources (Freesound IDs, Internet Archive URLs, openpianosamples.com CC0 sets, etc.) within the 5 MB cap; planner may iterate based on available bundle size
- Crossfade interpolation between pp + ff samples (linear is the SPEC default — sigmoid or other curves may be substituted if planner finds linear sounds wrong)
- A/B test fixture musical content (specific notes / chords / rhythms) — must showcase the instrument well
- Reflection paragraph wording (composer's call)
- License audit test implementation (Phase29-LicenseAuditTests pattern — must reuse existing test conventions)
- Repo size CI gate implementation (could be a unit test or a build script — planner picks)
- Whether to ship per-instrument FFT spectral envelope baseline as a checked-in fixture or compute on the fly during tests

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 29 Specification (PRIMARY)
- `.planning/phases/29-instrument-realism/29-SPEC.md` — Locked phase contract. All 8 requirements + boundaries + constraints + acceptance criteria.

### Cross-Phase Dependency
- `.planning/phases/28-midi-audio-polyphony-articulation-rewrite/28-SPEC.md` — Phase 28 locked articulation envelope rules. Phase 29 builds ON TOP of these envelopes. Read before planning the SampledInstrumentRenderer envelope-application logic.

### Project-Level Context
- `CLAUDE.md` — Project guidelines (Flow's goals, non-goals, build commands, architecture, conventions, dependency policy)
- `.planning/ROADMAP.md` — v1.4 milestone, Phase 29 entry, dependency on Phase 28

### Existing Code (Phase 29 modifies / extends these)
- `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs` — current piano synth (will delegate to SampledInstrumentRenderer)
- `flow-lang/StandardLibrary/Audio/Synthesizers/BrassSynthesizer.cs` — current brass synth (will delegate to SampledInstrumentRenderer)
- `flow-lang/StandardLibrary/Audio/Synthesizers/SaxSynthesizer.cs` — current sax synth (will delegate to SampledInstrumentRenderer)
- `flow-lang/StandardLibrary/Audio/Synthesizers/StringsSynthesizer.cs` — current strings synth (will delegate to SampledInstrumentRenderer)
- `flow-lang/StandardLibrary/Audio/Synthesizers/FluteSynthesizer.cs` — current flute synth (will delegate to SampledInstrumentRenderer)
- `flow-lang/StandardLibrary/Audio/Synthesizers/BellSynthesizer.cs` — current bell synth (will delegate to SampledInstrumentRenderer)
- `flow-lang/StandardLibrary/Audio/Synthesizers/DrumSynthesizer.cs` — current drum synth (will get multi-component upgrade)
- `flow-lang/StandardLibrary/Audio/Synthesizers/OrganSynthesizer.cs` — current organ synth (will get formant upgrade)
- `flow-lang/StandardLibrary/Audio/Synthesizers/WavetableSynthesizer.cs` — current wavetable synth (will get 2-3 new variants)
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` — INoteSynthesizer interface contract (SampledInstrumentRenderer must implement this)
- `flow-lang/StandardLibrary/Audio/FileIO.cs` — `loadWav(path)` and `loadWav(path, semitones|ratio)` (Phase 22 DX-15, lines 290-314). The varispeed primitive Phase 29 builds on.
- `flow-lang/StandardLibrary/Audio/SynthUtils.cs` — shared synthesizer helpers
- `flow-lang/StandardLibrary/Audio/SequenceRenderer.cs` — renders sequences through synthesizers
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — `renderSong(song, instrument)` entry point — Phase 29 hooks `SampleCache` eager-load here
- `flow-lang/audio.flow` — `loadWav` forward declarations + audio convenience functions

### Test Patterns
- `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` — two-run determinism test pattern. Phase 29's two-run determinism gate uses this pattern.

### Memory / Project Decisions
- `project_pre_public_no_legacy_burden` (memory) — pre-public; license decisions can land cleanly without deprecation windows
- `feedback_charitable_interpretation` (memory) — prefer silent-and-documented assumptions over errors; music > rigid correctness
- `feedback_ergonomics_priority` (memory) — pick the lower-friction option for composers even when it costs implementation complexity

</canonical_refs>

<specifics>
## Specific Ideas

### Sample Library Sourcing (research must concretize)
Locked-by-spec sources to investigate:
- **Freesound.org** — search filter `license:"Creative Commons 0"` for CC0-only results
- **openpianosamples.com** — public-domain piano sample sets (look for CC0 alternatives to Salamander, which is CC-BY)
- **archive.org** — public-domain audio collection, especially "Free Music Archive" and government-released recordings
- **University of Iowa Electronic Music Studios** — public-domain orchestral samples (verify CC0 status per file)

### Pitch Coverage Per SPEC
- Piano: C2, C3, C4, C5, C6 (5 pitches × 2 velocity layers = 10 samples)
- Brass: A3, A4, A5 (3 pitches × 1 velocity = 3 samples)
- Sax: F4, C5 (2 pitches × 1 velocity = 2 samples)
- Strings: D3, D4, D5 (3 pitches × 1 velocity = 3 samples)
- Flute: G4, G5 (2 pitches × 1 velocity = 2 samples)
- Bell: C5 (1 pitch × 1 velocity = 1 sample)
- **Total: 21 samples**. At 44.1 kHz / 16-bit / mono / 1-2 sec each → ~88-176 KB per sample → ~2-4 MB total. Comfortable within 5 MB cap.

### Closure Artifacts
- `examples/tests/realism_ab/{piano,brass,sax,strings,flute,drums}.flow` (6 fixtures)
- `examples/output/realism_ab/A_{fixture}.wav` + `B_{fixture}.wav` (12 outputs after closure script)
- `examples/output/realism_ab/answer_key.txt` (sealed in separate commit / git note)
- `29-VERIFICATION.md` with 5-gate sections + "Blind A/B Sign-off" subsection + 6 reflection paragraphs

### New Code Surface (target file list)
- `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs` (NEW)
- `flow-lang/StandardLibrary/Audio/SampleCache.cs` (NEW)
- `flow-lang/Samples/piano/`, `flow-lang/Samples/brass/`, `flow-lang/Samples/sax/`, `flow-lang/Samples/strings/`, `flow-lang/Samples/flute/`, `flow-lang/Samples/bell/` (NEW directories with WAVs + LICENSE.md)
- Modified: 6 tonal synth classes + Drums + Organ + Wavetable + SongRenderer (eager-load hook)
- New tests: `Phase29-LicenseAuditTests`, `Phase29-VelocityLayerTests`, `Phase29-SampleCacheTests`, `Phase29-ArticulationOnSampleTests`, `Phase29-HarmonicRichnessTests`, `Phase29-RepoSizeTests`, two-run determinism extension

</specifics>

<deferred>
## Deferred Ideas

Locked OUT-OF-SCOPE per SPEC; do not plan:
- Lazy-downloaded extended sample library (the "tier-2" library outside the repo) — DEFERRED to v1.5
- Per-articulation samples (e.g. `piano-staccato-C4.wav`) — DEFERRED to v1.5+
- Three-velocity-layer piano (pp / mf / ff) — Phase 29 ships pp + ff only
- Velocity layers for instruments other than piano — single-velocity only this phase
- Sample format expansion (FLAC, OGG, MP3) — only WAV
- Multi-mic / multi-position samples — single-mic only
- Round-robin sampling (multiple samples per pitch) — single sample per (pitch, velocity-layer)
- Per-key release samples ("piano sustain pedal release noise") — DEFERRED to v1.5
- Sample-format compression / streaming — bundled samples are uncompressed WAV
- New instruments — Phase 29 stays at the existing 9
- User-facing API changes — instrument names + `renderSong` signature unchanged
- Compile-time sample resolution / build-time check — eager-load-at-renderSong is the locked policy
- Per-FlowEngine-instance cache invalidation triggers (file-watcher on samples) — cache reload requires FlowEngine restart this phase
- External listener panel UAT — composer self-A/B is the locked mechanism
- FFT spectral comparison vs reference recordings — manual UAT only is the locked metric
- Sample-pack distribution as a separate package — bundled in-repo this phase
- Vocaloid voice synthesis improvements — separate Vocalization stack
- DAW-importable sample formats (SFZ, EXS24, Kontakt) — only raw WAV
- AI-generated samples (e.g. neural synth output) — recorded acoustic samples only

</deferred>

---

*Phase: 29-instrument-realism*
*Context gathered: 2026-05-10 via PRD Express Path from 29-SPEC.md*
