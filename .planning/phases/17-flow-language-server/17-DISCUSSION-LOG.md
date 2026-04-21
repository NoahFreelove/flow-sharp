# Phase 17: Flow Language Server - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in `17-CONTEXT.md` — this log preserves the alternatives considered.

**Date:** 2026-04-20
**Phase:** 17-flow-language-server
**Areas discussed:** Server stack & reuse, Highlighting approach, Diagnostics + intelligence scope, Distribution & editor targets

---

## Gray-area selection

| Option | Description | Selected |
|--------|-------------|----------|
| Server stack & reuse | C# LSP reusing flow-lang vs Node/TS reimplementation vs hybrid subprocess | ✓ |
| Highlighting approach | TextMate grammar vs LSP semantic tokens vs both | ✓ |
| Diagnostics + intelligence scope for v1 | How much of ErrorReporter to forward; how deep completion/hover/signature go | ✓ |
| Distribution & editor targets | Editor breadth, server shipping, marketplace publishing | ✓ |

**User's choice:** All four areas selected.
**Notes:** User wanted a full pass across every gray area.

---

## Server stack & reuse

### Q1: How should the LSP server be implemented?

| Option | Description | Selected |
|--------|-------------|----------|
| C# project reusing flow-lang (Recommended) | New `flow-lsp` csproj referencing flow-lang; OmniSharp.Extensions.LanguageServer; reuses SimpleLexer, Parser, ErrorReporter, InternalFunctionRegistry | ✓ |
| Separate TypeScript server | Node LSP re-implementing parser; smaller footprint but duplicates logic and drifts from authoritative behavior | |
| Hybrid: TS extension shells out to flow-interpreter | Thin TS extension + `flow-interpreter --lsp` subprocess; cheapest to build but bad UX | |

### Q2: Project layout

| Option | Description | Selected |
|--------|-------------|----------|
| New `flow-lsp` project sibling to flow-interpreter (Recommended) | Separate entry point; no audio deps dragged in | ✓ |
| Subcommand in flow-interpreter | `flow-interpreter --lsp`; fewer artifacts but pulls audio deps into LSP installs | |
| Library in flow-lang + thin bootstrap csproj | LSP handlers inside flow-lang; tight coupling | |

### Q3: Parser reuse strategy for incremental editing

| Option | Description | Selected |
|--------|-------------|----------|
| Full re-lex + re-parse on each change, ~150ms debounce (Recommended) | Simplest; ErrorReporter's soft-failure model returns AST + diagnostics in one pass | ✓ |
| Incremental parsing with persistent tree | Complex; not justified for typical .flow sizes | |
| Evaluator-level caching | Runs imports once; adds execution risk and complicates v1 | |

**Notes:** Decision locked cleanly to the recommended path across all three questions.

---

## Highlighting approach

### Q1: How should VSCode color .flow files?

| Option | Description | Selected |
|--------|-------------|----------|
| TextMate grammar + LSP semantic tokens (Recommended) | Grammar for pre-server baseline; semantic tokens from SimpleLexer for precision | ✓ |
| LSP semantic tokens only | No color before server starts | |
| TextMate grammar only | Fast to ship but misses lexer-precise distinctions | |

### Q2: Palette / theme mapping

| Option | Description | Selected |
|--------|-------------|----------|
| Map to standard VSCode scopes; user's theme picks colors (Recommended) | Standard TextMate scope names; no bundled theme | ✓ |
| Standard scopes + bundled 'Flow Mocha' theme | Optional flow-editor palette match | |
| Invent custom flow-specific scopes | Most precise but no default theme picks them up | |

**Notes:** User kept VSCode convention — theme agnosticism preferred over flow-editor palette import.

---

## Diagnostics + intelligence scope

### Q1: Which diagnostics should the LSP surface for v1?

| Option | Description | Selected |
|--------|-------------|----------|
| Everything ErrorReporter already produces (Recommended) | Lex, parse, undefined identifier, overload, type mismatch | ✓ |
| Lex + parse errors only | Ship syntax first, defer semantic | |
| Everything + runtime warnings | Unused imports, shadowed names — new analysis passes | |

### Q2: Which completion intelligence should ship for v1?

| Option | Description | Selected |
|--------|-------------|----------|
| Built-in functions + stdlib imports + keywords (Recommended) | Registry + module paths + keywords + snippets | |
| Built-in functions only | Just registry; no keywords or stdlib | |
| Full set plus user-defined symbols | Walk AST for in-scope variables, procs, imported names | ✓ |

