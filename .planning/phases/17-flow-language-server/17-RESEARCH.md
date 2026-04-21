# Phase 17: Flow Language Server - Research

**Researched:** 2026-04-20
**Domain:** LSP server in C# + VSCode extension for a domain-specific music language
**Confidence:** HIGH on stack & encoding; MEDIUM on bundling/CI specifics; LOW on a single concern (OmniSharp release cadence — see Open Questions)

## Summary

This phase ships an LSP server (`flow-lsp/`, a new C# project that references `flow-lang`) and a VSCode extension that bundles per-platform self-contained server binaries. The 15 locked decisions in CONTEXT.md (D-01 through D-15) form a coherent v1 design — none of them require user re-confirmation based on this research. The OmniSharp.Extensions.LanguageServer framework chosen in D-01 is the right pick: it works on .NET 10 (the project's actual target — see Pitfall 1), is the de-facto C#-LSP framework, and reusing `flow-lang` means the LSP and the interpreter cannot disagree about what is or isn't valid Flow syntax. [VERIFIED: nuget.org, OmniSharp GitHub]

The biggest research surprise: **the project actually targets `net10.0` across all five csproj files**, not net9.0 as CLAUDE.md states. The LSP project must follow suit. [VERIFIED: `flow-lang/flow-lang.csproj:4`, all sibling csproj files via grep]

The TextMate-grammar + LSP-semantic-tokens hybrid (D-04) is the standard 2-layer approach used by most modern language extensions (TypeScript, Rust-analyzer, gopls). The TM grammar provides instant coloring before the server boots; semantic tokens refine ambiguous cases (e.g., `Bb7` being a chord vs. `Bb7` being a note literal — the lexer already knows, the regex grammar can only guess). [CITED: code.visualstudio.com/api/language-extensions/semantic-highlight-guide]

Distribution to both Marketplace and OpenVSX is mechanical with `HaaLeo/publish-vscode-extension@v2`; per-platform VSIX requires looping `vsce package --target <platform>` over four targets. [CITED: github.com/HaaLeo/publish-vscode-extension]

**Primary recommendation:** Greenlight the plan. Wave 0 is `flow-lsp/` scaffolding + the TextMate grammar + the VSCode extension skeleton; Wave 1 wires diagnostics + semantic tokens; Wave 2 layers completion + hover + go-to-def + signature help; Wave 3 is the CI matrix + Marketplace/OpenVSX publish. Add `BuiltInDocs.cs` to `flow-lang/` first (cross-cutting; everyone reads it).

## User Constraints (from CONTEXT.md)

### Locked Decisions

**D-01 — LSP framework & code reuse.** Implement the LSP in C# as a new project that references `flow-lang` directly, using `OmniSharp.Extensions.LanguageServer` as the LSP framework. Reuse `SimpleLexer`, `Parser`, `ErrorReporter`, `InternalFunctionRegistry`, and `TypeSystem` unchanged.

**D-02 — Project layout.** New sibling csproj `flow-lsp/` in the solution, separate from `flow-interpreter`. Both reference `flow-lang`. LSP free of audio/PulseAudio dependencies.

**D-03 — Parse loop.** On `textDocument/didChange`, debounce ~150ms, then full re-lex + re-parse of the buffer. Soft-failure model returns full AST + diagnostic list in a single pass. No incremental parser, no evaluator-level caching for v1.

**D-04 — Highlighting hybrid.** Ship a TextMate grammar (`flow.tmLanguage.json`) for baseline coloring + LSP semantic tokens from `SimpleLexer` layered on top.

**D-05 — Standard scopes only.** Map tokens to standard VSCode TextMate scopes (`keyword.control`, `string.quoted`, `constant.numeric`, `entity.name.function`, `variable.other.note`, etc.). Do NOT ship a bundled Flow color theme; do NOT invent `*.flow`-specific scopes.

**D-06 — Diagnostics passthrough.** Forward everything `ErrorReporter` produces to LSP `publishDiagnostics`. No new analysis passes in v1.

**D-07 — Completion universe.** Built-ins from `InternalFunctionRegistry` + stdlib module paths (`@std`, `@audio`, `@collections`, `@bars`, `@notation`, `@composition`) for `use "..."` + language keywords + user-declared symbols (locals, procs, imports) + snippet templates for block constructs.

**D-08 — Hover.** Built-ins: full signature from `InternalFunctionRegistry` plus a one-line description. User symbols: declared type.

**D-09 — Go-to-definition.** Works for user procs and variables, and for imported names (jump to `.flow` stdlib file). Built-ins report `no definition available`.

**D-10 — Signature help.** Active-parameter hint while typing inside `foo(...)` for both built-ins and user procs.

**D-11 — Note-stream context-aware completion.** Inside `| ... |`, walk up the enclosing AST to find the active `key { }` block. If found, suggest roman numerals (`I`, `ii`, `iii`, `IV`, `V`, `V7`, `vi`, `vii°`). Otherwise suggest note letters + chord literals + durations (`q`, `h`, `w`, `e`, `s`) + rests (`_`) + tie/dot suffixes. Do not offer proc-name completions inside note streams.

**D-12 — `BuiltInDocs.cs`.** Single lookup table mapping built-in name → one-line summary + optional parameter blurbs. Hover falls back to signature-only if no doc exists.

**D-13 — VSCode-first, LSP-generic.** VSCode extension is the flagship artifact; server speaks plain LSP over stdio. Ship `docs/editor-setup/` with ≥1 snippet for `nvim-lspconfig` or `helix languages.toml`.

**D-14 — Per-platform self-contained binaries.** CI produces `dotnet publish -r linux-x64 --self-contained`, `win-x64`, `osx-x64`, `osx-arm64` and packs them into per-platform VSIXs. Accept ~30–70MB VSIX per-platform.

**D-15 — Dual-marketplace publish.** Publish to VSCode Marketplace AND OpenVSX in v1. Automate via CI on git tag push.

### Claude's Discretion

- Debounce window concrete value (150ms is starting point — tune during execution if it feels laggy or too eager).
- Exact wording and granularity of completion detail strings.
- Progress reporting / logging verbosity of the server process (`--trace` etc.).
- Semantic token modifier strategy (how richly to decorate tokens with modifiers like `readonly`, `declaration`).
- Extension manifest metadata details (icon, categories, keywords) beyond what's required for Marketplace publishing.
- CI matrix specifics (GitHub Actions vs alternative, matrix job shape).
- Snippet template bodies for block constructs — follow VSCode snippet conventions.
- Decision on whether to also wire a `flow-lsp` entry point into `flow-interpreter --lsp` as a convenience alias.

### Deferred Ideas (OUT OF SCOPE)

- **Find-references** (`textDocument/references`) — requires cross-file symbol index.
- **Rename refactoring** (`textDocument/rename`) — same index dependency.
- **Code actions / quick-fixes** — needs diagnostic-to-fix mapping logic.
- **Unused-import / shadowed-name warnings** — requires new analysis passes.
- **Documentation auto-pull from `wiki/`** — wiki prose isn't structured.
- **First-class Neovim/Helix/Emacs extensions published to package managers** — generic LSP + setup snippets cover this for v1.
- **Marketplace pre-release channel / telemetry** — v1 ships stable-only.
- **`flow-interpreter --lsp` convenience alias** — Claude's discretion.
- **Incremental parser** — only if profiling shows debounced re-parse is too slow.

## Phase Requirements

The phase has no REQ-IDs in REQUIREMENTS.md; the 15 CONTEXT.md decisions act as the requirement set. The mapping below shows which research findings support each.

| ID | Description | Research Support |
|----|-------------|------------------|
| D-01 | OmniSharp.Extensions.LanguageServer 0.19.9, reuse `flow-lang` | §Standard Stack — verified .NET 10 compat via package metadata; §Pitfalls Pitfall 4 |
| D-02 | New `flow-lsp/` csproj | §Architecture Patterns — Project Structure |
| D-03 | 150ms debounce + full re-lex/re-parse on didChange | §Architecture Patterns Pattern 3; §Code Examples Debounce |
| D-04 | TM grammar + semantic tokens hybrid | §Architecture Patterns Pattern 1; §Code Examples Semantic Tokens |
| D-05 | Standard VSCode scopes (no inventions) | §Standard Stack — TextMate scopes; §Common Pitfalls Pitfall 5 |
| D-06 | Forward `ErrorReporter` to `publishDiagnostics` | §Code Examples Diagnostics; §Architecture Patterns Pattern 2 |
| D-07 | Multi-source completion | §Architecture Patterns Pattern 4; §Code Examples Completion |
| D-08 | Hover from `InternalFunctionRegistry` + `BuiltInDocs.cs` | §Code Examples Hover |
| D-09 | Go-to-def for user symbols + stdlib imports | §Architecture Patterns Pattern 5; §Common Pitfalls Pitfall 6 |
| D-10 | Signature help for built-ins + user procs | §Code Examples Signature Help |
| D-11 | Note-stream context-aware completion | §Architecture Patterns Pattern 6; §Code Examples Note-stream walk |
| D-12 | `BuiltInDocs.cs` lookup table | §Architecture Patterns BuiltInDocs |
| D-13 | VSCode-first + generic LSP over stdio | §Architecture Patterns Project Structure; §Common Pitfalls Pitfall 7 |
| D-14 | Per-platform self-contained VSIX | §Standard Stack — vsce + dotnet publish; §Code Examples Bundling |
| D-15 | Dual Marketplace + OpenVSX publish | §Standard Stack — HaaLeo action; §Code Examples CI workflow |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Lex + parse Flow source | flow-lang (library) | — | Already shipped; LSP must never duplicate. |
| Diagnostics accumulation | flow-lang (`ErrorReporter`) | flow-lsp (transport) | Soft-failure model already correct for LSP needs. |
| LSP transport (stdio JSON-RPC) | flow-lsp (server process) | — | Owned by OmniSharp framework; do not hand-roll. |
| Symbol introspection (built-ins, user procs, stdlib procs) | flow-lsp (handlers) | flow-lang (registry, AST walk) | flow-lang exposes; flow-lsp queries. |
| TextMate baseline coloring | vscode-extension (TM grammar) | — | Static, regex-based; no server needed. |
| Semantic-token refinement | flow-lsp | vscode-extension (renders) | Server emits encoded ints; client just paints them. |
| Per-platform binary selection | vscode-extension (TS activation) | CI (builds the binaries) | Activation picks `process.platform + process.arch`. |
| Self-contained .NET publish | CI (GitHub Actions matrix) | flow-lsp (csproj props) | `dotnet publish -r <RID> --self-contained -p:PublishSingleFile=true`. |
| VSIX packaging per platform | CI (`vsce package --target <RID>`) | vscode-extension (`.vscodeignore`) | Each VSIX contains exactly one platform's binary. |
| Marketplace + OpenVSX publish | CI (`HaaLeo/publish-vscode-extension@v2`) | — | Triggered on git tag; uses two PAT secrets. |
| Audio/PulseAudio | (not present) | — | Explicitly excluded from `flow-lsp` per D-02. |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 10 SDK | 10.0.x | Runtime/build | Project already targets `net10.0` across all csproj — `flow-lsp/` must follow. [VERIFIED: `flow-lang/flow-lang.csproj:4`] |
| OmniSharp.Extensions.LanguageServer | 0.19.9 | LSP framework in C# | De-facto C# LSP framework; targets net6.0 + netstandard2.0, runs on .NET 10. Used by Dafny, Yarn Spinner, PowerShell Editor Services. [VERIFIED: nuget.org] |
| OmniSharp.Extensions.LanguageServer.Shared | 0.19.9 | Transitive | Pulled by the above. [VERIFIED: nuget.org dependency listing] |
| OmniSharp.Extensions.LanguageProtocol | 0.19.9 | Transitive | LSP types. [VERIFIED: nuget.org dependency listing] |
| OmniSharp.Extensions.JsonRpc | 0.19.9 | Transitive | JSON-RPC 2.0 wire protocol. [VERIFIED: nuget.org dependency listing] |
| Microsoft.Extensions.Configuration | ≥6.0.1 | Transitive | DI/config — pulled by OmniSharp. [VERIFIED: nuget.org dependency listing] |
| flow-lang (project ref) | local | Lex/parse/diagnostics/registry reuse | D-01 + D-02. |

### VSCode Extension Side (npm)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| vscode-languageclient | 9.0.1 | LSP client in TS for the extension host | Microsoft-maintained, only viable option. [VERIFIED: `npm view vscode-languageclient version`] |
| @vscode/vsce | 3.9.1 | Package + publish CLI for Marketplace | Official tool, replaces deprecated `vsce`. [VERIFIED: `npm view @vscode/vsce version`] |
| ovsx | 0.10.11 | Package + publish CLI for OpenVSX | Used internally by HaaLeo action; can also run standalone. [VERIFIED: `npm view ovsx version`] |
| typescript | 5.x | Compile activation script | Standard. |
| @types/vscode | match `engines.vscode` in package.json | API typings | Standard. |
| @types/node | 20.x | Activation runs in node host | Matches VSCode's bundled node. |

### CI / Publishing
| Tool | Version | Purpose | Why Standard |
|------|---------|---------|--------------|
| GitHub Actions | latest | CI matrix (4 platforms) + tag-triggered publish | Standard for OSS .NET projects. |
| HaaLeo/publish-vscode-extension | v2 | Single action that handles both Marketplace and OpenVSX | Most-cited action for this exact use case. [CITED: github.com/HaaLeo/publish-vscode-extension] |
| actions/setup-dotnet | v4 | Installs .NET 10 SDK on runners | Standard. |
| actions/setup-node | v4 | Installs Node 20 for `vsce` | Standard. |

### Test/Validation Tooling
| Library | Version | Purpose | Why |
|---------|---------|---------|-----|
| xunit.v3 | 3.2.2 | Already in `flow-lang.Tests/` | Reuse existing harness for in-process LSP handler tests. [VERIFIED: `flow-lang.Tests/flow-lang.Tests.csproj:13`] |
| vscode-tmgrammar-test | 0.1.x | Snapshot tests for TM grammar | The standard tool — supports `vscode-tmgrammar-test` (unit) and `vscode-tmgrammar-snap` (snapshot). [CITED: github.com/PanAeon/vscode-tmgrammar-test] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| OmniSharp.Extensions.LanguageServer | Microsoft.VisualStudio.LanguageServer.Protocol + StreamJsonRpc (hand-rolled handlers) | Lower-level; would re-implement what OmniSharp gives for free. Only use if OmniSharp is provably broken on net10 (it isn't — see Pitfall 4). [VERIFIED: github.com/microsoft/vs-streamjsonrpc] |
| OmniSharp.Extensions.LanguageServer | `kaby76/lsp-types` (StreamJsonRpc + Newtonsoft.Json based community port) | More current to LSP spec but tiny community, no major adopters. Higher risk than OmniSharp. [CITED: github.com/kaby76/lsp-types] |
| TextMate grammar + semantic tokens hybrid | TextMate-only | Cannot disambiguate `Bb7` (chord vs. note) or `H` (variable vs. note alias) using regex; would either over-color or under-color. Existing `FlowSyntaxHighlighter.cs` already proves the lexer-driven approach works in `flow-editor`. |
| TextMate + semantic tokens | semantic-tokens-only | TM grammar is needed for first-paint before the server boots, plus for users who never run the server (other editors that don't speak LSP). [CITED: code.visualstudio.com/api/language-extensions/semantic-highlight-guide — "TextMate grammars work as the syntax highlighting engine"] |
| Per-platform self-contained VSIX (D-14) | Single platform-agnostic VSIX requiring user-installed .NET 10 | Friction: most VSCode users don't have .NET 10. Per-platform is 4× the VSIX count but zero install friction. CONTEXT D-14 already locked this. |
| Per-platform VSIX | Trimmed self-contained binaries (`PublishTrimmed=true`) | Trimming is unsafe with reflection-heavy frameworks like OmniSharp (uses MediatR + DI heavily). [VERIFIED: learn.microsoft.com/dotnet/core/deploying/trimming/trim-self-contained — "trimming can lead to runtime errors if code that wasn't observed during build time is needed at runtime"] |
| HaaLeo/publish-vscode-extension | Manual `vsce publish` + `ovsx publish` in shell steps | Action handles edge cases (target platform inference, retries); shell fallback always available. |

**Installation (csproj snippet):**
```xml
<!-- flow-lsp/flow-lsp.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <RootNamespace>FlowLsp</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <!-- DO NOT enable PublishTrimmed — OmniSharp uses reflection heavily -->
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="OmniSharp.Extensions.LanguageServer" Version="0.19.9" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\flow-lang\flow-lang.csproj" />
  </ItemGroup>
</Project>
```

**Version verification (2026-04-20):**
- `OmniSharp.Extensions.LanguageServer` 0.19.9 — published 2023-09-21. [VERIFIED: nuget.org]
- `vscode-languageclient` latest is 9.0.1 (10.0.0-next series exists but pre-release). [VERIFIED: `npm view vscode-languageclient dist-tags`]
- `@vscode/vsce` 3.9.1, `ovsx` 0.10.11. [VERIFIED: npm view]

## Architecture Patterns

### System Architecture Diagram

```
┌─────────────────────────┐         ┌─────────────────────────┐
│  VSCode (extension      │         │  flow-lsp.exe           │
│   host: TypeScript)     │         │  (.NET 10 self-contained)│
│                         │         │                         │
│  • activate() picks     │ stdin/  │  ┌──────────────────┐   │
│    binary by platform   │ stdout  │  │ OmniSharp        │   │
│  • LanguageClient       │◄───────►│  │ LanguageServer   │   │
│    spawns the binary    │ JSON-RPC│  │ (stdio handlers) │   │
│  • TM grammar paints    │         │  └────────┬─────────┘   │
│    first frame instantly│         │           │             │
└──────────┬──────────────┘         │  ┌────────▼─────────┐   │
           │                        │  │ DocumentManager  │   │
           │ contributes:           │  │ (open buffers,   │   │
           │  • languages.flow      │  │  debounce,       │   │
           │  • grammars.tmlanguage │  │  cancel-token)   │   │
           │  • configuration       │  └────────┬─────────┘   │
           │                        │           │             │
           ▼                        │  ┌────────▼─────────┐   │
   .flow file in editor             │  │ FlowEngine bind  │   │
                                    │  │ (re-lex+re-parse │   │
                                    │  │  on each change) │   │
                                    │  └────┬──────┬──────┘   │
                                    │       │      │          │
                                    │   ┌───▼──┐ ┌─▼────────┐ │
                                    │   │AST + │ │Errors    │ │
                                    │   │Tokens│ │(soft-fail│ │
                                    │   └───┬──┘ └────┬─────┘ │
                                    │       │         │       │
                                    │       ▼         ▼       │
                                    │  ┌─────────────────┐    │
                                    │  │ Handlers:       │    │
                                    │  │  • Diagnostics  │    │
                                    │  │  • Completion   │    │
                                    │  │  • Hover        │    │
                                    │  │  • Definition   │    │
                                    │  │  • SignatureHelp│    │
                                    │  │  • SemanticTok. │    │
                                    │  └────────┬────────┘    │
                                    │           │             │
                                    │           ▼             │
                                    │   ┌───────────────┐     │
                                    │   │ Symbol sources│     │
                                    │   │ • InternalReg.│     │
                                    │   │ • AST walk    │     │
                                    │   │ • Stdlib parse│     │
                                    │   │ • BuiltInDocs │     │
                                    │   └───────────────┘     │
                                    └─────────────────────────┘
```

Data flow (for a keystroke):
1. User types → VSCode editor model updates → TM grammar repaints (instant, regex-based, no server roundtrip).
2. VSCode `LanguageClient` sends `textDocument/didChange` over stdio JSON-RPC.
3. `flow-lsp` enqueues a parse job keyed by document URI; cancels any in-flight job for the same URI.
4. After 150ms debounce, server runs `SimpleLexer` → `Parser` → AST + `ErrorReporter.Errors`.
5. Server emits `textDocument/publishDiagnostics` with the error list.
6. Server emits `textDocument/semanticTokens/full` (or refresh) with the token re-encoding.
7. On request (`textDocument/completion`, `hover`, `definition`, `signatureHelp`), handlers consult the cached AST + the symbol sources.

### Recommended Project Structure

```
flow-sharp/
├── flow-sharp.sln                     # Add flow-lsp.csproj
├── flow-lang/                         # UNCHANGED (except adding BuiltInDocs.cs)
│   └── StandardLibrary/
│       └── BuiltInDocs.cs             # NEW (D-12) — lookup table
├── flow-interpreter/                  # UNCHANGED
├── flow-editor/                       # UNCHANGED
├── flow-lang.Tests/                   # ADD: Phase17/ subdir for handler tests
│   └── Unit/Phase17/
│       ├── DocumentManagerTests.cs    # debounce + cancel
│       ├── DiagnosticsHandlerTests.cs # ErrorReporter → LSP Diagnostic[]
│       ├── SemanticTokensTests.cs     # token encoding determinism
│       ├── CompletionHandlerTests.cs  # built-ins, stdlib, keywords, locals
│       ├── HoverHandlerTests.cs       # signature + doc lookup
│       ├── DefinitionHandlerTests.cs  # user procs + stdlib jumps
│       ├── SignatureHelpHandlerTests.cs
│       └── NoteStreamContextTests.cs  # in-key roman-numeral suggestions
├── flow-lsp/                          # NEW (D-02)
│   ├── flow-lsp.csproj                # net10.0, OutputType=Exe
│   ├── Program.cs                     # LanguageServer.From(...) wiring
│   ├── DocumentManager.cs             # buffer cache + debounce + cancel
│   ├── LspMappings.cs                 # SourceLocation ↔ LSP Range, etc.
│   ├── Symbols/
│   │   ├── BuiltInIndex.cs            # InternalFunctionRegistry walker
│   │   ├── UserSymbolIndex.cs         # AST walker for current buffer
│   │   ├── StdlibSymbolIndex.cs       # parse @std/@audio/etc. once
│   │   └── KeywordIndex.cs            # static list from TokenType
│   ├── Handlers/
│   │   ├── TextDocumentSyncHandler.cs
│   │   ├── DiagnosticsPublisher.cs
│   │   ├── SemanticTokensHandler.cs
│   │   ├── CompletionHandler.cs
│   │   ├── HoverHandler.cs
│   │   ├── DefinitionHandler.cs
│   │   └── SignatureHelpHandler.cs
│   └── NoteStream/
│       └── NoteStreamContext.cs       # AST-walk to find enclosing key
├── vscode-extension/                  # NEW
│   ├── package.json                   # contributes block + activation
│   ├── tsconfig.json
│   ├── .vscodeignore
│   ├── language-configuration.json    # brackets, comment markers
│   ├── syntaxes/
│   │   └── flow.tmLanguage.json       # TM grammar
│   ├── snippets/
│   │   └── flow.code-snippets         # block-construct templates
│   ├── src/
│   │   └── extension.ts               # activate() spawns flow-lsp binary
│   ├── server/                        # populated at build time per-platform
│   │   ├── linux-x64/flow-lsp        (only one of these per VSIX)
│   │   ├── win32-x64/flow-lsp.exe
│   │   ├── darwin-x64/flow-lsp
│   │   └── darwin-arm64/flow-lsp
│   ├── tests/
│   │   └── grammar/                   # vscode-tmgrammar-test snapshots
│   │       ├── note-stream.flow
│   │       ├── chords.flow
│   │       └── musical-context.flow
│   └── README.md
├── docs/
│   └── editor-setup/                  # D-13 — non-VSCode editor snippets
│       ├── nvim-lspconfig.lua
│       └── helix-languages.toml
└── .github/
    └── workflows/
        └── publish-extension.yml      # tag-triggered build + dual publish
```

### Pattern 1: TM grammar baseline + LSP semantic tokens overlay (D-04)

**What:** TM grammar paints colors using regex while the server is offline or starting; LSP semantic tokens replace those colors with lexer-precise info once the server emits `semanticTokens/full`. VSCode merges by precedence (semantic tokens win where they overlap).

**When to use:** Always, for languages whose syntax has any context-dependent ambiguity (which Flow has — `Bb7` is a chord literal, but `Bbq` is a note + duration suffix; only the lexer knows).

**Example:** [VERIFIED: existing `flow-editor/Editor/FlowSyntaxHighlighter.cs:95-147` — token-to-color mapping already proven; the LSP semantic-tokens handler ports the same switch into LSP encoding]

```typescript
// vscode-extension/package.json (excerpt)
"contributes": {
  "languages": [{
    "id": "flow",
    "aliases": ["Flow", "flow"],
    "extensions": [".flow"],
    "configuration": "./language-configuration.json"
  }],
  "grammars": [{
    "language": "flow",
    "scopeName": "source.flow",
    "path": "./syntaxes/flow.tmLanguage.json"
  }]
}
```

### Pattern 2: Soft-failure parse → LSP diagnostics passthrough (D-06)

**What:** `ErrorReporter` already accumulates errors during a single parse pass and returns the AST + the error list together. The LSP handler reads `errorReporter.Errors`, maps each `FlowError` to an LSP `Diagnostic`, and calls `PublishDiagnostics`.

**When to use:** Every `didChange` and every `didOpen`. This is the natural dispatch point.

**Example:** See §Code Examples → "Diagnostics handler".

### Pattern 3: Debounce + cancel pattern for didChange (D-03)

**What:** Per-document debounce buffer; each new `didChange` cancels the in-flight `CancellationTokenSource` for that document and starts a new 150ms timer; on timer fire, run the parse on a background thread.

**When to use:** Every text-sync handler in every LSP. The OmniSharp framework provides `Task`-based handlers but does NOT provide debounce out of the box — you implement it on top.

**Example:** See §Code Examples → "Document manager with debounce".

### Pattern 4: Multi-source completion (D-07)

**What:** A single `CompletionHandler` consults four indices:

1. `BuiltInIndex` — static, built once at server start by walking `InternalFunctionRegistry`.
2. `UserSymbolIndex` — per-document, rebuilt on parse — walks the current AST collecting `ProcDeclaration`, `VariableDeclaration`, `SectionDeclaration`, `ImportStatement` names.
3. `StdlibSymbolIndex` — built once at server start by parsing all `flow-lang/*.flow` files (they're shipped beside the binary; see Pitfall 6) and extracting top-level `internal proc` + `proc` declarations.
4. `KeywordIndex` — static list derived from `TokenType` keywords (`proc`, `use`, `tempo`, `key`, etc.).

The handler chooses sources based on cursor context (e.g., inside a `use "..."` string → only stdlib paths).

### Pattern 5: Go-to-definition with stdlib-path resolution (D-09)

**What:** For user procs/variables, walk the AST for matching declarations. For imported names referenced via `use "@audio"`, resolve `@audio` → absolute file path using the same logic as `flow-lang/Runtime/ModuleLoader.cs:113-127` (`@`-prefix → `assemblyDir/<name>.flow`). Return an LSP `Location` with that URI.

**When to use:** All textDocument/definition requests.

**Critical detail:** The stdlib `.flow` files MUST be shipped beside the LSP binary so `Path.GetDirectoryName(typeof(ModuleLoader).Assembly.Location)` finds them. See Pitfall 6.

### Pattern 6: Note-stream context-aware completion (D-11)

**What:** When a completion request arrives, determine if the cursor is inside a `NoteStreamExpression`. Two viable detection strategies:

- **AST-based (preferred when AST is well-formed):** Walk the AST top-down; for each `NoteStreamExpression`, check if its `Location` plus its computed end-location bracket the cursor.
- **Token-based fallback (when AST is broken mid-edit):** Scan tokens left-of-cursor backwards; if you hit an unmatched `Pipe` token before any line break terminator, you're inside a note stream.

If inside a note stream, walk the AST upward (or check enclosing block statements) for the nearest `MusicalContextStatement` of `ContextType == Key`. If found, suggest roman numerals; otherwise suggest note letters + chord literals + durations.

### Anti-Patterns to Avoid

- **Re-implementing the lexer in TS/regex.** The TextMate grammar will categorize broadly, but D-04 + the existing `FlowSyntaxHighlighter` precedent says: never duplicate `SimpleLexer`'s decisions in regex. The semantic-tokens handler is the source of truth.
- **Spawning a `FlowEngine` per request.** `FlowEngine` initializes audio playback (`AudioPlaybackManager`) and allocates a real `Interpreter`. The LSP only needs the lex + parse halves; build a stripped-down "ParseSession" that constructs a `SimpleLexer` + `Parser` + `ErrorReporter` directly without `FlowEngine`. Reuses lex/parse without the audio dependency. (D-02 explicitly forbids audio in `flow-lsp`.)
- **Running the evaluator/interpreter inside the server.** `MusicalContext` walking for D-11 must be done by AST traversal — not by spinning up an `Interpreter` and reading `ExecutionContext.GetMusicalContext()`. The interpreter mutates state and renders audio; both are wrong inside an LSP.
- **Inventing custom TM scopes** (e.g., `keyword.music.tempo.flow`). Per D-05, only standard scopes. Themes don't know your custom scopes; user gets unstyled tokens.
- **Caching across `didChange`.** No incremental parser, no "diff the previous AST". D-03 chose the simple full re-parse; the parser is fast enough (Flow files are small).
- **Trimming the published binary.** OmniSharp + MediatR + DI use heavy reflection. `<PublishTrimmed>true</PublishTrimmed>` will produce a binary that crashes at first request. [VERIFIED: learn.microsoft.com/dotnet/core/deploying/trimming/trim-self-contained]

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| LSP JSON-RPC wire | Manual stdin/stdout JSON parsing | OmniSharp.Extensions.LanguageServer | Wire format has framing (Content-Length headers), batching, request cancellation, progress reporting — all easy to get wrong. [VERIFIED: github.com/OmniSharp/csharp-language-server-protocol] |
| Semantic tokens delta encoding | Custom int[] packing | OmniSharp's `SemanticTokensBuilder` (or write your own helper, ~20 lines, against the spec — see §Code Examples) | The 5-int delta format is well-defined; OmniSharp ships a builder. [CITED: code.visualstudio.com/api/language-extensions/semantic-highlight-guide] |
| Position math (line/col ↔ offset) | Manual character counting per request | Cache line-start offsets per document on `didOpen`/`didChange` | Doing it per-request is O(N) and adds up; one-pass cache is O(1) lookup. |
| Per-platform extension distribution | Custom downloader inside extension activation | `vsce package --target <platform>` + per-VSIX upload | VSCode marketplace handles platform selection at install time; no runtime download needed. [VERIFIED: code.visualstudio.com/api/working-with-extensions/publishing-extension] |
| OpenVSX publish + Marketplace publish | Two separate manual workflows | `HaaLeo/publish-vscode-extension@v2` (one action does both) | Action handles target inference, retries, license validation. [CITED: github.com/HaaLeo/publish-vscode-extension] |
| TextMate grammar testing | Eyeball-only review of `.tmLanguage.json` | `vscode-tmgrammar-test` snapshot tests | Regex-based grammars are notoriously fragile; snapshots catch silent regressions. [CITED: github.com/PanAeon/vscode-tmgrammar-test] |
| `.flow` file watching/debounce | Custom timer + threading | `System.Threading.Channels` or simple `CancellationTokenSource`-per-doc Dictionary | Both are stdlib BCL; OmniSharp doesn't impose either. |
| Reading stdlib `.flow` files inside the server | Hand-roll filesystem walk | Reuse `ModuleLoader.ResolvePath` logic for symmetry with the interpreter | Already debugged, already handles `@`-prefix. |

**Key insight:** Every "Don't" item above has a documented standard solution. The minimal-deps philosophy is honored because OmniSharp is the *only* added dependency (its 4 transitive packages are all OmniSharp-owned + Microsoft.Extensions.Configuration which most .NET apps already pull).

## Common Pitfalls

### Pitfall 1: CLAUDE.md says net9.0 but the project actually targets net10.0
**What goes wrong:** Following CLAUDE.md's "Runtime: .NET 9" constraint literally and creating `flow-lsp.csproj` with `<TargetFramework>net9.0</TargetFramework>` produces a project that won't build (other csproj files are net10.0; project references mix frameworks).
**Why it happens:** CLAUDE.md and `.planning/PROJECT.md` were last updated when the project genuinely targeted net9.0. Since then, all five csproj files have been bumped to `net10.0` (verified via grep across `flow-*/flow-*.csproj`). Plan 12-06 explicitly notes this as "tracked for a future doc-hygiene pass" (STATE.md line 119).
**How to avoid:** `flow-lsp/flow-lsp.csproj` MUST set `<TargetFramework>net10.0</TargetFramework>`. Verify by `grep -h TargetFramework flow-*/flow-*.csproj` before commit.
**Warning signs:** `CS8852: ProjectReference 'flow-lang' targets a different framework` at build time.

### Pitfall 2: OmniSharp's last release was September 2023
**What goes wrong:** A reasonable researcher might conclude "abandoned" and propose StreamJsonRpc + manual handlers as an alternative.
**Why it happens:** No 2024+ release on NuGet. But: (a) the GitHub repo shows ongoing PR activity through 2024+, (b) zero deprecation notice, (c) the package targets net6.0 + netstandard2.0 — both forward-compatible with net10.0 by .NET runtime contract, (d) no breaking changes in the LSP spec since 0.19.9 was published. [VERIFIED: github.com/OmniSharp/csharp-language-server-protocol]
**How to avoid:** Treat 0.19.9 as the stable release. If a runtime issue surfaces during execution (e.g., MEF/DI exception under net10), fall back to `StreamJsonRpc` + manual handlers (~300 LoC effort).
**Warning signs:** `MissingMethodException` or `TypeLoadException` at server startup. Mitigation: gate the implementation behind a smoke test in Wave 0 (instantiate `LanguageServer.From(...)` with no handlers, confirm it boots and accepts a `Shutdown` request).

### Pitfall 3: Activating `FlowEngine` inside the LSP pulls in PulseAudio
**What goes wrong:** `FlowEngine` constructor instantiates `AudioPlaybackManager` (`Core/FlowEngine.cs:41`), which loads `PulseAudioSimpleBackend` via P/Invoke — fails on Windows runners, OSX builds, and any Linux without libpulse.
**Why it happens:** The natural reflex is "import FlowEngine, call .Execute()". But D-02 explicitly says no audio in flow-lsp.
**How to avoid:** Do not instantiate `FlowEngine`. Build a small `ParseSession` class inside `flow-lsp/` that does just `SimpleLexer` + `Parser` + `ErrorReporter` (no `Interpreter`, no `AudioPlaybackManager`):
```csharp
public sealed class ParseSession {
    public ParseResult Parse(string source, string? path) {
        var er = new ErrorReporter();
        var tokens = new SimpleLexer(source, er, path).Tokenize();
        var ast = new Parser(tokens, er).Parse();
        return new ParseResult(ast, tokens, er.Errors);
    }
}
```
**Warning signs:** "DllNotFoundException: libpulse-simple.so.0" on a CI runner.

### Pitfall 4: Trimming kills the OmniSharp server
**What goes wrong:** `<PublishTrimmed>true</PublishTrimmed>` shaves the binary from ~70MB to ~25MB, but at the first request the server throws `MissingMethodException` because MediatR's reflection-based handler discovery was trimmed away.
**Why it happens:** OmniSharp uses MediatR (reflection-heavy DI) extensively; trimming requires AOT-compatible patterns that OmniSharp does not follow. [VERIFIED: learn.microsoft.com/dotnet/core/deploying/trimming/trim-self-contained]
**How to avoid:** Keep `<PublishTrimmed>` unset (default false). Use `<PublishSingleFile>true</PublishSingleFile>` + `<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>` only.
**Warning signs:** First-request crash, never a build error. Test the published binary in CI before VSIX-packing it (see §Validation Architecture).

### Pitfall 5: TextMate grammar lookahead breaks on note streams
**What goes wrong:** A naive grammar with `"begin": "\\|"`, `"end": "\\|"` for note streams will treat the second `|` of bar `| C4 D4 |` as the end, but a multi-bar stream `| C4 D4 | E4 F4 |` — also a single `NoteStreamExpression` in the AST — will be split incorrectly. Or worse: `or` boolean operator `(or true false)` (if added later) gets eaten as a stream delimiter.
**Why it happens:** TextMate grammars are line-oriented and regex-based; they cannot model "bars belong to a single stream" the way the parser does.
**How to avoid:** Treat the TM grammar as a permissive coloring layer — color individual notes/chords/durations *between* pipes regardless of stream boundaries. The semantic-tokens handler from the server will give the precise, AST-aware coloring once it boots. Snapshot-test the boundary cases with `vscode-tmgrammar-test` against `tests/*.flow` files that have multi-bar streams.
**Warning signs:** Grammar users see colors flicker across bar boundaries; the snapshot test catches this if it includes multi-bar samples.

### Pitfall 6: Stdlib `.flow` files must ship next to the binary
**What goes wrong:** `ModuleLoader.ResolvePath` uses `Path.GetDirectoryName(typeof(ModuleLoader).Assembly.Location)` to find `@audio` etc. (`flow-lang/Runtime/ModuleLoader.cs:125`). When `flow-lsp.exe` is shipped as a self-contained single-file inside the VSIX, the `.flow` files must sit beside it or `use "@audio"` go-to-def returns null.
**Why it happens:** `flow-lang.csproj` already has `<None Update="*.flow"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>` for all 7 stdlib files (`flow-lang.csproj:16-37`). When the LSP project does `dotnet publish`, those files copy to the publish dir. The VSIX bundling step must include them in the `server/<platform>/` directory structure.
**How to avoid:** `vscode-extension/.vscodeignore` must NOT exclude `server/**/*.flow`. The CI build step should explicitly verify `server/<platform>/audio.flow` exists after publish.
**Warning signs:** `Definition not found` for any `use "@..."` import; user sees error in extension's Output channel about file-not-found at the resolved path.

### Pitfall 7: VSCode `targetPlatform` doesn't include `osx-x64` until you ask for it
**What goes wrong:** Forgetting one of the four platforms means users on that platform see "no extension available for this platform" and the install fails silently.
**Why it happens:** Each `vsce publish --target <platform>` is a separate Marketplace upload; missing one is a silent gap. The four required `vsce` target names are `linux-x64`, `win32-x64`, `darwin-x64`, `darwin-arm64` (note: `win32-x64`, not `win-x64` — different from .NET RIDs!). The CI matrix needs to map .NET RIDs to vsce targets:
| .NET RID | vsce --target | Process.platform | Process.arch |
|----------|---------------|------------------|--------------|
| linux-x64 | linux-x64 | linux | x64 |
| win-x64 | win32-x64 | win32 | x64 |
| osx-x64 | darwin-x64 | darwin | x64 |
| osx-arm64 | darwin-arm64 | darwin | arm64 |
**How to avoid:** Drive the CI matrix from a single map and assert all four publish steps succeed. CONTEXT D-14 explicitly lists all four — do not silently drop any.
**Warning signs:** Marketplace listing shows fewer than 4 platform-specific VSIXs after a release.

### Pitfall 8: OpenVSX namespace must exist before first publish
**What goes wrong:** `ovsx publish` fails with "namespace 'flow-lang' does not exist" on the first ever publish because OpenVSX requires the namespace to be claimed manually first.
**Why it happens:** OpenVSX (Eclipse Foundation) doesn't auto-create namespaces from PAT alone — there's a one-time manual claim via `npx ovsx create-namespace <publisher>`. [CITED: github.com/eclipse/openvsx/wiki]
**How to avoid:** Setup task before the first CI run: `npx ovsx create-namespace <publisher> -p $OPENVSX_PAT`. Document this in the phase's setup README.
**Warning signs:** First CI run after tag push fails on the OpenVSX step with a 404; Marketplace step succeeds. Plan should include an explicit Wave 0 setup task for both PATs + namespace claim.

## Runtime State Inventory

> Greenfield phase — no rename/refactor. Section omitted.

## Code Examples

Verified patterns; minor adaptations from official samples.

### `BuiltInDocs.cs` shape (D-12)

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
        ["transpose"] = new("Transposes a sequence by a semitone or cent interval.", [
            new("seq", "The sequence to transpose."),
            new("interval", "Pitch shift, e.g., +5st or -100c."),
        ]),
        // ... grow as built-ins are added
    };

    public static Doc? TryGet(string name) =>
        _docs.TryGetValue(name, out var doc) ? doc : null;
}
```

### Document manager with debounce (Pattern 3)

```csharp
// flow-lsp/DocumentManager.cs
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

### Diagnostics handler (D-06)

```csharp
// Inside the parse callback:
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
        // SourceLocation is 1-based; LSP Range is 0-based.
        // Length-1 range when end is unknown (lex/parse errors only carry start).
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

### Semantic tokens encoding (D-04)

LSP semantic tokens encode every token as a 5-tuple `[deltaLine, deltaStartChar, length, tokenType, tokenModifiers]` packed into a single `int[]`. Each token's start position is **delta-encoded** relative to the previous token: same line → `deltaLine=0`, `deltaStartChar = currCol - prevCol`; new line → `deltaLine = currLine - prevLine`, `deltaStartChar = currCol` (absolute, since previous line). [CITED: code.visualstudio.com/api/language-extensions/semantic-highlight-guide; github.com/microsoft/language-server-protocol/blob/main/_specifications/lsp/3.17/specification.md]

**Standard `SemanticTokenTypes`** (the legend the server declares to the client):
`namespace, type, class, enum, interface, struct, typeParameter, parameter, variable, property, enumMember, decorator, event, function, method, macro, label, comment, string, keyword, number, regexp, operator`. [CITED: code.visualstudio.com/api/language-extensions/semantic-highlight-guide]

**Standard `SemanticTokenModifiers`**:
`declaration, definition, readonly, static, deprecated, abstract, async, modification, documentation, defaultLibrary`. [CITED: same source]

```csharp
// flow-lsp/Handlers/SemanticTokensHandler.cs (sketch — full impl uses OmniSharp's RegistrationOptions)
//
// Maps Flow TokenType → LSP SemanticTokenType. This is a port of
// flow-editor/Editor/FlowSyntaxHighlighter.cs:95-147 into LSP terminology.
private static readonly SemanticTokenType[] Legend = {
    SemanticTokenType.Keyword,    // index 0 — Flow keywords (proc, use, return, ...)
    SemanticTokenType.Type,       // 1 — Type keywords (Int, Note, Sequence, ...)
    SemanticTokenType.String,     // 2
    SemanticTokenType.Number,     // 3 — Int/Float/Cent/Decibel/Time literals
    SemanticTokenType.Operator,   // 4 — +, -, ->, =>, etc.
    SemanticTokenType.Comment,    // 5
    SemanticTokenType.Variable,   // 6 — note literals (NoteLiteral)
    SemanticTokenType.Function,   // 7 — chord literals (ChordLiteral) — semantic, not by Flow runtime type
    SemanticTokenType.Macro,      // 8 — | pipe delimiters (custom mapping; Macro is closest standard)
};
private static readonly SemanticTokenModifier[] LegendMods = {
    SemanticTokenModifier.Readonly,
    SemanticTokenModifier.Declaration,
};

private static int? MapTokenType(TokenType t) => t switch {
    TokenType.Proc or TokenType.EndProc or TokenType.Return or TokenType.Use
      or TokenType.Internal or TokenType.Lazy or TokenType.Fn
      or TokenType.Tempo or TokenType.Timesig or TokenType.Key or TokenType.Swing
      or TokenType.Dynamics or TokenType.Rit or TokenType.Accel or TokenType.Pickup
      or TokenType.Section or TokenType.For or TokenType.While or TokenType.Break
      or TokenType.Continue or TokenType.In or TokenType.Progression
      or TokenType.Pan or TokenType.Gain => 0,  // keyword
    TokenType.Void or TokenType.Int or TokenType.Float or TokenType.Long
      or TokenType.Double or TokenType.String or TokenType.Bool or TokenType.Number
      or TokenType.Note or TokenType.Buf => 1,  // type
    TokenType.StringLiteral or TokenType.InterpolatedStringText => 2,
    TokenType.IntLiteral or TokenType.FloatLiteral or TokenType.SemitoneLiteral
      or TokenType.CentLiteral or TokenType.TimeLiteral
      or TokenType.DecibelLiteral or TokenType.BoolLiteral => 3,
    TokenType.Arrow or TokenType.FatArrow or TokenType.Plus or TokenType.Minus
      or TokenType.Star or TokenType.Slash or TokenType.LessThan
      or TokenType.GreaterThan or TokenType.Assign => 4,
    TokenType.Comment => 5,
    TokenType.NoteLiteral => 6,
    TokenType.ChordLiteral => 7,
    TokenType.Pipe => 8,
    _ => null,  // skip (Eof, Identifier, delimiters, etc.)
};

// Encoding pseudocode — emit deltas relative to prev (line, col).
// Tokens MUST be sorted by (line, column) ascending; SimpleLexer already produces them in order.
public int[] EncodeTokens(IReadOnlyList<Token> tokens)
{
    var data = new List<int>(tokens.Count * 5);
    int prevLine = 0, prevCol = 0;
    foreach (var t in tokens)
    {
        var typeIdx = MapTokenType(t.Type);
        if (typeIdx is null) continue;
        int line = t.Location.Line - 1;       // LSP is 0-indexed
        int col  = t.Location.Column - 1;
        int dLine = line - prevLine;
        int dCol  = dLine == 0 ? col - prevCol : col;
        data.Add(dLine);
        data.Add(dCol);
        data.Add(t.Text.Length);
        data.Add(typeIdx.Value);
        data.Add(0);  // tokenModifiers bitmask — Claude's discretion per CONTEXT
        prevLine = line;
        prevCol = col;
    }
    return data.ToArray();
}
```

### Completion handler core (D-07)

```csharp
public Task<CompletionList> Handle(CompletionParams req, CancellationToken ct)
{
    var doc = _docManager.Get(req.TextDocument.Uri);
    var prevToken = TokenJustLeftOfCursor(doc, req.Position);

    // Inside `use "..."` string → only stdlib paths
    if (IsInsideUseStringLiteral(doc, req.Position))
        return Task.FromResult(StdlibPathCompletions());

    // Inside | ... | note stream → context-aware (D-11)
    if (IsInsideNoteStream(doc, req.Position))
        return Task.FromResult(NoteStreamCompletions(doc, req.Position));

    // Default: built-ins + stdlib procs + user procs/vars + keywords + snippets
    var items = new List<CompletionItem>();
    items.AddRange(_builtIns.Items);             // from InternalFunctionRegistry
    items.AddRange(_stdlib.Items);               // from parsing flow-lang/*.flow
    items.AddRange(_userSymbols.For(doc.Uri));   // current AST
    items.AddRange(_keywords.Items);             // static keyword list
    items.AddRange(SnippetTemplates());          // tempo/key/timesig/proc/section
    return Task.FromResult(new CompletionList(items));
}
```

### Note-stream context walk (D-11)

```csharp
public IReadOnlyList<CompletionItem> NoteStreamCompletions(Document doc, Position cursor)
{
    var enclosingKey = FindEnclosingKey(doc.Ast, cursor);
    if (enclosingKey is not null)
    {
        // Inside a key { } block → roman numerals
        return new[] {
            CompletionItem("I", $"Tonic chord in {enclosingKey}"),
            CompletionItem("ii", $"Supertonic minor in {enclosingKey}"),
            CompletionItem("iii", $"Mediant minor in {enclosingKey}"),
            CompletionItem("IV", $"Subdominant in {enclosingKey}"),
            CompletionItem("V",  $"Dominant in {enclosingKey}"),
            CompletionItem("V7", $"Dominant 7 in {enclosingKey}"),
            CompletionItem("vi", $"Submediant minor in {enclosingKey}"),
            CompletionItem("vii°", $"Leading-tone diminished in {enclosingKey}"),
        };
    }
    // Outside a key — note letters + chord literals + durations + rest + tie/dot
    return DefaultNoteStreamItems();
}

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

### Hover handler (D-08)

```csharp
public Task<Hover?> Handle(HoverParams req, CancellationToken ct)
{
    var token = TokenAt(req);
    if (token is null) return Task.FromResult<Hover?>(null);

    // 1. Built-in?
    var builtIn = _builtIns.Find(token.Text);
    if (builtIn is not null)
    {
        var doc = BuiltInDocs.TryGet(token.Text);
        var md = $"```flow\n{builtIn.SignatureToString()}\n```\n\n" +
                 (doc?.Summary ?? "*No documentation available.*");
        return Task.FromResult<Hover?>(new Hover { Contents = MarkdownString(md) });
    }
    // 2. User proc/variable in current AST?
    var userSym = _userSymbols.Find(req.TextDocument.Uri, token.Text);
    if (userSym is not null) return Task.FromResult<Hover?>(UserSymbolHover(userSym));

    // 3. Stdlib proc?
    var stdProc = _stdlib.Find(token.Text);
    if (stdProc is not null) return Task.FromResult<Hover?>(StdlibProcHover(stdProc));

    return Task.FromResult<Hover?>(null);
}
```

### `package.json` contributes block (D-13)

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
        "flow.server.path": {
          "type": "string",
          "default": "",
          "description": "Optional override for the bundled flow-lsp binary path."
        },
        "flow.trace.server": {
          "type": "string",
          "enum": ["off", "messages", "verbose"],
          "default": "off",
          "description": "Trace LSP communication."
        }
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

### TypeScript activation with platform detection (D-14)

```typescript
// vscode-extension/src/extension.ts
import * as path from 'path';
import * as fs from 'fs';
import { workspace, ExtensionContext, window } from 'vscode';
import { LanguageClient, LanguageClientOptions, ServerOptions, Executable, TransportKind } from 'vscode-languageclient/node';

let client: LanguageClient | undefined;

function platformDir(): string {
  const platform = process.platform;       // 'linux' | 'win32' | 'darwin'
  const arch = process.arch;               // 'x64' | 'arm64'
  return `${platform}-${arch}`;            // e.g. 'linux-x64', 'darwin-arm64'
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

  // Ensure executable bit on POSIX (VSIX may strip it on extraction).
  if (process.platform !== 'win32') {
    try { fs.chmodSync(binary, 0o755); } catch { /* best-effort */ }
  }

  const exe: Executable = {
    command: binary,
    transport: TransportKind.stdio,
    options: { env: process.env }
  };
  const serverOptions: ServerOptions = { run: exe, debug: exe };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: 'file', language: 'flow' }],
    synchronize: {
      fileEvents: workspace.createFileSystemWatcher('**/*.flow')
    },
    traceOutputChannel: window.createOutputChannel('Flow LSP Trace')
  };

  client = new LanguageClient('flow', 'Flow Language Server', serverOptions, clientOptions);
  await client.start();
}

export function deactivate(): Thenable<void> | undefined {
  return client?.stop();
}
```

### Per-platform CI workflow (D-14, D-15)

```yaml
# .github/workflows/publish-extension.yml
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
          # Send shutdown immediately and assert process exits cleanly.
          # (Detailed handshake: send Initialize, expect InitializeResult, then shutdown.)
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
      - name: Publish to VSCode Marketplace + OpenVSX
        uses: HaaLeo/publish-vscode-extension@v2
        with:
          pat: ${{ secrets.VSCE_PAT }}
          extensionFile: artifacts/vsix-linux-x64/flow-linux-x64.vsix
          registryUrl: https://marketplace.visualstudio.com
        # Loop over all 4 VSIXs — repeat block for win32-x64, darwin-x64, darwin-arm64
      - name: Publish to OpenVSX
        uses: HaaLeo/publish-vscode-extension@v2
        with:
          pat: ${{ secrets.OVSX_PAT }}
          extensionFile: artifacts/vsix-linux-x64/flow-linux-x64.vsix
        # Loop over all 4 VSIXs again for OpenVSX
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Single platform-agnostic VSIX requiring user-installed runtime | Per-platform VSIX with bundled binary | VSCode 1.61, Oct 2021 | Zero-friction install on all 4 platforms; CI matrix overhead. [VERIFIED: code.visualstudio.com/api/working-with-extensions/publishing-extension] |
| `vsce` (deprecated) | `@vscode/vsce` (npm namespace) | ~2022 | Same CLI, scoped under @vscode/. |
| TextMate-only highlighting | TM grammar + LSP semantic tokens | LSP 3.16, 2020 | Lexer-precise coloring without a custom theme. |
| Marketplace-only | Marketplace + OpenVSX | 2020+ | Cursor/VSCodium/Windsurf/Theia users covered. |
| OmniSharp.Roslyn (in `omnisharp-roslyn` repo) | Built-in Roslyn LSP in C# extension | dotnet/vscode-csharp 2.0, 2024 | This is C#-tooling-specific; doesn't affect the LSP framework choice for *building* a server (we're building a server FOR Flow, not for C#). [VERIFIED: github.com/dotnet/vscode-csharp] |

**Deprecated/outdated:**
- `vsce` (unscoped) → use `@vscode/vsce`.
- `Microsoft.VisualStudio.LanguageServer.Protocol` package — geared to VS 2019, "years behind the spec" per kaby76/lsp-types README. [CITED: github.com/kaby76/lsp-types]
- `<PublishTrimmed>true</PublishTrimmed>` for reflection-heavy frameworks like OmniSharp/MediatR — known to break at runtime. [VERIFIED: learn.microsoft.com/dotnet/core/deploying/trimming]

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | OmniSharp 0.19.9 (released Sep 2023) runs cleanly on .NET 10 because it targets net6.0/netstandard2.0 — no breaking change in those frameworks affects it | §Standard Stack, Pitfall 4 | If runtime issue surfaces (e.g., MEF/DI exception), Wave 0 must add a smoke-boot test before further development. Mitigation: see Open Question Q1. [ASSUMED — based on framework forward-compat contract; not empirically tested in this research] |
| A2 | The OmniSharp `SemanticTokensBuilder` exists and handles delta encoding correctly | §Code Examples Semantic Tokens | If missing or buggy, write the ~20-line encoder manually using the algorithm in §Code Examples. Algorithm is from the LSP spec — correct regardless. [ASSUMED — not verified in OmniSharp's public docs in this session] |
| A3 | VSIX size with bundled .NET runtime lands in 30–70MB band per platform | §User Constraints D-14 | If significantly larger (e.g., 200MB), users may complain; could revisit `PublishReadyToRun=false` and/or per-RID size profiling. CONTEXT D-14 already accepts the band. [ASSUMED — typical .NET self-contained single-file size; not measured for this specific app] |
| A4 | The 150ms debounce window is appropriate for typical Flow file size (<2KB scripts) | §User Constraints D-13 | If users on very large files feel lag, retune; if too eager on slow keystrokes, increase. CONTEXT explicitly leaves this to Claude's discretion. [ASSUMED] |
| A5 | The HaaLeo action @v2 is current best-practice and supports both Marketplace and OpenVSX in a single workflow | §Standard Stack | If deprecated, fall back to manual `vsce publish` + `ovsx publish` shell steps. [VERIFIED: action exists, latest in marketplace; the specific @v2 tag verification was not done in this session] |
| A6 | `ovsx create-namespace` is the right one-time setup command | §Common Pitfalls Pitfall 8 | If syntax has changed, README setup task is wrong. Low risk — error message on first publish will be clear. [CITED: eclipse/openvsx wiki] |

## Open Questions

1. **Will OmniSharp 0.19.9 boot cleanly under net10.0?**
   - What we know: targets net6.0/netstandard2.0 (forward-compatible with net10 by .NET runtime contract); no breaking change in those frameworks affects it.
   - What's unclear: not empirically tested; the package hasn't shipped a release since Sep 2023.
   - Recommendation: Wave 0 adds a smoke-boot test (`new LanguageServer.From(...)` with no handlers, send `initialize` + `shutdown`, assert clean exit). If that fails, fall back to `StreamJsonRpc` + manual handlers (~300 LoC; the LSP types ship in `Microsoft.VisualStudio.LanguageServer.Protocol` even if the high-level framework is dated).

2. **Should the LSP also export `flow-interpreter --lsp` as a convenience alias?**
   - What we know: CONTEXT explicitly puts this under Claude's discretion.
   - What's unclear: nothing — recommendation is a developer-experience preference call.
   - Recommendation: Skip for v1. Two binaries (`flow` for the interpreter, `flow-lsp` for the server) is clearer; non-VSCode editor configs reference `flow-lsp` directly.

3. **Where should `BuiltInDocs.cs` actually live — `flow-lang/StandardLibrary/` or `flow-lsp/`?**
   - What we know: CONTEXT D-12 says `flow-lang/StandardLibrary/BuiltInDocs.cs`.
   - What's unclear: a strict reading of D-02 says `flow-lsp` is consumer-only of `flow-lang`. Putting docs in `flow-lang` means the docs ship with the interpreter too (potentially useful for `--help` style features later) but adds a slightly off-topic file to the language library.
   - Recommendation: Honor D-12 — put it in `flow-lang/StandardLibrary/`. Future interpreter `--help <fn>` could reuse it.

4. **What happens to TM grammar coloring during the ~1s server-spawn window?**
   - What we know: The TM grammar paints first; semantic tokens overlay later.
   - What's unclear: Whether VSCode triggers a flicker when the overlay arrives.
   - Recommendation: Test in Extension Development Host during Wave 1; document in extension README if there's noticeable visual change.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK 10 | Building flow-lsp + tests | ✓ | 10.0.106 | — [VERIFIED: `dotnet --list-sdks`] |
| .NET SDK 9 | (not needed; project is net10) | ✓ | 9.0.116 | — |
| Node.js 20 | VSCode extension build, vsce, ovsx | ✓ | v20.16.0 | — [VERIFIED: `node --version`] |
| npm | Extension dependency install | ✓ | 10.8.1 | — |
| `@vscode/vsce` | Package + publish VSIX | ✗ (not installed locally) | latest 3.9.1 on registry | `npm install -g @vscode/vsce` per-CI |
| `ovsx` | Publish to OpenVSX | ✗ (not installed locally) | latest 0.10.11 on registry | `npm install -g ovsx` per-CI |
| nuget.org access | Pull OmniSharp + transitive packages | ✓ | — | Mirror config not required |
| GitHub Actions runners (ubuntu/windows/macos x64+arm64) | CI matrix | ✓ (assumed; project hosted on GitHub per `gitStatus`) | — | — |
| `VSCE_PAT` (Azure DevOps token) | Marketplace publish | ✗ | — | Setup task in Wave 0; one-time generation. |
| `OVSX_PAT` (Eclipse OpenVSX token) | OpenVSX publish | ✗ | — | Setup task in Wave 0; one-time generation. |
| OpenVSX namespace claim for the publisher | First-ever OpenVSX publish | ✗ | — | One-time `npx ovsx create-namespace` (Pitfall 8). |

**Missing dependencies with no fallback:**
- VSCE_PAT and OVSX_PAT — must be created by a human and added as GitHub Actions secrets before CI can publish. Plan must include this as a setup task in Wave 0 (or Wave 3 immediately before the first tag push).

**Missing dependencies with fallback:**
- `vsce` and `ovsx` are CI-only; no local install required.

## Validation Architecture

> Per `workflow.nyquist_validation: true` in `.planning/config.json`.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 (already in `flow-lang.Tests/`) for C#; vscode-tmgrammar-test 0.1.x for TM grammar |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj`; new `vscode-extension/tests/grammar/` for TM tests |
| Quick run command | `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17"` |
| Full suite command | `dotnet test flow-sharp.sln` + (in `vscode-extension/`) `npx vscode-tmgrammar-test 'tests/grammar/**/*.flow'` + `npx vscode-tmgrammar-snap -g syntaxes/flow.tmLanguage.json 'tests/grammar/**/*.flow'` |

### Phase Requirements → Test Map

LSP servers are notoriously hard to e2e-test. Strategy: test each layer at the lowest level that gives a useful guarantee. Three test layers:

- **L1 (in-process, fast):** Construct handlers directly with a fake `LanguageServer` shim or with the real one + an in-memory pipe; assert outputs.
- **L2 (golden-file, fast):** Snapshot tests for TM grammar and semantic-tokens encoding.
- **L3 (manual smoke):** Extension Development Host run-through against a checklist `.flow` file.

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| D-01 | OmniSharp boots on net10 + accepts `initialize`/`shutdown` | L1 integration | `dotnet test --filter "FullyQualifiedName~Phase17.OmniSharpBootTest"` | ❌ Wave 0 |
| D-02 | flow-lsp.csproj has no audio dependencies | L1 unit (build-time) | `dotnet build flow-lsp/ --verbosity normal` checked for absence of `PulseAudio` references | ❌ Wave 0 |
| D-03 | DocumentManager debounces correctly + cancels in-flight | L1 unit | `dotnet test --filter "FullyQualifiedName~DocumentManagerTests"` | ❌ Wave 0 |
| D-04 | Semantic tokens encoding matches spec | L2 golden | `dotnet test --filter "FullyQualifiedName~SemanticTokensTests"` (Theory pinning known-good outputs for sample .flow scripts) | ❌ Wave 0 |
| D-04 | TM grammar matches snapshot for sample `.flow` files | L2 golden | `npx vscode-tmgrammar-snap` | ❌ Wave 0 |
| D-05 | All TM scopes are standard (no `*.flow`-invented) | L1 unit | C# Fact reads `flow.tmLanguage.json` and asserts every scope matches a regex of standard prefixes (`keyword.*`, `entity.*`, `string.*`, etc.) | ❌ Wave 0 |
| D-06 | Every `ErrorReporter` error becomes an LSP `Diagnostic` with correct severity + range | L1 unit | `dotnet test --filter "FullyQualifiedName~DiagnosticsHandlerTests"`; Theory feeds known-bad source strings, asserts diagnostic count + severity + range | ❌ Wave 0 |
| D-07 | Completion returns built-ins + stdlib + keywords + user symbols | L1 unit | `dotnet test --filter "FullyQualifiedName~CompletionHandlerTests"`; multiple Facts: built-in `print`, stdlib `mix`, keyword `proc`, user proc declared in fixture | ❌ Wave 0 |
| D-07 | Completion in `use "..."` returns all 6 stdlib paths | L1 unit | Same suite, `UseStringCompletionsTest` Fact | ❌ Wave 0 |
| D-08 | Hover for `print` shows signature + BuiltInDocs summary | L1 unit | `HoverHandlerTests.PrintShowsDoc` Fact | ❌ Wave 0 |
| D-09 | Go-to-def for `mix` resolves to `flow-lang/audio.flow` line N | L1 unit | `DefinitionHandlerTests.MixGoesToAudioFlow` Fact | ❌ Wave 0 |
| D-10 | Signature help inside `(transpose seq, ` highlights param 2 | L1 unit | `SignatureHelpHandlerTests.ActiveParameter` Fact | ❌ Wave 0 |
| D-11 | Inside `key Cmajor { | I IV V | }` cursor inside stream returns roman numerals | L1 unit | `NoteStreamContextTests.RomanNumeralsInKey` Fact | ❌ Wave 0 |
| D-11 | Inside note stream WITHOUT key block returns notes/durations only (no procs) | L1 unit | `NoteStreamContextTests.NoProcsInStream` Fact | ❌ Wave 0 |
| D-12 | `BuiltInDocs.TryGet` returns null for absent key (hover falls back) | L1 unit | `BuiltInDocsTests.MissingKeyReturnsNull` Fact | ❌ Wave 0 |
| D-13 | Extension activates on `onLanguage:flow` | L3 manual | F5 in Extension Development Host, open `*.flow` file, see status bar `Flow Language Server` | ❌ Manual checklist |
| D-14 | Each per-platform VSIX bundles the platform's binary + all 7 stdlib `.flow` files | L1 CI (bash) | `test -f vscode-extension/server/${TARGET}/audio.flow && file vscode-extension/server/${TARGET}/flow-lsp* | grep -q <expected-arch>` | ❌ Wave 3 |
| D-14 | Smoke-boot of each per-platform binary completes `initialize` + `shutdown` cleanly | L1 CI (script) | `./scripts/lsp-smoke.sh vscode-extension/server/${TARGET}/flow-lsp*` | ❌ Wave 3 |
| D-15 | CI workflow successfully runs `vsce package` for all 4 targets on tag push | L1 CI | GitHub Actions matrix completes; check artifact list | ❌ Wave 3 |
| D-15 | CI workflow successfully publishes to both Marketplace + OpenVSX on tag push (dry-run testable) | L1 CI | `vsce publish --no-publish` + `ovsx publish --no-upload` (or equivalent dry mode) | ❌ Wave 3 |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Phase17"` (in-process LSP handler tests, target <30s)
- **Per wave merge:** `dotnet test flow-sharp.sln` + `npx vscode-tmgrammar-test` over snapshot fixtures
- **Phase gate:** Full suite green + a manual smoke checklist run in Extension Development Host on Linux + at least one OS that wasn't the dev box (CI runs the four-platform smoke-boot, but visual rendering needs human eyes once)

### Wave 0 Gaps
- [ ] `flow-lsp/flow-lsp.csproj` — new project, must build cleanly under net10.0
- [ ] `flow-lsp/Program.cs` — minimum-viable bootstrap (Wave 0 smoke test target)
- [ ] `flow-lang/StandardLibrary/BuiltInDocs.cs` — D-12 lookup table (must exist before hover/completion handlers can read it)
- [ ] `flow-lang.Tests/Unit/Phase17/` — directory + at least one fixture loading helper
- [ ] `vscode-extension/` — directory + `package.json` + minimal `extension.ts` (build the empty scaffold first; validates the npm tooling)
- [ ] `vscode-extension/syntaxes/flow.tmLanguage.json` — minimum viable grammar (can grow over the phase)
- [ ] `vscode-extension/tests/grammar/sample.flow` — first snapshot fixture
- [ ] `scripts/lsp-smoke.sh` — sends `initialize` + `shutdown` to a binary and asserts exit 0
- [ ] `.github/workflows/publish-extension.yml` — matrix workflow

## Security Domain

> `security_enforcement` is not set in `.planning/config.json`; treating as enabled per default. The phase's security surface is small but not zero.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | LSP server has no user auth; runs as the editor's process. |
| V3 Session Management | no | No sessions. |
| V4 Access Control | no | Trust model: editor trusts the server; server trusts buffers from the client. |
| V5 Input Validation | yes (low risk) | Source code from untrusted .flow files passes through `SimpleLexer` + `Parser`; both are already hardened (soft-failure model, max parse depth 500). LSP wire input is JSON; OmniSharp validates against the protocol schema. |
| V6 Cryptography | no | No crypto in scope. |
| V14 (Configuration) | yes | VSCE_PAT and OVSX_PAT must NOT be committed; must be GitHub Actions secrets only. |

### Known Threat Patterns for {LSP server + VSCode extension}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal via `use "../../etc/passwd"` (importing arbitrary files) | Information Disclosure | The LSP only *parses* the file referenced by `use`; it never executes it. `ModuleLoader` resolves `@`-prefix to assembly-dir only; arbitrary-relative `use "../foo.flow"` resolves under the workspace, which the user controls anyway. No mitigation needed beyond the existing parser surface. |
| Maliciously crafted .flow file crashes the server | DoS | `SimpleLexer` and `Parser` already have bounds (max parse depth 500, max error count 50). LSP server should additionally catch unhandled exceptions in handlers and log via `LogMessage` instead of crashing. |
| PAT leak via committed workflow file | Information Disclosure | Use `${{ secrets.VSCE_PAT }}` and `${{ secrets.OVSX_PAT }}` exclusively; add `*.pat`, `.env`, `*.secret` to `.gitignore`; document setup in a NON-committed README section. |
| Bundled .NET runtime in VSIX has known CVE | Tampering | Pin a specific .NET 10 SDK minor version in CI; rebuild + republish on advisory. (Out of scope for this phase but worth noting.) |
| Malicious extension impersonates flow-language | Spoofing | OpenVSX namespace claim (Pitfall 8) is the standard mitigation; once claimed, the namespace is locked to the publisher. |

## Sources

### Primary (HIGH confidence)
- nuget.org/packages/OmniSharp.Extensions.LanguageServer/0.19.9 — package metadata, target frameworks, transitive deps, publish date
- github.com/OmniSharp/csharp-language-server-protocol — repo activity, samples
- github.com/OmniSharp/csharp-language-server-protocol/blob/master/sample/SampleServer/Program.cs — minimal LanguageServer.From() shape
- code.visualstudio.com/api/working-with-extensions/publishing-extension — vsce target list, .vscodeignore, multi-platform recipe
- code.visualstudio.com/api/language-extensions/semantic-highlight-guide — standard SemanticTokenTypes / SemanticTokenModifiers enums; provider registration
- code.visualstudio.com/api/language-extensions/syntax-highlight-guide — TextMate scope conventions
- code.visualstudio.com/api/language-extensions/language-server-extension-guide — package.json contributes shape, vscode-languageclient setup
- learn.microsoft.com/dotnet/core/deploying/trimming/trim-self-contained — trimming reflection caveats
- github.com/HaaLeo/publish-vscode-extension — Marketplace + OpenVSX dual publish action (latest = v2)
- github.com/PanAeon/vscode-tmgrammar-test — TM grammar snapshot testing tool
- existing repo: `flow-lang/Lexing/SimpleLexer.cs`, `flow-lang/Lexing/TokenType.cs`, `flow-lang/Diagnostics/*`, `flow-lang/Core/SourceLocation.cs`, `flow-lang/Core/FlowEngine.cs`, `flow-lang/Runtime/ModuleLoader.cs`, `flow-lang/StandardLibrary/InternalFunctionRegistry.cs`, `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs`, `flow-lang/Runtime/MusicalContext.cs`, `flow-lang/Runtime/ExecutionContext.cs`, `flow-lang/Ast/**/*.cs`, `flow-editor/Editor/FlowSyntaxHighlighter.cs`, `flow-interpreter/Program.cs`, all `flow-*/flow-*.csproj`, `flow-lang/std.flow`, `flow-lang/audio.flow`

### Secondary (MEDIUM confidence)
- martinbjorkstrom.com/posts/2018-11-29-creating-a-language-server — minimal C# LSP walkthrough (slightly old)
- github.com/microsoft/vs-streamjsonrpc — alternative if OmniSharp falls through
- github.com/kaby76/lsp-types — community port of MS LSP types
- npm registry — verified `vscode-languageclient` 9.0.1 latest, `@vscode/vsce` 3.9.1, `ovsx` 0.10.11

### Tertiary (LOW confidence)
- Spec quotes for SemanticTokens 5-tuple encoding came from training knowledge of LSP 3.17 supplemented by VSCode docs (could not fetch the canonical microsoft.github.io spec page directly during this session — page returned only TOC). The encoding is well-established and matches the VSCode docs description; risk of error is low but flagged here for transparency.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — versions verified via NuGet + npm, csproj inspection, and existing similar projects (Dafny, Yarn Spinner)
- Architecture: HIGH — patterns mirror the existing `flow-editor` lexer-based highlighter and standard LSP-server idioms
- Pitfalls: HIGH for the project-specific ones (net10 drift, FlowEngine pulls audio, stdlib path resolution, trimming risk); MEDIUM for the publishing-side ones (HaaLeo action stability)
- Code examples: MEDIUM — adapted from official samples but not compile-tested in this session; planner should expect minor signature drift in OmniSharp 0.19.9 API surface

**Research date:** 2026-04-20
**Valid until:** 2026-05-20 (30 days; .NET tooling and VSCode marketplace are stable; OmniSharp's lack of recent releases is the only fast-moving concern)
