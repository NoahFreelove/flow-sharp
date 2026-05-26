---
phase: quick-srj-bundle-f
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - flow-lang/Runtime/ExecutionContext.cs
  - bench/results-bundle-f-*.txt
  - .planning/quick/260524-srj-bundle-f-overload-resolution-cache/260524-srj-SUMMARY.md
autonomous: true
requirements:
  - BUNDLE-F-CACHE  # overload resolution cache (per-ExecutionContext)
  - BUNDLE-F-INVAL  # invalidation on function (re)declaration
  - BUNDLE-F-BYPASS # named-args/varargs/Void cache bypass
  - BUNDLE-F-PROOF  # determinism + xUnit + bench parity proof

must_haves:
  truths:
    - "Two consecutive renders of granular.flow + tutorial.flow + test_humanize_voice_block.flow produce byte-identical WAV outputs"
    - "dotnet test flow-lang.Tests results match Bundle E baseline (1785 passed / 33 failed / 1 skipped / 1819 total) with ZERO new failures"
    - "REPL function redefinition test still passes — redefining `proc f` after the cache was populated for `(f Int)` resolves to the NEW overload"
    - "Phase 38 live-block reload tests still pass — fresh FlowEngine per render means cache lifecycle is naturally per-engine"
    - "Phase 43 module-namespace + qualified-import tests still pass — `use \"@x\"` invalidates cache via DeclareFunction chokepoint"
    - "Phase 36 PRNG-routing tests still pass — caching does not perturb PrngRegistry seeding"
    - "Calls using named-arg surface `(fn name=value)` bypass the cache (named-arg path's local rejection diagnostics depend on per-candidate state)"
    - "Calls where any candidate signature has `IsVarArgs=true` bypass the cache (Matches/CalculateSpecificity have arity-shifting semantics)"
    - "bench_overload.flow + bench_function_calls.flow show measurable improvement over Bundle E baseline (1.474s + 1.668s respectively)"
  artifacts:
    - path: "flow-lang/Runtime/ExecutionContext.cs"
      provides: "Per-context overload resolution cache (_overloadResolveCache + OverloadCacheKey struct + InvalidateOverloadCache wired into DeclareFunction + RestoreState)"
      contains: "_overloadResolveCache"
    - path: "bench/results-bundle-f-*.txt"
      provides: "Bench output captured via bash bench/run.sh --label bundle-f"
    - path: ".planning/quick/260524-srj-bundle-f-overload-resolution-cache/260524-srj-SUMMARY.md"
      provides: "Two tables (marginal vs Bundle E + cumulative vs original baseline) + invalidation audit recap + determinism proof"
  key_links:
    - from: "ExecutionContext.ResolveFunction"
      to: "_overloadResolveCache"
      via: "key build (name + argTypes) → TryGetValue → cache HIT short-circuits to cached FunctionOverload?"
      pattern: "_overloadResolveCache\\.TryGetValue"
    - from: "ExecutionContext.TryResolveFunction"
      to: "_overloadResolveCache"
      via: "same cache read; silent-mode probes share the cache"
      pattern: "_overloadResolveCache\\.TryGetValue"
    - from: "ExecutionContext.DeclareFunction"
      to: "InvalidateOverloadCache"
      via: "single chokepoint invalidation (full Clear) on every function declaration / redeclaration"
      pattern: "InvalidateOverloadCache"
    - from: "ExecutionContext.RestoreState (Phase 35 TEST-02)"
      to: "InvalidateOverloadCache"
      via: "defensive invalidation on hermetic-test restore boundary"
      pattern: "InvalidateOverloadCache"
---

<objective>
Bundle F — overload resolution cache. Today's `OverloadResolver.Resolve` (post-Bundle-A direct-FunctionOverload return) re-scores every call from scratch via `Matches` + `CalculateSpecificity`. For `(add Int Int)` in a tight loop, the same scoring work runs N times. Bundle F memoizes the resolution result on `ExecutionContext` keyed by `(name, argType[])` with full-cache-clear invalidation on any function (re)declaration.

