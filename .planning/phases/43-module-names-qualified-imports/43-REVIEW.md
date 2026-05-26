---
phase: 43-module-names-qualified-imports
reviewed: 2026-05-24T00:00:00Z
depth: standard
files_reviewed: 31
files_reviewed_list:
  - flow-lang/Lexing/TokenType.cs
  - flow-lang/Lexing/SimpleLexer.cs
  - flow-lang/Ast/Statements/ModuleDeclarationStatement.cs
  - flow-lang/Parsing/Parser.cs
  - flow-lang/Runtime/ModuleRegistry.cs
  - flow-lang/Runtime/ExecutionContext.cs
  - flow-lang/Runtime/ModuleLoader.cs
  - flow-lang/Interpreter/ExpressionEvaluator.cs
  - flow-lang/Interpreter/Interpreter.cs
  - flow-lang/Core/FlowEngine.cs
  - flow-lang/StandardLibrary/Audio/BeatConversionFunctions.cs
  - flow-lang/StandardLibrary/Audio/EffectsFunctions.cs
  - flow-lang/StandardLibrary/BuiltInFunctions.cs
  - flow-lang/audio.flow
  - flow-lang/bars.flow
  - flow-lang/collections.flow
  - flow-lang/composition.flow
  - flow-lang/generative.flow
  - flow-lang/improv.flow
  - flow-lang/notation-io.flow
  - flow-lang/notation.flow
  - flow-lang/osc.flow
  - flow-lang/patterns.flow
  - flow-lang/sfz.flow
  - flow-lang/test.flow
  - flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs
  - flow-lang.Tests/Integration/Phase43/BeatCompanionOverloadTests.cs
  - flow-lang.Tests/Integration/Phase43/BeatConversionTests.cs
  - flow-lang.Tests/Integration/Phase43/ModuleCollisionAdvisoryTests.cs
  - flow-lang.Tests/Integration/Phase43/ModuleDeclarationParserTests.cs
  - flow-lang.Tests/Integration/Phase43/ModuleRegistryTests.cs
  - flow-lang.Tests/Integration/Phase43/QualifiedAccessDispatchTests.cs
findings:
  critical: 4
  warning: 3
  info: 2
  total: 9
status: issues_found
---

# Phase 43: Code Review Report

**Reviewed:** 2026-05-24T00:00:00Z
**Depth:** standard
**Files Reviewed:** 31
**Status:** issues_found

## Summary

Phase 43 adds `module <name>` top-of-file declarations, qualified access (`mod.fn`), per-context `ModuleRegistry`, two beat-conversion builtins (`beatToSec`/`secToBeat`), and Beat-typed companion overloads for `delay` and `renderBarAtBeat`. The lexer/parser/AST/registry shape is sound and the test coverage is thorough. Four blockers were found.

**Critical issues** cover: (1) the qualified-call fast-path skips all argument coercion and named-arg reordering that the normal call path applies, producing wrong results for any typed coercion; (2) `ModuleRegistry` and `ProcOwnership` are not included in `TestSnapshot`, so module registrations leak across hermetic test boundaries; (3) the `ModuleLoader` exports ALL proc names including `internal` ones, making C#-backend-only `internal proc` declarations publicly addressable as `module.name`; (4) a module name that happens to be identical to an existing variable name silently short-circuits EvaluateMemberAccess before reaching instance-member dispatch, breaking the Pitfall-2 fall-through invariant for that collision case.

---

## Critical Issues

### CR-01: Qualified-call fast-path skips argument coercion and named-arg reordering

**File:** `flow-lang/Interpreter/ExpressionEvaluator.cs:240-255`

**Issue:** When `call.Name` contains a dot (qualified call), `EvaluateFunctionCall` short-circuits after evaluating raw argument values and immediately invokes `registeredOverload.Implementation!(qArgValues)` (for internal procs) or `ExecuteUserFunctionWithCaptures` (for user procs). The normal call path (lines 272-494) performs two critical transformations before invocation: (a) type-coercion to match the resolved signature (`argValues[i].ConvertTo(sig.InputTypes[i])`, lines 356-392), and (b) named-arg reordering into positional slots (lines 319-346). Neither transformation happens in the qualified fast-path.

