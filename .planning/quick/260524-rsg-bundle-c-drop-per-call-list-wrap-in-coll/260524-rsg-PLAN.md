---
task: 260524-rsg-bundle-c-drop-per-call-list-wrap-in-coll
mode: quick
type: execute
files_modified:
  - flow-lang/StandardLibrary/Collections.cs
autonomous: true
risk: low
---

<objective>
Bundle C: eliminate per-iteration `new List<Value> { element }` allocations in
the four collection higher-order builtins (`map`, `filter`, `each`, `reduce`).

A `(map arr fn)` over 10k elements currently creates 10k throwaway single-element
`List<Value>` instances. `reduce` creates 10k throwaway two-element lists. Bundle C
hoists ONE reusable buffer per call out of the loop and mutates it in place each
iteration via the indexer.

Purpose: continue the Bundle A/B compounding optimization series. Targets the
collections benchmark (`bench_collections.flow`, currently 2.182s post-Bundle-B
vs. 2.692s baseline = -19% cumulative; Bundle C should drop further since this
benchmark is allocation-dominated on those 4 builtins).

Output: smaller cumulative bench_collections wall-clock; zero observable
behavior change; full test suite GREEN at the Bundle B baseline (1785 pass /
33 fail / 1 skip / 1819 total — those 33 are documented Phase 28/29/35
deferred items, not Bundle-C-caused).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md
@flow-lang/StandardLibrary/Collections.cs

<safety_analysis>
**Strategy A precondition VERIFIED by planner (do not re-derive).**

The args list passed to `InvokeCallback` flows through two paths:

1. **Internal builtins** (`callback.IsInternal == true`, line 324):
   `callback.Implementation!(args)` — `Implementation` has signature
   `Func<IReadOnlyList<Value>, Value>` (see InternalFunctionRegistry). The
   builtin extracts what it needs from `args[0]`, `args[1]`, etc. during
   the synchronous call and returns a `Value`. It does NOT retain the
   list reference past return. All existing built-ins already follow this
   discipline (otherwise multi-arg builtins would be broken under the
   existing call sites that already pass shared `IReadOnlyList<Value>`).

2. **User-defined functions** (`callback.IsInternal == false`, line 325):
   `context.Invoker!.ExecuteUserFunctionWithCaptures(decl, args, captures)`
   in `Interpreter.cs:1108-1206`. The implementation:
   - Iterates `args` by index ONLY at lines 1135-1170 (parameter binding loop).
   - Extracts each `Value` via `args[i]` / `args[j]` and passes it to
     `DeclareVariable(name, value)` / `SetVariable(name, value)` — these
     store the EXTRACTED VALUE in the stack frame, NOT a reference to the
     args list.
   - After parameter binding completes (line 1171), `args` is never touched
     again before return.
   - The VarArgs branch at 1140-1160 IS a partial concern — when a param is
     IsVarArgs, line 1151 creates a NEW `List<Value>` (`varArgs`) and copies
     remaining args into it, then wraps that in a `Value.Array`. So even
     VarArgs binding copies-out before storing, never aliasing the input.

Conclusion: `args` is read-only-consumed within the synchronous call. A
caller-owned reusable buffer is safe — by the time control returns to the
caller's `for`-loop body, every read of `args[0]`/`args[1]` has already
been resolved into stack-frame values. Mutating `buffer[0] = nextElement`
for the next iteration cannot corrupt previously-bound parameters in any
recursive call.

**Choice of buffer type:** `InvokeCallback`'s parameter is `List<Value>`
(line 322). Two paths forward:

- **(Chosen)** Relax the parameter to `IReadOnlyList<Value>` — `List<Value>`
  already implements `IReadOnlyList<Value>`. This matches the downstream
  shape exactly (`Func<IReadOnlyList<Value>, Value>` for builtins;
  `ExecuteUserFunctionWithCaptures` takes `IReadOnlyList<Value>`). Allows
  any backing buffer (List, array, Span-backed wrapper) at call sites
  without further plumbing. `InvokeCallback` is private to Collections.cs,
  so this is a strictly local change.

