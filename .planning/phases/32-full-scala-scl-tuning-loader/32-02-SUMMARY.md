---
phase: 32-full-scala-scl-tuning-loader
plan: 02
subsystem: tuning
tags: [scala, tuning, scl, kbm, parser, microtonal, diagnostics]

# Dependency graph
requires:
  - phase: 32-01
    provides: 5 canonical .scl fixtures + 3 malformed fixtures used as the parse-correctness + error-format battery
  - phase: 23
    provides: ParseException base class (flow-lang/Parsing/TypeParser.cs:335) + RatioMath cents helpers
provides:
  - ParsedScala record (Description + StepCents[] N-1 + PeriodCents + Ratios{} + FilePath) — consumed by Plan 32-03 to build ResolvedTuning
  - ScalaKbm value class (7-field header + Mapping entries + Period) — D-05/D-07 always-has-KBM internal model
  - ScalaParser.Parse(content, filePath) — single-pass .scl parser with strict D-18 rejects and {file}:{line}:{col} diagnostics
  - ScalaKbmParser.Parse + ScalaKbmParser.Default(scl) — .kbm parser + synthetic linear-mapping factory that auto-adopts the tuning's period
  - ScalaParseException + ScalaKbmParseException — extending FlowLang.Parsing.ParseException; em-dash U+2014 format consistent with Flow's existing diagnostics
affects: [32-03-resolved-tuning, 32-04-load-builtin, 32-05-tuning-block, downstream Phase 32 plans]

# Tech tracking
tech-stack:
  added: []  # No new NuGet packages — hand-rolled parsers per CLAUDE.md "Minimal Dependencies" guiding principle
  patterns:
    - "Single-pass line-by-line parser with explicit line/column tracking for {file}:{line}:{col} diagnostics (mirrors Flow's existing ParseException at TypeParser.cs:335)"
    - "Sealed exception class extending FlowLang.Parsing.ParseException with FilePath/Line/Column/Expected/Found accessors exposed for programmatic test inspection"
    - "ParsedScala as a sealed record colocated in the same file as ScalaParser — same-file value-object pattern used by Phase 23 Tuning types"
    - "Default(ParsedScala) static factory on ScalaKbmParser — Phase 32's structural-dissolution of the period-mismatch edge case (D-07)"
    - "NumberStyles.Float & ~NumberStyles.AllowExponent & ~NumberStyles.AllowThousands cents mask + CultureInfo.InvariantCulture exclusively (Pitfall 8 determinism guard + D-18 strict reject)"
    - "Bounded-loop DoS guard: MaxStepCount/MaxMappingEntries = 10000 caps on the two allocation loops (threats T-32-PARSE-01 + T-32-PARSE-02)"

key-files:
  created:
    - "flow-lang/StandardLibrary/Audio/Tuning/ScalaParseException.cs"
    - "flow-lang/StandardLibrary/Audio/Tuning/ScalaKbmParseException.cs"
    - "flow-lang/StandardLibrary/Audio/Tuning/ScalaKbm.cs"
    - "flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs"
    - "flow-lang/StandardLibrary/Audio/Tuning/ScalaKbmParser.cs"
    - "flow-lang.Tests/Unit/Phase32/ScalaParserFacts.cs"
    - "flow-lang.Tests/Unit/Phase32/ScalaParserErrorFacts.cs"
    - "flow-lang.Tests/Unit/Phase32/ScalaKbmParserFacts.cs"
    - ".planning/phases/32-full-scala-scl-tuning-loader/deferred-items.md"
  modified: []

key-decisions:
  - "Co-locate ParsedScala record in ScalaParser.cs rather than a sibling file — same-file pattern matches the Phase 23 Tuning types (RenderTuning + TuningSystem + Mode all colocate their data types), keeps the parser/result coupling visible"
  - "Defensive D-18 reject of `3 / 2`: after taking the first whitespace token (cents=`3`), scan the rest of the line for a stray `/` and reject the entire authored sequence (e.g. found='3 / 2') so the diagnostic surface is human-readable rather than a confusing 'expected ratio got 3' on an integer that IS a valid bare-ratio form"
  - "Defensive comma + e/E character check in the cents path alongside the NumberStyles mask — even though InvariantCulture + the mask alone would reject `100,5` and `1.5e2`, the explicit char check makes the strict-reject intent obvious to future readers and surfaces a deterministic 'cents value or ratio' error message"
  - "ScalaKbmParser.Parse returns Period=0.0 placeholder; final ResolvedTuning builder in Plan 32-03 must overlay scl.PeriodCents (D-07). Plan 32-03 owns the cross-format wiring — keeping Parse single-format-scoped avoids cross-file dependencies in Plan 32-02"
  - "Non-zero formal-octave field strict-rejected with 'expected formal octave 0 (non-zero deferred to v1.5)' per RESEARCH A10 — the alternative (charitable-tolerate as 0) would silently mis-render the rare archive file that authors a non-zero value"
  - "Bounded-loop guard for size > 10000 fires BEFORE the int?[] allocation (T-32-PARSE-02 DoS mitigation grep-verified at line 68)"

