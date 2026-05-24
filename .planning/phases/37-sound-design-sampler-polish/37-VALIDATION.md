---
phase: 37
slug: sound-design-sampler-polish
status: closed
nyquist_compliant: true
wave_0_complete: true
created: 2026-05-22
closed: 2026-05-23
---

# Phase 37 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Distilled from `37-RESEARCH.md §"Validation Architecture"`.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + custom `RmsRegressionTests` helper (existing Phase 28 pattern) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` |
| **Quick run command** | `dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase37"` |
| **Full suite command** | `dotnet test` (entire test solution) |
| **Baseline directory** | `flow-lang.Tests/baselines/Phase37/` (new — mirrors `baselines/Phase28/`) |
| **RMS tolerance** | ±0.5 dB / 100 ms windows per SPEC-8 |
| **Estimated quick runtime** | ~30 s |
| **Estimated full runtime** | ~2 min |

---

## Sampling Rate

- **After every task commit:** `dotnet test --filter "FullyQualifiedName~Phase37&Category!=Slow"` (≤30 s — unit + lightweight integration)
- **After every plan wave:** `dotnet test --filter "FullyQualifiedName~Phase37"` (≤2 min — full Phase 37 suite)
- **Before `/gsd:verify-work`:** Full `dotnet test` must be green
- **Max feedback latency:** 30 seconds per task

---

## Per-Task Verification Map