- (Rejected) Keep `List<Value>` parameter, mutate via indexer. Works, but
  forces all four call sites to allocate a `List<Value>` even though the
  contract downstream only needs `IReadOnlyList<Value>`.

**Final shape per call site:**

- `Each`: single `Value[1]` buffer hoisted outside the foreach; `buffer[0] = element` each iteration.
- `Map`: same as Each plus `results.Add(...)`.
- `Filter`: same as Each plus conditional `results.Add(element)`.
- `Reduce`: single `Value[2]` buffer hoisted outside the foreach;
  `buffer[0] = accumulator; buffer[1] = element` each iteration.

`Value[]` is preferred over `List<Value>` for the buffer because:
- It satisfies `IReadOnlyList<Value>` (arrays implement `IList<T>` which extends `IReadOnlyList<T>`).
- It has no internal `_items`/`_size` growable backing — strictly N references plus header.
- Indexer assignment is a single MOV; no `_version++` mutation tracking like List.

**Empty-array edge case:** All four sites have `foreach (var element in arr)`
loops. When `arr` is empty, the buffer is allocated but never touched —
single allocation of `Value[1]` or `Value[2]`, freed at end-of-scope. Net
wash vs. status quo (which allocates nothing). Acceptable.

**Determinism / two-run cmp-clean:** No PRNG touched. No iteration-order
change. No type-resolution change. The `Map` elementType inference loop
(lines 355-357) is unchanged. The `Filter` elementType (line 378) is
unchanged. Pure allocation-shape optimization.
</safety_analysis>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Hoist reusable Value[] buffer across all 4 callback sites</name>
  <files>flow-lang/StandardLibrary/Collections.cs</files>
  <action>
Edit `flow-lang/StandardLibrary/Collections.cs`:

(1) Relax `InvokeCallback` parameter type at line 322 from
    `List<Value> args` to `IReadOnlyList<Value> args`. The body is
    unchanged — `Implementation!(args)` accepts `IReadOnlyList<Value>`
    (its actual delegate type), and `ExecuteUserFunctionWithCaptures`
    already takes `IReadOnlyList<Value>`. No other Collections.cs site
    calls `InvokeCallback`; verified by grep at planning time.

(2) `Each` (currently line 333-336): hoist `var buffer = new Value[1];`
    BEFORE the foreach. Inside the loop, replace
    `new List<Value> { element }` with `buffer[0] = element;` on its
    own statement followed by `InvokeCallback(context, callback, buffer);`.

(3) `Map` (currently line 347-350): same pattern as Each. Hoist
    `var buffer = new Value[1];` BEFORE the foreach. Inside, set
    `buffer[0] = element;` then `results.Add(InvokeCallback(context, callback, buffer));`.

(4) `Filter` (currently line 368-373): same pattern as Each. Hoist
    `var buffer = new Value[1];` BEFORE the foreach. Inside, set
    `buffer[0] = element;` then capture `var result = InvokeCallback(context, callback, buffer);`
    and the existing `if (result.As<bool>()) results.Add(element);` is unchanged.

(5) `Reduce` (currently line 389-392): hoist `var buffer = new Value[2];`
    BEFORE the foreach. Inside, set `buffer[0] = accumulator;` then
    `buffer[1] = element;` then `accumulator = InvokeCallback(context, callback, buffer);`.

(6) Do NOT touch the unrelated `new List<Value> { element }` site at
    line 282 in `Prepend` — that one is a return-value being captured into a
    `Value.Array`, not a per-iteration alloc. Out of scope for Bundle C.

(7) Add a one-line `// Bundle C: reused per-call buffer (planner-verified safe;
    args read-only-consumed by InvokeCallback path — see PLAN safety_analysis)`
    comment immediately before each of the 4 hoisted `var buffer = ...` lines.
    Future maintainers MUST NOT add code that retains the args reference
    past callback return without first reverting Bundle C or proving the
    new code path also satisfies the precondition.

