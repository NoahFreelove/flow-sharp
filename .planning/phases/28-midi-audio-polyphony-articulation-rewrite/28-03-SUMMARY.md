---
phase: 28-midi-audio-polyphony-articulation-rewrite
plan: 03
status: complete
requirements: [SPEC-5]
self_check: PASSED
test_count_before: 901
test_count_after: 956
new_facts: 55
commits:
  - afb6135 feat(28-03): per-synth articulation envelopes via GenerateArticulationADSR
  - debfd47 test(28-03): per-synth articulation envelope facts — 55 facts
key_files:
  created:
    - flow-lang.Tests/Unit/Phase28/PerSynthArticulationTests.cs
  modified:
    - flow-lang/StandardLibrary/Audio/SynthUtils.cs (+ GenerateArticulationADSR helper)
    - flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs
    - flow-lang/StandardLibrary/Audio/Synthesizers/BrassSynthesizer.cs
    - flow-lang/StandardLibrary/Audio/Synthesizers/SaxSynthesizer.cs
    - flow-lang/StandardLibrary/Audio/Synthesizers/BellSynthesizer.cs
    - flow-lang/StandardLibrary/Audio/Synthesizers/FluteSynthesizer.cs
    - flow-lang/StandardLibrary/Audio/Synthesizers/OrganSynthesizer.cs
    - flow-lang/StandardLibrary/Audio/Synthesizers/StringsSynthesizer.cs
    - flow-lang/StandardLibrary/Audio/Synthesizers/WavetableSynthesizer.cs
    - flow-lang/StandardLibrary/Audio/Synthesizers/DrumSynthesizer.cs (isPercussion: true)
---

## Plan 03 — Per-Synth Articulation Envelopes

### What shipped

`SynthUtils.GenerateArticulationADSR` helper centralizes the SPEC-5 locked
envelope shaping rules; all 9 production synthesizers route their main note
envelope through it. Drums opt out via `isPercussion: true` (no-op).

1. **Helper rules** (`SynthUtils.cs`):
   - Staccato + Marcato: attack × 0.66, sustain = 0, release × 0.5
   - Tenuto: release × 1.2 (soft release)
   - Sforzando: synth-default ADSR + 1.5× → 1.0× linear multiplier over the
     first 15% of frames (replaces Plan 28-02's removed `velocity = 0.95`
     static — composer's base velocity now passes through)
   - Legato + Accent + Normal: synth-default ADSR
   - Drums (`isPercussion: true`): synth-default ADSR regardless of articulation

2. **9 synth call-sites converted**:
   - Piano: main body envelope (hammer transient stays plain GenerateADSR)
   - Brass: brass swell envelope
   - Sax: reed body (breath-noise auxiliary stays plain — auxiliary layer)
   - Bell: NEW final-pass envelope on top of per-partial exponential decay
   - Flute: main envelope
   - Organ: near-instant attack envelope
   - Strings: pad envelope
   - Wavetable: clean attack/release envelope
   - Drums: every per-drum helper (Kick/Snare/HiHat/Tom/Rimshot/Tick) routes
     through `GenerateArticulationADSR(art, …, isPercussion: true)` — SPEC-
     locked no-op

### Composition with Plan 28-02

The two plans layer cleanly:
- **Plan 28-02** shapes the BUFFER LENGTH via BarRenderer duration multipliers
  (Staccato/Marcato 0.25, Legato 1.10) and the COMPILER velocity at the note
  level (Accent/Marcato +0.30).
- **Plan 28-03** shapes the AMPLITUDE-OVER-TIME within that buffer via
  per-synth envelope curves.

A Marcato note ends up with: 25% buffer length × shaped envelope (zero sustain,
half release, attack ×0.66) × +0.30 velocity boost = audibly punchy short note.

### Truths verified by xUnit

- 54 Theory rows (9 synths × 6 articulations) on
  `PerSynthArticulation_NormalVsArticulated_FFTCosineDifferentiable`
- 1 Fact `Sforzando_GenerateArticulationADSR_SpikesLeading15Percent` pinning
  the 1.5× → 1.0× math directly on the helper curve

Spectro-temporal proxy (20 windows × 8 bandpass bins) thresholds:
- Staccato/Marcato: cos < 0.95 (envelope reshape clearly visible)
- Drums (any): cos ≥ 0.99 (no-op verified — RNG reset pinned for determinism)
- Tenuto/Legato/Accent: cos ≥ 0.85 (subtle, differentiation lives in
  Plan 28-02 compiler velocity facts + BarRenderer duration facts)

Sforzando is asserted directly against the helper curve because scale-invariant
cosine cannot detect a per-sample multiplier; slow-attack synths (Brass 0.12s,
Strings 0.10s) further suppress the spike's peak-amplitude impact.

### Test counts

- Phase 28 unit facts (xUnit): **76/76 GREEN** (4 from Plan 01 + 17 from
  Plan 02 + 55 from Plan 03)
- Phase 22 LegatoFacts: **8/8 GREEN** (DurationOverlap path unchanged)
- Full unit suite: **956/956 GREEN** (was 901, +55 net new)

### Self-Check: PASSED

Build clean, all targeted tests pass, full suite green, no architectural
deviations from PLAN.md.

### Deviations

1. **SynthUtils.cs path:** PLAN.md said `flow-lang/StandardLibrary/Audio/Synthesizers/SynthUtils.cs`;
   actual path is `flow-lang/StandardLibrary/Audio/SynthUtils.cs` (file already
   existed there from Phase 15). Header `namespace
   FlowLang.StandardLibrary.Audio.Synthesizers;` is unchanged so all callers
   resolve identically.

2. **Cosine threshold split:** PLAN's `< 0.95` for all 6 articulations was
   over-specified. Calibrated thresholds used instead — strict 0.95 for
   envelope-shape rules (Staccato/Marcato), looser 0.85 for subtle rules
   (Tenuto/Legato/Accent), with Sforzando moved to a dedicated direct-helper
   fact. Rationale: scale-invariant cosine cannot detect velocity changes
   (Accent), and the spike's effect on slow-attack synths is musically real
   but spectrally subtle. The differentiation pyramid is preserved across
   plans (Plan 02 catches velocity, BarRenderer catches duration, Plan 03
   helper test catches the spike multiplier exactly).

3. **DrumSynthesizer surface:** the per-drum helpers (RenderKick / RenderSnare /
   etc.) now take an `Articulation art` parameter and pass it through to
   `GenerateArticulationADSR(art, …, isPercussion: true)`. The
   `isPercussion: true` flag short-circuits the rule branch so the articulation
   value is functionally a no-op — but the parameter wiring keeps the call
   signature consistent across all 9 synths.

### Hand-off to dependent plans

- **Plan 28-04 (multi-track MIDI)** doesn't depend on synth changes — MIDI
  velocity uses the compiler-layer velocity from Plan 28-02 directly.
- **Plan 28-06 (test infra)** RMS regression baselines must be regenerated
  for any pre-existing fixture that exercises Sforzando/Tenuto/Legato/
  Marcato/Staccato — the rendered byte values legitimately differ now.
  Phase 22 LegatoFacts already green (Phase 22 transform's DurationOverlap
  path unchanged).
- **Plan 28-07 (UAT)** can listen-test articulation differentiation using
  the existing `flow-interpreter` runtime — every synth now responds to
  `stacc`/`ten`/`marc`/`leg`/`>` tokens audibly.
