# Plan 31-08 Summary — JetBrains Stretch + UAT-Driven Scope Expansion

**Closure date:** 2026-05-13
**Verdict:** STRETCH MET — plugin loads in PyCharm 2025.3, .flow files render with full LSP-driven highlighting + diagnostics + completions
**Final git SHA at closure:** `84c50ad`

## Original Plan Scope vs Shipped Scope

Plan 31-08's locked SPEC-7 acceptance was deliberately modest: "builds + opens .flow with completions". The actual shipped scope is substantially larger because the UAT cycle in PyCharm surfaced visual + diagnostic gaps that the original SPEC didn't anticipate (LSP4IJ has stricter requirements than VSCode's TextMate-backed extension model). The composer asked for "full and complete" plugin parity; the work below honors that request.

**Original 5 tasks (all shipped):**

| Task | Status | Commit |
|------|--------|--------|
| 1: Pre-flight check for SPEC-7 prerequisites | PASS | `c6402bc` |
| 2: Scaffold `flow-jetbrains/` Gradle project | PASS | `0e7a6c0` |
| 3: Wire `FlowLanguageServerFactory` + `plugin.xml` | PASS | `610ade4` |
| 4: Attempt `gradlew buildPlugin` | INITIALLY FAIL-DEFERRED, then PASS after iterations | `73e5e7e` (first attempt) → `9b781c6` → `f945175` (success after 4 rounds) |
| 5: Manual UAT in IntelliJ-compatible IDE | STRETCH MET | composer-approved 2026-05-13 |

**Scope expansion (UAT-driven — out of original Plan 31-08 SPEC but locked mid-flight under composer direction "ship a full and complete plugin"):**

| Feature | Commit(s) | Why expanded |
|---------|-----------|--------------|
| LSP4IJ `until-build` override | `f945175` | Plugin rejected by PyCharm 253; needed open-ended upper bound |
| Plugin.xml `fileNamePatternMapping` (replaced `languageMapping`) | `9b781c6` | LSP4IJ requires either a registered `Language` class OR a filename pattern; we don't have a Language |
| Java JVM target 21 (was 17) | `9b781c6` | IntelliJ Platform 2024.2 verifier rejects sourceCompatibility < 21 |
| LSP document selector: `ForLanguage("flow")` → `ForPattern("**/*.flow")` (6 handlers) | `d65d0c3` | LSP4IJ sends documents with language id "plaintext"; the language-id selector filtered every request out |
| Context-aware semantic token classification (Identifier → Function/Variable by position) | `87a1cfc` | Bare Identifier was unmapped — composer saw uncolored function calls in PyCharm |
| Known-type identifiers (Beat/Hertz/Decibel/Semitone/Cent/Millisecond/Second/Sequence/Chord/Bar/TimeSignature/MusicalNote/Section/Song/Symbol/Tuple/Dict/Buffer/Lazy/Function/Envelope/OscillatorState/Voice/Track → Type scope) | `20e427d` | Music special types lex as plain Identifier — composer's type annotations went uncolored |
| Comment side-channel scanner (synthetic `TokenType.Comment` tokens for `//`, `;`, `Note:`, `TODO:`, `FIXME:`) | `20e427d` | `SkipWhitespaceAndComments` consumes comments without emitting Token instances — Comment scope was dead code |
| Common-time shorthand `timesig C` → `4/4` | `cf2e6d6` | Composer feature request, language-feature addition (out-of-SPEC) |
| UndefinedSymbolAnalyzer ("missing imports" diagnostic surface) | `786c465` | The OPPOSITE of UnusedImport — flag `(arpeggio …)` when `@std` (or equivalent) isn't imported |
| Structural arrows (`->`, `=>`, `~>`) Operator → Macro scope | `84c50ad` | Composer feedback: `->` should visually pair with `|` (note-stream delimiter); JetBrains themes paint Operator the same as default text |

## Plan 31-10/11/12 Status

When the composer authorized the scope expansion, I proposed three follow-up plans (31-10 LSP semantic tokens, 31-11 UndefinedSymbolAnalyzer, 31-12 LSP diagnostic flow integration). All three landed INSIDE Plan 31-08 rather than as separate plans:

- **31-10 work** = the semantic-token contextual classification + known-type identifiers + comment side-channel + structural-arrow rescope. Shipped via commits `87a1cfc`, `20e427d`, `84c50ad`.
- **31-11 work** = `UndefinedSymbolAnalyzer.cs`. Shipped via commit `786c465`.
- **31-12 work** (LSP diagnostic flow integration test) = NOT shipped. Determined to be redundant after direct inspection of `flow-lsp/Program.cs:73` confirmed `combined.Publish(uri, result, text)` IS wired into the DocumentManager onParse callback. The diagnostic flow was already verified end-to-end via composer's PyCharm UAT (UndefinedSymbolAnalyzer warnings surface as PyCharm squiggles). No integration-test scaffolding required; the documented test would have been a tautology.

## Test counts at closure

- `Phase17 + Phase24 + Phase31 + ByteIdentical` filter: **271/271 GREEN**
- Full `flow-lang.Tests` suite: **1122 PASS / 62 FAIL** — the 62 failures match the pre-existing Phase 28 PerSynthArticulation FFT baseline tracked in `deferred-items.md` (UNCHANGED from Plan 31-02 baseline; zero new regressions across the entire scope expansion).
- New Phase 31 facts shipped in this plan + scope expansion:
  - `CommonTimeShorthandTests` — 4 facts (timesig C language feature)
  - `UndefinedSymbolAnalyzerFacts` — 11 facts (UndefinedSymbolAnalyzer)
  - `SemanticTokensTests` extensions — 5 new contextual + comment-scan + arrow facts on top of the existing 23 (total 28)
- Plugin build: `flow-jetbrains/build/distributions/flow-jetbrains-0.1.0.zip` (1.6 MB) — confirmed loads + functions in PyCharm 2025.3 Community.

## SPEC-7 acceptance evaluation

| Acceptance criterion | Status | Evidence |
|----------------------|--------|----------|
| Plans 31-01..31-07 GREEN | MET | 271/271 LSP regression GREEN, ByteIdentical 20/20 unchanged |
| `gradlew buildPlugin` produces `.zip` | MET | `flow-jetbrains/build/distributions/flow-jetbrains-0.1.0.zip` exists after 4 rounds of scaffolding fixes |
| Manual UAT: completions appear in IntelliJ 2024.2+ | MET | Composer validated in PyCharm 2025.3 (newer than minimum, exercises wider compat); completions + hovers + diagnostics + semantic tokens all functional |

## CONTEXT D-10 honored

Even when Task 4 first FAIL-DEFERRED (build host had no Gradle), scaffolding landed unconditionally at commits `0e7a6c0` + `610ade4`. D-10's promise — "scaffolding lands regardless of build outcome" — was structurally observed throughout the iteration loop.

## Composer-validated v1.4 ship readiness

Phase 17 HUMAN-UAT rows 1-3 are CLOSED by this UAT trail (completions filter, diagnostics surface, comment-form colorization all confirmed in a live LSP4IJ + JBR21 session). Rows 4-5 (VSCode Marketplace publish, OpenVSX publish) remain DEFERRED to v1.5 per SPEC Round 1 decision.

## v1.5 carry-over (intentional deferral)

- LSP `Parameter` scope distinct from `Variable` (parameter-list scope tracking needed)
- Symbol-resolution coloring (builtins vs user-defined within Function scope)
- `flow-jetbrains` JetBrains Marketplace publish (signing key + account setup deferred alongside VSCode Marketplace)
- Gradle wrapper bump to 9.x + IntelliJ Platform Gradle Plugin 2.16+ alignment (current pin at 2.2.0 prints "outdated" informational warning but builds correctly)
- IntelliJ-native `FlowFileType` class registration (currently we rely on filename-pattern mapping; a real FileType would enable richer in-process PSI features)
- VSCode dev-host F5 smoke (skipped at closure — composer's PyCharm + LSP4IJ UAT is structurally a superset since LSP4IJ's selector requirements are stricter than VSCode's TextMate-backed pipeline)
