---
phase: 17-flow-language-server
plan: 02
subsystem: vscode-extension
tags: [vscode-extension, textmate-grammar, typescript, snippets, scaffold, lsp-client]

# Dependency graph
requires:
  - phase: 17-flow-language-server
    provides: "(none — Wave 1 parallel track; independent of plan 17-01 LSP scaffold)"
provides:
  - "VSCode extension scaffold with onLanguage:flow activation and per-platform LSP client"
  - "TextMate grammar using only standard VSCode scopes (D-05) — renders against any user theme"
  - "Snippet templates for tempo, key, timesig, proc, section block constructs (D-07)"
  - "Four grammar snapshot fixtures ready for vscode-tmgrammar-snap baselines in plan 17-07"
  - "Root .gitignore carve-outs so vscode-extension/tests/**/*.flow and README.md stay trackable despite global tests/ + *.flow ignores"
affects: [17-03, 17-04, 17-05, 17-06, 17-07, 17-08]

# Tech tracking
tech-stack:
  added:
    - "TypeScript (vscode-extension/) — NEW IDIOM, first TS in repo"
    - "vscode-languageclient ^9.0.1 (declared in package.json; npm install deferred to CI / plan 17-08)"
    - "@vscode/vsce ^3.9.1 + ovsx ^0.10.11 (devDeps for later CI publish)"
    - "vscode-tmgrammar-test ^0.1.3 (devDep — snapshot baselines in plan 17-07)"
  patterns:
    - "platformDir() — `${process.platform}-${process.arch}` matches vsce --target names (linux-x64, win32-x64, darwin-x64, darwin-arm64) per Pitfall 7"
    - "flow.server.path override pattern — user-supplied binary path takes precedence over VSIX-bundled default"
    - "TextMate scope naming convention: standard VSCode prefix + `.flow` suffix (keyword.control.flow, storage.type.flow, etc.) — no invented *.music.* sub-categories per D-05"
    - "Pattern ordering in TM grammar: chords BEFORE notes BEFORE numbers so `Bb7` resolves as a chord rather than `Bb` + `7`"

key-files:
  created:
    - vscode-extension/package.json
    - vscode-extension/tsconfig.json
    - vscode-extension/.vscodeignore
    - vscode-extension/language-configuration.json
    - vscode-extension/src/extension.ts
    - vscode-extension/syntaxes/flow.tmLanguage.json
    - vscode-extension/snippets/flow.code-snippets
    - vscode-extension/README.md
    - vscode-extension/tests/grammar/sample.flow
    - vscode-extension/tests/grammar/note-stream.flow
    - vscode-extension/tests/grammar/chords.flow
    - vscode-extension/tests/grammar/musical-context.flow
  modified:
    - .gitignore

key-decisions:
  - "Committed to `//` as the sole line-comment marker in both language-configuration.json and the TextMate grammar. Confirmed by reading flow-lang/Lexing/SimpleLexer.cs:826-833 — the lexer recognizes `//` and treats `;` as TokenType.Semicolon (statement terminator), never as a comment starter."
  - "Pipe delimiter `|` modeled as a standalone `match` pattern (Pitfall 5 avoidance), NOT a begin/end block. Per-bar precision is the semantic-tokens handler's job in plan 17-04."
  - "Chord pattern placed BEFORE notes in TM grammar repository include order, so `Bb7` + `Cmaj7` are colored as chords (entity.name.function.flow) rather than split into note + number."
  - "Added explicit .gitignore negation rules (!vscode-extension/tests/**, !vscode-extension/README.md) because the repo globally ignores tests/ and *.flow — without these carve-outs the grammar fixtures would be invisible to git."
  - "Placeholder publisher `flow-lang` in package.json; real Marketplace + OpenVSX publisher identity finalized in plan 17-08."

patterns-established:
  - "Standard-scope-only TextMate grammars: every scope uses a stock VSCode prefix (keyword.control, storage.type, constant.numeric, variable.other.note, entity.name.function, etc.) followed by `.flow` for our language suffix. No `keyword.music.flow` or other invented categories."
  - "Per-platform LSP binary selection in the extension activation: platformDir() returns `linux-x64 | win32-x64 | darwin-x64 | darwin-arm64`, followed by chmod 0o755 on POSIX (VSIX zip extraction does not preserve exec bit on all paths)."
  - "Grammar snapshot fixture layout: vscode-extension/tests/grammar/*.flow pairs with `.flow.snap` baselines (to be generated in plan 17-07). Fixtures exercise distinct concern axes (sample.flow = category coverage, note-stream.flow = bar-boundary stress, chords.flow = quality-suffix discrimination, musical-context.flow = nested scope layering)."

