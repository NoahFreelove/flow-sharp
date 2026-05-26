# Phase 44: Strict Mode - Pattern Map

**Mapped:** 2026-05-24
**Files analyzed:** 28 distinct files (1 modified/created in each integration point + ~19 stdlib site-rewrite modules + 13 new test files)
**Analogs found:** 28 / 28

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/Lexing/PragmaRegistry.cs` | registry / closed-set | dict-literal addition | self (D-01..D-06 existing entries lines 30-35) | exact |
| `flow-lang/Runtime/ExecutionContext.cs` | runtime field carrier | bool flag | self (`OscEnabled` line 301, `SfzEnabled` line 280, `NotationIoEnabled` line 289) | exact |
| `flow-lang/Runtime/ModuleLoader.cs` | per-file load + pragma bind | per-file metadata pattern | self (lines 83-92 PragmaScanner.Scan-per-module) | exact |
| `flow-lang/Ast/Statements/ProcDeclaration.cs` | AST record | parse-time bool attachment | self (defaulted `Span?` field) + Phase 35 `MatchExpression.CapturedPragmas` precedent | exact |
| `flow-lang/Parsing/Parser.cs` (line 384) | AST construction site | parse-time pragma threading | self (line 1794 `MatchExpression.CapturedPragmas: _pragmaSet`) | exact |
| `flow-lang/TypeSystem/FunctionSignature.cs` | dispatch predicate | tier-disable predicate | self (`Matches` lines 78-135 + `CalculateSpecificity` lines 140-175) | exact |
| `flow-lang/TypeSystem/OverloadResolver.cs` | dispatch / strict-bit forwarder | predicate threading | self (`Resolve` lines 49-247) | exact |
| `flow-lang/Interpreter/ExpressionEvaluator.cs` | call boundary | snapshot save/restore | self (lines 399-409 `prevCallSite` pattern) | exact |
| `flow-lang/Interpreter/Interpreter.cs` | proc-entry push/pop | try/finally lifecycle | self (lines 1117-1118 `PushFrame()` + matching `PopFrame()` in finally) | exact |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` (lines 150-475) | registration site + Axis C bug fix | overload registration + Void-wildcard | self (`equals`/`lt`/`gt` Void-wildcard lines 438-472) | exact |
| `flow-lang/StandardLibrary/ConversionFunctions.cs` (NEW) | builtin registration | always-available conversions | `BuiltInFunctions.cs:247-253` (`doubleToInt`/`intToDouble`) + Phase 36 `MarkovFunctions.RegisterContextDependent` | exact |
| `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (13 §6a sites) | clamp → strict-error rewrite | per-site branch | self (`Crescendo` line 646 + RESEARCH §"Axis B Site Rewrite") | exact |
| `flow-lang/StandardLibrary/Generative/MarkovFunctions.cs` (lines 220-256) | advisory → strict-error rewrite | per-site branch | self (`ClampOrderWithAdvisory` lines 247-258) | exact |
| `flow-lang/StandardLibrary/Generative/{Chaos,Lsystem,Cellular}Functions.cs` | advisory rewrite | per-site branch | `MarkovFunctions.cs:247-258` | exact |
| `flow-lang/StandardLibrary/Improv/JamFunctions.cs` | advisory rewrite (16 sites) | per-site branch | `MarkovFunctions.cs:247-258` | exact |
| `flow-lang/StandardLibrary/Patterns/PatternFunctions.cs` | advisory rewrite (17 sites) | per-site branch | `MarkovFunctions.cs:247-258` | exact |
| `flow-lang/StandardLibrary/Audio/Sfz/{SfzBuiltins,SfzParser,SfzRenderer,SfzSampleCache}.cs` | advisory rewrite (22 sites) | per-site branch | `MarkovFunctions.cs:247-258` | role-match |
| `flow-lang/StandardLibrary/Audio/DSP/{Granular,PitchShift,Stretch}*.cs` | advisory rewrite (5 sites) | per-site branch | `MarkovFunctions.cs:247-258` | role-match |
| `flow-lang/StandardLibrary/Notation/{AbcImport,AbcLexer,MmlImport}.cs` | advisory rewrite (15 sites) | per-site branch | `MarkovFunctions.cs:247-258` | role-match |
| `flow-lang/StandardLibrary/Audio/{SongRenderer,SampledInstrumentRenderer,MidiExport,InputFunctions}.cs` | advisory rewrite (~9 sites) | per-site branch | `MarkovFunctions.cs:247-258` | role-match |
| `flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs` | advisory rewrite (2 sites) | per-site branch | `MarkovFunctions.cs:247-258` | role-match |
| `flow-lang/StandardLibrary/Network/OscFunctions.cs` | advisory rewrite (3 sites) | per-site branch | `MarkovFunctions.cs:247-258` | role-match |
| `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` | advisory rewrite (1 site) | per-site branch | `MarkovFunctions.cs:247-258` | role-match |
| `flow-lang/Ast/Expressions/MatchExpression.cs` consumer (ExpressionEvaluator) | advisory rewrite (1 site) | per-site branch | `MarkovFunctions.cs:247-258` | role-match |
| `flow-interpreter/Repl.cs` (lines 216-223) | REPL meta-command | switch-arm addition | self (`:help`/`:quit`/`:clear`/`:stop` arms at lines 218-222) | exact |
| `flow-lang.Tests/Phase44/` (NEW directory) | xUnit test layout | per-phase test layout | `flow-lang.Tests/Phase36/` (10 files) + `Phase35/` | exact |
| `flow-lang.Tests/Phase44/Phase44ClampGrepConsistencyTests.cs` (NEW) | regression-pin extractor counts | xUnit Facts | `flow-lang.Tests/Integration/Phase42/ClampGrepConsistencyTests.cs` | exact |
| `tests/strict/test_*.flow` + `tests/strict/showcase_strict.flow` (NEW) | `.flow` integration smoke | composer-readable script | `tests/test_tuning_*.flow` (5 existing pragma-using files) | exact |

## Pattern Assignments

### `flow-lang/Lexing/PragmaRegistry.cs` (registry, dict-literal addition)

**Analog:** SELF — `flow-lang/Lexing/PragmaRegistry.cs:27-36` (closed-set with 6 existing entries).

**Existing pattern** (lines 27-36):
```csharp
public static readonly IReadOnlyDictionary<string, string> KnownPragmas =
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["hAsB"] = "Inside note streams, accept 'H' as a synonym for 'B' (German notation).",
        ["justIntonation"] = "5-limit just-intonation render-time tuning rooted at active key tonic (default C major).",
        ["pythagorean"] = "3-limit Pythagorean (chain-of-fifths) render-time tuning rooted at active key tonic.",
        ["equalTemperament"] = "12-tone equal temperament (default). Explicit form for tooling-visible intent.",
        ["scaleLint"] = "Phase 31 D-03: scale-lint is now default-on; this pragma is accepted as a no-op for v1.3 backward compat.",
        ["matchExhaustive"] = "Phase 35 D-v1.5-05: promote non-exhaustive match warnings to errors. File-scope only; does NOT propagate via use imports (Pitfall 4)."
    };
