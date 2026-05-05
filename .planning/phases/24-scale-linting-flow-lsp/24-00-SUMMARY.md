---
phase: 24-scale-linting-flow-lsp
plan: 00
subsystem: lsp
tags: [lsp, pragma, scale-lint, phase-24, wave-0, parse-session]

# Dependency graph
requires:
  - phase: 21-pragma-system
    provides: PragmaScanner.Scan + PragmaSet + 4-arg SimpleLexer + 3-arg Parser ctors
  - phase: 17-lsp-foundations
    provides: ParseSession + LspFixtures helper + soft-failure error model (D-06)
provides:
  - "Pragma-scan-then-parse pipeline in flow-lsp/ParseSession.Parse mirroring FlowEngine.Run() lines 66-82"
  - "Program.Pragmas now populated from source-level enable declarations in LSP-edited files"
  - "Phase 21 latent regression closed: enable hAsB; takes effect in LSP (H4q lexes as note literal)"
  - "Foundation for Plan 24-03 D-19 activation gate (Ast.Pragmas.Has(\"scaleLint\"))"
  - "3 xUnit Facts in flow-lang.Tests/Unit/Phase24/ pinning the pragma-scan widening + hAsB regression"
affects: [24-01, 24-02, 24-03, 24-04, 24-05, 24-scale-linting-flow-lsp]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "LSP parse pipeline mirrors FlowEngine pragma-scan stage verbatim (no logic divergence)"
    - "LSP variant deliberately omits er.HasErrors short-circuit (Phase 17 D-06 soft-failure)"

key-files:
  created:
    - flow-lang.Tests/Unit/Phase24/ParseSessionPragmaFacts.cs
  modified:
    - flow-lsp/ParseSession.cs

key-decisions:
  - "Soft-failure preserved: ParseSession does NOT short-circuit on er.HasErrors between stages, unlike FlowEngine.Run() which returns false. Required so analyzer/completion/hover keep working on partial parses (Phase 17 D-06)."
  - "Test 1 uses enable hAsB; (a Phase 21 known pragma) instead of scaleLint, isolating the pragma-scan widening from Plan 24-01's registry add. Avoids RED cascade across Wave 0/1."
  - "Zero-touch flow-lang invariant preserved: only flow-lsp/ParseSession.cs and flow-lang.Tests/Unit/Phase24/ touched. Phase 18 byte-identical determinism not affected (no flow-lang code changed)."

patterns-established:
  - "Phase 24 Wave 0: any new flow-lsp parse-pipeline stage must mirror FlowEngine.Run() but drop er.HasErrors short-circuits between stages"
  - "Phase 24 test convention: namespace FlowLang.Tests.Unit.Phase24 + reuse Phase17 LspFixtures helper via using FlowLang.Tests.Unit.Phase17;"

requirements-completed: [LINT-01, LINT-02]

# Metrics
duration: 2min
completed: 2026-05-04
---

# Phase 24 Plan 00: Wave 0 — ParseSession Pragma-Scan Widen Summary

**flow-lsp/ParseSession.Parse now runs PragmaScanner.Scan upstream of the lexer, populating Program.Pragmas from source — closes Pitfall 1 and the Phase 21 hAsB latent regression in 4 lines.**

## Performance

- **Duration:** ~2 min (118 s)
- **Started:** 2026-05-04T17:14:16Z
- **Completed:** 2026-05-04T17:16:14Z
- **Tasks:** 2 (Task 0 RED Facts, Task 1 GREEN widen)
- **Files modified:** 2 (1 created, 1 modified)
- **Test impact:** +3 Facts; full suite 608/608 passed (zero regression)

## Accomplishments

