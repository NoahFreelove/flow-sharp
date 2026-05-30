---
phase: 46-codebase-bloat-removal
plan: 01
subsystem: testing / audio-synthesis-guard
tags: [byte-guard, oscillator, regression-net, verification, wave-0]
requires: []
provides:
  - "NoteSynthesizerByteGuardTests — exact float[] contract for Sine/Saw/Square/Triangle RenderNote output (pre-redirect baseline)"
  - "D-04 verified: Fixtures→fixtures merge already shipped (Phase 44 e0d7274) — no-op for Phase 46"
  - "D-09 KEEP rationale recorded: Phase35 diagnostics .txt baselines are live-read by DiagnosticRendererGoldenTests"
affects:
  - "flow-lang.Tests (new Unit/Phase46 test + baselines/Phase46 dir)"
tech-stack:
  added: []
  patterns:
    - "Exact bit-pattern float[] compare (BitConverter.SingleToInt32Bits) as a ±1-ULP regression net the RMS/two-run gate cannot provide"
    - "In-test oracle mirroring current production arithmetic element-for-element = frozen pre-redirect contract without an external binary baseline"
key-files:
  created:
    - flow-lang.Tests/Unit/Phase46/NoteSynthesizerByteGuardTests.cs
    - flow-lang.Tests/baselines/Phase46/.gitkeep
  modified: []
decisions:
  - "D-03 guard captures the ABSOLUTE-TIME formula (t = i/sampleRate) that NoteSynthesizer uses today — which is NOT bit-identical to SynthUtils' phase-accumulator math; the guard is therefore load-bearing, not a formality"
  - "D-04 is a no-op verification (merge already shipped); no git mv, no path-string edits"
  - "D-09 = KEEP (zero commit); its own removal precondition is unmet because two Facts File.ReadAllText the .txt baselines"
metrics:
  duration: ~6 min
  completed: 2026-05-30
---

# Phase 46 Plan 01: Wave 0 Prerequisites + Byte Guard Summary

Established the D-03 exact-byte safety net (the redirect-proof contract Wave 2 needs) and converted the two "highest priority" audit items (D-04 latent FS bug, D-09 orphan baselines) into recorded green checkmarks — all without mutating a single line of production code.

## What Shipped

- **D-03 prerequisite (CLEAN-03):** `NoteSynthesizerByteGuardTests.cs` — 5 xUnit Facts (4 per-synth + 1 capture-parameter sanity) freezing `Sine/Saw/Square/Triangle` `RenderNote` output as an exact element-wise `float[]` contract. Captured from the current (pre-redirect) `dev` build. **5/5 GREEN.**
- **D-04 (CLEAN-04):** Verified the `Fixtures/`→`fixtures/` merge already shipped (Phase 44). No-op.
- **D-09 (CLEAN-09):** Verified the Phase35 diagnostics `.txt` baselines are live-read → KEEP, zero deletion.

## Task 1 — D-04 + D-09 Verification (no files changed)

**D-04 grep evidence (all as expected):**
- `grep -rn '"Fixtures"|"Fixtures/|/Fixtures/' --include="*.cs" flow-lang.Tests` → **EMPTY**
- `git ls-files | grep 'flow-lang.Tests/Fixtures/'` → **EMPTY**
- `test ! -d flow-lang.Tests/Fixtures && test -d flow-lang.Tests/fixtures` → **success** (no capital dir, lowercase dir present)

D-04 is a no-op for Phase 46: the merge landed in Phase 44 (`e0d7274`, squashed into `5f61a1e`); the audit + STATE.md:809 are STALE on this point. The lone residual `flow-midi.Tests/Fixtures/` is a **different project** with no sibling lowercase `fixtures/` dir → no case-collision risk → out of scope per RESEARCH §D-04 + A2. (`flow-lang.Tests/Integration/Phase38/TestFixtures/` is a distinct `TestFixtures` directory, not a bare `Fixtures`, so it is not a collision either.)

**D-09 live-read evidence:**
- `flow-lang.Tests/Phase35/DiagnosticRendererGoldenTests.cs:39` → `File.ReadAllText(path)`
- `:77` → `ReadBaseline("unknown_identifier.txt")`; `:116` → `ReadBaseline("type_mismatch.txt")`
- Both baselines exist on disk: `flow-lang.Tests/baselines/Phase35/diagnostics/{type_mismatch,unknown_identifier}.txt`

D-09's own escape clause is "REMOVE, but only after confirming the golden test does NOT read the .txt files." The condition is **UNMET** — two `[Fact]`s live-read them — so removal does NOT proceed. **D-09 = KEEP, zero commit.** No file under `flow-lang.Tests/baselines/Phase35/diagnostics/` was touched.

## Task 2 — Exact-Byte Synth Guard (D-03 prerequisite)

