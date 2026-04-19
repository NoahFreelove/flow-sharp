---
phase: 13-nyquist-validation-backfill
plan: 01
subsystem: testing
tags: [xunit, validation, nyquist, phase-6, regression-gates, two-pass-strict]

requires:
  - phase: 06-diagnostics-bug-fixes
    provides: QOL-01 --verbose flag, FIX-01 Sequence overload, FIX-02 bare expressions in sections (incl. gain-nested), FIX-03 fatal/non-fatal error distinction
  - phase: 12-stability
    provides: flow-lang.Tests xUnit harness, FlowEngineRunner fixture, FlowScriptData Theory catalog, ExpectedErrorScripts/RequiredSentinels dictionaries

provides:
  - Retroactive 06-VALIDATION.md at nyquist_compliant: true documenting Phase 6 validation strategy
  - flow-lang.Tests/Integration/Phase06/VerboseFlagTests.cs (QOL-01 positive+negative stderr-prefix Facts)
  - flow-lang.Tests/Integration/Phase06/SectionGainBareExpressionTests.cs (FIX-02 × AUDIO-06 numeric-frame-count regression gate)
  - Tightened FlowScriptData.RequiredSentinels[test_transpose_int.flow] pin for FIX-01
  - flow-lang.Tests/Integration/Phase06/ directory (NEW test-layout convention)

affects: [13-02, 13-03, 13-04, 13-05]

tech-stack:
  added: []
  patterns:
    - "Two-pass strict authorship (D-13): Pass 1 reads REQUIREMENTS + ROADMAP only; Pass 2 reconciles against shipped code and logs drift in ## Divergences"
    - "Numeric-frame-count regression gates over stdout sentinels for audio behavior (FIX-02 × AUDIO-06 example)"
    - "flow-lang.Tests/Integration/Phase{NN}/ directory layout for phase-scoped Integration Facts"
    - "Pass 1 Draft + Pass 2 Implementation Map + Divergences tri-section audit trail in backfilled VALIDATION.md"

key-files:
  created:
    - .planning/phases/06-diagnostics-bug-fixes/06-VALIDATION.md
    - flow-lang.Tests/Integration/Phase06/VerboseFlagTests.cs
    - flow-lang.Tests/Integration/Phase06/SectionGainBareExpressionTests.cs
  modified:
    - flow-lang.Tests/FlowScriptData.cs

key-decisions:
  - "[Plan 13-01] Two-pass strict authorship produced zero Divergences on Phase 6 because the v1.1 audit had already reconciled REQUIREMENTS.md with shipped behavior (FIX-02 × AUDIO-06 composition gap surfaced and fixed via commit 2156690 pre-milestone-close). Pass 1 draft and Pass 2 reality matched verbatim across QOL-01 prefix, FIX-01 sentinels, FIX-02 gain-nested script body, FIX-03 error string"
  - "[Plan 13-01] FIX-02 × AUDIO-06 regression gate authored as stdout numeric-frame assertion (Assert.DoesNotContain \"frames: 0\\n\") rather than replacing the .flow sentinel script, because the pre-fix bug silently rendered 0 frames while still printing 'PASSED'. The new Fact fails under the pre-fix bug; the existing .flow Theory row wouldn't have"
  - "[Plan 13-01] Rule 2 addition: VerboseFlagTests.RunSource_WithoutVerbose_DoesNotWriteVerbosePrefix pins the negative case (verbose=false ⇒ no [verbose] prefix). Pass 1 draft only specified positive case; without the negative pin a future refactor emitting [verbose] unconditionally would still pass QOL-01 regression. Logged in Divergences under Rule 2"
  - "[Plan 13-01] FIX-01 sentinel tightening uses RequiredSentinels append (not a new Fact) because test_transpose_int.flow already runs via Plan 12-01's Theory harness with errorCount==0 gate; additive sentinel ('transpose with int: ok' + 'test_transpose_int: PASSED') converts the row from errorCount-only to substring-pinned without duplicating the script"

patterns-established:
  - "Pattern: Backfilled VALIDATION.md carries frontmatter key `backfilled: true` to distinguish from phase-contemporaneous VALIDATION.md files"
  - "Pattern: ## Pass 1 Draft section is preserved verbatim after Pass 2 (do NOT rewrite to match reality); divergences surface as ## Divergences entries"
  - "Pattern: Empirical sentinel capture — run `dotnet run --project flow-interpreter tests/<script>.flow` and copy EXACT stdout strings into RequiredSentinels, never infer"
  - "Pattern: Numeric assertions in Integration Facts use in-process FlowEngineRunner.RunSource with $\"frames: {(str frames)}\" string interpolation; assertion is Assert.DoesNotContain(\"frames: 0\\n\", stdout) to survive DSP refactors"

requirements-completed: [TEST-04]

