---
phase: 47-compile-target-flavors
plan: 06
subsystem: docs
tags:
  - phase-47
  - wave-6
  - closer
  - tracking-sweep
  - test-project-web-build
requirements: [REQ-WEB-TARGET-01, REQ-WEB-TARGET-02, REQ-WEB-TARGET-03, REQ-WEB-TARGET-04, REQ-WEB-TARGET-05, REQ-WEB-TARGET-06, REQ-WEB-TARGET-07, REQ-WEB-TARGET-08, REQ-WEB-TARGET-09, REQ-WEB-TARGET-10]
dependency-graph:
  requires:
    - "Plan 47-01..47-05 (all prior waves shipped)"
  provides:
    - "47-VERIFICATION.md with REQ-WEB-TARGET-01..10 acceptance evidence + 3 known caveats + Next Steps pointer to Phase 48"
    - "ROADMAP.md Phase 47 marked SHIPPED 2026-05-25 + Progress table row Complete + 6 enumerated plans"
    - "STATE.md frontmatter (total_phases 8/15, percent 64) + Phase 47 highlights bullets + Current Position → Phase 48"
    - "REQUIREMENTS.md REQ-WEB-TARGET-01..10 all [x] + Phase 47 section header flipped to SHIPPED 2026-05-25"
    - "CLAUDE.md ## Compile-Target Flavors section documenting FlowTarget=Desktop|Web build surface for future contributors"
    - "flow-lang.Tests.csproj: FLOW_WEB define propagation + <Compile Remove> ItemGroup stripping 21 test files (18 from 47-04-SUMMARY.md + 3 Rule 3 deviation mic-input tests)"
    - "WebTargetGuardTests.cs Rule 1 bug fix — 4 plain [Fact] methods retagged as [FlowTargetFact(\"Desktop\")] (Plan 47-03 carryover that escaped Plan 47-04 attribute introduction)"
  affects:
    - "Phase 48 (WASM Runtime + WebAudioBackend) — unblocked; consumes FlowTarget=Web build infrastructure + WebAudioBackend stub + AssemblyReferenceScanTests invariant + DryWetMidi WASM-compat verified"
tech-stack:
  added: []
  patterns:
    - "Phase closer convention from Plan 44-11 / Plan 43-05 / Plan 42-04 — single docs-sweep commit per tracking file"
    - "Test-project FLOW_WEB define propagation via separate Web-conditional PropertyGroup (ProjectReferences inherit MSBuild properties but NOT DefineConstants)"
    - "Web-conditional <Compile Remove> ItemGroup in flow-lang.Tests.csproj mirrors flow-lang.csproj Plan 47-01 strip-list pattern verbatim"
    - "Rule 1 (bug fix) — retag plain [Fact] to [FlowTargetFact(\"Desktop\")] when test asserts Desktop-only behavior"
    - "Rule 3 (blocking issue) — when an N-file deferral list misses related Web-incompatible files, extend strip-list at closer (3 mic-input Phase 38 tests)"
key-files:
  created:
    - ".planning/phases/47-compile-target-flavors/47-VERIFICATION.md"
    - ".planning/phases/47-compile-target-flavors/47-06-SUMMARY.md"
  modified:
    - ".planning/ROADMAP.md"
    - ".planning/STATE.md"
    - ".planning/REQUIREMENTS.md"
    - "CLAUDE.md"
    - "flow-lang.Tests/flow-lang.Tests.csproj"
    - "flow-lang.Tests/Integration/Phase47/WebTargetGuardTests.cs"
decisions:
  - "Phase closer convention (Plan 44-11 / 43-05 / 42-04 precedent) — atomic per-tracking-file commits in single closer plan"
  - "21-file test-project strip (18 from 47-04-SUMMARY + 3 Rule 3) was the path-of-least-churn vs per-Fact [FlowTargetFact(\"Desktop\")] retag of every Fact in those 21 files (≥200 retags)"
  - "FLOW_WEB propagation must be explicit in flow-lang.Tests.csproj — ProjectReferences inherit FlowTarget MSBuild property but NOT DefineConstants (verified empirically — without explicit propagation, FlowTargetFactAttribute.CurrentTarget kept resolving to \"Desktop\" even under -p:FlowTarget=Web)"
  - "DryWetMidi 8.0.3 WASM-compat verified GREEN (both cross-target Facts PASS under Web) — no follow-up edit needed to strip DryWetMidi from Web; writeMidi ships on Web target subject to Phase 48 D-48-04 broader Mono-WASM runtime verification"
  - "Rule 1 retag of 4 WebTargetGuardTests Facts — they were Plan 47-03 carryover from BEFORE FlowTargetFactAttribute existed (Plan 47-04 introduced it); not a Plan 47-03 author error, just a wave-ordering artifact"
