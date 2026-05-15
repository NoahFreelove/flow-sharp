# Phase 31: LSP Enhancements + JetBrains Stretch — Research

**Researched:** 2026-05-11
**Domain:** LSP server + VSCode TextMate grammar + JetBrains plugin scaffolding (LSP4IJ)
**Confidence:** HIGH

## Summary

Phase 31 closes four `flow-lsp` gaps (diagnostics severity expansion, context-aware completion filtering, varargs visibility in signature help/hovers, and TextMate grammar polish) and adds JetBrains plugin scaffolding via LSP4IJ. The work is heavily codebase-grounded — every change extends an already-established pattern (Phase 17 analyzer + index shape, Phase 24 `CombinedDiagnosticsPublisher` orchestration, varargs already-modeled in `FunctionSignature.IsVarArgs`). The only NEW external dependency is the LSP4IJ Gradle artifact for the JetBrains stretch.

**Two findings reshape the plan:**

1. **`Note:` is ALREADY a recognized comment in `SimpleLexer.cs:1144`** (`IsStartOfLineContent()`-gated). REQ-4's "new `Note:` lead-in" is partially already done — only the TextMate grammar side needs the comment-form scope. This shrinks one piece of REQ-4 scope.
2. **`;` is a Semicolon TOKEN, NOT free whitespace** — used by Parser as a statement terminator (pragmas: `enable hAsB;`, declarations: `Int x = 5;`, flow chains: `5 -> doubler;`). Making `;` a line-comment is **NOT** simply additive lexer work — it requires either (a) reinterpreting `;` only when it appears in a column that isn't a valid token position (semantically fragile, breaks 70+ test files), or (b) the safer locked-decision interpretation: `;` becomes a comment when followed by whitespace+non-token-text and is preceded by no expression — but this is ambiguous in practice. See **Critical Decision** below.

**Primary recommendation:** Plan the lexer changes in four discrete tasks, sequenced (a) `;` line-comment with explicit statement-terminator preservation policy, (b) `TODO:` / `FIXME:` lead-ins (`Note:` already shipping), (c) varargs ellipsis rendering, (d) three new analyzers. Plan completion filtering as one task that wraps `BuildItems` with three filter functions (pure transforms on the existing 5-source merge). Plan the JetBrains scaffolding as TWO tasks: minimum scaffolding (REQ-7 mandatory per D-10), then stretch verification (manual UAT). Pin LSP4IJ to `com.redhat.devtools.intellij:lsp4ij:0.19.3` with IntelliJ Platform `2024.2` (build `242`).

## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01 [varargs-glyph]** Variadic parameters render with the Unicode horizontal ellipsis `…` (U+2026) — NOT three ASCII dots. Format: `(concat str: String…)`.
- **D-02 [varargs-position]** Ellipsis trails the parameter type, not the parameter name. Format: `name: Type…`. Matches Java / TypeScript / C# variadic-rendering convention.
- **D-03 [scalelint-default-on]** Promote `ScaleLintAnalyzer` from opt-in pragma to default-on Information severity. The `enable scaleLint;` pragma remains accepted as a no-op.
- **D-04 [scalelint-no-opt-out]** No language-level opt-out. Composers silence via editor settings or by avoiding `key { }` blocks.
- **D-05 [diagnostic-source-preserved]** Diagnostic source string stays `"flow.scaleLint"` (locked in Phase 24 D-18).
- **D-06 [scope-suffix]** Standard scopes with `.flow` language suffix — `entity.name.function.flow`, `comment.line.semicolon.flow`, etc.
- **D-07 [comment-scopes]**
  - `;` line comment → `comment.line.semicolon.flow`
  - `Note:` lead-in → `comment.line.documentation.flow`
  - `TODO:` lead-in → `comment.line.todo.flow`
  - `FIXME:` lead-in → `comment.line.fixme.flow`
- **D-08 [function-call-scope]** `entity.name.function.flow` for `(funcName …)` head positions; `variable.other.flow` for bare identifier references.
- **D-09 [lsp4ij-pin]** Pin LSP4IJ to a specific tested version in Gradle build (NO floating `latest` / `+` ranges).
- **D-10 [scaffolding-always-lands]** `flow-jetbrains/` directory always lands at phase closure regardless of stretch outcome.

### Claude's Discretion

- Exact LSP4IJ version pin number — **researched: 0.19.3** (latest stable as of 2026-04-15).
- Grammar snapshot tests: `vscode-tmgrammar-snap` already in `package.json` devDeps; snapshots already in `vscode-extension/tests/grammar/*.flow.snap` — use the existing toolchain.
- Whether in-repo `.flow` audit (REQ-6) emits a migration script or is done manually — **researched: do manually, only 2 collision sites exist (see Pitfall 4)**.
- Whether `DiagnosticsPublisher.cs` is extended in-place vs. adding `StructuredDiagnosticsPublisher.cs` — **researched: extend `CombinedDiagnosticsPublisher` in place per Phase 24 D-04 analyzer-per-diagnostic-type pattern; do not invent a new publisher**.

### Deferred Ideas (OUT OF SCOPE)

- VSCode Marketplace + OpenVSX publish (defer to v1.5).
- JetBrains Marketplace publish (Phase 31 ships only a .zip).
- macOS / Windows certification of the LSP and VSCode extension.
- LSP code actions, refactors, formatter, rename-symbol, find-references.
- Real-time LSP latency benchmarking.
- Auto-import quick fixes.
- Multi-line `Note:` block comments.
- Customizable comment-style mapping.
- `;`-as-statement-separator semantics (Flow stays whitespace-significant).
- LSP telemetry.
- Markdown-embedded Flow code-block highlighting.
- Three-velocity-layer piano (carried over from Phase 29 v1.5 backlog).

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SPEC-1 | Structured diagnostic severity expansion (UnusedImport Warning, UnreachableSection Information, OutOfKey default-Information, ShadowedVariable Warning) | Standard Stack §"Diagnostics expansion"; Architecture §"Analyzer-per-diagnostic-type"; Code Examples §"Adding an analyzer" |
| SPEC-2 | Context-aware completion filtering (Import-filter, Pragma-filter, Musical-context-filter) | Architecture §"Completion filter pipeline"; Code Examples §"Wrapping `BuildItems` with filters" |
| SPEC-3 | Varargs visibility in signature help + hovers | Standard Stack §"Varargs ellipsis"; Code Examples §"Rendering `FunctionSignature` with U+2026" |
| SPEC-4 | Grammar enhancements — new comment forms (`;`, `Note:`, `TODO:`, `FIXME:`) | Pitfall 1 §"Semicolon collision"; Code Examples §"Lexer comment-form additions"; State-of-the-Art §"Rust grammar reference" |
| SPEC-5 | Grammar enhancement — function-call coloring | Code Examples §"`entity.name.function` regex"; State-of-the-Art §"Rust grammar reference" |
| SPEC-6 | Lexer migration of in-repo v1.3 fixtures | Pitfall 4 §"Token-collision audit"; Standard Stack §"`vscode-tmgrammar-snap` snapshot regen" |
| SPEC-7 | JetBrains plugin stretch | Standard Stack §"LSP4IJ 0.19.3"; Architecture §"`flow-jetbrains/` minimum file set"; Code Examples §"plugin.xml + LanguageServerFactory" |

## Project Constraints (from CLAUDE.md)

