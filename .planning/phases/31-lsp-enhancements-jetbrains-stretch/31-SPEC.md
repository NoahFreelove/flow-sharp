# Phase 31: LSP Enhancements + JetBrains Stretch — Specification

**Created:** 2026-05-10
**Ambiguity score:** 0.19
**Requirements:** 7 locked

## Goal

Close the four major gaps in `flow-lsp` and the VSCode TextMate grammar that hurt music-production and functional-language audiences: (1) richer diagnostics with structured severity beyond the current Error-only set; (2) context-aware completion that filters by `use`d modules + pragma state + musical-context (key/tempo/timesig); (3) varargs visibility in signature help and hovers; (4) grammar enhancements covering `;` Lisp-style comments, `Note:` / `TODO:` / `FIXME:` lead-in forms, and distinct function-call vs identifier coloring. Lexer + grammar changes prioritize improvement over backward compatibility — v1.3 scripts may need adjustment if they rely on now-reserved token forms. Stretch: a JetBrains plugin via LSP4IJ shipped on a "builds + opens .flow with completions" bar. VSCode Marketplace publish deferred to v1.5.

## Background

Today (post-Phase 28 baseline):

**flow-lsp structure:**
- 7 handlers: `CompletionHandler.cs` (273 lines), `HoverHandler.cs` (123 lines), `DefinitionHandler.cs`, `DiagnosticsPublisher.cs` (60 lines), `SemanticTokensHandler.cs`, `SignatureHelpHandler.cs`, `TextDocumentSyncHandler.cs`
- 5 diagnostics modules: `CombinedDiagnosticsPublisher.cs`, `DiatonicSpellings.cs`, `IScaleLintPublisher.cs`, `ScaleLintAnalyzer.cs`, `ScaleLintPublisher.cs`
- 4 symbol indices: `BuiltInIndex.cs`, `KeywordIndex.cs`, `StdlibSymbolIndex.cs`, `UserSymbolIndex.cs`
- `ParseSession.cs` for incremental parsing; `DocumentManager.cs` for document state
- `LspMappings.cs` for Flow ↔ LSP type translation
- 16+ unit tests under `flow-lang.Tests/Unit/Phase17/`

**Known gaps (from SEED-001):**
- Completions don't filter by `use`d modules (suggests `(arpeggio ...)` even if `@harmony` not imported)
- Completions don't reflect feature-flag / pragma state (suggests `H4` notes even if `enable hAsB;` not set)
- Diagnostics surface is Error-only; no Information or Warning severity output
- Varargs functions don't surface their variadic shape in completion tooltips or hovers
- Lexer recognizes only `//` and `/* */` comments; common lead-in forms (`Note:`, `TODO:`, `FIXME:`) and Lisp-style `;` are not treated as comments
- TextMate grammar doesn't distinguish function calls from identifiers — `(myFunc x)` and `myVar` colorize identically

**VSCode extension state:**
- `vscode-extension/package.json` reports `version: 0.1.0`, `publisher: flow-language`. NOT yet published to Marketplace or OpenVSX. Phase 17 HUMAN-UAT rows 4-5 (Marketplace + OpenVSX publish) were deferred to "first release tag" — Phase 31 defers them further to v1.5.

**Local dev wiring already in place:**
- `vscode-extension/.vscode/launch.json` runs the extension dev host
- `vscode-extension/server/linux-x64` symlinked to `flow-lsp/bin/Debug/net10.0/`
- Live LSP iteration works without re-publishing

**JetBrains plugin landscape:**
- LSP4IJ (the LSP-to-IntelliJ-Platform bridge) is mature and well-documented. Wrapping `flow-lsp` is "mechanical" per SEED-001.
- No `flow-jetbrains/` project exists yet.
- JetBrains Marketplace publish requires a developer account (free for open-source plugins) — same shape as VSCode Marketplace publish.

**Backward-compat lean (locked in Round 2):**
- v1.3 `.flow` scripts may need adjustment if they use identifiers that collide with new lexer tokens (`Note:` at the start of a line becomes a comment; scripts that have `Note:` as a string literal inside `(print "Note: ...")` continue to work — the change is position-sensitive). Per `project_pre_public_no_legacy_burden`, breaking changes are acceptable in one commit.

## Requirements

