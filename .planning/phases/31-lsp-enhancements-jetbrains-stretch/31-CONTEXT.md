# Phase 31: LSP Enhancements + JetBrains Stretch — Context

**Gathered:** 2026-05-12
**Status:** Ready for planning
**Source:** SPEC Express Path (31-SPEC.md — ambiguity score 0.19)

<domain>
## Phase Boundary

Phase 31 makes `flow-lsp` and the VSCode TextMate grammar good enough for v1.4 by
closing four functional gaps and adding one polish surface:

1. **Diagnostic severity expansion** — 3 new analyzer-emitted diagnostics
   (UnusedImport/Warning, UnreachableSection/Information, ShadowedVariable/Warning)
   plus promoting `ScaleLintAnalyzer` from opt-in pragma to default-on Information.
2. **Context-aware completion filtering** — Import-filter, Pragma-filter,
   Musical-context-filter applied in `CompletionHandler.cs`.
3. **Varargs visibility** — variadic functions render with a trailing ellipsis
   in signature help, hovers, and completion tooltips.
4. **Grammar enhancements** — new comment forms (`;`, `Note:`, `TODO:`, `FIXME:`)
   in `SimpleLexer.cs` + `flow.tmLanguage.json`; function-call positions distinct
   from bare identifier references.
5. **In-repo `.flow` audit + migration** — all committed fixtures pass under the
   new lexer; two-run byte-identical determinism preserved.

**Stretch:** JetBrains plugin via LSP4IJ in a new `flow-jetbrains/` Gradle
project, shipped as a `.zip` artifact attached to the v1.4 release tag if the
"builds + opens .flow with completions" bar is met. VSCode Marketplace publish
+ OpenVSX publish + JetBrains Marketplace publish are all deferred to v1.5.

The phase intentionally breaks backward compatibility with any v1.3 `.flow`
scripts that used now-reserved token forms (`;` outside strings, bare `Note:` /
`TODO:` / `FIXME:` at column 0). All in-repo fixtures get migrated as part of
Requirement 6.

</domain>

<spec_lock>
## Requirements (locked via 31-SPEC.md)

**7 requirements are locked.** See `31-SPEC.md` for full requirements, boundaries,
and acceptance criteria.

Downstream agents (researcher, planner) MUST read `31-SPEC.md` before planning or
implementing. Requirements are not duplicated here.

**In scope (from SPEC.md Boundaries):**
- 4 new diagnostic types (UnusedImport / UnreachableSection / OutOfKey-default / ShadowedVariable)
- Promote `scaleLint` from opt-in pragma to default-Information severity
- Import-filter, Pragma-filter, Musical-context-filter for completions
- Varargs `param: Type…` rendering in signature help, hover, completion tooltips
- New comment forms: `;`, `Note:`, `TODO:`, `FIXME:` (lexer + TextMate grammar)
- Function-call coloring (`entity.name.function.flow` vs `variable.other.flow`)
- Audit + migration of in-repo `.flow` files for lexer-change collisions
- JetBrains plugin scaffolding (stretch: ship the built plugin `.zip` alongside v1.4 release tag)
- Unit tests for every new behavior; existing Phase 17 test pattern extended
- Optional grammar snapshot test additions for new comment forms + function-call coloring

**Out of scope (from SPEC.md Boundaries):**
- VSCode Marketplace publish (Phase 17 HUMAN-UAT rows 4-5) — deferred to v1.5
- OpenVSX publish — same deferral
- JetBrains Marketplace publish — Phase 31 ships a `.zip` artifact, not a Marketplace listing
- macOS / Windows LSP testing — Linux dev path only this phase
- LSP protocol-level integration tests — unit-only test approach locked in Round 2
- New LSP capabilities beyond the 4 work areas — code actions, refactors, formatter,
  rename-symbol, find-references are all v1.5+ scope
- Real-time LSP performance benchmarking
- Auto-import / quick-fix code actions
- `;`-as-statement-separator semantics — `;` is a comment, NOT a statement separator
- Customizable comment-style configuration — `Note:` is hardcoded
- Multi-line `Note:` blocks — comment forms are line-terminated only

</spec_lock>

<decisions>
## Implementation Decisions

Every decision below is **locked from the discuss-phase Q&A**. Do not re-litigate
during research or planning.

### Varargs Rendering (REQ-3)
- **D-01 [varargs-glyph]** Variadic parameters render with the Unicode horizontal
  ellipsis `…` (U+2026) — NOT three ASCII dots. Hover panel reads
  `(concat str: String…)`. Modern LSP clients (VSCode, JetBrains) render the
  glyph cleanly; the single code point keeps tooltips visually compact next to
  other variadic-friendly tokens.
