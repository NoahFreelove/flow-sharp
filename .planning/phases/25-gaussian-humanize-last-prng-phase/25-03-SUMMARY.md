---
phase: 25-gaussian-humanize-last-prng-phase
plan: 03
subsystem: examples-and-integration-tests
tags: [showcase, tutorial, integration, byte-identical, phase-25, wave-3, defer-06]

# Dependency graph
requires:
  - phase: 25-00
    provides: "FlowScriptData entry for test_humanize_gaussian.flow + Skip-marked ByteIdenticalShowcaseGaussianTests skeleton + Wave 0 placeholder smoke .flow"
  - phase: 25-01
    provides: "MusicalNoteData.With(velocity:) slot — used by humanizeGaussian under-the-hood"
  - phase: 25-02
    provides: "humanizeGaussian(Sequence, Double, Int) registered + std.flow declaration + 7 GREEN HumanizeGaussianFacts"
  - phase: 18
    provides: "ByteIdenticalTutorialTests + ByteIdenticalShowcaseTests run-to-run identity gate (preserved unchanged)"

provides:
  - "Additive humanizeGaussian call site on examples/showcase.flow melody Sequence (seed=314, amount=0.08)"
  - "examples/tutorial.flow Section 18.5 'Gaussian Humanize' top-level chapter with uniform-vs-Gaussian contrast (seed=42)"
  - "Real two-run byte-identical humanizeGaussian smoke in tests/test_humanize_gaussian.flow (replaces Wave 0 placeholder)"
  - "2 GREEN ByteIdenticalShowcaseGaussianTests Facts (D-21/D-24): WAV + MIDI run-to-run byte-identical for showcase.flow"
  - "Full xUnit suite: 688 passed, 0 failed, 0 skipped (was 686 passed + 2 skipped — the 2 unskipped pre-existing Phase 25 placeholders are now live and GREEN)"

affects: [25-04-integration-validation, phase-25-closure]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Additive transform wrap on existing Sequence: (humanizeGaussian | ... | amount seed) — preserves the v1.2 byte-identical baseline by not touching any other transform call site"
    - "Top-level tutorial chapter convention with own tempo/timesig wrapper, .Note: ----- divider, (print) heading, S-expression call style"
    - "Two-run byte-identical .flow smoke: writeMidi to *_a.mid then writeMidi to *_b.mid with same seed; cmp byte-identical externally; both sentinels print"
    - "Phase 25 integration test self-re-pinning: Skip removal alone activates the Facts because the showcase.flow edit is already in place (Task 1 ordered first)"

key-files:
  created:
    - .planning/phases/25-gaussian-humanize-last-prng-phase/25-03-SUMMARY.md
  modified:
    - examples/showcase.flow
    - examples/tutorial.flow
    - tests/test_humanize_gaussian.flow
    - flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs

key-decisions:
  - "D-20 enforced: ONE additive humanizeGaussian call site wraps the existing melody Sequence at examples/showcase.flow:20 with seed=314 and amount=0.08; NO other transforms touched (pad/padBase/pulse/crescendo/reverbTime/writeWav/writeMidi all preserved verbatim)."
  - "D-22 enforced: Section 18.5 'Gaussian Humanize' inserted as a top-level chapter between Section 18 (Euclidean Rhythms) and Section 19 (Voice Synthesis); chapter has own tempo/timesig wrapper; demonstrates uniform-vs-Gaussian contrast per CONTEXT specifics with seed=42 deterministic example."
  - "D-21/D-24 enforced: Skip markers removed from both ByteIdenticalShowcaseGaussianTests Facts; the RunTwiceAndCompare body was unchanged from Wave 0; bytes1.SequenceEqual(bytes2) holds for both WAV and MIDI."
  - "D-19 invariant preserved: Phase 18 ByteIdenticalShowcaseTests + ByteIdenticalTutorialTests stay GREEN under self-re-pinning semantics (assertion is run-to-run identity, not against frozen v1.2 bytes)."
  - "Renumbering avoided: chose '18.5' sibling-decimal over renumbering Section 19+ to minimise churn across the rest of tutorial.flow."