```

**Implementation hint:** Phase 44 adds ONE row between lines 35-36, immediately after the existing `matchExhaustive` row. Description text fixed by CONTEXT D-04 verbatim. Levenshtein typo recovery (`SuggestNearest` at line 61) and `IsKnown` lookup at line 39 are unchanged — they consume `KnownPragmas` by reflection.

---

### `flow-lang/Runtime/ExecutionContext.cs` (runtime field carrier, +2 bool fields)

**Analog:** SELF — `OscEnabled` at line 301 (Phase 38), `SfzEnabled` at line 280 (Phase 33), `NotationIoEnabled` at line 289 (Phase 39).

**Existing pattern for module-gate booleans** (line 291-301):
```csharp
/// <summary>
/// Phase 38 Plan 38-06 OSC-01 — flips <c>true</c> when the
/// <c>__enableOscModule</c> marker builtin runs (triggered by
/// <c>use "@osc"</c> in a script). Until then, <c>oscSend</c> /
/// <c>oscListen</c> / <c>oscStop</c> / <c>oscBundle</c> /
/// <c>oscSendBundle</c> are gated off and raise a clear
/// "requires <c>use \"@osc\"</c>" error. Mirrors the Phase 33
/// <see cref="SfzEnabled"/> / Phase 39 <see cref="NotationIoEnabled"/>
/// posture. Default <c>false</c>.
/// </summary>
public bool OscEnabled { get; set; } = false;
```

**Implementation hint:** lines 280, 289, 301 show the exact `public bool <Name> { get; set; } = false;` shape with XML doc. Phase 44 adds TWO fields adjacent to these (per D-02 + D-05):
- `StrictMode` — per-declaring-file bit set by ModuleLoader push/pop.
- `CallerStrictMode` (or `StrictModeAtCallSite` per CONTEXT Claude's-Discretion) — set/restored at the `ExpressionEvaluator.EvaluateFunctionCall` call boundary.
Default `false`. Read-only state — no `Push*`/`Pop*` methods needed; consumers save/restore directly via try/finally. Both fields use auto-properties (not backing fields) per the existing pattern.

**Snapshot-restore precedent** (lines 772, 857 — the existing engine-snapshot path already serializes `OscEnabled`, so any new bool field should follow the same Save/Restore arm if test-isolation needs it).

---

### `flow-lang/Runtime/ModuleLoader.cs` (per-file load + pragma bind)

**Analog:** SELF — lines 83-92 (per-module PragmaSet scan + per-file isolated ErrorReporter).

**Existing pragma-scan-per-file pattern** (lines 78-95):
```csharp
// Phase 21 D-06: each imported file gets its OWN PragmaSet computed
// from THIS file's source. Pragmas declared inside the module do NOT
// leak into the importer's parse session — PRAG-02 isolation is
// enforced structurally by lexical scoping (pragmaSet is a local
// variable, only passed to the lexer + parser of THIS module).
var localReporter = new Diagnostics.ErrorReporter();
var (pragmaSet, transformedSource) =
    Lexing.PragmaScanner.Scan(source, resolvedPath, localReporter);
if (localReporter.HasErrors) { ... return ModuleLoadResult.Error; }

