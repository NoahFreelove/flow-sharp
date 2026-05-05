---
phase: 24-scale-linting-flow-lsp
plan: 03
subsystem: lsp-diagnostics
tags: [analyzer, lsp, scale-lint, ast-traversal, diatonic-spelling, phase-24, wave-2]

# Dependency graph
requires:
  - phase: 24-scale-linting-flow-lsp/24-00
    provides: ParseSession populates Ast.Pragmas (D-19 activation gate input)
  - phase: 24-scale-linting-flow-lsp/24-01
    provides: PragmaRegistry.KnownPragmas["scaleLint"] (closed-set membership)
  - phase: 24-scale-linting-flow-lsp/24-02
    provides: DiatonicSpellings.GetDiatonicSpellings(root, mode) (119-entry lookup)
  - phase: 17-flow-language-server
    provides: NoteStreamContext.FindEnclosingKey (D-21 verbatim reuse target)
  - phase: 23-microtonal-tuning-wedge
    provides: ScaleDatabase.TryParseKeyWithMode (D-02 mode-parsing entry)
  - phase: 21-pragma-system-h-alias
    provides: Program.Pragmas + Token.OriginalText/DiagnosticText
provides:
  - ScaleLintAnalyzer.Analyze(ast, tokens, source) — pure read-only AST + token traversal returning Information-severity LSP Diagnostic instances
  - LINT-01 unit acceptance (F#4 in Cmajor → exactly one Information diagnostic)
  - LINT-02 unit acceptance (no pragma → zero diagnostics)
  - LINT-03 unit acceptance (innermost-key wins via FindEnclosingKey reuse)
  - 14 Facts + 7-row Theory pinning all D-01..D-23 element/key/pragma rules
affects: [24-04 (CombinedDiagnosticsPublisher will consume Analyze), 24-05 (Phase 24 closure smoke + REQUIREMENTS/ROADMAP/STATE)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "AST + token walker producing LSP-shaped output (mirrors NoteStreamContext.cs role)"
    - "D-19 short-circuit at analyzer entry — opt-in pragma gate as first line"
    - "D-22 silent fail-open — return Array.Empty when key/spelling unknown; never throw"
    - "Switch dispatch over NoteStreamElement records with intentional fall-through for SKIP cases (D-11..D-14 emit no case branch)"
    - "Token-wide squiggle range with adjacent duration-suffix absorption (lexer splits F#4q → F#4 + q)"

key-files:
  created:
    - flow-lsp/Diagnostics/ScaleLintAnalyzer.cs
    - flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerFacts.cs
  modified: []

key-decisions:
  - "Cent-offset sign stripping in ExtractSpellingAndOctave: lexer glues trailing +/- onto NoteName (e.g. E4+50c → NoteLiteral text 'E4+'). Plan claimed NoteName was already cent-stripped, but in practice the trailing sign survives. Strip it before octave extraction so D-08 base-spelling decision matches the spec text."
  - "Token-wide range extension to absorb adjacent w/h/q/e/s/t suffix Identifier: lexer splits F#4q into F#4 (NoteLiteral, width 3) + q (Identifier, width 1) by rewinding position after parse. The Plan acceptance pinned width >= 4, so the analyzer extends the range by the next-token width when it is a single duration-suffix character at the immediately-adjacent column. Squiggle now covers the full visible note as the composer typed it."
  - "Public static class (not internal) — flow-lsp.csproj has no InternalsVisibleTo for flow-lang.Tests, so analyzer must be public for the Facts to consume it."

patterns-established:
  - "Pragma activation gate at analyzer entry — first line short-circuit prevents any work when opt-in pragma absent"
  - "Innermost-key resolution via existing NoteStreamContext.FindEnclosingKey — analyzers reuse the Phase 17 token-walk verbatim instead of re-implementing brace-depth tracking"
  - "Silent fail-open chain — analyzer never emits diagnostics when (a) no enclosing key, (b) key unparseable, or (c) spelling set null. Charitable interpretation per project memory."

requirements-completed: [LINT-01, LINT-02, LINT-03]

# Metrics
duration: ~25 min
completed: 2026-05-04
---

# Phase 24 Plan 24-03: ScaleLintAnalyzer Summary

**Pure AST + token walker producing Information-severity LSP diagnostics for non-diatonic notes inside `key { }` blocks; LINT-01/02/03 unit acceptance achieved; 21/21 ScaleLintAnalyzerFacts (14 Facts + 7-row Theory) GREEN; full 668-test suite zero regressions.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-05-04T17:07:00Z (approx)
- **Completed:** 2026-05-04T17:31:56Z
- **Tasks:** 2 (TDD: RED + GREEN)
- **Files created:** 2 (1 production + 1 test)
- **Files modified:** 0 (zero flow-lang/ touch — D-04 invariant honored)

## Accomplishments

- Shipped `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` (336 lines, 14148 bytes) — the heart of Phase 24's static-analysis surface
- All three Phase 24 requirements unit-accepted: LINT-01 (positive flag), LINT-02 (D-19 short-circuit), LINT-03 (innermost-key)
- D-21 invariant honored — analyzer reuses `NoteStreamContext.FindEnclosingKey` verbatim (no parallel resolver)
- Spelling-aware D-01 dispatch produces three message branches: enharmonic-pitch-class match, lower+upper neighbors, single-neighbor fallback
- Element traversal correctly recurses into ChordElement/RandomChoiceElement/TupletElement (D-07/09/10) and silently skips RomanNumeral/NamedChord/VariableRef/Rest (D-11..D-14)
- Cent offsets verified silent on diatonic base spelling (D-08) and flagged on non-diatonic base spelling
- Plan 24-04 unblocked — `Analyze(ast, tokens, source) → IReadOnlyList<Diagnostic>` is the contract `CombinedDiagnosticsPublisher` will consume

## Task Commits

Each task was committed atomically (TDD: test → feat):

1. **Task 1 (RED): 15 Facts pinning analyzer behavior** — `3d9233a` (test)
2. **Task 2 (GREEN): Ship ScaleLintAnalyzer** — `3c18795` (feat)

_Note: No REFACTOR commit needed — implementation matched the RESEARCH skeleton (lines 599-737 of 24-RESEARCH.md) closely enough that the GREEN ship was already idiomatic._

## Files Created/Modified

- `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` (NEW) — public static class with `Analyze`, `WalkStatements`, `WalkNoteStream`, `CheckElement`, `CheckNote`, `ExtractSpellingAndOctave`, `SpellingToPitchClass`, `BuildDiagnostic`, `FindNeighbors` helpers
- `flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerFacts.cs` (NEW) — 14 Facts + 1 Theory (7 rows) covering LINT-01/02/03 + D-01/D-08/D-11/D-12/D-14/D-15/D-17/D-18/D-22/D-23 plus 7-mode coverage

## Decisions Made

### D-1: Cent-offset trailing-sign strip in ExtractSpellingAndOctave (Rule 1 fix)

**Found during:** Task 2 GREEN — `CentOffset_E4plus50c_InCmajor_Silent` failed with diagnostic `"E4+ not diatonic in Cmajor"`.

**Why:** The Plan and CONTEXT both stated `NoteElement.NoteName` is "already cent-stripped" — but inspection of `flow-lang/Lexing/SimpleLexer.cs` shows the lexer's note-shape branch *glues trailing `+`/`-` onto the note text* (so the cent-magnitude/unit `50c` can be a separate `CentLiteral` token). Result: under `E4+50c`, `NoteName = "E4+"`. The analyzer's `ExtractSpellingAndOctave` was treating `+` as part of the spelling, so `E+` failed the `spellings.Contains` check and fired a (wrong) diagnostic.

**Fix:** Strip trailing `+`/`-` characters before stripping the octave digits. Rule 1 (auto-fix bug) — the Plan's spec text is correct (D-08 says base-note spelling decides; cents irrelevant), but the implementation has to account for the lexer's actual output shape.

**Files modified:** flow-lsp/Diagnostics/ScaleLintAnalyzer.cs

**Verification:** `CentOffset_E4plus50c_InCmajor_Silent` now PASSES (zero diagnostics for `E4+50c` in Cmajor); `CentOffset_Ebplus50c_InCmajor_FlagsBaseSpelling` still PASSES (one diagnostic on Eb base spelling, message contains "Eb4").

**Committed in:** 3c18795 (Task 2 commit)

### D-2: Token-wide range extension to absorb adjacent duration-suffix Identifier (Rule 1 fix)

**Found during:** Task 2 GREEN — `Range_SpansFullTokenWidth` failed with `expected Range width >= 4 for token 'F#4q'; got 3`.

**Why:** The Plan and CONTEXT both described D-17 as token-wide using `Token.Text.Length`, asserting `F#4q` is 4 chars wide. But `flow-lang/Lexing/SimpleLexer.cs` (line 671) explicitly *rewinds position by 1* and emits `F#4q` as two tokens: `NoteLiteral "F#4"` (width 3) + `Identifier "q"` (width 1). Range based on the NoteLiteral alone is width 3.

**Fix:** When the matching NoteLiteral has an immediately-adjacent (same line, next column) `Identifier` token whose single character is a duration-suffix letter (`w/h/q/e/s/t`), extend the range width by the suffix length. Rule 1 (auto-fix bug) — the spec intent was clearly "cover the full visible note", and the lexer split is an implementation detail the analyzer must paper over.

**Files modified:** flow-lsp/Diagnostics/ScaleLintAnalyzer.cs

**Verification:** `Range_SpansFullTokenWidth` now PASSES (Range width = 4 for `F#4q`).

**Committed in:** 3c18795 (Task 2 commit)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Cent-offset trailing-sign survives in NoteName**
- **Found during:** Task 2 (GREEN ship)
- **Issue:** Lexer glues trailing `+`/`-` onto note text (e.g. `E4+50c` → NoteLiteral text `E4+`); Plan and CONTEXT incorrectly described NoteName as "already cent-stripped"
- **Fix:** Strip trailing `+`/`-` in ExtractSpellingAndOctave before octave-digit stripping; preserves D-08 base-spelling-decides intent
- **Files modified:** flow-lsp/Diagnostics/ScaleLintAnalyzer.cs (ExtractSpellingAndOctave helper)
- **Verification:** Both cent-offset Facts (E4+50c silent, Eb4+50c flagged) PASS; no regression in existing parse tests
- **Committed in:** 3c18795 (Task 2 commit)

**2. [Rule 1 - Bug] Lexer splits F#4q into two tokens; token-wide range underspans**
- **Found during:** Task 2 (GREEN ship)
- **Issue:** Plan claimed `Token.Text.Length` for `F#4q` is 4, but the lexer rewinds position and emits `F#4` (NoteLiteral, len 3) + `q` (Identifier, len 1) per the existing duration-suffix-tokenization branch in SimpleLexer
- **Fix:** Extend the analyzer's range by the next token's width when it is a single duration-suffix character (`w/h/q/e/s/t`) at the immediately-adjacent column on the same line; covers the composer-visible note width
- **Files modified:** flow-lsp/Diagnostics/ScaleLintAnalyzer.cs (BuildDiagnostic range computation)
- **Verification:** `Range_SpansFullTokenWidth` PASSES (width=4); other tests unaffected (most use bare NoteLiteral without suffix)
- **Committed in:** 3c18795 (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 1 bugs against Plan/CONTEXT spec assumptions about lexer output)
**Impact on plan:** No scope creep. Both fixes preserve original D-08 and D-17 spec intent — the analyzer simply has to handle the lexer's actual output rather than the spec's idealized output. Documented above so Plan 24-04 inherits accurate intuition about token shapes.

## Issues Encountered

- **Lexer behavior vs. spec text mismatch (×2)** — see Deviations. Both required reading SimpleLexer.cs to discover the actual token output shape; once known, the fixes were small and local to two helpers in the analyzer.

## Self-Check

**1. Created files exist:**
- FOUND: flow-lsp/Diagnostics/ScaleLintAnalyzer.cs
- FOUND: flow-lang.Tests/Unit/Phase24/ScaleLintAnalyzerFacts.cs

**2. Commits exist:**
- FOUND: 3d9233a (Task 1 RED)
- FOUND: 3c18795 (Task 2 GREEN)

**3. Acceptance criteria verified:**
- ScaleLintAnalyzer.cs contains `public static class ScaleLintAnalyzer` (1)
- D-19 short-circuit `Pragmas.Has("scaleLint")` present
- D-21 reuse `NoteStreamContext.FindEnclosingKey` present
- D-18 source string `"flow.scaleLint"` present
- D-16 `DiagnosticSeverity.Information` present
- 4 switch branches (NoteElement, ChordElement, RandomChoiceElement, TupletElement) present
- 0 forbidden SKIP-case branches (RomanNumeralElement, NamedChordElement, VariableReferenceElement, RestElement) — fall-through honored
- ScaleLintAnalyzerFacts: 21/21 PASSED (14 Facts + 7-row Theory)
- Phase 24 filter: 63/63 PASSED
- Full suite: 668/668 PASSED
- Zero flow-lang/ touches (D-04 invariant)

## Self-Check: PASSED

## Next Plan Readiness

- Plan 24-04 (CombinedDiagnosticsPublisher) — analyzer contract is `Analyze(ast, tokens, source) → IReadOnlyList<Diagnostic>`. Ready to wire into `DocumentManager.onParse` callback alongside existing `DiagnosticsPublisher.BuildDiagnostics(result.Errors)`.
- Plan 24-05 (Phase 24 closure) — LINT-01/02/03 unit acceptance achieved; remaining work is the `tests/test_scale_lint.flow` integration smoke and REQUIREMENTS/ROADMAP/STATE updates.
- No blockers.

---
*Phase: 24-scale-linting-flow-lsp*
*Completed: 2026-05-04*
