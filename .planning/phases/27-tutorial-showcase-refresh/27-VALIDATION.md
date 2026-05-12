---
phase: 27
slug: tutorial-showcase-refresh
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-10
---

# Phase 27 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Phase 27 is documentation-shaped: the "tests" are (1) tutorial.flow + showcase.flow + companion files exit cleanly with non-empty WAV+MIDI, (2) two consecutive runs produce byte-identical output, (3) full unit suite stays GREEN. No new test framework, only a new `Phase27ByteIdenticalPragmaTests` class for the companion files.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (existing — `flow-lang.Tests/flow-lang.Tests.csproj`) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (existing) |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase27"` |
| **Full suite command** | `dotnet test flow-lang.Tests --nologo` |
| **Smoke run (tutorial)** | `dotnet run --project flow-interpreter examples/tutorial.flow` |
| **Smoke run (showcase)** | `dotnet run --project flow-interpreter examples/showcase.flow` |
| **Smoke run (h_alias)** | `dotnet run --project flow-interpreter examples/pragmas/h_alias.flow` |
| **Smoke run (microtonal_ji)** | `dotnet run --project flow-interpreter examples/pragmas/microtonal_ji.flow` |
| **Estimated runtime** | ~15s for filtered Phase27; ~90s for full suite + smokes |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase27"`
- **After every plan wave:** Run Phase 27 + Phase 18 + Phase 25 byte-identical sentinels:
  `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase18.ByteIdentical|FullyQualifiedName~Phase25.ByteIdenticalShowcase|FullyQualifiedName~Phase27"`
- **Before `/gsd-verify-work`:** Full unit suite GREEN + 4 smoke runs (tutorial, showcase, h_alias, microtonal_ji) all exit 0 with non-empty `.wav` + `.mid` artifacts in `examples/output/`
- **Max feedback latency:** ~15s (filtered Phase27 facts)

---

## Per-Task Verification Map

> Tasks are not yet decomposed (planner spawns next). This map is **pre-populated by requirement** so the planner can attach Task IDs as plans are produced.