// 3. Lex and parse with the module's own pragmaSet + isolated reporter.
var lexer = new Lexing.SimpleLexer(transformedSource, localReporter, resolvedPath, pragmaSet);
```

**Implementation hint:** the `pragmaSet` local at line 84 is the source of truth for `pragmaSet.Has("strict")`. D-03's "ModuleLoader binds each proc to its declaring file's strict bit" is satisfied because the Parser at line 105 receives this exact `pragmaSet` — every `ProcDeclaration` constructed during the parse can read `_pragmaSet?.Has("strict")` (mirrors Phase 35 `MatchExpression.CapturedPragmas`). Plan-phase decides whether ModuleLoader ALSO pushes `context.StrictMode` for the duration of `interpreter.Execute(program)` at line 117 (mirrors how `FlowEngine.ApplyTuningPragma` at `FlowEngine.cs:311-320` flips file-scope state once between parse + interpret).

---

### `flow-lang/Ast/Statements/ProcDeclaration.cs` (AST record, +bool IsStrict)

**Analog:** SELF + Phase 35 `MatchExpression.CapturedPragmas` precedent (`flow-lang/Parsing/Parser.cs:1794`).

**Current shape** (full file):
```csharp
public record ProcDeclaration(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Parameter> Parameters,
    IReadOnlyList<Statement> Body,
    bool IsInternal,
    Span? Span = null) : Statement(Location);
```

**Implementation hint:** add a trailing `bool IsStrict = false` defaulted parameter (matches the existing `Span? Span = null` defaulted-trailing convention so existing callers and tests of `new ProcDeclaration(...)` remain byte-identical). The Parser threading site is `Parser.cs:384` — a single-line change:
```csharp
return new ProcDeclaration(location, name, parameters, body, isInternal,
    Span: new Span(location, PreviousToken.Location),
    IsStrict: _pragmaSet?.Has("strict") ?? false);
```
Mirrors the existing `MatchExpression.CapturedPragmas: _pragmaSet` threading at `Parser.cs:1794`. Per RESEARCH Assumption A1, plan-phase may prefer this AST flag over a fileName→PragmaSet map on ExecutionContext.

---

### `flow-lang/TypeSystem/FunctionSignature.cs` + `OverloadResolver.cs` (Axis A tier filter)

**Analog:** SELF — `Matches` at lines 78-135 (3-clause acceptance predicate) + `OverloadResolver.Resolve` at lines 49-247.

**Current 3-clause acceptance predicate** (lines 123-131):
```csharp
for (int i = 0; i < InputTypes.Count; i++)
{
    if (!argTypes[i].IsCompatibleWith(InputTypes[i])
        && !argTypes[i].CanConvertTo(InputTypes[i])
        && !InputTypes[i].IsCompatibleWith(argTypes[i]))
    {
        return false;
    }
}
```

**Current specificity tiers** (lines 153-167):
```csharp
if (argType.Equals(paramType))         { score += 1000; }   // exact
else if (argType.IsCompatibleWith(paramType)) { score += 500; }  // compatible
else if (argType.CanConvertTo(paramType))     { score += 100; }  // convertible
```

**Implementation hint:** per RESEARCH Pattern 4 + Pitfall 1, the strict gate filters at `Matches()` (NOT at `CalculateSpecificity`). Add `bool strictMode = false` defaulted parameter to `Matches` (and the parallel varargs branch at line 89). In strict, drop clauses 2 (`CanConvertTo`) AND 3 (`InputTypes[i].IsCompatibleWith(argTypes[i])`) — both are implicit conversions. Default-`false` keeps all existing callers byte-identical. `OverloadResolver.Resolve` at line 49 accepts a defaulted `bool strictMode = false` and forwards to `sig.Matches(argTypes, strictMode)` at line 202. The single read of `context.StrictMode` happens at `ExecutionContext.ResolveFunction` (the one outermost caller) and threads through. Per RESEARCH Pitfall 4, this is a 1-parameter-deep change across ~3 call sites — NOT a `ThreadLocal<bool>`.

---

### `flow-lang/Interpreter/ExpressionEvaluator.cs:399-409` (call-boundary snapshot)

**Analog:** SELF — `prevCallSite` save/restore pattern (lines 399-409). This is THE precedent for ANY per-call-boundary state.

**Existing pattern** (lines 393-409):
```csharp
// Phase 36 Plan 36-05: thread the call-site SourceLocation through
// ExecutionContext.CurrentCallSite so Phase 36 stochastic combinators
// (PatternFunctions.sometimes/degrade/sparseSeq) can key their PRNG
// by (site, name) without a new lambda-signature overload. Save +
// restore so nested builtin calls see their parent's site after the
// inner call returns (stack-like discipline without an actual stack).
var prevCallSite = _context.CurrentCallSite;
_context.CurrentCallSite = call.Location;
try
{
    // Call internal implementation
    return overload.Implementation!(argValues);
}
finally
{
    _context.CurrentCallSite = prevCallSite;
}
```

**Implementation hint:** D-05 adds a SECOND save/restore pair adjacent to the `prevCallSite` lines — `var prevCallerStrict = _context.CallerStrictMode; _context.CallerStrictMode = _context.StrictMode;` paired with `finally { _context.CallerStrictMode = prevCallerStrict; }`. Apply identical save/restore on the qualified-call branch at lines 243-252 (Phase 43 `(mod.fn args)` dispatch — already has its own `prevSite` save/restore at 243+251 — model the strict-bit save/restore on the same shape).

---

### `flow-lang/Interpreter/Interpreter.cs:1105-1168` (proc-entry strict push/pop)

**Analog:** SELF — `ExecuteUserFunctionWithCaptures` at lines 1105-1168 (the `PushFrame()` / `try { ... } finally { PopFrame }` pattern is the lifecycle template).

**Existing try/finally pattern** (lines 1117-1120):
```csharp
// Create new stack frame
_context.PushFrame();
try
{
    // ... bind params, execute body ...
}
// (finally with PopFrame elsewhere in the method)
```

**Implementation hint:** D-02/D-03 push/pop `_context.StrictMode = proc.IsStrict` here in the same try/finally as `PushFrame`. Per RESEARCH Pattern 2:
```csharp
var prevStrict = _context.StrictMode;
_context.StrictMode = proc.IsStrict;
_context.PushFrame();
try { ... } finally { _context.PopFrame(); _context.StrictMode = prevStrict; }
```
Order matters: restore strict bit AFTER the frame pop so any error reporting during pop reads the proc's strict bit. RESEARCH Anti-Pattern 1: never mutate `StrictMode` without paired restore (a throw inside a strict-file proc must leave the caller's bit untouched on unwind).

---

### `flow-lang/StandardLibrary/BuiltInFunctions.cs:150-475` (Axis C pre-strict bug fix + Void-wildcard)

**Analog:** SELF — `equals`/`sequals`/`lt`/`gt`/`lte`/`gte` Void-wildcard registrations at lines 438-472.

**Existing Void-wildcard registration pattern** (lines 438-442):
```csharp
// VoidType.Instance is used as a wildcard/"any type" parameter in these signatures.
// The overload resolver treats Void as compatible with all types, allowing these
// functions to accept arguments of any type.
var equalsSignature = new FunctionSignature(
    "equals",
    [VoidType.Instance, VoidType.Instance],
    ParameterNames: ["a", "b"]);