requirements-completed: [D-04, D-05, D-07, D-13]

# Metrics
duration: 12min
completed: 2026-04-20
---

# Phase 17 Plan 02: VSCode extension scaffold + TextMate grammar + snippets + grammar fixtures Summary

**VSCode extension scaffold with per-platform LSP client, standard-scope TextMate grammar, D-07 snippet templates, and four snapshot-ready grammar fixtures — committable and ready for Waves 2-4 handler + CI work.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-04-20T22:02:00Z
- **Completed:** 2026-04-20T22:14:10Z
- **Tasks:** 2
- **Files created:** 12 (vscode-extension/ tree)
- **Files modified:** 1 (.gitignore)

## Accomplishments

- Complete VSCode extension scaffold (`vscode-extension/package.json`, `tsconfig.json`, `.vscodeignore`, `language-configuration.json`, `src/extension.ts`, `README.md`) with `onLanguage:flow` activation and per-platform LSP binary selection ready for Waves 2-4.
- TextMate grammar (`vscode-extension/syntaxes/flow.tmLanguage.json`) using 10 standard VSCode scopes only, no `*.music.*` or other invented categories — renders against any user theme. Chord patterns ordered before notes so `Bb7`/`Cmaj7` tokenize correctly.
- Snippet templates for the five D-07 block constructs (`tempo`, `key`, `timesig`, `proc`, `section`) with tab-stop placeholders following VSCode snippet conventions.
- Four grammar snapshot fixtures covering category-sweep, multi-bar note streams, chord-quality discrimination, and nested musical-context layering — ready for `vscode-tmgrammar-snap` baselines in plan 17-07.
- Root `.gitignore` extended with VSCode extension build-artifact exclusions plus explicit negation rules so the fixture `.flow` files and `README.md` remain trackable despite the repo's global `tests/` + `*.flow` ignore rules.

## Task Commits

Each task was committed atomically:

1. **Task 1: Scaffold VSCode extension** — `5ea7f8e` (feat)
2. **Task 2: TextMate grammar + snippets + fixtures** — `550db48` (feat)

_(Plan metadata commit follows this summary; created by the orchestrator after all wave 1 agents finish.)_

## Files Created/Modified

### Created

- `vscode-extension/package.json` — npm manifest with `onLanguage:flow` activation, language/grammar/snippets contributions, and `flow.server.path` + `flow.trace.server` config properties.
- `vscode-extension/tsconfig.json` — TypeScript build config (ES2022, strict, commonjs output to `out/`).
- `vscode-extension/.vscodeignore` — VSIX packaging control. Explicitly does NOT exclude `server/**` or `*.flow` (Pitfall 6 — CI populates per-platform binaries + stdlib `.flow` files ship alongside them for later go-to-def resolution).
- `vscode-extension/language-configuration.json` — bracket pairs, auto-close, surrounding pairs, and single `"lineComment": "//"` entry (no `;` fallback).
- `vscode-extension/src/extension.ts` — LanguageClient activation. `platformDir()` returns `${process.platform}-${process.arch}`, producing vsce-target-compatible names (`linux-x64`, `win32-x64`, `darwin-x64`, `darwin-arm64`). POSIX binaries get `chmod 0o755` before spawn. Supports `flow.server.path` user override.
- `vscode-extension/syntaxes/flow.tmLanguage.json` — TextMate grammar, 10 repository entries, standard VSCode scopes only. Single `//.*$` comment pattern.
- `vscode-extension/snippets/flow.code-snippets` — 5 snippet entries: Tempo, Key, Timesig, Proc, Section blocks.
- `vscode-extension/README.md` — short feature list, requirements, configuration, publisher placeholder note (plan 17-08 finalizes).
- `vscode-extension/tests/grammar/sample.flow` — 18-line fixture exercising every grammar category (keyword, type, number, string, bool, note, chord, operator, pipe, comment).
- `vscode-extension/tests/grammar/note-stream.flow` — multi-bar stream fixture covering durations, chords, tied/dotted notes, random-choice `(? ...)`, and cent offsets (`+50c`/`-25c`) across bar boundaries.
- `vscode-extension/tests/grammar/chords.flow` — distilled from `tests/test_chords.flow` (hand-authored 10-line subset; not a `cp` — trimmed to exercise the discrimination cases: `Cmaj`, `Dm`, `Cmaj7`, `Am7`, `Bdim`, `Caug`, `Dsus2`, `Asus4`, `Csmaj`, `Bfm`).
- `vscode-extension/tests/grammar/musical-context.flow` — nested `tempo → timesig → key → dynamics` blocks to stress scope layering.