- **Pitfall 1 closed:** `flow-lsp/ParseSession.Parse` now invokes `PragmaScanner.Scan(source, path, er)` upstream of `SimpleLexer` and threads the resulting `pragmaSet` into the 4-arg `SimpleLexer` ctor and 3-arg `Parser` ctor. `ParseResult.Ast.Pragmas` is no longer always `PragmaSet.Empty` — it now reflects file-scope `enable <pragma>;` declarations.
- **Phase 21 latent regression closed (side-effect):** `enable hAsB;` declared in an LSP-edited file now takes effect — the lexer sees the pragmaSet so `H4q` canonicalizes to `B4q` instead of surfacing a spurious "unknown identifier" diagnostic.
- **3 RED→GREEN Facts pinned** in `flow-lang.Tests/Unit/Phase24/ParseSessionPragmaFacts.cs` to guard against future LSP regressions on this exact pipeline (`Parse_EnableHAsB_PopulatesPragmas`, `Parse_NoEnable_PragmasIsEmpty`, `Parse_EnableHAsB_LexesH4qAsNoteLiteral`).
- **D-19 activation gate unblocked:** Plans 24-01 → 24-05 can now rely on `parseResult.Ast.Pragmas.Has("scaleLint")` returning the actual user intent.
- **Zero-touch flow-lang invariant preserved:** No file under `flow-lang/` was modified. Phase 18 byte-identical determinism cannot regress from this plan.

## Task Commits

Each task was committed atomically:

1. **Task 0: Add 3 RED xUnit Facts pinning ParseSession pragma-scan behavior** — `7346b3b` (test)
   - Created `flow-lang.Tests/Unit/Phase24/ParseSessionPragmaFacts.cs`
   - Reuses Phase17 `LspFixtures.Parse` helper (no duplication)
   - RED state observed: `Failed: 2, Passed: 1` — proves Pitfall 1 is real before the fix

2. **Task 1: Widen ParseSession.Parse to mirror FlowEngine pragma-scan pipeline** — `6bcc697` (feat)
   - Inserted `PragmaScanner.Scan` upstream of `new SimpleLexer(...)`
   - Threaded `pragmaSet` into 4-arg `SimpleLexer` ctor and 3-arg `Parser` ctor
   - GREEN state achieved: `Passed: 3, Failed: 0` for Phase24 filter
   - Full suite: 608 passed / 0 failed

_Note: This is a TDD plan; commits follow the `test(...)` → `feat(...)` gate sequence._

## Files Created/Modified

- `flow-lang.Tests/Unit/Phase24/ParseSessionPragmaFacts.cs` — **Created.** 3 xUnit Facts pinning that ParseSession populates `Ast.Pragmas` and that `enable hAsB;` works under LSP.
- `flow-lsp/ParseSession.cs` — **Modified.** Body of `ParseResult Parse(string source, string? path)` widened from 3 lines (lex → parse → return) to 6 lines (pragma-scan → lex with pragmaSet → parse with pragmaSet → return). Class declaration, namespace, XML doc, and `ParseResult` record untouched.

### Before/After Diff (4-line widen)

**Before** (`flow-lsp/ParseSession.cs:18-24`):
```csharp
public ParseResult Parse(string source, string? path)
{
    var er = new ErrorReporter();
    var tokens = new SimpleLexer(source, er, path).Tokenize();
    var ast = new Parser(tokens, er).Parse();
    return new ParseResult(ast, tokens, er.Errors.ToList());
}
```

**After** (`flow-lsp/ParseSession.cs:18-34`):
```csharp
public ParseResult Parse(string source, string? path)
{
    var er = new ErrorReporter();
    // Phase 24 Wave 0 (Plan 24-00): mirror FlowEngine.Run() pragma-scan-then-parse
    // pipeline so Program.Pragmas reflects file-scope `enable <pragma>;` declarations.
    // ... (block comment trimmed) ...
    var (pragmaSet, transformedSource) = PragmaScanner.Scan(source, path, er);
    var tokens = new SimpleLexer(transformedSource, er, path, pragmaSet).Tokenize();
    var ast = new Parser(tokens, er, pragmaSet).Parse();
    return new ParseResult(ast, tokens, er.Errors.ToList());
}
```

The widen mirrors `FlowLang.Core.FlowEngine.Run()` lines 66-82 verbatim **except** the LSP variant deliberately drops both `if (_errorReporter.HasErrors) return false;` short-circuits — the soft-failure error model (Phase 17 D-06) requires every downstream stage to run on a partial AST so analyzer / completion / hover continue working mid-edit.

## Decisions Made

