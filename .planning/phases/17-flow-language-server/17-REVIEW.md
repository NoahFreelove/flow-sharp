---
phase: 17-flow-language-server
reviewed: 2026-04-20T00:00:00Z
depth: standard
files_reviewed: 54
files_reviewed_list:
  - .github/workflows/publish-extension.yml
  - .gitignore
  - docs/editor-setup/README.md
  - docs/editor-setup/generic-lsp.md
  - docs/editor-setup/helix-languages.toml
  - docs/editor-setup/helix.md
  - docs/editor-setup/manual-smoke.md
  - docs/editor-setup/neovim.md
  - docs/editor-setup/nvim-lspconfig.lua
  - flow-lang.Tests/Unit/Phase17/BuiltInDocsTests.cs
  - flow-lang.Tests/Unit/Phase17/BuiltInFunctionsTests.cs
  - flow-lang.Tests/Unit/Phase17/CompletionHandlerTests.cs
  - flow-lang.Tests/Unit/Phase17/DefinitionHandlerTests.cs
  - flow-lang.Tests/Unit/Phase17/DiagnosticsHandlerTests.cs
  - flow-lang.Tests/Unit/Phase17/DocumentManagerTests.CloseRace.cs
  - flow-lang.Tests/Unit/Phase17/DocumentManagerTests.cs
  - flow-lang.Tests/Unit/Phase17/HoverHandlerTests.cs
  - flow-lang.Tests/Unit/Phase17/LspFixtures.cs
  - flow-lang.Tests/Unit/Phase17/LspMappingsTests.cs
  - flow-lang.Tests/Unit/Phase17/NoteStreamContextTests.cs
  - flow-lang.Tests/Unit/Phase17/OmniSharpBootTest.cs
  - flow-lang.Tests/Unit/Phase17/ParseSessionTests.cs
  - flow-lang.Tests/Unit/Phase17/SemanticTokensTests.cs
  - flow-lang.Tests/Unit/Phase17/SignatureHelpHandlerTests.cs
  - flow-lang.Tests/Unit/Phase17/SymbolIndicesTests.Indices.cs
  - flow-lang.Tests/Unit/Phase17/SymbolIndicesTests.cs
  - flow-lang/StandardLibrary/BuiltInDocs.cs
  - flow-lsp/DocumentManager.cs
  - flow-lsp/Handlers/CompletionHandler.cs
  - flow-lsp/Handlers/DefinitionHandler.cs
  - flow-lsp/Handlers/DiagnosticsPublisher.cs
  - flow-lsp/Handlers/HoverHandler.cs
  - flow-lsp/Handlers/SemanticTokensHandler.cs
  - flow-lsp/Handlers/SignatureHelpHandler.cs
  - flow-lsp/Handlers/TextDocumentSyncHandler.cs
  - flow-lsp/LspMappings.cs
  - flow-lsp/NoteStream/NoteStreamContext.cs
  - flow-lsp/ParseSession.cs
  - flow-lsp/Program.cs
  - flow-lsp/Semantic/SemanticTokensEncoder.cs
  - flow-lsp/Symbols/BuiltInIndex.cs
  - flow-lsp/Symbols/KeywordIndex.cs
  - flow-lsp/Symbols/StdlibSymbolIndex.cs
  - flow-lsp/Symbols/UserSymbolIndex.cs
  - flow-lsp/flow-lsp.csproj
  - scripts/lsp-smoke.sh
  - vscode-extension/.vscodeignore
  - vscode-extension/README.md
  - vscode-extension/language-configuration.json
  - vscode-extension/package.json
  - vscode-extension/snippets/flow.code-snippets
  - vscode-extension/src/extension.ts
  - vscode-extension/syntaxes/flow.tmLanguage.json
  - vscode-extension/tsconfig.json
findings:
  critical: 1
  warning: 5
  info: 8
  total: 14
status: issues_found
---

# Phase 17: Code Review Report

**Reviewed:** 2026-04-20
**Depth:** standard
**Files Reviewed:** 54
**Status:** issues_found

## Summary