duration: 5min
completed: 2026-04-20
---

# Phase 13 Plan 01: Nyquist Validation Backfill — Phase 6 Summary

**Retroactive Phase 6 VALIDATION.md authored two-pass strict (QOL-01/FIX-01/FIX-02 incl. gain-nested/FIX-03) with new FIX-02 × AUDIO-06 numeric-frame regression gate and QOL-01 verbose-prefix Facts; 71/71 test suite green at nyquist_compliant: true**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-04-20T02:56:22Z
- **Completed:** 2026-04-20T03:00:55Z
- **Tasks:** 2 (Pass 1 draft, Pass 2 reality check + test authoring + promotion)
- **Files modified:** 4 (1 new VALIDATION.md, 2 new Fact files, 1 sentinel append)

## Accomplishments

- Retroactive `.planning/phases/06-diagnostics-bug-fixes/06-VALIDATION.md` lands at `nyquist_compliant: true`, closing the Phase 6 documentation-lag tech debt carried out of v1.1 close
- FIX-02 × AUDIO-06 regression gate installed as a NUMERIC frame-count assertion (`SectionGainBareExpressionTests.GainNestedInSection_RendersNonZeroFrames`) — the pre-fix bug at commit 2156690's parent silently rendered 0 frames while printing "PASSED" through the existing .flow sentinel; this Fact would fail under that regression
- QOL-01 `--verbose` flag pinned via two Facts (positive: `[verbose] Executing` substring present when verbose=true; negative: prefix absent when verbose=false) — Rule 2 addition strengthens the gate beyond Pass 1's positive-only draft
- FIX-01 `test_transpose_int.flow` Theory row tightened from errorCount-only to substring-pinned via additive `RequiredSentinels` entry (no new Fact class needed)
- FIX-03 coverage documented via citation of existing `ExpectedErrorScripts["test_error_masking.flow"]` (no new code)
- FIX-04 row documented as INVALID per v1.1-MILESTONE-AUDIT 2026-04-18 — no test authored
- Two-pass strict authorship produced zero Divergences on Phase 6: Pass 1 requirements-derived draft and Pass 2 shipped-codebase reality matched verbatim across all four active requirements
- New `flow-lang.Tests/Integration/Phase06/` directory convention established for phase-scoped Integration Facts (replicable for 13-02..13-04)

## Task Commits

Each task was committed atomically:

1. **Task 1: Pass 1 — Requirements-only 06-VALIDATION.md draft** — `ff901fa` (docs)
2. **Task 2a: Pass 2 — Author Phase 6 validation Facts + sentinel** — `4cf0ccd` (test)
3. **Task 2b: Pass 2 — Promote 06-VALIDATION.md to nyquist_compliant** — `39d53f3` (docs)

## Files Created/Modified

- `.planning/phases/06-diagnostics-bug-fixes/06-VALIDATION.md` — NEW retroactive VALIDATION.md at nyquist_compliant: true with Pass 1 Draft + Pass 2 Implementation Map + Divergences audit trail
- `flow-lang.Tests/Integration/Phase06/VerboseFlagTests.cs` — NEW 2 Facts pinning QOL-01 `--verbose` stderr prefix (positive + negative cases)
- `flow-lang.Tests/Integration/Phase06/SectionGainBareExpressionTests.cs` — NEW 1 Fact pinning FIX-02 × AUDIO-06 numeric frame count via `$"frames: {(str frames)}"` stdout assertion (`Assert.DoesNotContain("frames: 0\n", stdout)`)
- `flow-lang.Tests/FlowScriptData.cs` — MODIFIED: append `RequiredSentinels["test_transpose_int.flow"]` entry with "transpose with int: ok" + "test_transpose_int: PASSED" sentinels for FIX-01

## Decisions Made

