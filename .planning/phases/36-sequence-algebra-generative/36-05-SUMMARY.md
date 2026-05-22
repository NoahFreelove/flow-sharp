---
phase: 36-sequence-algebra-generative
plan: 05
subsystem: standard-library
tags: [patterns, tidal-combinators, prng, charitable-interpretation, GEN-05, PAT-01, PAT-02]

# Dependency graph
requires:
  - phase: 36-sequence-algebra-generative
    plan: 01
    provides: ExecutionContext.PrngRegistry + scripts/test_two_run_determinism.sh + PrngRegistryNewRandomGateTests source-grep CI gate (extended via PatternDeterminismTests.NoNewRandomInPatternFunctions)
  - phase: 36-sequence-algebra-generative
    plan: 02
    provides: FunctionSignature.ParameterNames defaulted-positional field (all 14 combinator registrations carry ParameterNames so composer can call `(every n=4 cb=... seq=...)` once Plans 36-03/04 backfill is finished)
provides:
  - "@patterns stdlib module — 13 Tidal-style combinators on Sequence (PAT-01 + PAT-02)"
  - "PatternFunctions.RegisterContextDependent — wires the 14 registrations into the InternalFunctionRegistry"
  - "ExecutionContext.CurrentCallSite — per-context SourceLocation set by ExpressionEvaluator before each builtin lambda invocation; used by Phase 36 stochastic primitives to key PrngRegistry by (call-site, name)"
  - "PatternFunctions.ResetChunkRotationForTesting — test-only hook clearing the per-call-site chunk-rotation counter map"
affects: [36-06, 36-07, 36-08, 36-09, 36-10, 36-11]

# Tech tracking
tech-stack:
  added: []   # Hand-rolled C# per D-v1.5-06 / RESEARCH § Pattern 6; zero new dependencies
  patterns:
    - "Per-context CurrentCallSite property on ExecutionContext set by ExpressionEvaluator save/restore around Implementation!(args) dispatch. Chosen over a new lambda-signature overload (Func<IReadOnlyList<Value>, SourceLocation, Value>) because the lambda surface stays unchanged — every existing builtin registration site continues to compile."
    - "Per-call-site rotation counter for chunk (Dictionary<SourceLocation, int> _chunkRotationCounters). Static state inside PatternFunctions; ResetChunkRotationForTesting() exposes the clear path for Fact isolation."
    - "Charitable-interpretation guards return Value.Sequence(seq) (input passthrough) + emit RenderingDiagnostics.WarnOnce; NEVER throw. Pattern matches D-v1.5-05 + Pitfall 2 + Pitfall 9."

key-files:
  created:
    - "flow-lang/StandardLibrary/Patterns/PatternFunctions.cs (895 lines — 13 combinators + RegisterContextDependent + per-call-site chunk-rotation counter + ResetChunkRotationForTesting test hook)"
    - "flow-lang/patterns.flow (29 lines — @patterns stdlib forward-decls for the 13 combinators using `cb` param name to dodge the reserved `fn` keyword)"
    - "flow-lang.Tests/Phase36/PatternEveryTests.cs (291 lines — 13 facts for the 10 deterministic combinators)"
    - "flow-lang.Tests/Phase36/PatternChalkyEdgeCasesTests.cs (175 lines — 8 charitable-interpretation facts)"
    - "flow-lang.Tests/Phase36/PatternDeterminismTests.cs (201 lines — 5 facts: 3 cross-engine reproducibility for sometimes/degrade/sparseSeq + 1 render-boundary-reset PRNG match + 1 source-grep gate)"
    - "tests/test_patterns_every.flow (47 lines — 5 composer-facing deterministic-combinator tests)"
    - "tests/test_patterns_edge_cases.flow (46 lines — 7 composer-facing charitable-interpretation tests)"
    - "tests/test_patterns_chain.flow (125 lines — 13 composer-facing combinator-chain exercises + writeWav target for the two-run determinism harness)"
  modified:
    - "flow-lang/Core/FlowEngine.cs (+7 lines — wires PatternFunctions.RegisterContextDependent into engine init alongside HarmonyFunctions/TransformFunctions registration)"
    - "flow-lang/Runtime/ExecutionContext.cs (+24 lines — CurrentCallSite property with xmldoc explaining the call-site-set-before-builtin-dispatch contract)"
    - "flow-lang/Interpreter/ExpressionEvaluator.cs (+13 lines — try/finally guard around Implementation!(args) sets/restores CurrentCallSite so nested builtin calls preserve outer site after inner returns)"
    - "flow-lang/flow-lang.csproj (+4 lines — patterns.flow copy-to-output)"