- **.NET 10 / C# 13 only** — `flow-lsp/flow-lsp.csproj` targets `net10.0`. No regression.
- **Minimal-dependencies philosophy** — NO new NuGet dependencies on the LSP side. `OmniSharp.Extensions.LanguageServer 0.19.9` stays the only LSP dep.
- **Two-run byte-identical determinism** — Phase 18/25/27/28 `ByteIdentical*Tests` must stay GREEN. New lexer must produce identical token streams across two runs of the same source. **This is preserved naturally** because: (a) new comment forms emit zero tokens (whitespace-equivalent); (b) the only flow-lang touch in the LSP-side gaps is the lexer.
- **Prefix-only arithmetic** — irrelevant to Phase 31 (no arithmetic surface).
- **Music-first ergonomics** — `Note:` already a comment because it's idiomatic in `tutorial.flow`; this phase formalizes it.
- **Charitable interpretation** (per `feedback_charitable_interpretation` memory) — diagnostic messages stay helpful (the Phase 24 `ScaleLintAnalyzer` "try F4 or G4" pattern is the precedent).
- **Pre-public lean** (per `project_pre_public_no_legacy_burden` memory) — backward compat with v1.3 scripts intentionally NOT preserved; in-repo migrations land in Phase 31.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| New comment forms (`;`, `TODO:`, `FIXME:`) | flow-lang Lexing (`SimpleLexer.cs:SkipWhitespaceAndComments`) | flow-lsp Semantic / LspMappings (token consumers — none should care; whitespace-equivalent) | The lexer owns token boundaries; everything downstream sees fewer tokens after a comment is skipped. |
| 3 new diagnostic analyzers | flow-lsp Diagnostics (sibling files of `ScaleLintAnalyzer.cs`) | flow-lsp Handlers `CombinedDiagnosticsPublisher` (wire-up) | Phase 24 D-04 locks "analyzer-per-diagnostic-type" — one file per analyzer. |
| Completion filters (import / pragma / musical-context) | flow-lsp Handlers `CompletionHandler` (static helpers in `BuildItems`) | flow-lsp Symbols (read-only consumers — `StdlibSymbolIndex` exposes module membership, no change) | `BuildItems` is the existing entry point; filtering is a pure post-processing transform. |
| Varargs ellipsis rendering | flow-lsp `LspMappings.cs` (new `FormatSignature` helper) | flow-lsp Handlers `SignatureHelpHandler`, `HoverHandler`, `Symbols/BuiltInIndex` (consumers) | The data lives in `FunctionSignature.IsVarArgs` already; the formatter is the missing layer. |
| TextMate grammar updates | vscode-extension `syntaxes/flow.tmLanguage.json` | vscode-extension `tests/grammar/*.flow.snap` (regenerated) | Pure grammar JSON edit; existing `vscode-tmgrammar-snap` toolchain regenerates snapshots. |
| JetBrains plugin scaffolding | `flow-jetbrains/` (new Gradle/Kotlin module) | `flow-lsp` (consumed unchanged via `OSProcessStreamConnectionProvider`) | LSP4IJ delegates to the flow-lsp binary via stdio — flow-lsp is reused as-is. |
| In-repo `.flow` fixture migration | `examples/`, `tests/`, `flow-lang/*.flow` | `flow-lang.Tests/Unit/Phase{18,25,27,28}/ByteIdentical*Tests.cs` (regression gates) | The migration is per-file content edits; the regression gates catch silent breakage. |

## Standard Stack

### Core (unchanged)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 10 SDK | 10.0.107 | C# 13 runtime | `flow-lsp.csproj` already targets `net10.0`; CLAUDE.md mandates [VERIFIED: `dotnet --list-sdks`] |
| OmniSharp.Extensions.LanguageServer | 0.19.9 | LSP framework | Already pinned in `flow-lsp.csproj`; Phase 17 D-01 locked this choice; no new dep [VERIFIED: csproj contents] |
| xunit (via flow-lang.Tests) | existing | Test framework for `flow-lang.Tests/Unit/Phase31/` | Phase 17/24 pattern (`Phase17/*Tests.cs`, `Phase24/*Facts.cs`) [VERIFIED: codebase ls] |

### New for JetBrains Stretch (REQ-7 only)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| LSP4IJ | **0.19.3** | LSP-to-IntelliJ-Platform bridge | Latest stable release (2026-04-15) per GitHub releases API. Maven coords `com.redhat.devtools.intellij:lsp4ij:0.19.3` [VERIFIED: GitHub API `/repos/redhat-developer/lsp4ij/releases?per_page=10`] [CITED: gradle.properties pluginGroup line] |
| IntelliJ Platform Gradle Plugin | 2.x latest | Build wrapper for IntelliJ plugin | Standard JetBrains plugin tooling [CITED: github.com/redhat-developer/lsp4ij/blob/main/build.gradle.kts] |
| Java 17 (toolchain 21) | OpenJDK 21.0.10 installed | LSP4IJ baseline | LSP4IJ targets Java 17 byte compat with Java 21 toolchain; system has JDK 21 [VERIFIED: `java -version`] [CITED: LSP4IJ build.gradle.kts: "compatibility ... restricted to Java 17"] |
| Kotlin (LSP4IJ-provided) | 1.9.x | Plugin language (or Java — both fine) | LSP4IJ's own factory pattern is Java in docs but Kotlin works equally — choose Kotlin to match modern JetBrains plugin convention [CITED: DeveloperGuide.md LanguageServerFactory snippet (Java)] |

**Version verification command:** `curl -s "https://api.github.com/repos/redhat-developer/lsp4ij/releases?per_page=1" | jq '.[0].tag_name'` returns `"0.19.3"` as of 2026-05-11 [VERIFIED: invoked].

### Supporting (already present)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| vscode-tmgrammar-test | 0.1.3 | Grammar snapshot testing | REQ-5 acceptance — regenerate snapshots after grammar edits [VERIFIED: vscode-extension/package.json line 58] |
| vscode-tmgrammar-snap (CLI) | bundled | Snapshot regen script | Already wired as `npm run test:grammar` and `npm run test:grammar:update` [VERIFIED: package.json scripts] |
| @vscode/vsce | 3.9.1 | VSIX packaging | Existing extension build (irrelevant — no publish this phase) [VERIFIED: package.json devDeps] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| LSP4IJ | `lsp-intellij` (community Kotlin LSP client) | LSP4IJ is the Red Hat first-party bridge with Quarkus/Liberty Tools as production references. Choosing the community alt = more glue code, no clear benefit. SEED-001 explicitly names LSP4IJ. |
| Extending `DiagnosticsPublisher.cs` in place | New `StructuredDiagnosticsPublisher.cs` sibling | Phase 24 already extends via `CombinedDiagnosticsPublisher` orchestrator pattern. A second publisher class would invent a new dispatch shape. Stay with the existing orchestrator + analyzer-per-diagnostic-type pattern. |
| Building lexer comments BEFORE TextMate grammar | TextMate first, lexer second | The lexer change is the riskier of the two (token-stream impact, byte-identical determinism gate). Land the grammar in a separate task so REQ-5 (function-call coloring) can ship even if a lexer pitfall surfaces. |
| New `StructuredDiagnostic` AST type in flow-lang | Use LSP `Diagnostic` directly in analyzers | flow-lang stays touched only via lexer; Phase 24 D-04 "zero flow-lang touch for LSP-only work" already locked this — analyzers construct `Diagnostic` instances directly. |

**Installation:** No new NuGet packages. JetBrains stretch:
```bash
# Once flow-jetbrains/ scaffolding exists:
cd flow-jetbrains
./gradlew buildPlugin    # produces flow-jetbrains/build/distributions/flow-jetbrains-0.1.0.zip
```

## Architecture Patterns

### System Architecture Diagram

```
                        Phase 31 — Data Flow
                        ====================

  user edits .flow         opens .flow file
       buffer                    │
         │                       │
         │                       ▼
         │           +-----------------------+
         │           │  VSCode (TextMate     │   <-- REQ-4 + REQ-5
         │           │  flow.tmLanguage.json │       grammar updates
         │           │  baseline coloring)   │
         │           +-----------------------+
         │                       │
         │                       │ activate
         ▼                       ▼
  +-----------------+   +----------------------+
  │  flow-lsp       │<--│ vscode-languageclient│
  │  (OmniSharp)    │   │ (already wired)      │
  └────────┬────────┘   └──────────────────────┘
           │
           │  textDocument/didChange
           ▼
  +-----------------+
  │  ParseSession   │   SimpleLexer + Parser
  │  cached         │   (REQ-4 LEXER CHANGE: +;, +TODO:, +FIXME:)
  └─────────────────┘   (Note: already shipping)
           │
           │ ParseResult { Ast, Tokens, Errors, Pragmas }
           │
           ▼
  +-------------------------------------------+
  │  CombinedDiagnosticsPublisher (extended)  │   <-- REQ-1
  │  ┌─────────────────────────────────────┐  │
  │  │  DiagnosticsPublisher (parse errs)  │  │
  │  │  ScaleLintAnalyzer (NOW default-on) │  │  <-- D-03
  │  │  UnusedImportAnalyzer     [NEW]     │  │
  │  │  UnreachableSectionAnalyzer [NEW]   │  │
  │  │  ShadowedVariableAnalyzer [NEW]     │  │
  │  └─────────────────────────────────────┘  │
  └─────────────────────┬─────────────────────┘
                        │ Diagnostic[] (mixed severity)
                        ▼
                publishDiagnostics

  user requests completion        cursor over symbol
           │                             │
           ▼                             ▼
  +-----------------+        +-----------------+
  │ CompletionHandler│       │  HoverHandler   │
  │  BuildItems     │        │   BuildHover    │
  │   default merge │        │   3-way lookup  │
  │       │         │        │       │         │
  │       ▼         │        │       ▼         │
  │  +-FILTERS-+    │ <-REQ-2│  +-VARARGS-+    │ <-REQ-3
  │  │ Import  │    │        │  │ render  │    │
  │  │ Pragma  │    │        │  │ Type… │    │
  │  │ MusicCtx│    │        │  └─────────┘    │
  │  +─────────+    │        +-----------------+
  └─────────────────┘
                                                   user opens .flow in IntelliJ
                                                              │
                                                              ▼
                                                   +-----------------------+
                                                   │ flow-jetbrains/       │  <-- REQ-7
                                                   │   plugin.xml          │
                                                   │   LanguageServerFactory│
                                                   └───────────┬───────────┘
                                                               │ stdio
                                                               ▼
                                                   +-----------------------+
                                                   │ flow-lsp binary       │  (reused unchanged)
                                                   │ (OSProcessStream...)  │
                                                   └───────────────────────┘
```