**Notes:** User explicitly went beyond recommendation — wants user-defined symbols in completion from day one.

### Q3: What should hover show?

| Option | Description | Selected |
|--------|-------------|----------|
| Signature + short inline doc (Recommended) | Registry signature + one-line description; user symbols → declared type | ✓ |
| Signature only | Cheaper but bare | |
| Auto-pull from wiki/CLAUDE.md | Brittle; wiki prose not structured | |

### Q4: Navigation for v1?

| Option | Description | Selected |
|--------|-------------|----------|
| Only go-to-definition for user procs and variables (Recommended) | Jump to declaration in same file or imported stdlib .flow file | ✓ |
| Defer all navigation | No navigation in v1 | |
| Go-to-def + find-references + rename | Stretches scope | |

### Q5: Signature help?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — for built-ins and user procs (Recommended) | Both symbol sources share completion infra | ✓ |
| Yes, but only for built-ins | Skip AST walk for user procs | |
| Defer to a later phase | Polish, not critical | |

### Q6: Note-stream completion context-awareness

| Option | Description | Selected |
|--------|-------------|----------|
| Context-aware: roman numerals / chord shapes based on active key (Recommended) | Walk enclosing `key { }` block, suggest `I`, `ii`, `IV`, `V7` with resolved chord | ✓ |
| Stream-aware but not key-aware | Note letters + chord literals + durations, no roman-numeral resolution | |
| Normal identifier completion | No special-casing | |

### Q7: Built-in doc source

| Option | Description | Selected |
|--------|-------------|----------|
| New `flow-lang/StandardLibrary/BuiltInDocs.cs` lookup table (Recommended) | Single maintainable file; starter set in v1 | ✓ |
| Inline `[Description]` attributes at registration sites | Colocation but spread across 20+ files | |
| Sidecar markdown files | Rich content but I/O overhead at startup | |

**Notes:** User went aggressive on scope — user-symbol completion, context-aware note-stream completion, signature help, and starter doc registry all in v1.

---

## Distribution & editor targets

### Q1: Which editors should Phase 17 target?

| Option | Description | Selected |
|--------|-------------|----------|
| VSCode-first, LSP-generic (Recommended) | VSCode extension + plain-LSP-over-stdio; docs for Neovim/Helix | ✓ |
| VSCode only | Single-editor focus | |
| VSCode + first-class Neovim/Helix extensions | Extra scope, uncertain payoff | |

### Q2: Server-binary shipping strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Bundle self-contained binaries in VSIX per-platform (Recommended) | `dotnet publish --self-contained` for linux-x64, win-x64, osx-x64, osx-arm64; VSIX ~30–70MB | ✓ |
| Require users to install .NET 9 and `dotnet tool install` | Tiny VSIX but friction | |
| Linux-x64 only for v1 | Expand later | |

### Q3: Publishing scope

| Option | Description | Selected |
|--------|-------------|----------|
| Marketplace + OpenVSX in v1 (Recommended) | Covers VSCode + Cursor/VSCodium/Windsurf; automated via CI | ✓ |
| Local VSIX only | GitHub release sideload; no discoverability | |
| Marketplace only (skip OpenVSX) | Closes off non-MS forks | |

**Notes:** User committed to full v1 distribution — zero-install VSIX across 4 platforms, published to both marketplaces.

---

## Claude's Discretion

- Debounce window value (150ms starter; tune during execution).
- Completion detail wording and doc blurb editorial voice.
- Server logging verbosity / `--trace` flags.
- Semantic token modifier granularity.
- VSCode extension manifest metadata beyond required fields.
- CI matrix specifics.
- Snippet template body details (follow VSCode convention).
- Whether to add `flow-interpreter --lsp` as a convenience alias alongside the standalone `flow-lsp` binary.

## Deferred Ideas

- Find-references, rename refactoring, code actions / quick-fixes.
- Runtime analysis warnings (unused imports, shadowed names).
- Doc strings auto-pulled from `wiki/` or `CLAUDE.md`.
- First-class non-VSCode editor extensions published to package managers.
- Marketplace pre-release channel and telemetry.
- `flow-interpreter --lsp` convenience alias (Claude's discretion whether to include).
- Incremental parser (only if re-parse proves too slow in profiling).