metrics:
  duration: 13min
  completed: 2026-05-26
  tasks_completed: 6
  files_created: 2
  files_modified: 6
  commits: 6
---

# Phase 47 Plan 06: Closer Summary

## One-liner

Closes Phase 47 — VERIFICATION.md (REQ-WEB-TARGET-01..10 evidence + 3 caveats) + ROADMAP/STATE/REQUIREMENTS/CLAUDE.md tracking sweep + 21-file test-project Web-build closer (18-file deferral from 47-04-SUMMARY.md + 3 Rule 3 mic-input tests + Rule 1 WebTargetGuardTests retag) flipping `dotnet test -p:FlowTarget=Web --filter Phase47` from 18-file cascade-fail to 20 PASS / 4 SKIP / 0 FAIL end-to-end, including the AssemblyReferenceScanTests invariant gate (Plan 47-05) running GREEN and DryWetMidi 8.0.3 WASM-compat confirmed working.

## What Was Done

### Task 1 — 47-VERIFICATION.md (commit `4ce8074`)

Created `.planning/phases/47-compile-target-flavors/47-VERIFICATION.md` (133 lines). Sections:

- **Outcome Summary** — FlowTarget=Desktop|Web build surface + composer-facing UX on Web target (`use "@sfz"` advisory / `live` block ParseException / `micBuffer` function-not-found / WebAudioBackend stub PNS)
- **Requirement Closure Table** — all 10 REQ-WEB-TARGET-NN entries with closing-plan citations + specific commit SHAs
- **Acceptance Evidence** — 5 sections covering Desktop + Web flow-lang builds, test-project Web build, Desktop test suite, Web Phase 47 fixture run
- **Plan Summary** table (6 plans × wave × description × outcome)
- **3 Known Caveats:** (1) DryWetMidi WASM-compat verified GREEN — no follow-up needed; (2) test-project Web buildability closed at this closer; (3) PATTERNS.md §Discrepancies acted upon
- **Next Steps** pointing at Phase 48

### Task 2 — ROADMAP.md sweep (commit `42608b4`)

5 edits to `.planning/ROADMAP.md`:

1. Phase 47 detail-section header: `### Phase 47: Compile-Target Flavors` → `### Phase 47: Compile-Target Flavors — SHIPPED 2026-05-25`
2. `**Requirements**:` line: enumerated REQ-WEB-TARGET-01..10
3. `**Plans:**` count: `0 plans` → `6/6 plans complete`
4. Plan placeholder section: replaced `[ ] TBD` placeholder with 6 enumerated `[x] 47-NN-PLAN.md` rows organized in 6-wave structure + commit SHAs
5. Progress table row: `| 47. Compile-Target Flavors | v1.5 | 5/6 | In Progress | |` → `| 47. Compile-Target Flavors | v1.5 | 6/6 | Complete | 2026-05-25 |`

Phase 48 + Phase 49 sections preserved unchanged.

### Task 3 — STATE.md sweep (commit `702edbe`)

3 edits to `.planning/STATE.md`:

1. **Frontmatter:** `completed_phases: 7 → 8`, `percent: 47 → 64`, `stopped_at` + `last_activity` flipped to Phase 47 closure language
2. **Current Position block:** `Phase: 47` → `Phase: 47 -- shipped 2026-05-25`, `Plan:` → `6/6 complete`, next-step list flipped to Phase 48 / 45 / 40 as composer choices
3. **Phase 47 highlights bullets** — 9-bullet block summarizing the entire phase: MSBuild conditioning landing + WebAudioBackend stub + central #if !FLOW_WEB guards at actual call sites + IsWebTarget/SupportsLiveBlocks static flags + Parser live-block gate + ModuleLoader stripped-stdlib gate + FlowTargetFactAttribute + AssemblyReferenceScanTests + DryWetMidi smoke verified + Plan 47-06 closer 21-file test-project fix
4. **v1.5 Phase Map table:** added Phase 44 (REQ-STRICT-01..15) + Phase 47 (REQ-WEB-TARGET-01..10) rows; table description updated to 13 phases / 85 REQs; summary line updated to `9/15 phases complete` (35 + 36 + 37 + 38 + 39 + 42 + 43 + 44 + 47).

