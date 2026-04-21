---
phase: 17-flow-language-server
plan: 06
subsystem: lsp
tags: [lsp, hover, signature-help, definition, note-stream, completion]

# Dependency graph
requires:
  - phase: 17-flow-language-server
    plan: 03
    provides: "DocumentManager + handler registration pattern"
  - phase: 17-flow-language-server
    plan: 05
    provides: "4 symbol indices (BuiltIn/Stdlib/User/Keyword), CompletionHandler with BuildItems 7-arg signature, BuiltInDocs 104 entries, RegisterSignaturesOnly D-07 coverage"
provides:
  - "NoteStreamContext — token-scan walker over ParseResult.Tokens with brace-depth tracking; IsInsideNoteStream (AST-based), FindEnclosingKey (token-scan)"
  - "CompletionHandler D-11 branch — note-stream context gating (roman numerals if enclosing key, else note letters+durations+rest; excludes proc/variable/keyword names inside note streams)"
  - "HoverHandler — 3-way lookup (BuiltIn → User → Stdlib); markdown content with signature + BuiltInDocs summary"
  - "SignatureHelpHandler.DetectCall — active-parameter by comma count; nested parens correctly skipped; trigger chars `(` `,`"
  - "DefinitionHandler — user-symbol AST walk + stdlib .flow file jump via ModuleLoader.ResolveStdlibPath; built-ins return null (D-09)"
  - "Program.cs wire — 6 handlers now registered (TextDocumentSync + SemanticTokens + Completion + Hover + SignatureHelp + Definition)"
affects: [17-07, 17-08]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Token-scan over cached ParseResult.Tokens instead of re-lexing. Walker consumes IReadOnlyList<Token> as a parameter — no SimpleLexer re-instantiation (which would require an ErrorReporter and duplicate work). Brace-depth tracking walks backward from cursor, decrementing on `{` and incrementing on `}`; when depth < 0 we've crossed into the enclosing block and can inspect the 2 tokens before the `{` for a `key <id>` pattern."
    - "Regression Fact as block-exit discriminator: CursorAfterClosedKeyBlock_FindEnclosingKey_ReturnsNull pins that a cursor AFTER a closed `key { }` block does NOT match that key. A naive line-heuristic (`cursor.Line >= stmt.Location.Line - 1`) fails this Fact; only the token-scan passes. Pattern reusable for future block-tracking features (fold regions, bracket matching)."
    - "Pure static handler-method pattern (plan 17-05 carried forward): BuildHover / DetectCall / FindUserDeclaration are all `public static` methods so the Fact suite can exercise them without an OmniSharp transport. Keeps the L1 unit test layer free of transport plumbing."
    - "Range type alias `using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;` — reused from plan 17-03 LspMappings.cs pattern. Required in DefinitionHandler.cs because the C# 8+ `System.Range` type collides with the OmniSharp LSP model. Position does NOT collide."

key-files:
  created:
    - "flow-lsp/NoteStream/NoteStreamContext.cs"
    - "flow-lsp/Handlers/HoverHandler.cs"
    - "flow-lsp/Handlers/SignatureHelpHandler.cs"
    - "flow-lsp/Handlers/DefinitionHandler.cs"
    - "flow-lang.Tests/Unit/Phase17/NoteStreamContextTests.cs"
    - "flow-lang.Tests/Unit/Phase17/HoverHandlerTests.cs"
    - "flow-lang.Tests/Unit/Phase17/SignatureHelpHandlerTests.cs"
    - "flow-lang.Tests/Unit/Phase17/DefinitionHandlerTests.cs"
  modified:
    - "flow-lsp/Handlers/CompletionHandler.cs (BuildItems signature extended with Program? ast + IReadOnlyList<Token>? tokens; note-stream branch + RomanNumeralItems + DefaultNoteStreamItems; ParseSession DI; Handle threads result.Tokens)"
    - "flow-lsp/Program.cs (3 new .WithHandler<> registrations — HoverHandler, SignatureHelpHandler, DefinitionHandler)"
    - "flow-lang.Tests/Unit/Phase17/CompletionHandlerTests.cs (3 existing Facts migrated to new 9-arg BuildItems signature + 2 new note-stream Facts)"

