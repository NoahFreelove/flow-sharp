---
phase: 33-sfz-orchestral-sampler
plan: 05
subsystem: audio
tags: [sfz, sampler, stdlib, builtins, symbol-dispatch, flow-config, opt-in-gating]

# Dependency graph
requires:
  - phase: 33-sfz-orchestral-sampler
    provides:
      - 33-01 — VSCO-CE 1.1.0 path audit (19 GM symbols, 15 verified + 4 TBD)
      - 33-02 — Value.Sfz factory + SfzType + ExecutionContext SFZ surface (SfzEnabled / SfzInstruments / SfzPatchRegistry / SfzDiagnostics / ResolvedSfzRoot) + FlowConfigPoco.SfzRoot
      - 33-04 — SfzParser.Parse entry point + SfzData / SfzRegion / SfzLoopMode / SfzParseException model
  - phase: 32-full-scala-scl-tuning-loader
    provides: ScalaBuiltins.Register template + RenderingDiagnostics.WarnOnce pattern + Tuning-in-std.flow forward-decl precedent
  - phase: 30-flow-config
    provides: FlowConfig.Active singleton + FlowConfig.Reset() test isolation hook
  - phase: 26.1
    provides: Symbol primitive type + per-context SymbolInternTable (Pitfall 1) + generic Dict<K, V> with (dict ...) constructor
provides:
  - flow-lang/sfz.flow — opt-in stdlib module, declares `__enableSfzModule` forward-decl + 19-entry GM dict + side-effecting init marker
  - flow-lang/StandardLibrary/Audio/Sfz/SfzBuiltins.cs — Register(InternalFunctionRegistry, ExecutionContext) wiring three signatures (__enableSfzModule, loadSfz Symbol, loadSfz String) + full LoadSfzSymbol/LoadSfzString bodies + Pitfall-2 first-read sfz_root cache + one-shot missing-config advisory
  - flow-lang/Core/FlowEngine.cs — SfzBuiltins.Register call site (Plan 33-05 owns this insertion; Plan 33-07 only verifies presence)
  - 3 integration test classes — SfzGatingTests (3 facts) + SfzSymbolLookupTests (6 facts) + SfzConfigTests (2 facts), 11 facts total, all green
affects: [33-06, 33-07, 34-symphony-showcase]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Composer-visible opt-in stdlib surface gated at runtime via ExecutionContext.SfzEnabled, NOT at parse-resolution time (CONTEXT D-10) — forward-decls in std.flow keep the function visible so the C# gate fires with a targeted 'use \"@sfz\"' error message instead of the generic 'Function not found'"
    - "Pitfall-2 first-read cache on ExecutionContext for FlowConfig.Active.SfzRoot — prevents singleton-mutation test flakes and insulates a single render from mid-script config edits"
    - "Symbol+String overload pair for loadSfz, dispatched by the OverloadResolver's exact-match scoring (Pitfall 12 — Phase 26.1 SYM-01 keeps Symbol strictly separate from String)"
    - "TBD-placeholder convention for VSCO-CE-1.1.0 gaps — the 4 missing GM rows ship a known _TBD_-prefixed filename so the lookup path produces a clear 'not bundled with VSCO Community Edition' error pointing at the absolute-path overload, not a generic FileNotFoundException"
    - "DictType wildcard registration symmetric with ArrayType — added to InternalFunctionRegistry.TypesEqual so Dict<Void, Void> overload registrations bind concrete Dict<Symbol, String> internal-proc decls at registration time (the OverloadResolver path already handled this via DictType.IsCompatibleWith; the registry layer needed the same wildcard)"

