---
phase: 21-pragma-system-h-alias
plan: 01
subsystem: lexing
tags: [pragma, infrastructure, lexer, parser, module-loader, prag-01, prag-02]

dependency_graph:
  requires:
    - 20-04   # Phase 20 complete (range/enharmonic/slice closures)
  provides:
    - flow-lang/Lexing/PragmaSet.cs           # PragmaSet + PragmaDeclarationSite records
    - flow-lang/Lexing/PragmaRegistry.cs      # closed-set known-pragma table + Levenshtein
    - flow-lang/Lexing/PragmaScanner.cs       # pre-lex source-transform stage
    - SimpleLexer._pragmaSet field            # plumbing for Plan 21-02 H-substitution
    - Program.Pragmas AST field               # downstream tooling (LSP / re-parse)
  affects:
    - flow-lang/Core/FlowEngine.cs            # pipeline gains pre-scan stage
    - flow-lang/Runtime/ModuleLoader.cs       # per-imported-file pragma isolation (D-06)
    - flow-lang/Parsing/Parser.cs             # ctor + Parse() return Program(... pragmaSet)
    - flow-lang/Ast/Program.cs                # gains PragmaSet field + 2-arg compat ctor

tech-stack:
  added: []
  patterns:
    - "Closed-set registry with Wagner-Fischer Levenshtein for did-you-mean errors (matches MusicTwentyOneShorthand house style)"
    - "Pre-lex source transformation preserving line+column numbering via equivalent-length whitespace replacement"
    - "Zero-allocation fast path returning original string reference when feature absent (preserves byte-identical determinism)"
    - "Per-imported-file PragmaSet computed in lexical scope of LoadModule — pragma isolation enforced structurally, not semantically"

key-files:
  created:
    - flow-lang/Lexing/PragmaSet.cs
    - flow-lang/Lexing/PragmaRegistry.cs
    - flow-lang/Lexing/PragmaScanner.cs
    - flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs
    - flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs
    - flow-lang.Tests/Integration/Phase21/PragmaIsolationFacts.cs
    - tests/test_pragma_isolation.flow
    - tests/test_pragma_isolation_module.flow
  modified:
    - flow-lang/Core/FlowEngine.cs
    - flow-lang/Runtime/ModuleLoader.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang/Lexing/SimpleLexer.cs
    - flow-lang/Ast/Program.cs

decisions:
  - "PragmaSet is `record` (reference type) not `record struct` — referenced by Program + Parser (heap-rooted), the IReadOnlySet<string> field already forces heap allocation, and copy semantics would be a micro-pessimization"
  - "PragmaScanner state machine accepts trailing `\\r` as part of the prefix line and emits `\\r\\n` verbatim during pragma replacement (Pitfall G CRLF preservation)"
  - "PragmaScanner also treats `Note:` line comments (a Flow-specific lexer comment form) as legal prefix-region content in addition to `//` and blank lines (D-03 spirit)"
  - "Per-line state-machine matcher rather than regex literal — avoids System.Text.RegularExpressions dependency for a hot-startup path"
  - "Plan 21-02 will tighten PragmaIsolationFacts (or add a sibling Fact) to assert that `H4q` in the importer raises a parse error after H-substitution lands; Plan 21-01 baseline asserts importer + module both parse cleanly"

metrics:
  duration_minutes: 35
  tasks_completed: 5
  files_changed: 13
  lines_added: ~620
  test_count_delta: +15
  date_completed: 2026-05-01
---

# Phase 21 Plan 01: Pragma System Plumbing — Summary

File-scope `enable <pragma>;` infrastructure (PragmaScanner pre-lex stage + PragmaSet/PragmaRegistry value types + Parser/SimpleLexer/Program/FlowEngine/ModuleLoader plumbing) closes PRAG-01 + PRAG-02 from REQUIREMENTS.md. Phase 18 byte-identical regression gate stays GREEN via the PragmaScanner zero-allocation fast path.

## What Landed

5 atomic commits (one per task in the plan):

