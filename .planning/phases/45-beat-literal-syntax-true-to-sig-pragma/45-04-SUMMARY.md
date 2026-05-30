---
phase: 45-beat-literal-syntax-true-to-sig-pragma
plan: 04
subsystem: interpreter + composer-facing tests
tags: [phase-45, evaluator, beat-literal, multiplier, true-to-sig, wave-3]
requires: [45-02, 45-03]
provides:
  - ExpressionEvaluator.EvaluateBeatLiteral method + BeatLiteralExpression switch arm (D-10)
  - Eval-time multiplier formula `final = pragma_on ? raw × (4.0 / denom) : raw`
  - Dedicated str(Beat) overload (StdLib.StrBeat + std.flow surface decl + registration, D-14)
  - 13 multiplier-matrix Facts in BeatTrueToSigPragmaTests.cs (8 behaviors)
  - 3 composer-facing .flow smoke scripts (test_beat_literal / _pragma_off / _pragma_on)
affects:
  - flow-lang/Interpreter/ExpressionEvaluator.cs (+1 switch arm, +1 method ~27 lines)
  - flow-lang/StandardLibrary/StdLib.cs (+1 method StrBeat)
  - flow-lang/StandardLibrary/BuiltInFunctions.cs (+1 str(Beat) registration)
  - flow-lang/std.flow (+1 internal proc str (Beat: value))
  - flow-lang.Tests/Integration/Phase45/BeatTrueToSigPragmaTests.cs (+13 Facts + RunCapture helper)
  - flow-lang.Tests/Integration/Phase45/PragmaScannerHyphenTests.cs (stale Wave-1 assertion fixed)
  - flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs (stale CSV expectation fixed)
  - tests/test_beat_literal.flow (NEW)
  - tests/test_beat_pragma_off.flow (NEW)
  - tests/test_beat_pragma_on.flow (NEW)
tech-stack:
  added: []
  patterns:
    - "Own switch arm for music literals (SymbolLiteralExpression precedent) — eval-time-resolved, reads ExecutionContext + active MusicalContext"
    - "Dedicated str(T) overload to disambiguate a music type that IsCompatibleWith both Float AND Double (mirrors str(Cent)/str(Second)/str(Decibel))"
    - "FlowEngine.Execute + Console.SetOut stdout capture for end-to-end eval Facts (Phase 44 strict-mode test style)"
key-files:
  created:
    - tests/test_beat_literal.flow
    - tests/test_beat_pragma_off.flow
    - tests/test_beat_pragma_on.flow
  modified:
    - flow-lang/Interpreter/ExpressionEvaluator.cs
    - flow-lang/StandardLibrary/StdLib.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/std.flow
    - flow-lang.Tests/Integration/Phase45/BeatTrueToSigPragmaTests.cs
    - flow-lang.Tests/Integration/Phase45/PragmaScannerHyphenTests.cs
    - flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs
decisions:
  - "D-10 honored: BeatLiteralExpression switch arm + EvaluateBeatLiteral method live directly in ExpressionEvaluator (no helper class); multiplier formula is 2 lines"
  - "D-02 honored: eval-time TimeSig lookup via GetMusicalContext().TimeSignature?.Denominator ?? 4 — default 4/4 identity, divide-by-zero-proof (T-45-09)"
  - "D-14 honored: (str Beat) emits plain double (no 'b' suffix) — REQUIRED a dedicated overload because Beat IsCompatibleWith both Float and Double (ambiguous otherwise)"
  - "D-11 honored: two-track testing — 13 xUnit Facts + 3 composer .flow smoke scripts"
metrics:
  duration_minutes: 9
  tasks_completed: 2
  files_created: 3
  files_modified: 7
  tests_added: 13
  tests_pass_phase45: 48
  completed_date: "2026-05-30"
requirements:
  - REQ-BEAT-AST-04
  - REQ-BEAT-TEST-01
  - REQ-BEAT-TEST-02
  - REQ-BEAT-TEST-03
---

# Phase 45 Plan 04: EvaluateBeatLiteral Switch Arm + Multiplier Formula Summary

