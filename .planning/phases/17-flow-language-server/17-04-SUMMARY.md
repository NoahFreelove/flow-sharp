---
phase: 17-flow-language-server
plan: 04
subsystem: lsp
tags: [lsp, semantic-tokens, highlighting, omnisharp, textmate-overlay]

# Dependency graph
requires:
  - phase: 17-flow-language-server
    plan: 01
    provides: "ParseSession.Parse returning ParseResult(Ast, Tokens, Errors) + OmniSharp 0.19.9 Wave 0 gate clearance"
  - phase: 17-flow-language-server
    plan: 03
    provides: "DocumentManager.GetText + OmniSharp handler-registration pattern (.WithHandler<T>())"
provides:
  - "SemanticTokensEncoder: pure static TokenType → LegendIndex map + 5-tuple delta encoder"
  - "SemanticTokensHandler: thin OmniSharp SemanticTokensHandlerBase subclass that pushes mapped tokens into SemanticTokensBuilder"
  - "flow-lsp exposes Semantic/ subfolder for transport-free token logic — reusable pattern for future ordering-sensitive encoders"
affects: [17-05, 17-06, 17-07, 17-08]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pure encoder + thin handler split: encoding unit under flow-lsp/Semantic/ depends only on FlowLang.Lexing + OmniSharp Protocol.Models (value enums); transport-bound handler under flow-lsp/Handlers/ depends on Protocol.Document + Protocol.Client.Capabilities. Keeps golden-file xUnit Facts transport-free."
    - "Legend-via-enum-index: private LegendIndex enum parallels the public SemanticTokenType[] Legend array, keeping numeric indices readable in the switch without exposing them as public API."
    - "Skip-unmapped-tokens contract: MapTokenType returns int? (nullable); EncodeTokens skips null-mapped tokens via `continue`, explicitly documented as preserving delta-encoding origin for subsequent mapped tokens."

key-files:
  created:
    - "flow-lsp/Semantic/SemanticTokensEncoder.cs"
    - "flow-lsp/Handlers/SemanticTokensHandler.cs"
    - "flow-lang.Tests/Unit/Phase17/SemanticTokensTests.cs"
  modified:
    - "flow-lsp/Program.cs (added .WithHandler<SemanticTokensHandler>() after TextDocumentSyncHandler)"

key-decisions:
  - "Encoder lives at flow-lsp/Semantic/SemanticTokensEncoder.cs, handler at flow-lsp/Handlers/SemanticTokensHandler.cs — matches 17-VALIDATION.md task row and the success_criteria split, not the plan's co-located example"
  - "Encoder imports Protocol.Models for SemanticTokenType value enums (vocabulary constants, not transport types). Zero handler/Document/Client.Capabilities deps — tests can exercise encoding without booting LSP."
  - "SemanticTokensBuilder.Push(int, int, int, SemanticTokenType?, SemanticTokenModifier[]) overload chosen over the null-param form because `null` is ambiguous between the SemanticTokenType?/SemanticTokenModifier[] and string/string[] overloads in 0.19.9. Empty SemanticTokenModifier[] disambiguates."
  - "Full=true with Delta=false in RegistrationOptions — delta support deferred per RESEARCH §Pitfalls; full response is cheap for typical Flow file sizes."

patterns-established:
  - "Pure-encoder + thin-handler split: when a feature requires deterministic unit-testable output (int[], JSON shapes), keep the algorithm in a transport-free helper and put the OmniSharp base-class glue in a separate wrapper. 17-05/17-06 completion+hover handlers can reuse this shape if Fact-pinned output helps regression testing."
  - "Golden-file Theory rows for LSP int[] encodings: SemanticTokensTests.GoldenFixtures MemberData pins exact int[] outputs for representative Flow snippets. Future binary-protocol encoders (e.g., MIDI event streams, WAV headers) can follow the same shape."
  - "Enum-index legend: private LegendIndex : int enum + public static SemanticTokenType[] Legend keeps `(int)LegendIndex.Keyword` readable in the switch while the Legend array drives what the client sees."

requirements-completed: [D-04, D-05]

# Metrics
duration: ~20min
completed: 2026-04-20
---

