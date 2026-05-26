# Phase 43: Module Names & Qualified Imports — Pattern Map

**Mapped:** 2026-05-24
**Files analyzed:** 16 (5 new C# files + 6 modified production files + 5 new test fixture files + 1 modified test fixture)
**Analogs found:** 16 / 16

## File Classification

### NEW production files

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `flow-lang/Ast/Statements/ModuleDeclarationStatement.cs` | AST record (statement) | parse-time | `flow-lang/Ast/Statements/ImportStatement.cs` | exact |
| `flow-lang/Runtime/ModuleRegistry.cs` | runtime registry | dict lookup / `use`-time write, member-access-time read | `flow-lang/Runtime/LiveBlockRegistry.cs` (+ `PrngRegistry.cs` for shape) | exact |
| `flow-lang/StandardLibrary/Audio/BeatConversionFunctions.cs` (new file) OR new methods in `MusicalConversions.cs` | stdlib builtin registration (context-dependent) | request-response (reads `ExecutionContext.GetMusicalContext().Tempo`) | `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` lines 359-389 (`RegisterContextDependent`) | exact |

### MODIFIED production files

| Modified File | Role | Data Flow | Closest Analog Within File | Match Quality |
|---------------|------|-----------|----------------------------|---------------|
| `flow-lang/Lexing/SimpleLexer.cs` | reserved-keyword add | parse-time | line 897 `"live" => TokenType.Live` | exact |
| `flow-lang/Lexing/TokenType.cs` | enum value add | parse-time | line 31 `Live, // Phase 38` | exact |
| `flow-lang/Parsing/Parser.cs` | `ParseModuleDeclaration` branch | parse-time | line 651 `ParseImportStatement` + `ParseStatement` cascade at line 89-112 | exact |
| `flow-lang/Runtime/ModuleLoader.cs` | hook after `Interpreter.Execute(program)` | `use`-time | line 116 `interpreter.Execute(program)` (insert right after) | exact |
| `flow-lang/Interpreter/ExpressionEvaluator.cs` | registry-first branch in `EvaluateMemberAccess` | request-response | line 627 method top (insert BEFORE `Evaluate(member.Object)`) | exact |
| `flow-lang/Runtime/ExecutionContext.cs` | new `ModuleRegistry` property | runtime state | line 141 `public PrngRegistry PrngRegistry { get; } = new();` | exact |
| `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` (via Audio module) | beat builtins registered | runtime registration | RegisterContextDependent wiring chain at `BuiltInFunctions.cs:1029` | exact |
| `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` | `delay(Buffer, Beat, ...)` overload | runtime registration | lines 359-389 (existing `delay(Buffer, NoteValue, ...)` Phase 22 overload) | exact |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` | `renderBarAtBeat(Bar, Beat, ...)` overload | runtime registration | lines 1477-1492 (existing `renderBarAtBeat(Bar, Double, ...)` registration) | exact |
| `flow-lang/*.flow` (13 stdlib files) | top-of-file `module <name>` declaration | parse-time | (none yet — first appearance; matches RESEARCH §"`module` declaration in a stdlib `.flow` file" template) | new pattern; sample skeleton |

### NEW test fixture files

| New Test File | Role | Data Flow | Closest Analog | Match Quality |
|---------------|------|-----------|----------------|---------------|
| `flow-lang.Tests/Integration/Phase43/ModuleDeclarationParserTests.cs` (a.k.a. `ModuleLexerTests`) | unit test (parser/lexer) | parse-only AST round-trip | `flow-lang.Tests/Integration/Phase38/LiveBlockParserTests.cs` | exact |
| `flow-lang.Tests/Integration/Phase43/ModuleRegistryTests.cs` | unit test (registry shape) | snapshot/insert | `flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs` (FlowEngine + registry enumeration) | role-match |
| `flow-lang.Tests/Integration/Phase43/QualifiedAccessDispatchTests.cs` | integration test (eval) | FlowEngine.Execute round-trip | `flow-lang.Tests/Integration/Phase38/LiveBlockDeterminismAdvisoryTests.cs` | exact |
| `flow-lang.Tests/Integration/Phase43/ModuleCollisionAdvisoryTests.cs` | unit test (advisory + stderr capture) | stderr capture + dedup | `flow-lang.Tests/Integration/Phase38/LiveBlockDeterminismAdvisoryTests.cs` + `Phase37/StretchAutoAdvisoryTests.cs` | exact |
| `flow-lang.Tests/Integration/Phase43/BeatBuiltinTests.cs` | unit test (builtin call + tempo context) | FlowEngine.Execute round-trip + advisory | `Phase37/StretchAutoAdvisoryTests.cs` + `Phase38/LiveBlockDeterminismAdvisoryTests.cs` | exact |
| `flow-lang.Tests/Integration/Phase43/BeatCompanionOverloadTests.cs` | unit test (overload resolution + WAV/voice output) | FlowEngine.Execute round-trip | `Phase37/StretchAutoAdvisoryTests.cs` (stretch overload + StretchEngine.Process call) | exact |

### MODIFIED test fixture files

| Modified Test File | Role | Data Flow | Reason | Match Quality |
|--------------------|------|-----------|--------|---------------|
| `flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs` | unit test (polarity flip) | reflective registry snapshot | D-10 — flip `Assert.Contains("BeatType", ...)` → `Assert.DoesNotContain(...)` at lines 231-242 | self-reference |

---

## Pattern Assignments

### `flow-lang/Ast/Statements/ModuleDeclarationStatement.cs` (AST record)

**Analog:** `flow-lang/Ast/Statements/ImportStatement.cs` (12 lines, exact-shape template)

**Full file excerpt** (`ImportStatement.cs` lines 1-12):

```csharp
using FlowLang.Core;

namespace FlowLang.Ast.Statements;

/// <summary>
/// Represents an import statement: use "filepath"
/// </summary>
public record ImportStatement(
    SourceLocation Location,
    string FilePath,
    Span? Span = null) : Statement(Location);
```

**Why:** Single-field statement that mirrors `ImportStatement` exactly — `string Name` replaces `string FilePath`. Per RESEARCH lines 322-332. The recommended planner shape is:

```csharp
namespace FlowLang.Ast.Statements;

public record ModuleDeclarationStatement(
    SourceLocation Location,
    string Name,
    Span? Span = null) : Statement(Location);
```

**Note:** A richer alternative analog is `TuningContextStatement.cs` (43 lines, documents the "parallel-to-MusicalContextStatement rather than 6th enum variant" decision). Use it when writing the XML doc comment to justify "new AST record vs. tagged literal in ExpressionStatement" (planner rejected the latter per RESEARCH A1).

---

### `flow-lang/Runtime/ModuleRegistry.cs` (registry, per-ExecutionContext)

**Analog:** `flow-lang/Runtime/LiveBlockRegistry.cs` (99 lines, closest by shape — keyed dictionary, `Register` / `Snapshot` / `Clear` API)

**Header pattern + class shape** (`LiveBlockRegistry.cs` lines 34-78):

```csharp
public sealed class LiveBlockRegistry
{
    private readonly ConcurrentDictionary<int, LiveBlockRegistration> _registry = new();

    /// <summary>
    /// Registers (or replaces) the registration for a given
    /// <see cref="LiveBlockRegistration.BlockId"/>. Replacement semantics
    /// mirror Plan 38-01's per-block pending-buffer staging — on each
    /// re-render the interpreter calls Register again ... last-write-wins.
    /// </summary>
    public void Register(LiveBlockRegistration registration)
    {
        _registry[registration.BlockId] = registration;
    }

    public IReadOnlyDictionary<int, LiveBlockRegistration> Snapshot()
    {
        return new Dictionary<int, LiveBlockRegistration>(_registry);
    }

    public void Clear()
    {
        _registry.Clear();
    }
}
```

**Secondary analog:** `PrngRegistry.cs` lines 78-89 (`GetRandom(...)` lookup-or-create pattern) and lines 44-58 (registry-field declaration with multi-paragraph XML doc explaining "singleton-per-ExecutionContext, NOT static singleton").

**Why this shape for ModuleRegistry:**
- `LiveBlockRegistry` is the freshest (Phase 38) registry — same conventions (`sealed class`, `Snapshot()` returning fresh dict so callers can iterate, `Clear()` for boundary reset).
- For Phase 43 the value type is `Dictionary<string, Value>` (ExportedProcSet — per RESEARCH §Pattern 4 alternative 4 — a snapshot of user-defined procs added during the `use`).
- API surface needed: `Register(name, exports)`, `Contains(name)`, `TryGetProc(moduleName, procName, out Value? proc)` — combination of the two analogs' shapes.

**Recommended skeleton** (planner adapts):

```csharp
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FlowLang.Runtime;

public sealed class ModuleRegistry
{
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, Value>> _modules = new();

    public bool Contains(string moduleName) => _modules.ContainsKey(moduleName);

    public void Register(string moduleName, IReadOnlyDictionary<string, Value> exportedProcs)
    {
        // Last-write-wins on duplicate; caller (ModuleLoader) emits D-06 advisory before calling
        _modules[moduleName] = exportedProcs;
    }

    public bool TryGetProc(string moduleName, string procName, out Value? procValue)
    {
        procValue = null;
        if (_modules.TryGetValue(moduleName, out var procs)
            && procs.TryGetValue(procName, out var v))
        {
            procValue = v;
            return true;
        }
        return false;
    }
}
```

---

### `ExecutionContext.cs` — ModuleRegistry field add

**Analog:** `flow-lang/Runtime/ExecutionContext.cs` lines 141-156 (PrngRegistry + LiveBlockRegistry property declarations)

**Excerpt** (lines 131-156):

```csharp
/// <summary>
/// Phase 36 Plan 36-01 (D-v1.5-06 / D-36-09) — per-context PRNG registry
/// keyed by <c>(SourceLocation, generator-name)</c>. ... Reseeded
/// at every <c>renderSong</c> / <c>writeWav</c> / <c>exportWav</c> boundary
/// to preserve the two-run cmp-clean determinism contract.
/// </summary>
public PrngRegistry PrngRegistry { get; } = new();

/// <summary>
/// Phase 38 Plan 38-02 (LIVE-01 / D-38-02) — per-context registry of the
/// composer's active <c>live &lt;quantize&gt; { ... }</c> blocks ...
/// Mirrors the <see cref="PrngRegistry"/> shape — singleton-per-context,
/// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/>-backed
/// for the background-render + audio-thread two-actor pattern.
/// </summary>
public LiveBlockRegistry LiveBlockRegistry { get; } = new();
```

**Why:** Insert `public ModuleRegistry ModuleRegistry { get; } = new();` immediately after `LiveBlockRegistry` (around line 157) with an XML doc that follows the exact `Phase N Plan N-NN — ... Mirrors the <see cref="X"/> shape ...` cadence. Per RESEARCH §Pattern 4 lines 350-367 and A1 (per-ExecutionContext, not process-global).

---

### `flow-lang/StandardLibrary/Audio/BeatConversionFunctions.cs` (NEW — or extend `MusicalConversions.cs`)

**Analog:** `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` lines 359-389 (`RegisterContextDependent` — Phase 22 DX-12 canonical pattern)

**Full excerpt** (`EffectsFunctions.cs:359-389`):

```csharp
/// <summary>
/// DX-12 (Phase 22 plan 22-04): registers the NoteValue-rate delay overload that reads
/// MusicalContext.Tempo at call time. Existing ms-rate delay (Register/RegisterDelay)
/// stays byte-identical — this method only ADDS a new signature, never mutates the
/// existing one.
///
/// Called from <see cref="BuiltInFunctions.RegisterContextDependentFunctions"/> alongside
/// <c>RegisterEuclideanOverloads</c>. The closure captures <paramref name="context"/> so
/// that the active tempo is read fresh at each call (inside or outside a tempo block).
/// </summary>
public static void RegisterContextDependent(
    InternalFunctionRegistry registry,
    FlowLang.Runtime.ExecutionContext context)
{
    var delaySyncedSig = new FunctionSignature("delay",
        [BufferType.Instance, NoteValueType.Instance, DoubleType.Instance, DoubleType.Instance],
        ParameterNames: ["buf", "rate", "feedback", "mix"]);
    registry.Register("delay", delaySyncedSig, args =>
    {
        var buffer = args[0].As<AudioBuffer>();
        int noteValueEnum = args[1].As<int>();
        float feedback = (float)args[2].As<double>();
        float mix = (float)args[3].As<double>();

        // Read tempo fresh from the active MusicalContext (inside a tempo block) or
        // fall back to 120 BPM when no tempo is active. Matches the
        // `context.GetMusicalContext().Tempo ?? 120.0` pattern used throughout the
        // interpreter (Interpreter.cs:200, Interpreter.cs:210).
        double bpm = context.GetMusicalContext().Tempo ?? 120.0;
        double delayMs = NoteValueToMs((NoteValueType.Value)noteValueEnum, bpm);

        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

        var result = Delay.Apply(buffer, (float)delayMs, feedback, mix);
        return Value.Buffer(result);
    });
}
```

**Why exactly this analog:** It is THE canonical Phase 22 RegisterContextDependent recipe explicitly cited by RESEARCH §"Reusable Assets" and §Don't-Hand-Roll table. The closure captures `context` so the active tempo is read fresh at each call. Phase 43 `beatToSec`/`secToBeat` MUST use this exact shape (RESEARCH §Anti-Patterns line 428: "Don't add `BeatType.CanConvertTo(SecondType)` override — pure-function FlowType methods have NO runtime context access").

**Wiring site for the new method:** `flow-lang/StandardLibrary/BuiltInFunctions.cs` line 1035, immediately after the existing `Audio.EffectsFunctions.RegisterContextDependent(registry, context);` call:

```csharp
// At BuiltInFunctions.cs:1029-1038 — the RegisterContextDependent wiring chain
public static void RegisterContextDependentFunctions(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
{
    Audio.SongRenderer.RegisterContextDependent(registry, context);
    Harmony.HarmonyFunctions.RegisterContextDependent(registry, context);
    Audio.EffectsFunctions.RegisterContextDependent(registry, context);  // Phase 22-04 DX-12
    // NEW Phase 43 wave:
    // Audio.BeatConversionFunctions.RegisterContextDependent(registry, context);  // Phase 43 D-08
    Transforms.TransformFunctions.RegisterContextDependent(registry, context);
    Audio.Vocalization.VocalizationFunctions.RegisterContextDependent(registry, context);
    Audio.MidiExport.RegisterContextDependent(registry, context);
}
```

**Concrete builtin recipe** (per RESEARCH lines 584-628 + the canonical advisory pattern):

```csharp
var beatToSecSig = new FunctionSignature("beatToSec",
    [BeatType.Instance],
    ParameterNames: ["beats"]);
registry.Register("beatToSec", beatToSecSig, args =>
{
    double beats = args[0].As<double>();
    double? tempo = context.GetMusicalContext().Tempo;
    double bpm = tempo ?? 120.0;
    if (tempo == null)
    {
        RenderingDiagnostics.WarnOnce(
            "beatToSec-no-tempo",
            "[beatToSec] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)");
    }
    double seconds = beats * (60.0 / bpm);
    return Value.Second(seconds);
});
```

Symmetric `secToBeat` mirrors the same shape — see RESEARCH lines 611-628.

---

### `flow-lang/Lexing/SimpleLexer.cs` — `module` keyword add

**Analog:** lines 874-919 (the keyword `switch` expression)

**Excerpt** (lines 895-898):

```csharp
"sustainPedal" => TokenType.SustainPedal,
"tuning" => TokenType.Tuning,
"live" => TokenType.Live,             // Phase 38 (LIVE-01) — live <quantize> { ... } block (D-38-02)
"match" => TokenType.Match,
```

**Why:** Single new keyword row. Insert `"module" => TokenType.Module, // Phase 43 (D-03)` adjacent to `"live"` (alphabetic) or end of the block per RESEARCH §Pattern 1 line 296.

---

### `flow-lang/Lexing/TokenType.cs` — `Module` enum value

**Analog:** lines 30-32

**Excerpt:**

```csharp
Tuning,             // Phase 32 (SPEC-2) — tuning <expr> { ... } musical-context block (D-13)
Live,               // Phase 38 (LIVE-01) — live <quantize> { ... } block (D-38-02)
Match,              // Phase 35 Plan 35-05 (LANG-01) — (match scrutinee | pat => body | ...)
```

**Why:** Insert `Module, // Phase 43 (D-03) — module <name> top-of-file declaration` immediately before or after `Live,` per RESEARCH §Pattern 1 line 310.

---

### `flow-lang/Parsing/Parser.cs` — `ParseModuleDeclaration` + `ParseStatement` branch

**Analog 1 — `ParseImportStatement`** (`Parser.cs` lines 651-656):

```csharp
private ImportStatement ParseImportStatement()
{
    var location = PreviousToken.Location;
    var path = Expect(TokenType.StringLiteral, "Expected string literal for import path");
    return new ImportStatement(location, (string)path.Value!, Span: new Span(location, PreviousToken.Location));
}
```

**Analog 2 — `ParseStatement` cascade** (`Parser.cs` lines 89-102):

```csharp
if (Match(TokenType.Proc))
    return ParseProcDeclaration(false);

if (Match(TokenType.Internal))
{
    Expect(TokenType.Proc, "Expected 'proc' after 'internal'");
    return ParseProcDeclaration(true);
}

if (Match(TokenType.Return))
    return ParseReturnStatement();

if (Match(TokenType.Use))
    return ParseImportStatement();
```

**Why:** `ParseModuleDeclaration` is structurally identical to `ParseImportStatement` (one identifier instead of one string). Add the `Match(TokenType.Module)` branch BEFORE `TokenType.Proc` per RESEARCH §Pattern 1 line 317. Position-constraint (D-01: first non-comment statement) is enforced at `Parser.Parse()` top by tracking "have we seen any non-comment, non-module statement?" — see RESEARCH §Pattern 3 line 348 ("position constraint enforcement"). The exact recipe is in RESEARCH lines 337-345.

---

### `flow-lang/Runtime/ModuleLoader.cs` — module-declaration capture hook

**Analog (insertion site):** `ModuleLoader.cs` lines 114-125 (the post-Execute path)

**Excerpt:**

```csharp
// 4. Execute in current context (no new frame - imports add to current scope)
var interpreter = ParentInterpreter ?? new Interpreter.Interpreter(context, _errorReporter, this);
interpreter.Execute(program);

_loadedModules.Add(resolvedPath);
if (_errorReporter.HasErrors)
{
    _diagnosticOutput?.WriteLine($"[verbose] Failed to load module: {resolvedPath} - errors during execution");
    return ModuleLoadResult.Error;
}
```

**Why:** Per RESEARCH §Pattern 5 lines 371-393 + Pitfall 7 (line 514): the module-registration hook goes BETWEEN `interpreter.Execute(program)` and `_loadedModules.Add(resolvedPath)`. It must execute exactly ONCE per resolvedPath (the early-out `if (_loadedModules.Contains(resolvedPath))` at line 53 already guarantees this — the hook lives in the first-load branch only).

**Recommended hook shape** (planner adapts):

```csharp
interpreter.Execute(program);

// Phase 43 (D-05): if the program declares `module <name>` as its first
// non-comment statement, register the module name → exported procs in the
// context-level ModuleRegistry. Files without a declaration stay unqualified-only
// per D-01 back-compat.
if (program.Statements.Count > 0
    && program.Statements[0] is ModuleDeclarationStatement modDecl)
{
    if (context.ModuleRegistry.Contains(modDecl.Name))
    {
        RenderingDiagnostics.WarnOnce(
            $"module-dup:{modDecl.Name}",
            $"[module] duplicate module name '{modDecl.Name}' — last load wins");
    }
    // Snapshot/diff strategy per RESEARCH A2: walk program.Statements for
    // ProcDeclaration nodes (more direct than diffing StackFrame.Functions).
    var exportedProcs = new Dictionary<string, Value>();
    foreach (var stmt in program.Statements)
    {
        if (stmt is ProcDeclaration proc
            && context.GlobalFrame.Functions.TryGetValue(proc.Name, out var procValue))
        {
            exportedProcs[proc.Name] = procValue;
        }
    }
    context.ModuleRegistry.Register(modDecl.Name, exportedProcs);
}

_loadedModules.Add(resolvedPath);
```

---

### `flow-lang/Interpreter/ExpressionEvaluator.cs` — registry-first branch in `EvaluateMemberAccess`

**Analog (insertion site):** `ExpressionEvaluator.cs` lines 627-708 — full `EvaluateMemberAccess`

**Excerpt (lines 627-641 — top of method showing existing dispatch):**

```csharp
private Value EvaluateMemberAccess(MemberAccessExpression member)
{
    var obj = Evaluate(member.Object);

    // Handle known types with property maps
    if (obj.Data is StandardLibrary.Audio.Voice voice)
    {
        return member.MemberName switch
        {
            "OffsetBeats" => Value.Double(voice.OffsetBeats),
            "Gain" => Value.Double(voice.Gain),
            "Pan" => Value.Double(voice.Pan),
            _ => ReportUnknownMember(obj.Type, member.MemberName, member.Location)
        };
    }
    // ... Track, ChordData, BarData, SectionData, SongData, reflection fallback ...
```

**Why:** Per RESEARCH §Pattern 6 lines 398-421 + Pitfall 2 (line 470): inject the registry-first branch BEFORE `var obj = Evaluate(member.Object);` so a bare `math` identifier (not declared as a variable, would otherwise error) is short-circuited into a function-value lookup. Gate on `member.Object is VariableExpression varExpr` so all existing instance-member access (`chord.root`, `song.sections`, `voice.Pan`) is preserved (those LHSes are NOT bare `VariableExpression` references to a registered module name).

**Insertion shape:**

```csharp
private Value EvaluateMemberAccess(MemberAccessExpression member)
{
    // Phase 43 (D-02): registry-first branch — when LHS is a bare identifier
    // matching a registered module name, return the named proc as a Function Value.
    // Falls through to instance-member dispatch otherwise. KEY DETAIL: peek at the
    // AST shape BEFORE evaluating member.Object — a bare `math` identifier is NOT
    // a value (the variable isn't declared anywhere), so the existing code path
    // would error. Short-circuiting here avoids the spurious error.
    if (member.Object is VariableExpression varExpr
        && _context.ModuleRegistry.TryGetProc(varExpr.Name, member.MemberName, out var procValue))
    {
        return procValue!;
    }

    var obj = Evaluate(member.Object);
    // ... existing 75 lines unchanged ...
```

---

### `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` — `delay(Buffer, Beat, ...)` overload

**Analog 1 — existing Phase 22 NoteValue overload** (lines 359-389, shown above).

**Analog 2 — existing Phase 26.2 Millisecond overload** (lines 299-306):

```csharp
// Phase 26.2 ERG-02: delay(Buffer, Millisecond, Double, Double) — explicit ms ergonomics.
// Delegates to existing DelayEffect lambda; Millisecond's CLR backing IS double
// (Value.Millisecond factory wraps a double — see Value.cs:36), so
// args[1].As<double>() reads it directly without per-overload coercion.
var delayMsSig = new FunctionSignature("delay",
    [BufferType.Instance, MillisecondType.Instance, DoubleType.Instance, DoubleType.Instance],
    ParameterNames: ["buf", "timeMs", "feedback", "mix"]);
registry.Register("delay", delayMsSig, DelayEffect);
```

**Why:** The Beat overload sits exactly between the NoteValue analog (reads tempo, computes ms, calls `Delay.Apply`) and the Millisecond analog (registers a new signature row, delegates to existing lambda). For Beat we need the tempo read (since Beat→ms requires BPM), so the NoteValue recipe is the closer match. Insert the Beat overload inside `RegisterContextDependent` directly after the existing `delaySyncedSig` block — RESEARCH lines 549-579 give the exact recipe.

**Concrete shape:**

```csharp
// Phase 43 D-09: delay(Buffer, Beat, Double, Double) — tempo-aware Beat→ms conversion.
var delayBeatSig = new FunctionSignature("delay",
    [BufferType.Instance, BeatType.Instance, DoubleType.Instance, DoubleType.Instance],
    ParameterNames: ["buf", "beats", "feedback", "mix"]);
registry.Register("delay", delayBeatSig, args =>
{
    var buffer = args[0].As<AudioBuffer>();
    double beats = args[1].As<double>();      // BeatType backs double per BeatType.cs:25-28
    float feedback = (float)args[2].As<double>();
    float mix = (float)args[3].As<double>();

    double? tempo = context.GetMusicalContext().Tempo;
    double bpm = tempo ?? 120.0;
    if (tempo == null)
    {
        RenderingDiagnostics.WarnOnce(
            "delay-beat-no-tempo",
            "[delay] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)");
    }
    double delayMs = beats * (60_000.0 / bpm);

    if (buffer.Frames == 0)
        return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

    var result = Delay.Apply(buffer, (float)delayMs, feedback, mix);
    return Value.Buffer(result);
});
```

---

### `flow-lang/StandardLibrary/BuiltInFunctions.cs` — `renderBarAtBeat(Bar, Beat, ...)` overload

**Analog:** existing `renderBarAtBeat(Bar, Double, String, Int, Double)` registration (`BuiltInFunctions.cs:1477-1492`)

**Excerpt:**

```csharp
var renderBarAtBeatSignature = new FunctionSignature(
    "renderBarAtBeat",
    [BarType.Instance, DoubleType.Instance, StringType.Instance, IntType.Instance, DoubleType.Instance],
    ParameterNames: ["bar", "beat", "synth", "sampleRate", "bpm"]);
registry.Register("renderBarAtBeat", renderBarAtBeatSignature, args =>
{
    var bar = (BarData)args[0].Data!;
    double beatOffset = (double)args[1].Data!;
    string synthType = (string)args[2].Data!;
    int sampleRate = (int)args[3].Data!;
    double bpm = (double)args[4].Data!;

    var voices = Audio.BarRenderer.RenderBarAtBeat(bar, beatOffset, synthType, sampleRate, bpm);
    var voiceValues = voices.Select(v => Value.Voice(v)).ToArray();
    return Value.Array(voiceValues, VoiceType.Instance);
});
```

**Why:** New Beat overload registered alongside — `BeatType.Instance` replaces `DoubleType.Instance` at position 1. Lambda body identical (BeatType backs `double`, so `args[1].Data!` cast works the same way). NOTE: this overload could live as a static `Register` method in BuiltInFunctions (no context closure needed — `bpm` is passed explicitly as `args[4]`, so it does NOT need RegisterContextDependent). Per RESEARCH §Pattern "Beat-Companion Overload Recipe": this is a SAME-namespace overload, no advisory required at registration site (only `beatToSec` itself fires the advisory if a caller pipes through).

**Concrete shape:**

```csharp
// Phase 43 D-09: renderBarAtBeat(Bar, Beat, String, Int, Double) — Beat-typed offset companion.
var renderBarAtBeatBeatSig = new FunctionSignature(
    "renderBarAtBeat",
    [BarType.Instance, BeatType.Instance, StringType.Instance, IntType.Instance, DoubleType.Instance],
    ParameterNames: ["bar", "beat", "synth", "sampleRate", "bpm"]);
registry.Register("renderBarAtBeat", renderBarAtBeatBeatSig, args =>
{
    var bar = (BarData)args[0].Data!;
    double beatOffset = (double)args[1].Data!;     // BeatType backs double
    string synthType = (string)args[2].Data!;
    int sampleRate = (int)args[3].Data!;
    double bpm = (double)args[4].Data!;
    var voices = Audio.BarRenderer.RenderBarAtBeat(bar, beatOffset, synthType, sampleRate, bpm);
    return Value.Array(voices.Select(v => Value.Voice(v)).ToArray(), VoiceType.Instance);
});
```

---

### `flow-lang/*.flow` — 13 stdlib module declarations

**Analog (template):** RESEARCH §"`module` declaration in a stdlib `.flow` file" lines 632-643

**Excerpt:**

```flow
Note: Phase 36 Plan 36-05 — @patterns stdlib module
Note: 13 Tidal-style sequence combinators (D-36-01 hybrid)

module patterns

use "@std"

internal proc every (Int: n, Function: cb, Sequence: seq)
internal proc fast (Sequence: seq, Double: factor)
...
```

**Why:** First-non-comment-statement rule per D-01. Notes (Flow's comment form) are allowed before. The 13-file table per CONTEXT.md D-07 + RESEARCH lines 28-43:

| File | Module declaration |
|------|--------------------|
| `audio.flow` | `module audio` |
| `bars.flow` | `module bars` |
| `collections.flow` | `module collections` |
| `composition.flow` | `module composition` |
| `generative.flow` | `module generative` |
| `improv.flow` | `module improv` |
| `notation.flow` | `module notes` (rename per D-07 + Pitfall 6) |
| `notation-io.flow` | `module notation` (claims the canonical name) |
| `osc.flow` | `module osc` |
| `patterns.flow` | `module patterns` |
| `sfz.flow` | `module sfz` |
| `std.flow` | _no declaration_ (always-on prelude) |
| `test.flow` | `module test` |

---

## Pattern Assignments — Test Fixtures

### `flow-lang.Tests/Integration/Phase43/ModuleDeclarationParserTests.cs` (or `ModuleLexerTests.cs`)

**Analog:** `flow-lang.Tests/Integration/Phase38/LiveBlockParserTests.cs` (lines 1-140 read; complete shape covers lex/parse helpers, AST walking, fact patterns)

**Skeleton excerpt** (lines 1-56):

```csharp
using System;
using System.Linq;
using FlowLang.Ast;
using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-02 LIVE-01 — Wave 0 parser tests for the
/// <c>live &lt;quantize&gt; { ... }</c> block surface.
/// ...
/// </summary>
[Collection("FlowScripts")]
public class LiveBlockParserTests : IDisposable
{
    public LiveBlockParserTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    private static Program ParseToProgram(string source, string? fileName = "<test>")
    {
        var errorReporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, errorReporter, fileName);
        var tokens = lexer.Tokenize();
        Assert.False(errorReporter.HasErrors, $"Lex errors: {errorReporter.Errors.FirstOrDefault()?.Message}");
        var parser = new Parser(tokens, errorReporter);
        var program = parser.Parse();
        Assert.False(errorReporter.HasErrors, $"Parse errors: {errorReporter.Errors.FirstOrDefault()?.Message}");
        return program;
    }
```

**Why exact match:** Same project, same Phase-N integration layout, same `[Collection("FlowScripts")]` + `IDisposable` + `RenderingDiagnostics.ResetForTesting()` ceremony, same `ParseToProgram` helper, same `FindLiveBlocks`-style AST walker. The Phase 43 fixture replaces "find LiveBlockStatement" with "find ModuleDeclarationStatement at Statements[0]" and asserts identifier-rule names + position-constraint errors for mid-file `module foo` (REQ-MOD-01).

---

### `flow-lang.Tests/Integration/Phase43/QualifiedAccessDispatchTests.cs`

**Analog:** `flow-lang.Tests/Integration/Phase38/LiveBlockDeterminismAdvisoryTests.cs` (lines 1-78 — full file)

**Skeleton excerpt:** (already shown above — file is the canonical FlowEngine.Execute round-trip with stderr capture)

**Why:** Phase 43 dispatch tests need to exercise `(math.sin 0.5)` from `.flow` source end-to-end. The fixture shape — `using var engine = new FlowEngine(); engine.Execute(source, "<test>");` — is exactly what `LiveBlockDeterminismAdvisoryTests` uses. Replace the `live 1bar { ... }` source with `use "@math"\n(math.sin 0.5)` and assert the dispatch result.

Additionally: REQ-MOD-02 (Pitfall 2 — chord.root must fall through) is the regression-pin form — assert that `(let chord = Cmaj7)` followed by `chord.Root` still returns `"C"` after Phase 43 ships. Use `engine.Execute` then read context state.

---

### `flow-lang.Tests/Integration/Phase43/ModuleCollisionAdvisoryTests.cs`

**Analog:** `flow-lang.Tests/Integration/Phase38/LiveBlockDeterminismAdvisoryTests.cs` (stderr capture + dedup-once-per-process pattern) + `flow-lang.Tests/Integration/Phase37/StretchAutoAdvisoryTests.cs` (regex-based stderr parsing + `CaptureStderr` helper)

**Excerpt from `StretchAutoAdvisoryTests.cs`** (lines 39-56):

```csharp
private static string CaptureStderr(Action action)
{
    var original = Console.Error;
    var sb = new StringBuilder();
    var writer = new StringWriter(sb);
    Console.SetError(writer);
    try
    {
        action();
    }
    finally
    {
        Console.SetError(original);
    }
    return sb.ToString();
}
```

**And** Phase 38's two-run dedup pattern (`LiveBlockDeterminismAdvisoryTests.cs` lines 66-77):

```csharp
// Re-execute the SAME source in the SAME process. The WarnOnce sentinel
// must dedup the advisory — the captured stderr should STILL contain
// exactly one such advisory line.
using var engine2 = new FlowLang.Core.FlowEngine();
var ok2 = engine2.Execute(source, "<test>");
Assert.True(ok2);

stderr = _capturedError.ToString();
int second = stderr.IndexOf(expectedFragment, firstHit + 1, StringComparison.Ordinal);
Assert.True(second < 0,
    $"Expected dedup — second FlowEngine.Execute MUST NOT re-emit the advisory.");
```

**Why:** Phase 43 D-04 / D-06 advisories are stderr-emitting + dedup-once. The Phase 38 pattern proves dedup across two FlowEngine.Execute calls in same process; Phase 37 pattern provides the per-test `CaptureStderr` helper. Both apply directly.

---

### `flow-lang.Tests/Integration/Phase43/BeatBuiltinTests.cs`

**Analog:** `flow-lang.Tests/Integration/Phase37/StretchAutoAdvisoryTests.cs` (`CaptureStderr` + FlowEngine round-trip + advisory regex assertion)

**Why:** Phase 43 D-08 default-120-BPM advisory is the spitting image of Phase 37's `[stretch] mode=#auto picked: ...` advisory — same `[builtin] message` bracket-tag convention, same `RenderingDiagnostics.WarnOnce` mechanism. The fixture should:

1. Execute `(beatToSec 1.0)` outside any `tempo` block. Assert: returns `0.5s` (1 beat × 60/120) AND stderr contains `[beatToSec] no active tempo — defaulting to 120 BPM`.
2. Execute `tempo 60 { (beatToSec 1.0) }`. Assert: returns `1.0s` (1 beat × 60/60) AND stderr contains NO advisory (tempo is set).
3. Symmetric for `secToBeat`.
4. Two-call dedup assertion (per RESEARCH Pitfall 8 + LiveBlockDeterminismAdvisoryTests pattern).

---

### `flow-lang.Tests/Integration/Phase43/BeatCompanionOverloadTests.cs`

**Analog:** `flow-lang.Tests/Integration/Phase37/StretchAutoAdvisoryTests.cs` (FlowEngine.Execute round-trip with WAV input + assertion on output shape) + `LiveBlockParserTests.cs` for AST verification of overload resolution.

**Why:** Phase 43 D-09 overloads need round-trip assertions:

1. `tempo 120 { (delay buf 0.5b 0.3 0.5) }` — should return a Buffer with delay applied as if `(delay buf 250.0 0.3 0.5)` ran (0.5 beat × 60_000/120 = 250 ms). RMS-equivalence assertion sufficient.
2. `tempo 120 { (renderBarAtBeat bar 1.0b "piano" 44100 120.0) }` — assert returned voice array length matches the existing `Double` overload path.
3. Overload-resolution sanity: parse `(delay buf 0.5b 0.3 0.5)` and reflectively look up the registered signature via `engine.Context.InternalRegistry.EnumerateSignatures()` — confirm the BeatType-arity signature is present. This pattern is identical to Phase 42 `AuditHarnessTests.Registry_WiresSfzAndNotationIoAndOsc` (lines 279-298).

---

### `flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs` — D-10 polarity flip

**Analog (self-reference):** lines 231-242 — the existing fixture

**Current state:**

```csharp
[Fact]
public void OrphanList_ContainsBeatType()
{
    var snap = _snapshot.Value;
    Assert.Contains("BeatType", snap.CoercibleOrphans);
    // Failure message context for future maintainers:
    // BeatType must appear in the orphan list — see RESEARCH.md §Summary.
    // ... If a producer/consumer for Beat shipped (e.g. a new signature
    // accepting a Beat parameter), this test needs to be updated to drop
    // BeatType from the expected-orphan set ...
}
```

**Target state** (rename method + flip assertion + update XML doc):

```csharp
[Fact]
public void OrphanList_DoesNotContainBeatType()
{
    var snap = _snapshot.Value;
    Assert.DoesNotContain("BeatType", snap.CoercibleOrphans);
    // Phase 43 closure context:
    // Before Phase 43, BeatType was the SOLE coercible orphan (AUDIT.md §1 anchor).
    // Phase 43 shipped `delay(Buffer, Beat, ...)` + `renderBarAtBeat(Bar, Beat, ...)`
    // + `beatToSec(Beat)` + `secToBeat(Second)` — Beat now has consumers, so the
    // orphan-detection rule (coercible AND zero signatures accept it) no longer
    // applies. If a future refactor drops the Beat-companion overloads, this fact
    // will fail with "BeatType found in CoercibleOrphans" — the expected failure
    // mode that protects against accidental regression.
}
```

**Why:** Same-commit atomic change with the Beat-companion overload registration. Per RESEARCH Pitfall 5 (line 494) — splitting across waves breaks the test suite.

---

## Shared Patterns (cross-cutting)

### S1: Charitable-advisory via `RenderingDiagnostics.WarnOnce`

**Source:** `flow-lang/Diagnostics/RenderingDiagnostics.cs:29`
**Apply to:** D-04 module shadow advisory, D-06 duplicate-module advisory, D-08 default-120-BPM advisory (`beatToSec` / `secToBeat` / `delay(Beat)`)

**Excerpt:**

```csharp
public static void WarnOnce(string sentinelKey, string message)
{
    lock (_lock)
    {
        if (!_emitted.Add(sentinelKey)) return;
    }
    Console.Error.WriteLine(message);
}
```

**Sentinel key conventions (locked by precedent):**
- Per-resource: `module-dup:<name>`, `module-shadow:<a>:<b>:<name>`
- Per-builtin: `beatToSec-no-tempo`, `secToBeat-no-tempo`, `delay-beat-no-tempo`

**Per RESEARCH §"Established Patterns" line 153 + 117 inventoried call sites.**

### S2: Per-ExecutionContext registry field declaration

**Source:** `flow-lang/Runtime/ExecutionContext.cs:141, 156, 177` (PrngRegistry / LiveBlockRegistry / StyleRegistry)
**Apply to:** new `ModuleRegistry` property
**Excerpt:** see "ExecutionContext.cs — ModuleRegistry field add" section above.

### S3: AST record convention

**Source:** `flow-lang/Ast/Statements/ImportStatement.cs` (12-line minimal) + `flow-lang/Ast/Statements/TuningContextStatement.cs` (43-line richly-documented)
**Apply to:** new `ModuleDeclarationStatement`
**Excerpt:** see ModuleDeclarationStatement section above.

### S4: `RegisterContextDependent` closure capturing `ExecutionContext`

**Source:** `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs:359-389`
**Apply to:** `beatToSec`, `secToBeat`, `delay(Buffer, Beat, ...)` — anything that reads `MusicalContext.Tempo` at call time
**Why:** Phase 22 DX-12 canonical recipe; closure pattern (vs stateless `args =>`) is what makes runtime context readable.

### S5: Test fixture ceremony

**Source:** `flow-lang.Tests/Integration/Phase38/LiveBlockParserTests.cs` (parser fixtures) + `flow-lang.Tests/Integration/Phase38/LiveBlockDeterminismAdvisoryTests.cs` (advisory/dispatch fixtures)
**Apply to:** all 5 new Phase 43 test files
**Ceremony:**

```csharp
[Collection("FlowScripts")]
public class XYZ : IDisposable
{
    public XYZ() { RenderingDiagnostics.ResetForTesting(); }
    public void Dispose() { RenderingDiagnostics.ResetForTesting(); }
    // ... [Fact]s ...
}
```

---

## No Analog Found

(None — every new-file slot in Phase 43 has a tight in-tree analog.)

---

## Metadata

**Analog search scope:** `flow-lang/Ast/`, `flow-lang/Runtime/`, `flow-lang/Lexing/`, `flow-lang/Parsing/`, `flow-lang/Interpreter/`, `flow-lang/StandardLibrary/Audio/`, `flow-lang/StandardLibrary/BuiltInFunctions.cs`, `flow-lang/Diagnostics/`, `flow-lang.Tests/Integration/Phase37/`, `flow-lang.Tests/Integration/Phase38/`, `flow-lang.Tests/Integration/Phase42/`

**Files scanned:** ~30 production files + 8 test files (full reads on 12 primary analogs; targeted Grep+offset reads on the rest)

**Pattern extraction date:** 2026-05-24
