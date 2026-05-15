---
phase: 32
slug: full-scala-scl-tuning-loader
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-13
---

# Phase 32 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from 32-RESEARCH.md § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (.NET 10) — matches Phase 23/28/29/31 |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (Microsoft.NET.Test.Sdk + xunit + xunit.runner.visualstudio already present) |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase32" -v minimal` |
| **Full suite command** | `dotnet test flow-lang.Tests -v minimal` |
| **Estimated runtime** | ~15 s (Phase 32 sub-suite) / ~90 s (full flow-lang.Tests, ~1003+ facts) |

---

## Sampling Rate

- **After every task commit:** `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase32" -v minimal`
- **After every plan wave:** `dotnet test flow-lang.Tests -v minimal` (full suite — must not increase the 62 pre-existing Phase 28 PerSynthArticulation failures)
- **Before `/gsd-verify-work`:** full suite GREEN minus the 62 pre-existing failures; Phase 23 sub-suite (`--filter "FullyQualifiedName~Phase23"`) MUST be 100% GREEN (any regression here = blocker)
- **Max feedback latency:** ~15 s for sub-suite; ~90 s for full

---

## Per-Task Verification Map

> Plan IDs are placeholders until /gsd-plan-phase 32 lands; researcher proposes Wave 0 (fixtures) + 6 implementation waves.