**Baseline capture method (so Wave 2 knows exactly what "bit-identical" is measured against):**
The baseline is reconstructed in-test by an independent oracle that replicates the **exact** arithmetic of each current NoteSynthesizer per-synth loop — the **absolute-time** formula `t = i / sampleRate; sample = amplitude * f(frequency * t)` with the per-synth amplitude scalar (Sine/Triangle `0.3 × velocity`, Saw/Square `0.2 × velocity`). Because the oracle mirrors the current code element-for-element, the assertion is GREEN against the pre-redirect build **by construction**, and any deviation introduced by the Wave 2 redirect makes it RED. No external binary baseline file is needed; `baselines/Phase46/.gitkeep` reserves the dir for any future binary capture.

**Fixed (frozen) inputs:** pitch A4 (`'A'`, octave 4, alt 0 → MIDI 69 → exactly 440.0 Hz in 12-TET via `RenderTuning.Default`), `sampleRate=44100`, `durationBeats=1.0`, `bpm=120` (→ 0.5 s → **22050 samples**), `velocity=0.63` (the `MusicalNoteData` default).

**Assertion:** exact bit-pattern compare via `BitConverter.SingleToInt32Bits` (NOT RMS tolerance), reporting the first divergent sample index + both hex bit patterns. The whole point is to fire on a single ±1-ULP IEEE-754 shift that RMS (±0.5 dB / 100 ms) and same-code two-run cmp-clean both miss.

**Why this guard is load-bearing (not a formality):** the current NoteSynthesizer uses the absolute-time formula above, whereas the Wave 2 redirect target `SynthUtils.Generate*` uses an **incremental phase accumulator** (`phase += phaseInc` with a wrap at 1.0). FP rounding of `frequency * (i/sr)` differs from a running sum of `frequency/sr`, so the two formulations are **not guaranteed** bit-identical. If the redirect diverges, this guard goes RED and 46-06 must take the documented fallback: **keep the oscillator loops inline in NoteSynthesizer, redirect only the trivially-identical helpers (`BeatsToSeconds` + `CreateSilence`)** — which removes the duplication that matters without changing a single rendered sample (RESEARCH §D-03 + Open Q2). This rationale is documented in the test's class-level comment.

**No production code touched:** `git status` after the work showed only the two new test artifacts; `NoteSynthesizer.cs` and `SynthUtils.cs` are byte-identical to HEAD.

## Verification

- `dotnet build flow-lang.Tests/flow-lang.Tests.csproj` → 0 errors (81 pre-existing warnings).
- `dotnet build flow-lang/flow-lang.csproj` → 0 errors (8 pre-existing warnings).
- `dotnet test --filter "FullyQualifiedName~NoteSynthesizerByteGuard"` → **Passed! 5/5, 0 failed, 33 ms.**
- `git status --short flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` → no production changes.

## Deviations from Plan

**Plan @-reference `46-RESEARCH.md` is absent from this worktree.** The `<read_first>` blocks and `<context>` reference `.planning/phases/46-codebase-bloat-removal/46-RESEARCH.md`, which does not exist (only `46-CONTEXT.md`, `46-DISCUSSION-LOG.md`, `46-VALIDATION.md` are present). The D-03 byte-risk rationale and D-04/D-09 correction evidence were sourced from `46-CONTEXT.md` + the live codebase (which the plan's `<interfaces>` block had already verified). No information was lost — every claim the plan attributed to RESEARCH was re-derived from primary sources (the actual `NoteSynthesizer.cs` / `SynthUtils.cs` / `DiagnosticRendererGoldenTests.cs` / git history). This is a documentation-reference gap, not a behavior change; tracked here for transparency, no Rule 1/2/3/4 escalation needed.

Otherwise: no deviations. No auto-fixes. No authentication gates. No architectural decisions. Tasks executed exactly as written.

## Known Stubs

None — the byte-guard test is fully wired against real `SynthesizerFactory.Create` dispatch and real `RenderNote` output; no placeholder data.

## D-18 / D-19 Compliance

- **D-18 (phase verification contract):** the byte-guard Fact is the exact-byte backstop the locked test-green gate (full `dotnet test` + `tests/test_*.flow` + Phase 28 RMS baselines + two-run cmp-clean) cannot provide on its own for a before-vs-after ±1-ULP shift.
- **D-19 (pre-traction single-commit latitude):** the one code-bearing target (Task 2) landed as a single atomic commit; no migrators, no shims. Task 1 (D-04/D-09) bore no commit because it is pure verification.

## Self-Check: PASSED

- `flow-lang.Tests/Unit/Phase46/NoteSynthesizerByteGuardTests.cs` → FOUND
- `flow-lang.Tests/baselines/Phase46/.gitkeep` → FOUND
- Commit `c78d3b1` (test 46-01 byte guard) → FOUND