Purpose: Eliminate redundant `Matches`/`CalculateSpecificity` work — the hottest scoring path in the interpreter. Expected to drop `bench_overload` + `bench_function_calls` significantly.
Output: `_overloadResolveCache` field + `OverloadCacheKey` struct + cache read in `ResolveFunction`/`TryResolveFunction` + invalidation wired into `DeclareFunction` and `RestoreState` + named-arg/varargs/Void bypass + bench/results-bundle-f + SUMMARY.md.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md
@.planning/STATE.md
@flow-lang/Runtime/ExecutionContext.cs
@flow-lang/TypeSystem/OverloadResolver.cs
@flow-lang/Runtime/StackFrame.cs
@flow-lang/Runtime/ModuleLoader.cs
@flow-lang/TypeSystem/FunctionSignature.cs
@flow-lang/TypeSystem/FlowType.cs

<interfaces>
<!-- Key types and contracts the executor needs. Extracted from codebase at planning time. -->
<!-- Executor should use these directly — no codebase exploration needed. -->

From flow-lang/TypeSystem/FlowType.cs:
- `public abstract class FlowType : IEquatable<FlowType>`
- `public virtual bool Equals(FlowType? other)` — default impl: `GetType() == other.GetType()` (so distinct concrete-type singletons compare by reference-equality of their CLR type identity)
- `public override int GetHashCode()` — default impl: `GetType().GetHashCode()`
- ArrayType overrides both (content-equal on ElementType).
- Every primitive/special type is a sealed singleton with `public static T Instance { get; } = new();` — e.g., `IntType.Instance`, `NoteType.Instance`, `CentType.Instance`, `VoidType.Instance`.

From flow-lang/Runtime/ExecutionContext.cs (current shape):
- `private readonly OverloadResolver _overloadResolver;`
- `public void DeclareFunction(FunctionOverload overload) { CurrentFrame.DeclareFunction(overload); }`  ← single chokepoint for ALL function declarations (Interpreter.cs:852 + 865 both call this; ModuleLoader executes procs through Interpreter so it flows through here too).
- `public FunctionOverload? ResolveFunction(string name, IReadOnlyList<FlowType> argTypes, Core.SourceLocation? location = null, IReadOnlyDictionary<string, FlowType>? namedArgTypes = null)`
- `public FunctionOverload? TryResolveFunction(string name, IReadOnlyList<FlowType> argTypes, IReadOnlyDictionary<string, FlowType>? namedArgTypes = null)`  ← silent probe; called by ExpressionEvaluator first, falls back to ResolveFunction on null.
- Bundle E precedent for invalidate-on-mutation chokepoint pattern: see `_cachedMusicalContext` + `InvalidateMusicalContextCache()` + `SetCurrentFrameMusicalContext` (ExecutionContext.cs:41, 564, 573).

From flow-lang/TypeSystem/OverloadResolver.cs:
- `public FunctionOverload? Resolve(string functionName, IReadOnlyList<FunctionOverload> candidates, IReadOnlyList<FlowType> positionalArgTypes, Core.SourceLocation? location = null, IReadOnlyDictionary<string, FlowType>? namedArgTypes = null, bool silent = false)` — Bundle A direct-FunctionOverload entry point.
- Named-arg branch (when `namedArgTypes is { Count: > 0 }`) walks candidates and emits per-candidate rejection diagnostics into a lazy `localReporter`. This branch MUST bypass the cache — its diagnostics depend on per-candidate state we'd be skipping.

From flow-lang/TypeSystem/FunctionSignature.cs:
- `public bool Matches(IReadOnlyList<FlowType> argTypes)` — IsVarArgs branch has arity-shifting semantics (accepts N >= InputTypes.Count-1).
- VarArgs candidates MUST bypass the cache because the SAME (name, argTypes) tuple can resolve differently when a new fixed-arity overload is registered alongside a varargs one — and our cache key doesn't track candidate-set identity.
</interfaces>

<invalidation_surface_audit>
Performed at planning time. All mutation sites for `StackFrame._functions` enumerated below. Single chokepoint identified.