- **D-02 [varargs-position]** Ellipsis trails the parameter type, not the parameter
  name. Format: `name: Type…`. Matches the strongest single convention from the
  Java / TypeScript / C# variadic-rendering family.

### scaleLint Default-On Mechanism (REQ-1d)
- **D-03 [scalelint-default-on]** Promote `ScaleLintAnalyzer` from opt-in
  pragma to default-on Information severity. The `enable scaleLint;` pragma
  remains accepted as a no-op for backward compatibility with v1.3 scripts
  that already declare it (per `project_pre_public_no_legacy_burden` — we
  could remove the pragma entirely, but keeping it as a silent no-op avoids
  pointless migration churn for the small set of in-repo scripts that use it).
- **D-04 [scalelint-no-opt-out]** No language-level opt-out mechanism.
  Composers who want to silence the diagnostic use:
  - VSCode setting: `"problems.severities": { "info": "none" }` (silences ALL
    Information diagnostics)
  - Per-occurrence right-click → "Suppress in this file" in the Problems panel
  - Avoiding `key { ... }` blocks (the analyzer is musical-context-scoped per
    Phase 24 LINT-03 innermost-key-wins; no key block = no lint).
  Rationale: Flow language stays free of editor-tooling concerns; the Phase 24
  D-22 silent-on-unrecognized-key behavior already makes the lint permissive.
  Negative pragmas (`enable noScaleLint;`) muddy the pragma surface for a
  Phase 31 design problem with a clean editor-side answer.
- **D-05 [diagnostic-source-preserved]** Diagnostic source string stays
  `"flow.scaleLint"` (locked in Phase 24 D-18) so editor UIs can filter or
  disable scale-lint independently from parse errors via standard LSP
  diagnostic-toggle UI.

### TextMate Grammar Scope Naming (REQ-4 + REQ-5)
- **D-06 [scope-suffix]** Standard scopes with `.flow` language suffix —
  matches universal TextMate convention (`entity.name.function.python`,
  `comment.line.double-slash.cpp`, etc.). Phase 17 D-05's "no Flow-specific
  scopes" is interpreted as "no invented scope hierarchies like `flow.note`,"
  NOT "no language suffix on standard scopes." Themes that don't know Flow
  match the parent scope and inherit color; themes that know Flow can target
  the suffix for refinement.
- **D-07 [comment-scopes]** Comment-form scopes (one per new comment style):
  - `;` line comment → `comment.line.semicolon.flow`
  - `Note:` lead-in → `comment.line.documentation.flow` (TextMate's standard
    for documentation comments; orange/teal in most themes)
  - `TODO:` lead-in → `comment.line.todo.flow` (orange in most themes via the
    common `comment.line.todo` parent)
  - `FIXME:` lead-in → `comment.line.fixme.flow` (red in most themes)
- **D-08 [function-call-scope]** Function-call positions inside `(funcName …)`
  forms → `entity.name.function.flow`. Bare identifier references outside that
  position → `variable.other.flow`. Decision applies to both flow.tmLanguage.json
  and any future LSP semantic-token contribution.

