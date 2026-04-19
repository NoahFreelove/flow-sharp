---
phase: 13-nyquist-validation-backfill
plan: 02
subsystem: testing
tags: [validation, backfill, nyquist, phase7, repl, math, comments, writewav, xunit]

# Dependency graph
requires:
  - phase: 07-developer-experience
    provides: DX-01 // comments, DX-02 math stdlib, DX-03 writeWav alias, DX-04 REPL auto-imports
  - phase: 12-stability
    provides: FlowScriptTests Theory harness + FlowScriptData.RequiredSentinels + FlowEngineRunner fixture
  - phase: 13-01
    provides: Integration/PhaseNN/ directory convention + two-pass strict authorship template
provides:
  - "Retroactive 07-VALIDATION.md at nyquist_compliant: true (TEST-04)"
  - "RepLAutoImportTests Fact as proxy for REPL interactive auto-import"
  - "Tightened RequiredSentinels for test_comments.flow + test_math.flow + test_writewav.flow"
  - "Empirical documentation of Flow's 10-sig-digit Double str format (Pitfall 5 anchor)"
affects: [13-03, 13-04, 13-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Two-pass strict authorship with documented Divergences — extended across phases"
    - "RequiredSentinels append-only tightening — no theory-row duplication"
    - "Integration/PhaseNN/ directory convention — replicated from 13-01 for Phase 7"

key-files:
  created:
    - .planning/phases/07-developer-experience/07-VALIDATION.md
    - flow-lang.Tests/Integration/Phase07/RepLAutoImportTests.cs
  modified:
    - flow-lang.Tests/FlowScriptData.cs

key-decisions:
  - "Two-pass strict authorship produced DX-02 Double format drift — Flow's str emits 10-sig-digit precision (3.141592654), NOT full Math.PI.ToString() precision (3.141592653589793). Pitfall 5 vindicated."
  - "Whole-valued Doubles strip trailing .0 in Flow str output (sin 0.0 → '0', sqrt 16.0 → '4', pow 2.0 10.0 → '1024'). Sentinel choice '1024' pins pow registration unambiguously."
  - "DX-04 proxy-via-direct-imports per v1.1 audit's 'not e2e-testable via piped stdin' — piped stdin routes to RunFromStdin (script mode), not the REPL (07-02-SUMMARY line 104). The Fact executes the SAME three imports the REPL hardcodes."
  - "Array[Int] is NOT valid Flow syntax — idiomatic array declaration is Int[] (confirmed at tests/test_lambdas.flow:40). Rule 3 substitution applied in Pass 2."
  - "Sentinel 'note stream ok' (line 31 of test_comments.flow) specifically pins the post-note-stream inline-// case — would RED if lexer dropped // support for inline comments on note-stream-producing lines. Sentinel '42' (line 40) specifically pins empty-// comments (line 39 is bare `//`)."

patterns-established:
  - "Empirical-capture protocol for str-format sentinels: run the script with dotnet run --project flow-interpreter, copy exact stdout substrings, log Pass 1↔Pass 2 drift in Divergences — MUST NOT infer Double formats from .NET defaults."

requirements-completed: [TEST-04]

# Metrics
duration: 8min
completed: 2026-04-20
---

# Phase 13 Plan 02: Phase 7 Validation Backfill Summary

**Retroactive Phase 7 VALIDATION.md at nyquist_compliant: true via two-pass strict authorship; RepLAutoImportTests Fact proxies the REPL's non-e2e-testable auto-import contract; three tightened sentinels pin DX-01 comments, DX-02 math stdlib (with empirical Double format capture), and DX-03 writeWav/exportWav alias.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-04-20T03:04:34Z
- **Completed:** 2026-04-20T03:12:13Z
- **Tasks:** 2 (3 atomic commits)
- **Files modified:** 3

## Accomplishments

- Authored `.planning/phases/07-developer-experience/07-VALIDATION.md` retroactively under TEST-04 via two-pass strict (Pass 1 REQUIREMENTS-only draft, Pass 2 empirical reality check + Divergences)
- Created `flow-lang.Tests/Integration/Phase07/RepLAutoImportTests.cs` — single xUnit Fact executing the same three `use` statements the REPL hardcodes (`@std`, `@audio`, `@collections`) + asserting `list`/`createSineTone`/`print` resolve. Best automatable proxy for the REPL's interactive auto-import contract per v1.1 audit's "not e2e-testable via piped stdin" observation.
- Appended three new `RequiredSentinels` entries to `flow-lang.Tests/FlowScriptData.cs` (keys `test_comments.flow`, `test_math.flow`, `test_writewav.flow`) — converts existing Theory rows from errorCount-only gates to substring-pinned regression gates
- Full `dotnet test flow-sharp.sln` suite: **72/72 green** (up from baseline 71 — +1 new RepLAutoImportTests Fact)
- Documented empirical Double format drift in Divergences: Flow's `str` emits 10-sig-digit precision; `Math.PI.ToString()` inference would have produced a RED sentinel

## Task Commits

Each task was committed atomically:

1. **Task 1: Pass 1 — Requirements-only 07-VALIDATION.md draft** — `fb1a1ae` (docs)
2. **Task 2a: Pass 2 — Author Phase 7 validation Fact + sentinels** — `ed64dec` (test)
3. **Task 2b: Pass 2 — Promote 07-VALIDATION.md to nyquist_compliant** — `9d7575f` (docs)

## Files Created/Modified

- `.planning/phases/07-developer-experience/07-VALIDATION.md` — NEW retroactive VALIDATION.md at nyquist_compliant: true; Pass 1 Draft + Pass 2 Implementation Map + Divergences + Per-Task Verification Map (all 4 DX reqs green) + Observable Invariants
- `flow-lang.Tests/Integration/Phase07/RepLAutoImportTests.cs` — NEW; single `[Fact] AutoImportedModulesResolve_StdAudioCollections`; `[Collection("FlowScripts")]`; uses `FlowEngineRunner.RunSource`
- `flow-lang.Tests/FlowScriptData.cs` — appended 3 `RequiredSentinels` entries (test_comments.flow, test_math.flow, test_writewav.flow); does NOT collide with 13-01's test_transpose_int.flow entry

## Decisions Made

- **DX-02 empirical Double format:** `(str pi)` → `"3.141592654"` (10 sig digits), NOT `"3.141592653589793"`. Flow's `str` uses a shorter precision than `Math.PI.ToString()` defaults. Pitfall 5 vindicated — the plan's explicit warning about inferring Double formats from .NET defaults saved a RED test.
- **DX-02 whole-valued-Double format:** trailing `.0` is stripped — `sin 0.0` prints `"0"`, `sqrt 16.0` prints `"4"`, `pow 2.0 10.0` prints `"1024"`. Chose `"1024"` as the pow sentinel because it unambiguously pins pow-registration (`"0"`, `"1"`, `"4"` would match too many things via substring).
- **DX-04 proxy Fact, not REPL piping:** Per 07-02-SUMMARY.md line 104, piped stdin routes to `RunFromStdin` (script mode), not the REPL. The v1.1 audit accepted this gap as "verified by code inspection only". The RepLAutoImportTests Fact executes the SAME three `use` statements `Repl.cs::AutoImportStandardModules` hardcodes (lines 88-90), so if any REPL-auto-imported module stops exporting the expected symbol, the Fact fails.
- **Sentinel specificity for DX-01:** Chose `"note stream ok"` (line 31 inline-// on a note-stream-producing line), `"42"` (line 40 after empty-// on line 39), and `"All comment tests passed"` (full-run gate). These three exercise the distinct comment styles the lexer must support — if `//` drops from `SkipWhitespaceAndComments`, at least one fails.
- **Array[Int] → Int[] substitution:** Pass 1 drafted `Array[Int] xs = (list 1 2 3)`. Pass 2 discovered via `tests/test_lambdas.flow:40` that idiomatic Flow syntax is `Int[]`. Substituted in the Fact source; logged in Divergences as a Rule 3 auto-fix.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Array[Int] → Int[] syntax substitution in DX-04 Fact source**
- **Found during:** Task 2 (Pass 2 reality check)
- **Issue:** Plan's sample Fact source used `Array[Int] xs = (list 1 2 3)`, but `Array[Int]` is NOT valid Flow type syntax. Confirmed via `tests/test_lambdas.flow:40` — idiomatic array declaration is `Int[]`.
- **Fix:** Substituted `Int[] xs = (list 1 2 3)` in the Fact source.
- **Files modified:** `flow-lang.Tests/Integration/Phase07/RepLAutoImportTests.cs`
- **Verification:** `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase07"` passes (1/1).
- **Committed in:** `ed64dec` (Task 2a commit)
- **Logged in:** `07-VALIDATION.md §Divergences` DX-04 entry.

**2. [Pass 1↔Pass 2 drift — not a deviation, tracked per two-pass protocol] DX-02 Double format**
- **Found during:** Task 2 Pass 2 empirical capture
- **Discrepancy:** Pass 1 drafted `"3.141592653589793"` (Math.PI.ToString()) for pi and `"6.283185307179586"` for tau. Actual Flow `str` output: `"3.141592654"` and `"6.283185307"` (10-sig-digit precision).
- **Resolution:** Replaced Pass 1 drafts with empirical captures — codebase `str`-output format is canonical per D-13 two-pass strict. Logged verbatim in `07-VALIDATION.md §Divergences`. This is the designed-for outcome of two-pass strict, not a bug.

---

**Total deviations:** 1 auto-fix (1 blocking, Rule 3); 1 documented Pass 1↔Pass 2 drift (expected per two-pass strict protocol, not a deviation).
**Impact on plan:** Zero scope creep. All planned tasks delivered. The two-pass strict discipline produced exactly the Divergence the plan anticipated (DX-02 Double format).

## Issues Encountered

- Running `test_writewav.flow` writes `test_writewav_output1.wav` and `test_writewav_output2.wav` to repo root (CWD of the Theory harness is repo root per FlowScriptTests.cs:24). Both files are `.gitignore`'d (`*.wav`); no repo pollution. Pre-existing behavior — not introduced by this plan.

## User Setup Required

None — no external service configuration required.

## Known Stubs

None. The RepLAutoImportTests Fact wires real FlowEngine + real module loading + real symbol resolution. The three tightened sentinels gate real stdout substrings, not placeholders.

## Self-Check

### Files Created/Modified (verification)

- `.planning/phases/07-developer-experience/07-VALIDATION.md` — FOUND (git ls-files confirms tracked at `9d7575f`)
- `flow-lang.Tests/Integration/Phase07/RepLAutoImportTests.cs` — FOUND (git ls-files confirms tracked at `ed64dec`)
- `flow-lang.Tests/FlowScriptData.cs` — MODIFIED (3 new RequiredSentinels entries; verified via `grep -q "test_comments\.flow\|test_math\.flow\|test_writewav\.flow"`)

### Commits (verification)

- `fb1a1ae` docs(13-02): pass 1 — requirements-first 07-VALIDATION.md draft — FOUND
- `ed64dec` test(13-02): pass 2 — author Phase 7 validation Fact + sentinels — FOUND
- `9d7575f` docs(13-02): pass 2 — promote 07-VALIDATION.md to nyquist_compliant — FOUND

### Test Suite (verification)

- `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase07"` → 1/1 pass (RepLAutoImportTests.AutoImportedModulesResolve_StdAudioCollections)
- `dotnet test flow-sharp.sln` → **72/72 green**, 0 failed (baseline was 71 at HEAD of 13-01; +1 new Fact)

## Self-Check: PASSED

## Next Phase Readiness

- Phase 8 backfill (`13-03-PLAN.md`) can now replicate the two-pass strict authorship + tightened-sentinels pattern. The `Integration/Phase07/` directory convention confirms the template scales across phases.
- Plan 13-02 completes 2/4 phase-validation backfills (13-01 covered Phase 6; this plan covers Phase 7). Remaining: 13-03 Phase 8 (AUDIO-05/06/07), 13-04 Phase 9 (AUDIO-08, QOL-02), 13-05 Phase 10 VOC-01/02 promotion.
- Phase 13 ROADMAP criterion 3 ("at least one validation test per phase pins a specific observable value") now satisfied for Phase 7 via: empirical pi/tau/pow str-format pins + "note stream ok" post-// sentinel + PASS strings for both writeWav + exportWav.

---
*Phase: 13-nyquist-validation-backfill*
*Completed: 2026-04-20*