### Recommended Project Structure

```
flow-lsp/
├── Diagnostics/
│   ├── CombinedDiagnosticsPublisher.cs     (EXTEND — wire new analyzers)
│   ├── DiagnosticsPublisher.cs              (unchanged)
│   ├── ScaleLintAnalyzer.cs                 (D-03: flip activation gate)
│   ├── IScaleLintPublisher.cs               (unchanged)
│   ├── ScaleLintPublisher.cs                (unchanged)
│   ├── DiatonicSpellings.cs                 (unchanged)
│   ├── UnusedImportAnalyzer.cs              [NEW]
│   ├── UnreachableSectionAnalyzer.cs        [NEW]
│   └── ShadowedVariableAnalyzer.cs          [NEW]
├── Handlers/
│   ├── CompletionHandler.cs                 (EXTEND BuildItems with 3 filters)
│   ├── HoverHandler.cs                      (EXTEND — varargs ellipsis in signature render)
│   ├── SignatureHelpHandler.cs              (EXTEND — varargs ellipsis in SignatureInformation.Label)
│   └── ...                                  (others unchanged)
├── LspMappings.cs                           (ADD FormatSignature helper for varargs U+2026)
└── ...

flow-lang/Lexing/
└── SimpleLexer.cs                           (EXTEND SkipWhitespaceAndComments with ;, TODO:, FIXME:)
                                             (Note: already at line 1144)

vscode-extension/
├── syntaxes/
│   └── flow.tmLanguage.json                 (EXTEND — comment-line patterns + function-call scope)
├── tests/grammar/                           (REGENERATE *.flow.snap after grammar edits)
└── language-configuration.json              (EXTEND — lineComment array now includes ";")

flow-jetbrains/                              [NEW DIRECTORY — D-10 always lands]
├── build.gradle.kts
├── settings.gradle.kts
├── gradle.properties
├── gradle/wrapper/                          (gradlew + gradle-wrapper.jar — std skeleton)
├── gradlew
├── gradlew.bat
└── src/main/
    ├── kotlin/dev/flowlang/jetbrains/
    │   └── FlowLanguageServerFactory.kt    (LSP4IJ contract; spawns flow-lsp via stdio)
    └── resources/META-INF/
        └── plugin.xml                       (LSP4IJ extension declarations)
```

### Pattern 1: Analyzer-per-Diagnostic-Type (Phase 24 D-04 — REUSED)

**What:** Each new diagnostic kind = one new file in `flow-lsp/Diagnostics/`. Pure static analyzer; never throws; returns `IReadOnlyList<Diagnostic>`. Activation gate inside `Analyze()`.

**When to use:** Every REQ-1 new analyzer (UnusedImport, UnreachableSection, ShadowedVariable) AND the D-03 promotion of ScaleLintAnalyzer to default-on.

**Example:**
```csharp
// Source: flow-lsp/Diagnostics/ScaleLintAnalyzer.cs (existing pattern)
public static class UnusedImportAnalyzer
{
    public static IReadOnlyList<Diagnostic> Analyze(
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source)
    {
        // Walk ast.Statements for ImportStatement instances
        var imports = ast.Statements.OfType<ImportStatement>().ToList();
        if (imports.Count == 0) return Array.Empty<Diagnostic>();

        // Walk the rest of the AST looking for identifier references that
        // resolve to symbols from each import's module. (StdlibSymbolIndex
        // already maps proc → module; reuse it.)
        // Any unused import → Warning diagnostic, Source="flow.unusedImport"
        ...
    }
}
```

### Pattern 2: `CombinedDiagnosticsPublisher.BuildAll` — Single Wire-Level Publish

**What:** ONE `_server.TextDocument.PublishDiagnostics` call per URI per parse cycle. Multiple analyzers' diagnostics merged into one `Container<Diagnostic>`. The published-replaces-prior semantics is preserved.

**When to use:** REQ-1 routes ALL new analyzers through this. Do NOT add new publishers per analyzer.

**Example:**
```csharp
// Source: flow-lsp/Diagnostics/CombinedDiagnosticsPublisher.cs:48 (existing)
public static IReadOnlyList<Diagnostic> BuildAll(ParseResult result, string source)
{
    var parseDiags  = DiagnosticsPublisher.BuildDiagnostics(result.Errors);
    var lintDiags   = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, source);
    var unusedDiags = UnusedImportAnalyzer.Analyze(result.Ast, result.Tokens, source);  // NEW
    var unreachDiags = UnreachableSectionAnalyzer.Analyze(result.Ast, result.Tokens, source);  // NEW
    var shadowDiags = ShadowedVariableAnalyzer.Analyze(result.Ast, result.Tokens, source);  // NEW
    // ...merge as one List<Diagnostic>...
}
```

### Pattern 3: Pure-Static-Helpers for Test Access (Phase 17 idiom)

**What:** Every handler exposes a pure `static` method (`BuildItems`, `BuildHover`, `BuildDiagnostics`, `DetectCall`, `BuildAll`) that takes primitive inputs (text, position, indices) and returns the LSP wire shape. Unit tests call the static method directly without OmniSharp transport.

**When to use:** Every new code path in Phase 31. The 10-second test-runtime-budget constraint depends on this.

**Example (existing):**
```csharp
// flow-lsp/Handlers/CompletionHandler.cs:93
public static IEnumerable<CompletionItem> BuildItems(
    DocumentUri uri, string text, FlowProgram? ast, IReadOnlyList<Token>? tokens,
    Position cursor, BuiltInIndex builtIns, UserSymbolIndex users,
    StdlibSymbolIndex stdlib, KeywordIndex keywords)
{ ... }
```

For REQ-2 the new filter functions become additional static helpers:
```csharp
// NEW: pure transforms on the merged item list
public static IEnumerable<CompletionItem> FilterByImports(
    IEnumerable<CompletionItem> items, FlowProgram ast, StdlibSymbolIndex stdlib) { ... }
public static IEnumerable<CompletionItem> FilterByPragmas(
    IEnumerable<CompletionItem> items, PragmaSet pragmas) { ... }
public static IEnumerable<CompletionItem> BoostByMusicalContext(
    IEnumerable<CompletionItem> items, FlowProgram ast, IReadOnlyList<Token> tokens,
    string text, Position cursor) { ... }
```

### Pattern 4: TextMate Grammar — Function-Call Distinction (from Rust grammar)

**What:** Match identifier + lookahead-or-consumed `(` for function calls; bare identifiers elsewhere fall through to `variable.other.flow`.

**When to use:** REQ-5 grammar enhancement.

**Example (Rust reference, adapted to Flow):**
```json
{
  "function-call": {
    "match": "\\b([A-Za-z_][A-Za-z0-9_]*)(?=\\s*\\()",
    "captures": {
      "1": { "name": "entity.name.function.flow" }
    }
  },
  "variable-ref": {
    "match": "\\b[A-Za-z_][A-Za-z0-9_]*\\b",
    "name": "variable.other.flow"
  }
}
```