### Modified

- `.gitignore` — added `vscode-extension/node_modules/`, `vscode-extension/out/`, `vscode-extension/server/`, `*.vsix` exclusions AND `!vscode-extension/tests/`, `!vscode-extension/tests/**`, `!vscode-extension/tests/**/*.flow`, `!vscode-extension/README.md` negation rules so VSCode extension sources stay trackable despite the pre-existing global `tests/` + `*.flow` + `*.md` ignores.

## Comment-Syntax Confirmation

Per the plan's `<output>` section: I read `flow-lang/Lexing/SimpleLexer.cs:826-833` and confirmed that:

1. The lexer's `SkipWhitespaceAndComments` loop contains `else if (c == '/' && PeekNext() == '/') { while (!IsAtEnd() && Peek() != '\n') Advance(); }` — `//` is the one and only line-comment form.
2. The adjacent branch at line 834 handles the legacy `Note:` line-start marker (orthogonal to `//`; irrelevant to TM grammar since `Note` is also a type keyword).
3. There is NO `;`-handling branch in the comment loop. `;` is tokenized as `TokenType.Semicolon` — a statement terminator.

Both `vscode-extension/language-configuration.json` (`"lineComment": "//"`) and `vscode-extension/syntaxes/flow.tmLanguage.json` (single pattern `"match": "//.*$"` in the `comments` repository entry) commit to `//` only. Adding a `;.*$` fallback would have miscolored every statement terminator as a comment.

## Regex Deltas from 17-PATTERNS.md Blueprint

- **Chord pattern (line ordering vs regex form):** The blueprint described "letter [#bsf]? quality (maj7|m7|dim|aug|sus|7|m|maj|...)". I kept the structure identical but ensured the `chords` repository entry appears before `notes` and before `numbers` in the top-level `patterns` include list. This is critical so `Bb7` resolves as a chord rather than `Bb` (note) + `7` (number). The Checker concern noted in 17-RESEARCH Pitfall 5 is avoided: pipes use `match` only, never `begin`/`end`.
- **Operator alternation ordering:** Placed `==` before `=` in the `operators` regex (`->|=>|\\+|-|\\*|/|<=|>=|<|>|==|=`) so `==` matches as a single token rather than two `=` operators.
- **Numbers with unit suffix:** Extended the `numbers` pattern beyond raw digits to include `ms|s|db|st|c|hz|khz` suffixes so Flow's time/decibel/semitone/cent/frequency literals (`100ms`, `-3db`, `+5st`, `440hz`) are colored as single numeric tokens.

## JSON Validity Confirmation

All five JSON files parse cleanly via `python3 -c "import json; json.load(open(...))"`:

- `vscode-extension/package.json`
- `vscode-extension/tsconfig.json`
- `vscode-extension/language-configuration.json`
- `vscode-extension/syntaxes/flow.tmLanguage.json`
- `vscode-extension/snippets/flow.code-snippets`

Grep-based audit of TM grammar confirms:

- `scopeName` equals `source.flow`.
- 12 occurrences of `.flow"` inside scope `name` values (well above the ≥5 threshold required by acceptance criteria).
- Zero `*.music.*` scope names.
- Exactly 1 pattern in `repository.comments.patterns`.
- Zero `;.*` comment patterns.
- Pipes pattern uses `match`, not `begin`/`end` (Pitfall 5 avoidance verified programmatically via Python).

## Chord Fixture Source