registry.Register("equals", equalsSignature, StdLib.Equals);
```

**Existing dual-registration for `if`** (lines 399-410 — Bool-Lazy AND Bool-Void-Void coexist):
```csharp
var ifSignature = new FunctionSignature(
    "if", [BoolType.Instance, new LazyType(VoidType.Instance), new LazyType(VoidType.Instance)],
    ParameterNames: ["cond", "then", "else"]);
registry.Register("if", ifSignature, StdLib.If);

// Strict (non-Lazy) if overload — Void-wildcard covers all Bool-T-T concrete shapes
var ifStrictSignature = new FunctionSignature(
    "if", [BoolType.Instance, VoidType.Instance, VoidType.Instance],
    ParameterNames: ["cond", "then", "else"]);
registry.Register("if", ifStrictSignature, StdLib.IfStrict);
```

**Implementation hint:** D-12 pre-strict bug fix adds a Void-wildcard `print` overload (companion to the existing `print(String)` at line 158-162):
```csharp
var printAnySig = new FunctionSignature("print", [VoidType.Instance], ParameterNames: ["s"]);
registry.Register("print", printAnySig, args => { /* auto-str non-strict, error strict */ });
```
Per RESEARCH Pitfall 3, the explicit `print(String)` registration at line 162 wins (+1000 exact) over the new Void-wildcard (+500 compatible) for `(print "hello")` — byte-identical preservation. Similar dual-registration pattern applies for `(if Int x ...)` (already has the Void-wildcard at line 410 — needs StdLib.IfStrict body extension), `(and Int x)` / `(or Int x)` / `(not Int)` (RESEARCH Assumption A6: `not` is NOT registered today — both strict + non-strict overloads must ship in Phase 44). The Phase 36 `RegisterContextDependentFunctions` pattern (line 1029) is the registration site for any builtin needing `ExecutionContext` capture (per RESEARCH Pattern 6 Option (b)).

---

### `flow-lang/StandardLibrary/ConversionFunctions.cs` (NEW — D-08/D-09/D-10 builtins)

**Analog:** `BuiltInFunctions.cs:247-253` (`doubleToInt`/`intToDouble` always-available cross-numeric conversions) + Phase 36 `MarkovFunctions.RegisterContextDependent` (modular Register entry point).

**Existing always-available conversion pattern** (lines 247-253):
```csharp
var doubleToIntSignature = new FunctionSignature("doubleToInt", [DoubleType.Instance],
    ParameterNames: ["value"]);
registry.Register("doubleToInt", doubleToIntSignature, StdLib.DoubleToInt);
```

**Existing modular Register entry point** (Phase 36 MarkovFunctions.cs:110-119):
```csharp
public static void RegisterContextDependent(
    InternalFunctionRegistry registry,
    ExecutionContext context)
{
    var trainSig = new FunctionSignature("markovTrain",
        [SequenceType.Instance, IntType.Instance],
        ParameterNames: ["corpus", "order"]);
    registry.Register("markovTrain", trainSig, args => MarkovTrainDefault(args, context));
    // ...
}
```

**Implementation hint:** new file `flow-lang/StandardLibrary/ConversionFunctions.cs` with a `public static void Register(InternalFunctionRegistry registry)` entry, wired into `BuiltInFunctions.RegisterAllImplementations` near line 41 (alongside `RegisterMath`). 6 forward builtins (`db`/`hz`/`ms`/`sec`/`cents`/`semitones`) with 5 numeric source-type overloads each (Int/Long/Float/Double + idempotent target type) — total 25 forward registrations; `semitones` is Int-ONLY per D-08 (1 registration, NOT 5 — follows `CentType.cs:24-27` pattern where `SemitoneType` has `IsCompatibleWith(Int)` true but NOT Float/Double). 4 reverse extractors (`double`/`float`/`int`/`long`) × 6 tagged music types = 24 reverse registrations. Idempotent target-type overloads (e.g. `(db -12dB)`) use `Value.Decibel(args[0].As<double>())` directly — the underlying CLR double is preserved (`Value.cs` extractor pattern).

---

### `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (13 §6a clamp sites)