Order matters: `function-call` MUST precede `variable-ref` in the patterns array so calls take precedence over the generic-identifier fallthrough. Existing patterns (chords, notes, types, booleans, keywords) keep their precedence and are NOT affected because each matches a more specific shape than `[A-Za-z_]`.

### Pattern 5: LSP4IJ Plugin via `OSProcessStreamConnectionProvider`

**What:** Implement `LanguageServerFactory` that returns a `StreamConnectionProvider` spawning the `flow-lsp` binary via stdio. plugin.xml declares the LSP4IJ extension + language mapping.

**When to use:** REQ-7 scaffolding (D-10 always lands).

**Example (adapted from LSP4IJ DeveloperGuide.md):**
```kotlin
// src/main/kotlin/dev/flowlang/jetbrains/FlowLanguageServerFactory.kt
package dev.flowlang.jetbrains

import com.intellij.execution.configurations.GeneralCommandLine
import com.intellij.openapi.project.Project
import com.redhat.devtools.lsp4ij.LanguageServerFactory
import com.redhat.devtools.lsp4ij.server.OSProcessStreamConnectionProvider
import com.redhat.devtools.lsp4ij.server.StreamConnectionProvider

class FlowLanguageServerFactory : LanguageServerFactory {
    override fun createConnectionProvider(project: Project): StreamConnectionProvider {
        // For dev: assume `flow-lsp` is on PATH after `flow install` (Phase 30).
        // For shipped plugin: bundle binaries under resources/lsp/ — DEFER to v1.5.
        val cmd = GeneralCommandLine("flow-lsp")
        return object : OSProcessStreamConnectionProvider() {
            init { commandLine = cmd }
        }
    }
}
```

