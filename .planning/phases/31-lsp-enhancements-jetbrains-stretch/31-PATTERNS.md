# Phase 31: LSP Enhancements + JetBrains Stretch — Pattern Map

**Mapped:** 2026-05-11
**Files analyzed:** 21 (3 new analyzers, 3 modified handlers, 1 modified lexer, 1 modified pragma registry, 2 new CLI files, 3 modified grammar files, 5 new JetBrains files, 6 new test files)
**Analogs found:** 17 / 21 (4 have no in-repo analog — JetBrains scaffolding; planner uses RESEARCH §6 LSP4IJ exemplars)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lsp/Diagnostics/UnusedImportAnalyzer.cs` | analyzer | request-response (pure AST walk) | `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` | exact |
| `flow-lsp/Diagnostics/UnreachableSectionAnalyzer.cs` | analyzer | request-response (pure AST walk) | `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` | exact |
| `flow-lsp/Diagnostics/ShadowedVariableAnalyzer.cs` | analyzer | request-response (pure AST walk) | `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` | exact |
| `flow-lsp/Diagnostics/CombinedDiagnosticsPublisher.cs` (MODIFY) | orchestrator | request-response | self (extend `BuildAll`) | exact |
| `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` (MODIFY) | analyzer | request-response | self (remove D-19 gate, return Information by default) | exact |
| `flow-lsp/Handlers/CompletionHandler.cs` (MODIFY) | handler | request-response | self (extend `BuildItems` with 3 filters) | exact |
| `flow-lsp/Handlers/HoverHandler.cs` (MODIFY) | handler | request-response | self (varargs ellipsis in `BuildHover`) | exact |
| `flow-lsp/Handlers/SignatureHelpHandler.cs` (MODIFY) | handler | request-response | self (varargs ellipsis in `Handle`) | exact |
| `flow-lsp/LspMappings.cs` (MODIFY) | utility | transform | self (add `FormatSignature` helper) | exact |
| `flow-lsp/Symbols/StdlibSymbolIndex.cs` (MODIFY) | symbol-index | request-response | self (add `ProcsForModule` helper) | exact |
| `flow-lang/Lexing/SimpleLexer.cs` (MODIFY) | lexer | transform (chars → tokens) | self (mirror `Note:` arm at line 1144) | exact |
| `flow-lang/Lexing/PragmaRegistry.cs` (MODIFY) | config | static-data | self (leave `scaleLint` in `KnownPragmas` as no-op) | exact |
| `flow-cli/Commands/LspCommand.cs` (NEW) | controller | event-driven (subcommand) | `flow-cli/Commands/ReplCommand.cs` | exact |
| `flow-cli/Commands/CommandRegistry.cs` (MODIFY) | config | static-data | self (insert `LspCommand.Build()` row) | exact |
| `vscode-extension/syntaxes/flow.tmLanguage.json` (MODIFY) | config (grammar) | declarative | self (extend `#comments` patterns + add `#function-call` / `#variable-ref`) | exact |
| `vscode-extension/tests/grammar/comment-forms.flow` (NEW) | test fixture | declarative | `vscode-extension/tests/grammar/sample.flow` | exact |
| `vscode-extension/tests/grammar/function-calls.flow` (NEW) | test fixture | declarative | `vscode-extension/tests/grammar/sample.flow` | exact |
| `flow-jetbrains/build.gradle.kts` (NEW) | config | declarative | NONE (use RESEARCH §6 exemplar) | no-analog |
| `flow-jetbrains/settings.gradle.kts` (NEW) | config | declarative | NONE | no-analog |
| `flow-jetbrains/gradle.properties` (NEW) | config | declarative | NONE | no-analog |
| `flow-jetbrains/src/main/resources/META-INF/plugin.xml` (NEW) | config | declarative | NONE (use RESEARCH §6 exemplar) | no-analog |
| `flow-jetbrains/src/main/kotlin/dev/flowlang/jetbrains/FlowLanguageServerFactory.kt` (NEW) | controller | event-driven (LSP factory) | NONE (use RESEARCH §6 exemplar) | no-analog |
| `flow-lang.Tests/Unit/Phase31/UnusedImportAnalyzerFacts.cs` (NEW) | test | request-response | `flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerFacts.cs` | exact |
| `flow-lang.Tests/Unit/Phase31/UnreachableSectionAnalyzerFacts.cs` (NEW) | test | request-response | same as above | exact |
| `flow-lang.Tests/Unit/Phase31/ShadowedVariableAnalyzerFacts.cs` (NEW) | test | request-response | same as above | exact |
| `flow-lang.Tests/Unit/Phase31/ScaleLintDefaultOnFacts.cs` (NEW) | test | request-response | same as above | exact |
| `flow-lang.Tests/Unit/Phase31/CompletionFilterFacts.cs` (NEW) | test | request-response | `flow-lang.Tests/Unit/Phase17/CompletionHandlerTests.cs` | exact |
| `flow-lang.Tests/Unit/Phase31/VarargsRenderingFacts.cs` (NEW) | test | request-response | `flow-lang.Tests/Unit/Phase17/HoverHandlerTests.cs` + `SignatureHelpHandlerTests.cs` | exact |
| `flow-lang.Tests/Unit/Phase31/Phase31LexerCommentFormsTests.cs` (NEW) | test | transform | follows xunit `[Fact]` shape (no direct lexer-test analog cited) | role-match |

## Pattern Assignments

### `flow-lsp/Diagnostics/UnusedImportAnalyzer.cs` (analyzer, request-response) — NEW

**Analog:** `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs`

**Imports pattern** (ScaleLintAnalyzer.cs:1-12):
```csharp
using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Lexing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using FlowProgram = FlowLang.Ast.Program;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace FlowLsp.Diagnostics;
```

New analyzer drops `FlowLang.StandardLibrary.Audio.Tuning` + `FlowLang.StandardLibrary.Harmony` + `FlowLsp.NoteStream` (those are scale-lint-specific) and ADDS `using FlowLsp.Symbols;` to consume `StdlibSymbolIndex`.

**Public static `Analyze` signature** (ScaleLintAnalyzer.cs:36-39):
```csharp
public static IReadOnlyList<Diagnostic> Analyze(
    FlowProgram ast,
    IReadOnlyList<Token> tokens,
    string source)
```

Phase 31 D-03 means new analyzers have NO pragma-gate short-circuit (Phase 24 D-19 was specific to scaleLint opt-in). The UnusedImport variant adds a `StdlibSymbolIndex stdlib` parameter (per RESEARCH §Code Examples line 599-600):
```csharp
public static IReadOnlyList<Diagnostic> Analyze(
    FlowProgram ast,
    IReadOnlyList<Token> tokens,
    string source,
    StdlibSymbolIndex stdlib)
```

**Diagnostic construction pattern** (ScaleLintAnalyzer.cs:264, 286-300 + builder shape):
```csharp
var range = new Range(new Position(line0, col0), new Position(line0, col0 + width));
var diag = new Diagnostic
{
    Severity = DiagnosticSeverity.Information,    // ScaleLint uses Information
    Source = "flow.scaleLint",                    // Dotted source for filter independence
    Message = message,
    Range = range
};
```