**Analog:** SELF — `Crescendo` at line 646 (the simplest exemplar; same template applies to lines 106/107/649/650/657/658/666/667/785/821/904/960/1106).

**Current pure-function clamp shape** (lines 646-652):
```csharp
private static Value Crescendo(IReadOnlyList<Value> args)
{
    var seq = args[0].As<SequenceData>();
    double startVel = Math.Clamp(args[1].As<double>(), 0.0, 1.0);
    double endVel = Math.Clamp(args[2].As<double>(), 0.0, 1.0);
    return Value.Sequence(ApplyVelocityGradient(seq, startVel, endVel));
}
```

**Implementation hint:** each of the 13 sites listed in RESEARCH §"Site Inventory" Table §6a follows the same shape. The transform from charitable→strict-aware requires either:
- (a) lift each `Value Foo(IReadOnlyList<Value> args)` to `Value Foo(IReadOnlyList<Value> args, ExecutionContext ctx, ErrorReporter er)` and re-register via context-dependent path (mirrors Phase 36 MarkovFunctions); OR
- (b) capture `context` + `errorReporter` in the registration lambda's closure at registry.Register call site (mirrors Phase 36 ChaosFunctions pattern).

RESEARCH §Pattern 6 recommends (b) for minimal blast radius. Per-site rewrite shape per RESEARCH lines 612-639:
```csharp
double startRaw = args[1].As<double>();
if (ctx.CallerStrictMode)
{
    if (startRaw < 0.0 || startRaw > 1.0)
    {
        er.ReportError($"[strict] crescendo startVel {startRaw} outside [0.0, 1.0]", ctx.CurrentCallSite);
        return Value.Void();
    }
    // ... continue without clamp ...
}
double startVel = Math.Clamp(startRaw, 0.0, 1.0);  // unchanged charitable path
```
Error strings are LOAD-BEARING — copy verbatim from AUDIT §6a Column 5 with `[strict] ` prefix per D-07. RESEARCH §"Site Inventory" Table §6a pins all 13 error sentinels. The 5 carve-out lines mentioned in CONTEXT integration-points (`Swing.cs`) do NOT exist — Plan 44 RESEARCH §Assumption A8 confirms swing clamps live at TransformFunctions.cs:106-107.

---

### `flow-lang/StandardLibrary/Generative/MarkovFunctions.cs:247-258` (advisory site template — applies to all 113 §6b sites)

**Analog:** SELF — `ClampOrderWithAdvisory` helper at lines 247-258. The cleanest advisory-with-context exemplar in the codebase.

**Current advisory pattern** (lines 247-258):
```csharp
private static int ClampOrderWithAdvisory(int requestedOrder, ExecutionContext ctx)
{
    int clamped = Math.Clamp(requestedOrder, 1, 3);
    if (clamped != requestedOrder)
    {
        RenderingDiagnostics.WarnOnce(
            $"markov:order-clamp:{ctx.CurrentCallSite}:{requestedOrder}",
            $"[markov] order {requestedOrder} clamped to {clamped} at {ctx.CurrentCallSite} "
            + "(GEN-01 limits order to [1, 3])");
    }
    return clamped;
}
```

**Existing module-level context-dependent registration** (lines 110-119):
```csharp
public static void RegisterContextDependent(
    InternalFunctionRegistry registry,
    ExecutionContext context)
{
    var trainSig = new FunctionSignature("markovTrain", ...);
    registry.Register("markovTrain", trainSig, args => MarkovTrainDefault(args, context));
}
```

**Implementation hint:** for each of the ~113 in-scope `WarnOnce` sites across `Patterns/`, `Generative/`, `Improv/`, `Audio/Sfz/`, `Audio/DSP/`, `Notation/`, `Network/`, `Audio/Tuning/`, `Harmony/`, the rewrite is mechanical:
```csharp
if (clamped != requestedOrder)
{
    if (ctx.CallerStrictMode)
    {
        er.ReportError(
            $"[strict] [markov] order {requestedOrder} clamped to {clamped} at {ctx.CurrentCallSite} (GEN-01 limits order to [1, 3])",
            ctx.CurrentCallSite);
        return clamped;  // or Value.Void() depending on call frame
    }
    RenderingDiagnostics.WarnOnce(
        $"markov:order-clamp:{ctx.CurrentCallSite}:{requestedOrder}",
        $"[markov] order {requestedOrder} clamped to {clamped} at {ctx.CurrentCallSite} (GEN-01 limits order to [1, 3])");
}
return clamped;
```
RESEARCH Pitfall 5: the strict error message is a deterministic concat of the same args/sentinel as the non-strict advisory — preserves two-run cmp-clean. RESEARCH Pitfall 2 + CONTEXT D-06 carve-outs: SKIP `Interpreter.cs:476` (`[live]` entering-block advisory) and `Improv/StyleRegistry.cs:156/244/258/265` (4 `[improv]` style-pack discovery advisories). These 5 sites STAY charitable in BOTH modes.

