---
phase: 18-foundation-rational-duration-arithmetic
verified: 2026-04-26T13:08:00Z
status: passed
score: 4/4 success criteria verified
overrides_applied: 0
re_verification:
  previous_status: none
  previous_score: n/a
  gaps_closed: []
  gaps_remaining: []
  regressions: []
---

# Phase 18: Foundation — Rational Duration Arithmetic Verification Report

**Phase Goal:** Rational arithmetic primitive lands so all subsequent tuplet / fractional / delay-sync / quantize math is drift-free.
**Verified:** 2026-04-26T13:08:00Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria)

| #   | Truth                                                                                                                                                                                                  | Status     | Evidence                                                                                                                                                                                                                  |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | `Fraction(int Num, int Denom)` value type exists in `flow-lang/TypeSystem/`, normalizes via GCD on construction, supports `+ / × / == / <` without ever using `double` arithmetic (FRAC-01)            | ✓ VERIFIED | `flow-lang/TypeSystem/Fraction.cs:19` declares `public readonly record struct Fraction`; ctor at L24 normalizes via Euclidean GCD; operators at L41-50 are all int-only (`l.Num * r.Denom + r.Num * l.Denom`, no `double` cast); `==` is free from record struct. 9 unit Facts PASSED. |
| 2   | Unit Facts pin canonical examples: `1/3 + 1/3 + 1/3 == 1`, `2/4 == 1/2`, `3/12 == 1/4` (FRAC-01)                                                                                                       | ✓ VERIFIED | `FractionTests.cs:16` `TripletThirds_SumToOne` PASSED; `:23` `TwoFourths_NormalizeToOneHalf` PASSED; `:30` `ThreeTwelfths_NormalizeToOneFourth` PASSED. All three canonical examples literally encoded as `Assert.Equal`. |
| 3   | `MusicalNoteData` exposes optional `Fraction? DurationFraction` that overrides the existing `DurationValue` enum when set; null leaves the existing power-of-2 path unchanged (FRAC-02)                | ✓ VERIFIED | `NoteType.cs:244` declares `public FlowLang.TypeSystem.Fraction? DurationFraction { get; }`; ctor (L246) accepts defaulted param; `GetBeats` (L266) branches on `HasValue` at TOP returning rational result; existing `DurationValue` + `IsDotted * 1.5` path preserved verbatim at L282-287. 6 unit Facts PASSED. |
| 4   | All ~70 existing `.flow` test scripts continue to pass with byte-identical output; `tutorial.flow` + `showcase.flow` regression gate via `cmp` is clean (FRAC-02)                                       | ✓ VERIFIED | Full suite `dotnet test flow-sharp.sln` returned `Passed: 306, Failed: 0, Skipped: 0` (287 pre-Phase-18 + 19 new = 306). FlowScriptData harness auto-enumerates 54 `tests/test_*.flow` scripts via `Directory.EnumerateFiles(testsRoot, "*.flow", SearchOption.AllDirectories)`. 4 two-runner integration Facts (`Tutorial_TwoRunsProduceIdentical{Wav,Midi}`, `Showcase_TwoRunsProduceIdentical{Wav,Midi}`) PASSED — `bytes1.SequenceEqual(bytes2)` returned true for all four. |

**Score:** 4/4 ROADMAP success criteria verified.

### Required Artifacts

