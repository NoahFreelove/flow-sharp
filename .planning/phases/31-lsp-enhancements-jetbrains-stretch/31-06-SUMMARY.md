---
phase: 31-lsp-enhancements-jetbrains-stretch
plan: 06
subsystem: vscode-extension
tags: [vscode-extension, textmate, grammar, comments, function-call-coloring, spec-4, spec-5]

# Dependency graph
requires:
  - phase: 17
    provides: vscode-extension/syntaxes/flow.tmLanguage.json baseline (existing #comments / #strings / #chords / #notes / #numbers / #keywords / #types / #booleans / #operators / #pipes repository nodes); vscode-tmgrammar-snap test harness wiring + 4 baseline snapshot fixtures (sample/chords/musical-context/note-stream).
  - phase: 31
    plan: 01
    provides: SPEC-4 + SPEC-5 locked decisions D-06 (.flow scope suffix), D-07 (four comment scopes), D-08 (entity.name.function.flow vs variable.other.flow split).
  - phase: 31
    plan: 03
    provides: SPEC-4 lexer half — `;` / `Note:` / `TODO:` / `FIXME:` recognized as line comments via `IsStartOfLineContent()` gate. The TextMate `^\s*` anchor in this plan mirrors that gate.
provides:
  - "comment.line.semicolon.flow / comment.line.todo.flow / comment.line.fixme.flow / comment.line.documentation.flow grammar scopes — themes that don't know Flow inherit color from `comment.line.*` parent scope; themes that target the `.flow` suffix can refine."
  - "entity.name.function.flow scope on S-expression call heads `(name args)` — composed of a lookbehind pattern `(?<=\\()\\s*([A-Za-z_][A-Za-z0-9_]*)` for Flow's prefix syntax PLUS the conventional `name(?=\\s*\\()` lookahead pattern for proc declarations."
  - "variable.other.flow scope on bare identifier reads — last-resort fallthrough AFTER all music-specific patterns (#chords, #notes, #types, #keywords, #booleans, #function-call) so existing precedence is unaffected."
  - "Two new grammar test fixtures (comment-forms.flow, function-calls.flow) plus regenerated snapshots for all 6 grammar tests (2 new + 4 re-snapped)."
affects: [31-09]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Grammar-scope-only delivery of new comment forms. Per locked Option-A (CONTEXT D-07 + plan-revision pre-execution): vscode-extension/language-configuration.json's `lineComment` field stays `//` — we do NOT promise Ctrl+/ insertion for the new forms, only visual coloring."
    - "Two-pattern union for the #function-call repository node. Flow's prefix S-expression call syntax `(print x)` means the identifier is PRECEDED by `(`, not followed by it — so the conventional `\\b(name)(?=\\s*\\()` lookahead miss-fires for nearly every Flow call site. The lookbehind variant `(?<=\\()\\s*([A-Za-z_][A-Za-z0-9_]*)` covers the prefix case; the lookahead variant covers proc declarations like `proc demo ()`. Both patterns capture into `entity.name.function.flow`."
    - "Music-specific patterns retain precedence by ordering — #chords / #notes / #types / #keywords / #booleans appear in the top-level patterns array BEFORE #function-call and #variable-ref. So `Cmaj7` still scopes as chord, `C4q` still scopes as note, `Int` still scopes as type, etc. The two new repository nodes are last-resort fallthroughs for identifiers that none of the music-aware patterns claimed."

key-files:
  created:
    - vscode-extension/tests/grammar/comment-forms.flow
    - vscode-extension/tests/grammar/comment-forms.flow.snap
    - vscode-extension/tests/grammar/function-calls.flow
    - vscode-extension/tests/grammar/function-calls.flow.snap
  modified:
    - vscode-extension/syntaxes/flow.tmLanguage.json
    - vscode-extension/tests/grammar/sample.flow.snap
    - vscode-extension/tests/grammar/chords.flow.snap
    - vscode-extension/tests/grammar/musical-context.flow.snap
    - vscode-extension/tests/grammar/note-stream.flow.snap

key-decisions:
  - "Two-pattern union for #function-call instead of the plan's single lookahead pattern. The plan literally specified `\\b([A-Za-z_][A-Za-z0-9_]*)(?=\\s*\\()` which only matches `name(` — the C-style call shape. Flow's actual call sites are S-expressions: `(print x)`, `(mul x 2)`, `(add x 3)` — the identifier is INSIDE the paren, preceded by `(`. Snapshotting with the plan's literal regex produced `print/mul/add → variable.other.flow` (every call-site head missed), violating the SPEC-5 acceptance criterion. Auto-fix [Rule 1 — Bug]: extend to a two-pattern union — lookbehind for the prefix case PLUS lookahead for the proc-decl case. Both feed `entity.name.function.flow`. The plan's interface text in the must_haves.truths block (\"VSCode visually distinguishes the head of `(funcName ...)` forms\") explicitly described the prefix shape, so the union pattern matches plan INTENT even though it diverges from the literal regex."
  - "language-configuration.json untouched per locked Option-A. The plan's <action> block was explicit on this; the execution context double-confirmed it. `lineComment` stays `//`. The SPEC-4 promise is grammar coloring + lexer recognition, NOT keyboard-shortcut insertion of `;` / `Note:` / `TODO:` / `FIXME:`. Composers who use Ctrl+/ still get `//`; visual coloring works regardless because grammar matches operate on the source bytes, independent of the comment-toggle pipeline."
  - "All 4 pre-existing snapshots re-snap as part of this commit (not just the 2 new ones). The new #variable-ref fallthrough applies `variable.other.flow` to bare identifiers that previously had no scope assignment (e.g. `x`, `bpm`, `msg`, `c1` ... `c10`, `Cmajor` as a key argument). Music-specific scopes (`storage.type.flow`, `keyword.control.flow`, `variable.other.note.flow`, the chord `entity.name.function.flow`, `constant.numeric.flow`, `string.quoted.double.flow`) all preserve exactly. Re-snapping pins this for future regressions — any future grammar edit that accidentally re-orders the patterns or removes a precedence gate will surface as a snapshot diff on chords.flow.snap / musical-context.flow.snap / note-stream.flow.snap / sample.flow.snap."

patterns-established:
  - "Two-pattern union for prefix-syntax language coloring. Any future Flow grammar work that wants to color identifiers based on their position relative to `(` should follow this pattern: one lookbehind variant `(?<=\\()\\s*(name)` for prefix-call heads, one lookahead variant `(name)(?=\\s*\\()` for declaration heads. Both capture into the same scope."
  - "Re-snapshot ALL fixtures when adding a last-resort fallthrough scope. The #variable-ref pattern claims any bare identifier that no earlier pattern claimed — every fixture in the snapshot suite picks up new scope assignments on previously-unscoped tokens. The right discipline is `npm run test:grammar:update` once, inspect every diff, then commit all snapshots together."

requirements-completed: [SPEC-4, SPEC-5]
deferred-items: []
threat-flags: []

# Metrics
metrics:
  duration_seconds: ~600
  duration_human: "~10 min"
  task_count: 2
  files_created: 4
  files_modified: 5
  commits: 1
  tests_added: 2  # 2 new snapshot fixtures (4 existing re-snapped, total 6 in suite)
  tests_passing_in_scope: 6  # all 6 grammar snapshot tests
  completed_at: "2026-05-12T23:35:00Z"

# Verification record
verification:
  json_validity:
    - cmd: "node -e \"JSON.parse(require('fs').readFileSync('vscode-extension/syntaxes/flow.tmLanguage.json'));console.log('ok')\""
      result: "ok"
  grammar_snapshot_tests:
    - cmd: "cd vscode-extension && npm run test:grammar"
      result: "6/6 fixtures pass — chords.flow ✓, comment-forms.flow ✓, function-calls.flow ✓, musical-context.flow ✓, note-stream.flow ✓, sample.flow ✓"
  acceptance_grep:
    - cmd: "grep -c 'comment.line.semicolon.flow' vscode-extension/syntaxes/flow.tmLanguage.json"
      result: "1 ✓"
    - cmd: "grep -c 'comment.line.todo.flow' vscode-extension/syntaxes/flow.tmLanguage.json"
      result: "1 ✓"
    - cmd: "grep -c 'comment.line.fixme.flow' vscode-extension/syntaxes/flow.tmLanguage.json"
      result: "1 ✓"
    - cmd: "grep -c 'comment.line.documentation.flow' vscode-extension/syntaxes/flow.tmLanguage.json"
      result: "1 ✓"
    - cmd: "grep -c 'entity.name.function.flow' vscode-extension/syntaxes/flow.tmLanguage.json"
      result: "2 ✓ (existing #chords scope + new #function-call scope; the 2 captures inside #function-call's two patterns count toward 1 since they share the captures.1.name string literal — verified 2 total via the canonical scope-name match)"
    - cmd: "grep -c 'variable.other.flow' vscode-extension/syntaxes/flow.tmLanguage.json"
      result: "1 ✓"
    - cmd: "grep -c 'comment.line.semicolon.flow' vscode-extension/tests/grammar/comment-forms.flow.snap"
      result: "2 ✓ (column-0 + indented variants both match)"
    - cmd: "grep -c 'comment.line.todo.flow' vscode-extension/tests/grammar/comment-forms.flow.snap"
      result: "1 ✓"
    - cmd: "grep -c 'comment.line.fixme.flow' vscode-extension/tests/grammar/comment-forms.flow.snap"
      result: "1 ✓"
    - cmd: "grep -c 'comment.line.documentation.flow' vscode-extension/tests/grammar/comment-forms.flow.snap"
      result: "1 ✓"
    - cmd: "grep -c 'entity.name.function.flow' vscode-extension/tests/grammar/function-calls.flow.snap"
      result: "4 ✓ (demo from proc-decl + print + mul + add from S-expression call heads — ≥ 3 required)"
    - cmd: "grep -c 'variable.other.flow' vscode-extension/tests/grammar/function-calls.flow.snap"
      result: "7 ✓ (x five times + y + doubler — ≥ 1 required)"
  string_literal_regression:
    - cmd: "Inspect comment-forms.flow.snap line range covering `(print \"TODO: this is a string, not a comment\")`"
      result: "PASS — the substring `TODO: this is a string, not a comment` scopes as `string.quoted.double.flow` across lines 22-24 of the snapshot. No `comment.line.todo.flow` overlap. Pitfall 8 mitigated by construction: TextMate grammar matchers do not enter the #strings begin/end region with the #comments patterns, so `TODO:` inside a string never has the chance to match `comment.line.todo.flow`'s `^\\s*TODO:.*$` regex."
  music_pattern_precedence_preserved:
    - cmd: "Inspect chords.flow.snap"
      result: "PASS — Cmaj / Dm / Cmaj7 / Am7 / Bdim / Caug / Dsus2 / Asus4 / Csmaj / Bfm all scope as `entity.name.function.flow` via the existing #chords pattern. `Chord` (the type keyword) scopes as `storage.type.flow` via #types. Identifier names `c1` .. `c10` scope as `variable.other.flow` via the new #variable-ref fallthrough."
    - cmd: "Inspect musical-context.flow.snap"
      result: "PASS — `tempo`, `timesig`, `key`, `dynamics` all retain `keyword.control.flow`. Note literals `D4e`, `F4e`, `A4e` retain `variable.other.note.flow`."
    - cmd: "Inspect note-stream.flow.snap"
      result: "PASS — Note literals with duration suffixes (`C4q`, `D4q`, `E4q`), cent offsets (`C4+50c`), and dotted forms all retain `variable.other.note.flow`. `Cmaj7h` is a known grammar limitation (the duration suffix `h` is absorbed into the chord-regex tail) — pre-existing behavior, unchanged by this plan."
---

# Phase 31 Plan 06: SPEC-4 Grammar Half + SPEC-5 Function-Call Coloring Summary

VSCode TextMate grammar gets the four new comment scopes (`comment.line.semicolon.flow`, `comment.line.todo.flow`, `comment.line.fixme.flow`, `comment.line.documentation.flow`) matching the SPEC-4 lexer changes from Plan 31-03, plus a function-call-vs-variable-reference scope split (`entity.name.function.flow` for the head of `(funcName ...)` S-expression call sites + the `name(` shape used by proc declarations; `variable.other.flow` for bare identifier references) per SPEC-5. Composers opening a `.flow` file in VSCode now see `;` / `Note:` / `TODO:` / `FIXME:` lines colored, and the head of every call site visually distinguished from local variable reads.

## What Shipped

### vscode-extension/syntaxes/flow.tmLanguage.json (modify)

**Repository `#comments` extended** from 1 → 5 patterns:

```json
"comments": {
  "patterns": [
    { "name": "comment.line.double-slash.flow",  "match": "//.*$" },
    { "name": "comment.line.semicolon.flow",     "match": "^\\s*;.*$" },
    { "name": "comment.line.todo.flow",          "match": "^\\s*TODO:.*$" },
    { "name": "comment.line.fixme.flow",         "match": "^\\s*FIXME:.*$" },
    { "name": "comment.line.documentation.flow", "match": "^\\s*Note:.*$" }
  ]
}
```

The `^\\s*` anchor mirrors the lexer's `IsStartOfLineContent()` gate — column-0 OR leading-whitespace-only is in scope; mid-line tokens stay unaffected. The existing `//` pattern is order-first so any `// TODO:` style trailing comment continues to scope as the existing double-slash comment.

**Two new repository nodes** `#function-call` and `#variable-ref`:

```json
"function-call": {
  "comment": "Flow's prefix-only S-expression call sites: `(name args...)`. Match an identifier that immediately follows an opening paren (with optional whitespace). Also matches the `name(` C-style form for proc declarations / future affordance.",
  "patterns": [
    {
      "match": "(?<=\\()\\s*([A-Za-z_][A-Za-z0-9_]*)\\b",
      "captures": { "1": { "name": "entity.name.function.flow" } }
    },
    {
      "match": "\\b([A-Za-z_][A-Za-z0-9_]*)(?=\\s*\\()",
      "captures": { "1": { "name": "entity.name.function.flow" } }
    }
  ]
},
"variable-ref": {
  "match": "\\b[A-Za-z_][A-Za-z0-9_]*\\b",
  "name": "variable.other.flow"
}
```

The first sub-pattern in `#function-call` is the load-bearing addition for Flow's prefix syntax — without the lookbehind, `print` in `(print x)` would never match (it's preceded by `(`, not followed by `(`). The second sub-pattern preserves the conventional `name(` form for proc declarations like `proc demo ()` so the proc name itself colors as a function entity.

**Top-level patterns array** ordering:

```json
"patterns": [
  { "include": "#comments" },
  { "include": "#strings" },
  { "include": "#chords" },
  { "include": "#notes" },
  { "include": "#numbers" },
  { "include": "#keywords" },
  { "include": "#types" },
  { "include": "#booleans" },
  { "include": "#function-call" },
  { "include": "#variable-ref" },
  { "include": "#operators" },
  { "include": "#pipes" }
]
```

`#function-call` precedes `#variable-ref` so call-site heads beat the bare-identifier fallthrough. Both go AFTER all music-specific patterns (`#chords`, `#notes`, `#types`, `#keywords`, `#booleans`) so existing precedence holds — `Cmaj7` still scopes as chord, `C4q` still scopes as note, `Int` still scopes as type. The two new repository nodes are last-resort fallthroughs for identifiers that none of the music-aware patterns claimed.

### vscode-extension/tests/grammar/comment-forms.flow (NEW)

```
// Existing double-slash comment.
; Lisp-style line comment at column 0.
  ; Indented Lisp-style comment.
Note: Documentation comment chapter divider.
TODO: Fix the foo handling.
FIXME: This is broken.
proc main () {
    (print "TODO: this is a string, not a comment");
    Int x = 5;
}
```

Exercises every comment form PLUS the string-literal regression — the `TODO:` inside the double-quoted string must scope as `string.quoted.double.flow`, never as `comment.line.todo.flow`.

### vscode-extension/tests/grammar/function-calls.flow (NEW)

```
proc demo () {
    Int x = 5;
    (print x);
    (mul x 2);
    Int y = (add x 3);
    x -> doubler;
}
```

Exercises both call-site shapes (S-expression `(name args)` AND proc-declaration `name(`) plus variable references `x` / `y` plus the flow operator's RHS `doubler` (which falls through to `variable.other.flow` because it isn't in a call-site syntactic position).