| Hash | Title |
| ---- | ----- |
| c378c20 | test(21-01): wave 0 scaffolding for pragma scanner + registry + isolation Facts |
| f2a48d0 | feat(21-01): add PragmaSet + PragmaRegistry + PragmaScanner production code |
| 19d7dc8 | feat(21-01): wire PragmaSet into Parser + SimpleLexer + Program AST (D-05, D-08) |
| 95c8c71 | feat(21-01): insert PragmaScanner.Scan stage into FlowEngine.Execute (D-01, D-07) |
| 60f7f18 | feat(21-01): insert PragmaScanner.Scan stage into ModuleLoader.LoadModule (D-06) |

### Files Created (8)

**Production (3):**
- `flow-lang/Lexing/PragmaSet.cs` — Immutable record + `Empty` singleton + `Has(name)` helper. `PragmaDeclarationSite(Name, Location)` companion record for diagnostic provenance.
- `flow-lang/Lexing/PragmaRegistry.cs` — Closed-set table (`hAsB` only per D-17), `IsKnown` / `AlphabetizedKnownNames` / `SuggestNearest` helpers, Wagner-Fischer Levenshtein DP with threshold `max(2, name.Length / 3)`.
- `flow-lang/Lexing/PragmaScanner.cs` — Pre-lex line-by-line state machine. Returns `(PragmaSet, transformedSource)`. Replaces matched pragma lines with equivalent-length whitespace preserving exact `\n` / `\r\n` newline (D-04 + Pitfall G). Emits D-11 (after-statement) and D-12 (unknown-name) errors via `ErrorReporter`.

**Test scaffolding (5):**
- `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs` — 9 Facts covering empty source, fast-path same-reference, hAsB recognition, prefix comments+blanks, line-number alignment, duplicate-silent, after-statement-error, unknown-pragma-with-suggestion, CRLF preservation.
- `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` — 5 Facts pinning closed-set membership + alphabetized listing + Levenshtein suggestion + far-away-null.
- `flow-lang.Tests/Integration/Phase21/PragmaIsolationFacts.cs` — 1 integration Fact running tests/test_pragma_isolation.flow under FlowEngineRunner; asserts importer + module both parse cleanly. Plan 21-02 will tighten this Fact to assert the H4q parse error after substitution lands.
- `tests/test_pragma_isolation.flow` — Importer fixture; does NOT declare `enable hAsB;`.
- `tests/test_pragma_isolation_module.flow` — Imported-module fixture; declares `enable hAsB;` internally.

### Files Modified (5)

- `flow-lang/Parsing/Parser.cs` — Added private `PragmaSet _pragmaSet` field + trailing optional `PragmaSet? pragmaSet = null` ctor parameter. `Parse()` returns `new Program(SourceLocation.Unknown, statements, _pragmaSet)`.
- `flow-lang/Lexing/SimpleLexer.cs` — Added private `PragmaSet _pragmaSet` field + trailing optional `PragmaSet? pragmaSet = null` ctor parameter. `TryParseNote` is **unchanged** (Plan 21-02 lands the H→B substitution).
- `flow-lang/Ast/Program.cs` — Promoted to a 3-positional record with `PragmaSet Pragmas` field + backward-compat 2-arg ctor preserving the existing `(Location, Statements)` signature for tests/LSP.
- `flow-lang/Core/FlowEngine.cs` — Inserted `PragmaScanner.Scan` as step 0 before `SimpleLexer`; threads pragmaSet into both lexer + parser ctors.
- `flow-lang/Runtime/ModuleLoader.cs` — Mirrors the FlowEngine pre-scan stage per imported file, using a LOCAL `localReporter` + LOCAL `pragmaSet` so module pragmas never leak to the importer. PRAG-02 isolation is enforced structurally (lexical scoping) rather than semantically.

## Verification Results

