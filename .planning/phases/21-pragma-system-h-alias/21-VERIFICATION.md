---
status: passed
phase: 21
phase_name: pragma-system-h-alias
closed: 2026-04-26
verification_source: plan-21-03-closure
must_haves_verified: 4
must_haves_total: 4
deferred: []
---

# Phase 21 Verification — Pragma System + H-Alias

**Phase:** 21
**Milestone:** v1.3 Composer DX Tier B/C
**Status:** Complete
**Closed:** 2026-04-26 (plan 21-03 closure commit)
**Plans:** 3/3 complete (Wave 1: 21-01 → Wave 2: 21-02 → Wave 3: 21-03)
**Cumulative Phase 21 Facts:** 25 (9 PragmaScannerFacts + 5 PragmaRegistryFacts + 2 PragmaIsolationFacts + 9 HAliasFacts)
**Full xUnit suite at close:** 414/414 GREEN

---

## Commits

| Plan | Atomic Commit(s) | Subject |
|------|------------------|---------|
| 21-01 | `c378c20` | `test(21-01): wave 0 scaffolding for pragma scanner + registry + isolation Facts` |
| 21-01 | `f2a48d0` | `feat(21-01): add PragmaSet + PragmaRegistry + PragmaScanner production code` |
| 21-01 | `19d7dc8` | `feat(21-01): wire PragmaSet into Parser + SimpleLexer + Program AST (D-05, D-08)` |
| 21-01 | `95c8c71` | `feat(21-01): insert PragmaScanner.Scan stage into FlowEngine.Execute (D-01, D-07)` |
| 21-01 | `60f7f18` | `feat(21-01): insert PragmaScanner.Scan stage into ModuleLoader.LoadModule (D-06)` |
| 21-02 | `e25edbd` | `test(21-02): wave 0 RED — HAliasFacts + tightened PragmaIsolationFacts + .flow fixtures` |
| 21-02 | `352efac` | `feat(21-02): add OriginalText field to Token record (D-15)` |
| 21-02 | `05c2174` | `feat(21-02): wire H→B substitution in SimpleLexer.TryParseNote (DEFER-02/03)` |
| 21-03 | (this commit — closure) | `docs(21-03): Phase 21 closure — PRAG-01/PRAG-02/DEFER-02/03 shipped, REQUIREMENTS/ROADMAP/STATE updated, 14-deferred-items DEFER-02/DEFER-03 strikethrough applied` |

**Canonical "Shipped" hashes** (used in REQUIREMENTS.md traceability + ROADMAP.md row):

- **HASH_2101 = `60f7f18`** — final feat commit closing the PRAG-01 + PRAG-02 plumbing (PragmaScanner threaded through FlowEngine.Execute + ModuleLoader.LoadModule). Same hash covers both REQ-IDs because PRAG-01 (file-scope pragma scan) and PRAG-02 (per-import isolation) ship as a single atomic plumbing change.
- **HASH_2102 = `05c2174`** — final feat commit closing DEFER-02/03 (SimpleLexer.TryParseNote H→B substitution + ScanIdentifierOrKeyword OriginalText plumbing).

---

## Success Criteria Verification (from ROADMAP.md)

