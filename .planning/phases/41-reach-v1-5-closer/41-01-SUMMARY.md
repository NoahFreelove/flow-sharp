---
phase: 41-reach-v1-5-closer
plan: 01
subsystem: testing
tags: [xunit, validation, wave-0, human-uat, rms-regression, audio-backend, doc-generator]

# Dependency graph
requires:
  - phase: 40-studio-sync
    provides: "40-HUMAN-UAT.md structural template (machine-vs-human split, flag-not-fake gate rows)"
  - phase: 28-articulation
    provides: "RmsRegressionTests.AssertRmsWithinTolerance (SPEC-8 ±0.5 dB / 100 ms) — the showcase RMS stub's target"
  - phase: 47-compile-target
    provides: "AssemblyReferenceScanTests forbidden-prefix gate pattern; CoreAudioBackend.IsAvailable() probe shape"
provides:
  - "8 Phase 41 xUnit test classes under flow-lang.Tests/Integration/Phase41/ (5 doc skip-stubs, WASAPI skip-stub, LIVE CoreAudio availability, showcase RMS skip-stub)"
  - "The Nyquist validation contract for Phase 41: every requirement (DOC-01/02, WASAPI-01, COREAUDIO-01, SHOWCASE-01) has a named, discoverable test target under FullyQualifiedName~Phase41"
  - "baselines/Phase41/ directory tracked (.gitkeep) for the 41-07 showcase baseline WAV"
  - "41-HUMAN-UAT.md — 7 honest pending cross-platform/external gate rows (flag, don't fake)"