All existing highlights blocks (Phase 38, 39, 42, 43) preserved.

### Task 4 — REQUIREMENTS.md sweep (commit `c683608`)

3 edits to `.planning/REQUIREMENTS.md`:

1. **Category-introduction line** (line 7): appended `REQ-AUDIT-*, REQ-MOD-*, REQ-STRICT-*, REQ-BEAT-*, REQ-WEB-TARGET-*` to the v1.5 prefix enumeration
2. **Phase 47 section header:** `**Phase 47 IN PROGRESS**` → `**Phase 47 SHIPPED 2026-05-25**` + closure summary + pointer to 47-VERIFICATION.md
3. **REQ-WEB-TARGET-10:** flipped from `Target: Plan 47-05` to proper closure citation with `5c6129c + 25b40ea` commit SHAs + Mono.Cecil package-legitimacy gate note + D-47-14 reference

All 10 REQ-WEB-TARGET-NN entries verified marked `[x]` with closing-plan + commit citations.

### Task 5 — CLAUDE.md ## Compile-Target Flavors (commit `cb91c46`)

Inserted new `## Compile-Target Flavors` section (38 lines) between the existing `## Build & Run Commands` section and `## Project Structure`. Covers:

- Build invocation patterns (Desktop default + explicit Desktop + Web opt-in)
- Strip-list rationale across 7 categories per D-47-01..14
- 85% language surface preserved on Web target
- Composer-facing UX (advisories, ParseException, stub throws)
- Guard locations (FlowEngine.cs:185,202 + BuiltInFunctions.cs:1027 + AudioPlaybackManager + ModuleLoader + Parser.cs:220 + IsWebTarget/SupportsLiveBlocks + AssemblyReferenceScanTests)
- 4-step pattern for adding new audio/network/IO features

### Task 6 — Test-project Web build closer + Rule 1/3 deviations (commit `c8a304e`)

The orchestrator's `<success_criteria>` required `dotnet build flow-lang.Tests -p:FlowTarget=Web` to exit 0 (was 50-error cascade per 47-04-SUMMARY.md). Three coordinated edits closed it:

**Edit 1 — `flow-lang.Tests/flow-lang.Tests.csproj`** added two PropertyGroup + ItemGroup blocks:

- `<FlowTarget Condition="'$(FlowTarget)' == ''">Desktop</FlowTarget>` default mirroring flow-lang.csproj
- `<PropertyGroup Condition="'$(FlowTarget)' == 'Web'">` appending `;FLOW_WEB` to `$(DefineConstants)` — load-bearing: ProjectReferences inherit MSBuild properties but NOT DefineConstants, so without this `FlowTargetFactAttribute.CurrentTarget` stayed `"Desktop"` even under `-p:FlowTarget=Web`
- `<ItemGroup Condition="'$(FlowTarget)' == 'Web'">` with 21 `<Compile Remove>` entries: the 18 files from 47-04-SUMMARY.md + 3 Rule 3 deviation mic-input Phase 38 tests caught by re-running the build (`MicBufferAttenuationTests`, `MicBufferResampleTests`, `PulseAudioCaptureBackendTests`)

**Edit 2 — Rule 1 bug fix in `WebTargetGuardTests.cs`** (Plan 47-03 carryover):

The Plan 47-03 author wrote 4 plain `[Fact]` methods asserting Desktop-only behavior (`IsWebTarget_IsFalse_OnDesktopBuild`, etc.). They predate Plan 47-04's `FlowTargetFactAttribute` introduction. Under `-p:FlowTarget=Web` they now incorrectly run and fail (asserting `IsWebTarget==false` when it's `true`). Retagged the 4 Facts as `[FlowTargetFact("Desktop")]` so they skip under Web — Plan 47-04's `WebTargetParserTests` already covers the Web-side counterparts.