| Check | Result |
| ----- | ------ |
| `dotnet build` | clean (0 errors, 14 warnings — all pre-existing, none introduced) |
| `dotnet test --filter "FullyQualifiedName~Phase21"` | 15/15 GREEN |
| `dotnet test --filter "FullyQualifiedName~Phase18"` | 19/19 GREEN (byte-identical regression gate) |
| `dotnet test --filter "FullyQualifiedName~Phase19"` | ALL GREEN |
| `dotnet test --filter "FullyQualifiedName~Phase20"` | ALL GREEN |
| `dotnet test` (full suite) | 399/399 GREEN |
| `for t in tests/test_*.flow; do ...; done` | all non-error scripts PASS; 3 known `ExpectedErrorScripts` still emit their documented errors as before (test_error_masking, test_iteration_guard, test_musical_context_errors) |
| `dotnet run --project flow-interpreter tests/test_pragma_isolation.flow` | exit 0 + both PASSED sentinels |

### PragmaScanner Zero-Allocation Fast Path Verification

`PragmaScannerFacts.NoEnableSubstring_FastPath_ReturnsOriginalReference` asserts `Assert.Same(source, transformed)` — the SAME string reference is returned when no `enable` substring exists. This is the load-bearing mitigation that keeps the Phase 18 byte-identical regression gate (`ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests`) GREEN: every legacy `.flow` file (no `enable` anywhere) flows through `Scan` with zero allocation and zero string mutation.

## Phase 21 Fact Count Delta

- Pre-Plan-21-01: 0 Phase 21 Facts (the phase had no test class)
- Post-Plan-21-01: **15 Phase 21 Facts** (9 PragmaScannerFacts + 5 PragmaRegistryFacts + 1 PragmaIsolationFacts)

Total xUnit suite: ~340 → **399** (the larger delta beyond +15 reflects Phase 19/20 Facts that were already in the tree pre-rebase but were not yet counted in older summaries).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] xUnit working-directory mismatch in PragmaIsolationFacts**

- **Found during:** Task 5 verification
- **Issue:** `runner.RunFile("tests/test_pragma_isolation.flow")` failed with `DirectoryNotFoundException` because xUnit runs with `cwd = bin/Debug/net10.0/` rather than the repo root, so the relative path resolved to `bin/Debug/net10.0/tests/test_pragma_isolation.flow`.
- **Fix:** Resolve the .flow fixture path via `FlowScriptData.FindTestsRoot()` (the same helper used by `ByteIdenticalTutorialTests` and other Integration Facts that read .flow files) and `Path.Combine` to get the absolute path.
- **Files modified:** `flow-lang.Tests/Integration/Phase21/PragmaIsolationFacts.cs`
- **Commit:** 60f7f18

**2. [Rule 2 — Missing critical functionality] CRLF line-ending handling in PragmaScanner**

The plan's PragmaScanner skeleton in 21-RESEARCH.md split lines on `\n` only, leaving any trailing `\r` embedded in `lineText`. That would (a) break the `TryMatchPragmaLine` whitespace check (a `\r` after `;` is neither space nor tab) and (b) when the line was a pragma, overwrite the `\r` with a space during replacement — both contradicting the `CrlfLineEndings_Preserved` Fact required by the plan's own behavior list.

- **Found during:** Task 2 (writing PragmaScanner.cs against the pre-authored `CrlfLineEndings_Preserved` Fact)
- **Fix:** Track newline span explicitly. After locating `\n`, if the preceding character is `\r`, set `contentEnd` one earlier and `newlineSpan = 2`. The pragma replacement path emits the original 1- or 2-byte newline verbatim via `AppendNewline`. Non-pragma lines copy via `sb.Append(source, lineStart, lineEndIncl - lineStart)` which already includes any `\r\n` verbatim.
- **Files modified:** `flow-lang/Lexing/PragmaScanner.cs`
- **Result:** `CrlfLineEndings_Preserved` Fact passes (asserts `transformed[12]='\r'`, `transformed[13]='\n'`, full-length preservation).

**3. [Charitable interpretation] Treat `Note:` line comments as legal prefix-region content**

