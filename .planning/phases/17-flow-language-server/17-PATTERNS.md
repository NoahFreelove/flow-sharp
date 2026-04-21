# Phase 17: Flow Language Server - Pattern Map

**Mapped:** 2026-04-20
**Files analyzed:** 28 (26 new + 2 modified/augmented)
**Analogs found:** 12 of 28 have strong in-repo analogs; 16 are net-new idioms (LSP handlers, TypeScript, TextMate, CI YAML, bash)

## File Classification

### C# server side (under `flow-lsp/`)

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `flow-lsp/flow-lsp.csproj` | csproj | config | `flow-interpreter/flow-interpreter.csproj` | exact role, extended with publish props |
| `flow-lsp/Program.cs` | server entrypoint | request-response | `flow-interpreter/Program.cs` (Main + arg parsing); `flow-lang/Core/FlowEngine.cs` (DI wiring) | role-match; LSP.From() shape is NEW IDIOM |
| `flow-lsp/ParseSession.cs` | service | request-response | `flow-lang/Core/FlowEngine.cs` §Execute (lex→parse→report) | role-match (stripped-down engine without audio/interpreter) |
| `flow-lsp/DocumentManager.cs` | service | event-driven | NEW IDIOM — no prior `CancellationTokenSource`-per-key Dictionary pattern in repo |
| `flow-lsp/LspMappings.cs` | utility | transform | NEW IDIOM — `SourceLocation` ↔ `Range` conversion; consumes `flow-lang/Core/SourceLocation.cs` + `Diagnostics/DiagnosticLevel.cs` |
| `flow-lsp/Symbols/BuiltInIndex.cs` | service | batch | `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` (exposes the name→overloads map to walk) |
| `flow-lsp/Symbols/UserSymbolIndex.cs` | service | batch | NEW IDIOM — AST visitor; closest shape is switch-dispatch over AST records used throughout `flow-lang/Interpreter/ExpressionEvaluator.cs` |
| `flow-lsp/Symbols/StdlibSymbolIndex.cs` | service | batch | `flow-lang/Runtime/ModuleLoader.cs` §ResolvePath (lines 113–127) — reuse `@`-prefix resolver |
| `flow-lsp/Symbols/KeywordIndex.cs` | utility | static data | `flow-editor/Editor/FlowSyntaxHighlighter.cs` §GetBrushForToken (token-category switch) |
| `flow-lsp/Handlers/TextDocumentSyncHandler.cs` | controller (LSP handler) | event-driven | NEW IDIOM — OmniSharp interface contract |
| `flow-lsp/Handlers/DiagnosticsPublisher.cs` | controller | transform | NEW IDIOM — `ErrorReporter.Errors` → LSP `Diagnostic[]` |
| `flow-lsp/Handlers/SemanticTokensHandler.cs` | controller | transform | `flow-editor/Editor/FlowSyntaxHighlighter.cs` §GetBrushForToken lines 95–147 (token → category switch; LSP version produces int-index into Legend instead of `IBrush`) |
| `flow-lsp/Handlers/CompletionHandler.cs` | controller | request-response | NEW IDIOM |
| `flow-lsp/Handlers/HoverHandler.cs` | controller | request-response | NEW IDIOM |
| `flow-lsp/Handlers/DefinitionHandler.cs` | controller | request-response | `flow-lang/Runtime/ModuleLoader.cs` §ResolvePath (reuse for stdlib import resolution) |
| `flow-lsp/Handlers/SignatureHelpHandler.cs` | controller | request-response | NEW IDIOM |
| `flow-lsp/NoteStream/NoteStreamContext.cs` | utility | transform | NEW IDIOM — AST descent looking for enclosing `MusicalContextStatement`; shape mimics evaluator dispatch but walks without executing |

### C# library addition (under `flow-lang/StandardLibrary/`)

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `flow-lang/StandardLibrary/BuiltInDocs.cs` | stdlib doc table | static lookup | `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` (same `public static class` + `IReadOnlyDictionary` shape) | role-match (doc table vs. impl table) |

### Test fixtures (under `flow-lang.Tests/Unit/Phase17/`)

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `flow-lang.Tests/Unit/Phase17/DocumentManagerTests.cs` | test | request-response | `flow-lang.Tests/Unit/Phase14/LexerTests.cs` (xUnit Fact shape) | exact role |
| `flow-lang.Tests/Unit/Phase17/DiagnosticsHandlerTests.cs` | test | request-response | same | exact role |
| `flow-lang.Tests/Unit/Phase17/SemanticTokensTests.cs` | test | request-response | same | exact role |
| `flow-lang.Tests/Unit/Phase17/CompletionHandlerTests.cs` | test | request-response | same | exact role |
| `flow-lang.Tests/Unit/Phase17/HoverHandlerTests.cs` | test | request-response | same | exact role |
| `flow-lang.Tests/Unit/Phase17/DefinitionHandlerTests.cs` | test | request-response | same | exact role |
| `flow-lang.Tests/Unit/Phase17/SignatureHelpHandlerTests.cs` | test | request-response | same | exact role |
| `flow-lang.Tests/Unit/Phase17/NoteStreamContextTests.cs` | test | request-response | same | exact role |
| `flow-lang.Tests/Unit/Phase17/OmniSharpBootTest.cs` | test | request-response | same | exact role (Wave 0 smoke test per Open Question Q1) |
| `flow-lang.Tests/Unit/Phase17/BuiltInDocsTests.cs` | test | request-response | same | exact role |

### VSCode extension (under `vscode-extension/`)

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `vscode-extension/package.json` | npm manifest | config | NEW IDIOM — no prior npm/TS in repo |
| `vscode-extension/tsconfig.json` | config | config | NEW IDIOM |
| `vscode-extension/.vscodeignore` | config | config | NEW IDIOM |
| `vscode-extension/language-configuration.json` | config | config | NEW IDIOM |
| `vscode-extension/src/extension.ts` | TS extension entrypoint | event-driven | NEW IDIOM |
| `vscode-extension/syntaxes/flow.tmLanguage.json` | TextMate grammar | static pattern | `flow-editor/Editor/FlowSyntaxHighlighter.cs` §GetBrushForToken — token-category blueprint; regexes are NEW IDIOM but categories port directly |
| `vscode-extension/snippets/flow.code-snippets` | VSCode snippet JSON | static data | NEW IDIOM — content derived from existing `.flow` idioms in `tests/` / `examples/` |
| `vscode-extension/tests/grammar/sample.flow` | grammar snapshot fixture | static data | `tests/test_chords.flow`, `tests/test_full_song.flow` (copy-paste source samples) |
| `vscode-extension/tests/grammar/note-stream.flow` | grammar snapshot fixture | static data | `tests/test_note_streams.flow` (if exists) or a distilled subset | partial |
| `vscode-extension/README.md` | docs | static | NEW IDIOM |

### CI and supporting scripts

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `.github/workflows/publish-extension.yml` | CI workflow | batch | NEW IDIOM — no `.github/` dir exists yet |
| `scripts/lsp-smoke.sh` | smoke script | event-driven | NEW IDIOM — no `scripts/` dir exists yet |
| `docs/editor-setup/nvim-lspconfig.lua` | docs snippet | static | NEW IDIOM |
| `docs/editor-setup/helix-languages.toml` | docs snippet | static | NEW IDIOM |

### Files modified (not created)

| File | Change |
|------|--------|
| `flow-sharp.sln` | Add `flow-lsp\flow-lsp.csproj` project entry (follow existing line-format for the other 5 projects). Grep the sln file for an existing `Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "flow-interpreter"...` block and replicate. |

---

## Pattern Assignments

### `flow-lsp/flow-lsp.csproj` (csproj, config)

**Analog:** `flow-interpreter/flow-interpreter.csproj`

**Core pattern** (`flow-interpreter/flow-interpreter.csproj:1-15`):
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\flow-lang\flow-lang.csproj" />
  </ItemGroup>

<PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>flow_interpreter</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