patterns-established:
  - "Pattern: hand-rolled spec-compliant parser with explicit line/column tracking + dedicated exception class extending Flow's ParseException — reusable template for any future format loader (e.g. .sf2, .sfz)"
  - "Pattern: ScalaKbmParser.Default(parsedScala) → synthetic value-object factory that auto-adopts the parent value's invariants — dissolves period-mismatch edge cases structurally rather than via runtime checks"
  - "Pattern: TDD-by-plan-task — write fixture-driven Facts in RED, observe the build-error confirming the missing public surface, implement to GREEN, commit each cycle separately"

requirements-completed: [SPEC-3, SPEC-4, SPEC-7]

# Metrics
duration: ~35min
completed: 2026-05-14
---

# Phase 32 Plan 02: ScalaParser + ScalaKbmParser + Diagnostics Summary

**Hand-rolled `.scl` + `.kbm` parsers shipping the {file}:{line}:{col} — expected X, got 'Y' diagnostic format Flow's other parsers use. 5 source files + 3 test classes / 20 Facts. Closes SPEC-3 (.scl), SPEC-4 (.kbm), and SPEC-7 (error semantics).**

## Performance

- **Duration:** ~35 min (executor start to SUMMARY commit)
- **Started:** 2026-05-14
- **Completed:** 2026-05-14
- **Tasks:** 3 / 3
- **Files created:** 9 (5 source + 3 test + 1 deferred-items)
- **Files modified:** 0
- **Test Facts added:** 20 (7 happy-path .scl + 5 error-path .scl + 8 .kbm including 2 exception-format)
- **Test Facts passing post-plan:** 111 / 111 GREEN across Phase 32 + Phase 23 regression sweep

## Accomplishments

### Task 1 — Foundational types

