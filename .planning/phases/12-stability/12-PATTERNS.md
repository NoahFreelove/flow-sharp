# Phase 12: Stability - Pattern Map

**Mapped:** 2026-04-19
**Files analyzed:** 13 new/modified (8 new + 5 modified)
**Analogs found:** 13 / 13

---

## Context Corrections from RESEARCH

These upstream-context facts are wrong and MUST NOT be propagated into PLAN.md files:

| Claim (source) | Wrong value | Correct value | Authority |
|----------------|-------------|---------------|-----------|
| Target framework (`CLAUDE.md` §Constraints) | `net9.0` | `net10.0` | `flow-lang/flow-lang.csproj:4`, `flow-interpreter/flow-interpreter.csproj:9`, `flow-midi/flow-midi.csproj:4` all `<TargetFramework>net10.0</TargetFramework>` |
| Solution file (CONTEXT canonical_refs, line 136) | `flow-lang.sln` | `flow-sharp.sln` | Only `flow-sharp.sln` exists at repo root |
| FIX-07a return-line numbers (CONTEXT D-03) | 151, 164, 178, 224, 240, 255, 263 | **Re-grep at implementation time** — actual lines drift by +1 (ReportError calls sit on CONTEXT's cited line; `return;` is on the next line: 152, 165, 179, 225, 241, 256, 264 as of 2026-04-19) | RESEARCH §"Pattern 4" verification |
| "exportWav auto-mkdir matches writeWav" (CONTEXT D-02) | Implies writeWav already creates parent dirs | Neither writeWav nor exportWav currently auto-mkdir. Both share `ExportWavInternal` helper at `FileIO.cs:41-63`. Edit the helper ONCE to fix both. | Read verbatim from `FileIO.cs:57` — `FileStream` opened directly, no `Directory.CreateDirectory` anywhere |
| `if(Bool, String, String)` overload sufficient (CONTEXT D-16) | String-specific overload fixes `test_custom_oscillator` | `test_custom_oscillator.flow:57` also needs `(Bool, Double, Double)`; register ONE wildcard `(Bool, Void, Void)` overload using the VoidType wildcard convention (`BuiltInFunctions.cs:231-234` comment authorizes). | RESEARCH Pitfall 5 |

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| **NEW** `flow-lang.Tests/flow-lang.Tests.csproj` | test-project csproj | build-config | `flow-midi/flow-midi.csproj` + `flow-lang/flow-lang.csproj` (ItemGroup for PackageRef) | role-match (first xUnit project in repo — greenfield for xUnit packages; csproj skeleton is exact match) |
| **NEW** `flow-lang.Tests/Fixtures/FlowEngineRunner.cs` | test fixture | request-response (drives `FlowEngine.Execute`) | `flow-interpreter/Program.cs:67-92` `RunFromString` | exact (same in-process invocation pattern; adds stdout capture) |
| **NEW** `flow-lang.Tests/FlowScriptTests.cs` | test class (wrap-as-Theory) | batch data-driven | No xUnit analog in repo — greenfield. Structural template: RESEARCH §"Pattern 2" | no-analog (use RESEARCH canonical xUnit pattern) |
| **NEW** `flow-lang.Tests/FlowScriptData.cs` | MemberData provider | file-glob enumeration | `.flow` module loader uses `File.Exists` / path resolution in `flow-lang/Runtime/ModuleLoader.cs`; closest is a static helper | partial-match (glob pattern is standard .NET) |
| **NEW** `flow-lang.Tests/Unit/CollectionsTests.cs` | unit test (xUnit Facts) | CRUD validation | Inline code sample RESEARCH lines 637-678 + target `Collections.Init` at `Collections.cs:84-92` | exact (test mirrors fixed SUT directly) |
| **NEW** `flow-lang.Tests/Unit/ThunkTests.cs` | unit test (xUnit Facts) | request-response w/ mock | Inline code sample RESEARCH lines 682-733 + target `Thunk` at `Runtime/Thunk.cs` | exact |
| **NEW** `flow-lang.Tests/Unit/InterpreterTests.cs` | unit test (xUnit Theory) | request-response via FlowEngineRunner | Inline code sample RESEARCH lines 738-776; uses FlowEngineRunner from Fixtures | exact |
| **MODIFIED** `flow-lang/StandardLibrary/Collections.cs:84-92` | stdlib (CRUD, array primitive) | transform w/ validation | `Collections.Head` at `Collections.cs:48-59` + `Collections.Last` at `Collections.cs:71-82` | exact (same file, same validation shape) |
| **MODIFIED** `flow-lang/Runtime/Thunk.cs` (full file, 49 lines) | runtime primitive (lazy eval cache) | memoize w/ exception cache | Current `Thunk.Force` at `Thunk.cs:27-46` (manual lock+cache pattern to be replaced) + BCL `Lazy<T>` canonical template from RESEARCH §"Pattern 3" | role-match (rewrite using .NET BCL `Lazy<T>`; no existing Lazy<T> usage in flow-lang to mirror) |
| **MODIFIED** `flow-lang/Interpreter/Interpreter.cs` (7 `return;`→`break;` inside `ExecuteMusicalContext`) | interpreter (statement dispatch) | event-driven switch | Sibling case branches in the same switch — `MusicalContextType.Dynamics` at lines 184-191 and `Rit` at lines 193-203 already use `break;` correctly (they are the post-fix template) | exact (same file, same switch, already-correct siblings serve as the template) |
| **MODIFIED** `flow-lang/Interpreter/Interpreter.cs:292` (AUDIT-VERIFIED marker update) | doc comment | static annotation | `BufferHelpers.cs:128`, `Interpreter.cs:75`, `EnvelopeProcessor.cs:105` — Phase 11 AUDIT-VERIFIED markers (Dismissed verdicts) | exact (same format, new date + "Fixed" verdict) |
| **MODIFIED** `flow-lang/StandardLibrary/Audio/FileIO.cs:41-63` (auto-mkdir in `ExportWavInternal`) | stdlib (file I/O) | file-I/O | Same file, same method — standalone hardening (2-line addition, no analog needed inside flow-lang) | greenfield-within-file |
| **MODIFIED** `flow-lang/StandardLibrary/StdLib.cs` + `flow-lang/StandardLibrary/BuiltInFunctions.cs:211-213` (new `if` wildcard overload) | stdlib registration + implementation | overload-dispatch | `StdLib.Equals` / `StdLib.LessThan` etc. at `BuiltInFunctions.cs:232-264` (Void-wildcard overload convention; "VoidType.Instance is used as a wildcard" authoritative comment) | exact (established convention, direct template) |
| **MODIFIED** `tests/test_musical_context_errors.flow` (sentinel string flip) | integration test script | string-literal edit | RESEARCH Pitfall 1 dictates post-FIX label | pitfall-driven (no pattern to copy; edit the string) |
| **MODIFIED** `flow-sharp.sln` (add `flow-lang.Tests` project) | solution file | build wiring | Existing `flow-midi` entry (lines 10, 30-33) | exact (same Project/EndProject + ProjectConfigurationPlatforms block format; generate GUID via `dotnet sln add`) |

---

## Pattern Assignments

### 1. `flow-lang.Tests/flow-lang.Tests.csproj` (test-project csproj)

**Analog:** `flow-midi/flow-midi.csproj` (structure) + RESEARCH §"Standard Stack" (xUnit packages)

**Property group pattern** (copy shape from `flow-midi/flow-midi.csproj:1-8`, override `<OutputType>` — test projects use the default Library output, not Exe):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>FlowLang.Tests</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <!-- Source: RESEARCH.md Standard Stack table -->
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="xunit.v3" Version="3.2.2" />
    <PackageReference Include="xunit.v3.runner.visualstudio" Version="3.2.2" />
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\flow-lang\flow-lang.csproj" />
  </ItemGroup>
</Project>
```

**Constraints from CLAUDE.md §"C# Conventions":** `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, file-scoped namespace under `FlowLang.Tests`. All three match existing flow-lang/flow-interpreter csproj patterns.

**Fallback:** RESEARCH §"Standard Stack" notes the SDK 10.0.106 `dotnet new xunit` template may default to xunit v2. If v3 friction appears, substitute `xunit` 2.9.3 + `xunit.runner.visualstudio` 3.x — API surface for `[Fact]`/`[Theory]`/`[MemberData]` is identical.

---

### 2. `flow-lang.Tests/Fixtures/FlowEngineRunner.cs` (test fixture)

**Analog:** `flow-interpreter/Program.cs:67-92` (`RunFromString`)

**Imports pattern** (mirror flow-interpreter/Program.cs top, adapt):
```csharp
using FlowLang.Core;
using FlowLang.Diagnostics;

namespace FlowLang.Tests.Fixtures;
```

**Core pattern** (copy `RunFromString` shape at `flow-interpreter/Program.cs:67-92`, adapt to capture stdout/stderr + return structured tuple):
```csharp
// Source adaptation: flow-interpreter/Program.cs:67-92 RunFromString
public sealed class FlowEngineRunner : IDisposable
{
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();
    private readonly TextWriter _origOut;
    private readonly TextWriter _origErr;
    private readonly FlowEngine _engine;

    public FlowEngineRunner(bool verbose = false)
    {
        _origOut = Console.Out;
        _origErr = Console.Error;
        Console.SetOut(_stdout);
        Console.SetError(_stderr);
        _engine = new FlowEngine(verbose);  // mirrors Program.cs:71
    }

    public (bool Success, string Stdout, string Stderr, int ErrorCount) RunFile(string path)
    {
        var source = File.ReadAllText(path);
        var success = _engine.Execute(source, path);  // mirrors Program.cs:73
        return (success, _stdout.ToString(), _stderr.ToString(), _engine.ErrorReporter.ErrorCount);
    }

    public (bool Success, string Stdout, string Stderr, int ErrorCount) RunSource(string source, string fileName = "<test>")
    {
        var success = _engine.Execute(source, fileName);
        return (success, _stdout.ToString(), _stderr.ToString(), _engine.ErrorReporter.ErrorCount);
    }

    public void Dispose()
    {
        _engine.Dispose();            // mirrors Program.cs:71 `using var engine`
        Console.SetOut(_origOut);
        Console.SetError(_origErr);
    }
}
```

**FlowEngine API signatures used (verified at `flow-lang/Core/FlowEngine.cs`):**
- `public FlowEngine(bool verbose = false)` — line 34
- `public bool Execute(string source, string? fileName = null)` — line 59
- `public ErrorReporter ErrorReporter => _errorReporter;` — line 25
- `public void Dispose()` — line 121

**Concurrency constraint:** `Console.SetOut` is process-global. Pair this fixture with `[CollectionDefinition("FlowScripts", DisableParallelization = true)]` on Theory classes. See RESEARCH Pitfall 4.

---

### 3. `flow-lang.Tests/FlowScriptTests.cs` (wrap-as-Theory)

**Analog:** Greenfield — no in-repo analog. Canonical xUnit pattern from RESEARCH §"Pattern 2" lines 239-289.

**Template** (copy directly from RESEARCH §"Pattern 2"):
```csharp
using Xunit;
using FlowLang.Tests.Fixtures;

namespace FlowLang.Tests;

[CollectionDefinition("FlowScripts", DisableParallelization = true)]
public class FlowScriptsCollection { }

[Collection("FlowScripts")]
public class FlowScriptTests
{
    [Theory]
    [MemberData(nameof(FlowScriptData.GetFlowScripts), MemberType = typeof(FlowScriptData))]
    public void RunsToCompletion(string relativePath)
    {
        var absolute = Path.Combine(FlowScriptData.FindTestsRoot(), relativePath);
        using var runner = new FlowEngineRunner();
        var (_, stdout, stderr, errorCount) = runner.RunFile(absolute);

        if (FlowScriptData.ExpectedErrorScripts.TryGetValue(relativePath, out var expectedStderr))
        {
            Assert.Contains(expectedStderr, stderr);
        }
        else
        {
            Assert.True(errorCount == 0,
                $"Script {relativePath} reported {errorCount} error(s):\n{stderr}");
        }
    }
}
```

**Spike c1 RED→GREEN flip mechanism (plan 12-04):** The entry for `spike/c1-musical-context-body.flow` in `ExpectedErrorScripts` (or a parallel `RequiredSentinels` dictionary) asserts presence of `c1-probe1-body-ran`, `c1-probe2-stmt1`, `c1-probe2-stmt2`, `c1-probe3-body-ran` on stdout. Pre-fix: those sentinels absent → assertion fails (RED). Post-fix: sentinels present → assertion passes (GREEN). Plan 12-01 commits the Theory row RED; plan 12-04 flips it GREEN via the `return;`→`break;` interpreter edit.

---

### 4. `flow-lang.Tests/FlowScriptData.cs` (MemberData provider)

**Analog:** No direct analog — standard .NET file enumeration.

**Template:**
```csharp
namespace FlowLang.Tests;

public static class FlowScriptData
{
    public static IEnumerable<object[]> GetFlowScripts()
    {
        var testsRoot = FindTestsRoot();
        foreach (var path in Directory.EnumerateFiles(testsRoot, "*.flow", SearchOption.AllDirectories))
        {
            // Skip tests/std.flow if present — it's a stdlib module, not a test
            if (Path.GetFileName(path) == "std.flow") continue;
            yield return new object[] { Path.GetRelativePath(testsRoot, path) };
        }
    }

    public static string FindTestsRoot()
    {
        // Walk up from AppContext.BaseDirectory (bin/Debug/net10.0/) until we find tests/
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests")))
            dir = dir.Parent;
        return dir != null ? Path.Combine(dir.FullName, "tests")
            : throw new DirectoryNotFoundException("Could not locate tests/ directory");
    }

    public static readonly Dictionary<string, string> ExpectedErrorScripts = new()
    {
        // Populate per RESEARCH §"Pattern 2" lines 278-284 + Pitfall 1
        ["test_error_masking.flow"] = "Function 'nonExistentFunction' not found",
        ["test_musical_context_errors.flow"] = "Tempo must be positive",
        // Plan 12-01 commits this as RED; plan 12-04 fix flips it GREEN:
        ["spike/c1-musical-context-body.flow"] = "Tempo must be positive",
    };
}
```

**Anti-pattern (RESEARCH §"Anti-Patterns"):** Don't use `Environment.CurrentDirectory` — it varies by test runner launch location. Always walk up from `AppContext.BaseDirectory`.

---

### 5. `flow-lang.Tests/Unit/CollectionsTests.cs` (FIX-05 unit test)

**Analog:** RESEARCH §"Code Examples" lines 637-678 + SUT at `Collections.cs:84-92`

**Imports pattern** (mirror `Collections.cs:1-4`):
```csharp
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Unit;
```

**Core test pattern** (copy directly from RESEARCH lines 648-678):
```csharp
public class CollectionsTests
{
    [Fact]
    public void Init_EmptyArray_ThrowsInvalidOperationException()
    {
        var emptyArray = Value.Array(new List<Value>(), VoidType.Instance);
        var ex = Assert.Throws<InvalidOperationException>(
            () => Collections.Init(new[] { emptyArray }));
        Assert.Equal("Cannot get init of empty array", ex.Message);
    }

    [Fact]
    public void Init_SingleElementArray_ReturnsEmpty()
    {
        var arr = Value.Array(new List<Value> { Value.Int(42) }, IntType.Instance);
        var result = Collections.Init(new[] { arr });
        var elements = result.As<IReadOnlyList<Value>>();
        Assert.Empty(elements);
    }

    [Fact]
    public void Init_MultipleElements_ReturnsAllButLast()
    {
        var arr = Value.Array(
            new List<Value> { Value.Int(1), Value.Int(2), Value.Int(3) },
            IntType.Instance);
        var result = Collections.Init(new[] { arr });
        var elements = result.As<IReadOnlyList<Value>>();
        Assert.Equal(2, elements.Count);
        Assert.Equal(1, elements[0].As<int>());
        Assert.Equal(2, elements[1].As<int>());
    }
}
```

---

### 6. `flow-lang.Tests/Unit/ThunkTests.cs` (FIX-06 unit test)

**Analog:** RESEARCH §"Code Examples" lines 682-733

**Imports and template:**
```csharp
using FlowLang.Runtime;
using FlowLang.Ast;
using FlowLang.Interpreter;
using Xunit;

namespace FlowLang.Tests.Unit;

public class ThunkTests
{
    [Fact]
    public void Force_CachesSuccessValue() { /* RESEARCH lines 690-703 */ }

    [Fact]
    public void Force_CachesExceptionAndRethrows() { /* RESEARCH lines 705-721 */ }

    [Fact]
    public void Force_RethrowPreservesStackTrace() { /* RESEARCH lines 723-732 */ }
}
```

**Open question flagged by RESEARCH line 736:** The three tests need `CountingEvaluator`/`FakeExpression` test doubles. Implementer should either (a) write minimal mocks implementing `ExpressionEvaluator`'s public surface, or (b) construct a real `ExpressionEvaluator` + a `LiteralExpression` wrapping a lambda that throws. Planner should call this out as a non-trivial Claude's Discretion in plan 12-03.

---

### 7. `flow-lang.Tests/Unit/InterpreterTests.cs` (FIX-07a unit test)

**Analog:** RESEARCH §"Code Examples" lines 738-776 + FlowEngineRunner fixture

**Template:**
```csharp
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit;

[Collection("FlowScripts")]   // serialize Console.SetOut with wrap-as-Theory
public class ExecuteMusicalContextTests
{
    [Fact]
    public void BadTempo_BodyStillRuns_ErrorReported()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, stderr, errorCount) = runner.RunSource(@"
tempo -5 {
    (print ""body-ran"")
}
(print ""after-block"")
");
        Assert.Contains("body-ran", stdout);
        Assert.Contains("after-block", stdout);
        Assert.True(errorCount >= 1);
        Assert.Contains("Tempo must be positive", stderr);
    }

    [Theory]
    [InlineData("tempo -5", "Tempo must be positive")]
    [InlineData("swing 2.0", "Swing must be between 0.0 and 1.0")]
    [InlineData("gain 5.0", "Gain must be between 0.0 and 2.0")]
    [InlineData("pan 2.0", "Pan value must be between -1.0 and 1.0")]
    [InlineData("key NotAKey", "Unrecognized key")]
    public void ValidationPath_BodyRunsUnderDefaultContext(string contextDecl, string expectedError)
    {
        using var runner = new FlowEngineRunner();
        var source = $"{contextDecl} {{ (print \"body-ran\") }}";
        var (_, stdout, stderr, _) = runner.RunSource(source);
        Assert.Contains("body-ran", stdout);
        Assert.Contains(expectedError, stderr);
    }
}
```

Expected-error strings verified verbatim against `Interpreter.cs:164,178,224,240,254,263` ReportError messages.

---

### 8. `flow-lang/StandardLibrary/Collections.cs:84-92` — FIX-05 target

**Analog:** Same file, `Collections.Head` at lines 48-59 and `Collections.Last` at lines 71-82.

**Current code (lines 84-92) — the bug:**
```csharp
public static Value Init(IReadOnlyList<Value> args)
{
    var arr = args[0];
    if (arr.Type is not ArrayType arrayType)
        throw new InvalidOperationException($"Expected Array, got {arr.Type}");

    var elements = arr.As<IReadOnlyList<Value>>();
    return Value.Array(elements.Take(elements.Count - 1).ToArray(), arrayType.ElementType);
    // BUG: when elements.Count == 0, this computes Take(-1) which returns empty silently.
}
```

**Analog template — `Collections.Head` at lines 48-59 (VERBATIM from file):**
```csharp
public static Value Head(IReadOnlyList<Value> args)
{
    var arr = args[0];
    if (arr.Type is not ArrayType)
        throw new InvalidOperationException($"Expected Array, got {arr.Type}");

    var elements = arr.As<IReadOnlyList<Value>>();
    if (elements.Count == 0)
        throw new InvalidOperationException("Cannot get head of empty array");

    return elements[0];
}
```

**Analog template — `Collections.Last` at lines 71-82 (VERBATIM):**
```csharp
public static Value Last(IReadOnlyList<Value> args)
{
    var arr = args[0];
    if (arr.Type is not ArrayType)
        throw new InvalidOperationException($"Expected Array, got {arr.Type}");

    var elements = arr.As<IReadOnlyList<Value>>();
    if (elements.Count == 0)
        throw new InvalidOperationException("Cannot get last of empty array");

    return elements[^1];
}
```

**Post-FIX-05 `Init`** (mirror Last exactly — retain the `arrayType` local since `Init` must pass `arrayType.ElementType`):
```csharp
public static Value Init(IReadOnlyList<Value> args)
{
    var arr = args[0];
    if (arr.Type is not ArrayType arrayType)
        throw new InvalidOperationException($"Expected Array, got {arr.Type}");

    var elements = arr.As<IReadOnlyList<Value>>();
    if (elements.Count == 0)
        throw new InvalidOperationException("Cannot get init of empty array");

    return Value.Array(elements.Take(elements.Count - 1).ToArray(), arrayType.ElementType);
}
```

**Error message format (from CONTEXT D-07 + Head/Last template):** `"Cannot get init of empty array"` — exactly matches the Head/Last suffix pattern `"Cannot get <op> of empty array"`.

**Registration (no change):** `BuiltInFunctions.cs:352-353` already registers `Init` with signature `(ArrayType(VoidType))`. Signature is unchanged — only the implementation raises instead of silently returning `[]`.

---

### 9. `flow-lang/Runtime/Thunk.cs` — FIX-06 full refactor

**Analog in-repo:** None — no existing `Lazy<T>` usage in flow-lang runtime or stdlib (grep confirms).

**Template:** RESEARCH §"Pattern 3" lines 305-339 (Microsoft Lazy<T> reference pattern).

**Current full file (49 lines, to be replaced):**
- `private Expression? _expression` — nullable, cleared post-evaluation
- `private ExpressionEvaluator? _evaluator` — nullable, cleared post-evaluation
- `private Value? _cachedValue`
- `private bool _isEvaluated`
- `private readonly object _lock`
- `public Value Force()` — double-checked lock pattern
- `public bool IsEvaluated => _isEvaluated`

**Post-fix full file (mirror RESEARCH lines 314-339):**
```csharp
using FlowLang.Ast;
using FlowLang.Interpreter;
using System.Threading;

namespace FlowLang.Runtime;

/// <summary>
/// Represents a deferred computation that can be forced to produce a value.
/// Caches both successful values and exceptions; re-throws cached exceptions
/// with the original stack trace preserved (ExceptionDispatchInfo semantics).
/// </summary>
public class Thunk
{
    private readonly Lazy<Value> _lazy;

    public Thunk(Expression expression, ExpressionEvaluator evaluator)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));
        if (evaluator == null) throw new ArgumentNullException(nameof(evaluator));

        // ExecutionAndPublication is the default for Lazy<T>(Func<T>).
        // Specifying it explicitly documents intent and guards against a
        // future .NET runtime changing the default mode.
        _lazy = new Lazy<Value>(
            () => evaluator.Evaluate(expression),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Forces evaluation. Returns the cached value if already evaluated.
    /// If the evaluator threw on first access, re-throws the same exception
    /// with the original stack trace preserved.
    /// </summary>
    public Value Force() => _lazy.Value;

    public bool IsEvaluated => _lazy.IsValueCreated;
}
```

**Why `Lazy<Value>` alone satisfies D-05 (ExceptionDispatchInfo):** Microsoft docs verified in RESEARCH lines 296-302: `Lazy<T>` internally uses `ExceptionDispatchInfo.Capture` + `.Throw()` to cache and re-throw with preserved stack. Manual `ExceptionDispatchInfo` field is redundant.

**Public API preservation:** Constructor signature `(Expression, ExpressionEvaluator)` is unchanged; `Force()` signature unchanged; `IsEvaluated` property unchanged. Downstream callers (`StdLib.If` at `StdLib.cs:339,343`, `StdLib.Eval` at `StdLib.cs:327`, `StdLib.And/Or` at `StdLib.cs:360,365`) need NO changes.

**Memory note:** Current code clears `_expression`/`_evaluator` after evaluation to permit GC (lines 39-40). The `Lazy<T>` version holds the closure for the Lazy's lifetime — closure captures `expression` and `evaluator`. After `_lazy.Value` is materialized, the factory closure becomes unreachable via Lazy internals (Microsoft guarantees this). Net GC behavior is equivalent.

---

### 10. `flow-lang/Interpreter/Interpreter.cs` — FIX-07a `return;`→`break;`

**Analog:** In-file siblings — `MusicalContextType.Dynamics` (lines 184-191) and `MusicalContextType.Rit` (lines 193-203) already end in `break;` and are the post-fix target shape.

**Exact lines to edit (re-grep at implementation time — CONTEXT line numbers are stale):**

| Case | ReportError line | `return;` line (to become `break;`) | Trigger |
|------|------------------|--------------------------------------|---------|
| Timesig | 151 | 152 | `ArgumentException` from `new TimeSignatureData(...)` |
| Tempo | 163-164 | 165 | `IsValidTempo(tempo)` false |
| Swing | 177-178 | 179 | `IsValidSwing(swing)` false |
| Pan | 223-224 | 225 | `pan < -1.0 || pan > 1.0` |
| Gain | 239-240 | 241 | `gain < 0.0 || gain > 2.0` |
| Key (invalid name) | 253-255 | 256 | `!IsValidKey(keyName)` |
| Key (non-literal) | 262-263 | 264 | `ctx.Value is not LiteralExpression` |

**Sibling-case template (lines 184-191 — `MusicalContextType.Dynamics`, already-correct `break;` shape; CITED VERBATIM from Interpreter.cs):**
```csharp
case MusicalContextType.Dynamics:
    var velVal = _evaluator.Evaluate(ctx.Value);
    double vel = velVal.Type is IntType
        ? (double)velVal.As<int>()
        : velVal.As<double>();
    vel = Math.Clamp(vel, 0.0, 1.0);
    musicalCtx.Velocity = vel;
    break;
```

**Frame-balance framing (lines 131-291 — MUST NOT be altered):**
```csharp
private void ExecuteMusicalContext(MusicalContextStatement ctx)
{
    _context.PushFrame();   // line 133
    try
    {
        // switch ... (lines 138-267)
        _context.CurrentFrame.MusicalContext = musicalCtx;  // line 269

        foreach (var stmt in ctx.Body)  // line 271-285 - body loop
        {
            ExecuteStatement(stmt);
            // ... section-capture hook
            if (_returnValue != null) break;
        }
    }
    finally
    {
        _context.PopFrame();  // line 289
    }
}
```

Post-fix: on validation failure, `break;` exits the switch → falls through to line 269 (MusicalContext assignment; `musicalCtx` has default values since the valid case never ran) → body loop runs under partial/default context → `finally` still pops frame.

**AUDIT-VERIFIED marker update (line 292):**

**Existing marker (Phase 11, verbatim):**
```csharp
// AUDIT-VERIFIED 2026-04-18: C1 — Confirmed: body skipped after validation error (tests/spike/c1-musical-context-body.flow)
```

**Post-fix marker (RESEARCH line 427):**
```csharp
// AUDIT-VERIFIED 2026-04-19: C1 — Fixed (returns→breaks); body now runs under partial/default context (tests/spike/c1-musical-context-body.flow GREEN)
```

**Marker format authority** (from `BufferHelpers.cs:128`, `Interpreter.cs:75`, `EnvelopeProcessor.cs:105`): `// AUDIT-VERIFIED YYYY-MM-DD: C[N] — <verdict> (<evidence path>)`. Shape verified across all three Phase 11 markers.

---

### 11. `flow-lang/StandardLibrary/Audio/FileIO.cs:41-63` — exportWav auto-mkdir

**Analog:** Same file, `ExportWavInternal` — edit in place.

**Important finding (RESEARCH Pitfall 6):** CONTEXT D-02 says "matches writeWav". Verification: `writeWav` at `FileIO.cs:240-246` routes through the **same** `ExportWavInternal(buffer, filepath, 16)`. Patching `ExportWavInternal` fixes BOTH `exportWav` and `writeWav` in one edit.

**Current code (`FileIO.cs:41-63`, verbatim — validation at 44-49, FileStream at 57):**
```csharp
private static void ExportWavInternal(AudioBuffer buffer, string filepath, int bitDepth)
{
    // Validate inputs
    if (buffer == null)
        throw new ArgumentNullException(nameof(buffer));
    if (string.IsNullOrWhiteSpace(filepath))
        throw new ArgumentException("Filepath cannot be null or empty", nameof(filepath));
    if (bitDepth != 16 && bitDepth != 24 && bitDepth != 32)
        throw new ArgumentException($"Bit depth must be 16, 24, or 32 (got {bitDepth})", nameof(bitDepth));

    // Calculate file sizes
    int bytesPerSample = bitDepth / 8;
    int dataSize = buffer.Frames * buffer.Channels * bytesPerSample;
    int fileSize = 36 + dataSize; // 44 bytes header - 8 bytes = 36

    // Write WAV file
    using var fileStream = new FileStream(filepath, FileMode.Create, FileAccess.Write);
    using var writer = new BinaryWriter(fileStream);
    // ... etc
}
```

**Post-fix insertion** (RESEARCH lines 572-579) — 4 lines added after the 3 validation checks, before `FileStream`:
```csharp
    // Ensure parent directory exists (idempotent — no-op if present).
    // Benefits both exportWav and writeWav via shared helper.
    var dir = Path.GetDirectoryName(filepath);
    if (!string.IsNullOrEmpty(dir))
        Directory.CreateDirectory(dir);

    // Write WAV file
    using var fileStream = new FileStream(filepath, FileMode.Create, FileAccess.Write);
```

**Edge cases handled:** `Path.GetDirectoryName("file.wav")` returns `""` (not null) for a bare filename — guarded by `IsNullOrEmpty`. `Directory.CreateDirectory` is idempotent — returns existing DirectoryInfo if the directory already exists. `Path.GetDirectoryName("/absolute/path/file.wav")` returns `"/absolute/path"` — created if missing.

---

### 12. `flow-lang/StandardLibrary/StdLib.cs` + `BuiltInFunctions.cs:211-213` — wildcard `if` overload

**Analog:** `BuiltInFunctions.cs:231-264` (Void-wildcard overload convention) + `StdLib.Equals`/`StdLib.LessThan` bodies.

**Established Void-wildcard convention** (VERBATIM from `BuiltInFunctions.cs:231-244`, the authoritative comment):
```csharp
// ===== Equality and Comparison Functions =====
// VoidType.Instance is used as a wildcard/"any type" parameter in these signatures.
// The overload resolver treats Void as compatible with all types, allowing these
// functions to accept arguments of any type.

var equalsSignature = new FunctionSignature(
    "equals",
    [VoidType.Instance, VoidType.Instance]);
registry.Register("equals", equalsSignature, StdLib.Equals);
```

**Existing `if` overload** (`BuiltInFunctions.cs:211-213`):
```csharp
var ifSignature = new FunctionSignature(
    "if", [BoolType.Instance, new LazyType(VoidType.Instance), new LazyType(VoidType.Instance)]);
registry.Register("if", ifSignature, StdLib.If);
```

**Existing `StdLib.If` implementation** (`StdLib.cs:331-345`, verbatim):
```csharp
public static Value If(IReadOnlyList<Value> args)
{
    var cond = args[0].As<bool>();
    var if_true = args[1].As<Thunk>();
    var otherwise = args[2].As<Thunk>();

    if (cond)
    {
        return if_true.Force();
    }
    else
    {
        return otherwise.Force();
    }
}
```

**Post-fix additions (plan 12-05):**

**(a) `StdLib.cs` — new strict implementation, placed immediately after `If` at line 345:**
```csharp
/// <summary>
/// Strict (non-Lazy) if overload. Both branches are eagerly evaluated,
/// but only the selected value is returned. Matches the Lazy-if contract
/// for concrete (non-Thunk) arguments. Uses Void-wildcard dispatch.
/// </summary>
public static Value IfStrict(IReadOnlyList<Value> args)
{
    var cond = args[0].As<bool>();
    return cond ? args[1] : args[2];
}
```

**(b) `BuiltInFunctions.cs` — new registration, placed immediately after the Lazy-if at line 213:**
```csharp
var ifStrictSignature = new FunctionSignature(
    "if", [BoolType.Instance, VoidType.Instance, VoidType.Instance]);
registry.Register("if", ifStrictSignature, StdLib.IfStrict);
```

**Overload-priority rationale** (RESEARCH Pitfall 5 lines 553-558): The `OverloadResolver` (at `TypeSystem/OverloadResolver.cs:62-82`) ranks by specificity — `LazyType<Void>` beats plain `Void` when the arg IS a Lazy. When called with a concrete `String`/`Double`/etc., the Lazy overload's `Matches(argTypes)` fails and the Void-wildcard overload is the only candidate. Both call sites (lazy-wrapped and concrete) work.

**Test scope** — verified both target sites covered:
- `test_custom_oscillator.flow:42`: `(if (gt frames1 0) "PASS" "FAIL")` → `(Bool, String, String)` → hits new overload ✓
- `test_custom_oscillator.flow:57`: `(if (lt phase2 0.5) 1.0 -1.0)` → `(Bool, Double, Double)` → hits new overload ✓

---

### 13. `tests/test_musical_context_errors.flow` — sentinel string flip (plan 12-04)

**Analog:** No pattern analog — string-literal edit driven by RESEARCH Pitfall 1.

**Current (pre-FIX-07a expectation):**
```flow
tempo -5 {
    (print "should not print - negative tempo")
}
(print "after invalid tempo block")
```

**Post-fix (after FIX-07a body-skip lands, body DOES run):**
```flow
tempo -5 {
    (print "body ran under partial tempo context")
}
(print "after invalid tempo block")
```

**Why:** After FIX-07a, the body sentinel WILL print (that's the whole point). The old label is actively misleading. Update the string in the same commit as the `return;`→`break;` edit, per plan 12-04.

**Gitignore reminder** (RESEARCH Pitfall 2): `.gitignore:7-8` matches `tests/` and `*.flow`. Use `git add -f tests/test_musical_context_errors.flow` to stage the edit.

---

### 14. `flow-sharp.sln` — add `flow-lang.Tests` project

**Analog:** Existing `flow-midi` entry at lines 10 and 30-33.

**Existing entry (verbatim from sln):**
```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "flow-midi", "flow-midi\flow-midi.csproj", "{DC930FEA-0744-46E3-9FA6-31078A59D4C2}"
EndProject
```

And in `ProjectConfigurationPlatforms`:
```
{DC930FEA-0744-46E3-9FA6-31078A59D4C2}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
{DC930FEA-0744-46E3-9FA6-31078A59D4C2}.Debug|Any CPU.Build.0 = Debug|Any CPU
{DC930FEA-0744-46E3-9FA6-31078A59D4C2}.Release|Any CPU.ActiveCfg = Release|Any CPU
{DC930FEA-0744-46E3-9FA6-31078A59D4C2}.Release|Any CPU.Build.0 = Release|Any CPU
```

**Preferred approach** (let the tool generate the GUID, don't hand-edit):
```bash
dotnet sln /home/noah/Desktop/projects/flow-sharp/flow-sharp.sln \
    add /home/noah/Desktop/projects/flow-sharp/flow-lang.Tests/flow-lang.Tests.csproj
```

This writes the Project/EndProject entry + the four ProjectConfigurationPlatforms rows automatically, matching the `flow-midi` shape.

---

## Shared Patterns

### File-scoped namespaces + nullable reference types (applies to all new .cs files)

**Source:** CLAUDE.md §"C# Conventions" + existing files
**Apply to:** `FlowEngineRunner.cs`, `FlowScriptTests.cs`, `FlowScriptData.cs`, `CollectionsTests.cs`, `ThunkTests.cs`, `InterpreterTests.cs`

**Shape (from existing `flow-lang/StandardLibrary/Collections.cs:1-8`):**
```csharp
using FlowLang.Runtime;           // relevant usings first
using FlowLang.TypeSystem;
// ...

namespace FlowLang.Tests.<Subfolder>;  // file-scoped; FlowLang.Tests root

public <class>
{
    // ...
}
```

Applied across all new .cs: `FlowLang.Tests` (top-level), `FlowLang.Tests.Fixtures`, `FlowLang.Tests.Unit`.

---

### xUnit parallelism collision mitigation (applies to all tests that call FlowEngineRunner)

**Source:** RESEARCH Pitfall 4 + §"Pattern 1 concurrency note"
**Apply to:** `FlowScriptTests` (wrap-as-Theory) AND `ExecuteMusicalContextTests` (FIX-07a unit test)

**Shape:**
```csharp
[CollectionDefinition("FlowScripts", DisableParallelization = true)]
public class FlowScriptsCollection { }

[Collection("FlowScripts")]
public class TestClass { /* ... */ }
```

**Why:** `Console.SetOut` is process-wide. Parallel `FlowEngineRunner` instances corrupt each other's captured stdout. Unit tests that DON'T use `FlowEngineRunner` (`CollectionsTests`, `ThunkTests`) may remain parallel.

---

### AUDIT-VERIFIED marker format (applies to any FIX-* post-commit annotation)

**Source:** Phase 11 D-02 convention, three existing markers:
- `flow-lang/StandardLibrary/Audio/BufferHelpers.cs:128`
- `flow-lang/Interpreter/Interpreter.cs:75`
- `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs:105`

**Apply to:** Plan 12-04 commit (updates marker at `Interpreter.cs:292`).

**Shape:**
```csharp
// AUDIT-VERIFIED YYYY-MM-DD: C[N] — <verdict> (<evidence path or note>)
```

Verdict vocabulary observed in existing markers:
- `Confirmed` — bug reproduced
- `Dismissed` — audit claim rejected with evidence
- `Fixed` — Phase 12 new verdict (post-repair, reuse on existing Confirmed lines)

---

### Soft-failure preservation (applies to FIX-07a)

**Source:** ROADMAP success criterion 5 + `Interpreter.cs` `ReportError` calls
**Apply to:** FIX-07a (plan 12-04)

**Rule:** `_errorReporter.ReportError(...)` calls at lines 151, 163-164, 177-178, 223-224, 239-240, 253-255, 262-263 MUST remain. Only the `return;` on the line following each is converted to `break;`. Violating this rule (e.g., swapping `return;` for `throw;`) breaks the accumulation contract — see RESEARCH §"Anti-Patterns to Avoid" line 433.

---

### Atomic commit per fix (applies to all 6 plans)

**Source:** ROADMAP success criterion 3 + Phase 11 D-08 precedent
**Apply to:** All plans (12-01 through 12-06)

**Rule:** Each FIX-* lands in its own commit even when bundled into the same plan (e.g., plan 12-05 has two commits: `if`-overload, then `exportWav`-mkdir). Bisectability via `git bisect` must pin the exact fix commit.

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `flow-lang.Tests/FlowScriptTests.cs` | wrap-as-Theory harness | batch data-driven | No xUnit project exists in repo. Pattern from RESEARCH §"Pattern 2" canonical template. |
| `flow-lang.Tests/FlowScriptData.cs` | MemberData glob | file enumeration | Standard .NET `Directory.EnumerateFiles` + `AppContext.BaseDirectory` walk. No in-repo analog since no test project exists. |
| `flow-lang.Tests/Unit/ThunkTests.cs` test doubles (`CountingEvaluator`, `FakeExpression`) | test mock | request-response | No existing mocks in codebase; planner must author minimal test doubles. Flagged as Claude's Discretion in plan 12-03. |

All other new/modified files have concrete analogs elsewhere in the codebase.

---

## Metadata

**Analog search scope:**
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/` (full tree)
- `/home/noah/Desktop/projects/flow-sharp/flow-interpreter/`
- `/home/noah/Desktop/projects/flow-sharp/flow-midi/`
- `/home/noah/Desktop/projects/flow-sharp/flow-editor/`
- `/home/noah/Desktop/projects/flow-sharp/flow-sharp.sln`
- `/home/noah/Desktop/projects/flow-sharp/.planning/phases/12-stability/12-CONTEXT.md`
- `/home/noah/Desktop/projects/flow-sharp/.planning/phases/12-stability/12-RESEARCH.md`

**Files read in full (analog extraction):**
- `flow-lang/StandardLibrary/Collections.cs` (298 lines) — FIX-05 target + Head/Last template
- `flow-lang/Runtime/Thunk.cs` (49 lines) — FIX-06 target (full file rewrite)
- `flow-lang/StandardLibrary/Audio/FileIO.cs` (441 lines) — exportWav auto-mkdir target
- `flow-lang/Core/FlowEngine.cs` (129 lines) — FlowEngine API surface for fixture

**Files read partially (targeted line ranges):**
- `flow-lang/Interpreter/Interpreter.cs` lines 1-50, 120-180, 180-291 — FIX-07a target
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` lines 200-360, 420-447 — registration sites
- `flow-lang/StandardLibrary/StdLib.cs` lines 320-380 — If implementation
- `flow-interpreter/Program.cs` lines 60-110 — RunFromString fixture template
- `flow-lang/flow-lang.csproj`, `flow-interpreter/flow-interpreter.csproj`, `flow-midi/flow-midi.csproj` — csproj templates
- `flow-sharp.sln` (36 lines) — solution file structure

**Pattern extraction date:** 2026-04-19
