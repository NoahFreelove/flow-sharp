---
phase: 41-reach-v1-5-closer
plan: 02
subsystem: language-core
tags: [lexer, parser, ast, doc-comments, flow-doc, DOC-01]

# Dependency graph
requires:
  - phase: 41-01
    provides: "DocCommentLexTests + DocCommentBindTests skip-stubs (RED→GREEN targets) + Phase41 test scaffold"
provides:
  - "`///` doc-comment lexer grammar (additive to `//`, ordered before the two-slash arm)"
  - "`TokenType.DocComment` + out-of-band DocComment token carrying captured text"
  - "`SimpleLexer.PendingDocComment` read-only side-channel accessor"
  - "`ProcDeclaration.DocComment` trailing defaulted field bound at parse time"
  - "charitable orphan-drop (a `///` not immediately followed by proc is dropped, zero errors)"
  - "`BuiltInDocs.All` public read-only accessor over the ~104 builtin entries"
affects: [41-03, flow-doc-generator, DOC-01, DOC-02]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Parse-time metadata capture onto ProcDeclaration via defaulted-trailing field (mirrors IsStrict/IsBeatTrueToSig)"
    - "Out-of-band token + parser-side pending buffer with consume-and-clear at the binding declaration"
    - "Multi-char-before-single-char lexer arm ordering (/// before //)"

key-files:
  created: []
  modified:
    - flow-lang/Lexing/TokenType.cs
    - flow-lang/Lexing/SimpleLexer.cs
    - flow-lang/Ast/Statements/ProcDeclaration.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang/StandardLibrary/BuiltInDocs.cs
    - flow-lang.Tests/Integration/Phase41/DocCommentLexTests.cs
    - flow-lang.Tests/Integration/Phase41/DocCommentBindTests.cs

key-decisions:
  - "Chose the token-based binding (TokenType.DocComment) over a pure lexer side-channel: the Parser only receives List<Token>, never the lexer, so an in-stream token is the clean integration seam. The lexer also exposes PendingDocComment for the lex contract test."
  - "Contiguous `///` lines accumulate inside a single SkipWhitespaceAndComments call (its while-loop walks intervening newline whitespace), so the Tokenize loop flushes exactly ONE DocComment token per block at the first line's source location."
  - "Consume + clear _pendingDocComment at ParseProcDeclaration ENTRY (before body parse) so a `///` inside the body or a following proc can never re-read it (Pitfall 2 leak guard)."

patterns-established:
  - "Doc-comment attachment: lexer captures /// → DocComment token → Parser buffers in _pendingDocComment → ParseProcDeclaration threads + clears."

requirements-completed: [DOC-01]

# Metrics
duration: 25min
completed: 2026-06-07
---

# Phase 41 Plan 02: `///` Doc-Comment Language-Core Grammar Summary

**`///` doc-comments now lex (additive to `//`, ordered before the two-slash arm), bind to the following `ProcDeclaration.DocComment` with charitable orphan-drop, and `BuiltInDocs.All` exposes the ~104 builtin entries — the load-bearing language-core infrastructure DOC-01's 41-03 generator reads.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-06-07T23:30Z (approx)
- **Completed:** 2026-06-07T23:56Z
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments
- `///` is a first-class doc-comment: captured (leading `///` + one space stripped, contiguous lines `\n`-joined), bound to the following proc, and runnable end-to-end (`(dbl 21)` script prints `42`).
- `//` line comments and `/* */` (which Flow lexes as Slash/Star tokens) stay byte-for-byte unchanged — the additive-grammar invariant holds (`test_comments.flow` green; 40 existing lexer tests green).
- Charitable interpretation honored: a proc with no `///` gets `DocComment == null` (signature-only); an orphaned `///` (non-proc statement between, or trailing at EOF) is dropped silently with zero `ErrorReporter` errors.
- `BuiltInDocs.All` read-only accessor added (`_docs` stays private) so the 41-03 generator reads builtin metadata directly with no duplication (D-08).
- The two 41-01 skip-stubs (`DocCommentLexTests`, `DocCommentBindTests`) are now LIVE with real assertions: 11 DocComment tests GREEN.
- Desktop AND Web builds both clean — the `///` change is platform-agnostic, no Web-strip regression.

## Task Commits

Each task was committed atomically (TDD: test + impl landed together per task as one cohesive `feat` commit, since the test and the symbols it references are inseparable at compile time):

1. **Task 1: Lex `///` as a doc-comment** — `c495f0a` (feat) — `TokenType.DocComment` + `SimpleLexer` `///` branch before `//` + `PendingDocComment` accessor; `DocCommentLexTests` 4/4 GREEN.
2. **Task 2: Add `ProcDeclaration.DocComment` + thread through Parser** — `0e95db0` (feat) — defaulted-trailing field + `_pendingDocComment` buffer + consume/clear + charitable orphan-drop; `DocCommentBindTests` 7/7 GREEN.
3. **Task 3: Expose `BuiltInDocs.All`** — `78f54a3` (feat) — public read-only accessor; `_docs` private; `TryGet` unchanged.

_Note: RED was verified per TDD before each GREEN — the test files referenced symbols (`TokenType.DocComment`, `PendingDocComment`, `ProcDeclaration.DocComment`) that did not compile until the implementation landed._