| Site | File:Line | Action | Cache action |
|------|-----------|--------|--------------|
| `StackFrame.DeclareFunction` | flow-lang/Runtime/StackFrame.cs:129 | Adds new overload OR replaces existing-signature overload at index (REPL redefinition path) | **Caught by chokepoint** (only called via ExecutionContext wrapper) |
| `ExecutionContext.DeclareFunction` | flow-lang/Runtime/ExecutionContext.cs:453 | Single chokepoint — wraps StackFrame.DeclareFunction | **INVALIDATE HERE** |
| `Interpreter.ExecuteProcDeclaration` | flow-lang/Interpreter/Interpreter.cs:852, 865 | Calls `_context.DeclareFunction(overload)` | Caught by chokepoint |
| `ModuleLoader.LoadModule` | flow-lang/Runtime/ModuleLoader.cs:117 | Calls `interpreter.Execute(program)` → runs `ExecuteProcDeclaration` for each `proc` in the module → `DeclareFunction` chokepoint | Caught transitively |
| `LiveReloadManager.RenderScript` | flow-interpreter/LiveReloadManager.cs:857 | **Creates fresh `FlowEngine` per render** (`using var engine = new FlowEngine()`). Each engine has its own `ExecutionContext` with its own `_overloadResolveCache`. The OLD engine is disposed; the NEW engine starts with an empty cache. | **NO ACTION NEEDED** — per-engine cache lifecycle handles it. |
| `LiveReloadManager.StagePendingBuffers` | flow-interpreter/LiveReloadManager.cs:595 | Operates on `newBuffers` from the fresh engine; does not mutate ANY existing ExecutionContext's `_functions` | **NO ACTION NEEDED** |
| `ExecutionContext.RestoreState` (Phase 35 TEST-02) | flow-lang/Runtime/ExecutionContext.cs:836 | Restores `GlobalFrame` variables + many singletons but does NOT touch `_functions` directly (TestSnapshot has no function-overload field per current SnapshotState). However, between `SnapshotState` and `RestoreState` the test body may call `DeclareFunction` which IS caught by the chokepoint. The chokepoint already invalidated on that call, so technically a no-op here. | **DEFENSIVE INVALIDATE** — costs one Clear() per test restoration, gives belt-and-suspenders safety against future SnapshotState changes |

**Audit conclusion:** Single chokepoint at `ExecutionContext.DeclareFunction` + defensive invalidation at `ExecutionContext.RestoreState` covers 100% of the mutation surface. Full-cache `Clear()` is the correct invalidation strategy:

1. Function (re)declarations are NOT in the hot inner loop (they happen at parse-time + module-load-time + REPL turn boundary). The cost of one `Dictionary.Clear()` per declaration is negligible.
2. Surgical per-name invalidation buys nothing: a new overload for `add` only invalidates cache entries with name=`add`, but `Dictionary.Clear()` is O(buckets) anyway.
3. The cache rebuilds in <100 calls during the next hot loop — measured by Bundle E precedent (musical-context cache: full Clear() per frame push/pop, no measurable bench regression).

**No invalidation needed for:** LiveReloadManager paths (fresh engine), PRNG state changes, SfzRegistry mutations, MusicalContext changes (those affect `GetMusicalContext()`, not function resolution).
</invalidation_surface_audit>

<tasks>

<task type="auto">
  <name>Task 1: Implement overload cache + invalidation + named-args/varargs/Void bypass</name>
  <files>flow-lang/Runtime/ExecutionContext.cs</files>
  <action>
Edit ExecutionContext.cs ONLY. All work confined to this single file per LOCKED constraint.

