---
phase: 17-flow-language-server
fixed_at: 2026-04-20T00:00:00Z
review_path: .planning/phases/17-flow-language-server/17-REVIEW.md
iteration: 1
findings_in_scope: 6
fixed: 6
skipped: 0
status: all_fixed
facts_baseline: 96
facts_after: 117
facts_added: 21
---

# Phase 17: Code Review Fix Report

**Fixed at:** 2026-04-20
**Source review:** `.planning/phases/17-flow-language-server/17-REVIEW.md`
**Iteration:** 1

## Summary

- Findings in scope: 6 (1 critical, 5 warnings)
- Fixed: 6
- Skipped: 0
- Phase17 Facts: 96 baseline → 117 after (+21 new regression Facts)
- All fixes verified via `dotnet test --filter "FullyQualifiedName~Phase17"` green

Info findings (IN-01..IN-08) were out-of-scope per `fix_scope: critical_warning`.

## Fixed Issues

### CR-01: VSCode `proc` snippet emits invalid Flow syntax

**Severity:** Critical
**Files modified:**
- `vscode-extension/snippets/flow.code-snippets`
- `flow-lang.Tests/Unit/Phase17/VscodeSnippetsContractTests.cs` (new)

**Commit:** `b52a6d8`
**Facts added:** 2
- `Snippets_JsonIsValid`
- `ProcSnippet_UsesEndProcTerminator`

**Applied fix:** Replaced the brace-style `proc name () { $0 }` snippet body with the canonical `proc name ()\n\t$0\nend proc` form (matches `CompletionHandler.SnippetTemplates()` output, `Parser.cs:255` EndProc expectation, and all stdlib `*.flow` procs). Tempo / key / timesig / section snippets left unchanged — they correctly use brace syntax for musical context and section blocks. Added contract tests that locate the snippets file by walking up from the test assembly and assert both JSON validity and the `end proc` form (explicitly rejecting the old `() {` regression).

---

### WR-01: `DefinitionHandler` scans entire line for `"@...` regardless of cursor position

**Severity:** Warning
**Files modified:**
- `flow-lsp/Handlers/DefinitionHandler.cs`
- `flow-lang.Tests/Unit/Phase17/DefinitionHandlerTests.cs`

**Commit:** `6dd33d2`
**Facts added:** 6
- `HasUseKeywordBefore_BareUseKeyword_ReturnsTrue`
- `HasUseKeywordBefore_SubstringMatches_ReturnsFalse`
- `HasUseKeywordBefore_UseFollowedBySpaceOrQuote_ReturnsTrue`
- `HasUseKeywordBefore_NoUseInPrefix_ReturnsFalse`
- `NonUseStringWithAtPrefix_DoesNotTriggerStdlibJump` (primary regression)
- `UseImportStringLiteral_TriggersStdlibJump` (positive case)

**Applied fix:** Gated the stdlib-jump path on both (a) cursor column inside the `"@..."` span `[atIdx, end + 1]`, and (b) the prefix before the opening `"` containing a standalone `use` keyword (exposed as the public static `HasUseKeywordBefore` helper with word-boundary checks on both sides — rejects `misuse`, `abuser`, `used`, `x_use`). This prevents two false-positive paths: clicking on any token on a line that also contains `use "@audio"` anywhere, and plain string literals like `String s = "@notation"`.

---

### WR-02: `UserSymbolIndex` is never cleared on `didClose` — per-URI snapshots leak

**Severity:** Warning
**Files modified:**
- `flow-lsp/Handlers/TextDocumentSyncHandler.cs`
- `flow-lang.Tests/Unit/Phase17/TextDocumentSyncHandlerTests.cs` (new)

**Commit:** `552add9`
**Facts added:** 4
- `CloseDocument_CallsUserSymbolIndexRemove` (primary Fact)
- `CloseDocument_PublishesEmptyDiagnostics`
- `CloseDocument_UserSymbolIndexRemoveIsIdempotent`
- `CloseDocument_OtherUriUsersUntouched`

