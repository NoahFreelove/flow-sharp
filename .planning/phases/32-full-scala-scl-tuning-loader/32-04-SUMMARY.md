---
phase: 32-full-scala-scl-tuning-loader
plan: 04
subsystem: tuning
tags: [scala, tuning, scl, kbm, builtin, microtonal, non-octave, carlos-alpha, partch, d-08, warnonce]

# Dependency graph
requires:
  - phase: 32-02
    provides: ScalaParser + ScalaKbmParser + ParsedScala/ScalaKbm value classes (already merged into Wave 2 base)
  - phase: 32-03
    provides: ResolvedTuning + TuningType + 128-entry MIDI→Hz table + Pattern-A NoteToFrequency Custom branch (already merged into Wave 2 base)
provides:
  - ScalaBuiltins.Register(InternalFunctionRegistry) — wires (loadScala String) 1-arg + (loadScala String, String) 2-arg + (str Tuning) overloads.
  - Value.Tuning(ResolvedTuning) factory — Flow Value wrapping with Type = TuningType.Instance, reference-identity equality.
  - Tuning type recognition in TypeParser (ParseType + TryParseSingularType) + Parser (IsTypeKeyword allowlist) so `Tuning t = (loadScala "...")` declarations parse.
  - std.flow proc declarations: loadScala(String) + loadScala(String, String) + str(Tuning) — surfaces builtin to user @std imports.
  - D-08 WarnOnce advisory firing path: FireUnmappedAdvisoryIfNeeded scans MidiToHz[FirstMidi..LastMidi] for zero entries; sentinel keyed by Description for once-per-description-per-process dedup.
affects: [32-05 (TuningStack — Plan 32-05 also modifies FlowEngine.cs; coordinate at lines 71-74 — see "FlowEngine.cs touched lines" section below), 32-06 (tuning context block), 32-07 (tutorial chapter consumes (loadScala) end-to-end)]

# Tech tracking
tech-stack:
  added: []  # no new NuGet packages — pure registration glue + tests
  patterns:
    - "Builtin registration via ScalaBuiltins.Register(InternalFunctionRegistry) — single Register entry point invoked at FlowEngine startup, mirrors Audio.EffectsFunctions.Register + Harmony.HarmonyFunctions.Register"
    - "Value.Tuning(ResolvedTuning) factory mirroring Sequence/Song/Chord composite-value pattern — reference identity, type stamped via TuningType.Instance"
    - "D-08 one-shot stderr advisory keyed by Description via RenderingDiagnostics.WarnOnce — reuses Phase 23 D-13's dedup pattern (cross-tuning isolation, same-tuning dedup)"
    - "TDD per task: explicit RED commit (failing test) → GREEN commit (impl) for Task 1; Tasks 2-3 are test-only and ship a single commit each"
    - "Synthetic .kbm/.scl content via inline string literals + Guid-named temp files — same pattern Phase 23 WriteMidiWarningFacts uses for /tmp scratch files"

key-files:
  created:
    - "flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs"
    - "flow-lang.Tests/Unit/Phase32/LoadScalaBuiltinFacts.cs"
    - "flow-lang.Tests/Unit/Phase32/NonOctavePitchFacts.cs"
    - "flow-lang.Tests/Unit/Phase32/UnmappedKeyAdvisoryFacts.cs"
  modified:
    - "flow-lang/Core/FlowEngine.cs"        # +2 lines (single Register call + comment)
    - "flow-lang/Runtime/Value.cs"          # +9 lines (Value.Tuning factory)
    - "flow-lang/Parsing/TypeParser.cs"     # +5 lines (ParseType + TryParseSingularType)
    - "flow-lang/Parsing/Parser.cs"         # +2 lines (IsTypeKeyword allowlist)
    - "flow-lang/std.flow"                  # +8 lines (str(Tuning) + 2 loadScala overloads)

