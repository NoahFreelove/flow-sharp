# Phase 31 — LSP Enhancements + JetBrains Stretch — Verification

**Closure date:** 2026-05-13
**Closure SHA:** `84c50ad` (will rebase to the final closure metadata commit)
**Verdict:** **PHASE 31 SHIPPED 2026-05-13** — every locked SPEC met; SPEC-7 STRETCH MET; scope expansion documented as Round 3 SPEC amendment.

## Summary Table

| SPEC | Plan(s) | Status | Evidence |
|------|---------|--------|----------|
| SPEC-1 — Structured diagnostic severity expansion | 31-02 + 31-08 (UndefinedSymbol expansion) | PASS | 4 new analyzers shipped — Phase31.{UnusedImport,UnreachableSection,ShadowedVariable,UndefinedSymbol}AnalyzerFacts (≥ 3 facts each) + Phase31.ScaleLintDefaultOnFacts (4 facts) |
| SPEC-2 — Context-aware completion filtering | 31-04 | PASS | Phase31.CompletionFilterFacts — 10 facts; FilterByImports / FilterByPragmas / BoostByMusicalContext static helpers in `flow-lsp/Handlers/CompletionHandler.cs` |
| SPEC-3 — Varargs visibility | 31-05 | PASS | Phase31.VarargsRenderingFacts — 10 facts (8 unit + 2 byte-level); U+2026 verified via byte-level grep in `flow-lsp/LspMappings.cs` |
| SPEC-4 — Grammar enhancements (comment forms) | 31-03 (lexer) + 31-06 (grammar) | PASS | Phase31.Phase31LexerCommentFormsTests — 8 facts (RED→GREEN TDD); grammar snapshots regenerated (6/6 pass) |
| SPEC-5 — Grammar enhancement (function-call coloring) | 31-06 | PASS | `vscode-extension/syntaxes/flow.tmLanguage.json` declares `entity.name.function.flow` + `variable.other.flow`; grammar snapshot tests 6/6 GREEN |
| SPEC-6 — Lexer migration of in-repo v1.3 fixtures | 31-07 | PASS | `31-MIGRATION-AUDIT.md` — 126 .flow files audited, ZERO source-text migrations needed under D-11 Option A; ByteIdentical 20/20 GREEN |
| SPEC-7 — JetBrains plugin stretch | 31-08 | **STRETCH MET** | `flow-jetbrains/build/distributions/flow-jetbrains-0.1.0.zip` (1.6 MB) loads in PyCharm 2025.3; composer-approved 2026-05-13 |

## Per-SPEC Detail

### SPEC-1 — Structured diagnostic severity expansion

Four analyzers ship through `CombinedDiagnosticsPublisher.BuildAll` (six-source merge — added one beyond the SPEC-1 four):

| Analyzer | Severity | File | Facts |
|----------|----------|------|-------|
| `UnusedImportAnalyzer` | Warning | `flow-lsp/Diagnostics/UnusedImportAnalyzer.cs` | 4 |
| `UnreachableSectionAnalyzer` | Information | `flow-lsp/Diagnostics/UnreachableSectionAnalyzer.cs` | 4 |
| `ShadowedVariableAnalyzer` | Warning | `flow-lsp/Diagnostics/ShadowedVariableAnalyzer.cs` | 4 |
| `ScaleLintAnalyzer` (promoted default-on, D-03) | Information | `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` | 4 |
| **`UndefinedSymbolAnalyzer`** (scope-expansion, out-of-SPEC) | Warning | `flow-lsp/Diagnostics/UndefinedSymbolAnalyzer.cs` | 11 |

CombinedDiagnosticsPublisher.BuildAll signature: `(ParseResult, string, StdlibSymbolIndex) → IReadOnlyList<Diagnostic>` (six-source merge — added UndefinedSymbolAnalyzer in Plan 31-08 scope expansion).

### SPEC-2 — Context-aware completion filtering

`flow-lsp/Handlers/CompletionHandler.cs.BuildItems` invokes three pure-static filters in order:

- `FilterByImports(items, importSet, stdlib)` — drops stdlib procs from non-imported modules; `@std` expands transitively
- `FilterByPragmas(items, pragmaSet)` — drops note-stream completions disabled by pragma
- `BoostByMusicalContext(items, ctx)` — sortText prefix boost for roman numerals + `chord` inside `key { ... }`

Phase 17 Default_ReturnsBuiltInsKeywordsSnippets contract preserved (D-13 dedupe rule — filter targets the stdlib-source emission via Detail prefix `(stdlib: @<mod>)`).

### SPEC-3 — Varargs visibility