| Artifact                                                                          | Expected                                                                              | Status     | Details                                                                                                                                                                                |
| --------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `flow-lang/TypeSystem/Fraction.cs`                                                | `readonly record struct Fraction` with Num/Denom + GCD normalization + arithmetic ops | ✓ VERIFIED | 57 LOC, file-scoped namespace `FlowLang.TypeSystem`. Sibling of `ArrayType.cs`; NOT a `FlowType` subclass. Substantive — operators present, GCD verbatim copy of `PolyrhythmFunctions.cs:117` idiom. |
| `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` (modified)                        | `Fraction? DurationFraction` property + defaulted ctor param + `GetBeats` branch     | ✓ VERIFIED | +27/-1 lines. Property at L244, ctor param at L246, ctor body assignment at L260, `GetBeats` branch at L268 with rational formula `(double)f.Num * timeSigDenominator / (f.Denom * 4.0)` at L278. |
| `flow-lang.Tests/Unit/Phase18/FractionTests.cs`                                   | 9 unit Facts pinning FRAC-01 acceptance + edge cases                                  | ✓ VERIFIED | 9 `[Fact]` declarations confirmed via grep. Covers all 3 ROADMAP examples + multiplication + comparison + zero denom + sign normalization + ToString format + GetHashCode value-equality. All 9 PASSED. |
| `flow-lang.Tests/Unit/Phase18/MusicalNoteDataTests.cs`                            | 6 unit Facts pinning FRAC-02 ctor wiring + GetBeats branch + ToString stability       | ✓ VERIFIED | 6 `[Fact]` declarations confirmed via grep. Defaults-to-null, named-arg ctor, null-enum-path, null-dotted-path, fraction-set-overrides, ToString-unchanged. All 6 PASSED. |
| `flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs`               | 2 two-runner byte-identical Facts (WAV + MIDI)                                        | ✓ VERIFIED | 2 `[Fact]` confirmed; `[Collection("FlowScripts")]` serializes against other script tests. Path-substitution mechanism with `Assert.NotEqual(source, sourceRun1)` fail-loud guard. Both Facts PASSED. |
| `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs`               | 2 two-runner byte-identical Facts (WAV + MIDI)                                        | ✓ VERIFIED | 2 `[Fact]` confirmed; same protocol as tutorial. Both Facts PASSED. |
| `.planning/phases/18-.../baseline/` directory                                     | MUST NOT EXIST per D-USER-02                                                          | ✓ VERIFIED | `ls .planning/phases/18-foundation-rational-duration-arithmetic/baseline/` returned `No such file or directory`. No committed binary baseline exists. |

### Key Link Verification

| From                                          | To                                                  | Via                                                      | Status     | Details                                                                                                                                  |
| --------------------------------------------- | --------------------------------------------------- | -------------------------------------------------------- | ---------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `FractionTests.cs`                            | `Fraction.cs`                                       | `using FlowLang.TypeSystem;`                             | ✓ WIRED    | L2 `using FlowLang.TypeSystem;` — direct reference; 9 Facts compile and exercise the API surface (operators, ctor, ToString, GetHashCode). |
| `MusicalNoteDataTests.cs`                     | `Fraction.cs` + `NoteType.cs`                       | `using FlowLang.TypeSystem; using FlowLang.TypeSystem.SpecialTypes;` | ✓ WIRED    | L1-2 imports both namespaces; Facts construct `new Fraction(1, 3)` and pass via `durationFraction:` named arg.                              |
| `NoteType.cs::MusicalNoteData`                | `Fraction.cs::Fraction`                             | FQN `FlowLang.TypeSystem.Fraction` (same assembly)       | ✓ WIRED    | Property decl at L244 + ctor param at L246 + ctor body at L260 + GetBeats branch at L268-278 reference the type via FQN — same-assembly access, no `using` needed. |
| `ByteIdenticalTutorialTests.cs`               | `examples/tutorial.flow`                            | `File.ReadAllText` + path substitution → `RunSource`    | ✓ WIRED    | L50 `Path.Combine(repoRoot, "examples", "tutorial.flow")`; L63 `File.ReadAllText`; L70-71 substitute `examples/output/flow_tutorial.{ext}` → `tests/output/phase18_tutorial_run{1,2}.{ext}`; L84/91 invoke `RunSource`. Tutorial.flow:618-619 confirmed to literally call `(writeWav "examples/output/flow_tutorial.wav" ...)` and `(writeMidi "examples/output/flow_tutorial.mid" ...)`. |
| `ByteIdenticalShowcaseTests.cs`               | `examples/showcase.flow`                            | `File.ReadAllText` + path substitution → `RunSource`    | ✓ WIRED    | Same protocol; showcase.flow:36-37 confirmed to literally call `(writeWav "examples/output/flow_showcase.wav" ...)` and `(writeMidi "examples/output/flow_showcase.mid" ...)`. |

### Data-Flow Trace (Level 4)

`MusicalNoteData.DurationFraction` is a **deliberately dormant** field per D-USER-04 — the data-flow trace verifies that NO production code path produces a non-null value.

| Artifact                                      | Data Variable          | Source                                  | Produces Real Data | Status        |
| --------------------------------------------- | ---------------------- | --------------------------------------- | ------------------ | ------------- |
| `MusicalNoteData.DurationFraction`            | `durationFraction` ctor param | All 30+ production call sites omit param (default null) | No (intentional)   | ✓ DORMANT_BY_DESIGN |
| `MusicalNoteData.GetBeats` rational branch    | `DurationFraction.Value` | Only `MusicalNoteDataTests.cs:GetBeats_DurationFractionSet_OverridesEnum` exercises this | No (intentional)   | ✓ DORMANT_BY_DESIGN |

**Empirical dormancy check (Step 7 follow-up):**