(8) Do NOT touch any other file. `flow-lang/Interpreter/Interpreter.cs`
    requires no edits — `ExecuteUserFunctionWithCaptures` already accepts
    `IReadOnlyList<Value>`, satisfied by `Value[]` and `List<Value>` alike.

Build clean (`dotnet build -c Release` from repo root) before handing off
to Task 2.
  </action>
  <verify>
    <automated>cd /home/noah/Desktop/projects/flow-sharp &amp;&amp; dotnet build -c Release 2>&amp;1 | tail -20 &amp;&amp; grep -c "new List&lt;Value&gt; {" flow-lang/StandardLibrary/Collections.cs</automated>
  </verify>
  <done>
    - Build succeeds, zero warnings introduced.
    - `grep -c 'new List&lt;Value&gt; {' flow-lang/StandardLibrary/Collections.cs` returns 1
      (the single remaining occurrence is the `Prepend` site at line 282, intentionally untouched).
    - `grep -c 'var buffer = new Value\[' flow-lang/StandardLibrary/Collections.cs` returns 4.
    - `InvokeCallback` signature shows `IReadOnlyList&lt;Value&gt; args`.
  </done>
</task>

<task type="auto">
  <name>Task 2: Verify tests + bench + write SUMMARY with marginal+cumulative tables</name>
  <files>.planning/quick/260524-rsg-bundle-c-drop-per-call-list-wrap-in-coll/260524-rsg-SUMMARY.md</files>
  <action>
Run verification gates in order. Stop and report on first hard-fail.

(A) **Test suite** — from repo root:
```
dotnet test flow-lang.Tests -c Release --nologo --verbosity minimal
```
Expected baseline (from Bundle B at git rev 8a00263):
  - Total: 1819
  - Passed: 1785
  - Failed: 33  (all documented Phase 28/29/35 deferred items; see
    `.planning/phases/42-type-system-stdlib-audit/deferred-items.md`)
  - Skipped: 1

Bundle C MUST match exactly. ANY new failure is a hard-fail — Bundle C
touches the lambda/callback hot path; any test regression points at a
broken aliasing assumption.

Pay specific attention to:
  - `flow-lang.Tests/StandardLibrary/MapFilterReduceTests.*` (or equivalent;
    find via `find flow-lang.Tests -iname '*.cs' | xargs grep -l 'map\|filter\|reduce\|each' | head`)
  - Lambda/closure tests (capture-variable tests are the most likely
    failure mode if Strategy A precondition is wrong somewhere — none
    expected based on planner analysis).

(B) **Composer .flow scripts** — run the standard test sweep:
```
for test in tests/test_*.flow; do
    dotnet run --project flow-interpreter -c Release "$test" > /dev/null 2>&amp;1 || echo "FAIL: $test"
done
```
4 expected non-zero-exit scripts (unchanged from Phase 43 baseline):
`test_dict_type_errors.flow`, `test_error_masking.flow`,
`test_iteration_guard.flow`, `test_musical_context_errors.flow`. Any
NEW failure here is a hard-fail.

(C) **Bench run** — from repo root:
```
bash bench/run.sh --label bundle-c
```
Wait for completion. The script emits `bench/results-bundle-c-<timestamp>.txt`.

(D) **Write SUMMARY.md** at the path
`.planning/quick/260524-rsg-bundle-c-drop-per-call-list-wrap-in-coll/260524-rsg-SUMMARY.md`
with the standard quick-mode summary template PLUS a load-bearing
"## Benchmark Results (Bundle C)" section containing TWO markdown tables:

  **Table 1 — Marginal vs Bundle B**
  Columns: Script | Bundle B Mean (s) | Bundle C Mean (s) | Δ (s) | Δ %.
  Source for Bundle B column: `bench/results-bundle-b-20260524-195548.txt`.
  Source for Bundle C column: the new `bench/results-bundle-c-*.txt` file.
  Compute Δ % as `(C-B)/B * 100`, negative = faster. One row per script,
  six rows total (bench_collections / bench_function_calls / bench_notestream /
  bench_overload / bench_parse / bench_var_lookup).

  **Table 2 — Cumulative vs Original Baseline**
  Columns: Script | Baseline Mean (s) | Bundle C Mean (s) | Δ (s) | Δ %.
  Source for baseline column: `bench/baseline.txt`.

