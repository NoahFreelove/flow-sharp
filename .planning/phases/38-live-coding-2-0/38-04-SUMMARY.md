---
phase: 38-live-coding-2-0
plan: 04
subsystem: repl
tags: [repl, lsp, prettyprompt, visualize, inspect, articulation]
requires:
  - 38-01  # flow-lsp ProjectReference + LiveStatusPanel infrastructure
provides:
  - repl-line-editor-prettyprompt
  - repl-tab-completion-in-process-lsp
  - repl-help-meta-command
  - repl-history-persistent-0600
  - visualize-articulation-glyphs
  - visualize-tick-mark-row
  - inspect-alias-builtin
affects:
  - flow-interpreter/Repl.cs
  - flow-lang/StandardLibrary/VisualizationFunctions.cs
tech-stack:
  added:
    - "PrettyPrompt 4.1.1 (NuGet, MPL-2.0, .NET 6+, by waf — verified live on NuGet API 2026-05-23)"
  patterns:
    - "In-process LSP embed via static CompletionHandler.BuildItems() (D-38-12 SIMPLIFICATION FINDING per RESEARCH §G — no MemoryStream LanguageServer plumbing)"
    - "PrettyPrompt IPromptCallbacks override + 1:1 LspCompletionItem → PrettyPrompt CompletionItem conversion"
    - "Lexer-based paren/brace/bracket-balance check shared between legacy Console.ReadLine and PrettyPrompt paths (ReplInputCompleteness static helper)"
key-files:
  created:
    - flow-interpreter/ReplLineEditor.cs (341 LOC)
    - flow-lang.Tests/Integration/Phase38/ReplCompletionTests.cs
    - flow-lang.Tests/Integration/Phase38/ReplHelpMetaCommandTests.cs
    - flow-lang.Tests/Integration/Phase38/ReplMultiLineTests.cs
    - flow-lang.Tests/Integration/Phase38/ReplHistorySearchTests.cs
    - flow-lang.Tests/Integration/Phase38/VisualizeArticulationGlyphTests.cs
    - flow-lang.Tests/Integration/Phase38/InspectAliasTests.cs
    - flow-lang.Tests/Integration/Phase38/GlyphCollisionTests.cs
  modified:
    - flow-interpreter/Repl.cs (HandleCommand `:help <name>` arm + ShowHelpForName + HandleCommandForTesting test seam + ShowHelp menu line)
    - flow-interpreter/flow-interpreter.csproj (PrettyPrompt 4.1.1 PackageReference)
    - flow-lang/StandardLibrary/VisualizationFunctions.cs (articulation glyph switch + Legato gap-fill + tick-mark row + inspect alias)
decisions:
  - "D-38-09 honored: `:help <name>` meta-command form on existing :quit/:help/:clear/:stop family (REQUIREMENTS.md REPL-02 `?fn` wording OVERRIDDEN — composer updates pending at Plan 38-07 closer)"
  - "D-38-10 honored: (visualize seq) extension + (inspect seq) alias dispatching to same Visualize body (REQUIREMENTS.md REPL-04 wording OVERRIDDEN — alias pair ships both names)"
  - "D-38-11 honored: PrettyPrompt 4.1.1 selected over ReadLine 2.0.1 — ReadLine lacks both Ctrl+R reverse search AND multi-line input (REPL-03 deal-breaker); ReadLine inactive since 2017"
  - "D-38-12 honored AND simplified: in-process LSP via STATIC CompletionHandler.BuildItems() call (RESEARCH §G SIMPLIFICATION) — no MemoryStream LanguageServer plumbing needed (BuildItems is already public static + transport-decoupled)"
  - "Multi-line continuation extended beyond brace+proc-depth to ALSO cover paren + bracket nesting (Rule 2 auto-add per PLAN behavior — `(add 1` and `[intro verse` are common composer continuation points; the legacy Repl.cs:182-208 brace-only check missed these)"