---

### `flow-interpreter/Repl.cs:216-223` (`:strict on/off` meta-commands)

**Analog:** SELF — `:help`/`:quit`/`:clear`/`:stop` switch arms at lines 218-222 + Phase 38 `:help <name>` extension at lines 204-214.

**Current meta-command dispatcher** (lines 199-224):
```csharp
private bool HandleCommand(string command)
{
    // Phase 38 Plan 38-04 (D-38-09): `:help <name>` extension
    var trimmed = command.TrimEnd();
    if (trimmed.StartsWith(":help ", StringComparison.OrdinalIgnoreCase)
        || trimmed.StartsWith(":h ", StringComparison.OrdinalIgnoreCase))
    {
        int spaceIdx = trimmed.IndexOf(' ');
        var name = trimmed.Substring(spaceIdx + 1).Trim();
        if (!string.IsNullOrEmpty(name))
        {
            return ShowHelpForName(name);
        }
    }

    return command.ToLower() switch
    {
        ":quit" or ":q" or ":exit" => false,
        ":help" or ":h" => ShowHelp(),
        ":clear" or ":cls" => ClearScreen(),
        ":stop" => StopAudio(),
        _ => UnknownCommand(command)
    };
}
```

**Implementation hint:** D-16 adds two arms to the switch expression (between `:stop` and the discard):
```csharp
":strict on" => SetStrict(true),
":strict off" => SetStrict(false),
```
The `SetStrict(bool)` helper sets `_sessionStrict` (new private field) AND mutates `_engine.Context.StrictMode = _sessionStrict` immediately. Before each `_engine.ExecuteScriptAndGetResult(input, "<repl>")` call at line 70, mutate `_engine.Context.StrictMode = _sessionStrict` so per-line inputs inherit the sticky flag (RESEARCH Pattern 8). The `HandleCommandForTesting` test-seam at line 232 already exposes `HandleCommand` to xUnit — Phase 44 reuses it for `ReplStrictMetaCommandTests`. UnknownCommand at the discard arm displays the existing "Unknown command" message — typos like `:strikt on` fall through harmlessly.

---

### `flow-lang.Tests/Phase44/Phase44ClampGrepConsistencyTests.cs` (NEW xUnit Theory pin)

**Analog:** `flow-lang.Tests/Integration/Phase42/ClampGrepConsistencyTests.cs` (313 LOC — verifies extractor inventory counts).

**Existing file:line-pinning structure** (lines 76-96):
```csharp
[Fact]
public void AllClamps_CountWithinTolerance()
{
    if (!IsBashAvailable()) return;

    string repoRoot = FindRepoRoot();
    RunBashScript(repoRoot, Path.Combine("scripts", "audit", "clamp-grep.sh"));

    string path = Path.Combine(
        repoRoot, ".planning", "phases", "42-type-system-stdlib-audit",
        "42-AUDIT-data", "all-clamps.txt");

    int lineCount = File.ReadAllLines(path).Length;

    Assert.True(
        lineCount >= AllClampsLowerBound && lineCount <= AllClampsUpperBound,
        $"all-clamps.txt line count {lineCount} outside tolerance " +
        $"[{AllClampsLowerBound}, {AllClampsUpperBound}] (RESEARCH baseline ~72). " +
        "A large drift suggests either (a) Plan 03+ added many clamps " +
        "(adjust upper bound) or (b) the extractor regressed.");
}
```

**Implementation hint:** Phase 44 pins (a) the count of 13 input-perimeter clamps (assert EXACTLY 13 — strict equality, not tolerance — because each is named in RESEARCH §"Site Inventory" Table §6a and pinned by site-specific xUnit Facts); (b) the count of ~126 advisory sites that became `[strict]` errors (allow ±2 tolerance per RESEARCH Open Question 4); (c) per-site verbatim error strings via xUnit `[Theory] [InlineData(...)]` over the 13 §6a clamp messages from AUDIT.md §6a Column 5. The `FindRepoRoot` + `RunBashScript` + `IsBashAvailable` helpers at lines 259-313 can be reused verbatim. The full Phase 44 negative-suite directory mirrors `Phase36/` (10 files, RESEARCH §Wave 0 Gaps lists all 13 .cs files to create).

---

### `flow-lang.Tests/Phase44/` directory (NEW — xUnit test layout)

**Analog:** `flow-lang.Tests/Phase36/` (10 files: `CellularDeterminismTests.cs`, `CellularTests.cs`, `ChaosDeterminismTests.cs`, `ChaosTests.cs`, `JamDeterminismTests.cs`, `JamFunctionsTests.cs`, `LsystemDeterminismTests.cs`, `LsystemModelTests.cs`, `MarkovDeterminismTests.cs`, `MarkovModelTests.cs`).