**Add (near `_overloadResolver` field at line 16, mirroring Bundle E's `_cachedMusicalContext` doc-comment posture):**

1. **Nested struct `OverloadCacheKey`** as a private nested type inside `ExecutionContext`:
   - Fields: `string Name`, `FlowType[] ArgTypes` (array, not List — size known at construction).
   - Implements `IEquatable<OverloadCacheKey>`.
   - Equality: `Name == other.Name && ArgTypes.Length == other.ArgTypes.Length && all argTypes[i].Equals(other.ArgTypes[i])`. Per the interfaces block above, every concrete `FlowType` is a sealed singleton; `Equals` for them collapses to reference-equality on the CLR type. ArrayType compares content-equal on ElementType.
   - GetHashCode: start with `Name.GetHashCode()`, then XOR-roll each `ArgTypes[i].GetHashCode()` with a shift-and-rotate to avoid symmetric collisions on equal-name same-multiset arg lists. Use `HashCode.Combine` or `unchecked { ... hash = hash * 31 + argType.GetHashCode(); }` — both are fine.

2. **Field**: `private readonly Dictionary<OverloadCacheKey, FunctionOverload?> _overloadResolveCache = new();`
   - Nullable value type: caches "no match" too (avoids re-paying resolution cost on known-misses such as repeat lookups for procs not yet declared).
   - Per-context lifecycle — initialized at construction, mirrors `_overloadResolver`. NOT static.

3. **Method**: `private void InvalidateOverloadCache() => _overloadResolveCache.Clear();`
   - Mirrors `InvalidateMusicalContextCache` doc-comment shape from Bundle E.

4. **Modify `ResolveFunction` (currently at line 468)** to add cached-read at the top, AFTER the `overloads.Count == 0` zero-overloads-found guard (so we still report "Function 'X' not found" errors).
   - Build a `BypassCache` predicate inline:
     - `namedArgTypes is { Count: > 0 }` → bypass
     - `argTypes` is non-empty AND any `argType is VoidType` (unresolved/wildcard) → bypass
     - any overload in the candidate list has `Signature.IsVarArgs == true` → bypass (probe the local `overloads` list)
   - If not bypassed:
     - Build `var key = new OverloadCacheKey(name, argTypes.ToArray())` — single allocation for the array snapshot.
     - `if (_overloadResolveCache.TryGetValue(key, out var cached)) return cached;`
     - Call `_overloadResolver.Resolve(...)` as today, store result: `_overloadResolveCache[key] = result; return result;`
   - If bypassed: call resolver directly without caching (preserves current behavior byte-identical).

5. **Modify `TryResolveFunction` (currently at line 729)** identically — same cached-read gate, same bypass rules, same `silent: true` passthrough on cache miss. Note: the silent probe SHARES the same cache as the non-silent path. This is correct: the cached `FunctionOverload?` value is the resolution outcome and doesn't depend on whether the original call was silent or noisy; only the diagnostics differ, and a cache HIT skips diagnostics entirely (which is the SAME behavior as `silent=true`).

6. **Wire invalidation in `DeclareFunction` (currently at line 453)**:
   ```
   public void DeclareFunction(FunctionOverload overload)
   {
       CurrentFrame.DeclareFunction(overload);
       InvalidateOverloadCache();
   }
   ```

7. **Wire invalidation in `RestoreState` (currently at line 836)**: add `InvalidateOverloadCache();` near the existing `InvalidateMusicalContextCache();` call (around line 874). Add a brief inline comment explaining "Bundle F (260524-srj) — defensive invalidation; the chokepoint at DeclareFunction already covered any in-test redefinitions, but pin this in case SnapshotState/RestoreState ever gain a `_functions`-restoring field."

8. **Doc comments**: every new member (struct, field, method, modifications) MUST have an XML `<summary>` doc-comment using the Bundle E (260524-sa3) precedent format. Reference "Bundle F (260524-srj)" + cite the `<invalidation_surface_audit>` block in this PLAN.md by name. Don't repeat the whole audit — point at the plan.

**Constraints reminder (LOCKED, do not violate):**
- Edits confined to `flow-lang/Runtime/ExecutionContext.cs`. NO other source files (per task_scope LOCKED constraint).
- Observable behavior IDENTICAL — cache must be invisible to every test, every script, every two-run cmp comparison.
- Don't touch CLAUDE.md.
- Don't touch `StackFrame.cs`, `ModuleLoader.cs`, `Interpreter.cs`, `LiveReloadManager.cs` — the audit proved no invalidation hook is needed there.
  </action>
  <verify>
    <automated>dotnet build -c Release flow-sharp.sln 2>&amp;1 | tail -10 | grep -E "Build succeeded|Build FAILED|error"</automated>
  </verify>
  <done>
- ExecutionContext.cs compiles in Release with zero warnings introduced.
- `_overloadResolveCache` field exists with XML doc-comment citing the audit block.
- `OverloadCacheKey` nested struct implements `IEquatable&lt;OverloadCacheKey&gt;` + overrides `GetHashCode`.
- `ResolveFunction` and `TryResolveFunction` both consult the cache; both bypass for named-args / varargs-candidate / VoidType-arg cases.
- `DeclareFunction` invalidates the cache on every call.
- `RestoreState` invalidates the cache defensively alongside the existing musical-context invalidation.
- No other source files modified.
  </done>
</task>

<task type="auto">
  <name>Task 2: xUnit + two-run determinism + bench + SUMMARY</name>
  <files>bench/results-bundle-f-*.txt, .planning/quick/260524-srj-bundle-f-overload-resolution-cache/260524-srj-SUMMARY.md</files>
  <action>
Execute the validation gates IN ORDER. Stop on first failure and diagnose before retrying. Do NOT proceed to bench if xUnit regresses; do NOT write SUMMARY if determinism fails.

**Gate 1 — xUnit parity vs Bundle E baseline.**
Run `dotnet test flow-lang.Tests -c Release 2>&amp;1 | tail -20` and verify the result line.

Bundle E baseline: **1785 passed / 33 failed / 1 skipped / 1819 total**.

Required: same counts. ZERO new failures. The 33 failures are the documented pre-existing baseline (Phase 28/29/35/38 FFT + Piano + Ragtime RMS + FlowTestCli + MatchExhaustivenessDefault).

PAY SPECIAL ATTENTION (cross-check these test classes individually if numeric result diverges):
- REPL function-redefinition tests — verify the cache invalidates on every `DeclareFunction`. Run `dotnet test flow-lang.Tests -c Release --filter "FullyQualifiedName~Repl" 2>&amp;1 | tail -5`.
- Phase 43 module-namespace + qualified-import tests — verify `use "@x"` flows through the chokepoint. Run `dotnet test flow-lang.Tests -c Release --filter "FullyQualifiedName~Module" 2>&amp;1 | tail -5`.
- Phase 38 live-block reload tests — verify per-engine cache lifecycle holds. Run `dotnet test flow-lang.Tests -c Release --filter "FullyQualifiedName~Live" 2>&amp;1 | tail -5`.
- Phase 36 PRNG-routing tests — verify caching does not perturb PrngRegistry seeding. Run `dotnet test flow-lang.Tests -c Release --filter "FullyQualifiedName~Prng" 2>&amp;1 | tail -5`.

**Gate 2 — two-run determinism cmp-clean.**
Per Bundle E precedent (test_humanize_voice_block.flow substituted for any tests/script that doesn't produce a stable WAV):

```bash
# granular.flow
dotnet run --project flow-interpreter -c Release -- examples/dsp/granular.flow
mv output.wav /tmp/granular-run1.wav 2>/dev/null || cp examples/dsp/*.wav /tmp/granular-run1.wav
# (use whatever produced output path; cmp the bytes)
dotnet run --project flow-interpreter -c Release -- examples/dsp/granular.flow
# cmp the two
```

Concretely, run each script twice in sequence; cmp the resulting WAV bytes. All three MUST be byte-identical between run 1 and run 2:
- `examples/dsp/granular.flow`
- `examples/tutorial.flow`
- `tests/test_humanize_voice_block.flow`

If the script writes to a fixed path (e.g., `output.wav` in CWD), copy the run-1 output aside before running it the second time. If the script writes a uniquely-named WAV per run, capture both and cmp directly.

`cmp` exit code 0 on all three pairs = PASS. Any non-zero = STOP and diagnose (likely a cache key that's missing a discriminator — most-likely-suspect is VoidType bypass being incomplete).

**Gate 3 — bench harness.**
```bash
bash bench/run.sh --label bundle-f
```
Output lands at `bench/results-bundle-f-<timestamp>.txt`. Compare against `bench/results-bundle-e-20260524-203614.txt` (Bundle E baseline):

| Script | Bundle E (Mean s) | Expected Bundle F |
|--------|------------------:|-------------------|
| bench_overload.flow | 1.474 | **DROP MOST** (this is the targeted hot path) |
| bench_function_calls.flow | 1.668 | **DROP** (function calls dispatch through ResolveFunction) |
| bench_var_lookup.flow | 3.218 | Neutral (Bundle B path, not this bundle) |
| bench_collections.flow | 2.262 | Slight drop possible (collections use builtins) |
| bench_notestream.flow | 1.290 | Neutral |
| bench_parse.flow | 1.440 | Neutral (parse path, not resolve path) |

**Gate 4 — SUMMARY.md.**
Write `.planning/quick/260524-srj-bundle-f-overload-resolution-cache/260524-srj-SUMMARY.md` with:

1. **Path chosen** statement: "FULL CACHE per ExecutionContext with single-chokepoint invalidation."
2. **Invalidation audit recap**: copy the audit table from the plan + one-sentence verdict for each row.
3. **Marginal-vs-Bundle-E table** (sourced from bench/results-bundle-f vs bench/results-bundle-e-20260524-203614.txt):

   ```markdown
   | Script | Bundle E (Mean s) | Bundle F (Mean s) | Δ (s) | Δ (%) |
   |--------|------------------:|------------------:|------:|------:|
   | bench_overload.flow | 1.474 | X.XXX | -Y.YYY | -ZZ% |
   ...
   ```

4. **Cumulative-vs-original-baseline table** (sourced from bench/results-bundle-f vs bench/results-baseline-20260524-192612.txt):

   ```markdown
   | Script | Baseline (Mean s) | Bundle F (Mean s) | Δ (s) | Δ (%) |
   |--------|------------------:|------------------:|------:|------:|
   ...
   ```

5. **Determinism proof**: cmp output for all 3 paired runs (granular / tutorial / test_humanize_voice_block).
6. **xUnit result**: pinned count from Gate 1 + statement "ZERO new failures vs Bundle E baseline 1785/33/1/1819".
7. **Files modified**: `flow-lang/Runtime/ExecutionContext.cs` (only).
8. **Bench expectations met / surprises**: brief commentary on whether bench_overload + bench_function_calls dropped as expected; flag any surprise regressions in the other 4 scripts.
9. **STATE.md follow-up note**: one paragraph for the user to paste into the next STATE.md update — "Completed quick task 260524-srj: Bundle F overload resolution cache (per-ExecutionContext memoization with single-chokepoint invalidation at DeclareFunction; bench_overload + bench_function_calls drops; cumulative A-E wins preserved)".
  </action>
  <verify>
    <automated>test -f bench/results-bundle-f-*.txt &amp;&amp; test -f .planning/quick/260524-srj-bundle-f-overload-resolution-cache/260524-srj-SUMMARY.md &amp;&amp; grep -q "Bundle F" .planning/quick/260524-srj-bundle-f-overload-resolution-cache/260524-srj-SUMMARY.md &amp;&amp; grep -q "FULL CACHE" .planning/quick/260524-srj-bundle-f-overload-resolution-cache/260524-srj-SUMMARY.md</automated>
  </verify>
  <done>
- xUnit suite: 1785 passed / 33 failed / 1 skipped / 1819 total (Bundle E parity, zero new failures).
- Two-run cmp-clean: 3/3 paired runs (granular / tutorial / test_humanize_voice_block) byte-identical.
- bench/results-bundle-f-*.txt exists with all 6 bench scripts measured.
- SUMMARY.md exists with: PATH-CHOSEN statement + invalidation audit recap + marginal table vs Bundle E + cumulative table vs original baseline + determinism proof + xUnit count + files modified + bench expectations + STATE.md follow-up paragraph.
- bench_overload + bench_function_calls show measurable improvement (the targeted hot paths).
  </done>
</task>

</tasks>

<verification>
End-of-phase check (run via `dotnet test`, `cmp`, and `bash bench/run.sh`):

1. **Build clean Release** — `dotnet build -c Release flow-sharp.sln` exits 0 with zero new warnings.
2. **xUnit parity** — `dotnet test flow-lang.Tests -c Release` returns 1785 passed / 33 failed / 1 skipped / 1819 total (Bundle E baseline).
3. **Two-run determinism** — `cmp` returns exit code 0 for granular.flow + tutorial.flow + test_humanize_voice_block.flow paired runs.
4. **Bench captured** — `bench/results-bundle-f-<timestamp>.txt` exists, all 6 scripts measured, bench_overload + bench_function_calls drop vs Bundle E baseline.
5. **SUMMARY.md complete** — every section listed in Task 2 Gate 4 is present.
6. **Scope discipline** — `git diff --stat` shows ONLY `flow-lang/Runtime/ExecutionContext.cs` + `bench/` + `.planning/quick/260524-srj-bundle-f-overload-resolution-cache/` modified.
</verification>

<success_criteria>
Bundle F SHIPPED when:
- [ ] Per-context overload resolution cache landed on ExecutionContext (single file edit).
- [ ] Cache invalidation chokepoint at `DeclareFunction` covers 100% of mutation surface per the invalidation audit.
- [ ] Defensive invalidation at `RestoreState` for hermetic-test safety.
- [ ] Named-args / varargs-candidate / VoidType-arg calls bypass the cache (correctness gate).
- [ ] Per-engine cache lifecycle naturally handles live-block reload (no LiveReloadManager edits needed — audited).
- [ ] xUnit parity vs Bundle E baseline (1785/33/1/1819) — zero new failures.
- [ ] Two-run cmp-clean on 3 representative scripts.
- [ ] bench/results-bundle-f-*.txt captured with measurable drops on bench_overload + bench_function_calls.
- [ ] SUMMARY.md committed with marginal + cumulative tables + audit recap + determinism proof + STATE.md follow-up paragraph.
</success_criteria>

<output>
Create `.planning/quick/260524-srj-bundle-f-overload-resolution-cache/260524-srj-SUMMARY.md` when done (covered by Task 2 Gate 4).
</output>