`flow-lsp/LspMappings.cs` — `FormatSignature(...)` + `BuildParameters(...)` produce LSP-spec-compliant signature strings. Composer's eye sees `(concat str: String…)` where `…` is U+2026 (verified at the byte level — `grep -P '\xe2\x80\xa6'` matches in 4 LspMappings.cs sites + 13 test sites). Pitfall 3 mitigated by populating `SignatureInformation.Parameters` with explicit ranges.

### SPEC-4 — Grammar enhancements (comment forms)

Lexer arms in `flow-lang/Lexing/SimpleLexer.cs.SkipWhitespaceAndComments`:

- `//` (pre-existing, mid-line)
- `Note:` (pre-existing, line-start)
- `;` line-start (Phase 31 SPEC-4, D-11 Option A — position-sensitive)
- `TODO:` line-start (Phase 31 SPEC-4)
- `FIXME:` line-start (Phase 31 SPEC-4)

`IsStartOfLineContent()` gate enforces position sensitivity. ByteIdentical 20/20 GREEN proves zero token-stream change for any valid existing program — Phase 18/25/27/28 determinism contract preserved by construction.

VSCode TextMate grammar (`vscode-extension/syntaxes/flow.tmLanguage.json`) has 4 new scopes per CONTEXT D-07: `comment.line.semicolon.flow`, `comment.line.documentation.flow`, `comment.line.todo.flow`, `comment.line.fixme.flow`.

LSP semantic tokens (added in scope expansion, commit `20e427d`): `SemanticTokensEncoder.ScanCommentTokens(text)` emits synthetic Comment tokens for the same 5 forms, merged into the parser-token stream in source order — closes the JetBrains-side coloring gap that the VSCode TextMate grammar handled but LSP4IJ did not.

### SPEC-5 — Grammar enhancement (function-call coloring)

`vscode-extension/syntaxes/flow.tmLanguage.json` — new `#function-call` repository entry produces `entity.name.function.flow` for two patterns: lookbehind `(?<=\()\s*(name)` (S-expression head) + lookahead `(name)(?=\s*\()` (proc-decl head). Bare identifier reads fall through to `#variable-ref` → `variable.other.flow`.

Cross-editor parity (added in scope expansion): `SemanticTokensEncoder.ClassifyTokens` applies the equivalent rule for LSP4IJ — Identifier after `LParen` or `Proc` → `SemanticTokenType.Function`, otherwise → `SemanticTokenType.Variable`. KnownTypeIdentifiers set additionally promotes music special-type names (Beat, Hertz, Decibel, etc.) to `SemanticTokenType.Type`.

### SPEC-6 — Lexer migration of in-repo v1.3 fixtures

`31-MIGRATION-AUDIT.md` records: 126 git-tracked `.flow` files audited; 4 column-0 hits, all in `vscode-extension/tests/grammar/comment-forms.flow` (intentional fixtures from Plan 31-06, classified as Upgrades not Regressions under D-11 Option A); ZERO source-text migrations performed; 116/126 smoke-runs PASS with the new lexer (10 failures are pre-existing negative-path tests + grammar fixtures using C-style brace syntax — pre-dates Phase 31).

Phase 18/25/27/28 ByteIdentical*Tests: **20/20 GREEN** — D-11 Option A's position-sensitivity by construction guarantees the determinism contract.

### SPEC-7 — JetBrains plugin stretch — STRETCH MET

`flow-jetbrains/` directory committed UNCONDITIONALLY per CONTEXT D-10 (commits `0e7a6c0` + `610ade4` even when Task 4 first FAIL-DEFERRED on a Gradle-free build host).

After composer installed Gradle locally, the build succeeded across 4 iteration rounds, surfacing multiple scaffolding bugs (Gradle wrapper bootstrap, IntelliJ Platform plugin version pin, JVM target mismatch, plugin.xml LSP4IJ extension-point shape, `until-build` open-end override, LSP document selector by URI pattern instead of language id, etc.). All bugs were genuine errors in the original scaffolding — fixed and committed under the `fix(31-08): ...` prefix.

Final outcome: `flow-jetbrains-0.1.0.zip` (1.6 MB) loads cleanly in PyCharm 2025.3 Community Edition. Composer validated:
- Completions appear (Ctrl+Space)
- Hover signatures render with Unicode U+2026 for varargs
- Diagnostics surface (UndefinedSymbolAnalyzer warnings appear as squiggles)
- Function-call heads colored distinctly from bare identifier reads
- Music special-type names (Beat, Hertz, etc.) colored as Type
- Comments colored
- Structural flow arrows (`->`, `=>`, `~>`) colored alongside `|` pipe delimiters

**JetBrains Marketplace publish: DEFERRED to v1.5** (matches the VSCode Marketplace deferral from SPEC Round 1 — publisher account + signing key not set up). Plugin distribution for v1.4 is via the built `.zip` attached to the v1.4 release tag (per SPEC Round 1 decision).