**Additions per RESEARCH (§Standard Stack Installation):**
- `<RootNamespace>FlowLsp</RootNamespace>` (matches the stated namespace)
- `<PublishSingleFile>true</PublishSingleFile>`
- `<SelfContained>true</SelfContained>`
- `<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>`
- `<PackageReference Include="OmniSharp.Extensions.LanguageServer" Version="0.19.9" />`
- DO NOT set `<PublishTrimmed>` (Pitfall 4 — OmniSharp uses reflection heavily)

**Caveats:**
- TargetFramework MUST be `net10.0`, not `net9.0` — verify with `grep -h TargetFramework flow-*/flow-*.csproj` pre-commit (Pitfall 1).
- NO `PackageReference` to `Melanchall.DryWetMidi` or `Pidgin` — LSP must stay lean. `flow-lang` pulls those transitively; that is fine, OmniSharp reflection surface doesn't need them.

---

### `flow-lsp/Program.cs` (server entrypoint, request-response)

**Analog:** `flow-interpreter/Program.cs` (arg parsing shape); `flow-lang/Core/FlowEngine.cs` (DI wiring style)

**`Main` arg-parse pattern to mirror** (`flow-interpreter/Program.cs:7-13`):
```csharp
class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("Flow Language Interpreter v0.1");
        Console.WriteLine();

        // Parse flags from args
        var flags = ParseFlags(args);
        ...
```

**DI wiring pattern to mirror** (`flow-lang/Core/FlowEngine.cs:38-54`):
```csharp
public FlowEngine(ErrorReporter errorReporter, bool verbose = false)
{
    _errorReporter = errorReporter;
    _audioManager = new AudioPlaybackManager();
    _diagnosticOutput = verbose ? Console.Error : null;

    // Create internal function registry and register C# implementations
    var internalRegistry = new InternalFunctionRegistry();
    BuiltInFunctions.RegisterAllImplementations(internalRegistry, _audioManager);

    _context = new RuntimeContext(_errorReporter, internalRegistry, _diagnosticOutput);
    BuiltInFunctions.RegisterIterationGuard(internalRegistry, _context);
    BuiltInFunctions.RegisterContextDependentFunctions(internalRegistry, _context);
    var moduleLoader = new ModuleLoader(_errorReporter, _diagnosticOutput);
    _interpreter = new Interpreter.Interpreter(_context, _errorReporter, moduleLoader);
    moduleLoader.ParentInterpreter = _interpreter;
}
```

**LSP-specific shape (RESEARCH §Architecture Patterns + Sample Program.cs):**
```csharp
using FlowLsp;
using OmniSharp.Extensions.LanguageServer.Server;

var server = await LanguageServer.From(options => options
    .WithInput(Console.OpenStandardInput())
    .WithOutput(Console.OpenStandardOutput())
    .WithServices(s => s
        .AddSingleton<ParseSession>()
        .AddSingleton<DocumentManager>()
        .AddSingleton<BuiltInIndex>()
        .AddSingleton<StdlibSymbolIndex>()
        .AddSingleton<KeywordIndex>())
    .WithHandler<TextDocumentSyncHandler>()
    .WithHandler<SemanticTokensHandler>()
    .WithHandler<CompletionHandler>()
    .WithHandler<HoverHandler>()
    .WithHandler<DefinitionHandler>()
    .WithHandler<SignatureHelpHandler>());
await server.WaitForExit;
```

**CRITICAL DIVERGENCE from FlowEngine analog:**
- **Do NOT** instantiate `AudioPlaybackManager` here. D-02 forbids audio in `flow-lsp` (Pitfall 3 — loads `libpulse-simple.so.0` via P/Invoke, fails on Win/macOS CI runners).
- **Do NOT** instantiate `Interpreter`. The server is a parse-time tool only.
- **Do NOT** register audio/playback built-ins. The `BuiltInIndex` walks the registry's *signatures* (for name/type), not executes anything.

**Caveats:**
- If OmniSharp 0.19.9 fails to boot on net10 (Open Question Q1), fall back to `StreamJsonRpc` + manual handlers per RESEARCH Pitfall 2. Wave 0 must gate this.

---

### `flow-lsp/ParseSession.cs` (service, request-response)

**Analog:** `flow-lang/Core/FlowEngine.cs:59-78` — this is the execute-pipeline excerpt **stripped to the first two stages**.

**Full Execute pattern in FlowEngine** (lines 59–78, the parts to keep):
```csharp
public bool Execute(string source, string? fileName = null)
{
    _errorReporter.Clear();

    try
    {
        // 1. Lex source into tokens
        var lexer = new SimpleLexer(source, _errorReporter, fileName);
        var tokens = lexer.Tokenize();

        if (_errorReporter.HasErrors)
            return false;

        // 2. Parse tokens into AST
        var parser = new Parser(tokens, _errorReporter);
        var program = parser.Parse();
```

**LSP shape (from RESEARCH Pitfall 3):**
```csharp
public sealed class ParseSession
{
    public ParseResult Parse(string source, string? path)
    {
        var er = new ErrorReporter();
        var tokens = new SimpleLexer(source, er, path).Tokenize();
        var ast = new Parser(tokens, er).Parse();
        return new ParseResult(ast, tokens, er.Errors);
    }
}

public sealed record ParseResult(
    Program Ast,
    IReadOnlyList<Token> Tokens,
    IReadOnlyList<FlowError> Errors);
```

**Caveats:**
- Do NOT clear errors between calls — each call allocates a fresh `ErrorReporter`. This is different from `FlowEngine.Execute` which calls `_errorReporter.Clear()` because it reuses the engine-scoped reporter.
- DO NOT call `new FlowEngine(...)` anywhere; that triggers `AudioPlaybackManager` ctor (FlowEngine.cs:41) which loads PulseAudio.

---

### `flow-lsp/DocumentManager.cs` (service, event-driven)

**Analog:** NEW IDIOM — no Dictionary-of-CancellationTokenSource pattern exists in the repo.

**Reference from RESEARCH §Code Examples (Document manager with debounce):**
```csharp
public sealed class DocumentManager
{
    private readonly Dictionary<DocumentUri, BufferEntry> _buffers = new();
    private readonly object _lock = new();
    private readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(150);
    private readonly Func<DocumentUri, string, CancellationToken, Task> _onParse;

    public DocumentManager(Func<DocumentUri, string, CancellationToken, Task> onParse)
        => _onParse = onParse;

    public void Open(DocumentUri uri, string text) => Update(uri, text);

    public void Update(DocumentUri uri, string text)
    {
        lock (_lock)
        {
            if (_buffers.TryGetValue(uri, out var existing))
                existing.Cts.Cancel();

            var cts = new CancellationTokenSource();
            _buffers[uri] = new BufferEntry(text, cts);
            _ = ScheduleParseAsync(uri, text, cts.Token);
        }
    }

    public void Close(DocumentUri uri)
    {
        lock (_lock)
        {
            if (_buffers.Remove(uri, out var existing))
                existing.Cts.Cancel();
        }
    }

    private async Task ScheduleParseAsync(DocumentUri uri, string text, CancellationToken ct)
    {
        try { await Task.Delay(_debounce, ct); }
        catch (TaskCanceledException) { return; }
        if (ct.IsCancellationRequested) return;
        await _onParse(uri, text, ct);
    }

    private sealed record BufferEntry(string Text, CancellationTokenSource Cts);
}
```

**Caveats:**
- **No in-repo analog.** Executor must treat this as a fresh pattern — the only prior debounce idea in the codebase is `flow-interpreter/LiveReloadManager.cs` (bar-boundary swap), which is a different problem.
- `Dictionary` must be guarded by `_lock`; OmniSharp dispatches handlers on multiple threads.

---

### `flow-lsp/LspMappings.cs` (utility, transform)

**Analog:** NEW IDIOM. Consumes `flow-lang/Core/SourceLocation.cs` (full contents below) and `flow-lang/Diagnostics/DiagnosticLevel.cs`.

**Source types** (`flow-lang/Core/SourceLocation.cs:1-16`):
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