Consequence: `(math.add 5 3.0)` resolves at +100 convertible-score for the Double overload but delivers an `int`-boxed `Value` to `args[0].As<double>()`, throwing `InvalidCastException` at runtime — the same class of bug that Phase 26's coercion fix addressed for the unqualified path (see comment at line 356). Named args via qualified syntax (`(math.compute x=1 y=2)`) are silently reordered incorrectly.

**Fix:** Evaluate arguments, run coercion and named-arg reordering through the shared utility that the normal path uses before invoking the registered overload. At minimum, apply the coercion loop from lines 360-391 after building `qArgValues`:
```csharp
// After: var qArgValues = call.Arguments.Select(Evaluate).ToList();
var qSig = registeredOverload.Signature;
for (int i = 0; i < qArgValues.Count && i < qSig.InputTypes.Count; i++)
{
    if (qSig.InputTypes[i] is ArrayType { ElementType: VoidType }) continue;
    if (qSig.InputTypes[i] is DictType { KeyType: VoidType, ValueType: VoidType }) continue;
    if (qSig.InputTypes[i] is TupleType { IsAnyArity: true }) continue;
    if (!qArgValues[i].Type.Equals(qSig.InputTypes[i])
        && qArgValues[i].Type.CanConvertTo(qSig.InputTypes[i]))
        qArgValues[i] = qArgValues[i].ConvertTo(qSig.InputTypes[i]);
}
```

---

### CR-02: ModuleRegistry and ProcOwnership absent from TestSnapshot — hermetic isolation broken

**File:** `flow-lang/StandardLibrary/TestFramework/TestSnapshot.cs` (entire file) and `flow-lang/Runtime/ExecutionContext.cs:732-891`

**Issue:** `SnapshotState` captures 13 named surfaces but does not snapshot `ModuleRegistry` (line 181) or `ProcOwnership` (line 202). `RestoreState` therefore never restores them. A `(test "a" ...)` body that does `use "@patterns"` (which loads `patterns.flow`, which has `module patterns`) permanently registers `patterns` into `context.ModuleRegistry` and claims proc ownership. The next `(test "b" ...)` starts with a polluted registry, violating the Phase 35 TEST-02 hermetic-isolation contract.

The snapshot's own comment block (lines 719-726) says "Adding a new mutable surface to the engine requires touching THREE places." Phase 43 added two new mutable surfaces (`ModuleRegistry`, `ProcOwnership`) but touched none of the three places.

**Fix:** Add snapshot fields and restore logic:
```csharp
// In TestSnapshot.cs — add after StyleOverrideAdvisoriesEmitted:
public IReadOnlyDictionary<string, IReadOnlyDictionary<string, Value>>? ModuleRegistryState { get; init; }
public IReadOnlyDictionary<string, string>? ProcOwnershipState { get; init; }

// In ExecutionContext.SnapshotState():
ModuleRegistryState = ModuleRegistry.Snapshot(),
ProcOwnershipState = new Dictionary<string, string>(ProcOwnership),

// In ExecutionContext.RestoreState(snap):
if (snap.ModuleRegistryState != null)
{
    ModuleRegistry.Clear();
    foreach (var (k, v) in snap.ModuleRegistryState)
        ModuleRegistry.Register(k, v);
}
if (snap.ProcOwnershipState != null)
{
    ProcOwnership.Clear();
    foreach (var (k, v) in snap.ProcOwnershipState)
        ProcOwnership[k] = v;
}
```

---

### CR-03: ModuleLoader exports internal procs, making C#-only declarations publicly addressable via qualified access

**File:** `flow-lang/Runtime/ModuleLoader.cs:139-149`

**Issue:** The export-building loop at lines 139-149 iterates all `ProcDeclaration` statements without filtering on `proc.IsInternal`. `internal proc` declarations are the bridge from `.flow` source to C# implementations — they are not meant to be callable from outside (they have no body; they match against the C# registry). But after Phase 43, `(audio.createBuffer 0 1 44100)` (for example) would resolve through `ModuleRegistry.TryGetProc("audio", "createBuffer")` and dispatch the registered C# overload directly, bypassing overload resolution, type coercion, and the entire normal call path. An internal proc with an overloaded name (e.g., `loadWav` has 3 overloads in `audio.flow`) will only expose the last-declared variant, silently dropping the other two from the qualified surface.

