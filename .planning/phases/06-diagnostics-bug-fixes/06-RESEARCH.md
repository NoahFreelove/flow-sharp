# Phase 6: Diagnostics & Bug Fixes - Research

**Researched:** 2026-04-02
**Domain:** Interpreter diagnostics, overload resolution, section execution, error reporting, static state isolation
**Confidence:** HIGH

## Summary

Phase 6 addresses five items: one developer experience feature (--verbose flag) and four bug fixes targeting overload resolution, section bare expressions, error masking, and static manager clobbering in watch mode. All five are changes to existing code with no new dependencies. The bugs are interconnected: the --verbose flag aids diagnosis of the overload and error masking issues, and the section fix depends on understanding how `_lastExpressionValue` flows through the interpreter.

The overload resolution bug has two distinct root causes confirmed by direct code reading. First, `transpose(sequence, 2)` fails because the second argument is `IntType` but the registered signature expects `SemitoneType`, and `SemitoneType` has no `IsCompatibleWith`/`CanConvertTo` override to accept Int. Second, `vary(sequence, 0.5)` fails with "function not found" when `@composition` is not imported -- `vary` lives in `composition.flow`, not `std.flow`, making it invisible without explicit import. Both are fixable with targeted changes.

The section bare-expressions bug is confirmed at `Interpreter.cs:360-368`: `ExecuteSectionDeclaration` only collects named variables of type `SequenceData` from the scope frame, ignoring the `_lastExpressionValue` that bare expression statements produce. The error masking bug is in `FlowEngine.Execute()` line 89: it returns `!_errorReporter.HasErrors` but the interpreter continues executing after errors are reported, meaning partial execution can produce incorrect results while still returning `true` (if the error was swallowed by `TryResolveFunction`'s temporary reporter). The static manager issue in `LiveReloadManager` already has a save/restore pattern (lines 346-355) but it has a race condition: the restore is not in a `finally` block.

**Primary recommendation:** Implement --verbose first to trace overload resolution at runtime, then fix each bug at the narrowest layer possible. Do not widen type compatibility globally.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| QOL-01 | `--verbose` flag shows registered functions, loaded modules, and type resolution details | CLI flag parsing in Program.cs, threading through FlowEngine/OverloadResolver/ModuleLoader |
| FIX-01 | Sequence type resolves correctly in overload matching (transpose, vary, and all transforms work with Sequence arguments) | Root cause confirmed: SemitoneType lacks Int compatibility; vary requires @composition import; need `transpose(Sequence, Int)` overload or SemitoneType.IsCompatibleWith(IntType) |
| FIX-02 | Bare expressions inside sections are captured as anonymous sequences (no silent 0-frame renders) | Root cause confirmed: ExecuteSectionDeclaration only scans named variables, ignores _lastExpressionValue |
| FIX-03 | Error reporter distinguishes fatal vs non-fatal errors and does not mask function-not-found failures as success | Root cause confirmed: TryResolveFunction swallows errors via temp reporter; ResolveFunction reports but callers continue |
| FIX-04 | Background FlowEngine instances do not clobber static PlaybackFunctions manager (proper isolation) | Root cause confirmed: save/restore exists but not in finally block; race condition possible |
</phase_requirements>

## Standard Stack

No new dependencies. All changes are to existing C# code in the `flow-lang` and `flow-interpreter` projects.

| Component | File(s) | Purpose |
|-----------|---------|---------|
| .NET 9 / C# 13 | Existing | Runtime and language |
| FlowEngine | `flow-lang/Core/FlowEngine.cs` | Pipeline orchestrator -- needs verbose flag threading |
| OverloadResolver | `flow-lang/TypeSystem/OverloadResolver.cs` | Overload matching -- needs verbose diagnostic output |
| FunctionSignature | `flow-lang/TypeSystem/FunctionSignature.cs` | 4-way compatibility check in `Matches()` |
| Interpreter | `flow-lang/Interpreter/Interpreter.cs` | Section execution, `_lastExpressionValue` |
| ErrorReporter | `flow-lang/Diagnostics/ErrorReporter.cs` | Error accumulation and formatting |
| PlaybackFunctions | `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs` | Static `_manager` field |
| LiveReloadManager | `flow-interpreter/LiveReloadManager.cs` | Watch mode save/restore of manager |
| Program.cs | `flow-interpreter/Program.cs` | CLI argument parsing |

## Architecture Patterns

### Pattern 1: Verbose Diagnostic Output via TextWriter

**What:** Thread a `TextWriter?` (null when not verbose) through `FlowEngine` -> `ExecutionContext` -> `OverloadResolver` -> `ModuleLoader`. When non-null, write diagnostic lines. When null, zero cost.

**Why this pattern:** Avoids boolean flags scattered through the codebase. A null check on a reference is fast. The TextWriter can be `Console.Error` for CLI or a `StringWriter` for testing.

```csharp
// In OverloadResolver.Resolve():
if (_diagnosticOutput != null)
{
    _diagnosticOutput.WriteLine($"[verbose] Resolving '{functionName}' with args ({string.Join(", ", argTypes)})");
    foreach (var sig in candidates)
        _diagnosticOutput.WriteLine($"[verbose]   candidate: {sig} -> matches={sig.Matches(argTypes)}");
}
```

### Pattern 2: Implicit Section Value Collection

**What:** After executing section body statements, check `_lastExpressionValue` for SequenceData and add it to the section's sequences with an auto-generated name.

**Why:** Mirrors how `proc` bodies handle implicit returns via `ImplicitReturnCollector`. Sections should behave consistently with procs for bare expressions.

```csharp
// After the foreach loop in ExecuteSectionDeclaration, before creating SectionData:
if (_lastExpressionValue?.Data is SequenceData implicitSeq)
{
    string autoName = $"_anon_{sequences.Count}";
    sequences[autoName] = implicitSeq;
}
```

### Pattern 3: Save/Restore with Finally Block

**What:** Wrap background engine creation in try/finally to guarantee static manager restoration.

```csharp
var savedManager = PlaybackFunctions.GetManager();
try
{
    using var engine = new FlowEngine();
    engine.AudioManager.CaptureMode = true;
    engine.Execute(source, filePath);
}
finally
{
    if (savedManager != null)
        PlaybackFunctions.SetManager(savedManager);
}
```

### Anti-Patterns to Avoid

- **Widening SequenceType.IsCompatibleWith globally:** Do not make SequenceType accept other types. The type system is correct; the issue is that `SemitoneType` needs to accept `Int`, and `vary` needs to be importable.
- **Removing the error accumulation model:** Do not switch to exception-based error handling. Instead, add early returns after error reports at specific sites.
- **Refactoring the static PlaybackFunctions._manager out:** This is a v2 architectural change. For v1.1, the save/restore pattern with proper `finally` is sufficient.
- **Making --verbose a global static bool:** Thread it as a `TextWriter?` through constructors. Static state is what caused FIX-04 in the first place.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Diagnostic output | Custom logging framework | `TextWriter?` (null = off) | Zero overhead when disabled; testable with `StringWriter` |
| CLI flag parsing | Argument parser library | Extend existing `ParseFlags()` in Program.cs | One new flag; existing pattern works fine |

## Common Pitfalls

### Pitfall 1: Fixing Sequence Overloads at the Wrong Layer

**What goes wrong:** Modifying `SequenceType.IsCompatibleWith()` or `FlowType.IsCompatibleWith()` to be more permissive. This breaks ALL overload resolution because every type that uses the base `Equals` check for matching will now get unexpected matches.

**Why it happens:** The symptom is "No matching overload for transpose with (Sequence, Int)" which looks like a Sequence type issue, but the actual problem is that `Int` does not match `Semitone`.

**How to avoid:** There are two correct fixes:
1. Add a `transpose(Sequence, Int)` overload that treats the Int as semitones (simplest).
2. Make `SemitoneType` override `IsCompatibleWith(FlowType target)` to return true when `target is IntType` (semantic: an Int is a valid number of semitones). This also fixes the reverse: `IntType.CanConvertTo(SemitoneType)` -- but Int doesn't know about Semitone, so SemitoneType must accept Int, not the reverse.

**Warning signs:** If after the fix, running `dotnet run --project flow-interpreter tests/test_*.flow` shows new failures, the fix was too broad.

### Pitfall 2: Multiple Bare Expressions in Sections

**What goes wrong:** Only collecting `_lastExpressionValue` captures only the LAST bare expression. If a section has multiple bare note streams, only the last one is captured.

**Why it happens:** `_lastExpressionValue` is overwritten on each `ExpressionStatement`.

**How to avoid:** Collect ALL expression statement results during section body execution, not just the last one. Track a `List<Value>` during section execution and add each `ExpressionStatement` result to it if it's SequenceData.

```csharp
// Better approach: collect during execution
var expressionResults = new List<Value>();
foreach (var stmt in section.Body)
{
    ExecuteStatement(stmt);
    if (stmt is ExpressionStatement && _lastExpressionValue?.Data is SequenceData)
        expressionResults.Add(_lastExpressionValue);
    if (_returnValue != null) break;
}
```

### Pitfall 3: --verbose Output Noise

**What goes wrong:** Dumping every function resolution attempt makes output unusable. There are hundreds of built-in function calls per script.

**How to avoid:** Only show verbose output for: (1) module loads, (2) failed overload resolutions (all candidates tried), (3) section rendering summaries, (4) `_manager` set/restore. Do NOT show successful resolutions unless a second `--trace` level is added later.

### Pitfall 4: TryResolveFunction Silently Eats Errors

**What goes wrong:** `TryResolveFunction` (ExecutionContext.cs:158) creates a temporary `ErrorReporter` and throws it away. This means overload resolution failures are completely invisible -- the caller just gets `null` and falls through to `ResolveFunction` which reports the error but execution continues past `Value.Void()` return.

**How to avoid:** For FIX-03, the fix is at the call site in `ExpressionEvaluator.EvaluateFunctionCall` (line 186-191). When `overload == null` after both `TryResolveFunction` and `ResolveFunction`, the code correctly reports an error and returns `Value.Void()`. The problem is that callers of the function call (upstream in the expression tree) treat `Value.Void()` as a valid result. The fix is to add a way to distinguish "function returned void" from "function resolution failed and we returned void as a sentinel."

### Pitfall 5: Race Condition in Static Manager Restore

**What goes wrong:** In `LiveReloadManager.RenderScript` (line 346-355), the save/restore of `PlaybackFunctions._manager` is not in a `finally` block. If `engine.Execute()` throws, the manager is not restored and the streaming loop breaks.

**How to avoid:** Move the restore into a `finally` block. Also, the current code checks `if (savedManager != null)` before restoring, but `savedManager` could legitimately be null (first engine creation). Change to unconditional restore.

## Code Examples

### QOL-01: Adding --verbose Flag to CLI

```csharp
// In Program.cs ParseFlags():
case "--verbose" or "-v":
    verbose = true;
    i++;
    break;

// In CliFlags record:
record CliFlags(string? ScriptPath, string? EvalCode, string? DeviceName,
                bool Watch, bool ShowHelp, bool ReadStdin, bool Verbose);

// Threading through FlowEngine:
public FlowEngine(bool verbose = false)
{
    _diagnosticOutput = verbose ? Console.Error : null;
    // ... pass to OverloadResolver, ModuleLoader
}
```

### FIX-01: Making transpose Accept Int (Add Overload)

```csharp
// In TransformFunctions.RegisterTranspose():
// transpose(Sequence, Int) -- treat Int as semitone count
var transposeIntSig = new FunctionSignature("transpose",
    [SequenceType.Instance, IntType.Instance]);
registry.Register("transpose", transposeIntSig, TransposeInt);

// Implementation:
private static Value TransposeInt(IReadOnlyList<Value> args)
{
    // Delegate to the Semitone version
    return TransposeSemitone([args[0], Value.Semitone(args[1].As<int>())]);
}
```

And in `std.flow`:
```
internal proc transpose (Sequence: seq, Int: semitones)
```

### FIX-02: Section Bare Expression Collection

```csharp
// In ExecuteSectionDeclaration, replace the existing collection loop:
var sequences = new Dictionary<string, SequenceData>();
var anonIndex = 0;

// Collect bare expression results
foreach (var stmt in section.Body)
{
    ExecuteStatement(stmt);
    // Capture bare expressions that produce sequences
    if (stmt is ExpressionStatement && _lastExpressionValue?.Data is SequenceData exprSeq)
    {
        sequences[$"_anon_{anonIndex++}"] = exprSeq;
    }
    if (_returnValue != null) break;
}

// Also collect named variables (existing behavior)
foreach (var (name, value) in _context.CurrentFrame.GetLocalVariables())
{
    if (value.Data is SequenceData seq && !sequences.ContainsValue(seq))
    {
        sequences[name] = seq;
    }
}
```

### FIX-04: Proper Save/Restore in LiveReloadManager

```csharp
// In LiveReloadManager.RenderScript():
var savedManager = PlaybackFunctions.GetManager();
try
{
    using var engine = new FlowEngine();
    engine.AudioManager.CaptureMode = true;
    engine.Execute(source, filePath);
    
    musicalContext = engine.Context.GetMusicalContext();
    var buffer = engine.AudioManager.GetCapturedBuffer();
    // ... rest of buffer extraction
    return buffer;
}
finally
{
    PlaybackFunctions.SetManager(savedManager!);
}
```

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | .flow test scripts (no unit test framework) |
| Config file | None -- tests are .flow scripts executed directly |
| Quick run command | `dotnet run --project flow-interpreter tests/test_comprehensive.flow` |
| Full suite command | `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| QOL-01 | --verbose shows resolution details | smoke | `dotnet run --project flow-interpreter -- --verbose -e 'use "@std"; Sequence s = \| C4 D4 \|; Sequence t = s -> transpose(+2st);'` 2>&1 | grep -q "\[verbose\]"` | No -- Wave 0 |
| FIX-01a | transpose(Sequence, Int) works | integration | `dotnet run --project flow-interpreter tests/test_transpose_int.flow` | No -- Wave 0 |
| FIX-01b | vary(Sequence, Double) works with @composition | integration | `dotnet run --project flow-interpreter tests/test_vary.flow` | No -- Wave 0 |
| FIX-02 | Bare note stream in section renders audio | integration | `dotnet run --project flow-interpreter tests/test_section_bare_expr.flow` | No -- Wave 0 |
| FIX-03 | Function-not-found returns error exit code | smoke | `dotnet run --project flow-interpreter -e 'use "@std"; (nonexistent 42)'; echo $?` should output 1 | No -- Wave 0 |
| FIX-04 | Watch mode save/restore uses finally | unit (manual review) | Code review -- verify `finally` block in `RenderScript` | Manual-only |

### Sampling Rate
- **Per task commit:** `dotnet run --project flow-interpreter tests/test_comprehensive.flow`
- **Per wave merge:** Full test suite (`for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done`)
- **Phase gate:** Full suite green before verify

### Wave 0 Gaps
- [ ] `tests/test_transpose_int.flow` -- covers FIX-01a (transpose with Int argument)
- [ ] `tests/test_vary.flow` -- covers FIX-01b (vary with @composition import)
- [ ] `tests/test_section_bare_expr.flow` -- covers FIX-02 (bare expressions in sections)
- [ ] Verbose flag smoke test script or inline command -- covers QOL-01

## Open Questions

1. **Should `SemitoneType.IsCompatibleWith(IntType)` be added globally, or just a new `transpose(Sequence, Int)` overload?**
   - What we know: The `Int -> Semitone` compatibility would affect ALL functions taking Semitone, not just transpose. Functions like `trill(Sequence, Semitone)` would also start accepting Int.
   - What's unclear: Whether users expect `trill(seq, 2)` to work (treating 2 as semitones).
   - Recommendation: Add `IsCompatibleWith` on `SemitoneType` for `IntType`. The semantic is clear: an integer IS a valid number of semitones. This fixes `transpose`, `trill`, and `repeat(Sequence, Int, Semitone)` all at once. Also add `CentType.IsCompatibleWith(DoubleType)` for consistency.

2. **Should `vary` be added to `std.flow` or require explicit `@composition` import?**
   - What we know: `transpose`, `invert`, `retrograde`, etc. are in `std.flow`. `vary` is in `composition.flow` alongside polyrhythm functions.
   - Recommendation: Add `vary` overloads to `std.flow` alongside the other transforms. It is a core transform, not a composition-specific utility. This eliminates the "function not found" confusion.

3. **How should FIX-03 distinguish "function returned void" from "resolution failed, returning void sentinel"?**
   - What we know: `EvaluateFunctionCall` returns `Value.Void()` on resolution failure. Upstream code cannot distinguish this from a legitimate void return.
   - Recommendation: For v1.1, the practical fix is ensuring `FlowEngine.Execute()` returns `false` when the error reporter has any errors. Currently `TryResolveFunction` silently swallows errors (temp reporter), so the real reporter never sees them. Fix: when `TryResolveFunction` returns null AND the variable lookup also fails, `ResolveFunction` reports the error (this already happens at line 189). The issue is that `Execute()` returns `!_errorReporter.HasErrors` but errors reported during interpretation ARE in the reporter. Verify this path works end-to-end; if it does, FIX-03 may already be partially working and just needs the verbose output to make it visible.

## Sources

### Primary (HIGH confidence -- direct code reading)
- `flow-lang/TypeSystem/OverloadResolver.cs` -- full overload resolution logic confirmed
- `flow-lang/TypeSystem/FunctionSignature.cs:52-112` -- 4-way compatibility check in `Matches()`
- `flow-lang/TypeSystem/FlowType.cs:16-19` -- base `IsCompatibleWith` uses `Equals` (GetType equality)
- `flow-lang/TypeSystem/SpecialTypes/SequenceType.cs:80-89` -- no `IsCompatibleWith` override (inherits base)
- `flow-lang/TypeSystem/SpecialTypes/SemitoneType.cs` -- no `IsCompatibleWith` override, no Int compatibility
- `flow-lang/TypeSystem/PrimitiveTypes/VoidType.cs:17-28` -- wildcard behavior confirmed
- `flow-lang/Interpreter/Interpreter.cs:336-377` -- `ExecuteSectionDeclaration` scope-only collection confirmed
- `flow-lang/Interpreter/Interpreter.cs:110-113` -- `_lastExpressionValue` set on ExpressionStatement
- `flow-lang/Interpreter/ExpressionEvaluator.cs:160-205` -- function call resolution path with fallback to variable
- `flow-lang/Runtime/ExecutionContext.cs:105-176` -- `ResolveFunction` and `TryResolveFunction` dual path
- `flow-lang/Diagnostics/ErrorReporter.cs` -- error accumulation model, no severity-based filtering
- `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs:15-26` -- static `_manager`, GetManager/SetManager
- `flow-interpreter/LiveReloadManager.cs:344-356` -- save/restore pattern without `finally`
- `flow-interpreter/Program.cs:156-221` -- CLI flag parsing, no --verbose support yet
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:93-104` -- transpose registered with Semitone, not Int
- `flow-lang/StandardLibrary/Composition/VariationFunctions.cs:23-51` -- vary registered with 6 overloads
- `flow-lang/composition.flow:165-180` -- vary internal proc declarations
- `flow-lang/std.flow:78-89` -- transforms in std.flow, vary NOT included
- `flow-lang/Core/FlowEngine.cs:64-96` -- Execute() returns `!HasErrors`, clears errors at start

### Secondary (MEDIUM confidence -- analysis of code interaction patterns)
- Error masking flow: TryResolveFunction -> temp reporter -> null -> ResolveFunction -> real reporter -> Void return -> caller continues. Traced through three files but not runtime-verified.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new dependencies, all files identified and read
- Architecture: HIGH -- all five bugs/features have confirmed root causes from code reading
- Pitfalls: HIGH -- root causes verified against actual code, not hypothesized

**Research date:** 2026-04-02
**Valid until:** 2026-05-02 (stable codebase, no external dependency changes)