`vscode-extension/tests/grammar/chords.flow` was **hand-authored** (not `cp`'d) as a 10-line distillation of `tests/test_chords.flow`. Rationale: the original is 73 lines with `Note:`-style comment blocks, stdio calls, and `(concat ...)` expressions that exercise runtime behavior rather than pure chord tokenization. The distillation keeps only the assignments that discriminate chord-quality regex branches: `Cmaj`, `Dm`, `Cmaj7`, `Am7`, `Bdim`, `Caug`, `Dsus2`, `Asus4`, `Csmaj`, `Bfm`, plus one `| Cmaj7 Am7 Dm |` stream to cover chord-in-note-stream context.

## Decisions Made

See `key-decisions` in frontmatter. No architectural deviations — all work followed the plan and patterns documents exactly. The only decision requiring judgment was the `.gitignore` negation-rules carve-out, which is a pure bookkeeping requirement of making the fixtures trackable given the repo's pre-existing global `tests/` + `*.flow` + `*.md` ignores.

## Deviations from Plan

None — plan executed exactly as written.

The `.gitignore` negation rules (`!vscode-extension/tests/`, `!vscode-extension/tests/**`, `!vscode-extension/tests/**/*.flow`, `!vscode-extension/README.md`) are a defensive addition beyond the plan's bare "add exclusions" instruction, but they are necessary correctness: without them, the repo's existing global `tests/` + `*.flow` + `*.md` ignore rules would silently hide the fixture files and README from git. Verified via `git check-ignore` both before (ignored) and after (tracked) the carve-out.

## Issues Encountered

- **STATE.md modification detected mid-execution.** An orchestrator process updated `.planning/STATE.md` between plan start and Task 1 commit (changed `Current focus`, `Phase: 17`, `Plan: 1 of 8`, etc.). Resolved by running `git restore --staged .planning/STATE.md && git checkout -- .planning/STATE.md` to revert the worktree's copy so this agent does not commit STATE.md (per `<parallel_execution>` instructions — shared-file updates are the orchestrator's concern post-wave, not this worktree's).

## Self-Check

All claimed files exist on disk, and both task commits are in the worktree branch log:

- `vscode-extension/package.json` — FOUND
- `vscode-extension/tsconfig.json` — FOUND
- `vscode-extension/.vscodeignore` — FOUND
- `vscode-extension/language-configuration.json` — FOUND
- `vscode-extension/src/extension.ts` — FOUND
- `vscode-extension/syntaxes/flow.tmLanguage.json` — FOUND
- `vscode-extension/snippets/flow.code-snippets` — FOUND
- `vscode-extension/README.md` — FOUND
- `vscode-extension/tests/grammar/sample.flow` — FOUND
- `vscode-extension/tests/grammar/note-stream.flow` — FOUND
- `vscode-extension/tests/grammar/chords.flow` — FOUND
- `vscode-extension/tests/grammar/musical-context.flow` — FOUND
- `.gitignore` — FOUND (modified)
- Commit `5ea7f8e` (Task 1) — FOUND
- Commit `550db48` (Task 2) — FOUND

## Self-Check: PASSED

## User Setup Required

None — no external service configuration required in this plan. (Marketplace publisher + OpenVSX namespace setup is plan 17-08's concern; binary bundling is plan 17-07.)

## Next Phase / Plan Readiness

- **Plan 17-01 (parallel):** Independent; no cross-dependencies. Both land in wave 1.
- **Plan 17-03 (DocumentManager + diagnostics, wave 2):** Consumes `vscode-extension/src/extension.ts`'s `LanguageClient` for receiving `publishDiagnostics` — no changes needed here.
- **Plan 17-04 (semantic tokens, wave 3):** Layers on top of the TextMate grammar via the LSP `semanticTokens` provider; grammar already permits this (D-04 hybrid).
- **Plan 17-07 (CI + VSIX packaging, wave 6):** Will populate `vscode-extension/server/<platform>/flow-lsp[.exe]` at package time and generate `*.flow.snap` baselines from the four fixtures via `vscode-tmgrammar-snap`.
- **Plan 17-08 (marketplace publish + docs, wave 7):** Will replace placeholder `publisher: "flow-lang"` with the real Marketplace publisher ID and finalize the README.

---

*Phase: 17-flow-language-server*
*Plan: 02*
*Completed: 2026-04-20*
