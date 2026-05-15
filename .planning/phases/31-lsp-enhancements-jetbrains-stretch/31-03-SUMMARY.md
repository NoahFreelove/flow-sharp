---
phase: 31-lsp-enhancements-jetbrains-stretch
plan: 03
subsystem: lsp
tags: [lexer, comments, lisp-style, todo, fixme, position-sensitive, byte-identical, determinism, spec-4]

# Dependency graph
requires:
  - phase: 16
    provides: existing `Note:` line-comment arm at SimpleLexer.cs:1144 + `IsStartOfLineContent()` helper at SimpleLexer.cs:1159 — the exact template the three new arms mirror
  - phase: 21
    provides: PragmaScanner / `enable hAsB;` pragma syntax that the Option A regression canary protects (Phase21.PragmaScannerFacts as the gate)
  - phase: 26
    provides: lexer-direct test pattern (`flow-lang.Tests/Unit/Phase26/NegativeLiteralLexFacts.cs`) — `new SimpleLexer(src, er, fileName: null, pragmaSet: PragmaSet.Empty).Tokenize()` shape
  - phase: 31-01
    provides: 31-DECISIONS.md D-11 Option A lock — the planning-phase decision that this plan implements
provides:
  - "SimpleLexer recognizes `;` at column-0 (with optional leading whitespace) as a Lisp-style line comment to end-of-line; mid-line `;` stays TokenType.Semicolon"
  - "SimpleLexer recognizes `TODO:` at column-0 as a lead-in line comment"
  - "SimpleLexer recognizes `FIXME:` at column-0 as a lead-in line comment"
  - "8 Phase31LexerCommentFormsTests [Fact] tests pin the new behavior + the D-11 Option A regression canary (`enable hAsB;` keeps its Semicolon)"
  - "Two-run byte-identical determinism contract preserved by construction — zero token-stream change for any valid existing program (verified: 20/20 ByteIdentical tests GREEN)"
affects: [31-06, 31-07]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Position-sensitive lexer arm: `c == 'X' && IsStartOfLineContent() [&& StartsWith(\"PREFIX:\")]` — three new arms follow the existing `Note:` template at SimpleLexer.cs:1144 verbatim"
    - "D-11 Option A: position-sensitive `;` Lisp-style comment vs. mid-line statement-terminator — composer writes either form unambiguously, no breaking change for any shipped pragma or typed declaration"

key-files:
  created:
    - flow-lang.Tests/Unit/Phase31/Phase31LexerCommentFormsTests.cs
  modified:
    - flow-lang/Lexing/SimpleLexer.cs

key-decisions:
  - "D-11 Option A position-sensitive `;` implemented exactly as the planning-phase lock specified — the `IsStartOfLineContent()` gate (already shipped for the `Note:` arm) does all the work. Mid-line `;` continues to lex as TokenType.Semicolon via the unchanged default path."
  - "RED→GREEN TDD discipline: 8 tests written first (4 RED failing for the new arms, 4 already GREEN for the canary behaviors), then arms added in a separate `feat` commit. Two atomic commits: `fdd1b5e` (test) and `dd81c87` (feat)."
  - "Used `_source.Substring(_position).StartsWith(...)` for the `TODO:`/`FIXME:` lookahead to match the existing `Note:` arm exactly (no allocation-style drift mid-file)."
  - "Source-code citation: the new `;` arm carries a 6-line comment block citing D-11 Option A + the RESEARCH Migration Audit zero-hit finding + the Phase 18/25/27/28 ByteIdentical contract — composer-grep-ability for the decision trail."

patterns-established:
  - "Lexer arm insertion at SkipWhitespaceAndComments: insert AFTER the existing `Note:` arm and BEFORE the `else break;` fall-through. New arms inherit the position-sensitive gate by construction."
  - "TDD for lexer changes: write [Fact] tests against `new SimpleLexer(...).Tokenize()` directly (no parser/engine), confirm RED, commit, then add arms, confirm GREEN, commit. Follows the Phase 26 NegativeLiteralLexFacts precedent."

requirements-completed: [SPEC-4]

# Metrics
duration: ~5min
completed: 2026-05-12
---

# Phase 31 Plan 03: SPEC-4 Lexer Comment Arms Summary

**Three position-sensitive line-comment forms added to SimpleLexer.SkipWhitespaceAndComments — `;` (Lisp-style), `TODO:`, and `FIXME:` — each gated by `IsStartOfLineContent()` mirroring the existing `Note:` arm verbatim. D-11 Option A preserves mid-line `;` as TokenType.Semicolon, so every shipped pragma (`enable hAsB;`) and typed declaration (`Int x = 5;`) keeps its lex behavior — zero token-stream change for any valid existing program.**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-05-12T22:46:46Z
- **Completed:** 2026-05-12T22:51:10Z
- **Tasks:** 1 / 1
- **Files modified:** 2 (1 created, 1 modified)

## Accomplishments