1. **Structured diagnostic severity expansion**: Diagnostics gain Information and Warning levels beyond the current Error-only set.
   - Current: `DiagnosticsPublisher.cs` (60 lines) emits Error-severity diagnostics from parse failures only
   - Target: Diagnostics surface includes: (a) **Unused imports** (Warning) — `use "@module"` declared but no identifier from that module referenced; (b) **Unreachable section** (Information) — `section name { ... }` defined but never referenced in any `Song`; (c) **Out-of-key note in `key { ... }` context** (Information) — already partially implemented via `ScaleLintAnalyzer.cs` for `enable scaleLint;`; Phase 31 promotes this from opt-in pragma to default-Information; (d) **Shadowed variable** (Warning) — same identifier declared twice in nested scopes. Structured severity model in `flow-lsp` exposes these uniformly through `CombinedDiagnosticsPublisher`
   - Acceptance: A test fixture with each of the 4 issues triggers the appropriate severity diagnostic via the LSP `textDocument/publishDiagnostics` notification. Verified via unit tests on each new analyzer class

2. **Context-aware completion filtering**: Completions reflect what's actually in scope.
   - Current: `CompletionHandler.cs:273` suggests every builtin + every stdlib symbol regardless of imports or pragmas
   - Target: Three new filters applied to the completion list:
     - **Import-filter**: builtins from `@harmony` only appear if `use "@harmony"` (or `use "@std"` which transitively imports it) is in the file
     - **Pragma-filter**: completions reflect active pragmas. `H4` / `H5` notes appear in note-stream context only if `enable hAsB;` is declared
     - **Musical-context-filter**: inside a `key Cmajor { ... }` block, the `chord` builtin and roman-numeral surface (`I`, `ii`, `IV`, `V7`, `vi`) bubble to the top of the suggestion list with higher priority weights
   - Acceptance: Test fixtures show different completion sets for files with/without specific imports + pragmas. Unit tests assert that `(arpeggio` is NOT suggested when `@harmony` is not imported, and IS suggested when it is. Roman-numeral suggestions only appear inside `key { ... }` blocks

3. **Varargs visibility in signature help + hovers**: Variadic function shapes surface in tooltips.
   - Current: `OverloadResolver.cs` knows about varargs but `LspMappings.cs` doesn't expose the variadic shape; signature help shows `(funcName arg1)` for a 0-or-more-args function
   - Target: Varargs parameters render as `param: Type...` (trailing ellipsis) in signature help, hovers, and completion tooltips. The ellipsis is a literal Unicode character `…` (U+2026) or three dots `...` — decision in plan-phase
   - Acceptance: Hovering `concat` (which takes Strings varargs) in a `.flow` file via the LSP shows `(concat str: String...)` in the hover panel. Same for `dict`, `list`, and any other varargs-taking stdlib builtin

4. **Grammar enhancements — comment forms**: SimpleLexer and TextMate grammar recognize Lisp + lead-in comment styles.
   - Current: SimpleLexer recognizes `//` and `/* */`. TextMate grammar matches these for VSCode highlighting
   - Target: Four new comment recognitions:
     - **`;` Lisp-style line comment**: `;` to end-of-line treated as a comment. Lexer skips it; TextMate grammar colorizes as comment
     - **`Note:` lead-in**: A line that starts with `Note:` (optionally preceded by whitespace) is a comment to end-of-line. Existing string-literal `"Note: ..."` inside `(print)` is UNAFFECTED (string-literal context wins). The chapter-divider convention used throughout `tutorial.flow` becomes first-class comments rather than bare-expressions
     - **`TODO:` / `FIXME:` lead-ins**: Same shape as `Note:` but distinct grammar scope so editors can color them differently (e.g. orange for `TODO`, red for `FIXME`)
     - Lexer changes break v1.3 backward compatibility intentionally per Round 2 decision. Per-fixture migrations land in this phase if existing `.flow` files contain ambiguous patterns
   - Acceptance: A `.flow` file with all 4 new comment forms lexes without errors, executes without runtime impact. VSCode opens the file with correct colorization for each form. Existing `tutorial.flow` chapter-divider `Note:` lines render as comments not as parse errors