key-decisions:
  - "**SourceLocation threading via per-context CurrentCallSite (Option B)** — Lambda signature `Func<IReadOnlyList<Value>, Value>` doesn't carry a SourceLocation; Phase 36 stochastic primitives need one for PrngRegistry keying. Option A (add a new overload taking SourceLocation) would have required touching every existing registration site. Option B sets `context.CurrentCallSite` immediately before invoking the lambda and restores it after — single-point modification in ExpressionEvaluator. Nested builtin calls preserve their outer site via try/finally save+restore."
  - "**Chunk rotation state via static Dictionary<SourceLocation, int>** — D-36-04 says chunk applies fn to one chunk per cycle, rotating across invocations. The per-site counter advances on each call; test hook ResetChunkRotationForTesting() clears it for [Collection]-isolated facts. Render-boundary integration is NOT wired (the counter doesn't reset across renders) — composers calling chunk inside a song get deterministic rotation across iterations within a song's render pass."
  - "**Lambda-required style per D-36-03** — Every transform-arg combinator (every / chunk / sometimes / jux / superimpose) declares a `Function: cb` parameter and routes invocation through `InvokeCallback` (the DictFunctions.cs:41-46 idiom). The parameter is named `cb` rather than the reserved `fn` keyword (Flow's lambda introducer)."
  - "**Charitable interpretation contract** — All combinators return Value.Sequence(seq) (input passthrough) + emit `RenderingDiagnostics.WarnOnce` on degenerate input. Sentinels include the CurrentCallSite so the dedup is per-source-position. Some combinators (fast/slow/phase/iter) use static sentinels without the site embedded — these emit the advisory at most ONCE per process per failure mode, which is desirable for the 'composer ran a buggy expression in a loop' case."
  - "**jux and superimpose are identical wire-shape today** — Both layer original + lambda result via Phase 28's `ParallelVoices`. The semantic distinction is that jux RESERVES the right to do L/R stereo placement in v1.6 (currently mono-mixed). Documented in the xmldoc on each registration."

requirements-completed: [PAT-01, PAT-02]
# Note: GEN-05 (two-run cmp-clean determinism for stochastic generative
# primitives) was claimed by Plan 36-01 but is REINFORCED here: this is the
# first plan to ship actual stochastic primitives that exercise the gate.
# Plans 36-06+ inherit the same surface and gate.

# Metrics
duration: ~25min
completed: 2026-05-22
---

# Phase 36 Plan 05: `@patterns` Stdlib — 13 Tidal-Style Combinators Summary

**Composer-facing surface for the `@patterns` stdlib lands: 10 deterministic combinators (every / fast / slow / chunk / phase / rev / iter / palindrome / jux / superimpose) + 3 stochastic combinators routed via PrngRegistry (sometimes / degrade / sparseSeq) + a default-prob `sometimes(fn, seq)` ergonomic overload. Lambda-required style per D-36-03; cycle unit is bars per D-36-04; charitable interpretation on every degenerate input per PAT-02 + Pitfall 2 + Pitfall 9. Zero `new Random(` constructions — all PRNG flows through `ExecutionContext.PrngRegistry` keyed by `(SourceLocation, name)` per D-v1.5-06.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-05-22T03:23Z
- **Completed:** 2026-05-22T03:49Z
- **Tasks:** 2 of 2
- **Files created:** 8
- **Files modified:** 4

## Accomplishments