key-decisions:
  - "Token-scan over ParseResult.Tokens chosen over AST walk for FindEnclosingKey because brace-depth tracking is naturally a lexer-level concern. The AST's `MusicalContextStatement(Key)` would expose the key name but only after the entire block parses cleanly — when the user is editing inside `key Cmajor { | <CURSOR>` the opening `}` might not yet be typed, so the AST wouldn't necessarily have the key as an ancestor. Token-scan handles this gracefully."
  - "IsInsideNoteStream stays AST-based because stream boundaries (`| ... |`) are a parser construct — the lexer emits Pipe tokens but doesn't distinguish opening-pipe vs closing-pipe vs separator-pipe. The AST walker looks for NoteStreamExpression nodes and computes containment via source-offset math."
  - "RomanNumeralItems resolves Detail via HarmonyFunctions.ScaleDatabase.ResolveRomanNumeral — e.g. `I` in `Cmajor` renders as `Detail = \"C (in Cmajor)\"` which makes the completion dropdown informative. Falls back to generic \"Roman numeral in <key>\" if resolver returns null (e.g. unknown numeral)."
  - "Major/Minor detection via case-insensitive `IndexOf(\"minor\", ...)` — same heuristic the planner called for. Key names like `Cmajor`, `Dminor`, `FsMinor` all classify correctly. Edge case: `harmonicMinor` would classify as minor (matches planner intent)."
  - "Per-request re-parse in CompletionHandler.Handle for correctness over caching. A DocumentManager per-URI ParseResult cache is the obvious future optimization (parse once per edit, consume in multiple handlers) but is not required for v1 correctness — SUMMARY notes it as a candidate for future work. Measured cost was acceptable for the test suite."
  - "UserProc + Variable + Section all recognized by DefinitionHandler.FindUserDeclaration. Nested procs handled by recursive walk into proc bodies, section bodies, and musical-context bodies (same walker shape as UserSymbolIndex)."
  - "Stdlib import click-through detects `use \"@...\"` by scanning the cursor line for `\"@` and extracting the module name up to the next `\"`. Cheap + correct for the common path; a tokenized context could tighten this in future if users type pathological stdlib names."
  - "Built-ins return null for go-to-def per D-09. LSP spec allows null for `textDocument/definition`; VSCode renders this as \"No definition found\" which is correct behavior for C#-implemented built-ins."

patterns-established:
  - "NoteStreamContext API shape — `(FlowProgram ast, IReadOnlyList<Token> tokens, string source, Position cursor)`. Pattern reusable for any cursor-context detector that needs both AST structure and token-level precision (e.g., future: \"is cursor inside a chord literal\", \"what proc am I inside\")."
  - "Regression Fact for block-exit bugs — write a Fact with a cursor explicitly AFTER a closed block, assert it does NOT match. Discriminates line-heuristic implementations from proper brace-tracking. Reusable for any block-scoped language feature."
  - "Handler subclass + pure static helper pattern continues — HoverHandler.BuildHover, SignatureHelpHandler.DetectCall, DefinitionHandler.FindUserDeclaration are all static. The subclass only plumbs `DocumentManager.GetText` + static helper invocations."

requirements-completed: [D-08, D-09, D-10, D-11]

# Metrics
duration: ~11min
completed: 2026-04-20
---

# Phase 17 Plan 06: Hover + SignatureHelp + Definition + NoteStreamContext Summary

**Wave 5 Flow LSP completion: 4 new handler/helper classes (NoteStreamContext + Hover + SignatureHelp + Definition) ship D-08/D-09/D-10/D-11. Token-scan walker over cached ParseResult.Tokens closes the Warning #3 block-exit bug. 24 new Facts green (6 NoteStreamContext + 2 new CompletionHandler + 6 Hover + 4 SigHelp + 6 Definition); 96/96 Phase 17 Facts green, 236/236 full-suite green.**

## Performance

- **Duration:** ~11 min (21:40 → 21:52 UTC)
- **Tasks:** 2 (atomic commits)
- **Files created:** 8 (4 production + 4 test)
- **Files modified:** 3 (CompletionHandler, Program.cs, CompletionHandlerTests)
- **Tests added:** 24 Facts — all green

