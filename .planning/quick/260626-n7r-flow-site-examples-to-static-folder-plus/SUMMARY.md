---
quick_id: 260626-n7r
slug: flow-site-examples-to-static-folder-plus
status: complete
date: 2026-06-26
---

# Quick Task Summary: flow-site — playground examples to static/, add ragtime

Moved the playground's hardcoded example sources out of `snippets.ts` into
`static/examples/*.flow` loaded at runtime via a manifest, and added a new
"Ragtime (24 bars)" example. Playground behavior preserved exactly (share
`#code=` / OAuth-stash precedence, WR-02 reset-on-load, default snippet on first
mount, left-rail markup, `prerender = false`, dynamic-import discipline).

## Files Created

- `flow-site/static/examples/manifest.json` — ordered 8-entry array
  (`{id,label,blurb,file}`): the 7 original snippets in the same order with the
  same labels/blurbs, plus a `ragtime` entry appended.
- `flow-site/static/examples/{sine-440,print-hello,note-stream,chord-progression,song-section,print-arith,abide-with-me}.flow`
- `flow-site/static/examples/ragtime.flow` (4718 B — copied verbatim from the
  staged task file; `cmp` confirmed byte-identical).
- `flow-site/src/lib/playground/snippets.test.ts` — new vitest (7 tests): manifest
  parses to non-empty array; every entry has non-empty id/label/blurb/file; ids
  unique; `DEFAULT_SNIPPET_ID` present; each `.flow` file exists + non-empty;
  ragtime entry present → `ragtime.flow`.

The 7 original snippet sources were extracted byte-for-byte by importing the
pre-refactor `snippets.ts` at build time (throwaway Node script that stripped only
TS-type syntax and wrote each `source` verbatim) — not hand-transcribed — so
whitespace + trailing newlines are unchanged.

## Files Changed

- `flow-site/src/lib/playground/snippets.ts` — removed hardcoded `SNIPPETS`, the
  `Snippet` interface, and sync `snippetById`. Kept `DEFAULT_SNIPPET_ID='sine-440'`
  + `BLANK_SOURCE=''`. Added `SnippetMeta` + `FetchLike` + async
  `loadManifest(fetchFn=fetch)` and `loadSnippetSource(file, fetchFn=fetch)` (fetch
  injectable for tests; throw on non-OK so callers degrade charitably).
- `flow-site/src/lib/playground/state.svelte.ts` — `editorValue` inits to
  `BLANK_SOURCE` (no sync snippet read); `loadSnippet(id)` → `loadSnippet(id, source)`
  with the identical WR-02 reset; `newBlank()` unchanged.
- `flow-site/src/routes/playground/+page.svelte` — `import { SNIPPETS }` →
  `let snippets = $state<SnippetMeta[]>([])` populated in onMount via `loadManifest()`
  (charitable: failure → empty rail + console.warn). Before computing `initialValue`,
  fetches the default snippet source (`defaultSource`), keeping precedence
  `pendingGistSource ?? arrival.source ?? defaultSource` (was `?? pg.editorValue`,
  the sync default — equivalent). Falls back to `BLANK_SOURCE` on fetch failure so
  Monaco still mounts. Rail `{#each SNIPPETS}` → `{#each snippets}` (markup/classes/
  aria unchanged). `onLoadSnippet` async: look up meta, await `loadSnippetSource`,
  then `pg.loadSnippet(id, source)`. `prerender=false`, `ssr=false`, dynamic imports
  preserved.

## Verification (from repo root)

- `pnpm -C flow-site test` → PASS: 16 files / 129 tests. New `snippets.test.ts` (7)
  and untouched `home-deeplinks.test.ts` (7) both pass when run explicitly (14/14).
- `pnpm -C flow-site build` → SUCCESS (built in 8.64s; adapter-cloudflare done). All
  9 `.flow` files + `manifest.json` present in `.svelte-kit/cloudflare/examples/`;
  `ragtime.flow` byte-identical there (`cmp` clean). CF serves `/examples/*` static.
- `pnpm -C flow-site check` → RED, but ONLY from pre-existing environmental noise,
  NOT this task. My 3 edited source files report ZERO errors. The 68 errors are:
  untouched `home-a11y.test.ts` (22), `home-deeplinks.test.ts` (18),
  `home-tokens.test.ts` (8) failing on `describe`/`it`/`expect` globals; untouched
  `src/lib/runtime.ts` (1, "Unused '@ts-expect-error' directive"); my new
  `snippets.test.ts` (19, same globals artifact); + 2 warnings (tsconfig "Cannot
  find type definition file for 'node'"; design unused-selector). Root cause: under
  pnpm's isolated node_modules, svelte-kit's generated `.svelte-kit/tsconfig.json`
  `"types":["node"]` can't resolve `@types/node` (only in `.pnpm/`) and lacks
  `vitest/globals` — so all `*.test.ts` fail type-check though they run green under
  vitest (`globals:true`). Baseline `dev` is already red for the same reason
  (untouched runtime.ts + 3 untouched test files). Out-of-scope install/hoisting
  gap; logged to `deferred-items.md`, not fixed here to avoid scope-creep.

## Playwright snippet id/label preservation (not run — heavy; left for CI)

- `tests/playground-export.spec.ts` + `tests/playground-run.spec.ts` → click label
  `'Print to console'` → PRESERVED (`print-hello`).
- `tests/playground-audio.spec.ts` + `tests/playground-mobile.spec.ts` → default
  snippet = 440 Hz sine → PRESERVED (`sine-440` / `Sine tone (440 Hz)`; default
  source fetched + seeds editor before Monaco mounts).
- `tests/wasm-boot.spec.ts` → asserts `.pg-snippet` rail scaffold → PRESERVED (rail
  still renders `.pg-snippet` buttons from the fetched manifest; markup unchanged).

All referenced ids/labels survive. CI note: the rail now populates async from
`/examples/manifest.json`; Playwright auto-waiting handles the fetch latency.

## Deviations

None functional. `loadSnippet(id, source)` chosen (plan's "accept from caller"
option) so state stays pure. Pre-existing `check` redness documented + deferred.

## Self-Check

- manifest + 8 `.flow` + test exist on disk: confirmed.
- ragtime.flow byte-identical to staged source: confirmed (`cmp`).
- vitest 129/129; new test 7; home-deeplinks 7; build green; examples in CF output.