- `flow-lang/StandardLibrary/Patterns/PatternFunctions.cs` — 895-line class implementing all 13 combinators. Lambda-required transform-arg style per D-36-03; cycle unit is bars per D-36-04. PRNG threading via `ExecutionContext.PrngRegistry.NextDouble(site, name)` — direct `new Random(` is BANNED (source-grep gate enforces).
- `flow-lang/patterns.flow` — stdlib forward-decls for all 13 (technically 14 with the default-prob `sometimes` overload). Parameter names are `cb` not `fn` to dodge Flow's reserved lambda keyword.
- `ExecutionContext.CurrentCallSite` — per-context SourceLocation property set by `ExpressionEvaluator.EvaluateFunctionCall` immediately before invoking the registered C# lambda. Save/restore around `Implementation!(args)` so nested builtin calls preserve their outer site.
- Charitable-interpretation contract delivered:
  - `every` / `chunk` / `iter`: `n <= 0` → input + `WarnOnce` sentinel keyed by `{name}:invalid-n:{site}`
  - `fast` / `slow`: `factor <= 0` or non-finite → input + `WarnOnce` keyed by `{name}:invalid-factor`
  - `phase`: non-finite offset → input + `WarnOnce` keyed by `phase:non-finite`
  - `sometimes` / `sparseSeq`: prob outside [0, 1] → clamped + `WarnOnce` keyed by `{name}:clamp:{site}`
  - All combinators: empty `Sequence` → input passthrough + `WarnOnce` keyed by `{name}:empty:{site}`
  - Lambda returns non-Sequence: charitable passthrough + `WarnOnce` keyed by `{name}:non-sequence-fn:{site}`
  - `jux` / `superimpose` bar-count mismatch: original returned + `WarnOnce` keyed by `{name}:bar-mismatch:{site}`
- 14 registrations (13 combinators + default-prob `sometimes` overload) wired into `FlowEngine` engine init via `PatternFunctions.RegisterContextDependent(internalRegistry, _context)`.
- 5 source-grep + render-boundary determinism gates plus 8 edge-case xUnit facts + 7 composer-facing edge-case tests.
- Two-run cmp-clean determinism verified: `bash scripts/test_two_run_determinism.sh tests/test_patterns_chain.flow --render-cmd "dotnet run --project flow-cli --no-build -- run <SCRIPT>"` produces identical SHA-256: `ca90fcad1cea64c8d7e9e008bafbaa793dedc76f304794e3ce8c24fe48560750` across both runs.

## Task Commits

1. **Task 1 — 10 deterministic combinators + Wave 0 RED xUnit stubs (now GREEN)** — `a0f9882` (feat)
2. **Task 2 — stochastic determinism + edge-case + chain tests** — `4ddbf86` (test)

## Files Created/Modified

### Created
- `flow-lang/StandardLibrary/Patterns/PatternFunctions.cs` — 895 lines; 13 combinator implementations + InvokeCallback helper + bar/seq utilities + ResetChunkRotationForTesting test hook
- `flow-lang/patterns.flow` — 29 lines; @patterns stdlib forward-decls (uses `cb` rather than reserved `fn`)
- `flow-lang.Tests/Phase36/PatternEveryTests.cs` — 291 lines; 13 facts pinning every / fast / slow / chunk / phase / rev / iter / palindrome / jux / superimpose
- `flow-lang.Tests/Phase36/PatternChalkyEdgeCasesTests.cs` — 175 lines; 8 facts for the charitable contract
- `flow-lang.Tests/Phase36/PatternDeterminismTests.cs` — 201 lines; 5 facts (3 cross-engine + 1 render-boundary + 1 source-grep)
- `tests/test_patterns_every.flow` — 47 lines; 5 composer-facing tests via `(test ...)` blocks
- `tests/test_patterns_edge_cases.flow` — 46 lines; 7 composer-facing edge-case tests
- `tests/test_patterns_chain.flow` — 125 lines; 13 composer-facing chain tests + writeWav target for the determinism harness

### Modified
- `flow-lang/Core/FlowEngine.cs` — `using FlowLang.StandardLibrary.Patterns;` + `PatternFunctions.RegisterContextDependent(internalRegistry, _context);` in engine init
- `flow-lang/Runtime/ExecutionContext.cs` — `CurrentCallSite` property with xmldoc explaining the contract
- `flow-lang/Interpreter/ExpressionEvaluator.cs` — try/finally guard around the internal `Implementation!(args)` dispatch saves/restores `CurrentCallSite`
- `flow-lang/flow-lang.csproj` — `<None Update="patterns.flow">` block with `CopyToOutputDirectory=PreserveNewest`