```xml
<!-- src/main/resources/META-INF/plugin.xml -->
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

### Anti-Patterns to Avoid

- **Adding a new diagnostic publisher per analyzer:** Breaks single-wire-publish invariant in `CombinedDiagnosticsPublisher`. LSP `publishDiagnostics` REPLACES per-URI — a second publish clobbers the first.
- **Filtering completions by modifying the SOURCES (BuiltInIndex etc.):** The 5 indices are pre-built at startup; filtering must happen at request time in `BuildItems`, NOT by rebuilding the indices.
- **Inventing a new TextMate scope hierarchy (`flow.note.something`):** Locked by D-06. Use standard scopes with `.flow` suffix only.
- **Embedding flow-lsp binary inside the JetBrains plugin .jar:** Out of scope; D-10 stretch bar is "builds + opens .flow with completions" assuming `flow-lsp` is on PATH (provided by Phase 30's `flow install`). Binary bundling is a v1.5 concern.
- **Trying to make `;` a line-comment globally:** Breaks every pragma + every statement terminator. See Critical Decision below.

## Critical Decision (BLOCKS REQ-4 implementation)

**The `;` Lisp-style line-comment requirement in REQ-4 has a token-collision problem the SPEC does not resolve.**

Current state: `;` is a `TokenType.Semicolon` token (`SimpleLexer.cs:163`). The Parser uses it as a statement terminator in 14 call sites (`Parser.cs:52,63,293,304,481,490,670,679,704,708,727,731,1115,1119,1334`). Every pragma uses it (`enable hAsB;`). Every typed variable declaration uses it (`Int x = 5;`). Every shipped `.flow` script in `examples/pragmas/` has at least one bare `;`.

Three resolution options for the planner to pick from, ordered by recommendation:

**Option A — Position-sensitive `;` comment (RECOMMENDED):**
- `;` at column-0 (with optional leading whitespace) → line comment (consumed by `SkipWhitespaceAndComments`)
- `;` mid-line (any non-whitespace before it on the same logical line) → still a `Semicolon` token
- Mirrors the existing `Note:` / `IsStartOfLineContent()` gate at `SimpleLexer.cs:1159`
- ZERO existing `.flow` files in the repo use `;` at column 0 (verified: 17 bare-semicolon usages found, ALL mid-line after pragmas or assignments)
- Backward-compatible with every shipped script
- **Cost:** Lexer change is ~5 lines (add a `Note:`-style arm for `;` + `IsStartOfLineContent()`)

**Option B — `;;` Lisp-style double-semicolon:**
- `;;` to end-of-line is the comment
- Zero collision with single `;` semicolon-terminator
- More idiomatic Lisp (Emacs/Scheme tradition uses `;;` for top-level comments, `;` for inline)
- **Cost:** Documentation drift — CONTEXT.md D-07 names the scope `comment.line.semicolon.flow`; semantically still fine, but the SPEC says "`;` Lisp-style line comment" which conflicts with this option

**Option C — Remove `;` entirely as a token, repurpose for comment:**
- Statement terminators become whitespace/newline only (Flow is already whitespace-significant per scope)
- Migrate every `.flow` file (~17 sites) to drop the `;`
- **Cost:** High blast radius; touches Parser's 14 `Match(TokenType.Semicolon)` call sites; risks breaking byte-identical determinism gates

**Recommendation:** Plan for Option A. It matches the existing `Note:` precedent verbatim, requires zero downstream parser changes, and passes the byte-identical determinism contract by construction (no token-stream change for any existing valid program). The planner should add this as a locked decision in the plan-phase output (a new D-11 row equivalent).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| LSP protocol stack | Custom JSON-RPC client/server | `OmniSharp.Extensions.LanguageServer` (already in csproj) | Phase 17 already locked this; OmniSharp handles capabilities, content-length framing, request lifecycle. |
| TextMate grammar testing | Hand-rolled regex assertions | `vscode-tmgrammar-snap` (already in package.json) | Snapshot format already in `tests/grammar/*.flow.snap`; regenerate with `npm run test:grammar:update`. |
| MIDI program selection for JetBrains | (irrelevant — wrong domain) | — | — |
| Diatonic spelling derivation | New mode lookup tables | `DiatonicSpellings.cs` + `ScaleDatabase.TryParseKeyWithMode` (Phase 24) | Already exists; D-03 promotes ScaleLintAnalyzer to default-on with no new code. |
| Innermost-key resolution | Brace-tracking walker per analyzer | `NoteStreamContext.FindEnclosingKey` (Phase 17 D-11) | Already used by ScaleLintAnalyzer; reuse verbatim for any other key-scoped analyzer (e.g. roman-numeral musical-context boost). |
| LSP-to-IntelliJ glue | Custom Kotlin LSP4J client | LSP4IJ (D-09 locked) | The Red Hat bridge is the standard; Liberty Tools + Quarkus Tools production references. |
| Function-call vs variable-ref grammar | Custom lookahead engine | Standard TextMate `(?=\\()` lookahead capture (Rust grammar pattern) | Microsoft's TypeScript grammar + Rust grammar both use this — it's the convention. |
| Symbol existence check for unused-import detection | New AST walk per call site | `StdlibSymbolIndex.Find` + `UserSymbolIndex.Find` (Phase 17 indices) | Already returns module-keyed proc data; intersect with `ImportStatement.FilePath` for "is this import referenced?" |
| Varargs detection | Parsing the signature string | Read `FunctionSignature.IsVarArgs` directly | `FunctionSignature` record already exposes the flag; `LspMappings.FormatSignature` just needs to render `…` when set. |

**Key insight:** Almost every Phase 31 requirement extends an existing pattern. The only NET-NEW external moving part is the LSP4IJ Gradle setup. Everything else is delta on shipped code.

## Runtime State Inventory

> This phase is partially a rename/refactor (REQ-6 lexer migration of in-repo `.flow` files).

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| **Stored data** | None — Flow is interpreted, no on-disk databases, no Mem0, no Chroma. The byte-identical regression baselines in `flow-lang.Tests/baselines/Phase28/` are content-pinned WAV/MIDI files keyed by SHA hash, NOT keyed on the string content of `.flow` source. | None |
| **Live service config** | None — flow-lsp is process-per-session, no persistent daemon config | None |
| **OS-registered state** | None — no Windows Task Scheduler / launchd / pm2 / systemd registrations | None |
| **Secrets / env vars** | None — no secrets reference. The `vscode-extension/server/linux-x64` symlink to `flow-lsp/bin/Debug/net10.0/` is a dev-only convenience; survives Phase 31 unchanged. | None |
| **Build artifacts** | (a) `flow-lsp/bin/` + `flow-lsp/obj/` — Roslyn rebuilds clean; (b) `vscode-extension/out/` — TypeScript rebuilds via `npm run compile`; (c) `vscode-extension/tests/grammar/*.flow.snap` — must REGENERATE after grammar edits via `npm run test:grammar:update`; (d) Future `flow-jetbrains/build/` — fresh on first `./gradlew buildPlugin`. | (c) regenerate snapshots as part of REQ-5 task |

**Critical migration items (REQ-6):**

| File | Collision | Migration |
|------|-----------|-----------|
| `tests/test_unpack_flow.flow:8` | `Note: ===== Test 1: 2-element tuple unpack (Note + Note) =====` — at column 0, ALREADY a comment under current lexer (the `Note:` form). No migration needed. | None |
| `tests/test_unpack_flow.flow:10` | `proc renderHit (Note: pitch, Note: dur)` — `Note:` mid-line is a parameter type annotation; `IsStartOfLineContent()` returns false, so it stays a type-annotation token. | None |
| `tests/std.flow:33` | `internal proc chord (Note: noteShapedSymbol)` — same as above; mid-line type annotation. | None |
| `examples/tutorial.flow:224` | `Note: 'Note:' appears at the start of a line as a chapter divider...` — explanatory text INSIDE a `Note:` comment; the outer `Note:` comments-out the entire line. No collision. | None |
| Bare `;` mid-line in 17 test files | These are statement terminators (`enable hAsB;`, `Int r17 = 5 -> doubler;`). Under recommended Option A, mid-line `;` stays a `Semicolon` token — NO migration needed. | None |
| **NOT FOUND:** Any column-0 `;` outside strings/comments | Verified by grep across all 647 `.flow` files | — |
| **NOT FOUND:** Any column-0 `TODO:` or `FIXME:` | Verified by grep across all 647 `.flow` files | — |

**Conclusion:** Under recommended **Option A** for `;` (position-sensitive), REQ-6 migration is **zero files**. The lexer-change risk is fully contained; the byte-identical determinism gates pass naturally. If the planner chooses Option C instead (full removal of `;`), 17 files need migration plus Parser changes — significantly higher risk.

## Common Pitfalls

### Pitfall 1: Semicolon Collision (covered in detail above)

**What goes wrong:** Naively adding `;` to `SkipWhitespaceAndComments` lexes existing pragmas as bare comments — every `enable hAsB;` becomes an unrecognized identifier `enable hAsB` with a comment.

**Why it happens:** `;` is currently a real token used by the Parser; the SPEC says "`;` Lisp-style line comment" without specifying column constraints.

**How to avoid:** Adopt Option A — position-sensitive (`IsStartOfLineContent()`-gated, mirroring the existing `Note:` lexer arm). Document the decision in the plan as a lock.

**Warning signs:** `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs` going RED. `tests/test_h_alias.flow` failing to parse.

### Pitfall 2: TextMate Scope Inheritance Drift

**What goes wrong:** A theme that previously colored `comment.line.double-slash.flow` (existing) does NOT color `comment.line.documentation.flow` / `comment.line.todo.flow` / `comment.line.fixme.flow` / `comment.line.semicolon.flow` because those scope tails are unfamiliar.

**Why it happens:** TextMate scope matching is prefix-based; a theme that targets `comment.line.{any-suffix}.{any-lang}` would match all four, but themes typically target either `comment` (universal) or a specific suffix.

**How to avoid:** Test snapshot tokens after the grammar update — open the regenerated `*.flow.snap` files manually and verify each new scope is present. VSCode's built-in `Developer: Inspect Editor Tokens and Scopes` command surfaces the active scope chain — verify with `examples/tutorial.flow` after the change.

**Warning signs:** A theme renders `TODO:` lines as black/unhighlighted in dev-host smoke. Snapshot diff shows new scope-name strings; user-visible color unchanged.

### Pitfall 3: Varargs Glyph in `SignatureInformation.Label`

**What goes wrong:** LSP `SignatureInformation.Label` is the entire signature string; clients (VSCode, JetBrains) compute `ActiveParameter` offsets BYTEWISE within the string. The `…` Unicode character (U+2026) is **3 bytes in UTF-8** but **1 grapheme**. If the client computes parameter offsets by byte vs char, the active-parameter highlight may misalign.

**Why it happens:** LSP spec says offsets are UTF-16 code units; VSCode and IntelliJ are both consistent — but tests must verify with a 2-arg varargs function (`concat str: String…, str2: String`) to catch the issue.

**How to avoid:** (a) Use `SignatureInformation.Parameters` (array of `ParameterInformation` with explicit `Label` ranges) instead of relying on string offsets within the merged label. This is the LSP-blessed shape. (b) Add a unit test that asserts: given `(concat "a" "b" "c")` with cursor at the second arg, `ActiveParameter` resolves to the varargs param.

**Warning signs:** VSCode highlights the wrong parameter in the signature help popup when cursor is past the ellipsis position.

### Pitfall 4: vscode-tmgrammar-snap Snapshot Staleness

**What goes wrong:** Grammar edits regenerate `flow.tmLanguage.json` but the four `tests/grammar/*.flow.snap` files still pin the OLD scope assignments. CI fails on snapshot diff.

**Why it happens:** snapshot tests assert byte-identical match against the committed `.snap` files.

**How to avoid:** Run `npm run test:grammar:update` in `vscode-extension/` as the LAST step of any plan task that touches `flow.tmLanguage.json`. Commit the regenerated snapshots alongside the grammar change. Add to the plan task's "files changed" expectation.

**Warning signs:** `npm run test:grammar` returns non-zero in CI. Snapshot diff shows new `entity.name.function.flow` / `variable.other.flow` scopes — verify the diff makes semantic sense before accepting.

### Pitfall 5: scaleLint Default-On Without Editor-Side Opt-Out Surprises Composers

**What goes wrong:** A composer writing a deliberately chromatic / borrowed-chord progression in `key Cmajor { ... }` gets 50+ Information squiggles after upgrading to v1.4. They didn't ask for them.

**Why it happens:** Phase 24 D-19 made scaleLint opt-in; D-03 here flips that to default-on. Composers used to v1.3 have ZERO advance notice.

**How to avoid:** (a) Phase closure should announce the default flip in `MILESTONES.md` / release notes. (b) Diagnostic source string `"flow.scaleLint"` (preserved per D-05) lets editors filter independently — document the VSCode setting `"problems.severities"` in the v1.4 release notes. (c) ScaleLintAnalyzer already silent-on-unrecognized-key (D-22 from Phase 24) which is the natural pressure-release for `key Cblues {...}` etc.

**Warning signs:** Composer feedback "Why is everything an Info squiggle now?" — anticipate this and put the per-file silence guidance in the README.

### Pitfall 6: LSP4IJ IntelliJ Platform Baseline Mismatch

**What goes wrong:** Composer's IntelliJ instance is older than 2024.2 (build 242). LSP4IJ 0.19.3 refuses to load.

**Why it happens:** LSP4IJ 0.19.x sets `pluginSinceBuild=242` per `gradle.properties`.

**How to avoid:** Document the IntelliJ 2024.2+ requirement in `flow-jetbrains/README.md`. The phase closure UAT must verify the loaded plugin against a current IntelliJ Community (2025.x recommended) — Phase 31 acceptance for REQ-7 explicitly requires "loading the plugin into a development IntelliJ instance + opening a `.flow` file shows working completions."

**Warning signs:** Plugin load fails with "incompatible build" error. Verify IntelliJ build number meets 242+.

### Pitfall 7: flow-lsp Binary Discoverability from JetBrains

**What goes wrong:** The JetBrains plugin spawns `flow-lsp` via `GeneralCommandLine("flow-lsp")` — but the binary is not on the user's PATH. Plugin loads but reports "language server failed to start."

**Why it happens:** Phase 30 added `flow install` which puts `flow` (the unified binary) on PATH at `~/.local/bin/flow`, but `flow-lsp` (the LSP server) is bundled INSIDE the VSCode extension's `server/` directory and NOT exposed by `flow install` as a top-level command.

**How to avoid:** Option (a) — for Phase 31 stretch, the JetBrains plugin assumes the user runs `flow lsp` (a subcommand) — verify by reading `flow-cli/Commands/` whether `flow lsp` exists. If yes, change the factory to `GeneralCommandLine("flow", "lsp")`. Option (b) — document an env-var fallback `FLOW_LSP_PATH` and read it from `FlowLanguageServerFactory.createConnectionProvider`. Option (c) — DEFER binary discoverability to v1.5 and ship a `flow-jetbrains/README.md` that says "set the LSP binary path manually in LSP4IJ settings until v1.5."

**Recommendation:** Plan task "Verify `flow` CLI has an `lsp` subcommand" as a prerequisite for the LSP4IJ factory wiring. If absent, plan task to add the subcommand (small Phase 30 follow-up) OR plan to use env-var fallback.

**Warning signs:** Manual UAT for REQ-7 acceptance shows "language server failed to start" in IntelliJ logs.

### Pitfall 8: `Note:` and `TODO:` False Positives in Strings

**What goes wrong:** A string literal `"see TODO: foo bar"` inside `(print)` could be tokenized as a comment if the lexer fires on `TODO:` regardless of string context.

**Why it happens:** SPEC REQ-4 says "Existing string-literal `"Note: ..."` inside `(print)` is UNAFFECTED (string-literal context wins)."

**How to avoid:** The `SkipWhitespaceAndComments` skip ONLY runs when the lexer is between tokens — not inside `ScanString` (which has its own loop). The existing `Note:` lexer arm is correctly placed in `SkipWhitespaceAndComments` (line 1144), so it never fires inside a string. New lead-ins (`TODO:`, `FIXME:`) follow the same pattern verbatim. Add a unit test in `Phase31LexerCommentFormsTests.cs` that asserts `(print "TODO: hello")` produces 4 tokens (LParen, Identifier `print`, StringLiteral `TODO: hello`, RParen) and zero comment-skipped bytes.

**Warning signs:** `tests/test_strings.flow` failing. Strings containing colon-prefixed words rendering as comments in VSCode.

## Code Examples

### Adding the UnusedImportAnalyzer

```csharp
// Source: extend pattern from flow-lsp/Diagnostics/ScaleLintAnalyzer.cs
using FlowLang.Ast;
using FlowLang.Ast.Statements;
using FlowLang.Lexing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using FlowProgram = FlowLang.Ast.Program;

namespace FlowLsp.Diagnostics;

public static class UnusedImportAnalyzer
{
    public static IReadOnlyList<Diagnostic> Analyze(
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source,
        StdlibSymbolIndex stdlib)
    {
        var imports = ast.Statements.OfType<ImportStatement>().ToList();
        if (imports.Count == 0) return Array.Empty<Diagnostic>();

        var referencedNames = CollectIdentifierReferences(ast);
        var diagnostics = new List<Diagnostic>();
        foreach (var import in imports)
        {
            var moduleName = ExtractModuleName(import.FilePath);  // "@harmony" → "harmony"
            var moduleProcs = stdlib.ProcsForModule(moduleName);  // NEW helper on StdlibSymbolIndex
            if (moduleProcs.Any(p => referencedNames.Contains(p.Name))) continue;

            diagnostics.Add(new Diagnostic
            {
                Severity = DiagnosticSeverity.Warning,
                Source = "flow.unusedImport",
                Message = $"Unused import: \"{import.FilePath}\"",
                Range = LspMappings.ToRange(import.Location)  // existing helper
            });
        }
        return diagnostics;
    }

    private static HashSet<string> CollectIdentifierReferences(FlowProgram ast)
    {
        // Walk AST collecting every FunctionCallExpression.FunctionName,
        // VariableExpression.Name, MemberAccessExpression.MemberName.
        // Return a Set<string> for O(1) "is this name referenced?" check.
        ...
    }
}
```

### Wrapping CompletionHandler.BuildItems with Filters

```csharp
// Source: flow-lsp/Handlers/CompletionHandler.cs:93 extended

public static IEnumerable<CompletionItem> BuildItems(
    DocumentUri uri, string text, FlowProgram? ast, IReadOnlyList<Token>? tokens,
    Position cursor, BuiltInIndex builtIns, UserSymbolIndex users,
    StdlibSymbolIndex stdlib, KeywordIndex keywords)
{
    // EXISTING: note-stream branch + use-string-literal gate stay unchanged
    if (ast is not null && NoteStream.NoteStreamContext.IsInsideNoteStream(ast, text, cursor)) { ... }
    if (IsInsideUseStringLiteral(text, cursor)) return stdlib.UseStringPathItems();

    // EXISTING: 5-source merge
    var merged = builtIns.Items()
        .Concat(stdlib.Items())
        .Concat(users.CompletionsFor(uri))
        .Concat(keywords.Items())
        .Concat(SnippetTemplates());

    // NEW: 3 filters applied in sequence (each a pure transform)
    if (ast is not null)
    {
        merged = FilterByImports(merged, ast, stdlib);          // REQ-2 a
        merged = FilterByPragmas(merged, ast.Pragmas);          // REQ-2 b
        if (tokens is not null)
            merged = BoostByMusicalContext(merged, ast, tokens, text, cursor);  // REQ-2 c
    }

    return merged;
}

// Each filter is a pure static helper exposed for unit tests:
public static IEnumerable<CompletionItem> FilterByImports(
    IEnumerable<CompletionItem> items, FlowProgram ast, StdlibSymbolIndex stdlib)
{
    var importedModules = new HashSet<string>(
        ast.Statements.OfType<ImportStatement>()
           .Select(i => ExtractModuleName(i.FilePath)));

    // Special case: @std transitively imports @harmony, @audio, @collections, @bars, @notation, @composition
    if (importedModules.Contains("std"))
        importedModules.UnionWith(StdlibSymbolIndex.ModuleNames);

    return items.Where(item =>
    {
        // Builtins from a module the user didn't import → filtered out
        var proc = stdlib.Find(item.Label);
        if (proc is null) return true;  // not a stdlib proc (e.g. builtin, keyword, snippet)
        return importedModules.Contains(proc.Module);
    });
}
```

### LspMappings.FormatSignature (varargs ellipsis)

```csharp
// Source: NEW helper in flow-lsp/LspMappings.cs

/// <summary>
/// Format a FunctionSignature for hover / signature-help / completion-tooltip.
/// Variadic params render with U+2026 (`…`) trailing the param type per D-01/D-02.
/// Non-variadic params render unchanged.
/// </summary>
public static string FormatSignature(FunctionSignature sig)
{
    // FunctionSignature.ToString() exists (FunctionSignature.cs:9-15) but emits
    // "..." for varargs. Phase 31 D-01 mandates U+2026 — so we render here, not in
    // FunctionSignature itself (flow-lang stays untouched per Phase 24 D-04 policy).
    var inputs = sig.InputTypes.Select((t, i) =>
        sig.IsVarArgs && i == sig.InputTypes.Count - 1
            ? $"{t}…"   // U+2026
            : $"{t}");
    return $"{sig.Name}({string.Join(", ", inputs)})";
}
```

### Lexer Comment-Form Additions (Option A, recommended)

```csharp
// Source: flow-lang/Lexing/SimpleLexer.cs:SkipWhitespaceAndComments (extend)

private void SkipWhitespaceAndComments()
{
    while (!IsAtEnd())
    {
        char c = Peek();
        if (char.IsWhiteSpace(c)) { Advance(); }
        else if (c == '\\' && PeekNext() == '\n') { /* line continuation */ ... }
        else if (c == '/' && PeekNext() == '/') { /* existing // comment */ ... }
        else if (c == 'N' && IsStartOfLineContent() && _source.AsSpan(_position).StartsWith("Note:")) { ... }
        // NEW Phase 31 REQ-4:
        else if (c == ';' && IsStartOfLineContent())  // Option A: position-sensitive
        {
            while (!IsAtEnd() && Peek() != '\n') Advance();
        }
        else if (c == 'T' && IsStartOfLineContent() && _source.AsSpan(_position).StartsWith("TODO:"))
        {
            while (!IsAtEnd() && Peek() != '\n') Advance();
        }
        else if (c == 'F' && IsStartOfLineContent() && _source.AsSpan(_position).StartsWith("FIXME:"))
        {
            while (!IsAtEnd() && Peek() != '\n') Advance();
        }
        else break;
    }
}
```

### TextMate Grammar — Function-Call + 4 Comment Forms

```json
{
  "repository": {
    "comments": {
      "patterns": [
        { "name": "comment.line.double-slash.flow", "match": "//.*$" },
        { "name": "comment.line.semicolon.flow",     "match": "^\\s*;.*$" },
        { "name": "comment.line.todo.flow",          "match": "^\\s*TODO:.*$" },
        { "name": "comment.line.fixme.flow",         "match": "^\\s*FIXME:.*$" },
        { "name": "comment.line.documentation.flow", "match": "^\\s*Note:.*$" }
      ]
    },
    "function-call": {
      "match": "\\b([A-Za-z_][A-Za-z0-9_]*)(?=\\s*\\()",
      "captures": { "1": { "name": "entity.name.function.flow" } }
    },
    "variable-ref": {
      "match": "\\b[A-Za-z_][A-Za-z0-9_]*\\b",
      "name": "variable.other.flow"
    }
  },
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
}
```

Note the `^\\s*` anchor on the three lead-in comments — matches column-0 OR leading-whitespace-only-then-token, mirroring the lexer's `IsStartOfLineContent()` gate.

### flow-jetbrains/build.gradle.kts (minimum)

```kotlin
// Source: adapted from LSP4IJ DeveloperGuide.md + redhat-developer/lsp4ij build.gradle.kts

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
        plugin("com.redhat.devtools.lsp4ij:0.19.3")  // D-09 pin
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

