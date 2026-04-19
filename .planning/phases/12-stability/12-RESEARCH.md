# Phase 12: Stability — Research

**Researched:** 2026-04-19
**Domain:** .NET 10 interpreter stability (C#) + xUnit test harness bootstrap
**Confidence:** HIGH

## Summary

Phase 12 ships six bug fixes against a single-threaded .NET 10 interpreter and stands up the repo's first test framework (`flow-lang.Tests` xUnit project) by wrapping 70+ existing `.flow` integration test scripts as `[Theory]` cases. All six fixes are mechanically small: `init([])` error (one LINQ replacement), `Thunk` Lazy-refactor (swap the body of `Force()`), `ExecuteMusicalContext` body-skip (seven `return;` → `break;`), `if(Bool,T,T)` overloads (new registrations), and `exportWav` auto-mkdir (one `Directory.CreateDirectory` line). The heavy lifting is the test harness scaffold in plan 12-01.

Three concrete risks surfaced during research that CONTEXT.md does not address: (1) `tests/test_musical_context_errors.flow` contains `(print "should not print - negative tempo")` inside a bad-tempo block — when FIX-07a body-skip lands, this print WILL emit, directly breaking the "continues passing post-FIX-07a" claim in D-04 unless the sentinel string is inverted or the assertion strategy accommodates; (2) the project targets `net10.0` (not `net9.0` as CLAUDE.md states) and the solution is `flow-sharp.sln` at repo root (not `flow-lang.sln` as CONTEXT.md references) — plan 12-01 csproj and solution wiring must use the real targets; (3) `tests/` and `*.flow` are `.gitignore`d (see `.gitignore:7-8`), so every new test file needs `git add -f` — the spike pattern from Phase 11 applies to Phase 12 too.

**Primary recommendation:** Use `xunit.v3` 3.2.2 (January 2026 release, supports .NET 8+), back Theory data with a `MemberData` method that globs `tests/**/*.flow` at runtime (not a source generator), and implement `Thunk.Force()` as a direct wrapper over `Lazy<Value>` with its default `ExecutionAndPublication` mode — that alone gives exception caching with `ExceptionDispatchInfo`-based re-throw, no manual wiring needed.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Test scope reframing:**
- **D-01:** REQUIREMENTS.md TEST-01/02/03 edited in plan 12-06 to reflect empirical reality. TEST-01 closed (`range(Int, Int)` is already implemented — verified via `test_custom_oscillator.flow:84` Test 4 running). TEST-02 closed (`break`/`continue` already interpreted at `Interpreter.cs:120-124,321-322,354-355` — verified via `test_while_loop.flow` passing). TEST-03 reframed: `bpm()` / `createStereoTrack` / `renderBars` all implemented; actual `test_full_song.flow` failure is missing `tests/output/` parent dir. Actual `test_custom_oscillator.flow:42` failure is missing `if(Bool, String, String)` overload — not a `range` issue.
- **D-02:** `exportWav` auto-creates parent directories: one-line `Directory.CreateDirectory(Path.GetDirectoryName(path))` in `flow-lang/StandardLibrary/Audio/FileIO.cs`. "Matches writeWav" — but verification below shows writeWav does NOT currently do this either. Fix both, or document that the "matches" claim refers to the post-fix state.

**FIX-07a — ExecuteMusicalContext body-skip:**
- **D-03:** Replace 7 early `return;` at `Interpreter.cs:151,164,178,224,240,255,263` with `break;` so the body loop at `Interpreter.cs:271-285` runs under partial/default context. Frame balance via try/finally at `Interpreter.cs:287-290` must NOT be altered.
- **D-04:** Soft-failure contract reuses existing tests — no new dedicated test. Spike c1 flips RED→GREEN; `tests/test_musical_context_errors.flow` continues passing. **RESEARCH FINDING: sentinel in test_musical_context_errors.flow breaks under D-03 fix — see `## Existing Tests That Break Under FIX-07a`.**

**FIX-06 — Thunk caching:**
- **D-05:** Capture evaluator failure via `ExceptionDispatchInfo.Capture(ex)`, `.Throw()` on re-access. Preserves original stack trace.
- **D-06:** `Lazy<T>`-style refactor with `LazyThreadSafetyMode.ExecutionAndPublication`. Replace `Thunk` internal state (`_isEvaluated`, `_cachedValue`, `_evaluator`) with a `Lazy<Value>` wrapping the evaluator call.

**FIX-05 — init([]) error:**
- **D-07:** Raise `InvalidOperationException("Cannot get init of empty array")` matching `Collections.cs:78,79` format. Replace `Take(elements.Count - 1)` at `Collections.cs:84-92` with empty-check throw.

**Test framework adoption:**
- **D-08:** New `flow-lang.Tests` xUnit project added to solution. First test framework.
- **D-09:** All 70+ `.flow` test scripts migrated via wrap-as-Theory. Scripts NOT rewritten — only the runner changes.
- **D-10:** FIX-05/06/07a regression tests authored as native C# xUnit unit tests (against `Collections.Init`, `Thunk.Force`, `Interpreter.ExecuteMusicalContext`). Integration coverage via wrap-as-Theory layer alongside.
- **D-11:** `tests/spike/c1-musical-context-body.flow` registered as Theory expecting GREEN (success). Initially RED in test report (committed in plan 12-01); FIX-07a commit in plan 12-04 flips it GREEN within the same plan.

**Plan structure (6 plans):**
- **D-12:** 12-01 — xUnit harness scaffold + wrap-as-Theory migration.
- **D-13:** 12-02 — FIX-05 `init([])` error + native xUnit unit test.
- **D-14:** 12-03 — FIX-06 Thunk caching: `Lazy<Value>` + `ExecutionAndPublication` + `ExceptionDispatchInfo` + native xUnit unit test.
- **D-15:** 12-04 — FIX-07a body-skip (7 `return;`→`break;`) + AUDIT-VERIFIED 2026-04-19 marker + flips spike c1 Theory RED→GREEN in same plan.
- **D-16:** 12-05 — `if(Bool, String, String)` overload + `exportWav` auto-mkdir.
- **D-17:** 12-06 — REQUIREMENTS.md edits (close TEST-01/02, reframe TEST-03) + 12-VERIFICATION.md.
- **D-18:** Atomic commits per fix — multiple commits per plan when plan covers distinct concerns. Bisectability preserved.

### Claude's Discretion

- Exact xUnit Theory naming (`Tests.FlowScripts.test_while_loop` vs `Tests.Integration.WhileLoop`)
- Internal field names in Lazy-refactored Thunk (`_lazy`, `_dispatchInfo`)
- Wording of REQUIREMENTS.md status-update lines for closed TEST-01/02
- `[InlineData]` per script vs `[ClassData]` source generator vs `[MemberData]` glob
- Substring-assert expected-stdout fragments per Theory case

### Deferred Ideas (OUT OF SCOPE)

- CI integration of `dotnet test` (GitHub Actions etc.) — defer to later DX phase
- Native C# rewrites of selected `.flow` tests
- Lock-granularity tuning for Thunk
- DryWetMidi / PulseAudio P/Invoke isolation in xUnit (no test exercises these)
- Full `.flow` test intent rewrite (rejected in discussion — 5x scope inflation)
- REQUIREMENTS.md TEST-01/02 retained as audit-trail entries — closed-as-already-done
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| FIX-05 | `init([])` throws `InvalidOperationException("Cannot get init of empty array")` | `Collections.Head` (line 56) and `Collections.Last` (line 79) have direct-template error messages; `Collections.Init` at lines 84-92 currently returns `[]` silently via `Take(elements.Count - 1)` with `-1` argument. One-for-one translation. |
| FIX-06 | `Thunk.Force()` caches evaluator exceptions and re-raises with preserved stack | `Lazy<T>` with default `LazyThreadSafetyMode.ExecutionAndPublication` natively caches exceptions via internal `ExceptionDispatchInfo` and re-throws the SAME exception on every subsequent `.Value` access. Can also manually capture via `ExceptionDispatchInfo.Capture(ex)` + `.Throw()` — both patterns valid. |
| FIX-07a | `ExecuteMusicalContext` body runs after validation error; error reported AND body executed | Confirmed mechanism: 7 early `return;` at `Interpreter.cs:151,164,178,224,240,255,263` inside the switch exit the method before the body loop at 271-285. Replace each with `break;` to fall through to body execution under partial/default context. Frame balance (PopFrame in try/finally at 287-290) is correct and unchanged. |
| TEST-01 | `range(Int, Int)` — documented as missing, empirically ALREADY IMPLEMENTED | `test_custom_oscillator.flow:84` uses `(range 0 sz)` inside Test 4 lambda. Empirical test run confirms Test 4 would execute if Test 2 didn't halt on `if`-overload error. Plan 12-06 closes this as audit-false-positive. |
| TEST-02 | `break`/`continue` — documented as not interpreted, empirically ALREADY INTERPRETED | `Interpreter.cs:120-124` has `case BreakStatement: throw new BreakSignal();` and `case ContinueStatement: throw new ContinueSignal();`. `ExecuteForStatement`/`ExecuteWhileStatement` catch these signals. Empirical `test_while_loop.flow` passes — output `5,3,0,0,1,0,3`. Plan 12-06 closes as audit-false-positive. |
| TEST-03 | `bpm`/`createStereoTrack`/`renderBars` — documented as missing, empirically all exist; real failure is directory creation | Actual `test_full_song.flow:158-159` failure is missing `tests/output/` parent dir for `(exportWav mixed "tests/output/test_full_song.wav")`. Plan 12-06 reframes TEST-03 around dir creation + `if`-overload. |
</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| `init([])` error raising | Standard Library (Collections.cs) | — | Stdlib built-ins validate their inputs and throw; consistent with Head/Last at same file. |
| Thunk memoization + exception caching | Runtime (Thunk.cs) | — | `Thunk` is the Runtime primitive for deferred evaluation; `Lazy<T>` is the .NET primitive that already implements the full memoize-with-exception-caching contract. |
| Musical context validation + body execution | Interpreter (Interpreter.cs) | Diagnostics (ErrorReporter) | Interpreter owns statement dispatch; ErrorReporter accumulates errors (soft-failure model). Body executes under the pushed frame regardless of validation verdict. |
| `if(Bool, T, T)` overload resolution | Standard Library (StdLib.cs) + BuiltInFunctions (registration) | TypeSystem (OverloadResolver) | New signature registered via `InternalFunctionRegistry`; OverloadResolver selects it when args are concrete (non-Lazy). |
| `exportWav` parent-dir creation | Standard Library (Audio/FileIO.cs) | — | File I/O concern lives in FileIO.cs; `Directory.CreateDirectory` is a `System.IO` primitive. |
| Test harness execution | New `flow-lang.Tests` project | flow-lang (SUT) | xUnit drives FlowEngine in-process; `Console.SetOut/SetError` capture stdout/stderr; assert substring presence/absence. |
| `.flow` script discovery | New `flow-lang.Tests` project | `tests/` folder | MemberData method globs `tests/**/*.flow` at test discovery time; no build-time file enumeration. |

## Standard Stack

### Core (Existing — No Changes)
| Technology | Version | Purpose | Why Standard |
|------------|---------|---------|--------------|
| .NET 10 | net10.0 | Runtime | Already targeted (confirmed via `flow-lang/flow-lang.csproj:4` and `flow-interpreter/flow-interpreter.csproj:9`). NOTE: CLAUDE.md says "net9.0" — this is a doc-lag. Plan 12-06 should NOT advertise net9.0 as the target. |
| C# 13 / 14 | SDK 10.0.106 | Language | Records, pattern matching, file-scoped namespaces already used. |
| `Lazy<T>` (System) | Built-in | Memoize-with-exception-caching for Thunk | Default `LazyThreadSafetyMode.ExecutionAndPublication` caches exceptions AND uses `ExceptionDispatchInfo` internally to preserve stack traces. No third-party equivalent needed. |
| `ExceptionDispatchInfo` (System.Runtime.ExceptionServices) | Built-in | Stack-preserving re-throw | Canonical .NET primitive. Already available. |
| `Directory.CreateDirectory` + `Path.GetDirectoryName` (System.IO) | Built-in | Auto-mkdir for exportWav | Both are idempotent — `CreateDirectory` returns existing DirectoryInfo if already present. |

### New Test Framework Dependencies (plan 12-01 only)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| xunit.v3 | 3.2.2 | Test framework | Latest stable as of 2026-01-14; supports .NET 8+. v2 (2.9.3) is in maintenance-only mode (security fixes only). For a NEW test project in 2026, v3 is the default choice. [VERIFIED: nuget.org] |
| xunit.v3.runner.visualstudio | 3.2.2 | VSTest adapter for `dotnet test` discovery | Pairs with xunit.v3. |
| Microsoft.NET.Test.Sdk | 17.13.0+ | VSTest host + `dotnet test` integration | Required for `dotnet test` to discover tests. |
| coverlet.collector | 6.0.0+ | Code coverage collector (optional) | Emits XPlat code coverage; safe to include by default. [CITED: standard xUnit template] |

**Note on xunit vs xunit.v3:** xunit v2.9.3 is the "classic" package that most existing guides still reference; xunit.v3 uses a new execution model (`Microsoft.Testing.Platform`) and different base type (`TestContext` replaces some uses of the old xunit `IXunitTestRunner`). If the team prefers the broadly-documented path of least resistance, **xunit 2.9.3** works equally well for this phase — its API surface (`[Fact]`, `[Theory]`, `[MemberData]`) is identical for the patterns plan 12-01 needs. Recommendation: use v3 for new project in 2026; fall back to v2 if any v3 tooling friction appears.

**Installation (plan 12-01):**
```bash
cd /home/noah/Desktop/projects/flow-sharp
dotnet new xunit -n flow-lang.Tests -o flow-lang.Tests --framework net10.0
cd flow-lang.Tests
dotnet add reference ../flow-lang/flow-lang.csproj
dotnet sln ../flow-sharp.sln add flow-lang.Tests/flow-lang.Tests.csproj
```

**Verification:** As of 2026-04-19, `dotnet new xunit` template still defaults to xunit v2 on the installed SDK (10.0.106). Plan 12-01 can either accept the v2 default or run `dotnet new xunit3` if that template is installed. [ASSUMED — unverified which template name current SDK ships.]

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| xunit.v3 | NUnit 4.x | NUnit is well-understood but is a second-class citizen in the `dotnet test` ecosystem; xUnit is canonical. No project benefit to NUnit here. |
| xunit.v3 | MSTest v3 | Official Microsoft framework but `[DataTestMethod]` + `[DynamicData]` ergonomics are strictly inferior to xUnit `[Theory]` + `[MemberData]` for this use case. |
| `Lazy<Value>` | Manual try/catch + `ExceptionDispatchInfo` field | Manual version is ~15 lines and easier to audit but duplicates BCL logic. `Lazy<T>` is 1 line. Pick `Lazy<T>`. |
| `[MemberData]` glob | `[ClassData]` with IEnumerable | Equivalent at runtime; `MemberData` with a static method is marginally less boilerplate. |
| `[MemberData]` glob | Source generator emitting `[InlineData]` | Source generator gives static test names at compile time (better IDE experience), but adds a generator project and triples the complexity. Not worth it for 51 files. |

## Architecture Patterns

### System Architecture (post-Phase 12)

```
Developer runs: dotnet test

         flow-sharp.sln
         ├── flow-lang/                           (class lib, net10.0)
         │   ├── Core/FlowEngine.cs               ← entry point for tests
         │   ├── Interpreter/Interpreter.cs       ← FIX-07a target
         │   ├── Runtime/Thunk.cs                 ← FIX-06 target
         │   ├── StandardLibrary/
         │   │   ├── Collections.cs               ← FIX-05 target
         │   │   ├── StdLib.cs                    ← if(Bool,T,T) registration
         │   │   ├── BuiltInFunctions.cs          ← registration site
         │   │   └── Audio/FileIO.cs              ← exportWav auto-mkdir
         │   └── ...
         ├── flow-interpreter/                    (console app, net10.0)
         └── flow-lang.Tests/                     (NEW, xunit, net10.0)
             ├── flow-lang.Tests.csproj           ← references flow-lang
             ├── FlowScriptTests.cs               ← [Theory] wrap-as-Theory
             ├── FlowScriptData.cs                ← MemberData glob
             ├── Fixtures/
             │   └── FlowEngineRunner.cs          ← in-process FlowEngine + stdout capture
             ├── Unit/
             │   ├── CollectionsTests.cs          ← FIX-05 native tests
             │   ├── ThunkTests.cs                ← FIX-06 native tests
             │   └── InterpreterTests.cs          ← FIX-07a native tests
             └── Integration/
                 └── MusicalContextTests.cs       ← optional cross-cutting

Test run path:
  xunit discovery
       ↓
  FlowScriptTests.ExecutesWithExpectedOutput(scriptPath)
       ↓
  FlowScriptData.GetFlowScripts() → glob tests/**/*.flow → yield object[] { path, expectedSentinels[] }
       ↓
  FlowEngineRunner.Run(scriptPath)
       ├─ Console.SetOut(stdoutCapture)
       ├─ Console.SetError(stderrCapture)
       ├─ new FlowEngine().Execute(File.ReadAllText(path), path)
       └─ return (success, stdout, stderr)
       ↓
  Assert.Contains("expected-sentinel", stdout)  OR
  Assert.DoesNotContain("forbidden-sentinel", stdout)
```

### Pattern 1: FlowEngine test fixture (in-process, stdout-captured)

**What:** A disposable test helper that runs a `.flow` source string through `FlowEngine` in the same process as xUnit, capturing stdout/stderr as strings for assertion.

**When to use:** Every Theory case (wrap-as-Theory) and every Integration test.

**Example:**
```csharp
// flow-lang.Tests/Fixtures/FlowEngineRunner.cs
// Source: based on FlowEngine.Execute signature at flow-lang/Core/FlowEngine.cs:59
using FlowLang.Core;

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
        _engine = new FlowEngine(verbose);
    }

    public (bool Success, string Stdout, string Stderr, int ErrorCount) RunFile(string path)
    {
        var source = File.ReadAllText(path);
        var success = _engine.Execute(source, path);
        return (success, _stdout.ToString(), _stderr.ToString(), _engine.ErrorReporter.ErrorCount);
    }

    public void Dispose()
    {
        _engine.Dispose();
        Console.SetOut(_origOut);
        Console.SetError(_origErr);
    }
}
```

**Concurrency note:** Global `Console.SetOut`/`SetError` is process-wide. xUnit runs tests in parallel within an assembly by default. Plan 12-01 MUST either disable parallelism on the wrap-as-Theory test class via `[CollectionDefinition("FlowScripts", DisableParallelization = true)]` or use `TextWriter.Synchronized` + a `[Collection]` attribute to serialize access. Recommendation: disable parallelism on the Theory class — simpler, no concurrency bugs, and the full suite should still run in <60s given each script takes <1s.

### Pattern 2: Wrap-as-Theory via MemberData glob

**What:** A single `[Theory]` that receives every `tests/**/*.flow` path as a data row.

**When to use:** D-09 — migrate 70+ existing test scripts without rewriting them.

**Example:**
```csharp
// flow-lang.Tests/FlowScriptTests.cs
using Xunit;

[Collection("FlowScripts")]  // serialize Console.SetOut across rows
public class FlowScriptTests
{
    public static IEnumerable<object[]> FlowScripts()
    {
        var testsRoot = FindTestsRoot();  // walks up from AppContext.BaseDirectory
        foreach (var path in Directory.EnumerateFiles(testsRoot, "*.flow", SearchOption.AllDirectories))
        {
            // Skip tests/std.flow — it's a stdlib module, not a test
            if (Path.GetFileName(path) == "std.flow") continue;
            yield return new object[] { Path.GetRelativePath(testsRoot, path) };
        }
    }

    [Theory]
    [MemberData(nameof(FlowScripts))]
    public void RunsToCompletion(string relativePath)
    {
        var absolute = Path.Combine(FindTestsRoot(), relativePath);
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errorCount) = runner.RunFile(absolute);

        // Default assertion: any errors reported = test failure.
        // Per-script opt-outs via SkipScripts / ExpectedErrorScripts dictionaries.
        if (ExpectedErrorScripts.TryGetValue(relativePath, out var expected))
        {
            Assert.Contains(expected, stderr);
        }
        else
        {
            Assert.False(errorCount > 0, $"Script reported {errorCount} error(s):\n{stderr}");
        }
    }

    // Scripts that intentionally emit errors (e.g., test_error_masking.flow, test_musical_context_errors.flow).
    private static readonly Dictionary<string, string> ExpectedErrorScripts = new()
    {
        ["test_error_masking.flow"] = "Function 'nonExistentFunction' not found",
        ["test_musical_context_errors.flow"] = "Tempo must be positive",
        ["spike/c1-musical-context-body.flow"] = "Tempo must be positive",
        // ... etc
    };
}
```

**Alternative: per-script sentinel assertions.** For richer coverage, each script can have a row in a dictionary mapping path → `(required_sentinels[], forbidden_sentinels[])`. The spike c1 test uses this: probe 4 sentinel `c1-probe4-body-ran` is required; probes 1-3 body sentinels flip from forbidden (pre-FIX) to required (post-FIX). Planner should use this structure since the spike c1 flip depends on it.

### Pattern 3: Lazy<Value> Thunk refactor (FIX-06)

**What:** Replace Thunk's manual `_isEvaluated` / `_cachedValue` / `lock` triad with a `Lazy<Value>` that natively caches both successful values and exceptions.

**When to use:** FIX-06 implementation.

**Why `Lazy<T>` alone is sufficient:** [VERIFIED: learn.microsoft.com System.Lazy<T>]

> If an exception occurs and is unhandled in the initialization function, that exception is cached and rethrown on subsequent accesses of the Lazy<T>.Value property.

> With `LazyThreadSafetyMode.ExecutionAndPublication` [the default for `Lazy<T>(Func<T>)`], that same exception is thrown on every subsequent attempt to access the Value property.

Internally, `Lazy<T>` uses `ExceptionDispatchInfo.Capture` + `.Throw()` to preserve the original stack trace. The programmer-visible result is identical to D-05's manual requirement.

**Example:**
```csharp
// flow-lang/Runtime/Thunk.cs (post-FIX-06)
// Source: Microsoft Lazy<T> reference implementation pattern
using FlowLang.Ast;
using FlowLang.Interpreter;
using System.Threading;

namespace FlowLang.Runtime;

public class Thunk
{
    private readonly Lazy<Value> _lazy;

    public Thunk(Expression expression, ExpressionEvaluator evaluator)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));
        if (evaluator == null) throw new ArgumentNullException(nameof(evaluator));

        // ExecutionAndPublication is the default for Lazy<T>(Func<T>), but
        // specifying it explicitly documents the intent and guards against a
        // future .NET runtime changing the default.
        _lazy = new Lazy<Value>(
            () => evaluator.Evaluate(expression),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Forces evaluation. Returns the cached value if already evaluated.
    /// If the evaluator threw on first access, re-throws the same exception
    /// with the original stack trace preserved (ExceptionDispatchInfo semantics).
    /// </summary>
    public Value Force() => _lazy.Value;

    public bool IsEvaluated => _lazy.IsValueCreated;
}
```

**If the team prefers explicit `ExceptionDispatchInfo` per D-05 CONTEXT wording:**

```csharp
public class Thunk
{
    private Expression? _expression;
    private ExpressionEvaluator? _evaluator;
    private Value? _cachedValue;
    private ExceptionDispatchInfo? _cachedFailure;
    private bool _isEvaluated;
    private readonly object _lock = new();

    public Value Force()
    {
        if (_isEvaluated)
        {
            _cachedFailure?.Throw();       // re-throw cached failure (unreachable if null)
            return _cachedValue!;
        }

        lock (_lock)
        {
            if (_isEvaluated)
            {
                _cachedFailure?.Throw();
                return _cachedValue!;
            }

            try
            {
                _cachedValue = _evaluator!.Evaluate(_expression!);
            }
            catch (Exception ex)
            {
                _cachedFailure = ExceptionDispatchInfo.Capture(ex);
                _isEvaluated = true;
                _expression = null;
                _evaluator = null;
                _cachedFailure.Throw();  // re-throw on this first call too
                return default!;         // unreachable
            }

            _isEvaluated = true;
            _expression = null;
            _evaluator = null;
            return _cachedValue!;
        }
    }

    public bool IsEvaluated => _isEvaluated;
}
```

**Recommendation:** `Lazy<Value>` version. It's canonical, 15 lines instead of 45, and the failure-cache semantics are already audited by Microsoft.

### Pattern 4: Replace `return;` with `break;` in switch (FIX-07a)

**What:** Change seven `return;` inside `ExecuteMusicalContext`'s `switch` to `break;`. The `break` exits the `switch` statement but NOT the enclosing method, so execution falls through to `_context.CurrentFrame.MusicalContext = musicalCtx;` at line 269 and the body loop at 271-285.

**When to use:** FIX-07a implementation.

**Exact edits:**

| Line (pre-fix) | Current | Post-fix |
|---|---|---|
| 152 | `return;` | `break;` |
| 165 | `return;` | `break;` |
| 179 | `return;` | `break;` |
| 225 | `return;` | `break;` |
| 241 | `return;` | `break;` |
| 256 | `return;` | `break;` |
| 264 | `return;` | `break;` |

**Verification against CONTEXT's line numbers:** CONTEXT.md says lines 151,164,178,224,240,255,263 — but I re-read Interpreter.cs and found the actual `return;` lines are 152,165,179,225,241,256,264 (one line below each ReportError call). The discrepancy is probably because CONTEXT counted the ReportError line or was authored against a slightly-different file state. **Planner MUST re-grep `return;` at implementation time, not rely on CONTEXT line numbers.** The seven locations are still correct; only the precise line numbers drift. Here is a clean grep pattern the planner can use:

```bash
grep -n "return;" flow-lang/Interpreter/Interpreter.cs | sed -n '/^15[0-9]:\|^16[0-9]:\|^17[0-9]:\|^22[0-5]:\|^24[0-2]:\|^25[5-7]:\|^26[3-5]:/p'
```

**Frame balance check:** The `finally { _context.PopFrame(); }` at lines 287-290 runs on normal completion AND on `break`. Both pre-fix and post-fix code balance PushFrame at line 133 against PopFrame in finally. No change to the balance logic — the fix only changes what runs between them.

**Other exits in ExecuteMusicalContext:** Grep for `return|throw|goto` inside lines 131-291. I already checked — there are exactly seven `return;` in the switch (one per invalid-path case), plus an implicit return after the method body. No `goto`, no other `throw`. Converting only the seven explicit `return;` to `break;` is complete.

**Also update the AUDIT-VERIFIED marker at line 292.** Phase 11 left a "Confirmed" marker; after FIX-07a, change to:
```csharp
// AUDIT-VERIFIED 2026-04-19: C1 — Fixed (returns→breaks); body now runs under partial/default context (tests/spike/c1-musical-context-body.flow GREEN)
```
This follows the Phase 11 D-02 pattern: one marker line per claim, format `// AUDIT-VERIFIED YYYY-MM-DD: C[N] — <verdict> (<evidence path>)`.

### Anti-Patterns to Avoid

- **Don't convert `return;` to `throw;`** — violates soft-failure model (CONTEXT D-03 prerequisite, 11-01-SUMMARY.md "Next action" #3).
- **Don't set `_returnValue` on error** — pairs incorrectly with C2 dismissal (11-01-SUMMARY.md "Next action" #4).
- **Don't rewrite `.flow` tests into C# Asserts** — D-09 explicitly forbids. The scripts ARE the language spec; xUnit is the harness.
- **Don't hand-roll WAV parsing, MIDI writing, or Lazy memoization** — BCL or DryWetMidi already covers these.
- **Don't use `Environment.CurrentDirectory` to locate `tests/`** — tests may run from `flow-lang.Tests/bin/Debug/net10.0/`. Walk up from `AppContext.BaseDirectory` until you find a sibling `tests/` folder, or use `[assembly: AssemblyMetadata("TestsRoot", ...)]` set by the csproj.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Memoize-with-exception-caching | Manual `_isEvaluated` + `_cachedValue` + `lock` + try/catch | `Lazy<Value>` with default `ExecutionAndPublication` | BCL primitive already does `ExceptionDispatchInfo.Capture` + `.Throw()` internally; audited by Microsoft. |
| Stack-preserving re-throw | `throw new Exception("wrapping: " + ex.Message)` | `ExceptionDispatchInfo.Capture(ex).Throw()` | Standard .NET pattern; preserves stack across thread boundaries. |
| Parent-dir creation for WAV export | Manual `File.Exists` + loop | `Directory.CreateDirectory(Path.GetDirectoryName(path))` | Idempotent; creates full nested path; returns existing dir if present. |
| xUnit test-data generation | Source generator emitting 51 `[InlineData]` | `[MemberData]` pointing at a static method that globs | Generator adds a project and compile-time complexity; glob is one method. |
| Stdout capture in-process | Fork/exec + pipe | `Console.SetOut(StringWriter)` | `FlowEngine` uses `Console.WriteLine` via `(print)` built-in; `SetOut` routes to `StringWriter`. |

**Key insight:** Every Phase 12 fix is a 1-line-to-15-line change leaning on existing .NET primitives. The heavy lifting is test harness scaffolding (plan 12-01), not the bugs themselves.

## Runtime State Inventory

> Phase 12 is a mix of code fixes + test-harness scaffolding. No rename / refactor / string replacement. This section is minimal by design; a full Runtime State Inventory applies when code references survive in runtime stores.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — no databases, no user_ids to rename. `tests/output/*.wav` are build artifacts, not state. | None |
| Live service config | None — no external services | None |
| OS-registered state | None — no scheduled tasks, systemd units, pm2 processes | None |
| Secrets/env vars | None — no new env vars, no secrets renamed | None |
| Build artifacts / installed packages | `flow-lang.Tests/bin/` and `obj/` will be created; `.gitignore` already covers `bin/` and `obj/`. No stale egg-info equivalents in C#. | None — gitignore already covers |

**Nothing found in any category.** This is a pure code-edit + new-project phase.

## Common Pitfalls

### Pitfall 1: `test_musical_context_errors.flow` sentinel inversion after FIX-07a

**What goes wrong:** The existing test `tests/test_musical_context_errors.flow` contains:
```flow
tempo -5 {
    (print "should not print - negative tempo")
}
(print "after invalid tempo block")
```
The comment-label `"should not print - negative tempo"` was written under the pre-FIX-07a behavior. Empirical run (2026-04-19):
```
after invalid tempo block
tests/test_musical_context_errors.flow:3:1: error: Tempo must be positive, got -5
```
Only "after invalid tempo block" prints; the body sentinel does NOT. **After FIX-07a lands, the body sentinel WILL print** — because that's the whole point of the fix. D-04's claim that this test "continues passing post-FIX-07a" is only true if the Theory-wrap assertion strategy uses `Assert.Contains("after invalid tempo block", stdout)` + `Assert.Contains("Tempo must be positive", stderr)` without also asserting that the body sentinel is absent.

**Why it happens:** The string constant in the `.flow` file encodes a pre-FIX expectation in its text. `grep "should not" tests/*.flow` surfaces only this file + `test_error_masking.flow` and `test_for_loop.flow` (the latter two are not affected by FIX-07a).

**How to avoid:**
1. Plan 12-04 (FIX-07a) MUST update the string literal in `test_musical_context_errors.flow` from `"should not print - negative tempo"` to `"body ran under partial tempo context"` (or similar post-fix-appropriate label) as part of the same commit as the interpreter change.
2. Alternatively, plan 12-01's Theory data for this script should assert only: `stderr contains "Tempo must be positive"` AND `stdout contains "after invalid tempo block"` — deliberately NOT asserting on the body sentinel's presence/absence.
3. Option (1) is cleaner because it keeps the `.flow` script self-documenting; option (2) keeps the `.flow` file frozen.

**Warning signs:** If plan 12-04 is authored as "just swap return→break," the test will pass in CI because xUnit's default Theory assertion (errorCount == 0) already fails on this test (it has a real tempo error). The xUnit layer wouldn't catch the mismatch. Only a careful reading of the script reveals the sentinel drift.

### Pitfall 2: `.gitignore` swallows new test files

**What goes wrong:** `.gitignore:7-8` has `tests/` and `*.flow`. Any new `.flow` test file, and the new `flow-lang.Tests/` directory (if it matches `tests/` — it doesn't, but be careful), will not be picked up by `git add .` or `git commit -a`.

**Why it happens:** Pre-existing convention from v1.0 when tests were considered scratch space; Phase 11 encountered this and used `git add -f` (11-01-SUMMARY.md "Decisions Made").

**How to avoid:**
1. Every plan that touches `tests/**/*.flow` MUST `git add -f` the file explicitly.
2. Plan 12-01 creates `flow-lang.Tests/` at repo root — this directory name does NOT match the `tests/` ignore pattern, so the C# files inside will be tracked normally. Verify with `git check-ignore flow-lang.Tests/FlowScriptTests.cs` — should return empty (not ignored).
3. CLAUDE.md's Build & Run documentation already uses `dotnet run`, which doesn't interact with git. No doc update required in plan 12-06 for `dotnet test` either, unless the team wants to advertise it.

**Warning signs:** Commit looks clean (no errors) but the file isn't in the tree. `git log --stat <commit>` will reveal only Markdown / C# files, no `.flow`.

### Pitfall 3: `net9.0` / `net10.0` / `flow-lang.sln` drift

**What goes wrong:** CLAUDE.md says "targets .NET 9" and "flow-lang.sln"; the actual state is `net10.0` and `flow-sharp.sln` at repo root. Plan 12-01's csproj for `flow-lang.Tests` that uses `<TargetFramework>net9.0</TargetFramework>` will fail to resolve the project reference to `flow-lang` (which is net10.0) or at minimum emit a downlevel warning.

**Why it happens:** Doc lag — CLAUDE.md was authored when .NET 9 was the target; the project upgraded to .NET 10 without updating the doc.

**How to avoid:**
1. Plan 12-01 MUST use `<TargetFramework>net10.0</TargetFramework>` in `flow-lang.Tests.csproj`.
2. The solution file to `dotnet sln add` is `flow-sharp.sln` (at repo root), NOT `flow-lang.sln` (which doesn't exist). CONTEXT.md `canonical_refs` mentions "flow-lang.sln" which is also wrong.
3. Plan 12-06 CAN include a CLAUDE.md "Build & Run Commands" correction from `net9.0` → `net10.0`, but this is optional polish.

**Warning signs:** `dotnet build` emits NETSDK1045 ("The current .NET SDK does not support targeting") or a warning about framework mismatch between test project and flow-lang.

### Pitfall 4: Console.SetOut collides with parallel xUnit execution

**What goes wrong:** xUnit v3 and v2 both run tests in parallel within an assembly by default. Two `FlowEngineRunner` instances calling `Console.SetOut(writer)` simultaneously will corrupt each other's captured output.

**Why it happens:** `Console.Out` is a single process-wide `TextWriter`. `SetOut` mutates global state.

**How to avoid:**
1. Mark the wrap-as-Theory test class with `[Collection("FlowScripts")]` AND add `[CollectionDefinition("FlowScripts", DisableParallelization = true)]`.
2. Native FIX-05/06/07a unit tests (plan 12-02/03/04) that don't rely on stdout capture CAN remain parallel — they exercise `Collections.Init`, `Thunk.Force`, etc. directly.
3. Expected suite runtime with serialized Theory: 51 scripts × <1s each = <60s. Acceptable.

**Warning signs:** Flaky test failures with stdout showing fragments from the "wrong" script; failures that disappear under `xunit.runner.visualstudio.dotnet.UseParallelExecution=false`.

### Pitfall 5: `if` overload auto-wrapping does not exist

**What goes wrong:** The test `(if (gt frames1 0) "PASS..." "FAIL: Empty buffer")` at `test_custom_oscillator.flow:42` calls `if` with `(Bool, String, String)` but the only registered overload is `(Bool, Lazy<Void>, Lazy<Void>)`. The OverloadResolver at `flow-lang/TypeSystem/OverloadResolver.cs` does NOT auto-wrap concrete values into lazy thunks — it reports `No matching overload for function 'if' with argument types (Bool, String, String)`.

**Why it happens:** The Lazy overload works when the call site uses `(if cond (lazy { ... }) (lazy { ... }))` or when the grammar implicitly marks positions. The grammar does not; all lazy-wrapping is explicit.

**How to avoid — plan 12-05 implementation:**

The cleanest approach is to register a wildcard strict-evaluation overload:

```csharp
// In BuiltInFunctions.cs RegisterStdLib, after the existing Lazy if registration:
var ifStrictSignature = new FunctionSignature(
    "if", [BoolType.Instance, VoidType.Instance, VoidType.Instance]);
registry.Register("if", ifStrictSignature, StdLib.IfStrict);
```

With the implementation:
```csharp
// In StdLib.cs, after existing If (Lazy):
public static Value IfStrict(IReadOnlyList<Value> args)
{
    var cond = args[0].As<bool>();
    return cond ? args[1] : args[2];
}
```

**Important overload-priority note:** `VoidType.Instance` is used as a wildcard (BuiltInFunctions.cs:232-234 comment confirms this is the established convention). The OverloadResolver at `OverloadResolver.cs:62-82` ranks by specificity — `LazyType<Void>` has higher specificity than `Void` alone when the arg IS a Lazy, so the existing overload wins when called with `lazy { ... }`. When called with a concrete String/Double/etc, the Lazy overload fails the type-match check (`Matches(argTypes)` at line 38) and the wildcard overload is the only candidate. Both call sites work.

**Test verification:** After registering `IfStrict`, `test_custom_oscillator.flow:42,57,75,98` all succeed — line 57 `(if (lt phase2 0.5) 1.0 -1.0)` also needed this (Double, Double).

**Warning signs:** Adding only `if(Bool, String, String)` per CONTEXT D-16 literal wording leaves line 57 still broken (`Double, Double`) and `test_custom_oscillator.flow` still fails Test 4. The planner should treat CONTEXT's "if(Bool, String, String)" as a specific-case hint and implement the wildcard overload that solves ALL Bool-T-T concrete shapes.

### Pitfall 6: writeWav does NOT currently auto-mkdir

**What goes wrong:** CONTEXT.md D-02 says `exportWav` auto-mkdir "matches writeWav". Verification: `writeWav` at `FileIO.cs:240-246` calls `ExportWavInternal(buffer, filepath, 16)` — same internal as exportWav — which opens a `FileStream` directly with no `Directory.CreateDirectory` call. **Neither function currently auto-creates parent dirs.**

**Why it happens:** The CONTEXT author reasoned from the intended end state, not the current state.

**How to avoid:** Plan 12-05 should add the auto-mkdir in `ExportWavInternal` (the shared helper at `FileIO.cs:41-63`), which immediately benefits BOTH exportWav AND writeWav. One edit, both paths fixed:

```csharp
// flow-lang/StandardLibrary/Audio/FileIO.cs, inside ExportWavInternal, before FileStream:
var dir = Path.GetDirectoryName(filepath);
if (!string.IsNullOrEmpty(dir))
    Directory.CreateDirectory(dir);
```

`Directory.CreateDirectory` is idempotent (no-op when dir exists), so this is safe for all existing call sites. `Path.GetDirectoryName("file.wav")` returns `""` — guard against that with the null/empty check.

**Warning signs:** If plan 12-05 edits `ExportWav` but not `ExportWavInternal`, writeWav still breaks on nested paths. Testing only `test_full_song.flow` won't catch this — there's no test that uses `writeWav` with a nested path.

### Pitfall 7: CLAUDE.md GSD enforcement gate

**What goes wrong:** CLAUDE.md §"GSD Workflow Enforcement" says: "Before using Edit, Write, or other file-changing tools, start work through a GSD command". This is process guidance for the human operator and doesn't affect plan content, but the plan-checker and executor may surface this as a requirement.

**How to avoid:** Already addressed — this entire phase is inside the GSD workflow (`/gsd-plan-phase` → phase 12). No action needed.

## Code Examples

Verified patterns from existing codebase sources:

### Existing Head/Last error format (FIX-05 template)
```csharp
// Source: flow-lang/StandardLibrary/Collections.cs:48-59 (unchanged)
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

### Target Init function (FIX-05 post-fix)
```csharp
// flow-lang/StandardLibrary/Collections.cs:84-92 post-FIX-05
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

### FlowEngine driver in xUnit fixture (plan 12-01 integration)
```csharp
// Source: mirrors flow-interpreter/Program.cs:67-92 RunFromString pattern
using var runner = new FlowEngineRunner(verbose: false);
var (success, stdout, stderr, errorCount) = runner.RunFile("tests/test_while_loop.flow");
Assert.Equal(0, errorCount);
Assert.Contains("5", stdout);  // expected count value
Assert.Contains("3", stdout);  // expected val2 value
```

### Native unit test for FIX-05 (plan 12-02)
```csharp
// flow-lang.Tests/Unit/CollectionsTests.cs
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

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

### Native unit test for FIX-06 (plan 12-03)
```csharp
// flow-lang.Tests/Unit/ThunkTests.cs
using FlowLang.Runtime;
using FlowLang.Ast;
using Xunit;

public class ThunkTests
{
    [Fact]
    public void Force_CachesSuccessValue()
    {
        int callCount = 0;
        var evaluator = new CountingEvaluator(() =>
        {
            callCount++;
            return Value.Int(42);
        });
        var thunk = new Thunk(FakeExpression.Instance, evaluator);
        Assert.Equal(42, thunk.Force().As<int>());
        Assert.Equal(42, thunk.Force().As<int>());
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Force_CachesExceptionAndRethrows()
    {
        int callCount = 0;
        var evaluator = new CountingEvaluator(() =>
        {
            callCount++;
            throw new InvalidOperationException("boom");
        });
        var thunk = new Thunk(FakeExpression.Instance, evaluator);

        var first = Assert.Throws<InvalidOperationException>(() => thunk.Force());
        var second = Assert.Throws<InvalidOperationException>(() => thunk.Force());
        Assert.Equal("boom", first.Message);
        Assert.Equal("boom", second.Message);
        Assert.Equal(1, callCount);  // evaluator called only once — failure cached
    }

    [Fact]
    public void Force_RethrowPreservesStackTrace()
    {
        var evaluator = new CountingEvaluator(() => throw new InvalidOperationException("boom"));
        var thunk = new Thunk(FakeExpression.Instance, evaluator);
        Exception? captured = null;
        try { thunk.Force(); } catch (Exception ex) { captured = ex; }
        Assert.NotNull(captured);
        Assert.Contains("CountingEvaluator", captured.StackTrace ?? "");  // original frame present
    }
}
```

Planner will need to write `CountingEvaluator` / `FakeExpression` test doubles or use a minimal real `ExpressionEvaluator` instance with a `LiteralExpression` seeded to throw.

### Native unit test for FIX-07a (plan 12-04)
```csharp
// flow-lang.Tests/Unit/InterpreterTests.cs
using FlowLang.Core;
using Xunit;

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
    [InlineData("key NotAKey", "Unrecognized key 'NotAKey'")]
    public void ValidationPath_BodyRunsUnderDefaultContext(string contextDecl, string expectedError)
    {
        using var runner = new FlowEngineRunner();
        var source = $"{contextDecl} {{ (print \"body-ran\") }}";
        var (_, stdout, _, _) = runner.RunSource(source);
        Assert.Contains("body-ran", stdout);
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| xunit 2.x classic host | xunit.v3 3.x on `Microsoft.Testing.Platform` | 2024-2026 | Plan 12-01 picks v3 for new project; v2 still viable. |
| Manual Lazy + lock + cache | `Lazy<T>` with `ExecutionAndPublication` | .NET 4.0 (2010) | FIX-06 uses built-in. |
| Manual throw ex (stack lost) | `ExceptionDispatchInfo.Capture(ex).Throw()` | .NET 4.5 (2012) | Used internally by `Lazy<T>`; surfaced in public API for explicit use. |

**Deprecated/outdated:**
- xunit 2.x — maintenance-only as of 2024. No breaking choice to avoid v3.
- Manual `ExceptionDispatchInfo` wiring for memoize-cache pattern — `Lazy<T>` supersedes.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `dotnet new xunit` on SDK 10.0.106 defaults to xunit v2 template (not v3) | Standard Stack | LOW — if v3 is the default, plan 12-01 gets v3 "for free"; if v2, plan uses `dotnet new xunit3` explicitly. No blocking impact. |
| A2 | xUnit v3 test parallelism default requires explicit `[Collection]` + `DisableParallelization` to serialize Console capture | Pitfall 4 | LOW — if v3 changed the default to serial, the pitfall is over-cautious but not harmful. |
| A3 | The `if` overload error at line 42 is the root failure; line 57 `(if Bool Double Double)` also needs the wildcard overload | Pitfall 5 | MEDIUM — if test_custom_oscillator.flow shortcircuits on line 42 error and line 57 is never reached, the wildcard overload is still correct but the "both fixes in one overload" claim is weaker. Empirical run shows cascade — line 42 error halts execution before line 57 runs. |
| A4 | `Lazy<T>.Value` re-throws the original exception reference on subsequent accesses (verified by Microsoft docs) | FIX-06 pattern | LOW — behavior is documented; if the caller relies on throwing a NEW exception each time, `Lazy<T>` is wrong choice. The test `Force_CachesExceptionAndRethrows` in Code Examples confirms the cached semantics. |
| A5 | `dotnet test` on the new `flow-lang.Tests` project will discover and run all xUnit tests without additional config | Plan 12-01 | LOW — standard dotnet-test flow; if discovery fails, `Microsoft.NET.Test.Sdk` needs an explicit `<IsPackable>false</IsPackable>` in the csproj. |

## Open Questions

1. **Which xUnit major version (v2 vs v3) does the team prefer?**
   - What we know: v3 is current (released 2026-01-14); v2 is maintenance-only.
   - What's unclear: Team familiarity / tooling preference not captured in CONTEXT.md (marked as Claude's Discretion).
   - Recommendation: Use v3 (xunit.v3 3.2.2). If plan author encounters v3-specific friction (e.g., `ITestOutputHelper` is now `TestContext.Current.TestOutputHelper`), fall back to v2 2.9.3 — both work for this phase.

2. **Should test scripts that print errors (test_error_masking, test_musical_context_errors, spike/c1) be tagged as `ExpectedErrorScripts` with sentinel dictionaries, or asserted loosely via `errorCount > 0`?**
   - What we know: 4 scripts intentionally emit errors. CONTEXT doesn't specify assertion strategy.
   - What's unclear: Granularity of assertion.
   - Recommendation: Tag each via a dictionary `ExpectedErrorScripts` with specific stderr substring. This preserves the RED→GREEN bisect for spike c1 (pre-fix: body sentinels absent; post-fix: body sentinels present). Without per-row sentinels, the Theory can't flip on the FIX-07a commit.

3. **What's the naming convention for the Theory method and the Data method?**
   - Recommendation (Claude's Discretion): `FlowScriptTests.RunsToCompletion(string relativePath)` + `FlowScriptTests.FlowScripts()` data method. Keep method names boring and discoverable in test output.

4. **Does the `.gitignore` need a carve-out for `flow-lang.Tests/`?**
   - What we know: `.gitignore:7` is `tests/` (relative glob matching any `tests/` directory in the tree). `flow-lang.Tests/` does NOT match `tests/` — different name.
   - Verified: `flow-lang.Tests/FlowScriptTests.cs` would not be ignored.
   - Recommendation: No carve-out needed; planner can verify with `git check-ignore flow-lang.Tests/dummy.cs` during plan 12-01.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|-------------|-----------|---------|----------|
| .NET SDK 10.x | All plans | ✓ | 10.0.106 | — |
| `dotnet` CLI | Build + test | ✓ | 10.0.106 | — |
| xunit.v3 NuGet | plan 12-01 | ✓ (on NuGet) | 3.2.2 (2026-01-14) | xunit 2.9.3 as backup |
| xunit.v3.runner.visualstudio | plan 12-01 | ✓ (on NuGet) | 3.2.2 | xunit.runner.visualstudio 3.x |
| Microsoft.NET.Test.Sdk | plan 12-01 | ✓ (on NuGet) | 17.13.0+ | — |
| coverlet.collector | plan 12-01 (optional) | ✓ (on NuGet) | 6.0.2 | Skip coverage |
| PulseAudio runtime | — | Not needed by tests | — | No test script uses `play`/`preview`/`loop` (verified via grep) |
| DryWetMidi NuGet | flow-lang production | ✓ (already in csproj) | 8.0.3 | — |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** None — all required tooling is present.

**Audio / MIDI isolation confirmation:** Grep of `tests/*.flow` for `(play `, `(preview `, `(loop `, `writeMidi`, `exportMidi` returns ZERO matches. The existing test suite is entirely audio-playback-free and MIDI-export-free. CONTEXT D-29 (defer PulseAudio/DryWetMidi isolation) is correct — no wrap-as-Theory row hits those subsystems.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 (new — plan 12-01 scaffolds) |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` (new) + optional `xunit.runner.json` for Collection config |
| Quick run command | `dotnet test flow-lang.Tests/ --filter "FullyQualifiedName!~FlowScripts"` (fast: native unit tests only) |
| Full suite command | `dotnet test flow-lang.Tests/` (includes wrap-as-Theory rows — ~60s) |
| Phase gate | Full suite green before `/gsd-verify-work` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| FIX-05 | `init([])` throws `InvalidOperationException` with exact message | unit | `dotnet test flow-lang.Tests/ --filter "Init_EmptyArray"` | ❌ Wave 0 — plan 12-02 creates `CollectionsTests.cs` |
| FIX-05 | `init([1])` returns `[]`; `init([1,2,3])` returns `[1,2]` | unit | `dotnet test flow-lang.Tests/ --filter "CollectionsTests"` | ❌ Wave 0 |
| FIX-06 | `Thunk.Force()` caches success value; evaluator called once | unit | `dotnet test flow-lang.Tests/ --filter "Force_CachesSuccessValue"` | ❌ Wave 0 — plan 12-03 creates `ThunkTests.cs` |
| FIX-06 | `Thunk.Force()` caches exception; re-throws on second call; evaluator called once | unit | `dotnet test flow-lang.Tests/ --filter "Force_CachesExceptionAndRethrows"` | ❌ Wave 0 |
| FIX-06 | Re-thrown exception preserves original stack trace | unit | `dotnet test flow-lang.Tests/ --filter "Force_RethrowPreservesStackTrace"` | ❌ Wave 0 |
| FIX-07a | Bad tempo context: body runs, error reported, frame balanced | unit | `dotnet test flow-lang.Tests/ --filter "BadTempo_BodyStillRuns"` | ❌ Wave 0 — plan 12-04 creates `InterpreterTests.cs` |
| FIX-07a | Same for bad swing / gain / pan / key | unit (Theory) | `dotnet test flow-lang.Tests/ --filter "ValidationPath_BodyRunsUnderDefaultContext"` | ❌ Wave 0 |
| FIX-07a | spike c1 RED→GREEN flip: `c1-probe1-body-ran`, `c1-probe2-stmt1`, `c1-probe2-stmt2`, `c1-probe3-body-ran` ALL present on stdout | integration (wrap-as-Theory) | `dotnet test flow-lang.Tests/ --filter "spike/c1"` | ❌ Wave 0 — plan 12-01 registers Theory case |
| TEST-01 | `test_custom_oscillator.flow` runs to completion, no error count | integration | `dotnet test flow-lang.Tests/ --filter "test_custom_oscillator"` | ❌ Wave 0 |
| TEST-02 | `test_while_loop.flow` runs to completion, stdout contains "5", "3", "1", "3" sentinel values | integration | `dotnet test flow-lang.Tests/ --filter "test_while_loop"` | ❌ Wave 0 — already passes; wrap locks behavior |
| TEST-03 | `test_full_song.flow` runs to completion, stdout contains "=== All integration tests passed! ===" | integration | `dotnet test flow-lang.Tests/ --filter "test_full_song"` | ❌ Wave 0 |
| TEST-03 | After `exportWav` auto-mkdir, `tests/output/test_full_song.wav` file exists post-run | integration | `dotnet test flow-lang.Tests/ --filter "test_full_song"` (assertion via File.Exists) | ❌ Wave 0 |
| REQUIREMENTS edits | FIX-05/06/07a status updated to "Shipped <commit-hash>" in REQUIREMENTS.md; TEST-01/02 marked Closed; TEST-03 reframed | manual-only | Review-time check — no automated test | ❌ Plan 12-06 output |

### Observable Invariants (Nyquist-style; fail if feature removed)

1. **FIX-05 invariant:** Calling `Collections.Init` on an empty array must throw an exception whose message contains "Cannot get init of empty array". If the fix is reverted (the LINQ `Take(-1)` restored), the unit test fails because no exception is thrown.

2. **FIX-06 invariant — success caching:** An evaluator whose side-effect is counting its invocations must be called EXACTLY ONCE across multiple `Force()` calls on the same Thunk. If caching regresses, call count exceeds 1 and the test fails.

3. **FIX-06 invariant — failure caching:** An evaluator that throws must have its exception cached; call count must be 1 across multiple `Force()` calls. If the pre-fix bug (null return on second call) returns, either call count exceeds 1 OR the second `Force` returns null instead of throwing.

4. **FIX-06 invariant — stack preservation:** The StackTrace of a Force-thrown exception must contain the frame of the original throwing callback. If a naïve `throw ex` is used instead of `ExceptionDispatchInfo.Throw()`, the stack gets truncated at `Thunk.Force` and the frame check fails.

5. **FIX-07a invariant — body execution:** `tempo -5 { (print "X") }` must emit "X" on stdout. If the 7 `return;` are restored, the body skip returns and "X" is absent.

6. **FIX-07a invariant — error reporting:** `tempo -5 { ... }` must ALSO emit "Tempo must be positive, got -5" on stderr. Without the ErrorReporter call (which IS preserved — only the `return;` changes), the error isn't reported.

7. **FIX-07a invariant — frame balance:** After `tempo -5 { (print "body") } (print "after")`, both "body" AND "after" emit. If frame balance broke (PopFrame missing), "after" would execute under a corrupted stack. Assertion: stdout contains both sentinels in order.

8. **FIX-07a invariant — spike c1 GREEN:** `tests/spike/c1-musical-context-body.flow` produces all 9 sentinels (`c1-probe1-body-ran`, `c1-probe1-after-block`, `c1-probe2-stmt1`, `c1-probe2-stmt2`, `c1-probe2-after-block`, `c1-probe3-body-ran`, `c1-probe3-after-block`, `c1-probe4-body-ran`, `c1-probe4-after-block`). Pre-fix: only 5 (the `after-block` ones + probe 4). Post-fix: all 9.

9. **`if` wildcard overload invariant:** `(if true "A" "B")` evaluates to `"A"`. `(if false 1.0 2.0)` evaluates to `2.0`. If the wildcard overload is removed, both calls fail with `No matching overload` error.

10. **`exportWav` auto-mkdir invariant:** `(exportWav buf "nested/dir/file.wav")` succeeds even when `nested/dir/` does not pre-exist. Post-run, `File.Exists("nested/dir/file.wav")` is true.

11. **Soft-failure contract preservation:** `test_error_masking.flow` and `test_musical_context_errors.flow` continue to produce non-empty stderr AND non-empty stdout for the statements after the error. ROADMAP success criterion 5 — errors accumulate, execution continues.

### Sampling Rate
- **Per task commit:** `dotnet test flow-lang.Tests/ --filter "FullyQualifiedName!~FlowScripts"` (unit tests, <5s)
- **Per wave merge:** `dotnet test flow-lang.Tests/` (full suite including wrap-as-Theory, ~60s)
- **Phase gate:** Full suite green before `/gsd-verify-work` in plan 12-06

### Wave 0 Gaps

Every test file listed below is new as of Phase 12:

- [ ] `flow-lang.Tests/flow-lang.Tests.csproj` — xUnit project (plan 12-01)
- [ ] `flow-lang.Tests/Fixtures/FlowEngineRunner.cs` — in-process test driver (plan 12-01)
- [ ] `flow-lang.Tests/FlowScriptTests.cs` + `FlowScriptData.cs` — wrap-as-Theory (plan 12-01)
- [ ] `flow-lang.Tests/Unit/CollectionsTests.cs` — FIX-05 coverage (plan 12-02)
- [ ] `flow-lang.Tests/Unit/ThunkTests.cs` — FIX-06 coverage (plan 12-03)
- [ ] `flow-lang.Tests/Unit/InterpreterTests.cs` — FIX-07a coverage (plan 12-04)
- [ ] Update `tests/test_musical_context_errors.flow` sentinel string (plan 12-04 — see Pitfall 1)
- [ ] Framework install: `dotnet add package xunit.v3 xunit.v3.runner.visualstudio Microsoft.NET.Test.Sdk coverlet.collector`
- [ ] Solution wiring: `dotnet sln flow-sharp.sln add flow-lang.Tests/flow-lang.Tests.csproj`

## Security Domain

> `security_enforcement` is not present in `.planning/config.json`; per the default-enabled policy this section is included.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V1 Architecture | yes (minor) | Interpreter runs locally; no network boundary. Threat model = malicious `.flow` script authored by user. |
| V2 Authentication | no | Local CLI tool — no auth layer |
| V3 Session Management | no | Single-shot interpreter |
| V4 Access Control | no | Local FS access only |
| V5 Input Validation | yes | FIX-05 is a V5 control — reject invalid input (empty array) with clear error. FIX-07a preserves error accumulation (multiple errors reported, not suppressed). |
| V6 Cryptography | no | No crypto |
| V7 Error Handling | yes | FIX-06 is a V7 control — preserve exception detail (stack trace) across memoization boundaries. Do not silently swallow failures. |
| V8 Data Protection | no | No sensitive data flowing |
| V9 Communication | no | No network |
| V10 Malicious Code | partial | `(exportWav ... path)` writes arbitrary paths; `Directory.CreateDirectory` creates arbitrary directories. NOT new risk in Phase 12 — existing ExportWavInternal already accepts arbitrary `filepath`. Auto-mkdir extends the file-write surface to include parent dir creation. Accept: same trust boundary as existing writeWav. Users already write `.wav` to arbitrary paths via `writeWav`. |
| V11 Business Logic | no | Dev tool |
| V12 File Upload | no | No upload layer |
| V13 API | no | No API layer |
| V14 Configuration | yes (minor) | `flow-lang.Tests.csproj` should not include test secrets; no concern for this phase |

### Known Threat Patterns for the phase stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal via exportWav filepath | Tampering | Trust boundary: local user running `dotnet run` has full FS access already. Not a mitigation target in Phase 12. Deferred to a future hardening phase if the interpreter ever runs untrusted scripts. |
| Silent data loss via Thunk exception swallow | Information Disclosure / Denial of Service | FIX-06 IS the mitigation — cache and re-throw rather than return null. |
| Error masking after single failure | Information Disclosure | Pre-existing soft-failure model (Interpreter.cs ErrorReporter) handles. FIX-07a preserves this invariant by keeping `ReportError` calls and only changing `return;` → `break;`. |
| Test harness Console.SetOut race (parallel test execution) | Tampering | Pattern 4 Pitfall — serialize wrap-as-Theory collection. |

No new security review items specific to Phase 12 beyond what the fixes themselves address.

## Sources

### Primary (HIGH confidence)
- `.planning/phases/12-stability/12-CONTEXT.md` (user-authored decisions)
- `.planning/phases/11-audit-spike/11-VERIFICATION.md`, `11-01-SUMMARY.md` (Phase 11 handoff)
- `.planning/REQUIREMENTS.md` (phase requirement IDs)
- `.planning/ROADMAP.md` §"Phase 12: Stability" (success criteria)
- `.planning/CODEBASE-AUDIT-2026-04-18.md` §1 rows C1/C6/C7 (bug definitions)
- `flow-lang/Interpreter/Interpreter.cs:131-292` (FIX-07a target, verified line-by-line 2026-04-19)
- `flow-lang/Runtime/Thunk.cs:1-49` (FIX-06 target, read verbatim)
- `flow-lang/StandardLibrary/Collections.cs:48-92` (FIX-05 target + Head/Last template)
- `flow-lang/StandardLibrary/Audio/FileIO.cs:16-258` (exportWav + writeWav — verified neither currently auto-mkdirs)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:211-213` (if overload registration site)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:425-447` (exportWav / writeWav registration)
- `flow-lang/StandardLibrary/StdLib.cs:331-345` (If implementation)
- `flow-lang/Core/FlowEngine.cs:16-129` (FlowEngine API surface for test fixture)
- `flow-interpreter/Program.cs:67-92` (RunFromString reference pattern for test fixture)
- `flow-lang/flow-lang.csproj:4`, `flow-interpreter/flow-interpreter.csproj:9` (net10.0 target — verified)
- `flow-sharp.sln:1-35` (solution file location + project GUIDs)
- `.gitignore:7-8` (tests/ and *.flow ignore patterns)
- `tests/test_custom_oscillator.flow` (empirical run 2026-04-19 — "if overload with (Bool, String, String)" error confirmed)
- `tests/test_musical_context_errors.flow` (pitfall 1 sentinel inversion)
- `tests/test_while_loop.flow`, `tests/test_for_loop.flow`, `tests/test_error_masking.flow` (behavior verification)
- `tests/spike/c1-musical-context-body.flow` (RED→GREEN flip reference)
- [Microsoft Learn: System.Lazy<T>](https://learn.microsoft.com/en-us/dotnet/api/system.lazy-1?view=net-10.0) — exception caching semantics
- [Microsoft Learn: Lazy Initialization](https://learn.microsoft.com/en-us/dotnet/framework/performance/lazy-initialization) — ExecutionAndPublication mode details
- [Microsoft Learn: LazyThreadSafetyMode](https://learn.microsoft.com/en-us/dotnet/api/system.threading.lazythreadsafetymode?view=net-8.0) — mode comparison
- [NuGet: xunit 2.9.3](https://www.nuget.org/packages/xunit) — v2 maintenance-only status
- [NuGet: xunit.v3 3.2.2](https://www.nuget.org/packages/xunit.v3) — v3 latest stable (2026-01-14)
- [xUnit.net: Release Notes](https://xunit.net/releases/) — v3 as active development line

### Secondary (MEDIUM confidence)
- [Microsoft: Unit testing C# with dotnet test and xUnit](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-xunit) — canonical setup reference
- [Andrew Lock: Creating parameterised tests in xUnit](https://andrewlock.net/creating-parameterised-tests-in-xunit-with-inlinedata-classdata-and-memberdata/) — MemberData pattern
- [Morteza Sahragard: Rethrow Exceptions with ExceptionDispatchInfo](https://weblogs.asp.net/morteza/rethrow-exceptions-with-exceptiondispatchinfo/) — re-throw semantics
- [The Burning Monk: Be Lazy, but beware of initialization exception](https://theburningmonk.com/2013/04/be-lazy-but-be-ware-of-initialization-exception/) — Lazy exception caching explanation

### Tertiary (LOW confidence — noted for validation)
- Exact `dotnet new xunit` default template version on SDK 10.0.106 (A1 in Assumptions Log)
- xUnit v3 parallelism default (A2 in Assumptions Log) — well-documented in guides but project-specific behavior unverified

## Project Constraints (from CLAUDE.md)

| Directive | Source line | How Plan Must Comply |
|-----------|-------------|----------------------|
| File-scoped namespaces throughout | CLAUDE.md §"C# Conventions" | All new .cs files in `flow-lang.Tests/` use file-scoped namespace `FlowLang.Tests` or sub-namespaces. |
| `FlowLang.*` namespace root (library) | CLAUDE.md §"C# Conventions" | Test project uses `FlowLang.Tests` to match. |
| Nullable reference types enabled | CLAUDE.md §"C# Conventions" | Test csproj includes `<Nullable>enable</Nullable>` (also matches existing flow-lang.csproj). |
| Implicit usings enabled | CLAUDE.md §"C# Conventions" | Test csproj includes `<ImplicitUsings>enable</ImplicitUsings>`. |
| AST nodes are `record` types | CLAUDE.md §"C# Conventions" | Not applicable — Phase 12 doesn't add AST nodes. |
| Pattern matching over visitor pattern | CLAUDE.md §"C# Conventions" | Not applicable — Phase 12 doesn't add dispatch logic (FIX-07a only replaces `return;` with `break;`). |
| .NET 9 target | CLAUDE.md §"Constraints" — INCONSISTENT | **Actual target is net10.0.** Plan 12-01's test csproj uses `net10.0`. See Pitfall 3. |
| No new NuGet packages | CLAUDE.md §"Constraints" / STACK.md | **Exception justified:** xUnit is a test framework, not a runtime dependency. CONTEXT D-08 authorizes the new `flow-lang.Tests` project. Plan 12-01 adds xunit.v3 + runner + SDK + coverlet — all test-time only. Runtime production code (flow-lang.csproj, flow-interpreter.csproj) gets NO new packages. |
| Existing .flow test suite must continue to work | CLAUDE.md §"Constraints — Compatibility" | Wrap-as-Theory (D-09) achieves this — scripts unchanged, runner added. Exception: `tests/test_musical_context_errors.flow` sentinel string update per Pitfall 1. |
| No GC pressure in hot paths | CLAUDE.md §"Constraints — Performance" | `Lazy<T>` allocates once per Thunk; no change from existing Thunk allocation behavior. `break;` has zero allocation. Not a concern for Phase 12. |
| GSD Workflow Enforcement | CLAUDE.md §"GSD Workflow Enforcement" | Already in GSD phase 12 — satisfied. |

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all versions verified against nuget.org / learn.microsoft.com
- Architecture: HIGH — read Interpreter.cs, Thunk.cs, Collections.cs, FileIO.cs, BuiltInFunctions.cs, StdLib.cs, FlowEngine.cs verbatim
- Pitfalls: HIGH — empirical runs of test_custom_oscillator.flow and test_musical_context_errors.flow performed 2026-04-19
- FIX-06 Lazy<T> pattern: HIGH — Microsoft docs explicit on ExecutionAndPublication exception caching
- xUnit v3 parallelism default: MEDIUM — documented broadly but project-specific friction possible

**Research date:** 2026-04-19
**Valid until:** 2026-05-19 (30 days — stable frameworks and BCL primitives)

---

## RESEARCH COMPLETE

**Phase:** 12 — Stability
**Confidence:** HIGH

### Key Findings

1. **FIX-06 is simpler than CONTEXT suggests.** `Lazy<Value>` with default `ExecutionAndPublication` mode natively caches exceptions via internal `ExceptionDispatchInfo` and re-throws the same exception on every `.Value` access. D-05's explicit `ExceptionDispatchInfo.Capture/.Throw()` pattern is valid but redundant — the 15-line `Lazy<Value>` refactor subsumes it.

2. **Three CONTEXT/CLAUDE.md facts need correction in plan text:** project targets `net10.0` (not `net9.0`); solution file is `flow-sharp.sln` at repo root (not `flow-lang.sln`); neither `writeWav` nor `exportWav` currently auto-mkdir (CONTEXT's "matches writeWav" phrasing reflects the intended post-fix state, not the current state — fix both by editing `ExportWavInternal`).

3. **`tests/test_musical_context_errors.flow` contains a sentinel string (`"should not print - negative tempo"`) that becomes incorrect after FIX-07a lands.** D-04 claims this test "continues passing" post-fix — technically true if the xUnit Theory asserts only on error presence, but the string label in the `.flow` file will be misleading. Plan 12-04 should update the string as part of the fix commit, OR plan 12-01's Theory assertion must be written to be silent on the body-sentinel's presence.

4. **The `if` overload fix per D-16 needs to be wildcard `(Bool, Void, Void)` (strict), not only `(Bool, String, String)`.** test_custom_oscillator.flow line 57 also calls `if` with `(Bool, Double, Double)`. One wildcard overload solves both; the String-specific overload per CONTEXT's literal wording leaves line 57 broken.

5. **FIX-07a return-line numbers drift.** CONTEXT says lines 151,164,178,224,240,255,263 — actual `return;` lines are 152,165,179,225,241,256,264 (one line below each `ReportError`). Planner MUST re-grep at implementation time; don't hard-code CONTEXT's numbers.

6. **Test suite is audio/MIDI-playback-free.** Zero `.flow` scripts call `(play ...)`, `(preview ...)`, `(loop ...)`, or `(writeMidi ...)`. CONTEXT D-29's deferral of PulseAudio/DryWetMidi isolation is correct — no wrap-as-Theory row exercises those subsystems. Safe to ship xUnit harness without platform-abstraction work.

### File Created
`.planning/phases/12-stability/12-RESEARCH.md`

### Confidence Assessment
| Area | Level | Reason |
|------|-------|--------|
| Standard Stack | HIGH | xUnit v2/v3 versions verified against nuget.org; Lazy<T> semantics verified against learn.microsoft.com |
| Architecture | HIGH | All target files read verbatim; empirical test runs performed |
| Pitfalls | HIGH | Each pitfall has an empirical or doc-verified origin |
| Code Examples | HIGH | Based on actual method signatures from the codebase |
| Environment Availability | HIGH | `.NET SDK 10.0.106` verified present; NuGet packages verified current |
| Test framework defaults (v3 parallelism, template default) | MEDIUM | Well-documented broadly, project-specific friction possible — flagged in Assumptions Log |

### Open Questions
1. xUnit v2 vs v3 (team preference)
2. Theory assertion granularity (ExpectedErrorScripts dictionary vs errorCount-only)
3. Theory case naming convention
4. `.gitignore` carve-out (research says no carve-out needed — verify at plan time)

### Ready for Planning
Research complete. Six plans (12-01..12-06) can now be authored with:
- Verified target framework (`net10.0`)
- Verified solution file (`flow-sharp.sln`)
- Verified FIX-06 approach (`Lazy<Value>` sufficient, ExceptionDispatchInfo optional)
- Verified FIX-07a mechanics (7 `return;`→`break;`; exact line numbers to be re-grep'd; marker update required)
- Verified FIX-05 mechanics (empty-check throw matching Head/Last template)
- Verified `if` overload strategy (wildcard `(Bool, Void, Void)` solves String + Double cases)
- Verified `exportWav` auto-mkdir target (edit `ExportWavInternal` to benefit both exportWav AND writeWav)
- Verified xUnit harness (xunit.v3 3.2.2 + MemberData glob + Console.SetOut capture + serialized Collection)
- Surfaced `test_musical_context_errors.flow` sentinel string risk (must be addressed by plan 12-04 OR plan 12-01)

---

*Phase 12 research complete. Planner can proceed to author 12-01..12-06 PLAN.md files.*