Wave 3 — closed the literal-form half of Phase 45 by adding the `ExpressionEvaluator.EvaluateBeatLiteral` switch arm + the eval-time multiplier formula `final = pragma_on ? raw × (4.0 / denom) : raw`. `Nb` literals are now end-to-end functional: lex (45-01) → parse (45-02) → pragma plumbing (45-03) → evaluate-with-multiplier (this plan). A dedicated `str(Beat)` overload was added (D-14, plain double) because `(str Beat)` was otherwise ambiguous between `str(Float)`/`str(Double)` — without it the plan's entire `(print (str b))` verification path (xUnit Facts AND smoke scripts) was unreachable. 13 multiplier-matrix Facts + 3 composer-facing `.flow` smoke scripts pin the full `(timesig × pragma)` matrix; the Phase 45 suite is at 48 GREEN with zero net regression.

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| 1 | EvaluateBeatLiteral switch arm + multiplier method + str(Beat) overload + 13 Facts | `8ec7145` |
| 2 | 3 composer-facing .flow smoke scripts | `d62c64d` |

## Key Changes

### `flow-lang/Interpreter/ExpressionEvaluator.cs`

- **Switch arm** inserted immediately after the `SymbolLiteralExpression` arm (line 46), before `LambdaExpression` (D-10 / REQ-BEAT-AST-04):
  ```csharp
  BeatLiteralExpression beatLit => EvaluateBeatLiteral(beatLit),
  ```
- **`EvaluateBeatLiteral` method** placed adjacent to `EvaluateSymbolLiteral`. Final form:
  ```csharp
  int denom = _context.GetMusicalContext().TimeSignature?.Denominator ?? 4;
  double multiplier = _context.BeatTrueToSig ? (4.0 / denom) : 1.0;
  return Value.Beat(beatLit.RawValue * multiplier);
  ```
  `grep -c "EvaluateBeatLiteral"` = 2 (arm + declaration); `grep -n "_context.BeatTrueToSig"` = exactly 1 line.

### `str(Beat)` overload — three coordinated sites (D-14, Rule 3 blocking fix)

`(str Beat)` was ambiguous: `BeatType.IsCompatibleWith` covers both `Double` and `Float` at equal +500 specificity, so the resolver reported `Ambiguous overload ... Candidates: str(Float), str(Double)` and BOTH the xUnit Facts and the smoke scripts (whose acceptance criteria require `(print (str b))` to emit "0.5"/"2") could not run. Builtin overload candidates come from the `internal proc` surface declarations in `std.flow` (NOT directly from the registry), so the fix required:
1. `flow-lang/StandardLibrary/StdLib.cs` — `StrBeat` emitting `$"{args[0].As<double>()}"` (plain double, no "b" suffix per D-14, to avoid the pragma round-trip hazard).
2. `flow-lang/StandardLibrary/BuiltInFunctions.cs` — `registry.Register("str", str(Beat) sig, StdLib.StrBeat)`.
3. `flow-lang/std.flow` — `internal proc str (Beat: value)` surface declaration (the frame-candidate source that the OverloadResolver actually sees).

The exact-Beat match scores +1000 vs the +500 Float/Double convertible tier, so the dedicated overload wins decisively.

### `flow-lang.Tests/Integration/Phase45/BeatTrueToSigPragmaTests.cs` (+13 Facts)

Added a `RunCapture(source)` helper (FlowEngine.Execute + `Console.SetOut` capture, finally-restored) and the multiplier matrix covering all 8 plan behaviors:

| Fact | Behavior |
|------|----------|
| `MultiplierFormula_PragmaOff_Identity` (Theory ×3) | 4/4, 6/8, 2/2 each `1b → "1"` pragma OFF |
| `MultiplierFormula_PragmaOn_4Over4` | `1b → "1"` (denom 4, identity) |
| `MultiplierFormula_PragmaOn_6Over8` (Theory ×3) | `1b→0.5`, `2b→1`, `0.5b→0.25` |
| `MultiplierFormula_PragmaOn_2Over2` (Theory ×2) | `1b→2`, `0.5b→1` |
| `MultiplierFormula_PragmaOn_5Over4` | `1b → "1"` (identity) |
| `MultiplierFormula_PragmaOn_7Over8` | `1b → "0.5"` |
| `MultiplierFormula_NegativePassthrough` | `-2b → "-2"` (D-08) |
| `MultiplierFormula_NoActiveTimesig` | pragma on, no timesig → `1b → "1"` (Pitfall 4 / D-02) |

### 3 `.flow` smoke scripts (Task 2)

| Script | Lines | Expected stdout |
|--------|-------|-----------------|
| `tests/test_beat_literal.flow` | ~28 | `0.5`, `2`, `1`, `-2`, `1`, `test_beat_literal: PASSED` |
| `tests/test_beat_pragma_off.flow` | ~33 | `1`, `1`, `1`, `test_beat_pragma_off: PASSED` (identity in 4/4/6/8/2/2) |
| `tests/test_beat_pragma_on.flow` | ~58 | `1`, `0.5`, `1`, `0.25`, `2`, `1`, `1`, `0.5`, `test_beat_pragma_on: PASSED` |