Below the tables, add a 1-2 sentence interpretation. Expected outcome
(stated upfront so it's verifiable): `bench_collections` shows the
largest marginal drop, since that benchmark drives 10k iterations × 20
outer reps × 4 callback sites = ~800k eliminated list allocations per
run. Other benchmarks should show noise-level Δ (no callback hot path).

If `bench_collections` does NOT show a marginal improvement (Δ ≤ 0 with
absolute |Δ| > stddev), flag it in the interpretation and call out the
discrepancy — do not paper over it. Possible explanations to note: GC
hidden in noise (revisit with larger N), JIT having already hoisted via
escape analysis (.NET 10 may have caught this), or the list internal
buffer reuse via the pool somewhere upstream.

(E) The SUMMARY should follow the standard quick summary template
(`@$HOME/.claude/get-shit-done/templates/summary.md`) for the top
sections (problem / approach / files-touched / test results / known
issues), with the bench section AFTER the standard sections.

Do NOT commit. The composer drives commits manually for benchmark
bundles per the established Bundle A/B pattern.
  </action>
  <verify>
    <automated>test -f /home/noah/Desktop/projects/flow-sharp/.planning/quick/260524-rsg-bundle-c-drop-per-call-list-wrap-in-coll/260524-rsg-SUMMARY.md &amp;&amp; ls /home/noah/Desktop/projects/flow-sharp/bench/results-bundle-c-*.txt &amp;&amp; grep -c "Benchmark Results (Bundle C)" /home/noah/Desktop/projects/flow-sharp/.planning/quick/260524-rsg-bundle-c-drop-per-call-list-wrap-in-coll/260524-rsg-SUMMARY.md</automated>
  </verify>
  <done>
    - `dotnet test` reports 1785 passed / 33 failed / 1 skipped / 1819 total — IDENTICAL to Bundle B baseline.
    - All 119 happy-path `tests/test_*.flow` scripts exit 0; the 4 expected non-zero-exit scripts unchanged.
    - `bench/results-bundle-c-<timestamp>.txt` exists with all 6 benchmark rows populated.
    - SUMMARY.md exists at the expected path with both marginal and cumulative tables, and a 1-2 sentence interpretation that names which benchmark moved the most and whether the expected `bench_collections` drop was observed.
  </done>
</task>

</tasks>

<verification>
- Build clean in Release.
- Test parity with Bundle B baseline (1785/33/1/1819) — zero new failures.
- Composer `tests/test_*.flow` sweep unchanged.
- `bench/results-bundle-c-*.txt` written.
- SUMMARY.md includes "## Benchmark Results (Bundle C)" with marginal + cumulative tables.
- Two-run cmp-clean determinism preserved (no PRNG touched, no iteration-order touched — preserved by construction).
</verification>

<success_criteria>
- 4 callback sites in `Collections.cs` (`Map`, `Filter`, `Each`, `Reduce`) use
  hoisted reusable `Value[]` buffers instead of per-iteration `new List<Value>`.
- `InvokeCallback` accepts `IReadOnlyList<Value>` (was `List<Value>`).
- No other file modified — `git diff --stat` shows exactly one file changed
  (`flow-lang/StandardLibrary/Collections.cs`).
- Observable behavior IDENTICAL — full test suite at Bundle B baseline.
- `bench/results-bundle-c-*.txt` exists; SUMMARY.md documents marginal +
  cumulative effect.
</success_criteria>

<output>
Write summary to:
`.planning/quick/260524-rsg-bundle-c-drop-per-call-list-wrap-in-coll/260524-rsg-SUMMARY.md`
</output>