**Fix:** Skip `IsInternal` procs in the export loop:
```csharp
if (stmt is Ast.Statements.ProcDeclaration proc && !proc.IsInternal)
{
    var overloads = context.GlobalFrame.GetFunctionOverloads(proc.Name);
    if (overloads.Count > 0)
        exportedProcs[proc.Name] = Value.Function(overloads[overloads.Count - 1]);
}
```
Internal functions remain accessible via their unqualified names as before; they are not part of the module's public surface.

---

### CR-04: Variable-name collision with registered module name silently short-circuits instance-member dispatch

**File:** `flow-lang/Interpreter/ExpressionEvaluator.cs:694-707`

**Issue:** `EvaluateMemberAccess` checks `_context.ModuleRegistry.TryGetProc(varExpr.Name, member.MemberName)` first, before evaluating the LHS expression. If a composer writes:

```flow
use "@patterns"
Section patterns = (getSections song)  // variable named 'patterns'
(print patterns.Name)                  // .Name is a SectionData member
```

The LHS `patterns` is both a registered module name (from `module patterns`) and a local variable. The registry-first branch fires, finds no proc named `Name`, falls into the second branch (line 699-706), reports `[module] module 'patterns' has no proc 'Name'`, and returns `Value.Void()`. The composer's actual `Section` variable is never consulted. This is a silent silent behavioral regression that is impossible to work around — the composer cannot rename the stdlib module.

The comment at line 688 says "only bare VariableExpression references to REGISTERED module names hit this branch" — but it does not account for the case where a composer's variable happens to collide with a module name. The `varExpr.Name` check is purely syntactic; it does not confirm the variable does not exist in scope.

**Fix:** Before dispatching through the registry, confirm no variable with that name exists in the current scope:
```csharp
if (member.Object is VariableExpression varExpr)
{
    // Only use the registry-first path if no variable by this name is in scope —
    // a variable always wins over a module name for backward-compat.
    bool hasLocalVar = false;
    try { _context.GetVariable(varExpr.Name); hasLocalVar = true; } catch { }
    if (!hasLocalVar && _context.ModuleRegistry.TryGetProc(varExpr.Name, member.MemberName, out var procValue))
        return procValue!;
    if (!hasLocalVar && _context.ModuleRegistry.Contains(varExpr.Name))
    {
        _errorReporter.ReportError(...);
        return Value.Void();
    }
}
```

---

## Warnings

### WR-01: Duplicate `using FlowLang.Diagnostics;` directive in ExpressionEvaluator.cs

**File:** `flow-lang/Interpreter/ExpressionEvaluator.cs:7,14`

**Issue:** `using FlowLang.Diagnostics;` appears on both line 7 and line 14. The duplicate is harmless in C# (no compiler error) but indicates a careless edit — Phase 43 added the second occurrence without noticing the first. This will produce a compiler warning in strict diagnostic mode and should be removed.

**Fix:** Remove the duplicate `using FlowLang.Diagnostics;` at line 14.

---

### WR-02: ModuleRegistry.Snapshot() is not thread-safe against concurrent Register calls

**File:** `flow-lang/Runtime/ModuleRegistry.cs:100-103`

**Issue:** `Snapshot()` constructs a `new Dictionary<>(modules)` from the `ConcurrentDictionary`. The copy constructor iterates the source dict while taking a lock on each bucket in turn — it is NOT a point-in-time atomic snapshot. A concurrent `Register()` call arriving mid-iteration can produce a snapshot that sees the new registration in some buckets but not others, creating an internally inconsistent copy. While the concurrency concern is noted in the XML doc ("two-actor pattern"), the implementation is weaker than the claim implies. The `LiveBlockRegistry.Snapshot()` this is said to mirror has the same issue; this review flags it here as it is a new addition.

For the test-snapshot (CR-02 fix) this matters: if a test does `use` from a background thread (possible in async tests), the snapshot may be torn.