All exit 0. `pragma_on` stdout contains "0.5" (2×) and "2"; `pragma_off` stdout contains no "0.5" (identity proof). No `[error]`/`[warn]` stderr noise.

## Acceptance Criteria

| Criterion | Status |
|-----------|--------|
| All 8 multiplier behaviors PASS (13 Facts incl. Theory rows) | PASS |
| `grep -c "EvaluateBeatLiteral"` ≥ 2 | PASS (= 2) |
| `grep -n "_context.BeatTrueToSig"` = exactly 1 | PASS (line 1034) |
| Plan 45-01/02/03 Phase 45 Facts still GREEN | PASS (48/48 Phase45) |
| Zero regression to Phase44.Overload / Phase35.Match / Phase26.Tuple | PASS (35/35 combined) |
| 3 .flow smoke scripts exit 0 with PASSED markers | PASS |
| pragma_on stdout has "0.5"+"2"; pragma_off has no "0.5" | PASS |
| Phase 45 total ≥ 38 Facts GREEN | PASS (48) |

## Verification

```bash
dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase45" --no-build
#   Passed!  - Failed: 0, Passed: 48, Skipped: 0, Total: 48
dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase35.Match|FullyQualifiedName~Phase26.Tuple|FullyQualifiedName~Phase44.Overload|FullyQualifiedName~Phase44.ModuleLoaderStrict|FullyQualifiedName~Phase44.PragmaRegistry" --no-build
#   Passed!  - Failed: 0, Passed: 35, Skipped: 0, Total: 35
for t in tests/test_beat_*.flow; do dotnet run --project flow-interpreter "$t"; done   # all exit 0, all PASSED
```

## Deviations from Plan

### Auto-Fixed Issues

**1. [Rule 3 — Blocking] `(str Beat)` ambiguous between str(Float)/str(Double) — blocked the entire verification path**
- **Found during:** Task 1 empirical verification of the `(print (str b))` path the plan's Facts + smoke scripts depend on.
- **Issue:** `BeatType.IsCompatibleWith` covers both `Double` and `Float` at equal +500 specificity. With no dedicated `str(Beat)` overload, `(str someBeat)` resolved to `Ambiguous overload ... Candidates: str(Float), str(Double)` and errored — making EVERY plan acceptance criterion that prints a Beat (the 13 Facts AND all 3 smoke scripts) unreachable. CONTEXT D-14 explicitly specifies `(str Beat)` emits a plain double like "0.5", so the intended behavior was already locked; the overload was simply never registered (sibling music types Cent/Second/Decibel all HAVE dedicated `str` overloads).
- **Fix:** Added `StdLib.StrBeat` (plain double, no "b" suffix per D-14 round-trip rationale) + `BuiltInFunctions` registration + `std.flow` `internal proc str (Beat: value)` surface declaration (the frame-candidate source the OverloadResolver reads). Exact-Beat match scores +1000, winning over the +500 convertible tier.
- **Files modified:** `flow-lang/StandardLibrary/StdLib.cs`, `flow-lang/StandardLibrary/BuiltInFunctions.cs`, `flow-lang/std.flow` (commit `8ec7145`).
- **Scope note:** D-14 is a locked Phase 45 surface decision; this closes the gap the literal-syntax half requires. Within scope.

