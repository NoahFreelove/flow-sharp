---
phase: 18-foundation-rational-duration-arithmetic
plan: 02
subsystem: type-system
tags: [foundation, musical-note-data, byte-identical-determinism, regression-gate, dormant-wiring, defaulted-parameter]

# Dependency graph
requires:
  - phase: 18-01
    provides: Fraction primitive (readonly record struct at flow-lang/TypeSystem/Fraction.cs) consumed via FQN reference inside MusicalNoteData
provides:
  - "MusicalNoteData.DurationFraction nullable property (Fraction?) — DORMANT in Phase 18, activated by Phase 19 lexer/parser"
  - "Defaulted 13th ctor parameter durationFraction = null appended at END of signature — zero call-site edits across 30+ existing new MusicalNoteData(...) sites"
  - "GetBeats branch on DurationFraction.HasValue — rational override path when set, existing power-of-2 enum path verbatim when null"
  - "6 unit Facts pinning FRAC-02 ctor wiring + GetBeats branch shape + ToString unchanged"
  - "4 integration Facts pinning byte-identical determinism contract for examples/tutorial.flow + examples/showcase.flow (WAV + MIDI each, two-runner pattern per D-USER-02)"
affects:
  - "Phase 19 tuplets (TUP-01..08) — will feed non-null DurationFraction values from {N:M ...} bracket lexer/parser; this plan ships the storage and GetBeats consumer ready"
  - "MusicalNoteData byte-identity contract pinned across tutorial.flow + showcase.flow + 54 tests/test_*.flow (FlowScriptTests Theory harness automatic coverage)"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Defaulted-parameter additive-field migration (RESEARCH §3 Pattern 1) — appending nullable defaulted parameter at END of ctor signature so all positional-arg call sites compile unmodified"
    - "GetBeats branch on HasValue (RESEARCH §3 Pattern 2) — single chokepoint method decides which duration representation to use; existing path runs verbatim when null"
    - "Two-runner byte-identical Fact (Phase 15 EuclideanByteIdenticalTests precedent) — two FRESH FlowEngineRunner instances + per-run output paths + SequenceEqual comparison; no committed binary baseline (D-USER-02)"
    - "Path substitution via String.Replace at runtime — read examples/{tutorial,showcase}.flow, rewrite the writeWav/writeMidi target path per run, fail-loud on substitution miss via Assert.NotEqual(source, sourceRun1)"

key-files:
  created:
    - "flow-lang.Tests/Unit/Phase18/MusicalNoteDataTests.cs (68 lines, 6 Facts)"
    - "flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs (105 lines, 2 Facts + shared helper)"
    - "flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs (87 lines, 2 Facts + shared helper)"
  modified:
    - "flow-lang/TypeSystem/SpecialTypes/NoteType.cs (+27/-1 lines: property decl + ctor param + ctor body assignment + GetBeats branch + xmldoc)"

key-decisions:
  - "Defaulted-parameter additive migration over named-arg per-site refactor — appending Fraction? durationFraction = null at END of 12-param ctor produces zero call-site edits structurally; Pitfall 2 (positional-arg shifting) avoided by appending not inserting"
  - "FQN reference FlowLang.TypeSystem.Fraction over adding using statement — minimal blast radius; SpecialTypes namespace stays at namespace-statement-count of 1"
  - "Branch on HasValue at TOP of GetBeats — rational path runs first when set; existing power-of-2 path runs verbatim when null (zero diff vs pre-Phase-18 behavior for current call paths)"
  - "Beats formula in quarter-note units (music21 convention): beats = (Num × timeSigDenominator) / (Denom × 4.0) — pinned in xmldoc + Test 5 GetBeats_DurationFractionSet_OverridesEnum"
  - "ToString deliberately UNCHANGED in Phase 18 (Pitfall 5 mitigation) — 54 tests/test_*.flow scripts emit (str note) sentinels that depend on existing 'quarter(C4)' format; pinned by Test 6 ToString_UnchangedFromPreFraction asserting non-null DurationFraction does NOT surface"
  - "GetBeats keeps double return signature (D-USER-01) — sibling GetBeatsFraction deferred to Phase 19 if TUP-05 bar-fit validator needs it; minimizes Phase 18 blast radius across 21 GetBeats consumers"
  - "Two-runner Fact over committed binary baseline (D-USER-02) — Phase 15 EuclideanByteIdenticalTests pattern verbatim; mirrors RESEARCH Open Q 3 fallback option (b); no .planning/phases/18-.../baseline/ directory exists"
  - "Path substitution via String.Replace with Assert.NotEqual fail-loud guard — rewrites tutorial.flow's writeWav/writeMidi target path per run before passing to RunSource; if the substitution did not actually replace anything, Fact fails rather than false-passes"
  - "Atomic commit landing all 4 files (NoteType.cs + 3 test files) — Phase 12-02 6e5a960 + Phase 15-05 + Phase 18-01 2092f32 precedent for foundation-tier landings where RED state is empirically confirmed via incremental local build before GREEN test files land"
  - "Phase 18 Plan 18-01 + 18-02 cumulative test count 9 + 6 + 4 = 19 Phase18 Facts; full suite at pre-Phase-18 baseline + 19 = 306/306 GREEN"

