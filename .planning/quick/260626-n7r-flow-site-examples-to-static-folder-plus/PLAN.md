---
quick_id: 260626-n7r
slug: flow-site-examples-to-static-folder-plus
status: planned
---

# Quick Task: flow-site — move playground examples to static/, add ragtime snippet

This is GREENFIELD WEB code (`flow-site/`): SvelteKit 2 / Svelte 5 runes / TS /
Tailwind v4 / pnpm / Vite / ESM. The repo-root C# conventions DO NOT apply here.
All work is under `flow-site/`.

## Goal

1. Move the playground's hardcoded example sources out of
   `src/lib/playground/snippets.ts` into `static/examples/` (SvelteKit's
   `/public` equivalent), loaded dynamically via a manifest — per the
   composer's request to "move examples to a /public folder and load them in
   from there instead of hardcoding."
2. Add a new "Ragtime" example (24-bar excerpt converted from a MIDI file).

Behavior of the playground must be preserved exactly (share `#code=` precedence,
OAuth-stash precedence, WR-02 reset-on-load, default snippet on first mount,
left-rail list + active highlight, prerender=false). Existing tests stay green.

## Background (current structure — verified)

- `src/lib/playground/snippets.ts` exports: `Snippet` interface `{id,label,blurb,source}`,
  `DEFAULT_SNIPPET_ID='sine-440'`, `BLANK_SOURCE=''`, `SNIPPETS: Snippet[]` (7
  snippets: `sine-440`, `print-hello`, `note-stream`, `chord-progression`,
  `song-section`, `print-arith`, `abide-with-me`), `snippetById(id)`.
- Consumers: `src/lib/playground/state.svelte.ts` imports `DEFAULT_SNIPPET_ID, snippetById`;
  `editorValue = $state(snippetById(DEFAULT_SNIPPET_ID).source)` (SYNC init) and
  `loadSnippet(id)` (SYNC). `src/routes/playground/+page.svelte` imports `SNIPPETS`
  and iterates `{#each SNIPPETS as snip (snip.id)}` for the rail; in `onMount` the
  initial editor value is `pendingGistSource ?? arrival.source ?? pg.editorValue`
  (so the DEFAULT source currently flows through the sync-initialized `pg.editorValue`).
- The HOME hero snippets live separately in `src/routes/+page.svelte` and are
  guarded by `src/routes/home-deeplinks.test.ts` — DO NOT touch home; this refactor
  is playground-only and must leave that test green.

## Tasks

### 1. Create `static/examples/` content
- `static/examples/<id>.flow` for ALL 7 existing snippets — extract each `source`
  from `snippets.ts` VERBATIM (byte-for-byte; preserve trailing newline).
- `static/examples/ragtime.flow` — copy verbatim from the staged file
  `.planning/quick/260626-n7r-flow-site-examples-to-static-folder-plus/ragtime.flow`
  (a 24-bar, six-voice excerpt; web-safe; ends in `(play (renderSong s "piano" 0.4s))`).
- `static/examples/manifest.json` — ordered array of
  `{ "id", "label", "blurb", "file" }` for the 7 existing snippets (same order,
  same labels/blurbs as snippets.ts) PLUS a ragtime entry appended:
  `{ "id":"ragtime", "label":"Ragtime (24 bars)", "blurb":"A six-voice ragtime excerpt converted from MIDI — stride bass + syncopated melody.", "file":"ragtime.flow" }`.

### 2. Refactor the loader (`src/lib/playground/snippets.ts`)
- Keep exports `DEFAULT_SNIPPET_ID='sine-440'` and `BLANK_SOURCE=''`.
- Keep a metadata type, e.g. `SnippetMeta { id; label; blurb; file }` (and/or keep
  `Snippet` for back-comat). Add async loaders:
  - `loadManifest(fetchFn=fetch): Promise<SnippetMeta[]>` → `fetch('/examples/manifest.json')`.
  - `loadSnippetSource(file, fetchFn=fetch): Promise<string>` → `fetch('/examples/'+file)`.
  Make `fetch` injectable (default global) so a vitest can pass a stub.