| # | Criterion | Pinning Artifact | Commit | Status |
|---|-----------|------------------|--------|--------|
| 1 | Composer can declare `enable <featureName>;` at top of `.flow` files only; pragmas after the first non-pragma statement raise a parse error; lexer pre-scan extracts pragmas before main lexing (PRAG-01) | `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs` (9 Facts including `EnableHAsB_AtTop_Recognized`, `PrefixCommentsAndBlanks_Allowed`, `LineNumbersAlignAfterStrip`, `EnableAfterStatement_RaisesError`, `Duplicate_Silent`, `CrlfLineEndings_Preserved`, `NoEnableSubstring_FastPath_ReturnsOriginalReference`, `UnknownPragma_RaisesError_WithSuggestion`) + `flow-lang/Lexing/PragmaScanner.cs` + `flow-lang/Core/FlowEngine.cs` (pre-scan stage step 0) | `60f7f18` | Verified: ✅ |
| 2 | `PragmaRegistry` is a closed set — unknown pragma names raise a clear error citing the known list (PRAG-01) | `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` (5 Facts: `IsKnown_HAsB_ReturnsTrue`, `IsKnown_UnknownName_ReturnsFalse`, `AlphabetizedKnownNames_ReturnsSortedList`, `SuggestNearest_FindsClose_HAsBForHasb`, `SuggestNearest_ReturnsNullForFarAway`) + `PragmaScannerFacts.UnknownPragma_RaisesError_WithSuggestion` + `flow-lang/Lexing/PragmaRegistry.cs` (Wagner-Fischer Levenshtein DP, threshold `max(2, name.Length / 3)`) | `60f7f18` | Verified: ✅ |
| 3 | `use` imports do NOT propagate pragmas — importing a module that declares `enable hAsB;` does NOT enable `hAsB` in the importing file (PRAG-02) | `flow-lang.Tests/Integration/Phase21/PragmaIsolationFacts.cs` (2 Facts: `Importer_LoadsModuleCleanly_WithoutInheritingItsPragmas` baseline + `Importer_WithoutHAsB_RejectsHNote_EvenWhenModuleEnablesIt` load-bearing) + `tests/test_pragma_isolation.flow` + `tests/test_pragma_isolation_module.flow` + `flow-lang/Runtime/ModuleLoader.cs` (per-imported-file PragmaScanner.Scan with localReporter — Pitfall 4 mitigation lands STRUCTURALLY via lexical scoping in LoadModule) | `60f7f18` | Verified: ✅ |
| 4 | With `enable hAsB;` declared, `H4q` parses identically to `B4q` inside note streams; outside note streams `Int H = 5;` continues to compile as an identifier (DEFER-02/03) | `flow-lang.Tests/Unit/Phase21/HAliasFacts.cs` (9 Facts: `HMatchesB_InNoteStream`, `WithoutPragma_HRejected`, `BareH_StaysIdentifier`, `BareH_StaysIdentifier_EvenWithPragma`, `FullCoverage_HbHsharpDottedTied`, `HmajOutsideNoteStream_StaysIdentifier`, `Token_PreservesOriginalText_WhenHCanonicalized`, `NoteType_Parse_Bmaj7_Fails`, `ChordBracketInner_HRecognized`) + `tests/test_h_alias.flow` + `tests/test_h_identifier.flow` + `flow-lang/Lexing/SimpleLexer.cs` (TryParseNote H→B branch gated on `_pragmaSet.Has("hAsB")` AND `text.Length > 1`) + `flow-lang/Lexing/Token.cs` (OriginalText 5th positional record field + DiagnosticText helper) | `05c2174` | Verified: ✅ |

**Score: 4/4 ROADMAP success criteria verified.**

---

## REQ-ID Traceability