## Files Created/Modified
- `flow-lang/Lexing/TokenType.cs` — added `DocComment` enum member.
- `flow-lang/Lexing/SimpleLexer.cs` — `///` capture branch (before `//`), `_docCommentBuilder`/`_docCommentStart`/`_docCommentPending` state, `PendingDocComment` accessor, Tokenize-loop flush emitting one DocComment token per block.
- `flow-lang/Ast/Statements/ProcDeclaration.cs` — trailing `string? DocComment = null` + XML-doc rationale mirroring IsStrict/IsBeatTrueToSig.
- `flow-lang/Parsing/Parser.cs` — `_pendingDocComment` field, DocComment-token buffering in `ParseStatement`, charitable orphan-clear, consume+clear+thread at `ParseProcDeclaration`.
- `flow-lang/StandardLibrary/BuiltInDocs.cs` — `public static IReadOnlyDictionary<string, Doc> All => _docs;`.
- `flow-lang.Tests/Integration/Phase41/DocCommentLexTests.cs` — 4 live lexer-contract assertions (replaced 3 skip-stubs; added MultipleTripleSlash_Concatenate).
- `flow-lang.Tests/Integration/Phase41/DocCommentBindTests.cs` — 7 live binding-contract assertions (replaced 2 skip-stubs; added multi-line/internal/no-doc/trailing-orphan/no-leak cases).

## Decisions Made
- **Token-based binding over pure side-channel.** The Parser is constructed from `List<Token>` and never holds a lexer reference, so a lexer-only `_pendingDocComment` field could not reach it. An out-of-band `DocComment` token (filtered exactly like the existing dead `TokenType.Comment`) is the clean seam; the lexer additionally exposes `PendingDocComment` purely for the lex contract test. This satisfies both RESEARCH options (a)+(b) at once.
- **Flush one token per contiguous block at the first line's location.** Contiguous `///` lines are all consumed within one `SkipWhitespaceAndComments` call, so the Tokenize loop flushes the accumulated `\n`-joined text as a single token after that call returns.
- **Consume + clear at ParseProcDeclaration entry, before body parse.** Guarantees a `///` inside the body or a following proc cannot re-read this proc's doc-comment (Pitfall 2 leak).

## Deviations from Plan

None — plan executed exactly as written. No bugs, missing-critical functionality, blocking issues, or architectural changes encountered.

The plan left the lexer binding mechanism to executor discretion ("the simpler path … is a side-channel `_pendingDocComment` the Parser reads"); the token-based seam was chosen because the Parser has no lexer handle (documented under Decisions, not a deviation — both approaches were sanctioned by the plan/RESEARCH).

## Issues Encountered
- **Test body-shape correction (not a deviation):** the first `DocCommentBindTests` draft used `proc one() (print "a") end` (empty params + same-line paren body), which hits a PRE-EXISTING Flow grammar quirk — an empty-param-list proc whose body starts on the same line with `(` mis-parses (`Unexpected token RParen`). Verified the error reproduces WITHOUT any `///` (confirming it is not a regression from this plan). Rewrote the test fixtures to the well-formed multi-line `proc name (Int: x)\n    (body)\nend proc` shape used by real stdlib/test `.flow` files. All 7 bind tests then GREEN. The `///` lexer/parser change itself is unaffected by this orthogonal grammar limitation.

## Verification
- `dotnet test --filter "FullyQualifiedName~DocComment"` → **11 passed, 0 failed, 0 skipped**.
- `dotnet build flow-lang -p:FlowTarget=Desktop` → Build succeeded (0 errors).
- `dotnet build flow-lang -p:FlowTarget=Web` → Build succeeded (0 errors) — no Web-strip regression.
- Existing lexer suite (40), parser/module/Phase44/Phase45 (387), broad lexer/parser/proc (185) → all 0 failed.
- `tests/test_comments.flow` → "All comment tests passed" (no `//`/`Note:` regression).
- End-to-end smoke: a multi-line-`///`-documented `proc dbl` script runs and prints `42`.

## User Setup Required
None — no external service configuration required.

## Next Phase Readiness
- **41-03 (flow doc generator) unblocked:** `ProcDeclaration.DocComment` is populated at parse time for every proc the generator walks; `BuiltInDocs.All` enumerates the ~104 builtin entries; the lexer's additive `///` grammar means stdlib/composer `.flow` procs can carry `///` doc-comments without disturbing existing scripts.
- No blockers. The 6 remaining Phase41 skip-stubs (DocCache, DocExampleExec, FlowDocGen, Phase41ShowcaseRms, Wasapi/CoreAudio availability) are the 41-03+ generator/backend targets, untouched by this plan.

## Self-Check: PASSED

- FOUND: `.planning/phases/41-reach-v1-5-closer/41-02-SUMMARY.md`
- FOUND: commit `c495f0a` (Task 1 — lexer `///`)
- FOUND: commit `0e95db0` (Task 2 — ProcDeclaration.DocComment + Parser)
- FOUND: commit `78f54a3` (Task 3 — BuiltInDocs.All)

---
*Phase: 41-reach-v1-5-closer*
*Completed: 2026-06-07*
