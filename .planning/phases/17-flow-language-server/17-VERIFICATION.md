---
phase: 17-flow-language-server
verified: 2026-04-20T22:00:00Z
status: passed
score: 15/15 must-haves verified
overrides_applied: 0
re_verification:
  previous_status: none
  previous_score: n/a
  gaps_closed: []
  gaps_remaining: []
  regressions: []
persistent_uat:
  - source: .planning/phases/17-flow-language-server/17-HUMAN-UAT.md
    rows_pending: 3
    rows_deferred_to_release: 2
    notes: >
      3 rows (1-3) tracked as ongoing HUMAN-UAT per CONTEXT.md §deferred pattern
      — do not block phase closure. Rows 4-5 (D-14 non-dev OS binary, D-15
      marketplace publish) explicitly deferred to first release tag milestone;
      cannot execute without VSIX artifacts existing.
---

# Phase 17: Flow Language Server Verification Report

**Phase Goal:** Flow users editing `.flow` files in VSCode get syntax highlighting, live diagnostics from the interpreter's parser/type-checker, and intelligent completions/hover suggestions for built-in functions, musical types, chord symbols, and imported stdlib modules — delivered as an LSP server (reusing flow-lang) and a VSCode extension that ships the server binary.

**Verified:** 2026-04-20T22:00:00Z
**Status:** passed (with persistent HUMAN-UAT items; see notes)
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths (ROADMAP §Phase 17 Success Criteria + CONTEXT.md D-01..D-15)

