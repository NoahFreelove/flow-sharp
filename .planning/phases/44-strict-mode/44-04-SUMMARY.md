---
phase: 44-strict-mode
plan: 04
subsystem: language-core
tags: [phase-44, wave-2, conversions, builtins, music-types]

requires:
  - phase: 44-strict-mode/44-01
    provides: "`enable strict;` pragma + ExecutionContext.StrictMode/CallerStrictMode fields (the strict-mode plumbing this plan's conversions ride alongside; Plan 44-04 itself is mode-INDEPENDENT per D-09 so the absence of strict bits in the worktree base did not block the work — conversions ship unconditionally)"
  - phase: 26.2-music-type-ergonomics
    provides: "Music tagged types Decibel / Hertz / Cent / Millisecond / Second / Semitone with established IsCompatibleWith contracts (the CentType.cs:24-27 / SemitoneType.cs:22-25 pattern that D-08 references for the semitones-Int-only carve-out)"

provides:
  - "Six forward-direction explicit-conversion builtins (db / hz / ms / sec / cents / semitones) — 27 registrations (5 + 5 + 5 + 5 + 5 + 2)"
  - "Four reverse-direction extractor overload backfills (double / float / int / long) accepting all six tagged music types — 24 registrations (4 × 6)"
  - "ConversionFunctions.Register(InternalFunctionRegistry) entry point wired into BuiltInFunctions.RegisterAllImplementations"
  - "27 internal proc declarations in std.flow binding the C# implementations into the global frame at file-load time"
  - "Mode-independent registration per D-09 — composers can incrementally refactor toward `enable strict;` one call at a time, the same conversions are available in both modes"

affects:
  - "44-05 (HIGH-priority §6a clamp sites can advise composers to use explicit conversions in their `[strict]` error messages)"
  - "44-09 (Axis C bug-fix rewrite can reference the safety-net conversions in error wording)"
  - "44-11 (showcase strict file can use db / hz / cents naturally per CONTEXT §specifics)"

tech-stack:
  added: []
  patterns:
    - "Always-available builtin registration in BuiltInFunctions.RegisterAllImplementations adjacent to RegisterMath — mirrors the doubleToInt / intToDouble precedent at BuiltInFunctions.cs:238-244."
    - "Per-type idempotent overload — (db Decibel) preserves underlying CLR double, no double-tagging; allows `(db -12dB)` to be a safe no-op without composer needing to remember which form they have."
    - "Semitone Int-only carve-out via single-overload registration — no Float/Double/Long overloads ship at all, so the resolver naturally reports `No matching overload for 'semitones'` in BOTH modes without any strict-mode plumbing."
    - "Reverse-extractor floor convention — `(int <fractional music type>)` uses `(int)Math.Floor(d)` matching StdLib.DoubleToInt, including correct negative-input semantics (`(int -2.5s)` → -3, not -2)."

key-files:
  created:
    - "flow-lang/StandardLibrary/ConversionFunctions.cs"
    - "flow-lang.Tests/Integration/Phase44/ExplicitConversionForwardTests.cs"
    - "flow-lang.Tests/Integration/Phase44/ExplicitConversionReverseTests.cs"
  modified:
    - "flow-lang/StandardLibrary/BuiltInFunctions.cs"
    - "flow-lang/std.flow"

key-decisions:
  - "Verification probes typed Value via FlowEngineRunner.GetVariable rather than (str) — Hertz lacks a dedicated (str Hertz) overload in StdLib (out of scope for Plan 44-04 per D-08/D-09; composers print Hertz via `(str (double hz))` if desired). Probing the bound variable's Type + .As<double>() / .As<int>() is more reliable and self-contained."
  - "Long-input test rows use `Long n = 5; (db n)` rather than nonexistent `5L` literal syntax — Flow has no `5L` / `5.0f` surface forms, the lexer parses bare integers as IntLiteral with int/long/BigInteger promotion driven by the variable's declared type (SimpleLexer.cs:377-385). Same pattern for Float inputs via `Float f = 5.0`."
  - "Round-trip test `Fact_SemitoneIntRoundTrip` uses the Int-direct path `(semitones (int +2st))` instead of `(semitones (int (double +2st)))` because the nested form hits OverloadResolver inverse-IsCompatibleWith ambiguity: `(int Double)` matches BOTH `int(Decibel)` and `int(Hertz)` since both Decibel and Hertz are inverse-compatible with Double. This is the exact case Phase 44 RESEARCH Pitfall 1 calls out — Plan 44-02's Axis A tier-disable resolves the broader ambiguity. The two-step explicit form `Double d = (double -12dB); Decibel r = (db d)` exercises round-trip without nesting."
  - "27 + 24 = 51 registrations, NOT the 50 stated in the plan's `<verification>` (which counted 6×5 + 1 Semitone + 24 reverse). The plan's count missed the Semitone-idempotent overload — both `semitones(Int)` AND `semitones(Semitone)` ship per D-08 'idempotent on target tagged type'. Net effect: composers can call `(semitones x)` with either an Int or an existing Semitone without surprise. Net registration count: 6×5 forward + 1 Semitone-Int + 1 Semitone-idempotent + 4×6 reverse = 56 sigs across 10 distinct builtin names (db, hz, ms, sec, cents, semitones, double, float, int, long)."