## Accomplishments

- **D-11 delivered.** CompletionHandler now gates on note-stream context first. Inside a `| ... |` stream, the default 5-source merge is replaced by:
  - Roman numeral items (`I`, `ii`, `iii`, `IV`, `V`, `V7`, `vi`, `vii°` for major keys; `i`, `ii°`, `III`, `iv`, `v`, `V7`, `VI`, `VII` for minor) when an enclosing `key <name> { ... }` block is detected.
  - Default stream items (note letters C–B, octave-4 notes C4–B4, duration suffixes q/h/w/e/s, rest `_`) when no key is enclosing.
  - Proc/variable/keyword names are suppressed per D-11 — verified by the `NoteStreamWithKey_ReturnsRomanNumerals` and `NoteStreamWithoutKey_ReturnsNoteLettersAndDurations` Facts.

- **Warning #3 block-exit bug closed.** `NoteStreamContext.FindEnclosingKey` walks the cached `ParseResult.Tokens` backward from the cursor offset, tracking `{`/`}` depth. When the scan crosses into an enclosing block (`depth < 0`), it checks the 2 tokens before the `{` for a `key <identifier>` pattern. Cursors after a closed `key { }` block correctly return null. Pinned by the `CursorAfterClosedKeyBlock_FindEnclosingKey_ReturnsNull` regression Fact.

- **D-08 Hover delivered.** `HoverHandler.BuildHover` implements the 3-way lookup exactly as specified: BuiltInIndex → BuiltInDocs.TryGet summary; UserSymbolIndex → kind+name; StdlibSymbolIndex → module-qualified signature. Markdown content uses the `flow` code block fence. Built-ins with no BuiltInDocs entry render signature-only with an `*(no documentation)*` placeholder.

- **D-10 SignatureHelp delivered.** `SignatureHelpHandler.DetectCall` is a pure parser — backward scan on the cursor line, paren-depth tracking for nested calls, comma count as the active parameter index. The `NestedParens_OnlyOuterDepthCounts` Fact proves the depth tracking correctly handles `outer(mul(a, b), |` → ActiveParameter=1 for `outer`.

- **D-08 Definition delivered.** `DefinitionHandler.FindUserDeclaration` walks the AST for ProcDeclaration, VariableDeclaration, and SectionDeclaration (recursing into proc + section + musical-context bodies). Stdlib imports jump to the resolved `.flow` file via `ModuleLoader.ResolveStdlibPath`. Built-ins return null per D-09 — honored because no BuiltInIndex lookup is performed in the definition path.

- **Program.cs wire** adds 3 new `.WithHandler<>()` calls (Hover, SignatureHelp, Definition) and updates CompletionHandler DI to receive `ParseSession` (for per-request re-parse used by the note-stream branch). No new service registrations needed — everything is already DI-registered from plan 17-05.

## Task Commits

1. **Task 1: NoteStreamContext token-scan walker + CompletionHandler D-11 branch** — `d6dcc89` (feat)
2. **Task 2: Hover + SignatureHelp + Definition handlers + Program.cs wiring** — `c8a4678` (feat)

## Files Created/Modified

### Created

- `flow-lsp/NoteStream/NoteStreamContext.cs` — token-scan walker. `IsInsideNoteStream(Program, string, Position)` (AST walk), `FindEnclosingKey(Program, IReadOnlyList<Token>, string, Position)` (token-scan with brace-depth tracking).
- `flow-lsp/Handlers/HoverHandler.cs` — `HoverHandlerBase` subclass. Static `BuildHover(string?, BuiltInIndex, UserSymbolIndex, StdlibSymbolIndex, DocumentUri)` + `IdentifierAt(string, Position)` helpers.
- `flow-lsp/Handlers/SignatureHelpHandler.cs` — `SignatureHelpHandlerBase` subclass. Static `DetectCall(string, Position) → CallContext?` parser.
- `flow-lsp/Handlers/DefinitionHandler.cs` — `DefinitionHandlerBase` subclass. Static `FindUserDeclaration(Program, string) → (int, int)?` AST walker + stdlib-import jump logic in `Handle`.
- `flow-lang.Tests/Unit/Phase17/NoteStreamContextTests.cs` — 6 Facts (inside-stream no-key, inside-stream with-key, nested-deeper-wins, file-start-no-stream, block-exit regression, sibling-stream-after-closed-key).
- `flow-lang.Tests/Unit/Phase17/HoverHandlerTests.cs` — 6 Facts (builtin shows sig+doc, unknown returns null, user proc hover, identifier-at-cursor, identifier-at-whitespace, empty identifier).
- `flow-lang.Tests/Unit/Phase17/SignatureHelpHandlerTests.cs` — 4 Facts (no args → active 0, one comma → active 1, no parens → null, nested parens depth tracking).
- `flow-lang.Tests/Unit/Phase17/DefinitionHandlerTests.cs` — 6 Facts (user-proc-location, unknown returns null, stdlib-import path rooted, stdlib file exists, user-variable location, nested proc found).

