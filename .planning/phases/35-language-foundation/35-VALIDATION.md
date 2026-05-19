---
phase: 35
slug: language-foundation
status: approved
nyquist_compliant: true
wave_0_complete: true
created: 2026-05-18
---

# Phase 35 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from `35-RESEARCH.md` § "Validation Architecture". Phase 35 ships four language features (spans/diagnostics, test framework, pattern matching, `-> as name`) plus four housekeeping items (HK-01..04). Sampling must remain fast — feature implementation depends on the Wave 1 spans and test framework, so the test loop has to stay tight.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (`flow-lang.Tests/`) for C# unit/integration; pure-Flow `(test ...)` framework (NEW this phase) for composer-facing acceptance once Wave 2 lands |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` |
| **Quick run command** | `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "FullyQualifiedName~Phase35"` |
| **Full suite command** | `dotnet test` (root solution — runs xUnit) + `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t" || break; done` (existing .flow regression) |
| **Estimated runtime** | xUnit quick ~10 s, xUnit full ~90 s, .flow regression ~120 s |

Phase 35-introduced `flow test [path]` subcommand becomes a third command tier once Wave 2 commits — at that point it replaces the bare `for` loop for new composer-authored test_*.flow files (existing 70+ tests continue running via the bare-loop path per CLAUDE.md "verified by their console output").

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "FullyQualifiedName~Phase35"` (≤ 30 s feedback)
- **After every plan wave:** Run `dotnet test` (≤ 90 s feedback)
- **Before `/gsd:verify-work`:** Full suite + `.flow` regression loop must be green
- **Max feedback latency:** 90 seconds (quick + per-task = ≤ 30 s; wave = ≤ 90 s)

---

## Per-Feature Validation Architecture

### Wave 1 — Span migration foundation (LANG-04 prerequisite, blocks every later wave)

| Layer | What gets tested | Test type | Command |
|-------|------------------|-----------|---------|
| Lexer | Every Token carries a Span with start/end line+column matching source position | C# unit | `dotnet test --filter "FullyQualifiedName~LexerSpanTests"` |
| Parser | Every AST `record` ctor sets Span; nested expressions carry parent-spanning Span (start = first child, end = last child) | C# unit (golden AST snapshots) | `dotnet test --filter "FullyQualifiedName~AstSpanTests"` |
| Migration | Existing 70+ `tests/test_*.flow` pass byte-identical output after Span migration | regression | `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done` |
| Determinism | Two-run cmp-clean preserved for Phase 18/25/27/28 baselines | regression | `tests/test_phase_28_determinism.flow` |

### Wave 2a — Diagnostics renderer (LANG-04)

| Layer | What gets tested | Test type | Command |
|-------|------------------|-----------|---------|
| Format | Rust-style header + source-quoted span + caret + label + secondary notes + did-you-mean | golden file | `dotnet test --filter "FullyQualifiedName~DiagnosticRendererGoldenTests"` |
| Levenshtein | Suggestion threshold ≤ 3 produces the right candidate from in-scope identifiers | C# unit | `dotnet test --filter "FullyQualifiedName~LevenshteinSuggestionTests"` |
| TTY | ANSI color when isatty, plain when piped | C# unit (forced env) | `dotnet test --filter "FullyQualifiedName~DiagnosticTtyTests"` |
| Multi-error | ErrorReporter batch renders each diagnostic separately with blank-line separation | golden file | `dotnet test --filter "FullyQualifiedName~MultiErrorRenderingTests"` |
| REPL source | Renderer handles "no file path" REPL source via in-memory SourceMap entry | C# integration | `dotnet test --filter "FullyQualifiedName~ReplDiagnosticTests"` |

**Golden-file strategy.** Diagnostic baselines live under `flow-lang.Tests/baselines/Phase35/diagnostics/`. Baselines are stored ANSI-stripped (plain text) — the TTY test forces color OFF before comparing. Beware: diagnostic line/column numbers are 1-indexed (rustc convention); make this explicit in the renderer + the baseline format header.

### Wave 2b — Pure-Flow test framework (TEST-01, TEST-02)

| Layer | What gets tested | Test type | Command |
|-------|------------------|-----------|---------|
| Builtin registration | `(test ...)`, `(assert ...)`, `(assertEq ...)`, `(assertNotesMatch ...)`, `(assertBytesEqual ...)`, `(assertWithinDb ...)` registered in `InternalFunctionRegistry` | C# unit | `dotnet test --filter "FullyQualifiedName~TestFrameworkBuiltinsTests"` |
| Body deferral | `(test "name" body)` defers body evaluation until the test runner invokes it; passes through `If`-style thunk pattern (`BuiltInFunctions.cs:339`) | C# unit | `dotnet test --filter "FullyQualifiedName~TestBodyDeferralTests"` |
| `flow test` CLI | `dotnet run --project flow-interpreter test tests/` scans `tests/test_*.flow`, runs each `(test ...)` block, prints per-test pass/fail + summary, exits 0 if all pass | C# integration | `dotnet test --filter "FullyQualifiedName~FlowTestCliTests"` |
| Hermetic isolation | Between tests in one FlowEngine process, reset: `MusicalContext` stack, `VoicePool`, `PrngRegistry` (D-v1.5-06 incoming hooks; today's Phase 25 Gaussian PRNG), `ExecutionContext` bindings | C# unit (state-leak detection) | `dotnet test --filter "FullyQualifiedName~HermeticIsolationTests"` |
| `assertWithinDb` semantics | Wraps `RmsRegressionTests.AssertRmsWithinTolerance` — ±0.5 dB / 100 ms tolerance per SPEC-8 | C# unit | `dotnet test --filter "FullyQualifiedName~AssertWithinDbTests"` |
| Meta-test discipline | The test-framework C# tests use xUnit directly — do NOT recursively test the tester in Flow | review/lint | `! grep -rn "(test " flow-lang.Tests/` |

### Wave 3 — Pattern matching (LANG-01, LANG-02)

| Layer | What gets tested | Test type | Command |
|-------|------------------|-----------|---------|
| Lexer | `match`, `|`, `=>`, `_` produce distinct tokens; `|` inside `(match ...)` does NOT confuse note-stream lexing | C# unit | `dotnet test --filter "FullyQualifiedName~MatchLexerTests"` |
| Parser | `(match scrutinee | pat => body | ... | _ => body)` parses to `MatchExpression` with `Ast/Patterns/` arm nodes; note-stream `| C4 D4 |` still parses correctly outside `(match` | golden AST | `dotnet test --filter "FullyQualifiedName~MatchParserTests"` |
| Pattern AST | `LiteralPattern`, `WildcardPattern`, `BindingPattern`, `ConstructorPattern`, `GuardPattern`, music-aware extractor patterns (chord quality, roman numeral, articulation symbol) all round-trip | C# unit | `dotnet test --filter "FullyQualifiedName~PatternAstTests"` |
| Runtime correctness | Each pattern kind matches the right values; first-match-wins (no fall-through) | C# unit + `.flow` integration | `dotnet test --filter "FullyQualifiedName~MatchRuntimeTests"` + `tests/test_pattern_matching.flow` |
| Music-aware extractors | `Cmaj7` matches any chord with quality maj7; `V7` resolves from active key context; `#staccato` matches `Articulation.Staccato` | `.flow` integration | `tests/test_pattern_match_music.flow` |
| Non-exhaustive default | Without pragma: WARN to stderr + fall through to `Void` value (D-v1.5-05 charitable) | C# unit (stderr capture) | `dotnet test --filter "FullyQualifiedName~MatchExhaustivenessDefaultTests"` |
| `enable matchExhaustive;` pragma | With pragma: non-exhaustive match is parse-time / type-time error (per Phase 21 pragma precedent) | C# unit | `dotnet test --filter "FullyQualifiedName~MatchExhaustivePragmaTests"` |
| Pragma scope | Pragma is per-file (ExecutionContext stack frame), not global | C# integration | `dotnet test --filter "FullyQualifiedName~PragmaScopeTests"` |
| Backend choice | If naive-linear-scan compiler (per discuss-phase Q1), validate correctness only; decision-tree migration is deferred follow-up | strategy note | n/a |

### Wave 4 — `-> as name` chain naming (LANG-03)

| Layer | What gets tested | Test type | Command |
|-------|------------------|-----------|---------|
| Lexer / Parser | `seq -> (transpose 2) as melody -> (legato 0.5) as legato-melody -> render` parses to nested calls with intermediate bindings | golden AST | `dotnet test --filter "FullyQualifiedName~AsBindingParserTests"` |
| Disambiguation | `as` token does NOT collide with any existing use (grep first; expect no collisions) | C# unit | `dotnet test --filter "FullyQualifiedName~AsKeywordReservationTests"` |
| Scope | `melody` binding visible to subsequent expressions in the enclosing block / function until block close | C# integration | `dotnet test --filter "FullyQualifiedName~AsBindingScopeTests"` |
| Type carry-through | `melody`'s Value type matches `(transpose 2)`'s return type (no explicit type inference needed — Value carries it) | C# unit | `dotnet test --filter "FullyQualifiedName~AsBindingTypeTests"` |
| Integration | End-to-end composer example renders byte-identical to manual let-rebinding equivalent | `.flow` regression | `tests/test_chain_naming.flow` |

### v1.4 Housekeeping (HK-01..04, parallel-safe with Wave 1)

| Item | What gets tested | Test type | Command |
|------|------------------|-----------|---------|
| HK-01 | `humanizeGaussian` over voice blocks: `ParallelVoices` survives the transform; deterministic with seed | `.flow` regression + C# unit | `tests/test_humanize_voice_blocks.flow` + `dotnet test --filter "FullyQualifiedName~HumanizeGaussianVoiceBlocksTests"` |
| HK-02 | Phase 17 HUMAN-UAT rows 1-3 status = closed (documentation-only per researcher confidence note) | review | grep `.planning/phases/17-*/HUMAN-UAT.md` for `status: closed` on rows 1-3 |
| HK-03 | Phase 04 VERIFICATION.md gap items resolved (read existing gaps, mark resolved) | review | grep `.planning/phases/04-*/VERIFICATION.md` for outstanding ⚠ markers |
| HK-04 | CLAUDE.md "Public as of v1.4" footnote rewritten to post-public deprecation framing | review | grep `CLAUDE.md` for current vs updated footnote text |

---

## Per-Task Verification Map

Phase 35 has no external attack surface (language internals only) — Threat Ref / Secure Behavior columns are N/A throughout. Every plan's Task 1 is a Wave 0 stub; subsequent tasks each carry an `<automated>` verify block. Status starts ⬜ pending; execute-phase flips per task.

| Plan | Wave | Tasks | Requirement | Test Type | Automated Command | Wave 0 Stub | Status |
|------|------|-------|-------------|-----------|-------------------|-------------|--------|
| 35-01 (Span migration) | 1 | 4 | LANG-04 (Span prereq) | unit + regression | `dotnet test --filter "FullyQualifiedName~Phase35.LexerSpanTests\|FullyQualifiedName~Phase35.AstSpanTests\|FullyQualifiedName~Phase35.SpanMigrationRegressionTests"` + `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done` | `flow-lang.Tests/Phase35/LexerSpanTests.cs`, `AstSpanTests.cs`, `SpanMigrationRegressionTests.cs` | ⬜ pending |
| 35-02 (HK closeout) | 1 | 3 | HK-01, HK-02, HK-03, HK-04 | unit + regression + review | `dotnet test --filter "FullyQualifiedName~HumanizeGaussianVoiceBlocksTests"` + `dotnet run --project flow-interpreter tests/test_humanize_voice_blocks.flow` + grep audits per HK item | `flow-lang.Tests/Phase35/HumanizeGaussianVoiceBlocksTests.cs`, `tests/test_humanize_voice_blocks.flow` | ⬜ pending |
| 35-03 (Diagnostics) | 2 | 3 | LANG-04 | unit + golden | `dotnet test --filter "FullyQualifiedName~Phase35.DiagnosticRendererGoldenTests\|FullyQualifiedName~Phase35.LevenshteinSuggestionTests\|FullyQualifiedName~Phase35.DiagnosticTtyTests\|FullyQualifiedName~Phase35.MultiErrorRenderingTests\|FullyQualifiedName~Phase35.ReplDiagnosticTests"` | `flow-lang.Tests/Phase35/DiagnosticRendererGoldenTests.cs` + `flow-lang.Tests/baselines/Phase35/diagnostics/` | ⬜ pending |
| 35-04 (Test framework) | 2 | 3 | TEST-01, TEST-02 | unit + CLI integration | `dotnet test --filter "FullyQualifiedName~Phase35.TestFrameworkBuiltinsTests\|FullyQualifiedName~Phase35.HermeticIsolationTests\|FullyQualifiedName~Phase35.FlowTestCliTests\|FullyQualifiedName~Phase35.TestBodyDeferralTests\|FullyQualifiedName~Phase35.AssertWithinDbTests"` | `flow-lang.Tests/Phase35/TestFrameworkBuiltinsTests.cs`, `HermeticIsolationTests.cs` | ⬜ pending |
| 35-05 (Pattern matching foundation) | 3 | 4 | LANG-01 | unit + parser + runtime | `dotnet test --filter "FullyQualifiedName~Phase35.MatchLexerTests\|FullyQualifiedName~Phase35.MatchParserTests\|FullyQualifiedName~Phase35.PatternAstTests\|FullyQualifiedName~Phase35.MatchRuntimeTests"` + `dotnet run --project flow-interpreter tests/test_pattern_matching.flow` | `flow-lang.Tests/Phase35/MatchParserTests.cs`, `tests/test_pattern_matching.flow` | ⬜ pending |
| 35-06 (Music extractors + exhaustiveness) | 4 | 4 | LANG-02 | unit + integration | `dotnet test --filter "FullyQualifiedName~Phase35.MatchExhaustivenessDefaultTests\|FullyQualifiedName~Phase35.MatchExhaustivePragmaTests\|FullyQualifiedName~Phase35.PragmaScopeTests"` + `dotnet run --project flow-interpreter tests/test_pattern_match_music.flow` | `flow-lang.Tests/Phase35/MatchExhaustivenessDefaultTests.cs`, `tests/test_pattern_match_music.flow` | ⬜ pending |
| 35-07 (`-> as name`) | 5 | 4 | LANG-03 | unit + parser + integration | `dotnet test --filter "FullyQualifiedName~Phase35.AsBindingParserTests\|FullyQualifiedName~Phase35.AsKeywordReservationTests\|FullyQualifiedName~Phase35.AsBindingScopeTests\|FullyQualifiedName~Phase35.AsBindingTypeTests"` + `dotnet run --project flow-interpreter tests/test_chain_naming.flow` | `flow-lang.Tests/Phase35/AsBindingParserTests.cs`, `tests/test_chain_naming.flow` | ⬜ pending |

**Sampling continuity audit:** Each plan's Task 1 is a Wave 0 stub (creates failing tests); Tasks 2-N each carry an `<automated>` verify command. No plan has 3 consecutive tasks without automated verify. Full-suite regression (`dotnet test`) runs at the close of every plan.

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Phase35/LexerSpanTests.cs` — stubs for LANG-04 span migration
- [ ] `flow-lang.Tests/Phase35/AstSpanTests.cs` — stubs for AST Span field migration
- [ ] `flow-lang.Tests/Phase35/DiagnosticRendererGoldenTests.cs` — golden-file harness stub (LANG-04)
- [ ] `flow-lang.Tests/Phase35/TestFrameworkBuiltinsTests.cs` — stubs for TEST-01 builtins
- [ ] `flow-lang.Tests/Phase35/HermeticIsolationTests.cs` — stubs for TEST-02 isolation
- [ ] `flow-lang.Tests/Phase35/MatchParserTests.cs` — stubs for LANG-01 parser
- [ ] `flow-lang.Tests/Phase35/MatchExhaustivenessDefaultTests.cs` — stubs for LANG-02 default WARN behavior
- [ ] `flow-lang.Tests/Phase35/AsBindingParserTests.cs` — stubs for LANG-03 parser
- [ ] `flow-lang.Tests/Phase35/HumanizeGaussianVoiceBlocksTests.cs` — stubs for HK-01 regression
- [ ] `flow-lang.Tests/baselines/Phase35/diagnostics/` — empty directory for diagnostic golden baselines
- [ ] `tests/test_pattern_matching.flow`, `tests/test_chain_naming.flow`, `tests/test_humanize_voice_blocks.flow` — composer-facing acceptance stubs

xUnit is already installed (existing `flow-lang.Tests/` project); no framework install required. New Phase 35 test files extend the existing project.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Diagnostic rendering visually matches Rust's quality bar | LANG-04 | "Rust-style" is a perceptual / readability claim — automated golden files cover format correctness but not the experience | Run `dotnet run --project flow-interpreter tests/test_diagnostics_demo.flow` (a NEW demo file the planner adds) — eyeball the multi-line error output against a side-by-side `rustc` example included in the demo file's header comment |
| `flow test` console output (per-test pass/fail + summary) reads well in a terminal | TEST-01 | Output formatting / spacing / color is a UX call; automated tests cover correctness but not readability | Run `dotnet run --project flow-interpreter test tests/` — confirm the output is scannable: clear pass/fail mark per test, indented failure detail, summary line at bottom with totals |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies (per the Per-Task Verification Map above — every plan's Task 1 is a Wave 0 stub; subsequent tasks each carry an explicit `dotnet test --filter` or `dotnet run` command in their `<automated>` block)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify (confirmed in the sampling continuity audit note above)
- [x] Wave 0 covers all MISSING references (11 xUnit test file stubs + 3 `.flow` acceptance stubs + diagnostics baseline directory all enumerated in Wave 0 Requirements above; each plan's Task 1 creates the stubs for its bucket)
- [x] No watch-mode flags (all commands are single-shot `dotnet test` / `dotnet run` — no `--watch` / `dotnet watch` invocations)
- [x] Feedback latency < 90 s (quick filter `--filter "FullyQualifiedName~Phase35.<TestClass>"` < 30 s per-task; per-wave full `dotnet test` ~90 s)
- [x] `nyquist_compliant: true` set in frontmatter (flipped 2026-05-18 after Wave 0 stub list verified against plan task lists)

**Approval:** approved 2026-05-18 (Phase 35 plan-checker iteration)