**Implementation hint:** Phase 44 follows the same `Phase{NN}/` flat namespace pattern (`namespace FlowLang.Tests.Phase44`). The 13 files from RESEARCH §"Wave 0 Gaps":
- `StrictModeNegativeTests.cs` — ~126 Theory rows pinning error strings verbatim
- `ExplicitConversionTests.cs` — forward + reverse direction round-trip
- `OverloadResolverStrictTierTests.cs` — +100 tier disabled
- `PrintCharitablyTests.cs` — non-strict `(print 42)` auto-strs
- `IfTruthyCoerceTests.cs` — non-strict `(if Int x)` truthy
- `CallerStrictModeSnapshotTests.cs` — D-05 snapshot at call boundary
- `ModuleLoaderStrictPropagationTests.cs` — strict file calls non-strict module
- `PragmaRegistryStrictTests.cs` — D-04 closed-set membership + typo
- `LiveBlockStrictTests.cs` — strict applies inside `live { }`
- `ReplStrictMetaCommandTests.cs` — `:strict on/off` toggle
- `AxisCBoolRequiredTests.cs` — strict `(and)`/`(or)`/`(not)` Bool-only
- `CrossTypeComparisonStrictTests.cs` — `(gt 1 "2")` errors in strict
- `Phase44TwoRunDeterminismTests.cs` — SHA-equal across two runs

---

### `tests/strict/*.flow` (NEW — positive `.flow` integration smoke)

**Analog:** 5 existing pragma-using `.flow` test scripts: `tests/test_tuning_equal.flow`, `tests/test_tuning_pythagorean.flow`, `tests/test_tuning_transpose_invariant.flow`, `tests/test_scale_lint.flow`, `tests/test_pragma_isolation.flow`.

**Existing pragma-using test surface** (representative `tests/test_pragma_isolation.flow` shape):
- Begins with `enable <pragma>;` at top of file
- Uses Flow-language builtins exercising the pragma's behavior
- Verifies via `print` output (no unit-test framework — `.flow` tests are console-output verified per CLAUDE.md)

**Implementation hint:** 7 new `tests/strict/test_strict_*.flow` files from RESEARCH §"Wave 0 Gaps":
- `test_strict_axis_a_overload.flow` — explicit conversions only; verifies `(gain buf -12.0)` fails
- `test_strict_axis_b_clamps.flow` — exercises in-range arg to all 13 §6a clamp sites
- `test_strict_explicit_conversions.flow` — `(db -12.0)` / `(hz 440)` / `(cents +50)` / etc.
- `test_strict_equality.flow` — `(equals 1 1.0)` returns false per D-11
- `test_strict_with_justintonation.flow` — both pragmas compose per CONTEXT §specifics
- `test_strict_dict_typecheck.flow` — D-13 Dict type-strict lookup preserved
- `showcase_strict.flow` — ~16 bar single-instrument piece using `(db x)`/`(hz x)`/`(cents x)` naturally per CONTEXT §specifics "Showcase a strict file"

Use Phase 43 qualified imports (`use "@strict-fixtures"`) for any shared helpers across positive tests per D-14.

---

## Shared Patterns

### Pattern S1: File-scope pragma push/pop (D-02/D-03)

**Source:** `flow-lang/Core/FlowEngine.cs:286-320` (`ResetBlockTuningStack` + `ApplyTuningPragma` between parse and interpret).
**Apply to:** ExecutionContext field-mutation site in ModuleLoader / FlowEngine.

**Existing pattern** (lines 286-320):
```csharp
_context.ResetBlockTuningStack();
ApplyTuningPragma(program);

// 4. Interpret AST
_interpreter.Execute(program);
```

```csharp
private void ApplyTuningPragma(Ast.Program program)
{
    if (program.Pragmas.Has("justIntonation"))
        _context.SetFileScopeTuning(BuildPragmaTuning(TuningSystem.JustIntonation));
    // ...
}
```

**Phase 44 application:** Between parse and interpret in both `FlowEngine.Execute` (line 287) and `ModuleLoader.LoadModule` (after line 105 parse, before line 117 `interpreter.Execute(program)`), set `_context.StrictMode = pragmaSet.Has("strict")`. Plan-phase decides whether to use a dedicated `ApplyStrictPragma(program)` helper (mirrors `ApplyTuningPragma` cleanly) or inline the one-liner.

---

### Pattern S2: Call-boundary save/restore (D-05)

**Source:** `flow-lang/Interpreter/ExpressionEvaluator.cs:399-409` (the `prevCallSite` precedent).
**Apply to:** All call dispatch paths — unqualified (line 399), qualified Phase 43 module call (lines 243-252), and user-proc invocation (lines 254-256 and 414-415).

```csharp
var prevCallSite = _context.CurrentCallSite;
_context.CurrentCallSite = call.Location;
try
{
    return overload.Implementation!(argValues);
}
finally
{
    _context.CurrentCallSite = prevCallSite;
}
```

**Phase 44 application:** Augment each save/restore site with the matching `CallerStrictMode` pair (`var prevCallerStrict = _context.CallerStrictMode; _context.CallerStrictMode = _context.StrictMode;` paired with `finally { _context.CallerStrictMode = prevCallerStrict; }`). RESEARCH Anti-Pattern 1: NEVER mutate without restore.

---

### Pattern S3: WarnOnce → strict-error per-site rewrite (D-07)

