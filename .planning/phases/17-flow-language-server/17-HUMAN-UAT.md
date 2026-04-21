---
status: partial
phase: 17-flow-language-server
source: [17-VALIDATION.md §Manual-Only Verifications, 17-08-PLAN.md Task 3]
started: 2026-04-20
updated: 2026-04-20
---

## Current Test

[awaiting human testing in VSCode Extension Development Host]

## Tests

### 1. D-04/D-05 syntax highlighting matches flow-editor/ categories
expected: |
  Open `tests/test_chords.flow` (or any file from `tests/` / `examples/`) in the
  Extension Development Host (F5 from `vscode-extension/`). Each of the 11
  category tick-boxes below renders consistently with
  `flow-editor/Editor/FlowSyntaxHighlighter.cs`, with chords visually distinct
  from notes and no unstyled regions:
    - Keywords (`proc`, `use`, `section`, `tempo`, `key`, `timesig`, `swing`,
      `dynamics`, `return`) — single consistent "keyword" color
    - Type names (`Int`, `Float`, `String`, `Buffer`, `Note`, `Chord`,
      `Sequence`, `Song`) — "type" color distinct from keywords
    - Strings (`"literal"`, `"@audio"`) — "string" color
    - Numbers (`120`, `3.14`, `0.5`) — "numeric" color
    - Comments (`// comment`) — muted/gray
    - Notes (`C4`, `Db5q`, `F#3`) — "note" color
    - Chords (`Cmaj7`, `Dm`, `Bb7`, `Bdim`) — distinct from notes; NOT colored
      as notes or identifiers
    - Roman numerals inside `key C { | I IV V7 | }` — colored as chords
      (semantic-tokens override)
    - Note-stream delimiters (`|`) — "operator" or distinct color
    - Operators (`->`, `=`, `+`, `*`, `/`) — "operator" color
    - Booleans (`true`, `false`) — "constant" color
  Full checklist with reproduction steps:
  `docs/editor-setup/manual-smoke.md` §"row 1/5".
result: [pending]

### 2. D-04 TM→semantic token transition (0–300ms window)
expected: |
  Close and re-open a `.flow` file in the Extension Development Host. Watch the
  first 0–300 ms after the file loads. TextMate grammar paints immediately;
  semantic tokens may refine scopes once the LSP server responds (~100–300 ms).
  Any visible repaint should be "not noticeable" or "subtle / acceptable" — not
  jarring. Full reproduction: `docs/editor-setup/manual-smoke.md` §"row 2/5".
result: [pending]

### 3. D-13 extension activation + embedded feature sanity
expected: |
  Extension activates on `.flow` file open and surfaces a status indicator
  (status bar or `Flow LSP Trace` Output channel). While the session is active,
  confirm each embedded feature from `docs/editor-setup/manual-smoke.md`
  §"row 3/5":
    - D-06 diagnostics: typing `proc (` produces a red squiggle within ~200 ms;
      removing it clears the squiggle.
    - D-07 completion: `pri` → `print` in completion list with signature detail;
      `use "@` → exactly the 6 stdlib paths (`@std`, `@audio`, `@collections`,
      `@bars`, `@notation`, `@composition`) and NOT built-in or user symbols.
    - D-08 hover: hovering `print` shows markdown tooltip with signature + doc
      summary from `BuiltInDocs`.
    - D-09 go-to-definition: Ctrl+Click on `use "@audio"` jumps to `audio.flow`;
      clicking a user-declared proc name jumps to its declaration.
    - D-10 signature help: `transpose(seq, ` shows a signature tooltip with
      "active parameter 1" indication.
    - D-11 note-stream context-aware completion: inside `key Cmajor { | `
      Ctrl+Space offers roman numerals (`I`, `IV`, `V7`, …); inside a `| ... |`
      stream with no `key` context offers note letters / chord literals /
      durations / rests. Neither context surfaces user procs or top-level
      keywords.
    - Snippet expansion: `tempo` + Tab → `tempo ${1:120} { $0 }` with cursor at
      placeholder 1.
result: [pending]

## Summary

total: 3
passed: 0
issues: 0
pending: 3
skipped: 0
blocked: 0

## Gaps

<!-- Empty — populated if any test returns result: issue. -->

---

## Note on deferred items (rows 4–5 from 17-VALIDATION.md)

Two additional manual verifications from
`.planning/phases/17-flow-language-server/17-VALIDATION.md`
§Manual-Only Verifications are **NOT tracked in this UAT file**. They are
deferred to the first release tag milestone because they cannot execute until
the first VSIX artifacts exist on the marketplaces:

- **Row 4 — D-14 per-platform binary on non-dev OS.** Install the platform-
  appropriate VSIX on one non-Linux machine (macOS or Windows VM) and repeat
  rows 1–3 above. Blocked today — no VSIX has been published yet. To execute:
  after `git push origin v*` completes and the Marketplace / OpenVSX listings
  land, install from the stock VSCode Marketplace or OpenVSX on a non-Linux
  machine, then walk through this file's rows 1–3.

- **Row 5 — D-15 Marketplace + OpenVSX publish succeeds on tag push.** On the
  first `v*` tag, watch the `.github/workflows/publish-extension.yml` run
  complete (both `build-server` and `publish` jobs green), then verify both
  marketplace listings appear with all 4 per-platform VSIX entries. Tracked by
  `.planning/phases/17-flow-language-server/17-MARKETPLACE-SETUP.md` Step 4
  and its status-checklist table.

These two rows will be picked up at release time via a separate HUMAN-UAT
session scoped to the release tag (not to Phase 17 closure). Phase 17 closes
independently once rows 1–3 in this file resolve.

## Reproduction steps

Full step-by-step instructions (prerequisite build, F5 launch, per-row
checklists, reporting format) live at:

- `docs/editor-setup/manual-smoke.md` — source of truth for all 5 rows
- `.planning/phases/17-flow-language-server/17-VALIDATION.md` §Manual-Only
  Verifications — the validation contract rows originate from

Resume signal format for results 1–3: `smoke: clear` / `smoke: partial - <desc>` /
`smoke: blocked - <desc>`.
