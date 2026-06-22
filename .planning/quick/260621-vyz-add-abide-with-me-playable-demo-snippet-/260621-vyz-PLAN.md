---
phase: quick-260621-vyz
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - flow-site/src/lib/playground/snippets.ts
autonomous: true
requirements: []
must_haves:
  truths:
    - "The playground snippet list shows an 'Abide With Me (hymn)' entry."
    - "Selecting it loads the 5-voice Flow hymn source into the editor; Run renders + plays it (synthesised piano on Web)."
    - "The sine-440 tone remains the default first-mount snippet (DEFAULT_SNIPPET_ID unchanged)."
    - "snippets.ts still type-checks — the new object matches the Snippet interface and the source string is validly escaped."
  artifacts:
    - path: "flow-site/src/lib/playground/snippets.ts"
      provides: "SNIPPETS array with a new abide-with-me entry appended"
      contains: "id: 'abide-with-me'"
  key_links:
    - from: "flow-site/src/lib/playground/snippets.ts"
      to: "flow-site playground UI"
      via: "SNIPPETS array consumed by the snippet rail; snippetById() lookup"
      pattern: "abide-with-me"
---

<objective>
Add "Abide With Me" as a new playable preset snippet to the flowlang.dev playground, so visitors can load it and press Run to hear the desktop-verified 5-voice hymn arrangement (rendered as synthesised piano on the Web target).

Purpose: Showcase a real, multi-voice composition in the in-browser playground — a faithful conversion of `abide_with_me.mid` (via `flow midi2flow`) the composer has confirmed "sounds quite good."
Output: One new `Snippet` object appended to the `SNIPPETS` array in `flow-site/src/lib/playground/snippets.ts`.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
</execution_context>

<context>
@.planning/STATE.md
@./CLAUDE.md

# The file to modify — note flow-site/ uses TS/Svelte conventions, NOT the repo-root C# rules.
@flow-site/src/lib/playground/snippets.ts

# The desktop-verified, web-safe Flow source to embed (read it verbatim — do not paraphrase the notes).
@/home/noah/Downloads/midi/abide_with_me_play.flow
</context>

<tasks>

<task type="auto">
  <name>Task 1: Append the Abide With Me snippet to the SNIPPETS array</name>
  <files>flow-site/src/lib/playground/snippets.ts</files>
  <action>
Append exactly ONE new `Snippet` object to the END of the existing `SNIPPETS` array literal (after the `print-arith` entry, inside the closing `]`). Add a comma after the current last entry. Do NOT touch `DEFAULT_SNIPPET_ID`, `BLANK_SOURCE`, the `Snippet` interface, the `sine-440` entry, or `snippetById()` — the sine tone stays the default first-mount snippet.

The new object's fields:
- `id: 'abide-with-me'`
- `label: 'Abide With Me (hymn)'`
- `blurb: 'A faithful 5-voice hymn arrangement, converted from MIDI and rendered as piano.'`
- `source`: the Flow code from `/home/noah/Downloads/midi/abide_with_me_play.flow`, authored as a TS string.

SOURCE-STRING AUTHORING RULES (match the existing multi-line snippets in this file — see `note-stream` / `song-section`):
- Author `source` as `'\n'`-terminated single-quoted string literal concatenation (one `+`-joined line per physical Flow line), NOT a template literal. Match the surrounding code style.
- Preserve the Flow source EXACTLY — every note token, every duration suffix (`q`/`h`/`w`/`e`/dotted `.`), every velocity `mf`, every rest `_`, every flat-accidental `-` on the pitch tokens, and the nesting/indentation of the `tempo`/`timesig`/`key`/`section` blocks. Transcribe verbatim from the .flow file; do not "clean up" or re-wrap the long Sequence lines.
- Each line ends with `\n` inside the string. The two leading comment lines of the .flow file (`// Abide With Me …` / `// Playable demo …`) MAY be included for context or omitted — composer's choice; if included, keep them verbatim. Prefer omitting them to keep the editor focused on runnable code.
- Single-quote the string literals; there are no single-quote or backslash characters in the Flow source, so no escaping is required beyond the `\n` line terminators. Double-quotes inside the source (e.g. `use "@std"`, `"piano"`, the section is unquoted) sit fine inside single-quoted TS strings — leave them as plain `"`.