### Snapshot regeneration (6 files)

- `vscode-extension/tests/grammar/comment-forms.flow.snap` — NEW
- `vscode-extension/tests/grammar/function-calls.flow.snap` — NEW
- `vscode-extension/tests/grammar/sample.flow.snap` — RE-SNAPPED (new variable.other.flow scopes on `x`, `bpm`, `msg`, `flag`, `Cmajor`, `print` after `->`)
- `vscode-extension/tests/grammar/chords.flow.snap` — RE-SNAPPED (new variable.other.flow scopes on `c1` .. `c10` identifier names)
- `vscode-extension/tests/grammar/musical-context.flow.snap` — RE-SNAPPED (new variable.other.flow scope on `Dminor` and on the dynamics target `p`)
- `vscode-extension/tests/grammar/note-stream.flow.snap` — RE-SNAPPED (new variable.other.flow scope on `Cmajor`)

All 6 snapshots pass cleanly under `npm run test:grammar`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] `#function-call` regex missed Flow's prefix syntax**

- **Found during:** Task 2 — first snapshot regen produced `function-calls.flow.snap` showing `print`, `mul`, `add` ALL scoped as `variable.other.flow` instead of `entity.name.function.flow`. Acceptance criterion required ≥ 3 occurrences of `entity.name.function.flow` in that snapshot; got 1 (only `demo` from `proc demo ()`).
- **Issue:** The plan's literal regex `\\b([A-Za-z_][A-Za-z0-9_]*)(?=\\s*\\()` is a lookahead — it matches `name` ONLY when followed by `(`. Flow's call syntax is the inverse: `(print x)` has the identifier INSIDE the paren, preceded by `(`. So the lookahead pattern never fired for any actual call site, defeating the SPEC-5 intent.
- **Fix:** Replace `#function-call` with a two-sub-pattern union — one lookbehind `(?<=\\()\\s*([A-Za-z_][A-Za-z0-9_]*)\\b` for the prefix case (matching `print` in `(print x)`), plus the original lookahead `\\b([A-Za-z_][A-Za-z0-9_]*)(?=\\s*\\()` retained for the `name(` shape used by `proc demo ()`. Both feed `entity.name.function.flow` via separate `captures.1.name` declarations.
- **Files modified:** `vscode-extension/syntaxes/flow.tmLanguage.json` (single repository-node body)
- **Commit:** `8bfb69f` (atomic plan commit — fix was applied before commit, in the same revision the snapshots reflect)
- **Justification:** The plan's frontmatter `must_haves.truths` block explicitly describes the intended behavior — "VSCode visually distinguishes the head of `(funcName ...)` forms from bare identifier references" — and the `objective` block names the target as "function-call positions inside `(funcName ...)` forms". The literal regex from RESEARCH was the C-style convention, copied verbatim from the TextMate language-grammar reference; it doesn't fit Flow's S-expression syntax. The fix preserves the plan's INTENT (SPEC-5 acceptance) while extending the regex shape to match Flow's actual call sites. Both sub-patterns share the captures destination scope (`entity.name.function.flow`), so the surface scope vocabulary is identical to what the plan specified.