**Edit 3 — `47-VERIFICATION.md` updates:** replaced placeholder `<output: ...>` text with real captured outputs + updated Caveat 2 to reflect the actual 21-file count (not 18) + Rule 3 + Rule 1 deviations.

**Result:**

| Target | Before Plan 47-06 | After Plan 47-06 |
|--------|-------------------|------------------|
| `dotnet build flow-lang -p:FlowTarget=Desktop` | 0 errors | 0 errors |
| `dotnet build flow-lang -p:FlowTarget=Web` | 0 errors | 0 errors |
| `dotnet build flow-lang.Tests -p:FlowTarget=Web` | 50 errors / 18 files | 0 errors |
| `dotnet test --filter Phase47 -p:FlowTarget=Web` | (build-broken) | **20 PASS / 4 SKIP / 0 FAIL** |
| `dotnet test` (Desktop) | 2127 PASS / 9 SKIP / 0 FAIL | 2127 PASS / 9 SKIP / 0 FAIL |

The 20 Web PASSES include `AssemblyReferenceScanTests.WebBuild_HasNoRefsToStrippedNamespaces` (Plan 47-05 invariant gate live) + `WebBuild_RetainsLegitimateRefs` + 2 `DryWetMidiWasmCompatTests` (cross-target — confirms DryWetMidi 8.0.3 WASM-compatible) + 3 `WebTargetParserTests` + 3 `WebTargetModuleLoaderTests` + 3 `BuildConditioningSmokeTests` + 7 `WebAudioBackendStubTests`.

## Acceptance Verification

### Source-grep assertions

| Assertion | Expected | Actual |
|---|---|---|
| `grep -c "REQ-WEB-TARGET-" 47-VERIFICATION.md` | ≥ 12 | 25 ✓ |
| `grep -c "Known Caveats" 47-VERIFICATION.md` | 1 | 1 ✓ |
| `grep -c "Phase 47: Compile-Target Flavors — SHIPPED" ROADMAP.md` | 1 | 1 ✓ |
| `grep -c "REQ-WEB-TARGET-01" ROADMAP.md` | ≥ 1 | 1 ✓ |
| `grep -c "47-01-PLAN.md" ROADMAP.md` | 1 | 1 ✓ |
| `grep -c "47-06-PLAN.md" ROADMAP.md` | 1 | 1 ✓ |
| `grep "47. Compile-Target Flavors.*Complete" ROADMAP.md` | row present | row present ✓ |
| `grep "total_plans: 71" STATE.md` | present | present ✓ |
| `grep "completed_plans: 60" STATE.md` | present | present ✓ |
| `grep "Phase 47 highlights" STATE.md` | present | present ✓ |
| `grep -c "REQ-WEB-TARGET" STATE.md` | ≥ 1 | 1 ✓ |
| `grep -c "\\[x\\] \\*\\*REQ-WEB-TARGET-" REQUIREMENTS.md` | 10 | 10 ✓ |
| `grep "Compile-Target Flavors (Phase 47)" REQUIREMENTS.md` | present | present ✓ |
| `grep -c "## Compile-Target Flavors" CLAUDE.md` | 1 | 1 ✓ |
| `grep -c "FlowTarget=Web" CLAUDE.md` | ≥ 2 | 2 ✓ |
| `grep -c "WebAudioBackend" CLAUDE.md` | ≥ 1 | 2 ✓ |
| `grep -c "FlowEngine.IsWebTarget" CLAUDE.md` | ≥ 1 | 1 ✓ |

### Build assertions

| Build invocation | Expected | Actual |
|---|---|---|
| `dotnet build flow-lang -p:FlowTarget=Desktop` | exit 0 | exit 0 ✓ |
| `dotnet build flow-lang -p:FlowTarget=Web` | exit 0 | exit 0 ✓ |
| `dotnet build flow-lang.Tests -p:FlowTarget=Desktop` | exit 0 | exit 0 ✓ |
| `dotnet build flow-lang.Tests -p:FlowTarget=Web` | exit 0 | exit 0 ✓ |

### Test assertions

