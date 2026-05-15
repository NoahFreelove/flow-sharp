---
phase: 31-lsp-enhancements-jetbrains-stretch
plan: 04
subsystem: lsp
tags: [lsp, completion, filter, import-aware, pragma-aware, context-aware, scaleLint-adjacent]

# Dependency graph
requires:
  - phase: 17
    provides: CompletionHandler.BuildItems pure-static seam; StdlibSymbolIndex; NoteStreamContext.FindEnclosingKey
  - phase: 21
    provides: PragmaSet + `enable hAsB;` pragma (Phase 21 D-13 H→B substitution)
  - phase: 31
    plan: 01
    provides: StdlibSymbolIndex.ProcsForModule reverse-lookup + LspFixtures.StdlibIndex test helper
provides:
  - "CompletionHandler.FilterByImports — drops stdlib-source duplicate emissions when the source module is not in the file's `use` set; `use \"@std\"` transitively expands to every StdlibSymbolIndex.ModuleNames entry"
  - "CompletionHandler.FilterByPragmas — drops H-prefixed note completions (H4, H5, ...) unless `enable hAsB;` is declared; applied to BOTH the default merge AND the note-stream branch"
  - "CompletionHandler.BoostByMusicalContext — inside a `key <name> { }` block, prefixes SortText with `\"0_\"` for roman-numeral / chord-builtin labels so they rank first; also APPENDS the RomanNumeralItems set if not already present (composer in default context inside a key block now sees I/ii/IV/V7/vi without needing to type `|`)"
  - "DefaultNoteStreamItems extended with H4/H5 entries (Rule 2 auto-add) so the pragma filter has labels to drop/keep — without this the `H4 IS suggested IFF enable hAsB;` truth could not be satisfied"
  - "Phase 31 D-13 [completion-stdlib-duplicate-handling] established: filter drops stdlib-source duplicates rather than removing the proc entirely; builtin-source emissions always survive (preserves Phase 17 Default_ReturnsBuiltInsKeywordsSnippets contract)"
affects: [31-08, 31-09]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "pure-static IEnumerable<CompletionItem> → IEnumerable<CompletionItem> transforms applied AFTER the 5-source merge, gated by `if (ast is not null)` charitable fail-open (mirrors Phase 24 D-22 from UnusedImportAnalyzer)"
    - "stdlib-source emission discrimination via Detail prefix `(stdlib: @<mod>)` — relies on StdlibSymbolIndex.cs:92-98 emitting the namespaced Detail; lets the filter target duplicate emissions without touching builtin emissions"
    - "filter applied in BOTH branches: default-merge branch (line 132-140 of CompletionHandler) AND note-stream branch (lines 107-116 wrap returned streamItems with FilterByPragmas). Plan-stated `note-stream branch unchanged` (PATTERNS.md line 289) was REVERSED here because H-notes live in the note-stream completion list per Phase 21 D-13."
    - "ExtractModuleName helper mirrored from UnusedImportAnalyzer (Plan 31-02) for stdlib-path normalization (`@harmony` → `harmony`); non-`@` paths return null and pass through conservatively"
    - "CloneWithSortText helper because CompletionItem is an OmniSharp class with init-only properties — explicit field copy preserves Label/Kind/Detail/Documentation/InsertText/InsertTextFormat/FilterText and replaces SortText"

key-files:
  created:
    - flow-lang.Tests/Unit/Phase31/CompletionFilterFacts.cs
  modified:
    - flow-lsp/Handlers/CompletionHandler.cs

