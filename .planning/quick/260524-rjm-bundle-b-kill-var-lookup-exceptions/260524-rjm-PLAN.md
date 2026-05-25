---
phase: quick-260524-rjm-bundle-b
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - flow-lang/Runtime/StackFrame.cs
  - flow-lang/Interpreter/ExpressionEvaluator.cs
  - flow-lang/Interpreter/Interpreter.cs
autonomous: true
requirements:
  - quick-bundle-b

must_haves:
  truths:
    - "Bare identifiers naming a function no longer pay throw/catch cost in EvaluateVariable hot path"
    - "EvaluateFunctionCall variable-vs-function disambiguation no longer pays throw/catch cost on the miss branch"
    - "ExecuteAssignment preserves identical error message and identical type-check + conversion + SetVariable semantics for both found and not-found cases"
    - "Two-run cmp-clean determinism preserved across all tests"
    - "Observable behavior identical — same diagnostics, same suggestion text, same shadowing semantics"
    - "Build clean (Release); xUnit test counts identical to Bundle A baseline (1785 passed / 33 failed / 1 skipped / 1819 total)"
    - "Bench run produces results file `bench/results-bundle-b-<timestamp>.txt`"
    - "SUMMARY.md includes two benchmark tables (marginal vs Bundle A; cumulative vs baseline)"
  artifacts:
    - path: "flow-lang/Runtime/StackFrame.cs"
      provides: "Non-throwing TryGetVariable(string, out Value) walking this → parent chain"
      contains: "public bool TryGetVariable"
    - path: "flow-lang/Interpreter/ExpressionEvaluator.cs"
      provides: "EvaluateVariable + EvaluateFunctionCall variable-vs-function fallback using TryGetVariable"
    - path: "flow-lang/Interpreter/Interpreter.cs"
      provides: "ExecuteAssignment using TryGetVariable for existence probe before type-check + SetVariable"
    - path: ".planning/quick/260524-rjm-bundle-b-kill-var-lookup-exceptions/SUMMARY.md"
      provides: "Quick task SUMMARY with two benchmark tables"
  key_links:
    - from: "ExpressionEvaluator.EvaluateVariable"
      to: "StackFrame.TryGetVariable"
      via: "_context.CurrentFrame.TryGetVariable(var.Name, out var v)"
      pattern: "TryGetVariable"
    - from: "ExpressionEvaluator.EvaluateFunctionCall (variable-holding-lambda fallback)"
      to: "StackFrame.TryGetVariable"
      via: "_context.CurrentFrame.TryGetVariable(call.Name, out var variable)"
      pattern: "TryGetVariable"
    - from: "Interpreter.ExecuteAssignment"
      to: "StackFrame.TryGetVariable"
      via: "_context.CurrentFrame.TryGetVariable(assignment.Name, out var existingValue)"
      pattern: "TryGetVariable"
---