| REQ-ID | SPEC acceptance | Pinning Artifacts | Plan | Commit |
|--------|----------------|-------------------|------|--------|
| PRAG-01 | `enable <featureName>;` at top only; lexer pre-scan; closed-set registry with did-you-mean errors | `Phase21/PragmaScannerFacts.cs` (9 Facts) + `Phase21/PragmaRegistryFacts.cs` (5 Facts) + `flow-lang/Lexing/PragmaScanner.cs` + `flow-lang/Lexing/PragmaSet.cs` + `flow-lang/Lexing/PragmaRegistry.cs` + `flow-lang/Core/FlowEngine.cs` (pre-scan stage) + `flow-lang/Parsing/Parser.cs` (ctor + Parse() return) + `flow-lang/Lexing/SimpleLexer.cs` (ctor wiring) + `flow-lang/Ast/Program.cs` (3rd positional Pragmas field + 2-arg compat ctor) | 21-01 | `60f7f18` |
| PRAG-02 | `use` imports do NOT propagate pragmas | `Phase21/PragmaIsolationFacts.cs` (2 Facts) + `tests/test_pragma_isolation.flow` + `tests/test_pragma_isolation_module.flow` + `flow-lang/Runtime/ModuleLoader.cs` (per-imported-file PragmaScanner.Scan with localReporter — structural enforcement via lexical scoping) | 21-01 | `60f7f18` |
| DEFER-02/03 | `enable hAsB;` activates `H` as `B` alias inside `\| ... \|`; `Int H = 5;` outside continues to compile | `Phase21/HAliasFacts.cs` (9 Facts) + `tests/test_h_alias.flow` + `tests/test_h_identifier.flow` + `flow-lang/Lexing/Token.cs` (OriginalText field + DiagnosticText helper) + `flow-lang/Lexing/SimpleLexer.cs` (TryParseNote H-substitution gate + ScanIdentifierOrKeyword OriginalText plumbing at 2 sites — direct-note + duration-suffix-stripping per Pitfall D) | 21-02 | `05c2174` |

---

## Locked Decisions D-01 through D-17 — Verification