Phase 17 introduces a full LSP server (`flow-lsp`), a VSCode extension, editor-setup docs for Helix/Neovim/generic editors, a per-platform publish workflow, and comprehensive xUnit test coverage (14 test files, all pure-static helpers with a single reflection-scoped OmniSharp boot smoke). The production LSP code is well-structured, with deliberate separation between transport-bound OmniSharp handlers and pure helpers exposed to Facts. Thread-safety in `DocumentManager` and `UserSymbolIndex` is carefully handled with locks; the close-race guard is explicitly modeled with a regression Fact.

Review surfaced one BLOCKER: the VSCode extension snippet file (`vscode-extension/snippets/flow.code-snippets`) emits invalid Flow syntax for the `proc` snippet, using `{ ... }` instead of the `end proc` block terminator. This is end-user-facing and will produce uncompilable code as soon as a user types `proc` + Tab in VSCode.

Other significant findings cluster around UX correctness rather than protocol safety: `DefinitionHandler` does a line-wide `"@..."` scan regardless of cursor position or preceding `use` keyword, so `go-to-def` on any unrelated token on a line containing a stdlib import string will incorrectly jump to the stdlib file; and `UserSymbolIndex` entries are never removed on `didClose`, leaking per-URI symbol snapshots for the lifetime of the session.

No security vulnerabilities were identified. The CI workflow handles secrets correctly via `${{ secrets.* }}`, the bash smoke script properly quotes its binary path argument, and the TypeScript extension performs no shell interpolation on the `flow.server.path` user setting.

Nothing in the LSP critical path (didOpen/didChange/didClose debounce + parse + publish pipeline) or the semantic-tokens encoder raised correctness concerns; the close-race Fact, the SkipBetweenMapped delta-math Fact, and the CursorAfterClosedKeyBlock regression Fact all pin the right invariants.

## Critical Issues

### CR-01: VSCode `proc` snippet emits invalid Flow syntax