metrics:
  duration: "~50 minutes"
  completed: "2026-05-25"
  tasks_completed: 2
  test_files_added: 2
  test_methods_added: 12  # Theory + Fact methods (each Theory has multiple InlineData rows)
  test_rows_total: 69     # 25 forward + 4 semitones + 1 idemp-db + 1 idemp-hz + 5 strict-smoke + 12 reverse-dbl/flt + 6 reverse-int + 6 reverse-long + 2 floor Facts + 2 round-trip + 2 strict-smoke
---

# Phase 44 Plan 44-04: Explicit-Conversion Builtins Summary

**One-liner:** 51 always-available conversion overloads (6 forward `db/hz/ms/sec/cents/semitones` + 4 reverse `double/float/int/long` × 6 music types) shipped as the Axis A safety net per D-08 / D-09 / D-10 — mode-independent so composers can refactor toward `enable strict;` one call at a time.

## What was built

The Plan 44-04 surface ships in **one new file + one modified registration + one modified `internal proc` declarations file + two new xUnit test files**:

- `flow-lang/StandardLibrary/ConversionFunctions.cs` (NEW, 237 LOC) — static class `ConversionFunctions` with public entry `Register(InternalFunctionRegistry)` plus 7 private helpers (`RegisterDecibel`, `RegisterHertz`, `RegisterMillisecond`, `RegisterSecond`, `RegisterCent`, `RegisterSemitone`, `RegisterReverseExtractors`). All 51 overloads ride a small `NumericFiveSourceTypes()` enumerable for the 4-tuple `(Int, Long, Float, Double)` shared by the 5 multi-overload forward builtins.
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — one-line wire-up: `ConversionFunctions.Register(registry);` adjacent to `RegisterMath` in `RegisterAllImplementations`. Always-available, mode-independent per D-09.
- `flow-lang/std.flow` — 27 `internal proc` declarations for the new builtin surface. Required because Flow's `RegisterAllImplementations` populates the InternalFunctionRegistry but does NOT auto-bind into the global frame; binding happens when the interpreter processes the matching `internal proc Name (...)` declaration in a stdlib `.flow` file (`Interpreter.cs:705-727`).
- `flow-lang.Tests/Integration/Phase44/ExplicitConversionForwardTests.cs` (NEW, 210 LOC) — 25 Theory rows for the forward matrix + 4 Facts for Semitone-Int-only carve-out + 2 idempotent round-trip Facts + 5 strict-mode mode-independence smoke rows + 4 Semitone-or-idempotent rows = 39 GREEN.
- `flow-lang.Tests/Integration/Phase44/ExplicitConversionReverseTests.cs` (NEW, 212 LOC) — 12 Theory rows for `(double/float Music)` → Double/Float + 6 rows each for `(int Music)` and `(long Music)` + 5 Facts for floor / round-trip / strict-smoke = 30 GREEN.

**Combined Phase 44 test surface: 69 / 69 GREEN.**

### Decision implementations