key-decisions:
  - "FlowEngine.cs touch is MINIMAL SURGICAL — 2 lines (`ScalaBuiltins.Register(internalRegistry);` + comment) inserted between lines 72-74 (BuiltInFunctions.RegisterAllImplementations call and the context construction). Plan 32-05's stack refactor diff merges cleanly because the new line is isolated."
  - "Tuning type wired through ParseType + TryParseSingularType + IsTypeKeyword — the type system already exposed TuningType.Instance from Plan 32-03; this plan adds the parser identifier-recognition that was missing."
  - "Value.Tuning(ResolvedTuning) factory placed AFTER Value.Song(SongData song) in Value.cs to match the existing pattern of composite-reference factories adjacent to each other."
  - "D-08 WarnOnce sentinel keyed by Description — matches CONTEXT § Specifics 'once per (description) per process'. Plan-spec wording used: '[tuning] unmapped MIDI keys under '<description>' — rendered as rest'."
  - "ScalaKbmParser.Parse returns Period=0.0 (intentional per its XML-doc); the 2-arg loadScala builtin overlays parsed.PeriodCents onto the partial ScalaKbm via a fresh ScalaKbm ctor call. This is the same overlay Plan 32-02's SUMMARY documented as Plan 32-03's responsibility — implemented here in the 2-arg builtin path so the load-time KBM auto-adopts the .scl's period per D-07."
  - "Test fixture paths resolved at runtime via FindRepoRoot walking up from AppContext.BaseDirectory (same pattern as ScalaParserFacts/ScalaKbmParserFacts). The .flow source receives absolute paths, sidestepping cwd surprises when tests run from bin/Debug/net10.0/."

patterns-established:
  - "Pattern: external-format loader builtin — read the file via File.ReadAllText, route through the existing parser, wrap the resolved value object in a Value factory, register both single-arg (default companion) and two-arg (explicit companion) overloads. Template for future format loaders (.sf2, .sfz, custom DSL)."
  - "Pattern: SpecialType-registration triple — type system (TypeSystem/SpecialTypes/*.cs) + parser type recognition (TypeParser ParseType + TryParseSingularType + Parser IsTypeKeyword) + std.flow proc declarations + Value factory. Plan 32-03 shipped the type; Plan 32-04 wires the remaining three layers."
  - "Pattern: load-time D-08 advisory — scan the post-construction value for unmapped slots in the KBM's range, fire WarnOnce keyed by Description. Mirrors Phase 23 D-13 cross-tuning isolation."

requirements-completed: [SPEC-1, SPEC-4, SPEC-5]

# Metrics
duration: ~45min
completed: 2026-05-14
---

# Phase 32 Plan 04: (loadScala) Builtin Registration Summary

**Composer-facing `(loadScala "path.scl")` + `(loadScala "scl" "kbm")` builtins ship the SPEC-1 + SPEC-4 + SPEC-5 surface — `Tuning t = (loadScala …)` is now callable from `.flow` source. D-08 unmapped-MIDI-key advisory wired through `RenderingDiagnostics.WarnOnce` with per-description dedup. SPEC-5 (carlos_alpha non-octave acceptance) verified at unit + builtin layer with ±0.1¢ cents-precision battery.**

## Performance

- **Duration:** ~45 min (executor start to SUMMARY commit)
- **Started:** 2026-05-14
- **Completed:** 2026-05-14
- **Tasks:** 3 / 3
- **Files created:** 4 (1 source + 3 tests)
- **Files modified:** 5 (FlowEngine.cs, Value.cs, TypeParser.cs, Parser.cs, std.flow)
- **Test Facts added:** 13 (4 LoadScalaBuiltin + 5 NonOctavePitch + 4 UnmappedKeyAdvisory)
- **Phase 23 + Phase 32 regression sweep:** 145 / 145 GREEN
- **Pre-existing failures (out of scope):** 26 — all in Phase 28 PerSynthArticulationTests + RagtimeFixtureTests, unchanged by this plan (Pitfall 7 documented in Plan 32-02 SUMMARY).

## Accomplishments

### Task 1: ScalaBuiltins registration + Value wrapper + parser wiring (RED commit `1a65cdc` + GREEN commit `1f4002b`)

- **`ScalaBuiltins.cs`** ships three registered signatures:
  - `loadScala(String) → Tuning` — opens via `File.ReadAllText`, parses via `ScalaParser.Parse`, synthesizes the default linear KBM via `ScalaKbmParser.Default(parsedScl)` (period auto-adopts per D-07), constructs `ResolvedTuning`, fires D-08 advisory if any MIDI in [FirstMidi, LastMidi] has `MidiToHz[i] == 0.0`, returns `Value.Tuning(resolved)`.
  - `loadScala(String, String) → Tuning` — same path but parses the .kbm via `ScalaKbmParser.Parse(kbmContent, kbmPath)` and overlays `parsed.PeriodCents` onto the partial ScalaKbm so the resolved value still auto-adopts the .scl's period.
  - `str(Tuning) → String` — calls `resolved.ToString()` which produces the D-04 format `Tuning("<desc>", N steps, period XXX.XX¢)`.