metrics:
  duration_iso: "PT1H40M"
  completed: "2026-05-24"
  task_count: 3
  commit_count: 3
  test_count: 7
  files_created: 8
  files_modified: 3
---

# Phase 38 Plan 38-04: REPL Polish (REPL-01..04) Summary

PrettyPrompt 4.1.1 + in-process flow-lsp tab completion + `:help <name>` meta-command + articulation glyphs + bar tick marks + `(inspect seq)` alias — REPL surface shipped per D-38-09/10/11/12.

## Deliverables

### REPL Line Editing
- **`flow-interpreter/ReplLineEditor.cs`** (341 LOC) wraps `PrettyPrompt.Prompt 4.1.1` (D-38-11) with `FlowPromptCallbacks`:
  - **Tab completion** routes through `FlowLsp.Handlers.CompletionHandler.BuildItems(...)` static helper per D-38-12 SIMPLIFICATION FINDING (RESEARCH §G lines 854-929). The 4 symbol indices (`BuiltInIndex` / `StdlibSymbolIndex` / `KeywordIndex` / `UserSymbolIndex`) are constructed ONCE at ctor time so each Tab does not pay the cold-load cost.
  - **Multi-line continuation**: bare Enter on unbalanced input transforms to Shift+Enter soft-newline (PrettyPrompt convention). Preserves Repl.cs:117-119 + 182-208 paren-balance + backslash-EOL contract.
  - **Ctrl+R reverse history search** comes for free via PrettyPrompt's `persistentHistoryFilepath` ctor parameter pointing at `~/.config/flow/history`.
  - **History file** at `~/.config/flow/history` per UI-SPEC line 297; 10k cap with rotation-on-append; **0600 mode on Linux/macOS** per UI-SPEC line 300.

### `:help <name>` Meta-Command (D-38-09)
- Extends `Repl.HandleCommand` switch at lines 210-220 with a `:help <name>` prefix branch.
- On hit: renders 3-block layout per UI-SPEC lines 263-280 — `\x1b[1m\x1b[32m<name>\x1b[0m` (bold+green header), `\x1b[2m(<name> p1 p2)\x1b[0m` (dim signature), default-attribute body, dim `Example:` label + generic one-liner using param names.
- On miss: emits locked yellow advisory `[help] no documentation entry for '<name>' — try ':help' for the meta-command list` per UI-SPEC line 289.
- ShowHelp text appends `:help <name>` discovery line per UI-SPEC line 362.
- `HandleCommandForTesting` public test seam exposed for xUnit coverage.

### `ReplInputCompleteness` Static Helper
- Extracted from Repl.cs:182-208 so the legacy `Console.ReadLine` path AND the new PrettyPrompt path call ONE shared implementation (Pitfall avoidance — drift between the two would silently break multi-line detection).
- Extends the existing brace + proc-depth check with **LParen/RParen + LBracket/RBracket** nesting (Rule 2 auto-add): `(add 1` and `[intro verse` are common composer continuation points the brace-only check missed.

### `(visualize seq)` Extension + `(inspect seq)` Alias (D-38-10)
- `flow-lang/StandardLibrary/VisualizationFunctions.cs`:
  - Note-placement loop carries the Phase 28 `Articulation` enum off `MusicalNoteData`; switch maps:
    | Articulation | Glyph |
    |---|---|
    | Accent | `>` |
    | Staccato | `.` |
    | Marcato | `^` |
    | Tenuto | `_` |
    | Sforzando | `!` |
    | Normal | `#` (pre-Phase-38 baseline) |
  - Onset glyph fills `startCol`; sustain cells stay `#` per UI-SPEC line 210.
  - Single-cell collapse per UI-SPEC line 211 — short notes get `endCol = startCol + 1` so the onset glyph is always visible.
  - **Legato gap-fill pass** (UI-SPEC line 212): for each Legato note, find a prior note on the same row that ends immediately before this onset and fill the gap cell with `~`. Charitable skip on no-gap per D-v1.5-05.
  - **Tick-mark row** (UI-SPEC lines 217-228): rendered ABOVE the first pitch row using the existing bottom-separator `+`/`-` shape; `+` at each bar-line column, `-` elsewhere.
  - Cell-output switch: onset glyphs (`>./^_!~`) win over sustain per UI-SPEC line 213; bar lines `|` win over sustain `#` per line 214.