| Plan / Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---|---|---|---|---|---|---|---|
| W0 — fixtures + LICENSE | SPEC-3, SPEC-6 | T-32-FIX | Fixtures load deterministically | infra | n/a (file presence) | ❌ W0 | ⬜ pending |
| W1 — `ScalaParser.cs` core | SPEC-3 | T-32-PARSE | Malformed input does not crash the host process | unit | `dotnet test --filter "ClassName~ScalaParserFacts"` | ❌ W0 | ⬜ pending |
| W1 — `ScalaParser` error format | SPEC-7 | T-32-PARSE | Errors localized to `{file}:{line}:{col}`; no partial loads | unit | `dotnet test --filter "ClassName~ScalaParserErrorFacts"` | ❌ W0 | ⬜ pending |
| W1 — `ScalaKbmParser.cs` | SPEC-4 | T-32-PARSE | Default factory `Default(tuning)` always produces a valid KBM | unit | `dotnet test --filter "ClassName~ScalaKbmParserFacts"` | ❌ W0 | ⬜ pending |
| W2 — `ResolvedTuning` + `TuningType` | SPEC-1 | — | Immutable value; cents math accepts negatives | unit | `dotnet test --filter "ClassName~TuningTypeFacts"` | ❌ W0 | ⬜ pending |
| W2 — `(loadScala)` builtins (1-arg + 2-arg) | SPEC-1, SPEC-4 | T-32-IO | Builtin only opens files under user-provided path arg; no path-traversal escalation | unit + integration | `dotnet test --filter "ClassName~LoadScalaBuiltinFacts"` | ❌ W0 | ⬜ pending |
| W3 — `RenderTuning.Custom` extension | SPEC-1, SPEC-6 | — | Default `RenderTuning` still triggers 12-TET short-circuit | unit | `dotnet test --filter "ClassName~RenderTuningExtensionFacts"` | ❌ W0 | ⬜ pending |
| W3 — `PitchConversion` branch on `Custom != null` | SPEC-1, SPEC-5 | — | 12-TET path unchanged; Phase 23 byte-identical short-circuit preserved | unit + integration | `dotnet test --filter "FullyQualifiedName~Phase23" --no-build` | ✅ existing | ⬜ pending |
| W4 — `MusicalContext.Tuning` → `Stack<RenderTuning>` refactor | SPEC-2, SPEC-6 | — | Pragma push-once + block push/pop preserves Phase 23 D-08 sticky | unit | `dotnet test --filter "ClassName~TuningStackFacts"` | ❌ W0 | ⬜ pending |
| W4 — pragma handlers route through stack | SPEC-6 | — | `enable justIntonation;` still renders identically to Phase 23 | unit + integration | (Phase23 regression filter) | ✅ existing | ⬜ pending |
| W5 — `TuningContextStatement` AST + parser + interpreter | SPEC-2 | T-32-AST | Three expr forms (identifier, inline call, string-literal sugar) all parse | unit + integration | `dotnet test --filter "ClassName~TuningContextStatementFacts"` | ❌ W0 | ⬜ pending |
| W5 — last-wins pragma + block interaction | SPEC-6 | — | `enable JI; tuning partch { ... }` renders distinct spectra inside vs outside the block | integration | `dotnet test --filter "FullyQualifiedName~LastWins"` | ❌ W0 | ⬜ pending |
| W6 — non-octave + carlos_alpha frequency precision | SPEC-5 | — | Step frequencies within ±0.1 cents of reference values | unit | `dotnet test --filter "ClassName~NonOctavePitchFacts"` | ❌ W0 | ⬜ pending |
| W6 — negative cents descending | SPEC-5 | — | `-78.0` cent step produces ratio < 1 | unit | (above class) | ❌ W0 | ⬜ pending |
| W6 — unmapped KBM key advisory | SPEC-4 | — | `WarnOnce` fires once per Description per process; renders silence | unit | `dotnet test --filter "ClassName~UnmappedKeyAdvisoryFacts"` | ❌ W0 | ⬜ pending |
| W6 — D-13 MIDI export advisory preserved | SPEC-6 | — | `writeMidi` under custom tuning still emits 12-TET + advisory | unit | `dotnet test --filter "ClassName~WriteMidiWarningFacts"` | ✅ existing | ⬜ pending |
| W6 — two-run byte-identical determinism | SPEC-6 | — | Same script + same fixtures + same SHA → identical bytes | integration | `dotnet test --filter "ClassName~ScalaTuningDeterminismTests"` | ❌ W0 | ⬜ pending |
| W7 — tutorial chapter | SPEC-1, SPEC-2 (D-19) | — | `examples/scala/intro.flow` (or extended tutorial) compiles + renders | integration | `dotnet run --project flow-interpreter examples/scala/intro.flow` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/fixtures/scala/` — new directory
  - [ ] `partch_43.scl` (verified raw archive content)
  - [ ] `slendro.scl` (verified raw archive content)
  - [ ] `carlos_alpha.scl` (verified raw archive content; period ≈ 1404¢)
  - [ ] `pythagorean_12.scl` (verified `pyth_12.scl` archive content, renamed per D-16)
  - [ ] `just_5limit.scl` (verified `ji_12.scl` archive content, renamed per D-16; 7-limit tritone at step 6 accepted)
  - [ ] `malformed_step_count.scl` (hand-authored: negative integer in line 2)
  - [ ] `malformed_cents.scl` (hand-authored: non-numeric token in a step line)
  - [ ] `malformed_kbm.kbm` (hand-authored: negative reference frequency)
  - [ ] `LICENSE.md` (softened community-use wording per D-17 + archive-to-repo filename mapping for the 2 renames)
- [ ] `flow-lang.Tests/Unit/Phase32/` — new directory for parser / type / builtin unit Facts
- [ ] `flow-lang.Tests/Integration/Phase32/` — new directory for last-wins, determinism, fixture round-trip Facts
- [ ] No framework install needed — xUnit + .NET 10 already present
- [ ] `flow-lang.Tests/baselines/Phase32/` — **NOT created** (Claude's Discretion: tolerance-only assertions per ±0.1 cents acceptance; no RMS baselines required)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Composer audibly hears different timbres between JI and Partch sections | SPEC-6 (acceptance #2 — spectral envelope comparison can be automated but the audible difference is a UAT-tier check) | Spectral envelope automation pins the math but composer ear-test confirms the artistic claim "audibly correct non-octave intervals" | Run `examples/scala/intro.flow` end-to-end; listen to the rendered WAV; confirm sections render under their tuning |
| Tutorial chapter renders correctly when copy-pasted by a new user | SPEC D-19 | Live composer experience can't be unit-tested; "would someone new to the language succeed?" is a human judgment call | Read `examples/scala/intro.flow` cold; reproduce the render; confirm output matches expectations |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (fixtures, test directories, LICENSE.md)
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s (sub-suite filter)
- [ ] `nyquist_compliant: true` set in frontmatter (after planner approves)

**Approval:** pending