patterns-established:
  - "Inverse-success-criterion phase pattern (D-USER-04) — Phase 18 ships wiring + regression gate but produces zero behavioral change; success measured by full-suite GREEN at +19 Facts AND zero pre-existing Fact regression"
  - "Two-runner Fact via runtime path substitution — applicable to any future plan that needs to pin byte-identical determinism for an existing .flow script without baking baseline binaries into the repo"
  - "Dormant-but-wired field — FRAC-02 wires DurationFraction storage + GetBeats consumer, but no production-code path sets the field non-null. Activation deferred to Phase 19 lexer/parser. Pattern reusable for any cross-phase foundation that ships type+method but defers syntax."

requirements-completed: [FRAC-02]

# Metrics
duration: ~4min
completed: 2026-04-26
---

# Phase 18 Plan 02: Foundation — Rational Duration Arithmetic — MusicalNoteData Wiring + Byte-Identical Gate Summary

**Wired `Fraction? DurationFraction` into `MusicalNoteData` via the defaulted-parameter pattern (zero call-site edits across 30+ existing `new MusicalNoteData(...)` sites) + GetBeats branch on `DurationFraction.HasValue` + 6 unit Facts pinning ctor wiring/branch shape/ToString unchanged + 4 integration Facts pinning two-runner byte-identical determinism contract for `examples/tutorial.flow` + `examples/showcase.flow` (WAV + MIDI). DurationFraction stays DORMANT per D-USER-04 — Phase 18 closes with the foundation ready and the regression gate green.**

## Performance

- **Duration:** ~4 min (file authorship + build/test/grep/commit pipeline)
- **Started:** 2026-04-26T16:46:12Z
- **Completed:** 2026-04-26T16:49:58Z
- **Tasks:** 2 (Task 1 NoteType.cs edit + 6 unit Facts; Task 2 4 integration Facts)
- **Files created:** 3 (MusicalNoteDataTests.cs 68 lines + ByteIdenticalTutorialTests.cs 105 lines + ByteIdenticalShowcaseTests.cs 87 lines = 260 LOC)
- **Files modified:** 1 (NoteType.cs +27/-1 lines)

## Accomplishments

### NoteType.cs surgical edits (3 touchpoints, +27/-1 net)

1. **Property declaration** — Inserted `public FlowLang.TypeSystem.Fraction? DurationFraction { get; }` after `SourceLength` property with xmldoc explaining quarter-note units, dormancy contract (D-USER-04), and Phase 19 activation plan.

2. **Defaulted ctor parameter** — Appended `, FlowLang.TypeSystem.Fraction? durationFraction = null` at END of 12-param ctor signature. FQN reference avoids `using FlowLang.TypeSystem;` addition (minimal blast radius). Pitfall 2 (positional-arg shifting) avoided structurally — defaulted nullable parameter at END means no call site needs to provide it.

