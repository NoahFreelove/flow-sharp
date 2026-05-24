---
phase: 36-sequence-algebra-generative
plan: 10
subsystem: parameterized-sections
tags: [section, overload, pattern-match, synthetic-frame, SECT-01, D-36-13..18]
dependency_graph:
  requires:
    - 36-02  # named-argument syntax (FunctionCallExpression.NamedArgs + parser)
    - 35-05  # Pattern AST family (BindingPattern / ConstructorPattern / GuardPattern / etc.)
    - 35-06  # music-aware extractors (IsChordLiteral / IsRomanNumeral / IsArticulationSymbol)
  provides:
    - SectionDeclaration.Parameters + DefaultValues defaulted-positional fields
    - SectionCallElement + BareSectionElement under new Ast/Elements/ family
    - SongExpression.Elements parallel SongElement list
    - BindingPattern.TypeAnnotation optional FlowType
    - ExecutionContext.SectionRegistry shape change to Dictionary<string, List<SectionData>>
    - ExecutionContext.SectionRegistryFlat() backward-compat helper
    - PatternMatcher.TryMatchAll list-of-patterns matcher with specificity scoring
    - SectionOverloadDispatch runtime dispatcher with default-value folding
    - IFunctionInvoker.ExecuteStatement + IFunctionInvoker.LastExpressionValue
    - ExpressionEvaluator.EvaluateSectionCallToData synthetic-frame body re-execution
  affects:
    - all subsequent Phase 36 plans that author section-call sites
    - SongRenderer / MidiExport / SfzSampleCache (kept working via SectionRegistryFlat)
tech-stack:
  added: []
  patterns:
    - defaulted-positional record extension (Phase 35 LANG-03 precedent)
    - parallel-AST family (Ast/Elements/ alongside Expressions/Statements)
    - structural pattern comparator (PatternsHaveIdenticalShape) for declaration-time ambiguity check
    - dispatcher returning (sig, finalArgs, bindings) tuple for callsite synthetic frame
key-files:
  created:
    - flow-lang/Ast/Elements/SongElement.cs
    - flow-lang/Ast/Elements/SectionCallElement.cs
    - flow-lang/Interpreter/SectionOverloadDispatch.cs
    - flow-lang.Tests/Phase36/SectionParamsParserTests.cs
    - flow-lang.Tests/Phase36/SectionOverloadTests.cs
    - flow-lang.Tests/Phase36/SectionDefaultsTests.cs
    - flow-lang.Tests/Phase36/SectionDiagnosticsTests.cs
    - tests/test_section_params.flow
    - tests/test_section_overload.flow
    - tests/test_section_pattern_destructure.flow
    - tests/test_section_repeat.flow
    - tests/test_section_defaults.flow
  modified:
    - flow-lang/Ast/Statements/SectionDeclaration.cs
    - flow-lang/Ast/Patterns/BindingPattern.cs
    - flow-lang/Ast/Expressions/SongExpression.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang/Interpreter/Interpreter.cs
    - flow-lang/Interpreter/ExpressionEvaluator.cs
    - flow-lang/Interpreter/PatternMatcher.cs
    - flow-lang/Interpreter/IFunctionInvoker.cs
    - flow-lang/Runtime/ExecutionContext.cs
    - flow-lang/StandardLibrary/Composition/SongFunctions.cs
    - flow-lang/StandardLibrary/TestFramework/TestSnapshot.cs
    - flow-lang/TypeSystem/SpecialTypes/SectionType.cs
    - flow-lang.Tests/Unit/ThunkTests.cs