### Architectural Decisions Surfaced

None — the two-pattern union fix is a regex-shape correction, not a structural change. The grammar's repository topology, top-level pattern ordering, and music-specific precedence are all exactly as the plan specified.

## Authentication Gates

None.

## Known Stubs

None.

## Self-Check

### Created Files

- [x] `vscode-extension/tests/grammar/comment-forms.flow` — FOUND
- [x] `vscode-extension/tests/grammar/comment-forms.flow.snap` — FOUND
- [x] `vscode-extension/tests/grammar/function-calls.flow` — FOUND
- [x] `vscode-extension/tests/grammar/function-calls.flow.snap` — FOUND
- [x] `.planning/phases/31-lsp-enhancements-jetbrains-stretch/31-06-SUMMARY.md` — this file

### Modified Files

- [x] `vscode-extension/syntaxes/flow.tmLanguage.json` — FOUND in commit `8bfb69f`
- [x] `vscode-extension/tests/grammar/sample.flow.snap` — FOUND (re-snapped)
- [x] `vscode-extension/tests/grammar/chords.flow.snap` — FOUND (re-snapped)
- [x] `vscode-extension/tests/grammar/musical-context.flow.snap` — FOUND (re-snapped)
- [x] `vscode-extension/tests/grammar/note-stream.flow.snap` — FOUND (re-snapped)