affects: [41-02, 41-03, 41-04, 41-07, "Phase 41 verifier", "v1.5 milestone close"]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Wave-0 skip-stub: [Fact(Skip = \"Wave 0 stub — turns RED/GREEN when {plan} lands {feature}\")] so the class is discoverable but doesn't fail the suite prematurely"
    - "Live-vs-skip split within one wave: CoreAudio availability is LIVE (type already exists) while WASAPI is skip-stubbed (type lands in 41-04)"
    - "HUMAN-UAT ledger: every cross-platform/external gate is an honest pending row mirroring 40-HUMAN-UAT.md, never a fabricated pass"

key-files:
  created:
    - "flow-lang.Tests/Integration/Phase41/DocCommentLexTests.cs"
    - "flow-lang.Tests/Integration/Phase41/DocCommentBindTests.cs"
    - "flow-lang.Tests/Integration/Phase41/FlowDocGenTests.cs"
    - "flow-lang.Tests/Integration/Phase41/DocCacheTests.cs"
    - "flow-lang.Tests/Integration/Phase41/DocExampleExecTests.cs"
    - "flow-lang.Tests/Integration/Phase41/WasapiBackendAvailabilityTests.cs"
    - "flow-lang.Tests/Integration/Phase41/CoreAudioBackendAvailabilityTests.cs"
    - "flow-lang.Tests/Integration/Phase41/Phase41ShowcaseRmsTests.cs"
    - "flow-lang.Tests/baselines/Phase41/.gitkeep"
    - ".planning/phases/41-reach-v1-5-closer/41-HUMAN-UAT.md"
  modified: []

key-decisions:
  - "CoreAudioBackendAvailabilityTests created (no pre-existing duplicate — grep-confirmed before adding per 41-VALIDATION.md confirm-before-add note); made LIVE not skipped since CoreAudioBackend already exists"
  - "8 test classes created (the full set; no CoreAudio duplicate to skip)"
  - "All Phase 41 human gates recorded as 7 honest 'pending' rows in 41-HUMAN-UAT.md — zero faked passes"

patterns-established:
  - "Phase41 filter subset: namespace FlowLang.Tests.Integration.Phase41 + [Trait(Category, Phase41)] → discoverable under --filter FullyQualifiedName~Phase41 in <30 s"
  - "Skip-stub method names are exact downstream <automated> filter targets (TripleSlash_LexesAsDocComment, IsAvailable_FalseOnLinux_NoCrash, Showcase_RmsWithinTolerance, etc.)"

requirements-completed: [DOC-01, DOC-02, WASAPI-01, COREAUDIO-01, SHOWCASE-01]

# Metrics
duration: ~6min
completed: 2026-06-07
---

# Phase 41 Plan 01: Wave 0 Test Scaffolding + Human-Gate Ledger Summary

**Seeded the Phase 41 Nyquist validation contract — 8 named xUnit classes (5 doc skip-stubs, WASAPI skip-stub, a LIVE CoreAudio-availability test passing on this Linux box, and a showcase RMS skip-stub) plus a tracked `baselines/Phase41/` dir — and authored `41-HUMAN-UAT.md` with 7 honest pending cross-platform/external gate rows.**

## Performance

- **Duration:** ~6 min
- **Started:** 2026-06-07T23:38:37Z (plan execution start)
- **Completed:** 2026-06-07T23:42:21Z
- **Tasks:** 2
- **Files modified:** 10 created (8 test classes + .gitkeep + HUMAN-UAT.md)

## Accomplishments
- Every Phase 41 requirement now has a named, compiling, discoverable xUnit target so downstream plans turn RED→GREEN against concrete filters rather than inventing verification ad hoc: DOC-01 (lex/bind/gen/cache), DOC-02 (example exec), WASAPI-01 (Linux availability), COREAUDIO-01 (Linux availability — LIVE), SHOWCASE-01 (RMS regression).
- The CoreAudio availability test is LIVE and passes: `CoreAudioBackend.IsAvailable()` returns `false` on this Linux host without throwing (probe catches `DllNotFoundException`).
- `baselines/Phase41/` is tracked via `.gitkeep` so 41-07 has a home for the showcase baseline WAV.
- `41-HUMAN-UAT.md` records all 7 human gates (Windows WASAPI audible, macOS CoreAudio audible+<20 ms, osx-x64/osx-arm64/win-x64 exec smoke, JetBrains Marketplace publish, v1.5.0 GitHub Release) as honest `pending` rows — no fabricated pass — with the T-41-01-IDISCLOSE env-var-only secret-handling note for JET-01.

## Task Commits

Each task was committed atomically:

1. **Task 1: Seed the 8 Phase 41 xUnit test stubs + baseline dir** - `0a536b0` (test)
2. **Task 2: Author 41-HUMAN-UAT.md with honest cross-platform/external gate rows** - `dd81cb4` (docs)

**Plan metadata:** (this SUMMARY + STATE/ROADMAP/REQUIREMENTS updates) — see final commit.

## Files Created/Modified
- `flow-lang.Tests/Integration/Phase41/DocCommentLexTests.cs` - DOC-01 lexer stub (`///` vs `//` vs `/* */`); 3 skip-Facts until 41-02
- `flow-lang.Tests/Integration/Phase41/DocCommentBindTests.cs` - DOC-01 binding + charitable orphan-drop; 2 skip-Facts until 41-02
- `flow-lang.Tests/Integration/Phase41/FlowDocGenTests.cs` - DOC-01 HTML+Markdown emit + `--out` path-traversal (T-41); 2 skip-Facts until 41-03
- `flow-lang.Tests/Integration/Phase41/DocCacheTests.cs` - DOC-01 content-hash incremental cache; 2 skip-Facts until 41-03
- `flow-lang.Tests/Integration/Phase41/DocExampleExecTests.cs` - DOC-02 pass/`[example failed]` annotation; 2 skip-Facts until 41-03
- `flow-lang.Tests/Integration/Phase41/WasapiBackendAvailabilityTests.cs` - WASAPI-01 Linux `IsAvailable()==false` no-crash; 1 skip-Fact until 41-04
- `flow-lang.Tests/Integration/Phase41/CoreAudioBackendAvailabilityTests.cs` - COREAUDIO-01 Linux `IsAvailable()==false` no-throw; **1 LIVE Fact (passes now)**
- `flow-lang.Tests/Integration/Phase41/Phase41ShowcaseRmsTests.cs` - SHOWCASE-01 RMS regression vs `baselines/Phase41/`; 1 skip-Fact until 41-07
- `flow-lang.Tests/baselines/Phase41/.gitkeep` - tracks the baseline dir for the 41-07 showcase WAV
- `.planning/phases/41-reach-v1-5-closer/41-HUMAN-UAT.md` - 7 honest pending human-gate rows mirroring 40-HUMAN-UAT.md

## Decisions Made
- **No CoreAudio availability test pre-existed** (grep across `flow-lang.Tests/` returned nothing) → created `CoreAudioBackendAvailabilityTests.cs` as the full 8th class, and made it LIVE (not skipped) since `CoreAudioBackend.IsAvailable()` already exists and is verifiable on Linux today.
- **Skip-stub message convention** encodes the down-stream plan + feature (e.g. "turns RED/GREEN when 41-04 adds WasapiBackend.cs") so the next executor knows exactly which stub their plan flips.
- **All human gates are pending** — per CONTEXT D-05 + `feedback_autonomous_phase_execution`, the autonomous run flags but never fakes cross-platform/external-account verification.

## Deviations from Plan

None - plan executed exactly as written. The plan's conditional (skip CoreAudio test if a duplicate exists) resolved to "no duplicate → create it", which is the documented expected path, not a deviation.

## Issues Encountered
None. Build succeeded with 0 errors on first try (82 pre-existing warnings in unrelated Phase37/38/44 files — out of scope per the scope boundary, not touched). The Phase41 filter run reported 1 passed / 13 skipped / 0 failed.

## User Setup Required
None for this plan — but note `41-HUMAN-UAT.md` records 7 composer-action gates that must be cleared (on real Windows/macOS hardware + a JetBrains account) before Phase 41 / the v1.5 milestone flips to passed. Those are downstream of the autonomous code-landing plans (41-02..41-07), not this scaffolding plan.

## Next Phase Readiness
- Wave 0 complete: every downstream Phase 41 plan (41-02 lexer/parser, 41-03 generator+cache+examples, 41-04 WasapiBackend, 41-07 showcase) now has a concrete `<automated>` RED target to turn GREEN.
- 41-HUMAN-UAT.md is ready to receive composer sign-off as the cross-platform code lands.
- No blockers. The `41-VALIDATION.md` `wave_0_complete` flag can flip to true (the 8 test classes + baseline dir + HUMAN-UAT rows it enumerates all exist).

## Self-Check: PASSED

---
*Phase: 41-reach-v1-5-closer*
*Completed: 2026-06-07*