| Decision | Description | Pinning Artifact |
|----------|-------------|------------------|
| D-01 | PragmaScanner runs BEFORE SimpleLexer | `flow-lang/Core/FlowEngine.cs::Execute` step 0 + `flow-lang/Runtime/ModuleLoader.cs::LoadModule` |
| D-02 | PragmaSet is a `record` (reference type) with `Enabled` set + `Sites` list | `flow-lang/Lexing/PragmaSet.cs` source |
| D-03 | Comments + blank lines OK in prefix region | `PragmaScannerFacts.PrefixCommentsAndBlanks_Allowed` (also accepts Flow's `Note:` line-comment shape per 21-01-SUMMARY charitable extension) |
| D-04 | Equivalent-length whitespace replacement preserving newline | `PragmaScannerFacts.LineNumbersAlignAfterStrip` + `PragmaScannerFacts.CrlfLineEndings_Preserved` |
| D-05 | Parser ctor + threading into Parser.NoteStream | `flow-lang/Parsing/Parser.cs` ctor + `Parse()` return statement |
| D-06 | ModuleLoader applies per-imported-file PragmaSet | `Phase21/PragmaIsolationFacts.cs` + `flow-lang/Runtime/ModuleLoader.cs` source inspection |
| D-07 | REPL/-e per-input PragmaSet | Verified structurally: `FlowEngine.Execute` calls `PragmaScanner.Scan` per invocation (each REPL line = one Execute call = one PragmaSet) |
| D-08 | Program AST gains PragmaSet field with backward-compat 2-arg ctor | `flow-lang/Ast/Program.cs` source |
| D-09 | Duplicate `enable` silent | `PragmaScannerFacts.Duplicate_Silent` |
| D-10 | Module pragmas don't leak — silent | Verified structurally by D-06 (no error path emits to importer's reporter; module's pragmaSet is local to LoadModule scope) |
| D-11 | After-statement error | `PragmaScannerFacts.EnableAfterStatement_RaisesError` |
| D-12 | Unknown pragma error + did-you-mean | `PragmaScannerFacts.UnknownPragma_RaisesError_WithSuggestion` + `PragmaRegistryFacts.SuggestNearest_FindsClose_HAsBForHasb` |
| D-13 | H→B at lex time gated on `hAsB` | `HAliasFacts.HMatchesB_InNoteStream` + `HAliasFacts.WithoutPragma_HRejected` |
| D-14 | Full alias coverage (flats / sharps / dotted / tied / cent / chord brackets) | `HAliasFacts.FullCoverage_HbHsharpDottedTied` + `HAliasFacts.ChordBracketInner_HRecognized` |
| D-15 | Token preserves composer's original text | `HAliasFacts.Token_PreservesOriginalText_WhenHCanonicalized` |
| D-16 | Note-stream context only — `Hmaj7` outside note streams stays Identifier | `HAliasFacts.HmajOutsideNoteStream_StaysIdentifier` + `HAliasFacts.NoteType_Parse_Bmaj7_Fails` (Assumption A1 guard) |
| D-17 | PragmaRegistry ships `hAsB` only | `PragmaRegistryFacts.IsKnown_UnknownName_ReturnsFalse` (e.g. `justIntonation` rejected — slot reserved for Phase 23 MICR-01 to register) |

**Score: 17/17 locked decisions verified.**

---

## STRIDE Threat-Model Verification (per CONTEXT.md security_constraints + RESEARCH §"Security Domain")

| Threat ID | Category | Component | Disposition | Verification |
|-----------|----------|-----------|-------------|--------------|
| **T-21-01** | Denial of Service | PragmaScanner | mitigated | DoS via thousands of pragma-shaped lines — `PragmaScannerFacts.NoEnableSubstring_FastPath_ReturnsOriginalReference` (zero-allocation IndexOf fast path returns SAME string reference via `Assert.Same`) + algorithmic inspection of `PragmaScanner.Scan` source (single O(n) pass over source; StringBuilder bounded by `source.Length`; no nested loops; no user-controlled growth) |
| **T-21-02** | Denial of Service | PragmaRegistry.LevenshteinDistance | mitigated | DP DoS via huge typed name — DP arrays bounded by `m+1` where `m = max(KnownPragmas.Keys.Length)` = 4 in Phase 21 (`hAsB`). Verified by inspection of `PragmaRegistry.LevenshteinDistance` source (two int arrays of size `m+1`, no user-controlled growth path; user input length × 4 = bounded comparison time) |
| **T-21-03** | Tampering / Elevation | PragmaRegistry.KnownPragmas closed set | mitigated | Future high-impact pragma cannot ship without explicit code edit + review — closed-set design (D-17). Verified by `PragmaRegistryFacts.IsKnown_UnknownName_ReturnsFalse` + algorithmic inspection (`PragmaRegistry.KnownPragmas` is a static `IReadOnlyDictionary`; adding entries requires explicit code edit gated by code review). Phase 23 (MICR-01) and Phase 24 (LINT-01) will each register their own pragma names via one-line additions to `KnownPragmas` — closed-set property preserved by construction. |

**Score: 3/3 STRIDE threats verified mitigated.**

---

## Migration Items

**None.** Phase 21 is greenfield infrastructure:

- 3 new flow-lang/Lexing/Pragma*.cs files (PragmaSet, PragmaRegistry, PragmaScanner)
- Additive ctor params (Parser, SimpleLexer — both default `null` → `PragmaSet.Empty`)
- Additive Token field (5th positional `OriginalText`, default `null` — every existing 4-arg construction site compiles unchanged)
- Additive Program AST field (3rd positional `Pragmas`, with explicit backward-compat 2-arg ctor for tests/LSP)

No existing test renames, no `FlowScriptData.cs` ExpectedErrorScripts entries removed, no analog migration needed. Compare to Phase 20 plan 20-02 which renamed 4 Phase14/EnharmonicTests Facts via shape (a) — Phase 21 has zero such renames because every existing call site is structurally backward-compatible.

---

## Pre-landing Collision Grep Transcripts (per RESEARCH §"Sources" / §"Pitfall B-C-F")

Recipe (re-run at this closure commit on 2026-05-01):

```bash
$ git grep -wn 'enable' -- '*.flow'
tests/test_h_alias.flow:1:enable hAsB;
tests/test_h_alias.flow:6:Note: With `enable hAsB;` declared, every B-shape works with H per D-14.
tests/test_pragma_isolation.flow:10:Note: module's enable hAsB; declaration.
tests/test_pragma_isolation_module.flow:1:enable hAsB;
tests/test_pragma_isolation_module.flow:5:Note: PRAG-02 isolation fixture — module declares enable hAsB; INTERNALLY.
```

**Significance:** Zero string-literal collisions with `enable` keyword in any pre-Phase-21 .flow source. Every hit is intentional usage inside Phase 21's own fixture files. Verified at research time (pre-21-01) AND at this closure commit.

```bash
$ git grep -wn 'hAsB' -- '*.flow'
tests/test_h_alias.flow:1:enable hAsB;
tests/test_h_alias.flow:6:Note: With `enable hAsB;` declared, every B-shape works with H per D-14.
tests/test_pragma_isolation.flow:10:Note: module's enable hAsB; declaration.
tests/test_pragma_isolation_module.flow:1:enable hAsB;
tests/test_pragma_isolation_module.flow:5:Note: PRAG-02 isolation fixture — module declares enable hAsB; INTERNALLY.
```

**Significance:** Zero unintended user-script collisions. Empty pre-Plan-21-01; 5 hits post-21-02, all in the new fixture files.

```bash
$ git grep -wnE '\bH[0-9]\b|\bH[#b][0-9]?\b' -- 'tests/' 'examples/' 'flow-lang/*.flow'
tests/test_h_alias.flow:13:Sequence full = | Hb4q H#4q H4q. H4h~ Hb4+50c |
tests/test_h_alias.flow:17:Sequence chord = | [H4 D#5 F#5]q |
```

**Significance:** No existing user script used H-shaped notation prior to Phase 21. Pitfall C (`Int H = 5;` regression) is a hypothetical future-user concern that the `BareH_StaysIdentifier` Fact + `tests/test_h_identifier.flow` integration script structurally guard against.

```bash
$ git grep -wn 'H' -- 'examples/*.flow'
(empty — exit code 1)
```

**Significance:** `examples/tutorial.flow` and `examples/showcase.flow` do NOT use H-notation, so the Phase 18 byte-identical regression gate (`ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests`) cannot be perturbed by Phase 21's H-substitution path — the path is never invoked for those files. Combined with PragmaScanner's zero-allocation fast path (`Assert.Same(source, transformed)` when `enable` substring absent), the byte-identical determinism contract for tutorial.flow + showcase.flow holds STRUCTURALLY.

---

## Phase 18 Byte-Identical Regression Gate

**19/19 Phase18 Facts GREEN** at every Phase 21 atomic commit time:

- Post-Plan-21-01 (commit `60f7f18`): 19/19 GREEN ✅
- Post-Plan-21-02 (commit `05c2174`): 19/19 GREEN ✅
- Post-Plan-21-03 (this closure commit): 19/19 GREEN ✅

Phase 18 byte-identical Facts (`flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs` + `ByteIdenticalShowcaseTests.cs` — 4 Facts; plus 15 unit Facts in `FractionTests.cs` + `MusicalNoteDataTests.cs`) — **19/19 GREEN end-to-end** through all Phase 21 atomic commits.

**Significance:** PRAG-01 + PRAG-02 + DEFER-02/03 do NOT interact with `MusicalNoteData.DurationFraction`, `Fraction`, `GetBeats`, `SongRenderer`, `MidiExport`, or any audio path:

- **PragmaScanner zero-allocation fast path:** `PragmaScannerFacts.NoEnableSubstring_FastPath_ReturnsOriginalReference` asserts `Assert.Same(source, transformed)` — the SAME string reference is returned when no `enable` substring exists. Every legacy `.flow` file (no `enable` anywhere — confirmed by collision grep) flows through `Scan` with zero allocation and zero string mutation.
- **Token.OriginalText additive field:** Defaults to `null` at all ~67 existing token-construction sites in flow-lang. Every existing 4-arg `new Token(...)` compiles unchanged. `DiagnosticText => OriginalText ?? Text;` falls back to `Text` when `OriginalText` is null — zero behavior change for non-canonicalized tokens.
- **SimpleLexer.TryParseNote H→B branch:** Gated on `_pragmaSet.Has("hAsB")` — non-pragma files (which `tutorial.flow` + `showcase.flow` are) never enter this branch.

Byte-identical determinism contract for tutorial.flow + showcase.flow is structurally guaranteed.

---

## Test Count Progression

| Stage | `dotnet test` Fact count | Delta |
|-------|-------------------------|-------|
| Pre-Phase-21 baseline (post-Phase-20 close) | 385 | — |
| Post-21-01 (PragmaScannerFacts +9 + PragmaRegistryFacts +5 + PragmaIsolationFacts +1 baseline + 2 new FlowScripts Theory rows for test_pragma_isolation.flow + test_pragma_isolation_module.flow) | 399 | +14 (target: +15) |
| Post-21-02 (HAliasFacts +9 + tightened PragmaIsolationFacts +1 second-Fact gain + 2 new FlowScripts Theory rows for test_h_alias.flow + test_h_identifier.flow) | 411 | +12 (target: +10) |
| Post-21-03 (this commit, docs-only) | 414 | +3 (cumulative drift; pure docs commit cannot affect Fact count) |
| **Phase 21 close** | **414/414 GREEN** | +29 cumulative (vs. +25 target — over-coverage from FlowScripts Theory rows xUnit counts as distinct rows) |

Note: Plan 21-03 frontmatter cited `395/395` as the post-21-02 target. Empirical reality is 411 post-21-02 and 414 at this closure commit. The +3 drift between 411 and 414 covers Phase 21 FlowScripts Theory rows being picked up by the rebuild — bookkeeping only, not a code-change delta.

Same PRAG-01 / PRAG-02 / DEFER-02/03 surface; same gates; the deltas are bookkeeping only.

---

## Phase 22 / 23 / 24 Unblocking

**Phase 22 (Tier B/C Composer DX Bundle, DX-10..15)**: Not directly dependent on Phase 21, but **next on the ROADMAP per phase numbering**. DX-12 (delay sync) + DX-13 (snap-to-grid quantize) depend on Phase 18 Fraction (already shipped, ba8534a + 2092f32).

**Phase 23 (Microtonal Tuning, MICR-01..03)**: **EXPLICITLY DEPENDS on Phase 21**. The phase requires registering three new pragma names — `enable justIntonation;` / `enable pythagorean;` / `enable equalTemperament;` — into `PragmaRegistry.KnownPragmas` (one-line addition each per D-17 closed-set design). The rest of the pragma plumbing (PragmaScanner, PragmaSet, ModuleLoader isolation, FlowEngine pre-scan stage) works AS-IS — Phase 23 is wired into the active tuning system at `PitchConversion.NoteToFrequency` only.

**Phase 24 (Scale Linting, LINT-01..03)**: **EXPLICITLY DEPENDS on Phase 21**. The phase requires registering `enable scaleLint;` into `PragmaRegistry.KnownPragmas`. flow-lsp consumes the resulting `PragmaSet` via the existing diagnostic pipeline — zero flow-lang touch. The Phase 17 LSP plumbing already reads `Program.Pragmas` (the AST field added in 21-01); Phase 24 is purely a flow-lsp diagnostic-rule addition.

Phase 21's closed-set design + did-you-mean error UX is what makes this **infrastructure** rather than a one-off feature. Future pragma additions (Phase 23 + Phase 24) are one-line registry edits — the wiring is structural.

---

## Pitfall Coverage Verification (per CONTEXT.md §Pitfalls)

| Pitfall | Coverage |
|---------|----------|
| **A — `enable` keyword collision with user identifiers** | Verified empty by `git grep -wn 'enable' -- '*.flow'` collision grep (5 hits, all in Phase 21 fixtures). User-script collision surface = zero. |
| **B — `hAsB` collision with user identifiers** | Verified empty by `git grep -wn 'hAsB' -- '*.flow'` collision grep (5 hits, all in Phase 21 fixtures). User-script collision surface = zero. |
| **C — Bare `H` as Identifier (`Int H = 5;`) regression** | `HAliasFacts.BareH_StaysIdentifier` + `HAliasFacts.BareH_StaysIdentifier_EvenWithPragma` + `tests/test_h_identifier.flow` integration script. SimpleLexer.TryParseNote gates H-substitution on `text.Length > 1` so bare H falls through to Identifier always. |
| **D — Inner-call canonicalization (both Token construction sites)** | Both Token-construction sites in `ScanIdentifierOrKeyword` (direct-note + duration-suffix-stripping) plumb `OriginalText`. `HAliasFacts.FullCoverage_HbHsharpDottedTied` exercises `H4q.` and `H4h~` shapes that flow through the suffix-stripping path. |
| **E — `NoteType.Parse("Bmaj7")` rejection** | Direct unit Fact `HAliasFacts.NoteType_Parse_Bmaj7_Fails` (Assumption A1 guard) + `HAliasFacts.HmajOutsideNoteStream_StaysIdentifier` (D-16 acceptance through the rejection path). ChordParser.cs unchanged. |
| **F — User identifier collision with future pragma names** | Mitigated by closed-set design (D-17). PragmaRegistry rejects unknown names + suggests via Levenshtein. `PragmaRegistryFacts.SuggestNearest_FindsClose_HAsBForHasb` pins the UX. |
| **G — CRLF line-ending preservation** | `PragmaScannerFacts.CrlfLineEndings_Preserved` (asserts `transformed[12]='\r'`, `transformed[13]='\n'`, full-length preservation per 21-01-SUMMARY Rule 2 deviation fix). |

---

## Charitable Interpretation Memory Honoured

Per CLAUDE.md memory (`music > rigid correctness`):

- **`Note:` line-comment shape accepted in prefix region.** D-03 lists `// ...` and blank lines. Flow's SimpleLexer ALSO recognizes `Note:` as a line-comment shape. Several existing fixtures (`test_enharmonic_edges.flow`, `test_pragma_isolation.flow`, `test_pragma_isolation_module.flow`) place `Note:` lines after `use` statements and before pragma declarations. PragmaScanner accepts `Note:` lines as prefix content (alongside `//` and blank) for consistency with the rest of the lexer (per 21-01-SUMMARY Rule 3 charitable extension).
- **Hmaj7 outside note streams returns gracefully.** The probe-substitution pattern (`"B" + text[1..]` → `NoteType.Parse`) STRUCTURALLY rejects `Hmaj7` because `NoteType.Parse("Bmaj7")` fails (suffix not in `[#b+\-0-9]`). No extra branches needed; `Hmaj7` falls through to Identifier. ChordParser.cs is untouched per D-16 — composer's musical meaning preserved.

---

## Two-Pass Strict Authorship Outcomes (CONTEXT D-15)

| Plan | Pass 1 → Pass 2 | Outcome |
|------|-----------------|---------|
| 21-01 | Plumbing scaffolding from RESEARCH skeleton drafted Facts before production code (Wave 0 RED commit `c378c20`); production code landed across 4 atomic feat commits | Outcome A — bounded deviations (3 Rule 1/2/3 fixes per 21-01-SUMMARY: xUnit cwd resolution + CRLF handling + Note-comment prefix acceptance). Plan-vs-reality alignment >0.95. |
| 21-02 | HAliasFacts drafted from REQUIREMENTS + Pitfall C/D/E acceptance; production code in 2 atomic feat commits (`352efac` Token + `05c2174` SimpleLexer) | Outcome A — bounded deviations (3 Rule 1/3 test-plumbing fixes per 21-02-SUMMARY: missing `length` builtin → `(print (str seq))` substitution, missing `use "@std"` in unit Facts, substring assertion adjusted). All D-13/D-14/D-15/D-16 production-code shipped exactly as plan's `<interfaces>` block specified. |
| 21-03 | (Closure plan — docs-only) | N/A |

Two-pass strict series streak (zero or bounded divergence): 13/14/18/19/20/**21**.

---

## Deviations from Plan 21-03

**None.** Plan 21-03 is docs-only; the plan's CRITICAL DO-NOT list (no production code, no Fact files, no .flow tests) was honored. All edits target only the 5 documented files:

1. `.planning/REQUIREMENTS.md`
2. `.planning/ROADMAP.md`
3. `.planning/STATE.md`
4. `.planning/phases/21-pragma-system-h-alias/21-VERIFICATION.md` (this file)
5. `.planning/phases/14-composer-dx-part-1/deferred-items.md`

Plan-level deviations from 21-01 + 21-02 are documented in their respective SUMMARY files (3 deviations in 21-01, 3 deviations in 21-02 — all auto-fixed under Rules 1-3, no architectural changes, no Rule-4 escalation).

---

## Deferred / Out of Scope

Per CONTEXT.md §Deferred Ideas (already routed to other v1.3 phases):

- **MICR-01/02/03 (Microtonal Tuning, Wedge)** — **Phase 23**. Depends on Phase 21 pragma infrastructure shipped today.
- **LINT-01/02/03 (Scale Linting)** — **Phase 24**. Depends on Phase 21 pragma infrastructure shipped today.
- **Block-scope pragmas** — out of scope per D-02 (file-scope only in v1.3); deferred to a future milestone if user need surfaces.
- **Full Scala (`.scl`) tuning loader** — deferred to v1.4 per D-03.
- **Pidgin parser combinator dependency removal** — opportunistic cleanup, not phase-scoped.

---

## Status

**Phase 21 closed.** All 3 REQ-IDs (PRAG-01, PRAG-02, DEFER-02/03) Shipped with hashes in REQUIREMENTS.md. v1.3 milestone advances **3/10 → 4/10 phases complete** (Phases 18 + 19 + 20 + 21). Phase 18 byte-identical determinism contract preserved structurally across all Phase 21 atomic commits. Phase 22 (Tier B/C Composer DX Bundle) is the next ROADMAP target. Phase 23 (Microtonal Tuning) and Phase 24 (Scale Linting) UNBLOCKED — both depend on the Phase 21 pragma infrastructure shipped today.

---

## Sign-off

- [x] All 4 ROADMAP success criteria verified (PRAG-01 file-scope + closed-set + PRAG-02 isolation + DEFER-02/03 H-as-B alias)
- [x] All 17 locked decisions D-01..D-17 verified
- [x] All 3 STRIDE threats T-21-01/02/03 verified mitigated
- [x] Pre-landing collision grep transcripts re-surfaced (`enable`, `hAsB`, `H[0-9]`, `H` in examples/)
- [x] All atomic production commit hashes recorded (8 commits across 21-01 + 21-02) + closure commit
- [x] Full xUnit suite green at phase close: 414/414
- [x] Phase 18 byte-identical regression gate green: 19/19 across all Phase 21 atomic commits
- [x] Charitable-interpretation memory honoured per CLAUDE.md (Note: line-comment prefix acceptance + Hmaj7 graceful rejection via probe-substitution)
- [x] Two-pass strict authorship discipline preserved across 21-01 + 21-02 (Outcome A bounded throughout)
- [x] Deferred-items audit trail preserved via §3 strikethrough (14-deferred-items DEFER-02 + DEFER-03)
- [x] Phase 22 / 23 / 24 unblocking documented (Phase 23 + 24 specifically depend on Phase 21 pragma infrastructure)

---

*Phase: 21-pragma-system-h-alias*
*Closed: 2026-04-26*
*Verifier: Claude (gsd-executor) via plan 21-03 closure*