- **`ScalaParseException`** + **`ScalaKbmParseException`**: sealed, both extend Flow's existing `FlowLang.Parsing.ParseException` (TypeParser.cs:335). Message format matches Flow's established diagnostic style — `{file}:{line}:{col} — expected X, got 'Y'` with em-dash U+2014. Exposes `FilePath` / `Line` / `Column` / `Expected` / `Found` accessors for programmatic test inspection.
- **`ScalaKbm`**: sealed value class holding the 7 Huygens-Fokker `.kbm` header fields (Size, FirstMidi, LastMidi, MiddleNote, ReferenceNote, ReferenceHz, FormalOctave) + the trailing `Mapping` entries (`int?[]` where `null` = unmapped `x` per D-08) + the `Period` mirror (auto-adopted from the parent .scl's `PeriodCents` per D-07). `ToString()` override surfaces all fields for debugging.

### Task 2 — `.scl` parser + tests

- **`ScalaParser.Parse(content, filePath)`** → `ParsedScala`. Single-pass, line by line, LF + CRLF both supported (`StripCr` per-line). Returns the description (verbatim first non-comment line, trimmed) + `StepCents[]` (length N-1 intra-period per D-10) + `PeriodCents` (dedicated field) + `Ratios{}` dict (original n/d form preserved for ratio inputs only per D-11) + `FilePath`.
- **Spec compliance highlights:**
  - `!` comments skipped anywhere; leading blank/comment lines tolerated before the description (RESEARCH A1 charitable)
  - Cents-vs-ratio decided by presence of `.` per spec; bare integer (e.g. `2`) parses as `2/1` per spec; "anything after a valid pitch value should be ignored" — first whitespace-delimited token only
  - Negative cents accepted verbatim per D-09 (descending pitches)
- **Strict-reject rules per D-18:** `3 / 2` (whitespace around slash), `1.5e2` (scientific notation), `100,5` (comma-decimal) all surface as `ScalaParseException` with `Expected = "cents value or ratio"`.
- **DoS guard:** `MaxStepCount = 10000` cap on the step-count allocation loop (threat T-32-PARSE-01).
- **Determinism guard:** zero raw `double.Parse`/`int.Parse` calls — all numeric parsing routes through `CultureInfo.InvariantCulture` + a `NumberStyles` mask that excludes `AllowExponent` and `AllowThousands` (Pitfall 8).
- **12 Facts cover** all 5 canonical fixtures, the D-09 negative-cents path, the RESEARCH A1 comments-only-header path, the 2 malformed fixtures (exact line/column/message regex), and the 3 D-18 strict-reject paths.

### Task 3 — `.kbm` parser + Default factory + tests

- **`ScalaKbmParser.Parse(content, filePath)`** reads the 7-field header + Size-many mapping entries. Validates MIDI range (0..127), `firstMidi ≤ lastMidi`, `referenceHz > 0`, formal-octave == 0 (non-zero strict-rejected per RESEARCH A10), and mapping entries (non-negative int OR literal lowercase `x`).
- **`ScalaKbmParser.Default(ParsedScala scl)`** synthesizes the linear-mapping KBM (Size=0, Middle=60, Reference=69@440Hz, FirstMidi=0, LastMidi=127). Period auto-adopts `scl.PeriodCents` per D-07 — Carlos Alpha + default KBM produces a non-octave keyboard automatically.
- **DoS guard:** `MaxMappingEntries = 10000` cap on the mapping-entries allocation loop (threat T-32-PARSE-02).
- **8 Facts cover** the 2 Task-1 exception-format Facts + Default factory for both octave-repeating (partch_43 → 1200¢) and non-octave (carlos_alpha → 1404¢) tunings + minimal valid `.kbm` + unmapped-`x` mapping entry + malformed_kbm.kbm exact line:col + non-zero formal-octave reject.

## Task Commits

| Task | Description                                                              | RED            | GREEN          |
| ---- | ------------------------------------------------------------------------ | -------------- | -------------- |
| 1    | Exception classes + ScalaKbm value class                                 | `c450886` test | `75a4cea` feat |
| 2    | ScalaParser (.scl format) + ScalaParserFacts + ScalaParserErrorFacts     | `dc1979d` test | `dc8e656` feat |
| 3    | ScalaKbmParser (.kbm format) + Default factory + extended Facts          | `5c6e95a` test | `78c5b7d` feat |

_All 6 commits are atomic; the orchestrator will add the metadata commit (SUMMARY.md) post-merge._

## Files Created/Modified

### Created (source — `flow-lang/StandardLibrary/Audio/Tuning/`)
- `ScalaParseException.cs` — 33 lines; sealed class extending `FlowLang.Parsing.ParseException`; em-dash U+2014 separator
- `ScalaKbmParseException.cs` — 33 lines; distinct exception type (not a reuse) so callers can `Assert.Throws<…>` per format
- `ScalaKbm.cs` — 64 lines; 7-field header + Mapping + Period mirror; `ToString()` debug-friendly
- `ScalaParser.cs` — 302 lines; ParsedScala record + parser class + NextStep helper + StripCr helper
- `ScalaKbmParser.cs` — 184 lines; Parse + Default factory + ReadInt/ReadDouble/NextField helpers + StripCr helper

### Created (tests — `flow-lang.Tests/Unit/Phase32/`)
- `ScalaParserFacts.cs` — 162 lines; 7 happy-path Facts (5 canonical fixtures + D-09 negative cents + RESEARCH A1 charitable header)
- `ScalaParserErrorFacts.cs` — 122 lines; 5 error-path Facts (2 malformed fixtures + 3 D-18 strict-reject)
- `ScalaKbmParserFacts.cs` — 211 lines; 8 Facts (2 exception-format from Task 1 + 6 parser/factory from Task 3)

### Created (planning — `.planning/phases/32-full-scala-scl-tuning-loader/`)
- `deferred-items.md` — logs the 26 pre-existing Phase 28 test failures observed during the regression sweep (NOT caused by this plan)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking Issue] dotnet test filter syntax mismatch**
- **Found during:** Task 1 verification (`dotnet test --filter ClassName~ScalaKbmParserFacts` returned "No test matches the given testcase filter")
- **Issue:** The acceptance criteria in the plan used `ClassName~X` filter syntax; xUnit v3 (Microsoft.NET.Test.Sdk 17.13 + xunit.v3 3.2.2 in this repo) requires `FullyQualifiedName~X` instead. The plan's acceptance commands as written would always report 0 matches.
- **Fix:** Substituted `FullyQualifiedName~` for `ClassName~` in all verification commands. Each Facts class is still uniquely identified because the FullyQualifiedName includes the namespace + class name; substring match resolves correctly.
- **Files modified:** None (verification command only — no source change)
- **Commit:** No code change required; recorded here for the next planner so they don't propagate the broken filter.