- Remove the hardcoded `SNIPPETS` array and the sync `snippetById`. (If keeping a
  helper is easier for callers, provide an async equivalent.)

### 3. Update `state.svelte.ts`
- `editorValue` must no longer sync-read a snippet source. Init it to `BLANK_SOURCE`
  (`''`); the page sets the real initial value after fetching (Task 4). Keep
  `activeSnippetId = $state<string>(DEFAULT_SNIPPET_ID)`.
- `loadSnippet` becomes async: `async loadSnippet(id: string, source: string)` OR
  `async loadSnippet(meta: SnippetMeta)` — fetch the source (or accept it from the
  caller) then set `editorValue`/`activeSnippetId` and do the SAME WR-02 reset
  (clear stdout/stderr/errors/midi, runStatus='idle', lastRunAt/lastDurationMs=null,
  clearSettleTimer). Keep `newBlank()` unchanged.

### 4. Update `src/routes/playground/+page.svelte`
- Replace the static `import { SNIPPETS }` with a `$state` list, e.g.
  `let snippets = $state<SnippetMeta[]>([])`, populated in `onMount` via
  `await loadManifest()` (handle fetch failure charitably — empty list + console.warn,
  never crash the page).
- In the existing `onMount` async IIFE, BEFORE computing `initialValue`, fetch the
  DEFAULT snippet's source: find the manifest entry with `id===DEFAULT_SNIPPET_ID`,
  `const defaultSource = await loadSnippetSource(entry.file)`. Then keep the exact
  precedence: `initialValue = pendingGistSource ?? arrival.source ?? defaultSource`.
  (If the manifest/default fetch fails, fall back to `BLANK_SOURCE` so Monaco still
  mounts.)
- The rail `{#each SNIPPETS ...}` becomes `{#each snippets as snip (snip.id)}`
  (same markup/classes/aria).
- `onLoadSnippet(id)` → look up the meta, `await pg.loadSnippet(...)` (await the
  source fetch). Keep the active-highlight behavior.
- Preserve `export const prerender = false` and the dynamic-import discipline.

### 5. Tests
- Add `src/lib/playground/snippets.test.ts` (vitest): manifest parses; every entry
  has id/label/blurb/file; `DEFAULT_SNIPPET_ID` is present in the manifest; each
  `file` exists under `static/examples/` and is non-empty; the ragtime entry is
  present. (Read files via node fs against the repo `static/examples/` dir.)
- Keep `src/routes/home-deeplinks.test.ts` green (do NOT modify home).

## Constraints

- All sources web-safe (no `@sfz`/`@osc`/`micBuffer`/`live {}` — these snippets
  already are; the ragtime uses only `@std`/`@audio`/renderSong/play).
- CF Pages stays pure-static (fetching `/examples/*` is served from the built
  `static/` output — confirm files land in the build output dir).
- No new dependencies.

## Verification

Run from repo root:
- `pnpm -C flow-site check` (svelte-check + sync) → clean.
- `pnpm -C flow-site test` (vitest) → all pass, including the new snippets.test.ts
  and the untouched home-deeplinks.test.ts.
- `pnpm -C flow-site build` → succeeds; confirm `static/examples/manifest.json` and
  the `.flow` files are present in `flow-site/.svelte-kit/cloudflare/` (or the
  served static output).
- Manual reasoning check (state in SUMMARY): default snippet still loads on first
  mount; selecting a snippet fetches+loads its source with the WR-02 reset; the
  `#code=` and OAuth-stash precedence is unchanged.
- NOTE: full playwright `test:e2e` (playground-mobile.spec.ts, wasm-boot.spec.ts)
  is heavy (needs the served WASM bundle) — leave it for CI, but DO confirm the
  snippet ids/labels those specs may reference still exist. Report which specs
  reference snippets and whether their referenced ids/labels are preserved.

## Commit

Single atomic commit on `dev`:
`refactor(flow-site): load playground examples from static/examples + add ragtime snippet`
(end with the Co-Authored-By trailer).