- **`Value.Tuning(ResolvedTuning)` factory** — slots in right after `Value.Song`, mirrors the composite-reference factory pattern (Sequence/Song/Chord). Reference identity by default per CONTEXT D-* / Claude's Discretion.
- **`FlowEngine.cs` integration** — single-line `ScalaBuiltins.Register(internalRegistry);` between the existing BuiltInFunctions.RegisterAllImplementations call (line 72) and the context construction (line 76).
- **`TypeParser.cs`** — Tuning identifier recognized in both `ParseType` (line 207) and `TryParseSingularType` (line 322).
- **`Parser.cs`** — Tuning added to `IsTypeKeyword` allowlist (line 1245) so `Tuning t = ...` declarations parse.
- **`std.flow`** — `internal proc str (Tuning: value)` declaration + two `internal proc loadScala` overloads so user code can `use "@std"` to surface the builtin.
- **4 Facts in `LoadScalaBuiltinFacts.cs`**:
  - `LoadScala_OneArg_Partch43_ParsesAndReturnsTuning` — `(str t)` output contains the description + "43 steps" + "1200.00" period.
  - `LoadScala_OneArg_CarlosAlpha_ParsesAndReturnsTuning` — `(str t)` output contains "Wendy Carlos' Alpha" + "18 steps" + "1404.00" non-octave period.
  - `LoadScala_TwoArg_AppliesKbm` — synthetic .kbm with shifted middleNote=64 loads cleanly via the 2-arg overload; description still surfaces in stdout.
  - `LoadScala_NonexistentFile_RaisesError` — bad path raises a FileNotFoundException wrapped as a Flow error.

### Task 2: NonOctavePitchFacts — SPEC-5 + SPEC-4 + D-09 batteries (commit `c0cfa20`, tests only)

- **5 Facts in `NonOctavePitchFacts.cs`** (Pattern C — direct ResolvedTuning.MidiToHz inspection; no FFT, no audio render):
  - `CarlosAlpha_MidiAscending_FrequenciesMatchSpecValues_Within01Cents` — SPEC-5 acceptance: every MIDI 60..78 within ±0.1¢ of the self-referenced cents target computed from `parsed.StepCents` + `parsed.PeriodCents`.
  - `CarlosAlpha_PeriodWrap_IsNonOctave` — headline non-octave verification: period ratio = 2^(1404/1200) ≈ 2.2501 (the SPEC-5 + RESEARCH analytic value), NOT an octave; cents-precision verified within ±0.1¢.
  - `NegativeCents_ProducesDescendingPitch` — D-09 implemented: a synthetic ParsedScala with `StepCents=[-100.0]` produces `MidiToHz[61] < MidiToHz[60]` with 1e-6 cents precision on the -100¢ shift.
  - `Partch43_KnownRatios_ProduceExpectedHz` — 81/80, 33/32, 21/20, 4/3 ratios round-trip through cents math at 1e-9 precision (D-11 preservation verified end-to-end).
  - `LoadScala_TwoArg_KbmAltersPitchMapping_AtNonTonicMidi` — SPEC-4 verification: synthetic shifted-middleNote .kbm produces a >0.5 Hz delta at non-tonic MIDI 65 vs default KBM; both KBMs preserve `MidiToHz[69] ≈ 440.0` anchor invariant.

### Task 3: UnmappedKeyAdvisoryFacts — D-08 WarnOnce (commit `b041301`, tests only)

- **4 Facts in `UnmappedKeyAdvisoryFacts.cs`** (`[Collection("FlowScripts")]` + `RenderingDiagnostics.ResetForTesting` in ctor/Dispose):
  - `UnmappedKey_LoadsKbmWithX_FiresWarnOnce` — single load with synthetic .kbm (size=2, one `x` entry); stderr contains the advisory text with the description + "rendered as rest".
  - `UnmappedKey_LoadsTwice_FiresOnlyOnce` — same scl+kbm loaded twice; WarnOnce dedup keyed by description fires exactly once.
  - `UnmappedKey_TwoDifferentScls_FireTwoSeparateWarnings` — partch_43 + slendro both with unmapped kbm → two separate advisories per CONTEXT § Specifics "once per (description) per process".
  - `MappedKbm_NoUnmappedEntries_NoWarning` — fully-mapped kbm (size=0 linear) produces zero advisory output.

