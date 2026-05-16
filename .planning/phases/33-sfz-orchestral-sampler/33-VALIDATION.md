---
phase: 33
slug: sfz-orchestral-sampler
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-15
---

# Phase 33 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from `33-RESEARCH.md` §"Validation Architecture".

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 3.2.2 (`flow-lang.Tests/flow-lang.Tests.csproj:13`) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~Phase33" --logger "console;verbosity=minimal"` |
| **Full suite command** | `dotnet test flow-sharp.sln --logger "console;verbosity=minimal"` |
| **Estimated runtime** | ~30 seconds (Phase 33 only); ~3 minutes (full suite) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "FullyQualifiedName~Phase33" --logger "console;verbosity=minimal"`
- **After every plan wave:** Run `dotnet test flow-sharp.sln --logger "console;verbosity=minimal"` (guards Phase 29 byte-identical regression)
- **Before `/gsd:verify-work`:** Full suite must be green AND `dotnet test --filter "FullyQualifiedName~Phase33SfzSmoke"` green
- **Max feedback latency:** 30 seconds for the Phase 33 filter

---

## Per-Task Verification Map

> Filled by `/gsd:plan-phase` once PLAN.md files exist. The Requirement column maps to SPEC-1..SPEC-8 (see `33-SPEC.md`). The base test command per requirement is pre-pinned from research.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 33-XX-YY | XX | W | SPEC-1 | — | `loadSfz` undefined without `use "@sfz"` | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzGatingTests"` | ❌ W0 | ⬜ pending |
| 33-XX-YY | XX | W | SPEC-2 | — | Symbol-keyed dict + `sfz_root` resolution | integration | `dotnet test --filter "FullyQualifiedName~Phase33.SfzSymbolLookupTests"` | ❌ W0 | ⬜ pending |
| 33-XX-YY | XX | W | SPEC-3 | T-33-PARSE-01 | 13-opcode whitelist; advisory dedup; strict numeric | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzParserTests"` | ❌ W0 | ⬜ pending |
| 33-XX-YY | XX | W | SPEC-4 | — | (pitch, vel) region match + nearest-pitch varispeed fallback | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzRegionMatchTests"` | ❌ W0 | ⬜ pending |
| 33-XX-YY | XX | W | SPEC-5 | — | Per-sample discontinuity ≤ 0.05; equal-power vs linear spectral centroid ±2% | unit | `dotnet test --filter "FullyQualifiedName~Phase33.SfzLoopCrossfadeTests"` | ❌ W0 | ⬜ pending |
| 33-XX-YY | XX | W | SPEC-6 | — | `Sfz` value type + `sampler:NAME` dispatch + unknown-name error | integration | `dotnet test --filter "FullyQualifiedName~Phase33.SfzBindingTests"` | ❌ W0 | ⬜ pending |
| 33-XX-YY | XX | W | SPEC-7 | — | CI smoke renders < 100 KB fixture; RMS > −40 dBFS; discontinuity check | integration | `dotnet test --filter "FullyQualifiedName~Phase33SfzSmoke"` | ❌ W0 | ⬜ pending |
| 33-XX-YY | XX | W | SPEC-8 | — | 6 articulations distinct buffers; `ampeg_attack` override | integration | `dotnet test --filter "FullyQualifiedName~Phase33.SfzArticulationTests"` | ❌ W0 | ⬜ pending |
| 33-XX-YY | XX | W | det. | — | Two-run byte-identical determinism on smoke fixture | integration | `dotnet test --filter "FullyQualifiedName~Phase33.SfzDeterminismTests"` | ❌ W0 | ⬜ pending |
| 33-XX-YY | XX | W | reg. | — | Existing `renderSong song "piano"` byte-identical pre/post Phase 33 | regression | `dotnet test --filter "FullyQualifiedName~Phase29.RmsBaselineTests"` | ✅ exists | ⬜ pending |
| 33-XX-YY | XX | W | size | — | Phase 33 in-repo artifacts < 100 KB | integration | `dotnet test --filter "FullyQualifiedName~Phase33.RepoSizeTests"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

All Phase 33 test files are gaps. Wave 0 must establish:

- [ ] `flow-lang.Tests/Integration/Phase33/SfzSmokeTests.cs` — SPEC-7 smoke; renders fixture, asserts non-empty + RMS + discontinuity
- [ ] `flow-lang.Tests/Integration/Phase33/SfzGatingTests.cs` — SPEC-1 import-gating
- [ ] `flow-lang.Tests/Integration/Phase33/SfzSymbolLookupTests.cs` — SPEC-2 symbol resolution + 19-symbol list error
- [ ] `flow-lang.Tests/Integration/Phase33/SfzConfigTests.cs` — SPEC-2 missing-config-key error
- [ ] `flow-lang.Tests/Unit/Phase33/SfzParserTests.cs` — SPEC-3 opcode whitelist + advisory dedup + strict numeric + header inheritance
- [ ] `flow-lang.Tests/Unit/Phase33/SfzRegionMatchTests.cs` — SPEC-4 grid lookup + nearest-pitch fallback (spectral fingerprint helper)
- [ ] `flow-lang.Tests/Unit/Phase33/SfzLoopCrossfadeTests.cs` — SPEC-5 per-sample discontinuity + equal-power spectral
- [ ] `flow-lang.Tests/Integration/Phase33/SfzBindingTests.cs` — SPEC-6 typed-variable binding + sampler dispatch + unknown-name
- [ ] `flow-lang.Tests/Integration/Phase33/SfzArticulationTests.cs` — SPEC-8 6-articulation distinctness + `ampeg_attack` override
- [ ] `flow-lang.Tests/Integration/Phase33/SfzDeterminismTests.cs` — two-run byte-identical contract
- [ ] `flow-lang.Tests/Integration/Phase33/RepoSizeTests.cs` — < 100 KB cap on `fixtures/sfz-smoke/`
- [ ] `flow-lang.Tests/fixtures/sfz-smoke/smoke.sfz` — 2-region synthetic fixture
- [ ] `flow-lang.Tests/fixtures/sfz-smoke/C4_sine.wav` + `G5_sine.wav` — synthetic sine bursts, committed
- [ ] `flow-lang.Tests/Tools/Phase33FixtureGenerator.cs` — helper that regenerates fixture WAVs from a known seed
- [ ] No framework install needed — xunit.v3 is already wired

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Real VSCO Community CE 1.1.0 library loads + plays correctly (orchestral patches) | SPEC-2 (acceptance) | Library is composer-supplied; not vendored in-repo; CI runs the < 100 KB synthetic fixture only | (1) Download VSCO-CE 1.1.0 SFZ release; (2) set `sfz_root` in `~/.config/flow/config.toml`; (3) run `examples/symphony/sfz_smoke.flow`; (4) verify `(loadSfz #violin)` produces audible output through Phase 29 PulseAudio playback path |
| "Postable on GitHub" mix quality for Phase 34 symphony showcase | SPEC-2 + Phase 34 | Subjective listening test — blind A/B against reference orchestral recording | Phase 34 UAT — out of scope for Phase 33 |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (13 test files + 3 fixtures + 1 generator helper)
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s for Phase 33 filter
- [ ] `nyquist_compliant: true` set in frontmatter after PLAN tasks pin each test command to a specific Task ID

**Approval:** pending
