# Phase 35: Language Foundation - Pattern Map

**Mapped:** 2026-05-18
**Files analyzed:** ~70 new/modified across 5 buckets
**Analogs found:** 70/70 (every file has an in-codebase precedent)

This map is consumed by `gsd-planner`. Each entry names a concrete analog file + line range; planners reference these directly in plan action sections rather than re-discovering them.

---

## File Classification

### Bucket 1 - Span migration foundation (Wave 1)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/Core/Span.cs` | core record (new) | data-only | `flow-lang/Core/SourceLocation.cs` | exact |
| `flow-lang/Lexing/Token.cs` | lexing record (modify) | data-only | self - Phase 21 `OriginalText` defaulted-param precedent | exact |
| `flow-lang/Lexing/SimpleLexer.cs` | lexer (modify) | transform | self - Phase 21 propagation of `OriginalText` through `new Token(...)` call sites | exact |
| `flow-lang/Parsing/Parser.cs` | parser (modify) | transform | self - existing AST construction sites already pass `Location` positionally | exact |
| `flow-lang/Ast/Expressions/*.cs` (16 files) | AST record (modify) | data-only | `flow-lang/Ast/Expressions/FunctionCallExpression.cs` | exact |
| `flow-lang/Ast/Statements/*.cs` (14 files) | AST record (modify) | data-only | `flow-lang/Ast/Statements/VariableDeclaration.cs` | exact |

**Sequencing note:** Bucket 1 is Wave 1, runs FIRST in the phase. Every subsequent bucket consumes `Span` on AST nodes. HK-01..04 (Bucket 5) is parallel-safe with Wave 1 because it doesn't touch the AST / Token records.

### Bucket 2a - Diagnostics renderer (Wave 2a, LANG-04)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/Diagnostics/FlowDiagnostic.cs` | diagnostic record (new) | data-only | `flow-lang/Diagnostics/FlowError.cs` | exact |
| `flow-lang/Diagnostics/DiagnosticRenderer.cs` (SnippetRenderer per RESEARCH) | renderer/formatter (new) | request-response | `flow-lang/Diagnostics/FlowError.cs:23-34` (ToString single-line) + TTY emit pattern in `flow-interpreter/Program.cs:77` | role-match |
| `flow-lang/Diagnostics/LevenshteinHelper.cs` | utility (new, EXTRACTED) | pure-function | `flow-lang/Lexing/PragmaRegistry.cs:42-84` | exact (extract-and-promote) |
| `flow-lang/Core/SourceMap.cs` | source-text registry (new) | CRUD/lookup | `flow-lang/Runtime/ExecutionContext.cs:85` (Dictionary-keyed-per-process registry pattern) | role-match |
| `flow-lang/Diagnostics/ErrorReporter.cs` | error sink (modify) | event-driven sink | self - existing `Report(FlowError)` API | exact (additive overload) |
| `flow-lang/Interpreter/ExpressionEvaluator.cs` | runtime error throw site (modify) | request-response | self - existing `_errorReporter.ReportError(msg, loc)` sites | exact |
| `flow-lang/Parsing/Parser.cs` | parse-error emit site (modify) | request-response | self - existing `_errorReporter` use | exact |

**Sequencing note:** Wave 2a. Runs parallel with Wave 2b (test framework) after Wave 1 (span) lands. Both consume `Span` but do not block each other.

### Bucket 2b - Pure-Flow test framework (Wave 2b, TEST-01 + TEST-02)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/StandardLibrary/TestFramework/TestFunctions.cs` | builtin registration (new) | event-driven | `flow-lang/StandardLibrary/BuiltInFunctions.cs:333-347` (Lazy `eval` + `if` registration) | exact |
| `flow-lang/StandardLibrary/TestFramework/TestRunner.cs` | test orchestrator (new) | batch | new shape - closest: REPL `Run()` loop iterating user input is shape-analog | role-match (partial) |
| `flow-lang/StandardLibrary/TestFramework/AssertionHelpers.cs` | assertion + comparison (new) | pure-function | `flow-lang.Tests/Helpers/RmsRegressionTests.cs:38-62` (RMS comparison) | role-match |
| `flow-lang.Tests/Helpers/RmsComparator.cs` | pure RMS comparison (new, EXTRACTED) | pure-function | `flow-lang.Tests/Helpers/RmsRegressionTests.cs:71+` (currently xUnit-coupled) | exact (extract-and-decouple) |
| `flow-cli/Commands/TestCommand.cs` | CLI subcommand (new) | request-response | `flow-cli/Commands/CheckCommand.cs` (full file) | exact |
| `flow-cli/Commands/CommandRegistry.cs` | dispatch (modify) | event-driven | self - existing `BuildAllCommands()` list at lines 14-31 | exact |
| `flow-lang/Core/FlowEngine.cs` | engine surface (modify) | data-only | self - existing `Execute(source, file)` entrypoint | role-match (additive: SnapshotState/RestoreState) |
| `flow-lang/Runtime/ExecutionContext.cs` | state owner (modify, +Snapshot/Restore +TestRegistry) | data-only | `flow-lang/Runtime/MusicalContext.cs` push/pop scoped state (TuningStack: `MusicalContext.cs:99`) | role-match |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` | registration entry (modify) | event-driven | self - existing `Register...` partial-class entry pattern | exact |
| `flow-lang/test.flow` | stdlib module file (new) | data-only | `flow-lang/std.flow` (re-export shell) | exact |
| `tests/test_test_framework.flow` | meta-test (new, composer-facing) | data-only | existing `tests/test_humanize.flow` / any `tests/test_*.flow` | exact |

**Sequencing note:** Wave 2b. Parallel with Wave 2a. Both depend on Wave 1 (Span on AST so test-failure diagnostics carry source location). Pattern matching (Wave 3) depends on this wave because match tests are authored in the test framework.

### Bucket 3 - Pattern matching (Wave 3, LANG-01 + LANG-02)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/Ast/Patterns/Pattern.cs` | base AST record (new) | data-only | `flow-lang/Ast/AstNode.cs` | exact |
| `flow-lang/Ast/Patterns/LiteralPattern.cs` | leaf pattern (new) | data-only | `flow-lang/Ast/Expressions/LiteralExpression.cs` | exact |
| `flow-lang/Ast/Patterns/WildcardPattern.cs` | leaf pattern (new) | data-only | `flow-lang/Ast/Expressions/SymbolLiteralExpression.cs` (single-string-payload record shape) | exact |
| `flow-lang/Ast/Patterns/BindingPattern.cs` | leaf pattern (new) | data-only | `flow-lang/Ast/Expressions/VariableExpression.cs` (name-only payload) | exact |
| `flow-lang/Ast/Patterns/ConstructorPattern.cs` | composite pattern (new) | data-only | `flow-lang/Ast/Expressions/FunctionCallExpression.cs` (name + arg list) | exact |
| `flow-lang/Ast/Patterns/GuardPattern.cs` | wrapper pattern (new) | data-only | `flow-lang/Ast/Expressions/LazyExpression.cs` (wraps inner expr) | role-match |
| `flow-lang/Ast/Patterns/ChordQualityPattern.cs` (or fold into ConstructorPattern flag) | music-aware pattern (new) | data-only | `flow-lang/Ast/Expressions/ChordLiteralExpression.cs` | exact |
| `flow-lang/Ast/Patterns/RomanNumeralPattern.cs` (or fold) | music-aware pattern (new) | data-only | `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` (resolveNumeral consumer) | role-match |
| `flow-lang/Ast/Patterns/ArticulationSymbolPattern.cs` (or fold) | music-aware pattern (new) | data-only | `flow-lang/Ast/Expressions/SymbolLiteralExpression.cs` | exact |
| `flow-lang/Ast/Patterns/MatchArm.cs` | record pairing pattern+body (new) | data-only | `flow-lang/Ast/Expressions/LambdaExpression.cs` (LambdaParameter record at line 10) | exact |
| `flow-lang/Ast/Expressions/MatchExpression.cs` | match scrutinee + arms (new) | data-only | `flow-lang/Ast/Expressions/FlowExpression.cs` | exact |
| `flow-lang/Interpreter/PatternMatcher.cs` | runtime dispatch (new) | request-response | `flow-lang/Interpreter/ExpressionEvaluator.cs:325-346` (EvaluateFlowExpression switch shape) | role-match |
| `flow-lang/Lexing/SimpleLexer.cs` | tokenize `match`/`_`/`when` (modify) | transform | self - keyword table at `SimpleLexer.cs:850-891` | exact |
| `flow-lang/Lexing/TokenType.cs` | new token types (modify) | data-only | self - existing enum entries | exact |
| `flow-lang/Parsing/Parser.cs` | parse `(match ...)` (modify) | transform | self - `ParseFlowExpression` at `Parser.cs:850-913` + `(func args)` primary form at `Parser.cs:1089-1115` | role-match |
| `flow-lang/Interpreter/ExpressionEvaluator.cs` | dispatch (modify) | event-driven | self - existing switch arms at `ExpressionEvaluator.cs:50-51` | exact |
| `flow-lang/Lexing/PragmaRegistry.cs` | register `matchExhaustive` (modify) | data-only | self - `KnownPragmas` dict at lines 16-24 (5 existing entries) | exact |
| `flow-lang/Runtime/ExecutionContext.cs` | expose pragmaSet to evaluator (modify) | data-only | `flow-lang/Runtime/ModuleLoader.cs:77-94` (per-file PragmaSet threading) | role-match |