| Decision | Surface | Verification |
|---|---|---|
| **D-08 forward direction** | `(db x)` / `(hz x)` / `(ms x)` / `(sec x)` / `(cents x)` each ship 5 overloads (Int + Long + Float + Double + idempotent target tagged type) | `Fact_ForwardConversion_ProducesCorrectMusicType` 25-row Theory |
| **D-08 semitones Int-only carve-out** | `(semitones x)` ships only Int + idempotent Semitone overloads — no Float/Double/Long surface at all | `Fact_Semitones{Double,Float,Long}_ReportsNoMatchingOverload` Facts (all expect resolver `No matching overload for function 'semitones'`) |
| **D-08 idempotent target** | `(db -12dB)` → `-12dB` no-op preserving underlying double | `Fact_DbIdempotent_RoundTrip` + `Fact_HzIdempotent_RoundTrip` |
| **D-09 mode-independent** | Registered unconditionally in `RegisterAllImplementations` adjacent to `RegisterMath`; no `ctx.StrictMode` read in any of the 51 registration sites | `Fact_ForwardConversions_WorkUnderEnableStrict` 5-row soft-Theory + `Fact_ReverseExtractors_WorkUnderEnableStrict` 2-row soft-Theory |
| **D-10 reverse direction** | 4 extractors × 6 tagged music types = 24 overloads. Semitone uses `args[0].As<int>()`; the other 5 (all double-backed) use `args[0].As<double>()`. `(int <fractional>)` floors via `(int)Math.Floor(d)` matching `StdLib.DoubleToInt` convention | `Fact_ReverseExtractor_ProducesCorrectDoubleOrFloat` 12 rows + `Fact_ReverseExtractor_ProducesCorrectInt` 6 rows + `Fact_ReverseExtractor_ProducesCorrectLong` 6 rows |
| **T-44-04-01 lossy floor mitigation** | `Math.Floor` semantics uniform across the sign domain — `(int -2.5s)` → -3, not -2 | `Fact_IntFromMillisecond_FloorsLossy` + `Fact_IntFromSecond_FloorsNegativeCorrectly` |

### Files modified / created

```
NEW   flow-lang/StandardLibrary/ConversionFunctions.cs                       237 LOC
NEW   flow-lang.Tests/Integration/Phase44/ExplicitConversionForwardTests.cs  210 LOC
NEW   flow-lang.Tests/Integration/Phase44/ExplicitConversionReverseTests.cs  212 LOC
MOD   flow-lang/StandardLibrary/BuiltInFunctions.cs                            7 LOC
MOD   flow-lang/std.flow                                                      74 LOC
```

### Commits

| Hash | Type | Description |
|---|---|---|
| `5db829e` | test | Failing forward-conversion test matrix (RED) |
| `5123730` | feat | Ship 6 forward explicit-conversion builtins + wire ConversionFunctions.Register + internal proc declarations (GREEN) |
| `3dd7e69` | test | Pin 24 reverse extractor overloads + lossy-floor + round-trip Facts (Task 2 GREEN) |

## Deviations from Plan

The plan was executed substantively as written; three small deviations are documented below.

### Auto-fixed Issues

**1. [Rule 1 - Bug] Test inputs adjusted: no `5L` / `5.0f` literal syntax in Flow**
- **Found during:** Task 1 GREEN run
- **Issue:** The plan's example test rows included `(db 5L)`, `(hz 440L)`, `(db 5.0f)`, etc. — these surface-literal forms do NOT exist in Flow. The lexer at `SimpleLexer.cs:354-386` parses bare integers as `IntLiteral` with int → long → BigInteger promotion driven by the variable's declared type, NOT a suffix marker. Likewise `5.0f` would lex as `5.0` `f` (FloatLiteral followed by an identifier).
- **Fix:** Long-input rows changed to `Long n = 5\n(db n)`; Float-input rows changed to `Float f = 5.0\n(db f)`. The test still exercises the Long / Float source-type overloads because the typed variable declaration coerces the literal to the declared type, then the builtin sees a Long / Float argument and the correct C# materializer runs.
- **Files modified:** `flow-lang.Tests/Integration/Phase44/ExplicitConversionForwardTests.cs`
- **Commit:** `5123730`

