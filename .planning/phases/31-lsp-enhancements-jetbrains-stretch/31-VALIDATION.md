---
phase: 31
slug: lsp-enhancements-jetbrains-stretch
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-05-12
---

# Phase 31 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Extracted from `31-RESEARCH.md` § Validation Architecture as the canonical validation artifact (Dimension 8e gate).

---

## Test Infrastructure

### Unit / Integration (flow-lang.Tests + flow-lsp.Tests subprojects via xUnit)

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.x (existing in `flow-lang.Tests.csproj`) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase31" --logger "console;verbosity=minimal"` |
| **Full suite command** | `dotnet test --logger "console;verbosity=minimal"` |
| **Estimated runtime** | ~10 sec (Phase31 filter, per SPEC runtime budget) / ~3-5 min (full suite) |

### Grammar Snapshot (vscode-tmgrammar-snap)

| Property | Value |
|----------|-------|
| **Framework** | vscode-tmgrammar-snap 0.1.3 |
| **Config file** | `vscode-extension/package.json` scripts |
| **Quick run command** | `cd vscode-extension && npm run test:grammar` |
| **Regenerate command** | `cd vscode-extension && npm run test:grammar:update` |

### JetBrains Stretch (SPEC-7 manual UAT)

| Property | Value |
|----------|-------|
| **Build command** | `cd flow-jetbrains && ./gradlew buildPlugin` (downloads Gradle 8.6 on first run) |
| **Artifact path** | `flow-jetbrains/build/distributions/flow-jetbrains-0.1.0.zip` |
| **Manual UAT** | IntelliJ Community 2024.2+ → Settings → Plugins → Install from disk → select the .zip → open `examples/tutorial.flow` → cursor in `proc ...` body → trigger completion → assert flow-lsp items appear |

---

## Sampling Rate

- **Per task commit:** `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase31"` (under 10s budget per SPEC constraint)
- **Per wave merge:** `dotnet test --logger "console;verbosity=minimal"` (full suite) + `cd vscode-extension && npm run test:grammar`
- **Phase gate:** Full suite green + grammar snapshot green + manual UAT (SPEC-7 stretch + SPEC-5 VSCode dev-host smoke) before `/gsd-verify-work`
- **Max feedback latency:** 10 sec for per-task; 5 min for per-wave

---

## Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| SPEC-1 | UnusedImport Warning emitted for unreferenced `use "@module"` | unit | `dotnet test --filter "Name~UnusedImportAnalyzer"` | ❌ Wave 0 |
| SPEC-1 | UnreachableSection Information emitted for orphan `section` | unit | `dotnet test --filter "Name~UnreachableSectionAnalyzer"` | ❌ Wave 0 |
| SPEC-1 | ShadowedVariable Warning emitted for nested-scope shadow | unit | `dotnet test --filter "Name~ShadowedVariableAnalyzer"` | ❌ Wave 0 |
| SPEC-1 | scaleLint default-on (no `enable scaleLint;` needed) | unit | `dotnet test --filter "Name~ScaleLintDefault"` | ❌ Wave 0 |
| SPEC-2 | CompletionHandler filters out `arpeggio` when `@harmony` not imported | unit | `dotnet test --filter "Name~FilterByImports"` | ❌ Wave 0 |
| SPEC-2 | CompletionHandler filters `H4` when `enable hAsB;` absent | unit | `dotnet test --filter "Name~FilterByPragmas"` | ❌ Wave 0 |
| SPEC-2 | CompletionHandler boosts roman-numerals inside `key { }` | unit | `dotnet test --filter "Name~BoostByMusicalContext"` | ❌ Wave 0 |
| SPEC-3 | `FormatSignature` renders varargs with U+2026 | unit | `dotnet test --filter "Name~FormatSignature"` | ❌ Wave 0 |
| SPEC-3 | HoverHandler renders `(concat str: String…)` | unit | `dotnet test --filter "Name~HoverHandlerTests" --filter "DisplayName~vararg"` | ✅ (extend `HoverHandlerTests.cs`) |
| SPEC-3 | SignatureHelpHandler renders varargs in Label | unit | `dotnet test --filter "Name~SignatureHelpHandlerTests" --filter "DisplayName~vararg"` | ✅ (extend `SignatureHelpHandlerTests.cs`) |
| SPEC-4 | Lexer skips `;` at column-0 | unit | `dotnet test --filter "Name~Phase31LexerCommentForms"` | ❌ Wave 0 |
| SPEC-4 | Lexer skips `TODO:` / `FIXME:` lead-ins | unit | same as above | ❌ Wave 0 |
| SPEC-4 | String `"TODO: x"` NOT skipped | unit | same as above | ❌ Wave 0 |
| SPEC-4 | Existing `enable hAsB;` still parses (Option A canary) | unit | `dotnet test --filter "Name~PragmaScannerFacts"` (existing) | ✅ |
| SPEC-5 | TextMate grammar function-call vs identifier scope | grammar-snapshot | `cd vscode-extension && npm run test:grammar` | ✅ (extend `tests/grammar/sample.flow.snap` after regeneration) |
| SPEC-5 | TextMate grammar 4 comment forms scope correctly | grammar-snapshot | same as above | ❌ Wave 0 (new fixture: `tests/grammar/comment-forms.flow`) |
| SPEC-6 | All 70+ `tests/test_*.flow` still parse | smoke (existing) | `dotnet run --project flow-interpreter tests/test_h_alias.flow` (manual loop or scripted) | ✅ |
| SPEC-6 | Phase 18/25/27/28 ByteIdentical*Tests stay GREEN | unit (existing) | `dotnet test --filter "Name~ByteIdentical"` | ✅ |
| SPEC-7 | `flow-jetbrains/` scaffolding files exist | structural | `test -f flow-jetbrains/build.gradle.kts && test -f flow-jetbrains/src/main/resources/META-INF/plugin.xml` | ❌ Wave 0 |
| SPEC-7 | `./gradlew buildPlugin` produces a .zip | manual / CI-extension (defer to v1.5) | `cd flow-jetbrains && ./gradlew buildPlugin` | ❌ Wave 0 |
| SPEC-7 | Manual UAT: IntelliJ + plugin .zip + open .flow shows completions | manual-only | (manual checklist in 31-VERIFICATION.md) | — |

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Unit/Phase31/Phase31LexerCommentFormsTests.cs` — covers SPEC-4 (semicolon, TODO:, FIXME: lead-in detection + string-literal exclusion + `enable hAsB;` canary)
- [ ] `flow-lang.Tests/Unit/Phase31/UnusedImportAnalyzerFacts.cs` — SPEC-1 unused-import
- [ ] `flow-lang.Tests/Unit/Phase31/UnreachableSectionAnalyzerFacts.cs` — SPEC-1 unreachable-section
- [ ] `flow-lang.Tests/Unit/Phase31/ShadowedVariableAnalyzerFacts.cs` — SPEC-1 shadowed-variable
- [ ] `flow-lang.Tests/Unit/Phase31/ScaleLintDefaultOnFacts.cs` — SPEC-1d / D-03 (analyzer activates without pragma; pragma still parses as no-op)
- [ ] `flow-lang.Tests/Unit/Phase31/CompletionFilterFacts.cs` — SPEC-2 (3 filters)
- [ ] `flow-lang.Tests/Unit/Phase31/VarargsRenderingFacts.cs` — SPEC-3 (`FormatSignature` + extend `HoverHandlerTests.cs` + `SignatureHelpHandlerTests.cs`)
- [ ] `flow-lang.Tests/Unit/Phase31/LspFixtures.cs` shared fixtures (or reuse Phase 17's existing `LspFixtures.cs` — Plan 31-01 Task 2 extends it with `StdlibIndex()` helper)
- [ ] `vscode-extension/tests/grammar/comment-forms.flow` + regenerated `.snap` — SPEC-4 / SPEC-5
- [ ] `vscode-extension/tests/grammar/function-calls.flow` + regenerated `.snap` — SPEC-5

*Framework install: NONE — xunit / vscode-tmgrammar-snap / Gradle wrapper already wired.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| IntelliJ Community 2024.2+ loads flow-jetbrains .zip and serves completions on `examples/tutorial.flow` | SPEC-7 (stretch acceptance) | Plugin install + GUI interaction; no headless automation available for IntelliJ plugin sandboxing in this phase | Plan 31-08 Task 5 checkpoint: install plugin via Settings → Plugins → Install from Disk, open tutorial.flow, trigger Ctrl+Space inside a proc body, verify flow-lsp items appear + Unicode ellipsis renders on `concat` vararg hover |
| VSCode dev-host smoke (SPEC-4 + SPEC-5 lit) | SPEC-4, SPEC-5 | Visual confirmation that the new comment forms colorize and function-call/variable-ref scopes apply per theme | Phase closure (31-09): open `examples/tutorial.flow` in `code --extensionDevelopmentPath=vscode-extension`, confirm column-0 `;`, `Note:`, `TODO:`, `FIXME:` lines pick up comment color; `(print …)` shows function-call coloring |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify (manual UAT in Plan 31-08 is the only manual-gate; all other tasks have unit / grammar-snapshot verification)
- [ ] Wave 0 covers all MISSING references (7 new unit test files + 2 new grammar fixtures + 1 shared LspFixtures extension)
- [ ] No watch-mode flags
- [ ] Feedback latency < 10 sec per task / < 60 sec for full Phase 31 suite (per SPEC runtime budget)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending (will be approved at closure once all gates green)