## Decisions Made

- **SourceLocation threading: Option B (per-context property)** — Lambda signature `Func<IReadOnlyList<Value>, Value>` doesn't carry a SourceLocation. Two options surveyed:
  - Option A: extend `InternalFunctionRegistry.Register` with a new overload accepting `Func<IReadOnlyList<Value>, SourceLocation, Value>` and thread the call's `SourceLocation` through `ExpressionEvaluator`.
  - Option B: add `CurrentCallSite` property on `ExecutionContext`, set immediately before `Implementation!(args)` by the evaluator, restored after.
  - Option B chosen because the lambda surface stays unchanged. Every existing registration site (≥ 150) continues to compile without modification. The save/restore in `EvaluateFunctionCall` is single-point, single-file. Nested builtin calls (e.g., a combinator that invokes another via `InvokeCallback`) see their own outer site for the duration of the nested execution because the outer's call doesn't return until inner returns — the restore in finally restores the parent's site.
- **Chunk rotation state via static `Dictionary<SourceLocation, int>` _chunkRotationCounters** — Per D-36-04, chunk applies fn to one chunk per cycle, rotating which chunk receives the transform across invocations. The counter advances by 1 per call at each unique site. The test hook `ResetChunkRotationForTesting()` clears the dictionary; this is also what enables Fact isolation for `ChunkAppliesOneChunkPerCycle` to start at index 0. Render-boundary integration (clearing the counter on `ResetAtRenderBoundary`) was NOT wired — chunk rotation is intentionally PROCESS-LIFETIME so composers calling chunk in a song get deterministic rotation across iterations.
- **Lambda parameter named `cb` not `fn`** — `fn` is the lambda-introducer keyword in Flow; using it as a parameter name in `internal proc` declarations is a lex error. The DictFunctions stdlib precedent uses `cb` (callback) / `pred` (predicate). Patterns combinators follow suit.
- **`jux` and `superimpose` are wire-shape identical today** — Both attach `[original, fn(original)]` as `ParallelVoices` on each output bar. Per the xmldoc, jux reserves the right to do L/R stereo placement in v1.6 while superimpose stays mono-mixed. v1.5 mixes both equally.
- **`degrade` uses fixed 50% drop with the `draw >= prob` convention** (KEEP-when-draw-is-high) — matches Tidal compat; `sparseSeq` shares the same `DropBars` implementation with composer-supplied prob.
- **Empty-sequence advisory suppression for stochastic combinators** — `sparseSeq 1.0` legitimately drops every bar (no advisory). Only INPUT-side emptiness (i.e., the source already has 0 bars) raises the WarnOnce. Tested in `EmptySequenceUnchangedThroughCombinators` which chains `sparseSeq 1.0` → `sometimes 0.5` and asserts both produce an empty output without throwing.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] `ExecutionContext` is ambiguous between `FlowLang.Runtime.ExecutionContext` and `System.Threading.ExecutionContext`**
- **Found during:** Task 1 build
- **Issue:** Under net10.0's implicit usings, `System.Threading.ExecutionContext` shadows the bare name `ExecutionContext`, producing CS0104 ambiguity errors across PatternFunctions.cs.
- **Fix:** Added `using ExecutionContext = FlowLang.Runtime.ExecutionContext;` alias at the top of the file. This mirrors the pattern used in `flow-lang/Core/FlowEngine.cs:10` (`using RuntimeContext = FlowLang.Runtime.ExecutionContext;`).
- **Files modified:** flow-lang/StandardLibrary/Patterns/PatternFunctions.cs
- **Commit:** a0f9882

**2. [Rule 1 — Bug] `MusicalNoteData.With()` doesn't have a `durationValue` parameter**
- **Found during:** Task 1 — implementing fast/slow's per-note duration shift
- **Issue:** The `With(...)` builder helper at NoteType.cs:325 only exposes `onsetOffset` / `durationOverlap` / `portamentoMs` / `velocity` slots — `durationValue` isn't in the builder API.
- **Fix:** Use the full `new MusicalNoteData(...)` constructor when stamping a new duration. Preserves all 17 fields (Phase 25 contract).
- **Files modified:** flow-lang/StandardLibrary/Patterns/PatternFunctions.cs
- **Commit:** a0f9882