**2. [Rule 1 — Bug] Stale Wave-1 `PragmaScannerHyphenTests.Fact_..._AcceptsHyphenatedName_BeatTrueToSig` asserted the pragma was UNregistered**
- **Found during:** Task 1 Phase 45 regression run.
- **Issue:** This Wave-1 Fact asserted `reporter.HasErrors` with the message "not yet registered in PragmaRegistry — Wave 2/3 wires that". Plan 45-03 (Wave 2) DID register the pragma, so `enable beat-true-to-sig;` now scans cleanly with NO error — the assertion was already failing at the plan-spawn commit `cfabc17` (pre-existing, NOT introduced by this plan; Plan 45-03's verification filter missed it). The Fact's own docstring anticipated "Once Wave 2/3 registers the pragma, an additional Fact ... will be added."
- **Fix:** Updated the Fact to the post-registration reality — assert `!reporter.HasErrors` AND `pragmas.Has("beat-true-to-sig")`, which proves the scanner accepted the hyphens in continuation position (a truncated 'beat' prefix would fail registry lookup and error).
- **Files modified:** `flow-lang.Tests/Integration/Phase45/PragmaScannerHyphenTests.cs` (commit `8ec7145`).

**3. [Rule 1 — Bug] Stale `Phase21.PragmaRegistryFacts.AlphabetizedKnownNames_ReturnsCsvSorted` expected the pre-45-03 7-entry CSV**
- **Found during:** Task 1 broader str/dict regression run.
- **Issue:** Plan 45-03 added `beat-true-to-sig` to `KnownPragmas`. Under ordinal sort it lands FIRST ('b' < 'e'), so the alphabetized CSV grew from 7 → 8 entries. The Phase 21 Fact still hard-coded the 7-entry string — pre-existing failure from Wave 2, not this plan.
- **Fix:** Updated the expected CSV + comment to the 8-entry post-45-03 reality.
- **Files modified:** `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` (commit `8ec7145`).

### Plan-body adjustments (no code defect)

**4. Smoke-script `(add 0.5b 0.5b)` line dropped from `test_beat_literal.flow`.**
- The plan's Task 2 Step A listed `Double sum = (add 0.5b 0.5b)` expecting "1". `(add Beat Beat)` is ambiguous (`add(Float,Float)` vs `add(Double,Double)`) — the SAME Beat→Float/Double convertibility that affects `str`, but `add` is a PRE-EXISTING characteristic of `BeatType` not introduced by Phase 45 (Beat was convertible to both before this plan's literal syntax landed). The plan note itself hedged ("confirm by reading audio.flow / BeatType"). Fixing `add(Beat, Beat)` is out of scope (would require an `add(Beat, Beat)` overload or relaxing OverloadResolver — a v1.6 backlog item already noted in CLAUDE.md "sparse-named-arg call ergonomics / OverloadResolver relaxation"). The Dict-tuple-key line (also in Step A) is retained and exercises the same hashable-Beat-key acceptance shape.

**5. `tests/` is gitignored; smoke scripts force-added per precedent.**
- `.gitignore` broadly ignores `tests/` + `*.flow`, but 127 `tests/test_*.flow` files are already tracked (force-added by long-standing convention). Used `git add -f` to match.

**6. Dict tuple-key annotation uses `Dict<Tuple<<Note, Beat>>, Int>`** (the canonical form from `tests/test_dict_keys.flow:18`), not the plan's bare `Dict<<Note, Beat>, Int>` which is a parse error.

### Out-of-Scope Discoveries
None beyond the two stale Phase 45/21 test assertions above (both Wave-2-introduced, fixed as Rule 1 since they pin THIS phase's pragma behavior). Pre-existing uncommitted working-tree noise (`.planning/.continue-here.md`, `.planning/config.json`) was deliberately NOT staged into either task commit.

## Stub Tracking
None. The switch arm + method are fully wired and proven end-to-end. The `(beat N)` constructor still ignores the pragma (uses plain `Register`, not `RegisterContextDependent`) — that migration is the explicit Plan 45-05 boundary, documented in the plan `<objective>`, not a stub.

## Threat Flags
None. T-45-09 (multiplier NaN/Inf) is mitigated: `denom` comes from `TimeSignatureData.Denominator` (constrained to powers of 2 by the parser) and `?? 4` prevents divide-by-zero. Pure interpreter switch-dispatch; no new network/auth/file-access surface.

## Self-Check: PASSED

- File existence:
  - `flow-lang/Interpreter/ExpressionEvaluator.cs` — FOUND (modified, EvaluateBeatLiteral present)
  - `flow-lang/StandardLibrary/StdLib.cs` — FOUND (StrBeat present)
  - `flow-lang/std.flow` — FOUND (`internal proc str (Beat: value)` present)
  - `tests/test_beat_literal.flow` — FOUND (created, contains `0.5b`)
  - `tests/test_beat_pragma_off.flow` — FOUND (created, contains `1b`)
  - `tests/test_beat_pragma_on.flow` — FOUND (created, contains `enable beat-true-to-sig;`)
- Commit existence:
  - `8ec7145` (Task 1) — FOUND in git log
  - `d62c64d` (Task 2) — FOUND in git log
- Deliverables: switch arm + method (grep = 2) + str(Beat) overload + 13 Facts + 3 smoke scripts; 48/48 Phase45 GREEN; zero net regression.