5. **Grammar enhancement — function-call coloring**: TextMate grammar distinguishes `(funcName ...)` from bare identifiers.
   - Current: `vscode-extension/syntaxes/flow.tmLanguage.json` has one identifier scope for all identifier-shaped tokens
   - Target: Two distinct grammar scopes: `entity.name.function.flow` for the head of any `(identifier ...)` form, `variable.other.flow` for bare identifier references. Snippets in `vscode-extension/snippets/flow.code-snippets` may need style touch-up to match. The grammar change is purely visual — semantics unchanged
   - Acceptance: A composer opens a `.flow` file in VSCode and the function-call positions visually contrast against variable references (verified manually + via grammar snapshot tests in `vscode-extension/tests/grammar/`)

6. **Lexer migration of in-repo v1.3 fixtures**: Tutorial + showcase + every test fixture continues to parse + render under the new lexer.
   - Current: `examples/tutorial.flow`, `examples/showcase.flow`, all `tests/test_*.flow`, all `examples/pragmas/*.flow` use existing comment forms only
   - Target: Audit every committed `.flow` file for token-pattern collisions with the new comment forms. Where collisions exist (e.g. a bare expression `Note: 5` at column 0 that was parsing as identifier-colon-int) migrate to an unambiguous form. Where existing `Note:` lines were already comments-in-spirit (chapter dividers in tutorial.flow), they now become first-class comments — this is an upgrade, not a regression. Full unit suite must remain GREEN
   - Acceptance: All 4 v1.3 Phase 27 fixtures (tutorial.flow, showcase.flow, h_alias.flow, microtonal_ji.flow) plus all `tests/test_*.flow` plus all Phase 28 ragtime fixtures parse + render + smoke-pass under the new lexer. Two-run byte-identical determinism preserved (Phase 18/25/27 ByteIdentical test classes remain GREEN)

7. **JetBrains plugin stretch — builds + opens .flow with completions**: A JetBrains plugin wrapping `flow-lsp` via LSP4IJ ships if all mandatory work lands cleanly.
   - Current: No `flow-jetbrains/` project; no plugin descriptor; no LSP4IJ integration
   - Target: New `flow-jetbrains/` directory containing IntelliJ Platform plugin scaffolding (Gradle build, `plugin.xml` descriptor) that wraps `flow-lsp` via LSP4IJ. Stretch ships when: (a) all 6 mandatory requirements above are GREEN, (b) the plugin builds via `gradlew buildPlugin`, (c) loading the plugin into a development IntelliJ instance + opening a `.flow` file shows working completions. Plugin is NOT published to JetBrains Marketplace this phase (matches VSCode Marketplace deferral). Plugin distribution is via the built `.zip` artifact attached to the v1.4 release tag
   - Acceptance: If stretch is met, `flow-jetbrains/build/distributions/flow-jetbrains-*.zip` exists; manually loading it in IntelliJ Community + opening a `.flow` file shows completions from the LSP. If stretch is NOT met (any mandatory area still red), the plugin descriptor + Gradle scaffolding may still land for v1.5 follow-up, but the acceptance for THIS requirement is "documented as deferred to v1.5" — explicit, not silent

## Boundaries

**In scope:**
- 4 new diagnostic types (Unused import Warning, Unreachable section Information, Out-of-key Information default, Shadowed variable Warning)
- Promote `scaleLint` from opt-in pragma to default-Information severity
- Import-filter, Pragma-filter, Musical-context-filter for completions
- Varargs `param: Type...` rendering in signature help, hover, completion tooltips
- New comment forms: `;`, `Note:`, `TODO:`, `FIXME:` (lexer + TextMate grammar)
- Function-call coloring (`entity.name.function.flow` grammar scope)
- Audit + migration of in-repo `.flow` files for lexer-change collisions
- JetBrains plugin scaffolding (stretch: ship the built plugin .zip alongside v1.4 release tag)
- Unit tests for every new behavior; existing Phase 17 test pattern extended
- Optional grammar snapshot test additions for new comment forms + function-call coloring