**`DiagnosticLevel` enum** (`flow-lang/Diagnostics/DiagnosticLevel.cs:1-11`):
```csharp
namespace FlowLang.Diagnostics;

public enum DiagnosticLevel
{
    Info,
    Warning,
    Error
}
```

**Mapping pattern (from RESEARCH §Code Examples Diagnostics handler):**
```csharp
// SourceLocation is 1-based; LSP Range is 0-based.
Range = new Range(
    new Position(Math.Max(0, e.Location.Line - 1), Math.Max(0, e.Location.Column - 1)),
    new Position(Math.Max(0, e.Location.Line - 1), Math.Max(0, e.Location.Column - 1) + 1))
```

**Caveats:**
- `SourceLocation.Unknown` has Line=0, Column=0 — the `Math.Max(0, ...)` guards protect against this.
- `FlowError` (see `flow-lang/Diagnostics/FlowError.cs:8-13`) is a record with `Level`, `Message`, `Location`, `InnerException`. The mapper only needs the first three.

---

### `flow-lsp/Symbols/BuiltInIndex.cs` (service, batch)

**Analog:** `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` — the thing we walk.

**Registry surface to read** (`flow-lang/StandardLibrary/InternalFunctionRegistry.cs:10-20`):
```csharp
public class InternalFunctionRegistry
{
    private readonly Dictionary<string, List<(FunctionSignature Signature, Func<IReadOnlyList<Value>, Value> Implementation)>> _implementations = new();

    public void Register(string name, FunctionSignature signature, Func<IReadOnlyList<Value>, Value> implementation)
    {
        if (!_implementations.ContainsKey(name))
            _implementations[name] = [];

        _implementations[name].Add((signature, implementation));
    }
    ...
    public bool HasImplementation(string name) => _implementations.ContainsKey(name);
```

**Problem:** `_implementations` is private. Two viable strategies:
1. **Expose enumeration** — add `public IEnumerable<KeyValuePair<string, IReadOnlyList<FunctionSignature>>> EnumerateAll()` to `InternalFunctionRegistry`. Clean.
2. **Mirror-register** — have BuiltInIndex receive its own empty registry and call the same `RegisterAllImplementations(registry, audioManager: null-or-dummy)` — but this bootstrap calls into audio code (PulseAudio ctor). Don't do this.

**Recommendation:** Strategy 1 — add a read-only enumerator to `InternalFunctionRegistry`, then build `BuiltInIndex` by iterating. Keep the delegate out of the enumerator.

**Caveats:**
- The name→overloads structure means a single name can have multiple signatures. BuiltInIndex items must preserve all overloads for signature-help's over-resolution.
- `BuiltInIndex` is built once at server startup (D-07: "built once at server start by walking `InternalFunctionRegistry`").

---

### `flow-lsp/Symbols/UserSymbolIndex.cs` (service, batch)

**Analog:** NEW IDIOM — AST visitor. Closest shape is the switch-dispatch over AST records in `flow-lang/Interpreter/ExpressionEvaluator.cs` (visit-per-node-type via pattern match, NOT visitor pattern).

**AST node records to walk** (from `flow-lang/Ast/Statements/`):
- `ProcDeclaration` — collect name + parameters.
- `VariableDeclaration` — collect name + declared type.
- `SectionDeclaration` — collect name.
- `ImportStatement` — collect import path so completion knows which stdlib names are in scope.
- `MusicalContextStatement` — recurse into `.Body` (its shape in `flow-lang/Ast/Statements/MusicalContextStatement.cs:14-20`):
```csharp
public record MusicalContextStatement(
    SourceLocation Location,
    MusicalContextType ContextType,
    Expression Value,
    Expression? Value2,
    IReadOnlyList<Statement> Body
) : Statement(Location);
```

**Shape pattern (from RESEARCH §Code Examples Note-stream context walk — same walk shape):**
```csharp
void Walk(IReadOnlyList<Statement> stmts)
{
    foreach (var s in stmts)
        if (s is ProcDeclaration pd) { Collect(pd); Walk(pd.Body); }
        else if (s is VariableDeclaration vd) Collect(vd);
        else if (s is SectionDeclaration sd) Walk(sd.Body);
        else if (s is MusicalContextStatement m) Walk(m.Body);
        else if (s is ImportStatement imp) Collect(imp);
}
```