1. **Soft-failure preserved between stages.** The LSP variant intentionally omits the two `er.HasErrors` short-circuits that `FlowEngine.Run()` uses. Editor-time analysis must run on partial parses to support live diagnostics, completion, hover, and signature help — Phase 17 D-06 anchors this. Documented in the inline block comment so Wave 1+ planners do not "fix" the omission.
2. **Test 1 uses `enable hAsB;` instead of `enable scaleLint;`.** The plan calls out that `scaleLint` is not yet a known pragma at Wave 0 — it is added by Plan 24-01. Using a Phase 21 known pragma (`hAsB`) isolates the pragma-scan widening from the registry-add change, preventing a RED cascade when Wave 0 closes and Wave 1 starts. The Fact still proves the exact thing the plan needed to prove (Pragmas reflects source).
3. **No new `using` directive needed.** The existing `using FlowLang.Lexing;` at the top of `ParseSession.cs` already exposes `PragmaScanner`, `SimpleLexer`, and `PragmaSet` — Edit kept the file's `using` block untouched, minimizing diff surface.
4. **Zero-touch flow-lang invariant.** Wave 0's plan explicitly prohibited any change under `flow-lang/`. Verified: only `flow-lsp/ParseSession.cs` and `flow-lang.Tests/Unit/Phase24/` were modified. Phase 18 byte-identical determinism gate is therefore untouched.

## Deviations from Plan

**None — plan executed exactly as written.**

The plan specified the exact file to create, exact method body to replace, exact assertion text, and exact verification grep. All five acceptance criteria for Task 0 (file exists, three method names verbatim, `using FlowLang.Tests.Unit.Phase17;`, namespace declaration, `Failed: 2, Passed: 1` RED state) and all six acceptance criteria for Task 1 (`PragmaScanner.Scan` count = 1, `new SimpleLexer.*pragmaSet`, `new Parser.*pragmaSet`, `dotnet build` exits 0, Phase24 filter 3/3 passed, full suite exits 0) were met without adjustment.

---

**Total deviations:** 0
**Impact on plan:** Plan executed verbatim. No scope creep, no auto-fixes, no architectural shifts.

## Issues Encountered

None. The plan's interface section quoted the exact API signatures for `PragmaScanner.Scan`, the 4-arg `SimpleLexer` ctor, and the 3-arg `Parser` ctor — no API drift, no compile errors on first build.

## Phase 17 + Phase 21 Latent-Bug Closure

This plan silently closes two latent issues that were never their own bug reports:

| Issue | Origin | How Wave 0 closes it |
|-------|--------|----------------------|
| `Program.Pragmas` always `PragmaSet.Empty` in LSP | Phase 17 ParseSession ships with 2-arg Parser ctor; pragma-scan never invoked | Wave 0 widen runs `PragmaScanner.Scan` upstream; `pragmaSet` threaded through |
| `enable hAsB;` ineffective in LSP-edited files | Phase 21 D-08 wired pragmaSet into Program but Phase 17 ParseSession never picked it up; lexer never saw the pragma so `H4q` lexed as identifier | Same widen — lexer now receives `pragmaSet` and triggers H→B canonicalization |

Both closures are pinned by Facts in `ParseSessionPragmaFacts.cs`, so future LSP refactors cannot silently regress them.

## Next Phase Readiness

- **Plan 24-01 (registry add):** `Ast.Pragmas` is now populated → can add `scaleLint` to `PragmaRegistry` and immediately have it observable from the LSP.
- **Plan 24-02 (spellings helper), 24-03 (analyzer), 24-04 (wiring), 24-05 (closure):** All can use `parseResult.Ast.Pragmas.Has("scaleLint")` as the activation gate per D-19.
- **No blockers.** Build green, full test suite green, zero-touch flow-lang preserved.

## Self-Check: PASSED

Verified post-write:

- `flow-lang.Tests/Unit/Phase24/ParseSessionPragmaFacts.cs` exists (3 method names confirmed via grep).
- `flow-lsp/ParseSession.cs` modified: 1 occurrence of `PragmaScanner\.Scan`, 1 occurrence of `new SimpleLexer(...pragmaSet)`, 1 occurrence of `new Parser(...pragmaSet)`.
- Commits exist on the worktree branch: `7346b3b` (test) and `6bcc697` (feat).
- `dotnet build` exits 0; `dotnet test --filter "FullyQualifiedName~ParseSessionPragmaFacts"` reports 3/3 passed; full `dotnet test` reports 608/608 passed.

---
*Phase: 24-scale-linting-flow-lsp*
*Plan: 00*
*Completed: 2026-05-04*