# Phase 17 Plan 04: SemanticTokensEncoder + Handler (standard LSP scopes) Summary

**LSP semantic-tokens handler ported from FlowSyntaxHighlighter's token-category switch to standard LSP SemanticTokenType indices (Keyword/Type/Number/String/Operator/Comment/Variable/Function/Macro) with pure int[] encoder separated from transport-bound handler — 17 xUnit Facts + 3 golden Theory rows green, 40/40 Phase 17 green overall.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-04-20T22:26:00Z (approx.)
- **Completed:** 2026-04-20T22:48:00Z
- **Tasks:** 1 (atomic commit per task_commit_protocol)
- **Files created:** 3 (2 production + 1 test)
- **Files modified:** 1 (flow-lsp/Program.cs)
- **Tests added:** 17 xUnit Facts (12 MapTokenType + 5 EncodeTokens) + 3 golden Theory rows — all green

## Accomplishments

- `SemanticTokensEncoder.MapTokenType(TokenType) -> int?` classifies 48 `TokenType` enum values across 9 legend indices (Keyword/Type/String/Number/Operator/Comment/Variable/Function/Macro), mapping every `TokenType` that `FlowSyntaxHighlighter` colors in flow-editor.
- `SemanticTokensEncoder.EncodeTokens(IReadOnlyList<Token>) -> int[]` produces LSP 3.17-compliant 5-tuple delta encoding: first token uses absolute `(line, col)`, same-line tokens use `(0, col - prevCol)`, cross-line tokens use `(dLine, absoluteCol)`.
- `SemanticTokensHandler` subclasses `OmniSharp.Extensions.LanguageServer.Protocol.Document.SemanticTokensHandlerBase` with the confirmed 0.19.9 override triple: `Tokenize(SemanticTokensBuilder, ITextDocumentIdentifierParams, CancellationToken)`, `GetSemanticTokensDocument(...)`, and `CreateRegistrationOptions(SemanticTokensCapability, ClientCapabilities)`.
- `Program.cs` registers the handler via `.WithHandler<SemanticTokensHandler>()` after the existing `TextDocumentSyncHandler`.
- 17 xUnit Facts + 3 golden Theory rows pin: empty buffer → empty int[], single-keyword 5-tuple, same-line column offset delta, cross-line absolute column delta, `xyz` identifier → skipped (empty int[]), `proc xyz return` → skipped-between-mapped preserves delta origin, `NoteLiteral → Variable`, `ChordLiteral → Function`, `Pipe → Macro`, music-context keywords (Tempo/Key/Timesig/Swing/Dynamics/Rit/Accel/Pickup/Section) → Keyword, type keywords (Void/Int/Float/Long/Double/String/Bool/Number/Note/Buf) → Type, `Eof → null`, `Identifier → null`.
- D-04 hybrid overlay and D-05 standard-scopes-only gates both satisfied — negative gates pass: `! grep flow\\.semantic|flow\\.token` and `! grep SemanticTokenType\\.(flow|music)` both empty.

## Task Commits

1. **Task 1: SemanticTokensEncoder + SemanticTokensHandler + Program.cs wire-up + SemanticTokensTests** — `5d010d7` (feat)

## Files Created/Modified

### Created
- `flow-lsp/Semantic/SemanticTokensEncoder.cs` — pure static class; `Legend: SemanticTokenType[]`, `ModifierLegend: SemanticTokenModifier[]` (empty v1), `MapTokenType(TokenType) -> int?`, `EncodeTokens(IReadOnlyList<Token>) -> int[]`. Only depends on `FlowLang.Lexing` + `OmniSharp.Extensions.LanguageServer.Protocol.Models` (value enums). Zero transport-level dependencies.
- `flow-lsp/Handlers/SemanticTokensHandler.cs` — thin wrapper. DI-injected `DocumentManager` + `ParseSession`; `Tokenize` fetches buffer text, re-parses, Pushes each mapped token into `SemanticTokensBuilder` using `(line, col, length, SemanticTokenType, System.Array.Empty<SemanticTokenModifier>())` overload. `CreateRegistrationOptions` wires the Legend and sets `Full = new SemanticTokensCapabilityRequestFull { Delta = false }`, `Range = false`.
- `flow-lang.Tests/Unit/Phase17/SemanticTokensTests.cs` — 17 Facts + 3 Theory rows via `LspFixtures.Parse`.