**Fix:** Take an atomic snapshot by enumerating under a copy guard:
```csharp
public IReadOnlyDictionary<string, IReadOnlyDictionary<string, Value>> Snapshot()
{
    // ToArray() on ConcurrentDictionary enumerates consistently without holding a lock
    // across the entire enumeration (entries are individually locked, not the whole dict).
    // For true point-in-time consistency, collect under a local lock or use ToArray().
    return _modules.ToArray().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
}
```
Note: This is still not perfectly atomic under adversarial concurrent mutation, but it prevents torn multi-bucket reads and matches the pattern used by `PrngRegistry`.

---

### WR-03: Qualified user-proc call does not check Declaration null — potential NullReferenceException

**File:** `flow-lang/Interpreter/ExpressionEvaluator.cs:254-255`

**Issue:** In the qualified-call fast-path, when `registeredOverload.IsInternal` is false (user-defined proc), the code calls:
```csharp
return _invoker.ExecuteUserFunctionWithCaptures(
    registeredOverload.Declaration!, qArgValues, registeredOverload.CapturedVariables);
```
The `!` null-forgiving operator suppresses the nullable warning, but `Declaration` can legitimately be null for `FunctionOverload.Internal(...)` entries (which set `Implementation` but not `Declaration`). The `IsInternal` property is `Implementation != null` — so a proc registered via `FunctionOverload.Internal` would have `IsInternal == true` and take the other branch. However, `ModuleLoader` builds the exported proc dict by calling `Value.Function(overloads[overloads.Count - 1])` where the last overload for an `internal proc` statement is an `Internal` overload (set by the interpreter at line 851 of `Interpreter.cs`). This means `IsInternal` should always be true for C#-backed entries and the null-forgiving `!` is safe in the current code. But any path that stores a user-proc overload incorrectly as an internal one would NullReferenceException here. A defensive null check costs nothing:

**Fix:**
```csharp
if (registeredOverload.Declaration == null)
{
    _errorReporter.ReportError($"[module] qualified call '{call.Name}': internal function reached user-proc dispatch path", call.Location);
    return Value.Void();
}
return _invoker.ExecuteUserFunctionWithCaptures(
    registeredOverload.Declaration, qArgValues, registeredOverload.CapturedVariables);
```

---

## Info

### IN-01: `beatToSec`/`secToBeat` advisory sentinel keys are process-global, not per-context

**File:** `flow-lang/StandardLibrary/Audio/BeatConversionFunctions.cs:69-71,88-91`

**Issue:** `RenderingDiagnostics.WarnOnce` is a process-global dedup set. The sentinels `"beatToSec-no-tempo"` and `"secToBeat-no-tempo"` are single strings, so once fired in any `FlowEngine` instance during a process lifetime, they never fire again. This is consistent with other Phase 36/37 advisory keys and is the documented behavior per D-v1.5-06. It is noted because test order may cause a test that expects the advisory to see it suppressed if another test runs first — `BeatConversionTests.BeatToSec_AdvisoryDedupsAcrossRuns` (Test 6) correctly calls `RenderingDiagnostics.ResetForTesting()` in `Dispose()`, but `BeatConversionTests` is in the `[Collection("FlowScripts")]` shared collection, meaning xUnit may reuse the process state. The ctor resets the latch, which mitigates this — no change required.

**Fix:** No action required; documented for awareness.

---

### IN-02: `module` keyword is now reserved but `ParseProcDeclaration` keyword-as-name allowlist is not updated

**File:** `flow-lang/Parsing/Parser.cs:317-320`

**Issue:** `ParseProcDeclaration` allows certain musical-context keywords to serve as procedure names (`pan`, `gain`, `tempo`, `swing`, `key`, `timesig`) because these were grandfathered uses. `module` is now a reserved keyword (line 897 of `SimpleLexer.cs`) but is NOT in the `ParseProcDeclaration` allowlist. This means:

```flow
proc module (Int: n) ...  // previously valid (was an identifier), now a parse error
```

In practice, per RESEARCH Pitfall 1, no existing `.flow` file uses `module` as an identifier, so no regression. This is purely informational — a composer cannot write `proc module(...)` any longer but would have no reason to.

**Fix:** No action required (the prohibition is intentional per D-03).

---

_Reviewed: 2026-05-24T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