## Task Commits

| # | Phase  | Hash      | Type | Description                                                       |
|---|--------|-----------|------|-------------------------------------------------------------------|
| 1 | RED    | `1a65cdc` | test | failing LoadScalaBuiltinFacts (parser doesn't yet know `Tuning`)  |
| 2 | GREEN  | `1f4002b` | feat | ScalaBuiltins + Value.Tuning + TypeParser/Parser/std.flow wiring  |
| 3 | (test) | `c0cfa20` | test | NonOctavePitchFacts — 5 Facts                                     |
| 4 | (test) | `b041301` | test | UnmappedKeyAdvisoryFacts — 4 Facts                                |

_The orchestrator will add the metadata commit (this SUMMARY.md) after wave merge._

## FlowEngine.cs touched lines (for Plan 32-05 merge coordination)

**Plan 32-05 will also modify `flow-lang/Core/FlowEngine.cs`** (per parallel_execution warning). Plan 32-04 made a **minimal surgical touch** to avoid overlap:

- **Only modification:** 2 lines inserted between lines 72 and 76:
  ```csharp
  BuiltInFunctions.RegisterAllImplementations(internalRegistry, _audioManager);
  // Phase 32 Plan 32-04: register (loadScala) overloads + (str Tuning).
  ScalaBuiltins.Register(internalRegistry);

  _context = new RuntimeContext(_errorReporter, internalRegistry, _diagnosticOutput);
  ```
- **Files NOT touched in FlowEngine.cs:** the `ApplyTuningPragma` method (lines 148-157), the `Execute` pipeline (lines 92-139), and the disposal / static-cache wiring (lines 185-198). Plan 32-05's TuningStack refactor at `ApplyTuningPragma` should merge cleanly.

If Plan 32-05's diff conflicts with my 2-line insertion at lines 72-74, resolution is trivial: keep BOTH edits (the `ScalaBuiltins.Register` line is independent of `ApplyTuningPragma` changes).

## Files Created/Modified

### Created (source — `flow-lang/StandardLibrary/Audio/Tuning/`)
- `ScalaBuiltins.cs` — 121 lines; Register entry point + LoadScalaOneArg + LoadScalaTwoArg + StrTuning + FireUnmappedAdvisoryIfNeeded helpers.

### Created (tests — `flow-lang.Tests/Unit/Phase32/`)
- `LoadScalaBuiltinFacts.cs` — 119 lines; 4 end-to-end Facts via FlowEngineRunner.
- `NonOctavePitchFacts.cs` — 174 lines; 5 Pattern-C Facts at ResolvedTuning layer.
- `UnmappedKeyAdvisoryFacts.cs` — 183 lines; 4 D-08 advisory Facts via FlowEngineRunner.

### Modified (source)
- `flow-lang/Core/FlowEngine.cs` (+2 lines) — single ScalaBuiltins.Register call + comment.
- `flow-lang/Runtime/Value.cs` (+9 lines) — Value.Tuning(ResolvedTuning) factory.
- `flow-lang/Parsing/TypeParser.cs` (+5 lines) — Tuning recognition in ParseType + TryParseSingularType.
- `flow-lang/Parsing/Parser.cs` (+2 lines) — Tuning added to IsTypeKeyword allowlist.
- `flow-lang/std.flow` (+8 lines) — internal proc loadScala overloads + str(Tuning).

## Decisions Made

See `key-decisions` in the frontmatter for the full list. The most important call-outs:

- **FlowEngine.cs touch is minimal surgical** to keep Plan 32-05's merge clean.
- **2-arg loadScala overlays .scl PeriodCents onto the parsed .kbm** (the Plan 32-02 SUMMARY flagged this as a deferred wiring step; implemented here per D-07).
- **Test fixture paths resolved via FindRepoRoot at runtime** to dodge bin/Debug/net10.0 cwd surprises.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking Issue] Tuning identifier not recognized by parser**