### Modified
- `flow-lsp/Program.cs` — one-line addition: `.WithHandler<SemanticTokensHandler>()` appended after `.WithHandler<TextDocumentSyncHandler>()`. DI resolves `DocumentManager` + `ParseSession` automatically (already registered in plan 17-03).

## OmniSharp 0.19.9 API Signatures Confirmed

Resolved via reflection-probe against
`/home/noah/.nuget/packages/omnisharp.extensions.languageprotocol/0.19.9/lib/net6.0/OmniSharp.Extensions.LanguageProtocol.dll`:

- **Base class:** `OmniSharp.Extensions.LanguageServer.Protocol.Document.SemanticTokensHandlerBase` (abstract).
- **Abstract overrides required:**
  - `Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier, CancellationToken cancellationToken): Task`
  - `GetSemanticTokensDocument(ITextDocumentIdentifierParams @params, CancellationToken cancellationToken): Task<SemanticTokensDocument>`
- **Virtual override:** `CreateRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities): SemanticTokensRegistrationOptions`
- **SemanticTokensCapability namespace:** `OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities` (the plan's approximate sample placed it under Protocol — real location is `Client.Capabilities`, mirroring 17-03's `TextSynchronizationCapability` discovery).
- **SemanticTokensBuilder.Push overloads:** ten total — the (int, int, int, SemanticTokenType?, SemanticTokenModifier[]) and (int, int, int, string, string[]) overloads both accept `null` as the final argument, causing a CS0121 ambiguity when called with bare `null`. Resolved with `System.Array.Empty<SemanticTokenModifier>()` to force the typed overload. Noted for 17-05/17-06 if they Push anything.
- **SemanticTokensDocument constructor:** `new SemanticTokensDocument(SemanticTokensLegend legend)` — the simpler of the two ctors. Base class uses this document as a caching seam for delta responses; we return a fresh one each call since `Delta = false`.
- **SemanticTokensCapabilityRequestFull:** `new() { Delta = false }` is the required shape for `SemanticTokensRegistrationOptions.Full` (a tri-state: bool? for "supported" + explicit Delta flag when an object).

## Constraints Confirmed

- **net10.0** — all 6 csprojs still target net10.0. Build + test run green.
- **D-04 hybrid** — this handler intentionally layers on top of plan 17-02's TextMate grammar. No changes to the grammar; VSCode merges via precedence (semantic tokens win where they overlap). Will be visually verified in 17-07 / 17-08 manual smoke test.
- **D-05 standard scopes only** — Legend contains exactly 9 entries, all `SemanticTokenType` static properties (`Keyword, Type, String, Number, Operator, Comment, Variable, Function, Macro`). No `flow.*` or `music.*` strings emitted. Verified via negative grep acceptance gates.
- **Pure encoder** — `flow-lsp/Semantic/SemanticTokensEncoder.cs` imports only `FlowLang.Lexing` + `OmniSharp.Extensions.LanguageServer.Protocol.Models`. No `Protocol.Document`, `Protocol.Server`, `Protocol.Client`, or `MediatR` — the three namespaces a transport-bound file typically pulls in.
- **No invented scopes** — `! grep -qE "flow\\.semantic|flow\\.token" flow-lsp/Handlers/SemanticTokensHandler.cs flow-lsp/Semantic/SemanticTokensEncoder.cs` passes. `! grep -qE "SemanticTokenType\\.(flow|music)" ...` passes.

## Decisions Made