- `Register()` adds an `inspect(Sequence)` signature dispatching to the SAME `Visualize` body per PATTERNS line 808 ("Same dispatch") — composer can call either name; identical output.

### Tests (7 Wave 0)
- `ReplCompletionTests` (2 tests): Tab on `(transp` returns `transpose`; Tab on empty input returns keyword + builtin merge.
- `ReplHelpMetaCommandTests` (2): `:help transpose` prints header + body + Example; `:help fooBar` emits locked advisory.
- `ReplMultiLineTests` (3): unbalanced parens / backslash-EOL request continuation; balanced single-line submits.
- `ReplHistorySearchTests` (3): file load order most-recent-first; 0600 mode on Linux/macOS; append persists immediately.
- `VisualizeArticulationGlyphTests` (Theory + 3): all 6 Phase 28 articulations + Normal; single-cell staccato collapses to `.` alone; Normal-only sequence preserves pre-Phase-38 output.
- `InspectAliasTests` (2): inspect ≡ visualize byte-identical; signature registered.
- `GlyphCollisionTests` (2): bar-line wins; onset wins.

## Deviations from Plan

### Rule 2 — Auto-added missing functionality

**[Rule 2 — Functionality] Multi-line paren + bracket nesting beyond brace-only check**
- **Found during:** Task 2 (`ReplMultiLineTests.UnbalancedParens_RequestsContinuation` failed initially)
- **Issue:** The existing `Repl.cs:182-208` IsInputComplete logic ONLY tracked `LBrace`/`RBrace`/`Proc`/`EndProc` nesting. The Plan's stated behavior (and UI-SPEC line 257) called it "paren-balanced detection" but the implementation never actually checked parens. The test `(add 1` failed because parser-aware paren counting wasn't part of the helper.
- **Fix:** Extended `ReplInputCompleteness.IsInputComplete` to ALSO track `LParen`/`RParen` AND `LBracket`/`RBracket` nesting. `(add 1` and `[intro verse` now correctly request continuation.
- **Files modified:** `flow-interpreter/ReplLineEditor.cs` (the new `ReplInputCompleteness` helper carries the extended check; the legacy `Repl.IsInputComplete` delegates to it).
- **Commit:** `bf5a3b1`

### Rule 3 — Auto-fixed blocking issue

**[Rule 3 — Test fixture] NoteValueType.Value is an ordinal enum (0-7), not the literal duration integer**
- **Found during:** Task 3 (`SingleCellStaccato_RendersDotOnly` threw `ArgumentException: Invalid note value: 16`)
- **Issue:** `MusicalNoteData(durationValue: 16)` treats `16` as `NoteValueType.Value` cast — enum ordinals go 0=WHOLE, 1=HALF, 2=QUARTER, 3=EIGHTH, 4=SIXTEENTH. Passing 16 is out of range.
- **Fix:** Test fixtures now use `(int)NoteValueType.Value.SIXTEENTH` etc. for clarity.
- **Files modified:** All 3 visualize/inspect/glyph-collision test files.
- **Commit:** `644aeb8`

## Authentication Gates

None — Plan 38-04 ships entirely in-process; no external auth required.

## Package Legitimacy Gate

