---
phase: 24-scale-linting-flow-lsp
plan: 02
subsystem: diagnostics
tags: [diagnostics, spellings, closed-set, phase-24, wave-1, scale-lint, music-theory]

# Dependency graph
requires:
  - phase: 23-microtonal-tuning-wedge
    provides: "Mode enum (Major/Minor/Dorian/Phrygian/Lydian/Mixolydian/Locrian) + ScaleDatabase.TryParseKeyWithMode 17-root accept-set"
  - phase: 21-pragma-system-h-alias
    provides: "PragmaRegistry.KnownPragmas — Phase 24 pragma reservation slot for `scaleLint`"
provides:
  - "DiatonicSpellings.GetDiatonicSpellings(string root, Mode mode) → IReadOnlySet<string>? — closed-set lookup over 119 (root, mode) pairs"
  - "DiatonicSpellings.EntryCount property pinned at 119 by Map_HasExactly119Entries Fact"
  - "Spelling-aware diatonic membership semantics (D-01): Cmajor's set is {C,D,E,F,G,A,B} — does NOT include E# or Gb"
  - "D-22 silent fail-open contract: unknown (root, mode) pair returns null instead of throwing"
affects: [phase-24-plan-03-analyzer, scale-lint, lint-01, lint-02, lint-03]

# Tech tracking
tech-stack:
  added: []  # zero new dependencies — pure C# under flow-lsp
  patterns:
    - "Pattern 2: Closed-set hardcoded lookup table (mirrors TuningTables.cs:60-188 precedent)"
    - "D-04 zero-flow-lang-touch invariant: helper lives entirely under flow-lsp/"
    - "D-22 silent fail-open: null on unknown input rather than throw"

key-files:
  created:
    - "flow-lsp/Diagnostics/DiatonicSpellings.cs"
    - "flow-lang.Tests/Unit/Phase24/DiatonicSpellingsFacts.cs"
  modified: []  # ZERO modifications to flow-lang/

key-decisions:
  - "D-04 enforced: helper lives in flow-lsp/Diagnostics/, not flow-lang/. Audit-grep verified no flow-lang/ files modified."
  - "Closed-set hardcoded over circle-of-fifths algorithm: 119 entries scan-readable, mirrors TuningTables precedent (RESEARCH Pattern 2)."
  - "Map keys mirror ScaleDatabase.NoteToSemitone (17 canonical root spellings: C, Csharp, Db, D, Dsharp, Eb, E, F, Fsharp, Gb, G, Gsharp, Ab, A, Asharp, Bb, B) — exact match required for D-22 fail-open + closed-set integrity Fact."
  - "Return type IReadOnlySet<string>? (vs string[]): O(1) Contains() in the analyzer is the hot path; HashSet construction allocates 7 strings per call but didChange debounce caps cost."
  - "Double-sharps and double-flats preserved (e.g., F## in Csharp Lydian, Bbb in Db Phrygian): NOT enharmonically simplified — spelling-awareness IS the point per D-01."

patterns-established:
  - "Closed-set growth pattern: count Fact (Map_HasExactly119Entries) is the canonical audit trail when the closed set ever extends."
  - "Spelling-aware canary Fact pattern: Cmajor_DoesNotContainEsharp + Cmajor_DoesNotContainGb pin the D-01 invariant explicitly so a future regression to pitch-class-only membership fails immediately."
  - "Closed-set integrity Fact pattern: GetDiatonicSpellings_AllRootsAllModes_NonNull pins full root × mode coverage — ensures the analyzer never silently fails open on a (root, mode) ScaleDatabase accepts."

requirements-completed: [LINT-01]  # foundation only — analyzer in 24-03 closes acceptance

# Metrics
duration: 7min
completed: 2026-05-04
---

# Phase 24 Plan 02: DiatonicSpellings Closed-Set Helper Summary

**Shipped a 119-entry hardcoded diatonic-spelling lookup (`flow-lsp/Diagnostics/DiatonicSpellings.cs`) with letter+accidental membership semantics, providing the closed-set foundation Plan 24-03's analyzer consumes for LINT-01 acceptance — zero flow-lang touch.**

## Performance

- **Duration:** ~7 min
- **Started:** 2026-05-04T17:14:41Z
- **Completed:** 2026-05-04T17:21:21Z
- **Tasks:** 2/2
- **Files created:** 2 (1 production + 1 test)
- **Files modified in flow-lang/:** 0 (D-04 zero-flow-lang-touch invariant honored)