patterns-established:
  - "Additive transform wrap: when adding a new transform to a frozen example file, wrap an existing Sequence in-place with a single-line edit; do NOT replace any other transform; verify by grep counts on the existing call sites."
  - "Sibling-decimal chapter insertion: when adding a new tutorial chapter between existing chapters, use a sibling-decimal label (e.g., '18.5' between 18 and 19) instead of renumbering everything downstream."
  - "Skip-flip for pre-staged integration tests: Wave 0 stages the test skeleton with Skip markers and a complete RunTwiceAndCompare body; the live wave only removes Skip — implementation completeness is verified by the test passing on first un-skip."

requirements-completed: [DEFER-06]

# Metrics
duration: 4m13s
completed: 2026-05-04
---

# Phase 25 Plan 03: humanizeGaussian Showcase + Tutorial + Integration Wiring Summary

**Wired `humanizeGaussian` into the user-facing example surfaces and flipped the Phase 25 integration tests live: ONE additive call site on `examples/showcase.flow:20` (melody, seed=314, amount=0.08), a Section 18.5 'Gaussian Humanize' top-level chapter inserted between Section 18 and Section 19 of `examples/tutorial.flow`, the Wave 0 placeholder in `tests/test_humanize_gaussian.flow` replaced with a real two-run byte-identical smoke, and the `ByteIdenticalShowcaseGaussianTests` Facts unskipped — all GREEN. Full xUnit suite: 688 passed, 0 failed, 0 skipped.**

## Performance

- **Duration:** ~4m13s (execution-only; CPU-bound by `dotnet test` runs)
- **Started:** 2026-05-04T23:25:24Z
- **Completed:** 2026-05-04T23:29:37Z
- **Tasks:** 4
- **Files modified:** 4 (showcase.flow, tutorial.flow, test_humanize_gaussian.flow, ByteIdenticalShowcaseGaussianTests.cs)
- **Files created:** 0 (all four target files pre-existed; this plan modifies only)

## Accomplishments