**Sequencing note:** Wave 3. Depends on Wave 1 (Span on AST + Patterns folder needs Span field) AND Wave 2b (test framework needed to cover the new MatchExpression evaluator coverage). LANG-01 + LANG-02 land together in a single plan because music-aware extractors compose with the constructor pattern dispatch.

### Bucket 4 - `-> as name` chain naming (Wave 4, LANG-03)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/Lexing/SimpleLexer.cs` | reserve `as` (modify) | data-only | self - keyword table at `SimpleLexer.cs:850-891` (`tuning`/`voicePool` precedents) | exact |
| `flow-lang/Lexing/TokenType.cs` | add `As` token type (modify) | data-only | self - existing entries (e.g. `Tuning` at TokenType.cs:29) | exact |
| `flow-lang/Parsing/Parser.cs` | extend `->` parse-time transform (modify) | transform | self - `Parser.cs:850-913` (ParseFlowExpression with parse-time desugar to FunctionCallExpression) | exact |
| `flow-lang/Ast/Expressions/FlowExpression.cs` | add `IntermediateName?` (modify) | data-only | self - Phase 21 `Token.OriginalText` defaulted-param precedent | exact |
| `flow-lang/Interpreter/ExpressionEvaluator.cs` | declare binding after eval (modify) | event-driven | self - `EvaluateFlowExpression` at `ExpressionEvaluator.cs:325-346` | exact |

**Sequencing note:** Wave 4 (LAST). Depends on every prior wave - especially Wave 3's MatchExpression handling because some `as name` chain steps may feed match scrutinees. Pure parser-level desugar per RESEARCH; no new AST node.

### Bucket 5 - v1.4 housekeeping (parallel-safe with Wave 1)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:931-962` | HK-01 fix (modify) | transform | `flow-lang/StandardLibrary/Audio/BarRenderer.cs:62-77` (ParallelVoices recursion exemplar) | exact |
| `tests/test_humanize_voice_block.flow` | HK-01 regression (new) | data-only | any existing `tests/test_*.flow` | exact |
| `.planning/phases/17-flow-language-server/17-HUMAN-UAT.md` | HK-02 doc update | docs | self - existing frontmatter; rows already `[pass-via-phase-31-uat]` | exact (cleanup-only) |
| `.planning/phases/04-composition-tools/04-VERIFICATION.md` | HK-03 close | docs | self - `04-VERIFICATION.md` gap entries at lines 6-25 | exact |
| `.planning/REQUIREMENTS.md` | HK-03 checkbox flips | docs | self - existing checkbox conventions | exact |
| `flow-lang/StandardLibrary/Composition/VariationFunctions.cs:253` | HK-03 MutateRhythm enum fix | bugfix | self - per VERIFICATION.md gap entry | exact |
| `CLAUDE.md` | HK-04 footnote rewrite | docs | external memory `project_pre_public_no_legacy_burden.md` | exact (verbatim alignment target) |

**Sequencing note:** Bucket 5 = Wave 1 parallel-safe. HK-01 + HK-03 touch disjoint code (transforms + composition); HK-02 + HK-04 are doc-only. Can land in a single housekeeping plan early in the phase or trickle through the phase as fill-in work.

---

## Pattern Assignments

### Bucket 1 - Span Migration

#### `flow-lang/Core/Span.cs` (new core record)

**Analog:** `flow-lang/Core/SourceLocation.cs` (full file)

**Existing pattern** (`SourceLocation.cs:1-16`):
```csharp
namespace FlowLang.Core;

public record SourceLocation(int Line, int Column, string? FileName = null)
{
    public static SourceLocation Unknown { get; } = new(0, 0, null);

    public override string ToString()
    {
        if (FileName != null)
            return $"{FileName}:{Line}:{Column}";
        return $"{Line}:{Column}";
    }
}
```

**Apply to Span.cs:** Mirror the shape - immutable `record` with two `SourceLocation` fields (Start, End), static `Unknown` singleton, `static At(loc)` convenience for zero-width spans, `ToString()` that prints `Start..End` (or just `Start` when equal). Live in same `FlowLang.Core` namespace alongside SourceLocation.

**Notable departures:** Do NOT replace `SourceLocation`. Per RESEARCH Pattern 1 + Pitfall 1, 200+ read-sites consume `SourceLocation`; Span SUPPLEMENTS rather than supplants. Both fields coexist on every AST/Token record.

---

#### `flow-lang/Lexing/Token.cs` (modify)

**Analog:** Self - Phase 21 `OriginalText` defaulted-param precedent (full current file).

**Existing pattern** (`Token.cs:19-25`):
```csharp
public record Token(
    TokenType Type,
    string Text,
    SourceLocation Location,
    object? Value = null,
    string? OriginalText = null)
{
    public string DiagnosticText => OriginalText ?? Text;
    ...
}
```

**Apply:** Append `Span? Span = null` as a 6th positional ctor param. Add helper `public Span EffectiveSpan => Span ?? FlowLang.Core.Span.At(Location);` so back-compat callers get a synthesized zero-width Span. Keep `Location` for the 4-arg call sites.

**Notable departures:** None - this IS the precedent. Phase 21 added one defaulted param; Phase 35 adds a second. 46 `new Token(...)` sites in SimpleLexer continue to compile unchanged (verified via grep at `SimpleLexer.cs:52`, `:71`, `:79`, `:92`, `:140`, etc.).

---

#### `flow-lang/Lexing/SimpleLexer.cs` (modify - 46 call sites)

**Analog:** Self - the same file already shows the precedent at line 911-923.

**Existing pattern** (`SimpleLexer.cs:911-923`):
```csharp
if (ChordParser.IsChordSymbol(text))
{
    return new Token(TokenType.ChordLiteral, text, start, text);
}

// Phase 21 D-15: when canonicalization happened ...
// preserve the composer's original text in OriginalText so diagnostics surface
// the authored shape.
string? originalText = (text != noteValue) ? text : null;
return new Token(TokenType.NoteLiteral, noteValue, start, noteValue, originalText);
```