**Out of scope:**
- VSCode Marketplace publish (Phase 17 HUMAN-UAT rows 4-5) — deferred to v1.5 per Round 1 decision; the marketplace process requires Microsoft + Eclipse Foundation publisher accounts + secrets that aren't set up
- OpenVSX publish — same deferral
- JetBrains Marketplace publish — Phase 31 ships a .zip artifact, not a Marketplace listing
- macOS / Windows LSP testing — Linux dev path only this phase; matches Phase 30's platform scope
- LSP protocol-level integration tests (subprocess + JSON-RPC stub) — unit-only test approach locked in Round 2
- New LSP capabilities beyond the 4 work areas — code actions, refactors, formatter, rename-symbol, find-references are all v1.5+ scope
- Real-time LSP performance benchmarking — Phase 31 doesn't promise latency targets
- Auto-import / quick-fix code actions — deferred
- `;`-as-statement-separator semantics — `;` is a comment, NOT a statement separator (Flow is whitespace-significant for statement boundaries)
- Customizable comment-style configuration — `Note:` is hardcoded; users cannot remap to e.g. `Remark:`
- Multi-line `Note:` blocks — comment forms are line-terminated only

**Adjacent problems excluded:**
- LSP for Vim / Helix / Emacs — `flow-lsp` already speaks LSP so these clients work today via standard `lspconfig.lua` / equivalent. No phase-specific work needed.
- VSCode extension auto-update — composer manually pulls the .vsix this phase; auto-update lives in v1.5 alongside `flow update`
- Telemetry from LSP usage — never in scope without explicit opt-in
- Cross-extension interaction (e.g. with Markdown extensions for embedded Flow code blocks) — out of scope

## Constraints

- **Linux x64 dev path**: All testing on Linux x64. VSCode dev-host wiring (`vscode-extension/.vscode/launch.json`) is Linux-tested; macOS/Windows VSCode users can still install the extension but Phase 31 doesn't certify those paths.
- **Backward compatibility intentionally NOT preserved**: Lexer changes may invalidate v1.3 `.flow` scripts that contain `;` outside of strings/comments, or bare-expression `Note:` / `TODO:` / `FIXME:` patterns at column 0. Pre-public lean (per `project_pre_public_no_legacy_burden` memory) authorizes the break. Phase 31 migrates all in-repo `.flow` files as part of Requirement 6.
- **Two-run byte-identical determinism**: Phase 18 / 25 / 27 / 28 ByteIdentical test classes must stay GREEN. New lexer must produce identical token streams across two runs of the same source.
- **Test runtime budget**: New LSP unit tests must run within 10 seconds total. Phase 17 test pattern is fast; Phase 31 follows it.
- **No new external NuGet dependencies on the LSP side**: `flow-lsp` already pulls `OmniSharp.Extensions.LanguageProtocol`. Phase 31 doesn't add more.
- **JetBrains stretch dependency**: LSP4IJ artifact pulled from JetBrains' public Maven repository at Gradle build time. No vendored copy in the repo.
- **JetBrains plugin built but not published**: Plugin .zip attaches to v1.4 release tag if stretch is met. JetBrains Marketplace requires publisher account + signing key — both deferred.
- **`scaleLint` default**: Promoted from opt-in `enable scaleLint;` to default-on Information severity. Composers who don't want it can declare `enable noScaleLint;` (new opposite pragma — decided in plan-phase) OR the diagnostic stays opt-out (no pragma needed to silence; only opt-in to enable). Mechanism decided in plan-phase.

## Acceptance Criteria

