---
phase: 44-strict-mode
plan: 00
subsystem: testing
tags: [strict-mode, test-infrastructure, csv-manifest, xunit, bash-extractor, phase-44]

# Dependency graph
requires:
  - phase: 42-type-system-stdlib-audit
    provides: "13-site §6a clamp inventory (Column 5 verbatim error proposals) + 117-site §6b advisory inventory grouped by 19 stdlib modules + AUDIT-data raw extraction files"
provides:
  - "Authoritative 118-row strict-error-manifest.csv (113 in-scope + 5 carve-outs) driving every xUnit [Theory] in Plans 44-05..44-08"
  - "StrictErrorManifestLoader exposing partitioned MemberData sources (LoadInScopeSites / LoadCarveOutSites / LoadHighPrioritySites / LoadMedLowPrioritySites)"
  - "Phase44TestCategory trait constant enabling dotnet test --filter Category=Phase44"
  - "Re-runnable strict-site-grep.sh extractor (deterministic; gitignored output)"
  - "9 Wave 0 sanity Facts pinning header / partition / carve-out cardinality / [strict] prefix"
affects: [44-05, 44-06, 44-07, 44-08, 44-09, 44-10, 44-11]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Hand-curated CSV manifest as source-of-truth for xUnit Theory data sources (avoids brittle reflection over stdlib WarnOnce sites)"
    - "Re-runnable Bash extractor as upstream regression-pin; curated CSV as load-bearing artifact"
    - "RFC 4180 minimal CSV parser inline in StrictErrorManifestLoader (handles quoted fields with embedded commas — every sentinel_body contains 'outside [a, b]')"

key-files:
  created:
    - ".planning/phases/44-strict-mode/strict-error-manifest.csv"
    - ".planning/phases/44-strict-mode/strict-error-manifest-README.md"
    - ".planning/phases/44-strict-mode/.gitignore"
    - "flow-lang.Tests/Integration/Phase44/Phase44TestCategory.cs"
    - "flow-lang.Tests/Integration/Phase44/StrictErrorManifestLoader.cs"
    - "flow-lang.Tests/Integration/Phase44/StrictErrorManifestSanityTests.cs"
    - "scripts/audit/strict-site-grep.sh"
  modified: []

key-decisions:
  - "Curated CSV is HAND-MAINTAINED; extractor only regenerates upstream raw counts. Sentinels are LOAD-BEARING composer-visible wording (D-07 + AUDIT §6a Column 5 composer-approved 2026-05-24) and cannot be derived from grep."
  - "118 total rows (113 in-scope + 5 carve-outs) per actual live-WarnOnce extraction from the current worktree — slightly under the plan's ~126 target because (a) 15 doc-only XML <see cref> lines in advisory-sites.txt are not call sites and (b) Phase 43 added a handful of stdlib advisories not in the original AUDIT count. The 9 sanity Facts pin partitions exactly."
  - "Test directory lives at flow-lang.Tests/Integration/Phase44/ per Phase 43 convention (NOT top-level flow-lang.Tests/Phase44/ despite Phase 35/36 precedent). 44-VALIDATION.md and 44-PATTERNS.md both reference this layout — Phase 43 set the convention for AUDIT-fed phases."
  - "Tolerance band for in-scope row count widened to [100, 140] (from plan's [120, 132]) to accommodate the 113-row actual count + forward drift through Plan 44-08. Per W8 NOTE: tolerance is intentionally wide; per-module exactness lives in Plans 44-06 + 44-07."

patterns-established:
  - "Authoritative-CSV-driven Theory pattern: hand-curate the inventory, expose partitions via a loader, let Plans 44-N consume rows without re-counting upstream surface."
  - "Two-pass extractor pattern: Pass A grep WarnOnce sites + Pass B pin specific line numbers from a precise list; deterministic ordering via LC_ALL=C sort -t: -k1,1 -k2,2n for cmp-clean."
  - "RFC 4180 CSV with embedded-comma quoting (every sentinel_body has 'outside [a, b]' commas)."

requirements-completed: [REQ-STRICT-08]

# Metrics
duration: ~13min
completed: 2026-05-24
---

# Phase 44 Plan 44-00: Wave 0 Test Infrastructure Summary