The plan's D-03 lists `// ...` and blank lines as the legal prefix-region content. Flow's `SimpleLexer` ALSO recognizes `Note:` as a line-comment shape (see `SimpleLexer.SkipWhitespaceAndComments` line 835). Several existing fixtures (`test_enharmonic_edges.flow`, `test_pragma_isolation.flow`, `test_pragma_isolation_module.flow`) place `Note:` lines after `use` statements and before pragma declarations. To keep prefix-region acceptance consistent with how the rest of the lexer treats comments, the scanner now also accepts `Note:` lines as prefix content (alongside `//` and blank).

- **Found during:** Task 2 (matching the prefix-region semantics across the existing comment styles)
- **Files modified:** `flow-lang/Lexing/PragmaScanner.cs` (added `isNoteComment` branch)

These three are tracked as deviations under Rules 1-3 (no architectural changes, no user permission required). All three are self-contained additive fixes that make the production code align with the test contracts pre-authored in Task 1.

### Locked Decisions Honored

D-01 (pre-scan), D-02 (PragmaSet record), D-03 (comments+blanks in prefix), D-04 (equivalent-length whitespace), D-05 (Parser ctor param), D-06 (per-imported-file isolation), D-07 (REPL semantics — implicit via FlowEngine path), D-08 (Program.Pragmas), D-09 (duplicate silent), D-10 (module pragmas silent), D-11 (after-statement error), D-12 (unknown name + did-you-mean), D-17 (hAsB only) — all honored without modification.

D-13/D-14/D-15/D-16 (H→B substitution + token original-text preservation + chord-literal scope) are out of scope for Plan 21-01 — they land in Plan 21-02.

## Hand-off to Plan 21-02

Plan 21-02 (H-alias substitution) is unblocked:
- `SimpleLexer._pragmaSet` field exists and is wired through both `FlowEngine.Execute` and `ModuleLoader.LoadModule` ctor calls
- `PragmaSet.Has("hAsB")` returns true for files declaring `enable hAsB;`
- Plan 21-02 needs to:
  - Modify `SimpleLexer.TryParseNote` to accept `H`-shaped strings when `_pragmaSet.Has("hAsB")` is true (D-13)
  - Add `OriginalText` to the `Token` record (D-15) so diagnostics show `H4q` while internal pitch handling reads `B4q`
  - Update `tests/test_pragma_isolation.flow` to add an `H4q`-using line that asserts the parse error in the importer (the now-relaxed `PragmaIsolationFacts.Importer_LoadsModule_WithoutInheritingItsPragmas` Fact comments document the path forward)

## Threat Surface Scan

No new network endpoints, auth paths, file access patterns, or schema changes at trust boundaries. The threat register entries `T-21-01` (DoS over pragma-shaped lines), `T-21-02` (Levenshtein DP allocation), `T-21-03` (closed-set tampering) are all mitigated as planned:

- T-21-01: PragmaScanner is O(n) in source length; fast path is zero-allocation when `enable` absent.
- T-21-02: Levenshtein DP allocates two int arrays of size `m+1` where `m = max(KnownPragmas.Keys.Length)` = 4 (`hAsB`). The bound is the closed-set max, not user-controlled.
- T-21-03: PragmaRegistry is a CLOSED set in code (D-17). Unknown names always error via D-12. Adding a high-impact pragma requires an explicit code edit gated by review.

## Self-Check: PASSED

**Files verified to exist:**
- FOUND: flow-lang/Lexing/PragmaSet.cs
- FOUND: flow-lang/Lexing/PragmaRegistry.cs
- FOUND: flow-lang/Lexing/PragmaScanner.cs
- FOUND: flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs
- FOUND: flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs
- FOUND: flow-lang.Tests/Integration/Phase21/PragmaIsolationFacts.cs
- FOUND: tests/test_pragma_isolation.flow
- FOUND: tests/test_pragma_isolation_module.flow

**Commits verified to exist:**
- FOUND: c378c20 (Task 1 — test scaffolding)
- FOUND: f2a48d0 (Task 2 — PragmaSet + PragmaRegistry + PragmaScanner)
- FOUND: 19d7dc8 (Task 3 — Parser/SimpleLexer/Program plumbing)
- FOUND: 95c8c71 (Task 4 — FlowEngine integration)
- FOUND: 60f7f18 (Task 5 — ModuleLoader integration)