## Accomplishments

- **119-entry closed-set map shipped:** 17 canonical root spellings × 7 church modes covering every (root, mode) pair `ScaleDatabase.TryParseKeyWithMode` accepts.
- **Spelling-aware semantics pinned (D-01):** `Cmajor_DoesNotContainEsharp` + `Cmajor_DoesNotContainGb` Facts encode the project's "letter+accidental, not pitch-class" membership rule explicitly. Pitch-class 5 (= F natural) is in Cmajor's set; the spelling `E#` is not.
- **Silent fail-open contract pinned (D-22):** `GetDiatonicSpellings("NotARealRoot", Mode.Major)` returns `null` — analyzer treats null as "no diagnostics" so unknown input never crashes the LSP.
- **Closed-set integrity audit Fact:** `GetDiatonicSpellings_AllRootsAllModes_NonNull` iterates every (root, mode) pair `ScaleDatabase.TryParseKeyWithMode` accepts and asserts the map covers it — drift between the two surfaces fails immediately.
- **Zero new dependencies:** pure hand-rolled C# data table, mirrors `TuningTables.cs:60-188` precedent.
- **Zero flow-lang touch:** verified via `git diff --name-only HEAD HEAD~2 | grep flow-lang/` returns no production source files (Phase 24's only flow-lang touch is the one-line PragmaRegistry add owned by parallel sibling Plan 24-01).

## Task Commits

Each task was committed atomically (TDD RED → GREEN):

1. **Task 1: Add 6 RED xUnit Facts pinning DiatonicSpellings contract** — `9eae7ae` (test)
   - 30 InlineData rows covering all 7 modes × C, all 17 roots × ≥1 mode, cross-mode coverage on non-C roots, enharmonic-distinct minor roots (D#/A#/G#)
   - 5 Facts: `Cmajor_DoesNotContainEsharp`, `Cmajor_DoesNotContainGb`, `Map_HasExactly119Entries`, `GetDiatonicSpellings_UnknownRoot_ReturnsNull`, `GetDiatonicSpellings_AllRootsAllModes_NonNull`
   - Build deliberately fails: `error CS0234: The type or namespace name 'Diagnostics' does not exist in the namespace 'FlowLsp'` — RED state

2. **Task 2: Ship the 119-entry DiatonicSpellings map (turns Task 1 GREEN)** — `94ccdaf` (feat)
   - 119 dictionary entries (17 × 7 = 119, audited via `grep -c '\[("'`)
   - All 17 canonical root spellings present as map keys (audited via `grep -oP '\("(?:C|Csharp|...|B)"' | sort -u | wc -l` = 17)
   - All 7 modes appear ≥119 times (audited via `grep -cE 'Mode\.(Major|...|Locrian)'` = 119)
   - `public static class DiatonicSpellings` (no InternalsVisibleTo on flow-lsp.csproj — verified)
   - `dotnet test --filter "FullyQualifiedName~DiatonicSpellingsFacts"`: **35/35 passed** (30 Theory rows + 5 Facts)
   - Full suite regression: **640/640 passed** (no other tests perturbed)

## Files Created/Modified

- `flow-lsp/Diagnostics/DiatonicSpellings.cs` *(NEW, 199 lines)* — Closed-set 17 × 7 = 119-entry hardcoded spelling map. Public static. `GetDiatonicSpellings(string root, Mode mode) → IReadOnlySet<string>?` returns 7 letter+accidental strings or null. `EntryCount` property pinned at 119.
- `flow-lang.Tests/Unit/Phase24/DiatonicSpellingsFacts.cs` *(NEW, 117 lines)* — xUnit Theory + 5 Facts. Phase24 directory itself was new (mirrors `Phase17/`, `Phase21/`, `Phase23/` convention).

## Decisions Made

None — followed plan exactly as written. The plan already encoded the 119 entries verbatim, the InlineData rows verbatim, and the spelling-aware canaries verbatim. The executor's job was data integrity verification (spot-checked Csharp Lydian's F## degree, Db Phrygian's Bbb degree, Gb Locrian's Dbb degree, Asharp Lydian's full chromatic spelling, Eb Locrian's Bbb degree, Ab Locrian's full chromatic) before writing.

## Deviations from Plan

None — plan executed exactly as written.

## Spelling-Aware Canary Cases Pinned (D-01)

The plan's defining invariant — that the analyzer compares letter+accidental, not pitch-class — is explicitly encoded by these test cases:

| Canary | Expected Behavior | Pinned By |
|--------|-------------------|-----------|
| `Cmajor` excludes `E#` | pitch-class 5 IS diatonic (= F natural), spelling E# is NOT | `Cmajor_DoesNotContainEsharp` |
| `Cmajor` excludes `Gb` | pitch-class 6 is non-diatonic regardless of spelling | `Cmajor_DoesNotContainGb` |
| `Cmajor` excludes `F#` | both spellings of pitch-class 6 flagged | `Cmajor_DoesNotContainGb` (asserts both) |
| `Fsharp major` includes `E#` | canonical 7th degree of F# major IS E#, not F | InlineData row 17 |
| `Csharp major` includes `B#` | canonical 7th degree of C# major IS B#, not C | InlineData row 18 |
| `Gb major` includes `Cb` | canonical 4th degree of Gb major IS Cb, not B | InlineData row 22 |
| `Dsharp minor` ≠ `Eb minor` | enharmonic-equivalent roots have spelling-distinct sets (D# minor's `E#` vs Eb minor's `F`) | InlineData rows 28 vs Eb-Mode.Minor (in Map) |

## D-22 Silent Fail-Open Contract Pinned

`GetDiatonicSpellings_UnknownRoot_ReturnsNull` proves the helper returns `null` (not throws) on unknown input. This is the foundation for the analyzer's "fail open" posture: when the innermost enclosing key is itself non-parseable (e.g., a future `key Eblues { }`), the analyzer emits zero diagnostics and the LSP stays charitable per the project's `feedback_charitable_interpretation` memory.

## Pattern 2 Hardcoded-Data Justification (RESEARCH)

A circle-of-fifths algorithm could derive these 119 entries in ~30 lines of code. We rejected that approach for three reasons documented in 24-RESEARCH.md Pattern 2:

1. **Auditability**: a composer can scan Eb major's diatonic spelling row and verify it in 5 seconds; an algorithm review requires "trust the algorithm" reasoning.
2. **Existing precedent**: `flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs:60-188` is the same hardcoded mode-keyed-table style, already shipped and trusted.
3. **Closed-set posture**: `Mode` is a closed C# enum (7 values), `ScaleDatabase.NoteToSemitone` is a closed dictionary (17 keys). The product is finite and stable; algorithmic generality buys nothing.

The Map_HasExactly119Entries Fact is the canonical audit trail if a future phase ever extends the root set or adds non-church modes.

## Threat Flags

None — file is internal data lookup with no network, file, or auth surface.

## Self-Check: PASSED

Verifications performed before SUMMARY write:

- **File `flow-lsp/Diagnostics/DiatonicSpellings.cs` exists:** FOUND (199 lines, namespace `FlowLsp.Diagnostics;`)
- **File `flow-lang.Tests/Unit/Phase24/DiatonicSpellingsFacts.cs` exists:** FOUND (117 lines, namespace `FlowLang.Tests.Unit.Phase24`)
- **Commit `9eae7ae` exists:** FOUND (`test(24-02): add 6 RED Facts pinning DiatonicSpellings 119-entry contract`)
- **Commit `94ccdaf` exists:** FOUND (`feat(24-02): ship 119-entry DiatonicSpellings closed-set lookup`)
- **`dotnet test --filter "FullyQualifiedName~DiatonicSpellingsFacts"`:** 35/35 passed (30 Theory rows + 5 Facts)
- **Full suite regression:** 640/640 passed
- **Map_HasExactly119Entries Fact:** PASSED (entry count = 119)
- **GetDiatonicSpellings_AllRootsAllModes_NonNull:** PASSED (every 17 × 7 pair returns non-null)
- **Cmajor_DoesNotContainEsharp:** PASSED (D-01 pinned)
- **Cmajor_DoesNotContainGb:** PASSED (D-01 pinned)
- **GetDiatonicSpellings_UnknownRoot_ReturnsNull:** PASSED (D-22 pinned)
- **Zero flow-lang/ modifications:** VERIFIED via `git diff --name-only HEAD~2 HEAD | grep -E '^flow-lang/'` — empty result (D-04 invariant honored)
- **`grep -c '\[("' flow-lsp/Diagnostics/DiatonicSpellings.cs`:** 119
- **`grep -c 'public static class DiatonicSpellings' flow-lsp/Diagnostics/DiatonicSpellings.cs`:** 1
