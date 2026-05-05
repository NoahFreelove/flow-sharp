---
phase: 25
slug: gaussian-humanize-last-prng-phase
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-04
---

# Phase 25 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (.NET 10) + .flow integration smokes |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (existing — no Wave 0 framework install needed) |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase25"` |
| **Full suite command** | `dotnet test flow-lang.Tests` then `dotnet run --project flow-interpreter examples/showcase.flow && dotnet run --project flow-interpreter examples/tutorial.flow` |
| **Estimated runtime** | ~30s Phase 25 filter; ~2min full xUnit; ~3min .flow smokes |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase25"` (~7 unit Facts + 2 integration Facts; <30s)
- **After every plan wave:** Run `dotnet test flow-lang.Tests` (full suite — must stay GREEN)
- **Before `/gsd-verify-work`:** Full suite green AND two consecutive runs of `examples/showcase.flow` and `examples/tutorial.flow` produce cmp-clean WAV + MIDI output (Phase 18 byte-identical regression contract)
- **Max feedback latency:** ~30 seconds for the per-task filter

---

## Per-Task Verification Map

> Filled by planner. Each task ships with an `<automated>` verify command (xUnit Fact name or `.flow` script invocation) OR an explicit Wave 0 dependency.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 25-00-* | 25-00 | 0 | DEFER-06 (scaffold) | — | Test directories + skeleton facts compile in RED state | scaffold | `dotnet build flow-lang.Tests` exits 0 | ❌ W0 | ⬜ pending |
| 25-01-* | 25-01 | 1 | DEFER-06 (helper) | — | `MusicalNoteData.With(velocity:)` extension preserves all 17 fields | unit | `dotnet test --filter "FullyQualifiedName~NoteTypeWithVelocityFacts"` | ❌ W0 | ⬜ pending |
| 25-02-* | 25-02 | 2 | DEFER-06 (impl) | — | `humanizeGaussian` deterministic per seed; rests passthrough; clamps; amount=0 short-circuit | unit | `dotnet test --filter "FullyQualifiedName~HumanizeGaussianFacts"` | ❌ W0 | ⬜ pending |
| 25-03-* | 25-03 | 3 | DEFER-06 (showcase) | — | Showcase produces byte-identical WAV + MIDI across two consecutive runs after additive call site | integration | `dotnet test --filter "FullyQualifiedName~ByteIdenticalShowcaseGaussianTests"` | ❌ W0 | ⬜ pending |
| 25-04-* | 25-04 | 4 | DEFER-06 (closure) | — | REQUIREMENTS / ROADMAP / STATE / VERIFICATION marked shipped; tutorial chapter added; Phase 18 regression GREEN | integration | `dotnet test flow-lang.Tests` exits 0 + `dotnet run --project flow-interpreter examples/tutorial.flow` exits 0 + `examples/showcase.flow` two-run cmp clean | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Unit/Phase25/` directory created
- [ ] `flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs` skeleton (xUnit `[Collection("FlowScripts")]`, `private const double Tol = 1e-9;`, `private const double BaseVelocity = 0.63;` — matches Phase15/EuclideanSwingTests.cs:32-33)
- [ ] `flow-lang.Tests/Integration/Phase25/` directory created
- [ ] `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` skeleton (mirrors `Phase18/ByteIdenticalShowcaseTests.cs:1-89`; only changes: namespace `FlowLang.Tests.Integration.Phase25`, run-file basenames `phase25_showcase_run1.{wav,mid}` / `phase25_showcase_run2.{wav,mid}`)
- [ ] `tests/test_humanize_gaussian.flow` smoke script (sentinels: `humanizeGaussian seed=42: PASSED`, `two runs byte-identical: PASSED` — pattern matches Phase 15 euclidean smoke)
- [ ] `flow-lang.Tests/FlowScriptData.cs` registered with the new smoke (mirror lines 225-231 entry style)
- [ ] `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:317` — `With(...)` helper extended with a `double? velocity = null` parameter (RESEARCH critical pre-existing bug avoidance — `humanizeGaussian` MUST NOT repeat the existing `Humanize` 12-arg ctor that drops 5 fields)

*Existing infrastructure covers test framework + .flow interpreter + std.flow declaration. No new csproj or NuGet package required (D-04 / D-18 invariants).*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Audible naturalness — Gaussian humanize sounds more "human pianist" than uniform humanize | DEFER-06 (subjective acceptance) | Distribution shape is statistical; perceptual quality is subjective | Run `dotnet run --project flow-interpreter examples/showcase.flow`; compare to a pre-Phase-25 baseline by ear. Confirm the new `humanizeGaussian` call site on `melody` produces a more naturalistic feel than uniform humanize would. (Optional — not a ship gate.) |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (the 7 Wave-0 items above)
- [ ] No watch-mode flags (`dotnet test` is one-shot; `.flow` smokes are one-shot)
- [ ] Feedback latency < 30s for the Phase25 filter
- [ ] **Phase 18 byte-identical regression gate verified** — `examples/tutorial.flow` produces cmp-clean WAV + MIDI across two consecutive runs (D-18 / D-19 invariant: existing `humanize` is FROZEN); `examples/showcase.flow` likewise (D-20 additive call site + D-21 self-re-pinning Phase 18 test stays GREEN)
- [ ] Two-runs byte-identical smoke for `tests/test_humanize_gaussian.flow` proves `humanizeGaussian` is deterministic at the .flow integration level
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