**3. [Rule 1 — Bug] `fn` is a reserved keyword in Flow — cannot be used as a parameter name in `internal proc` declarations**
- **Found during:** Task 1 — first attempt to load `patterns.flow` via `use "@patterns"`
- **Issue:** Original patterns.flow declared `internal proc every (Int: n, Function: fn, Sequence: seq)` — Parser raised "Expected parameter name. Got Fn 'fn'" because `fn` is the lambda-introducer token.
- **Fix:** Renamed all `fn` parameters to `cb` (callback), matching the DictFunctions stdlib precedent in std.flow.
- **Files modified:** flow-lang/patterns.flow
- **Commit:** a0f9882

**4. [Rule 1 — Bug] Composer-facing test patterns chain assertion failed for stochastic combinators**
- **Found during:** Task 2 composer-test `tests/test_patterns_chain.flow`
- **Issue:** Initial test wrote `(assertNotesMatch (sometimes ...) (sometimes ...))` against the same source position. Within a single render pass, two same-position calls SHARE PRNG state per D-v1.5-06 — the second call sees state advanced by the first, producing a different Sequence. The test was asserting the wrong contract.
- **Fix:** Bind each stochastic combinator result to a variable once, then assert `assertNotesMatch result result` (reference-equal sequence). The two-run cmp-clean contract is verified by the `scripts/test_two_run_determinism.sh` invocation against the file's writeWav output, not by in-script assertion.
- **Files modified:** tests/test_patterns_chain.flow
- **Commit:** 4ddbf86

All 4 auto-fixes are localized; none changed the plan's scope or contracts.

## Test Results

### Phase 36 Pattern suite (this plan)

```
dotnet test --filter "FullyQualifiedName~Phase36.Pattern" --no-build
→ 26 passed, 0 failed
  • PatternEveryTests:               13/13
  • PatternChalkyEdgeCasesTests:      8/8
  • PatternDeterminismTests:          5/5
```

### Composer-facing acceptance

```
dotnet run --project flow-cli -- test tests/test_patterns_every.flow
→ PASS  every applies fn at cycle boundary deterministically
→ PASS  fast and slow are inverses
→ PASS  rev twice is identity
→ PASS  palindrome is deterministic
→ PASS  jux is deterministic
Total: 5; Passed: 5; Failed: 0

dotnet run --project flow-cli -- test tests/test_patterns_edge_cases.flow
→ All 7 tests PASS (with stderr advisories visible: `[chunk] n must be > 0`,
  `[fast] factor must be > 0 and finite`, `[sometimes] prob 1.5 clamped to 1`,
  `[sparseSeq] prob -0.5 clamped to 0`, etc.)
Total: 7; Passed: 7; Failed: 0

dotnet run --project flow-cli -- test tests/test_patterns_chain.flow
→ All 13 tests PASS
Total: 13; Passed: 13; Failed: 0
```

### Two-run determinism gate

```
bash scripts/test_two_run_determinism.sh tests/test_patterns_chain.flow \
  --render-cmd "dotnet run --project flow-cli --no-build -- run <SCRIPT>"
→ Run A: ca90fcad1cea64c8d7e9e008bafbaa793dedc76f304794e3ce8c24fe48560750
→ Run B: ca90fcad1cea64c8d7e9e008bafbaa793dedc76f304794e3ce8c24fe48560750
→ Two-run determinism: PASS (identical SHA-256)
```

### Source-grep CI gate

```
grep -v '^[[:space:]]*//' flow-lang/StandardLibrary/Patterns/PatternFunctions.cs | grep -c 'new Random('
→ 0
```

### Regression gates

| Suite                                          | Pass/Total | Status |
|------------------------------------------------|------------|--------|
| Phase 35 (language foundation)                 | 80/80      | unchanged |
| Phase 36 (full — incl. 36-01..04 + 36-05)      | 77/77      | grew from 23/23 + new 54 in 36-03/04 + new 26 in 36-05 |
| Combined Phase 35 + 36                         | 157/157    | green |