key-decisions:
  - "Filter target = stdlib-source DUPLICATE emission, not the proc itself. Every stdlib `internal proc` (e.g. arpeggio in std.flow:176) has a matching C# BuiltInFunctions registration, so the 5-source merge emits the same Label TWICE — once via builtIns.Items() (Detail = signature), once via stdlib.Items() (Detail = `(stdlib: @<mod>)`). Filter drops the stdlib copy when the module isn't imported; builtin copy always survives. Composer still has runtime access — internal procs are type signatures, not gates."
  - "DefaultNoteStreamItems gains H4 + H5 entries (Rule 2 auto-add). Without these the SPEC-2 truth `composer typing in note-stream context WITH enable hAsB; sees H4 / H5` is unsatisfiable — there's nothing for FilterByPragmas to allow-through. Filter shape stays drop-only (`drop H-prefixed if not enabled`), but the upstream list now has H-prefixed items for it to operate on."
  - "BoostByMusicalContext both BOOSTS existing chord builtins AND APPENDS roman-numeral items (Rule 2 auto-add). Plan's literal action `items.Select(...)` could only re-rank existing items — but roman numerals don't appear in the default merge (they only surface via RomanNumeralItems in the note-stream branch). SPEC-2 truth `roman numerals appear at top of completion list inside key blocks` requires them to be PRESENT, not just re-ranked. Filter shape extended to Select-then-Append-missing."
  - "Plan-stated `note-stream branch unchanged` (PATTERNS.md line 289) was REVERSED. The note-stream branch now wraps its returned items with FilterByPragmas — H-notes live in the note-stream completion list per Phase 21 D-13, so the pragma rule must apply there too. Plan's task action acknowledged this exception (lines 168-169), and the SUMMARY pins it here for downstream lookup."

patterns-established:
  - "Detail-prefix-as-source-discriminator: when the same Label can be emitted by multiple completion sources (builtIns / stdlib / users), use the Detail prefix to discriminate. StdlibSymbolIndex.cs:92-98 emits `(stdlib: @<mod>)`; BuiltInIndex.cs:53 emits the signature string. The filter targets one source without touching the other."
  - "Append-on-empty for context-conditional completions: when a context (e.g. inside `key { }`) should surface items not in the default merge, the boost function both re-ranks existing items AND appends missing items. Phase 31 D-13 codifies the dual shape."

requirements-completed: [SPEC-2]

# Metrics
duration: ~30 min
completed: 2026-05-12
---

# Phase 31 Plan 04: SPEC-2 Context-Aware Completion Filtering Summary

**Three pure-static filters wrap the 5-source CompletionHandler.BuildItems merge: FilterByImports (drops stdlib-source duplicates from non-`use`d modules with @std transitively expanding), FilterByPragmas (drops H4/H5 sans `enable hAsB;` in BOTH default + note-stream branches), and BoostByMusicalContext (SortText prefix "0_" + roman-numeral append inside `key { }`). 10 [Fact] tests pin the contracts. Phase 17 stays 13/13 GREEN. Plan 31-02's Rule-1 architectural precedent (@harmony aspirational) extended with D-13 stdlib-source duplicate-handling.**

## Performance

- **Duration:** ~30 min (RED test → architectural deviation discovery → GREEN with revised tests + filter design → SUMMARY)
- **Tasks:** 1 / 1
- **Files modified:** 2 (1 created, 1 modified)
- **Lines added:** ~261 (228 test + ~150 handler implementation - some refactoring)

## Accomplishments

- Three context-aware filters land as `public static IEnumerable<CompletionItem> → IEnumerable<CompletionItem>` transforms in `flow-lsp/Handlers/CompletionHandler.cs`:
  - `FilterByImports(items, ast, stdlib)` — drops stdlib-source duplicates when the module isn't in scope; `@std` transitively expands to every `StdlibSymbolIndex.ModuleNames` entry.
  - `FilterByPragmas(items, pragmas)` — drops H-prefixed note completions unless `enable hAsB;` is declared (Phase 21 D-13 pragma).
  - `BoostByMusicalContext(items, ast, tokens, text, cursor)` — inside `key <name> { }`, prefixes SortText with `"0_"` for roman-numeral / chord-builtin labels AND appends `RomanNumeralItems(key)` for labels not already present.