| Test scope | Expected | Actual |
|---|---|---|
| Full Desktop suite | ≤ baseline failures | **2127 PASS / 9 SKIP / 0 FAIL** ✓ |
| Phase 47 fixture (Desktop) | mix pass+skip | 16 PASS / 8 SKIP ✓ |
| Phase 47 fixture (Web) | majority pass, web-only Facts execute | **20 PASS / 4 SKIP** ✓ — `AssemblyReferenceScanTests` (Plan 47-05) + `DryWetMidiWasmCompatTests` (Plan 47-04) + `WebTargetParserTests` (Plan 47-04) + `WebTargetModuleLoaderTests` (Plan 47-04) ALL GREEN |
| Zero new failures vs Plan 47-05 baseline | yes | yes ✓ |

## Deviations from Plan

### Rule 1 auto-fix — WebTargetGuardTests.cs 4-Fact retag

**Trigger:** Plan 47-03 shipped `WebTargetGuardTests.cs` with 4 plain `[Fact]` methods asserting Desktop-only behavior (`IsWebTarget==false`, `SupportsLiveBlocks==true`, live block parses, no `[target]` stderr). At the time Plan 47-03 author was correct — there was no `FlowTargetFactAttribute` yet. Plan 47-04 introduced it. Under `dotnet test -p:FlowTarget=Web` the plain Facts now incorrectly run and fail.

**Issue:** Test assumes Desktop runtime state but runs unconditionally under Web build.

**Fix:** Retag 4 Facts as `[FlowTargetFact("Desktop")]` so they skip when `CurrentTarget == "Web"`. Added `using FlowLang.Tests.Helpers;` for the attribute namespace.

**Files modified:** `flow-lang.Tests/Integration/Phase47/WebTargetGuardTests.cs`

**Commit:** included in `c8a304e` (Task 6).

### Rule 3 auto-fix — 3 additional mic-input test files

**Trigger:** Plan 47-04 Task 1's 18-file deferral list (Sfz + Osc test files) missed 3 mic-input Phase 38 tests that also reference Web-stripped types (`PulseAudioCaptureBackend`, `InputFunctions`). After adding the 18-file `<Compile Remove>` ItemGroup, `dotnet build flow-lang.Tests -p:FlowTarget=Web` still failed with 29 errors in those 3 files.

**Issue:** Incomplete strip-list — 3 files NOT in the 47-04-SUMMARY.md deferral table but with the same Web-incompatibility surface.

**Fix:** Extended the `<Compile Remove>` ItemGroup with 3 more entries (`Integration/Phase38/MicBufferAttenuationTests.cs` + `MicBufferResampleTests.cs` + `PulseAudioCaptureBackendTests.cs`). Mechanically identical to the original 18 — same Web-incompatible type references.

**Files modified:** `flow-lang.Tests/flow-lang.Tests.csproj`

**Commit:** included in `c8a304e` (Task 6).

### No Rule 2 / Rule 4 escalations

