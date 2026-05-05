---
phase: 26-op-standardization-prefix-only
plan: 01
subsystem: testing
tags: [xunit, phase26, prefix-only, scaffolding, red-green, csproj]

# Dependency graph
requires: []
provides:
  - 7 xUnit Phase26 Fact files in flow-lang.Tests/Unit/Phase26/ pinning D-01/D-03/D-05/D-07/D-08/D-15
  - scripts/Migrate26/ project scaffold (csproj + stub Program.cs + README.md) ready for Wave 2
  - flow-sharp.sln updated to include Migrate26 project
affects: [26-02, 26-03, 26-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Test-first RED scaffolding: Wave 0 commits failing Facts before Wave 1 lands implementation"
    - "Registry-direct Fact pattern (S-05): BuildRegistry + Call helpers for fast no-engine assertions"
    - "FlowEngineRunner end-to-end Fact pattern (S-06): [Collection(\"FlowScripts\")] for stdout-capturing tests"
    - "Theory + InlineData matrix (S-07) for lex-position and infix-rejection coverage"

key-files:
  created:
    - flow-lang.Tests/Unit/Phase26/NewOverloadFacts.cs
    - flow-lang.Tests/Unit/Phase26/NegOverloadFacts.cs
    - flow-lang.Tests/Unit/Phase26/IntegerDivisionFacts.cs
    - flow-lang.Tests/Unit/Phase26/MixedTypeArithmeticFacts.cs
    - flow-lang.Tests/Unit/Phase26/NegativeLiteralLexFacts.cs
    - flow-lang.Tests/Unit/Phase26/UnaryMinusShorthandFacts.cs
    - flow-lang.Tests/Unit/Phase26/InfixRejectedFacts.cs
    - scripts/Migrate26/Migrate26.csproj
    - scripts/Migrate26/Program.cs
    - scripts/Migrate26/README.md
  modified:
    - flow-sharp.sln

key-decisions:
  - "Used BuiltInFunctions.RegisterAllImplementations(registry) for Fact bootstrapping — the actual public entry point on BuiltInFunctions (the plan's reference to a singular Register(registry) method does not exist; RegisterAllImplementations is correct)"
  - "Added Migrate26.csproj to flow-sharp.sln so dotnet build over the whole solution exercises the migration tool's compile path (per plan acceptance criterion: dotnet build whole solution exits 0)"
  - "NegativeLiteralLexFacts after-LBracket InlineData simplified to multi-statement Int decl per the plan's fallback note ('Voids' parses-but-isn't-a-real-type concern moot because the plan target is the lexer, not the parser)"

patterns-established:
  - "Phase26 Fact directory namespace: FlowLang.Tests.Unit.Phase26"
  - "Each registry-direct Fact file inlines BuildRegistry + Call helpers (matches HumanizeGaussianFacts.cs convention — file-scoped self-containment)"
  - "Migrate26 project scaffold: <OutputType>Exe</OutputType> + <ProjectReference>..\\..\\flow-lang\\flow-lang.csproj</ProjectReference> two levels up from scripts/"

requirements-completed: [STD-01, STD-02]

# Metrics
duration: 18min
completed: 2026-05-04
---

# Phase 26 Plan 01: Wave 0 RED Scaffolding Summary

**7 xUnit Fact files (33 RED Facts pinning D-01/D-03/D-05/D-07/D-08/D-15 + Pitfall-1) plus Migrate26 csproj scaffold; whole solution builds clean, Phase26 filter discovers all Facts.**

## Performance

- **Duration:** ~18 min
- **Started:** 2026-05-04T23:01:00Z (approximate — start time recorded at agent spawn)
- **Completed:** 2026-05-04T23:11:00Z
- **Tasks:** 3 of 3
- **Files created:** 10
- **Files modified:** 1 (flow-sharp.sln)

## Accomplishments

- 7 xUnit Phase26 Fact files compile and are discoverable by `dotnet test --filter "FullyQualifiedName~Phase26"`
- 36 total Phase26 Facts (24 [Fact] + 2 [Theory] yielding 12 InlineData rows): 33 currently RED, 3 incidentally GREEN (TempoMinus_PreservesStandaloneMinus pre-implementation passthrough + 2 unary shorthand stdout-substring matches that work via the legacy parser)
- scripts/Migrate26/ project scaffold (csproj + stub Program.cs + README.md) compiles, runs, and validates the relative ProjectReference path traversal `..\..\flow-lang\flow-lang.csproj`
- Migrate26 project added to flow-sharp.sln so `dotnet build` over the whole solution exercises it
- Whole solution builds with 0 warnings, 0 errors

## Task Commits

Wave 0 was committed atomically per Decision D-13 (single mega-commit shape; no intermediate state where build is green AND tests pass between waves):

1. **Wave 0 (Tasks 1-3 combined):** 7 Fact files + scripts/Migrate26/{Migrate26.csproj, Program.cs, README.md} + flow-sharp.sln update — `8209ec2` (test)

## Files Created/Modified

### Created (10 files)

- `flow-lang.Tests/Unit/Phase26/NewOverloadFacts.cs` — 8 Facts pinning D-05 same-type Long+Number arithmetic registrations (registry-direct)
- `flow-lang.Tests/Unit/Phase26/NegOverloadFacts.cs` — 5 Facts pinning D-07 `(neg)` 5-pack, one per numeric type
- `flow-lang.Tests/Unit/Phase26/IntegerDivisionFacts.cs` — 2 Facts pinning D-08 `(div Int Int)→Double` auto-promotion + `(idiv Int Int)→Int` truncation
- `flow-lang.Tests/Unit/Phase26/MixedTypeArithmeticFacts.cs` — 6 Facts pinning D-05 convertible-scoring path (Int+Double, Float+Double, Int+Long, Long+Number, etc.) via FlowEngineRunner
- `flow-lang.Tests/Unit/Phase26/NegativeLiteralLexFacts.cs` — Theory with 7 InlineData rows (one per expression-start lex position) + TempoMinus_PreservesStandaloneMinus Pitfall-1 Fact
- `flow-lang.Tests/Unit/Phase26/UnaryMinusShorthandFacts.cs` — 2 Facts pinning D-01 `-x→(neg x)` + D-03 `+x` silent strip via FlowEngineRunner
- `flow-lang.Tests/Unit/Phase26/InfixRejectedFacts.cs` — Theory with 5 InlineData rows asserting bare infix produces parse error (D-15)
- `scripts/Migrate26/Migrate26.csproj` — standalone Exe csproj with `<ProjectReference Include="..\..\flow-lang\flow-lang.csproj" />` per P-16
- `scripts/Migrate26/Program.cs` — Wave 0 stub Main; touches `new SimpleLexer(...)` to validate ProjectReference path
- `scripts/Migrate26/README.md` — Wave-by-wave plan and historical-record note per D-12

### Modified (1 file)

- `flow-sharp.sln` — added Migrate26 project entry via `dotnet sln add`

## Decisions Made

- **Used `BuiltInFunctions.RegisterAllImplementations(registry)`** for Fact bootstrapping. The plan's `<interfaces>` section referenced a singular `BuiltInFunctions.Register(registry)` API; the actual public entry point is `RegisterAllImplementations`. This is the same method `FlowEngine.cs:47` uses, so the registry built by these Facts matches the production registry exactly.

- **Added Migrate26.csproj to flow-sharp.sln.** The plan's acceptance criterion required `dotnet build` (whole solution) to exit 0 — adding the project to the .sln ensures the build actually exercises it (otherwise it would be a freestanding csproj never built by the solution-level command).

- **Simplified the after-LBracket InlineData row** in NegativeLiteralLexFacts. The plan's example `"Voids a = [-1, 2, 3]"` referenced a non-existent type "Voids" with a fallback note suggesting a multi-statement form if needed. Used `"Int x = 5\nInt z = -1"` to exercise the same statement-start lex position cleanly. The lexer doesn't reject unknown identifiers (it produces Identifier tokens), so this is a documentation-cosmetic change, not a behavior change.

- **Suppressed xUnit1026 unused-parameter warning** in NegativeLiteralLexFacts by including the `desc` parameter in the assertion failure message (instead of removing it). This preserves the human-readable Theory descriptions in test output.

## Deviations from Plan

None — plan executed exactly as written, modulo three minor corrections (documented above as Decisions, not deviations because they fall under the planner's "Claude's Discretion" allowances):

1. Method name correction: `BuiltInFunctions.RegisterAllImplementations` instead of the plan's claimed `BuiltInFunctions.Register`. The interface block in the plan was reference-imprecise; the actual public surface uses the full name. No behavior difference — `RegisterAllImplementations` is the production-equivalent registration path.
2. flow-sharp.sln addition was required to satisfy the "dotnet build whole solution" acceptance criterion; not flagged as a deviation because the plan's success criteria implicitly require it.
3. NegativeLiteralLexFacts InlineData simplification was explicitly permitted by the plan's fallback note (`"if 'Voids' produces a parse-time issue, simplify to 'Int z = -1' style"`).

## Issues Encountered

- **`*.md` gitignore rule** — `.gitignore` line 18 has `*.md` with whitelist exceptions for `!.planning/**/*.md` and `!CLAUDE.md`. Initial concern that `scripts/Migrate26/README.md` would be ignored. Resolved on inspection: `.gitignore` has a downstream `!README.md` rule (line 35) that re-includes README files everywhere. Verified via `git check-ignore` before committing.

## Test Results

- **Pre-Wave-1 RED state confirmed:** `dotnet test --filter "FullyQualifiedName~Phase26"` reports `Failed: 33, Passed: 3, Skipped: 0, Total: 36`. The 3 passing Facts are intentional pre-implementation passthroughs:
  - `TempoMinus_PreservesStandaloneMinus` — passes because the lexer hasn't been changed yet, so `tempo` followed by `Minus` followed by IntLiteral(120) is the natural lex output. Wave 1 must preserve this when adding the expression-start gate (Pitfall-1).
  - `MinusIdent_LowersToNegCall` — passes by coincidence because the legacy parser handles `Int y = -x` via the soon-to-be-deleted `0 - x` trick in `ParseUnary`. Wave 1 deletes that trick and replaces it with the `(neg x)` shorthand; result remains -5, so the Fact stays GREEN through the transition.
  - `PlusIdent_StripsSilently` — passes by coincidence because the legacy parser also handles `+x` via `0 + x` in the unary arithmetic branch. Wave 1 silently strips the Plus token; result remains 5.
- All 36 Facts are discoverable. No compile errors, no test runner errors, no exceptions about file-not-found.

## Self-Check: PASSED

- ✓ All 7 Phase26 Fact files exist in `flow-lang.Tests/Unit/Phase26/` (verified via `find ... | wc -l = 7`)
- ✓ All 7 Fact files contain `namespace FlowLang.Tests.Unit.Phase26` (verified via `grep -l`)
- ✓ Fact counts per acceptance criteria:
  - NewOverloadFacts: 8 [Fact] (≥8) ✓
  - NegOverloadFacts: 5 [Fact] (≥5) ✓
  - IntegerDivisionFacts: 2 [Fact] containing `DivIntInt_AutoPromotesToDouble` and `IDivIntInt_TruncatesToInt` ✓
  - NegativeLiteralLexFacts: 1 [Theory] with 7 [InlineData] + `TempoMinus_PreservesStandaloneMinus` Fact ✓
  - MixedTypeArithmeticFacts: 6 [Fact] with `[Collection("FlowScripts")]` ✓
  - UnaryMinusShorthandFacts: 2 [Fact] containing `MinusIdent_LowersToNegCall` and `PlusIdent_StripsSilently` ✓
  - InfixRejectedFacts: 1 [Theory] with 5 [InlineData] ✓
- ✓ `scripts/Migrate26/Migrate26.csproj` exists, contains `<ProjectReference Include="..\..\flow-lang\flow-lang.csproj" />`, `<OutputType>Exe</OutputType>`, `<TargetFramework>net10.0</TargetFramework>`
- ✓ `scripts/Migrate26/Program.cs` exists, contains `namespace FlowLang.Migrate26` and `new SimpleLexer(`
- ✓ `scripts/Migrate26/README.md` exists
- ✓ `dotnet build` (whole solution) exits 0 with 0 warnings, 0 errors
- ✓ `dotnet run --project scripts/Migrate26 --` (no args) prints the "Wave 0 stub" notice and exits 0
- ✓ Commit hash `8209ec2` exists in `git log --oneline`
- ✓ `git status --porcelain` is clean post-commit (no untracked files, no modified files)

## Next Phase Readiness

- **Wave 1 (plan 26-02) ready:** Lexer + parser + builtins changes (P-02 through P-08) can land directly against this scaffold. The 33 RED Facts are the green-gate for Wave 1.
- **Wave 2 (plan 26-03) ready:** `scripts/Migrate26/Program.cs` has a stub Main that compiles and a touch-the-lexer line forcing the ProjectReference to be exercised. Wave 2 fills in the token walker + precedence climber per P-17.
- **Wave 3 (plan 26-04) ready:** README.md documents the wave-by-wave plan; `dotnet run --project scripts/Migrate26 -- ...` is the invocation path that Wave 3 will use to sweep `tests/`, `examples/`, and `flow-lang/`.

## Risks / Concerns

- **3 incidentally-GREEN Facts** in pre-Wave-1 state (documented above). These three Facts pass *for the wrong reason* today (legacy parser implementation) but happen to give the right answer. Wave 1 must verify they remain GREEN *for the right reason* (lexer expression-start gate + parser shorthand). If any of the three flips to RED during Wave 1, the implementation has regressed; if all three stay GREEN, the contract holds.
- **No risks blocking Wave 1.** All 7 Fact files compile, all 36 Facts are discoverable, the build is clean.

---

*Phase: 26-op-standardization-prefix-only*
*Plan: 01 (Wave 0)*
*Completed: 2026-05-04*
*Commit: 8209ec2*