### Phase31 Unit Test Skeleton

```csharp
// flow-lang.Tests/Unit/Phase31/UnusedImportAnalyzerFacts.cs
using Xunit;
using FlowLsp.Diagnostics;
// ... namespace/imports follow Phase24 pattern

public class UnusedImportAnalyzerFacts
{
    [Fact]
    public void Unused_import_emits_warning_diagnostic()
    {
        var src = "use \"@harmony\";\nproc main () { (print \"hi\") }";
        var (ast, tokens) = LspFixtures.Parse(src);   // existing Phase17 helper
        var stdlib = LspFixtures.StdlibIndex();
        var diags = UnusedImportAnalyzer.Analyze(ast, tokens, src, stdlib);
        Assert.Single(diags);
        Assert.Equal(DiagnosticSeverity.Warning, diags[0].Severity);
        Assert.Equal("flow.unusedImport", diags[0].Source);
    }

    [Fact]
    public void Used_import_emits_zero_diagnostics()
    {
        var src = "use \"@harmony\";\nChord c = (arpeggio Cmaj 4);";
        var (ast, tokens) = LspFixtures.Parse(src);
        var stdlib = LspFixtures.StdlibIndex();
        var diags = UnusedImportAnalyzer.Analyze(ast, tokens, src, stdlib);
        Assert.Empty(diags);
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Hand-rolled IntelliJ language plugin (PSI tree, BNF) | LSP4IJ — reuse the LSP server | 2024 (LSP4IJ stabilization) | Phase 31 stretch is achievable in ~200 lines of Kotlin + plugin.xml |
| Custom comment scope inventions (`comment.flow.note`) | Standard scopes with `.flow` suffix (`comment.line.documentation.flow`) | TextMate convention since ~2010 | D-06 locks the convention; matches Python/Rust/Java grammars |
| LSP `Diagnostic` Error-only | Multi-severity (Error/Warning/Info/Hint) | LSP 3.x spec | OmniSharp framework already supports — Phase 31 just uses the existing severity field |
| Single-monolithic-publish per server | `CombinedDiagnosticsPublisher` orchestrator | Phase 24 D-04 (this codebase) | Phase 31 extends pattern — analyzer-per-diagnostic-type is the locked policy |
| Three-ASCII-dots for varargs (`String...`) | Unicode ellipsis U+2026 (`String…`) | Modern signature-help conventions | D-01 codifies for compactness in hover tooltips |
| Manual TextMate grammar testing | `vscode-tmgrammar-snap` snapshot tests | Microsoft tooling 2020+ | Already in `vscode-extension/package.json`; regenerate after grammar edits |

**Deprecated/outdated:**
- The `comment.line.semicolon.lisp` scope name is used by some Emacs configurations and AtomTextMate grammars — adopted here as `comment.line.semicolon.flow` per D-06/D-07.
- `vscode-tmgrammar-test` (the older project) coexists with `vscode-tmgrammar-snap` (the newer); use the latter since `npm run test:grammar:update` is already wired.
- `managed-midi` (legacy .NET MIDI lib) — irrelevant to Phase 31 (MIDI export already locked to DryWetMidi).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | flow-lsp build + tests | ✓ | 10.0.107 | — |
| Java 17+ | LSP4IJ Gradle build (REQ-7) | ✓ (21.0.10) | OpenJDK 21 | — |
| Gradle CLI | flow-jetbrains build | ✗ | — | Use `./gradlew` (Gradle wrapper script bundled in the scaffold; downloads its own Gradle 8.6 distribution per LSP4IJ convention) |
| npm | vscode-extension grammar snapshot regen | (not verified — Phase 17 already used it; assume present) | — | If absent, skip snapshot regeneration in CI; manual verification in dev-host smoke |
| IntelliJ IDEA Community 2024.2+ | Manual UAT for REQ-7 stretch | (user-side; not on CI) | — | Reasonable to assume the human running the stretch UAT has a current IntelliJ installation; document the build-number requirement |
| flow-lsp binary on PATH (or via `flow lsp` subcommand) | JetBrains plugin runtime | (see Pitfall 7) | — | Document env-var fallback `FLOW_LSP_PATH` in flow-jetbrains/README.md |

**Missing dependencies with no fallback:** None — every gap has a documented fallback.

**Missing dependencies with fallback:**
- **Gradle CLI** — use `./gradlew` (wrapper). Phase 31 plan must commit the gradle-wrapper files (`gradlew`, `gradlew.bat`, `gradle/wrapper/gradle-wrapper.jar`, `gradle/wrapper/gradle-wrapper.properties`) as part of the scaffolding.

## Validation Architecture

See [31-VALIDATION.md](./31-VALIDATION.md) for canonical validation architecture.


## Security Domain

> Phase 31 is LSP / language-tooling work with no network surface, no auth, no input from untrusted sources beyond local `.flow` files which the user already trusts.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — |
| V3 Session Management | no | — |
| V4 Access Control | no | — |
| V5 Input Validation | **partial** | The `flow-lsp` server receives `.flow` text from the editor — already untrusted-input territory but with existing parse-error soft-failure model (`ErrorReporter`). New analyzers (UnusedImport / UnreachableSection / ShadowedVariable) must null-check AST traversal — never throw on partial parses (Phase 24 D-22 precedent: silent fail-open). |
| V6 Cryptography | no | — |
| V7 Error Handling | yes | Analyzers must never propagate exceptions to the LSP protocol layer. The existing `ScaleLintAnalyzer` doesn't throw; new analyzers follow the same shape. |
| V12 Files | partial | LSP4IJ plugin spawns `flow-lsp` via `OSProcessStreamConnectionProvider` — uses standard JVM `GeneralCommandLine`. No user-controlled path construction. Document the binary discovery path in `flow-jetbrains/README.md` (Pitfall 7). |

### Known Threat Patterns for {flow-lsp + LSP4IJ stack}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malicious `.flow` file causing analyzer OOM | DoS | Existing 150ms didChange debounce + parse-failure-fast contract. New analyzers must be O(n) over AST. UnusedImport / Shadowed walks are linear; UnreachableSection same. |
| LSP4IJ launching wrong binary | Tampering | LSP4IJ uses `OSProcessStreamConnectionProvider` with a `GeneralCommandLine` — the binary name `flow-lsp` is hard-coded in `FlowLanguageServerFactory.kt`. No env-var injection unless the env-var fallback (Pitfall 7) is added; document that fallback carefully. |
| Exception in analyzer crashes LSP server | DoS | Each `Analyze()` method runs under the `CombinedDiagnosticsPublisher.Publish` wrapper; any exception there terminates the publish but does NOT crash the server. Defense-in-depth: each analyzer wraps its body in try/catch returning `Array.Empty<Diagnostic>()` on exception (charitable / silent fail-open per project memory). |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Option A (position-sensitive `;`) is the correct resolution of the Semicolon collision | Critical Decision + Pitfall 1 | If user wants Option B (`;;`) or Option C (full removal), plan needs restructuring. **Recommended planner action: lock this as D-11 in plan-phase.** |
| A2 | LSP4IJ 0.19.3 is stable enough for production | Standard Stack | LSP4IJ is on its first major-version arc; 0.19.x could have regressions. Verified via GitHub releases API that 0.19.3 has been stable since 2026-04-15 with only patch-level changes. |
| A3 | `flow-lsp` binary is discoverable from the JetBrains plugin via `flow lsp` subcommand or PATH | Pitfall 7 | Phase 30 added a unified `flow` CLI with subcommands — research did NOT verify whether `flow lsp` is a registered subcommand. Planner must verify this in plan-phase before locking the factory wiring. If not, plan a Phase 30 follow-up task OR use env-var fallback. |
| A4 | `vscode-tmgrammar-snap` is on the npm registry and resolves cleanly | Validation Architecture | Already in `vscode-extension/package.json` devDeps; not freshly verified against current npm. Low risk — package is widely used. |
| A5 | Two-run byte-identical determinism is preserved under Option A `;` lexer change | Pitfall 1 + Project Constraints | Assumption: position-sensitive `;` consumes whitespace-equivalent bytes only when no existing-script `;` appears at column-0. Verified zero column-0 `;` in 647 `.flow` files. Confidence HIGH but not formally proven. |

**If this table is empty:** N/A. The 5 assumptions above need user confirmation before execution. **A1 (Option A vs B vs C for semicolon comments) is the highest-risk assumption and should be locked as a D-11 decision in plan-phase.**

## Open Questions

1. **`flow` CLI lsp subcommand existence**
   - What we know: Phase 30 added a unified `flow` binary with 11 subcommands per `30-SPEC.md REQ-1`.
   - What's unclear: Whether `flow lsp` is one of the 11. Did NOT grep `flow-cli/Commands/` in research (low priority for research phase; planner can verify in seconds).
   - Recommendation: Planner runs `ls flow-cli/Commands/` early in plan-phase. If `LspCommand.cs` exists, factory uses `GeneralCommandLine("flow", "lsp")`. If not, planner adds a small task to register the subcommand (or uses env-var fallback).

2. **Snapshot file format compatibility**
   - What we know: `vscode-tmgrammar-snap 0.1.3` is in devDeps; 4 `.snap` files committed.
   - What's unclear: Whether the `.snap` file format has changed in any minor version bumps since they were generated.
   - Recommendation: Plan task starts with `cd vscode-extension && npm install && npm run test:grammar` to verify the existing snapshots still pass under the current tool version BEFORE modifying the grammar. If they fail, regenerate as a separate prerequisite step.

3. **CombinedDiagnosticsPublisher static vs DI wiring**
   - What we know: `CombinedDiagnosticsPublisher.BuildAll` static helper (line 48) directly invokes `ScaleLintAnalyzer.Analyze`; the wired-up `Publish` method takes `IScaleLintPublisher` via DI for testability.
   - What's unclear: For 3 new analyzers, do we plumb 3 new interfaces (`IUnusedImportPublisher` etc.) for DI symmetry, or just add static calls to `BuildAll` for simplicity?
   - Recommendation: Use static calls in `BuildAll`. Mirroring the existing `ScaleLintAnalyzer.Analyze(ast, tokens, source)` shape. DI symmetry can be added in v1.5 if a test needs to mock-out a single analyzer; for Phase 31 the static path keeps wiring trivial. (Phase 24 already proved this works — `BuildAll` is the static-call path.)

4. **`flow-jetbrains/` package namespace**
   - What we know: D-10 says the scaffolding lands; SPEC doesn't fix the Kotlin package namespace.
   - What's unclear: Should it be `dev.flowlang.jetbrains` (matches general convention) or `lang.flow.jetbrains` (other convention) or something else?
   - Recommendation: `dev.flowlang.jetbrains` — short, no clash with `org.flowlang` (Apache namespace convention if Flow ever gets promoted), follows JetBrains plugin convention. Easily renamed in v1.5.

## Sources

### Primary (HIGH confidence)
- **GitHub releases API for LSP4IJ** — `https://api.github.com/repos/redhat-developer/lsp4ij/releases?per_page=10` invoked 2026-05-11, returned `0.19.3` as the latest stable (non-prerelease) tag. Verified prerelease flag false.
- **LSP4IJ DeveloperGuide.md** — `https://github.com/redhat-developer/lsp4ij/blob/main/docs/DeveloperGuide.md` — plugin.xml + LanguageServerFactory + build.gradle.kts patterns.
- **LSP4IJ build.gradle.kts + gradle.properties** — `https://github.com/redhat-developer/lsp4ij/blob/main/build.gradle.kts` + `.../gradle.properties` — JDK 17/21 baseline, IntelliJ Platform 2024.2, pluginSinceBuild=242.
- **Codebase verified files:**
  - `flow-lsp/Handlers/CompletionHandler.cs` (273 lines, read in full)
  - `flow-lsp/Handlers/HoverHandler.cs` (123 lines, read in full)
  - `flow-lsp/Handlers/SignatureHelpHandler.cs` (101 lines, read in full)
  - `flow-lsp/Handlers/DiagnosticsPublisher.cs` (60 lines, read in full)
  - `flow-lsp/Diagnostics/CombinedDiagnosticsPublisher.cs` (80 lines, read in full)
  - `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` (336 lines, read in full)
  - `flow-lsp/Symbols/BuiltInIndex.cs` (62 lines, read in full)
  - `flow-lsp/Symbols/StdlibSymbolIndex.cs` (read first 80 lines)
  - `flow-lsp/LspMappings.cs` (35 lines, read in full)
  - `flow-lang/Lexing/SimpleLexer.cs:1107-1192` (`SkipWhitespaceAndComments` + `IsStartOfLineContent`)
  - `flow-lang/TypeSystem/FunctionSignature.cs` (full file)
  - `flow-lang/TypeSystem/OverloadResolver.cs` (84 lines)
  - `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` (136 lines)
  - `flow-lang/Ast/Program.cs` + `Ast/Statements/ImportStatement.cs` + `Ast/Statements/VariableDeclaration.cs`
  - `vscode-extension/syntaxes/flow.tmLanguage.json` (105 lines, read in full)
  - `vscode-extension/package.json` (60 lines, read in full)
  - `vscode-extension/src/extension.ts` (full file)
  - `vscode-extension/tests/grammar/` (8 files inventoried; sample.flow + sample.flow.snap read)
  - `vscode-extension/language-configuration.json` (full file)