For Phase 31 the three new analyzers use:
- UnusedImport → `Severity = Warning`, `Source = "flow.unusedImport"`
- UnreachableSection → `Severity = Information`, `Source = "flow.unreachableSection"`
- ShadowedVariable → `Severity = Warning`, `Source = "flow.shadowedVariable"`

**Class shape and "never throws" contract** (ScaleLintAnalyzer.cs:14-29 docblock — copy this discipline):
- `public static class` (NOT instance)
- Single public method `Analyze`; everything else `private static`
- Pure: reads AST + tokens + source string, returns `IReadOnlyList<Diagnostic>`
- Never publishes; the orchestrator (`CombinedDiagnosticsPublisher`) does that
- Charitable / fail-open: malformed AST returns `Array.Empty<Diagnostic>()`

**AST traversal helper** (ScaleLintAnalyzer.cs:50-78):
```csharp
private static void WalkStatements(
    IReadOnlyList<Statement> stmts,
    FlowProgram ast,
    IReadOnlyList<Token> tokens,
    string source,
    List<Diagnostic> diagnostics)
{
    foreach (var stmt in stmts)
    {
        switch (stmt)
        {
            case MusicalContextStatement m:
                WalkStatements(m.Body, ast, tokens, source, diagnostics);
                break;
            case SectionDeclaration sd:
                WalkStatements(sd.Body, ast, tokens, source, diagnostics);
                break;
            case ProcDeclaration pd:
                WalkStatements(pd.Body, ast, tokens, source, diagnostics);
                break;
            // case branches per analyzer-specific interest
        }
    }
}
```

For ShadowedVariableAnalyzer specifically: extend with a `Dictionary<string, SourceLocation>` scope stack so `VariableDeclaration` nodes inside nested `MusicalContextStatement` / `SectionDeclaration` / `ProcDeclaration` bodies can detect collisions against the outer scope.

**Required AST types** (already exist):
- `flow-lang/Ast/Statements/ImportStatement.cs` (Location, FilePath) — UnusedImport walks these
- `flow-lang/Ast/Statements/SectionDeclaration.cs` (Location, Name, Body) — UnreachableSection walks these and cross-references `SongExpression`
- `flow-lang/Ast/Statements/VariableDeclaration.cs` (Location, Type, Name, Value) — ShadowedVariable walks these

---

### `flow-lsp/Diagnostics/UnreachableSectionAnalyzer.cs` (analyzer, request-response) — NEW

**Analog:** `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs`

Same imports + same shape as UnusedImportAnalyzer above. Key delta:

**Two-pass strategy** (no direct analog — derive from `WalkStatements` shape):
1. Pass 1: walk all `SectionDeclaration` nodes; collect `(Name → Location)` map of every defined section
2. Pass 2: walk all `ExpressionStatement` containing `SongExpression` (and direct `SongExpression` references); collect referenced section names
3. Emit one `Diagnostic { Severity = Information, Source = "flow.unreachableSection" }` per defined-but-unreferenced section

The Phase 24 `WalkStatements` recursion handles section-inside-MusicalContextStatement nesting verbatim.

---

### `flow-lsp/Diagnostics/ShadowedVariableAnalyzer.cs` (analyzer, request-response) — NEW

**Analog:** `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs`

Same imports + same shape. Key delta:

**Scope-stack walker** (extend `WalkStatements` with a `Stack<HashSet<string>> scopes`):
- Push new scope on entry to `MusicalContextStatement.Body` / `SectionDeclaration.Body` / `ProcDeclaration.Body`
- For each `VariableDeclaration`: walk the stack — if `Name` matches an OUTER frame, emit `Diagnostic { Severity = Warning, Source = "flow.shadowedVariable" }`; then add `Name` to the current frame
- Pop on body exit

Message format (charitable per `feedback_charitable_interpretation` memory): `Variable '{name}' shadows declaration at line {outerLine}, column {outerCol}`.

---

### `flow-lsp/Diagnostics/CombinedDiagnosticsPublisher.cs` (orchestrator, request-response) — MODIFY

**Analog:** self (extend the existing static `BuildAll` method at line 48)

**Current shape** (CombinedDiagnosticsPublisher.cs:48-58):
```csharp
public static IReadOnlyList<Diagnostic> BuildAll(ParseResult result, string source)
{
    var parseDiags = DiagnosticsPublisher.BuildDiagnostics(result.Errors);
    var lintDiags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, source);
    if (parseDiags.Count == 0 && lintDiags.Count == 0)
        return Array.Empty<Diagnostic>();
    var merged = new List<Diagnostic>(parseDiags.Count + lintDiags.Count);
    merged.AddRange(parseDiags);
    merged.AddRange(lintDiags);
    return merged;
}
```

**Target shape** (extend with three new analyzers; replace short-circuit with full merge):
```csharp
public static IReadOnlyList<Diagnostic> BuildAll(
    ParseResult result, string source, StdlibSymbolIndex stdlib)
{
    var parseDiags   = DiagnosticsPublisher.BuildDiagnostics(result.Errors);
    var lintDiags    = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, source);          // D-03 now default-on
    var unusedDiags  = UnusedImportAnalyzer.Analyze(result.Ast, result.Tokens, source, stdlib);  // NEW
    var unreachDiags = UnreachableSectionAnalyzer.Analyze(result.Ast, result.Tokens, source);    // NEW
    var shadowDiags  = ShadowedVariableAnalyzer.Analyze(result.Ast, result.Tokens, source);      // NEW
    var merged = new List<Diagnostic>(
        parseDiags.Count + lintDiags.Count + unusedDiags.Count + unreachDiags.Count + shadowDiags.Count);
    merged.AddRange(parseDiags);
    merged.AddRange(lintDiags);
    merged.AddRange(unusedDiags);
    merged.AddRange(unreachDiags);
    merged.AddRange(shadowDiags);
    return merged;
}
```

**MUST preserve** (CombinedDiagnosticsPublisher.cs:23-28 docblock):
- The instance `Publish` method MUST call `PublishDiagnostics` UNCONDITIONALLY (no `if (merged.Count > 0)` guard) — empty publish is the only way to clear stale squiggles
- Caller of `Publish` (DocumentManager onParse callback) needs the new `StdlibSymbolIndex` parameter wired in — verify in `flow-lsp/Program.cs` DI setup

---

### `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` (analyzer, request-response) — MODIFY (D-03)

**Change scope (one-line + docblock update):**

**Remove the D-19 short-circuit** at ScaleLintAnalyzer.cs:42-43:
```csharp
// DELETE these two lines:
if (!ast.Pragmas.Has("scaleLint"))
    return Array.Empty<Diagnostic>();
```