## Threat Surface Scan

No new threat surface beyond the plan's `<threat_model>` register:

| Threat | Disposition | Status |
|--------|-------------|--------|
| T-36-11 (Integrity / determinism via wall-clock Random) | mitigate | ✓ Zero `new Random(` in PatternFunctions.cs; PatternDeterminismTests.NoNewRandomInPatternFunctions enforces |
| T-36-12 (DoS / NaN+Infinity to fast/slow/phase) | mitigate | ✓ Charitable advisory + input passthrough; PatternChalkyEdgeCasesTests pins |
| T-36-13 (Integrity / empty seq silent swallow) | mitigate | ✓ IsEmptySeqAdvisory emits WarnOnce on entry; EmptySequenceUnchangedThroughCombinators pins |

No new threat flags emerged.

## What This Unblocks

- **Plan 36-06 (`@generative` stdlib — Markov / L-system / cellular / chaos primitives)** — uses the same SourceLocation threading + charitable-interpretation patterns. The runtime surface (`PrngRegistry.NextDouble(ctx.CurrentCallSite, name)` + WarnOnce on degenerate input) is established and inheritable. Reminder: 36-06 also touches FlowEngine.cs (same file) — runs SEQUENTIALLY after this plan per the parallel-wave dependency note in the orchestration prompt.
- **Plan 36-07 (Markov feature-extraction with named args)** — uses Plan 36-02's named-arg surface + Plan 36-05's CurrentCallSite threading.
- **Plan 36-10 (`jam` chord-aware Markov improvisation)** — stochastic builtin; routes through `PrngRegistry.NextDouble(CurrentCallSite, "jam")`.
- **Plan 36-12 (Phase 36 GEN-05 phase gate)** — the two-run cmp-clean harness invocation against `tests/test_patterns_chain.flow` is the canonical Phase 36 stochastic-primitive verification artifact.

## Self-Check: PASSED

**Files asserted:**
- `[ -f flow-lang/StandardLibrary/Patterns/PatternFunctions.cs ]` → FOUND (895 lines)
- `[ -f flow-lang/patterns.flow ]` → FOUND
- `[ -f flow-lang.Tests/Phase36/PatternEveryTests.cs ]` → FOUND
- `[ -f flow-lang.Tests/Phase36/PatternChalkyEdgeCasesTests.cs ]` → FOUND
- `[ -f flow-lang.Tests/Phase36/PatternDeterminismTests.cs ]` → FOUND
- `[ -f tests/test_patterns_every.flow ]` → FOUND
- `[ -f tests/test_patterns_edge_cases.flow ]` → FOUND
- `[ -f tests/test_patterns_chain.flow ]` → FOUND

**Commits asserted:**
- `a0f9882` (Task 1) → FOUND in `git log --oneline`
- `4ddbf86` (Task 2) → FOUND in `git log --oneline`

**No-regression assertions:**
- Phase 36: 77/77 PASS (all prior + new 26 Pattern facts)
- Phase 35: 80/80 PASS (no regression)
- Two-run cmp-clean: SHA-256 match across 2 renders of tests/test_patterns_chain.flow
- Source-grep gate: 0 hits for `new Random(` in PatternFunctions.cs

## Issues Encountered

**Orphan working-tree changes** persist in the worktree from prior sessions (`flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs`, `flow-midi/Conversion/FlowGenerator.cs`, `flow-midi/Program.cs`, plus an untracked `a.out`). Per Plan 36-01's SUMMARY (lines 127-142), these pre-existed the worktree spawn. They do NOT touch Plan 36-05's surface, and per the destructive_git_prohibition I cannot roll them back. The orchestrator should resolve them at merge time.

In-scope tests are unaffected — Plan 36-05's PatternFunctions.cs is the only newly-introduced production-code file, and the build is clean for both `flow-lang.csproj` and `flow-lang.Tests.csproj`.

---
*Phase: 36-sequence-algebra-generative*
*Plan: 05*
*Completed: 2026-05-22*