### LSP4IJ Version + JetBrains Scaffolding (REQ-7)
- **D-09 [lsp4ij-pin]** Pin LSP4IJ to a specific tested version in
  `flow-jetbrains/build.gradle.kts` (or `build.gradle` — planner's call).
  Exact version chosen at plan-phase time after a one-time freshness check
  on JetBrains' Maven repository — the version locks at that point and
  doesn't drift unless someone explicitly upgrades it. NO floating `latest` /
  no `+` range patterns.
- **D-10 [scaffolding-always-lands]** The `flow-jetbrains/` directory (Gradle
  build files, `plugin.xml`, LSP4IJ wiring) ALWAYS lands at phase closure,
  regardless of whether the full "builds + opens .flow with completions"
  stretch bar is met. Closure status is a documentation decision:
  29-VERIFICATION-style → "stretch met (plugin .zip attached to v1.4 tag)"
  OR "stretch deferred to v1.5 (scaffolding ready)." Never delete the
  scaffolding — v1.5 picks it up immediately if Phase 31 doesn't ship the
  plugin itself.

### Claude's Discretion
- Exact LSP4IJ version pin number — planner does a freshness check at plan-phase
  and locks the version. Suggested: latest stable release as of plan-phase date.
- Whether grammar snapshot tests use Jest's `toMatchSnapshot` style or simple
  string-equality against committed fixtures — both are valid; planner picks.
- Whether the in-repo `.flow` fixture audit (REQ-6) emits a one-off migration
  script or is done manually per file — depends on collision count discovered.
- Whether `DiagnosticsPublisher.cs` is extended in-place vs adding a new
  `StructuredDiagnosticsPublisher.cs` alongside — researcher reads the file
  and recommends.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents (researcher, planner) MUST read these before planning or implementing.**

### Phase 31 spec + this context
- `.planning/phases/31-lsp-enhancements-jetbrains-stretch/31-SPEC.md` — locked
  requirements (7), boundaries, acceptance criteria, constraints, ambiguity
  report.
- `.planning/phases/31-lsp-enhancements-jetbrains-stretch/31-CONTEXT.md` — this
  file (decisions D-01..D-10).

### Prior phase context that constrains Phase 31
- `.planning/phases/17-flow-language-server/17-CONTEXT.md` — original
  flow-lsp scope; D-04 hybrid TextMate + LSP semantic tokens; D-05 standard
  scope naming (clarified here in D-06).
- `.planning/phases/24-scale-linting-flow-lsp/24-CONTEXT.md` — ScaleLintAnalyzer
  origin; LINT-01..LINT-03; D-18 diagnostic source string `"flow.scaleLint"`;
  D-19 pragma-activation gate; D-22 silent-on-unrecognized-key (charitable);
  D-23 no meta-diagnostic for unused pragma.
- `.planning/phases/21-pragma-system-h-alias/` — pragma vocabulary +
  `PragmaRegistry.cs` (the `KnownPragmas` map). Phase 31 D-03 keeps
  `enable scaleLint;` as a recognized no-op for backward compatibility.

### Codebase landmarks (read by researcher to ground plans)
- `flow-lsp/Handlers/CompletionHandler.cs` — 273 lines; current completion
  generation. REQ-2 modifies this for the 3 filters.
- `flow-lsp/Handlers/HoverHandler.cs` — 123 lines; current hover renderer.
  REQ-3 adds varargs ellipsis.
- `flow-lsp/Handlers/SignatureHelpHandler.cs` — current signature help.
  REQ-3 surface.
- `flow-lsp/Diagnostics/DiagnosticsPublisher.cs` — 60 lines, Error-only.
  REQ-1 expands to multi-severity.
- `flow-lsp/Diagnostics/CombinedDiagnosticsPublisher.cs` — diagnostic aggregator;
  REQ-1 routes new analyzers through this.
- `flow-lsp/Diagnostics/ScaleLintAnalyzer.cs` + `DiatonicSpellings.cs` +
  `ScaleLintPublisher.cs` — existing scale-lint. REQ-1d flips activation
  default; D-03 keeps the pragma as no-op.
- `flow-lsp/LspMappings.cs` — Flow ↔ LSP type translation. REQ-3 may need
  varargs-shape extension.
- `flow-lang/Lexing/SimpleLexer.cs` — REQ-4 adds 4 new comment-form recognitions.
- `flow-lang/Lexing/PragmaRegistry.cs` — `KnownPragmas` map; D-03 leaves
  `"scaleLint"` registered but its analyzer activation becomes always-on.
- `vscode-extension/syntaxes/flow.tmLanguage.json` — REQ-4 + REQ-5 grammar
  enhancements (comment forms + function-call coloring).
- `vscode-extension/tests/grammar/` — grammar snapshot tests directory
  (acceptance for REQ-5).
- `flow-lang.Tests/Unit/Phase17/` — existing 16+ LSP unit tests; pattern
  extended for Phase 31 new tests.

### v1.3 / v1.4 fixtures audited under REQ-6
- `examples/tutorial.flow` — uses `Note:` chapter dividers; becomes
  first-class comments under new lexer.
- `examples/showcase.flow` — Phase 27 fixture, byte-identical-pinned.
- `examples/pragmas/h_alias.flow` — Phase 21 fixture.
- `examples/pragmas/microtonal_ji.flow` — Phase 23 fixture.
- `tests/test_*.flow` — full ad-hoc test suite (70+ files).
- Phase 28 ragtime fixtures — byte-identical-pinned.

### External docs
- LSP4IJ (JetBrains LSP bridge) — `https://github.com/redhat-developer/lsp4ij`.
  Planner does a freshness check at plan-phase to lock the version.
- TextMate scope naming convention — Microsoft's TextMate-style language
  grammar guidelines; standard scope tree at
  `https://macromates.com/manual/en/language_grammars` §12.4.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`flow-lsp/Diagnostics/ScaleLintAnalyzer.cs`** — analyzer pattern for new
  REQ-1 diagnostics. Walk Program.Statements, accumulate Diagnostics list,
  emit via CombinedDiagnosticsPublisher. New analyzers (UnusedImport,
  UnreachableSection, ShadowedVariable) extend this shape.
- **`flow-lsp/Symbols/BuiltInIndex.cs`, `StdlibSymbolIndex.cs`,
  `UserSymbolIndex.cs`** — symbol resolution sources for the completion
  filter. The import-filter walks the file's `use` statements and intersects
  against StdlibSymbolIndex's `(module, symbol)` mapping.
- **`flow-lsp/ParseSession.cs`** — incremental parse cache. Diagnostic +
  completion handlers share the cached `ParseResult` so the 3 new filters
  in REQ-2 don't re-parse.
- **`flow-lang/Ast/`** — AST node types for the diagnostic analyzers to walk.
  `ImportStatement` (UnusedImport), `SectionDeclaration` + `SongExpression`
  (UnreachableSection), `VariableDeclaration` shadowing detection.

### Established Patterns
- **Analyzer-per-diagnostic-type** (Phase 24 D-04) — each new diagnostic type
  is one new file under `flow-lsp/Diagnostics/`. CombinedDiagnosticsPublisher
  invokes each in sequence on every `didChange` parse.
- **Diagnostic source string convention** (Phase 24 D-18) — dotted suffix
  like `"flow.scaleLint"`, `"flow.unusedImport"`, `"flow.unreachableSection"`,
  `"flow.shadowedVariable"` lets editors filter independently.
- **Zero flow-lang touch for LSP-only work** (Phase 24 directive) — LSP
  improvements live in `flow-lsp/`; new comment forms (REQ-4) are the only
  flow-lang side change this phase (SimpleLexer).
- **TextMate + LSP semantic tokens hybrid** (Phase 17 D-04) — grammar gives
  baseline coloring before the server starts; LSP refines. Phase 31 adds to
  grammar; semantic-token contribution for the new scopes is optional in plan.

### Integration Points
- **PragmaRegistry**: Phase 31 D-03 leaves `scaleLint` in `KnownPragmas` as a
  no-op (analyzer always runs). The pragma still parses cleanly so v1.3
  scripts that declared it don't error.
- **DiagnosticsPublisher.cs**: REQ-1 entry point — extend to route new
  analyzer outputs through the published-diagnostics LSP notification.
- **CompletionHandler.cs:273**: REQ-2 entry point — wrap the existing
  completion list with the 3 new filters before returning.
- **`flow-jetbrains/` (new directory)**: Phase 31's first JetBrains touch.
  D-10 says it always lands. Researcher needs to read LSP4IJ docs to figure
  out the minimal `plugin.xml` shape that wraps an external LSP server.

</code_context>

<specifics>
## Specific Ideas

- **Varargs glyph** is the Unicode ellipsis (U+2026) `…`, NOT three ASCII dots.
  The reasoning is hover-panel tightness, not stylistic preference — width
  matters when variadic functions show up alongside other tooltip text.
- **scaleLint** stays editor-side for opt-out. The user explicitly rejected
  introducing `enable noScaleLint;` opposite-pragma vocabulary.
- **TextMate scope `.flow` suffix** is universal — not just function calls and
  variables, also every new comment form (D-07).
- **`flow-jetbrains/` scaffolding** is not contingent on the stretch bar.
  Researcher should plan the Gradle skeleton as a mandatory deliverable, with
  the "opens .flow with completions" demo as a separate verification gate.

</specifics>

<deferred>
## Deferred Ideas

- VSCode Marketplace + OpenVSX publish (still deferred to v1.5 — Phase 17
  HUMAN-UAT rows 4-5 continue to pend).
- JetBrains Marketplace publish (Phase 31 ships only a `.zip`).
- macOS / Windows certification of the LSP and VSCode extension.
- LSP code actions, refactors, formatter, rename-symbol, find-references.
- Real-time LSP latency benchmarking.
- Auto-import quick fixes.
- Multi-line `Note:` block comments.
- Customizable comment-style mapping (composers cannot rename `Note:` →
  `Remark:`).
- `;`-as-statement-separator semantics (Flow stays whitespace-significant).
- LSP telemetry (would only ever be opt-in).
- Markdown-embedded Flow code-block highlighting.
- Three-velocity-layer piano (carried over from Phase 29 v1.5 backlog —
  unrelated but adjacent to the v1.4 → v1.5 transition).

</deferred>

---

*Phase: 31-lsp-enhancements-jetbrains-stretch*
*Context gathered: 2026-05-12*