No missing-functionality additions (Rule 2 — `FlowTargetFactAttribute` already exists from Plan 47-04). No architectural escalations (Rule 4 — the closer's docs-and-build-config work has well-established precedent at Plan 44-11 / 43-05 / 42-04).

## Authentication Gates

None — pure tracking-file + build-config work.

## Decisions Made

- **Closer convention from Plan 44-11 / 43-05 / 42-04 honored** — single docs-sweep landed across 6 atomic commits (one per tracking file + one for the test-project Web build closer).
- **21-file Compile Remove was path-of-least-churn** — alternative was per-Fact `[FlowTargetFact("Desktop")]` retag of every Fact in those 21 files (≥200 retags spread across `SfzArticulationTests` 27 facts, `SfzParserTests` ~50 facts, etc.). The csproj approach matches Plan 47-01's `<Compile Remove>` strip pattern verbatim; conceptual symmetry.
- **FLOW_WEB propagation must be explicit in flow-lang.Tests.csproj** — verified empirically that without the Web-conditional `<DefineConstants>$(DefineConstants);FLOW_WEB</DefineConstants>` in the test project, `FlowTargetFactAttribute.CurrentTarget` resolves to `"Desktop"` even when the top-level invocation is `-p:FlowTarget=Web`. The DefineConstants propagation is NOT automatic across ProjectReference; properties propagate but compile-time symbols do not.
- **DryWetMidi 8.0.3 WASM-compat resolved POSITIVE** — both cross-target Facts PASS under Web, so DryWetMidi STAYS in Web build. No follow-up strip per D-48-04 was needed at Phase 47 closer. Phase 48 still owns the broader Mono-WASM runtime question (does the .NET 10 jiterpreter correctly execute DryWetMidi's MidiFile IO at runtime in a browser).
- **Plan 47-06 deliberately includes the test-project Web build closer** — the orchestrator's success criteria treats this as Plan 47-06's load-bearing work, even though the plan body itself only enumerates docs tasks. Treated as a Rule 3 deviation (blocking issue — without it, success criteria gate "Now AssemblyReferenceScanTests + DryWetMidiWasmCompatTests + WebTargetParserTests + WebTargetModuleLoaderTests run GREEN under FlowTarget=Web" cannot be met).

## Threat Flags

None — Phase 47 closer touched only documentation + build configuration + 4 xUnit attribute retags. No new runtime input perimeter; no new packages; no new attack surface.

## Known Stubs

None in this plan. `WebAudioBackend` (Plan 47-02 stub) remains as intended — Phase 48 fills the JSImport bodies.

## Files Touched

```text
.planning/phases/47-compile-target-flavors/47-VERIFICATION.md    (NEW, 133 lines)
.planning/phases/47-compile-target-flavors/47-06-SUMMARY.md      (NEW, this file)
.planning/ROADMAP.md                                              (+27/-10 lines)
.planning/STATE.md                                                (+32/-16 lines)
.planning/REQUIREMENTS.md                                         (+3/-3 lines)
CLAUDE.md                                                         (+38 lines)
flow-lang.Tests/flow-lang.Tests.csproj                            (+34 lines)
flow-lang.Tests/Integration/Phase47/WebTargetGuardTests.cs        (+1/-0 lines, 4 attr retags)
```

## Commits

| Hash | Type | Description |
|---|---|---|
| `4ce8074` | docs | add 47-VERIFICATION.md — Phase 47 completion record |
| `42608b4` | docs | flip ROADMAP.md Phase 47 to SHIPPED — 6 plans enumerated |
| `702edbe` | docs | update STATE.md — Phase 47 closure + Current Position → Phase 48 |
| `c683608` | docs | close Phase 47 REQ-WEB-TARGET-NN section in REQUIREMENTS.md |
| `cb91c46` | docs | add Compile-Target Flavors section to CLAUDE.md |
| `c8a304e` | fix | close test-project Web build via Compile Remove + FLOW_WEB propagation |

## Self-Check: PASSED

- File `.planning/phases/47-compile-target-flavors/47-VERIFICATION.md` exists ✓
- File `.planning/phases/47-compile-target-flavors/47-06-SUMMARY.md` exists ✓
- Commit `4ce8074` (Task 1 VERIFICATION) present in git log ✓
- Commit `42608b4` (Task 2 ROADMAP) present in git log ✓
- Commit `702edbe` (Task 3 STATE) present in git log ✓
- Commit `c683608` (Task 4 REQUIREMENTS) present in git log ✓
- Commit `cb91c46` (Task 5 CLAUDE.md) present in git log ✓
- Commit `c8a304e` (Task 6 test-project Web closer + retag) present in git log ✓
- `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Desktop` exits 0 ✓
- `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web` exits 0 ✓
- `dotnet build flow-lang.Tests/flow-lang.Tests.csproj -p:FlowTarget=Web` exits 0 (was 50 errors) ✓
- Desktop full suite 2127 PASS / 9 SKIP / 0 FAIL — zero new failures vs Plan 47-05 baseline ✓
- Web Phase 47 fixture 20 PASS / 4 SKIP / 0 FAIL — AssemblyReferenceScanTests + DryWetMidiWasmCompatTests + WebTargetParserTests + WebTargetModuleLoaderTests all GREEN ✓
- All 10 REQ-WEB-TARGET-NN entries in REQUIREMENTS.md marked [x] with closure citations ✓
- ROADMAP.md Phase 47 SHIPPED + Progress table Complete ✓
- STATE.md frontmatter (completed_phases 8, percent 64) + Phase 47 highlights ✓
- CLAUDE.md ## Compile-Target Flavors section present after ## Build & Run Commands ✓
