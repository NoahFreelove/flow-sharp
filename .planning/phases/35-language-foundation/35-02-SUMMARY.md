---
phase: 35-language-foundation
plan: 02
subsystem: housekeeping
tags: [housekeeping, hk-01, hk-02, hk-03, hk-04, humanize-gaussian, voice-block, parallel-voices, mutate-rhythm, claude-md, requirements]
dependency_graph:
  requires: []
  provides: [HK-01-CLOSED, HK-02-CLOSED, HK-03-CLOSED, HK-04-CLOSED, MutateRhythmEnumValuesTests, HumanizeGaussianVoiceBlocksTests, test_humanize_voice_block_flow]
  affects: [TransformFunctions.HumanizeGaussian, VariationFunctions.MutateRhythm, CLAUDE.md, REQUIREMENTS.md, 04-VERIFICATION.md, 17-HUMAN-UAT.md]
tech_stack:
  added: []
  patterns: [parallel-voices-recursion, shared-seeded-rng-for-determinism, internal-visible-to-for-fact-testing]
key_files:
  created:
    - flow-lang.Tests/Phase35/HumanizeGaussianVoiceBlocksTests.cs
    - flow-lang.Tests/Phase35/MutateRhythmEnumValuesTests.cs
    - tests/test_humanize_voice_block.flow
    - .planning/phases/35-language-foundation/35-02-SUMMARY.md
  modified:
    - flow-lang/StandardLibrary/Transforms/TransformFunctions.cs
    - flow-lang/StandardLibrary/Composition/VariationFunctions.cs
    - flow-lang/flow-lang.csproj
    - .planning/REQUIREMENTS.md
    - .planning/phases/17-flow-language-server/17-HUMAN-UAT.md
    - .planning/phases/04-composition-tools/04-VERIFICATION.md
    - CLAUDE.md
decisions:
  - "HumanizeGaussian fix uses post-construction `humanizedParent.ParallelVoices = humanizedVoices` (the existing mutable property at BarType.cs:76) rather than a new BarData ctor overload — keeps the change scoped to TransformFunctions.cs, matches the BarRenderer recursion shape, zero new ctor surface."
  - "MutateRhythm visibility widened private → internal + flow-lang.csproj InternalsVisibleTo `flow-lang.Tests` — preferred over a new public test-API or reflection-based call. Single consumer is the new MutateRhythmEnumValuesTests."
  - "HK-03 source fix was found ALREADY LANDED at audit time — the switch already uses 0→1, 1→2, 2→3, 3→4 (correct NoteValueType enum integers). The 04-VERIFICATION.md gap description was stale by 2026-05-18. Recorded the audit finding in REQUIREMENTS.md HK-03 note and 04-VERIFICATION.md closed_via field; the new xUnit facts serve as anti-regression pins."
  - "COMP-01 / COMP-02 checkbox flips in v1.5 REQUIREMENTS.md were not possible because those requirements were rolled into v1.4 milestone closure — they don't exist in the active v1.5 REQUIREMENTS.md. Documented in 04-VERIFICATION.md closed_via field as 'no longer applies post-milestone-roll'."
metrics:
  duration: ~30min
  completed: 2026-05-19
  tasks_completed: 3
  files_changed: 10
  insertions: 348
  deletions: 25
---

# Phase 35 Plan 02: v1.4 Housekeeping Closeout Summary

Closes all four v1.4 housekeeping carryovers (HK-01..04) in a single
parallel-safe plan: one real bug fix (HK-01 humanizeGaussian dropping voice-block
content), one documentation-confirmation closure (HK-02 Phase 17 UAT already
closed via Phase 31 Plan 31-08), one regression-pin + status flip (HK-03
MutateRhythm enum mapping already correct in source — new xUnit facts pin it),
and one CLAUDE.md prose rewrite (HK-04 footnote aligned to the rewritten
pre-traction no-deprecation external memory framing).

## What Shipped

### HK-01 — humanizeGaussian voice-block fix (the load-bearing bug)