- **Found during:** Task 1 GREEN — initial dotnet test run after wiring `ScalaBuiltins.Register` reported `"Unexpected token Assign '=' at <test>:2:10"` parsing `Tuning t = (loadScala ...)`.
- **Issue:** Plan 32-03 added `TuningType : FlowType` and registered the Instance, but did NOT wire the identifier `"Tuning"` into TypeParser's ParseType + TryParseSingularType + Parser's IsTypeKeyword allowlist. Without these wirings, the variable declaration `Tuning t = expr` doesn't parse — the parser doesn't recognize `Tuning` as a type identifier, so it falls through to the assignment path which then chokes on `=`.
- **Fix:** Added `Tuning` to three locations: TypeParser.ParseType (line 207), TypeParser.TryParseSingularType (line 322), Parser.IsTypeKeyword (line 1245). All three additions are single-line entries in existing allowlists.
- **Files modified:** flow-lang/Parsing/TypeParser.cs, flow-lang/Parsing/Parser.cs (both bundled into the Task 1 GREEN commit `1f4002b`).
- **Verification:** `Tuning t = (loadScala "...")` now parses; all 4 LoadScalaBuiltinFacts pass.
- **Committed in:** `1f4002b` (Task 1 GREEN).

**2. [Rule 3 — Blocking Issue] str(Tuning) overload + loadScala signature missing from std.flow**

- **Found during:** Task 1 GREEN — after wiring TypeParser/Parser, runs progressed past the parser stage but hit `"Function 'loadScala' not found"` from FlowEngine's runtime function-overload resolution. Functions are only callable from .flow source if they have an `internal proc` declaration in @std (or another loaded module).
- **Issue:** Plan 32-03 added the C# Value-side wiring; Plan 32-04 added the C# builtin registration. Neither declared the @std-level proc signatures that surface the builtin to user code. The InternalFunctionRegistry holds the C# impl, but the user-side function resolution still walks the StackFrame's _functions map, which is populated from .flow source declarations.
- **Fix:** Added `internal proc loadScala (String: sclPath)` + `internal proc loadScala (String: sclPath, String: kbmPath)` + `internal proc str (Tuning: value)` to std.flow.
- **Files modified:** flow-lang/std.flow (bundled into the Task 1 GREEN commit `1f4002b`).
- **Verification:** Tests advance past function resolution and exercise the actual loadScala path.
- **Committed in:** `1f4002b` (Task 1 GREEN).

**3. [Rule 1 — Bug] Plan's `<acceptance_criteria>` referenced an incorrect numeric value for carlos_alpha period wrap ratio**

- **Found during:** Task 2 — initial NonOctavePitchFacts.CarlosAlpha_PeriodWrap_IsTritone_NotOctave assertion `Assert.InRange(ratio, 3.10, 3.30)` failed with `ratio=2.2501`.
- **Issue:** The plan text + RESEARCH.md §"Worked example — carlos_alpha at MIDI 60..65" claim `MIDI 78 = step 18 = middleHz × 2^(1404/1200) = middleHz × ~3.2003`. That value `3.2003` is incorrect. `2^(1404/1200) = 2.2501`, not 3.2003 (the latter would be `2^(1850/1200)`). Carlos Alpha period spans ~1.17 octaves, not ~1.67 octaves.
- **Fix:** Renamed the Fact to `CarlosAlpha_PeriodWrap_IsNonOctave` (the headline claim stands: NOT an octave); corrected the analytic value to 2.2501; tightened the InRange to [2.20, 2.30]. The cents-error tolerance against `2^(parsed.PeriodCents/1200)` is preserved as the SPEC-5 primary assertion.
- **Files modified:** flow-lang.Tests/Unit/Phase32/NonOctavePitchFacts.cs (Task 2 commit `c0cfa20`).
- **Verification:** The Fact's PRIMARY assertion (cents-error <0.1¢ vs computed period ratio) is the SPEC-5 acceptance condition and remains correct; only the secondary "is approximately ~3.2003" sanity sentinel was wrong. The pre-existing TuningTypeFacts.ResolvedTuning_CarlosAlpha_NonOctaveWrap Fact (Plan 32-03) uses the SAME math correctly (it compares against `Math.Pow(2.0, 1404.0 / 1200.0)` directly, not the human-readable approximation), so the upstream math is sound; only the plan text's prose computation was mis-stated.
- **Committed in:** `c0cfa20` (Task 2).

