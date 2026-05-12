---
phase: 29
slug: instrument-realism
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-05-10
---

# Phase 29 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from `29-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.x (existing in `flow-lang.Tests.csproj`) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase29" --nologo` |
| **Full suite command** | `dotnet test flow-lang.Tests --nologo` |
| **Estimated runtime** | ~30 sec (Phase29 filter) / ~3-5 min (full suite) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase29" --nologo`
- **After every plan wave:** Run `dotnet test flow-lang.Tests --nologo`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 sec for per-task; 5 min for per-wave

Per SPEC D-34 (test runtime budget): all new Phase 29 unit tests + sample-cache load + varispeed accuracy + articulation-on-sample must complete within 60 seconds total.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 29-00-01 | 00 (samples) | 0 | REQ-2 | T-29-V5-01 (path traversal) | Sample paths confined to `flow-lang/Samples/` allowlisted instruments | unit | `dotnet test --filter "Name~LicenseAuditTests"` | ❌ W0 | ⬜ pending |
| 29-00-02 | 00 (samples) | 0 | REQ-2 | — | du -sh ≤ 5 MB | unit | `dotnet test --filter "Name~RepoSizeTests"` | ❌ W0 | ⬜ pending |
| 29-01-01 | 01 (infra) | 1 | REQ-4 | — | Eager-load deterministic order | unit | `dotnet test --filter "Name~SampleCacheTests"` | ❌ W0 | ⬜ pending |
| 29-01-02 | 01 (infra) | 1 | REQ-4 | — | Second renderSong ≥ 30% faster | unit | `dotnet test --filter "Name~SampleCacheTests"` | ❌ W0 | ⬜ pending |
| 29-01-03 | 01 (infra) | 1 | REQ-1 | T-29-V5-02 (instrument allowlist) | SampledInstrumentRenderer routes only allowlisted instruments | unit | `dotnet test --filter "Name~SampledInstrumentSmokeTests"` | ❌ W0 | ⬜ pending |
| 29-02-01 | 02 (piano) | 2 | REQ-3 | — | Piano velocity layer crossfade cosSim < 0.92 | unit | `dotnet test --filter "Name~VelocityLayerTests"` | ❌ W0 | ⬜ pending |
| 29-02-02 | 02 (piano) | 2 | REQ-5 | — | 6 articulations × piano sample produce 6 distinct buffers | unit | `dotnet test --filter "Name~ArticulationOnSampleTests"` | ❌ W0 | ⬜ pending |
| 29-03-01 | 03 (other tonal) | 2 | REQ-1, REQ-3 | — | Brass/Sax/Strings/Flute/Bell single-velocity scaling; cosSim ≥ 0.92 across velocity range | unit | `dotnet test --filter "Name~VelocityLayerTests"` | ❌ W0 | ⬜ pending |
| 29-04-01 | 04 (drums/organ/wavetable) | 3 | REQ-6 | — | ≥ 20% harmonic-richness gain vs Phase 28 baseline | unit | `dotnet test --filter "Name~HarmonicRichnessTests"` | ❌ W0 | ⬜ pending |
| 29-05-01 | 05 (A/B + UAT infra) | 4 | REQ-7 | — | 6 A/B fixtures + closure render script + sealed answer-key path | unit | `dotnet test --filter "Name~AbFixtureSmokeTests"` (renders fixtures without error) | ❌ W0 | ⬜ pending |
| 29-06-01 | 06 (determinism) | 4 | (continuity) | — | Phase 29 fixtures byte-identical across two runs | unit | `dotnet test --filter "Name~Phase29ByteIdentical"` | ❌ W0 | ⬜ pending |
| 29-07-01 | 07 (closure) | 5 | REQ-8 | — | All 5 closure gates pass | manual + automated | composer manual UAT + full `dotnet test` exits 0 | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Integration/Phase29/SampledInstrumentSmokeTests.cs` — smoke test each tonal instrument renders without exception
- [ ] `flow-lang.Tests/Integration/Phase29/LicenseAuditTests.cs` — parse every `flow-lang/Samples/{instrument}/LICENSE.md`; assert `License:` and `Source:` lines + CC0/PD declaration
- [ ] `flow-lang.Tests/Integration/Phase29/RepoSizeTests.cs` — enumerate `flow-lang/Samples/` recursively; assert sum of file lengths ≤ 5*1024*1024 bytes
- [ ] `flow-lang.Tests/Integration/Phase29/VelocityLayerTests.cs` — render C4q at v=0.2 + v=0.95; FFT magnitudes; cosine similarity assertion per instrument
- [ ] `flow-lang.Tests/Integration/Phase29/SampleCacheTests.cs` — eager-load + cache-hit speedup ≥ 30%
- [ ] `flow-lang.Tests/Integration/Phase29/ArticulationOnSampleTests.cs` — piano C4q × 6 articulations → 6 distinct buffers; durations match Phase 28 ±5%
- [ ] `flow-lang.Tests/Integration/Phase29/HarmonicRichnessTests.cs` — hand-rolled FFT; ≥ 20% gain per Drums/Organ/Wavetable
- [ ] `flow-lang.Tests/Integration/Phase29/AbFixtureSmokeTests.cs` — each `examples/tests/realism_ab/*.flow` renders to WAV without exception
- [ ] `flow-lang.Tests/Integration/Phase29/Phase29ByteIdenticalTests.cs` — extends Phase 18 two-run pattern for at least one Phase 29 fixture
- [ ] `flow-lang.Tests/Fixtures/Phase29/tiny_test_sample.wav` — ≤ 100 KB; shared test fixture for unit-level coverage (avoids loading full bundle in unit tests, per SPEC D-34)
- [ ] (Test framework xUnit + Fixtures/Integration directory pattern already present — no install needed)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Composer correctly identifies Phase 29 on ≥ 5 of 6 A/B fixtures | REQ-7 (Gate A) | Audio realism is a perceptual judgment; no automated proxy for "sounds more like a real instrument" | Closure script renders each fixture under Phase 28 baseline + Phase 29 with randomized A/B mapping. Composer listens to all 12 WAVs in `examples/output/realism_ab/`. Writes A or B per fixture in `29-VERIFICATION.md`. Runs unseal command. Pass if N ≥ 5/6 correct. |
| Per-fixture reflection paragraph (6 total) explaining which timbral aspects improved | REQ-8 (Gate E) | Subjective reflection; no automated proxy | Composer writes one paragraph per fixture in `29-VERIFICATION.md` "Closure Reflection" section after A/B blind test concludes. |
| Bell instrument quality at varispeed reach > ±6 semitones | open Q2 from RESEARCH | Audio quality at extremes is a perceptual judgment | Composer renders test fixture exercising bell at C4 and B5 (worst-case varispeed shift from C5). If audibly unacceptable, plan adds a second bell sample (C6) and re-validates. |
| License source verification (each LICENSE.md `Source:` URL leads to verified CC0 declaration on the actual host) | REQ-2 (Gate C, closure-time only) | License-audit unit test verifies file structure but cannot fetch external URLs in unit-test runtime | Closure-time manual review: open each `Source:` URL, confirm the destination page declares CC0 or public-domain. Document outcome in `29-VERIFICATION.md`. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify (composer A/B is the only manual-gate; all other tasks have unit-test verification)
- [ ] Wave 0 covers all MISSING references (9 new test files + 1 shared fixture)
- [ ] No watch-mode flags
- [ ] Feedback latency < 30 sec per task / < 60 sec for full Phase 29 suite
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending (will be approved at closure once all gates green)