| # | Truth (from ROADMAP Success Criteria + CONTEXT decisions) | Status | Evidence |
|---|---|---|---|
| 1 | `flow-lsp/` builds under net10.0, references only flow-lang (no audio), boots OmniSharp over stdio (Wave 0 gate — D-01, D-02) | VERIFIED | `flow-lsp/flow-lsp.csproj` targets `net10.0`; `ProjectReference` is `flow-lang` only; grep of `flow-lsp/*.cs Handlers/ Symbols/ Semantic/ NoteStream/` for `AudioPlaybackManager\|PulseAudio\|FlowEngine\|flow-interpreter\|flow-midi` returns empty; `Program.cs:16` calls `LanguageServer.From(options => options.WithInput(Console.OpenStandardInput())...)` with `await server.WaitForExit` at line 62. `OmniSharpBootTest` Fact passes (reflection-scoped per 17-01 SUMMARY) confirming OmniSharp 0.19.9 type binds under net10 |
| 2 | Every `ErrorReporter` error surfaces as an LSP Diagnostic with correct severity + 0-based range; empty diagnostic arrays still publish (D-06) | VERIFIED | `flow-lsp/Handlers/DiagnosticsPublisher.cs` forwards `FlowError[]` via `Publish(uri, errors)`; `LspMappings.ToRange(loc)` produces 0-based `Range` with `Math.Max(0, loc.Line - 1)` underflow guard; `LspMappings.ToSeverity(DiagnosticLevel)` maps Error/Warning/Info. `TextDocumentSyncHandler.Handle(DidCloseTextDocumentParams)` calls `_diagnostics.Publish(uri, Array.Empty<FlowError>())` (line 67) to clear stale squiggles. 4 DiagnosticsHandlerTests Facts pin the mapping |
| 3 | Semantic tokens emit valid 5-tuple delta-encoded LSP output mapping every TokenType, standard VSCode scopes only (D-04, D-05) | VERIFIED | `flow-lsp/Semantic/SemanticTokensEncoder.cs` Legend uses 9 stock `SemanticTokenType` properties (Keyword/Type/String/Number/Operator/Comment/Variable/Function/Macro); `EncodeTokens` emits 5-tuple deltas with skip-preserve-origin documented and Fact-pinned (SemanticTokensTests — 15 Facts incl. 3 golden Theory rows). No `flow.*` or `SemanticTokenType.(flow|music)` scopes. `vscode-extension/syntaxes/flow.tmLanguage.json` uses only standard scopes (`keyword.control.flow`, `storage.type.flow`, `string.quoted.double.flow`, `entity.name.function.flow`, `variable.other.note.flow`, `constant.numeric.flow`, `keyword.operator.flow`, `punctuation.section.flow`, `constant.language.flow`, `comment.line.double-slash.flow`) — grep confirms zero `*.music.*` scopes |
| 4 | Completion delivers built-ins + stdlib + users + keywords + 5 snippets in default context; `use "@"` returns only 6 stdlib paths; note-stream returns roman numerals or notes/durations; never proc names inside streams (D-07, D-11) | VERIFIED | `CompletionHandler.BuildItems` (line 93-126) gates note-stream (D-11) first, then use-string literal, then 5-source merge. 4 symbol indices (`BuiltInIndex`, `StdlibSymbolIndex`, `UserSymbolIndex`, `KeywordIndex`) wired in `Program.cs`. `BuiltInFunctions.RegisterSignaturesOnly` covers every Register\* path (core + audio + transforms + harmony + playback + visualization + vocalization + MIDI + context-dependent + iteration guard — 166 Register call sites via StubbingRegistryProxy). `IsInsideUseStringLiteral` uses word-boundary `FindLastStandaloneUse` (WR-04 fix). 15 CompletionHandlerTests Facts pin behavior |
| 5 | Hover shows signature + BuiltInDocs summary for built-ins; user symbol kind for locals; stdlib-proc signature for imports (D-08, D-12) | VERIFIED | `HoverHandler.BuildHover` (line 46-88) implements 3-way lookup: BuiltInIndex → signature + `BuiltInDocs.TryGet(name).Summary` (fallback `*(no documentation)*`); UserSymbolIndex → symbol kind/name markdown; StdlibSymbolIndex → module-qualified signature. `BuiltInDocs.cs` ships 104 entries (D-12; requested ≥40). 6 HoverHandlerTests Facts pin behavior |
| 6 | Go-to-definition jumps to user procs/vars + stdlib .flow for imports; built-ins return null (D-09) | VERIFIED | `DefinitionHandler.FindUserDeclaration` walks AST for ProcDeclaration/VariableDeclaration/SectionDeclaration recursing into proc/section/musical-context bodies. Stdlib jump is gated on (a) cursor inside `"@..."` span and (b) word-boundary `use` keyword before `"` (WR-01 fix via `HasUseKeywordBefore`). Built-ins fall through to null return. 12 DefinitionHandlerTests Facts pin behavior including WR-01 regression |
| 7 | Signature help reports correct active parameter by comma count for built-ins + user procs (D-10) | VERIFIED | `SignatureHelpHandler.DetectCall` (line 40-70) parses backward from cursor with paren-depth tracking (nested parens correctly skipped), produces CallContext(FunctionName, ActiveParameter). TriggerCharacters `(` and `,`. 4 SignatureHelpHandlerTests Facts (no args, one comma, no parens, nested depth). Resolves signature via BuiltInIndex |
| 8 | Per-platform self-contained VSIXs for 4 platforms (linux-x64/win32-x64/darwin-x64/darwin-arm64); each VSIX contains flow-lsp + 6 stdlib .flow files (Pitfall 6 gate) (D-14) | VERIFIED | `.github/workflows/publish-extension.yml` has a 4-row matrix with correct `{rid, target, runner, exe}` tuples — linux-x64→linux-x64/ubuntu-latest/flow-lsp, win-x64→win32-x64/windows-latest/flow-lsp.exe (Pitfall 7), osx-x64→darwin-x64/macos-13, osx-arm64→darwin-arm64/macos-14. Publish step has 6 explicit `cp flow-lang/*.flow` lines and 6 `test -f .../server/*.flow` verify lines (Pitfall 6 gate). `-p:_IsPublishing=true` activates the conditional PropertyGroup in flow-lsp.csproj. No `PublishTrimmed` anywhere (Pitfall 4 gate). Deferred to first release tag for real-world non-dev OS install smoke |
| 9 | Dual-marketplace publish (VSCode Marketplace + OpenVSX) via tag push; OpenVSX namespace claimed before first publish (Pitfall 8) (D-15) | VERIFIED (setup) | Publish job gated on `startsWith(github.ref, 'refs/tags/v')`, fan-out matrix across 4 vsce targets, fail-fast: false. `HaaLeo/publish-vscode-extension@v2` uploads to both marketplaces via `secrets.VSCE_PAT` + `secrets.OVSX_PAT`. 17-MARKETPLACE-SETUP.md runbook covers publisher creation, PAT generation, `npx ovsx create-namespace` (Pitfall 8), workflow_dispatch dry-run, tag-push procedure, 17-row status checklist. **Execution deferred to first release tag** — cannot test without VSIX artifacts existing; tracked in 17-HUMAN-UAT.md §"Note on deferred items" |
| 10 | Non-VSCode editor users have nvim-lspconfig.lua + helix-languages.toml starter snippets + README with build-from-source (D-13 second clause) | VERIFIED | `docs/editor-setup/` contains README.md, nvim-lspconfig.lua (grep: `filetypes = { 'flow' }`), helix-languages.toml (grep: `command = "flow-lsp"`, `comment-token = "//"` matching language-configuration.json), neovim.md/helix.md/generic-lsp.md guides, manual-smoke.md checklist. README mentions `dotnet publish` from source + stdlib .flow copy reminder (Pitfall 6) |
| 11 | ParseSession reuses SimpleLexer + Parser + ErrorReporter — no FlowEngine/audio construction (D-01 reuse, Pitfall 3) | VERIFIED | `flow-lsp/ParseSession.cs` allocates `new ErrorReporter()` + `new SimpleLexer(source, er, path)` + `new Parser(tokens, er)`, returns `ParseResult(Ast, Tokens, Errors)`. No FlowEngine, AudioPlaybackManager, Interpreter, or ModuleLoader construction. ProjectReference is flow-lang only |
| 12 | DocumentManager has debounce + cancel + HasDocument(uri) + close-race guard wired into Program.cs onParse callback (D-03) | VERIFIED | `DocumentManager.cs:23` debounces 150ms via `TimeSpan.FromMilliseconds(150)`; `Update(uri, text)` cancels prior CTS inside lock then schedules outside lock; `HasDocument(uri)` returns tracked state; `Close(uri)` cancels + removes. `Program.cs:46` wraps `users.Update + diag.Publish` in `if (dm!.HasDocument(uri))` guard. 7 DocumentManagerTests Facts including `ParseCompletingConcurrentlyWithClose_DoesNotPublishWhenGuarded` discriminating regression Fact |
| 13 | TextDocumentSyncHandler handles didOpen/didChange/didClose; close path publishes empty diagnostics + calls UserSymbolIndex.Remove (WR-02 fix) | VERIFIED | `TextDocumentSyncHandler.cs` override methods handle all 4 RPCs; `DidCloseTextDocumentParams` handler calls `_docs.Close(uri)` + `_diagnostics.Publish(uri, Array.Empty<FlowError>())` + `_users.Remove(uri)` (WR-02 fix at line 70). Registration uses `TextDocumentSelector.ForLanguage("flow")` and `TextDocumentSyncKind.Full` (D-03 no-incremental). 4 TextDocumentSyncHandlerTests Facts |
| 14 | NoteStreamContext uses cached ParseResult.Tokens + brace-depth scan — no re-lex, no `.Lexeme`, handles closed key blocks correctly (D-11 enforcement) | VERIFIED | `flow-lsp/NoteStream/NoteStreamContext.cs:43` signature `FindEnclosingKey(FlowProgram, IReadOnlyList<Token>, string, Position)`. Uses `Token.Text` not `.Lexeme`. Walks brace-depth backward (lines 65-107) correctly tracking RBrace→depth++ / LBrace→depth-- with key-block check on `Identifier` + `Key` tokens before `{`. No `new SimpleLexer(...)` in the file. `StreamContainsOffset` has WR-05 fix with EOF fallback for unclosed streams. 9 NoteStreamContextTests Facts including `CursorAfterClosedKeyBlock_FindEnclosingKey_ReturnsNull` regression + `CursorAfterClosedKey_InSiblingStream` |
| 15 | VSCode `proc` snippet uses `end proc` form (CR-01 fix); contract test pins it; comment syntax `//` only matching SimpleLexer | VERIFIED | `vscode-extension/snippets/flow.code-snippets:19` proc body is `["proc ${1:name} (${2})", "\t$0", "end proc"]` — CR-01 fix confirmed. `VscodeSnippetsContractTests.ProcSnippet_UsesEndProcTerminator` Fact pins it (asserts `Contains "end proc"` + `DoesNotContain "() {"`). `language-configuration.json` has `"lineComment": "//"` only — no `;` fallback. `flow.tmLanguage.json` comments repository has single `//.*$` pattern |