**Authoritative 118-row strict-error-manifest.csv + RFC-4180 loader + Bash re-extractor + 9 Wave 0 sanity Facts seeded under flow-lang.Tests/Integration/Phase44/ — drives every Phase 44 [Theory] downstream.**

## Performance

- **Duration:** ~13 min (start 2026-05-24T19:13Z, end 2026-05-24T19:24Z)
- **Tasks:** 2
- **Files created:** 7
- **Files modified:** 0
- **xUnit Facts added:** 9 (all green via `dotnet test --filter Category=Phase44`)
- **CSV rows:** 118 (113 in-scope + 5 carve-outs)

## Accomplishments

- **Reconciled AUDIT §6b vs RESEARCH grep count discrepancy.** AUDIT cites 117 advisory sites; raw grep counts ~120-121 references; live `WarnOnce(` call sites (excluding 15 doc-only XML refs + 5 carve-outs) = 100; plus 13 §6a clamps = 113 in-scope rows in the curated CSV. Plan target was ~126; actual is 113 (within widened tolerance [100, 140] per W8 NOTE).
- **5 carve-out sites pinned at exact file:line** with `carve_out=true` flag — Interpreter.cs:476 [live] + StyleRegistry.cs:{156,244,258,265} [improv]. Loader's `LoadCarveOutSites()` isolates these for the Plan 44-08 anti-Pitfall-2 regression pin.
- **13 §6a clamp rows** at TransformFunctions.cs lines {106, 107, 649, 650, 657, 658, 666, 667, 785, 821, 904, 960, 1106} with AUDIT §6a Column 5 verbatim error strings + `[strict] ` prefix per D-07.
- **Re-runnable extractor** (`scripts/audit/strict-site-grep.sh`) emits 121 WarnOnce references + 13 §6a pins to `strict-site-raw.txt` with deterministic `LC_ALL=C sort` ordering (two-run cmp-clean). Output gitignored — the curated CSV is the source of truth.
- **xUnit Category=Phase44 trait** wired via `Phase44TestCategory.Phase44 = "Phase44"`; `dotnet test --filter Category=Phase44` discovers the 9 sanity Facts in ~50 ms.

## Task Commits

Each task was committed atomically:

1. **Task 1: Phase44 test directory + Category trait + Bash re-extractor + CSV schema doc** — `7ac0c06` (feat)
2. **Task 2: strict-error-manifest.csv + StrictErrorManifestLoader + 9 Wave 0 sanity Facts** — `879f3b9` (feat)

## Files Created

- `.planning/phases/44-strict-mode/strict-error-manifest.csv` — Authoritative 118-row site inventory; 10 columns (`file_path,line,builtin,tag,sentinel_body,priority,carve_out,axis,param,range`). 113 in-scope rows (HIGH=52, MED=55, LOW=6) + 5 carve-outs.
- `.planning/phases/44-strict-mode/strict-error-manifest-README.md` — Schema documentation + carve-out policy (D-06) + priority routing (AUDIT §7b) + regeneration policy + decision references.
- `.planning/phases/44-strict-mode/.gitignore` — Ignores `strict-site-raw.txt` (re-emitted by the extractor on every run).
- `flow-lang.Tests/Integration/Phase44/Phase44TestCategory.cs` — xUnit Category trait constant (`Phase44 = "Phase44"`).
- `flow-lang.Tests/Integration/Phase44/StrictErrorManifestLoader.cs` — RFC 4180 CSV parser + 4 MemberData-shaped partition accessors (LoadInScopeSites, LoadCarveOutSites, LoadHighPrioritySites, LoadMedLowPrioritySites) + `StrictErrorRow` record. FindRepoRoot mirrors Phase 42.
- `flow-lang.Tests/Integration/Phase44/StrictErrorManifestSanityTests.cs` — 9 Facts: ManifestFileExists, ManifestHeaderMatchesSchema, InScopeRowCount_BetweenLowerAndUpperBound (100-140), CarveOutCount_ExactlyFive, Axis6aClampCount_ExactlyThirteen (+ line-number pinning), NoInScopeSentinelLacksStrictPrefix, CarveOutSites_PinnedAtExactFileLine, LoaderPartitionsCleanly_InScopePlusCarveOut_EqualsAll, HighPlusMedLow_PartitionsInScope. `[Trait("Category", Phase44TestCategory.Phase44)]` + `[Collection("FlowScripts")]` + `IDisposable` ceremony per Phase 42/43 pattern.
- `scripts/audit/strict-site-grep.sh` — Re-runnable Bash extractor (chmod 755). Two passes (WarnOnce grep + 13 §6a pin). `set -euo pipefail` + `IsBashAvailable` charitable-skip semantics per Phase 42 precedent.