```bash
grep -rn "DurationFraction\|new Fraction(" flow-lang/ --include="*.cs"
```

Result: 7 lines, ALL within either `flow-lang/TypeSystem/Fraction.cs` (1 doc-comment mention) or `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` (the wiring itself). Zero production callers construct `new Fraction(...)` or pass `durationFraction:` — confirming D-USER-04 ("Phase 18 dormant — nothing existing should change") is structurally upheld.

### Behavioral Spot-Checks

| Behavior                                                                  | Command                                                                                                                                              | Result                                                                       | Status   |
| ------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------- | -------- |
| All 19 Phase 18 Facts pass                                                | `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase18"`                                                                                   | `Passed: 19, Failed: 0, Skipped: 0, Total: 19, Duration: 6 s`                | ✓ PASS   |
| Full suite green (no regression of pre-Phase-18 tests)                    | `dotnet test flow-sharp.sln --no-build`                                                                                                              | `Passed: 306, Failed: 0, Skipped: 0, Total: 306, Duration: 23 s`             | ✓ PASS   |
| Production code dormancy (D-USER-04)                                      | `grep -rn "DurationFraction\|new Fraction(" flow-lang/ --include="*.cs" \| grep -v "TypeSystem/Fraction.cs\|TypeSystem/SpecialTypes/NoteType.cs"` | (zero lines)                                                                 | ✓ PASS   |
| No committed binary baseline (D-USER-02)                                  | `ls .planning/phases/18-foundation-rational-duration-arithmetic/baseline/`                                                                            | `No such file or directory`                                                  | ✓ PASS   |
| Tutorial.flow output paths match integration-test substitution            | `grep -n "writeWav\|writeMidi" examples/tutorial.flow`                                                                                                | L618 `writeWav "examples/output/flow_tutorial.wav"`, L619 `writeMidi "examples/output/flow_tutorial.mid"` | ✓ PASS   |
| Showcase.flow output paths match integration-test substitution            | `grep -n "writeWav\|writeMidi" examples/showcase.flow`                                                                                                | L36 `writeWav "examples/output/flow_showcase.wav"`, L37 `writeMidi "examples/output/flow_showcase.mid"` | ✓ PASS   |
| Both Phase 18 commits exist in git history (REQUIREMENTS.md traceability) | `git log --oneline 2092f32 ba8534a -2`                                                                                                               | Both commits found: `2092f32 feat(18-01): FRAC-01 ...`, `ba8534a feat(18-02): FRAC-02 ...` | ✓ PASS   |

### Requirements Coverage

| Requirement | Source Plan                       | Description                                                                                                                                                                                              | Status       | Evidence                                                                                                                          |
| ----------- | --------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------ | --------------------------------------------------------------------------------------------------------------------------------- |
| FRAC-01     | 18-01-PLAN.md                     | New `Fraction(int Num, int Denom)` value type with GCD normalization + `+ * == <` operators + canonical-example unit Facts                                                                              | ✓ SATISFIED  | `flow-lang/TypeSystem/Fraction.cs` shipped; 9 unit Facts in `FractionTests.cs` PASSED; commit `2092f32`. REQUIREMENTS.md marks `[x]`. |
| FRAC-02     | 18-02-PLAN.md                     | `MusicalNoteData.DurationFraction` optional override field; existing power-of-2 path preserved when null; ~70 existing `.flow` scripts byte-identical; tutorial+showcase regression gate clean         | ✓ SATISFIED  | `NoteType.cs` modified with property + defaulted ctor + GetBeats branch; 6 unit + 4 integration Facts PASSED; full suite 306/306; commit `ba8534a`. REQUIREMENTS.md marks `[x]`. |

**Orphaned requirements check:** `grep -E "Phase 18" .planning/REQUIREMENTS.md` shows only FRAC-01 and FRAC-02 mapped to Phase 18. Both are claimed by plans (Plan 18-01 and Plan 18-02 respectively) and have shipped commits. No orphans.

### Anti-Patterns Found

Files modified in Phase 18:

- `flow-lang/TypeSystem/Fraction.cs` (created, 57 LOC)
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` (modified, +27/-1 LOC)
- `flow-lang.Tests/Unit/Phase18/FractionTests.cs` (created, 75 LOC)
- `flow-lang.Tests/Unit/Phase18/MusicalNoteDataTests.cs` (created, 68 LOC)
- `flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs` (created, 105 LOC)
- `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` (created, 87 LOC)

| File                                              | Line | Pattern                                                | Severity | Impact                                                                                                                            |
| ------------------------------------------------- | ---- | ------------------------------------------------------ | -------- | --------------------------------------------------------------------------------------------------------------------------------- |
| (none — all anti-pattern greps clean)             | -    | -                                                      | -        | No `TODO`, `FIXME`, `XXX`, `HACK`, `placeholder`, `coming soon`, or `not yet implemented` markers in any of the 6 Phase 18 files. |

The dormant-field xmldoc comments in `Fraction.cs` and `NoteType.cs` reference Phase 19 ("Phase 19 tuplet duration math", "Phase 19 lexer/parser feed it") as **future-coupling documentation**, not stub markers. These are intentional cross-phase pointers identifying when the wired-but-unreached branch will be activated, not unfinished implementation.

### Locked Decisions Verification (D-USER-01..04)

| Decision     | Description                                                              | Status     | Evidence                                                                                                                                                          |
| ------------ | ------------------------------------------------------------------------ | ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| D-USER-01    | Existing `GetBeats` signature unchanged (sibling `GetBeatsFraction` deferred to Phase 19) | ✓ VERIFIED | `NoteType.cs:266` `public double GetBeats(int timeSigDenominator)` — return type still `double`; no sibling method exists.                                          |
| D-USER-02    | No committed binary baseline; two-runner xUnit Fact pattern              | ✓ VERIFIED | `.planning/phases/18-.../baseline/` directory absent. Both `ByteIdentical*Tests.cs` files use `using var runner1 = new FlowEngineRunner()` + `runner2` + `bytes1.SequenceEqual(bytes2)` pattern. Mirrors Phase 15 `EuclideanByteIdenticalTests.cs` precedent. |
| D-USER-03    | `Fraction.ToString` always emits `Num/Denom` (no special-casing)         | ✓ VERIFIED | `Fraction.cs:56` `public override string ToString() => $"{Num}/{Denom}";` — no conditional formatting; `ToString_FormatNumSlashDenom` Fact at `FractionTests.cs:62` pins `1/1` literally. |
| D-USER-04    | Phase 18 dormant — nothing existing should change                        | ✓ VERIFIED | (1) Production-code dormancy grep returns zero lines outside the wiring sites. (2) `MusicalNoteData.ToString` unchanged (Pitfall 5 — `ToString_UnchangedFromPreFraction` Fact pins it). (3) GetBeats power-of-2 path preserved verbatim when `DurationFraction` is null. (4) Full suite 306/306 with zero pre-existing Fact regressed. |

### Human Verification Required

None. Phase 18's success criteria are entirely programmatically verifiable:

- **SC1, SC2** (Fraction primitive + canonical examples): exercised by 9 unit Facts.
- **SC3** (MusicalNoteData wiring + null preserves existing path): exercised by 6 unit Facts.
- **SC4** (~70 .flow scripts byte-identical + tutorial/showcase regression gate): exercised by FlowScriptTests Theory harness (54 scripts auto-enumerated) + 4 two-runner integration Facts. Byte-identity is the definition of "no behavioral change," and the two-runner pattern verifies it deterministically.

This is a foundation-tier dormant-wiring phase with no user-visible behavior to inspect.

### Gaps Summary

No gaps. Phase 18 successfully ships the rational-arithmetic foundation (`Fraction` primitive + `MusicalNoteData.DurationFraction` storage + `GetBeats` rational branch) while preserving the v1.2 byte-identical determinism contract via dormant-by-design wiring. All 4 ROADMAP success criteria, both requirements (FRAC-01, FRAC-02), and all 4 locked user decisions (D-USER-01..04) verified empirically:

- **19/19 Phase 18 Facts PASSED** in 6 s (9 Fraction unit + 6 MusicalNoteData unit + 4 byte-identical integration).
- **306/306 full suite PASSED** in 23 s — zero pre-existing test regressed (287 pre-Phase-18 baseline + 19 new = 306).
- **Production-code dormancy structurally guaranteed** — `grep` confirms no production caller constructs `new Fraction(...)` or sets `durationFraction:`.
- **No committed binary baseline** — D-USER-02 honored; two-runner pattern is the contract.
- **Tutorial + showcase byte-identical** across two FRESH `FlowEngineRunner` runs for both WAV and MIDI output.
- **Both commits in git history** — `2092f32` (FRAC-01) and `ba8534a` (FRAC-02) match REQUIREMENTS.md traceability claims.

The foundation is ready for Phase 19's tuplet `{N:M ...}` and arbitrary-fractional `C4/12` syntax to feed non-null `DurationFraction` values via lexer/parser changes.

---

_Verified: 2026-04-26T13:08:00Z_
_Verifier: Claude (gsd-verifier)_