- [ ] `flow-lsp/Diagnostics/` contains analyzers for: UnusedImport (Warning), UnreachableSection (Information), ShadowedVariable (Warning); plus the existing ScaleLintAnalyzer now wired to default-Information
- [ ] Unit tests cover each new diagnostic with a test fixture triggering it; severity asserted via published-diagnostics shape
- [ ] `CompletionHandler.cs` filters out builtins from non-imported stdlib modules; unit test asserts `arpeggio` is suggested IFF `@harmony` is in scope
- [ ] `CompletionHandler.cs` filters note-stream completions by active pragma set; unit test asserts `H4` is suggested IFF `enable hAsB;` is declared
- [ ] `CompletionHandler.cs` boosts roman-numeral + chord completions inside `key { ... }` blocks; unit test verifies ranking
- [ ] `SignatureHelpHandler.cs` + `HoverHandler.cs` render varargs as `param: Type...` (or `param: Type…` — locked in plan-phase); unit tests over `concat`, `dict`, `list` all show the variadic shape
- [ ] `SimpleLexer.cs` recognizes `;` as line-comment start; unit test verifies token stream
- [ ] `SimpleLexer.cs` recognizes `Note:` / `TODO:` / `FIXME:` lead-ins (column-0 or whitespace-only-prefix) as comments to end-of-line; unit tests cover each
- [ ] String-literal context unaffected: `(print "Note: hello")` still produces a string token, not a comment
- [ ] `vscode-extension/syntaxes/flow.tmLanguage.json` distinguishes function-call positions (`entity.name.function.flow`) from identifier references (`variable.other.flow`); grammar snapshot test under `vscode-extension/tests/grammar/` asserts the new scopes
- [ ] `vscode-extension/syntaxes/flow.tmLanguage.json` colorizes `;`, `Note:`, `TODO:`, `FIXME:` as comments
- [ ] All 4 Phase 27 fixtures + all `tests/test_*.flow` + Phase 28 ragtime fixtures parse + render under the new lexer; full unit suite GREEN
- [ ] Phase 18 / 25 / 27 / 28 ByteIdentical two-run tests stay GREEN
- [ ] Manual LSP smoke in `vscode-extension/.vscode/launch.json` dev-host: open `examples/tutorial.flow`, verify completions reflect imports + pragmas; verify diagnostics render at correct severity; verify comment forms colorize
- [ ] (Stretch) `flow-jetbrains/` directory exists with Gradle build + plugin.xml
- [ ] (Stretch) `gradlew buildPlugin` produces `flow-jetbrains/build/distributions/flow-jetbrains-*.zip`
- [ ] (Stretch) Plugin loaded into a dev IntelliJ instance + opening a `.flow` file shows completions; manual UAT
- [ ] (Stretch or deferred) Phase closure documents JetBrains plugin status: shipped as .zip or deferred to v1.5

## Ambiguity Report

| Dimension          | Score | Min  | Status | Notes                                                                                          |
|--------------------|-------|------|--------|------------------------------------------------------------------------------------------------|
| Goal Clarity       | 0.90  | 0.75 | ✓      | 4 mandatory work areas locked + JetBrains stretch criterion explicit                           |
| Boundary Clarity   | 0.80  | 0.70 | ✓      | Marketplace publish, LSP protocol-level tests, code actions, formatter all explicit-deferred   |
| Constraint Clarity | 0.75  | 0.65 | ✓      | Backward-compat dropped per Round 2 user decision; Linux x64 only; ≤10s test budget            |
| Acceptance Criteria| 0.72  | 0.70 | ✓      | 15+ pass/fail criteria; stretch criterion explicit                                             |
| **Ambiguity**      | 0.19  | ≤0.20| ✓      | Gate passed                                                                                    |

## Interview Log

| Round | Perspective       | Question summary                                                              | Decision locked                                                                                                                    |
|-------|-------------------|-------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------|
| 1     | Researcher        | Which of the 6 SEED-001 work areas are required?                              | 4: diagnostics expansion, context-aware completion, varargs visibility, grammar enhancements. Marketplace publish + JetBrains carved out separately. |
| 1     | Researcher        | VSCode Marketplace + OpenVSX publish — in this phase?                         | No — defer to v1.5. Phase 17 HUMAN-UAT rows 4-5 continue to defer.                                                                |
| 1     | Researcher        | JetBrains plugin — required, stretch, deferred?                               | Stretch — ships if mandatory work lands cleanly.                                                                                  |
| 2     | Failure Analyst   | Test approach — unit-only or end-to-end?                                      | Unit tests against handler classes (existing Phase 17 pattern extended).                                                          |
| 2     | Failure Analyst   | JetBrains stretch bar?                                                        | Builds + opens .flow with completions; ships as .zip alongside v1.4 release tag, NOT to JetBrains Marketplace.                    |
| 2     | Boundary Keeper   | Grammar / lexer changes — break v1.3 scripts?                                 | Break them if needed to make the LSP better; no backward-compat constraint. Pre-public lean.                                       |

---

*Phase: 31-lsp-enhancements-jetbrains-stretch*
*Spec created: 2026-05-10*
*Next step: /gsd-discuss-phase 31 — implementation decisions (Unicode `…` vs ASCII `...` for varargs rendering; `enable noScaleLint;` opposite-pragma vs silent-by-default for scaleLint default; specific TextMate grammar scope names; LSP4IJ version pin)*