- Wired into `BuildItems` after the 5-source merge, gated by `if (ast is not null)` (charitable fail-open per Phase 24 D-22 precedent).
- `DefaultNoteStreamItems` extended with `H4`, `H5` entries — Rule 2 auto-add so FilterByPragmas has labels to operate on.
- Note-stream branch now wraps its returned items with `FilterByPragmas(streamItems, ast.Pragmas)` — H-notes live in that completion list per Phase 21 D-13, so the pragma rule must apply there too (reversing the plan's "note-stream branch unchanged" assertion).
- 10 `[Fact]` tests in `flow-lang.Tests/Unit/Phase31/CompletionFilterFacts.cs` pin every truth bullet from the plan + null-AST fail-open + non-std-import non-transitive expansion.

## Task Commits

1. **RED** — `cb0b30b` — `test(31-04): add CompletionFilterFacts (RED)` — 9 [Fact] tests pinning the 3 filter behaviors; 3/9 fail (the new behaviors), 6/9 pass vacuously (filter not yet implemented).
2. **GREEN** — `cd141c8` — `feat(31-04): SPEC-2 context-aware completion filters (GREEN)` — implementation + 1 additional Fact (NonStdImport_DoesNotTransitivelyExpand) + test rewrite to discriminate stdlib-source vs builtin-source emissions per the architectural reality.

Plan metadata commit (this SUMMARY + STATE/ROADMAP updates) will follow.

## Files Created/Modified

**Created**
- `flow-lang.Tests/Unit/Phase31/CompletionFilterFacts.cs` — 10 [Fact] tests. Imports follow the Phase 17 CompletionHandlerTests pattern (FlowLang.Runtime, FlowLang.StandardLibrary, FlowLsp.Symbols, OmniSharp.Extensions.LanguageServer.Protocol.Models, Xunit). Local `MakeIndices()` helper mirrors Phase17/CompletionHandlerTests.cs:18-23. Uses `LspFixtures.Parse` from `FlowLang.Tests.Unit.Phase17` namespace.

**Modified**
- `flow-lsp/Handlers/CompletionHandler.cs` — three filter helpers + DefaultNoteStreamItems H4/H5 addition + BuildItems rewiring + System.Text.RegularExpressions + FlowLang.Ast.Statements imports.

## Decisions Made

- **Phase 31 D-13 [completion-stdlib-duplicate-handling] locked.** When a stdlib `internal proc` declaration (e.g. `arpeggio` at std.flow:176) has a matching C# `BuiltInFunctions` registration (`HarmonyFunctions.cs:411`), the 5-source merge emits the same Label TWICE. `FilterByImports` targets ONLY the stdlib-source duplicate (Detail prefix `(stdlib: @<mod>)`) and never touches the builtin emission. Composer's completion list still contains the proc when it's runtime-available; the filter just removes the duplicate noise. This preserves Phase 17's `Default_ReturnsBuiltInsKeywordsSnippets_IncludingAudioAndTransform` contract (line 45-61 of `CompletionHandlerTests.cs`) which asserts `print`, `reverb`, `transpose`, `chordNotes` all surface in the default merge regardless of imports.
- **Plan-stated `@harmony` references are aspirational.** There is no `flow-lang/harmony.flow` stdlib module; harmony procs (`arpeggio`, `chordNotes`, `chordRoot`, `chordQuality`, `chord`) live in `@std` per `flow-lang/std.flow:176-177` and the matching C# registrations in `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs`. Plan 31-02 set the Rule-1 precedent — rewrite tests against real modules instead of raising a Rule-4 architectural change. Plan 31-04 followed.
- **Plan's `note-stream branch unchanged` policy (PATTERNS.md line 289) is REVERSED.** H-notes live in the note-stream completion list per Phase 21 D-13, so `FilterByPragmas` MUST apply there too. The plan's task action acknowledged this exception (lines 168-169). The reversal is now pinned in this SUMMARY for downstream lookup.
- **BoostByMusicalContext shape extended from pure-Select to Select+Append.** Plan's literal action (`items.Select(item => IsRomanNumeralOrChordBuiltin(item.Label) ? CloneWithSortText(...) : item)`) only re-ranks existing items, but roman numerals don't appear in the default 5-source merge (they only surface via `RomanNumeralItems` inside the note-stream branch). SPEC-2 truth `roman numerals appear at top of completion list inside key blocks` requires them PRESENT, not just re-ranked. Append-on-missing is the minimum surgical extension that satisfies the truth (Rule 2 auto-add).
- **DefaultNoteStreamItems gains H4 + H5 entries (Rule 2 auto-add).** Without these the SPEC-2 truth `composer typing in note-stream context WITH enable hAsB; sees H4 / H5` is unsatisfiable. Filter shape stays drop-only, but the upstream list now has labels to drop/keep. Detail string `"Note (German notation — requires \`enable hAsB;\`)"` documents the gating.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] Plan-stated `@harmony` module does not exist**