| REQ ID | Behavior | Test Type | Automated Command | File Exists | Status |
|--------|----------|-----------|-------------------|-------------|--------|
| DSP-01 | `(granular buf grain=50ms density=20Hz jitter=0.3 windowing=#hann)` returns Buffer composable with reverb/gain/pan/filter | integration | `dotnet test --filter "GranularSynthesisTests"` | ✅ shipped | ✅ green |
| DSP-01 | Hann/Gaussian/Tukey windowing options produce DIFFERENT output | unit | `dotnet test --filter "WindowFunctionTests"` | ✅ shipped | ✅ green |
| DSP-01 | Granular jitter PRNG via PrngRegistry — two-run cmp-clean | integration | `dotnet test --filter "GranularDeterminismTests"` | ✅ shipped | ✅ green |
| DSP-02 | `(stretch buf 2.0 mode=#vocoder)` doubles length within ±1 sample | integration | `dotnet test --filter "StretchVocoderTests"` | ✅ shipped | ✅ green |
| DSP-02 | `(stretch buf 2.0 mode=#psola)` preserves transients (onset drift ≤ 5 ms) | integration | `dotnet test --filter "StretchPsolaTransientTests"` | ✅ shipped | ✅ green |
| DSP-02 | `mode=#auto` emits stderr `[stretch] mode=#auto picked: X% vocoder / Y% psola` exactly once per call | integration | `dotnet test --filter "StretchAutoAdvisoryTests"` | ✅ shipped | ✅ green |
| DSP-02 | `(stretch buf 1.0)` returns input byte-for-byte (fast-path identity) | unit | `dotnet test --filter "StretchIdentityTests"` | ✅ shipped | ✅ green |
| DSP-03 | `(pitchShift buf +5st)` shifts pitch by 5 semitones, preserves duration within ±1 sample | integration | `dotnet test --filter "PitchShiftTests"` | ✅ shipped | ✅ green |
| DSP-03 | `loadWav` varispeed path unaffected — Phase 27 byte-identical baseline holds | regression | `dotnet test --filter "LoadWavVarispeedRegression"` | ✅ (Phase 27) | ✅ green |
| MIX-01 | Synth-path pan baseline pinned via RMS regression | regression | `dotnet test --filter "Phase37MixSynthPathRegression"` | ✅ shipped | ✅ green |
| MIX-02 | SFZ voice with `voice.Pan = 0.7` produces stereo with right-louder-than-left | integration | `dotnet test --filter "SfzPanRetrofitTests"` | ✅ shipped | ✅ green |
| MIX-02 | SFZ per-region + per-voice pan compose (additive-with-clamp, lock in plan-phase) | integration | `dotnet test --filter "SfzPanCompositionTests"` | ✅ shipped | ✅ green |
| SAMP-01 | `seq_position`/`seq_length` parsed; multiple triggers produce DIFFERENT samples | integration | `dotnet test --filter "SfzRoundRobinTests"` | ✅ shipped | ✅ green |
| SAMP-01 | Round-robin sequence deterministic across two renders (voice ordinal seed) | integration | `dotnet test --filter "SfzRoundRobinDeterminismTests"` | ✅ shipped | ✅ green |
| SAMP-02 | `xfin_lovel`/`xfin_hivel` parsed; velocity in crossfade band produces NON-zero output from BOTH layers | integration | `dotnet test --filter "SfzVelocityCrossfadeTests"` | ✅ shipped | ✅ green |
| SAMP-02 | Hard-switch fallback when xfin/xfout absent matches Phase 33 byte-identical baseline | regression | `dotnet test --filter "SfzHardSwitchRegression"` | ✅ shipped | ✅ green |
| SAMP-03 | Per-articulation envelope multiplier active on sample path; synth Phase 28 regression unaffected | regression | `dotnet test --filter "Phase28ArticulationRegression"` | ✅ (Phase 28) | ✅ green |
| SAMP-03 | Sample-path staccato has measurably more harmonic energy than pre-multiplier baseline | integration | `dotnet test --filter "SampledStaccatoEnergyTests"` | ✅ shipped | ✅ green |
| PIANO-01 | Piano `SampleCache` has ≥4 velocity layers (pp/mp/mf/ff) after eager-load | unit | `dotnet test --filter "PianoSampleCacheLayersTest"` | ✅ shipped | ✅ green |
| PIANO-01 | `release=` named arg overrides default; release=2.0 produces audible tail at t=1.5s past authored end | integration | `dotnet test --filter "PianoReleaseKnobTests"` | ✅ shipped | ✅ green |
| FLUTE-01 | Flute `SampleCache` has ≥3 sample points (G4, [A4 OR D5], G5) | unit | `dotnet test --filter "FluteSampleCacheTests"` | ✅ shipped | ✅ green |
| FLUTE-01 | D5 crossover gap closed — D5 note timbre RMS-matches nearer sample point within ±0.5 dB | integration | `dotnet test --filter "FluteD5CrossoverTests"` | ✅ shipped | ✅ green |
| DRUM-01 | `(loadSfz #drums)` resolves to `GM-StylePerc.sfz` and parses without error | integration | `dotnet test --filter "SfzDrumsLoadTest"` | ✅ shipped | ✅ green |
| DRUM-01 | Drum pitch-shift uses `#auto` PSOLA path for transient kits (kick=36, snare=38) | integration | `dotnet test --filter "DrumPitchShiftAutoTests"` | ✅ shipped | ✅ green |
| GLOBAL | Two-run cmp-clean determinism preserved on full v1.4 example suite + new Phase 37 examples | regression | `dotnet test --filter "TwoRunDeterminismTests"` | ✅ (P18/25/27/28/29/33/36) | ✅ green |
| GLOBAL | SPEC-8 RMS regression baselines (±0.5 dB / 100 ms) committed for behavior-changing tests | regression | `dotnet test --filter "Phase37RmsRegression"` | ✅ shipped | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `flow-lang.Tests/Integration/Phase37/GranularSynthesisTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/WindowFunctionTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/GranularDeterminismTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/StretchVocoderTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/StretchPsolaTransientTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/StretchAutoAdvisoryTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/StretchIdentityTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/PitchShiftTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/SfzRoundRobinTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/SfzRoundRobinDeterminismTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/SfzVelocityCrossfadeTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/SfzHardSwitchRegression.cs`
- [x] `flow-lang.Tests/Integration/Phase37/SfzPanRetrofitTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/SfzPanCompositionTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/SampledStaccatoEnergyTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/PianoSampleCacheLayersTest.cs`
- [x] `flow-lang.Tests/Integration/Phase37/PianoReleaseKnobTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/FluteSampleCacheTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/FluteD5CrossoverTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/SfzDrumsLoadTest.cs`
- [x] `flow-lang.Tests/Integration/Phase37/DrumPitchShiftAutoTests.cs`
- [x] `flow-lang.Tests/Integration/Phase37/Phase37MixSynthPathRegression.cs`
- [x] `flow-lang.Tests/Integration/Phase37/Phase37RmsRegression.cs`
- [x] `flow-lang.Tests/baselines/Phase37/` (RMS baseline directory — `mix_synth_path_pan.wav` + `piano_warmth_smoke.wav` committed)
- [x] `flow-lang.Tests/fixtures/Phase37/` (test WAV fixtures: sustained sine, drum hit, mixed material — `Phase37Fixtures.EnsureFixturesExist` regenerates per test ctor)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Ragtime piano "warmth" subjective approval | PIANO-01 | Composer-perceptual; ergonomics-first principle (D-37-12) | Composer listens to `examples/ragtime/ragtime.flow` rerendered with `release=` smart default + ≥4 velocity layers; signs off in `37-HUMAN-UAT.md`; orchestrator locks RMS baseline at approval |
| Synthesized mp piano layer acceptability | PIANO-01 (A5) | Perceptual distinctness of RMS-interpolated mp vs pp/mf | A/B test during ragtime UAT — composer toggles synthesized-mp on/off and confirms it adds value vs pp+mf alone; escalate to Path 2 (more pitch points) if rejected |
| SAMP-03 multiplier shape final pick | SAMP-03 (A8) | Perceptual "thinner than synth" gap closure | A/B test: same staccato passage through synth vs sample-path with Option A scalar multipliers; composer confirms or escalates to Option B full curve overlay |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (24 file gaps enumerated above)
- [x] No watch-mode flags
- [x] Feedback latency < 30 s per task
- [x] `nyquist_compliant: true` set in frontmatter (after Wave 0 files materialize)

**Approval:** approved 2026-05-23 — Wave 0 task completed in Plan 37-01 (23-file scaffold + fixture/baseline directories); all 11 Phase 37 REQs verified per `37-VERIFICATION.md` (Plan 37-07 closer)

---

## Security Domain (ASVS L1 baseline)

Phase 37 is pure audio DSP + sample loading — no network, no auth, no user-input parsing beyond existing SFZ parser surface.

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V5 Input Validation | yes | SfzParser charitable-fallback (Phase 33 pattern) extended to new opcodes |
| V12 File and Resources | yes | Sample file paths via `sfz_root` anchor + `Path.Combine` (Phase 33 SPEC-3) |

### Known Threat Patterns

| Pattern | STRIDE | Mitigation |
|---------|--------|-----------|
| Malformed SFZ `seq_length=999999999` | DoS | Clamp to spec max 100 + WarnOnce |
| Pathological granular `density=1e9Hz` | DoS | Document cost model; no hard cap (composer philosophy) |
| Negative stretch factor `factor <= 0.0` | Tampering | Validate at builtin entry; throw with clear message |
| FFT non-power-of-2 frame size | DoS | Validate power-of-2 + auto-pad with warning |
| Sample path traversal in user SFZ | Tampering | Existing Phase 33 `sfz_root` anchor — no `..` escape |