- **3 new lexer arms** added to `flow-lang/Lexing/SimpleLexer.cs::SkipWhitespaceAndComments`, each a 7-line consume-to-newline block gated by `IsStartOfLineContent()`. Total: +30 insertions, zero deletions, zero existing-arm modifications.
- **8 `[Fact]` tests** in `flow-lang.Tests/Unit/Phase31/Phase31LexerCommentFormsTests.cs` pinning every behavior in the plan's `<behavior>` block:
  1. `Semicolon_AtColumn0_IsLineComment` — `; comment\nproc main ()` produces zero Semicolon tokens; first non-Eof token is `proc`.
  2. `Semicolon_MidLine_IsStillSemicolonToken` — `Int x = 5;` still emits Semicolon.
  3. `Semicolon_IndentedAtLineStart_IsLineComment` — `  ; indented\nInt y = 1;` emits exactly one Semicolon (the one on line 2).
  4. `TODO_AtColumn0_IsLineComment` — `TODO: fix this\nproc main ()` emits no `TODO` Identifier and no Colon; first non-Eof token is `proc`.
  5. `FIXME_AtColumn0_IsLineComment` — `FIXME: broken\nInt x = 5` emits no `FIXME` Identifier and no Colon; `IntLiteral 5` survives.
  6. `TODO_InsideStringLiteral_IsNotComment` — `(print "TODO: hello")` keeps the entire `"TODO: hello"` as a StringLiteral (RESEARCH Pitfall 8: SkipWhitespaceAndComments only runs between tokens, never inside ScanString).
  7. `EnableHAsB_StillParses_OptionACanary` — `enable hAsB;\nproc main ()` keeps its Semicolon (the D-11 Option A regression canary).
  8. `DoubleSlash_StillWorks_NoRegression` — `// regular comment\nInt x = 5;` still works (existing arm unaffected).
- **20 / 20** `ByteIdentical*` determinism tests pass — the hard constraint. By construction: the new arms emit zero tokens for any existing valid .flow program (RESEARCH §Migration Audit confirms zero column-0 `;`/`TODO:`/`FIXME:` exist in any in-repo .flow file).
- **9 / 9** `Phase21.PragmaScanner` regression canary tests pass — D-11 Option A preserved `enable hAsB;` parse.
- **Real-world canary**: `examples/pragmas/h_alias.flow` runs end-to-end via `dotnet run --project flow-interpreter`, producing the expected WAV+MIDI output with the `enable hAsB;` pragma intact.

## Task Commits

Each task was committed atomically (RED → GREEN TDD discipline):

1. **Task 1 RED: failing tests for the three new comment forms** — `fdd1b5e` (test)
2. **Task 1 GREEN: SimpleLexer arms for `;` / `TODO:` / `FIXME:`** — `dd81c87` (feat)

Plan metadata commit (this SUMMARY + STATE/ROADMAP updates) follows separately.

## Files Created/Modified

**Created**

- `flow-lang.Tests/Unit/Phase31/Phase31LexerCommentFormsTests.cs` — 122 lines. 8 `[Fact]` tests with a `Tokenize(string)` helper that constructs `new SimpleLexer(src, er, fileName: null, pragmaSet: PragmaSet.Empty).Tokenize()`. Pattern matches `flow-lang.Tests/Unit/Phase26/NegativeLiteralLexFacts.cs::Lex` (lexer-direct, no parser/engine). Each test docstring cites the decision or pitfall it pins.

**Modified**

- `flow-lang/Lexing/SimpleLexer.cs` — +30 insertions, zero deletions. Three new arms inserted in `SkipWhitespaceAndComments` after the existing `Note:` arm at line 1144-1151 and before the `else break;` fall-through. The `;` arm carries a 6-line comment block citing D-11 Option A, the RESEARCH Migration Audit, and the Phase 18/25/27/28 ByteIdentical contract. The `TODO:` and `FIXME:` arms carry one-line cite comments. Existing `Note:` arm, `IsStartOfLineContent()` helper, and `TokenType.Semicolon` path all untouched.

## Decisions Made

- **D-11 Option A as locked.** No deviation from the 31-DECISIONS.md plan: position-sensitive `;` via `IsStartOfLineContent()` exactly as the existing `Note:` arm uses it. Mid-line `;` continues through the default lex path → `TokenType.Semicolon`.
- **`Substring`-not-`AsSpan`.** The plan's `<action>` block said "match whichever the existing code uses for consistency; do NOT introduce a new style mid-file." The existing `Note:` arm uses `_source.Substring(_position).StartsWith(...)`, so `TODO:` and `FIXME:` use the same. Slightly more allocation-heavy than `AsSpan` but consistency wins; a future allocation-cleanup pass can sweep all four arms together.
- **Order of arm insertion follows decision-ID.** The `;` arm is first (D-11 is the locked decision driving this plan), then `TODO:`, then `FIXME:`. No functional difference (each arm tests a different first character), but the order reads decision-first.
- **No grammar / TextMate change in this plan.** Per the execution-context `<files_to_read>` boundary: only `flow-lang/Lexing/SimpleLexer.cs` + the test file changed. `vscode-extension/syntaxes/flow.tmLanguage.json` is Plan 31-06's job; .flow file migrations (zero needed per RESEARCH) are Plan 31-07's job.