key-files:
  created:
    - flow-lang/sfz.flow
    - flow-lang/StandardLibrary/Audio/Sfz/SfzBuiltins.cs
    - flow-lang.Tests/Integration/Phase33/SfzGatingTests.cs
    - flow-lang.Tests/Integration/Phase33/SfzSymbolLookupTests.cs
    - flow-lang.Tests/Integration/Phase33/SfzConfigTests.cs
  modified:
    - flow-lang/Core/FlowEngine.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang/Parsing/TypeParser.cs
    - flow-lang/Lexing/SimpleLexer.cs
    - flow-lang/StandardLibrary/InternalFunctionRegistry.cs
    - flow-lang/std.flow
    - flow-lang/flow-lang.csproj
    - .gitignore

key-decisions:
  - "Moved the `loadSfz(Symbol)` + `loadSfz(String)` forward-decls from sfz.flow to std.flow so the function is visible at parse-resolution time WITHOUT `use \"@sfz\"`. CONTEXT D-10's contract is that the gate is RUNTIME via SfzEnabled — putting the forward-decls in sfz.flow (the opt-in module) would gate at parse-resolution instead, producing the wrong 'Function not found' error. With this move, the composer gets the targeted SPEC-1 message: 'loadSfz requires use \"@sfz\"'. Only `__enableSfzModule` stays in sfz.flow because it's an internal-only marker."
  - "Added `use \"@std\"` at the top of sfz.flow so the (dict ...) constructor is available when sfz.flow runs. Composers who `use \"@sfz\"` therefore get @std transitively — mirrors the existing composition.flow → @audio pattern."
  - "TBD-placeholder filenames (`_TBD_choir_not_in_vsco-ce.sfz` etc.) chosen over null/empty strings for the 4 VSCO-CE-1.1.0 gaps. The TBD prefix triggers a specific 'not bundled with VSCO Community Edition' error that points the composer at the absolute-path overload, rather than the generic FileNotFoundException a real-looking path would produce when the file is missing."
  - "Sorted (StringComparer.Ordinal) the supported-symbol list in the unknown-symbol error message. Deterministic + scannable composer-facing output."
  - "ResolveSfzRoot returns `string` (not `string?`) and throws on null/empty — the caller path never needs to null-check the cached value. The throw site fires the WarnOnce advisory before throwing so composers get the stderr guidance even if the exception is caught upstream."

patterns-established:
  - "Multi-rule deviation cluster — 6 Rule-2 missing-critical-functionality fixes (TypeParser binding, IsTypeKeyword allowlist, SimpleLexer `_` continuation, csproj copy-to-output, InternalFunctionRegistry DictType wildcard, std.flow forward-decls) all landed inside Task 2's single commit alongside the plan-specified body work. Each was a precondition for ANY Flow-source `Sfz`/`loadSfz` codepath to function — they belong with the body, not as separate prep commits."
  - "SfzBuiltins ResolveSfzRoot helper pattern — first-call reads FlowConfig.Active, caches on ctx, throws-with-advisory on null. Future surfaces (e.g. when Phase 34's symphony showcase adds a `Sfz` MIDI export hook) can adopt the same pattern verbatim."

requirements-completed: [SPEC-1, SPEC-2, SPEC-3]

# Metrics
duration: 25min
completed: 2026-05-16
tasks: 2
commits: 2
files-touched: 12
new-test-classes: 3
new-test-facts: 11
---

# Phase 33 Plan 33-05: Composer-Visible @sfz Stdlib + loadSfz Builtins Summary