### Modified

- `flow-lsp/Handlers/CompletionHandler.cs` — `BuildItems` extended from 7 to 9 params (`FlowProgram? ast`, `IReadOnlyList<Token>? tokens`). Note-stream branch (gates FIRST — even before use-string gate). `RomanNumeralItems(string keyName)` + `DefaultNoteStreamItems()` helpers. Constructor gains `ParseSession _parser`. `Handle` does per-request parse and threads `result.Ast` + `result.Tokens` through to `BuildItems`. Added `using FlowLang.Lexing`, `using FlowProgram = FlowLang.Ast.Program`.
- `flow-lsp/Program.cs` — 3 new `.WithHandler<>()` calls appended. No new DI registrations.
- `flow-lang.Tests/Unit/Phase17/CompletionHandlerTests.cs` — 3 existing Facts updated to pass `result.Ast` + `result.Tokens` to the new 9-arg `BuildItems`. 2 new Facts (`NoteStreamWithKey_ReturnsRomanNumerals`, `NoteStreamWithoutKey_ReturnsNoteLettersAndDurations`).

## Actual OmniSharp override methods used

Confirmed by first build:

- **HoverHandler** extends `HoverHandlerBase` (namespace `OmniSharp.Extensions.LanguageServer.Protocol.Document`). Overrides `Handle(HoverParams, CancellationToken) → Task<Hover?>` and `CreateRegistrationOptions(HoverCapability, ClientCapabilities) → HoverRegistrationOptions`. `HoverCapability` lives in `OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities`.
- **SignatureHelpHandler** extends `SignatureHelpHandlerBase` (same namespace). Overrides `Handle(SignatureHelpParams, CancellationToken) → Task<SignatureHelp?>` and `CreateRegistrationOptions(SignatureHelpCapability, ClientCapabilities) → SignatureHelpRegistrationOptions`. TriggerCharacters: `(` and `,`.
- **DefinitionHandler** extends `DefinitionHandlerBase`. Overrides `Handle(DefinitionParams, CancellationToken) → Task<LocationOrLocationLinks?>` and `CreateRegistrationOptions(DefinitionCapability, ClientCapabilities) → DefinitionRegistrationOptions`.
- All three use `TextDocumentSelector.ForLanguage("flow")` for registration (same pattern as 17-03/17-04/17-05).

## Token record field names confirmed

Per plan's output requirement — verified at `flow-lang/Lexing/Token.cs`:

- `Token(TokenType Type, string Text, SourceLocation Location, object? Value = null)`
- **Lexeme is `Token.Text`** — NOT `Token.Lexeme` (that field doesn't exist).
- **Line/Column are on `Token.Location`** — NOT top-level `Token.Line` / `Token.Column`.
- **Lexer is 1-based** (confirmed via `SimpleLexer.cs:43` where `_line` starts at 1). `TokenAbsOffset` uses `Math.Max(0, t.Location.Line - 1)` to guard `SourceLocation.Unknown` (which has Line=0/Column=0).
- Grep verifies — `! grep -q '.Lexeme' flow-lsp/NoteStream/NoteStreamContext.cs` passes.

## File.Exists on stdlib path in test context

`StdlibImport_FileExists_WhenStdlibCopiedToOutput` Fact PASSES under test runner. `ModuleLoader.ResolveStdlibPath("@audio")` resolves to `flow-lang.Tests/bin/Debug/net10.0/audio.flow` which exists because `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` on the stdlib .flow files (per flow-lang.csproj) propagates them to every consuming project's output. This confirms the CopyToOutputDirectory contract is intact — a regression here would have meant the LSP's `StdlibSymbolIndex` construction would silently degrade to empty.

## Per-request re-parse cost

CompletionHandler.Handle now re-parses on every completion request (needed for the note-stream context detection). Measured test-suite cost: acceptable (96 Phase17 tests run in ~1s, 236 full-suite in ~17s). Under real editor load, the parse cost is still bounded by SimpleLexer + Parser (both O(n) in source length) and runs off the UI thread via OmniSharp's async pipeline. **Candidate future optimization:** DocumentManager could cache the most-recent ParseResult per URI keyed by text-hash, and handlers consume the cached result instead of re-parsing. Not required for v1 correctness; logged here for plan 17-07/17-08 or a future performance pass if telemetry shows completion latency regressions.

## Trivia-token handling

`NoteStreamContext.IsTriviaToken` is a no-op (`return false`) — confirmed against `SimpleLexer.cs`'s `SkipWhitespaceAndComments` which consumes whitespace in the lex loop and NEVER enqueues a whitespace/newline token. Comments are skipped by the same loop. The `IsTriviaToken` hook stays in place as a future-proofing anchor: if a future phase adds trivia tokens to the lexer, the walker skips them correctly in the backward scan.

## Decisions Made

1. **Token-scan over AST-walk for enclosing-key detection** — The AST has `MusicalContextStatement(ContextType=Key)` nodes that expose the key name, but they only appear after the full block parses cleanly. When the user is editing inside `key Cmajor { | <CURSOR>` the closing `}` may not yet be typed, so the AST walker would miss the key. Token-scan handles partial/incomplete source gracefully because tokens are produced regardless of parse recovery quality.

2. **AST-walk for IsInsideNoteStream** — The inverse choice for stream boundaries, which are a parser construct (the lexer emits `Pipe` tokens but doesn't distinguish opening / separator / closing). The walker looks for `NoteStreamExpression` nodes and checks source-offset containment. Nested streams don't exist in Flow — a stream can span multiple bars (`| ... | ... |`) but isn't nestable — so a simple linear containment check suffices.

3. **RomanNumeralItems Detail resolves via ScaleDatabase** — Completion Detail shows the actual chord in the active key (e.g. `I` in `Cmajor` → `C (in Cmajor)`). Adds composer-facing value at minimal implementation cost (reuse of existing `ResolveRomanNumeral` helper). Falls back to a generic label if the resolver returns null; wrapped in `try/catch` defensively since ResolveRomanNumeral touches ChordParser which could throw on malformed inputs.

4. **Range type alias in DefinitionHandler.cs** — Required because `System.Range` (C# 8 range operator) collides with `OmniSharp.Extensions.LanguageServer.Protocol.Models.Range`. Same pattern as plan 17-03 `LspMappings.cs`. Position does NOT collide, so alias only on Range. Recorded in STATE.md under plan 17-03 decisions; now applied consistently across the LSP handlers.

5. **Per-request re-parse in CompletionHandler.Handle** — Chose correctness-first for v1. The alternative (cache the most recent ParseResult on DocumentManager) is a clear future optimization but adds cache-invalidation complexity that isn't justified yet.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Flow proc test fixtures used `{}` syntax, not `end proc`**
- **Found during:** First run of Task 2 Facts (`UserProc_FindUserDeclaration_ReturnsLocation`, `UserProc_AfterParse_ReturnsHover`, `NestedProcInProc_FindsInnerDecl` all failed with `Assert.NotNull()` on the declaration).
- **Issue:** The planner's test fixtures assumed `proc foo () { Int x = 5 }` would parse to a `ProcDeclaration`, but Flow's proc syntax requires `end proc`. The parser silently drops partial `proc foo () {` openings that never resolve.
- **Fix:** Updated 4 test fixtures to use `proc foo ()\n  Int x = 5\nend proc` — matches `tests/test_comments.flow:23-28` convention.
- **Files modified:** `flow-lang.Tests/Unit/Phase17/DefinitionHandlerTests.cs`, `flow-lang.Tests/Unit/Phase17/HoverHandlerTests.cs`
- **Verification:** All Facts flip from RED to GREEN on re-run.
- **Committed in:** `c8a4678`

**2. [Rule 1 - Bug] `IdentifierAt_AtWhitespace_ReturnsNull` Fact asserted wrong behavior**
- **Found during:** First run of HoverHandler Facts.
- **Issue:** The Fact assumed cursor at column 4 in `"proc foo ()"` (on the space after `proc`) would yield null, but `IdentifierAt` walks LEFT from the cursor column — starting at index 4, `line[3]='c'` is an ident char so it steps left, gathering `"proc"`. The implementation is correct (IDE convention: cursor just-after a token still selects that token); the test expectation was wrong.
- **Fix:** Replaced with `IdentifierAt_BetweenIdentifiers_ReturnsNullOrEmpty` using `"foo  bar"` at column 4 — both sides of the cursor are spaces, no identifier to collect.
- **Files modified:** `flow-lang.Tests/Unit/Phase17/HoverHandlerTests.cs`
- **Verification:** New Fact passes (null returned when neither adjacent char is an ident char).
- **Committed in:** `c8a4678`

**3. [Rule 3 - Blocking] `System.Range` vs `OmniSharp.Extensions.LanguageServer.Protocol.Models.Range` collision**
- **Found during:** First build after creating DefinitionHandler.cs.
- **Issue:** CS0104 "Range is an ambiguous reference between OmniSharp.Extensions.LanguageServer.Protocol.Models.Range and System.Range" at 2 call sites (`new Range(...)` constructor).
- **Fix:** Added `using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;` — same pattern plan 17-03 used in LspMappings.cs.
- **Files modified:** `flow-lsp/Handlers/DefinitionHandler.cs`
- **Verification:** `dotnet build flow-sharp.sln` exits 0.
- **Committed in:** `c8a4678`

**4. [Rule 2 - API refinement] BuiltInIndex.Entry field is `Signatures`, not `SignatureLines`**
- **Found during:** Planning the initial HoverHandler / SignatureHelpHandler implementations.
- **Issue:** The 17-06 plan drafted `b.SignatureLines[0]` access but the actual `BuiltInIndex.Entry` record (shipped in 17-05) has a field named `Signatures` (`IReadOnlyList<FunctionSignature>`). The plan's draft was from an earlier iteration; 17-05 made the final call.
- **Fix:** Used `b.Signatures[0].ToString()` (FunctionSignature's `ToString` gives the wire-format sig line).
- **Files modified:** `flow-lsp/Handlers/HoverHandler.cs`, `flow-lsp/Handlers/SignatureHelpHandler.cs`
- **Verification:** Facts pass; the hover content contains the signature string.
- **Committed in:** `c8a4678`

---

**Total deviations:** 4 (2 Rule 3 blocking, 1 Rule 1 test-bug, 1 Rule 2 API refinement). No Rule 4 escalations. No scope creep.

**Impact on plan:** All deviations were mechanical corrections that surfaced after first build / first test run. Core design — token-scan walker, 3-way hover lookup, comma-count signature-help — landed exactly as the plan specified.

## Constraints Confirmed

- **net10.0** — all csprojs still target net10.0.
- **No audio in flow-lsp** — no new audio/PulseAudio references added.
- **No flow-interpreter ref** — flow-lsp csproj unchanged.
- **Interpreter still green** — full test suite (236/236 Facts + 70+ .flow script tests) stays green.

## Issues Encountered

- **Flow proc syntax fixture drift** — see Deviation 1. Pattern: future phase plans authoring .flow source fixtures should cite the canonical convention (`proc ... end proc`, NOT `proc ... { ... }`) to avoid the same trap.
- **xUnit analyzer warnings (VSTHRD200, xUnit1051)** — carried forward from Phase 12–17 test files, no new ones introduced.
- **NU1903 vulnerability in Tmds.DBus.Protocol** — out of scope, surfaces from flow-editor.csproj.

## Next Phase Readiness

Plan 17-07 (VSIX packaging) can safely assume:

- 6 handlers are now registered: TextDocumentSync, SemanticTokens, Completion, Hover, SignatureHelp, Definition. Every VSCode-enabled surface (syntax coloring, completion, hover, go-to-def, signature help) lights up once the VSIX ships the LSP binary.
- `NoteStreamContext` is a pure static — no DI dependencies, no service registration. 17-07 packaging does NOT need to touch DI wiring.
- Program.cs wire-order shipped in 17-06 is stable; no handler ordering dependencies observed (all 6 handlers are request-response, fully independent).

Plan 17-08 (manual Extension Dev Host smoke) should verify:

- Hovering over `print` shows its signature + BuiltInDocs summary in a markdown tooltip.
- Hovering over a user-declared `proc myHelper` shows the kind+name tooltip.
- Ctrl+clicking on `use "@audio"` jumps to the `audio.flow` stdlib file.
- Ctrl+clicking on a user `proc foo ()` call site jumps to the `proc foo` declaration.
- Typing `(transpose seq, ` inside a proc body shows a signature-help tooltip with ActiveParameter=1.
- Typing inside `key Cmajor { | <CURSOR>` shows roman numeral completions (`I`, `ii`, `iii`, `IV`, `V`, `V7`, `vi`, `vii°`).
- Typing `|<CURSOR> ` outside any key block shows note letters + duration suffixes + `_` (rest), and does NOT show proc/variable/keyword names.
- Placing the cursor AFTER a closed `key { }` block then typing inside a new `| ... |` stream shows note letters (NOT roman numerals) — confirms the block-exit fix in live use.

## Self-Check: PASSED

Verification that all claimed artifacts exist:

- `flow-lsp/NoteStream/NoteStreamContext.cs` — FOUND
- `flow-lsp/Handlers/HoverHandler.cs` — FOUND
- `flow-lsp/Handlers/SignatureHelpHandler.cs` — FOUND
- `flow-lsp/Handlers/DefinitionHandler.cs` — FOUND
- `flow-lang.Tests/Unit/Phase17/NoteStreamContextTests.cs` — FOUND
- `flow-lang.Tests/Unit/Phase17/HoverHandlerTests.cs` — FOUND
- `flow-lang.Tests/Unit/Phase17/SignatureHelpHandlerTests.cs` — FOUND
- `flow-lang.Tests/Unit/Phase17/DefinitionHandlerTests.cs` — FOUND
- Commit `d6dcc89` — FOUND (verified via `git log --oneline`)
- Commit `c8a4678` — FOUND (verified via `git log --oneline`)
- `dotnet build flow-sharp.sln -c Debug` exits 0 — VERIFIED
- `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17"` — 96/96 Facts pass — VERIFIED
- `dotnet test flow-sharp.sln` (full suite) — 236/236 Facts pass — VERIFIED
- `grep -q "IReadOnlyList<Token>" flow-lsp/NoteStream/NoteStreamContext.cs` — PASS
- `! grep -q "new SimpleLexer" flow-lsp/NoteStream/NoteStreamContext.cs` — PASS
- `! grep -q "\.Lexeme" flow-lsp/NoteStream/NoteStreamContext.cs` — PASS
- `! grep -qE "cursor.Line >= [a-zA-Z.]+.Location.Line" flow-lsp/NoteStream/NoteStreamContext.cs` — PASS (no line-heuristic residue)
- `grep -q "result.Tokens" flow-lsp/Handlers/CompletionHandler.cs` — PASS
- `grep -q "CursorAfterClosedKeyBlock" flow-lang.Tests/Unit/Phase17/NoteStreamContextTests.cs` — PASS
- `grep -q "CursorAfterClosedKey_InSiblingStream" flow-lang.Tests/Unit/Phase17/NoteStreamContextTests.cs` — PASS
- `grep -q "WithHandler<.*HoverHandler>" flow-lsp/Program.cs` — PASS
- `grep -q "WithHandler<.*SignatureHelpHandler>" flow-lsp/Program.cs` — PASS
- `grep -q "WithHandler<.*DefinitionHandler>" flow-lsp/Program.cs` — PASS

---
*Phase: 17-flow-language-server*
*Plan: 06*
*Completed: 2026-04-20*