- `examples/showcase.flow` now contains exactly ONE additive `humanizeGaussian` call wrapping the melody Sequence at line 20 with `(humanizeGaussian | mp _ _ E5q G5q | A5h E5h | _ _ G5q B5q | A5w | 0.08 314)`. All other transforms are preserved verbatim.
- `examples/tutorial.flow` has a new top-level Section 18.5 'Gaussian Humanize' chapter inserted between Section 18 (line 524 closing `}`) and Section 19 (line 526 `Note: -----`). Chapter contains a uniform-vs-Gaussian contrast example with seed=42.
- `tests/test_humanize_gaussian.flow` has been upgraded from the Wave 0 placeholder body (just two prints) to a real two-run smoke that calls `humanizeGaussian baseSeq 0.1 42` twice and writes byte-identical MIDI files. Both FlowScriptData sentinels still print.
- `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` has both Skip markers removed; the two Facts (`Showcase_TwoRunsProduceIdenticalWav`, `Showcase_TwoRunsProduceIdenticalMidi`) are GREEN with the unchanged Wave 0 RunTwiceAndCompare body.
- D-19 byte-identical regression invariant verified empirically: Phase 18 `ByteIdenticalShowcaseTests` + `ByteIdenticalTutorialTests` 19/19 GREEN. The tutorial.flow chapter addition does NOT break two-run identity because humanizeGaussian with `seed=42` is deterministic.
- `cmp tests/output/phase25_humanize_gaussian_a.mid tests/output/phase25_humanize_gaussian_b.mid` exits 0 (byte-identical, externally verified).
- Full xUnit suite GREEN: **688 passed, 0 failed, 0 skipped** (up from Plan 25-02's 686 passed + 2 skipped — the 2 freshly-unskipped Facts are in this delta).

## Task Commits

Each task was committed atomically on the worktree branch `worktree-agent-a3d7fe6021bfed9b8`.

1. **Task 1: Wrap melody with humanizeGaussian in examples/showcase.flow (D-20)** — `24fd415` (feat)
   - Single-line replacement at `examples/showcase.flow:20`.
   - `grep -c humanizeGaussian` returns 1; `grep -c "0.08 314"` returns 1; pad/pulse/writeWav/writeMidi all UNTOUCHED.
   - `dotnet run --project flow-interpreter examples/showcase.flow` exits 0; WAV+MIDI written.
2. **Task 2: Append humanizeGaussian chapter 18.5 to tutorial.flow (D-22)** — `ab08b37` (feat)
   - 25-line top-level chapter inserted between Section 18's closing `}` and Section 19's `Note: -----` divider.
   - Contains `(humanize myMelody 0.1)` (uniform contrast) and `(humanizeGaussian myMelody 0.1 42)` (Gaussian, deterministic).
   - `awk` ORDER_OK check confirms 18 < 18.5 < 19 ordering.
   - Phase 18 byte-identical regression: 19/19 GREEN.
3. **Task 3: Replace test_humanize_gaussian.flow Wave 0 placeholder with real two-run smoke** — `8be8c66` (test)
   - Wave 0 placeholder body (just two prints) replaced with humanizeGaussian + writeMidi two-run pattern mirroring `tests/test_euclidean_humanize.flow`.
   - Both FlowScriptData sentinels still print.
   - `cmp` between the two MIDI outputs is byte-identical.
   - FlowScriptTests: 83/83 GREEN.
4. **Task 4: Flip Phase25 ByteIdenticalShowcaseGaussianTests Facts from Skip to live (D-21/D-24)** — `5169db8` (test)
   - Two Skip-marker removals; no other body changes.
   - `Showcase_TwoRunsProduceIdenticalWav` GREEN; `Showcase_TwoRunsProduceIdenticalMidi` GREEN.
   - Phase 25 filter: 13/13 GREEN; Phase 18 filter: 19/19 GREEN.

## Verification Results

| Check | Expected | Actual |
|-------|----------|--------|
| `dotnet build` | green | **green** (0 errors, 12 warnings — all pre-existing) |
| `dotnet run --project flow-interpreter examples/showcase.flow` | exit 0, no errors, WAV+MIDI written | **OK** (both files written) |
| `dotnet run --project flow-interpreter examples/tutorial.flow` | exit 0, no errors, prints `--- 18.5 Gaussian Humanize ---` | **OK** |
| `dotnet run --project flow-interpreter tests/test_humanize_gaussian.flow` | both sentinels print | **OK** |
| `cmp tests/output/phase25_humanize_gaussian_{a,b}.mid` | byte-identical (exit 0) | **byte-identical** |
| ByteIdenticalShowcaseGaussianTests | 2 passed | **2 passed** |
| HumanizeGaussianFacts (Plan 25-02 regression) | 7 passed | **7 passed** |
| Phase 25 filter | 13 passed (11 unit + 2 integration) | **13 passed, 0 failed, 0 skipped** |
| Phase 18 filter (D-19 invariant) | 19 passed | **19 passed** |
| FlowScriptTests | all green | **83 passed, 0 failed** |
| Full `flow-lang.Tests` suite | 0 failed, 0 skipped | **688 passed, 0 failed, 0 skipped** |
| `grep -c humanizeGaussian examples/showcase.flow` | exactly 1 | **1** |
| `grep -c "Skip = " ByteIdenticalShowcaseGaussianTests.cs` | exactly 0 | **0** |
| `awk` ORDER 18 < 18.5 < 19 in tutorial.flow | ORDER_OK | **ORDER_OK** |

## Deviations from Plan

**Two minor deviations, both documentation-only — no functional impact:**

### [Doc - acceptance criterion language] grep count for filenames in test smoke (Task 3)

- **Found during:** Task 3 acceptance criteria check.
- **Issue:** The plan specified `grep -c "phase25_humanize_gaussian_a.mid" tests/test_humanize_gaussian.flow` returns exactly `1`. Actual count was `2` because the filename appears twice: once in the descriptive comment block at the top of the file (which is verbatim from PATTERNS.md §test_humanize_gaussian.flow) and once in the actual `(writeMidi ...)` call. The PATTERNS.md exemplar (`tests/test_euclidean_humanize.flow`) has the same structure: each `_a.mid`/`_b.mid` filename appears once in the comment header and once in the writeMidi call.
- **Resolution:** Used the file content verbatim from PATTERNS.md (which is the canonical reference), and the deeper behavioural acceptance — exactly one `writeMidi` call per filename, byte-identical output, both sentinels printing — is fully satisfied. The grep count of `2` reflects the comment header + call site structure, not a bug.
- **Files modified:** None (no source change — only the acceptance-criterion interpretation).
- **Commit:** None (verification-only deviation).

### [Doc - duplicated step] Action duplicates "Step 3" / "Step 4" in Task 2

- **Found during:** Reading Task 2's `<action>` block.
- **Issue:** The plan's Task 2 `<action>` block lists Steps 1, 2, 3, 4 followed by a re-stated Step 3 + Step 4 (identical content). Likely a copy-paste artefact during planning.
- **Resolution:** Executed the unique steps once (1, 2, 3, 4) and verified the corresponding acceptance criteria.
- **Files modified:** None.
- **Commit:** None.

Otherwise the plan executed exactly as written. All D-19, D-20, D-21, D-22, D-24 invariants hold; the Phase 18 byte-identical regression remains GREEN; the FROZEN existing humanize block in TransformFunctions.cs was not touched (out of scope for this plan).

## Threat Flags

None. The threat model in 25-03-PLAN.md (T-25-03-01..T-25-03-05) is fully covered:

- **T-25-03-01 (Tampering — non-additive showcase change):** mitigated. `grep -c humanizeGaussian examples/showcase.flow` returns exactly 1; pad/padBase/pulse/euclidean/writeWav/writeMidi grep gates all return their expected counts; no other transforms were touched.
- **T-25-03-02 (Tampering — tutorial.flow chapter breaks parse):** mitigated. `dotnet run --project flow-interpreter examples/tutorial.flow` exits 0, prints all expected output; Phase 18 ByteIdenticalTutorialTests stays GREEN (covered by Phase 18 filter pass).
- **T-25-03-03 (Tampering — humanizeGaussian non-determinism leaks via tutorial.flow):** mitigated. Tutorial chapter uses `humanizeGaussian myMelody 0.1 42` (fixed seed); Phase 18 byte-identical regression is the regression sentinel and is GREEN.
- **T-25-03-04 (Information Disclosure — none):** accept. No PII/secrets surface; example files are open source.
- **T-25-03-05 (DoS — disk usage):** accept. MIDI files are sub-KB; tests/output/ exists from Wave 0; subsequent runs overwrite.

No new threat surface introduced.

## Self-Check: PASSED

**Files claimed in summary:**
- `examples/showcase.flow` (modified) — confirmed via `git log --name-only 24fd415`.
- `examples/tutorial.flow` (modified) — confirmed via `git log --name-only ab08b37`.
- `tests/test_humanize_gaussian.flow` (modified) — confirmed via `git log --name-only 8be8c66` and `git ls-files`.
- `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` (modified) — confirmed via `git log --name-only 5169db8`.

**Commits claimed in summary:**
- `24fd415` (Task 1) — present in `git log`.
- `ab08b37` (Task 2) — present in `git log`.
- `8be8c66` (Task 3) — present in `git log`.
- `5169db8` (Task 4) — present in `git log`.

All claims verified.