### Plan-Spec Adherence

- Plan Task 1 says the `ScalaParseException` constructor signature is `(string filePath, int line, int column, string expected, string found)`. Shipped exactly as specified.
- Plan Task 2 acceptance says "AllowExponent | grep -v '^#' is EMPTY OR every match is preceded by `~`". Shipped result: 4 matches; all 4 are either inside XML-doc comments or include the bitmask negation (`~NumberStyles.AllowExponent`) — the spirit of the guard (no positive AllowExponent uses) is honored.
- Plan Task 3 says ScalaKbmParser.Parse "After the 7 header fields, read EXACTLY `size` mapping entries". Shipped a bounded `for (int i = 0; i < size; i++)` loop with `Size` validated to `<= MaxMappingEntries = 10000` BEFORE the int?[] allocation. Mitigates T-32-PARSE-02 per the threat model.

## Pre-existing Failures (Out of Scope)

A full `dotnet test flow-lang.Tests --no-build` run reveals 26 pre-existing failures in Phase 28 unrelated to this plan:

- **`Phase28.PerSynthArticulationTests.PerSynthArticulation_NormalVsArticulated_FFTCosineDifferentiable`** — 24 parameterized variants (every synth × articulation combo) failing in the FFT cosine differentiability check
- **`Phase28.RagtimeFixtureTests.Ragtime_Synthetic_RmsRegression`** + **`Phase28.RagtimeFixtureTests.Ragtime_MapleLeaf_RmsRegression`** — RMS deviation exceeds ±0.5 dB tolerance (delta 1.07 dB)

These are pre-existing — verified by running the same filter against the worktree's baseline commit `efd875c` before any plan changes. Phase 32 Plan 02 only adds new files under `flow-lang/StandardLibrary/Audio/Tuning/` + `flow-lang.Tests/Unit/Phase32/`; no Phase 28 code is touched. Logged to `deferred-items.md` per the executor's SCOPE BOUNDARY rule.

## Authentication Gates Encountered

None. Plan 32-02 is pure C# implementation + xUnit tests; no auth required.

## Acceptance Verification

All `<acceptance_criteria>` items pass for all 3 tasks:

**Task 1:**
- `flow-lang/StandardLibrary/Audio/Tuning/ScalaParseException.cs` contains `public sealed class ScalaParseException : ParseException` (line 13)
- `flow-lang/StandardLibrary/Audio/Tuning/ScalaKbmParseException.cs` contains `public sealed class ScalaKbmParseException` (line 12)
- `flow-lang/StandardLibrary/Audio/Tuning/ScalaKbm.cs` contains `public sealed class ScalaKbm` with 9 documented properties (line 25)
- `dotnet build flow-lang.Tests/flow-lang.Tests.csproj -v minimal` exits 0 (Build succeeded)
- ≥ 2 exception-message-format Facts in `ScalaKbmParserFacts.cs` pass via dotnet test

**Task 2:**
- `dotnet test --filter FullyQualifiedName~ScalaParserFacts` reports 7 Facts passed (target ≥ 7)
- `dotnet test --filter FullyQualifiedName~ScalaParserErrorFacts` reports 5 Facts passed (target ≥ 5)
- `grep 'class ScalaParser' ScalaParser.cs` → one match marked `public sealed` (line 46)
- `grep AllowExponent ScalaParser.cs` → all 4 matches either in XML-doc comments or preceded by `~` bitmask negation
- `grep -E 'double\.Parse|int\.Parse'` → ZERO matches in ScalaParser.cs (only `TryParse` with `InvariantCulture`)
- Bounded-loop guard `grep 10000` → ≥ 1 match (4 total: const + docstring + check + error)