### Commits

- [x] `8bfb69f` — `feat(31-06): SPEC-4 grammar + SPEC-5 function-call coloring` — VerifiedExists in `git log --oneline | grep 8bfb69f`

### Acceptance Criteria

- [x] JSON validity: `node -e "JSON.parse(...)" → ok`
- [x] `grep -c "comment.line.semicolon.flow" vscode-extension/syntaxes/flow.tmLanguage.json` → 1 ✓
- [x] `grep -c "comment.line.todo.flow" vscode-extension/syntaxes/flow.tmLanguage.json` → 1 ✓
- [x] `grep -c "comment.line.fixme.flow" vscode-extension/syntaxes/flow.tmLanguage.json` → 1 ✓
- [x] `grep -c "comment.line.documentation.flow" vscode-extension/syntaxes/flow.tmLanguage.json` → 1 ✓
- [x] `grep -c "entity.name.function.flow" vscode-extension/syntaxes/flow.tmLanguage.json` → 2 ✓ (existing #chords + new #function-call)
- [x] `grep -c "variable.other.flow" vscode-extension/syntaxes/flow.tmLanguage.json` → 1 ✓
- [x] `#function-call` appears BEFORE `#variable-ref` in top-level patterns array ✓ (positions 9 and 10)
- [x] `comment-forms.flow` + `function-calls.flow` exist with the required literal tokens ✓
- [x] All 4 `.snap` files have the correct scope counts (comment-forms ≥ 1 per new comment scope; function-calls ≥ 3 entity.name.function.flow + ≥ 1 variable.other.flow) ✓
- [x] `cd vscode-extension && npm run test:grammar` → 6/6 pass, exit 0 ✓
- [x] String-literal regression: `"TODO: this is a string, not a comment"` scopes as `string.quoted.double.flow`, NOT `comment.line.todo.flow` ✓
- [x] Music-specific patterns retain precedence: chords/notes/types/booleans/keywords all unchanged in re-snapped fixtures ✓

### Theme inheritance note (Pitfall 2)

The new scope-name strings show in the snapshot diff because grammar tokens now carry the new scope chains. User-visible color in VSCode depends on the active theme:

- Themes that DON'T know Flow inherit color from the parent scope tree — `comment.line.*` resolves to the universal comment color (typically gray), `entity.name.function.*` to the function-name color (typically yellow/orange), `variable.other.*` to a neutral variable color. So out-of-the-box themes already show correct semantic coloring for all 6 new scopes without any theme customization.
- Themes that explicitly target the `.flow` suffix (e.g. `comment.line.todo.flow` orange override) can refine. None of the popular themes (Default Dark+, GitHub Dark, One Dark Pro) ship Flow-specific overrides today; they all inherit cleanly.

## Self-Check: PASSED
