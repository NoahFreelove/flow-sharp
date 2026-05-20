# Phase 36: Sequence Algebra & Generative - Pattern Map

**Mapped:** 2026-05-20
**Files analyzed:** 51 new/modified files (15 stdlib C#, 8 runtime/type/AST, 3 lexer/parser/resolver, 6 stdlib .flow, 3 example .flow, 14 composer test .flow, 9 xUnit test .cs, 1 shell script)
**Analogs found:** 48 / 51 (3 with partial/composite match)

## File Classification

### New / Modified C# (under `flow-lang/`)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/Runtime/PrngRegistry.cs` | runtime state | request-response (key → Random) | `flow-lang/StandardLibrary/Composition/VariationFunctions.cs` (PRNG seeding) + `flow-lang/Runtime/MusicalContext.cs` (singleton-per-context stack) | composite |
| `flow-lang/Runtime/MarkovModelData.cs` | model / reference-type | data wrapper | `flow-lang/StandardLibrary/Audio/Tuning/ResolvedTuning.cs` (ref-identity wrapper) | role-match |
| `flow-lang/Runtime/LsystemModelData.cs` | model / reference-type | data wrapper | `flow-lang/StandardLibrary/Audio/Tuning/ResolvedTuning.cs` | role-match |
| `flow-lang/TypeSystem/SpecialTypes/MarkovModelType.cs` | type singleton | n/a | `flow-lang/TypeSystem/SpecialTypes/TuningType.cs` | exact |
| `flow-lang/TypeSystem/SpecialTypes/LsystemModelType.cs` | type singleton | n/a | `flow-lang/TypeSystem/SpecialTypes/SfzType.cs` | exact |
| `flow-lang/TypeSystem/FunctionSignature.cs` (modify) | type record | extension | itself (defaulted-param sweep convention) | self |
| `flow-lang/TypeSystem/OverloadResolver.cs` (modify) | type-system dispatcher | named-arg matching | itself (existing specificity scoring) | self |
| `flow-lang/Ast/Statements/SectionDeclaration.cs` (modify) | AST record | extension | `flow-lang/Ast/Statements/ProcDeclaration.cs` (Parameter list + record extension) | role-match |
| `flow-lang/Ast/Expressions/FunctionCallExpression.cs` (modify) | AST record | extension | itself (Phase 35 defaulted-positional-param sweep) | self |
| `flow-lang/Ast/Expressions/SongExpression.cs` (modify) | AST record | extension | itself (SongSectionReference list) | self |
| `flow-lang/Lexing/SimpleLexer.cs` (modify) | lexer | token emission | itself (TryLexSignedNumber Phase 35 sweep for `Match`/`When`) | self |
| `flow-lang/Parsing/Parser.cs` (modify ParseSectionDeclaration) | parser | token → AST | `flow-lang/Parsing/Parser.cs` lines 254-327 (`ParseProcDeclaration` — has typed params) | role-match |
| `flow-lang/Parsing/Parser.cs` (modify function-call parsing) | parser | token → AST | `flow-lang/Parsing/Parser.cs` (existing FunctionCallExpression argument parsing) | self |
| `flow-lang/Interpreter/ExpressionEvaluator.cs` (modify, section call) | evaluator | dispatch | `flow-lang/Interpreter/ExpressionEvaluator.cs` (EvaluateFunctionCall) | role-match |
| `flow-lang/Interpreter/PatternMatcher.cs` (modify, tuple-of-args) | matcher | pattern dispatch | itself (Phase 35 naive linear scan) | self |
| `flow-lang/Runtime/ExecutionContext.cs` (modify, PrngRegistry field + SnapshotState extension) | runtime state | snapshot/restore | itself (lines 519-554, 11-surface contract) | self |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` (modify, RegisterAll) | wiring | registration | itself (lines 34-53) | self |
| `flow-lang/StandardLibrary/Patterns/PatternFunctions.cs` (NEW) | stdlib builtins | Sequence → Sequence | `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (transpose / invert / retrograde) | exact |
| `flow-lang/StandardLibrary/Generative/MarkovFunctions.cs` (NEW) | stdlib builtins (PRNG) | corpus → model → sequence | `flow-lang/StandardLibrary/Composition/VariationFunctions.cs` + `flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs` | composite |
| `flow-lang/StandardLibrary/Generative/LsystemFunctions.cs` (NEW) | stdlib builtins | rules → expanded symbols | `flow-lang/StandardLibrary/Composition/VariationFunctions.cs` (PRNG) + `flow-lang/StandardLibrary/Collections/DictFunctions.cs` (Dict<Symbol,_> reads) | composite |
| `flow-lang/StandardLibrary/Generative/CellularFunctions.cs` (NEW) | stdlib builtins (PRNG) | rule int → grid → seq | `flow-lang/StandardLibrary/Composition/VariationFunctions.cs` | role-match |
| `flow-lang/StandardLibrary/Generative/ChaosFunctions.cs` (NEW) | stdlib builtins | scalar params → series + quantize | `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` (ScaleDatabase reads via context) | role-match |
| `flow-lang/StandardLibrary/Improv/JamFunctions.cs` (NEW) | stdlib builtins (PRNG + ctx) | chord seq → improv seq | `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` (RegisterContextDependent — uses ctx) + `flow-lang/StandardLibrary/Composition/VariationFunctions.cs` (PRNG) | composite |
| `flow-lang/StandardLibrary/Improv/StyleRegistry.cs` (NEW) | stdlib registry | filesystem discovery + Dict storage | `flow-lang/StandardLibrary/Audio/Sfz/SfzPatchRegistry.cs` (per Phase 33) — note: confirm via `find`; fallback is `flow-lang/Runtime/FlowConfig.cs` for XDG-config scan | role-match |

### New `.flow` stdlib modules (under `flow-lang/`)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/patterns.flow` | stdlib module | `internal proc` declarations | `flow-lang/std.flow` lines 135-150 (Transform proc decls) | exact |
| `flow-lang/generative.flow` | stdlib module | `internal proc` declarations | `flow-lang/std.flow` lines 210-213 (euclidean) + lines 243-258 (loadScala / loadSfz forward-decls) | exact |
| `flow-lang/improv.flow` | stdlib module | `internal proc` declarations | `flow-lang/std.flow` lines 243-258 (loadScala / loadSfz forward-decls) | exact |
| `flow-lang/improv/styles/jazz.flow` | rule pack (Flow data) | `(registerStyle #jazz dict)` top-level expression | `flow-lang/std.flow` (top-level Flow file with `use` + decls) — closest pure-data Flow file is none-existent today; use `flow-lang/audio.flow` (top-level `internal proc` declarations) as nearest structural analog | partial |
| `flow-lang/improv/styles/blues.flow` | rule pack (Flow data) | same | same as `jazz.flow` | partial |
| `flow-lang/improv/styles/classical.flow` | rule pack (Flow data) | same | same as `jazz.flow` | partial |

### Example `.flow` files (under `examples/`)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `examples/generative/markov_jazz.flow` | tutorial example | full audio render pipeline | `examples/symphony/symphony.flow` | exact |
| `examples/generative/tidal_combinators.flow` | tutorial example | combinator chain → render | `examples/symphony/symphony.flow` + `tests/test_generative.flow` (note-stream + euclidean + seq print) | composite |
| `examples/sections/parameterized.flow` | tutorial example | section overload demo | `examples/symphony/symphony.flow` (uses `section`) + `tests/test_pattern_match_music.flow` (pattern syntax demos) | composite |

### Composer-facing tests (under `tests/`, 14 files)

All follow the same `use "@std" / use "@test" + (test ... lazy(...))` shape.

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `tests/test_patterns_every.flow` | composer test | combinator → assert | `tests/test_generative.flow` | exact |
| `tests/test_patterns_chain.flow` | composer test | chain → assert | `tests/test_generative.flow` | exact |
| `tests/test_patterns_edge_cases.flow` | composer test | charitable advisory | `tests/test_generative.flow` | exact |
| `tests/test_markov_oneshot.flow` | composer test | gen → assert | `tests/test_generative.flow` | exact |
| `tests/test_markov_train_generate.flow` | composer test | model + gen → assert | `tests/test_generative.flow` | exact |
| `tests/test_lsystem_oneshot.flow` | composer test | rules → assert | `tests/test_generative.flow` | exact |
| `tests/test_lsystem_train_generate.flow` | composer test | model + gen → assert | `tests/test_generative.flow` | exact |
| `tests/test_cellular_rule30.flow` | composer test | rule → grid → assert | `tests/test_generative.flow` | exact |
| `tests/test_cellular_life.flow` | composer test | 2D life → assert | `tests/test_generative.flow` | exact |
| `tests/test_lorenz_quantize.flow` | composer test | chaos → quantize → assert | `tests/test_generative.flow` | exact |
| `tests/test_logistic.flow` | composer test | chaos → assert | `tests/test_generative.flow` | exact |
| `tests/test_section_params.flow` | composer test | section call → render | `tests/test_pattern_match_music.flow` | exact |
| `tests/test_section_overload.flow` | composer test | section overload | `tests/test_pattern_match_music.flow` | exact |
| `tests/test_section_pattern_destructure.flow` | composer test | section + patterns | `tests/test_pattern_match_music.flow` | exact |
| `tests/test_section_repeat.flow` | composer test | `*N` repeat | `tests/test_pattern_match_music.flow` | exact |
| `tests/test_section_defaults.flow` | composer test | section defaults | `tests/test_pattern_match_music.flow` | exact |
| `tests/test_jam_jazz.flow` | composer test | jam → seq → assert | `tests/test_generative.flow` | exact |
| `tests/test_jam_key_override.flow` | composer test | jam + key= → assert | `tests/test_generative.flow` | exact |
| `tests/test_jam_styles.flow` | composer test | 3 packs → assert | `tests/test_generative.flow` | exact |
| `tests/test_named_args.flow` | composer test | named-arg call | `tests/test_pattern_match_music.flow` | role-match |
| `tests/test_prng_determinism.flow` | composer test | two-run cmp | `tests/test_generative.flow` | exact |

### xUnit Phase 36 tests (under `flow-lang.Tests/Phase36/`, 9 files)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang.Tests/Phase36/PatternEveryTests.cs` | unit test | source → eval → assert | `flow-lang.Tests/Phase35/MatchRuntimeTests.cs` | exact |
| `flow-lang.Tests/Phase36/PatternChalkyEdgeCasesTests.cs` | unit test | charitable advisory | `flow-lang.Tests/Phase35/MatchRuntimeTests.cs` | exact |
| `flow-lang.Tests/Phase36/MarkovModelTests.cs` | unit test | model identity + structural eq | `flow-lang.Tests/Phase35/MatchRuntimeTests.cs` | role-match |
| `flow-lang.Tests/Phase36/PrngRegistryTests.cs` | unit test | source-location keying | `flow-lang.Tests/Phase35/HermeticIsolationTests.cs` | role-match |
| `flow-lang.Tests/Phase36/SectionOverloadTests.cs` | unit test | overload dispatch | `flow-lang.Tests/Phase35/MatchParserTests.cs` | role-match |
| `flow-lang.Tests/Phase36/SectionDiagnosticsTests.cs` | unit test | Rust-style errors | `flow-lang.Tests/Phase35/DiagnosticRendererGoldenTests.cs` | exact |
| `flow-lang.Tests/Phase36/NamedArgsParserTests.cs` | unit test | parser AST gates | `flow-lang.Tests/Phase35/MatchParserTests.cs` | exact |
| `flow-lang.Tests/Phase36/NamedArgBackcompatTests.cs` | unit test | positional → resolver | `flow-lang.Tests/Phase35/MatchRuntimeTests.cs` | role-match |
| `flow-lang.Tests/Phase36/ParameterNamesCoverageTest.cs` | unit test (audit) | grep-style assertion | `flow-lang.Tests/Integration/Phase29/LicenseAuditTests.cs` | role-match |

### Misc

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `scripts/test_two_run_determinism.sh` | integration script | render + SHA-256 cmp | Search `scripts/` — no analog yet. Fallback: any existing Phase 28/29 two-run gate in xUnit (`Phase29ByteIdenticalTests.cs`) | no-analog |

## Pattern Assignments

### `flow-lang/Runtime/PrngRegistry.cs` (runtime state, key → Random)

**Analog 1 (PRNG seeding shape):** `flow-lang/StandardLibrary/Composition/VariationFunctions.cs` lines 71-86

```csharp
// Existing seeded vary() overloads use this shape: explicit Random(seed) per call site
private static Value VarySeeded(IReadOnlyList<Value> args)
{
    var seq = args[0].As<SequenceData>();
    double probability = args[1].As<double>();
    int seed = args[2].As<int>();
    return Value.Sequence(ApplyVariation(seq, probability, null, new Random(seed), null));
}

private static Value VaryRandom(IReadOnlyList<Value> args)
{
    var seq = args[0].As<SequenceData>();
    double probability = args[1].As<double>();
    return Value.Sequence(ApplyVariation(seq, probability, null, new Random(), null));
    //                                                          ^^^^^^^^^^^^^^^^
    //                                            NON-DETERMINISTIC — PrngRegistry migration replaces this
}
```

**Analog 2 (singleton-per-context with stack discipline):** `flow-lang/Runtime/MusicalContext.cs` lines 99-110 + `flow-lang/Runtime/ExecutionContext.cs` lines 434-451 (push/pop discipline)

```csharp
// MusicalContext.cs — TuningStack singleton with push/pop scoping
public Stack<RenderTuning> TuningStack { get; } = new Stack<RenderTuning>();
public RenderTuning ActiveTuning =>
    TuningStack.Count > 0 ? TuningStack.Peek() : RenderTuning.Default;

// ExecutionContext.cs — paired push/pop
public void PushTuning(RenderTuning renderTuning) { ... }
public void PopTuning()
{
    if (TuningStack.Count == 0)
        throw new InvalidOperationException(
            "PopTuning called with an empty TuningStack — push/pop must be balanced");
    ...
}
```

**Snapshot/Restore integration** (ExecutionContext.cs lines 519-554):

```csharp
public FlowLang.StandardLibrary.TestFramework.TestSnapshot SnapshotState()
{
    return new FlowLang.StandardLibrary.TestFramework.TestSnapshot
    {
        // 5. PRNG state — FixedRandSeed + FixedGen + Gen
        FixedRandSeed = FixedRandSeed,
        FixedGen = FixedGen,
        Gen = Gen,
        // ... PrngRegistry must be added here as a NEW captured surface (#12+)
    };
}
```

**Determinism guard pattern** (the existing two-run-cmp-clean contract — see this section's PatternFunctions analog):

```csharp
// Hash combining file path + line + col + name + salt — DO NOT use
// C# string.GetHashCode() (randomized per process). Use FNV-1a variant.
private static int ComputeDeterministicSeed(SourceLocation site, string name, int salt)
{
    unchecked
    {
        uint hash = 2166136261;
        hash = (hash ^ (uint)(site.FilePath?.GetDeterministicHash() ?? 0)) * 16777619;
        hash = (hash ^ (uint)site.Line) * 16777619;
        // ... etc per RESEARCH.md §Pattern 6
    }
}
```

---

### `flow-lang/Runtime/MarkovModelData.cs` + `LsystemModelData.cs` (reference-identity model)

**Analog:** `flow-lang/StandardLibrary/Audio/Tuning/ResolvedTuning.cs` (companion to TuningType.cs — wrapped via `Value.Tuning(resolved)`)

**Wrapping pattern** (Value.cs lines 60-72):

```csharp
public static Value Tuning(StandardLibrary.Audio.Tuning.ResolvedTuning resolved)
    => new(resolved, TuningType.Instance);

public static Value Sfz(StandardLibrary.Audio.Sfz.SfzData data)
    => new(data, SfzType.Instance);
```

**Data-class convention** (reference-identity = NO C# `record`, plain `class`, no structural equals):

```csharp
public class MarkovModelData    // NOT a record — reference identity
{
    public int Order { get; }
    public IReadOnlyDictionary<ImmutableArray<int>,
        IReadOnlyList<(int State, double Weight)>> Transitions { get; }
    public IReadOnlyList<int> StateAlphabet { get; }
    public string FeatureMode { get; }   // "pitch" | "pitch+duration"

    public MarkovModelData(int order, ..., string featureMode) { ... }
}
```

---

### `flow-lang/TypeSystem/SpecialTypes/MarkovModelType.cs` + `LsystemModelType.cs` (type singleton)

**Analog:** `flow-lang/TypeSystem/SpecialTypes/TuningType.cs` (full file — 28 lines, exactly the shape to copy)

```csharp
namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Phase 36 GEN-01 (D-36-06) — first-class reference-identity value type for
/// a trained Markov model. Returned by (markovTrain corpus order), consumed
/// by (markovGenerate model length [seed]). Specificity 148 — slotted above
/// Phase 33's SfzType (150) ... wait, see below.
/// </summary>
public sealed class MarkovModelType : FlowType
{
    private MarkovModelType() { }
    public static MarkovModelType Instance { get; } = new();
    public override string Name => "MarkovModel";
    public override int GetSpecificity() => 148;
    public override bool IsCompatibleWith(FlowType target) => target is MarkovModelType;
    public override bool CanConvertTo(FlowType target) => target is MarkovModelType;
}
```

**Specificity slot table** (from Phase 33 SfzType.cs xmldoc + Phase 32 TuningType comment):

- TuningType = 137
- SectionType = 138
- BeatType = 139
- SongType = 140
- HertzType = 144
- SfzType = 150
- **MarkovModelType = 148** (pick — between Hertz and Sfz)
- **LsystemModelType = 149** (pick — between Markov and Sfz)

Sfz's claim of "150 — above all" predates Phase 36; check `SfzType.cs:30` and re-confirm slot ordering at implementation time.

---

### `flow-lang/TypeSystem/FunctionSignature.cs` (extension — ParameterNames defaulted positional)

**Self-analog:** the existing record's `IsVarArgs = false` defaulted positional (FunctionSignature.cs:6-9)

```csharp
// EXISTING shape — extension below follows Phase 35 LANG-03 (FlowExpression.IntermediateName)
// convention: add a defaulted-positional parameter, no new record type.
public record FunctionSignature(
    string Name,
    IReadOnlyList<FlowType> InputTypes,
    bool IsVarArgs = false,
    IReadOnlyList<string>? ParameterNames = null)   // NEW — D-36-11
{
    // Existing Equals/GetHashCode must be extended to compare ParameterNames
    // when present (or ignore when null for backward compat with un-backfilled
    // registrations during the 36-12/36-13 sweep).
}
```

---

### `flow-lang/TypeSystem/OverloadResolver.cs` (extended for named-arg matching)

**Self-analog:** existing Resolve() at lines 22-83

```csharp
// Resolve signature with mixed positional + named args.
// Bind positional args to first N param slots in declaration order.
// Then bind named args to remaining slots by ParameterNames lookup.
// Reject if:
//   - named-arg refers to an already-bound positional slot
//   - named-arg name not in ParameterNames
//   - varargs sig + any named args (Open Question 2 per RESEARCH)
public FunctionSignature? Resolve(
    string functionName,
    IReadOnlyList<FunctionSignature> candidates,
    IReadOnlyList<FlowType> positionalArgTypes,
    IReadOnlyDictionary<string, FlowType>? namedArgTypes = null,   // NEW
    Core.SourceLocation? location = null)
{
    // Filter using existing Matches() — extended to also check named args
    // against ParameterNames slot positions.
    ...
}
```

**Existing specificity-scoring path** (lines 62-82, REUSED VERBATIM):

```csharp
// Multiple matches - rank by specificity
var rankedCandidates = matchingCandidates
    .Select(sig => new
    {
        Signature = sig,
        Specificity = sig.CalculateSpecificity(argTypes)
    })
    .OrderByDescending(x => x.Specificity)
    .ToList();

// Check for ambiguous overloads
if (rankedCandidates.Count > 1
    && rankedCandidates[0].Specificity == rankedCandidates[1].Specificity)
{
    _errorReporter.ReportError(
        $"Ambiguous overload for function '{functionName}' with argument types ...",
        location);
    return null;
}
```

---

### `flow-lang/Ast/Statements/SectionDeclaration.cs` (extension — Parameters + DefaultValues)

**Self-analog:** current shape (12 lines) + ProcDeclaration.cs Parameter list pattern

```csharp
// CURRENT (SectionDeclaration.cs — full file):
public record SectionDeclaration(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Statement> Body,
    Span? Span = null
) : Statement(Location);

// PHASE 36 EXTENSION (D-36-13..18) — defaulted-positional pattern per LANG-03:
public record SectionDeclaration(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Statement> Body,
    Span? Span = null,
    IReadOnlyList<Pattern>? Parameters = null,                // D-36-17 (full Phase 35 pattern AST)
    IReadOnlyList<Expression?>? DefaultValues = null          // D-36-15
) : Statement(Location);
```

**Pattern from ProcDeclaration.cs (for Parameter typed-param shape — REJECT for sections per D-36-17, which uses Phase 35 Patterns instead):**

```csharp
// ProcDeclaration.cs — illustrative ONLY; sections use Pattern, not Parameter:
public record Parameter(string Name, FlowType Type, bool IsVarArgs = false);
```

---

### `flow-lang/Ast/Expressions/FunctionCallExpression.cs` (extension — NamedArgs)

**Self-analog:** current 13-line record

```csharp
// CURRENT:
public record FunctionCallExpression(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Expression> Arguments,
    Span? Span = null) : Expression(Location);

// PHASE 36 D-36-11 EXTENSION (defaulted-positional per LANG-03 convention):
public record FunctionCallExpression(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Expression> Arguments,
    Span? Span = null,
    IReadOnlyDictionary<string, Expression>? NamedArgs = null) : Expression(Location);
```

---

### `flow-lang/Lexing/SimpleLexer.cs` (modify TryLexSignedNumber expression-start set)

**Self-analog:** existing `TryLexSignedNumber` at lines 455-475

```csharp
// Existing expression-start set (post Phase 35 sweep). Phase 36 adds TokenType.Assign
// per RESEARCH Open Question 4 — required so `(fn arg=-5)` lexes `-5` as one IntLiteral.
private Token? TryLexSignedNumber(SourceLocation start)
{
    // ... last-emitted-type predicate ...
    if (_lastEmittedType is
        TokenType.LParen or TokenType.LBracket or TokenType.Arrow
        or TokenType.Assign     // already present
        or TokenType.Pipe
        // PHASE 36: TokenType.Assign already in set — verify; if NOT, add it here.
        // Plus add: Match/When were Phase 35; nothing new for Phase 36 named-args
        // (Assign was already covered).
        )
    { ... }
}
```

NOTE: looking at lines 425 / 468, `TokenType.Assign` is **already in both expression-start sets** — Phase 35 LANG-03 likely added it for `Int x = -5;`. Verify at implementation time; if present, NO lexer change needed for D-36-11.

---

### `flow-lang/Parsing/Parser.cs` lines 484-508 `ParseSectionDeclaration` (modify per D-36-13..18)

**Analog 1 (typed-param parsing):** Parser.cs lines 270-293 `ParseProcDeclaration` parameter-list loop

```csharp
// EXISTING proc-param parsing shape (lines 270-293) — adapt for sections:
var parameters = new List<Parameter>();
Expect(TokenType.LParen, "Expected '(' after procedure name");

while (!Check(TokenType.RParen) && !IsAtEnd())
{
    var (paramType, nextIndex, isVarArgs) = TypeParser.ParseType(_tokens, _current);
    _current = nextIndex;

    if (Check(TokenType.Ellipsis))
    {
        Advance();
        isVarArgs = true;
    }

    Expect(TokenType.Colon, "Expected ':' after parameter type");
    var paramName = ExpectParameterName().Text;

    parameters.Add(new Parameter(paramName, paramType, isVarArgs));

    if (!Check(TokenType.RParen))
        Expect(TokenType.Comma, "Expected ',' between parameters");
}

Expect(TokenType.RParen, "Expected ')' after parameters");
```

**Analog 2 (pattern parsing — REQUIRED for D-36-17):** Phase 35 Plan 35-05's `ParsePattern` (search `Parser.cs` for `ParsePattern` private method — locate before mod). The section param parser body becomes:

```csharp
// PHASE 36 D-36-13..18 — section param parser:
// section name(Pattern, Pattern, ...) { body }
// where each Pattern can be:
//   - LiteralPattern         (e.g. Cmaj7, C4)
//   - BindingPattern         (e.g. Note root  — type annotation + binding name)
//   - ConstructorPattern     (e.g. <<Note root, Int repeats>> — tuple destructure)
//   - GuardPattern           (e.g. Chord c when (= c.Quality "maj7"))
// Optional `= DefaultExpression` per D-36-15.
if (Match(TokenType.LParen))
{
    var parameters = new List<Pattern>();
    var defaultValues = new List<Expression?>();
    while (!Check(TokenType.RParen) && !IsAtEnd())
    {
        var pattern = ParsePattern();   // Phase 35 entry point — REUSE
        Expression? defaultExpr = null;
        if (Match(TokenType.Assign))
            defaultExpr = ParseExpression();
        parameters.Add(pattern);
        defaultValues.Add(defaultExpr);
        if (!Check(TokenType.RParen))
            Expect(TokenType.Comma, "Expected ',' between section parameters");
    }
    Expect(TokenType.RParen, "Expected ')' after section parameters");
}
```

---

### `flow-lang/StandardLibrary/Patterns/PatternFunctions.cs` (NEW — 13 combinators)

**Analog:** `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` lines 50-73 (legato — Sequence in, Sequence out)

```csharp
// Direct copy-and-adapt shape:
public static class PatternFunctions
{
    public static void Register(InternalFunctionRegistry registry)
    {
        RegisterEvery(registry);
        RegisterFast(registry);
        // ... etc
    }

    private static void RegisterEvery(InternalFunctionRegistry registry)
    {
        var everySig = new FunctionSignature("every",
            [IntType.Instance, FunctionType.Instance, SequenceType.Instance],
            ParameterNames: ["n", "fn", "seq"]);    // D-36-11 backfill
        registry.Register("every", everySig, args =>
        {
            int n = args[0].As<int>();
            var fn = args[1].As<FunctionOverload>();   // see DictFunctions invoke pattern
            var seq = args[2].As<SequenceData>();

            // PAT-02 charitable interpretation (Pitfall 2)
            if (n <= 0 || seq.Bars.Count == 0)
            {
                RenderingDiagnostics.WarnOnce(
                    $"every:invalid:{/* srcLoc */}",
                    "every: cycle count must be positive AND sequence non-empty; unchanged");
                return Value.Sequence(seq);
            }
            // ... transform ...
            return Value.Sequence(newSeq);
        });
    }
}
```

**Lambda invocation idiom** — copy from `flow-lang/StandardLibrary/Collections/DictFunctions.cs` lines 41-46:

```csharp
private static Value InvokeCallback(
    FlowLang.Runtime.ExecutionContext context, FunctionOverload cb, List<Value> args)
{
    return cb.IsInternal
        ? cb.Implementation!(args)
        : context.Invoker!.ExecuteUserFunctionWithCaptures(
            cb.Declaration!, args, cb.CapturedVariables);
}
```

NOTE: For PatternFunctions to invoke lambdas, the Register entry needs `ExecutionContext` access — use the RegisterContextDependent pattern from `HarmonyFunctions.cs:23-27`:

```csharp
public static void RegisterContextDependent(
    InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
{
    var sig = new FunctionSignature("every", [...], ParameterNames: [...]);
    registry.Register("every", sig, args => Every(args, context));
}
```

---

### `flow-lang/StandardLibrary/Generative/MarkovFunctions.cs` (NEW — markov / markovTrain / markovGenerate)

**Analog 1 (PRNG threading):** `VariationFunctions.cs` lines 71-86 (seeded vs unseeded overload split)

**Analog 2 (registration of a new Value type):** `flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs` lines 36-50

```csharp
// ScalaBuiltins.cs:36-50 — exact template for "loads → returns reference-id Value":
public static void Register(InternalFunctionRegistry registry)
{
    var sigOne = new FunctionSignature("loadScala", [StringType.Instance]);
    registry.Register("loadScala", sigOne, LoadScalaOneArg);

    var sigTwo = new FunctionSignature("loadScala",
        [StringType.Instance, StringType.Instance]);
    registry.Register("loadScala", sigTwo, LoadScalaTwoArg);

    // (str Tuning) → String per CONTEXT D-04
    var sigStrTuning = new FunctionSignature("str", [TuningType.Instance]);
    registry.Register("str", sigStrTuning, StrTuning);
}

private static Value LoadScalaOneArg(System.Collections.Generic.IReadOnlyList<Value> args)
{
    string sclPath = args[0].As<string>();
    var resolved = /* build ResolvedTuning */;
    return Value.Tuning(resolved);
}
```

**Mapped to Markov:**

```csharp
public static void Register(InternalFunctionRegistry registry,
                            FlowLang.Runtime.ExecutionContext context)
{
    // markovTrain(Sequence, Int) → MarkovModel
    var trainSig = new FunctionSignature("markovTrain",
        [SequenceType.Instance, IntType.Instance],
        ParameterNames: ["corpus", "order"]);
    registry.Register("markovTrain", trainSig, args =>
    {
        var corpus = args[0].As<SequenceData>();
        int order = Math.Clamp(args[1].As<int>(), 1, 3);
        var model = TrainMarkov(corpus, order, featureMode: "pitch");
        return Value.MarkovModel(model);
    });

    // markovGenerate(MarkovModel, Int, Int) → Sequence  (explicit seed)
    var genSeededSig = new FunctionSignature("markovGenerate",
        [MarkovModelType.Instance, IntType.Instance, IntType.Instance],
        ParameterNames: ["model", "length", "seed"]);
    registry.Register("markovGenerate", genSeededSig, args =>
    {
        var model = args[0].As<MarkovModelData>();
        int length = args[1].As<int>();
        int seed = args[2].As<int>();
        return Value.Sequence(GenerateMarkov(model, length, new Random(seed)));
    });

    // markovGenerate(MarkovModel, Int) → Sequence  (PrngRegistry seed)
    var genUnseededSig = new FunctionSignature("markovGenerate",
        [MarkovModelType.Instance, IntType.Instance],
        ParameterNames: ["model", "length"]);
    registry.Register("markovGenerate", genUnseededSig, args =>
    {
        var model = args[0].As<MarkovModelData>();
        int length = args[1].As<int>();
        var rng = context.PrngRegistry.GetRandom(/* callSite */, "markovGenerate");
        return Value.Sequence(GenerateMarkov(model, length, rng));
    });
}
```

---

### `flow-lang/StandardLibrary/Improv/JamFunctions.cs` (NEW — chord-aware Markov)

**Analog (context + PRNG composition):** `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` lines 23-66 (RegisterContextDependent + Enharmonic reading MusicalContext)

```csharp
// HarmonyFunctions.cs:23-66 — pattern for "builtin that reads MusicalContext":
public static void RegisterContextDependent(
    InternalFunctionRegistry registry,
    FlowLang.Runtime.ExecutionContext context)
{
    var enharmonicSig = new FunctionSignature("enharmonic", [NoteType.Instance]);
    registry.Register("enharmonic", enharmonicSig, args => Enharmonic(args, context));
}

private static Value Enharmonic(IReadOnlyList<Value> args,
                                 FlowLang.Runtime.ExecutionContext context)
{
    string noteStr = args[0].As<string>();
    var musicalCtx = context.GetMusicalContext();
    string? key = musicalCtx?.Key;
    // ... read active musical state ...
    // ... emit charitable-advisory WarnOnce when surprising context detected ...
    RenderingDiagnostics.WarnOnce("enharmonic-non-equal-temperament", "...");
    // ...
}
```

**Key push/pop pattern for jam's `key=` override** — adapt from existing `PushTuning`/`PopTuning` in ExecutionContext.cs:434-451:

```csharp
// jam key= override mechanism — adapt MusicalContext push/pop:
context.PushFrame();
var newCtx = new MusicalContext { Key = keyOverride };
context.CurrentFrame.MusicalContext = newCtx;
try
{
    // ... generate ...
}
finally
{
    context.PopFrame();
}
```

---

### `flow-lang/StandardLibrary/Improv/StyleRegistry.cs` (NEW — Flow-file pack discovery)

**Analog:** Phase 30 `FlowConfig` XDG-config loading (`flow-lang/Runtime/FlowConfig.cs`) + Phase 33 SFZ patch registry (locate via `find flow-lang -name "SfzPatchRegistry*" -o -name "SfzRoot*"`)

```csharp
// Pattern for "scan a directory for .flow files at init":
public static class StyleRegistry
{
    private static readonly Dictionary<string, DictData> _styles = new(StringComparer.Ordinal);

    public static void LoadAtEngineInit(FlowEngine engine)
    {
        // 1. Shipped packs first (load order locked per Pitfall 8)
        LoadDir(engine, Path.Combine(engine.StdLibRoot, "improv", "styles"));
        // 2. User packs second (override semantics)
        LoadDir(engine, Path.Combine(
            Environment.GetFolderPath(SpecialFolder.UserProfile),
            ".config", "flow", "styles"));
    }

    private static void LoadDir(FlowEngine engine, string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.flow"))
        {
            engine.Execute(File.ReadAllText(file));   // executes (registerStyle ...) calls
        }
    }

    public static void Register(Value symbolKey, DictData pack)
    {
        // ... overwrite-on-collision with WarnOnce advisory ...
    }
}
```

---

### `flow-lang/patterns.flow` (NEW — internal proc declarations for the 13 combinators)

**Analog:** `flow-lang/std.flow` lines 135-150 (existing Transforms forward-decls)

```text
Note: Phase 36 PAT-01 — 13 Tidal-style combinators on Sequence
Note: All take a (lambda: fn) per D-36-03; varargs none

Note: Cycle-dependent combinators (cycle unit = bars per D-36-04)
internal proc every (Int: n, Function: fn, Sequence: seq)
internal proc chunk (Int: n, Function: fn, Sequence: seq)
internal proc phase (Double: offset, Sequence: seq)

Note: Time-stretch combinators
internal proc fast (Sequence: seq, Double: factor)
internal proc slow (Sequence: seq, Double: factor)

Note: Pitch / order combinators
internal proc rev (Sequence: seq)
internal proc iter (Int: n, Sequence: seq)
internal proc palindrome (Sequence: seq)

Note: Probabilistic combinators (route through PrngRegistry)
internal proc sometimes (Double: prob, Function: fn, Sequence: seq)
internal proc sometimes (Function: fn, Sequence: seq)
internal proc degrade (Sequence: seq)
internal proc sparseSeq (Double: prob, Sequence: seq)

Note: Multi-voice combinators
internal proc jux (Function: fn, Sequence: seq)
internal proc superimpose (Function: fn, Sequence: seq)
```

---

### `flow-lang/improv/styles/jazz.flow` (NEW — Flow-data rule pack)

**Analog:** RESEARCH §Pattern 8 sample contract + `flow-lang/audio.flow` top-level structure

```text
Note: Phase 36 IMPROV-01 — baseline jazz rule pack
Note: Registered via (registerStyle #jazz dict) at FlowEngine init
Note: See flow-lang/improv/styles/README.md for the Dict shape contract

use "@improv"

Dict<Symbol, Value> jazzPack = (dict
  #beat_weights (dict
    #strong (dict #chord_tone 0.75 #scale_tone 0.20 #chromatic_passing 0.05)
    #weak   (dict #chord_tone 0.30 #scale_tone 0.50 #chromatic_passing 0.20))
  #interval_transitions (dict
    #step_up 0.30 #step_down 0.30
    #leap_up 0.10 #leap_down 0.15
    #chromatic 0.10 #repeat 0.05)
  #rhythmic_template <<e e e e e e e e>>
  #articulation_distribution (dict
    #downbeat #legato
    #offbeat  #accent
    #syncopated #marcato))

(registerStyle #jazz jazzPack)
```

---

### `examples/generative/markov_jazz.flow` + `tidal_combinators.flow` (NEW — tutorials)

**Analog:** `examples/symphony/symphony.flow` lines 1-50 (header comment + `use` + `Sfz` bindings + voicePool/tempo/key block + `section`-based composition)

```flow
// =====================================================================
// Markov Jazz -- v1.5 generative showcase (Phase 36)
// =====================================================================
// Demonstrates one-shot markov + markovTrain/markovGenerate split +
// jam with style override + Phase 35 `as` chain naming.
//
// Render with:
//   flow render examples/generative/markov_jazz.flow

use "@std"
use "@audio"
use "@patterns"
use "@generative"
use "@improv"

voicePool 32 {
    tempo 120 {
        key Cmajor {
            // Train once from a corpus, generate many
            Sequence corpus = | C4 D4 E4 F4 G4 F4 E4 D4 C4 |
            MarkovModel m = (markovTrain corpus 2)
            Sequence riff1 = (markovGenerate m 16 42)
            Sequence riff2 = (markovGenerate m 16 99)

            // Chord-aware improvisation
            Sequence chords = | Cmaj7 Am7 Dm7 G7 |
            Sequence solo = (jam over=chords style=#jazz length=8 seed=1234)

            // ... section + writeWav ...
        }
    }
}
```

---

### `tests/test_patterns_every.flow` + `tests/test_markov_*.flow` etc. (composer tests)

**Analog:** `tests/test_generative.flow` (full file, 38 lines) — idiomatic shape with `use "@std"`, musical context block, `(? ...)` random choice + `(?? ...)` seeded random + `euclidean` exercise pattern

```flow
use "@std"
use "@test"
use "@patterns"

(test "every applies fn to bars 0, N, 2N"
  lazy(
    Sequence s = | C4q D4q E4q F4q | G4q A4q B4q C5q | D5q E5q F5q G5q |
    Sequence varied = (every 2 (fn s => (fast s 2)) s)
    // ... assertion via captured variable ...
  ))

(test "every charitably ignores n=0"
  lazy(
    Sequence s = | C4q D4q |
    Sequence unchanged = (every 0 (fn x => (rev x)) s)
    (assertEq s unchanged)
  ))
```

---

### `flow-lang.Tests/Phase36/PatternEveryTests.cs` (xUnit unit tests)

**Analog:** `flow-lang.Tests/Phase35/MatchRuntimeTests.cs` lines 1-50 (full file pattern — engine.ExecuteScriptAndGetResult helper + per-Fact source string)

```csharp
using FlowLang.Core;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-03 — PAT-01 every combinator.
///
/// Drives FlowEngine on small (every n fn seq) sources and inspects the
/// resulting last-expression Value. Pins:
///   1. Cycle unit is bars (D-36-04).
///   2. Charitable interpretation on n=0 (D-36-02 / PAT-02).
///   3. Charitable interpretation on empty sequence.
///   4. Lambda receives the bar-as-subsequence shape.
/// </summary>
public class PatternEveryTests
{
    private static Value? Eval(string source)
    {
        using var engine = new FlowEngine(verbose: false);
        return engine.ExecuteScriptAndGetResult(source);
    }

    [Fact]
    public void EveryAppliesFnAtCycleBoundary()
    {
        var v = Eval(@"
            use ""@patterns""
            (every 2 (fn s => (fast s 2)) | C4q D4q | E4q F4q | G4q A4q |)
        ");
        Assert.NotNull(v);
        // ...
    }

    [Fact]
    public void EveryCharitablyIgnoresZeroN()
    {
        // ...
    }
}
```

---

### `flow-lang.Tests/Phase36/ParameterNamesCoverageTest.cs` (audit / grep-style)

**Analog:** `flow-lang.Tests/Integration/Phase29/LicenseAuditTests.cs` (full file pattern — file-system scanning + assert-each)

```csharp
using System.IO;
using FlowLang.Tests;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 D-36-11 backfill completeness gate. Every registry.Register call site
/// in BuiltInFunctions.cs + Audio + Harmony + Transforms + Composition must declare
/// ParameterNames so named-arg syntax works universally.
///
/// Mirrors the LicenseAuditTests pattern: source-grep + assert-each.
/// </summary>
public class ParameterNamesCoverageTest
{
    [Theory]
    [InlineData("BuiltInFunctions.cs")]
    [InlineData("Audio/EffectsFunctions.cs")]
    [InlineData("Harmony/HarmonyFunctions.cs")]
    [InlineData("Transforms/TransformFunctions.cs")]
    public void EveryRegisterCallSite_HasParameterNames(string relativePath)
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string path = Path.Combine(repoRoot, "flow-lang", "StandardLibrary", relativePath);
        string contents = File.ReadAllText(path);

        int registerCount = CountSubstring(contents, "registry.Register(");
        int paramNamesCount = CountSubstring(contents, "ParameterNames:");

        Assert.True(registerCount == paramNamesCount,
            $"Backfill incomplete in {relativePath}: " +
            $"{registerCount} Register calls but {paramNamesCount} ParameterNames");
    }
}
```

---

## Shared Patterns

### Charitable Interpretation Advisory

**Source:** `flow-lang/Diagnostics/RenderingDiagnostics.cs` lines 19-36 (full class)
**Apply to:** All PAT-01..PAT-02 combinators, GEN-01..GEN-05 builtins, IMPROV-01 jam, all section overload resolution diagnostics

```csharp
public static class RenderingDiagnostics
{
    private static readonly HashSet<string> _emitted = new(StringComparer.Ordinal);
    private static readonly object _lock = new();

    public static void WarnOnce(string sentinelKey, string message)
    {
        lock (_lock)
        {
            if (!_emitted.Add(sentinelKey)) return;
        }
        Console.Error.WriteLine(message);
    }

    // Called by test [Collection] setup
    public static void ResetForTesting() { ... }
}
```

**Sentinel-key convention** (from Phase 32/33 callers — `enharmonic-non-equal-temperament`, `MIDI-non-equal-tempo`, etc.):
- Format: `f"{transform-name}:{condition}:{srcLoc}"` per the per-Span sentinel idiom
- Always tests must call `ResetForTesting()` in their `[Collection]` setup (see HermeticIsolationTests.cs Phase 35)

---

### MusicalContext push/pop for synthetic-frame param binding

**Source:** `flow-lang/Runtime/ExecutionContext.cs` lines 434-451 (PushTuning / PopTuning) + lines 194-214 (PushFrame / PopFrame)
**Apply to:** Section call (D-36-13), `jam` `key=` override (D-36-10)

```csharp
// Pattern (used by tuning t { ... }, voicePool 32 { ... }, key Cmajor { ... }):
context.PushFrame();
try
{
    context.CurrentFrame.MusicalContext = new MusicalContext { Key = override };
    // ... evaluate body in this frame ...
}
finally
{
    context.PopFrame();    // exception-safe pop
}
```

Pairs with the existing `GetMusicalContext` walker at ExecutionContext.cs:282 (walks frame stack top-to-bottom, first non-null property wins).

---

### Lambda invocation (FunctionOverload dispatch)

**Source:** `flow-lang/StandardLibrary/Collections/DictFunctions.cs` lines 41-46 + lines 28-37
**Apply to:** Every combinator that takes a `Function`-typed parameter (`every`, `chunk`, `sometimes`, `jux`, `superimpose`, `lsystemToSequence`)

```csharp
private static Value InvokeCallback(
    FlowLang.Runtime.ExecutionContext context,
    FunctionOverload cb,
    List<Value> args)
{
    return cb.IsInternal
        ? cb.Implementation!(args)
        : context.Invoker!.ExecuteUserFunctionWithCaptures(
            cb.Declaration!, args, cb.CapturedVariables);
}
```

Requires the registration to use `RegisterContextDependent(registry, context)` shape (not the plain `Register(registry)` shape) — see HarmonyFunctions.cs:23-27.

---

### Reference-identity Value wrapper

**Source:** `flow-lang/Runtime/Value.cs` lines 60-72 (Tuning + Sfz factory methods)
**Apply to:** `MarkovModelData` and `LsystemModelData` wrapping

```csharp
public static Value Tuning(StandardLibrary.Audio.Tuning.ResolvedTuning resolved)
    => new(resolved, TuningType.Instance);

public static Value Sfz(StandardLibrary.Audio.Sfz.SfzData data)
    => new(data, SfzType.Instance);

// Phase 36 ADDITIONS:
public static Value MarkovModel(Runtime.MarkovModelData model)
    => new(model, MarkovModelType.Instance);

public static Value LsystemModel(Runtime.LsystemModelData model)
    => new(model, LsystemModelType.Instance);
```

---

### Backwards-compatible registration with ParameterNames

**Source:** Phase 35 LANG-03 defaulted-positional sweep convention (FlowExpression.IntermediateName) + extension method on FunctionSignature
**Apply to:** Every Phase 36 builtin registration AND the ~211+ existing registrations during the 36-12/36-13 backfill waves

```csharp
// BEFORE (existing pattern across 211+ sites in BuiltInFunctions.cs):
var sig = new FunctionSignature("transpose",
    [SequenceType.Instance, SemitoneType.Instance]);

// AFTER (D-36-11 — defaulted-positional, backward-compat preserved):
var sig = new FunctionSignature("transpose",
    [SequenceType.Instance, SemitoneType.Instance],
    ParameterNames: ["seq", "amount"]);
```

The named-arg resolver short-circuits to the existing positional path when `ParameterNames == null`, so partially-backfilled registries remain functional during the sweep waves (Pitfall 5 mitigation).

---

## No Analog Found

| File | Role | Data Flow | Reason | Planner Fallback |
|------|------|-----------|--------|------------------|
| `scripts/test_two_run_determinism.sh` | bash script | render twice + cmp | No existing bash determinism harness; Phase 28/29 byte-identical tests are xUnit, not shell | Use xUnit `Phase29ByteIdenticalTests.cs` as conceptual reference; build minimal bash wrapper: `flow render … -o /tmp/run1.wav && flow render … -o /tmp/run2.wav && sha256sum /tmp/run1.wav /tmp/run2.wav \| sort -u \| wc -l` (must be 1) |
| `flow-lang/improv/styles/{jazz,blues,classical}.flow` rule packs | Flow data files | top-level `(registerStyle ...)` call | No existing top-level Flow file is purely data — all are `internal proc` decls. Closest shape is `flow-lang/std.flow`, but it's a forward-declaration file, not a data-construction file | Planner should use RESEARCH §Pattern 8 sample contract verbatim. The pack body is `Dict<Symbol, Value> pack = (dict ...)` followed by `(registerStyle #name pack)` — no analog needed; the Dict + Symbol + registerStyle surface IS the analog |
| `flow-lang.Tests/Phase36/MarkovModelTests.cs` (model identity vs structural eq) | unit test | reference-eq + (markovEqual) | Phase 32 Tuning + Phase 33 Sfz tests likely exist; locate via `find flow-lang.Tests -name "*TuningEquality*" -o -name "*SfzEquality*"` | Adapt MatchRuntimeTests.cs shape with two-trained-models comparison: `(eq m1 m2)` → false, `(markovEqual m1 m2)` → true |

---

## Metadata

**Analog search scope:**
- `/home/noah/Desktop/projects/flow-sharp/flow-lang/` (all subdirectories)
- `/home/noah/Desktop/projects/flow-sharp/flow-lang.Tests/Phase35/`, `/Integration/Phase29/`, `/Integration/Phase33/`
- `/home/noah/Desktop/projects/flow-sharp/tests/` (filtered to test_generative.flow + test_pattern*.flow)
- `/home/noah/Desktop/projects/flow-sharp/examples/symphony/`

**Files scanned:** 31 files read in full or part (TransformFunctions, VariationFunctions, ScalaBuiltins, HarmonyFunctions, MusicalContext, ExecutionContext, OverloadResolver, FunctionSignature, TuningType, SfzType, SectionDeclaration, ProcDeclaration, FunctionCallExpression, SongExpression, Pattern, RenderingDiagnostics, SimpleLexer, Parser, BuiltInFunctions, InternalFunctionRegistry, std.flow, audio.flow, DictFunctions, Collections, Value, symphony.flow, test_generative.flow, test_pattern_match_music.flow, MatchRuntimeTests, MatchParserTests, LicenseAuditTests)

**Pattern extraction date:** 2026-05-20

**Coverage confidence:**
- Exact analog (12 categories): all combinators, MarkovModel/LsystemModel types, ScalaBuiltins shape for Markov registration, AST field extensions, composer-facing tests, xUnit tests, examples — HIGH confidence
- Composite analog (3 categories): PrngRegistry (PRNG + stack), JamFunctions (PRNG + context + key push/pop), tidal_combinators example (symphony + test_generative) — HIGH confidence (composition is mechanical)
- Partial analog (2 categories): rule-pack Flow files, ParameterNamesCoverageTest — MEDIUM confidence (shape is clear, but no exact precedent)
- No analog (1 category): bash determinism script — planner builds minimal wrapper from xUnit reference

**Cross-cutting patterns ready for plan-checker:**
1. **`RegisterContextDependent(registry, context)`** is the only registration form that supports lambda invocation AND MusicalContext reads — every Phase 36 PRNG-bearing or context-bearing builtin uses this form, NOT the bare `Register(registry)` form.
2. **`RenderingDiagnostics.WarnOnce`** is the only correct charitable-advisory channel — never throw, never `Console.WriteLine` directly, never use `_errorReporter.ReportError` for charitable-fall-through paths.
3. **Reference-identity value types** (`MarkovModelType`, `LsystemModelType`) are sealed singletons with `Instance` getter — NEVER C# records, NEVER structural equality. Pair with explicit `markovEqual`/`lsystemEqual` builtins for content compare.
4. **Defaulted-positional record extension** (Phase 35 LANG-03 convention) is the ONLY allowed AST shape addition — never new record types for FunctionCallExpression.NamedArgs / SectionDeclaration.Parameters / FunctionSignature.ParameterNames.
5. **Synthetic-frame push/pop discipline** for `section verse(args)` body, `jam key="..."` override, and any future PRNG-boundary work uses `PushFrame()`/`PopFrame()` with try/finally — same as Phase 28 voicePool, Phase 32 tuning, Phase 33 sfz dispatch.
