# Phase 17: Flow Language Server - Context

**Gathered:** 2026-04-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Ship an LSP server plus a VSCode extension that deliver, for `.flow` files:

1. Syntax highlighting that matches what `flow-editor/` already renders (token categories sourced from `SimpleLexer`).
2. Live diagnostics sourced from the interpreter's real parser + evaluator bind phase — i.e., every error `ErrorReporter` already accumulates.
3. Intelligent completion, signature help, hover, and go-to-definition covering built-in functions, stdlib modules (`use "@..."`), language keywords, user-declared procs/variables, imported names, and note-stream-aware musical context.
4. A distribution path that ships the server binary bundled inside a VSCode extension, published to the Marketplace + OpenVSX, while also remaining usable as a generic LSP server for any editor.

Out of scope for this phase:
- Find-references, rename refactoring, code actions/quick-fixes (future phases).
- New language analysis passes (unused-import warnings, shadowed-name warnings, linting).
- First-class non-VSCode editor extensions published to package managers (generic LSP over stdio + docs is sufficient for v1).
- Pulling doc strings from `wiki/` or `CLAUDE.md` via heuristic parsing.

</domain>

<decisions>
## Implementation Decisions

### Server Stack & Reuse
- **D-01:** Implement the LSP in C# as a new project that references `flow-lang` directly, using `OmniSharp.Extensions.LanguageServer` as the LSP framework. Reuse `SimpleLexer`, `Parser`, `ErrorReporter`, `InternalFunctionRegistry`, and `TypeSystem` unchanged — no re-implementation of the parser in another language. This matches the minimal-dependencies philosophy and mirrors what `flow-editor/Editor/FlowSyntaxHighlighter.cs` already does.
- **D-02:** New sibling csproj `flow-lsp/` in the solution, separate from `flow-interpreter`. Both reference `flow-lang`. Keeps the LSP free of audio/PulseAudio dependencies so the shipped server binary stays lean.
- **D-03:** On `textDocument/didChange`, debounce ~150ms, then full re-lex + re-parse of the buffer. `ErrorReporter`'s soft-failure model already returns a full AST + diagnostic list in a single pass — this is the natural LSP dispatch point. No incremental parser, no evaluator-level caching for v1.

### Syntax Highlighting
- **D-04:** Hybrid model — ship a TextMate grammar (`flow.tmLanguage.json`) for baseline coloring that works before the server starts and for users without .NET, plus LSP semantic tokens from `SimpleLexer` layered on top for lexer-precise refinement (chord vs identifier, roman numerals inside `key { }`, musical context keywords).
- **D-05:** Map tokens to standard VSCode TextMate scopes (`keyword.control`, `string.quoted`, `constant.numeric`, `entity.name.function`, `variable.other.note`, etc.). The user's active theme colors everything. Do NOT ship a bundled Flow color theme and do NOT invent `*.flow`-specific scopes. Contributors adding new tokens should pick the closest standard scope rather than inventing one.

### Diagnostics
- **D-06:** Forward everything `ErrorReporter` produces during the existing parse + bind pipeline to LSP `publishDiagnostics`: lex errors, parse errors, undefined identifiers, no-matching-overload errors, and type mismatches. Each diagnostic carries the `SourceLocation` from `FlowError`. No new analysis passes in v1.

### Completion
- **D-07:** Complete over the full symbol universe:
  - Every built-in in `InternalFunctionRegistry` (with type signature in the completion detail).
  - Every stdlib module path — `@std`, `@audio`, `@collections`, `@bars`, `@notation`, `@composition` — for `use "..."`.
  - Language keywords: `proc`, `use`, `section`, `tempo`, `key`, `timesig`, `swing`, `dynamics`, etc.
  - User-declared symbols from the current buffer's AST: local variables, procs, imported names (follow `use` statements to their module's top-level declarations).
  - Snippet templates for block constructs: `tempo ${1:120} { $0 }`, `key ${1:Cmajor} { $0 }`, `timesig ${1:4}/${2:4} { $0 }`, `proc ${1:name} () { $0 }`, `section ${1:name} { $0 }`.