Analyzer now always runs. The `enable scaleLint;` pragma remains a no-op (D-03 keeps it in `PragmaRegistry.KnownPragmas` so v1.3 scripts that declare it don't error out). The `Source = "flow.scaleLint"` diagnostic source string stays UNCHANGED (D-05).

Update class-level docblock (lines 14-28) to reflect the policy flip — keep the historical Phase 24 D-19/D-21/D-22 references for context but mark D-19 as superseded by Phase 31 D-03.

---

### `flow-lsp/Handlers/CompletionHandler.cs` (handler, request-response) — MODIFY (REQ-2)

**Analog:** self (extend `BuildItems` at line 93)

**Current 5-source merge** (CompletionHandler.cs:121-125):
```csharp
return builtIns.Items()
    .Concat(stdlib.Items())
    .Concat(users.CompletionsFor(uri))
    .Concat(keywords.Items())
    .Concat(SnippetTemplates());
```

**Target shape with 3 filters** (RESEARCH §Code Examples lines 655-666):
```csharp
var merged = builtIns.Items()
    .Concat(stdlib.Items())
    .Concat(users.CompletionsFor(uri))
    .Concat(keywords.Items())
    .Concat(SnippetTemplates());

if (ast is not null)
{
    merged = FilterByImports(merged, ast, stdlib);                              // REQ-2a
    merged = FilterByPragmas(merged, ast.Pragmas);                              // REQ-2b
    if (tokens is not null)
        merged = BoostByMusicalContext(merged, ast, tokens, text, cursor);      // REQ-2c
}

return merged;
```

**Each filter is a `public static` helper** (mirrors the existing `SnippetTemplates`, `IsInsideUseStringLiteral`, `RomanNumeralItems` discipline at lines 65-191) — pure transforms exposed for unit tests without OmniSharp transport.

**FilterByImports excerpt** (RESEARCH §Code Examples lines 668-686):
```csharp
public static IEnumerable<CompletionItem> FilterByImports(
    IEnumerable<CompletionItem> items, FlowProgram ast, StdlibSymbolIndex stdlib)
{
    var importedModules = new HashSet<string>(
        ast.Statements.OfType<ImportStatement>()
           .Select(i => ExtractModuleName(i.FilePath)));
    if (importedModules.Contains("std"))
        importedModules.UnionWith(StdlibSymbolIndex.ModuleNames);     // line 26-27 in StdlibSymbolIndex

    return items.Where(item =>
    {
        var proc = stdlib.Find(item.Label);
        if (proc is null) return true;                                 // builtins/keywords/snippets pass through
        return importedModules.Contains(proc.Module);
    });
}
```

`StdProc` record at `StdlibSymbolIndex.cs:29` already exposes `Module` — no plumbing needed there.

**Note-stream branch unchanged** (CompletionHandler.cs:107-116) — D-11 keeps the note-stream completion list isolated from the default merge; the new filters apply to the default branch only.

---

### `flow-lsp/Handlers/HoverHandler.cs` (handler, request-response) — MODIFY (REQ-3)

**Analog:** self (extend `BuildHover` at line 46)

**Current built-in branch** (HoverHandler.cs:51-63):
```csharp
var b = builtIns.Find(identifier);
if (b is not null)
{
    var doc = BuiltInDocs.TryGet(identifier);
    var signature = b.Signatures.Count > 0 ? b.Signatures[0].ToString() : identifier;
    // ...
}
```

`b.Signatures[0].ToString()` calls `FunctionSignature.ToString()` (FunctionSignature.cs:11-18), which currently emits `"..."` for varargs. Phase 31 D-01 mandates U+2026.

**Target shape** — replace `.ToString()` with the new `LspMappings.FormatSignature(b.Signatures[0])`:
```csharp
var signature = b.Signatures.Count > 0
    ? LspMappings.FormatSignature(b.Signatures[0])      // NEW: emits "concat(String…)" with U+2026
    : identifier;
```

`flow-lang` stays untouched (Phase 24 D-04 policy — "zero flow-lang touch for LSP-only work"). The LSP-side formatter is the missing layer.

---

### `flow-lsp/Handlers/SignatureHelpHandler.cs` (handler, request-response) — MODIFY (REQ-3)

**Analog:** self (extend the `Handle` method at line 72-91)

**Current label assignment** (SignatureHelpHandler.cs:80-83):
```csharp
var sig = new SignatureInformation
{
    Label = b.Signatures.Count > 0 ? b.Signatures[0].ToString() : ctx.FunctionName,
    Parameters = new Container<ParameterInformation>()
};
```

**Target shape** — same swap as HoverHandler plus populate `Parameters` per Pitfall 3:
```csharp
var sig = new SignatureInformation
{
    Label = b.Signatures.Count > 0
        ? LspMappings.FormatSignature(b.Signatures[0])
        : ctx.FunctionName,
    Parameters = LspMappings.BuildParameters(b.Signatures[0])    // NEW: explicit ParameterInformation array
};
```

**Why `Parameters` matters (RESEARCH Pitfall 3, lines 519-527):** `…` U+2026 is 3 bytes in UTF-8 / 1 grapheme. LSP clients compute `ActiveParameter` offsets in UTF-16 code units — both VSCode and IntelliJ are consistent there, but the safer path is to use `SignatureInformation.Parameters` (explicit `ParameterInformation` ranges) instead of relying on string offsets inside the merged label. Add a Phase 31 unit test that pins the active-parameter highlight when cursor is past the varargs ellipsis position.

---

### `flow-lsp/LspMappings.cs` (utility, transform) — MODIFY (REQ-3)

**Analog:** self (existing `ToRange` + `ToSeverity` at lines 21-34 set the static-helper convention)

**Add `FormatSignature` helper** (RESEARCH §Code Examples lines 693-709):
```csharp
/// <summary>
/// Format a FunctionSignature for hover / signature-help / completion-tooltip.
/// Variadic params render with U+2026 (`…`) trailing the param type per D-01/D-02.
/// flow-lang stays untouched (Phase 24 D-04 policy) — the LSP-side formatter is
/// the missing layer; FunctionSignature.ToString() still emits "..." for runtime use.
/// </summary>
public static string FormatSignature(FunctionSignature sig)
{
    var inputs = sig.InputTypes.Select((t, i) =>
        sig.IsVarArgs && i == sig.InputTypes.Count - 1
            ? $"{t}…"   // U+2026 horizontal ellipsis
            : $"{t}");
    return $"{sig.Name}({string.Join(", ", inputs)})";
}
```

**Add `BuildParameters` helper** (new — emit explicit `ParameterInformation` array for `SignatureInformation.Parameters`):
```csharp
public static Container<ParameterInformation> BuildParameters(FunctionSignature sig)
{
    var list = new List<ParameterInformation>();
    for (int i = 0; i < sig.InputTypes.Count; i++)
    {
        var typeStr = sig.IsVarArgs && i == sig.InputTypes.Count - 1
            ? $"{sig.InputTypes[i]}…"
            : $"{sig.InputTypes[i]}";
        list.Add(new ParameterInformation { Label = new ParameterInformationLabel(typeStr) });
    }
    return new Container<ParameterInformation>(list);
}
```

`FunctionSignature.IsVarArgs` is at `flow-lang/TypeSystem/FunctionSignature.cs:9`.

---

### `flow-lsp/Symbols/StdlibSymbolIndex.cs` (symbol-index, request-response) — MODIFY (REQ-2)

**Analog:** self (extend with one helper)

**Current shape** (StdlibSymbolIndex.cs:29, 62-63):
```csharp
public sealed record StdProc(string Name, string Module, string FilePath);
private readonly Dictionary<string, StdProc> _byName = new();
public StdProc? Find(string name) =>
    _byName.TryGetValue(name, out var p) ? p : null;
```

**Add reverse-lookup helper** (needed by `UnusedImportAnalyzer` + `FilterByImports`):
```csharp
/// <summary>
/// Returns every stdlib proc declared in <paramref name="moduleName"/>
/// (e.g. "harmony", "audio", "std"). Used by Phase 31 UnusedImportAnalyzer
/// to determine whether a `use "@harmony"` actually has any referenced procs,
/// and by CompletionHandler.FilterByImports to drop suggestions from
/// non-imported modules.
/// </summary>
public IEnumerable<StdProc> ProcsForModule(string moduleName)
{
    foreach (var p in _byName.Values)
        if (p.Module == moduleName)
            yield return p;
}
```

---

### `flow-lang/Lexing/SimpleLexer.cs` (lexer, transform) — MODIFY (REQ-4)

**Analog:** self (mirror the existing `Note:` arm at line 1144)

**Existing `Note:` arm** (SimpleLexer.cs:1144-1151):
```csharp
else if (c == 'N' && IsStartOfLineContent() && _source.Substring(_position).StartsWith("Note:"))
{
    // Skip comment until end of line
    while (!IsAtEnd() && Peek() != '\n')
    {
        Advance();
    }
}
```

`IsStartOfLineContent()` at SimpleLexer.cs:1159-1169 returns true iff all preceding characters on the current line are whitespace — this is the column-0-with-optional-leading-whitespace gate.

**Add three NEW arms** in `SkipWhitespaceAndComments` (RESEARCH §Code Examples lines 726-738; insert AFTER the existing `Note:` arm and BEFORE the `else break`):

```csharp
// Phase 31 REQ-4: position-sensitive `;` Lisp-style line comment (Option A — D-11 lock).
// `;` at column-0 (with optional leading whitespace) → comment to end-of-line.
// `;` mid-line → still a TokenType.Semicolon used as statement terminator.
// Verified zero column-0 `;` exist in any in-repo .flow file (RESEARCH §Migration table).
else if (c == ';' && IsStartOfLineContent())
{
    while (!IsAtEnd() && Peek() != '\n') Advance();
}
// Phase 31 REQ-4: `TODO:` lead-in (mirrors existing `Note:` arm above).
else if (c == 'T' && IsStartOfLineContent() && _source.Substring(_position).StartsWith("TODO:"))
{
    while (!IsAtEnd() && Peek() != '\n') Advance();
}
// Phase 31 REQ-4: `FIXME:` lead-in.
else if (c == 'F' && IsStartOfLineContent() && _source.Substring(_position).StartsWith("FIXME:"))
{
    while (!IsAtEnd() && Peek() != '\n') Advance();
}
```

**Critical: `Note:` is ALREADY shipping** (RESEARCH §Summary finding 1). The lexer line 1144 arm is the existing pattern — Phase 31 only adds three new arms and only the TextMate grammar side gets the `Note:` comment scope.

**String-literal context unaffected** (RESEARCH Pitfall 8): `SkipWhitespaceAndComments` only runs between tokens, never inside `ScanString` — the existing `Note:` arm is correctly placed and the new arms follow verbatim.

---

### `flow-lang/Lexing/PragmaRegistry.cs` (config, static-data) — MODIFY (D-03)

**Analog:** self (the `KnownPragmas` dict at line 16-24)

**Current entry for scaleLint** (PragmaRegistry.cs:23):
```csharp
["scaleLint"] = "Inside `key { ... }` blocks, surface non-diatonic notes as Information-severity LSP diagnostics."
```

**Target shape** — keep the entry (Phase 31 D-03 keeps the pragma as a recognized no-op), update the description to reflect the new default-on behavior:
```csharp
["scaleLint"] = "Phase 31 D-03: scale-lint is now default-on; this pragma is accepted as a no-op for v1.3 backward compat."
```

No other registry changes. The closed-set design (PragmaRegistry.cs:11) keeps unknown pragma names surfacing the D-12 error.

---

### `flow-cli/Commands/LspCommand.cs` (controller, event-driven subcommand) — NEW

**Analog:** `flow-cli/Commands/ReplCommand.cs` (closest — thin wrapper subcommand)

**Imports pattern** (ReplCommand.cs:1-4):
```csharp
using System.CommandLine;
using FlowInterpreter;

namespace FlowCli.Commands;
```

For `LspCommand` the import becomes `using FlowLsp;` and the entry-point invocation matches the existing `flow-lsp` `Program.Main` shape.

**Builder shape** (ReplCommand.cs:10-22):
```csharp
internal static class ReplCommand
{
    public static Command Build()
    {
        var cmd = new Command("repl", "Start the interactive Flow REPL");
        cmd.SetAction(parseResult =>
        {
            new Repl().Run();
            return 0;
        });
        return cmd;
    }
}
```

**Target shape for `LspCommand`** (one-paragraph description, single action):
```csharp
internal static class LspCommand
{
    public static Command Build()
    {
        var cmd = new Command("lsp", "Start the Flow Language Server (stdio LSP)");
        cmd.SetAction(parseResult =>
        {
            // Delegate to flow-lsp's existing Program.Main entry point —
            // see flow-lsp/Program.cs for the OmniSharp LanguageServer.From() wiring.
            return FlowLsp.Program.Main(Array.Empty<string>()).GetAwaiter().GetResult();
        });
        return cmd;
    }
}
```

**Resolves RESEARCH Pitfall 7** (binary discoverability from JetBrains plugin). With `flow lsp` registered, `FlowLanguageServerFactory.kt` invokes `GeneralCommandLine("flow", "lsp")` and the user's existing `flow install` (Phase 30) gives discoverability for free.

---

### `flow-cli/Commands/CommandRegistry.cs` (config, static-data) — MODIFY

**Analog:** self (the `BuildAllCommands` method at line 13-29)

**Current shape** (CommandRegistry.cs:13-29):
```csharp
return new[]
{
    RunCommand.Build(),
    EvalCommand.Build(),
    ReplCommand.Build(),
    WatchCommand.Build(),
    PlayCommand.Build(),
    RenderCommand.Build(),
    Flow2MidiCommand.Build(),
    Midi2FlowCommand.Build(),
    CheckCommand.Build(),
    VersionCommand.Build(),
    NewCommand.Build(),
};
```

**Target shape** — insert one row (placement at end of group is consistent with Phase 30 plan conventions; alphabetical-by-alphabetic-similar-position works too — planner's call):
```csharp
return new[]
{
    RunCommand.Build(),
    EvalCommand.Build(),
    ReplCommand.Build(),
    WatchCommand.Build(),
    PlayCommand.Build(),
    RenderCommand.Build(),
    Flow2MidiCommand.Build(),
    Midi2FlowCommand.Build(),
    CheckCommand.Build(),
    VersionCommand.Build(),
    NewCommand.Build(),
    LspCommand.Build(),     // NEW — Phase 31 REQ-7 support
};
```

Update the leading comment block at CommandRegistry.cs:5-10 to mention "12 subcommands; Phase 31 adds LspCommand."

---

### `vscode-extension/syntaxes/flow.tmLanguage.json` (config grammar, declarative) — MODIFY

**Analog:** self (the `#comments` repository node at line 19-26 and the patterns ordering at lines 6-17)

**Current comments node** (flow.tmLanguage.json:19-26):
```json
"comments": {
  "patterns": [
    {
      "name": "comment.line.double-slash.flow",
      "match": "//.*$"
    }
  ]
}
```

**Target shape** — extend per D-07 + RESEARCH §Code Examples lines 749-757:
```json
"comments": {
  "patterns": [
    { "name": "comment.line.double-slash.flow",  "match": "//.*$" },
    { "name": "comment.line.semicolon.flow",     "match": "^\\s*;.*$" },
    { "name": "comment.line.todo.flow",          "match": "^\\s*TODO:.*$" },
    { "name": "comment.line.fixme.flow",         "match": "^\\s*FIXME:.*$" },
    { "name": "comment.line.documentation.flow", "match": "^\\s*Note:.*$" }
  ]
}
```

The `^\\s*` anchor mirrors the lexer's `IsStartOfLineContent()` gate.

**Add `function-call` and `variable-ref` repository nodes** (RESEARCH §Code Examples lines 758-765):
```json
"function-call": {
  "match": "\\b([A-Za-z_][A-Za-z0-9_]*)(?=\\s*\\()",
  "captures": { "1": { "name": "entity.name.function.flow" } }
},
"variable-ref": {
  "match": "\\b[A-Za-z_][A-Za-z0-9_]*\\b",
  "name": "variable.other.flow"
}
```

**Update top-level `patterns` array** (flow.tmLanguage.json:6-17) to include the new nodes in the correct order — `function-call` MUST precede `variable-ref`, and both go AFTER the music-specific patterns (chords, notes, types, booleans, keywords) so those keep their precedence:
```json
"patterns": [
  { "include": "#comments" },
  { "include": "#strings" },
  { "include": "#chords" },
  { "include": "#notes" },
  { "include": "#numbers" },
  { "include": "#keywords" },
  { "include": "#types" },
  { "include": "#booleans" },
  { "include": "#function-call" },
  { "include": "#variable-ref" },
  { "include": "#operators" },
  { "include": "#pipes" }
]
```

**Pitfall 4 mitigation:** After saving the JSON, run `cd vscode-extension && npm run test:grammar:update` and commit the regenerated `.snap` files alongside.

---

### `vscode-extension/tests/grammar/comment-forms.flow` + `.snap` (test fixture, declarative) — NEW

**Analog:** `vscode-extension/tests/grammar/sample.flow` (existing 19-line .flow fixture)

**Sample analog content** (sample.flow:1-19):
```
// Sample .flow fixture exercising every grammar category.
use "@std";
use "@audio";

proc main () {
    Int x = 42;
    Float bpm = 120.0;
    String msg = "hello";
    Bool flag = true;
    ...
}
```

**Target content** for `comment-forms.flow` — exercise all four new comment forms:
```
// Existing double-slash comment.
; Lisp-style line comment at column 0.
  ; Indented Lisp-style comment.
Note: Documentation comment chapter divider.
TODO: Fix the foo handling.
FIXME: This is broken.
proc main () {
    (print "TODO: this is a string, not a comment");
    Int x = 5;
}
```

Snapshot regen via `cd vscode-extension && npm run test:grammar:update` produces `.snap` (see sample.flow.snap pattern at lines 1-40 — each line annotated with `#^^^` ranges and scope chain `source.flow <scope>.flow`).

---

### `vscode-extension/tests/grammar/function-calls.flow` + `.snap` (test fixture, declarative) — NEW

**Analog:** `vscode-extension/tests/grammar/sample.flow`

**Target content** — exercise function-call vs variable-ref distinction:
```
proc demo () {
    Int x = 5;
    (print x);
    (mul x 2);
    Int y = (add x 3);
    x -> doubler;
}
```

Expected snapshot scopes:
- `print`, `mul`, `add`, `doubler` → `entity.name.function.flow` (head of `(name …)` form)
- `x`, `y` → `variable.other.flow` (bare identifier reference)
- `Int` → `storage.type.flow` (existing)

---

### `flow-jetbrains/build.gradle.kts` (config, declarative) — NEW

**Analog:** NONE in-repo. Use RESEARCH §Code Examples lines 788-826 (LSP4IJ DeveloperGuide.md + redhat-developer/lsp4ij upstream build.gradle.kts).

**Pattern to copy** (RESEARCH §Code Examples lines 789-826):
```kotlin
plugins {
    id("java")
    id("org.jetbrains.kotlin.jvm") version "1.9.25"
    id("org.jetbrains.intellij.platform") version "2.2.0"
}

group = "dev.flowlang"
version = "0.1.0"

repositories {
    mavenCentral()
    intellijPlatform { defaultRepositories() }
}

dependencies {
    intellijPlatform {
        intellijIdeaCommunity("2024.2")  // matches LSP4IJ since-build 242
        plugin("com.redhat.devtools.lsp4ij:0.19.3")  // D-09 pin (RESEARCH locked)
    }
}

kotlin {
    jvmToolchain(21)
}

tasks.withType<JavaCompile> {
    options.release.set(17)
}

intellijPlatform {
    pluginConfiguration {
        ideaVersion {
            sinceBuild = "242"
        }
    }
}
```

---

### `flow-jetbrains/settings.gradle.kts` (config, declarative) — NEW

**Analog:** NONE. Use minimal Gradle settings:
```kotlin
rootProject.name = "flow-jetbrains"
```

---

### `flow-jetbrains/gradle.properties` (config, declarative) — NEW

**Analog:** NONE. Use:
```properties
org.gradle.jvmargs=-Xmx2g -Dfile.encoding=UTF-8
kotlin.code.style=official
```

---

### `flow-jetbrains/src/main/resources/META-INF/plugin.xml` (config, declarative) — NEW

**Analog:** NONE in-repo. Use RESEARCH §Code Examples lines 391-414:

```xml
<idea-plugin>
    <id>dev.flowlang.jetbrains</id>
    <name>Flow Language</name>
    <vendor>Flow Language</vendor>
    <version>0.1.0</version>
    <idea-version since-build="242"/>
    <depends>com.intellij.modules.platform</depends>
    <depends>com.redhat.devtools.lsp4ij</depends>

    <extensions defaultExtensionNs="com.intellij">
        <fileType name="Flow" implementationClass="com.intellij.openapi.fileTypes.LanguageFileType"
                  fieldName="INSTANCE" language="Flow" extensions="flow"/>
    </extensions>

    <extensions defaultExtensionNs="com.redhat.devtools.lsp4ij">
        <server id="flow"
                name="Flow Language Server"
                factoryClass="dev.flowlang.jetbrains.FlowLanguageServerFactory">
            <description><![CDATA[Flow LSP via flow-lsp binary]]></description>
        </server>
        <languageMapping language="Flow" serverId="flow"/>
    </extensions>
</idea-plugin>
```

---

### `flow-jetbrains/src/main/kotlin/dev/flowlang/jetbrains/FlowLanguageServerFactory.kt` (controller, event-driven) — NEW

**Analog:** NONE in-repo. Use RESEARCH §Code Examples lines 369-388:

```kotlin
package dev.flowlang.jetbrains

import com.intellij.execution.configurations.GeneralCommandLine
import com.intellij.openapi.project.Project
import com.redhat.devtools.lsp4ij.LanguageServerFactory
import com.redhat.devtools.lsp4ij.server.OSProcessStreamConnectionProvider
import com.redhat.devtools.lsp4ij.server.StreamConnectionProvider

class FlowLanguageServerFactory : LanguageServerFactory {
    override fun createConnectionProvider(project: Project): StreamConnectionProvider {
        // Phase 31 + Phase 30 integration: `flow lsp` subcommand on PATH (added by `flow install`).
        // The flow-cli/Commands/LspCommand.cs entry point delegates to flow-lsp's Program.Main.
        val cmd = GeneralCommandLine("flow", "lsp")
        return object : OSProcessStreamConnectionProvider() {
            init { commandLine = cmd }
        }
    }
}
```

**Pitfall 7 resolution:** Use `GeneralCommandLine("flow", "lsp")` (NOT `"flow-lsp"` directly) — the new `LspCommand` subcommand is the discoverability path. Document `FLOW_LSP_PATH` env-var fallback in `flow-jetbrains/README.md` (deferred shipping concern).

---

### `flow-lang.Tests/Unit/Phase31/UnusedImportAnalyzerFacts.cs` (test, request-response) — NEW

**Analog:** `flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerFacts.cs`

**Imports pattern** (ScaleLintAnalyzerFacts.cs:1-6):
```csharp
using FlowLang.Tests.Unit.Phase17;
using FlowLsp.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase24;
```

For Phase 31: change namespace to `FlowLang.Tests.Unit.Phase31`; reuse `FlowLang.Tests.Unit.Phase17.LspFixtures` (no need to recreate).

**Class shape** (ScaleLintAnalyzerFacts.cs:22-37):
```csharp
public class ScaleLintAnalyzerFacts
{
    [Fact]
    public void NonDiatonic_FsharpInCmajor_FlagsOneDiagnostic()
    {
        var src = "enable scaleLint;\nkey Cmajor { | C4 D4 E4 F#4 G4 | }";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Single(diags);
        Assert.Equal(DiagnosticSeverity.Information, diags[0].Severity);
        Assert.Equal("flow.scaleLint", diags[0].Source);
        Assert.Contains("F#4", diags[0].Message);
        Assert.Contains("not diatonic in Cmajor", diags[0].Message);
    }
}
```

**LspFixtures helper** (LspFixtures.cs:9-13):
```csharp
public static class LspFixtures
{
    public static ParseResult Parse(string source, string? path = null) =>
        new ParseSession().Parse(source, path);
}
```

For UnusedImport tests, the analyzer takes an extra `StdlibSymbolIndex` arg — extend `LspFixtures` or instantiate locally (the existing Phase17 `HoverHandlerTests.Indices()` pattern at HoverHandlerTests.cs:18-23 is the precedent):
```csharp
private static StdlibSymbolIndex StdlibIndex() => new(new ParseSession());
```

**Skeleton tests per RESEARCH §Code Examples lines 838-860:**
```csharp
[Fact]
public void Unused_import_emits_warning_diagnostic()
{
    var src = "use \"@harmony\";\nproc main () { (print \"hi\") }";
    var result = LspFixtures.Parse(src);
    var stdlib = StdlibIndex();
    var diags = UnusedImportAnalyzer.Analyze(result.Ast, result.Tokens, src, stdlib);
    Assert.Single(diags);
    Assert.Equal(DiagnosticSeverity.Warning, diags[0].Severity);
    Assert.Equal("flow.unusedImport", diags[0].Source);
}

[Fact]
public void Used_import_emits_zero_diagnostics()
{
    var src = "use \"@harmony\";\nChord c = (arpeggio Cmaj 4);";
    var result = LspFixtures.Parse(src);
    var stdlib = StdlibIndex();
    var diags = UnusedImportAnalyzer.Analyze(result.Ast, result.Tokens, src, stdlib);
    Assert.Empty(diags);
}
```

---

### `flow-lang.Tests/Unit/Phase31/CompletionFilterFacts.cs` (test, request-response) — NEW

**Analog:** `flow-lang.Tests/Unit/Phase17/CompletionHandlerTests.cs`

**Imports + setup** (CompletionHandlerTests.cs:1-25):
```csharp
using System.Linq;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLsp;
using FlowLsp.Handlers;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

public class CompletionHandlerTests
{
    private static (BuiltInIndex bi, UserSymbolIndex ui, StdlibSymbolIndex si, KeywordIndex ki) MakeIndices()
    {
        var reg = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterSignaturesOnly(reg);
        var parser = new ParseSession();
        return (new BuiltInIndex(reg), new UserSymbolIndex(), new StdlibSymbolIndex(parser), new KeywordIndex());
    }
}
```

Copy `MakeIndices()` helper verbatim into Phase31 namespace (or expose a shared helper in `LspFixtures`).

**Existing test pattern using `BuildItems`** (CompletionHandlerTests.cs:44-61):
```csharp
[Fact]
public void Default_ReturnsBuiltInsKeywordsSnippets_IncludingAudioAndTransform()
{
    var (bi, ui, si, ki) = MakeIndices();
    var uri = DocumentUri.File("/t.flow");
    var text = "proc main ()\n  ";
    var result = LspFixtures.Parse(text);
    var items = CompletionHandler.BuildItems(
        uri, text, result.Ast, result.Tokens, new Position(1, 2), bi, ui, si, ki).ToList();

    Assert.Contains(items, i => i.Label == "proc");
    Assert.Contains(items, i => i.Label == "print");
    Assert.Contains(items, i => i.Label == "reverb");
    Assert.Contains(items, i => i.Label == "transpose");
    Assert.Contains(items, i => i.Label == "chordNotes");
}
```

**Phase 31 target tests** (REQ-2 acceptance):
```csharp
[Fact]
public void FilterByImports_DropsHarmonyProcs_WhenHarmonyNotImported()
{
    var (bi, ui, si, ki) = MakeIndices();
    var uri = DocumentUri.File("/no-harmony.flow");
    var text = "proc main ()\n  ";   // NO `use "@harmony"`
    var result = LspFixtures.Parse(text);
    var items = CompletionHandler.BuildItems(
        uri, text, result.Ast, result.Tokens, new Position(1, 2), bi, ui, si, ki).ToList();

    // arpeggio is from @harmony — should NOT be in the list
    Assert.DoesNotContain(items, i => i.Label == "arpeggio");
}

[Fact]
public void FilterByImports_AllowsHarmonyProcs_WhenHarmonyImported()
{
    var (bi, ui, si, ki) = MakeIndices();
    var uri = DocumentUri.File("/with-harmony.flow");
    var text = "use \"@harmony\"\nproc main ()\n  ";
    var result = LspFixtures.Parse(text);
    var items = CompletionHandler.BuildItems(
        uri, text, result.Ast, result.Tokens, new Position(2, 2), bi, ui, si, ki).ToList();

    Assert.Contains(items, i => i.Label == "arpeggio");
}
```

---

### `flow-lang.Tests/Unit/Phase31/VarargsRenderingFacts.cs` (test, request-response) — NEW

**Analog:** `flow-lang.Tests/Unit/Phase17/HoverHandlerTests.cs` + `flow-lang.Tests/Unit/Phase17/SignatureHelpHandlerTests.cs`

**Imports pattern** (HoverHandlerTests.cs:1-9):
```csharp
using FlowLang.StandardLibrary;
using FlowLsp;
using FlowLsp.Handlers;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
```

**Pattern: hover-by-builtin** (HoverHandlerTests.cs:26-33):
```csharp
[Fact]
public void BuiltIn_ShowsSignatureAndDoc()
{
    var (bi, ui, si) = Indices();
    var hover = HoverHandler.BuildHover("print", bi, ui, si, DocumentUri.File("/t.flow"));
    Assert.NotNull(hover);
    Assert.NotNull(hover!.Contents.MarkupContent);
    Assert.Contains("print", hover.Contents.MarkupContent!.Value);
}
```

**Phase 31 target test** (REQ-3 acceptance — varargs U+2026 visible in hover):
```csharp
[Fact]
public void Hover_VarargsBuiltin_RendersEllipsis()
{
    var (bi, ui, si) = Indices();
    var hover = HoverHandler.BuildHover("concat", bi, ui, si, DocumentUri.File("/t.flow"));
    Assert.NotNull(hover);
    var md = hover!.Contents.MarkupContent!.Value;
    Assert.Contains("…", md);   // U+2026 horizontal ellipsis
    Assert.DoesNotContain("...", md); // NOT three ASCII dots
}

[Fact]
public void FormatSignature_VarargsParam_UsesU2026()
{
    var sig = new FunctionSignature("concat",
        new[] { (FlowType)StringType.Instance }, IsVarArgs: true);
    var rendered = LspMappings.FormatSignature(sig);
    Assert.Equal("concat(String…)", rendered);
}
```

---

### `flow-lang.Tests/Unit/Phase31/Phase31LexerCommentFormsTests.cs` (test, transform) — NEW

**Analog:** No direct in-repo lexer-only test class for `SkipWhitespaceAndComments`. Closest: `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs` (Pitfall 1 mentions it as the regression canary). Pattern is generic xunit `[Fact]` against `new SimpleLexer(...).Tokenize()`.

**Required tests** (RESEARCH §Wave 0 + Pitfall 8):
```csharp
[Fact]
public void Semicolon_AtColumn0_IsLineComment()
{
    var tokens = Tokenize("; This is a Lisp comment\nproc main ()");
    // No Semicolon token; first token is 'proc' keyword
    Assert.DoesNotContain(tokens, t => t.Type == TokenType.Semicolon);
}

[Fact]
public void Semicolon_MidLine_IsStillSemicolonToken()
{
    var tokens = Tokenize("Int x = 5;");
    // Semicolon survives as statement terminator
    Assert.Contains(tokens, t => t.Type == TokenType.Semicolon);
}

[Fact]
public void TODO_AtColumn0_IsLineComment()
{
    var tokens = Tokenize("TODO: fix this\nproc main ()");
    // No Identifier "TODO" or Colon tokens
    Assert.DoesNotContain(tokens, t => t.Text == "TODO");
}

[Fact]
public void TODO_InsideString_IsNotComment()
{
    var tokens = Tokenize("(print \"TODO: hello\")");
    Assert.Contains(tokens, t => t.Type == TokenType.StringLiteral && t.Text.Contains("TODO"));
}

[Fact]
public void EnableHAsB_StillParses_OptionACanary()
{
    // Pitfall 1 warning sign: ensure existing pragmas don't lex as comments
    var tokens = Tokenize("enable hAsB;\nproc main ()");
    Assert.Contains(tokens, t => t.Type == TokenType.Semicolon);  // semicolon survives
}
```

Helper:
```csharp
private static List<Token> Tokenize(string src)
{
    var er = new ErrorReporter();
    return new SimpleLexer(src, er, null, PragmaSet.Empty).Tokenize();
}
```

---

## Shared Patterns

### Analyzer "never throws" contract (Phase 24 D-04)
**Source:** `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs:14-29` (class docblock) + `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs:36-48` (`Analyze` shape)
**Apply to:** All three new analyzers (UnusedImport, UnreachableSection, ShadowedVariable)
```csharp
// Pure read-only AST + token traversal; never throws, never publishes
// (returns IReadOnlyList<Diagnostic>). Wave 0 wires this into CombinedDiagnosticsPublisher.
public static class XxxAnalyzer
{
    public static IReadOnlyList<Diagnostic> Analyze(
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source /* + optional StdlibSymbolIndex */ )
    {
        // Charitable fail-open: defensive null checks; return Array.Empty on malformed AST
        if (ast?.Statements is null) return Array.Empty<Diagnostic>();
        var diagnostics = new List<Diagnostic>();
        // walk + collect
        return diagnostics;
    }
}
```

### Diagnostic source-string convention (Phase 24 D-18, preserved by Phase 31 D-05)
**Source:** `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs:289` + `flow-lsp/Handlers/DiagnosticsPublisher.cs:42`
**Apply to:** All new analyzers — dotted suffix lets editors filter independently
```csharp
new Diagnostic
{
    Severity = DiagnosticSeverity.Warning,    // or .Information per requirement
    Source = "flow.unusedImport",              // dotted: "flow.<analyzerKind>"
    Message = $"Unused import: \"{path}\"",
    Range = LspMappings.ToRange(loc)
}
```

### Pure-static-helpers for test access (Phase 17 idiom)
**Source:** `flow-lsp/Handlers/CompletionHandler.cs:93` (`BuildItems`); `flow-lsp/Handlers/HoverHandler.cs:46` (`BuildHover`); `flow-lsp/Handlers/SignatureHelpHandler.cs:40` (`DetectCall`); `flow-lsp/Diagnostics/CombinedDiagnosticsPublisher.cs:48` (`BuildAll`)
**Apply to:** Every new code path in Phase 31; the 10-second test-runtime-budget constraint depends on this
```csharp
// Every handler exposes a pure static method that takes primitive inputs and
// returns the LSP wire shape. Unit tests call the static method directly without
// constructing an ILanguageServerFacade.
public static IEnumerable<CompletionItem> FilterByImports(
    IEnumerable<CompletionItem> items, FlowProgram ast, StdlibSymbolIndex stdlib) { ... }
```

### Test shape (Phase 24 Facts class + Phase 17 `LspFixtures.Parse`)
**Source:** `flow-lang.Tests/Unit/Phase17/LspFixtures.cs:9-13` + `flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerFacts.cs:22-37`
**Apply to:** All six new Phase 31 Facts files
```csharp
using FlowLang.Tests.Unit.Phase17;       // for LspFixtures
using FlowLsp.Diagnostics;               // (or .Handlers for handler tests)
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase31;

public class XxxAnalyzerFacts
{
    [Fact]
    public void Behavior_Setup_AssertOutcome()
    {
        var src = "...";
        var result = LspFixtures.Parse(src);
        var diags = XxxAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Single(diags);
        Assert.Equal(DiagnosticSeverity.Warning, diags[0].Severity);
        Assert.Equal("flow.xxx", diags[0].Source);
    }
}
```

### Subcommand registration (Phase 30 pattern)
**Source:** `flow-cli/Commands/ReplCommand.cs:10-22` + `flow-cli/Commands/CommandRegistry.cs:13-29`
**Apply to:** New `LspCommand.cs` plus single-row insertion into `CommandRegistry.BuildAllCommands()`
```csharp
internal static class XxxCommand
{
    public static Command Build()
    {
        var cmd = new Command("xxx", "<one-line description>");
        cmd.SetAction(parseResult =>
        {
            // delegate to existing entry point
            return 0;
        });
        return cmd;
    }
}
```

### TextMate grammar snapshot regen (Phase 17)
**Source:** `vscode-extension/package.json` (`test:grammar:update` script) + `vscode-extension/tests/grammar/sample.flow.snap`
**Apply to:** Any task that touches `vscode-extension/syntaxes/flow.tmLanguage.json`
- After grammar edit: `cd vscode-extension && npm run test:grammar:update`
- Commit regenerated `.snap` files alongside grammar change
- New fixtures (`comment-forms.flow`, `function-calls.flow`) get fresh `.snap` files in the same step

### LSP4IJ scaffolding (Phase 31 first-of-kind — RESEARCH §6)
**Source:** RESEARCH.md §Code Examples lines 369-414 (DeveloperGuide.md adapted) + RESEARCH.md §Standard Stack lines 102-110
**Apply to:** All five `flow-jetbrains/` files (build.gradle.kts, settings.gradle.kts, gradle.properties, plugin.xml, FlowLanguageServerFactory.kt)
- Package namespace: `dev.flowlang.jetbrains` (RESEARCH Open Question 4 recommendation)
- LSP4IJ pin: `com.redhat.devtools.lsp4ij:0.19.3`
- IntelliJ Platform: `2024.2`, sinceBuild `242`
- Binary discovery: `GeneralCommandLine("flow", "lsp")` — assumes Phase 31 ships the new `LspCommand` subcommand (planner task ordering: `LspCommand.cs` MUST land before `FlowLanguageServerFactory.kt` for the stretch UAT to work)

## No Analog Found

Files with no close in-repo match — planner uses RESEARCH.md exemplars directly:

| File | Role | Data Flow | Reason | Use Instead |
|------|------|-----------|--------|-------------|
| `flow-jetbrains/build.gradle.kts` | config | declarative | First Gradle/Kotlin file in the repo | RESEARCH §Code Examples lines 788-826 |
| `flow-jetbrains/settings.gradle.kts` | config | declarative | Same | Minimal scaffolding (one-line: `rootProject.name = "flow-jetbrains"`) |
| `flow-jetbrains/gradle.properties` | config | declarative | Same | Standard JVM args + Kotlin style |
| `flow-jetbrains/src/main/resources/META-INF/plugin.xml` | config | declarative | First IntelliJ plugin descriptor | RESEARCH §Code Examples lines 391-414 |
| `flow-jetbrains/src/main/kotlin/dev/flowlang/jetbrains/FlowLanguageServerFactory.kt` | controller | event-driven | First Kotlin file; first LSP4IJ integration | RESEARCH §Code Examples lines 369-388 |

## Migration Audit (REQ-6)

RESEARCH §Migration table confirms: **zero in-repo .flow files need migration** under recommended Option A (position-sensitive `;`).

| File | Audit Result | Action |
|------|--------------|--------|
| `examples/tutorial.flow:224` | Existing `Note:` at column 0 — already a comment under shipped lexer (SimpleLexer.cs:1144) | none |
| `examples/showcase.flow` | No column-0 `;` / `TODO:` / `FIXME:` | none |
| `examples/pragmas/h_alias.flow` | Bare `enable hAsB;` mid-line — Option A preserves | none |
| `examples/pragmas/microtonal_ji.flow` | Same as above | none |
| `tests/test_*.flow` (70+ files) | 17 mid-line `;` usages — all preserved under Option A | none |
| `tests/test_unpack_flow.flow:8,10` | `Note:` is mid-line type annotation OR already a column-0 comment | none |
| `tests/std.flow:33` | `Note:` is mid-line type annotation | none |
| Phase 28 ragtime fixtures | No collisions | none |

**Verification commands:**
```bash
# Confirm no column-0 `;` outside strings/comments:
grep -nE "^\s*;" examples/**/*.flow tests/**/*.flow flow-lang/*.flow | grep -v "^[^:]*:[^:]*://"
# Confirm no column-0 `TODO:` / `FIXME:`:
grep -nE "^\s*(TODO|FIXME):" examples/**/*.flow tests/**/*.flow flow-lang/*.flow
```

Both grep commands MUST return zero hits before planner closes REQ-6.

## Metadata

**Analog search scope:**
- `flow-lsp/Diagnostics/` (5 files), `flow-lsp/Handlers/` (7 files), `flow-lsp/Symbols/` (4 files)
- `flow-lang/Lexing/` (5 files), `flow-lang/Ast/Statements/` (13 files)
- `flow-cli/Commands/` (11 files)
- `vscode-extension/syntaxes/` (1 file), `vscode-extension/tests/grammar/` (8 files)
- `flow-lang.Tests/Unit/Phase17/` (19 files), `flow-lang.Tests/Unit/Phase24/` (5 files)

**Files scanned:** 78 (read fully or via targeted grep + targeted Read)

**Pattern extraction date:** 2026-05-11

**Cross-cutting constraints honored:**
- Phase 24 D-04 (zero flow-lang touch for LSP-only work): the SimpleLexer change is the ONLY flow-lang touch this phase; all other new files live under `flow-lsp/`, `flow-cli/`, `vscode-extension/`, `flow-jetbrains/`, or `flow-lang.Tests/`
- Phase 24 D-18 / Phase 31 D-05 (diagnostic source string convention): dotted `"flow.<analyzerKind>"` per new analyzer
- Phase 17 D-04 hybrid TextMate + LSP semantic tokens: grammar gets the new comment + function-call scopes; semantic-token contribution remains optional for v1.5
- Pre-public lean: lexer break is acceptable per `project_pre_public_no_legacy_burden`; REQ-6 migration is zero files under Option A
- Two-run byte-identical determinism: Option A `;` change emits zero new tokens for any existing valid program → Phase 18/25/27/28 ByteIdentical gates pass by construction