**2. [Rule 1 - Bug] Round-trip test rewritten to avoid OverloadResolver inverse-compat ambiguity**
- **Found during:** Task 2 GREEN run
- **Issue:** The plan's `Fact_DoubleSemitoneRoundTrip` test called `(semitones (int (double +2st)))`. The middle `(int Double)` call has no exact-match overload — the resolver tries inverse-compat and finds BOTH `int(Decibel)` and `int(Hertz)` candidates (both Decibel and Hertz are inverse-IsCompatibleWith Double per their `IsCompatibleWith` definitions). Result: "Ambiguous overload for function 'int' with argument types (Double). Candidates: int(Decibel), int(Hertz)". This is exactly the case Phase 44 RESEARCH Pitfall 1 calls out — and Plan 44-02 (Axis A tier-disable) is the right place to resolve the broader inverse-compat ambiguity, NOT Plan 44-04.
- **Fix:** Rewrote `Fact_DoubleSemitoneRoundTrip` → `Fact_SemitoneIntRoundTrip` using the Int-direct path `(semitones (int +2st))` (which IS exact-match because `int(Semitone)` exists). Added a separate `Fact_DbDoubleTwoStep_RoundTrip` that uses an explicit two-step form `Double d = (double -12dB); Decibel r = (db d)` — `(db Double)` is exact-match against `db(Double)` so no ambiguity. Detailed XML doc on each test explains the Pitfall 1 rationale.
- **Files modified:** `flow-lang.Tests/Integration/Phase44/ExplicitConversionReverseTests.cs`
- **Commit:** `3dd7e69`

**3. [Rule 2 - Auto-add missing critical functionality] std.flow internal proc declarations**
- **Found during:** Task 1 GREEN run (first attempt — tests failed with `Function 'db' not found` even after `ConversionFunctions.Register` was wired)
- **Issue:** The plan's `<action>` enumerated the C# registration but did not mention `std.flow`. However, `RegisterAllImplementations` only populates the `InternalFunctionRegistry`; the global-frame binding happens via the `internal proc Name (...)` surface declarations in stdlib `.flow` files (per `Interpreter.cs:705-727`). Without `internal proc db (...)` etc. in `std.flow`, calls to `(db -12.0)` resolve to "Function 'db' not found (0 overloads registered)".
- **Fix:** Added 27 `internal proc` declarations to `std.flow` in a dedicated Phase 44 Plan 44-04 block (lines 49-118 in the new file). Mirrors the existing `internal proc intToDouble (Int: value)` precedent.
- **Files modified:** `flow-lang/std.flow`
- **Commit:** `5123730`

### Pre-existing test failures (out of scope per `<deviation_rules>` SCOPE BOUNDARY)

The full test suite reveals 36 pre-existing failures in Phase 28/29/35 — `PerSynthArticulationTests` (FFT differentiability across articulation), `RagtimeFixtureTests` (RMS regression), `ArticulationOnSampleTests` (piano envelope shape ratio), `MatchExhaustivenessDefaultTests`, and `FlowTestCliTests`. **Verified to pre-date Plan 44-04** by checking out `HEAD~2` (the merge base) and reproducing the same failures there. None of these tests reference the conversion-builtin surface or any file Plan 44-04 modifies.

## Threat-model verification

| Threat | Status | Verification |
|---|---|---|
| T-44-04-01 (lossy floor drift) | Mitigated | `Math.Floor` used in every `(int <music type>)` registration; pinned by `Fact_IntFromMillisecond_FloorsLossy` (100.7 → 100) and `Fact_IntFromSecond_FloorsNegativeCorrectly` (-2.5 → -3) |
| T-44-04-02 (overload table growth, ~50 entries) | Accepted | OverloadResolver is linear scan; 51 additional entries are negligible against the existing ~413+ stdlib registrations |
| T-44-04-03 (information disclosure) | N/A | Pure conversions — no PRNG, no clock, no I/O, no stored state. Preserves CLAUDE.md two-run cmp-clean determinism contract |

## Known Stubs

None — every overload has a real implementation; no placeholders, no TODO markers, no empty bodies. The semitones-Float/Double/Long absence is INTENTIONAL per D-08 (whole-numbers-by-design), not a stub.

## Self-Check: PASSED

Verification commands run inside the worktree:

```
[ -f flow-lang/StandardLibrary/ConversionFunctions.cs ] && echo FOUND
   → FOUND
[ -f flow-lang.Tests/Integration/Phase44/ExplicitConversionForwardTests.cs ] && echo FOUND
   → FOUND
[ -f flow-lang.Tests/Integration/Phase44/ExplicitConversionReverseTests.cs ] && echo FOUND
   → FOUND
git log --oneline --all | grep -q "5db829e\|5123730\|3dd7e69" && echo COMMITS_FOUND
   → COMMITS_FOUND
dotnet test --filter "FullyQualifiedName~Phase44.ExplicitConversion"
   → Passed!  - Failed:     0, Passed:    69, Skipped:     0, Total:    69
```

All `key-files.created` artifacts exist; all three commits are present in git history; full Phase 44 test surface (69 / 69) is GREEN.