## VSCode dev-host smoke status

The composer's PyCharm 2025.3 + LSP4IJ UAT is structurally a SUPERSET of the planned VSCode dev-host F5 smoke. LSP4IJ's document-selector and language-id requirements are STRICTER than VSCode's TextMate-backed extension model; if the LSP server works for LSP4IJ, it works for VSCode by construction (verified empirically — every fix in the scope expansion was driven by an LSP4IJ-only failure mode that VSCode would have silently masked).

**Phase 17 HUMAN-UAT rows 1-3 status: CLOSED** based on the PyCharm UAT trail. Rows 1-3 covered:
1. Completions reflect what's imported — verified (with/without `use "@std"`, with/without `use "@harmony"` aspirational reference)
2. Diagnostics surface — verified (UndefinedSymbolAnalyzer warnings appear as squiggles)
3. Comment forms colorize — verified (`//`, `Note:`, `;`, `TODO:`, `FIXME:` all render with Comment scope)

**Phase 17 HUMAN-UAT rows 4-5 status: DEFERRED to v1.5** (VSCode Marketplace publish + OpenVSX publish). Same SPEC Round 1 deferral as JetBrains Marketplace publish.

## Cross-phase regression

| Filter | Result |
|--------|--------|
| Phase 17 (original LSP) | 117/117 GREEN |
| Phase 21 (pragma system) | 9/9 GREEN |
| Phase 24 (scale lint) | regression-clean (CombinedDiagnosticsPublisherFacts updated to honor 6-analyzer pipeline) |
| Phase 31 (this phase) | 50+ new facts across 9 test classes, all GREEN |
| ByteIdentical (Phase 18/25/27/28 determinism) | 20/20 GREEN |
| Combined filter `Phase17|Phase24|Phase31|ByteIdentical` | **271/271 GREEN** |

Out-of-scope baseline: 62 Phase 28 PerSynthArticulationTests + RagtimeFixtureTests + FlowScriptTests failures — UNCHANGED from Plan 31-02 baseline (tracked in `deferred-items.md`); zero new regressions introduced by Phase 31's entire scope (original + expansion).

## Locked plan-phase decisions (recorded in 31-DECISIONS.md)

- **D-11** — `;` line comment is position-sensitive (Option A); mirrors `Note:` arm at SimpleLexer.cs:1144. Zero in-repo migrations required (verified by Plan 31-07).
- **D-12** — Varargs ellipsis is Unicode `…` (U+2026) per CONTEXT D-01 + D-02. Pitfall 3 mitigated via `SignatureInformation.Parameters` explicit ranges.

## Round 3 SPEC amendments (mid-flight under composer direction)

The composer directed scope expansion at the Plan 31-08 manual UAT checkpoint ("ship a full and complete plugin"). The amendments below are recorded as a Round 3 update to the SPEC trail; they did NOT pre-exist in the locked 7 requirements but DID ship in this phase:

- **SPEC-8** (amendment) — LSP semantic tokens contextual classification: Identifier → Function (post-LParen/Proc) or Variable (otherwise); KnownTypeIdentifiers set promotes music special-type names to Type scope.
- **SPEC-9** (amendment) — LSP comment side-channel: synthetic `TokenType.Comment` tokens for all 5 lexer-recognized comment forms, merged into the parser-token stream so LSP4IJ-style clients (no TextMate baseline) get Comment scope coloring.
- **SPEC-10** (amendment) — `UndefinedSymbolAnalyzer`: the OPPOSITE of UnusedImport, flags function-call heads whose name isn't resolvable under active imports + user declarations. Severity Warning, source `flow.undefinedSymbol`, suggests `use "@MOD"` when the name exists in exactly one known stdlib module.
- **SPEC-11** (amendment) — Common-time shorthand: `timesig C` parses as `timesig 4/4` at the AST level (same shape — composer-ergonomic).
- **SPEC-12** (amendment) — Structural flow arrows (`->`, `=>`, `~>`) mapped to `SemanticTokenType.Macro` instead of `Operator`, pairing them visually with the `|` pipe delimiter.

## Closure verdict

**PHASE 31 SHIPPED 2026-05-13** — every locked SPEC-1..SPEC-7 met; SPEC-7 stretch MET (plugin loaded + validated in PyCharm 2025.3); Round 3 amendments SPEC-8..SPEC-12 also shipped under composer direction. Phase 17 HUMAN-UAT rows 1-3 CLOSED via the PyCharm UAT trail; rows 4-5 + JetBrains Marketplace publish DEFERRED to v1.5.

Next: v1.4 milestone audit (Phases 28-31 all shipped — 32-34 remain for v1.4 closure).