**Caveats:**
- No `IAstVisitor` exists in the codebase. Use pattern-match dispatch per repo convention (CLAUDE.md §C# Conventions: "Pattern matching (`switch` expressions) for node dispatch rather than visitor pattern").
- This index is **rebuilt per-document on each parse** — not a startup-built static index (contrast with `BuiltInIndex`).

---

### `flow-lsp/Symbols/StdlibSymbolIndex.cs` (service, batch)

**Analog:** `flow-lang/Runtime/ModuleLoader.cs` §ResolvePath — reuse the `@`-prefix resolver.

**Exact resolver pattern** (`flow-lang/Runtime/ModuleLoader.cs:113-127`):
```csharp
private string ResolvePath(string path, string? currentFile)
{
    // Handle internal library imports (e.g., "@std" or "@std.flow")
    if (path.StartsWith("@"))
    {
        var libraryName = path.Substring(1); // Remove '@' prefix

        // Add .flow extension if not present
        if (!libraryName.EndsWith(".flow"))
            libraryName += ".flow";

        // Resolve to the standard library directory (same directory as the executing assembly)
        var assemblyDir = Path.GetDirectoryName(typeof(ModuleLoader).Assembly.Location) ?? Environment.CurrentDirectory;
        return Path.GetFullPath(Path.Combine(assemblyDir, libraryName));
    }
    ...
}
```

**Caveats:**
- `typeof(ModuleLoader).Assembly.Location` resolves to where `flow-lang.dll` lives AT RUNTIME. When packed in single-file self-contained mode, that is the temporary extracted dir, so the 6 stdlib `.flow` files must be copied there. `flow-lang.csproj:16-37` already lists all 7 with `CopyToOutputDirectory=PreserveNewest`; CI must verify they land in `vscode-extension/server/<platform>/` (Pitfall 6).
- `StdlibSymbolIndex` reads the 6 stdlib paths (`@std, @collections, @audio, @bars, @notation, @composition`), runs each through `ParseSession.Parse`, collects top-level `ProcDeclaration` names. Built ONCE at server startup, not per-document.
- Alternative: scan `assemblyDir` for all `*.flow` files at startup (per CONTEXT §Specifics: "`use "@name"` completion should read the actual `flow-lang/` directory at server startup so new stdlib modules appear without code changes").

---

### `flow-lsp/Symbols/KeywordIndex.cs` (utility, static data)

**Analog:** `flow-editor/Editor/FlowSyntaxHighlighter.cs` §GetBrushForToken (the enum list).

**Keyword-selection pattern** (`flow-editor/Editor/FlowSyntaxHighlighter.cs:99-108`):
```csharp
// Keywords
TokenType.Proc or TokenType.EndProc or TokenType.Return or
TokenType.Use or TokenType.Internal or TokenType.Lazy or TokenType.Fn
    => KeywordBrush,

// Music context keywords
TokenType.Tempo or TokenType.Timesig or TokenType.Key or
TokenType.Swing or TokenType.Dynamics or TokenType.Rit or
TokenType.Accel or TokenType.Pickup
    => MusicKeywordBrush,
```

**LSP port:** static array of strings derived from `TokenType` enum values that map to keywords. Do NOT read `TokenType.cs` at runtime — hard-code the list in source. New keywords added later mean a code change here AND in the semantic-tokens legend; keep them co-located or cross-linked.

**Caveats:**
- Must include BOTH general keywords (`proc`, `use`, `return`, `internal`, `lazy`, `fn`, `section`, `for`, `while`, `break`, `continue`, `in`, `progression`) AND music keywords (`tempo`, `timesig`, `key`, `swing`, `dynamics`, `rit`, `accel`, `pickup`, `pan`, `gain`) per D-07.
- Type keywords (`Int`, `Float`, `String`, `Bool`, `Note`, `Buf`, etc.) are ALSO completable as identifiers in variable declarations — include them but tag as `CompletionItemKind.TypeParameter`.

---

### `flow-lsp/Handlers/TextDocumentSyncHandler.cs` (controller, event-driven)

**Analog:** NEW IDIOM — OmniSharp interface.

**Responsibilities (from RESEARCH §Data flow):**
- Implement `ITextDocumentSyncHandler` / inherit `TextDocumentSyncHandlerBase` (per OmniSharp convention).
- On `didOpen` / `didChange`: call `DocumentManager.Update(uri, text)`.
- On `didClose`: call `DocumentManager.Close(uri)`.
- Register document-selector for `.flow` files.

**Caveats:**
- OmniSharp API signatures must be verified at code-write time; RESEARCH examples are sketches. Expect minor API drift.
- The `onParse` callback passed into `DocumentManager` is where the pipeline lights up: `ParseSession.Parse → store cached ParseResult → invoke DiagnosticsPublisher and UserSymbolIndex rebuild`.

---

### `flow-lsp/Handlers/DiagnosticsPublisher.cs` (controller, transform)

**Analog:** NEW IDIOM — but `ErrorReporter` → `FlowError` shape is the input.

**Input contract** (`flow-lang/Diagnostics/FlowError.cs:8-13`):
```csharp
public record FlowError(
    DiagnosticLevel Level,
    string Message,
    SourceLocation Location,
    Exception? InnerException = null)
```

**Exact mapping (from RESEARCH §Code Examples Diagnostics handler):**
```csharp
private void PublishDiagnostics(DocumentUri uri, IReadOnlyList<FlowError> errors)
{
    var diags = errors.Select(e => new Diagnostic {
        Severity = e.Level switch {
            DiagnosticLevel.Error   => DiagnosticSeverity.Error,
            DiagnosticLevel.Warning => DiagnosticSeverity.Warning,
            DiagnosticLevel.Info    => DiagnosticSeverity.Information,
            _ => DiagnosticSeverity.Error
        },
        Source = "flow",
        Message = e.Message,
        Range = new Range(
            new Position(Math.Max(0, e.Location.Line - 1), Math.Max(0, e.Location.Column - 1)),
            new Position(Math.Max(0, e.Location.Line - 1), Math.Max(0, e.Location.Column - 1) + 1))
    }).ToImmutableArray();

    _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams {
        Uri = uri,
        Diagnostics = new Container<Diagnostic>(diags)
    });
}
```

**Caveats:**
- `FlowError.Location` carries only a START position (no end). Use a length-1 range. If parse-error recovery later adds end positions, this can be widened.
- When `errors` is empty, the call MUST still fire — LSP uses an empty diagnostics array to clear prior markers. Don't early-return.

---

### `flow-lsp/Handlers/SemanticTokensHandler.cs` (controller, transform)

**Analog:** `flow-editor/Editor/FlowSyntaxHighlighter.cs` §GetBrushForToken (the whole switch, lines 95–147).

**Exact token→category switch to port** (`flow-editor/Editor/FlowSyntaxHighlighter.cs:95-146`):
```csharp
private static IBrush? GetBrushForToken(TokenType type)
{
    return type switch
    {
        // Keywords
        TokenType.Proc or TokenType.EndProc or TokenType.Return or
        TokenType.Use or TokenType.Internal or TokenType.Lazy or TokenType.Fn
            => KeywordBrush,

        // Music context keywords
        TokenType.Tempo or TokenType.Timesig or TokenType.Key or
        TokenType.Swing or TokenType.Dynamics or TokenType.Rit or
        TokenType.Accel or TokenType.Pickup
            => MusicKeywordBrush,

        // Section
        TokenType.Section => SectionBrush,

        // Type keywords
        TokenType.Void or TokenType.Int or TokenType.Float or
        TokenType.Long or TokenType.Double or TokenType.String or
        TokenType.Bool or TokenType.Number or TokenType.Note or
        TokenType.Buf
            => TypeBrush,

        // Literals
        TokenType.IntLiteral or TokenType.FloatLiteral or
        TokenType.SemitoneLiteral or TokenType.CentLiteral or
        TokenType.TimeLiteral or TokenType.DecibelLiteral
            => NumberBrush,

        TokenType.StringLiteral => StringBrush,
        TokenType.BoolLiteral => BoolBrush,

        // Music literals
        TokenType.NoteLiteral or TokenType.ChordLiteral
            => NoteBrush,

        // Operators
        TokenType.Arrow or TokenType.FatArrow or TokenType.Plus or
        TokenType.Minus or TokenType.Star or TokenType.Slash or
        TokenType.LessThan or TokenType.GreaterThan or TokenType.Assign
            => OperatorBrush,

        // Pipe delimiters (note streams)
        TokenType.Pipe => PipeBrush,

        // Comments
        TokenType.Comment => CommentBrush,

        _ => null
    };
}
```

**LSP port** (from RESEARCH §Code Examples Semantic Tokens — exactly mirrors `FlowSyntaxHighlighter`'s categories but returns an index into a `SemanticTokenType[]` legend instead of `IBrush`):
```csharp
private static readonly SemanticTokenType[] Legend = {
    SemanticTokenType.Keyword,    // 0 — general + music keywords + section (port FlowSyntaxHighlighter.SectionBrush → Keyword)
    SemanticTokenType.Type,       // 1 — type keywords
    SemanticTokenType.String,     // 2
    SemanticTokenType.Number,     // 3
    SemanticTokenType.Operator,   // 4
    SemanticTokenType.Comment,    // 5
    SemanticTokenType.Variable,   // 6 — note literals
    SemanticTokenType.Function,   // 7 — chord literals
    SemanticTokenType.Macro,      // 8 — pipe delimiters
};
```

**Delta encoding** (from RESEARCH §Code Examples, 5-tuple per token):
```csharp
public int[] EncodeTokens(IReadOnlyList<Token> tokens)
{
    var data = new List<int>(tokens.Count * 5);
    int prevLine = 0, prevCol = 0;
    foreach (var t in tokens)
    {
        var typeIdx = MapTokenType(t.Type);
        if (typeIdx is null) continue;
        int line = t.Location.Line - 1;
        int col  = t.Location.Column - 1;
        int dLine = line - prevLine;
        int dCol  = dLine == 0 ? col - prevCol : col;
        data.Add(dLine);
        data.Add(dCol);
        data.Add(t.Text.Length);
        data.Add(typeIdx.Value);
        data.Add(0);  // modifiers
        prevLine = line;
        prevCol = col;
    }
    return data.ToArray();
}
```

**Caveats:**
- `FlowSyntaxHighlighter` uses `SectionBrush` (pink) for `TokenType.Section`. Per D-05, no invented scopes — port `Section` to the same index as general Keyword (0), accept the color loss. Theme authors can distinguish via semantic-token-MODIFIERS if desired (CONTEXT left this to Claude's discretion).
- Musical context keywords (`Tempo`, `Timesig`, etc.) get indexed as Keyword too — no standard `keyword.other.music` scope exists per D-05.
- Tokens MUST be sorted by `(line, column)` ascending. `SimpleLexer` already guarantees this.

---

### `flow-lsp/Handlers/CompletionHandler.cs` (controller, request-response)

**Analog:** NEW IDIOM.

**Handler core** (from RESEARCH §Code Examples Completion handler core):
```csharp
public Task<CompletionList> Handle(CompletionParams req, CancellationToken ct)
{
    var doc = _docManager.Get(req.TextDocument.Uri);
    var prevToken = TokenJustLeftOfCursor(doc, req.Position);

    if (IsInsideUseStringLiteral(doc, req.Position))
        return Task.FromResult(StdlibPathCompletions());

    if (IsInsideNoteStream(doc, req.Position))
        return Task.FromResult(NoteStreamCompletions(doc, req.Position));

    var items = new List<CompletionItem>();
    items.AddRange(_builtIns.Items);
    items.AddRange(_stdlib.Items);
    items.AddRange(_userSymbols.For(doc.Uri));
    items.AddRange(_keywords.Items);
    items.AddRange(SnippetTemplates());
    return Task.FromResult(new CompletionList(items));
}
```

**Snippet templates** (from CONTEXT D-07):
- `tempo ${1:120} { $0 }`
- `key ${1:Cmajor} { $0 }`
- `timesig ${1:4}/${2:4} { $0 }`
- `proc ${1:name} () { $0 }`
- `section ${1:name} { $0 }`

**Caveats:**
- Each source's `Items` should be precomputed where possible — `_builtIns.Items` and `_stdlib.Items` are set at server startup; `_userSymbols.For(uri)` regenerates per document.
- Inside `| ... |`, proc/variable names are EXCLUDED (D-11). Must short-circuit before adding `_builtIns` / `_userSymbols`.
- Inside `"..."` of `use "..."`, ONLY stdlib paths — skip all other sources.

---

### `flow-lsp/Handlers/HoverHandler.cs` (controller, request-response)

**Analog:** NEW IDIOM. Consumes `BuiltInDocs.TryGet` (new file below).

**Pattern** (from RESEARCH §Code Examples Hover handler):
```csharp
public Task<Hover?> Handle(HoverParams req, CancellationToken ct)
{
    var token = TokenAt(req);
    if (token is null) return Task.FromResult<Hover?>(null);

    var builtIn = _builtIns.Find(token.Text);
    if (builtIn is not null)
    {
        var doc = BuiltInDocs.TryGet(token.Text);
        var md = $"```flow\n{builtIn.SignatureToString()}\n```\n\n" +
                 (doc?.Summary ?? "*No documentation available.*");
        return Task.FromResult<Hover?>(new Hover { Contents = MarkdownString(md) });
    }

    var userSym = _userSymbols.Find(req.TextDocument.Uri, token.Text);
    if (userSym is not null) return Task.FromResult<Hover?>(UserSymbolHover(userSym));

    var stdProc = _stdlib.Find(token.Text);
    if (stdProc is not null) return Task.FromResult<Hover?>(StdlibProcHover(stdProc));

    return Task.FromResult<Hover?>(null);
}
```

**Caveats:**
- Three-way lookup order: built-in → user symbol → stdlib proc. Match CONTEXT D-08 (built-ins: signature + doc; user symbols: declared type; stdlib: signature from parsed proc).
- `TokenAt(req)` must resolve the position to a `Token` from the cached `ParseResult.Tokens` — line-start offset cache (per `Don't Hand-Roll` table) makes this O(1).

---

### `flow-lsp/Handlers/DefinitionHandler.cs` (controller, request-response)

**Analog:** `flow-lang/Runtime/ModuleLoader.cs:113-127` (reuse resolver — already excerpted under `StdlibSymbolIndex`).

**Responsibilities:**
1. For user procs/variables: walk current-document AST for matching `ProcDeclaration` / `VariableDeclaration` with matching name, return its `SourceLocation` as an LSP `Location`.
2. For imports (`use "@audio"`): resolve `@audio` via `ModuleLoader.ResolvePath` logic, return `Location` with that file URI + position (0,0).
3. For built-ins: return null (D-09 "Built-ins report `no definition available`"). OmniSharp usually maps null → no-response, which the client shows as "No definition found".

**Caveats:**
- `ModuleLoader.ResolvePath` is `private`. Either:
  - (a) Duplicate the 15-line `@`-prefix logic in the handler, OR
  - (b) Extract it to a `public static string ResolveStdlibPath(string name)` on `ModuleLoader`.
  Recommend (b) — cleaner, and the existing evaluator can use it too.
- Stdlib `.flow` files must be present next to the published `flow-lsp` binary (Pitfall 6).

---

### `flow-lsp/Handlers/SignatureHelpHandler.cs` (controller, request-response)

**Analog:** NEW IDIOM.

**Responsibilities:**
- Detect cursor inside `foo(arg1, arg2, |cursor|)`.
- Count commas from opening `(` to cursor → active-parameter index.
- Look up `foo` in `_builtIns` / `_userSymbols` / `_stdlib`; emit `SignatureInformation` with all overloads.
- Source info (built-in signatures) comes from `InternalFunctionRegistry`'s `FunctionSignature`.

**Caveats:**
- `FunctionSignature` type lives in `flow-lang/TypeSystem/` — verify shape before consuming. The hover handler needs the same formatter (`SignatureToString()`) — consider hoisting to `LspMappings.cs` or `BuiltInIndex`.
- Overloaded built-ins: emit ONE `SignatureHelp` with all overloads as `Signatures`, let the client pick active by parameter-count match.

---

### `flow-lsp/NoteStream/NoteStreamContext.cs` (utility, transform)

**Analog:** NEW IDIOM, closest-shape is the walk dispatch in interpreter code. The walk is over `MusicalContextStatement.Body`.

**Target AST node** (`flow-lang/Ast/Statements/MusicalContextStatement.cs:6-20`):
```csharp
public enum MusicalContextType { Timesig, Tempo, Swing, Key, Dynamics, Rit, Accel, Pan, Gain }

public record MusicalContextStatement(
    SourceLocation Location,
    MusicalContextType ContextType,
    Expression Value,
    Expression? Value2,
    IReadOnlyList<Statement> Body
) : Statement(Location);
```

**Target container** (`flow-lang/Ast/Expressions/NoteStreamExpression.cs:129-132`):
```csharp
public record NoteStreamExpression(
    SourceLocation Location,
    IReadOnlyList<NoteStreamBar> Bars
) : Expression(Location);
```

**Exact walk pattern** (from RESEARCH §Code Examples Note-stream context walk):
```csharp
private static string? FindEnclosingKey(Program ast, Position cursor)
{
    string? winner = null;
    void Walk(IReadOnlyList<Statement> stmts)
    {
        foreach (var s in stmts)
            if (s is MusicalContextStatement m && m.ContextType == MusicalContextType.Key
                && Contains(m, cursor))
            {
                winner = ((LiteralExpression)m.Value).Value as string ?? winner;
                Walk(m.Body);  // deeper key wins
            }
            else if (s is SectionDeclaration sd && Contains(sd, cursor)) Walk(sd.Body);
            else if (s is ProcDeclaration pd && Contains(pd, cursor))    Walk(pd.Body);
            else if (s is MusicalContextStatement m2 && Contains(m2, cursor)) Walk(m2.Body);
    }
    Walk(ast.Statements);
    return winner;
}
```

**CRITICAL CAVEAT:**
- **This walks the AST only. NO evaluator.** Do NOT call `ExecutionContext.GetMusicalContext()` or instantiate `Interpreter`. Per RESEARCH §Anti-Patterns: "MusicalContext walking for D-11 must be done by AST traversal — not by spinning up an `Interpreter` and reading `ExecutionContext.GetMusicalContext()`. The interpreter mutates state and renders audio; both are wrong inside an LSP."
- Contrast with `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:50`, which DOES call `context.GetMusicalContext()` — that is evaluator-side code and is the WRONG pattern for the LSP side.
- Token-based fallback path exists for mid-edit broken ASTs (scan tokens backward for unmatched `Pipe`). Plan to implement only if AST-based fails in practice.

---

### `flow-lang/StandardLibrary/BuiltInDocs.cs` (stdlib doc table, static lookup)

**Analog:** `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` — same `public static class` + `IReadOnlyDictionary` static-member idiom.

**Header & shape** (derived from analog):
```csharp
// flow-lang/StandardLibrary/BuiltInDocs.cs
namespace FlowLang.StandardLibrary;

/// <summary>
/// Static lookup table mapping built-in function name → human-readable doc.
/// Hover handler reads this; falls back to signature-only when the key is absent.
/// Add new entries here when you register a new built-in.
/// </summary>
public static class BuiltInDocs
{
    public sealed record Doc(string Summary, IReadOnlyList<ParamDoc> Params);
    public sealed record ParamDoc(string Name, string Description);

    private static readonly IReadOnlyDictionary<string, Doc> _docs = new Dictionary<string, Doc>
    {
        ["print"] = new("Prints a string to standard output.", [
            new("s", "The string to print.")
        ]),
        ["concat"] = new("Concatenates two strings into one.", [
            new("a", "First string."),
            new("b", "Second string."),
        ]),
        // ... grow as built-ins are added
    };

    public static Doc? TryGet(string name) =>
        _docs.TryGetValue(name, out var doc) ? doc : null;
}
```

**Starter-set candidates per CONTEXT D-12** (stdio, arithmetic, collections, audio core, chord/note operations):
- I/O: `print`, `input`, `str`
- Collections: `head`, `tail`, `map`, `filter`, `reduce`, `length`, `range`, `append`
- Audio core: `buffer`, `sine`, `saw`, `writeWav`, `play`
- Chord/note: `chordNotes`, `arpeggio`, `transpose`, `invert`

**Caveats:**
- File lives in `flow-lang/StandardLibrary/` not `flow-lsp/` — per Open Question Q3, CONTEXT D-12 explicitly specifies the flow-lang location. This means the doc table ships with the interpreter too (fine; future `flow --help <fn>` can reuse it).
- No audio/playback registration here — pure data.

---

### Test fixtures under `flow-lang.Tests/Unit/Phase17/`

**Analog for all 10 test files:** `flow-lang.Tests/Unit/Phase14/LexerTests.cs` (the xUnit Fact shape).

**File header + Fact shape** (`flow-lang.Tests/Unit/Phase14/LexerTests.cs:1-41`):
```csharp
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase14;

/// <summary>
/// Phase 14 DX-06 lexer regression Facts.
/// ...
/// </summary>
public class LexerTests
{
    private static Token FirstNonEof(string source)
    {
        var lexer = new SimpleLexer(source, new ErrorReporter());
        var tokens = lexer.Tokenize();
        foreach (var t in tokens)
        {
            if (t.Type != TokenType.Eof)
                return t;
        }
        throw new InvalidOperationException("No non-Eof tokens produced");
    }

    [Fact] public void Dm_IsChord()     => Assert.Equal(TokenType.ChordLiteral, FirstNonEof("Dm").Type);
    [Fact] public void Cmaj7_IsChord()  => Assert.Equal(TokenType.ChordLiteral, FirstNonEof("Cmaj7").Type);
    ...
```

**Port for Phase 17:**
- Namespace: `FlowLang.Tests.Unit.Phase17`
- Setup helper: `ParseFixture(string source)` that returns a `ParseResult` via `new ParseSession().Parse(source, null)`.
- One `public class <HandlerName>Tests` per test file.
- Xunit `[Fact]` with short, one-line lambdas where possible (Phase14 style — the one-liner `=>` body is idiomatic here).
- Facts targeting specific D-IDs should include the D-ID in the test name or doc comment (Phase14 pattern: "Phase 14 DX-06 ... CONTEXT D-07").

**Caveats:**
- Phase 14 tests demonstrate `[Fact]` not `[Theory]`; use `[Theory]` + `[InlineData]` only when many similar cases (e.g., many note/chord strings through the same assertion).
- OmniSharp boot-test (`OmniSharpBootTest.cs`, per Open Question Q1) must run an in-process LSP initialize+shutdown — this is a NEW IDIOM, use `LanguageServer.From(options).Start()` + synthetic input/output streams.

---

### `vscode-extension/package.json` (npm manifest, config)

**Analog:** NEW IDIOM — no npm project in repo.

**Full contents** (from RESEARCH §Code Examples package.json contributes block, unmodified):
```json
{
  "name": "flow-language",
  "displayName": "Flow Language",
  "version": "1.0.0",
  "publisher": "<your-publisher-id>",
  "engines": { "vscode": "^1.85.0" },
  "categories": ["Programming Languages"],
  "activationEvents": ["onLanguage:flow"],
  "main": "./out/extension.js",
  "contributes": {
    "languages": [{
      "id": "flow",
      "aliases": ["Flow"],
      "extensions": [".flow"],
      "configuration": "./language-configuration.json"
    }],
    "grammars": [{
      "language": "flow",
      "scopeName": "source.flow",
      "path": "./syntaxes/flow.tmLanguage.json"
    }],
    "snippets": [{
      "language": "flow",
      "path": "./snippets/flow.code-snippets"
    }],
    "configuration": {
      "type": "object",
      "title": "Flow",
      "properties": {
        "flow.server.path": { "type": "string", "default": "", "description": "..." },
        "flow.trace.server": { "type": "string", "enum": ["off","messages","verbose"], "default": "off" }
      }
    }
  },
  "dependencies": { "vscode-languageclient": "^9.0.1" },
  "devDependencies": {
    "@types/node": "^20.0.0",
    "@types/vscode": "^1.85.0",
    "@vscode/vsce": "^3.9.1",
    "ovsx": "^0.10.11",
    "typescript": "^5.0.0",
    "vscode-tmgrammar-test": "^0.1.3"
  }
}
```

**Caveats:**
- `publisher` must be set to a real VSCode Marketplace publisher ID before tag-triggered CI can publish (D-15 setup task).
- `engines.vscode` must match `@types/vscode` version to avoid type mismatch warnings.

---

### `vscode-extension/src/extension.ts` (TS extension entrypoint, event-driven)

**Analog:** NEW IDIOM — no TS anywhere in repo.

**Full contents** (from RESEARCH §Code Examples TypeScript activation with platform detection, unmodified):
```typescript
import * as path from 'path';
import * as fs from 'fs';
import { workspace, ExtensionContext, window } from 'vscode';
import { LanguageClient, LanguageClientOptions, ServerOptions, Executable, TransportKind } from 'vscode-languageclient/node';

let client: LanguageClient | undefined;

function platformDir(): string {
  const platform = process.platform;
  const arch = process.arch;
  return `${platform}-${arch}`;
}

function defaultBinaryPath(context: ExtensionContext): string {
  const dir = platformDir();
  const exe = process.platform === 'win32' ? 'flow-lsp.exe' : 'flow-lsp';
  return context.asAbsolutePath(path.join('server', dir, exe));
}

export async function activate(context: ExtensionContext) {
  const config = workspace.getConfiguration('flow');
  const override = (config.get<string>('server.path') ?? '').trim();
  const binary = override !== '' ? override : defaultBinaryPath(context);

  if (!fs.existsSync(binary)) {
    window.showErrorMessage(`Flow LSP binary not found at ${binary}`);
    return;
  }

  if (process.platform !== 'win32') {
    try { fs.chmodSync(binary, 0o755); } catch { /* best-effort */ }
  }

  const exe: Executable = { command: binary, transport: TransportKind.stdio, options: { env: process.env } };
  const serverOptions: ServerOptions = { run: exe, debug: exe };
  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: 'file', language: 'flow' }],
    synchronize: { fileEvents: workspace.createFileSystemWatcher('**/*.flow') },
    traceOutputChannel: window.createOutputChannel('Flow LSP Trace')
  };

  client = new LanguageClient('flow', 'Flow Language Server', serverOptions, clientOptions);
  await client.start();
}

export function deactivate(): Thenable<void> | undefined {
  return client?.stop();
}
```

**Caveats:**
- `platformDir()` produces `linux-x64`, `win32-x64`, `darwin-x64`, `darwin-arm64` — these MUST match the `vsce --target <platform>` names (Pitfall 7: note `win32-x64`, not `win-x64`).
- Binary must be chmod'd on POSIX post-VSIX-extract; VSIX (zip-based) does not preserve exec bit on all extraction paths.

---

### `vscode-extension/syntaxes/flow.tmLanguage.json` (TextMate grammar, static pattern)

**Analog:** `flow-editor/Editor/FlowSyntaxHighlighter.cs` — the categories (keywords, music keywords, types, literals, operators, pipes, comments) map directly to TM scopes, but the regex patterns themselves are NEW.

**Scope-assignment blueprint (from D-05 + the brush categories in FlowSyntaxHighlighter):**
- General keywords (`proc`, `use`, `return`, `internal`, `lazy`, `fn`, `section`, `for`, `while`, `break`, `continue`, `in`, `progression`) → `keyword.control.flow`
- Music-context keywords (`tempo`, `timesig`, `key`, `swing`, `dynamics`, `rit`, `accel`, `pickup`, `pan`, `gain`) → `keyword.control.flow` (same bucket per D-05; no invented `keyword.music.flow`)
- Type keywords (`Int`, `Float`, `Long`, `Double`, `String`, `Bool`, `Number`, `Note`, `Buf`, `Void`) → `storage.type.flow`
- Number/duration/cent/decibel/time literals → `constant.numeric.flow`
- String literal → `string.quoted.double.flow`
- Bool literal → `constant.language.flow`
- Note literal (e.g. `C4`, `Db5`, `F#3`) → `variable.other.note.flow` (per CONTEXT D-05 example)
- Chord literal (e.g. `Cmaj7`, `Dm`, `Bdim`) → `entity.name.function.flow` (closest standard scope per D-05)
- Operators (`->`, `=>`, `+`, `-`, `*`, `/`, `<`, `>`, `=`) → `keyword.operator.flow`
- Pipe delimiter `|` → `punctuation.section.flow` or `keyword.operator.flow` (pick one; document choice)
- Line comment `;` → `comment.line.flow`

**Caveats:**
- Pitfall 5: Do NOT model note-stream bar boundaries with `begin`/`end` blocks. Color notes/chords/durations as standalone token patterns regardless of the enclosing stream. Semantic tokens from the server give the AST-precise refinement.
- No `keyword.other.music.flow` or similar invented scopes (D-05).

---

### `vscode-extension/snippets/flow.code-snippets` (VSCode snippet JSON, static data)

**Analog:** NEW IDIOM. Content drawn from existing idioms in `tests/*.flow`.

**Snippet set (from CONTEXT D-07 + §Specifics "lowercase keywords, space before `{`, body on following lines"):**
```json
{
  "Tempo block": {
    "prefix": "tempo",
    "body": ["tempo ${1:120} {", "\t$0", "}"],
    "description": "Insert a tempo musical context block"
  },
  "Key block": {
    "prefix": "key",
    "body": ["key ${1:Cmajor} {", "\t$0", "}"]
  },
  "Timesig block": {
    "prefix": "timesig",
    "body": ["timesig ${1:4}/${2:4} {", "\t$0", "}"]
  },
  "Proc declaration": {
    "prefix": "proc",
    "body": ["proc ${1:name} (${2}) {", "\t$0", "}"]
  },
  "Section declaration": {
    "prefix": "section",
    "body": ["section ${1:name} {", "\t$0", "}"]
  }
}
```

**Caveats:**
- Snippet prefix MUST match a lowercase keyword token — VSCode completion surfaces snippets as regular completions when the prefix matches.

---

### `vscode-extension/tests/grammar/sample.flow` (grammar snapshot fixture, static data)

**Analog:** `tests/test_chords.flow`, `tests/test_full_song.flow` — just copy (or trim) existing `.flow` samples to exercise the grammar scopes.

**Suggested coverage:**
- `sample.flow` — one of each token category (keyword, type, string, number, note, chord, operator, pipe, comment)
- `note-stream.flow` — multi-bar stream with chords, rests, durations, dotted/tied notes, random choice, roman numerals inside a `key { }` block
- `chords.flow` — copy of `tests/test_chords.flow` fragments, exercises `Dm`, `Cmaj7`, `Bfm`, `Csmaj`, etc.
- `musical-context.flow` — nested `tempo { key { timesig { ... } } }` to test scope layering

**Caveats:**
- Snapshots are generated by `npx vscode-tmgrammar-snap` and committed to the repo; regressions surface as diffs.

---

### `.github/workflows/publish-extension.yml` (CI workflow, batch)

**Analog:** NEW IDIOM — no `.github/workflows/` directory exists in the repo yet (verified: `ls .github/` → "no .github dir").

**Full content (from RESEARCH §Code Examples Per-platform CI workflow):**
```yaml
name: Publish Flow Extension
on:
  push:
    tags: ['v*']
  workflow_dispatch:

jobs:
  build-server:
    strategy:
      fail-fast: true
      matrix:
        include:
          - rid: linux-x64
            target: linux-x64
            runner: ubuntu-latest
          - rid: win-x64
            target: win32-x64
            runner: windows-latest
          - rid: osx-x64
            target: darwin-x64
            runner: macos-latest
          - rid: osx-arm64
            target: darwin-arm64
            runner: macos-latest
    runs-on: ${{ matrix.runner }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - uses: actions/setup-node@v4
        with: { node-version: '20' }
      - name: Publish flow-lsp
        run: >
          dotnet publish flow-lsp/flow-lsp.csproj
          -c Release
          -r ${{ matrix.rid }}
          --self-contained
          -p:PublishSingleFile=true
          -p:IncludeNativeLibrariesForSelfExtract=true
          -o vscode-extension/server/${{ matrix.target }}
      - name: Verify stdlib copied
        shell: bash
        run: test -f vscode-extension/server/${{ matrix.target }}/audio.flow
      - name: Smoke-test the binary
        shell: bash
        run: |
          ./scripts/lsp-smoke.sh vscode-extension/server/${{ matrix.target }}/flow-lsp* 2>&1
      - name: Install vsce + ovsx
        working-directory: vscode-extension
        run: npm ci && npm install -g @vscode/vsce ovsx
      - name: Package VSIX
        working-directory: vscode-extension
        run: vsce package --target ${{ matrix.target }} -o ../flow-${{ matrix.target }}.vsix
      - uses: actions/upload-artifact@v4
        with:
          name: vsix-${{ matrix.target }}
          path: flow-${{ matrix.target }}.vsix

  publish:
    needs: build-server
    runs-on: ubuntu-latest
    if: startsWith(github.ref, 'refs/tags/v')
    steps:
      - uses: actions/download-artifact@v4
        with: { path: artifacts }
      - name: Publish to VSCode Marketplace
        uses: HaaLeo/publish-vscode-extension@v2
        with:
          pat: ${{ secrets.VSCE_PAT }}
          extensionFile: artifacts/vsix-linux-x64/flow-linux-x64.vsix
          registryUrl: https://marketplace.visualstudio.com
      # repeat the publish step for win32-x64, darwin-x64, darwin-arm64
      - name: Publish to OpenVSX
        uses: HaaLeo/publish-vscode-extension@v2
        with:
          pat: ${{ secrets.OVSX_PAT }}
          extensionFile: artifacts/vsix-linux-x64/flow-linux-x64.vsix
      # repeat for the other 3 targets
```

**Caveats:**
- This is the ONLY CI workflow in the repo (none exists today). Keep it focused on Phase 17 concerns; do NOT add `dotnet test` gating here (CONTEXT §Integration Points: "Runs alongside the existing `dotnet test` workflow; does not replace it" — so a future test workflow is a separate file).
- `VSCE_PAT` and `OVSX_PAT` GitHub Secrets must exist before first tag push; OpenVSX namespace must be claimed once via `npx ovsx create-namespace` (Pitfall 8). These are Wave 0 / Wave 3 setup tasks.
- Pitfall 7 RID↔target mapping MUST be exactly as above. The `win32-x64` vs `win-x64` gotcha is silent.

---

### `scripts/lsp-smoke.sh` (smoke script, event-driven)

**Analog:** NEW IDIOM — no `scripts/` dir in repo.

**Responsibilities (per RESEARCH Pitfall 4 + §Validation D-14):**
- Accept a binary path as `$1`.
- Pipe an LSP `initialize` JSON-RPC request + `shutdown` + `exit` notification into the binary's stdin.
- Assert exit code 0 within a timeout.
- Used by both CI (per-platform smoke) and local dev sanity checks.

**Minimal shape (Bash with heredoc + `timeout`; LSP frames need `Content-Length:` header):**
```bash
#!/usr/bin/env bash
set -euo pipefail
BIN="$1"
# Construct Initialize + Shutdown + Exit messages with proper Content-Length framing.
# Pipe through 'timeout 10s' and assert clean exit.
```

**Caveats:**
- OmniSharp framework insists on the `Content-Length: N\r\n\r\n<json>` framing per LSP spec. Hand-writing the bytes in bash is error-prone; consider shelling out to a tiny node/python helper in the same script if bash strings get too fragile.
- CI runs this on 4 platforms. Bash on Windows runner works via `shell: bash` (Git Bash). No PowerShell equivalent needed.

---

### `docs/editor-setup/nvim-lspconfig.lua` + `helix-languages.toml` (docs, static)

**Analog:** NEW IDIOM — no editor-config precedent in repo. Contents should be minimal working configs:

**`nvim-lspconfig.lua` skeleton:**
```lua
require'lspconfig'.configs.flow = {
  default_config = {
    cmd = { 'flow-lsp' },  -- assume on PATH
    filetypes = { 'flow' },
    root_dir = require'lspconfig'.util.root_pattern('.git', '.flowproject'),
  },
}
```

**`helix-languages.toml` skeleton:**
```toml
[[language]]
name = "flow"
file-types = ["flow"]
language-server = { command = "flow-lsp" }
```

**Caveats:**
- Both assume `flow-lsp` is on `PATH`. Ship docs alongside explaining how to build or download the binary for non-VSCode users.
- Keep these SHORT — the goal (per CONTEXT D-13) is "≥1 snippet" to lower the barrier for non-VSCode editors. Comprehensive coverage is out of scope.

---

## Shared Patterns

### Parse-pipeline reuse (no audio)
**Source:** `flow-lang/Core/FlowEngine.cs:59-78` (the lex→parse steps, WITHOUT the interpreter step).
**Apply to:** `flow-lsp/ParseSession.cs`, all handlers that need an AST.
**Excerpt:**
```csharp
var lexer = new SimpleLexer(source, _errorReporter, fileName);
var tokens = lexer.Tokenize();
if (_errorReporter.HasErrors) return false;
var parser = new Parser(tokens, _errorReporter);
var program = parser.Parse();
```

### Error-reporter soft-failure contract
**Source:** `flow-lang/Diagnostics/ErrorReporter.cs:7-46`.
**Apply to:** `ParseSession`, `DiagnosticsPublisher`.
**Contract:** `ErrorReporter` accumulates up to 50 errors; `.Errors` always returns the accumulated list; parser + lexer never throw — they call `ReportError` and continue. LSP handlers read `.Errors` at the end of each parse, never wrap parse in a try/catch for error collection.

### Source-location 1-based → LSP 0-based
**Source:** `flow-lang/Core/SourceLocation.cs:6` (`record SourceLocation(int Line, int Column, ...)`).
**Apply to:** `LspMappings.cs`, `DiagnosticsPublisher`, `SemanticTokensHandler`, `DefinitionHandler`.
**Rule:** Subtract 1 from both Line and Column; guard with `Math.Max(0, x-1)` for `SourceLocation.Unknown` (0,0).

### `@`-prefix stdlib resolution
**Source:** `flow-lang/Runtime/ModuleLoader.cs:113-127`.
**Apply to:** `StdlibSymbolIndex.cs`, `DefinitionHandler.cs`.
**Recommendation:** Extract to `public static string ResolveStdlibPath(string name)` on `ModuleLoader` (or a new helper) so both the evaluator and the LSP use the same code path — ensures stdlib `.flow` files are found at the same location on both sides.

### Pattern-match dispatch over AST records
**Source:** All of `flow-lang/Interpreter/ExpressionEvaluator.cs` and `flow-lang/Interpreter/Interpreter.cs` (per CLAUDE.md §C# Conventions).
**Apply to:** `UserSymbolIndex.cs`, `NoteStreamContext.cs`.
**Rule:** `switch (s) { case ProcDeclaration pd: ... ; case VariableDeclaration vd: ... ; ... }` — no visitor pattern (not established in this repo).

### xUnit Fact shape for phase tests
**Source:** `flow-lang.Tests/Unit/Phase14/LexerTests.cs:1-41`.
**Apply to:** All 10 files in `flow-lang.Tests/Unit/Phase17/`.
**Template:**
```csharp
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

public class <Name>Tests
{
    // helper methods for fixtures
    [Fact] public void Case_Name() => Assert.Equal(expected, actual);
    // or [Theory] + [InlineData(...)] for parameterized
}
```

---

## No Analog Found

These files are **net-new idioms** — no close match exists in the codebase. The executor must lean on RESEARCH.md §Code Examples and external docs cited there.

| File | Role | Primary External Reference |
|------|------|----------------------------|
| `flow-lsp/DocumentManager.cs` | debounce + cancel | RESEARCH §Code Examples Document manager with debounce |
| `flow-lsp/Handlers/*.cs` (except SemanticTokens) | OmniSharp handler contracts | OmniSharp sample server at github.com/OmniSharp/csharp-language-server-protocol/blob/master/sample/SampleServer/Program.cs |
| `flow-lsp/Handlers/SemanticTokensHandler.cs` | 5-tuple delta encoding | LSP 3.17 spec + RESEARCH §Code Examples Semantic tokens |
| `vscode-extension/package.json` | contributes manifest | RESEARCH §Code Examples package.json |
| `vscode-extension/src/extension.ts` | LanguageClient activation | RESEARCH §Code Examples TS activation |
| `vscode-extension/syntaxes/flow.tmLanguage.json` | regex grammar | RESEARCH §Pattern 1 + D-05 scope conventions |
| `vscode-extension/tsconfig.json`, `.vscodeignore`, `language-configuration.json` | standard VSCode extension scaffolding | code.visualstudio.com/api/language-extensions/overview |
| `vscode-extension/snippets/flow.code-snippets` | VSCode snippet format | code.visualstudio.com/api/language-extensions/snippet-guide |
| `.github/workflows/publish-extension.yml` | CI matrix + dual publish | RESEARCH §Code Examples Per-platform CI workflow |
| `scripts/lsp-smoke.sh` | LSP initialize+shutdown over stdio | LSP 3.17 spec framing |
| `docs/editor-setup/nvim-lspconfig.lua`, `helix-languages.toml` | editor-specific LSP configs | nvim-lspconfig README, Helix manual |

**Executor note:** For all NEW IDIOM files, copy the RESEARCH.md code sketches verbatim as a starting point, then adapt for project-specific naming (namespace `FlowLsp`, file-scoped namespaces, nullable-enabled, etc.). These examples are **not compile-tested** (RESEARCH §Metadata Confidence breakdown), so expect minor signature drift in OmniSharp 0.19.9 APIs.

---

## Metadata

**Analog search scope:**
- `flow-lang/` (all subdirs)
- `flow-interpreter/`
- `flow-editor/Editor/`
- `flow-midi/`
- `flow-lang.Tests/Unit/`
- `.github/` (does not exist yet)
- `scripts/` (does not exist yet)
- `docs/` (exists but only `plans/` subdir — no prior editor-config)

**Files scanned:** ~25 (csproj × 5, entry points × 3, FlowEngine, ModuleLoader, SourceLocation, FlowError, DiagnosticLevel, ErrorReporter, InternalFunctionRegistry, BuiltInFunctions, Harmony/HarmonyFunctions, FlowSyntaxHighlighter, Phase 14 tests × 2, MusicalContextStatement, NoteStreamExpression, EffectsFunctions, test file listings, solution file, stdlib `.flow` filenames)

**Pattern extraction date:** 2026-04-20

**Cross-cutting executor reminders:**
1. All new `.cs` files under `flow-lsp/` use file-scoped namespace `FlowLsp` (or sub-namespaces like `FlowLsp.Handlers`, `FlowLsp.Symbols`).
2. `net10.0` target, not `net9.0` (Pitfall 1).
3. No `AudioPlaybackManager`, no `Interpreter`, no `FlowEngine` inside `flow-lsp/` (Pitfall 3).
4. No `PublishTrimmed` (Pitfall 4).
5. `vsce --target` names differ from .NET RIDs (Pitfall 7: `win32-x64` vs `win-x64`, `darwin-x64` vs `osx-x64`).
6. Stdlib `.flow` files must ship beside the published binary (Pitfall 6).