**File:** `vscode-extension/snippets/flow.code-snippets:18-20`
**Issue:** The `proc` snippet body is
```json
"body": ["proc ${1:name} (${2}) {", "\t$0", "}"]
```
which expands to `proc name () { ... }`. Flow's actual `proc` syntax is `proc name (...) body end proc` — SimpleLexer.cs:583 maps the keyword `end` to `TokenType.EndProc`, and every stdlib proc in `flow-lang/audio.flow` et al uses `end proc`. `CompletionHandler.SnippetTemplates()` in flow-lsp correctly emits `"proc ${1:name} ()\n\t$0\nend proc"`, but the package-shipped snippets file does not agree. Users who type `proc` + Tab in VSCode (a very common path — activation-events include `onLanguage:flow` and the snippet prefix matches the keyword exactly) get a syntactically invalid stub that will redline on the very next parse. `manual-smoke.md:166` explicitly lists the `proc` snippet expansion as part of the smoke gate, so this will fail human UAT once exercised.
**Fix:**
```json
"Proc declaration": {
  "prefix": "proc",
  "body": ["proc ${1:name} (${2})", "\t$0", "end proc"],
  "description": "Declare a proc"
}
```
The `tempo`, `key`, `timesig`, and `section` snippets in the same file are already correct (they use braces, matching Flow's musical context + section block syntax). Only the `proc` entry needs the fix.

## Warnings

### WR-01: `DefinitionHandler` scans entire line for `"@...` regardless of cursor position

**File:** `flow-lsp/Handlers/DefinitionHandler.cs:88-112`
**Issue:** The stdlib-import jump code does `lineStr.IndexOf("\"@", System.StringComparison.Ordinal)` across the whole cursor line, not the token under the cursor. Consequences:

1. **Cross-token false positive.** Given `Int x = 5 ; use "@audio"` on one line, clicking go-to-def on `x` jumps to `audio.flow` because the `"@` pattern is found somewhere on the line. The user's cursor position is ignored.
2. **Non-use string triggers.** A plain string literal containing `"@notation"` (e.g. `String s = "@notation"`) will also trigger the stdlib jump if the resolved path happens to exist. The code does test `File.Exists` first, so only real stdlib names jump, but that is an accidental coincidence rather than a cursor check.

**Fix:** Gate the stdlib path on (a) the cursor being inside the `"@...."` token, and (b) the line containing a `use` keyword before the `"`:
```csharp
var lineStr = lines[request.Position.Line];
var atIdx = lineStr.IndexOf("\"@", System.StringComparison.Ordinal);
if (atIdx >= 0)
{
    var end = lineStr.IndexOf('"', atIdx + 1);
    if (end > atIdx
        && request.Position.Character >= atIdx
        && request.Position.Character <= end + 1
        && lineStr.AsSpan(0, atIdx).IndexOf("use") >= 0)
    {
        // existing resolve + File.Exists + return block
    }
}
```

### WR-02: `UserSymbolIndex` is never cleared on `didClose` — per-URI snapshots leak

**File:** `flow-lsp/Handlers/TextDocumentSyncHandler.cs:53-59`, `flow-lsp/Symbols/UserSymbolIndex.cs:43-49`
**Issue:** `UserSymbolIndex` exposes a `Remove(DocumentUri)` method but no caller invokes it. `TextDocumentSyncHandler.Handle(DidCloseTextDocumentParams)` closes the document in `DocumentManager` and clears diagnostics, but leaves the user-symbol snapshot in place forever. For a long-running LSP session with many opened/closed files, the per-URI dictionary grows unbounded. A grep confirms no production caller invokes `users.Remove` or `UserSymbolIndex.Remove` anywhere in flow-lsp/.

This is primarily a memory leak in long-running sessions; the symbols themselves cannot leak across URIs (`CompletionsFor(uri)` is scoped per-URI), so there is no false-completion bug.

**Fix:** Wire `users.Remove` into the close path. Either (a) inject `UserSymbolIndex` into `TextDocumentSyncHandler` and call `_users.Remove(request.TextDocument.Uri)` in `DidCloseTextDocumentParams`, or (b) push a `Removed` event from `DocumentManager.Close` so the index observes it generically. Option (a) is simpler and mirrors the Open/Update path where `Program.cs` hands the index to the onParse callback.

### WR-03: csproj targets net10.0, but CLAUDE.md and project constraints specify .NET 9

**File:** `flow-lsp/flow-lsp.csproj:3`, `.github/workflows/publish-extension.yml:54`, `docs/editor-setup/README.md:23`, `docs/editor-setup/helix.md:14`, `docs/editor-setup/neovim.md:14`
**Issue:** The flow-lsp csproj sets `<TargetFramework>net10.0</TargetFramework>` and the CI workflow provisions `10.0.x`. CLAUDE.md documents the project constraint as `Runtime: .NET 9 — all code must target net9.0`. The editor-setup docs also reference ".NET 10 SDK".

If the rest of the solution (flow-lang, flow-interpreter) truly targets net9.0, a net10.0-only project is a build break for downstream tooling that standardizes on the .NET 9 LTS. If the broader project has actually moved to net10.0, CLAUDE.md is stale and should be updated.

**Fix:** Reconcile. Either:
- Change flow-lsp.csproj to `net9.0` and the workflow to `9.0.x` if .NET 9 is the real target, OR
- Update CLAUDE.md "Constraints" section (line 29-34) to say net10.0 if .NET 10 is now standard. The editor-setup docs are already consistent with net10.0 — only CLAUDE.md needs updating in that branch.

Review cannot decide which is correct without maintainer intent; flagging the discrepancy.

### WR-04: `CompletionHandler.IsInsideUseStringLiteral` matches non-word-boundary `use`

**File:** `flow-lsp/Handlers/CompletionHandler.cs:197-216`
**Issue:** The check does `prefix.LastIndexOf("use")` (substring, not word-boundary). Any identifier containing "use" as a substring triggers the stdlib-path completion branch:

- `misuse` — would match `use` inside it; if followed by an open quote, completion incorrectly surfaces `@std`, `@audio`, etc. as suggestions.
- `abuser` — similar.
- `houses` — similar.

In practice, the failure mode is minor (user types `misuse "...` and gets stdlib module completions) but it still counts as pollution of a supposedly-gated completion set (the gate is advertised as "only when typing `use "..."`").

**Fix:** Use a word-boundary regex, or check `useIdx == 0 || !IsIdentChar(prefix[useIdx - 1])`:
```csharp
var useIdx = prefix.LastIndexOf("use");
if (useIdx < 0) return false;
if (useIdx > 0 && (char.IsLetterOrDigit(prefix[useIdx - 1]) || prefix[useIdx - 1] == '_'))
    return false; // "use" is embedded in a longer identifier
// Also verify the char AFTER "use" is not an identifier character:
var afterIdx = useIdx + 3;
if (afterIdx < prefix.Length && (char.IsLetterOrDigit(prefix[afterIdx]) || prefix[afterIdx] == '_'))
    return false;
// Existing quote-count logic follows.
```

### WR-05: `NoteStreamContext.StreamContainsOffset` uses end-of-file as stream end if no closing `|` found

**File:** `flow-lsp/NoteStream/NoteStreamContext.cs:226-248`
**Issue:** `FindMatchingCloseStream` scans forward from the opening `|` looking for more `|` or a `}`. If the user is mid-edit with an unclosed note stream at the end of the buffer (e.g. they just typed `| C4 D4 ` without the closing `|`), the function returns `lastPipe = startOffset` (just the opening pipe). A cursor positioned AFTER the opening pipe will then fail the `cursorOffset <= endOffset` check and the note-stream branch in CompletionHandler will not activate — the user gets default completions (procs, keywords, builtins) instead of note-stream completions at exactly the moment they most need them.

The algorithm also depends on braces not appearing inside a bar (which is true of Flow note streams) and assumes a newline is not a stream terminator (which is actually correct per Flow's multi-line bar syntax). The unclosed-stream edge case is the real concern.

**Fix:** When the loop finds no additional `|` after the opening one, fall back to end-of-file (or the next `}`) as the stream end. Currently `lastPipe` is initialized to `startOffset`; initialize it instead to `source.Length` (or the next `}` index), then update to each `|` we see but NEVER shrink back:
```csharp
int lastPipe = source.Length; // default to EOF — unclosed stream
for (int i = startOffset + 1; i < source.Length; i++)
{
    char c = source[i];
    if (c == '|') { lastPipe = i; }
    else if (c == '}') { if (lastPipe == source.Length) lastPipe = i; break; }
}
return lastPipe;
```
This ensures mid-edit unclosed streams still route the cursor to the note-stream completion branch.

## Info

### IN-01: Editor-setup documentation references `.NET 10 SDK` which may diverge from project constraints

**File:** `docs/editor-setup/README.md:23`, `docs/editor-setup/helix.md:14`, `docs/editor-setup/neovim.md:14`, `docs/editor-setup/helix-languages.toml:8`, `docs/editor-setup/nvim-lspconfig.lua:7`
**Issue:** All three editor-setup docs explicitly say "Requires the .NET 10 SDK". If WR-03 resolves to `net9.0`, these docs become inaccurate.
**Fix:** Reconcile after WR-03 decision.

### IN-02: TextMate grammar note regex accepts `[+-]*` in the middle of a note literal

**File:** `vscode-extension/syntaxes/flow.tmLanguage.json:76`
**Issue:** The note pattern `\\b[A-G][#bsf]?[+-]*[0-9]+(?:[qhwes]\\.?~?)?(?:\\+[0-9]+c)?\\b` allows any sequence of `+` / `-` between the accidental and the octave digit. Flow has no `C-4` or `C++4` note syntax — only accidentals `#bsf` and octave digits. This is cosmetic (a TM grammar false-positive highlights rare strings as notes) but not a correctness issue because the LSP semantic tokens handler re-classifies with lexer precision per D-04 hybrid.
**Fix:** Remove `[+-]*`:
```json
"match": "\\b[A-G][#bsf]?[0-9]+(?:[qhwes]\\.?~?)?(?:\\+[0-9]+c)?\\b"
```

### IN-03: `StdlibSymbolIndex` first-write-wins hides proc shadowing across modules

**File:** `flow-lsp/Symbols/StdlibSymbolIndex.cs:52-58`
**Issue:** `if (pd is ProcDeclaration pd && !_byName.ContainsKey(pd.Name))` drops second occurrences of the same proc name. `std.flow` re-exports `@collections` and `@bars`, so `head`, `tail`, `map`, etc. appear in both `@collections` (the owning module) and transitively in `@std`. Order of iteration (`ModuleNames` array: std first, then audio, collections, bars, notation, composition) means procs resolve to `@std` rather than their authoring module. This is a hover-label issue (`Detail = "(stdlib: @std)"` instead of `@collections`) but nothing more — not a bug in completion behavior.
**Fix:** Walk modules in dependency order (e.g. collections, bars before std), OR index by (name, module) tuple and surface all modules in hover. Low priority.

### IN-04: Extension `out/test/**` exclusion in `.vscodeignore` references a path that is never produced

**File:** `vscode-extension/.vscodeignore:4`
**Issue:** Line 4 excludes `out/test/**` but there is no `test/` subdirectory under `src/` (the only thing that compiles to `out/`). The entry is harmless — it just matches nothing — but suggests leftover from a template.
**Fix:** Drop the line, or add a placeholder comment explaining it is defensive.

### IN-05: `.gitignore` `!vscode-extension/tests/**/*.flow` interacts with `tests/` ignore

**File:** `.gitignore:24-26`
**Issue:** Line 8 ignores `tests/` globally, then lines 24-26 unignore `vscode-extension/tests/**`. The grammar test corpus lives there. This works, but the pattern interaction is fragile — a future `tests/` ignore refinement elsewhere in the file could re-shadow these. A single `!vscode-extension/tests/` (no `**`) combined with the existing overrides would be equivalent and simpler.
**Fix:** Consolidate to:
```
!vscode-extension/tests/
!vscode-extension/tests/**
```
Already done. Just noting the existing structure is not a bug, and if simplified further watch out for the `tests/` ignore applying differently.

### IN-06: `SemanticTokensHandler` ignores cancellation timing — partial token set may reach client

**File:** `flow-lsp/Handlers/SemanticTokensHandler.cs:61-65`
**Issue:** If `cancellationToken` is cancelled mid-loop, the method `break`s with a partial token list already pushed into the builder. OmniSharp will emit whatever was pushed. LSP spec allows clients to re-request on cancellation, so the client self-heals, but the partial response is technically ambiguous (not invalid). The current behavior is fine for v1; documenting for future tightening.
**Fix:** (Optional) `throw new OperationCanceledException()` instead of `break` so OmniSharp sees an explicit cancel signal and does not commit the partial builder state.

### IN-07: `NoteStreamContext.IsTriviaToken` returns hardcoded false — comment hints at fragile assumption

**File:** `flow-lsp/NoteStream/NoteStreamContext.cs:111-117`
**Issue:** `IsTriviaToken` always returns false and hard-comments "SimpleLexer does not emit whitespace tokens". Correct today, but the helper exists only to be extended. If a future phase adds whitespace/newline tokens to SimpleLexer, this helper becomes a latent bug because `FindEnclosingKey`'s `keyIdx` reverse-walk-through-trivia loop will no longer skip anything.
**Fix:** Either (a) delete the helper and the pre-trivia-loop entirely (cleaner — current lexer never has trivia), or (b) add an assert in SimpleLexer that pins "no trivia tokens" as an invariant and triggers a compile error if lexer changes contradict it. Low priority.

### IN-08: Publish workflow lacks explicit `permissions:` block

**File:** `.github/workflows/publish-extension.yml:1-161`
**Issue:** The workflow writes to VSCode Marketplace and OpenVSX via `${{ secrets.VSCE_PAT }}` and `${{ secrets.OVSX_PAT }}`; it does not interact with GitHub's repo contents. Without an explicit `permissions:` block, the workflow inherits the repo default (often `contents: write` for push events). Best practice for least-privilege is to scope down:
```yaml
permissions:
  contents: read
```
at the workflow top level.
**Fix:** Add the block above. Non-urgent — current permissions are not being abused and secrets are the real publish path.

---

_Reviewed: 2026-04-20_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