**Applied fix:** Injected `UserSymbolIndex` as a ctor dependency of `TextDocumentSyncHandler` (already registered as a singleton in `Program.cs`, so no bootstrap change was needed — OmniSharp's DI activator resolves it automatically). Added `_users.Remove(uri)` to the `DidCloseTextDocumentParams` handler after `DocumentManager.Close` and the empty-diagnostics publish. New regression Facts construct the handler with a recording diagnostics publisher and verify the full close fan-out (symbol removal, diagnostics publish, idempotence on double-close, scope isolation across URIs).

---

### WR-03: csproj targets net10.0, but CLAUDE.md and project constraints specify .NET 9

**Severity:** Warning
**Files modified:**
- `CLAUDE.md`

**Commit:** `7bdfc4a`
**Facts added:** 0 (docs-only fix)

**Applied fix:** Reconciled CLAUDE.md with the actual solution target (`net10.0`). Updated 7 net9/`.NET 9` references to net10/`.NET 10` across the intro paragraph, C# Conventions section, Project blurb, Constraints "Runtime" bullet, Technology Stack "Core Runtime" table row, DryWetMidi compatibility note, and Sources citation. The entire solution (flow-lang, flow-interpreter, flow-lsp, flow-lang.Tests) already targets `net10.0` via `TargetFramework` in each csproj; STATE.md had already tracked the CLAUDE.md drift as a known doc-lag. Per the fix prompt, this was resolved as a documentation update, not a code change.

---

### WR-04: `CompletionHandler.IsInsideUseStringLiteral` matches non-word-boundary `use`

**Severity:** Warning
**Files modified:**
- `flow-lsp/Handlers/CompletionHandler.cs`
- `flow-lang.Tests/Unit/Phase17/CompletionHandlerTests.cs`

**Commit:** `134aac1`
**Facts added:** 6
- `IsInsideUseStringLiteral_OnWordMisuse_ReturnsFalse` (primary regression)
- `IsInsideUseStringLiteral_OnWordAbuser_ReturnsFalse`
- `IsInsideUseStringLiteral_OnWordUsed_ReturnsFalse` (right-boundary case)
- `IsInsideUseStringLiteral_OnWordHouses_ReturnsFalse`
- `IsInsideUseStringLiteral_OnBareUseKeyword_ReturnsTrue` (positive sanity)
- `IsInsideUseStringLiteral_MisuseFollowedByUseOnSameLine_UsesStandaloneUse`

**Applied fix:** Replaced the simple `prefix.LastIndexOf("use")` with a new private helper `FindLastStandaloneUse` that walks backward and accepts only matches where neither neighbor is an identifier char (letter, digit, or underscore). Identifiers containing `use` as a substring (`misuse`, `abuser`, `houses`, `used`, `x_use`) no longer trigger the stdlib-path completion branch. Positive cases including `misuse` followed later on the same line by a standalone `use "@..."` still correctly activate (`FindLastStandaloneUse` returns the rightmost standalone match).

---

### WR-05: `NoteStreamContext.StreamContainsOffset` uses end-of-file as stream end if no closing `|` found

**Severity:** Warning
**Files modified:**
- `flow-lsp/NoteStream/NoteStreamContext.cs`
- `flow-lang.Tests/Unit/Phase17/NoteStreamContextTests.cs`

**Commit:** `32c9e4b`
**Facts added:** 3
- `StreamContainsOffset_OnUnclosedMidEditStream_ReturnsTrue` (primary; `key Cmajor { | C4 D4 \n}` with cursor before closing brace)
- `StreamContainsOffset_UnclosedStreamInProc_ReturnsTrue` (EOF fallback inside proc body)
- `StreamContainsOffset_PlainUnclosedStream_ReturnsTrue` (file-level unclosed stream, EOF branch)

**Applied fix:** Rewrote `FindMatchingCloseStream` with a sentinel `closedEnd = -1` that tracks the last seen closing `|`. On reaching a `}`, returns the last closing `|` if any, else the brace index itself (treats mid-edit cursors before the brace as inside the stream). On reaching end-of-source, returns the last closing `|` if any, else `source.Length` (EOF fallback for plain unclosed streams). Previously the function initialized `lastPipe = startOffset` and never re-assigned when no closing `|` was found, so `StreamContainsOffset`'s `cursor <= endOffset` check failed for any cursor past the opening — users got default completions instead of note-stream completions while actively typing in a stream.

During fact-authoring we discovered that the specific fixture in the review ("key Cmajor { | C4 D4" with NEITHER stream nor block closed) is NOT exercised by this fix: the parser returns 0 statements (errs=1) in that case, so no `NoteStreamExpression` exists in the AST to test. The fix correctly addresses the review's intended scenario (NoteStreamExpression node exists in the AST but lacks a closing pipe) — the adapted fixture closes the outer block to keep the parser's AST shape valid.

---

## Skipped Issues

_None_

---

## Verification Evidence

- `dotnet build flow-sharp.sln -c Debug` — clean after each atomic commit
- `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17"` — 117 passed, 0 failed
- Phase17 Fact count: 96 baseline → 117 after (+21 regression Facts)
- Commit chain: `b52a6d8` (CR-01) → `6dd33d2` (WR-01) → `552add9` (WR-02) → `7bdfc4a` (WR-03) → `134aac1` (WR-04) → `32c9e4b` (WR-05)

---

_Fixed: 2026-04-20_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