- **Rule 2 addition** — added a second Fact `VerboseFlagTests.RunSource_WithoutVerbose_DoesNotWriteVerbosePrefix` to pin the negative case. Pass 1 draft only specified positive case; without the negative pin a future refactor emitting `[verbose]` unconditionally would still pass QOL-01 regression. Logged in `## Divergences` of the VALIDATION.md.
- **FIX-01 via sentinel append (not new Fact)** — `test_transpose_int.flow` already runs via Plan 12-01's Theory harness with `errorCount==0` gate; tightening to substring-pinned required only a `RequiredSentinels` entry, not a new Fact class. Matches D-19 (existing coverage wins).
- **FIX-02 × AUDIO-06 via numeric-pin, not .flow-script fix** — the audit's observation was that the pre-fix .flow script printed "PASSED" regardless of frame count, masking the 0-frame regression. A new Fact with `Assert.DoesNotContain("frames: 0\n", stdout)` fails under the pre-fix bug; the existing .flow Theory row does not. Both assets remain — the Fact is the authoritative regression gate.
- **No script-body substitution** — Pass 1's literal Flow source (`section s { gain 0.5 { | C4 D4 E4 F4 | } } Song sg = [s] Buffer b = (renderSong sg "sine") Int frames = (getFrames b) (print $"frames: {(str frames)}")`) parses and executes at HEAD. All of `renderSong`, `getFrames`, `$"..."` interpolation, and `Song`/`Buffer`/`Int` bindings are shipped stdlib.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical Coverage] Added QOL-01 negative-case Fact**
- **Found during:** Task 2 (Pass 2 — test authoring)
- **Issue:** Pass 1 draft only specified the positive case for QOL-01 (verbose=true ⇒ `[verbose]` prefix in stderr). Without pinning the negative case (verbose=false ⇒ no `[verbose]` prefix), a future refactor that always emits `[verbose]` output regardless of the flag would still pass the positive Fact, silently breaking the QOL-01 opt-in contract.
- **Fix:** Added `VerboseFlagTests.RunSource_WithoutVerbose_DoesNotWriteVerbosePrefix` as a companion `[Fact]` in the same file. `Assert.DoesNotContain("[verbose]", stderr)` when `FlowEngineRunner(verbose: false)`.
- **Files modified:** `flow-lang.Tests/Integration/Phase06/VerboseFlagTests.cs`
- **Verification:** `dotnet test --filter "FullyQualifiedName~VerboseFlagTests"` reports 2/2 Passed.
- **Committed in:** `4cf0ccd` (Task 2a commit)
- **Documented in:** `## Divergences` of `06-VALIDATION.md`

---

**Total deviations:** 1 auto-fixed (1 Rule 2 missing critical coverage)
**Impact on plan:** Additive strengthening of regression gate. No scope creep; no production source touched.

## Issues Encountered

None. Two-pass strict workflow executed as designed. The v1.1 audit had already reconciled REQUIREMENTS.md with shipped behavior (FIX-02 × AUDIO-06 gap surfaced and fixed pre-close via commit 2156690), so Pass 1 requirements-derived assertions and Pass 2 shipped-codebase reality matched verbatim. Zero script-body substitutions, zero assertion-text adjustments.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- **13-02 (Phase 7 VALIDATION.md)**: same two-pass strict pattern replicates. Directory convention `flow-lang.Tests/Integration/Phase07/` follows 13-01 precedent.
- **13-03 (Phase 8 VALIDATION.md)**: AUDIO-06 per-section gain is partly covered here; 13-03 Pass 1 may cite the 13-01 Fact for the composition-gap case and author Phase-8-specific pins for the in-section gain-ratio semantics.
- **13-04 (Phase 9 VALIDATION.md)**: independent (tempoRamp + QOL-02 tutorial).
- **13-05 (Phase 10 promotion + TEST-04 closure)**: final plan in phase; updates REQUIREMENTS.md traceability for TEST-04 after all four backfills ship.

### Threat Surface Scan

No new security-relevant surface introduced. Pure docs + test authoring. No network I/O, no subprocess invocation, no file writes outside the test project directories, no user-input handling. Threat register entry `T-13-01-01` disposition `accept` (severity: none) holds.

### Known Stubs

None. Every test assertion pins a concrete observable value (stderr substring, stdout substring, or numeric-frame-count absence pattern). No placeholder text, no hardcoded empty values, no `TODO`/`FIXME` markers.

### Verification Evidence

- `dotnet test flow-sharp.sln --filter "FullyQualifiedName~FlowLang.Tests.Integration.Phase06"` → `Failed: 0, Passed: 3, Total: 3` (VerboseFlag×2 + SectionGainBareExpression×1)
- `dotnet test flow-sharp.sln` → `Failed: 0, Passed: 71, Total: 71, Duration: 14s` (baseline 68 + 3 new Facts; no regressions)

## Self-Check

- `test -f .planning/phases/06-diagnostics-bug-fixes/06-VALIDATION.md` → FOUND
- `test -f flow-lang.Tests/Integration/Phase06/VerboseFlagTests.cs` → FOUND
- `test -f flow-lang.Tests/Integration/Phase06/SectionGainBareExpressionTests.cs` → FOUND
- `grep -q "test_transpose_int.flow" flow-lang.Tests/FlowScriptData.cs` → FOUND
- `grep -q "^nyquist_compliant: true$" .planning/phases/06-diagnostics-bug-fixes/06-VALIDATION.md` → FOUND
- Commit `ff901fa` (Pass 1 draft) → FOUND in `git log`
- Commit `4cf0ccd` (Pass 2 tests) → FOUND in `git log`
- Commit `39d53f3` (Pass 2 promotion) → FOUND in `git log`

## Self-Check: PASSED

---
*Phase: 13-nyquist-validation-backfill*
*Completed: 2026-04-20*