## Deviations from Plan

None — plan executed exactly as written. Acceptance criteria all met (see below).

## Acceptance Criteria — All Met

- `grep -c "FIXME:" flow-lang/Lexing/SimpleLexer.cs` → **2** (≥ 1 required: arm trigger + comment text).
- `grep -c "TODO:" flow-lang/Lexing/SimpleLexer.cs` → **2** (≥ 1 required: arm trigger + comment text).
- `grep -c "IsStartOfLineContent" flow-lang/Lexing/SimpleLexer.cs` → **6** (≥ 4 required: 1 definition + 4 call sites — Note + 3 new — plus the in-comment mention).
- `grep -c "D-11" flow-lang/Lexing/SimpleLexer.cs` → **1** (≥ 1 required: decision-ID citation in the new `;` arm comment block).
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase31.Phase31LexerCommentForms"` → **8 / 8 GREEN** (≥ 8 required).
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase21.PragmaScanner"` → **9 / 9 GREEN** (regression canary).
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~ByteIdentical"` → **20 / 20 GREEN** (Phase 18/25/27/28 determinism contracts preserved).

## Issues Encountered

None. The plan's `<read_first>` block correctly identified the `Note:` arm location at SimpleLexer.cs:1144 and the `IsStartOfLineContent()` helper at SimpleLexer.cs:1159 — both were exactly where the plan said.

## Threat Flags

None. The three threats in the plan's `<threat_model>` (T-31-03-01 DoS, T-31-03-02 string-context tampering, T-31-03-03 determinism break) were addressed by construction:

- **T-31-03-01** (pathological many-comments DoS): accepted as designed — each arm consumes its line in O(line-length); total cost O(file-length); same shape as the `Note:` arm shipped since Phase 16 without incident.
- **T-31-03-02** (in-string firing): mitigated. `SkipWhitespaceAndComments` is the SkipWhitespaceAndComments method — it runs only between tokens, never inside ScanString. Pitfall 8 pinned by Fact #6 (`TODO_InsideStringLiteral_IsNotComment`).
- **T-31-03-03** (determinism break): mitigated. Zero column-0 `;`/`TODO:`/`FIXME:` exist in any in-repo .flow file (RESEARCH §Migration table). 20/20 ByteIdentical tests GREEN at HEAD.

## User Setup Required

None — pure lexer change, no external service configuration.

## Next Plan Readiness

- **Plan 31-06** (TextMate grammar half of SPEC-4) — independent of this plan's lexer work; consumes the lexer-side contract pinned here. The grammar adds composer-facing syntax highlighting (`comment.line.semicolon.flow`, `comment.line.todo.flow`, `comment.line.fixme.flow`) for editors via `vscode-extension/syntaxes/flow.tmLanguage.json`.
- **Plan 31-07** (full-sweep .flow regression sweep) — picks up the lexer change for end-to-end verification against all 70+ in-repo `tests/test_*.flow` scripts. Plan 31-03 already smoke-tested two real scripts (`dotnet run -e 'Int x = 5; (print (str x))'` + `examples/pragmas/h_alias.flow`) — both pass.
- **No new tech-stack additions** — pure C# lexer change against existing infrastructure. The `Phase31LexerCommentFormsTests` pattern is reusable for any future lexer-arm additions.

## Self-Check: PASSED

- Verified `flow-lang.Tests/Unit/Phase31/Phase31LexerCommentFormsTests.cs` exists (file at `/home/noah/Desktop/projects/flow-sharp/flow-lang.Tests/Unit/Phase31/Phase31LexerCommentFormsTests.cs`).
- Verified `flow-lang.Tests/Unit/Phase31/Phase31LexerCommentFormsTests.cs` contains `public class Phase31LexerCommentFormsTests` (must-have artifact contract).
- Verified `flow-lang/Lexing/SimpleLexer.cs` contains `FIXME:` (must-have artifact contract).
- Verified RED commit `fdd1b5e` exists in `git log`.
- Verified GREEN commit `dd81c87` exists in `git log`.
- Verified `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase31.Phase31LexerCommentForms"` exits 0 with 8 / 8 GREEN.
- Verified `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase21.PragmaScanner"` exits 0 with 9 / 9 GREEN.
- Verified `dotnet test flow-lang.Tests --filter "FullyQualifiedName~ByteIdentical"` exits 0 with 20 / 20 GREEN — the critical determinism gate.
- Verified `dotnet build flow-lang/flow-lang.csproj /clp:ErrorsOnly` returns 0 errors.
- Verified the `<key_links>` pattern from the plan frontmatter: `IsStartOfLineContent\(\)` appears in SimpleLexer.cs SkipWhitespaceAndComments (4 call sites — 1 existing `Note:` arm + 3 new arms).