**Score:** 15/15 truths verified (ROADMAP 10 SCs + CONTEXT.md D-01..D-15 rolled into the 15 truths above with overlap collapsed)

### Deferred Items

Two manual-smoke rows from 17-VALIDATION.md are explicitly deferred to the **first release tag milestone** (not to Phase 17 closure). Both are tracked in the "Note on deferred items" section of 17-HUMAN-UAT.md:

| # | Item | Addressed In | Evidence |
|---|------|-------------|----------|
| 1 | D-14 — Install per-platform VSIX on non-Linux OS and repeat rows 1-3 of manual-smoke.md | First release tag (`v*`) | 17-HUMAN-UAT.md §"Note on deferred items" explicitly scopes to release; cannot execute today because no VSIX has been published yet — circular dependency pattern documented |
| 2 | D-15 — Marketplace + OpenVSX publish succeeds on tag push (CI `publish` job green, listings appear on both marketplaces) | First release tag (`v*`) | 17-MARKETPLACE-SETUP.md Step 4 tracks this; depends on one-time PAT/namespace setup which is also a Noah-only action |

**These deferred items do not block phase closure** under the deferral pattern documented in CONTEXT.md §deferred and 17-08 SUMMARY.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `flow-lsp/flow-lsp.csproj` | net10.0, OmniSharp 0.19.9, flow-lang-only deps, no PublishTrimmed | VERIFIED | `<TargetFramework>net10.0</TargetFramework>`, `PackageReference Include="OmniSharp.Extensions.LanguageServer" Version="0.19.9"`, `ProjectReference Include="..\flow-lang\flow-lang.csproj"`. `_IsPublishing`-gated PropertyGroup for self-contained single-file publish. No PublishTrimmed |
| `flow-lsp/ParseSession.cs` | SimpleLexer + Parser + ErrorReporter wrapper, no FlowEngine | VERIFIED | 31 lines. `new ErrorReporter() + new SimpleLexer + new Parser`; returns `ParseResult(Ast, Tokens, Errors)` |
| `flow-lsp/Program.cs` | LanguageServer.From + DI wiring + 6 handlers + close-race-guarded onParse | VERIFIED | `LanguageServer.From(options => options.WithInput(...)...)`; DI graph: ParseSession + Diagnostics(×2) + InternalFunctionRegistry (via RegisterSignaturesOnly) + BuiltInIndex + StdlibSymbolIndex + KeywordIndex + UserSymbolIndex + DocumentManager factory with `HasDocument`-guarded onParse calling `users.Update` + `diag.Publish`. 6 `.WithHandler<>()` registrations |
| `flow-lsp/DocumentManager.cs` | Debounce + cancel + HasDocument + Close | VERIFIED | 86 lines. 150ms debounce, per-URI CTS cancel, thread-safe Dictionary access via `_lock`, schedules outside lock |
| `flow-lsp/LspMappings.cs` | 1-based→0-based Range + severity mapping + underflow guard | VERIFIED | Uses `using Range = OmniSharp...Range` alias. `Math.Max(0, loc.Line - 1)` + `Math.Max(0, loc.Column - 1)` underflow guards for `SourceLocation.Unknown (0,0)` |
| `flow-lsp/Handlers/TextDocumentSyncHandler.cs` | 4 RPC overrides + close publishes empty + UserSymbolIndex.Remove | VERIFIED | WR-02 fix at line 70 calls `_users.Remove(uri)`. Registration uses `TextDocumentSyncKind.Full` |
| `flow-lsp/Handlers/DiagnosticsPublisher.cs` | IDiagnosticsPublisher seam + Build + Publish | VERIFIED | Static `BuildDiagnostics(FlowError[]) -> Diagnostic[]` separated for Fact suite; transport-bound `Publish(uri, errors)` always fires (empty is how LSP clears squiggles) |
| `flow-lsp/Semantic/SemanticTokensEncoder.cs` | 9-entry standard Legend + MapTokenType + 5-tuple EncodeTokens | VERIFIED | Pure helper with zero transport deps. 9 standard `SemanticTokenType` properties only. `EncodeTokens` preserves delta origin across skipped tokens |
| `flow-lsp/Handlers/SemanticTokensHandler.cs` | OmniSharp SemanticTokensHandlerBase subclass | VERIFIED | Registers Full=new SemanticTokensCapabilityRequestFull{ Delta = false }, Range=false. Pushes mapped tokens into builder |
| `flow-lsp/Handlers/CompletionHandler.cs` | 5-source merge + use-string gate + note-stream gate + word-boundary `use` check | VERIFIED | BuildItems 9-arg signature (adds ast + tokens for D-11). WR-04 fix via `FindLastStandaloneUse` rejects `misuse`/`abuser`/`houses` |
| `flow-lsp/Handlers/HoverHandler.cs` | 3-way lookup + markdown content | VERIFIED | BuiltIn → signature+doc; User → kind+name; Stdlib → module-qualified |
| `flow-lsp/Handlers/SignatureHelpHandler.cs` | active-parameter via comma count + nested paren depth | VERIFIED | Trigger chars `(` and `,`. Backward-scan paren-depth tracking |
| `flow-lsp/Handlers/DefinitionHandler.cs` | User AST walk + stdlib jump + WR-01 cursor+use-keyword gate | VERIFIED | `HasUseKeywordBefore` word-boundary helper public for WR-01 regression Fact. Built-ins fall through to null per D-09 |
| `flow-lsp/NoteStream/NoteStreamContext.cs` | Token-scan + brace-depth + closed-key regression-safe + WR-05 EOF fallback | VERIFIED | IReadOnlyList<Token> parameter (no re-lex); `Token.Text` not `.Lexeme`; EOF fallback when no closing pipe |
| `flow-lsp/Symbols/BuiltInIndex.cs` | Wraps EnumerateSignatures from registry | VERIFIED | `BuiltInIndex.Entry(Name, Signatures)`. SortText `1_{name}`. Items include Detail from signature.ToString + Documentation from BuiltInDocs |
| `flow-lsp/Symbols/StdlibSymbolIndex.cs` | Startup-parses 6 stdlib modules | VERIFIED | `UseStringPathItems()` returns 6 items. Parses via ParseSession + `ModuleLoader.ResolveStdlibPath` |
| `flow-lsp/Symbols/UserSymbolIndex.cs` | Per-URI AST walker + Update/Remove/For/Find | VERIFIED | Proc/Variable/Section walker, recurses into proc/section/musical-context bodies |
| `flow-lsp/Symbols/KeywordIndex.cs` | 20 keywords + 30 types | VERIFIED | Static items with SortText prefixes `4_`/`5_` |
| `flow-lang/StandardLibrary/BuiltInDocs.cs` | Static Doc TryGet lookup; ≥40 entries | VERIFIED | 104 entries (2.6× target). `public static Doc? TryGet(string name)` |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` (RegisterSignaturesOnly) | Every built-in registered with stub delegate | VERIFIED | 166 Register call sites forwarded via `StubbingRegistryProxy`. Stub throws NotSupportedException on invocation. Covers audio/transforms/harmony/playback/visualization/vocalization/MIDI + context-dependent |
| `vscode-extension/package.json` | Valid npm manifest + onLanguage:flow + contributes | VERIFIED | valid JSON; `scopeName: source.flow`; `onLanguage:flow` activation; 2 config props (flow.server.path, flow.trace.server); 2 test:grammar scripts |
| `vscode-extension/syntaxes/flow.tmLanguage.json` | Standard scopes only + `//` comment pattern + pattern ordering | VERIFIED | 10 repository entries using standard `keyword.control/storage.type/…` scopes. Single `//.*$` comment pattern. chords BEFORE notes BEFORE numbers so `Bb7`/`Cmaj7` tokenize correctly |
| `vscode-extension/src/extension.ts` | LanguageClient + platformDir() + chmodSync | VERIFIED | `platformDir()` returns `${process.platform}-${process.arch}` producing vsce-target-compatible names. chmod 0o755 on POSIX |
| `vscode-extension/language-configuration.json` | `//` only line comment | VERIFIED | `"lineComment": "//"` only — no `;` fallback |
| `vscode-extension/.vscodeignore` | Does NOT exclude server/** or *.flow | VERIFIED | grep -E "^server/" returns empty. Does not exclude `tests/` recursively either |
| `vscode-extension/snippets/flow.code-snippets` | `proc` uses `end proc` (CR-01 fix) | VERIFIED | proc body contains `"end proc"`; other snippets use `{ ... }` as appropriate |
| `vscode-extension/tests/grammar/*.flow.snap` | 4 committed snapshot baselines | VERIFIED | sample.flow.snap (3722B), note-stream.flow.snap (3283B), chords.flow.snap (4008B), musical-context.flow.snap (2210B) |
| `scripts/lsp-smoke.sh` | Executable, syntactically valid, timeout + cleanup | VERIFIED | mode 100755. `bash -n` syntax check passes. Python heredoc handles Content-Length framing. Timeout via `LSP_SMOKE_TIMEOUT_SEC` (default 15s) |
| `.github/workflows/publish-extension.yml` | 4-platform matrix + PATs via secrets + stdlib copy + no PublishTrimmed | VERIFIED | 4 matrix rows with correct RID↔target mapping (Pitfall 7). 6 `cp` + 6 `test -f` lines (Pitfall 6). `secrets.VSCE_PAT` + `secrets.OVSX_PAT`. No `PublishTrimmed` anywhere |
| `docs/editor-setup/*.{md,lua,toml}` | nvim-lspconfig.lua + helix-languages.toml + README + neovim.md + helix.md + generic-lsp.md + manual-smoke.md | VERIFIED | All 7 files present. Helix comment-token = "//" matching VSCode |
| `.planning/phases/17-flow-language-server/17-MARKETPLACE-SETUP.md` | Runbook with VSCE_PAT + OVSX_PAT + ovsx create-namespace | VERIFIED | All 4 required terms present (VSCE_PAT, OVSX_PAT, create-namespace, Marketplace.Manage). 17-row status checklist |
| `README.md` (top-level) | Announces VSCode extension | VERIFIED | Contains "VSCode extension" + "docs/editor-setup/" pointer |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| flow-sharp.sln | flow-lsp/flow-lsp.csproj | Project entry | VERIFIED | grep -q "flow-lsp" flow-sharp.sln passes |
| flow-lsp.csproj | flow-lang.csproj | ProjectReference | VERIFIED | `ProjectReference Include="..\flow-lang\flow-lang.csproj"` |
| Program.cs | TextDocumentSyncHandler + SemanticTokensHandler + CompletionHandler + HoverHandler + SignatureHelpHandler + DefinitionHandler | `.WithHandler<T>()` | VERIFIED | 6 WithHandler<> registrations, one per handler |
| Program.cs onParse callback | DocumentManager.HasDocument guard | `if (dm!.HasDocument(uri))` | VERIFIED | Close-race guard wraps both `users.Update` and `diag.Publish` |
| CompletionHandler | BuiltInIndex + StdlibSymbolIndex + UserSymbolIndex + KeywordIndex + ParseSession | ctor DI | VERIFIED | 6-arg ctor injects all 5 indices + ParseSession for per-request re-parse |
| HoverHandler | BuiltInIndex + UserSymbolIndex + StdlibSymbolIndex + BuiltInDocs | ctor + TryGet | VERIFIED | 4-arg ctor; BuildHover reads BuiltInDocs.TryGet |
| DefinitionHandler | ModuleLoader.ResolveStdlibPath | static call | VERIFIED | Resolved path + File.Exists gate |
| TextDocumentSyncHandler | DocumentManager + IDiagnosticsPublisher + UserSymbolIndex | ctor DI | VERIFIED | 3-arg ctor; DidClose calls all three (Close/Publish/Remove) |
| CompletionHandler.BuildItems | NoteStreamContext.IsInsideNoteStream + FindEnclosingKey | static call | VERIFIED | Line 107-116 gates note-stream branch before use-string |
| BuiltInIndex | InternalFunctionRegistry.EnumerateSignatures | enumeration | VERIFIED | Built from registry populated via RegisterSignaturesOnly |
| vscode-extension/package.json | flow.tmLanguage.json + flow.code-snippets + language-configuration.json | contributes.grammars/snippets/languages | VERIFIED | all three paths present |
| extension.ts | server/<platform>/flow-lsp[.exe] | LanguageClient.ServerOptions.command + platformDir() + asAbsolutePath | VERIFIED | Spawn via Executable with TransportKind.stdio |
| CI workflow | scripts/lsp-smoke.sh | `bash scripts/lsp-smoke.sh "..."` | VERIFIED | Invoked from the matrix smoke step |
| CI workflow | VSCE_PAT + OVSX_PAT | `${{ secrets.* }}` | VERIFIED | Both secrets referenced in HaaLeo/publish-vscode-extension@v2 uses |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|---------------------|--------|
| DiagnosticsPublisher.Publish | FlowError[] | ErrorReporter.Errors populated during Parser.Parse | Yes — SimpleLexer/Parser emit real soft-failure errors via ErrorReporter | FLOWING |
| BuiltInIndex.Items() | Entries from EnumerateSignatures | InternalFunctionRegistry populated via RegisterSignaturesOnly's StubbingRegistryProxy visiting every Register* method | Yes — 166 call sites → non-empty entries | FLOWING |
| StdlibSymbolIndex.Items() | Top-level procs | ParseSession.Parse called on each of 6 stdlib .flow files at startup | Yes — files exist beside flow-lang.dll via `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` | FLOWING |
| UserSymbolIndex.For(uri) | Symbols from AST walker | Update(uri, ast) called by onParse callback after ParseSession.Parse | Yes — real AST walk over ProcDeclaration/VariableDeclaration/SectionDeclaration | FLOWING |
| SemanticTokensHandler Tokenize | Token[] | ParseSession.Parse tokens | Yes — SimpleLexer emits tokens per keystroke | FLOWING |
| HoverHandler.BuildHover | Hover MarkupContent | BuiltInDocs.TryGet / UserSymbolIndex.Find / StdlibSymbolIndex.Find | Yes — real entries resolved | FLOWING |
| DefinitionHandler.Handle | Location | FindUserDeclaration AST walk OR ModuleLoader.ResolveStdlibPath | Yes — real AST + stdlib path resolution | FLOWING |
| CompletionHandler.Handle | CompletionItem[] | Per-request ParseSession.Parse + 5-source merge | Yes — merges real indices | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build succeeds | `dotnet build flow-sharp.sln -c Debug` | 0 errors, 13 warnings | PASS |
| Phase17 tests green | `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17" --no-build` | 117 passed, 0 failed | PASS |
| Full suite green | `dotnet test flow-sharp.sln --no-build` | 257 passed, 0 failed | PASS |
| Smoke script syntax valid | `bash -n scripts/lsp-smoke.sh` | exit 0 | PASS |
| TM grammar JSON valid | `python3 -c "import json; json.load(open('vscode-extension/syntaxes/flow.tmLanguage.json'))"` | exit 0 | PASS |
| Snippets contract pins end proc | Phase17 VscodeSnippetsContractTests.ProcSnippet_UsesEndProcTerminator | PASS (within the 117 total) | PASS |
| Close-race discriminator holds | Phase17 DocumentManagerTests.ParseCompletingConcurrentlyWithClose_DoesNotPublishWhenGuarded | PASS (within the 117) | PASS |
| Note-stream block-exit regression | Phase17 NoteStreamContextTests.CursorAfterClosedKeyBlock_FindEnclosingKey_ReturnsNull | PASS (within the 117) | PASS |
| No audio refs in flow-lsp source | grep -rE "AudioPlaybackManager\|PulseAudio\|FlowEngine\|flow-interpreter\|flow-midi" flow-lsp/ --include="*.cs" | empty | PASS |
| No PublishTrimmed in csproj or CI | grep -q "PublishTrimmed" flow-lsp/flow-lsp.csproj .github/workflows/publish-extension.yml | empty | PASS |

### Requirements Coverage

Phase 17 is mapped to D-01..D-15 (CONTEXT.md locked decisions) rather than REQ-IDs in REQUIREMENTS.md (per RESEARCH §"Phase Requirements" and ROADMAP Phase 17 `Requirements` field). Every decision is claimed by at least one plan:

| Decision | Source Plan(s) | Description | Status | Evidence |
|----------|----------------|-------------|--------|----------|
| D-01 | 17-01 | LSP in C# referencing flow-lang via OmniSharp; reuse SimpleLexer/Parser/ErrorReporter/Registry/TypeSystem | SATISFIED | flow-lsp/flow-lsp.csproj + ParseSession.cs; OmniSharp 0.19.9 pinned |
| D-02 | 17-01 | New sibling flow-lsp/ csproj, audio-free | SATISFIED | flow-lsp.csproj references flow-lang only; grep for audio symbols empty |
| D-03 | 17-03 | didChange → 150ms debounce → full re-lex+re-parse; no incremental | SATISFIED | DocumentManager.cs 150ms debounce; TextDocumentSyncKind.Full |
| D-04 | 17-02, 17-04 | Hybrid TextMate grammar + LSP semantic tokens | SATISFIED | TM grammar + SemanticTokensEncoder using standard LSP types |
| D-05 | 17-02, 17-04 | Standard VSCode scopes only | SATISFIED | 10 standard scopes in TM grammar; 9 standard SemanticTokenType properties; negative greps pass |
| D-06 | 17-03 | Forward all ErrorReporter output to publishDiagnostics | SATISFIED | DiagnosticsPublisher.cs Publish + BuildDiagnostics |
| D-07 | 17-05 | Complete over builtins + stdlib + keywords + user symbols + snippets | SATISFIED | RegisterSignaturesOnly (166 sites) + 4 indices + 5 snippets |
| D-08 | 17-06 | Hover = signature + doc summary | SATISFIED | HoverHandler.BuildHover 3-way lookup |
| D-09 | 17-06 | Go-to-def for user symbols + stdlib imports; built-ins return null | SATISFIED | DefinitionHandler.FindUserDeclaration + stdlib branch + built-ins fall through |
| D-10 | 17-06 | Signature help with active-parameter | SATISFIED | SignatureHelpHandler.DetectCall comma-count with nested paren depth |
| D-11 | 17-06 | Note-stream context-aware completion (roman numerals in key block, notes outside) | SATISFIED | NoteStreamContext + CompletionHandler gated branch |
| D-12 | 17-01, 17-05 | BuiltInDocs lookup table for hover | SATISFIED | 104 entries shipped (≥40 required) |
| D-13 | 17-02, 17-08 | VSCode-first + LSP-generic with docs/editor-setup/ snippets | SATISFIED | vscode-extension/ + docs/editor-setup/ (7 files) |
| D-14 | 17-07 | Per-platform self-contained VSIXs for 4 platforms | SATISFIED (setup) | CI matrix present; deferred to first release tag for real-world install (see HUMAN-UAT §deferred) |
| D-15 | 17-07, 17-08 | Publish to VSCode Marketplace + OpenVSX via tag push | SATISFIED (setup) | CI publish job present; runbook shipped; deferred to first release tag for actual execution (see HUMAN-UAT §deferred) |

**All 15 decisions satisfied at the setup/artifact level.** D-14 and D-15 require Noah-only one-time actions (PAT generation, namespace claim) + first tag push for real-world execution; both are documented in 17-MARKETPLACE-SETUP.md runbook and explicitly deferred under the CONTEXT.md §deferred pattern.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none) | — | — | — | — |

Notes:
- `NoteStreamContext.IsTriviaToken` returns hardcoded `false` with documented rationale (SimpleLexer does not emit whitespace tokens) — flagged IN-07 in REVIEW, out of scope, not a real anti-pattern.
- `StdlibSymbolIndex` first-write-wins for cross-module proc shadowing — flagged IN-03 in REVIEW, cosmetic only, not a stub.
- xUnit analyzer warnings (VSTHRD200, xUnit1051) carried forward from Phase 12-14 — not Phase 17 introductions.
- Stale `Folder Include="bin\Debug\net9.0\"` in flow-lang.csproj — dead entry, not a target framework.

### Human Verification Required

**Persistent HUMAN-UAT tests (documented in 17-HUMAN-UAT.md), tracked asynchronously under CONTEXT §deferred pattern — do NOT block phase closure:**

1. **HUMAN-UAT Test 1 — D-04/D-05 syntax highlighting matches flow-editor categories (11-item tick list).**
   Test: Open `tests/test_chords.flow` in Extension Dev Host (F5 from `vscode-extension/`). Verify each of 11 category tick-boxes renders consistently (keywords, types, strings, numbers, comments, notes, chords, roman numerals, note-stream delimiters, operators, booleans).
   Expected: Chords visually distinct from notes; no unstyled regions.
   Why human: Theme-dependent visual output.

2. **HUMAN-UAT Test 2 — D-04 TM→semantic token transition window (0–300ms).**
   Test: Close and re-open a `.flow` file in Extension Dev Host. Watch first 0–300ms.
   Expected: Repaint is "not noticeable" or "subtle / acceptable" — not jarring.
   Why human: Visual transition quality.

3. **HUMAN-UAT Test 3 — D-13 extension activation + embedded feature sanity (D-06/D-07/D-08/D-09/D-10/D-11 spot-checks).**
   Test: Extension activates on `.flow` file open; status indicator present; then run 7 embedded checks (diagnostics squiggle, `pri` → `print` completion with signature, `use "@` → exactly 6 stdlib paths, hover markdown, Ctrl+Click go-to-def, signature help active param, roman numeral completion in key block, snippet expansion).
   Expected: Each embedded feature behaves per D-06..D-11 spec.
   Why human: Requires VSCode Extension Development Host + interactive UX testing.

**All 3 tests tracked in `.planning/phases/17-flow-language-server/17-HUMAN-UAT.md` with `result: [pending]`** — NOT faked as pass. Per user direction ("Defer to HUMAN-UAT. Close the phase now without blocking on F5"), these resolve asynchronously via `/gsd-verify-work` sessions and do not block phase 17 closure.

**Deferred-to-release (NOT tracked in 17-HUMAN-UAT.md) — cannot execute until VSIX artifacts exist:**

4. D-14 — install per-platform VSIX on non-Linux OS, repeat rows 1-3.
5. D-15 — Marketplace + OpenVSX publish succeeds on tag push (both listings show all 4 VSIX entries).

### Gaps Summary

**No gaps found.** All 15 observable truths verified; all 32 required artifacts exist and pass their substantive checks; all key links wire correctly; data flows through every handler from source→index→response with real data; 117/117 Phase17 Facts green; 257/257 full-suite green (up from 236 pre-phase baseline — +21 net new, matching REVIEW-FIX additions). All 6 REVIEW findings in scope (1 CR + 5 WR) fixed per REVIEW-FIX.md.

The phase ships a complete LSP server + VSCode extension + CI/publish pipeline + editor-setup docs + marketplace runbook. Remaining items are:
- **3 persistent HUMAN-UAT tests** (manual smoke in Extension Dev Host) — tracked in 17-HUMAN-UAT.md, resolve async, do not block closure per CONTEXT §deferred.
- **2 release-gated items** (non-dev OS install + marketplace publish verification) — executable only after first `v*` tag push; tracked in 17-MARKETPLACE-SETUP.md Step 4 status checklist.
- **1 operational one-time human action** (Noah's publisher/PAT/namespace setup via 17-MARKETPLACE-SETUP.md Steps 1-3) — a precondition for Step 4, not a phase 17 code gap.

Phase 17 is goal-achieved at the code/artifact/CI-setup level.

---

_Verified: 2026-04-20T22:00:00Z_
_Verifier: Claude (gsd-verifier)_