1. **Encoder + handler file split** — Success criteria and 17-VALIDATION.md row 17-04-T1 both list the encoder at `flow-lsp/Semantic/SemanticTokensEncoder.cs` and handler at `flow-lsp/Handlers/SemanticTokensHandler.cs`. The PLAN's `<action>` snippet co-located everything in the handler for brevity; I honored the explicit two-file split because it makes the "pure encoder" contract enforceable (the encoder file has no transport deps at all).
2. **`Full = new SemanticTokensCapabilityRequestFull { Delta = false }`** — versus `Full = true`. The 0.19.9 `SemanticTokensRegistrationOptions.Full` property is a tri-state (`BooleanOr<SemanticTokensCapabilityRequestFull>`); using the explicit object form makes the "no delta support" stance unambiguous and documents intent for 17-05 readers. Equivalent behavior to `Full = true` for now; easier to flip Delta to true later without changing the shape.
3. **`SemanticTokensDocument(RegistrationOptions.Legend)` over `SemanticTokensDocument(RegistrationOptions)`** — both constructors exist. Passing just the legend avoids needing `RegistrationOptions` to be non-null at GetSemanticTokensDocument time (the base class initializes it lazily). Matches OmniSharp's sample server pattern.
4. **Empty `SemanticTokenModifier[]` over `null` at Push** — see Deviations #1 below for the CS0121 rationale.
5. **Theory `GoldenFixtures` pins deliberate absolute-coordinate cases** — `"proc Int"` (same-line, second column offset = 5), `"proc\nreturn"` (cross-line, second absolute column = 0). Chose fixtures where `Token.Text.Length` is unambiguous (`proc`=4, `Int`=3, `return`=6) so the int[] assertions read as spec documentation.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `SemanticTokensBuilder.Push` overload ambiguity (CS0121) when final arg is `null`**
- **Found during:** Task 1 first build after handler file landed
- **Issue:** The plan's sketch used `builder.Push(line, col, t.Text.Length, Legend[idx.Value], null)` for the final modifiers arg. Under OmniSharp 0.19.9 the builder has ten `Push` overloads; two accept a final-arg `null`: `(int, int, int, SemanticTokenType?, SemanticTokenModifier[])` and `(int, int, int, string, string[])`. `null` is convertible to both `SemanticTokenModifier[]` and `string[]`, triggering CS0121 "ambiguous between the following methods."
- **Fix:** Replaced `null` with `System.Array.Empty<SemanticTokenModifier>()` to force the typed overload. Functionally identical (both mean "no modifiers") but compiler-unambiguous.
- **Files modified:** `flow-lsp/Handlers/SemanticTokensHandler.cs`
- **Verification:** `dotnet build flow-sharp.sln -c Debug` exits 0. No other ambiguity diagnostics.
- **Committed in:** `5d010d7` (Task 1 commit — bundled with the handler's initial authorship)

**2. [Rule 2 - Strengthening Coverage] Added `EncodeTokens_SkipBetweenMapped_PreservesDeltaMath` Fact**
- **Found during:** Test authorship for Task 1 (pre-GREEN test suite review)
- **Issue:** The plan drafted 8 Facts. Fact #5 (`EncodeTokens_SkipsUnmappedTokens`) covers the trivial case where ALL tokens are unmapped (empty result). It does NOT discriminate the subtler invariant: when an unmapped token sits BETWEEN two mapped tokens, the second mapped token's delta must still be measured from the first mapped token (not the skipped identifier). A naive implementation that updates `prevLine/prevCol` even for skipped tokens would pass Fact #5 but fail in real .flow source.
- **Fix:** Added `EncodeTokens_SkipBetweenMapped_PreservesDeltaMath` using source `"proc xyz return"` (Proc + Identifier + Return). Asserts the encoded int[] is 10 ints (only Proc + Return) AND the second tuple's deltas are `(0, retCol - procCol)` — i.e., measured from Proc, not from xyz. This is the real regression gate for the skip-preserves-origin contract documented in `EncodeTokens`'s XML doc.
- **Files modified:** `flow-lang.Tests/Unit/Phase17/SemanticTokensTests.cs`
- **Verification:** Fact green on first run under the implemented encoder (which uses `first` boolean + always updates `prevLine/prevCol` only after a successful `continue`-bypassing mapping). Discrimination spot-check NOT performed (low-risk addition, straight-line code path).
- **Committed in:** `5d010d7`
- **Precedent:** This mirrors the 17-03 `ParseCompletingConcurrentlyWithClose_DoesNotPublishWhenGuarded` Fact addition (real regression gate beyond the plan's minimum coverage). Same Rule 2 rationale.

**3. [Rule 2 - Strengthening Coverage] Added 3 golden Theory rows (`GoldenFixtures`)**
- **Found during:** Task 1 test authorship
- **Issue:** 17-VALIDATION.md row 17-04-T1 calls for "L1 + L2 (fixture int[] arrays pinned)" and the success_criteria says "At least 3 golden Theory Facts pin the encoding for representative snippets." The plan's drafted Facts were all L1-shape (field-by-field int[] assertions via `encoded[0..4]`). Without full int[] Theory rows, a refactor that silently changed the 5-tuple ordering or padding would slip through.
- **Fix:** Added `[Theory] [MemberData(nameof(GoldenFixtures))]` with 3 rows — `"proc"`, `"proc Int"`, `"proc\nreturn"` — pinning the exact int[] output. Each row tests a distinct dimension: single-token baseline, same-line delta, cross-line delta.
- **Files modified:** `flow-lang.Tests/Unit/Phase17/SemanticTokensTests.cs`
- **Verification:** All 3 Theory rows green on first run.
- **Committed in:** `5d010d7`

**4. [Rule 2 - Location Refinement] Encoder placed at `flow-lsp/Semantic/SemanticTokensEncoder.cs`, not co-located in the handler file**
- **Found during:** Task 1 scaffolding decision
- **Issue:** The PLAN `<action>` step 1 drafts both encoder methods (`MapTokenType`, `EncodeTokens`) as static members inside `SemanticTokensHandler.cs`. Success criteria and 17-VALIDATION.md row 17-04-T1 both list the encoder at `flow-lsp/Semantic/SemanticTokensEncoder.cs` as a separate file. Co-locating would mean the "pure encoder" file contract (zero transport deps) cannot be enforced structurally.
- **Fix:** Separated into two files. Encoder (`SemanticTokensEncoder.cs`) lives under `flow-lsp/Semantic/` and depends only on `FlowLang.Lexing` + `OmniSharp...Protocol.Models`. Handler (`SemanticTokensHandler.cs`) lives under `flow-lsp/Handlers/` (alongside `TextDocumentSyncHandler.cs`, `DiagnosticsPublisher.cs`) and holds all transport glue.
- **Files modified:** Both files created in this split form from the start.
- **Verification:** `grep -E "Protocol\\.(Document|Client|Server)" flow-lsp/Semantic/SemanticTokensEncoder.cs` empty. Grep for all `OmniSharp.Extensions.*` imports in encoder shows only `Protocol.Models`.
- **Committed in:** `5d010d7`

---

**Total deviations:** 4 (1 Rule 3 blocking, 3 Rule 2 coverage/refinement). No Rule 4 escalations.

**Impact on plan:** All deviations necessary or strictly additive. No scope creep — 17 Facts + 3 Theory rows match or exceed the plan's "≥6 Facts" bar; the Rule 3 fix was compiler-forced; the Rule 2 file-split makes the success_criteria's "encoder is PURE" check objectively pass via `grep`.

## Issues Encountered

- **xUnit analyzer warnings (VSTHRD200, xUnit1051)** — carried forward from 17-03 test files. Not introduced by this plan; the new `SemanticTokensTests.cs` file itself has zero analyzer hits. Out of scope per SCOPE BOUNDARY deviation rule.
- **NU1903 high-severity vulnerability in `Tmds.DBus.Protocol` 0.21.2** — surfaced from `flow-editor.csproj`; unchanged by this plan. Out of scope (not caused by the current task's changes).
- **Identifier edge case** — `"xyz"` lexes to `TokenType.Identifier` as the plan anticipated, confirmed via ad-hoc lexer probe: `Identifier 'xyz' @ L1C1, Eof @ L1C4`. No adjustment needed to Fact #5; the assertion holds empirically.

## OmniSharp Handler Registration Count

Per plan verification requirement — Program.cs now registers **2 handlers** (matches the target):

1. `TextDocumentSyncHandler` (plan 17-03)
2. `SemanticTokensHandler` (plan 17-04 — this plan)

## Next Phase Readiness

Plan 17-05 (UserSymbolIndex + CompletionHandler) can safely assume:
- Handler-registration pattern `.WithHandler<T>()` is proven 2x; DI-injected `DocumentManager` + `ParseSession` pattern works.
- `SemanticTokensEncoder.Legend` public readonly array is stable API — if 17-05 adds completion-item annotations that reference semantic tokens, it can read the same legend.
- Pure-helper + thin-wrapper split is a reusable pattern for any handler whose output is deterministic (completion item list, hover markdown, etc.).

Plan 17-06 (HoverHandler + DefinitionHandler) can safely assume:
- `OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities.*Capability` is the canonical namespace for capability parameters (confirmed with `SemanticTokensCapability`, previously with `TextSynchronizationCapability`). 17-06 can grep for `Hover*Capability` / `Definition*Capability` in the same namespace.
- `SemanticTokensBuilder.Push` overload ambiguity at `null` — if 17-06 handlers emit `Command[]` or `MarkupContent` with optional modifier-like lists, prefer typed empty arrays over `null`.

Plan 17-07 (VSIX packaging) can ship the LSP binary knowing:
- Semantic tokens respond on `textDocument/semanticTokens/full` with stock LSP scopes — any VSCode theme that supports semantic tokens (2020+) will color Flow files correctly without a bundled theme.
- TextMate grammar + semantic tokens overlap harmoniously (D-04 hybrid); the brief overlay flicker during server-spawn is acceptable per 17-VALIDATION.md §100.

Plan 17-08 (manual Extension Dev Host smoke) should verify visually:
- Opening `tests/test_note_streams.flow` in Dev Host shows chord literals (`Cmaj7`, `Dm`) in the theme's **Function** color (distinct from the **Variable** color applied to bare note literals like `C4`, `D4`).
- Pipe delimiters (`|`) render in the theme's **Macro** color.
- Comments (`//`, `/* */`) render in the theme's **Comment** color (both TM grammar AND semantic tokens agree — overlap region).
- `key { ... }` block: the `key` keyword shows theme **Keyword** color; roman numerals inside (`I`, `IV`, `V7`) do NOT get semantic colors in v1 (plan 17-04 scope) — they will get completion suggestions from 17-05's note-stream completion (D-11).

## Self-Check: PASSED

Verification that all claimed artifacts exist:

- `flow-lsp/Semantic/SemanticTokensEncoder.cs` — FOUND
- `flow-lsp/Handlers/SemanticTokensHandler.cs` — FOUND
- `flow-lang.Tests/Unit/Phase17/SemanticTokensTests.cs` — FOUND
- `flow-lsp/Program.cs` (modified — `.WithHandler<SemanticTokensHandler>()` added) — FOUND
- Commit `5d010d7` — FOUND (verified via `git log --oneline -2`)
- `dotnet build flow-sharp.sln -c Debug` exits 0 — VERIFIED
- `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17.SemanticTokensTests"` — 17/17 Facts pass — VERIFIED
- `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17"` — 40/40 Facts pass (23 prior + 17 new) — VERIFIED
- `grep -q "SemanticTokenType.Keyword" flow-lsp/Semantic/SemanticTokensEncoder.cs` — PASS
- `grep -q "SemanticTokenType.Variable" flow-lsp/Semantic/SemanticTokensEncoder.cs` — PASS
- `grep -q "SemanticTokenType.Function" flow-lsp/Semantic/SemanticTokensEncoder.cs` — PASS
- `grep -q "public static int\[\] EncodeTokens" flow-lsp/Semantic/SemanticTokensEncoder.cs` — PASS
- `grep -q "public static int? MapTokenType" flow-lsp/Semantic/SemanticTokensEncoder.cs` — PASS
- `grep -q "WithHandler<SemanticTokensHandler>" flow-lsp/Program.cs` — PASS
- `! grep -qE "flow\.semantic|flow\.token" flow-lsp/Handlers/SemanticTokensHandler.cs flow-lsp/Semantic/SemanticTokensEncoder.cs` — PASS (no invented scopes)
- `! grep -qE "SemanticTokenType\\.(flow|music)" flow-lsp/Handlers/SemanticTokensHandler.cs flow-lsp/Semantic/SemanticTokensEncoder.cs` — PASS (no invented enums)

---
*Phase: 17-flow-language-server*
*Plan: 04*
*Completed: 2026-04-20*