**Root cause:** `TransformFunctions.HumanizeGaussian` at lines 931-962 iterated
only `bar.MusicalNotes` per outer bar and constructed
`new BarData(newNotes, bar.TimeSignature!)`. When a bar carried Phase 28 voice
blocks (`| {voice ...} {voice ...} |`), the inner voices live in
`bar.ParallelVoices` — NOT in `bar.MusicalNotes` — so the construction silently
dropped them. The output BarData had empty MusicalNotes (because originals were
in ParallelVoices) AND empty ParallelVoices (because they weren't copied),
producing header-only 44-byte WAVs after render.

**Fix shape (mirrors BarRenderer.cs:62-77 ParallelVoices recursion):**

1. Extracted `HumanizeBarNotes(bar, amount, rng)` — the original MusicalNotes-only
   loop, now reusable per bar.
2. Added `HumanizeBar(bar, amount, rng)` dispatcher — when `bar.ParallelVoices`
   is non-null, recurses into each voice sub-bar reusing the SAME seeded Random
   instance, then preserves the humanized voices on the output BarData via the
   existing mutable `ParallelVoices` property at BarType.cs:76. Otherwise falls
   through to `HumanizeBarNotes`.
3. Top-level `HumanizeGaussian` becomes a per-bar dispatch loop — the inner
   iteration is byte-identical for non-voice-block bars, so all pre-existing
   tests stay green.

**Determinism contract preserved (T-35-04 mitigation):** single shared seeded
Random across all voices in a bar — NEVER per-voice. Per Phase 18/25 byte-
identical determinism gate, two consecutive seeded runs of
`HumanizeOverVoiceBlockIsDeterministic` produce `SequenceEqual` WAV bytes.

**BarData ParallelVoices assignment path chosen:** post-construction mutable
property assignment (`humanizedParent.ParallelVoices = humanizedVoices`) rather
than a new BarData ctor overload. This keeps the change scoped to
TransformFunctions.cs and matches how Phase 28's BarRenderer already mutates
`voiceBar.TimeSignature` post-construction.

### HK-02 — Phase 17 HUMAN-UAT rows 1-3 closure (cross-reference only)

The file's frontmatter and per-row entries already showed
`status: closed` / `closed_via: Phase 31 Plan 31-08 UAT` /
`result: [pass-via-phase-31-uat]` — all done before Plan 35-02 started, per
RESEARCH §Assumption A9.

**Plan 35-02 contribution:** added an `audit_cross_reference:` line to the
`## Summary` block of 17-HUMAN-UAT.md so the Plan 35-02 confirmation is
auditable from the source file; flipped REQUIREMENTS.md HK-02 checkbox to `[x]`
with cross-reference to Phase 31 Plan 31-08.

### HK-03 — Phase 04 VERIFICATION.md gap closure (audit finding + regression pin)

**Audit finding:** the `MutateRhythm` switch at `VariationFunctions.cs:253` was
**already correct at audit time** — `0 => 1, 1 => 2, 2 => 3, 3 => 4` is the
proper NoteValueType enum mapping (WHOLE→HALF→QUARTER→EIGHTH→SIXTEENTH). The
04-VERIFICATION.md gap entry from 2026-04-02 described a bug shape (`1=>2, 2=>4,
4=>8, 8=>16`) that no longer exists in source. The fix landed silently at an
earlier checkpoint between 2026-04-02 and 2026-05-18.

**Plan 35-02 contribution:**
- `flow-lang.Tests/Phase35/MutateRhythmEnumValuesTests.cs` — 5 xUnit facts
  pinning each enum transition (WHOLE→HALF, HALF→QUARTER, QUARTER→EIGHTH,
  EIGHTH→SIXTEENTH, SIXTEENTH→single-note-fallthrough). T-35-05 mitigation.
- Visibility widened: `MutateRhythm` private → internal + flow-lang.csproj
  `InternalsVisibleTo("flow-lang.Tests")`. Test consumer is exclusive.
- 04-VERIFICATION.md frontmatter flipped from `status: gaps_found` (score
  6/8) to `status: verified` (score 8/8) with `closed_via:` field documenting
  the enum-mismatch audit AND the COMP-* doc-staleness resolution.
- REQUIREMENTS.md HK-03 checkbox flipped to `[x]`.

**Confirmation: NoteValueType integer values** (from `NoteType.cs:24-29` +
`notation.flow:28-29`): WHOLE=0, HALF=1, QUARTER=2, EIGHTH=3, SIXTEENTH=4 —
matches the switch literal values exactly.

### HK-04 — CLAUDE.md footnote rewrite

**Diff summary:**
- Header revised from `Note (Public as of v1.4):` → `Note (Public as of v1.4,
  pre-traction):` to signal latitude status at-a-glance.
- Prose rewritten from "deprecation cycle now applies" → "no-deprecation
  latitude REMAINS ACTIVE through pre-traction; breaking changes still ship
  in single commits; in-repo migrators only; no `flow migrate` CLI subcommand
  required yet."
- Added explicit list of all four revisit triggers from the rewritten external
  memory (non-author composer issue/PR, third-party fork, user-observed
  traction signals, package-registry install path).
- Cross-reference updated from `(rewritten 2026-05-16)` → `(rewritten
  2026-05-17)` matching the external memory's rewrite date.
- REQUIREMENTS.md HK-04 checkbox flipped to `[x]`.

## Test Results

| Test set | Before fix | After fix |
|----------|-----------|-----------|
| HumanizeGaussianVoiceBlocksTests (2 facts) | 2 FAIL (44-byte WAV) | 2 PASS |
| MutateRhythmEnumValuesTests (5 facts) | 5 PASS (regression pins) | 5 PASS |
| tests/test_humanize_voice_block.flow WAV | 44 bytes | 352,844 bytes |
| Phase 25 ByteIdenticalShowcaseGaussian (4 facts) | 4 PASS | 4 PASS |
| Full xUnit suite (1283 facts) | 1257 PASS / 26 FAIL | 1257 PASS / 26 FAIL |

The 26 baseline failures are entirely in Phase 28 instrument-rendering tests
(`PerSynthArticulationTests.PerSynthArticulation_NormalVsArticulated_FFTCosineDifferentiable`
parameterized facts, plus `RagtimeFixtureTests.Ragtime_MapleLeaf_RmsRegression`
and `Ragtime_Synthetic_RmsRegression`). All 26 failed identically on the pre-fix
baseline (verified by `git stash` + re-run) — none are caused by Plan 35-02
changes. See **Deferred Issues** below.

## Deferred Issues

These pre-existing test failures were detected during Plan 35-02 verification
but are OUT OF SCOPE for this plan (they are not caused by HK-01/03 changes,
and the plan's job is voice-block + enum-mapping closure, not Phase 28
instrument timbre regression):

- **Phase 28 PerSynthArticulationTests** (24 parameterized facts) —
  `PerSynth_NormalVsArticulated_FFTCosineDifferentiable` for piano / brass /
  bell / strings / sax / flute × Accent / Legato / Tenuto / Sforzando.
  Cosine value 0.0000 indicates one of the rendered buffers is silent —
  unrelated to humanize behavior. Likely a Phase 28 articulation-envelope
  regression that needs its own debug plan.
- **Phase 28 RagtimeFixtureTests** (2 facts) — RMS deviations of 1.07 dB and
  0.90 dB exceed the locked ±0.5 dB tolerance. Either the locked baselines
  need refresh against the current synthesis stack, or there is an upstream
  perceptual regression. Needs its own debug/refresh plan.

Both should be triaged by a separate plan; they predate Plan 35-02 and were
unaffected by the HK-01 fix.

## Deviations from Plan

### Rule 3 — Plan acceptance criterion stale vs current source

**1. HK-03 source fix was ALREADY LANDED at audit time**

- **Found during:** Task 1 (writing the RED regression tests for HK-03)
- **Issue:** Plan 35-02's Task 1 acceptance criterion specified that
  `MutateRhythmEnumValuesTests` should "report FAIL on at least one fact
  (expected RED — switch returns wrong values pre-fix)". When run, all 5
  facts PASSED immediately because the switch in `VariationFunctions.cs:253`
  already uses the correct NoteValueType enum integers (`0=>1, 1=>2, 2=>3,
  3=>4`). The `04-VERIFICATION.md` gap description from 2026-04-02 was stale
  by 2026-05-18 — the bug was silently fixed at an earlier checkpoint.
- **Fix:** Reframed the MutateRhythmEnumValuesTests as **regression pins**
  (T-35-05 mitigation) rather than RED-then-GREEN bug fixes. The facts still
  fulfill the plan's HK-03 goal — they prevent the documented enum-mismatch
  bug from ever re-appearing. The audit finding is recorded in REQUIREMENTS.md
  HK-03 note and 04-VERIFICATION.md `closed_via:` field.
- **Files modified:** None additional — this only changed the framing of
  existing Task 1 + Task 3 work, not the file set.
- **Commit:** 56cb53a (test) + fc3a6d7 (docs)

### Rule 3 — Plan acceptance grep targets v1.4 REQUIREMENTS.md sections

**2. COMP-01 / COMP-02 checkbox flips not possible in v1.5 REQUIREMENTS.md**

- **Found during:** Task 3 (locating COMP-01 / COMP-02 in REQUIREMENTS.md)
- **Issue:** Plan 35-02 Task 3 acceptance criterion required
  `grep -cE "\[x\] \*\*COMP-0[12]" .planning/REQUIREMENTS.md` to return 2.
  COMP-01 / COMP-02 do not exist in the active v1.5 REQUIREMENTS.md — they
  were v1.4 requirements that rolled into v1.4 milestone closure when the v1.5
  REQUIREMENTS.md was written 2026-05-17. The plan author wrote the
  acceptance criterion against pre-roll planning state.
- **Fix:** Documented the milestone-roll resolution in 04-VERIFICATION.md
  `closed_via:` field — explicitly states the original line-number-specific
  checkbox flips no longer apply post-milestone-roll. The HK-03 closure is
  complete because the underlying source code is correct, regression-pinned,
  and the verification file's status is flipped.
- **Files modified:** None additional — the plan's other Task 3 acceptance
  criteria all met (`HK-0[1-4]` returns 4, `status: verified` returns 1,
  `pre-traction` returns 2, `Phase 31 Plan 31-08` returns 1).
- **Commit:** fc3a6d7

### Rule 2 — Added InternalsVisibleTo to flow-lang.csproj

**3. flow-lang.csproj gained an InternalsVisibleTo("flow-lang.Tests") entry**

- **Found during:** Task 1 (writing MutateRhythmEnumValuesTests)
- **Issue:** `MutateRhythm` was `private static`. Testing it through the
  full `vary()` stochastic stack would require probability=1.0 + multiple
  bar fixtures + assertion against bar.MusicalNotes — far more brittle than
  a direct method-level fact set.
- **Fix:** Widened `MutateRhythm` visibility from `private` → `internal` +
  added a small `<InternalsVisibleTo Include="flow-lang.Tests" />` ItemGroup
  to flow-lang.csproj. Single consumer is `MutateRhythmEnumValuesTests`.
  Plan's `files_modified` list already includes
  `flow-lang/StandardLibrary/Composition/VariationFunctions.cs` so the
  visibility change is in-scope; the csproj edit is a small additive
  facility for the visibility change.
- **Files modified:** flow-lang/flow-lang.csproj (one new ItemGroup)
- **Commit:** 56cb53a

## Authentication Gates

None occurred. This plan touched only source code, planning artifacts, and
the existing `CLAUDE.md` — no external services, package installs, network
calls, or credential handling.

## Self-Check

- [x] FOUND: flow-lang.Tests/Phase35/HumanizeGaussianVoiceBlocksTests.cs
- [x] FOUND: flow-lang.Tests/Phase35/MutateRhythmEnumValuesTests.cs
- [x] FOUND: tests/test_humanize_voice_block.flow
- [x] FOUND: 56cb53a — test(35-02): add HK-01 + HK-03 regression facts
- [x] FOUND: 567d7f2 — fix(35-02): HK-01 — humanizeGaussian recurses into ParallelVoices
- [x] FOUND: fc3a6d7 — docs(35-02): HK-02 + HK-03 + HK-04 closures
- [x] FOUND: grep returns 4 HK [x] checkboxes in REQUIREMENTS.md
- [x] FOUND: grep returns "status: verified" in 04-VERIFICATION.md
- [x] FOUND: grep returns "pre-traction" in CLAUDE.md (×2)
- [x] FOUND: 7/7 Phase 35 HK xUnit facts PASS after fix
- [x] FOUND: WAV grew 44 bytes → 352,844 bytes after fix
- [x] FOUND: 4/4 Phase 25 byte-identical determinism facts PASS

## Self-Check: PASSED