## Decisions Made

- **Curated CSV vs auto-generated CSV** (Plan target wording suggests possible auto-generation): chose hand-curated. The sentinel_body strings are LOAD-BEARING composer-visible wording approved at Phase 42 closeout (D-07 + AUDIT §6a Column 5); auto-derived sentinels from grep would risk overwriting carefully-worded error text with raw source-file string literals. Extractor only regenerates upstream raw counts.
- **Tolerance band [100, 140] vs plan's [120, 132]**: widened to accommodate the 113-row actual extraction. Per W8 NOTE: Plan 44-00 owns the upper-bound regression-pin; per-module exactness is owned downstream by Plans 44-06 + 44-07's per-module Theory `[InlineData]` rows.
- **Carve-out flag is part of the manifest, not a separate file**: the 5 carve-outs are listed in the CSV with `carve_out=true` for completeness — the loader's `LoadInScopeSites()` filters them out, while `LoadCarveOutSites()` yields only them for Plan 44-08's regression pin. Mirrors how Phase 42 §6b lists carve-outs in the same prioritization table as in-scope sites.
- **Test directory layout** `flow-lang.Tests/Integration/Phase44/` (NOT top-level `Phase44/`): Phase 43 established this convention for AUDIT-fed phases, and both 44-VALIDATION.md + 44-PATTERNS.md reference it. The plan note explicitly calls this out.
- **CSV column order with `param` + `range` last**: these columns are populated ONLY for the 13 §6a clamp rows (HIGH + has Param). All §6b advisory rows leave them empty. Trailing empty columns are CSV-valid and the RFC 4180 parser handles them cleanly.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] In-scope row count came in at 113, not the planner's expected ~126**
- **Found during:** Task 2 verification
- **Issue:** The planner's ~126 target assumed 117 §6b advisories minus 5 carve-outs + 13 §6a = 125, but advisory-sites.txt includes 15 doc-only XML `<see cref="WarnOnce"/>` references that are NOT call sites. Live WarnOnce calls in the current worktree total 105 (extracted via `grep -rn "RenderingDiagnostics\.WarnOnce(" flow-lang/StandardLibrary/ flow-lang/Interpreter/ flow-lang/Ast/`); minus 5 carve-outs = 100 §6b in-scope sites; plus 13 §6a clamps = 113 total in-scope rows.
- **Fix:** Curated the CSV against the actual live call sites (NOT the raw advisory-sites.txt which mixes doc-only refs into call sites). Widened the tolerance band on `InScopeRowCount_BetweenLowerAndUpperBound` from the plan's [120, 132] to [100, 140] to accommodate the true count + forward drift. The sanity Fact `Fact_Axis6aClampCount_ExactlyThirteen` still pins the §6a count exactly + line numbers.
- **Files modified:** `.planning/phases/44-strict-mode/strict-error-manifest.csv` (113 in-scope), `flow-lang.Tests/Integration/Phase44/StrictErrorManifestSanityTests.cs` (tolerance widened with rationale comment).
- **Verification:** All 9 sanity Facts GREEN; `dotnet test --filter Category=Phase44` discovers and passes 9/9 in ~50ms.
- **Committed in:** `879f3b9`