| Req ID | Behavior | Test Type | Automated Command | File Exists | Status |
|--------|----------|-----------|-------------------|-------------|--------|
| QOL-04 (a) | tutorial.flow demonstrates every v1.3 feature | grep audit + smoke | `dotnet run --project flow-interpreter examples/tutorial.flow` exit 0 + grep audit one-liner (closure plan) | ❌ W0 (grep audit script — embedded in closure plan, not a tracked artifact) | ⬜ pending |
| QOL-04 (a) | showcase.flow demonstrates v1.3 features audibly | smoke | `dotnet run --project flow-interpreter examples/showcase.flow` exit 0 + non-empty wav + non-empty mid | Existing pattern; new content | ⬜ pending |
| QOL-04 (b) | tutorial.flow exits 0 + non-empty WAV + non-empty MIDI | smoke | `[ -s examples/output/flow_tutorial.wav ] && [ -s examples/output/flow_tutorial.mid ]` | Existing (Phase 16 pattern); re-run after Phase 27 changes | ⬜ pending |
| QOL-04 (c) | tutorial.flow byte-identical across two runs | xUnit (existing — auto-follows new content) | `dotnet test --filter "FullyQualifiedName~Phase18.ByteIdenticalTutorial"` | ✅ `Phase18/ByteIdenticalTutorialTests.cs` | ⬜ pending |
| QOL-04 (c) | showcase.flow byte-identical across two runs | xUnit (existing — auto-follows new content) | `dotnet test --filter "FullyQualifiedName~Phase18.ByteIdenticalShowcase\|FullyQualifiedName~Phase25.ByteIdenticalShowcaseGaussian"` | ✅ `Phase18/ByteIdenticalShowcaseTests.cs` + `Phase25/ByteIdenticalShowcaseGaussianTests.cs` | ⬜ pending |
| QOL-04 (d) | h_alias.flow + microtonal_ji.flow byte-identical | xUnit (NEW) | `dotnet test --filter "FullyQualifiedName~Phase27.ByteIdenticalPragma"` | ❌ W0 — `flow-lang.Tests/Integration/Phase27/Phase27ByteIdenticalPragmaTests.cs` (mirrors Phase18 ShowcaseTests verbatim, 4 facts) | ⬜ pending |
| QOL-04 (e) | Existing v1.1 + v1.2 chapters preserved | grep audit | `grep -cE "^Note: [0-9]+\\." examples/tutorial.flow` (expect ≥ baseline chapter count + new chapters) | Existing (chapter-header convention from Phase 16) | ⬜ pending |
| QOL-04 (f) | REQUIREMENTS.md QOL-04 rewrite includes Phase 26.2 surface (D-101) | grep | `grep -E "QOL-04.*volume\|QOL-04.*Hertz\|QOL-04.*Ms.*FX\|QOL-04.*Second.*reverb" .planning/REQUIREMENTS.md` (expect match) | ❌ Wave 5 closure task | ⬜ pending |
| QOL-04 (g) | CLAUDE.md gains "Music Types Quick Reference" table (D-104) | grep | `grep -A 3 "Music Types Quick Reference" CLAUDE.md` (expect table header line) | ❌ Wave 5 closure task | ⬜ pending |
| Full suite GREEN | Zero regressions across all phases | xUnit | `dotnet test flow-lang.Tests --nologo` | Existing (~879+ facts pre-Phase-27 baseline) | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Integration/Phase27/Phase27ByteIdenticalPragmaTests.cs` — covers QOL-04 (d) byte-identical contract for the two new companion files (D-403). Mirrors `Phase18/ByteIdenticalShowcaseTests.cs` structure verbatim: two-run `SequenceEqual` + `bytes.Length > 0` per file × 2 files = 4 facts.
- [ ] `examples/pragmas/h_alias.flow` — companion file (D-402); ~30 lines; activates `enable hAsB;`, demonstrates `| H4q B4q |` audibly identical, demonstrates `H` outside note streams stays an identifier (`Int H = 5;`); writes WAV to `examples/output/h_alias.wav`.
- [ ] `examples/pragmas/microtonal_ji.flow` — companion file (D-402); ~40 lines; activates `enable justIntonation;`, prints C4/E4/G4 frequencies in JI vs 12-TET, renders short Cmaj triad WAV to `examples/output/microtonal_ji.wav`.
- [ ] (Optional, embedded in closure plan — not a tracked artifact) Bash one-liner that confirms each v1.3 feature appears at least once in tutorial.flow. Lives in the closure plan's verification section as a documentation-only smoke check.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Tutorial reads cleanly to a new composer (chapter ordering, prose flow, snippet density) | QOL-04 (a) | Quality-of-prose judgment — no automatable rubric; companion to the grep-audit which only confirms feature **presence**, not pedagogical quality | After Wave 2 + Wave 3 land: read `examples/tutorial.flow` end-to-end. Confirm new v1.3 chapters slot into existing 19-chapter flow without orphan references; confirm graduation song closes the file. |
| Showcase "wow listen to this" mood lands | QOL-04 (a) | Aesthetic judgment; piece must feel like a deliberate composition, not a feature parade | After Wave 3 lands: `dotnet run --project flow-interpreter examples/showcase.flow` and listen to `examples/output/flow_showcase.wav` (PulseAudio playback or external player). |
| Tutorial graduation song musically resolves | QOL-04 (a) | Composer-feel check; graduation should sound finished, not abruptly cut | After Wave 3 lands: listen to `examples/output/flow_tutorial.wav` final section. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (`Phase27ByteIdenticalPragmaTests.cs`, both companion `.flow` files)
- [ ] No watch-mode flags (smoke commands are one-shot `dotnet run`)
- [ ] Feedback latency < 30s (Phase 27 filtered facts: ~15s)
- [ ] `nyquist_compliant: true` set in frontmatter (set by closure plan once all rows ✅)

**Approval:** pending