### Secondary (MEDIUM confidence)
- **Rust grammar (function-call coloring reference)** — `https://raw.githubusercontent.com/dustypomerleau/rust-syntax/master/syntaxes/rust.tmLanguage.json` — pattern for `entity.name.function.rust` via lookahead `(?=\\()`.
- **Python grammar (TODO/FIXME codetag reference)** — `https://raw.githubusercontent.com/microsoft/vscode/main/extensions/python/syntaxes/MagicPython.tmLanguage.json` — `keyword.codetag.notation.python` precedent (Flow uses `comment.line.todo.flow` per D-07, a tighter scope).
- **TextMate manual** — `https://macromates.com/manual/en/language_grammars` §12.4 — standard scope tree.
- **`grep` audit of 647 `.flow` files** — zero column-0 `;` outside strings/comments; zero `TODO:`/`FIXME:` at column 0. Verified via shell commands in research session.

### Tertiary (LOW confidence)
- **LSP4IJ Maven Central artifact resolution** — `mvnrepository.com` returned 403 to WebFetch; verified via secondary search results that the artifact path is `com.redhat.devtools.intellij:lsp4ij`. Cross-verified via the LSP4IJ `gradle.properties` `pluginGroup` value.
- **VSCode `problems.severities` configuration** for editor-side scaleLint opt-out (D-04) — exact setting key not verified against current VSCode docs in this session; recommend a quick `WebSearch` during plan-phase if planner wants to document this exactly in release notes.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every existing dependency verified by reading csproj/package.json; LSP4IJ version verified by GitHub API.
- Architecture: HIGH — every Phase 17/24 pattern read in source; integration points and entry points named with line numbers.
- Pitfalls: HIGH — the Semicolon collision (Pitfall 1) was discovered by reading Parser source + grep audit, not assumed. Pitfall 7 (binary discoverability) flagged because Phase 30 wiring not verified in this session.

**Research date:** 2026-05-11
**Valid until:** 2026-06-10 (30 days — codebase moves fast but LSP/grammar is stable territory; LSP4IJ release cadence is ~2-3 month minor versions, so 0.19.3 pin is durable for the phase duration).