- **PrettyPrompt 4.1.1** — Verified live on NuGet flatcontainer API 2026-05-23: MPL-2.0 (file-scope copyleft compatible with Flow's MIT distribution per RESEARCH §H line 132), last published 2023-09-30, 104.4K total downloads, 199 stars on GitHub, source at `github.com/waf/PrettyPrompt`. Transitive dep TextCopy 6.2.1 (MIT). **Approved — not [SLOP]**.

## Verification

- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase38"` → **32/32 pass** (the 7 new + 25 from Plan 38-01 carryover including parameterized expansions).
- `dotnet build flow-interpreter` → **succeeds**; csproj contains `PrettyPrompt 4.1.1` PackageReference + the Plan 38-01-prep `flow-lsp` ProjectReference.
- `grep -n ':help' flow-interpreter/Repl.cs` → returns 5 lines (existing `:help` arms + new `:help <name>` arm + ShowHelp text update).
- `grep -n 'CompletionHandler.BuildItems' flow-interpreter/ReplLineEditor.cs` → 1 hit.
- `grep -n 'PromptCallbacks' flow-interpreter/ReplLineEditor.cs` → 5 hits.
- `dotnet run --project flow-interpreter tests/test_visualization.flow` → renders the new tick-mark row above each pitch grid; existing pitch+bar-line content unchanged (backwards-compat per UI-SPEC line 232).
- Full xunit suite: **1665 pass / 34 fail** — the 34 failures are pre-existing in Phase 28/29/35 audio synth + ragtime fixture suites (RMS regression / FFT cosine differentiable / sampled-articulation tests). **Zero failures in any Phase 38 / Visualize / Inspect / REPL test.** None of my changes touch the affected files.

### Manual smokes deferred to Plan 38-07
- Real REPL session: `dotnet run --project flow-interpreter` → type `:help transpose` and verify bold+green header + dim signature + body + `Example:` block render in a real terminal.
- Tab completion in TTY: type `(transp<Tab>` and confirm the completion menu surfaces.
- Ctrl+R in TTY: type previous-session input + press Ctrl+R, confirm PrettyPrompt's history search panel opens against `~/.config/flow/history`.

## Known Stubs

None. Every code path ships fully wired:
- The `ExtractDescription` helper in `ReplLineEditor` reads both `Detail` and `Documentation` from the LSP CompletionItem and concatenates non-empty values.
- The `:help <name>` example line builds a generic `(<name> p1 p2)` form from `BuiltInDocs.Doc.Params`. `BuiltInDocs.Doc` has no dedicated `Example` field today (Phase 31 ships Summary + per-param Description). When Phase 31's doc table is later augmented with an `Example` field (out of scope for Plan 38-04), the `:help <name>` renderer should be updated to prefer it.

## Threat Flags

None — Plan 38-04 touches in-process REPL surface only. The 3 threat boundaries (composer input, history file 0600, PrettyPrompt MPL-2.0 supply chain) are explicitly mitigated per `<threat_model>` rows T-38-SC / T-38-13 / T-38-HIS / T-38-09 in the plan.

## Self-Check: PASSED

- File `flow-interpreter/ReplLineEditor.cs` → **FOUND** (341 LOC)
- File `flow-lang/StandardLibrary/VisualizationFunctions.cs` → **FOUND** (extended)
- File `flow-interpreter/Repl.cs` → **FOUND** (extended with :help <name>)
- Test files under `flow-lang.Tests/Integration/Phase38/` → **7 FOUND** (Repl{Completion,HelpMetaCommand,MultiLine,HistorySearch}Tests + Visualize{ArticulationGlyph,InspectAlias,GlyphCollision}Tests... InspectAliasTests + GlyphCollisionTests + VisualizeArticulationGlyphTests)
- Commit `1a99aa9` → **FOUND** (Task 1 — PrettyPrompt csproj + 7 failing tests)
- Commit `bf5a3b1` → **FOUND** (Task 2 — ReplLineEditor + :help <name>)
- Commit `644aeb8` → **FOUND** (Task 3 — visualize extension + inspect alias)