**Apply:** At each `new Token(...)` site, track end position (capture `_position`/`_column` AFTER the token's last character is consumed; build a 2nd `SourceLocation` from those + `_fileName`). Pass `Span: new Span(start, endLoc)` as named arg to keep the diff readable.

**Notable departures:** Existing sites store the start `SourceLocation` in a local `start` BEFORE advancing. Span migration needs an `end` local captured AT THE END of each token's scan. For single-char tokens (`SingleChar` path at `:201`), end == start (use `Span.At(start)`).

---

#### `flow-lang/Parsing/Parser.cs` (modify - ~86 AST construction sites)

**Analog:** Self at `Parser.cs:1067` (existing AST construction):
```csharp
Expect(TokenType.RBracket, "Expected ']' after array literal");
return new ArrayLiteralExpression(location, elements);
```

**Apply:** At every `new XxxExpression(location, ...)` or `new XxxStatement(location, ...)` site, capture `endLocation = PreviousToken.Location` AFTER the closing delimiter (or last consumed token) and pass `Span: new Span(location, endLocation)` as named arg. For productions ending with `Expect(...)`, the post-Expect `PreviousToken.Location` IS the end position.

**Notable departures:** A handful of sites construct AST inside a `while`/`for` loop (e.g., args). Span for the OUTER node should still span open-delimiter through close-delimiter. The inner arg expressions already carry their own Spans set by their `Parse*` recursion.

---

#### `flow-lang/Ast/Expressions/*.cs` (16 files) + `flow-lang/Ast/Statements/*.cs` (14 files)

**Analog (Expressions):** `flow-lang/Ast/Expressions/FunctionCallExpression.cs` (full):
```csharp
public record FunctionCallExpression(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Expression> Arguments) : Expression(Location);
```

**Analog (Statements):** `flow-lang/Ast/Statements/VariableDeclaration.cs` (full):
```csharp
public record VariableDeclaration(
    SourceLocation Location,
    FlowType Type,
    string Name,
    Expression Value) : Statement(Location);
```

**Macro precedent for Span as a defaulted last positional param:** `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:292` - `MusicalNoteData` has 17+ defaulted ctor params added across Phases 22 and 25.

**Apply:** Add `Span? Span = null` as the LAST positional param on each record. Do NOT touch the `: Expression(Location)` / `: Statement(Location)` base call - `Location` stays for back-compat reads in LSP/tests (per RESEARCH Pitfall 1).

**Files to touch (Expressions, 16):**
- ArrayIndexExpression.cs, ArrayLiteralExpression.cs, ChordLiteralExpression.cs, FlowExpression.cs, FunctionCallExpression.cs, InterpolatedStringExpression.cs, LambdaExpression.cs, LazyExpression.cs, LiteralExpression.cs, MemberAccessExpression.cs, NoteStreamExpression.cs, ProgressionExpression.cs, SongExpression.cs, SymbolLiteralExpression.cs, TupleLiteralExpression.cs, TupleUnpackFlowExpression.cs, VariableExpression.cs

**Files to touch (Statements, 14):**
- AssignmentStatement.cs, BreakStatement.cs, ContinueStatement.cs, ExpressionStatement.cs, ForStatement.cs, ImportStatement.cs, MusicalContextStatement.cs, ProcDeclaration.cs, ReturnStatement.cs, SectionDeclaration.cs, TupleDestructureStatement.cs, TuningContextStatement.cs, VariableDeclaration.cs, WhileStatement.cs

**Notable departures:** `LambdaExpression` has a nested `LambdaParameter` record (not derived from AstNode) at `LambdaExpression.cs:10` - it does NOT need Span (it's a sub-record, not a node). Skip `LambdaParameter.cs` and the analogous `TupleDestructurePattern` at `TupleDestructureStatement.cs:13`.

---

### Bucket 2a - Diagnostics Renderer

#### `flow-lang/Diagnostics/FlowDiagnostic.cs` (new)

**Analog:** `flow-lang/Diagnostics/FlowError.cs` (full file):
```csharp
public record FlowError(
    DiagnosticLevel Level,
    string Message,
    SourceLocation Location,
    Exception? InnerException = null)
{
    public static FlowError Create(string message, SourceLocation? location = null)
        => new(DiagnosticLevel.Error, message, location ?? SourceLocation.Unknown);
    ...
    public override string ToString() => $"{Location}: {levelStr}: {Message}";
}
```

**Apply:** New richer record:
```csharp
public record FlowDiagnostic(
    DiagnosticLevel Level,
    string Message,
    Span Primary,
    IReadOnlyList<DiagnosticLabel> Labels,
    IReadOnlyList<string> Notes,
    string? Suggestion = null);
```

with `static` factories mirroring FlowError's `Create`/`Warning`/`Info`. Add a `DiagnosticLabel(Span Span, string Text)` nested record for secondary spans. Keep `FlowError` UNCHANGED as legacy path.

**Notable departures:** FlowError carries `SourceLocation`; FlowDiagnostic carries `Span` (Wave 1 prerequisite). FlowDiagnostic is multi-line-aware (Notes list, Suggestion); FlowError prints single-line `{loc}: {level}: {msg}`.

---

#### `flow-lang/Diagnostics/DiagnosticRenderer.cs` (new, also called SnippetRenderer in RESEARCH)

**Analog A (formatting shape):** `flow-lang/Diagnostics/FlowError.cs:23-34` - the existing single-line `ToString()` that builds a label string from the enum:
```csharp
var levelStr = Level switch
{
    DiagnosticLevel.Error => "error",
    DiagnosticLevel.Warning => "warning",
    ...
};
return $"{Location}: {levelStr}: {Message}";
```

**Analog B (TTY color emit):** `flow-interpreter/Program.cs:77-79`:
```csharp
Console.ForegroundColor = ConsoleColor.Red;
Console.Error.WriteLine(engine.ErrorReporter.FormatErrors());
Console.ResetColor();
```

**Apply:** New static `Render(FlowDiagnostic d, SourceMap sources)` returns a multi-line string per RESEARCH §Example 4:
```
error: <Message>
  --> <FileName>:<line>:<col>
   |
 N | <source line>
   |    ^^^^^^^ <label>
   = note: <text>
   = help: did you mean '<suggestion>'?
```

TTY detection via `Console.ForegroundColor = ConsoleColor.Red` only when emitting (mirror Program.cs:77 - .NET auto-handles pipe-vs-TTY by suppressing color when redirected; no explicit `Console.IsOutputRedirected` check needed - the existing 7-site precedent in `flow-interpreter/` is the convention).

**Notable departures:** FlowError uses `Location` and single-line format. DiagnosticRenderer consumes `Span` and pulls source text via `SourceMap` (new). Caret-line generation needs `Span.End.Column - Span.Start.Column` to size the underline.

---

#### `flow-lang/Diagnostics/LevenshteinHelper.cs` (new, EXTRACTED)

**Analog:** `flow-lang/Lexing/PragmaRegistry.cs:42-84` (full Levenshtein impl + SuggestNearest wrapper).

**Existing pattern** (`PragmaRegistry.cs:60-84`):
```csharp
private static int LevenshteinDistance(string a, string b)
{
    int n = a.Length, m = b.Length;
    if (n == 0) return m;
    if (m == 0) return n;
    var prev = new int[m + 1];
    var curr = new int[m + 1];
    for (int j = 0; j <= m; j++) prev[j] = j;
    for (int i = 1; i <= n; i++)
    {
        curr[0] = i;
        for (int j = 1; j <= m; j++)
        {
            int cost = a[i - 1] == b[j - 1] ? 0 : 1;
            curr[j] = Math.Min(
                Math.Min(curr[j - 1] + 1, prev[j] + 1),
                prev[j - 1] + cost);
        }
        (prev, curr) = (curr, prev);
    }
    return prev[m];
}
```

**Apply:** Lift verbatim into new `LevenshteinHelper.cs` (public static). Promote `SuggestNearest(typed, IEnumerable<string> candidates, int? threshold = null)` so it accepts any candidate set (not just KnownPragmas). Default `threshold = Math.Max(2, typed.Length / 3)` per existing PragmaRegistry choice. Update `PragmaRegistry.SuggestNearest` to delegate to the new helper - both call sites converge.

**Notable departures:** Existing impl is private + closure over `KnownPragmas.Keys`. New helper takes candidates as a parameter so DiagnosticRenderer can pass `StackFrame.GetAllAccessibleVariables().Keys` (per RESEARCH §Architectural Responsibility Map row 4).

---

#### `flow-lang/Core/SourceMap.cs` (new)

**Analog:** `flow-lang/Runtime/ExecutionContext.cs:85` - `SymbolInternTable` per-context Dictionary:
```csharp
public Dictionary<string, Value> SymbolInternTable { get; } = new();
```

**Apply:** Module-singleton (or owned by `FlowEngine`) `Dictionary<string, string> _sourceTexts = new(StringComparer.Ordinal)` keyed by absolute file path. Register on entry to lexer (where source is read). Special-case REPL/eval: keys `<eval>`, `<stdin>`, `<repl>` map to the in-memory source string (mirror existing `flow-interpreter/Program.cs:73,101` which already pass these sentinel names to `engine.Execute(source, "<eval>")`).

**Notable departures:** SymbolInternTable is per-`ExecutionContext` instance. SourceMap likely belongs as a member of `FlowEngine` (or static if cross-engine semantics is acceptable per RESEARCH §Pitfall 3 list).

---

#### `flow-lang/Diagnostics/ErrorReporter.cs` (modify)

**Analog:** Self - `ErrorReporter.cs:16-42` (full Report/ReportError methods).

**Existing pattern** (`ErrorReporter.cs:16-31`):
```csharp
public void Report(FlowError error)
{
    if (error.Level == DiagnosticLevel.Error)
        _hasErrors = true;
    if (_errors.Count < MaxErrorCount)
        _errors.Add(error);
    else if (_errors.Count == MaxErrorCount)
        _errors.Add(FlowError.Warning("Maximum error limit reached...", null));
}
```

**Apply:** Add a parallel `Report(FlowDiagnostic diagnostic)` overload that pushes onto a NEW `List<FlowDiagnostic> _diagnostics` collection. `FormatErrors()` stays as-is (legacy path); add `FormatDiagnostics(SourceMap sources)` that routes each diagnostic through `DiagnosticRenderer.Render`. Top-level emit (Program.cs:78) picks `FormatDiagnostics` when the diagnostics list is non-empty, falling back to `FormatErrors` otherwise.

**Notable departures:** Both collections coexist - mid-migration, parser may emit either. Wave 2a's success criterion is that NEW emit sites use FlowDiagnostic; existing FlowError sites are progressively migrated.

---

#### `flow-lang/Interpreter/ExpressionEvaluator.cs` (modify)

**Analog:** Self at `ExpressionEvaluator.cs:335` and `:344`:
```csharp
_errorReporter.ReportError($"Right side of -> must be a function, got {rightVal.Type}", flowEx.Location);
```

**Apply:** Where the error CAN now be a span-rich diagnostic (type-mismatch with two operand spans, did-you-mean for unknown variable name), construct `FlowDiagnostic` instead of plain message + Location. For unknown identifiers, query `LevenshteinHelper.SuggestNearest(typed, _context.CurrentFrame.GetAllAccessibleVariables().Keys)` (per RESEARCH Pitfall 5).

**Notable departures:** Existing call sites pass `flowEx.Location` (single SourceLocation). Span-aware sites use `flowEx.Span ?? Span.At(flowEx.Location)` to handle the mid-migration period where Span may still be null.

---

#### `flow-lang/Parsing/Parser.cs` (parse-error emit, modify)

**Analog:** Self - existing `Expect(TokenType, string message)` pattern throughout the file.

**Apply:** At each parse-error throw site, construct FlowDiagnostic with primary span = `new Span(start, current)` where start was captured at the production's open delimiter. Use the new diagnostic for syntactic errors that have clear span boundaries (unmatched paren, missing semicolon, unknown keyword).

**Notable departures:** Some Parser errors are bare error strings without context - those can keep FlowError emit for v1.5 and migrate later.

---

### Bucket 2b - Pure-Flow Test Framework

#### `flow-lang/StandardLibrary/TestFramework/TestFunctions.cs` (new)

**Analog:** `flow-lang/StandardLibrary/BuiltInFunctions.cs:333-347` - Lazy-wrapped special-form builtins:
```csharp
// Note: eval is registered with Lazy<Void> but will work with any Lazy<T>
var evalSignature = new FunctionSignature(
    "eval",
    [new LazyType(VoidType.Instance)]);
registry.Register("eval", evalSignature, StdLib.Eval);

var ifSignature = new FunctionSignature(
    "if", [BoolType.Instance, new LazyType(VoidType.Instance), new LazyType(VoidType.Instance)]);
registry.Register("if", ifSignature, StdLib.If);
```

**Apply:** Register `test`, `assert`, `assertEq`, `assertNotesMatch`, `assertBytesEqual`, `assertWithinDb` builtins via `InternalFunctionRegistry`. `test` has shape `[StringType.Instance, new LazyType(VoidType.Instance)]` (mirroring `if`'s LazyType wrap so body defers). Assertion functions use `[VoidType.Instance, VoidType.Instance]` (Void wildcard - existing OverloadResolver convention seen at `BuiltInFunctions.cs:371-379` for `equals`).

**Notable departures:** Per RESEARCH Pitfall 10, the LazyType wrap is LOAD-BEARING. Without it, `(test "foo" (body))` evaluates `body` at registration time and hermetic isolation is meaningless. The `if` precedent at line 339 is the exact analog - copy its LazyType shape.

---

#### `flow-lang/StandardLibrary/TestFramework/TestRunner.cs` (new)

**Analog:** No exact analog - closest is the iteration shape in `flow-interpreter/Repl.cs` (REPL Run loop). The orchestration shape is genuinely new.

**Apply:** Public method `Run(FlowEngine engine, string filePath) : (int passed, int failed)` that walks `engine.Context.TestRegistry`, for each test: `engine.Context.SnapshotState() / try { test.BodyThunk.Force(); } catch (AssertionException) { ... } finally { engine.Context.RestoreState(); }`. Concrete shape lifted from RESEARCH §Example 3 (Code Examples block 3).

**Notable departures:** This file is new orchestration logic with no in-codebase analog. Plan should reference RESEARCH §Example 3 directly for the loop body shape.

---

#### `flow-lang/StandardLibrary/TestFramework/AssertionHelpers.cs` (new) + `flow-lang.Tests/Helpers/RmsComparator.cs` (new, EXTRACTED)

**Analog:** `flow-lang.Tests/Helpers/RmsRegressionTests.cs:38-62`:
```csharp
public static void AssertRmsWithinTolerance(
    AudioBuffer rendered,
    string baselineWavPath,
    double windowMs = DefaultWindowMs,
    double toleranceDb = DefaultToleranceDb,
    string? overrideReason = null)
{
    ValidateOverride(toleranceDb, overrideReason);
    string tempPath = Path.Combine(Path.GetTempPath(),
        $"flow_rms_compare_{Guid.NewGuid():N}.wav");
    try
    {
        var args = new List<Value> { Value.String(tempPath), Value.Buffer(rendered) };
        FileIO.WriteWav(args);
        AssertWavMatchesBaseline(tempPath, baselineWavPath, windowMs, toleranceDb, overrideReason);
    }
    finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
}
```

**Apply:** Extract pure comparison logic out of `RmsRegressionTests` into new `RmsComparator.MaxWindowDeviationDb(AudioBuffer a, AudioBuffer b, double windowMs) : double`. The new helper returns the deviation - no `Xunit.Assert` dependency. `AssertionHelpers` (in flow-lang) wraps it and throws a new `AssertionException` when deviation > tolerance. The existing `RmsRegressionTests` xUnit helper keeps its current call signature but delegates to RmsComparator + xUnit.Assert.

**Notable departures:** `RmsRegressionTests.cs` lives in `flow-lang.Tests` (test assembly); the new pure helper must live in either `flow-lang` itself or a new `flow-lang.TestRuntime` project so the runtime can call it without depending on xUnit. RESEARCH §Wave 0 Gaps suggests `flow-lang.Tests/Helpers/RmsComparator.cs`; planner may need to relocate to `flow-lang/StandardLibrary/Audio/` or similar so the runtime builtin can reference it. Confirm at plan-time.

---

#### `flow-cli/Commands/TestCommand.cs` (new)

**Analog:** `flow-cli/Commands/CheckCommand.cs` (full file, 80 lines).

**Existing pattern** (CheckCommand.cs:16-44):
```csharp
public static Command Build()
{
    var scriptArg = new Argument<FileInfo>("script") { Description = "Path to .flow script" };
    var cmd = new Command("check", "Parse a Flow script without executing it");
    cmd.Add(scriptArg);
    cmd.SetAction(parseResult =>
    {
        var script = parseResult.GetValue(scriptArg)!;
        if (!File.Exists(script.FullName))
        {
            Console.Error.WriteLine($"Error: File not found: {script.FullName}");
            return 1;
        }
        string source = File.ReadAllText(script.FullName);
        ...
        using var engine = new FlowEngine(verbose: false);
        success = engine.Execute(source, script.FullName);
        ...
    });
    return cmd;
}
```

**Apply:** Mirror the shape per RESEARCH §Example 3 (Code Examples block 3, lines 697-752). Use `Argument<string?>` with `ArgumentArity.ZeroOrOne` so `flow test` (no arg) defaults to `tests/`. Resolve to file-list via `Directory.GetFiles(path, "test_*.flow", SearchOption.TopDirectoryOnly)`. For each file, instantiate `FlowEngine`, execute the source (registers tests via `(test ...)` calls), iterate `engine.Context.TestRegistry` running snapshot/restore around each `test.BodyThunk.Force()`.

**Notable departures:** CheckCommand takes ONE file; TestCommand takes a file OR directory. Use the `Directory.Exists(path)` branch on the resolved path to choose between glob-walk and single-file mode.

---

#### `flow-cli/Commands/CommandRegistry.cs` (modify)

**Analog:** Self (full file, 32 lines):
```csharp
public static Command[] BuildAllCommands()
{
    return new[]
    {
        RunCommand.Build(), EvalCommand.Build(), ReplCommand.Build(),
        WatchCommand.Build(), PlayCommand.Build(), RenderCommand.Build(),
        Flow2MidiCommand.Build(), Midi2FlowCommand.Build(),
        CheckCommand.Build(), VersionCommand.Build(),
        NewCommand.Build(),
        LspCommand.Build(),
    };
}
```

**Apply:** Append `TestCommand.Build(),` to the array. Update the inline comment counter (currently says "12 subcommands total"; bumps to 13).

**Notable departures:** None. This is a one-line addition.

---

#### `flow-lang/Core/FlowEngine.cs` + `flow-lang/Runtime/ExecutionContext.cs` (modify - SnapshotState/RestoreState)

**Analog:** `flow-lang/Runtime/MusicalContext.cs:99` + `flow-lang/Runtime/ExecutionContext.cs:171-191` (push/pop scoped state - the conceptual analog).

**Existing pattern (PushFrame/PopFrame)** at `ExecutionContext.cs:171-191`:
```csharp
public void PushFrame()
{
    _callDepth++;
    if (_callDepth > MaxCallDepth)
        throw new InvalidOperationException($"Stack overflow: maximum call depth of {MaxCallDepth} exceeded");
    var newFrame = new StackFrame(CurrentFrame);
    _callStack.Push(newFrame);
}

public void PopFrame()
{
    if (_callStack.Count <= 1)
        throw new InvalidOperationException("Cannot pop global frame");
    _callStack.Pop();
    _callDepth--;
}
```

**Apply:** New `public TestSnapshot SnapshotState()` returns an immutable record capturing the 11 mutable surfaces enumerated in RESEARCH §Pitfall 3 (call stack, MusicalContext stacks, voice pool, PRNG, SymbolInternTable, Sfz statics, RenderingDiagnostics._emitted, FlowEngine.CurrentSampleCache, FixedRandSeed/FixedGen/Gen, FlowConfig.Active). `public void RestoreState(TestSnapshot snap)` reinstates each. Mirror the existing `RenderingDiagnostics.ResetForTesting()` (`RenderingDiagnostics.cs:47-50`) and `SynthUtils.ResetNoiseRng()` patterns for the static-mutable resets.

**Notable departures:** PushFrame/PopFrame manage ONLY the call stack. SnapshotState/RestoreState manage the whole-process state surface. New `TestRegistry` property: `public List<TestRecord> TestRegistry { get; } = new();` added alongside `SectionRegistry` (existing at `ExecutionContext.cs:77`). Per RESEARCH Pitfall 3 list - enumerate every site as an explicit checklist in the plan; do NOT use reflection.

---

#### `flow-lang/StandardLibrary/BuiltInFunctions.cs` (modify - registration entry)

**Analog:** Self - the file is composed of partial-class `Register*` calls dispatched from a single `RegisterAll(...)` orchestrator.

**Apply:** Add `RegisterTestFramework(registry, context);` call to the orchestrator. Implementation lives in the new `TestFramework/TestFunctions.cs` partial class (per RESEARCH §Recommended Project Structure).

**Notable departures:** None - additive only.

---

#### `flow-lang/test.flow` (new stdlib module)

**Analog:** `flow-lang/std.flow` (the existing re-export shell module).

**Apply:** Minimal `.flow` file - per RESEARCH Assumption A10, the BUILT-INS live in C#; `test.flow` exists so `use "@test"` works. Initial content can be a single comment or a few `proc` wrappers documenting the surface.

**Notable departures:** No code logic in this file initially - it's a discoverability anchor for `use "@test"`.

---

#### `tests/test_test_framework.flow` (new - meta-dogfooding test)

**Analog:** any existing `tests/test_*.flow` script (60+ examples).

**Apply:** A `.flow` script that itself uses `(test "name" body)` to validate the framework end-to-end. Per CLAUDE.md "no unit test framework... tests are .flow scripts verified by console output" - the meta-test exercises the new framework via the existing convention.

**Notable departures:** This file pioneers the new framework idiom - subsequent tests can reference it as a template.

---

### Bucket 3 - Pattern Matching

#### `flow-lang/Ast/Patterns/Pattern.cs` (new base record)

**Analog:** `flow-lang/Ast/AstNode.cs` (full):
```csharp
public abstract record AstNode(SourceLocation Location)
{
    public FlowType? ResolvedType { get; init; }
}

public abstract record Expression(SourceLocation Location) : AstNode(Location);
public abstract record Statement(SourceLocation Location) : AstNode(Location);
```

**Apply:** Mirror the abstract-base + per-subtype shape. Phase 35 patterns DO NOT inherit from `AstNode` - they're a parallel family per RESEARCH §Recommended Project Structure. New base:
```csharp
public abstract record Pattern(SourceLocation Location, Span? Span = null);
```

Each subtype inherits `: Pattern(Location, Span)`.

**Notable departures:** Patterns DO carry `ResolvedType` (for guard-pattern coercion and exhaustiveness analysis) but optionally - planner should decide whether to add the `init`-property at base or per-subtype.

---

#### `flow-lang/Ast/Patterns/LiteralPattern.cs`

**Analog:** `flow-lang/Ast/Expressions/LiteralExpression.cs` (full):
```csharp
public record LiteralExpression(
    SourceLocation Location,
    object Value) : Expression(Location);
```

**Apply:**
```csharp
public record LiteralPattern(
    SourceLocation Location,
    object Value,
    Span? Span = null) : Pattern(Location, Span);
```

**Notable departures:** None - direct shape lift.

---

#### `flow-lang/Ast/Patterns/WildcardPattern.cs`

**Analog:** `flow-lang/Ast/Expressions/SymbolLiteralExpression.cs` (the single-string-payload shape):
```csharp
public record SymbolLiteralExpression(
    SourceLocation Location,
    string Name) : Expression(Location);
```

**Apply:** Even simpler - no payload:
```csharp
public record WildcardPattern(
    SourceLocation Location,
    Span? Span = null) : Pattern(Location, Span);
```

**Notable departures:** Wildcard has NO payload. Per RESEARCH §Example 2 PatternMatcher, it returns `true` unconditionally.

---

#### `flow-lang/Ast/Patterns/BindingPattern.cs`

**Analog:** `flow-lang/Ast/Expressions/VariableExpression.cs` (name-only payload).

**Apply:**
```csharp
public record BindingPattern(
    SourceLocation Location,
    string Name,
    Span? Span = null) : Pattern(Location, Span);
```

**Notable departures:** Bindings collect a `Value` into the arm's frame when matched. Per RESEARCH Pitfall 6 - bindings die with the arm-body frame; do NOT leak into enclosing scope.

---

#### `flow-lang/Ast/Patterns/ConstructorPattern.cs`

**Analog:** `flow-lang/Ast/Expressions/FunctionCallExpression.cs`:
```csharp
public record FunctionCallExpression(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Expression> Arguments) : Expression(Location);
```

**Apply:**
```csharp
public record ConstructorPattern(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Pattern> SubPatterns,
    Span? Span = null) : Pattern(Location, Span)
{
    // Per RESEARCH §Example 2 EvaluateMatch:
    public bool IsChordLiteral { get; init; }
    public bool IsRomanNumeral { get; init; }
    public bool IsArticulationSymbol { get; init; }
}
```

**Notable departures:** Three discriminator flags differentiate music-aware extractors from generic constructors. Parser sets the appropriate flag based on the recognized token shape (ChordLiteral token → IsChordLiteral=true, etc.). Alternative: separate subtype records `ChordQualityPattern` / `RomanNumeralPattern` / `ArticulationSymbolPattern` - planner decides between flag-or-subtype shape based on extensibility cost.

---

#### `flow-lang/Ast/Patterns/GuardPattern.cs`

**Analog:** `flow-lang/Ast/Expressions/LazyExpression.cs` (wraps an inner expression).

**Apply:**
```csharp
public record GuardPattern(
    SourceLocation Location,
    Pattern Inner,
    Expression GuardExpression,
    Span? Span = null) : Pattern(Location, Span);
```

**Notable departures:** Guard expression evaluated AFTER inner pattern matches AND in the extended scope (so bindings made by Inner are visible to the guard). Per RESEARCH §Example 2 PatternMatcher.

---

#### `flow-lang/Ast/Patterns/MatchArm.cs`

**Analog:** `LambdaParameter` at `flow-lang/Ast/Expressions/LambdaExpression.cs:10`:
```csharp
public record LambdaParameter(string Name, FlowType Type);
```

**Apply:**
```csharp
public record MatchArm(
    Pattern Pattern,
    Expression Body,
    Span? Span = null);
```

**Notable departures:** MatchArm is a value record NOT inheriting from Pattern/Statement/Expression - same posture as LambdaParameter (a sub-record). Sequence-of-arms held by MatchExpression.

---

#### `flow-lang/Ast/Expressions/MatchExpression.cs`

**Analog:** `flow-lang/Ast/Expressions/FlowExpression.cs`:
```csharp
public record FlowExpression(
    SourceLocation Location,
    Expression Left,
    Expression Right) : Expression(Location);
```

**Apply:**
```csharp
public record MatchExpression(
    SourceLocation Location,
    Expression Scrutinee,
    IReadOnlyList<MatchArm> Arms,
    Span? Span = null) : Expression(Location);
```

**Notable departures:** Sibling to FlowExpression in the Expressions/ folder (not under Patterns/). RESEARCH §Recommended Project Structure puts the AST node here intentionally.

---

#### `flow-lang/Interpreter/PatternMatcher.cs` (new)

**Analog:** `flow-lang/Interpreter/ExpressionEvaluator.cs:325-346` (EvaluateFlowExpression switch shape).

**Apply:** Per RESEARCH §Example 2:
```csharp
private bool PatternMatches(Pattern pattern, Value scrutinee, Dictionary<string, Value> bindings)
{
    return pattern switch
    {
        WildcardPattern => true,
        BindingPattern b => Bind(b.Name, scrutinee, bindings),
        LiteralPattern lit => Value.Equals(scrutinee, lit.Value),
        ConstructorPattern ctor => MatchConstructor(ctor, scrutinee, bindings),
        GuardPattern guard => PatternMatches(guard.Inner, scrutinee, bindings)
                              && EvaluateGuard(guard.GuardExpression),
        _ => throw new NotSupportedException($"Unknown pattern: {pattern.GetType().Name}")
    };
}
```

For music-aware extractors, `MatchConstructor` dispatches by the discriminator flags - reuses `ChordParser.IsChordSymbol` + `ChordParser.Parse` (already correct at `flow-lang/StandardLibrary/Harmony/ChordParser.cs:200,258`) and `HarmonyFunctions.resolveNumeral`.

**Notable departures:** Naive linear scan per RESEARCH Open Question 1 default - NOT a decision-tree compile. Identical observable behavior; back-end swap deferred to v1.6.

---

#### `flow-lang/Lexing/SimpleLexer.cs` (modify - new keywords)

**Analog:** `flow-lang/Lexing/SimpleLexer.cs:850-891` (keyword table):
```csharp
var type = text switch
{
    "proc" => TokenType.Proc,
    ...
    "voicePool" => TokenType.VoicePool,
    "tuning" => TokenType.Tuning,
    ...
};
```

**Apply:** Add `"match" => TokenType.Match` and `"when" => TokenType.When` entries. Wildcard `_` is already TokenType.Underscore (existing at `TokenType.cs:91`). FatArrow `=>` is already a token at `TokenType.cs:69` (used by lambdas - reused for match arms).

**Notable departures:** Per RESEARCH Pitfall 4 - `match` and `when` MUST be added carefully; verify no existing tests use them as variable names. Per D-v1.5-01 latitude they break in one commit if collision found.

---

#### `flow-lang/Lexing/TokenType.cs` (modify)

**Analog:** Self - existing enum entries.

**Apply:** Add `Match,` and `When,` in the Keywords block (alongside `Tuning` at TokenType.cs:29).

**Notable departures:** None.

---

#### `flow-lang/Parsing/Parser.cs` (modify - ParseMatch)

**Analog:** Existing parenthesized-call form at `Parser.cs:1089-1115`:
```csharp
if (Match(TokenType.LParen))
{
    var location = PreviousToken.Location;
    if ((Check(TokenType.Identifier) || Check(TokenType.Pan) || Check(TokenType.Gain))
        && _current + 1 < _tokens.Count
        && _tokens[_current + 1].Type != TokenType.Arrow
        ...)
    {
        var name = Advance().Text;
        var args = new List<Expression>();
        ...
        while (!Check(TokenType.RParen) && !IsAtEnd())
            args.Add(ParseExpression());
        Expect(TokenType.RParen, "Expected ')' after function arguments");
        return new FunctionCallExpression(location, name, args);
    }
    ...
}
```

**Apply:** New `ParseMatch()` invoked when the parser sees `(match` (a LParen followed by the `Match` keyword token). After consuming `(match`, parse a single scrutinee expression, then loop: expect `Pipe`, parse a Pattern (new `ParsePattern()`), expect `FatArrow`, parse a body Expression; collect into MatchArm list. End on `RParen`. Note-stream disambiguation: per RESEARCH Pitfall 2, ParseMatch's `Pipe` handling never delegates to `ParseNoteStream` - the disambiguator is "in match-arms mode" tracked via a local flag (not via `_inFuncCallArgs`).

**Notable departures:** Per RESEARCH §Critical detail at line 437, the disambiguation rule is: inside `ParseMatch`, after consuming the scrutinee, every `Pipe` introduces an arm. The existing `Parser.cs:1044` note-stream `Pipe` only fires from primary-expression start positions, which match-arm Pipe is NOT (it's a delimiter).

---

#### `flow-lang/Interpreter/ExpressionEvaluator.cs` (modify - dispatch)

**Analog:** Self at `ExpressionEvaluator.cs:50-51` (existing switch arms):
```csharp
FlowExpression flowEx => EvaluateFlowExpression(flowEx),
TupleUnpackFlowExpression unpackEx => EvaluateTupleUnpackFlow(unpackEx),
```

**Apply:** Add a new arm:
```csharp
MatchExpression matchEx => EvaluateMatch(matchEx),
```

Plus the EvaluateMatch method per RESEARCH §Example 2 (lines 567-599). Bindings declared in a pushed StackFrame per Pitfall 6 (PushFrame/PopFrame pattern already at `ExecutionContext.cs:171-191`).

**Notable departures:** None - direct extension of existing dispatch.

---

#### `flow-lang/Lexing/PragmaRegistry.cs` (modify - matchExhaustive entry)

**Analog:** Self - `PragmaRegistry.cs:16-24` (KnownPragmas dict):
```csharp
public static readonly IReadOnlyDictionary<string, string> KnownPragmas =
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["hAsB"] = "Inside note streams, accept 'H' as a synonym for 'B' ...",
        ["justIntonation"] = "5-limit just-intonation render-time tuning ...",
        ["pythagorean"] = "3-limit Pythagorean ...",
        ["equalTemperament"] = "12-tone equal temperament ...",
        ["scaleLint"] = "Phase 31 D-03: scale-lint is now default-on; ..."
    };
```

**Apply:** Add the entry:
```csharp
["matchExhaustive"] = "Promote non-exhaustive match warnings (D-v1.5-05) to errors. File-scope only; does NOT propagate via `use` imports (PRAG-02)."
```

**Notable departures:** None - 5 existing entries set the exact precedent.

---

#### `flow-lang/Runtime/ExecutionContext.cs` (modify - expose pragmaSet to evaluator)

**Analog:** `flow-lang/Runtime/ModuleLoader.cs:77-94` shows how PragmaSet is currently threaded into lexer + parser per file.

**Apply:** Per RESEARCH §Example 2 EvaluateMatch: the evaluator queries `_context.ProgramPragmaSet.Has("matchExhaustive")`. Add a `PragmaSet ProgramPragmaSet { get; set; } = PragmaSet.Empty;` property on ExecutionContext, set by FlowEngine/ModuleLoader at parse-time entry.

**Notable departures:** Per RESEARCH Pitfall 4, pragmas are FILE-SCOPE. Each imported file has its own PragmaSet (already enforced at ModuleLoader.cs:82-83). The ExecutionContext-level ProgramPragmaSet reflects the CURRENTLY-PARSED file's pragmas; when evaluation crosses module boundaries, the executing match expression carries the pragmaSet IT was parsed under. Planner should verify whether to thread pragmaSet on MatchExpression itself OR rely on ExecutionContext switching - probably the former (per the AST node carrying its own pragma context).

---

### Bucket 4 - `-> as name` Chain Naming

#### `flow-lang/Lexing/SimpleLexer.cs` (modify - reserve `as`)

**Analog:** Same keyword-table site at `SimpleLexer.cs:850-891`.

**Apply:** Add `"as" => TokenType.As,` to the switch. Per RESEARCH Assumption A3 (verified via grep) `as` is not currently a keyword in SimpleLexer; D-v1.5-01 latitude covers any user collision.

**Notable departures:** None.

---

#### `flow-lang/Lexing/TokenType.cs` (modify)

**Analog:** Self - existing entries.

**Apply:** Add `As,` in the Keywords block (alongside `In` at TokenType.cs:35).

**Notable departures:** None.

---

#### `flow-lang/Parsing/Parser.cs` (modify - extend `->` parse-time transform)

**Analog:** Self - `Parser.cs:850-913` (full `ParseFlowExpression`):
```csharp
private Expression ParseFlowExpression()
{
    var left = ParseUnaryShorthand();
    while (true)
    {
        bool isTildeArrow;
        if (Match(TokenType.Arrow)) { isTildeArrow = false; }
        else if (Match(TokenType.TildeArrow)) { isTildeArrow = true; }
        else break;
        var location = PreviousToken.Location;
        var right = ParseUnaryShorthand();
        ...
        if (right is VariableExpression varExpr) { ... }
        else if (right is FunctionCallExpression funcCall) { ... }
        else { left = new FlowExpression(location, left, right); continue; }
        left = right;
    }
    return left;
}
```

**Apply:** After the `var right = ParseUnaryShorthand();` line, peek for `As` token. If present, advance + consume an Identifier, store as `intermediateName` local. After the existing transform produces the final `right` (the FunctionCallExpression with `left` prepended), attach `IntermediateName: intermediateName` to it - if right is a FunctionCallExpression, use a parallel parse-time desugar OR annotate FlowExpression with the name (RESEARCH §Pattern 3 + §Pitfall 7 recommend FlowExpression annotation).

**Notable departures:** Per RESEARCH Pattern 3, the desugar shape is `{ Type name = (RHS LHS); (NEXT name) }`. Simpler implementation: annotate FlowExpression.IntermediateName and let the evaluator do `_context.DeclareVariable(intermediateName, result)` in the current frame after computing the result. RESEARCH §Pitfall 7 details the scope visibility rule: "the `as` clause makes the result available under the given name from this point onward in the enclosing scope, until end of statement."

---

#### `flow-lang/Ast/Expressions/FlowExpression.cs` (modify)

**Analog:** Self + the Token.cs Phase 21 defaulted-param precedent.

**Existing pattern** (`FlowExpression.cs:9-12`):
```csharp
public record FlowExpression(
    SourceLocation Location,
    Expression Left,
    Expression Right) : Expression(Location);
```

**Apply:** Add the defaulted-param annotation per RESEARCH §Pattern 3:
```csharp
public record FlowExpression(
    SourceLocation Location,
    Expression Left,
    Expression Right,
    string? IntermediateName = null,
    Span? Span = null) : Expression(Location);
```

**Notable departures:** Two defaulted params land together (IntermediateName for LANG-03 + Span from Wave 1). Order matters for record-with syntax - put Span LAST per the Wave 1 sweep convention. Per LANG-03 explicit constraint: NO new AST node - this record gets the annotation.

---

#### `flow-lang/Interpreter/ExpressionEvaluator.cs` (modify - bind after eval)

**Analog:** Self - `ExpressionEvaluator.cs:325-346` (EvaluateFlowExpression):
```csharp
private Value EvaluateFlowExpression(FlowExpression flowEx)
{
    var leftVal = Evaluate(flowEx.Left);
    var rightVal = Evaluate(flowEx.Right);
    if (rightVal.Type is FunctionType || rightVal.Data is FunctionOverload)
    {
        var overload = rightVal.Data as FunctionOverload;
        ...
        var args = new List<Value> { leftVal };
        if (overload.IsInternal) return overload.Implementation!(args);
        else return _invoker.ExecuteUserFunctionWithCaptures(overload.Declaration!, args, overload.CapturedVariables);
    }
    ...
}
```

**Apply:** After the result Value is computed (each return path), check `if (flowEx.IntermediateName != null) _context.DeclareVariable(flowEx.IntermediateName, result);` before returning. Result of the chain step is the Value returned to the caller AND the name is now in scope for subsequent chain steps in the same enclosing block.

**Notable departures:** Per RESEARCH Pitfall 7 - declaration must happen in CURRENT frame (NOT a pushed temporary) so subsequent `->` steps and same-block statements can read it.

---

### Bucket 5 - v1.4 Housekeeping

#### `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:931-962` (HK-01 fix)

**Analog A (buggy code):** Self at `TransformFunctions.cs:931-962`:
```csharp
private static Value HumanizeGaussian(IReadOnlyList<Value> args)
{
    var seq = args[0].As<SequenceData>();
    double amount = Math.Clamp(args[1].As<double>(), 0.0, 1.0);
    int seed = args[2].As<int>();
    if (amount == 0.0) return Value.Sequence(seq);
    var rng = new Random(seed);
    var result = new SequenceData();
    foreach (var bar in seq.Bars)
    {
        var newNotes = new List<MusicalNoteData>();
        foreach (var note in bar.MusicalNotes)             // BUG: ignores bar.ParallelVoices
        {
            if (note.IsRest) { newNotes.Add(note); continue; }
            double z = NextGaussianSample(rng);
            double velJitter = z * amount * 0.2;
            double newVelocity = Math.Clamp(note.Velocity + velJitter, 0.05, 1.0);
            newNotes.Add(note.With(velocity: newVelocity));
        }
        result.AddBar(new BarData(newNotes, bar.TimeSignature!));   // BUG: drops ParallelVoices
    }
    return Value.Sequence(result);
}
```

**Analog B (correct ParallelVoices recursion):** `flow-lang/StandardLibrary/Audio/BarRenderer.cs:62-77`:
```csharp
if (bar.ParallelVoices != null && bar.ParallelVoices.Count > 0)
{
    var combined = new List<Voice>();
    foreach (var voiceBar in bar.ParallelVoices)
    {
        if (voiceBar.TimeSignature == null)
            voiceBar.TimeSignature = bar.TimeSignature;
        var subVoices = RenderBarToVoices(voiceBar, synthesizer, sampleRate, bpm, tuning);
        combined.AddRange(subVoices);
    }
    return combined;
}
```

**Apply:** Restructure HumanizeGaussian to mirror the BarRenderer recursion: check `bar.ParallelVoices`, and when non-null, recursively humanize each voice-sub-bar with the SAME RNG (so determinism preserved - per RESEARCH Pitfall 8 "humanize them with the SAME seed-derived RNG so the same composer-visible determinism holds"). Construct output BarData preserving ParallelVoices either by passing through or by populating with humanized sub-bars. Two-line conceptual fix:
1. Build new bar's `ParallelVoices` from the recursively-humanized sub-bars
2. New BarData ctor either takes the ParallelVoices arg OR (likely) requires post-construction `.ParallelVoices = humanizedVoices` assignment per the existing BarData shape at `BarType.cs:76` (mutable property).

**Notable departures:** RNG sharing is load-bearing - per Phase 25 / Phase 18 byte-identical determinism contract preserved by sharing the single seeded Random across all voices. Do NOT seed a per-voice RNG.

---

#### `tests/test_humanize_voice_block.flow` (new - HK-01 regression)

**Analog:** any `tests/test_*.flow` (~70 existing). Closest: existing `tests/test_humanize.flow` if present.

**Apply:** Per RESEARCH §Pitfall 8 Verification approach: construct a 2-voice block, apply humanizeGaussian, render to WAV, assert WAV is >44 bytes AND contains non-silent samples. Uses existing `writeWav` builtin; verification by `success = no errors` per CLAUDE.md test convention.

**Notable departures:** None - direct application of CLAUDE.md test convention.

---

#### `.planning/phases/17-flow-language-server/17-HUMAN-UAT.md` (HK-02 doc update)

**Analog:** Self - file's existing frontmatter format.

**Apply:** Per RESEARCH §Validation Architecture HK-02 row + Assumption A9, rows 1-3 already show `[pass-via-phase-31-uat]` + `closed_via: Phase 31 Plan 31-08 UAT`. HK-02 work is therefore documentation closure - confirm by Read + flip any remaining REQUIREMENTS.md checkbox + add cross-reference in STATE.md.

**Notable departures:** Likely zero edits to 17-HUMAN-UAT.md itself (already closed). Edits land in REQUIREMENTS.md / STATE.md / phase 17's tracking file.

---

#### `.planning/phases/04-composition-tools/04-VERIFICATION.md` + `flow-lang/StandardLibrary/Composition/VariationFunctions.cs:253` + `.planning/REQUIREMENTS.md` (HK-03)

**Analog:** Self - the verification gap is documented in detail at `04-VERIFICATION.md:6-25`:
```
gaps:
  - truth: "COMP-01 and COMP-02 are reflected as complete in REQUIREMENTS.md"
    missing:
      - "Update REQUIREMENTS.md line 31: change '- [ ] **COMP-01**' to '- [x] **COMP-01**'"
      ...
  - truth: "vary() rhythm mutation splits notes into correct subdivisions"
    artifacts:
      - path: "flow-lang/StandardLibrary/Composition/VariationFunctions.cs"
        issue: "MutateRhythm switch at line 253: case values treat DurationValue as beat fractions ... instead of NoteValueType enum integers"
    missing:
      - "Fix MutateRhythm switch to use correct enum values: case 0 => 1 (WHOLE->HALF), case 1 => 2 (HALF->QUARTER), case 2 => 3 (QUARTER->EIGHTH), case 3 => 4 (EIGHTH->SIXTEENTH)"
```

**Apply:** Per the missing list verbatim:
- Flip 4 checkboxes in REQUIREMENTS.md (lines 31, 32, 99, 100)
- Fix VariationFunctions.cs:253 switch (4 case values)
- Update 04-VERIFICATION.md `status: gaps_found` → `status: verified` after re-verification

**Notable departures:** None - this is a documented, deterministic fix.

---

#### `CLAUDE.md` (HK-04 footnote rewrite)

**Analog:** External memory `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/project_pre_public_no_legacy_burden.md` (the rewritten target framing per CLAUDE.md current "Public as of v1.4" footnote).

**Apply:** Rewrite the CLAUDE.md footnote (currently citing the post-public-deprecation rule) to match the rewritten memory file's pre-traction-no-deprecation framing. Per D-v1.5-01: latitude is ACTIVE - breaking syntax/builtin changes still ship in single commits.

**Notable departures:** Pure prose edit. No code touch. The current CLAUDE.md text reads "Pre-public scope-creep-without-deprecation latitude no longer applies" - this is now stale per the rewritten memory.

---

## Shared Patterns

### Defaulted-Parameter Record Migration (Span sweep)

**Source pattern:** `flow-lang/Lexing/Token.cs:19-25` (Phase 21 OriginalText) + `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:292` (Phase 22+25 17-param MusicalNoteData)

**Apply to:** every AST record (16 expressions + 14 statements) + Token

**Pattern:**
```csharp
public record FooExpression(
    SourceLocation Location,
    // ... existing fields ...
    Span? Span = null) : Expression(Location);  // Span is LAST positional, defaulted
```

**Critical:** Keep `Location` as the FIRST positional field. Span SUPPLEMENTS. Every existing call site continues to compile.

---

### Lazy-Body Special-Form Builtin

**Source:** `flow-lang/StandardLibrary/BuiltInFunctions.cs:333-340`:
```csharp
var ifSignature = new FunctionSignature(
    "if", [BoolType.Instance, new LazyType(VoidType.Instance), new LazyType(VoidType.Instance)]);
registry.Register("if", ifSignature, StdLib.If);
```

**Apply to:** `(test "name" body)` (TEST-01) + match-arm bodies (LANG-01)

**Pattern:** Wrap any param that should defer evaluation in `new LazyType(VoidType.Instance)`. The arg arrives at the implementation as a `Thunk` which is `.Force()`-ed only when intentionally invoked. Per RESEARCH Pitfall 10 - this is LOAD-BEARING for the test framework.

---

### Pragma Plumbing (file-scope, not propagated via `use`)

**Source:** `flow-lang/Lexing/PragmaRegistry.cs:16-24` (KnownPragmas dict) + `flow-lang/Lexing/PragmaSet.cs:14-28` (Has) + `flow-lang/Runtime/ModuleLoader.cs:77-94` (per-import isolation)

**Apply to:** `matchExhaustive` (LANG-01)

**Pattern:** Add entry to `KnownPragmas`; query at runtime via `pragmaSet.Has("matchExhaustive")` from the evaluator. Phase 21 (hAsB), Phase 23 (justIntonation / pythagorean / equalTemperament), Phase 24 (scaleLint) all set the precedent - 5 entries are already in the dict. Per RESEARCH Pitfall 4: pragmas are PER-FILE; mod1 enabling does NOT propagate to mod2 imported via `use`.

---

### One-Shot stderr Advisory (charitable interpretation)

**Source:** `flow-lang/Diagnostics/RenderingDiagnostics.cs:29-36`:
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

**Apply to:**
- Non-exhaustive match warning (D-v1.5-05) - sentinel `match-non-exhaustive:{Span}`
- HK-01 humanize voice-block edge cases if any
- Any test-framework advisory (e.g., test body returns non-Void per RESEARCH Open Question 4)

**Pattern:** Single point of stderr emission with per-sentinel dedup so REPL workflows don't flood the console. ResetForTesting() at line 47 is the test-isolation hook.

---

### TTY-Aware Color Emit

**Source:** `flow-interpreter/Program.cs:77-79` (7 sites total in flow-interpreter):
```csharp
Console.ForegroundColor = ConsoleColor.Red;
Console.Error.WriteLine(engine.ErrorReporter.FormatErrors());
Console.ResetColor();
```

**Apply to:** All diagnostic emit sites in `DiagnosticRenderer` (LANG-04) + `TestCommand`'s FAIL line.

**Pattern:** .NET's `Console.ForegroundColor` auto-suppresses ANSI escapes when stdout/stderr is redirected to a pipe/file - no explicit `Console.IsOutputRedirected` check needed. Always `ResetColor()` after the colored line.

---

### ParallelVoices Recursion (correctness pattern)

**Source:** `flow-lang/StandardLibrary/Audio/BarRenderer.cs:62-77` (canonical) + `flow-lang/StandardLibrary/Audio/MidiExport.cs:467-470` + `flow-lang/StandardLibrary/Audio/Sfz/SfzSampleCache.cs:182-184` (3 in-codebase precedents).

**Apply to:** HK-01 HumanizeGaussian fix.

**Pattern:** When iterating bars in a SequenceData, check `bar.ParallelVoices != null && Count > 0` BEFORE iterating `bar.MusicalNotes`. Recurse into voice sub-bars; mix/combine results. Phase 28 voice blocks store inner voices in `ParallelVoices` (typed `List<BarData>?` at `BarType.cs:76`), not in MusicalNotes. Any pass that touches Sequence content must respect this branching.

---

## No Analog Found

The following files genuinely lack an in-codebase precedent. Planner should reference RESEARCH.md examples directly:

| File | Role | Reason |
|------|------|--------|
| `flow-lang/StandardLibrary/TestFramework/TestRunner.cs` | test orchestrator | First in-process test runner; closest is REPL Run loop. Reference RESEARCH §Example 3 (lines 694-752) for the loop body shape. |
| `flow-lang/Interpreter/PatternMatcher.cs` (the EvaluateMatch + PatternMatches logic) | runtime pattern dispatch | First pattern-match evaluator. Switch-on-AST-node is precedented (ExpressionEvaluator.cs:50-51) but the recursive PatternMatches + Bind logic is new. Reference RESEARCH §Example 2 (lines 564-630) for the exact shape. |
| `flow-lang/Core/SourceMap.cs` | file-path-keyed source registry | First cross-engine source-text cache. ExecutionContext.SymbolInternTable is shape-only analog. REPL "no path" handling per RESEARCH §Don't Hand-Roll table. |
| `flow-lang/Diagnostics/DiagnosticRenderer.cs` (rust-style multi-line output) | renderer | First multi-line diagnostic emit in the codebase. Reference RESEARCH §Example 4 (lines 757-781) for the format spec; rustc-dev-guide for the canonical pattern. |

---

## Metadata

**Analog search scope:**
- `flow-lang/Ast/` (full)
- `flow-lang/Core/` (full)
- `flow-lang/Diagnostics/` (full)
- `flow-lang/Lexing/` (full)
- `flow-lang/Parsing/Parser.cs` (focused at lines 850-1149)
- `flow-lang/Interpreter/ExpressionEvaluator.cs` (focused at lines 50-51, 325-385)
- `flow-lang/Runtime/` (ExecutionContext, MusicalContext, StackFrame, ModuleLoader)
- `flow-lang/StandardLibrary/` (BuiltInFunctions, ChordParser, Harmony, Transforms/TransformFunctions, Audio/BarRenderer)
- `flow-lang/TypeSystem/SpecialTypes/` (NoteType)
- `flow-lang.Tests/Helpers/RmsRegressionTests.cs`
- `flow-cli/` (full Commands/ + Program.cs hierarchy)
- `flow-interpreter/` (Program.cs, color-emit precedents)

**Files scanned:** ~50 source files + 4 planning files (RESEARCH.md, REQUIREMENTS.md, ROADMAP.md, 04-VERIFICATION.md)

**Pattern extraction date:** 2026-05-18