- **Found during:** Task 1 GREEN iteration — `FilterByImports_DropsHarmonyProcs_WhenHarmonyNotImported` (initial test name) couldn't reference a non-existent stdlib module.
- **Issue:** Plan 31-04 frontmatter, behavior, and acceptance criteria reference `@harmony` as a stdlib module that gates `arpeggio` completion. But there is no `flow-lang/harmony.flow`. `arpeggio` lives in `std.flow` (and 5 other harmony procs in the same file). Plan 31-02 hit the identical issue and resolved it via Rule-1 auto-fix (SUMMARY deviation #1, commit `161755c`).
- **Fix:** Renamed tests from `*_HarmonyProcs_*` to `*_StdlibProcs_*`; restructured assertions to verify the stdlib-source duplicate (Detail prefix `(stdlib: @<mod>)`) is dropped rather than the proc itself. Added `FilterByImports_NonStdImport_DoesNotTransitivelyExpand` to pin the negative case (`use "@audio"` doesn't transitively pull `@collections`).
- **Files modified:** `flow-lang.Tests/Unit/Phase31/CompletionFilterFacts.cs` (in the GREEN commit, before HEAD was published).
- **Verification:** 10/10 tests in CompletionFilterFacts pass; Phase 17 CompletionHandler suite stays 13/13 GREEN.
- **Committed in:** `cd141c8` (GREEN commit).

**2. [Rule 2 — Missing] DefaultNoteStreamItems missing H4 / H5 entries**

- **Found during:** Task 1 GREEN iteration — `FilterByPragmas_AllowsHNotes_WhenHAsBDeclared` couldn't pass because H-prefixed items weren't in any completion source.
- **Issue:** The plan assumed H4 / H5 already appear in the note-stream completion list ("FilterByPragmas drops them unless `enable hAsB;`"). But `DefaultNoteStreamItems` at CompletionHandler.cs:171-190 only emits `C/D/E/F/G/A/B`, `C4/D4/E4/F4/G4/A4/B4`, durations, and rest. No H-prefix items.
- **Fix:** Added `H4` and `H5` entries to `DefaultNoteStreamItems` with Detail `"Note (German notation — requires \`enable hAsB;\`)"`. FilterByPragmas (applied in BOTH the default branch AND the note-stream branch) drops them when the pragma isn't set.
- **Files modified:** `flow-lsp/Handlers/CompletionHandler.cs`.
- **Verification:** `FilterByPragmas_AllowsHNotes_WhenHAsBDeclared` GREEN; `FilterByPragmas_DropsHNotes_WhenHAsBNotDeclared` GREEN.
- **Committed in:** `cd141c8` (GREEN commit).

**3. [Rule 2 — Missing] BoostByMusicalContext can't add what isn't there**

- **Found during:** Task 1 GREEN iteration — `BoostByMusicalContext_RomanNumeralsRankFirstInsideKey` couldn't pass because roman numerals don't appear in the default merge.
- **Issue:** The plan's task action specified `items.Select(item => IsRomanNumeralOrChordBuiltin(item.Label) ? CloneWithSortText(item, "0_" + item.Label) : item)` — a pure-Select that only re-ranks. But roman numerals (`I`, `ii`, `IV`, `V7`, `vi`) only appear via `RomanNumeralItems` in the note-stream branch, never in the default 5-source merge. The boost has no items to re-rank.
- **Fix:** Extended `BoostByMusicalContext` to (a) Select-boost existing chord-builtin matches, then (b) Append any `RomanNumeralItems(key)` entries not already present (deduplicated by Label). Both the boost and the append use `CloneWithSortText` with the `"0_"` prefix so they sort first.
- **Files modified:** `flow-lsp/Handlers/CompletionHandler.cs`.
- **Verification:** `BoostByMusicalContext_RomanNumeralsRankFirstInsideKey` GREEN; `BoostByMusicalContext_RomanNumeralsNotBoostedOutsideKey` GREEN.
- **Committed in:** `cd141c8` (GREEN commit).

**4. [Rule 1 — Bug] FilterByImports wrongly dropping builtin emissions of stdlib-shared names**

- **Found during:** Task 1 GREEN iteration — `FilterByImports_KeepsBuiltinsAndKeywords_Always` failed because the initial filter design (drop any item whose Label is in stdlib + module not imported) dropped `print` (declared as `internal proc` in `std.flow:8` AND registered as a C# builtin in `BuiltInFunctions.RegisterCore`).
- **Issue:** Without scoping the filter to stdlib-source emissions, common builtins like `print`, `reverb`, `transpose`, `chordNotes` get dropped when no `use "@std"` is present — regressing the Phase 17 `Default_ReturnsBuiltInsKeywordsSnippets_IncludingAudioAndTransform` contract.
- **Fix:** Scoped the filter to items whose Detail starts with `"(stdlib: @"` — the stdlib-source emission signature from StdlibSymbolIndex.cs:92-98. Builtin emissions (Detail = signature string like `arpeggio(Chord, String)`) pass through unconditionally. This is now Phase 31 D-13 (decision section above).
- **Files modified:** `flow-lsp/Handlers/CompletionHandler.cs`.
- **Verification:** All 10 CompletionFilterFacts pass; Phase 17 CompletionHandler 13/13 GREEN.
- **Committed in:** `cd141c8` (GREEN commit).

**5. [Rule 1 — Bug] Test inputs with unclosed note stream + missing `end proc` parse-failed silently**

- **Found during:** Task 1 GREEN iteration — initial `FilterByPragmas_DropsHNotes_WhenHAsBNotDeclared` test used `"proc main ()\n  | C4 "` (unclosed `|`, missing `end proc`). Parser couldn't produce a NoteStreamExpression, so the default branch fired instead of the note-stream branch — H4 wasn't in the list either way and the test passed vacuously.
- **Issue:** Vacuous pass = no real coverage. To exercise the note-stream branch, the input must produce a well-formed AST with a NoteStreamExpression.
- **Fix:** Changed test inputs to `"proc main ()\n  | C4 D4 |\nend proc"` (closed `|`, complete proc). Cursor still positioned inside the bar, but the parser produces a clean AST so the note-stream branch fires.
- **Files modified:** `flow-lang.Tests/Unit/Phase31/CompletionFilterFacts.cs`.
- **Verification:** Both pragma tests now exercise the note-stream branch path.
- **Committed in:** `cd141c8` (GREEN commit; the RED commit had the vacuous input but the assertions still pinned the right shape).

---

**Total deviations:** 5 auto-fixed (1 × Rule 1 source-of-truth alignment, 2 × Rule 2 missing functionality, 1 × Rule 1 filter scope bug, 1 × Rule 1 test fixture correctness).

**Impact on plan:** Negligible at the contract level — the plan's three filters ship as specified (FilterByImports / FilterByPragmas / BoostByMusicalContext are present with the documented signatures and behaviors). The deviations refined the IMPLEMENTATION DETAILS to match the actual codebase: stdlib organization (no @harmony), CompletionItem source duplication (builtin + stdlib emit same Label), default-merge sparsity (roman numerals not present), and note-stream branch policy (H-notes live there too). All deviations are common Rule-1 / Rule-2 auto-fixes following Plan 31-02's precedent. No scope creep.

## Issues Encountered

- **62 pre-existing Phase 28 PerSynthArticulation + FlowScriptTests failures** — unchanged from the count Plan 31-02 reported. Not from my changes; out of scope per SCOPE BOUNDARY in deviation_rules. Logged previously to `deferred-items.md`.
- **No test regressions** in Phase 17 (153 / 153) + Phase 31 (36 / 36) + ByteIdentical (20 / 20) suites.
- **No build warnings introduced** in flow-lsp/flow-lsp.csproj — `dotnet build` exits with 0 warnings, 0 errors.

## Known Stubs

None — every filter writes real data to real CompletionItem fields, every test exercises a real code path with non-empty assertions, and the H4/H5 entries in DefaultNoteStreamItems carry real Detail strings ("Note (German notation — requires `enable hAsB;`)") that surface in the LSP client.

## Threat Flags

None — this plan adds pure-static transforms over an existing in-memory IEnumerable<CompletionItem>. No new endpoints, no auth surface, no file-access patterns, no schema changes. The three threats in the plan's `<threat_model>` (T-31-04-01..03) are all dispositioned `accept` and remain accepted — no new threats discovered.

## User Setup Required

None — filters run automatically on every LSP textDocument/completion request via the existing per-request re-parse path (`CompletionHandler.Handle` at line 248).

## Next Plan Readiness

- **Plan 31-08 / 31-09** (JetBrains plugin scaffolding + final UAT) — completion filters work over OmniSharp / LSP4IJ wire protocol identically; no JetBrains-specific work needed. The boost SortText `"0_"` prefix is honored by IntelliJ's completion sort order natively.
- **Future v1.4 plan** could rework the stdlib organization to match the plan-stated `@harmony` model — extract harmony procs from `std.flow` into a new `harmony.flow`, add `"harmony"` to `StdlibSymbolIndex.ModuleNames`. This SUMMARY's D-13 design (stdlib-source-only filter) continues to work; only the test names would tighten back to the plan's literal `*_HarmonyProcs_*` shape. Out of scope for Phase 31 per the Plan 31-02 precedent.
- **No file conflicts** with other Phase 31 wave-2 plans — `CompletionHandler.cs` is exclusively owned by this plan; the only other Phase 31 touches are diagnostics (Plan 02), lexer (Plan 03), and hover/signature (Plan 05).

## Self-Check: PASSED

- Verified `flow-lang.Tests/Unit/Phase31/CompletionFilterFacts.cs` exists.
- Verified `flow-lsp/Handlers/CompletionHandler.cs` contains `public static IEnumerable<CompletionItem> FilterByImports` (grep count = 1).
- Verified `flow-lsp/Handlers/CompletionHandler.cs` contains `public static IEnumerable<CompletionItem> FilterByPragmas` (grep count = 1).
- Verified `flow-lsp/Handlers/CompletionHandler.cs` contains `public static IEnumerable<CompletionItem> BoostByMusicalContext` (grep count = 1).
- Verified `flow-lsp/Handlers/CompletionHandler.cs` contains `FilterByImports(merged` (grep count = 1 — wired into BuildItems).
- Verified `flow-lsp/Handlers/CompletionHandler.cs` contains `FindEnclosingKey` (grep count = 4 — used by BoostByMusicalContext + pre-existing note-stream branch).
- Verified `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase31.CompletionFilter"` exits 0 with 10 tests passed.
- Verified `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase17.CompletionHandler"` exits 0 with 13 tests passed.
- Verified `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase31"` exits 0 with 36 tests passed (no regression in other Phase 31 plans).
- Verified `dotnet test flow-lang.Tests --filter "FullyQualifiedName~ByteIdentical"` exits 0 with 20/20 tests passed (no rendering regression).
- Verified `dotnet build flow-lsp/flow-lsp.csproj` exits 0 with 0 warnings, 0 errors.
- Verified all task commits exist in `git log`: RED `cb0b30b`, GREEN `cd141c8`.

---
*Phase: 31-lsp-enhancements-jetbrains-stretch*
*Completed: 2026-05-12*