Web-safety is a static property already satisfied by this source: it uses only `use "@std"` + `use "@audio"`, musical-context blocks (tempo/timesig/key), a `section`, `Sequence` note streams, `renderSong s "piano"`, and `(play mix)`. It references NONE of the Web-stripped surfaces (no @sfz/@osc/@midi/@jack, no `micBuffer`, no `live {}` blocks). Do not add, remove, or "modernise" any line — the desktop playability is already verified and the sampler→synthesis fallback on Web is documented Phase 47/48 behavior.
  </action>
  <verify>
    <automated>cd flow-site && node -e "const s=require('fs').readFileSync('src/lib/playground/snippets.ts','utf8'); const ids=[...s.matchAll(/id:\s*'([^']+)'/g)].map(m=>m[1]); if(!ids.includes('abide-with-me')) throw new Error('abide-with-me id missing'); if(!/DEFAULT_SNIPPET_ID\s*=\s*'sine-440'/.test(s)) throw new Error('default snippet id changed'); if(!s.includes('renderSong')) throw new Error('renderSong source line missing'); if(!s.includes('Abide With Me (hymn)')) throw new Error('label missing'); console.log('OK: abide-with-me present, default unchanged, source embedded ('+ids.length+' snippets total)');"</automated>
  </verify>
  <done>The SNIPPETS array contains a well-formed `abide-with-me` entry with the verbatim Flow source; `DEFAULT_SNIPPET_ID` is still `'sine-440'`; the file is otherwise unchanged.</done>
</task>

<task type="auto">
  <name>Task 2: Confirm the file type-checks (with manual-review fallback)</name>
  <files>flow-site/src/lib/playground/snippets.ts</files>
  <action>
Confirm the edited file is valid TypeScript and the new object matches the `Snippet` interface (id/label/blurb/source all present, all strings).

Run `pnpm -C flow-site exec tsc --noEmit` (the project's type-check). If pnpm reports missing dependencies, run `pnpm -C flow-site install` first, then retry. If the wider `tsc --noEmit` surfaces PRE-EXISTING errors unrelated to this one-object edit elsewhere in flow-site, that does not block this task — scope the judgment to: does `snippets.ts` itself compile and does the new entry satisfy `interface Snippet`? Optionally narrow with vitest: `pnpm -C flow-site test` (the suite has no SNIPPETS-length/count assertion, so it must still pass).

FALLBACK if the toolchain is unavailable in this environment (no network for `pnpm install`, or pnpm/tsc not runnable): do a careful manual review instead and note in the SUMMARY that automated type-check was skipped for that reason. Manual review checklist: (1) the new object has exactly the four `Snippet` keys; (2) the `source` value is a syntactically valid TS string expression (balanced quotes, `+` concatenation well-formed, no stray unescaped quote/backslash); (3) a trailing comma separates it from the prior entry and the array's closing `]` is intact; (4) `DEFAULT_SNIPPET_ID`, `BLANK_SOURCE`, the interface, and `snippetById()` are byte-unchanged.
  </action>
  <verify>
    <automated>cd flow-site && pnpm exec tsc --noEmit 2>&1 | { grep "snippets.ts" && { echo "FAIL: tsc error in snippets.ts"; exit 1; } || echo "OK: no tsc errors in snippets.ts"; }</automated>
  </verify>
  <done>`tsc --noEmit` reports no errors in `snippets.ts` (or, if the toolchain is unavailable, a documented manual review confirms the four-key `Snippet` shape, valid string escaping, intact trailing comma + closing `]`, and unchanged exports). The SUMMARY records which path was taken.</done>
</task>

</tasks>

<verification>
- `snippets.ts` contains an `abide-with-me` entry with `label: 'Abide With Me (hymn)'` and the verbatim hymn `source`.
- `DEFAULT_SNIPPET_ID` is still `'sine-440'`; the `Snippet` interface, `BLANK_SOURCE`, and `snippetById()` are unchanged.
- `pnpm -C flow-site exec tsc --noEmit` reports no errors in `snippets.ts` (or a manual-review fallback is documented in the SUMMARY).
- No test references the SNIPPETS array length/count (verified at plan time), so no test needs updating.
</verification>

<success_criteria>
A new `abide-with-me` snippet is selectable in the playground rail, loads the 5-voice hymn into the editor, and is web-safe (no stripped modules referenced). The default starter snippet and all existing snippets are unchanged. The file type-checks.

Out of scope (do NOT do): building the WASM runtime, running `dotnet publish`, regenerating `flow-site/static/wasm/`, or deploying the site. Web audibility is covered by the site's existing human-UAT posture; the source's web-safety is a static property already satisfied.
</success_criteria>

<output>
Create `.planning/quick/260621-vyz-add-abide-with-me-playable-demo-snippet-/260621-vyz-SUMMARY.md` when done.
</output>