---

**Total deviations:** 3 auto-fixed (3 Rule 3 / 1 blocking-issue, 1 missing-stdlib-declaration, 1 bug-in-plan-numeric-value)
**Impact on plan:** All three deviations were necessary to ship a working `(loadScala)` surface. None expanded scope beyond the plan's stated objective. The plan-text numeric correction (D3) does not impact SPEC-5 acceptance because the Fact uses the analytic period-ratio computation, not the human-readable approximation.

## Authentication Gates Encountered

None — Plan 32-04 is pure C# implementation + xUnit tests; no auth required.

## Pre-existing Failures (Out of Scope per Executor Rules)

The full `dotnet test` suite reports 26 failures, all unchanged from the Wave-1 baseline:
- 24 × `Phase28.PerSynthArticulationTests.PerSynthArticulation_NormalVsArticulated_FFTCosineDifferentiable` (every synth × articulation combo failing FFT cosine differentiability)
- 2 × `Phase28.RagtimeFixtureTests.Ragtime_*_RmsRegression` (RMS deviation exceeds ±0.5 dB)

Verified pre-existing — these failures appear in `dotnet test` runs BEFORE my changes (Plan 32-02 + 32-03 SUMMARYs both document the same 26 in their "Pre-existing Failures" sections). Pitfall 7 in `32-RESEARCH.md` calls out this baseline. No Plan 32-04 code change touches Phase 28 surface.

## Acceptance Verification

All `<acceptance_criteria>` items pass for all 3 tasks:

### Task 1 acceptance
- ✅ `flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs` contains `public static void Register(InternalFunctionRegistry registry)` AND `loadScala`
- ✅ `grep -n 'ScalaBuiltins.Register' flow-lang/Core/FlowEngine.cs` returns ≥ 1 match (1 match at line 74)
- ✅ `grep -n 'public static Value Tuning' flow-lang/Runtime/Value.cs` returns 1 match
- ✅ `dotnet test --filter "FullyQualifiedName~LoadScalaBuiltinFacts"` exits 0; 4 Facts pass (target ≥ 3)
- ✅ Phase 23 sub-suite regression sweep stays GREEN: 91/91 Phase 23 Facts pass

### Task 2 acceptance
- ✅ `dotnet test --filter "FullyQualifiedName~NonOctavePitchFacts"` exits 0; 5 Facts pass (target ≥ 5)
- ✅ SPEC-5 acceptance Fact `CarlosAlpha_MidiAscending_FrequenciesMatchSpecValues_Within01Cents` passes
- ✅ SPEC-5 verification Fact `CarlosAlpha_PeriodWrap_IsNonOctave` passes (period ratio = 2.2501, demonstrably NOT 2.0)
- ✅ Negative-cents Fact `NegativeCents_ProducesDescendingPitch` passes (D-09 verified)
- ✅ SPEC-4 frequency-comparison Fact `LoadScala_TwoArg_KbmAltersPitchMapping_AtNonTonicMidi` passes (.kbm demonstrably alters MidiToHz at non-tonic MIDI)

### Task 3 acceptance
- ✅ `dotnet test --filter "FullyQualifiedName~UnmappedKeyAdvisoryFacts"` exits 0; 4 Facts pass (target ≥ 4)
- ✅ `grep -n 'ResetForTesting' flow-lang.Tests/Unit/Phase32/UnmappedKeyAdvisoryFacts.cs` returns 2 matches (ctor + Dispose)
- ✅ D-08 advisory message format includes `[tuning]` AND `unmapped MIDI keys under` AND `rendered as rest` (asserted by Fact 1)

### Overall plan verification (`<verification>` block)
- ✅ `dotnet test --filter "FullyQualifiedName~LoadScalaBuiltinFacts"` GREEN
- ✅ `dotnet test --filter "FullyQualifiedName~NonOctavePitchFacts"` GREEN (SPEC-5 acceptance)
- ✅ `dotnet test --filter "FullyQualifiedName~UnmappedKeyAdvisoryFacts"` GREEN (D-08)
- ✅ Phase 23 sub-suite regression sweep 100% GREEN (91/91)
- ✅ Phase 32 sub-suite regression sweep 100% GREEN (54/54 — 21 from 32-03 + 20 from 32-02 + 13 from 32-04)
- ✅ Full-suite failure count = 26 = pre-existing Phase 28 baseline (≤ 62 plan upper bound met with margin)