**Source:** `flow-lang/StandardLibrary/Generative/MarkovFunctions.cs:247-258` (the cleanest exemplar).
**Apply to:** All 113 in-scope `WarnOnce` sites across 19 stdlib modules.

```csharp
// BEFORE
RenderingDiagnostics.WarnOnce(
    $"<key>:<loc>",
    $"[<tag>] <body>");

// AFTER
if (ctx.CallerStrictMode)
{
    er.ReportError($"[strict] [<tag>] <body>", ctx.CurrentCallSite);
    return /* early-return value */;
}
RenderingDiagnostics.WarnOnce(
    $"<key>:<loc>",
    $"[<tag>] <body>");
```

**Phase 44 application:** Mechanical text rewrite at each in-scope site. Skip the 5 carve-outs (1 `[live]` + 4 `[improv]` sites). xUnit `[Theory]` pins each verbatim string in `StrictModeNegativeTests.cs`.

---

### Pattern S4: Always-available conversion builtin (D-09)

**Source:** `flow-lang/StandardLibrary/BuiltInFunctions.cs:247-253` (`doubleToInt`/`intToDouble` precedent).
**Apply to:** All 6 forward + 4 reverse direction conversion builtins.

```csharp
var doubleToIntSignature = new FunctionSignature("doubleToInt", [DoubleType.Instance],
    ParameterNames: ["value"]);
registry.Register("doubleToInt", doubleToIntSignature, StdLib.DoubleToInt);
```

**Phase 44 application:** Register in BOTH modes (mode-independent) at engine init. Mirrors the existing `(float x)` / `(int x)` / `(double x)` / `(long x)` pattern. ConversionFunctions.cs is a single new file with one `Register(InternalFunctionRegistry)` entry point wired into `BuiltInFunctions.RegisterAllImplementations`.

---

### Pattern S5: REPL meta-command family (D-16)

**Source:** `flow-interpreter/Repl.cs:218-222` (existing `:help`/`:quit`/`:clear`/`:stop` arms).
**Apply to:** `:strict on` + `:strict off` meta-commands.

```csharp
return command.ToLower() switch
{
    ":quit" or ":q" or ":exit" => false,
    ":help" or ":h" => ShowHelp(),
    ":clear" or ":cls" => ClearScreen(),
    ":stop" => StopAudio(),
    _ => UnknownCommand(command)
};
```

**Phase 44 application:** Add `":strict on" => SetStrict(true)` and `":strict off" => SetStrict(false)` arms; new private `_sessionStrict` field + `SetStrict(bool)` helper mutate `_engine.Context.StrictMode`. The `HandleCommandForTesting` test-seam at line 232 already exists.

---

### Pattern S6: AST flag for file-scope pragma capture

**Source:** `flow-lang/Parsing/Parser.cs:1794` (`MatchExpression.CapturedPragmas: _pragmaSet` — Phase 35 LANG-04 precedent).
**Apply to:** `ProcDeclaration.IsStrict` capture.

```csharp
// In MatchExpression construction:
return new MatchExpression(
    location, expr, arms,
    CapturedPragmas: _pragmaSet);
```

**Phase 44 application:** `ProcDeclaration.IsStrict = _pragmaSet?.Has("strict") ?? false` at `Parser.cs:384`. The Phase 35 precedent threads an entire PragmaSet; Phase 44 threads only the boolean evaluation of `.Has("strict")` (smaller surface, no nullable handling at the read site).

---

## No Analog Found

No files in this phase lack a strong codebase analog — strict mode is mechanically scoped per RESEARCH §"Summary" (one PragmaRegistry entry, two ExecutionContext fields, one push/pop hook, one OverloadResolver predicate, ~126 mechanical site rewrites, ~17 builtin registrations, three test surfaces). Every primitive already exists in the codebase per Phase 21/23/32/33/35/36/38/39 precedents.

## Metadata

**Analog search scope:**
- `flow-lang/Lexing/` (PragmaRegistry, PragmaScanner, PragmaSet)
- `flow-lang/Runtime/` (ExecutionContext, ModuleLoader, StackFrame)
- `flow-lang/Ast/Statements/` + `flow-lang/Ast/Expressions/` (ProcDeclaration, MatchExpression)
- `flow-lang/Parsing/Parser.cs` (Parse-time pragma threading)
- `flow-lang/TypeSystem/` (OverloadResolver, FunctionSignature, CentType, SemitoneType)
- `flow-lang/Interpreter/` (ExpressionEvaluator, Interpreter)
- `flow-lang/Core/FlowEngine.cs` (ApplyTuningPragma file-scope pragma application)
- `flow-lang/StandardLibrary/` (BuiltInFunctions, Transforms/TransformFunctions, Generative/MarkovFunctions, all 19 advisory-site modules)
- `flow-interpreter/Repl.cs` + `LiveReloadManager.cs`
- `flow-lang.Tests/Phase36/` (10-file layout) + `Phase35/` + `Integration/Phase42/ClampGrepConsistencyTests.cs`
- `tests/` (5 existing pragma-using `.flow` scripts)

**Files scanned:** 28 (concrete file:line analogs cited above) + 19 advisory-host modules enumerated in RESEARCH §"Site Inventory" §6b grouping.

**Pattern extraction date:** 2026-05-24
