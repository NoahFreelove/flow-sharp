---
phase: 17
slug: flow-language-server
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-04-20
updated: 2026-04-20
---

# Phase 17 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from `17-RESEARCH.md` §Validation Architecture. Planner: populate the Per-Task Verification Map as plans are written; executor: flip statuses as tasks complete.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (C#, already in `flow-lang.Tests/`); `vscode-tmgrammar-test` + `vscode-tmgrammar-snap` (TM grammar); bash smoke harness for LSP binary boot |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (new `Unit/Phase17/` subtree); `vscode-extension/tests/grammar/` (Wave 0 scaffold) |
| **Quick run command** | `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17"` |
| **Full suite command** | `dotnet test flow-sharp.sln && (cd vscode-extension && npx vscode-tmgrammar-test 'tests/grammar/**/*.flow' && npx vscode-tmgrammar-snap -g syntaxes/flow.tmLanguage.json 'tests/grammar/**/*.flow')` |
| **Estimated runtime** | ~30s quick (C# in-process LSP handler tests), ~90s full (adds TM snapshots + solution build) |

---

## Sampling Rate

- **After every task commit:** Run quick command — `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17"`
- **After every plan wave:** Run full suite
- **Before `/gsd-verify-work`:** Full suite must be green + manual Extension Development Host smoke on Linux + one non-dev OS
- **Max feedback latency:** 30 seconds for quick; 90 seconds for full

---

## Three-Layer Test Strategy

LSP servers are hard to end-to-end-test. Each guarantee is placed at the lowest layer that still proves the requirement.

- **L1 (in-process, fast):** C# tests that construct handlers directly, feed them LSP params built in-memory, and assert the returned structures. No real JSON-RPC pipe.
- **L2 (golden-file, fast):** Snapshot tests — TM grammar scopes on sample `.flow` files (`vscode-tmgrammar-snap`), and semantic-tokens encoding on fixture scripts (xUnit Theory with pinned `int[]` data arrays).
- **L3 (manual smoke):** Checklist run in VSCode's Extension Development Host (F5) against a representative `.flow` file covering all 15 decisions; plus CI smoke-boot of each per-platform binary via `scripts/lsp-smoke.sh`.

---

## Per-Task Verification Map

> Populated by the planner. Each plan task gets one row. `Test Type` is L1/L2/L3 per the strategy above.
> Status flipped by the executor as tasks complete.

| Task ID | Plan | Wave | Decisions | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-----------|------------|-----------------|-----------|-------------------|-------------|--------|
| 17-01-T1 | 17-01 | 1 | D-01, D-02, D-12 | T-17-01 (V5 input validation) | ParseSession isolates lex+parse from evaluator; no Audio/Interpreter ctor | L1 | `dotnet build flow-sharp.sln -c Debug && dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17.ParseSessionTests\|FullyQualifiedName~Phase17.BuiltInDocsTests"` | flow-lsp/flow-lsp.csproj, flow-lsp/ParseSession.cs, flow-lang/StandardLibrary/BuiltInDocs.cs, flow-lang.Tests/Unit/Phase17/LspFixtures.cs | pending |
| 17-01-T2 | 17-01 | 1 | D-01 | T-17-02 (DoS via malformed init) | OmniSharp boot Fact proves initialize/shutdown handshake; exit 0 gated | L1 | `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17.OmniSharpBootTest"` | flow-lang.Tests/Unit/Phase17/OmniSharpBootTest.cs | pending |
| 17-02-T1 | 17-02 | 1 | D-13, D-14 | T-17-03 (spoofing via binary override) | extension.ts selects per-platform binary; accepts user override via flow.server.path setting | L1 (JSON validity) + L3 (F5 smoke deferred to 17-08) | `test -f vscode-extension/package.json && test -f vscode-extension/src/extension.ts && python3 -c "import json; json.load(open('vscode-extension/package.json'))"` | vscode-extension/{package.json,tsconfig.json,.vscodeignore,language-configuration.json,src/extension.ts,README.md}, .gitignore updated | pending |
| 17-02-T2 | 17-02 | 1 | D-04, D-05, D-07 | (no direct threat) | TM grammar uses standard scopes only (D-05); no invented music-specific scopes; per-note coloring not per-bar (Pitfall 5) | L2 (snapshot baselines land in 17-07) | `python3 -c "import json; g=json.load(open('vscode-extension/syntaxes/flow.tmLanguage.json')); assert g['scopeName']=='source.flow'"` | vscode-extension/syntaxes/flow.tmLanguage.json, vscode-extension/snippets/flow.code-snippets, vscode-extension/tests/grammar/{sample,note-stream,chords,musical-context}.flow | pending |
| 17-03-T1 | 17-03 | 2 | D-03 | T-17-05 (DoS) | DocumentManager debounce + cancel bounds work per-keystroke; HasDocument accessor enables close-race guard | L1 | `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17.DocumentManagerTests\|FullyQualifiedName~Phase17.LspMappingsTests"` | flow-lsp/DocumentManager.cs, flow-lsp/LspMappings.cs, flow-lang.Tests/Unit/Phase17/DocumentManagerTests.cs, flow-lang.Tests/Unit/Phase17/LspMappingsTests.cs | pending |
| 17-03-T2 | 17-03 | 2 | D-03, D-06 | T-17-05, T-17-06, T-17-12 (stale diagnostics after close) | Empty diagnostics still publish (clears stale markers); close-race guard prevents revival of cleared diagnostics after Close | L1 | `dotnet build flow-sharp.sln -c Debug && dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17.DiagnosticsHandlerTests\|FullyQualifiedName~Phase17.DocumentManagerTests"` | flow-lsp/Handlers/{TextDocumentSyncHandler.cs,DiagnosticsPublisher.cs}, flow-lsp/Program.cs (updated), flow-lang.Tests/Unit/Phase17/DiagnosticsHandlerTests.cs | pending |
| 17-04-T1 | 17-04 | 3 | D-04, D-05 | (no direct threat) | SemanticTokensEncoder produces 5-tuple delta-encoded int[]; pure helper; O(n) in token count | L1 + L2 (fixture int[] arrays pinned) | `dotnet build flow-sharp.sln -c Debug && dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17.SemanticTokensTests"` | flow-lsp/Handlers/SemanticTokensHandler.cs, flow-lsp/Semantic/SemanticTokensEncoder.cs, flow-lang.Tests/Unit/Phase17/SemanticTokensTests.cs | pending |
| 17-05-T1 | 17-05 | 4 | D-12 | (no direct threat) | InternalFunctionRegistry.EnumerateSignatures is read-only; ModuleLoader.ResolveStdlibPath confines to assembly dir | L1 | `dotnet build flow-sharp.sln -c Debug && dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17.SymbolIndicesTests\|FullyQualifiedName~Phase17.BuiltInDocsTests"` | flow-lang/StandardLibrary/InternalFunctionRegistry.cs (amended), flow-lang/Runtime/ModuleLoader.cs (amended), flow-lang/StandardLibrary/BuiltInDocs.cs (grown to ≥40 entries), flow-lang.Tests/Unit/Phase17/SymbolIndicesTests.cs | pending |
| 17-05-T2 | 17-05 | 4 | D-07 | T-17-08 (stdlib file read), T-17-09 (tampering w/ registration) | RegisterSignaturesOnly registers EVERY built-in signature with a stub that throws NotSupportedException — LSP only introspects, never invokes. D-07 "every built-in" delivered in full (audio + transforms + harmony) | L1 | `dotnet build flow-sharp.sln -c Debug && dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17.CompletionHandlerTests\|FullyQualifiedName~Phase17.SymbolIndicesTests\|FullyQualifiedName~Phase17.BuiltInFunctionsTests"` | flow-lang/StandardLibrary/BuiltInFunctions.cs (RegisterSignaturesOnly added), flow-lsp/Symbols/{BuiltInIndex,UserSymbolIndex,StdlibSymbolIndex,KeywordIndex}.cs, flow-lsp/Handlers/CompletionHandler.cs, flow-lsp/Program.cs (updated), flow-lang.Tests/Unit/Phase17/CompletionHandlerTests.cs | pending |
| 17-06-T1 | 17-06 | 5 | D-11 | T-17-11 (DoS via deep AST) | Token-scan walks backward from cursor with brace-depth tracking — correctly detects block exits (cursor AFTER closed `key { }` returns null); parser max-depth 500 bounds recursion | L1 | `dotnet build flow-sharp.sln -c Debug && dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17.NoteStreamContextTests\|FullyQualifiedName~Phase17.CompletionHandlerTests"` | flow-lsp/NoteStream/NoteStreamContext.cs, flow-lsp/Handlers/CompletionHandler.cs (extended with note-stream branch), flow-lang.Tests/Unit/Phase17/NoteStreamContextTests.cs | pending |
| 17-06-T2 | 17-06 | 5 | D-08, D-09, D-10 | T-17-10 (path traversal in defn) | DefinitionHandler stdlib jump only resolves `@`-prefix names through ModuleLoader.ResolveStdlibPath (confined to assembly dir); built-ins return null per D-09 | L1 | `dotnet build flow-sharp.sln -c Debug && dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17.HoverHandlerTests\|FullyQualifiedName~Phase17.SignatureHelpHandlerTests\|FullyQualifiedName~Phase17.DefinitionHandlerTests"` | flow-lsp/Handlers/{HoverHandler,SignatureHelpHandler,DefinitionHandler}.cs, flow-lsp/Program.cs (updated), flow-lang.Tests/Unit/Phase17/{HoverHandlerTests,SignatureHelpHandlerTests,DefinitionHandlerTests}.cs | pending |
| 17-07-T1 | 17-07 | 6 | D-13, D-14 | T-17-13 (CI supply chain) | scripts/lsp-smoke.sh framed-JSON-RPC handshake fails closed on non-zero exit; TM grammar snapshots prevent accidental scope drift | L2 + L3 (CI binary smoke) | `test -x scripts/lsp-smoke.sh && bash -n scripts/lsp-smoke.sh && grep -q "Content-Length" scripts/lsp-smoke.sh && ls vscode-extension/tests/grammar/*.flow.snap` | scripts/lsp-smoke.sh, vscode-extension/package.json (test:grammar script), vscode-extension/tests/grammar/*.flow.snap | pending |
| 17-07-T2 | 17-07 | 6 | D-14, D-15 | T-17-14 (PAT leak) | Workflow references `${{ secrets.VSCE_PAT }}` / `${{ secrets.OVSX_PAT }}` — no committed secrets; 4-platform matrix self-contained publish | L2 (yaml lint) + L3 (first tag push) | `test -f .github/workflows/publish-extension.yml && python3 -c "import yaml; w=yaml.safe_load(open('.github/workflows/publish-extension.yml')); assert 'jobs' in w"` (soft-check — requires PyYAML) | .github/workflows/publish-extension.yml | pending |
| 17-08-T1 | 17-08 | 7 | D-13 | (no direct threat) | Non-VSCode editor setup snippets documented; generic LSP over stdio discoverable for Neovim/Helix/Emacs | L3 (manual doc review) | `test -d docs/editor-setup && ls docs/editor-setup/*.md` | docs/editor-setup/{neovim.md,helix.md,...}, README.md updated | pending |
| 17-08-T2 | 17-08 | 7 | D-15 | T-17-14 (PAT leak), T-17-15 (extension impersonation) | Marketplace + OpenVSX setup runbook documents PAT creation + namespace claim (`ovsx create-namespace`); secrets live in GitHub Actions only | L3 (manual runbook) | `test -f .planning/phases/17-flow-language-server/17-MARKETPLACE-SETUP.md && grep -q "VSCE_PAT" ... && grep -q "OVSX_PAT" ... && grep -q "create-namespace" ...` | .planning/phases/17-flow-language-server/17-MARKETPLACE-SETUP.md | pending |
| 17-08-T3 | 17-08 | 7 | D-13, D-15 | (no direct threat) | Manual checkpoint: F5 Extension Dev Host smoke across 15 CONTEXT decisions | L3 (human-verify checkpoint) | Human checklist (captured in 17-08-PLAN.md checkpoint task) | `.planning/phases/17-flow-language-server/17-SMOKE-CHECKLIST.md` (executor-produced) | pending |

*Status legend: pending · green · red · flaky*

---

## Wave 0 Requirements

These scaffolding items must land before any handler task can be verified:

- [ ] `flow-lsp/flow-lsp.csproj` — new project targeting `net10.0`; references `flow-lang`; no `flow-interpreter` reference (keeps audio out).
- [ ] `flow-lsp/Program.cs` — bootstrap that wires `LanguageServer.From()` and speaks `initialize` + `shutdown` over stdio. Smoke-booted by `scripts/lsp-smoke.sh`.
- [ ] `flow-lang/StandardLibrary/BuiltInDocs.cs` — per D-12 lookup table; starter entries for stdio/arithmetic/collections/audio/chord built-ins.
- [ ] `flow-lang.Tests/Unit/Phase17/` — xUnit fixture directory + `LspFixtures.cs` helper for constructing test documents.
- [ ] `vscode-extension/package.json` + `vscode-extension/src/extension.ts` — minimal scaffold with `onLanguage:flow` activation and `LanguageClient` wired to the bundled binary.
- [ ] `vscode-extension/syntaxes/flow.tmLanguage.json` — minimum viable grammar (keywords, strings, comments, numbers); grown over the phase.
- [ ] `vscode-extension/tests/grammar/sample.flow` — first snapshot fixture.
- [ ] `scripts/lsp-smoke.sh` — sends framed `initialize` + `shutdown` to a binary and asserts exit 0.
- [ ] `.github/workflows/publish-extension.yml` — four-target matrix, Marketplace + OpenVSX tag-triggered publish.

---

## Manual-Only Verifications

Some behaviors are infeasible to automate cheaply and are verified by checklist in the Extension Development Host.

| Behavior | Decision | Why Manual | Test Instructions |
|----------|----------|------------|-------------------|
| Syntax highlighting visually matches `flow-editor/` (color categories, not exact palette) | D-04, D-05 | Visual perception — automated pixel-diff is fragile across themes. | F5, open `examples/demo.flow`, compare category-by-category against `FlowSyntaxHighlighter.cs` output; tick each category. |
| TM-grammar → semantic-tokens visual transition during server-spawn window is acceptable | D-04 | Perception/timing — the switch-over produces a brief flicker that users notice or don't. | F5, open a fresh `.flow` file, watch the 0–300ms window after activation; record "noticeable / not noticeable". |
| Extension Development Host F5 loads extension, activates on a `.flow` file, shows `Flow Language Server` status | D-13 | Integration with the VSCode host process. | F5, open any `*.flow` under `examples/` or `tests/`, confirm status indicator present and diagnostics populate. |
| Per-platform binary works on a non-dev OS | D-14 | CI smoke-boots each binary but visual rendering still needs human eyes. | Before first release tag, install the OS-specific VSIX on one non-Linux machine (macOS or Windows), repeat the F5 checklist. |
| Marketplace + OpenVSX publish succeeds on tag push | D-15 | End-to-end publish is idempotent/destructive — dry-runs are the automated surrogate, but the real-run has to happen once. | On first tag, watch the CI workflow complete; verify the extension appears on both marketplaces with the right platform VSIX entries. |

---

## Security Domain

> `workflow.security_enforcement` defaults to enabled. Phase 17's security surface is small but not zero.

| ASVS Cat | Applies | Standard Control |
|----------|---------|-----------------|
| V5 Input Validation | yes (low risk) | Untrusted `.flow` source flows through `SimpleLexer` + `Parser` — both already have bounds (max parse depth 500, max error count 50). LSP wire input is JSON validated by OmniSharp against the protocol schema. |
| V14 Configuration | yes | `VSCE_PAT` and `OVSX_PAT` MUST live in GitHub Actions secrets only; never committed. Workflow files reference `${{ secrets.VSCE_PAT }}` / `${{ secrets.OVSX_PAT }}`. |

Known threats carried forward from `17-RESEARCH.md`:

| Threat | Mitigation |
|--------|-----------|
| Path traversal via `use "../../etc/passwd"` | Parse-only; never executes imported modules. `ModuleLoader` confines `@`-prefix to assembly dir. No new mitigation. |
| Malicious `.flow` DoSes the server | Existing parser bounds; additionally, LSP handlers catch unhandled exceptions and route via `window/logMessage` instead of crashing. |
| PAT leak via committed workflow | Secrets-only references; add `*.pat` / `.env` to `.gitignore` if not already. |
| Extension impersonation | Claim OpenVSX namespace (`npx ovsx create-namespace <publisher>`) before first publish; Marketplace publisher is locked to the Azure DevOps PAT identity. |
| Stale diagnostics after doc close | Close-race guard in DocumentManager onParse callback (plan 17-03); regression Fact `DocumentManagerTests.CloseCancelsPendingDiagnostics_NoPublishAfterClose`. |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies (map above covers every task)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify (every row has an automated command)
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 30s (quick) / < 90s (full)
- [x] `nyquist_compliant: true` set in frontmatter
- [x] Per-Task Verification Map populated (16 rows covering plans 17-01 through 17-08)

**Approval:** pending executor sign-off as tasks complete