- **D-11:** Inside note streams (`| ... |`), completion is context-aware. Walk up the enclosing AST to find the active `key { }` block. If found, suggest roman numerals (`I`, `ii`, `iii`, `IV`, `V`, `V7`, `vi`, `vii°`) with the completion detail resolving to the actual chord via `HarmonyFunctions`. Outside a `key` block (or at any depth without one), suggest note letters + chord literals + durations (`q`, `h`, `w`, `e`, `s`) + rests (`_`) + tie/dot suffixes. Do not offer proc-name completions inside note streams.

### Hover & Navigation
- **D-08:** Hover shows signature + a short inline doc. Built-ins: full signature from `InternalFunctionRegistry` (`reverb(Buffer, Double, Double) -> Buffer`) plus a one-line description. User symbols: declared type.
- **D-09:** Go-to-definition works for user procs and variables, and for imported names (jump to the `.flow` stdlib file). Built-ins report `no definition available` (they live in C# registration code — no user-level definition to navigate to).
- **D-10:** Signature help (active-parameter hint while typing inside `foo(...)`) works for both built-ins and user procs. Shares the same symbol sources as completion.

### Doc Source for Built-ins
- **D-12:** Create `flow-lang/StandardLibrary/BuiltInDocs.cs` as a single lookup table mapping built-in name → one-line summary + optional parameter blurbs. Ship with a starter set covering the most-used built-ins (stdio, arithmetic, collections, audio core, chord/note operations); grows over time as contributors add entries. If no doc exists for a name, hover falls back to signature-only. This keeps the hover-content source in one discoverable file and avoids spreading `[Description]` attributes across 20+ registration sites.

### Distribution
- **D-13:** VSCode-first, LSP-generic. The VSCode extension is the flagship artifact in v1; the server binary speaks plain LSP over stdio so Neovim, Helix, Emacs, Zed, Cursor, Windsurf, and VSCodium all work. Ship a `docs/editor-setup/` directory with ≥1 snippet for `nvim-lspconfig` or `helix languages.toml` so non-VSCode users have a starting point. Do NOT build editor-specific extensions or publish to editor-specific package managers in this phase.
- **D-14:** Ship self-contained server binaries bundled inside the VSIX, one per platform. CI produces `dotnet publish -r linux-x64 --self-contained`, `win-x64`, `osx-x64`, and `osx-arm64` builds and packs them into the VSIX. The extension activation selects the right binary based on `process.platform + process.arch`. User installs the extension → it just works, no `.NET` SDK required. Accept a ~30–70MB VSIX per-platform.
- **D-15:** Publish to the official VSCode Marketplace AND to OpenVSX in v1. This covers Cursor, VSCodium, Windsurf, and Gitpod/Theia users in addition to stock VSCode. Automate both via CI on git tag push. Requires one-time publisher setup (Azure DevOps Personal Access Token + OpenVSX token), which is a setup task inside this phase.

### Claude's Discretion
- Debounce window concrete value (150ms is a starting point — tune during execution if it feels laggy or too eager).
- Exact wording and granularity of completion detail strings (signatures are mechanical; doc blurbs need an editorial voice).
- Progress reporting / logging verbosity of the server process (`--trace` etc.).
- Semantic token modifier strategy (how richly to decorate tokens with modifiers like `readonly`, `declaration`).
- Extension manifest metadata details (icon, categories, keywords) beyond what's required for Marketplace publishing.
- CI matrix specifics (GitHub Actions vs alternative, matrix job shape).
- Snippet template bodies for block constructs — follow VSCode snippet conventions.
- Decision on whether to also wire a `flow-lsp` entry point into `flow-interpreter --lsp` as a convenience alias (nice-to-have, not blocking).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + project constraints
- `.planning/ROADMAP.md` §Phase 17 — goal, dependency (Phase 12), success criteria placeholder.
- `.planning/PROJECT.md` §"Key Decisions", §"Constraints", §"Active Requirements" (LSP listed at line 104) — minimal-deps philosophy, .NET 9 target, hand-written parser decision.
- `CLAUDE.md` §"Technology Stack", §"Guiding Principle: Minimal Dependencies" — OmniSharp.Extensions.LanguageServer was not pre-vetted in the stack doc; planner/researcher MUST verify it is acceptable under the minimal-deps philosophy or propose the minimum alternative.

### Parser + diagnostic reuse surface (the "flow-lang side")
- `flow-lang/Lexing/SimpleLexer.cs` — token types and scan logic. Source of truth for both the TextMate grammar (categories to replicate) and the LSP semantic tokens.
- `flow-lang/Lexing/TokenType.cs` — 78 token types; semantic-token legend derives from this.
- `flow-lang/Parsing/Parser.cs` — recursive-descent parser; produces full AST + diagnostic list under soft-failure model.
- `flow-lang/Parsing/TypeParser.cs` — type annotation parsing.
- `flow-lang/Diagnostics/ErrorReporter.cs` — accumulator the LSP forwards to `publishDiagnostics`.
- `flow-lang/Diagnostics/FlowError.cs` — message + location carrier.
- `flow-lang/Diagnostics/DiagnosticLevel.cs` — severity mapping for LSP `DiagnosticSeverity`.
- `flow-lang/Core/SourceLocation.cs` — mapping to LSP `Range`.

### Symbol sources (completion, hover, signature help)
- `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` — signature → lambda map; source of truth for every built-in's name, parameter types, return type.
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` and `flow-lang/StandardLibrary/Audio/*.cs`, `Harmony/*.cs`, `Transforms/*.cs` — registration sites; completion + signature help traverse these.
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` — roman-numeral → chord resolver; used by note-stream context-aware completion inside `key { }` blocks.
- `flow-lang/Runtime/NoteStreamCompiler.cs` — note-stream grammar reference for completion inside `| ... |`.
- `flow-lang/TypeSystem/` (PrimitiveTypes/, SpecialTypes/, `ArrayType.cs`) — type names surfaced in signatures.
- `flow-lang/*.flow` (std, collections, audio, bars, notation, composition) — go-to-def targets for `use "@..."` imports; stdlib completion namespace.

### Existing prior art
- `flow-editor/Editor/FlowSyntaxHighlighter.cs` — existing token → palette mapping (Catppuccin Mocha); blueprint for the TextMate grammar's scope assignment. Note: palette is NOT shipped; only the categorization logic transfers.
- `flow-editor/Editor/ScopeColorizer.cs` — scope-bracket coloring reference (informational only; LSP is not shipping bracket-scope coloring in v1).
- `flow-interpreter/Program.cs` §watch mode — reference for how the interpreter handles continuous file re-reads; LSP's didChange handling mirrors the same parse-on-change pattern.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`SimpleLexer`**: token stream for both highlighting (semantic tokens) and snippet disambiguation inside note streams.
- **`Parser` + `ErrorReporter`**: one-pass soft-failure parse yields AST + diagnostics → directly maps to LSP `publishDiagnostics`.
- **`InternalFunctionRegistry`**: every built-in's signature is introspectable → completion + signature help + hover.
- **`HarmonyFunctions` + `ChordParser`**: roman-numeral resolution for context-aware completion inside `key { }`.
- **`NoteStreamCompiler` semantics**: definition of legal note-stream tokens → governs completions inside `| ... |`.
- **`flow-editor/Editor/FlowSyntaxHighlighter.cs`**: existing token-category mapping to port to TextMate scopes.
- **`flow-lang/*.flow` stdlib files**: go-to-def targets; also parseable to extract top-level `proc` names for module-level completion.

### Established Patterns
- **Soft-failure error model** (`ErrorReporter` accumulates without throwing): ideal for LSP where every keystroke produces a mostly-broken buffer. Do not change this contract.
- **Parse-time flow operator transform (`->` → nested call)**: semantic tokens see the post-transform shape, which is fine — users will hover on the surface syntax and expect to see the chained function.
- **Musical context as scoped stack** (`ExecutionContext`): note-stream completion context-awareness must walk the AST-enclosing `MusicalContextStatement` nodes, not run the evaluator. The LSP is a parse-time tool; do not bind an evaluator inside the server.
- **`flow-editor` reuses flow-lang for coloring**: same pattern applies server-side. Do not duplicate the lexer.

### Integration Points
- `flow-sharp.sln`: add `flow-lsp/flow-lsp.csproj` as a new project entry; reference from the VSCode extension's bundle script.
- VSCode extension lives in a new directory (not in `flow-lang/`) — proposed path: `vscode-extension/` at the repo root, with its own `package.json`, `tsconfig.json`, and a small TS activation file that spawns the bundled server binary.
- TextMate grammar: `vscode-extension/syntaxes/flow.tmLanguage.json`. Authored by hand during the phase; validated against sample `.flow` files from `tests/` and `examples/`.
- CI: GitHub Actions workflow builds four self-contained server binaries + packs the VSIX + (on tag) publishes to both marketplaces. Runs alongside the existing `dotnet test` workflow; does not replace it.

</code_context>

<specifics>
## Specific Ideas

- `flow-editor`'s FlowSyntaxHighlighter assigns: purple → keywords, teal → music keywords (tempo/key/timesig), blue → type keywords, green → strings, peach → numbers, yellow → notes/chords, red → operators, gray → comments, peach → booleans, yellow → `|` delimiters. Use these categories as the TextMate scope targets (e.g., music keywords → `keyword.other.music.flow` wait — per D-05, no invented scopes → use `keyword.control.flow` for both general and music keywords and rely on the user's theme; if discrimination matters, prefer semantic tokens over scope invention).
- Note-stream completion should explicitly exclude proc/variable names — inside `| ... |`, nothing outside notes, chords, durations, rests, tie/dot modifiers, cent-offset (`+NNc`), random-choice brackets (`(? ... )` / `(?? ... )`), and roman numerals is valid.
- `use "@name"` completion should read the actual `flow-lang/` directory at server startup so new stdlib modules appear without code changes.
- Snippet templates should match existing idiomatic `.flow` style — lowercase keywords, space before `{`, body on following lines.

</specifics>

<deferred>
## Deferred Ideas

- **Find-references** (`textDocument/references`) — requires a cross-file symbol index; natural follow-up phase once v1 ships.
- **Rename refactoring** (`textDocument/rename`) — same index dependency as find-references; pairs with it.
- **Code actions / quick-fixes** (e.g., "wrap in `key C { ... }`", "import `@audio`") — valuable but needs diagnostic-to-fix mapping logic that doesn't exist yet.
- **Unused-import / shadowed-name warnings** — would require new analysis passes beyond what `ErrorReporter` produces today.
- **Documentation auto-pull from `wiki/`** — wiki prose isn't structured for this. Could be revisited if a future phase adds frontmatter or structured docs.
- **First-class Neovim / Helix / Emacs extensions published to package managers** — generic LSP + documented setup snippets cover this for v1; first-class packages become a follow-up if uptake warrants.
- **Marketplace pre-release channel / telemetry** — v1 ships stable-only; pre-release and usage telemetry are post-v1 polish.
- **`flow-interpreter --lsp` convenience alias** — at most a ~10-line delegation; Claude's discretion whether to include in v1.
- **Incremental parser** — only if profiling shows the debounced re-parse is too slow on very large `.flow` files. Not expected for typical workloads.

### Reviewed Todos (not folded)

None — no pending todos matched Phase 17 scope.

</deferred>

---

*Phase: 17-flow-language-server*
*Context gathered: 2026-04-20*