**Composer surface for the SFZ orchestral sampler — `use "@sfz"` flips the runtime gate, the 19-entry GM dict ships in Flow source (not C#), and `loadSfz #violin` / `loadSfz "/path"` both produce composer-facing errors that name the fix.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-05-16T02:58:13Z
- **Completed:** 2026-05-16
- **Tasks:** 2 (one scaffolding + one TDD body+tests)
- **Files modified:** 12 (5 created, 7 patched, 1 patched + gitignored)

## Accomplishments

- **`flow-lang/sfz.flow`** — opt-in stdlib module. Forward-declares `__enableSfzModule(Dict<Symbol, String>)`, binds the 19-entry GM orchestral dict (15 verified VSCO-CE 1.1.0 rows + 4 TBD placeholders per the Plan 33-01 audit), then calls `(__enableSfzModule __sfzInstruments)` as the side-effecting init marker. Pulls in `@std` transitively for the `dict` constructor.
- **`flow-lang/StandardLibrary/Audio/Sfz/SfzBuiltins.cs`** — full builtin surface. Registers three signatures (`__enableSfzModule`, `loadSfz(Symbol)`, `loadSfz(String)`). LoadSfzSymbol does the dict lookup + TBD-placeholder detection + Pitfall-2 cached sfz_root resolve + Path.Combine + SfzParser.Parse + Value.Sfz wrap. LoadSfzString bypasses the dict for absolute/relative literal paths. EnableSfzModule copies the dict entries into ctx.SfzInstruments and flips ctx.SfzEnabled.
- **`flow-lang/Core/FlowEngine.cs`** — `SfzBuiltins.Register(internalRegistry, _context)` inserted after `RegisterContextDependentFunctions`, alongside the existing Phase 32 ScalaBuiltins wiring. Plan 33-05 OWNS this insertion (per the plan's must_haves); Plan 33-07 will only verify presence.
- **3 integration test classes / 11 facts** all green:
  - **`SfzGatingTests.cs`** — 3 facts: LoadSfz_WithoutImport_Errors (SPEC-1), LoadSfzString_WithoutImport_Errors (companion), LoadSfz_WithImport_NoGateError_ButMissingRootError (positive control proving the gate flips correctly).
  - **`SfzSymbolLookupTests.cs`** — 6 facts: WithSymbol_AndConfig_ResolvesPath (happy path), MultipleSymbols_AllResolve (4 symbols), WithUnknownSymbol_Errors (lists all 19 supported), WithTbdSymbol_ErrorsWithVscoCeNote (4 TBD rows), WithString_BypassesDict, WithString_MissingFile_Errors.
  - **`SfzConfigTests.cs`** — 2 facts: MissingRoot_Errors (config path error), SfzRoot_CachedOncePerContext (Pitfall-2 isolation contract).

## Task Commits

| # | Name                                                  | Commit    |
| - | ----------------------------------------------------- | --------- |
| 1 | sfz.flow stdlib + SfzBuiltins skeleton                | `37dfea0` |
| 2 | loadSfz Symbol/String body + 11 integration tests     | `043d3a3` |

Plan metadata commit: _(orchestrator-managed in worktree mode)_

## Files Created/Modified

### Created
- `flow-lang/sfz.flow` — 19-entry GM dict + __enableSfzModule call site (62 LOC)
- `flow-lang/StandardLibrary/Audio/Sfz/SfzBuiltins.cs` — Register + 3 builtin bodies + ResolveSfzRoot helper (~220 LOC)
- `flow-lang.Tests/Integration/Phase33/SfzGatingTests.cs` — 3 SPEC-1 facts
- `flow-lang.Tests/Integration/Phase33/SfzSymbolLookupTests.cs` — 6 SPEC-2 facts (dict + lookup)
- `flow-lang.Tests/Integration/Phase33/SfzConfigTests.cs` — 2 SPEC-2 facts (config + cache)

### Modified
- `flow-lang/Core/FlowEngine.cs` — SfzBuiltins.Register wired after RegisterContextDependentFunctions
- `flow-lang/Parsing/TypeParser.cs` — `Sfz → SfzType.Instance` in both keyword switch + fallback
- `flow-lang/Parsing/Parser.cs` — `"Sfz"` added to IsTypeKeyword allowlist
- `flow-lang/Lexing/SimpleLexer.cs` — `_` fall-through to identifier when followed by another `_` (so `__enableSfzModule` lexes as one Identifier)
- `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` — DictType wildcard case in TypesEqual symmetric with ArrayType
- `flow-lang/std.flow` — loadSfz(Symbol) + loadSfz(String) forward-decls
- `flow-lang/flow-lang.csproj` — sfz.flow CopyToOutputDirectory + CopyToPublishDirectory
- `.gitignore` — `!flow-lang/sfz.flow` allow-list entry

## Decisions Made

- **Resolved the SPEC-1 gating-layer ambiguity** in favour of `runtime-gate-with-always-visible-forward-decls`. CONTEXT D-10 specifies the runtime check via SfzEnabled, but the plan text was silent on whether the forward-decls live in `sfz.flow` (opt-in) or `std.flow` (always visible). Putting them in `sfz.flow` would cause "Function not found" before the gate fires — exactly the WRONG composer experience SPEC-1's acceptance text rejects ("error message containing `use \"@sfz\"`"). Moved the forward-decls to `std.flow`. Only the internal-only `__enableSfzModule` marker stays in `sfz.flow`.
- **`use "@std"` at the top of `sfz.flow`** so the (dict ...) constructor is available when the module's side-effecting init runs. Mirrors `composition.flow`'s existing `use "@audio"` precedent.
- **TBD-placeholder filenames** for the 4 VSCO-CE-1.1.0 gaps (`_TBD_choir_not_in_vsco-ce.sfz` etc.) chosen over null/empty strings. Triggers a specific "not bundled with VSCO Community Edition" error pointing at the absolute-path overload — semantically distinct from a normal FileNotFoundException, which is what the composer would otherwise see if VSCO bundled the file at a different path.
- **`ResolveSfzRoot` throws-then-warns ordering** — the WarnOnce advisory fires BEFORE throwing so composers see the stderr guidance even if the exception is caught upstream. Sentinel-keyed dedup keeps the advisory from spamming on repeated calls.
- **Sorted-Ordinal supported-symbol error message** — deterministic, scannable, deduplication-friendly.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical Functionality] TypeParser binding for `Sfz`**
- **Found during:** Task 2 (test run)
- **Issue:** Plan 33-02 added `SfzType.cs` to `flow-lang/TypeSystem/SpecialTypes/` but did NOT add the `"Sfz" → SfzType.Instance` entries to `flow-lang/Parsing/TypeParser.cs` (both the keyword switch at line 206 AND the fallback at line 321). Without these, `Sfz v = (loadSfz #violin)` failed at parse time with "Expected type name".
- **Fix:** Added `TokenType.Identifier when token.Text == "Sfz" => SfzType.Instance` and `"Sfz" => SfzType.Instance` entries alongside the existing `"Tuning"` precedent.
- **Files modified:** `flow-lang/Parsing/TypeParser.cs`
- **Verification:** All 11 new SFZ tests now pass — `Sfz v = ...` declarations parse correctly. Phase 32 (`Tuning t = ...`) suite stays 82/82 green.
- **Committed in:** `043d3a3` (Task 2)

**2. [Rule 2 - Missing Critical Functionality] Parser IsTypeKeyword allowlist for "Sfz"**
- **Found during:** Task 2 (test run; same regression as #1)
- **Issue:** `flow-lang/Parsing/Parser.cs` line 1322-1330 has a hardcoded allowlist of type-name identifier strings (`IsTypeKeyword`) controlling whether a statement-start identifier routes to `ParseVariableDeclaration`. Without `"Sfz"` in this list, the parser saw `Sfz v = ...` as an expression-statement and choked on `=`.
- **Fix:** Added `or "Sfz"` to the allowlist alongside `or "Tuning"`.
- **Files modified:** `flow-lang/Parsing/Parser.cs`
- **Verification:** `Sfz v = (loadSfz ...)` now parses through to `ParseVariableDeclaration` and the type binding works.
- **Committed in:** `043d3a3` (Task 2)

**3. [Rule 2 - Missing Critical Functionality] SimpleLexer `__` identifier support**
- **Found during:** Task 2 (running sfz.flow standalone)
- **Issue:** `flow-lang/Lexing/SimpleLexer.cs` line 157-161 has a `case '_':` branch that returns `SingleChar(TokenType.Underscore)` when followed by a non-letter-or-digit char. For `__enableSfzModule`, the first `_` is followed by `_` — neither letter nor digit — so the lexer emits two `Underscore` tokens + `enableSfzModule`, breaking the parse. Phase 33 is the first feature to use the `__` internal-marker naming convention so this gap had never surfaced.
- **Fix:** Extended the check to also accept `_` as a continuation character: `if (IsAtEnd() || (!char.IsLetterOrDigit(PeekNext()) && PeekNext() != '_')) return SingleChar(Underscore);`. Now `__enableSfzModule` falls through to `ScanIdentifierOrKeyword` and lexes as one Identifier token.
- **Files modified:** `flow-lang/Lexing/SimpleLexer.cs`
- **Verification:** `__enableSfzModule` parses correctly. Single-underscore rest token (`_` in note streams) still works because the `IsAtEnd()` and "next char is whitespace / `(` / `)` / etc." cases all still hit the `SingleChar(Underscore)` branch.
- **Committed in:** `043d3a3` (Task 2)

**4. [Rule 2 - Missing Critical Functionality] InternalFunctionRegistry DictType wildcard**
- **Found during:** Task 2 (test run; "No C# implementation found for internal proc '__enableSfzModule' with signature __enableSfzModule(Dict<Symbol, String>)")
- **Issue:** `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` `TypesEqual` had ArrayType-wildcard handling but NOT DictType-wildcard handling. My `__enableSfzModule` signature uses `DictType(Void, Void)` (the wildcard convention from BuiltInFunctions.cs:944-957 for the existing dict ops). At runtime, the Flow forward-decl `internal proc __enableSfzModule (Dict<Symbol, String>: instruments)` produces a signature with `DictType(Symbol, String)`. Without the recursive wildcard, the registration lookup at `Interpreter.ExecuteProcDeclaration:567` failed — the existing dict ops (get/set/remove/...) work via the OverloadResolver's IsCompatibleWith check on calls, but my CASE was at REGISTRATION time via `TryGetImplementation`, which uses a stricter equality.
- **Fix:** Added a recursive `DictType` case to `TypesEqual` symmetric with the ArrayType case: `if (registered is DictType rDict && requested is DictType reqDict) return TypesEqual(rDict.KeyType, reqDict.KeyType) && TypesEqual(rDict.ValueType, reqDict.ValueType);`. Now the Void-wildcard propagates through the recursion.
- **Files modified:** `flow-lang/StandardLibrary/InternalFunctionRegistry.cs`
- **Verification:** `__enableSfzModule` registration binds correctly. Phase 26 (Dict ops) + Phase 30 stay 136/136 green — the fix is purely additive.
- **Committed in:** `043d3a3` (Task 2)

**5. [Rule 2 - Missing Critical Functionality] loadSfz forward-decls in std.flow (not sfz.flow)**
- **Found during:** Task 2 (test run; "Function 'loadSfz' not found" reached BEFORE the SfzEnabled gate)
- **Issue:** SPEC-1's acceptance text mandates the error message contain `use "@sfz"`. CONTEXT D-10 specifies the SfzEnabled RUNTIME gate. The plan put the forward-decls in `sfz.flow`, but that makes them invisible at parse-resolution time without the `use "@sfz"` import — the Flow resolver fails with the GENERIC "Function not found" before the gate can fire. The composer-facing error misses SPEC-1 entirely.
- **Fix:** Moved the `loadSfz(Symbol)` + `loadSfz(String)` forward-decls from `sfz.flow` to `std.flow` so they're always visible. The runtime SfzEnabled check now produces the targeted SPEC-1-compliant error. Only `__enableSfzModule` (an internal-only marker composers never call) stays in `sfz.flow`.
- **Files modified:** `flow-lang/std.flow`, `flow-lang/sfz.flow`
- **Verification:** LoadSfz_WithoutImport_Errors fact (SfzGatingTests) passes — stderr contains the exact `use "@sfz"` substring.
- **Committed in:** `043d3a3` (Task 2)

**6. [Rule 2 - Missing Critical Functionality] csproj copy-to-output for sfz.flow**
- **Found during:** Task 2 (test run; "Import file not found: .../bin/Debug/net10.0/sfz.flow")
- **Issue:** `flow-lang/flow-lang.csproj` has explicit `<None Update="*.flow"><CopyToOutputDirectory>` entries for every stdlib module (std.flow, audio.flow, etc.) so the `ModuleLoader` can find them at `AppContext.BaseDirectory` at runtime. The plan didn't mention this gap; without it, my new `sfz.flow` never reached the test/runtime bin directory.
- **Fix:** Added the same `CopyToOutputDirectory` + `CopyToPublishDirectory` block for `sfz.flow`.
- **Files modified:** `flow-lang/flow-lang.csproj`
- **Verification:** `flow-lang.Tests/bin/Debug/net10.0/sfz.flow` exists post-build; `use "@sfz"` resolves at runtime.
- **Committed in:** `043d3a3` (Task 2)

**7. [Rule 2 - Missing Critical Functionality] .gitignore allow-list for flow-lang/sfz.flow**
- **Found during:** Task 1 (staging)
- **Issue:** The repo's `.gitignore` line 10 has `*.flow` globally ignored. Existing stdlib files (`std.flow`, `audio.flow`, etc.) escape the ignore because they predate the rule. Phase 32 added `examples/scala/**/*.flow` allow-list and Phase 28 added `examples/tests/**/*.flow`, but NO entry covers `flow-lang/sfz.flow`. Without an explicit allow-list line, `git add flow-lang/sfz.flow` reported "ignored by gitignore."
- **Fix:** Added `!flow-lang/sfz.flow` after the Phase 33 SFZ smoke fixture block.
- **Files modified:** `.gitignore`
- **Verification:** `git check-ignore -v flow-lang/sfz.flow` shows the allow-list match. The file commits cleanly in Task 1's commit.
- **Committed in:** `37dfea0` (Task 1) — gitignore + sfz.flow + SfzBuiltins.cs landed together

---

**Total deviations:** 7 auto-fixed (all Rule 2 — Missing Critical Functionality).

**Impact on plan:** All 7 were prerequisite fixes — without them, the plan-specified Sfz/loadSfz codepath was not callable from Flow source. Each fix is small, scoped, and validated by the new integration tests. Phase 26/30/32 cross-suite verification (218 tests across 3 phases) confirms no regression. No scope creep beyond what was strictly needed to make the plan's surface usable.

## Issues Encountered

- **TDD RED/GREEN cycle compression:** the plan's TDD task structure expected three discrete commits (RED test, GREEN feat, optional REFACTOR). In practice I iterated through 7 prerequisite Rule-2 fixes alongside the body implementation — the RED-phase failures my tests produced were initially "parser doesn't know Sfz" / "lexer doesn't know `__`" rather than the SfzEnabled gate semantics. Once the prerequisites landed, the gate logic itself was straightforward (~30 LOC). Committing all the body + prerequisite fixes as a single GREEN feat commit is the cleanest representation; the RED-phase intent is captured in the plan + this summary's deviation log.

- **Pre-existing Phase 28 test failures (26)** in PerSynthArticulationTests + RagtimeFixtureTests are NOT regressions. Verified by `git stash + dotnet test` against commit `37dfea0` (pre-Task-2 baseline) — the same 26 failures exist there. Plan 33-04's summary also flagged these. Out of scope per the executor SCOPE BOUNDARY rule.

## User Setup Required

None — Plan 33-05 ships the surface; the `sfz_root` config key is populated by the composer at install time (not a phase-time setup task). Composer-facing instructions for that config edit are out of scope here; Plan 33-07's renderer wiring will surface them in the examples/symphony/README.md.

## Threat Model Compliance

| Threat ID         | Disposition      | Mitigation Status                                                                                  |
| ----------------- | ---------------- | -------------------------------------------------------------------------------------------------- |
| T-33-PATH-01      | accept           | Symbol-resolved Path.Combine + absolute-path String overload documented contract. v1.5 follow-up.   |
| T-33-IO-01        | accept           | Phase 32 loadScala precedent — composer-controlled config; same posture as writeWav.                |
| T-33-PITFALL-2    | mitigate         | ResolveSfzRoot helper caches FlowConfig.Active.SfzRoot on ctx.ResolvedSfzRoot on first read.        |

T-33-PITFALL-2 is fully mitigated by the `SfzRoot_CachedOncePerContext` integration fact in `SfzConfigTests.cs`. The two "accept" threats remain accepted per the plan's threat model.

## Known Stubs

None. All shipped code paths produce real values or real errors. The 4 TBD GM symbols (`#choir`, `#guitar`, `#harpsichord`, `#celeste`) are intentional placeholders with clear composer-facing error messages — they are not "stubs that prevent the plan's goal." They represent the documented VSCO-CE 1.1.0 coverage gap per the Plan 33-01 audit.

## Next Phase Readiness

- **Plan 33-06 (SfzRenderer + SfzSampleCache)** already merged on the base ref — Plan 33-05's composer surface composes with it cleanly because both consume `SfzData` from the same `SfzParser.Parse` entry point.
- **Plan 33-07 (sampler:NAME dispatch + ExecutionContext.SfzPatchRegistry write hook)** can now:
  - Read `loadSfz` outputs as `Value.Sfz(SfzData)` through normal Flow type-checking (the type-system binding lands here in Plan 33-05).
  - Verify the `SfzBuiltins.Register(internalRegistry, _context)` line is present in `flow-lang/Core/FlowEngine.cs` (Plan 33-05 owns the insertion; Plan 33-07 only grep-verifies).
  - Add the `SamplerDispatch_WithoutImport_Errors` fact in `SfzBindingTests.cs` (the locked single-location ownership decision; Plan 33-05 deliberately did NOT add a stub for this).
- **Phase 34 (symphony showcase)** is the downstream consumer of the full Phase 33 surface.

No blockers.

## Self-Check: PASSED

Files-on-disk verification:

```
FOUND: flow-lang/sfz.flow
FOUND: flow-lang/StandardLibrary/Audio/Sfz/SfzBuiltins.cs
FOUND: flow-lang.Tests/Integration/Phase33/SfzGatingTests.cs
FOUND: flow-lang.Tests/Integration/Phase33/SfzSymbolLookupTests.cs
FOUND: flow-lang.Tests/Integration/Phase33/SfzConfigTests.cs
```

Commit verification (worktree-agent-a81d4d79a47fd23b6 branch):

```
FOUND: 37dfea0  feat(33-05): add @sfz stdlib module + SfzBuiltins skeleton (Task 1)
FOUND: 043d3a3  feat(33-05): loadSfz Symbol/String body + 11 integration tests (Task 2)
```

Test verification:
- `dotnet test --filter "FullyQualifiedName~Phase33.SfzGatingTests|FullyQualifiedName~Phase33.SfzSymbolLookupTests|FullyQualifiedName~Phase33.SfzConfigTests"` exits 0 — **Passed 11 / Failed 0** (137 ms).
- `dotnet test --filter "FullyQualifiedName~Phase33"` exits 0 — **48 / 48 green** (234 ms; 37 pre-existing + 11 new).
- `dotnet test --filter "FullyQualifiedName~Phase32|FullyQualifiedName~Phase26|FullyQualifiedName~Phase30"` exits 0 — **218 / 218 green** (no regression from the Parser / Lexer / Registry / std.flow changes).

---
*Phase: 33-sfz-orchestral-sampler*
*Completed: 2026-05-16*