**Task 3:**
- `dotnet test --filter FullyQualifiedName~ScalaKbmParserFacts` reports 8 Facts passed (target ≥ 8)
- `grep 'public static ScalaKbm Default' ScalaKbmParser.cs` → 1 match (line 34)
- `grep 'public static ScalaKbm Parse' ScalaKbmParser.cs` → 1 match (line 54)
- Bounded-loop guard `grep 10000` → 3 matches (docstring + const + check)
- `dotnet test --filter FullyQualifiedName~Phase23 --no-build` exits 0; 91 Facts passed (regression sweep stays 100% GREEN)

## Threat Model Adherence

All 5 mitigations declared in `<threat_model>` are in place:

- **T-32-PARSE-01 (DoS, ScalaParser step-count loop):** Mitigated — `MaxStepCount = 10000` cap enforced BEFORE the `double[] cents = new double[stepCount]` allocation (ScalaParser.cs lines 121-125). Verified by grep gate.
- **T-32-PARSE-02 (DoS, ScalaKbmParser mapping-entries loop):** Mitigated — `MaxMappingEntries = 10000` cap enforced BEFORE the `int?[] mapping = new int?[size]` allocation (ScalaKbmParser.cs lines 66-71). Verified by grep gate.
- **T-32-PARSE-03 (Tampering, locale-dependent numeric parsing):** Mitigated — both parsers use `CultureInfo.InvariantCulture` exclusively for all numeric parsing; the `100,5` D-18 test Fact pins the InvariantCulture guard against comma-decimal interpretation.
- **T-32-PARSE-04 (Elevation via unbounded recursion):** Accepted as low-risk — both parsers are fully iterative (single while-loop + bounded for-loop per parser); no call-stack-depth threat exists.
- **T-32-PARSE-05 (Information Disclosure via 'Found' echoing untrusted content):** Accepted — tokens are bounded by line length, quoted, and surface no shell-meta interpretation; matches Flow's existing ParseException style at TypeParser.cs:335.

## Known Stubs

None. Plan 32-02 ships the full parser surface specified in `<interfaces>` — `ParsedScala`, `ScalaKbm`, `ScalaParser.Parse`, `ScalaKbmParser.Parse`, `ScalaKbmParser.Default`. The only "placeholder" is `ScalaKbm.Period == 0.0` returned from `ScalaKbmParser.Parse` — and that's intentional, documented in the XML-doc, and Plan 32-03's `ResolvedTuning` builder owns the cross-format overlay.

## TDD Gate Compliance

All 3 tasks followed the RED → GREEN sequence as a single feature per `tdd="true"`:

- **Task 1:** `c450886` test (RED) → `75a4cea` feat (GREEN) — confirmed RED via missing-type build error
- **Task 2:** `dc1979d` test (RED) → `dc8e656` feat (GREEN) — confirmed RED via 10× CS0103 errors
- **Task 3:** `5c6e95a` test (RED) → `78c5b7d` feat (GREEN) — confirmed RED via 6× CS0103 errors

No REFACTOR commits needed — implementations were minimal-to-GREEN on first try.

## Self-Check: PASSED

All 9 claimed artifacts exist on disk:
- `flow-lang/StandardLibrary/Audio/Tuning/ScalaParseException.cs` — FOUND
- `flow-lang/StandardLibrary/Audio/Tuning/ScalaKbmParseException.cs` — FOUND
- `flow-lang/StandardLibrary/Audio/Tuning/ScalaKbm.cs` — FOUND
- `flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs` — FOUND
- `flow-lang/StandardLibrary/Audio/Tuning/ScalaKbmParser.cs` — FOUND
- `flow-lang.Tests/Unit/Phase32/ScalaParserFacts.cs` — FOUND
- `flow-lang.Tests/Unit/Phase32/ScalaParserErrorFacts.cs` — FOUND
- `flow-lang.Tests/Unit/Phase32/ScalaKbmParserFacts.cs` — FOUND
- `.planning/phases/32-full-scala-scl-tuning-loader/deferred-items.md` — FOUND

All 6 task commits exist in git log:
- `c450886` (Task 1 RED) — FOUND
- `75a4cea` (Task 1 GREEN) — FOUND
- `dc1979d` (Task 2 RED) — FOUND
- `dc8e656` (Task 2 GREEN) — FOUND
- `5c6e95a` (Task 3 RED) — FOUND
- `78c5b7d` (Task 3 GREEN) — FOUND