3. **GetBeats branch** — Added `if (DurationFraction.HasValue) { ... return (double)f.Num * timeSigDenominator / (f.Denom * 4.0); }` at TOP of method. Existing power-of-2 path (`if (!DurationValue.HasValue) return 1.0; ...; if (IsDotted) fraction *= 1.5; return fraction * timeSigDenominator;`) preserved VERBATIM when DurationFraction is null.

**ToString deliberately UNCHANGED** (Pitfall 5 mitigation) — 54 `tests/test_*.flow` scripts emit `(str note)` sentinels that depend on existing `quarter(C4)` format. Test 6 `ToString_UnchangedFromPreFraction` pins this by constructing a MusicalNoteData WITH non-null DurationFraction and asserting ToString still returns `"quarter(C4)"`.

### 30+ call sites compiled WITHOUT modification

Defaulted-parameter pattern verified structurally — `dotnet build flow-sharp.sln` returned 0 errors. Call sites span:
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (15 sites)
- `flow-lang/StandardLibrary/Composition/VariationFunctions.cs` (6 sites)
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` (1 site)
- `flow-lang/Runtime/ProgressionCompiler.cs` (1 site)
- `flow-lang/Runtime/NoteStreamCompiler.cs` (12+ sites)

ALL pass parameters positionally up to the existing 12-parameter signature; appending the 13th defaulted parameter required ZERO edits. Pitfall 2 avoided by structural design (append at end vs insert in middle).

### 6 unit Facts at flow-lang.Tests/Unit/Phase18/MusicalNoteDataTests.cs

1. `DurationFraction_DefaultsToNull` — `new MusicalNoteData('C', 4, 0, QUARTER, false).DurationFraction == null` (defaulted-param contract)
2. `DurationFraction_OptionalCtorParam_AcceptedAtEndOfSignature` — `new MusicalNoteData(..., durationFraction: new Fraction(1, 3)).DurationFraction == new Fraction(1, 3)` (named-arg wiring at end of signature)
3. `GetBeats_DurationFractionNull_UsesEnumPath` — quarter note in 4/4 returns 1.0 beats (existing power-of-2 path verbatim)
4. `GetBeats_DurationFractionNull_DottedQuarter_UsesEnumPath` — dotted quarter in 4/4 returns 1.5 beats (dotted multiplier preserved)
5. `GetBeats_DurationFractionSet_OverridesEnum` — `Fraction(1, 3)` quarter-note units in 4/4 returns 1/3 beats (rational override path; 10 decimal places of precision)
6. `ToString_UnchangedFromPreFraction` — non-null DurationFraction does NOT surface in ToString (Pitfall 5 mitigation pinned)

### 4 integration Facts at flow-lang.Tests/Integration/Phase18/

**ByteIdenticalTutorialTests.cs (2 Facts):**
- `Tutorial_TwoRunsProduceIdenticalWav` — two FRESH FlowEngineRunner instances run examples/tutorial.flow with substituted output paths; SequenceEqual byte comparison
- `Tutorial_TwoRunsProduceIdenticalMidi` — same pattern for MIDI output

**ByteIdenticalShowcaseTests.cs (2 Facts):**
- `Showcase_TwoRunsProduceIdenticalWav` — same protocol for examples/showcase.flow
- `Showcase_TwoRunsProduceIdenticalMidi` — same protocol for showcase MIDI

Path substitution mechanism: read source from disk, `source.Replace("examples/output/flow_{tutorial,showcase}.{wav,mid}", "tests/output/phase18_{tutorial,showcase}_run{1,2}.{ext}")`, run each substituted source in its own runner, SequenceEqual the resulting bytes. `Assert.NotEqual(source, sourceRun1)` guards against silent false-pass if substitution misses.

`[Collection("FlowScripts")]` serializes script execution to avoid output-path races (mirrors EuclideanByteIdenticalTests precedent).

### Phase 18 cumulative count: 19/19 GREEN

- Plan 18-01: 9 FractionTests Facts (Fraction primitive)
- Plan 18-02 unit: 6 MusicalNoteDataTests Facts (ctor wiring + branch shape + ToString stability)
- Plan 18-02 integration: 4 byte-identical Facts (tutorial.flow + showcase.flow × WAV + MIDI)

**Full suite:** 306/306 passed (287 pre-Phase-18 baseline + 9 from 18-01 + 10 from 18-02 = 306).

**Zero pre-existing Fact regressed.** Defaulted-parameter pattern's structural guarantee held — every existing test that depends on MusicalNoteData behavior passed unchanged because the new field is null in every production-code path.

## Task Commits

Per the plan's atomic-commit directive (Phase 12-02 6e5a960 + Phase 15-05 + Phase 18-01 2092f32 precedent — RED+GREEN bisectable but HEAD never broken):

1. **Task 1 + Task 2 (atomic):** `ba8534a` — `feat(18-02): FRAC-02 wire DurationFraction into MusicalNoteData (dormant)`
   - `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` (modified, +27/-1 lines)
   - `flow-lang.Tests/Unit/Phase18/MusicalNoteDataTests.cs` (NEW, 68 lines)
   - `flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs` (NEW, 105 lines)
   - `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` (NEW, 87 lines)

_Note: Task 1's RED state was empirically confirmed via local `dotnet build` after editing NoteType.cs but before creating the test file — the GetBeats branch and property declaration compiled, and the 6 Facts went GREEN on first run after the test file was authored. Task 2's 4 integration Facts went GREEN on first run as well. No iteration needed._

**Plan metadata commit:** Will land alongside SUMMARY.md, STATE.md, ROADMAP.md, REQUIREMENTS.md updates as the docs(18-02) closure commit.

## Files Created/Modified

### Created

- **`flow-lang.Tests/Unit/Phase18/MusicalNoteDataTests.cs`** (68 lines) — 6 `[Fact]` xUnit tests pinning FRAC-02 ctor wiring + GetBeats branch shape + ToString stability. `using FlowLang.TypeSystem; using FlowLang.TypeSystem.SpecialTypes; using Xunit;` — namespace `FlowLang.Tests.Unit.Phase18`. Sibling of FractionTests.cs from Plan 18-01.

- **`flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs`** (105 lines) — 2 `[Fact]` two-runner byte-identical tests on examples/tutorial.flow. Shared `RunTwiceAndCompare(bool isMidi)` helper substitutes the writeWav/writeMidi target path per run, runs each substituted source in a FRESH FlowEngineRunner, SequenceEqual-compares the resulting bytes. `[Collection("FlowScripts")]` serializes against other script-running tests.

- **`flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs`** (87 lines) — 2 `[Fact]` companion tests on examples/showcase.flow. Same protocol; denser feature surface (euclidean + reverbTime + dynamics + per-section gain per Phase 16 Plan 04 SUMMARY) discriminates regressions tutorial.flow may not catch.

### Modified

- **`flow-lang/TypeSystem/SpecialTypes/NoteType.cs`** (+27/-1 lines) — 3 surgical edits inside the `MusicalNoteData` class:
  1. Insert `public FlowLang.TypeSystem.Fraction? DurationFraction { get; }` property + xmldoc after `SourceLength` (~10 lines incl. xmldoc)
  2. Append `, FlowLang.TypeSystem.Fraction? durationFraction = null` to ctor signature + `DurationFraction = durationFraction;` to ctor body (2 lines net)
  3. Add `if (DurationFraction.HasValue) { ... return (double)f.Num * timeSigDenominator / (f.Denom * 4.0); }` branch at TOP of GetBeats; existing power-of-2 path body kept VERBATIM (~14 lines incl. comments)

  ToString unchanged — Pitfall 5 mitigation. Existing `if (IsRest)` branch + `NoteValueType.Format` calls + `quarter(C4)` format string unmodified.

## Decisions Made

- **Defaulted-parameter pattern at END of ctor signature** — Appending `Fraction? durationFraction = null` rather than inserting in the middle (avoiding Pitfall 2 — positional-arg shifting). Structural guarantee that all 30+ positional call sites compile unmodified. Verified empirically via `dotnet build flow-sharp.sln` returning 0 errors.

- **FQN reference `FlowLang.TypeSystem.Fraction` over `using` statement** — Same-assembly access path, but `SpecialTypes` is a child of `TypeSystem`, so technically `using FlowLang.TypeSystem;` could simplify. Chose FQN to keep the namespace-statement-count of NoteType.cs at 1 (only the file-scoped namespace declaration). Minimal blast radius — touching 3 lines in a class definition rather than adding a 4th line at the top of the file. xmldoc references the full type name for clarity.

- **Branch on `HasValue` at TOP of GetBeats** — Rational path checked FIRST so the new branch is the most-recently-added code (clear in diff). Existing power-of-2 path runs verbatim when null. This branching shape is RESEARCH §3 Pattern 2.

- **Beats formula `(Num × timeSigDenominator) / (Denom × 4.0)` in quarter-note units** — music21 DurationTuple convention (RESEARCH §3 Pattern 2 commentary). Cast `(double)f.Num` first to avoid integer overflow in `Num × timeSigDenominator`. Phase 19 callers will feed Fraction values consistent with this convention. Pinned in Test 5 with 1/3 quarter in 4/4 → 1/3 beats (10 decimal places of precision).

- **GetBeats keeps `double` return signature (D-USER-01)** — 21 GetBeats consumers (BarRenderer:51,260, MidiExport:184,195,216,259, BarType:141,152,169, etc.) read double. Sibling `GetBeatsFraction` deferred to Phase 19 if TUP-05 bar-fit validator needs the Fraction-typed beats for exact-fit-vs-drift detection. Phase 18's blast radius minimized to 1 file modification.

- **ToString deliberately UNCHANGED** (Pitfall 5) — 54 `tests/test_*.flow` scripts emit `(str note)` sentinels that depend on existing format. DurationFraction does NOT surface in Phase 18. Phase 19 may update ToString once non-null DurationFraction values exist in the wild, but Phase 18 keeps it byte-identical. Test 6 explicitly pins this by constructing a MusicalNoteData WITH non-null DurationFraction and asserting ToString returns `"quarter(C4)"` (existing format).

- **Two-runner pattern over committed binary baseline (D-USER-02)** — RESEARCH Open Q 3 resolved by user constraint. No `.planning/phases/18-foundation-rational-duration-arithmetic/baseline/` directory exists or is created. The two-runner pattern asserts byte-identity across two FRESH FlowEngineRunner instances in the SAME test session — mirrors RESEARCH Open Q 3 fallback option (b) and Phase 15 EuclideanByteIdenticalTests verbatim. Tradeoff: cannot detect cross-version drift via the test suite alone (Phase 15 used empirical byte pinning for that), but Phase 18 doesn't change any audio/MIDI byte-producing code path, so cross-version drift detection is structurally provided by Phase 15's EuclideanByteIdenticalTests Fact already in the suite.

- **Path substitution via `String.Replace` + `Assert.NotEqual` fail-loud guard** — Rewrites the writeWav/writeMidi target path per run (`examples/output/flow_tutorial.wav` → `tests/output/phase18_tutorial_run1.wav`) so the two runs do not race on the same output file. `Assert.NotEqual(source, sourceRun1)` ensures the substitution actually replaced something — if a future tutorial.flow refactor changes the path form (e.g. to a variable-built path), the Fact fails loudly rather than silently false-passing on stale identical bytes. Both `examples/tutorial.flow:618-619` and `examples/showcase.flow:36-37` use string-literal paths today, so substitution works cleanly.

- **`[Collection("FlowScripts")]` serialization** — All 4 integration Facts run serialized against other script-running tests (FlowScriptTests, RepLAutoImportTests, EuclideanByteIdenticalTests, etc.). Avoids `Console.SetOut` races and output-path races. Mirrors precedent established in 10+ prior phase test files.

- **Atomic commit landing all 4 files** — RED+GREEN bisectable (each Task's tests would have failed in isolation before the implementation landed) but HEAD never carries a broken build. Phase 12-02 6e5a960 + Phase 15-05 + Phase 18-01 2092f32 precedent.

## Deviations from Plan

**None — plan executed exactly as written.**

The plan was unusually high-fidelity (post-RESEARCH §3 Pattern 1/2 ships canonical defaulted-parameter + GetBeats branch shapes verbatim; post-Plan-18-01 Fraction primitive shipped clean and ready to consume). The plan's `<action>` blocks were copy-pasteable and produced correct output on first run.

- **No Rule 1 (bug fix) deviations** — NoteType.cs compiled cleanly on first edit; all 6 unit Facts passed on first test run; both integration Facts passed on first test run.
- **No Rule 2 (missing critical) deviations** — All edge cases (ToString stability, defaulted-param at end, named-arg ctor, dotted-quarter dual-path) pre-specified.
- **No Rule 3 (blocking) deviations** — Phase18 directory created cleanly; FQN reference avoided `using` statement complexity; FlowEngineRunner API surface stable since Phase 12-01.
- **No Rule 4 (architectural) deviations** — Defaulted-parameter pattern + GetBeats branch decisions pre-locked in ARCHITECTURE.md §3 + RESEARCH §3.

**Total deviations:** 0
**Impact on plan:** None — pure adherence. Validates the RESEARCH-then-PLAN cascade for foundation-tier additive-migration work where prior research has already reduced ambiguity to zero.

## Issues Encountered

**None.** Build clean (0 errors after each Task's edit; only pre-existing warnings unrelated to FRAC-02). Phase18 Facts GREEN (19/19, 6s integration runtime). Full suite GREEN (306/306, 23s). No deletions in commit (`git diff --diff-filter=D --name-only HEAD~1 HEAD` empty). No silently-set DurationFraction non-null path discovered in production code (`grep -rn 'durationFraction:' flow-lang/ --include='*.cs'` returns 0 lines outside test files — D-USER-04 dormancy contract structurally guaranteed).

## Confirmation of Byte-Identity Contract

**Tutorial WAV + MIDI:** Two-runner Fact GREEN. Both runs produce non-empty bytes; `bytes1.SequenceEqual(bytes2)` returns true.

**Showcase WAV + MIDI:** Two-runner Fact GREEN. Both runs produce non-empty bytes; `bytes1.SequenceEqual(bytes2)` returns true.

**54 tests/test_*.flow scripts:** Pinned via existing FlowScriptTests.cs Theory harness (Phase 12-01 infrastructure) — automatic coverage. Full-suite count incremented from 296 (post-18-01) to 306 (post-18-02) — the 10 new Facts (6 unit + 4 integration) explain the entire delta. Zero pre-existing Theory row regressed.

**Cross-Phase Bridge:** Phase 15 EuclideanByteIdenticalTests Facts (`SameSeed_ByteIdenticalMidi`, `SameSeed_ByteIdenticalWav`) continue to pass with empirically-pinned velocity bytes `[122, 70, 108]`. This pin would break if any Phase 18 edit silently shifted velocity computation through the GetBeats path — the 0-byte-shift guarantee is therefore double-pinned (Phase 15 byte pin + Phase 18 two-runner contract).

## Confirmation of D-USER-04 Dormancy

**Production code paths setting DurationFraction:** 0 (verified via `grep -rn 'durationFraction:' flow-lang/ --include='*.cs'`).

**Test code paths setting DurationFraction:** 1 — `flow-lang.Tests/Unit/Phase18/MusicalNoteDataTests.cs` Tests 2, 5, 6 use named-arg `durationFraction: new Fraction(1, 3)` to exercise the rational override path and ToString stability. These are the SOLE non-null-DurationFraction call sites in the entire codebase.

**.flow source paths setting DurationFraction:** 0. Lexer/parser unchanged. No `{N:M ...}` bracket syntax (Phase 19 territory). No `C4/12` arbitrary-fractional duration syntax (Phase 19 TUP-04 territory).

**Conclusion:** DurationFraction is wired-but-unreached in Phase 18. Every `MusicalNoteData` constructed by production code has `DurationFraction == null`, falls through to the existing power-of-2 enum path in GetBeats, and produces byte-identical results to pre-Phase-18.

## User Setup Required

**None** — no external service configuration, no environment variables, no third-party API keys. Pure C# code addition with zero dependency surface change.

## Next Phase Readiness

### Phase 18 closure ready

Both plans complete:
- **Plan 18-01:** Fraction primitive shipped at `flow-lang/TypeSystem/Fraction.cs` (commit `2092f32`)
- **Plan 18-02:** MusicalNoteData wiring + byte-identical regression gate shipped (commit `ba8534a`)

19/19 Phase18 Facts GREEN. Full suite 306/306. Zero pre-existing test regressed. Foundation tier closes.

### Phase 19 forward-readiness

When Phase 19 (tuplets) starts, it can consume directly:
- `Fraction` arithmetic primitive (Plan 18-01) — sum, multiply, compare, value-equality
- `MusicalNoteData.DurationFraction` storage field (Plan 18-02) — nullable, defaulted, named-arg ctor
- `MusicalNoteData.GetBeats` rational branch (Plan 18-02) — already evaluates `(double)f.Num * timeSigDenominator / (f.Denom * 4.0)` when set

Phase 19 work then becomes:
1. Lexer changes for `{N:M ...}q` and `C4/12` syntax
2. AST node `TupletElement` (mirrors existing NoteStreamExpression pattern)
3. NoteStreamCompiler emission of non-null `DurationFraction` values in TupletElement-derived MusicalNoteData
4. MIDI tick auto-elevation up to TPQN cap 9600 per D-05
5. Bar-fit validator extension for tuplet sums (Pitfall 2) — may require sibling `GetBeatsFraction` returning `Fraction?` (D-USER-01 deferred decision)

All of these lean on the surface this plan + Plan 18-01 shipped.

## Self-Check: PASSED

- [x] `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` modified (verified via `git show ba8534a --stat`)
- [x] `flow-lang.Tests/Unit/Phase18/MusicalNoteDataTests.cs` exists (verified via `git show ba8534a --stat`)
- [x] `flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs` exists (verified via `git show ba8534a --stat`)
- [x] `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` exists (verified via `git show ba8534a --stat`)
- [x] Commit `ba8534a` exists in git log (verified via `git rev-parse --short HEAD`)
- [x] Build succeeded with 0 errors (verified via `dotnet build flow-sharp.sln` post-commit)
- [x] All 19 Phase18 Facts pass (verified via `dotnet test --filter "FullyQualifiedName~Phase18" --no-build` → 19/19 GREEN, 6s)
- [x] Full suite 306/306 GREEN (verified via `dotnet test flow-sharp.sln --no-build` → 306 passed, 0 failed, 0 skipped, 23s)
- [x] Property decl present: `grep -c 'Fraction? DurationFraction' flow-lang/TypeSystem/SpecialTypes/NoteType.cs` returns 1
- [x] Defaulted ctor param present: `grep -c 'FlowLang.TypeSystem.Fraction? durationFraction = null' flow-lang/TypeSystem/SpecialTypes/NoteType.cs` returns 1
- [x] Ctor body assignment present: `grep -c 'DurationFraction = durationFraction;' flow-lang/TypeSystem/SpecialTypes/NoteType.cs` returns 1
- [x] GetBeats branch present: `grep -c 'if (DurationFraction.HasValue)' flow-lang/TypeSystem/SpecialTypes/NoteType.cs` returns 1
- [x] Power-of-2 path preserved: `grep -c 'double fraction = NoteValueType.ToFraction'` and `grep -c 'if (IsDotted) fraction \\*= 1.5;'` both return 1
- [x] Production-code dormancy: `grep -rn 'durationFraction:' flow-lang/ --include='*.cs'` returns 0 lines (excluding Fraction.cs and tests)
- [x] No committed binary baseline: `ls .planning/phases/18-foundation-rational-duration-arithmetic/baseline/ 2>/dev/null` returns empty (D-USER-02)
- [x] No deletions in commit: `git diff --diff-filter=D --name-only HEAD~1 HEAD` returns empty
- [x] 6 unit Facts: `grep -c '\\[Fact\\]' flow-lang.Tests/Unit/Phase18/MusicalNoteDataTests.cs` returns 6
- [x] 2 + 2 = 4 integration Facts: `grep -c '\\[Fact\\]'` on each ByteIdentical*Tests.cs returns 2

---
*Phase: 18-foundation-rational-duration-arithmetic*
*Plan: 02 (FRAC-02 — MusicalNoteData wiring + byte-identical regression gate)*
*Completed: 2026-04-26*