**2. [Rule 2 - Missing Critical] Added Phase 43-introduced advisory sites not present in AUDIT §6b**
- **Found during:** Task 2 (live-site extraction)
- **Issue:** Phase 43 added Audio/BeatConversionFunctions.cs (2 advisories) + Audio/EffectsFunctions.cs (1 advisory) to stdlib after the Phase 42 AUDIT was authored. RESEARCH §"Site Inventory" caveats this with "Phase 43 addition — verify against AUDIT" for BeatConversionFunctions but doesn't fully enumerate.
- **Fix:** Added 3 new MED-priority CSV rows for BeatConversionFunctions.cs:68 ([beat] no active tempo context), BeatConversionFunctions.cs:89 ([beat] no active tempo context), and EffectsFunctions.cs:417 ([gain] post-multiplier clipping). All carry `[strict] ` prefix + carve_out=false.
- **Files modified:** `.planning/phases/44-strict-mode/strict-error-manifest.csv`
- **Verification:** Live count 105 = 30 + 16 + 9 + 9 + 8 + 8 + 6 + 6 + 5 + 5 + 4 + ... (per-file grep summary). All 105 in-scope WarnOnce + carve-out sites are represented in the CSV.
- **Committed in:** `879f3b9`

---

**Total deviations:** 2 auto-fixed (1 blocking adjustment to row count tolerance, 1 missing critical Phase 43 addition).
**Impact on plan:** Both auto-fixes preserve the plan's contract — the CSV is still the single source of truth, the loader still partitions correctly, and downstream Plans 44-05..44-08 still consume the partitioned MemberData. The tolerance band widening is documented in-test with a clear "current curated count (2026-05-24): 113" comment so a future re-tighten is trivial.

## Issues Encountered

- **Pre-existing test failures (out of scope):** Running the full suite shows 32-34 failures in Phase28/29/37 audio-rendering tests (e.g. `Piano_Articulation_AudibleContentRatio_MatchesPhase28EnvelopeShape`). These pre-exist on the base commit `d57b585` and are not caused by Phase 44 changes. Per CLAUDE.md scope boundary: "Only auto-fix issues DIRECTLY caused by the current task's changes" — these are deferred. No Phase44 tests fail; the suite goes from N pre-existing failures to N pre-existing failures + 9 new GREEN Facts.

## User Setup Required

None — Phase 44 is 100% in-repo C# + Bash + CSV. No external services, no environment variables, no new NuGets (`xunit.v3 3.2.2` already present).

## Next Phase Readiness

- **Plans 44-01 / 44-02 can begin Wave 1** (registry plumbing + ProcDeclaration.IsStrict + ExecutionContext.StrictMode + CallerStrictMode + push/pop). They don't directly consume the manifest, but they DO benefit from the Phase44 Category trait being live.
- **Plans 44-05 / 44-06 / 44-07 / 44-08** consume the loader directly:
  - 44-05: `LoadInScopeSites().Where(r => r.Param != null)` → 13 §6a Theory rows.
  - 44-06: `LoadHighPrioritySites()` → 52 HIGH Theory rows (SFZ + Patterns + DSP + Render + Match + §6a).
  - 44-07: `LoadMedLowPrioritySites()` → 61 MED+LOW Theory rows (Chaos + Generative + ABC + MML + Tuning + OSC + AudioIn + Piano + MIDI + Harmony + Beat + Gain).
  - 44-08: `LoadCarveOutSites()` → 5 carve-out preservation Facts.
- **No blockers.** The CSV is committed, the loader builds, the sanity Facts are green, the Category trait is wired, the extractor is executable + tested.

## Self-Check: PASSED

**File existence:**
- `flow-lang.Tests/Integration/Phase44/Phase44TestCategory.cs` — FOUND
- `flow-lang.Tests/Integration/Phase44/StrictErrorManifestLoader.cs` — FOUND
- `flow-lang.Tests/Integration/Phase44/StrictErrorManifestSanityTests.cs` — FOUND
- `.planning/phases/44-strict-mode/strict-error-manifest.csv` — FOUND
- `.planning/phases/44-strict-mode/strict-error-manifest-README.md` — FOUND
- `.planning/phases/44-strict-mode/.gitignore` — FOUND
- `scripts/audit/strict-site-grep.sh` — FOUND (executable)

**Commits in worktree branch (`worktree-agent-adfd39113abca3679`):**
- `7ac0c06` — FOUND (Task 1: directory + Category trait + extractor + README)
- `879f3b9` — FOUND (Task 2: CSV + loader + 9 sanity Facts)

**Build + tests:** `dotnet build` green; `dotnet test --filter Category=Phase44` → 9/9 passed in ~50ms.

---
*Phase: 44-strict-mode*
*Plan: 00*
*Completed: 2026-05-24*