## Threat Model Adherence

- **T-32-IO-01 (Information Disclosure, file open):** Accepted per plan's `<threat_model>` — `(loadScala)` opens any file the running user can read. Matches Flow's existing `writeWav` / `writeMidi` posture. No path sanitization added; composer responsibility per Flow's overall trust model. Documentation lives in the `ScalaBuiltins.cs` XML-doc comment ("File access: …").
- **T-32-IO-02 (DoS, huge file):** Mitigation (partial) inherited from Plan 32-02's bounded loops (MaxStepCount = 10000, MaxMappingEntries = 10000). A 1 GB malformed file would still consume RAM at the `ReadAllText` boundary; accepted per plan.
- **T-32-IO-03 (Symlink shenanigans):** Accepted — composer-owned files, no additional guard.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema changes at trust boundaries. The new `(loadScala)` builtin opens files via `File.ReadAllText` — a pattern already accepted at the same trust level by `writeWav` / `writeMidi`.

## Known Stubs

None. Plan 32-04 ships the complete surface specified in `<interfaces>`:
- `(loadScala String)` 1-arg overload — registered, callable, returns Tuning.
- `(loadScala String, String)` 2-arg overload — registered, callable, returns Tuning; the partial ScalaKbm's Period=0.0 placeholder is OVERLAID with parsed.PeriodCents inside the builtin, closing the cross-format wiring deferred to Plan 32-04 by Plan 32-02's SUMMARY.
- `(str Tuning)` — registered, returns the D-04 format string.
- D-08 WarnOnce advisory — wired, dedup verified, 4 Facts pinning the per-Description sentinel cardinality.

## TDD Gate Compliance

Plan 32-04 has `tdd="true"` on all 3 tasks. Gate sequence:

- **Task 1:** `1a65cdc` test (RED — 3 of 4 Facts fail per "Unexpected token =" parser error) → `1f4002b` feat (GREEN — all 4 Facts pass after parser/Value/std.flow/ScalaBuiltins wiring).
- **Task 2:** Test-only task (no new C# source needed; exercises 32-02 + 32-03 surface). The Facts ship in a single `c0cfa20` test commit. No RED gate marker because the runtime surface already existed at base — running the file as RED would just fail to compile, not produce a meaningful failing-test signal.
- **Task 3:** Test-only task (no new C# source — the WarnOnce wiring Task 1 added is the impl). Single `b041301` test commit. Same RED-gate rationale as Task 2.

The two test-only Tasks bypass the explicit RED commit because the runtime they test was shipped in earlier plans + Task 1. This matches the TDD principle (verify behavior via tests) without introducing artificial RED commits for tests that exercise pre-existing functionality.

## Self-Check: PASSED

All 4 claimed file paths created exist on disk:
- `flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs` — FOUND
- `flow-lang.Tests/Unit/Phase32/LoadScalaBuiltinFacts.cs` — FOUND
- `flow-lang.Tests/Unit/Phase32/NonOctavePitchFacts.cs` — FOUND
- `flow-lang.Tests/Unit/Phase32/UnmappedKeyAdvisoryFacts.cs` — FOUND

All 4 task commits exist in git log:
- `1a65cdc` (Task 1 RED) — FOUND
- `1f4002b` (Task 1 GREEN) — FOUND
- `c0cfa20` (Task 2) — FOUND
- `b041301` (Task 3) — FOUND

All 5 modified files match the manifest:
- `flow-lang/Core/FlowEngine.cs` (+2 lines, ScalaBuiltins.Register call) — VERIFIED via `git show 1f4002b -- flow-lang/Core/FlowEngine.cs`
- `flow-lang/Runtime/Value.cs` (+9 lines, Value.Tuning factory) — VERIFIED
- `flow-lang/Parsing/TypeParser.cs` (+5 lines, Tuning recognition) — VERIFIED
- `flow-lang/Parsing/Parser.cs` (+2 lines, IsTypeKeyword) — VERIFIED
- `flow-lang/std.flow` (+8 lines, proc declarations) — VERIFIED