<objective>
Bundle B: replace exception-driven variable lookup with `TryGetVariable` at three hot-path call sites. Today `StackFrame.GetVariable` throws `InvalidOperationException` on a miss, and three call sites catch that exception as control flow (`EvaluateVariable`, `EvaluateFunctionCall`'s variable-holding-lambda fallback, `ExecuteAssignment`). Every bare identifier that names a function — i.e. most identifiers in a Flow program — pays the full throw/catch cost.

Purpose: cut per-identifier dispatch overhead on the hot path with zero behavior change. Continuation of Bundle A (260524-r4o, -15% to -27% on dispatch-heavy benches).

Output:
- New `TryGetVariable` API on `StackFrame` (non-throwing, walks parent chain).
- Three call sites swapped from `try { GetVariable } catch (InvalidOperationException)` to `if (TryGetVariable(out var v)) { ... } else { ... }`.
- Bench results file proving marginal improvement vs Bundle A and cumulative improvement vs baseline.
- Quick task SUMMARY with two benchmark tables.

Constraints (LOCKED, from task_scope):
- Edits confined to: `flow-lang/Runtime/StackFrame.cs`, `flow-lang/Interpreter/ExpressionEvaluator.cs`, `flow-lang/Interpreter/Interpreter.cs`. NO other source files.
- `GetVariable` is NOT removed — it's still useful where the throw IS the correct semantic.
- Observable behavior IDENTICAL: same error messages, same Levenshtein suggestion path, same shadowing semantics, same assignment type-check + conversion + error wording.
- Two-run cmp-clean determinism preserved.
- No determinism change, no audio render path, no PRNG ordering.
- xUnit counts MUST match Bundle A baseline (1785/33/1/1819). Zero new failures.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md
@.planning/STATE.md

@flow-lang/Runtime/StackFrame.cs
@flow-lang/Interpreter/ExpressionEvaluator.cs
@flow-lang/Interpreter/Interpreter.cs
@bench/results-bundle-a-20260524-194547.txt
@bench/results-baseline-20260524-192612.txt

<interfaces>
<!-- Key contracts the executor needs. Extracted from codebase pre-edit. -->
<!-- No codebase exploration required during execution — these are the load-bearing shapes. -->

From flow-lang/Runtime/StackFrame.cs (today):

  public Value GetVariable(string name)
  {
      if (_variables.TryGetValue(name, out var value))
          return value;
      if (Parent != null)
          return Parent.GetVariable(name);
      throw new InvalidOperationException($"Variable '{name}' not found");
  }

  public bool HasVariable(string name)
  {
      return _variables.ContainsKey(name) || (Parent?.HasVariable(name) ?? false);
  }

Note: Functions live in a SEPARATE dictionary (`_functions`), not in `_variables`. `GetVariable` ONLY throws on "not found" — it never throws because "the slot holds a function reference" (functions are not stored in the variable slot at all). So the swap is purely "miss-as-return-false" not "miss-OR-function-as-return-false". The variable-vs-function disambiguation at call sites is done by checking the returned Value's Data type (`variable.Data is FunctionOverload`) — that check stays unchanged.

From flow-lang/Runtime/ExecutionContext.cs:

  public Value GetVariable(string name)
  {
      return CurrentFrame.GetVariable(name);
  }

(`_context.GetVariable(name)` is a 1-line delegation; swap call sites to `_context.CurrentFrame.TryGetVariable(name, out var v)` directly per task_scope wording.)

From flow-lang/Interpreter/ExpressionEvaluator.cs around lines 160-220 (EvaluateVariable):

  private Value EvaluateVariable(VariableExpression var)
  {
      try
      {
          return _context.GetVariable(var.Name);
      }
      catch (InvalidOperationException)
      {
          // Variable not found - check if it's a zero-argument function or a function reference
          var overloads = _context.CurrentFrame.GetFunctionOverloads(var.Name);
          if (overloads.Count > 0)
          {
              var zeroArgOverload = _context.TryResolveFunction(var.Name, Array.Empty<FlowType>());
              if (zeroArgOverload != null)
              {
                  if (zeroArgOverload.IsInternal)
                      return zeroArgOverload.Implementation!(new List<Value>());
                  else
                      return _invoker.ExecuteUserFunction(zeroArgOverload.Declaration!, new List<Value>());
              }
              return Value.Function(overloads[0]);
          }
          // Phase 35 LANG-04 rich FlowDiagnostic with Levenshtein suggestion
          var span = var.Span ?? Span.At(var.Location);
          var candidates = new HashSet<string>(StringComparer.Ordinal);
          foreach (var name in _context.CurrentFrame.GetAllAccessibleVariables().Keys)
              candidates.Add(name);
          foreach (var (name, _) in _context.InternalRegistry.EnumerateSignatures())
              candidates.Add(name);
          var suggestion = LevenshteinHelper.SuggestNearest(var.Name, candidates);
          var diag = new FlowDiagnostic(
              DiagnosticLevel.Error,
              $"unknown identifier '{var.Name}'",
              span,
              Labels: [new DiagnosticLabel(span, "not found in scope")],
              Notes: Array.Empty<string>(),
              Suggestion: suggestion);
          _errorReporter.Report(diag);
          return Value.Void();
      }
  }

From flow-lang/Interpreter/ExpressionEvaluator.cs around lines 307-321 (EvaluateFunctionCall — variable-holding-lambda fallback):

  // If no function found, try looking up as a variable holding a lambda
  if (overload == null)
  {
      try
      {
          var variable = _context.GetVariable(call.Name);
          if (variable.Data is FunctionOverload varOverload)
          {
              overload = varOverload;
          }
      }
      catch (InvalidOperationException)
      {
          // Not a variable either
      }
  }

NOTE on task_scope wording: task_scope says "if the call target is a bare identifier, prefer the variable slot when present (TryGetVariable hit), else dispatch as function." Re-reading the existing code, the order is the OPPOSITE — function resolution runs FIRST (line 304: `_context.TryResolveFunction`), and the variable-holding-lambda lookup is only the fallback when overload resolution returned null. Preserving the EXISTING ordering is mandatory ("Observable behavior IDENTICAL") — do NOT flip the order. The swap is purely mechanical: replace the try/catch with TryGetVariable inside the existing `if (overload == null)` block.

From flow-lang/Interpreter/Interpreter.cs around lines 1038-1074 (ExecuteAssignment):

  private void ExecuteAssignment(AssignmentStatement assignment)
  {
      var newValue = _evaluator.Evaluate(assignment.Value);
      try
      {
          var existingValue = _context.GetVariable(assignment.Name);
          var targetType = existingValue.Type;
          if (!newValue.Type.IsCompatibleWith(targetType) &&
              !newValue.Type.CanConvertTo(targetType))
          {
              _errorReporter.ReportError(
                  $"Cannot assign {newValue.Type} to variable of type {targetType}",
                  assignment.Location);
              return;
          }
          if (!newValue.Type.Equals(targetType) && newValue.Type.CanConvertTo(targetType))
          {
              newValue = newValue.ConvertTo(targetType);
          }
          _context.SetVariable(assignment.Name, newValue);
      }
      catch (InvalidOperationException)
      {
          _errorReporter.ReportError(
              $"Variable '{assignment.Name}' not found",
              assignment.Location);
      }
  }

Assignment semantic intent: `GetVariable` is a "what's the existing type?" probe to drive type-check + conversion before `SetVariable`. Catch maps "not declared" → clean ReportError. Equivalent post-swap:

  if (_context.CurrentFrame.TryGetVariable(assignment.Name, out var existingValue))
  {
      var targetType = existingValue.Type;
      // ... same type check / conversion / SetVariable ...
  }
  else
  {
      _errorReporter.ReportError($"Variable '{assignment.Name}' not found", assignment.Location);
  }

Identical error wording, identical behavior on found, identical behavior on miss.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Add TryGetVariable + swap EvaluateVariable + swap EvaluateFunctionCall fallback</name>
  <files>flow-lang/Runtime/StackFrame.cs, flow-lang/Interpreter/ExpressionEvaluator.cs</files>
  <action>
Edit `flow-lang/Runtime/StackFrame.cs`:

1. Add a new public method `TryGetVariable(string name, out Value value)` directly below the existing `GetVariable` (around line 44). Implementation mirrors `GetVariable` but never throws:

   - If `_variables.TryGetValue(name, out var v)` succeeds, set `value = v` and return `true`.
   - Else if `Parent != null`, tail-call `return Parent.TryGetVariable(name, out value)`.
   - Else set `value = default` (or `default(Value)` / `null!` — match the existing nullable conventions of the file; the file uses non-nullable `Value` storage so `value = default!` is idiomatic) and return `false`.
   - XML doc comment summarizes: "Bundle B (260524-rjm) hot-path probe. Walks this → parent chain identically to GetVariable but returns false instead of throwing on miss. Does NOT throw under any circumstance. Use this in dispatch hot paths; use GetVariable where the throw IS the correct semantic."
   - Do NOT remove `GetVariable`. Do NOT modify `HasVariable`, `SetVariable`, `DeclareVariable`, function-management methods, or any snapshot/restore helpers.

Edit `flow-lang/Interpreter/ExpressionEvaluator.cs`:

2. In `EvaluateVariable` (around lines 160-220), replace the `try { return _context.GetVariable(var.Name); } catch (InvalidOperationException) { <fallback> }` structure with:

   `if (_context.CurrentFrame.TryGetVariable(var.Name, out var v)) return v;`

   followed by the existing fallback body (function-overload lookup → zero-arg function call OR function-reference Value OR rich FlowDiagnostic with Levenshtein suggestion) verbatim — same control flow, same error wording, same suggestion-candidate construction, same `Value.Void()` return at the end. Just unwrap the catch block into a straight-line `else` branch (or move it after the early-return if since the function body otherwise falls through).

3. In `EvaluateFunctionCall`, inside the existing `if (overload == null) { ... }` block at the "try looking up as a variable holding a lambda" fallback (around lines 307-321), replace:

   ```
   try {
       var variable = _context.GetVariable(call.Name);
       if (variable.Data is FunctionOverload varOverload) { overload = varOverload; }
   } catch (InvalidOperationException) { }
   ```

   with:

   `if (_context.CurrentFrame.TryGetVariable(call.Name, out var variable) && variable.Data is FunctionOverload varOverload) { overload = varOverload; }`

   Do NOT reorder this fallback relative to the preceding `_context.TryResolveFunction` call — the existing ordering (function resolution first, variable-holding-lambda fallback second) is observable behavior and must be preserved.

Build clean (Release): `dotnet build -c Release flow-sharp.sln 2>&1 | tail -20`.
  </action>
  <verify>
    <automated>cd /home/noah/Desktop/projects/flow-sharp &amp;&amp; dotnet build -c Release 2>&amp;1 | tail -5</automated>
  </verify>
  <done>
- `StackFrame.TryGetVariable` exists, returns bool, walks parent chain, never throws.
- `EvaluateVariable` uses TryGetVariable; identical fallback behavior + error wording + suggestion path.
- `EvaluateFunctionCall` variable-holding-lambda fallback uses TryGetVariable; ordering preserved.
- Release build clean (warnings allowed, errors not).
  </done>
</task>

<task type="auto">
  <name>Task 2: Swap ExecuteAssignment + run xUnit + bench + write SUMMARY</name>
  <files>flow-lang/Interpreter/Interpreter.cs, .planning/quick/260524-rjm-bundle-b-kill-var-lookup-exceptions/SUMMARY.md</files>
  <action>
Edit `flow-lang/Interpreter/Interpreter.cs`:

1. In `ExecuteAssignment` (around lines 1038-1074), replace the `try { var existingValue = _context.GetVariable(assignment.Name); ... } catch (InvalidOperationException) { ReportError("Variable '...' not found") }` structure with an `if (_context.CurrentFrame.TryGetVariable(assignment.Name, out var existingValue)) { ... } else { ReportError(...) }` structure. Preserve EVERY detail of the inner body:

   - `var targetType = existingValue.Type;`
   - The `!newValue.Type.IsCompatibleWith(targetType) && !newValue.Type.CanConvertTo(targetType)` check with the `$"Cannot assign {newValue.Type} to variable of type {targetType}"` error wording and the `return;` short-circuit.
   - The conditional `newValue = newValue.ConvertTo(targetType)` conversion.
   - The final `_context.SetVariable(assignment.Name, newValue);`.
   - The else-branch error wording stays EXACTLY `$"Variable '{assignment.Name}' not found"`.

Verify build:

   `cd /home/noah/Desktop/projects/flow-sharp && dotnet build -c Release 2>&1 | tail -5`

Run xUnit (must match Bundle A baseline exactly — 1785 passed / 33 failed / 1 skipped / 1819 total, zero new failures):

   `cd /home/noah/Desktop/projects/flow-sharp && dotnet test flow-lang.Tests -c Release --nologo 2>&1 | tail -20`

   If the pass/fail counts deviate from Bundle A's (1785/33/1/1819), STOP and diagnose. Any new failure is a behavior regression — Observable behavior IDENTICAL is a LOCKED constraint.

Run benchmark:

   `cd /home/noah/Desktop/projects/flow-sharp && bash bench/run.sh --label bundle-b`

   This produces `bench/results-bundle-b-<timestamp>.txt`. Note the filename for the SUMMARY.

Write `.planning/quick/260524-rjm-bundle-b-kill-var-lookup-exceptions/SUMMARY.md` following the standard quick-task SUMMARY shape:

- Header: title, date, scope, files-touched list.
- "## What changed" — TryGetVariable added; 3 call sites swapped (EvaluateVariable, EvaluateFunctionCall fallback, ExecuteAssignment).
- "## Why" — exception-as-control-flow on bare-identifier hot path; functions live in `_functions` so most bare identifiers triggered the catch path.
- "## Verification" — Release build clean; xUnit 1785/33/1/1819 (matches Bundle A — zero new failures); bench results file path.
- "## Benchmark Results (Bundle B)" — REQUIRED section with two tables:
  - **Table 1: marginal vs Bundle A** — columns `| Script | Bundle A mean (s) | Bundle B mean (s) | Δ (s) | Δ (%) |`. One row per bench script (collections, function_calls, notestream, overload, parse, var_lookup). Bundle A means from `bench/results-bundle-a-20260524-194547.txt` (already in context above). Compute Δ = (B - A), Δ% = ((B - A) / A) × 100.
  - **Table 2: cumulative vs baseline** — columns `| Script | Baseline mean (s) | Bundle B mean (s) | Δ (s) | Δ (%) |`. Baseline means from `bench/results-baseline-20260524-192612.txt` (read it first if not already in context).
- "## Constraints honored" — edits confined to the 3 allowed files; GetVariable retained; observable behavior identical; two-run cmp-clean preserved; zero PRNG/audio/determinism impact.
- "## Follow-ups" — note any places where `try { GetVariable } catch` still appears in the codebase that weren't in-scope for Bundle B (do a quick `grep -rn "GetVariable" flow-lang/ | grep -v TryGetVariable | grep -v "Set\|Has\|Declare\|Snapshot\|Restore\|Local\|Accessible"` and list anything not in the 3 touched files as a future-bundle candidate).

Commit via gsd-sdk (one cohesive commit covering both tasks):

   `gsd-sdk query commit "perf(runtime): swap try/catch variable lookup for TryGetVariable" --files flow-lang/Runtime/StackFrame.cs flow-lang/Interpreter/ExpressionEvaluator.cs flow-lang/Interpreter/Interpreter.cs .planning/quick/260524-rjm-bundle-b-kill-var-lookup-exceptions/SUMMARY.md bench/results-bundle-b-*.txt`
  </action>
  <verify>
    <automated>cd /home/noah/Desktop/projects/flow-sharp &amp;&amp; dotnet build -c Release 2>&amp;1 | tail -5 &amp;&amp; dotnet test flow-lang.Tests -c Release --nologo 2>&amp;1 | tail -5 &amp;&amp; ls bench/results-bundle-b-*.txt &amp;&amp; grep -c "Benchmark Results (Bundle B)" .planning/quick/260524-rjm-bundle-b-kill-var-lookup-exceptions/SUMMARY.md</automated>
  </verify>
  <done>
- ExecuteAssignment uses TryGetVariable; identical type-check + conversion + error wording on both branches.
- Release build clean.
- xUnit: 1785 passed / 33 failed / 1 skipped / 1819 total. Identical to Bundle A. Zero new failures.
- `bench/results-bundle-b-<timestamp>.txt` exists.
- SUMMARY.md has "## Benchmark Results (Bundle B)" section with both required tables (Table 1: marginal vs Bundle A; Table 2: cumulative vs baseline).
- Commit landed with all 5 file groups.
  </done>
</task>

</tasks>

<verification>
End-of-phase manual sanity:

1. Confirm `grep -n "GetVariable" flow-lang/Runtime/StackFrame.cs` shows BOTH `GetVariable` (still present, throws on miss) AND `TryGetVariable` (new, non-throwing). The old method MUST NOT be removed.

2. Confirm `grep -n "catch (InvalidOperationException)" flow-lang/Interpreter/ExpressionEvaluator.cs flow-lang/Interpreter/Interpreter.cs` shows zero hits in the three swapped methods (EvaluateVariable / EvaluateFunctionCall variable-fallback / ExecuteAssignment). Other unrelated catches in these files MAY remain — only the three swapped sites are in-scope.

3. Confirm `git diff --name-only HEAD` shows ONLY the 3 source files + SUMMARY.md + the new bench results .txt. Anything else means the scope guard was breached.

4. SUMMARY.md Table 1 should show same-sign-or-improvement deltas — Bundle B should be ≤ Bundle A on dispatch-heavy benches (var_lookup, function_calls). A small regression on any single script is acceptable (< 5%) but should be called out in the SUMMARY narrative. A regression > 5% on any script is a red flag — investigate before commit.
</verification>

<success_criteria>
- `flow-lang/Runtime/StackFrame.cs` has new `TryGetVariable(string, out Value)` returning bool, walking parent chain, never throwing.
- All 3 call sites (EvaluateVariable, EvaluateFunctionCall variable-holding-lambda fallback, ExecuteAssignment) use TryGetVariable; no try/catch for variable-lookup-as-control-flow remains in these 3 sites.
- `GetVariable` retained on `StackFrame` (other call sites untouched).
- Release build clean.
- xUnit: 1785 passed / 33 failed / 1 skipped / 1819 total — IDENTICAL to Bundle A. Zero new failures.
- `bench/results-bundle-b-<timestamp>.txt` produced.
- SUMMARY.md has "## Benchmark Results (Bundle B)" with Table 1 (marginal vs Bundle A) AND Table 2 (cumulative vs baseline), both with the required column shape.
- Single commit lands all changes.
- Two-run cmp-clean determinism preserved (xUnit covers this).
- Scope boundary held: only `flow-lang/Runtime/StackFrame.cs` + `flow-lang/Interpreter/ExpressionEvaluator.cs` + `flow-lang/Interpreter/Interpreter.cs` source edits.
</success_criteria>

<output>
Create `.planning/quick/260524-rjm-bundle-b-kill-var-lookup-exceptions/SUMMARY.md` when done (per Task 2 action).
</output>