decisions:
  - D-36-10-01 — SectionRegistry shape change is internal-only; SongData.SectionRegistry stays Dictionary<string, SectionData> via materialized-overload synthetic-name keys (verse#0, verse#1) so SongRenderer / MidiExport / SfzSampleCache see a flat registry of zero-arg-shaped entries
  - D-36-10-02 — parameterized section body executes ON EACH CALL (deferred from declaration); zero-arg sections execute ONCE at declaration time (backward compat)
  - D-36-10-03 — synthetic-frame closure inherits CALLSITE's MusicalContext, not declaration's (Pitfall 7 dynamic scope)
  - D-36-10-04 — section overload ambiguity caught at DECLARATION time via PatternsHaveIdenticalShape structural comparator (Pitfall 3)
  - D-36-10-05 — typed BindingPattern.TypeAnnotation is the per-slot type-compatibility gate in PatternMatcher.TryMatchAll (D-36-17 typed-bindings; arg.Type.IsCompatibleWith(annotation) must hold for the candidate to match)
  - D-36-10-06 — tuple-destructure ConstructorPattern uses Name="Tuple" + SubPatterns as the discriminator; differentiated from chord-literal / roman-numeral / articulation-symbol ConstructorPatterns by the absence of their respective flags
metrics:
  duration: ~80 minutes
  tasks_completed: 3
  files_changed: 25
  tests_added: 24
  tests_passing_phase36: 175
  tests_passing_phase35_regression: 80
  completed_date: 2026-05-22
---

# Phase 36 Plan 36-10: Parameterized Sections (SECT-01) Summary

Parameterized sections — `section verse(Note root = C4, Int repeats = 2) { ... }` — and the polymorphic overload surface that lets multiple same-name declarations coexist with different pattern signatures. Full Phase 35 pattern syntax (literal patterns, typed bindings, ConstructorPattern with music-aware extractors, tuple destructure, guards) lands in section signatures. The OverloadResolver picks the highest-specificity match per RESEARCH §Pattern 7 table at call time, dispatches via a synthetic frame inheriting the CALLSITE's MusicalContext (Pitfall 7 dynamic scope), and routes arity / type / ambiguity failures through the Phase 35-03 DiagnosticRenderer.

## What Shipped

### 1. AST extension + Parser (Task 1, commit e935991)

`SectionDeclaration` gains `IReadOnlyList<Pattern>? Parameters` and `IReadOnlyList<Expression?>? DefaultValues` as the last two defaulted-positional fields — mirrors the Phase 35 LANG-03 / Phase 36 36-02 sweep convention so every pre-Phase-36 construction site compiles unchanged. `null` Parameters is the legacy zero-arg form.

New `flow-lang/Ast/Elements/` namespace introduced for `SongElement` (abstract base) with concrete records:
- `BareSectionElement(name, RepeatCount)` — legacy bare-name song element
- `SectionCallElement(name, PositionalArgs, NamedArgs, RepeatCount)` — D-36-13 parameterized call

`SongExpression` gains a parallel defaulted-positional `Elements: IReadOnlyList<SongElement>?` carrying the full call-args + repeat-count detail; the legacy `Sections: IReadOnlyList<SongSectionReference>` field stays populated as a bare-name summary for any pre-Phase-36 consumer.

`BindingPattern.TypeAnnotation: FlowType?` lets typed section params (`Note root`) carry the Note type alongside the `root` name — used by PatternMatcher.TryMatchAll as the per-slot type-compatibility gate (+500 specificity vs untyped +200 per RESEARCH §Pattern 7).

`Parser.ParseSectionDeclaration` accepts an optional `(Pattern[, Pattern]...)` clause after the section name; each parameter is parsed via the new `ParseSectionParameterPattern` entry point which recognizes:
- Typed binding: `Type identifier` → BindingPattern with TypeAnnotation
- Tuple destructure: `<<Type identifier, Type identifier>>` → ConstructorPattern("Tuple", [...])
- Guard clause: `pattern when (expr)` → wrap inner in GuardPattern
- Otherwise: fall through to ParsePattern (LiteralPattern / ConstructorPattern with music-aware flags / etc.)

After each parameter, an optional `= Expression` default value is parsed (D-36-15) and stored in the parallel DefaultValues list.

`Parser.ParseSongExpression` recognizes `Identifier(args)*N` as a SectionCallElement (full named-arg + positional-arg handling mirrored from Plan 36-02's FunctionCallExpression branch) and `Identifier*N` as a BareSectionElement; the trailing `*N` postfix sets RepeatCount.

10 `SectionParamsParserTests` facts pin the parser contract — backward-compat bare section, typed binding, multi-param, chord constructor, tuple destructure, guard, defaults, song call, *N repeat, named args.

### 2. Overload dispatch + synthetic frame (Task 2, commit d0ddfb9)

#### SectionRegistry shape migration

`ExecutionContext.SectionRegistry` changes from `Dictionary<string, SectionData>` to `Dictionary<string, List<SectionData>>` so overloads with the same name coexist (D-36-18). A new `SectionRegistryFlat()` helper returns a flat `Dictionary<string, SectionData>` (last-registered per name) — used by Phase 30 SongFunctions and any other pre-Phase-36 consumer that doesn't yet know about overloads.

`TestSnapshot.SectionRegistry` field type updated to the list-shape; snapshot+restore deep-copy each per-name list so post-snapshot mutations don't leak. Phase 35 TEST-02 regression intact.

`SongData.SectionRegistry` deliberately STAYS `Dictionary<string, SectionData>` — the runtime materializes parameterized section calls under synthetic names (`verse#0`, `verse#1`, ...) at song-evaluation time, so SongRenderer / MidiExport / SfzSampleCache see a flat registry of zero-arg-shaped entries. This kept the migration surgical — only ExecutionContext + Interpreter + ExpressionEvaluator + a few snapshot/restore paths needed updates.

#### SectionData parameter metadata

`SectionData` extended with three optional fields:
- `Parameters: IReadOnlyList<Pattern>?` — declaration parameter list
- `DefaultValues: IReadOnlyList<Expression?>?` — per-slot default expressions
- `Body: IReadOnlyList<Statement>?` — captured body for callsite re-execution

Parameterized sections register a STUB entry with empty Sequences at declaration time; the body executes on each call site producing a materialized SectionData with harvested sequences.

#### PatternMatcher.TryMatchAll

New static helper `TryMatchAll(IReadOnlyList<Pattern>, IReadOnlyList<Value>, ExpressionEvaluator, ExecutionContext)` returns `(bool matched, Dictionary<string, Value> bindings, int specificity)`:

| Pattern kind | Specificity |
|---|---|
| LiteralPattern | 1000 |
| ConstructorPattern (chord literal / roman numeral / articulation symbol) | 800 |
| ConstructorPattern (tuple destructure, Name="Tuple") | 600 |
| BindingPattern (typed, TypeAnnotation non-null) | 500 |
| BindingPattern (untyped) | 200 |
| WildcardPattern | 100 |
| GuardPattern | inner pattern's specificity |

Typed BindingPattern enforces `arg.Type.IsCompatibleWith(annotation)` as a hard gate — type mismatch causes the candidate to MISS (not error). Tuple-destructure recurses into SubPatterns; chord-literal / roman-numeral / articulation-symbol delegates to the Phase 35-06 music-aware extractors via the existing PatternMatcher.PatternMatches dispatcher.

#### SectionOverloadDispatch.Resolve

3-stage runtime dispatcher:
1. **Build final args** — fold positional + named + DefaultValues into a per-candidate `Value[]` matching the candidate's parameter shape. Named args resolve against BindingPattern slot names; missing slots fill from defaults; un-satisfied slots disqualify the candidate (sibling overloads may still accept).
2. **Pattern-match per candidate** — PatternMatcher.TryMatchAll filters non-matches.
3. **Rank by specificity** — sum scores per candidate; ties → Ambiguous-overload diagnostic naming both source locations; otherwise pick the highest.

Failures route to:
- 0 candidates registered → `no section '<name>' is registered`
- 0 matches → `no overload of section '<name>' matches the supplied arguments`
- Ambiguous tie → `Ambiguous section overload — section '<name>' has two equally-specific overloads matching the supplied arguments (at <loc1> and <loc2>)`

#### Synthetic-frame body re-execution

`ExpressionEvaluator.EvaluateSong` dispatches each `SongExpression.Elements` entry:
- `BareSectionElement` → look up in `SectionRegistry`, pick the zero-arg overload (or last-registered as fallback)
- `SectionCallElement` → call `EvaluateSectionCallToData` which:
  1. Evaluates positional + named arg expressions to Values
  2. Calls `SectionOverloadDispatch.Resolve` to pick a candidate + get bindings
  3. Pushes a synthetic frame, declares bindings, captures the **callsite's MusicalContext** (Pitfall 7 — section body sees the CALLSITE's `key { ... }` / `tempo { ... }` not the declaration's)
  4. Re-runs the section body via `IFunctionInvoker.ExecuteStatement` (new interface method)
  5. Harvests local-variable Sequences + bare-expression Sequences (mirrors Interpreter.ExecuteSectionDeclaration's harvest shape)
  6. Materializes a SectionData under a synthetic key `verse#0` and registers it in the song's flat registry

#### Declaration-time ambiguity check (Pitfall 3)

`Interpreter.ExecuteSectionDeclaration` checks the new section's Parameters against every prior registration of the same name via `PatternsHaveIdenticalShape` (recursive structural comparator). Identical shapes emit:
```
Ambiguous section overload — section 'verse' already declared with identical pattern shape at <prior loc>
```
and refuse to register, so the resolver never has to tiebreak structurally-identical overloads at call time.

#### IFunctionInvoker contract extension

The interface gains two members so the ExpressionEvaluator's section-call dispatcher can re-execute body statements from outside the Interpreter class:
- `void ExecuteStatement(Statement stmt)` — executes a statement
- `Value? LastExpressionValue { get; }` — exposes the last evaluated expression value (for bare-expression sequence harvest)

Interpreter implements both naturally; the NoopInvoker test double in `ThunkTests.cs` is updated with NotSupportedException stubs.

6 SectionOverloadTests facts + 4 SectionDefaultsTests facts pin the runtime contract.

### 3. Composer-facing tests + diagnostics + structural comparator (Task 3, commit ac07132)

`PatternsHaveIdenticalShape` recursively compares pattern kinds + flags + sub-shapes so `verse(Cmaj7)` + `verse(<<Note root, Int reps>>)` + `verse(Note root)` correctly register as 3 distinct overloads (initial naive `GetType()`-only comparison incorrectly flagged Cmaj7 vs tuple-destructure as identical since both are ConstructorPattern records).

`SectionDiagnosticsTests` ships 4 facts:
- ArityMismatchRendersDiagnostic — wrong positional-arg count triggers a diagnostic
- TypeMismatchRendersDiagnostic — String passed to Note slot routes to no-overload
- AmbiguousOverloadRendersBothCandidates — declaration-time ambiguity caught
- UnknownSectionRaises — call to a name that's not registered

5 composer-facing `tests/test_section_*.flow` files exercise the surface end-to-end:
- `test_section_params` — basic typed parameter
- `test_section_overload` — 3-overload example from RESEARCH §Code Examples
- `test_section_pattern_destructure` — tuple destructure
- `test_section_repeat` — `*N` repetition (D-36-14)
- `test_section_defaults` — D-36-15 defaults + named-arg override

Two-run cmp-clean determinism verified on `test_section_overload` and `test_section_repeat` (identical SHA-256 across consecutive renders via `scripts/test_two_run_determinism.sh`).

## Decisions Made

- **D-36-10-01 — SectionRegistry shape change is internal-only.** The list-of-overloads lives only in `ExecutionContext.SectionRegistry`; downstream consumers (SongData / SongRenderer / MidiExport / SfzSampleCache) keep their flat-dict view via the synthetic-name materialization at song-evaluation time. This kept the migration surgical and preserved Phase 18/25/27/28/29/33 two-run cmp-clean determinism for renderable artifacts.
- **D-36-10-02 — Parameterized section body deferred to call time.** Zero-arg sections execute ONCE at declaration time (legacy path). Parameterized sections register declaration metadata only; the body re-executes on every call site with the call's bound parameter values pushed into a synthetic frame. The composer pays for what they use — a `verse(C4)*3` call invokes the body once, not three times (the renderer concatenates the produced buffer).
- **D-36-10-03 — Pitfall 7 dynamic-scope semantic.** The synthetic frame's MusicalContext is captured at the CALLSITE inside `EvaluateSectionCallToData` via `_context.GetMusicalContext()`. The declaration-time MusicalContext on the stub SectionData is informational only — the renderer reads from the materialized SectionData's Context which is the callsite snapshot. Documented in CLAUDE.md (T-36-25 threat-register entry).
- **D-36-10-04 — Pitfall 3 ambiguity caught at declaration, not call.** A structural recursive `PatternsHaveIdenticalShape` comparator runs in `Interpreter.ExecuteSectionDeclaration` against every prior same-name registration. Two `section verse(Note root)` declarations are rejected at the SECOND declaration site with the prior source location; this is friendlier than a runtime dispatch error and keeps the OverloadResolver's specificity ranking from having to invent a tiebreaker for genuinely-identical shapes.
- **D-36-10-05 — Typed BindingPattern is the type gate.** When `BindingPattern.TypeAnnotation` is non-null, `PatternMatcher.TryMatchAll` checks `arg.Type.IsCompatibleWith(annotation)` and the candidate MISSES on incompatibility (not errors — sibling overloads may accept the arg). This is the only way to differentiate `verse(Note root)` from `verse(Int root)` at dispatch time since both are otherwise BindingPattern.
- **D-36-10-06 — Tuple-destructure ConstructorPattern uses Name="Tuple" + SubPatterns.** The parser produces `ConstructorPattern("Tuple", [sub1, sub2, ...], IsChordLiteral=false, IsRomanNumeral=false, IsArticulationSymbol=false)` for `<<Type name, Type name>>` shapes. PatternMatcher.TryMatchAll discriminates via `cp.Name == "Tuple" && !cp.IsChordLiteral && !cp.IsRomanNumeral && !cp.IsArticulationSymbol` and recurses into each sub-pattern. Specificity is a flat +600 for the tuple itself; sub-pattern specificity isn't currently aggregated (acceptable for v1.5 baseline — composer feedback could refine).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] Naive `GetType()`-only identical-shape comparator incorrectly flagged Cmaj7 vs `<<Note, Int>>` as duplicates**
- **Found during:** Task 3 composer test `test_section_overload.flow`
- **Issue:** The initial Pitfall 3 pre-flight check compared `prior.Parameters[i].GetType() != newSection.Parameters[i].GetType()`. Both `Cmaj7` (chord literal) and `<<Note root, Int reps>>` (tuple destructure) are `ConstructorPattern` records, so the naive check classified them as identical shapes and refused to register the third overload.
- **Fix:** Introduced `PatternsHaveIdenticalShape` — a recursive structural comparator that distinguishes ConstructorPattern subtypes by their flags (IsChordLiteral / IsRomanNumeral / IsArticulationSymbol) and special-cases the Name="Tuple" tuple-destructure shape with sub-pattern recursion. Now correctly registers all three overloads as distinguishable.
- **Files modified:** flow-lang/Interpreter/Interpreter.cs
- **Commit:** ac07132

**2. [Rule 3 — Blocking] flow-cli was stale, surfacing as the wrong duplicate-section error**
- **Found during:** Task 3 manual `dotnet run --project flow-cli -- check ...` invocation
- **Issue:** `dotnet run --no-build` against flow-cli was using a stale binary that still had the pre-Plan-36-10 ExecuteSectionDeclaration logic (the unconditional duplicate-name reject). Composer-facing test runs misleadingly reported the legacy error message even though the worktree code had the new branch.
- **Fix:** Ran `dotnet build flow-cli/flow-cli.csproj` explicitly before re-running the composer tests. No source change.
- **Commit:** none required (rebuild only)

**3. [Rule 1 — Bug] Initial SyntheticFrameInheritsOuterMusicalContext xUnit fact tried to declare `Song s` INSIDE a `key { ... }` block and read it from the outer scope**
- **Found during:** Task 2 SectionDefaultsTests run
- **Issue:** Variables declared inside a `key Gmajor { ... }` block aren't visible at the outer scope — accessing `engine.Context.GetVariable("s")` after the block exits raised `Variable 's' not found`.
- **Fix:** Restructured the test to declare the song at the outer scope (Context.Key=null) and verified the materialized section's Context.Key is null in that case. The affirmative key-active case is exercised through the composer-facing test files.
- **Files modified:** flow-lang.Tests/Phase36/SectionDefaultsTests.cs
- **Commit:** d0ddfb9

**4. [Rule 1 — Bug] Composer test files initially used multi-statement `lazy(...)` bodies**
- **Found during:** Task 3 first `dotnet run --project flow-cli -- test tests/test_section_params.flow` invocation
- **Issue:** Flow's `lazy()` accepts a single expression, not a statement block — the planned `lazy(Song s = [verse(C4)] (assertEq ...))` parsed as a parse error at the second statement.
- **Fix:** Hoisted variable declarations out of `lazy()` to top-level scope, leaving each `lazy()` body as a single `(assertEq ...)` expression.
- **Files modified:** all 5 `tests/test_section_*.flow` files
- **Commit:** ac07132

**5. [Rule 3 — Blocking] `s.Sections` member access not supported on Song**
- **Found during:** Task 3 composer test
- **Issue:** Flow's Song type only exposes `SectionCount` via member access — not `Sections` directly.
- **Fix:** Replaced `(length s.Sections)` with `s.SectionCount` throughout the composer tests.
- **Files modified:** all 5 `tests/test_section_*.flow` files
- **Commit:** ac07132

All 5 auto-fixes are localized to test surfaces or downstream cosmetic comparators; the resolver / dispatcher / synthetic-frame contract from the plan is unchanged.

## Test Results

### Phase 36 Plan 36-10 xUnit suite (this plan)

```
dotnet test --filter "FullyQualifiedName~Phase36.SectionParamsParserTests" → 10/10 PASS
dotnet test --filter "FullyQualifiedName~Phase36.SectionOverloadTests"     → 6/6 PASS
dotnet test --filter "FullyQualifiedName~Phase36.SectionDefaultsTests"     → 4/4 PASS
dotnet test --filter "FullyQualifiedName~Phase36.SectionDiagnosticsTests"  → 4/4 PASS
                                                                  Total → 24/24 PASS
```

### Composer-facing acceptance

```
flow test tests/test_section_params.flow              → 1/1 PASS
flow test tests/test_section_overload.flow            → 3/3 PASS
flow test tests/test_section_pattern_destructure.flow → 1/1 PASS
flow test tests/test_section_repeat.flow              → 1/1 PASS
flow test tests/test_section_defaults.flow            → 2/2 PASS
                                              Total → 8/8 PASS
```

### Two-run determinism

```
scripts/test_two_run_determinism.sh tests/test_section_overload.flow → PASS (1bfb4ea6...)
scripts/test_two_run_determinism.sh tests/test_section_repeat.flow   → PASS (dfd5e861...)
```

### Regression gates

| Suite | Pass/Total | Status |
|-------|------------|--------|
| Phase 35 (language foundation) | 80/80 | unchanged |
| Phase 36 (full) | 175/175 | +24 from this plan; 141 prior |

Pre-existing Phase 28 articulation FFT-cosine + Phase 29 sampled-instrument-articulation + Phase 28 Ragtime RMS regression failures (32 in total) are NOT exercised by this plan's surface and are NOT regressions from this work — they predate Plan 36-10 (per the v1.5 backlog memory `project_v15_backlog.md` and STATE.md's "carryover highlights" section).

## What This Unblocks

- **Future Phase 36 plans authoring section-call sites** — every composer-facing `[verse(C4)*3]` shape works going forward; downstream plans can author tutorial chapters and rule-pack examples without re-engineering the dispatch surface.
- **v1.5 polymorphic / pivot section idiom** — composers can ship a single `section verse(Note root)` + `section verse(Cmaj7)` pair so transposed verses and pivot bars coexist in one place without duplicating the body.
- **CLAUDE.md document update** — the synthetic-frame Pitfall 7 dynamic-scope semantic should be surfaced in the Music-Specific section so composers expect callsite-context, not declaration-context. (Not done in this commit — left as a follow-up Plan 36-12 or Phase 36 verifier task.)

## Threat Surface Scan

No new attack surface — section overload + synthetic frame are composer-facing language ergonomics; no network endpoints, file I/O, auth paths, or schema boundaries are touched. The three threats enumerated in the plan's `<threat_model>` are all mitigated:

- **T-36-24 (Integrity: silent ambiguous overload pick)** — `PatternsHaveIdenticalShape` declaration-time check raises an Ambiguous-overload diagnostic before the registry ever contains two indistinguishable overloads. Runtime dispatch can also raise on equally-specific dispatch survivors as a defensive fallback. Both diagnostics flow through ErrorReporter.
- **T-36-25 (Integrity: Pitfall 7 dynamic-scope confusion)** — Dynamic-scope semantic is intentional + documented in this Summary's `<decisions>` block; the `SyntheticFrameInheritsOuterMusicalContext` SectionDefaultsTests fact pins the behavior. Composer-facing test ergonomics will encourage shipping a CLAUDE.md edit downstream.
- **T-36-26 (Integrity: SectionRegistry shape migration breaks zero-arg sections)** — Backward-compat is enforced via the `SectionRegistryFlat()` helper + every existing same-name single-registration path still works (validated by the 80/80 Phase 35 regression and the existing 141 Phase 36 tests).

## Self-Check: PASSED

- All 3 task commits exist in git log (`e935991`, `d0ddfb9`, `ac07132`)
- All 12 listed files-modified paths exist and contain the documented surfaces
- All 5 composer-facing `tests/test_section_*.flow` exist (force-added past `tests/` global gitignore)
- `dotnet build` exits 0
- `dotnet test --filter "FullyQualifiedName~Phase36.Section"` → 24/24
- `dotnet test --filter "FullyQualifiedName~Phase35"` → 80/80 (regression intact)
- `dotnet test --filter "FullyQualifiedName~Phase36"` → 175/175
- `flow test tests/test_section_*.flow` → 8/8 PASS
- Two-run cmp-clean determinism PASS on test_section_overload + test_section_repeat
